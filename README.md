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

### 1. Variables de Entorno y Credenciales
`appsettings.json` conserva las claves compartidas, pero no contiene credenciales. En desarrollo, `WebApplication.CreateBuilder` carga automáticamente ASP.NET Core User Secrets cuando el entorno es `Development`. El proyecto ya incluye `UserSecretsId`; no es necesario inicializarlo nuevamente.

Configurar las siguientes claves en **Manage User Secrets** del proyecto web en Visual Studio, o importar un objeto JSON mediante entrada estándar a `dotnet user-secrets set --project ProyectoFinalTPI.Backend`. No guardar ese JSON dentro del repositorio ni mostrar sus valores en logs.

| Clave de User Secrets | Variable de entorno para producción |
|---|---|
| `ConnectionStrings:PostgresConnection` | `ConnectionStrings__PostgresConnection` |
| `Neo4j:Password` | `Neo4j__Password` |
| `Cloudinary:CloudName` | `Cloudinary__CloudName` |
| `Cloudinary:ApiKey` | `Cloudinary__ApiKey` |
| `Cloudinary:ApiSecret` | `Cloudinary__ApiSecret` |

La cadena PostgreSQL debe contener la conexión completa a la base de datos local. User Secrets se almacena fuera del repositorio y no está cifrado; se utiliza únicamente para desarrollo. En producción, configurar las variables mediante el mecanismo de secretos del entorno de despliegue. `.env` está ignorado por Git, pero no se carga automáticamente.

Para ejecutar en desarrollo desde PowerShell, en la raíz del repositorio:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project ProyectoFinalTPI.Backend --no-launch-profile
```

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
