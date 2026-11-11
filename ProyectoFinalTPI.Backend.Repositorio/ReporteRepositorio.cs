using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Dtos.Reportes;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public class ReporteRepositorio(ApplicationDbContext context) : IReporteRepositorio
{
    public Task<ObjetivoReporteDto?> ObtenerObjetivo(
        TipoContenidoReporte tipo, int contenidoId, int reportanteId,
        CancellationToken cancellationToken = default) => tipo switch
    {
        TipoContenidoReporte.Publicacion => ObtenerPublicacion(contenidoId, cancellationToken),
        TipoContenidoReporte.Comentario => ObtenerComentario(contenidoId, cancellationToken),
        TipoContenidoReporte.MensajeChat => ObtenerMensaje(contenidoId, reportanteId, cancellationToken),
        TipoContenidoReporte.Perfil => ObtenerPerfil(contenidoId, cancellationToken),
        _ => Task.FromResult<ObjetivoReporteDto?>(null)
    };

    public Task<bool> ExisteActivo(
        int reportanteId, TipoContenidoReporte tipo, int contenidoId,
        CancellationToken cancellationToken = default) =>
        context.Reportes.AnyAsync(r => r.ReportanteId == reportanteId
            && r.TipoContenido == tipo && r.ContenidoId == contenidoId
            && (r.Estado == EstadoReporte.Pendiente || r.Estado == EstadoReporte.EnRevision),
            cancellationToken);

    public async Task Agregar(Reporte reporte, CancellationToken cancellationToken = default)
    {
        context.Reportes.Add(reporte);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (EsDuplicado(exception))
        {
            throw new ReporteDuplicadoException(
                "Ya enviaste un reporte pendiente sobre este contenido.");
        }
    }

    public async Task<PaginaAdminDto<ReporteAdminDto>> Listar(
        FiltroReportesAdminDto filtro, CancellationToken cancellationToken = default)
    {
        var consulta = AplicarFiltros(context.Reportes.AsNoTracking(), filtro);
        var total = await consulta.CountAsync(cancellationToken);
        var items = await Proyectar(consulta).OrderByDescending(r => r.FechaCreacion)
            .Skip((filtro.Pagina - 1) * filtro.Tamanio).Take(filtro.Tamanio)
            .ToListAsync(cancellationToken);
        return new PaginaAdminDto<ReporteAdminDto>
        {
            Items = items, Pagina = filtro.Pagina, Tamanio = filtro.Tamanio, TotalItems = total
        };
    }

    public Task<Reporte?> Obtener(int id, CancellationToken cancellationToken = default) =>
        context.Reportes.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task EliminarMensajeChat(int id, CancellationToken cancellationToken = default)
    {
        var mensaje = await context.MensajesChat.SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("El mensaje de chat ya no existe.");
        if (mensaje.EstaEliminado) return;
        mensaje.EstaEliminado = true;
        mensaje.FechaEliminacion = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ResolverRelacionados(
        Reporte reporte, int administradorId, AccionReporte accion, string? observacion,
        CancellationToken cancellationToken = default)
    {
        var relacionados = await context.Reportes.Where(r => r.TipoContenido == reporte.TipoContenido
            && r.ContenidoId == reporte.ContenidoId
            && (r.Estado == EstadoReporte.Pendiente || r.Estado == EstadoReporte.EnRevision))
            .ToListAsync(cancellationToken);
        relacionados.ForEach(r => Resolver(r, administradorId, accion, observacion));
        await context.SaveChangesAsync(cancellationToken);
    }

    private Task<ObjetivoReporteDto?> ObtenerPublicacion(int id, CancellationToken cancellationToken) =>
        context.Publicaciones.AsNoTracking().Where(p => p.Id == id && !p.EstaOculto)
            .Select(p => new ObjetivoReporteDto
            {
                UsuarioReportadoId = p.UsuarioId,
                ContenidoSnapshot = p.Titulo + "\n" + p.Descripcion,
                ContextoId = p.Id,
                Contexto = p.Titulo
            }).SingleOrDefaultAsync(cancellationToken);

    private Task<ObjetivoReporteDto?> ObtenerComentario(int id, CancellationToken cancellationToken) =>
        context.Comentarios.AsNoTracking().Where(c => c.Id == id && !c.EstaEliminado && !c.EstaOculto)
            .Select(c => new ObjetivoReporteDto
            {
                UsuarioReportadoId = c.UsuarioId,
                ContenidoSnapshot = c.Contenido,
                ContextoId = c.PublicacionId,
                Contexto = c.Publicacion.Titulo
            }).SingleOrDefaultAsync(cancellationToken);

    private Task<ObjetivoReporteDto?> ObtenerMensaje(
        int id, int reportanteId, CancellationToken cancellationToken) =>
        context.MensajesChat.AsNoTracking().Where(m => m.Id == id && !m.EstaEliminado
            && m.FechaExpiracion > DateTime.UtcNow
            && (m.EmisorId == reportanteId || m.DestinatarioId == reportanteId))
            .Select(m => new ObjetivoReporteDto
            {
                UsuarioReportadoId = m.EmisorId,
                ContenidoSnapshot = m.Contenido,
                Contexto = "Mensaje de chat privado"
            }).SingleOrDefaultAsync(cancellationToken);

    private Task<ObjetivoReporteDto?> ObtenerPerfil(int id, CancellationToken cancellationToken) =>
        context.Usuarios.AsNoTracking().Where(u => u.Id == id && !u.EstaEliminado)
            .Select(u => new ObjetivoReporteDto
            {
                UsuarioReportadoId = u.Id,
                ContenidoSnapshot = "@" + u.Username,
                ContextoId = u.Id,
                Contexto = u.Email
            }).SingleOrDefaultAsync(cancellationToken);

    private static IQueryable<Reporte> AplicarFiltros(
        IQueryable<Reporte> consulta, FiltroReportesAdminDto filtro)
    {
        if (filtro.TipoContenido.HasValue) consulta = consulta.Where(r => r.TipoContenido == filtro.TipoContenido);
        if (filtro.Motivo.HasValue) consulta = consulta.Where(r => r.Motivo == filtro.Motivo);
        if (filtro.Estado.HasValue) consulta = consulta.Where(r => r.Estado == filtro.Estado);
        if (!string.IsNullOrWhiteSpace(filtro.Texto)) consulta = FiltrarTexto(consulta, filtro.Texto.Trim());
        return consulta;
    }

    private static IQueryable<Reporte> FiltrarTexto(IQueryable<Reporte> consulta, string texto) =>
        consulta.Where(r => r.Reportante.Username.ToLower().Contains(texto.ToLower())
            || r.UsuarioReportado.Username.ToLower().Contains(texto.ToLower())
            || r.ContenidoSnapshot.ToLower().Contains(texto.ToLower()));

    private IQueryable<ReporteAdminDto> Proyectar(IQueryable<Reporte> consulta) =>
        consulta.Select(r => new ReporteAdminDto
        {
            Id = r.Id, TipoContenido = r.TipoContenido, ContenidoId = r.ContenidoId,
            Motivo = r.Motivo, Detalle = r.Detalle, ContenidoSnapshot = r.ContenidoSnapshot,
            ContextoId = r.ContextoId, Contexto = r.Contexto, Estado = r.Estado,
            FechaCreacion = r.FechaCreacion, ReportanteId = r.ReportanteId,
            ReportanteUsername = r.Reportante.Username, UsuarioReportadoId = r.UsuarioReportadoId,
            UsuarioReportadoUsername = r.UsuarioReportado.Username,
            CantidadCoincidentes = context.Reportes.Count(x => x.TipoContenido == r.TipoContenido
                && x.ContenidoId == r.ContenidoId
                && (x.Estado == EstadoReporte.Pendiente || x.Estado == EstadoReporte.EnRevision)),
            AdministradorId = r.AdministradorId,
            AdministradorUsername = r.Administrador == null ? null : r.Administrador.Username,
            FechaResolucion = r.FechaResolucion, AccionTomada = r.AccionTomada,
            ObservacionAdministrativa = r.ObservacionAdministrativa
        });

    private static void Resolver(
        Reporte reporte, int administradorId, AccionReporte accion, string? observacion)
    {
        reporte.Estado = accion == AccionReporte.Desestimar
            ? EstadoReporte.Desestimado : EstadoReporte.Resuelto;
        reporte.AdministradorId = administradorId;
        reporte.FechaResolucion = DateTime.UtcNow;
        reporte.AccionTomada = accion;
        reporte.ObservacionAdministrativa = string.IsNullOrWhiteSpace(observacion)
            ? null : observacion.Trim();
    }

    private static bool EsDuplicado(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_reportes_ReportanteId_TipoContenido_ContenidoId"
        };
}
