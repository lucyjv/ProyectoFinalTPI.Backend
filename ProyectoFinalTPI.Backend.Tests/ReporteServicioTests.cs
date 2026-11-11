using Moq;
using ProyectoFinalTPI.Backend.Dtos.Reportes;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class ReporteServicioTests
{
    private readonly Mock<IReporteRepositorio> _repositorio = new();

    [Fact]
    public async Task QueCrearReporteNoModereContenido()
    {
        var dto = new CrearReporteDto
        {
            TipoContenido = TipoContenidoReporte.Comentario,
            ContenidoId = 9,
            Motivo = MotivoReporte.Acoso,
            Detalle = "  Insultos reiterados  "
        };
        _repositorio.Setup(r => r.ObtenerObjetivo(
                dto.TipoContenido, 9, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjetivoReporteDto
            {
                UsuarioReportadoId = 7,
                ContenidoSnapshot = "Comentario original",
                ContextoId = 3
            });
        Reporte? guardado = null;
        _repositorio.Setup(r => r.Agregar(It.IsAny<Reporte>(), It.IsAny<CancellationToken>()))
            .Callback<Reporte, CancellationToken>((reporte, _) => guardado = reporte);

        var resultado = await new ReporteServicio(_repositorio.Object).Crear(2, dto);

        Assert.NotNull(guardado);
        Assert.Equal(EstadoReporte.Pendiente, resultado.Estado);
        Assert.Equal("Comentario original", guardado.ContenidoSnapshot);
        Assert.Equal("Insultos reiterados", guardado.Detalle);
    }

    [Fact]
    public async Task QueRechaceReporteDuplicadoPendiente()
    {
        var dto = new CrearReporteDto
        {
            TipoContenido = TipoContenidoReporte.Perfil,
            ContenidoId = 8,
            Motivo = MotivoReporte.Suplantacion
        };
        _repositorio.Setup(r => r.ObtenerObjetivo(
                dto.TipoContenido, 8, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjetivoReporteDto { UsuarioReportadoId = 8 });
        _repositorio.Setup(r => r.ExisteActivo(
                2, dto.TipoContenido, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<ReporteDuplicadoException>(
            () => new ReporteServicio(_repositorio.Object).Crear(2, dto));
    }

    [Fact]
    public async Task QueNoPermitaReportarContenidoPropio()
    {
        var dto = new CrearReporteDto
        {
            TipoContenido = TipoContenidoReporte.Publicacion,
            ContenidoId = 4,
            Motivo = MotivoReporte.Otro
        };
        _repositorio.Setup(r => r.ObtenerObjetivo(
                dto.TipoContenido, 4, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjetivoReporteDto { UsuarioReportadoId = 2 });

        await Assert.ThrowsAsync<ArgumentException>(
            () => new ReporteServicio(_repositorio.Object).Crear(2, dto));
    }
}
