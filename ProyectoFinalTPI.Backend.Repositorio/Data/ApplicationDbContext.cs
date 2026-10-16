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
        public DbSet<Logro> Logro { get; set; }
        public DbSet<Logros_Usuarios> Logros_Usuarios { get; set; }



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

            modelBuilder.Entity<Logro>()
                .ToTable("logros");

            modelBuilder.Entity<Logros_Usuarios>(entity =>
            {
                entity.ToTable("logros_usuarios");

                entity.HasKey(ul => new { ul.UsuarioId, ul.LogroId });

                entity.HasOne(ul => ul.Usuario)
                      .WithMany(u => u.LogrosUsuarios) 
                      .HasForeignKey(ul => ul.UsuarioId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ul => ul.Logro)
                      .WithMany(l => l.LogrosUsuarios)
                      .HasForeignKey(ul => ul.LogroId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<Logro>().HasData(
                new Logro { Id = 1, Titulo = "Primer Paso", Descripcion = "Creaste tu primer recuerdo en el mapa.", PuntosRecompensa = 0, UrlIcono = "." },
                new Logro { Id = 2, Titulo = "Cronista Instantáneo", Descripcion = "Creaste 5 recuerdos cercanos a vos.", PuntosRecompensa = 0, UrlIcono = "." },
                new Logro { Id = 3, Titulo = "Trotamundos", Descripcion = "Cruzaste fronteras para expandir la memoria. Dejaste anclas emocionales en 3 localidades distintas.", PuntosRecompensa = 0, UrlIcono = "." },
                new Logro { Id = 4, Titulo = "Voz de una Época", Descripcion = "Tus vivencias conectan de forma única 3 décadas distintas.", PuntosRecompensa = 0, UrlIcono = "." }, 
                new Logro { Id = 5, Titulo = "Década Completa", Descripcion = "Viviste, recordaste y conquistaste más de medio siglo. Tenés al menos un recuerdo en cada una de las 6 décadas.", PuntosRecompensa = 0, UrlIcono = "." },
                new Logro { Id = 6, Titulo = "Popular", Descripcion = "Tu historia tocó el corazón de la comunidad. Lograste que un recuerdo supere los 20 puntos.", PuntosRecompensa = 0, UrlIcono = "." },
                new Logro { Id = 7, Titulo = "Leyenda", Descripcion = "Tu recuerdo es oficialmente un clásico imborrable. Lograste un recuerdo con más de 100 puntos.", PuntosRecompensa = 0, UrlIcono = "." }, 
                new Logro { Id = 8, Titulo = "Generoso", Descripcion = "Creés que las buenas historias merecen recompensa. Apoyaste a otros creadores distribuyendo 100 puntos de valor.", PuntosRecompensa = 0, UrlIcono = "." },
                new Logro { Id = 9, Titulo = "Mecenas", Descripcion = "La plataforma es un lugar mejor gracias a vos. Financiaste la comunidad entregando 1000 puntos en total.", PuntosRecompensa = 0, UrlIcono = "." }, 
                new Logro { Id = 10, Titulo = "Primera Palabra", Descripcion = "Tu primer comentario encendió la chispa de la conversación.", PuntosRecompensa = 0, UrlIcono = "." },
                new Logro { Id = 11, Titulo = "Conversador", Descripcion = "El debate y la tertulia son lo tuyo. Llevás 50 comentarios uniendo cabos, debatiendo y sumando anécdotas en historias ajenas.", PuntosRecompensa = 0, UrlIcono = "." },
                new Logro { Id = 12, Titulo = "Corazón Abierto", Descripcion = "No te guardás los elogios. Alentaste a la comunidad dejando 100 likes en comentarios ajenos.", PuntosRecompensa = 0, UrlIcono = "." },
                new Logro { Id = 13, Titulo = "Con Amigos", Descripcion = "Seguís de cerca los pasos y las memorias de 10 usuarios distintos.", PuntosRecompensa = 0, UrlIcono = "." }, 
                new Logro { Id = 14, Titulo = "Influencer", Descripcion = "Cuando vos hablás, la gente se detiene a escuchar. Llegaste a los 50 seguidores.", PuntosRecompensa = 0, UrlIcono = "." },
                new Logro { Id = 15, Titulo = "Tendencia", Descripcion = "Tocaste una fibra sensible y tu recuerdo se transformó en un imán. Tu recuerdo es el centro de un hilo colaborativo.", PuntosRecompensa = 0, UrlIcono = "." },
                new Logro { Id = 16, Titulo = "Historiador", Descripcion = "Conectaste el pasado personal con la historia grande. Vinculaste un recuerdo tuyo con una efeméride histórica oficial.", PuntosRecompensa = 0, UrlIcono = "." },
                new Logro { Id = 17, Titulo = "Fotógrafo del Recuerdo", Descripcion = "Tus recuerdos ahora tienen color, textura y rostro. Lograste subir 10 historias acompañadas por imágenes que reviven el pasado.", PuntosRecompensa = 0, UrlIcono = "." },
                new Logro { Id = 18, Titulo = "Racha de Hierro", Descripcion = "7 días seguidos viajando en el tiempo sin mirar atrás.", PuntosRecompensa = 0, UrlIcono = "." },
                new Logro { Id = 19, Titulo = "Racha de Oro", Descripcion = "Un mes entero dedicado a revivir el pasado. Tu constancia es inquebrantable: 30 días seguidos aportando a la máquina del tiempo.", PuntosRecompensa = 0, UrlIcono = "." }, 
                new Logro { Id = 20, Titulo = "Coleccionista", Descripcion = "Agrupaste tus memorias con precisión quirúrgica creando 3 álbumes o colecciones conceptuales distintas.", PuntosRecompensa = 0, UrlIcono = "." }





);

        }
    }
}
