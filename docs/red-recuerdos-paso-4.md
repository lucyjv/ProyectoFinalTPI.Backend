# Red de recuerdos: cola persistente de análisis (paso 4)

## Qué queda implementado

La creación de un recuerdo original visible guarda la publicación y un trabajo pendiente en el mismo `SaveChanges` de PostgreSQL. EF confirma ambos dentro de su transacción: si falla la inserción del trabajo, tampoco queda guardada la publicación. El `POST /api/publicaciones` conserva su respuesta `201 { id }` y no invoca al analizador. Lugar y día siguen disponibles de inmediato.

El worker consume trabajos en segundo plano, uno por ciclo y con un scope nuevo. Puede haber varios workers/instancias: PostgreSQL reserva cada fila con `FOR UPDATE SKIP LOCKED`. Cada reserva tiene token y vencimiento; una instancia anterior no puede confirmar resultados después de que otra recupere el trabajo.

**Todavía no hay proveedor de IA.** `AnalizadorRecuerdoSinProveedor` permite ejecutar el circuito completo, termina como `sinAnalizador` y no agrega ni retira asociaciones. Los tests usan analizadores controlados para comprobar resultados, errores y carreras. La extracción de acontecimientos, su resolución/creación y el proveedor corresponden al paso 5.

## Modelo y versiones

`TrabajoAnalisisRecuerdo` contiene Id, PublicacionId, VersionContenido, estado, intentos, próxima ejecución, token/vencimiento de reserva, fechas UTC, código de error y versión del analizador. El índice único `(PublicacionId, VersionContenido)` impide duplicar un trabajo para la misma versión. Al eliminar el recuerdo, sus trabajos se eliminan en cascada.

`Publicacion.VersionContenido` empieza en 1 y es un token de concurrencia de EF. Es independiente de `VersionRelaciones`:

- Guardar un cambio real de título, descripción o fecha en una entidad cargada por EF incrementa VersionContenido y genera otro trabajo en la misma transacción.
- Una corrección de momentos cambia VersionRelaciones, sin generar trabajos de contenido.
- Los reposts y recuerdos ocultos no generan nuevos trabajos.
- Todavía no existe un endpoint para editar el texto del recuerdo. Las futuras ediciones deben cargar la entidad y guardarla con EF. SQL manual y `ExecuteUpdate` no pasan por esta preparación de `SaveChanges` y no deben usarse para editar contenido sin implementar su correspondiente encolado/versionado.
- Las publicaciones existentes reciben VersionContenido = 1 al migrar, pero no se encolan en masa. Sin trabajo muestran `noProgramado`. El [paso 6](red-recuerdos-paso-6.md) incorpora su procesamiento y la reprogramación de los trabajos `sinAnalizador` por lotes seleccionados.

## Procesamiento y consistencia

1. Reservar atómicamente un trabajo pendiente cuyo próximo intento ya llegó, o recuperar uno cuya reserva venció. Incrementar intentos y generar un token nuevo.
2. Leer el contenido solo si siguen vigentes la reserva, la versión, la visibilidad y la preferencia de automatización. Si dejaron de ser válidos, descartar sin llamar al analizador.
3. Invocar `IAnalizadorRecuerdo` con cancelación y tiempo máximo. Validar la salida antes de persistir: hasta 50 momentos distintos, Id positivos y confianza finita entre 0 y 1.
4. Tomar el mismo bloqueo por publicación que usan las correcciones del paso 3. Volver a comprobar reserva, versión de contenido y preferencias actuales.
5. Para un análisis real, reemplazar solamente las asociaciones automáticas. Mantener intactas las decisiones del autor, tanto activas como rechazadas. Una salida real vacía puede retirar vínculos automáticos anteriores; el analizador temporal sin proveedor no lo hace.
6. Guardar asociaciones, nueva VersionRelaciones si hubo cambios y estado final del trabajo en una sola transacción. Si algo falla, se revierte el resultado completo antes de registrar el fallo/reintento.

El contrato del analizador devuelve datos; no debe escribir asociaciones por su cuenta y debe respetar cancelación. El circuito admite resultados referidos a momentos ya resueltos; la resolución semántica se implementará detrás de este contrato en el paso 5.

Una edición durante el análisis invalida el resultado anterior. Una corrección puede desactivar todo el análisis o impedir vínculos concretos mediante rechazos; ambos se respetan al confirmar. Si se elimina el recuerdo durante el proceso, el resultado tardío se ignora.

## Recuperación y fallos

- Si el proceso se interrumpe, el trabajo permanece reservado hasta su vencimiento y luego puede recuperarse.
- Si un proceso interrumpido ya consumió el máximo de intentos, el siguiente consumidor lo marca fallido; no queda recuperándose indefinidamente.
- Los errores del proveedor y tiempos agotados se reintentan con espera exponencial y variación aleatoria de ±10 %. El tope nominal de espera es 300 s antes de esa variación.
- Las entradas/resultados inválidos fallan sin repetir un análisis que no puede persistirse.
- El cierre del host cancela el análisis y deja la reserva para recuperación; no se registra como un fallo del proveedor.
- Las confirmaciones con token viejo, reserva vencida o trabajo ya finalizado se ignoran.
- No se guardan títulos, descripciones, prompts ni mensajes arbitrarios del proveedor en el error del trabajo. Se usan códigos acotados: `analisis_error`, `tiempo_agotado`, `analisis_invalido`, `reintentos_agotados`.

