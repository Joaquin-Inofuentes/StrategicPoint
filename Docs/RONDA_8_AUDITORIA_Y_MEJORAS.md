# Ronda 8 — Auditoría de 100 puntos, mejoras implementadas y tutorial actualizado

Se auditó el juego jugando `SC_Gameplay` con input real (capturas, mediciones en Play y revisión del código) y se armó una lista de 100 puntos. Esta ronda **implementa lo que se pudo implementar y probar**, corrige la lista donde la auditoría se equivocó y deja explícito qué quedó pendiente. Capturas en `Assets/Validacion/Ronda8/`. La suite headless quedó **en verde con una fase nueva (FASE 17)**.

## Corrección honesta de la auditoría

Al ir a implementar, 9 puntos resultaron **falsos o ya resueltos**; la lista original los marcó por no haberlos verificado a fondo:

| Punto | Lo que se dijo | Realidad |
|---|---|---|
| 16 | Sin viñeta ni dirección de daño | Existen `DamageVignetteView`, `DamageDirectionView` y `LowHealthPulseView` |
| 17 | [F]/[G] sin enfriamiento | El cuchillo se pone gris en enfriamiento y la granada dice «SIN GRANADAS» |
| 22 | El menú no ofrece dificultad | Al apretar JUGAR aparece el panel de dificultad |
| 29 | Morir no da feedback | Hay `DeathSequence`: cámara al cadáver, anillo del asesino y rescate automático |
| 46 | Sin retroceso | Existe `KickRecoil` |
| 47 | Sin hitmarker | Existe en `AimUI` |
| 48 | Sin rueda para cambiar arma | Existe (rueda del mouse en `PlayerInputDriver`) |
| 67 | Sin pasos | Hay `footstep_grass_*` y `FootstepConcrete` |
| 7 | Controles omite atajos | Es por diseño: la lista es contextual (FPS muestra solo lo de FPS) |

## Cambios implementados

### 1. HUD y menús (puntos 1, 2, 3, 4, 9, 12, 13, 15, 68)

| Antes | Ahora |
|---|---|
| Cartel «Obstáculo destructible» enorme, fijo aunque apuntes a un muro a 100 m | Solo aparece a menos de 25 m y con letra de 20 px |
| El título PAUSA y el panel de Controles quedaban **debajo** del HUD | `PauseController` pasa al frente al abrir pausa/controles |
| Pausa: `timeScale=0` pero el audio seguía | `AudioListener.pause=true` en pausa; los clics de menú siguen sonando (`ignoreListenerPause` en las voces 2D) |
| Panel de misión pisaba la barra «ENEMIGOS · ESCUADRA» | Más angosto (320 px) |
| Línea «MEDIO · enem. -5%/-10% · alia. …» en pantalla | «Dificultad: MEDIO» |
| Minimapa de 107 px y negro puro | 150 px con fondo verde oscuro; las escenas ya horneadas se suben solas |
| La mira tapaba el texto central del radial | La mira se oculta con el radial abierto (`HudPulido`) |
| Subtexto del roster gris sobre azul, 10 px | 11 px y color claro |
| El panel del tutorial pisaba el minimapa nuevo | Baja a `y=-176` |

| Pausa (título libre, HUD por debajo) | Controles (por encima del panel de misión) |
|---|---|
| ![pausa](../Assets/Validacion/Ronda8/p1_pausa.png) | ![controles](../Assets/Validacion/Ronda8/p1_controles.png) |

| HUD (minimapa 150 px, misión angosta) | Radial (sin mira encima del texto) |
|---|---|
| ![hud](../Assets/Validacion/Ronda8/p1_hud.png) | ![radial](../Assets/Validacion/Ronda8/p1_radial.png) |

**Medido:** con la pausa activa `AudioListener.pause=True` y `timeScale=0`; al reanudar vuelve a `False` y `1`.

### 2. Cajas de suministros (puntos 41 y 42)
Antes las granadas (3) y la vida **no se reponían nunca** en una partida. Ahora hay `CajaDeSuministros` (`Player/CajaDeSuministros.cs`): caja verde con cruz blanca que flota y gira; al caminar hasta ella con el soldado que manejas repone las 3 granadas y cura el 60 % de la vida, con sonido (`HealDone`) y aviso «SUMINISTROS +2 GRANADAS +VIDA». Se vuelve a llenar a los 45 s; si ya estás completo avisa y no se gasta. La misión tiene 3, repartidas entre la salida y la plaza.

