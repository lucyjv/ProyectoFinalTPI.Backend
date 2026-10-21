using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Repositorio.Data;

internal class ConfiguracionMomento : IEntityTypeConfiguration<Momento>
{
    public void Configure(EntityTypeBuilder<Momento> entity)
    {
        entity.ToTable("momentos", tabla =>
        {
            tabla.HasCheckConstraint("CK_momentos_padre_distinto", "\"MomentoPadreId\" IS NULL OR \"MomentoPadreId\" <> \"Id\"");
            tabla.HasCheckConstraint("CK_momentos_fechas", "\"FechaInicio\" IS NULL OR \"FechaFin\" IS NULL OR \"FechaFin\" >= \"FechaInicio\"");
            tabla.HasCheckConstraint("CK_momentos_nombre", "length(btrim(\"Nombre\")) > 0");
            tabla.HasCheckConstraint("CK_momentos_canonico_distinto", "\"MomentoCanonicoId\" IS NULL OR \"MomentoCanonicoId\" <> \"Id\"");
            tabla.HasCheckConstraint("CK_momentos_identidad_activa", "\"MomentoCanonicoId\" IS NULL OR \"ClaveIdentidad\" IS NULL");
        });
        entity.Property(m => m.Nombre).IsRequired().HasMaxLength(200);
        entity.Property(m => m.Descripcion).IsRequired().HasMaxLength(2000);
        entity.HasIndex(m => m.Nombre);
        entity.Property(m => m.ClaveIdentidad).HasMaxLength(64);
        entity.HasIndex(m => m.ClaveIdentidad).IsUnique();
        entity.Property(m => m.VersionCatalogo).HasDefaultValue(1L).IsConcurrencyToken();
        entity.HasOne(m => m.MomentoCanonico).WithMany().HasForeignKey(m => m.MomentoCanonicoId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(m => m.MomentoPadre).WithMany(m => m.Hijos)
            .HasForeignKey(m => m.MomentoPadreId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class ConfiguracionMomentoAlias : IEntityTypeConfiguration<MomentoAlias>
{
    public void Configure(EntityTypeBuilder<MomentoAlias> entity)
    {
        entity.ToTable("momentos_alias", tabla =>
            tabla.HasCheckConstraint("CK_momentos_alias_texto", "length(btrim(\"Texto\")) > 0 AND length(btrim(\"TextoNormalizado\")) > 0"));
        entity.Property(a => a.Texto).IsRequired().HasMaxLength(200);
        entity.Property(a => a.TextoNormalizado).IsRequired().HasMaxLength(200);
        entity.HasIndex(a => new { a.MomentoId, a.TextoNormalizado }).IsUnique();
        // Un mismo alias puede nombrar acontecimientos diferentes: no es único globalmente.
        entity.HasIndex(a => a.TextoNormalizado);
        entity.HasOne(a => a.Momento).WithMany(m => m.Alias)
            .HasForeignKey(a => a.MomentoId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class ConfiguracionPublicacionMomento : IEntityTypeConfiguration<PublicacionMomento>
{
    public void Configure(EntityTypeBuilder<PublicacionMomento> entity)
    {
        entity.ToTable("publicaciones_momentos", tabla =>
        {
            tabla.HasCheckConstraint("CK_publicaciones_momentos_confianza", "\"Confianza\" IS NULL OR (\"Confianza\" >= 0 AND \"Confianza\" <= 1)");
            tabla.HasCheckConstraint("CK_publicaciones_momentos_origen", "\"Origen\" IN (0, 1)");
            tabla.HasCheckConstraint("CK_publicaciones_momentos_estado", "\"Estado\" IN (0, 1)");
        });
        entity.HasKey(a => new { a.PublicacionId, a.MomentoId });
        entity.Property(a => a.VersionAnalisis).HasMaxLength(100);
        entity.HasIndex(a => new { a.MomentoId, a.Estado, a.PublicacionId });
        entity.HasOne(a => a.Publicacion).WithMany(p => p.Momentos)
            .HasForeignKey(a => a.PublicacionId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(a => a.Momento).WithMany(m => m.Publicaciones)
            .HasForeignKey(a => a.MomentoId).OnDelete(DeleteBehavior.Restrict);
    }
}
