namespace ProyectoFinalTPI.Backend.Entidades;

public sealed class CupoDiarioPuntosNostalgiaAgotadoException(int puntosRestantes)
    : Exception($"No tenés suficientes puntos diarios. Te quedan {puntosRestantes}.")
{
    public int PuntosRestantes { get; } = puntosRestantes;
}
