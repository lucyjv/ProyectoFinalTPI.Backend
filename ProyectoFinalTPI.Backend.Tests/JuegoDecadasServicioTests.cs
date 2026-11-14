using Moq;
using ProyectoFinalTPI.Backend.Dtos.JuegoDecadas;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class JuegoDecadasServicioTests
{
    [Fact]
    public async Task QueSeConserveLaPublicacionYSeCalculeLaDecadaConLaFechaDelRecuerdo()
    {
        var repositorio = new Mock<IJuegoDecadasRepositorio>(MockBehavior.Strict);
        using var cancelacion = new CancellationTokenSource();
        repositorio.Setup(r => r.ObtenerFotos("elNostalgico", "Expo Proyecto",
            Fecha(1970), Fecha(2030), cancelacion.Token))
            .ReturnsAsync(new List<FotoCandidataJuegoDecadasDto>
            {
                new(10, "https://fotos.example/plaza.jpg", Fecha(1989)),
                new(11, "https://fotos.example/centro.jpg", Fecha(2000)),
                new(12, "https://fotos.example/estacion.jpg", Fecha(2026))
            });

        var fotos = await new JuegoDecadasServicio(repositorio.Object).ObtenerFotos(cancelacion.Token);

        Assert.Equal(new[] { 1980, 2000, 2020 }, fotos.Select(f => f.Decada));
        Assert.Equal(new[] { 10, 11, 12 }, fotos.Select(f => f.PublicacionId));
        repositorio.VerifyAll();
    }

    [Fact]
    public async Task QueNoSeRepitanImagenesNiSeIncluyanFechasSinOpcionOUrlsInvalidas()
    {
        var repositorio = new Mock<IJuegoDecadasRepositorio>();
        repositorio.Setup(r => r.ObtenerFotos(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FotoCandidataJuegoDecadasDto>
            {
                new(1, " https://fotos.example/unica.jpg ", Fecha(1970)),
                new(2, "https://fotos.example/unica.jpg", Fecha(1970)),
                new(3, "https://fotos.example/anterior.jpg", Fecha(1969)),
                new(4, "https://fotos.example/futura.jpg", Fecha(2030)),
                new(5, "javascript:alert(1)", Fecha(1990)),
                new(6, "", Fecha(1990)),
                new(0, "https://fotos.example/sin-publicacion.jpg", Fecha(1990))
            });

        var foto = Assert.Single(await new JuegoDecadasServicio(repositorio.Object).ObtenerFotos());

        Assert.Equal(1, foto.PublicacionId);
        Assert.Equal("https://fotos.example/unica.jpg", foto.Url);
    }

    private static DateTime Fecha(int año) => new(año, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}
