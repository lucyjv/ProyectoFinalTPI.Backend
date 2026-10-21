namespace ProyectoFinalTPI.Backend.Entidades;

public class MomentoAlias
{
    public int Id { get; set; }
    public int MomentoId { get; set; }
    public Momento Momento { get; set; } = null!;
    public string Texto { get; set; } = string.Empty;
    public string TextoNormalizado { get; set; } = string.Empty;
}
