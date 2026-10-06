using ProyectoFinalTPI.Backend.Entidades;
using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;

namespace ProyectoFinalTPI.Backend.Repositorio
{
    public class PublicacionRepositorio : IPublicacionRepositorio
    {
        private readonly ApplicationDbContext _context;

        public PublicacionRepositorio(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Publicacion> ObtenerTodasPublicaciones()
        {
            return _context.Publicaciones
                           .Where(p => !p.EstaOculto) 
                           .Include(p => p.Usuario)
                           .ToList();
        }

        public void GuardarPublicacion(Publicacion publicacion)
        {
            _context.Publicaciones.Add(publicacion);
            _context.SaveChanges();
        }

        public List<Publicacion> ObtenerPublicacionPorCategoria(CategoriaEnum categoria)
        {
            return _context.Publicaciones
                           .Where(p => p.Categoria == categoria && !p.EstaOculto)
                           .ToList();
        }

        public async Task<int> Crear(Publicacion publicacion)
        {
            await _context.Publicaciones.AddAsync(publicacion);
            await _context.SaveChangesAsync();
            return publicacion.Id;
        }


    }
}
