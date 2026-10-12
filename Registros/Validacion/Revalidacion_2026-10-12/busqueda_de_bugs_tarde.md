# Búsqueda de bugs (tarde del 12/10/2026): guardado, RTS, cinemática

| # | Qué se encontró | Cómo se midió | Arreglo |
|---|---|---|---|
| 1 | 49 cámaras `ZzCam` (1600x900) guardadas en `SC_Operacion`: restos de los scripts de captura | `Camera.allCamerasCount` = 51 en Play; 5 fps → 76 fps al borrarlas | Borradas desde el editor y escena guardada (`70581ef`) |
| 2 | `Fragmentador.Liberar` asignaba velocidad a trozos kinemáticos (aviso de Unity por trozo) | Consola en el objetivo 2 | Se omite si el cuerpo ya es kinemático (`70581ef`) |
| 3 | RTS: el aliado mandado a más de 12,5 m del líder llegaba y volvía solo con el líder | Orden de 12 m en el objetivo 3: avanzaba y regresaba a 4 m del líder en ~2 s | `MantienePosicion` en `AiBrain`; vuelve solo si el líder se aleja 45 m. Prueba nueva en la suite (`bb8567f`) |

## Lo que se probó y estaba bien

- **Guardar y cargar (tiempo congelado):** objetivo 2 con 12 enemigos y 4 muertos a propósito: al cargar vuelven los 12 con la misma vida y posición. Objetivos 3, 4 y 5: fase, aliados, vehículo y vida idénticos antes y después.
- **Cinemática de apertura:** dura 68 s, se puede saltar con Espacio y termina sola. Capturas en `Registros/Validacion/Cinematica_2026-10-12/`.
- **Baterías #194–#220 repetidas con el código nuevo:** mismos resultados que las de la mañana (solo cambian decimales y el reloj).
- **Suite headless completa:** `OK`, 0 fallas.

## Sin reproducir

- Una vez, al cargar una partida en el objetivo 5, salió una `NullReferenceException` repetida en `EmplazamientoDeTorreta.Pintar` (línea 103, `bloque.SetColor`) y el director no apareció. En 5 repeticiones más (con y sin tiempo congelado) no volvió a pasar. Queda anotado por si reaparece.
