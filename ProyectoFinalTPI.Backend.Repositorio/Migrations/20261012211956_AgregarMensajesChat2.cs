using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMensajesChat2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mensajes_chat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmisorId = table.Column<int>(type: "integer", nullable: false),
                    DestinatarioId = table.Column<int>(type: "integer", nullable: false),
                    Contenido = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FechaEnvio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaExpiracion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mensajes_chat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mensajes_chat_Usuario_DestinatarioId",
                        column: x => x.DestinatarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mensajes_chat_Usuario_EmisorId",
                        column: x => x.EmisorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mensajes_chat_DestinatarioId",
                table: "mensajes_chat",
                column: "DestinatarioId");

            migrationBuilder.CreateIndex(
                name: "IX_mensajes_chat_expiracion",
                table: "mensajes_chat",
                column: "FechaExpiracion");

            migrationBuilder.CreateIndex(
                name: "IX_mensajes_chat_participantes",
                table: "mensajes_chat",
                columns: new[] { "EmisorId", "DestinatarioId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mensajes_chat");
        }
    }
}
