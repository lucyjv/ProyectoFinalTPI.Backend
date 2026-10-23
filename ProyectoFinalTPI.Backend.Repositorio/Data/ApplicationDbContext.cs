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
        public DbSet<SesionUsuario> SesionesUsuario { get; set; }
        public DbSet<Comentario> Comentarios { get; set; }
        public DbSet<ReaccionComentario> ReaccionesComentario { get; set; }
        public DbSet<Grupo> Grupos { get; set; }
        public DbSet<GrupoIntegrante> GruposIntegrantes { get; set; }
        public DbSet<Momento> Momentos { get; set; }
        public DbSet<MomentoAlias> MomentosAlias { get; set; }
        public DbSet<PublicacionMomento> PublicacionesMomentos { get; set; }
        public DbSet<TrabajoAnalisisRecuerdo> TrabajosAnalisisRecuerdos { get; set; }
        public DbSet<Notificacion> Notificaciones { get; set; }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            PrepararIdentidadesMomentos();
            PrepararTrabajosAnalisis();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            PrepararIdentidadesMomentos();
            PrepararTrabajosAnalisis();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void PrepararIdentidadesMomentos()
        {
            ChangeTracker.DetectChanges();
            foreach (var entry in ChangeTracker.Entries<Momento>().Where(e => e.State is EntityState.Added or EntityState.Modified).ToList())
            {
                var m = entry.Entity;
                m.ClaveIdentidad = m.MomentoCanonicoId == null ? IdentidadMomento.Clave(m.Nombre, m.FechaInicio, m.FechaFin) : null;
                m.VersionCatalogo = entry.State == EntityState.Added ? 1 : checked(entry.Property(x => x.VersionCatalogo).OriginalValue + 1);
            }
        }

        private void PrepararTrabajosAnalisis()
        {
            ChangeTracker.DetectChanges();
            foreach (var entry in ChangeTracker.Entries<Publicacion>().ToList())
            {
                var p = entry.Entity;
                if (p.PublicacionOriginalId != null || p.EstaOculto || entry.State is EntityState.Deleted or EntityState.Detached) continue;
                var nuevo = entry.State == EntityState.Added;
                var contenidoCambio = new[] { nameof(Publicacion.Titulo), nameof(Publicacion.Descripcion), nameof(Publicacion.Fecha) }
                    .Any(n => entry.Property(n).IsModified && !Equals(entry.Property(n).OriginalValue, entry.Property(n).CurrentValue));
                if (!nuevo && !contenidoCambio) continue;
                // Una repetición de SaveChanges tras un fallo conserva el trabajo pendiente del contexto.
                if (ChangeTracker.Entries<TrabajoAnalisisRecuerdo>().Any(t => t.State == EntityState.Added && ReferenceEquals(t.Entity.Publicacion, p))) continue;
                p.VersionContenido = nuevo ? 1 : checked(entry.Property(x => x.VersionContenido).OriginalValue + 1);
                var ahora = DateTime.UtcNow;
                TrabajosAnalisisRecuerdos.Add(new TrabajoAnalisisRecuerdo
                {
                    Publicacion = p, VersionContenido = p.VersionContenido,
                    FechaCreacion = ahora, FechaActualizacion = ahora, ProximoIntento = ahora
                });
            }
        }

        // ── Chat ──────────────────────────────────────────────────────────────
        public DbSet<MensajeChat> MensajesChat { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasPostgresExtension("postgis");

            modelBuilder.Entity<Publicacion>().HasIndex(p => p.Fecha);
            modelBuilder.Entity<Publicacion>().Property(p => p.PermitirMomentosAutomaticos).HasDefaultValue(true);
            modelBuilder.Entity<Publicacion>().Property(p => p.VersionContenido).HasDefaultValue(1L).IsConcurrencyToken();
            modelBuilder.ApplyConfiguration(new ConfiguracionTrabajoAnalisis());
            modelBuilder.ApplyConfiguration(new ConfiguracionMomento());
            modelBuilder.ApplyConfiguration(new ConfiguracionMomentoAlias());
            modelBuilder.ApplyConfiguration(new ConfiguracionPublicacionMomento());

            modelBuilder.Entity<Usuario>()
                .ToTable("Usuario")
                .HasDiscriminator<string>("tipo_usuario")
                .HasValue<Usuario>("Usuario")
                .HasValue<Usuario_Personal>("Usuario_Personal")
                .HasValue<Usuario_Marca>("Usuario_Marca")
                .HasValue<Moderador>("Moderador");

            modelBuilder.Entity<Usuario>()
                .HasIndex(usuario => usuario.Email)
                .IsUnique();

            modelBuilder.Entity<Usuario>()
                .HasIndex(usuario => usuario.Username)
                .IsUnique();

            modelBuilder.Entity<SesionUsuario>(entity =>
            {
                entity.ToTable("sesiones_usuario");
                entity.HasKey(sesion => sesion.Id);
                entity.Property(sesion => sesion.RefreshTokenHash).IsRequired().HasMaxLength(64);
                entity.Property(sesion => sesion.UserAgent).HasMaxLength(512);
                entity.Property(sesion => sesion.Version).IsRowVersion();
                entity.HasIndex(sesion => sesion.UsuarioId);
                entity.HasIndex(sesion => sesion.FechaExpiracion);
                entity.HasOne(sesion => sesion.Usuario)
                    .WithMany()
                    .HasForeignKey(sesion => sesion.UsuarioId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

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

            modelBuilder.Entity<Comentario>(entity =>
            {
                entity.ToTable("Comentario");
                entity.Property(comentario => comentario.Contenido).IsRequired();
                entity.HasIndex(comentario => new
                {
                    comentario.PublicacionId,
                    comentario.ComentarioPadreId,
                    comentario.FechaCreacion,
                    comentario.Id
                });
                entity.HasOne(comentario => comentario.Publicacion)
                    .WithMany(publicacion => publicacion.Comentarios)
                    .HasForeignKey(comentario => comentario.PublicacionId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(comentario => comentario.Usuario)
                    .WithMany()
                    .HasForeignKey(comentario => comentario.UsuarioId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(comentario => comentario.ComentarioPadre)
                    .WithMany(comentario => comentario.Respuestas)
                    .HasForeignKey(comentario => comentario.ComentarioPadreId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ReaccionComentario>(entity =>
            {
                entity.ToTable("reacciones_comentario");
                entity.HasKey(reaccion => new { reaccion.ComentarioId, reaccion.UsuarioId });
                entity.HasOne(reaccion => reaccion.Comentario)
                    .WithMany(comentario => comentario.Reacciones)
                    .HasForeignKey(reaccion => reaccion.ComentarioId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(reaccion => reaccion.Usuario)
                    .WithMany()
                    .HasForeignKey(reaccion => reaccion.UsuarioId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Grupo>(entity =>
            {
                entity.ToTable("grupos");
                entity.Property(grupo => grupo.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(grupo => grupo.Descripcion).IsRequired().HasMaxLength(500);
                entity.Property(grupo => grupo.Decada).IsRequired();
                entity.HasIndex(grupo => grupo.CreadorId);
                entity.HasOne(grupo => grupo.Creador)
                    .WithMany()
                    .HasForeignKey(grupo => grupo.CreadorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<GrupoIntegrante>(entity =>
            {
                entity.ToTable("grupos_integrantes");
                entity.HasKey(integrante => new { integrante.GrupoId, integrante.UsuarioId });
                entity.HasIndex(integrante => integrante.UsuarioId);
                entity.HasOne(integrante => integrante.Grupo)
                    .WithMany(grupo => grupo.Integrantes)
                    .HasForeignKey(integrante => integrante.GrupoId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(integrante => integrante.Usuario)
                    .WithMany()
                    .HasForeignKey(integrante => integrante.UsuarioId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Publicacion>()
                .HasOne(publicacion => publicacion.Grupo)
                .WithMany(grupo => grupo.Recuerdos)
                .HasForeignKey(publicacion => publicacion.GrupoId)
                .OnDelete(DeleteBehavior.SetNull);

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
            // ── Configuración de Notificacion ─────────────────────────────────────
            modelBuilder.Entity<Notificacion>(entity =>
            {
                entity.ToTable("notificaciones");

                entity.Property(e => e.Tipo)
                      .HasConversion<string>()
                      .HasMaxLength(50);

                // FK al destinatario: si el usuario es eliminado, sus notificaciones también.
                entity.HasOne(e => e.UsuarioDestinatario)
                      .WithMany()
                      .HasForeignKey(e => e.UsuarioDestinatarioId)
                      .OnDelete(DeleteBehavior.Cascade);

                // FK al emisor: opcional. Si el emisor es eliminado, la FK queda en null.
                entity.HasOne(e => e.Emisor)
                      .WithMany()
                      .HasForeignKey(e => e.EmisorId)
                      .OnDelete(DeleteBehavior.SetNull);

                // Índice principal: obtener notificaciones de un usuario ordenadas por fecha.
                entity.HasIndex(e => new { e.UsuarioDestinatarioId, e.FechaCreacion })
                      .HasDatabaseName("IX_notificaciones_usuario_fecha");

                // Índice para la limpieza de expiradas.
                entity.HasIndex(e => e.FechaExpiracion)
                      .HasDatabaseName("IX_notificaciones_expiracion");

                // Índice para ExisteNotificacionPendienteAsync (unicidad por tipo+publicación).
                entity.HasIndex(e => new { e.UsuarioDestinatarioId, e.Tipo, e.PublicacionId, e.Leida })
                      .HasDatabaseName("IX_notificaciones_pendiente");
            });
            // ──────────────────────────────────────────────────────────────────────
        }
    }
}
