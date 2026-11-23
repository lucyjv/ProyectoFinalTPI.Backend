using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Dtos.JuegoTesoro;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Configuracion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio.Extensions;

namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class JuegoTesoroServicio(IJuegoTesoroRepositorio repositorio, IUsuarioRepositorio usuarios,
    IOptions<JuegoTesoroOpciones> opciones, TimeProvider reloj) : IJuegoTesoroServicio
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    public async Task<RetoTesoroDto?> ObtenerActual(int usuarioId, CancellationToken ct = default)
    {
        await ExigirUsuario(usuarioId);
        var reto = await ObtenerRetoActual(ct);
        if (reto is null) return null;
        var hallazgo = await repositorio.ObtenerHallazgo(reto.Id, usuarioId, ct);
        return new(reto.Id, reto.Pista, InicioUtc(reto.SemanaInicio), InicioUtc(reto.SemanaInicio.AddDays(7)),
            reto.ZoomMinimo, hallazgo?.FechaHallazgoUtc);
    }

    public async Task<TesoroVisibleDto?> ObtenerVisible(int usuarioId, VistaMapaTesoroDto vista, CancellationToken ct = default)
    {
        ValidarVista(vista);
        await ExigirUsuario(usuarioId);
        var reto = await ObtenerRetoActual(ct);
        if (reto is null || !EstaEnVista(reto, vista)) return null;
        if (await repositorio.ObtenerHallazgo(reto.Id, usuarioId, ct) is not null) return null;
        return new(reto.Id, reto.Latitud, reto.Longitud);
    }

    public async Task<HallazgoTesoroDto> Recoger(int usuarioId, int retoId, VistaMapaTesoroDto vista, CancellationToken ct = default)
    {
        retoId.ValidarId("reto");
        ValidarVista(vista);
        await ExigirUsuario(usuarioId);
        var reto = await ObtenerRetoActual(ct);
        if (reto is null || reto.Id != retoId) throw new ArgumentException("El reto ya no está vigente. Volvé a consultar la pista.");
        if (!EstaEnVista(reto, vista)) throw new ArgumentException("Acercá el mapa al lugar del tesoro para recogerlo.");
        var hallazgo = await repositorio.GuardarHallazgo(new HallazgoTesoro
        { RetoId = reto.Id, UsuarioId = usuarioId, FechaHallazgoUtc = reloj.GetUtcNow().UtcDateTime }, ct);
        return new(reto.Id, reto.NombreHallazgo, hallazgo.FechaHallazgoUtc);
    }

    private async Task<RetoTesoro?> ObtenerRetoActual(CancellationToken ct)
    {
        var config = opciones.Value;
        if (!config.Habilitado || config.Ubicaciones.Count == 0) return null;
        var fecha = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), Zona).DateTime);
        var semana = fecha.AddDays(-(int)fecha.DayOfWeek);
        var semanas = (int)Math.Floor((semana.DayNumber - config.SemanaBase.DayNumber) / 7d);
        var indice = ((semanas % config.Ubicaciones.Count) + config.Ubicaciones.Count) % config.Ubicaciones.Count;
        var lugar = config.Ubicaciones[indice];
        ValidarUbicacion(lugar);
        return await repositorio.ObtenerOCrearSemana(new RetoTesoro
        {
            SemanaInicio = semana, ClaveUbicacion = lugar.Clave, Pista = lugar.Pista,
            NombreHallazgo = lugar.NombreHallazgo, Latitud = lugar.Latitud, Longitud = lugar.Longitud, ZoomMinimo = lugar.ZoomMinimo
        }, ct);
    }

    private async Task ExigirUsuario(int usuarioId)
    {
        usuarioId.ValidarId("usuario");
        var usuario = await usuarios.ObtenerPorIdAsync(usuarioId);
        if (usuario is null || usuario.EstaEliminado || usuario is Usuario_Interactivo { SuspendidoHasta: { } hasta }
            && hasta > reloj.GetUtcNow().UtcDateTime)
            throw new UnauthorizedAccessException("La cuenta no está habilitada para participar.");
    }

    private static DateTime InicioUtc(DateOnly fecha) => TimeZoneInfo.ConvertTimeToUtc(fecha.ToDateTime(TimeOnly.MinValue), Zona);

    private static bool EstaEnVista(RetoTesoro reto, VistaMapaTesoroDto vista) =>
        vista.Zoom >= reto.ZoomMinimo && reto.Latitud >= vista.MinLatitud && reto.Latitud <= vista.MaxLatitud
        && reto.Longitud >= vista.MinLongitud && reto.Longitud <= vista.MaxLongitud;

    private static void ValidarVista(VistaMapaTesoroDto vista)
    {
        ArgumentNullException.ThrowIfNull(vista);
        if (!double.IsFinite(vista.Zoom) || vista.Zoom is < 0 or > 22
            || !double.IsFinite(vista.MinLatitud) || !double.IsFinite(vista.MaxLatitud)
            || !double.IsFinite(vista.MinLongitud) || !double.IsFinite(vista.MaxLongitud)
            || vista.MinLatitud < -90 || vista.MaxLatitud > 90 || vista.MinLatitud >= vista.MaxLatitud
            || vista.MinLongitud < -180 || vista.MaxLongitud > 180 || vista.MinLongitud >= vista.MaxLongitud)
            throw new ArgumentException("La vista del mapa no es válida.");
    }

    private static void ValidarUbicacion(UbicacionTesoroOpciones lugar)
    {
        if (string.IsNullOrWhiteSpace(lugar.Clave) || lugar.Clave.Length > 100
            || string.IsNullOrWhiteSpace(lugar.Pista) || lugar.Pista.Length > 500
            || string.IsNullOrWhiteSpace(lugar.NombreHallazgo) || lugar.NombreHallazgo.Length > 200
            || !double.IsFinite(lugar.Latitud) || lugar.Latitud is < -90 or > 90
            || !double.IsFinite(lugar.Longitud) || lugar.Longitud is < -180 or > 180
            || !double.IsFinite(lugar.ZoomMinimo) || lugar.ZoomMinimo is < 17 or > 19)
            throw new InvalidOperationException("Revisá las ubicaciones configuradas en JuegoTesoro.");
    }
}
