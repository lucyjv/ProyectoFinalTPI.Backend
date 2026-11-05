using Moq;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AdminPublicacionServicioTests
{
    private readonly Mock<IAdminPublicacionRepositorio> _repositorio = new();
    private readonly Mock<INotificacionServicio> _notificaciones = new();

    [Fact]
    public async Task QueOcultarGuardeMotivoYNotifiqueAlAutor()
    {
        var publicacion = CrearPublicacion();
        _repositorio.Setup(r => r.Obtener(3, It.IsAny<CancellationToken>())).ReturnsAsync(publicacion);
        var servicio = new AdminPublicacionServicio(_repositorio.Object, _notificaciones.Object);
        await servicio.CambiarVisibilidad(3,
            new CambiarVisibilidadPublicacionDto { Ocultar = true, Motivo = " Incumple normas " });
        Assert.True(publicacion.EstaOculto);
        Assert.Equal("Incumple normas", publicacion.MotivoOculto);
        _notificaciones.Verify(n => n.NotificarAdministrativaAsync(8, 3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueActualizarValideYModifiqueCamposPermitidos()
    {
        var publicacion = CrearPublicacion();
        _repositorio.Setup(r => r.Obtener(3, It.IsAny<CancellationToken>())).ReturnsAsync(publicacion);
        var servicio = new AdminPublicacionServicio(_repositorio.Object, _notificaciones.Object);
        var resultado = await servicio.Actualizar(3, new ActualizarPublicacionAdminDto
        {
            Titulo = " Nuevo ", Descripcion = " Descripción ", Fecha = new DateTime(1999, 1, 1),
            Categoria = CategoriaEnum.Cine
        });
        Assert.Equal("Nuevo", resultado.Titulo);
        Assert.Equal(CategoriaEnum.Cine, publicacion.Categoria);
        _repositorio.Verify(r => r.Guardar(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Publicacion CrearPublicacion() => new()
    {
        Id = 3, Titulo = "Anterior", Descripcion = "Texto", UsuarioId = 8,
        Usuario = new Usuario_Personal { Id = 8, Username = "autor" }
    };
}
