using System.Text.Json;
using Microsoft.Extensions.Options;
using Moq;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Dtos.Lugar;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests
{
    public class PublicacionServicioTests
    {
        private readonly Mock<IPublicacionRepositorio> _publicacionRepositorioMock = new();
        private readonly Mock<IUsuarioRepositorio> _usuarioRepositorioMock = new();
        private readonly Mock<IMultimediaServicio> _multimediaMock = new();
        private readonly Mock<ISesionFypRepositorio> _sesionFypRepositorioMock = new();
        private readonly Mock<ISeguimientoServicio> _seguimientoMock = new();
        private readonly Mock<INotificacionServicio> _notificacionMock = new();
        private readonly Mock<ILogroServicio> _logroMock = new();

        private readonly List<PublicacionDetalleDto> _catalogoPublicaciones = new();
        private readonly List<SesionFypDto> _sesionesCreadas = new();
        private readonly List<(int PublicacionId, int ReposteadorId, string ReposteadorUsername, DateTime Fecha)> _repostsSeguidos = new();
        private int? _ultimoPuntajeGuardado;
        private readonly PublicacionServicio _servicio;

        public PublicacionServicioTests()
        {
            // Comportamiento base seguro para consultas de publicaciones e interacciones
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerResumenLikes(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<int> ids, int _, CancellationToken _) =>
                    ids.Distinct().ToDictionary(id => id, _ => (Cantidad: 0, DioLike: false)));

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerResumenPuntuaciones(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<int> ids, int? _, CancellationToken _) =>
                    ids.Distinct().ToDictionary(id => id, _ => (PuntosTotales: 0, MiPuntuacion: (int?)null)));

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerResumenReposts(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<int> ids, int _, CancellationToken _) =>
                    ids.Distinct().ToDictionary(id => id, _ => (1, false)));

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerResumenGuardados(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<int> ids, int _, CancellationToken _) =>
                    ids.Distinct().ToDictionary(id => id, _ => false));

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerEstadoRepost(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int publicacionId, int _, CancellationToken _) => new RepostPublicacionDto
                {
                    PublicacionId = publicacionId,
                    CantidadReposts = 1,
                    DioRepost = true
                });

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerRepostsDeSeguidos(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _repostsSeguidos);

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerPublicacionesPorIds(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
                    _catalogoPublicaciones.Where(p => ids.Contains(p.Id)).ToList());

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerInteraccionesFeed(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<InteraccionFeedDto>());

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerRecomendacionesFeed(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<PesosRecomendacionFeedDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<RecomendacionPublicacionDto>());

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerPublicacionesParaVos(It.IsAny<IReadOnlyCollection<CategoriaEnum>>(), It.IsAny<DateTime>(), It.IsAny<CursorCronologicoDto?>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<CategoriaEnum> _, DateTime fechaCorte, CursorCronologicoDto? _, int limite, IReadOnlyCollection<int> excluirIds, CancellationToken _) =>
                    _catalogoPublicaciones
                        .Where(p => p.FechaCreacion <= fechaCorte && !excluirIds.Contains(p.Id))
                        .OrderByDescending(p => p.FechaCreacion).ThenByDescending(p => p.Id)
                        .Take(limite + 1)
                        .ToList());

            _publicacionRepositorioMock
                .Setup(r => r.PuntuarPublicacion(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<int, int, int, CancellationToken>((_, _, puntaje, _) => _ultimoPuntajeGuardado = puntaje)
                .Returns(Task.CompletedTask);

            _publicacionRepositorioMock
                .Setup(r => r.QuitarPuntuacion(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<int, int, CancellationToken>((_, _, _) => _ultimoPuntajeGuardado = null)
                .Returns(Task.CompletedTask);

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerEstadoPuntuacion(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int publicacionId, int? _, CancellationToken _) => new PuntuacionPublicacionDto
                {
                    PublicacionId = publicacionId,
                    PuntosTotales = _ultimoPuntajeGuardado ?? 0,
                    MiPuntuacion = _ultimoPuntajeGuardado
                });

            _usuarioRepositorioMock
                .Setup(u => u.ObtenerUsuariosPorIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
                    ids.Select(id => new AutorResumenDto { Id = id, Username = $"usuario{id}" }).ToList());

            _multimediaMock
                .Setup(m => m.SubirAsync(It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<MultimediaEnum>()))
                .ReturnsAsync("https://example.com/subida.jpg");

            // Configuración en memoria para simular sesiones FYP
            _sesionFypRepositorioMock
                .Setup(s => s.Crear(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>()))
                .Returns((int usuarioId, IReadOnlyCollection<int> publicacionesIds) =>
                {
                    var sesion = new SesionFypDto(Guid.NewGuid(), usuarioId, DateTime.UtcNow.AddMinutes(30), publicacionesIds);
                    _sesionesCreadas.Add(sesion);
                    return sesion;
                });

            _sesionFypRepositorioMock
                .Setup(s => s.Obtener(It.IsAny<Guid>(), It.IsAny<int>()))
                .Returns((Guid sesionId, int usuarioId) =>
                    _sesionesCreadas.SingleOrDefault(s => s.Id == sesionId && s.UsuarioId == usuarioId));

            _servicio = new PublicacionServicio(
                _publicacionRepositorioMock.Object,
                _usuarioRepositorioMock.Object,
                _multimediaMock.Object,
                _sesionFypRepositorioMock.Object,
                _seguimientoMock.Object,
                Options.Create(new FeedRecomendacionesOptions()),
                _notificacionMock.Object,
                _logroMock.Object);
        }

        #region Helpers Reutilizables

        private void ConfigurarUsuarioExiste(int usuarioId = 10, bool existe = true)
        {
            _usuarioRepositorioMock
                .Setup(u => u.ExisteUsuario(usuarioId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existe);
        }

        private void ConfigurarPublicacionVisible(int publicacionId = 1, bool visible = true)
        {
            _publicacionRepositorioMock
                .Setup(r => r.ExistePublicacionVisible(publicacionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(visible);
        }

        private static Publicacion CrearPublicacionEjemplo(int usuarioId = 10, string? urlMultimedia = "https://example.com/original.jpg")
        {
            return new Publicacion
            {
                UsuarioId = usuarioId,
                Titulo = "Recuerdo del Obelisco",
                Descripcion = "Una visita a Buenos Aires",
                Lugar = new Lugar
                {
                    Nombre = "Obelisco",
                    Coordenadas = new NetTopologySuite.Geometries.Point(-58.3816, -34.6037) { SRID = 4326 }
                },
                Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Categoria = CategoriaEnum.Lugares,
                TipoMultimedia = MultimediaEnum.Foto,
                UrlMultimedia = urlMultimedia ?? "https://example.com/original.jpg"
            };
        }

        private static PublicacionFiltroDto CrearFiltroPinesValido() => new()
        {
            MinLatitud = -35,
            MaxLatitud = -34,
            MinLongitud = -59,
            MaxLongitud = -58
        };

        private static PublicacionDetalleDto PublicacionFeed(int id, CategoriaEnum categoria, int anio,
            double latitud = 0, double longitud = 0) => new()
        {
            Id = id,
            Categoria = categoria,
            Fecha = new DateTime(anio, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            FechaCreacion = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Lugar = new LugarDetalleDto { Latitud = latitud, Longitud = longitud }
        };

        #endregion

        #region Creación de Publicaciones

        [Fact]
        public async Task QueSePuedaCrearUnaPublicacionConUnAutorExistente()
        {
            //Arrange
            ConfigurarUsuarioExiste(10);
            Publicacion? creada = null;
            _publicacionRepositorioMock
                .Setup(r => r.Crear(It.IsAny<Publicacion>()))
                .Callback<Publicacion>(p => creada = p)
                .ReturnsAsync(25);

            var publicacion = CrearPublicacionEjemplo(10, "https://example.com/original.jpg");

            //Act
            var result = await _servicio.Crear(publicacion);

            //Assert
            Assert.Equal(25, result);
            Assert.NotNull(creada);
            Assert.Equal(publicacion.Titulo, creada.Titulo);
            Assert.Equal("Obelisco", creada.Lugar.Nombre);
            Assert.Equal(10, creada.UsuarioId);
            Assert.Equal("https://example.com/subida.jpg", creada.UrlMultimedia);
            _multimediaMock.Verify(m => m.SubirAsync(null, null, "https://example.com/original.jpg", MultimediaEnum.Foto), Times.Once);
            _usuarioRepositorioMock.Verify(u => u.ExisteUsuario(10, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QueNoSeCreeUnaPublicacionSiElAutorNoExiste()
        {
            //Arrange
            ConfigurarUsuarioExiste(99, existe: false);
            var publicacion = new Publicacion { UsuarioId = 99 };

            //Act
            var result = await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.Crear(publicacion));

            //Assert
            Assert.Contains("usuario", result.Message);
            _multimediaMock.Verify(m => m.SubirAsync(It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<MultimediaEnum>()), Times.Never);
            _publicacionRepositorioMock.Verify(r => r.Crear(It.IsAny<Publicacion>()), Times.Never);
        }

        [Fact]
        public async Task QueNoSeGuardeUnaPublicacionSiFallaLaSubidaMultimedia()
        {
            //Arrange
            ConfigurarUsuarioExiste(10);
            var error = new InvalidOperationException("Error al subir la imagen");
            _multimediaMock
                .Setup(m => m.SubirAsync(It.IsAny<Stream?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<MultimediaEnum>()))
                .ThrowsAsync(error);
            var publicacion = new Publicacion { UsuarioId = 10, UrlMultimedia = "https://example.com/foto.jpg" };

            //Act
            var result = await Assert.ThrowsAsync<InvalidOperationException>(() => _servicio.Crear(publicacion));

            //Assert
            Assert.Same(error, result);
            _publicacionRepositorioMock.Verify(r => r.Crear(It.IsAny<Publicacion>()), Times.Never);
        }

        [Fact]
        public async Task QueSeConserveElLugarDeLaEntidad()
        {
            //Arrange
            ConfigurarUsuarioExiste(10);
            Publicacion? creada = null;
            _publicacionRepositorioMock
                .Setup(r => r.Crear(It.IsAny<Publicacion>()))
                .Callback<Publicacion>(p => creada = p)
                .ReturnsAsync(1);

            var publicacion = new Publicacion
            {
                UsuarioId = 10,
                Lugar = new Lugar
                {
                    Nombre = "Obelisco",
                    Coordenadas = new NetTopologySuite.Geometries.Point(-58.3816, -34.6037) { SRID = 4326 }
                }
            };

            //Act
            await _servicio.Crear(publicacion);

            //Assert
            Assert.NotNull(creada);
            Assert.Equal(-58.3816, creada.Lugar.Coordenadas.X);
            Assert.Equal(-34.6037, creada.Lugar.Coordenadas.Y);
            Assert.Equal(4326, creada.Lugar.Coordenadas.SRID);
        }

        #endregion

        #region Eliminación de Publicaciones

        [Fact]
        public async Task QueSeElimineUnaPublicacionCorrectamente()
        {
            //Arrange
            _publicacionRepositorioMock
                .Setup(r => r.Eliminar(5, 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            //Act
            await _servicio.Eliminar(5, 10);

            //Assert
            _publicacionRepositorioMock.Verify(r => r.Eliminar(5, 10, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QueFalleAlEliminarSiElIdEsInvalido()
        {
            //Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Eliminar(0));
        }

        [Fact]
        public async Task QueFalleAlEliminarSiLaPublicacionNoExiste()
        {
            //Arrange
            _publicacionRepositorioMock
                .Setup(r => r.Eliminar(99, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            //Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.Eliminar(99));
        }

        #endregion

        #region Pines en Mapa

        [Fact]
        public async Task QueNoSeObtenganPinesConUnFiltroNulo()
        {
            //Arrange
            PublicacionFiltroDto filtro = null!;

            //Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _servicio.ObtenerPines(filtro));
        }

        [Fact]
        public async Task QueNoSeObtenganPinesConUnaCategoriaInvalida()
        {
            //Arrange
            var filtro = new PublicacionFiltroDto
            {
                MinLatitud = -35, MaxLatitud = -34, MinLongitud = -59, MaxLongitud = -58,
                Categoria = (CategoriaEnum)999
            };

            //Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ObtenerPines(filtro));
        }

        [Theory]
        [InlineData(-181, -58)]
        [InlineData(-59, 181)]
        [InlineData(-58, -59)]
        [InlineData(-58, -58)]
        [InlineData(double.NaN, -58)]
        [InlineData(-59, double.PositiveInfinity)]
        public async Task QueNoSeObtenganPinesConLongitudesInvalidas(double minimo, double maximo)
        {
            //Arrange
            var filtro = new PublicacionFiltroDto
            {
                MinLatitud = -35, MaxLatitud = -34, MinLongitud = minimo, MaxLongitud = maximo
            };

            //Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ObtenerPines(filtro));
        }

        [Fact]
        public async Task QueSeDevuelvanDoscientosPinesYSeIndiqueQueHayMas()
        {
            //Arrange
            var filtro = CrearFiltroPinesValido();
            var pines = Enumerable.Range(1, 201)
                .Select(id => new PublicacionPinDto { Id = id }).ToList();

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerPines(filtro, 200, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pines);

            //Act
            var result = await _servicio.ObtenerPines(filtro);

            //Assert
            Assert.Equal(200, result.Pines.Count);
            Assert.True(result.HayMas);
            Assert.Equal(200, result.Pines[^1].Id);
            _publicacionRepositorioMock.Verify(r => r.ObtenerPines(filtro, 200, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(200)]
        public async Task QueNoSeIndiqueQueHayMasSiLosPinesNoSuperanElLimite(int cantidad)
        {
            //Arrange
            var filtro = CrearFiltroPinesValido();
            var pines = Enumerable.Range(1, cantidad)
                .Select(id => new PublicacionPinDto { Id = id }).ToList();

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerPines(filtro, 200, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pines);

            //Act
            var result = await _servicio.ObtenerPines(filtro);

            //Assert
            Assert.Equal(cantidad, result.Pines.Count);
            Assert.False(result.HayMas);
        }

        #endregion

        #region Resumen y Detalle

        [Fact]
        public async Task QueSePuedaObtenerElResumenDeUnaPublicacion()
        {
            //Arrange
            var resumenEsperado = new PublicacionResumenDto { Id = 5, Titulo = "Recuerdo" };
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerResumenPorId(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(resumenEsperado);

            //Act
            var result = await _servicio.ObtenerResumenPorId(5);

            //Assert
            Assert.NotNull(result);
            Assert.Equal("Recuerdo", result.Titulo);
            Assert.Same(resumenEsperado, result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task QueNoSeObtengaElResumenConUnIdInvalido(int id)
        {
            //Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ObtenerResumenPorId(id));
        }

        [Fact]
        public async Task QueSePuedaObtenerElDetalleDeUnaPublicacion()
        {
            //Arrange
            var detalleEsperado = new PublicacionDetalleDto { Id = 5, Titulo = "Recuerdo completo" };
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerDetallePorId(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(detalleEsperado);

            //Act
            var result = await _servicio.ObtenerDetallePorId(5);

            //Assert
            Assert.NotNull(result);
            Assert.Equal("Recuerdo completo", result.Titulo);
            Assert.Same(detalleEsperado, result);
        }

        #endregion

        #region Feed de Seguidos

        [Fact]
        public async Task QueSeDevuelvaUnFeedVacioSiElUsuarioNoSigueANadie()
        {
            //Arrange
            _seguimientoMock
                .Setup(s => s.ObtenerSeguidosIdsAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<int>());

            //Act
            var result = await _servicio.ObtenerFeedSeguidos(10, null, 20);

            //Assert
            Assert.Empty(result.Items);
            Assert.Null(result.SiguienteCursor);
            _publicacionRepositorioMock.Verify(r => r.ObtenerFeedSeguidos(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CursorCronologicoDto?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task QueSeConsultenLasPublicacionesDeLosAutoresDevueltosPorSeguimiento()
        {
            //Arrange
            _seguimientoMock
                .Setup(s => s.ObtenerSeguidosIdsAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { 20, 30 });

            var feed = new List<PublicacionDetalleDto>
            {
                new() { Id = 1 }, new() { Id = 2 }, new() { Id = 3 }, new() { Id = 4 }
            };

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerFeedSeguidos(It.Is<IReadOnlyCollection<int>>(ids => ids.Contains(20) && ids.Contains(30)), null, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(feed);

            //Act
            var result = await _servicio.ObtenerFeedSeguidos(10, null, 20);

            //Assert
            _seguimientoMock.Verify(s => s.ObtenerSeguidosIdsAsync(10, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(new[] { 1, 2, 3, 4 }, result.Items.Select(p => p.Id));
        }

        [Fact]
        public async Task QueNoSeConsultenPublicacionesSiFallaLaConsultaDeSeguidos()
        {
            //Arrange
            var error = new InvalidOperationException("No se pudieron consultar los seguidos");
            _seguimientoMock
                .Setup(s => s.ObtenerSeguidosIdsAsync(10, It.IsAny<CancellationToken>()))
                .ThrowsAsync(error);

            //Act
            var result = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _servicio.ObtenerFeedSeguidos(10, null, 20));

            //Assert
            Assert.Same(error, result);
            _publicacionRepositorioMock.Verify(r => r.ObtenerFeedSeguidos(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CursorCronologicoDto?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task QueSePagineElFeedSeguidosUsandoLaUltimaPublicacionEntregada()
        {
            //Arrange
            _seguimientoMock
                .Setup(s => s.ObtenerSeguidosIdsAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { 20 });

            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var feed = new List<PublicacionDetalleDto>
            {
                new() { Id = 12, FechaCreacion = fecha },
                new() { Id = 11, FechaCreacion = fecha },
                new() { Id = 10, FechaCreacion = fecha.AddMinutes(-1) }
            };

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerFeedSeguidos(It.IsAny<IReadOnlyCollection<int>>(), null, 2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(feed);

            CursorCronologicoDto? cursorSegundaConsulta = null;
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerFeedSeguidos(It.IsAny<IReadOnlyCollection<int>>(), It.IsNotNull<CursorCronologicoDto>(), 2, It.IsAny<CancellationToken>()))
                .Callback<IReadOnlyCollection<int>, CursorCronologicoDto?, int, CancellationToken>((_, c, _, _) => cursorSegundaConsulta = c)
                .ReturnsAsync(new List<PublicacionDetalleDto>());

            //Act
            var result = await _servicio.ObtenerFeedSeguidos(10, null, 2);
            await _servicio.ObtenerFeedSeguidos(10, result.SiguienteCursor, 2);

            //Assert
            Assert.Equal(new[] { 12, 11 }, result.Items.Select(p => p.Id));
            Assert.NotNull(result.SiguienteCursor);
            Assert.NotNull(cursorSegundaConsulta);
            Assert.Equal(11, cursorSegundaConsulta.Id);
            Assert.Equal(fecha, cursorSegundaConsulta.FechaCreacion);
        }

        [Fact]
        public async Task QueLaUltimaPaginaDelFeedSeguidosNoTengaSiguienteCursor()
        {
            //Arrange
            _seguimientoMock
                .Setup(s => s.ObtenerSeguidosIdsAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { 20 });

            var feed = new List<PublicacionDetalleDto>
            {
                new() { Id = 12 }, new() { Id = 11 }
            };

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerFeedSeguidos(It.IsAny<IReadOnlyCollection<int>>(), null, 2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(feed);

            //Act
            var result = await _servicio.ObtenerFeedSeguidos(10, null, 2);

            //Assert
            Assert.Equal(2, result.Items.Count);
            Assert.Null(result.SiguienteCursor);
        }

        #endregion

        #region Feed Para Vos / FYP y Sesiones

        [Fact]
        public async Task QueSeGenerenRecomendacionesYUnaSesionSiNoSeEnviaCursor()
        {
            //Arrange
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerRecomendacionesFeed(10, 500, It.IsAny<long>(), It.IsAny<PesosRecomendacionFeedDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new RecomendacionPublicacionDto { PublicacionId = 7, PuntajeColaborativo = 2 },
                    new RecomendacionPublicacionDto { PublicacionId = 3, PuntajeColaborativo = 1 }
                });

            _catalogoPublicaciones.AddRange(new[]
            {
                new PublicacionDetalleDto { Id = 7 },
                new PublicacionDetalleDto { Id = 3 }
            });

            //Act
            var result = await _servicio.ObtenerPublicacionesParaVos(10, null, 1);

            //Assert
            Assert.Equal(7, Assert.Single(result.Items).Id);
            Assert.NotNull(result.SiguienteCursor);
            var sesion = Assert.Single(_sesionesCreadas);
            Assert.Equal(10, sesion.UsuarioId);
            Assert.Equal(new[] { 7, 3 }, sesion.PublicacionesIds);
        }

        [Fact]
        public async Task QueSeContinueLaSesionSinVolverAGenerarRecomendaciones()
        {
            //Arrange
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerRecomendacionesFeed(10, 500, It.IsAny<long>(), It.IsAny<PesosRecomendacionFeedDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new RecomendacionPublicacionDto { PublicacionId = 7, PuntajeColaborativo = 3 },
                    new RecomendacionPublicacionDto { PublicacionId = 3, PuntajeColaborativo = 2 },
                    new RecomendacionPublicacionDto { PublicacionId = 9, PuntajeColaborativo = 1 }
                });

            _catalogoPublicaciones.AddRange(new[]
            {
                new PublicacionDetalleDto { Id = 7 },
                new PublicacionDetalleDto { Id = 3 },
                new PublicacionDetalleDto { Id = 9 }
            });

            var primeraPagina = await _servicio.ObtenerPublicacionesParaVos(10, null, 1);

            //Act
            var result = await _servicio.ObtenerPublicacionesParaVos(10, primeraPagina.SiguienteCursor, 1);

            //Assert
            Assert.Equal(3, Assert.Single(result.Items).Id);
            _publicacionRepositorioMock.Verify(r => r.ObtenerRecomendacionesFeed(10, 500, It.IsAny<long>(), It.IsAny<PesosRecomendacionFeedDto>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Single(_sesionesCreadas);
        }

        [Fact]
        public async Task QueSeGenereUnaNuevaSesionAlRecargarSinCursor()
        {
            //Arrange
            _publicacionRepositorioMock
                .SetupSequence(r => r.ObtenerRecomendacionesFeed(10, 500, It.IsAny<long>(), It.IsAny<PesosRecomendacionFeedDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new RecomendacionPublicacionDto { PublicacionId = 7, PuntajeColaborativo = 2 },
                    new RecomendacionPublicacionDto { PublicacionId = 3, PuntajeColaborativo = 1 }
                })
                .ReturnsAsync(new[]
                {
                    new RecomendacionPublicacionDto { PublicacionId = 3, PuntajeColaborativo = 2 },
                    new RecomendacionPublicacionDto { PublicacionId = 7, PuntajeColaborativo = 1 }
                });

            _catalogoPublicaciones.AddRange(new[]
            {
                new PublicacionDetalleDto { Id = 7 },
                new PublicacionDetalleDto { Id = 3 }
            });

            await _servicio.ObtenerPublicacionesParaVos(10, null, 1);

            //Act
            var result = await _servicio.ObtenerPublicacionesParaVos(10, null, 1);

            //Assert
            _publicacionRepositorioMock.Verify(r => r.ObtenerRecomendacionesFeed(10, 500, It.IsAny<long>(), It.IsAny<PesosRecomendacionFeedDto>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.Equal(2, _sesionesCreadas.Count);
            Assert.NotEqual(_sesionesCreadas[0].Id, _sesionesCreadas[1].Id);
            Assert.Equal(3, Assert.Single(result.Items).Id);
        }

        [Fact]
        public async Task QueNoSeContinueUnaSesionConUnaPosicionFueraDeRango()
        {
            //Arrange
            var sesion = _sesionFypRepositorioMock.Object.Crear(10, new[] { 7, 3 });
            var cursorDto = new CursorFypDto { SesionId = sesion.Id, Posicion = 3 };
            var cursor = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(cursorDto))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            //Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _servicio.ObtenerPublicacionesParaVos(10, cursor, 20));
        }

        [Fact]
        public async Task QueLasPublicacionesRespetenElOrdenDeLaSesion()
        {
            //Arrange
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerRecomendacionesFeed(10, 500, It.IsAny<long>(), It.IsAny<PesosRecomendacionFeedDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new RecomendacionPublicacionDto { PublicacionId = 9, PuntajeColaborativo = 3 },
                    new RecomendacionPublicacionDto { PublicacionId = 3, PuntajeColaborativo = 2 },
                    new RecomendacionPublicacionDto { PublicacionId = 7, PuntajeColaborativo = 1 }
                });

            _catalogoPublicaciones.AddRange(new[]
            {
                new PublicacionDetalleDto { Id = 3 },
                new PublicacionDetalleDto { Id = 7 },
                new PublicacionDetalleDto { Id = 9 }
            });

            //Act
            var result = await _servicio.ObtenerPublicacionesParaVos(10, null, 20);

            //Assert
            Assert.Equal(new[] { 9, 3, 7 }, result.Items.Select(p => p.Id));
        }

        [Fact]
        public async Task QueSeCompleteLaPaginaOmitiendoPublicacionesNoDisponibles()
        {
            //Arrange
            var recomendaciones = Enumerable.Range(1, 105)
                .Select((id, index) => new RecomendacionPublicacionDto { PublicacionId = id, PuntajeColaborativo = 105 - index })
                .ToArray();

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerRecomendacionesFeed(10, 500, It.IsAny<long>(), It.IsAny<PesosRecomendacionFeedDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(recomendaciones);

            _catalogoPublicaciones.AddRange(new[]
            {
                new PublicacionDetalleDto { Id = 105 },
                new PublicacionDetalleDto { Id = 51 },
                new PublicacionDetalleDto { Id = 2 }
            });

            //Act
            var result = await _servicio.ObtenerPublicacionesParaVos(10, null, 2);
            var segundaPagina = await _servicio.ObtenerPublicacionesParaVos(10, result.SiguienteCursor, 2);

            //Assert
            Assert.Equal(new[] { 2, 51 }, result.Items.Select(p => p.Id));
            Assert.NotNull(result.SiguienteCursor);
            Assert.Equal(105, Assert.Single(segundaPagina.Items).Id);
        }

        [Fact]
        public async Task QueSinRecomendacionesDevuelvaGeneralesPorFechaYConserveLaPaginacion()
        {
            //Arrange
            var fecha = DateTime.UtcNow.AddDays(-1);
            _catalogoPublicaciones.AddRange(new[]
            {
                new PublicacionDetalleDto { Id = 8, FechaCreacion = fecha.AddDays(-1) },
                new PublicacionDetalleDto { Id = 2, FechaCreacion = fecha },
                new PublicacionDetalleDto { Id = 3, FechaCreacion = fecha }
            });

            //Act
            var primera = await _servicio.ObtenerPublicacionesParaVos(10, null, 2);
            var segunda = await _servicio.ObtenerPublicacionesParaVos(10, primera.SiguienteCursor, 2);

            //Assert
            Assert.Equal(new[] { 3, 2 }, primera.Items.Select(p => p.Id));
            Assert.NotNull(primera.SiguienteCursor);
            Assert.Equal(8, Assert.Single(segunda.Items).Id);
            Assert.Null(segunda.SiguienteCursor);
        }

        [Fact]
        public async Task QueSinRecomendacionesNiPublicacionesDevuelvaPaginaVacia()
        {
            //Arrange & Act
            var resultado = await _servicio.ObtenerPublicacionesParaVos(10, null, 20);

            //Assert
            Assert.Empty(resultado.Items);
            Assert.Null(resultado.SiguienteCursor);
        }

        [Fact]
        public async Task QueUnaPuntuacionAltaAporteAfinidadDeCategoriaSinLimitarLaDecada()
        {
            //Arrange
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerInteraccionesFeed(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new InteraccionFeedDto { PublicacionId = 1, MiPuntuacion = 5 }
                });

            _catalogoPublicaciones.AddRange(new[]
            {
                PublicacionFeed(1, CategoriaEnum.Cine, 1982),
                PublicacionFeed(2, CategoriaEnum.Cine, 2005),
                PublicacionFeed(3, CategoriaEnum.Música, 2005)
            });

            //Act
            var resultado = await _servicio.ObtenerPublicacionesParaVos(10, null, 20);

            //Assert
            Assert.Equal(new[] { 2, 3 }, resultado.Items.Select(p => p.Id));
        }

        [Fact]
        public async Task QueUnLikeFavorezcaPublicacionesCercanasAlLugarDeLaSemilla()
        {
            //Arrange
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerInteraccionesFeed(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new InteraccionFeedDto { PublicacionId = 1, DioLike = true }
                });

            _catalogoPublicaciones.AddRange(new[]
            {
                PublicacionFeed(1, CategoriaEnum.Lugares, 1985, -34.60, -58.38),
                PublicacionFeed(2, CategoriaEnum.Cine, 2000, -34.61, -58.38),
                PublicacionFeed(3, CategoriaEnum.Cine, 2000, -41.13, -71.31)
            });

            //Act
            var resultado = await _servicio.ObtenerPublicacionesParaVos(10, null, 20);

            //Assert
            Assert.Equal(new[] { 2, 3 }, resultado.Items.Select(p => p.Id));
        }

        [Fact]
        public async Task QueElPuntajeColaborativoSeSumeAlRanking()
        {
            //Arrange
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerRecomendacionesFeed(10, 500, It.IsAny<long>(), It.IsAny<PesosRecomendacionFeedDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new RecomendacionPublicacionDto { PublicacionId = 2, PuntajeColaborativo = 10 },
                    new RecomendacionPublicacionDto { PublicacionId = 3, PuntajeColaborativo = 1 }
                });

            _catalogoPublicaciones.AddRange(new[]
            {
                PublicacionFeed(2, CategoriaEnum.Cine, 2000),
                PublicacionFeed(3, CategoriaEnum.Cine, 2000)
            });

            //Act
            var resultado = await _servicio.ObtenerPublicacionesParaVos(10, null, 20);

            //Assert
            Assert.Equal(new[] { 2, 3 }, resultado.Items.Select(p => p.Id));
        }

        [Fact]
        public async Task QueElRankingEnvieAlRepositorioLosPesosConfigurados()
        {
            //Arrange
            var opciones = new FeedRecomendacionesOptions
            {
                PesoLike = 5,
                PesoPuntuacion = 7,
                PesoEtiqueta = 2
            };

            var servicio = new PublicacionServicio(
                _publicacionRepositorioMock.Object,
                _usuarioRepositorioMock.Object,
                _multimediaMock.Object,
                _sesionFypRepositorioMock.Object,
                _seguimientoMock.Object,
                Options.Create(opciones),
                _notificacionMock.Object,
                _logroMock.Object);

            PesosRecomendacionFeedDto? pesosEnviados = null;
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerRecomendacionesFeed(10, 500, It.IsAny<long>(), It.IsAny<PesosRecomendacionFeedDto>(), It.IsAny<CancellationToken>()))
                .Callback<int, int, long, PesosRecomendacionFeedDto, CancellationToken>((_, _, _, pesos, _) => pesosEnviados = pesos)
                .ReturnsAsync(Array.Empty<RecomendacionPublicacionDto>());

            //Act
            await servicio.ObtenerPublicacionesParaVos(10, null, 20);

            //Assert
            Assert.NotNull(pesosEnviados);
            Assert.Equal(new PesosRecomendacionFeedDto(5, 7, 2), pesosEnviados);
        }

        [Fact]
        public async Task QueElFeedExcluyaLikesYPuntuacionesInclusoSiSonRecomendados()
        {
            //Arrange
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerInteraccionesFeed(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new InteraccionFeedDto { PublicacionId = 1, DioLike = true },
                    new InteraccionFeedDto { PublicacionId = 2, MiPuntuacion = 1 }
                });

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerRecomendacionesFeed(10, 500, It.IsAny<long>(), It.IsAny<PesosRecomendacionFeedDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new RecomendacionPublicacionDto { PublicacionId = 1, PuntajeColaborativo = 3 },
                    new RecomendacionPublicacionDto { PublicacionId = 2, PuntajeColaborativo = 2 },
                    new RecomendacionPublicacionDto { PublicacionId = 3, PuntajeColaborativo = 1 }
                });

            _catalogoPublicaciones.AddRange(new[]
            {
                PublicacionFeed(1, CategoriaEnum.Cine, 2000),
                PublicacionFeed(2, CategoriaEnum.Cine, 2000),
                PublicacionFeed(3, CategoriaEnum.Cine, 2000)
            });

            //Act
            var resultado = await _servicio.ObtenerPublicacionesParaVos(10, null, 20);

            //Assert
            Assert.Equal(3, Assert.Single(resultado.Items).Id);
        }

        [Fact]
        public async Task QueElFallbackPrioriceNuncaVistasLuegoVistasAntiguasYLuegoRecientes()
        {
            //Arrange
            var ahora = DateTimeOffset.UtcNow;
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerInteraccionesFeed(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new InteraccionFeedDto { PublicacionId = 2, UltimaImpresionUnixMs = ahora.AddDays(-15).ToUnixTimeMilliseconds() },
                    new InteraccionFeedDto { PublicacionId = 3, UltimaImpresionUnixMs = ahora.AddDays(-1).ToUnixTimeMilliseconds() }
                });

            _catalogoPublicaciones.AddRange(new[]
            {
                PublicacionFeed(1, CategoriaEnum.Cine, 2000),
                PublicacionFeed(2, CategoriaEnum.Cine, 2000),
                PublicacionFeed(3, CategoriaEnum.Cine, 2000)
            });

            //Act
            var resultado = await _servicio.ObtenerPublicacionesParaVos(10, null, 20);

            //Assert
            Assert.Equal(new[] { 1, 2, 3 }, resultado.Items.Select(p => p.Id));
        }

        [Fact]
        public async Task QueUnaVistaRecienteCompleteLaPaginaSiNoHayContenidoNuevo()
        {
            //Arrange
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerInteraccionesFeed(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new InteraccionFeedDto
                    {
                        PublicacionId = 2,
                        UltimaImpresionUnixMs = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds()
                    }
                });

            _catalogoPublicaciones.AddRange(new[]
            {
                PublicacionFeed(1, CategoriaEnum.Cine, 2000),
                PublicacionFeed(2, CategoriaEnum.Cine, 2000)
            });

            //Act
            var resultado = await _servicio.ObtenerPublicacionesParaVos(10, null, 2);

            //Assert
            Assert.Equal(new[] { 1, 2 }, resultado.Items.Select(p => p.Id));
        }

        [Fact]
        public async Task QueElCorteDeVistasRecientesSeEnvieANeo4jConCatorceDias()
        {
            //Arrange
            var antes = DateTimeOffset.UtcNow.AddDays(-14).ToUnixTimeMilliseconds();
            long vistasDesdeEnviadas = 0;

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerRecomendacionesFeed(10, 500, It.IsAny<long>(), It.IsAny<PesosRecomendacionFeedDto>(), It.IsAny<CancellationToken>()))
                .Callback<int, int, long, PesosRecomendacionFeedDto, CancellationToken>((_, _, vistas, _, _) => vistasDesdeEnviadas = vistas)
                .ReturnsAsync(Array.Empty<RecomendacionPublicacionDto>());

            //Act
            await _servicio.ObtenerPublicacionesParaVos(10, null, 20);

            //Assert
            var despues = DateTimeOffset.UtcNow.AddDays(-14).ToUnixTimeMilliseconds();
            Assert.InRange(vistasDesdeEnviadas, antes, despues);
        }

        #endregion

        #region Registro de Impresiones

        [Fact]
        public async Task QueRegistrarImpresionesElimineDuplicadosYDescartePublicacionesNoVisibles()
        {
            //Arrange
            ConfigurarUsuarioExiste(10);
            _catalogoPublicaciones.Add(PublicacionFeed(2, CategoriaEnum.Cine, 2000));

            //Act
            await _servicio.RegistrarImpresionesFeed(10, new[] { 2, 2, 99 });

            //Assert
            _publicacionRepositorioMock.Verify(r => r.RegistrarImpresionesFeed(
                10,
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(2)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(0, new[] { 1 })]
        [InlineData(10, new int[] { })]
        [InlineData(10, new[] { 0 })]
        public async Task QueRegistrarImpresionesRechaceDatosInvalidos(int usuarioId, int[] ids)
        {
            //Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _servicio.RegistrarImpresionesFeed(usuarioId, ids));

            _publicacionRepositorioMock.Verify(r => r.RegistrarImpresionesFeed(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task QueRegistrarImpresionesRechaceUsuarioInexistente()
        {
            //Arrange
            ConfigurarUsuarioExiste(10, existe: false);

            //Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _servicio.RegistrarImpresionesFeed(10, new[] { 1 }));

            _publicacionRepositorioMock.Verify(r => r.RegistrarImpresionesFeed(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task QueRegistrarImpresionesRechaceMasDeCincuentaIds()
        {
            //Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _servicio.RegistrarImpresionesFeed(10, Enumerable.Range(1, 51).ToArray()));

            _publicacionRepositorioMock.Verify(r => r.RegistrarImpresionesFeed(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task QueRegistrarImpresionesNoEscribeSiNingunaPublicacionEsVisible()
        {
            //Arrange
            ConfigurarUsuarioExiste(10);

            //Act
            await _servicio.RegistrarImpresionesFeed(10, new[] { 99 });

            //Assert
            _publicacionRepositorioMock.Verify(r => r.RegistrarImpresionesFeed(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Puntuaciones

        [Theory]
        [InlineData(0)]
        [InlineData(6)]
        [InlineData(-1)]
        public async Task QueFalleAlPuntuarSiPuntajeEstaFueraDeRango(int puntajeInvalido)
        {
            //Arrange
            ConfigurarUsuarioExiste(10);
            ConfigurarPublicacionVisible(1);

            //Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                _servicio.Puntuar(1, 10, puntajeInvalido));
        }

        [Fact]
        public async Task QueFalleAlPuntuarSiUsuarioPuntuaSuPropiaPublicacion()
        {
            //Arrange
            ConfigurarUsuarioExiste(10);
            ConfigurarPublicacionVisible(1);
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerPorId(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Publicacion { Id = 1, UsuarioId = 10 }); // Mismo usuario autor

            //Act
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _servicio.Puntuar(1, 10, 5));

            //Assert
            Assert.Contains("propia publicación", ex.Message);
        }

        [Fact]
        public async Task QueSeRegistrePuntajeExitosamente()
        {
            //Arrange
            ConfigurarUsuarioExiste(10);
            ConfigurarPublicacionVisible(1);
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerPorId(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Publicacion { Id = 1, UsuarioId = 99 });

            //Act
            var resultado = await _servicio.Puntuar(1, 10, 5);

            //Assert
            Assert.Equal(5, resultado.PuntosTotales);
            Assert.Equal(5, resultado.MiPuntuacion);
            _publicacionRepositorioMock.Verify(r => r.PuntuarPublicacion(1, 10, 5, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QueHagaToggleSiSeEnviaElMismoPuntaje()
        {
            //Arrange
            ConfigurarUsuarioExiste(10);
            ConfigurarPublicacionVisible(1);
            _ultimoPuntajeGuardado = 4; // Ya tenía puntaje 4
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerPorId(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Publicacion { Id = 1, UsuarioId = 99 });

            //Act
            var resultado = await _servicio.Puntuar(1, 10, 4);

            //Assert
            _publicacionRepositorioMock.Verify(r => r.QuitarPuntuacion(1, 10, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Reposts y Citas

        [Fact]
        public async Task QueSeValideExistenciaYSeGuardeEnNeo4jAlRepostear()
        {
            //Arrange
            ConfigurarUsuarioExiste(20);
            ConfigurarPublicacionVisible(10);
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerPorId(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Publicacion { Id = 10, UsuarioId = 5 });

            _publicacionRepositorioMock
                .Setup(r => r.CrearRepost(10, 20, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            //Act
            var resultado = await _servicio.Repostear(10, 20);

            //Assert
            Assert.Equal(10, resultado.PublicacionId);
            Assert.True(resultado.DioRepost);
            _publicacionRepositorioMock.Verify(r => r.CrearRepost(10, 20, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QueFalleAlRepostearSiUsuarioNoExiste()
        {
            //Arrange
            ConfigurarUsuarioExiste(20, existe: false);
            ConfigurarPublicacionVisible(10);
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerPorId(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Publicacion { Id = 10, UsuarioId = 5 });

            //Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.Repostear(10, 20));
        }

        [Fact]
        public async Task QueFalleAlRepostearSiPublicacionNoExiste()
        {
            //Arrange
            ConfigurarUsuarioExiste(20);
            ConfigurarPublicacionVisible(10, visible: false);

            //Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.Repostear(10, 20));
        }

        [Fact]
        public async Task QueSeElimineEnNeo4jAlQuitarRepost()
        {
            //Arrange
            ConfigurarUsuarioExiste(20);
            _publicacionRepositorioMock
                .Setup(r => r.QuitarRepost(10, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            //Act
            var resultado = await _servicio.QuitarRepost(10, 20);

            //Assert
            Assert.Equal(10, resultado.PublicacionId);
            _publicacionRepositorioMock.Verify(r => r.QuitarRepost(10, 20, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QueSeValideExistenciaYSeGuardeEnPostgresAlCitar()
        {
            //Arrange
            ConfigurarUsuarioExiste(20);
            ConfigurarPublicacionVisible(10);
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerPorId(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Publicacion
                {
                    Id = 10,
                    Titulo = "Original",
                    UsuarioId = 5,
                    Categoria = CategoriaEnum.Lugares,
                    LugarId = 2,
                    UrlMultimedia = "https://example.com/foto.jpg",
                    TipoMultimedia = MultimediaEnum.Foto
                });

            Publicacion? citaCreada = null;
            _publicacionRepositorioMock
                .Setup(r => r.Crear(It.IsAny<Publicacion>()))
                .Callback<Publicacion>(p => citaCreada = p)
                .ReturnsAsync(44);

            //Act
            var idCita = await _servicio.Citar(10, 20, "Mi cita genial");

            //Assert
            Assert.Equal(44, idCita);
            Assert.NotNull(citaCreada);
            Assert.Equal(10, citaCreada.PublicacionOriginalId);
            Assert.Equal(20, citaCreada.UsuarioId);
            Assert.Equal("Mi cita genial", citaCreada.Descripcion);
            Assert.Equal("Original", citaCreada.Titulo);
        }

        [Fact]
        public async Task QueFalleAlCitarSiDescripcionEstaVacia()
        {
            //Arrange
            ConfigurarUsuarioExiste(20);
            ConfigurarPublicacionVisible(10);
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerPorId(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Publicacion { Id = 10, UsuarioId = 5 });

            //Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Citar(10, 20, "   "));
        }

        [Fact]
        public async Task QueObtenerPorUsuarioIdIncluyaPublicacionesPropiasYRepostsSimples()
        {
            //Arrange
            var fechaPropia = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var fechaRepost = new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc);

            var propia = new PublicacionDetalleDto
            {
                Id = 1,
                Titulo = "Mi publicacion",
                FechaCreacion = fechaPropia,
                Autor = new AutorResumenDto { Id = 5, Username = "usuario5" }
            };

            var ajena = new PublicacionDetalleDto
            {
                Id = 2,
                Titulo = "Publicacion ajena",
                FechaCreacion = new DateTime(2025, 12, 1, 12, 0, 0, DateTimeKind.Utc),
                Autor = new AutorResumenDto { Id = 99, Username = "otro" }
            };

            _catalogoPublicaciones.AddRange(new[] { propia, ajena });
            _publicacionRepositorioMock
                .Setup(r => r.ObtenerPorUsuarioId(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PublicacionDetalleDto> { propia });

            _repostsSeguidos.Add((2, 5, "usuario5", fechaRepost));

            //Act
            var resultado = await _servicio.ObtenerPorUsuarioId(5, null);

            //Assert
            Assert.Equal(2, resultado.Count);
            // El primer elemento debe ser el repost ya que tiene fecha más reciente
            Assert.Equal(2, resultado[0].Id);
            Assert.NotNull(resultado[0].ReposteadoPor);
            Assert.Equal(5, resultado[0].ReposteadoPor!.UsuarioId);
            Assert.Equal("usuario5", resultado[0].ReposteadoPor!.Username);
            Assert.Equal(fechaRepost, resultado[0].FechaCreacion);

            // El segundo elemento es la publicación propia
            Assert.Equal(1, resultado[1].Id);
            Assert.Null(resultado[1].ReposteadoPor);
            Assert.Equal(fechaPropia, resultado[1].FechaCreacion);
        }

        [Fact]
        public async Task QueObtenerFeedSeguidosResuelvaUsernameDeReposteadorCuandoNeo4jTieneUsernameVacio()
        {
            //Arrange
            var fechaRepost = DateTime.UtcNow;
            _seguimientoMock
                .Setup(s => s.ObtenerSeguidosIdsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<int> { 12 });

            var original = new PublicacionDetalleDto
            {
                Id = 50,
                Titulo = "Pub original",
                FechaCreacion = fechaRepost.AddDays(-1),
                Autor = new AutorResumenDto { Id = 99, Username = "autor_original" }
            };

            _catalogoPublicaciones.Add(original);

            _publicacionRepositorioMock
                .Setup(r => r.ObtenerFeedSeguidos(It.IsAny<IReadOnlyCollection<int>>(), null, 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PublicacionDetalleDto>());

            _repostsSeguidos.Add((50, 12, "", fechaRepost));

            _usuarioRepositorioMock
                .Setup(u => u.ObtenerUsuariosPorIdsAsync(It.Is<IReadOnlyCollection<int>>(ids => ids.Contains(12)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AutorResumenDto> { new() { Id = 12, Username = "usuario12" } });

            //Act
            var resultado = await _servicio.ObtenerFeedSeguidos(1, null, 10);

            //Assert
            Assert.Single(resultado.Items);
            var item = resultado.Items[0];
            Assert.Equal(50, item.Id);
            Assert.NotNull(item.ReposteadoPor);
            Assert.Equal(12, item.ReposteadoPor!.UsuarioId);
            Assert.Equal("usuario12", item.ReposteadoPor!.Username);
        }

        #endregion
    }
}
