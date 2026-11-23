using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Repositorio.Data;

internal sealed class ConfiguracionBilleteraPuntosNostalgia : IEntityTypeConfiguration<BilleteraPuntosNostalgia>
{
    public void Configure(EntityTypeBuilder<BilleteraPuntosNostalgia> entidad)
    {
        entidad.ToTable("billeteras_puntos_nostalgia", tabla =>
            tabla.HasCheckConstraint("CK_billeteras_puntos_extra", """ "PuntosExtra" >= 0 """));
        entidad.HasKey(billetera => billetera.UsuarioId);
        entidad.Property(billetera => billetera.UsuarioId).ValueGeneratedNever();
        entidad.HasOne<Usuario>().WithMany().HasForeignKey(billetera => billetera.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
