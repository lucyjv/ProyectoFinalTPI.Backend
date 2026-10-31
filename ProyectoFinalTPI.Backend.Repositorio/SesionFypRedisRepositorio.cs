using System.Text.Json;
using StackExchange.Redis;
using ProyectoFinalTPI.Backend.Entidades;
using Microsoft.Extensions.Caching.Distributed;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;

namespace ProyectoFinalTPI.Backend.Repositorio;

/// <summary>Almacena el orden del feed en Redis conservando el contrato de sesión.</summary>
public sealed class SesionFypRedisRepositorio(IDistributedCache cache) : ISesionFypRepositorio
{
    private static readonly TimeSpan Duracion = TimeSpan.FromMinutes(30);

    public SesionFypDto Crear(int usuarioId, IReadOnlyCollection<int> publicacionesIds)
    {
        ArgumentNullException.ThrowIfNull(publicacionesIds);
        if (usuarioId <= 0) throw new ArgumentException("El ID del usuario debe ser mayor que cero.");
        if (publicacionesIds.Any(id => id <= 0)) throw new ArgumentException("Los IDs de publicaciones deben ser mayores que cero.");
        var registro = new Registro(Guid.NewGuid(), usuarioId, DateTime.UtcNow.Add(Duracion), publicacionesIds.Distinct().ToArray());
        try
        {
            cache.Set(Clave(registro.Id), JsonSerializer.SerializeToUtf8Bytes(registro),
                new DistributedCacheEntryOptions { AbsoluteExpiration = new DateTimeOffset(registro.Expira) });
        }
        catch (RedisException ex) { throw new AlmacenSesionNoDisponibleException(ex); }
        return Convertir(registro);
    }

    public SesionFypDto? Obtener(Guid sesionId, int usuarioId)
    {
        byte[]? bytes;
        try { bytes = cache.Get(Clave(sesionId)); }
        catch (RedisException ex) { throw new AlmacenSesionNoDisponibleException(ex); }
        if (bytes is null) return null;
        Registro? registro;
        try { registro = JsonSerializer.Deserialize<Registro>(bytes); }
        catch (JsonException) { return null; }
        if (registro is null || registro.Publicaciones is null || registro.Id != sesionId || registro.UsuarioId != usuarioId || registro.Expira <= DateTime.UtcNow)
            return null;
        return Convertir(registro);
    }

    private static string Clave(Guid id) => $"fyp:sesion:v1:{id:N}";
    private static SesionFypDto Convertir(Registro r) => new(r.Id, r.UsuarioId, r.Expira, r.Publicaciones);
    private sealed record Registro(Guid Id, int UsuarioId, DateTime Expira, int[] Publicaciones);
}
