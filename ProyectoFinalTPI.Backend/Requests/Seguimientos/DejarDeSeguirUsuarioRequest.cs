using System.ComponentModel.DataAnnotations;

namespace ProyectoFinalTPI.Backend.Requests.Seguimientos
{
    public class DejarDeSeguirUsuarioRequest
    {
        [Range(1, int.MaxValue)]
        public int IdUsuario { get; set; }

        [Range(1, int.MaxValue)]
        public int IdUsuarioASeguir { get; set; }
    }
}
