# Ronda 11 — Plan de implementación (20 puntos + integración de la Ronda 10)

Solo plan: **no se cambió código de juego**. El "antes" medido está en [`ANTES.md`](ANTES.md) (datos y capturas en
`evidencia_antes/`). Cada punto lleva: *Antes* (qué se vio), *Por qué* (causa), *Cambio* (qué hacer), *Archivos*,
*Verificación* (qué debe decir el log/`Check()` después) y *Riesgo*. La Ronda 10 (`../RONDA_10_PLAN_CORRECCION.md`, 100 bugs, 8 olas)
se cruza en la sección 3.

## 0. Decisiones que conviene fijar antes de empezar

| # | Tensión | Propuesta (por defecto) |
|---|---|---|
| D1 | Punto 1 (al morir todos, termina) contra punto 20 (todos muertos y en calma, revivir en 4 s) | Al caer el último aliado se abre una **ventana de decisión**: si hay acción (enemigo vivo a menos de su rango de visión o daño recibido en los últimos 4 s) → derrota tras 2 s de cámara de muerte; si hay calma → cuenta de 4 s y reviven. La derrota se reevalúa cada cuadro mientras la ventana esté abierta, no una sola vez. |
| D2 | Punto 8: "menos de medio segundo" | Umbral `SostenerParaMenu` 0,3 → **0,5 s**. Al soltar antes = acción por defecto; sostener 0,5 s abre el radial. |
| D3 | Punto 14: quitar `Ctrl` de la acción especial | `E` mantenido ≥ 0,5 s = habilidad de clase; `E` tap = interactuar. `Ctrl` queda solo para agacharse y (RTS) guardar grupos. Ojo: `E` ya tiene un mantener de 5 s para revivir a un aliado (`TiempoDeRevivir`); se resuelve por contexto (con caído a mano gana revivir). |
| D4 | Punto 17: "sin lerp" | Zoom instantáneo con `rtsZoomSpeed` 40 → 80 (×2). El anclaje al cursor (`ZoomHaciaCursor`) se conserva, aplicado en el mismo cuadro. |
| D5 | Punto 10: HUD izquierdo | Tres renglones exactos por ficha: `N-Especialidad` / `Vida-Arma` / barra. Sin nombre propio, sin estado, sin "(vos)": el poseído se marca con el color de la ficha. |

## 1. Los 20 puntos

### 1 · Al morir todos no termina la partida
- **Antes:** con 0 aliados vivos, 8 s y 20 s después: `outcomeMostrando=False`, fase `Infiltrar` (corrida 02).
- **Por qué:** `Outcome.ShowDefeat` solo se llama dentro de la corrutina de muerte del poseído (`PlayerInputDriver.cs` ~925–936) y solo si en ese instante no hay aliado libre. Los aliados que mueren después nunca se reevalúan.
- **Cambio:** un evaluador único `EstadoDePartida.Tick()` (nuevo, en `Assets/_Project/Scripts/Mision/`) que corre siempre: cuenta vivos de `TeamId.Player`; si es 0 aplica D1. Lo llaman también `EntityDiedEvent` y el cambio de modo. `MisionDirector.Perder` sigue siendo el único que muestra derrota.
- **Archivos:** `PlayerInputDriver.cs`, `GameOutcomeController.cs`, `MisionDirector.cs`, nuevo `EstadoDePartida.cs`.
- **Verificación:** `AntesProbe` con el mismo guion (mata al poseído, luego al resto): esperar `ShowDefeat` ≤ 2 s tras el último; `Check()` nuevo en la fase de misión; no debe quedar `IsShowing=false` con 0 vivos y acción.
- **Riesgo:** medio (toca el flujo de muerte, Ronda 10 bugs 69/71).

