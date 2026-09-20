# Ronda 10 — Plan de implementación de los 100 bugs

Plan de corrección de los 100 bugs de [`RONDA_10_BUGS.md`](RONDA_10_BUGS.md). Mismo número de bug en
los dos documentos.

Para cada bug hay **Arreglo** (pasos concretos, con nombres reales del proyecto) y **Verificación**
(qué `Check()` agregar a `HeadlessTestRunner` o, cuando no es práctico, qué correr en Play y qué
mirar). Se respetan las convenciones ya vigentes: `Bootstrap()` perezoso, `EventBus` con `IDisposable`
en campo liberado en `OnDestroy`, pools con presupuesto fijo y `HideFlags`, `SafeMaterial.Create`,
`MaterialPropertyBlock` vía `CubeFxReactor.WriteTint`, acumuladores de `dt` en vez de `Time.time`, y
`WorldSimulationDriver.Step` como único camino de simulación.

---

## 1. Orden de ejecución: 8 olas

El orden no es por severidad: es por **dependencia**. Arreglar el bug 57 antes que el 35 hace que el 35
se pueda medir; arreglar el 1 antes que nada hace que el resto de las mediciones sean repetibles.

| Ola | Tema | Bugs | Por qué va acá | Riesgo |
|---|---|---|---|---|
| **0** | Cimientos de medición | 1, 8, 9, 10, 47, 55, 70, 90, 100 | Sin reset de estáticos limpio y sin desempate determinista, ninguna medición posterior es repetible. Es la ola que hace que las otras siete se puedan verificar. | Bajo |
| **1** | Abrir la cobertura | 57, 6, 40, 27, 51, 5 | Hoy el A*, la supresión, las granadas y el salto **no se ejercitan** en la suite. Hasta que corran ahí, los arreglos de las olas siguientes no tienen cómo probarse. | Medio |
| **2** | Reglas de juego rotas | 13/37, 14, 22, 35, 69, 71, 4, 29, 31, 34 | Once de los catorce P0: los que cambian el resultado de una partida. Van juntos porque varios se tocan (13↔37, 29↔69). Los tres P0 restantes (1 en la ola 0, 49 en la 4, 86 en la 6) van con su subsistema. | Alto |
| **3** | Máquina de estados de la IA | 36, 37, 41, 42, 43, 44, 45, 46, 38, 39 | Todo lo que vive dentro de `AiBrain`. Se hace de una para no tocar el mismo `Tick` cuatro veces. | Alto |
| **4** | Motor, física y navegación | 48, 49, 50, 52, 53, 54, 56, 58, 59, 60, 28, 23 | Movimiento, colisión y grilla. Cambia sensación de juego: se mide con el banco de balance antes y después. | Medio |
| **5** | Órdenes y entrada | 61–68, 72–84 | La capa que traduce intención en órdenes. Muchos son de una línea; el valor está en hacerlos juntos y cerrar con una sola pasada de pruebas de UI. | Bajo-Medio |
| **6** | Vehículos y torretas | 85–93 | Subsistema aislado; se prueba con la fase de vehículos de la suite y una corrida del autoplayer. | Medio |
| **7** | Materiales, UI y limpieza | 15, 98, 99, 94, 95, 96, 97, 12, 11, 16–21, 24, 25, 26, 30, 32, 33 | Fugas, materiales compartidos, casos de borde y deuda. Sin riesgo de regresión de juego. | Bajo |

**Regla de cierre de cada ola:** `Strategic Point > Run All Tests Headless` en verde +
`Tools/Qa/qa_total.bat --rapido` sin pasos fallidos + `Tools/Ci/verificar_estatico.py` en verde.
Ninguna ola se da por terminada con la siguiente ya empezada.

---

# OLA 0 — Cimientos de medición

### Bug 1 · `ReinicioDeEstaticos` incompleto
**Arreglo**
1. Darle a cada sistema con estado estático su propio `RestablecerEstaticos()` público, en su propio
   archivo (el dueño del estado es quien sabe qué resetear). Nuevos métodos:
   `SpatialGrid.Restablecer()` (vacía `cells`, `built = false`),
   `WorldSystemsRegistry.Clear()` (ya existe),
   `ActorRegistry.Clear()` (ya existe),
   `Soldier.ResetIdCounterForTests()` (renombrar a `ResetIdCounter()` y dejar el viejo como alias
   `[System.Obsolete]`),
   `Projectile.ResetInstanceIdCounter()`,
   `WorldSimulationDriver.ResetTickCounter()`,
   `RescateAutomatico.Cancelar()` (ya existe),
   `AiBrain.ReiniciarRelojesDeAviso()` (nuevo: `proximoAvisoRadio`, `ultimoAvisoDeSeguir`,
   `ultimoAvisoDeteccion`) y `AiBrain.ReiniciarContadoresGranadaIA()` (ya existe).
2. `ReinicioDeEstaticos.Restablecer()` los llama a todos, en este orden: primero los registros
   (`ActorRegistry`, `WorldSystemsRegistry`, `SpatialGrid`), después `EventBus.Instance.ClearAll()`,
   después los contadores, y al final los flags de juego que ya tenía.
3. `OrderService.ManejadoAMano = null;` y `AjustesDeEscuadra.Lider = null;` entran en la misma lista.
4. Para que no vuelva a quedarse corta: agregar a `Tools/Ci/verificar_estatico.py` una comprobación que
   liste todos los `static` mutables no-`readonly` fuera de `Editor/` y falle si alguno no aparece
   mencionado ni en `ReinicioDeEstaticos.cs` ni en un `[RuntimeInitializeOnLoadMethod]` propio.

**Verificación** — `HeadlessTestRunner`, fase nueva `FaseReinicio`:
```csharp
// Ensuciar todo a propósito, restablecer, y comprobar que quedó limpio.
Soldier.ResetIdCounter();
CrearSoldado(...);                       // consume un Id, registra, suscribe
SP.Core.ReinicioDeEstaticos.Restablecer();
Check("ActorRegistry vacio tras el reinicio", ActorRegistry.All.Count == 0);
Check("SpatialGrid sin celdas tras el reinicio", SpatialGrid.CellCount == 0);
Check("Id vuelve a 1 tras el reinicio", CrearSoldado(...).Id == 1);
Check("Lider suelto tras el reinicio", AjustesDeEscuadra.Lider == null);
Check("ManejadoAMano suelto tras el reinicio", OrderService.ManejadoAMano == null);
```
Y la prueba de verdad: `RunMany(100)` tiene que dar 100 iteraciones idénticas. Hoy no da.

**Riesgo:** `EventBus.ClearAll()` en `SubsystemRegistration` corre **antes** de que carguen las escenas,
así que no hay suscriptor legítimo que perder. Verificar que ningún `[RuntimeInitializeOnLoadMethod]`
de otro sistema se suscriba en `SubsystemRegistration` (hoy ninguno lo hace).

---

### Bug 9 · La grilla espacial no se invalida al cambiar de escena
**Arreglo** — copiar el patrón de `NavService.Reset()` (`Core/NavService.cs:115-128`):
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void Reset()
{
    Restablecer();
    SceneManager.sceneLoaded -= AlCargarEscena;
    SceneManager.sceneLoaded += AlCargarEscena;
}
static void AlCargarEscena(Scene _, LoadSceneMode __) => Restablecer();

