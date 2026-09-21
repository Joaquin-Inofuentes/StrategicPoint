# Plan de implementación — eliminar los `Find*` de runtime

Censo exacto sobre `Assets/_Project/Scripts`, excluyendo `Editor/` y excluyendo
líneas comentadas. **224 llamadas en 77 archivos de runtime.**

| Familia | Cantidad | Costo real |
|---|---|---|
| `FindAnyObjectByType` / `FindFirstObjectByType` | 50 | Barrido de escena hasta el primer match |
| `FindObjectsByType<T>` | 35 | Barrido completo + asignación de array |
| `GameObject.Find("nombre")` | 31 | Barrido completo por nombre, sin índice |
| `Resources.FindObjectsOfTypeAll` | 2 | El más caro: incluye assets y objetos ocultos |
| **Subtotal búsquedas globales** | **118** | |
| `transform.Find("hijo")` | 102 | Barato (hijos directos), pero frágil por nombre |
| `Shader.Find` | 4 | Se paga una vez por material |

Las 121 llamadas restantes viven en `Editor/` (pipelines y la suite headless).
**No entran en este plan**: ahí el costo es irrelevante y el barrido es
legítimo, porque el pipeline justamente construye la escena.

---

## Nota de honestidad antes de empezar

Medí dónde corre cada llamada. **Casi todas están en `Awake`, `OnEnable`,
`Start` o `Bootstrap()`, no en `Update`.** El único `Find` en un hot path real
es `UI/HudPulido.cs:43-52`, que llama `GameObject.Find("Canvas")` dentro de su
ciclo por frame.

O sea: esto **no es principalmente una optimización de FPS**. El beneficio real
es otro, y es el que justifica el trabajo:

1. **Costo de carga de escena.** 118 barridos concentrados en los primeros
   frames de `SC_Gameplay` (2 MB de escena) es un tirón medible al entrar a
   partida, no un problema de régimen.
2. **Fragilidad.** 31 búsquedas por nombre literal (`"Tut_Pared"`, `"Canvas"`,
   `"Enemies"`) se rompen en silencio si alguien renombra un objeto. No hay
   error de compilación, no hay excepción: la referencia queda en `null` y la
   funcionalidad desaparece. Varios bugs de `RONDA_10_BUGS.md` son exactamente
   eso.
3. **Reintentos por frame.** El patrón `if (x == null) x = FindAnyObjectByType<T>()`
   aparece 14 veces. Cuando el objeto **sí** existe se paga una vez y está
   bien; cuando **no** existe, se paga el barrido completo en cada frame para
   siempre. Es el caso peligroso y es invisible leyendo el código.

Ordeno las fases por valor real, no por cantidad de llamadas.

---

## Fase 0 — La red de seguridad (primero, antes de tocar nada)

Sin esto, los `Find` vuelven a entrar en la próxima ronda. El proyecto **ya
tiene el mecanismo**: `Editor/HeadlessTestRunner.Fase19.cs:175-186` escanea el
código de runtime y falla si encuentra `Camera.main` o `Resources.Load<`. Hay
que extenderlo, no inventar nada.

**Archivo:** `Editor/HeadlessTestRunner.Fase19.cs`

**Implementación:**

1. Agregar al mismo bucle que ya recorre `Assets/_Project/Scripts` un contador
   por familia (`GameObject.Find(`, `FindObjectsByType<`, `FindAnyObjectByType`,
   `FindFirstObjectByType`, `Resources.FindObjectsOfTypeAll`).
2. **Presupuesto decreciente**, no cero de entrada. Una constante por familia
   que se baja al cerrar cada fase:
   ```csharp
   const int PresupuestoBusquedasGlobales = 118; // baja con cada fase; meta: lista blanca
   ```
   `Check("Las busquedas globales de escena no crecen", globales <= PresupuestoBusquedasGlobales)`
3. **Lista blanca explícita**, con el archivo y el motivo, igual que la
   exclusión que ya existe para `CamaraPrincipal.cs` y `RecursosCache.cs`:
   ```csharp
   static readonly string[] BusquedaPermitida = {
       "Core/ActorRegistry.cs",            // Rebarrer: capta los que arrancan desactivados
       "Core/WorldSystemsRegistry.cs",     // EnsurePopulated: una sola vez, guardado por flag
       "Core/ApoyoEnElPiso.cs",            // arranque, una vez
       "Core/Coberturas.cs",               // Registrar(), arranque
       "Core/MetricasDeBuild.cs",          // diagnóstico a pedido
       "Presentation/LimpiezaDeEscena.cs", // utilidad de limpieza
   };
   ```
