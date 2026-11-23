namespace ProyectoFinalTPI.Backend.Dtos;

public sealed record SaldoDiarioPuntosNostalgiaDto(
    int Cupo,
    int PuntosRestantes,
    DateTimeOffset Reinicio,
    int PuntosExtra = 0)
{
    public int TotalDisponible => PuntosRestantes + PuntosExtra;
}
