# Red de recuerdos: prevención y gestión de momentos duplicados (paso 7)

## Prevención al crear

Cada momento activo guardado con EF recibe una `ClaveIdentidad`: SHA-256 del nombre normalizado y las fechas inicial/final exactas, incluyendo la ausencia de fechas. Se ignoran mayúsculas, acentos, puntuación y diferencias de espacios en el nombre. El índice único impide dos claves iguales. El nombre visible no se modifica.

`MomentoRepositorio.Crear` reutiliza una identidad existente en lugar de insertar otra. También consulta alias compatibles con el período: los nombres de momentos fusionados siguen permitiendo reutilizar el canónico. Un alias ambiguo no se resuelve arbitrariamente: devuelve conflicto para revisión. Las fechas siguen siendo parte de la identidad; no se confunden automáticamente ediciones ni acontecimientos distintos de un mismo año.

El analizador resuelve tanto propuestas nuevas como IDs conocidos contra el catálogo canónico, dentro de la transacción de confirmación. Si el momento fue fusionado mientras Gemini respondía, su resultado se aplica al canónico actual. Se agrupan los IDs que terminan resolviendo al mismo momento y se conserva la confianza máxima, respetando las decisiones del autor. Un conflicto de identidad del catálogo termina el trabajo como fallido con código interno `catalogo_ambiguo`, sin repetir llamadas que requieren antes una revisión administrativa. Después de resolverlo, puede reprogramarse con el paso 6 incluyendo fallidos.

Esto evita duplicados deterministas y reduce la repetición mediante alias y candidatos del catálogo. **No garantiza equivalencia semántica global:** dos nombres distintos sin referencias compartidas todavía pueden representar el mismo acontecimiento. Las sugerencias son textuales y la fusión requiere una decisión administrativa; no se fusiona automáticamente por similitud ni se consulta a Gemini para administrar el catálogo.

## Modelo y migración

Migración: `20261021183413_AgregarIdentidadYFusionMomentos`.

- `ClaveIdentidad`, nullable e indexada como única.
- `VersionCatalogo`, comienza en 1 y controla cambios de metadatos del momento. Es un token de concurrencia de EF; no reemplaza `Publicacion.VersionRelaciones`.
- `MomentoCanonicoId`, referencia restrictiva al momento que conserva la identidad pública después de una fusión.

La migración no cambia nombres, no fusiona ni borra filas. Los momentos anteriores conservan inicialmente la clave nula para no asignar identidades incompatibles ni fallar ante duplicados ya presentes. Al buscar una identidad se comparan también esas filas mediante la misma normalización administrada. Si hay varias equivalentes, se bloquea la nueva alta hasta revisar/fusionar. Los momentos nuevos y los modificados con EF reciben su clave; una fusión asigna la clave al destino y libera la del origen.

Los campos se preparan en `SaveChanges`, no solamente en el servicio de creación. SQL manual y `ExecuteUpdate` no pasan por esa preparación; deben reservarse a operaciones controladas que mantengan las invariantes. La migración mantiene a propósito el catálogo legado, no lo depura de forma automática.

Aplicación:

```powershell
dotnet ef database update --project ProyectoFinalTPI.Backend.Repositorio --startup-project ProyectoFinalTPI.Backend
```

Al desplegar, actualizar y reiniciar todas las instancias que escriben momentos para que usen la nueva normalización, resolución y orden de bloqueos.

## Flujo administrativo

Los tres endpoints requieren JWT con `esAdmin=true` y sesión vigente. Están documentados con `SwaggerOperation` y `SwaggerResponse`. Sin sesión: 401. Sin condición de administrador: 403. Los errores de dominio usan ProblemDetails.

### 1. Buscar posibles duplicados

```http
GET /api/admin/momentos/1/duplicados?limite=20
Authorization: Bearer <TOKEN_ADMIN>
```

Devuelve hasta 50 candidatos (20 por defecto), ordenados por similitud descendente e Id ascendente. La comparación utiliza nombres y alias normalizados y descarta períodos incompatibles. Los motivos son `nombreOAliasCoincidente` o `textoSimilar`. El puntaje usa coincidencia de conjuntos de palabras y un umbral de 0,5; **no es una probabilidad de identidad**. La consulta compara metadatos locales del período compatible y no modifica datos. Los alias genéricos pueden dar sugerencias que deben descartarse manualmente.

La búsqueda de candidatos no impide indicar explícitamente otro destino encontrado mediante el catálogo público. No limita la revisión a los primeros resultados sugeridos.

### 2. Revisar la fusión

```http
GET /api/admin/momentos/1/fusion?destinoId=2
Authorization: Bearer <TOKEN_ADMIN>
```

```json
{
  "origen": { "id": 1, "nombre": "México 86", "fechaInicio": "1986-01-01", "fechaFin": "1986-12-31", "versionCatalogo": 1 },
  "destino": { "id": 2, "nombre": "Argentina campeón 1986", "fechaInicio": "1986-01-01", "fechaFin": "1986-12-31", "versionCatalogo": 1 },
  "publicacionesAfectadas": 2,
  "decisionesEnConflicto": 1,
  "hijosATrasladar": 0,
  "yaFusionado": false
}
```

