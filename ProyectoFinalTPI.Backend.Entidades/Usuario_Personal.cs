namespace ProyectoFinalTPI.Backend.Entidades
{
    public class Usuario_Personal : Usuario_Interactivo
    {
        public string Nombre {  get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public DateOnly FechaNacimiento { get; set; }
        public ReputacionEnum Reputacion { get; set; }
    }

    public enum ReputacionEnum
    {
        Errante,
        Nostalgico,
        Aedo,
        Mnnemónide
    }
}
