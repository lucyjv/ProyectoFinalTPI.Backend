using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.RankingMusica;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Entidades.ValueObjects;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class RankingMusicaServicio(
    IRankingMusicaRepositorio repositorio, ICategoriaServicio categorias, ICupoPuntosNostalgiaServicio puntosNostalgia,
    TimeProvider reloj, IOptions<RankingMusicaOptions> opciones,
    ILogger<RankingMusicaServicio>? logger = null) : IRankingMusicaServicio
{
    private const int CantidadGanadorasPorDecada = 3;

    public async Task<RankingMusicaActualDto?> ObtenerActual(int? usuarioId = null, CancellationToken ct = default)
    {
        var ronda = await ObtenerRondaVigente(ct);
        if (ronda is null)
            return null;

        var candidaturas = await repositorio.ListarCandidaturas(ronda.Id, ct);
        var decadas = AgruparCandidaturasPorDecada(candidaturas);
        SaldoDiarioPuntosNostalgiaDto? saldo = null;
        if (usuarioId.HasValue)
        {
            try
            {
                saldo = await puntosNostalgia.ObtenerSaldo(usuarioId.Value, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && ex is DbException or TimeoutException)
            {
                logger?.LogError(ex,
                    "No se pudo consultar el saldo del usuario {UsuarioId}. Se devuelve el ranking de música con saldo no disponible.",
                    usuarioId.Value);
            }
        }
        return new(ronda.Id, ronda.Apertura, ronda.Cierre, decadas, saldo);
    }

    public async Task<ResultadosRankingMusicaDto?> ObtenerResultados(int? rankingMusicaId = null, CancellationToken ct = default)
    {
        if (rankingMusicaId.HasValue)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rankingMusicaId.Value, nameof(rankingMusicaId));

        var datos = await repositorio.ObtenerResultados(rankingMusicaId, ct);
        return datos is null ? null : MapearResultados(datos);
    }

    public async Task<CandidaturaRankingMusicaDto> SugerirMusica(
        int usuarioId, int decada, string titulo, string artista, CancellationToken ct = default)
    {
        await categorias.ExigirPermiso(usuarioId, PermisoCuenta.SugerirMusica, ct);
        return await AgregarCandidatura(usuarioId, decada, titulo, artista, ct);
    }

    public Task<CandidaturaRankingMusicaDto> AgregarMusicaComoAdministrador(
        int usuarioId, bool esAdministrador, int decada, string titulo, string artista, CancellationToken ct = default)
    {
        if (!esAdministrador)
            throw new UnauthorizedAccessException("Se requieren permisos de administrador.");

        return AgregarCandidatura(usuarioId, decada, titulo, artista, ct);
    }

    public async Task<CandidaturaRankingMusicaDto> EditarCandidatura(
        int candidaturaId, int usuarioId, bool esAdministrador, string titulo, string artista, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(candidaturaId);
        var candidatura = await repositorio.CorregirCandidatura(candidaturaId, usuarioId, esAdministrador, titulo, artista, ct);
        return MapearCandidatura(candidatura, 0);
    }

    public Task RetirarCandidatura(int candidaturaId, int usuarioId, bool esAdministrador, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(candidaturaId);
        return repositorio.RetirarCandidatura(candidaturaId, usuarioId, esAdministrador, ct);
    }

    public async Task<VotoRankingMusicaDto> PuntuarMusica(int candidaturaId, int usuarioId, int puntos, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(candidaturaId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(puntos);
        var cupo = await puntosNostalgia.ObtenerCupoParaPuntuar(usuarioId, PermisoCuenta.VotarCancion, ct);
        var voto = await repositorio.RegistrarVoto(candidaturaId, usuarioId, puntos, cupo, ct);
        return new(voto.VotoId, voto.PuntosTotales, voto.PuntosDiariosRestantes);
    }

    public async Task<DateTimeOffset?> ProcesarRonda(CancellationToken ct = default)
    {
        var ronda = await repositorio.ObtenerRondaAbierta(ct);
        var ahora = reloj.GetUtcNow();
        if (ronda is null)
            return await CrearPrimeraRonda(ahora, ct);

        if (ahora < ronda.Cierre)
            return ronda.Cierre;

        var cierreSiguiente = CalcularCierre(CalendarioNostalgia.FechaLocal(ronda.Cierre));
        var cerrada = await repositorio.CerrarRonda(ronda.Id, cierreSiguiente, ct);
        return cerrada ? cierreSiguiente : null;
    }

    private async Task<DateTimeOffset?> CrearPrimeraRonda(DateTimeOffset ahora, CancellationToken ct)
    {
        var inicio = opciones.Value.FechaInicio ?? CalendarioNostalgia.FechaLocal(ahora);
        var cierre = CalcularCierre(inicio);
        var creada = await repositorio.CrearRondaInicial(CalendarioNostalgia.InicioDelDia(inicio), cierre, ct);
        return creada ? cierre : null;
    }

    private DateTimeOffset CalcularCierre(DateOnly inicio) =>
        CalendarioNostalgia.InicioDelDia(inicio.AddDays(opciones.Value.DuracionDias));

    private async Task<CandidaturaRankingMusicaDto> AgregarCandidatura(
        int usuarioId, int decada, string titulo, string artista, CancellationToken ct)
    {
        var decadaCandidatura = new Decada(decada);
        var ronda = await ObtenerRondaVigente(ct)
            ?? throw new ConflictoRankingMusicaException("No hay una ronda de música abierta a candidaturas.");
        var candidatura = await repositorio.AgregarCandidatura(ronda.Id, usuarioId, decadaCandidatura, titulo, artista, ct);
        return MapearCandidatura(candidatura, 0);
    }

    private async Task<RankingMusica?> ObtenerRondaVigente(CancellationToken ct)
    {
        var ronda = await repositorio.ObtenerRondaAbierta(ct);
        var ahora = reloj.GetUtcNow();
        return ronda is { Estado: EstadoRankingMusica.Abierto } && ahora >= ronda.Apertura && ahora < ronda.Cierre
            ? ronda
            : null;
    }

    private static CandidaturasPorDecadaDto[] AgruparCandidaturasPorDecada(
        IReadOnlyList<CandidaturaRankingMusicaConPuntos> candidaturas) =>
        candidaturas.GroupBy(fila => fila.Candidatura.Decada.Valor)
            .Select(grupo => new CandidaturasPorDecadaDto(grupo.Key,
                grupo.Select(fila => MapearCandidatura(fila.Candidatura, fila.Puntos)).ToArray()))
            .ToArray();

    private static ResultadosRankingMusicaDto MapearResultados(RondaMusicaConResultados datos)
    {
        var decadas = datos.Posiciones.GroupBy(fila => fila.Resultado.Decada.Valor)
            .Select(grupo => new ResultadosPorDecadaDto(grupo.Key,
                grupo.Where(fila => fila.Resultado.Posicion <= CantidadGanadorasPorDecada).Select(MapearPosicion).ToArray(),
                grupo.Where(fila => fila.Resultado.Posicion > CantidadGanadorasPorDecada).Select(MapearPosicion).ToArray()))
            .ToArray();
        return new(datos.Ronda.Id, datos.Ronda.Apertura, datos.Ronda.Cierre, decadas);
    }

    private static CandidaturaRankingMusicaDto MapearCandidatura(CandidaturaRankingMusica candidatura, long puntos) =>
        new(candidatura.Id, candidatura.CreadorId, candidatura.Decada.Valor, candidatura.Titulo, candidatura.Artista, puntos);

    private static PosicionRankingMusicaDto MapearPosicion(PosicionRankingMusica posicion) =>
        new(posicion.Resultado.CandidaturaId, posicion.Resultado.Posicion, posicion.Titulo, posicion.Artista, posicion.Resultado.Puntos);
}
