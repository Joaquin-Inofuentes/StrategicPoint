# Ronda 14 — Plan de implementación y pruebas (ejecutor: Gemini 3.8 Flash, esfuerzo alto)

> Versión 2. Reemplaza al plan v1 de 31 fases. Cada tarea está anclada en el código real
> (archivo:línea verificados el 2026-09-26 sobre `claude/modest-wozniak-018bas`).
> Índice del proyecto: `Docs/INDICE_IA/00_MAPA.md`. Proyecto: **Unity 6 (6000.5.6f1), URP, build Windows**
> — no es un proyecto 2022.3 WebGL; no cambiar de versión ni de plataforma.

---

## 0. Qué se corrigió respecto del plan v1

| Problema del v1 | Realidad en el código | Consecuencia en el v2 |
|---|---|---|
| Inventaba teclas nuevas para C, Espacio, F1–F3 | `C` = `VerTactico` (`KeyBindings.cs:82`), `Espacio` = `Frenar` y `Recentrar` (`:91`, `:94`), F1–F3 ya poseen en FPS y seleccionan en RTS (`PlayerInputDriver.cs:798`, `PlayerInputDriver.Escuadra.cs:69`) | Se extienden las acciones existentes; se listan los conflictos en §3 |
| "Buscar X en 01_archivos.md" en cada fase | Los archivos existen y tienen nombre propio | Cada tarea nombra archivo y línea |
| Proponía un AudioMixer con buses | `AudioDucking.cs` dice explícitamente que **no hay AudioMixer**; el volumen vive en `AudioDirector` (`SfxChannel`) | T-24 trabaja sobre los canales de `AudioDirector` |
| Sistemas "nuevos" de rombos, cursor, líneas y minimapa | Ya existen `DiamondGizmo`, `UnitLocatorCylinder`, `InteractGearMarker`, `RomboVisibilidad`, `CursorContextual`, `AttackLineManager`, `MinimapIcon`, `WorldUiDirector` | Todo se extiende, nada se duplica |
| Capturas "a mano" sin herramienta | Existe `Editor/AntesProbe.cs` (`Iniciar/Marca/Foto/Cerrar` → `Logs/Antes/<corrida>/log.jsonl` + `fotos/`) | Protocolo único de evidencia (§2.4) |
| Los canvases se separaban al final (fase 18) | Es la causa raíz de dos bugs (Esc en cinemática y anillo de recarga) | Pasa a la Ola 1 |
| Fase 9 vacía, fases sin dependencias explícitas | — | 41 tareas en 9 olas, con dependencias y matriz de trazabilidad (§6) |
| Sin tareas humanas para assets | Faltan malla de camioneta, moto y 3 animaciones | Tareas H-1..H-4 con plan B de blockout |

---

## 1. Cómo usar este plan con Gemini 3.8 Flash

Un modelo "Flash" rinde mejor con tareas **atómicas, cerradas y verificables**. Reglas de uso:

1. **Una tarea por sesión** (o por turno largo). Nunca pegar una ola entera.
2. **Paquete de contexto mínimo**: el prompt común (§2.1) + el bloque de la tarea + los archivos que la
   tarea nombra. No pegar `02_simbolos.md` entero (3.244 líneas); si hace falta un símbolo, hacer grep.
3. **Ninguna decisión de diseño abierta queda para el modelo**: las que existían están resueltas en §3.
   Si aparece otra, el modelo **se detiene y pregunta** (regla STOP).
4. **Dos roles en sesiones distintas**: Implementador y Verificador (§2.2). El Verificador no ve el
   razonamiento del Implementador, solo el diff, el log y las fotos.
5. **Commit por tarea**, mensaje `R14 T-xx: <título>`. Si la suite falla, no se pasa a la siguiente.

Nota: no hay una ficha técnica pública de "Gemini 3.8 Flash" que el plan pueda asumir; está escrito
para cualquier modelo rápido con ventana amplia pero razonamiento corto. Con un modelo más fuerte
se pueden agrupar tareas de la misma ola.

---

## 2. Roles, prompts y protocolo

### 2.1 Prompt común (pegar al inicio de CADA sesión)

```
ROL: Implementador de gameplay en Unity 6 (6000.5.6f1, URP) sobre el repo Strategic Point.

INVARIANTES (romper una = tarea rechazada):
1. Toda lógica de simulación (IA, armas, vida, vehículos, torretas) avanza SOLO desde
   SP.Ai.WorldSimulationDriver.Step(dt). No agregues Update() con lógica de juego.
   Presentation y UI sí pueden usar Update() para dibujar.
2. PlayerInputDriver traduce input, no decide. Toda tecla nueva va como constante en
   Player/KeyBindings.cs (con default) y como fila en UI/ControlsTable.cs y UI/KeyRebindView.cs.
   Leer Keyboard.current.xKey directo para una acción de juego es un bug (bug 76).
3. Eventos: structs inmutables en Core/Events.cs, solo ids/posiciones/valores. Guardar el
   IDisposable de Subscribe y liberarlo en OnDisable/OnDestroy.
4. SC_TestLevel, SC_MainMenu, SC_Tutorial y parte de SC_Gameplay se generan por código
   (Editor/*Builder.cs). Un cambio de escena permanente va en el builder, nunca a mano.
5. Cero Instantiate en combate: usar los pools existentes (ProjectilePool, ImpactFxPool,
   DebrisPool, DecalPool, ObjectPool<T>).
6. Todo texto visible pasa por Loc (Core/Loc.cs) y su traducción va en Core/LocTextos.cs.
7. Estado estático nuevo: registrarlo en Core/ReinicioDeEstaticos.cs.
8. No toques archivos fuera de la lista de la tarea salvo que sea imprescindible; si lo es,
   decí cuál y por qué en el informe.

REGLA STOP: si el código real no coincide con lo que dice la tarea (línea movida, clase
renombrada, comportamiento distinto), o si hace falta una decisión de diseño no escrita,
DETENTE y reportá qué encontraste. No improvises.

ENTREGA OBLIGATORIA (en este orden):
A. Resumen de 3-5 líneas de lo que cambiaste.
B. Lista de archivos tocados con el motivo.
C. Salida de: python Tools/Ci/verificar_estatico.py
D. Resultado de la suite: Unity -batchmode -nographics -projectPath . -executeMethod
   SP.EditorTools.HeadlessTestRunner.RunAll -logFile - (líneas de tus Check nuevos + OK/FALLO final).
E. Ruta de Logs/Antes/R14_<T-xx>/ con log.jsonl y fotos/ (ver protocolo de evidencia).
F. Riesgos o dudas que quedaron.
```

### 2.2 Roles

| Rol | Quién | Qué hace | Qué NO hace |
|---|---|---|---|
| **Orquestador** | Vos (o un modelo más fuerte) | Elige la próxima tarea, pega prompt común + bloque, decide ante un STOP | Escribir código |
| **Implementador** | Gemini 3.8 Flash, sesión A | Ejecuta el bloque, entrega A–F | Decidir diseño, tocar otras tareas |
| **Verificador** | Gemini 3.8 Flash, sesión B (limpia) | Revisa diff + log + fotos contra "Aceptación" | Arreglar: solo aprueba o rechaza con motivo |
| **Humano** | Vos | Tareas H-x (assets), mirar fotos, jugar 2 min por ola | — |

