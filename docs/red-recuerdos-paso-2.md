# Red de recuerdos: modelo de momentos (paso 2)

Esta entrega incorporó entidades, repositorio, servicio interno y migración. La navegación HTTP, la selección completa y las correcciones ya están documentadas en [Paso 3](red-recuerdos-paso-3.md). Las respuestas de lugar y día del paso 1 conservan su contrato. No hay procesamiento de IA ni carga automática de un catálogo en estas entregas.

## Entidades

| Entidad | Campos / propósito |
|---|---|
| `Momento` | Id, nombre (200 caracteres), descripción (2000), fecha inicial/final opcionales (`DateOnly`/`date`), fecha de creación UTC y padre opcional. |
| `MomentoAlias` | Id, MomentoId, texto original y normalizado (200 caracteres). Permite reconocer distintas formas de nombrar el mismo acontecimiento. |
| `PublicacionMomento` | Clave compuesta PublicacionId + MomentoId, origen, estado, confianza opcional, versión del análisis y fechas UTC de creación/actualización. |

Un recuerdo puede pertenecer a varios momentos. El vínculo al padre no se copia automáticamente: si un gol pertenece a un partido y ese partido al Mundial, el vínculo del recuerdo se guarda al acontecimiento que corresponda. `ObtenerHijos` consulta hijos directos; la consulta HTTP con descendientes pertenece al paso 3.

`OrigenAsociacionMomento`: Automatico = 0; Autor = 1. `EstadoAsociacionMomento`: Activo = 0; Rechazado = 1. Las consultas de momentos de una publicación solo incluyen vínculos activos a recuerdos originales visibles. Un rechazo conserva su fila para que el análisis no vuelva a imponer la asociación.

Las fechas de los acontecimientos no modifican `Publicacion.Fecha` ni restringen las asociaciones: un recuerdo de otra fecha puede hablar del mismo acontecimiento colectivo.

## Reglas y escrituras internas

`IMomentoServicio` ofrece:

- `Crear`: valida nombre, descripción y orden de fechas. Solo crea una entidad nueva; alias, hijos y asociaciones se agregan por sus operaciones correspondientes.
- `CambiarPadre`: permite asignar, cambiar o quitar el padre. El repositorio valida la cadena completa antes de guardar; no permite padre propio, ciclos o referencias inexistentes.
- `AgregarAlias`: conserva el texto original recortado; elimina tildes, convierte a minúsculas y unifica espacios en el texto normalizado. Es idempotente por momento y texto normalizado. Por ejemplo, `MÉXICO   86` y `México 86` producen `mexico 86`.
- `AsociarAutomaticamente`: requiere confianza finita entre 0 y 1 y versión de análisis no vacía (máximo 100 caracteres). Devuelve false si una decisión previa del autor impide el cambio.
- `AsociarPorAutor`: valida que el usuario indicado sea dueño del recuerdo; guarda origen Autor, sin confianza ni versión de IA. El futuro controller deberá obtener ese usuario de la autenticación, no de un Id enviado libremente por el cliente.
- `ObtenerMomentosPublicacion`: valida la visibilidad del recuerdo y obtiene sus asociaciones activas.

Los métodos de asociación no aceptan reposts; se trabaja explícitamente con el recuerdo original. La corrección completa, el rechazo por el autor y la preferencia para desactivar asociaciones automáticas se implementan en el paso 3. El estado rechazado y su protección ya están preparados y probados.

Un alias no es único globalmente: `Campeones` puede aparecer en acontecimientos de distintos años. No se fusionan momentos solo por ese texto. La detección semántica, las fusiones/canonicalización y las etiquetas temáticas quedan para las entregas posteriores.

## Consistencia y concurrencia

- PostgreSQL garantiza claves foráneas, unicidad de asociaciones y alias dentro de cada momento, enums válidos, confianza de 0 a 1, nombre/alias no vacíos y fechas ordenadas.
- El repositorio toma un bloqueo transaccional de PostgreSQL antes de leer y modificar la jerarquía. Evita ciclos incluso si dos peticiones proponen padres recíprocos simultáneamente. Los ciclos indirectos se validan en el repositorio: no escribir jerarquías con SQL manual saltando estas operaciones.
- Las asociaciones se serializan por publicación. El repositorio vuelve a leer el vínculo dentro del bloqueo y da prioridad a decisiones del autor, tanto activas como rechazadas. Repetir un análisis actualiza el vínculo automático existente sin duplicarlo ni cambiar su fecha de creación.
- Las operaciones de alias también se serializan para que un reintento concurrente no cree duplicados.
- Al eliminar una publicación se eliminan sus asociaciones. Eliminar un momento sin referencias elimina sus alias. No se permite borrar un momento que tenga hijos o publicaciones asociadas; no se eliminan recuerdos por borrar un acontecimiento.

Los tests de repositorio usan InMemory como los tests de publicaciones. Las restricciones físicas, los bloqueos, las carreras y los borrados se verifican adicionalmente contra PostgreSQL/PostGIS real en bases temporales aisladas.

## Migración y validación

`20261021141850_AgregarModeloMomentos` crea `momentos`, `momentos_alias` y `publicaciones_momentos`, sus restricciones e índices. No agrega momentos de ejemplo ni modifica fechas o contenido de publicaciones existentes.

```powershell
dotnet ef database update --project ProyectoFinalTPI.Backend.Repositorio --startup-project ProyectoFinalTPI.Backend
dotnet test ProyectoFinalTPI.Backend.Tests/ProyectoFinalTPI.Backend.Tests.csproj --filter "FullyQualifiedName~MomentoRepositorioTests|FullyQualifiedName~MomentoServicioTests|FullyQualifiedName~MomentosIntegracionTests"
```

Los tests usan nombres descriptivos en español y bloques `//Arrange`, `//Act` y `//Assert`. Para integración hace falta Docker Desktop con contenedores Linux; no se utiliza la base de desarrollo.

Cobertura de esta entrega: 17 casos de repositorio, 33 de servicio y 15 de integración (65 en total, contando las variantes de Theory). La verificación conjunta con los 52 casos del paso 1 pasó con 117 casos y ninguno omitido. Se verificó además que el modelo no tiene cambios sin migración.
