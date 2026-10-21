# Plan de acción para frontend: red de recuerdos de Nostalgiar

## Instrucción para el chat que implementará frontend

Implementar en el frontend existente la red de recuerdos de Nostalgiar y sus herramientas administrativas. Leer este documento completo y contrastar los contratos con el Swagger del backend. Investigar primero la estructura real del frontend, sus instrucciones locales, cliente HTTP, autenticación, componentes, rutas, manejo de estado y tests. Reutilizar esos patrones y la identidad visual actual; los nombres de componentes y rutas de UI de este documento son propuestas adaptables.

El alcance incluye los **13 endpoints** enumerados aquí: relaciones por día/lugar, exploración de momentos, correcciones del autor, estado del análisis, reprocesamiento por lotes y gestión de duplicados. Implementar las pantallas y recorridos completos que los consumen, no solamente funciones HTTP. No cambiar contratos del backend ni agregar endpoints, configuraciones de Gemini o claves al navegador. Si se encuentra una incompatibilidad real, documentar el caso y coordinarla antes de inventar un comportamiento.

Trabajar por las entregas indicadas, comprobar sus criterios de aceptación y mantener los tests con el patrón del repositorio frontend. Al terminar, informar endpoints integrados, pantallas, verificaciones realizadas y limitaciones pendientes. No ejecutar reprocesamientos ni fusiones sobre datos reales para probar la interfaz: usar fixtures y un entorno de prueba controlado.

## 1. Objetivo y reglas del producto

Hacer visible y navegable el slogan **“Un día. Un momento. Un lugar.”** desde el detalle de una publicación.

- **Este lugar:** recuerdos a una distancia inclusiva de 200 metros. No exige el mismo pin ni la misma fecha.
- **Este día:** recuerdos que además de compartir ese radio tienen el mismo día, mes y año del recuerdo en UTC. No es un aniversario de años distintos.
- **Este momento:** recuerdos asociados a un acontecimiento colectivo, aunque tengan otros lugares o fechas. No es una búsqueda genérica por palabras ni por categoría.

Las relaciones geográficas se calculan en backend; no calcularlas ni filtrar distancias de nuevo en frontend. El día y el lugar pueden compartir publicaciones: no eliminar una tarjeta del bloque lugar por aparecer también en día. Cada bloque representa su propio criterio.

La publicación se crea inmediatamente mediante el flujo existente. La detección de acontecimientos corre después en segundo plano. No agregar un paso obligatorio de selección de momento, no esperar a Gemini para publicar y no llamar al proveedor desde el navegador.

Las decisiones del autor tienen prioridad sobre la detección. Las variantes semánticas dudosas de acontecimientos requieren revisión administrativa; una similitud textual no autoriza una fusión automática.

## 2. Fuentes, conexión y alcance

- Usar la URL base configurable que ya tenga el frontend. El perfil HTTP local del backend usa `http://localhost:5199`; no dejarla fija en los componentes.
- Swagger UI: `/swagger`. Contrato OpenAPI: `/swagger/v1/swagger.json` sobre la URL base del backend.
- Los ejemplos y tipos de este documento reflejan el código del backend al preparar la integración. Si el backend cambia, contrastar con su Swagger y los DTO de origen.
- Los JSON usan camelCase. Los enums de multimedia/categoría se serializan numéricamente; las etiquetas de origen y estado de esta funcionalidad son strings explícitos.
- Reutilizar el flujo de sesión, refresh y logout existente. Las operaciones protegidas llevan `Authorization: Bearer <accessToken>`.
- Consultar o reutilizar los datos de `GET /api/auth/me` para identificar al usuario y su `esAdmin`. Ese endpoint es soporte de autenticación, no uno de los 13 endpoints nuevos de esta integración.
- El `POST /api/publicaciones` existente conserva su contrato. Integrarlo con la actualización posterior del detalle, sin reimplementar el formulario ni inventar campos de IA.

Las lecturas públicas funcionan sin sesión. Las correcciones requieren ser autor del original; la administración requiere `esAdmin=true` y sesión vigente. Los guards de frontend mejoran la experiencia; backend sigue siendo la autoridad sobre los permisos.

## 3. Inventario completo de endpoints

