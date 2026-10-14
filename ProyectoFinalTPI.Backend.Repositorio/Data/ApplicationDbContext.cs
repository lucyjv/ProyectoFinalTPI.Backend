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

        // ── Chat ──────────────────────────────────────────────────────────────
        public DbSet<MensajeChat> MensajesChat { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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

            // ── Configuración de MensajeChat ──────────────────────────────────
            modelBuilder.Entity<MensajeChat>(entity =>
            {
                entity.ToTable("mensajes_chat");

                entity.Property(e => e.Contenido)
                      .IsRequired()
                      .HasMaxLength(2000);

                // FK al emisor: si el usuario es eliminado, sus mensajes también.
                entity.HasOne(e => e.Emisor)
                      .WithMany()
                      .HasForeignKey(e => e.EmisorId)
                      .OnDelete(DeleteBehavior.Cascade);

                // FK al destinatario: idem.
                entity.HasOne(e => e.Destinatario)
                      .WithMany()
                      .HasForeignKey(e => e.DestinatarioId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Índice en FechaExpiracion para que la consulta de limpieza
                // sea rápida incluso con millones de mensajes.
                entity.HasIndex(e => e.FechaExpiracion)
                      .HasDatabaseName("IX_mensajes_chat_expiracion");

                // Índice compuesto para acelerar la consulta del historial
                // entre dos usuarios concretos.
                entity.HasIndex(e => new { e.EmisorId, e.DestinatarioId })
                      .HasDatabaseName("IX_mensajes_chat_participantes");
            });
        }
    }
}
