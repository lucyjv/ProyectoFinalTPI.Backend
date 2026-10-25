using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProyectoFinalTPI.Backend.Entidades
{
    public class Logro
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string UrlIcono { get; set; } = string.Empty;
        public int PuntosRecompensa { get; set; }
        public ICollection<Logros_Usuarios> LogrosUsuarios { get; set; } = new List<Logros_Usuarios>();

    }
    public enum LogroEnum
    {
        PrimerPaso = 1,
        PrimeraPalabra = 2,
        ConAmigos = 3,
        Curioso = 6,
        CronistaLocal = 7,
        VozDeUnaEpoca = 8,
        FotografoDelRecuerdo = 9,
        Popular = 10,
        Generoso = 11,
        Conversador = 12,
        LinceDeLasPraderas = 13,
        RachaDeHierro = 14,
        DecadaCompleta = 15,
        Leyenda = 16,
        Influencer = 18,
        RachaDeOro = 19,
        ViajeroDelTiempo = 20
    }
}
