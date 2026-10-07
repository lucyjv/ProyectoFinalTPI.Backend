using System.Text.Json;
using ProyectoFinalTPI.Backend.Dtos;

namespace ProyectoFinalTPI.Backend.Servicio
{
    internal static class CursorCronologicoCodec
    {
        public static string Crear(DateTime fechaCreacion, int id)
        {
            var cursor = new CursorCronologicoDto
            {
                FechaCreacion = fechaCreacion,
                Id = id
            };

            var bytes = JsonSerializer.SerializeToUtf8Bytes(cursor);

            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        public static CursorCronologicoDto? Leer(string? valor)
        {
            if (valor == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(valor) || valor.Length > 512)
            {
                throw new ArgumentException("El cursor no es válido.");
            }

            try
            {
                var base64 = valor
                    .Replace('-', '+')
                    .Replace('_', '/');

                base64 = base64.PadRight(
                    base64.Length + (4 - base64.Length % 4) % 4,
                    '=');

                var bytes = Convert.FromBase64String(base64);

                var cursor =
                    JsonSerializer.Deserialize<CursorCronologicoDto>(bytes);

                if (cursor == null ||
                    cursor.Id <= 0 ||
                    cursor.FechaCreacion == default ||
                    cursor.FechaCreacion.Kind != DateTimeKind.Utc)
                {
                    throw new ArgumentException("El cursor no es válido.");
                }

                return cursor;
            }
            catch (FormatException)
            {
                throw new ArgumentException("El cursor no es válido.");
            }
            catch (JsonException)
            {
                throw new ArgumentException("El cursor no es válido.");
            }
        }
    }
}