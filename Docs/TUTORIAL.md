# Módulo de tutorial — SC_Tutorial

Escena nueva (`Assets/_Project/Scenes/SC_Tutorial.unity`) con un nivel propio hecho **solo con cubos (blocking) y pintura de terreno**, y un módulo que enseña los controles en 17 pasos con un cuadro de diálogo, banderas booleanas, teclas iluminadas (RawImage), balizas en el mundo, sonido y logs claros. Se abre desde el menú principal (botón **TUTORIAL**).

Capturas: `Assets/Validacion/Tutorial/*.png` (las rutas de abajo son relativas a `Docs/`).
Log de una corrida: `Docs/Tutorial_Log_corrida_final.txt` (también se escribe solo en `Logs/Tutorial_Log.txt` en cada partida).

---

## 1. Cómo está armado

| Archivo | Qué hace |
|---|---|
| `Scripts/Tutorial/TutorialManager.cs` | Máquina de 17 pasos. Cada paso tiene sub-pasos, cada sub-paso una bandera. Solo **mira** lo que hace el jugador (teclas, estado, órdenes, impactos). |
| `Scripts/Tutorial/TutorialFlags.cs` | Todos los booleanos (visibles en el Inspector, `TutorialManager > Flags`). |
| `Scripts/Tutorial/TutorialUI.cs` | Cuadro de diálogo (columna derecha bajo el minimapa): paso, título, barra de avance, instrucción, casillas de sub-pasos, teclas, pista, cartel "PASO N COMPLETADO". El alto se adapta al contenido. |
| `Scripts/Tutorial/KeyCapView.cs` | Cada tecla / botón de mouse es un **RawImage** con textura generada por código: gris = normal, **amarillo = apretada ahora**, **verde = ya usada en el paso**. Suena un tick al apretarla. |
| `Scripts/Tutorial/TutorialBeacon.cs` | Baliza en el mundo (columna translúcida + anillo + etiqueta con sombra) que puede seguir a un objeto. |
| `Scripts/Tutorial/VictoriaTutorial.cs` | Efecto de victoria: fanfarria, destello, fuegos artificiales de cubitos, confeti de interfaz y panel final (REPETIR / VOLVER AL MENÚ). |
| `Scripts/Tutorial/TutorialLog.cs` | Log por paso y sub-paso (consola, `GameLog` y archivo). |
| `Scripts/Editor/TutorialSceneBuilder.cs` | Menú `Strategic Point > Tutorial > Construir escena SC_Tutorial`: copia SC_Gameplay, borra el nivel viejo, pinta un terreno propio (`TerrainData_Tutorial.asset`, el del juego no se toca), arma 31 cubos, coloca escuadra, tanque, enemigo estático y marcadores, hornea NavMesh. Idempotente. |

Cambios mínimos en el juego para poder observarlo: `ObstacleMarker.Golpeado/Derrumbado` (eventos estáticos), 4 sonidos nuevos (`TutKey`, `TutSub`, `TutStep`, `TutVictory`), `GameplaySceneBootstrap.esTutorial` (apaga carteles de misión), botón TUTORIAL en `MainMenuController` / `MenuSceneBuilder`, y **`PlayerInputDriver.SetDestination(Vector3)`** (ver §4).

### Nivel (blocking)
Terreno 70 × 210 m, de sur a norte: **campo de tiro** (pared gris, caja amarilla destructible de 90 HP, enemigo quieto de 100 HP) → **plaza** (2 aliados lejos, zona A y zona B) → **patio del tanque** → **corredor** de 30 m con 3 chicanes y la **meta** (pilares + dintel dorado).

### Banderas y mensajes
Cada sub-paso tiene: texto de casilla, **mensaje** (qué hacer *ahora*), **pista** (aparece a los 16 s sin progreso) y una bandera. El diálogo muestra el mensaje del **primer sub-paso pendiente**:

