# Google Photos: importación de una foto

Esta integración no requiere migraciones. Usa OAuth y Google Photos Picker API;
no publica recuerdos ni sube a Cloudinary hasta que el frontend envía el archivo
por el POST /api/publicaciones existente (campo multipart Archivo).

## Configuración

Habilitar Google Photos Picker API y registrar un cliente OAuth de aplicación web.
Agregar el scope https://www.googleapis.com/auth/photospicker.mediaitems.readonly y
las cuentas de prueba en Google Auth Platform. Guardar mediante user-secrets:

- GooglePhotos:ClientId
- GooglePhotos:ClientSecret
- GooglePhotos:RedirectUri = http://localhost:5000/api/integraciones/google-photos/callback

La URI debe coincidir exactamente con la registrada en Google. No versionar el
JSON descargado ni poner el secreto en NEXT_PUBLIC_. Si fue expuesto, reemplazarlo.
La aplicación sigue arrancando sin esta configuración; iniciar la integración devuelve 503.

## Contrato para el frontend

Todas las rutas salvo callback requieren Authorization: Bearer <JWT de Nostalgiar>.
Usar credentials: 'include' para enviar/recibir la cookie HttpOnly que vincula OAuth
con el navegador. Desarrollo: front y API deben usar ambos localhost (no mezclar
127.0.0.1). Producción: desplegar en el mismo sitio, por ejemplo app.example.com y
api.example.com, con HTTPS y el origen real habilitado en CORS. La cookie SameSite=Lax
no está pensada para dominios de sitios distintos.

1. Abrir una ventana vacía sincrónicamente en el click para evitar bloqueos de popup.
2. POST /api/integraciones/google-photos/sesiones, sin body. Responde
   { operationId, authorizationUrl, expiresAt }. Navegar la ventana a authorizationUrl.
   Guardar operationId en el borrador. Una operación activa por usuario; 409 indica
   que debe cancelar la anterior antes de empezar otra.
3. Google vuelve al callback y el servidor redirige al Picker con /autoclose.
   El callback valida state de un solo uso, PKCE y cookie. Ningún token llega al front.
4. GET /api/integraciones/google-photos/sesiones/{operationId}. Responde
   { status, pickerUri, pollAfterSeconds, expiresAt }. Estados: authorizing, selecting,
   ready, imported. Respetar pollAfterSeconds y detenerse al vencer expiresAt. El
   backend también limita sus consultas según pollingConfig de Google.
5. Al recibir ready: POST /api/integraciones/google-photos/sesiones/{id}/imagen.
   Respuesta binaria, no JSON. Convertir response.blob() en File, mostrar una vista
   previa mediante URL.createObjectURL y usar el flujo actual de Archivo al publicar.
   El nombre de archivo depende del Content-Type (jpeg/png/webp/gif). No usar una
   URL temporal de Google como UrlArchivo.
6. DELETE /api/integraciones/google-photos/sesiones/{id} al cancelar, reemplazar o
   después de recibir el archivo. Revocar también la object URL del navegador al salir.
   No borrar la sesión sólo porque la ventana se cierre: /autoclose puede indicar éxito;
   consultar primero el estado. Conservar el texto y demás campos del formulario.

Los errores son ProblemDetails (leer detail). 401: sesión Nostalgiar; 404: operación
inexistente/otro dueño; 409: estado inválido o autorización rechazada; 410: vencida;
413: imagen mayor a 2 MiB; 415: video/formato no compatible; 429: límite temporal;
502/504: error o demora de Google; 503: configuración ausente.

## Límites y ciclo de vida

- Una foto. Picker puede permitir elegir video, pero la importación lo rechaza.
- Se solicita una versión de hasta 2048 px, luego 1600 y 1024 si excede 2 MiB,
  sin recortar. Se valida MIME, firma del archivo y tamaño durante la lectura.
  Si Google no entrega un formato compatible o suficientemente pequeño, se informa.
- Sólo se descarga la URL devuelta por la selección del usuario; no se aceptan URLs
  arbitrarias. HTTPS googleusercontent.com, sin redirecciones HTTP automáticas.
- El archivo importado se mantiene temporalmente para reintentos sin duplicar subidas.
  El servidor intenta borrar la sesión remota al importar y al cancelar/vencer.
  Si Google no responde al limpiar, la sesión remota expirará; no se registra su token.
- Tokens y operaciones viven en memoria, máximo 15 minutos (o menos si Google lo indica),
  con barrido cada minuto y máximo 100 operaciones. Reiniciar pierde las selecciones.
  Para múltiples réplicas se necesita almacenamiento temporal compartido y bloqueo
  distribuido, o afinidad de sesión. No hay refresh tokens ni acceso permanente.
- No configurar logs de cuerpos OAuth, cookies o query strings del callback en el proxy.

## Validación manual pendiente con Google

Autorizar una cuenta de prueba, elegir una foto, descargarla y publicar por el flujo
normal. Verificar la URL permanente de Cloudinary. Probar rechazo de consentimiento,
cierre de ventana, expiración, video, imagen grande y cancelación sin perder el borrador.
Las pruebas automatizadas usan HTTP simulado; no consumen cuentas ni suben archivos reales.

Referencias:
- https://developers.google.com/photos/overview/configure-your-app
- https://developers.google.com/photos/picker/guides/sessions
- https://developers.google.com/photos/picker/guides/media-items
