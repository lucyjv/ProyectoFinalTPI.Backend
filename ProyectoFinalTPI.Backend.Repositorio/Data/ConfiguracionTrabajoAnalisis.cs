using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Repositorio.Data;

internal class ConfiguracionTrabajoAnalisis : IEntityTypeConfiguration<TrabajoAnalisisRecuerdo>
{
    public void Configure(EntityTypeBuilder<TrabajoAnalisisRecuerdo> entity)
    {
        entity.ToTable("trabajos_analisis_recuerdos", t =>
        {
            t.HasCheckConstraint("CK_trabajos_analisis_intentos", "\"Intentos\" >= 0 AND \"VersionContenido\" > 0");
            t.HasCheckConstraint("CK_trabajos_analisis_estado", "\"Estado\" BETWEEN 0 AND 6");
            t.HasCheckConstraint("CK_trabajos_analisis_reserva", "(\"Estado\" = 1 AND \"TokenReserva\" IS NOT NULL AND \"ReservadoHasta\" IS NOT NULL) OR (\"Estado\" <> 1 AND \"TokenReserva\" IS NULL AND \"ReservadoHasta\" IS NULL)");
        });
        entity.Property(t => t.CodigoError).HasMaxLength(50);
        entity.Property(t => t.VersionAnalizador).HasMaxLength(100);
        entity.HasIndex(t => new { t.PublicacionId, t.VersionContenido }).IsUnique();
        entity.HasIndex(t => new { t.Estado, t.ProximoIntento });
        entity.HasIndex(t => new { t.Estado, t.ReservadoHasta });
        entity.HasOne(t => t.Publicacion).WithMany(p => p.TrabajosAnalisis)
            .HasForeignKey(t => t.PublicacionId).OnDelete(DeleteBehavior.Cascade);
    }
}
