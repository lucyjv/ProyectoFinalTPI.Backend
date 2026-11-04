using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Cache;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class CacheMapaYPerfilTests
{
    private static CacheLecturasPublicas CrearCache() => new(
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
        NullLogger<CacheLecturasPublicas>.Instance);

    private static ApplicationDbContext CrearContexto(CacheLecturasPublicas cache) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new InvalidacionPublicaInterceptor(cache, new DetectorCambiosPublicos()))
            .Options);

    private static void AgregarPines(ApplicationDbContext db)
    {
        var usuario = new Usuario_Personal { Id = 1, Username = "ana", Nombre = "Ana", Apellido = "Perez" };
        var lugar = new Lugar { Id = 1, Nombre = "Plaza", Direccion = "Centro", Coordenadas = new Point(-58, -34) { SRID = 4326 } };
        // Más pines que el límite de una respuesta: la caché debe conservarlos todos.
        for (var id = 1; id <= 205; id++)
            db.Publicaciones.Add(new Publicacion
            {
                Id = id, Usuario = usuario, Lugar = lugar, Titulo = $"Recuerdo {id}",
                Fecha = new DateTime(1990, 6, 1).AddDays(id),
                Categoria = id == 205 ? CategoriaEnum.Cine : CategoriaEnum.Lugares,
                EstaOculto = id == 204,
                PublicacionOriginalId = id == 203 ? 1 : null
            });
    }

    private static PublicacionFiltroDto Filtro() => new()
    {
        MinLatitud = -35, MaxLatitud = -33, MinLongitud = -59, MaxLongitud = -57
    };

    [Fact]
    public async Task QueLaCacheGuardeTodosLosPinesYFiltreSinVolverALaBase()
    {
        var cache = CrearCache();
        var db = CrearContexto(cache);
        AgregarPines(db);
        await db.SaveChangesAsync();
        var repositorio = new PublicacionRepositorio(db, cache: cache);
        Assert.Equal(201, (await repositorio.ObtenerPines(Filtro(), 200)).Count);

        // Si cualquiera de las siguientes lecturas consulta EF, fallará por contexto cerrado.
        await db.DisposeAsync();
        var todos = await repositorio.ObtenerPines(Filtro(), 300);
        Assert.Equal(203, todos.Count);
        Assert.DoesNotContain(todos, p => p.Id == 203);
        Assert.DoesNotContain(todos, p => p.Id == 204);
        var filtro = Filtro();
        filtro.Categoria = CategoriaEnum.Cine;
        Assert.Equal(205, Assert.Single(await repositorio.ObtenerPines(filtro, 200)).Id);
        filtro.Categoria = null;
        filtro.FechaDesde = new DateTime(1990, 6, 2);
        filtro.FechaHasta = new DateTime(1990, 6, 3);
        Assert.Equal(1, Assert.Single(await repositorio.ObtenerPines(filtro, 200)).Id);
        filtro.MinLatitud = -33;
        filtro.MaxLatitud = -32;
        Assert.Empty(await repositorio.ObtenerPines(filtro, 200));
    }

    [Fact]
    public async Task QueOcultarPublicacionesYCambiarUbicacionesInvalideLosPines()
    {
        var cache = CrearCache();
        using var db = CrearContexto(cache);
        AgregarPines(db);
        await db.SaveChangesAsync();
        var repositorio = new PublicacionRepositorio(db, cache: cache);
        await repositorio.ObtenerPines(Filtro(), 300);
        (await db.Publicaciones.SingleAsync(p => p.Id == 1)).EstaOculto = true;
        await db.SaveChangesAsync();
        Assert.DoesNotContain(await repositorio.ObtenerPines(Filtro(), 300), p => p.Id == 1);
        (await db.Lugares.SingleAsync()).Coordenadas = new Point(0, 0) { SRID = 4326 };
        await db.SaveChangesAsync();
        Assert.Empty(await repositorio.ObtenerPines(Filtro(), 300));
    }

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

    [Theory]
    [InlineData("usuario")]
    [InlineData("logro")]
    [InlineData("catalogo")]
    public async Task QueGuardarCambiosDelPerfilInvalideSuInformacionCompleta(string tipo)
    {
        var cache = CrearCache();
        using var db = CrearContexto(cache);
        await cache.Obtener("perfil:prueba", () => Task.FromResult<string?>("anterior"), default);
        switch (tipo)
        {
            case "usuario": db.Usuarios.Add(new Usuario_Personal { Username = "ana", Nombre = "Ana", Apellido = "Perez" }); break;
            case "logro": db.Logros_Usuarios.Add(new Logros_Usuarios { UsuarioId = 1, LogroId = 1 }); break;
            case "catalogo": db.Logro.Add(new Logro { Id = 1, Titulo = "Logro", Descripcion = "Descripcion", UrlIcono = "icono.png" }); break;
        }
        await db.SaveChangesAsync();
        Assert.Equal("nuevo", await cache.Obtener("perfil:prueba", () => Task.FromResult<string?>("nuevo"), default));
    }
}
