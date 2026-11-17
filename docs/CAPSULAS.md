# Cápsulas: backend y cómo probarlo

Rama: `Funcionalidad-Capsula`, creada desde `main` (`05cbf07`). Esta entrega implementa el backend; la pantalla «Mis cápsulas» y sus tarjetas por década quedan para el frontend.

## Qué permite

- Crear una cápsula con nombre (3–100 caracteres), descripción opcional (hasta 500) y visibilidad privada o pública. Es privada por defecto y puede empezar vacía.
- Agregar recuerdos originales propios ya publicados. Un recuerdo puede estar en varias cápsulas. Las citas/republicaciones y los recuerdos ajenos no se admiten.
- Al publicar un recuerdo nuevo, elegir varias cápsulas propias y guardar sus asociaciones en la misma transacción que la publicación, el consumo del cupo y su trabajo de análisis.
- Usar una portada automática, una foto de un recuerdo incluido o una foto subida desde el dispositivo. Admite la foto del modelo anterior y los nuevos adjuntos. Sin una selección ni fotos disponibles, devuelve `null` para la portada predeterminada.
- Recorrer un hilo cronológico por **fecha del recuerdo**, de más antiguo a más reciente; ante fechas iguales, ordenar por ID. No depende de la década seleccionada en la web.
- Ver en el mapa únicamente los recuerdos de esa cápsula, con paginación y límites geográficos globales para encuadrarlos.
- Editar, quitar recuerdos o eliminar la cápsula sin borrar las publicaciones originales.

La visibilidad privada protege la colección y sus datos; **no convierte en privados los recuerdos originales**, que siguen las reglas actuales de publicaciones. Solo su propietario administra la cápsula. Otros usuarios, incluso sin sesión, pueden recorrerla si es pública.

Los recuerdos ocultos o cuyo autor fue eliminado no aportan fotos, fechas, cantidad, hilo ni pines. Si el propietario fue eliminado, toda su cápsula deja de estar disponible. Agregar un recuerdo existente no dispara un nuevo análisis de Gemini ni consume otro cupo de publicación.

## Recorrido del código

1. `CapsulasController` obtiene el usuario desde el token. Los endpoints de escritura y «mías» requieren autenticación.
2. `CapsulaServicio` valida los datos, los permisos, los límites y los cursores de paginación.
3. `CapsulaRepositorio` consulta PostgreSQL aplicando la visibilidad también al hilo, la portada y el mapa. Las modificaciones se guardan en una transacción y usan el mismo bloqueo del autor que el alta con cupos.
4. Para mostrar el hilo se reutiliza `PublicacionServicio`: carga los detalles y las interacciones en bloque. Las etiquetas también se completan en bloque.
5. Al publicar, `PublicacionServicio` comprueba las cápsulas **antes de subir archivos**. `PersistenciaCapsulas` vuelve a comprobarlas al guardar, dentro de la transacción, para cubrir cambios concurrentes.

Se usan los proyectos ya existentes de entidades, DTOs, interfaces, repositorio y servicio. No se agregó una biblioteca ni un proveedor externo. Las cápsulas se guardan en PostgreSQL; no se duplican en Neo4j ni se agregan a la caché pública de perfiles. Sus respuestas indican `Cache-Control: no-store`; el middleware común conserva ese criterio en las respuestas de error.

## Endpoints

Todos respetan hasta tres segmentos después de `/api/`.

| Método | Ruta | Uso |
| --- | --- | --- |
| GET | `/api/capsulas/mias` | Cápsulas propias, incluidas privadas. Requiere sesión. |
| GET | `/api/capsulas?propietarioId=10` | Solo las públicas de ese usuario. |
| GET | `/api/capsulas/4` | Datos, portada, cantidad y período de una cápsula accesible. |
| GET | `/api/capsulas/4/portada` | Descarga de la foto subida, previa comprobación de visibilidad. Devuelve WebP, sin caché. |
| POST | `/api/capsulas` | Crear; el dueño sale del token. |
| PUT | `/api/capsulas/4` | Reemplazar nombre, descripción, visibilidad y selección de portada. |
| DELETE | `/api/capsulas/4` | Eliminar la cápsula y sus asociaciones. |
| GET | `/api/capsulas/recuerdos-disponibles?capsulaId=4` | Selector de recuerdos originales propios, con `yaAgregado`. El ID de cápsula es opcional. |
| POST | `/api/capsulas/4/recuerdos` | Agregar un lote de recuerdos propios. |
| DELETE | `/api/capsulas/4/recuerdos?publicacionId=11` | Quitar una asociación. |
| GET | `/api/capsulas/4/recuerdos` | Hilo cronológico paginado. |
| GET | `/api/capsulas/4/mapa` | Pines paginados y límites globales. |

