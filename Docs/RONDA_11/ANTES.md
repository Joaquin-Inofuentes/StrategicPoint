# Ronda 11 — El "antes": qué se midió, cómo y qué dice

Registro de la situación **previa a cualquier arreglo** para los 20 puntos pedidos. No se tocó código del juego:
todo se midió con `Assets/_Project/Scripts/Editor/AntesProbe.cs` (solo Editor), que se suscribe al `EventBus`
(disparos, daño, curas, muertes, posesión, estados de IA), muestrea el Animator por cuadro y saca capturas.
El mismo instrumento se vuelve a correr después de cada ola para el "ahora".

- Datos crudos: `Docs/RONDA_11/evidencia_antes/<corrida>/log.jsonl` (una línea por evento) y `fotos/*.png`.
- Entorno: Unity 6000.5.6f1, escena `SC_Gameplay`, dificultad FACIL, Play sin recarga de dominio, commit base `main` + rama `claude/great-cannon-pf30m4` ya integrada.
- Ronda 10 (100 bugs, plan en 8 olas, 22 pasos de tutorial, `qa_total.bat`) se cruza en `PLAN_IMPLEMENTACION.md`; acá solo se mide lo pedido en los 20 puntos.

## Resumen: qué se confirmó con datos y qué es solo lectura de código

| # | Punto pedido | Estado del "antes" | Fuente |
|---|---|---|---|
| 1 | Al morir todos no termina la partida | **Reproducido**: 0 vivos, `outcomeMostrando=False`, fase `Infiltrar` a los 8 s y a los 16 s | 02 |
| 2 | El médico no revive | **Reproducido**: el médico queda en `Patrol` a 2,8 m del caído; el rescate vence | 02 |
| 3 | Disparo con dispersión y más alcance | **Reproducido**: 99–100 % de aciertos a 8–28 m, sin ráfagas; alcance 22/13 (enemigo) y 20/12 (aliado) | 03 |
| 4 | Al darle Jugar la UI se rompe | **Reproducido**: panel de dificultad desborda (usa medidas de 1920×1080 en un lienzo de 960×540) | 00 |
| 5 | Caminata: glitch al terminar | **Medido**: `Adelante` cae de 1,0 a 0 en un solo cuadro mientras `Velocidad` baja en 0,2 s → el árbol mezcla las 8 direcciones | 04 |
| 6 | Costado: base bajo tierra | **Medido**: pies 0,04–0,09 m por debajo de la altura de reposo (0,13) | 04 |
| 7 | Salto: aterriza mal | **Medido**: el cuerpo toca suelo a 0,91 s pero la animación sigue en `jump_loop`/`jump_down` 0,75 s más, con los pies 0,2 m arriba | 04 |
| 8 | Q tap vs mantenido | Lectura de código: umbral `SostenerParaMenu = 0.3` (piden 0,5); sin blanco el radial no hace nada por defecto | código |
| 9 | Bomba del asalto a la mitad de distancia | Lectura de código: `Demolicion.AlcanceMaximo = 9` (pasa a 4,5) | código |
| 10 | HUD lateral con demasiados datos | **Capturado**: cada ficha muestra rol, nombre, vida, arma, estado y "(vos)", con desborde a 3 líneas | 05 |
| 11 | HUD derecho: arma bugueada y falta francotirador | **Reproducido**: el Sniper muestra el **ícono del fusil**; solo existen `Icono_Rifle/Pistol/Heavy` | 05 |
| 12 | Enemigos y aliados sin dispersión/retardo | **Reproducido** (mismo dato que el 3) | 03 |
| 13 | Herido: dispara buscando cobertura, en ráfagas | Lectura de código: en cobertura solo `SetCrouching(true)`; no hay ráfaga ni disparo al moverse | código |
| 14 | E: mantener = especial, tap = interactuar | Lectura de código: la habilidad de clase va por `Ctrl` sostenido (`~1183/1260`); `E` solo interactúa | código |
| 15 | El médico no me revive + comando para matarme | **Reproducido** (ver 2). No existe comando de consola para bajar la vida | 02 |
| 16 | Cursor distinto sobre interactuables en RTS | Lectura de código: no hay ningún `Cursor.SetCursor` en el proyecto | código |
| 17 | Zoom RTS lento | Lectura de código: `rtsZoomSpeed=40` y `AnimarZoom` suaviza con exponencial (`VelocidadDeZoomAnimado`); pedido: sin lerp | código |
| 18 | RTS: 1/2/3 y F1/F2/F3 seleccionan | Lectura: `F1–F3` solo posee y está tras `AtajosDeTecladoHeredados = false` (apagado); dígitos = grupos de control | código |
| 19 | Resaltar cobertura apuntada en RTS | Lectura: ya existe `CoverHologram.Mostrar/Resaltar`, pero solo lo usa la orden de cubrirse | código |
| 20 | Todos muertos y en calma: revivir en 4 s | Lectura: `RescateAutomatico` solo vive con un médico vivo; nada revive a la escuadra completa | código |

