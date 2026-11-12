using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Interfaces.Servicio;

public interface ICategoriaServicio
{
    IReadOnlyList<CategoriaCuentaDto> ObtenerCatalogo();

    CategoriaCuentaDto Obtener(CategoriaCuenta categoria);

    CategoriaCuentaDto ObtenerParaUsuario(Usuario usuario);

    bool TienePermiso(
        CategoriaCuenta categoria,
        PermisoCuenta permiso);

    Task ActualizarPorRecuerdo(
        Publicacion? publicacion,
        int puntosRecibidos,
        CancellationToken ct = default);
}