**Prompt del Verificador:**
```
ROL: Verificador. Te paso: (1) el bloque de la tarea T-xx, (2) el diff, (3) el log de la suite,
(4) Logs/Antes/R14_T-xx/log.jsonl y las fotos.
Para CADA criterio de "Aceptación" respondé: CUMPLE / NO CUMPLE / NO VERIFICABLE, citando la
línea del diff, la marca del log o el nombre de la foto que lo prueba.
Además chequeá: ¿se violó algún invariante del prompt común? ¿hay Update() con lógica de juego?
¿tecla leída fuera de KeyBindings? ¿Subscribe sin Dispose? ¿texto sin Loc? ¿Instantiate en combate?
Veredicto final: APROBADA o RECHAZADA (con la lista de lo que falta). No propongas código.
```

### 2.3 Topics (subsistemas) y su contexto mínimo

| Topic | Archivos base a adjuntar al prompt |
|---|---|
| **UI-Canvas** | `UI/AimUI.cs`, `Presentation/PauseController.cs`, `Presentation/GameOutcomeController.cs`, `Mision/CinematicaDeIntro.cs`, `UI/HudPulido.cs` |
| **Input-Cámara** | `Player/KeyBindings.cs`, `UI/ControlsTable.cs`, `Player/PlayerInputDriver.cs` (solo `UpdateRts` ~2852 y regiones citadas), `Camera/CameraRig.cs` |
| **Marcadores-Minimapa** | `Presentation/DiamondGizmo.cs`, `UnitLocatorCylinder.cs`, `InteractGearMarker.cs`, `RomboVisibilidad.cs`, `MinimapIcon.cs`, `WorldUiDirector.cs`, `UI/MinimapFollow.cs` |
| **Escuadra-IA** | `Ai/AiBrain*.cs` (la parte citada), `Player/OrderService.cs`, `Player/OrdenesDeEscuadra.cs`, `Core/Coberturas.cs`, `Player/Reanimacion.cs` |
| **Misión-Civil** | `Mision/MisionDirector.cs`, `Mision/MisionHud.cs`, `Player/Rehen.cs`, `Presentation/AccionesEnCursoView.cs` |
| **Combate-Feel** | `Combat/Projectile.cs`, `Combat/WeaponCatalog.cs`, `Combat/Health.cs`, `Combat/Granada.cs`, `Presentation/CubeFxReactor.cs`, `Presentation/SoldierAnimatorDriver.cs` |
| **Ajustes** | `UI/AjustesDeJuego.cs`, `UI/LayoutDeAjustes.cs`, `Presentation/PauseController.cs`, `Core/Loc.cs`, `Presentation/AudioDirector.cs`, `Presentation/Subtitulos.cs` |

### 2.4 Protocolo de evidencia (logs + capturas)

Todas las tareas usan **la misma sonda** que ya existe, `Editor/AntesProbe.cs`:

```csharp
AntesProbe.Iniciar("R14_T-xx");      // crea Logs/Antes/R14_T-xx/{log.jsonl, fotos/}
AntesProbe.Marca("antes", ...);      // estado medido ANTES de la acción
AntesProbe.Foto("antes_<qué>");      // captura numerada
// ... ejecutar el caso (con la API del juego, no con clics a mano) ...
AntesProbe.Marca("despues", ...);
AntesProbe.Foto("despues_<qué>");
AntesProbe.Cerrar();
```

- Se captura el **antes** en la rama sin el cambio (o con el cambio desactivado) y el **después** con él.
- Cada foto lleva el nombre que pide la tarea. Las 7 resoluciones solo en tareas de layout (marcadas **[7R]**),
  usando la etapa de capturas de `Tools/Qa/qa_total.bat`.
- Las pruebas automatizadas nuevas van en **`Editor/HeadlessTestRunner.Fase23.cs`** (Ronda 14), una
  función `Fase23_<Nombre>()` por tarea, con el mismo estilo de `Check("texto", condición)` de
  `HeadlessTestRunner.Fase21.cs`, y se registran en `RunAll`.
- El sonido no se verifica por foto: se verifica con `Editor/AudioAnalisisReport.cs` (métricas) y una
  marca en el log con el nombre del clip.
- Al terminar una ola: `python Tools/Indice/indexar_ia.py` y `--check` (el índice no debe quedar viejo).

---

## 3. Decisiones de diseño ya tomadas (para que Flash no decida)

| # | Tema | Decisión |
|---|---|---|
| D1 | **`C` mantenido** | Sigue siendo `VerTactico` (coberturas + rutas de patrulla). Se le **agregan** líneas a enemigos visibles/que atacaron en los últimos 6 s (rojas) y a aliados vivos (azules). |
| D2 | **`C` + clic izquierdo** | Mientras `VerTactico` está apretado, el clic izquierdo **no dispara**: emite orden "cubrirse en el punto apuntado". Destinatario: el seleccionado; si está ocupado con una orden de movimiento, el aliado **libre** más cercano al punto. |
| D3 | **Espacio en RTS** | Ya centra en el centroide de la escuadra viva (`PlayerInputDriver.cs:3187`) y cede ante un recorrido trazado. Se mantiene; T-17 solo verifica y cubre con test. |
| D4 | **`F` en RTS** | Nueva acción `FocalizarRts` (default `F`). No choca con el cuchillo: `UpdateRts` y `UpdateFps` son ramas excluyentes. |
| D5 | **Tecla `|`** | En teclado latinoamericano es la tecla física a la izquierda del `1` = `Key.Backquote` en Input System. Nueva acción `RotarCamaraRts` (default `Backquote`). |
| D6 | **Espacio para desatar al civil** | Dentro del radio de rescate y con el civil sin liberar, Espacio **no salta**: cuenta un nudo. Fuera de ese caso, Espacio vuelve a ser salto. |
| D7 | **HUD mínimo / daltonismo** | Se quitan del menú de ajustes. `F10` (HUD mínimo) **se conserva** como atajo. La paleta de daltonismo se desactiva (default false) y su código queda muerto → se borra. |
| D8 | **Volumen** | Sin AudioMixer: volumen efectivo de cada canal = `Master × Canal`. Se guarda en `PlayerPrefs`. |
| D9 | **Calma** | "Calma" = nadie del bando del jugador recibió ni hizo daño en los últimos N s, con la misma definición que ya usa `Fase21_MedicoEnCalma`. Regeneración: `Health.SegundosSinDanoParaRegenerar` pasa de 3 s a **9 s** (×3). |
| D10 | **Headshot** | No existe detección por zona. Regla: impacto de proyectil cuyo punto está por encima de `soldado.y + 1.45 m` de pie (`+0.95 m` agachado) = headshot, ×2 de daño. Constantes en un solo lugar. |
| D11 | **Lanzacohetes vs tanque** | Nuevo campo `MultiplicadorVsVehiculo` en `WeaponCatalog.Spec`; cohete = 2.5 → 95×2.5 ≈ 237 sobre 260 de vida: **2 impactos directos** destruyen un tanque. |
| D12 | **Cadáveres** | Quedan en el mundo; tope de **24** cadáveres visibles (el más viejo se oculta) para no romper el benchmark de estrés con 50+ unidades. |
| D13 | **Colores de estado** (una sola paleta en `DiamondGizmo`) | Tanque: vacío = gris, aliado = azul, enemigo = rojo, mixto (ambos bandos) = violeta. Torreta = naranja, botiquín = verde, munición = azul claro. |

---

## 4. Tareas humanas (antes de las olas que las necesitan)