Los ítems "solo código" tienen su medición ya escrita en el plan (sección *Verificación* de cada uno) y se
miden con la misma sonda en la ola correspondiente; no se simularon teclas humanas para no ensuciar el "antes".

## Detalle por corrida

### 00 · Menú y Jugar (punto 4) — `evidencia_antes/00_menu_jugar/`
Capturas del menú inicial y del juego a los 4 y 10 s. El panel de dificultad de `MainMenuController.MostrarDificultad`
se construye con tamaños pensados para 1920×1080 pero `MenuSceneBuilder` fija `CanvasScaler` a 960×540: los botones
ocupan el doble de lo previsto y se salen del panel.
Nota lateral: en varias capturas de juego sigue visible el texto "ABATIDO POR TU ESCUADRA" de una partida anterior, superpuesto al objetivo (posible residuo de estáticos; se cruza con el Bug 1 de la Ronda 10, **sin confirmar**).

### 02 · Fin de partida y rescate (puntos 1, 2, 15, 20) — `evidencia_antes/02_fin_de_partida/`
Línea de tiempo (tiempo de juego):
1. 34,26 s: se le quita toda la vida al soldado poseído (id 1). Muerte registrada. El médico (id 22) pasa un cuadro a `Follow` y a los 0,1 s vuelve a `Patrol`.
2. 54–66 s: `RescateAutomatico` sigue activo (`rescate=True`), el médico está quieto en (4.5, 0.8, 0) **a 2,8 m del caído**; el alcance para revivir es 2 m y nunca camina. A los 66 s el rescate vence (`rescate=False`, espera máxima 30 s).
3. 90,0 s: se mata a los dos aliados restantes. A los 98 s (8 s después) y a los 110 s (20 s): `aliados vivos=0`, `outcomeMostrando=False`, `fase=Infiltrar`. **La derrota nunca se dispara.**
Conclusión: dos fallas independientes. (a) la derrota (`Outcome.ShowDefeat`) se decide **una sola vez**, en la corrutina de muerte del soldado poseído (`PlayerInputDriver.cs` ~925–936, solo si `FindNearestFreeAlly` no devuelve a nadie en ese instante); si cuando cae el poseído quedaba algún aliado y estos mueren después, nadie vuelve a evaluar; (b) el rescate no mueve al médico hacia el caído (queda en `Patrol`, no en un estado de "ir a revivir").

### 03 · Puntería y alcance (puntos 3, 12) — `evidencia_antes/03_punteria_y_alcance/`
Una víctima inmóvil con vida enorme, un tirador a 8, 14, 20 y 28 m, 8–10 s por tramo:

| Tirador → víctima | Distancia | Disparos | Impactos | Acierto |
|---|---|---|---|---|
| enemigo → jugador | 8 m | 133 | 132 | 99 % |
| enemigo → jugador | 14 m | 45 | 45 | 100 % |
| enemigo → jugador | 20 m | 40 | 40 | 100 % |
| enemigo → jugador | 28 m | 33 | 33 | 100 % |
| aliado → enemigo | 8 m | 170 | 168 | 99 % |
| aliado → enemigo | 14 m | 161 | 161 | 100 % |
| aliado → enemigo | 20 m | 114 | 114 | 100 % |
| aliado → enemigo | 28 m | 146 | 136 | 93 % |

Sin dispersión efectiva y sin retardo entre ráfagas visible; el enemigo deja de disparar a 22 m (visión 22 / ataque 13) y el aliado a 20/12: por eso "disparan de cerca".
**Hallazgo a resolver en la ola 1:** `WeaponHolder.TryFire` sí aplica `ApplySpread(dir, SpreadDegEfectivo)` a todos (máx. 6°, +1,6° por disparo, decae 3°/s, multiplicado por postura, rol, enfoque y torreta fija), así que con 6° a 28 m el desvío sería de hasta ~2,9 m y no debería acertar el 93–100 %. Hipótesis en orden: (1) los multiplicadores dejan la dispersión de la IA cerca de 0 (agachado en cobertura, rol, enfoque); (2) el proyectil corrige hacia el blanco o el impacto es por línea, no por trayectoria; (3) el blanco de la prueba era un colisionador mayor que el cuerpo. Se mide con un `AntesProbe` que registre `SpreadDegEfectivo` y el ángulo real de cada bala antes de cambiar nada.

