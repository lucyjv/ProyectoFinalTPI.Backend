using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Capsulas;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

// PostgreSQL y PostGIS reales, aislados por factory; nunca usa las conexiones de desarrollo o producción.
[Trait("Categoria", "Integracion")]
public class CapsulasIntegracionTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    private PublicacionesApiFactory CrearFactory() => new(postgres)
    {
        ConfigurarServiciosAdicionales = AutenticacionPruebas.Registrar
    };

    #region Privacidad y lecturas

    [Fact]
    public async Task QueLaPortadaSubidaSeProtejaSeReemplaceYSeQuiteSinBorrarRecuerdos()
    {
        var archivos = new PortadasFake();
        await using var factory = new PublicacionesApiFactory(postgres)
        {
            ConfigurarServiciosAdicionales = services =>
            {
                AutenticacionPruebas.Registrar(services);
                services.RemoveAll<IPortadaCapsulaAlmacenamiento>();
                services.AddSingleton<IPortadaCapsulaAlmacenamiento>(archivos);
            }
        };
        using var propietario = AutenticacionPruebas.CrearCliente(factory, 10);
        using var visitante = AutenticacionPruebas.CrearCliente(factory, 20);
        using var anonimo = factory.CreateClient();
        await PrepararDatos(factory);
        using var form = FormularioPortada(incluirRecuerdo: true);
        using var creada = await propietario.PostAsync("/api/capsulas", form);
        Assert.True(creada.StatusCode == HttpStatusCode.Created, await creada.Content.ReadAsStringAsync());
        var capsula = (await creada.Content.ReadFromJsonAsync<CapsulaDto>())!;
        Assert.True(capsula.PortadaSubida);
        Assert.Equal($"/api/capsulas/{capsula.Id}/portada", capsula.PortadaUrl);
        Assert.Equal(1, capsula.CantidadRecuerdos);
        Assert.Single(archivos.Activos);
        Assert.DoesNotContain("PortadaRecursoId", await creada.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        foreach (var cliente in new[] { visitante, anonimo })
        {
            using var denegada = await cliente.GetAsync(capsula.PortadaUrl);
            Assert.Equal(HttpStatusCode.NotFound, denegada.StatusCode);
            Assert.True(denegada.Headers.CacheControl?.NoStore);
        }
        Assert.Equal(0, archivos.Lecturas);
        using (var foto = await propietario.GetAsync(capsula.PortadaUrl))
        {
            Assert.Equal(HttpStatusCode.OK, foto.StatusCode);
            Assert.Equal("image/webp", foto.Content.Headers.ContentType?.MediaType);
            Assert.True(foto.Headers.CacheControl?.NoStore);
        }
        using var reemplazo = FormularioPortada();
        using var actualizada = await propietario.PutAsync($"/api/capsulas/{capsula.Id}", reemplazo);
        Assert.True(actualizada.IsSuccessStatusCode, await actualizada.Content.ReadAsStringAsync());
        Assert.Single(archivos.Activos);
        Assert.Equal(1, archivos.Borrados);

        // Una edición JSON común conserva la foto subida si no pide cambiarla.
        using var publica = await propietario.PutAsJsonAsync($"/api/capsulas/{capsula.Id}",
            new ActualizarCapsulaDto { Nombre = "Mis viajes", Visibilidad = VisibilidadCapsula.Publica });
        Assert.True(publica.IsSuccessStatusCode);
        Assert.True((await publica.Content.ReadFromJsonAsync<CapsulaDto>())!.PortadaSubida);
        using var visible = await anonimo.GetAsync(capsula.PortadaUrl);
        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);

        using var automatica = await propietario.PutAsJsonAsync($"/api/capsulas/{capsula.Id}",
            new ActualizarCapsulaDto { Nombre = "Mis viajes", ConservarPortadaSubida = false });
        Assert.True(automatica.IsSuccessStatusCode);
        var sinSubida = (await automatica.Content.ReadFromJsonAsync<CapsulaDto>())!;
        Assert.False(sinSubida.PortadaSubida);
        Assert.Equal("https://media.test/11.jpg", sinSubida.PortadaUrl);
        Assert.Empty(archivos.Activos);
        Assert.Equal(2, archivos.Borrados);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(4, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Publicaciones.CountAsync());
    }

    private static MultipartFormDataContent FormularioPortada(bool incluirRecuerdo = false)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent("Mis viajes"), "Nombre");
        form.Add(new StringContent("0"), "Visibilidad");
        if (incluirRecuerdo) form.Add(new StringContent("11"), "PublicacionesIds[0]");
        var foto = new ByteArrayContent(new byte[] { 255, 216, 255, 224, 0, 0, 0, 0, 0, 0, 0, 0 });
        foto.Headers.ContentType = new("image/jpeg");
        form.Add(foto, "Archivo", "portada.jpg");
        return form;
    }

    private sealed class PortadasFake : IPortadaCapsulaAlmacenamiento
    {
        public HashSet<string> Activos { get; } = new();
        public int Borrados, Lecturas;
        public Task<string> Subir(Stream archivo, CancellationToken ct)
        {
            var id = $"nostalgiar/capsulas/{Guid.NewGuid():N}";
            Activos.Add(id);
            return Task.FromResult(id);
        }
        public Task<byte[]> Leer(string recursoId, CancellationToken ct)
        {
            Assert.Contains(recursoId, Activos); Lecturas++;
            return Task.FromResult("RIFF0000WEBP"u8.ToArray());
        }
        public Task Eliminar(string recursoId, CancellationToken ct)
        {
            Assert.True(Activos.Remove(recursoId)); Borrados++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task QueUnaCapsulaPrivadaNoExpongaMetadatosPortadaHiloNiMapaAunqueSusRecuerdosSeanPublicos()
    {
        // Arrange
        await using var factory = CrearFactory();
        using var propietario = AutenticacionPruebas.CrearCliente(factory, 10);
        using var visitante = AutenticacionPruebas.CrearCliente(factory, 20);
        using var anonimo = factory.CreateClient();
        await PrepararDatos(factory);
        var capsula = await Crear(propietario, new() { Nombre = "Mi infancia", PublicacionesIds = new() { 11, 12 } });

        // Act / Assert
        Assert.Equal(VisibilidadCapsula.Privada, capsula.Visibilidad);
        foreach (var cliente in new[] { visitante, anonimo })
        {
            foreach (var sufijo in new[] { "", "/recuerdos", "/mapa" })
            {
                using var respuesta = await cliente.GetAsync($"/api/capsulas/{capsula.Id}{sufijo}");
                Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
                Assert.True(respuesta.Headers.CacheControl?.NoStore);
            }
            var publicas = await cliente.GetFromJsonAsync<ResultadoPaginadoDto<CapsulaDto>>("/api/capsulas?propietarioId=10");
            Assert.Empty(publicas!.Items);
        }
        using var sinLogin = await anonimo.GetAsync("/api/capsulas/mias");
        Assert.Equal(HttpStatusCode.Unauthorized, sinLogin.StatusCode);
        var propias = await propietario.GetFromJsonAsync<ResultadoPaginadoDto<CapsulaDto>>("/api/capsulas/mias");
        Assert.Equal(capsula.Id, Assert.Single(propias!.Items).Id);
        using var edicionAjena = await visitante.PutAsJsonAsync($"/api/capsulas/{capsula.Id}", new ActualizarCapsulaDto { Nombre = "Cambio ajeno" });
        Assert.Equal(HttpStatusCode.NotFound, edicionAjena.StatusCode);
    }

    [Fact]
    public async Task QueElHiloSeaCronologicoEstableYElMapaConserveLimitesGlobalesEntrePaginasYEpocas()
    {
        // Arrange: dos recuerdos comparten fecha, otro está a miles de kilómetros y en otra década.
        await using var factory = CrearFactory();
        using var propietario = AutenticacionPruebas.CrearCliente(factory, 10);
        using var anonimo = factory.CreateClient();
        await PrepararDatos(factory);
        var capsula = await Crear(propietario, new() { Nombre = "Mis viajes", Visibilidad = VisibilidadCapsula.Publica,
            PublicacionesIds = new() { 13, 12, 11 }, PortadaPublicacionId = 13, PortadaAdjuntoId = 101 });
        Assert.Equal("https://media.test/adjunto.jpg", capsula.PortadaUrl);
        Assert.Equal(3, capsula.CantidadRecuerdos);
        Assert.Equal(Fecha(1975), capsula.FechaDesde);
        Assert.Equal(Fecha(2022), capsula.FechaHasta);

        // Act
        var primera = await anonimo.GetFromJsonAsync<ResultadoPaginadoDto<PublicacionDetalleDto>>($"/api/capsulas/{capsula.Id}/recuerdos?limite=2");
        var segunda = await anonimo.GetFromJsonAsync<ResultadoPaginadoDto<PublicacionDetalleDto>>(
            $"/api/capsulas/{capsula.Id}/recuerdos?limite=2&cursor={Uri.EscapeDataString(primera!.SiguienteCursor!)}");
        var mapa = await anonimo.GetFromJsonAsync<MapaCapsulaDto>($"/api/capsulas/{capsula.Id}/mapa?limite=1");
        var area = await anonimo.GetFromJsonAsync<MapaCapsulaDto>(
            $"/api/capsulas/{capsula.Id}/mapa?minLatitud=-40&maxLatitud=-30&minLongitud=-70&maxLongitud=-50");

        // Assert
        Assert.Equal(new[] { 11, 12 }, primera.Items.Select(p => p.Id));
        Assert.Equal(13, Assert.Single(segunda!.Items).Id);
        Assert.False(segunda.HayMas);
        Assert.Single(mapa!.Items);
        Assert.True(mapa.HayMas);
        Assert.Equal(new LimitesMapaCapsulaDto(-35, 50, -60, 140), mapa.Limites);
        Assert.Equal(new[] { 11, 12 }, area!.Items.Select(p => p.Id));
        Assert.Equal(mapa.Limites, area.Limites);
    }

    [Fact]
    public async Task QueOcultarUnRecuerdoQuiteSuFotoFechasYPinYQueEliminarElAutorOculteTodaLaCapsula()
    {
        await using var factory = CrearFactory();
        using var cliente = AutenticacionPruebas.CrearCliente(factory, 10);
        await PrepararDatos(factory);
        var capsula = await Crear(cliente, new() { Nombre = "Mis viajes", Visibilidad = VisibilidadCapsula.Publica,
            PublicacionesIds = new() { 11, 13 }, PortadaPublicacionId = 13, PortadaAdjuntoId = 101 });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Publicaciones.SingleAsync(p => p.Id == 13)).EstaOculto = true;
            await db.SaveChangesAsync();
        }
        var actualizada = await cliente.GetFromJsonAsync<CapsulaDto>($"/api/capsulas/{capsula.Id}");
        Assert.Equal(1, actualizada!.CantidadRecuerdos);
        Assert.Equal(Fecha(1975), actualizada.FechaHasta);
        Assert.Equal("https://media.test/11.jpg", actualizada.PortadaUrl);
        Assert.Null(actualizada.PortadaPublicacionId);
        Assert.Null(actualizada.PortadaAdjuntoId);
        var mapa = await cliente.GetFromJsonAsync<MapaCapsulaDto>($"/api/capsulas/{capsula.Id}/mapa");
        Assert.Equal(11, Assert.Single(mapa!.Items).Id);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Usuarios.SingleAsync(u => u.Id == 10)).EstaEliminado = true;
            await db.SaveChangesAsync();
        }
        using var respuesta = await cliente.GetAsync($"/api/capsulas/{capsula.Id}");
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    #endregion

    #region Escrituras y transacciones

    [Fact]
    public async Task QueBorrarLaPublicacionDePortadaQuiteSusAsociacionesYUseOtraFoto()
    {
        await using var factory = CrearFactory();
        using var cliente = AutenticacionPruebas.CrearCliente(factory, 10);
        await PrepararDatos(factory);
        var capsula = await Crear(cliente, new() { Nombre = "Viajes", PublicacionesIds = new() { 11, 13 },
            PortadaPublicacionId = 13, PortadaAdjuntoId = 101 });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Publicaciones.Remove(await db.Publicaciones.SingleAsync(p => p.Id == 13));
            await db.SaveChangesAsync();
        }
        var actual = await cliente.GetFromJsonAsync<CapsulaDto>($"/api/capsulas/{capsula.Id}");
        Assert.Equal(1, actual!.CantidadRecuerdos);
        Assert.Null(actual.PortadaPublicacionId);
        Assert.Null(actual.PortadaAdjuntoId);
        Assert.Equal("https://media.test/11.jpg", actual.PortadaUrl);
    }

    [Fact]
    public async Task QueLasAsociacionesSeanIdempotentesYQuitarlasOBorrarCapsulaNoBorreNiReanaliceRecuerdos()
    {
        await using var factory = CrearFactory();
        using var cliente = AutenticacionPruebas.CrearCliente(factory, 10);
        await PrepararDatos(factory);
        var capsula = await Crear(cliente, new() { Nombre = "Infancia", PublicacionesIds = new() { 11, 11 }, PortadaPublicacionId = 11 });
        var trabajos = await ContarTrabajos(factory);
        foreach (var ids in new[] { new[] { 11, 12, 12 }, new[] { 11, 12 } })
        {
            using var respuesta = await cliente.PostAsJsonAsync($"/api/capsulas/{capsula.Id}/recuerdos", new { publicacionesIds = ids });
            Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        }
        var disponibles = await cliente.GetFromJsonAsync<ResultadoPaginadoDto<RecuerdoDisponibleCapsulaDto>>(
            $"/api/capsulas/recuerdos-disponibles?capsulaId={capsula.Id}");
        Assert.True(disponibles!.Items.Single(p => p.Id == 11).YaAgregado);
        Assert.False(disponibles.Items.Single(p => p.Id == 13).YaAgregado);
        Assert.DoesNotContain(disponibles.Items, p => p.Id == 21);
        using var quitar = await cliente.DeleteAsync($"/api/capsulas/{capsula.Id}/recuerdos?publicacionId=11");
        Assert.Equal(HttpStatusCode.NoContent, quitar.StatusCode);
        var despues = await cliente.GetFromJsonAsync<CapsulaDto>($"/api/capsulas/{capsula.Id}");
        Assert.Equal(1, despues!.CantidadRecuerdos);
        Assert.Null(despues.PortadaPublicacionId);
        Assert.Equal("https://media.test/12.jpg", despues.PortadaUrl);
        using var borrar = await cliente.DeleteAsync($"/api/capsulas/{capsula.Id}");
        Assert.Equal(HttpStatusCode.NoContent, borrar.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.CapsulasPublicaciones.AnyAsync());
        Assert.Equal(4, await db.Publicaciones.CountAsync());
        Assert.Equal(trabajos, await db.TrabajosAnalisisRecuerdos.CountAsync());
    }

    [Fact]
    public async Task QueAgregarEnParaleloElMismoRecuerdoNoDupliqueLaRelacion()
    {
        await using var factory = CrearFactory();
        using var cliente = AutenticacionPruebas.CrearCliente(factory, 10);
        await PrepararDatos(factory);
        var capsula = await Crear(cliente, new() { Nombre = "Infancia" });
        async Task Agregar()
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ICapsulaRepositorio>().AgregarRecuerdos(capsula.Id, 10, new[] { 11 }, default);
        }
        await Task.WhenAll(Agregar(), Agregar());
        using var scope = factory.Services.CreateScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().CapsulasPublicaciones.ToListAsync());
    }

    [Fact]
    public async Task QueUnLoteInvalidoNoAgregueParcialmenteYQueLaPortadaNoPuedaSerAjenaONoIncluida()
    {
        await using var factory = CrearFactory();
        using var cliente = AutenticacionPruebas.CrearCliente(factory, 10);
        await PrepararDatos(factory);
        var capsula = await Crear(cliente, new() { Nombre = "Infancia" });
        using var agregar = await cliente.PostAsJsonAsync($"/api/capsulas/{capsula.Id}/recuerdos", new { publicacionesIds = new[] { 11, 21 } });
        Assert.Equal(HttpStatusCode.BadRequest, agregar.StatusCode);
        using var portada = await cliente.PutAsJsonAsync($"/api/capsulas/{capsula.Id}", new ActualizarCapsulaDto
            { Nombre = "Cambio no permitido", PortadaPublicacionId = 13, PortadaAdjuntoId = 101 });
        Assert.Equal(HttpStatusCode.BadRequest, portada.StatusCode);
        var actual = await cliente.GetFromJsonAsync<CapsulaDto>($"/api/capsulas/{capsula.Id}");
        Assert.Equal("Infancia", actual!.Nombre);
        Assert.Equal(0, actual.CantidadRecuerdos);
        Assert.Null(actual.FechaDesde);
        Assert.Null(actual.PortadaUrl);
        var mapa = await cliente.GetFromJsonAsync<MapaCapsulaDto>($"/api/capsulas/{capsula.Id}/mapa");
        Assert.Empty(mapa!.Items);
        Assert.Null(mapa.Limites);
    }

    [Fact]
    public async Task QuePublicarConCapsulasGuardeCupoYUnSoloTrabajoYQueUnLoteAjenoNoGuardeNada()
    {
        await using var factory = CrearFactory();
        using var propietario = AutenticacionPruebas.CrearCliente(factory, 10);
        using var otro = AutenticacionPruebas.CrearCliente(factory, 20);
        await PrepararDatos(factory);
        var primera = await Crear(propietario, new() { Nombre = "Viajes" });
        var segunda = await Crear(propietario, new() { Nombre = "Vacaciones" });
        var ajena = await Crear(otro, new() { Nombre = "Otra persona" });
        var trabajos = await ContarTrabajos(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ICategoriaRepositorio>();
            await Assert.ThrowsAsync<ArgumentException>(() => repo.CrearConCupo(NuevoRecuerdo(primera.Id, ajena.Id),
                new DateOnly(2026, 11, 15), 5));
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(4, await db.Publicaciones.CountAsync());
            Assert.Empty(await db.Set<ConsumoPublicaciones>().ToListAsync());
            Assert.Equal(trabajos, await db.TrabajosAnalisisRecuerdos.CountAsync());
        }
        using (var scope = factory.Services.CreateScope())
        {
            var id = await scope.ServiceProvider.GetRequiredService<ICategoriaRepositorio>().CrearConCupo(
                NuevoRecuerdo(primera.Id, segunda.Id), new DateOnly(2026, 11, 15), 5);
            Assert.NotNull(id);
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(5, await db.Publicaciones.CountAsync());
            Assert.Equal(2, await db.CapsulasPublicaciones.CountAsync());
            Assert.Equal(1, (await db.Set<ConsumoPublicaciones>().SingleAsync()).Cantidad);
            Assert.Equal(trabajos + 1, await db.TrabajosAnalisisRecuerdos.CountAsync());
        }
    }

    [Fact]
    public async Task QueElFormularioDePublicacionAsocieLasCapsulasYValidePermisosAntesDeSubirMultimedia()
    {
        await using var factory = CrearFactory();
        using var cliente = AutenticacionPruebas.CrearCliente(factory, 10);
        using var otro = AutenticacionPruebas.CrearCliente(factory, 20);
        await PrepararDatos(factory);
        var propia = await Crear(cliente, new() { Nombre = "Vacaciones" });
        var ajena = await Crear(otro, new() { Nombre = "Ajena" });
        using var invalido = Formulario(ajena.Id);
        using var rechazado = await cliente.PostAsync("/api/publicaciones", invalido);
        Assert.Equal(HttpStatusCode.BadRequest, rechazado.StatusCode);
        Assert.Equal(0, factory.Media.Calls);
        using var valido = Formulario(propia.Id);
        using var creado = await cliente.PostAsync("/api/publicaciones", valido);
        Assert.True(creado.StatusCode == HttpStatusCode.Created, await creado.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var relacion = await db.CapsulasPublicaciones.SingleAsync();
        Assert.Equal(propia.Id, relacion.CapsulaId);
        Assert.Equal(10, (await db.Publicaciones.SingleAsync(p => p.Id == relacion.PublicacionId)).UsuarioId);
    }

    #endregion

    private static MultipartFormDataContent Formulario(int capsulaId)
    {
        var form = new MultipartFormDataContent();
        foreach (var campo in new Dictionary<string, string>
        {
            ["Titulo"] = "Vacaciones", ["Descripcion"] = "Un recuerdo", ["Fecha"] = "2022-12-18T00:00:00Z",
            ["Categoria"] = "0", ["NombreLugar"] = "Buenos Aires", ["Latitud"] = "-35", ["Longitud"] = "-60",
            ["TipoMultimedia"] = "0", ["UrlArchivo"] = "https://media.test/nuevo.jpg", ["AutorId"] = "20",
            ["CapsulasIds[0]"] = capsulaId.ToString()
        }) form.Add(new StringContent(campo.Value), campo.Key);
        return form;
    }

    private static async Task<CapsulaDto> Crear(HttpClient cliente, CrearCapsulaDto datos)
    {
        using var respuesta = await cliente.PostAsJsonAsync("/api/capsulas", datos);
        Assert.True(respuesta.StatusCode == HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());
        Assert.True(respuesta.Headers.CacheControl?.NoStore);
        return (await respuesta.Content.ReadFromJsonAsync<CapsulaDto>())!;
    }

    private static DateTime Fecha(int año) => new(año, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static Publicacion NuevoRecuerdo(params int[] capsulasIds) => new()
    {
        UsuarioId = 10, Titulo = "Otro recuerdo", Fecha = Fecha(2000), Lugar = NuevoLugar(-35, -60),
        Capsulas = capsulasIds.Select(id => new CapsulaRecuerdo { CapsulaId = id }).ToList()
    };
    private static Lugar NuevoLugar(double latitud, double longitud) => new()
        { Nombre = "Lugar", Direccion = "Dirección", Coordenadas = new Point(longitud, latitud) { SRID = 4326 } };

    private static async Task<int> ContarTrabajos(PublicacionesApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().TrabajosAnalisisRecuerdos.CountAsync();
    }

    private static async Task PrepararDatos(PublicacionesApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var propietario = new Usuario_Personal { Id = 10, Username = "propietario", Email = "propietario@test.com", Reputacion = ReputacionEnum.Nostalgico };
        var otro = new Usuario_Personal { Id = 20, Username = "otro", Email = "otro@test.com" };
        foreach (var (id, usuario, año, latitud, longitud) in new[]
        {
            (11, propietario, 1975, -35d, -60d), (12, propietario, 1975, -34d, -58d),
            (13, propietario, 2022, 50d, 140d), (21, otro, 2000, 0d, 0d)
        })
            db.Publicaciones.Add(new Publicacion
            {
                Id = id, Usuario = usuario, Titulo = $"Recuerdo {id}", Fecha = Fecha(año), Lugar = NuevoLugar(latitud, longitud),
                UrlMultimedia = $"https://media.test/{id}.jpg",
                Adjuntos = id == 13 ? new List<AdjuntoPublicacion>
                    { new() { Id = 101, Tipo = MultimediaEnum.Foto, Url = "https://media.test/adjunto.jpg", Orden = 0 } } : new()
            });
        await db.SaveChangesAsync();
    }
}
