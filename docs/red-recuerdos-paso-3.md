# Red de recuerdos: momentos y correcciones (paso 3)

## Endpoints

Las lecturas son públicas. Enviar JWT al leer los momentos de una publicación si se quiere obtener `puedeCorregir` para la sesión actual. Solo el PUT requiere autenticación y ser el autor.

| Método / ruta | Uso |
|---|---|
| `GET /api/momentos?busqueda=...&limite=20&cursor=...` | Búsqueda por nombre o alias. Sin búsqueda lista todos. |
| `GET /api/momentos/{id}` | Detalle, padre, hijos directos y alias. |
| `GET /api/momentos/{id}/publicaciones?incluirDescendientes=true&limite=20&cursor=...` | Recuerdos del acontecimiento y, opcionalmente, de sus descendientes. |
| `GET /api/publicaciones/{id}/momentos` | Asociaciones activas, origen, preferencia y versión para editar. |
| `PUT /api/publicaciones/{id}/momentos` | Reemplaza la selección completa del autor, conservando rechazos. |

Los endpoints de lugar/día del paso 1 conservan su contrato. Para mostrar "Parte de este momento", consultar el GET de momentos de la publicación en paralelo a las relaciones de lugar/día.

Esta entrega no crea un catálogo inicial ni expone creación de acontecimientos por HTTP. Los momentos existentes se pueden explorar y seleccionar; su generación automática desde título/descripción se implementará en la entrega de análisis. Las fusiones y redirecciones a momentos canónicos quedan para esa etapa.

## Explorar

La búsqueda admite hasta 200 caracteres y no distingue mayúsculas. En los alias se normalizan también tildes y espacios, como en el paso 2. La coincidencia de nombres es textual; no es una búsqueda semántica. Se ordena por Id ascendente.

Detalle ilustrativo:

```json
{
  "id": 2,
  "nombre": "Argentina campeón 1986",
  "fechaInicio": "1986-06-29",
  "fechaFin": "1986-06-29",
  "momentoPadreId": 1,
  "descripcion": "La consagración en México.",
  "padre": {
    "id": 1,
    "nombre": "Mundial de México 1986",
    "fechaInicio": "1986-05-31",
    "fechaFin": "1986-06-29",
    "momentoPadreId": null
  },
  "hijos": [],
  "alias": ["Campeones del 86"]
}
```

Las fechas del acontecimiento son fechas calendario `YYYY-MM-DD`, opcionales; no son instantes UTC. No desplazar su día al aplicar el huso local. `padre` es null en una raíz; `hijos` son solo los directos.

La búsqueda devuelve una página de resúmenes con `id`, `nombre`, `fechaInicio`, `fechaFin` y `momentoPadreId`. Las publicaciones de un momento usan `PublicacionResumenDto`: `id`, `titulo`, `descripcionBreve` (hasta 180 caracteres en esta consulta), `fecha`, `categoria`, `urlMultimedia`, `tipoMultimedia` y `autor: { id, username }`. `categoria` y `tipoMultimedia` son enums numéricos. `fecha` es la fecha UTC del recuerdo y debe mostrarse conservando el día UTC, como en el paso 1.

Los recuerdos se ordenan por **fecha del recuerdo descendente e Id descendente**. No es la fecha de subida. Solo se incluyen originales visibles con vínculos activos. Una publicación asociada a un padre y a varios hijos aparece una sola vez. `incluirDescendientes` es true por defecto; false limita a asociaciones directas.

Ambos listados devuelven:

```json
{
  "items": [],
  "siguienteCursor": null,
  "hayMas": false
}
```

`limite` admite 1-50, por defecto 20. Omitir `cursor` en la primera página y continuar con el valor recibido usando `URLSearchParams`. No reutilizar un cursor entre búsquedas, momentos, tipos de listado o valores de `incluirDescendientes`. No hay total ni número de página. Los listados no representan una instantánea fija; refrescar sin cursor y deduplicar por Id al anexar si los datos cambian.

## Leer antes de editar

```http
GET /api/publicaciones/10/momentos
Authorization: Bearer <token>
```

Respuesta ilustrativa:

```json
{
  "publicacionId": 10,
  "versionRelaciones": 3,
  "permitirMomentosAutomaticos": true,
  "puedeCorregir": true,
  "momentos": [
    {
      "momento": {
        "id": 2,
        "nombre": "Argentina campeón 1986",
        "fechaInicio": "1986-06-29",
        "fechaFin": "1986-06-29",
        "momentoPadreId": 1
      },
      "origen": "automatico"
    }
  ]
}
```

`origen` es `automatico` o `autor`; no se expone la confianza del analizador. Las asociaciones rechazadas no se muestran, pero se conservan internamente. Un autor puede volver a seleccionar uno de esos momentos mediante la búsqueda.

Si se consulta un repost, el GET devuelve el estado de su original visible y `publicacionId` identifica ese original. Para guardar usar ese Id; el PUT directo sobre un repost devuelve 400. Si el origen o su original están ocultos/no existen, la lectura devuelve 404. `puedeCorregir` solo es true para el autor identificado; el servidor vuelve a verificar permisos al guardar.

## Guardar la selección completa

```http
PUT /api/publicaciones/10/momentos
Authorization: Bearer <token>
Content-Type: application/json
```

```json
{
  "momentoIds": [2, 3],
  "permitirMomentosAutomaticos": true,
  "versionRelaciones": 3
}
```

