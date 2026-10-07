using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Servicio
{
    public class InteresServicio : IInteresServicio
    {
        private readonly IPublicacionRepositorio _publicacionRepositorio;

        public InteresServicio(
            IPublicacionRepositorio publicacionRepositorio)
        {
            _publicacionRepositorio = publicacionRepositorio;
        }

        public async Task<IReadOnlyList<InteresCategoriaDto>>
            ObtenerIntereses(
                int usuarioId,
                CancellationToken cancellationToken = default)
        {
            if (usuarioId <= 0)
            {
                throw new ArgumentException(
                    "El ID del usuario debe ser mayor que cero.");
            }

            var desde = DateTime.UtcNow.AddDays(-90);

            var cantidades =
                await _publicacionRepositorio
                    .ObtenerCantidadPorCategoriaDelUsuario(
                        usuarioId,
                        desde,
                        cancellationToken);

            var totalPublicaciones = cantidades.Values.Sum();

            if (totalPublicaciones == 0)
            {
                return Array.Empty<InteresCategoriaDto>();
            }

            return cantidades
                .Select(cantidad => new InteresCategoriaDto
                {
                    Categoria = cantidad.Key,
                    Afinidad =
                        (double)cantidad.Value / totalPublicaciones
                })
                .OrderByDescending(interes => interes.Afinidad)
                .ToList();
        }
    }
}
