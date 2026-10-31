# Entorno local completo con Docker

Requiere Docker Desktop iniciado, con contenedores Linux, y los dos repositorios:

```text
Nostalgiar/
  ProyectoFinalTPI.Backend/   # compose.yaml y Dockerfiles
  Web/ProyectoFinalTPI/      # frontend Next.js y package-lock.json
```

Desde la raíz del backend:

```powershell
docker compose up --build -d
docker compose ps -a
```

La primera ejecución descarga imágenes, compila ambos proyectos, crea bases nuevas,
aplica todas las migraciones y carga `ProyectoFinalTPI.Backend/seed.json`.
`initialize` debe terminar con código 0; los otros cuatro servicios deben quedar healthy.
El frontend usa una compilación de producción; para incorporar cambios hay que reconstruir.

- Aplicación: http://localhost:3001
- API y Swagger: http://localhost:5199/swagger
- Cuenta de ejemplo: `miguel@example.com`, contraseña `password123`.
- El mapa usa modo 2D, sin token Mapbox.
- Las bases solo son accesibles dentro de la red Compose.

Las bases son propias de `nostalgiar-local`, sin importar datos ni credenciales de las
bases actuales. Los volúmenes `postgres-data` y `neo4j-data` conservan los datos.
El inicializador se puede repetir: no cambia contraseñas existentes ni duplica recuerdos.
Neo4j recibe nodos `Usuario` y `Publicacion` con las PK de PostgreSQL y restricciones
únicas. Los likes, seguimientos y demás relaciones se generan al usar la aplicación.
Si falla Neo4j después de cargar PostgreSQL, se puede repetir la inicialización.

## Configuración opcional

No hace falta copiar secretos para probar el mapa, login y recuerdos sin multimedia.
Los valores predeterminados son exclusivamente para este entorno local, enlazado a loopback.
Para configurar puertos o integraciones:

```powershell
Copy-Item .env.docker.example .env.docker
docker compose --env-file .env.docker up --build -d
```

Usar `--env-file .env.docker` también en los comandos posteriores si se creó ese archivo.
No sobrescribir el `.env` existente. Las variables propias de Compose usan prefijo
`DOCKER_`; no se leen User Secrets. `.env.docker` queda excluido de Git y de la imagen.
Si 3001 o 5199 están ocupados por procesos locales, detener esos procesos o cambiar
`FRONTEND_PORT` y `BACKEND_PORT` en `.env.docker` antes de construir.

La URL pública de API se incorpora al build de Next.js. Al cambiar `BACKEND_PORT`,
reconstruir el frontend. Abrir la aplicación con `localhost` para mantener la misma
dirección que usa la API y sus cookies de sesión.

Cloudinary requiere completar las tres variables de la plantilla para subir archivos.
Las imágenes del seed apuntan a sus URLs originales y pueden requerir internet.
Gemini y el worker de análisis están desactivados en este entorno por defecto.
Google Photos requiere configuración adicional de OAuth (cliente y URL de retorno);
no se configura con credenciales de la máquina de desarrollo automáticamente.

## Operación

```powershell
# Ver errores de inicialización y API
docker compose logs --tail 100 initialize backend

# Detener sin perder datos
docker compose down

# Volver a levantar
docker compose up -d

# Aplicar nuevas migraciones y completar datos sin duplicarlos
docker compose run --rm initialize

# Reconstruir después de modificar código o seed
docker compose up --build -d

# Consultar PostgreSQL
docker compose exec postgres psql -U nostalgiar -d nostalgiar

# Abrir Cypher Shell (solicita contraseña)
docker compose exec neo4j cypher-shell -u neo4j
```

`docker compose down --volumes` elimina permanentemente las dos bases de este entorno.
Usarlo solo cuando se quiera borrar todo y comenzar otra vez.

Este Compose es para desarrollo/demostraciones locales. Un despliegue público requiere
HTTPS, dominios, secretos propios, copias de seguridad y configuración de producción.
No se deben publicar las credenciales y cuentas de demostración.

Los Dockerfiles están en este repositorio; el Dockerfile de frontend usa el código de
su repositorio como contexto. Su archivo específico `frontend.Dockerfile.dockerignore`
excluye dependencias, builds y secretos según la [documentación de Docker](https://docs.docker.com/build/concepts/context/).
