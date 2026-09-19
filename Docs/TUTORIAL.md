> **Ronda 7:** el tutorial ahora tiene 35 pasos y usa todos los comandos del radial. El recorrido completo con capturas está en [TUTORIAL_PASO_A_PASO.md](TUTORIAL_PASO_A_PASO.md).

# Módulo de tutorial — SC_Tutorial (26 pasos)

Escena `Assets/_Project/Scenes/SC_Tutorial.unity` con un nivel propio hecho **solo con cubos (blocking) y pintura de terreno**, y un módulo que enseña **todos** los controles del juego con un cuadro de diálogo, banderas booleanas, teclas iluminadas, balizas en el mundo, sonido y logs. Se abre desde el menú principal (botón **TUTORIAL**).

> **Actualizado en la ronda 6.** El tutorial pasó de 17 a **26 pasos** y de un esquema de teclas sueltas (F, T, G, Y, U, I, F1-3) a uno donde **todo lo que es una orden se da con el radial `[Q]`** y solo quedan como teclas directas: `TAB`, `WASD`, `Shift` (correr), `Ctrl` (agacharse), `Espacio`, `E` (interactuar), `R`, `1/2/3/4`, `C` (ver coberturas) y `F4` (modo dios). Ver `Docs/RONDA_6_RADIAL_CONTEXTUAL_TORRETA_MIRA_Y_UX.md`.
> Capturas de esta versión: `Assets/Validacion/Ronda6/` (30 a 40). Las capturas de la corrida original de 17 pasos siguen en `Assets/Validacion/Tutorial/` y `Assets/Validacion/Tutorial_Completo/` (los pasos que no cambiaron —cámara, WASD, disparar, agacharse, RTS básico— se ven igual). Los logs `Docs/Tutorial_Log_corrida_*.txt` son de esa versión de 17 pasos.
> El log de cada partida se escribe solo en `Logs/Tutorial_Log.txt`.

---

## 1. Cómo está armado

| Archivo | Qué hace |
|---|---|
| `Scripts/Tutorial/TutorialManager.cs` | Máquina de 26 pasos. Cada paso tiene sub-pasos y cada sub-paso una bandera. Solo **mira** lo que hace el jugador (teclas, estado, órdenes, impactos). Cada frame le dice al radial qué opción hay que elegir (`AplicarPistaDeRadial`). |
| `Scripts/Tutorial/TutorialFlags.cs` | Todos los booleanos (visibles en el Inspector, `TutorialManager > Flags`). |
| `Scripts/Tutorial/TutorialUI.cs` | Cuadro de diálogo (columna derecha bajo el minimapa): paso, título (se achica si es largo), barra de avance, instrucción, casillas de sub-pasos, teclas, pista, cartel "PASO N COMPLETADO". **El alto se calcula según el mensaje** (`Reflow`). |
| `Scripts/Tutorial/KeyCapView.cs` | Cada tecla / botón de mouse es un **RawImage** con textura generada por código: gris = normal, **amarillo = apretada ahora**, **verde = ya usada en el paso**. Suena un tick. Soporta `Shift`, `Ctrl`, `Tab`, `Espacio`, `MOUSE`, `LMB`, `RMB`, cualquier letra, dígito y `F4`. |
| `Scripts/Tutorial/TutorialBeacon.cs` | Baliza en el mundo (columna translúcida + anillo + etiqueta) que puede seguir a un objeto. |
| `Scripts/Tutorial/VictoriaTutorial.cs` | Efecto de victoria: fanfarria, destello, fuegos artificiales, confeti y panel final (REPETIR / VOLVER AL MENÚ). |
| `Scripts/Tutorial/TutorialLog.cs` | Log por paso y sub-paso (consola, `GameLog` y archivo). |
| `Scripts/Editor/TutorialSceneBuilder.cs` | Menú `Strategic Point > Tutorial > Construir escena SC_Tutorial` (idempotente). |
| `Scripts/UI/MenuDeOrdenes.cs` | `PonerPista(categoria, opcion)`: la porción/opción del radial que el paso pide **late en celeste con `▶ ELEGÍ ESTA`**. |

