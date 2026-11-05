namespace ProyectoFinalTPI.Backend.Dtos.Admin;

public class UsuarioAdminDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public string? Apellido { get; set; }
    public bool EsAdmin { get; set; }
    public bool EstaEliminado { get; set; }
    public TipoUsuarioAdmin Tipo { get; set; }
    public DateTime? SuspendidoHasta { get; set; }
    public DateTime FechaCreacion { get; set; }
}

public class FiltroUsuariosAdminDto
{
    public int Pagina { get; set; } = 1;
    public int Tamanio { get; set; } = 20;
    public string? Texto { get; set; }
    public bool? EsAdmin { get; set; }
    public bool? Suspendido { get; set; }
    public bool? Eliminado { get; set; } = false;
    public TipoUsuarioAdmin? Tipo { get; set; }
}

public enum TipoUsuarioAdmin
{
    Personal,
    Marca,
    Moderador,
    Otro
}

public class CambiarSuspensionUsuarioDto
{
    public bool Suspender { get; set; }
    public int Dias { get; set; } = 7;
}
