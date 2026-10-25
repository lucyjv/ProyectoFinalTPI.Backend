using System.Globalization;
using System.Text;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public class MomentoServicio(IMomentoRepositorio momentos, IPublicacionRepositorio publicaciones) : IMomentoServicio
{
    public Task<int> Crear(Momento momento, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(momento);
        if (momento.Id != 0 || momento.Hijos.Count != 0 || momento.Alias.Count != 0 || momento.Publicaciones.Count != 0 || momento.MomentoPadre != null ||
            momento.MomentoCanonicoId != null || momento.MomentoCanonico != null || momento.ClaveIdentidad != null || momento.VersionCatalogo != 1)
            throw new ArgumentException("Crear el momento sin entidades anidadas ni un Id asignado.");
        momento.Nombre = ValidarTexto(momento.Nombre, 200, "nombre");
        momento.Descripcion = (momento.Descripcion ?? "").Trim();
        if (momento.Descripcion.Length > 2000) throw new ArgumentException("La descripción admite hasta 2000 caracteres.");
        if (momento.FechaInicio.HasValue && momento.FechaFin.HasValue && momento.FechaFin < momento.FechaInicio)
            throw new ArgumentException("La fecha final debe ser igual o posterior a la inicial.");
        if (momento.MomentoPadreId.HasValue) ValidarId(momento.MomentoPadreId.Value);
        momento.FechaCreacion = DateTime.UtcNow;
        return momentos.Crear(momento, ct);
    }

    public Task CambiarPadre(int id, int? padreId, CancellationToken ct = default)
    {
        ValidarId(id);
        if (padreId.HasValue) ValidarId(padreId.Value);
        if (id == padreId) throw new ArgumentException("Un momento no puede ser su propio padre.");
        // El repositorio valida la cadena dentro del bloqueo de jerarquía, sin carreras entre lecturas y escritura.
        return momentos.CambiarPadre(id, padreId, ct);
    }

    public Task AgregarAlias(int id, string texto, CancellationToken ct = default)
    {
        ValidarId(id);
        texto = ValidarTexto(texto, 200, "alias");
        var sinMarcas = new string(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        var normalizado = string.Join(" ", sinMarcas.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Normalize(NormalizationForm.FormC);
        if (normalizado.Length == 0 || normalizado.Length > 200) throw new ArgumentException("El alias normalizado no es válido.");
        return momentos.AgregarAlias(new MomentoAlias { MomentoId = id, Texto = texto, TextoNormalizado = normalizado }, ct);
    }

    public async Task<bool> AsociarAutomaticamente(int publicacionId, int momentoId, double confianza, string versionAnalisis, CancellationToken ct = default)
    {
        if (!double.IsFinite(confianza) || confianza < 0 || confianza > 1)
            throw new ArgumentException("La confianza debe estar entre 0 y 1.");
        versionAnalisis = ValidarTexto(versionAnalisis, 100, "versión del análisis");
        await ValidarAsociacion(publicacionId, momentoId, ct);
        return await momentos.GuardarAsociacion(new PublicacionMomento
        {
            PublicacionId = publicacionId, MomentoId = momentoId,
            Origen = OrigenAsociacionMomento.Automatico, Confianza = confianza, VersionAnalisis = versionAnalisis
        }, ct);
    }

    public async Task AsociarPorAutor(int publicacionId, int momentoId, int usuarioId, CancellationToken ct = default)
    {
        ValidarId(usuarioId);
        var publicacion = await ValidarAsociacion(publicacionId, momentoId, ct);
        if (publicacion.UsuarioId != usuarioId) throw new UnauthorizedAccessException("Solo el autor puede asociar su recuerdo.");
        await momentos.GuardarAsociacion(new PublicacionMomento
        {
            PublicacionId = publicacionId, MomentoId = momentoId, Origen = OrigenAsociacionMomento.Autor
        }, ct);
    }

    public async Task<List<Momento>> ObtenerMomentosPublicacion(int publicacionId, CancellationToken ct = default)
    {
        await ObtenerRecuerdo(publicacionId, ct);
        return await momentos.ObtenerMomentosPublicacion(publicacionId, ct);
    }

    private async Task<Publicacion> ValidarAsociacion(int publicacionId, int momentoId, CancellationToken ct)
    {
        ValidarId(momentoId);
        var publicacion = await ObtenerRecuerdo(publicacionId, ct);
        if (await momentos.ObtenerPorId(momentoId, ct) == null) throw new KeyNotFoundException("El momento no existe.");
        return publicacion;
    }

    private async Task<Publicacion> ObtenerRecuerdo(int id, CancellationToken ct)
    {
        ValidarId(id);
        var publicacion = await publicaciones.ObtenerPorId(id, ct);
        if (publicacion == null || publicacion.EstaOculto) throw new KeyNotFoundException("La publicación no existe o no está disponible.");
        if (publicacion.PublicacionOriginalId.HasValue) throw new ArgumentException("Las asociaciones se realizan sobre el recuerdo original, no sobre un repost.");
        return publicacion;
    }

    private static void ValidarId(int id)
    {
        if (id <= 0) throw new ArgumentException("El identificador debe ser positivo.");
    }

    private static string ValidarTexto(string? texto, int maximo, string nombre)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length > maximo)
            throw new ArgumentException($"El {nombre} es obligatorio y admite hasta {maximo} caracteres.");
        return texto.Trim();
    }
}
