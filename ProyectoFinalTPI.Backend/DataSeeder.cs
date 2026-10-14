using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend
{
    public static class DataSeeder
    {
        public static void Seed(IServiceProvider serviceProvider, IWebHostEnvironment env)
        {
            if (!env.IsDevelopment())
                return;

            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DataSeeder");
            var path = Path.Combine(env.ContentRootPath, "seed.json");

            if (!File.Exists(path))
            {
                logger.LogWarning("No se cargaron datos de demostración: falta {Ruta}.", path);
                return;
            }

            var data = JsonSerializer.Deserialize<SeedData>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("seed.json está vacío.");
            Validar(data);

            // Si falla una publicación, se revierte también la creación de usuarios y lugares.
            using var transaction = db.Database.BeginTransaction();
            var existentes = db.Usuarios.ToList();
            var userMap = new Dictionary<string, Usuario_Personal>(StringComparer.OrdinalIgnoreCase);
            var usuariosCreados = 0;
            var publicacionesCreadas = 0;
            string? passwordHash = null;

            foreach (var datos in data.Users)
            {
                var coincidencias = existentes.Where(u =>
                    string.Equals(u.Username, datos.Username, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(u.Email, datos.Email, StringComparison.OrdinalIgnoreCase)).ToList();

                if (coincidencias.Count > 1)
                    throw new InvalidOperationException($"Usuario ambiguo en seed: {datos.Username}.");

                if (coincidencias.Count == 1)
                {
                    if (coincidencias[0] is not Usuario_Personal personal ||
                        !string.Equals(personal.Username, datos.Username, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"El usuario o email de {datos.Username} ya pertenece a otro perfil.");

                    // No cambiar los datos ni la contraseña de una cuenta existente.
                    userMap.Add(datos.Username, personal);
                    continue;
                }

                passwordHash ??= BCrypt.Net.BCrypt.HashPassword("password123");
                var usuario = new Usuario_Personal
                {
                    Username = datos.Username,
                    Email = datos.Email,
                    Contraseña = passwordHash,
                    Nombre = datos.Nombre,
                    Apellido = datos.Apellido,
                    FechaNacimiento = datos.FechaNacimiento,
                    Reputacion = ReputacionEnum.Errante,
                    FechaCreacion = DateTime.UtcNow,
                    EsAdmin = false
                };
                db.Usuarios.Add(usuario);
                userMap.Add(datos.Username, usuario);
                usuariosCreados++;
            }
            db.SaveChanges();

            foreach (var mem in data.Memories)
            {
                var usuario = userMap[mem.Author];
                var fecha = new DateTime(mem.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                // El ID del JSON no es la PK numérica de PostgreSQL.
                // Reconocer el mismo recuerdo por autor, título y fecha evita duplicarlo al reiniciar.
                if (db.Publicaciones.Any(p =>
                    p.UsuarioId == usuario.Id && p.Titulo == mem.Title && p.Fecha == fecha))
                    continue;

                var url = !string.IsNullOrWhiteSpace(mem.Media?.Url)
                    ? mem.Media.Url : mem.Image ?? string.Empty;
                var tipo = mem.Media?.Kind == "video" ||
                    url.Contains("youtube", StringComparison.OrdinalIgnoreCase)
                    ? MultimediaEnum.Video : MultimediaEnum.Foto;

                db.Publicaciones.Add(new Publicacion
                {
                    Titulo = mem.Title,
                    Descripcion = mem.Description,
                    Fecha = fecha,
                    Categoria = Categoria(mem.Category),
                    Usuario = usuario,
                    Lugar = new Lugar
                    {
                        Nombre = mem.Place,
                        Direccion = mem.Place,
                        Coordenadas = new Point(mem.Lng, mem.Lat) { SRID = 4326 }
                    },
                    FechaCreación = DateTime.UtcNow,
                    UrlMultimedia = url,
                    TipoMultimedia = tipo,
                    EstaOculto = false
                });
                publicacionesCreadas++;
            }

            db.SaveChanges();
            transaction.Commit();
            logger.LogInformation(
                "Seed completado: {Usuarios} usuarios y {Publicaciones} publicaciones nuevas; {Omitidas} publicaciones ya existentes.",
                usuariosCreados, publicacionesCreadas, data.Memories.Count - publicacionesCreadas);
        }

        private static CategoriaEnum Categoria(string categoria)
        {
            if (categoria == "Personales")
                return CategoriaEnum.Recuerdos_Personales;
            if (Enum.TryParse<CategoriaEnum>(categoria, out var resultado) &&
                Enum.IsDefined(resultado))
                return resultado;
            throw new InvalidOperationException($"Categoría desconocida en seed: {categoria}.");
        }

        private static void Validar(SeedData data)
        {
            if (data.Users == null || data.Memories == null)
                throw new InvalidOperationException("seed.json debe contener users y memories.");
            var usuarios = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var u in data.Users)
            {
                if (u == null || string.IsNullOrWhiteSpace(u.Username) ||
                    string.IsNullOrWhiteSpace(u.Nombre) || string.IsNullOrWhiteSpace(u.Apellido) ||
                    string.IsNullOrWhiteSpace(u.Email) || u.FechaNacimiento == default ||
                    !usuarios.Add(u.Username) || !emails.Add(u.Email))
                    throw new InvalidOperationException("Usuario inválido o repetido en seed.json.");
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var recuerdos = new HashSet<(string, string, int)>();
            foreach (var m in data.Memories)
            {
                if (m == null || string.IsNullOrWhiteSpace(m.Id) || !ids.Add(m.Id) ||
                    string.IsNullOrWhiteSpace(m.Title) || string.IsNullOrWhiteSpace(m.Description) ||
                    string.IsNullOrWhiteSpace(m.Place) || string.IsNullOrWhiteSpace(m.Author) ||
                    !usuarios.Contains(m.Author) || m.Year < 1 || m.Year > 9999 ||
                    !double.IsFinite(m.Lat) || !double.IsFinite(m.Lng) ||
                    m.Lat < -90 || m.Lat > 90 || m.Lng < -180 || m.Lng > 180 ||
                    !recuerdos.Add((m.Author.ToUpperInvariant(), m.Title, m.Year)))
                    throw new InvalidOperationException("Publicación inválida o repetida en seed.json.");
                Categoria(m.Category);
            }
        }
    }

    public class SeedData
    {
        public List<SeedUser> Users { get; set; } = new();
        public List<SeedMemory> Memories { get; set; } = new();
    }

    public class SeedUser
    {
        public string Username { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateOnly FechaNacimiento { get; set; }
    }

    public class SeedMemory
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Author { get; set; } = string.Empty;
        public string Place { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Image { get; set; }
        public SeedMedia? Media { get; set; }
    }

    public class SeedMedia
    {
        public string Kind { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}

