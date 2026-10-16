using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarModuloComentarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Comentario_PublicacionId",
                table: "Comentario");

            migrationBuilder.AddColumn<int>(
                name: "ComentarioPadreId",
                table: "Comentario",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EstaEliminado",
                table: "Comentario",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "Comentario",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "reacciones_comentario",
                columns: table => new
                {
                    ComentarioId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reacciones_comentario", x => new { x.ComentarioId, x.UsuarioId });
                    table.ForeignKey(
                        name: "FK_reacciones_comentario_Comentario_ComentarioId",
                        column: x => x.ComentarioId,
                        principalTable: "Comentario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reacciones_comentario_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Comentario_ComentarioPadreId",
                table: "Comentario",
                column: "ComentarioPadreId");

            migrationBuilder.CreateIndex(
                name: "IX_Comentario_PublicacionId_ComentarioPadreId_FechaCreacion_Id",
                table: "Comentario",
                columns: new[] { "PublicacionId", "ComentarioPadreId", "FechaCreacion", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_reacciones_comentario_UsuarioId",
                table: "reacciones_comentario",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Comentario_Comentario_ComentarioPadreId",
                table: "Comentario",
                column: "ComentarioPadreId",
                principalTable: "Comentario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comentario_Comentario_ComentarioPadreId",
                table: "Comentario");

            migrationBuilder.DropTable(
                name: "reacciones_comentario");

            migrationBuilder.DropIndex(
                name: "IX_Comentario_ComentarioPadreId",
                table: "Comentario");

            migrationBuilder.DropIndex(
                name: "IX_Comentario_PublicacionId_ComentarioPadreId_FechaCreacion_Id",
                table: "Comentario");

            migrationBuilder.DropColumn(
                name: "ComentarioPadreId",
                table: "Comentario");

            migrationBuilder.DropColumn(
                name: "EstaEliminado",
                table: "Comentario");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "Comentario");

            migrationBuilder.CreateIndex(
                name: "IX_Comentario_PublicacionId",
                table: "Comentario",
                column: "PublicacionId");
        }
    }
}
