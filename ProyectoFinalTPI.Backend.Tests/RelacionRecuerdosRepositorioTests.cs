using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class RelacionRecuerdosRepositorioTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly RelacionRecuerdosRepositorio _repositorio;

    public RelacionRecuerdosRepositorioTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repositorio = new RelacionRecuerdosRepositorio(_context);
    }

    [Fact]
    public async Task QueSePuedaObtenerElIdDeUnRecuerdoOriginalVisible()
    {
        //Arrange
        _context.Publicaciones.Add(new Publicacion { Id = 10, Titulo = "Recuerdo del Obelisco" });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        //Act
        var result = await _repositorio.ObtenerOriginalVisible(10, CancellationToken.None);

        //Assert
        Assert.Equal(10, result);
    }

    [Fact]
    public async Task QueNoSeObtengaUnRecuerdoQueNoExiste()
    {
        //Arrange
        var idInexistente = 99;

        //Act
        var result = await _repositorio.ObtenerOriginalVisible(idInexistente, CancellationToken.None);

        //Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task QueNoSeObtengaUnRecuerdoOcultoAunqueExista()
    {
        //Arrange
        _context.Publicaciones.Add(new Publicacion { Id = 10, EstaOculto = true });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        //Act
        var result = await _repositorio.ObtenerOriginalVisible(10, CancellationToken.None);

        //Assert
        Assert.Null(result);
        Assert.True(await _context.Publicaciones.AnyAsync(p => p.Id == 10));
    }

    [Fact]
    public async Task QueSeObtengaElRecuerdoOriginalAlConsultarUnRepostVisible()
    {
        //Arrange
        _context.Publicaciones.AddRange(
            new Publicacion { Id = 10, Titulo = "Original" },
            new Publicacion { Id = 25, PublicacionOriginalId = 10 });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        //Act
        var result = await _repositorio.ObtenerOriginalVisible(25, CancellationToken.None);

        //Assert
        Assert.Equal(10, result);
    }

    [Fact]
    public async Task QueNoSeObtengaElOriginalDeUnRepostOculto()
    {
        //Arrange
        _context.Publicaciones.AddRange(
            new Publicacion { Id = 10 },
            new Publicacion { Id = 25, PublicacionOriginalId = 10, EstaOculto = true });
        await _context.SaveChangesAsync();

        //Act
        var result = await _repositorio.ObtenerOriginalVisible(25, CancellationToken.None);

        //Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task QueNoSeObtengaUnOriginalOcultoDesdeUnRepostVisible()
    {
        //Arrange
        _context.Publicaciones.AddRange(
            new Publicacion { Id = 10, EstaOculto = true },
            new Publicacion { Id = 25, PublicacionOriginalId = 10 });
        await _context.SaveChangesAsync();

        //Act
        var result = await _repositorio.ObtenerOriginalVisible(25, CancellationToken.None);

        //Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task QueNoSeObtengaUnOriginalInexistenteDesdeUnRepost()
    {
        //Arrange
        // InMemory permite representar una referencia huérfana sin alterar la base real.
        _context.Publicaciones.Add(new Publicacion { Id = 25, PublicacionOriginalId = 99 });
        await _context.SaveChangesAsync();

        //Act
        var result = await _repositorio.ObtenerOriginalVisible(25, CancellationToken.None);

        //Assert
        Assert.Null(result);
    }

    // Las consultas SQL de Obtener requieren PostGIS y se verifican en
    // RelacionesRecuerdosIntegracionTests; InMemory no ejecuta ST_DWithin/ST_Distance.
    public void Dispose() => _context.Dispose();
}