4. Un `Check` extra que falle si aparece el patrón
   `if (.* == null) .* = FindAnyObjectByType` fuera de la lista blanca — ese es
   el reintento por frame.

**Verificación:** correr la suite antes de cambiar nada. Tiene que pasar con el
presupuesto en 118. Si no pasa, el escáner está mal escrito, no el código.

**Riesgo:** ninguno, es solo un test.

---

## Fase 1 — `HudPulido`: el único `Find` por frame

**Archivo:** `UI/HudPulido.cs:43-52` (4 llamadas)

Es la fase más chica y la única con impacto directo en FPS. Va primero para
tener una medición limpia, antes de que el resto del refactor ensucie el número.

**Causa raíz:** el bloque resuelve `GameObject.Find("Canvas")` y después
`raiz.transform.Find("Crosshair")` / `"PromptText"` dentro del ciclo por frame,
con guardas `if (mira == null)`. Mientras el HUD no exista (o si alguien
renombró el canvas), son dos barridos completos de escena por frame, para
siempre.

**Implementación:**
1. Mover la resolución completa a un `Bootstrap()` perezoso con el patrón que ya
   usan `Soldier` y `AiBrain`: `if (bootstrapped) return; bootstrapped = true;`.
2. Guardar `raizHud`, `mira` y `cartel` en campos. El `Update` solo los usa.
3. Si el bootstrap no encuentra el canvas, **no reintentar en silencio**:
   `GameLog.Line` una vez y `enabled = false`. Un HUD que no encontró su canvas
   está roto, y gastar un barrido por frame no lo va a arreglar.

**Verificación:** `BalanceBench` / `RunPerformanceBenchmarks`. El bloque de UI
tiene que bajar de forma medible con el HUD presente, y **muchísimo** en el caso
sin HUD (escena de test sin canvas).

**Riesgo:** bajo. El único efecto es que el HUD deja de auto-repararse si el
canvas aparece tarde. Si algún flujo depende de eso (el menú de órdenes se arma
en `GameplaySceneBootstrap.Start`), hay que asegurar que `HudPulido` bootstrapee
**después**, no antes.

---

## Fase 2 — Los servicios únicos: 50 llamadas → 0

El grupo más grande y el de mejor relación esfuerzo/resultado. Son búsquedas de
objetos de los que **existe uno solo** en la escena.

Distribución real de `FindAny/FirstObjectByType<T>`:

| Tipo buscado | Llamadas | Quién lo busca |
|---|---|---|
| `PlayerInputDriver` | 21 | `MisionDirector` (3), `MisionHud`, `CajaDeSuministros`, `Demolicion`, `MunicionPickup`, `Rehen`, `AliadosSinEstorbo`, `OffscreenAllyMarkerView` (2), `MenuDeOrdenes`, `GameplaySceneBootstrap`, `PauseController`, `RosterView`, `AnuncioDeZonas`, `TutorialAutoPlayer` (3), `TutorialManager`, `VictoriaTutorial`, `PlayerInputDriver.Escuadra` |
| `PlayerBrain` | 5 | `RosterRowView`, `SelectedSoldierUI`, `DamageDirectionView`, `DamageVignetteView`, `LowHealthPulseView` |
| `ProjectilePool` | 4 | `TurretWeapon`, `Helicoptero`, `MisionDirector`, `CinematicaDeVictoria` |
| `GameOutcomeController` | 2 | `MisionDirector`, `PauseController` |
| `MisionDirector` | 2 | `CajaDeSuministros`, `GameplaySceneBootstrap` |
| `MenuDeOrdenes` | 2 | `HudPulido`, `MenuDeOrdenes` (chequeo de duplicado) |
| Resto (1 c/u) | 14 | `RosterView`, `MinimapFollow`, `HudPulido`, `ModoDiosView`, `MenuAmbiente`, `EntityStateDebugView`, `WorldUiDirector`, `WeaponStatusView`, `AudioListener`, `AjustesDeEscuadra`, `AnuncioDeZonas`, `CajaDeSuministros`, `Canvas` |

