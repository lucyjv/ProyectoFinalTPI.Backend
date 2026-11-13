using System.Net;
using ProyectoFinalTPI.Backend.Dtos.Auth;

namespace ProyectoFinalTPI.Backend.Servicio;

public static class CorreosRecuperacion
{
    public static CorreoElectronicoDto CrearEnlace(string email, string enlace, int minutos, Guid id)
    {
        var html = $"""
            <h1>Restablecé tu contraseña de Nostalgiar</h1>
            <p>Recibimos una solicitud para cambiar tu contraseña.</p>
            <p><a href="{WebUtility.HtmlEncode(enlace)}">Elegir una nueva contraseña</a></p>
            <p>Este enlace vence en {minutos} minutos y se puede usar una sola vez.</p>
            <p>Si no lo solicitaste, podés ignorar este correo. Tu contraseña sigue siendo la misma.</p>
            """;
        var texto = $"Restablecé tu contraseña de Nostalgiar: {enlace}\n"
            + $"El enlace vence en {minutos} minutos y se puede usar una sola vez. "
            + "Si no lo solicitaste, podés ignorar este correo.";
        return new(email, "Restablecé tu contraseña de Nostalgiar", html, texto, $"recuperacion/{id:N}");
    }

    public static CorreoElectronicoDto CrearAviso(string email, Guid id) => new(email,
        "Se actualizó tu contraseña de Nostalgiar",
        "<h1>Tu contraseña fue actualizada</h1><p>Se cerraron las sesiones anteriores de tu cuenta.</p>"
            + "<p>Si no realizaste este cambio, solicitá una nueva recuperación desde el sitio de Nostalgiar.</p>",
        "Tu contraseña de Nostalgiar fue actualizada y se cerraron tus sesiones anteriores. "
            + "Si no realizaste este cambio, solicitá una nueva recuperación desde el sitio de Nostalgiar.",
        $"contrasena-actualizada/{id:N}");
}