Es una vista previa de solo lectura. Un conflicto entre decisiones significa que una publicación tiene aceptación del autor en un momento y rechazo del autor en el otro. La cantidad de publicaciones es la cantidad de vínculos del origen, incluidos los rechazados; no significa que todas vayan a verse en el feed.

El destino conserva su nombre, descripción, fechas y padre. La selección debe representar el mismo acontecimiento; un torneo y una final no se vuelven equivalentes por compartir palabras.

### 3. Confirmar

```http
POST /api/admin/momentos/1/fusion
Authorization: Bearer <TOKEN_ADMIN>
Content-Type: application/json

{
  "destinoId": 2,
  "versionOrigen": 1,
  "versionDestino": 1
}
```

```json
{
  "origenId": 1,
  "destinoId": 2,
  "publicacionesAfectadas": 2,
  "decisionesEnConflicto": 1,
  "versionDestino": 2,
  "yaFusionado": false
}
```

Devuelve 200 al confirmar. Las versiones deben ser las de la vista previa. Si cambian los metadatos del catálogo antes de confirmar, devuelve 409 y se debe recargar la vista previa. Los alias pueden enriquecerse entre consultas; la fusión conserva los alias actuales, sin descartarlos por haber llegado después de la revisión.

La fusión:

1. Traslada los vínculos del origen al destino, sin repetir `(PublicacionId, MomentoId)`.
2. Ante colisiones, prioriza rechazo del autor, luego aceptación del autor y finalmente vínculo automático activo de mayor confianza. Un rechazo manual no se convierte en aceptación por una fusión.
3. Conserva el nombre anterior y los alias en el destino, sin repetir el mismo alias normalizado por momento. La fila histórica conserva sus metadatos.
4. Reasigna los hijos activos al destino, que conserva su propio padre. No permite fusionar un ancestro con su descendiente ni momentos con años/períodos incompatibles.
5. Conserva el origen con una redirección al destino y actualiza redirecciones previas hacia el nuevo canónico.
6. Incrementa una vez `VersionRelaciones` por publicación afectada. No modifica su contenido, `VersionContenido`, preferencia de automatización ni genera nuevos trabajos de análisis.

Una repetición hacia el mismo canónico devuelve `yaFusionado=true`, sin repetir movimientos ni incrementar versiones. Un origen ya fusionado en otro destino devuelve 409; no se mueve ese canónico implícitamente. No hay endpoint para deshacer la fusión.

La confirmación usa una sola transacción. Se libera la identidad del origen antes de indexar un destino legado, pero ambas modificaciones, los alias y los vínculos quedan dentro de la misma transacción. Un fallo revierte todo. Los logs registran administrador, IDs y cantidades, sin texto de publicaciones ni credenciales.

## URLs y correcciones existentes

`GET /api/momentos/{idAnterior}` devuelve **200 con el momento canónico**, cuyo `id` puede ser distinto al solicitado, y `redirigidoDesdeId` indica el Id anterior. No es una redirección HTTP. El detalle incluye `versionCatalogo`.

`GET /api/momentos/{idAnterior}/publicaciones` consulta las publicaciones del canónico, incluida su jerarquía si corresponde. El listado y la búsqueda pública excluyen filas fusionadas. Los alias copiados permiten encontrar al canónico por nombres anteriores. El catálogo enviado a Gemini también excluye filas fusionadas.

Una corrección del autor enviada con la versión de relaciones anterior a una fusión recibe 409. Al recargar, el frontend obtiene IDs y versión actuales. Si una selección nueva todavía incluye un Id anterior, se resuelve al canónico; elegir origen y destino simultáneamente no crea dos vínculos. El autor puede aceptar explícitamente el canónico después de un rechazo conservado durante la fusión.

## Consistencia y concurrencia

Las escrituras de asociaciones, el análisis y las fusiones adquieren el bloqueo de catálogo antes de los bloqueos por publicación. Ese orden evita invertir bloqueos cuando una fusión toca varios recuerdos. Las fusiones se serializan, y las correcciones y altas revalidan su estado después del bloqueo. Dos altas con la misma identidad reutilizan un solo Id, respaldadas además por el índice único.

Este alcance prioriza consistencia del catálogo: el bloqueo de catálogo serializa las confirmaciones que modifican momentos o asociaciones. Las llamadas a Gemini ocurren fuera de la transacción; las lecturas públicas no esperan ese bloqueo de aplicación. Si el volumen aumenta, se puede refinar la granularidad conservando el mismo orden y las invariantes.

## Verificación

Tests de repositorio, servicio y controller con nombres en español y Arrange/Act/Assert. Las integraciones utilizan PostgreSQL/PostGIS temporal y analizadores ficticios. Se verifica identidad normalizada, índice único, altas concurrentes, permisos, vistas previas, conservación de rechazos, repetición, IDs anteriores, versiones obsoletas, períodos incompatibles, datos legados sin clave, rollback y un análisis que responde después de la fusión.

La migración por sí sola no requiere llamadas a Gemini ni reprocesar las publicaciones existentes.
