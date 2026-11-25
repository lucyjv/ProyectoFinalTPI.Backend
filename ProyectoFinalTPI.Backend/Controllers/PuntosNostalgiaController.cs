using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.PuntosNostalgia;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using Swashbuckle.AspNetCore.Annotations;

namespace ProyectoFinalTPI.Backend.Controllers;

[ApiController]
[Route("api/puntos-nostalgia")]
public sealed class PuntosNostalgiaController(
    ICupoPuntosNostalgiaServicio cuposServicio,
    IMercadoPagoPuntosServicio mercadoPagoServicio,
    ICotizacionDolarServicio cotizacionServicio) : ControllerBase
{
    [HttpGet("saldo")]
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [SwaggerOperation(Summary = "Consulta tu saldo de puntos nostalgia.",
        Description = "Requiere una sesión válida. Devuelve el cupo diario de la categoría, los puntos diarios restantes, el reinicio en horario de Argentina, los puntos extra comprados y el total disponible. No consulta Mercado Pago ni la cotización del dólar.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Saldo diario y puntos extra del usuario autenticado.", typeof(SaldoDiarioPuntosNostalgiaDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una sesión válida.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "El usuario no existe.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "No se pudo consultar el saldo. Incluye traceId para localizar el error en los logs.", typeof(ProblemDetails))]
    [ProducesResponseType(typeof(SaldoDiarioPuntosNostalgiaDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SaldoDiarioPuntosNostalgiaDto>> ObtenerSaldo(CancellationToken cancellationToken)
    {
        var usuarioId = User.ObtenerUsuarioIdRequerido();
        var saldo = await cuposServicio.ObtenerSaldo(usuarioId, cancellationToken);
        return Ok(saldo);
    }

    [HttpGet("packs")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Consulta los packs de puntos disponibles.",
        Description = "Consulta pública. Incluye los puntos de cada pack y sus precios en USD y ARS, calculados con la cotización vigente o de respaldo.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Catálogo de packs con sus precios.", typeof(IReadOnlyList<PackPuntosDto>))]
    [ProducesResponseType(typeof(IReadOnlyList<PackPuntosDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PackPuntosDto>>> ObtenerPacks(CancellationToken cancellationToken)
    {
        var packs = await mercadoPagoServicio.ObtenerPacksDisponiblesAsync(cancellationToken);
        return Ok(packs);
    }

    [HttpGet("cotizacion")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Consulta la cotización del dólar para comprar puntos.",
        Description = "Consulta pública. Devuelve Moneda (USD) y CotizacionArs. Utiliza caché y una cotización de respaldo si el proveedor no está disponible.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Cotización utilizada para calcular los precios en pesos.", typeof(CotizacionPuntosDto))]
    [ProducesResponseType(typeof(CotizacionPuntosDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CotizacionPuntosDto>> ObtenerCotizacion(CancellationToken cancellationToken)
    {
        var cotizacion = await cotizacionServicio.ObtenerCotizacionAsync(cancellationToken);
        return Ok(new CotizacionPuntosDto("USD", cotizacion));
    }

    [HttpPost("comprar")]
    [Authorize]
    [SwaggerOperation(Summary = "Inicia la compra de un pack de puntos.",
        Description = "Requiere una sesión válida y el PackId del catálogo. Crea la preferencia de Mercado Pago y devuelve el enlace de pago. Los puntos se acreditan tras verificar un pago aprobado.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Preferencia creada con el enlace de pago.", typeof(PreferenciaCompraDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El pack solicitado no existe o los datos son inválidos.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una sesión válida.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "No se pudo generar la preferencia de pago.", typeof(ProblemDetails))]
    [ProducesResponseType(typeof(PreferenciaCompraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PreferenciaCompraDto>> CrearPreferencia(
        [FromBody] SolicitudCompraPackDto solicitud,
        CancellationToken cancellationToken)
    {
        var usuarioId = User.ObtenerUsuarioIdRequerido();
        var preferencia = await mercadoPagoServicio.CrearPreferenciaCompraAsync(usuarioId, solicitud.PackId, cancellationToken);
        return Ok(preferencia);
    }

    [HttpPost("confirmar")]
    [Authorize]
    [SwaggerOperation(Summary = "Verifica y acredita una compra de puntos.",
        Description = "Requiere una sesión válida y el PaymentId emitido por Mercado Pago. Consulta al proveedor y acredita los puntos únicamente si el pago está aprobado y su referencia coincide. Repetir la confirmación de una compra acreditada no vuelve a sumar puntos.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Compra aprobada y saldo resultante.", typeof(ResultadoCompraDto))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Falta PaymentId o el pago no pudo verificarse como aprobado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Se requiere una sesión válida.")]
    [ProducesResponseType(typeof(ResultadoCompraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResultadoCompraDto>> ConfirmarCompra(
        [FromBody] ConfirmacionCompraDto confirmacion,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(confirmacion.PaymentId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Pago no verificado",
                Detail = "Se requiere el ID de pago emitido por Mercado Pago para validar la acreditación."
            });
        }

        var resultado = await mercadoPagoServicio.ProcesarNotificacionPagoAsync(confirmacion.PaymentId, cancellationToken);
        if (resultado == null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Pago no aprobado",
                Detail = "Mercado Pago no ha confirmado la aprobación de este pago o la referencia no coincide."
            });
        }

        return Ok(resultado);
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Recibe notificaciones de pagos de Mercado Pago.",
        Description = "Endpoint público para el proveedor. Acepta el identificador del pago en id, data.id o en el cuerpo JSON. Verifica el pago con Mercado Pago antes de acreditar. Responde status=approved si acreditó o status=received si no confirmó un pago aprobado.")]
    [SwaggerResponse(StatusCodes.Status200OK, "Notificación recibida; la respuesta indica si se confirmó el pago.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "El cuerpo JSON o los parámetros son inválidos.", typeof(ProblemDetails))]
    public async Task<IActionResult> WebhookMercadoPago(
        [FromQuery] string? topic,
        [FromQuery] string? id,
        [FromQuery(Name = "data.id")] string? dataId,
        [FromBody] System.Text.Json.JsonElement? body,
        CancellationToken cancellationToken)
    {
        string? paymentId = id ?? dataId;

        if (string.IsNullOrWhiteSpace(paymentId) && body.HasValue)
        {
            if (body.Value.TryGetProperty("data", out var dataEl) && dataEl.TryGetProperty("id", out var idEl))
            {
                paymentId = idEl.GetString() ?? idEl.GetRawText();
            }
            else if (body.Value.TryGetProperty("id", out var directId))
            {
                paymentId = directId.GetString() ?? directId.GetRawText();
            }
        }

        if (!string.IsNullOrWhiteSpace(paymentId))
        {
            var resultado = await mercadoPagoServicio.ProcesarNotificacionPagoAsync(paymentId, cancellationToken);
            if (resultado != null)
            {
                return Ok(new { status = "approved", compraId = resultado.CompraId, puntos = resultado.PuntosAcreditados });
            }
        }

        return Ok(new { status = "received" });
    }
}
