# ProyectoFinalTPI.Backend

## Cobertura de tests

Coverlet recolecta la cobertura y ReportGenerator genera un informe HTML. Desde la raíz:

```powershell
dotnet tool restore
.\coverage.ps1
```

Abrir `coverage-report/index.html` para ver la cobertura por archivo y línea. Para ejecutar solo un grupo de tests:

```powershell
.\coverage.ps1 -Filter 'FullyQualifiedName~PublicacionServicioTests'
```

El informe refleja únicamente los tests ejecutados. La suite completa requiere Docker para las pruebas de integración con PostgreSQL/PostGIS. Los resultados se guardan en `TestResults/` y `coverage-report/`, excluidos de Git.

Este es el repositorio del Backend para nuestro **Proyecto Final TPI**. La solución está desarrollada en **ASP.NET Core 9 (MVC)** bajo una arquitectura de **Persistencia Políglota**, combinando bases de datos relacionales, espaciales y orientadas a grafos para lograr el máximo rendimiento en nuestra red social.

---

## Base de datos

*   **PostgreSQL 17:** Almacena los datos maestros, perfiles de usuario y datos estructurados.
*   **PostGIS Extension:** Motor geoespacial acoplado a Postgres. Maneja coordenadas, puntos geográficos reales (`Point`) y búsquedas eficientes por radio de cercanía.
*   **Neo4j (Community Edition):** Base de datos de grafos. Gestiona relaciones de la red, como seguimientos y usuarios etiquetados en publicaciones.

---

## Requisitos Previos 

Antes de levantar la aplicación .NET:

