using ProyectoFinalTPI.Backend.Interfaces.Repositorio;

namespace ProyectoFinalTPI.Backend.Services
{
    /// <summary>
    /// Servicio en background que elimina periódicamente los mensajes de chat
    /// cuya fecha de expiración (7 días desde el envío) ya pasó.
    ///
    /// BackgroundService es una clase base de .NET que corre de forma
    /// independiente al ciclo de vida de las peticiones HTTP.
    /// .NET lo inicia automáticamente al levantar la app y lo detiene
    /// limpiamente cuando la app se cierra.
    /// </summary>
    public class LimpiezaMensajesService : BackgroundService
    {
        // IServiceScopeFactory es necesario porque IMensajeChatRepositorio
        // está registrado como Scoped (vive por petición), pero este servicio
        // es Singleton (vive toda la app). No se puede inyectar un Scoped
        // directamente en un Singleton: hay que crear un scope manualmente
        // cada vez que se necesita usar el repositorio.
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LimpiezaMensajesService> _logger;

        private static readonly TimeSpan IntervaloLimpieza = TimeSpan.FromHours(1);

        public LimpiezaMensajesService(
            IServiceScopeFactory scopeFactory,
            ILogger<LimpiezaMensajesService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// Loop principal del background service.
        /// Espera 1 hora, luego ejecuta la limpieza, y repite.
        /// El CancellationToken se activa cuando la app comienza a cerrarse.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Servicio de limpieza de mensajes iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(IntervaloLimpieza, stoppingToken);

                try
                {
                    await EjecutarLimpiezaAsync();
                }
                catch (Exception ex)
                {
                    // Loguear y continuar: un error en la limpieza no debe
                    // tumbar toda la aplicación.
                    _logger.LogError(ex, "Error durante la limpieza de mensajes expirados.");
                }
            }
        }

        private async Task EjecutarLimpiezaAsync()
        {
            // Crear un scope para poder resolver servicios Scoped (repositorio + DbContext).
            using var scope = _scopeFactory.CreateScope();
            var repositorio = scope.ServiceProvider
                .GetRequiredService<IMensajeChatRepositorio>();

            _logger.LogInformation("Ejecutando limpieza de mensajes expirados...");
            await repositorio.EliminarExpiradosAsync();
            _logger.LogInformation("Limpieza completada.");
        }
    }
}
