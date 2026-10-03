namespace ProyectoFinalTPI.Backend.Entidades
{
    public class Usuario_Marca : Usuario_Interactivo
    {
        public string NombreEmpresa {  get; set; } = string.Empty;
        public string Cuit {  get; set; } = string.Empty;
        public string WebOficial { get; set; } = string.Empty;
    }
}
