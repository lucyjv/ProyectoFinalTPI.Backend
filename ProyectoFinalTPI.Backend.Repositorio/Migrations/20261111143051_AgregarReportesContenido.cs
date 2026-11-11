using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarReportesContenido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EstaEliminado",
                table: "mensajes_chat",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "mensajes_chat",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "reportes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReportanteId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioReportadoId = table.Column<int>(type: "integer", nullable: false),
                    TipoContenido = table.Column<int>(type: "integer", nullable: false),
                    ContenidoId = table.Column<int>(type: "integer", nullable: false),
                    Motivo = table.Column<int>(type: "integer", nullable: false),
                    Detalle = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ContenidoSnapshot = table.Column<string>(type: "text", nullable: false),
                    ContextoId = table.Column<int>(type: "integer", nullable: true),
                    Contexto = table.Column<string>(type: "text", nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AdministradorId = table.Column<int>(type: "integer", nullable: true),
                    FechaResolucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AccionTomada = table.Column<int>(type: "integer", nullable: true),
                    ObservacionAdministrativa = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reportes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reportes_Usuario_AdministradorId",
                        column: x => x.AdministradorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reportes_Usuario_ReportanteId",
                        column: x => x.ReportanteId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reportes_Usuario_UsuarioReportadoId",
                        column: x => x.UsuarioReportadoId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reportes_AdministradorId",
                table: "reportes",
                column: "AdministradorId");

            migrationBuilder.CreateIndex(
                name: "IX_reportes_Estado_FechaCreacion",
                table: "reportes",
                columns: new[] { "Estado", "FechaCreacion" });

            migrationBuilder.CreateIndex(
                name: "IX_reportes_ReportanteId_TipoContenido_ContenidoId",
                table: "reportes",
                columns: new[] { "ReportanteId", "TipoContenido", "ContenidoId" },
                unique: true,
                filter: "\"Estado\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_reportes_TipoContenido_ContenidoId",
                table: "reportes",
                columns: new[] { "TipoContenido", "ContenidoId" });

            migrationBuilder.CreateIndex(
                name: "IX_reportes_UsuarioReportadoId",
                table: "reportes",
                column: "UsuarioReportadoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reportes");

            migrationBuilder.DropColumn(
                name: "EstaEliminado",
                table: "mensajes_chat");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "mensajes_chat");
        }
    }
}
