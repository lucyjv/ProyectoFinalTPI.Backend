using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public class GeminiIntegracionTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    private static ResultadoAnalisisRecuerdoDto Resultado(int anio) => new([], Propuestas:
        [new(0, $"Argentina campeón del mundo {anio}", anio, [], 0.95, $"Argentina campeón del mundo {anio}")]);

    private PublicacionesApiFactory CrearFactory(HttpGeminiFake? http = null) => new(postgres)
    {
        ConfigurarServiciosAdicionales = servicios =>
        {
            if (http == null) return;
            servicios.Configure<OpcionesGemini>(o => { o.Habilitado = true; o.ApiKey = "clave-ficticia"; o.IntervaloMinimoSegundos = 1; });
            servicios.AddHttpClient<AnalizadorRecuerdoGemini>().ConfigurePrimaryHttpMessageHandler(() => http);
        }
    };

    private static async Task Preparar(PublicacionesApiFactory factory, int segundoAnio = 1986)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Usuarios.Add(new Usuario_Personal { Id = 7, Username = "autor", Email = "gemini@test.com" });
        db.Lugares.Add(new Lugar { Id = 1, Nombre = "Obelisco", Coordenadas = new Point(-58.3816, -34.6037) { SRID = 4326 } });
        db.Publicaciones.AddRange(new Publicacion { Id = 10, UsuarioId = 7, LugarId = 1, Titulo = "Argentina campeón del mundo 1986", Fecha = new DateTime(1986, 6, 29, 0, 0, 0, DateTimeKind.Utc) },
            new Publicacion { Id = 11, UsuarioId = 7, LugarId = 1, Titulo = $"Argentina campeón del mundo {segundoAnio}", Fecha = new DateTime(segundoAnio, 7, 1, 0, 0, 0, DateTimeKind.Utc) });
        await db.SaveChangesAsync();
    }

    private static async Task Procesar(PublicacionesApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<IAnalisisRecuerdosServicio>().ProcesarUno(CancellationToken.None));
    }

    [Theory]
    [InlineData(1986, 1)]
    [InlineData(2022, 2)]
    public async Task QueElProveedorSeIntegreConLaColaElCatalogoYLosEndpointsExistentes(int segundoAnio, int cantidadMomentos)
    {
        //Arrange
        var http = new HttpGeminiFake([1986, segundoAnio]);
        await using var factory = CrearFactory(http);
        using var cliente = factory.CreateClient();
        await Preparar(factory, segundoAnio);

        //Act
        await Procesar(factory);
        await Procesar(factory);
        var estado = await cliente.GetFromJsonAsync<EstadoAnalisisRecuerdoDto>("/api/publicaciones/11/analisis");
        using var respuesta = await cliente.GetAsync("/api/publicaciones/11/momentos");

        //Assert
        Assert.Equal("completado", estado!.Estado);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains($"{segundoAnio}", await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(2, http.Llamadas);
        Assert.Contains("Argentina", http.Solicitudes[1]);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(cantidadMomentos, await db.Momentos.CountAsync());
        Assert.Equal(2, await db.PublicacionesMomentos.CountAsync());
        Assert.All(await db.PublicacionesMomentos.ToListAsync(), a => Assert.Equal(OrigenAsociacionMomento.Automatico, a.Origen));
    }

    [Fact]
    public async Task QueDosConfirmacionesConcurrentesNoDupliquenElMomentoCanonico()
    {
        //Arrange
        await using var factory = CrearFactory();
        using var cliente = factory.CreateClient();
        await Preparar(factory);
        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();
        var repo1 = scope1.ServiceProvider.GetRequiredService<ITrabajoAnalisisRepositorio>();
        var repo2 = scope2.ServiceProvider.GetRequiredService<ITrabajoAnalisisRepositorio>();
        var ahora = DateTime.UtcNow;
        var reserva1 = (await repo1.Reservar(ahora, TimeSpan.FromMinutes(2), 3, CancellationToken.None))!;
        var reserva2 = (await repo2.Reservar(ahora, TimeSpan.FromMinutes(2), 3, CancellationToken.None))!;

        //Act
        var resultados = await Task.WhenAll(repo1.Completar(reserva1, Resultado(1986), "test-v1", ahora, CancellationToken.None),
            repo2.Completar(reserva2, Resultado(1986), "test-v1", ahora, CancellationToken.None));

        //Assert
        Assert.All(resultados, Assert.True);
        using var consulta = factory.Services.CreateScope();
        var db = consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.Momentos.CountAsync());
        Assert.Equal(2, await db.PublicacionesMomentos.CountAsync());
        Assert.Equal(1, await db.MomentosAlias.CountAsync());
    }

    [Fact]
    public async Task QueUnFalloAlAsociarReviertaTambienElMomentoYLosAliasCreados()
    {
        //Arrange
        await using var factory = CrearFactory(new HttpGeminiFake([1986]));
        using var cliente = factory.CreateClient();
        await Preparar(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION fallar_asociacion_test() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'fallo de prueba al asociar'; END; $$;
                CREATE TRIGGER fallar_asociacion BEFORE INSERT ON publicaciones_momentos
                FOR EACH ROW EXECUTE FUNCTION fallar_asociacion_test();
                """);
        }

        //Act
        await Procesar(factory);

        //Assert
        using var consulta = factory.Services.CreateScope();
        var dbConsulta = consulta.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await dbConsulta.Momentos.ToListAsync());
        Assert.Empty(await dbConsulta.MomentosAlias.ToListAsync());
        Assert.Empty(await dbConsulta.PublicacionesMomentos.ToListAsync());
        Assert.Equal(EstadoTrabajoAnalisis.Pendiente, (await dbConsulta.TrabajosAnalisisRecuerdos.SingleAsync(j => j.PublicacionId == 10)).Estado);
    }

    private sealed class HttpGeminiFake(int[] anios) : HttpMessageHandler
    {
        public int Llamadas;
        public List<string> Solicitudes { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Solicitudes.Add(await request.Content!.ReadAsStringAsync(ct));
            var resultado = Resultado(anios[Llamadas++]);
            var texto = JsonSerializer.Serialize(new { propuestas = resultado.Propuestas }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var cuerpo = JsonSerializer.Serialize(new { candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text = texto } } } } } });
            return new(HttpStatusCode.OK) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };
        }
    }
}
