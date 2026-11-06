using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Dtos.Admin;

public class PublicacionAdminDto
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public DateTime FechaCreacion { get; set; }
    public CategoriaEnum Categoria { get; set; }
    public bool EstaOculto { get; set; }
    public string? MotivoOculto { get; set; }
    public int AutorId { get; set; }
    public string AutorUsername { get; set; } = string.Empty;
}

public class FiltroPublicacionesAdminDto
{
    public int Pagina { get; set; } = 1;
    public int Tamanio { get; set; } = 20;
    public string? Texto { get; set; }
    public int? AutorId { get; set; }
    public CategoriaEnum? Categoria { get; set; }
    public bool? Oculta { get; set; }
}

public class ActualizarPublicacionAdminDto
{
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public CategoriaEnum Categoria { get; set; }
}

public class CambiarVisibilidadPublicacionDto
{
    public bool Ocultar { get; set; }
    public string? Motivo { get; set; }
}
