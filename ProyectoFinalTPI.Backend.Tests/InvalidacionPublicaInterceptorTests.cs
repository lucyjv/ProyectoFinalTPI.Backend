using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;
using ProyectoFinalTPI.Backend.Repositorio.Cache;
namespace ProyectoFinalTPI.Backend.Tests;
public class InvalidacionPublicaInterceptorTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task QueSeInvalideSoloDespuesDeGuardarCambiosPublicosSinRepetirLaInvalidacion(bool asincrono, bool hayCambios)
    {
        //Arrange
        var contexto = new Mock<DbContext>().Object;
        var detector = new Mock<IDetectorCambiosPublicos>(MockBehavior.Strict);
        detector.Setup(d => d.HayCambios(contexto)).Returns(hayCambios);
        var cache = new Mock<IInvalidadorLecturasPublicas>(MockBehavior.Strict);
        if (hayCambios) { if (asincrono) cache.Setup(c => c.InvalidarAsync(CancellationToken.None)).Returns(Task.CompletedTask); else cache.Setup(c => c.Invalidar()); }
        var interceptor = new InvalidacionPublicaInterceptor(cache.Object, detector.Object);
        var inicio = new DbContextEventData(null!, (_, _) => "", contexto);
        var fin = new SaveChangesCompletedEventData(null!, (_, _) => "", contexto, 3);
        //Act
        if (asincrono) await interceptor.SavingChangesAsync(inicio, default); else interceptor.SavingChanges(inicio, default);
        cache.VerifyNoOtherCalls();
        var result = asincrono ? await interceptor.SavedChangesAsync(fin, 3) : interceptor.SavedChanges(fin, 3);
        if (asincrono) await interceptor.SavedChangesAsync(fin, 0); else interceptor.SavedChanges(fin, 0);
        //Assert
        Assert.Equal(3, result);
        detector.Verify(d => d.HayCambios(contexto), Times.Once);
        cache.Verify(c => c.Invalidar(), hayCambios && !asincrono ? Times.Once() : Times.Never());
        cache.Verify(c => c.InvalidarAsync(It.IsAny<CancellationToken>()), hayCambios && asincrono ? Times.Once() : Times.Never());
    }
}
