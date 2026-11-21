using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Repositorio.Data;

internal sealed class ConfiguracionPuntuacionPublicacionPersistida : IEntityTypeConfiguration<PuntuacionPublicacionPersistida>
{
    public void Configure(EntityTypeBuilder<PuntuacionPublicacionPersistida> entidad)
    {
        entidad.ToTable("puntuaciones_publicacion", tabla =>
            tabla.HasCheckConstraint("CK_puntuaciones_publicacion_puntaje", """ "Puntaje" BETWEEN 1 AND 5 """));
        entidad.HasIndex(puntuacion => new { puntuacion.UsuarioId, puntuacion.PublicacionId }).IsUnique();
        entidad.HasIndex(puntuacion => puntuacion.PublicacionId);
        entidad.HasIndex(puntuacion => puntuacion.PendienteDeProyectar).HasFilter(""" "PendienteDeProyectar" """);
        entidad.HasOne<Usuario>().WithMany().HasForeignKey(puntuacion => puntuacion.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
        entidad.HasOne<Publicacion>().WithMany().HasForeignKey(puntuacion => puntuacion.PublicacionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
