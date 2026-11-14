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
        private readonly ICategoriaServicio? _categoriaServicio;

        public PublicacionServicio(
            IPublicacionRepositorio publicacionRepositorio,
            IUsuarioRepositorio usuarioRepositorio,
            IMultimediaServicio multimediaServicio,
            ISesionFypRepositorio sesionFypRepositorio,
            ISeguimientoServicio seguimientoServicio,
            IOptions<FeedRecomendacionesOptions> opcionesFeed,
            INotificacionServicio notificacionServicio,
            ILogroServicio logroServicio,
            IOptions<OpcionesPublicacion>? opcionesPublicacion = null, 
            IResumenPublicacionConsulta? resumenConsulta = null, 
            IProveedorRankingFeed? proveedorRanking = null, 
            ICategoriaServicio? categoriaServicio = null)
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
            _categoriaServicio = categoriaServicio;
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
            if (_categoriaServicio is not null) await _categoriaServicio.ExigirPermiso(usuarioId, global::ProyectoFinalTPI.Backend.Entidades.PermisoCuenta.PuntuarPublicacion, cancellationToken);
            ValidarPuntaje(puntaje);
            await ValidarInteraccionConPublicacion(publicacionId, usuarioId, cancellationToken);

            var publicacion = await _publicacionRepositorio.ObtenerPorId(publicacionId, cancellationToken);
            VerificarNoEsPublicacionPropia(usuarioId, publicacion);

            await ActualizarPuntuacion(publicacionId, usuarioId, puntaje, publicacion, cancellationToken);

            var estadoNuevo = await _publicacionRepositorio.ObtenerEstadoPuntuacion(publicacionId, usuarioId, cancellationToken);
            await VerificacionLogrosPuntuacion(usuarioId, publicacion, estadoNuevo, cancellationToken);
            await ActualizarCategoriaPorRecuerdo(publicacion, estadoNuevo.PuntosTotales, cancellationToken);

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
            if (_categoriaServicio is not null) await _categoriaServicio.ExigirPermiso(usuarioId, global::ProyectoFinalTPI.Backend.Entidades.PermisoCuenta.Compartir, cancellationToken);
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
            if (_categoriaServicio is not null) await _categoriaServicio.ExigirPermiso(usuarioId, global::ProyectoFinalTPI.Backend.Entidades.PermisoCuenta.Compartir, cancellationToken);
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
            var pagina = await LeerPaginaSeguidos(autores, avance, limite, cancellationToken);
            await CompletarInteraccionesPaginaFeed(pagina.Items, usuarioId, cancellationToken);
            etapa.Completar();
            return new()
            {
                Items = pagina.Items,
                SiguienteCursor = pagina.Agotado || pagina.Avance is null ? null
                    : CursorCronologicoCodec.Crear(pagina.Avance.FechaCreacion, pagina.Avance.Id, pagina.Avance.ReposteadorId)
            };
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
            // los .completar()  para tiempos de las etapas de Application insights

            using var total = new TelemetriaFeed("para-vos");
            try
            {
                SesionFypDto sesion;
                int posicion;
                using (var etapa = new TelemetriaFeed(cursor is null ? "sesion-nueva" : "sesion-existente"))
                {
                    // Obtiene la sesion del feed y la posicion
                    // La sesion: IDs de los recuerdos recomendados para el usuario, ordenados por ranking y fecha de creacion 
                    // La posición: donde seguir leyendoas
                    (sesion, posicion) = await ObtenerSesionFeed(usuarioId, cursor, cancellationToken);
                    etapa.Completar();
                }
                using var pagina = new TelemetriaFeed("pagina");

                //carga los datos de los recuerdos que corresponde mostrar
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

        public Task<int> Crear(Publicacion publicacion, Stream? archivo = null, string? nombreArchivo = null, Point? gpsUsuario = null) =>
            Crear(publicacion, archivo, nombreArchivo, gpsUsuario, null);

        public async Task<int> Crear(Publicacion publicacion, Stream? archivo, string? nombreArchivo, Point? gpsUsuario,
            IReadOnlyList<AdjuntoEntradaDto>? adjuntos)
        {
            await PrepararPublicacionNueva(publicacion, archivo, nombreArchivo, adjuntos);
            var id = await GuardarRecuerdo(publicacion);
            await VerificacionLogrosCrearPublicacion(publicacion, gpsUsuario);
            return id;
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
        private IReadOnlyList<AdjuntoEntradaDto> NormalizarAdjuntos(
            Publicacion publicacion, Stream? archivo, string? nombreArchivo,
            IReadOnlyList<AdjuntoEntradaDto>? adjuntos)
        {
            var entradas = ObtenerEntradasAdjuntas(publicacion, archivo, nombreArchivo, adjuntos);
            ValidarCantidadAdjuntos(entradas.Count);
            foreach (var entrada in entradas) ValidarAdjunto(entrada);
            return entradas;
        }

        private static void ValidarMusica(Publicacion publicacion)
        {
            if (publicacion.MusicaSpotifyId is null && publicacion.MusicaTitulo is null && publicacion.MusicaArtista is null) return;
            if (!Regex.IsMatch(publicacion.MusicaSpotifyId ?? "", "^[A-Za-z0-9]{22}$")
                || string.IsNullOrWhiteSpace(publicacion.MusicaTitulo) || publicacion.MusicaTitulo.Length > 120
                || string.IsNullOrWhiteSpace(publicacion.MusicaArtista) || publicacion.MusicaArtista.Length > 120)
                throw new ArgumentException("La música requiere una canción de Spotify, título y artista válidos.");
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
                Adjuntos = original.Adjuntos,
                Musica = original.Musica,
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
            //un feed nuevo o continuar con el que ya tenemos abierto ?
            var cursorDecodificado = CursorFypCodec.Leer(cursor);

            SesionFypDto sesion;
            int posicion;

            if (cursorDecodificado == null)
            {
                // SIN CURSOR: obtenemos un ranking nuevo y guardaMOS los IDs ordenados en una sesión
                var recomendaciones = _proveedorRanking is not null
                    ? await _proveedorRanking.ObtenerAsync(usuarioId, cancellationToken)
                    : await new GeneradorRankingFeed(_publicacionRepositorio,
                        Options.Create(_opcionesFeed)).GenerarAsync(usuarioId, cancellationToken);

                sesion = await _sesionFypRepositorio.CrearAsync(usuarioId, recomendaciones, cancellationToken);

                posicion = 0;
            }
            else
            {
                // CON CURSOR: obtenemos la sesion existente y la posicion del cursor|  
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
        // convertir los IDs de la sesion en una pagina de recuerdos para mostrar.
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
        private Task ActualizarCategoriaPorRecuerdo(
        Publicacion? publicacion,
        int puntosRecibidos,
        CancellationToken ct = default) =>
        _categoriaServicio?.ActualizarPorRecuerdo(
            publicacion, puntosRecibidos, ct)
        ?? Task.CompletedTask;

        private async Task PrepararPublicacionNueva(Publicacion publicacion, Stream? archivo, string? nombreArchivo,
            IReadOnlyList<AdjuntoEntradaDto>? adjuntos)
        {
            var entradas = await ValidarPublicacionNueva(publicacion, archivo, nombreArchivo, adjuntos);
            await ProcesarAdjuntos(publicacion, entradas);
            IniciarEstadoPublicacion(publicacion);
        }
        private async Task<IReadOnlyList<AdjuntoEntradaDto>> ValidarPublicacionNueva(
            Publicacion publicacion, Stream? archivo, string? nombreArchivo,
            IReadOnlyList<AdjuntoEntradaDto>? adjuntos)
        {
            ArgumentNullException.ThrowIfNull(publicacion);
            await VerificarAutorExistente(publicacion.UsuarioId);
            var entradas = NormalizarAdjuntos(publicacion, archivo, nombreArchivo, adjuntos);
            ValidarMusica(publicacion);
            if (_categoriaServicio is not null)
                await _categoriaServicio.ValidarCreacion(publicacion.UsuarioId, entradas.Count, publicacion.MusicaSpotifyId != null);
            return entradas;
        }

        private Task<int> GuardarRecuerdo(Publicacion publicacion) =>
            _categoriaServicio is null
                ? _publicacionRepositorio.Crear(publicacion)
                : _categoriaServicio.CrearRecuerdo(publicacion);

        private async Task ProcesarAdjuntos(Publicacion publicacion, IReadOnlyList<AdjuntoEntradaDto> entradas)
        {
            publicacion.Adjuntos.Clear();
            foreach (var entrada in entradas)
            {
                var url = await _multimediaServicio.SubirAsync(entrada.Archivo, entrada.NombreArchivo, entrada.Url, entrada.Tipo);
                publicacion.Adjuntos.Add(new AdjuntoPublicacion { Url = url, Tipo = entrada.Tipo, Orden = publicacion.Adjuntos.Count });
            }
            publicacion.UrlMultimedia = publicacion.Adjuntos.FirstOrDefault()?.Url ?? string.Empty;
            publicacion.TipoMultimedia = publicacion.Adjuntos.FirstOrDefault()?.Tipo ?? MultimediaEnum.Foto;
        }
        private static IReadOnlyList<AdjuntoEntradaDto> ObtenerEntradasAdjuntas(
    Publicacion publicacion, Stream? archivo, string? nombreArchivo,
    IReadOnlyList<AdjuntoEntradaDto>? adjuntos)
        {
            var tieneAnterior = archivo is not null || !string.IsNullOrWhiteSpace(publicacion.UrlMultimedia);
            if (adjuntos is { Count: > 0 } && tieneAnterior)
                throw new ArgumentException("No combines Adjuntos con Archivo o UrlArchivo.");
            return adjuntos is { Count: > 0 } ? adjuntos
                : tieneAnterior ? new[] { new AdjuntoEntradaDto(archivo, nombreArchivo, archivo is null ? publicacion.UrlMultimedia : null, publicacion.TipoMultimedia) }
                : Array.Empty<AdjuntoEntradaDto>();
        }

        private void ValidarCantidadAdjuntos(int cantidad)
        {
            if (cantidad > 4) throw new ArgumentException("Una publicación admite hasta 4 adjuntos.");
            if (_opcionesPublicacion.RequerirMultimedia && cantidad == 0)
                throw new ArgumentException("Debe subir un archivo o indicar una URL.");
        }

        private static void ValidarAdjunto(AdjuntoEntradaDto entrada)
        {
            if (!Enum.IsDefined(entrada.Tipo)) throw new ArgumentException("El tipo de adjunto no es válido.");
            if ((entrada.Archivo is null) == string.IsNullOrWhiteSpace(entrada.Url))
                throw new ArgumentException("Cada adjunto requiere un archivo o una URL, exclusivamente.");
            ValidarArchivoAdjunto(entrada.Archivo);
            ValidarUrlAdjunto(entrada);
        }

        private static void ValidarArchivoAdjunto(Stream? archivo)
        {
            if (archivo is { CanSeek: true } && (archivo.Length == 0 || archivo.Length > 50 * 1024 * 1024))
                throw new ArgumentException("Cada archivo debe tener contenido y ocupar hasta 50 MB.");
        }

        private static void ValidarUrlAdjunto(AdjuntoEntradaDto entrada)
        {
            if (entrada.Archivo is null
                && (!Uri.TryCreate(entrada.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https"))
                throw new ArgumentException("La URL del adjunto debe ser HTTPS.");
        }

        private async Task<(List<PublicacionDetalleDto> Items, CursorCronologicoDto? Avance, bool Agotado)> LeerPaginaSeguidos(
            IReadOnlyCollection<int> autores, CursorCronologicoDto? avance, int limite, CancellationToken cancellationToken)
        {
            var items = new List<PublicacionDetalleDto>();
            var agotado = false;
            for (var lote = 0; lote < _opcionesFeed.CantidadMaximaLotesSeguidos; lote++)
            {
                var cantidad = Math.Max(1, limite - items.Count);
                var datos = await ObtenerLoteSeguidos(autores, avance, cantidad, cancellationToken);
                foreach (var evento in datos.Eventos.Take(cantidad + 1))
                {
                    var detalle = ResolverDetalleEvento(evento, datos.Publicaciones, datos.Nombres);
                    if (detalle is not null)
                    {
                        if (items.Count == limite) return (items, avance, false);
                        items.Add(detalle);
                    }
                    avance = new() { FechaCreacion = evento.Fecha, Id = evento.PublicacionId, ReposteadorId = evento.ReposteadorId };
                }
                agotado = datos.Agotado;
                if (agotado || datos.Eventos.Length == 0) break;
            }
            return (items, avance, agotado);
        }
        private async Task<(EventoSeguido[] Eventos, Dictionary<int, PublicacionDetalleDto> Publicaciones,
    Dictionary<int, string> Nombres, bool Agotado)> ObtenerLoteSeguidos(
    IReadOnlyCollection<int> autores, CursorCronologicoDto? avance, int cantidad, CancellationToken cancellationToken)
        {
            var originales = await _publicacionRepositorio.ObtenerFeedSeguidos(autores, avance, cantidad, cancellationToken);
            var reposts = await _publicacionRepositorio.ObtenerRepostsDeSeguidosPaginados(autores, avance, cantidad, cancellationToken);
            var eventos = CrearEventosSeguidos(originales, reposts);
            var porId = originales.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
            await CompletarOriginalesDeReposts(eventos, cantidad, porId, cancellationToken);
            var nombres = await ObtenerNombresDeReposts(eventos, cantidad, cancellationToken);
            var agotado = eventos.Length <= cantidad + 1 && originales.Count < cantidad + 1 && reposts.Count < cantidad + 1;
            return (eventos, porId, nombres, agotado);
        }

        private static EventoSeguido[] CrearEventosSeguidos(
            IEnumerable<PublicacionDetalleDto> originales,
            IEnumerable<(int PublicacionId, int ReposteadorId, string ReposteadorUsername, DateTime Fecha)> reposts) =>
            originales.Select(p => new EventoSeguido(p.Id, 0, "", p.FechaCreacion, p))
                .Concat(reposts.Select(r => new EventoSeguido(r.PublicacionId, r.ReposteadorId, r.ReposteadorUsername, r.Fecha, null)))
                .OrderByDescending(e => e.Fecha).ThenByDescending(e => e.PublicacionId)
                .ThenByDescending(e => e.ReposteadorId).ToArray();

        private async Task CompletarOriginalesDeReposts(
            IEnumerable<EventoSeguido> eventos, int cantidad,
            Dictionary<int, PublicacionDetalleDto> porId, CancellationToken cancellationToken)
        {
            var ids = eventos.Take(cantidad + 1).Where(e => e.ReposteadorId > 0 && !porId.ContainsKey(e.PublicacionId))
                .Select(e => e.PublicacionId).Distinct().ToArray();
            if (ids.Length == 0) return;
            foreach (var publicacion in await _publicacionRepositorio.ObtenerPublicacionesPorIds(ids, cancellationToken))
                porId[publicacion.Id] = publicacion;
        }

        private async Task<Dictionary<int, string>> ObtenerNombresDeReposts(
            IEnumerable<EventoSeguido> eventos, int cantidad, CancellationToken cancellationToken)
        {
            var ids = eventos.Take(cantidad + 1).Where(e => e.ReposteadorId > 0 && string.IsNullOrWhiteSpace(e.Username))
                .Select(e => e.ReposteadorId).Distinct().ToArray();
            return ids.Length == 0 ? new Dictionary<int, string>()
                : (await _usuarioRepositorio.ObtenerUsuariosPorIdsAsync(ids, cancellationToken))
                    .ToDictionary(usuario => usuario.Id, usuario => usuario.Username);
        }

        private static PublicacionDetalleDto? ResolverDetalleEvento(
            EventoSeguido evento, IReadOnlyDictionary<int, PublicacionDetalleDto> publicaciones,
            IReadOnlyDictionary<int, string> nombres)
        {
            if (evento.ReposteadorId <= 0 || !publicaciones.TryGetValue(evento.PublicacionId, out var original))
                return evento.Original;
            var username = string.IsNullOrWhiteSpace(evento.Username)
                ? nombres.GetValueOrDefault(evento.ReposteadorId, "") : evento.Username;
            return CrearDetalleRepost(original, evento.ReposteadorId, username, evento.Fecha);
        }

        #region Verificación de Logros
        private async Task VerificacionLogrosCrearPublicacion(Publicacion publicacion, Point? gpsUsuario)
        {
            var usuarioId = publicacion.UsuarioId;

            await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.PrimerPaso);

            await VerificarLogroCronistaLocal(publicacion, gpsUsuario, usuarioId);
            
            await VerificarLogroLinceDeLasPraderas(usuarioId);
            
            await VerificarLogroFotografoDelRecuerdo(publicacion, usuarioId);

            await VerificarLogrosDeDecadas(usuarioId);
        }

        private async Task VerificarLogroFotografoDelRecuerdo(Publicacion publicacion, int usuarioId)
        {
            if (publicacion.Adjuntos.Any(a => a.Tipo == MultimediaEnum.Foto)
                || (!string.IsNullOrEmpty(publicacion.UrlMultimedia) && publicacion.TipoMultimedia == MultimediaEnum.Foto))
            {
                int posteosConFoto = await _publicacionRepositorio.ContarPosteosConImagenDelUsuario(usuarioId);

                if (posteosConFoto >= 10)
                {
                    await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.FotografoDelRecuerdo);
                }
            }
        }

        private async Task VerificarLogroLinceDeLasPraderas(int usuarioId)
        {
            int cantidadPublicaciones = await _usuarioRepositorio.ContarPublicacionesVisiblesAsync(usuarioId);
            if (cantidadPublicaciones >= 15)
            {
                await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.LinceDeLasPraderas);
            }
        }

        private async Task VerificarLogrosDeDecadas(int usuarioId)
        {
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

        private async Task VerificarLogroCronistaLocal(Publicacion publicacion, Point? gpsUsuario, int usuarioId)
        {
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
