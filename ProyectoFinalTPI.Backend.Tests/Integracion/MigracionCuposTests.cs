using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

public class MigracionCuposTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    [Fact]
    public async Task QueLaMigracionCuenteLosRecuerdosPreviosIncluidosOcultosSinContarCitas()
    {
        await using var factory = new PublicacionesApiFactory(postgres);
        using var cliente = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Usuarios.Add(new Usuario_Personal { Id = 77, Username = "historial", Email = "historial@example.test" });
        var lugar = new Lugar { Nombre = "Barrio", Coordenadas = new Point(-58, -34) { SRID = 4326 } };
        foreach (var id in new[] { 101, 102, 103, 104 }) db.Publicaciones.Add(new Publicacion
        {
            Id = id,
            UsuarioId = 77,
            Titulo = "Historia",
            Descripcion = "Historia",
            Lugar = lugar,
            Fecha = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            FechaCreación = new DateTime(2026, 11, id == 103 ? 3 : 10, 15, 0, 0, DateTimeKind.Utc),
            EstaOculto = id == 102,
            PublicacionOriginalId = id == 104 ? 101 : null
        });
        await db.SaveChangesAsync();
        var anterior = db.Database.GetMigrations().TakeWhile(m => !m.EndsWith("AplicarCategoriasYAdjuntos")).Last();
        var migrador = db.Database.GetService<IMigrator>();
        await migrador.MigrateAsync(anterior);
        await migrador.MigrateAsync();
        Assert.Equal(2, await db.Set<ConsumoPublicaciones>().Where(c => c.UsuarioId == 77 && c.SemanaInicio == new DateOnly(2026, 11, 8)).Select(c => c.Cantidad).SingleAsync());
        Assert.Equal(1, await db.Set<ConsumoPublicaciones>().Where(c => c.UsuarioId == 77 && c.SemanaInicio == new DateOnly(2026, 11, 1)).Select(c => c.Cantidad).SingleAsync());
    }
}