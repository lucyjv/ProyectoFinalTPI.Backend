using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Interfaces.Configuracion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class CorreosRecuperacionWorker(IServiceScopeFactory scopes,
    IOptions<RecuperacionContrasenaOptions> opciones, ILogger<CorreosRecuperacionWorker> logger)
    : BackgroundService, IColaRecuperacionContrasena
{
    private static readonly Meter Medidor = new("Nostalgiar.Auth");
    private static readonly Counter<long> Correos = Medidor.CreateCounter<long>("auth.recuperacion.correos");
    private readonly Channel<Pendiente> cola = Channel.CreateBounded<Pendiente>(new BoundedChannelOptions(128)
    {
        FullMode = BoundedChannelFullMode.Wait, SingleReader = true
    });
    private readonly HashSet<string> pendientes = new(StringComparer.Ordinal);
    private readonly object cerrojo = new();

    public bool EncolarSolicitud(string email) => Encolar(new(email, null));
    public bool EncolarAviso(string email, Guid recuperacionId) => Encolar(new(email, recuperacionId));

    private bool Encolar(Pendiente mensaje)
    {
        lock (cerrojo)
        {
            if (!pendientes.Add(mensaje.Clave)) return true;
            if (cola.Writer.TryWrite(mensaje)) return true;
            pendientes.Remove(mensaje.Clave);
        }
        logger.LogWarning("La cola de correos de recuperación está llena. Tipo: {Tipo}.", mensaje.Tipo);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opciones.Value.Habilitado) return;
        try
        {
            await foreach (var pendiente in cola.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await Procesar(pendiente, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    var codigo = ex is EnvioCorreoException envio ? envio.Codigo
                        : ex is OperationCanceledException ? "tiempo_agotado" : "procesamiento";
                    logger.LogError("Falló el correo de recuperación. Tipo: {Tipo}. Código: {Codigo}.", pendiente.Tipo, codigo);
                    Registrar(pendiente.Tipo, "error");
                }
                finally
                {
                    lock (cerrojo) pendientes.Remove(pendiente.Clave);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task Procesar(Pendiente pendiente, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var correo = pendiente.RecuperacionId is { } id
            ? CorreosRecuperacion.CrearAviso(pendiente.Email, id)
            : await scope.ServiceProvider.GetRequiredService<IProcesadorRecuperacionContrasena>()
                .PrepararRecuperacionAsync(pendiente.Email, cancellationToken);
        if (correo is null)
        {
            Registrar(pendiente.Tipo, "omitido");
            return;
        }
        await scope.ServiceProvider.GetRequiredService<IEnvioCorreo>().EnviarAsync(correo, cancellationToken);
        Registrar(pendiente.Tipo, "enviado");
    }

    private static void Registrar(string tipo, string resultado) => Correos.Add(1,
        new KeyValuePair<string, object?>("tipo", tipo), new KeyValuePair<string, object?>("resultado", resultado));

    private sealed record Pendiente(string Email, Guid? RecuperacionId)
    {
        public string Tipo => RecuperacionId is null ? "solicitud" : "aviso";
        public string Clave => RecuperacionId is null ? $"solicitud:{Email}" : $"aviso:{RecuperacionId:N}";
    }
}
