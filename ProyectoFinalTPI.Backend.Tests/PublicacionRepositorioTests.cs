using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests
{
    public class PublicacionRepositorioTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly PublicacionRepositorio _repositorio;

        public PublicacionRepositorioTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _repositorio = new PublicacionRepositorio(_context);
        }

        private readonly Usuario_Personal usuario = new Usuario_Personal
        {
            Id = 10,
            Username = "usuarioTest",
            Email = "test@test.com",
            Contraseña = "password",
            Nombre = "Juan",
            Apellido = "Prueba",
            FechaNacimiento = new DateOnly(1995, 1, 1)
        };

         private readonly Lugar lugar = new Lugar
        {
            Id = 1,
            Nombre = "Obelisco",
            Direccion = "Buenos Aires",
            Coordenadas = new Point(-58.3816, -34.6037)
            {
                SRID = 4326
            }
        };

        [Fact]
        public async Task QueFeedGeneralExcluyaIdsInteractuadosAntesDeAplicarElLimite()
        {
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var id = 1; id <= 3; id++)
            {
                _context.Publicaciones.Add(new Publicacion
                {
                    Id = id,
                    Titulo = $"Publicación {id}",
                    Descripcion = "Recuerdo de prueba",
                    FechaCreación = fecha.AddMinutes(id),
                    Fecha = fecha,
                    UsuarioId = usuario.Id,
                    Usuario = usuario,
                    LugarId = lugar.Id,
                    Lugar = lugar
                });
            }
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var resultado = await _repositorio.ObtenerPublicacionesParaVos(
                Array.Empty<CategoriaEnum>(), fecha.AddDays(1), null, 1, new[] { 3 });

            Assert.Equal(new[] { 2, 1 }, resultado.Select(p => p.Id));
        }


        [Fact]
        public async Task QueSePuedaObtenerUnaPublicacionPorSuId()
        {
            //Arrange      
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);

            var publicacion = new Publicacion
            {
                Id = 1,
                Titulo = "Recuerdo del Obelisco",
                Descripcion = "Una visita a Buenos Aires",
                Fecha = new DateTime(
                    2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FechaCreación = new DateTime(
                    2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                Categoria = CategoriaEnum.Lugares,
                TipoMultimedia = MultimediaEnum.Foto,
                UrlMultimedia = "https://example.com/foto.jpg",
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion);
            await _context.SaveChangesAsync();

            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerPorId(1);

            //Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Recuerdo del Obelisco", result.Titulo);
            Assert.Equal(usuario.Id, result.UsuarioId);
            Assert.NotNull(result.Lugar);
            Assert.Equal("Obelisco", result.Lugar.Nombre);
        }

        [Fact]
        public async Task QueNoSeObtengaUnaPublicacionSiElIdNoExiste()
        {
            //Arrange
            var idInexistente = 99;

            //Act
            var result = await _repositorio.ObtenerPorId(idInexistente);

            //Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task QueNoSeObtengaUnaPublicacionQueEstaOculta()
        {
            //Arrange      
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);

            var publicacion = new Publicacion
            {
                Id = 1,
                Titulo = "Recuerdo del Obelisco",
                Descripcion = "Una visita a Buenos Aires",
                Fecha = new DateTime(
                    2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FechaCreación = new DateTime(
                    2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                Categoria = CategoriaEnum.Lugares,
                TipoMultimedia = MultimediaEnum.Foto,
                UrlMultimedia = "https://example.com/foto.jpg",
                EstaOculto = true,
                MotivoOculto = "Publicación en revisión",
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion);
            await _context.SaveChangesAsync();

            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerPorId(1);

            //Assert
            Assert.Null(result);

            Assert.True(
                await _context.Publicaciones.AnyAsync(p => p.Id == 1));
        }

        [Fact]
        public async Task QueSePuedaCrearUnaPublicacion()
        {
            //Arrange
            var usuario = new Usuario_Personal
            {
                Id = 10,
                Username = "usuarioTest",
                Email = "test@test.com",
                Contraseña = "password",
                Nombre = "Juan",
                Apellido = "Prueba",
                FechaNacimiento = new DateOnly(1995, 1, 1)
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            var lugar = new Lugar
            {
                Nombre = "Obelisco",
                Direccion = "Buenos Aires",
                Coordenadas = new Point(-58.3816, -34.6037)
                {
                    SRID = 4326
                }
            };

            var publicacion = new Publicacion
            {
                Titulo = "Recuerdo del Obelisco",
                Descripcion = "Una visita a Buenos Aires",
                Fecha = new DateTime(
                    2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FechaCreación = new DateTime(
                    2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                Categoria = CategoriaEnum.Lugares,
                TipoMultimedia = MultimediaEnum.Foto,
                UrlMultimedia = "https://example.com/foto.jpg",
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                Lugar = lugar
            };

            //Act
            var result = await _repositorio.Crear(publicacion);

            //Assert
            Assert.True(result > 0);

            _context.ChangeTracker.Clear();

            var publicacionGuardada = await _context.Publicaciones
                .Include(p => p.Lugar)
                .SingleAsync(p => p.Id == result);

            Assert.Equal("Recuerdo del Obelisco", publicacionGuardada.Titulo);
            Assert.Equal("Obelisco", publicacionGuardada.Lugar.Nombre);
            Assert.Equal(usuario.Id, publicacionGuardada.UsuarioId);
        }

        [Fact]
        public async Task QueSeObtenganSoloLosPinesVisiblesDentroDelArea()
        {
            //Arrange
            _context.Usuarios.Add(usuario);

            var lugarDentro = new Lugar
            {
                Id = 1,
                Nombre = "Obelisco",
                Direccion = "Buenos Aires",
                Coordenadas = new Point(-58.3816, -34.6037)
                {
                    SRID = 4326
                }
            };

            var lugarFuera = new Lugar
            {
                Id = 2,
                Nombre = "Fuera del área",
                Direccion = "Lugar de prueba",
                Coordenadas = new Point(-60, -34.6037)
                {
                    SRID = 4326
                }
            };

            _context.Lugares.AddRange(lugarDentro, lugarFuera);

            var publicacionDentro = new Publicacion
            {
                Id = 1,
                Titulo = "Publicación dentro del área",
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugarDentro.Id,
                Lugar = lugarDentro
            };

            var publicacionFuera = new Publicacion
            {
                Id = 2,
                Titulo = "Publicación fuera del área",
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugarFuera.Id,
                Lugar = lugarFuera
            };

            var publicacionOculta = new Publicacion
            {
                Id = 3,
                Titulo = "Publicación oculta",
                EstaOculto = true,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugarDentro.Id,
                Lugar = lugarDentro
            };

            _context.Publicaciones.AddRange(
                publicacionDentro,
                publicacionFuera,
                publicacionOculta);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var filtro = new PublicacionFiltroDto
            {
                MinLatitud = -35,
                MaxLatitud = -34,
                MinLongitud = -59,
                MaxLongitud = -58
            };

            //Act
            var result = await _repositorio.ObtenerPines(filtro, limite: 20);

            //Assert
            var pin = Assert.Single(result);
            Assert.Equal(publicacionDentro.Id, pin.Id);
        }

        [Fact]
        public async Task QueSeObtenganSoloLosPinesDeLaCategoriaIndicada()
        {
            //Arrange      
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);

            var publicacionDeLugares = new Publicacion
            {
                Id = 1,
                Titulo = "Recuerdo del Obelisco",
                Categoria = CategoriaEnum.Lugares,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            var publicacionDeMusica = new Publicacion
            {
                Id = 2,
                Titulo = "Recuerdo de un recital",
                Categoria = CategoriaEnum.Música,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.AddRange(
                publicacionDeLugares,
                publicacionDeMusica);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var filtro = new PublicacionFiltroDto
            {
                MinLatitud = -35,
                MaxLatitud = -34,
                MinLongitud = -59,
                MaxLongitud = -58,
                Categoria = CategoriaEnum.Música
            };

            //Act
            var result = await _repositorio.ObtenerPines(filtro, limite: 20);

            //Assert
            var pin = Assert.Single(result);
            Assert.Equal(publicacionDeMusica.Id, pin.Id);
        }

        [Fact]
        public async Task QueSeObtenganLosPinesDentroDelRangoDeFechas()
        {
            //Arrange      
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);

            var publicacionAnterior = new Publicacion
            {
                Id = 1,
                Titulo = "Recuerdo anterior",
                Fecha = new DateTime(
                    1999, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            var publicacionEnFechaDesde = new Publicacion
            {
                Id = 2,
                Titulo = "Recuerdo al inicio del rango",
                Fecha = new DateTime(
                    2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            var publicacionDentroDelRango = new Publicacion
            {
                Id = 3,
                Titulo = "Recuerdo dentro del rango",
                Fecha = new DateTime(
                    2000, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            var publicacionEnFechaHasta = new Publicacion
            {
                Id = 4,
                Titulo = "Recuerdo en el límite final",
                Fecha = new DateTime(
                    2001, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.AddRange(
                publicacionAnterior,
                publicacionEnFechaDesde,
                publicacionDentroDelRango,
                publicacionEnFechaHasta);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var filtro = new PublicacionFiltroDto
            {
                MinLatitud = -35,
                MaxLatitud = -34,
                MinLongitud = -59,
                MaxLongitud = -58,
                FechaDesde = new DateTime(
                    2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FechaHasta = new DateTime(
                    2001, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            //Act
            var result = await _repositorio.ObtenerPines(filtro, limite: 20);

            //Assert
            Assert.Equal(
                new[] { publicacionEnFechaDesde.Id, publicacionDentroDelRango.Id },
                result.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task QueSeObtenganLosPinesHastaElLimiteMasUno()
        {
            //Arrange      
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);

            foreach (var id in new[] { 4, 2, 1, 3 })
            {
                var publicacion = new Publicacion
                {
                    Id = id,
                    Titulo = $"Publicación {id}",
                    EstaOculto = false,
                    UsuarioId = usuario.Id,
                    Usuario = usuario,
                    LugarId = lugar.Id,
                    Lugar = lugar
                };

                _context.Publicaciones.Add(publicacion);
            }

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var filtro = new PublicacionFiltroDto
            {
                MinLatitud = -35,
                MaxLatitud = -34,
                MinLongitud = -59,
                MaxLongitud = -58
            };

            //Act
            var result = await _repositorio.ObtenerPines(filtro, limite: 2);

            //Assert
            Assert.Equal(
                new[] { 1, 2, 3 },
                result.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task QueSeObtengaElResumenConLaDescripcionRecortada()
        {
            //Arrange      
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);

            var inicioDescripcion = new string('A', 180);

            var publicacion = new Publicacion
            {
                Id = 1,
                Titulo = "Recuerdo del Obelisco",
                Descripcion = inicioDescripcion + "Texto que no debe aparecer",
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion);
            await _context.SaveChangesAsync();

            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerResumenPorId(1);

            //Assert
            Assert.NotNull(result);
            Assert.Equal(inicioDescripcion + "...", result.DescripcionBreve);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(179)]
        [InlineData(180)]
        public async Task QueElResumenNoRecorteUnaDescripcionQueNoSuperaElLimite(
    int cantidadCaracteres)
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);

            var descripcion = new string('A', cantidadCaracteres);

            var publicacion = new Publicacion
            {
                Id = 1,
                Titulo = "Recuerdo del Obelisco",
                Descripcion = descripcion,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion);
            await _context.SaveChangesAsync();

            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerResumenPorId(1);

            //Assert
            Assert.NotNull(result);
            Assert.Equal(descripcion, result.DescripcionBreve);
        }

        [Fact]
        public async Task QueSeObtengaElDetalleConLaDescripcionCompleta()
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);

            var descripcion = new string('A', 500);

            var publicacion = new Publicacion
            {
                Id = 1,
                Titulo = "Recuerdo del Obelisco",
                Descripcion = descripcion,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion);
            await _context.SaveChangesAsync();

            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerDetallePorId(1);

            //Assert
            Assert.NotNull(result);
            Assert.Equal(descripcion, result.Descripcion);
            Assert.Equal(usuario.Id, result.Autor.Id);
            Assert.Equal(lugar.Id, result.Lugar.Id);
        }

        [Fact]
        public async Task QueNoSeObtengaElDetalleDeUnaPublicacionOculta()
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);

            var publicacion = new Publicacion
            {
                Id = 1,
                Titulo = "Recuerdo del Obelisco",
                Descripcion = "Descripción de prueba",
                EstaOculto = true,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion);
            await _context.SaveChangesAsync();

            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerDetallePorId(1);

            //Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task QueSeObtenganLasPublicacionesDeLosUsuariosSeguidos()
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);

            var otroUsuario = new Usuario_Personal
            {
                Id = usuario.Id + 1,
                Username = "otroUsuario",
                Email = "otro@test.com",
                Contraseña = "password",
                Nombre = "Pedro",
                Apellido = "Prueba",
                FechaNacimiento = new DateOnly(1995, 1, 1)
            };

            _context.Usuarios.Add(otroUsuario);

            var fechaCreacion = new DateTime(
                2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

            var primeraPublicacion = new Publicacion
            {
                Id = 1,
                Titulo = "Primera publicación",
                FechaCreación = fechaCreacion,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            var segundaPublicacion = new Publicacion
            {
                Id = 2,
                Titulo = "Segunda publicación",
                FechaCreación = fechaCreacion.AddHours(1),
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            var publicacionDeOtroUsuario = new Publicacion
            {
                Id = 3,
                Titulo = "Publicación de alguien que no sigo",
                FechaCreación = fechaCreacion.AddHours(2),
                EstaOculto = false,
                UsuarioId = otroUsuario.Id,
                Usuario = otroUsuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.AddRange(
                primeraPublicacion,
                segundaPublicacion,
                publicacionDeOtroUsuario);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var autoresIds = new[] { usuario.Id };

            //Act
            var result = await _repositorio.ObtenerFeedSeguidos(
                autoresIds,
                cursor: null,
                limite: 20);

            //Assert
            Assert.Equal(
                new[] { segundaPublicacion.Id, primeraPublicacion.Id },
                result.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task QueNoSeObtenganPublicacionesSiNoHayUsuariosSeguidos()
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var publicacion1 = new Publicacion
            {
                Id = 1,
                Titulo = "Publicación 1",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion1);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerFeedSeguidos(
                Array.Empty<int>(), null, limite: 20);

            //Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task QueNoSeObtenganPublicacionesOcultasEnElFeedSeguidos()
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var publicacion1 = new Publicacion
            {
                Id = 1,
                Titulo = "Publicación 1",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion1);

            var publicacion2 = new Publicacion
            {
                Id = 2,
                Titulo = "Publicación 2",
                FechaCreación = fecha,
                EstaOculto = true,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion2);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerFeedSeguidos(
                new[] { usuario.Id }, null, limite: 20);

            //Assert
            var publicacion = Assert.Single(result);
            Assert.Equal(1, publicacion.Id);
        }

        [Fact]
        public async Task QueSePuedaContinuarElFeedSeguidosSinRepetirPublicaciones()
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var publicacion10 = new Publicacion
            {
                Id = 10,
                Titulo = "Publicación 10",
                FechaCreación = fecha.AddDays(-1),
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion10);

            var publicacion1 = new Publicacion
            {
                Id = 1,
                Titulo = "Publicación 1",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion1);

            var publicacion2 = new Publicacion
            {
                Id = 2,
                Titulo = "Publicación 2",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion2);

            var publicacion3 = new Publicacion
            {
                Id = 3,
                Titulo = "Publicación 3",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion3);

            var publicacion4 = new Publicacion
            {
                Id = 4,
                Titulo = "Publicación 4",
                FechaCreación = fecha.AddDays(1),
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion4);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            //Act
            var primeraPagina = await _repositorio.ObtenerFeedSeguidos(
                new[] { usuario.Id }, cursor: null, limite: 2);

            var ultimaMostrada = primeraPagina[1];
            var cursor = new CursorCronologicoDto
            {
                FechaCreacion = ultimaMostrada.FechaCreacion,
                Id = ultimaMostrada.Id
            };

            var result = await _repositorio.ObtenerFeedSeguidos(
                new[] { usuario.Id }, cursor, limite: 2);

            //Assert
            Assert.Equal(new[] { 4, 3, 2 }, primeraPagina.Select(p => p.Id).ToArray());
            Assert.Equal(new[] { 2, 1, 10 }, result.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task QueElFeedSeguidosDevuelvaHastaElLimiteMasUno()
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var publicacion1 = new Publicacion
            {
                Id = 1,
                Titulo = "Publicación 1",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion1);

            var publicacion4 = new Publicacion
            {
                Id = 4,
                Titulo = "Publicación 4",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion4);

            var publicacion2 = new Publicacion
            {
                Id = 2,
                Titulo = "Publicación 2",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion2);

            var publicacion3 = new Publicacion
            {
                Id = 3,
                Titulo = "Publicación 3",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion3);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerFeedSeguidos(
                new[] { usuario.Id }, null, limite: 2);

            //Assert
            Assert.Equal(new[] { 4, 3, 2 }, result.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task QueLaDescripcionDelFeedSeguidosNoSupereLosTrescientosCaracteres()
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var publicacion1 = new Publicacion
            {
                Id = 1,
                Titulo = "Publicación 1",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar,
                Descripcion = new string('A', 297) + "Texto que no debe aparecer"
            };

            _context.Publicaciones.Add(publicacion1);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerFeedSeguidos(
                new[] { usuario.Id }, null, limite: 20);

            //Assert
            var publicacion = Assert.Single(result);
            Assert.Equal(new string('A', 297) + "...", publicacion.Descripcion);
        }

        [Fact]
        public async Task QueElFeedSeguidosDevuelvaElIdDelLugarCorrecto()
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var publicacion25 = new Publicacion
            {
                Id = 25,
                Titulo = "Publicación 25",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion25);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerFeedSeguidos(
                new[] { usuario.Id }, null, limite: 20);

            //Assert
            var publicacion = Assert.Single(result);
            Assert.Equal(lugar.Id, publicacion.Lugar.Id);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(99)]
        public async Task QueNoSeObtengaElResumenDeUnaPublicacionNoDisponible(int id)
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var publicacion1 = new Publicacion
            {
                Id = 1,
                Titulo = "Publicación 1",
                FechaCreación = fecha,
                EstaOculto = true,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion1);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerResumenPorId(id);

            //Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task QueNoSeObtengaElDetalleDeUnaPublicacionInexistente()
        {
            //Arrange
            var idInexistente = 99;

            //Act
            var result = await _repositorio.ObtenerDetallePorId(idInexistente);

            //Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(2, false)]
        [InlineData(99, false)]
        public async Task QueSeVerifiqueSiUnaPublicacionExisteYEsVisible(int id, bool esperado)
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var publicacion1 = new Publicacion
            {
                Id = 1,
                Titulo = "Publicación 1",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion1);

            var publicacion2 = new Publicacion
            {
                Id = 2,
                Titulo = "Publicación 2",
                FechaCreación = fecha,
                EstaOculto = true,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion2);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ExistePublicacionVisible(id);

            //Assert
            Assert.Equal(esperado, result);
        }

        [Fact]
        public async Task QueLasPublicacionesParaVosRespetenLasCategoriasIndicadas()
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var publicacion1 = new Publicacion
            {
                Id = 1,
                Titulo = "Publicación 1",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar,
                Categoria = CategoriaEnum.Cine
            };

            _context.Publicaciones.Add(publicacion1);

            var publicacion2 = new Publicacion
            {
                Id = 2,
                Titulo = "Publicación 2",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar,
                Categoria = CategoriaEnum.Música
            };

            _context.Publicaciones.Add(publicacion2);

            var publicacion3 = new Publicacion
            {
                Id = 3,
                Titulo = "Publicación 3",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar,
                Categoria = CategoriaEnum.Lugares
            };

            _context.Publicaciones.Add(publicacion3);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerPublicacionesParaVos(
                new[] { CategoriaEnum.Cine, CategoriaEnum.Música }, fecha, null, limite: 20);

            //Assert
            Assert.Equal(new[] { 2, 1 }, result.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task QueSeObtenganPublicacionesDeCualquierCategoriaSiNoSeIndicanIntereses()
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var publicacion1 = new Publicacion
            {
                Id = 1,
                Titulo = "Publicación 1",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar,
                Categoria = CategoriaEnum.Cine
            };

            _context.Publicaciones.Add(publicacion1);

            var publicacion2 = new Publicacion
            {
                Id = 2,
                Titulo = "Publicación 2",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar,
                Categoria = CategoriaEnum.Música
            };

            _context.Publicaciones.Add(publicacion2);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerPublicacionesParaVos(
                Array.Empty<CategoriaEnum>(), fecha, null, limite: 20);

            //Assert
            Assert.Equal(new[] { 2, 1 }, result.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task QueLasPublicacionesParaVosRespetenLaFechaDeCorteYLaVisibilidad()
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var publicacion1 = new Publicacion
            {
                Id = 1,
                Titulo = "Publicación 1",
                FechaCreación = fecha.AddDays(-1),
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion1);

            var publicacion2 = new Publicacion
            {
                Id = 2,
                Titulo = "Publicación 2",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion2);

            var publicacion3 = new Publicacion
            {
                Id = 3,
                Titulo = "Publicación 3",
                FechaCreación = fecha.AddSeconds(1),
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion3);

            var publicacion4 = new Publicacion
            {
                Id = 4,
                Titulo = "Publicación 4",
                FechaCreación = fecha,
                EstaOculto = true,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion4);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerPublicacionesParaVos(
                Array.Empty<CategoriaEnum>(), fecha, null, limite: 20);

            //Assert
            Assert.Equal(new[] { 2, 1 }, result.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task QueLasPublicacionesParaVosRespetenElCursorYElLimiteMasUno()
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var publicacion11 = new Publicacion
            {
                Id = 11,
                Titulo = "Publicación 11",
                FechaCreación = fecha.AddDays(-2),
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion11);

            var publicacion10 = new Publicacion
            {
                Id = 10,
                Titulo = "Publicación 10",
                FechaCreación = fecha.AddDays(-1),
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion10);

            var publicacion1 = new Publicacion
            {
                Id = 1,
                Titulo = "Publicación 1",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion1);

            var publicacion2 = new Publicacion
            {
                Id = 2,
                Titulo = "Publicación 2",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion2);

            var publicacion3 = new Publicacion
            {
                Id = 3,
                Titulo = "Publicación 3",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion3);

            var publicacion4 = new Publicacion
            {
                Id = 4,
                Titulo = "Publicación 4",
                FechaCreación = fecha.AddHours(1),
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion4);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
            var cursor = new CursorCronologicoDto
            {
                FechaCreacion = fecha,
                Id = 3
            };

            //Act
            var result = await _repositorio.ObtenerPublicacionesParaVos(
                Array.Empty<CategoriaEnum>(), fecha.AddDays(1), cursor, limite: 2);

            //Assert
            Assert.Equal(new[] { 2, 1, 10 }, result.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task QueSeObtenganSoloLasPublicacionesVisiblesDeLosIdsSolicitados()
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var publicacion1 = new Publicacion
            {
                Id = 1,
                Titulo = "Publicación 1",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion1);

            var publicacion2 = new Publicacion
            {
                Id = 2,
                Titulo = "Publicación 2",
                FechaCreación = fecha,
                EstaOculto = true,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion2);

            var publicacion3 = new Publicacion
            {
                Id = 3,
                Titulo = "Publicación 3",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion3);

            var publicacion4 = new Publicacion
            {
                Id = 4,
                Titulo = "Publicación 4",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion4);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerPublicacionesPorIds(
                new[] { 4, 1, 2, 1, 99 });

            //Assert
            Assert.Equal(new[] { 1, 4 }, result.Select(p => p.Id).OrderBy(id => id).ToArray());
        }

        [Fact]
        public async Task QueNoSeObtenganPublicacionesSiLaListaDeIdsEstaVacia()
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var publicacion1 = new Publicacion
            {
                Id = 1,
                Titulo = "Publicación 1",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion1);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerPublicacionesPorIds(
                Array.Empty<int>());

            //Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task QueSeCuentenPorCategoriaLasPublicacionesVisiblesDelUsuarioDesdeLaFecha()
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var otroUsuario = new Usuario_Personal
            {
                Id = usuario.Id + 1,
                Username = "otroUsuario",
                Email = "otro@test.com",
                Contraseña = "password",
                Nombre = "Pedro",
                Apellido = "Prueba",
                FechaNacimiento = new DateOnly(1995, 1, 1)
            };

            _context.Usuarios.Add(otroUsuario);

            var publicacion1 = new Publicacion
            {
                Id = 1,
                Titulo = "Publicación 1",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar,
                Categoria = CategoriaEnum.Cine
            };

            _context.Publicaciones.Add(publicacion1);

            var publicacion2 = new Publicacion
            {
                Id = 2,
                Titulo = "Publicación 2",
                FechaCreación = fecha.AddDays(1),
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar,
                Categoria = CategoriaEnum.Cine
            };

            _context.Publicaciones.Add(publicacion2);

            var publicacion3 = new Publicacion
            {
                Id = 3,
                Titulo = "Publicación 3",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar,
                Categoria = CategoriaEnum.Música
            };

            _context.Publicaciones.Add(publicacion3);

            var publicacion4 = new Publicacion
            {
                Id = 4,
                Titulo = "Publicación 4",
                FechaCreación = fecha.AddDays(-1),
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar,
                Categoria = CategoriaEnum.Cine
            };

            _context.Publicaciones.Add(publicacion4);

            var publicacion5 = new Publicacion
            {
                Id = 5,
                Titulo = "Publicación 5",
                FechaCreación = fecha,
                EstaOculto = true,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar,
                Categoria = CategoriaEnum.Música
            };

            _context.Publicaciones.Add(publicacion5);

            var publicacion6 = new Publicacion
            {
                Id = 6,
                Titulo = "Publicación 6",
                FechaCreación = fecha,
                EstaOculto = false,
                UsuarioId = otroUsuario.Id,
                Usuario = otroUsuario,
                LugarId = lugar.Id,
                Lugar = lugar,
                Categoria = CategoriaEnum.Cine
            };

            _context.Publicaciones.Add(publicacion6);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerCantidadPorCategoriaDelUsuario(
                usuario.Id, fecha);

            //Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(2, result[CategoriaEnum.Cine]);
            Assert.Equal(1, result[CategoriaEnum.Música]);
        }

        [Fact]
        public async Task QueElConteoPorCategoriaEsteVacioSiNoHayPublicacionesQueCumplanLosFiltros()
        {
            //Arrange
            _context.Usuarios.Add(usuario);
            _context.Lugares.Add(lugar);
            var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var publicacion1 = new Publicacion
            {
                Id = 1,
                Titulo = "Publicación 1",
                FechaCreación = fecha.AddDays(-1),
                EstaOculto = false,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion1);

            var publicacion2 = new Publicacion
            {
                Id = 2,
                Titulo = "Publicación 2",
                FechaCreación = fecha,
                EstaOculto = true,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                LugarId = lugar.Id,
                Lugar = lugar
            };

            _context.Publicaciones.Add(publicacion2);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            //Act
            var result = await _repositorio.ObtenerCantidadPorCategoriaDelUsuario(
                usuario.Id, fecha);

            //Assert
            Assert.Empty(result);
        }


        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
