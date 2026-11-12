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