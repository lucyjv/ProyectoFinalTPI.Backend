using System;

namespace ProyectoFinalTPI.Backend.Dtos.Publicacion
{
    public class CompartirPublicacionDto
    {
        public int PublicacionOriginalId { get; set; }
        public int AutorId { get; set; }
        public string Descripcion { get; set; } = string.Empty;
    }
}