### Feedback (más de lo que había)
* Cada sub-paso cumplido: tick `TutSub` + etiqueta "OK" sobre el soldado + casilla verde con tilde + línea de log.
* Cada paso cumplido: arpegio `TutStep` + destello verde + cartel "PASO N COMPLETADO" + barra de avance.
* Cada tecla: se ilumina y suena (`TutKey`).
* **El radial señala la opción correcta** (celeste latiendo) y el cartel de la mira se pone **dorado** en las acciones contextuales.
* Balizas en el mundo sobre cada objetivo (aliados, herido/caído, muro, torreta, tanque, meta).
* Pista amarilla si el jugador se traba 16 s.

---

## 2. Los 26 pasos

Cómo leer la tabla: **Detección** = qué mira el manager para tildar el sub-paso. Los pasos con ★ son nuevos o cambiaron en la ronda 6.

| # | Paso | Teclas | Sub-pasos y detección |
|---|---|---|---|
| 1 | MOVER LA CÁMARA | mouse | Mirar a los costados (≥ 50° de yaw acumulado) y arriba/abajo (≥ 18° de pitch). |
| 2 | MOVERSE CON W A S D | W A S D | Las cuatro teclas se iluminan y quedan verdes (`kb.wKey.isPressed`…). |
| 3 ★ | CORRER CON SHIFT | Shift W | Correr ≥ 1,2 s (`Motor.Corriendo`); soltar Shift y seguir caminando. Los aliados corren con vos. |
| 4 | DISPARAR | LMB R | Matar al enemigo quieto · impactar la pared gris · destruir la caja amarilla (balizas roja / gris / amarilla). |
| 5 ★ | CAMBIAR DE SOLDADO (RADIAL) | Q | Mantener `Q` (radial abierto) · **apuntar a un aliado** y elegir POSEER A ESTE (dorado). Cambia `Brain.Current`. |
| 6 | AGACHARSE | Ctrl | Mantener (`Motor.IsCrouching`) y soltar. |
| 7 ★ | APUNTAR: MIRA EN PRIMERA PERSONA | RMB LMB | Mantener el clic derecho · la cámara llega al ojo (`Rig.AdsBlendSuave > 0.9`) · disparar apuntando · soltar (vuelve al hombro). |
| 8 ★ | VER COBERTURAS Y RUTAS | C | Mantener `C` (`Coberturas.MarcasVisibles`) y soltar (se esconden). |
| 9 | RTS: ORDEN A LOS 2 | Tab LMB RMB | `TAB` (`Rig.Mode == Rts`) · seleccionar a los 2 aliados (recuadro o Ctrl+A; contador) · clic derecho en el círculo verde (`MoveOrderIssuedEvent`). Rueda = zoom hacia el cursor. |
| 10 | VOLVER A PRIMERA PERSONA | Tab | `TAB` (`Mode == Fps`) y un clic izquierdo para recapturar el mouse. |
| 11 ★ | SÍGANME Y QUIETOS (RADIAL) | Q | `Q` → POSICIÓN → SÍGANME (los 2 en `AiState.Follow`) → `Q` → POSICIÓN → TODOS QUIETOS (`Brain.Quieto`). |
| 12 | SELECCIONAR EN FPS | Shift RMB | Shift + clic derecho sobre un aliado (`Selection.Selected`). *(Shift sigue seleccionando acá; correr solo cuenta al caminar hacia adelante.)* |
| 13 | ORDEN DE MOVER EN FPS | RMB | Clic derecho en el suelo con un aliado seleccionado. |
| 14 ★ | CUBRIRSE HACIA DONDE MIRO (RADIAL) | Q | `Q` → CUBRIRSE → TODOS **mirando las coberturas de práctica** (los discos de cobertura se ven mientras el radial está ahí) · los 2 quedan agachados a cubierto (`Brain.EnCobertura`). |
| 15 ★ | CURAR A UN ALIADO (RADIAL) | Q | **Apuntar al herido** (columna roja) → `Q` → CURAR A ESTE (dorado) · el médico llega y sube su vida ≥ 90 %. |
| 16 ★ | REANIMAR A UN CAÍDO (RADIAL) | Q | **Apuntar al caído** → `Q` → REVIVIR A ESTE (dorado, solo con médico vivo) · el médico se queda 4 s y lo levanta (`Health.IsAlive`). |
| 17 ★ | DEMOLER UN MURO (ASALTO) | Ctrl | Apuntar al muro de práctica (el radial ofrece DEMOLER en dorado) · **Ctrl agachado y quieto** carga 4 s (`DemoledorAsalto.Progreso`) · el muro vuela en pedazos. |
| 18 ★ | AMETRALLADORA FIJA | E | Acercarse a la torreta de práctica (≤ 4,5 m) · **E** (o `Q` → TORRETA FIJA → USAR) la ocupa (`TorretaFija.Ocupante`) · dispararla (100 balas, `ShotFiredEvent` estando en ella) · **E** para salir. |
| 19 ★ | MODO DIOS | F4 | `F4` (`ModoDios.Activo`) y otra vez `F4` para apagarlo. |
| 20 ★ | ENTRAR AL TANQUE (RADIAL) | Q | Caminar hasta ≤ 6,5 m del tanque · apuntarlo (cartel dorado `[E] SUBIR`) y `Q` → TANQUE → SUBIRME YO (o `E`). |
| 21 ★ | ALIADOS AL TANQUE (RADIAL) | Q | Dentro del tanque: `Q` → TANQUE → SUBIR TODOS · los 2 a bordo (`Vehicle.RoleOf`). |
| 22 | CAMBIAR A LA TORRETA | 2 | `2` (`CurrentSeat == Gunner`); intercambia con quien esté. |
| 23 ★ | MIRA DEL CAÑÓN Y LA METRALLETA | RMB | Clic derecho en el cañón (mira telescópica) · `3` + clic derecho en la metralleta (mira de anillo) · soltar y volver al cañón. Detecta `CurrentSeat`, `Rig.EstaConZoom` y `Rig.AdsBlend > 0.9`. |
| 24 ★ | AVANZAR Y DISPARAR | Q LMB | `Q` → TANQUE → TANQUE ALLÍ (el tanque se mueve > 6 m) · disparar el cañón (`TurretWeapon.CooldownFraction01`) · derribar 3 enemigos de la ola A. |
| 25 | LLEGAR AL FINAL | Q | Mandar el tanque a la meta dorada (≤ 9 m). Al pasar `meta.z - 34` aparece la ola B (opcional). |
| 26 | ¡TUTORIAL COMPLETADO! | — | Fanfarria, fuegos artificiales, confeti y panel con REPETIR / VOLVER AL MENÚ. |

