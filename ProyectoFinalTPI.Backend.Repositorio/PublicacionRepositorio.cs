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
                p.Lugar.Coordenadas.X >= filtro.MinLatitud &&
                p.Lugar.Coordenadas.X <= filtro.MaxLatitud);

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
                        longitudMaximaDescripcion) + "..."
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

                    // TODO: reemplazar por contadores e interacción reales.
                    CantidadLikes = 0,
                    CantidadComentarios = 0,
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
                                0, longitudMaximaDescripcion) + "..."
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
                        Id = p.Id,
                        Nombre = p.Lugar.Nombre,
                        Direccion = p.Lugar.Direccion,
                        Latitud = p.Lugar.Coordenadas.Y,
                        Longitud = p.Lugar.Coordenadas.X
                    },

                    // TODO: reemplazar por contadores e interacción reales.
                    CantidadLikes = 0,
                    CantidadComentarios = 0,
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

                    // TODO: reemplazar por contadores e interacción reales.
                    CantidadLikes = 0,
                    CantidadComentarios = 0,
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

                    // TODO: reemplazar por contadores e interacción reales.
                    CantidadLikes = 0,
                    CantidadComentarios = 0,
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
    }
}