public static void Restablecer() { cells.Clear(); built = false; }
```
**Verificación:** `Check("grilla vacia tras cargar escena", SpatialGrid.CellCount == 0)` justo después
de `EditorSceneManager.OpenScene` en la fase que cambia de escena.

---

### Bug 8 · Desempate no determinista en `SpatialGrid.FindNearestInRange`
**Arreglo** — una sola letra, pero hay que elegirla a conciencia. El contrato correcto es el del
método que esta reemplazó (`ActorRegistry.FindNearest`, `Core/ActorRegistry.cs:137`): **gana el
primero**, no el último.
```csharp
if (sqr < bestSqr) { bestSqr = sqr; best = s; }   // era <=
```
Con `<` estricto, un empate exacto lo gana el primer candidato recorrido — que sigue dependiendo del
orden del diccionario. Para que sea **de verdad** determinista hace falta un criterio estable que no
dependa de la estructura de datos: desempatar por `Id`.
```csharp
if (sqr < bestSqr || (sqr == bestSqr && best != null && s.Id < best.Id))
{
    bestSqr = sqr; best = s;
}
```
El `Id` es único, estable dentro de una partida y ya se usa como criterio determinista en otros tres
lugares del proyecto (el desfasaje de sensado, el de LOD y las ranuras de formación).

**Verificación** — el caso que hoy no se puede escribir:
```csharp
// Dos enemigos EXACTAMENTE a la misma distancia, uno a cada lado.
var a = CrearEnemigo(centro + Vector3.left * 5f);
var b = CrearEnemigo(centro + Vector3.right * 5f);
var primero = SpatialGrid.FindNearestInRange(centro, 10f, s => true);
for (int i = 0; i < 20; i++)
{
    SpatialGrid.Rebuild();
    Check($"el empate se resuelve igual siempre (vuelta {i})",
          SpatialGrid.FindNearestInRange(centro, 10f, s => true) == primero);
}
Check("y gana el de Id mas bajo", primero == (a.Id < b.Id ? a : b));
```
Y, transversalmente: `RunEquivalenceCheck` (que compara la grilla contra el barrido lineal) tiene que
seguir dando `LastEquivalenceMismatches == 0` **después** del cambio. Si el barrido lineal y la grilla
desempatan distinto, el que hay que alinear es el barrido.

---

### Bug 10 · Los predicados de la grilla derreferencian `Health` sin comprobarlo
**Arreglo** — el contrato del predicado tiene que ser único y estar en un solo lugar. Hoy hay tres
versiones distintas del mismo filtro (`ActorRegistry.cs:151`, `Projectile.cs:749`,
`AiBrain.Granadas.cs:135`) y sólo una comprueba `Health != null`.
1. Que `SpatialGrid` garantice la precondición en vez de confiar en cada llamador. En
   `QueryInRange` y en `FindNearestInRange`, la guarda pasa a ser:
   ```csharp
   if (s == null || s.Health == null || !predicate(s)) continue;
   ```
   Una comparación de referencia por candidato, y ningún predicado futuro puede volver a olvidarse.
2. Con eso, los tres predicados quedan más cortos y dejan de repetir la misma guarda. El de
   `Granadas.cs:135` pierde además el `s != null` redundante.

**Verificación:**
```csharp
var roto = CrearSoldado(...);
Object.DestroyImmediate(roto.GetComponent<Health>());   // soldado a medio construir
ActorRegistry.Register(roto);
SpatialGrid.Rebuild();
var lista = new List<Soldier>();
SpatialGrid.QueryInRange(roto.transform.position, 10f, lista, s => s.Health.IsAlive);
Check("un soldado sin Health no revienta la consulta", true);   // llegar aca ya es la asercion
Check("y no aparece en el resultado", !lista.Contains(roto));
```
Sin el arreglo, esa fase lanza `NullReferenceException` y aborta el tick de **todos** los soldados de
ese frame, porque `WorldSimulationDriver.Step` recorre sin `try/catch`.

---

### Bug 55 · `Soldier.nextId` sin reset · Bug 70 · estáticos del rescate · Bug 90 · registro doble de vehículos
**Arreglo:** los tres se resuelven al entrar en la lista del bug 1. Además:
- Bug 90: borrar `Vehicle.Todos` por completo y que `TurretAI` (su único consumidor real) lea
  `WorldSystemsRegistry.Vehicles`. Dos registros paralelos del mismo tipo es la causa; el arreglo es
  que quede uno. `Vehicle.OnEnable/OnDisable` pasan a registrar sólo en `WorldSystemsRegistry`.
**Verificación:** `Check("un solo registro de vehiculos", WorldSystemsRegistry.Vehicles.Count == N)`
tras construir la escena de vehículos, y buscar `Vehicle.Todos` en el repo: cero referencias.

---

### Bug 100 · Contadores estáticos sin reset entre iteraciones
**Arreglo**
1. Agregar `Restablecer()` a `Feedback`, `CuchilloFx`, `SelectionRingFx`, `AudioDirector`, `Subtitulos`
   (y usar el `ReiniciarContadoresGranadaIA` que ya existe).
2. Llamarlos desde `ReinicioDeEstaticos.Restablecer()` (bug 1) **y** desde el arranque de cada escenario
   de `HeadlessTestRunner` (el método que hoy limpia la escena entre fases).
**Verificación:** `RunMany(100)` con una fase que afirme `CuchilloFx.Aciertos == 1` después de una sola
cuchillada: hoy pasa 1 vez de 100; después tiene que pasar 100 de 100.

---

### Bug 47 · Suscripciones de `AiBrain` acumuladas entre corridas
**Arreglo**
1. `AiBrain.Bootstrap()`: antes de suscribir, liberar lo que hubiera
   (`damageSub?.Dispose(); shotSub?.Dispose();`). Barato y cierra el caso de un `Bootstrap` doble.
2. `HeadlessTestRunner`: llamar a `EventBus.Instance.ClearAll()` en la limpieza entre escenarios (ya
   queda cubierto si se enchufa `ReinicioDeEstaticos.Restablecer()` ahí, del bug 1).
**Verificación:** exponer `EventBus.Instance.SubscriberCount<T>()` (sólo para tests) y
`Check("sin suscriptores colgados entre escenarios", bus.SubscriberCount<ShotFiredEvent>() == soldadosVivos)`.

---

# OLA 1 — Abrir la cobertura

Esta ola no arregla comportamiento: **hace visible** el que hoy no se mide. Es la más importante del
plan porque sin ella los arreglos de la ola 2 se hacen a ciegas.

### Bug 57 · El rodeo de obstáculos está apagado fuera de Play
**Arreglo**
1. Borrar `if (!Application.isPlaying) return false;` de `NavService.TryFindDetour`
   (`Core/NavService.cs:199`).
2. `Build()` ya usa `FindObjectsByType`, que funciona en Edit mode. Lo único que hay que asegurar es que
   la suite llame a `NavService.Invalidate()` después de construir cada escenario (hoy lo hace el
   `sceneLoaded`, que en Edit mode no dispara): agregar `NavService.Invalidate()` al final del
   constructor de escena de `HeadlessTestRunner`.
3. Comprobar que `graph.Build` no tarda de más en Edit mode: si el escenario de estrés (50+) lo hace
   pesado, permitir a la suite fijar un `Spacing` mayor con una propiedad `public static float SpacingDeTest`.

**Verificación** — fase nueva `FaseRodeo`:
```csharp
// Muro entre A y B. Sin rodeo el soldado se clava; con rodeo llega.
var soldado = CrearSoldado(new Vector3(0, 0.8f, -10));
CrearMuro(new Vector3(0, 1, 0), new Vector3(20, 2, 1));
soldado.Brain.IssueMoveOrder(new Vector3(0, 0.8f, 10));
Check("planifico un rodeo", soldado.Brain.RemainingPathPoints > 0);
SimulateUntil(() => soldado.Brain.State == AiState.Patrol, 30f);
Check("llego al otro lado del muro", soldado.transform.position.z > 8f);
```
Esta fase, hoy, falla. Ese es el punto.

---

### Bug 6 · `EnsureAllRegistered` con reloj de pared
**Arreglo**
1. Cambiar `proximoBarrido` a un acumulador de `dt`:
   ```csharp
   static float segundosDesdeElBarrido = float.MaxValue;   // el primero corre siempre
   public static void AvanzarReloj(float dt) => segundosDesdeElBarrido += dt;
   public static void EnsureAllRegistered()
   {
       if (segundosDesdeElBarrido < IntervaloDeBarrido) return;
       segundosDesdeElBarrido = 0f;
       Rebarrer();
   }
   ```
2. `WorldSimulationDriver.Step(dt)` llama `ActorRegistry.AvanzarReloj(dt)` como **primera** línea,
   antes de `SpatialGrid.Rebuild()`.
**Verificación:**
```csharp
var oculto = CrearSoldadoDesactivado(...);   // no corre Awake, no se registra solo
Check("todavia invisible", ActorRegistry.FindById(oculto.Id) == null);
SimulateSeconds(0.6f);                        // > IntervaloDeBarrido de tiempo SIMULADO
Check("el barrido simulado lo levanto", ActorRegistry.FindById(oculto.Id) != null);
```

---

### Bug 40 · Granadas, supresión y avisos con `Time.time`
**Arreglo** — convertir los 15 relojes de `AiBrain.Granadas.cs` y `AiBrain.Tactica.cs` a acumuladores de
`dt`, siguiendo el patrón textual de `Health.cs:26-30`. Por campo:

| Campo actual | Reemplazo |
|---|---|
| `proximaGranadaIA` (absoluto) | `segundosHastaLaProximaGranada` (cuenta atrás, `-= dt`) |
| `huyendoHasta` | `segundosHuyendo` (`-= dt`, `HuyendoDeGranada => segundosHuyendo > 0f`) |
| `suprimidoHasta` | `segundosSuprimido` (`-= dt`, `RecibirSupresion` hace `Mathf.Max`) |
| `suprimirOrdenadoHasta` | `segundosSuprimiendoPorOrden` |
| `proximoAvisoRadio` (static) | `segundosHastaElProximoAvisoRadio` (static, decrementado desde `TickGranadas`) |
| `proximoAvisoPropio` | `segundosHastaElProximoAvisoPropio` |
| `ultimoAvisoDeteccion` (static) | `segundosDesdeElUltimoAvisoDeteccion` (static) |
| `tiempoUltimoAtaque` | `segundosDesdeElUltimoAtaque` (crece con `dt`, comparado contra 4 f) |
| `ultimoAvisoDeSeguir` (static) | `segundosDesdeElUltimoAvisoDeSeguir` (static) |

Los estáticos se decrementan una sola vez por tick: agregar al principio de
`WorldSimulationDriver.Step` una llamada `AiBrain.AvanzarRelojesGlobales(dt)`.

**Verificación:**
```csharp
enemigo.Brain.RecibirSupresion(2.5f);
Check("suprimido al recibirla", enemigo.Brain.Suprimido);
SimulateSeconds(3f);
Check("deja de estar suprimido con tiempo SIMULADO", !enemigo.Brain.Suprimido);
// Y el de granadas, que hoy es imposible:
AiBrain.ReiniciarContadoresGranadaIA();
SimulateSeconds(20f);
Check("la IA tiro al menos una granada en 20 s simulados", AiBrain.GranadasLanzadasPorIA > 0);
```

---

### Bug 27 · El fuego de supresión no corre fuera de Play
**Arreglo** — en `Projectile.SuprimirCercanos`, mover la guarda de `Application.isPlaying` para que
cubra **sólo** el audio (que no hay ninguno acá) y dejar el cálculo siempre activo:
```csharp
void SuprimirCercanos(float dt)
{
    segundosHastaLaProximaSupresion -= dt;       // acumulador, no Time.time (bug 26)
    if (segundosHastaLaProximaSupresion > 0f || age < 0.05f) return;
    segundosHastaLaProximaSupresion = 0.08f;
    ...
}
```
**Verificación:** disparar a 1 m de un enemigo con `SimStep` y
`Check("la bala cercana lo agacho", enemigo.Brain.Suprimido && enemigo.Motor.IsCrouching)`.

---

### Bug 51 · Salto y trepa fuera del camino de simulación
**Arreglo**
1. Renombrar `SoldierMotor.Update()` a `public void Tick(float dt)` y mover ahí el cuerpo, cambiando
   los dos `Time.deltaTime` por `dt` (líneas 188, 190, 137).
2. Dejar un `void Update() { if (SP.Ai.WorldSimulationDriver.Instance == null) Tick(Time.deltaTime); }`
   **no** — mejor no dejar doble camino (es exactamente el bug 13). En su lugar:
   `WorldSimulationDriver.Step` llama `s.Motor?.Tick(dtSoldado)` junto a `Brain`, `Weapon` y `Health`.
   Borrar `Update()` del motor.
3. Comprobar el orden: `Motor.Tick` va **después** de `Brain.Tick` (el cerebro pide el movimiento
   horizontal; el motor integra la vertical).
**Verificación:**
```csharp
soldado.Motor.Jump();
Check("esta en el aire", soldado.Motor.IsJumping);
float alturaMax = 0f;
SimulateUntil(() => { alturaMax = Mathf.Max(alturaMax, soldado.transform.position.y); return !soldado.Motor.IsJumping; }, 3f);
Check("aterrizo", !soldado.Motor.IsJumping);
Check($"subio ~0,45 m (subio {alturaMax - y0:0.00})", Mathf.Abs((alturaMax - y0) - 0.45f) < 0.15f);
```
Y la trepa (item 53), que hoy tampoco tiene cobertura: poner un cajón de 1 m delante y comprobar
`Vaulting` y la posición final.

**Riesgo:** el salto pasa a estar sujeto a la pausa igual que antes (`Step` no corre con
`Time.timeScale = 0` porque `Time.deltaTime` es 0), pero ahora también al LOD por distancia: un soldado
lejano saltando integra la parábola a la mitad de frecuencia. Como el LOD sólo se aplica a >120 m de
cámara, no se ve. Documentarlo en el comentario del `Tick`.

---

### Bug 5 · `Step` no puebla el registro de vehículos
**Arreglo** — primera línea de `WorldSimulationDriver.Step`, junto a la del bug 6:
```csharp
ActorRegistry.AvanzarReloj(dt);
WorldSystemsRegistry.EnsurePopulated();
SpatialGrid.Rebuild();
```
`EnsurePopulated` sale por `if (populated) return;` en una comparación, así que el costo por tick es cero.
**Verificación:** `Check("torreta premontada tickea", turretAi.TicksSimulados > 0)` tras
`SimulateSeconds(1f)` en un escenario donde el `TurretAI` arranca desactivado.

---

# OLA 2 — Reglas de juego rotas

### Bug 13 y 37 · Doble tick del arma en combate
**Arreglo** — borrar la línea `self.Weapon.Tick(dt);` de `Ai/AiBrain.cs:1065`. Nada más: el driver ya
lo hace para todos los soldados activos.
**Verificación:**
```csharp
// 10 s de combate sostenido con cadencia 0.35 -> ~28 disparos, no ~57.
int disparos = 0;
using (bus.Subscribe<ShotFiredEvent>(e => { if (e.ShooterId == atacante.Id) disparos++; }))
    SimulateSeconds(10f);
