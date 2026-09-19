# Ronda 9: mejoras de la auditoria de 100 puntos

Resumen honesto: de los 100 puntos, **61 hechos, 15 que ya estaban resueltos, 10 parciales y 14 sin hacer** (motivo de cada uno en [AUDITORIA_100_ITEMS.md](AUDITORIA_100_ITEMS.md)). La suite headless (`Strategic Point/Run All Tests Headless`) termina en OK con la FASE 18 y la FASE 19 nuevas.

## Que se hizo

* **Municion, mando y ajustes** (fase 1): reservas de municion, gamepad, pantalla/resolucion/calidad, daltonismo, HUD minimo (F10), cajas de suministros, IA con granadas y supresion.
* **Build real medido** (`-spmetrics`): 327 fps, 0 B de GC por frame en 20 s de muestreo. Es un build standalone en un monitor de 1080p.
* **Tutorial que no traba** (fase 3): [F8] salta el paso, [F7] retoma el paso guardado, flecha de ayuda tras 16 s ("OBJETIVO a tu DERECHA >> · 14 m"), aliados regrupados al saltar, progreso guardado.
* **Radial legible**: letra 15, fondo mas translucido; la etiqueta de la baliza se desvanece sobre la mira.
* **Menu principal** rehecho a 960x540 con subtitulo y consejo; zoom del fusil x3; saltar agachado te levanta; IR ALLA ajusta puntos inaccesibles.
* **Repositorio** (fase 4): README, `Tests/LEAME.md`, flujo de CI, archivos gigantes partidos (`PlayerInputDriver`, `AiBrain`, `HeadlessTestRunner`), flechas de aliados que no pisan el HUD.
* **Radial ATACAR (item 61)**: dos opciones nuevas, **TODOS SUPRIMEN** (rafagas de 6 s hacia el punto apuntado) y **GRANADA ALLI** (la lanza el aliado mas cercano que tenga granadas y cuya parabola llegue; si nadie puede, avisa). Probado en la FASE 19.

## Capturas

| Menu principal | HUD 16:9 |
|---|---|
| ![menu](../Assets/Validacion/Ronda9/menu_nuevo.png) | ![16x9](../Assets/Validacion/Ronda9/hud_16x9.png) |

| HUD 4:3 | HUD ~21:9 (1920x820) |
|---|---|
| ![4x3](../Assets/Validacion/Ronda9/hud_4x3.png) | ![21x9](../Assets/Validacion/Ronda9/hud_21x9.png) |

## Lo que NO esta hecho ni verificado

* **Arte** (36, 44, 55, 21): modelos, animaciones, ragdoll, arte y musica de menu. No se pueden resolver desde codigo.
* **Oido humano** (65): los sonidos se verificaron por metricas; solo una persona puede juzgarlos.
* **4K** (19): no se probo (monitor 1080p).
* **CI** (81): el flujo `.github/workflows/tests.yml` esta escrito pero **nunca se ejecuto en GitHub**.
* **Sin hacer**: localizacion (27), arsenal por clase (43), caida con dano (51, no aplica: no hay caidas), trepar (53), reordenar escuadra (64), tutorial 100 % con gestos reales (95), materiales/`Camera.main`/`Resources.Load`/canvases (74-77).
* Los servidores MCP de GitHub, Sentry y Supabase piden autorizacion y no se usaron.