Las cápsulas privadas ajenas y las inexistentes responden igual: **404**. Datos inválidos: **400**; escritura sin sesión: **401**. El alta devuelve **201** y la ubicación del recurso. Agregar, quitar o eliminar devuelve **204**.

### Crear o editar

```json
{
  "nombre": "Nuestros viajes",
  "descripcion": "Recuerdos de distintos años y lugares",
  "visibilidad": 0,
  "publicacionesIds": [11, 12],
  "portadaPublicacionId": 11,
  "portadaAdjuntoId": null
}
```

`visibilidad`: `0` privada, `1` pública (serialización numérica existente). En `PUT` no se envía `publicacionesIds`: el contenido se administra con los endpoints de asociaciones. Omitir o enviar `null` en ambos campos de portada vuelve a la selección automática. Si se elige un adjunto, debe ser una foto de la publicación indicada y esa publicación debe estar en la cápsula.

### Agregar recuerdos ya publicados

```json
{ "publicacionesIds": [11, 12, 13] }
```

Hasta 100 identificadores por operación. Se descartan duplicados; si una asociación ya existe, no se repite. Si un recuerdo del lote no es propio, original y visible, se rechaza todo el lote sin guardar parcialmente.

### Publicar dentro de cápsulas

El formulario `multipart/form-data` de publicación admite campos opcionales:

```text
CapsulasIds[0] = 4
CapsulasIds[1] = 7
```

También funciona en el alta de recuerdos de grupos. No enviarlos mantiene el comportamiento anterior. Se admiten hasta 100 IDs por formulario, todos de cápsulas propias. La selección no cambia quién es el autor ni los permisos del grupo.

### Paginación

Listados, selector e hilo: `limite` por defecto 20, máximo 50. Mapa: por defecto y máximo 200. Respuesta:

```json
{ "items": [], "siguienteCursor": null, "hayMas": false }
```

Para seguir, enviar el `siguienteCursor` recibido en `?cursor=...` (codificado como parámetro de URL). No construirlo ni reutilizarlo en otra cápsula, usuario o listado. Al cambiar el área del mapa se empieza sin cursor. Si se modifica una cápsula durante el recorrido, refrescar desde la primera página para ver el conjunto actualizado.

El mapa añade `limites` con `minLatitud`, `maxLatitud`, `minLongitud` y `maxLongitud`. Describe **toda la cápsula visible**, incluso cuando la página o el área consultada solo contiene parte de los pines. Para una cápsula vacía devuelve `null`. Para una sola ubicación, el frontend debe agregar margen al encuadre.

Filtro opcional: enviar los cuatro límites juntos. Las latitudes deben quedar entre −90 y 90; longitudes entre −180 y 180; mínimo menor que máximo. Para un área que cruce el antimeridiano, el frontend puede dividirla en dos consultas; no se admite un mínimo mayor que el máximo.

## Migración pendiente de aplicar

`20261116212648_AgregarCapsulas` crea:

- `capsulas`, con el propietario y las referencias opcionales de portada.
- `capsulas_publicaciones`, con clave compuesta para impedir recuerdos duplicados en una misma cápsula.
- Índices, claves foráneas y restricción de visibilidad.

No modifica ni elimina los datos actuales de publicaciones. Borrar una cápsula solo elimina sus asociaciones; borrar una publicación elimina sus asociaciones y libera las referencias de portada correspondientes.

La migración fue **generada, no aplicada**. No se tocó `Nostalgiar_Dev` ni producción. Desde la raíz del backend, cuando se confirme que los secretos apuntan a la base local correcta:

```powershell
dotnet ef database update --project ProyectoFinalTPI.Backend.Repositorio --startup-project ProyectoFinalTPI.Backend
```

En producción se mantiene el procedimiento manual acordado: revisar el script contra las migraciones realmente aplicadas, contar con respaldo y aplicar antes de desplegar el código que usa las tablas. No requiere nuevas variables en Azure ni secretos de Key Vault.

## Pruebas preparadas (sin ejecutar)

