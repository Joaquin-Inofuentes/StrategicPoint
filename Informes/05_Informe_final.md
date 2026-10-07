# Informe final: bugs y mejoras #065 a #101 (Operación Cuartel)

Fecha: 2026-10-03 a 2026-10-05. Proyecto: `DV_C6_StrategicPoint`, escena `SC_Operacion`.

## 1. Resultado

- **37 de 37 bugs/mejoras implementados** y validados en una pasada de punta a punta (objetivos 1 a 6) más corridas aisladas.
- Veredictos del validador: **37 OK, 0 FALLA, 0 PARCIAL**. Dos con observación de diseño: #074 (los milicianos usan cobertura solo 25-29 % del tiempo de combate) y #081 (margen estrecho de la IA de cobertura).
- La validación encontró **1 falla real**, la toma final del #077 en la partida real, y la arregló. También corrigió 1 criterio de prueba flojo (#068, tolerancia de materiales). El encabezado de `04_Validacion_final.md` dice "2 fallas reales"; el detalle de la sección (b) y el resumen del validador dicen 1 real más 1 criterio flojo.
- Suite headless: sin fallos nuevos (31 preexistentes contra 32 en la base).
- **Nada se commiteó ni se pusheó.** Todo está en el working tree (unos 770 archivos modificados o nuevos). Hay una build de prueba en `Builds/Windows64/`, pero es anterior a WP9b, WP10, WP11 y a los arreglos de la validación.

## 2. Cómo lo hice (los 4 pasos que pediste)

| Paso | Modelo | Qué hizo | Duración / consumo |
|---|---|---|---|
| 1. Diagnóstico y plan | **Opus** | Reprodujo los bugs en el editor donde se pudo, confirmó causas raíz y escribió el plan de 14 paquetes (WP0 a WP11) con criterios de aceptación | 28 min · 75 llamadas · 352 k tokens |
| 2. Implementación | **Sonnet** (un agente por paquete, en serie) | WP0, WP3, WP1, WP2, WP4, WP5a, WP5b, WP6, WP7, WP8, WP9a, WP9b, WP10, WP11 | ≈ 25 h en total; entre 18 min (WP3) y 4,5 h (WP11) por paquete |
| 3. Validación | **Sonnet** | Recreó el escenario original de cada bug en una sola sesión de Play, con captura y veredicto por bug; arregló lo que falló | 198 min · 333 llamadas · 264 k tokens |
| 4. Informe | Yo (modelo de la sesión principal) | Este documento | — |

