using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos.JuegoDecadas;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public class JuegoDecadasIntegracionTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    [Fact]
    public async Task QueLaApiPublicaMezcleFotosAntiguasYAdjuntosSinExpoVideosRepostsNiOcultos()
    {
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();
        int antiguaId, mixtaId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var autor = new Usuario_Personal { Username = "ELNOSTALGICO", Email = "fuente@example.test", EsAdmin = true };
            var otro = new Usuario_Personal { Username = "otraCuenta", Email = "otra@example.test" };
            var eliminada = new Usuario_Personal { Username = "elNostalgico_borrado", Email = "borrada@example.test", EstaEliminado = true };
            var antigua = Recuerdo(autor, "La estación en los ochenta", 1989, "antigua.jpg");
            var mixta = Recuerdo(autor, "El barrio en 2001", 2001, "portada-no-repetida.jpg");
            mixta.Adjuntos = new List<AdjuntoPublicacion>
            {
                new() { Orden = 0, Tipo = MultimediaEnum.Foto, Url = "https://fotos.example/foto1.jpg" },
                new() { Orden = 1, Tipo = MultimediaEnum.Video, Url = "https://fotos.example/video.mp4" },
                new() { Orden = 2, Tipo = MultimediaEnum.Foto, Url = "https://fotos.example/foto2.jpg" }
            };
            var oculta = Recuerdo(autor, "Oculta", 1995, "oculta.jpg");
            oculta.EstaOculto = true;
            var video = Recuerdo(autor, "Video", 1995, "video.mp4");
            video.TipoMultimedia = MultimediaEnum.Video;
            var sinFoto = Recuerdo(autor, "Solo texto", 1995, "");
            sinFoto.UrlMultimedia = "";
            var repost = Recuerdo(autor, "Compartido", 1995, "repost.jpg");
            repost.PublicacionOriginal = antigua;
            db.Publicaciones.AddRange(antigua, mixta, oculta, video, sinFoto, repost,
                Recuerdo(otro, "Otra cuenta", 1995, "ajena.jpg"),
                Recuerdo(autor, "Visita a Expo Proyecto 2026", 2026, "expo1.jpg"),
                Recuerdo(autor, "Recuerdo EXPO PROYECTO", 1990, "expo2.jpg"),
                Recuerdo(autor, "Una eXpO pRoYeCtO inolvidable", 2000, "expo3.jpg"),
                Recuerdo(autor, "Fuera del rango", 1969, "1969.jpg"),
                Recuerdo(autor, "Fuera del rango", 2030, "2030.jpg"),
                Recuerdo(eliminada, "Otra cuenta eliminada", 1995, "eliminada.jpg"));
            await db.SaveChangesAsync();
            antiguaId = antigua.Id;
            mixtaId = mixta.Id;
        }

        var response = await client.GetAsync("/api/juegos/decadas/fotos");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fotos = (await response.Content.ReadFromJsonAsync<List<FotoJuegoDecadasDto>>())!;

        Assert.Equal(3, fotos.Count);
        Assert.Contains(fotos, f => f.PublicacionId == antiguaId && f.Decada == 1980 && f.Url.EndsWith("antigua.jpg"));
        Assert.Equal(2, fotos.Count(f => f.PublicacionId == mixtaId && f.Decada == 2000));
        Assert.Equal(new[] { "antigua.jpg", "foto1.jpg", "foto2.jpg" },
            fotos.Select(f => f.Url.Split('/').Last()).Order().ToArray());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Usuarios.Where(u => u.Username == "ELNOSTALGICO")
                .ExecuteUpdateAsync(update => update.SetProperty(u => u.EstaEliminado, true));
        }
        Assert.Empty((await client.GetFromJsonAsync<List<FotoJuegoDecadasDto>>("/api/juegos/decadas/fotos"))!);
    }

    [Fact]
    public async Task QueUnaCuentaSinFotosCandidatasDevuelvaUnCatalogoVacioSinAutenticacion()
    {
        await using var factory = new PublicacionesApiFactory(postgres);
        using var client = factory.CreateClient();

        var fotos = await client.GetFromJsonAsync<List<FotoJuegoDecadasDto>>("/api/juegos/decadas/fotos");

        Assert.NotNull(fotos);
        Assert.Empty(fotos);
    }

    private static Publicacion Recuerdo(Usuario_Personal autor, string titulo, int año, string archivo) => new()
    {
        Usuario = autor,
        Titulo = titulo,
        Descripcion = "Foto del lugar",
        Fecha = new DateTime(año, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UrlMultimedia = "https://fotos.example/" + archivo,
        TipoMultimedia = MultimediaEnum.Foto,
        Lugar = new Lugar { Nombre = "Estación", Coordenadas = new Point(-58.38, -34.60) { SRID = 4326 } }
    };
}