### 2 y 15 · El médico no revive / no viene a revivirme (+ comando de consola para matar al poseído)
- **Antes:** médico en `Patrol` a 2,8 m del caído, nunca camina; el rescate vence a los 30 s (corrida 02).
- **Por qué:** `RescateAutomatico.Tick` marca el rescate pero no deja al médico en un estado que lo lleve a alcance (`AlcanceDeRevivir = 2`); tras el `Follow` de un cuadro el cerebro vuelve a `Patrol`. Coincide con el Bug 69 de la Ronda 10 ("el rescatista nunca sale") y el 29 ("revivir no avisa").
- **Cambio:** (a) estado explícito `Rescue` en `AiBrain`, que navega hasta 1,5 m del caído, canaliza `TiempoDeCanal` (5 s) y llama al revivir; no lo interrumpe ni `Patrol` ni el retorno a formación; (b) si el médico está a más de `RadioDeSeguridad` de enemigos, sí sale; si no, espera en cobertura; (c) publicar `HealedEvent`/`SoldierRevivedEvent` para HUD y sonido; (d) **comando de consola** `matar` (y `matar <id>`): quita toda la vida al poseído/al id indicado vía el mismo `Health.TakeDamage` que usa el combate, para reproducir el caso a mano; `revivir` de compañía.
- **Archivos:** `RescateAutomatico.cs`, `AiBrain.cs` (+ `AiState`), consola junto a `Core/ModoDios.cs`, `SoldierRevive*` si existe.
- **Verificación:** `AntesProbe` corrida 02: esperar `estado_ia Rescue` del médico dentro de 1 s, `cura` y `posesion` al revivido, todo antes de 12 s con enemigos lejos; caso con enemigo cerca: el médico no sale y el log lo dice; `Check()` "medico_revive_en_alcance".
- **Riesgo:** alto (IA + posesión). Va con Ronda 10 Ola 2 (bugs 29, 69).

### 3, 12 y 13 · Dispersión, alcance, ráfagas y disparar al recibir impacto
- **Antes:** 93–100 % de aciertos de 8 a 28 m para enemigos y aliados; el enemigo ataca hasta 13 m (ve a 22), el aliado hasta 12 m (ve a 20) (corrida 03). Herido, solo se agacha (`SetCrouching(true)`), no dispara mientras busca cobertura.
- **Por qué:** `TryFire` ya aplica dispersión pero el resultado medido es ~0 (ver hallazgo en `ANTES.md`): hay que **medir el ángulo real** antes de tocar. El alcance de ataque es corto por `attackRange`/`EffectiveAttackRange`.
- **Cambio:** (a) primero, sonda que registre `SpreadDegEfectivo` y desvío real por bala; (b) dispersión de IA propia por *dificultad × distancia × postura*, con un mínimo de 1,5° para enemigos y 1° para aliados, y un retardo de reacción aleatorio de 0,25–0,6 s al adquirir blanco; (c) alcance efectivo por arma (`WeaponCatalog`: fusil 35 m, SMG 25 m, escopeta 12 m, francotirador 60 m) tanto de enemigos como de aliados, con `visionRange` ≥ alcance; (d) **ráfagas**: 3–5 disparos, pausa 0,4–0,9 s; (e) al recibir un `DamageTakenEvent` con IA en `Attack`/`Chase` y cobertura disponible, pasar a `SeekCover` **sin dejar de disparar** (ráfagas, dispersión ×1,5 mientras se mueve) hasta llegar; ya en cobertura, asoma y dispara.
- **Archivos:** `AiBrain.cs` (`TickCoberturaTactica`, ~757, ~1052–1115), `WeaponHolder.cs`, `WeaponCatalog.cs`, `Core/Dificultad.cs`.
- **Verificación:** repetir la corrida 03: acierto objetivo enemigo 25–45 % a 20 m y aliados 40–60 % según dificultad; distribución de ráfagas en el log (longitud media 3–5); disparos durante `SeekCover` > 0; `BalanceBench` antes/después (ver sección 4).
- **Riesgo:** alto (cambia el balance): siempre con `BalanceBench` y las olas de la Ronda 10 (bugs 13/37/22/48).

### 4 · Al darle Jugar la UI se rompe
- **Antes:** panel de dificultad desborda (capturas de la corrida 00).
- **Por qué:** `MainMenuController.MostrarDificultad` usa medidas de 1920×1080 en un lienzo de referencia 960×540 (`MenuSceneBuilder`).
- **Cambio:** unificar la resolución de referencia (1920×1080) en `MenuSceneBuilder` y `CanvasScaler` con `matchWidthOrHeight = 0.5`; revisar el resto de paneles del menú con la misma escala; regenerar `SC_Menu` con el builder (no con `git checkout`: escena serializada binaria).
- **Archivos:** `MainMenuController.cs`, `MenuSceneBuilder.cs`, `SC_Menu.unity`.
- **Verificación:** capturas a 1920×1080, 1280×720 y 800×600 con `AntesProbe.Foto`; `Check()` que todo `RectTransform` del panel cae dentro del rect del lienzo.
- **Riesgo:** bajo.

