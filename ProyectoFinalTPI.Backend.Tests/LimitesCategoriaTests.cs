using Moq;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Entidades.Categorias;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class LimitesCategoriaTests
{
    [Theory]
    [InlineData("2026-11-15T02:59:59Z", "2026-11-08")]
    [InlineData("2026-11-15T03:00:00Z", "2026-11-15")]
    public async Task LaSemanaCambiaElDomingoALasCeroDeArgentina(string ahora, string semana)
    {
        var repo = Preparar(ReputacionEnum.Errante);
        var servicio = new CategoriaServicio(repo.Object, new RelojCategoria(DateTimeOffset.Parse(ahora)));
        var estado = await servicio.ObtenerEstado(1);
        Assert.Equal(DateOnly.Parse(semana), estado.SemanaInicio);
        Assert.Equal(5, estado.PublicacionesRestantes);
    }

    [Fact]
    public async Task UnaCuentaQueYaPublicoNoVuelveAErranteAunqueBorreSuRecuerdo()
    {
        var repo = Preparar(ReputacionEnum.Errante);
        repo.Setup(r => r.TieneRecuerdos(1, default)).ReturnsAsync(true);
        var estado = await new CategoriaServicio(repo.Object).ObtenerEstado(1);
        Assert.Equal("Nostalgico", estado.Categoria.Codigo);
    }

    [Theory]
    [InlineData(ReputacionEnum.Errante, 1)]
    [InlineData(ReputacionEnum.Nostalgico, 1)]
    [InlineData(ReputacionEnum.Aedo, 1)]
    [InlineData(ReputacionEnum.Mnnemónide, 4)]
    public async Task LaCantidadDeAdjuntosDependeDeLaCategoria(ReputacionEnum reputacion, int maximo)
    {
        var servicio = new CategoriaServicio(Preparar(reputacion).Object);
        Assert.Equal(maximo, (await servicio.ObtenerEstado(1)).MaximoAdjuntos);
        await servicio.ValidarCreacion(1, maximo, false);
        if (maximo == 1) await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.ValidarCreacion(1, 2, false));
        await Assert.ThrowsAsync<ArgumentException>(() => servicio.ValidarCreacion(1, 5, false));
    }

    [Fact]
    public async Task ErrantePuedePublicarElPrimeroPeroNoComentarNiPuntuarNiRepostear()
    {
        var servicio = new CategoriaServicio(Preparar(ReputacionEnum.Errante).Object);
        await servicio.ValidarCreacion(1, 0, false);
        foreach (var permiso in new[] { PermisoCuenta.Comentar, PermisoCuenta.PuntuarPublicacion, PermisoCuenta.Compartir, PermisoCuenta.EtiquetarUsuario })
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.ExigirPermiso(1, permiso));
    }

    [Theory]
    [InlineData(ReputacionEnum.Nostalgico, false)]
    [InlineData(ReputacionEnum.Aedo, true)]
    public async Task LaMusicaSePermiteDesdeAedo(ReputacionEnum reputacion, bool permitido)
    {
        var servicio = new CategoriaServicio(Preparar(reputacion).Object);
        if (permitido) await servicio.ValidarCreacion(1, 0, true);
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.ValidarCreacion(1, 0, true));
    }

    [Fact]
    public async Task ElCupoAgotadoSeRechazaAntesDeGuardar()
    {
        var repo = Preparar(ReputacionEnum.Nostalgico);
        repo.Setup(r => r.ObtenerConsumo(1, It.IsAny<DateOnly>(), default)).ReturnsAsync(5);
        var servicio = new CategoriaServicio(repo.Object);
        await Assert.ThrowsAsync<CupoPublicacionesAgotadoException>(() => servicio.CrearRecuerdo(new Publicacion { UsuarioId = 1 }));
        repo.Verify(r => r.CrearConCupo(It.IsAny<Publicacion>(), It.IsAny<DateOnly>(), It.IsAny<int?>(), default), Times.Never);
    }

    private static Mock<ICategoriaRepositorio> Preparar(ReputacionEnum reputacion)
    {
        var repo = new Mock<ICategoriaRepositorio>();
        repo.Setup(r => r.ObtenerUsuario(1, default)).ReturnsAsync(new Usuario_Personal { Id = 1, Reputacion = reputacion });
        return repo;
    }
}

public sealed class RelojCategoria(DateTimeOffset ahora) : TimeProvider
{
    public DateTimeOffset Ahora { get; set; } = ahora;
    public override DateTimeOffset GetUtcNow() => Ahora;
}