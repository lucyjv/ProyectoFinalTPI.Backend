# Monitoreo de Nostalgiar con Application Insights

## Qué agrega esta rama

Peticiones y llamadas HTTP: duración, resultado y relación con la petición original.
PostgreSQL: sus actividades de Npgsql (sin exportar el texto SQL).
Redis: lectura/escritura de sesiones del feed, tiempo y errores; contador de fallback de lecturas públicas.
Feed Para vos: tiempo de sesión nueva/existente, interacciones, candidatos, ranking y carga de página.
Análisis de recuerdos: actividad por trabajo y exportación de los contadores y duración existentes. Los fallos llevan un código acotado (cuota, credenciales, configuración, etc.).

No se agregó instrumentación específica de Neo4j/Bolt ni de SignalR. Las llamadas HTTP a Gemini y Cloudinary se observan si su cliente usa HttpClient. Comprobarlo al validar, sin asumir cobertura de todos los SDK.

## Configuración

- `Monitoreo__Habilitado=true`: activa el exportador del código. Por defecto false, para poder desplegar primero con el agente actual.
- `APPLICATIONINSIGHTS_CONNECTION_STRING`: conservar la del recurso existente; no ponerla en Git.
- `Monitoreo__ProporcionMuestreo=0.2`: conserva aproximadamente el 20 % de los recorridos. Para una comprobación breve se puede usar 1 y luego volver a 0.2. El muestreo no reduce todas las métricas ni todos los logs y no garantiza conservar cada error.
- `OTEL_SERVICE_NAME=nostalgiar-api`: nombre del backend en los gráficos (opcional).

Los logs enviados con este proveedor son Warning o superiores. No se agregan cuerpos, encabezados, tokens, usuarios, ubicaciones ni contenido de recuerdos a las mediciones propias. Se eliminan query strings de URLs de trazas y atributos SQL antes de exportar. Los logs de excepciones ya existentes aún pueden incluir mensajes del proveedor: revisar una muestra antes de ampliar la captura. No habilitar SQL detallado ni captura de prompts.

## Paso a producción (coordinar con quien despliega)

1. Revisar cambios en esta rama. El usuario ejecuta los tests.
2. Integrar mediante PR a main; main despliega automáticamente. Inicialmente mantener `Monitoreo__Habilitado` ausente o false: el agente de Azure sigue a cargo.
3. Guardar los ajustes actuales del monitoreo, incluida la conexión al recurso, para poder volver atrás.
4. En Variables de entorno del App Service preparar, en el mismo guardado: `ApplicationInsightsAgent_EXTENSION_VERSION=disabled`, `XDT_MicrosoftApplicationInsights_Mode=disabled` y `Monitoreo__Habilitado=true`. Si existe `XDT_MicrosoftApplicationInsights_PreemptSdk`, quitarlo para no interceptar la integración del código. Conservar la cadena `APPLICATIONINSIGHTS_CONNECTION_STRING`.
5. Agregar `Monitoreo__ProporcionMuestreo=0.2` y aplicar el conjunto de cambios. Se reinicia el backend. Esto NO implica eliminar el recurso de Application Insights ni su workspace. No volver a pulsar Habilitar en el panel automático después de cambiar al código.
6. Verificar los datos siguientes antes de dar la tarea por terminada. No mantener dos mecanismos activos como solución a un fallo.

Volver atrás: `Monitoreo__Habilitado=false`, aplicar y reactivar la instrumentación automática de Azure con su conexión original. No modifica datos de la aplicación.

## Cómo comprobarlo

Durante una ventana breve se puede muestrear al 100 % para que el recorrido de prueba sea visible. Esperar unos minutos de ingesta.

- Abrir Para vos y cargar una segunda página. Debe haber una sola petición por cada llamada real y etapas `feed.*` dentro de su recorrido. No confundir reintentos del navegador con duplicados.
- Buscar `redis.sesion.crear` y `redis.sesion.leer` en los detalles.
- Ver las llamadas PostgreSQL sin texto SQL ni parámetros.
- Publicar un recuerdo de prueba y esperar su análisis. Debe verse `analisis.procesar`, la llamada HTTP a Gemini y métricas `analisis.*`. Consultar además `/api/publicaciones/{id}/analisis` y `/momentos` para validar el resultado funcional.
- Verificar que los errores reales del middleware se consultan en Azure. No provocar caídas ni exponer secretos para ensayar errores en producción.
- Revisar atributos de una muestra: sin valores de búsquedas, claves, cuerpos ni SQL.
- El endpoint de estado y las consultas funcionales no demuestran por sí solos que la telemetría se exporte: confirmar ambos lados.

En Registros del recurso Application Insights (no del workspace), ejemplos KQL:

```kusto
requests
| where timestamp > ago(30m)
| summarize total=sum(itemCount), fallos=sumif(itemCount, success == false), p95=percentile(duration, 95) by name
| order by p95 desc
```

```kusto
dependencies
| where timestamp > ago(30m)
| where name startswith "feed." or name startswith "redis.sesion." or name == "analisis.procesar"
| project timestamp, name, duration, success, operation_Id, customDimensions
| order by timestamp desc
```

```kusto
customMetrics
| where timestamp > ago(30m)
| where name startswith "feed." or name startswith "redis." or name startswith "analisis."
| project timestamp, name, value, valueSum, valueCount, customDimensions
| order by timestamp desc
```

```kusto
traces
| where timestamp > ago(30m) and severityLevel >= 2
| project timestamp, message, operation_Id, customDimensions
| order by timestamp desc
```

Si se consulta el workspace, usar las tablas AppRequests, AppDependencies, AppMetrics y AppTraces y sus columnas equivalentes.

## Seguimiento y crédito

Revisar volumen de datos en Application Insights y Log Analytics tras 24-48 horas y semanalmente. Configurar retención y límite diario según el crédito; el límite no es un tope de gasto exacto y puede detener registros. No crear alertas de pago sin revisar precio.

Propuesta inicial de alertas, a ajustar con tráfico real:
- 3 o más respuestas 5xx en 10 minutos. Los 4xx esperados no son incidentes por sí solos.
- Feed con p95 mayor a 2 segundos durante 15 minutos y al menos 20 peticiones. Sin tráfico suficiente, revisar manualmente.
- Fallos de credenciales/configuración de Gemini; cuota agotada repetida.
- Fallos repetidos de sesiones Redis o aumento del fallback.

Estas alertas son propuestas; no se crearon recursos ni reglas en Azure desde esta rama.

Referencias:
https://learn.microsoft.com/en-us/azure/app-service/tutorial-ai-agent-monitoring-dotnet
 https://learn.microsoft.com/en-us/dotnet/api/overview/azure/monitor.opentelemetry.aspnetcore-readme
https://learn.microsoft.com/en-us/azure/azure-monitor/logs/daily-cap