Los tres campos son obligatorios. Se permiten hasta 50 Id positivos y distintos. La versión debe ser la última recibida en el GET/PUT, no una cantidad de momentos calculada en el cliente. El usuario se toma del JWT; no se recibe un autor en el body.

- Los momentos seleccionados quedan activos con origen `autor`, sin confianza ni versión de IA.
- Los vínculos existentes omitidos quedan rechazados por el autor. El análisis posterior no puede reactivarlos.
- Para "Este recuerdo no pertenece a ningún momento", enviar `momentoIds: []` y `permitirMomentosAutomaticos: false`.
- Una selección vacía con automatización true rechaza los vínculos actuales, pero permite detectar otros acontecimientos nuevos en el futuro.
- Desactivar la automatización no borra los seleccionados; impide nuevas escrituras automáticas sobre el recuerdo.
- Volver a habilitarla no elimina los rechazos anteriores ni dispara todavía un trabajo de análisis. La cola se incorpora en el paso 4.
- Un momento seleccionado previamente rechazado puede reactivarse explícitamente por el autor.

El PUT exitoso devuelve `200` y el mismo formato de estado actualizado. Reemplazar el estado local con esa respuesta. Guardar la misma selección y preferencia con la versión vigente no duplica vínculos ni incrementa la versión. Enviar una versión vieja devuelve 409 incluso si se repite un request anterior: recargar antes de volver a guardar.

Ejemplo de cliente (tipos abreviados):

```typescript
interface EstadoMomentos {
  publicacionId: number;
  versionRelaciones: number;
  permitirMomentosAutomaticos: boolean;
  puedeCorregir: boolean;
  momentos: Array<{
    momento: { id: number; nombre: string; fechaInicio: string | null;
               fechaFin: string | null; momentoPadreId: number | null };
    origen: "automatico" | "autor";
  }>;
}

async function guardarMomentos(
  estadoLeido: EstadoMomentos, ids: number[], permitir: boolean,
  token: string, signal?: AbortSignal
): Promise<EstadoMomentos> {
  const respuesta = await fetch(
    `${API_BASE_URL}/api/publicaciones/${estadoLeido.publicacionId}/momentos`, {
      method: "PUT",
      headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
      body: JSON.stringify({ momentoIds: ids, permitirMomentosAutomaticos: permitir,
                            versionRelaciones: estadoLeido.versionRelaciones }),
      signal
    }
  );
  if (!respuesta.ok) {
    const problema = await respuesta.json().catch(() => ({}));
    throw Object.assign(new Error(problema.detail ?? "No se pudo guardar"), {
      status: respuesta.status, traceId: problema.traceId
    });
  }
  return respuesta.json();
}
```

Ante 409, mostrar "Las asociaciones cambiaron; revisá la selección actualizada", recargar el GET y dejar al autor revisar antes de reenviar. No reintentar automáticamente con la versión nueva, porque podría rechazar una asociación que el autor todavía no vio. Deshabilitar Guardar mientras se procesa el PUT y descartar respuestas al cambiar de recuerdo.

## Errores

| Estado | Significado |
|---|---|
| 400 | Id, límite, cursor o selección inválidos; campos obligatorios ausentes; intento de corregir un repost. |
| 401 | PUT sin JWT válido. |
| 403 | El usuario autenticado no es el autor. |
| 404 | Momento/recuerdo no existe o el recuerdo no está disponible. En PUT también si algún Id seleccionado no existe. |
| 409 | La versión cambió desde la lectura del editor. No se aplicó la selección. |
| 500 | Fallo inesperado; conservar la selección local para reintentar tras resolver el problema. |

Validaciones, conflictos y errores de aplicación usan ProblemDetails (`status`, `title`, `detail`, `instance`, `traceId`); el binding puede incluir `errors` por campo. La autenticación 401 y rutas no coincidentes pueden no tener ese cuerpo: basarse primero en el estado HTTP. Una selección que incluya un momento inexistente falla completa, sin rechazar ni guardar otros vínculos.

## Migración y pruebas

`AgregarCorreccionMomentos` agrega a Publicaciones `PermitirMomentosAutomaticos` (true para existentes) y `VersionRelaciones` (0 para existentes). Conserva todas las asociaciones previas. Cada cambio efectivo, sea del autor o automático, incrementa la versión. Los cambios y correcciones toman el mismo bloqueo transaccional por publicación.

```powershell
dotnet ef database update --project ProyectoFinalTPI.Backend.Repositorio --startup-project ProyectoFinalTPI.Backend
dotnet test ProyectoFinalTPI.Backend.Tests/ProyectoFinalTPI.Backend.Tests.csproj --filter "FullyQualifiedName~ExploracionMomentos|FullyQualifiedName~MomentosControllerTests|FullyQualifiedName~MomentosPublicacionControllerTests"
```

Los tests siguen nombres en español y Arrange/Act/Assert. Hay pruebas por capa y HTTP con PostgreSQL/PostGIS temporal, incluyendo permisos reales JWT, selección vacía, rechazos persistentes, reactivación, errores sin efectos parciales, paginación de descendientes y correcciones concurrentes. No requieren ni modifican la base de desarrollo.

Validación de esta entrega: 13 casos de repositorio, 22 de servicio, 13 de controllers y 14 de integración (62 nuevos). La regresión conjunta de los pasos 1-3 y el manejo de excepciones pasó con 180 casos, sin fallos ni omitidos. Se comprobó además que el modelo no tiene cambios sin migración.