**Patrón a aplicar** — el mismo espíritu que `CamaraPrincipal`, pero con
registro activo en vez de búsqueda perezosa:

```csharp
public class PlayerInputDriver : MonoBehaviour
{
    public static PlayerInputDriver Activo { get; private set; }

    void Awake()
    {
        // Si ya hay uno, el nuevo no pisa al viejo en silencio: eso enmascara
        // escenas con dos drivers, que es un bug, no una configuración.
        if (Activo != null && Activo != this)
            GameLog.Line($"[AVISO] Segundo PlayerInputDriver en la escena: {name}");
        Activo = this;
        ...
    }

    void OnDestroy()
    {
        if (Activo == this) Activo = null;
    }
}
```

**Orden de implementación** (uno por commit, para poder bisecar):

1. `PlayerInputDriver.Activo` — 21 llamadas, el grueso.
2. `PlayerBrain.Activo` — 5 llamadas, todas de vistas de UI.
3. `ProjectilePool.Activa` — 4 llamadas. **Ojo:** `Combat/Projectile.cs:712` ya
   tiene un comentario que dice que ahí se usa `Instance` y **no**
   `FindAnyObjectByType` *porque `Explode` corre muy seguido*. El patrón ya
   existe para este tipo; hay que verificar si ya hay propiedad estática y las 4
   llamadas simplemente no la usan.
4. `GameOutcomeController.Activo`, `MisionDirector.Activo`, `MenuDeOrdenes.Activo`.
5. El resto, caso por caso.

**Punto crítico — `ReinicioDeEstaticos`:** cada `Activo` nuevo es un estático que
sobrevive entre corridas con "Enter Play Mode sin recarga de dominio".
`Core/ReinicioDeEstaticos.cs` existe exactamente por eso y ya lista 4 campos.
**Cada propiedad estática que agregue esta fase tiene que sumar su línea a
`Restablecer()`**, o van a aparecer falsos fallos en la suite que cuestan horas
de encontrar. Esto no es opcional.

**Verificación:**
- Un `Check` por servicio: `PlayerInputDriver.Activo != null` tras cargar
  `SC_Gameplay`, y `== null` tras `ReinicioDeEstaticos.Restablecer()`.
- Un `Check` que corra dos escenarios seguidos en la misma sesión de Edit mode y
  confirme que el segundo no ve el `Activo` del primero.

**Riesgo — el real, no el teórico:**
- **Orden de `Awake`.** Unity no garantiza el orden entre componentes. Si
  `RosterRowView.Awake` lee `PlayerBrain.Activo` y `PlayerBrain.Awake` todavía no
  corrió, queda `null`. **Mitigación:** los consumidores leen el `Activo` en
  `Start` o en su `Bootstrap()` perezoso (patrón ya establecido), nunca en
  `Awake`.
- **Objetos desactivados.** `Awake` no corre en un GameObject que arranca
  desactivado. Es el mismo problema que documenta `ActorRegistry.cs:52-58`. Si
  alguno de estos servicios puede arrancar apagado, necesita registro en
  `OnEnable` además de `Awake`, o quedarse con un barrido acotado.

---

## Fase 3 — Los barridos de tipo: 35 `FindObjectsByType` → registros

### 3a. Los que ya tienen lista propia y no la usan (arreglo trivial)

Estos tipos **ya mantienen una lista estática**; la búsqueda es redundante:

| Llamada | Reemplazo directo |
|---|---|
| `Presentation/ImpactFx.cs:162` → `FindObjectsByType<ImpactFx>` | su propia lista de pool |
| `Presentation/OrderMarkerFx.cs:93` → `FindObjectsByType<OrderMarkerFx>` | su propia lista de pool |
| `Presentation/DebrisPool.cs:70` → `FindObjectsByType<Debris>` | la lista interna del pool |
| `Player/OrderService.cs:98` → `FindObjectsByType<Vehicle>` | `WorldSystemsRegistry.Vehicles` |
| `Presentation/MinimapIcon.cs:266` → `FindObjectsByType<ObstacleMarker>` | `WorldSystemsRegistry.Obstacles` |
| `Vehicles/TorretaFija.cs:171` → `FindObjectsByType<Transform>` | `TorretaFija.Todas` (**ya existe y nadie la consume**) |

