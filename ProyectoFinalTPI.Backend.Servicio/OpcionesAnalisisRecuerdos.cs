using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Servicio;

public class OpcionesAnalisisRecuerdos
{
    public bool Habilitado { get; set; } = true;
    [Range(1, 60)] public int IntervaloSegundos { get; set; } = 5;
    [Range(10, 900)] public int ReservaSegundos { get; set; } = 120;
    [Range(1, 300)] public int TiempoMaximoSegundos { get; set; } = 30;
    [Range(1, 10)] public int MaxIntentos { get; set; } = 3;
    [Range(1, 60)] public int ReintentoBaseSegundos { get; set; } = 10;
    [Range(100, 100000)] public int MaxCaracteresEntrada { get; set; } = 20000;
}
