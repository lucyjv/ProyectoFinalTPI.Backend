# ProyectoFinalTPI.Backend

## Frontend, API y bases con Docker

Desde esta carpeta, con Docker Desktop iniciado:

```powershell
docker compose up --build -d
```

Aplicación en http://localhost:3001 y Swagger en http://localhost:5199/swagger.
Crea PostgreSQL/PostGIS y Neo4j propios, aplica migraciones y carga datos de demostración.
Requiere el frontend en `../Web/ProyectoFinalTPI`. Ver [configuración y operación](docs/DOCKER.md).

## Cobertura de tests

`coverage.runsettings` limita la medición a los ensamblados `ProyectoFinalTPI.Backend*`, excluyendo el proyecto de tests y las bibliotecas externas. Lo utilizan `coverage.ps1` y CI. Para ejecutar manualmente: `dotnet test --settings coverage.runsettings --collect:"XPlat Code Coverage"`. En Visual Studio se puede seleccionar desde Test > Configure Run Settings > Select Solution Wide runsettings File. Si se utiliza otra extensión para mostrar cobertura, configurar ese mismo archivo en la extensión. Volver a generar el informe después de cambiar filtros: los informes anteriores conservan sus resultados.

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
| `POST` | `/api/publicaciones` | Crea una publicación con sus datos, ubicación y multimedia. Recibe `multipart/form-data`, con un archivo o una URL HTTPS. |
| `POST` | `/api/publicaciones/{id}/repost` | Repostea una publicación simple en Neo4j vinculada al usuario autenticado. |
| `DELETE` | `/api/publicaciones/{id}/repost` | Deshace/elimina el repost simple que el usuario realizó sobre la publicación. |
| `POST` | `/api/publicaciones/{id}/citar` | Cita una publicación existente creando una nueva publicación en PostgreSQL con comentario propio. |
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

### Red de recuerdos: integración de frontend (paso 1)

El detalle de un recuerdo puede mostrar **"Ese día, por acá"** y **"Más historias de este lugar"**. Las relaciones ya se calculan al consultar; no requieren análisis ni un paso adicional al publicar. Los endpoints son públicos, no reciben `usuarioId` y no requieren JWT.

#### Reglas del contrato

| Bloque / tipo | Publicaciones incluidas |
|---|---|
| `dia` | Mismo día, mes y año del recuerdo de origen, a **200 metros o menos**. |
| `lugar` | A **200 metros o menos**, sin restringir fecha. También incluye las coincidencias de `dia`. |

- El radio es fijo y se mide desde el recuerdo consultado; no se envían coordenadas ni radio desde el cliente. El límite de **200 m está incluido**.
- Ambos bloques se ordenan por distancia ascendente y luego por Id ascendente. No ordenarlos por fecha en el cliente: el cursor continúa ese orden.
- Se excluyen el propio recuerdo, publicaciones ocultas y reposts. Si se consulta un repost visible, el backend resuelve su original visible. `publicacionId` del resumen identifica ese original y debe usarse al continuar.
- La cercanía no es transitiva: al abrir otra tarjeta, las nuevas relaciones se calculan desde la ubicación de esa tarjeta.
- Las tarjetas usan un DTO propio, con autor en campos planos (`autorId`, `autorUsername`). No es el DTO de detalle ni el de feed; no incluye likes, puntuaciones, comentarios o coordenadas. Para abrir una tarjeta, consultar el detalle existente: `GET /api/publicaciones/{id}`.
- El paso 1 no devuelve `momentos` ni estado de análisis de IA. Esos campos pertenecen a entregas futuras y no deben esperarse en estas respuestas.

#### 1. Cargar los dos bloques del detalle

```http
GET /api/publicaciones/10/relaciones
```

Devuelve `200 OK` con hasta **6 tarjetas por bloque**. IDs, textos y distancias de este ejemplo son ilustrativos:

