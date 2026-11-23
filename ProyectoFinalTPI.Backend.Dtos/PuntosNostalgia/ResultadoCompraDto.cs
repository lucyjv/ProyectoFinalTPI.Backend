namespace ProyectoFinalTPI.Backend.Dtos.PuntosNostalgia;

public sealed record ResultadoCompraDto(
    int CompraId,
    string Estado,
    int PuntosAcreditados,
    SaldoDiarioPuntosNostalgiaDto SaldoActual);
