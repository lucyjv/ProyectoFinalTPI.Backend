using System.Globalization;
using System.Text;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio.Extensions;

namespace ProyectoFinalTPI.Backend.Servicio;

public class MomentoServicio(IMomentoRepositorio momentos, IPublicacionRepositorio publicaciones) : IMomentoServicio
{

    #region Operaciones públicas

    public Task<int> Crear(Momento momento, CancellationToken ct = default)
    {
        PrepararMomentoParaCreacion(momento);
        return momentos.Crear(momento, ct);
    }
    public Task CambiarPadre(int id, int? padreId, CancellationToken ct = default)
    {
        ValidarCambioDePadre(id, padreId);
        // El repositorio valida la cadena dentro del bloqueo de jerarquía, sin carreras entre lecturas y escritura.
        return momentos.CambiarPadre(id, padreId, ct);
    }
    public Task AgregarAlias(int id, string texto, CancellationToken ct = default)
    {
        id.ValidarId("alias");
        return momentos.AgregarAlias(CrearAlias(id, texto), ct);
    }
    public async Task<bool> AsociarAutomaticamente(int publicacionId, int momentoId, double confianza, string versionAnalisis, CancellationToken ct = default)
    {
        ValidarConfianza(confianza);
        versionAnalisis = versionAnalisis.ValidarTexto(100, "versión del análisis");
        await ValidarAsociacion(publicacionId, momentoId, ct);
        return await momentos.GuardarAsociacion(CrearAsociacion(
            publicacionId, momentoId,
            OrigenAsociacionMomento.Automatico,
            confianza, versionAnalisis
        ), ct);
    }
    public async Task AsociarPorAutor(int publicacionId, int momentoId, int usuarioId, CancellationToken ct = default)
    {
        usuarioId.ValidarId("usuario");
        var publicacion = await ValidarAsociacion(publicacionId, momentoId, ct);
        ValidarAutor(publicacion, usuarioId); 
        await momentos.GuardarAsociacion(
            CrearAsociacion(
                publicacionId, momentoId,
                OrigenAsociacionMomento.Autor),
            ct);
    }
    public async Task<List<Momento>> ObtenerMomentosPublicacion(int publicacionId, CancellationToken ct = default)
    {
        await ObtenerRecuerdo(publicacionId, ct);
        return await momentos.ObtenerMomentosPublicacion(publicacionId, ct);
    }

    #endregion

    #region Métodos privados y validaciones
    private static void PrepararMomentoParaCreacion(Momento momento)
    {
        ArgumentNullException.ThrowIfNull(momento);
        ValidarEstadoInicial(momento);
        NormalizarDatos(momento);
        ValidarFechas(momento);
        if (momento.MomentoPadreId.HasValue)
            momento.MomentoPadreId.Value.ValidarId("momento");
        momento.FechaCreacion = DateTime.UtcNow;
    }
    private static void ValidarEstadoInicial(Momento momento)
    {
        if (momento.Id != 0 ||
            momento.Hijos.Count != 0 ||
            momento.Alias.Count != 0 ||
            momento.Publicaciones.Count != 0 ||
            momento.MomentoPadre != null ||
            momento.MomentoCanonicoId != null ||
            momento.MomentoCanonico != null ||
            momento.ClaveIdentidad != null ||
            momento.VersionCatalogo != 1)
            throw new ArgumentException(
                "Crear el momento sin entidades anidadas ni un Id asignado.");
    }
    private static void NormalizarDatos(Momento momento)
    {
        momento.Nombre = momento.Nombre.ValidarTexto(200, "nombre");
        momento.Descripcion = momento.Descripcion.ValidarTexto(2000, "descripción", minimo:0);
    }
    private static void ValidarFechas(Momento momento)
    {
        if (momento.FechaInicio.HasValue &&
            momento.FechaFin.HasValue &&
            momento.FechaFin < momento.FechaInicio)
            throw new ArgumentException(
                "La fecha final debe ser igual o posterior a la inicial.");
    }
    private static void ValidarCambioDePadre(int id, int? padreId)
    {
        id.ValidarId("momento");
        if (padreId.HasValue) padreId.Value.ValidarId("momento padre");
        if (id == padreId)
            throw new ArgumentException(
                "Un momento no puede ser su propio padre.");
    }
    private static MomentoAlias CrearAlias(int id, string texto)
    {
        var validado = texto.ValidarTexto(200, "alias");
        return new MomentoAlias
        {
            MomentoId = id,
            Texto = validado,
            TextoNormalizado = NormalizarAlias(validado)
        };
    }
    private static string NormalizarAlias(string texto)
    {
        var sinMarcas = new string(
            texto.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) !=
                    UnicodeCategory.NonSpacingMark)
                .ToArray());

        var normalizado = string.Join(
            " ",
            sinMarcas.ToLowerInvariant().Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries))
            .Normalize(NormalizationForm.FormC);

        if (normalizado.Length == 0 || normalizado.Length > 200)
            throw new ArgumentException(
                "El alias normalizado no es válido.");
        return normalizado;
    }
    private async Task<Publicacion> ValidarAsociacion(int publicacionId, int momentoId, CancellationToken ct)
    {
        momentoId.ValidarId("momento");
        var publicacion = await ObtenerRecuerdo(publicacionId, ct);
        if (await momentos.ObtenerPorId(momentoId, ct) == null) throw new KeyNotFoundException("El momento no existe.");
        return publicacion;
    }
    private async Task<Publicacion> ObtenerRecuerdo(int id, CancellationToken ct)
    {
        id.ValidarId("publicación");
        var publicacion = await publicaciones.ObtenerPorId(id, ct);
        if (publicacion == null || publicacion.EstaOculto) throw new KeyNotFoundException("La publicación no existe o no está disponible.");
        if (publicacion.PublicacionOriginalId.HasValue) throw new ArgumentException("Las asociaciones se realizan sobre el recuerdo original, no sobre un repost.");
        return publicacion;
    }
    private static PublicacionMomento CrearAsociacion(
        int publicacionId,
        int momentoId,
        OrigenAsociacionMomento origen,
        double? confianza = null,
        string? versionAnalisis = null) =>
        new()
        {
            PublicacionId = publicacionId,
            MomentoId = momentoId,
            Origen = origen,
            Confianza = confianza,
            VersionAnalisis = versionAnalisis
        };
    private static void ValidarConfianza(double confianza)
    {
        if (!double.IsFinite(confianza) || confianza < 0 || confianza > 1)
            throw new ArgumentException("La confianza debe estar entre 0 y 1.");
    }
    private static void ValidarAutor(
        Publicacion publicacion, int usuarioId)
    {
        if (publicacion.UsuarioId != usuarioId)
            throw new UnauthorizedAccessException(
                "Solo el autor puede asociar su recuerdo.");
    }

    #endregion
}
