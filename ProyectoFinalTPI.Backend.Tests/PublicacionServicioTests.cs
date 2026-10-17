using System.Text.Json;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
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
        private readonly PublicacionRepositorioFake _repositorio = new();
        private readonly UsuarioRepositorioFake _usuarios = new();
        private readonly MultimediaServicioFake _multimedia = new();
        private readonly SesionFypRepositorioFake _sesiones = new();
        private readonly SeguimientoServicioFake _seguimiento = new();
        private readonly PublicacionServicio _servicio;

        public PublicacionServicioTests()
        {
            _servicio = new PublicacionServicio(
                _repositorio, _usuarios, _multimedia, _sesiones, _seguimiento);
        }

        [Fact]
        public async Task QueSePuedaCrearUnaPublicacionConUnAutorExistente()
        {
            //Arrange
            _usuarios.Existe = true;
            _repositorio.IdCreado = 25;
            var dto = new CrearPublicacionDto
            {
                AutorId = 10,
                Titulo = "Recuerdo del Obelisco",
                Descripcion = "Una visita a Buenos Aires",
                NombreLugar = "Obelisco",
                Latitud = -34.6037,
                Longitud = -58.3816,
                Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Categoria = CategoriaEnum.Lugares,
                TipoMultimedia = MultimediaEnum.Foto,
                UrlArchivo = "https://example.com/original.jpg"
            };

            //Act
            var result = await _servicio.Crear(dto);

            //Assert
            Assert.Equal(25, result);
            Assert.NotNull(_repositorio.Creada);
            Assert.Equal(dto.Titulo, _repositorio.Creada.Titulo);
            Assert.Equal("Obelisco", _repositorio.Creada.Lugar.Nombre);
            Assert.Equal(10, _repositorio.Creada.UsuarioId);
            Assert.Equal(_multimedia.Url, _repositorio.Creada.UrlMultimedia);
            Assert.Equal(dto.UrlArchivo, _multimedia.UrlExterna);
            Assert.Equal(10, _usuarios.IdConsultado);
        }

        [Fact]
        public async Task QueNoSeCreeUnaPublicacionSiElAutorNoExiste()
        {
            //Arrange
            _usuarios.Existe = false;
            var dto = new CrearPublicacionDto { AutorId = 99 };

            //Act
            var result = await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.Crear(dto));

            //Assert
            Assert.Contains("usuario", result.Message);
            Assert.Equal(0, _multimedia.Llamadas);
            Assert.Null(_repositorio.Creada);
        }

        [Fact]
        public async Task QueNoSeGuardeUnaPublicacionSiFallaLaSubidaMultimedia()
        {
            //Arrange
            _usuarios.Existe = true;
            var error = new InvalidOperationException("Error al subir la imagen");
            _multimedia.Error = error;
            var dto = new CrearPublicacionDto { AutorId = 10 };

            //Act
            var result = await Assert.ThrowsAsync<InvalidOperationException>(() => _servicio.Crear(dto));

            //Assert
            Assert.Same(error, result);
            Assert.Equal(1, _multimedia.Llamadas);
            Assert.Null(_repositorio.Creada);
        }

        [Fact]
        public async Task QueSeCreeElLugarConLasCoordenadasCorrectas()
        {
            //Arrange
            _usuarios.Existe = true;
            var dto = new CrearPublicacionDto
            {
                AutorId = 10, NombreLugar = "Obelisco", Latitud = -34.6037, Longitud = -58.3816
            };

            //Act
            await _servicio.Crear(dto);

            //Assert
            Assert.NotNull(_repositorio.Creada);
            Assert.Equal(-58.3816, _repositorio.Creada.Lugar.Coordenadas.X);
            Assert.Equal(-34.6037, _repositorio.Creada.Lugar.Coordenadas.Y);
            Assert.Equal(4326, _repositorio.Creada.Lugar.Coordenadas.SRID);
        }

        [Fact]
        public async Task QueSePuedaCompartirUnaPublicacionConservandoLaReferenciaOriginal()
        {
            //Arrange
            _usuarios.Existe = true;
            _repositorio.IdCreado = 30;
            _repositorio.Original = new Publicacion
            {
                Id = 5, Titulo = "Recuerdo original", LugarId = 7,
                UsuarioId = 20, UrlMultimedia = "https://example.com/foto.jpg"
            };
            var dto = new CompartirPublicacionDto
            {
                AutorId = 10, PublicacionOriginalId = 5, Descripcion = "Mi recuerdo compartido"
            };

            //Act
            var result = await _servicio.Compartir(dto);

            //Assert
            Assert.Equal(30, result);
            Assert.NotNull(_repositorio.Creada);
            Assert.Equal(5, _repositorio.Creada.PublicacionOriginalId);
            Assert.Equal("Recuerdo original", _repositorio.Creada.Titulo);
            Assert.Equal(7, _repositorio.Creada.LugarId);
            Assert.Equal(10, _repositorio.Creada.UsuarioId);
            Assert.Equal(dto.Descripcion, _repositorio.Creada.Descripcion);
            Assert.Equal(5, _repositorio.IdOriginalConsultado);
            Assert.Equal(0, _multimedia.Llamadas);
        }

        [Fact]
        public async Task QueNoSeCompartaUnaPublicacionSiElAutorNoExiste()
        {
            //Arrange
            _usuarios.Existe = false;
            var dto = new CompartirPublicacionDto { AutorId = 99, PublicacionOriginalId = 5 };

            //Act
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.Compartir(dto));

            //Assert
            Assert.Null(_repositorio.IdOriginalConsultado);
            Assert.Null(_repositorio.Creada);
        }

        [Fact]
        public async Task QueNoSeCompartaUnaPublicacionSiLaOriginalNoEstaDisponible()
        {
            //Arrange
            _usuarios.Existe = true;
            _repositorio.Original = null;
            var dto = new CompartirPublicacionDto { AutorId = 10, PublicacionOriginalId = 99 };

            //Act
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.Compartir(dto));

            //Assert
            Assert.Equal(99, _repositorio.IdOriginalConsultado);
            Assert.Null(_repositorio.Creada);
            Assert.Equal(0, _multimedia.Llamadas);
        }

        [Fact]
        public async Task QueSeElimineUnaPublicacionCorrectamente()
        {
            _repositorio.EliminarResultado = true;
            await _servicio.Eliminar(5, 10);
            Assert.Equal(5, _repositorio.IdEliminado);
            Assert.Equal(10, _repositorio.AutorEliminado);
        }

        [Fact]
        public async Task QueFalleAlEliminarSiElIdEsInvalido()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _servicio.Eliminar(0));
        }

        [Fact]
        public async Task QueFalleAlEliminarSiLaPublicacionNoExiste()
        {
            _repositorio.EliminarResultado = false;
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.Eliminar(99));
        }

        [Fact]
        public async Task QueSeElimineUnRepostCorrectamente()
        {
            _repositorio.EliminarResultado = true;
            await _servicio.EliminarRepost(5, 10);
            Assert.Equal(5, _repositorio.IdEliminado);
            Assert.Equal(10, _repositorio.AutorEliminado);
        }

        [Fact]
        public async Task QueFalleAlEliminarRepostSiElIdEsInvalido()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _servicio.EliminarRepost(0));
        }

        [Fact]
        public async Task QueFalleAlEliminarRepostSiNoExiste()
        {
            _repositorio.EliminarResultado = false;
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _servicio.EliminarRepost(99));
        }

        [Fact]
        public async Task QueNoSeObtenganPinesConUnFiltroNulo()
        {
            //Arrange
            PublicacionFiltroDto filtro = null!;

            //Act
            await Assert.ThrowsAsync<ArgumentNullException>(() => _servicio.ObtenerPines(filtro));

            //Assert
            Assert.Null(_repositorio.FiltroConsultado);
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

            //Act
            await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ObtenerPines(filtro));

            //Assert
            Assert.Null(_repositorio.FiltroConsultado);
        }

        [Fact]
        public async Task QueSeDevuelvanDoscientosPinesYSeIndiqueQueHayMas()
        {
            //Arrange
            var filtro = new PublicacionFiltroDto
            {
                MinLatitud = -35, MaxLatitud = -34, MinLongitud = -59, MaxLongitud = -58
            };
            _repositorio.Pines = Enumerable.Range(1, 201)
                .Select(id => new PublicacionPinDto { Id = id }).ToList();

            //Act
            var result = await _servicio.ObtenerPines(filtro);

            //Assert
            Assert.Equal(200, result.Pines.Count);
            Assert.True(result.HayMas);
            Assert.Equal(200, result.Pines[^1].Id);
            Assert.Same(filtro, _repositorio.FiltroConsultado);
            Assert.Equal(200, _repositorio.LimitePines);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(200)]
        public async Task QueNoSeIndiqueQueHayMasSiLosPinesNoSuperanElLimite(int cantidad)
        {
            //Arrange
            var filtro = new PublicacionFiltroDto
            {
                MinLatitud = -35, MaxLatitud = -34, MinLongitud = -59, MaxLongitud = -58
            };
            _repositorio.Pines = Enumerable.Range(1, cantidad)
                .Select(id => new PublicacionPinDto { Id = id }).ToList();

            //Act
            var result = await _servicio.ObtenerPines(filtro);

            //Assert
            Assert.Equal(cantidad, result.Pines.Count);
            Assert.False(result.HayMas);
        }

        [Fact]
        public async Task QueSePuedaObtenerElResumenDeUnaPublicacion()
        {
            //Arrange
            _repositorio.Resumen = new PublicacionResumenDto { Id = 5, Titulo = "Recuerdo" };

            //Act
            var result = await _servicio.ObtenerResumenPorId(5);

            //Assert
            Assert.Equal("Recuerdo", result.Titulo);
            Assert.Same(_repositorio.Resumen, result);
            Assert.Equal(5, _repositorio.IdResumenConsultado);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task QueNoSeObtengaElResumenConUnIdInvalido(int id)
        {
            //Arrange
            _repositorio.Resumen = new PublicacionResumenDto { Titulo = "Recuerdo" };

            //Act
            await Assert.ThrowsAsync<ArgumentException>(() => _servicio.ObtenerResumenPorId(id));

            //Assert
            Assert.Null(_repositorio.IdResumenConsultado);
        }

        [Fact]
        public async Task QueSePuedaObtenerElDetalleDeUnaPublicacion()
        {
            //Arrange
            _repositorio.Detalle = new PublicacionDetalleDto { Id = 5, Titulo = "Recuerdo completo" };

            //Act
            var result = await _servicio.ObtenerDetallePorId(5);

            //Assert
            Assert.Equal("Recuerdo completo", result.Titulo);
            Assert.Same(_repositorio.Detalle, result);
            Assert.Equal(5, _repositorio.IdDetalleConsultado);
        }

        [Fact]
        public async Task QueSeDevuelvaUnFeedVacioSiElUsuarioNoSigueANadie()
        {
            //Arrange
            _seguimiento.Seguidos = Array.Empty<int>();

            //Act
            var result = await _servicio.ObtenerFeedSeguidos(10, null, 20);

            //Assert
            Assert.Empty(result.Items);
            Assert.Null(result.SiguienteCursor);
            Assert.Equal(0, _repositorio.ConsultasFeed);
        }

        [Fact]
        public async Task QueSeConsultenLasPublicacionesDeLosAutoresDevueltosPorSeguimiento()
        {
            //Arrange
            _seguimiento.Seguidos = new[] { 20, 30 };
            _repositorio.Feed = new List<PublicacionDetalleDto>
            {
                new() { Id = 1 }, new() { Id = 2 }, new() { Id = 3 }, new() { Id = 4 }
            };

            //Act
            var result = await _servicio.ObtenerFeedSeguidos(10, null, 20);

            //Assert
            Assert.Equal(10, _seguimiento.UsuarioConsultado);
            Assert.Equal(new[] { 20, 30 }, _repositorio.AutoresConsultados);
            Assert.Equal(new[] { 1, 2, 3, 4 }, result.Items.Select(p => p.Id));
            Assert.Equal(20, _repositorio.LimiteFeed);
        }

        [Fact]
        public async Task QueNoSeConsultenPublicacionesSiFallaLaConsultaDeSeguidos()
        {
            //Arrange
            var error = new InvalidOperationException("No se pudieron consultar los seguidos");
            _seguimiento.Error = error;

            //Act
            var result = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _servicio.ObtenerFeedSeguidos(10, null, 20));

            //Assert
            Assert.Same(error, result);
            Assert.Equal(0, _repositorio.ConsultasFeed);
        }

        [Fact]
        public async Task QueSePagineElFeedSeguidosUsandoLaUltimaPublicacionEntregada()
        {
            //Arrange
            _seguimiento.Seguidos = new[] { 20 };
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            _repositorio.Feed = new List<PublicacionDetalleDto>
            {
                new() { Id = 12, FechaCreacion = fecha },
                new() { Id = 11, FechaCreacion = fecha },
                new() { Id = 10, FechaCreacion = fecha.AddMinutes(-1) }
            };

            //Act
            var result = await _servicio.ObtenerFeedSeguidos(10, null, 2);
            await _servicio.ObtenerFeedSeguidos(10, result.SiguienteCursor, 2);

            //Assert
            Assert.Equal(new[] { 12, 11 }, result.Items.Select(p => p.Id));
            Assert.NotNull(result.SiguienteCursor);
            Assert.Equal(11, _repositorio.CursorFeed!.Id);
            Assert.Equal(fecha, _repositorio.CursorFeed.FechaCreacion);
        }

        [Fact]
        public async Task QueLaUltimaPaginaDelFeedSeguidosNoTengaSiguienteCursor()
        {
            //Arrange
            _seguimiento.Seguidos = new[] { 20 };
            _repositorio.Feed = new List<PublicacionDetalleDto>
            {
                new() { Id = 12 }, new() { Id = 11 }
            };

            //Act
            var result = await _servicio.ObtenerFeedSeguidos(10, null, 2);

            //Assert
            Assert.Equal(2, result.Items.Count);
            Assert.Null(result.SiguienteCursor);
        }

        [Fact]
        public async Task QueSeGenerenRecomendacionesYUnaSesionSiNoSeEnviaCursor()
        {
            //Arrange
            _repositorio.Recomendaciones = new[] { 7, 3 };
            _repositorio.PublicacionesPorIds = new List<PublicacionDetalleDto>
            {
                new() { Id = 7 }, new() { Id = 3 }
            };

            //Act
            var result = await _servicio.ObtenerPublicacionesParaVos(10, null, 1);

            //Assert
            Assert.Equal(7, Assert.Single(result.Items).Id);
            Assert.NotNull(result.SiguienteCursor);
            Assert.Equal(10, _repositorio.UsuarioRecomendacionesConsultado);
            Assert.Equal(500, _repositorio.LimiteRecomendaciones);
            var sesion = Assert.Single(_sesiones.Creadas);
            Assert.Equal(10, sesion.UsuarioId);
            Assert.Equal(new[] { 7, 3 }, sesion.PublicacionesIds);
        }
        
        [Fact]
        public async Task QueSeContinueLaSesionSinVolverAGenerarRecomendaciones()
        {
            //Arrange
            _repositorio.Recomendaciones = new[] { 7, 3, 9 };
            _repositorio.PublicacionesPorIds = new List<PublicacionDetalleDto>
            {
                new() { Id = 7 }, new() { Id = 3 }, new() { Id = 9 }
            };
            var primeraPagina = await _servicio.ObtenerPublicacionesParaVos(10, null, 1);

            //Act
            var result = await _servicio.ObtenerPublicacionesParaVos(10, primeraPagina.SiguienteCursor, 1);

            //Assert
            Assert.Equal(3, Assert.Single(result.Items).Id);
            Assert.Equal(1, _repositorio.ConsultasRecomendaciones);
            Assert.Single(_sesiones.Creadas);
            Assert.Equal(_sesiones.Creadas[0].Id, _sesiones.IdConsultado);
            Assert.Equal(10, _sesiones.UsuarioConsultado);
            Assert.Equal(new[] { 3, 9 }, _repositorio.LotesConsultados[^1]);
        }

        [Fact]
        public async Task QueSeGenereUnaNuevaSesionAlRecargarSinCursor()
        {
            //Arrange
            _repositorio.Recomendaciones = new[] { 7, 3 };
            _repositorio.PublicacionesPorIds = new List<PublicacionDetalleDto>
            {
                new() { Id = 7 }, new() { Id = 3 }
            };
            await _servicio.ObtenerPublicacionesParaVos(10, null, 1);
            _repositorio.Recomendaciones = new[] { 3, 7 };

            //Act
            var result = await _servicio.ObtenerPublicacionesParaVos(10, null, 1);

            //Assert
            Assert.Equal(2, _repositorio.ConsultasRecomendaciones);
            Assert.Equal(2, _sesiones.Creadas.Count);
            Assert.NotEqual(_sesiones.Creadas[0].Id, _sesiones.Creadas[1].Id);
            Assert.Equal(3, Assert.Single(result.Items).Id);
        }

        [Fact]
        public async Task QueNoSeContinueUnaSesionConUnaPosicionFueraDeRango()
        {
            //Arrange
            var sesion = _sesiones.Crear(10, new[] { 7, 3 });
            var cursorDto = new CursorFypDto { SesionId = sesion.Id, Posicion = 3 };
            var cursor = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(cursorDto))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            //Act
            await Assert.ThrowsAsync<ArgumentException>(
                () => _servicio.ObtenerPublicacionesParaVos(10, cursor, 20));

            //Assert
            Assert.Empty(_repositorio.LotesConsultados);
            Assert.Equal(0, _repositorio.ConsultasRecomendaciones);
        }

        [Fact]
        public async Task QueLasPublicacionesRespetenElOrdenDeLaSesion()
        {
            //Arrange
            _repositorio.Recomendaciones = new[] { 9, 3, 7 };
            _repositorio.PublicacionesPorIds = new List<PublicacionDetalleDto>
            {
                new() { Id = 3 }, new() { Id = 7 }, new() { Id = 9 }
            };

            //Act
            var result = await _servicio.ObtenerPublicacionesParaVos(10, null, 20);

            //Assert
            Assert.Equal(new[] { 9, 3, 7 }, result.Items.Select(p => p.Id));
        }

        [Fact]
        public async Task QueSeCompleteLaPaginaOmitiendoPublicacionesNoDisponibles()
        {
            //Arrange
            _repositorio.Recomendaciones = Enumerable.Range(1, 105).ToArray();
            _repositorio.PublicacionesPorIds = new List<PublicacionDetalleDto>
            {
                new() { Id = 105 }, new() { Id = 51 }, new() { Id = 2 }
            };

            //Act
            var result = await _servicio.ObtenerPublicacionesParaVos(10, null, 2);
            var lotesPrimeraPagina = _repositorio.LotesConsultados.Count;
            var segundaPagina = await _servicio.ObtenerPublicacionesParaVos(10, result.SiguienteCursor, 2);

            //Assert
            Assert.Equal(new[] { 2, 51 }, result.Items.Select(p => p.Id));
            Assert.NotNull(result.SiguienteCursor);
            Assert.Equal(2, lotesPrimeraPagina);
            Assert.Equal(105, Assert.Single(segundaPagina.Items).Id);
            Assert.Equal(new[] { 105 }, _repositorio.LotesConsultados[^1]);
        }

        [Fact]
        public async Task SinRecomendacionesDevuelveGeneralesPorFechaYConservaLaPaginacion()
        {
            var fecha = DateTime.UtcNow.AddDays(-1);
            _repositorio.PublicacionesPorIds = new()
            {
                new() { Id = 8, FechaCreacion = fecha.AddDays(-1) },
                new() { Id = 2, FechaCreacion = fecha },
                new() { Id = 3, FechaCreacion = fecha }
            };

            var primera = await _servicio.ObtenerPublicacionesParaVos(10, null, 2);
            var segunda = await _servicio.ObtenerPublicacionesParaVos(10, primera.SiguienteCursor, 2);

            Assert.Equal(new[] { 3, 2 }, primera.Items.Select(p => p.Id));
            Assert.NotNull(primera.SiguienteCursor);
            Assert.Equal(8, Assert.Single(segunda.Items).Id);
            Assert.Null(segunda.SiguienteCursor);
            Assert.Equal(1, _repositorio.ConsultasGenerales);
            Assert.Equal(1, _repositorio.ConsultasRecomendaciones);
        }

        [Fact]
        public async Task SinRecomendacionesNiPublicacionesDevuelvePaginaVacia()
        {
            var resultado = await _servicio.ObtenerPublicacionesParaVos(10, null, 20);

            Assert.Empty(resultado.Items);
            Assert.Null(resultado.SiguienteCursor);
            Assert.Equal(1, _repositorio.ConsultasGenerales);
        }

        // Estos dobles reemplazan las dependencias externas del servicio.
        private class PublicacionRepositorioFake : IPublicacionRepositorio
        {
            public Task<int> CrearComentario(Comentario comentario, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public int IdCreado { get; set; } = 1;
            public Publicacion? Creada { get; private set; }
            public Publicacion? Original { get; set; }
            public int? IdOriginalConsultado { get; private set; }
            public List<PublicacionPinDto> Pines { get; set; } = new();
            public PublicacionFiltroDto? FiltroConsultado { get; private set; }
            public int LimitePines { get; private set; }
            public PublicacionResumenDto? Resumen { get; set; }
            public int? IdResumenConsultado { get; private set; }
            public PublicacionDetalleDto? Detalle { get; set; }
            public int? IdDetalleConsultado { get; private set; }
            public bool ExisteVisible { get; set; }
            public int? IdVisibleConsultado { get; private set; }
            public List<ComentarioDto> Comentarios { get; set; } = new();
            public int ConsultasComentarios { get; private set; }
            public int IdComentariosConsultado { get; private set; }
            public int LimiteComentarios { get; private set; }
            public CursorCronologicoDto? CursorComentarios { get; private set; }
            public List<PublicacionDetalleDto> Feed { get; set; } = new();
            public int ConsultasFeed { get; private set; }
            public IReadOnlyCollection<int>? AutoresConsultados { get; private set; }
            public int LimiteFeed { get; private set; }
            public CursorCronologicoDto? CursorFeed { get; private set; }
            public List<PublicacionDetalleDto> PublicacionesPorIds { get; set; } = new();
            public List<int[]> LotesConsultados { get; } = new();
            public IReadOnlyList<int> Recomendaciones { get; set; } = Array.Empty<int>();
            public int ConsultasRecomendaciones { get; private set; }
            public int UsuarioRecomendacionesConsultado { get; private set; }
            public int LimiteRecomendaciones { get; private set; }

            public Task<int> Crear(Publicacion publicacion)
            {
                Creada = publicacion;
                return Task.FromResult(IdCreado);
            }

            public Task<Publicacion?> ObtenerPorId(int id, CancellationToken cancellationToken = default)
            {
                IdOriginalConsultado = id;
                return Task.FromResult(Original);
            }

            public Task<List<PublicacionPinDto>> ObtenerPines(PublicacionFiltroDto filtro, int limite, CancellationToken cancellationToken = default)
            {
                FiltroConsultado = filtro;
                LimitePines = limite;
                return Task.FromResult(Pines);
            }

            public Task<PublicacionResumenDto?> ObtenerResumenPorId(int id, CancellationToken cancellationToken = default)
            {
                IdResumenConsultado = id;
                return Task.FromResult(Resumen);
            }

            public Task<PublicacionDetalleDto?> ObtenerDetallePorId(int id, CancellationToken cancellationToken = default)
            {
                IdDetalleConsultado = id;
                return Task.FromResult(Detalle);
            }

            public Task<bool> ExistePublicacionVisible(int id, CancellationToken cancellationToken = default)
            {
                IdVisibleConsultado = id;
                return Task.FromResult(ExisteVisible);
            }

            public Task CrearLike(int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task QuitarLike(int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<MeGustaPublicacionDto> ObtenerEstadoLike(int publicacionId, int usuarioId, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<IReadOnlyDictionary<int, (int Cantidad, bool DioLike)>> ObtenerResumenLikes(
                IReadOnlyCollection<int> publicacionesIds, int usuarioId, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyDictionary<int, (int Cantidad, bool DioLike)>>(
                    publicacionesIds.Distinct().ToDictionary(id => id, _ => (Cantidad: 0, DioLike: false)));

            public Task<List<ComentarioDto>> ObtenerComentarios(int publicacionId, CursorCronologicoDto? cursor, int limite, CancellationToken cancellationToken = default)
            {
                ConsultasComentarios++;
                IdComentariosConsultado = publicacionId;
                CursorComentarios = cursor;
                LimiteComentarios = limite;
                return Task.FromResult(Comentarios);
            }

            public Task<List<PublicacionDetalleDto>> ObtenerFeedSeguidos(IReadOnlyCollection<int> autoresIds, CursorCronologicoDto? cursor, int limite, CancellationToken cancellationToken = default)
            {
                ConsultasFeed++;
                AutoresConsultados = autoresIds;
                CursorFeed = cursor;
                LimiteFeed = limite;
                return Task.FromResult(Feed);
            }

            public Task<List<PublicacionDetalleDto>> ObtenerPublicacionesPorIds(IReadOnlyCollection<int> publicacionesIds, CancellationToken cancellationToken = default)
            {
                LotesConsultados.Add(publicacionesIds.ToArray());
                return Task.FromResult(PublicacionesPorIds.Where(p => publicacionesIds.Contains(p.Id)).ToList());
            }

            public Task<IReadOnlyList<int>> ObtenerPublicacionesRelacionadasPorLikes(
                int usuarioId, int cantidadMaxima, CancellationToken cancellationToken = default)
            {
                ConsultasRecomendaciones++;
                UsuarioRecomendacionesConsultado = usuarioId;
                LimiteRecomendaciones = cantidadMaxima;
                return Task.FromResult(Recomendaciones);
            }

            public List<Publicacion> ObtenerTodasPublicaciones() => throw new NotImplementedException();
            public List<Publicacion> ObtenerPublicacionPorCategoria(CategoriaEnum categoria) => throw new NotImplementedException();
            public int ConsultasGenerales { get; private set; }
            public Task<List<PublicacionDetalleDto>> ObtenerPublicacionesParaVos(IReadOnlyCollection<CategoriaEnum> categorias, DateTime fechaCorte, CursorCronologicoDto? cursor, int limite, CancellationToken cancellationToken = default)
            {
                ConsultasGenerales++;
                Assert.Empty(categorias);
                return Task.FromResult(PublicacionesPorIds.Where(p => p.FechaCreacion <= fechaCorte)
                    .OrderByDescending(p => p.FechaCreacion).ThenByDescending(p => p.Id)
                    .Take(limite + 1).ToList());
            }
            public Task<Dictionary<CategoriaEnum, int>> ObtenerCantidadPorCategoriaDelUsuario(int usuarioId, DateTime desde, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public bool EliminarResultado { get; set; } = true;
            public int? IdEliminado { get; private set; }
            public int? AutorEliminado { get; private set; }
            public Task<bool> Eliminar(int id, int? autorId = null, CancellationToken cancellationToken = default)
            {
                IdEliminado = id;
                AutorEliminado = autorId;
                return Task.FromResult(EliminarResultado);
            }
            public Task<bool> EliminarRepost(int originalId, int? autorId = null, CancellationToken cancellationToken = default)
            {
                IdEliminado = originalId;
                AutorEliminado = autorId;
                return Task.FromResult(EliminarResultado);
            }
        }

        private class UsuarioRepositorioFake : IUsuarioRepositorio
        {
            public bool Existe { get; set; }
            public int IdConsultado { get; private set; }

            public Task<bool> ExisteUsuario(int id, CancellationToken cancellationToken = default)
            {
                IdConsultado = id;
                return Task.FromResult(Existe);
            }

            public Task<Usuario?> ObtenerPorIdAsync(int id) => throw new NotImplementedException();
            public Task<List<AutorResumenDto>> ObtenerUsuariosPorIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<List<int>> ObtenerIdsExistentesAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> ExistePorEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task AgregarAsync(Usuario usuario, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task ActualizarAsync(Usuario usuario, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<Usuario?> ObtenerPorUsernameAsync(string username, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private class MultimediaServicioFake : IMultimediaServicio
        {
            public string Url { get; } = "https://example.com/subida.jpg";
            public string? UrlExterna { get; private set; }
            public Exception? Error { get; set; }
            public int Llamadas { get; private set; }

            public Task<string> SubirAsync(Stream? archivo, string? nombreArchivo, string? urlExterna, MultimediaEnum tipo)
            {
                Llamadas++;
                UrlExterna = urlExterna;
                return Error == null ? Task.FromResult(Url) : Task.FromException<string>(Error);
            }
        }

        private class SesionFypRepositorioFake : ISesionFypRepositorio
        {
            public List<SesionFypDto> Creadas { get; } = new();
            public Guid? IdConsultado { get; private set; }
            public int UsuarioConsultado { get; private set; }

            public SesionFypDto Crear(int usuarioId, IReadOnlyCollection<int> publicacionesIds)
            {
                var sesion = new SesionFypDto(Guid.NewGuid(), usuarioId, DateTime.UtcNow.AddMinutes(30), publicacionesIds);
                Creadas.Add(sesion);
                return sesion;
            }

            public SesionFypDto? Obtener(Guid sesionId, int usuarioId)
            {
                IdConsultado = sesionId;
                UsuarioConsultado = usuarioId;
                return Creadas.SingleOrDefault(s => s.Id == sesionId && s.UsuarioId == usuarioId);
            }
        }

        private class SeguimientoServicioFake : ISeguimientoServicio
        {
            public IReadOnlyCollection<int> Seguidos { get; set; } = Array.Empty<int>();
            public int UsuarioConsultado { get; private set; }
            public Exception? Error { get; set; }

            public Task<IReadOnlyCollection<int>> ObtenerSeguidosIdsAsync(int usuarioId, CancellationToken cancellationToken = default)
            {
                UsuarioConsultado = usuarioId;
                return Error == null ? Task.FromResult(Seguidos) : Task.FromException<IReadOnlyCollection<int>>(Error);
            }

            public Task<ResultadoSeguimiento> SeguirUsuario(int idUsuario, int idUsuarioASeguir, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<ResultadoSeguimiento> DejarDeSeguirUsuario(int idUsuario, int idUsuarioASeguir, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<AutorResumenDto>?> ListarSeguidos(int idUsuario, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IReadOnlyList<AutorResumenDto>?> ListarSeguidores(int idUsuario, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }
    }
}
