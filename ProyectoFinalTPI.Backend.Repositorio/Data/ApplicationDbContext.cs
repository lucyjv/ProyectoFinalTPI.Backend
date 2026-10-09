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

            modelBuilder.Entity<Usuario>()
                .ToTable("Usuario")
                .HasDiscriminator<string>("tipo_usuario")
                .HasValue<Usuario>("Usuario")
                .HasValue<Usuario_Personal>("Usuario_Personal")
                .HasValue<Usuario_Marca>("Usuario_Marca")
                .HasValue<Moderador>("Moderador");

            modelBuilder.Entity<Lugar>(entity =>
            {
                entity.ToTable("lugares");
                entity.Property(e => e.Coordenadas)
                      .HasColumnType("geometry(Point, 4326)");
            });

            modelBuilder.Entity<Publicacion>()
                .HasOne(p => p.PublicacionOriginal)
                .WithMany()
                .HasForeignKey(p => p.PublicacionOriginalId)
                .OnDelete(DeleteBehavior.Cascade);

        }
    }
}
