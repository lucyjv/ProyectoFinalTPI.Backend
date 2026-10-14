using Moq;
using ProyectoFinalTPI.Backend.Dtos.Chat;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ChatServicioTests
{
    // ──────────────────────────────────────────────
    // EnviarMensajeAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task EnviarMensaje_llama_a_repositorio_con_datos_correctos()
    {
        var repositorioMock = new Mock<IMensajeChatRepositorio>();
        var servicio = new ChatServicio(repositorioMock.Object);

        await servicio.EnviarMensajeAsync(
            emisorId: 1,
            emisorUsername: "usuarioA",
            destinatarioId: 2,
            contenido: "Hola!");

        // Verificar que GuardarAsync fue llamado exactamente una vez
        // con los valores correctos en la entidad
        repositorioMock.Verify(r => r.GuardarAsync(
            It.Is<MensajeChat>(m =>
                m.EmisorId == 1 &&
                m.DestinatarioId == 2 &&
                m.Contenido == "Hola!"
            )), Times.Once);
    }

    [Fact]
    public async Task EnviarMensaje_devuelve_dto_con_datos_del_mensaje()
    {
        var repositorioMock = new Mock<IMensajeChatRepositorio>();
        var servicio = new ChatServicio(repositorioMock.Object);

        var resultado = await servicio.EnviarMensajeAsync(
            emisorId: 1,
            emisorUsername: "usuarioA",
            destinatarioId: 2,
            contenido: "Hola!");

        Assert.Equal(1, resultado.EmisorId);
        Assert.Equal("usuarioA", resultado.EmisorUsername);
        Assert.Equal(2, resultado.DestinatarioId);
        Assert.Equal("Hola!", resultado.Contenido);
    }

    [Fact]
    public async Task EnviarMensaje_fija_fecha_expiracion_en_7_dias()
    {
        var repositorioMock = new Mock<IMensajeChatRepositorio>();
        var servicio = new ChatServicio(repositorioMock.Object);

        var antes = DateTime.UtcNow;
        await servicio.EnviarMensajeAsync(1, "usuarioA", 2, "Hola!");
        var despues = DateTime.UtcNow;

        // Capturar la entidad que se pasó al repositorio
        repositorioMock.Verify(r => r.GuardarAsync(
            It.Is<MensajeChat>(m =>
                // La expiración debe ser FechaEnvio + 7 días (con margen de 1 segundo)
                m.FechaExpiracion >= antes.AddDays(7) &&
                m.FechaExpiracion <= despues.AddDays(7).AddSeconds(1)
            )), Times.Once);
    }

    [Fact]
    public async Task EnviarMensaje_fecha_envio_es_utc_reciente()
    {
        var repositorioMock = new Mock<IMensajeChatRepositorio>();
        var servicio = new ChatServicio(repositorioMock.Object);

        var antes = DateTime.UtcNow;
        var resultado = await servicio.EnviarMensajeAsync(1, "usuarioA", 2, "Hola!");
        var despues = DateTime.UtcNow;

        Assert.InRange(resultado.FechaEnvio, antes, despues);
    }

    // ──────────────────────────────────────────────
    // ObtenerHistorialAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ObtenerHistorial_devuelve_mensajes_mapeados_a_dto()
    {
        var repositorioMock = new Mock<IMensajeChatRepositorio>();

        // Preparar datos falsos que devuelve el repositorio
        var emisorFalso = new Usuario { Id = 1, Username = "usuarioA" };
        var mensajesFalsos = new List<MensajeChat>
        {
            new() {
                Id = 10,
                EmisorId = 1,
                Emisor = emisorFalso,
                DestinatarioId = 2,
                Contenido = "Primer mensaje",
                FechaEnvio = DateTime.UtcNow.AddMinutes(-10)
            },
            new() {
                Id = 11,
                EmisorId = 1,
                Emisor = emisorFalso,
                DestinatarioId = 2,
                Contenido = "Segundo mensaje",
                FechaEnvio = DateTime.UtcNow.AddMinutes(-5)
            }
        };

        repositorioMock
            .Setup(r => r.ObtenerHistorialAsync(1, 2))
            .ReturnsAsync(mensajesFalsos);

        var servicio = new ChatServicio(repositorioMock.Object);

        var resultado = (await servicio.ObtenerHistorialAsync(1, 2)).ToList();

        Assert.Equal(2, resultado.Count);
        Assert.Equal("Primer mensaje", resultado[0].Contenido);
        Assert.Equal("usuarioA", resultado[0].EmisorUsername);
        Assert.Equal("Segundo mensaje", resultado[1].Contenido);
    }

    [Fact]
    public async Task ObtenerHistorial_repositorio_sin_mensajes_devuelve_lista_vacia()
    {
        var repositorioMock = new Mock<IMensajeChatRepositorio>();
        repositorioMock
            .Setup(r => r.ObtenerHistorialAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<MensajeChat>());

        var servicio = new ChatServicio(repositorioMock.Object);

        var resultado = await servicio.ObtenerHistorialAsync(1, 2);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ObtenerHistorial_llama_al_repositorio_con_los_ids_correctos()
    {
        var repositorioMock = new Mock<IMensajeChatRepositorio>();
        repositorioMock
            .Setup(r => r.ObtenerHistorialAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<MensajeChat>());

        var servicio = new ChatServicio(repositorioMock.Object);

        await servicio.ObtenerHistorialAsync(usuarioAId: 5, usuarioBId: 7);

        repositorioMock.Verify(r => r.ObtenerHistorialAsync(5, 7), Times.Once);
    }

    [Fact]
    public async Task ObtenerHistorial_emisor_null_usa_string_vacio_para_username()
    {
        var repositorioMock = new Mock<IMensajeChatRepositorio>();

        // Simular un mensaje cuyo Emisor no fue cargado (navigation property null)
        var mensajesSinNavigation = new List<MensajeChat>
        {
            new() {
                Id = 1,
                EmisorId = 1,
                Emisor = null!, // navigation property no cargada
                DestinatarioId = 2,
                Contenido = "Mensaje",
                FechaEnvio = DateTime.UtcNow
            }
        };

        repositorioMock
            .Setup(r => r.ObtenerHistorialAsync(1, 2))
            .ReturnsAsync(mensajesSinNavigation);

        var servicio = new ChatServicio(repositorioMock.Object);

        var resultado = (await servicio.ObtenerHistorialAsync(1, 2)).ToList();

        // No debe lanzar NullReferenceException
        Assert.Equal(string.Empty, resultado[0].EmisorUsername);
    }
}