| ID | Qué | Formato/destino | Plan B si no está a tiempo |
|---|---|---|---|
| **H-1** | Animación de civil **arrodillado/atado** (Mixamo, buscar "kneeling", "hostage" o "tied"), sin root motion | FBX en `Assets/ARTS/SP_Arte/Anim/`, rig Humanoid | Usar `idle crouching.fbx` (ya existe) |
| **H-2** | Animaciones **melee (culatazo/cuchillo)** y **lanzar granada** de pie, sin root motion | Igual que H-1 | Solo la capa de torso con `reloading.fbx` (ya existe) y el tajo actual de `CuchilloFx` |
| **H-3** | Malla de **camioneta** (pickup) y **moto** low-poly, pivote en el piso, forward = +Z | FBX en `Assets/ARTS/SP_Arte/_FBX_Export/` para que `WorldArtPipeline` la tome | Blockout con cubos (patrón `LevelBlockoutBuilder`) |
| **H-4** | Textura/variante de **ropa civil** | Material para `SoldierLook.MaterialCivil()` | Cambiar solo el tinte base |

Mirar las fotos de cada ola y jugar 2 minutos también es tarea humana (el Verificador no juzga "se siente bien").

---

## 5. Olas y tareas

Orden: primero lo que destraba a otros (canvases, colliders), después UI barata, después sistemas,
y al final la feature grande (camioneta). Dentro de una ola las tareas son independientes salvo que se diga.

### OLA 0 — Línea de base

#### T-00 · Baseline y arnés de pruebas
- **Pasos:** 1) Crear `Editor/HeadlessTestRunner.Fase23.cs` vacío con `RunPhase23()` registrado en `RunAll`.
  2) Correr la suite completa → guardar salida en `Logs/R14_baseline_suite.txt`.
  3) `AntesProbe.Iniciar("R14_T-00")` y capturar: cinemática (toma 1 y 3), HUD FPS en combate, RTS con escuadra,
  radial abierto, ajustes (las 3 pestañas), minimapa, derrota, victoria, árboles a 150 m.
- **Aceptación:** suite `OK` con 647+ checks; 13 fotos `antes_*`; `RunPhase23` corre (aunque vacía).

---

### OLA 1 — Bugs con causa raíz y fundamentos

#### T-01 · Separar canvases y arreglar el anillo de recarga trabado **[7R]**
- **Pedido:** "Al recargar y cambiar a RTS el círculo radial de carga se rompe… 2 canvas distintos FPS/RTS, otro para menú, otro victoria/derrota, otro feedback temporal."
- **Estado real:** `AimUI.SetVisible(false)` (`UI/AimUI.cs:253-262`) oculta mira, prompt y paneles, pero **no** `circuloRecarga` ni `circuloVidaEnemigo`. `UpdateReloadCircle` (`:195`) solo se llama desde FPS → en RTS el anillo queda congelado con el último valor.
- **Pasos:**
  1. Fix mínimo primero (commit propio): en `SetVisible(false)` llamar `circuloRecarga?.SetVisible(false)` y `circuloVidaEnemigo?.SetVisible(false)`. Al volver a FPS, `UpdateReloadCircle` lo reenciende solo si `weapon.IsReloading`.
  2. Crear `UI/CapasDeHud.cs` (servicio único, patrón `PlayerInputDriver.Registro.cs`) con 5 raíces: `Hud_FPS`, `Hud_RTS`, `Hud_Menu`, `Hud_Resultado`, `Hud_Feedback`, y `static void ActivarModo(ControlMode)` que hace `SetActive` explícito de FPS/RTS.
  3. Reparentar los paneles existentes a la raíz que corresponde **en el builder** de la escena (no a mano). `Hud_Menu` (pausa/ajustes) y `Hud_Resultado` nunca los toca un cambio de modo.
  4. Llamar `CapasDeHud.ActivarModo` desde `CameraRig.SetMode` (`Camera/CameraRig.cs:371`) o desde donde `PlayerInputDriver` alterna Tab.
- **Aceptación:** recargar → Tab a RTS → el anillo no se ve; Tab a FPS a mitad de recarga → el anillo sigue avanzando; Tab después de terminar → no hay anillo. Con Tab 20 veces seguidas no quedan elementos de FPS visibles en RTS ni al revés.
- **Test:** `Fase23_AnilloRecargaEnRts` (simular recarga, `SetMode(Rts)`, check `!circuloRecarga.activeInHierarchy`), `Fase23_CapasDeHud` (cada raíz existe y un cambio de modo no toca `Hud_Menu`).
- **Evidencia:** `antes_anillo_trabado_rts`, `despues_rts_sin_anillo`, `despues_fps_anillo_continua`.
- **Desbloquea:** T-02, T-10, T-26.

#### T-02 · Esc durante la cinemática abre el menú
- **Pedido:** "En la cinemática si apreto Esc no me abre el menú, solo me lo pausa."
- **Estado real:** `CinematicaDeIntro.Rutina` apaga **todos** los canvas screen-space (`Mision/CinematicaDeIntro.cs:122-123`), incluido el del menú; `PauseController.Update` (`Presentation/PauseController.cs:283`) sí pausa, pero su panel queda invisible.
- **Pasos:** 1) Apagar solo `Hud_FPS`, `Hud_RTS` y `Hud_Feedback` (de T-01), nunca `Hud_Menu`. 2) Igual en `Saltar()` (`:47`) y `EsperarTeclaYCerrar` (`:241`) al reencender. 3) Con la pausa abierta, la corrutina de la cinemática se congela (ya usa `Time.deltaTime`, que es 0 con `timeScale=0`: verificar). 4) En `EsperarTeclaYCerrar` (`:223`) excluir `Esc` de `anyKey` para que Esc no arranque la partida.
- **Aceptación:** Esc en cualquier toma muestra el panel de pausa; "Continuar" retoma la cinemática en la misma toma; Esc en el cartel final abre el menú y no inicia.
- **Test:** `Fase23_EscEnCinematica`: iniciar cinemática, `PauseController.ShowPause()`, check panel activo y `WaypointActual` sin cambiar tras simular 2 s.
- **Evidencia:** `antes_esc_cinematica_sin_menu`, `despues_esc_cinematica_menu`.
- **Depende de:** T-01.

#### T-03 · Auditoría de colliders invisibles + colliders de cobertura faltantes
- **Pedido:** "Hay colliders invisibles, especialmente 1 a la derecha de la torreta… revisa solo los que no tienen mesh." + "Faltan colliders para objetos de cobertura."
- **Pasos:**
  1. Herramienta `Editor/AuditoriaDeColliders.cs` (menú `Strategic Point > Auditar colliders sin malla`): recorre SC_Gameplay y lista todo `Collider` no-trigger sin `MeshRenderer`/`SkinnedMeshRenderer` en sí ni en hijos, **excluyendo** los marcados `HitboxDeImpacto` (`Vehicles/HitboxDeImpacto.cs`) y los de soldados/vehículos. Salida: `Docs/RONDA_14_COLLIDERS.csv` (escena, ruta jerárquica, centro, tamaño, builder que lo crea si se puede inferir por nombre).
  2. Segundo listado: objetos con `ObstacleMarker` o registrados en `Coberturas` cuya malla **no** tiene collider.
  3. **STOP humano:** revisar el CSV (el caso reportado es el de la derecha de `TorretaFija`). Marcar "borrar" / "dejar".
  4. Aplicar en el builder que lo crea (`LevelBlockoutBuilder`, `BlockoutArtDresser`, `ArtBuilder`), no en la escena.
- **Aceptación:** 0 colliders sin malla no justificados; toda cobertura registrada tiene collider; la suite sigue `OK` (el navmesh/`NavService.BlocksMovement` no cambia de forma inesperada).
- **Test:** `Fase23_ColliderSinMalla` (el conteo coincide con la lista blanca), `Fase23_CoberturasConCollider`.
- **Evidencia:** fotos en Scene view con gizmos de colliders: `antes_collider_torreta`, `despues_collider_torreta`; el CSV antes/después.

