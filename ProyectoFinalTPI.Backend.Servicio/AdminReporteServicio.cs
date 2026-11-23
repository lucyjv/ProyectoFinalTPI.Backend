using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio.Extensions;

namespace ProyectoFinalTPI.Backend.Servicio;

public class AdminReporteServicio(
    IReporteRepositorio repositorio,
    IAdminPublicacionServicio publicaciones,
    IAdminComentarioServicio comentarios,
    IAdminUsuarioServicio usuarios,
    INotificacionServicio notificaciones) : IAdminReporteServicio
{
    public Task<PaginaAdminDto<ReporteAdminDto>> Listar(
        FiltroReportesAdminDto filtro, CancellationToken cancellationToken = default)
    {
        ValidarPagina(filtro);
        return repositorio.Listar(filtro, cancellationToken);
    }

    public async Task Resolver(
        int administradorId, ResolverReporteAdminDto dto,
        CancellationToken cancellationToken = default)
    {
        ValidarResolucion(dto);
        var reporte = await repositorio.Obtener(dto.ReporteId, cancellationToken)
            ?? throw new KeyNotFoundException("El reporte no existe.");
        if (reporte.Estado is EstadoReporte.Resuelto or EstadoReporte.Desestimado)
            throw new ArgumentException("El reporte ya fue resuelto.");
        await AplicarAccion(administradorId, reporte, dto, cancellationToken);
        await repositorio.ResolverRelacionados(
            reporte, administradorId, dto.Accion, dto.Observacion, cancellationToken);
    }

    private Task AplicarAccion(
        int administradorId, Reporte reporte, ResolverReporteAdminDto dto,
        CancellationToken cancellationToken) => dto.Accion switch
    {
        AccionReporte.Desestimar => Task.CompletedTask,
        AccionReporte.OcultarContenido => Ocultar(reporte, dto.Observacion, cancellationToken),
        AccionReporte.EliminarContenido => Eliminar(reporte, cancellationToken),
        AccionReporte.SuspenderUsuario => Suspender(
            administradorId, reporte, dto.DiasSuspension, cancellationToken),
        AccionReporte.DarDeBajaUsuario => DarDeBaja(administradorId, reporte, cancellationToken),
        _ => throw new ArgumentException("La acción administrativa no es válida.")
    };

    private Task Ocultar(Reporte reporte, string? motivo, CancellationToken cancellationToken)
    {
        if (reporte.TipoContenido != TipoContenidoReporte.Publicacion)
            throw new ArgumentException("Sólo las publicaciones se pueden ocultar.");
        return publicaciones.CambiarVisibilidad(reporte.ContenidoId,
            new CambiarVisibilidadPublicacionDto
            {
                Ocultar = true,
                Motivo = string.IsNullOrWhiteSpace(motivo)
                    ? "Contenido reportado que no cumple los estándares de la comunidad."
                    : motivo.Trim()
            }, cancellationToken);
    }

    private async Task Eliminar(Reporte reporte, CancellationToken cancellationToken)
    {
        switch (reporte.TipoContenido)
        {
            case TipoContenidoReporte.Publicacion:
                await publicaciones.Eliminar(reporte.ContenidoId, cancellationToken);
                break;
            case TipoContenidoReporte.Comentario:
                await comentarios.Eliminar(reporte.ContenidoId, cancellationToken);
                break;
            case TipoContenidoReporte.MensajeChat:
                await repositorio.EliminarMensajeChat(reporte.ContenidoId, cancellationToken);
                await notificaciones.NotificarAdministrativaAsync(
                    reporte.UsuarioReportadoId, null, cancellationToken);
                break;
            default:
                throw new ArgumentException("Un perfil no es contenido eliminable.");
        }
    }

    private async Task Suspender(
        int administradorId, Reporte reporte, int dias, CancellationToken cancellationToken)
    {
        await usuarios.CambiarSuspension(reporte.UsuarioReportadoId, administradorId,
            new CambiarSuspensionUsuarioDto { Suspender = true, Dias = dias }, cancellationToken);
        await notificaciones.NotificarAdministrativaAsync(
            reporte.UsuarioReportadoId, reporte.ContextoId, cancellationToken);
    }

    private async Task DarDeBaja(
        int administradorId, Reporte reporte, CancellationToken cancellationToken)
    {
        await usuarios.DarDeBaja(
            reporte.UsuarioReportadoId, administradorId, cancellationToken);
        await notificaciones.NotificarAdministrativaAsync(
            reporte.UsuarioReportadoId, reporte.ContextoId, cancellationToken);
    }

    private static void ValidarPagina(FiltroReportesAdminDto filtro)
    {
        filtro.Pagina.ValidarId("página");
        filtro.Tamanio.ValidarRango(1, 100, "tamaño");
        if (filtro.TipoContenido.HasValue)
            filtro.TipoContenido.Value.ValidarEnum("tipo de contenido");
        if (filtro.Motivo.HasValue)
            filtro.Motivo.Value.ValidarEnum("motivo");
        if (filtro.Estado.HasValue)
            filtro.Estado.Value.ValidarEnum("estado");
    }

    private static void ValidarResolucion(ResolverReporteAdminDto dto)
    {
        dto.ReporteId.ValidarId("reporte");
        dto.Accion.ValidarEnum("acción");
        if (dto.Accion == AccionReporte.SuspenderUsuario && dto.DiasSuspension is < 1 or > 365)
            throw new ArgumentException("La suspensión debe durar entre 1 y 365 días.");
        dto.Observacion.ValidarTexto(1000, "observación", minimo: 0);
    }
}