```csharp
// TutorialManager.cs (paso 9: pedir que te sigan)
S("Presiona Y: ordenas seguirte",
  "Presiona [Y]: los 2 aliados vienen hacia ti y te siguen a donde vayas.",
  "Y = \"síganme\". Funciona sin apuntar a nada.",
  () => f.ordenDeSeguir, v => f.ordenDeSeguir = v),
...
Evaluar = () =>
{
    if (kb.yKey.wasPressedThisFrame) f.ordenDeSeguir = true;
    if (aliados.Count > 0 && Siguiendo() >= aliados.Count) f.aliadosSiguen = true;   // State == Follow
},
```
```csharp
// mensaje = primer sub-paso pendiente
Sub pendiente = null;
foreach (var s in p.Subs) if (!s.Hecha) { pendiente = s; break; }
string mensaje = p.MensajeVivo != null ? p.MensajeVivo() : pendiente != null ? pendiente.Mensaje : "Paso completo";
```

Banderas: `camaraHorizontal, camaraVertical, teclaW/A/S/D, disparoEnemigo, disparoPared, disparoDestruible, apuntoAlAliado, cambioDeSoldado, agachado, levantado, apuntaConZoom, disparaConZoom, rtsActivo, aliadosSeleccionados, ordenDeMoverARts, vueltaAFps, mouseCapturado, ordenDeSeguir, aliadosSiguen, aliadoSeleccionadoEnFps, ordenDeMoverEnFps, cercaDelTanque, dentroDelTanque, ordenDeSubir, aliadosABordo, enLaTorreta, ordenDeAvanzar, disparoCanon, enemigosEliminados, llegoAlFinal, victoria`.

### Feedback de cada acción
Cada sub-paso cumplido: tick de sonido `TutSub` + etiqueta "OK" sobre el soldado + casilla verde con tilde (RawImage) + línea de log. Cada paso cumplido: arpegio `TutStep` + destello verde de pantalla + cartel "PASO N COMPLETADO" + barra de avance. Cada tecla: se ilumina y suena (`TutKey`). Pista amarilla si el jugador se traba.

---

## 2. Los 17 pasos: qué hacer, cómo se detecta y cómo se probó

Cómo se probó (todo en Play, con el Editor enfocado): **teclas reales** inyectadas al Input System (`QueueStateEvent(Keyboard.current, new KeyboardState(Key.W))`), **botones y movimiento de mouse** inyectados (`MouseState`, `QueueDeltaStateEvent(mouse.delta)`), y en la vista táctica el **clic derecho real del sistema operativo** (`SetCursorPos` + `mouse_event`) sobre la zona verde. Para *posicionar* la mira o caminar (no para actuar) se usó la API (`Brain.RotateYaw`, `TurretWeapon.AddDesiredYaw`, `SetDestination`); el gesto que pide el paso (F, Ctrl, clic, T, E, G, 2…) siempre fue real.

### 1 · Movimiento de cámara — `MOVER LA CÁMARA` (tecla: icono de mouse)
- Sub-pasos: mirar a los costados (≥ 50° acumulados de yaw) · mirar arriba/abajo (≥ 18° de pitch).
- Detección: `acumYaw += |DeltaAngle(yawAnterior, yaw)|`, `acumPitch += |pitch - pitchAnterior|` sobre `CameraRig`.
- Pista: "haz clic dentro de la ventana" (el mouse se captura con un clic).
- Capturas: [01 inicio](../Assets/Validacion/Tutorial/01_paso01_inicio.png) · [02 mouse iluminado](../Assets/Validacion/Tutorial/02_paso01_mover_mouse_tecla_iluminada.png) · [03 costados hecho](../Assets/Validacion/Tutorial/03_paso01_subpaso1_costados_hecho.png) · [04 arriba/abajo](../Assets/Validacion/Tutorial/04_paso01_mirando_arriba_abajo.png). (La 05 ya muestra el paso 2: el screenshot tarda ~1 s y el paso terminó antes.)

