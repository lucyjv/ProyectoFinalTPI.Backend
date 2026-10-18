using System.Security.Claims;

namespace ProyectoFinalTPI.Backend.Extensions
{
    public static class UsuarioAutenticadoExtensions
    {
        /// <summary>
        /// Obtiene el ID del usuario autenticado a partir de los claims NameIdentifier o sub del token JWT.
        /// Retorna null si el usuario no está autenticado o el valor no es un entero positivo válido.
        /// </summary>
        public static int? ObtenerUsuarioId(this ClaimsPrincipal? principal)
        {
            if (principal == null) return null;

            var idString = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? principal.FindFirst("sub")?.Value;

            return int.TryParse(idString, out var id) && id > 0 ? id : null;
        }

        /// <summary>
        /// Obtiene el ID del usuario autenticado o lanza UnauthorizedAccessException si no está autenticado.
        /// </summary>
        public static int ObtenerUsuarioIdRequerido(this ClaimsPrincipal? principal)
        {
            return principal.ObtenerUsuarioId()
                ?? throw new UnauthorizedAccessException("Debés iniciar sesión para realizar esta acción.");
        }
    }
}
