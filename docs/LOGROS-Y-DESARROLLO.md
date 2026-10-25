# Logros y configuración local

El frontend consulta `GET /api/usuario/{id}/perfil` (o `perfil/{username}`), con el JWT en `Authorization: Bearer …`. La respuesta incluye el catálogo real en `logros`: identificador, título, descripción, icono, puntos, `desbloqueado` y `fechaDesbloqueo`. Nunca se serializan las entidades de EF ni sus referencias circulares. El perfil de un usuario solo marca los logros guardados para ese usuario.

La rama `logros` necesitaba registrar `ILogroServicio`, quitar el registro del servicio de recomendaciones inexistente y agregar la migración `CompletarConfiguracionLogros`. Esta migración incorpora las columnas de racha, inicializa la racha de usuarios existentes e inserta los 20 logros del modelo. No requiere borrar la base. Si ya creaste manualmente esas columnas o esos 20 registros, revisá la diferencia con tu esquema antes de aplicar la migración; la prueba local se hizo con el historial de migraciones del repositorio.

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
