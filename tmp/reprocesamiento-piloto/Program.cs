using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Servicio;

var raiz = Directory.GetCurrentDirectory();
var configuracion = new ConfigurationBuilder().SetBasePath(Path.Combine(raiz, "ProyectoFinalTPI.Backend"))
    .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", optional: true)
    .AddUserSecrets("proyectofinaltpi-backend-6b725eee-1bde-4c02-a8cb-5f46828ddc59").AddEnvironmentVariables().Build();
var seleccion = JsonSerializer.Deserialize<SolicitudReprocesamientoDto>(
    await File.ReadAllTextAsync(Path.Combine(raiz, "output/reprocesamiento/primer-lote.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
if (seleccion.Recuerdos.Count != 5 || seleccion.Recuerdos.Any(r => r.PublicacionId < 49 || r.PublicacionId > 53 || r.VersionContenido != 1))
    throw new InvalidOperationException("El piloto solo admite los cinco recuerdos 49–53 en versión 1.");
var ids = seleccion.Recuerdos.Select(r => r.PublicacionId).ToArray();
var opcionesDb = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(configuracion.GetConnectionString("PostgresConnection"), o => o.UseNetTopologySuite()).Options;
using var limite = new CancellationTokenSource(TimeSpan.FromMinutes(12));
var ct = limite.Token;
await using (var db = new ApplicationDbContext(opcionesDb))
{
    var cola = await db.TrabajosAnalisisRecuerdos.AsNoTracking().GroupBy(j => j.Estado)
        .Select(g => new { estado = g.Key.ToString(), cantidad = g.Count() }).ToListAsync(ct);
    var otrosActivos = await db.TrabajosAnalisisRecuerdos.AsNoTracking().Where(j => !ids.Contains(j.PublicacionId) &&
        (j.Estado == EstadoTrabajoAnalisis.Pendiente || j.Estado == EstadoTrabajoAnalisis.Procesando))
        .Select(j => new { j.PublicacionId, j.VersionContenido, j.Estado }).ToListAsync(ct);
    var recuerdos = await db.Publicaciones.AsNoTracking().Where(p => ids.Contains(p.Id))
        .Select(p => new { p.Id, p.VersionContenido, p.EstaOculto, p.PublicacionOriginalId, p.PermitirMomentosAutomaticos }).ToListAsync(ct);
    Console.WriteLine(JsonSerializer.Serialize(new { fase = "inspeccion", cola, otrosActivos, recuerdos }));
    var momentos = await db.Momentos.AsNoTracking().Where(m => m.MomentoCanonicoId == null)
        .Select(m => new { m.Nombre, m.FechaInicio, m.FechaFin, m.ClaveIdentidad }).ToListAsync(ct);
    Console.WriteLine(JsonSerializer.Serialize(new { fase = "catalogo", activos = momentos.Count,
        fusionados = await db.Momentos.CountAsync(m => m.MomentoCanonicoId != null, ct),
        sinClave = momentos.Count(m => m.ClaveIdentidad == null),
        gruposIdenticos = momentos.GroupBy(m => IdentidadMomento.Clave(m.Nombre, m.FechaInicio, m.FechaFin)).Count(g => g.Count() > 1) }));
}
if (!args.Contains("--ejecutar")) return;

var gemini = configuracion.GetSection("Gemini").Get<OpcionesGemini>() ?? new();
gemini.Habilitado = true;
Validator.ValidateObject(gemini, new ValidationContext(gemini), validateAllProperties: true);
if (string.IsNullOrWhiteSpace(gemini.ApiKey)) throw new InvalidOperationException("Falta Gemini:ApiKey.");
var opcionesAnalisis = configuracion.GetSection("AnalisisRecuerdos").Get<OpcionesAnalisisRecuerdos>() ?? new();
opcionesAnalisis.Habilitado = true;
Validator.ValidateObject(opcionesAnalisis, new ValidationContext(opcionesAnalisis), validateAllProperties: true);
if (opcionesAnalisis.ReservaSegundos <= opcionesAnalisis.TiempoMaximoSegundos + 5)
    throw new InvalidOperationException("La reserva no supera el tiempo máximo de análisis.");
await using (var db = new ApplicationDbContext(opcionesDb))
{
    var lote = await new ReprocesamientoRecuerdosRepositorio(db).Encolar(seleccion, DateTime.UtcNow, ct);
    Console.WriteLine(JsonSerializer.Serialize(new { fase = "encolado", lote }));
}
using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
using var cuota = new ControlCuotaGemini(TimeProvider.System);
while (!ct.IsCancellationRequested)
{
    await using var db = new ApplicationDbContext(opcionesDb);
    var estados = await db.TrabajosAnalisisRecuerdos.AsNoTracking().Where(j => ids.Contains(j.PublicacionId) && j.VersionContenido == 1)
        .Select(j => new { j.PublicacionId, estado = j.Estado, j.Intentos, j.CodigoError, j.ProximoIntento }).ToListAsync(ct);
    if (!estados.Any(j => j.estado is EstadoTrabajoAnalisis.Pendiente or EstadoTrabajoAnalisis.Procesando)) break;
    var repositorio = new RepositorioLote(db, ids);
    var analizador = new AnalizadorRecuerdoGemini(http, new CatalogoAnalisisRepositorio(db), Options.Create(gemini), cuota);
    var servicio = new AnalisisRecuerdosServicio(repositorio, analizador, new RelacionRecuerdosRepositorio(db),
        Options.Create(opcionesAnalisis), TimeProvider.System, NullLogger<AnalisisRecuerdosServicio>.Instance);
    if (await servicio.ProcesarUno(ct))
    {
        Console.WriteLine(JsonSerializer.Serialize(new { fase = "progreso", publicacionId = repositorio.UltimaPublicacionId,
            estado = await repositorio.ObtenerEstado(repositorio.UltimaPublicacionId!.Value, ct) }));
    }
    else await Task.Delay(TimeSpan.FromSeconds(2), ct);
}
await using (var db = new ApplicationDbContext(opcionesDb))
{
    var trabajos = await db.TrabajosAnalisisRecuerdos.AsNoTracking().Where(j => ids.Contains(j.PublicacionId) && j.VersionContenido == 1)
        .OrderBy(j => j.PublicacionId).Select(j => new { j.PublicacionId, j.VersionContenido, estado = j.Estado.ToString(), j.Intentos, j.CodigoError, j.VersionAnalizador }).ToListAsync(ct);
    var asociaciones = await db.PublicacionesMomentos.AsNoTracking().Where(a => ids.Contains(a.PublicacionId))
        .OrderBy(a => a.PublicacionId).Select(a => new { a.PublicacionId, a.MomentoId, a.Momento.Nombre,
            origen = a.Origen.ToString(), estado = a.Estado.ToString(), a.Confianza }).ToListAsync(ct);
    var informe = JsonSerializer.Serialize(new { ejecutadoUtc = DateTime.UtcNow, modelo = gemini.Modelo, trabajos, asociaciones },
        new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(Path.Combine(raiz, "output/reprocesamiento/resultado-primer-lote.json"), informe, ct);
    Console.WriteLine(informe);
    if (trabajos.Count != 5 || trabajos.Any(j => j.estado is not ("Completado" or "SinMomento"))) Environment.ExitCode = 1;
}

// Adaptador operativo para este piloto: la reserva solo puede tomar la selección autorizada.
// El análisis, la validación, la confirmación y los reintentos usan los servicios/repositorios existentes.
sealed class RepositorioLote(ApplicationDbContext db, int[] ids) : ITrabajoAnalisisRepositorio
{
    private readonly TrabajoAnalisisRepositorio _original = new(db);
    public int? UltimaPublicacionId { get; private set; }
    public async Task<ReservaAnalisisDto?> Reservar(DateTime ahora, TimeSpan duracion, int maxIntentos, CancellationToken ct)
    {
        var token = Guid.NewGuid();
        var hasta = ahora.Add(duracion);
        var reservas = await db.Database.SqlQuery<ReservaAnalisisDto>($"""
            WITH candidato AS (
                SELECT "Id" FROM trabajos_analisis_recuerdos
                WHERE "PublicacionId" = ANY({ids}) AND "VersionContenido" = 1 AND "Intentos" < {maxIntentos}
                  AND (("Estado" = 0 AND "ProximoIntento" <= {ahora}) OR ("Estado" = 1 AND "ReservadoHasta" <= {ahora}))
                ORDER BY "FechaCreacion", "Id" FOR UPDATE SKIP LOCKED LIMIT 1
            )
            UPDATE trabajos_analisis_recuerdos j SET "Estado" = 1, "Intentos" = j."Intentos" + 1,
                "TokenReserva" = {token}, "ReservadoHasta" = {hasta}, "FechaActualizacion" = {ahora}
            FROM candidato c WHERE j."Id" = c."Id"
            RETURNING j."Id", j."PublicacionId", j."VersionContenido", j."Intentos", j."TokenReserva", j."ReservadoHasta"
            """).ToListAsync(ct);
        var reserva = reservas.SingleOrDefault();
        UltimaPublicacionId = reserva?.PublicacionId;
        return reserva;
    }
    public Task<EntradaAnalisisRecuerdoDto?> ObtenerEntrada(ReservaAnalisisDto r, DateTime ahora, CancellationToken ct) => _original.ObtenerEntrada(r, ahora, ct);
    public Task<bool> Completar(ReservaAnalisisDto r, ResultadoAnalisisRecuerdoDto resultado, string version, DateTime ahora, CancellationToken ct) => _original.Completar(r, resultado, version, ahora, ct);
    public Task Descartar(ReservaAnalisisDto r, DateTime ahora, CancellationToken ct) => _original.Descartar(r, ahora, ct);
    public Task RegistrarFallo(ReservaAnalisisDto r, string codigo, DateTime ahora, DateTime siguiente, int max, CancellationToken ct) => _original.RegistrarFallo(r, codigo, ahora, siguiente, max, ct);
    public Task<EstadoAnalisisRecuerdoDto?> ObtenerEstado(int id, CancellationToken ct) => _original.ObtenerEstado(id, ct);
}
