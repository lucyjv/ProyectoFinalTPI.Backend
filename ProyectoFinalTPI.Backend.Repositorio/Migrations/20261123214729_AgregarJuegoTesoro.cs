using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarJuegoTesoro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "retos_tesoro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SemanaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    ClaveUbicacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Pista = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    NombreHallazgo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Latitud = table.Column<double>(type: "double precision", nullable: false),
                    Longitud = table.Column<double>(type: "double precision", nullable: false),
                    ZoomMinimo = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retos_tesoro", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hallazgos_tesoro",
                columns: table => new
                {
                    RetoId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    FechaHallazgoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hallazgos_tesoro", x => new { x.RetoId, x.UsuarioId });
                    table.ForeignKey(
                        name: "FK_hallazgos_tesoro_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_hallazgos_tesoro_retos_tesoro_RetoId",
                        column: x => x.RetoId,
                        principalTable: "retos_tesoro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hallazgos_tesoro_UsuarioId",
                table: "hallazgos_tesoro",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_retos_tesoro_SemanaInicio",
                table: "retos_tesoro",
                column: "SemanaInicio",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hallazgos_tesoro");

            migrationBuilder.DropTable(
                name: "retos_tesoro");
        }
    }
}
