using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPortadaSubidaCapsula : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PortadaRecursoId",
                table: "capsulas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_capsulas_PortadaUnica",
                table: "capsulas",
                sql: "\"PortadaRecursoId\" IS NULL OR (\"PortadaPublicacionId\" IS NULL AND \"PortadaAdjuntoId\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_capsulas_PortadaUnica",
                table: "capsulas");

            migrationBuilder.DropColumn(
                name: "PortadaRecursoId",
                table: "capsulas");
        }
    }
}
