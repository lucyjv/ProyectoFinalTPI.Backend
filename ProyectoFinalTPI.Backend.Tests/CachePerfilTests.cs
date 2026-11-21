using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio.Cache;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class CachePerfilTests
{
    private static CacheLecturasPublicas CrearCache() => new(
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
        NullLogger<CacheLecturasPublicas>.Instance);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QueElPerfilCompletoSeReutiliceSinCompartirLoSigoEntreVisitantes(bool porUsername)
    {
        var cache = CrearCache();
        var usuarios = new Mock<IUsuarioRepositorio>(MockBehavior.Strict);
        var logros = new Mock<ILogroServicio>(MockBehavior.Strict);
        var seguimiento = new Mock<ISeguimientoServicio>(MockBehavior.Strict);
        var usuario = new Usuario_Personal
        {
            Id = 7, Username = "ana", Nombre = "Ana", Apellido = "Perez", FotoPerfilUrl = "foto.png",
            Email = "privado@example.com", Contraseña = "secreto", PuntosNostalgia = 42
        };
        usuarios.Setup(r => r.ObtenerPorIdAsync(7)).ReturnsAsync(usuario);
        usuarios.Setup(r => r.ObtenerPorUsernameAsync("ana", default)).ReturnsAsync(usuario);
        usuarios.Setup(r => r.ContarPublicacionesVisiblesAsync(7, default)).ReturnsAsync(3);
        logros.Setup(l => l.ObtenerCatalogoParaUsuario(7)).ReturnsAsync(new List<LogroEstadoDto>
        {
            new() { Id = 1, Titulo = "Logro", Desbloqueado = true }
        });
        seguimiento.Setup(s => s.ListarSeguidores(7, default)).ReturnsAsync(new List<AutorResumenDto> { new() { Id = 8 } });
        seguimiento.Setup(s => s.ListarSeguidos(7, default)).ReturnsAsync(new List<AutorResumenDto> { new() { Id = 9 } });
        seguimiento.Setup(s => s.ObtenerSeguidosIdsAsync(8, default)).ReturnsAsync(new[] { 7 });
        seguimiento.Setup(s => s.ObtenerSeguidosIdsAsync(9, default)).ReturnsAsync(Array.Empty<int>());
        var servicio = new UsuarioServicio(usuarios.Object, logros.Object, seguimiento.Object, cache: cache);
        Task<UsuarioPerfilDto> Leer(int? visitante) => porUsername
            ? servicio.ObtenerPerfilPorUsernameAsync("ana", visitante)
            : servicio.ObtenerPerfilPorIdAsync(7, visitante);

        var primero = await Leer(8);
        var segundo = await Leer(9);
        var anonimo = await Leer(null);
        Assert.True(primero.LoSigo);
        Assert.False(segundo.LoSigo);
        Assert.False(anonimo.LoSigo);
        Assert.Equal("Ana", segundo.Nombre);
        Assert.Equal("foto.png", segundo.FotoPerfilUrl);
        Assert.Equal(42, segundo.PuntosNostalgia);
        Assert.Equal(3, segundo.CantidadPublicaciones);
        Assert.Equal(1, segundo.CantidadSeguidores);
        Assert.Equal(1, segundo.CantidadSeguidos);
        Assert.True(Assert.Single(segundo.Logros).Desbloqueado);
        usuarios.Verify(r => r.ContarPublicacionesVisiblesAsync(7, default), Times.Once);
        usuarios.Verify(r => r.ObtenerPorIdAsync(7), porUsername ? Times.Never() : Times.Once());
        usuarios.Verify(r => r.ObtenerPorUsernameAsync("ana", default), porUsername ? Times.Once() : Times.Never());
        logros.Verify(l => l.ObtenerCatalogoParaUsuario(7), Times.Once);
        seguimiento.Verify(s => s.ListarSeguidores(7, default), Times.Once);
        seguimiento.Verify(s => s.ListarSeguidos(7, default), Times.Once);

        await cache.InvalidarAsync(default);
        await Leer(null);
        logros.Verify(l => l.ObtenerCatalogoParaUsuario(7), Times.Exactly(2));
    }
}
