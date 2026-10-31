using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

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

    #region Obtención por Id y Existencia

    [Fact]
    public async Task QueObtenerPorIdAsyncRetorneUsuarioCuandoIdExiste()
    {
        //Arrange
        await SeedUsuariosAsync();

        //Act
        var resultado = await _repositorio.ObtenerPorIdAsync(1);

        //Assert
        Assert.NotNull(resultado);
        Assert.Equal(1, resultado.Id);
        Assert.Equal("usuario1", resultado.Username);
    }

    [Fact]
    public async Task QueObtenerPorIdAsyncRetorneNullCuandoIdNoExiste()
    {
        //Arrange
        await SeedUsuariosAsync();

        //Act
        var resultado = await _repositorio.ObtenerPorIdAsync(99);

        //Assert
        Assert.Null(resultado);
    }

    [Fact]
    public async Task QueExisteUsuarioRetorneTrueCuandoUsuarioExiste()
    {
        //Arrange
        await SeedUsuariosAsync();

        //Act
        var resultado = await _repositorio.ExisteUsuario(2);

        //Assert
        Assert.True(resultado);
    }

    [Fact]
    public async Task QueExisteUsuarioRetorneFalseCuandoUsuarioNoExiste()
    {
        //Arrange
        await SeedUsuariosAsync();

        //Act
        var resultado = await _repositorio.ExisteUsuario(99);

        //Assert
        Assert.False(resultado);
    }

    #endregion

    #region Consultas por Lote de Ids

    [Fact]
    public async Task QueObtenerUsuariosPorIdsAsyncLanceExcepcionCuandoListaEsNull()
    {
        //Arrange
        IReadOnlyCollection<int> ids = null!;

        //Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repositorio.ObtenerUsuariosPorIdsAsync(ids));
    }

    [Fact]
    public async Task QueObtenerUsuariosPorIdsAsyncRetorneListaVaciaCuandoListaEstaVacia()
    {
        //Arrange
        var ids = new List<int>();

        //Act
        var resultado = await _repositorio.ObtenerUsuariosPorIdsAsync(ids);

        //Assert
        Assert.NotNull(resultado);
        Assert.Empty(resultado);
    }

    [Fact]
    public async Task QueObtenerUsuariosPorIdsAsyncRetorneSoloUsuariosExistentes()
    {
        //Arrange
        await SeedUsuariosAsync();
        var idsBuscar = new List<int> { 1, 3, 99 };

        //Act
        var resultado = await _repositorio.ObtenerUsuariosPorIdsAsync(idsBuscar);

        //Assert
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, u => u.Id == 1 && u.Username == "usuario1");
        Assert.Contains(resultado, u => u.Id == 3 && u.Username == "usuario3");
        Assert.DoesNotContain(resultado, u => u.Id == 99);
    }

    [Fact]
    public async Task QueObtenerIdsExistentesAsyncLanceExcepcionCuandoListaEsNull()
    {
        //Arrange
        IReadOnlyCollection<int> ids = null!;

        //Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repositorio.ObtenerIdsExistentesAsync(ids));
    }

    [Fact]
    public async Task QueObtenerIdsExistentesAsyncRetorneListaVaciaCuandoListaEstaVacia()
    {
        //Arrange
        var ids = new List<int>();

        //Act
        var resultado = await _repositorio.ObtenerIdsExistentesAsync(ids);

        //Assert
        Assert.NotNull(resultado);
        Assert.Empty(resultado);
    }

    [Fact]
    public async Task QueObtenerIdsExistentesAsyncRetorneSoloIdsQueExisten()
    {
        //Arrange
        await SeedUsuariosAsync();
        var idsBuscar = new List<int> { 2, 4, 5, 1 };

        //Act
        var resultado = await _repositorio.ObtenerIdsExistentesAsync(idsBuscar);

        //Assert
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.Count);
        Assert.Contains(1, resultado);
        Assert.Contains(2, resultado);
        Assert.DoesNotContain(4, resultado);
        Assert.DoesNotContain(5, resultado);
    }

    #endregion

    #region Búsqueda y Existencia por Username

    [Fact]
    public async Task QueBuscarPorUsernameAsyncDevuelvaCoincidenciasParcialesSinDistinguirMayusculas()
    {
        //Arrange
        await SeedUsuariosAsync();

        //Act
        var resultado = await _repositorio.BuscarPorUsernameAsync("RIO", 10);

        //Assert
        Assert.Equal(3, resultado.Count);
        Assert.Equal(new[] { "usuario1", "usuario2", "usuario3" }, resultado.Select(usuario => usuario.Username));
    }

    [Fact]
    public async Task QueBuscarPorUsernameAsyncRespeteElLimite()
    {
        //Arrange
        await SeedUsuariosAsync();

        //Act
        var resultado = await _repositorio.BuscarPorUsernameAsync("usuario", 2);

        //Assert
        Assert.Equal(2, resultado.Count);
    }

    [Fact]
    public async Task QueExistePorUsernameAsyncRetorneTrueCuandoUsernameExiste()
    {
        //Arrange
        await SeedUsuariosAsync();

        //Act
        var resultado = await _repositorio.ExistePorUsernameAsync("usuario1");

        //Assert
        Assert.True(resultado);
    }

    [Fact]
    public async Task QueExistePorUsernameAsyncRetorneFalseCuandoUsernameNoExiste()
    {
        //Arrange
        await SeedUsuariosAsync();

        //Act
        var resultado = await _repositorio.ExistePorUsernameAsync("noexiste");

        //Assert
        Assert.False(resultado);
    }

    [Fact]
    public async Task QueExistePorUsernameAsyncSeaCaseInsensitive()
    {
        //Arrange
        await SeedUsuariosAsync();

        //Act
        var resultadoMayusculas = await _repositorio.ExistePorUsernameAsync("USUARIO1");
        var resultadoMixto = await _repositorio.ExistePorUsernameAsync("Usuario1");

        //Assert
        Assert.True(resultadoMayusculas);
        Assert.True(resultadoMixto);
    }

    [Fact]
    public async Task QueExistePorUsernameAsyncIgnoreEspaciosAlrededorDelUsername()
    {
        //Arrange
        await SeedUsuariosAsync();

        //Act
        var resultado = await _repositorio.ExistePorUsernameAsync("  usuario1  ");

        //Assert
        Assert.True(resultado);
    }

    [Fact]
    public async Task QueExistePorUsernameAsyncRetorneFalseCuandoNoHayUsuarios()
    {
        //Arrange & Act (sin seed — base de datos vacía)
        var resultado = await _repositorio.ExistePorUsernameAsync("cualquiera");

        //Assert
        Assert.False(resultado);
    }

    #endregion

    #region Métodos Auxiliares Privados

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

    #endregion
}
