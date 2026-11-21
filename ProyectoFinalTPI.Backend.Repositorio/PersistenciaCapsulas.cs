using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

// Se comparte el bloqueo del autor con CrearConCupo para evitar carreras con borrar cápsulas.
internal static class PersistenciaCapsulas
{
    internal static async Task BloquearPropietario(ApplicationDbContext contexto, int propietarioId, CancellationToken ct)
    {
        if (contexto.Database.IsRelational())
            await contexto.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Usuario\" WHERE \"Id\" = {propietarioId} FOR UPDATE", ct);
        if (!await contexto.Usuarios.AnyAsync(u => u.Id == propietarioId && !u.EstaEliminado, ct))
            throw new KeyNotFoundException("El usuario no existe o no está disponible.");
    }

    internal static async Task PrepararAsociaciones(ApplicationDbContext contexto, Publicacion publicacion, CancellationToken ct)
    {
        if (publicacion.Capsulas.Count == 0) return;
        var ids = publicacion.Capsulas.Select(r => r.CapsulaId).Distinct().ToArray();
        if (ids.Length != publicacion.Capsulas.Count || ids.Length > 100 || ids.Any(id => id <= 0) || publicacion.PublicacionOriginalId.HasValue)
            throw new ArgumentException("Las cápsulas del recuerdo no son válidas.");
        var capsulas = await contexto.Capsulas.Where(c => ids.Contains(c.Id) && c.PropietarioId == publicacion.UsuarioId &&
            !c.Propietario.EstaEliminado).ToListAsync(ct);
        if (capsulas.Count != ids.Length) throw new ArgumentException("Todas las cápsulas seleccionadas deben existir y pertenecerte.");
        var ahora = DateTime.UtcNow;
        foreach (var capsula in capsulas) capsula.FechaActualizacion = ahora;
        foreach (var relacion in publicacion.Capsulas) relacion.FechaIncorporacion = ahora;
    }
}