#### T-04 · Punto base del soldado a Y=0
- **Pedido:** "El soldado: su punto base no es realmente 0, está un poquito elevado."
- **Pistas:** `MisionDirector.SpawnCivilOculto` instancia en `y = 0.8f` (`Mision/MisionDirector.cs:446`); `Core/ApoyoEnElPiso.cs` ya corrige flotantes/enterrados. Comparar pivote del prefab (`SoldierPrefabPipeline`), offset del collider y `baseOffset` del NavMeshAgent si lo hubiera.
- **Pasos:** 1) Medir en SC_TestLevel: `transform.y`, `collider.bounds.min.y` y `SkinnedMeshRenderer.bounds.min.y` de cada soldado quieto → `Marca`. 2) Corregir la fuente (prefab en `SoldierPrefabPipeline` o spawns con Y hardcodeada), no con un offset visual.
- **Aceptación:** |bounds.min.y de la malla − suelo| < 0.03 m para soldados de pie, agachados y el civil.
- **Test:** `Fase23_PiesEnElPiso`.
- **Evidencia:** vista lateral `antes_pivote`, `despues_pivote` con una grilla de referencia.

#### T-05 · Razón de la derrota visible y persistente
- **Pedido:** "El cartel de la razón se pierde… Perdiste a toda tu escuadra o murió el civil."
- **Archivos:** `Presentation/GameOutcomeController.cs`, `Mision/EstadoDePartida.cs` (decide la derrota), `UI/Diagramador.cs`.
- **Pasos:** exponer la causa como enum (`EscuadraCaida`, `CivilMuerto`, …) desde `EstadoDePartida`; `GameOutcomeController` la muestra en un texto propio, estático, debajo del título, con `FondoOpaco`, en `Hud_Resultado`.
- **Aceptación:** el texto sigue visible 30 s después; es el correcto en los dos casos; pasa por `Loc`.
- **Test:** `Fase23_RazonDeDerrota` (forzar ambos casos).
- **Evidencia [7R]:** `despues_derrota_escuadra`, `despues_derrota_civil`.

#### T-06 · Radial desactivado en RTS
- **Estado real:** `ResolverGestoDeQ` (`Player/PlayerInputDriver.Radial.cs:48-60`) no mira el modo.
- **Pasos:** salir temprano si `Rig.Mode == ControlMode.Rts`; si el radial estaba abierto al entrar a RTS, cerrarlo (sin ejecutar orden).
- **Aceptación/Test:** `Fase23_RadialNoAbreEnRts`.

#### T-07 · LOD de árboles lejanos
- **Pedido:** "De lejos se ve raro, como cortado o solo aristas; de cerca bien."
- **Pasos:** 1) Identificar si los árboles son `Terrain` tree prototypes o prefabs con `LODGroup` (`Editor/TerrainSetupHelper.cs`, `BlockoutArtDresser.cs`). 2) Medir: porcentajes de transición de cada LOD, si el último LOD es billboard, `Alpha Clipping`/umbral del material, `Terrain.treeBillboardDistance`, `treeDistance`. 3) Causas típicas: LOD final con malla decimada sin normales correctas o material sin alpha clip, o salto directo a "Culled".
- **Aceptación:** misma posición de cámara a 50/100/150/250 m sin siluetas rotas; costo de frame no sube más de 5 % (benchmark).
- **Evidencia:** `antes_arboles_150m`, `despues_arboles_150m` (misma cámara, fijada por script); tabla de distancias en el log.

---

### OLA 2 — UI y HUD rápidos

#### T-08 · Quitar el cartel "X EN COBERTURA"
- **Estado real:** `Ai/AiBrain.Tactica.cs:127` → `Feedback.Accion(SfxKind.CoverTake, "<NOMBRE> EN COBERTURA", …)`.
- **Pasos:** conservar el sonido y el holograma, quitar el texto (parámetro o sobrecarga de `Feedback.Accion` sin cartel). Aplica a FPS y RTS.
- **Test:** `Fase23_SinCartelCobertura` (orden de cobertura → ningún `WorldTag`/alerta con "COBERTURA").

#### T-09 · Cartel de seleccionados abajo al centro **[7R]**
- **Archivos:** `UI/SelectionCountView.cs`, `UI/SelectedSoldierUI.cs`. Ojo: `InstructionBannerView` ya vive abajo-centro → apilarlos sin solaparse (usar `UI/Diagramador.cs`).
- **Aceptación:** anclado bottom-center en las 7 resoluciones y con escala de interfaz 100/125/150 %, sin tapar el banner de instrucciones.

#### T-10 · HUD inferior izquierdo: munición en número, vida solo barra
- **Archivos:** `UI/PlayerHealthView.cs` (quitar número), `UI/WeaponStatusView.cs` (cargador / reserva total).
- **Aceptación:** abajo a la izquierda se lee `12 / 84`; la vida es solo barra. Test `Fase23_HudMunicion`.

#### T-11 · Radial: soldado muerto en gris con calavera
- **Archivos:** `UI/MenuDeOrdenes.cs` (contexto), `UI/RadialIconFactory.cs` (icono calavera por código, mismo estilo de los iconos existentes).
- **Aceptación:** opción de un soldado muerto: gris, texto "MUERTO" (Loc), calavera, no confirmable. Test `Fase23_RadialMuerto`.

#### T-12 · Borde rojo vibrante al recibir impacto
- **Estado real:** ya existen `UI/DamageVignetteView.cs` (viñeta negra), `UI/LowHealthPulseView.cs` (pulso rojo con poca vida) y `UI/ScreenFlashView.cs`.
- **Pasos:** en `DamageVignetteView`, al `DamageTakenEvent` del poseído, pulso rojo de borde (0.35 s, intensidad ∝ daño, con overshoot). Respetar `CameraFxSettings` (interruptor global de efectos).
- **Aceptación:** visible en cada impacto, no tapa la mira, no se acumula más allá de un tope.
- **Evidencia:** `despues_borde_rojo` (capturar el frame pico).

#### T-13 · Ajustes: volúmenes, grupos por color, resolución desplegable, idioma, quitar calidad/daltonismo/HUD mínimo/sonido de subtítulos **[7R]**
- **Archivos:** `UI/AjustesDeJuego.cs`, `UI/LayoutDeAjustes.cs`, `Presentation/PauseController.cs`, `Presentation/AudioDirector.cs`, `Presentation/Subtitulos.cs`, `Core/Loc.cs`.
- **Pasos:**
  1. Volumen (D8): `Master` multiplica a `Música`, `Efectos`, `Voces/UI`. Cada grupo con su color de relleno de slider.
  2. Tres bloques visuales con color propio: **Interfaz**, **Sensibilidad**, **Volumen**. Layout con `LayoutDeAjustes` (responsivo a resolución y escala).
  3. Resolución: `Dropdown` con `Screen.resolutions` sin duplicados, aplicado al confirmar.
  4. Quitar: sección Calidad (`AjustesDeJuego.cs:61,111`), daltonismo (D7), HUD mínimo del menú (D7), opción de sonido de subtítulos.
  5. Idioma: el parpadeo viene de reconstruir todo el panel al cambiar idioma. Separar "refrescar textos" de "refrescar valores". Generar lista de textos que no pasan por `Loc` → `Docs/RONDA_14_TEXTOS_SIN_TRADUCIR.txt` y traducirlos en `LocTextos.cs`.
  6. Pantalla completa: probar `FullScreenMode.FullScreenWindow` y `ExclusiveFullScreen` en 1920×1080 y 1280×720 (build real, `-spmetrics` no alcanza: hacerlo en la etapa de build de `qa_total`).
