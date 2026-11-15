using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public class AdminPublicacionServicio(
    IAdminPublicacionRepositorio repositorio,
    INotificacionServicio notificaciones) : IAdminPublicacionServicio
{

    #region Operaciones públicas

    public Task<PaginaAdminDto<PublicacionAdminDto>> Listar(
        FiltroPublicacionesAdminDto filtro, CancellationToken cancellationToken = default)
    {
        ValidarPagina(filtro.Pagina, filtro.Tamanio);
        return repositorio.Listar(filtro, cancellationToken);
    }

    public async Task<PublicacionAdminDto> Actualizar(
        int id, ActualizarPublicacionAdminDto dto, CancellationToken cancellationToken = default)
    {
        ValidarEdicion(dto);
        var publicacion = await Obtener(id, cancellationToken);
        AplicarEdicion(publicacion, dto);
        await repositorio.Guardar(cancellationToken);
        return Mapear(publicacion);
    }

    public async Task CambiarVisibilidad(
        int id, CambiarVisibilidadPublicacionDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Ocultar && string.IsNullOrWhiteSpace(dto.Motivo))
            throw new ArgumentException("El motivo es obligatorio al ocultar una publicación.");
        var publicacion = await Obtener(id, cancellationToken);
        var notificar = dto.Ocultar && !publicacion.EstaOculto;
        publicacion.EstaOculto = dto.Ocultar;
        publicacion.MotivoOculto = dto.Ocultar ? dto.Motivo!.Trim() : null;
        await repositorio.Guardar(cancellationToken);
        if (notificar) await notificaciones.NotificarAdministrativaAsync(publicacion.UsuarioId, id, cancellationToken);
    }

    public async Task Eliminar(int id, CancellationToken cancellationToken = default)
    {
        var publicacion = await Obtener(id, cancellationToken);
        var autorId = publicacion.UsuarioId;
        await repositorio.Eliminar(publicacion, cancellationToken);
        await notificaciones.NotificarAdministrativaAsync(autorId, id, cancellationToken);
    }

    #endregion

    #region Métodos privados y validaciones
    private async Task<Publicacion> Obtener(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) throw new ArgumentException("El ID de publicación debe ser mayor que cero.");
        return await repositorio.Obtener(id, cancellationToken)
            ?? throw new KeyNotFoundException("La publicación no existe.");
    }

    private static void ValidarPagina(int pagina, int tamanio)
    {
        if (pagina < 1) throw new ArgumentException("La página debe ser mayor que cero.");
        if (tamanio is < 1 or > 100) throw new ArgumentException("El tamaño debe estar entre 1 y 100.");
    }

    private static void ValidarEdicion(ActualizarPublicacionAdminDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Titulo) || dto.Titulo.Trim().Length > 200)
            throw new ArgumentException("El título es obligatorio y no puede superar 200 caracteres.");
        if (string.IsNullOrWhiteSpace(dto.Descripcion) || dto.Descripcion.Trim().Length > 5000)
            throw new ArgumentException("La descripción es obligatoria y no puede superar 5000 caracteres.");
        if (!Enum.IsDefined(dto.Categoria)) throw new ArgumentException("La categoría no es válida.");
    }

    private static void AplicarEdicion(Publicacion publicacion, ActualizarPublicacionAdminDto dto)
    {
        publicacion.Titulo = dto.Titulo.Trim();
        publicacion.Descripcion = dto.Descripcion.Trim();
        publicacion.Fecha = dto.Fecha;
        publicacion.Categoria = dto.Categoria;
    }

    private static PublicacionAdminDto Mapear(Publicacion p) => new()
    {
        Id = p.Id,
        Titulo = p.Titulo,
        Descripcion = p.Descripcion,
        Fecha = p.Fecha,
        FechaCreacion = p.FechaCreación,
        Categoria = p.Categoria,
        EstaOculto = p.EstaOculto,
        MotivoOculto = p.MotivoOculto,
        AutorId = p.UsuarioId,
        AutorUsername = p.Usuario.Username
    };

    #endregion
}
