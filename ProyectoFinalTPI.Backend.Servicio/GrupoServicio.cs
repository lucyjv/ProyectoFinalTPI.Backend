using ProyectoFinalTPI.Backend.Dtos.Grupo;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Entidades.ValueObjects;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio.Mappings;

namespace ProyectoFinalTPI.Backend.Servicio;

public class GrupoServicio(
    IGrupoRepositorio grupoRepositorio,
    IUsuarioRepositorio usuarioRepositorio,
    IPublicacionServicio publicacionServicio) : IGrupoServicio
{
    public async Task<IReadOnlyList<GrupoResumenDto>> ObtenerTodos(
        int? usuarioActualId, CancellationToken cancellationToken = default)
    {
        var grupos = await grupoRepositorio.ObtenerTodos(cancellationToken);
        return grupos.Select(grupo => grupo.ToDto(usuarioActualId)).ToList();
    }

    public async Task<IReadOnlyList<GrupoResumenDto>> Buscar(
        string texto, int limite, int? usuarioActualId,
        CancellationToken cancellationToken = default)
    {
        var textoNormalizado = texto?.Trim() ?? string.Empty;
        if (textoNormalizado.Length is < 2 or > 100)
            throw new ArgumentException("El texto de busqueda debe tener entre 2 y 100 caracteres.");
        if (limite is < 1 or > 50)
            throw new ArgumentException("El limite debe estar entre 1 y 50.");

        var grupos = await grupoRepositorio.Buscar(textoNormalizado, limite, cancellationToken);
        return grupos.Select(grupo => grupo.ToDto(usuarioActualId)).ToList();
    }

    public async Task<GrupoResumenDto> ObtenerPorId(
        int grupoId, int? usuarioActualId, CancellationToken cancellationToken = default)
    {
        ValidarId(grupoId, "grupo");
        var grupo = await ObtenerGrupo(grupoId, cancellationToken);
        return grupo.ToDto(usuarioActualId);
    }

    public async Task<GrupoResumenDto> Crear(Grupo grupo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grupo);
        ValidarDatos(grupo);
        ValidarId(grupo.CreadorId, "creador");

        var creador = await usuarioRepositorio.ObtenerPorIdAsync(grupo.CreadorId)
            ?? throw new KeyNotFoundException("El usuario creador no existe.");

        grupo.Nombre = grupo.Nombre.Trim();
        grupo.Descripcion = grupo.Descripcion.Trim();
        grupo.FechaCreacion = DateTime.UtcNow;
        grupo.Creador = creador;
        grupo.Integrantes.Add(new GrupoIntegrante
        {
            UsuarioId = creador.Id,
            Usuario = creador,
            FechaUnion = grupo.FechaCreacion
        });

        await grupoRepositorio.Crear(grupo, cancellationToken);
        return grupo.ToDto(creador.Id);
    }

    public async Task<GrupoResumenDto> Actualizar(
        int grupoId, int usuarioId, Grupo cambios, CancellationToken cancellationToken = default)
    {
        ValidarId(grupoId, "grupo");
        ValidarId(usuarioId, "usuario");
        ArgumentNullException.ThrowIfNull(cambios);
        ValidarDatos(cambios);

        var grupo = await ObtenerGrupo(grupoId, cancellationToken);
        ValidarCreador(grupo, usuarioId);
        grupo.Nombre = cambios.Nombre.Trim();
        grupo.Descripcion = cambios.Descripcion.Trim();
        grupo.Decada = cambios.Decada;
        await grupoRepositorio.Actualizar(grupo, cancellationToken);
        return grupo.ToDto(usuarioId);
    }

    public async Task Eliminar(int grupoId, int usuarioId, CancellationToken cancellationToken = default)
    {
        ValidarId(grupoId, "grupo");
        ValidarId(usuarioId, "usuario");
        var grupo = await ObtenerGrupo(grupoId, cancellationToken);
        ValidarCreador(grupo, usuarioId);
        await grupoRepositorio.Eliminar(grupo, cancellationToken);
    }

    public async Task Unirse(int grupoId, int usuarioId, CancellationToken cancellationToken = default)
    {
        ValidarId(grupoId, "grupo");
        ValidarId(usuarioId, "usuario");
        await ObtenerGrupo(grupoId, cancellationToken);
        if (!await usuarioRepositorio.ExisteUsuario(usuarioId, cancellationToken))
            throw new KeyNotFoundException("El usuario no existe.");
        if (await grupoRepositorio.EsIntegrante(grupoId, usuarioId, cancellationToken)) return;

        await grupoRepositorio.AgregarIntegrante(new GrupoIntegrante
        {
            GrupoId = grupoId,
            UsuarioId = usuarioId,
            FechaUnion = DateTime.UtcNow
        }, cancellationToken);
    }

    public async Task Salir(int grupoId, int usuarioId, CancellationToken cancellationToken = default)
    {
        ValidarId(grupoId, "grupo");
        ValidarId(usuarioId, "usuario");
        var grupo = await ObtenerGrupo(grupoId, cancellationToken);
        if (grupo.CreadorId == usuarioId)
            throw new ArgumentException("El creador no puede abandonar su grupo; debe eliminarlo.");

        var integrante = await grupoRepositorio.ObtenerIntegrante(grupoId, usuarioId, cancellationToken)
            ?? throw new KeyNotFoundException("El usuario no integra el grupo.");
        await grupoRepositorio.QuitarIntegrante(integrante, cancellationToken);
    }

    public async Task Expulsar(
        int grupoId, int usuarioIdCreador, int usuarioIdAEliminar,
        CancellationToken cancellationToken = default)
    {
        ValidarId(grupoId, "grupo");
        ValidarId(usuarioIdCreador, "usuario");
        ValidarId(usuarioIdAEliminar, "integrante");
        var grupo = await ObtenerGrupo(grupoId, cancellationToken);
        ValidarCreador(grupo, usuarioIdCreador);
        if (grupo.CreadorId == usuarioIdAEliminar)
            throw new ArgumentException("El creador no puede eliminarse del grupo.");

        var integrante = await grupoRepositorio.ObtenerIntegrante(
            grupoId, usuarioIdAEliminar, cancellationToken)
            ?? throw new KeyNotFoundException("El usuario no integra el grupo.");
        await grupoRepositorio.QuitarIntegrante(integrante, cancellationToken);
    }

    public async Task<IReadOnlyList<GrupoIntegranteDto>> ObtenerIntegrantes(
        int grupoId, CancellationToken cancellationToken = default)
    {
        ValidarId(grupoId, "grupo");
        var grupo = await ObtenerGrupo(grupoId, cancellationToken);
        var integrantes = await grupoRepositorio.ObtenerIntegrantes(grupoId, cancellationToken);
        return integrantes.Select(integrante => integrante.ToDto(grupo.CreadorId)).ToList();
    }

    public async Task<IReadOnlyList<PublicacionDetalleDto>> ObtenerRecuerdos(
        int grupoId, CancellationToken cancellationToken = default)
    {
        ValidarId(grupoId, "grupo");
        await ObtenerGrupo(grupoId, cancellationToken);
        return await grupoRepositorio.ObtenerRecuerdos(grupoId, cancellationToken);
    }

    public async Task<int> CrearRecuerdo(
        int grupoId, int usuarioId, Publicacion publicacion, Stream? archivo = null,
        string? nombreArchivo = null, CancellationToken cancellationToken = default)
    {
        ValidarId(grupoId, "grupo");
        ValidarId(usuarioId, "usuario");
        ArgumentNullException.ThrowIfNull(publicacion);
        await ObtenerGrupo(grupoId, cancellationToken);
        if (!await grupoRepositorio.EsIntegrante(grupoId, usuarioId, cancellationToken))
            throw new UnauthorizedAccessException("Solo los integrantes pueden publicar recuerdos en el grupo.");

        publicacion.GrupoId = grupoId;
        publicacion.UsuarioId = usuarioId;
        return await publicacionServicio.Crear(publicacion, archivo, nombreArchivo);
    }

    private async Task<Grupo> ObtenerGrupo(int grupoId, CancellationToken cancellationToken)
    {
        return await grupoRepositorio.ObtenerPorId(grupoId, cancellationToken)
            ?? throw new KeyNotFoundException("El grupo no existe.");
    }

    private static void ValidarCreador(Grupo grupo, int usuarioId)
    {
        if (grupo.CreadorId != usuarioId)
            throw new UnauthorizedAccessException("Solo el creador puede realizar esta accion.");
    }

    private static void ValidarDatos(Grupo grupo)
    {
        var nombre = grupo.Nombre?.Trim() ?? string.Empty;
        var descripcion = grupo.Descripcion?.Trim() ?? string.Empty;
        if (nombre.Length is < 3 or > 100)
            throw new ArgumentException("El nombre debe tener entre 3 y 100 caracteres.");
        if (descripcion.Length > 500)
            throw new ArgumentException("La descripcion no puede superar los 500 caracteres.");
        _ = new Decada(grupo.Decada);
    }

    private static void ValidarId(int id, string recurso)
    {
        if (id <= 0) throw new ArgumentException($"El ID de {recurso} debe ser mayor que cero.");
    }
}
