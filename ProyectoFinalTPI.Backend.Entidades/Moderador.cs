namespace ProyectoFinalTPI.Backend.Entidades
{
    public class Moderador : Usuario_Personal
    {
        public int ReportesAtendidos { get; set; }
        public DateTime FechaAscenso { get; set; }
    }
}