Sobre `TorretaFija.cs:171`: barre **todos los `Transform` de la escena** para
encontrar emplazamientos. En `SC_Gameplay` (2 MB) son miles de objetos. Es de los
peores del proyecto y se arregla usando una lista que ya está escrita.

### 3b. `WorldUiDirector`: 5 barridos seguidos (`WorldUiDirector.cs:328-336`)

Busca `HealthBarView`, `MinimapIcon`, `PossessedMarkerView`, `UnitLabelView` y
`RevivePromptView` — cinco barridos completos en una sola función.

**Implementación:** un `VistasDeMundoRegistry` estático en `Core/`, calcado de
`WorldSystemsRegistry` (mismo `Register`/`Unregister`/`Clear`/`EnsurePopulated`).
Cada una de las 5 vistas se registra en `OnEnable` y se da de baja en
`OnDisable`. `WorldUiDirector` recorre las 5 listas.

**Dato a favor:** `GameplaySceneBootstrap.Start` ya llama
`UnitLabelView.RegistrarTodas()` y `RevivePromptView.RegistrarTodas()`. Esas
funciones ya son el punto de alta natural; solo tienen que poblar el registro.

### 3c. Los tipos genéricos (los más caros, tratamiento aparte)

| Archivo | Busca | Qué hacer |
|---|---|---|
| `Presentation/DecalPool.cs:72` | todos los `Transform` | Lista propia del pool. |
| `Core/Coberturas.cs:46`, `Core/NavService.cs:217` | todos los `Collider` | Corren en arranque/rehorneado. **Dejar, con lista blanca.** Un registro de colliders sería peor que la enfermedad. |
| `Core/Loc.cs:161` | todos los `Text` | Corre al cambiar idioma. Acotar a `GetComponentsInChildren<Text>(true)` desde las raíces de canvas registradas. |
| `CinematicaDeVictoria` (2), `MenuDeOrdenes:464`, `AjustesDeJuego:39` | `Canvas` / `CanvasScaler` | Un registro chico de canvases (`HudRefs`, ver Fase 4b). |
| `Core/MetricasDeBuild.cs:88-91` | `Canvas`, `Graphic`, `AudioSource` | **Dejar.** Es diagnóstico a pedido, no corre en partida. Lista blanca. |
| `Core/ApoyoEnElPiso.cs:56,61` | `Soldier`, `Vehicle` | `ActorRegistry.All` + `WorldSystemsRegistry.Vehicles`. **Cuidado:** usa `FindObjectsInactive.Include` a propósito; los registros tienen que cubrir inactivos (`ActorRegistry.Rebarrer` ya lo hace). |
| `Presentation/LimpiezaDeEscena.cs:76,95` | `Resources.FindObjectsOfTypeAll<Transform>` | **Dejar.** Utilidad de limpieza, corre una vez. Lista blanca con comentario. |
| `Presentation/UnitLabelView.cs:175-179`, `RevivePromptView.cs:128` | `Soldier`, `Vehicle`, `ObstacleMarker` | Los registros de 3b + `ActorRegistry.All`. |

---

## Fase 4 — Los 31 `GameObject.Find("nombre")`

### 4a. Tutorial: 17 llamadas (el bloque más grande y el más frágil)

**Archivos:** `Tutorial/TutorialAutoPlayer.cs` (5), `.Entrada.cs` (5),
`.Radial.cs` (7), `TutorialManager.cs` (2).

Buscan `Tut_Enemigo_Estatico` (4 veces), `Tut_Pared` (2), `Tut_Destruible` (2),
`Tut_Enemigo_Ataque` (2), `Tut_Cobertura_1` (2), `Tut_ZonaA`, `Tut_ZonaB`,
`Tut_Meta`, `Tut_MuroDemolible`, `Tut_TorretaFija`, `Tut_Enemigo_Cuchillo`,
`Tut_Enemigo_Granada`.

**Lo que hace que esto sea fácil:** `Editor/TutorialSceneBuilder.cs` **crea esos
objetos** (`caja.name = "Tut_Destruible"` en :296, `dummy.name =
"Tut_Enemigo_Estatico"` en :363) y guarda la escena con
`EditorSceneManager.SaveScene` desde el menú
*Strategic Point > Tutorial > Construir escena SC_Tutorial*. El builder tiene las
referencias en la mano en el momento de crearlas.