- `CapsulaServicioTests`: permisos, usuarios eliminados, validaciones, cursores, límites, hilo y mapa.
- `CapsulaPublicacionTests`: rechazo antes de subir multimedia, asociación única, consultas de detalles/interacciones en bloque y publicación desde grupos.
- `Integracion/CapsulasIntegracionTests`: APIs sobre PostgreSQL/PostGIS temporal, privacidad en todos los accesos, portadas de ambos modelos, paginación, exclusión de ocultos, borrado sin perder recuerdos, concurrencia, atomicidad del cupo y alta con formulario.

Se conserva la estructura con xUnit y Moq; las integraciones reutilizan `PostgisFixture` y `PublicacionesApiFactory`. Estas últimas requieren Docker y crean una base temporal por factory. No usan la conexión de desarrollo ni de producción.

Para que las ejecutes vos desde la raíz del backend:

```powershell
# Pruebas con mocks, sin Docker
dotnet test ProyectoFinalTPI.Backend.Tests --filter "FullyQualifiedName~CapsulaServicioTests|FullyQualifiedName~CapsulaPublicacionTests"

# Integración: requiere Docker encendido
dotnet test ProyectoFinalTPI.Backend.Tests --filter "FullyQualifiedName~CapsulasIntegracionTests"
```

La compilación correcta verifica los tipos y las referencias, pero no reemplaza la ejecución de las pruebas ni confirma por sí sola las consultas contra PostgreSQL.

## Portadas subidas desde el dispositivo

La migración adicional es `20261117193506_AgregarPortadaSubidaCapsula`. Agrega una referencia nullable en `capsulas` y una restricción que impide combinar una portada subida con la selección de una foto de un recuerdo. **Está generada, todavía no aplicada en desarrollo ni producción.** Aplicarla antes de levantar esta versión contra una base existente. No modifica las publicaciones ni los archivos de los recuerdos.

Se reutilizan las credenciales existentes de `Cloudinary:CloudName`, `Cloudinary:ApiKey` y `Cloudinary:ApiSecret`. No hay nuevos secretos, paquetes ni proyectos.

`POST /api/capsulas` y `PUT /api/capsulas/{id}` aceptan también `multipart/form-data`, con `Nombre`, `Descripcion`, `Visibilidad`, `Archivo` y, al crear, `PublicacionesIds[0]`, etc. No enviar las referencias de portada de un recuerdo junto con un archivo. La carga y los datos se envían en una misma solicitud; una falla no requiere crear otra cápsula desde el navegador. Las variantes multipart comparten ruta con JSON y están documentadas aquí; Swagger muestra la variante JSON para evitar operaciones duplicadas con el mismo método y ruta.

En una edición JSON, `conservarPortadaSubida` es `true` por defecto: una edición común no pierde la foto. Para volver a automática, enviar `false` y las referencias de recuerdo en `null`. Para usar un recuerdo, enviar su referencia (y `false`). `portadaSubida` indica en la respuesta si el frontend debe descargar la imagen por la API; nunca se entrega el ID de almacenamiento ni la URL firmada.

Se admiten JPEG, PNG y WebP de hasta 5 MiB. Se comprueban tamaño, firma y MIME; Cloudinary decodifica y convierte a WebP con un máximo de 1600×1600, calidad automática y sin metadatos. La foto se guarda como recurso `authenticated` incluso si la cápsula es pública. El backend firma y descarga la imagen; no redirige al navegador. La lectura vuelve a comprobar los permisos de la cápsula. Referencia del proveedor: [acceso a archivos](https://cloudinary.com/documentation/control_access_to_media).

Al reemplazar, volver a automática o eliminar, se limpia exclusivamente la portada subida anterior, después de confirmar el cambio en PostgreSQL. Si el guardado falla, se intenta retirar la foto nueva; la anterior permanece. Si la limpieza del proveedor falla, se registra una advertencia para revisión: no hay un worker de reintentos de limpieza en esta entrega. Una caída de red durante la propia subida también puede dejar un recurso huérfano y requiere revisar Cloudinary. No se eliminan imágenes de las publicaciones.

Pruebas nuevas preparadas: `CapsulaPortadaTests` (validaciones, permisos, compensación y limpieza) y un caso adicional en `CapsulasIntegracionTests` (multipart, privacidad, reemplazo, vuelta a automática y conservación de publicaciones). La integración reemplaza Cloudinary por un almacenamiento simulado; no usa credenciales reales.

```powershell
dotnet test ProyectoFinalTPI.Backend.Tests --filter "FullyQualifiedName~CapsulaPortadaTests"
```
