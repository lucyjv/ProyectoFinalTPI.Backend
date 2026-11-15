using ProyectoFinalTPI.Backend.Dtos.JuegoDecadas;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class JuegoDecadasServicio(IJuegoDecadasRepositorio repositorio) : IJuegoDecadasServicio
{
    private const string UsuarioFuente = "elNostalgico";
    private const string TituloExcluido = "Expo Proyecto";
    private static readonly DateTime Desde = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Hasta = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task<IReadOnlyList<FotoJuegoDecadasDto>> ObtenerFotos(CancellationToken cancellationToken = default)
    {
        var fotos = await repositorio.ObtenerFotos(
            UsuarioFuente, TituloExcluido, Desde, Hasta, cancellationToken);
        return fotos.Where(EsFotoValida)
            .Select(f => new FotoJuegoDecadasDto(f.PublicacionId, f.Url.Trim(), f.Fecha.Year / 10 * 10))
            .DistinctBy(f => f.Url, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool EsFotoValida(FotoCandidataJuegoDecadasDto foto) =>
        foto.PublicacionId > 0 && foto.Fecha >= Desde && foto.Fecha < Hasta
        && Uri.TryCreate(foto.Url?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