| Nº | Método y ruta | Acceso | Resultado normal y uso |
|---|---|---|---|
| 1 | `GET /api/publicaciones/{id}/relaciones` | Público | 200: vista inicial de día y lugar, hasta seis tarjetas por bloque. |
| 2 | `GET /api/publicaciones/{id}/relacionadas` | Público | 200: continuación por `tipo=dia` o `tipo=lugar`, cursor y límite. |
| 3 | `GET /api/momentos` | Público | 200: catálogo/búsqueda por nombre o alias, con cursor. |
| 4 | `GET /api/momentos/{id}` | Público | 200: detalle, jerarquía, alias y resolución al canónico si fue fusionado. |
| 5 | `GET /api/momentos/{id}/publicaciones` | Público | 200: publicaciones del momento, con opción de descendientes y cursor. |
| 6 | `GET /api/publicaciones/{id}/momentos` | Público, sesión opcional | 200: momentos activos, origen, preferencia, permisos y versión para corregir. |
| 7 | `PUT /api/publicaciones/{id}/momentos` | Autor del original | 200: reemplaza la selección completa y devuelve su estado actualizado. |
| 8 | `GET /api/publicaciones/{id}/analisis` | Público | 200: estado del análisis de la versión actual. |
| 9 | `GET /api/admin/analisis-recuerdos/reprocesamiento` | Administrador | 200: vista previa paginada de recuerdos elegibles, sin efectos. |
| 10 | `POST /api/admin/analisis-recuerdos/reprocesamiento` | Administrador | 202: resultado del encolado de una selección concreta. |
| 11 | `GET /api/admin/momentos/{origenId}/duplicados` | Administrador | 200: array de posibles duplicados, no una página con cursor. |
| 12 | `GET /api/admin/momentos/{origenId}/fusion` | Administrador | 200: vista previa de fusión con `destinoId` por query. |
| 13 | `POST /api/admin/momentos/{origenId}/fusion` | Administrador | 200: confirma la fusión con las versiones revisadas. |

### Query strings y límites

| Endpoint | Parámetros |
|---|---|
| Relacionadas | `tipo`: `lugar` por defecto o `dia`; `limite`: 1–50, por defecto 20; `cursor`: opcional. |
| Catálogo de momentos | `busqueda`: opcional, máximo 200 caracteres; `limite`: 1–50, por defecto 20; `cursor`: opcional. |
| Publicaciones de un momento | `incluirDescendientes`: true por defecto; `limite`: 1–50, por defecto 20; `cursor`: opcional. |
| Vista previa de reprocesamiento | `limite`: 1–25, por defecto 10; `despuesDeId`: entero no negativo, por defecto 0; `incluirFallidos`: false por defecto. |
| Duplicados de un momento | `limite`: 1–50, por defecto 20. No tiene cursor ni `hayMas`. |
| Vista previa de fusión | `destinoId`: identificador positivo de otro momento. |

Los cursores de las primeras tres listas son opacos. Pasarlos con `URLSearchParams` o codificación equivalente, sin decodificarlos, modificarlos ni convertirlos en números de página. No enviarlos como cadena `null` o vacía para la primera página.

Al cambiar publicación/tipo/búsqueda/momento/opción de descendientes, reiniciar la lista y el cursor. Nunca reutilizar el cursor de una consulta con otros filtros. Una página de día y otra de lugar tienen cursores independientes. El cursor que viene en la vista inicial de seis tarjetas sirve para continuar ese mismo bloque.

## 4. Contratos TypeScript de referencia

Adaptar la ubicación de los tipos al proyecto. Reutilizar los tipos existentes que sean equivalentes, verificando su forma real. Los tipos internos de entrada/salida de Gemini y las reservas del worker **no son contratos de frontend**.

