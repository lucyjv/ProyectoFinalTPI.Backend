using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.Grupo;
using ProyectoFinalTPI.Backend.Dtos.Lugar;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public class GrupoRepositorio(ApplicationDbContext context) : IGrupoRepositorio
{
    public Task<List<Grupo>> ObtenerTodos(CancellationToken cancellationToken = default) =>
        Detalles()
            .OrderByDescending(grupo => grupo.FechaCreacion)
            .ThenByDescending(grupo => grupo.Id)
            .ToListAsync(cancellationToken);

    public Task<List<Grupo>> Buscar(
        string texto, int limite, CancellationToken cancellationToken = default)
    {
        var textoNormalizado = texto.Trim().ToLower();
        return Detalles()
            .Where(grupo =>
                grupo.Nombre.ToLower().Contains(textoNormalizado) ||
                grupo.Descripcion.ToLower().Contains(textoNormalizado))
            .OrderByDescending(grupo => grupo.Nombre.ToLower().StartsWith(textoNormalizado))
            .ThenBy(grupo => grupo.Nombre)
            .ThenByDescending(grupo => grupo.FechaCreacion)
            .Take(limite)
            .ToListAsync(cancellationToken);
    }

    public Task<Grupo?> ObtenerPorId(int grupoId, CancellationToken cancellationToken = default) =>
        Detalles().SingleOrDefaultAsync(grupo => grupo.Id == grupoId, cancellationToken);

    public async Task<int> Crear(Grupo grupo, CancellationToken cancellationToken = default)
    {
        await context.Grupos.AddAsync(grupo, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return grupo.Id;
    }

    public async Task Actualizar(Grupo grupo, CancellationToken cancellationToken = default)
    {
        context.Grupos.Update(grupo);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Eliminar(Grupo grupo, CancellationToken cancellationToken = default)
    {
        context.Grupos.Remove(grupo);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> EsIntegrante(int grupoId, int usuarioId, CancellationToken cancellationToken = default) =>
        context.GruposIntegrantes.AsNoTracking().AnyAsync(
            integrante => integrante.GrupoId == grupoId && integrante.UsuarioId == usuarioId,
            cancellationToken);

    public Task<GrupoIntegrante?> ObtenerIntegrante(
        int grupoId, int usuarioId, CancellationToken cancellationToken = default) =>
        context.GruposIntegrantes.SingleOrDefaultAsync(
            integrante => integrante.GrupoId == grupoId && integrante.UsuarioId == usuarioId,
            cancellationToken);

    public async Task AgregarIntegrante(
        GrupoIntegrante integrante, CancellationToken cancellationToken = default)
    {
        await context.GruposIntegrantes.AddAsync(integrante, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task QuitarIntegrante(
        GrupoIntegrante integrante, CancellationToken cancellationToken = default)
    {
        context.GruposIntegrantes.Remove(integrante);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<List<GrupoIntegrante>> ObtenerIntegrantes(
        int grupoId, CancellationToken cancellationToken = default) =>
        context.GruposIntegrantes
            .AsNoTracking()
            .Include(integrante => integrante.Usuario)
            .Where(integrante => integrante.GrupoId == grupoId)
            .OrderBy(integrante => integrante.FechaUnion)
            .ThenBy(integrante => integrante.UsuarioId)
            .ToListAsync(cancellationToken);

    public Task<List<PublicacionDetalleDto>> ObtenerRecuerdos(
        int grupoId, CancellationToken cancellationToken = default) =>
        context.Publicaciones
            .AsNoTracking()
            .Where(publicacion => publicacion.GrupoId == grupoId && !publicacion.EstaOculto)
            .OrderByDescending(publicacion => publicacion.FechaCreación)
            .ThenByDescending(publicacion => publicacion.Id)
            .Select(publicacion => new PublicacionDetalleDto
            {
                Id = publicacion.Id,
                Titulo = publicacion.Titulo,
                Descripcion = publicacion.Descripcion,
                Fecha = publicacion.Fecha,
                FechaCreacion = publicacion.FechaCreación,
                Categoria = publicacion.Categoria,
                UrlMultimedia = publicacion.UrlMultimedia,
                TipoMultimedia = publicacion.TipoMultimedia,
                Autor = new AutorResumenDto
                {
                    Id = publicacion.UsuarioId,
                    Username = publicacion.Usuario.Username
                },
                Lugar = new LugarDetalleDto
                {
                    Id = publicacion.LugarId,
                    Nombre = publicacion.Lugar.Nombre,
                    Direccion = publicacion.Lugar.Direccion,
                    Latitud = publicacion.Lugar.Coordenadas.Y,
                    Longitud = publicacion.Lugar.Coordenadas.X
                },
                CantidadComentarios = publicacion.Comentarios.Count(comentario =>
                    !comentario.EstaOculto && !comentario.EstaEliminado),
                Grupo = new GrupoResumenDto
                {
                    Id = publicacion.Grupo!.Id,
                    Nombre = publicacion.Grupo.Nombre,
                    Descripcion = publicacion.Grupo.Descripcion,
                    Decada = publicacion.Grupo.Decada,
                    FechaCreacion = publicacion.Grupo.FechaCreacion,
                    Creador = new AutorResumenDto
                    {
                        Id = publicacion.Grupo.CreadorId,
                        Username = publicacion.Grupo.Creador.Username
                    },
                    CantidadIntegrantes = publicacion.Grupo.Integrantes.Count
                }
            })
            .ToListAsync(cancellationToken);

    private IQueryable<Grupo> Detalles() =>
        context.Grupos
            .AsNoTracking()
            .Include(grupo => grupo.Creador)
            .Include(grupo => grupo.Integrantes);
}
