using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Repositorio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests
{
    public class SesionFypRepositorioTests : IDisposable
    {
        private readonly IMemoryCache _cache;
        private readonly SesionFypRepositorio _repositorio;

        public SesionFypRepositorioTests()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
            _repositorio = new SesionFypRepositorio(_cache);
        }

        public void Dispose()
        {
            _cache.Dispose();
        }


        [Fact]
        public void CrearDeberiaCrearSesionYGuardarlaEnCacheCuandoDatosSonValidos()
        {
            int usuarioId = 1;
            var publicacionesIds = new List<int> { 10, 20, 30 };

            var sesion = _repositorio.Crear(usuarioId, publicacionesIds);

            Assert.NotNull(sesion);
            Assert.NotEqual(Guid.Empty, sesion.Id);
            Assert.Equal(usuarioId, sesion.UsuarioId);
            Assert.Equal(publicacionesIds.Count, sesion.PublicacionesIds.Count);

            var clave = $"fyp:sesion:{sesion.Id:N}";
            Assert.True(_cache.TryGetValue(clave, out SesionFypDto sesionEnCache));
            Assert.NotNull(sesionEnCache);
        }

        [Fact]
        public void CrearDeberiaEliminarIdsDuplicados()
        {
            int usuarioId = 1;
            var publicacionesIds = new List<int> { 10, 20, 10, 30, 20 };

            var sesion = _repositorio.Crear(usuarioId, publicacionesIds);

            Assert.NotNull(sesion);
            Assert.Equal(3, sesion.PublicacionesIds.Count);
            Assert.Contains(10, sesion.PublicacionesIds);
            Assert.Contains(20, sesion.PublicacionesIds);
            Assert.Contains(30, sesion.PublicacionesIds);
        }

        [Fact]
        public void CrearDeberiaLanzarArgumentNullExceptionCuandoListaEsNull()
        {
            int usuarioId = 1;
            IReadOnlyCollection<int> publicacionesIds = null!;

            Assert.Throws<ArgumentNullException>(() => _repositorio.Crear(usuarioId, publicacionesIds));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void CrearDeberiaLanzarArgumentExceptionCuandoUsuarioIdEsInvalido(int usuarioIdInvalido)
        {
            var publicacionesIds = new List<int> { 10, 20 };

            var excepcion = Assert.Throws<ArgumentException>(() => _repositorio.Crear(usuarioIdInvalido, publicacionesIds));
            Assert.Contains("El ID del usuario debe ser mayor que cero", excepcion.Message);
        }

        [Fact]
        public void CrearDeberiaLanzarArgumentExceptionCuandoAlgunPublicacionIdEsInvalido()
        {
            int usuarioId = 1;
            var publicacionesIds = new List<int> { 10, 0, 20 }; // El 0 es inválido

            var excepcion = Assert.Throws<ArgumentException>(() => _repositorio.Crear(usuarioId, publicacionesIds));
            Assert.Contains("Los IDs de publicaciones deben ser mayores que cero", excepcion.Message);
        }


        [Fact]
        public void ObtenerDeberiaRetornarSesionCuandoExisteYPerteneceAlUsuario()
        {
            int usuarioId = 1;
            var publicacionesIds = new List<int> { 10, 20, 30 };
            var sesionCreada = _repositorio.Crear(usuarioId, publicacionesIds);

            var sesionObtenida = _repositorio.Obtener(sesionCreada.Id, usuarioId);

            Assert.NotNull(sesionObtenida);
            Assert.Equal(sesionCreada.Id, sesionObtenida.Id);
            Assert.Equal(usuarioId, sesionObtenida.UsuarioId);
        }

        [Fact]
        public void ObtenerDeberiaRetornarNullCuandoSesionNoExiste()
        {
            var idInexistente = Guid.NewGuid();
            int usuarioId = 1;

            var sesionObtenida = _repositorio.Obtener(idInexistente, usuarioId);

            Assert.Null(sesionObtenida);
        }

        [Fact]
        public void ObtenerDeberiaRetornarNullCuandoSesionExistePeroEsDeOtroUsuario()
        {
            int usuarioOriginalId = 1;
            int usuarioInvasorId = 2;
            var publicacionesIds = new List<int> { 10, 20 };

            var sesionCreada = _repositorio.Crear(usuarioOriginalId, publicacionesIds);

            var sesionObtenida = _repositorio.Obtener(sesionCreada.Id, usuarioInvasorId);

            Assert.Null(sesionObtenida);
        }
    }
}