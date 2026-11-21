using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPuntuacionesPublicacionConCupo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "puntuaciones_publicacion",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    PublicacionId = table.Column<int>(type: "integer", nullable: false),
                    Puntaje = table.Column<int>(type: "integer", nullable: true),
                    FechaActualizacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PendienteDeProyectar = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_puntuaciones_publicacion", x => x.Id);
                    table.CheckConstraint("CK_puntuaciones_publicacion_puntaje", " \"Puntaje\" BETWEEN 1 AND 5 ");
                    table.ForeignKey(
                        name: "FK_puntuaciones_publicacion_Publicaciones_PublicacionId",
                        column: x => x.PublicacionId,
                        principalTable: "Publicaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_puntuaciones_publicacion_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_puntuaciones_publicacion_PendienteDeProyectar",
                table: "puntuaciones_publicacion",
                column: "PendienteDeProyectar",
                filter: " \"PendienteDeProyectar\" ");

            migrationBuilder.CreateIndex(
                name: "IX_puntuaciones_publicacion_PublicacionId",
                table: "puntuaciones_publicacion",
                column: "PublicacionId");

            migrationBuilder.CreateIndex(
                name: "IX_puntuaciones_publicacion_UsuarioId_PublicacionId",
                table: "puntuaciones_publicacion",
                columns: new[] { "UsuarioId", "PublicacionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "puntuaciones_publicacion");
        }
    }
}
