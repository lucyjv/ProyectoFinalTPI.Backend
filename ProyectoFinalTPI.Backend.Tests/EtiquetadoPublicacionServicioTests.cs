using System.Reflection;
using Neo4j.Driver;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class EtiquetadoPublicacionServicioTests
{
    [Fact]
    public async Task Etiquetar_usuarios_crea_relaciones_en_Neo4j_y_elimina_ids_duplicados()
    {
        var repositorioUsuarios = new UsuarioRepositorioFake(10, 20);
        var repositorioPublicaciones = CrearRepositorioPublicaciones(existe: true);
        var neo4j = new Neo4jDriverFake(relacionesCreadas: 2);
        var servicio = new EtiquetadoPublicacionServicio(
            repositorioUsuarios,
            repositorioPublicaciones,
            neo4j.Driver);

        var resultado = await servicio.EtiquetarUsuarios(25, new[] { 10, 10, 20 });

        Assert.Equal(2, resultado);
        Assert.Equal("25", neo4j.PublicacionId);
        Assert.Equal(new[] { 10, 20 }, neo4j.UsuariosIds);
        Assert.Contains("ETIQUETADO_EN", neo4j.Consulta);
        Assert.Equal(1, neo4j.CantidadConsultas);
    }

    [Fact]
    public async Task Publicacion_invalida_falla_antes_de_consultar_repositorios()
    {
        var usuarios = new UsuarioRepositorioFake(10);
        var servicio = CrearServicio(usuarios, existePublicacion: true, new Neo4jDriverFake(0).Driver);

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.EtiquetarUsuarios(0, new[] { 10 }));

        Assert.Equal(0, usuarios.ConsultasIdsExistentes);
    }

    [Fact]
    public async Task Lista_vacia_falla_sin_acceder_a_los_repositorios()
    {
        var usuarios = new UsuarioRepositorioFake(10);
        var servicio = CrearServicio(usuarios, existePublicacion: true, new Neo4jDriverFake(0).Driver);

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.EtiquetarUsuarios(25, Array.Empty<int>()));

        Assert.Equal(0, usuarios.ConsultasIdsExistentes);
    }

    [Fact]
    public async Task ID_de_usuario_invalido_falla_antes_de_consultar_repositorios()
    {
        var usuarios = new UsuarioRepositorioFake(10);
        var servicio = CrearServicio(usuarios, existePublicacion: true, new Neo4jDriverFake(0).Driver);

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.EtiquetarUsuarios(25, new[] { 0 }));

        Assert.Equal(0, usuarios.ConsultasIdsExistentes);
    }

    [Fact]
    public async Task Publicacion_inexistente_falla_sin_consultar_usuarios_ni_Neo4j()
    {
        var usuarios = new UsuarioRepositorioFake(10);
        var neo4j = new Neo4jDriverFake(0);
        var servicio = CrearServicio(usuarios, existePublicacion: false, neo4j.Driver);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => servicio.EtiquetarUsuarios(25, new[] { 10 }));

        Assert.Equal(0, usuarios.ConsultasIdsExistentes);
        Assert.Equal(0, neo4j.CantidadConsultas);
    }

    [Fact]
    public async Task Usuario_inexistente_falla_sin_crear_relaciones_en_Neo4j()
    {
        var usuarios = new UsuarioRepositorioFake(10);
        var neo4j = new Neo4jDriverFake(0);
        var servicio = CrearServicio(usuarios, existePublicacion: true, neo4j.Driver);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => servicio.EtiquetarUsuarios(25, new[] { 10, 99 }));

        Assert.Contains("99", exception.Message);
        Assert.Equal(0, neo4j.CantidadConsultas);
    }

    private static EtiquetadoPublicacionServicio CrearServicio(
        UsuarioRepositorioFake usuarios,
        bool existePublicacion,
        IDriver neo4jDriver)
    {
        return new EtiquetadoPublicacionServicio(
            usuarios,
            CrearRepositorioPublicaciones(existePublicacion),
            neo4jDriver);
    }

    private static IPublicacionRepositorio CrearRepositorioPublicaciones(bool existe)
    {
        return TestDispatchProxy<IPublicacionRepositorio>.Create((metodo, _) =>
            metodo.Name == nameof(IPublicacionRepositorio.ExistePublicacionVisible)
                ? Task.FromResult(existe)
                : throw new NotSupportedException($"No se esperaba llamar a {metodo.Name}."));
    }

    private sealed class UsuarioRepositorioFake : IUsuarioRepositorio
    {
        private readonly HashSet<int> _idsExistentes;

        public UsuarioRepositorioFake(params int[] idsExistentes)
        {
            _idsExistentes = idsExistentes.ToHashSet();
        }

        public int ConsultasIdsExistentes { get; private set; }

        public Task<List<int>> ObtenerIdsExistentesAsync(
            IReadOnlyCollection<int> ids,
            CancellationToken cancellationToken = default)
        {
            ConsultasIdsExistentes++;
            return Task.FromResult(ids.Where(_idsExistentes.Contains).ToList());
        }

        public Task<Usuario?> ObtenerPorIdAsync(int id) =>
            Task.FromResult<Usuario?>(null);

        public Task<bool> ExisteUsuario(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_idsExistentes.Contains(id));

        public Task<List<AutorResumenDto>> ObtenerUsuariosPorIdsAsync(
            IReadOnlyCollection<int> ids,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AutorResumenDto>());
    }

    private sealed class Neo4jDriverFake
    {
        public Neo4jDriverFake(int relacionesCreadas)
        {
            var cursor = TestDispatchProxy<IResultCursor>.Create((metodo, _) =>
                metodo.Name == nameof(IResultCursor.ConsumeAsync)
                    ? Task.FromResult(CrearResumen(relacionesCreadas))
                    : throw new NotSupportedException($"No se esperaba llamar a {metodo.Name}."));

            var queryRunner = TestDispatchProxy<IAsyncQueryRunner>.Create((metodo, argumentos) =>
            {
                if (metodo.Name != nameof(IAsyncQueryRunner.RunAsync))
                {
                    throw new NotSupportedException($"No se esperaba llamar a {metodo.Name}.");
                }

                Consulta = (string)argumentos![0]!;
                var parametros = argumentos[1]!;
                var tipoParametros = parametros.GetType();
                PublicacionId = tipoParametros.GetProperty("publicacionId")!
                    .GetValue(parametros)!.ToString();
                UsuariosIds = ((IEnumerable<int>)tipoParametros
                    .GetProperty("usuariosIds")!
                    .GetValue(parametros)!).ToArray();
                CantidadConsultas++;

                return Task.FromResult(cursor);
            });

            var session = TestDispatchProxy<IAsyncSession>.Create((metodo, argumentos) =>
            {
                if (metodo.Name == nameof(IAsyncSession.ExecuteWriteAsync))
                {
                    var callback = (Func<IAsyncQueryRunner, Task<int>>)argumentos![0]!;
                    return callback(queryRunner);
                }

                if (metodo.Name == nameof(IAsyncDisposable.DisposeAsync))
                {
                    return ValueTask.CompletedTask;
                }

                throw new NotSupportedException($"No se esperaba llamar a {metodo.Name}.");
            });

            Driver = TestDispatchProxy<IDriver>.Create((metodo, _) =>
                metodo.Name == nameof(IDriver.AsyncSession)
                    ? session
                    : throw new NotSupportedException($"No se esperaba llamar a {metodo.Name}."));
        }

        public IDriver Driver { get; }
        public string Consulta { get; private set; } = string.Empty;
        public string? PublicacionId { get; private set; }
        public int[] UsuariosIds { get; private set; } = Array.Empty<int>();
        public int CantidadConsultas { get; private set; }

        private static IResultSummary CrearResumen(int relacionesCreadas)
        {
            var counters = TestDispatchProxy<ICounters>.Create((metodo, _) =>
                metodo.Name == "get_RelationshipsCreated"
                    ? relacionesCreadas
                    : throw new NotSupportedException($"No se esperaba consultar {metodo.Name}."));

            return TestDispatchProxy<IResultSummary>.Create((metodo, _) =>
                metodo.Name == "get_Counters"
                    ? counters
                    : throw new NotSupportedException($"No se esperaba consultar {metodo.Name}."));
        }
    }
}

public class TestDispatchProxy<T> : DispatchProxy where T : class
{
    private Func<MethodInfo, object?[]?, object?>? _handler;

    public static T Create(Func<MethodInfo, object?[]?, object?> handler)
    {
        var proxy = DispatchProxy.Create<T, TestDispatchProxy<T>>();
        ((TestDispatchProxy<T>)(object)proxy)._handler = handler;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        return _handler!(targetMethod!, args);
    }
}
