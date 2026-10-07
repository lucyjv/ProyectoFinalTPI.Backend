using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class ActualizarPublicacionMultimedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Foto",
                table: "Publicaciones",
                newName: "UrlMultimedia");

            migrationBuilder.RenameColumn(
                name: "Año",
                table: "Publicaciones",
                newName: "TipoMultimedia");

            migrationBuilder.AddColumn<DateTime>(
                name: "Fecha",
                table: "Publicaciones",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Fecha",
                table: "Publicaciones");

            migrationBuilder.RenameColumn(
                name: "UrlMultimedia",
                table: "Publicaciones",
                newName: "Foto");

            migrationBuilder.RenameColumn(
                name: "TipoMultimedia",
                table: "Publicaciones",
                newName: "Año");
        }
    }
}