### 5, 6 y 7 · Animaciones: caminata, costado y salto
- **Antes:** ver corrida 04. Parada: `Adelante` 1→0 en un cuadro (mezcla de 8 clips ~0,2 s). Costado: pies 4–9 cm bajo la altura de reposo. Salto: aterrizaje visual 0,74 s tarde, `jump_loop` en el suelo.
- **Cambio:** (5) en `SoldierAnimatorDriver`, suavizar `Adelante`/`Lateral` con el mismo `Damp` que `Velocidad` (0,08 s), clamp a [-1,1] y solo poner la dirección en 0 cuando `Velocidad` < 0,05; (6) revisar `Root Transform Position (Y)` y *Foot IK* de `run left/right`: activar *Bake Into Pose* en Y y *Offset Y* del clip; si el clip está mal, corregir el `Center of Mass` en el importador (`.fbx.meta` / `AC_Soldado.controller`); (7) el bool `Salto` debe volverse `false` en el cuadro de aterrizaje y `jump_down` entrar por `Grounded` con `exit time = 0`; recortar `jump_loop` a la duración real de vuelo o hacerla dependiente de la velocidad vertical; duración de `jump_down` ≤ 0,25 s.
- **Archivos:** `SoldierAnimatorDriver.cs`, `AC_Soldado.controller`, importadores de clips.
- **Verificación:** repetir la corrida 04: en la parada `Adelante` decae ≥ 0,15 s sin saltar; `pieMinY` en costado ≥ reposo − 0,01; `salto` cae a 0 y el clip pasa a `idle` ≤ 0,3 s tras tocar suelo.
- **Riesgo:** medio (arte/animador); mantener los clips originales versionados.

### 8 · Q apretada contra Q mantenida
- **Antes:** umbral 0,3 s; sin blanco el radial por defecto no hace nada (código).
- **Cambio:** D2 (0,5 s). Tap: acción por defecto (ciclar/poseer al apuntado). Mantener: radial. **En el radial por defecto, si no apunto a nada, la acción es "Seguir"** (los aliados siguen al jugador), en la misma función que resuelve la orden por defecto.
- **Archivos:** `PlayerInputDriver.Radial.cs` (`ResolverGestoDeQ`, `SostenerParaMenu`), `KeyBindings.cs`, `HeadlessTestRunner.Fases8a12.cs` (usa la constante).
- **Verificación:** `Check()` con pulsaciones simuladas a 0,2 s (tap) y a 0,6 s (radial); sin blanco: `ordenDeSeguir` en el log.
- **Riesgo:** bajo.

### 9 · Bomba del asalto a la mitad de distancia
- **Antes:** `Demolicion.AlcanceMaximo = 9 m`; el mensaje dice "MAX 9 m".
- **Cambio:** 9 → 4,5 m y actualizar el texto (derivarlo de la constante). Revisar `DemoledorAsalto` para que la IA se acerque a 4,5 m.
- **Archivos:** `Demolicion.cs`, la lógica de `DemoledorAsalto` (si vive en otro archivo), tests de demolición.
- **Verificación:** `Check()` de rechazo a 5 m y aceptación a 4 m; tutorial paso de demolición actualizado.
- **Riesgo:** bajo.

### 10 y 11 · HUD izquierdo y derecho
- **Antes:** ficha izquierda con rol, nombre, vida numérica, arma, estado y "(vos)", con desborde (captura 05). Derecha: el Sniper usa el ícono del fusil; solo hay 3 íconos (Rifle, Pistol, Heavy).
- **Cambio:** (10) `RosterRowView` con exactamente 3 renglones (D5): `N · Especialidad` / `Vida · Arma` / barra; texto de 11 px sin *wrap*; el mismo formato en la esquina derecha para el arma actual (arma-munición-reserva), sin más datos. (11) Crear `Icono_Sniper.png` (y `Icono_Smg`, `Icono_Shotgun`, `Icono_Rocket`); quitar el mapeo de familia en `WeaponStatusView.IconFor` y ampliar `iconCache` a 8; comprobar que la munición y el cargador del francotirador (5/5) coincidan con `WeaponCatalog`. Verificar por qué el numeral `[4]` coincide (o no) con la tecla de arma.
- **Archivos:** `RosterRowView.cs`, `WeaponStatusView.cs`, `Resources/UI/WeaponIcons/*`, el constructor del HUD.
- **Verificación:** capturas antes/después con el mismo guion (05); `Check()`: cada `WeaponKind` tiene ícono propio; ninguna ficha supera 3 líneas a 1920×1080 y 1280×720.
- **Riesgo:** bajo (el arte del ícono lo aporta quien lo dibuje; mientras tanto se genera uno procedimental).

