using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarColaAnalisisRecuerdos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "VersionContenido",
                table: "Publicaciones",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "trabajos_analisis_recuerdos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicacionId = table.Column<int>(type: "integer", nullable: false),
                    VersionContenido = table.Column<long>(type: "bigint", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Intentos = table.Column<int>(type: "integer", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProximoIntento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TokenReserva = table.Column<Guid>(type: "uuid", nullable: true),
                    ReservadoHasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CodigoError = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    VersionAnalizador = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trabajos_analisis_recuerdos", x => x.Id);
                    table.CheckConstraint("CK_trabajos_analisis_estado", "\"Estado\" BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_trabajos_analisis_intentos", "\"Intentos\" >= 0 AND \"VersionContenido\" > 0");
                    table.CheckConstraint("CK_trabajos_analisis_reserva", "(\"Estado\" = 1 AND \"TokenReserva\" IS NOT NULL AND \"ReservadoHasta\" IS NOT NULL) OR (\"Estado\" <> 1 AND \"TokenReserva\" IS NULL AND \"ReservadoHasta\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_trabajos_analisis_recuerdos_Publicaciones_PublicacionId",
                        column: x => x.PublicacionId,
                        principalTable: "Publicaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trabajos_analisis_recuerdos_Estado_ProximoIntento",
                table: "trabajos_analisis_recuerdos",
                columns: new[] { "Estado", "ProximoIntento" });

            migrationBuilder.CreateIndex(
                name: "IX_trabajos_analisis_recuerdos_Estado_ReservadoHasta",
                table: "trabajos_analisis_recuerdos",
                columns: new[] { "Estado", "ReservadoHasta" });

            migrationBuilder.CreateIndex(
                name: "IX_trabajos_analisis_recuerdos_PublicacionId_VersionContenido",
                table: "trabajos_analisis_recuerdos",
                columns: new[] { "PublicacionId", "VersionContenido" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trabajos_analisis_recuerdos");

            migrationBuilder.DropColumn(
                name: "VersionContenido",
                table: "Publicaciones");
        }
    }
}