### 2 · Moverse con W A S D — cuatro **RawImage** que se iluminan
- Cada tecla es un `KeyCapView` (RawImage). Al apretarla: amarillo + salto + tick; queda **verde** al usarse.
- Detección: `kb.wKey.isPressed → f.teclaW = true` (idem A/S/D). El mensaje dice "Faltan: W A S D" y se actualiza.
```csharp
// KeyCapView.Update
bool p = apretada();
if (p && !eraApretada) { salto = 1f; AudioDirector.PlayUi2D(SfxKind.TutKey, 0.35f, 0.5f); }
img.texture = TutorialTextures.Tecla(ancho, alto, p ? Encendida : (Hecha ? Hecha : Normal));
```
- Capturas: [06 inicio](../Assets/Validacion/Tutorial/06_paso02_inicio_wasd.png) · [07 W](../Assets/Validacion/Tutorial/07_paso02_W_iluminada.png) · [08 A (W ya verde)](../Assets/Validacion/Tutorial/08_paso02_A_iluminada_W_verde.png) · [09 S](../Assets/Validacion/Tutorial/09_paso02_S_iluminada.png) · [10 completo, las 4 verdes](../Assets/Validacion/Tutorial/10_paso02_paso_completado.png)

### 3 · Disparar — enemigo estático, pared, destruible
- El enemigo estático tiene `Stance = AltoElFuego` y visión 0,5 m: no se mueve ni dispara; los aliados también están en alto el fuego para no matarlo.
- Sub-pasos: matar al enemigo (`EntityDiedEvent`) · impactar la pared gris (`ObstacleMarker.Golpeado`) · destruir la caja amarilla (`ObstacleMarker.Derrumbado`).
- Balizas roja / gris / amarilla sobre cada blanco; el impacto suelta cubitos rojos (soldado) o amarillos (obstáculo).
- Capturas: [11 balizas](../Assets/Validacion/Tutorial/11_paso03_inicio_balizas_apuntando_enemigo.png) · [12 impacto en soldado](../Assets/Validacion/Tutorial/12_paso03_impacto_soldado_cubitos_rojos.png) · [13 enemigo eliminado](../Assets/Validacion/Tutorial/13_paso03_subpaso1_enemigo_eliminado.png) · [14 pared](../Assets/Validacion/Tutorial/14_paso03_impacto_pared_cubitos_amarillos.png) · [15 caja](../Assets/Validacion/Tutorial/15_paso03_disparando_caja_destruible.png) · [16 (ya paso 4)](../Assets/Validacion/Tutorial/16_paso03_paso_completado_banner.png)

### 4 · Cambiar a 1 soldado alejado — tecla F
- Dos aliados a ~45 m con columna celeste. Sub-pasos: apuntarles (`Aim.Evaluate → Ally`) · `driver.Brain.Current != soldadoInicial`.
- Capturas: [17 apuntando al aliado](../Assets/Validacion/Tutorial/17_paso04_apuntando_al_aliado_lejano.png) · [18 F toma el control](../Assets/Validacion/Tutorial/18_paso04_F_toma_el_control.png)

### 5 · Agacharse — Ctrl
- Sub-pasos: mantener (`Motor.IsCrouching`) · soltar (`agachado && !IsCrouching`).
- Capturas: [19](../Assets/Validacion/Tutorial/19_paso05_inicio_agacharse.png) · [20 Ctrl iluminada](../Assets/Validacion/Tutorial/20_paso05_ctrl_agachado_tecla_iluminada.png) · [21](../Assets/Validacion/Tutorial/21_paso05_paso_completado.png)

### 6 · Mira con clic derecho
- Sub-pasos: mantener RMB (`Rig.EstaConZoom`) · disparar con zoom (`ShotFiredEvent` del jugador con zoom activo).
- El icono de mouse ilumina el botón derecho / izquierdo.
- Capturas: [22 zoom](../Assets/Validacion/Tutorial/22_paso06_clic_derecho_zoom.png) · [23 disparo con zoom](../Assets/Validacion/Tutorial/23_paso06_disparo_con_zoom.png) · [24](../Assets/Validacion/Tutorial/24_paso06_paso_completado.png)

