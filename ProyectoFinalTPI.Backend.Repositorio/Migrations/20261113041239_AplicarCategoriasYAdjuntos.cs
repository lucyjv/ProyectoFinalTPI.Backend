using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AplicarCategoriasYAdjuntos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MusicaArtista",
                table: "Publicaciones",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicaSpotifyId",
                table: "Publicaciones",
                type: "character varying(22)",
                maxLength: 22,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicaTitulo",
                table: "Publicaciones",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "adjuntos_publicacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicacionId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adjuntos_publicacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_adjuntos_publicacion_Publicaciones_PublicacionId",
                        column: x => x.PublicacionId,
                        principalTable: "Publicaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consumos_publicaciones",
                columns: table => new
                {
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    SemanaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    Cantidad = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consumos_publicaciones", x => new { x.UsuarioId, x.SemanaInicio });
                    table.ForeignKey(
                        name: "FK_consumos_publicaciones_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Conservar el consumo de recuerdos previos, incluidos los ocultos, sin contar citas.
            migrationBuilder.Sql("""
                INSERT INTO consumos_publicaciones ("UsuarioId", "SemanaInicio", "Cantidad")
                SELECT "UsuarioId", fecha - EXTRACT(DOW FROM fecha)::int, COUNT(*)::int
                FROM (
                    SELECT "UsuarioId", ("FechaCreación" AT TIME ZONE 'America/Argentina/Buenos_Aires')::date AS fecha
                    FROM "Publicaciones" WHERE "PublicacionOriginalId" IS NULL
                ) AS recuerdos
                GROUP BY "UsuarioId", fecha - EXTRACT(DOW FROM fecha)::int;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_adjuntos_publicacion_PublicacionId_Orden",
                table: "adjuntos_publicacion",
                columns: new[] { "PublicacionId", "Orden" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adjuntos_publicacion");

            migrationBuilder.DropTable(
                name: "consumos_publicaciones");

            migrationBuilder.DropColumn(
                name: "MusicaArtista",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "MusicaSpotifyId",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "MusicaTitulo",
                table: "Publicaciones");
        }
    }
}
