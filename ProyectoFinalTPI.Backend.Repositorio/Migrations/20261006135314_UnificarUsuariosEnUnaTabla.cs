using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinalTPI.Backend.Repositorio.Migrations
{
    public partial class UnificarUsuariosEnUnaTabla : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE usuarios, usuarios_interactivos, usuarios_personales, usuarios_marcas, moderadores IN ACCESS EXCLUSIVE MODE;

                DO $guard$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM usuarios_interactivos i
                        LEFT JOIN usuarios_personales p ON p."Id" = i."Id"
                        LEFT JOIN usuarios_marcas m ON m."Id" = i."Id"
                        WHERE (p."Id" IS NULL AND m."Id" IS NULL)
                           OR (p."Id" IS NOT NULL AND m."Id" IS NOT NULL)
                    ) THEN
                        RAISE EXCEPTION 'Hay usuarios interactivos sin subtipo concreto o con dos subtipos. Corregir antes de unificar.';
                    END IF;
                END
                $guard$;

                ALTER TABLE "Comentario" DROP CONSTRAINT "FK_Comentario_usuarios_UsuarioId";
                ALTER TABLE "Publicaciones" DROP CONSTRAINT "FK_Publicaciones_usuarios_interactivos_UsuarioId";
                ALTER TABLE usuarios RENAME TO "Usuario";
                ALTER TABLE "Usuario" RENAME CONSTRAINT "PK_usuarios" TO "PK_Usuario";
                ALTER TABLE "Usuario"
                    ADD COLUMN "Apellido" text,
                    ADD COLUMN "Cuit" text,
                    ADD COLUMN "FechaAscenso" timestamp with time zone,
                    ADD COLUMN "FechaNacimiento" date,
                    ADD COLUMN "Nombre" text,
                    ADD COLUMN "NombreEmpresa" text,
                    ADD COLUMN "PuntosNostalgia" integer,
                    ADD COLUMN "ReportesAtendidos" integer,
                    ADD COLUMN "Reputacion" integer,
                    ADD COLUMN "SuspendidoHasta" timestamp with time zone,
                    ADD COLUMN "WebOficial" text,
                    ADD COLUMN tipo_usuario varchar(21) NOT NULL DEFAULT 'Usuario';

                UPDATE "Usuario" u
                SET "PuntosNostalgia" = i."PuntosNostalgia", "SuspendidoHasta" = i."SuspendidoHasta"
                FROM usuarios_interactivos i WHERE u."Id" = i."Id";

                UPDATE "Usuario" u
                SET "Nombre" = p."Nombre", "Apellido" = p."Apellido",
                    "FechaNacimiento" = p."FechaNacimiento", "Reputacion" = p."Reputacion",
                    tipo_usuario = 'Usuario_Personal'
                FROM usuarios_personales p WHERE u."Id" = p."Id";

                UPDATE "Usuario" u
                SET "NombreEmpresa" = m."NombreEmpresa", "Cuit" = m."Cuit",
                    "WebOficial" = m."WebOficial", tipo_usuario = 'Usuario_Marca'
                FROM usuarios_marcas m WHERE u."Id" = m."Id";

                UPDATE "Usuario" u
                SET "ReportesAtendidos" = m."ReportesAtendidos",
                    "FechaAscenso" = m."FechaAscenso", tipo_usuario = 'Moderador'
                FROM moderadores m WHERE u."Id" = m."Id";

                ALTER TABLE "Usuario" ALTER COLUMN tipo_usuario DROP DEFAULT;

                DROP TABLE moderadores;
                DROP TABLE usuarios_marcas;
                DROP TABLE usuarios_personales;
                DROP TABLE usuarios_interactivos;

                ALTER TABLE "Comentario" ADD CONSTRAINT "FK_Comentario_Usuario_UsuarioId"
                    FOREIGN KEY ("UsuarioId") REFERENCES "Usuario" ("Id") ON DELETE CASCADE;
                ALTER TABLE "Publicaciones" ADD CONSTRAINT "FK_Publicaciones_Usuario_UsuarioId"
                    FOREIGN KEY ("UsuarioId") REFERENCES "Usuario" ("Id") ON DELETE CASCADE;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException(
                "La vuelta a tablas separadas requiere una migración inversa de datos; se bloquea para evitar perder los campos de los usuarios.");
        }
    }
}
