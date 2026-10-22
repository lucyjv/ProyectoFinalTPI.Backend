using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests
{
    public class UsuarioRepositorioTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly UsuarioRepositorio _repositorio;

        public UsuarioRepositorioTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _repositorio = new UsuarioRepositorio(_context);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private async Task SeedUsuariosAsync()
        {
            _context.Usuarios.AddRange(
                new Usuario_Personal
                {
                    Id = 1,
                    Username = "usuario1",
                    Email = "user1@test.com",
                    Contraseña = "password123",
                    Nombre = "Juan",
                    Apellido = "Pérez",
                    FechaNacimiento = new DateOnly(1990, 1, 1)
                },
                new Usuario_Personal
                {
                    Id = 2,
                    Username = "usuario2",
                    Email = "user2@test.com",
                    Contraseña = "password123",
                    Nombre = "Ana",
                    Apellido = "Gómez",
                    FechaNacimiento = new DateOnly(1992, 5, 10)
                },
                new Usuario_Personal
                {
                    Id = 3,
                    Username = "usuario3",
                    Email = "user3@test.com",
                    Contraseña = "password123",
                    Nombre = "Carlos",
                    Apellido = "López",
                    FechaNacimiento = new DateOnly(1988, 10, 20)
                }
            );

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
        }


        [Fact]
        public async Task ObtenerPorIdAsyncDeberiaRetornarUsuarioCuandoIdExiste()
        {
            await SeedUsuariosAsync();

            var resultado = await _repositorio.ObtenerPorIdAsync(1);

            Assert.NotNull(resultado);
            Assert.Equal(1, resultado.Id);
            Assert.Equal("usuario1", resultado.Username);
        }

        [Fact]
        public async Task ObtenerPorIdAsyncDeberiaRetornarNullCuandoIdNoExiste()
        {
            await SeedUsuariosAsync();

            var resultado = await _repositorio.ObtenerPorIdAsync(99);

            Assert.Null(resultado);
        }


        [Fact]
        public async Task ExisteUsuarioDeberiaRetornarTrueCuandoUsuarioExiste()
        {
            await SeedUsuariosAsync();

            var resultado = await _repositorio.ExisteUsuario(2);

            Assert.True(resultado);
        }

        [Fact]
        public async Task ExisteUsuarioDeberiaRetornarFalseCuandoUsuarioNoExiste()
        {
            await SeedUsuariosAsync();

            var resultado = await _repositorio.ExisteUsuario(99);

            Assert.False(resultado);
        }


        [Fact]
        public async Task ObtenerUsuariosPorIdsAsyncDeberiaLanzarExcepcionCuandoListaEsNull()
        {
            IReadOnlyCollection<int> ids = null!;

            await Assert.ThrowsAsync<ArgumentNullException>(() => _repositorio.ObtenerUsuariosPorIdsAsync(ids));
        }

        [Fact]
        public async Task ObtenerUsuariosPorIdsAsyncDeberiaRetornarListaVaciaCuandoListaEstaVacia()
        {
            var ids = new List<int>();

            var resultado = await _repositorio.ObtenerUsuariosPorIdsAsync(ids);

            Assert.NotNull(resultado);
            Assert.Empty(resultado);
        }

        [Fact]
        public async Task ObtenerUsuariosPorIdsAsyncDeberiaRetornarSoloUsuariosExistentes()
        {
            await SeedUsuariosAsync();
            var idsBuscar = new List<int> { 1, 3, 99 }; 

            var resultado = await _repositorio.ObtenerUsuariosPorIdsAsync(idsBuscar);

            Assert.NotNull(resultado);
            Assert.Equal(2, resultado.Count);

            Assert.Contains(resultado, u => u.Id == 1 && u.Username == "usuario1");
            Assert.Contains(resultado, u => u.Id == 3 && u.Username == "usuario3");
            Assert.DoesNotContain(resultado, u => u.Id == 99);
        }


        [Fact]
        public async Task ObtenerIdsExistentesAsyncDeberiaLanzarExcepcionCuandoListaEsNull()
        {
            IReadOnlyCollection<int> ids = null!;

            await Assert.ThrowsAsync<ArgumentNullException>(() => _repositorio.ObtenerIdsExistentesAsync(ids));
        }

        [Fact]
        public async Task ObtenerIdsExistentesAsyncDeberiaRetornarListaVaciaCuandoListaEstaVacia()
        {
            var ids = new List<int>();

            var resultado = await _repositorio.ObtenerIdsExistentesAsync(ids);

            Assert.NotNull(resultado);
            Assert.Empty(resultado);
        }

        [Fact]
        public async Task ObtenerIdsExistentesAsyncDeberiaRetornarSoloIdsQueExisten()
        {
            await SeedUsuariosAsync();
            var idsBuscar = new List<int> { 2, 4, 5, 1 }; 

            var resultado = await _repositorio.ObtenerIdsExistentesAsync(idsBuscar);

            Assert.NotNull(resultado);
            Assert.Equal(2, resultado.Count);

            Assert.Contains(1, resultado);
            Assert.Contains(2, resultado);
            Assert.DoesNotContain(4, resultado);
            Assert.DoesNotContain(5, resultado);
        }

        [Fact]
        public async Task BuscarPorUsernameAsyncDevuelveCoincidenciasParcialesSinDistinguirMayusculas()
        {
            await SeedUsuariosAsync();

            var resultado = await _repositorio.BuscarPorUsernameAsync("RIO", 10);

            Assert.Equal(3, resultado.Count);
            Assert.Equal(new[] { "usuario1", "usuario2", "usuario3" }, resultado.Select(usuario => usuario.Username));
        }

        [Fact]
        public async Task BuscarPorUsernameAsyncRespetaElLimite()
        {
            await SeedUsuariosAsync();

            var resultado = await _repositorio.BuscarPorUsernameAsync("usuario", 2);

            Assert.Equal(2, resultado.Count);
        }
    }
}
