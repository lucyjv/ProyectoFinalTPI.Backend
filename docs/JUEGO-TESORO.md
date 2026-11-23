# Búsqueda del tesoro semanal

La pista aparece en el feed general de usuarios autenticados. El tesoro se busca
en el mapa Mapbox de Explorar: requiere zoom suficiente y estar mirando su zona;
no requiere inclinación ni presencia física/GPS. Todas las categorías participan.
Esta versión registra el hallazgo y muestra una celebración, sin entregar puntos.

## Datos y capas

- `RetoTesoro` contiene la pista y ubicación de una semana.
- `HallazgoTesoro` guarda usuario, reto y fecha UTC; clave única por usuario y reto.
- El controller usa el usuario de la sesión. El servicio resuelve calendario,
  visibilidad y cuentas habilitadas. El repositorio realiza consultas e inserciones.
- La nueva migración crea `retos_tesoro` y `hallazgos_tesoro`, sin modificar recuerdos.
- Las inserciones con `ON CONFLICT DO NOTHING` hacen idempotentes tanto la creación
  del catálogo semanal como la captura, incluso desde pestañas simultáneas.

## Configuración

`JuegoTesoro` en appsettings admite:

- `Habilitado`: permite pausar el juego. Deshabilitado devuelve 204.
- `SemanaBase`: domingo de referencia para la rotación, inicialmente 2026-11-22.
- `Ubicaciones`: lista ordenada de lugares con `Clave`, `Pista`, `NombreHallazgo`,
  `Latitud`, `Longitud` y `ZoomMinimo` (entre 17 y 19, límite del mapa actual).

La configuración inicial apunta al monumento de San Martín en Plaza San Martín,
Retiro, CABA, con zoom mínimo 18. Se puede ajustar el punto en la configuración
antes de que se materialice el primer reto de esa semana.

Las semanas van del domingo 00:00 al siguiente domingo 00:00 de Argentina.
Se elige una ubicación por índice de semana desde `SemanaBase`, rotando la lista.
Con una sola ubicación, ésta se repite; para variar las semanas hay que agregar
otras ubicaciones y pistas. Los cambios de configuración requieren reiniciar.

El primer acceso materializa una sola fila semanal. Una vez creada se conserva
en la base: cambiar la lista no mueve un tesoro que otros usuarios ya empezaron
a buscar. Los cambios de catálogo se aplican a semanas aún no creadas.

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
