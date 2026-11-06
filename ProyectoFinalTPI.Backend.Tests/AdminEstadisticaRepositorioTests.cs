using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AdminEstadisticaRepositorioTests
{
    [Fact]
    public async Task QueCalculeTotalesYCategoriasDesdePostgres()
    {
        await using var context = CrearContexto();
        var usuario = new Usuario_Personal { Username = "autor", Email = "autor@test.com" };
        context.Publicaciones.AddRange(CrearPublicacion(usuario, CategoriaEnum.Cine, false),
            CrearPublicacion(usuario, CategoriaEnum.Cine, true));
        await context.SaveChangesAsync();
        var resultado = await new AdminEstadisticaRepositorio(context).Obtener(10);
        Assert.Equal(1, resultado.TotalUsuarios);
        Assert.Equal(2, resultado.TotalPublicaciones);
        Assert.Equal(1, resultado.PublicacionesVisibles);
        Assert.Equal(1, resultado.PublicacionesOcultas);
        Assert.Equal(2, Assert.Single(resultado.PublicacionesPorCategoria).Cantidad);
        Assert.Empty(resultado.MasGustadas);
    }

    private static Publicacion CrearPublicacion(Usuario_Interactivo usuario, CategoriaEnum categoria, bool oculta) => new()
    {
        Titulo = "Recuerdo", Descripcion = "Texto", Categoria = categoria, EstaOculto = oculta,
        Fecha = DateTime.UtcNow, Usuario = usuario,
        Lugar = new Lugar { Nombre = "Lugar", Coordenadas = new Point(0, 0) { SRID = 4326 } }
    };

    private static ApplicationDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
