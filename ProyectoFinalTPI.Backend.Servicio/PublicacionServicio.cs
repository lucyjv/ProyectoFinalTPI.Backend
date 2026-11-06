using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Mapa;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio.ProyectoFinalTPI.Backend.Servicio;
using NetTopologySuite.Geometries;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ProyectoFinalTPI.Backend.Servicio
{
    public class PublicacionServicio : IPublicacionServicio
    {
        private static readonly Regex PatronHashtag = new(
            @"(?:^|[\s(¿¡.,;:!?])#([\p{L}\p{N}_]+)", RegexOptions.Compiled);
        private static readonly Regex PatronNombreHashtag = new(
            @"^[\p{L}\p{N}_]{1,32}$", RegexOptions.Compiled);
        private static readonly CultureInfo CulturaHashtag = CultureInfo.GetCultureInfo("es");
        private readonly IPublicacionRepositorio _publicacionRepositorio;
        private readonly IUsuarioRepositorio _usuarioRepositorio;
        private readonly IMultimediaServicio _multimediaServicio;
        private readonly ISesionFypRepositorio _sesionFypRepositorio;
        private readonly ISeguimientoServicio _seguimientoServicio;
        private readonly FeedRecomendacionesOptions _opcionesFeed;
        private readonly IProveedorRankingFeed? _proveedorRanking;
        private readonly INotificacionServicio _notificacionServicio;
        private readonly ILogroServicio _logroServicio;
        private readonly OpcionesPublicacion _opcionesPublicacion;
        private readonly IResumenPublicacionConsulta? _resumenConsulta;

        public PublicacionServicio(
            IPublicacionRepositorio publicacionRepositorio,
            IUsuarioRepositorio usuarioRepositorio,
            IMultimediaServicio multimediaServicio,
            ISesionFypRepositorio sesionFypRepositorio,
            ISeguimientoServicio seguimientoServicio,
            IOptions<FeedRecomendacionesOptions> opcionesFeed,
            INotificacionServicio notificacionServicio,
            ILogroServicio logroServicio,
            IOptions<OpcionesPublicacion>? opcionesPublicacion = null, IResumenPublicacionConsulta? resumenConsulta = null, IProveedorRankingFeed? proveedorRanking = null)
        {
            _publicacionRepositorio = publicacionRepositorio;
            _resumenConsulta = resumenConsulta;
            _usuarioRepositorio = usuarioRepositorio;
            _multimediaServicio = multimediaServicio;
            _sesionFypRepositorio = sesionFypRepositorio;
            _seguimientoServicio = seguimientoServicio;
            _opcionesFeed = opcionesFeed.Value;
            _proveedorRanking = proveedorRanking;
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

        public async Task CompletarResumenesGuardados(
            IReadOnlyCollection<PublicacionDetalleDto> publicaciones, int usuarioId,
            CancellationToken cancellationToken = default)
        {
            if (publicaciones.Count == 0 || usuarioId <= 0) return;

            var resumenes = await _publicacionRepositorio.ObtenerResumenGuardados(
                publicaciones.Select(publicacion => publicacion.Id).Distinct().ToArray(),
                usuarioId,
                cancellationToken);

            foreach (var publicacion in publicaciones)
            {
                if (resumenes.TryGetValue(publicacion.Id, out var guardada))
                {
                    publicacion.Guardada = guardada;
                }
            }
        }

        public async Task<GuardadoPublicacionDto> Guardar(
            int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
        {
            await ValidarInteraccionConPublicacion(publicacionId, usuarioId, cancellationToken);
            await _publicacionRepositorio.CrearGuardado(publicacionId, usuarioId, cancellationToken);
            return await _publicacionRepositorio.ObtenerEstadoGuardado(publicacionId, usuarioId, cancellationToken);
        }

        public async Task<GuardadoPublicacionDto> QuitarGuardado(
            int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
        {
            await ValidarInteraccionConPublicacion(publicacionId, usuarioId, cancellationToken);
            await _publicacionRepositorio.QuitarGuardado(publicacionId, usuarioId, cancellationToken);
            return await _publicacionRepositorio.ObtenerEstadoGuardado(publicacionId, usuarioId, cancellationToken);
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

            return await (_resumenConsulta is null ? _publicacionRepositorio.ObtenerResumenPorId(id, cancellationToken) : _resumenConsulta.Obtener(id, cancellationToken))
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

        public async Task<List<PublicacionDetalleDto>> ObtenerGuardadasPorUsuarioId(
            int usuarioId, int? usuarioAutenticadoId, CancellationToken cancellationToken = default)
        {
            ValidarIdUsuario(usuarioId);

            var idsGuardadas = await _publicacionRepositorio.ObtenerIdsPublicacionesGuardadas(usuarioId, cancellationToken);
            if (idsGuardadas.Count == 0) return new List<PublicacionDetalleDto>();

            var publicaciones = await _publicacionRepositorio.ObtenerPublicacionesPorIds(idsGuardadas, cancellationToken);

            var orden = idsGuardadas.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
            publicaciones = publicaciones.OrderBy(p => orden.TryGetValue(p.Id, out var idx) ? idx : int.MaxValue).ToList();

            await CompletarResumenesInteracciones(publicaciones, usuarioAutenticadoId, cancellationToken);
            return publicaciones;
        }

        public async Task<ResultadoPaginadoDto<PublicacionDetalleDto>> ObtenerFeedSeguidos(
            int usuarioId, string? cursor, int limite, CancellationToken cancellationToken = default)
        {
            ValidarIdUsuario(usuarioId);
            ValidarLimite(limite);
            var avance = CursorCronologicoCodec.Leer(cursor);
            var autores = await _seguimientoServicio.ObtenerSeguidosIdsAsync(usuarioId, cancellationToken);
            if (autores.Count == 0) return new();
            using var etapa = new TelemetriaFeed("seguidos");
            var items = new List<PublicacionDetalleDto>();
            var agotado = false;
            for (var lote = 0; lote < _opcionesFeed.CantidadMaximaLotesSeguidos; lote++)
            {
                var cantidad = Math.Max(1, limite - items.Count);
                var originales = await _publicacionRepositorio.ObtenerFeedSeguidos(autores, avance, cantidad, cancellationToken);
                var reposts = await _publicacionRepositorio.ObtenerRepostsDeSeguidosPaginados(autores, avance, cantidad, cancellationToken);
                var eventos = originales.Select(p => new EventoSeguido(p.Id, 0, "", p.FechaCreacion, p))
                    .Concat(reposts.Select(r => new EventoSeguido(r.PublicacionId, r.ReposteadorId, r.ReposteadorUsername, r.Fecha, null)))
                    .OrderByDescending(e => e.Fecha).ThenByDescending(e => e.PublicacionId)
                    .ThenByDescending(e => e.ReposteadorId).ToArray();
                var porId = originales.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
                var idsFaltantes = eventos.Take(cantidad + 1).Where(e => e.ReposteadorId > 0 && !porId.ContainsKey(e.PublicacionId))
                    .Select(e => e.PublicacionId).Distinct().ToArray();
                if (idsFaltantes.Length > 0)
                    foreach (var p in await _publicacionRepositorio.ObtenerPublicacionesPorIds(idsFaltantes, cancellationToken)) porId[p.Id] = p;
                var idsSinNombre = eventos.Take(cantidad + 1).Where(e => e.ReposteadorId > 0 && string.IsNullOrWhiteSpace(e.Username))
                    .Select(e => e.ReposteadorId).Distinct().ToArray();
                var nombres = idsSinNombre.Length == 0 ? new Dictionary<int, string>()
                    : (await _usuarioRepositorio.ObtenerUsuariosPorIdsAsync(idsSinNombre, cancellationToken)).ToDictionary(u => u.Id, u => u.Username);
                foreach (var evento in eventos.Take(cantidad + 1))
                {
                    var detalle = evento.Original;
                    if (evento.ReposteadorId > 0 && porId.TryGetValue(evento.PublicacionId, out var original))
                        detalle = CrearDetalleRepost(original, evento.ReposteadorId,
                            string.IsNullOrWhiteSpace(evento.Username) ? nombres.GetValueOrDefault(evento.ReposteadorId, "") : evento.Username, evento.Fecha);
                    if (detalle is not null)
                    {
                        if (items.Count == limite) return await FinalizarSeguidos(avance, false);
                        items.Add(detalle);
                    }
                    avance = new() { FechaCreacion = evento.Fecha, Id = evento.PublicacionId, ReposteadorId = evento.ReposteadorId };
                }
                agotado = eventos.Length <= cantidad + 1 && originales.Count < cantidad + 1 && reposts.Count < cantidad + 1;
                if (agotado || eventos.Length == 0) break;
            }
            return await FinalizarSeguidos(avance, agotado);

            async Task<ResultadoPaginadoDto<PublicacionDetalleDto>> FinalizarSeguidos(CursorCronologicoDto? posicion, bool fin)
            {
                await CompletarInteraccionesPaginaFeed(items, usuarioId, cancellationToken);
                etapa.Completar();
                return new()
                {
                    Items = items,
                    SiguienteCursor = fin || posicion is null ? null
                        : CursorCronologicoCodec.Crear(posicion.FechaCreacion, posicion.Id, posicion.ReposteadorId)
                };
            }
        }

        private sealed record EventoSeguido(int PublicacionId, int ReposteadorId, string Username,
            DateTime Fecha, PublicacionDetalleDto? Original);

        public async Task<ResultadoPaginadoDto<PublicacionDetalleDto>> ObtenerPublicacionesParaVos(
        int usuarioId,
        string? cursor,
        int limite,
        CancellationToken cancellationToken = default)
        {
            ValidarIdUsuario(usuarioId);
            ValidarLimite(limite);

            using var total = new TelemetriaFeed("para-vos");
            try
            {
                SesionFypDto sesion;
                int posicion;
                using (var etapa = new TelemetriaFeed(cursor is null ? "sesion-nueva" : "sesion-existente"))
                {
                    (sesion, posicion) = await ObtenerSesionFeed(usuarioId, cursor, cancellationToken);
                    etapa.Completar();
                }
                using var pagina = new TelemetriaFeed("pagina");
                var resultado = await ObtenerPaginaFeed(sesion, posicion, usuarioId, limite, cancellationToken);
                pagina.Completar();
                total.Completar();
                return resultado;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                total.Cancelar();
                throw;
            }
        }

        public async Task<ResultadoPaginadoDto<PublicacionDetalleDto>> ObtenerPublicacionesPorHashtag(
            string hashtag, string? cursor, int limite, int? usuarioId, CancellationToken cancellationToken = default)
        {
            var nombre = NormalizarHashtag(hashtag);
            ValidarLimite(limite);
            var cursorDecodificado = CursorCronologicoCodec.Leer(cursor);
            var ids = await _publicacionRepositorio.ObtenerIdsPorHashtag(
                nombre, cursorDecodificado, limite, cancellationToken);
            var idsPagina = ids.Take(limite).ToArray();
            var publicaciones = await _publicacionRepositorio.ObtenerPublicacionesPorIds(idsPagina, cancellationToken);
            var porId = publicaciones.ToDictionary(p => p.Id);
            var items = idsPagina.Where(porId.ContainsKey).Select(id => porId[id]).ToList();
            await CompletarResumenesInteracciones(items, usuarioId, cancellationToken);

            return new ResultadoPaginadoDto<PublicacionDetalleDto>
            {
                Items = items,
                SiguienteCursor = ids.Count > limite && items.Count > 0
                    ? CursorCronologicoCodec.Crear(items[^1].FechaCreacion, items[^1].Id)
                    : null
            };
        }

        public async Task<List<TendenciaHashtagDto>> ObtenerTendenciasHashtags(
            int limite, CancellationToken cancellationToken = default)
        {
            ValidarLimite(limite);
            var desde = DateTime.UtcNow.AddDays(-7);
            var publicaciones = await _publicacionRepositorio.ObtenerDescripcionesParaHashtags(desde, cancellationToken);
            return AgruparHashtags(publicaciones).Take(limite).ToList();
        }

        public async Task<List<TendenciaHashtagDto>> BuscarHashtags(
            string consulta, int limite, CancellationToken cancellationToken = default)
        {
            ValidarLimite(limite);
            if (string.IsNullOrWhiteSpace(consulta)) return new List<TendenciaHashtagDto>();
            var prefijo = NormalizarHashtag(consulta);
            var publicaciones = await _publicacionRepositorio.ObtenerDescripcionesParaHashtags(null, cancellationToken);
            return AgruparHashtags(publicaciones)
                .Where(t => t.Hashtag.StartsWith(prefijo, StringComparison.Ordinal))
                .Take(limite).ToList();
        }

        private static string NormalizarHashtag(string valor)
        {
            var texto = valor.Trim();
            var nombre = (texto.StartsWith('#') ? texto[1..] : texto)
                .Normalize(NormalizationForm.FormKC).ToLower(CulturaHashtag);
            if (!PatronNombreHashtag.IsMatch(nombre))
                throw new ArgumentException("El hashtag debe tener entre 1 y 32 letras, números o guiones bajos.");
            return nombre;
        }

        private static IEnumerable<string> ExtraerHashtags(string descripcion) =>
            PatronHashtag.Matches(descripcion.Normalize(NormalizationForm.FormKC))
                .Select(m => m.Groups[1].Value.ToLower(CulturaHashtag))
                .Where(nombre => nombre.Length <= 32)
                .Distinct(StringComparer.Ordinal);

        private static List<TendenciaHashtagDto> AgruparHashtags(
            IEnumerable<(string Descripcion, int UsuarioId)> publicaciones)
        {
            var conteos = new Dictionary<string, (int Cantidad, HashSet<int> Autores)>(StringComparer.Ordinal);
            foreach (var (descripcion, usuarioId) in publicaciones)
            {
                foreach (var hashtag in ExtraerHashtags(descripcion))
                {
                    if (!conteos.TryGetValue(hashtag, out var estado))
                        estado = (0, new HashSet<int>());
                    estado.Autores.Add(usuarioId);
                    conteos[hashtag] = (estado.Cantidad + 1, estado.Autores);
                }
            }

            return conteos.Select(par => new TendenciaHashtagDto
                {
                    Hashtag = par.Key,
                    CantidadPublicaciones = par.Value.Cantidad,
                    CantidadAutores = par.Value.Autores.Count
                })
                .OrderByDescending(t => t.CantidadPublicaciones)
                .ThenByDescending(t => t.CantidadAutores)
                .ThenBy(t => t.Hashtag, StringComparer.Ordinal)
                .ToList();
        }

        #endregion

        #region Creacion y Eliminacion

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
            var idsVisibles = await _publicacionRepositorio.ObtenerIdsVisibles(idsUnicos, cancellationToken);
            if (idsVisibles.Count > 0)
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
        private async Task CompletarInteraccionesPaginaFeed(
            IReadOnlyCollection<PublicacionDetalleDto> publicaciones, int usuarioId, CancellationToken cancellationToken)
        {
            if (publicaciones.Count == 0) return;
            using var etapa = new TelemetriaFeed("resumen-interacciones");
            var resumenes = await _publicacionRepositorio.ObtenerResumenInteraccionesFeed(
                publicaciones.Select(p => p.Id).Distinct().ToArray(), usuarioId, cancellationToken);
            foreach (var publicacion in publicaciones)
            {
                if (!resumenes.TryGetValue(publicacion.Id, out var resumen)) continue;
                publicacion.CantidadLikes = resumen.CantidadLikes;
                publicacion.DioLike = resumen.DioLike;
                publicacion.PuntosTotales = resumen.PuntosTotales;
                publicacion.MiPuntuacion = resumen.MiPuntuacion;
                publicacion.CantidadReposts = resumen.CantidadReposts;
                publicacion.DioRepost = resumen.DioRepost;
                publicacion.Guardada = resumen.Guardada;
            }
            etapa.Completar();
        }

        private async Task CompletarResumenesInteracciones(
            IReadOnlyCollection<PublicacionDetalleDto> publicaciones, int? usuarioId,
            CancellationToken cancellationToken)
        {
            await CompletarResumenesLikes(publicaciones, usuarioId ?? 0, cancellationToken);
            await CompletarResumenesPuntuaciones(publicaciones, usuarioId, cancellationToken);
            await CompletarResumenesReposts(publicaciones, usuarioId ?? 0, cancellationToken);
            await CompletarResumenesGuardados(publicaciones, usuarioId ?? 0, cancellationToken);
        }
        private async Task<(SesionFypDto Sesion, int Posicion)> ObtenerSesionFeed(
            int usuarioId, string? cursor, CancellationToken cancellationToken)
        {
            var cursorDecodificado = CursorFypCodec.Leer(cursor);

            SesionFypDto sesion;
            int posicion;

            if (cursorDecodificado == null)
            {
                var recomendaciones = _proveedorRanking is not null
                    ? await _proveedorRanking.ObtenerAsync(usuarioId, cancellationToken)
                    : await new GeneradorRankingFeed(_publicacionRepositorio,
                        Options.Create(_opcionesFeed)).GenerarAsync(usuarioId, cancellationToken);
                sesion = await _sesionFypRepositorio.CrearAsync(usuarioId, recomendaciones, cancellationToken);

                posicion = 0;
            }
            else
            {
                sesion = await _sesionFypRepositorio.ObtenerAsync(cursorDecodificado.SesionId, usuarioId, cancellationToken)
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
            var tamanioLote = limite + 1;

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
                            await CompletarInteraccionesPaginaFeed(items, usuarioId, cancellationToken);

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
                // Si hay bajas u ocultamientos, avanzamos en lotes mayores sin consultar ID por ID.
                tamanioLote = Math.Min(50, tamanioLote * 2);
            }

            await CompletarInteraccionesPaginaFeed(items, usuarioId, cancellationToken);

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
