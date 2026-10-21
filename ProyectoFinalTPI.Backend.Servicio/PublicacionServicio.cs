using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Dtos.Mapa;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
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
        private readonly ISeguimientoServicio _seguimientoServicio;
        private readonly ILogroServicio _logroServicio;
        public PublicacionServicio(IPublicacionRepositorio publicacionRepositorio, IUsuarioRepositorio usuarioRepositorio, 
            IMultimediaServicio multimediaServicio, IRecomendacionServicio recomendacionServicio, ISesionFypRepositorio sesionFypRepositorio,
            ISeguimientoServicio seguimientoServicio, ILogroServicio logroServicio)
        {
            _publicacionRepositorio = publicacionRepositorio;
            _usuarioRepositorio = usuarioRepositorio;
            _multimediaServicio = multimediaServicio;
            _recomendacionServicio = recomendacionServicio;
            _sesionFypRepositorio = sesionFypRepositorio;
            _seguimientoServicio = seguimientoServicio;
            _logroServicio = logroServicio;
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
            await VerificarAutorExistente(dto.AutorId);
            string urlMultimedia = await ProcesarMultimedia(dto);
            Publicacion publicacion = ConstruirPublicacion(dto, urlMultimedia);

            int idPublicacion = await _publicacionRepositorio.Crear(publicacion);

            await VerificaciónLogros(dto, publicacion);

            return idPublicacion;
        }
        public async Task<int> Compartir(CompartirPublicacionDto dto, CancellationToken cancellationToken = default)
        {
            await VerificarAutorExistente(dto.AutorId);
            Publicacion original = await VerificarPublicacionExistente(dto.PublicacionOriginalId, cancellationToken);
            Publicacion publicacionCompartida = ConstruirPublicacionCompartida(dto, original);

            return await _publicacionRepositorio.Crear(publicacionCompartida);
        }
        public async Task<ResultadoMapaDto> ObtenerPines(PublicacionFiltroDto filtro, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filtro);

            const int limitePines = 200;

            ValidarCoordenadas(filtro);
            ValidarFechasYCategorias(filtro);

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
            ValidarIdPublicacion(id);

            return await _publicacionRepositorio.ObtenerResumenPorId(id, cancellationToken)
           ?? throw new KeyNotFoundException("La publicación no existe o no está disponible.");
        }
        public async Task<PublicacionDetalleDto> ObtenerDetallePorId(int id, CancellationToken cancellationToken = default)
        {
            ValidarIdPublicacion(id);

            return await _publicacionRepositorio.ObtenerDetallePorId(id, cancellationToken)
           ?? throw new KeyNotFoundException("La publicación no existe o no está disponible.");
        }
        public async Task<ResultadoPaginadoDto<ComentarioDto>> ObtenerComentarios(int publicacionId, string? cursor, int limite,
            CancellationToken cancellationToken = default)
        {
            ValidarIdPublicacion(publicacionId);
            ValidarLimite(limite);

            var cursorDecodificado = CursorCronologicoCodec.Leer(cursor);

            if (!await _publicacionRepositorio.ExistePublicacionVisible(publicacionId, cancellationToken))
                throw new KeyNotFoundException("La publicación no existe o no está disponible.");

            var comentarios = await _publicacionRepositorio.ObtenerComentarios(
                publicacionId,
                cursorDecodificado,
                limite,
                cancellationToken);
            var items = comentarios
                .Take(limite)
                .ToList();

            return new ResultadoPaginadoDto<ComentarioDto>
            {
                Items = items,
                SiguienteCursor = comentarios.Count > limite
                    ? CursorCronologicoCodec.Crear(items[^1].FechaCreacion, items[^1].Id)
                    : null
            };
        }
        public async Task<ResultadoPaginadoDto<PublicacionDetalleDto>>ObtenerFeedSeguidos(int usuarioId, string? cursor, int limite,
        CancellationToken cancellationToken = default)
        {
            ValidarIdUsuario(usuarioId);
            ValidarLimite(limite);

            var cursorDecodificado = CursorCronologicoCodec.Leer(cursor);
            var autoresIds = await _seguimientoServicio.ObtenerSeguidosIdsAsync(
                usuarioId,
                cancellationToken);

            if (autoresIds.Count == 0)
            {
                return new ResultadoPaginadoDto<PublicacionDetalleDto>();
            }

            var publicaciones =
                await _publicacionRepositorio.ObtenerFeedSeguidos(
                    autoresIds,
                    cursorDecodificado,
                    limite,
                    cancellationToken);
            var items = publicaciones
                .Take(limite)
                .ToList();

            return new ResultadoPaginadoDto<PublicacionDetalleDto>
            {
                Items = items,
                SiguienteCursor = publicaciones.Count > limite
            ? CursorCronologicoCodec.Crear(items[^1].FechaCreacion, items[^1].Id)
            : null
            };
        }
        public async Task<ResultadoPaginadoDto<PublicacionDetalleDto>>ObtenerPublicacionesParaVos(
        int usuarioId,
        string? cursor,
        int limite,
        CancellationToken cancellationToken = default)
        {
            ValidarIdUsuario(usuarioId);
            ValidarLimite(limite);

            var cursorDecodificado = CursorFypCodec.Leer(cursor);

            SesionFypDto sesion;
            int posicion;

            if (cursorDecodificado == null)
            {
                var recomendaciones =
                    await _recomendacionServicio.GenerarRecomendacionParaUsuario(
                        usuarioId,
                        500,
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

        private async Task VerificaciónLogros(CrearPublicacionDto dto, Publicacion publicacion)
        {
            var usuarioId = publicacion.UsuarioId;

            await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.PrimerPaso);

            var puntoUsuario = new Point(dto.LongitudUsuario, dto.LatitudUsuario) { SRID = 4326 };
            double distanciaEnGrados = publicacion.Lugar.Coordenadas.Distance(puntoUsuario);

            if (distanciaEnGrados <= 0.01)
            {
                int publicacionesCercanas = await _publicacionRepositorio.ContarPosteosCercanosAlUsuario(usuarioId, puntoUsuario);

                if (publicacionesCercanas >= 5)
                {
                    await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.CronistaLocal);
                }
            }

            int decadasDelUsuario = await _publicacionRepositorio.ContarPosteosDeDiferentesEpocasDelUsuario(usuarioId);
            if (decadasDelUsuario >= 3)
            {
                await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.VozDeUnaEpoca); 
            } if (decadasDelUsuario == 6)
            {
                await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.DecadaCompleta);
            }
        }
        private Publicacion ConstruirPublicacion(CrearPublicacionDto dto, string urlMultimedia)
        {
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

            return new Publicacion
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
        }
        private async Task<string> ProcesarMultimedia(CrearPublicacionDto dto)
        {
            return await _multimediaServicio.SubirAsync(
                dto.Archivo,
                dto.NombreArchivo,
                dto.UrlArchivo,
                dto.TipoMultimedia);
        }
        private async Task VerificarAutorExistente(int autor)
        {
            var existeAutor = await _usuarioRepositorio.ExisteUsuario(autor);

            if (!existeAutor)
                throw new KeyNotFoundException(
                    "El usuario indicado no existe.");
        }
        private Publicacion ConstruirPublicacionCompartida(CompartirPublicacionDto dto, Publicacion original)
        {
            return new Publicacion
            {
                Titulo = original.Titulo,
                Descripcion = dto.Descripcion,
                Fecha = DateTime.UtcNow,
                Categoria = original.Categoria,
                UrlMultimedia = original.UrlMultimedia,
                TipoMultimedia = original.TipoMultimedia,
                LugarId = original.LugarId,
                UsuarioId = dto.AutorId,
                PublicacionOriginalId = original.Id,
                FechaCreación = DateTime.UtcNow,
                EstaOculto = false
            };
        }
        private async Task<Publicacion> VerificarPublicacionExistente(int publicacionOriginalId, CancellationToken cancellationToken)
        {
            var original = await _publicacionRepositorio.ObtenerPorId(publicacionOriginalId, cancellationToken);
            if (original == null)
                throw new KeyNotFoundException("La publicacion original no existe o no esta disponible.");
            return original;
        }
        private void ValidarFechasYCategorias(PublicacionFiltroDto filtro)
        {
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
        }
        private void ValidarCoordenadas(PublicacionFiltroDto filtro)
        {
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
        }
        private void ValidarIdPublicacion(int idPublicacion)
        {
            if (idPublicacion <= 0)
            {
                throw new ArgumentException(
                    "El ID de la publicación debe ser mayor que cero.");
            }
        }
        private void ValidarLimite(int limite)
        {
            if (limite < 1 || limite > 50)
            {
                throw new ArgumentException(
                    "El límite debe estar entre 1 y 50.");
            }
        }
        private void ValidarIdUsuario(int usuarioId)
        {
            if (usuarioId <= 0)
            {
                throw new ArgumentException(
                    "El ID del usuario debe ser mayor que cero.");
            }
        }
    }
}
