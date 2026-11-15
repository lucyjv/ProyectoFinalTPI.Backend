using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAccesoGoogle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleSubject",
                table: "Usuario",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PuedeVincularGoogle",
                table: "Usuario",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SoloGoogle",
                table: "Usuario",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Preserve eligibility only for password accounts that already exist at deployment.
            // The column default remains false for all later registrations.
            migrationBuilder.Sql("""
                UPDATE "Usuario" SET "PuedeVincularGoogle" = TRUE
                WHERE "Contraseña" <> '' AND NOT "EstaEliminado";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_GoogleSubject",
                table: "Usuario",
                column: "GoogleSubject",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuario_GoogleSubject",
                table: "Usuario");

            migrationBuilder.DropColumn(
                name: "GoogleSubject",
                table: "Usuario");

            migrationBuilder.DropColumn(
                name: "PuedeVincularGoogle",
                table: "Usuario");

            migrationBuilder.DropColumn(
                name: "SoloGoogle",
                table: "Usuario");
        }
    }
}
