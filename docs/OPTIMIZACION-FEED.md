# Optimización del feed

Rama: `Optimizacion-del-feed`. Estos cambios no agregan tablas ni requieren una migración nueva.
Para ejecutar la aplicación local, PostgreSQL debe tener aplicadas las migraciones existentes de main.
No se modificaron User Secrets ni se actualizaron las bases locales o las de Azure.

## Qué cambió

- `GeneradorRankingFeed` arma el ranking con ID, categoría, coordenadas y fecha. Los textos,
  autores, grupos y contadores de comentarios se consultan al construir la página.
- Las semillas se seleccionan en Neo4j con ventana y límite. Las exclusiones se verifican
  por candidata contra todos sus likes y puntuaciones, aunque sean antiguos o bajos.
- Las recomendaciones reciben el mismo conjunto de semillas y limitan vecinos y publicaciones
  por vecino. Se eliminó la combinación intermedia de todos los likes con todas las puntuaciones.
- La colección final de candidatas queda acotada. Las consultas generales avanzan por cursor
  con un máximo de lotes. Las vistas se buscan solamente si falta contenido.
- La afinidad de categoría y cercanía conserva sus pesos. Los lugares repetidos se deduplican.
- Cada página completa likes, puntuaciones, reposts y guardados mediante una consulta a Neo4j
  con agregaciones separadas. Las impresiones solo consultan los IDs visibles en PostgreSQL.
- Las sesiones conservan claves y formato Redis, orden estable y vencimiento absoluto de 30 minutos.
  Su interfaz es asíncrona y recibe cancelación.
- Las solicitudes simultáneas de un usuario comparten una generación dentro de una instancia.
  Cada generación usa un scope propio; cancelar un cliente no dispone el contexto de los otros.
  Una recarga posterior genera un ranking nuevo. No se comparte este control entre instancias.
- Seguidos pagina también los reposts y desempata por fecha, publicación y reposteador.
  Los cursores anteriores siguen siendo legibles. El orden considera la precisión de fechas de ambas bases.

## Límites configurables

Se conservan las variables existentes. No hace falta agregar variables en Azure para usar los valores predeterminados.
La sección es `FeedRecomendaciones`; en variables de entorno se usa `FeedRecomendaciones__Nombre`.

| Nombre | Predeterminado |
|---|---:|
| CantidadMaximaCandidatas | 500 |
| CantidadMaximaSemillas | 100 |
| DiasDeHistorialParaSemillas | 180 |
| VecinosPorSemilla | 20 |
| PublicacionesPorVecino | 25 |
| PorcentajeCandidatasColaborativas | 60 |
| CantidadMaximaLotesGenerales | 4 |
| CantidadMaximaLotesSeguidos | 4 |
| TiempoMaximoGeneracionSegundos | 30 |
| DiasDeExclusionDeImpresiones | 14 |

El porcentaje reserva el cupo inicial de selección colaborativa, no garantiza una distribución
porcentual en el resultado. Los límites de historial y exploración cambian el conjunto de
recomendaciones: hay que comprobar relevancia, diversidad y cuentas con poca actividad.
Las relaciones antiguas sin fecha pueden servir de semillas, siempre dentro del máximo.

Las consultas de lectura nuevas de Neo4j tienen un timeout de transacción de 30 segundos por defecto,
configurable con `Neo4j__TiempoMaximoConsultaFeedSegundos` (de 1 a 120). Este tiempo también
incluye la preparación de la consulta en Neo4j: el primer plan puede tardar más que las siguientes ejecuciones.
La selección de semillas usa un único recorrido de likes/puntuaciones y una agregación, sin CALL/UNION.
El plazo de generación solicita cancelación; red y reintentos del driver pueden agregar demora.
LIMIT reduce resultados y expansiones posteriores, pero no garantiza un costo constante de
buscar/ordenar relaciones de alta cardinalidad. Falta comprobar planes e índices reales.

Si se agota la exploración, Para vos puede tener menos de 500 IDs. La prioridad de nunca vistas
se aplica a lo explorado; no se recorre todo el catálogo para asegurar que no quede otra sin ver.
En Seguidos, muchos reposts ocultos pueden producir una página parcial o vacía con siguiente cursor.
El cliente debe seguir `siguienteCursor` y no interpretar una página vacía como fin automático.

## Comprobaciones preparadas

Las pruebas unitarias conservan mocks, nombres `Que...` y Arrange/Act/Assert. Las pruebas de
integración usan las fixtures existentes, categoría Integracion y bases desechables creadas por
Testcontainers. Sus conexiones no se toman de User Secrets. No usar bases reales para ellas.

Ejecutar desde Visual Studio o desde la raíz del repositorio:

```powershell
# Sin Docker: mocks, EF en memoria y pruebas de comportamiento.
dotnet test ProyectoFinalTPI.Backend.Tests --filter "Categoria!=Integracion"

# Con Docker Desktop abierto: consultas Cypher, proyecciones SQL y Redis reales.
dotnet test ProyectoFinalTPI.Backend.Tests --filter "FullyQualifiedName~FeedNeo4jIntegracionTests|FullyQualifiedName~FeedPostgresIntegracionTests|FullyQualifiedName~RedisIntegracionTests"
```

Verificar también desde el frontend: primera página y continuación, recarga, publicaciones
ocultas durante una sesión, guardados/reposts en páginas intermedias, usuario nuevo y Seguidos
con varios reposts. Las pruebas quedaron preparadas para ejecución por el usuario.

## Medición y despliegue

Se conservan `feed.etapa.duracion` y las etapas principales. Se agregan etapas de semillas,
consultas colaborativas/generales, estados de candidatas y resúmenes, más `feed.elementos`
para cantidades de semillas, candidatas y publicaciones generales exploradas. No hay IDs de
usuarios como dimensiones. Los medidores existentes ya están registrados en Application Insights.

Antes de integrar: ejecutar pruebas, revisar planes con datos representativos, comprobar las
restricciones únicas de Usuario/Publicacion por postgresId en Neo4j y los índices de PostgreSQL.
Comparar tiempos de primera página y continuación por separado, cantidad de datos procesados y
calidad de recomendaciones. No se ejecutaron benchmarks ni se demostró una mejora en segundos.
No se agregaron índices a ciegas ni se cambió la configuración de Azure.
