# Catálogo de fotos para el juego de las décadas

`GET /api/juegos/decadas/fotos` es una consulta pública de solo lectura. Devuelve una lista con `publicacionId`, `url` y `decada`, o `[]` si no hay fotos candidatas.

```json
[{ "publicacionId": 123, "url": "https://res.cloudinary.com/ejemplo/image/upload/foto.jpg", "decada": 1980 }]
```

La consulta usa exclusivamente recuerdos originales y visibles de `elNostalgico` (nombre sin distinguir mayúsculas), y excluye cualquier título que contenga «Expo Proyecto», también sin distinguir mayúsculas. No incluye reposts, videos ni cuentas eliminadas. La década procede de la fecha del recuerdo y debe corresponder a una opción del juego: 1970, 1980, 1990, 2000, 2010 o 2020.

Las publicaciones antiguas aportan su foto principal. En las que tienen adjuntos se toman las fotos de esa colección y se descartan los videos, sin repetir la portada antigua. El servicio elimina URLs repetidas, vacías o que no sean HTTP/HTTPS.

El filtrado de base de datos está en `JuegoDecadasRepositorio`, las reglas del catálogo y el mapeo en `JuegoDecadasServicio`, y la entrada HTTP en `JuegoDecadasController`. No se agregaron entidades, migraciones ni cambios en los datos existentes. El frontend conserva las fotos locales como respaldo cuando la consulta no está disponible.

Pruebas específicas:

```sh
dotnet test ProyectoFinalTPI.Backend.Tests --filter "FullyQualifiedName~JuegoDecadas"
```

Las pruebas de integración usan PostgreSQL/PostGIS en un contenedor y una base temporal propia. No consultan ni modifican la base productiva.

## Arranque y migraciones después de cambiar de rama

El directorio `ProyectoFinalTPI.Backend/wwwroot` se conserva en Git mediante `.gitkeep`. Los manifiestos de archivos estáticos de ASP.NET pueden referenciarlo aunque la API no publique archivos propios.

Si una compilación anterior dejó referencias a una carpeta eliminada, actualizá la rama y ejecutá `dotnet clean`. Desde la carpeta del proyecto web (`ProyectoFinalTPI.Backend/ProyectoFinalTPI.Backend`), aplicá las migraciones indicando el proyecto de repositorio, donde está `ApplicationDbContext`:

```powershell
dotnet clean
dotnet ef database update --project ..\ProyectoFinalTPI.Backend.Repositorio --startup-project . --context ApplicationDbContext
dotnet run
```

No hay que agregar una migración para corregir una carpeta ausente.