- **Aceptación:** bajar Master a 0 silencia todo; cambiar idioma 10 veces no hace parpadear otros controles; 0 textos sin traducir en ajustes/pausa/HUD; fullscreen sin UI cortada.
- **Test:** `Fase23_VolumenMaestro`, `Fase23_AjustesSinCalidad`, `Fase23_IdiomaSinParpadeo` (los valores de los otros controles no cambian al cambiar idioma).
- **Evidencia:** `despues_ajustes_es`, `despues_ajustes_en`, `despues_fullscreen_1080`, `despues_fullscreen_720`.

---

### OLA 3 — Rombos y minimapa

#### T-14 · Rombo del tanque con color por ocupación
- **Estado real:** `UnitLocatorCylinder` ya pone rombo a unidades y a vehículos con tripulante (`:199`); paleta en `DiamondGizmo`.
- **Pasos:** paleta D13 en `DiamondGizmo`; el rombo del tanque existe siempre (también vacío) y recalcula color cuando cambia un asiento (`Vehicles/Vehicle.cs`, `RoleOf`).
- **Test:** `Fase23_RomboTanque` (vacío/aliado/enemigo/mixto → color esperado).
- **Evidencia:** 4 fotos `despues_rombo_tanque_<estado>`.

#### T-15 · Rombos de interactuables + icono de munición
- **Estado real:** `InteractGearMarker.cs` ya arma rombos de interactuable; la munición es una moneda 3D (`Editor/MonedaMunicionBuilder.cs`, `Player/MunicionPickup.cs`).
- **Pasos:** rombo con color D13 sobre `TorretaFija` libre, `CajaDeSuministros` (botiquín) y `MunicionPickup`; se apaga cuando ya no es interactuable. Munición: reemplazar la moneda por un rombo azul claro (mismo builder).
- **Test:** `Fase23_RombosInteractuables`.
- **Evidencia:** `despues_rombo_torreta`, `despues_rombo_botiquin`, `despues_rombo_municion`.

#### T-16 · Rombo del enemigo al que le pegás: visible a través de obstáculos por 4 s
- **Archivos:** `Presentation/UnitLocatorCylinder.cs`, `Presentation/RomboVisibilidad.cs`.
- **Pasos:** bool `IgnorarOclusion` por marcador; se pone en true con `DamageTakenEvent` cuyo `AttackerId` es el poseído; timer de 4 s que se reinicia con cada impacto. Implementar con un segundo material con `ZTest Always` (no cambiar el shader global).
- **Test:** `Fase23_RomboSinOclusion` (true tras impacto, false a los 4.1 s sin impactos).
- **Evidencia:** `despues_rombo_tras_muro`.

#### T-17 · Minimapa: aliados, tanques vacíos, interactuables, entorno por forma
- **Estado real:** `MinimapIcon` vive en la capa Minimap; `MinimapIcon.RegistrarObstaculos` ya registra obstáculos (`GameplaySceneBootstrap.cs:93`).
- **Pasos:** 1) Diagnosticar por qué no se ven aliados (filtro de registro o capa) y corregir. 2) Registrar vehículos sin tripulación. 3) Forma por tipo: aliado = círculo azul, enemigo = triángulo rojo, vehículo = cuadrado (color D13), interactuable = rombo, cobertura/collider de entorno = rectángulo gris plano. **No** shader de post-proceso: iconos planos en la capa Minimap (más barato).
- **Test:** `Fase23_MinimapaRegistro` (conteo por tipo).
- **Evidencia:** `antes_minimapa`, `despues_minimapa`.

#### T-18 · Minimapa: círculo que se expande desde quien te disparó
- **Pasos:** `DamageTakenEvent` del poseído con `AttackerId` válido → `ActorRegistry` da la posición → anillo pooleado en la capa Minimap que crece con lerp 0→8 m en 1.2 s y se desvanece. Máximo 4 simultáneos.
- **Test:** `Fase23_AnilloAtacante`.
- **Evidencia:** `despues_anillo_chico`, `despues_anillo_grande`.

---

### OLA 4 — Control, cámara, cursor, órdenes

#### T-19 · Cinemática: avanzar de toma con tecla, transición 0.5 s
- **Archivo:** `Mision/CinematicaDeIntro.cs` (bucle de `:145-179`).
- **Pasos:** cartel "[ESPACIO / CLIC] SIGUIENTE" (Loc). Al apretar durante el viaje o la espera de un waypoint: terminar el viaje con una transición de **0.5 s** desde la pose actual y saltear la espera. Apretar en todas las tomas recorre la cinemática en ~0.5 s × tomas. Esc queda para el menú (T-02).
- **Aceptación:** el log muestra `toma i→i+1` con Δt ≤ 0.55 s al saltar.
- **Test:** `Fase23_CinematicaSaltoRapido` (llamar el método de avance N veces → termina en < 0.6 × N s de reloj simulado).
- **Depende de:** T-02.

#### T-20 · RTS: roster con clic (centra) y doble clic (entra a FPS)
- **Archivos:** `UI/RosterRowView.cs`, `UI/SelectedSoldierUI.cs`, `Camera/CameraRig.cs` (`RecenterOn`), `Player/PossessionService.cs`.
- **Pasos:** clic simple = seleccionar + `RecenterOn(pos)`; doble clic (≤ 0.3 s) = poseer y pasar a FPS.
- **Test:** `Fase23_RosterClics`.

#### T-21 · RTS: `F` focaliza, rueda ×3, rotación orbital con `|`
- **Archivos:** `Player/KeyBindings.cs` (acciones `FocalizarRts`=F, `RotarCamaraRts`=Backquote — D4, D5), `UI/ControlsTable.cs`, `UI/KeyRebindView.cs`, `Player/PlayerInputDriver.cs` (`UpdateRts`, rueda en `:2898-2899`), `Camera/CameraRig.cs` (`RtsLookEuler` en `:52-54`, hoy yaw fijo en 0).
- **Pasos:**
  1. `F`: centrar en el poseído o en el primer seleccionado.
  2. Rueda: `rtsZoomSpeed × 3`.
  3. Agregar `rtsYaw` a `CameraRig`: `RtsLookEuler = (90 − rtsInclinacionAdelante, rtsYaw, 0)`. Con `|` apretado, `Δmouse.x` cambia el yaw orbitando `rtsFocusPoint`. La inclinación (pitch) **no** cambia al rotar: a 180° se ve la escena "desde atrás" con la misma inclinación. `RtsCameraPositionFor` y el paneo WASD deben usar el yaw (si no, WASD queda invertido al rotar).
  4. Guardar/restaurar el yaw con `savedRtsFocus` (`:368`).
- **Aceptación:** tras rotar 180°, W sigue moviendo "hacia arriba de la pantalla"; zoom hacia el cursor sigue apuntando al punto correcto.
- **Test:** `Fase23_RtsYawPaneo`, `Fase23_RtsZoomTriple`, `Fase23_TeclasNuevasEnKeyBindings`.
- **Evidencia:** `despues_rts_yaw_0`, `despues_rts_yaw_180`.