```json
{
  "publicacionId": 10,
  "radioMetros": 200,
  "dia": {
    "items": [
      {
        "id": 11,
        "titulo": "Los festejos con mi familia",
        "descripcionBreve": "Así vivimos ese día cerca del Obelisco.",
        "fecha": "1986-06-29T18:00:00Z",
        "urlMultimedia": "https://example.com/recuerdo-11.jpg",
        "tipoMultimedia": 0,
        "autorId": 3,
        "autorUsername": "ana",
        "nombreLugar": "Obelisco",
        "distanciaMetros": 125.43,
        "mismoDia": true,
        "mismoLugar": true
      }
    ],
    "siguienteCursor": null,
    "hayMas": false
  },
  "lugar": {
    "items": [
      {
        "id": 12,
        "titulo": "Una tarde en el centro",
        "descripcionBreve": "Una visita años después.",
        "fecha": "2000-04-20T00:00:00Z",
        "urlMultimedia": "https://example.com/recuerdo-12.jpg",
        "tipoMultimedia": 0,
        "autorId": 4,
        "autorUsername": "juan",
        "nombreLugar": "Avenida Corrientes",
        "distanciaMetros": 50,
        "mismoDia": false,
        "mismoLugar": true
      },
      {
        "id": 11,
        "titulo": "Los festejos con mi familia",
        "descripcionBreve": "Así vivimos ese día cerca del Obelisco.",
        "fecha": "1986-06-29T18:00:00Z",
        "urlMultimedia": "https://example.com/recuerdo-11.jpg",
        "tipoMultimedia": 0,
        "autorId": 3,
        "autorUsername": "ana",
        "nombreLugar": "Obelisco",
        "distanciaMetros": 125.43,
        "mismoDia": true,
        "mismoLugar": true
      }
    ],
    "siguienteCursor": null,
    "hayMas": false
  }
}
```

`descripcionBreve` contiene como máximo 180 caracteres, sin puntos suspensivos añadidos por el backend. `tipoMultimedia` es numérico: `0` = foto, `1` = video. `distanciaMetros` se entrega con precisión para la paginación; se puede redondear visualmente, sin modificar el cursor. `mismoLugar` siempre es `true` en estas consultas; `mismoDia` permite explicar coincidencias adicionales en el bloque de lugar.

#### 2. Ver más y continuar páginas

```http
GET /api/publicaciones/10/relacionadas?tipo=dia&limite=20
GET /api/publicaciones/10/relacionadas?tipo=lugar&limite=20
```

| Parámetro | Contrato |
|---|---|
| `tipo` | `dia` o `lugar`, en minúsculas. Por defecto `lugar`. |
| `limite` | Entero entre 1 y 50. Por defecto 20. No modifica el máximo de 6 del endpoint de resumen. |
| `cursor` | Omitir en la primera página. Para continuar, enviar el `siguienteCursor` del bloque o página anterior. |

La respuesta es una página: `{ "items": [...], "siguienteCursor": "...", "hayMas": true }`. Si no hay más, `siguienteCursor` es `null` y `hayMas` es `false`. Una lista sin coincidencias devuelve `200`, `items: []`, `siguienteCursor: null`, `hayMas: false`; no es un error.

Para **"Ver más"** hay dos alternativas:

1. Conservar las tarjetas del resumen y continuar con el cursor de ese bloque, si `hayMas` es `true`.
2. Abrir una lista completa y pedir su primera página sin cursor, reemplazando las tarjetas iniciales.

No anexar una primera página sin cursor a las seis tarjetas del resumen: repetiría resultados. Los cursores son opacos; no construirlos ni decodificarlos. Usar `URLSearchParams` o equivalente porque pueden contener `+`, `/` y `=`. Mantener cursor y estado de carga independientes para cada tipo y recuerdo; no intercambiarlos. No hay número de página ni total de resultados.

#### Tipos y cliente de ejemplo (TypeScript)

`API_BASE_URL` debe apuntar a la raíz de la API configurada por el frontend, sin `/api` al final.

