using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Dtos.Publicacion
{
    public class SesionFypDto
    {
        public Guid Id { get; }
        public int UsuarioId { get; }
        public DateTime FechaExpiracion { get; }
        public IReadOnlyList<int> PublicacionesIds { get; }

        public SesionFypDto(
            Guid id,
            int usuarioId,
            DateTime fechaExpiracion,
            IEnumerable<int> publicacionesIds)
        {
            Id = id;
            UsuarioId = usuarioId;
            FechaExpiracion = fechaExpiracion;

            PublicacionesIds = Array.AsReadOnly(
                publicacionesIds.ToArray());
        }
    }
}
