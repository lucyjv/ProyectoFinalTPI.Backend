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

        public SesionFypDto Crear(
            int usuarioId,
            IReadOnlyCollection<int> publicacionesIds)
        {
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

            // Elimina duplicados conservando el orden recibido.
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

            return sesion;
        }

        public SesionFypDto? Obtener(
            Guid sesionId,
            int usuarioId)
        {
            var clave = ObtenerClave(sesionId);

            if (!_cache.TryGetValue<SesionFypDto>(
                clave,
                out var sesion))
            {
                return null;
            }

            if (sesion == null || sesion.UsuarioId != usuarioId)
            {
                return null;
            }

            if (sesion.FechaExpiracion <= DateTime.UtcNow)
            {
                _cache.Remove(clave);
                return null;
            }

            return sesion;
        }

        private static string ObtenerClave(Guid sesionId)
        {
            return $"fyp:sesion:{sesionId:N}";
        }
    }
}