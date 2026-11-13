using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public sealed class RecuperacionContrasenaRepositorio(
    ApplicationDbContext context, ISesionUsuarioRepositorio sesiones, TimeProvider reloj)
    : IRecuperacionContrasenaRepositorio
{
    public async Task<bool> CrearSiCorrespondeAsync(RecuperacionContrasena recuperacion, TimeSpan intervalo,
        CancellationToken cancellationToken = default)
    {
        await using var transaccion = await context.Database.BeginTransactionAsync(cancellationToken);
        var usuario = await BloquearUsuario(recuperacion.UsuarioId, cancellationToken);
        var ahora = reloj.GetUtcNow().UtcDateTime;

        if (!PuedeRecuperar(usuario, recuperacion.EmailDestino, ahora) || recuperacion.FechaExpiracion <= ahora)
            return false;

        var desde = ahora.Subtract(intervalo);
        if (await context.RecuperacionesContrasena.AnyAsync(
                r => r.UsuarioId == recuperacion.UsuarioId && r.FechaCreacion > desde, cancellationToken))
            return false;

        context.RecuperacionesContrasena.Add(recuperacion);
        await context.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
        return true;
    }

    public Task<RecuperacionContrasena?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.RecuperacionesContrasena.AsNoTracking().Include(r => r.Usuario)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<bool> RestablecerAsync(Guid id, string tokenHash, string nuevaContrasenaHash,
        CancellationToken cancellationToken = default)
    {
        var recuperacion = await ObtenerPorIdAsync(id, cancellationToken);
        if (recuperacion is null) return false;

        await using var transaccion = await context.Database.BeginTransactionAsync(cancellationToken);

        // bloqueamos primero al usuario: dos enlaces de la misma cuenta no pueden cambiarla a la vez
        var usuario = await BloquearUsuario(recuperacion.UsuarioId, cancellationToken);
        var ahora = reloj.GetUtcNow().UtcDateTime;
        if (!PuedeRecuperar(usuario, recuperacion.EmailDestino, ahora)) return false;

        var consumidas = await context.RecuperacionesContrasena
            .Where(r => r.Id == id && r.TokenHash == tokenHash && r.FechaUso == null
                && r.FechaRevocacion == null && r.FechaExpiracion > ahora)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.FechaUso, ahora), cancellationToken);

        if (consumidas != 1) return false;

        await context.Usuarios.Where(u => u.Id == recuperacion.UsuarioId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Contraseña, nuevaContrasenaHash), cancellationToken);
        await context.RecuperacionesContrasena
            .Where(r => r.UsuarioId == recuperacion.UsuarioId && r.Id != id
                && r.FechaUso == null && r.FechaRevocacion == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.FechaRevocacion, ahora), cancellationToken);

        // la cancelación pertenece a la misma transacción.
        await sesiones.RevocarTodasAsync(recuperacion.UsuarioId, ahora, cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<Usuario?> BloquearUsuario(int usuarioId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Id\" FROM \"Usuario\" WHERE \"Id\" = {usuarioId} FOR UPDATE", cancellationToken);
        return await context.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);
    }

    private static bool PuedeRecuperar(Usuario? usuario, string email, DateTime ahora) =>
        usuario is not null && !usuario.EstaEliminado
        && !(usuario is Usuario_Interactivo { SuspendidoHasta: { } hasta } && hasta > ahora)
        && string.Equals(usuario.Email.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase);
}
