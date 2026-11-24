using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Dtos.JuegoTesoro;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Configuracion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Servicio.Extensions;

namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class AdminJuegoTesoroServicio(IJuegoTesoroRepositorio repositorio, IJuegoTesoroServicio juego,
    IOptions<JuegoTesoroOpciones> opciones, TimeProvider reloj) : IAdminJuegoTesoroServicio
{
    public async Task<PanelTesoroAdminDto> ObtenerPanel(int adminId, CancellationToken ct = default)
    {
        await juego.ObtenerActual(adminId, ct);
        var semana = CalendarioTesoro.Semana(reloj);
        var ubicaciones = await repositorio.ListarUbicaciones(ct);
        var programaciones = await repositorio.ListarProgramaciones(semana, semana.AddDays(84), ct);
        var actual = await repositorio.ObtenerSemana(semana, ct);
        var semanas = Enumerable.Range(0, 13).Select(i => DescribirSemana(semana.AddDays(i * 7),
            i > 0, ubicaciones, programaciones, i == 0 ? actual : null)).ToList();
        var vigente = actual is null ? null : new RetoTesoroAdminDto(actual.Id, actual.NombreHallazgo,
            actual.Pista, actual.Latitud, actual.Longitud, actual.ZoomMinimo);
        return new(opciones.Value.Habilitado, semana, vigente, ubicaciones.Select(Mapear).ToList(), semanas);
    }

    public async Task<UbicacionTesoroDto> GuardarUbicacion(int adminId, int? id, GuardarUbicacionTesoroDto dto, CancellationToken ct = default)
    {
        var ubicacion = ValidarUbicacion(dto);
        if (id.HasValue)
        {
            id.Value.ValidarId("ubicación");
            if (!(await repositorio.ListarUbicaciones(ct)).Any(u => u.Id == id))
                throw new KeyNotFoundException("No existe esa ubicación.");
            ubicacion.Id = id.Value;
        }
        // Congela la semana vigente antes de cambiar el catálogo, incluso si
        // todavía nadie abrió el feed. Las semanas siguientes usan la edición.
        await juego.ObtenerActual(adminId, ct);
        return Mapear(await repositorio.GuardarUbicacion(ubicacion, ct));
    }

    public async Task Programar(DateOnly semana, int? ubicacionId, CancellationToken ct = default)
    {
        ValidarSemanaFutura(semana);
        if (ubicacionId.HasValue)
        {
            ubicacionId.Value.ValidarId("ubicación");
            var ubicacion = (await repositorio.ListarUbicaciones(ct)).SingleOrDefault(u => u.Id == ubicacionId);
            if (ubicacion is null) throw new KeyNotFoundException("No existe esa ubicación.");
            if (!ubicacion.Activa) throw new ArgumentException("Activá la ubicación antes de programarla.");
        }
        ValidarSemanaFutura(semana);
        await repositorio.GuardarProgramacion(new() { SemanaInicio = semana, UbicacionId = ubicacionId }, ct);
    }

    public async Task UsarRotacion(DateOnly semana, CancellationToken ct = default)
    {
        ValidarSemanaFutura(semana);
        await repositorio.EliminarProgramacion(semana, ct);
    }

    public async Task EditarActual(int adminId, int retoId, EditarRetoTesoroDto dto, CancellationToken ct = default)
    {
        retoId.ValidarId("reto");
        ArgumentNullException.ThrowIfNull(dto);
        var datos = ValidarUbicacion(new(dto.Nombre, dto.Pista, dto.Latitud, dto.Longitud, dto.ZoomMinimo, true));
        await juego.ObtenerActual(adminId, ct);
        var actual = await repositorio.ObtenerSemana(CalendarioTesoro.Semana(reloj), ct);
        if (actual is null || actual.Id != retoId)
            throw new ArgumentException("El reto cambió de semana. Actualizá el panel antes de editarlo.");
        actual.NombreHallazgo = datos.Nombre;
        actual.Pista = datos.Pista;
        actual.Latitud = datos.Latitud;
        actual.Longitud = datos.Longitud;
        actual.ZoomMinimo = datos.ZoomMinimo;
        await repositorio.ActualizarReto(actual, ct);
    }

    private void ValidarSemanaFutura(DateOnly semana)
    {
        var actual = CalendarioTesoro.Semana(reloj);
        if (semana.DayOfWeek != DayOfWeek.Sunday || semana <= actual || semana > actual.AddDays(84))
            throw new ArgumentException("Elegí un domingo de las próximas 12 semanas. La semana vigente y las anteriores están cerradas.");
    }

    private SemanaTesoroAdminDto DescribirSemana(DateOnly semana, bool editable,
        IReadOnlyList<UbicacionTesoro> ubicaciones, IReadOnlyList<ProgramacionTesoro> programaciones, RetoTesoro? actual)
    {
        if (actual is not null) return new(semana, false, "fijo", null, actual.NombreHallazgo, actual.Pista);
        var programacion = programaciones.SingleOrDefault(p => p.SemanaInicio == semana);
        var lugar = CalendarioTesoro.Elegir(semana, opciones.Value.SemanaBase, ubicaciones, programacion);
        var modo = programacion is null ? "automatica" : lugar is null ? "pausada" : "programada";
        return new(semana, editable, modo, programacion?.UbicacionId, lugar?.Nombre, lugar?.Pista);
    }

    private static UbicacionTesoro ValidarUbicacion(GuardarUbicacionTesoroDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var nombre = dto.Nombre.ValidarTexto(200, "nombre");
        var pista = dto.Pista.ValidarTexto(500, "pista");
        if (!double.IsFinite(dto.Latitud) || dto.Latitud is < -85 or > 85
            || !double.IsFinite(dto.Longitud) || dto.Longitud is < -180 or > 180
            || !double.IsFinite(dto.ZoomMinimo) || dto.ZoomMinimo is < 17 or > 19)
            throw new ArgumentException("Elegí un punto válido del mapa y un zoom entre 17 y 19.");
        return new() { Nombre = nombre, Pista = pista, Latitud = dto.Latitud, Longitud = dto.Longitud,
            ZoomMinimo = dto.ZoomMinimo, Activa = dto.Activa };
    }

    private static UbicacionTesoroDto Mapear(UbicacionTesoro u) =>
        new(u.Id, u.Nombre, u.Pista, u.Latitud, u.Longitud, u.ZoomMinimo, u.Activa);
}