### Capturas de los pasos nuevos o cambiados (`Assets/Validacion/Ronda6/`)

| | |
|---|---|
| ![](../Assets/Validacion/Ronda6/34_tutorial_poseer_radial.png) | ![](../Assets/Validacion/Ronda6/30_tutorial_mira_primera_persona.png) |
| **Paso 5** — radial contextual con POSEER en dorado y la pista celeste `▶ ELEGÍ ESTA`. | **Paso 7** — mira en primera persona; el título largo se achica. |
| ![](../Assets/Validacion/Ronda6/31_tutorial_cubrirse_coberturas_visibles.png) | ![](../Assets/Validacion/Ronda6/39_tutorial_reanimar.png) |
| **Paso 14** — radial sobre CUBRIRSE con las coberturas visibles en el piso. | **Paso 16** — reanimar al caído. |
| ![](../Assets/Validacion/Ronda6/40_tutorial_demoler.png) | ![](../Assets/Validacion/Ronda6/33_tutorial_torreta_disparando.png) |
| **Paso 17** — demoler (carga agachado y quieto). | **Paso 18** — ametralladora fija: los 3 sub-pasos tildados, falta salir con `E`. |
| ![](../Assets/Validacion/Ronda6/38_tutorial_modo_dios.png) | ![](../Assets/Validacion/Ronda6/35_tutorial_tanque_radial.png) |
| **Paso 19** — `F4`. | **Paso 20** — apuntando al tanque: TANQUE en dorado con `SUBIRME YO` / `SUBIR TODOS`. |
| ![](../Assets/Validacion/Ronda6/36_tutorial_mira_canon.png) | ![](../Assets/Validacion/Ronda6/37_tutorial_victoria.png) |
| **Paso 23** — mira del cañón. | **Paso 26** — victoria. |