#### T-22 · F1/F2/F3: verificar y completar
- **Estado real:** en FPS poseen al 1/2/3 (`PlayerInputDriver.cs:798-805`), en RTS seleccionan (`PlayerInputDriver.Escuadra.cs:69-74`).
- **Pasos:** 1) Reproducir el reclamo: ¿falla en algún modo (vehículo, torreta, tras revivir, con el tutorial)? Registrar en el log. 2) Arreglar solo lo que falle. 3) En RTS, además de seleccionar, centrar la cámara (coherente con T-20).
- **Test:** `Fase23_FTeclas` (FPS y RTS, soldado vivo y muerto → `DeadNoticeView`).

#### T-23 · Cursor contextual y de color en RTS y FPS
- **Estado real:** `Presentation/CursorContextual.cs` (53 líneas, `CursorTipo`) ya cambia en RTS con selección.
- **Pasos:** tipos: Atacar (enemigo), Seguir/Mover (aliado), Subir/Montar (vehículo o torreta libre), Recoger (arma/munición/caja), Cubrirse (punto de `Coberturas`), Interactuar (`IInteractable`), Default. Texturas por código (patrón `KeyCapView`/`SpriteDeTriangulo`). En FPS el equivalente es el color de la mira (`AimUI`): mismo mapa de colores. Resolver el objetivo con `AimTargeting` (no otro raycast).
- **Test:** `Fase23_CursorPorObjetivo` (un caso por tipo).
- **Evidencia:** una foto por tipo.

#### T-24 · `C` mantenido: líneas; `C` + clic: orden de cubrirse
- **Estado real:** `VerTactico` se lee en `PlayerInputDriver.cs:1887`, `:2585` y `PlayerInputDriver.Escuadra.cs:96`; ya existe `AttackLineManager` (líneas rojas de ataque) y `OrdenesDeEscuadra.PuntoDeCobertura`.
- **Pasos (D1, D2):**
  1. Con `C` apretado: `AttackLineManager` dibuja líneas del poseído a enemigos visibles o que le pegaron hace ≤ 6 s (memoria corta alimentada por `DamageTakenEvent`), y líneas azules a aliados vivos. Pool de líneas, tope 16.
  2. `C` + clic izq.: bloquear el disparo ese frame; resolver punto con `AimTargeting`; destinatario = seleccionado si está libre, si no el aliado libre más cercano al punto (libre = sin orden de movimiento en curso en `OrderService`). Emitir por `OrdenesDeEscuadra` (misma ruta que el radial "Cubrirse").
- **Aceptación:** con C apretado nunca sale un disparo; dos C+clic seguidos asignan a dos aliados distintos.
- **Test:** `Fase23_CClicCobertura`, `Fase23_CLineas`.

---

### OLA 5 — Escuadra e IA

#### T-25 · Formación: a mis costados, "sentirse poderoso"
- **Archivos:** `Player/OrderService.cs` (`FormationKind`), `Ai/AiBrain.Tactica.cs` (seguir al líder), `Ai/AjustesDeEscuadra.cs` (distancias).
- **Pasos:** formación de seguimiento por defecto en **cuña**: índice par a la derecha, impar a la izquierda, 2.5 m lateral y 1.5 m atrás (valores en `AjustesDeEscuadra`). Validar el punto con `NavService`; si no es alcanzable, caer al punto de atrás. Al trotar, igualar la velocidad del líder (que no se queden atrás).
- **Aceptación:** caminando recto 20 m, los aliados quedan dentro de ±30° del lateral del jugador el 80 % del tiempo.
- **Test:** `Fase23_FormacionLateral` (muestreo de ángulos).
- **Evidencia:** `despues_formacion_fps`, `despues_formacion_rts`.

#### T-26 · Agacharse contagia a los aliados cercanos
- **Pasos:** cuando cambia `SoldierMotor.IsCrouching` del poseído, los aliados que lo siguen a ≤ 8 m copian el estado (vía `AiBrain`, dentro de `Step`). Al levantarse, se levantan (salvo los que están en cobertura).
- **Test:** `Fase23_AgachadoContagio` (reusar la preparación de `Fase21_Agachado`).

#### T-27 · Médico cura/revive solo al acercarse si no recibe impactos
- **Estado real:** `Player/Reanimacion.cs` es el único camino para revivir; existe `Fase21_MedicoEnCalma`.
- **Pasos:** el médico (`RoleType` médico) libre, a ≤ 2.5 m de un herido o caído, arranca la acción automáticamente; `AccionesEnCurso` la muestra; un `DamageTakenEvent` sobre el médico la cancela.
- **Test:** `Fase23_MedicoAutomatico` (con y sin impacto).

#### T-28 · En calma cualquiera revive a cualquiera; regeneración ×3 más lenta
- **Archivos:** `Combat/Health.cs:31` (`SegundosSinDanoParaRegenerar` 3 → 9, D9), `Player/Reanimacion.cs`, `Ai/AiBrain.Revivir.cs`.
- **Pasos:** en calma, `Reanimacion` acepta a cualquier aliado vivo como reanimador (no solo el médico); fuera de calma se mantiene la regla actual.
- **Test:** `Fase23_ReviveEnCalma`, `Fase23_RegenNueveSegundos` (no regenera a los 8.9 s, sí a los 9.1 s).

#### T-29 · IA en cobertura: agacharse, asomarse, esquinas, recargar cubierto
- **Estado real:** `AiBrain.Tactica.cs` ya busca coberturas (del jugador o propias en combate); `AiBrain.cs:781` agacha en cobertura; `Core/Coberturas.cs` define puntos.
- **Pasos:** sub-estado de cobertura con ciclo **oculto (1.2–2.0 s) → asomado (0.8–1.4 s, dispara) → oculto**; si el cargador está vacío, recarga oculto y no se asoma. En esquinas (punto de cobertura en el borde de un obstáculo), "asomarse" = desplazamiento lateral 0.6 m en vez de pararse. Aplica a aliados no poseídos y enemigos. Todo dentro de `Tick` (Step).
- **Aceptación:** en `BalanceBench`, el % de tiempo expuesto de una unidad en cobertura baja al menos 40 % respecto de hoy; el benchmark de estrés (50+) no empeora más de 10 %.
- **Test:** `Fase23_CicloCobertura`, `Fase23_RecargaCubierto`.
- **Evidencia:** 3 fotos del mismo enemigo: `oculto`, `asomado_disparando`, `recargando_oculto`.

---

### OLA 6 — Misión y civil

#### T-30 · Civil quieto y atado hasta liberarlo
- **Estado real:** `SpawnCivilOculto` lo agacha y lo pone `Pasivo` (`Mision/MisionDirector.cs:443-452`); `AparecerCivil` lo levanta (`:457`) y desde ahí se mueve.
- **Pasos:** estado `Atado` en `Rehen`: motor con velocidad 0, sin rotar, animación H-1 (o `idle crouching` como plan B). `MisionDirector` solo lo pasa a "seguir" al completar T-31.
- **Test:** `Fase23_CivilQuieto` (posición constante ±0.05 m durante 30 s de Rescatar con disparos cerca).

#### T-31 · Desatar con Espacio repetido + barra sobre el civil
- **Estado real:** hoy es por tiempo: `tRescate` sube si estás a ≤ 4.5 m (`MisionDirector.cs:576`), `DuracionRescate = 10` (`:50`); la barra la dibuja `MisionHud.cs:129` en el cartel de misión (no sobre el civil).
- **Pasos (D6):** `NudosParaLiberar = 12` (en `MisionDirector`); cada Espacio dentro del radio suma 1; sin apretar por 2 s pierde 1 nudo cada 0.5 s. `ProgresoRescate = nudos / 12`. Barra en el mundo sobre el civil reusando `AccionesEnCursoView.BarraMundo`, con marcas por nudo; quitar la barra del `MisionHud`. Cartel "[ESPACIO] DESATAR" (Loc). Espacio no salta mientras aplica.
- **Test:** `Fase23_Nudos` (12 pulsaciones → liberado; 11 y esperar → baja).
- **Evidencia:** `despues_barra_sobre_civil_50`, `despues_barra_sobre_civil_100`.

