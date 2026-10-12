# Integración de publicaciones con PostgreSQL/PostGIS

Requiere Docker Desktop con el motor de contenedores Linux iniciado.

Desde la raíz del proyecto:

```powershell
dotnet test ProyectoFinalTPI.Backend.Tests/ProyectoFinalTPI.Backend.Tests.csproj --filter FullyQualifiedName~PublicacionesIntegracionTests
```

Testcontainers descarga la imagen `postgis/postgis:16-3.4` en la primera ejecución
y levanta un contenedor temporal con puerto y contraseña generados.
`PostgisFixture` comparte ese servidor entre los casos de la clase.

Cada `PublicacionesApiFactory` usa una base distinta con nombre
`publicaciones_test_<guid>`, aplica las migraciones y configura
`UseNpgsql` con `UseNetTopologySuite`. Las peticiones HTTP recorren
el controlador, servicio y repositorio reales. La base se elimina al terminar
el caso y el contenedor se elimina al terminar la clase.

La conexión se obtiene exclusivamente del contenedor: no se usa la base
de desarrollo ni se necesitan credenciales en appsettings.
Cloudinary sigue reemplazado por `MediaFake`; estos GET no consultan Neo4j.

Si Docker no está disponible, las pruebas fallan al preparar el contenedor;
no pasan silenciosamente a InMemory. Los tests originales de
`CrearPublicacionTests.cs` y `CompartirPublicacionTests.cs` conservan su
configuración anterior.
