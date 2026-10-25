# Logros y configuración local

El frontend consulta `GET /api/usuario/{id}/perfil` (o `perfil/{username}`), con el JWT en `Authorization: Bearer …`. La respuesta incluye el catálogo real en `logros`: identificador, título, descripción, icono, puntos, `desbloqueado` y `fechaDesbloqueo`. Nunca se serializan las entidades de EF ni sus referencias circulares. El perfil de un usuario solo marca los logros guardados para ese usuario.

## Reglas conectadas

- **Rachas:** registro, login válido y renovación de sesión registran el ingreso. Cuenta una vez por día calendario UTC, suma si el último ingreso fue ayer y reinicia en 1 si hubo días intermedios. Los umbrales son 3 y 7 días. Un login fallido no cuenta. La respuesta de autenticación incluye `logrosNuevos` para poder anunciar una racha que se acaba de conseguir sin repetir logros históricos.
- **Cronista Local:** el POST de publicaciones admite `GpsLatitud` y `GpsLongitud` opcionales, siempre como par. Se trata de la ubicación actual del usuario, distinta de las coordenadas del recuerdo. El controlador la envía al servicio para verificar los 5 recuerdos cercanos. Sin GPS se permite publicar, pero no se comprueba ese logro. Se conserva el criterio de cercanía existente de 0,01 grados.
- **Lince de las Praderas:** se otorga al alcanzar 15 publicaciones visibles, conforme a su descripción.
- **Puntuaciones:** `PUT /api/publicaciones/{id}/puntuacion`, autenticado y con `{ "puntaje": 1..5 }`, devuelve `miPuntuacion` y `puntosTotales`. Repetir la misma puntuación la quita. Generoso requiere 50 puntos otorgados; Popular y Leyenda requieren 20 y 100 puntos recibidos por una publicación. El autor recibe esos logros, no la persona que puntúa. No se permite puntuar la publicación propia.

Estos cambios utilizan las columnas existentes y no agregan una migración nueva. La prueba `LogrosReglasIntegracionTests` comprueba días repetidos y salteados, los límites 14/15 publicaciones, GPS incompleto o ausente, y los límites 19/20, 99/100 y 49/50 puntos. Usa PostgreSQL/PostGIS y Neo4j temporales reales; solo reemplaza la subida multimedia a Cloudinary por el adaptador de prueba existente.

La rama `logros` necesitaba registrar `ILogroServicio`, quitar el registro del servicio de recomendaciones inexistente y agregar la migración `CompletarConfiguracionLogros`. Esta migración incorpora las columnas de racha, inicializa la racha de usuarios existentes e inserta los 20 logros del modelo. No requiere borrar la base. Si ya creaste manualmente esas columnas o esos 20 registros, revisá la diferencia con tu esquema antes de aplicar la migración; la prueba local se hizo con el historial de migraciones del repositorio.

La migración posterior `RetirarLogrosSinImplementacion` elimina «Melómano» (ID 4), «Turista del Pasado» (ID 5) y «Mecenas» (ID 17), junto con sus desbloqueos si existían. Conserva los usuarios, los otros desbloqueos y los IDs restantes; el catálogo queda en 17 logros. No se deben editar las migraciones anteriores ni borrar la base: aplicá las migraciones pendientes con el comando siguiente.

Desde la raíz del backend, con PostgreSQL/PostGIS, Neo4j y la configuración local del README preparados:

```sh
dotnet restore
dotnet build
dotnet ef database update --project ProyectoFinalTPI.Backend.Repositorio --startup-project ProyectoFinalTPI.Backend --context ApplicationDbContext
dotnet run --project ProyectoFinalTPI.Backend --no-launch-profile
```

Usá `Development` como `ASPNETCORE_ENVIRONMENT` y `DOTNET_ENVIRONMENT` y configurá `ASPNETCORE_URLS=http://localhost:5199` si el frontend apunta a ese puerto. Aplicá las migraciones con la cadena `ConnectionStrings__PostgresConnection` de tu base local (o el User Secret equivalente), nunca con una conexión de producción para esta prueba.

## Cloudinary

Cloudinary es un servicio externo. El SDK ya se instala con `dotnet restore`; no hay que instalar un servidor Cloudinary. Creá una cuenta y obtené **Cloud name**, **API key** y **API secret** desde su consola. No compartas el secret en el chat ni lo guardes en Git.

En tu computadora podés abrir **Manage User Secrets / Administrar secretos de usuario** en el proyecto web desde Visual Studio, o ejecutar desde la raíz del backend estos comandos reemplazando los marcadores con tus propios valores:

```sh
dotnet user-secrets set "Cloudinary:CloudName" "TU_CLOUD_NAME" --project ProyectoFinalTPI.Backend
dotnet user-secrets set "Cloudinary:ApiKey" "TU_API_KEY" --project ProyectoFinalTPI.Backend
dotnet user-secrets set "Cloudinary:ApiSecret" "TU_API_SECRET" --project ProyectoFinalTPI.Backend
```

El historial del shell puede conservar los comandos: no lo compartas. No ejecutes `user-secrets init`: el proyecto ya tiene identificador. Reiniciá el backend en `Development` después de configurar estos valores. No hace falta modificar `appsettings.json`.

En un despliegue usá el gestor de secretos de ese servicio con `Cloudinary__CloudName`, `Cloudinary__ApiKey` y `Cloudinary__ApiSecret`. No son variables `NEXT_PUBLIC_`: pertenecen exclusivamente al backend.

El cliente de Cloudinary se inicializa cuando se solicita una carga de archivos. Por eso mapa, feed, perfil, comentarios y lectura de logros funcionan sin esas credenciales. Las subidas siguen exigiendo una cuenta válida. No se verificó una carga real porque este entorno no tiene esas credenciales.

## Verificación

```sh
dotnet test --filter 'Categoria!=Integracion'
dotnet test --filter 'Categoria=Integracion'
```

La integración requiere Docker y crea bases temporales. Las pruebas de logros comprueban el catálogo, el estado por usuario y que otorgar dos veces el mismo logro no lo duplique. El recorrido de navegador está en el repositorio frontend: `tests/profile-real.spec.ts`.
