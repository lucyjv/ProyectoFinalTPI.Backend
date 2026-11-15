using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface ICategoriaServicio
{
    IReadOnlyList<CategoriaCuentaDto> ObtenerCatalogo();
    CategoriaCuentaDto Obtener(CategoriaCuenta categoria);
    CategoriaCuentaDto ObtenerParaUsuario(Usuario usuario);
    bool TienePermiso(CategoriaCuenta categoria, PermisoCuenta permiso);
    Task ActualizarPorRecuerdo(Publicacion? publicacion, int puntosRecibidos, CancellationToken ct = default);
    Task<EstadoCategoriaDto> ObtenerEstado(int usuarioId, CancellationToken ct = default);
    Task ExigirPermiso(int usuarioId, PermisoCuenta permiso, CancellationToken ct = default);
    Task ValidarCreacion(int usuarioId, int cantidadAdjuntos, bool tieneMusica, CancellationToken ct = default);
    Task<int> CrearRecuerdo(Publicacion publicacion, CancellationToken ct = default);
    double ObtenerBonificacion(ReputacionEnum? reputacion, bool marca, double alta, double muyAlta);
}