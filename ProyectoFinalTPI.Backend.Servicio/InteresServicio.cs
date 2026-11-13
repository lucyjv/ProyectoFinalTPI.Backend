using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public class InteresServicio(IPublicacionRepositorio publicacionRepositorio) : IInteresServicio
{

    #region Operaciones públicas

    public async Task<IReadOnlyList<InteresCategoriaDto>> ObtenerIntereses(
        int usuarioId, CancellationToken cancellationToken = default)
    {
        ValidarId(usuarioId);

        var cantidades = await publicacionRepositorio.ObtenerCantidadPorCategoriaDelUsuario(
            usuarioId, DateTime.UtcNow.AddDays(-90), cancellationToken);
        return CalcularAfinidades(cantidades);
    }

    #endregion

    #region Métodos privados y validaciones

    private static void ValidarId(int usuarioId)
    {
        if (usuarioId <= 0)
            throw new ArgumentException("El ID del usuario debe ser mayor que cero.");
    }

    private static IReadOnlyList<InteresCategoriaDto> CalcularAfinidades(Dictionary<Entidades.CategoriaEnum, int> cantidades)
    {
        var total = cantidades.Values.Sum();
        if (total == 0) return Array.Empty<InteresCategoriaDto>();

        return cantidades.Select(cantidad => new InteresCategoriaDto
        {
            Categoria = cantidad.Key,
            Afinidad = (double)cantidad.Value / total
        })
            .OrderByDescending(interes => interes.Afinidad)
            .ToArray();
    }

    #endregion
}
