# Red de recuerdos: lugar y día

## Consultas

- `GET /api/publicaciones/{id}/relaciones`: bloques `dia` y `lugar`, hasta seis tarjetas cada uno, con `hayMas` y `siguienteCursor`.
- `GET /api/publicaciones/{id}/relacionadas?tipo=dia&limite=20`: misma fecha completa y hasta 200 metros.
- `GET /api/publicaciones/{id}/relacionadas?tipo=lugar&limite=20`: hasta 200 metros, cualquier fecha.

El límite admite valores entre 1 y 50. Para continuar, enviar `siguienteCursor` en `cursor`, codificado como parámetro de URL. No reutilizarlo con otra publicación o tipo. Los bloques pueden compartir tarjetas: el cliente puede deduplicar su presentación, pero las consultas conservan todas las coincidencias de cada criterio.

Las tarjetas incluyen título, descripción breve, fecha, multimedia, autor, nombre del lugar, distancia en metros y las coincidencias `mismoDia`/`mismoLugar`. La distancia se devuelve sin redondear para mantener la precisión del cursor; el cliente puede redondearla al mostrarla.

## Reglas

Orden ascendente por distancia y luego Id. Se excluyen ocultos, el propio recuerdo y reposts. Si la consulta parte de un repost visible, se usa su original visible; `publicacionId` del resumen identifica ese original. Si el origen o su original están ocultos o no existen, devuelve 404. Parámetros inválidos devuelven 400 con ProblemDetails. No requiere sesión.

Cada radio parte de la ubicación del recuerdo consultado. La cercanía no es transitiva. Los conteos y cursores no representan una instantánea: ocultamientos o modificaciones entre páginas pueden alterar los resultados.

## Fechas y compatibilidad

Se conserva `Publicacion.Fecha` (`DateTime`/`timestamp with time zone`) y el contrato existente de fechas UTC. Mismo día significa igualdad de la fecha calendario **en UTC**, incluyendo año. La conversión SQL explicita UTC y no depende del huso de la sesión PostgreSQL. No se modifican ni se reinterpretan fechas históricas.

La migración a `DateOnly`/`date` del plan general queda diferida: requiere auditar las convenciones de carga de fechas del frontend y adaptar filtros/DTO existentes. Esta entrega no introduce ese cambio de contrato.

## Migración y ejecución

`AgregarIndicesRedRecuerdos` crea un índice GiST sobre la conversión de coordenadas a geography, un índice sobre el día UTC y un índice sobre Fecha. La consulta preselecciona con `ST_DWithin(..., 200.000001)` y aplica el filtro definitivo `ST_Distance(...) <= 200`, en geography y metros. El margen de un micrómetro solo se usa para preseleccionar y evita que diferencias numéricas entre ambas funciones excluyan el borde; la distancia devuelta y el cursor siguen limitados a 200 m. Los índices por expresión se mantienen explícitamente en Up/Down de la migración.

```powershell
dotnet ef database update --project ProyectoFinalTPI.Backend.Repositorio --startup-project ProyectoFinalTPI.Backend
dotnet test ProyectoFinalTPI.Backend.Tests/ProyectoFinalTPI.Backend.Tests.csproj --filter FullyQualifiedName~RelacionesRecuerdosIntegracionTests
```

Las pruebas crean un contenedor PostGIS temporal y bases aisladas; nunca usan la base de desarrollo. Verifican proximidad a ambos lados del límite, ausencia de transitividad, igualdad de fecha pese a horas diferentes, año distinto, ocultos/reposts, desempates, paginación y validación de cursores. El servicio actual de relaciones no consulta Neo4j.

El caso `QueSeIncluyanLosRecuerdosAExactamenteDoscientosMetrosYSePuedanPaginar` construye dos ubicaciones con `ST_Project(..., 200, ...)`, comprueba que PostGIS devuelve una distancia de exactamente 200 m, verifica ambos bloques y continúa la paginación entre esos dos recuerdos. Un recuerdo a 200.001 m queda excluido.

## Tests por capa

Se sigue el patrón de los tests de publicaciones: nombres `QueSe...` en español, bloques `//Arrange`, `//Act`, `//Assert`, fakes propios para dependencias e InMemory con una base única por test de repositorio.

- `RelacionRecuerdosRepositorioTests`: 7 casos para origen visible, oculto, inexistente y resolución de reposts. InMemory no ejecuta el SQL espacial; esa consulta conserva sus pruebas con PostGIS real.
- `RelacionRecuerdosServicioTests`: 31 casos, contando las variantes de Theory, sobre bloques, límites, validación, paginación, cursores, resolución al original y propagación de cancelación.
- `RelacionesRecuerdosControllerTests`: 10 casos, contando las variantes de Theory, sobre respuestas, envío de parámetros y cancelación al servicio, valores predeterminados y errores ProblemDetails mediante el helper de middleware existente.

```powershell
dotnet test ProyectoFinalTPI.Backend.Tests/ProyectoFinalTPI.Backend.Tests.csproj --filter "FullyQualifiedName~RelacionRecuerdosRepositorioTests|FullyQualifiedName~RelacionRecuerdosServicioTests|FullyQualifiedName~RelacionesRecuerdosControllerTests"
```
