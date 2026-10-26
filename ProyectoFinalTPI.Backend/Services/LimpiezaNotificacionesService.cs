using ProyectoFinalTPI.Backend.Interfaces.Repositorio;

namespace ProyectoFinalTPI.Backend.Services
{
    /// <summary>
    /// Servicio en segundo plano que elimina periódicamente las notificaciones expiradas.
    /// Una notificación expira a los 7 días de haber sido leída.
    /// Corre cada hora, igual que LimpiezaMensajesService.
    /// </summary>
    public class LimpiezaNotificacionesService : BackgroundService
    {
        private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LimpiezaNotificacionesService> _logger;

        public LimpiezaNotificacionesService(
            IServiceScopeFactory scopeFactory,
            ILogger<LimpiezaNotificacionesService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(Intervalo, stoppingToken);

                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var repositorio = scope.ServiceProvider
                        .GetRequiredService<INotificacionRepositorio>();

                    await repositorio.EliminarExpiradasAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Apagado limpio: no loguear como error.
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error al limpiar notificaciones expiradas.");
                }
            }
        }
    }
}
