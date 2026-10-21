using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public class ReprocesamientoRecuerdosServicio(IReprocesamientoRecuerdosRepositorio repositorio,
    IAnalizadorRecuerdo analizador, IOptions<OpcionesAnalisisRecuerdos> opciones, TimeProvider reloj,
    ILogger<ReprocesamientoRecuerdosServicio> logger) : IReprocesamientoRecuerdosServicio
{
    public async Task<VistaPreviaReprocesamientoDto> Previsualizar(int limite, int despuesDeId, bool incluirFallidos, CancellationToken ct)
    {
        if (limite is < 1 or > 25 || despuesDeId < 0) throw new ArgumentException("El límite debe estar entre 1 y 25 y el cursor debe ser no negativo.");
        var pagina = await repositorio.Previsualizar(limite, despuesDeId, incluirFallidos, ct);
        return pagina with { AnalisisDisponible = analizador.Disponible && opciones.Value.Habilitado };
    }

    public async Task<ResultadoReprocesamientoDto> Encolar(SolicitudReprocesamientoDto solicitud, int administradorId, CancellationToken ct)
    {
        if (administradorId <= 0) throw new UnauthorizedAccessException("Se requiere un administrador identificado.");
        if (solicitud?.Recuerdos == null || solicitud.Recuerdos.Count is < 1 or > 25 ||
            solicitud.Recuerdos.Any(r => r == null || r.PublicacionId <= 0 || r.VersionContenido <= 0) ||
            solicitud.Recuerdos.Select(r => r.PublicacionId).Distinct().Count() != solicitud.Recuerdos.Count ||
            solicitud.IntervaloSegundos is < 10 or > 3600)
            throw new ArgumentException("Seleccionar de 1 a 25 recuerdos distintos con Id y versión positivos, y un intervalo de 10 a 3600 segundos.");
        if (!analizador.Disponible || !opciones.Value.Habilitado) throw new AnalisisNoDisponibleException();
        var resultado = await repositorio.Encolar(solicitud, reloj.GetUtcNow().UtcDateTime, ct);
        logger.LogInformation("Administrador {AdministradorId}: reprocesamiento con {Programados} programados y {Omitidos} omitidos",
            administradorId, resultado.Programados, resultado.Omitidos);
        return resultado;
    }
}
