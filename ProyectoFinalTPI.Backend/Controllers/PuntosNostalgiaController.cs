using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.PuntosNostalgia;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

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
    [ProducesResponseType(typeof(SaldoDiarioPuntosNostalgiaDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SaldoDiarioPuntosNostalgiaDto>> ObtenerSaldo(CancellationToken cancellationToken)
    {
        var usuarioId = User.ObtenerUsuarioIdRequerido();
        var saldo = await cuposServicio.ObtenerSaldo(usuarioId, cancellationToken);
        return Ok(saldo);
    }

    [HttpGet("packs")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<PackPuntosDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PackPuntosDto>>> ObtenerPacks(CancellationToken cancellationToken)
    {
        var packs = await mercadoPagoServicio.ObtenerPacksDisponiblesAsync(cancellationToken);
        return Ok(packs);
    }

    [HttpGet("cotizacion")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> ObtenerCotizacion(CancellationToken cancellationToken)
    {
        var cotizacion = await cotizacionServicio.ObtenerCotizacionAsync(cancellationToken);
        return Ok(new { Moneda = "USD", CotizacionArs = cotizacion });
    }

    [HttpPost("comprar")]
    [Authorize]
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
    [ProducesResponseType(typeof(ResultadoCompraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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
