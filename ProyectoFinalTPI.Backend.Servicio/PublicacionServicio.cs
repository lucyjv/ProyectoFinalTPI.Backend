using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Dtos.Mapa;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Servicio.ProyectoFinalTPI.Backend.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio
{
    public class PublicacionServicio : IPublicacionServicio
    {
        private readonly IPublicacionRepositorio _publicacionRepositorio;
        private readonly IUsuarioRepositorio _usuarioRepositorio;
        private readonly IMultimediaServicio _multimediaServicio;
        private readonly IRecomendacionServicio _recomendacionServicio;
        private readonly ISesionFypRepositorio _sesionFypRepositorio;
        public PublicacionServicio(IPublicacionRepositorio publicacionRepositorio, IUsuarioRepositorio usuarioRepositorio, 
            IMultimediaServicio multimediaServicio, IRecomendacionServicio recomendacionServicio, ISesionFypRepositorio sesionFypRepositorio)
        {
            _publicacionRepositorio = publicacionRepositorio;
            _usuarioRepositorio = usuarioRepositorio;
            _multimediaServicio = multimediaServicio;
            _recomendacionServicio = recomendacionServicio;
            _sesionFypRepositorio = sesionFypRepositorio;
        }

        public List<Publicacion> ObtenerPublicaciones()
        {
            return _publicacionRepositorio.ObtenerTodasPublicaciones();
        }
        public List<Publicacion> ObtenerPublicacionPorCategoria(CategoriaEnum categoria)
        {
            return _publicacionRepositorio.ObtenerPublicacionPorCategoria(categoria);
        }

        public async Task<int> Crear(CrearPublicacionDto dto)
        {
            var existeAutor = await _usuarioRepositorio.ExisteUsuario(dto.AutorId);

            if(!existeAutor)
                throw new KeyNotFoundException(
                    "El usuario indicado no existe.");

            var urlMultimedia = await _multimediaServicio.SubirAsync(
                dto.Archivo,
                dto.NombreArchivo,
                dto.UrlArchivo,
                dto.TipoMultimedia);

            var lugar = new Lugar
            {
                Nombre = dto.NombreLugar,

                Coordenadas = new Point(
                    dto.Longitud,
                    dto.Latitud)
                {
                    SRID = 4326
                }
            };

            var publicacion = new Publicacion
            {
                Titulo = dto.Titulo,
                Descripcion = dto.Descripcion,
                Fecha = dto.Fecha,

                Categoria = dto.Categoria,

                UrlMultimedia = urlMultimedia,
                TipoMultimedia = dto.TipoMultimedia,

                Lugar = lugar,

                UsuarioId = dto.AutorId,

                FechaCreación = DateTime.UtcNow,
                EstaOculto = false,
                MotivoOculto = null
            };

            return await _publicacionRepositorio.Crear(publicacion);
        }

        public async Task<ResultadoMapaDto> ObtenerPines(PublicacionFiltroDto filtro, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filtro);

            const int limitePines = 200;

            if (!double.IsFinite(filtro.MinLatitud) ||
                !double.IsFinite(filtro.MaxLatitud) ||
                filtro.MinLatitud < -90 ||
                filtro.MaxLatitud > 90 ||
                filtro.MinLatitud >= filtro.MaxLatitud)
            {
                throw new ArgumentException(
                    "El rango de latitud debe estar entre -90 y 90, " +
                    "con el mínimo menor que el máximo.");
            }

            if (!double.IsFinite(filtro.MinLongitud) ||
                !double.IsFinite(filtro.MaxLongitud) ||
                filtro.MinLongitud < -180 ||
                filtro.MaxLongitud > 180 ||
                filtro.MinLongitud >= filtro.MaxLongitud)
            {
                throw new ArgumentException(
                    "El rango de longitud debe estar entre -180 y 180, " +
                    "con el mínimo menor que el máximo.");
            }

            if (filtro.Categoria.HasValue &&
                !Enum.IsDefined(typeof(CategoriaEnum), filtro.Categoria.Value))
            {
                throw new ArgumentException("La categoría indicada no es válida.");
            }

            if (filtro.FechaDesde.HasValue &&
                filtro.FechaDesde.Value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "La fecha desde debe estar expresada en UTC.");
            }

            if (filtro.FechaHasta.HasValue &&
                filtro.FechaHasta.Value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "La fecha hasta debe estar expresada en UTC.");
            }

            if (filtro.FechaDesde.HasValue &&
                filtro.FechaHasta.HasValue &&
                filtro.FechaDesde.Value >= filtro.FechaHasta.Value)
            {
                throw new ArgumentException(
                    "La fecha desde debe ser anterior a la fecha hasta.");
            }

            var pines = await _publicacionRepositorio.ObtenerPines(
                filtro,
                limitePines,
                cancellationToken);

            return new ResultadoMapaDto
            {
                Pines = pines.Take(limitePines).ToList(),
                HayMas = pines.Count > limitePines
            };
        }

        public async Task<PublicacionResumenDto> ObtenerResumenPorId(int id, CancellationToken cancellationToken = default)
        {
            if (id <= 0)
            {
                throw new ArgumentException(
                    "El ID de la publicación debe ser mayor que cero.");
            }

            var publicacion = await _publicacionRepositorio.ObtenerResumenPorId(
                id,
                cancellationToken);

            if (publicacion == null)
            {
                throw new KeyNotFoundException(
                    "La publicación no existe o no está disponible.");
            }

            return publicacion;
        }

        public async Task<PublicacionDetalleDto> ObtenerDetallePorId(int id, CancellationToken cancellationToken = default)
        {
            if (id <= 0)
            {
                throw new ArgumentException(
                    "El ID de la publicación debe ser mayor que cero.");
            }

            var publicacion = await _publicacionRepositorio.ObtenerDetallePorId(
                id,
                cancellationToken);

            if (publicacion == null)
            {
                throw new KeyNotFoundException(
                    "La publicación no existe o no está disponible.");
            }

            return publicacion;
        }

        public async Task<ResultadoPaginadoDto<ComentarioDto>> ObtenerComentarios(int publicacionId, string? cursor, int limite,
            CancellationToken cancellationToken = default)
        {
            if (publicacionId <= 0)
            {
                throw new ArgumentException(
                    "El ID de la publicación debe ser mayor que cero.");
            }

            if (limite < 1 || limite > 50)
            {
                throw new ArgumentException(
                    "El límite debe estar entre 1 y 50.");
            }

            var cursorDecodificado = CursorCronologicoCodec.Leer(cursor);

            var existePublicacion =
                await _publicacionRepositorio.ExistePublicacionVisible(
                    publicacionId,
                    cancellationToken);

            if (!existePublicacion)
            {
                throw new KeyNotFoundException(
                    "La publicación no existe o no está disponible.");
            }

            var comentarios = await _publicacionRepositorio.ObtenerComentarios(
                publicacionId,
                cursorDecodificado,
                limite,
                cancellationToken);

            var hayMas = comentarios.Count > limite;

            var items = comentarios
                .Take(limite)
                .ToList();

            string? siguienteCursor = null;

            if (hayMas)
            {
                var ultimoComentario = items[^1];

                siguienteCursor = CursorCronologicoCodec.Crear(
                    ultimoComentario.FechaCreacion,
                    ultimoComentario.Id);
            }

            return new ResultadoPaginadoDto<ComentarioDto>
            {
                Items = items,
                SiguienteCursor = siguienteCursor
            };
        }

        public async Task<ResultadoPaginadoDto<PublicacionDetalleDto>>ObtenerFeedSeguidos(IReadOnlyCollection<int> autoresIds, string? cursor, int limite,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(autoresIds);

            if (limite < 1 || limite > 50)
            {
                throw new ArgumentException(
                    "El límite debe estar entre 1 y 50.");
            }

            if (autoresIds.Any(id => id <= 0))
            {
                throw new ArgumentException(
                    "Los IDs de los autores deben ser mayores que cero.");
            }

            var cursorDecodificado = CursorCronologicoCodec.Leer(cursor);

            var autoresUnicos = autoresIds
                .Distinct()
                .ToArray();

            if (autoresUnicos.Length == 0)
            {
                return new ResultadoPaginadoDto<PublicacionDetalleDto>();
            }

            var publicaciones =
                await _publicacionRepositorio.ObtenerFeedSeguidos(
                    autoresUnicos,
                    cursorDecodificado,
                    limite,
                    cancellationToken);

            var hayMas = publicaciones.Count > limite;

            var items = publicaciones
                .Take(limite)
                .ToList();

            string? siguienteCursor = null;

            if (hayMas)
            {
                var ultimaPublicacion = items[^1];

                siguienteCursor = CursorCronologicoCodec.Crear(
                    ultimaPublicacion.FechaCreacion,
                    ultimaPublicacion.Id);
            }

            return new ResultadoPaginadoDto<PublicacionDetalleDto>
            {
                Items = items,
                SiguienteCursor = siguienteCursor
            };
        }

        public async Task<ResultadoPaginadoDto<PublicacionDetalleDto>>
    ObtenerPublicacionesParaVos(
        int usuarioId,
        string? cursor,
        int limite,
        CancellationToken cancellationToken = default)
        {
            if (usuarioId <= 0)
            {
                throw new ArgumentException(
                    "El ID del usuario debe ser mayor que cero.");
            }

            if (limite < 1 || limite > 50)
            {
                throw new ArgumentException(
                    "El límite debe estar entre 1 y 50.");
            }

            var cursorDecodificado = CursorFypCodec.Leer(cursor);

            SesionFypDto sesion;
            int posicion;

            if (cursorDecodificado == null)
            {
                const int cantidadMaximaRecomendaciones = 500;

                var recomendaciones =
                    await _recomendacionServicio.GenerarRecomendacionParaUsuario(
                        usuarioId,
                        cantidadMaximaRecomendaciones,
                        cancellationToken);

                sesion = _sesionFypRepositorio.Crear(
                    usuarioId,
                    recomendaciones);

                posicion = 0;
            }
            else
            {
                sesion = _sesionFypRepositorio.Obtener(
                    cursorDecodificado.SesionId,
                    usuarioId)
                    ?? throw new SesionFypNoDisponibleException();

                posicion = cursorDecodificado.Posicion;

                if (posicion > sesion.PublicacionesIds.Count)
                {
                    throw new ArgumentException(
                        "La posición del cursor no es válida.");
                }
            }

            var items = new List<PublicacionDetalleDto>();

            const int tamanioLote = 50;

            while (posicion < sesion.PublicacionesIds.Count)
            {
                var idsLote = sesion.PublicacionesIds
                    .Skip(posicion)
                    .Take(tamanioLote)
                    .ToArray();

                var publicaciones =
                    await _publicacionRepositorio.ObtenerPublicacionesPorIds(
                        idsLote,
                        cancellationToken);

                var publicacionesPorId = publicaciones
                    .ToDictionary(p => p.Id);

                foreach (var id in idsLote)
                {
                    if (publicacionesPorId.TryGetValue(
                        id,
                        out var publicacion))
                    {
                        // Encontramos otra publicación visible:
                        // la página actual ya está completa.
                        if (items.Count == limite)
                        {
                            return new ResultadoPaginadoDto<PublicacionDetalleDto>
                            {
                                Items = items,
                                SiguienteCursor = CursorFypCodec.Crear(
                                    sesion.Id,
                                    posicion)
                            };
                        }

                        items.Add(publicacion);
                    }

                    posicion++;
                }
            }

            return new ResultadoPaginadoDto<PublicacionDetalleDto>
            {
                Items = items,
                SiguienteCursor = null
            };
        }

    }
}
