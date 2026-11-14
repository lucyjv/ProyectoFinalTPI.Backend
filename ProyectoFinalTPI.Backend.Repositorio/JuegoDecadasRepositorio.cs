using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.JuegoDecadas;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public sealed class JuegoDecadasRepositorio(ApplicationDbContext context) : IJuegoDecadasRepositorio
{
    public Task<List<FotoCandidataJuegoDecadasDto>> ObtenerFotos(
        string usuario, string tituloExcluido, DateTime desde, DateTime hasta,
        CancellationToken cancellationToken = default)
    {
        var publicaciones = context.Publicaciones.AsNoTracking()
            .Where(p => !p.EstaOculto && !p.Usuario.EstaEliminado && p.PublicacionOriginalId == null
                && EF.Functions.ILike(p.Usuario.Username, usuario)
                && !EF.Functions.ILike(p.Titulo, "%" + tituloExcluido + "%")
                && p.Fecha >= desde && p.Fecha < hasta);

        var adjuntos = publicaciones.SelectMany(p => p.Adjuntos
            .Where(a => a.Tipo == MultimediaEnum.Foto && a.Url != "")
            .Select(a => new { PublicacionId = p.Id, a.Url, p.Fecha }));

        // Las publicaciones anteriores a los adjuntos conservan su foto principal.
        // Si existe una colección de adjuntos, esa colección es la fuente de verdad.
        var anteriores = publicaciones
            .Where(p => !p.Adjuntos.Any() && p.TipoMultimedia == MultimediaEnum.Foto
                && p.UrlMultimedia != "")
            .Select(p => new { PublicacionId = p.Id, Url = p.UrlMultimedia, p.Fecha });

        return adjuntos.Concat(anteriores)
            .OrderBy(f => f.PublicacionId).ThenBy(f => f.Url)
            .Select(f => new FotoCandidataJuegoDecadasDto(f.PublicacionId, f.Url, f.Fecha))
            .ToListAsync(cancellationToken);
    }
}
