using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class FotoPerfilCloudinaryAlmacenamiento : IFotoPerfilAlmacenamiento
{
    private readonly Lazy<Cloudinary> _cloudinary;

    public FotoPerfilCloudinaryAlmacenamiento(IConfiguration configuration)
    {
        _cloudinary = new Lazy<Cloudinary>(() =>
        {
            var cloudName = configuration["Cloudinary:CloudName"];
            var apiKey = configuration["Cloudinary:ApiKey"];
            var apiSecret = configuration["Cloudinary:ApiSecret"];
            if (string.IsNullOrWhiteSpace(cloudName) ||
                string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(apiSecret))
                throw new InvalidOperationException("Falta la configuración de Cloudinary.");
            return new Cloudinary(new Account(cloudName, apiKey, apiSecret));
        });
    }

    public async Task<string> SubirAsync(
        Stream archivo, string nombreArchivo, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resultado = await _cloudinary.Value.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(nombreArchivo, archivo),
            Folder = "nostalgiar/perfiles"
        });

        if (resultado.Error is not null || resultado.SecureUrl is null)
            throw new InvalidOperationException(resultado.Error?.Message ?? "Cloudinary no devolvió la URL de la foto.");

        return resultado.SecureUrl.ToString();
    }
}