Check($"cadencia real ~ la del catalogo ({disparos} disparos)", disparos >= 25 && disparos <= 31);
```
**Riesgo alto:** esto **baja a la mitad** la cadencia real de toda la IA en combate. Correr
`BalanceBench` antes y después: si el balance del nivel dependía de la cadencia doble, hay que
reajustar `fireCooldown` en `WeaponCatalog` — pero de forma explícita y documentada, no por accidente.

---

### Bug 14 · Cambiar de arma recarga gratis
**Arreglo** — en `WeaponHolder.EquipWeapon`, reemplazar las líneas 261-267 por:
```csharp
if (!cambioDeArma)
{
    // Re-equipar la misma arma no es un cambio: no toca el cargador.
}
else if (UsaReservas)
{
    InicializarReserva(kind);
    int guardado = cargadorPorArma[(int)kind];
    CurrentAmmo = guardado >= 0 ? Mathf.Min(guardado, magazineSize) : magazineSize;
}
else
{
    // Sin reservas la municion es ilimitada por diseno: el cargador arranca lleno.
    CurrentAmmo = magazineSize;
}
```
**Verificación:**
```csharp
w.EquipFromLoadout(0); GastarBalas(w, 5);
int antes = w.CurrentAmmo;
w.EquipFromLoadout(0);                       // la MISMA arma
Check("re-equipar la misma no rellena", w.CurrentAmmo == antes);
w.EquipFromLoadout(1); w.EquipFromLoadout(0);   // ida y vuelta, con reservas
Check("el cargador a medias se conserva", w.CurrentAmmo == antes);
```

---

### Bug 22 · Las balas atraviesan paredes
**Arreglo** — resolver los dos impactos por **distancia**, no por orden de chequeo. En
`Projectile.Tick`, reemplazar el bloque 246-283 por:
```csharp
var posPrevia = ...;   // ya existe
// 1) Candidato soldado, con su distancia al origen del tramo.
var puntoSoldado = transform.position;
var hitSoldado = BuscarBlancoEnElTramo(posPrevia, transform.position, ref puntoSoldado);
float distSoldado = hitSoldado != null ? Vector3.Distance(posPrevia, puntoSoldado) : float.MaxValue;

// 2) Candidato mundo solido, con la suya.
bool hayMundo = BuscarMundoEnElTramo(posPrevia, transform.position, out var impactoMundo);
float distMundo = hayMundo ? impactoMundo.distance : float.MaxValue;