#### T-32 · Civil visible mientras te sigue y huye; ropa nueva
- **Pasos:** 1) Diagnosticar por qué no se ve al seguir: candidatos `AliadosSinEstorbo` (oculta aliados pegados a la cámara), LOD de `WorldUiDirector`, o el cuerpo oculto al subir a un vehículo. 2) El civil nunca se oculta por `AliadosSinEstorbo` (a lo sumo semitransparente). 3) Ropa: H-4 en `SoldierLook.MaterialCivil()`.
- **Test:** `Fase23_CivilVisible` (renderers activos durante toda la fase Escapar).
- **Evidencia:** `despues_civil_siguiendo`, `despues_civil_ropa`.

#### T-33 · Cartel "volvé al objetivo" si te alejás
- **Estado real:** `MisionDirector` ya tiene `tiempoFuera` y `avisoAtras` (`:69`) y `MisionHud` muestra distancia al objetivo.
- **Pasos:** si la distancia al objetivo crece durante 4 s y supera un umbral por fase, cartel en `Hud_Feedback` con pulso de escala (1→1.15) y flecha; se expande mientras siga creciendo la distancia; fade al volver.
- **Test:** `Fase23_CartelVolver`.

---

### OLA 7 — Combate y sensación

#### T-34 · Luminarias: se apagan al dispararles y tiran chispas
- **Estado real:** las luces las crea `Editor/NightLightingBuilder.cs` (`AddComponent<Light>` en `:159`), probablemente sin collider propio.
- **Pasos:** el builder agrega a cada lámpara un collider chico + componente `Luminaria` (vida 1). En `Projectile` (resolución de impacto contra entorno, cerca de `:394`) detectar `Luminaria` → evento nuevo `LuminariaRotaEvent(pos)` → la presentación apaga la `Light`, baja la emisión del material y dispara chispas pooleadas (`ImpactFxPool`/`SpriteFx`) + `SfxKind.ImpactMetal`.
- **Test:** `Fase23_Luminaria`.
- **Evidencia:** `antes_luminaria`, `despues_luminaria_chispas`, `despues_luminaria_apagada`.

#### T-35 · Lanzacohetes destroza tanques
- **Estado real:** cohete `Damage = 95` (`Combat/WeaponCatalog.cs:64`), tanque `maxHealth = 260` (`Vehicles/Vehicle.cs:22`), y la explosión reparte con caída por distancia (`Projectile.cs:698-707`).
- **Pasos (D11):** `Spec.MultiplicadorVsVehiculo` (default 1; cohete 2.5), aplicado en el impacto directo y en `ExplodeAt` sobre vehículos.
- **Test:** `Fase23_CoheteVsTanque` (2 impactos directos destruyen; 1 no).
- **Evidencia:** barra del tanque antes/después de un impacto.

#### T-36 · Headshot con sonido grande
- **Pasos (D10):** detección en `Projectile` al impactar un soldado; evento `HeadshotEvent(shooterId, targetId, pos)`; `KillFeedbackDirector` reproduce un sonido nuevo `SfxKind.Headshot` (sintetizado en `SfxSintetico`, más grave y largo que `Hit`, agregado **al final** del enum), destello corto de mira y texto "¡TIRO A LA CABEZA!". Si mata, usar las animaciones de muerte por headshot ya importadas (`death from front headshot.fbx`, etc.).
- **Test:** `Fase23_Headshot` (impacto alto → evento y daño ×2; impacto en torso → no).
- **Evidencia:** `AudioAnalisisReport` con el clip nuevo (pico y duración) + marca en log.

#### T-37 · Temporizador visual de la granada
- **Archivos:** `Combat/Granada.cs`, `Presentation/TrayectoriaGranadaView.cs`.
- **Pasos:** anillo en el piso bajo la granada que se cierra con el tiempo restante + luz que titila más rápido; en el Editor, `OnDrawGizmos` con el tiempo.
- **Evidencia:** 3 fotos a 100 %, 50 %, 10 % del tiempo.

#### T-38 · Cadáveres visibles
- **Estado real:** `CubeFxReactor.MorirAnimado` oculta el cuerpo a los `SegundosHastaDesaparecer` (2 s) (`Presentation/CubeFxReactor.cs:210-222`).
- **Pasos (D12):** no ocultar; cola FIFO de 24 cadáveres; al revivir (`Health` avisa, ver `Health.cs:52`) se saca de la cola. Los marcadores (rombo, alerta, minimapa) sí se apagan al morir.
- **Test:** `Fase23_Cadaveres` (visible a los 10 s; el 25.º oculta al 1.º); correr también el estrés 50+.

#### T-39 · Zoom con el arma centrada y nítida
- **Archivos:** `Presentation/MiraOptica.cs`, `UI/MirillaView.cs`, `Presentation/ArmaEnLaMano.cs`, `Camera/CameraRig.cs` (zoom FOV 60→25).
- **Pasos:** en ADS, interpolar el arma a una pose centrada (línea de mira en el centro de pantalla); evitar recorte por near plane y desenfoque (revisar DOF/post en `PostFxDirector`).
- **Evidencia:** `antes_zoom`, `despues_zoom` por cada arma del catálogo.

#### T-40 · Animaciones de melee, granada y recarga con máscara de piernas
- **Archivos:** `Presentation/SoldierAnimatorDriver.cs`, controlador del Animator (lo arma `SoldierPrefabPipeline`/`SoldierVariantBuilder`), `Presentation/CuchilloFx.cs`, `Combat/WeaponHolder.cs` (`MeleeAttackEvent`).
- **Pasos:** capa "Torso" con `AvatarMask` sin piernas, peso 1 durante la acción; triggers `Melee`, `Granada`, `Recarga` disparados por los eventos que ya existen (`MeleeAttackEvent`, `GrenadeThrownEvent`, `WeaponHolder.IsReloading`). Usar H-2; `reloading.fbx` ya está.
- **Aceptación:** caminando, las piernas siguen el ciclo mientras el torso hace la acción.
- **Evidencia:** 3 fotos caminando (una por acción).

#### T-41 · Partículas de polvo al caminar y con el tanque en movimiento
- **Pasos:** emisor pooleado de polvo (`DebrisPool`/`ImpactFxPool`) cada 0.9 m en soldados que corren y cada 0.5 m por oruga en el tanque; tamaño/intensidad distintos; respetar LOD de distancia de `WorldUiDirector` para no pagar lejos de la cámara.
- **Aceptación:** benchmark sin empeorar más de 3 %.
- **Evidencia:** `despues_polvo_soldado`, `despues_polvo_tanque`.

---

### OLA 8 — Escape en camioneta con motos (feature grande)