### 7 · Cambio a RTS y orden de moverse a los 2
- Sub-pasos: TAB (`Rig.Mode == Rts`) · seleccionar a los 2 aliados (arrastrar recuadro o Ctrl+A; contador 0/2) · clic derecho en el círculo verde (`MoveOrderIssuedEvent` a ambos con destino a ≤ 10 m de la zona A).
- Al entrar en RTS el tutorial aleja y centra la cámara sobre la escuadra (`Rig.Zoom(-60)`, `Rig.RecenterOn`) y pone etiquetas a los aliados.
- Probado con Ctrl+A real y **clic derecho real del sistema operativo** sobre la zona A.
- Capturas: [25](../Assets/Validacion/Tutorial/25_paso07_inicio_rts_pendiente.png) · [26 vista táctica](../Assets/Validacion/Tutorial/26_paso07_tab_vista_tactica_alejada.png) · [27 Ctrl+A](../Assets/Validacion/Tutorial/27_paso07_ctrl_A_seleccion_2_aliados.png) · [28 cursor sobre zona A](../Assets/Validacion/Tutorial/28_paso07_cursor_sobre_zonaA.png) · [29 aliados van](../Assets/Validacion/Tutorial/29_paso07_clic_derecho_aliados_van.png)

### 8 · Volver a FPS
- Sub-pasos: TAB (`Mode == Fps`) · **clic izquierdo** para recapturar el mouse (`Cursor.lockState == Locked`). Se agregó este sub-paso porque tras volver de RTS el mouse no gira hasta hacer un clic.
- Capturas: [30](../Assets/Validacion/Tutorial/30_paso08_inicio_volver_fps.png) · [31 falta capturar el mouse](../Assets/Validacion/Tutorial/31_paso08_tab_fps_falta_capturar_mouse.png) · [32 completo](../Assets/Validacion/Tutorial/32_paso08_clic_mouse_capturado_paso_completo.png)

### 9 · Decir que me sigan — Y
- Sub-pasos: Y (`yKey.wasPressedThisFrame`) · los 2 en `AiState.Follow` (contador 0/2).
- Capturas: [33](../Assets/Validacion/Tutorial/33_paso09_inicio_seguir.png) · [34 vienen](../Assets/Validacion/Tutorial/34_paso09_Y_aliados_vienen_a_seguirte.png) · [35 los 2 siguen](../Assets/Validacion/Tutorial/35_paso09_los_2_siguen_paso_completo.png)

### 10 · Seleccionar en FPS — Shift + clic derecho sobre un aliado
- Detección: `Selection.Selected` contiene un aliado. (Se limpia la selección al empezar el paso.)
- Capturas: [36 apuntando](../Assets/Validacion/Tutorial/36_paso10_apuntando_a_un_aliado.png) · [37 Shift+RMB](../Assets/Validacion/Tutorial/37_paso10_shift_clic_derecho_selecciona.png)

### 11 · Dar orden de moverse en FPS — clic derecho en el suelo
- Detección: `MoveOrderIssuedEvent` del aliado seleccionado estando en FPS.
- Capturas: [38](../Assets/Validacion/Tutorial/38_paso11_apuntando_al_suelo_zonaB.png) · [39](../Assets/Validacion/Tutorial/39_paso11_orden_aliado_va.png)

### 12 · Entrar en el tanque — E
- Sub-pasos: acercarse (≤ 3,3 m, dentro del radio de interacción de 3,5 m; muestra los metros) · E (`Vehicle.PlayerAboard`). Baliza naranja sobre el tanque.
- Capturas: [40](../Assets/Validacion/Tutorial/40_paso12_inicio_tanque_columna_naranja.png) · [41 cerca](../Assets/Validacion/Tutorial/41_paso12_subpaso1_cerca_del_tanque.png) · [42 dentro + panel de teclas](../Assets/Validacion/Tutorial/42_paso12_E_dentro_del_tanque_panel_de_teclas.png)