// 3) Gana el mas cercano. Empate a favor del soldado (un enemigo PEGADO a la pared
//    sigue recibiendo el tiro, que es lo que el comentario original queria proteger).
if (distSoldado <= distMundo && hitSoldado != null) { ResolverImpactoSoldado(hitSoldado, puntoSoldado); return; }
if (hayMundo) { ResolverImpactoMundo(impactoMundo); return; }
```
Eso exige partir `ChocoContraElMundo` en dos: `BuscarMundoEnElTramo` (busca y devuelve el hit, sin
efectos) y `ResolverImpactoMundo` (aplica daño/VFX/SFX/`Expire`). El cuerpo se mueve tal cual.

**Verificación** — esta es la que hay que escribir primero, porque el bug se mide:
```csharp
// Enemigo a 2 m DETRAS de un muro, tirador a 3 m delante. Un solo tramo los cubre a los dos.
var enemigo = CrearEnemigo(new Vector3(0, 0.8f, 3));
CrearMuro(new Vector3(0, 1, 1), new Vector3(6, 2, 0.4f));
int vidaAntes = enemigo.Health.Current;
DispararDesde(new Vector3(0, 0.8f, -3), Vector3.forward);
SimulateSeconds(0.2f);
Check("el muro paro la bala", enemigo.Health.Current == vidaAntes);
// Y el caso que NO hay que romper: enemigo pegado al muro, del lado del tirador.
```
**Riesgo:** al arreglarlo, la IA empieza a fallar tiros que antes acertaba a través de coberturas. Eso
es lo correcto, pero `BalanceBench` va a moverse: medir antes/después.

---

### Bug 35 · La detección de atasco no puede dispararse
**Arreglo** — separar el reloj de atasco del de replanificación. `PlanPathTo` **no** debe resetear el
vigilante de atasco; eso lo hace sólo quien empieza una orden nueva.
1. Sacar `ResetStuckWatch();` de `PlanPathTo` (`Navegacion.cs:24`).
2. Llamar `ResetStuckWatch()` explícitamente desde los sitios que sí empiezan un destino nuevo:
   `IssueMoveOrder`, `IssueMountOrder`, `IssueFollowOrder`, `IssueAttackOrder`, `IssueCoverOrder`,
   `CancelOrder` y la transición de salida de `Dead`.
3. En `AvanzarConRodeo`, resetear el vigilante **sólo** cuando el objetivo cambió de verdad
   (`seMovioMucho`), no en el refresco periódico:
   ```csharp
   if (!tieneDestino || seMovioMucho) { ResetStuckWatch(); repathed = false; }
   ```
**Verificación:**
```csharp
// Soldado contra una esquina en U, con orden de seguir a un lider del otro lado.
soldado.Brain.IssueFollowOrder(lider);
SimulateSeconds(3f);
Check("el vigilante de atasco disparo", soldado.Brain.RutaRecalculadaPorAtasco);  // contador nuevo
```
Exponer `public int RecalculosPorAtasco { get; private set; }` en `AiBrain` (se incrementa en las dos
ramas de `TickStuckWatch`/`TickStuckWatchAgent`): es la única forma de verificar esto desde afuera sin
mirar logs.

---

### Bug 69 · El rescatista nunca sale
**Arreglo** — `RescateAutomatico.Solicitar` no debe usar una orden de **seguir** (que exige un líder
vivo) sino una orden de **mover** al punto donde cayó, refrescada mientras dure el rescate:
```csharp
Caido = caido; Rescatista = rescatista;
OrderService.IssueMoveOrder(rescatista, caido.transform.position);
```
y en `Tick`, cuando todavía está lejos, re-emitir la orden si el rescatista la perdió (combate, atasco):
```csharp
if (d > AlcanceDeRevivir)
{
    canalizado = 0f;
    segundosDesdeLaOrden += dt;
    if (segundosDesdeLaOrden >= 2f && Rescatista.Brain.State != AiState.MovingToOrder)
    {
        segundosDesdeLaOrden = 0f;
        OrderService.IssueMoveOrder(Rescatista, Caido.transform.position);
    }
    return;
}
```
El caído no se mueve, así que un punto fijo alcanza y no hace falta tocar `AiBrain.Follow`.

**Verificación:**
```csharp
MatarA(jugador);
Check("hay rescatista asignado", RescateAutomatico.Activo);
SimulateUntil(() => jugador.Health.IsAlive, 25f);
Check("el rescatista llego y revivio al caido", jugador.Health.IsAlive);
Check("y quedo con vida llena", jugador.Health.Current == jugador.Health.MaxHealth);
```
Hoy esa fase se agota en el timeout. Es la prueba de que A5 nunca funcionó.

---

### Bug 71 · El HUD del arma no vuelve tras morir
**Arreglo** — en `UpdateFps`, junto a `AimUiRef.SetVisible(true)`, devolver el resto:
```csharp
if (WeaponStatus != null && !WeaponStatus.gameObject.activeSelf) WeaponStatus.gameObject.SetActive(true);
```
Mejor aún: extraer un `void MostrarHudDeAPie(bool visible)` que encienda/apague **el mismo conjunto**
que apaga `DeathSequence`, y llamarlo `false` al entrar a la cámara de muerte y `true` al principio de
`UpdateFps`. Un solo sitio con la lista, sin posibilidad de olvidarse de uno.
**Verificación (Play):** matar al soldado, esperar el rescate o apretar `[Espacio]`, y confirmar que el
panel de arma vuelve. En la suite: `Check("el HUD de arma vuelve tras morir", WeaponStatus.gameObject.activeSelf)`
tras forzar `DeathSequence` y un `UpdateFps`.

---

### Bug 4 · El montado no regenera ni termina de recargar
**Arreglo** — en `WorldSimulationDriver.Step`, separar lo que necesita el GameObject activo (IA,
movimiento) de lo que no (vida, arma):
```csharp
foreach (var s in ActorRegistry.All)
{
    if (s == null) continue;
    // Estos dos no dependen de estar activo en la escena: un pasajero sigue vivo,
    // sigue sangrando y sigue recargando adentro del tanque.
    s.Health?.Tick(dt);
    s.Weapon?.Tick(dt);
    if (!s.gameObject.activeInHierarchy) continue;
    ... // LOD, Brain.Tick, Motor.Tick
}
```
Ojo: `Health.Tick` y `Weapon.Tick` pasan a recibir `dt` sin LOD. Es correcto: no son visibles, no
tartamudean, y el LOD existe para lo que se ve.
**Verificación:**
```csharp
GastarBalas(pasajero.Weapon, 8); pasajero.Weapon.Reload();
vehiculo.Mount(pasajero);
SimulateSeconds(3f);
Check("termino de recargar adentro del tanque", pasajero.Weapon.CurrentAmmo == pasajero.Weapon.MagazineSize);
pasajero.Health.TakeDamage(30, -1);
SimulateSeconds(8f);
Check("regenero adentro del tanque", pasajero.Health.Current > 70);
```

---

### Bug 29 · Revivir no avisa a nadie
**Arreglo**
1. `Core/Events.cs`: agregar
   `public readonly struct EntityRevivedEvent { public readonly int ActorId; public readonly int Health; ... }`.
2. `Health`: separar los dos usos de `Initialize`:
   - `Initialize(int actorId, int max)` queda como el alta original (silenciosa, se llama en `Bootstrap`).
   - `Revivir()` nuevo: `Current = maxHealth; LastAttackerId = -1; ...; EventBus.Publish(new EntityRevivedEvent(ActorId, Current));`
   - `FijarMaximo(int max)` nuevo, para el bug 54 y el 33: cambia `maxHealth` **sin** tocar `Current`.
3. Cambiar los tres llamadores de revivir a `Revivir()`: `RescateAutomatico.cs:97`,
   `PlayerInputDriver.TryRevivir:1538` y el camino del médico (`ActualizarHabilidadMedico:1647`, que
   ya pasa por `TryRevivir`).
4. Suscribir al evento: `HealthBarView`, `RosterRowView`, `KillFeedView` (línea "X fue reanimado"),
   `MisionDirector`/`GameOutcomeController` (para descontar la baja) y `SelectionController` (para
   volver a admitirlo).
**Verificación:**
```csharp
int revividos = 0;
using (bus.Subscribe<EntityRevivedEvent>(_ => revividos++))
{
    MatarA(aliado);
    driver.TryRevivir(aliado, sostenidoLoSuficiente: true);
}
Check("revivir publica su evento", revividos == 1);
Check("el contador de bajas volvio atras", ActorRegistry.CountDead(TeamId.Player) == 0);
```

---

### Bug 31 · El tanque publica `EntityDiedEvent(-1)`
**Arreglo** — el vehículo no debe compartir el `Health` que publica eventos de soldado. Dos opciones;
va la segunda por ser menos invasiva:
1. *(descartada)* Darle al vehículo su propio componente de vida.
2. Agregar a `Health` una bandera `public bool PublicaEventosDeActor = true;` que `Vehicle.Health` pone
   en `false` justo después de `Initialize(-1, maxHealth)`. `TakeDamage` y `Heal` la comprueban antes de
   cada `Publish`. El vehículo sigue teniendo vida, modo dios, regeneración desactivable y todo lo
   demás; sólo deja de mentirle al bus con un `ActorId` que no existe.
3. Mientras tanto, endurecer los consumidores: `AiBrain.OnAnyDamage` y `PlayerInputDriver.OnEntityDied`
   deben salir temprano con `if (evt.ActorId < 0) return;`.
**Verificación:**
```csharp
int muertesDeActor = 0;
using (bus.Subscribe<EntityDiedEvent>(_ => muertesDeActor++))
    DestruirVehiculo(tanque);