Los logs del procesador muestran Id de trabajo y código. El meter .NET `Nostalgiar.AnalisisRecuerdos` registra `analisis.trabajos.procesados`, `analisis.trabajos.fallos` y `analisis.duracion` (ms). Conectar un exportador si el entorno necesita recolectarlos; esta entrega no crea un dashboard o endpoint de métricas.

## Consulta para frontend

```http
GET /api/publicaciones/10/analisis
```

Lectura pública de la versión actual del recuerdo visible:

```json
{
  "publicacionId": 10,
  "versionContenido": 1,
  "estado": "pendiente"
}
```

| Estado | Interpretación |
|---|---|
| `noProgramado` | La versión actual no tiene trabajo (por ejemplo, una publicación anterior a la migración). |
| `pendiente` | En espera o programado para reintento. |
| `procesando` | Un worker tiene la reserva. |
| `completado` | Se procesó una salida real con momentos detectados. Consultar el GET de momentos para conocer las asociaciones activas. |
| `sinMomento` | Una salida real no detectó momentos. No elimina decisiones del autor. |
| `sinAnalizador` | El circuito terminó sin proveedor configurado. Es el resultado normal del analizador temporal de esta entrega. |
| `descartado` | El trabajo dejó de ser aplicable y no impuso su resultado. |
| `fallido` | Se agotaron intentos o hubo una entrada/salida inválida. |
| `desactivado` | El autor desactivó asociaciones automáticas; tiene prioridad sobre el estado interno del trabajo. |

No se exponen códigos de error internos, intentos ni tokens de reserva. Un repost se resuelve al original visible. Id no positivo devuelve 400; recuerdo inexistente/oculto u original no disponible devuelve 404 con el contrato habitual ProblemDetails.

El frontend puede consultar cada 5 segundos mientras el detalle siga abierto y el estado sea `pendiente` o `procesando`, cancelando al cambiar de recuerdo. Al terminar, recargar `/api/publicaciones/{publicacionId}/momentos`. Las relaciones por lugar y día no deben esperar este proceso. El estado no confirma por sí solo una asociación: los rechazos del autor pueden impedir todos los momentos detectados. No interpretar `sinAnalizador` como ausencia de acontecimientos en el texto.

## Configuración

Valores predeterminados, configurables en la sección `AnalisisRecuerdos`:

```json
{
  "AnalisisRecuerdos": {
    "Habilitado": true,
    "IntervaloSegundos": 5,
    "ReservaSegundos": 120,
    "TiempoMaximoSegundos": 30,
    "MaxIntentos": 3,
    "ReintentoBaseSegundos": 10,
    "MaxCaracteresEntrada": 20000
  }
}
```

También se pueden usar variables como `AnalisisRecuerdos__Habilitado`. La reserva debe superar el tiempo máximo de análisis en más de 5 segundos; hay validación al iniciar. Habilitado = false detiene el consumo, pero no impide guardar nuevos trabajos. En `Testing` se desactiva el loop por defecto para evitar carreras con las pruebas; los tests ejecutan ciclos explícitos contra la cola real.

Desactivar y volver a activar la preferencia de un autor no reprograma un trabajo terminal. No se expone un endpoint público para vaciar, reintentar o reprocesar la cola.

## Migración y pruebas

`20261021160638_AgregarColaAnalisisRecuerdos` agrega VersionContenido y la tabla de trabajos con índices y restricciones. No cambia fechas de recuerdos ni crea asociaciones.

```powershell
dotnet ef database update --project ProyectoFinalTPI.Backend.Repositorio --startup-project ProyectoFinalTPI.Backend
dotnet test ProyectoFinalTPI.Backend.Tests/ProyectoFinalTPI.Backend.Tests.csproj --filter "FullyQualifiedName~TrabajoAnalisis|FullyQualifiedName~AnalisisRecuerdos"
```

Los tests siguen Arrange/Act/Assert y nombres en español. Las pruebas por capa verifican servicio, repositorio, controller y scopes del worker. Con PostgreSQL/PostGIS temporal se verifican creación sin invocar análisis, rollback si falla el encolado, reservas concurrentes, SKIP LOCKED, recuperación de tokens, versiones antiguas, decisiones del autor y confirmación atómica de resultados. Nunca usan la base de desarrollo.

Validación de esta entrega: 14 casos de repositorio, 13 de servicio, 2 del worker, 1 de controller y 14 de integración (44 nuevos). La regresión de la red de recuerdos, manejo de errores, repositorio de publicaciones, creación y reposts pasó con 282 casos, sin fallos ni omitidos. El modelo se verificó sincronizado con las migraciones.
