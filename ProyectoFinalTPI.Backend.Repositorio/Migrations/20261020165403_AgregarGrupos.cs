using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarGrupos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GrupoId",
                table: "Publicaciones",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "grupos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreadorId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grupos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_grupos_Usuario_CreadorId",
                        column: x => x.CreadorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "grupos_integrantes",
                columns: table => new
                {
                    GrupoId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    FechaUnion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grupos_integrantes", x => new { x.GrupoId, x.UsuarioId });
                    table.ForeignKey(
                        name: "FK_grupos_integrantes_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_grupos_integrantes_grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "grupos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_GrupoId",
                table: "Publicaciones",
                column: "GrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_grupos_CreadorId",
                table: "grupos",
                column: "CreadorId");

            migrationBuilder.CreateIndex(
                name: "IX_grupos_integrantes_UsuarioId",
                table: "grupos_integrantes",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Publicaciones_grupos_GrupoId",
                table: "Publicaciones",
                column: "GrupoId",
                principalTable: "grupos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Publicaciones_grupos_GrupoId",
                table: "Publicaciones");

            migrationBuilder.DropTable(
                name: "grupos_integrantes");

            migrationBuilder.DropTable(
                name: "grupos");

            migrationBuilder.DropIndex(
                name: "IX_Publicaciones_GrupoId",
                table: "Publicaciones");

            migrationBuilder.DropColumn(
                name: "GrupoId",
                table: "Publicaciones");
        }
    }
}
