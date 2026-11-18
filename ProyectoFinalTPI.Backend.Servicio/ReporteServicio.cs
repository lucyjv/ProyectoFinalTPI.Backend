using ProyectoFinalTPI.Backend.Dtos.Reportes;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public class ReporteServicio(IReporteRepositorio repositorio) : IReporteServicio
{
    public async Task<Reporte> Crear(
        int reportanteId, CrearReporteDto dto, CancellationToken cancellationToken = default)
    {
        Validar(dto);
        var objetivo = await repositorio.ObtenerObjetivo(
            dto.TipoContenido, dto.ContenidoId, reportanteId, cancellationToken)
            ?? throw new KeyNotFoundException("El contenido que querés reportar no existe o no está disponible.");
        if (objetivo.UsuarioReportadoId == reportanteId)
            throw new ArgumentException("No podés reportar tu propio contenido o perfil.");
        if (await repositorio.ExisteActivo(reportanteId, dto.TipoContenido, dto.ContenidoId, cancellationToken))
            throw new ReporteDuplicadoException("Ya enviaste un reporte pendiente sobre este contenido.");
        var reporte = CrearEntidad(reportanteId, dto, objetivo);
        await repositorio.Agregar(reporte, cancellationToken);
        return reporte;
    }

    private static void Validar(CrearReporteDto dto)
    {
        if (dto.ContenidoId <= 0) throw new ArgumentException("El ID del contenido debe ser mayor que cero.");
        if (!Enum.IsDefined(dto.TipoContenido)) throw new ArgumentException("El tipo de contenido no es válido.");
        if (!Enum.IsDefined(dto.Motivo)) throw new ArgumentException("El motivo del reporte no es válido.");
        if (dto.Detalle?.Trim().Length > 1000) throw new ArgumentException("El detalle no puede superar 1000 caracteres.");
    }

    private static Reporte CrearEntidad(
        int reportanteId, CrearReporteDto dto, ObjetivoReporteDto objetivo) => new()
    {
        ReportanteId = reportanteId,
        UsuarioReportadoId = objetivo.UsuarioReportadoId,
        TipoContenido = dto.TipoContenido,
        ContenidoId = dto.ContenidoId,
        Motivo = dto.Motivo,
        Detalle = string.IsNullOrWhiteSpace(dto.Detalle) ? null : dto.Detalle.Trim(),
        ContenidoSnapshot = objetivo.ContenidoSnapshot,
        ContextoId = objetivo.ContextoId,
        Contexto = objetivo.Contexto
    };
}
