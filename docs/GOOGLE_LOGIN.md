# Acceso con Google

El backend mantiene sus JWT y su cookie HttpOnly de renovación. Google se usa exclusivamente para acreditar la identidad; no se solicita acceso a Drive, contactos ni otros servicios.

## Configuración y despliegue

1. En Google Cloud Console, configurar Google Auth Platform (pantalla de consentimiento: nombre, soporte, dominio, privacidad y términos del sitio; audiencia externa si el público no pertenece a una organización).
2. Crear una credencial OAuth de tipo **Aplicación web**. No es la contraseña del correo ni una API key. Que funcione el login actual no significa que esta credencial exista.
3. Agregar en **Orígenes de JavaScript autorizados** el origen exacto del FE, incluyendo esquema y puerto: por ejemplo `http://localhost:3001` y el origen HTTPS real de producción. Son ejemplos; usar los dominios reales del despliegue. El flujo GIS usa callback JavaScript y no requiere una ruta de redirección OAuth del BE.
4. Si la aplicación de Google está en pruebas, agregar las cuentas de prueba; para uso general completar los requisitos de publicación que indique Google.
5. Configurar **solo en el BE** `Google__ClientId` con el Client ID público (termina en `.apps.googleusercontent.com`). En desarrollo se puede usar User Secrets:

   ```sh
   dotnet user-secrets set "Google:ClientId" "CLIENT_ID_PUBLICO" --project ProyectoFinalTPI.Backend
   ```

   No hace falta un client secret para este flujo. No guardar tokens de usuarios ni secretos en Git.
6. Antes de habilitar la nueva versión del BE, aplicar la migración sobre la base correspondiente mediante el mecanismo habitual de despliegue:

   ```sh
   dotnet ef database update --project ProyectoFinalTPI.Backend.Repositorio --startup-project ProyectoFinalTPI.Backend
   ```

   La conexión se resuelve desde la configuración existente. La migración `AgregarAccesoGoogle` agrega `GoogleSubject` (índice único), `SoloGoogle` y `PuedeVincularGoogle`. Marca como elegibles **una sola vez** las cuentas con contraseña existentes al aplicar la migración, salvo las eliminadas. Los nuevos registros tienen elegibilidad falsa. Esto define el corte en el despliegue, no mediante una fecha del navegador. En una base nueva vacía, las cuentas creadas después no tienen esta excepción.
7. Reiniciar el BE y desplegar el FE de la rama correspondiente. El FE obtiene Client ID y un desafío desde `/api/auth/google/config`; no necesita variables de Google durante el build. Sin Client ID, el botón permanece oculto y el acceso existente sigue disponible.

Usar HTTPS en producción y el perfil HTTPS del BE en desarrollo (`https://localhost:7014`, certificado de desarrollo confiable). Las cookies tienen prefijo `__Host-`, `Secure`, `HttpOnly`, ruta `/`, sin dominio; la política SameSite es la misma del refresh existente. Si el BE tiene varias instancias, compartir y persistir las claves de ASP.NET Data Protection para que cualquiera pueda validar la cookie de desafío durante sus 10 minutos de vigencia.

Mantener CORS restringido al origen del FE y credenciales habilitadas. Si FE y BE están en sitios distintos, revisar la arquitectura de cookies existente; no debilitar las políticas para habilitar Google.

Si existen restricciones de red, el BE necesita obtener metadatos y claves de `accounts.google.com` y `www.googleapis.com` por HTTPS. El navegador debe poder cargar Google Identity Services (`accounts.google.com/gsi/client`, sus iframes y recursos asociados de Google). Si hay CSP/COOP externos al repositorio, seguir los requisitos oficiales de GIS, sin desactivar las protecciones globalmente.

Referencias: https://developers.google.com/identity/gsi/web/guides/get-google-api-clientid y https://developers.google.com/identity/gsi/web/guides/verify-google-id-token

## Reglas

