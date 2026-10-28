using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Mapa;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio.ProyectoFinalTPI.Backend.Servicio;
using NetTopologySuite.Geometries;

namespace ProyectoFinalTPI.Backend.Servicio
{
    public class PublicacionServicio : IPublicacionServicio
    {
        private readonly IPublicacionRepositorio _publicacionRepositorio;
        private readonly IUsuarioRepositorio _usuarioRepositorio;
        private readonly IMultimediaServicio _multimediaServicio;
        private readonly ISesionFypRepositorio _sesionFypRepositorio;
        private readonly ISeguimientoServicio _seguimientoServicio;
        private readonly FeedRecomendacionesOptions _opcionesFeed;
        private readonly INotificacionServicio _notificacionServicio;
        private readonly ILogroServicio _logroServicio;
        private readonly OpcionesPublicacion _opcionesPublicacion;

        public PublicacionServicio(
            IPublicacionRepositorio publicacionRepositorio,
            IUsuarioRepositorio usuarioRepositorio,
            IMultimediaServicio multimediaServicio,
            ISesionFypRepositorio sesionFypRepositorio,
            ISeguimientoServicio seguimientoServicio,
            IOptions<FeedRecomendacionesOptions> opcionesFeed,
            INotificacionServicio notificacionServicio,
            ILogroServicio logroServicio,
            IOptions<OpcionesPublicacion>? opcionesPublicacion = null)
        {
            _publicacionRepositorio = publicacionRepositorio;
            _usuarioRepositorio = usuarioRepositorio;
            _multimediaServicio = multimediaServicio;
            _sesionFypRepositorio = sesionFypRepositorio;
            _seguimientoServicio = seguimientoServicio;
            _opcionesFeed = opcionesFeed.Value;
            _notificacionServicio = notificacionServicio;
            _logroServicio = logroServicio;
            _opcionesPublicacion = opcionesPublicacion?.Value ?? new OpcionesPublicacion();
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

        public async Task CompletarResumenesReposts(
            IReadOnlyCollection<PublicacionDetalleDto> publicaciones, int usuarioId,
            CancellationToken cancellationToken = default)
        {
            if (publicaciones.Count == 0) return;

            var resumenes = await _publicacionRepositorio.ObtenerResumenReposts(
                publicaciones.Select(publicacion => publicacion.Id).Distinct().ToArray(),
                usuarioId,
                cancellationToken);

            foreach (var publicacion in publicaciones)
                if (resumenes.TryGetValue(publicacion.Id, out var resumen))
                {
                    publicacion.CantidadReposts = resumen.Cantidad;
                    publicacion.DioRepost = resumen.DioRepost;
                }
        }

        public async Task<PuntuacionPublicacionDto> Puntuar(
            int publicacionId, int usuarioId, int puntaje, CancellationToken cancellationToken = default)
        {
            ValidarPuntaje(puntaje);
            await ValidarInteraccionConPublicacion(publicacionId, usuarioId, cancellationToken);

            var publicacion = await _publicacionRepositorio.ObtenerPorId(publicacionId, cancellationToken);
            VerificarNoEsPublicacionPropia(usuarioId, publicacion);

            await ActualizarPuntuacion(publicacionId, usuarioId, puntaje, publicacion, cancellationToken);

            var estadoNuevo = await _publicacionRepositorio.ObtenerEstadoPuntuacion(publicacionId, usuarioId, cancellationToken);
            await VerificacionLogrosPuntuacion(usuarioId, publicacion, estadoNuevo, cancellationToken);

            return estadoNuevo;
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

        public async Task<RepostPublicacionDto> Repostear(int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
        {
            ValidarIdPublicacion(publicacionId);
            ValidarIdUsuario(usuarioId);
            await VerificarAutorExistente(usuarioId);
            Publicacion original = await ObtenerPublicacionOriginalParaCompartir(publicacionId, cancellationToken);

            var targetId = original.PublicacionOriginalId ?? original.Id;
            await _publicacionRepositorio.CrearRepost(targetId, usuarioId, cancellationToken);
            await VerificacionLogroCompartir(usuarioId, cancellationToken);
            return await _publicacionRepositorio.ObtenerEstadoRepost(targetId, usuarioId, cancellationToken);
        }
        public async Task<RepostPublicacionDto> QuitarRepost(int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
        {
            ValidarIdPublicacion(publicacionId);
            ValidarIdUsuario(usuarioId);

            await _publicacionRepositorio.QuitarRepost(publicacionId, usuarioId, cancellationToken);
            return await _publicacionRepositorio.ObtenerEstadoRepost(publicacionId, usuarioId, cancellationToken);
        }
        public async Task<int> Citar(int publicacionId, int usuarioId, string descripcion, CancellationToken cancellationToken = default)
        {
            ValidarIdPublicacion(publicacionId);
            ValidarIdUsuario(usuarioId);
            ValidarDescripcion(descripcion);
            await VerificarAutorExistente(usuarioId);

            var original = await ObtenerPublicacionOriginalParaCompartir(publicacionId, cancellationToken);

            var targetOriginal = original.PublicacionOriginalId.HasValue
                ? await _publicacionRepositorio.ObtenerPorId(original.PublicacionOriginalId.Value, cancellationToken) ?? original
                : original;
            Publicacion cita = CrearPublicacion(usuarioId, descripcion, targetOriginal);

            var idCita = await _publicacionRepositorio.Crear(cita);
            await VerificacionLogroCompartir(usuarioId, cancellationToken);
            return idCita;
        }
        public async Task<int> Compartir(Publicacion publicacion, CancellationToken cancellationToken = default)
        {
            var targetId = publicacion.PublicacionOriginalId.GetValueOrDefault();
            if (string.IsNullOrWhiteSpace(publicacion.Descripcion))
            {
                await Repostear(targetId, publicacion.UsuarioId, cancellationToken);
                return targetId;
            }
            else
            {
                return await Citar(targetId, publicacion.UsuarioId, publicacion.Descripcion, cancellationToken);
            }
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

            publicaciones = await IncorporarRepostsDelUsuario(publicaciones, usuarioId, cancellationToken);

            await CompletarResumenesInteracciones(publicaciones, usuarioAutenticadoId, cancellationToken);
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

            publicaciones = await IncorporarRepostsDeSeguidos(
                publicaciones, autoresIds, cursorDecodificado, cancellationToken);

            var items = publicaciones
                .Take(limite)
                .ToList();

            await CompletarResumenesInteracciones(items, usuarioId, cancellationToken);

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

            var (sesion, posicion) = await ObtenerSesionFeed(usuarioId, cursor, cancellationToken);
            return await ObtenerPaginaFeed(sesion, posicion, usuarioId, limite, cancellationToken);
        }

        #endregion

        #region Creacion, Compartir y Eliminacion

        public async Task<int> Crear(Publicacion publicacion, Stream? archivo = null, string? nombreArchivo = null, Point? gpsUsuario = null)
        {
            await VerificarAutorExistente(publicacion.UsuarioId);

            publicacion.UrlMultimedia = await ProcesarMultimedia(publicacion, archivo, nombreArchivo);
            IniciarEstadoPublicacion(publicacion);

            int idPublicacion = await _publicacionRepositorio.Crear(publicacion);

            await VerificacionLogrosCrearPublicacion(publicacion, gpsUsuario);

            return idPublicacion;
        }
        public async Task Eliminar(int id, int? autorId = null, CancellationToken cancellationToken = default)
        {
            ValidarIdPublicacion(id);

            var eliminado = await _publicacionRepositorio.Eliminar(id, autorId, cancellationToken);
            VerificarEliminar(eliminado);
        }

        public async Task EliminarRepost(int originalId, int? autorId = null, CancellationToken cancellationToken = default)
        {
            ValidarIdPublicacion(originalId);

            var eliminado = await _publicacionRepositorio.EliminarRepost(originalId, autorId, cancellationToken);
            VerificarEliminar(eliminado);
        }
       
        #endregion

        #region Métodos Privados y Validaciones
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
        private async Task<IReadOnlyList<int>> CrearRankingFeedAsync(
            int usuarioId, CancellationToken cancellationToken)
        {
            var cantidadMaxima = _opcionesFeed.CantidadMaximaCandidatas;

            //trae likes, puntuaciones, vistas y publicaciones semilla
            var contexto = await PrepararInteraccionesFeedAsync(usuarioId, cancellationToken);

            //consulta recomendaciones, publicaciones generales y el fallback de vistas.
            var candidatos = await ObtenerCandidatosFeedAsync(
                usuarioId, contexto, cantidadMaxima, cancellationToken);

            //aplica las prioridades y calcula el puntaje final.
            return OrdenarCandidatosFeed(contexto, candidatos, cantidadMaxima);
        }

        private async Task<ContextoFeed> PrepararInteraccionesFeedAsync(
            int usuarioId, CancellationToken cancellationToken)
        {
            var vistasDesde = DateTimeOffset.UtcNow
                .AddDays(-_opcionesFeed.DiasDeExclusionDeImpresiones)
                .ToUnixTimeMilliseconds();
            var interacciones = await _publicacionRepositorio.ObtenerInteraccionesFeed(
                usuarioId, cancellationToken);
            var idsInteractuados = interacciones
                .Where(interaccion => interaccion.DioLike || interaccion.MiPuntuacion.HasValue)
                .Select(interaccion => interaccion.PublicacionId)
                .ToHashSet();
            var vistas = interacciones
                .Where(interaccion => interaccion.UltimaImpresionUnixMs.HasValue)
                .GroupBy(interaccion => interaccion.PublicacionId)
                .ToDictionary(grupo => grupo.Key,
                    grupo => grupo.Max(interaccion => interaccion.UltimaImpresionUnixMs!.Value));
            var vistasRecientes = vistas
                .Where(vista => vista.Value >= vistasDesde)
                .Select(vista => vista.Key)
                .ToHashSet();
            var semillasIds = interacciones
                .Where(interaccion => interaccion.DioLike || interaccion.MiPuntuacion >= 3)
                .Select(interaccion => interaccion.PublicacionId)
                .Distinct()
                .ToArray();
            var semillas = semillasIds.Length == 0
                ? new List<PublicacionDetalleDto>()
                : await _publicacionRepositorio.ObtenerPublicacionesPorIds(semillasIds, cancellationToken);

            return new ContextoFeed(
                interacciones, idsInteractuados, vistas, vistasRecientes, semillas, vistasDesde);
        }

        private async Task<CandidatosFeed> ObtenerCandidatosFeedAsync(
            int usuarioId, ContextoFeed contexto, int cantidadMaxima,
            CancellationToken cancellationToken)
        {
            var idsExcluidos = contexto.IdsInteractuados
                .Concat(contexto.Vistas.Keys).Distinct().ToArray();
            var pesos = new PesosRecomendacionFeedDto(
                _opcionesFeed.PesoLike, _opcionesFeed.PesoPuntuacion, _opcionesFeed.PesoEtiqueta);
            var recomendacionesColaborativas = await _publicacionRepositorio.ObtenerRecomendacionesFeed(
                usuarioId, cantidadMaxima, contexto.VistasDesde, pesos, cancellationToken);
            var candidatosGenerales = await _publicacionRepositorio.ObtenerPublicacionesParaVos(
                Array.Empty<CategoriaEnum>(), DateTime.UtcNow, null, cantidadMaxima,
                idsExcluidos, cancellationToken);
            var idsCandidatos = recomendacionesColaborativas.Select(r => r.PublicacionId)
                .Concat(candidatosGenerales.Select(p => p.Id))
                .Distinct()
                .ToArray();
            var vistasAntiguas = contexto.Vistas
                .Where(vista => vista.Value < contexto.VistasDesde)
                .OrderBy(vista => vista.Value)
                .Select(vista => vista.Key)
                .Where(id => !contexto.IdsInteractuados.Contains(id) && !idsCandidatos.Contains(id))
                .Take(cantidadMaxima)
                .ToArray();
            idsCandidatos = idsCandidatos.Concat(vistasAntiguas).Distinct().ToArray();

            // Las impresiones recientes son el último recurso para completar el feed.
            // Se agregan después de las publicaciones nunca vistas y de las vistas antiguas.
            var vistasRecientesFallback = contexto.Vistas
                .Where(vista => vista.Value >= contexto.VistasDesde)
                .OrderBy(vista => vista.Value)
                .Select(vista => vista.Key)
                .Where(id => !contexto.IdsInteractuados.Contains(id) && !idsCandidatos.Contains(id))
                .Take(cantidadMaxima)
                .ToArray();
            idsCandidatos = idsCandidatos.Concat(vistasRecientesFallback).Distinct().ToArray();

            var publicaciones = idsCandidatos.Length == 0
                ? new List<PublicacionDetalleDto>()
                : await _publicacionRepositorio.ObtenerPublicacionesPorIds(idsCandidatos, cancellationToken);

            return new CandidatosFeed(publicaciones, recomendacionesColaborativas);
        }

        private IReadOnlyList<int> OrdenarCandidatosFeed(
            ContextoFeed contexto, CandidatosFeed candidatos, int cantidadMaxima)
        {
            var recomendacionesPorId = candidatos.RecomendacionesColaborativas
                .GroupBy(recomendacion => recomendacion.PublicacionId)
                .ToDictionary(grupo => grupo.Key, grupo => grupo.Max(r => r.PuntajeColaborativo));
            var ultimaVistaPorId = contexto.Vistas;
            var pesosCategorias = CalcularAfinidadesCategorias(contexto.Interacciones, contexto.Semillas);
            var lugaresSemilla = contexto.Semillas.Where(semilla =>
                    contexto.Interacciones.Any(interaccion =>
                        interaccion.PublicacionId == semilla.Id && interaccion.DioLike))
                .Select(semilla => (semilla.Lugar.Latitud, semilla.Lugar.Longitud))
                .ToArray();

            return candidatos.Publicaciones
                .Where(publicacion => !contexto.IdsInteractuados.Contains(publicacion.Id))
                .OrderBy(publicacion => !ultimaVistaPorId.ContainsKey(publicacion.Id)
                    ? 0
                    : contexto.VistasRecientes.Contains(publicacion.Id) ? 2 : 1)
                .ThenBy(publicacion => ultimaVistaPorId.GetValueOrDefault(publicacion.Id, long.MaxValue))
                .ThenByDescending(publicacion =>
                    recomendacionesPorId.GetValueOrDefault(publicacion.Id)
                    + pesosCategorias.GetValueOrDefault(publicacion.Categoria)
                        * _opcionesFeed.PesoAfinidadCategoria
                    + CalcularPuntajeCercania(publicacion, lugaresSemilla,
                        _opcionesFeed.PesoCercania,
                        _opcionesFeed.DistanciaDeReferenciaKm))
                .ThenByDescending(publicacion => publicacion.FechaCreacion)
                .ThenByDescending(publicacion => publicacion.Id)
                .Take(cantidadMaxima)
                .Select(publicacion => publicacion.Id)
                .ToArray();
        }

        private sealed record ContextoFeed(
            IReadOnlyList<InteraccionFeedDto> Interacciones,
            HashSet<int> IdsInteractuados,
            Dictionary<int, long> Vistas,
            HashSet<int> VistasRecientes,
            List<PublicacionDetalleDto> Semillas,
            long VistasDesde);

        private sealed record CandidatosFeed(
            List<PublicacionDetalleDto> Publicaciones,
            IReadOnlyList<RecomendacionPublicacionDto> RecomendacionesColaborativas);

        private static Dictionary<CategoriaEnum, double> CalcularAfinidadesCategorias(
            IReadOnlyCollection<InteraccionFeedDto> interacciones,
            IReadOnlyCollection<PublicacionDetalleDto> semillas)
        {
            var pesos = interacciones
                .Where(interaccion => interaccion.DioLike || interaccion.MiPuntuacion >= 3)
                .GroupBy(interaccion => interaccion.PublicacionId)
                .ToDictionary(grupo => grupo.Key, grupo => grupo.Max(interaccion =>
                    interaccion.DioLike ? 1d : (interaccion.MiPuntuacion ?? 0) / 5d));
            var pesoTotal = pesos.Values.Sum();
            if (pesoTotal == 0) return new Dictionary<CategoriaEnum, double>();

            return semillas
                .Where(semilla => pesos.ContainsKey(semilla.Id))
                .GroupBy(semilla => semilla.Categoria)
                .ToDictionary(grupo => grupo.Key,
                    grupo => grupo.Sum(semilla => pesos[semilla.Id]) / pesoTotal);
        }

        private static double CalcularPuntajeCercania(PublicacionDetalleDto candidata,
            IReadOnlyCollection<(double Latitud, double Longitud)> lugaresSemilla,
            double peso, double distanciaKm)
        {
            if (lugaresSemilla.Count == 0) return 0;
            var distanciaMinima = lugaresSemilla.Min(lugar => DistanciaKm(
                candidata.Lugar.Latitud, candidata.Lugar.Longitud, lugar.Latitud, lugar.Longitud));
            return peso * Math.Exp(-distanciaMinima / distanciaKm);
        }

        private static double DistanciaKm(double latitud1, double longitud1, double latitud2, double longitud2)
        {
            const double radioTierraKm = 6371d;
            var dLatitud = GradosARadianes(latitud2 - latitud1);
            var dLongitud = GradosARadianes(longitud2 - longitud1);
            var a = Math.Pow(Math.Sin(dLatitud / 2), 2)
                    + Math.Cos(GradosARadianes(latitud1)) * Math.Cos(GradosARadianes(latitud2))
                    * Math.Pow(Math.Sin(dLongitud / 2), 2);
            a = Math.Clamp(a, 0d, 1d);
            return radioTierraKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double GradosARadianes(double grados) => grados * Math.PI / 180d;

        public async Task RegistrarImpresionesFeed(int usuarioId, IReadOnlyCollection<int> publicacionesIds,
            CancellationToken cancellationToken = default)
        {
            if (usuarioId <= 0) throw new ArgumentException("El ID del usuario debe ser mayor que cero.");
            if (publicacionesIds.Count is < 1 or > 50)
                throw new ArgumentException("Se deben enviar entre 1 y 50 publicaciones.");
            if (publicacionesIds.Any(id => id <= 0))
                throw new ArgumentException("Los IDs de publicaciones deben ser mayores que cero.");
            if (!await _usuarioRepositorio.ExisteUsuario(usuarioId, cancellationToken))
                throw new KeyNotFoundException("El usuario autenticado no existe.");

            var idsUnicos = publicacionesIds.Distinct().ToArray();
            var visibles = await _publicacionRepositorio.ObtenerPublicacionesPorIds(idsUnicos, cancellationToken);
            var idsVisibles = visibles.Select(publicacion => publicacion.Id).ToArray();
            if (idsVisibles.Length > 0)
                await _publicacionRepositorio.RegistrarImpresionesFeed(usuarioId, idsVisibles, cancellationToken);
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
                if (archivo == null && string.IsNullOrWhiteSpace(publicacion.UrlMultimedia))
                {
                    if (_opcionesPublicacion.RequerirMultimedia)
                        throw new ArgumentException("Debe subir un archivo o indicar una URL.");
                    // No invocar al proveedor ni inventar una imagen para recuerdos de texto.
                    return string.Empty;
                }

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
        private static void ValidarPuntaje(int puntaje)
        {
            if (puntaje < 1 || puntaje > 5)
                throw new ArgumentOutOfRangeException(nameof(puntaje), "La puntuación debe estar entre 1 y 5.");
        }
        private static void VerificarEliminar(bool eliminado)
        {
            if (!eliminado)
            {
                throw new KeyNotFoundException("La publicación no existe o no se pudo eliminar.");
            }
        }
        private static void VerificarNoEsPublicacionPropia(int usuarioId, Publicacion? publicacion)
        {
            if (publicacion != null && publicacion.UsuarioId == usuarioId)
            {
                throw new InvalidOperationException("No podés puntuar tu propia publicación.");
            }
        }
        private async Task<Publicacion> ObtenerPublicacionOriginalParaCompartir(int publicacionId, CancellationToken cancellationToken)
        {
            return await _publicacionRepositorio.ObtenerPorId(publicacionId, cancellationToken)
                ?? throw new KeyNotFoundException("La publicación original no existe o no está disponible.");
        }
        private static Publicacion CrearPublicacion(int usuarioId, string descripcion, Publicacion targetOriginal)
        {
            return new Publicacion
            {
                Titulo = targetOriginal.Titulo,
                Descripcion = descripcion.Trim(),
                Fecha = targetOriginal.Fecha,
                Categoria = targetOriginal.Categoria,
                UrlMultimedia = targetOriginal.UrlMultimedia,
                TipoMultimedia = targetOriginal.TipoMultimedia,
                LugarId = targetOriginal.LugarId,
                PublicacionOriginalId = targetOriginal.Id,
                UsuarioId = usuarioId,
                FechaCreación = DateTime.UtcNow,
                EstaOculto = false,
                MotivoOculto = null
            };
        }
        private static void ValidarDescripcion(string descripcion)
        {
            if (string.IsNullOrWhiteSpace(descripcion))
                throw new ArgumentException("La descripción de la cita no puede estar vacía.");
        }
        private async Task<List<PublicacionDetalleDto>> IncorporarRepostsDelUsuario(
            List<PublicacionDetalleDto> publicaciones, int usuarioId, CancellationToken cancellationToken)
        {
            var reposts = await _publicacionRepositorio.ObtenerRepostsDeSeguidos(new[] { usuarioId }, cancellationToken);
            if (reposts.Count == 0) return publicaciones;

            var usuario = await _usuarioRepositorio.ObtenerPorIdAsync(usuarioId);
            var usernameFallback = usuario?.Username ?? "";

            var mapaPubs = await ObtenerOriginalesDeReposts(
                reposts.Select(r => r.PublicacionId), cancellationToken);

            var itemsRepost = new List<PublicacionDetalleDto>();
            foreach (var r in reposts)
            {
                if (mapaPubs.TryGetValue(r.PublicacionId, out var orig))
                {
                    var nombreUsuario = !string.IsNullOrEmpty(r.ReposteadorUsername) ? r.ReposteadorUsername : usernameFallback;
                    itemsRepost.Add(CrearDetalleRepost(
                        orig, r.ReposteadorId, nombreUsuario, r.Fecha));
                }
            }

            return OrdenarPublicacionesConReposts(publicaciones, itemsRepost);
        }
        private async Task<Dictionary<int, PublicacionDetalleDto>> ObtenerOriginalesDeReposts(
           IEnumerable<int> publicacionesIds, CancellationToken cancellationToken)
        {
            var publicaciones = await _publicacionRepositorio.ObtenerPublicacionesPorIds(
                publicacionesIds.Distinct().ToList(), cancellationToken);
            return publicaciones.ToDictionary(publicacion => publicacion.Id);
        }
        private static PublicacionDetalleDto CrearDetalleRepost(
            PublicacionDetalleDto original, int usuarioId, string username, DateTime fecha)
        {
            return new PublicacionDetalleDto
            {
                Id = original.Id,
                Titulo = original.Titulo,
                Descripcion = original.Descripcion,
                Fecha = original.Fecha,
                FechaCreacion = fecha,
                Categoria = original.Categoria,
                UrlMultimedia = original.UrlMultimedia,
                TipoMultimedia = original.TipoMultimedia,
                Autor = original.Autor,
                Lugar = original.Lugar,
                Grupo = original.Grupo,
                PublicacionOriginal = original.PublicacionOriginal,
                ReposteadoPor = new RepostInfoEmergenteDto
                {
                    UsuarioId = usuarioId,
                    Username = username,
                    Fecha = fecha
                }
            };
        }
        private static List<PublicacionDetalleDto> OrdenarPublicacionesConReposts(
            IEnumerable<PublicacionDetalleDto> publicaciones, IEnumerable<PublicacionDetalleDto> reposts)
        {
            return publicaciones.Concat(reposts)
                .OrderByDescending(publicacion => publicacion.FechaCreacion)
                .ThenByDescending(publicacion => publicacion.Id)
                .ToList();
        }
        private async Task CompletarResumenesInteracciones(
            IReadOnlyCollection<PublicacionDetalleDto> publicaciones, int? usuarioId,
            CancellationToken cancellationToken)
        {
            await CompletarResumenesLikes(publicaciones, usuarioId ?? 0, cancellationToken);
            await CompletarResumenesPuntuaciones(publicaciones, usuarioId, cancellationToken);
            await CompletarResumenesReposts(publicaciones, usuarioId ?? 0, cancellationToken);
        }
        private async Task<List<PublicacionDetalleDto>> IncorporarRepostsDeSeguidos(
            List<PublicacionDetalleDto> publicaciones, IReadOnlyCollection<int> autoresIds,
            CursorCronologicoDto? cursorDecodificado, CancellationToken cancellationToken)
        {
            var reposts = await _publicacionRepositorio.ObtenerRepostsDeSeguidos(autoresIds, cancellationToken);
            if (reposts.Count == 0) return publicaciones;

            if (cursorDecodificado != null)
            {
                reposts = reposts.Where(r =>
                    r.Fecha < cursorDecodificado.FechaCreacion ||
                    (r.Fecha == cursorDecodificado.FechaCreacion && r.PublicacionId < cursorDecodificado.Id))
                    .ToList();
            }

            var mapaPubs = await ObtenerOriginalesDeReposts(
                reposts.Select(r => r.PublicacionId), cancellationToken);

            var reposteadoresIds = reposts.Select(r => r.ReposteadorId).Distinct().ToList();
            var usuariosReposteadores = await _usuarioRepositorio.ObtenerUsuariosPorIdsAsync(reposteadoresIds, cancellationToken);
            var mapaUsuarios = usuariosReposteadores.ToDictionary(u => u.Id, u => u.Username);

            var itemsRepost = new List<PublicacionDetalleDto>();
            foreach (var r in reposts)
            {
                if (mapaPubs.TryGetValue(r.PublicacionId, out var orig))
                {
                    var nombreUsuario = !string.IsNullOrWhiteSpace(r.ReposteadorUsername)
                        ? r.ReposteadorUsername
                        : (mapaUsuarios.TryGetValue(r.ReposteadorId, out var uName) ? uName : "");

                    itemsRepost.Add(CrearDetalleRepost(
                        orig, r.ReposteadorId, nombreUsuario, r.Fecha));
                }
            }

            return OrdenarPublicacionesConReposts(publicaciones, itemsRepost);
        }
        private async Task<(SesionFypDto Sesion, int Posicion)> ObtenerSesionFeed(
            int usuarioId, string? cursor, CancellationToken cancellationToken)
        {
            var cursorDecodificado = CursorFypCodec.Leer(cursor);

            SesionFypDto sesion;
            int posicion;

            if (cursorDecodificado == null)
            {
                var recomendaciones = await CrearRankingFeedAsync(usuarioId, cancellationToken);
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

            return (sesion, posicion);
        }

        private async Task<ResultadoPaginadoDto<PublicacionDetalleDto>> ObtenerPaginaFeed(
            SesionFypDto sesion, int posicion, int usuarioId, int limite, CancellationToken cancellationToken)
        {
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

            await CompletarResumenesInteracciones(items, usuarioId, cancellationToken);

            return new ResultadoPaginadoDto<PublicacionDetalleDto>
            {
                Items = items,
                SiguienteCursor = null
            };
        }
        private async Task ActualizarPuntuacion(
            int publicacionId, int usuarioId, int puntaje, Publicacion? publicacion,
            CancellationToken cancellationToken)
        {
            var estadoActual = await _publicacionRepositorio.ObtenerEstadoPuntuacion(publicacionId, usuarioId, cancellationToken);
            if (estadoActual.MiPuntuacion == puntaje)
            {
                // Toggle: si vuelve a presionar el mismo puntaje, se quita la puntuación
                await _publicacionRepositorio.QuitarPuntuacion(publicacionId, usuarioId, cancellationToken);
                return;
            }

            await _publicacionRepositorio.PuntuarPublicacion(
                publicacionId, usuarioId, puntaje, cancellationToken);

            if (publicacion != null && publicacion.UsuarioId != usuarioId)
            {
                await _notificacionServicio.NotificarPuntuacionEnPublicacionAsync(
                    publicacion.UsuarioId, publicacionId, usuarioId, cancellationToken);
            }
        }
        #region Verificación de Logros
        private async Task VerificacionLogrosCrearPublicacion(Publicacion publicacion, Point? gpsUsuario)
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

            int cantidadPublicaciones = await _usuarioRepositorio.ContarPublicacionesVisiblesAsync(usuarioId);
            if (cantidadPublicaciones >= 15)
            {
                await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.LinceDeLasPraderas);
            }

            if (!string.IsNullOrEmpty(publicacion.UrlMultimedia) && publicacion.TipoMultimedia == MultimediaEnum.Foto)
            {
                int posteosConFoto = await _publicacionRepositorio.ContarPosteosConImagenDelUsuario(usuarioId);

                if (posteosConFoto >= 10)
                {
                    await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.FotografoDelRecuerdo);
                }
            }

            int decadasDelUsuario = await _publicacionRepositorio.ContarPosteosDeDiferentesEpocasDelUsuario(usuarioId);
            if (decadasDelUsuario >= 3)
            {
                await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.VozDeUnaEpoca);
            }
            if (decadasDelUsuario == 6)
            {
                await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.DecadaCompleta);
            }
        }
        private async Task VerificacionLogrosPuntuacion(int usuarioId, Publicacion? publicacion, PuntuacionPublicacionDto estadoNuevo, CancellationToken cancellationToken)
        {
            int totalPuntosOtorgados = await _publicacionRepositorio.SumarPuntosOtorgadosPorUsuario(usuarioId, cancellationToken);
            if (totalPuntosOtorgados >= 1)
            {
                await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.Curioso);
            }
            if (totalPuntosOtorgados >= 50)
            {
                await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.Generoso);
            }

            if (publicacion != null)
            {
                int autorId = publicacion.UsuarioId;
                int puntosTotalesEnPost = estadoNuevo.PuntosTotales;

                if (puntosTotalesEnPost >= 20)
                {
                    await _logroServicio.VerificarYDarLogro(autorId, LogroEnum.Popular);
                }

                if (puntosTotalesEnPost >= 100)
                {
                    await _logroServicio.VerificarYDarLogro(autorId, LogroEnum.Leyenda);
                }
            }
        }
        private async Task VerificacionLogroCompartir(int usuarioId, CancellationToken cancellationToken)
        {
            int totalRepostsAjenos = await _publicacionRepositorio
                .ContarRepostsDeOtrosUsuarios(usuarioId, cancellationToken);

            if (totalRepostsAjenos >= 10)
            {
                await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.ViajeroDelTiempo);
            }
        }
        #endregion

        #endregion
    }
}
