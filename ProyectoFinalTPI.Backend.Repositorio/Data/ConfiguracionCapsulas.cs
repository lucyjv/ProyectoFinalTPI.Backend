using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Repositorio.Data;

public sealed class ConfiguracionCapsula : IEntityTypeConfiguration<Capsula>
{
    public void Configure(EntityTypeBuilder<Capsula> e)
    {
        e.ToTable("capsulas", t =>
        {
            t.HasCheckConstraint("CK_capsulas_Visibilidad", "\"Visibilidad\" IN (0, 1)");
            t.HasCheckConstraint("CK_capsulas_PortadaUnica", "\"PortadaRecursoId\" IS NULL OR (\"PortadaPublicacionId\" IS NULL AND \"PortadaAdjuntoId\" IS NULL)");
        });
        e.HasKey(c => c.Id);
        e.Property(c => c.Nombre).IsRequired().HasMaxLength(100);
        e.Property(c => c.Descripcion).HasMaxLength(500);
        e.Property(c => c.PortadaRecursoId).HasMaxLength(200);
        e.HasIndex(c => new { c.PropietarioId, c.Id });
        e.HasOne(c => c.Propietario).WithMany().HasForeignKey(c => c.PropietarioId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(c => c.PortadaPublicacion).WithMany().HasForeignKey(c => c.PortadaPublicacionId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(c => c.PortadaAdjunto).WithMany().HasForeignKey(c => c.PortadaAdjuntoId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class ConfiguracionCapsulaRecuerdo : IEntityTypeConfiguration<CapsulaRecuerdo>
{
    public void Configure(EntityTypeBuilder<CapsulaRecuerdo> e)
    {
        e.ToTable("capsulas_publicaciones");
        e.HasKey(r => new { r.CapsulaId, r.PublicacionId });
        e.HasIndex(r => r.PublicacionId);
        e.HasOne(r => r.Capsula).WithMany(c => c.Recuerdos).HasForeignKey(r => r.CapsulaId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(r => r.Publicacion).WithMany(p => p.Capsulas).HasForeignKey(r => r.PublicacionId).OnDelete(DeleteBehavior.Cascade);
    }
}
