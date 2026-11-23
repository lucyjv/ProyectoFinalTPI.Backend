using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarBilleteraYComprasPuntosNostalgia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billeteras_puntos_nostalgia",
                columns: table => new
                {
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    PuntosExtra = table.Column<int>(type: "integer", nullable: false),
                    FechaActualizacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billeteras_puntos_nostalgia", x => x.UsuarioId);
                    table.CheckConstraint("CK_billeteras_puntos_extra", " \"PuntosExtra\" >= 0 ");
                    table.ForeignKey(
                        name: "FK_billeteras_puntos_nostalgia_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "compras_puntos_nostalgia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    PackId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Puntos = table.Column<int>(type: "integer", nullable: false),
                    PrecioUsd = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    CotizacionDolar = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    PrecioArs = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PreferenceId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    PaymentId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    FechaCreacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FechaAcreditacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compras_puntos_nostalgia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compras_puntos_nostalgia_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_compras_puntos_nostalgia_UsuarioId",
                table: "compras_puntos_nostalgia",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billeteras_puntos_nostalgia");

            migrationBuilder.DropTable(
                name: "compras_puntos_nostalgia");
        }
    }
}
