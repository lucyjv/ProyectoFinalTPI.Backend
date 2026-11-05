using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAdministracionUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EstaEliminado",
                table: "Usuario",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "Usuario",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_EstaEliminado_FechaCreacion",
                table: "Usuario",
                columns: new[] { "EstaEliminado", "FechaCreacion" });

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_Categoria_EstaOculto_FechaCreación",
                table: "Publicaciones",
                columns: new[] { "Categoria", "EstaOculto", "FechaCreación" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuario_EstaEliminado_FechaCreacion",
                table: "Usuario");

            migrationBuilder.DropIndex(
                name: "IX_Publicaciones_Categoria_EstaOculto_FechaCreación",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "EstaEliminado",
                table: "Usuario");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "Usuario");
        }
    }
}
