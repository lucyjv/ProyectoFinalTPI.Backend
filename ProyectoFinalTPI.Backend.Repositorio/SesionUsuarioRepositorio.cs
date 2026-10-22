using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public class SesionUsuarioRepositorio : ISesionUsuarioRepositorio
{
    private readonly ApplicationDbContext _context;

    public SesionUsuarioRepositorio(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AgregarAsync(SesionUsuario sesion, CancellationToken cancellationToken = default)
    {
        await _context.SesionesUsuario.AddAsync(sesion, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<SesionUsuario?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.SesionesUsuario
            .Include(s => s.Usuario)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> EstaActivaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ahora = DateTime.UtcNow;
        return _context.SesionesUsuario
            .AsNoTracking()
            .AnyAsync(s => s.Id == id && s.FechaRevocacion == null && s.FechaExpiracion > ahora, cancellationToken);
    }

    public async Task<bool> RotarAsync(SesionUsuario sesion, string refreshTokenHash, DateTime fechaExpiracion, DateTime ultimoUso, CancellationToken cancellationToken = default)
    {
        sesion.RefreshTokenHash = refreshTokenHash;
        sesion.FechaExpiracion = fechaExpiracion;
        sesion.UltimoUso = ultimoUso;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task RevocarAsync(SesionUsuario sesion, DateTime fechaRevocacion, CancellationToken cancellationToken = default)
    {
        if (sesion.FechaRevocacion is not null)
            return;

        sesion.FechaRevocacion = fechaRevocacion;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otra solicitud ya rotó o revocó la misma sesión.
        }
    }

    public Task RevocarTodasAsync(int usuarioId, DateTime fechaRevocacion, CancellationToken cancellationToken = default) =>
        _context.SesionesUsuario
            .Where(s => s.UsuarioId == usuarioId && s.FechaRevocacion == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.FechaRevocacion, fechaRevocacion), cancellationToken);
}
