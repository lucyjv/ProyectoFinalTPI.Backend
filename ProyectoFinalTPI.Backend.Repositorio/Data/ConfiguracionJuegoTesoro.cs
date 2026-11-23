using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Repositorio.Data;

public sealed class ConfiguracionRetoTesoro : IEntityTypeConfiguration<RetoTesoro>
{
    public void Configure(EntityTypeBuilder<RetoTesoro> e)
    {
        e.ToTable("retos_tesoro");
        e.HasKey(r => r.Id);
        e.HasIndex(r => r.SemanaInicio).IsUnique();
        e.Property(r => r.ClaveUbicacion).HasMaxLength(100).IsRequired();
        e.Property(r => r.Pista).HasMaxLength(500).IsRequired();
        e.Property(r => r.NombreHallazgo).HasMaxLength(200).IsRequired();
    }
}

public sealed class ConfiguracionHallazgoTesoro : IEntityTypeConfiguration<HallazgoTesoro>
{
    public void Configure(EntityTypeBuilder<HallazgoTesoro> e)
    {
        e.ToTable("hallazgos_tesoro");
        e.HasKey(h => new { h.RetoId, h.UsuarioId });
        e.HasOne<RetoTesoro>().WithMany().HasForeignKey(h => h.RetoId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<Usuario>().WithMany().HasForeignKey(h => h.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}
