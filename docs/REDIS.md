# Redis

Alcance: sesiones del feed y pines del mapa (30 minutos), información pública del perfil (10 minutos) y resúmenes de publicaciones (2 minutos). Las publicaciones de los perfiles y el catálogo de momentos quedan fuera. Los vencimientos de las lecturas públicas se definen en `CacheLecturasPublicas.DuracionPara`.

El backend usa los contratos `ISesionFypRepositorio`, `IPerfilPublicoConsulta`, `IResumenPublicacionConsulta` e `ICacheLecturasPublicas`. No se serializan credenciales de usuarios.

- `UsuarioServicio` cachea el `UsuarioPerfilDto` por ID y por username: nombre, apellido, foto, tipo, reputación, puntos, cantidades de publicaciones/seguidores/seguidos y catálogo de logros con su estado. `LoSigo` se completa en cada petición según el visitante y no se comparte entre usuarios. La lista de publicaciones del perfil sigue consultándose por separado, sin caché.
- `PublicacionRepositorio.ObtenerPines` cachea todos los `PublicacionPinDto` de publicaciones visibles y originales en una única entrada `mapa:pines:v1`. Incluye internamente la fecha completa para filtrar por rangos exactos. El área, la categoría y las fechas se filtran sobre esa copia, conservando el orden por ID y el límite de 200 pines más `HayMas` en la respuesta. La primera consulta después de vencer o invalidar la entrada vuelve a cargar el conjunto completo desde PostgreSQL.

## Configuración

Fuera de Compose, Redis está desactivado por defecto. Para habilitarlo:

```text
Redis__Habilitado=true
Redis__ConnectionString=localhost:6379,abortConnect=false,connectTimeout=1000,syncTimeout=1000,asyncTimeout=1000
```

En Compose se utiliza `redis:6379`. El puerto no se publica al host. Redis tiene volumen propio, AOF y límite de 128 MB con `noeviction`, para no descartar sesiones activas bajo presión de memoria. Al llenarse, las nuevas escrituras pueden fallar: las lecturas públicas recurren a PostgreSQL y crear una sesión de feed devuelve 503.

En Azure, la conexión depende del recurso disponible: `localhost:6379` para un sidecar; dirección, TLS y autenticación correspondientes para un servidor compartido. Configurar valores secretos en Azure, nunca en Git. Un sidecar no comparte sesiones entre instancias de App Service.

## Consistencia

Los cambios de usuarios, publicaciones, lugares, logros y logros obtenidos mediante EF invalidan una generación global de lecturas públicas. Seguir o dejar de seguir también invalida esa generación después de modificar la relación en Neo4j. Es una estrategia inicial conservadora: también invalida perfiles y pines no afectados. Las sesiones del feed no se invalidan. Las claves de generaciones anteriores dejan de usarse inmediatamente y se eliminan al cumplir su vencimiento original: 30 minutos para pines, 10 para perfiles y 2 para resúmenes. Una lectura concurrente conserva su generación original y no repuebla la nueva.

La existencia y visibilidad de una publicación se consultan antes de devolver su resumen cacheado. Las consultas de perfil por username resuelven el nombre en PostgreSQL solo cuando no hay una entrada válida; cambiar el username mediante EF invalida también las entradas del nombre anterior. Cambios hechos mediante SQL externo no disparan el interceptor; las copias públicas pueden permanecer hasta su vencimiento. Si falla una invalidación por caída de Redis, se aplica el vencimiento correspondiente a cada tipo de entrada. No ofrece consistencia transaccional entre PostgreSQL y Redis; no usar estas copias para autorización.

Un Redis caído produce fallback a la base en las lecturas públicas. Para sesiones de feed devuelve 503; una sesión ausente o vencida conserva el comportamiento existente de reiniciar paginación. No se reconstruye otro orden bajo el mismo cursor.