```ts
type FechaHoraIso = string;
type FechaCalendario = string; // YYYY-MM-DD; sin zona horaria.
type Multimedia = 0 | 1; // 0 = Foto, 1 = Video.
type Categoria = 0 | 1 | 2 | 3 | 4 | 5 | 6;
// Lugares, Música, Cine, Televisión, Videojuegos,
// Acontecimientos, Recuerdos_Personales, respectivamente.

interface Pagina<T> {
  items: T[];
  siguienteCursor: string | null;
  hayMas: boolean;
}

interface RecuerdoRelacionado {
  id: number;
  titulo: string;
  descripcionBreve: string;
  fecha: FechaHoraIso;
  urlMultimedia: string;
  tipoMultimedia: Multimedia;
  autorId: number;
  autorUsername: string;
  nombreLugar: string;
  distanciaMetros: number;
  mismoDia: boolean;
  mismoLugar: true;
}

interface RelacionesRecuerdo {
  publicacionId: number;
  radioMetros: 200;
  dia: Pagina<RecuerdoRelacionado>;
  lugar: Pagina<RecuerdoRelacionado>;
}

interface PublicacionResumen {
  id: number;
  titulo: string;
  descripcionBreve: string;
  fecha: FechaHoraIso;
  categoria: Categoria;
  urlMultimedia: string;
  tipoMultimedia: Multimedia;
  autor: { id: number; username: string };
}

interface MomentoResumen {
  id: number;
  nombre: string;
  fechaInicio: FechaCalendario | null;
  fechaFin: FechaCalendario | null;
  momentoPadreId: number | null;
}

interface MomentoDetalle extends MomentoResumen {
  versionCatalogo: number;
  redirigidoDesdeId: number | null;
  descripcion: string;
  padre: MomentoResumen | null;
  hijos: MomentoResumen[];
  alias: string[];
}

interface MomentosPublicacion {
  publicacionId: number;
  versionRelaciones: number;
  permitirMomentosAutomaticos: boolean;
  puedeCorregir: boolean;
  momentos: Array<{
    momento: MomentoResumen;
    origen: "automatico" | "autor";
  }>;
}

interface CorregirMomentos {
  momentoIds: number[];
  permitirMomentosAutomaticos: boolean;
  versionRelaciones: number;
}

type EstadoAnalisis =
  | "noProgramado" | "pendiente" | "procesando" | "completado"
  | "sinMomento" | "sinAnalizador" | "descartado" | "fallido"
  | "desactivado";

interface AnalisisRecuerdo {
  publicacionId: number;
  versionContenido: number;
  estado: EstadoAnalisis;
}

interface RecuerdoSeleccionado {
  publicacionId: number;
  versionContenido: number;
}

interface VistaReprocesamiento {
  recuerdos: Array<RecuerdoSeleccionado & {
    titulo: string;
    estado: "noProgramado" | "sinAnalizador" | "fallido";
  }>;
  hayMas: boolean;
  siguienteDespuesDeId: number | null;
  analisisDisponible: boolean;
}

interface SolicitudReprocesamiento {
  recuerdos: RecuerdoSeleccionado[];
  incluirFallidos: boolean;
  intervaloSegundos: number;
}

type MotivoOmision =
  | "noDisponible" | "automatizacionDesactivada"
  | "versionDesactualizada" | "yaProgramado" | "yaAnalizado"
  | "fallidoNoIncluido" | "descartado";

interface ResultadoReprocesamiento {
  recuerdos: Array<RecuerdoSeleccionado & {
    resultado: "encolado" | "reprogramado" | "omitido";
    motivo: MotivoOmision | null;
    proximoIntento: FechaHoraIso | null;
  }>;
  programados: number;
  omitidos: number;
}

interface MomentoCatalogo {
  id: number;
  nombre: string;
  fechaInicio: FechaCalendario | null;
  fechaFin: FechaCalendario | null;
  versionCatalogo: number;
}

interface CandidatoDuplicado {
  momento: MomentoCatalogo;
  similitud: number;
  motivo: "nombreOAliasCoincidente" | "textoSimilar";
}

interface VistaFusion {
  origen: MomentoCatalogo;
  destino: MomentoCatalogo;
  publicacionesAfectadas: number;
  decisionesEnConflicto: number;
  hijosATrasladar: number;
  yaFusionado: boolean;
}

interface SolicitudFusion {
  destinoId: number;
  versionOrigen: number;
  versionDestino: number;
}

interface ResultadoFusion {
  origenId: number;
  destinoId: number;
  publicacionesAfectadas: number;
  decisionesEnConflicto: number;
  versionDestino: number;
  yaFusionado: boolean;
}

interface ProblemaApi {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>; // Validación de ASP.NET.
}
```

No unificar a la fuerza `RecuerdoRelacionado` con `PublicacionResumen`: en uno el autor es plano y en el otro es un objeto. Si se reutiliza una tarjeta visual, agregar adaptadores explícitos para ambas formas y conservar los campos de relación.

## 5. Entrega 1: cliente API, tipos y manejo común

### Implementación

1. Revisar el cliente HTTP actual y sumar funciones para los 13 endpoints. Mantener la URL base y autenticación existentes.
2. Todas las funciones deben aceptar cancelación cuando lo permita el cliente y conservar los cuerpos/respuestas tipados.
3. Reutilizar los interceptores de sesión. No almacenar la clave de Gemini ni duplicar el mecanismo de refresh.
4. Implementar lectura tolerante de errores: ProblemDetails y errores de validación, pero también 401/403 sin cuerpo y fallos de conexión sin respuesta JSON.
5. Construir query strings con codificación segura. Omitir parámetros opcionales ausentes.
6. Identificar las familias de caché de relaciones, catálogo, detalle/publicaciones de momento, asociaciones por publicación, análisis y administración.

Las claves de caché deben incluir los filtros y el ámbito completos. El GET de momentos de una publicación depende del usuario por `puedeCorregir`: incluir identidad de sesión o invalidar esa familia al cambiar de usuario. No conservar permisos derivados de otra sesión.

### Aceptación

- Se pueden invocar los 13 contratos desde una capa común, sin HTTP disperso en componentes.
- Los cursores con caracteres especiales se transmiten intactos.
- Una respuesta 401/403 vacía no produce un error al intentar leer JSON.
- Cambiar filtros descarta respuestas viejas y páginas previas.

