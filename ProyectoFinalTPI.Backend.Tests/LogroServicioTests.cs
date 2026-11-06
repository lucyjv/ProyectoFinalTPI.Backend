using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class LogroServicioTests
{
    #region Catálogo de Logros

    [Fact]
    public async Task QueCatalogoNuevoTengaDiecisieteLogrosBloqueados()
    {
        //Arrange
        await using var db = await CrearContexto();
        var servicio = new LogroServicio(new LogrosUsuariosRepositorio(db));

        //Act
        var catalogo = await servicio.ObtenerCatalogoParaUsuario(1);

        //Assert
        Assert.Equal(Enumerable.Range(1, 20).Except(new[] { 4, 5, 17 }), catalogo.Select(logro => logro.Id));
        Assert.All(catalogo, logro => { Assert.False(logro.Desbloqueado); Assert.Null(logro.FechaDesbloqueo); });
        Assert.Equal("Primera Palabra", catalogo.Single(logro => logro.Id == 2).Titulo);
    }

    #endregion

    #region Perfil y Desbloqueos

    [Fact]
    public async Task QuePerfilMuestreSoloLogrosDelUsuarioYNoDupliqueDesbloqueos()
    {
        //Arrange
        await using var db = await CrearContexto();
        var servicio = new LogroServicio(new LogrosUsuariosRepositorio(db));
        await servicio.VerificarYDarLogro(1, LogroEnum.PrimeraPalabra);
        await servicio.VerificarYDarLogro(1, LogroEnum.PrimeraPalabra);
        var usuarios = new UsuarioRepositorio(db);
        var perfiles = new UsuarioServicio(usuarios, servicio);

        //Act
        var propio = await perfiles.ObtenerPerfilPorIdAsync(1, 1);
        var otro = await perfiles.ObtenerPerfilPorIdAsync(2, 1);

        //Assert
        var logro = Assert.Single(propio.Logros, l => l.Desbloqueado);
        Assert.Equal(2, logro.Id);
        Assert.NotNull(logro.FechaDesbloqueo);
        Assert.All(otro.Logros, l => Assert.False(l.Desbloqueado));
        Assert.Equal(1, await db.Logros_Usuarios.CountAsync());
    }

    #endregion

    #region Métodos Auxiliares Privados

    private static async Task<ApplicationDbContext> CrearContexto()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await db.Database.EnsureCreatedAsync();
        db.Usuarios.AddRange(new Usuario_Personal { Id = 1, Username = "uno", Email = "uno@example.test" },
            new Usuario_Personal { Id = 2, Username = "dos", Email = "dos@example.test" });
        await db.SaveChangesAsync();
        return db;
    }

    #endregion
}
