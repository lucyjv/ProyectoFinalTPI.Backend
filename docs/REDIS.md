# Redis

Alcance: sesiones del feed (30 minutos), datos públicos del perfil y resúmenes de publicaciones (2 minutos). Mapa y catálogo de momentos quedan fuera.

El backend usa los contratos `ISesionFypRepositorio`, `IPerfilPublicoConsulta` e `IResumenPublicacionConsulta`. Las implementaciones de infraestructura gestionan Redis y EF; los servicios conservan sus operaciones y completan contadores, logros y estados del visitante en cada petición. No se serializan credenciales de usuarios.

## Configuración

Fuera de Compose, Redis está desactivado por defecto. Para habilitarlo:

```text
Redis__Habilitado=true
Redis__ConnectionString=localhost:6379,abortConnect=false,connectTimeout=1000,syncTimeout=1000,asyncTimeout=1000
```

En Compose se utiliza `redis:6379`. El puerto no se publica al host. Redis tiene volumen propio, AOF y límite de 128 MB con `noeviction`, para no descartar sesiones activas bajo presión de memoria. Al llenarse, las nuevas escrituras pueden fallar: las lecturas públicas recurren a PostgreSQL y crear una sesión de feed devuelve 503.

En Azure, la conexión depende del recurso disponible: `localhost:6379` para un sidecar; dirección, TLS y autenticación correspondientes para un servidor compartido. Configurar valores secretos en Azure, nunca en Git. Un sidecar no comparte sesiones entre instancias de App Service.

## Consistencia

Los cambios de usuarios y publicaciones mediante EF invalidan una generación global de lecturas públicas. Es una estrategia inicial conservadora: también invalida perfiles no afectados. Las sesiones del feed no se invalidan. Las claves de generaciones anteriores vencen a los dos minutos. Una lectura concurrente conserva su generación original y no repuebla la nueva.

La existencia y visibilidad de una publicación se consultan antes de devolver su resumen cacheado. Las consultas por username resuelven el nombre actual en PostgreSQL. Cambios hechos mediante SQL externo no disparan el interceptor; las copias públicas pueden permanecer hasta su vencimiento. Si falla una invalidación por caída de Redis, también se aplica ese límite de dos minutos. No ofrece consistencia transaccional entre PostgreSQL y Redis; no usar estas copias para autorización.

Un Redis caído produce fallback a la base en las lecturas públicas. Para sesiones de feed devuelve 503; una sesión ausente o vencida conserva el comportamiento existente de reiniciar paginación. No se reconstruye otro orden bajo el mismo cursor.