## 6. Entrega 2: los tres bloques en el detalle del recuerdo

### Pantalla y datos

Integrar en el detalle existente, con una composición equivalente a:

```text
Detalle del recuerdo
  Este día       → misma fecha completa y dentro de 200 m
  Este momento   → acontecimientos colectivos asociados
  Este lugar     → recuerdos dentro de 200 m
```

Adaptar orden y diseño al frontend, manteniendo los tres conceptos reconocibles. En móvil pueden ser bloques verticales; en escritorio, filas o carruseles accesibles. Las tarjetas deben permitir abrir el recuerdo usando su ruta existente.

Al abrir el detalle, cargar de forma independiente relaciones, momentos y análisis mediante endpoints 1, 6 y 8. Un error o espera de IA no debe bloquear la publicación ni los bloques geográficos. Cancelar respuestas de una publicación anterior al navegar a otra.

El GET de relaciones ya trae seis tarjetas por bloque. “Ver más” o “Cargar más” continúa con el endpoint 2 usando el cursor del bloque, `tipo` correspondiente y su propia lista. No reiniciar desde la primera página ni compartir cursor entre bloques. Agregar páginas conservando el orden del backend; evitar dobles clics y deduplicar defensivamente por Id dentro de cada lista.

El bloque momento muestra los acontecimientos activos del endpoint 6 como chips o tarjetas que navegan a su página. No convertir categorías o palabras del texto en momentos en el cliente. La indicación `origen` puede presentarse como “Detectado automáticamente” o “Elegido por el autor” cuando ayude a entender/corregir la asociación.

### Vacíos, permisos y reposts

- Día: “Todavía no hay otros recuerdos de ese día en este lugar”.
- Lugar: “Todavía no hay otros recuerdos cerca de este lugar”.
- Momento: ajustar el vacío al estado de análisis; no tratar un análisis pendiente o fallido como ausencia confirmada de acontecimientos.
- Las listas vacías son respuestas normales 200, no errores.
- Los GET resuelven reposts al original. Usar el `publicacionId` devuelto como referencia del original para consultar/corregir sus asociaciones.
- El PUT no permite corregir directamente el Id del repost. Si corresponde, ofrecer “Corregir momentos del recuerdo original” y operar sobre ese original con sus permisos/versiones.

### Fechas

La fecha histórica que determina “Este día” es el día UTC del recuerdo. No dejar que `new Date(...).toLocaleDateString()` con la zona local cambie el día mostrado. Usar el mecanismo del proyecto con UTC explícito o una representación de fecha calendario adecuada.

Las fechas de un momento son `YYYY-MM-DD`, no instantes. Pueden ser nulas. Un intervalo del 1 de enero al 31 de diciembre del mismo año puede representar precisión anual: mostrar el año sin presentar esos extremos como fechas exactas conocidas del acontecimiento.

### Aceptación

- Se distinguen día, momento y lugar en la navegación real.
- Se muestran fotos y videos con el comportamiento de las tarjetas actuales.
- Un recuerdo puede aparecer en día y lugar sin desaparecer de uno de los bloques.
- “Ver más” mantiene el orden y no repite la primera página.
- Los estados de carga, vacío y error de cada bloque son independientes.

## 7. Entrega 3: seguimiento del análisis automático

Mientras el detalle esté abierto, consultar el endpoint 8 aproximadamente cada cinco segundos **solo en `pendiente` o `procesando`**. Evitar solicitudes superpuestas; programar la próxima al completar la anterior, o usar las capacidades de polling de la librería existente.

Cancelar al desmontar, cambiar de publicación o dejar de necesitar el seguimiento; pausar cuando la página esté oculta y revalidar al volver. Limitar reintentos de errores de red y ofrecer una acción manual de recarga. No mantener polling infinito sobre fallos de red ni sobre estados terminales.

| Estado | Comportamiento sugerido |
|---|---|
| `pendiente` | “Buscando momentos relacionados…”; continuar seguimiento. |
| `procesando` | “Analizando este recuerdo…”; continuar seguimiento. |
| `completado` | Detener seguimiento y recargar las asociaciones actuales. |
| `sinMomento` | Detener; “No se identificaron acontecimientos colectivos en este recuerdo”. Las asociaciones manuales pueden seguir existiendo. |
| `noProgramado` | Detener; el recuerdo todavía no tiene trabajo para esta versión. No ofrecer un botón de análisis que no exista en backend. |
| `sinAnalizador` | Detener; informar que la detección automática no estuvo disponible, sin confundirlo con `sinMomento`. |
| `fallido` | Detener; mostrar un mensaje breve de indisponibilidad y permitir volver a consultar el estado. No iniciar un reprocesamiento desde la vista pública. |
| `descartado` | Detener; recargar asociaciones si corresponde y evitar anunciar una detección aplicada. |
| `desactivado` | Detener; mostrar que la detección automática está desactivada para el recuerdo. |

