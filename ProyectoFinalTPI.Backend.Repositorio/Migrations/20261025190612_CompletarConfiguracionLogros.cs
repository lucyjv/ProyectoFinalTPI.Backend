using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class CompletarConfiguracionLogros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RachaDiasActual",
                table: "Usuario",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimoIngresoUtc",
                table: "Usuario",
                type: "timestamp with time zone",
                nullable: true);

            // Los usuarios interactivos existentes también necesitan una racha válida.
            migrationBuilder.Sql("""
                UPDATE "Usuario"
                SET "RachaDiasActual" = 0
                WHERE "tipo_usuario" IN ('Usuario_Personal', 'Usuario_Marca');
                """);

            migrationBuilder.InsertData(
                table: "logros",
                columns: new[] { "Id", "Descripcion", "PuntosRecompensa", "Titulo", "UrlIcono" },
                values: new object[,]
                {
                    { 1, "Crear tu primer recuerdo en el mapa.", 0, "Primer Paso", "." },
                    { 2, "Escribir tu primer comentario en el recuerdo de otra persona.", 0, "Primera Palabra", "." },
                    { 3, "Seguir a tus primeros 5 usuarios en la red social.", 0, "Con Amigos", "." },
                    { 4, "Sintonizar tu primera canción en el reproductor web retro.", 0, "Melómano", "." },
                    { 5, "Abrir tu primer Dato Curioso Aleatorio en el mapa.", 0, "Turista del Pasado", "." },
                    { 6, "Dar tu primer punto de nostalgia a una publicación.", 0, "Curioso", "." },
                    { 7, "Publicar 5 recuerdos cerca de tu ubicación física.", 0, "Cronista Local", "." },
                    { 8, "Tener recuerdos publicados en 3 décadas distintas.", 0, "Voz de una Época", "." },
                    { 9, "Subir 10 recuerdos que incluyan una imagen o foto.", 0, "Fotógrafo del Recuerdo", "." },
                    { 10, "Lograr que uno de tus recuerdos llegue a 20 puntos de nostalgia.", 0, "Popular", "." },
                    { 11, "Dar 50 puntos de nostalgia en total a publicaciones ajenas.", 0, "Generoso", "." },
                    { 12, "Llegar a los 25 comentarios en total dentro de la plataforma.", 0, "Conversador", "." },
                    { 13, "Alcanzar un total de 15 publicaciones creadas en tu muro.", 0, "Lince de las Praderas", "." },
                    { 14, "Ingresar a la aplicación durante 3 días seguidos.", 0, "Racha de Hierro", "." },
                    { 15, "Tener al menos un recuerdo publicado en cada una de las 6 décadas.", 0, "Década Completa", "." },
                    { 16, "Lograr que uno de tus recuerdos llegue a 100 puntos de nostalgia.", 0, "Leyenda", "." },
                    { 17, "Otorgar 500 puntos de nostalgia en total a la comunidad.", 0, "Mecenas", "." },
                    { 18, "Conseguir 50 seguidores en tu red.", 0, "Influencer", "." },
                    { 19, "Ingresar a la aplicación durante 7 días seguidos.", 0, "Racha de Oro", "." },
                    { 20, "Republicar 10 publicaciones de otros usuarios en tu feed.", 0, "Viajero del Tiempo", "." }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 3);

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
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "logros",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DropColumn(
                name: "RachaDiasActual",
                table: "Usuario");

            migrationBuilder.DropColumn(
                name: "UltimoIngresoUtc",
                table: "Usuario");
        }
    }
}
