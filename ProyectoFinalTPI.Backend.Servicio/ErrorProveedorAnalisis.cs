namespace ProyectoFinalTPI.Backend.Servicio;

public class ErrorProveedorAnalisis(string codigo, bool permanente = false, TimeSpan? reintentarEn = null)
    : Exception("El proveedor de análisis no pudo completar la solicitud.")
{
    public string Codigo { get; } = codigo;
    public bool Permanente { get; } = permanente;
    public TimeSpan? ReintentarEn { get; } = reintentarEn;
}

// Una sola instancia por host, compartida entre scopes y solicitudes.
public sealed class ControlCuotaGemini(TimeProvider reloj) : IDisposable
{
    private readonly SemaphoreSlim _turno = new(1);
    private DateTimeOffset _siguiente;

    public async Task Esperar(int intervaloSegundos, CancellationToken ct)
    {
        await _turno.WaitAsync(ct);
        try
        {
            var espera = _siguiente - reloj.GetUtcNow();
            if (espera > TimeSpan.Zero) await Task.Delay(espera, reloj, ct);
            _siguiente = reloj.GetUtcNow().AddSeconds(intervaloSegundos);
        }
        finally { _turno.Release(); }
    }

    public void Dispose() => _turno.Dispose();
}
