using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    /// <inheritdoc />
    public partial class MigracionInicialCompleta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La rama anterior utilizaba herencia TPH y nombres singulares.
            // Conservar ese esquema vacío permite aplicar esta migración TPT
            // sin borrar tablas ni falsificar el historial de migraciones.
            migrationBuilder.Sql("""
                DO $migration$
                DECLARE
                    tabla text;
                    tiene_datos boolean;
                BEGIN
                    IF to_regclass('public."Usuario"') IS NOT NULL THEN
                        FOREACH tabla IN ARRAY ARRAY['Usuario', 'Publicacion', 'Comentario', 'Lugar', 'Moderacion'] LOOP
                            IF to_regclass(format('public.%I', tabla)) IS NULL THEN
                                RAISE EXCEPTION 'Esquema anterior incompleto: falta %. Revisar antes de migrar.', tabla;
                            END IF;
                            EXECUTE format('SELECT EXISTS (SELECT 1 FROM public.%I)', tabla) INTO tiene_datos;
                            IF tiene_datos THEN
                                RAISE EXCEPTION 'La tabla % contiene datos. Se requiere una migración de datos TPH a TPT antes de continuar.', tabla;
                            END IF;
                        END LOOP;
                        IF EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'nostalgiar_legacy_20261006') THEN
                            RAISE EXCEPTION 'Ya existe el esquema de respaldo nostalgiar_legacy_20261006. Revisar antes de migrar.';
                        END IF;
                        CREATE SCHEMA nostalgiar_legacy_20261006;
                        FOREACH tabla IN ARRAY ARRAY['Usuario', 'Publicacion', 'Comentario', 'Lugar', 'Moderacion'] LOOP
                            EXECUTE format('ALTER TABLE public.%I SET SCHEMA nostalgiar_legacy_20261006', tabla);
                        END LOOP;
                    END IF;
                END
                $migration$;
                """);

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "lugares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Direccion = table.Column<string>(type: "text", nullable: false),
                    Coordenadas = table.Column<Point>(type: "geometry(Point, 4326)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lugares", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Moderaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JustificacionModerador = table.Column<string>(type: "text", nullable: false),
                    FechaCreada = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaResuelto = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Motivo = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Moderaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Contraseña = table.Column<string>(type: "text", nullable: false),
                    EsAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_interactivos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    PuntosNostalgia = table.Column<int>(type: "integer", nullable: false),
                    SuspendidoHasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios_interactivos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_usuarios_interactivos_usuarios_Id",
                        column: x => x.Id,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Publicaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    Año = table.Column<int>(type: "integer", nullable: false),
                    Foto = table.Column<string>(type: "text", nullable: false),
                    EstaOculto = table.Column<bool>(type: "boolean", nullable: false),
                    MotivoOculto = table.Column<string>(type: "text", nullable: true),
                    FechaCreación = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LugarId = table.Column<int>(type: "integer", nullable: false),
                    Categoria = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Publicaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Publicaciones_lugares_LugarId",
                        column: x => x.LugarId,
                        principalTable: "lugares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Publicaciones_usuarios_interactivos_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios_interactivos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_marcas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    NombreEmpresa = table.Column<string>(type: "text", nullable: false),
                    Cuit = table.Column<string>(type: "text", nullable: false),
                    WebOficial = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios_marcas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_usuarios_marcas_usuarios_interactivos_Id",
                        column: x => x.Id,
                        principalTable: "usuarios_interactivos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_personales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Apellido = table.Column<string>(type: "text", nullable: false),
                    FechaNacimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    Reputacion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios_personales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_usuarios_personales_usuarios_interactivos_Id",
                        column: x => x.Id,
                        principalTable: "usuarios_interactivos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Comentario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Contenido = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstaOculto = table.Column<bool>(type: "boolean", nullable: false),
                    PublicacionId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comentario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comentario_Publicaciones_PublicacionId",
                        column: x => x.PublicacionId,
                        principalTable: "Publicaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Comentario_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "moderadores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ReportesAtendidos = table.Column<int>(type: "integer", nullable: false),
                    FechaAscenso = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderadores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_moderadores_usuarios_personales_Id",
                        column: x => x.Id,
                        principalTable: "usuarios_personales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Comentario_PublicacionId",
                table: "Comentario",
                column: "PublicacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Comentario_UsuarioId",
                table: "Comentario",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_LugarId",
                table: "Publicaciones",
                column: "LugarId");

            migrationBuilder.CreateIndex(
                name: "IX_Publicaciones_UsuarioId",
                table: "Publicaciones",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Comentario");

            migrationBuilder.DropTable(
                name: "Moderaciones");

            migrationBuilder.DropTable(
                name: "moderadores");

            migrationBuilder.DropTable(
                name: "usuarios_marcas");

            migrationBuilder.DropTable(
                name: "Publicaciones");

            migrationBuilder.DropTable(
                name: "usuarios_personales");

            migrationBuilder.DropTable(
                name: "lugares");

            migrationBuilder.DropTable(
                name: "usuarios_interactivos");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
