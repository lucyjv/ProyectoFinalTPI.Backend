using Microsoft.Extensions.Logging;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public class GestionDuplicadosMomentoServicio(IGestionDuplicadosMomentoRepositorio repositorio,
    ILogger<GestionDuplicadosMomentoServicio> logger) : IGestionDuplicadosMomentoServicio
{
    public Task<IReadOnlyList<CandidatoDuplicadoMomentoDto>> BuscarCandidatos(int id, int limite, CancellationToken ct)
    {
        if (id <= 0 || limite is < 1 or > 50) throw new ArgumentException("El Id debe ser positivo y el límite debe estar entre 1 y 50.");
        return repositorio.BuscarCandidatos(id, limite, ct);
    }

    public Task<VistaFusionMomentoDto> Previsualizar(int origenId, int destinoId, CancellationToken ct)
    {
        ValidarIds(origenId, destinoId);
        return repositorio.Previsualizar(origenId, destinoId, ct);
    }

    public async Task<ResultadoFusionMomentoDto> Fusionar(int origenId, SolicitudFusionMomentoDto solicitud, int administradorId, CancellationToken ct)
    {
        if (administradorId <= 0) throw new UnauthorizedAccessException("Se requiere un administrador identificado.");
        ArgumentNullException.ThrowIfNull(solicitud);
        ValidarIds(origenId, solicitud.DestinoId);
        if (solicitud.VersionOrigen <= 0 || solicitud.VersionDestino <= 0) throw new ArgumentException("Las versiones de catálogo deben ser positivas.");
        var resultado = await repositorio.Fusionar(origenId, solicitud, ct);
        logger.LogInformation("Administrador {AdministradorId}: fusión de momento {OrigenId} en {DestinoId}, {Publicaciones} publicaciones afectadas",
            administradorId, origenId, resultado.DestinoId, resultado.PublicacionesAfectadas);
        return resultado;
    }

    private static void ValidarIds(int origenId, int destinoId)
    {
        if (origenId <= 0 || destinoId <= 0 || origenId == destinoId) throw new ArgumentException("Elegir dos momentos distintos con identificadores positivos.");
    }
}
