using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public class RelacionesRecuerdosIntegracionTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    private static readonly DateTime Fecha = new(1986, 6, 29, 0, 0, 0, DateTimeKind.Utc);

    private static async Task Preparar(ApplicationDbContext db)
    {
        db.Usuarios.Add(new Usuario_Personal { Id = 1, Username = "autor", Email = "relaciones@test.com" });
        await db.SaveChangesAsync();
    }

    private static async Task<Publicacion> Agregar(ApplicationDbContext db, double metros, DateTime? fecha = null,
        bool oculto = false, int? original = null)
    {
        var p = new Publicacion
        {
            Titulo = "Historia", Descripcion = "Una mirada del acontecimiento", UsuarioId = 1,
            Fecha = fecha ?? Fecha, EstaOculto = oculto, PublicacionOriginalId = original,
            Lugar = new Lugar { Nombre = "Obelisco", Coordenadas = new Point(-58.3816, -34.6037) { SRID = 4326 } }
        };
        db.Publicaciones.Add(p);
        await db.SaveChangesAsync();
        // Desplazamiento geodésico real hacia el este, calculado por PostGIS.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE lugares SET "Coordenadas" = ST_Project(
                ST_SetSRID(ST_MakePoint(-58.3816, -34.6037), 4326)::geography, {metros}, pi()/2)::geometry
            WHERE "Id" = {p.LugarId}
            """);
        return p;
    }

    [Fact]
    public async Task QueSeIncluyanLosRecuerdosAExactamenteDoscientosMetrosYSePuedanPaginar()
    {
        //Arrange
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await Preparar(db);
        var origen = await Agregar(db, 0);
        var enElLimite = await Agregar(db, 200);
        var otroEnElLimite = await Agregar(db, 200);
        await Agregar(db, 200.001);
        // Comprobar la distancia almacenada, no solo el argumento enviado al helper.
        var distanciaReal = await db.Database.SqlQuery<double>($"""
            SELECT ST_Distance(a."Coordenadas"::geography, b."Coordenadas"::geography) AS "Value"
            FROM lugares a, lugares b
            WHERE a."Id" = {origen.LugarId} AND b."Id" = {enElLimite.LugarId}
            """).SingleAsync();

        //Act
        var resumen = await client.GetFromJsonAsync<RelacionesRecuerdoDto>(
            $"/api/publicaciones/{origen.Id}/relaciones");
        var primera = await client.GetFromJsonAsync<ResultadoPaginadoDto<RecuerdoRelacionadoDto>>(
            $"/api/publicaciones/{origen.Id}/relacionadas?tipo=dia&limite=1");
        Assert.NotNull(primera);
        Assert.NotNull(primera.SiguienteCursor);
        var segunda = await client.GetFromJsonAsync<ResultadoPaginadoDto<RecuerdoRelacionadoDto>>(
            $"/api/publicaciones/{origen.Id}/relacionadas?tipo=dia&limite=1&cursor={Uri.EscapeDataString(primera.SiguienteCursor)}");

        //Assert
        Assert.Equal(200d, distanciaReal);
        Assert.NotNull(resumen);
        Assert.Equal(new[] { enElLimite.Id, otroEnElLimite.Id }, resumen.Dia.Items.Select(p => p.Id));
        Assert.Equal(new[] { enElLimite.Id, otroEnElLimite.Id }, resumen.Lugar.Items.Select(p => p.Id));
        Assert.All(resumen.Dia.Items, p => Assert.Equal(200d, p.DistanciaMetros));
        Assert.Equal(enElLimite.Id, Assert.Single(primera.Items).Id);
        Assert.NotNull(segunda);
        Assert.Equal(otroEnElLimite.Id, Assert.Single(segunda.Items).Id);
        Assert.Null(segunda.SiguienteCursor);
    }

    [Fact]
    public async Task LugarYDia_RespetanMetrosFechaCompletaVisibilidadYNoSonTransitivos()
    {
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await Preparar(db);
        var origen = await Agregar(db, 0);
        var cercano = await Agregar(db, 100, Fecha.AddHours(23));
        var limiteInterior = await Agregar(db, 199.999);
        await Agregar(db, 200.001);
        await Agregar(db, 280); // Cerca del recuerdo a 100 m, fuera del radio del origen.
        var otroAnio = await Agregar(db, 50, Fecha.AddYears(1));
        await Agregar(db, 30, oculto: true);
        var repost = await Agregar(db, 0, original: origen.Id);
        var resumen = await client.GetFromJsonAsync<RelacionesRecuerdoDto>($"/api/publicaciones/{origen.Id}/relaciones");
        Assert.NotNull(resumen);
        Assert.Equal(new[] { cercano.Id, limiteInterior.Id }, resumen.Dia.Items.Select(p => p.Id));
        Assert.Equal(new[] { otroAnio.Id, cercano.Id, limiteInterior.Id }, resumen.Lugar.Items.Select(p => p.Id));
        Assert.All(resumen.Lugar.Items, p => Assert.InRange(p.DistanciaMetros, 0, 200));
        Assert.False(resumen.Lugar.Items[0].MismoDia);
        Assert.True(resumen.Dia.Items[0].MismoDia);
        var desdeRepost = await client.GetFromJsonAsync<RelacionesRecuerdoDto>($"/api/publicaciones/{repost.Id}/relaciones");
        Assert.Equal(origen.Id, desdeRepost!.PublicacionId);
        Assert.Equal(resumen.Lugar.Items.Select(p => p.Id), desdeRepost.Lugar.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task Cursor_NoPierdeEmpatesYValidaContextoYParametros()
    {
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await Preparar(db);
        var origen = await Agregar(db, 0);
        var esperados = new List<int>();
        for (var i = 0; i < 8; i++) esperados.Add((await Agregar(db, 25)).Id);
        var ids = new List<int>();
        string? cursor = null;
        do
        {
            var pagina = await client.GetFromJsonAsync<ResultadoPaginadoDto<RecuerdoRelacionadoDto>>(
                $"/api/publicaciones/{origen.Id}/relacionadas?tipo=dia&limite=3" +
                (cursor == null ? "" : $"&cursor={Uri.EscapeDataString(cursor)}"));
            Assert.NotNull(pagina);
            ids.AddRange(pagina.Items.Select(p => p.Id));
            cursor = pagina.SiguienteCursor;
        } while (cursor != null);
        Assert.Equal(esperados, ids);
        var resumen = await client.GetFromJsonAsync<RelacionesRecuerdoDto>($"/api/publicaciones/{origen.Id}/relaciones");
        Assert.Equal(6, resumen!.Dia.Items.Count);
        Assert.True(resumen.Dia.HayMas);
        foreach (var query in new[] { "tipo=invalido", "limite=0", "limite=51", "cursor=basura",
            "tipo=lugar&cursor=" + Uri.EscapeDataString(resumen.Dia.SiguienteCursor!) })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/publicaciones/{origen.Id}/relacionadas?{query}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(
            $"/api/publicaciones/{esperados[0]}/relacionadas?tipo=dia&cursor={Uri.EscapeDataString(resumen.Dia.SiguienteCursor!)}")).StatusCode);
    }

    [Fact]
    public async Task OrigenOcultoInexistenteORepostDeOculto_NoExponeRelaciones()
    {
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await Preparar(db);
        var oculto = await Agregar(db, 0, oculto: true);
        var repost = await Agregar(db, 0, original: oculto.Id);
        foreach (var id in new[] { oculto.Id, repost.Id, int.MaxValue })
        foreach (var ruta in new[] { "relaciones", "relacionadas?tipo=dia" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/publicaciones/{id}/{ruta}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/publicaciones/0/relaciones")).StatusCode);
    }
}
