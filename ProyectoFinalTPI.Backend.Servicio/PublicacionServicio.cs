using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Dtos.Mapa;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio.ProyectoFinalTPI.Backend.Servicio;
using NetTopologySuite.Geometries;
using Npgsql.PostgresTypes;

namespace ProyectoFinalTPI.Backend.Servicio
{
    public class PublicacionServicio : IPublicacionServicio
    {
        private readonly IPublicacionRepositorio _publicacionRepositorio;
        private readonly IUsuarioRepositorio _usuarioRepositorio;
        private readonly IMultimediaServicio _multimediaServicio;
        private readonly ISesionFypRepositorio _sesionFypRepositorio;
        private readonly ISeguimientoServicio _seguimientoServicio;
        private readonly ILogroServicio _logroServicio;
        public PublicacionServicio(IPublicacionRepositorio publicacionRepositorio, IUsuarioRepositorio usuarioRepositorio, 
            IMultimediaServicio multimediaServicio, ISesionFypRepositorio sesionFypRepositorio,
            ISeguimientoServicio seguimientoServicio, ILogroServicio logroServicio)
        {
            _publicacionRepositorio = publicacionRepositorio;
            _usuarioRepositorio = usuarioRepositorio;
            _multimediaServicio = multimediaServicio;
            _sesionFypRepositorio = sesionFypRepositorio;
            _seguimientoServicio = seguimientoServicio;
            _logroServicio = logroServicio;
        }

