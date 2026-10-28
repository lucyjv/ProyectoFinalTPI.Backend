# Publicaciones con multimedia opcional

Para la presentación se permite crear recuerdos sin foto, video ni enlace. Se conservan las opciones de archivo, Google Photos y enlace, y las validaciones del contenido que se adjunte.

El formulario permite usar **Continuar sin adjunto**. Título, historia, lugar y fecha siguen siendo obligatorios. La regla se aplica a publicaciones generales y a recuerdos dentro de grupos.

## Configuración actual

Frontend, `.env.local` (también se usa este valor si la variable no existe):

```dotenv
NEXT_PUBLIC_REQUIRE_PUBLICATION_MEDIA=false
```

Backend, `ProyectoFinalTPI.Backend/appsettings.json`:

```json
"Publicaciones": {
  "RequerirMultimedia": false
}
```

La variable de entorno equivalente del backend es `Publicaciones__RequerirMultimedia=false`.

## Volver a exigir multimedia

1. Cambiar `Publicaciones:RequerirMultimedia` a `true` en el backend y reiniciarlo.
2. Cambiar `NEXT_PUBLIC_REQUIRE_PUBLICATION_MEDIA=true` en el frontend y reiniciar `npm run dev`. En producción, volver a ejecutar `npm run build`: Next incorpora esta variable pública durante la compilación.

Se deben cambiar ambas opciones. El backend valida la regla también al llamar al servicio directamente; no depende de que el navegador oculte el botón para omitir el adjunto.

Los recuerdos de texto que ya estén publicados seguirán siendo válidos. Reactivar la regla se aplica a las nuevas publicaciones.

## Persistencia y logros

- Sin adjunto no se llama a Cloudinary ni se necesita tener sus credenciales configuradas.
- `UrlMultimedia` se guarda como cadena vacía; no se inventa una URL ni se agrega una foto de ejemplo.
- No se requieren migraciones de base de datos.
- Los recuerdos de texto cuentan como publicaciones y pueden desbloquear Primer paso, Lince, etc.
- El contador de Fotógrafo del recuerdo exige una URL no vacía: los recuerdos de texto no suman como fotografías.
- Si se adjunta contenido, se conservan los límites de tamaño, la validación HTTPS y la exclusión de archivo + URL.