### 1. PostgreSQL + PostGIS
1. Descargar e instalar **PostgreSQL 17** (o superior) desde la [Página Oficial](https://postgresql.org).
2. Durante la instalación, configurar el usuario administrador `postgres` con una contraseña local y utilizarla en `ConnectionStrings:PostgresConnection` de User Secrets.
3. Asegurarse de que el motor esté corriendo en el puerto por defecto `5432`.
4. El soporte espacial **PostGIS** se activará de forma automática al impactar las migraciones.

### 2. Neo4j Server (Portable)
1. Descargar la versión **Neo4j Community Server** en archivo ZIP.
2. Descomprimir el ZIP de Neo4j en una ruta cómoda (ej: `C:\Neo4j\`).
3. Abrir una consola, navegar hasta la carpeta de Neo4j y encender el servidor con:
   ```bash
   bin\neo4j-admin server console
   ```
   *(Mantener esta consola abierta mientras se pruebe la aplicación)*.
4. Entrar al navegador a [http://localhost:7474](http://localhost:7474) (Username: `neo4j`), e introducir la contraseña local configurada en `Neo4j:Password` de User Secrets.

---

## Configuración del Proyecto en .NET 9

### 1. Credenciales locales con User Secrets

Cada integrante debe configurar las credenciales en su propia computadora y usuario del sistema. No se descargan al clonar el repositorio. `appsettings.json` conserva la configuración compartida; las credenciales de desarrollo se guardan fuera del repositorio mediante **ASP.NET Core User Secrets**.

#### Configuración inicial

1. Instalar el **SDK de .NET 9** y comprobarlo con `dotnet --list-sdks`.
2. Solicitar al responsable del equipo los datos de Cloudinary por un canal privado. Para PostgreSQL y Neo4j, utilizar las credenciales de las bases locales configuradas en los requisitos previos. No publicar credenciales en GitHub, capturas ni mensajes públicos.
3. Abrir **PowerShell en la raíz del repositorio**, donde se encuentra este README. Restaurar los paquetes:

   ```powershell
   dotnet restore
   ```

4. Reemplazar los valores de ejemplo y ejecutar los cinco comandos siguientes. La cadena de PostgreSQL debe incluir el nombre de la base, el usuario y la contraseña locales:

   ```powershell
   dotnet user-secrets set 'Cloudinary:CloudName' 'REEMPLAZAR_CLOUD_NAME' --project './ProyectoFinalTPI.Backend/ProyectoFinalTPI.Backend.csproj'
   dotnet user-secrets set 'Cloudinary:ApiKey' 'REEMPLAZAR_API_KEY' --project './ProyectoFinalTPI.Backend/ProyectoFinalTPI.Backend.csproj'
   dotnet user-secrets set 'Cloudinary:ApiSecret' 'REEMPLAZAR_API_SECRET' --project './ProyectoFinalTPI.Backend/ProyectoFinalTPI.Backend.csproj'
   dotnet user-secrets set 'ConnectionStrings:PostgresConnection' 'Host=localhost;Port=5432;Database=REEMPLAZAR_BASE;Username=postgres;Password=REEMPLAZAR_PASSWORD_POSTGRES' --project './ProyectoFinalTPI.Backend/ProyectoFinalTPI.Backend.csproj'
   dotnet user-secrets set 'Neo4j:Password' 'REEMPLAZAR_PASSWORD_NEO4J' --project './ProyectoFinalTPI.Backend/ProyectoFinalTPI.Backend.csproj'
   ```

   Los valores son ejemplos, no credenciales válidas. El proyecto ya tiene `UserSecretsId`: **no ejecutar `dotnet user-secrets init`**. Para actualizar una credencial, repetir el comando correspondiente con el nuevo valor. Los comandos con valores reales pueden quedar en el historial de la terminal; no compartirlo.

#### Ejecución y verificación

Después de completar la sincronización de la base de datos de la siguiente sección, iniciar PostgreSQL y Neo4j y ejecutar desde la raíz del repositorio:

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project './ProyectoFinalTPI.Backend/ProyectoFinalTPI.Backend.csproj' --no-launch-profile
```

Comprobar que el inicio indique `Hosting environment: Development` y abrir la dirección indicada en `Now listening on`. Verificar una operación que consulte PostgreSQL, una que utilice Neo4j y una carga de imagen a Cloudinary: el inicio de la aplicación por sí solo no valida todas las credenciales. No compartir la salida de `dotnet user-secrets list`, ya que muestra los valores completos.

#### Si la configuración no funciona

- **Valores vacíos o credenciales no encontradas:** comprobar que los comandos apuntan al proyecto web indicado y se ejecutaron con el mismo usuario del sistema que inicia la aplicación. User Secrets se carga automáticamente en `Development` mediante `WebApplication.CreateBuilder`.
- **Errores de conexión o autenticación:** comprobar que las bases estén encendidas y que los datos coincidan con la instalación local. Para Cloudinary, comprobar que los tres valores pertenezcan a la misma cuenta.
- **Un valor anterior sigue activo:** revisar si existen variables de entorno con los nombres de la tabla siguiente; tienen prioridad sobre User Secrets. Reiniciar la aplicación después de cambiar la configuración.

#### Producción y almacenamiento

User Secrets es solo para desarrollo y **no cifra los valores**. No copiar estos datos a `appsettings.json` ni a archivos del repositorio. `.env` está ignorado por Git, pero el arranque actual no lo carga automáticamente.

En producción, configurar las credenciales mediante el mecanismo de secretos del entorno de despliegue, con estos nombres de variables de entorno:

| Clave de User Secrets | Variable de entorno para producción |
|---|---|
| `ConnectionStrings:PostgresConnection` | `ConnectionStrings__PostgresConnection` |
| `Neo4j:Password` | `Neo4j__Password` |
| `Cloudinary:CloudName` | `Cloudinary__CloudName` |
| `Cloudinary:ApiKey` | `Cloudinary__ApiKey` |
| `Cloudinary:ApiSecret` | `Cloudinary__ApiSecret` |

Parate en la raíz del repositorio, donde están el README.md y el archivo .sln

C:\Users\GianCroci\Documents\Nostalgiar\ProyectoFinalTPI.Backend

Desde ahí, usá:

dotnet user-secrets set "Cloudinary:ApiSecret" "TU_SECRETO_REAL" --project ".\ProyectoFinalTPI.Backend\ProyectoFinalTPI.Backend.csproj"

Reemplazá TU_SECRETO_REAL por el valor correspondiente. Para las otras claves de Cloudinary:

dotnet user-secrets set "Cloudinary:CloudName" "TU_CLOUD_NAME" --project ".\ProyectoFinalTPI.Backend\ProyectoFinalTPI.Backend.csproj"

dotnet user-secrets set "Cloudinary:ApiKey" "TU_API_KEY" --project ".\ProyectoFinalTPI.Backend\ProyectoFinalTPI.Backend.csproj"

Cada comando guarda o actualiza una clave en tus User Secrets, fuera del repositorio. No necesitás ejecutar init: el proyecto ya está configurado.

Tené en cuenta que el valor escrito puede quedar en el historial de tu terminal; no compartas capturas ni el historial con credenciales.

### 2. Sincronización Inicial de Paquetes y Base de Datos
Una vez clonado el repositorio, abre una terminal en la raíz de la solución y ejecuta los comandos en este orden exacto para limpiar la caché de NuGet, compilar el proyecto y **crear la base de datos relacional y geográfica automáticamente**:

```bash
# Limpiar y restaurar paquetes unificados de NetTopologySuite y EF Core 9.0.4
dotnet clean
dotnet restore
dotnet build

# Posicionarse en el proyecto web principal
cd ProyectoFinalTPI.Backend

# Crear la estructura física de tablas y extensiones en PostgreSQL
dotnet ef database update --context ApplicationDbContext
```

### 3. Carga Automática de Datos de Prueba (Data Seeder)

El proyecto incluye un mecanismo de **seed automático** ([`DataSeeder.cs`](ProyectoFinalTPI.Backend/DataSeeder.cs)) para poblar la base de datos con usuarios y publicaciones geolocalizadas de prueba la primera vez que se inicia el backend.

#### ¿Cómo funciona?

1. **Detección automática al iniciar:** Al levantar la API en entorno de desarrollo (`Development`), el seeder comprueba si la tabla `Usuarios` contiene registros (`!db.Usuarios.Any()`).
2. **Lectura de datos (`seed.json`):** Si la base está vacía, busca y lee el archivo `seed.json` en la raíz de la solución. Este archivo contiene los 37 autores y todas las memorias históricas de las décadas de los 70s, 80s, 90s y 2000s con coordenadas geográficas reales.
3. **Poblado de entidades:**
   - **Usuarios:** Crea un `Usuario_Personal` para cada autor del seed con correo generado y reputación inicial.
   - **Lugares:** Inserta las coordenadas espaciales con geometría `Point(lng, lat)` y SRID 4326 en PostgreSQL/PostGIS.
   - **Publicaciones:** Crea las publicaciones vinculándolas a su lugar, categoría, multimedia y autor.
4. **Idempotencia:** Si la base ya tiene usuarios, el seeder no se vuelve a ejecutar para proteger los datos existentes.

#### Credenciales de prueba para iniciar sesión

Todos los usuarios creados por el seeder tienen el mismo formato de correo y contraseña:

| Campo | Formato / Valor | Ejemplos |
|---|---|---|
| **Email** | `<nombre_sin_tildes>@example.com` | `miguel@example.com`, `susana@example.com`, `diego@example.com` |
| **Contraseña** | `password123` | `password123` |

> [!NOTE]
> En el primer inicio de sesión a través de `/api/auth/login`, `AuthServicio` valida la contraseña inicial en texto plano y la migra automáticamente a un hash **BCrypt**.

#### ¿Cómo reiniciar o forzar una nueva carga del Seeder?

Si durante el desarrollo deseas limpiar la base de datos y volver a cargar el set de datos inicial:

```powershell
# 1. Posicionarse en ProyectoFinalTPI.Backend
cd ProyectoFinalTPI.Backend

# 2. Eliminar y volver a aplicar las migraciones
dotnet ef database drop --context ApplicationDbContext
dotnet ef database update --context ApplicationDbContext

# 3. Al iniciar la aplicación, detectará la base vacía y resembrará automáticamente
dotnet run
```

#### ¿Cómo regenerar `seed.json` desde el Frontend?

Si se agregan nuevas memorias o autores en el frontend (`src/eras/registry.ts`), se puede actualizar `seed.json` ejecutando el script en la raíz de la solución:

```powershell
npx tsx export-seed.ts
```

---

## 🔄 Flujo de Trabajo para Modificaciones (Importante)

Dado que implementamos el patrón **Code-First** con Entity Framework Core, **está prohibido crear tablas o columnas a mano en pgAdmin**. 

Si necesitas agregar una nueva entidad o añadir un atributo (ejemplo: agregar la propiedad `Telefono` a la clase `Usuario`), debes seguir este flujo para que no se rompa el repositorio:

1. Modifica la clase correspondiente dentro de la biblioteca de clases `ProyectoFinalTPI.Backend.Entidades`.
2. Abre la terminal en el proyecto web principal (`ProyectoFinalTPI.Backend`).
3. Crea una nueva migración con un nombre descriptivo de lo que agregaste:
   ```bash
   dotnet ef migrations add AgregarTelefonoAUsuario --context ApplicationDbContext
   ```
4. Actualiza tu base de datos local para verificar que funcione:
   ```bash
   dotnet ef database update --context ApplicationDbContext
   ```
5. Haz el `git add` y `git commit` incluyendo los nuevos archivos generados dentro de la carpeta `Migrations`. Al hacer `git pull`, tus compañeros solo tendrán que ejecutar el comando `database update` para estar sincronizados.

*Nota: Las relaciones y propiedades añadidas en **Neo4j** no requieren migraciones de Entity Framework. Se implementan mediante consultas Cypher en el servicio correspondiente.*


### Tests de integración de publicaciones con Docker

Estos tests prueban los GET de mapa, resumen, detalle y comentarios mediante
peticiones HTTP que recorren el controlador, servicio y repositorio reales,
con PostgreSQL 16 y PostGIS.

**Requisitos y ejecución**

1. Tener instalado el SDK de .NET compatible con el proyecto y Docker Desktop.
2. Abrir Docker Desktop con el motor de contenedores Linux y esperar a que indique que está funcionando.
3. Desde la raíz del repositorio, ejecutar:

```powershell
dotnet test ProyectoFinalTPI.Backend.Tests/ProyectoFinalTPI.Backend.Tests.csproj --filter FullyQualifiedName~PublicacionesIntegracionTests
```

No hace falta iniciar la API manualmente ni crear una base de datos.
La primera ejecución necesita acceso a Internet para restaurar las dependencias
y descargar las imágenes de Docker; por eso puede tardar más.

**Qué sucede al ejecutar los tests**

- Testcontainers levanta un contenedor temporal con la imagen `postgis/postgis:16-3.4`.
- Cada caso crea una base independiente y aplica las migraciones del proyecto.
- La API de prueba utiliza esa base para ejecutar las consultas, incluidas las geográficas.
- Al terminar cada caso se elimina su base; al finalizar la clase se elimina el contenedor.

La base de desarrollo no se utiliza ni se modifica. La conexión y las credenciales
temporales las administra Testcontainers. Cloudinary está simulado y estos GET
no consultan Neo4j.

Si aparece `DockerUnavailableException`, comprobar que Docker Desktop esté abierto
y que el motor esté funcionando antes de repetir el comando. Las pruebas requieren
PostgreSQL/PostGIS real y no cambian automáticamente a una base en memoria.

Los tests originales de `CrearPublicacionTests.cs` y
`CompartirPublicacionTests.cs` mantienen su configuración anterior.
Más detalles en [Integracion/README.md](ProyectoFinalTPI.Backend.Tests/Integracion/README.md).

### Datos de demostración

El archivo `ProyectoFinalTPI.Backend/seed.json` incluye 42 publicaciones únicas
y 37 autores con perfiles personales y apellidos ficticios. Se carga automáticamente
al iniciar la API en `Development`, usando la conexión `PostgresConnection`.
La base debe tener aplicadas las migraciones.

Para cargar los datos sin dejar el servidor ejecutándose, desde la raíz:

```powershell
dotnet run --project ProyectoFinalTPI.Backend --no-launch-profile -- --environment Development --seed-only
```

La carga también funciona cuando ya hay usuarios. Reutiliza perfiles personales
por username/email, sin cambiar sus datos ni contraseñas; si hay una coincidencia
ambigua, informa el problema. Omite recuerdos existentes con el mismo autor,
título y fecha, y guarda los cambios dentro de una transacción.

Los usuarios nuevos de demostración tienen la contraseña `password123`, guardada
como hash BCrypt; sus emails están en `seed.json` (por ejemplo,
`miguel@example.com`). No son administradores. Las cuentas preexistentes conservan
sus credenciales. Estos datos son exclusivamente para desarrollo.

Se conservaron los textos, categorías, ubicaciones y enlaces originales.
Hay 19 publicaciones sin multimedia, cuya URL queda vacía.
Los metadatos `groupId` y `hashtags` permanecen en el JSON, pero este seeder solo
carga usuarios, lugares y publicaciones: no crea grupos ni relaciones en Neo4j.
Los bloques editoriales `facts` y `music` no se importan como publicaciones.

### Manejo centralizado de errores

`ManejoExcepcionesMiddleware`, ubicado en `ProyectoFinalTPI.Backend/Middlewares`,
centraliza las excepciones no manejadas de las solicitudes HTTP. Se registra una
sola vez en `Program.cs`, antes de `UseRouting`:

```csharp
app.UseMiddleware<ProyectoFinalTPI.Backend.Middlewares.ManejoExcepcionesMiddleware>();
```

Aplica automáticamente a todos los controladores, incluidos los que se agreguen
en el futuro. No hace falta invocarlo desde cada acción. El middleware ejecuta
`await next(context)` dentro de un `try`: continúa hacia el controlador y, si
el controlador o un servicio lanza una excepción, la captura y genera la respuesta.

| Excepción | Código HTTP | Título |
|---|---|---|
| `ArgumentException` | `400` | Solicitud inválida |
| `KeyNotFoundException` | `404` | Recurso no encontrado |
| Cualquier otra excepción no manejada | `500` | Error interno del servidor |

Las respuestas usan `ProblemDetails` con `Content-Type: application/problem+json`
y los campos `status`, `title`, `detail`, `instance` (ruta de la solicitud) y
`traceId`. Para 400 y 404, `detail` contiene el mensaje de la excepción, por lo
que debe ser apto para mostrar al cliente. Para 500, devuelve el mensaje genérico
«No se pudo completar la operación. Intentá nuevamente más tarde.» y registra
la excepción completa, el método, la ruta y el `traceId` en los logs.

Por ejemplo, una acción de Publicaciones llama directamente al servicio:

```csharp
return Ok(await _publicacionServicio.ObtenerDetallePorId(id, cancellationToken));
```

Si el servicio lanza `KeyNotFoundException`, el middleware genera el 404.
Los métodos `EjecutarOperacion` y `CrearProblema` ya no son necesarios en
`PublicacionesController`.

Si otro controlador captura una excepción con su propio `try/catch` y devuelve
una respuesta, esa excepción no llega al middleware. Si la vuelve a lanzar con
`throw;`, sí llega. Las respuestas explícitas como `BadRequest(...)` y
`NotFound(...)`, y las respuestas automáticas de validación de modelos, no se
modifican: no son excepciones.

Las cancelaciones (`OperationCanceledException` cuando `RequestAborted` está
cancelado) se propagan sin convertirse en 500. Si la respuesta ya comenzó a
enviarse, el middleware también propaga la excepción porque ya no puede
reemplazarla por un `ProblemDetails`.

### Endpoints de publicaciones

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/publicaciones` | Devuelve todas las publicaciones visibles con sus datos completos (autor, año, lugar, coordenadas, multimedia, categoría) y metadatos de reposts o citas (`repost`: `id`, `originalId`, `author`, `comment`). |
| `GET` | `/api/publicaciones/test-users` | Devuelve la lista de usuarios de prueba (`id`, `username`) disponibles en la base de datos para desarrollo y testing local. |
| `POST` | `/api/publicaciones` | Crea una publicación con sus datos, ubicación y multimedia. Recibe `multipart/form-data`, con un archivo o una URL HTTPS. |
| `POST` | `/api/publicaciones/compartir` | Comparte una publicación existente. Recibe `publicacionOriginalId`, `descripcion` (opcional) y `autorId`. Si `descripcion` está vacía o es null, genera un repost simple (desenrollando a la publicación raíz original); si contiene texto, genera un repost con cita (*quote tweet*) directo a dicha publicación. |
| `DELETE` | `/api/publicaciones/compartir/{originalId}` | Deshace/elimina el repost simple que el usuario autenticado (o especificado por query `autorId`) realizó sobre la publicación original. No elimina quote tweets ni la publicación original. |
| `DELETE` | `/api/publicaciones/{id}` | Elimina una publicación propia por su `id`. Si la solicitud incluye autenticación JWT, valida que el usuario sea el autor de la misma. |
| `POST` | `/api/publicaciones/{id}/etiquetas` | Etiqueta uno o más usuarios en una publicación existente. Recibe `usuariosIds` en JSON; repetir una etiqueta existente no la duplica. |
| `POST` | `/api/publicaciones/{publicacionId}/like` | Da like a la publicación indicada al usuario autenticado. No recibe body; el usuario se obtiene del JWT. |
| `DELETE` | `/api/publicaciones/{publicacionId}/like` | Quita el like del usuario autenticado a la publicación indicada. No recibe body; el usuario se obtiene del JWT. |
| `GET` | `/api/publicaciones/mapa` | Devuelve los pines del área visible del mapa. Permite filtrar por categoría y fecha del recuerdo. |
| `GET` | `/api/publicaciones/{id}/resumen` | Devuelve una vista resumida para mostrar al seleccionar un pin. |
| `GET` | `/api/publicaciones/{id}` | Devuelve el detalle de una publicación, con descripción completa, autor, multimedia y ubicación. |
| `GET` | `/api/publicaciones/{id}/comentarios` | Devuelve los comentarios visibles de una publicación, paginados y ordenados del más reciente al más antiguo. |
| `POST` | `/api/publicaciones/{publicacionId}/comentarios` | Crea un comentario asociado a la publicación indicada en la URL. Recibe contenido y usuarioId en JSON. |

Las consultas públicas excluyen las publicaciones ocultas. Los comentarios se consultan por separado del detalle.
Las etiquetas se almacenan en Neo4j como relaciones entre los nodos `Usuario` y `Publicacion`, cuyos datos maestros permanecen en PostgreSQL.

### Dar o quitar me gusta

Ambas operaciones requieren un JWT válido en `Authorization: Bearer <token>`. El `publicacionId` se envía en la ruta; no se envía `usuarioId` ni un body, porque el usuario se identifica desde el token.

```http
POST /api/publicaciones/42/like
DELETE /api/publicaciones/42/like
Authorization: Bearer <token>
```

El `POST` crea en Neo4j la relación `(:Usuario)-[:LE_GUSTA {creadaEn}]->(:Publicacion)`. Utiliza `MERGE`, por lo que repetir la solicitud del mismo usuario a la misma publicación no duplica el like. El `DELETE` elimina esa relación; si ya no existe, la operación no crea ni duplica nada.

Antes de escribir en Neo4j, el backend comprueba en PostgreSQL que el usuario existe y que la publicación existe y está visible. Ambas rutas responden `200 OK` con el estado actualizado:

```json
{
  "publicacionId": 42,
  "cantidadLikes": 1,
  "dioLike": true
}
```

`cantidadLikes` cuenta las relaciones de like de esa publicación y `dioLike` indica si el usuario del token le dio like. Después de quitarlo, para ese usuario `dioLike` será `false`; el contador reflejará los likes restantes. Se devuelve `401 Unauthorized` si falta un JWT válido, `400 Bad Request` ante un ID inválido, `404 Not Found` si el usuario o la publicación no existen o la publicación está oculta, y `500 Internal Server Error` ante un fallo inesperado.

Los likes se guardan en Neo4j y también alimentan `GET /api/feed/para-vos`: se recomiendan publicaciones que gustaron a usuarios con likes en común o que comparten usuarios etiquetados con publicaciones que el usuario marcó. El feed excluye publicaciones que el usuario ya likeó y valida en PostgreSQL que las recomendadas sigan visibles. No se necesita una migración de PostgreSQL para los likes.

### Crear un comentario

En Postman, seleccionar **POST**, **Body → raw → JSON**:

```http
POST /api/publicaciones/25/comentarios
Content-Type: application/json
```

```json
{
  "contenido": "¡Qué lindo recuerdo!",
  "usuarioId": 1
}
```

El ID de la publicación se toma de la URL (25 en este ejemplo).
`usuarioId` debe corresponder a un usuario existente; se recibe temporalmente
en el body, igual que `AutorId` al crear publicaciones, hasta integrar el usuario
autenticado del token en estas acciones.

El contenido es obligatorio y no puede estar vacío ni contener solo espacios.
Se eliminan los espacios al principio y al final. El servidor asigna la fecha
UTC y guarda el comentario como visible.

Devuelve **201 Created** con el comentario:

```json
{
  "id": 8,
  "contenido": "¡Qué lindo recuerdo!",
  "fechaCreacion": "2026-10-13T15:00:00Z",
  "autor": {
    "id": 1,
    "username": "usuarioTest"
  }
}
```

Los IDs y la fecha de esta respuesta son ilustrativos. El comentario se puede
consultar mediante `GET /api/publicaciones/25/comentarios`.

- **400:** contenido o identificadores inválidos. Las validaciones del request
  incluyen `errors` por campo.
- **404:** el usuario no existe o la publicación no existe o está oculta.
- **500:** falla inesperada, con mensaje público y detalle técnico en los logs.

Los errores usan `ProblemDetails`. Se utiliza la tabla de comentarios existente,
por lo que este endpoint no requiere una migración nueva. El detalle y el feed
calculan `cantidadComentarios` contando solo los comentarios visibles de cada
publicación. Los campos `cantidadLikes` y `dioLike` se consultan en Neo4j.

### Endpoints del feed

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/feed/para-vos` | Recomienda publicaciones relacionadas con los likes del usuario, usando likes en común y usuarios etiquetados compartidos en Neo4j. Excluye publicaciones que el usuario ya likeó. |
| `GET` | `/api/feed/seguidos` | Devuelve las publicaciones de las cuentas que sigue el usuario, ordenadas de más recientes a más antiguas. Los seguimientos se consultan en Neo4j. |

Ambos reciben temporalmente `usuarioId` por query hasta integrar autenticación. Las descripciones del feed tienen un máximo de 300 caracteres.

### Paginación

Los comentarios y ambos feeds reciben:

- `limite`: cantidad de elementos por página, entre 1 y 50; por defecto, 20.
- `cursor`: valor recibido en `siguienteCursor` para continuar. Se omite en la primera petición.

La respuesta contiene `items`, `siguienteCursor` y `hayMas`.

```http
GET /api/feed/para-vos?usuarioId=1&limite=20
GET /api/feed/seguidos?usuarioId=1&limite=20
GET /api/publicaciones/25/comentarios?limite=20
```

Si “Para vos” no encuentra recomendaciones visibles, muestra publicaciones generales visibles ordenadas por fecha de creación de más nueva a más antigua (y por ID descendente en caso de empate). No requiere que el usuario haya creado publicaciones ni dado likes.

Para recargar “Para vos”, se realiza otra petición sin cursor. Esto genera una nueva selección. Cada sesión conserva hasta 500 publicaciones, también cuando usa el listado general, y vence a los 30 minutos; si deja de estar disponible, el endpoint devuelve `410 Gone`.

### Endpoints de seguimiento

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/seguimientos` | Crea la relación de seguimiento entre dos usuarios en Neo4j. |
| `DELETE` | `/api/seguimientos` | Elimina la relación de seguimiento entre dos usuarios. |
| `GET` | `/api/seguimientos/{idUsuario}/seguidos` | Lista las personas que sigue el usuario indicado. |
| `GET` | `/api/seguimientos/{idUsuario}/seguidores` | Lista las personas que siguen al usuario indicado. |

Ambos reciben un cuerpo JSON:

```json
{
  "idUsuario": 1,
  "idUsuarioASeguir": 2
}
```

### Autenticación JWT y Swagger

#### Nuevos secrets requeridos

Agregar estos tres comandos a la configuración inicial de User Secrets (junto a los existentes):

```powershell
dotnet user-secrets set "Jwt:Clave" "nostalgia-clave-secreta-super-segura-2026" --project "./ProyectoFinalTPI.Backend/ProyectoFinalTPI.Backend.csproj"
dotnet user-secrets set "Jwt:Emisor" "nostalgia-backend" --project "./ProyectoFinalTPI.Backend/ProyectoFinalTPI.Backend.csproj"
dotnet user-secrets set "Jwt:Audiencia" "nostalgia-frontend" --project "./ProyectoFinalTPI.Backend/ProyectoFinalTPI.Backend.csproj"
```

La tabla de variables de entorno para producción se amplía con:

| Clave de User Secrets | Variable de entorno para producción |
|---|---|
| `Jwt:Clave` | `Jwt__Clave` |
| `Jwt:Emisor` | `Jwt__Emisor` |
| `Jwt:Audiencia` | `Jwt__Audiencia` |

#### Swagger

Con la aplicación corriendo, la documentación interactiva está disponible en: `http://localhost:5199/swagger` (o `http://localhost:5000/swagger` según el launch profile).

Para probar endpoints protegidos: obtener un token desde `POST /api/auth/login` y pegarlo en el botón **Authorize** (`Bearer <token>`).

### Endpoints de autenticación

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/auth/registro` | Registra un nuevo usuario. Recibe `username`, `email` y `contrasena` en JSON. Devuelve `201 Created` con token JWT. |
| `POST` | `/api/auth/login` | Autentica un usuario existente. Recibe `email` y `contrasena` en JSON. Devuelve `200 OK` con token JWT. |

Las contraseñas se almacenan hasheadas con BCrypt. El token JWT tiene validez de 7 días e incluye el id, email, username y rol del usuario.

## Chat en tiempo real (SignalR)

El chat privado entre usuarios utiliza **SignalR** sobre WebSockets. A diferencia de los endpoints HTTP del resto de la API, la comunicación ocurre a través de un **Hub** persistente: el cliente establece una conexión que permanece abierta y el servidor puede enviar mensajes en cualquier momento sin que el cliente los solicite.

### Funcionamiento interno

- **`PresenciaService`** mantiene en memoria un diccionario `UsuarioId → ConnectionId`. Al conectarse, el usuario queda registrado como "en línea"; al desconectarse, se elimina del registro.
- **`ChatServicio`** persiste los mensajes en PostgreSQL con una fecha de expiración de **7 días**. Los mensajes solo se pueden enviar si el destinatario está en línea en ese momento.
- **`LimpiezaMensajesService`** es un servicio en background que elimina automáticamente los mensajes expirados cada hora.

### Ruta del Hub

```
/hubs/chat
```

El token JWT se envía por query string (mecanismo estándar de SignalR, ya que el handshake WebSocket no admite headers personalizados):

```
ws://localhost:{puerto}/hubs/chat?access_token=<TOKEN_JWT>
```

### Métodos invocables desde el cliente

| Método | Parámetros | Descripción |
|---|---|---|
| `EnviarMensaje` | `destinatarioId: int, contenido: string` | Envía un mensaje privado. Falla si el destinatario está offline, el contenido está vacío o supera los 2000 caracteres. |
| `ObtenerHistorial` | `otroUsuarioId: int` | Devuelve los mensajes no expirados entre el usuario autenticado y el otro, ordenados cronológicamente. |

### Eventos que el cliente debe escuchar

| Evento | Payload | Cuándo se dispara |
|---|---|---|
| `RecibirMensaje` | `MensajeChatDto` | Cuando llega un mensaje nuevo dirigido al usuario conectado. |
| `MensajeEnviado` | `MensajeChatDto` | Confirmación al emisor de que su mensaje fue guardado y entregado. |

### Endpoints HTTP del chat
| Método    |	Ruta    |	Descripción
| GET   |	/api/chat/conversaciones    |	Devuelve las conversaciones activas del usuario autenticado: una entrada por cada usuario con quien haya intercambiado mensajes en los últimos 7 días, con el último mensaje de cada conversación. Requiere JWT. Devuelve lista vacía si no hay conversaciones activas.

### Endpoints de usuarios
|   Método  |	Ruta    |	Descripción
|   GET |	/api/usuarios/por-username/{username}   |	Busca un usuario por su username exacto. Devuelve su id y username. Requiere JWT. Devuelve 404 si no existe ningún usuario con ese username.
