using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Cache;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public sealed class CategoriaRepositorio(
    ApplicationDbContext contexto,
    IInvalidadorLecturasPublicas? cache = null)
    : ICategoriaRepositorio
{
    public async Task<bool> PromoverHasta(
        int usuarioId,
        ReputacionEnum destino,
        CancellationToken ct = default)
    {
        var candidatos = contexto.Set<Usuario_Personal>()
            .Where(usuario =>
                usuario.Id == usuarioId &&
                (int)usuario.Reputacion < (int)destino);

        var cambiado = contexto.Database.IsRelational()
            ? await candidatos.ExecuteUpdateAsync(
                cambios => cambios.SetProperty(
                    usuario => usuario.Reputacion, destino),
                ct) > 0
            : await PromoverEnMemoria(candidatos, destino, ct);

        if (cambiado && cache is not null)
            await cache.InvalidarAsync(CancellationToken.None);

        return cambiado;
    }
    public Task<Usuario?> ObtenerUsuario(int usuarioId, CancellationToken ct = default) =>
        contexto.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == usuarioId, ct);

    public async Task<bool> TieneRecuerdos(int usuarioId, CancellationToken ct = default) =>
        await contexto.Publicaciones.AnyAsync(p => p.UsuarioId == usuarioId && p.PublicacionOriginalId == null, ct)
        || await contexto.Set<ConsumoPublicaciones>().AnyAsync(c => c.UsuarioId == usuarioId && c.Cantidad > 0, ct);

    public async Task<int> ObtenerConsumo(int usuarioId, DateOnly semana, CancellationToken ct = default) =>
        await contexto.Set<ConsumoPublicaciones>().Where(c => c.UsuarioId == usuarioId && c.SemanaInicio == semana)
            .Select(c => c.Cantidad).SingleOrDefaultAsync(ct);

    public async Task<int?> CrearConCupo(Publicacion publicacion, DateOnly semana, int? limite, CancellationToken ct = default)
    {
        if (!contexto.Database.IsRelational()) return await GuardarConCupo(publicacion, semana, limite, ct);
        // Todas las creaciones del mismo autor se serializan en PostgreSQL.
        // El consumo y la publicación se guardan en la misma transacción.
        await using var transaccion = await contexto.Database.BeginTransactionAsync(ct);
        await contexto.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Usuario\" WHERE \"Id\" = {publicacion.UsuarioId} FOR UPDATE", ct);
        var id = await GuardarConCupo(publicacion, semana, limite, ct);
        if (id.HasValue)
        {
            await transaccion.CommitAsync(ct);
            if (cache is not null) await cache.InvalidarAsync(CancellationToken.None);
        }
        return id;
    }

    private async Task<int?> GuardarConCupo(Publicacion publicacion, DateOnly semana, int? limite, CancellationToken ct)
    {
        var consumo = await contexto.Set<ConsumoPublicaciones>().SingleOrDefaultAsync(
            c => c.UsuarioId == publicacion.UsuarioId && c.SemanaInicio == semana, ct);
        if (limite.HasValue && (consumo?.Cantidad ?? 0) >= limite.Value) return null;
        if (consumo is null)
        {
            consumo = new() { UsuarioId = publicacion.UsuarioId, SemanaInicio = semana };
            contexto.Add(consumo);
        }
        consumo.Cantidad++;
        contexto.Publicaciones.Add(publicacion);
        await contexto.SaveChangesAsync(ct);
        return publicacion.Id;
    }

    private async Task<bool> PromoverEnMemoria(
        IQueryable<Usuario_Personal> candidatos,
        ReputacionEnum destino,
        CancellationToken ct)
    {
        var usuario = await candidatos.SingleOrDefaultAsync(ct);
        if (usuario is null) return false;

        usuario.Reputacion = destino;
        await contexto.SaveChangesAsync(ct);
        return true;
    }
}