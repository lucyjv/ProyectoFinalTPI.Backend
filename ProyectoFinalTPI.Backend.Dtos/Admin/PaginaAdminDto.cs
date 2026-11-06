namespace ProyectoFinalTPI.Backend.Dtos.Admin;

public class PaginaAdminDto<T>
{
    public List<T> Items { get; set; } = [];
    public int Pagina { get; set; }
    public int Tamanio { get; set; }
    public int TotalItems { get; set; }
    public int TotalPaginas => Tamanio == 0 ? 0 : (int)Math.Ceiling((double)TotalItems / Tamanio);
}