**Prueba en Play (input real):** granadas 1→3, vida 111→207/207, `Recogidas=1`, y el sonido `HealDone` en `AudioDirector.Historial`.

| La caja con su baliza en el tutorial | Recogida |
|---|---|
| ![caja](../Assets/Validacion/Ronda8/t_suministros_caja_frente.png) | ![recogida](../Assets/Validacion/Ronda8/t_suministros_recogida.png) |

### 3. Movimiento (punto 50)
**Buffer de salto** en `SoldierMotor`: si apretás Espacio hasta 0,12 s antes de aterrizar, salta apenas tocás el piso (antes se perdía la pulsación). El *coyote time* no aplica: el soldado no se cae de bordes.

### 4. Rendimiento y técnica (puntos 71 parcial, 79)
- `WeaponStatusView` armaba el texto de `[F] CUCHILLO / [G] GRANADA` **en cada frame** (concatenación de strings); ahora solo lo rearma si cambió el estado.
- `Application.targetFrameRate = 144` (antes sin tope).

### 5. Repositorio y suite (puntos 32, 85, 87, 88, 91)
- Eliminadas las escenas copia sin referencias (`SC_Gameplay Copia 2/3`) y los restos de plantilla (`Readme.asset`, `New Terrain.asset`); siguen en el historial de git.
- `0_Plan de mejoras.txt` pasó a `Docs/PLAN_DE_MEJORAS_NOTAS.txt`.
- La suite **vuelve a la escena que tenías abierta** al terminar (antes dejaba `SC_TestLevel`).
- Comentario obsoleto de `KeyBindings` («V = cuchillo») corregido.

### 6. Tutorial actualizado: 36 pasos
Paso nuevo **12 · CAJA DE SUMINISTROS**: al entrar gasta 2 granadas y baja el 40 % de la vida, crea una caja 7 m adelante con baliza, y se completa al recogerla. Los pasos siguientes se renumeraron y `TUTORIAL_PASO_A_PASO.md` se regeneró con las capturas nuevas. Verificado en Play: paso `suministros` → recoger → pasa a `vista_tactica` solo.

## Pruebas
FASE 17 de la suite: caja creada y disponible, lejos no pasa nada, junto a ella granadas al máximo y vida sube, la caja se vacía y cuenta la recogida, el salto en el aire queda en el buffer, minimapa ≥ 140 px, cartel de obstáculos ≤ 30 m y tutorial con ≥ 36 pasos. **Suite completa: OK.**

## Estado de los 100 puntos

**Hechos (20):** 1, 2, 3, 4, 9, 12, 13, 15, 32, 41, 42, 50, 68, 79, 85, 87, 88, 91, más 71 (parcial: se corrigió una fuente de asignaciones, no se remidió el promedio) y 100 (el documento del tutorial se regenera desde su script y quedó al día).

**Ya existían / eran falsos (9):** 7, 16, 17, 22, 29, 46, 47, 48, 67.

**Pendientes, con motivo (71):**
- *Arte y animación* (necesitan modelos, rigs o clips nuevos): 35, 36, 38, 44, 55, 53.
- *Diseño grande* (varias rondas cada uno): 25–28 (resolución, gamepad, localización, accesibilidad), 40 (reservas de munición), 43 (loadout elegible), 57 y 58 (IA con granadas o que las esquive), 61 (órdenes nuevas en el radial), 62, 63, 64.
- *Refactors sin cambio visible*: 82 y 83 (partir `PlayerInputDriver`, `AiBrain` y `HeadlessTestRunner`), 73–78 (los `Find*` revisados están casi todos cacheados con guarda de null; falta un perfil real para decidir), 81 (CI).
- *No verificables sin build o sin oído humano*: 65, 72, 78, 66, 69, 70.
- *Menores no tocados*: 5, 6, 8, 10, 11, 14, 18, 19, 20, 21, 23, 24, 30, 31, 33, 34, 37, 39, 45, 49, 51, 52, 54, 56, 59, 60, 80, 84, 86, 89, 90, 92, 93, 94–99.

No se afirma más de lo hecho: 20 de 100 implementados y probados en esta ronda, 9 corregidos como falsos, 71 siguen abiertos.