**Implementación:**
1. Un `MonoBehaviour` nuevo `Tutorial/CatalogoDelTutorial.cs` con campos públicos
   serializados: `EnemigoEstatico`, `Pared`, `Destruible`, `ZonaA`, `ZonaB`,
   `Meta`, `MuroDemolible`, `TorretaFija`, `EnemigoCuchillo`, `EnemigoGranada`,
   `EnemigoAtaque`, `Cobertura1`.
2. `TutorialSceneBuilder` crea el catálogo y **asigna cada campo al crear el
   objeto**, no buscándolo después.
3. Los tres `TutorialAutoPlayer.*` y `TutorialManager` leen del catálogo.
4. **El catálogo valida al arrancar**: si un campo quedó en `null`, `GameLog.Line`
   con el nombre del campo. Hoy un renombre rompe el tutorial en silencio; con
   esto grita.
5. Regenerar `SC_Tutorial` con el menú y commitear la escena.

**Verificación:** un `Check` que recorra los campos del catálogo por reflexión y
falle si alguno es `null`. Eso sustituye 17 fallos silenciosos por un fallo
ruidoso. Después, la corrida completa del tutorial
(`Docs/Tutorial_Log_corrida_final.txt` como referencia) tiene que dar los mismos
pasos.

**Riesgo:** medio, y concentrado en un punto: **`SC_Tutorial.unity` es binaria**
(`m_SerializationMode: 2`, 640 KB). No se puede editar ni revisar en diff. Todo
cambio de referencias pasa sí o sí por el builder + regenerar + commitear el
binario. Conviene hacer esta fase en un commit propio y limpio.

### 4b. El HUD: `"Canvas"` (2), `"UI_Canvas/Canvas"`, `"TurretRadiusRing"`

**Archivos:** `HudPulido.cs:44,52` (ya resueltos en Fase 1),
`TutorialManager.cs:118`, `TurretAimView.cs:58`.

**Implementación:** un `HudRefs`, o extender `GameplaySceneBootstrap` que ya
tiene campos públicos (`ObjectiveBanner`, `ModeToast`), con la raíz del canvas y
los pocos objetos globales de HUD. Un solo punto de verdad, que además sirve a la
Fase 3c (los barridos de `Canvas`).

### 4c. Raíces de escena: `"Enemies"`, `"Waypoints"`

**Archivo:** `Mision/MisionDirector.cs:83,163`.

Dos campos serializados en `MisionDirector`, asignados por
`Editor/MisionBuilder.cs`. Si el builder no los crea, se asignan a mano en el
Inspector — son dos.

---

## Fase 5 — Los 102 `transform.Find("hijo")`

Es la fase más grande en cantidad y la de **menor ganancia de rendimiento**:
`transform.Find` busca entre hijos directos, no barre la escena. El motivo para
hacerla es la fragilidad, no el costo. **Va última a propósito.**

Ranking: `PauseController` (21), `VehicleStatusView` (14), `WeaponStatusView` (6),
`AimUI` (6), `GameOutcomeController` (6), `TurretAimView` (3), `AjustesDeJuego` (3),
`MainMenuController` (3), y unas 40 más repartidas de a una o dos.

**El patrón ya está inventado en el proyecto:** hay **24 métodos `Bind(...)`
públicos** en las vistas (`PauseController.Bind`, `AimUI.Bind`,
`WeaponStatusView.Bind`, `TurretAimView.Bind`, `VehicleStatusView.Bind`…). El
`transform.Find` de `OnEnable` es el **fallback** para cuando nadie llamó a
`Bind`. Ver `PauseController.cs:50` y `:64-92`: `Bind()` recibe los paneles, y
`OnEnable` los busca solo `if (pausePanel == null)`.

O sea: el trabajo no es diseñar nada, es **cerrar el camino del fallback**.

**Implementación, por subtipo:**