### 04 · Animaciones (puntos 5, 6, 7) — `evidencia_antes/04_animaciones/`
Guion determinista de movimiento (`AntesProbe.Guion`) y muestreo por cuadro de `Velocidad/Adelante/Lateral`, clip, transición, altura de la raíz y del pie más bajo.

**Caminata al terminar (5).** En el cuadro en que se suelta la tecla (t=2,07 s): `Velocidad` 0,71 pero `Adelante` 0,00. El árbol de mezcla queda con velocidad media y dirección cero, es decir mezcla las 8 caminatas (clips `walk forward/backward/left/right + diagonales`) durante ~0,2 s hasta llegar a `idle`. Es el "traba": los pies pasan por una pose promedio. Además, corriendo, `Adelante` oscila entre 0,79 y 1,09 (sobrepasa 1) y el estado alterna entre `rifle run` y la mezcla completa cuadro a cuadro.
Causa probable (dato coherente con lo medido, código a confirmar en la ola): `SoldierAnimatorDriver` pone la dirección en 0 al soltar (salta de golpe) pero suaviza solo `Velocidad`.

**Costado (6).** Pie más bajo en `run left` = 0,040 m y en `run right` = 0,092 m contra 0,129 m en reposo: entre 4 y 9 cm hundidos. (`cuerpoMinY` es constante porque los límites de los `SkinnedMeshRenderer` no se actualizan: no sirve como medida y se descarta.) Captura: `fotos/02_costado_izq_en_marcha.png`.

**Salto (7).**
| t | Evento |
|---|---|
| 0,30 s | orden de salto |
| 0,47 | `jump_up` |
| 0,87 | `jump_loop` (el cuerpo aún baja) |
| 0,91 | **el cuerpo toca suelo** (`IsJumping` pasa a 0, raíz 0,8) |
| 0,91–1,09 | sigue `jump_loop`, pies a 0,32 m (reposo 0,13) |
| 1,09–1,57 | recién entonces `jump_down`, con el soldado ya en el suelo |
| 1,65 | `idle` |
El aterrizaje visual llega **0,74 s tarde**. El salto físico dura 0,6 s; el clip de aire, 1,3 s. Capturas: `fotos/03_*`, `04_*`.

### 05 · HUD y francotirador (puntos 10, 11) — `evidencia_antes/05_hud/`
Textos vivos del HUD con Vega equipada con el francotirador:
- Ficha izquierda: `1 · Asalto` / `Soldado_1_Vega · 243/243 · Sniper · (vos)` (rol, nombre, vida numérica, arma, estado). Wrappea a 3 líneas y pisa la barra.
- Panel derecho: `[4] Francotirador 5/5 · 15` **con el ícono del fusil de asalto** (`WeaponStatusView.IconFor` mapea `Sniper → Rifle` y solo hay tres PNG en `Resources/UI/WeaponIcons`). También `Smg` y `Shotgun` reusan el del fusil.
- El numeral `[4]` sale de la posición en el `Loadout` de Vega (Rifle, Pistol, Rocket, Sniper); a verificar en la ola de HUD si coincide con la tecla que realmente equipa cada arma.
Captura: `fotos/01_hud_con_francotirador.png`.

## Regresión abierta: el autoplayer
La corrida `20260919_234914` (después de regenerar `SC_Tutorial.unity`) terminó `INTERRUMPIDA` con 9 pasos `FALLIDO_FORZADO` (cambiar, rts, seguir, seleccionar, mover_fps, ir_atacar, formaciones, cubrirse, curar) y tiempos reales negativos (`real=-17,2 s`): la corrida se solapó con mis sondas en el mismo Editor, así que **no es concluyente**. La anterior (`232009`) fue COMPLETA en 285 s. Acción del plan: repetir con el Editor libre antes de tocar nada; si vuelve a fallar, bisectar entre el escenario regenerado y el LOD por distancia.

## Cómo repetir
```
AntesProbe.Iniciar("nombre") → AntesProbe.Guion(id, "guion", "t,adelante,derecha,correr,saltar;...", duración)
→ AntesProbe.Foto("nombre") → AntesProbe.Cerrar()
```
Desde `unity.exe cmd eval` con Play activo. Los resultados quedan en `Logs/Antes/<nombre>/`.
