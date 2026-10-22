using ProyectoFinalTPI.Backend.Dtos.Usuario;

namespace ProyectoFinalTPI.Backend.Dtos.Grupo;

public class GrupoIntegranteDto
{
    public AutorResumenDto Usuario { get; set; } = null!;
    public DateTime FechaUnion { get; set; }
    public bool EsCreador { get; set; }
}
