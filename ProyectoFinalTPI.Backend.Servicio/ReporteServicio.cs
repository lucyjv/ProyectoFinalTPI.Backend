using ProyectoFinalTPI.Backend.Dtos.Reportes;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio.Extensions;

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
        dto.ContenidoId.ValidarId("contenido");
        dto.TipoContenido.ValidarEnum("tipo de contenido");
        dto.Motivo.ValidarEnum("motivo");
        dto.Detalle.ValidarTexto(1000, "detalle");
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
