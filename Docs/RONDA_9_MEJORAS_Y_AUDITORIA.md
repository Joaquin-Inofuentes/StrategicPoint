# Ronda 9: mejoras de la auditoria de 100 puntos

Resumen honesto: de los 100 puntos, **68 hechos, 18 que ya estaban resueltos, 8 parciales y 6 sin hacer** (motivo de cada uno en [AUDITORIA_100_ITEMS.md](AUDITORIA_100_ITEMS.md)). La suite headless termina en OK con la FASE 18 y la FASE 19.

## Que se hizo

* **Municion, mando y ajustes** (fase 1): reservas de municion, gamepad, pantalla/resolucion/calidad, daltonismo, HUD minimo (F10), cajas de suministros, IA con granadas y supresion.
* **Build real medido** (`-spmetrics`): 327 fps, 0 B de GC por frame en 20 s de muestreo. Es un build standalone en un monitor de 1080p.
* **Tutorial que no traba** (fase 3): [F8] salta el paso, [F7] retoma el paso guardado, flecha de ayuda tras 16 s ("OBJETIVO a tu DERECHA >> · 14 m"), aliados regrupados al saltar, progreso guardado.
* **Radial legible**: letra 15, fondo mas translucido; la etiqueta de la baliza se desvanece sobre la mira.
* **Menu principal** rehecho a 960x540 con subtitulo y consejo; zoom del fusil x3; saltar agachado te levanta; IR ALLA ajusta puntos inaccesibles.
* **Repositorio** (fase 4): README, `Tests/LEAME.md`, flujo de CI, archivos gigantes partidos (`PlayerInputDriver`, `AiBrain`, `HeadlessTestRunner`), flechas de aliados que no pisan el HUD.
* **Radial ATACAR (item 61)**: dos opciones nuevas, **TODOS SUPRIMEN** (rafagas de 6 s hacia el punto apuntado) y **GRANADA ALLI** (la lanza el aliado mas cercano que tenga granadas y cuya parabola llegue; si nadie puede, avisa). Probado en la FASE 19.

* **Trepar (53)**: saltar contra un cajon/murete de 0,5 a 1,3 m lo trepa en medio segundo.
* **Reordenar la escuadra (64)**: **[** y **]** mueven al soldado que manejas en el orden (cambia su numero en el roster y su tecla F1-F9).
* **Tamano de interfaz (28)**: Configuraciones > TAMANO DE INTERFAZ (100/125/150 %).
* **Rendimiento (75, 76)**: `CamaraPrincipal` y `RecursosCache` reemplazan a `Camera.main` y `Resources.Load` en todo el runtime, con precarga y un test que impide volver atras. El punto 74 (materiales) era en su mayoria un falso positivo del conteo.

* **Arsenal (43)**: junto a una caja de suministros, **[,]** y **[.]** cambian el arma principal (6 armas; salta las que ya llevas en otra ranura).
* **Idioma ES/EN (27)**: **[F12]** o Configuraciones > IDIOMA. Traduce menu, dificultad, pausa, configuraciones, radial y avisos comunes; el tutorial y los textos de mision siguen en espanol (por eso queda PARCIAL).

* **Subtitulos de sonido (28)**: Configuraciones > SUBTITULOS DE SONIDO. Con la opcion en SI, los sonidos de combate escriben el lado y la distancia ("[DISPARO] << IZQUIERDA 9 m").

## Capturas

| Menu principal | HUD 16:9 |
|---|---|
| ![menu](../Assets/Validacion/Ronda9/menu_nuevo.png) | ![16x9](../Assets/Validacion/Ronda9/hud_16x9.png) |

| HUD 4:3 | HUD ~21:9 (1920x820) |
|---|---|
| ![4x3](../Assets/Validacion/Ronda9/hud_4x3.png) | ![21x9](../Assets/Validacion/Ronda9/hud_21x9.png) |

**HUD a 3840x2160** (build, supersampling x2): ![4k](../Assets/Validacion/Ronda9/hud_4k.jpg)

## Lo que NO esta hecho ni verificado

* **Arte** (36, 44, 55, 21): modelos, animaciones, ragdoll, arte y musica de menu. No se pueden resolver desde codigo.
* **Oido humano** (65): los sonidos se verificaron por metricas; solo una persona puede juzgarlos.
* **4K** (19): renderizado a 3840x2160 por supersampling desde el build; no se vio en un monitor 4K fisico.
* **CI** (81): el flujo SI corre en GitHub, pero falla por falta del secreto `UNITY_LICENSE` (corregido: ahora avisa y se salta la suite). Cargar la licencia es del dueno del repo.
* **Sin hacer**: localizacion completa del tutorial y las misiones (27, parcial), caida con dano (51, no aplica: no hay caidas), tutorial 100 % con gestos reales (95), canvases (77).
* Los servidores MCP de GitHub, Sentry y Supabase piden autorizacion y no se usaron.
