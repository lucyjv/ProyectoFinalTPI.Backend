namespace ProyectoFinalTPI.Backend.Dtos.Auth;

// Solo circula dentro del backend. No se devuelve por la API ni se escribe en logs.
public sealed record CorreoElectronicoDto(
    string Destinatario, string Asunto, string Html, string Texto, string ClaveIdempotencia);
