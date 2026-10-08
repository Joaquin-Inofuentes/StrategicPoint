# 12 — Cambios del helicóptero, la ciudad de cubos y el balance (2026-10-07/08)

## Helicóptero
- `Mision/Helicoptero.cs`: `VolumenDelRotor` (volumen real del AudioSource, para pruebas), `SilenciarYa()` y volumen inicial 0 cuando la cinemática de rapel lo enciende con `VolumenExtra = 0` (sin golpe de sonido al activarlo).
- `CinematicaDeRapel.cs`: duración 12,5 s, cuatro tomas, cámara sin sacudida, helicóptero a ~58 m/s que frena y se estaciona girando, volumen que sube hasta 1,0 en t=2 s y baja hasta ~0 al final, helicóptero visible >= 4 s.
- Check `Bug123`: velocidad de cámara (< 2,5 m/s), visibilidad acumulada (>= 4 s), curva de volumen. Resultado de hoy: 11,3 s visible, volumen 0,004 / 1,000 / 0,142.

## Ciudad de cubos y civiles
- `Editor/OperacionBuilder.CiudadDeCubos.cs` y `Operacion/CivilesDeAmbiente.cs`: edificios con BoxCollider y volumen no caminable, el resto fusionado sin collider; máx. 60 civiles/autos activos en un radio de 150 m. Zonas de objetivos, reservas, waypoints y corredor de la cinemática excluidas.
- Efecto observado: la regresión #65-#132 no muestra fallos atribuibles a navegación ni minimapa. Sí queda #127 (bajas del helicóptero) con una caída de 6-8 a 2-10 según la corrida; se sospecha de la línea de tiro.

## Balance
- Aliados 100 -> 160 de vida; 4 -> 6 cargadores iniciales; aviso de oleada de 8 -> 12 s con tope mínimo de 0,5 s de reloj efectivo (la oleada 1 avisaba antes de que arrancara el reloj).

## Otros arreglos de la tanda
- `ArtilleroDeTorreta` en su propio archivo (crash de nivel 2 en la build), `OperacionTextoMundo` con `MaterialPropertyBlock` por renderer (carteles legibles en la build), 3 shaders SP en Always Included, `PruebaEnPlayer -pruebaop-escena`.
