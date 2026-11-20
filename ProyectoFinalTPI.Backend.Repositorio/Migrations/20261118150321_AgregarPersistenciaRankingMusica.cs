using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPersistenciaRankingMusica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consumos_diarios_puntos_nostalgia",
                columns: table => new
                {
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    PuntosGastados = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consumos_diarios_puntos_nostalgia", x => new { x.UsuarioId, x.Fecha });
                    table.CheckConstraint("CK_consumos_diarios_puntos_gastados", " \"PuntosGastados\" >= 0 ");
                    table.ForeignKey(
                        name: "FK_consumos_diarios_puntos_nostalgia_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rankings_musica",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Apertura = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Cierre = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rankings_musica", x => x.Id);
                    table.CheckConstraint("CK_rankings_musica_estado", " \"Estado\" IN (0, 1) ");
                    table.CheckConstraint("CK_rankings_musica_fechas", " \"Apertura\" < \"Cierre\" ");
                });

            migrationBuilder.CreateTable(
                name: "candidaturas_ranking_musica",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RankingMusicaId = table.Column<int>(type: "integer", nullable: false),
                    CreadorId = table.Column<int>(type: "integer", nullable: false),
                    Decada = table.Column<int>(type: "integer", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Artista = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TituloNormalizado = table.Column<string>(type: "text", nullable: false),
                    ArtistaNormalizado = table.Column<string>(type: "text", nullable: false),
                    FechaIngreso = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FechaRetiro = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidaturas_ranking_musica", x => x.Id);
                    table.UniqueConstraint("AK_candidaturas_ranking_musica_RankingMusicaId_Decada_Id", x => new { x.RankingMusicaId, x.Decada, x.Id });
                    table.CheckConstraint("CK_candidaturas_musica_retiro", " \"FechaRetiro\" IS NULL OR \"FechaRetiro\" >= \"FechaIngreso\" ");
                    table.CheckConstraint("CK_candidaturas_musica_textos", "length(btrim(\"Titulo\")) > 0 AND length(btrim(\"Artista\")) > 0\nAND length(btrim(\"TituloNormalizado\")) > 0 AND length(btrim(\"ArtistaNormalizado\")) > 0");
                    table.ForeignKey(
                        name: "FK_candidaturas_ranking_musica_Usuario_CreadorId",
                        column: x => x.CreadorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_candidaturas_ranking_musica_rankings_musica_RankingMusicaId",
                        column: x => x.RankingMusicaId,
                        principalTable: "rankings_musica",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resultados_ranking_musica",
                columns: table => new
                {
                    CandidaturaId = table.Column<int>(type: "integer", nullable: false),
                    RankingMusicaId = table.Column<int>(type: "integer", nullable: false),
                    Decada = table.Column<int>(type: "integer", nullable: false),
                    Posicion = table.Column<int>(type: "integer", nullable: false),
                    Puntos = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resultados_ranking_musica", x => x.CandidaturaId);
                    table.CheckConstraint("CK_resultados_musica_posicion", " \"Posicion\" BETWEEN 1 AND 6 ");
                    table.CheckConstraint("CK_resultados_musica_puntos", " \"Puntos\" > 0 ");
                    table.ForeignKey(
                        name: "FK_resultados_ranking_musica_candidaturas_ranking_musica_Ranki~",
                        columns: x => new { x.RankingMusicaId, x.Decada, x.CandidaturaId },
                        principalTable: "candidaturas_ranking_musica",
                        principalColumns: new[] { "RankingMusicaId", "Decada", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "votos_ranking_musica",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CandidaturaId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Puntos = table.Column<int>(type: "integer", nullable: false),
                    FechaVoto = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_votos_ranking_musica", x => x.Id);
                    table.CheckConstraint("CK_votos_musica_puntos", " \"Puntos\" > 0 ");
                    table.ForeignKey(
                        name: "FK_votos_ranking_musica_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_votos_ranking_musica_candidaturas_ranking_musica_Candidatur~",
                        column: x => x.CandidaturaId,
                        principalTable: "candidaturas_ranking_musica",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_candidaturas_ranking_musica_CreadorId",
                table: "candidaturas_ranking_musica",
                column: "CreadorId");

            migrationBuilder.CreateIndex(
                name: "IX_candidaturas_ranking_musica_RankingMusicaId_Decada_TituloNo~",
                table: "candidaturas_ranking_musica",
                columns: new[] { "RankingMusicaId", "Decada", "TituloNormalizado", "ArtistaNormalizado" },
                unique: true,
                filter: " \"FechaRetiro\" IS NULL ");

            migrationBuilder.CreateIndex(
                name: "IX_rankings_musica_Apertura",
                table: "rankings_musica",
                column: "Apertura",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rankings_musica_Estado",
                table: "rankings_musica",
                column: "Estado",
                unique: true,
                filter: " \"Estado\" = 0 ");

            migrationBuilder.CreateIndex(
                name: "IX_resultados_ranking_musica_RankingMusicaId_Decada_Candidatur~",
                table: "resultados_ranking_musica",
                columns: new[] { "RankingMusicaId", "Decada", "CandidaturaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resultados_ranking_musica_RankingMusicaId_Decada_Posicion",
                table: "resultados_ranking_musica",
                columns: new[] { "RankingMusicaId", "Decada", "Posicion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_votos_ranking_musica_CandidaturaId",
                table: "votos_ranking_musica",
                column: "CandidaturaId");

            migrationBuilder.CreateIndex(
                name: "IX_votos_ranking_musica_UsuarioId",
                table: "votos_ranking_musica",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consumos_diarios_puntos_nostalgia");

            migrationBuilder.DropTable(
                name: "resultados_ranking_musica");

            migrationBuilder.DropTable(
                name: "votos_ranking_musica");

            migrationBuilder.DropTable(
                name: "candidaturas_ranking_musica");

            migrationBuilder.DropTable(
                name: "rankings_musica");
        }
    }
}
