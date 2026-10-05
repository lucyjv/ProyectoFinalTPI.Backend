using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinalTPI.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AgregarUsuarioEnPublicacionYReferenciaInversaEnUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutorId",
                table: "Publicacion",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Publicacion_AutorId",
                table: "Publicacion",
                column: "AutorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Publicacion_Usuario_AutorId",
                table: "Publicacion",
                column: "AutorId",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Publicacion_Usuario_AutorId",
                table: "Publicacion");

            migrationBuilder.DropIndex(
                name: "IX_Publicacion_AutorId",
                table: "Publicacion");

            migrationBuilder.DropColumn(
                name: "AutorId",
                table: "Publicacion");
        }
    }
}
