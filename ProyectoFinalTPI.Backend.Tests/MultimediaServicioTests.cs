using Microsoft.Extensions.Configuration;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class MultimediaServicioTests
{
    #region Procesamiento de Enlaces Multimedia

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=VBvXwX7bNMk")]
    [InlineData("https://youtu.be/VBvXwX7bNMk?si=compartido")]
    [InlineData("https://m.youtube.com/watch?v=VBvXwX7bNMk&t=30s")]
    [InlineData("https://www.youtube.com/shorts/VBvXwX7bNMk")]
    [InlineData("https://www.youtube.com/live/VBvXwX7bNMk")]
    [InlineData("https://www.youtube.com/embed/VBvXwX7bNMk")]
    [InlineData("https://www.youtube-nocookie.com/embed/VBvXwX7bNMk")]
    public async Task QueVideoDeYoutubeConserveEnlaceSinSubirACloudinary(string url)
    {
        //Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Cloudinary:CloudName"] = "test",
                ["Cloudinary:ApiKey"] = "test",
                ["Cloudinary:ApiSecret"] = "test"
            }).Build();
        var servicio = new MultimediaServicio(configuration);

        //Act
        var resultado = await servicio.SubirAsync(null, null, url, MultimediaEnum.Video);

        //Assert
        Assert.Equal(url, resultado);
    }

    #endregion
}
