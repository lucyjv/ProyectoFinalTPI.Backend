using ProyectoFinalTPI.Backend.Dtos.Usuario;

namespace ProyectoFinalTPI.Backend.Dtos.Grupo;

public class GrupoResumenDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int Decada { get; set; }
    public DateTime FechaCreacion { get; set; }
    public AutorResumenDto Creador { get; set; } = null!;
    public int CantidadIntegrantes { get; set; }
    public bool EsIntegrante { get; set; }
    public bool EsCreador { get; set; }
}
