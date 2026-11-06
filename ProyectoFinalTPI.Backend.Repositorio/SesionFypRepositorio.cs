using Microsoft.Extensions.Caching.Memory;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;

namespace ProyectoFinalTPI.Backend.Repositorio
{
    public class SesionFypRepositorio : ISesionFypRepositorio
    {
        private readonly IMemoryCache _cache;

        private static readonly TimeSpan DuracionSesion =
            TimeSpan.FromMinutes(30);

        public SesionFypRepositorio(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task<SesionFypDto> CrearAsync(
            int usuarioId,
            IReadOnlyCollection<int> publicacionesIds, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(publicacionesIds);

            if (usuarioId <= 0)
            {
                throw new ArgumentException(
                    "El ID del usuario debe ser mayor que cero.");
            }

            if (publicacionesIds.Any(id => id <= 0))
            {
                throw new ArgumentException(
                    "Los IDs de publicaciones deben ser mayores que cero.");
            }

            var idsUnicos = new List<int>();
            var idsAgregados = new HashSet<int>();

            foreach (var id in publicacionesIds)
            {
                if (idsAgregados.Add(id))
                {
                    idsUnicos.Add(id);
                }
            }

            var sesion = new SesionFypDto(
                Guid.NewGuid(),
                usuarioId,
                DateTime.UtcNow.Add(DuracionSesion),
                idsUnicos);

            var opciones = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration =
                    new DateTimeOffset(sesion.FechaExpiracion)
            };

            _cache.Set(
                ObtenerClave(sesion.Id),
                sesion,
                opciones);

            return Task.FromResult(sesion);
        }

        public Task<SesionFypDto?> ObtenerAsync(
            Guid sesionId,
            int usuarioId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var clave = ObtenerClave(sesionId);

            if (!_cache.TryGetValue<SesionFypDto>(
                clave,
                out var sesion))
            {
                return Task.FromResult<SesionFypDto?>(null);
            }

            if (sesion == null || sesion.UsuarioId != usuarioId)
            {
                return Task.FromResult<SesionFypDto?>(null);
            }

            if (sesion.FechaExpiracion <= DateTime.UtcNow)
            {
                _cache.Remove(clave);
                return Task.FromResult<SesionFypDto?>(null);
            }

            return Task.FromResult<SesionFypDto?>(sesion);
        }

        private static string ObtenerClave(Guid sesionId)
        {
            return $"fyp:sesion:{sesionId:N}";
        }
    }
}