### 14 · `E` mantenido = acción especial, `E` tap = interactuar
- **Antes:** la habilidad de clase se activa con `Ctrl` sostenido (`PlayerInputDriver.cs` ~1183/1260/1583+); `E` solo interactúa.
- **Cambio:** D3. `KeyBindings.AccionEspecial = Key.E` con `HeldSeconds ≥ 0,5`; al soltar antes, `TryInteractuarConMira`. Quitar `Ctrl` de `ActualizarHabilidadDeClase`. Actualizar `ControlsTable` y los textos de ayuda/tutorial.
- **Archivos:** `PlayerInputDriver.cs`, `PlayerInputDriver.Interaccion.cs`, `KeyBindings.cs`, `ControlsTable.cs`, tutorial.
- **Verificación:** `Check()` de tap/mantener; ningún texto de HUD ni `Tutorial` menciona "Ctrl" como acción especial (búsqueda en `verificar_estatico.py`).
- **Riesgo:** medio (reasigna una tecla; se combina con la ola de entrada de la Ronda 10).

### 16 · Cursor distinto sobre interactuables en RTS
- **Antes:** no hay ningún `Cursor.SetCursor` en el proyecto.
- **Cambio:** nuevo `CursorContextual` que, en RTS con soldados seleccionados, lanza el raycast bajo el cursor y elige entre *predeterminado / interactuable (mano) / atacar (mira) / mover*; texturas de 32×32 en `Resources/UI/Cursores`; volver al cursor por defecto al salir de RTS o del foco.
- **Archivos:** nuevo `CursorContextual.cs`, `PlayerInputDriver.cs` (~2784 RTS), recursos.
- **Verificación:** `Check()` de que el estado del cursor cambia con interactuables/enemigos; captura por estado.
- **Riesgo:** bajo.

### 17 · Zoom RTS
- **Antes:** `rtsZoomSpeed = 40` con `AnimarZoom` suavizado exponencial.
- **Cambio:** D4: velocidad ×2 y aplicar la altura objetivo directamente (sin `Lerp`), manteniendo el anclaje al pixel del cursor.
- **Archivos:** `PlayerInputDriver.cs` (~73, ~2809), `CameraRig.cs` (`AnimarZoom`, `ZoomHaciaCursor`).
- **Verificación:** medir con el probe el tiempo de 5 desplazamientos de rueda: de ~0,6 s a ≤ 0,1 s hasta la altura objetivo.
- **Riesgo:** bajo.

### 18 · RTS: `1 2 3` y `F1 F2 F3` seleccionan soldados
- **Antes:** `F1–F3` solo *poseen* y están tras `AtajosDeTecladoHeredados = false`; los dígitos son grupos de control (Ctrl guarda, dígito recupera).
- **Cambio:** en RTS, `1/2/3` y `F1/F2/F3` seleccionan el soldado 1/2/3 de la escuadra (Shift suma); doble toque = centrar cámara. Los grupos de control pasan a `Ctrl+4…9` para no chocar. En FPS, `F1–F3` siguen poseyendo (sin depender del bandera heredada).
- **Archivos:** `PlayerInputDriver.cs` (~770, ~3225 `UpdateControlGroups`), `ControlsTable.cs`.
- **Verificación:** `Check()` con teclas simuladas en RTS y en FPS.
- **Riesgo:** medio (reasigna grupos de control ya documentados en el tutorial).

### 19 · Resaltar la cobertura apuntada en RTS
- **Antes:** `CoverHologram.Mostrar/Resaltar` ya existe pero solo lo usa la orden de cubrirse.
- **Cambio:** en RTS con soldados seleccionados, resaltar (contorno + color) el obstáculo con cobertura bajo el cursor usando el mismo `Resaltar(obstaculo)`; ocultar al mover fuera.
- **Archivos:** `CoverHologram.cs`, `PlayerInputDriver.cs` RTS.
- **Verificación:** captura antes/después; `Check()` de `CoverHologram.Visible` al apuntar y no al salir.
- **Riesgo:** bajo.

### 20 · FPS: todos muertos y en calma → los aliados reviven en 4 s
- **Antes:** `RescateAutomatico` solo existe con un médico vivo; nadie revive a la escuadra entera.
- **Cambio:** parte de D1: si los aliados están todos muertos y "calma" (ningún enemigo con visión sobre el cadáver más cercano, sin `DamageTakenEvent` en 4 s, sin `ShotFiredEvent` enemigo a < 40 m), se corre una cuenta de 4 s con indicador en HUD; al cumplirse, `Health.Revive` a todos con 50 % de vida, posición del cadáver. Cualquier acción cancela la cuenta y, si sigue todo muerto, aplica la derrota.
- **Archivos:** nuevo `EstadoDePartida.cs` (compartido con el 1), `Health.cs`, HUD.
- **Verificación:** `AntesProbe` con enemigos apagados: `cura` ×3 entre 4,0 y 4,5 s; con un enemigo activo: `ShowDefeat` sin revivir.
- **Riesgo:** medio.

