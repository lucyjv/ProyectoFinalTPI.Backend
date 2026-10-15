using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Dtos.Lugar;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio
{
    public class PublicacionRepositorio : IPublicacionRepositorio
    {
        private readonly ApplicationDbContext _context;

        public PublicacionRepositorio(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Publicacion> ObtenerTodasPublicaciones()
        {
            return _context.Publicaciones
                           .Where(p => !p.EstaOculto)
                           .Include(p => p.Usuario)
                           .ToList();
        }

        public void GuardarPublicacion(Publicacion publicacion)
        {
            _context.Publicaciones.Add(publicacion);
            _context.SaveChanges();
        }

        public List<Publicacion> ObtenerPublicacionPorCategoria(CategoriaEnum categoria)
        {
            return _context.Publicaciones
                           .Where(p => p.Categoria == categoria && !p.EstaOculto)
                           .ToList();
        }

        public async Task<int> Crear(Publicacion publicacion)
        {
            await _context.Publicaciones.AddAsync(publicacion);
            await _context.SaveChangesAsync();
            return publicacion.Id;
        }

        public async Task<int> CrearComentario(Comentario comentario, CancellationToken cancellationToken = default)
        {
            await _context.Set<Comentario>().AddAsync(comentario, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return comentario.Id;
        }

        public async Task<Publicacion?> ObtenerPorId(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Publicaciones
                .Include(p => p.Lugar)
                .FirstOrDefaultAsync(p => p.Id == id && !p.EstaOculto, cancellationToken);
        }

        public async Task<List<PublicacionPinDto>> ObtenerPines(PublicacionFiltroDto filtro, int limite, CancellationToken cancellationToken = default)
        {
            var consulta = _context.Publicaciones
                .AsNoTracking()
                .Where(p => !p.EstaOculto)
                .Where(p =>
                p.Lugar.Coordenadas.Y >= filtro.MinLatitud &&
                p.Lugar.Coordenadas.Y <= filtro.MaxLatitud &&
                p.Lugar.Coordenadas.X >= filtro.MinLongitud &&
                p.Lugar.Coordenadas.X <= filtro.MaxLongitud);

            if (filtro.Categoria.HasValue)
            {
                consulta = consulta.Where(
                    p => p.Categoria == filtro.Categoria.Value);
            }

            if (filtro.FechaDesde.HasValue)
            {
                consulta = consulta.Where(
                    p => p.Fecha >= filtro.FechaDesde.Value);
            }

            if (filtro.FechaHasta.HasValue)
            {
                consulta = consulta.Where(
                    p => p.Fecha < filtro.FechaHasta.Value);
            }

            return await consulta
                .OrderBy(p => p.Id)
                .Select(p => new PublicacionPinDto
                {
                    Id = p.Id,
                    Latitud = p.Lugar.Coordenadas.Y,
                    Longitud = p.Lugar.Coordenadas.X,
                    Categoria = p.Categoria
                })
                .Take(limite + 1)
                .ToListAsync(cancellationToken);
        }

        public async Task<PublicacionResumenDto?> ObtenerResumenPorId(int id, CancellationToken cancellationToken = default)
        {
            const int longitudMaximaDescripcion = 180;

            return await _context.Publicaciones
                .AsNoTracking()
                .Where(p => p.Id == id && !p.EstaOculto)
                .Select(p => new PublicacionResumenDto
                {
                    Id = p.Id,
                    Titulo = p.Titulo,

                    DescripcionBreve = p.Descripcion.Length > longitudMaximaDescripcion
                    ? p.Descripcion.Substring(
                        0, longitudMaximaDescripcion) + "..."
                        : p.Descripcion,

                    Fecha = p.Fecha,
                    Categoria = p.Categoria,
                    UrlMultimedia = p.UrlMultimedia,
                    TipoMultimedia = p.TipoMultimedia,

                    Autor = new AutorResumenDto
                    {
                        Id = p.UsuarioId,
                        Username = p.Usuario.Username
                    }
                })
                .SingleOrDefaultAsync(cancellationToken);

        }

        public async Task<PublicacionDetalleDto?> ObtenerDetallePorId(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Publicaciones
                .AsNoTracking()
                .Where(p => p.Id == id && !p.EstaOculto)
                .Select(p => new PublicacionDetalleDto
                {
                    Id = p.Id,
                    Titulo = p.Titulo,
                    Descripcion = p.Descripcion,

                    Fecha = p.Fecha,
                    FechaCreacion = p.FechaCreación,
                    Categoria = p.Categoria,

                    UrlMultimedia = p.UrlMultimedia,
                    TipoMultimedia = p.TipoMultimedia,

                    Autor = new AutorResumenDto
                    {
                        Id = p.UsuarioId,
                        Username = p.Usuario.Username
                    },

                    Lugar = new LugarDetalleDto
                    {
                        Id = p.LugarId,
                        Nombre = p.Lugar.Nombre,
                        Direccion = p.Lugar.Direccion,
                        Latitud = p.Lugar.Coordenadas.Y,
                        Longitud = p.Lugar.Coordenadas.X
                    },

                    // TODO: implementar el contador de likes y la interacción del usuario.
                    CantidadLikes = 0,
                    CantidadComentarios = p.Comentarios.Count(c => !c.EstaOculto),
                    DioLike = false
                })
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<List<PublicacionDetalleDto>> ObtenerFeedSeguidos(IReadOnlyCollection<int> autoresIds, CursorCronologicoDto? cursor, int limite,
        CancellationToken cancellationToken = default)
        {
            if (autoresIds.Count == 0)
            {
                return new List<PublicacionDetalleDto>();
            }

            const int longitudMaximaDescripcion = 300;

            var consulta = _context.Publicaciones
                .AsNoTracking()
                .Where(p =>
                    !p.EstaOculto &&
                    autoresIds.Contains(p.UsuarioId));

            if (cursor != null)
            {
                consulta = consulta.Where(p =>
                    p.FechaCreación < cursor.FechaCreacion ||
                    (p.FechaCreación == cursor.FechaCreacion &&
                     p.Id < cursor.Id));
            }

            return await consulta
                .OrderByDescending(p => p.FechaCreación)
                .ThenByDescending(p => p.Id)
                .Select(p => new PublicacionDetalleDto
                {
                    Id = p.Id,
                    Titulo = p.Titulo,

                    Descripcion =
                        p.Descripcion.Length > longitudMaximaDescripcion
                            ? p.Descripcion.Substring(
                                0, longitudMaximaDescripcion - 3) + "..."
                            : p.Descripcion,

                    Fecha = p.Fecha,
                    FechaCreacion = p.FechaCreación,
                    Categoria = p.Categoria,

                    UrlMultimedia = p.UrlMultimedia,
                    TipoMultimedia = p.TipoMultimedia,

                    Autor = new AutorResumenDto
                    {
                        Id = p.UsuarioId,
                        Username = p.Usuario.Username
                    },

                    Lugar = new LugarDetalleDto
                    {
                        Id = p.LugarId,
                        Nombre = p.Lugar.Nombre,
                        Direccion = p.Lugar.Direccion,
                        Latitud = p.Lugar.Coordenadas.Y,
                        Longitud = p.Lugar.Coordenadas.X
                    },

                    // TODO: implementar el contador de likes y la interacción del usuario.
                    CantidadLikes = 0,
                    CantidadComentarios = p.Comentarios.Count(c => !c.EstaOculto),
                    DioLike = false
                })
                .Take(limite + 1)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<ComentarioDto>> ObtenerComentarios(int publicacionId, CursorCronologicoDto? cursor, int limite, CancellationToken cancellationToken = default)
        {
            var consulta = _context.Publicaciones
                .AsNoTracking()
                .Where(p =>
                    p.Id == publicacionId &&
                    !p.EstaOculto)
                .SelectMany(p => p.Comentarios)
                .Where(c => !c.EstaOculto);

            if (cursor != null)
            {
                consulta = consulta.Where(c =>
                    c.FechaCreacion < cursor.FechaCreacion ||
                    (c.FechaCreacion == cursor.FechaCreacion &&
                     c.Id < cursor.Id));
            }

            return await consulta
                .OrderByDescending(c => c.FechaCreacion)
                .ThenByDescending(c => c.Id)
                .Select(c => new ComentarioDto
                {
                    Id = c.Id,
                    Contenido = c.Contenido,
                    FechaCreacion = c.FechaCreacion,

                    Autor = new AutorResumenDto
                    {
                        Id = c.Usuario.Id,
                        Username = c.Usuario.Username
                    }
                })
                .Take(limite + 1)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<ComentarioDto>> ObtenerHilosComentarios(
            int publicacionId, CursorCronologicoDto? cursor, int limite, int? usuarioId,
            CancellationToken cancellationToken = default)
        {
            var consulta = _context.Comentarios
                .AsNoTracking()
                .AsSplitQuery()
                .Where(c => c.PublicacionId == publicacionId && c.ComentarioPadreId == null && !c.EstaOculto);

            if (cursor != null)
            {
                consulta = consulta.Where(c =>
                    c.FechaCreacion < cursor.FechaCreacion ||
                    (c.FechaCreacion == cursor.FechaCreacion && c.Id < cursor.Id));
            }

            var comentarios = await consulta
                .OrderByDescending(c => c.FechaCreacion)
                .ThenByDescending(c => c.Id)
                .Include(c => c.Usuario)
                .Include(c => c.Reacciones)
                .Include(c => c.Respuestas.Where(r => !r.EstaOculto).OrderBy(r => r.FechaCreacion))
                    .ThenInclude(r => r.Usuario)
                .Include(c => c.Respuestas.Where(r => !r.EstaOculto).OrderBy(r => r.FechaCreacion))
                    .ThenInclude(r => r.Reacciones)
                .Take(limite + 1)
                .ToListAsync(cancellationToken);

            return comentarios.Select(c => MapearComentario(c, usuarioId, incluirRespuestas: true)).ToList();
        }

        public Task<Comentario?> ObtenerComentario(int publicacionId, int comentarioId, CancellationToken cancellationToken = default) =>
            _context.Comentarios.FirstOrDefaultAsync(c =>
                c.Id == comentarioId && c.PublicacionId == publicacionId && !c.EstaOculto, cancellationToken);

        public async Task<ComentarioDto?> ObtenerComentarioDto(
            int publicacionId, int comentarioId, int? usuarioId, CancellationToken cancellationToken = default)
        {
            var comentario = await _context.Comentarios
                .AsNoTracking()
                .Include(c => c.Usuario)
                .Include(c => c.Reacciones)
                .FirstOrDefaultAsync(c => c.Id == comentarioId && c.PublicacionId == publicacionId && !c.EstaOculto, cancellationToken);
            return comentario == null ? null : MapearComentario(comentario, usuarioId, incluirRespuestas: false);
        }

        public async Task EstablecerReaccionComentario(
            int comentarioId, int usuarioId, TipoReaccionComentario tipo, CancellationToken cancellationToken = default)
        {
            var reaccion = await _context.ReaccionesComentario.FindAsync(new object[] { comentarioId, usuarioId }, cancellationToken);
            if (reaccion == null)
            {
                _context.ReaccionesComentario.Add(new ReaccionComentario
                {
                    ComentarioId = comentarioId,
                    UsuarioId = usuarioId,
                    Tipo = tipo,
                    FechaCreacion = DateTime.UtcNow
                });
            }
            else
            {
                reaccion.Tipo = tipo;
                reaccion.FechaCreacion = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task QuitarReaccionComentario(int comentarioId, int usuarioId, CancellationToken cancellationToken = default)
        {
            var reaccion = await _context.ReaccionesComentario.FindAsync(new object[] { comentarioId, usuarioId }, cancellationToken);
            if (reaccion != null)
            {
                _context.ReaccionesComentario.Remove(reaccion);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private static ComentarioDto MapearComentario(Comentario comentario, int? usuarioId, bool incluirRespuestas)
        {
            var dto = new ComentarioDto
            {
                Id = comentario.Id,
                PublicacionId = comentario.PublicacionId,
                ComentarioPadreId = comentario.ComentarioPadreId,
                Contenido = comentario.Contenido,
                FechaCreacion = comentario.FechaCreacion,
                Autor = new AutorResumenDto { Id = comentario.UsuarioId, Username = comentario.Usuario.Username },
                Reacciones = comentario.Reacciones
                    .GroupBy(r => r.Tipo.ToString().ToLowerInvariant())
                    .ToDictionary(grupo => grupo.Key, grupo => grupo.Count()),
                ReaccionActual = usuarioId.HasValue
                    ? comentario.Reacciones.FirstOrDefault(r => r.UsuarioId == usuarioId.Value)?.Tipo.ToString().ToLowerInvariant()
                    : null
            };
            if (incluirRespuestas)
            {
                dto.Respuestas = comentario.Respuestas
                    .Where(r => !r.EstaOculto)
                    .OrderBy(r => r.FechaCreacion)
                    .ThenBy(r => r.Id)
                    .Select(r => MapearComentario(r, usuarioId, incluirRespuestas: false))
                    .ToList();
            }
            return dto;
        }

        public Task<bool> ExistePublicacionVisible(int id, CancellationToken cancellationToken = default)
        {
            return _context.Publicaciones
                .AnyAsync(
                    p => p.Id == id && !p.EstaOculto,
                    cancellationToken);
        }

        public async Task<List<PublicacionDetalleDto>> ObtenerPublicacionesParaVos(IReadOnlyCollection<CategoriaEnum> categorias, DateTime fechaCorte,
        CursorCronologicoDto? cursor, int limite, CancellationToken cancellationToken = default)
        {
            var consulta = _context.Publicaciones
                .AsNoTracking()
                .Where(p =>
                    !p.EstaOculto &&
                    p.FechaCreación <= fechaCorte);

            if (categorias.Count > 0)
            {
                consulta = consulta.Where(
                    p => categorias.Contains(p.Categoria));
            }

            if (cursor != null)
            {
                consulta = consulta.Where(p =>
                    p.FechaCreación < cursor.FechaCreacion ||
                    (p.FechaCreación == cursor.FechaCreacion &&
                     p.Id < cursor.Id));
            }

            return await consulta
                .OrderByDescending(p => p.FechaCreación)
                .ThenByDescending(p => p.Id)
                .Select(p => new PublicacionDetalleDto
                {
                    Id = p.Id,
                    Titulo = p.Titulo,

                    Descripcion = p.Descripcion.Length > 300
                        ? p.Descripcion.Substring(0, 297) + "..."
                        : p.Descripcion,

                    Fecha = p.Fecha,
                    FechaCreacion = p.FechaCreación,
                    Categoria = p.Categoria,

                    UrlMultimedia = p.UrlMultimedia,
                    TipoMultimedia = p.TipoMultimedia,

                    Autor = new AutorResumenDto
                    {
                        Id = p.UsuarioId,
                        Username = p.Usuario.Username
                    },

                    Lugar = new LugarDetalleDto
                    {
                        Id = p.LugarId,
                        Nombre = p.Lugar.Nombre,
                        Direccion = p.Lugar.Direccion,
                        Latitud = p.Lugar.Coordenadas.Y,
                        Longitud = p.Lugar.Coordenadas.X
                    },

                    // TODO: implementar el contador de likes y la interacción del usuario.
                    CantidadLikes = 0,
                    CantidadComentarios = p.Comentarios.Count(c => !c.EstaOculto),
                    DioLike = false
                })
                .Take(limite + 1)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<PublicacionDetalleDto>> ObtenerPublicacionesPorIds(IReadOnlyCollection<int> publicacionesIds, CancellationToken cancellationToken = default)
        {
            if (publicacionesIds.Count == 0)
            {
                return new List<PublicacionDetalleDto>();
            }

            return await _context.Publicaciones
                .AsNoTracking()
                .Where(p =>
                    !p.EstaOculto &&
                    publicacionesIds.Contains(p.Id))
                .Select(p => new PublicacionDetalleDto
                {
                    Id = p.Id,
                    Titulo = p.Titulo,

                    Descripcion = p.Descripcion.Length > 300
                        ? p.Descripcion.Substring(0, 297) + "..."
                        : p.Descripcion,

                    Fecha = p.Fecha,
                    FechaCreacion = p.FechaCreación,
                    Categoria = p.Categoria,

                    UrlMultimedia = p.UrlMultimedia,
                    TipoMultimedia = p.TipoMultimedia,

                    Autor = new AutorResumenDto
                    {
                        Id = p.UsuarioId,
                        Username = p.Usuario.Username
                    },

                    Lugar = new LugarDetalleDto
                    {
                        Id = p.LugarId,
                        Nombre = p.Lugar.Nombre,
                        Direccion = p.Lugar.Direccion,
                        Latitud = p.Lugar.Coordenadas.Y,
                        Longitud = p.Lugar.Coordenadas.X
                    },

                    // TODO: implementar el contador de likes y la interacción del usuario.
                    CantidadLikes = 0,
                    CantidadComentarios = p.Comentarios.Count(c => !c.EstaOculto),
                    DioLike = false
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<Dictionary<CategoriaEnum, int>> ObtenerCantidadPorCategoriaDelUsuario(int usuarioId, DateTime desde, 
            CancellationToken cancellationToken = default)
        {
            return await _context.Publicaciones
                .AsNoTracking()
                .Where(p =>
                    p.UsuarioId == usuarioId &&
                    !p.EstaOculto &&
                    p.FechaCreación >= desde)
                .GroupBy(p => p.Categoria)
                .Select(grupo => new
                {
                    Categoria = grupo.Key,
                    Cantidad = grupo.Count()
                })
                .ToDictionaryAsync(
                    resultado => resultado.Categoria,
                    resultado => resultado.Cantidad,
                    cancellationToken);
        }

        public async Task<bool> Eliminar(int id, int? autorId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Publicaciones.Where(p => p.Id == id);
            if (autorId.HasValue && autorId.Value > 0)
            {
                query = query.Where(p => p.UsuarioId == autorId.Value);
            }

            var publicacion = await query.FirstOrDefaultAsync(cancellationToken);
            if (publicacion == null) return false;

            _context.Publicaciones.Remove(publicacion);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> EliminarRepost(int originalId, int? autorId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Publicaciones.Where(p => p.PublicacionOriginalId == originalId);
            if (autorId.HasValue && autorId.Value > 0)
            {
                query = query.Where(p => p.UsuarioId == autorId.Value);
            }

            var repost = await query.OrderByDescending(p => p.FechaCreación).FirstOrDefaultAsync(cancellationToken);
            if (repost == null)
            {
                // Por si se envió directamente el Id de la fila de repost
                repost = await _context.Publicaciones.FirstOrDefaultAsync(
                    p => p.Id == originalId && (autorId == null || p.UsuarioId == autorId.Value),
                    cancellationToken);
            }

            if (repost == null) return false;

            _context.Publicaciones.Remove(repost);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
