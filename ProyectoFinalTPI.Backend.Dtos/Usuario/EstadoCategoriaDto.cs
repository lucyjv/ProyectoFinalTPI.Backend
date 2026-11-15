namespace ProyectoFinalTPI.Backend.Dtos.Usuario;

public sealed record EstadoCategoriaDto(
    CategoriaCuentaDto Categoria, int PublicacionesConsumidas, int? PublicacionesRestantes,
    DateOnly SemanaInicio, DateOnly ReinicioSemana, int MaximoAdjuntos);