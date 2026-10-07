# 06 — Resumen del plan de bugs #104-#132

Plan completo: `Informes/06_Plan_bugs_104_132.md`. Paquetes EN SERIE, uno por agente. Checks `Bug1NN()` en `Editor/ChecksBugs104.<Paquete>.cs` (parciales de `ChecksBugs065`).

| Paquete | Bugs | Esfuerzo | Por qué en este orden |
|---|---|---|---|
| P0 Infraestructura de checks | — | bajo | Rango 104-132 y selección de escena (SC_Gameplay/SC_Loading) para los checks. |
| P1 Combate | #107 #108 #110 #132 | medio | Sistemas transversales y aislados; el arma/poseído inicial (Kes) lo usan los paquetes siguientes. |
| P2 Arte: pesos de piel | #109 | alto | Independiente; regenera mallas antes de las capturas de validación. |
| P3 Cuartel: visibilidad | #104 | medio | Solo builders/FX de la fase 1. |
| P4 Guía FPS | #105 #106 #111 | medio-alto | Ruta punteada y flechas que reutiliza P7. |
| P5 Motor/efectos/líneas | #126 #113 #125 | medio | Arreglos chicos transversales antes de tocar Resistir. |
| P6 RTS | #116 #121 #124 #117 #119 #112 #118 #115 | alto | Base de controles/lectura sobre la que se rediseña Resistir. |
| P7 Resistir rediseño | #120 (sin guardado) #122 #114 | alto | Radio opcional, revivir milicianos, C para cambiar de aliado, 4 milicianos. |
| P8 Extraer y Victoria | #128 #127 #129 | alto | Necesita los 4 milicianos de P7. |
| P9 Cinemática de rapel | #123 | medio-alto | Antes del guardado, para enganchar su autoguardado. |
| P10 Guardado de partida | #120 (guardar/Continuar/autoguardado) | alto | Último sistema de estado: guarda todo lo nuevo (operador IA, milicianos, cinemáticas). |
| P11 SC_Gameplay | #130 #131 | medio | Independiente de la Operación. |
| P12 Integración | todos | medio | `CorrerTodos(0,132)`, recorrido completo, capturas `v2_`, informe 07. |

## Reproducido vs. no

- **Reproducido en el editor (render/Play)**: #104 (artillero de TV2 tapado por el parapeto, faro diminuto que al romperse queda negro, sin FX), #109 (caras con pesos mano+cadera mezclados en las 3 mallas), #112/#118 (letras con `GUI/Text Shader`, sin prueba de profundidad, flotando a y=0,5).
- **Confirmado por código/JSON**: #107/#108 (headshot = solo ×2), #110 (Vega inicial, 4 cargadores fijos, ADS 0,15), #113 (son casquillos del heli, negros de noche; la captura se congeló porque el reporte pone `timeScale=0`), #115, #116 (velocidad), #117, #119 (clic sobre la radio = "DESTINO BLOQUEADO"), #120 (revivir solo busca en `Squad`, los milicianos quedan afuera; [E] suelta la radio), #121, #122 (6 necesarios, 4 disponibles), #124, #125, #126 (umbral de caída 0,8+0,5 = 1,30 exacto: levita para siempre), #128 (`Escuadra()` excluye milicianos), #131 (texto en `CinematicaIntroBuilder.cs:52`), #132 (animación de 0,4 s contra 1,5 s de recarga; sonido 3D bajo).
- **No reproducido**: #113 en vivo (necesita al heli disparando sobre el helipuerto; queda confirmado por captura + código), #116 "sectores del mapa no terminados" (no se inspeccionaron los bordes; P6 los renderiza y los cierra).
- **Pedidos de diseño** (no hay defecto que reproducir): #105, #106, #111, #114, #123, #127, #129, #130.

## Decisiones clave

- **#120 vs WP10**: la radio pasa a ser un ROL. El reloj corre con un operador (IA o jugador) en la radio, se puede volver a FPS con Tab; los checks de WP10 se actualizan.
- **C**: un toque = aliado más cercano; mantener = rombos resaltados y, al soltar, se toma el más centrado. La ruta pasa a ser un punteado automático + tecla nueva (propuesta V).
- **RTS**: Q/E bajan/suben la cámara; la radio se suelta manteniendo [E] 1 s en FPS o desde el panel.
- **#122**: 4 milicianos (7 − operador = 6 = 3×2).
- **#110**: Kes inicial; reservas 8/6/4 por dificultad; ADS 0,03 (Smg 0,06). Headshot del jugador mata al instante (los del enemigo no).
- **Guardado**: JSON versionado en `SesionLog.Carpeta/Partidas/partida_<escena>.json` sobre `EstadoDeSesion` + `OperacionDirector.CapturarEstado`. Guardado manual con ESC, autoguardado al terminar cada cinemática y en cada `EntrarFase`, Continuar en el menú, se borra al ganar. Funciona como checkpoint: los enemigos de la zona se reinician.

## Riesgos principales

Escenas solo por builders (incluido `rtsPanSpeed` serializado); checks viejos que asumen Vega inicial, RTS forzado o 2 milicianos; deformaciones nuevas al rehacer los pesos de piel; orden de inicialización al restaurar una partida guardada; checks largos que tienen que ser asíncronos (el eval corta a los 5 s).