### 13 · Decir que entren al tanque — G (U = uno)
- Sub-pasos: orden (`MountTargetVehicle == tanque` o ya sentado) · los 2 a bordo (`RoleOf(aliado) != null`, contador 0/2).
- Capturas: [43](../Assets/Validacion/Tutorial/43_paso13_inicio_en_tanque.png) · [44 corren](../Assets/Validacion/Tutorial/44_paso13_G_aliados_corren_al_tanque.png) · [45 a bordo](../Assets/Validacion/Tutorial/45_paso13_aliados_a_bordo_paso_completado.png)

### 14 · Cambiar a torreta — tecla 2 (intercambia si el puesto está ocupado)
- Detección: `driver.CurrentSeat == Gunner`.
- Capturas: [46](../Assets/Validacion/Tutorial/46_paso14_inicio_torreta.png) · [47 cañón](../Assets/Validacion/Tutorial/47_paso14_2_canon_intercambia_con_aliado.png)

### 15 · Ordenar avance y disparar entre enemigos que se acercan — T + clic izquierdo
- Al entrar aparece la **ola A** (4 soldados) que avanza hacia el tanque. Sub-pasos: T mirando al suelo (el tanque se mueve > 6 m) · **disparar el cañón** (se detecta por el enfriamiento `TurretWeapon.CooldownFraction01`, porque el cañón no publica `ShotFiredEvent`) · derribar 3 enemigos (`EntityDiedEvent`, contador 0/3). Si mueren antes de tiempo aparecen más.
- Capturas: [70 inicio, 3 sub-pasos](../Assets/Validacion/Tutorial/70_paso15_inicio_tres_subpasos_enemigos_vienen.png) · [71 T avanza](../Assets/Validacion/Tutorial/71_paso15_T_el_tanque_avanza.png) · [72 disparo del cañón](../Assets/Validacion/Tutorial/72_paso15_disparo_del_canon.png) · [73 completado](../Assets/Validacion/Tutorial/73_paso15_paso_completado.png)

### 16 · Avanzar y llegar al final
- Baliza dorada en la meta; contador de metros. Al pasar `meta.z - 34` aparece la **ola B** (3 refuerzos, opcionales). Se cumple a ≤ 9 m de la meta.
- Capturas: [74](../Assets/Validacion/Tutorial/74_paso16_inicio_meta_dorada.png) · [75 corredor](../Assets/Validacion/Tutorial/75_paso16_tanque_avanza_por_el_corredor.png) · [76 llegó](../Assets/Validacion/Tutorial/76_paso16_llego_a_la_meta_paso_completado.png)

### 17 · Efecto de victoria y fin
- Fanfarria (`TutVictory`), destello dorado, cartel, 7 ráfagas de fuegos artificiales de cubitos de colores, 110 piezas de confeti, y panel con resumen (pasos, tiempo, derribos) y botones REPETIR TUTORIAL / VOLVER AL MENÚ. `Terminado = true`.
- Capturas: [77 efecto](../Assets/Validacion/Tutorial/77_paso17_victoria_fuegos_confeti.png) · [78 panel final](../Assets/Validacion/Tutorial/78_paso17_panel_final_botones.png)

### Menú principal
Botón **TUTORIAL** (abre `SC_Tutorial`). Probado con un clic real del sistema operativo: [00](../Assets/Validacion/Tutorial/00_menu_principal_boton_tutorial.png) · [00b](../Assets/Validacion/Tutorial/00b_menu_cursor_sobre_tutorial.png).

---

## 3. Logs

Formato: `[TUTORIAL] <segundos> [PASO nn/17 TÍTULO] <mensaje>`. Tipos: `INICIO` del paso (con sus sub-pasos `PENDIENTE`), `sub-paso k/n CUMPLIDO`, `PISTA`, `COMPLETADO en X s`, y `[EVENTO]` (olas, derribos), `[VICTORIA]`, `[FIN]`.