Al terminar, forzar actualización del endpoint 6 para evitar conservar el resultado cacheado de antes del análisis. `completado` no garantiza que haya momentos activos: los rechazos del autor pueden haber impedido las asociaciones. El endpoint 6 es la fuente de verdad de los vínculos que se muestran.

Conservar `versionContenido` junto al estado consultado. Si cambia, corresponde a otra versión del texto; no adjudicar el resultado de una versión anterior a la nueva. No confundir esta versión con las de edición de asociaciones o catálogo.

### Aceptación

- Publicar sigue siendo inmediato y el estado se actualiza después en el detalle.
- Lugar/día permanecen utilizables mientras el análisis está en curso.
- Al navegar rápido entre recuerdos no aparecen respuestas del recuerdo anterior.
- Los estados terminales detienen el polling.
- `sinMomento` conserva y muestra cualquier asociación manual que devuelva el backend.

## 8. Entrega 4: catálogo y página de acontecimiento

Agregar rutas de frontend equivalentes a `/momentos` y `/momentos/:id`, respetando las convenciones del proyecto.

### Catálogo

- Consumir endpoint 3 para catálogo y búsqueda por nombre/alias.
- Aplicar debounce de aproximadamente 300 ms y cancelar búsquedas anteriores.
- Reiniciar páginas al cambiar `busqueda`. Conservar su valor al continuar con `siguienteCursor`.
- Mostrar nombre, período cuando exista y acceso al detalle.
- Un resultado vacío es normal; no crear un momento nuevo desde esta pantalla.

### Detalle

- Consumir endpoint 4 para nombre, descripción, período, alias, padre e hijos directos.
- Navegar al padre/hijos sin inventar nodos de jerarquía.
- Consumir endpoint 5 para publicaciones, ordenadas por fecha del recuerdo e Id descendentes.
- Permitir incluir o excluir descendientes. Al cambiar la opción, reiniciar lista y cursor.
- No volver a ordenar por fecha de subida en frontend.

### IDs fusionados

Si el endpoint 4 devuelve `id` diferente al Id de la URL, normalizar la ruta al canónico mediante reemplazo de historial, sin añadir un salto de navegación innecesario. El backend responde 200; no esperar un 301/302.

La opción más simple es resolver primero el detalle y cargar luego las publicaciones con el Id canónico. Si había consultas/cursor de la ruta anterior, cancelarlos y reiniciar esa lista al cambiar al canónico. Un cursor obtenido con el Id anterior no debe enviarse luego con el nuevo Id.

### Aceptación

- Buscar un alias permite llegar al acontecimiento canónico.
- Abrir una URL con un Id fusionado termina en el detalle y publicaciones del canónico sin bucles.
- La paginación de descendientes no mezcla consultas con la opción contraria.
- Se distinguen 404, catálogo vacío y falla de conexión.

## 9. Entrega 5: corrección de momentos por el autor

Mostrar “Corregir momentos” únicamente cuando el endpoint 6 devuelva `puedeCorregir=true`. Utilizar un modal o panel accesible con la selección completa actual, búsqueda del catálogo y control de automatización.

El buscador usa el endpoint 3. Mantener seleccionados los momentos ya elegidos aunque no estén en la página actual de búsqueda. Admitir hasta 50 IDs distintos. No permitir creación libre de acontecimientos desde este selector: no existe un endpoint público para eso.

Ejemplo de guardado mediante endpoint 7:

```json
{
  "momentoIds": [12, 18],
  "permitirMomentosAutomaticos": true,
  "versionRelaciones": 4
}
```

Los tres campos son obligatorios. `versionRelaciones` debe ser la obtenida al cargar la selección que la persona está editando.

- El PUT **reemplaza la selección completa**; no es un parche para agregar un solo momento.
- Los vínculos actuales que se omiten quedan rechazados por el autor.
- `momentoIds=[]` es válido.
- Lista vacía y automatización false: quitar la selección y desactivar automatización.
- Lista vacía y automatización true: rechazar los vínculos actuales, manteniendo permitidas futuras detecciones.
- Cambiar la preferencia a true no garantiza encolar por sí mismo un análisis nuevo. No prometer “volver a analizar ahora” ni llamar a un endpoint inexistente.

Al guardar, deshabilitar doble envío y consumir la respuesta actualizada. Ante 409, informar que cambiaron las relaciones, recargar y pedir una nueva decisión. **No actualizar el número de versión y reenviar silenciosamente la selección vieja.** Conservar el borrador como referencia si ayuda, pero no aplicarlo automáticamente sobre el estado nuevo.

