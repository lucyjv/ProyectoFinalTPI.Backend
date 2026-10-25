# Red de recuerdos: Gemini y acontecimientos colectivos (paso 5)

## Qué hace

El worker del paso 4 puede analizar título y descripción con Gemini para identificar hasta tres acontecimientos colectivos. No cambia el POST de publicaciones: se publica inmediatamente y el análisis continúa en segundo plano. Un cumpleaños, paseo o lugar por sí solo no genera un momento.

Gemini devuelve propuestas con nombre canónico, año del acontecimiento, confianza, evidencia literal y alias usados en el recuerdo. Recibe hasta 40 candidatos del catálogo, con hasta ocho alias cada uno, seleccionados por palabras del texto. Puede elegir uno de esos IDs o proponer uno nuevo. No recibe otras publicaciones, usuarios, coordenadas ni multimedia. La fecha histórica del recuerdo se envía como contexto, sin imponerla como fecha del acontecimiento.

Antes de guardar se validan estructura, límites, confianza, IDs ofrecidos y que la evidencia y los alias aparezcan literalmente en el título o descripción. Solo se aceptan propuestas con confianza >= 0,85, configurable. Una respuesta inválida o bloqueada no equivale a una detección vacía y no retira vínculos existentes.

El repositorio vuelve a verificar reserva, versión del contenido, visibilidad y preferencia del autor. Resuelve las propuestas dentro de la misma transacción que confirma los vínculos y el trabajo. El bloqueo de catálogo evita que dos workers creen simultáneamente el mismo nombre canónico. Reutiliza un nombre existente o su alias normalizado, compatible con el año; si la identidad es ambigua no elige arbitrariamente. Varias propuestas para el mismo Id se convierten en un vínculo con la mayor confianza. Las decisiones del autor, incluso los rechazos, se mantienen.

Los nuevos momentos tienen un intervalo del 1 de enero al 31 de diciembre del año identificado: representa **precisión anual**, no fechas exactas conocidas del acontecimiento. No se infiere el día de la final a partir del recuerdo. Los momentos existentes conservan sus fechas y jerarquía; el modelo no crea padres ni fusiona eventos automáticamente.

Ejemplo: recuerdos que describen el triunfo argentino de México 86 se canonicalizan como `Argentina campeón del mundo 1986`. El campeonato de 2022 es otro momento. Una referencia solo al torneo no basta para afirmar que habla del triunfo argentino.

## Modelo y gratuidad