```typescript
type TipoRelacion = "dia" | "lugar";

interface RecuerdoRelacionado {
  id: number;
  titulo: string;
  descripcionBreve: string;
  fecha: string; // ISO 8601 UTC
  urlMultimedia: string;
  tipoMultimedia: 0 | 1;
  autorId: number;
  autorUsername: string;
  nombreLugar: string;
  distanciaMetros: number;
  mismoDia: boolean;
  mismoLugar: boolean;
}

interface Pagina<T> {
  items: T[];
  siguienteCursor: string | null;
  hayMas: boolean;
}

interface RelacionesRecuerdo {
  publicacionId: number;
  radioMetros: number;
  dia: Pagina<RecuerdoRelacionado>;
  lugar: Pagina<RecuerdoRelacionado>;
}

interface ProblemaApi {
  status: number;
  title?: string;
  detail?: string;
  instance?: string;
  traceId?: string;
}

async function consultar<T>(ruta: string, signal?: AbortSignal): Promise<T> {
  const respuesta = await fetch(`${API_BASE_URL}${ruta}`, { signal });
  if (!respuesta.ok) {
    const problema = await respuesta.json().catch(() => ({})) as Partial<ProblemaApi>;
    throw Object.assign(new Error(problema.detail ?? "No se pudo cargar los recuerdos"), {
      status: respuesta.status,
      traceId: problema.traceId
    });
  }
  return respuesta.json() as Promise<T>;
}

function obtenerRelaciones(id: number, signal?: AbortSignal) {
  return consultar<RelacionesRecuerdo>(`/api/publicaciones/${id}/relaciones`, signal);
}

function obtenerRelacionadas(
  id: number, tipo: TipoRelacion, cursor: string | null = null,
  limite = 20, signal?: AbortSignal
) {
  const query = new URLSearchParams({ tipo, limite: String(limite) });
  if (cursor !== null) query.set("cursor", cursor);
  return consultar<Pagina<RecuerdoRelacionado>>(
    `/api/publicaciones/${id}/relacionadas?${query}`, signal
  );
}

// Ejemplo: conservar la vista inicial y anexar la siguiente página de "dia".
const relaciones = await obtenerRelaciones(idRecuerdo);
if (relaciones.dia.hayMas && relaciones.dia.siguienteCursor !== null) {
  const siguiente = await obtenerRelacionadas(
    relaciones.publicacionId, "dia", relaciones.dia.siguienteCursor
  );
  const itemsDia = [...relaciones.dia.items, ...siguiente.items];
  // Guardar itemsDia y siguiente.siguienteCursor/hayMas en el estado de "dia".
}
```

#### Fechas, estados y navegación

- **Fecha del recuerdo:** la comparación de día completo usa UTC. Mostrarla con `timeZone: "UTC"` para evitar que una fecha a medianoche aparezca como el día anterior en Argentina. Ejemplo: `new Intl.DateTimeFormat("es-AR", { timeZone: "UTC", day: "numeric", month: "long", year: "numeric" }).format(new Date(item.fecha))`. No se está mostrando la fecha de creación del post.
- **Carga:** consultar el detalle y las relaciones por separado. Mostrar carga propia para los bloques y permitir reintentar un fallo sin bloquear el recuerdo principal.
- **Sin coincidencias:** ocultar el bloque o mostrar un estado vacío; no tratar `items: []` como 404.
- **Tarjetas repetidas entre bloques:** `lugar` incluye las de `dia`. Si se quiere evitar duplicación visual, filtrar por Id en la presentación. Conservar los datos y cursores originales de cada bloque; `hayMas` se refiere al criterio completo del servidor, no a la cantidad visible después del filtro.
- **Cambio de recuerdo:** cancelar solicitudes anteriores con `AbortController`, limpiar listas/cursores y cargar las nuevas relaciones. Ignorar resultados de solicitudes que ya no correspondan al recuerdo abierto. Deshabilitar "Cargar más" mientras haya una petición del mismo bloque en curso.
- **Cambios entre páginas:** no hay una instantánea fija. Si se oculta o modifica una publicación, los resultados pueden cambiar. Deduplicar por Id al anexar páginas y reiniciar sin cursor al refrescar.

#### Errores HTTP

Los errores de validación/consulta usan `application/problem+json` (`ProblemDetails`). La respuesta puede incluir `status`, `title`, `detail`, `instance` y `traceId`; los errores de binding también pueden incluir `errors` por campo.

| Estado | Causa / tratamiento sugerido |
|---|---|
| `400` | Id no positivo, tipo inválido, límite fuera de 1-50 o cursor inválido/de otro contexto. Revisar parámetros; si el cursor quedó obsoleto, reiniciar ese bloque sin cursor. |
| `404` | El recuerdo no existe o está oculto; también si es un repost cuyo original no está disponible. Mostrar que las relaciones ya no están disponibles. |
| `500` | Fallo inesperado. Permitir reintentar y conservar el detalle ya cargado. `traceId` sirve para reportar el fallo al backend. |

