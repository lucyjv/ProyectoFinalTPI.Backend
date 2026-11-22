using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Entidades.ValueObjects;

namespace ProyectoFinalTPI.Backend.Repositorio.Data;

internal sealed class ConfiguracionRankingMusica : IEntityTypeConfiguration<RankingMusica>
{
    public void Configure(EntityTypeBuilder<RankingMusica> entidad)
    {
        entidad.ToTable("rankings_musica", tabla =>
        {
            tabla.HasCheckConstraint("CK_rankings_musica_fechas", """ "Apertura" < "Cierre" """);
            tabla.HasCheckConstraint("CK_rankings_musica_estado", """ "Estado" IN (0, 1) """);
        });
        entidad.HasIndex(ronda => ronda.Apertura).IsUnique();
        entidad.HasIndex(ronda => ronda.Estado).IsUnique().HasFilter(""" "Estado" = 0 """);
    }
}

internal sealed class ConfiguracionCandidaturaRankingMusica : IEntityTypeConfiguration<CandidaturaRankingMusica>
{
    public void Configure(EntityTypeBuilder<CandidaturaRankingMusica> entidad)
    {
        entidad.ToTable("candidaturas_ranking_musica", tabla =>
        {
            tabla.HasCheckConstraint("CK_candidaturas_musica_retiro", """ "FechaRetiro" IS NULL OR "FechaRetiro" >= "FechaIngreso" """);
            tabla.HasCheckConstraint("CK_candidaturas_musica_textos", """
                length(btrim("Titulo")) > 0 AND length(btrim("Artista")) > 0
                AND length(btrim("TituloNormalizado")) > 0 AND length(btrim("ArtistaNormalizado")) > 0
                """);
        });
        entidad.Property(candidatura => candidatura.Decada)
            .HasConversion(decada => decada.Valor, valor => new Decada(valor));
        entidad.Property(candidatura => candidatura.Titulo).HasMaxLength(CandidaturaRankingMusica.LongitudMaximaTexto);
        entidad.Property(candidatura => candidatura.Artista).HasMaxLength(CandidaturaRankingMusica.LongitudMaximaTexto);
        entidad.HasIndex(candidatura => new
        {
            candidatura.RankingMusicaId, candidatura.Decada,
            candidatura.TituloNormalizado, candidatura.ArtistaNormalizado
        }).IsUnique().HasFilter(""" "FechaRetiro" IS NULL """);
        entidad.HasOne<RankingMusica>().WithMany(ronda => ronda.Candidaturas)
            .HasForeignKey(candidatura => candidatura.RankingMusicaId).OnDelete(DeleteBehavior.Restrict);
        entidad.HasOne<Usuario>().WithMany().HasForeignKey(candidatura => candidatura.CreadorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ConfiguracionVotoRankingMusica : IEntityTypeConfiguration<VotoRankingMusica>
{
    public void Configure(EntityTypeBuilder<VotoRankingMusica> entidad)
    {
        entidad.ToTable("votos_ranking_musica", tabla =>
            tabla.HasCheckConstraint("CK_votos_musica_puntos", """ "Puntos" > 0 """));
        entidad.HasOne<CandidaturaRankingMusica>().WithMany().HasForeignKey(voto => voto.CandidaturaId)
            .OnDelete(DeleteBehavior.Restrict);
        entidad.HasOne<Usuario>().WithMany().HasForeignKey(voto => voto.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ConfiguracionResultadoRankingMusica : IEntityTypeConfiguration<ResultadoRankingMusica>
{
    public void Configure(EntityTypeBuilder<ResultadoRankingMusica> entidad)
    {
        entidad.ToTable("resultados_ranking_musica", tabla =>
        {
            tabla.HasCheckConstraint("CK_resultados_musica_posicion", """ "Posicion" BETWEEN 1 AND 6 """);
            tabla.HasCheckConstraint("CK_resultados_musica_puntos", """ "Puntos" > 0 """);
        });
        entidad.HasKey(resultado => resultado.CandidaturaId);
        entidad.Property(resultado => resultado.Decada)
            .HasConversion(decada => decada.Valor, valor => new Decada(valor));
        entidad.HasIndex(resultado => new { resultado.RankingMusicaId, resultado.Decada, resultado.Posicion })
            .IsUnique();
        entidad.HasOne<CandidaturaRankingMusica>().WithOne()
            .HasForeignKey<ResultadoRankingMusica>(resultado => new
            {
                resultado.RankingMusicaId, resultado.Decada, resultado.CandidaturaId
            })
            .HasPrincipalKey<CandidaturaRankingMusica>(candidatura => new
            {
                candidatura.RankingMusicaId, candidatura.Decada, candidatura.Id
            }).OnDelete(DeleteBehavior.Restrict);
    }
}
