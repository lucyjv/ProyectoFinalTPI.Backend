using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCorreccionMomentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PermitirMomentosAutomaticos",
                table: "Publicaciones",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<long>(
                name: "VersionRelaciones",
                table: "Publicaciones",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PermitirMomentosAutomaticos",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "VersionRelaciones",
                table: "Publicaciones");
        }
    }
}