Una URL con Id no entero no coincide con la ruta `{id:int}` y puede devolver un 404 del enrutador sin este cuerpo. El cliente debe basarse primero en el estado HTTP, como en el ejemplo anterior.

#### Preparación y verificación de la integración

En cada entorno del backend, aplicar las migraciones (incluye `20261021124515_AgregarIndicesRedRecuerdos`) desde la raíz:

```powershell
dotnet ef database update --project ProyectoFinalTPI.Backend.Repositorio --startup-project ProyectoFinalTPI.Backend
```

Comprobar en la UI: misma fecha completa; otra fecha dentro del radio solo en `lugar`; inclusión a 200 m y exclusión fuera; ausencia de ocultos/reposts; ambos bloques vacíos; navegación desde un repost; carga de más sin duplicados; cambio de recuerdo durante una petición y representación de fechas UTC sin desplazamiento.

La suite del paso 1 tiene **48 casos por capa y 4 de integración** con PostGIS, incluido el límite exacto de 200 m y su paginación. Detalles de arquitectura y ejecución: [Red de recuerdos: paso 1](docs/red-recuerdos-paso-1.md).

### Modelo de momentos (paso 2, backend interno)

El backend incorpora `Momento`, `MomentoAlias` y `PublicacionMomento`, con fechas opcionales del acontecimiento, jerarquía padre/hijo sin ciclos y varias asociaciones por recuerdo. Los alias se normalizan; las asociaciones automáticas respetan decisiones previas del autor.

Esta etapa creó la base interna de momentos. Los endpoints de exploración y correcciones se agregan en el paso 3, descrito a continuación; el análisis de IA se incorpora después. Para sincronizar la base, ejecutar `dotnet ef database update --project ProyectoFinalTPI.Backend.Repositorio --startup-project ProyectoFinalTPI.Backend` desde la raíz (incluye `20261021141850_AgregarModeloMomentos`).

Reglas, contratos internos y pruebas: [Modelo de momentos: paso 2](docs/red-recuerdos-paso-2.md).

### Momentos: integración de frontend y correcciones (paso 3)

| Método | Ruta | Uso |
|---|---|---|
| GET | `/api/momentos?busqueda=...&limite=20&cursor=...` | Búsqueda paginada por nombre/alias; sin búsqueda lista todos. |
| GET | `/api/momentos/{id}` | Detalle, padre, hijos directos y alias. |
| GET | `/api/momentos/{id}/publicaciones?incluirDescendientes=true&limite=20&cursor=...` | Recuerdos visibles del momento y sus descendientes, sin duplicados. |
| GET | `/api/publicaciones/{id}/momentos` | Asociaciones activas, origen, preferencia de automatización y versión de edición. |
| PUT | `/api/publicaciones/{id}/momentos` | Corrección completa por el autor autenticado. |

Las lecturas son públicas. Para mostrar **"Parte de este momento"**, consultar el GET de momentos del recuerdo por separado del endpoint de relaciones del paso 1. Enviar JWT en esa lectura si se quiere obtener `puedeCorregir` para el autor actual. La respuesta incluye `publicacionId`, `versionRelaciones`, `permitirMomentosAutomaticos`, `puedeCorregir` y `momentos: [{ momento: { id, nombre, fechaInicio, fechaFin, momentoPadreId }, origen }]`. El origen es `automatico` o `autor`.

El PUT requiere JWT y recibe los tres campos obligatorios:

```json
{
  "momentoIds": [2, 3],
  "permitirMomentosAutomaticos": true,
  "versionRelaciones": 3
}
```

La selección admite hasta 50 Id positivos sin repetir; los omitidos quedan rechazados por el autor. Para indicar ningún acontecimiento, enviar lista vacía y automatización false. El autor se toma del token. El PUT devuelve el estado actualizado; si devuelve **409**, recargar el GET y dejar revisar la selección antes de reenviar. No reintentar automáticamente con otra versión. Rechazos y automatización desactivada se respetan en futuros análisis.

