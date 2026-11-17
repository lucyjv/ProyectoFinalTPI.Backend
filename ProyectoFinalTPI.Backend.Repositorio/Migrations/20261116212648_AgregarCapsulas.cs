using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCapsulas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "capsulas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropietarioId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Visibilidad = table.Column<int>(type: "integer", nullable: false),
                    PortadaPublicacionId = table.Column<int>(type: "integer", nullable: true),
                    PortadaAdjuntoId = table.Column<int>(type: "integer", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capsulas", x => x.Id);
                    table.CheckConstraint("CK_capsulas_Visibilidad", "\"Visibilidad\" IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_capsulas_Publicaciones_PortadaPublicacionId",
                        column: x => x.PortadaPublicacionId,
                        principalTable: "Publicaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_capsulas_Usuario_PropietarioId",
                        column: x => x.PropietarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_capsulas_adjuntos_publicacion_PortadaAdjuntoId",
                        column: x => x.PortadaAdjuntoId,
                        principalTable: "adjuntos_publicacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "capsulas_publicaciones",
                columns: table => new
                {
                    CapsulaId = table.Column<int>(type: "integer", nullable: false),
                    PublicacionId = table.Column<int>(type: "integer", nullable: false),
                    FechaIncorporacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capsulas_publicaciones", x => new { x.CapsulaId, x.PublicacionId });
                    table.ForeignKey(
                        name: "FK_capsulas_publicaciones_Publicaciones_PublicacionId",
                        column: x => x.PublicacionId,
                        principalTable: "Publicaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_capsulas_publicaciones_capsulas_CapsulaId",
                        column: x => x.CapsulaId,
                        principalTable: "capsulas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_capsulas_PortadaAdjuntoId",
                table: "capsulas",
                column: "PortadaAdjuntoId");

            migrationBuilder.CreateIndex(
                name: "IX_capsulas_PortadaPublicacionId",
                table: "capsulas",
                column: "PortadaPublicacionId");

            migrationBuilder.CreateIndex(
                name: "IX_capsulas_PropietarioId_Id",
                table: "capsulas",
                columns: new[] { "PropietarioId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_capsulas_publicaciones_PublicacionId",
                table: "capsulas_publicaciones",
                column: "PublicacionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "capsulas_publicaciones");

            migrationBuilder.DropTable(
                name: "capsulas");
        }
    }
}
