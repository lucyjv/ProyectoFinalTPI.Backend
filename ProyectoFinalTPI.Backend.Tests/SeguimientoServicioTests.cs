using ProyectoFinalTPI.Backend.Entidades;
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

        var resultado = await servicio.SeguirUsuarioAsync(10, 10);

        Assert.Equal(ResultadoSeguirUsuario.AutoSeguimiento, resultado);
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

        var resultado = await servicio.SeguirUsuarioAsync(10, 20);

        Assert.Equal(ResultadoSeguirUsuario.UsuarioNoEncontrado, resultado);
        Assert.Equal(new[] { 10, 20 }, repositorio.IdsConsultados);
        Assert.Contains(idUsuarioInexistente, repositorio.IdsConsultados);
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

        public Task<bool> ExisteUsuario(
            int id,
            CancellationToken cancellationToken = default)
        {
            IdsConsultados.Add(id);
            return Task.FromResult(_idsExistentes.Contains(id));
        }
    }
}
