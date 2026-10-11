using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class SeguimientoServicioTests
{
    [Fact]
    public async Task Seguirse_a_si_mismo_devuelve_auto_seguimiento_sin_consultar_usuarios()
    {
        var repositorio = new UsuarioRepositorioFake(10);
        var servicio = new SeguimientoServicio(repositorio, null!);

        var resultado = await servicio.SeguirUsuario(10, 10);

        Assert.Equal(ResultadoSeguimiento.AutoSeguimiento, resultado);
        Assert.Empty(repositorio.IdsConsultados);
    }

    [Theory]
    [InlineData(10, 20)]
    [InlineData(20, 10)]
    public async Task Si_falta_cualquiera_de_los_usuarios_devuelve_no_encontrado(
        int idUsuarioExistente,
        int idUsuarioInexistente)
    {
        var repositorio = new UsuarioRepositorioFake(idUsuarioExistente);
        var servicio = new SeguimientoServicio(repositorio, null!);

        var resultado = await servicio.SeguirUsuario(10, 20);

        Assert.Equal(ResultadoSeguimiento.UsuarioNoEncontrado, resultado);
        Assert.Equal(new[] { 10, 20 }, repositorio.IdsConsultados);
        Assert.Contains(idUsuarioInexistente, repositorio.IdsConsultados);
    }

    [Fact]
    public async Task Dejar_de_seguirse_a_si_mismo_devuelve_auto_seguimiento_sin_consultar_usuarios()
    {
        var repositorio = new UsuarioRepositorioFake(10);
        var servicio = new SeguimientoServicio(repositorio, null!);

        var resultado = await servicio.DejarDeSeguirUsuario(10, 10);

        Assert.Equal(ResultadoSeguimiento.AutoSeguimiento, resultado);
        Assert.Empty(repositorio.IdsConsultados);
    }

    [Theory]
    [InlineData(10, 20)]
    [InlineData(20, 10)]
    public async Task Al_dejar_de_seguir_si_falta_un_usuario_devuelve_no_encontrado(
        int idUsuarioExistente,
        int idUsuarioInexistente)
    {
        var repositorio = new UsuarioRepositorioFake(idUsuarioExistente);
        var servicio = new SeguimientoServicio(repositorio, null!);

        var resultado = await servicio.DejarDeSeguirUsuario(10, 20);

        Assert.Equal(ResultadoSeguimiento.UsuarioNoEncontrado, resultado);
        Assert.Equal(new[] { 10, 20 }, repositorio.IdsConsultados);
        Assert.Contains(idUsuarioInexistente, repositorio.IdsConsultados);
    }

    [Fact]
    public async Task Listar_seguidos_devuelve_null_si_el_usuario_no_existe_sin_acceder_a_Neo4j()
    {
        var repositorio = new UsuarioRepositorioFake();
        var servicio = new SeguimientoServicio(repositorio, null!);

        var resultado = await servicio.ListarSeguidos(99);

        Assert.Null(resultado);
        Assert.Equal(new[] { 99 }, repositorio.IdsConsultados);
    }

    [Fact]
    public async Task Listar_seguidores_devuelve_null_si_el_usuario_no_existe_sin_acceder_a_Neo4j()
    {
        var repositorio = new UsuarioRepositorioFake();
        var servicio = new SeguimientoServicio(repositorio, null!);

        var resultado = await servicio.ListarSeguidores(99);

        Assert.Null(resultado);
        Assert.Equal(new[] { 99 }, repositorio.IdsConsultados);
    }

    private sealed class UsuarioRepositorioFake : IUsuarioRepositorio
    {
        private readonly HashSet<int> _idsExistentes;

        public UsuarioRepositorioFake(params int[] idsExistentes)
        {
            _idsExistentes = idsExistentes.ToHashSet();
        }

        public List<int> IdsConsultados { get; } = new();

        public Task<Usuario?> ObtenerPorIdAsync(int id)
        {
            Usuario? usuario = _idsExistentes.Contains(id)
                ? new Usuario { Id = id }
                : null;

            return Task.FromResult(usuario);
        }

        public Task<List<AutorResumenDto>> ObtenerUsuariosPorIdsAsync(
            IReadOnlyCollection<int> ids,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ids
                .Where(_idsExistentes.Contains)
                .Select(id => new AutorResumenDto { Id = id, Username = $"usuario_{id}" })
                .ToList());
        }

        public Task<List<int>> ObtenerIdsExistentesAsync(
            IReadOnlyCollection<int> ids,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ids.Where(_idsExistentes.Contains).ToList());
        }

        public Task<bool> ExisteUsuario(
            int id,
            CancellationToken cancellationToken = default)
        {
            IdsConsultados.Add(id);
            return Task.FromResult(_idsExistentes.Contains(id));
        }
    }
}
