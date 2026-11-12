namespace ProyectoFinalTPI.Backend.Entidades;

public sealed class AdjuntoPublicacion
{
    public int Id { get; set; }
    public int PublicacionId { get; set; }
    public string Url { get; set; } = string.Empty;
    public MultimediaEnum Tipo { get; set; }
    public int Orden { get; set; }
}