```
[TUTORIAL] 000,0s [PASO 01/17 MOVER LA CÁMARA] INICIO · 2 sub-pasos · teclas: MOUSE
[TUTORIAL] 000,0s [PASO 01/17 MOVER LA CÁMARA]    sub-paso 1/2 PENDIENTE: Mira a los costados (izquierda y derecha)
[TUTORIAL] 478,1s [PASO 07/17 RTS: ORDEN A LOS 2]    sub-paso 2/3 CUMPLIDO: Selecciona a los 2 aliados (2/2)
[TUTORIAL] 595,7s [PASO 07/17 RTS: ORDEN A LOS 2]    sub-paso 3/3 CUMPLIDO: Clic derecho en el círculo verde (2/2)
[TUTORIAL] 595,7s [PASO 07/17 RTS: ORDEN A LOS 2] COMPLETADO en 130,9 s
[TUTORIAL] 200,6s [PASO 15/17 AVANZAR Y DISPARAR] COMPLETADO en 46,0 s
[TUTORIAL] 317,1s [VICTORIA] EFECTO DE VICTORIA: fanfarria + fuegos artificiales + confeti + panel final
[TUTORIAL] 318,9s [FIN] Tutorial finalizado en 270,1 s · 5 bajas
```
Log completo de la última corrida: `Docs/Tutorial_Log_corrida_final.txt`.

---

## 4. `SetDestination(Vector3)` — mover al jugador sin cursor

Pedido durante las pruebas: depender del cursor y de la cámara para desplazarse era frágil. `PlayerInputDriver` ahora expone:

```csharp
public void SetDestination(Vector3 punto);   // a pie: camina solo hasta el punto; en vehículo: manda el vehículo (con un aliado al volante si hace falta)
public void CancelDestination();
public bool TieneDestino { get; }
```
Sirve por mensaje: `driver.SendMessage("SetDestination", new Vector3(0, 0, 103));`. WASD lo interrumpe. Con esto el jugador caminó ~100 m hasta el tanque y el tanque recorrió el corredor hasta la meta.

---

## 5. Transparencia: qué se probó exactamente y qué no

- **Corrida real completa de los pasos 1–11** (teclas/mouse reales; la mira y el caminar posicionados por API) en una sesión, y **corrida de los pasos 12–17** en otra: en esa segunda sesión los pasos 1–11 se dieron por cumplidos con `TutorialManager.SaltarPaso()` (ver las líneas `[SALTO]` de `Tutorial_Log_corrida_final.txt`) para llegar al tanque; los pasos 12–17 fueron reales (E, G, 2, T, clic izquierdo con el cañón, `SetDestination` para la marcha). **No hubo una única corrida ininterrumpida de 17 pasos sin ayudas.**
- Las capturas de los pasos 1–14 son de la primera sesión; 70–78 de la segunda. Las capturas 05 y 16 muestran ya el paso siguiente (tiempos de captura).
- Los enemigos de la ola A mueren por la metralleta del aliado a bordo casi siempre antes de que el jugador dispare: por eso el paso exige un disparo del cañón **y** 3 derribos de cualquiera.
- La captura del cartel "PASO N COMPLETADO" en su momento exacto no quedó bien (el cartel dura ~2 s y la captura tarda ~1 s); se comprobó por API (`Ui.TextoBanner`) y el cartel fue reubicado a la izquierda para no tapar el diálogo.
- Suite headless completa: **TODAS LAS FASES COMPLETADAS CON ÉXITO** después de los cambios.
- Bugs hallados y corregidos durante la prueba: el texto de sub-pasos con contadores se armaba antes de inicializar (0/0); umbral de "cerca del tanque" (4,5 m) mayor que el radio de interacción real (3,5 m) → ahora 3,3 m; balizas con `hideFlags` DontSave sobrevivían al cambio de escena (aparecían en el menú) → sin hideFlags y limpieza al destruir el manager; un aliado mataba al enemigo estático → alto el fuego; título largo que se partía en dos líneas; panel tapando el HUD del tanque → alto dinámico.