1. **Fallback puro** (la mayoría: `PlayerHealthView`, `RosterRowView`,
   `ScreenFlashView`, `PerfHudView`, `OffscreenKillMarkerView`,
   `LowHealthPulseView`, `DamageDirectionView`…).
   → Asegurar que el constructor de la escena llame a `Bind()` y **borrar el
   bloque `transform.Find`**. Si `Bind` no se llamó: `GameLog.Line` y
   `enabled = false`, no búsqueda silenciosa.

2. **Re-resolución repetida** (los que duelen de verdad):
   - `WeaponStatusView.cs:98` hace `transform.Find(IconName)` **dos veces en la
     misma línea**, dentro de `UpdateFrom`, que corre seguido.
   - `VehicleStatusView.cs:114-126` resuelve `Row_N`, `Icon`, `IconLabel`,
     `NameLabel`, `HealthBG/HealthFill` **dentro de un bucle de filas**.
   → Cachear en un array de structs de fila, resuelto una vez.
   `SelectedSoldierUI.cs:211` ya hace exactamente eso (`Label = child.Find(...)`
   guardado en struct); es el modelo a copiar.

3. **Crear-si-no-existe** (`WeaponStatusView.EnsureIcon`, `PathPreview:48`,
   `EnemyAlertIndicatorView:53`, `SquadStateIndicatorView:70`, `FondoOpaco:90`).
   → Son idempotentes a propósito y el `Find` es su chequeo de idempotencia.
   **Guardar el campo al crear** y usar `if (campo != null) return campo;` como
   única guarda. `EnsureIcon` ya empieza con `if (icon != null) return icon;` —
   solo falta que el `Find` posterior desaparezca una vez que el campo persiste.

4. **`PauseController` (21) — tratar aparte.**
   Son casi todas `settingsPanel.transform.Find("Volumen_Slider")`,
   `"Sensibilidad de mouse_Value"`, etc. Buscar controles **por un string que
   incluye la etiqueta visible en español** es la peor variante: cambiar un texto
   de UI rompe el control. Corren en `OnEnable`, no por frame.
   → Una tabla de filas (`nombreDeControl` → `Slider`/`Text`) armada una vez en
   `Bind`, o un componente `FilaDeAjuste` en cada fila que se auto-registre. Ya
   hay un helper escrito (`PauseController.cs:254-255`) que arma el par
   `_Value`/`_Slider`: puede pasar a devolver la fila cacheada.

**Verificación:** `Editor/HeadlessTestRunner.cs` **ya construye toda esta
jerarquía de UI** (`new GameObject("Crosshair")` en :2771, `"PromptText"` :2779,
`"BarBG"`/`"BarFill"` :2919-2980, `"PausePanel"` :3763). Es el lugar exacto donde
agregar la llamada a `Bind()` y donde un `Check` puede confirmar que cada vista
quedó con sus referencias no nulas **sin haber usado `Find`**.

**Riesgo:** el más alto del plan, por volumen. 102 sitios en unos 40 archivos, y
el modo de falla es "el control deja de responder", que no tira excepción.
Mitigación: ir vista por vista, un commit por vista, y apoyarse en las capturas
que ya están en `DemoCaptures/` y `ClaudeCaptures/` para comparar el HUD antes y
después.

---

## Fase 6 — Cierre

1. Bajar el presupuesto de la Fase 0 a la lista blanca final (estimado: **6
   archivos, ~10 llamadas legítimas**).
2. Convertir el `Check` de presupuesto en un `Check` de lista blanca estricta:
   cualquier `Find` global fuera de esos 6 archivos falla la suite.
3. Los 4 `Shader.Find` (`PlayerInputDriver:196`, `MiraOptica:85`,
   `SafeMaterial:61,62`) **se dejan**: `SafeMaterial` es justamente el cache de
   materiales del proyecto y sus dos `Shader.Find` son el fallback de shader no
   soportado. Documentar el porqué en la lista blanca.

---

## Resumen de ejecución

| Fase | Qué elimina | Llamadas | Esfuerzo | Ganancia |
|---|---|---|---|---|
| 0 | — (red de seguridad) | 0 | Baja | Evita la regresión |
| 1 | `HudPulido` por frame | 4 | Baja | **FPS real** |
| 2 | Servicios únicos | 50 | Media | Carga de escena + robustez |
| 3 | Barridos de tipo | ~28 de 35 | Media | Carga de escena |
| 4 | `GameObject.Find` por nombre | 31 | Media-alta | **Robustez** (fallos ruidosos) |
| 5 | `transform.Find` | ~95 de 102 | **Alta** | Robustez |
| 6 | Cierre y lista blanca | — | Baja | Permanencia |

