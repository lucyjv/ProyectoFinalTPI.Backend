using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Dtos.Momento;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Services;
using ProyectoFinalTPI.Backend.Servicio;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class AnalisisRecuerdosWorkerTests
{
    [Fact]
    public async Task QueCadaCicloCreeYLibereSuPropioScope()
    {
        //Arrange
        var instancias = new List<ProcesadorFake>();
        var servicios = new ServiceCollection();
        servicios.AddScoped<IAnalisisRecuerdosServicio>(_ => { var servicio = new ProcesadorFake(); instancias.Add(servicio); return servicio; });
        await using var provider = servicios.BuildServiceProvider();
        using var worker = new AnalisisRecuerdosWorker(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new OpcionesAnalisisRecuerdos()), TimeProvider.System, NullLogger<AnalisisRecuerdosWorker>.Instance);
        using var cancelacion = new CancellationTokenSource();

        //Act
        await worker.EjecutarCiclo(cancelacion.Token);
        await worker.EjecutarCiclo(cancelacion.Token);

        //Assert
        Assert.Equal(2, instancias.Count);
        Assert.NotSame(instancias[0], instancias[1]);
        Assert.All(instancias, s => { Assert.True(s.Liberado); Assert.Equal(cancelacion.Token, s.Token); });
    }

    [Fact]
    public async Task QueElWorkerDeshabilitadoNoConsumaLaCola()
    {
        //Arrange
        await using var provider = new ServiceCollection().BuildServiceProvider();
        using var worker = new AnalisisRecuerdosWorker(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new OpcionesAnalisisRecuerdos { Habilitado = false }), TimeProvider.System, NullLogger<AnalisisRecuerdosWorker>.Instance);

        //Act
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        //Assert
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    private sealed class ProcesadorFake : IAnalisisRecuerdosServicio, IDisposable
    {
        public bool Liberado;
        public CancellationToken Token;
        public Task<bool> ProcesarUno(CancellationToken ct) { Token = ct; return Task.FromResult(false); }
        public Task<EstadoAnalisisRecuerdoDto> ObtenerEstado(int id, CancellationToken ct) => throw new NotSupportedException();
        public void Dispose() => Liberado = true;
    }
}
