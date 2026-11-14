namespace ProyectoFinalTPI.Backend.Interfaces.Configuracion;

public sealed class RecuperacionContrasenaOptions
{
    public const string Seccion = "RecuperacionContrasena";
    public bool Habilitado { get; set; }
    public string UrlRestablecimiento { get; set; } = "https://nostalgiar.com.ar/auth/restablecer";
    public int ValidezMinutos { get; set; } = 30;
    public int IntervaloSolicitudesSegundos { get; set; } = 120;
}

public sealed class CorreoResendOptions
{
    public const string Seccion = "CorreoResend";
    public string ApiKey { get; set; } = string.Empty;
    public string Remitente { get; set; } = "no-reply@notify.nostalgiar.com.ar";
    public string NombreRemitente { get; set; } = "Nostalgiar";
}
