using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class SistemaDeLogrosEntero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "logros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    UrlIcono = table.Column<string>(type: "text", nullable: false),
                    PuntosRecompensa = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_logros", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "logros_usuarios",
                columns: table => new
                {
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    LogroId = table.Column<int>(type: "integer", nullable: false),
                    FechaDesbloqueo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_logros_usuarios", x => new { x.UsuarioId, x.LogroId });
                    table.ForeignKey(
                        name: "FK_logros_usuarios_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_logros_usuarios_logros_LogroId",
                        column: x => x.LogroId,
                        principalTable: "logros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_logros_usuarios_LogroId",
                table: "logros_usuarios",
                column: "LogroId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "logros_usuarios");

            migrationBuilder.DropTable(
                name: "logros");
        }
    }
}
