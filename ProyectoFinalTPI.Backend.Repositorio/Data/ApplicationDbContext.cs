using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using NetTopologySuite.Geometries;

namespace ProyectoFinalTPI.Backend.Repositorio.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Publicacion> Publicaciones { get; set; }
        public DbSet<Lugar> Lugares { get; set; }
        public DbSet<Moderador> Moderadores { get; set; }
        public DbSet<Moderación> Moderaciones { get; set; }
        public DbSet<Usuario_Interactivo> Usuario_Interactivos { get; set; }
        public DbSet<Usuario_Marca> Usuario_Marcas { get; set; }
        public DbSet<Usuario_Personal> Usuario_Personales { get; set; }



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Le indicamos explícitamente a EF Core que use PostGIS para la propiedad de ubicación
            modelBuilder.HasPostgresExtension("postgis");

            modelBuilder.Entity<Usuario>().ToTable("usuarios");

            modelBuilder.Entity<Usuario_Interactivo>().ToTable("usuarios_interactivos");

            modelBuilder.Entity<Usuario_Personal>().ToTable("usuarios_personales");

            modelBuilder.Entity<Usuario_Marca>().ToTable("usuarios_marcas");

            modelBuilder.Entity<Moderador>().ToTable("moderadores");

            modelBuilder.Entity<Lugar>(entity =>
            {
                entity.ToTable("lugares");
                entity.Property(e => e.Coordenadas)
                      .HasColumnType("geometry(Point, 4326)");
            });


        }
    }
}
