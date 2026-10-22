using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIdentidadYFusionMomentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClaveIdentidad",
                table: "momentos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MomentoCanonicoId",
                table: "momentos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VersionCatalogo",
                table: "momentos",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateIndex(
                name: "IX_momentos_ClaveIdentidad",
                table: "momentos",
                column: "ClaveIdentidad",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_momentos_MomentoCanonicoId",
                table: "momentos",
                column: "MomentoCanonicoId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_momentos_canonico_distinto",
                table: "momentos",
                sql: "\"MomentoCanonicoId\" IS NULL OR \"MomentoCanonicoId\" <> \"Id\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_momentos_identidad_activa",
                table: "momentos",
                sql: "\"MomentoCanonicoId\" IS NULL OR \"ClaveIdentidad\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_momentos_momentos_MomentoCanonicoId",
                table: "momentos",
                column: "MomentoCanonicoId",
                principalTable: "momentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_momentos_momentos_MomentoCanonicoId",
                table: "momentos");

            migrationBuilder.DropIndex(
                name: "IX_momentos_ClaveIdentidad",
                table: "momentos");

            migrationBuilder.DropIndex(
                name: "IX_momentos_MomentoCanonicoId",
                table: "momentos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_momentos_canonico_distinto",
                table: "momentos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_momentos_identidad_activa",
                table: "momentos");

            migrationBuilder.DropColumn(
                name: "ClaveIdentidad",
                table: "momentos");

            migrationBuilder.DropColumn(
                name: "MomentoCanonicoId",
                table: "momentos");

            migrationBuilder.DropColumn(
                name: "VersionCatalogo",
                table: "momentos");
        }
    }
}
