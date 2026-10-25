using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class RetirarLogrosSinImplementacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 17);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "logros",
                columns: new[] { "Id", "Descripcion", "PuntosRecompensa", "Titulo", "UrlIcono" },
                values: new object[,]
                {
                    { 4, "Sintonizar tu primera canción en el reproductor web retro.", 0, "Melómano", "." },
                    { 5, "Abrir tu primer Dato Curioso Aleatorio en el mapa.", 0, "Turista del Pasado", "." },
                    { 17, "Otorgar 500 puntos de nostalgia en total a la comunidad.", 0, "Mecenas", "." }
                });
        }
    }
}