Ambos listados usan `items`, `siguienteCursor`, `hayMas` y límite 1-50 (20 por defecto). La búsqueda ordena por Id ascendente; los recuerdos por fecha del recuerdo e Id descendentes. No reutilizar cursores entre consultas/contextos. Un GET desde un repost resuelve al original visible; guardar usando el `publicacionId` de la respuesta.

Aplicar `20261021144634_AgregarCorreccionMomentos` mediante el comando habitual de `database update`. La generación automática de acontecimientos y la cola se incorporan en entregas posteriores. Ejemplos JSON completos, cliente TypeScript, fechas, errores y pruebas: [Integración de momentos: paso 3](docs/red-recuerdos-paso-3.md).

### Análisis en segundo plano (paso 4)

Crear un recuerdo original visible guarda también un trabajo persistente de análisis, en la misma transacción. El POST conserva `201 { id }` y no espera al analizador. El worker usa reservas con token/vencimiento, reintentos y control de versiones para recuperar interrupciones y descartar resultados de texto desactualizado. Las correcciones del autor siguen teniendo prioridad.

El analizador temporal termina en `sinAnalizador`, sin inventar ni retirar asociaciones. El paso 5 permite habilitar Gemini para detectar y resolver acontecimientos colectivos.

Frontend puede consultar el estado de la versión actual con `GET /api/publicaciones/{id}/analisis` (público):

```json
{
  "publicacionId": 10,
  "versionContenido": 1,
  "estado": "pendiente"
}
```

Consultar de forma espaciada mientras el detalle esté abierto y el estado sea `pendiente` o `procesando`; después recargar sus momentos. Lugar/día no esperan al análisis. `VersionContenido` es independiente de `VersionRelaciones`, que sigue usándose al corregir momentos. Las publicaciones anteriores no se encolan en masa y muestran `noProgramado` hasta que se seleccionen para reprocesar desde el paso 6.

Aplicar `20261021160638_AgregarColaAnalisisRecuerdos` mediante el comando habitual de migración. Configuración, estados, recuperación, métricas y pruebas: [Cola de análisis: paso 4](docs/red-recuerdos-paso-4.md).

### Detección de acontecimientos con Gemini (paso 5)

Se integró Gemini detrás de `IAnalizadorRecuerdo`, con el modelo configurable `gemini-3.5-flash-lite`. Analiza título/descripción, propone acontecimientos colectivos con evidencia y confianza, reutiliza el catálogo o crea momentos y alias al confirmar el trabajo. El catálogo y los vínculos se guardan juntos; se respetan versiones y correcciones del autor. No requiere nuevas migraciones ni cambios en los endpoints de frontend.

Gemini viene deshabilitado. Guardar `Gemini:ApiKey` en User Secrets del proyecto web y habilitar expresamente `Gemini:Habilitado` para procesar recuerdos reales. El nivel gratuito y sus cuotas dependen del proyecto en AI Studio. Google puede usar contenido enviado en el nivel gratuito para mejorar sus productos; evaluar primero con datos ficticios.

La prueba aislada `dotnet run --project ProyectoFinalTPI.Backend --no-launch-profile -- --probar-gemini`, ejecutada en Development, envía tres ejemplos ficticios y no abre ni modifica la base. La clave no se envía al frontend. Cambiar luego `Gemini:Modelo` permite evaluar otro modelo compatible sin modificar la cola ni sus endpoints.

Configuración completa, ejecución de la prueba, cuotas, privacidad y límites de la canonicalización: [Gemini y acontecimientos colectivos: paso 5](docs/red-recuerdos-paso-5.md).

### Reprocesar recuerdos existentes por lotes (paso 6)

Los administradores pueden previsualizar recuerdos elegibles con `GET /api/admin/analisis-recuerdos/reprocesamiento?limite=10`, y enviar una selección de IDs/versiones al POST de la misma ruta. La vista previa no modifica datos ni llama a Gemini. El POST responde 202 con los trabajos programados y los motivos de cada omisión.

Se admiten hasta 25 recuerdos por lote y un intervalo de 10 a 3600 segundos. Incluye versiones sin trabajo o terminadas como `sinAnalizador`; los fallidos requieren `incluirFallidos=true`. No reactiva versiones ya analizadas, pendientes, procesando o descartadas. Excluye ocultos, reposts y automatización desactivada. Vuelve a validar cada versión al encolar, conserva las correcciones del autor y no duplica trabajos cuando se repite un lote.

