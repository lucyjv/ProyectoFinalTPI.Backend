using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Repositorio.Data;

internal sealed class ConfiguracionConsumoDiarioPuntosNostalgia : IEntityTypeConfiguration<ConsumoDiarioPuntosNostalgia>
{
    public void Configure(EntityTypeBuilder<ConsumoDiarioPuntosNostalgia> entidad)
    {
        entidad.ToTable("consumos_diarios_puntos_nostalgia", tabla =>
            tabla.HasCheckConstraint("CK_consumos_diarios_puntos_gastados", """ "PuntosGastados" >= 0 """));
        entidad.HasKey(consumo => new { consumo.UsuarioId, consumo.Fecha });
        entidad.HasOne<Usuario>().WithMany().HasForeignKey(consumo => consumo.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
