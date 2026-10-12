using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Entidades
{
    public class Logros_Usuarios
    {
        public int UsuarioId { get; set; }
        public Usuario_Interactivo Usuario { get; set; } = null!;
        public int LogroId { get; set; }
        public Logro Logro { get; set; } = null!;
        public DateTime FechaDesbloqueo { get; set; } = DateTime.UtcNow;
    }
}
