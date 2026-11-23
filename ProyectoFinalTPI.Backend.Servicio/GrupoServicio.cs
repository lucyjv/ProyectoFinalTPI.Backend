using ProyectoFinalTPI.Backend.Dtos.Grupo;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Entidades.ValueObjects;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio.Mappings;
using ProyectoFinalTPI.Backend.Servicio.Extensions;

namespace ProyectoFinalTPI.Backend.Servicio;

public class GrupoServicio(
    IGrupoRepositorio grupoRepositorio,
    IUsuarioRepositorio usuarioRepositorio,
    IPublicacionServicio publicacionServicio) : IGrupoServicio
{

    #region Operaciones públicas

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
        string textoNormalizado = ValidarBusqueda(texto, limite);
        var grupos = await grupoRepositorio.Buscar(textoNormalizado, limite, cancellationToken);
        return grupos.Select(grupo => grupo.ToDto(usuarioActualId)).ToList();
    }
    public async Task<GrupoResumenDto> ObtenerPorId(
        int grupoId, int? usuarioActualId, CancellationToken cancellationToken = default)
    {
        grupoId.ValidarId("grupo");
        var grupo = await ObtenerGrupo(grupoId, cancellationToken);
        return grupo.ToDto(usuarioActualId);
    }

    public async Task<GrupoResumenDto> Crear(Grupo grupo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grupo);
        ValidarDatos(grupo);
        grupo.CreadorId.ValidarId("creador");

        var creador = await ObtenerCreador(grupo);
        PrepararGrupoParaCreacion(grupo, creador);

        await grupoRepositorio.Crear(grupo, cancellationToken);
        return grupo.ToDto(creador.Id);
    }
    public async Task<GrupoResumenDto> Actualizar(
        int grupoId, int usuarioId, Grupo cambios, CancellationToken cancellationToken = default)
    {
        ValidarGrupoYUsuario(grupoId, usuarioId);
        ArgumentNullException.ThrowIfNull(cambios);
        ValidarDatos(cambios);

        Grupo grupo = await ObtenerGrupoDelCreador(grupoId, usuarioId, cancellationToken);
        AplicarCambios(grupo, cambios);
        await grupoRepositorio.Actualizar(grupo, cancellationToken);
        return grupo.ToDto(usuarioId);
    }

    public async Task Eliminar(int grupoId, int usuarioId, CancellationToken cancellationToken = default)
    {
        ValidarGrupoYUsuario(grupoId,usuarioId);
        var grupo = await ObtenerGrupoDelCreador(grupoId, usuarioId, cancellationToken);
        await grupoRepositorio.Eliminar(grupo, cancellationToken);
    }

    public async Task Unirse(int grupoId, int usuarioId, CancellationToken cancellationToken = default)
    {
        ValidarGrupoYUsuario(grupoId, usuarioId);
        await ObtenerGrupo(grupoId, cancellationToken);
        await VerificarUsuarioExistente(usuarioId, cancellationToken);
        if (await grupoRepositorio.EsIntegrante(grupoId, usuarioId, cancellationToken)) return;

        await grupoRepositorio.AgregarIntegrante(
            CrearIntegrante(grupoId, usuarioId), cancellationToken);
    }
    public async Task Salir(int grupoId, int usuarioId, CancellationToken cancellationToken = default)
    {
        ValidarGrupoYUsuario(grupoId, usuarioId);
        var grupo = await ObtenerGrupo(grupoId, cancellationToken);
        ValidarSalida(usuarioId, grupo);
        var integrante = await ObtenerIntegrante(grupoId, usuarioId, cancellationToken);
        await grupoRepositorio.QuitarIntegrante(integrante, cancellationToken);
    }
    public async Task Expulsar(
        int grupoId, int usuarioIdCreador, int usuarioIdAEliminar,
        CancellationToken cancellationToken = default)
    {
        ValidarGrupoYUsuario(grupoId, usuarioIdCreador);
        usuarioIdAEliminar.ValidarId("integrante");
        var grupo = await ObtenerGrupoDelCreador(grupoId, usuarioIdCreador,cancellationToken);
        ValidarExpulsion(grupo, usuarioIdAEliminar);
        var integrante = await ObtenerIntegrante(
            grupoId, usuarioIdAEliminar, cancellationToken);
        await grupoRepositorio.QuitarIntegrante(
            integrante, cancellationToken);
    }
    public async Task<IReadOnlyList<GrupoIntegranteDto>> ObtenerIntegrantes(
        int grupoId, CancellationToken cancellationToken = default)
    {
        grupoId.ValidarId("grupo");
        var grupo = await ObtenerGrupo(grupoId, cancellationToken);
        var integrantes = await grupoRepositorio.ObtenerIntegrantes(grupoId, cancellationToken);
        return integrantes.Select(integrante => integrante.ToDto(grupo.CreadorId)).ToList();
    }
    public async Task<IReadOnlyList<PublicacionDetalleDto>> ObtenerRecuerdos(
        int grupoId, CancellationToken cancellationToken = default)
    {
        grupoId.ValidarId("grupo");
        await ObtenerGrupo(grupoId, cancellationToken);
        return await grupoRepositorio.ObtenerRecuerdos(grupoId, cancellationToken);
    }

    public Task<int> CrearRecuerdo(int grupoId, int usuarioId, Publicacion publicacion, Stream? archivo = null,
        string? nombreArchivo = null, CancellationToken cancellationToken = default) =>
        CrearRecuerdo(grupoId, usuarioId, publicacion, archivo, nombreArchivo, cancellationToken, null);

    public Task<int> CrearRecuerdo(
        int grupoId, int usuarioId, Publicacion publicacion, Stream? archivo,
        string? nombreArchivo, CancellationToken cancellationToken, IReadOnlyList<global::ProyectoFinalTPI.Backend.Dtos.Publicacion.AdjuntoEntradaDto>? adjuntos) =>
        CrearRecuerdo(grupoId, usuarioId, publicacion, archivo, nombreArchivo, cancellationToken, adjuntos, Array.Empty<int>());

    public async Task<int> CrearRecuerdo(
        int grupoId, int usuarioId, Publicacion publicacion, Stream? archivo,
        string? nombreArchivo, CancellationToken cancellationToken, IReadOnlyList<global::ProyectoFinalTPI.Backend.Dtos.Publicacion.AdjuntoEntradaDto>? adjuntos,
        IReadOnlyCollection<int> capsulasIds)
    {
        ValidarGrupoYUsuario(grupoId, usuarioId);
        ArgumentNullException.ThrowIfNull(publicacion);
        await VerificarPermisoParaPublicar(
            grupoId, usuarioId, cancellationToken);
        AsociarRecuerdoAlGrupo(publicacion, grupoId, usuarioId);
        if (capsulasIds.Count > 0)
            return await publicacionServicio.Crear(publicacion, archivo, nombreArchivo, null, adjuntos, capsulasIds);
        return adjuntos is null ? await publicacionServicio.Crear(publicacion, archivo, nombreArchivo)
            : await publicacionServicio.Crear(publicacion, archivo, nombreArchivo, null, adjuntos);
    }

    #endregion

    #region Métodos privados

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
        grupo.Nombre.ValidarTexto(100, "nombre", minimo: 3);
        grupo.Descripcion.ValidarTexto(500, "descripción", minimo: 0);
        _ = new Decada(grupo.Decada);
    }
    private static void ValidarGrupoYUsuario(int grupoId, int usuarioId)
    {
        grupoId.ValidarId("grupo");
        usuarioId.ValidarId("usuario");
    }
    private static string ValidarBusqueda(string texto, int limite)
    {
        var textoNormalizado = texto.ValidarTexto(100, "texto de búsqueda", minimo: 2);
        limite.ValidarLimite(50);
        return textoNormalizado;
    }
    private async Task<Usuario> ObtenerCreador(Grupo grupo)
    {
        return await usuarioRepositorio.ObtenerPorIdAsync(grupo.CreadorId)
                    ?? throw new KeyNotFoundException("El usuario creador no existe.");
    }
    private static void PrepararGrupoParaCreacion(
        Grupo grupo, Usuario creador)
    {
        NormalizarDatos(grupo);
        grupo.FechaCreacion = DateTime.UtcNow;
        grupo.Creador = creador;
        grupo.Integrantes.Add(new GrupoIntegrante
        {
            UsuarioId = creador.Id,
            Usuario = creador,
            FechaUnion = grupo.FechaCreacion
        });
    }
    private static void NormalizarDatos(Grupo grupo)
    {
        grupo.Nombre = grupo.Nombre.Trim();
        grupo.Descripcion = grupo.Descripcion.Trim();
    }
    private async Task<Grupo> ObtenerGrupoDelCreador(int grupoId, int usuarioId, CancellationToken cancellationToken)
    {
        var grupo = await ObtenerGrupo(grupoId, cancellationToken);
        ValidarCreador(grupo, usuarioId);
        return grupo;
    }
    private static void AplicarCambios(Grupo grupo, Grupo cambios)
    {
        grupo.Nombre = cambios.Nombre.Trim();
        grupo.Descripcion = cambios.Descripcion.Trim();
        grupo.Decada = cambios.Decada;
    }
    private static GrupoIntegrante CrearIntegrante(
        int grupoId, int usuarioId) =>
        new()
        {
            GrupoId = grupoId,
            UsuarioId = usuarioId,
            FechaUnion = DateTime.UtcNow
        };
    private async Task VerificarUsuarioExistente(int usuarioId, CancellationToken cancellationToken)
    {
        if (!await usuarioRepositorio.ExisteUsuario(usuarioId, cancellationToken))
            throw new KeyNotFoundException("El usuario no existe.");
    }
    private async Task<GrupoIntegrante> ObtenerIntegrante(int grupoId, int usuarioId, CancellationToken cancellationToken)
    {
        return await grupoRepositorio.ObtenerIntegrante(grupoId, usuarioId, cancellationToken)
            ?? throw new KeyNotFoundException("El usuario no integra el grupo.");
    }

    private static void ValidarExpulsion(Grupo grupo, int usuarioId)
    {
        if (grupo.CreadorId == usuarioId)
            throw new ArgumentException("No se puede expulsar al creador del grupo.");
    }

    private static void ValidarSalida(int usuarioId, Grupo grupo)
    {
        if (grupo.CreadorId == usuarioId)
            throw new ArgumentException("El creador no puede abandonar su grupo; debe eliminarlo.");
    }
    private async Task VerificarPermisoParaPublicar(
        int grupoId, int usuarioId, CancellationToken cancellationToken)
    {
        await ObtenerGrupo(grupoId, cancellationToken);
        if (!await grupoRepositorio.EsIntegrante(
            grupoId, usuarioId, cancellationToken))
            throw new UnauthorizedAccessException(
                "Solo los integrantes pueden publicar recuerdos en el grupo.");
    }
    private static void AsociarRecuerdoAlGrupo(
        Publicacion publicacion, int grupoId, int usuarioId)
    {
        publicacion.GrupoId = grupoId;
        publicacion.UsuarioId = usuarioId;
    }

    #endregion
}
