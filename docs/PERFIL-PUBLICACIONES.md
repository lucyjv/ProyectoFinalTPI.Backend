# Error 500 al cargar publicaciones del perfil

La migración `20261120165404_AgregarPuntuacionesPublicacionConCupo` crea
`puntuaciones_publicacion`. Las lecturas de publicaciones con resultados consultan
esta tabla para combinar los puntajes persistidos con los históricos de Neo4j.
Si falta, PostgreSQL devuelve `42P01: relation "puntuaciones_publicacion" does not exist`.

El fallo se reprodujo con PostgreSQL y Neo4j temporales: el listado del perfil y
el detalle responden 500 antes de aplicar la migración y 200 después, conservando
los puntajes históricos. Esto reproduce una causa posible; no confirma el estado
del esquema de producción.

El workflow `deploy-azure.yml` publica la API pero no aplica migraciones. Su
comprobación de Swagger tampoco consulta la tabla de puntuaciones.

## Confirmación sin Application Insights

En la base PostgreSQL utilizada por la API, ejecutar estas consultas de lectura:

```sql
SELECT to_regclass('puntuaciones_publicacion') AS tabla_puntuaciones;

SELECT "MigrationId"
FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20261120165404_AgregarPuntuacionesPublicacionConCupo';
```

Si la primera devuelve `NULL` y la segunda no devuelve filas, falta la migración.
Si la tabla y el registro existen, esta explicación no basta para diagnosticar
el error. Si solo uno existe, revisar la inconsistencia antes de aplicar cambios.

## Aplicación de la migración pendiente

Si están aplicadas las migraciones hasta
`20261118150321_AgregarPersistenciaRankingMusica`, ejecutar
[`sql/restaurar-publicaciones-perfil.sql`](sql/restaurar-publicaciones-perfil.sql)
en esa base. El script fue generado por EF Core para esta única migración,
utiliza una transacción y comprueba el historial para poder repetirse.
No elimina publicaciones ni modifica los puntajes históricos de Neo4j.

La prueba `PerfilPublicacionesIntegracionTests` ejecuta el SQL dos veces sobre
una base temporal y verifica que el perfil y el detalle vuelven a responder 200.
No se ha ejecutado este script en producción.

Después de aplicarlo, comprobar `GET /api/publicaciones/usuario/7` y el detalle
de una publicación existente. Si persiste el 500, continuar con las consultas
de interacciones de Neo4j y la excepción interna del servidor.
