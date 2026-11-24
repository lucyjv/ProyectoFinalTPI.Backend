# Búsqueda del tesoro semanal

La pista aparece en el feed general de usuarios autenticados. El tesoro se busca
en el mapa Mapbox de Explorar: requiere zoom suficiente y estar mirando su zona;
no requiere inclinación ni presencia física/GPS. Todas las categorías participan.
Esta versión registra el hallazgo y muestra una celebración, sin entregar puntos.

## Datos y capas

- `RetoTesoro` contiene la pista y ubicación de una semana.
- `HallazgoTesoro` guarda usuario, reto y fecha UTC; clave única por usuario y reto.
- `UbicacionTesoro` guarda el catálogo editable; `ProgramacionTesoro` asigna una
  ubicación a una semana futura, o la pausa con una ubicación nula.
- El controller usa el usuario de la sesión. El servicio resuelve calendario,
  visibilidad y cuentas habilitadas. El repositorio realiza consultas e inserciones.
- Las migraciones AgregarJuegoTesoro y AdministrarJuegoTesoro crean las cuatro
  tablas y cargan San Martín como ubicación inicial, sin modificar recuerdos.
- Las inserciones con `ON CONFLICT DO NOTHING` hacen idempotentes tanto la creación
  del catálogo semanal como la captura, incluso desde pestañas simultáneas.

## Configuración

`JuegoTesoro` en appsettings conserva las opciones operativas:

- `Habilitado`: permite pausar el juego. Deshabilitado devuelve 204.
- `SemanaBase`: domingo de referencia para la rotación, inicialmente 2026-11-22.

El catálogo se guarda en la BD y se administra desde `/admin`, sección Búsqueda
del tesoro. Cada lugar tiene nombre al encontrarlo, pista, latitud (entre -85 y
85, zona navegable del mapa), longitud, zoom mínimo (17 a 19) y estado activo.
La migración carga el monumento de San Martín en Plaza San Martín, Retiro, con
zoom mínimo 18. El catálogo ya no se lee desde `JuegoTesoro:Ubicaciones`.

Las semanas van del domingo 00:00 al siguiente domingo 00:00 de Argentina.
Sin una programación explícita se rota por ID entre las ubicaciones activas,
según las semanas transcurridas desde `SemanaBase`. Con una sola se repite.
El panel permite programar las próximas 12 semanas, pausar una semana o volver
a la rotación automática. La vista previa automática puede variar al editar el
catálogo. Desactivar un lugar pausa las semanas futuras que lo tenían asignado;
no las reemplaza silenciosamente por otro lugar.

El primer acceso materializa una sola fila semanal. Editar el catálogo conserva
esa copia del reto en curso. Para cambiarlo deliberadamente se usa **Editar reto
vigente**: actualiza pista, nombre, coordenadas y zoom, manteniendo el mismo ID
y todos los hallazgos. Quienes ya lo encontraron conservan el reto completado;
los demás buscan la ubicación nueva. La edición vigente no cambia el catálogo.
El backend rechaza IDs de retos anteriores y la programación de semanas pasadas.
Los cambios del panel no requieren reiniciar. Los jugadores reciben los cambios
al volver a la pestaña, recargar o en la consulta periódica (hasta un minuto).

## API

Todos los endpoints requieren JWT de Nostalgiar y usan respuestas sin caché:

- `GET /api/juegos/tesoro/actual`: pista, fechas, zoom mínimo y hallazgo propio.
  No contiene coordenadas ni el nombre del lugar.
- `GET /api/juegos/tesoro/visible`: recibe `zoom`, `minLatitud`, `maxLatitud`,
  `minLongitud`, `maxLongitud`; devuelve el marcador sólo si está en esa vista,
  con zoom suficiente y sin hallazgo previo. En otro caso devuelve 204.
- `POST /api/juegos/tesoro/{retoId}/hallazgo`: recibe la misma vista en JSON.
  Comprueba reto vigente y visibilidad; devuelve el nombre del lugar y la fecha
  persistida. Los reintentos devuelven el mismo hallazgo, sin duplicarlo.

Los endpoints administrativos usan la política existente `AdministrarPanel`
(JWT con `esAdmin=true`), y respuestas sin caché:

- `GET /api/admin/juegos/tesoro`: catálogo, reto actual y próximas 12 semanas.
- `POST /api/admin/juegos/tesoro/ubicaciones`: crea un lugar.
- `PUT /api/admin/juegos/tesoro/ubicaciones/{id}`: edita o desactiva un lugar.
- `PUT /api/admin/juegos/tesoro/semanas/{yyyy-MM-dd}`: programa una ubicación;
  `ubicacionId: null` pausa esa semana. Sólo domingos futuros, hasta 12 semanas.
- `DELETE /api/admin/juegos/tesoro/semanas/{yyyy-MM-dd}`: vuelve a la rotación.
- `PUT /api/admin/juegos/tesoro/actual/{retoId}`: edita el reto de esta semana,
  sin cambiar su ID ni borrar sus hallazgos.

La vista es información del cliente, no una prueba antifraude de haber explorado
manualmente. No hay recompensas económicas ni de puntos en esta implementación.

## Levantar localmente

Desde la raíz de la solución:

```powershell
dotnet ef database update --project .\ProyectoFinalTPI.Backend.Repositorio --startup-project .\ProyectoFinalTPI.Backend --context ApplicationDbContext
dotnet run --project .\ProyectoFinalTPI.Backend
```

No ejecutar contra producción para probar el juego. Las pruebas automatizadas
usan PostgreSQL temporal y no consultan bases de desarrollo o producción.

## Comprobaciones

`JuegoTesoroServicioTests` cubre calendario argentino, zoom, zona, cuentas,
capturas vigentes y funcionamiento sin inclinación/GPS. `JuegoTesoroIntegracionTests`
aplica migraciones reales y comprueba autenticación, ausencia de coordenadas en
la pista, ocho capturas concurrentes, persistencia y aislamiento entre usuarios,
rotación semanal y creación concurrente de una sola pista.
`AdminJuegoTesoroIntegracionTests` verifica la migración con catálogo inicial,
permisos 401/403, ediciones, programación, pausa y rotación, límites del calendario
y preservación de hallazgos al mover el tesoro vigente.
