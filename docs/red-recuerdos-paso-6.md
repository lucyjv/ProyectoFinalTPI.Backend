# Red de recuerdos: reprocesamiento controlado (paso 6)

## Alcance

Permite analizar publicaciones existentes sin volver a subirlas. La vista previa es de solo lectura; el encolado requiere seleccionar IDs y versiones concretos. No hay un disparador que recorra automáticamente toda la base.

Los endpoints requieren JWT con `esAdmin=true` y sesión vigente. Un usuario sin sesión recibe 401; un usuario común, 403. La clave de Gemini sigue exclusivamente en el backend.

No requiere migraciones nuevas: se utiliza el trabajo único por `(PublicacionId, VersionContenido)` del paso 4. No se cambia el contenido, la versión de relaciones ni las asociaciones al encolar.

## 1. Previsualizar

```http
GET /api/admin/analisis-recuerdos/reprocesamiento?limite=10&despuesDeId=0&incluirFallidos=false
Authorization: Bearer <TOKEN_ADMIN>
```

```json
{
  "recuerdos": [
    { "publicacionId": 10, "versionContenido": 1, "titulo": "Festejos del 86", "estado": "noProgramado" },
    { "publicacionId": 15, "versionContenido": 2, "titulo": "México 86", "estado": "sinAnalizador" }
  ],
  "hayMas": true,
  "siguienteDespuesDeId": 15,
  "analisisDisponible": true
}
```

El límite predeterminado es 10 y admite de 1 a 25. La navegación ordena por Id ascendente: cuando `hayMas=true`, enviar `siguienteDespuesDeId` como `despuesDeId`. Este cursor es exclusivo y no es un número de página. La selección es una fotografía: puede cambiar antes de enviarla.

La vista previa funciona aunque Gemini o el worker estén deshabilitados; en ese caso `analisisDisponible=false`. No invoca al proveedor ni modifica la cola.

Son elegibles recuerdos originales visibles cuyo autor permite momentos automáticos y cuya **versión actual** no tiene trabajo o terminó en `sinAnalizador`. Con `incluirFallidos=true`, también se incluyen trabajos `fallido`.

Se excluyen ocultos, reposts, automatización desactivada, trabajos pendientes o procesando, completados, sin momento y descartados. Las reservas vencidas corresponden a la recuperación del worker, no a esta herramienta. No se fuerza un segundo análisis de versiones ya procesadas correctamente.

## 2. Encolar una selección

```http
POST /api/admin/analisis-recuerdos/reprocesamiento
Authorization: Bearer <TOKEN_ADMIN>
Content-Type: application/json

{
  "recuerdos": [
    { "publicacionId": 10, "versionContenido": 1 },
    { "publicacionId": 15, "versionContenido": 2 }
  ],
  "incluirFallidos": false,
  "intervaloSegundos": 10
}
```

Devuelve **202 Accepted**, que confirma el encolado y no la finalización del análisis:

```json
{
  "recuerdos": [
    { "publicacionId": 10, "versionContenido": 1, "resultado": "encolado", "motivo": null, "proximoIntento": "2026-10-21T15:00:10Z" },
    { "publicacionId": 15, "versionContenido": 2, "resultado": "reprogramado", "motivo": null, "proximoIntento": "2026-10-21T15:00:20Z" }
  ],
  "programados": 2,
  "omitidos": 0
}
```

Se admiten de 1 a 25 IDs distintos, todos con versión positiva. No se selecciona implícitamente el resto de la página. El intervalo admite de 10 a 3600 segundos y vale 10 por defecto. Los resultados se devuelven ordenados por Id.

El primer trabajo se programa después de un intervalo, y los siguientes se separan con ese intervalo. Un lote posterior toma como piso el último horario pendiente de la cola. Son fechas de elegibilidad, no una promesa de hora exacta de ejecución ni una reserva de cuota de Google. El limitador del proveedor y los reintentos por cuota continúan funcionando.

Los trabajos `sinAnalizador` y, si se solicita, `fallido`, se reutilizan: pasan a `pendiente`, con intentos en cero, sin error, versión de analizador ni reserva anterior. Se conserva el Id del trabajo. No se crean duplicados para la misma versión y no se borran decisiones del autor.

El proveedor real y el worker deben estar habilitados al encolar. Si falta alguno, responde **503 ProblemDetails** sin modificar la cola. No se valida la credencial mediante una llamada extra: el worker informa el resultado de la solicitud real.

