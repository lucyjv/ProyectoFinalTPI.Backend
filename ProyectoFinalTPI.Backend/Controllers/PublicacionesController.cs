using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos.Mapa;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;
using ProyectoFinalTPI.Backend.Mappings;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Controllers
{
    [ApiController]
    [Route("api/publicaciones")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado al procesar la operación.", typeof(ProblemDetails))]
    public class PublicacionesController : ControllerBase
    {
        private readonly IPublicacionServicio _publicacionServicio;
        private readonly IEtiquetadoPublicacionServicio _etiquetadoPublicacionServicio;

        public PublicacionesController(
            IPublicacionServicio publicacionServicio,
            IEtiquetadoPublicacionServicio etiquetadoPublicacionServicio)
        {
            _publicacionServicio = publicacionServicio;
            _etiquetadoPublicacionServicio = etiquetadoPublicacionServicio;
        }

        [HttpGet("mapa")]
        [SwaggerOperation(
            Summary = "Obtiene los pines de publicaciones para mostrar en el mapa.",
            Description = "Permite filtrar por un área geográfica, categoría y fechas del recuerdo. Solo devuelve publicaciones visibles y no requiere iniciar sesión. Devuelve hasta 200 pines; hayMas indica si el área contiene más resultados."
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Pines encontrados e indicador de resultados adicionales.", typeof(ResultadoMapaDto))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "El área geográfica, la categoría o las fechas no son válidas.", typeof(ProblemDetails))]
        public async Task<ActionResult<ResultadoMapaDto>> ObtenerMapa(
            [FromQuery] PublicacionFiltroDto filtro,
            CancellationToken cancellationToken)
        {
            return Ok(await _publicacionServicio.ObtenerPines(
                filtro, cancellationToken));
        }

        [HttpGet("{id:int}/resumen")]
        [SwaggerOperation(
            Summary = "Obtiene el resumen de una publicación.",
            Description = "Devuelve los datos resumidos de una publicación visible a partir de su identificador."
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Resumen de la publicación.", typeof(PublicacionResumenDto))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador de la publicación no es válido.", typeof(ProblemDetails))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "La publicación no existe o no está disponible.", typeof(ProblemDetails))]
        public async Task<ActionResult<PublicacionResumenDto>> ObtenerResumen(
            int id,
            CancellationToken cancellationToken)
        {
            return Ok(await _publicacionServicio.ObtenerResumenPorId(
                id, cancellationToken));
        }

        [HttpGet("{id:int}")]
        [SwaggerOperation(
            Summary = "Obtiene el detalle de una publicación.",
            Description = "Devuelve una publicación visible con su información multimedia y el estado de sus likes para el usuario identificado."
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Detalle de la publicación.", typeof(PublicacionDetalleDto))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador de la publicación no es válido.", typeof(ProblemDetails))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "La publicación no existe o no está disponible.", typeof(ProblemDetails))]
        public async Task<ActionResult<PublicacionDetalleDto>> ObtenerDetalle(
            int id,
            CancellationToken cancellationToken)
        {
            var detalle = await _publicacionServicio.ObtenerDetallePorId(id, cancellationToken);
            var usuarioIdActual = User.ObtenerUsuarioId();
            await _publicacionServicio.CompletarResumenesLikes(
                [detalle], usuarioIdActual ?? 0, cancellationToken);
            await _publicacionServicio.CompletarResumenesPuntuaciones(
                [detalle], usuarioIdActual, cancellationToken);

            return Ok(detalle);
        }

        [HttpPost]
        [SwaggerOperation(
            Summary = "Crea una publicación.",
            Description = "Recibe los datos del recuerdo y su ubicación como multipart/form-data. Debe enviarse un archivo o una URL HTTPS, no ambos. El archivo puede ocupar hasta 50 MB. Se utiliza el autor autenticado cuando está disponible; en caso contrario, AutorId."
        )]
        [SwaggerResponse(StatusCodes.Status201Created, "Publicación creada. Devuelve su id y la cabecera Location.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Los datos del recuerdo, la ubicación o el contenido multimedia no son válidos.", typeof(ProblemDetails))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "El autor indicado no existe.", typeof(ProblemDetails))]
        [SwaggerResponse(StatusCodes.Status415UnsupportedMediaType, "El formato del cuerpo no es compatible; utilice multipart/form-data.", typeof(ProblemDetails))]
        public async Task<IActionResult> Crear([FromForm] CrearPublicacionRequest request)
        {
            var autorIdAutenticado = User.ObtenerUsuarioId();
            var autorIdFinal = (autorIdAutenticado.HasValue && autorIdAutenticado.Value > 0)
                ? autorIdAutenticado.Value
                : request.AutorId;

            using var stream = request.Archivo?.OpenReadStream();

            var id = await _publicacionServicio.Crear(
                request.ToPublicacion(autorIdFinal), stream, request.Archivo?.FileName);

            return Created(
                $"/api/publicaciones/{id}",
                new { id });
        }

        [HttpPost("compartir")]
        [SwaggerOperation(
            Summary = "Comparte una publicación.",
            Description = "Crea una publicación vinculada a la original, con un comentario opcional. Conserva sus datos multimedia y ubicación. Se utiliza el autor autenticado cuando está disponible; en caso contrario, AutorId."
        )]
        [SwaggerResponse(StatusCodes.Status201Created, "Publicación compartida. Devuelve su id y la cabecera Location.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Los datos para compartir no son válidos.", typeof(ProblemDetails))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "El autor o la publicación original no existen o no están disponibles.", typeof(ProblemDetails))]
        [SwaggerResponse(StatusCodes.Status415UnsupportedMediaType, "El formato del cuerpo no es compatible; utilice application/json.", typeof(ProblemDetails))]
        public async Task<IActionResult> Compartir(
            [FromBody] CompartirPublicacionRequest request,
            CancellationToken cancellationToken = default)
        {
            var autorIdAutenticado = User.ObtenerUsuarioId();
            var autorIdFinal = (autorIdAutenticado.HasValue && autorIdAutenticado.Value > 0)
                ? autorIdAutenticado.Value
                : request.AutorId;

            var id = await _publicacionServicio.Compartir(
                request.ToPublicacion(autorIdFinal), cancellationToken);

            return Created(
                $"/api/publicaciones/{id}",
                new { id });
        }

        [HttpDelete("compartir/{originalId:int}")]
        [SwaggerOperation(
            Summary = "Elimina un repost.",
            Description = "Elimina la publicación compartida asociada al identificador original y al autor indicado. Se utiliza el autor autenticado cuando está disponible; en caso contrario, autorId."
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Repost eliminado.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador de la publicación original no es válido.", typeof(ProblemDetails))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "El repost no existe o no se pudo eliminar para el autor indicado.", typeof(ProblemDetails))]
        public async Task<IActionResult> EliminarRepost(
            int originalId,
            [FromQuery] int? autorId = null,
            CancellationToken cancellationToken = default)
        {
            var autorIdAutenticado = User.ObtenerUsuarioId();
            var autorIdFinal = (autorIdAutenticado.HasValue && autorIdAutenticado.Value > 0)
                ? autorIdAutenticado.Value
                : autorId;

            await _publicacionServicio.EliminarRepost(originalId, autorIdFinal, cancellationToken);
            return Ok(new { mensaje = "Repost eliminado con éxito.", id = originalId });
        }

        [HttpDelete("{id:int}")]
        [SwaggerOperation(
            Summary = "Elimina una publicación.",
            Description = "Elimina una publicación por su identificador. Se utiliza el autor autenticado cuando está disponible; en caso contrario, autorId."
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Publicación eliminada.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador de la publicación no es válido.", typeof(ProblemDetails))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "La publicación no existe o no se pudo eliminar para el autor indicado.", typeof(ProblemDetails))]
        public async Task<IActionResult> Eliminar(
            int id,
            [FromQuery] int? autorId = null,
            CancellationToken cancellationToken = default)
        {
            var autorIdAutenticado = User.ObtenerUsuarioId();
            var autorIdFinal = (autorIdAutenticado.HasValue && autorIdAutenticado.Value > 0)
                ? autorIdAutenticado.Value
                : autorId;

            await _publicacionServicio.Eliminar(id, autorIdFinal, cancellationToken);
            return Ok(new { mensaje = "Publicación eliminada con éxito.", id });
        }

        [HttpPost("{publicacionId:int}/etiquetas")]
        [SwaggerOperation(
            Summary = "Etiqueta usuarios en una publicación.",
            Description = "Recibe una lista de identificadores de usuarios. Crea las etiquetas faltantes sin duplicar las existentes."
        )]
        [SwaggerResponse(StatusCodes.Status201Created, "Se crearon nuevas etiquetas.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Todos los usuarios indicados ya estaban etiquetados.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador de la publicación o la lista de usuarios no son válidos.", typeof(ProblemDetails))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "La publicación o alguno de los usuarios no existen.", typeof(ProblemDetails))]
        [SwaggerResponse(StatusCodes.Status415UnsupportedMediaType, "El formato del cuerpo no es compatible; utilice application/json.", typeof(ProblemDetails))]
        public async Task<IActionResult> EtiquetarUsuarios(
            int publicacionId, [FromBody] EtiquetarUsuariosRequest request, CancellationToken cancellationToken)
        {
            var cantidadEtiquetasNuevas = await _etiquetadoPublicacionServicio.EtiquetarUsuarios(
                publicacionId, request.UsuariosIds, cancellationToken);

            var respuesta = new
            {
                mensaje = cantidadEtiquetasNuevas > 0
                    ? "Se etiquetaron los usuarios en la publicacion."
                    : "usuarios ya etiquetados en la publicacion",
                publicacionId,
                cantidadEtiquetasNuevas
            };

            return cantidadEtiquetasNuevas > 0
                ? StatusCode(StatusCodes.Status201Created, respuesta)
                : Ok(respuesta);
        }

        [Authorize]
        [HttpPost("{publicacionId:int}/like")]
        [SwaggerOperation(
            Summary = "Agrega un like a una publicación.",
            Description = "Requiere autenticación. Registra el like del usuario y devuelve la cantidad de likes y su estado actualizado."
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Estado actualizado de los likes.", typeof(MeGustaPublicacionDto))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador de la publicación no es válido.", typeof(ProblemDetails))]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "No hay una autenticación válida o el token no identifica a un usuario válido.")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "El usuario o la publicación visible no existen.", typeof(ProblemDetails))]
        public async Task<ActionResult<MeGustaPublicacionDto>> DarLike(
            int publicacionId, CancellationToken cancellationToken = default)
        {
            var usuarioId = User.ObtenerUsuarioId();
            if (!usuarioId.HasValue || usuarioId.Value <= 0) return Unauthorized();

            return Ok(await _publicacionServicio.DarLike( publicacionId, usuarioId.Value, cancellationToken));
        }

        [Authorize]
        [HttpDelete("{publicacionId:int}/like")]
        [SwaggerOperation(
            Summary = "Quita un like de una publicación.",
            Description = "Requiere autenticación. Quita el like del usuario y devuelve la cantidad de likes y su estado actualizado."
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Estado actualizado de los likes.", typeof(MeGustaPublicacionDto))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador de la publicación no es válido.", typeof(ProblemDetails))]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "No hay una autenticación válida o el token no identifica a un usuario válido.")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "El usuario o la publicación visible no existen.", typeof(ProblemDetails))]
        public async Task<ActionResult<MeGustaPublicacionDto>> QuitarLike(
            int publicacionId, CancellationToken cancellationToken = default)
        {
            var usuarioId = User.ObtenerUsuarioId();
            if (!usuarioId.HasValue || usuarioId.Value <= 0) return Unauthorized();

            return Ok(await _publicacionServicio.QuitarLike( publicacionId, usuarioId.Value, cancellationToken));
        }

        [Authorize]
        [HttpPut("{publicacionId:int}/puntuacion")]
        [SwaggerOperation(
            Summary = "Puntúa una publicación (1 a 5).",
            Description = "Requiere autenticación y categoría Nostálgico. Si se envía la misma puntuación previa, se desmarca/elimina (toggle)."
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Puntuación registrada o actualizada.", typeof(PuntuacionPublicacionDto))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Puntuación o publicación no válida.", typeof(ProblemDetails))]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "No autenticado.")]
        [SwaggerResponse(StatusCodes.Status403Forbidden, "Nivel insuficiente para puntuar (debe ser Nostálgico o superior).", typeof(ProblemDetails))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "Publicación no encontrada.", typeof(ProblemDetails))]
        public async Task<ActionResult<PuntuacionPublicacionDto>> Puntuar(
            int publicacionId,
            [FromBody] PuntuarPublicacionDto dto,
            CancellationToken cancellationToken = default)
        {
            var usuarioId = User.ObtenerUsuarioId();
            if (!usuarioId.HasValue || usuarioId.Value <= 0) return Unauthorized();

            try
            {
                var resultado = await _publicacionServicio.Puntuar(
                    publicacionId, usuarioId.Value, dto.Puntaje, cancellationToken);
                return Ok(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "Nivel insuficiente para puntuar",
                    Detail = ex.Message,
                    Status = StatusCodes.Status403Forbidden
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Operación no permitida",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        [Authorize]
        [HttpDelete("{publicacionId:int}/puntuacion")]
        [SwaggerOperation(
            Summary = "Quita la puntuación de una publicación.",
            Description = "Requiere autenticación. Elimina la puntuación del usuario en la publicación y devuelve el estado actualizado."
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Puntuación eliminada.", typeof(PuntuacionPublicacionDto))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "El identificador de la publicación no es válido.", typeof(ProblemDetails))]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "No autenticado.")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "Publicación no encontrada.", typeof(ProblemDetails))]
        public async Task<ActionResult<PuntuacionPublicacionDto>> QuitarPuntuacion(
            int publicacionId, CancellationToken cancellationToken = default)
        {
            var usuarioId = User.ObtenerUsuarioId();
            if (!usuarioId.HasValue || usuarioId.Value <= 0) return Unauthorized();

            var resultado = await _publicacionServicio.QuitarPuntuacion(
                publicacionId, usuarioId.Value, cancellationToken);
            return Ok(resultado);
        }



    }
}
