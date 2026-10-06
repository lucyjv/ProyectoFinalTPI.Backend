# ProyectoFinalTPI.Backend
Este es el repositorio del Backend para nuestro **Proyecto Final TPI**. La solución está desarrollada en **ASP.NET Core 9 (MVC)** bajo una arquitectura de **Persistencia Políglota**, combinando bases de datos relacionales, espaciales y orientadas a grafos para lograr el máximo rendimiento en nuestra red social.

---

## Base de datos

*   **PostgreSQL 17:** Almacena los datos maestros, perfiles de usuario y datos estructurados.
*   **PostGIS Extension:** Motor geoespacial acoplado a Postgres. Maneja coordenadas, puntos geográficos reales (`Point`) y búsquedas eficientes por radio de cercanía.
*   **Neo4j (Community Edition):** Base de datos de grafos. Gestiona de manera ultra veloz la red de conexiones.

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

*Nota: Las propiedades añadidas en **Neo4j** no requieren migraciones debido a que es una base de datos sin esquema (schema-less). Solo modifica la consulta Cypher dentro de los controladores de C#.*