## Cambios entre la vista previa y el envío

Cada recuerdo se vuelve a validar bajo bloqueo, junto a la versión actual. Si dejó de ser elegible, se omite y el resto del lote puede programarse:

| Motivo | Significado |
|---|---|
| `noDisponible` | No existe, está oculto o es un repost. |
| `automatizacionDesactivada` | El autor desactivó la automatización. |
| `versionDesactualizada` | Cambió el contenido desde la selección. Volver a previsualizar. |
| `yaProgramado` | Hay un trabajo pendiente o procesando; no se reinicia ni se pospone. |
| `yaAnalizado` | La versión terminó correctamente, con o sin momento. |
| `fallidoNoIncluido` | Es fallido y no se solicitó incluir fallidos. |
| `descartado` | El trabajo fue descartado; no se reactiva desde esta herramienta. |

Una omisión devuelve `resultado="omitido"` y `proximoIntento=null`. Si todos se omiten, el 202 contiene `programados=0`; el cliente debe mostrar ese resultado, no anunciar que hubo análisis.

Repetir el mismo POST mientras los trabajos están pendientes/procesando no duplica trabajos ni retrasa sus horarios. Dos lotes concurrentes se serializan con un bloqueo de PostgreSQL y vuelven a verificar cada recuerdo. Los lotes se confirman en una transacción: un error de persistencia revierte tanto las altas como las reprogramaciones del lote completo. Los motivos de omisión, en cambio, son resultados normales por elemento.

El formato de solicitud inválido devuelve 400; errores internos, 500 con ProblemDetails. Se registra en logs el administrador y las cantidades programadas/omitidas, sin textos del recuerdo ni claves. La tabla conserva el estado del último intento de esta versión, no un historial de cada ejecución: reprogramar reinicia el contador de intentos.

## 3. Seguir el resultado

Consultar `GET /api/publicaciones/{id}/analisis` mientras sea pendiente/procesando y recargar `GET /api/publicaciones/{id}/momentos` al finalizar, como en el paso 5. Guardar en el cliente los IDs y versiones devueltos por el lote. Si el GET muestra otra `versionContenido`, el contenido fue editado y ya no corresponde al trabajo seleccionado originalmente.

El análisis respeta los rechazos y las asociaciones manuales del autor al confirmar, incluso si cambiaron mientras Gemini estaba respondiendo. Encolar no los altera. Lugar y día siguen disponibles durante el procesamiento.

## Operación local

Desde la raíz del repositorio, una vista previa sin iniciar servidor, seeder, migraciones ni workers:

```powershell
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --project ProyectoFinalTPI.Backend --no-launch-profile -- --previsualizar-reprocesamiento --limite 5
```

Puede agregarse `--incluir-fallidos`. La salida de esta herramienta muestra solo IDs, versiones y estados; no muestra títulos, descripciones, claves ni datos de conexión. No encola ni llama a Gemini.

Para usar los endpoints, habilitar el proveedor y el worker, iniciar normalmente el backend, iniciar sesión como administrador y ejecutar GET/POST desde Swagger o la herramienta administrativa. Habilitar el worker permite consumir **cualquier trabajo pendiente de la cola**, incluidas publicaciones nuevas; no solo los seleccionados por esta herramienta. Revisar la cola antes de activar en un entorno con datos reales.

```powershell
dotnet user-secrets set "Gemini:Habilitado" "true" --project ProyectoFinalTPI.Backend
dotnet user-secrets set "AnalisisRecuerdos:Habilitado" "true" --project ProyectoFinalTPI.Backend
```

Comenzar con cinco recuerdos, revisar sus asociaciones y recién después seleccionar otro lote. El nivel gratuito conserva sus condiciones de privacidad y cuotas: ver el paso 5. No se activa facturación ni se cambia de modelo automáticamente. Los intentos máximos del worker también se aplican a los trabajos reprogramados; un lote de cinco podría consumir más de cinco solicitudes si hay reintentos.

## Verificación

Tests de repositorio, servicio y controller con nombres en español y Arrange/Act/Assert, siguiendo el patrón del proyecto. Las integraciones usan PostgreSQL/PostGIS temporal y analizador ficticio: permisos 401/403, paginación sin efectos, encolado y consumo sin recargar publicaciones, rechazos del autor, lotes concurrentes, cambios de versión/preferencias y rollback completo ante fallos. Ningún test automático consume la clave real.
