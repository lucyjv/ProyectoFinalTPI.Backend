using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Repositorio.Data;

internal sealed class ConfiguracionCompraPuntosNostalgia : IEntityTypeConfiguration<CompraPuntosNostalgia>
{
    public void Configure(EntityTypeBuilder<CompraPuntosNostalgia> entidad)
    {
        entidad.ToTable("compras_puntos_nostalgia");
        entidad.HasKey(compra => compra.Id);
        entidad.Property(compra => compra.PackId).IsRequired().HasMaxLength(50);
        entidad.Property(compra => compra.Estado).IsRequired().HasMaxLength(30);
        entidad.Property(compra => compra.PreferenceId).HasMaxLength(150);
        entidad.Property(compra => compra.PaymentId).HasMaxLength(150);
        entidad.Property(compra => compra.PrecioUsd).HasColumnType("numeric(10,2)");
        entidad.Property(compra => compra.CotizacionDolar).HasColumnType("numeric(10,2)");
        entidad.Property(compra => compra.PrecioArs).HasColumnType("numeric(12,2)");

        entidad.HasOne<Usuario>().WithMany().HasForeignKey(compra => compra.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
