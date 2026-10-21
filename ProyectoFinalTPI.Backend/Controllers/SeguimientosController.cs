using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Seguimientos;

namespace ProyectoFinalTPI.Backend.Controllers
{
    [ApiController]
    [Route("api/seguimientos")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado al procesar la operación.")]
    public class SeguimientosController : ControllerBase
    {
        private readonly ISeguimientoServicio _seguimientoServicio;
        private readonly ILogger<SeguimientosController> _logger;

        public SeguimientosController( ISeguimientoServicio seguimientoServicio, ILogger<SeguimientosController> logger)
        {
            _seguimientoServicio = seguimientoServicio;
            _logger = logger;
        }

        [HttpPost]
        [SwaggerOperation(
            Summary = "Sigue a un usuario.",
            Description = "Recibe los identificadores en el cuerpo JSON y prioriza el usuario autenticado sobre IdUsuario. Repetir la operación no duplica relaciones ni genera cambios adicionales."
        )]
        [SwaggerResponse(StatusCodes.Status201Created, "Se creó el seguimiento.")]
        [SwaggerResponse(StatusCodes.Status200OK, "El usuario ya estaba seguido.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Los identificadores no son válidos o se intenta operar sobre el propio usuario.")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "Uno o ambos usuarios no existen.")]
        [SwaggerResponse(StatusCodes.Status415UnsupportedMediaType, "Formato de contenido no compatible; utilice application/json.", typeof(ProblemDetails))]
        public Task<IActionResult> SeguirUsuario(
            [FromBody] SeguirUsuarioRequest request, CancellationToken cancellationToken)
        {
            var idUsuario = User.ObtenerUsuarioId() ?? request.IdUsuario;
            return EjecutarOperacion( ()
                => _seguimientoServicio.SeguirUsuario( idUsuario, request.IdUsuarioASeguir,cancellationToken),

                MapearResultadoSeguir, idUsuario,request.IdUsuarioASeguir,cancellationToken,"crear");
        }

        [HttpPost("{idUsuarioASeguir:int}")]
        [SwaggerOperation(
            Summary = "Sigue a un usuario.",
            Description = "Recibe el destinatario por ruta y el usuario por token o, como alternativa, idUsuario en la consulta. Repetir la operación no duplica relaciones ni genera cambios adicionales."
        )]
        [SwaggerResponse(StatusCodes.Status201Created, "Se creó el seguimiento.")]
        [SwaggerResponse(StatusCodes.Status200OK, "El usuario ya estaba seguido.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Los identificadores no son válidos o se intenta operar sobre el propio usuario.")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "Uno o ambos usuarios no existen.")]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "No se pudo identificar al usuario que realiza la operación.")]
        public Task<IActionResult> SeguirUsuarioPorRuta(
            int idUsuarioASeguir, [FromQuery] int? idUsuario = null, CancellationToken cancellationToken = default)
        {
            if (idUsuarioASeguir <= 0)
            {
                return Task.FromResult<IActionResult>(BadRequest(new { error = "El ID del usuario es erroneo" }));
            }

            var seguidorId = User.ObtenerUsuarioId() ?? idUsuario ?? 0;
            if (seguidorId <= 0)
            {
                return Task.FromResult<IActionResult>(Unauthorized(new { error = "Debés iniciar sesión para seguir a un usuario." }));
            }

            return EjecutarOperacion( ()
                => _seguimientoServicio.SeguirUsuario( seguidorId, idUsuarioASeguir, cancellationToken),

                MapearResultadoSeguir, seguidorId, idUsuarioASeguir, cancellationToken, "crear");
        }

        [HttpDelete("{idUsuarioASeguir:int}")]
        [SwaggerOperation(
            Summary = "Deja de seguir a un usuario.",
            Description = "Recibe el destinatario por ruta y el usuario por token o, como alternativa, idUsuario en la consulta. Repetir la operación no duplica relaciones ni genera cambios adicionales."
        )]
        [SwaggerResponse(StatusCodes.Status204NoContent, "Se eliminó el seguimiento o ya no existía; no devuelve contenido.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Los identificadores no son válidos o se intenta operar sobre el propio usuario.")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "Uno o ambos usuarios no existen.")]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "No se pudo identificar al usuario que realiza la operación.")]
        public Task<IActionResult> DejarDeSeguirUsuarioPorRuta(
            int idUsuarioASeguir, [FromQuery] int? idUsuario = null, CancellationToken cancellationToken = default)
        {
            if (idUsuarioASeguir <= 0)
            {
                return Task.FromResult<IActionResult>(BadRequest(new { error = "El ID del usuario es erroneo" }));
            }

            var seguidorId = User.ObtenerUsuarioId() ?? idUsuario ?? 0;
            if (seguidorId <= 0)
            {
                return Task.FromResult<IActionResult>(Unauthorized(new { error = "Debés iniciar sesión para dejar de seguir a un usuario." }));
            }

            return EjecutarOperacion(() 
                => _seguimientoServicio.DejarDeSeguirUsuario( seguidorId, idUsuarioASeguir, cancellationToken),
                
                MapearResultadoDejarDeSeguir, seguidorId, idUsuarioASeguir, cancellationToken, "eliminar");
        }

        [HttpDelete]
        [SwaggerOperation(
            Summary = "Deja de seguir a un usuario.",
            Description = "Recibe los identificadores en el cuerpo JSON y prioriza el usuario autenticado sobre IdUsuario. Repetir la operación no duplica relaciones ni genera cambios adicionales."
        )]
        [SwaggerResponse(StatusCodes.Status204NoContent, "Se eliminó el seguimiento o ya no existía; no devuelve contenido.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Los identificadores no son válidos o se intenta operar sobre el propio usuario.")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "Uno o ambos usuarios no existen.")]
        [SwaggerResponse(StatusCodes.Status415UnsupportedMediaType, "Formato de contenido no compatible; utilice application/json.", typeof(ProblemDetails))]
        public Task<IActionResult> DejarDeSeguirUsuario(
            [FromBody] DejarDeSeguirUsuarioRequest request, CancellationToken cancellationToken)
        {
            var idUsuario = User.ObtenerUsuarioId() ?? request.IdUsuario;
            return EjecutarOperacion(() 
                => _seguimientoServicio.DejarDeSeguirUsuario( idUsuario, request.IdUsuarioASeguir,cancellationToken),
                
                MapearResultadoDejarDeSeguir, idUsuario, request.IdUsuarioASeguir,cancellationToken,"eliminar");
        }

        [HttpGet("{idUsuario:int}/seguidos")]
        [SwaggerOperation(
            Summary = "Lista los usuarios seguidos.",
            Description = "Devuelve los usuarios seguidos asociados al identificador indicado. La lista puede estar vacía."
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Lista de usuarios seguidos.", typeof(IReadOnlyList<ProyectoFinalTPI.Backend.Dtos.Usuario.AutorResumenDto>))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Los datos o parámetros enviados no son válidos.")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "El usuario indicado no existe.")]
        public async Task<IActionResult> ListarSeguidos( int idUsuario, CancellationToken cancellationToken)
        {
            if (idUsuario <= 0)
            {
                return BadRequest(new { error = "El ID del usuario es erroneo" });
            }

            try
            {
                var usuariosSeguidos = await _seguimientoServicio.ListarSeguidos( idUsuario, cancellationToken);

                return usuariosSeguidos is null
                    ? NotFound(new { error = "No se encontro el usuario" })
                    : Ok(usuariosSeguidos);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error al listar los usuarios seguidos por el usuario {IdUsuario}.",
                    idUsuario);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { error = "Ocurrio un error al listar los usuarios seguidos" });
            }
        }

        [HttpGet("{idUsuario:int}/seguidores")]
        [SwaggerOperation(
            Summary = "Lista los usuarios seguidores.",
            Description = "Devuelve los usuarios seguidores asociados al identificador indicado. La lista puede estar vacía."
        )]
        [SwaggerResponse(StatusCodes.Status200OK, "Lista de usuarios seguidores.", typeof(IReadOnlyList<ProyectoFinalTPI.Backend.Dtos.Usuario.AutorResumenDto>))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "Los datos o parámetros enviados no son válidos.")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "El usuario indicado no existe.")]
        public async Task<IActionResult> ListarSeguidores(
            int idUsuario, CancellationToken cancellationToken)
        {
            if (idUsuario <= 0)
            {
                return BadRequest(new { error = "El ID del usuario es erroneo" });
            }

            try
            {
                var usuariosSeguidores = await _seguimientoServicio.ListarSeguidores(
                    idUsuario,
                    cancellationToken);

                return usuariosSeguidores is null
                    ? NotFound(new { error = "No se encontro el usuario" })
                    : Ok(usuariosSeguidores);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error al listar los seguidores del usuario {IdUsuario}.",
                    idUsuario);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { error = "Ocurrio un error al listar los seguidores" });
            }
        }







        private IActionResult MapearResultadoSeguir(ResultadoSeguimiento resultado)
        {
            return resultado switch
            {
                ResultadoSeguimiento.Aplicado => StatusCode( StatusCodes.Status201Created,new { mensaje = "Ahora seguís a este usuario." }),
                ResultadoSeguimiento.SinCambios => Ok(new { mensaje = "Ya seguías a este usuario."}),
                ResultadoSeguimiento.AutoSeguimiento => BadRequest(new { error = "No podés seguirte a vos mismo." }),
                ResultadoSeguimiento.UsuarioNoEncontrado => NotFound(new { error = "No se encontró uno o ambos usuarios." }),

                _ => ErrorInterno()
            };
        }

        private IActionResult MapearResultadoDejarDeSeguir( ResultadoSeguimiento resultado)
        {
            return resultado switch
            {
                ResultadoSeguimiento.Aplicado or ResultadoSeguimiento.SinCambios => NoContent(),
                ResultadoSeguimiento.AutoSeguimiento => BadRequest(new { error = "No podés dejar de seguirte a vos mismo" }),
                ResultadoSeguimiento.UsuarioNoEncontrado => NotFound(new { error = "No se encontro uno o ambos usuarios" }),

                _ => ErrorInterno()
            };
        }

        private async Task<IActionResult> EjecutarOperacion<TResult>(
            Func<Task<TResult>> ejecutar, Func<TResult, IActionResult> mapearResultado,
            int idUsuario, int idUsuarioASeguir, CancellationToken cancellationToken, string accion)
        {
            try
            {
                var resultado = await ejecutar();
                return mapearResultado(resultado);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error al {Accion} la relación de seguimiento entre los usuarios {IdUsuario} y {IdUsuarioASeguir}.",
                    accion,
                    idUsuario,
                    idUsuarioASeguir);

                return ErrorInterno();
            }
        }

        private IActionResult ErrorInterno()
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "Ocurrió un error al procesar el seguimiento." });
        }
    }
}
