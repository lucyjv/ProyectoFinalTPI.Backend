using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class GrupoRepositorioTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly GrupoRepositorio _repositorio;

    public GrupoRepositorioTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _repositorio = new GrupoRepositorio(_context);
    }

    private readonly Usuario_Personal _creador = new()
    {
        Id = 10,
        Username = "creadorTest",
        Email = "creador@test.com",
        Contraseña = "password",
        Nombre = "Creador",
        Apellido = "Prueba",
        FechaNacimiento = new DateOnly(1995, 1, 1)
    };

    private readonly Usuario_Personal _integrante = new()
    {
        Id = 20,
        Username = "integranteTest",
        Email = "integrante@test.com",
        Contraseña = "password",
        Nombre = "Integrante",
        Apellido = "Prueba",
        FechaNacimiento = new DateOnly(1996, 1, 1)
    };

    [Fact]
    public async Task QueSePuedaCrearUnGrupoConSuCreadorEIntegranteInicial()
    {
        // Arrange
        _context.Usuarios.Add(_creador);
        await _context.SaveChangesAsync();

        var grupo = new Grupo
        {
            Nombre = "Amigos de la escuela",
            Descripcion = "Recuerdos de la secundaria",
            Decada = 1990,
            FechaCreacion = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc),
            CreadorId = _creador.Id,
            Integrantes =
            [
                new GrupoIntegrante
                {
                    UsuarioId = _creador.Id,
                    FechaUnion = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc)
                }
            ]
        };

        // Act
        var id = await _repositorio.Crear(grupo);

        // Assert
        Assert.True(id > 0);
        _context.ChangeTracker.Clear();

        var guardado = await _context.Grupos
            .Include(actual => actual.Integrantes)
            .SingleAsync(actual => actual.Id == id);

        Assert.Equal("Amigos de la escuela", guardado.Nombre);
        Assert.Equal(1990, guardado.Decada);
        Assert.Equal(_creador.Id, guardado.CreadorId);
        Assert.Equal(_creador.Id, Assert.Single(guardado.Integrantes).UsuarioId);
    }

    [Fact]
    public async Task QueSePuedaObtenerUnGrupoPorSuIdConCreadorEIntegrantes()
    {
        // Arrange
        var grupo = await CrearGrupoConIntegrantes();
        _context.ChangeTracker.Clear();

        // Act
        var resultado = await _repositorio.ObtenerPorId(grupo.Id);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(grupo.Id, resultado.Id);
        Assert.Equal(_creador.Username, resultado.Creador.Username);
        Assert.Equal(2, resultado.Integrantes.Count);
    }

    [Fact]
    public async Task QueNoSeObtengaUnGrupoSiElIdNoExiste()
    {
        // Arrange
        const int idInexistente = 999;

        // Act
        var resultado = await _repositorio.ObtenerPorId(idInexistente);

        // Assert
        Assert.Null(resultado);
    }

    [Fact]
    public async Task QueLosGruposSeObtenganDelMasRecienteAlMasAntiguo()
    {
        // Arrange
        _context.Usuarios.Add(_creador);
        _context.Grupos.AddRange(
            new Grupo
            {
                Id = 1,
                Nombre = "Grupo anterior",
                Descripcion = "Anterior",
                CreadorId = _creador.Id,
                FechaCreacion = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc)
            },
            new Grupo
            {
                Id = 2,
                Nombre = "Grupo reciente",
                Descripcion = "Reciente",
                CreadorId = _creador.Id,
                FechaCreacion = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
            });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var resultado = await _repositorio.ObtenerTodos();

        // Assert
        Assert.Equal(new[] { 2, 1 }, resultado.Select(grupo => grupo.Id).ToArray());
    }

    [Fact]
    public async Task QueLaBusquedaEncuentrePorNombreYDescripcionSinDistinguirMayusculas()
    {
        // Arrange
        _context.Usuarios.Add(_creador);
        _context.Grupos.AddRange(
            new Grupo
            {
                Id = 1,
                Nombre = "Viajes por Argentina",
                Descripcion = "Fotos y anecdotas",
                CreadorId = _creador.Id
            },
            new Grupo
            {
                Id = 2,
                Nombre = "Mis viajes favoritos",
                Descripcion = "Recuerdos del mundo",
                CreadorId = _creador.Id
            },
            new Grupo
            {
                Id = 3,
                Nombre = "Coleccion familiar",
                Descripcion = "Viajes con la familia",
                CreadorId = _creador.Id
            },
            new Grupo
            {
                Id = 4,
                Nombre = "Musica de los noventa",
                Descripcion = "Recitales",
                CreadorId = _creador.Id
            });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var resultado = await _repositorio.Buscar("VIAJES", limite: 10);

        // Assert
        Assert.Equal(new[] { 1, 3, 2 }, resultado.Select(grupo => grupo.Id).ToArray());
    }

    [Fact]
    public async Task QueLaBusquedaRespeteElLimiteYPrioriceElInicioDelNombre()
    {
        // Arrange
        _context.Usuarios.Add(_creador);
        _context.Grupos.AddRange(
            new Grupo { Id = 1, Nombre = "Club de cine", Descripcion = "Peliculas", CreadorId = _creador.Id },
            new Grupo { Id = 2, Nombre = "Cine argentino", Descripcion = "Clasicos", CreadorId = _creador.Id },
            new Grupo { Id = 3, Nombre = "Cine internacional", Descripcion = "Estrenos", CreadorId = _creador.Id });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var resultado = await _repositorio.Buscar("cine", limite: 2);

        // Assert
        Assert.Equal(new[] { 2, 3 }, resultado.Select(grupo => grupo.Id).ToArray());
    }

    [Fact]
    public async Task QueSePuedaActualizarElNombreYLaDescripcion()
    {
        // Arrange
        var grupo = await CrearGrupoConIntegrantes();
        _context.ChangeTracker.Clear();
        var grupoAActualizar = await _repositorio.ObtenerPorId(grupo.Id);
        Assert.NotNull(grupoAActualizar);
        grupoAActualizar.Nombre = "Nombre actualizado";
        grupoAActualizar.Descripcion = "Descripcion actualizada";

        // Act
        await _repositorio.Actualizar(grupoAActualizar);

        // Assert
        _context.ChangeTracker.Clear();
        var actualizado = await _context.Grupos.SingleAsync(actual => actual.Id == grupo.Id);
        Assert.Equal("Nombre actualizado", actualizado.Nombre);
        Assert.Equal("Descripcion actualizada", actualizado.Descripcion);
    }

    [Fact]
    public async Task QueSePuedaAgregarYConsultarUnIntegrante()
    {
        // Arrange
        var grupo = await CrearGrupoSoloConCreador();
        _context.Usuarios.Add(_integrante);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var membresia = new GrupoIntegrante
        {
            GrupoId = grupo.Id,
            UsuarioId = _integrante.Id,
            FechaUnion = new DateTime(2026, 10, 20, 13, 0, 0, DateTimeKind.Utc)
        };

        // Act
        await _repositorio.AgregarIntegrante(membresia);

        // Assert
        _context.ChangeTracker.Clear();
        Assert.True(await _repositorio.EsIntegrante(grupo.Id, _integrante.Id));
        var guardada = await _repositorio.ObtenerIntegrante(grupo.Id, _integrante.Id);
        Assert.NotNull(guardada);
    }

    [Fact]
    public async Task QueSePuedaQuitarUnIntegrante()
    {
        // Arrange
        var grupo = await CrearGrupoConIntegrantes();
        _context.ChangeTracker.Clear();
        var membresia = await _repositorio.ObtenerIntegrante(grupo.Id, _integrante.Id);
        Assert.NotNull(membresia);

        // Act
        await _repositorio.QuitarIntegrante(membresia);

        // Assert
        _context.ChangeTracker.Clear();
        Assert.False(await _repositorio.EsIntegrante(grupo.Id, _integrante.Id));
        Assert.True(await _repositorio.EsIntegrante(grupo.Id, _creador.Id));
    }

    [Fact]
    public async Task QueSeObtenganLosIntegrantesConSusUsuarios()
    {
        // Arrange
        var grupo = await CrearGrupoConIntegrantes();
        _context.ChangeTracker.Clear();

        // Act
        var resultado = await _repositorio.ObtenerIntegrantes(grupo.Id);

        // Assert
        Assert.Equal(2, resultado.Count);
        Assert.Equal(
            new[] { _creador.Username, _integrante.Username },
            resultado.Select(actual => actual.Usuario.Username).ToArray());
    }

    [Fact]
    public async Task QueSeObtenganSoloLosRecuerdosVisiblesDelGrupoSolicitado()
    {
        // Arrange
        var grupo = await CrearGrupoConIntegrantes();
        var otroGrupo = new Grupo
        {
            Id = 2,
            Nombre = "Otro grupo",
            Descripcion = "Otro",
            CreadorId = _creador.Id,
            FechaCreacion = new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc)
        };
        var lugar = new Lugar
        {
            Id = 1,
            Nombre = "Obelisco",
            Direccion = "Buenos Aires",
            Coordenadas = new Point(-58.3816, -34.6037) { SRID = 4326 }
        };
        _context.Grupos.Add(otroGrupo);
        _context.Lugares.Add(lugar);
        _context.Publicaciones.AddRange(
            CrearPublicacion(1, "Visible anterior", grupo.Id, lugar, false,
                new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc)),
            CrearPublicacion(2, "Visible reciente", grupo.Id, lugar, false,
                new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)),
            CrearPublicacion(3, "Oculta", grupo.Id, lugar, true,
                new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc)),
            CrearPublicacion(4, "De otro grupo", otroGrupo.Id, lugar, false,
                new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc)));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var resultado = await _repositorio.ObtenerRecuerdos(grupo.Id);

        // Assert
        Assert.Equal(new[] { 2, 1 }, resultado.Select(recuerdo => recuerdo.Id).ToArray());
        Assert.All(resultado, recuerdo =>
        {
            Assert.NotNull(recuerdo.Grupo);
            Assert.Equal(grupo.Id, recuerdo.Grupo.Id);
            Assert.Equal(grupo.Nombre, recuerdo.Grupo.Nombre);
        });
    }

    [Fact]
    public async Task QueSePuedaEliminarUnGrupo()
    {
        // Arrange
        var grupo = await CrearGrupoConIntegrantes();
        _context.ChangeTracker.Clear();
        var grupoAEliminar = await _repositorio.ObtenerPorId(grupo.Id);
        Assert.NotNull(grupoAEliminar);

        // Act
        await _repositorio.Eliminar(grupoAEliminar);

        // Assert
        _context.ChangeTracker.Clear();
        Assert.False(await _context.Grupos.AnyAsync(actual => actual.Id == grupo.Id));
    }

    private async Task<Grupo> CrearGrupoSoloConCreador()
    {
        _context.Usuarios.Add(_creador);
        var grupo = new Grupo
        {
            Id = 1,
            Nombre = "Grupo publico",
            Descripcion = "Descripcion del grupo",
            Decada = 1990,
            CreadorId = _creador.Id,
            FechaCreacion = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc),
            Integrantes =
            [
                new GrupoIntegrante
                {
                    UsuarioId = _creador.Id,
                    FechaUnion = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc)
                }
            ]
        };
        _context.Grupos.Add(grupo);
        await _context.SaveChangesAsync();
        return grupo;
    }

    private async Task<Grupo> CrearGrupoConIntegrantes()
    {
        _context.Usuarios.AddRange(_creador, _integrante);
        var grupo = new Grupo
        {
            Id = 1,
            Nombre = "Grupo publico",
            Descripcion = "Descripcion del grupo",
            Decada = 1990,
            CreadorId = _creador.Id,
            FechaCreacion = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc),
            Integrantes =
            [
                new GrupoIntegrante
                {
                    UsuarioId = _creador.Id,
                    FechaUnion = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc)
                },
                new GrupoIntegrante
                {
                    UsuarioId = _integrante.Id,
                    FechaUnion = new DateTime(2026, 10, 20, 13, 0, 0, DateTimeKind.Utc)
                }
            ]
        };
        _context.Grupos.Add(grupo);
        await _context.SaveChangesAsync();
        return grupo;
    }

    private Publicacion CrearPublicacion(
        int id, string titulo, int grupoId, Lugar lugar, bool estaOculta, DateTime fechaCreacion)
    {
        return new Publicacion
        {
            Id = id,
            Titulo = titulo,
            Descripcion = "Recuerdo de prueba",
            Fecha = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            FechaCreación = fechaCreacion,
            Categoria = CategoriaEnum.Lugares,
            TipoMultimedia = MultimediaEnum.Foto,
            UrlMultimedia = "https://example.com/foto.jpg",
            EstaOculto = estaOculta,
            UsuarioId = _creador.Id,
            LugarId = lugar.Id,
            GrupoId = grupoId
        };
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
