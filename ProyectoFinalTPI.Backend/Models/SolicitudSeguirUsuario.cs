using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Models
{
    public class SolicitudSeguirUsuario
    {
        [Range(1, int.MaxValue)]
        public int IdUsuario { get; set; }

        [Range(1, int.MaxValue)]
        public int IdUsuarioASeguir { get; set; }
    }
}
