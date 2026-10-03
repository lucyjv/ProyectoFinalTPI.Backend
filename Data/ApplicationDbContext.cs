using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<Usuario> Usuario { get; set; }
        public DbSet<Publicacion> Publicacion { get; set; }
        public DbSet<Lugar> Lugar { get; set; }
        public DbSet<Moderador> Moderador { get; set; }
        public DbSet<Moderación> Moderacion { get; set; }
        public DbSet<Usuario_Interactivo> Usuario_Interactivo { get; set; }
        public DbSet<Usuario_Marca> Usuario_Marca { get; set; }
        public DbSet<Usuario_Personal> Usuario_Personal { get; set; }



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Le indicamos explícitamente a EF Core que use PostGIS para la propiedad de ubicación
            modelBuilder.HasPostgresExtension("postgis");

            modelBuilder.Entity<Usuario>()
            .HasDiscriminator<string>("tipo_usuario")
            .HasValue<Usuario_Personal>("Personal")
            .HasValue<Usuario_Marca>("Marca")
            .HasValue<Moderador>("Moderador");
        }
    }
}
