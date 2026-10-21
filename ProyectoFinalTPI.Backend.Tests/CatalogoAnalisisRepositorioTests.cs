using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class CatalogoAnalisisRepositorioTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task QueSeBusquenCandidatosPorNombreYAliasSinEnviarPublicacionesAlProveedor()
    {
        //Arrange
        _context.Momentos.AddRange(new Momento { Id = 1, Nombre = "Argentina campeón 1986", Alias = [new() { Texto = "México 86", TextoNormalizado = "mexico 86" }] },
            new Momento { Id = 2, Nombre = "Recital de Soda Stereo 1997" });
        await _context.SaveChangesAsync();
        var repositorio = new CatalogoAnalisisRepositorio(_context);

        //Act
        var result = await repositorio.BuscarCandidatos("Mi recuerdo de MÉXICO 86", CancellationToken.None);

        //Assert
        Assert.Equal(1, Assert.Single(result).Id);
        Assert.Contains("México 86", result[0].Alias);
    }

    [Fact]
    public async Task QueElCatalogoSeLimiteACuarentaCandidatosYOchoAliasPorMomento()
    {
        //Arrange
        for (var i = 1; i <= 45; i++) _context.Momentos.Add(new Momento
        {
            Id = i, Nombre = $"Mundial {i}", Alias = Enumerable.Range(1, 12)
                .Select(a => new MomentoAlias { Texto = $"Alias {a}", TextoNormalizado = $"alias {a}" }).ToList()
        });
        await _context.SaveChangesAsync();
        var repositorio = new CatalogoAnalisisRepositorio(_context);

        //Act
        var result = await repositorio.BuscarCandidatos("Mundial", CancellationToken.None);

        //Assert
        Assert.Equal(40, result.Count);
        Assert.All(result, m => Assert.Equal(8, m.Alias.Length));
        Assert.Equal(1, result[0].Id);
        Assert.Equal(40, result[^1].Id);
    }

    public void Dispose() => _context.Dispose();
}