- María, registrada con contraseña antes del despliegue y `maria@gmail.com`: Google acredita ese mismo correo, se vincula su `sub` de forma atómica y conserva ID, contraseña, publicaciones, recuerdos guardados y logros. No se pide contraseña. Puede seguir usando cualquiera de los dos accesos.
- Pedro, registrado con `pedro@otrogestor.com`, que llega con `pedro@gmail.com`: son correos distintos. El BE solicita username para crear una cuenta independiente; no copia ni fusiona datos.
- Víctor, nuevo con Google: elige username disponible y queda con acceso exclusivo por Google. Login y registro por contraseña lo redirigen al método correcto. Recuperación de contraseña no envía enlaces para cuentas exclusivas de Google y tampoco permite restablecerlas mediante un token previo.
- Una cuenta con contraseña creada después del corte no se vincula automáticamente a Google, aunque coincida el correo.
- Para vincular cuentas antiguas sin pedir contraseña, solo se aceptan correos cuya propiedad acredita Google: Gmail (`gmail.com`/`googlemail.com`) o Workspace con `email_verified` y `hd` coincidente. Una cuenta de Google con correo externo no se vincula automáticamente a una cuenta previa. Sí puede crear una cuenta nueva exclusiva de Google si su correo verificado está disponible.
- Las cuentas suspendidas o eliminadas no pueden iniciar sesión. Nunca se usa coincidencia de nombres, alias `+`, puntos de Gmail ni dominios diferentes para fusionar usuarios.
- Después de vincular, el identificador estable `sub` identifica la cuenta; un cambio posterior del correo de Google no crea ni vincula otra cuenta.

Las cuentas locales antiguas no requerían verificar email: por decisión de producto, quien demuestre con Google ser dueño del Gmail de una cuenta antigua podrá entrar a esa cuenta. No se pide la contraseña anterior.

## Contrato y protección

- `GET /api/auth/google/config`: devuelve `{habilitado:false}` o `{habilitado:true,clientId,nonce}`. Cookie de desafío protegida por ASP.NET Data Protection con vencimiento de 10 minutos y respuesta `no-store`.
- `POST /api/auth/google`: JSON `{credential,username?}`, cookies y cabecera `X-Nostalgiar-Request: 1`. Solo se aceptan identidades obtenidas tras validar firma RS256 con claves oficiales, issuer, audience, authorized party, expiración, correo verificado y nonce coincidente con la cookie protegida.
- Usuario nuevo sin username: `200 {requiereUsername:true}`, sin crear cuenta ni sesión. Se repite el POST con la misma credencial aún válida y un username; no se guarda la credencial en disco ni localStorage.
- Acceso correcto: mismo contrato/cookie de login existente. Se elimina la cookie de desafío. Error de identidad/desafío: 401; conflicto de cuenta/username: 409; Google no disponible: 503. Todos los endpoints usan el límite de intentos de autenticación existente.

## Validación

Desde la raíz del BE, con el SDK .NET 9:

```sh
dotnet test ProyectoFinalTPI.Backend.Tests --filter 'FullyQualifiedName~GoogleAuthTests|FullyQualifiedName~GoogleIdentityVerifierTests|FullyQualifiedName~GoogleControllerTests|FullyQualifiedName~AuthServicioTests|FullyQualifiedName~AuthIntegracionTests|FullyQualifiedName~RecuperacionContrasenaTests'
dotnet test ProyectoFinalTPI.Backend.Tests --filter FullyQualifiedName~GoogleMigrationTests
```

El segundo comando requiere Docker/PostGIS y verifica el corte de elegibilidad, repetición de migraciones y vinculación atómica en PostgreSQL real. Ejecutar las suites con configuración de Testing propia, sin variables de la API local que sobrescriban claves JWT; no ejecutar suites con contenedores concurrentemente en Docker Rootless.

El FE incluye pruebas de contrato y de navegador con Google simulado; la firma y claims se prueban en el BE con JWT firmados por claves de prueba. Para verificar la integración real faltan el Client ID y los orígenes autorizados. Con ellos, comprobar manualmente los casos María, Pedro y Víctor, logout/refresh y un correo ya ocupado, sin compartir tokens ni registrar credenciales en consola.