**Queda deliberadamente sin tocar:** ~10 llamadas en `ActorRegistry`,
`WorldSystemsRegistry`, `Coberturas`, `NavService`, `MetricasDeBuild`,
`LimpiezaDeEscena`, más los 4 `Shader.Find`. En todos esos casos el barrido es el
mecanismo correcto y reemplazarlo introduce más riesgo del que saca.

**Orden sugerido de commits:** 0 → 1 → 2 (uno por servicio) → 3a → 3b → 3c → 4a →
4b/4c → 5 (una vista por commit) → 6.

**Dependencias duras:**
- La Fase 2 no se cierra sin actualizar `Core/ReinicioDeEstaticos.cs`.
- La Fase 4a no se cierra sin regenerar y commitear `SC_Tutorial.unity`.

---

## Resultado de la ejecución (2026-09-21)

Las fases 0–6 están hechas y la suite headless las custodia (`HeadlessTestRunner.BusquedasGlobales.cs`).

| Métrica | Antes | Después |
|---|---|---|
| `Find*` globales de escena fuera de la lista blanca | 107 | **0** (presupuesto 0) |
| Reintentos `if (x == null) x = Find…` por frame | 21 | **0** |
| `transform.Find` dentro de métodos por frame (`Update`, `UpdateFrom`, `UpdateInVehicle`…) | 3 | **0** (check propio) |
| `transform.Find` por nombre en total | ~102 | 115 contados por el escáner (tope 115, solo puede bajar) |

**Verificación:** suite headless OK, humo en Play (gameplay, tutorial y menú principal sin errores) y autoplay del
tutorial completo (35 OK, 1 saltado como en la corrida de referencia, 0 errores).

### Desviaciones respecto del plan (honestas)

1. **La lista blanca es de 18 archivos, no de 6.** El plan subestimó dos cosas: los pools (`ImpactFx`, `OrderMarkerFx`,
   `DebrisPool`, `DecalPool`) y `TorretaFija.Todas` **no sustituyen** al barrido (limpian huérfanos tras Enter Play Mode
   sin recarga de dominio y encuentran arte sin componente), y `Loc` / `FuentesBelicas` / `WorldUiDirector` / `MenuDeOrdenes`
   / `AjustesDeJuego` / `CinematicaDeVictoria` barren `Text` o canvas sin que exista un registro. Cada entrada de la lista
   lleva su motivo y su cupo máximo.
2. **Trabajo futuro:** `Loc.Recorrer` (0,5 s en inglés) y `FuentesBelicas.Aplicar` (0,25 s) siguen siendo pasadas periódicas
   sobre todos los `Text`. Un registro de `Text` las eliminaría.
3. **F5 (`transform.Find`) se cerró por el lado de lo que importa**: se quitaron los que corrían por frame
   (`WeaponStatusView.UpdateFrom`, y `UpdateInVehicle`, que hacía dos por frame: ahora `Vehicle.TorretaCanon` /
   `TorretaMetralleta` se resuelven una vez). Los ~115 restantes son cableado de arranque (`OnEnable`/`Awake`, guardados por
   `== null`) o "crear si no existe" que guarda el resultado en un campo. **No se eliminaron**: `VehicleStatusView` documenta
   que sus campos no serializados se perdían al recargar la escena y el fallback por nombre es lo que lo arregla, y
   `PauseController` conecta botones por nombre porque los `onClick` del Editor no sobreviven a Play. Quitarlos exigiría
   serializar esos campos en escenas binarias: más riesgo que ganancia. El escáner impide que suban o que vuelvan a un método
   por frame.
4. `MisionDirector` usa `RaicesDeEscena.Buscar` (barre solo las raíces de escena) en lugar de campos serializados.
5. El tutorial tiene `CatalogoDelTutorial` solo para los 6 objetos horneados en la escena; los que se crean en runtime salen de
   los accesores de `TutorialManager`.
6. Los 4 `Shader.Find` se dejan a propósito (fallback de shader no soportado en `SafeMaterial`, y los de `PlayerInputDriver`/`MiraOptica`).
