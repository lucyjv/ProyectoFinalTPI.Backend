using ProyectoFinalTPI.Backend.Entidades.Excepciones;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class ExcepcionesEntidadesTests
{
    [Fact]
    public void QueElConflictoDeCatalogoConserveElMotivoRecibido()
    {
        const string motivo = "El catálogo cambió desde la vista previa.";

        var conflicto = new ConflictoCatalogoException(motivo);

        Assert.Equal(motivo, conflicto.Message);
    }

    [Fact]
    public void QueElConflictoDeRelacionesIndiqueQueHayQueRecargarLosMomentos()
    {
        var conflicto = new ConflictoRelacionesException();

        Assert.Equal("Las asociaciones cambiaron. Recargá los momentos antes de guardar.", conflicto.Message);
    }
}