#### T-42 · Reemplazar el helicóptero por una camioneta con recorrido
- **Estado real:** `Mision/Helicoptero.cs`, `Mision/CinematicaDeVictoria.cs` (sube escuadra y civil al heli), `MisionDirector` fases `Escapar`/`Victoria`, textos "AL HELICOPTERO" (`MisionDirector.cs:431`, `:597`), cinemática de intro que muestra "a donde debe ir el helicoptero", tutorial y tests que lo nombran.
- **Pasos:**
  1. `grep -rn -i "heli"` en Scripts y Editor → lista de todos los usos en el log (STOP si > 40 usos fuera de Mision: pedir confirmación).
  2. `Mision/Camioneta.cs`: vehículo que reusa `Vehicle` + `VehicleMotor` + `VehicleBrain` (sigue waypoints; no inventar otro sistema de rutas). Malla H-3 o blockout.
  3. Al llegar con el civil a la extracción: todos suben (reusar `BoardAll`), la camioneta arranca un recorrido de ~60 s por waypoints (`WaypointGraph`/lista en el builder `MisionBuilder`), el jugador queda como artillero/pasajero con arma.
  4. Victoria al final del recorrido con la camioneta viva; derrota si la destruyen (razón nueva en T-05).
  5. Actualizar textos (Loc), cinemática de intro y tests que nombran al helicóptero.
- **Test:** `Fase23_EscapeCamioneta` (llega al final en tiempo simulado; destruida → derrota con razón).

#### T-43 · Motos enemigas perseguidoras que explotan
- **Pasos:** `Vehicles/Moto.cs` (o prefab de `Vehicle` de 2 asientos: conductor + tirador), IA de persecución: mantener 12–25 m detrás/al costado de la camioneta y disparar. Oleadas de 2–3 motos durante el recorrido. Vida baja (60); al destruirse: explosión con `ImpactFx.SpawnExplosion` + `Fragmentador`/`DebrisPool` + `SfxKind.Explosion`, sin Instantiate en combate (pool de motos).
- **Test:** `Fase23_Motos` (aparecen, disparan, mueren y vuelven al pool).
- **Evidencia:** `despues_motos_persiguen`, `despues_moto_explota`.

---

### OLA 9 — Cierre

#### T-44 · QA total e informe de ronda
- **Pasos:** `Tools\Qa\qa_total.bat` completo; `python Tools/Indice/indexar_ia.py --check`; tutorial automático entero (los cambios de teclas pueden romper pasos: `TutorialAutoPlayer.Entrada.cs` usa C, F, Espacio); escribir `Docs/RONDA_14_INFORME.md` con, por tarea: pedido, archivos, Check nuevos, fotos antes/después, veredicto del Verificador.
- **Aceptación:** suite `OK`, `historico.csv` sin regresión de FPS/GC > 5 %, tutorial automático completo, 0 checks nuevos en FALLO.

---

## 6. Matriz de trazabilidad (pedido → tarea)

| # | Pedido (resumido) | Tarea |
|---|---|---|
| 1 | Cinemática: tecla para siguiente toma, transición 0.5 s | T-19 |
| 2 | Al seguirme, a izquierda y derecha; sentirse poderoso | T-25 |
| 3 | Civil no debe moverse; animación atado en cuclillas | T-30, H-1 |
| 4 | Colliders invisibles (torreta) | T-03 |
| 5 | Luminarias se apagan con chispas | T-34 |
| 6 | Rombo del tanque con color por estado | T-14 |
| 7 | Rombos de torreta, botiquín, munición | T-15 |
| 8 | Icono de munición: rombo azul | T-15 |
| 9 | C + clic: cubrirse; si ocupado, el más cercano | T-24 |
| 10 | Cartel vibrante "volver al objetivo" | T-33 |
| 11 | Ver al civil mientras huye | T-32 |
| 12 | Cursor RTS: atacar, subir, montar, recoger | T-23 |
| 13 | IA: cobertura, agacharse, asomarse, esquinas, recargar cubierto | T-29 |
| 14 | Quitar cartel "soldado cobertura" | T-08 |
| 15 | Cartel de seleccionados abajo al centro | T-09 |
| 16 | Médico cura/revive al acercarse si no recibe impactos | T-27 |
| 17 | Rehén: Espacio repetido, barra por nudos | T-31 |
| 18 | Zoom con arma centrada y clara | T-39 |
| 19 | Si me agacho, los cercanos también | T-26 |
| 20 | Camioneta en vez de helicóptero + motos que explotan | T-42, T-43, H-3 |
| 21 | Ver el cadáver | T-38 |
| 22 | En calma cualquiera revive a cualquiera | T-28 |
| 23 | Tiempo de calma ×3 para curarse | T-28 |
| 24 | Anillo de recarga trabado; canvases separados | T-01 |
| 25 | Minimapa: círculo desde quien me disparó | T-18 |
| 26 | F1/F2/F3 cambian de soldado | T-22 |
| 27 | Cursor de color con aliados/interactuables; distinguir acción | T-23 |
| 28 | Radial: soldado muerto en gris con calavera | T-11 |
| 29 | Colliders en objetos de cobertura | T-03 |
| 30 | Timer/gizmo de granada | T-37 |
| 31 | Animaciones melee/granada/recarga con máscara de piernas | T-40, H-2 |
| 32 | Lanzacohetes hace mucho daño al tanque | T-35 |
| 33 | Razón de la derrota visible | T-05 |
| 34 | LOD de árboles | T-07 |
| 35 | Ajustes: volúmenes simples, colores, general afecta al resto, agrupar | T-13 |
| 36 | Accesibilidad: quitar daltonismo/HUD mínimo, resolución desplegable, idioma, textos, sonido de subtítulos | T-13 |
| 37 | Quitar calidad | T-13 |
| 38 | Testear fullscreen | T-13 |
| 39 | Otra ropa para el civil | T-32, H-4 |
| 40 | Esc en cinemática abre el menú | T-02 |
| 41 | Tanques vacíos en el minimapa | T-17 |
| 42 | Minimapa con interactuables, enemigos, entorno por forma/color | T-17 |
| 43 | Borde rojo al recibir impacto | T-12 |
| 44 | Barra de "liberando" sobre el civil | T-31 |
| 45 | Headshot con sonido de logro | T-36 |
| 46 | Rombo del enemigo ignora oclusión 4 s | T-16 |
| 47 | RTS: clic en roster centra, doble clic entra a FPS | T-20 |
| 48 | RTS: Espacio centra en todos, F focaliza | T-21 (F), D3 (Espacio ya existe) |
| 49 | RTS: rueda ×3, rotación con `|` e inclinación | T-21 |
| 50 | RTS: sin radial | T-06 |
| 51 | HUD: total de balas, vida solo barra | T-10 |
| 52 | Partículas al caminar y con el tanque | T-41 |
| 53 | Pivote del soldado elevado | T-04 |
| 54 | Aliados en el minimapa | T-17 |
| 55 | Mantener C: líneas a enemigos y aliados | T-24 |

---

## 7. Riesgos conocidos

| Riesgo | Dónde | Mitigación |
|---|---|---|
| Cambiar teclas rompe el tutorial automático | `TutorialAutoPlayer.Entrada.cs` / `.Radial.cs` (usan C, F, Espacio, Ctrl) | Correr el tutorial automático al cerrar las Olas 4 y 6 |
| Tests viejos que nombran al helicóptero | `HeadlessTestRunner*.cs`, `MissionFlowTestRunner.cs` | T-42 paso 1 lista todos los usos antes de tocar |
| Cadáveres + polvo + líneas degradan el estrés 50+ | T-38, T-41, T-24 | Topes fijos y benchmark por tarea |
| IA de cobertura cambia el balance | T-29 | `Editor/BalanceBench.cs` antes/después |
| Reparentar HUD rompe referencias serializadas | T-01 | Hacerlo en los builders y correr `Fase22` (búsquedas globales) |
| Estado estático nuevo sobrevive entre corridas | T-16, T-18, T-24 | Registrar en `ReinicioDeEstaticos`; correr "100 iteraciones" al cerrar cada ola |