Si el análisis termina mientras hay un borrador abierto, no sobrescribir la edición local; avisar que hay una actualización o resolver el conflicto al guardar. El GET y el PUT deben operar sobre el original cuando el detalle proviene de un repost.

Invalidar las asociaciones del original y las listas de publicaciones de momentos que puedan haber cambiado. Al abrir de nuevo el editor, usar el estado vigente del servidor.

### Aceptación

- Un usuario ajeno al original no ve controles de corrección.
- Una selección que abarca varias páginas de búsqueda se envía completa.
- Quitar todos los momentos no produce validación errónea.
- Un 409 de autor se resuelve recargando, sin sobrescribir decisiones recientes.
- Los cambios se reflejan en el detalle y en las páginas de los momentos afectados.

## 10. Entrega 6: administración del reprocesamiento

Crear una pantalla protegida para administradores, con una ruta equivalente a `/admin/recuerdos/reprocesamiento`.

### Selección y vista previa

- Consumir endpoint 9.
- Mostrar título, Id, versión del contenido y estado elegible; permitir selección explícita.
- Checkbox `incluirFallidos`, inicialmente false. Al cambiarlo, limpiar o revalidar selección y reiniciar la paginación.
- Mantener como máximo 25 recuerdos distintos seleccionados, aun si se seleccionan entre páginas.
- “Seleccionar todos” debe referirse a la página visible y respetar el máximo; no a toda la base.
- `siguienteDespuesDeId` es un cursor numérico exclusivo, distinto de los cursores de las listas públicas.
- Si `analisisDisponible=false`, permitir revisar candidatos y deshabilitar el envío explicando que el análisis no está disponible. No ofrecer controles de configuración ni de API key desde el navegador.

### Encolado

Enviar al endpoint 10 exactamente los IDs y versiones elegidos:

```json
{
  "recuerdos": [
    { "publicacionId": 49, "versionContenido": 1 },
    { "publicacionId": 50, "versionContenido": 1 }
  ],
  "incluirFallidos": false,
  "intervaloSegundos": 10
}
```

El intervalo admite de 10 a 3600 segundos. Antes del envío, presentar el resumen concreto de la selección y el intervalo. No autoprocesar la siguiente página ni crear una cadena infinita de lotes.

El 202 representa el resultado del encolado. Mostrar `programados`, `omitidos` y el resultado de cada fila, incluidos sus horarios o motivos. No anunciar que todos los recuerdos fueron analizados.

| Motivo de omisión | Mensaje o acción |
|---|---|
| `noDisponible` | El recuerdo ya no está disponible para esta operación. |
| `automatizacionDesactivada` | El autor desactivó la automatización. |
| `versionDesactualizada` | Cambió el contenido; volver a previsualizar. |
| `yaProgramado` | Ya estaba pendiente/procesando; no se duplicó. |
| `yaAnalizado` | La versión ya fue analizada. |
| `fallidoNoIncluido` | No se solicitó incluir fallidos. |
| `descartado` | El trabajo no admite reactivación desde esta herramienta. |

Un 202 con `programados=0` es un resultado normal que debe mostrarse como tal. Un 503 indica que no se encoló por indisponibilidad de configuración; refrescar la vista previa y mostrar el error.

### Seguimiento

No existe un endpoint de estado por lote ni un `loteId`. Conservar la selección devuelta y consultar el endpoint 8 por publicación, solo para filas pendientes/procesando, con concurrencia acotada y pausas. No lanzar 25 solicitudes simultáneas cada cinco segundos.

Si el estado consultado tiene otra `versionContenido`, indicar que corresponde a una edición posterior. Mostrar resultados terminales y permitir abrir el detalle del recuerdo. Refrescar los candidatos tras el encolado para que no sigan figurando trabajos que ya están pendientes.

### Aceptación

- Un usuario común no puede acceder a la pantalla ni consumir sus datos.
- La vista previa nunca encola por sí misma.
- Se respetan máximo, intervalo y selección concreta entre páginas.
- Se distinguen encolado, reprogramado, omitido y finalización del análisis.
- 202 parcial, 202 sin programados y 503 tienen tratamiento visible y correcto.

## 11. Entrega 7: administración de duplicados y fusiones

Crear una pantalla protegida equivalente a `/admin/momentos/duplicados`.

### Elegir origen y destino

1. Buscar/seleccionar el origen usando el catálogo público del endpoint 3.
2. Consumir endpoint 11 para sugerencias de posibles duplicados.
3. Mostrar nombre, período, similitud y motivo como ayuda para revisar.
4. Permitir buscar un destino en el catálogo aunque no aparezca entre las sugerencias.
5. Presentar con claridad: **“Se conservará [destino]; [origen] quedará como referencia al destino”**.