Encolar requiere un proveedor real y el worker habilitados; de lo contrario devuelve 503 sin cambios. Ambos endpoints requieren sesión de administrador (`esAdmin=true`). No hay migraciones nuevas ni procesamiento histórico automático. Vista previa local de solo lectura: `dotnet run --project ProyectoFinalTPI.Backend --no-launch-profile -- --previsualizar-reprocesamiento --limite 5`, en Development.

Ejemplos completos, seguimiento, concurrencia y ejecución: [Reprocesamiento controlado: paso 6](docs/red-recuerdos-paso-6.md).

### Prevención y gestión de momentos duplicados (paso 7)

Las altas reutilizan una identidad normalizada por nombre y fechas, respaldada por un índice único. Se resuelven los IDs fusionados al momento canónico y se reutilizan sus alias. Las variantes semánticas dudosas requieren revisión: no se fusionan automáticamente por similitud textual.

Los administradores cuentan con `GET /api/admin/momentos/{origenId}/duplicados`, `GET /api/admin/momentos/{origenId}/fusion?destinoId=...` y `POST /api/admin/momentos/{origenId}/fusion`. La confirmación requiere las versiones de catálogo de la vista previa. Conserva publicaciones, nombres anteriores, alias y rechazos del autor; actualiza las versiones de relaciones afectadas y deja referencias válidas para los IDs anteriores.

Aplicar `20261021183413_AgregarIdentidadYFusionMomentos`. La migración no fusiona ni elimina datos. Semántica de conflictos, respuestas Swagger, concurrencia y ejemplos completos: [Gestión de duplicados: paso 7](docs/red-recuerdos-paso-7.md).

### Plan de implementación para frontend

El [plan de acción de frontend](docs/plan-frontend-red-recuerdos.md) reúne los 13 endpoints de la red de recuerdos, contratos TypeScript, reglas de paginación y versiones, entregas por pantalla, manejo de errores y criterios de aceptación. Incluye la experiencia pública, las correcciones del autor y la administración de reprocesamiento y duplicados; está preparado para compartir como contexto de implementación.

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

Los likes se guardan en Neo4j y alimentan `GET /api/feed/para-vos` junto con las puntuaciones y las etiquetas. El feed excluye publicaciones que el usuario ya likeó o puntuó y valida en PostgreSQL que las candidatas sigan visibles. Las reglas completas de ranking están en la sección «Endpoints del feed». No se necesita una migración de PostgreSQL para los likes.