        #region Interacciones (Likes y Puntuaciones)
        public async Task<MeGustaPublicacionDto> DarLike(
            int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
        {
            await ValidarInteraccionConPublicacion(publicacionId, usuarioId, cancellationToken);
            await _publicacionRepositorio.CrearLike(publicacionId, usuarioId, cancellationToken);
            
            //vuelve a consultar a Neo4j para saber camo quedo esa publicacion (cantidad de likes, quien le dio MG) y arma el DTO
            return await _publicacionRepositorio.ObtenerEstadoLike(publicacionId, usuarioId, cancellationToken);
        }

        public async Task<MeGustaPublicacionDto> QuitarLike(
            int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
        {
            await ValidarInteraccionConPublicacion(publicacionId, usuarioId, cancellationToken);
            await _publicacionRepositorio.QuitarLike(publicacionId, usuarioId, cancellationToken);
            return await _publicacionRepositorio.ObtenerEstadoLike(publicacionId, usuarioId, cancellationToken);
        }

        public async Task CompletarResumenesLikes(
            IReadOnlyCollection<PublicacionDetalleDto> publicaciones, int usuarioId,
            CancellationToken cancellationToken = default)
        {
            if (publicaciones.Count == 0) return;

            var resumenes = await _publicacionRepositorio.ObtenerResumenLikes(
                publicaciones.Select(publicacion => publicacion.Id).Distinct().ToArray(),
                usuarioId,
                cancellationToken);

            foreach (var publicacion in publicaciones)
                if (resumenes.TryGetValue(publicacion.Id, out var resumen))
                {
                    publicacion.CantidadLikes = resumen.Cantidad;
                    publicacion.DioLike = resumen.DioLike;
                }
        }
        public async Task<PuntuacionPublicacionDto> Puntuar(
            int publicacionId, int usuarioId, int puntaje, CancellationToken cancellationToken = default)
        {
            if (puntaje < 1 || puntaje > 5)
                throw new ArgumentOutOfRangeException(nameof(puntaje), "La puntuación debe estar entre 1 y 5.");

            await ValidarInteraccionConPublicacion(publicacionId, usuarioId, cancellationToken);

            var publicacion = await _publicacionRepositorio.ObtenerPorId(publicacionId, cancellationToken);
            if (publicacion != null && publicacion.UsuarioId == usuarioId)
            {
                throw new InvalidOperationException("No podés puntuar tu propia publicación.");
            }

            var estadoActual = await _publicacionRepositorio.ObtenerEstadoPuntuacion(publicacionId, usuarioId, cancellationToken);
            if (estadoActual.MiPuntuacion == puntaje)
            {
                // Toggle: si vuelve a presionar el mismo puntaje, se quita la puntuación
                await _publicacionRepositorio.QuitarPuntuacion(publicacionId, usuarioId, cancellationToken);
            }
            else
            {
                await _publicacionRepositorio.PuntuarPublicacion(publicacionId, usuarioId, puntaje, cancellationToken);
            }

            return await _publicacionRepositorio.ObtenerEstadoPuntuacion(publicacionId, usuarioId, cancellationToken);
        }

        public async Task<PuntuacionPublicacionDto> QuitarPuntuacion(
            int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
        {
            await ValidarInteraccionConPublicacion(publicacionId, usuarioId, cancellationToken);
            await _publicacionRepositorio.QuitarPuntuacion(publicacionId, usuarioId, cancellationToken);
            return await _publicacionRepositorio.ObtenerEstadoPuntuacion(publicacionId, usuarioId, cancellationToken);
        }

        public async Task CompletarResumenesPuntuaciones(
            IReadOnlyCollection<PublicacionDetalleDto> publicaciones, int? usuarioId,
            CancellationToken cancellationToken = default)
        {
            if (publicaciones.Count == 0) return;

            var resumenes = await _publicacionRepositorio.ObtenerResumenPuntuaciones(
                publicaciones.Select(publicacion => publicacion.Id).Distinct().ToArray(),
                usuarioId,
                cancellationToken);

            foreach (var publicacion in publicaciones)
                if (resumenes.TryGetValue(publicacion.Id, out var resumen))
                {
                    publicacion.PuntosTotales = resumen.PuntosTotales;
                    publicacion.MiPuntuacion = resumen.MiPuntuacion;
                }
        }
        
        #endregion

        #region Consultas y Feeds

        public List<Publicacion> ObtenerPublicaciones()
        {
            return _publicacionRepositorio.ObtenerTodasPublicaciones();
        }
        public List<Publicacion> ObtenerPublicacionPorCategoria(CategoriaEnum categoria)
        {
            return _publicacionRepositorio.ObtenerPublicacionPorCategoria(categoria);
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
        public async Task<List<PublicacionDetalleDto>> ObtenerPorUsuarioId(int usuarioId, int? usuarioAutenticadoId, CancellationToken cancellationToken = default)
        {
            ValidarIdUsuario(usuarioId);

            var publicaciones = await _publicacionRepositorio.ObtenerPorUsuarioId(usuarioId, cancellationToken);
            await CompletarResumenesLikes(publicaciones, usuarioAutenticadoId ?? 0, cancellationToken);
            await CompletarResumenesPuntuaciones(publicaciones, usuarioAutenticadoId, cancellationToken);
            return publicaciones;
        }
        public async Task<ResultadoPaginadoDto<PublicacionDetalleDto>> ObtenerFeedSeguidos(int usuarioId, string? cursor, int limite,
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

            await CompletarResumenesLikes(items, usuarioId, cancellationToken);
            await CompletarResumenesPuntuaciones(items, usuarioId, cancellationToken);

            return new ResultadoPaginadoDto<PublicacionDetalleDto>
            {
                Items = items,
                SiguienteCursor = publicaciones.Count > limite
            ? CursorCronologicoCodec.Crear(items[^1].FechaCreacion, items[^1].Id)
            : null
            };
        }
        public async Task<ResultadoPaginadoDto<PublicacionDetalleDto>> ObtenerPublicacionesParaVos(
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
                const int cantidadMaximaRecomendaciones = 500;

                var recomendaciones = await ObtenerRecomendacionesPorLikes(
                    usuarioId, cantidadMaximaRecomendaciones, cancellationToken);

                if (recomendaciones.Count == 0)
                {
                    var generales = await _publicacionRepositorio.ObtenerPublicacionesParaVos(
                        Array.Empty<CategoriaEnum>(), DateTime.UtcNow, null,
                        cantidadMaximaRecomendaciones, cancellationToken);
                    recomendaciones = generales.Take(cantidadMaximaRecomendaciones)
                        .Select(p => p.Id).ToArray();
                }
                
                sesion = _sesionFypRepositorio.Crear(usuarioId, recomendaciones);

                posicion = 0;
            }
            else
            {
                sesion = _sesionFypRepositorio.Obtener(cursorDecodificado.SesionId, usuarioId)
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
                            await CompletarResumenesLikes(items, usuarioId, cancellationToken);
                            await CompletarResumenesPuntuaciones(items, usuarioId, cancellationToken);

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

            await CompletarResumenesLikes(items, usuarioId, cancellationToken);
            await CompletarResumenesPuntuaciones(items, usuarioId, cancellationToken);

            return new ResultadoPaginadoDto<PublicacionDetalleDto>
            {
                Items = items,
                SiguienteCursor = null
            };
        }

        #endregion

        #region Creacion, Compartir y Eliminacion

        public async Task<int> Crear(Publicacion publicacion, Stream? archivo = null, string? nombreArchivo = null, Point? gpsUsuario = null)
        {
            await VerificarAutorExistente(publicacion.UsuarioId);

            publicacion.UrlMultimedia = await ProcesarMultimedia(publicacion, archivo, nombreArchivo);
            IniciarEstadoPublicacion(publicacion);

            int idPublicacion = await _publicacionRepositorio.Crear(publicacion);

            await VerificacionLogros(publicacion, gpsUsuario);

            return idPublicacion;
        }
        public async Task<int> Compartir(Publicacion publicacion, CancellationToken cancellationToken = default)
        {
            await VerificarAutorExistente(publicacion.UsuarioId);

            var original = await _publicacionRepositorio.ObtenerPorId(publicacion.PublicacionOriginalId.GetValueOrDefault(), cancellationToken);
            if (original == null)
                throw new KeyNotFoundException("La publicacion original no existe o no esta disponible.");

            // Si la publicación a compartir es un repost SIMPLE (sin comentario propio),
            // enlazamos con la publicación que fue reposteada para evitar cadenas de retweets simples.
            // Pero si es una publicación con contenido propio (un quote tweet con su propia descripción),
            // se enlaza directamente a ella para permitir repostearla o hacer un quote al quote.
            var targetOriginal = original;
            while (targetOriginal.PublicacionOriginalId.HasValue && string.IsNullOrEmpty(targetOriginal.Descripcion))
            {
                var padre = await _publicacionRepositorio.ObtenerPorId(targetOriginal.PublicacionOriginalId.Value, cancellationToken);
                if (padre == null) break;
                targetOriginal = padre;
            }

            publicacion.Titulo = targetOriginal.Titulo;
            publicacion.Fecha = targetOriginal.Fecha;
            publicacion.Categoria = targetOriginal.Categoria;
            publicacion.UrlMultimedia = targetOriginal.UrlMultimedia;
            publicacion.TipoMultimedia = targetOriginal.TipoMultimedia;
            publicacion.LugarId = targetOriginal.LugarId;
            publicacion.PublicacionOriginalId = targetOriginal.Id;
            publicacion.FechaCreación = DateTime.UtcNow;
            publicacion.EstaOculto = false;
            publicacion.MotivoOculto = null;

            return await _publicacionRepositorio.Crear(publicacion);
        }
        public async Task Eliminar(int id, int? autorId = null, CancellationToken cancellationToken = default)
        {
            if (id <= 0)
            {
                throw new ArgumentException("El ID de la publicación debe ser mayor que cero.");
            }

            var eliminado = await _publicacionRepositorio.Eliminar(id, autorId, cancellationToken);
            if (!eliminado)
            {
                throw new KeyNotFoundException("La publicación no existe o no se pudo eliminar.");
            }
        }
        public async Task EliminarRepost(int originalId, int? autorId = null, CancellationToken cancellationToken = default)
        {
            if (originalId <= 0)
            {
                throw new ArgumentException("El ID de la publicación debe ser mayor que cero.");
            }

            var eliminado = await _publicacionRepositorio.EliminarRepost(originalId, autorId, cancellationToken);
            if (!eliminado)
            {
                throw new KeyNotFoundException("El repost no existe o no se pudo eliminar.");
            }
        }
       
        #endregion

        #region Métodos Privados y Validaciones
        private async Task VerificacionLogros(Publicacion publicacion, Point? gpsUsuario)
        {
            var usuarioId = publicacion.UsuarioId;

            await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.PrimerPaso);

            if (gpsUsuario != null && publicacion.Lugar?.Coordenadas != null)
            {
                double distancia = publicacion.Lugar.Coordenadas.Distance(gpsUsuario);

                if (distancia <= 0.01)
                {
                    int posteosCercanos = await _publicacionRepositorio.ContarPosteosCercanosAlUsuario(usuarioId, gpsUsuario);

                    if (posteosCercanos >= 5)
                    {
                        await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.CronistaLocal);
                    }
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
        private async Task VerificarAutorExistente(int autor)
        {
            var existeAutor = await _usuarioRepositorio.ExisteUsuario(autor);

            if (!existeAutor)
                throw new KeyNotFoundException(
                    "El usuario indicado no existe.");
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
        }
        private async Task<IReadOnlyList<int>> ObtenerRecomendacionesPorLikes(
            int usuarioId, int cantidadMaxima, CancellationToken cancellationToken)
        {
            // Obtiene una lista de IDs de publicaciones recomendadas para el usuario
            var idsRecomendados = await _publicacionRepositorio.ObtenerPublicacionesRelacionadasPorLikes(
                usuarioId, cantidadMaxima, cancellationToken);
            if (idsRecomendados.Count == 0) return Array.Empty<int>();

            // Busca los detalles de esas publicaciones para convertirlas en PublicacionDetalleDto
            var publicacionesVisibles = await _publicacionRepositorio.ObtenerPublicacionesPorIds(
                idsRecomendados, cancellationToken);

            // Filtran las recomendaciones con solo los ID de publicaciones visibles,
            // manteniendo el orden por relevancia que devolvio Neo4j
            var idsVisibles = publicacionesVisibles.Select(publicacion => publicacion.Id).ToHashSet();
            return idsRecomendados.Where(idsVisibles.Contains).ToArray();
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
        private async Task ValidarInteraccionConPublicacion(
                    int publicacionId, int usuarioId, CancellationToken cancellationToken)
                {
            if (publicacionId <= 0)
                throw new ArgumentException("El ID de publicacion es erroneo");
            if (usuarioId <= 0)
                throw new ArgumentException("El ID de usuario es erroneo");
            if (!await _usuarioRepositorio.ExisteUsuario(usuarioId, cancellationToken))
                throw new KeyNotFoundException("El usuario autenticado no existe");
            if (!await _publicacionRepositorio.ExistePublicacionVisible(publicacionId, cancellationToken))
                throw new KeyNotFoundException("La publicacion no existe o no esta disponible");
        }
        private async Task<string> ProcesarMultimedia(
        Publicacion publicacion,
        Stream? archivo,
        string? nombreArchivo)
            {
                string? urlExistente = string.IsNullOrEmpty(publicacion.UrlMultimedia)
                    ? null
                    : publicacion.UrlMultimedia;

                return await _multimediaServicio.SubirAsync(
                    archivo,
                    nombreArchivo,
                    urlExistente,
                    publicacion.TipoMultimedia);
            }
        private static void IniciarEstadoPublicacion(Publicacion publicacion)
        {
            publicacion.FechaCreación = DateTime.UtcNow;
            publicacion.EstaOculto = false;
            publicacion.MotivoOculto = null;
        }
        #endregion
    }
}
