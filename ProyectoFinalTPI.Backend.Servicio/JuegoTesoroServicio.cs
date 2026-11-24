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
    public async Task<RetoTesoroDto?> ObtenerActual(int usuarioId, CancellationToken ct = default)
    {
        await ExigirUsuario(usuarioId);
        var reto = await ObtenerRetoActual(ct);
        if (reto is null) return null;
        var hallazgo = await repositorio.ObtenerHallazgo(reto.Id, usuarioId, ct);
        return new(reto.Id, reto.Pista, CalendarioTesoro.InicioUtc(reto.SemanaInicio), CalendarioTesoro.InicioUtc(reto.SemanaInicio.AddDays(7)),
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
        if (!config.Habilitado) return null;
        var semana = CalendarioTesoro.Semana(reloj);
        var existente = await repositorio.ObtenerSemana(semana, ct);
        if (existente is not null) return existente;
        var ubicaciones = await repositorio.ListarUbicaciones(ct);
        var programacion = (await repositorio.ListarProgramaciones(semana, semana, ct)).SingleOrDefault();
        var lugar = CalendarioTesoro.Elegir(semana, config.SemanaBase, ubicaciones, programacion);
        if (lugar is null) return null;
        return await repositorio.ObtenerOCrearSemana(new RetoTesoro
        {
            SemanaInicio = semana, ClaveUbicacion = $"ubicacion:{lugar.Id}", Pista = lugar.Pista,
            NombreHallazgo = lugar.Nombre, Latitud = lugar.Latitud, Longitud = lugar.Longitud, ZoomMinimo = lugar.ZoomMinimo
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
}
