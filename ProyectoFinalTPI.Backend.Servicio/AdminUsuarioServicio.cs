using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public class AdminUsuarioServicio(
    IAdminUsuarioRepositorio repositorio,
    ISesionUsuarioRepositorio sesiones) : IAdminUsuarioServicio
{
    public Task<PaginaAdminDto<UsuarioAdminDto>> Listar(
        FiltroUsuariosAdminDto filtro, CancellationToken cancellationToken = default)
    {
        ValidarPagina(filtro.Pagina, filtro.Tamanio);
        return repositorio.Listar(filtro, cancellationToken);
    }

    public async Task CambiarSuspension(
        int id, int adminId, CambiarSuspensionUsuarioDto dto,
        CancellationToken cancellationToken = default)
    {
        ValidarObjetivo(id, adminId);
        if (dto.Suspender && dto.Dias is < 1 or > 365)
            throw new ArgumentException("La suspensión debe durar entre 1 y 365 días.");
        var usuario = await ObtenerInteractivo(id, cancellationToken);
        usuario.SuspendidoHasta = dto.Suspender ? DateTime.UtcNow.AddDays(dto.Dias) : null;
        await repositorio.Guardar(cancellationToken);
        if (dto.Suspender) await sesiones.RevocarTodasAsync(id, DateTime.UtcNow, cancellationToken);
    }

    public async Task DarDeBaja(int id, int adminId, CancellationToken cancellationToken = default)
    {
        ValidarObjetivo(id, adminId);
        var usuario = await repositorio.Obtener(id, cancellationToken)
            ?? throw new KeyNotFoundException("El usuario no existe.");
        if (usuario.EsAdmin) throw new UnauthorizedAccessException("No se puede dar de baja a otro administrador.");
        if (usuario.EstaEliminado) return;
        usuario.EstaEliminado = true;
        usuario.FechaEliminacion = DateTime.UtcNow;
        if (usuario is Usuario_Interactivo interactivo) interactivo.SuspendidoHasta = DateTime.UtcNow.AddYears(100);
        await repositorio.Guardar(cancellationToken);
        await sesiones.RevocarTodasAsync(id, DateTime.UtcNow, cancellationToken);
    }

    private async Task<Usuario_Interactivo> ObtenerInteractivo(int id, CancellationToken cancellationToken)
    {
        var usuario = await repositorio.Obtener(id, cancellationToken)
            ?? throw new KeyNotFoundException("El usuario no existe.");
        if (usuario.EstaEliminado) throw new ArgumentException("El usuario está dado de baja.");
        return usuario as Usuario_Interactivo
            ?? throw new ArgumentException("El usuario no admite suspensiones.");
    }

    private static void ValidarObjetivo(int id, int adminId)
    {
        if (id <= 0) throw new ArgumentException("El ID de usuario debe ser mayor que cero.");
        if (id == adminId) throw new UnauthorizedAccessException("No podés aplicar esta acción sobre tu propia cuenta.");
    }

    private static void ValidarPagina(int pagina, int tamanio)
    {
        if (pagina < 1) throw new ArgumentException("La página debe ser mayor que cero.");
        if (tamanio is < 1 or > 100) throw new ArgumentException("El tamaño debe estar entre 1 y 100.");
    }
}
