using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AdministrarJuegoTesoro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ubicaciones_tesoro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Pista = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Latitud = table.Column<double>(type: "double precision", nullable: false),
                    Longitud = table.Column<double>(type: "double precision", nullable: false),
                    ZoomMinimo = table.Column<double>(type: "double precision", nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ubicaciones_tesoro", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "programaciones_tesoro",
                columns: table => new
                {
                    SemanaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    UbicacionId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programaciones_tesoro", x => x.SemanaInicio);
                    table.ForeignKey(
                        name: "FK_programaciones_tesoro_ubicaciones_tesoro_UbicacionId",
                        column: x => x.UbicacionId,
                        principalTable: "ubicaciones_tesoro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_programaciones_tesoro_UbicacionId",
                table: "programaciones_tesoro",
                column: "UbicacionId");

            // Conserva la ubicación inicial al pasar del archivo de configuración
            // al catálogo administrable. La identidad queda a cargo de PostgreSQL.
            migrationBuilder.Sql("""
                INSERT INTO ubicaciones_tesoro ("Nombre", "Pista", "Latitud", "Longitud", "ZoomMinimo", "Activa")
                VALUES ('Monumento al General San Martín · Plaza San Martín, Retiro',
                  'Cruzó los Andes y fue Libertador de Argentina, Chile y Perú. Hoy, a caballo entre los árboles de Retiro, guarda un secreto.',
                  -34.59558, -58.3785, 18, TRUE);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "programaciones_tesoro");

            migrationBuilder.DropTable(
                name: "ubicaciones_tesoro");
        }
    }
}
