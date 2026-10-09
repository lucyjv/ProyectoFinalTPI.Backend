using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCompartirPublicacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PublicacionOriginalId",
                table: "Publicaciones",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_PublicacionOriginalId",
                table: "Publicaciones",
                column: "PublicacionOriginalId");

            migrationBuilder.AddForeignKey(
                name: "FK_Publicaciones_Publicaciones_PublicacionOriginalId",
                table: "Publicaciones",
                column: "PublicacionOriginalId",
                principalTable: "Publicaciones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Publicaciones_Publicaciones_PublicacionOriginalId",
                table: "Publicaciones");

            migrationBuilder.DropIndex(
                name: "IX_Publicaciones_PublicacionOriginalId",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "PublicacionOriginalId",
                table: "Publicaciones");
        }
    }
}
