using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarNotificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notificaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioDestinatarioId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Leida = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaLeida = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaExpiracion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublicacionId = table.Column<int>(type: "integer", nullable: true),
                    EmisorId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notificaciones_Usuario_EmisorId",
                        column: x => x.EmisorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_notificaciones_Usuario_UsuarioDestinatarioId",
                        column: x => x.UsuarioDestinatarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_EmisorId",
                table: "notificaciones",
                column: "EmisorId");

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_expiracion",
                table: "notificaciones",
                column: "FechaExpiracion");

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_pendiente",
                table: "notificaciones",
                columns: new[] { "UsuarioDestinatarioId", "Tipo", "PublicacionId", "Leida" });

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_usuario_fecha",
                table: "notificaciones",
                columns: new[] { "UsuarioDestinatarioId", "FechaCreacion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notificaciones");
        }
    }
}