El modelo inicial configurable es `gemini-3.5-flash-lite`. Al implementar este paso, Google lo publica con nivel gratuito para entrada y salida estándar. El costo y los límites dependen también del proyecto y su nivel en AI Studio; elegir un nombre de modelo no garantiza gratuidad si se habilita facturación. No hay cambio automático a otro modelo ni activación de facturación. Consultar [precios](https://ai.google.dev/gemini-api/docs/pricing) y [límites del proyecto](https://ai.google.dev/gemini-api/docs/rate-limits).

En el nivel gratuito, Google puede usar los contenidos para mejorar sus productos. Para evaluar, usar datos ficticios; revisar privacidad y los [términos de Gemini](https://ai.google.dev/gemini-api/terms) antes de habilitar el envío de recuerdos de personas. Guardar una clave no habilita automáticamente el análisis.

## Configuración

Desde la raíz del repositorio:

```powershell
dotnet user-secrets set "Gemini:ApiKey" "TU_CLAVE_REAL" --project ProyectoFinalTPI.Backend
```

La clave se mantiene fuera del repositorio y del frontend. En un servidor usar el almacén de secretos del entorno, por ejemplo `Gemini__ApiKey`. Los User Secrets se cargan en Development.

Valores de `appsettings.json`:

```json
{
  "Gemini": {
    "Habilitado": false,
    "Modelo": "gemini-3.5-flash-lite",
    "ConfianzaMinima": 0.85,
    "IntervaloMinimoSegundos": 6
  }
}
```

Para activar el procesamiento real local después de evaluar:

```powershell
dotnet user-secrets set "Gemini:Habilitado" "true" --project ProyectoFinalTPI.Backend
```

También debe estar habilitado `AnalisisRecuerdos:Habilitado`. Si Gemini está deshabilitado, se conserva el analizador sin proveedor del paso 4. Si está habilitado sin clave, la validación falla al iniciar. El tiempo máximo y la cantidad de reintentos se configuran en `AnalisisRecuerdos`.

Para pasar a otro modelo compatible de Gemini basta cambiar `Gemini:Modelo`; para usar un proveedor diferente, implementar `IAnalizadorRecuerdo` y registrar esa implementación. El proveedor devuelve datos: el catálogo y las asociaciones siguen siendo responsabilidad del repositorio. La cola, los endpoints y el frontend no dependen del modelo.

## Prueba real aislada

```powershell
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --project ProyectoFinalTPI.Backend --no-launch-profile -- --probar-gemini
```

Este comando hace hasta tres solicitudes con ejemplos ficticios: Argentina campeón 1986, un paseo familiar sin acontecimiento y Argentina campeón 2022. Usa un catálogo vacío, no abre la base, no inicia el servidor ni workers y no modifica publicaciones. Funciona aunque `Gemini:Habilitado` sea false, porque el comando solicita expresamente esta prueba. Imprime los resultados y la versión del analizador; no imprime la clave. Ante un error de cuota/configuración termina sin cambiar de modelo ni reintentar. Las pruebas automáticas habituales usan HTTP simulado y no consumen Gemini.

## Cuotas y fallos

Se utiliza REST `generateContent` con salida JSON restringida mediante esquema y la clave en `x-goog-api-key`, sin parámetros secretos en la URL ni redirecciones. El texto del recuerdo viaja separado de las instrucciones del sistema y se trata como datos no confiables. La salida se limita a 64 KiB.

El intervalo mínimo entre solicitudes se comparte entre scopes del mismo proceso. No es un limitador distribuido: al ejecutar varias instancias se debe dividir el presupuesto del proyecto entre ellas o agregar una cuota centralizada. El valor inicial de seis segundos no asegura cumplir cualquier cuota de AI Studio.

Un 429 programa el reintento del trabajo respetando `Retry-After`, con espera mínima de 60 segundos y máxima de 24 horas. Las credenciales inválidas y los errores de modelo/configuración se consideran permanentes; los 5xx se reintentan con el backoff del paso 4. Los intentos siguen acotados. Se guardan códigos internos `proveedor_cuota`, `proveedor_credenciales`, `proveedor_configuracion` o `proveedor_no_disponible`, sin cuerpos arbitrarios del proveedor. El frontend continúa recibiendo los estados públicos existentes.

## Integración de frontend

No hay endpoints nuevos ni API key en el navegador:

1. Crear normalmente mediante `POST /api/publicaciones`.
2. Consultar `GET /api/publicaciones/{id}/analisis` mientras sea `pendiente` o `procesando`.
3. Al finalizar, recargar `GET /api/publicaciones/{id}/momentos`.
4. Corregir con el PUT existente y su `versionRelaciones`; manejar 409 recargando antes de una nueva decisión.
5. Explorar publicaciones del acontecimiento con `GET /api/momentos/{id}/publicaciones`.

Una detección válida vacía termina `sinMomento`; un fallo termina `fallido` al agotar sus intentos. `completado` no garantiza asociaciones activas: el autor puede haber rechazado lo detectado.

## Alcance y operación

No se necesita una migración adicional: se usan las tablas de los pasos anteriores. Los recuerdos anteriores y los trabajos ya terminados como `sinAnalizador` no se reencolan en masa al activar una clave. El [paso 6](red-recuerdos-paso-6.md) permite seleccionarlos y reprogramarlos por lotes desde una herramienta administrativa. Publicar un recuerdo ficticio nuevo permite evaluar el flujo completo. La edición de contenido vía EF genera una versión nueva como en el paso 4.

La búsqueda acotada y la canonicalización reducen duplicados, pero no aseguran equivalencia semántica global. Si una variante no recupera al candidato correcto y el modelo propone otro nombre canónico, todavía puede crear un duplicado. Quedan para una evolución la búsqueda semántica, la fusión revisable de duplicados, los acontecimientos sin año y la jerarquía automática. No usar alias genéricos para fusionar eventos de oficio.

Los tests mantienen nombres descriptivos en español y bloques Arrange/Act/Assert. Cubren HTTP, contrato, confianza, errores y cancelación; repositorio y servicio; y PostgreSQL real con HTTP ficticio para verificar endpoints, separación de años, concurrencia y rollback del catálogo junto a sus asociaciones. Las pruebas del controller y de autorización del paso 4 siguen aplicando sin cambios de contrato.
