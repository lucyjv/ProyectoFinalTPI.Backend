using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Servicio
{
    using global::ProyectoFinalTPI.Backend.Dtos;
    using System.Text.Json;

    namespace ProyectoFinalTPI.Backend.Servicio
    {
        internal static class CursorFypCodec
        {
            public static string Crear(Guid sesionId, int posicion)
            {
                if (sesionId == Guid.Empty || posicion < 0)
                {
                    throw new ArgumentException(
                        "Los datos del cursor no son válidos.");
                }

                var cursor = new CursorFypDto
                {
                    SesionId = sesionId,
                    Posicion = posicion
                };

                var bytes = JsonSerializer.SerializeToUtf8Bytes(cursor);

                return Convert.ToBase64String(bytes)
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_');
            }

            public static CursorFypDto? Leer(string? valor)
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
                        JsonSerializer.Deserialize<CursorFypDto>(bytes);

                    if (cursor == null ||
                        cursor.SesionId == Guid.Empty ||
                        cursor.Posicion < 0)
                    {
                        throw new ArgumentException(
                            "El cursor no es válido.");
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
}