Check("destruir un tanque no publica muerte de soldado", muertesDeActor == 0);
Check("pero si publica la suya", vehiculoDestruidoRecibido);
```

---

### Bug 34 · El jugador cobra dos veces su ventaja
**Arreglo** — decidir cuál de las dos mitades es la buena. El comentario del encabezado de `Dificultad`
ya lo decide: `VidaJugador` es "más vida **sin tocar los números del HUD**", o sea la alternativa. Por
lo tanto `AjustarVida` tiene que **excluir al soldado poseído** del multiplicador de aliados:
```csharp
public static bool AjustarVida(Soldier s)
{
    if (s == null || s.Health == null) return false;
    // El soldado que maneja el jugador recibe su ventaja por dano recibido
    // (VidaJugador, en MultiplicadorDeDano), no por vida maxima: si no, la cobra dos veces.
    if (s.Brain != null && s.Brain.IsPossessedByPlayer) return false;
    ...
}
```
Y, como la posesión cambia en partida, reaplicar/quitar en `PossessionChangedEvent`: el soldado que
deja de estar poseído recupera el multiplicador de aliado; el nuevo lo pierde. Se hace con el
`FijarMaximo` del bug 29 (cambiar el máximo sin curar) y guardando el máximo base por soldado.
**Verificación** (función pura, sin escena):
```csharp
// MEDIO: 100 base, aliado 1.15, jugador /1.25.
Check("aliado: 115 de vida", VidaEfectivaDe(aliado) == 115);
Check("jugador: 100 de vida y dano /1.25", VidaEfectivaDe(poseido) == 100);
Check("golpe de 40 al jugador quita 32", DanoAplicado(40, poseido) == 32);
```

---

# OLA 3 — Máquina de estados de la IA

### Bug 36 · El fuego cancela la orden de seguir
**Arreglo** — en `OnAnyDamage`, proteger `Follow` igual que `MovingToOrder`:
```csharp
if (State == AiState.Idle || State == AiState.Patrol || State == AiState.MovingToOrder || State == AiState.Follow)
{
    target = attacker;
    // La orden NO se cancela: el combate la suspende, y al terminar se retoma.
    if (State != AiState.MovingToOrder && State != AiState.Follow) hasOrder = false;
    // followTarget se conserva: es lo que permite volver a Follow en las lineas 793-823.
    SetState(AiState.Chase);
}
```
y borrar `followTarget = null;` de esa rama (la línea 512). La recuperación de las líneas 807-811 ya
contempla el caso (`SetState(followTarget != null ? Follow : MovingToOrder)`).
**Verificación:**
```csharp
OrderService.IssueFollowOrderForSelection(escuadra, lider);
SimulateSeconds(1f);
Check("siguen", aliado.Brain.State == AiState.Follow);
DispararleA(aliado);
SimulateUntil(() => aliado.Brain.State == AiState.Chase, 2f);
MatarAlEnemigo();
SimulateUntil(() => aliado.Brain.State == AiState.Follow, 5f);
Check("retoma el seguimiento al terminar el tiroteo", aliado.Brain.State == AiState.Follow);
```

### Bug 41 · La supresión ordenada se salta todo
**Arreglo** — `TickSuprimirOrdenado` deja de consumir el tick entero y de disparar sin condiciones:
1. Devolver `false` (no consume el tick): se convierte en un modificador, no en un camino alternativo.
   El disparo se hace en su propio bloque, después de los chequeos normales.
2. Añadir las tres guardas que faltan: `if (!StanceAllowsFire) return;`,
   `if (!NavService.HayLineaDeTiro(origen, suprimirOrdenadoPunto, transform, null)) return;` y
   una comprobación de fuego amigo con `SpatialGrid.QueryInRange` sobre la línea (mismo criterio que
   `IntentarLanzarGranada:134-136`).
3. `LookTowards(suprimirOrdenadoPunto, dt)` con el `dt` real, no 20.
**Verificación:** ordenar suprimir un punto con un aliado en el medio y comprobar
`Check("no dispara con un aliado en la linea", danoAlAliado == 0)`, y que el estado del soldado siga
avanzando (`Check("la maquina de estados sigue viva", brain.TicksSimulados creciente)`).

### Bug 42 · Cobertura destruida tratada como vigente
**Arreglo** — invertir la condición en `Tactica.cs:139`:
```csharp
if (coberturaDueno != null && Coberturas.Vigente(coberturaDueno)) return;   // sigue en pie
PerderCobertura();                                                          // se cayo (o desaparecio)
```
**Verificación:** mandar a un aliado a cubrirse detrás de un barril, destruir el barril con
`ObstacleMarker.Estallar()`, y `Check("se dio cuenta y no quedo agachado detras de la nada", !aliado.Brain.EnCobertura)`.

### Bug 43 · Ruta al pivote, caminata al punto de abordaje
**Arreglo** — `IssueMountOrder` planifica al mismo punto al que después camina:
```csharp
orderDestination = vehicle.ClosestBoardingPoint(self.transform.position);
mountTarget = vehicle;
PlanPathTo(orderDestination);
```
y el `case MovingToOrder` recalcula `ClosestBoardingPoint` cada tick como hoy (el vehículo se mueve),
pero si el punto se corrió más de `RehacerRutaSiSeMovio` (3 m), replanifica.
**Verificación:** muro entre el soldado y el tanque, `IssueMountOrder`, y
`Check("subio", vehiculo.RoleOf(soldado) != null)` en 20 s simulados. Hoy se traba contra el casco.

### Bug 44 · Estado incorrecto al salir de rango
**Arreglo** — en las dos líneas (`AiBrain.cs:1047` y `:1088`) cambiar `hasOrder` por `orderIsAttack`.
**Verificación:** dar una orden de mover a un soldado en combate (attack-move), alejar al enemigo, y
`Check("vuelve a Chase, no a MovingToAttackOrder", brain.State == AiState.Chase)`.

### Bug 45 · Los gizmos bootstrapean soldados
**Arreglo** — `OnDrawGizmos` no puede tocar getters con auto-bootstrap. Dos cambios:
```csharp
void OnDrawGizmos()
{
    if (!Application.isPlaying && !bootstrapped) return;   // en Edit mode, solo si ya se bootstrapeo
    ...
    // y usar los campos serializados directos, no las propiedades Effective*:
    DibujarCirculo(pos, visionRange * StanceVisionMultiplier, colorDeteccion);
    DibujarCirculo(pos, attackRange, colorAtaque);
}
```
**Verificación:** con la escena abierta en el Editor y gizmos activados,
`Check("dibujar gizmos no consume Ids", Soldier.ProximoId == antes)` tras forzar un repintado
(`SceneView.RepaintAll()`), y `Check("no registra nadie", ActorRegistry.All.Count == antes)`.

### Bug 46 · Raycasts duplicados al elegir blanco
**Arreglo**
1. `PuntajeDeObjetivo(Soldier s, bool lineaYaCalculada, bool linea)` — sobrecarga que acepta el
   resultado ya conocido. La versión de un argumento queda como conveniencia pública.
2. `MejorObjetivoVisible` calcula `linea` una sola vez por candidato y se lo pasa.
3. `TickRetarget` guarda el mejor puntaje que devolvió `MejorObjetivoVisible` (que pasa a devolver
   `out float puntaje`) en vez de recalcularlo.
**Verificación:** contador `public static int RaycastsDeSensado` incrementado en `TieneLineaDeTiro`;
`Check("<= 1 raycast por candidato", RaycastsDeSensado <= candidatos)` tras un sensado.

### Bug 38 · `string` por cambio de estado
**Arreglo** — `AiStateChangedEvent` pasa a llevar `AiState NewState` (el enum). Actualizar los
consumidores (`SquadStateIndicatorView:142`, `EntityStateDebugView`, la suite) para que comparen enums
y formateen a string sólo cuando de verdad van a mostrarlo.
**Verificación:** `BalanceBench` / `RunPerformanceBenchmarks`: la asignación por frame del bloque
`LastAiWeaponMs` tiene que bajar. Medirlo con el `ProfilerRecorder` de `MetricasDeBuild`
("GC Allocated In Frame") en un escenario de 50 soldados en combate.

### Bug 39 · O(n²) por disparo
**Arreglo** — dejar de tener un suscriptor por soldado. Un solo oyente central:
1. Nuevo `SP.Ai.SensorDeRuido` estático, con un único
   `EventBus.Subscribe<ShotFiredEvent>` y un único `Subscribe<DamageTakenEvent>`.
2. Al recibir el evento, resuelve el tirador **una vez** (con un `Dictionary<int, Soldier>` de Id a
   soldado que `ActorRegistry` ya puede mantener en `Register`/`Unregister` — eso arregla también el
   bug 7 de raíz) y hace **una** consulta `SpatialGrid.QueryInRange(pos, alertRadius, buffer, ...)`.
3. A cada soldado devuelto le llama un método nuevo `AiBrain.OirDisparo(Soldier tirador)` /
   `AiBrain.OirImpacto(Soldier atacante, Soldier victima)` con la lógica que hoy vive en
   `OnShotFiredNearby` / `OnAnyDamage`, ya sin el chequeo de distancia (lo hizo la grilla).
4. Borrar las dos suscripciones de `AiBrain.Bootstrap` (y con eso, el bug 47 deja de existir).
**Verificación:** contador de invocaciones; con 50 soldados y 1 disparo,
`Check("un disparo despierta solo a los que estan en radio", invocaciones <= enRadio)`. Y en
`RunPerformanceBenchmarks`, el tiempo del bloque de IA con 200 unidades tiene que caer de forma medible.

---

# OLA 4 — Motor, física y navegación

### Bug 48 · Agacharse no cuesta velocidad
**Arreglo**
```csharp
public const float FactorAgachado = 0.55f;   // agachado se avanza al 55 %
public float MoveSpeed => moveSpeed * (Corriendo ? FactorDeCarrera : IsCrouching ? FactorAgachado : 1f);
```
**Verificación:** función pura, sin escena:
`Check("agachado camina al 55 %", Mathf.Approximately(motor.MoveSpeed, moveSpeed * 0.55f))` con
`SetCrouching(true)`. Y `BalanceBench` antes/después: el enemigo que se agacha en `Attack` deja de
hacer attack-move a velocidad plena, así que su tasa de acierto sube y su movilidad baja.

### Bug 49 · La cámara queda hundida al revivir agachado
**Arreglo** — `ResetMotionState` tiene que pasar por el camino que restaura el ojo:
```csharp
public void ResetMotionState()
{
    SetCrouching(false);     // restaura EyeAnchor.localPosition (sale temprano si ya estaba de pie)
    IsJumping = false;
    Vaulting = false;
    verticalVelocity = 0f;
    pideCorrer = false;
    saltoPedidoHasta = 0f;   // bug 50
    alturaDePivote = -1f;    // bug 50
    vaultT = 0f;
}
```
**Verificación:**
```csharp
soldado.Motor.SetCrouching(true);
float yAgachado = soldado.EyeAnchor.localPosition.y;
MatarA(soldado); soldado.Health.Revivir(); soldado.Motor.ResetMotionState();
Check("el ojo volvio a la altura de pie", soldado.EyeAnchor.localPosition.y > yAgachado);
Check("y EyeHeightDrop es 0", Mathf.Approximately(soldado.Motor.EyeHeightDrop, 0f));
```

### Bug 50 · Salto fantasma al revivir
**Arreglo:** cubierto por el bug 49 (mismo método).
**Verificación:** `Check("no salta solo tras revivir", !soldado.Motor.IsJumping)` tras
`SimulateSeconds(1f)` con el buffer armado antes de morir.

### Bug 52 · Buffers de raycast truncados y compartidos
**Arreglo** — subir `SondeoPiso` a 24, darle su propio buffer a `SondearObstaculo`, y en los dos
métodos, cuando `n == buffer.Length`, loguear una vez (`Debug.LogWarning` con guarda de "ya avisé") en
vez de fallar en silencio. Mismo tratamiento para los otros tres buffers (bugs 23 y 59).
**Verificación:** escenario con 30 colliders apilados;
`Check("encontro el piso real", Mathf.Abs(piso - esperado) < 0.05f)`.

### Bug 53 · El arma nueva no entra en el cacheo de renderers
**Arreglo**
1. `Soldier`: `public void InvalidarRenderers() { bodyRenderers = null; }`
2. `WeaponHolder.ApplyWeaponVisualModel`, al final: `GetComponent<Soldier>()?.InvalidarRenderers();`
3. `Soldier.SetBodyVisible` recuerda el último estado pedido (`bool cuerpoVisible`,
   `bool conservandoArma`) y lo reaplica cuando se invalida, para que el arma nueva nazca con la
   visibilidad correcta.
**Verificación (Play):** apuntar (cuerpo oculto), cambiar de arma con `2`, y comprobar que el cuerpo
sigue oculto y el arma nueva se ve. En la suite:
`Check("el modelo nuevo respeta el ocultado", RenderersVisibles(soldado) == soloArma)`.

### Bug 54 · `Configure` cura a tope
**Arreglo:** usar el `FijarMaximo(int)` del bug 29:
`if (bootstrapped && health != null) health.FijarMaximo(maxHealth);`
`FijarMaximo` hace `maxHealth = max; Current = Mathf.Min(Current, max);` — nunca sube `Current`.
**Verificación:** `soldado.Health.TakeDamage(40, -1); soldado.Configure(..., max: 200);`
`Check("no lo curo", soldado.Health.Current == 60)` y `Check("subio el maximo", soldado.Health.MaxHealth == 200)`.

### Bug 56 · El cañón desprendido es una pared invisible
**Arreglo**
1. `DetachTurret`, antes de `AddComponent<DetachedTurretFlight>()`: desactivar los colliders de la
   torreta (`foreach (var c in t.GetComponentsInChildren<Collider>(true)) c.enabled = false;`). Vuela
   como decoración pura, que es lo que es.
2. `DetachedTurretFlight`: usar el piso real en vez de `y <= 0.25f`:
   ```csharp
   float piso = Physics.Raycast(transform.position + Vector3.up, Vector3.down, out var h, 50f, ~0, QueryTriggerInteraction.Ignore) ? h.point.y : 0f;
   if (transform.position.y <= piso + 0.25f) { ... }
   ```
3. Documentar el contrato en `NavService.Invalidate`: *"todo objeto sólido que aparezca, se mueva o
   desaparezca en runtime tiene que llamar a `Invalidate()`"*, y dejar el arreglo del bug 58
   (invalidación por región) para que ese contrato sea barato de cumplir.
**Verificación:** destruir el tanque y comprobar `Check("la torreta en vuelo no bloquea el tiro",
NavService.HayLineaDeTiro(a, b, null, null))` con la torreta pasando entre A y B.

### Bug 58 · Invalidar la grilla es reconstruirla entera
**Arreglo** — invalidación por región:
```csharp
public static void Invalidate(Bounds region)
{
    Version++;
    if (!graph.IsBuilt) { dirty = true; return; }
    graph.RemarcarRegion(region.min, region.max, IsBlockedAt);   // metodo nuevo en WaypointGraph
}
public static void Invalidate() { dirty = true; hayArea = false; Version++; }   // el de siempre, para cambio de escena
```
`ObstacleMarker` (el único llamador de juego) pasa el `bounds` del obstáculo caído inflado por
`Clearance`. Con un muro de 6 × 2 m eso son ~12 nodos en vez de 2.400.
**Verificación:** cronometrar con `System.Diagnostics.Stopwatch` en la suite:
`Check("demoler cuesta < 1 ms de regrilla", msDeInvalidacion < 1.0)`.

### Bug 59 · Buffer de nodo truncado · Bug 23 · Buffer de bala truncado
**Arreglo:** mismo tratamiento que el bug 52 (subir capacidad + aviso al llenarse). Para el bug 23,
además, **ordenar por distancia** no alcanza: `RaycastNonAlloc` puede no traer el más cercano. La
corrección real es usar `Physics.Raycast` (un solo hit, siempre el más cercano) con una máscara de capa
que excluya soldados y proyectiles, y dejar `RaycastNonAlloc` sólo donde de verdad se necesitan todos.
Eso exige crear dos capas (`Escenario`, `Actores`) y asignarlas en `WorldArtPipeline`/`SoldierPrefabPipeline`.
**Verificación:** 12 colliders finos apilados entre tirador y pared;
`Check("la bala choca con el mas cercano", puntoDeImpacto.z ~= primeraPared.z)`.

### Bug 60 · El área jugable incluye soldados
**Arreglo:** en el bucle de `NavService.Build`, usar el mismo filtro que el sondeo:
`if (c == null || !BlocksMovement(c)) continue;`
**Verificación:** `Check("el area no se estira con un soldado lejano", TryArea(out var a) && a.max.z < 200)`
tras poner un soldado en `z = 500`.

### Bug 28 · La explosión mete soldados dentro de los muros
**Arreglo**
```csharp
// Modo dios: si no recibe dano, tampoco lo empuja la onda.
if (ModoDios.Protege(s.Health)) continue;
...
// El empuje pasa por la MISMA resolucion de colision que caminar.
var empuje = away.normalized * strength * 2.2f;
s.transform.position += SP.Core.Deslizador.Resolver(s.transform, s.GetComponent<Collider>(), empuje, 0.45f);
```
**Verificación:** granada pegada a un muro con un soldado entre medio;
`Check("no quedo dentro del muro", !Physics.CheckSphere(s.transform.position, 0.3f, ~0, QueryTriggerInteraction.Ignore))`.

---

# OLA 5 — Órdenes y entrada

Los 22 bugs de esta ola son, en su mayoría, de una a cinco líneas. Van agrupados por archivo para
tocar cada uno una sola vez.

## `Player/OrderService.cs`

| Bug | Arreglo | Verificación |
|---|---|---|
| **61** | `BajarSiVaMontado` recorre `WorldSystemsRegistry.Vehicles` en vez de `FindObjectsByType`. | Contador de barridos de escena = 0 durante una orden de lote. |
| **62** | `IssueFollowOrderForSelection` usa `AliveOnly(selection)` y después `list.RemoveAll(s => s == leader)`. | `Check("el lote no cuenta muertos", OrderHistory.Ultimo.Cantidad == vivos)`. |
| **63** | `IssueMoveOrder` valida con **las dos**: `PuntoAlcanzable` (NavMesh, si hay) **y** `IsValidDestination` (física, siempre). Rechaza si cualquiera falla. | Orden a un punto dentro de un muro, sin NavMesh: `Check("rechazada", !huboOrden && huboRechazo)`. |
| **64** | Cubierto por el 63: con la física siempre activa, la ausencia de NavMesh deja de ser un agujero. | Ídem. |
| **65** | Reemplazar los 5 `soldier.GetComponent<AiBrain>()` por `soldier.Brain`. | `grep -c "GetComponent<AiBrain>" OrderService.cs` = 0. |
| **66** | `IssueMountOrder`: `if (soldier == null \|\| vehicle == null \|\| soldier.Health == null \|\| !soldier.Health.IsAlive \|\| LoManejaElJugador(soldier) \|\| soldier.Role == RoleType.Civilian) return;` | `Check("no se le ordena montar a un muerto", vehiculo.RoleOf(muerto) == null)` tras la orden y 10 s. |
| **67** | Guardas de null al principio de `IssueMoveOrder` e `IssueAttackOrder`. | Llamarlas con null en la suite y comprobar que no lanzan. |
| **68** | `AnnounceBatch` reusa un `List<int>` estático y un `int[]` que crece; `AliveOnly` recibe un `List<Soldier>` del llamador (patrón de `SpatialGrid.QueryInRange`). | `ProfilerRecorder` de "GC Allocated In Frame" durante 20 órdenes seguidas. |

## `Player/PlayerInputDriver.cs` (+ parciales)

| Bug | Arreglo | Verificación |
|---|---|---|
| **72** | Escribir `AjustesDeEscuadra.Correr` en `Update()`, antes de los `return` tempranos, junto a `ManejadoAMano` y `Lider`: `AjustesDeEscuadra.Correr = Rig.Mode == ControlMode.Fps && !currentSeat.HasValue && correrSolicitado;` (con `correrSolicitado` como campo que `UpdateFps` actualiza). | `Check("al pasar a RTS los aliados dejan de correr", !AjustesDeEscuadra.Correr)` tras `ToggleMode` con Shift apretado. |
| **73** | Envolver la rueda y `Rig.SetZoomed` en `if (!menuDeOrdenesAbierto)`. Mover el cálculo de `menuDeOrdenesAbierto` arriba de todo, junto al resto de las guardas. | Play: abrir el radial, girar la rueda, confirmar que no cambia el arma. |
| **74** | `Start()` usa `PossessionService.Swap(Brain, Squad[0])` y se borra el parche del `FindAnyObjectByType<RosterView>()`. | `Check("el arranque publica PossessionChangedEvent", eventosDePosesion == 1)`. |
| **75** | Agregar `AjustesDeEscuadra.Lider = null;` al `OnDisable`, junto al `ManejadoAMano` que ya está. | `Check("lider suelto al apagar el driver", AjustesDeEscuadra.Lider == null)`. |
| **76** | `KeyBindings.ModoDios` y `KeyBindings.PanelDeRendimiento` nuevos (default `F4` y `P`), y leerlos con `KeyBindings.WasPressed`. Agregarlos a `ControlsTable` y a `KeyRebindView`. | `Check("F4 y P son reasignables", KeyBindings.Todas.Contains(KeyBindings.ModoDios))`. |
| **77** | Calcular `resultRts` **una** vez al principio de `UpdateRts` y pasarlo por parámetro a `UpdateCoverPreviewRts`, al bloque de `ctrlHeld` y al de `pidioOrden`. Borrar los tres `Aim.Evaluate` restantes. | Contador `AimTargeting.EvaluacionesPorFrame`: `Check("1 raycast de mira por frame en RTS", <= 1)`. |
| **78** | `var vb = vehicle.GetComponent<VehicleBrain>(); if (vb == null) return false;` | Vehículo sin `VehicleBrain`: la orden devuelve false sin lanzar. |
| **79** | La rama de la mira comprueba distancia igual que la del más cercano: `if (result.Type == Ally && Herido(result.Soldier) && Vector3.Distance(...) <= AlcanceDeCuracionMedico)`. Constante nueva `AlcanceDeCuracionMedico = 6f` (mayor que `interactRadius`, porque apuntar es deliberado, pero acotada). | `Check("no cura a 30 m", herido.Health.Current == antes)` tras 3 s con el médico apuntándole desde lejos. |
| **80** | `Physics.Raycast(..., ~0, QueryTriggerInteraction.Ignore)` y máscara que excluya actores (ver bug 23). | `Check("el pickup no cambia el sonido de pisada", kind == FootstepConcrete)` parado sobre un `MunicionPickup`. |
| **81** | `if (!fired && emptyBeforeFire && Brain.Current.Weapon.SinMunicionTotal && emptyClickCooldown <= 0f)` — el clic seco sólo cuando de verdad no queda nada. El caso "se vació y está recargando" ya tiene su propio sonido en `StartReload`. | Play: vaciar el cargador con el gatillo apretado; se oye la recarga, no el clic seco. |
| **82** | Reemplazar `pidioOrden = false;` por un `return;` de la rama, o envolver el resto en `else`. | Revisión de código; no cambia comportamiento. |
| **83** | Decidir: si el viewmodel y `MiraOptica` están reemplazados, **borrarlos**. Sacar `weaponViewmodel`, `weaponViewmodelRenderer`, `recoilKick`, `apuntadoVisual`, `Mira`, `MiraOptica.cs` y el shader `SP/ArmaEnPrimeraPersona` si no lo usa nada más. Si se quieren conservar para volver a activarlos, envolverlos en `if (MostrarViewmodel)` con el default en `false` y no ejecutar nada cuando está apagado. | `Check("no hay WeaponViewmodel en escena", GameObject.Find("WeaponViewmodel") == null)` y medición de frame. |
| **84** | `IssueGroundOrderT` con `shiftHeld` llama a `OrderService.IssueFormationOrderForSelection(libres, punto, Vector3.forward, currentFormation)` en vez del bucle manual. | `Check("Shift+T acusa recibo", OrderHistory.Cantidad == antes + 1)`. |

---

# OLA 6 — Vehículos y torretas

### Bug 85 · Bajarse cambia de tamaño
**Arreglo** — `Dismount` restaura la escala guardada:
```csharp
soldier.transform.localScale = mountTrueScale.TryGetValue(soldier, out var escalaReal) ? escalaReal : soldier.transform.localScale;
mountTrueScale.Remove(soldier);
```
y `Mount` guarda `mountTrueScale[soldier]` **también** en el camino instantáneo (hoy sólo lo hace en el
de animación, `Vehicle.cs:363`).
**Verificación:** `var e0 = s.transform.localScale; v.Mount(s); v.Dismount(s);`
`Check("la escala se conserva", s.transform.localScale == e0)`.

### Bug 86 · Cambiar de asiento puede perder a un soldado
**Arreglo** — `SwapSeats` y `MoveToSeat` con rollback:
```csharp
public bool MoveToSeat(Soldier soldier, VehicleSeatRole role)
{
    var rolPrevio = RoleOf(soldier);
    if (soldier == null || rolPrevio == null || !IsSeatFree(role) || IsMountAnimating(soldier)) return false;
    Dismount(soldier);
    soldier.gameObject.SetActive(false);
    if (Mount(soldier, role)) return true;
    // No entro: se lo devuelve a su asiento de antes. Si tampoco entra ahi, se lo baja
    // de verdad (activo, en el piso) en vez de dejarlo desactivado y sin asiento.
    if (!Mount(soldier, rolPrevio.Value)) { soldier.gameObject.SetActive(true); Dismount(soldier); }
    return false;
}
```
Mismo patrón, con los dos soldados, en `SwapSeats`.
**Verificación:** llenar los 4 asientos, destruir el vehículo entre el `Dismount` y el `Mount` (con un
`SimStep` en el medio), y `Check("nadie quedo desactivado y sin asiento", todos.All(s => s.gameObject.activeInHierarchy))`.

### Bug 87 · `Occupants` asigna por lectura
**Arreglo** — lista cacheada que sólo se reconstruye en `Mount`/`Dismount`:
```csharp
readonly List<Soldier> occupantsCache = new List<Soldier>();
bool occupantsDirty = true;
public IReadOnlyList<Soldier> Occupants
{
    get
    {
        if (!occupantsDirty) return occupantsCache;
        occupantsDirty = false;
        occupantsCache.Clear();
        foreach (var role in AllRoles) if (seats.TryGetValue(role, out var s)) occupantsCache.Add(s);
        return occupantsCache;
    }
}
```
Los llamadores que iteran mientras modifican (`OnDestroyed`, `DismountAll`, `BajarATodos`) ya hacen
`new List<Soldier>(Occupants)`: eso se conserva y ahora es la **única** asignación.
**Verificación:** `ProfilerRecorder` durante 60 frames con `VehicleStatusView` activo.

### Bug 88 · El tanque clona el material de todo lo que cuelgue
**Arreglo**
```csharp
void CacheColorIfNeeded()
{
    if (colorCached) return;
    colorCached = true;
    var lista = new List<Renderer>();
    foreach (var r in GetComponentsInChildren<Renderer>(true))
    {
        if (r == null) continue;
        if (r.GetComponentInParent<SP.Actors.Soldier>() != null) continue;   // el artillero de pie no es chasis
        lista.Add(r);
        if (r.sharedMaterial != null) r.sharedMaterial = new Material(r.sharedMaterial);
    }
    chassisRenderers = lista.ToArray();
    ...
}
```
Y en `DetachTurret`, sacar del array los renderers de la torreta desprendida.
Además: `OnDestroy` del `Vehicle` tiene que destruir los materiales clonados (misma fuga que el bug 24).
**Verificación:** `Check("el artillero no se tine del color del tanque",
ColorDe(artillero) == colorOriginalDelArtillero)` con un ocupante en `Passenger1`.

### Bug 89 · `Dismount` no resetea el movimiento
**Arreglo:** `soldier.Motor?.ResetMotionState();` justo después del `SetActive(true)`.
**Verificación:** `Check("baja de pie y quieto", !s.Motor.IsJumping && !s.Motor.IsCrouching)` tras montar
a un soldado saltando.

### Bug 91 · Instalar torretas ensucia la escena
**Arreglo**
1. `TorretaFija.Instalar`: en Edit mode, `Undo.AddComponent` en vez de `AddComponent`, y marcar los
   componentes con `hideFlags |= HideFlags.DontSaveInEditor` cuando `!Application.isPlaying`.
2. Mejor todavía: que la instalación sea parte del **pipeline de construcción** de la escena
   (`WorldArtPipeline` / `LevelBlockoutBuilder`), que ya se corre a propósito y guarda, y que
   `InstalarEnEscena()` en runtime sólo dé de alta lo que ya existe.
3. Agregar a `RespaldoDeEscenas` la comprobación de que la escena no cambió tras una corrida de la suite
   (ya existe la infraestructura para el ítem 33 de la ronda anterior).
**Verificación:** `git diff --stat Assets/_Project/Scenes` tras `Run All Tests Headless` = 0 líneas.

### Bug 92 · `Ocupar` sin guarda de arma
**Arreglo:** `var arma = s.Weapon; if (arma == null) { motivo = "EL SOLDADO NO TIENE ARMA"; return false; }`
**Verificación:** llamar `Ocupar` con un soldado sin `WeaponHolder`; devuelve `false` con motivo.

### Bug 93 · `OnDisable` teletransporta al ocupante
**Arreglo:** `Liberar(bool reposicionar = true)`; `OnDisable` llama `Liberar(reposicionar: false)`.
**Verificación:** descargar la escena con un ocupante y comprobar que su posición no cambia en el
último frame (log de `LimpiezaDeEscena`).

---

# OLA 7 — Materiales, UI y limpieza

### Bugs 15, 98, 99 · Escrituras sobre `sharedMaterial` compartido
**Arreglo común** — los tres pasan a `MaterialPropertyBlock` vía el helper que ya existe:
- `WeaponHolder.ApplyWeaponVisualColor` → `CubeFxReactor.WriteTint(WeaponVisualRenderer, color)`
- `SquadStateIndicatorView:142` y `EnemyAlertIndicatorView:128,166,176` → ídem.
- `TurretWeapon:433-436` → ídem (y con eso deja de hacer falta el clon del material).
- `VehicleMountIndicator:69-75` → ídem para los tres renderers y los `LineRenderer`
  (`LineRenderer` acepta `SetPropertyBlock`).
**Verificación** — una prueba que cubra la clase entera del bug:
```csharp
// Dos soldados del mismo equipo comparten material. Tenir a uno no debe tenir al otro.
var c0 = CubeFxReactor.ReadTint(b.Renderer);
CubeFxReactor.WriteTint(a.Renderer, Color.red);
Check("tenir a uno no tine al otro", CubeFxReactor.ReadTint(b.Renderer) == c0);
```
y repetirla para los 5 sitios. Además, agregar a `Tools/Ci/verificar_estatico.py` una regla que falle
si aparece `sharedMaterial.color =` o `sharedMaterial.SetColor(` fuera de `SafeMaterial.cs` y de
`CubeFxReactor.cs`.

### Bug 94 · `SelectSingle(null)`
**Arreglo:** guarda al principio de `SelectSingle` y `AddToSelection`
(`if (s == null || s.Health == null || !s.Health.IsAlive) return;`), y en `Publish` saltear nulls por si
alguno se coló (`foreach (var s in selected) if (s != null) ids.Add(s.Id);`).
**Verificación:** `Selection.SelectSingle(null); Check("no revento y la seleccion sigue limpia", Selection.Selected.Count == 0)`.

### Bug 95 · La selección conserva a los montados
**Arreglo**
1. `Core/Events.cs`: `VehicleSeatChangedEvent(Vehicle v, int soldierId, bool subio)`, publicado por
   `Vehicle.Mount` y `Vehicle.Dismount`.
2. `SelectionController` se suscribe y hace `selected.RemoveAll(s => s != null && s.Id == evt.SoldierId)`
   cuando `subio == true`.
3. `Publish()` saneaa de paso: `selected.RemoveAll(s => s == null || !s.gameObject.activeInHierarchy);`
**Verificación:** `Check("el que subio salio de la seleccion", !Selection.Selected.Contains(s))` tras
`Mount`.

### Bug 96 · `List<int>` por publicación
**Arreglo:** `readonly List<int> idsBuffer = new List<int>();` reusado en `Publish`, y
`SelectionChangedEvent` que lleve `IReadOnlyList<int>` en vez de `List<int>` (los consumidores sólo leen).
**Verificación:** `ProfilerRecorder` durante un arrastre de selección de 2 s.

### Bug 97 · 34 `FindAnyObjectByType` en runtime
**Arreglo** — por categoría, no uno por uno:
1. **Cacheos perezosos correctos** (`if (x == null) x = FindAnyObjectByType<...>()` en un `Awake` o en
   la primera llamada): se dejan, pero se mueven a `Awake`/`Asegurar*` para que sean auditables.
2. **En bucles de actualización** (`RosterRowView:93`, `MisionDirector:321`, `MinimapIcon:266`,
   `UnitLabelView:175-179`, `RevivePromptView:128`): se cambian por una referencia inyectada al
   construir la vista (todas se construyen desde `WorldUiDirector` o desde un pipeline de Editor, que
   ya tiene la referencia) o por un singleton estático como el que ya usan `KillFeedbackDirector` y
   `CameraRig`.
3. **`Loc.cs:161`** (barrido de todos los `Text` al cambiar de idioma): mantener un registro de
   `Text` localizables (`LocText` componente marcador) en vez de barrer.
4. Agregar la regla a `Tools/Ci/verificar_estatico.py`: prohibido `FindAnyObjectByType` /
   `FindObjectsByType` fuera de `Editor/`, de `Awake`, de `Start` y de métodos que empiecen con
   `Asegurar`/`Instalar`/`Registrar`. Lista blanca explícita para las excepciones justificadas.
**Verificación:** el script de CI falla hoy con 34 hallazgos y tiene que terminar en 0 (o en la lista
blanca).

### Bug 12 · Reentrada del `EventBus`
**Arreglo:** marcar el `ActionDisposable` como "ya liberado" y que `Publish` lo saltee:
```csharp
sealed class ActionDisposable : IDisposable
{
    public bool Liberado { get; private set; }
    public void Dispose() { if (Liberado) return; Liberado = true; onDispose?.Invoke(); onDispose = null; }
}
```
y `ClearAll()` recorre los disposables entregados marcándolos antes de vaciar el diccionario (requiere
guardarlos en una lista). Alternativa más simple y suficiente: documentar que darse de baja durante el
`Publish` surte efecto **a partir del evento siguiente**, y agregar un `Check` que lo demuestre.
**Verificación:** `Check("darse de baja dentro del handler no rompe", ...)` con un suscriptor que se
libera a sí mismo.

### Bug 11 · Asignación muerta en `ObjectPool.Clear`
**Arreglo:** borrar la línea 107 y el bloque de comentario que describe un arreglo que no existe;
dejar el `Debug.LogWarning`, que sí es útil.
**Verificación:** revisión de código.

### Bugs 16, 17, 18, 19, 20, 21 · `WeaponHolder`
| Bug | Arreglo | Verificación |
|---|---|---|
| **16** | `const int MaxArmas = 16;` y `if (i < 0 \|\| i >= MaxArmas) continue;` en `ReponerMunicion` y `AgregarMunicion`. Mejor: dimensionar los arrays con `System.Enum.GetValues(typeof(WeaponKind)).Length`. | `Check("cubre todo WeaponKind", reservaPorArma.Length >= Enum.GetValues(typeof(WeaponKind)).Length)`. |
| **17** | `reloadDuration > 0f ? 1f - Clamp01(reloadTimer / reloadDuration) : 1f`, y `ConfigurarCargador` hace `Mathf.Max(0.01f, segundosDeRecarga)`. | `ConfigurarCargador(100, 0f); Check("sin NaN", !float.IsNaN(w.ReadinessFraction01))`. |
| **18** | `[SerializeField] List<WeaponKind> loadout = new() {...};` con `public IReadOnlyList<WeaponKind> Loadout => loadout;` y un `CambiarRanura(int, WeaponKind)`. | Cambiar el arma principal, entrar a Play, `Check("sobrevivio", w.Loadout[0] == elegida)`. |
| **19** | `cargadorPorArma[(int)candidata] = -1;` junto al `reservaInicializada = false`. | `Check("el arma nueva llega con el cargador lleno", w.CurrentAmmo == w.MagazineSize)`. |
| **20** | Pasar `espec.ProjectileSpeed` en las tres ramas de `TryFire`. | `Check("respeta la velocidad del catalogo", proyectil.Velocity.magnitude ~= base * espec.ProjectileSpeed)`. |
| **21** | `visualModelInstance.hideFlags = HideFlags.DontSaveInEditor \| HideFlags.DontSaveInBuild;` (mismo patrón que `DebrisPool.Create:115`). | `git diff` de prefabs y escenas tras la suite = vacío. |

### Bugs 24, 25, 26 · `Projectile`
| Bug | Arreglo | Verificación |
|---|---|---|
| **24** | `void OnDestroy() { if (ownMaterialReady && cachedRenderer != null && cachedRenderer.sharedMaterial != null) Destroy(cachedRenderer.sharedMaterial); }` | `Check("no crecen los materiales", Resources.FindObjectsOfTypeAll<Material>().Length)` antes/después de 200 disparos + cambio de escena. |
| **25** | `Expire()`: `if (pool != null) pool.Release(this); else { OnDespawn(); gameObject.SetActive(false); }` | `Check("sin pool igual se recoge", !ActiveInstances.Contains(p) && !p.gameObject.activeSelf)`. |
| **26** | `OnDespawn` resetea también `segundosHastaLaProximaSupresion = 0f;`, `ultimoImpactoFueCabeza = false;`, `explosionRadius = 0f;`, `damage = 0;`. | `Check("instancia reciclada sin herencia", p.Gravity == 0 && !p.UltimoImpactoFueCabeza)`. |

### Bug 30 · Comentario de `Heal` desalineado
**Arreglo:** corregir el comentario (`Health.cs:22-27`) para que diga que revivir es `Revivir()` (el
método nuevo del bug 29), no `Heal()`.

### Bugs 32, 33 · `Dificultad`
| Bug | Arreglo | Verificación |
|---|---|---|
| **32** | `AplicarVida()` empieza con `ActorRegistry.EnsureAllRegistered();` | `Check("la tripulacion del tanque enemigo tambien", tripulante.Health.MaxHealth == esperado)`. |
| **33** | Guardar el máximo base y hacerla idempotente: `Health.MaximoBase` (se fija en el primer `Initialize`) y `AjustarVida` calcula `FijarMaximo(RoundToInt(MaximoBase * k))`. Así dos llamadas dan lo mismo. | `AplicarVida(); int v1 = s.Health.MaxHealth; AplicarVida(); Check("idempotente", s.Health.MaxHealth == v1)`. |

---

## 2. Esfuerzo estimado

| Ola | Archivos tocados | Pruebas nuevas | Estimación |
|---|---|---|---|
| 0 | 11 | 1 fase (`FaseReinicio`) + 6 `Check` de determinismo + `RunMany` en verde | 1,5 días |
| 1 | 6 | 4 fases nuevas (rodeo, supresión, granadas, salto/trepa) | 2 días |
| 2 | 9 | 10 `Check` nuevos, 3 fases | 3 días |
| 3 | 4 (`AiBrain.*`) + 1 nuevo (`SensorDeRuido`) | 8 `Check` + benchmark | 3 días |
| 4 | 8 | 10 `Check` + `BalanceBench` antes/después | 2,5 días |
| 5 | 3 | 15 `Check` + una pasada del autoplayer | 2 días |
| 6 | 4 | 8 `Check` + fase de vehículos | 1,5 días |
| 7 | ~20 | 12 `Check` + 2 reglas de CI | 2 días |
| | | | **≈ 17,5 días** |

## 3. Criterios de aceptación de la ronda

1. `Strategic Point > Run All Tests Headless` en verde, con **al menos 60 `Check` nuevos**.
2. `RunMany(100)` con 100 iteraciones idénticas (hoy no lo es: bugs 1, 55, 100).
3. `Tools/Ci/verificar_estatico.py` en verde con las **tres reglas nuevas**: estáticos sin reset,
   `sharedMaterial.color` fuera de los dos archivos permitidos, y `FindAnyObjectByType` fuera de
   `Awake`/`Start`/`Asegurar*`.
4. `Tools/Qa/qa_total.bat` completo (ver `Docs/RONDA_10_QA_AUTOMATICO.md`): tutorial de 58 pasos con
   0 fallidos, capturas de los 58, `spmetrics.txt` con `p99_ms` por debajo del de la ronda 9, y 0
   errores y 0 excepciones en `Player.log`.
5. `git diff --stat Assets/_Project/Scenes Assets/_Project/Prefabs` = 0 después de correr la suite
   entera (bugs 21 y 91).
6. `BalanceBench` corrido antes y después, con las diferencias explicadas por escrito: los bugs 13, 22
   y 48 **cambian el balance a propósito** y hay que decir en cuánto.
