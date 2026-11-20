using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Entidades.ValueObjects;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public sealed class RankingMusicaRepositorioTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    private static readonly DateTimeOffset Apertura = new(2026, 4, 1, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Cierre = Apertura.AddDays(15);
    private static readonly Decada AñosOchenta = new(1980);

    [Fact]
    public async Task QueDosVotosCompartanElCupoYElSaldoVenzaAMedianocheEnBuenosAires()
    {
        var reloj = new Reloj(Apertura.AddDays(1).AddMinutes(-1));
        await using var factory = new PublicacionesApiFactory(postgres, timeProvider: reloj);
        await factory.PrepararUsuario();
        await using var scope = factory.Services.CreateAsyncScope();
        var ranking = scope.ServiceProvider.GetRequiredService<IRankingMusicaRepositorio>();
        var consumo = scope.ServiceProvider.GetRequiredService<IConsumoDiarioPuntosNostalgiaRepositorio>();
        var ronda = await CrearRonda(ranking);
        var primera = await ranking.AgregarCandidatura(ronda.Id, 1, AñosOchenta, "Una canción", "Una banda");
        var segunda = await ranking.AgregarCandidatura(ronda.Id, 1, AñosOchenta, "Otra canción", "Una banda");

        var resultados = await Task.WhenAll(Votar(primera.Id), Votar(segunda.Id));

        Assert.Single(resultados, resultado => resultado?.PuntosDiariosRestantes == 1);
        Assert.Single(resultados, resultado => resultado is null);
        Assert.Equal(4, await consumo.ObtenerPuntosGastados(1, new DateOnly(2026, 4, 1)));
        Assert.Equal(4L, (await ranking.ListarCandidaturas(ronda.Id)).Sum(fila => fila.Puntos));

        reloj.Ahora = Apertura.AddDays(1);
        var voto = await ranking.RegistrarVoto(primera.Id, 1, 1, 5);
        Assert.Equal(4, voto.PuntosDiariosRestantes);
        Assert.Equal(1, await consumo.ObtenerPuntosGastados(1, new DateOnly(2026, 4, 2)));

        async Task<VotoRankingMusicaRegistrado?> Votar(int candidaturaId)
        {
            await using var scopeVoto = factory.Services.CreateAsyncScope();
            var repositorio = scopeVoto.ServiceProvider.GetRequiredService<IRankingMusicaRepositorio>();
            try
            {
                return await repositorio.RegistrarVoto(candidaturaId, 1, 4, 5);
            }
            catch (CupoDiarioPuntosNostalgiaAgotadoException ex)
            {
                Assert.Equal(1, ex.PuntosRestantes);
                return null;
            }
        }
    }

    [Fact]
    public async Task QueSoloElCreadorOAdminPuedanCorregirYElRetiroConserveLosVotosYElGasto()
    {
        var reloj = new Reloj(Apertura);
        await using var factory = new PublicacionesApiFactory(postgres, timeProvider: reloj);
        await factory.PrepararUsuario();
        await using var scope = factory.Services.CreateAsyncScope();
        var ranking = scope.ServiceProvider.GetRequiredService<IRankingMusicaRepositorio>();
        var consumo = scope.ServiceProvider.GetRequiredService<IConsumoDiarioPuntosNostalgiaRepositorio>();
        var contexto = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ronda = await CrearRonda(ranking);
        var candidatura = await ranking.AgregarCandidatura(ronda.Id, 1, AñosOchenta, "Mi canción", "Banda");
        await Assert.ThrowsAsync<ConflictoRankingMusicaException>(() =>
            ranking.AgregarCandidatura(ronda.Id, 1, AñosOchenta, " mi  canción ", "BANDA"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            ranking.CorregirCandidatura(candidatura.Id, 2, false, "Título", "Banda"));

        var corregida = await ranking.CorregirCandidatura(candidatura.Id, 1, false, "Título", "Banda");
        Assert.Equal("Título", corregida.Titulo);
        await ranking.RegistrarVoto(candidatura.Id, 1, 2, 5);
        await Assert.ThrowsAsync<ConflictoRankingMusicaException>(() =>
            ranking.CorregirCandidatura(candidatura.Id, 2, true, "Otro título", "Banda"));

        await ranking.RetirarCandidatura(candidatura.Id, 2, true);
        Assert.Empty(await ranking.ListarCandidaturas(ronda.Id));
        Assert.Equal(2, await consumo.ObtenerPuntosGastados(1, new DateOnly(2026, 4, 1)));
        Assert.Equal(2, (await contexto.Set<VotoRankingMusica>().AsNoTracking().SingleAsync()).Puntos);
    }

    [Fact]
    public async Task QueElCorteRechaceVotosYDosCierresGuardenUnaSolaRondaConClasificadas()
    {
        var reloj = new Reloj(Apertura);
        await using var factory = new PublicacionesApiFactory(postgres, timeProvider: reloj);
        await factory.PrepararUsuario();
        await using var scope = factory.Services.CreateAsyncScope();
        var ranking = scope.ServiceProvider.GetRequiredService<IRankingMusicaRepositorio>();
        var ronda = await CrearRonda(ranking);
        var candidaturas = new List<CandidaturaRankingMusica>();
        for (var indice = 1; indice <= 7; indice++)
        {
            var candidatura = await ranking.AgregarCandidatura(ronda.Id, 1, AñosOchenta, $"Canción {indice}", "Banda");
            candidaturas.Add(candidatura);
            await ranking.RegistrarVoto(candidatura.Id, 1, 1, 10);
        }
        await ranking.AgregarCandidatura(ronda.Id, 1, new Decada(1990), "Sin votos", "Banda");
        reloj.Ahora = Cierre;

        await Assert.ThrowsAsync<ConflictoRankingMusicaException>(() =>
            ranking.RegistrarVoto(candidaturas[0].Id, 1, 1, 10));
        await using var otroScope = factory.Services.CreateAsyncScope();
        var otroRanking = otroScope.ServiceProvider.GetRequiredService<IRankingMusicaRepositorio>();
        var cierres = await Task.WhenAll(ranking.CerrarRonda(ronda.Id, Cierre.AddDays(15)),
            otroRanking.CerrarRonda(ronda.Id, Cierre.AddDays(15)));
        Assert.Single(cierres, cerrado => cerrado);

        var anterior = Assert.IsType<RondaMusicaConResultados>(await ranking.ObtenerResultados(ronda.Id));
        Assert.Equal(EstadoRankingMusica.Cerrado, anterior.Ronda.Estado);
        Assert.Equal(candidaturas.Take(6).Select(fila => fila.Id), anterior.Posiciones.Select(fila => fila.Resultado.CandidaturaId));
        Assert.Equal(Enumerable.Range(1, 6), anterior.Posiciones.Select(fila => fila.Resultado.Posicion));
        Assert.Equal(ronda.Id, (await ranking.ObtenerResultados())!.Ronda.Id);

        var siguiente = Assert.IsType<RankingMusica>(await ranking.ObtenerRondaAbierta());
        Assert.Equal(Cierre, siguiente.Apertura);
        var clasificadas = await ranking.ListarCandidaturas(siguiente.Id);
        Assert.Equal(candidaturas.Skip(3).Take(3).Select(fila => fila.Titulo), clasificadas.Select(fila => fila.Candidatura.Titulo));
        Assert.All(clasificadas, fila => Assert.Equal(0L, fila.Puntos));
    }

    private static async Task<RankingMusica> CrearRonda(IRankingMusicaRepositorio ranking)
    {
        Assert.True(await ranking.CrearRondaInicial(Apertura, Cierre));
        Assert.False(await ranking.CrearRondaInicial(Apertura, Cierre));
        return Assert.IsType<RankingMusica>(await ranking.ObtenerRondaAbierta());
    }

    private sealed class Reloj(DateTimeOffset ahora) : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = ahora;
        public override DateTimeOffset GetUtcNow() => Ahora;
    }
}