La similitud va de 0 a 1 y es textual. No mostrarla como “probabilidad de que sea el mismo acontecimiento”, ni disparar la fusión por superar un umbral. Los alias genéricos pueden sugerir eventos distintos. El catálogo no tiene por qué traer pares previamente agrupados; el endpoint toma un origen y devuelve candidatos.

### Revisar y confirmar

Consumir endpoint 12 al seleccionar el par. Mostrar ambos nombres, fechas, IDs, versiones, publicaciones afectadas, conflictos de decisiones e hijos que se trasladarán.

Explicar antes de confirmar que se conservarán las referencias anteriores y los alias, que prevalecerá el rechazo del autor ante conflictos y que la operación no tiene deshacer desde la interfaz/backend actual. No prometer que las publicaciones afectadas equivalen al número de tarjetas visibles: también puede haber vínculos rechazados.

Enviar al endpoint 13 las versiones exactas de esa vista previa:

```json
{
  "destinoId": 2,
  "versionOrigen": 1,
  "versionDestino": 1
}
```

Deshabilitar doble envío. La UI no debe fusionar en forma optimista. Confirmar el resultado del servidor antes de actualizar rutas y catálogos.

- En 200, mostrar el destino canónico y cantidades afectadas.
- `yaFusionado=true` significa que la operación ya estaba realizada; mostrar el destino sin anunciar movimientos nuevos.
- En 409, recargar la vista previa. Puede ser versión vieja, período incompatible, relación de ancestro/descendiente o una redirección cambiada. No reintentar con nuevas versiones sin revisión del administrador.
- En 400/404, revisar selección y datos. En 500, no anunciar una fusión exitosa; backend revierte la transacción.

Después de fusionar, invalidar catálogo, detalles y páginas de publicaciones de momentos, candidatos/vistas previas y asociaciones por publicación que estén cacheadas. La respuesta no enumera todos los IDs de publicaciones afectadas: no inventar una invalidación precisa basada solo en las que se veían en pantalla. Invalidar esas familias y recargar las consultas activas de manera controlada.

### Aceptación

- No se puede confirmar sin una vista previa vigente del par elegido.
- Cambiar origen/destino descarta la vista previa anterior y sus versiones.
- No se fusionan automáticamente sugerencias parecidas.
- Se muestran 409, operación ya realizada y errores sin declarar éxito falso.
- Tras fusionar no se ofrecen duplicados retirados como si siguieran siendo momentos activos.
- Una URL antigua sigue navegando al canónico.

## 12. Versiones, errores y estados compartidos

### Versiones que no deben confundirse

| Campo | Alcance | Dónde se utiliza |
|---|---|---|
| `versionContenido` | Versión del título/descripción/fecha del recuerdo | Estado del análisis y selección de reprocesamiento. |
| `versionRelaciones` | Versión de los vínculos y preferencias del recuerdo | PUT del autor. Una fusión también puede incrementarla. |
| `versionCatalogo` | Versión de los metadatos del acontecimiento | Vista previa y confirmación administrativa de fusión. |

No calcular ninguna de estas versiones en el cliente. Usar los valores devueltos por backend. Las tres pueden evolucionar independientemente.

### Errores HTTP

| Código | Tratamiento |
|---|---|
| 400 | Mostrar validación; corregir selección/filtros o reiniciar un cursor inválido. No repetir la misma petición en bucle. |
| 401 | Reutilizar la recuperación de sesión existente. Si no se recupera, pedir login o volver a vista pública. No reenviar silenciosamente mutaciones con estado/versiones que ya no se revisaron. |
| 403 | Mostrar falta de permisos; quitar controles que dejaron de estar habilitados. No intentar renovar sesión repetidamente para obtener acceso administrativo. |
| 404 | Mostrar recurso no disponible y permitir regresar. No anunciar borrado definitivo si el backend también usa 404 para ocultos/no disponibles. |
| 409 | Recargar el estado o vista previa correspondiente y requerir una nueva decisión. |
| 415 | Corregir la integración: PUT/POST deben usar JSON y `Content-Type: application/json`. |
| 500 | Error recuperable de UI, conservar navegación/datos previos cuando sea posible y permitir reintento consciente. |
| 503 | Reprocesamiento no disponible; no afirmar que quedó encolado. |

No todos los endpoints exponen todos los códigos; consultar Swagger para cada operación. El estado `fallido` del análisis llega dentro de un 200 del endpoint de estado: no es un 500 del polling.

Mostrar mensajes comprensibles. No exponer stack traces, prompts ni datos de configuración. El `traceId` puede conservarse para soporte. Los mensajes técnicos de infraestructura deben tener un fallback amigable.

## 13. Entrega 8: pruebas y validación de punta a punta

