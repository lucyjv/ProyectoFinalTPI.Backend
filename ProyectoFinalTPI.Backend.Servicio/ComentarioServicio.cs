using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Comentario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio.Mappings;

namespace ProyectoFinalTPI.Backend.Servicio;

public class ComentarioServicio(
    IComentarioRepositorio _comentarioRepositorio,
    IUsuarioRepositorio _usuarioRepositorio,
    INotificacionServicio _notificacionServicio,
    ILogroServicio _logroServicio,
    IPublicacionRepositorio _publicacionRepositorio) : IComentarioServicio
{
    private const int LimiteRespuestasIniciales = 3;

    public async Task<ResultadoPaginadoDto<ComentarioDto>> ObtenerRaices(
        int publicacionId, string? cursor, int limite, int? usuarioId,
        CancellationToken cancellationToken = default)
    {
        await ValidarPublicacion(publicacionId, cancellationToken);
        ValidarLimite(limite, 50);
        var comentarios = await _comentarioRepositorio.ObtenerRaices(
            publicacionId, CursorCronologicoCodec.Leer(cursor), limite, cancellationToken);
        var raices = comentarios.Take(limite).ToList();
        var respuestasPorRaiz = new Dictionary<int, List<Comentario>>();
        foreach (var raiz in raices)
        {
            respuestasPorRaiz[raiz.Id] = await _comentarioRepositorio.ObtenerRespuestas(
                publicacionId, raiz.Id, null, LimiteRespuestasIniciales, cancellationToken);
        }

        var ids = raices.Select(comentario => comentario.Id)
            .Concat(respuestasPorRaiz.Values.SelectMany(respuestas => respuestas.Take(LimiteRespuestasIniciales))
                .Select(respuesta => respuesta.Id))
            .ToArray();
        var cantidades = await _comentarioRepositorio.ContarRespuestasDirectas(ids, cancellationToken);
        var items = raices.Select(raiz =>
        {
            var respuestas = respuestasPorRaiz[raiz.Id];
            var respuestasVisibles = respuestas.Take(LimiteRespuestasIniciales).ToList();
            return raiz.ToDto(
                usuarioId,
                Cantidad(cantidades, raiz.Id),
                respuestasVisibles.Select(respuesta =>
                    respuesta.ToDto(usuarioId, Cantidad(cantidades, respuesta.Id))).ToList(),
                CrearSiguienteCursor(respuestas, respuestasVisibles));
        }).ToList();

        return Pagina(items, comentarios.Count > limite ? raices.LastOrDefault() : null);
    }

    public async Task<ResultadoPaginadoDto<ComentarioDto>> ObtenerRespuestas(
        int publicacionId, int comentarioId, string? cursor, int limite, int? usuarioId,
        CancellationToken cancellationToken = default)
    {
        await ValidarPublicacion(publicacionId, cancellationToken);
        ValidarId(comentarioId, "comentario");
        ValidarLimite(limite, 20);
        _ = await _comentarioRepositorio.Obtener(publicacionId, comentarioId, cancellationToken)
            ?? throw new KeyNotFoundException("El comentario no existe o no está disponible.");
        var respuestas = await _comentarioRepositorio.ObtenerRespuestas(
            publicacionId, comentarioId, CursorCronologicoCodec.Leer(cursor), limite, cancellationToken);
        var visibles = respuestas.Take(limite).ToList();
        var cantidades = await _comentarioRepositorio.ContarRespuestasDirectas(
            visibles.Select(respuesta => respuesta.Id).ToArray(), cancellationToken);
        var items = visibles.Select(respuesta =>
            respuesta.ToDto(usuarioId, Cantidad(cantidades, respuesta.Id))).ToList();
        return Pagina(items, respuestas.Count > limite ? visibles.LastOrDefault() : null);
    }

    public async Task<ComentarioDto> Crear(
        Comentario comentario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comentario);
        await ValidarPublicacion(comentario.PublicacionId, cancellationToken);
        ValidarId(comentario.UsuarioId, "usuario");

        var contenido = comentario.Contenido?.Trim() ?? string.Empty;
        if (contenido.Length == 0) throw new ArgumentException("Debe indicar el contenido del comentario.");
        if (contenido.Length > 1000) throw new ArgumentException("El comentario no puede superar los 1000 caracteres.");
        
        var usuario = await _usuarioRepositorio.ObtenerPorIdAsync(comentario.UsuarioId)
            ?? throw new KeyNotFoundException("El usuario indicado no existe.");

        Comentario? padre = null;
        if (comentario.ComentarioPadreId.HasValue)
        {
            padre = await _comentarioRepositorio.Obtener(
                comentario.PublicacionId, comentario.ComentarioPadreId.Value, cancellationToken)
                ?? throw new KeyNotFoundException("El comentario al que querés responder no existe.");
            if (padre.EstaEliminado)
                throw new ArgumentException("No se puede responder a un comentario eliminado.");
        }

        comentario.Contenido = contenido;
        comentario.FechaCreacion = DateTime.UtcNow;
        comentario.UsuarioId = usuario.Id;
        comentario.Usuario = usuario;
        comentario.ComentarioPadreId = padre?.Id;

        await _comentarioRepositorio.Crear(comentario, cancellationToken);

        // Notificar al autor de la publicación si el comentador no es el mismo autor.
        var publicacion = await ObtenerPublicacionParaNotificacion(
            comentario.PublicacionId, cancellationToken);
        if (publicacion != null && publicacion.UsuarioId != comentario.UsuarioId)
        {
            await _notificacionServicio.NotificarComentarioEnPublicacionAsync(
                publicacion.UsuarioId,
                comentario.PublicacionId,
                comentario.UsuarioId,
                cancellationToken);
        }
        await VerificarLogrosComentario(comentario.UsuarioId, comentario.PublicacionId, cancellationToken);

        return comentario.ToDto(comentario.UsuarioId);
    }

    public async Task Eliminar(
        int publicacionId, int comentarioId, int usuarioId, CancellationToken cancellationToken = default)
    {
        ValidarId(usuarioId, "usuario");
        var comentario = await _comentarioRepositorio.Obtener(publicacionId, comentarioId, cancellationToken)
            ?? throw new KeyNotFoundException("El comentario no existe o no está disponible.");
        if (comentario.UsuarioId != usuarioId)
            throw new UnauthorizedAccessException("No podés eliminar un comentario de otra persona.");
        if (comentario.EstaEliminado) return;
        await _comentarioRepositorio.Eliminar(comentario, cancellationToken);
    }

    public async Task<ComentarioDto> EstablecerReaccion(
        int publicacionId, int comentarioId, int usuarioId, string tipo,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<TipoReaccionComentario>(tipo, true, out var reaccion))
            throw new ArgumentException("La reacción indicada no es válida.");
        var comentario = await ObtenerParaInteraccion(publicacionId, comentarioId, cancellationToken);
        await _comentarioRepositorio.EstablecerReaccion(comentario.Id, usuarioId, reaccion, cancellationToken);
        return await ObtenerDto(publicacionId, comentarioId, usuarioId, cancellationToken);
    }

    public async Task<ComentarioDto> QuitarReaccion(
        int publicacionId, int comentarioId, int usuarioId, CancellationToken cancellationToken = default)
    {
        var comentario = await ObtenerParaInteraccion(publicacionId, comentarioId, cancellationToken);
        await _comentarioRepositorio.QuitarReaccion(comentario.Id, usuarioId, cancellationToken);
        return await ObtenerDto(publicacionId, comentarioId, usuarioId, cancellationToken);
    }

    private async Task<Comentario> ObtenerParaInteraccion(
        int publicacionId, int comentarioId, CancellationToken cancellationToken)
    {
        var comentario = await _comentarioRepositorio.Obtener(publicacionId, comentarioId, cancellationToken)
            ?? throw new KeyNotFoundException("El comentario no existe o no está disponible.");
        if (comentario.EstaEliminado)
            throw new ArgumentException("No se puede interactuar con un comentario eliminado.");
        return comentario;
    }

    private async Task<ComentarioDto> ObtenerDto(
        int publicacionId, int comentarioId, int usuarioId, CancellationToken cancellationToken)
    {
        var comentario = await _comentarioRepositorio.ObtenerDetalle(publicacionId, comentarioId, cancellationToken)
            ?? throw new KeyNotFoundException("El comentario no existe o no está disponible.");
        var cantidades = await _comentarioRepositorio.ContarRespuestasDirectas([comentarioId], cancellationToken);
        return comentario.ToDto(usuarioId, Cantidad(cantidades, comentarioId));
    }

    private async Task ValidarPublicacion(int publicacionId, CancellationToken cancellationToken)
    {
        ValidarId(publicacionId, "publicación");
        if (!await _comentarioRepositorio.ExistePublicacionVisible(publicacionId, cancellationToken))
            throw new KeyNotFoundException("La publicación no existe o no está disponible.");
    }

    private static void ValidarId(int id, string recurso)
    {
        if (id <= 0) throw new ArgumentException($"El ID de {recurso} debe ser mayor que cero.");
    }

    private static void ValidarLimite(int limite, int maximo)
    {
        if (limite < 1 || limite > maximo)
            throw new ArgumentException($"El límite debe estar entre 1 y {maximo}.");
    }

    private static int Cantidad(IReadOnlyDictionary<int, int> cantidades, int comentarioId) =>
        cantidades.GetValueOrDefault(comentarioId);

    private static string? CrearSiguienteCursor(List<Comentario> pagina, List<Comentario> visibles) =>
        pagina.Count > visibles.Count && visibles.Count > 0
            ? CursorCronologicoCodec.Crear(visibles[^1].FechaCreacion, visibles[^1].Id)
            : null;

    private static ResultadoPaginadoDto<ComentarioDto> Pagina(
        List<ComentarioDto> items, Comentario? ultimoConMasResultados) => new()
        {
            Items = items,
            SiguienteCursor = ultimoConMasResultados is null
            ? null
            : CursorCronologicoCodec.Crear(ultimoConMasResultados.FechaCreacion, ultimoConMasResultados.Id)
        };
    private async Task VerificarLogrosComentario(int usuarioId, int publicacionId, CancellationToken cancellationToken)
    {
        var publicacion = await _comentarioRepositorio.ObtenerPublicacionAsync(publicacionId, cancellationToken);
        if (publicacion == null) return;

        if (publicacion.UsuarioId != usuarioId)
        {
            int comentariosEnPosteosAjenos = await _comentarioRepositorio
                .ContarComentariosEnPosteosAjenosAsync(usuarioId, cancellationToken);

            if (comentariosEnPosteosAjenos >= 1)
            {
                await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.PrimeraPalabra);
            }
        }

        int totalComentarios = await _comentarioRepositorio.ContarComentariosTotalesAsync(usuarioId, cancellationToken);
        if (totalComentarios >= 25)
        {
            await _logroServicio.VerificarYDarLogro(usuarioId, LogroEnum.Conversador);
        }
    }

    private async Task<Publicacion?> ObtenerPublicacionParaNotificacion(
    int publicacionId, CancellationToken cancellationToken)
    {
        // Necesitamos saber el UsuarioId del autor de la publicación.
        // Usamos el repositorio de comentarios que ya existe en este servicio.
        // Si no existe, simplemente no notificamos (no rompemos el flujo principal).
        return await _comentarioRepositorio.ObtenerPublicacionAsync(
            publicacionId, cancellationToken);
    }
}