### Endpoints de seguimientos

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/seguimientos/{idUsuarioASeguir}` | Sigue al usuario indicado. El seguidor se extrae del JWT autenticado. |
| `DELETE` | `/api/seguimientos/{idUsuarioASeguir}` | Deja de seguir al usuario indicado. No requiere body. El seguidor se extrae del JWT autenticado. |
| `GET` | `/api/seguimientos/{idUsuario}/seguidos` | Lista los usuarios seguidos por `idUsuario`. |
| `GET` | `/api/seguimientos/{idUsuario}/seguidores` | Lista los seguidores de `idUsuario`. |

```http
POST /api/seguimientos/20
DELETE /api/seguimientos/20
Authorization: Bearer <token>
```

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
| `GET` | `/api/feed/para-vos` | Ordena publicaciones por coincidencias sociales, afinidad de categoría y cercanía a lugares que le gustaron al usuario. Excluye likes y puntuaciones; prioriza publicaciones no vistas y recupera vistas si falta contenido. |
| `GET` | `/api/feed/seguidos` | Devuelve las publicaciones de las cuentas que sigue el usuario, ordenadas de más recientes a más antiguas. Los seguimientos se consultan en Neo4j. |
| `POST` | `/api/feed/impresiones` | Registra hasta 50 publicaciones mostradas en “Para vos”. Requiere JWT y recibe `publicacionesIds`; actualiza `(:Usuario)-[:VIO {ultimaVez}]->(:Publicacion)` en Neo4j. |

“Para vos” combina las coincidencias sociales existentes con afinidad por categoría y cercanía a lugares de publicaciones que el usuario marcó con like. La afinidad por categoría no limita la década. Las publicaciones que ya tienen like o puntuación se excluyen; las publicaciones no vistas tienen prioridad. Luego se muestran las vistas hace más de 14 días y, si todavía falta contenido, las vistas recientes, empezando por las menos recientes. La ubicación y categoría se leen de PostgreSQL; las interacciones y las impresiones se almacenan en Neo4j.

Cada publicación semilla se considera una sola vez aunque el usuario le haya dado like y también la haya puntuado. Las coincidencias por like, puntuación alta y etiquetas suman señales independientes. Los pesos se pueden ajustar con la sección `FeedRecomendaciones` de la configuración o con variables de entorno, sin cambiar la consulta Cypher:

| Clave | Valor inicial | Uso |
|---|---:|---|
| `PesoLike` | 2 | Coincidencias de likes en Neo4j. |
| `PesoPuntuacion` | 3 | Coincidencias de puntuaciones de 3 o más en Neo4j. |
| `PesoEtiqueta` | 1 | Personas etiquetadas en ambas publicaciones. |
| `PesoAfinidadCategoria` | 4 | Afinidad de categoría, sin limitar la década. |
| `PesoCercania` | 3 | Cercanía a lugares de publicaciones con like. |
| `DistanciaDeReferenciaKm` | 50 | Escala de decaimiento del puntaje por distancia. |
| `DiasDeExclusionDeImpresiones` | 14 | Plazo que define una vista reciente. |
| `CantidadMaximaCandidatas` | 500 | Máximo de publicaciones que puede devolver el ranking. |

Por ejemplo, `FeedRecomendaciones__PesoLike=4` cambia ese peso al iniciar el backend. Los pesos admiten cero; el plazo, el máximo de candidatas y la distancia de referencia deben ser mayores que cero.

El frontend registra las publicaciones devueltas por cada página de “Para vos” con:

```http
POST /api/feed/impresiones
Authorization: Bearer <token>
Content-Type: application/json

{"publicacionesIds":[42,41,39]}
```

Los `GET` priorizan el usuario del JWT y admiten `usuarioId` por query como alternativa temporal. `POST /api/feed/impresiones` requiere JWT. Las descripciones del feed tienen un máximo de 300 caracteres.

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

Para recargar “Para vos”, se realiza otra petición sin cursor. Esto genera una nueva selección. Cada sesión conserva hasta 500 publicaciones por defecto, también cuando usa el listado general, y vence a los 30 minutos; si deja de estar disponible, el endpoint devuelve `410 Gone`.

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

### Notificaciones en tiempo real (SignalR)

El sistema de notificaciones utiliza SignalR sobre WebSockets. Al igual que el chat, el servidor puede enviar notificaciones al cliente en cualquier momento sin que este las solicite. Las notificaciones también se persisten en base de datos, por lo que el usuario puede recuperarlas aunque no esté conectado en el momento en que se generaron.

Ruta del Hub
/hubs/notificaciones

El token JWT se envía por query string, igual que en el chat:

ws://localhost:{puerto}/hubs/notificaciones?access_token=<TOKEN_JWT>
Eventos que el cliente debe escuchar
Evento	Payload	Cuándo se dispara
RecibirNotificacion	NotificacionDto	Cuando se genera una notificación nueva para el usuario conectado.

Endpoints HTTP de notificaciones

Todos requieren JWT en el header Authorization: Bearer <token>.

Método	|   Ruta	|   Descripción
|   GET	|   /api/notificaciones	|   Devuelve las notificaciones no expiradas del usuario autenticado, ordenadas de más reciente a más antigua. Devuelve lista vacía si no hay ninguna.
|   PATCH	|   /api/notificaciones/{id}/leida	|   Marca una notificación específica como leída e inicia el conteo de expiración de 7 días. Devuelve 404 si no existe o no pertenece al usuario.
|   PATCH	|   /api/notificaciones/leidas	|   Marca todas las notificaciones no leídas del usuario como leídas.

### Endpoints de usuarios
|   Método  |	Ruta    |	Descripción
|   GET |	/api/usuarios/por-username/{username}   |	Busca un usuario por su username exacto. Devuelve su id y username. Requiere JWT. Devuelve 404 si no existe ningún usuario con ese username.