**Por qué estos modelos.** Opus para el diseño: había que entender 37 pedidos, tres rediseños grandes (#078, #097, #101) y evitar conflictos entre ellos en `OperacionBuilder` y `OperacionDirector`. Sonnet para aplicar y validar: el trabajo era largo, con muchas llamadas, y el plan ya traía el diseño y los checks de cada paquete.

**Dos desvíos respecto de lo pedido:**
- El plan recomendó Sonnet "alto" o "bajo" por paquete, pero la herramienta de agentes solo me deja elegir el modelo, no el nivel de esfuerzo. Todos corrieron con el esfuerzo por defecto.
- Para el paso 3 pediste "Sonnet medio" y pasó lo mismo.

**Por qué en serie y no en paralelo.** Hay un solo editor de Unity y los paquetes comparten archivos. Dos agentes en paralelo habrían pisado la escena y las compilaciones.

## 3. Qué se hizo, por paquete

| Paquete | Bugs | Cambios principales |
|---|---|---|
| WP0 Columna | habilitante | `OperacionDirector` y `OperacionBuilder` en partials; subfases; `OperacionHud.Pasos/Subobjetivos/BarraDeJefe`; `ChecksBugs065` (checks `Bug0NN()` por reflexión); torretas del centro, noche y luces portadas al builder |
| WP3 UI | 098, 099, 100, 065 (minimapa) | Perillas de sliders redondas; pausa y ajustes en su propio canvas (el HUD ya no parpadea al cambiar el tamaño); tabla de controles con íconos y scroll, sin botón REMAPEAR; muros en el minimapa |
| WP1 Combate | 094, 095, 079, 065 (cursor), 096 | Headshot por caja de cabeza real (0 al pecho, 8/8 a la cabeza); munición con reserva real y HUD "cargador / total"; killstreaks DOBLE a PENTA con FURIA (daño ×2, +30 % velocidad); cursor de baja; Q con 1 s de margen y diana |
| WP2 Audio | 065, 095, 072, 093 | Perfiles espaciales 3D por tipo de sonido; distorsión y graves horneados; explosiones y cañón con graves; himno de victoria sintetizado con fade largo |
| WP4 IA | 080, 081, 074, 085, 086, 087 | Línea de tiro de boca a pecho; coberturas más usadas; separación anti-solape (de 13 pares a 0-1); herido se repliega disparando y va al médico |
| WP5a Interacción | 083, 084, 088, 089, 092 | Cartel "DEBES SER X" al usar [E] con el rol equivocado; revivir con animación, bloqueo de movimiento, sonido y anillos; el agachado ya no se entierra |
| WP5b Feedback | 066, 067, 068 | Engranajes sobre quien opera y sobre el objetivo; animación para 8 tipos de acción; anillo de destino que gira, se expande y se desvanece, y línea blanca de 3 s |
| WP6 Vehículos | 091, 090, 069, 071, 070, 072, 073 | Giro del tanque corregido y anillo de impacto; el tanque aplasta muros; aliados atacan camionetas con lanzacohetes; cursor "Atacar"; cráteres y chapas; 4 camionetas simultáneas |
| WP7 Helicóptero | 075, 076, 077 | Polvo y ondas de viento; ametralladoras con artilleros visibles; toma final desde el suelo con fondo transparente |
| WP8 Cuartel | 078, 085 | 4 sectores, 2 torres de francotirador, 2 torretas vigía, 4 reflectores con operador, 20 guardias en cobertura |
| WP9a Puestos | 082, 097 (parte 1), 084, 085 | Dos puestos a volar con mecha de 20 s y contraataque; cinemática inicial de unos 29 s con 6 pasos que bloquea el input |
| WP9b Huida | 097 (parte 2), 073 | Jefe GOLIAT con piso de vida, reparación de Kes, carrera de 70 s con helicóptero enemigo y cinemática final en cámara lenta |
| WP10 Resistir | 101, 074 | Objetivo 5 jugable solo desde el RTS: sectores que caen y se recuperan, oleadas anunciadas, tutorial de 6 pasos |
| WP11 Integración | todos | Suite aislada (`CorrerTodos`), #063 corregido (bocas distintas por oleada), fuga de materiales de 109 mil a 1,2 mil, picos de lag reducidos |

El detalle de cada uno (archivos, desviaciones del plan, pendientes) está en `02_Progreso_implementacion.md`.

## 4. Hallazgos que nadie había pedido

- El anillo de impacto del tanque **nunca se dibujaba** en `SC_Operacion`: un campo sin serializar llegaba nulo.
- La IA **no veía a las camionetas**: el rayo de línea de tiro terminaba dentro del suelo.
- El anillo de destino estaba **enterrado bajo el asfalto**, y `SetPipCount` lo apagaba con 0 pips.
- El headshot fallaba por una sola muestra de 1 m de paso, no por la altura de la caja de la cabeza como decía el plan.
- La toma final de victoria caía contra una casa o en un callejón en la partida real (#077), que solo se vio al validar con enemigos reales.

## 5. Lo que NO está verificado (tenés que probarlo vos)

El validador no pudo comprobar esto por línea de comandos:

- **Audio por oído:** #065, #072, #089, #093, #095, #076. Se verificó con datos (spatialBlend, volúmenes, espectro de los clips), pero nadie escuchó nada. Los disparos lejanos pueden quedar bajos porque el perfil de disparo es logarítmico.
- **Input real con mouse y teclado:** clic derecho y Q sobre un vehículo, panel RTS (#071, #101, #074, #087), mantener [E] de verdad (#082, #083, #066, #088, #097). Reparar el tanque poseyendo a Kes exige mantener [E]. Las pruebas de RTS usaron las funciones de selección y órdenes.
- **A ojo:** parpadeo del HUD (#099), perillas y controles en otras resoluciones (#098, #100), forma del cursor de hardware.
- **Sensación y balance:**
  - La carrera de 70 s con el Halcón (#073, #097): el tanque termina casi siempre en el piso del 20 %, así que puede estar dura.
  - La FURIA (#079).
  - La cobertura enemiga y de los milicianos (#081, #085, #074).
  - La velocidad de las camionetas (#090).
  - La claridad del RTS (#101).

## 6. Rendimiento (medido en el editor, sin build)

| Fase | fps antes → después de WP11 |
|---|---|
| Infiltrar | 81 → 78 (66 en combate) |
| Puestos | 82 → 72 |
| Centro de Datos | 112 → 109 |
| Huida | 82 → 90 |
| Resistir | 70 → 67 (cae a 51-58 al final; peor frame 363 ms en la validación) |
| Extraer | 104 → 100 (un hitch de 360 ms al arrancar la toma final) |

- La media no mejoró; los **picos** sí: Huida de 343 a 104 ms y Resistir de 145 a 38 ms.
- Estas cifras incluyen unos 20 ms de overhead del editor por cuadro. No se perfiló una build.
- El cuartel con artilleros de torreta y 4 reflectores bajó a 32-47 fps en WP8 y WP11 no reprodujo ese número. Sigue sin explicarse y la hipótesis (costo del editor) no está probada.
- El render no se optimizó: unos 3000 renderers y 62 luces.

## 7. Pendientes y riesgos conocidos

- Checks encadenados: se contaminan entre sí (082b/c, 085b, 088/089, 078e) y por eso `CorrerTodos` usa un Play fresco por check. #069b falló 2 veces en Plays muy largos y no se reprodujo en más de 10 corridas.
- Sin check propio: el costo de los fragmentos de destrucción del tanque (0 activos en las corridas).
- La niebla de guerra del minimapa no existe en la escena: "revelar enemigos" del campanario (#101) es solo una bandera.
- La rampa y varios flujos de WP9b (clic en "¡FUEGO!", matar al jefe apuntando) se probaron con ganchos y modo dios, no con juego humano.
- Cambios laterales que conviene saber: 57 `.meta` de audio en mono (WP2); se regeneró `SC_Operacion` varias veces con el builder (de 5405 a más de 7800 objetos); `ShowPause` informó `IsPaused=False` en un script de humo y no se investigó.
- **El editor se cayó 4 veces** por errores nativos del heap (Profiler, NavMesh). Se relanzó sin pérdida de código. El Profiler quedó desactivado.
- Durante el diagnóstico se movió el tamaño de HUD del usuario para medir #099 y se restauró a 0,85.

## 8. Próximos pasos sugeridos

1. Una **build nueva** con todo incluido, para medir el rendimiento real y probar con input y audio reales.
2. Jugar los puntos de la sección 5 y devolver lo que no te convenza.
3. Cuando quieras, commit y push. Hoy está todo sin commitear.

## Archivos de esta tanda (en `Informes/`)

- `01_Plan_bugs_065_101.md` y `01_Plan_bugs_065_101_resumen.md`: plan.
- `02_Progreso_implementacion.md`: bitácora por paquete.
- `03_Resultado_checks.md`: pasada de checks previa a los arreglos finales.
- `04_Validacion_final.md`: tabla de los 37 bugs con veredicto, evidencia y captura.
- Capturas de la validación: `Assets/Validacion/val_NNN_*.png` (192 archivos con ese prefijo) y `v2_*.png` de cada paquete.
