using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIndicesRedRecuerdos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE INDEX "IX_lugares_Coordenadas_geography"
                ON lugares USING GIST (("Coordenadas"::geography));
                CREATE INDEX "IX_Publicaciones_FechaCalendarioUTC"
                ON "Publicaciones" ((("Fecha" AT TIME ZONE 'UTC')::date));
                """);
            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_Fecha",
                table: "Publicaciones",
                column: "Fecha");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX "IX_lugares_Coordenadas_geography";
                DROP INDEX "IX_Publicaciones_FechaCalendarioUTC";
                """);
            migrationBuilder.DropIndex(
                name: "IX_Publicaciones_Fecha",
                table: "Publicaciones");
        }
    }
}
