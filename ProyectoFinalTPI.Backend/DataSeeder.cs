using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend
{
    public static class DataSeeder
    {
        public static void Seed(IServiceProvider serviceProvider, IWebHostEnvironment env)
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (!db.Usuarios.Any())
            {
                var seedJsonPath = Path.Combine(env.ContentRootPath, "..", "..", "seed.json");
                if (File.Exists(seedJsonPath))
                {
                    var seedJson = File.ReadAllText(seedJsonPath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var data = JsonSerializer.Deserialize<SeedData>(seedJson, options);

                    if (data != null)
                    {
                        var userMap = new Dictionary<string, Usuario_Personal>();
                        foreach (var userName in data.users)
                        {
                            var user = new Usuario_Personal
                            {
                                Username = userName,
                                Email = $"{userName.ToLower().Replace(" ", "").Replace("é", "e").Replace("á", "a").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")}@example.com",
                                Contraseña = "password123",
                                EsAdmin = false,
                                Nombre = userName,
                                Apellido = "",
                                FechaNacimiento = new DateOnly(1980, 1, 1),
                                Reputacion = ReputacionEnum.Errante,
                                FechaCreacion = DateTime.UtcNow
                            };
                            db.Usuarios.Add(user);
                            userMap[userName] = user;
                        }
                        db.SaveChanges();

                        foreach (var mem in data.memories)
                        {
                            var lugar = new Lugar
                            {
                                Nombre = mem.place ?? "Desconocido",
                                Direccion = mem.place ?? "Desconocido",
                                Coordenadas = new Point(mem.lng, mem.lat) { SRID = 4326 }
                            };
                            db.Lugares.Add(lugar);
                            db.SaveChanges();

                            CategoriaEnum catEnum = CategoriaEnum.Lugares;
                            if (mem.category == "Música") catEnum = CategoriaEnum.Música;
                            else if (mem.category == "Cine") catEnum = CategoriaEnum.Cine;
                            else if (mem.category == "Televisión") catEnum = CategoriaEnum.Televisión;
                            else if (mem.category == "Videojuegos") catEnum = CategoriaEnum.Videojuegos;
                            else if (mem.category == "Acontecimientos") catEnum = CategoriaEnum.Acontecimientos;
                            else if (mem.category == "Personales") catEnum = CategoriaEnum.Recuerdos_Personales;

                            string urlMult = string.Empty;
                            MultimediaEnum tipoMult = MultimediaEnum.Foto;

                            if (mem.media != null && !string.IsNullOrEmpty(mem.media.url)) {
                                urlMult = mem.media.url;
                                tipoMult = mem.media.kind == "video" ? MultimediaEnum.Video : MultimediaEnum.Foto;
                            } else if (!string.IsNullOrEmpty(mem.image)) {
                                urlMult = mem.image;
                                tipoMult = mem.image.Contains("youtube") ? MultimediaEnum.Video : MultimediaEnum.Foto;
                            }

                            var pub = new Publicacion
                            {
                                Titulo = mem.title ?? "Sin Título",
                                Descripcion = mem.description ?? "",
                                Fecha = new DateTime(mem.year > 1000 ? mem.year : 2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                                LugarId = lugar.Id,
                                Categoria = catEnum,
                                UsuarioId = userMap[mem.author].Id,
                                FechaCreación = DateTime.UtcNow,
                                UrlMultimedia = urlMult,
                                TipoMultimedia = tipoMult
                            };
                            db.Publicaciones.Add(pub);
                        }
                        db.SaveChanges();
                    }
                }
                else
                {
                    db.Usuarios.Add(new Usuario
                    {
                        Id = 1,
                        Username = "DemoUser",
                        Email = "demo@example.com",
                        Contraseña = "password123",
                        EsAdmin = false
                    });
                    db.SaveChanges();
                }
            }
        }
    }

    public class SeedData
    {
        public List<string> users { get; set; } = new List<string>();
        public List<SeedMemory> memories { get; set; } = new List<SeedMemory>();
    }

    public class SeedMemory
    {
        public string id { get; set; } = string.Empty;
        public string title { get; set; } = string.Empty;
        public int year { get; set; }
        public string author { get; set; } = string.Empty;
        public string place { get; set; } = string.Empty;
        public double lat { get; set; }
        public double lng { get; set; }
        public string description { get; set; } = string.Empty;
        public string category { get; set; } = string.Empty;
        public string source { get; set; } = string.Empty;
        public string image { get; set; } = string.Empty;
        public SeedMedia media { get; set; }
    }

    public class SeedMedia
    {
        public string kind { get; set; } = string.Empty;
        public string url { get; set; } = string.Empty;
    }
}