Más: [`32_tutorial_torreta_apuntada`](../Assets/Validacion/Ronda6/32_tutorial_torreta_apuntada.png).

### Cómo se probó esta versión
En Play, con el Editor enfocado: **teclas reales** (`QueueStateEvent(Keyboard.current, …)`: `Shift+W`, `C`, `E`, `F4`, `Q`, `Ctrl`, `2`, `3`), **botones y deltas de mouse reales** (`MouseState`, `QueueDeltaStateEvent`) para el radial (cursor virtual), el clic derecho de la mira y el disparo. Para *posicionar* la mira o al soldado (no para actuar) se usó la API (`Brain.RotateYaw`, `SetDestination`, teletransportes). Los pasos que **no** cambiaron respecto de la versión de 17 pasos (cámara, WASD, disparar, agacharse, RTS básico, seleccionar/mover en FPS, avanzar, final) se dieron por cumplidos con `TutorialManager.SaltarPaso()` porque ya estaban validados con gestos reales. Se recorrieron los **26 pasos hasta la victoria**; el log de esa corrida termina en `[FIN] Tutorial finalizado`.

Hallazgos que salieron de esta corrida y se corrigieron: mensajes largos que pisaban las casillas del cuadro (`Reflow`), títulos que se partían en dos (`resizeTextForBestFit`), el sub-paso "Dispara una ráfaga" de la torreta que no se tildaba (faltaba el enganche a `ShotFiredEvent`) y la baliza 3D de la torreta que tapaba la pantalla estando adentro (se retira al ocuparla).

---

## 3. Logs

Formato: `[TUTORIAL] <segundos> [PASO nn/26 TÍTULO] <mensaje>`. Tipos: `INICIO` del paso (con sus sub-pasos `PENDIENTE`), `sub-paso k/n CUMPLIDO`, `PISTA`, `COMPLETADO en X s`, y `[EVENTO]` (olas, derribos, órdenes del radial: `radial: categoria 5 (CURAR) opcion 3 (REVIVIR A ESTE)`), `[VICTORIA]`, `[FIN]`.

```
[TUTORIAL] 649,6s [PASO 25/26 LLEGAR AL FINAL]    sub-paso 1/1 CUMPLIDO: Llega a la meta dorada (58 m)
[TUTORIAL] 649,6s [PASO 25/26 LLEGAR AL FINAL] COMPLETADO en 3,5 s
[TUTORIAL] 651,7s [VICTORIA] EFECTO DE VICTORIA: fanfarria + fuegos artificiales + confeti + panel final
[TUTORIAL] 651,7s [PASO 26/26 ¡TUTORIAL COMPLETADO!] INICIO · 0 sub-pasos · teclas: -
[TUTORIAL] 653,3s [VICTORIA] Panel final visible: REPETIR TUTORIAL / VOLVER AL MENU
[TUTORIAL] 653,5s [FIN] Tutorial finalizado en 571,9 s · 0 bajas
```

---

## 4. `SetDestination(Vector3)` — mover al jugador sin cursor

`PlayerInputDriver` expone:

```csharp
public void SetDestination(Vector3 punto);   // a pie: camina solo hasta el punto; en vehículo: manda el vehículo (con un aliado al volante si hace falta)
public void CancelDestination();
public bool TieneDestino { get; }
```
Sirve por mensaje: `driver.SendMessage("SetDestination", new Vector3(0, 0, 103));`. WASD lo interrumpe. En esta ronda además lo usa `UsarTorreta` para que el soldado camine solo hasta una ametralladora fija lejana.

---

## 5. Nivel (blocking)

Terreno 70 × 210 m, de sur a norte: **campo de tiro** (pared gris, caja amarilla destructible, enemigo quieto) → **plaza** (2 aliados lejos, zona A y zona B) → **patio del tanque** → **corredor** de 30 m con 3 chicanes y la **meta** (pilares + dintel dorado). Los pasos nuevos crean sus propios objetos de práctica en runtime y los limpian al salir: **2 coberturas de sacos** (paso 14), **muro demolible** (paso 17) y **ametralladora fija de práctica** (paso 18: base baja, trípode y cañón hechos con primitivas + `TorretaFija.Instalar`).