## 2. Orden de las olas de esta ronda

| Ola | Contenido | Por qué en este orden |
|---|---|---|
| A | Cimientos: sonda `AntesProbe` con `SpreadDegEfectivo`, repetir el autoplayer con el Editor libre, comando `matar`/`revivir` | Sin el comando no se puede reproducir el 2/15 a mano; sin la sonda no se sabe por qué el 3/12 acierta el 100 % |
| B | Puntos 1, 2/15, 20 (+ Ronda 10 Ola 2: 69, 71, 29, 31) | Reglas de partida rotas; comparten `EstadoDePartida` y `RescateAutomatico` |
| C | Puntos 3, 12, 13 (+ Ronda 10 Ola 3: 36, 37, 41–44) | Todo en `AiBrain`; se mide con `BalanceBench` |
| D | Puntos 5, 6, 7 | Independientes del combate; se miden con la corrida 04 |
| E | Puntos 8, 14, 16, 17, 18, 19 (+ Ronda 10 Ola 5: 61–68, 72–84) | Capa de entrada: una sola pasada de pruebas de UI |
| F | Puntos 4, 9, 10, 11 (+ Ronda 10 Ola 7: UI y limpieza) | HUD y menú: capturas antes/después |
| G | Ronda 10: Olas 0, 1, 4, 6 restantes y los **22 pasos nuevos del tutorial** (`../RONDA_10_TUTORIAL_22_PASOS_NUEVOS.md`) | Se deja para el final lo que no bloquea a los 20 puntos, salvo Ola 0 que va **antes de A** por reproducibilidad |

Regla de cierre de cada ola (la de la Ronda 10): `Strategic Point > Run All Tests Headless` en verde + `Tools/Qa/qa_total.bat --rapido` sin pasos fallidos + `python Tools/Ci/verificar_estatico.py` en verde, y una corrida del autoplayer completa. Ola 0 de la Ronda 10 (reset de estáticos, bugs 1, 8, 9, 10, 47, 55, 70, 90, 100) va primera: hace repetibles las mediciones del "después".

## 3. Cruce con la Ronda 10

| Punto de esta ronda | Bugs de la Ronda 10 que comparte | Nota |
|---|---|---|
| 1, 20 | 71 (HUD del arma tras morir), 34, 49–50 (revivir) | Reutilizan el camino de revivir |
| 2, 15 | 29 (revivir no avisa), 69 (el rescatista nunca sale), 70 (estáticos del rescate) | El 70 va antes por reproducibilidad |
| 3, 12, 13 | 13/37 (doble tick del arma), 14 (recarga gratis), 22 (balas atraviesan paredes), 36, 41–44 | 13/37/22 cambian el balance: `BalanceBench` antes y después |
| 5–7 | 48–50 (agacharse, cámara hundida, salto fantasma), 51 | El 51 es "salto fuera del camino de simulación" |
| 8, 14, 16–19 | 61–68, 72–84 (órdenes y entrada) | Se hacen juntos |
| 4, 10, 11 | 11, 12, 16–21 (UI, materiales) | Misma ola de limpieza |

## 4. BalanceBench: antes y después

Los cambios de 3/12/13, el alcance de la IA y la ventana de calma del 20 mueven el balance. Registrar antes de la Ola C: bajas por minuto, tiempo de partida, vida media al final, aciertos por distancia; repetir tras cada ola de combate. Se aceptan desviaciones ≤ 15 % en dificultad FACIL y se ajusta con `Dificultad`, no con constantes sueltas.

## 5. Abiertos y trabajo que sigue pendiente

- Repetir el autoplayer con el Editor libre (la corrida `234914` se solapó con las mediciones).
- Auditoría de los 100 puntos anteriores: quedan 21 (arte), 81 (`UNITY_LICENSE` del dueño), 89 (decisión sobre `ARTS`), 51/65 (no aplican) — ver `../AUDITORIA_100_ITEMS.md`.
- Autorizar los MCP `github`, `sentry` y `supabase-user` en el cliente de Claude (no pueden hacerse desde una sesión sin interacción).
