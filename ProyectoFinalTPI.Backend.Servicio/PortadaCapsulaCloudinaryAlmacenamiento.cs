using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using System.Text.RegularExpressions;

namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class PortadaCapsulaCloudinaryAlmacenamiento : IPortadaCapsulaAlmacenamiento
{
    private readonly Lazy<Cloudinary> _cloudinary;
    private readonly HttpClient _http;
    private static readonly Regex RecursoValido = new(@"^nostalgiar/capsulas/[a-f0-9]{32}$", RegexOptions.CultureInvariant);

    public PortadaCapsulaCloudinaryAlmacenamiento(IConfiguration configuration, HttpClient http)
    {
        _http = http;
        _cloudinary = new Lazy<Cloudinary>(() =>
        {
            var nombre = configuration["Cloudinary:CloudName"];
            var clave = configuration["Cloudinary:ApiKey"];
            var secreto = configuration["Cloudinary:ApiSecret"];
            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(clave) || string.IsNullOrWhiteSpace(secreto))
                throw new InvalidOperationException("Falta la configuración de Cloudinary.");
            return new Cloudinary(new Account(nombre, clave, secreto));
        });
    }

    public async Task<string> Subir(Stream archivo, CancellationToken ct)
    {
        var id = $"nostalgiar/capsulas/{Guid.NewGuid():N}";
        var resultado = await _cloudinary.Value.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription("portada", archivo), PublicId = id, Type = "authenticated", Format = "webp",
            Overwrite = false, Transformation = new Transformation().Width(1600).Height(1600).Crop("limit").Quality("auto").Flags("strip_profile")
        }, ct);
        if (resultado.Error is not null) throw new IOException("No se pudo subir la portada a Cloudinary.");
        if (resultado.PublicId != id) throw new IOException("Cloudinary no confirmó la portada esperada.");
        return id;
    }

    public async Task<byte[]> Leer(string recursoId, CancellationToken ct)
    {
        ValidarRecurso(recursoId);
        // La URL firmada nunca sale del backend. Cada descarga vuelve a validar el acceso a la cápsula.
        var url = _cloudinary.Value.Api.UrlImgUp.Secure(true).Signed(true).Type("authenticated").BuildUrl(recursoId + ".webp");
        using var respuesta = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        respuesta.EnsureSuccessStatusCode();
        await using var entrada = await respuesta.Content.ReadAsStreamAsync(ct);
        using var salida = new MemoryStream();
        var buffer = new byte[81920];
        int leidos;
        while ((leidos = await entrada.ReadAsync(buffer, ct)) > 0)
        {
            if (salida.Length + leidos > 10 * 1024 * 1024) throw new IOException("La portada recibida supera el tamaño permitido.");
            await salida.WriteAsync(buffer.AsMemory(0, leidos), ct);
        }
        return salida.ToArray();
    }

    public async Task Eliminar(string recursoId, CancellationToken ct)
    {
        ValidarRecurso(recursoId);
        ct.ThrowIfCancellationRequested();
        var resultado = await _cloudinary.Value.DestroyAsync(new DeletionParams(recursoId) { Type = "authenticated", Invalidate = true });
        if (resultado.Error is not null || resultado.Result is not ("ok" or "not found"))
            throw new IOException("No se pudo eliminar la portada anterior de Cloudinary.");
    }

    private static void ValidarRecurso(string id)
    {
        if (!RecursoValido.IsMatch(id)) throw new ArgumentException("La referencia de portada no es válida.");
    }
}
