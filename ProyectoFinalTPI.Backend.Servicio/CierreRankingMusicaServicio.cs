using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class CierreRankingMusicaServicio(
    IRankingMusicaRepositorio repositorio, TimeProvider reloj, IOptions<RankingMusicaOptions> opciones)
    : ICierreRankingMusicaServicio
{
    private const int DiasPorRonda = 15;

    public async Task<bool> ProcesarRonda(CancellationToken ct = default)
    {
        var ronda = await repositorio.ObtenerRondaAbierta(ct);
        var ahora = reloj.GetUtcNow();
        if (ronda is null)
        {
            var inicio = opciones.Value.FechaInicio ?? CalendarioNostalgia.FechaLocal(ahora);
            return await repositorio.CrearRondaInicial(
                CalendarioNostalgia.InicioDelDia(inicio), CalendarioNostalgia.InicioDelDia(inicio.AddDays(DiasPorRonda)), ct);
        }

        if (ahora < ronda.Cierre)
            return false;

        var inicioSiguiente = CalendarioNostalgia.FechaLocal(ronda.Cierre);
        var cierreSiguiente = CalendarioNostalgia.InicioDelDia(inicioSiguiente.AddDays(DiasPorRonda));
        return await repositorio.CerrarRonda(ronda.Id, cierreSiguiente, ct);
    }
}
