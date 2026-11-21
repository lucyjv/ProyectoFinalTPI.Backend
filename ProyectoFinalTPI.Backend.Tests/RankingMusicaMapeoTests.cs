using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public sealed class RankingMusicaMapeoTests
{
    [Fact]
    public void QueElModeloPermitaUnaSolaRondaAbiertaYCandidaturasActivasSinDuplicados()
    {
        // Arrange
        using var contexto = CrearContexto();

        // Act
        var modelo = contexto.GetService<IDesignTimeModel>().Model;
        var rondas = modelo.FindEntityType(typeof(RankingMusica))!;
        var candidaturas = modelo.FindEntityType(typeof(CandidaturaRankingMusica))!;

        // Assert
        Assert.Equal("rankings_musica", rondas.GetTableName());
        var indiceAbierta = Assert.Single(rondas.GetIndexes(), indice =>
            indice.Properties.Select(propiedad => propiedad.Name).SequenceEqual(new[] { nameof(RankingMusica.Estado) }));
        Assert.True(indiceAbierta.IsUnique);
        Assert.Equal("\"Estado\" = 0", indiceAbierta.GetFilter()?.Trim());

        Assert.Equal("candidaturas_ranking_musica", candidaturas.GetTableName());
        var indiceCandidaturas = Assert.Single(candidaturas.GetIndexes(), indice => indice.IsUnique);
        Assert.Equal(new[] { "RankingMusicaId", "Decada", "TituloNormalizado", "ArtistaNormalizado" },
            indiceCandidaturas.Properties.Select(propiedad => propiedad.Name));
        Assert.Equal("\"FechaRetiro\" IS NULL", indiceCandidaturas.GetFilter()?.Trim());
        Assert.Equal(typeof(int), candidaturas.FindProperty(nameof(CandidaturaRankingMusica.Decada))!.GetValueConverter()?.ProviderClrType);
    }

    [Fact]
    public void QueElResultadoIdentifiqueUnaCandidaturaYUnPuestoUnicoPorDecadaYRonda()
    {
        // Arrange
        using var contexto = CrearContexto();

        // Act
        var resultado = contexto.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ResultadoRankingMusica))!;

        // Assert
        Assert.Equal("resultados_ranking_musica", resultado.GetTableName());
        Assert.Equal("CandidaturaId", Assert.Single(resultado.FindPrimaryKey()!.Properties).Name);
        var candidatura = Assert.Single(resultado.GetForeignKeys());
        Assert.Equal(typeof(CandidaturaRankingMusica), candidatura.PrincipalEntityType.ClrType);
        Assert.Equal(new[] { "RankingMusicaId", "Decada", "CandidaturaId" },
            candidatura.Properties.Select(propiedad => propiedad.Name));
        Assert.Contains(resultado.GetIndexes(), indice => indice.IsUnique &&
            indice.Properties.Select(propiedad => propiedad.Name).SequenceEqual(new[] { "RankingMusicaId", "Decada", "Posicion" }));
    }

    [Fact]
    public void QueElConsumoTengaUnaFilaPorUsuarioYFechaYElVotoRequieraUnaCandidatura()
    {
        // Arrange
        using var contexto = CrearContexto();

        // Act
        var modelo = contexto.GetService<IDesignTimeModel>().Model;
        var consumo = modelo.FindEntityType(typeof(ConsumoDiarioPuntosNostalgia))!;
        var voto = modelo.FindEntityType(typeof(VotoRankingMusica))!;

        // Assert
        Assert.Equal("consumos_diarios_puntos_nostalgia", consumo.GetTableName());
        Assert.Equal(new[] { "UsuarioId", "Fecha" }, consumo.FindPrimaryKey()!.Properties.Select(propiedad => propiedad.Name));
        Assert.Equal(typeof(Usuario), Assert.Single(consumo.GetForeignKeys()).PrincipalEntityType.ClrType);

        Assert.Equal("votos_ranking_musica", voto.GetTableName());
        var candidatura = Assert.Single(voto.GetForeignKeys(), clave => clave.PrincipalEntityType.ClrType == typeof(CandidaturaRankingMusica));
        Assert.True(candidatura.IsRequired);
        Assert.Contains(voto.GetCheckConstraints(), restriccion => restriccion.Sql.Trim() == "\"Puntos\" > 0");
    }

    private static ApplicationDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=modelo_ranking", opciones => opciones.UseNetTopologySuite())
            .Options);
}