Usar las herramientas de test y los patrones ya existentes en el frontend. Priorizar comportamientos observables, no snapshots o tests que solo repitan la implementación. Si un caso usa backend real, emplear una base/entorno de prueba y proveedor ficticio; no depender de Gemini ni de su cuota.

### Casos mínimos de comportamiento

1. Lectura anónima de relaciones y momentos, con tarjetas vacías y con contenido.
2. Día y lugar superpuestos sin eliminación entre bloques; paginación independiente.
3. Continuación con cursor especial y reinicio ante cambio de filtros.
4. Publicación inmediata seguida de pendiente → procesando → completado y recarga de asociaciones.
5. `sinMomento` con asociaciones manuales existentes; sinAnalizador/fallido/desactivado con mensajes distintos.
6. Cancelación al navegar y ausencia de respuestas viejas en otro detalle.
7. Cambio de usuario sin reutilizar `puedeCorregir` de la sesión anterior.
8. Autor agrega, quita, envía selección vacía y modifica automatización con el cuerpo completo correcto.
9. 409 al corregir: recarga visible sin reenvío automático del borrador.
10. Repost: consulta resuelta al original y corrección dirigida al Id original, no al repost.
11. Detalle de momento con Id fusionado: 200 → ruta canónica, reinicio de cursor y sin bucle.
12. Catálogo por alias y cambio de descendientes con reinicio de páginas.
13. Administración: acceso denegado al usuario común y sin solicitudes protegidas innecesarias.
14. Reprocesamiento parcial: 202 con programados/omitidos y motivos; también programados=0 y 503.
15. Seguimiento de lote que detecta una `versionContenido` nueva sin atribuirle el resultado anterior.
16. Fusión: selección de par → vista previa → POST con versiones revisadas → actualización de caches.
17. Fusión ya realizada y 409 por versión/período/jerarquía, sin mensajes de éxito incorrectos.
18. 401/403 con cuerpo vacío y 400 con errores de validación sin romper el cliente HTTP.
19. Fecha del recuerdo cerca de medianoche UTC mostrada en el día correcto, y DateOnly sin corrimientos locales.
20. Accesibilidad: teclado, foco de modales, etiquetas de formularios, estados de carga anunciables y diseño móvil.

### Datos de prueba

Preparar fixtures de: recuerdo con relaciones por día y lugar; recuerdo con momento automático; autor con asociación aceptada y rechazada; momento sin publicaciones; momento con hijos; momento fusionado; cada estado de análisis; lote con omisiones; y fusión con conflicto entre aceptación y rechazo.

No depender de que la base local ya tenga esos datos. Al cerrar el trabajo de backend, el catálogo local todavía estaba vacío y los recuerdos 49–53 habían terminado `sinMomento`. Eso es un resultado válido y no ofrece por sí solo ejemplos visuales de todas las pantallas. No volver a analizar esos IDs automáticamente para intentar llenar la UI.

## 14. Orden de trabajo y condición de finalización

Orden recomendado, con entregas revisables:

1. Cliente HTTP, contratos y errores comunes.
2. Tres bloques en el detalle y continuación de día/lugar.
3. Seguimiento del análisis y actualización de asociaciones.
4. Catálogo y página de momento, con jerarquía y canónicos.
5. Corrección del autor y conflictos de versiones.
6. Administración del reprocesamiento, incluida su lectura de resultados.
7. Administración de duplicados, vista previa y fusión.
8. Pruebas de los recorridos, accesibilidad y verificación responsive.

Se considera terminado cuando los 13 endpoints tienen un uso real en UI, las pantallas públicas y administrativas respetan permisos, los estados vacíos/errores/conflictos tienen tratamiento visible y los recorridos anteriores están verificados. Incluir en la entrega final una matriz de los 13 endpoints con pantalla consumidora y evidencia de validación. No dar por terminada la integración si solo se crearon tipos, funciones API o pantallas estáticas.

## 15. Referencias de backend

Documentos complementarios del mismo directorio:

- `red-recuerdos-paso-1.md`: reglas y relaciones geográficas.
- `red-recuerdos-paso-3.md`: exploración y corrección del autor.
- `red-recuerdos-paso-4.md`: estados y cola de análisis.
- `red-recuerdos-paso-5.md`: detección de acontecimientos y sus límites.
- `red-recuerdos-paso-6.md`: reprocesamiento por lotes y omisiones.
- `red-recuerdos-paso-7.md`: prevención de duplicados, fusión y referencias canónicas.

Backend verificado con 397 tests de la regresión de esta funcionalidad, incluyendo PostgreSQL temporal. La migración `20261021183413_AgregarIdentidadYFusionMomentos` ya fue aplicada en la base local de trabajo. Otros entornos deben tener las migraciones correspondientes antes de integrar. Este documento no implica que frontend ya esté implementado ni probado.
