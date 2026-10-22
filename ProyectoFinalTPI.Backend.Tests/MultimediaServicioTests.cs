using Microsoft.Extensions.Configuration;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class MultimediaServicioTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=VBvXwX7bNMk")]
    [InlineData("https://youtu.be/VBvXwX7bNMk?si=compartido")]
    [InlineData("https://m.youtube.com/watch?v=VBvXwX7bNMk&t=30s")]
    [InlineData("https://www.youtube.com/shorts/VBvXwX7bNMk")]
    [InlineData("https://www.youtube.com/live/VBvXwX7bNMk")]
    [InlineData("https://www.youtube.com/embed/VBvXwX7bNMk")]
    [InlineData("https://www.youtube-nocookie.com/embed/VBvXwX7bNMk")]
    public async Task Video_de_youtube_conserva_enlace_sin_subir_a_cloudinary(string url)
    {
        // Las credenciales ficticias hacen que una subida accidental no pueda pasar.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Cloudinary:CloudName"] = "test",
                ["Cloudinary:ApiKey"] = "test",
                ["Cloudinary:ApiSecret"] = "test"
            }).Build();
        var servicio = new MultimediaServicio(configuration);

        var resultado = await servicio.SubirAsync(null, null, url, MultimediaEnum.Video);

        Assert.Equal(url, resultado);
    }
}
