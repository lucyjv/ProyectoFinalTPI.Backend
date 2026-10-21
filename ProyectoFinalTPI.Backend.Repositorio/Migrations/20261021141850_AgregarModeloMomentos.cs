using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarModeloMomentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "momentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MomentoPadreId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_momentos", x => x.Id);
                    table.CheckConstraint("CK_momentos_fechas", "\"FechaInicio\" IS NULL OR \"FechaFin\" IS NULL OR \"FechaFin\" >= \"FechaInicio\"");
                    table.CheckConstraint("CK_momentos_nombre", "length(btrim(\"Nombre\")) > 0");
                    table.CheckConstraint("CK_momentos_padre_distinto", "\"MomentoPadreId\" IS NULL OR \"MomentoPadreId\" <> \"Id\"");
                    table.ForeignKey(
                        name: "FK_momentos_momentos_MomentoPadreId",
                        column: x => x.MomentoPadreId,
                        principalTable: "momentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "momentos_alias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MomentoId = table.Column<int>(type: "integer", nullable: false),
                    Texto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TextoNormalizado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_momentos_alias", x => x.Id);
                    table.CheckConstraint("CK_momentos_alias_texto", "length(btrim(\"Texto\")) > 0 AND length(btrim(\"TextoNormalizado\")) > 0");
                    table.ForeignKey(
                        name: "FK_momentos_alias_momentos_MomentoId",
                        column: x => x.MomentoId,
                        principalTable: "momentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "publicaciones_momentos",
                columns: table => new
                {
                    PublicacionId = table.Column<int>(type: "integer", nullable: false),
                    MomentoId = table.Column<int>(type: "integer", nullable: false),
                    Origen = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Confianza = table.Column<double>(type: "double precision", nullable: true),
                    VersionAnalisis = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_publicaciones_momentos", x => new { x.PublicacionId, x.MomentoId });
                    table.CheckConstraint("CK_publicaciones_momentos_confianza", "\"Confianza\" IS NULL OR (\"Confianza\" >= 0 AND \"Confianza\" <= 1)");
                    table.CheckConstraint("CK_publicaciones_momentos_estado", "\"Estado\" IN (0, 1)");
                    table.CheckConstraint("CK_publicaciones_momentos_origen", "\"Origen\" IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_publicaciones_momentos_Publicaciones_PublicacionId",
                        column: x => x.PublicacionId,
                        principalTable: "Publicaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_publicaciones_momentos_momentos_MomentoId",
                        column: x => x.MomentoId,
                        principalTable: "momentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_momentos_MomentoPadreId",
                table: "momentos",
                column: "MomentoPadreId");

            migrationBuilder.CreateIndex(
                name: "IX_momentos_Nombre",
                table: "momentos",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_momentos_alias_MomentoId_TextoNormalizado",
                table: "momentos_alias",
                columns: new[] { "MomentoId", "TextoNormalizado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_momentos_alias_TextoNormalizado",
                table: "momentos_alias",
                column: "TextoNormalizado");

            migrationBuilder.CreateIndex(
                name: "IX_publicaciones_momentos_MomentoId_Estado_PublicacionId",
                table: "publicaciones_momentos",
                columns: new[] { "MomentoId", "Estado", "PublicacionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "momentos_alias");

            migrationBuilder.DropTable(
                name: "publicaciones_momentos");

            migrationBuilder.DropTable(
                name: "momentos");
        }
    }
}
