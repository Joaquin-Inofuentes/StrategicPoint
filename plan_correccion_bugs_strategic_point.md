# Strategic Point — Plan de implementación para los 97 bugs

Este documento es el plan de corrección paso a paso para cada uno de los 97 bugs encontrados en la auditoría (`bug_report_strategic_point.md`). Está organizado por subsistema, igual que el reporte original, para que sea fácil ir de un bug puntual del reporte a su plan de solución acá.

Para cada bug se detalla:
- **Archivos** afectados (con línea exacta cuando aplica).
- **Causa raíz**: por qué pasa, en términos del código real.
- **Plan de implementación**: pasos concretos, con nombres de métodos/campos reales del proyecto y respetando las convenciones ya establecidas (patrón `Bootstrap()`, pools con budget fijo + `HideFlags`, `SafeMaterial.Create()` para materiales instanciados, `EventBus`, `WorldSystemsRegistry`, etc.).
- **Verificación**: cómo confirmar que el fix funciona — en lo posible, qué `Check()` nuevo agregar a `HeadlessTestRunner.cs`, o si no es práctico, qué secuencia de test en Play Mode correr y qué observar.
- **Riesgo/efectos secundarios**: qué más podría romperse y qué revisar.

Los bugs más severos de cada sección están marcados explícitamente por el redactor de esa sección. Dos bugs (animación de abordaje de vehículo, color compartido del cañón por calor) son regresiones de features agregadas en esta misma sesión — están señalados igual en sus secciones correspondientes.

## Índice

1. [Actores / IA / Cámara](#actores--ia--cámara) — 18 bugs
2. [Combate / Core](#combate--core) — 11 bugs
3. [Player (input, órdenes, posesión)](#player-input-órdenes-posesión) — 18 bugs (incluye integración entre sistemas)
4. [Vehículos / Demo](#vehículos--demo) — 11 bugs
5. [Presentation (VFX, audio, UI de mundo)](#presentation-vfx-audio-ui-de-mundo) — 12 bugs + 1 nota cruzada
6. [UI (HUD, menús, paneles)](#ui-hud-menús-paneles) — 14 bugs
7. [Editor / Tooling de testing](#editor--tooling-de-testing) — 7 bugs
8. [Configuración / Persistencia](#configuración--persistencia) — 2 bugs

**Total: 94 planes individuales + 3 bugs cubiertos como parte de otro plan (integraciones cross-sistema contadas una sola vez) = 97 bugs del reporte original.**

---


---

# Actores / IA / Cámara — Planes de corrección (18 bugs)

> Contexto de convenciones usadas en este documento (ya vigentes en el proyecto, ver los 8 archivos leídos):
> - **Bootstrap() perezoso**: `if (bootstrapped) return; bootstrapped = true; ...` — usado en `Soldier`, `AiBrain`.
> - **EventBus.Instance.Subscribe<T>/Publish<T>** con `IDisposable` guardado en un campo y liberado en `OnDestroy`.
> - **`WorldSystemsRegistry.Register/Unregister`** para listas cacheadas que `WorldSimulationDriver` recorre en vez de `FindObjectsByType`.
> - **`HeadlessTestRunner.Check(mensaje, condición)`** dentro de `RunPhaseN`, con mensajes descriptivos e interpolados. `SimulateSeconds(t)` / `SimulateUntil(cond, timeoutSeg)` para avanzar el reloj en Edit mode. `SP.Core.WorldSimulationDriver.Step(dt)` es el único camino de simulación real (`RunAll` y Play mode corren exactamente el mismo código).
> - **`SafeMaterial.Create`** para materiales de runtime.

---

### Bug 1: El soldado marcha a un `orderDestination` nunca fijado tras completar una orden de ataque
**Archivos:** `Ai/AiBrain.cs:278-284` (`IssueAttackOrder`), `Ai/AiBrain.cs:327-328` (`Tick`, reseteo de `Chase`/`Attack`)

**Causa raíz:** `IssueAttackOrder` (líneas 278-284) pone `hasOrder = true` y `target = enemy`, pero nunca escribe `orderDestination` — ese campo se queda en su valor por defecto `Vector3.zero` (o en lo que haya quedado de una orden anterior). Cuando el enemigo muere y `Tick()` llega a la línea 327-328, evalúa `target == null && (State == Chase || State == Attack)` y como `hasOrder` sigue en `true` (nadie lo bajó: `IssueAttackOrder` nunca lo limpia al completarse, solo el código de `MovingToOrder` en la línea 384 lo hace), decide `SetState(AiState.MovingToOrder)` en vez de `AiState.Patrol`. El soldado entonces camina hacia `orderDestination`, que es `(0,0,0)` o un punto viejo de una orden anterior no relacionada.

**Plan de implementación:**
1. En `IssueAttackOrder` (línea 278-284), fijar `orderDestination` a la posición actual del soldado (o del enemigo) para que, si por algún motivo se recae en `MovingToOrder`, el "viaje" sea trivial (distancia ~0) y no un paseo hasta el origen del mundo:
   ```csharp
   public void IssueAttackOrder(Soldier enemy)
   {
       if (!bootstrapped) Bootstrap();
       target = enemy;
       hasOrder = true;
       orderDestination = self.transform.position; // evita el punto (0,0,0) si el Tick recae en MovingToOrder
       SetState(AiState.MovingToAttackOrder);
   }
   ```
2. Corregir la causa de fondo, no solo el síntoma: una orden de ataque completada (el objetivo murió) NO debería reactivar `MovingToOrder` — eso es semántica de "IssueMoveOrder", no de "IssueAttackOrder". Añadir un campo `bool isAttackOrder` (o reutilizar el hecho de que `MovingToAttackOrder`/`Attack` ya distinguen el origen) que se limpie junto con `hasOrder` cuando el objetivo de ataque muere. La forma más simple y consistente con el resto del archivo es bajar `hasOrder = false` en el mismo punto donde se detecta `target == null` viniendo de un ataque:
   ```csharp
   if (target == null && (State == AiState.Chase || State == AiState.Attack))
   {
       // Bug 1/3: una orden de ATAQUE completada no debe reencaminar a
       // MovingToOrder con un destino que IssueAttackOrder nunca fijo.
       // Solo una orden de MOVIMIENTO (mountTarget o un punto simple)
       // sigue vigente tras perder el target.
       bool wasPlainMoveOrder = hasOrder && wasFromMoveOrder;
       hasOrder = wasPlainMoveOrder; // se aclara mas abajo (paso 3)
       SetState(!hasOrder ? AiState.Patrol : followTarget != null ? AiState.Follow : AiState.MovingToOrder);
   }
   ```
   En la práctica, lo más simple y menos invasivo es agregar un campo `bool orderIsAttack` puesto a `true` en `IssueAttackOrder` y a `false` en `IssueMoveOrder`/`IssueMountOrder`/`IssueFollowOrder`/`CancelOrder`, y usarlo en la condición de la línea 328:
   ```csharp
   if (target == null && (State == AiState.Chase || State == AiState.Attack))
   {
       if (orderIsAttack) { hasOrder = false; orderIsAttack = false; }
       SetState(!hasOrder ? AiState.Patrol : followTarget != null ? AiState.Follow : AiState.MovingToOrder);
   }
   ```
3. Declarar el campo `bool orderIsAttack;` junto a `hasOrder` (línea 48), ponerlo `true` en `IssueAttackOrder` y `false` explícitamente en `IssueMoveOrder`, `IssueMountOrder`, `IssueFollowOrder` y `CancelOrder` (todas las hermanas `Issue*`/`CancelOrder`), siguiendo el mismo patrón en que cada `Issue*` ya resetea `mountTarget`/`orderQueue`.

**Verificación:** Añadir en `RunPhase2` (donde ya existe la secuencia `OrderService.IssueAttackOrder(nearestFree, enemy2)` alrededor de la línea 933) un `Check` posterior a `resolved` (línea 940) que confirme que, tras ganar el combate, `nfBrain.State == AiState.Patrol` (no `MovingToOrder`) y que la posición del soldado no se movió hacia `(0,0,0)`:
   ```csharp
   Check($"{nearestFree.DisplayName} vuelve a Patrol (no camina al origen del mundo) tras ganar el ataque",
       nfBrain.State == AiState.Patrol);
   ```

**Riesgo/efectos secundarios:** Revisar que ningún otro código dependa de que `hasOrder` siga `true` después de un `IssueAttackOrder` completado (por ejemplo `CurrentOrderDestination`, que ya filtra por `State == MovingToOrder`, así que no se ve afectado). Confirmar que `FindNextSquadmateToBoard` en `PlayerInputDriver.cs` (línea ~1098-1101), que lee `brain.CurrentOrderDestination`, sigue funcionando igual porque ese getter ya excluye `MovingToAttackOrder`.

---

### Bug 2: `TransitionRoutine` no verifica que `target` siga vivo y puede quedar `IsTransitioning=true` para siempre
**Archivos:** `Camera/CameraRig.cs:233-251` (`TransitionRoutine`)

**Causa raíz:** El bucle `while (t < duration)` en `TransitionRoutine` lee `target.position` y `target.rotation` cada frame sin comprobar si `target` (un `Transform`, por ejemplo `EyeAnchor` de un aliado poseído) fue destruido a mitad de camino. Si el `GameObject` dueño de ese `Transform` se destruye (aliado muerto, vehículo destruido durante la transición de cámara), Unity convierte la referencia en un "fake null" y `target.position` lanza una `MissingReferenceException` (o similar) dentro de la corrutina. La excepción interrumpe el `IEnumerator` sin ejecutar el resto del método, así que `IsTransitioning` nunca vuelve a `false` ni `transitionRoutine` se limpia — la cámara queda congelada permanentemente porque `LateUpdate()` (línea 79) y `FollowFps`/`FollowAnchor`/`FollowThirdPerson` (líneas 255, 265, 273) están todos guardados por `!IsTransitioning`.

**Plan de implementación:**
1. Agregar una comprobación de nulidad al inicio de cada iteración del `while`, y salir de forma segura hacia el estado final "más razonable" (quedarse donde está la cámara, en vez de crashear):
   ```csharp
   IEnumerator TransitionRoutine(Transform target, float duration)
   {
       IsTransitioning = true;
       Vector3 fromPos = transform.position;
       Quaternion fromRot = transform.rotation;
       float t = 0f;
       while (t < duration)
       {
           // El target puede morir a mitad de transicion (aliado poseido
           // que recibe el golpe final, vehiculo destruido): sin este
           // chequeo, dereferenciar target.position tira y deja
           // IsTransitioning en true para siempre, congelando la camara.
           if (target == null)
           {
               transform.position = fromPos;
               transform.rotation = fromRot;
               IsTransitioning = false;
               transitionRoutine = null;
               yield break;
           }
           t += Time.deltaTime;
           float k = t / duration;
           transform.position = Vector3.Lerp(fromPos, target.position, k);
           transform.rotation = Quaternion.Slerp(fromRot, target.rotation, k);
           yield return null;
       }
       if (target != null)
       {
           transform.position = target.position;
           transform.rotation = target.rotation;
       }
       IsTransitioning = false;
       transitionRoutine = null;
   }
   ```
   (El segundo chequeo `if (target != null)` después del `while` cubre el caso borde de que `target` muera exactamente en el último frame antes de la asignación final.)
2. Revisar los llamadores de `BeginTransition` (p. ej. `PlayerInputDriver.DeathSequence`, `TryPossess`) para confirmar que ya tienen su propio manejo de "target murió", pero que ahora, al menos, no dejarán la cámara trabada — es una salvaguarda de última línea, no reemplaza lógica de gameplay.

**Verificación:** No es trivial de probar sin Play mode real porque depende de una corrutina y `Time.deltaTime`. Documentar como prueba de Play mode: poseer a un aliado con `[F]`, en el mismo instante en que arranca la transición (`BeginTransition`), matarlo con daño masivo desde otra fuente (o forzar `Destroy(target.gameObject)` vía consola de depuración) y confirmar que `CameraRig.IsTransitioning` vuelve a `false` en menos de `duration` segundos y que la cámara no queda congelada (se puede seguir moviendo con W/A/S/D tras la muerte). Alternativamente, si se quiere cobertura headless, extraer la lógica de nulidad a un método estático puro testeable (fuera de alcance de este bug, ya que el bug pedido es específicamente sobre la corrutina).

**Riesgo/efectos secundarios:** Verificar que `yield break` dentro del nuevo bloque no deja al `Coroutine` en un estado que `StopCoroutine` en `BeginTransition` (línea 229) no pueda manejar — es código estándar de Unity, no debería haber problema. Confirmar visualmente que el "congelamiento" en la posición `fromPos/fromRot` (en vez de saltar a algún otro punto) es aceptable como comportamiento de fallback.

---

### Bug 3: `IssueAttackOrder` no limpia `mountTarget`/`orderQueue`, puede causar un montaje de vehículo no deseado
**Archivos:** `Ai/AiBrain.cs:278-284` (`IssueAttackOrder`)

**Causa raíz:** Todas las demás órdenes (`IssueMoveOrder` línea 233-249, `IssueMountOrder` línea 251-260, `IssueFollowOrder` línea 266-276) limpian `mountTarget = null` (o lo fijan explícitamente) y `orderQueue.Clear()` antes de aplicar la nueva orden. `IssueAttackOrder` es la única hermana que NO lo hace: si un soldado tenía un `IssueMountOrder` pendiente (`mountTarget` apuntando a un vehículo, en camino) y en ese momento se le da una orden de atacar a un enemigo, `mountTarget` sigue viva. Combinado con el Bug 1 (el `Tick()` puede recaer en `MovingToOrder` tras terminar el ataque porque `hasOrder` sigue en `true`), el soldado vuelve a `MovingToOrder` con `mountTarget != null` todavía seteado, y al llegar a `orderDestination` (el bug 1 aparte) ejecuta `mountTarget.Mount(self)` — un montaje de vehículo que el jugador nunca pidió después de dar la orden de atacar.

**Plan de implementación:**
1. Igualar `IssueAttackOrder` al patrón de sus hermanas, limpiando explícitamente `mountTarget` y `orderQueue`:
   ```csharp
   public void IssueAttackOrder(Soldier enemy)
   {
       if (!bootstrapped) Bootstrap();
       target = enemy;
       hasOrder = true;
       mountTarget = null;      // Bug 3: una orden de ataque pisa cualquier Mount en curso, igual que las demas Issue*
       orderQueue.Clear();      // idem: no deben resumirse waypoints viejos si el ataque termina volviendo a MovingToOrder
       orderDestination = self.transform.position; // ver Bug 1
       SetState(AiState.MovingToAttackOrder);
   }
   ```
2. Este cambio es defensivo y complementa la corrección de fondo del Bug 1 (con `orderIsAttack` ya no se debería recaer en `MovingToOrder` en absoluto), pero limpiar `mountTarget` aquí es correcto por sí mismo, independientemente de esa corrección: nunca debe quedar un `mountTarget` de una orden anterior colgando de una orden nueva no relacionada.

**Verificación:** Nuevo `Check` en `RunPhase2` o `RunPhase6` (donde ya se prueban `IssueMountOrder`/`IssueAttackOrder`): dar `IssueMountOrder(vehicle)` a un soldado, inmediatamente (antes de que llegue) dar `IssueAttackOrder(enemigo)`, simular hasta que el enemigo muera, y verificar `brain.CurrentOrderDestination` es `null` o que el soldado NO terminó montado en el vehículo:
   ```csharp
   var mountBrain = kes.GetComponent<AiBrain>();
   OrderService.IssueMountOrder(kes, vehicle); // o mountBrain.IssueMountOrder si no hay wrapper
   mountBrain.IssueAttackOrder(algunEnemigo);
   SimulateUntil(() => !algunEnemigo.Health.IsAlive, 10f);
   Check("Una orden de atacar dada sobre un Mount pendiente no deja al soldado montado despues",
       vehicle.RoleOf(kes) == null);
   ```

**Riesgo/efectos secundarios:** Ninguno esperado: es un `null`/`Clear()` adicional, simétrico a las otras tres `Issue*`. Verificar que no rompe el flujo normal donde `IssueAttackOrder` se da sin ningún Mount/cola pendiente (el caso común), donde `mountTarget` y `orderQueue` ya estarían vacíos de todos modos.

---

### Bug 4: `Bootstrap()` de `Soldier` no valida `GetComponent<Health>()`
**Archivos:** `Actors/Soldier.cs:78-83` (`Bootstrap`)

**Causa raíz:** `Bootstrap()` asume que el `GameObject` siempre trae un componente `Health` (línea 78: `health = GetComponent<Health>();`) y lo usa sin chequeo en la línea siguiente: `health.Initialize(Id, maxHealth);`. Si el prefab/objeto se construye sin `Health` adjunto (prefab mal armado, un test que crea un `Soldier` "a mano" sin todas las piezas, o una futura variante de prefab que lo omita por error), esa línea lanza una `NullReferenceException` dentro de `Bootstrap()`, que además puede ser invocado desde `Awake()`, desde cualquiera de los getters perezosos (`Health`, `Motor`, `Weapon`, `Brain` en líneas 41-44), o desde `AiBrain.Bootstrap()` — así que el punto de fallo real puede estar lejos del prefab roto, dificultando el diagnóstico.

**Plan de implementación:**
1. Agregar una validación explícita justo después de los `GetComponent` en `Bootstrap()`, con un mensaje de error claro (patrón estándar de Unity: `Debug.LogError` + salida temprana, evitando el NRE en cascada):
   ```csharp
   public void Bootstrap()
   {
       if (bootstrapped) return;
       bootstrapped = true;

       Id = nextId++;
       health = GetComponent<Health>();
       motor = GetComponent<SoldierMotor>();
       weapon = GetComponent<WeaponHolder>();
       aiBrain = GetComponent<AiBrain>();

       if (health == null)
       {
           Debug.LogError($"Soldier '{name}' no tiene un componente Health adjunto: no se puede inicializar.", this);
           return; // deja bootstrapped=true para no reintentar en bucle, pero sin tocar un health nulo
       }

       health.Initialize(Id, maxHealth);
       ActorRegistry.Register(this);
   }
   ```
2. Decidir conscientemente el resto de los componentes (`motor`, `weapon`, `aiBrain`): son usados con `?.` en varios lugares (p. ej. `s.Weapon?.Tick` no existe tal cual, pero `WorldSimulationDriver.Step` sí chequea `s.Weapon != null` en la línea 68) así que no es estrictamente necesario replicarles la misma guarda ahora — pero si se quiere blindar totalmente, aplicar el mismo patrón de log + return temprano para cualquiera que sea `null`, ya que de lo contrario el registro en `ActorRegistry` (línea 84) se saltea igual por el `return` de arriba y el soldado queda "fantasma" (nunca registrado, nunca sensado, pero tampoco crashea el resto del frame).
3. Mantener el `return` temprano ANTES de `ActorRegistry.Register(this)`: registrar un soldado sin `Health` inicializado sería peor (rompería `SenseNearestEnemy`, `FindNearestEnemyInRange`, etc. al leer `Health.IsAlive` sobre un componente sin `Initialize`).

**Verificación:** Añadir un test en `RunPhase5` (sección de regresión) que construya un `GameObject` mínimo con `Soldier` pero SIN `Health`, llame `Bootstrap()` dentro de un `try/catch`, y confirme que no lanza excepción:
   ```csharp
   var brokenGO = new GameObject("SoldadoSinHealth");
   var brokenSoldier = brokenGO.AddComponent<Soldier>();
   bool threw = false;
   try { brokenSoldier.Bootstrap(); } catch { threw = true; }
   Check("Soldier.Bootstrap() sin componente Health no lanza NRE (falla controlada con log)", !threw);
   UnityEngine.Object.DestroyImmediate(brokenGO);
   ```
   Nota: como `Awake()` ya llama `Bootstrap()` automáticamente al hacer `AddComponent<Soldier>()`, el `try/catch` explícito puede no ser necesario, pero conviene dejarlo por si el orden de ejecución de `Awake` cambia.

**Riesgo/efectos secundarios:** Confirmar que ningún prefab real del proyecto (`BuildAndSaveSoldierPrefab` en `HeadlessTestRunner.cs`) le falta el componente `Health` — si le faltara, este cambio pasaría de "crash silencioso pero visible" a "el soldado existe pero no hace nada", lo cual podría ocultar el problema real en vez de resolverlo; por eso el `Debug.LogError` es importante para no perder visibilidad.

---

### Bug 5: Un soldado en `Follow` no reacciona a que le disparen directamente
**Archivos:** `Ai/AiBrain.cs:208` (`OnAnyDamage`)

**Causa raíz:** `OnAnyDamage` maneja el caso "me dispararon a mí" (línea 206-215) con la condición `if (State == AiState.Idle || State == AiState.Patrol || State == AiState.MovingToOrder)` antes de reaccionar pasando a `Chase`. `AiState.Follow` no está en esa lista, así que un soldado siguiendo al jugador (`IssueFollowOrder`) que recibe daño directo simplemente lo ignora y sigue caminando detrás del líder como si nada — un comportamiento incoherente con Idle/Patrol/MovingToOrder, que sí reaccionan, y peor aún que el caso "le dispararon a un aliado cerca" (línea 217-227), que si excluye explícitamente a Follow por diseño (`if (State != AiState.Idle && State != AiState.Patrol) return;`) pero ESE caso es sobre un tercero, no sobre uno mismo.

**Plan de implementación:**
1. Agregar `AiState.Follow` a la condición de la línea 208, siguiendo exactamente el mismo patrón ya usado ahí (reaccionar aunque esté fuera del rango de visión normal):
   ```csharp
   // Me dispararon a mí: reacciono aunque esté fuera de mi rango de visión normal.
   if (evt.TargetId == self.Id)
   {
       if (State == AiState.Idle || State == AiState.Patrol || State == AiState.MovingToOrder || State == AiState.Follow)
       {
           target = attacker;
           hasOrder = false;
           SetState(AiState.Chase);
       }
       return;
   }
   ```
2. Confirmar que `hasOrder = false` es lo correcto también para Follow: al entrar en `Chase`, el `Tick()` (línea 327-328) usará `hasOrder` para decidir a dónde volver cuando el combate termine. Como `Follow` se activa con `hasOrder = true` (línea 271) y NO usa `orderDestination`, después de este cambio, cuando el combate termine, el soldado volverá a `MovingToOrder` (porque `hasOrder` seguirá en su valor real salvo que se ponga en `false` aquí) en vez de volver a `Follow` — hay que decidir el comportamiento deseado. Como el propio comentario del código dice que Follow "se corta si lo cancelan o lo interrumpe el combate", lo correcto es que, tras terminar el combate, el soldado NO retome el Follow automáticamente sino que caiga a `Patrol` (comportamiento consistente con cómo YA se corta Follow por sensado normal en la guarda `State != Chase && State != Attack` de la línea 335, que si permite interrumpir Follow). Dejar `hasOrder = false` como está arriba es correcto y coherente con esa semántica ya existente (Follow interrumpido por combate no se retoma solo).
3. Nota: también hay que limpiar `followTarget = null` para que, al volver de `Chase`/`Attack`, el `Tick()` de la línea 328 (`followTarget != null ? AiState.Follow : AiState.MovingToOrder`) no intente retomar un Follow "fantasma" con `hasOrder=false` pero `followTarget` todavía asignado (aunque como `hasOrder` ya se puso en `false`, esa rama nunca se alcanza porque el ternario de la 328 solo importa cuando `!hasOrder` es falso — revisar: la expresión es `!hasOrder ? Patrol : (followTarget != null ? Follow : MovingToOrder)`, así que con `hasOrder=false` cae directo a `Patrol` sin mirar `followTarget`; limpiar `followTarget` igual es buena higiene para que `FollowTarget` (propiedad pública, línea 176) no exponga un valor obsoleto mientras el estado ya cambió a Chase).
   ```csharp
   if (State == AiState.Idle || State == AiState.Patrol || State == AiState.MovingToOrder || State == AiState.Follow)
   {
       target = attacker;
       hasOrder = false;
       followTarget = null; // no reanudar un Follow viejo cuando termine este combate
       SetState(AiState.Chase);
   }
   ```

**Verificación:** Añadir en `RunPhase6` (donde ya existe la sección de `IssueFollowOrder` alrededor de la línea 1461) un test de daño directo durante Follow:
   ```csharp
   OrderService.IssueFollowOrder(kes, vega);
   Check("Kes esta en Follow antes del disparo", kesBrain.State == AiState.Follow);
   kes.Health.TakeDamage(10, enemy1.Id); // dispara DamageTakenEvent -> OnAnyDamage
   Check("Un soldado en Follow que recibe daño directo pasa a Chase", kesBrain.State == AiState.Chase);
   Check("El atacante queda como target", kesBrain.CurrentTarget != null);
   ```

**Riesgo/efectos secundarios:** Verificar que el test de Follow ya existente (líneas 1461-1483, que mide que la distancia se reduce y luego que `CancelOrder`/desactivación del líder funcionan) no reciba daño accidental de otra fuente en el medio de la simulación (los 400 ticks de `Tick(0.05f)` en la línea 1466 corren en una esquina vacía del mapa a propósito, según el comentario de la línea 1440-1448, así que no debería haber interferencia).

---

### Bug 6: El destino de `IssueMountOrder` es una foto fija; si el vehículo se mueve, el soldado camina al punto viejo y monta ahí igual
**Archivos:** `Ai/AiBrain.cs:251-260` (`IssueMountOrder`), `Ai/AiBrain.cs:363-372` (`Tick`, case `MovingToOrder`)

**Causa raíz:** `IssueMountOrder` (línea 258) hace `orderDestination = vehicle.transform.position;` UNA SOLA VEZ, en el momento de dar la orden. A diferencia de `Follow` (que recalcula el destino cada `Tick` contra `followTarget.transform.position`, línea 405), `MovingToOrder` usa el `orderDestination` fijo guardado en ese momento (línea 364: `self.Motor.MoveTowards(orderDestination, arriveThreshold, dt)`). Si el vehículo se mueve mientras el soldado camina hacia él (otro jugador lo conduce, o su propia IA lo desplaza), el soldado llega al punto donde el vehículo SOLÍA estar, y como ese punto está dentro de `arriveThreshold` de la posición vieja (no de la actual), `Tick()` ejecuta `mountTarget.Mount(self)` en la línea 369 de todos modos — intentando montar sin estar realmente cerca del vehículo actual (que puede estar lejos), lo que en el mejor caso falla silenciosamente en `Vehicle.Mount` (que sí valida algo internamente) y en el peor dejaría al soldado plantado en medio de la nada pensando que "llegó".

**Plan de implementación:**
1. Cambiar el `case AiState.MovingToOrder` en `Tick()` para que, cuando `mountTarget != null`, recalcule el destino contra la posición ACTUAL del vehículo en cada tick, en vez de depender del snapshot guardado en `IssueMountOrder`. El patrón correcto es el mismo que ya usa `Follow` (línea 401-405):
   ```csharp
   case AiState.MovingToOrder:
       // Bug 6: si hay un vehiculo objetivo, el destino se recalcula
       // cada tick contra su posicion ACTUAL -- igual que Follow -- en
       // vez de perseguir la foto fija que tomo IssueMountOrder. Sin
       // esto, un vehiculo que se mueve deja al soldado caminando a un
       // punto viejo y montando "en el aire".
       Vector3 moveTarget = mountTarget != null ? mountTarget.transform.position : orderDestination;
       if (self.Motor.MoveTowards(moveTarget, arriveThreshold, dt))
       {
           if (mountTarget != null)
           {
               hasOrder = false;
               mountTarget.Mount(self);
               mountTarget = null;
               return; // el GameObject quedó inactivo: no tocar más estado.
           }

           EventBus.Instance.Publish(new OrderCompletedEvent(self.Id));

           if (orderQueue.Count > 0)
           {
               orderDestination = orderQueue.Dequeue();
               break;
           }

           hasOrder = false;
           SetState(AiState.Patrol);
       }
       break;
   ```
2. Opcional pero recomendable: si el vehículo se destruye o queda fuera de alcance mientras el soldado camina hacia él, agregar una guarda similar a la de `Follow` (línea 394) para soltar la orden en vez de perseguir un vehículo destruido para siempre:
   ```csharp
   if (mountTarget != null && (mountTarget.gameObject == null || mountTarget.IsDestroyed))
   {
       hasOrder = false;
       mountTarget = null;
       SetState(AiState.Patrol);
       break;
   }
   ```
   (Insertar esta guarda ANTES del `MoveTowards`, al principio del case.) Nota: `Vehicle.IsDestroyed` ya existe (`Vehicles/Vehicle.cs:46`), así que reutilizarlo es consistente con el resto del código (`GOrderOnVehicle` en `PlayerInputDriver.cs` ya chequea `vehicle.IsDestroyed`).

**Verificación:** Nuevo test en `RunPhase4` o `RunPhase6` (donde ya se prueba `Mount`/vehículo): dar `IssueMountOrder` a un soldado lejos del vehículo, mover el vehículo varias veces mientras el soldado todavía está en camino, y confirmar que termina montando cerca de la posición FINAL, no de la inicial:
   ```csharp
   var mountTestBrain = doc.GetComponent<AiBrain>();
   vehicle.transform.position = new Vector3(50f, 0.6f, 50f);
   doc.transform.position = vehicle.transform.position + new Vector3(-20f, 0f, 0f);
   mountTestBrain.IssueMountOrder(vehicle);
   for (int i = 0; i < 30; i++) mountTestBrain.Tick(0.05f); // avanza un poco
   vehicle.transform.position += new Vector3(15f, 0f, 0f); // el vehiculo se mueve mientras el soldado camina
   bool docMounted = SimulateUntil(() => vehicle.RoleOf(doc) != null, 10f);
   Check($"Doc monto el vehiculo pese a que se movio mientras caminaba (distancia final al vehiculo actual chica)", docMounted);
   ```

**Riesgo/efectos secundarios:** Este cambio hace que `MoveTowards` reciba un destino que cambia de frame a frame cuando hay `mountTarget`; verificar que `SoldierMotor.MoveTowards` (no leído en este audit, pero usado igual por `Follow`) tolera bien un destino móvil sin oscilar ni "vibrar" al llegar — como `Follow` ya usa exactamente ese patrón sin problemas reportados, el riesgo es bajo. También verificar que `CurrentOrderDestination` (línea 171, usado por `FindNextSquadmateToBoard` en `PlayerInputDriver.cs`) sigue siendo razonable: ahora, mientras `mountTarget != null`, `orderDestination` deja de actualizarse en el campo mismo (solo se usa una variable local `moveTarget`), así que `CurrentOrderDestination` seguiría devolviendo el snapshot viejo de `IssueMountOrder`. Si eso importa (por ejemplo para el chequeo de "ya va camino a este vehículo" en `FindNextSquadmateToBoard`, línea ~1099-1101), considerar actualizar también el campo `orderDestination = moveTarget;` dentro del case para mantenerlo fresco.

---

### Bug 7: `recoilPitch` no tiene tope superior
**Archivos:** `Camera/CameraRig.cs:98` (`KickRecoil`)

**Causa raíz:** `pitch` está acotado con `Mathf.Clamp(pitch + delta, -MaxPitch, MaxPitch)` en `AddPitch` (línea 88) y `shakeOffset` está acotado con `Vector3.ClampMagnitude(shakeOffset, maxShakeMagnitude)` en `KickDirectional` (línea 121). En cambio `KickRecoil` (línea 98) simplemente hace `recoilPitch += degrees;` sin ningún límite. Con disparos sostenidos (ráfaga de un arma automática, o varias fuentes de recoil sumando — jugador + torreta, si compartieran el mismo `CameraRig`), `recoilPitch` puede crecer sin límite antes de que `MoveTowards` en `LateUpdate` (línea 63) alcance a bajarlo, lo que se traduce en una cámara que sube la mira muy por encima de cualquier ángulo jugable (recordar que se usa como `-(pitch + recoilPitch)` en `FollowFps`, línea 258).

**Plan de implementación:**
1. Agregar una constante de tope máximo, siguiendo el mismo patrón que `MaxPitch` (línea 33), y aplicar `Mathf.Clamp` en `KickRecoil`:
   ```csharp
   float recoilPitch;
   [SerializeField] float recoilRecoverySpeed = 40f; // grados/seg
   [SerializeField] float maxRecoilPitch = 25f; // tope duro: varias fuentes sumando no deben mandar la mira al cielo
   public void KickRecoil(float degrees) => recoilPitch = Mathf.Clamp(recoilPitch + degrees, 0f, maxRecoilPitch);
   public float RecoilPitch => recoilPitch;
   ```
   Nota: el clamp inferior en `0f` (no negativo) porque `KickRecoil` siempre se llama con `degrees` positivos (ver `PlayerInputDriver.OnShotFiredForRecoil`, línea 131: `Rig.KickRecoil(kickDeg)` con `kickDeg` siempre `>= 0.6f`), y `recoilPitch` decae hacia `0f` en `LateUpdate` vía `MoveTowards` (línea 63) — nunca debería ir a negativo por diseño.
2. Elegir el valor de `maxRecoilPitch` con criterio de jugabilidad: debe ser sensiblemente menor que `MaxPitch` (80°) para que el culatazo nunca "tape" completamente la posibilidad de mirar hacia abajo o girar la cámara con normalidad. Un valor en el rango 15°-30° es razonable dado que el culatazo por disparo individual ya está acotado en `PlayerInputDriver.cs:130` a `Mathf.Clamp(spec.Damage * 0.025f, 0.6f, 3f)` grados — con `maxRecoilPitch = 25f` se permiten ~8-40 disparos acumulados antes de topar, dependiendo del arma, lo cual da margen realista para ráfagas sostenidas sin volverse infinito.

**Verificación:** Extender el test ya existente de `KickDirectional`/presupuesto de sacudida en `RunPhase5` (línea ~1239-1253) con un bloque análogo para `KickRecoil`:
   ```csharp
   if (rig != null)
   {
       float recoilBefore = rig.RecoilPitch;
       for (int i = 0; i < 50; i++) rig.KickRecoil(5f); // 250 grados pedidos, muy por encima de cualquier tope razonable
       Check($"KickRecoil queda acotado a un tope maximo ({rig.RecoilPitch:0.0} grados)",
           rig.RecoilPitch <= 30f + 0.01f); // usar el mismo valor que maxRecoilPitch, o exponerlo como propiedad publica para no hardcodear
   }
   ```
   Si se quiere evitar hardcodear el número mágico `30f` en el test, considerar exponer `public float MaxRecoilPitch => maxRecoilPitch;` como propiedad de solo lectura (mismo patrón que `MaxShakeMagnitude`, línea 114) y comparar contra esa propiedad en el `Check`.

**Riesgo/efectos secundarios:** Verificar visualmente en Play mode que un arma de alta cadencia (Heavy) con el nuevo tope no se sienta "cortado" de forma abrupta — como el decaimiento (`recoilRecoverySpeed = 40°/seg`) ya es rápido, el tope debería sentirse como un límite natural, no un corte brusco. Revisar también si algún otro sistema lee `RecoilPitch` esperando valores sin límite (no se encontró ninguno en el grep de este audit fuera de `CameraRig` y `FollowFps`/`FollowAnchor`... ver Bug 9).

---

### Bug 8: `WorldSimulationDriver` no tiene guarda de instancia única
**Archivos:** `Ai/WorldSimulationDriver.cs:11-13` (declaración de clase, sin `Awake`/`OnEnable`)

**Causa raíz:** `WorldSimulationDriver` no implementa ningún patrón de singleton: no hay `Awake()`, no hay `Instance`, no hay chequeo de duplicados. Cada instancia que exista en la escena registra su propio `Update()` (línea 13: `void Update() => Step(Time.deltaTime);`) con el sistema de mensajes de Unity, y como `Step` es un método `static` que opera sobre estado global (`SpatialGrid.Rebuild()`, `ActorRegistry.All`, `WorldSystemsRegistry.VehicleBrains/TurretWeapons/TurretAis`), si dos instancias del componente quedan activas a la vez (por ejemplo, una escena cargada aditivamente sobre otra que ya tenía la suya, o un error de construcción de escena que agregue el componente dos veces), CADA UNA llama `Step(dt)` en su propio `Update()`, duplicando literalmente: dos rebuilds del grid por frame, dos ticks de IA/armas por soldado (fuego al doble de cadencia real, doble avance de vehículos/torretas), con más impacto cuanto más entidades haya.

**Plan de implementación:**
1. Aplicar el mismo patrón de instancia única que ya usa `CameraRig` (línea 16-19: `public static Instance { get; private set; }` + `OnEnable`/`OnDisable`), adaptado a que `WorldSimulationDriver` además corre por `Update()` y no quiere que una segunda copia simule nada en absoluto:
   ```csharp
   public class WorldSimulationDriver : MonoBehaviour
   {
       public static WorldSimulationDriver Instance { get; private set; }

       void Awake()
       {
           // Guarda de instancia unica: dos WorldSimulationDriver activos
           // tickearian el mundo (IA, armas, vehiculos, torretas) dos
           // veces por frame cada uno -- silencioso pero grave (cadencia
           // de disparo real al doble, doble avance de vehiculos).
           if (Instance != null && Instance != this)
           {
               Debug.LogWarning($"Ya existe un WorldSimulationDriver activo ({Instance.name}); se desactiva esta segunda instancia ({name}) para no duplicar la simulacion.", this);
               enabled = false;
               return;
           }
           Instance = this;
       }

       void OnDestroy()
       {
           if (Instance == this) Instance = null;
       }

       void Update() => Step(Time.deltaTime);
       // ... resto sin cambios
   }
   ```
2. Preferir `enabled = false` (deja el componente vivo pero sin `Update`) antes que `Destroy(this)`/`Destroy(gameObject)`, porque el proyecto ya tiene el hábito de no destruir agresivamente objetos que otros sistemas podrían referenciar (ver comentarios de `CleanupDeathSequence` sobre limpieza cuidadosa) y porque `HeadlessTestRunner` agrega el componente sobre `servicesGO` (línea 668) — destruir el `GameObject` completo sería mucho más invasivo que necesario si el problema real es solo evitar el doble tick.
3. Confirmar que ningún código depende de más de un `WorldSimulationDriver` coexistiendo intencionalmente (no se encontró ninguno en el grep de este audit: la única instanciación es la línea 668 de `HeadlessTestRunner.cs`, vía `servicesGO.AddComponent<WorldSimulationDriver>()`, una sola vez por corrida).

**Verificación:** Añadir un test en `RunPhase5` (regresión) que instancie un segundo `WorldSimulationDriver` sobre un `GameObject` temporal y confirme que queda desactivado:
   ```csharp
   var dupGO = new GameObject("DupSimDriver");
   var dup = dupGO.AddComponent<WorldSimulationDriver>();
   Check("Un segundo WorldSimulationDriver se autodesactiva (guarda de instancia unica)", !dup.enabled);
   UnityEngine.Object.DestroyImmediate(dupGO);
   Check("Tras destruir el duplicado, el WorldSimulationDriver.Instance original sigue activo",
       SP.Ai.WorldSimulationDriver.Instance != null && SP.Ai.WorldSimulationDriver.Instance.enabled);
   ```
   Nota: en Edit mode (como corre `HeadlessTestRunner`), `Awake()` de un `AddComponent` se ejecuta sincrónicamente, así que este test es viable sin Play mode.

**Riesgo/efectos secundarios:** Revisar que `HeadlessTestRunner.BuildScene` no reconstruya la escena más de una vez sin destruir la anterior primero (si lo hiciera, el segundo `WorldSimulationDriver` quedaría desactivado y la suite dejaría de simular nada, en vez de duplicar — un fallo mucho más visible y fácil de diagnosticar que el bug original, así que es una mejora neta incluso en ese caso).

---

### Bug 9: `FollowAnchor` omite `recoilPitch`, que `FollowFps` sí aplica
**Archivos:** `Camera/CameraRig.cs:258` (`FollowFps`) vs `Camera/CameraRig.cs:267` (`FollowAnchor`)

**Causa raíz:** `FollowFps` (línea 258) calcula la rotación como `eye.rotation * Quaternion.Euler(-(pitch + recoilPitch), 0f, 0f)`, incluyendo el culatazo de cámara. `FollowAnchor` (línea 267), que según su propio comentario (línea 261-262: "sirve para el ojo de un soldado O EL ASIENTO DE UN VEHÍCULO") es el método usado cuando el jugador está en un vehículo, calcula `anchor.rotation * Quaternion.Euler(-pitch, 0f, 0f)` — sin sumar `recoilPitch`. El resultado es que el culatazo de cámara (`KickRecoil`, disparado por `PlayerInputDriver.OnShotFiredForRecoil` en CUALQUIER disparo del jugador, esté a pie o en un asiento de vehículo) es completamente invisible mientras el jugador está sentado en un vehículo (torreta, conductor), aunque el propio código de recoil no distingue el contexto y sigue acumulando `recoilPitch` igual.

**Plan de implementación:**
1. Igualar `FollowAnchor` a `FollowFps`, sumando `recoilPitch` de la misma forma:
   ```csharp
   public void FollowAnchor(Transform anchor)
   {
       if (anchor == null || IsTransitioning) return;
       transform.position = anchor.position;
       transform.rotation = anchor.rotation * Quaternion.Euler(-(pitch + recoilPitch), 0f, 0f);
   }
   ```
2. Nota importante: `FollowAnchor` también se usa desde `PlayerInputDriver.DeathSequence` (línea 552: `Rig.FollowAnchor(deathPullBackGO.transform)`), donde el recoil NO debería aplicarse (es la cámara de muerte orbitando, no una vista de disparo). Sin embargo, en ese contexto `recoilPitch` ya debería estar en `0` o cerca (nadie dispara durante la secuencia de muerte, y decae solo con `MoveTowards` en `LateUpdate`), así que el riesgo práctico es bajo — pero conviene revisar/documentar esto explícitamente en el comentario del método para que no sea una sorpresa futura:
   ```csharp
   // Primera persona genérica: sirve para el ojo de un soldado, el
   // asiento de un vehículo, o un punto de cámara arbitrario (ej. la
   // orbita de la secuencia de muerte). Aplica recoilPitch igual que
   // FollowFps -- el culatazo de disparo debe verse tambien adentro de
   // un vehiculo, que es el caso que motivo este metodo.
   public void FollowAnchor(Transform anchor)
   ```

**Verificación:** Extender la sección de `RunPhase6` que ya prueba "Sin vibración de camara al disparar" (línea ~1519-1541, que usa reflección para forzar `CameraRig.Instance`) con un chequeo específico de `recoilPitch` vía `FollowAnchor`:
   ```csharp
   rig.KickRecoil(0f); // no-op, solo para claridad
   float recoilBefore2 = rig.RecoilPitch;
   Rig.KickRecoil(5f); // simula un disparo
   var dummyAnchor = new GameObject("DummyVehicleSeat").transform;
   dummyAnchor.position = Vector3.zero;
   dummyAnchor.rotation = Quaternion.identity;
   rig.FollowAnchor(dummyAnchor);
   float pitchAngle = rig.transform.rotation.eulerAngles.x; // 0-360, revisar signo/wrap segun convencion del proyecto
   Check($"FollowAnchor aplica recoilPitch igual que FollowFps (rig.RecoilPitch={rig.RecoilPitch:0.0})",
       rig.RecoilPitch > 0f && pitchAngle != 0f);
   UnityEngine.Object.DestroyImmediate(dummyAnchor.gameObject);
   ```
   (Ajustar el chequeo del ángulo según cómo se quiera comparar `Quaternion.Euler` — puede ser más robusto comparar `rig.transform.rotation` contra el valor esperado calculado con la misma fórmula que `FollowFps`, en vez de leer `eulerAngles.x` crudo, que tiene wrap-around.)

**Riesgo/efectos secundarios:** Revisar visualmente en Play mode, adentro de un vehículo, disparando con la torreta o como pasajero armado, que el culatazo se sienta correcto y no excesivo combinado con cualquier otro efecto de cámara del vehículo (bamboleo por movimiento, si existiera). Confirmar que no se rompe el uso de `FollowAnchor` en la cámara de muerte (`DeathSequence`), donde `recoilPitch` debería estar en 0 de todos modos.

---

### Bug 10: `SetMode`/`ToggleMode` no reposicionan la cámara, solo cambian `Mode`/`orthographic`
**Archivos:** `Camera/CameraRig.cs:173-189` (`SetMode`), `Camera/CameraRig.cs:215` (`ToggleMode`)

**Causa raíz:** `SetMode` (línea 173-189) cambia `Mode` y `cam.orthographic`, y opcionalmente guarda `savedRtsPosition`/`savedRtsOrthoSize` al SALIR de RTS — pero nunca llama a ninguno de los métodos que sí mueven `transform.position`/`transform.rotation` (`SetRtsView`, `RestoreOrSetRtsView`, `FollowFps`, `FollowAnchor`). Quien llama a `SetMode` (por ejemplo `PlayerInputDriver.DeathSequence` línea 528 y 572, o `TryPossess` línea 1043) tiene que acordarse de llamar TAMBIÉN a `RestoreOrSetRtsView`/`BeginTransition`/`FollowFps` por su cuenta inmediatamente después — y de hecho la mayoría de los call-sites SÍ lo hacen (ver `PlayerInputDriver.cs:386-394`, que llama `Rig.RestoreOrSetRtsView(focus)` justo después del `Rig.ToggleMode()` de la línea 374). El problema real es que esto es un contrato implícito y frágil: si un futuro call-site (o un test) llama `SetMode`/`ToggleMode` solo, esperando razonablemente que la cámara "esté" en el modo nuevo, se encuentra con `Mode`/`cam.orthographic` cambiados pero la `transform` todavía apuntando a donde estaba en el modo anterior — una cámara ortográfica de arriba mirando desde un ángulo de FPS, o viceversa.

**Plan de implementación:**
1. Esto es una decisión de diseño, no un simple parche: hay dos caminos razonables.
   - **Camino A (mínimo, respeta el diseño actual):** Documentar explícitamente en el comentario de `SetMode` que es responsabilidad del llamador reposicionar la cámara después, y agregar una aserción de desarrollo (solo en editor/debug) que detecte el caso "cambié de modo pero la cámara sigue en la orientación anterior" — de bajo valor práctico, no recomendado como solución principal.
   - **Camino B (recomendado):** Hacer que `SetMode` sea responsable de dejar la cámara en un estado consistente con el nuevo modo, delegando en los métodos que YA existen para eso, con un `Vector3? centerHint` opcional para los casos donde no hay "restaurar vista guardada" aplicable:
   ```csharp
   public void SetMode(ControlMode mode, Vector3? rtsFallbackCenter = null)
   {
       bool wasRts = Mode == ControlMode.Rts;
       bool goingToRts = mode == ControlMode.Rts;

       if (wasRts && !goingToRts && cam != null)
       {
           savedRtsPosition = transform.position;
           savedRtsOrthoSize = cam.orthographicSize;
       }

       Mode = mode;
       if (cam != null) cam.orthographic = mode == ControlMode.Rts;

       // Bug 10: antes SetMode solo cambiaba el flag, dejando la
       // transform en la orientacion del modo anterior hasta que el
       // llamador se acordara de reposicionarla a mano. Ahora, si se
       // pide una vista RTS y hay un centro de referencia, se reubica
       // aca mismo -- el llamador puede seguir pisando esto con su
       // propio FollowFps/BeginTransition inmediatamente despues (por
       // ejemplo para una transicion suave), pero ya no queda un estado
       // a medio camino si no lo hace.
       if (goingToRts && !wasRts && rtsFallbackCenter.HasValue)
           RestoreOrSetRtsView(rtsFallbackCenter.Value);
   }

   public void ToggleMode(Vector3? rtsFallbackCenter = null) =>
       SetMode(Mode == ControlMode.Fps ? ControlMode.Rts : ControlMode.Fps, rtsFallbackCenter);
   ```
   Este camino B es intencionalmente conservador: NO reposiciona automáticamente al pasar a FPS (porque ahí el llamador casi siempre quiere una transición suave con `BeginTransition`, distinta según el soldado poseído), pero SÍ resuelve el caso RTS (donde `RestoreOrSetRtsView` ya existe justamente para esto y es barato de invocar siempre).
2. Actualizar los call-sites existentes en `PlayerInputDriver.cs` para pasar el nuevo parámetro opcional donde tenga sentido (opcional — el comportamiento por defecto sin argumento es no reposicionar, igual que antes, así que es retrocompatible), o dejarlos como están si prefieren seguir llamando `RestoreOrSetRtsView` explícitamente después (ambos caminos coexisten sin conflicto porque `RestoreOrSetRtsView` es idempotente).

**Verificación:** Nuevo test en `RunPhase5`/`RunPhase7` (regresión de cámara):
   ```csharp
   rig.SetRtsView(Vector3.zero); // estado inicial conocido: RTS en el origen
   rig.SetMode(ControlMode.Fps); // sin reposicionar explícitamente
   Vector3 posTrasFps = rig.transform.position;
   rig.SetMode(ControlMode.Rts, new Vector3(40f, 0f, 40f)); // ahora con fallback center
   Check($"SetMode a RTS con centro de referencia reposiciona la camara (pos={rig.transform.position})",
       Vector3.Distance(new Vector3(rig.transform.position.x, 0f, rig.transform.position.z), new Vector3(40f, 0f, 40f)) < 1f);
   ```

**Riesgo/efectos secundarios:** Es el cambio de mayor superficie de este lote porque toca la firma pública de `SetMode`/`ToggleMode` (parámetro opcional, así que no rompe binariamente los call-sites existentes en C#, pero conviene revisar TODOS los usos — `PlayerInputDriver.cs` líneas 374, 380 y 386-394 vía `Rig.ToggleMode()`/`Rig.RestoreOrSetRtsView`, `TryPossess` línea 1043 vía `Rig.SetMode(ControlMode.Fps)`, `DeathSequence` líneas 528 y 572 y 578 — para confirmar que ninguno queda haciendo doble trabajo (posicionar dos veces) de forma visualmente notoria (un salto en dos pasos en vez de uno). Probar en Play mode el flujo completo: TAB para alternar FPS/RTS varias veces seguidas, y la secuencia de muerte completa (queda en RTS al final si no hay aliados vivos, línea 578).

---

### Bug 11: `OrderCompletedEvent` se publica en cada tramo de una ruta con waypoints, no solo al llegar al destino final
**Archivos:** `Ai/AiBrain.cs:374-382` (`Tick`, case `MovingToOrder`)

**Causa raíz:** Cuando `IssueMoveOrder(point, queued: true)` encola varios puntos (línea 237-241: `orderQueue.Enqueue(point)`), el soldado camina de tramo en tramo. Al llegar a CADA tramo intermedio, la línea 374 publica `EventBus.Instance.Publish(new OrderCompletedEvent(self.Id))` INCONDICIONALMENTE, antes de revisar si hay más puntos en `orderQueue` (línea 378-382). El evento `OrderCompletedEvent` debería significar semánticamente "el soldado terminó de cumplir la orden que se le dio" (útil por ejemplo para que la UI saque el marcador de destino, o para que `FindNextSquadmateToBoard` sepa que ya no está "en camino"), pero con una ruta de 3 waypoints planificados, se publican 3 eventos — uno por cada tramo — en vez de uno solo al llegar al último punto.

**Plan de implementación:**
1. Mover la publicación del evento a DESPUÉS de comprobar si quedan más puntos en la cola, para que solo se dispare cuando la ruta realmente termina:
   ```csharp
   case AiState.MovingToOrder:
       if (self.Motor.MoveTowards(orderDestination, arriveThreshold, dt))
       {
           if (mountTarget != null)
           {
               hasOrder = false;
               mountTarget.Mount(self);
               mountTarget = null;
               return; // el GameObject quedó inactivo: no tocar más estado.
           }

           // Bug 11: el evento de "orden cumplida" es sobre la RUTA
           // completa, no sobre cada tramo intermedio -- se publica
           // recien cuando no queda ningun waypoint mas por recorrer.
           if (orderQueue.Count > 0)
           {
               orderDestination = orderQueue.Dequeue();
               break;
           }

           EventBus.Instance.Publish(new OrderCompletedEvent(self.Id));
           hasOrder = false;
           SetState(AiState.Patrol);
       }
       break;
   ```
2. Confirmar que ningún suscriptor de `OrderCompletedEvent` dependía (aunque fuera accidentalmente) de recibir un evento por tramo intermedio — buscar todos los `Subscribe<OrderCompletedEvent>` del proyecto antes de aplicar el cambio, para no romper una función que hoy "funciona por casualidad" gracias al bug.

**Verificación:** Nuevo test en la fase donde ya se prueba `orderQueue` (buscar en `HeadlessTestRunner.cs` el uso de `QueuedOrderCount`/`QueuedDestinations`, o agregar uno si no existe): dar una orden encolada de 3 puntos, suscribirse a `OrderCompletedEvent`, simular hasta que el soldado llegue al final, y contar cuántas veces se publicó el evento:
   ```csharp
   int completedCount = 0;
   var sub = EventBus.Instance.Subscribe<SP.Core.OrderCompletedEvent>(e => { if (e.ActorId == vega.Id) completedCount++; });
   var vegaBrain = vega.GetComponent<AiBrain>();
   Vector3 baseP = vega.transform.position;
   vegaBrain.IssueMoveOrder(baseP + new Vector3(5f, 0f, 0f));
   vegaBrain.IssueMoveOrder(baseP + new Vector3(10f, 0f, 0f), queued: true);
   vegaBrain.IssueMoveOrder(baseP + new Vector3(15f, 0f, 0f), queued: true);
   SimulateUntil(() => vegaBrain.State == AiState.Patrol, 15f);
   sub.Dispose();
   Check($"OrderCompletedEvent se publica UNA sola vez para una ruta de 3 tramos (se publico {completedCount} veces)",
       completedCount == 1);
   ```

**Riesgo/efectos secundarios:** Revisar `AutoDemoRunner.cs` y cualquier UI (marcador de orden, `OrderMarkerFx`) que escuche `OrderCompletedEvent` esperando el timing viejo (por-tramo) — si alguna lógica de feedback visual dependía de ver "llegó a un punto" en cada tramo, ese feedback ahora solo llegará al final; si hace falta un evento por tramo intermedio para UI, ese debería ser un evento NUEVO y distinto (p. ej. `OrderLegCompletedEvent`), no reutilizar `OrderCompletedEvent` para dos semánticas distintas.

---

### Bug 12: El contador estático `nextId` de `Soldier` nunca se resetea sin domain reload
**Archivos:** `Actors/Soldier.cs:19` (`static int nextId = 1;`), `Actors/Soldier.cs:77` (`Id = nextId++;`)

**Causa raíz:** `nextId` es un campo `static`, así que vive a nivel de `AppDomain`, no de escena ni de sesión de Play. En Play mode normal del Editor, un domain reload (por defecto, al entrar a Play) lo resetea a `1`. Pero `HeadlessTestRunner` corre en Edit mode y puede invocarse varias veces en la misma sesión de Editor sin recargar el dominio (por ejemplo, correr `RunAll()` dos veces seguidas, o `RunAll()` seguido de `RunEquivalenceCheck()` en la misma sesión — ambos existen y ambos spawnean soldados con `SpawnSoldier`). Cada corrida sucesiva sigue incrementando `nextId` desde donde quedó la anterior, así que los `Id` de los soldados de la segunda corrida NO empiezan en `1` — cualquier lógica de test que asuma IDs bajos/predecibles, o cualquier comparación entre corridas (el propio `RunEquivalenceCheck`, que compara resultados "antes/después" según el comentario de la línea 254-256: "para que dos benchmarks... sean comparables entre sí") puede volverse sutilmente no determinista según cuántas veces se corrió la suite antes en esa sesión de Editor.

**Plan de implementación:**
1. Agregar un método de reseteo explícito, testeable, siguiendo el patrón ya usado en el proyecto para resets controlados (ej. `EventBus.Instance.ClearAll()` en `Core/EventBus.cs:41`, pensado explícitamente "solo para tests/reinicios de escena"):
   ```csharp
   public class Soldier : MonoBehaviour
   {
       [SerializeField] string displayName;
       [SerializeField] TeamId team;
       [SerializeField] RoleType role;
       [SerializeField] int maxHealth = 100;

       static int nextId = 1;

       // Solo para tests/reinicios de escena en Edit mode, donde no hay
       // domain reload entre corridas: sin esto, correr la suite dos
       // veces en la misma sesion de Editor da Id's que no arrancan en 1,
       // rompiendo cualquier comparacion "antes/despues" entre corridas
       // (ej. RunEquivalenceCheck) o cualquier aserción que asuma IDs bajos.
       public static void ResetIdCounterForTests() => nextId = 1;

       bool bootstrapped;
       ...
   }
   ```
2. Llamar a `Soldier.ResetIdCounterForTests()` al principio de `HeadlessTestRunner.RunAll()` (y de `RunEquivalenceCheck()` y de cualquier otro punto de entrada equivalente que construya una escena de test desde cero), ANTES de crear el primer `Soldier`. Buscar el inicio de `RunAll()` (línea 157) y del método `BuildScene`/setup inicial para insertar la llamada en el lugar donde ya se hace limpieza equivalente (por ejemplo, si ya existe algo como `EventBus.Instance.ClearAll()` o `ActorRegistry.Clear()` al principio, agregar la línea justo al lado, siguiendo la misma agrupación).
3. Alternativa más autocontenida (evita depender de que cada punto de entrada se acuerde de llamarlo): resetear el contador dentro de la propia `SpawnSoldier` de `HeadlessTestRunner.cs` NO es correcto (eso reiniciaría el contador en cada soldado, causando IDs duplicados entre soldados de la MISMA corrida) — por eso el reseteo debe vivir en el punto de "arranca una corrida nueva de la suite", no en el de "se crea un soldado".

**Verificación:** Nuevo test en `RunPhase1` (al principio, antes de cualquier otra cosa) o como parte de la infraestructura de `RunAll()`:
   ```csharp
   // Verificable indirectamente: el primer soldado de esta corrida debe
   // tener Id == 1 si el reseteo se aplico correctamente al arrancar.
   Check($"El contador de Id de Soldier arranca en 1 en cada corrida de la suite (vega.Id={vega.Id})", vega.Id == 1);
   ```
   Para una verificación más directa y aislada (sin depender del orden de creación de `vega`), agregar en `RunPhase5` un test unitario puro:
   ```csharp
   Soldier.ResetIdCounterForTests();
   var tempGO1 = new GameObject("TempSoldierIdTest1");
   var tempSoldier1 = tempGO1.AddComponent<Soldier>(); // Awake -> Bootstrap -> Id = nextId++
   Check($"ResetIdCounterForTests deja el proximo Id en 1 (obtenido: {tempSoldier1.Id})", tempSoldier1.Id == 1);
   UnityEngine.Object.DestroyImmediate(tempGO1);
   Soldier.ResetIdCounterForTests(); // no dejar el contador corrido para el resto de la suite
   ```

**Riesgo/efectos secundarios:** IMPORTANTE: resetear `nextId` a mitad de una corrida (en vez de solo al principio) causaría IDs DUPLICADOS entre soldados vivos simultáneamente, lo cual rompería `ActorRegistry`, `Health.ActorId`, y cualquier lookup por Id — por eso el reseteo debe llamarse EXCLUSIVAMENTE al arrancar una corrida nueva, nunca en medio. Revisar con cuidado que `RunAll()` y `RunEquivalenceCheck()` (y cualquier otro entry point de test) no se llamen nunca de forma anidada o concurrente entre sí en la misma sesión sin pasar primero por el reseteo.

---

### Bug 13: `IssueMountOrder` tampoco limpia `orderQueue` (hermano del Bug 3)
**Archivos:** `Ai/AiBrain.cs:251-260` (`IssueMountOrder`)

**Causa raíz:** Igual que el Bug 3 pero en el método hermano: `IssueMountOrder` (línea 251-260) limpia `target = null` y fija `mountTarget`, pero NO llama `orderQueue.Clear()` (a diferencia de `IssueMoveOrder`, línea 246, y `IssueFollowOrder`, línea 273). Si un soldado tenía una ruta con waypoints encolados (`IssueMoveOrder(..., queued: true)` varias veces) y en medio de esa ruta se le da una orden de montar un vehículo, al llegar y montar (línea 369, `mountTarget.Mount(self)`), el soldado se desactiva (`return` en la línea 371, comentario "el GameObject quedó inactivo"). Si más adelante ese mismo soldado se desmonta del vehículo y retoma control de IA normal, `orderQueue` todavía contiene los waypoints viejos de ANTES del Mount, y el `Tick()` en su próximo ciclo de `MovingToOrder` (si algo vuelve a poner `hasOrder=true` con una orden nueva que llegue al final normalmente, línea 378-382) los "resucita", moviendo al soldado a puntos que el jugador jamás pidió después de desmontar.

**Plan de implementación:**
1. Igualar `IssueMountOrder` al patrón de `IssueMoveOrder`/`IssueFollowOrder`, agregando `orderQueue.Clear()`:
   ```csharp
   public void IssueMountOrder(Vehicle vehicle)
   {
       if (vehicle == null) return;
       if (!bootstrapped) Bootstrap();
       target = null;
       hasOrder = true;
       mountTarget = vehicle;
       orderQueue.Clear(); // Bug 13: sin esto, waypoints planificados antes del Mount resucitaban despues de desmontar
       orderDestination = vehicle.transform.position;
       SetState(AiState.MovingToOrder);
   }
   ```
2. Este cambio es independiente de la corrección del Bug 6 (destino recalculado dinámicamente) y del Bug 3 — los tres tocan métodos vecinos pero cada uno corrige un descuido distinto; aplicarlos juntos no genera conflicto porque tocan líneas distintas dentro de cada método.

**Verificación:** Nuevo test (puede ir junto al de Bug 3 en la misma sección de `RunPhase6`):
   ```csharp
   var queueTestBrain = doc.GetComponent<AiBrain>();
   queueTestBrain.IssueMoveOrder(doc.transform.position + new Vector3(5f, 0f, 0f));
   queueTestBrain.IssueMoveOrder(doc.transform.position + new Vector3(10f, 0f, 0f), queued: true);
   queueTestBrain.IssueMoveOrder(doc.transform.position + new Vector3(15f, 0f, 0f), queued: true);
   Check("Hay 2 waypoints encolados antes del Mount", queueTestBrain.QueuedOrderCount == 2);
   queueTestBrain.IssueMountOrder(vehicle);
   Check("IssueMountOrder limpia la cola de waypoints previa", queueTestBrain.QueuedOrderCount == 0);
   ```

**Riesgo/efectos secundarios:** Ninguno esperado, es simétrico al Bug 3. Verificar que no había NINGÚN flujo intencional que dependiera de "montar sin perder la ruta planificada para después de desmontar" — no se encontró ninguno en el código leído; la intención original de `orderQueue` (comentario línea 98-99 de `AiBrain.cs`) es exclusivamente sobre `IssueMoveOrder` encadenado.

---

### Bug 14: `PathPreview.Attach` nunca se conecta en producción — la vista previa de ruta con obstáculos jamás se activa en gameplay real
**Archivos:** `Ai/PathPreview.cs:27,41` (campo `graph` y método `Attach`), `Player/PlayerInputDriver.cs:36` (campo `NavGraph`)

**Causa raíz:** `PathPreview.Attach(WaypointGraph)` (línea 41) es el único punto donde se asigna el campo `graph`, que a su vez determina si `Show()` usa pathfinding real (`graph.TryFindPath`, línea 81) o cae a una línea recta (`DrawStraight`, línea 76, cuando `graph == null`). El único llamador de `Attach` en TODO el proyecto es `HeadlessTestRunner.cs:706` (`pathPreview.Attach(navGraph)`), dentro de la construcción de escena de **Editor** para la suite de test. `PlayerInputDriver` tiene su propio campo `NavGraph` (línea 36, `[System.NonSerialized] public SP.Core.WaypointGraph NavGraph;`) con un comentario que dice "se arma al construir la escena, no lo calcula el driver" — y en efecto, `HeadlessTestRunner.cs:661` sí hace `inputDriver.NavGraph = inputDriverNavGraph;`. Pero **nada** conecta `inputDriver.NavGraph` con `PathPreview.Instance.Attach(...)`: son dos campos separados que reciben el MISMO `WaypointGraph` construido por `HeadlessTestRunner`, pero solo uno de los dos caminos (`PathPreview.Attach`, llamado directo por el propio `HeadlessTestRunner`) realmente lo enchufa. En producción real (una escena de juego construida de otra forma, sin pasar por `HeadlessTestRunner.BuildScene`, que es una herramienta de Editor y "Play mode no puede llamar a este script de Editor" según el propio comentario de la línea 776-780 de `HeadlessTestRunner.cs`), NADIE llama `PathPreview.Attach`, así que `PathPreview.Instance.graph` queda `null` para siempre y la vista previa de ruta SIEMPRE dibuja la línea recta ingenua (`DrawStraight`), sin rodear obstáculos, aunque el juego sí tenga un `WaypointGraph` real disponible en `PlayerInputDriver.NavGraph`.

**Plan de implementación:**
1. La causa de fondo es que `HeadlessTestRunner.BuildScene` ES la única forma que existe hoy de construir la escena completa de este proyecto (no hay un "GameplaySceneBootstrap" runtime separado que arme la escena en Play mode desde cero — el comentario de la línea 776-780 confirma que ese guión de construcción vive exclusivamente en Editor). Por lo tanto, el bug no es "falta un segundo lugar en runtime que llame Attach", sino que el ÚNICO lugar que construye la escena real (`HeadlessTestRunner.cs`, usado también para producir la escena que se juega) ya tiene AMBOS objetos (`pathPreview` y `inputDriver`) en el mismo método, pero solo conecta uno de los dos consumidores del grafo. La corrección más simple y directa es, en el mismo bloque donde ya se hacen ambas asignaciones (alrededor de `HeadlessTestRunner.cs:705-707`), asegurarse de que el campo de `PlayerInputDriver` y el de `PathPreview` reciban la conexión desde el mismo punto, sin depender de que sean dos pasos separados y sincronizados a mano. Revisar primero si el orden actual (`pathPreview.Attach(navGraph)` en la línea 706, e `inputDriver.NavGraph = inputDriverNavGraph` en la línea 661, en un punto DISTINTO y anterior del método) ya cubre el caso — si es así, el problema real puede estar en que `inputDriver.NavGraph` se asigna pero **nunca se lee** en ningún lado del propio `PlayerInputDriver.cs` para propagarlo a `PathPreview` en el momento en que el driver arranca.
2. Grepear (ya hecho en este audit) confirma: `NavGraph` solo aparece en la declaración (línea 36) y en la asignación de `HeadlessTestRunner.cs:661` — el campo `Brain`, `Aim`, `Rig`, etc. de `PlayerInputDriver` se USAN activamente en el propio archivo, pero `NavGraph` no se lee en ningún método de `PlayerInputDriver.cs`. La corrección correcta es que `PlayerInputDriver` (que sí corre en Play mode, a diferencia de `HeadlessTestRunner`) sea quien conecte el grafo a `PathPreview.Instance` al arrancar, por ejemplo en `Start()` (línea 226-233) o en `OnEnable()` (línea 239-246), como buena práctica defensiva independiente de quién construyó la escena:
   ```csharp
   void Start()
   {
       if (Brain.Current == null && Squad != null && Squad.Count > 0)
       {
           Brain.Possess(Squad[0]);
           Rig.FollowFps(Squad[0]);
       }

       // Bug 14: PathPreview.Attach solo lo llamaba HeadlessTestRunner
       // (herramienta de Editor). En una escena de juego real construida
       // de otra forma, PathPreview.Instance.graph quedaba null para
       // siempre y la vista previa de ruta nunca rodeaba obstaculos.
       // Conectarlo aca, donde el driver SI corre en Play mode real,
       // garantiza que la vista previa quede armada sin importar como
       // se construyo la escena.
       if (SP.Ai.PathPreview.Instance != null && NavGraph != null)
           SP.Ai.PathPreview.Instance.Attach(NavGraph);
   }
   ```
3. Mantener también la llamada existente en `HeadlessTestRunner.cs:706` (no hace daño, es redundante pero inofensiva — `Attach` es una simple asignación de campo, idempotente) para no arriesgar romper el flujo de test actual, que sigue dependiendo de que el grafo esté listo ANTES de que la suite empiece a simular órdenes (mientras que `PlayerInputDriver.Start()` solo corre en Play mode real, nunca durante `HeadlessTestRunner.RunAll()`, que es Edit mode puro).

**Verificación:** Como este bug es específicamente sobre el camino de PRODUCCIÓN (Play mode real, sin pasar por `HeadlessTestRunner`), la forma más fiel de verificarlo es un test de Play mode: entrar a Play, dar una orden de movimiento con obstáculos de por medio (mantener clic sostenido sobre el suelo, como describe el comentario del propio `PathPreview.cs:9-13`), y observar que la línea dibujada RODEA el obstáculo en vez de atravesarlo en línea recta. Como complemento headless (no cubre el bug en sí, pero sí una regresión futura), agregar en `RunPhase5` un `Check` de que `PathPreview.Instance` con un grafo asignado usa `TryFindPath` y no `DrawStraight`:
   ```csharp
   if (SP.Ai.PathPreview.Instance != null)
   {
       // Ya deberia estar conectado por HeadlessTestRunner.cs:706; esto
       // confirma que la conexion se mantiene y produce una ruta real
       // (mas de 2 puntos) al rodear un obstaculo, no la linea recta.
       bool shown = SP.Ai.PathPreview.Instance.Show(new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f));
       Check($"PathPreview con grafo conectado produce una ruta (no solo 2 puntos): {SP.Ai.PathPreview.Instance.PointCount} puntos",
           shown && SP.Ai.PathPreview.Instance.PointCount >= 2);
   }
   ```
   Para probar específicamente el nuevo cableado de `PlayerInputDriver.Start()`, sería necesario un test de Play mode real (fuera del alcance de `HeadlessTestRunner`, que corre en Edit mode); documentarlo como paso manual: "Entrar a Play, mantener clic izquierdo apuntando al suelo detrás de un obstáculo del mapa, confirmar que la línea celeste de vista previa lo rodea en vez de atravesarlo."

**Riesgo/efectos secundarios:** Confirmar que `PathPreview.Instance` ya existe (`OnEnable`, línea 29-33) para el momento en que `PlayerInputDriver.Start()` corre — como ambos son componentes de la misma escena y Unity ejecuta todos los `OnEnable` antes que cualquier `Start()` en el ciclo de vida estándar, esto debería ser seguro, pero conviene revisar el orden de construcción real en runtime (si `PathPreview` se instancia dinámicamente por código en algún `Bootstrap` de escena, verificar que ocurra antes de que `PlayerInputDriver.Start()` se dispare). Si no hay garantía de orden, considerar mover la conexión a `OnEnable()` de `PlayerInputDriver` en vez de `Start()`, o agregar un chequeo perezoso adicional en `UpdateRts`/donde se llama `PathPreview.Instance.Show` (línea 1891) que reintente `Attach` si `NavGraph != null` pero el grafo interno de `PathPreview` todavía no se conectó.

---

### Bug 15: `KickRecoil` ignora `CameraFxSettings.Enabled`
**Archivos:** `Camera/CameraRig.cs:98` (`KickRecoil`)

**Causa raíz:** `KickDirectional` (línea 116-122) y `AddFrameOffset` (línea 130-134) ambos empiezan con `if (!CameraFxSettings.Enabled) return;`, respetando el interruptor global de efectos de cámara (usado, según el propio test de `RunPhase5` líneas 1240-1252, para permitir desactivar sacudidas de cámara como opción de accesibilidad/preferencia). `KickRecoil` (línea 98) es el único de los tres canales de efecto de cámara que NO tiene esa guarda: `public void KickRecoil(float degrees) => recoilPitch += degrees;` se ejecuta siempre, sin importar `CameraFxSettings.Enabled`. Un jugador que desactiva los efectos de cámara (por mareo, por preferencia) seguiría sintiendo el culatazo de cámara en cada disparo, porque ese canal específico nunca consulta el interruptor que se supone gobierna exactamente este tipo de movimiento no solicitado de la cámara.

**Plan de implementación:**
1. Agregar la misma guarda que ya usan `KickDirectional` y `AddFrameOffset`, en el mismo lugar del método (primera línea):
   ```csharp
   public void KickRecoil(float degrees)
   {
       if (!CameraFxSettings.Enabled) return;
       recoilPitch += degrees; // (o el Clamp del Bug 7 combinado aca)
   }
   ```
   Si el Bug 7 (tope superior) se corrige en el mismo pase, el método combinado queda:
   ```csharp
   public void KickRecoil(float degrees)
   {
       if (!CameraFxSettings.Enabled) return;
       recoilPitch = Mathf.Clamp(recoilPitch + degrees, 0f, maxRecoilPitch);
   }
   ```
2. Verificar que `CameraFxSettings` (no leído en este audit, pero usado consistentemente en `CameraRig.cs` líneas 118, 132, 146) es una clase estática simple con una propiedad `Enabled`, del mismo estilo que ya se usa en los otros dos canales — no requiere cambios en `CameraFxSettings` mismo, solo en `KickRecoil`.

**Verificación:** Extender el bloque de test ya existente en `RunPhase5` (líneas 1240-1252, que prueba `KickDirectional` con `CameraFxSettings.Enabled` en `true`/`false`) con el caso análogo para `KickRecoil`:
   ```csharp
   if (rig != null)
   {
       bool fxWasEnabled2 = SP.CameraSystem.CameraFxSettings.Enabled;

       SP.CameraSystem.CameraFxSettings.Enabled = false;
       float recoilBeforeDisabled = rig.RecoilPitch;
       rig.KickRecoil(10f);
       Check($"Con efectos de camara apagados, KickRecoil no mueve recoilPitch (antes={recoilBeforeDisabled:0.0} despues={rig.RecoilPitch:0.0})",
           Mathf.Approximately(rig.RecoilPitch, recoilBeforeDisabled));

       SP.CameraSystem.CameraFxSettings.Enabled = true;
       rig.KickRecoil(5f);
       Check("Con efectos de camara prendidos, KickRecoil si mueve recoilPitch",
           rig.RecoilPitch > recoilBeforeDisabled);

       SP.CameraSystem.CameraFxSettings.Enabled = fxWasEnabled2;
   }
   ```

**Riesgo/efectos secundarios:** Verificar que `recoilPitch` no queda en un valor intermedio "raro" al alternar el flag a mitad de una ráfaga (por ejemplo, si el jugador desactiva efectos MIENTRAS `recoilPitch` ya es alto por disparos previos) — como el decaimiento en `LateUpdate` (línea 63) NO está condicionado por `CameraFxSettings.Enabled`, `recoilPitch` seguirá bajando a 0 normalmente aunque se desactiven los efectos a mitad de camino, lo cual es el comportamiento correcto (solo se bloquean NUEVOS impulsos, no la recuperación del que ya estaba en curso).

---

### Bug 16: Cambiar a vista RTS no cancela una transición de cámara en curso
**Archivos:** `Camera/CameraRig.cs:196-213` (`RestoreOrSetRtsView`) y `279-285` (`SetRtsView`) vs `226-251` (`BeginTransition`/`TransitionRoutine`)

**Causa raíz:** `BeginTransition` (línea 226-230) inicia una corrutina que controla `transform.position`/`transform.rotation` frame a frame durante `duration` segundos, marcando `IsTransitioning = true`. Ni `SetRtsView` (línea 279-285) ni `RestoreOrSetRtsView` (línea 196-213) — los dos caminos que llevan a la vista RTS — comprueban o cancelan `transitionRoutine` antes de escribir `transform.position`/`transform.rotation` directamente. Si el jugador dispara una transición (por ejemplo `TryPossess` iniciando `BeginTransition` hacia un aliado, línea 1042 de `PlayerInputDriver.cs`) y, ANTES de que termine, algo dispara un cambio a vista RTS (por ejemplo `[TAB]`, línea 374/386-394 de `PlayerInputDriver.cs`), ocurre lo siguiente: `SetRtsView`/`RestoreOrSetRtsView` escriben la `transform` de golpe al valor RTS, pero la corrutina de `TransitionRoutine` SIGUE VIVA (`transitionRoutine != null`, `IsTransitioning == true` todavía) y en el próximo `yield return null` vuelve a ejecutar `transform.position = Vector3.Lerp(fromPos, target.position, k)`, PISANDO la posición RTS recién asignada y haciendo que la cámara "salte de vuelta" visiblemente a mitad de la transición vieja, en vez de quedarse en RTS.

**Plan de implementación:**
1. Extraer la cancelación de la transición en curso (ya escrita una vez en `BeginTransition`, línea 229: `if (transitionRoutine != null) StopCoroutine(transitionRoutine);`) a un método reutilizable, y llamarlo desde los dos puntos que escriben la `transform` fuera de la corrutina:
   ```csharp
   // Corta cualquier transicion en curso sin arrancar una nueva -- para
   // los caminos que escriben la transform de una sola vez (SetRtsView,
   // RestoreOrSetRtsView) y necesitan la garantia de que nada la va a
   // pisar en el proximo frame.
   void CancelTransition()
   {
       if (transitionRoutine != null)
       {
           StopCoroutine(transitionRoutine);
           transitionRoutine = null;
       }
       IsTransitioning = false;
   }

   public void BeginTransition(Transform target, float duration = 0.35f)
   {
       if (target == null) return;
       CancelTransition();
       transitionRoutine = StartCoroutine(TransitionRoutine(target, duration));
   }
   ```
2. Llamar `CancelTransition()` al principio de `SetRtsView` y de `RestoreOrSetRtsView`:
   ```csharp
   public void SetRtsView(Vector3 center)
   {
       CancelTransition(); // Bug 16: sin esto, una transicion en curso pisaba esta posicion en el siguiente frame
       if (cam != null) cam.orthographicSize = rtsOrthoSize;
       transform.position = center + Vector3.up * rtsHeight;
       transform.rotation = Quaternion.Euler(rtsLookEuler);
       panTargetInitialized = false;
   }

   public void RestoreOrSetRtsView(Vector3 fallbackCenter)
   {
       if (savedRtsPosition.HasValue && savedRtsOrthoSize > 0f)
       {
           CancelTransition(); // idem
           transform.position = savedRtsPosition.Value;
           transform.rotation = Quaternion.Euler(rtsLookEuler);
           if (cam != null) cam.orthographicSize = savedRtsOrthoSize;
           panTargetInitialized = false;
       }
       else
       {
           SetRtsView(fallbackCenter); // ya cancela adentro
       }
   }
   ```
3. Decidir si también conviene cancelar en `SetMode`/`ToggleMode` en general (no solo al ir a RTS) — por ejemplo, ir a FPS sin pasar por `BeginTransition` también podría chocar con una transición vieja si algún call-site futuro llama `SetMode(ControlMode.Fps)` sin una transición nueva. Por prudencia y para no expandir el alcance más allá del bug reportado, esta plan se limita a los dos métodos explícitamente señalados (`RestoreOrSetRtsView`/`SetRtsView`), que son los que el bug 16 describe.

**Verificación:** Nuevo test en `RunPhase7`/regresión de cámara: arrancar una transición larga hacia un `Transform` cualquiera, y ANTES de que termine, llamar `SetRtsView`, y confirmar que unos frames después la cámara sigue en la posición RTS (no "rebota"):
   ```csharp
   var farTarget = new GameObject("FarTransitionTarget").transform;
   farTarget.position = new Vector3(200f, 5f, 200f);
   farTarget.rotation = Quaternion.identity;
   rig.BeginTransition(farTarget, 5f); // duracion larga a proposito
   Check("La transicion arranco", rig.IsTransitioning);

   rig.SetRtsView(new Vector3(40f, 0f, 40f));
   Check("SetRtsView cancela la transicion en curso (IsTransitioning vuelve a false)", !rig.IsTransitioning);
   Vector3 posInmediataDespues = rig.transform.position;

   // Nota: como no hay reloj real en Edit mode, no se puede "avanzar un
   // frame" para verificar que la corrutina no pisa la posicion --
   // basta con confirmar IsTransitioning==false, que es la garantia real
   // (LateUpdate y cualquier otro Follow* ya respetan ese flag, y la
   // corrutina detenida con StopCoroutine no vuelve a ejecutar).
   Check($"La camara quedo en la posicion RTS pedida (pos={posInmediataDespues})",
       Vector3.Distance(new Vector3(posInmediataDespues.x, 0f, posInmediataDespues.z), new Vector3(40f, 0f, 40f)) < 1f);

   UnityEngine.Object.DestroyImmediate(farTarget.gameObject);
   ```

**Riesgo/efectos secundarios:** Confirmar que `StopCoroutine(transitionRoutine)` es seguro de llamar en `CancelTransition` incluso si `transitionRoutine` ya terminó naturalmente (el método ya lo pone en `null` al terminar exitosamente en `TransitionRoutine`, línea 250, así que el `if (transitionRoutine != null)` cubre ese caso). Revisar visualmente en Play mode: poseer a un aliado lejano (transición larga) y apretar `[TAB]` inmediatamente — antes del fix debería "temblar"/saltar, después del fix debería cortar limpio a RTS.

---

### Bug 17: `AimUI.StateLabel` no tiene case para `AiState.Follow`
**Archivos:** `UI/AimUI.cs:408-418` (`StateLabel`)

**Causa raíz:** El `switch` expression `StateLabel` (línea 408-418) mapea cada valor de `AiState` a un texto legible en español ("Patrullando", "En reposo", "Cumpliendo orden", etc.) para mostrar en el panel de información al apuntarle a un soldado (usado en `UpdateSoldierInfo`, línea 404). Cubre `Patrol, Idle, MovingToOrder, Chase, MovingToAttackOrder, Attack, Dead` — pero NO `AiState.Follow` (que sí existe en el enum, `Ai/AiState.cs:8`, agregado para la funcionalidad de "que me sigan"). Como consecuencia, la rama `_ => state.ToString()` (línea 417) atrapa el caso Follow y muestra literalmente el texto en inglés/PascalCase `"Follow"` en la UI, rompiendo la consistencia de idioma y de estilo (todo lo demás está en español natural) del panel de información del jugador.

**Plan de implementación:**
1. Agregar el case faltante, con un texto en el mismo estilo que los demás (verbo/gerundio corto, en español, describiendo la acción):
   ```csharp
   static string StateLabel(SP.Ai.AiState state) => state switch
   {
       SP.Ai.AiState.Patrol => "Patrullando",
       SP.Ai.AiState.Idle => "En reposo",
       SP.Ai.AiState.MovingToOrder => "Cumpliendo orden",
       SP.Ai.AiState.Follow => "Siguiendo",
       SP.Ai.AiState.Chase => "Persiguiendo",
       SP.Ai.AiState.Attack => "En combate",
       SP.Ai.AiState.Dead => "Caido",
       _ => state.ToString(),
   };
   ```
2. Dejar la rama `_ => state.ToString()` como red de seguridad para cualquier estado futuro que se agregue al enum y todavía no tenga case explícito (mismo criterio defensivo que ya tiene el código), pero el objetivo es que, con este cambio, los 8 valores actuales del enum (`Patrol, Idle, MovingToOrder, Follow, Chase, MovingToAttackOrder, Attack, Dead`) queden todos cubiertos explícitamente.

**Verificación:** Nuevo test en la sección de `RunPhase5` donde ya se prueba el panel de info al apuntar (líneas ~1344-1356, item 65), o un test dedicado más simple usando reflexión sobre el método estático privado:
   ```csharp
   var stateLabelMethod = typeof(AimUI).GetMethod("StateLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
   string labelFollow = (string)stateLabelMethod.Invoke(null, new object[] { SP.Ai.AiState.Follow });
   Check($"StateLabel(Follow) no cae al ToString() crudo en ingles (obtenido: '{labelFollow}')", labelFollow != "Follow");
   ```
   Alternativa más end-to-end (sin reflexión): poner a un soldado en estado `Follow` de verdad (`OrderService.IssueFollowOrder`), apuntarle con `AimTargeting.Evaluate`, llamar `aimUiRef.UpdateFromAimResult(result)`, y verificar por reflexión sobre `soldierInfoText` que el texto NO contiene la palabra cruda `"Follow"`:
   ```csharp
   OrderService.IssueFollowOrder(kes, vega);
   var followResult = new AimResult { Type = AimTargetType.Ally, Soldier = kes, Point = kes.transform.position };
   aimUiRef.UpdateFromAimResult(followResult);
   var infoPanelField2 = typeof(AimUI).GetField("soldierInfoText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
   var infoText2 = ((Text)infoPanelField2.GetValue(aimUiRef))?.text ?? "";
   Check($"Panel de info de un soldado en Follow muestra 'Siguiendo', no 'Follow' crudo ('{infoText2}')",
       infoText2.Contains("Siguiendo") && !infoText2.Contains("Follow"));
   kes.GetComponent<AiBrain>().CancelOrder(); // no dejar a Kes siguiendo para el resto de la suite
   ```

**Riesgo/efectos secundarios:** Cambio de superficie mínima (una línea agregada a un `switch` expression puro, sin efectos colaterales de estado). Verificar que el texto elegido ("Siguiendo") no choca en longitud con el layout del panel `soldierInfoText` (línea 405, un solo renglón concatenado con `·`) — dado que es más corto que "Cumpliendo orden", no debería ser un problema.

---

### Bug 18 (EL MÁS GRAVE DE TODA LA AUDITORÍA): `Configure()` no tiene ningún efecto sobre la vida real del soldado — el HP personalizado se descarta en silencio
**Archivos:** `Actors/Soldier.cs:62-68` (`Configure`) vs `Actors/Soldier.cs:70-85` (`Awake`/`Bootstrap`), consumido incorrectamente por `Editor/HeadlessTestRunner.cs:1769-1793` (`SpawnSoldier`)

**Causa raíz:** `Soldier.Awake()` (línea 70) llama `Bootstrap()` INCONDICIONALMENTE apenas Unity instancia el componente — y punto crítico: esto sucede tanto en Play mode como en Edit mode, incluyendo el instante mismo de `PrefabUtility.InstantiatePrefab(prefab)` que usa `HeadlessTestRunner.SpawnSoldier` (línea 1771). `Bootstrap()` (línea 72-85) hace `health.Initialize(Id, maxHealth)` en la línea 83, usando el valor de `maxHealth` que en ESE momento es el default serializado del prefab (`[SerializeField] int maxHealth = 100;`, línea 17) — porque `Configure()` (que es quien recibiría el valor custom) todavía NO fue llamado por nadie: `SpawnSoldier` llama `Configure` DESPUÉS de `InstantiatePrefab`, en la línea 1788 (`soldier.Configure(name, team, role, maxHealth)`), pero para entonces `Awake()` ya corrió y ya bootstrappeó con el valor viejo. `Configure()` sí actualiza el CAMPO `maxHealth` del `Soldier` (línea 67: `maxHealth = max;`), pero nunca vuelve a llamar `health.Initialize(...)` con el nuevo valor — y el propio guard de `Bootstrap()` (`if (bootstrapped) return;`, línea 74) impide que la llamada explícita a `soldier.Bootstrap()` en la línea 1789 de `SpawnSoldier` haga nada, porque `bootstrapped` ya es `true` desde el `Awake()` automático. Resultado: **todo enemigo o aliado creado con `SpawnSoldier(..., maxHealth: 180)` (o 150, 120, etc. — se ve en TODA la suite: `HeadlessTestRunner.cs` líneas 780, 839, 932, 977 y más) en realidad nace con el `maxHealth` default del prefab (100), sin ningún error, sin ningún log — el número pasado como parámetro es pura ilusión.** Esto invalida silenciosamente el balance de combate de absolutamente todos los tests (un "enemigo de 180 HP" es en verdad un enemigo de 100 HP) y, más grave aún, si el mismo patrón `Configure()` se usa en cualquier construcción de escena de PRODUCCIÓN (no solo en tests), el balance de dificultad real del juego (jefes con más vida, soldados débiles con menos vida) queda completamente roto sin que nadie lo note, porque no hay ninguna señal de error — el juego corre normalmente, solo que con números equivocados.

**Plan de implementación:**
1. La corrección correcta NO es "hacer que `Configure()` llame a `health.Initialize()` también" como parche superficial — eso funcionaría para el caso de `SpawnSoldier` (que llama `Configure` antes de que nadie use `Health` en serio), pero dejaría intacta la causa de fondo: `Awake()` sigue bootstrappeando con el valor default ANTES de que `Configure()` tenga oportunidad de correr, así que cualquier código intermedio que lea `soldier.Health.MaxHealth` entre el `InstantiatePrefab` y el `Configure()` vería igual el valor viejo por una fracción de tiempo — un bug latente distinto pero emparentado. La solución robusta es que `Configure()` sea capaz de re-inicializar `Health` si el bootstrap ya corrió, y que además se recomiende (para cualquier llamador nuevo) invocar `Configure()` ANTES de que nada dispare `Bootstrap()` — pero como `Awake()` ya lo dispara automáticamente al instanciar, la única forma real de que `Configure()` sea confiable SIEMPRE (sin importar el orden de instanciación) es que vuelva a sincronizar `Health` explícitamente:
   ```csharp
   // Fija identidad y equipo. Se llama una vez al construir la escena.
   //
   // BUG CRITICO CORREGIDO: Awake() ya corre Bootstrap() (que a su vez
   // llama Health.Initialize(Id, maxHealth)) en el mismo instante en que
   // Unity instancia el prefab -- ANTES de que este metodo tenga chance
   // de correr. El "max" que se pasa aca antes se guardaba en el campo
   // maxHealth pero jamas volvia a llegar a Health, que ya habia quedado
   // inicializado con el default del prefab (100). Un enemigo "creado con
   // 180 de vida" en realidad nacia con 100, sin ningun error ni log.
   // Ahora, si el bootstrap ya corrio, se vuelve a sincronizar Health con
   // el valor real pedido.
   public void Configure(string name, TeamId t, RoleType r, int max)
   {
       displayName = name;
       team = t;
       role = r;
       maxHealth = max;

       if (bootstrapped && health != null)
           health.Initialize(Id, maxHealth);
   }
   ```
2. Esto cubre el caso general (cualquier orden de llamada entre `Awake`/`Configure`) sin tener que tocar `Bootstrap()` ni el guard `if (bootstrapped) return;`, que sigue siendo necesario para evitar reinicializar `Id`/`ActorRegistry.Register` dos veces. `Health.Initialize(actorId, max)` (`Combat/Health.cs:25-30`) es segura de llamar más de una vez: simplemente reescribe `maxHealth` y pone `Current = max` (vida llena), que es exactamente lo que se espera al "configurar" un soldado recién creado (todavía no recibió daño).
3. Revisar (y, si aplica, simplificar) `HeadlessTestRunner.SpawnSoldier` (línea 1787-1789): la llamada `soldier.Bootstrap()` de la línea 1789, después de `Configure`, se vuelve completamente redundante con la corrección del paso 1 (ya no hace nada útil porque `bootstrapped` ya es `true`, pero tampoco hace daño); se puede dejar como está por claridad histórica o eliminar — no es necesaria para la corrección, así que es opcional.
4. Auditar el resto del proyecto por otros llamadores de `Configure` que pudieran depender (sin saberlo) del bug — es decir, código que llama `Configure` y LUEGO decide hacer algo basado en `Health.Current`/`Health.MaxHealth` asumiendo (incorrectamente, hoy) que seguía en 100: no se encontró ninguno en el grep de `Configure(` de este audit fuera de `SpawnSoldier`, pero conviene una búsqueda final de `\.Configure\(` en todo `Assets/_Project/Scripts` antes de cerrar el fix, por si existe un segundo constructor de escena (por ejemplo uno de producción, fuera de `Editor/`) que también lo use.

**Verificación:** Este es el bug con verificación más directa y crítica de todo el lote — agregar un `Check` inmediatamente después de CADA `SpawnSoldier` con `maxHealth` distinto de 100 en `HeadlessTestRunner.cs`, o mínimamente uno representativo apenas se crea el primer enemigo con HP custom (por ejemplo, justo después de la línea 839, `enemy1 = SpawnSoldier(..., 180)`, o mejor aún, un test dedicado y explícito en `RunPhase1` al principio, antes de cualquier combate que pueda alterar la vida:
   ```csharp
   var enemyHpTest = SpawnSoldier(soldierPrefab, "Test_HP_180", TeamId.Enemy, RoleType.Enemy,
       new Vector3(999f, 0.8f, 999f), enemyColor, pool, 180);
   Check($"SpawnSoldier con maxHealth=180 realmente crea un soldado con 180 de vida maxima (obtenido: {enemyHpTest.Health.MaxHealth})",
       enemyHpTest.Health.MaxHealth == 180);
   Check($"Ademas nace con la vida ACTUAL llena en 180, no en el default de 100 (obtenido: {enemyHpTest.Health.Current})",
       enemyHpTest.Health.Current == 180);
   UnityEngine.Object.DestroyImmediate(enemyHpTest.gameObject);
   ```
   Colocar este bloque al principio de `RunPhase1` (justo después de `TestLog.Phase("FASE 1 ...")`, antes de cualquier otra cosa) para que, si el fix llegara a regresionar en el futuro, la suite falle inmediatamente y de forma inequívoca, en vez de que el síntoma quede enmascarado dentro de un resultado de combate que "por casualidad" sigue dando ganador razonable incluso con la vida equivocada.
   
   Adicionalmente, sería valioso (aunque no imprescindible) revisar retroactivamente si algún resultado de combate de la suite EXISTENTE (por ejemplo el `Check` de la línea 943, `"Gano {winner} sobre {loser}..."`, que hoy corre con enemigos que en realidad tienen 100 HP en vez de 150/120 como se pretendía) cambia de forma significativa una vez corregido el bug — es esperable que algunos combates que antes ganaba el jugador "de casualidad" (por enfrentar enemigos más débiles de lo previsto) ahora sean más reñidos o incluso los pierda, lo cual sería la suite finalmente probando el balance REAL que el diseño pretendía.

**Riesgo/efectos secundarios:** Este es el cambio de mayor impacto de todo el lote de 18 bugs, porque afecta el balance de CADA combate de la suite (todos los enemigos "de 120/150/180 HP" pasan a tener su vida real en vez de 100) y potencialmente el balance de producción si el mismo patrón `Configure()` se usa fuera de tests. Se recomienda:
   - Correr la suite completa (`RunAll()`) después del fix y revisar con atención cualquier `Check` de combate que empiece a fallar (por ejemplo, si algún test asumía implícitamente que el enemigo tenía 100 HP para calcular cuántos disparos hacían falta, o un timeout de `SimulateUntil` dimensionado para la vida vieja) — varios de los checks de combate usan `SimulateUntil(..., timeoutSeconds)` con generosidad (6-10 segundos), así que es probable que sigan pasando, pero un enemigo con 180 HP real en vez de 100 tarda genuinamente más en morir y podría acercarse al límite del timeout en máquinas lentas.
   - Revisar visualmente en Play mode que un enemigo "fuerte" (si existe alguno etiquetado como tal en el diseño del nivel real, fuera de la suite de test) efectivamente se siente más difícil de matar después del fix — es la confirmación final de que el balance pretendido por el diseño ahora se aplica de verdad.
   - Si existiera contenido de producción (niveles reales, no solo la escena de test) que llama `Configure` con valores personalizados esperando que NO tuvieran efecto (poco probable, pero a chequear), ese contenido cambiaría de dificultad con este fix — dado que el propio comentario de `Configure` dice "se llama una vez al construir la escena" con la clara intención de que el valor SÍ importe, esto se considera la corrección esperada y no una regresión.


---

# Combate / Core — Planes de corrección (11 bugs)

Todos los planes están escritos contra el código real leído en `Assets/_Project/Scripts/Combat/*.cs`, `Assets/_Project/Scripts/Core/*.cs`, `Assets/_Project/Scripts/Editor/HeadlessTestRunner.cs`, `Assets/_Project/Scripts/Ai/WorldSimulationDriver.cs`, `Assets/_Project/Scripts/Player/PlayerInputDriver.cs` y `Assets/_Project/Scripts/Demo/AutoDemoRunner.cs`. Se citan nombres de método/campo tal cual existen hoy.

---

### Bug 1: `ProjectilePool.Spawn()` explota con NRE si falta el prefab

**Archivos:** `Combat/ProjectilePool.cs:18-23` (`Bootstrap()`) y `Combat/ProjectilePool.cs:82-93` (`Spawn()`)

**Causa raíz:** `Bootstrap()` hace `if (prefab == null) return;` en silencio, así que `pool` se queda en `null` para siempre. `Spawn()` reintenta `Bootstrap()` si `pool == null`, pero como `prefab` sigue sin asignar el reintento vuelve a no-opear — y después, sin volver a preguntar, la línea `var p = pool.Get();` revienta con `NullReferenceException`. El chequeo `pool != null` que sí existe (para `ExhaustedCount`) es cosmético: protege un contador, no la llamada real.

**Plan de implementación:**
1. En `Bootstrap()`, cambiar el `return` silencioso por un `Debug.LogWarning` que identifique el GameObject, para que la mala configuración se vea en el momento en que se arma la escena (Awake) y no recién en el primer disparo, en medio de un tiroteo:
   ```csharp
   public void Bootstrap()
   {
       if (pool != null) return;
       if (prefab == null)
       {
           Debug.LogWarning($"[ProjectilePool] {name}: 'prefab' no esta asignado en el Inspector -- el pool no se puede construir. Spawn() no va a disparar nada hasta que se asigne.", this);
           return;
       }
       pool = new ObjectPool<Projectile>(prefab, prewarm, transform);
   }
   ```
2. En `Spawn()`, agregar un guard **después** del reintento de `Bootstrap()` y **antes** de tocar `pool.FreeCount`/`pool.Get()`:
   ```csharp
   public Projectile Spawn(Vector3 position, Vector3 direction, int shooterId, TeamId shooterTeam, int damage, Color? color = null, float explosionRadius = 0f, float gravity = 0f, SP.Vehicles.Vehicle sourceVehicle = null, float speedMultiplier = 1f)
   {
       if (pool == null) Bootstrap();
       if (pool == null) return null; // Bootstrap() ya logueo el motivo (prefab sin asignar)

       if (pool.FreeCount == 0) ExhaustedCount++;
       var p = pool.Get();
       p.Configure(this, position, direction, shooterId, shooterTeam, damage, color, explosionRadius, gravity, sourceVehicle, speedMultiplier);
       return p;
   }
   ```
3. Verificar que los llamadores reales toleran `null`: `WeaponHolder.TryFire` (línea 177, `pool.Spawn(...)` como sentencia suelta) y `TurretWeapon.cs:204` no usan el valor de retorno, así que devolver `null` no rompe nada. Solo el propio `HeadlessTestRunner` captura el retorno (`var p = pool.Spawn(...)`), y siempre con un prefab válido.

**Verificación:** Agregar en `RunPhase1` (cerca del check existente `"El proyectil volvio al pool"`, `HeadlessTestRunner.cs:837`) un `Check()` que arma un `ProjectilePool` a propósito SIN prefab y confirma que `Spawn()` no tira:
```csharp
var brokenPoolGO = new GameObject("BrokenPoolNoPrefab");
var brokenPool = brokenPoolGO.AddComponent<ProjectilePool>(); // Awake() corre Bootstrap() con prefab==null
bool threw = false;
Projectile result = null;
try { result = brokenPool.Spawn(Vector3.zero, Vector3.forward, -1, TeamId.Enemy, 10); }
catch { threw = true; }
Check("ProjectilePool.Spawn() sin 'prefab' asignado no tira NRE (devuelve null)", !threw && result == null);
UnityEngine.Object.DestroyImmediate(brokenPoolGO);
```

**Riesgo/efectos secundarios:** Ninguno funcional: hoy este camino solo terminaba en excepción no manejada, así que cualquier comportamiento nuevo (log + `null`) es estrictamente mejor. Único cuidado: si en el futuro algún llamador nuevo SÍ despacha el `Projectile` devuelto sin chequear `null` (por ejemplo para cachear `Velocity` o suscribirse a algo), va a fallar ahí — mantener el hábito de chequear `null` al usar el retorno de `Spawn()`.

---

### Bug 2: `ObjectPool<T>.Release()` sin guarda de doble liberación — EL MÁS SEVERO DE ESTA TANDA

**Archivos:** `Core/ObjectPool.cs:40-45`

**⚠️ Este es el bug más severo de la sección.** `ObjectPool<T>` es la clase base de la que depende **todo** pool del juego (hoy `ProjectilePool`, y cualquier otro sistema pooleado futuro). Si algo libera dos veces la misma instancia (un doble `Release`, un doble `Expire()`, una carrera entre dos caminos de código que devuelven el mismo proyectil al pool), esa instancia queda pusheada DOS VECES en el `Stack<T> free`. Las dos próximas llamadas a `Get()` van a hacer `Pop()` y devolver la **misma referencia** a **dos dueños distintos y simultáneos** — dos disparos "diferentes" controlando el mismo GameObject, con `OnSpawn()`/`Configure()` pisándose entre sí sin ningún error visible. Es corrupción de estado silenciosa, no un crash: mucho más difícil de diagnosticar que un NRE.

**Causa raíz:** `Release(T instance)` hace `instance.OnDespawn(); instance.gameObject.SetActive(false); free.Push(instance);` sin preguntar nunca si `instance` ya estaba en `free`. No hay ninguna estructura de datos que registre "esta instancia ya está liberada" — el `Stack<T>` solo permite apilar, no consultar membresía barato.

**Plan de implementación:**
1. Agregar un `HashSet<T>` que refleje, en paralelo al `Stack<T> free`, qué instancias están actualmente libres (comparación por referencia, que es la que ya usa `Component` por defecto — no hace falta ningún `IEqualityComparer` custom):
   ```csharp
   readonly Stack<T> free = new Stack<T>();
   readonly HashSet<T> freeSet = new HashSet<T>();
   ```
2. En el constructor, al prellenar, agregar también al `freeSet`:
   ```csharp
   for (int i = 0; i < prewarm; i++)
   {
       var instance = Object.Instantiate(prefab, parent);
       instance.gameObject.SetActive(false);
       free.Push(instance);
       freeSet.Add(instance);
   }
   ```
3. En `Get()`, sacar del `freeSet` a la par que se saca del `Stack`:
   ```csharp
   public T Get()
   {
       T instance;
       if (free.Count > 0) { instance = free.Pop(); freeSet.Remove(instance); }
       else instance = Object.Instantiate(prefab, parent);
       instance.gameObject.SetActive(true);
       instance.OnSpawn();
       return instance;
   }
   ```
4. En `Release()`, usar `HashSet<T>.Add()` como la guarda misma: `Add()` devuelve `false` si el elemento YA estaba presente, que es exactamente "esto ya estaba liberado":
   ```csharp
   public void Release(T instance)
   {
       if (instance == null) return; // instancia destruida entre medio: no la re-agregamos rota al pool
       if (!freeSet.Add(instance))
       {
           Debug.LogWarning($"[ObjectPool<{typeof(T).Name}>] Release() llamado dos veces sobre la misma instancia ({instance.name}); se ignora la segunda liberacion para no duplicarla en el pool.");
           return;
       }
       instance.OnDespawn();
       instance.gameObject.SetActive(false);
       free.Push(instance);
   }
   ```
5. `FreeCount` sigue leyendo `free.Count` sin cambios (los dos colecciones quedan siempre en sincronía por construcción).

**Verificación:** Agregar en `RunPhase1`, inmediatamente después del check `"El proyectil volvio al pool"` (`HeadlessTestRunner.cs:837`), un `Check()` que libere el mismo proyectil dos veces y confirme que `FreeCount` no crece la segunda vez, y que dos `Spawn()` posteriores no devuelvan la misma referencia:
```csharp
var probe = pool.Spawn(vega.transform.position, Vector3.forward, vega.Id, vega.Team, 1);
pool.Release(probe);
int freeAfterFirstRelease = pool.FreeCount;
pool.Release(probe); // segunda liberacion de la MISMA instancia
Check("ObjectPool<Projectile>.Release() ignora una segunda liberacion de la misma instancia (FreeCount no vuelve a crecer)",
    pool.FreeCount == freeAfterFirstRelease);
var a = pool.Spawn(vega.transform.position, Vector3.forward, vega.Id, vega.Team, 1);
var b = pool.Spawn(vega.transform.position, Vector3.forward, vega.Id, vega.Team, 1);
Check("Tras la guarda de doble-release, dos Spawn() consecutivos devuelven instancias DISTINTAS (antes podian ser la misma)",
    a != b);
pool.Release(a); pool.Release(b);
```

**Riesgo/efectos secundarios:** Costo extra despreciable (un `HashSet` con la misma cardinalidad que el `Stack`, mantenido en O(1) por operación). Ninguna API pública cambia de firma. Si algún código externo hoy dependía (por accidente) de que un doble-release "funcionara" duplicando la instancia — extremadamente improbable, y sería en sí mismo el bug que se está corrigiendo — se comportaría distinto (con log de warning en vez de corrupción silenciosa), que es la mejora buscada.

---

### Bug 3: `ProjectilePool.Configure()` filtra el pool viejo y mezcla proyectiles en vuelo en el pool nuevo

**Archivos:** `Combat/ProjectilePool.cs:25-34`

**Causa raíz:** `Configure()` hace `pool = null;` y después `Bootstrap()` arma un `ObjectPool<Projectile>` totalmente nuevo. Dos problemas simultáneos:
1. **Leak:** las instancias que estaban LIBRES en el pool viejo (ya instanciadas como hijas de `transform` durante el prewarm) no se destruyen ni se referencian por nadie más — quedan colgadas de la jerarquía para siempre, sin que ningún sistema las cuente ni las reuse.
2. **Mezcla:** un `Projectile` que estaba EN VUELO en ese momento (ya sacado del pool viejo con `Get()`, con su campo `pool` apuntando a este mismo `ProjectilePool`) va a llamar, al expirar, `pool?.Release(this)` (`Projectile.cs:486`) — pero `pool` (el campo interno `ObjectPool<Projectile>` de `ProjectilePool`) ya es el pool NUEVO, con otra configuración (otro prefab, quizás otra escala/tuning). Ese proyectil viejo termina reciclado dentro de un pool que no lo originó.

**Plan de implementación:**
1. Agregar a `Core/ObjectPool.cs` un método `Clear()` que vacíe y destruya las instancias libres antes de abandonar el pool:
   ```csharp
   public void Clear()
   {
       while (free.Count > 0)
       {
           var instance = free.Pop();
           if (instance != null) Object.Destroy(instance.gameObject);
       }
   }
   ```
   (Si se aplicó el Bug 2 primero, también hacer `freeSet.Clear()` acá para mantener las dos colecciones en sincronía.)
2. Agregar a `ProjectilePool` un contador de generación:
   ```csharp
   int generation;
   ```
3. En `Configure()`, drenar el pool viejo ANTES de soltarlo, e incrementar la generación:
   ```csharp
   public void Configure(Projectile projectilePrefab, int prewarmCount)
   {
       prefab = projectilePrefab;
       prewarm = prewarmCount;
       pool?.Clear(); // destruye las instancias LIBRES del pool viejo antes de abandonarlo
       pool = null;
       generation++; // cualquier proyectil en vuelo con la generacion anterior ya no calza con el pool nuevo
       ExhaustedCount = 0;
       Bootstrap();
   }
   ```
4. En `Combat/Projectile.cs`, agregar un campo `int poolGeneration` y guardarlo en `Configure(...)` (el único call site que invoca `Projectile.Configure` es `ProjectilePool.Spawn()`, así que el cambio de firma tiene un solo lugar que tocar):
   ```csharp
   int poolGeneration;
   public int PoolGeneration => poolGeneration;
   ```
   y en `ProjectilePool.Spawn()`, después de `var p = pool.Get();`, pasar `generation` a `Configure` (agregando el parámetro a la firma de `Projectile.Configure`, o simplemente asignando `p.poolGeneration = generation;` justo antes de llamar a `p.Configure(...)` si se prefiere no tocar la firma pública).
5. En `ProjectilePool.Release(Projectile p)`, comparar la generación antes de delegar al pool interno:
   ```csharp
   public void Release(Projectile p)
   {
       if (p == null) return;
       if (pool != null && p.PoolGeneration == generation) pool.Release(p);
       else Object.Destroy(p.gameObject); // instancia de una configuracion anterior: no se recicla en un pool que ya no coincide
   }
   ```

**Verificación:** Agregar un bloque de prueba aislado (mismo estilo que `BenchPool`/`EquivPool` en `HeadlessTestRunner.cs`) que reconfigura un pool MIENTRAS un proyectil sigue en vuelo:
```csharp
var reconfigGO = new GameObject("ReconfigPoolTest");
var reconfigPool = reconfigGO.AddComponent<ProjectilePool>();
reconfigPool.Configure(projectilePrefab, 4);
var inFlight = reconfigPool.Spawn(vega.transform.position, Vector3.forward, vega.Id, vega.Team, 5);
reconfigPool.Configure(projectilePrefab, 8); // reconfigura mientras 'inFlight' sigue vivo
int freeAfterReconfigure = reconfigPool.FreeCount;
reconfigPool.Release(inFlight); // el proyectil viejo "vuelve" solo
Check("Un proyectil en vuelo de la config ANTERIOR no contamina el pool reconfigurado (Release no le suma FreeCount)",
    reconfigPool.FreeCount == freeAfterReconfigure);
UnityEngine.Object.DestroyImmediate(reconfigGO);
```

**Riesgo/efectos secundarios:** `Configure()` solo se llama hoy desde `HeadlessTestRunner` (BenchPool/EquivPool/StressPool/BuildAndRun), nunca en tiempo real de juego — así que el riesgo de romper un flujo de Play mode real es bajo. Ojo con el orden: `pool?.Clear()` debe ejecutarse ANTES de `pool = null;` (si no, se pierde la referencia al pool viejo y no hay nada que drenar). Si se agrega el parámetro `poolGeneration` a la firma pública de `Projectile.Configure`, revisar que no haya otro call site además de `ProjectilePool.Spawn()` (confirmado por grep: no lo hay).

---

### Bug 4: El empuje de una granada en el epicentro exacto puede lanzar al soldado verticalmente

**Archivos:** `Combat/Projectile.cs:424-428` (dentro de `Explode(Vector3 point)`)

**Causa raíz:**
```csharp
Vector3 away = s.transform.position - point;
away.y = 0f;
if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitSphere;
float strength = 1f - Mathf.Clamp01(dist / explosionRadius);
s.transform.position += away.normalized * strength * 2.2f;
```
El vector principal se aplana a XZ explícitamente (`away.y = 0f`). Pero el *fallback* para el caso degenerado (el soldado está justo parado sobre el punto de impacto en XZ, empuje horizontal indefinido) usa `Random.insideUnitSphere`, que es un vector 3D uniforme **con componente Y**. Ese vector se normaliza y se suma directo a la posición, así que en ese caso borde el soldado puede salir disparado hacia arriba (o hundido hacia abajo) en vez de recibir el empujón lateral que el resto de la explosión sí respeta.

**Plan de implementación:**
1. Reemplazar el fallback en `Explode()` por una dirección aleatoria ya restringida al plano XZ, coherente con el vector principal (mismo criterio que ya usa `CameraRig.KickDirectional`'s fallback un poco más abajo en el mismo método, línea ~465, que usa `Vector3.up` como degenerado fijo):
   ```csharp
   Vector3 away = s.transform.position - point;
   away.y = 0f;
   if (away.sqrMagnitude < 0.0001f)
   {
       // Mismo criterio que el vector principal (arriba): el empuje de
       // una granada es lateral, nunca vertical. Random.insideUnitSphere
       // tenia componente Y y podia lanzar al soldado para arriba si
       // caia justo en el epicentro.
       var randomXZ = Random.insideUnitCircle;
       away = new Vector3(randomXZ.x, 0f, randomXZ.y);
       if (away.sqrMagnitude < 0.0001f) away = Vector3.forward; // caso degenerado de insideUnitCircle (~(0,0)): direccion fija
   }
   float strength = 1f - Mathf.Clamp01(dist / explosionRadius);
   s.transform.position += away.normalized * strength * 2.2f;
   ```
2. No hace falta tocar nada más: `strength`, el `Vector3.Distance` de más arriba y el resto del bucle de `Explode()` quedan iguales.

**Verificación:** Agregar en `RunPhase6` (donde ya se prueba el cañón del tanque, cerca de `HeadlessTestRunner.cs:1487-1494`) un `Check()` que posiciona a un soldado EXACTAMENTE en el punto de impacto (en XZ) y dispara una granada ahí, verificando que la altura no cambia:
```csharp
var epicenterPos = vehicle.transform.position; // punto de impacto que se va a usar
var epicenterSoldier = SpawnSoldier(soldierPrefab, "Epicentro_Test", TeamId.Enemy, RoleType.Enemy,
    new Vector3(epicenterPos.x, 0.8f, epicenterPos.z), enemyColor, pool, 100);
float yBefore = epicenterSoldier.transform.position.y;
var grenade = pool.Spawn(epicenterPos + Vector3.up * 3f, Vector3.down, -1, TeamId.Player, 10, null, 5f, 0f);
SimulateUntil(() => !epicenterSoldier.gameObject.activeInHierarchy || Mathf.Abs(epicenterSoldier.transform.position.y - yBefore) > 0.0001f || grenade == null, 1f);
Check("El empuje de una granada en el epicentro exacto no lanza al soldado verticalmente (fallback aleatorio forzado al plano XZ)",
    Mathf.Approximately(epicenterSoldier.transform.position.y, yBefore));
```
(Ajustar la forma exacta de disparar/posicionar según el escenario disponible en esa fase; lo importante es forzar `dist ≈ 0` en XZ entre soldado y punto de explosión y comprobar `position.y` sin cambios.)

**Riesgo/efectos secundarios:** Cambio 100% local a un caso borde que hoy es infrecuente (requiere que un soldado esté parado casi exactamente sobre el punto de impacto). No cambia el comportamiento del caso normal (`away` con magnitud significativa). Vale la pena revisar visualmente en Play mode una granada que cae encima de un grupo apretado de soldados, para confirmar que el empuje se ve consistente entre todos (ninguno saltando).

---

### Bug 5: `Health.TakeDamage()` sin tope superior — daño negativo sobrecura sin límite

**Archivos:** `Combat/Health.cs:32-42`

**Causa raíz:** `Heal()` clampea explícitamente hacia arriba (`Current = Mathf.Min(maxHealth, Current + amount);`), pero `TakeDamage()` solo clampea hacia abajo (`Current = Mathf.Max(0, Current - amount);`). Si `amount` llega negativo por cualquier camino (arma con daño mal calculado, algún futuro efecto de estado, un valor de test), `Current - amount` se convierte en una SUMA sin ningún techo — `Current` puede terminar muy por encima de `maxHealth`, y el evento `DamageTakenEvent` sale publicado como si fuera daño real cuando en los hechos curó de más.

**Plan de implementación:**
1. Cambiar el clamp de una sola cota a un `Mathf.Clamp` de dos cotas, simétrico al de `Heal()`:
   ```csharp
   public void TakeDamage(int amount, int attackerId)
   {
       if (!IsAlive) return;

       Current = Mathf.Clamp(Current - amount, 0, maxHealth);
       LastAttackerId = attackerId;
       EventBus.Instance.Publish(new DamageTakenEvent(ActorId, attackerId, amount, Current));

       if (Current <= 0)
           EventBus.Instance.Publish(new EntityDiedEvent(ActorId));
   }
   ```
   Es el cambio mínimo que cierra el camino de sobrecura: cualquiera sea el signo de `amount`, `Current` queda siempre dentro de `[0, maxHealth]`.
2. (Opcional, fuera del alcance mínimo del bug reportado, evaluar aparte): agregar un guard temprano `if (amount <= 0) return;` como en `Heal()`, para que un "daño" de 0 o negativo ni siquiera publique `DamageTakenEvent`. No se incluye como paso obligatorio porque cambia además CUÁNDO se publican eventos (no solo el clamp), y el bug reportado es específicamente sobre el tope faltante — se deja anotado en Riesgos para que se decida aparte si conviene.

**Verificación:** Agregar en `RunPhase1` (o donde ya se manipula `vega.Health`/`enemy1.Health`) un `Check()` directo:
```csharp
int hpBeforeNegativeDamage = vega.Health.Current;
vega.Health.TakeDamage(-9999, -1);
Check($"TakeDamage con dano negativo no sobre-cura mas alla de maxHealth ({vega.Health.Current}/{vega.Health.MaxHealth})",
    vega.Health.Current <= vega.Health.MaxHealth);
```

**Riesgo/efectos secundarios:** Cambio de una sola línea, sin efecto sobre el camino normal (`amount > 0`, que es el 100% de los casos reales hoy: `WeaponCatalog` solo genera daños positivos). Repasar que ningún test existente dependa a propósito del comportamiento roto (no se encontró ninguno: los `TakeDamage(9999, ...)` usados para "matar" en la suite siguen funcionando igual, clampeados a 0).

---

### Bug 6: `Projectile.ActiveInstances` no se purga entre sesiones de Play sin domain reload

**Archivos:** `Combat/Projectile.cs:71` (declaración), `:129` (`OnSpawn`), `:138` (`OnDespawn`)

**Causa raíz:** `ActiveInstances` es un `static readonly List<Projectile>` que solo se mantiene vía `OnSpawn`/`OnDespawn` de instancias vivas EN LA MISMA sesión. Con "Enter Play Mode Options" configurado sin Domain Reload (una opción común en Unity 6 para iterar más rápido), los campos estáticos **no** se resetean al volver a entrar en Play — solo se recarga la escena. Un proyectil destruido al salir de una sesión de Play anterior deja una referencia "fake-null" colgada en la lista para siempre. Hoy esto lo consume código real de Play mode: `AutoDemoRunner.cs:394-396,429-431` (agarra el último elemento para seguirlo con cámara) y `UI/PerfHudView.cs:95` (muestra el conteo "proyectiles en vuelo" en el HUD de rendimiento) — ambos quedarían leyendo un estado inflado/incorrecto heredado de la sesión anterior. El propio `HeadlessTestRunner` ya hace `Projectile.ActiveInstances.Clear()` a mano al principio de `BuildAndRun`/los harnesses (líneas 241, 480, 569), pero eso es Edit mode, no cubre el camino real de Play mode.

**Plan de implementación:**
1. Aplicar el mismo patrón ya establecido en el proyecto para este problema exacto (ver `UI/AlertQueue.cs:75-76`, `ResetOnLoad()` con `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`). En `Combat/Projectile.cs`, cerca de la declaración de `ActiveInstances` (línea 71):
   ```csharp
   public static readonly List<Projectile> ActiveInstances = new List<Projectile>();

   // Los estaticos sobreviven a "Enter Play Mode" sin domain reload: sin
   // este reset, ActiveInstances arrastraria referencias fake-null de
   // proyectiles de la sesion de Play ANTERIOR (mismo patron que
   // AlertQueue.ResetOnLoad), inflando el contador de PerfHudView y
   // arriesgando a que AutoDemoRunner agarre una referencia vieja.
   [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
   static void ResetActiveInstancesOnLoad() => ActiveInstances.Clear();
   ```
2. No hace falta tocar `HeadlessTestRunner` (sus `.Clear()` manuales en Edit mode siguen siendo necesarios y quedan como defensa adicional, ya que `RuntimeInitializeOnLoadMethod` no se dispara en ese camino de Editor-tool).

**Verificación:** Como el hook solo se dispara al entrar en Play mode real (no en el runner headless de Edit mode), la verificación automatizada vía `Check()`/`RunPhaseN` no aplica directamente. Secuencia manual en Play mode:
1. En *Project Settings > Editor > Enter Play Mode Settings*, desmarcar "Reload Domain".
2. Abrir la escena de demo (`Strategic Point/Construir nivel para demo (sin test)`), entrar en Play, disparar varias veces y anotar el valor de "proyectiles en vuelo" del `PerfHudView`.
3. Salir de Play, volver a entrar SIN disparar nada todavía, y confirmar que el HUD arranca en 0 inmediatamente (antes del fix arrastraría el conteo viejo, o peor, quedaría con referencias fake-null contadas como "en vuelo").
4. Como chequeo de humo rápido, se puede loguear temporalmente `ActiveInstances.Count` dentro de `ResetActiveInstancesOnLoad()` para confirmar que se dispara una vez por entrada a Play, y sacar el log después.

**Riesgo/efectos secundarios:** Cambio aislado y de bajo riesgo — mismo patrón ya probado en `AlertQueue`. Único cuidado: `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` corre MUY temprano (antes de cargar cualquier escena), así que no debe asumirse que hay soldados/pool ya construidos en ese momento — acá el método no depende de nada de eso, solo limpia la lista, así que no hay problema de orden de inicialización.

---

### Bug 7: `SpatialGrid.cells` crece sin límite — nunca elimina celdas vacías

**Archivos:** `Core/SpatialGrid.cs:44` (declaración de `cells`), `:63-86` (`Rebuild()`)

**Causa raíz:** `Rebuild()` hace `foreach (var list in cells.Values) list.Clear();` y después repuebla, creando una entrada nueva en el diccionario para cualquier celda ocupada por primera vez — pero nunca hace `cells.Remove(key)` para una celda que dejó de tener soldados. Cada `List<Soldier>` que alguna vez tuvo contenido queda en el diccionario PARA SIEMPRE, vacía pero viva. `Rebuild()` se llama una vez por `WorldSimulationDriver.Step()` — o sea, ~60 veces por segundo en Play mode real — así que el costo de `foreach (var list in cells.Values) list.Clear()` crece con la cantidad de celdas *alguna vez* visitadas en toda la sesión, no con la cantidad de soldados vivos ahora. Con soldados/vehículos recorriendo un mapa grande durante una partida larga, esto es tanto una fuga de memoria (listas vacías nunca liberadas) como una degradación de rendimiento progresiva del propio `Rebuild()`.

**Plan de implementación:**
1. Agregar un buffer reusado (sin asignar por llamada, mismo criterio que el resto del archivo — comparar con los comentarios de `WaypointGraph` sobre "sin asignar por consulta") para juntar las claves vacías de esta pasada:
   ```csharp
   static readonly List<long> emptyKeysBuffer = new List<long>();
   ```
2. Al final de `Rebuild()`, después de repoblar, recorrer `cells` una vez para juntar las claves cuya lista quedó vacía, y removerlas en una segunda pasada (no se puede `Remove` mientras se itera el mismo diccionario):
   ```csharp
   public static void Rebuild()
   {
       foreach (var list in cells.Values) list.Clear();

       foreach (var s in ActorRegistry.All)
       {
           if (s == null || s.Health == null || !s.Health.IsAlive) continue;
           var key = CellOf(s.transform.position);
           if (!cells.TryGetValue(key, out var list))
           {
               list = new List<Soldier>();
               cells[key] = list;
           }
           list.Add(s);
       }

       // Purga las celdas que quedaron vacias en ESTE Rebuild: sin esto,
       // toda celda que alguna vez tuvo un soldado se queda en el
       // diccionario para siempre, y Rebuild() -- que corre una vez por
       // Step, sesenta veces por segundo -- se pone mas lento con la
       // vida de la partida solo por el tamaño del diccionario, no por
       // la cantidad real de soldados.
       emptyKeysBuffer.Clear();
       foreach (var kvp in cells)
           if (kvp.Value.Count == 0) emptyKeysBuffer.Add(kvp.Key);
       for (int i = 0; i < emptyKeysBuffer.Count; i++) cells.Remove(emptyKeysBuffer[i]);

       built = true;
   }
   ```
3. Agregar una propiedad de diagnóstico de solo lectura para poder verificar el tamaño desde fuera sin reflection, siguiendo el mismo criterio que otros contadores ya expuestos por el proyecto (`ProjectilePool.ExhaustedCount`, `SP.Presentation.OrderMarkerFx.ActiveCount`/`TotalCount`, `SelectionRingFx.SpawnCount`):
   ```csharp
   public static int CellCount => cells.Count;
   ```

**Verificación:** Agregar un bloque de prueba (en `RunPhase1` o como bloque aparte) que fuerza a un soldado a ocupar varias celdas bien separadas (múltiplos de `CellSize=20`) y confirma que, tras volver a una sola celda y hacer `Rebuild()` de nuevo, `SpatialGrid.CellCount` no arrastra las celdas viejas:
```csharp
var originalPos = kes.transform.position;
for (int i = 0; i < 5; i++)
{
    kes.transform.position = new Vector3(i * 25f, 0.8f, 0f); // 5 celdas bien separadas
    SP.Core.SpatialGrid.Rebuild();
}
kes.transform.position = originalPos;
SP.Core.SpatialGrid.Rebuild();
int cellCountAfterReturning = SP.Core.SpatialGrid.CellCount;
Check($"SpatialGrid purga las celdas vacias en Rebuild() (quedan {cellCountAfterReturning} celdas no vacias, no 5+ acumuladas)",
    cellCountAfterReturning < 5);
```
(El número exacto de celdas "hoy ocupadas" depende de cuántos soldados vivos hay en ese punto de la fase; lo importante del `Check` es que `CellCount` no siga sumando las 5 celdas visitadas históricamente sino que refleje solo las ocupadas ahora.)

Adicionalmente, correr `Strategic Point/Benchmark de rendimiento` antes/después del cambio (ya mide `WorldSimulationDriver.LastRebuildMs` por `N=10,60,200`, ver `HeadlessTestRunner.RunPerformanceBenchmarks`) para confirmar que el costo de `Rebuild()` no empeora — la pasada de purga agrega un recorrido más de `cells`, pero acotado al tamaño YA reducido del diccionario.

**Riesgo/efectos secundarios:** El patrón "vaciar → purgar" descarta y vuelve a crear el objeto `List<Soldier>` de una celda que se desocupa y luego se reocupa (churn de GC menor). Si medido con el benchmark esto resulta significativo a N=200, la mejora natural de seguimiento sería pooler esas listas en vez de destruirlas — se deja anotado como posible paso 2 y no como parte obligatoria de este fix, para no mezclar la corrección de la fuga con una optimización no pedida por el bug.

---

### Bug 8: `WeaponCatalog.Get()` esconde un `WeaponKind` futuro detrás del `default` de Rifle

**Archivos:** `Combat/WeaponCatalog.cs:25-38`

**Causa raíz:**
```csharp
case WeaponKind.Rifle:
default:
    return new Spec { Damage = 26, ... };
```
`Rifle` y `default` comparten el mismo caso. Si el día de mañana se agrega un cuarto valor a `enum WeaponKind { Rifle, Pistol, Heavy }`, cualquier código que llame `WeaponCatalog.Get(nuevoValor)` sin haber agregado su `case` cae en este mismo bloque y recibe silenciosamente las stats de Rifle — sin ningún log, sin ninguna señal de que falta configurar el arma nueva. El bug no es el fallback en sí (fallback razonable), es que sea **indistinguible** de un Rifle real elegido a propósito.

**Plan de implementación:**
1. Separar el `case WeaponKind.Rifle:` del `default:`, y hacer que `default:` loguee un warning explícito antes de caer al mismo Spec de Rifle como resguardo (mismo estilo defensivo que ya usa el proyecto, ej. `Debug.LogWarning` en `Projectile.Configure` cuando falta el `Renderer`, o en `WaypointGraph.Build` cuando se excede `MaxNodes`):
   ```csharp
   public static Spec Get(WeaponKind kind)
   {
       switch (kind)
       {
           case WeaponKind.Pistol:
               return new Spec { Damage = 14, Cooldown = 0.15f, Color = new Color(0.95f, 0.88f, 0.20f), VisualScale = new Vector3(0.13f, 0.13f, 0.28f), MagazineSize = 12, ReloadDuration = 1.0f };
           case WeaponKind.Heavy:
               return new Spec { Damage = 50, Cooldown = 0.80f, Color = new Color(0.80f, 0.20f, 0.55f), VisualScale = new Vector3(0.26f, 0.26f, 0.65f), MagazineSize = 4, ReloadDuration = 2.2f };
           case WeaponKind.Rifle:
               return new Spec { Damage = 26, Cooldown = 0.30f, Color = new Color(0.55f, 0.68f, 0.78f), VisualScale = new Vector3(0.15f, 0.15f, 0.55f), MagazineSize = 8, ReloadDuration = 1.5f };
           default:
               // WeaponKind sin Spec definido en el catalogo: no debe
               // pasar desapercibido como si fuera un Rifle elegido a
               // proposito. Avisa fuerte y cae a Rifle solo como ultimo
               // recurso, para no tirar el combate abajo por un dato
               // faltante.
               Debug.LogWarning($"[WeaponCatalog] WeaponKind.{kind} no tiene Spec definido -- usando stats de Rifle como resguardo.");
               goto case WeaponKind.Rifle;
       }
   }
   ```
   (`goto case` evita duplicar el literal de Rifle dos veces; es válido en C# dentro de un `switch`.)
2. No se toca ninguna firma pública ni ningún call site — el comportamiento para los 3 `WeaponKind` existentes es exactamente el mismo, byte a byte.

**Verificación:** Agregar en `RunPhase4`, cerca del loop "Probando las 3 armas recogibles" (`HeadlessTestRunner.cs:1088-1096`), un `Check()` que simula un `WeaponKind` futuro casteando un entero fuera de rango (no se puede agregar un 4to valor real al enum solo para la prueba):
```csharp
var fallbackSpec = WeaponCatalog.Get((WeaponKind)99);
var rifleSpec = WeaponCatalog.Get(WeaponKind.Rifle);
Check("WeaponCatalog.Get() con un WeaponKind desconocido cae a las stats de Rifle (no revienta ni devuelve basura)",
    fallbackSpec.Damage == rifleSpec.Damage && fallbackSpec.MagazineSize == rifleSpec.MagazineSize);
```
Como verificación complementaria (manual, no automatizada): correr `RunAll()` una vez con esa línea agregada temporalmente y confirmar a mano en la consola que aparece el nuevo `Debug.LogWarning` con el texto `"WeaponKind.99 no tiene Spec definido"` — antes de sacar la línea de prueba, si se decidiera no dejarla permanente en la suite.

**Riesgo/efectos secundarios:** Ninguno para el comportamiento actual. Si en un futuro cercano se agrega un `WeaponKind` real, este cambio hace que el olvido de agregar su `Spec` sea ruidoso (log) en vez de silencioso — que es exactamente el objetivo. Vigilar que cualquier código que dependa de "todo lo desconocido es Rifle" (no se encontró ninguno) siga funcionando: sigue siendo así, solo que ahora avisa.

---

### Bug 9: `Health.Initialize()` no resetea `LastAttackerId` — revivir deja al asesino de la vida anterior

**Archivos:** `Combat/Health.cs:25-30`

**Causa raíz:** `Initialize(int actorId, int max)` resetea `ActorId`, `maxHealth` y `Current`, pero nunca toca `LastAttackerId` (que solo arranca en `-1` por el inicializador de la propiedad, `Combat/Health.cs:23`). `Initialize()` es exactamente el método que `HeadlessTestRunner` (línea 1614, comentario `// revivido: no contamina el resto de la suite`) y `AutoDemoRunner` (líneas 85, 442, 537) usan para "revivir" a un soldado. Si un soldado revivido muere otra vez ANTES de recibir ningún daño nuevo en su vida nueva, o si algo consulta `LastAttackerId` justo después de revivir, va a ver al asesino de la muerte ANTERIOR — dato que ya no corresponde a nada real. Esto es justo lo que consumen `PlayerInputDriver.cs:513` (`ActorRegistry.FindById(deadSoldier.Health.LastAttackerId)`, para la cámara de muerte) y `KillFeedbackDirector.cs:78` (`victim.Health.LastAttackerId == Brain.Current.Id`, para saber si la baja fue tuya).

**Plan de implementación:**
1. Agregar el reset en `Initialize()`:
   ```csharp
   public void Initialize(int actorId, int max)
   {
       ActorId = actorId;
       maxHealth = max;
       Current = max;
       // Revivir (HeadlessTestRunner y AutoDemoRunner llaman Initialize()
       // para esto) tiene que borrar tambien quien te mato la vez
       // anterior: si no, un soldado recien revivido queda con
       // LastAttackerId apuntando al verdugo de su muerte ANTERIOR hasta
       // que alguien le pegue de nuevo en esta vida.
       LastAttackerId = -1;
   }
   ```
2. No hace falta tocar ningún llamador: `Soldier.cs:83` (`health.Initialize(Id, maxHealth)`, primer spawn) y `Vehicles/Vehicle.cs:35` (`health.Initialize(-1, maxHealth)`) ya arrancan con `LastAttackerId == -1` de todas formas, así que el reset ahí es un no-op inofensivo.

**Verificación:** El caso de revivir a Doc ya existe en `RunPhase7` (`HeadlessTestRunner.cs:1607-1614`, comentario `"subir a un muerto"`), pero hoy usa `doc.Health.TakeDamage(9999, -1)` — con atacante `-1`, que es justo el valor "reseteado", así que ese test específico NO expondría el bug aunque existiera. Ajustar esa línea a un atacante REAL y agregar el `Check` del reset:
```csharp
int docMaxHp = doc.Health.MaxHealth;
doc.Health.TakeDamage(9999, vega.Id); // atacante REAL (no -1), para poder verificar que Initialize() lo borra al revivir
bool montoAUnMuerto = vehicle.Mount(doc);
Check("Vehicle.Mount() rechaza a un soldado muerto (antes lo montaba igual)",
    !montoAUnMuerto && vehicle.OccupantCount == 0);
doc.Health.Initialize(doc.Id, docMaxHp); // revivido: no contamina el resto de la suite
Check("Initialize() al revivir borra el LastAttackerId de la muerte anterior (no debe seguir apuntando a Vega)",
    doc.Health.LastAttackerId == -1);
```

**Riesgo/efectos secundarios:** Cambio de una línea en `Health.Initialize()`, sin riesgo funcional — solo hace que un dato quede coherente con la vida actual del actor. El único ajuste necesario es en el propio test existente (cambiar `-1` por `vega.Id` en la línea 1610), para que efectivamente ejercite el camino que el bug describe.

---

### Bug 10: `SpatialGrid.Rebuild()` nunca llama `ActorRegistry.EnsureAllRegistered()` — soldados premontados invisibles para la IA

**Archivos:** `Core/SpatialGrid.cs:63-86` (`Rebuild()`) + `Core/ActorRegistry.cs:33-89` (`EnsureAllRegistered()`, `CountAlive()`)

**Causa raíz:** `ActorRegistry.CountAlive()` (línea 50) y `SelectionController.CollectLivingAllies()` (`Player/SelectionController.cs:177`) llaman `ActorRegistry.EnsureAllRegistered()` antes de leer `soldiers`, precisamente porque `Soldier.Awake()` no corre en un GameObject que arranca desactivado (ver el comentario del propio `EnsureAllRegistered`, `ActorRegistry.cs:26-32`). Pero `SpatialGrid.Rebuild()` — que es lo que `WorldSimulationDriver.Step()` llama UNA vez por tick, antes de todo sensado de IA (`Ai/WorldSimulationDriver.cs:40`) — nunca hace esa misma llamada. Peor: el propio bucle de tick de `WorldSimulationDriver.Step()` (`foreach (var s in ActorRegistry.All)`, línea 64) itera directamente `ActorRegistry.All`, así que un soldado nunca registrado no solo queda fuera de la grilla espacial: queda fuera de TODO tick de IA/arma del juego. Un soldado que arranca la escena ya desactivado (ej. premontado en un vehículo) puede quedar invisible para el sensado de toda la partida, salvo que algo (una UI que llame `CountAlive`, o el jugador cicleando con Q) lo registre por casualidad antes.

**Plan de implementación:**
1. En `Core/SpatialGrid.cs`, agregar `ActorRegistry.EnsureAllRegistered();` como la primera línea de `Rebuild()`, para que la puerta de entrada real de la simulación por tick quede con la misma garantía que ya tienen `CountAlive()`/`CollectLivingAllies()`:
   ```csharp
   public static void Rebuild()
   {
       // WorldSimulationDriver.Step() llama Rebuild() una vez por tick,
       // antes de cualquier sensado de IA -- es la puerta de entrada real
       // de la simulacion. Sin este EnsureAllRegistered(), un soldado que
       // arranca la escena ya desactivado (ej. premontado en un vehiculo)
       // nunca entra en ActorRegistry.All -- y por lo tanto nunca en esta
       // grilla -- salvo que, por casualidad, algo mas (CountAlive /
       // CollectLivingAllies) lo haya registrado antes. Mismo arreglo que
       // ya tienen esos dos, aplicado donde realmente hace falta.
       ActorRegistry.EnsureAllRegistered();

       foreach (var list in cells.Values) list.Clear();
       // ... resto sin cambios
   }
   ```
2. **Cuidado de rendimiento a medir, no a ignorar:** `EnsureAllRegistered()` hace `FindObjectsByType<Soldier>(FindObjectsInactive.Include, ...)` — un barrido completo de la escena — SIN ningún guard de "ya lo hice, no repetir" (a diferencia de `WorldSystemsRegistry.EnsurePopulated()`, que sí tiene su bandera `populated`). Llamarlo una vez por `Rebuild()` significa una vez por tick, ~60 veces por segundo en Play mode real — exactamente el patrón que el resto de este archivo (y `WorldSystemsRegistry`) se tomó el trabajo de eliminar en otros lados. Antes de dar este fix por terminado, correr `Strategic Point/Benchmark de rendimiento` (ya mide `WorldSimulationDriver.LastRebuildMs` para `N=10,60,200`) ANTES y DESPUÉS del cambio. Si el costo medido a N=200 resulta significativo, el siguiente paso natural (fuera del alcance mínimo de este bug, pero anotado para que no se pierda) es agregarle a `ActorRegistry` una bandera liviana tipo `EnsurePopulated`'s `populated`, invalidada explícitamente solo cuando se sabe que pudo aparecer un actor nuevo, en vez de escanear la escena entera en cada tick.

**Verificación:** Agregar un bloque de prueba (en `RunPhase1` o aparte) que arma un soldado que arranca desactivado SIN pasar por ningún registro manual, y confirma que un solo `SpatialGrid.Rebuild()` alcanza para que aparezca en `ActorRegistry.All`:
```csharp
var ghostSoldier = SpawnSoldier(soldierPrefab, "Fantasma_Premontado", TeamId.Enemy, RoleType.Enemy,
    vega.transform.position + Vector3.forward * 5f, enemyColor, pool, 100);
ghostSoldier.gameObject.SetActive(false); // simula "premontado en un vehiculo desde el arranque de la escena"
ActorRegistry.Unregister(ghostSoldier); // fuerza el caso "nunca paso por Awake", sin depender de si SpawnSoldier ya lo registro
SP.Core.SpatialGrid.Rebuild(); // exactamente lo que hace WorldSimulationDriver.Step() cada tick
Check("SpatialGrid.Rebuild() por si solo ya registra a un soldado que arranco desactivado (antes solo lo hacian CountAlive/CollectLivingAllies)",
    ActorRegistry.All.Contains(ghostSoldier));
```

**Riesgo/efectos secundarios:** El riesgo principal es de RENDIMIENTO, no de corrección — está explícitamente marcado arriba como el punto a medir con el benchmark ya existente del proyecto antes de considerar el fix "cerrado". No debería cambiar ningún comportamiento de juego observable (un soldado ya registrado no se re-registra dos veces, `Register()` ya es idempotente vía `Contains`), solo adelanta el momento en que un actor recién-activado-pero-nunca-tickeado entra al sistema.

---

### Bug 11: `WeaponPickup.EquipOn()` sin guarda de reentrancia/reclamado

**Archivos:** `Combat/WeaponPickup.cs:37-42`

**Causa raíz:** `EquipOn(WeaponHolder holder, int soldierId)` solo chequea `holder == null`, y después llama incondicionalmente `holder.EquipWeapon(...)` y publica `WeaponPickedUpEvent`. No hay ningún estado en `WeaponPickup` que registre "ya se está equipando" — nada impide que dos llamadas superpuestas al mismo pickup (por ejemplo, un futuro suscriptor de `WeaponPickedUpEvent` que reentra llamando `EquipOn` de nuevo sobre el mismo pickup antes de que la llamada externa termine, o un segundo camino de input agregado más adelante) dupliquen `EquipWeapon()` y el evento (doble sonido, doble aviso en pantalla, posible doble contabilización si algo suscripto cuenta pickups). Hoy hay un único llamador real por frame (`PlayerInputDriver.Interactuar`, gateado por `KeyBindings.WasPressed`, de flanco), así que nada de esto se dispara en la práctica — pero la clase en sí no ofrece ninguna defensa si ese supuesto deja de sostenerse.

Importante: este pickup **no** es de un solo uso — no se destruye ni se desactiva al equiparse (`RunPhase4`, líneas 1088-1096, reequipa la MISMA arma repetidas veces en el loop de prueba). Cualquier guarda tiene que bloquear solo la reentrancia (llamadas superpuestas en la misma pila de ejecución), no una segunda llamada legítima y posterior sobre el mismo pickup.

**Plan de implementación:**
1. Agregar un flag booleano de guarda, mismo idioma ya usado en el proyecto para secciones críticas cortas y síncronas (comparar con `WeaponHolder.bootstrapped`):
   ```csharp
   public class WeaponPickup : MonoBehaviour
   {
       [SerializeField] WeaponKind kind = WeaponKind.Rifle;
       [SerializeField] int damage = 34;
       [SerializeField] float cooldown = 0.35f;
       [SerializeField] Color color = Color.white;

       // Guarda de reentrancia: EquipOn() hoy tiene un unico llamador
       // (PlayerInputDriver.Interactuar, gateado por una tecla de flanco),
       // pero la clase en si no ofrece NINGUNA defensa si el dia de
       // mañana aparece un segundo camino (ej. un boton de UI ademas de
       // la tecla de cercania). Sin esta guarda, dos llamadas superpuestas
       // a EquipOn() en el mismo pickup duplicarian EquipWeapon() y el
       // WeaponPickedUpEvent.
       bool equipping;

       public WeaponKind Kind => kind;
       public Color Color => color;

       public void Configure(WeaponKind weaponKind, int weaponDamage, float weaponCooldown, Color weaponColor)
       {
           kind = weaponKind;
           damage = weaponDamage;
           cooldown = weaponCooldown;
           color = weaponColor;
       }

       public void EquipOn(WeaponHolder holder, int soldierId)
       {
           if (holder == null || equipping) return;
           equipping = true;
           try
           {
               holder.EquipWeapon(kind, damage, cooldown, color);
               EventBus.Instance.Publish(new WeaponPickedUpEvent(soldierId, kind));
           }
           finally
           {
               equipping = false;
           }
       }
   }
   ```
2. Como `EquipWeapon()` y `EventBus.Publish()` son 100% síncronos (nada de corrutinas ni async), `equipping` solo es `true` durante la extensión exacta de esa única llamada — así que la guarda bloquea reentrancia real, y deja pasar sin problema cualquier llamada posterior no superpuesta (el caso normal de re-equipar el mismo pickup más tarde, que `RunPhase4` ya ejercita).
3. No hace falta tocar ningún llamador: `Player/PlayerInputDriver.cs:842`, `Demo/AutoDemoRunner.cs:391` y `Editor/HeadlessTestRunner.cs:1092` siguen llamando `EquipOn(...)` exactamente igual.

**Verificación:** Agregar en `RunPhase4`, justo después del loop existente de "Probando las 3 armas recogibles" (`HeadlessTestRunner.cs:1088-1096`), un `Check()` que fuerza una reentrada real desde un suscriptor del propio evento que dispara `EquipOn`:
```csharp
int reentryPublishCount = 0;
using (EventBus.Instance.Subscribe<WeaponPickedUpEvent>(evt =>
{
    reentryPublishCount++;
    if (reentryPublishCount == 1) pickups[0].EquipOn(doc.Weapon, doc.Id); // reentrada deliberada
}))
{
    pickups[0].EquipOn(doc.Weapon, doc.Id);
}
Check("EquipOn() rechaza una llamada reentrante sobre el MISMO pickup mientras la primera todavia esta en curso (guarda 'equipping')",
    reentryPublishCount == 1);
```
Sin el fix, la llamada anidada dentro del suscriptor completaría normalmente y publicaría un SEGUNDO `WeaponPickedUpEvent`, llevando `reentryPublishCount` a 2; con el fix, la llamada anidada retorna de inmediato (porque `equipping` ya es `true`) y el contador queda en 1.

**Riesgo/efectos secundarios:** Cambio acotado y de bajo riesgo. Único cuidado real: si en el futuro se decide que este pickup SÍ debería consumirse/desaparecer al primer uso (cambio de diseño, no de este bug), la guarda `equipping` no sirve para eso — haría falta un campo aparte (`bool consumed`) y tocar los 3 call sites para reaccionar cuando `EquipOn` "falla" por ya-consumido. Ese es un cambio de reglas de juego, deliberadamente fuera del alcance de este fix defensivo.


---

# Player (input, órdenes, posesión) — Planes de corrección (18 bugs, incluye integración entre sistemas)

---

## ⚠️ Bug 1 (EL MÁS GRAVE DE ESTA TANDA): el remapeo de teclas (item 208) es cosmético — 8 acciones rebindeables no leen su bind real

**Archivos:** `Player/PlayerInputDriver.cs` — 10 sitios exactos: líneas 336, 361, 713, 755, 1397, 1414, 1455, 1722, 1800, 1821.

**Causa raíz:** `KeyRebindView` y `KeyBindings.Set()`/`Get()` funcionan perfectamente y persisten en `PlayerPrefs` (ver Bug 16). El problema es que **10 lecturas de teclado en `PlayerInputDriver` siguen usando el campo crudo del `Keyboard` de Unity** (`kb.rKey`, `kb.fKey`, `kb.tabKey`, `kb.hKey`, `kb.gKey`, `kb.vKey`, `kb.spaceKey`, `kb.xKey`) en vez de pasar por `KeyBindings.WasPressed(actionId)` / `KeyBindings.IsPressed(actionId)`, que es el indirection layer que ya usan correctamente ~19 otras lecturas del mismo archivo (`KeyBindings.WasPressed(KeyBindings.CiclarPosesion)`, `KeyBindings.Reagrupar`, etc. — ver líneas 422-426, 836, 1355-1356, 1768, 1775, 1782, 1789, 1795). El jugador abre el panel de configuración, remapea "Recargar" de R a otra tecla, la UI confirma el cambio y lo persiste — pero al volver al juego, apretar la tecla nueva no hace nada, y R (la vieja, "liberada") sigue disparando la acción. Es la única funcionalidad del item 208 que en apariencia "existe" pero no tiene ningún efecto real en 8 de las 19 acciones remapeables.

**Plan de implementación:**
Reemplazar, exactamente en estos 10 sitios, la lectura cruda por la llamada a `KeyBindings`. La acción correspondiente en cada caso ya existe como constante en `KeyBindings.cs` (confirmado contra los defaults):

1. **Línea 336** — `if (kb.hKey.wasPressedThisFrame && PauseRef != null) PauseRef.ToggleControlsOverlay();`
   → `if (KeyBindings.WasPressed(KeyBindings.Controles) && PauseRef != null) PauseRef.ToggleControlsOverlay();`
2. **Línea 361** — `if (kb.tabKey.wasPressedThisFrame && !handlingDeath)`
   → `if (KeyBindings.WasPressed(KeyBindings.AlternarVista) && !handlingDeath)`
3. **Línea 713** — `if (kb.rKey.wasPressedThisFrame) Brain.Current.Weapon.Reload();`
   → `if (KeyBindings.WasPressed(KeyBindings.Recargar)) Brain.Current.Weapon.Reload();`
4. **Línea 755** — `if (kb.fKey.wasPressedThisFrame && result.Type == AimTargetType.Ally) TryPossess(result.Soldier);`
   → `if (KeyBindings.WasPressed(KeyBindings.Poseer) && result.Type == AimTargetType.Ally) TryPossess(result.Soldier);`
5. **Línea 1397** — `if (kb.vKey.wasPressedThisFrame) vehicleFirstPerson = !vehicleFirstPerson;`
   → `if (KeyBindings.WasPressed(KeyBindings.CamaraVehiculo)) vehicleFirstPerson = !vehicleFirstPerson;`
6. **Línea 1414** — `if (kb.gKey.isPressed) { motor.Brake(Time.deltaTime); }` (rama del conductor)
   → `if (KeyBindings.IsPressed(KeyBindings.Frenar)) { motor.Brake(Time.deltaTime); }`
   — OJO: también hay que corregir la variable `isBraking` de la línea 1348 (`bool isBraking = currentSeat == VehicleSeatRole.Driver && kb.gKey.isPressed;`), que alimenta `VehicleStatus.UpdateFrom(...)` con el mismo criterio y quedaría inconsistente con el frenado real si no se actualiza junto con el punto 6 (aunque no está en la lista de 10 líneas del audit, es la MISMA lectura repetida a 66 líneas de distancia y romper solo una de las dos deja el HUD mintiendo sobre si se está frenando).
7. **Línea 1455** — `if (kb.rKey.wasPressedThisFrame) { turret.CycleAmmo(); ... }`
   → `if (KeyBindings.WasPressed(KeyBindings.Recargar)) { turret.CycleAmmo(); ... }` (mismo `actionId` que el punto 3: es la misma tecla física reutilizada en un contexto mutuamente excluyente — a pie vs. artillero —, igual que ya hace el proyecto con G en Frenar/orden-al-vehículo).
8. **Línea 1722** — `if (kb.fKey.wasPressedThisFrame)` (rama RTS de poseer)
   → `if (KeyBindings.WasPressed(KeyBindings.Poseer))`
9. **Línea 1800** — `if (kb.xKey.wasPressedThisFrame && Selection.Selected.Count > 0)`
   → `if (KeyBindings.WasPressed(KeyBindings.CancelarOrden) && Selection.Selected.Count > 0)`
10. **Línea 1821** — `if (kb.spaceKey.wasPressedThisFrame && Squad != null)`
    → `if (KeyBindings.WasPressed(KeyBindings.Recentrar) && Squad != null)`

No hace falta tocar la firma de `Update(Keyboard kb, ...)` en los métodos que siguen recibiendo `kb` para otras teclas no remapeables (WASD, disparo, números, Ctrl, Shift): esas nunca estuvieron en el alcance del item 208 y deben seguir leyendo `kb` directo.

**Verificación:** Agregar un `Check()` nuevo a `RunPhase7` en `HeadlessTestRunner.cs` (que ya usa reflection sobre campos privados de `PlayerInputDriver`, ver línea 1566 `currentSeatField`) que:
   1. Llame `KeyBindings.ResetToDefaults()` y `KeyBindings.Set(KeyBindings.Recargar, Key.U)` + `KeyBindings.InvalidateCache()`.
   2. Simule `Brain.Current.Weapon` con munición gastada y llame por reflection al método privado equivalente, o — más directo — exponga temporalmente (o invoque por reflection) la línea de recarga; como `UpdateFps` no es fácilmente invocable sin un `Keyboard` simulado en Edit mode, la forma más práctica es un test unitario indirecto: verificar que `KeyBindings.WasPressed(KeyBindings.Recargar)` devuelve `false` para R después del rebind y que el código fuente ya no contiene `kb.rKey` fuera de los usos legítimos — o, más robusto en este proyecto (que privilegia probar el comportamiento real), agregar un test de Play mode manual: rebindear R→U desde el panel de Configuración, apretar U en juego y confirmar en el log (`GameLog`) que el arma recargó, y confirmar que apretar R ya NO recarga. Documentar este paso como prueba manual de Play mode si no se justifica simular `InputSystem` sintético en la suite headless.

**Riesgo/efectos secundarios:** Verificar que ningún otro lugar del archivo siga leyendo `kb.rKey`/`kb.fKey`/`kb.tabKey`/`kb.hKey`/`kb.gKey`/`kb.vKey`/`kb.spaceKey`/`kb.xKey` fuera de estos 10 sitios corregidos (hacer un grep final de verificación tras el cambio). Prestar atención a que `KeyBindings.WasPressed`/`IsPressed` devuelven `false` en lugar de tirar cuando `Keyboard.current == null` (batch mode/tests), así que el comportamiento en Edit mode headless no cambia. El punto 6 (Frenar) y el punto 3/7 (Recargar reusado en dos contextos) son los más delicados: confirmar que ambos contextos (a pie vs. adentro del vehículo; conductor vs. artillero) siguen siendo mutuamente excluyentes tras el cambio, para no terminar dos acciones distintas disparando con la misma tecla en el MISMO contexto (eso sí sería el bug 15).

---

### Bug 2: clickear el minimapa también arranca un drag-select en el mundo

**Archivos:** `Player/PlayerInputDriver.cs` — `UpdateDragSelection` (~2024-2074) vs. el manejo de clic de minimapa (~1747-1766).

**Causa raíz:** `UpdateRts` llama primero `UpdateDragSelection(kb, mouse)` (línea 1638) y recién después evalúa el clic sobre el minimapa (línea 1747, dentro del mismo `Update` lógico). `UpdateDragSelection` arma `dragging = true` en `mouse.leftButton.wasPressedThisFrame` sin preguntar nunca si el puntero está sobre una UI (`EventSystem.current.IsPointerOverGameObject()`), así que un clic sobre el `RectTransform` del minimapa dispara IGUAL el flujo de selección por arrastre en el mundo 3D. Si el jugador suelta el botón sin mover el mouse (el caso normal al clickear el minimapa), `UpdateDragSelection` interpreta ese click-sin-arrastre como un raycast al mundo bajo el cursor (que en la práctica cae sobre lo que sea que esté "detrás" del Canvas del minimapa en pantalla) y puede reemplazar o vaciar la selección recién hecha por el clic real del minimapa.

**Plan de implementación:**
1. Agregar `using UnityEngine.EventSystems;` a los `using` de `PlayerInputDriver.cs` (no está importado actualmente).
2. Al principio de `UpdateDragSelection(Keyboard kb, Mouse mouse)`, antes de leer `mouse.leftButton.wasPressedThisFrame`, agregar un corte temprano:
   ```csharp
   if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
   {
       // Si el click empezo sobre UI (ej. el minimapa), no arranca ni
       // continua un drag-select en el mundo -- pero si ya estaba
       // arrastrando desde el mundo y el mouse paso por encima de un
       // panel a mitad de camino, no lo cortamos a la mitad.
       if (!dragging) return;
   }
   ```
   Esto cubre tanto el `wasPressedThisFrame` (no arranca el drag) como, si haría falta, el caso de estar readonly sobre UI mientras ya se estaba arrastrando (se deja continuar para no cortar un drag legítimo que pasó momentáneamente sobre un ícono).
3. Confirmar el orden de las dos ramas en `UpdateRts`: como el chequeo de minimapa (línea 1747) ya está fuera de `UpdateDragSelection` y corre después en el mismo frame, con el guard de arriba el drag nunca se arma sobre el minimapa, así que el bloque de la línea 1747 queda como el único que reacciona a ese clic — sin necesidad de reordenar nada más.
4. Repetir el mismo criterio (opcional, defensivo) en el guard de `mouse.rightButton.wasPressedThisFrame` de `rightDragStart` (línea 1648), ya que clickear con el botón derecho sobre el minimapa hoy también arranca el paneo por arrastre con el mismo problema de fondo, aunque no esté explícitamente en el bug reportado.

**Verificación:** No es viable simular `EventSystem.current.IsPointerOverGameObject()` de forma confiable en Edit mode headless sin un `PointerEventData` real, así que el mejor camino es una prueba manual de Play mode: entrar en RTS, clickear una sola vez (sin arrastrar) sobre el minimapa con una selección previa hecha, y confirmar en el log/HUD que la selección NO cambia y que la orden se emitió (el toast "ORDEN DESDE EL MINIMAPA"). Como chequeo adicional automatizable, se puede verificar en `HeadlessTestRunner` que el campo `SelectionBox` (o el estado `dragging` vía reflection) nunca queda en `true` inmediatamente después de invocar el método que resuelve un clic de minimapa simulado con `IsPointerOverGameObject` forzado — pero dado que esto depende de `EventSystem`, que no corre en Edit mode, se documenta como prueba manual.

**Riesgo/efectos secundarios:** Confirmar que el guard no rompe la selección por arrastre normal cuando el drag empieza en el mundo y el mouse pasa fugazmente sobre el `SelectionCount`/`ModeToast`/otros elementos de HUD que puedan estar bajo el cursor — de ahí el `if (!dragging) return;` en vez de bloquear todo el método sin condición. Revisar también que ningún otro Canvas (GroupCards, KillFeedView) tape una región grande de la pantalla de juego en RTS y termine bloqueando drags legítimos por accidente.

---

### Bug 3: `currentSeat` no se limpia si el soldado poseído muere estando montado

**Archivos:** `Player/PlayerInputDriver.cs` — `OnEntityDied` (~452-477), `DeathSequence` (~479-590), `Update` (~409).

**Causa raíz:** `Update()` decide el camino de entrada (`UpdateInVehicle` vs. `UpdateFps`/`UpdateRts`) mirando únicamente `currentSeat.HasValue` (línea 409). `DeathSequence` cambia de poseído con `PossessionService.Swap(Brain, nearest)` (línea 571) pero nunca toca el campo `currentSeat`, que es estado propio de `PlayerInputDriver` y no algo que `PossessionService`/`Vehicle` puedan conocer. Si el soldado poseído muere estando montado (`currentSeat` con un valor no nulo), tras el swap el nuevo poseído — que casi seguro NO está en el vehículo — queda enrutado igual a `UpdateInVehicle` por el resto de la partida, porque `currentSeat` sigue "pegado" con el rol viejo.

**Plan de implementación:**
1. Extraer un pequeño helper privado nuevo en `PlayerInputDriver.cs`, cerca de `ExitVehicle()`, que centralice el reseteo de estado de asiento (se reutiliza también en el Bug 14):
   ```csharp
   // Un solo lugar que deja de tratar al jugador como "adentro de un
   // vehiculo": lo usa tanto la muerte del poseido como la rama de
   // vehiculo destruido (UpdateInVehicle). A diferencia de ExitVehicle(),
   // NO llama a Vehicle.Dismount: el cuerpo ya no es el poseido (murio) o
   // el vehiculo ya expulso a todos por su cuenta (OnDestroyed).
   void ClearVehicleSeatState()
   {
       if (currentSeat.HasValue && Vehicle != null)
       {
           Vehicle.PlayerAboard = false;
           var vb = Vehicle.GetComponent<VehicleBrain>();
           if (vb != null && currentSeat == VehicleSeatRole.Driver) vb.IsPlayerDriving = false;
       }
       currentSeat = null;
   }
   ```
2. Llamarlo en `DeathSequence`, justo donde ya se limpia el otro estado del soldado muerto (junto a `deadSoldier.SetBodyVisible(true); bodyHiddenFor = null;`, línea ~505-506), ANTES de decidir a quién poseer:
   ```csharp
   deadSoldier.SetBodyVisible(true);
   bodyHiddenFor = null;
   ClearVehicleSeatState();
   ```
3. No hace falta tocar `OnEntityDied` en sí: el `StartCoroutine(DeathSequence(...))` ya es el único punto de entrada para la muerte del poseído.

**Verificación:** Agregar un `Check()` en `RunPhase5` o `RunPhase7` de `HeadlessTestRunner.cs` (que ya monta/desmonta vehículos y usa reflection sobre `currentSeat`, ver `currentSeatField` en `RunPhase7`): montar a Vega (el poseído) como conductor (`vehicle.Mount(vega, VehicleSeatRole.Driver)` + `inputDriver` con `currentSeatField.SetValue(inputDriver, VehicleSeatRole.Driver)` simulando `EnterPossessedVehicleSeat`), matarlo (`vega.Health.TakeDamage(9999, -1)`, publicando `EntityDiedEvent` si el `Health`/`Soldier` no lo hace solo — revisar `Health.TakeDamage` para confirmar que publica el evento), simular el paso de la corrutina de muerte lo necesario (o invocar `DeathSequence` reflejado si no corre por no ser Play mode), y verificar con `currentSeatField.GetValue(inputDriver) == null` tras la secuencia.

**Riesgo/efectos secundarios:** Confirmar que `Vehicle` (el campo público, no el vehículo genérico) sigue apuntando al vehículo correcto en el momento en que corre `ClearVehicleSeatState()` — si en el futuro `Vehicle` llegara a cambiar antes de este punto, el reseteo de `PlayerAboard`/`IsPlayerDriving` apuntaría al vehículo equivocado. Verificar también que esto no interfiere con el camino normal de `ExitVehicle()` (que sigue llamando a `Vehicle.Dismount` explícitamente y no debería duplicar lógica de forma incompatible).

---

### Bug 4: las órdenes de selección no descartan a los soldados muertos

**Archivos:** `Player/OrderService.cs` — `IssueMoveOrderForSelection` (~240), `IssueFormationOrderForSelection` (~253-266), `IssueAttackOrderForSelection` (~229-238), `IssueMountOrderForSelection` (~362-371). Combinado con `Player/SelectionController.cs` (nunca depura `selected` cuando alguien muere).

**Causa raíz:** `RegroupSelection` (línea 386-389) e `IssueRetreatOrderForSelection` (línea 459-461) filtran explícitamente `s.Health.IsAlive` antes de operar. Las otras cuatro (`IssueMoveOrderForSelection`→`IssueFormationOrderForSelection`, `IssueAttackOrderForSelection`, `IssueMountOrderForSelection`) simplemente hacen `new List<Soldier>(selection)` y operan sobre todos, vivos o muertos. Como `SelectionController.selected` tampoco se depura nunca al morir alguien (no hay ningún `EventBus.Instance.Subscribe<EntityDiedEvent>` en ese archivo), un soldado seleccionado que muere se queda en la lista de selección indefinidamente. El impacto concreto: `AiBrain.IssueMoveOrder/IssueAttackOrder/IssueMountOrder` (`Ai/AiBrain.cs:233-284`) tampoco chequean `IsAlive` al asignar el estado — sólo `AiBrain.Tick` lo hace (línea 312, fuerza `AiState.Dead` y sale), así que el cadáver no camina, pero el PEDIDO sigue contando: `AnnounceBatch` reporta "se dio la orden a N soldados" incluyendo muertos, `OrderMarkerFx.Spawn` dibuja un marcador para una orden que nunca se va a cumplir, y — el efecto más visible — `IssueFormationOrderForSelection` calcula `FormationPoints(center, forward, list.Count, kind, spacing)` con el `Count` INFLADO por los muertos, y asigna `spots[i]` a `list[i]` en el orden original (línea 262-263): los soldados vivos terminan recibiendo una formación pensada para más integrantes de los que realmente hay, con huecos donde deberían estar los caídos, en vez de una formación compacta ajustada a los vivos (que es justamente lo que sí logra `RegroupSelection` con su asignación por cercanía).

**Plan de implementación:**
1. En `OrderService.cs`, agregar un helper privado reutilizable, con el mismo criterio que ya usan `RegroupSelection`/`IssueRetreatOrderForSelection`:
   ```csharp
   static List<Soldier> AliveOnly(IEnumerable<Soldier> selection)
   {
       var list = new List<Soldier>();
       if (selection == null) return list;
       foreach (var s in selection)
           if (s != null && s.Health != null && s.Health.IsAlive) list.Add(s);
       return list;
   }
   ```
2. En `IssueFormationOrderForSelection` (línea 253-257), reemplazar:
   ```csharp
   if (selection == null) return;
   var list = new List<Soldier>(selection);
   if (list.Count == 0) return;
   ```
   por:
   ```csharp
   var list = AliveOnly(selection);
   if (list.Count == 0) return;
   ```
   Como `IssueMoveOrderForSelection` delega en `IssueFormationOrderForSelection` (línea 244), este único cambio arregla ambas.
3. En `IssueAttackOrderForSelection` (línea 231-233), mismo reemplazo: `var list = AliveOnly(selection);`.
4. En `IssueMountOrderForSelection` (línea 364-366), mismo reemplazo: `var list = AliveOnly(selection);`.
5. Además, como defensa en el otro extremo (no solo en `OrderService`), suscribir `SelectionController` a `EntityDiedEvent` para depurar `selected` en el momento en que alguien muere, en vez de esperar a la próxima orden:
   ```csharp
   IDisposable deathSub;
   void OnEnable() => deathSub = EventBus.Instance.Subscribe<EntityDiedEvent>(OnEntityDied);
   void OnDisable() => deathSub?.Dispose();
   void OnEntityDied(EntityDiedEvent evt)
   {
       int before = selected.Count;
       selected.RemoveAll(s => s == null || s.Id == evt.ActorId);
       if (selected.Count != before) Publish();
   }
   ```
   Esto es defensa en profundidad: aunque `OrderService` ya filtre, el HUD (`SelectionCountView`, `GroupCardsView`) sigue contando muertos en `Selection.Selected.Count` hasta la próxima orden si no se hace esto.

**Verificación:** Agregar un `Check()` en `RunPhase3` (que ya maneja selección múltiple, ver firma `RunPhase3(playerBrain, rig, selection, aim, vega, kes, doc, ...)`): seleccionar a `kes` y `doc`, matar a `doc` con `doc.Health.TakeDamage(9999, -1)` (publicando `EntityDiedEvent`), y verificar: (a) `selection.Selected.Count == 1 && selection.Selected[0] == kes` tras el evento (prueba del punto 5); (b) llamar `OrderService.IssueMoveOrderForSelection(selection.Selected, algúnPunto)` y confirmar con `OrderHistory.TryGet(0, out var entry)` que `entry.ActorCount == 1` y no 2 (prueba de los puntos 2-4); revivir a `doc` al final con `doc.Health.Initialize(doc.Id, docMaxHp)` para no contaminar el resto de la suite, siguiendo el mismo patrón ya usado en `RunPhase7` línea 1614.

**Riesgo/efectos secundarios:** Confirmar que ningún llamador dependía de que la lista devuelta incluyera muertos (revisar `AnnounceBatch`, que ahora recibirá listas más chicas — esto es deseado, pero el conteo en pantalla bajará respecto del comportamiento actual, lo cual es el fix, no una regresión). Verificar que el nuevo `OnEntityDied` de `SelectionController` no colisiona en orden de suscripción con otros oyentes de `EntityDiedEvent` que dependan de leer `Selection.Selected` ANTES de la depuración (revisar `GroupCardsView`/`SelectionCountView` si escuchan el mismo evento).

---

### Bug 5: el indicador de montaje de vehículo (`VehicleMountIndicator`) queda flotando en el mundo

**Archivos:** `Player/PlayerInputDriver.cs:957-979` (`UpdateVehicleMountIndicator`), llamado solo desde `UpdateFps` (línea 709).

**Causa raíz:** `UpdateVehicleMountIndicator` es la única función que llama a `mountIndicator.Hide()` (línea 961, cuando `result.Type != AimTargetType.Vehicle`), y sólo se invoca desde dentro de `UpdateFps`. Si el jugador está apuntando a un vehículo (el indicador visible) y aprieta `[TAB]` para pasar a RTS, o monta con `[E]`/`[X]` (que llama `EnterVehicle` → `EnterPossessedVehicleSeat`, entrando a `UpdateInVehicle` en el próximo frame), `UpdateFps` deja de correr por completo y nada vuelve a llamar `Hide()`: el objeto `VehicleMountIndicator` (flecha + líneas hacia aliados cercanos) queda clavado en el mundo indefinidamente, visible incluso en vista RTS o adentro del vehículo.

**Plan de implementación:**
1. En `UpdateInVehicle` (línea 1310), justo en el bloque de limpieza de estado que ya existe al principio (junto a `ClearNearestAllyHighlight()`, línea 1325), agregar:
   ```csharp
   if (mountIndicator != null) mountIndicator.Hide();
   ```
2. En `UpdateRts` (línea 1588), en el mismo bloque de limpieza inicial (junto a `ClearNearestAllyHighlight()`, línea 1602), agregar la misma línea:
   ```csharp
   if (mountIndicator != null) mountIndicator.Hide();
   ```
3. Alternativa más robusta (recomendada si se quiere un solo punto de verdad): mover el `Hide()` a un helper `HideFpsOnlyIndicators()` llamado al INICIO de `UpdateInVehicle` y `UpdateRts`, que agrupe `mountIndicator.Hide()` + la limpieza de `nearestAllyRing`/`highlightedRenderer` (ver Bug 6) en un solo lugar, ya que los tres estados (resalte de aliado cercano, resalte de aim, indicador de montaje) son "solo válidos en UpdateFps" y hoy se limpian de forma inconsistente (uno sí — `ClearNearestAllyHighlight` — y dos no).

**Verificación:** Agregar un `Check()` en `RunPhase4`/`RunPhase7` (que ya manipulan vehículos): con `Rig.Mode == ControlMode.Fps`, forzar (via reflection sobre el campo privado `mountIndicator`, o invocando `UpdateVehicleMountIndicator` con un `AimResult` de tipo `Vehicle` por reflection) que el indicador quede creado y activo; luego invocar `Rig.ToggleMode()`/simular `[TAB]` y `UpdateRts` (o el método público equivalente), y verificar por reflection que `mountIndicator` esté oculto (exponer un `mountIndicator.IsVisible` o similar en `VehicleMountIndicator` si no existe ya, para no depender de inspeccionar `GameObject.activeSelf` a mano).

**Riesgo/efectos secundarios:** Si se agrega `IsVisible` a `VehicleMountIndicator.cs`, confirmar que no rompe su propia lógica interna de pooling/reactivación. Verificar que ocultar el indicador al entrar al vehículo no interfiere con el frame de transición (`EnterPossessedVehicleSeat` corre antes de que `Update()` decida la rama `UpdateInVehicle`, así que no hay carrera de un frame).

---

### Bug 6: el resalte de apuntado (`UpdateAimHighlight`) no se revierte al salir de FPS

**Archivos:** `Player/PlayerInputDriver.cs:894-917` (`UpdateAimHighlight`), llamado solo desde `UpdateFps` (línea 708).

**Causa raíz:** Mismo patrón exacto que el Bug 5. `UpdateAimHighlight` es la única función que restaura el color original vía `SP.Presentation.CubeFxReactor.WriteTint(highlightedRenderer, highlightedOriginalColor)` (línea 908-909) cuando el target cambia o desaparece, y sólo se llama desde `UpdateFps`. Si se deja de correr `UpdateFps` (por `[TAB]` a RTS, o al montar en el vehículo) mientras había un aliado o vehículo resaltado (tinte blanqueado), ese `Renderer` se queda con el tinte de resalte para siempre — y como los soldados de un mismo equipo COMPARTEN material (comentario de la línea 906, item 230), un aliado que quedó "pintado" blanco puede arrastrar a todo su equipo visualmente si el material compartido no se revierte.

**Plan de implementación:**
1. Igual que el Bug 5, agregar en `UpdateInVehicle` y `UpdateRts` (en el mismo bloque inicial de limpieza) una llamada a revertir el resalte si quedó alguno activo:
   ```csharp
   if (highlightedRenderer != null)
   {
       SP.Presentation.CubeFxReactor.WriteTint(highlightedRenderer, highlightedOriginalColor);
       highlightedRenderer = null;
   }
   ```
2. Si se opta por el helper unificado propuesto en el Bug 5 (`HideFpsOnlyIndicators`), meter este bloque ahí también, junto a `mountIndicator.Hide()` y `ClearNearestAllyHighlight()` — los tres son, literalmente, el mismo tipo de bug repetido tres veces por la misma causa (estado que sólo un método limpia, y sólo un método lo llama).

**Verificación:** Agregar un `Check()`: apuntar a un aliado en FPS (invocando `Aim.Evaluate` + `UpdateAimHighlight` por reflection, o comprobando el color real vía `CubeFxReactor.ReadTint` sobre el `Renderer` del aliado apuntado) para confirmar que el tinte cambió; luego forzar la transición a RTS/vehículo y confirmar con `CubeFxReactor.ReadTint(renderer)` que volvió a ser igual a `highlightedOriginalColor` (accesible por reflection) o al color base del equipo.

**Riesgo/efectos secundarios:** Confirmar que revertir el tinte en el momento de cambiar de modo no colisiona con otro sistema que en simultáneo esté tiñendo el mismo `Renderer` por otro motivo (por ejemplo `SelectionRingFx` no toca el material del cuerpo, así que no debería haber conflicto, pero si en el futuro se agrega un segundo consumidor de `CubeFxReactor.WriteTint` sobre soldados, esto necesitaría un manejo de prioridad/pila en vez de "guardar un solo color original").

---

### Bug 7: `nearVehicle` no descarta vehículos destruidos

**Archivos:** `Player/PlayerInputDriver.cs:826-828`.

**Causa raíz:** 
```csharp
var nearVehicle = Vehicle != null && Vector3.Distance(Brain.Current.transform.position, Vehicle.transform.position) <= interactRadius
    ? Vehicle : null;
```
Esta variable sólo mira distancia, no `Vehicle.IsDestroyed` — a diferencia de `GOrderOnVehicle` (línea 941: `if (vehicle.IsDestroyed) return;`) y de la rama `[G]` de RTS (línea 1707: `if (result.Type == AimTargetType.Vehicle && !result.Vehicle.IsDestroyed)`). El efecto concreto: parado junto a una carcasa quemada, `nearVehicle` sigue siendo no-nulo, así que `SetInstructionText` (línea 846) sigue mostrando `"[E] Subir al vehiculo..."` sobre un vehículo inservible, y apretar `[E]`/`[X]` (línea 836-843) llama `EnterVehicle(nearVehicle)`. Como `Vehicle.Mount()` sí rechaza internamente (`IsDestroyed`, línea 212 de `Vehicle.cs`), la consecuencia visible no es un crash sino un fallo COMPLETAMENTE silencioso: no pasa nada y no hay ningún aviso tipo "VEHICULO DESTRUIDO" (que sí muestran `GOrderOnVehicle` y la rama RTS), dejando al jugador sin ninguna pista de por qué la tecla "no funciona".

**Plan de implementación:**
1. Corregir la asignación de `nearVehicle` (línea 826-828):
   ```csharp
   var nearVehicle = Vehicle != null && !Vehicle.IsDestroyed
       && Vector3.Distance(Brain.Current.transform.position, Vehicle.transform.position) <= interactRadius
       ? Vehicle : null;
   ```
2. Con este cambio, `SetInstructionText` deja de ofrecer "[E] Subir al vehiculo" sobre una carcasa (cae al siguiente caso del operador ternario: pickup cercano o instrucción por defecto), y el bloque `if (KeyBindings.WasPressed(KeyBindings.SubirBajarVehiculo) && nearVehicle != null)` (línea 836) directamente no entra, dejando pasar a la rama `Interactuar` (equipar pickup) si corresponde.
3. Opcional pero recomendado para paridad de UX con `GOrderOnVehicle`/RTS: si se quiere el mismo aviso explícito "VEHICULO DESTRUIDO" en vez de silencio, se puede detectar el caso por separado (vehículo destruido Y en rango) antes de descartarlo del todo, y llamar `RejectOrder("VEHICULO DESTRUIDO")` cuando se presione la tecla de interactuar apuntando/cerca de una carcasa — pero esto es una mejora de UX adicional, no estrictamente necesaria para cerrar el bug reportado (que es sobre el filtro faltante, no sobre el mensaje).

**Verificación:** Agregar un `Check()` en la fase que ya destruye un vehículo (revisar `RunPhase5`/`RunPhase6`, que trabajan con `vehicle` y probablemente ya lo llevan a 0 HP en algún punto — confirmar buscando `vehicle.TakeDamage` o `OnDestroyed` en esas fases): tras destruir el vehículo, posicionar a Vega dentro de `interactRadius`, invocar el getter de `nearVehicle` por reflection (o el método público más cercano que lo exponga) y verificar `== null`; y confirmar que `EnterVehicle` no se llega a invocar mediante el flujo de `[E]` simulado.

**Riesgo/efectos secundarios:** Verificar que `FindNearestPickup` (que sigue evaluándose en paralelo, línea 828) no se ve afectado por este cambio — debería seguir funcionando igual, ya que es independiente. Confirmar que ningún otro punto del archivo lee el campo público `Vehicle` asumiendo que nunca está destruido cuando está "cerca" (por ejemplo `MinimapRef.Target`, línea 354, que usa `Vehicle.transform` si `currentSeat.HasValue` — ese caso es distinto, no pasa por `nearVehicle`, así que no se ve afectado).

---

### Bug 8: `EnterVehicle` itera `Squad` sin chequeo de null

**Archivos:** `Player/PlayerInputDriver.cs:1252`.

**Causa raíz:** 
```csharp
foreach (var s in Squad)
{
    if (s == null || s == driverSoldier || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
    ...
}
```
Todos los demás consumidores de `Squad` en el archivo (por ejemplo `UpdateNearestAllyHighlight` línea 869 `if (Squad == null || ...) return;`, `CycleLivingAlly` línea 1128 `if (Squad == null || Squad.Count == 0) return;`, `FindNextSquadmateToBoard` línea 1090 `if (Squad == null) return null;`, `UpdateVehicleMountIndicator` línea 968 `if (Squad != null) { foreach... }`) chequean `Squad == null` antes de iterar. `EnterVehicle` es la única excepción: si por algún motivo `Squad` no está asignado (falla de wiring en la escena, un test que construye `PlayerInputDriver` a mano sin poblar `Squad`, o un futuro camino de inicialización parcial), el `foreach` tira `NullReferenceException` y aborta a mitad de la función — dejando al jugador ya montado (el `Mount(driverSoldier, role)` de la línea 1247 ya corrió) pero sin haber intentado subir a los aliados cercanos, y potencialmente rompiendo el frame entero si la excepción no está contenida.

**Plan de implementación:**
1. En `EnterVehicle(Vehicle vehicle)` (línea 1234), envolver el `foreach` con el mismo guard que usa el resto de la clase:
   ```csharp
   // Los aliados libres cerca también suben, en cualquier asiento libre.
   if (Squad != null)
   {
       foreach (var s in Squad)
       {
           if (s == null || s == driverSoldier || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
           if (Vector3.Distance(s.transform.position, vehicle.transform.position) <= autoMountRadius)
               vehicle.Mount(s);
       }
   }
   ```

**Verificación:** Agregar un `Check()` dedicado (puede ir en `RunPhase4`, que ya prueba `EnterVehicle`/montaje): guardar el valor actual de `inputDriver.Squad`, ponerlo en `null` temporalmente, invocar `EnterVehicle` (público) sobre un vehículo con asiento libre, confirmar que NO tira excepción (envolver en try/catch en el propio test y hacer `Check("EnterVehicle no revienta con Squad null", !exceptionThrown)`), y restaurar `inputDriver.Squad` al valor original inmediatamente después para no afectar el resto de la suite.

**Riesgo/efectos secundarios:** Ninguno relevante — es un cambio puramente defensivo que no altera el comportamiento cuando `Squad` sí está poblado (que es el 100% de los casos reales en el juego shippeado). Confirmar que el test que fuerza `Squad = null` restaura el campo antes de que corran las fases siguientes, ya que muchas otras fases dependen de `inputDriver.Squad` para sus propios chequeos.

---

### Bug 9: `SelectAlliesInScreenRect` itera `Squad` sin chequeo de null

**Archivos:** `Player/PlayerInputDriver.cs:2112`.

**Causa raíz:** Mismo patrón que el Bug 8, en otro método:
```csharp
void SelectAlliesInScreenRect(Vector2 a, Vector2 b, bool addToExisting)
{
    ...
    foreach (var s in Squad)
    { ... }
}
```
Sin guard de `Squad == null`. Este método se llama desde `UpdateDragSelection` (línea 2071) cuando el jugador termina un arrastre de selección en RTS que superó el umbral de píxeles — un camino de uso muy frecuente (cualquier selección múltiple por arrastre), así que si `Squad` llegara a ser `null` en algún momento del ciclo de vida (por ejemplo durante una recarga de escena a medio terminar, o un test que no lo pobló), el primer drag-select del jugador tira `NullReferenceException`.

**Plan de implementación:**
1. En `SelectAlliesInScreenRect(Vector2 a, Vector2 b, bool addToExisting)` (línea 2104), agregar el guard al principio, con el mismo criterio que el resto de la clase:
   ```csharp
   void SelectAlliesInScreenRect(Vector2 a, Vector2 b, bool addToExisting)
   {
       if (Squad == null) return;

       float minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x);
       ...
   }
   ```
   (Return temprano en vez de envolver el `foreach`, porque el resto del método — `first`, `any`, el `Selection.Clear()` final — no tiene sentido ejecutarse sin escuadra: sin `Squad` no hay nada que seleccionar y el comportamiento correcto es "no cambiar nada", no "vaciar la selección".)
   — Alternativa más fiel al comportamiento actual con `Squad` vacío: si se prefiere preservar el `Selection.Clear()` de `!any && !addToExisting` incluso sin `Squad`, envolver sólo el `foreach` como en el Bug 8. Cualquiera de las dos es válida; se recomienda el `return` temprano por ser más simple y no tener efectos secundarios sorprendentes.

**Verificación:** Agregar un `Check()` en `RunPhase2`/`RunPhase3` (que ya usan `SelectionController` y simulan selección múltiple): con `inputDriver.Squad` puesto a `null` temporalmente, invocar `SelectAlliesInScreenRect` por reflection con un rectángulo cualquiera, confirmar que no tira excepción, y restaurar `Squad` al valor original.

**Riesgo/efectos secundarios:** Igual que el Bug 8: cambio puramente defensivo. Si se elige la variante "return temprano", confirmar que no cambia el comportamiento observable en el caso normal (`Squad` poblado), ya que la lógica interna no se toca, sólo se agrega el guard de entrada.

---

## ⚠️ Bug 11 (EL OTRO BUG MUY GRAVE — cross-file): el juego puede reanudarse a velocidad normal DETRÁS de la pantalla de victoria, para siempre

**Archivos:** `Presentation/KillFeedbackDirector.cs:46,72-98,174-218` + `Presentation/BattleManager.cs` + `Presentation/GameOutcomeController.cs`.

**Causa raíz:** `EntityDiedEvent` tiene dos oyentes independientes que, en la última baja de la partida, PUEDEN escribir `Time.timeScale` sin coordinarse entre sí: `KillFeedbackDirector.OnDied` → `TrySlowMotionOnLastKill()` → `StartCoroutine(SlowMotionRoutine())`, cuyo cuerpo corre de forma SÍNCRONA hasta el primer `yield` (así es como funcionan las corrutinas de Unity), y ahí mismo hace `Time.timeScale = SlowMotionScale` (0.25, línea 184); y `BattleManager.OnEntityDied` → `Outcome.ShowVictory()`, que hace `Time.timeScale = 0f` (línea 151 de `GameOutcomeController.cs`) también de forma síncrona. `EventBus.Publish` (`Core/EventBus.cs:25-29`) invoca a todos los suscriptores de un evento en el orden en que se llamó `Subscribe` — y ese orden depende, en última instancia, del orden en que se habilitan (`OnEnable`) los `MonoBehaviour` en la escena, algo que **Unity NO garantiza de forma determinística entre GameObjects distintos sin un `[DefaultExecutionOrder]` explícito** (ninguno de los tres componentes lo tiene hoy). Si en algún momento — por el orden real de carga de la escena, o tras cualquier refactor futuro que reordene cuándo se instancian `KillFeedbackDirector`/`BattleManager`/`GameOutcomeController` — `BattleManager.OnEntityDied` corre ANTES que `KillFeedbackDirector.OnDied` en la misma baja final: `ShowVictory()` deja `Time.timeScale = 0`, y milisegundos después (mismo `Publish`, mismo frame) `SlowMotionRoutine()` lo PISA a `0.25`. Pasados 0.9s reales (`WaitForSecondsRealtime`, que sigue corriendo aunque `timeScale` esté congelado), `EndSlowMotion()` chequea `Mathf.Approximately(Time.timeScale, SlowMotionScale)` — que en este escenario da `true`, porque nadie volvió a tocarlo después del pisado — y lo resetea a `1f` (línea 217). El resultado: la partida se reanuda a velocidad normal, con la IA, los proyectiles y el motor de vehículos corriendo de nuevo, detrás de un panel de "GANASTE" que sigue en pantalla y que el jugador cree que efectivamente congeló el juego.

**Plan de implementación:**
1. Darle a `KillFeedbackDirector` una referencia a `GameOutcomeController`, con el mismo patrón que ya usa para `PlayerBrain` (campo público `Brain`, cableado en `HeadlessTestRunner.cs:672-674`):
   ```csharp
   public SP.Presentation.GameOutcomeController Outcome;
   ```
2. En `TrySlowMotionOnLastKill()` (línea 174-179), agregar un corte temprano ANTES de arrancar la corrutina, para el caso "Victoria/Derrota ya se está mostrando o se muestra en este mismo instante":
   ```csharp
   void TrySlowMotionOnLastKill()
   {
       if (SlowMotionActive) return;
       if (Outcome != null && Outcome.IsShowing) return; // la pantalla de fin ya fijo timeScale=0; no lo pise
       if (ActorRegistry.CountAlive(TeamId.Enemy) > 0) return;
       if (Application.isPlaying) StartCoroutine(SlowMotionRoutine());
   }
   ```
3. Cubrir TAMBIÉN el orden inverso (slow-mo arranca primero, victoria corre después en el mismo `Publish` y pisa a 0 — hoy esto no revienta porque `EndSlowMotion` sólo actúa si `Time.timeScale` sigue siendo `0.25`, pero es frágil): en `EndSlowMotion()` (línea 213-218), agregar el mismo chequeo defensivo:
   ```csharp
   void EndSlowMotion()
   {
       if (!SlowMotionActive) return;
       SlowMotionActive = false;
       if (Outcome != null && Outcome.IsShowing) return; // no reanudar detras de la pantalla de fin
       if (Mathf.Approximately(Time.timeScale, SlowMotionScale)) Time.timeScale = 1f;
   }
   ```
4. Cablear el nuevo campo en `HeadlessTestRunner.cs`, junto a las demás asignaciones de `killDirector` (línea 672-678):
   ```csharp
   killDirector.Outcome = outcomeControllerRef;
   ```
5. (Refuerzo opcional pero recomendado dado que el bug depende de un orden no garantizado) Fijar el orden explícitamente con `[DefaultExecutionOrder]` para que `BattleManager`/`GameOutcomeController` NUNCA corran después de `KillFeedbackDirector` en el mismo evento, en vez de depender sólo de los guards reactivos de los puntos 2-3 (que son la defensa real, pero fijar el orden documenta la intención y evita que un futuro cambio reintroduzca el mismo tipo de carrera en otro par de sistemas). Ver Bug 13 para la discusión completa de `[DefaultExecutionOrder]` en este proyecto.

**Verificación:** Agregar un `Check()` nuevo, idealmente en `RunPhase6` (que ya maneja combate contra el pool de enemigos) o al final de `RunPhase7`: simular la muerte del último enemigo con `Outcome.ShowVictory()` llamado a mano ANTES de publicar el `EntityDiedEvent` final (fuerza el orden "peor caso" descrito arriba), luego publicar el evento (o invocar `killDirector` con el `EntityDiedEvent` directo vía reflection sobre `OnDied`), esperar/simular los 0.9s reales de `SlowMotionSeconds` (en Edit mode esto requiere invocar `SlowMotionRoutine`/`EndSlowMotion` de forma manual ya que no hay `Application.isPlaying`; documentar como prueba de Play mode si la corrutina no es practicable en Edit mode), y confirmar `Check("Time.timeScale sigue en 0 detras de la pantalla de victoria", Time.timeScale == 0f)`. Como prueba de Play mode complementaria: forzar manualmente (con un breakpoint o reordenando temporalmente el orden de `AddComponent` en `HeadlessTestRunner`) el escenario "BattleManager antes que KillFeedbackDirector", jugar hasta la última baja, y confirmar a ojo que el juego NO se mueve detrás del panel de victoria.

**Riesgo/efectos secundarios:** Confirmar que `GameOutcomeController.IsShowing` (ya pública, línea 47) refleja el estado correcto en el instante exacto en que se evalúa (es un simple `bool shown`, sin condición de carrera adicional ya que todo esto corre en el hilo principal de Unity de forma síncrona). Verificar que el guard del punto 3 no rompe el camino normal (sin victoria en curso) de `EndSlowMotion`, que debe seguir funcionando exactamente igual que antes. Revisar si existe un guard equivalente necesario para `ShowDefeat()` (la derrota la dispara `PlayerInputDriver.DeathSequence`, no `BattleManager` — ver Bug 12, que es el mismo tipo de carrera pero contra la MUERTE del jugador en vez de la última baja enemiga).

---

### Bug 10: `BattleManager` puede leer `enemyKills` un paso antes de que se incremente

**Archivos:** `Presentation/BattleManager.cs:27,30-41` + `Presentation/GameOutcomeController.cs:34-40,86,146-157`.

**Causa raíz:** Igual que el Bug 11, es un problema de orden de suscripción no garantizado sobre el mismo `EntityDiedEvent`. `GameOutcomeController.TrackDeaths` (línea 34-40) incrementa `enemyKills`/`squadLosses`; `BattleManager.OnEntityDied` (línea 30-41) revisa si ya no quedan enemigos vivos y, si es así, llama `Outcome.ShowVictory()`, que arma el texto de estadísticas con `BuildStatsText()` (línea 110-116) usando esos mismos contadores. Si en la baja FINAL el suscriptor de `BattleManager` corre ANTES que el de `GameOutcomeController` (nada en el código lo impide: ambos se suscriben en su propio `OnEnable`, sin ningún `[DefaultExecutionOrder]` ni coordinación explícita), `ShowVictory()` construye el texto de la pantalla ANTES de que `enemyKills` se haya incrementado por esa última baja — la pantalla de victoria muestra un enemigo menos de los que en realidad murieron.

**Plan de implementación:**
1. La forma más simple y robusta de eliminar la dependencia de orden es que `GameOutcomeController` no dependa de "haberse enterado a tiempo" vía evento: como `ShowVictory()`/`ShowDefeat()` ya calculan las estadísticas en el momento exacto en que se llaman (no antes), el conteo de bajas puede calcularse ahí mismo contra la fuente de verdad — el registro de actores — en vez de acumularse de forma incremental y frágil por evento. Cambiar `BuildStatsText()`:
   ```csharp
   string BuildStatsText()
   {
       float elapsed = Time.time - startTime;
       int minutes = Mathf.FloorToInt(elapsed / 60f);
       int seconds = Mathf.FloorToInt(elapsed % 60f);
       int enemyDead = SP.Core.ActorRegistry.CountDead(TeamId.Enemy); // ver punto 2
       int squadDead = SP.Core.ActorRegistry.CountDead(TeamId.Player);
       return $"Bajas enemigas: {enemyDead}   ·   Bajas propias: {squadDead}   ·   Tiempo: {minutes:00}:{seconds:00}";
   }
   ```
2. Revisar `Core/ActorRegistry.cs`: si ya existe `CountAlive(TeamId)` (usado en `BattleManager.cs:39-40`), agregar el complemento `CountDead(TeamId)` (total registrado del equipo menos `CountAlive`, o iterando `All` y contando `!s.Health.IsAlive`) siguiendo la misma convención de la clase.
3. Si por algún motivo de diseño se prefiere MANTENER el conteo incremental por evento (por ejemplo si `ActorRegistry` no retiene a los soldados muertos y no puede reconstruirse el total), la alternativa mínima es forzar el orden con `[DefaultExecutionOrder]` en `GameOutcomeController` (más negativo, o sea que se ejecuta antes) respecto de `BattleManager` — ver Bug 13 para la discusión de esta técnica en el proyecto — pero esto sólo tapa el síntoma para ESTE par de componentes y no es la solución preferida frente a la opción 1, que elimina la dependencia de orden por completo.

**Verificación:** Agregar un `Check()` en la fase de combate que ya lleva enemigos a 0 vida (revisar qué fase remata al último enemigo del pool — probablemente `RunPhase6`): antes de la baja final, leer `enemyKills`/el resultado de `BuildStatsText()` por reflection; forzar (para testear el peor caso) que `BattleManager.OnEntityDied` corra antes que `GameOutcomeController.TrackDeaths` invocando ambos manualmente en ese orden con el mismo `EntityDiedEvent`; confirmar que el texto final (`victoryStats.text` o el valor de `BuildStatsText()` invocado por reflection) refleja el conteo CORRECTO de bajas incluyendo la última.

**Riesgo/efectos secundarios:** Si se opta por la solución del punto 1-2 (contar desde `ActorRegistry` en el momento de mostrar la pantalla), confirmar que `ActorRegistry` efectivamversuselos actores muertos disponibles para contarlos (no los remueve del registro al morir) — si los remueve, hay que ajustar el enfoque (por ejemplo, llevar un contador total de altas por equipo al registrar, y restar los vivos). Verificar también que el tiempo transcurrido (`elapsed`) sigue siendo consistente y no se ve afectado por este cambio.

---

### Bug 12: la muerte del propio jugador puede colgar `DeathSequence` para siempre si la Victoria llega en el medio

**Archivos:** `Player/PlayerInputDriver.cs:479-590` (`DeathSequence`) + `Presentation/GameOutcomeController.ShowVictory()`.

**Causa raíz:** El bucle de órbita de `DeathSequence` (línea 543-554) avanza con `Time.deltaTime` (escalado, no `unscaledDeltaTime`):
```csharp
float t = 0f;
while (t < holdSeconds)
{
    t += Time.deltaTime;
    ...
    yield return null;
}
```
Si, mientras el soldado poseído está muerto y su propia cámara de muerte está orbitando, un aliado remata al último enemigo del mapa y `BattleManager` llama `Outcome.ShowVictory()` (que hace `Time.timeScale = 0f`), `Time.deltaTime` pasa a valer `0` en todos los frames siguientes. `t` deja de avanzar, el `while (t < holdSeconds)` nunca se cumple, y la corrutina queda "viva" pero completamente detenida — literalmente cuelga (no una excepción, sino un bucle que nunca progresa) mientras el juego entero está pausado por la pantalla de victoria. Como el propio comentario del código ya documenta (línea 482-497), el `try/finally` existe justamente para este escenario, PERO sólo limpia (`CleanupDeathSequence`, que resetea `handlingDeath` a `false` y destruye `deathKillerRing`/`deathPullBackGO`) cuando la corrutina TERMINA — y si nunca termina (porque `t` nunca avanza), el `finally` nunca corre. `handlingDeath` se queda en `true` para siempre, lo que bloquea cualquier `DeathSequence` futura (`OnEntityDied` línea 458: `if (handlingDeath) return;`) y dejaría a `PauseController` creyendo indefinidamente que sigue la cámara de muerte (`IsHandlingDeath`).

**Plan de implementación:**
1. Cambiar el acumulador del bucle de órbita a tiempo real, con el mismo criterio que ya usa el proyecto en casos similares (`KillFeedbackDirector.SlowMotionRoutine` usa `WaitForSecondsRealtime`; `OrderHistory.Record` usa `Time.unscaledTime` explícitamente "porque no debería estirarse con la cámara lenta ni congelarse en pausa" — el mismo razonamiento aplica acá, LA CÁMARA DE MUERTE NO DEBERÍA PODER QUEDAR CONGELADA POR UN `timeScale` AJENO):
   ```csharp
   float t = 0f;
   while (t < holdSeconds)
   {
       t += Time.unscaledDeltaTime;
       angle += orbitDegPerSec * Time.unscaledDeltaTime;
       ...
       yield return null;
   }
   ```
2. Revisar también el primer tramo de la corrutina, `while (Rig.IsTransitioning) yield return null;` (línea 530) — si `CameraRig.BeginTransition`/`IsTransitioning` internamente usa `Time.deltaTime` escalado para lerpear, tiene la MISMA vulnerabilidad (quedaría transicionando para siempre con `timeScale=0`). Esto excede el archivo de este bug puntual, pero debe verificarse: si `CameraRig` usa tiempo escalado, o se agrega ahí también una variante `unscaled`, o se documenta como riesgo conocido fuera de alcance.
3. Como defensa adicional independiente del punto 1 (en caso de que existan OTROS caminos dentro de `DeathSequence` que también dependan de tiempo escalado y no se hayan detectado), agregar un timeout de seguridad explícito basado en tiempo real que fuerce la salida del bucle si por cualquier motivo tarda demasiado — aunque con el fix del punto 1 esto debería ser innecesario, es una red de seguridad barata:
   ```csharp
   float realStart = Time.realtimeSinceStartup;
   while (t < holdSeconds && Time.realtimeSinceStartup - realStart < holdSeconds + 2f)
   { ... }
   ```
   (Opcional — evaluar si agrega complejidad injustificada una vez aplicado el punto 1; el punto 1 solo debería ser suficiente.)

**Verificación:** Agregar una prueba en `RunPhase6`/`RunPhase7`: matar al soldado poseído (publicando `EntityDiedEvent` para que arranque `DeathSequence`), simular unos pasos de la corrutina (en Edit mode esto requiere driving manual del `IEnumerator` con `MoveNext()`, ya que `StartCoroutine` real necesita Play mode — revisar si `HeadlessTestRunner` ya tiene un patrón para avanzar corrutinas a mano; si no, documentar como prueba de Play mode), forzar `Time.timeScale = 0f` a mitad del bucle de órbita (simulando que llegó la Victoria), simular varios frames más, y confirmar que `t` (por reflection) SIGUE avanzando pese a `timeScale=0` porque ahora usa `Time.unscaledDeltaTime`. Como prueba de Play mode: reproducir el escenario real (un aliado remata al último enemigo justo cuando el jugador acaba de morir) y confirmar que la cámara de muerte termina su órbita de 3 segundos con normalidad y `handlingDeath` vuelve a `false`.

**Riesgo/efectos secundarios:** Verificar que el resto de `DeathSequence` (el tramo de `Rig.BeginTransition`, la duración de 0.9s de la transición inicial) sea coherente con este cambio — si esa parte sigue en tiempo escalado y ésta pasa a tiempo real, la sensación de "peso" de las dos partes de la secuencia podría notarse distinta cuando NO hay `timeScale` alterado (en el caso normal, `Time.deltaTime ≈ Time.unscaledDeltaTime`, así que no debería notarse ninguna diferencia salvo quePausa/lentitud de por medio). Confirmar que `orbitDegPerSec * Time.unscaledDeltaTime` sigue dando la misma velocidad angular percibida en el caso normal (sin `timeScale` alterado, ambos deltas son iguales, así que no hay cambio de comportamiento fuera del escenario del bug).

---

### Bug 13: `Projectile.Update()` vs. `WorldSimulationDriver` corren en un orden no coordinado

**Archivos:** `Combat/Projectile.cs:146` (`Update()`) vs. `Ai/WorldSimulationDriver.cs:64-84` (`Step`, orden manual de ticks).

**Causa raíz:** `WorldSimulationDriver.Step(dt)` tickea, en orden fijo y documentado, `SpatialGrid.Rebuild()` → soldados (`Brain`/`Weapon`) → `VehicleBrain` → `TurretWeapon` → `TurretAI`. Los proyectiles NO forman parte de ese `Step`: cada `Projectile` se mueve solo, vía su propio `void Update() => Tick(Time.deltaTime);` (línea 146), que Unity llama en un orden relativo a `WorldSimulationDriver.Update()` que **no está fijado por ningún `[DefaultExecutionOrder]`** — es el orden interno (no documentado ni garantizado por Unity) en que el motor decide llamar `Update()` sobre distintos componentes de distintos GameObjects. El propio comentario de `HeadlessTestRunner.cs:1702-1708` ya reconoce esto explícitamente: en la suite headless, `SimStep` tickea `WorldSimulationDriver.Step(dt)` y LUEGO, en un paso aparte, itera `Projectile.ActiveInstances` — y el comentario aclara que esa es "la misma falta de orden garantizado que ya existe" en Play mode real, no algo que la suite haya inventado. El riesgo concreto: si un soldado se mueve (parte del `Step`) y un proyectil viaja (su propio `Update`) en el MISMO frame, dependiendo de qué corra primero ese frame, el proyectil puede evaluar el impacto contra la posición VIEJA o la NUEVA del soldado — una inconsistencia de un frame que en general es imperceptible a 60fps pero puede producir "impactos fantasma" (el proyectil pasa por donde el soldado YA NO ESTÁ, o pega donde el soldado TODAVÍA NO LLEGÓ) en combates con alta velocidad relativa (vehículos, o balas del tanque a doble velocidad).

**Plan de implementación (fix PRÁCTICO, no "perfecto" — se acepta explícitamente el alcance acotado, siguiendo el propio criterio del comentario de `HeadlessTestRunner.cs:1702-1708`):**
1. Fijar el orden relativo entre `WorldSimulationDriver` y `Projectile` con `[DefaultExecutionOrder]`, que es el mecanismo estándar de Unity para esto y no requiere tocar la arquitectura de ticking manual vs. `Update()` que ya coexiste en el proyecto:
   ```csharp
   // En Ai/WorldSimulationDriver.cs
   [DefaultExecutionOrder(-100)]
   public class WorldSimulationDriver : MonoBehaviour { ... }
   ```
   ```csharp
   // En Combat/Projectile.cs
   [DefaultExecutionOrder(-50)]
   public class Projectile : MonoBehaviour, IPoolable { ... }
   ```
   Con esto, `WorldSimulationDriver.Update()` (que mueve soldados/vehículos/torretas) corre SIEMPRE antes que cualquier `Projectile.Update()` en el mismo frame — así el proyectil siempre evalúa impactos contra la posición ya actualizada del frame actual, coherente con "todo el mundo se movió, ahora la bala revisa qué tocó", en vez de un orden arbitrario.
2. Documentar en un comentario, junto al atributo de `Projectile.cs`, el motivo (mismo tono que el resto del proyecto usa para explicar decisiones de este tipo):
   ```csharp
   // [DefaultExecutionOrder(-50)]: los proyectiles tienen que evaluar su
   // impacto DESPUES de que WorldSimulationDriver mueva a todo el mundo en
   // este mismo frame (-100), no antes -- si no, el orden relativo entre
   // "el soldado se movio" y "la bala revisa que toco" quedaba librado al
   // orden interno no documentado de Unity. No es una sincronizacion
   // perfecta (segun HeadlessTestRunner.cs, SimStep ya lo señala): esto
   // NO resuelve, por ejemplo, que un proyectil disparado y uno que
   // impacta en el MISMO frame se ordenen entre si de forma predecible
   // frente a otros proyectiles -- pero fija lo que mas importa: bala
   // despues de movimiento, siempre.
   ```
3. Actualizar el comentario de `HeadlessTestRunner.cs:1702-1708` para reflejar que ahora SÍ hay un orden fijado (aunque acotado) entre proyectiles y el resto de la simulación, en vez de dejar el comentario viejo afirmando que "no hay ningún orden garantizado" cuando eso ya no sería del todo cierto tras el punto 1.

**Verificación:** No es viable un `Check()` determinístico en la suite headless para esto, porque `SimStep` ya simula ambos caminos por separado y en un orden fijo propio (`WorldSimulationDriver.Step` primero, proyectiles después) — la suite headless ya "hace lo correcto" manualmente, independientemente de lo que Unity decida en Play mode real. La verificación real es indirecta: confirmar en el Inspector de Unity (Edit > Project Settings > Script Execution Order, o simplemente los atributos en código) que `WorldSimulationDriver` tiene un número menor que `Projectile`, y como prueba de Play mode, generar un escenario de disparo a alta velocidad relativa (vehículo moviéndose rápido cruzando la trayectoria de una bala) y confirmar visualmente que el punto de impacto es consistente frame a frame (sin "dientes de sierra" en la posición de impacto al variar el framerate).

**Riesgo/efectos secundarios:** `[DefaultExecutionOrder]` es un atributo de bajo riesgo (no cambia lógica, sólo el orden de invocación de `Update`/`OnEnable`/etc.), pero hay que revisar si algún OTRO componente del proyecto ya depende implícitamente del orden actual (no fijado) entre estos dos sistemas de una forma que este cambio podría romper — en particular, cualquier código que asuma que un proyectil se tickea ANTES que el movimiento del frame (poco probable dado que no hay ningún `[DefaultExecutionOrder]` hoy en el proyecto, así que no hay ninguna dependencia consciente de un orden específico, pero vale confirmarlo con una búsqueda de otros `Update()` que interactúen con `Projectile.ActiveInstances` o con el movimiento de soldados/vehículos en el mismo frame).

---

### Bug 14: un vehículo destruido con el jugador a bordo deja `PlayerAboard`/`IsPlayerDriving` atascados en `true`

**Archivos:** `Player/PlayerInputDriver.cs:1335-1342` (rama `Vehicle.IsDestroyed` de `UpdateInVehicle`) vs. `Vehicles/Vehicle.cs:177` (`PlayerAboard`) + `Vehicles/VehicleBrain.cs:22,55` (`IsPlayerDriving`).

**Causa raíz:**
```csharp
if (Vehicle.IsDestroyed)
{
    currentSeat = null;
    if (VehicleStatus != null) VehicleStatus.gameObject.SetActive(false);
    if (TurretAim != null) TurretAim.SetVisible(false);
    Rig.FollowFps(Brain.Current);
    return;
}
```
Esta rama resetea `currentSeat` pero NUNCA toca `Vehicle.PlayerAboard` ni `vb.IsPlayerDriving` — a diferencia de `ExitVehicle()` (línea 1293-1308), que SÍ resetea ambos (`vb.IsPlayerDriving = false;` línea 1298, `Vehicle.PlayerAboard = false;` línea 1299) además de `currentSeat = null`. `Vehicle.OnDestroyed()` (`Vehicle.cs:83-109`) expulsa físicamente a todos los ocupantes (`Dismount`, línea 88) y apaga motor/torreta/IA, pero como `Vehicle` no conoce a `PlayerInputDriver`, no tiene ninguna forma de avisarle "el jugador ya no está a bordo" — `PlayerAboard` es un campo que sólo pone/saca `PlayerInputDriver` (comentario de `Vehicle.cs:170-176`: "Lo pone/saca PlayerInputDriver al entrar/salir de un asiento"). Resultado: si el vehículo se destruye estando el jugador adentro (en vez de que el jugador se baje con `[E]`/`[X]`, el único camino que sí pasa por `ExitVehicle()`), `Vehicle.PlayerAboard` y `vb.IsPlayerDriving` quedan en `true` para siempre sobre un vehículo que ya ni siquiera tiene motor habilitado — cualquier sistema que consulte esos flags (la vibración del cañón mencionada en el comentario de `PlayerAboard`, o `VehicleBrain.Tick` que ya de por sí corta por `vehicle.IsDestroyed` en la línea 55 pero seguiría reportando `IsPlayerDriving == true` a quien lo consulte desde afuera) queda leyendo un estado mentiroso.

**Plan de implementación:**
1. Reusar el helper `ClearVehicleSeatState()` propuesto en el Bug 3 (que ya resetea `currentSeat`, `Vehicle.PlayerAboard` y `vb.IsPlayerDriving` en un solo lugar) en vez de resetear sólo `currentSeat` a mano en esta rama:
   ```csharp
   if (Vehicle.IsDestroyed)
   {
       ClearVehicleSeatState();
       if (VehicleStatus != null) VehicleStatus.gameObject.SetActive(false);
       if (TurretAim != null) TurretAim.SetVisible(false);
       Rig.FollowFps(Brain.Current);
       return;
   }
   ```
   Esto automáticamente cubre este bug SIEMPRE que el Bug 3 se implemente primero con el helper compartido tal como está diseñado (evaluar el orden de aplicación de ambos fixes juntos, ya que comparten la misma solución de raíz).
2. Si por algún motivo se prefiere no compartir el helper con el Bug 3 (implementación independiente), replicar el mismo patrón acá directamente:
   ```csharp
   if (Vehicle.IsDestroyed)
   {
       Vehicle.PlayerAboard = false;
       var vbDestroyed = Vehicle.GetComponent<VehicleBrain>();
       if (vbDestroyed != null && currentSeat == VehicleSeatRole.Driver) vbDestroyed.IsPlayerDriving = false;
       currentSeat = null;
       ...
   }
   ```

**Verificación:** Agregar un `Check()` en la fase que ya destruye el vehículo (revisar `RunPhase5`/`RunPhase6` por el patrón de destrucción de vehículo): montar a Vega como conductor (`EnterVehicle`/`EnterPossessedVehicleSeat` para que `PlayerAboard=true` y `vb.IsPlayerDriving=true` de verdad), destruir el vehículo con daño masivo (`vehicle.TakeDamage(9999, -1)`), simular el frame de `UpdateInVehicle` que detecta `IsDestroyed` (invocar el método privado por reflection, con el mismo patrón que usa `RunPhase7` para otros privados), y verificar `Check("PlayerAboard se limpia al destruirse el vehiculo con el jugador adentro", !vehicle.PlayerAboard)` y `Check("IsPlayerDriving se limpia al destruirse el vehiculo con el jugador adentro", !vehicle.GetComponent<VehicleBrain>().IsPlayerDriving)`.

**Riesgo/efectos secundarios:** Confirmar que este cambio no interfiere con el `Invoke(nameof(FinalExplosion), AgonySeconds)` de `Vehicle.OnDestroyed()` (línea 107), que corre en un momento posterior e independiente — el reseteo de `PlayerAboard`/`IsPlayerDriving` es puramente del lado de `PlayerInputDriver` y no debería interactuar con la secuencia de agonía/explosión del vehículo. Verificar que ningún sistema de presentación (vibración del cañón u otro) dependa de que `PlayerAboard` siga en `true` DURANTE el breve intervalo entre la destrucción y el próximo frame de `UpdateInVehicle` (debería ser inmediato, sin ventana perceptible).

---

### Bug 15: `KeyBindings.Set()` no detecta conflictos de tecla duplicada

**Archivos:** `Player/KeyBindings.cs:93-98` (`Set`).

**Causa raíz:**
```csharp
public static void Set(string actionId, Key key)
{
    EnsureLoaded();
    current[actionId] = key;
    PlayerPrefs.SetInt(PrefKey(actionId), (int)key);
}
```
No hay ninguna verificación de si `key` ya está en uso por otra acción. Hoy el ÚNICO llamador de `Set()` es `KeyRebindView.AssignKey` (`UI/KeyRebindView.cs:95-111`), que SÍ implementa manualmente su propia lógica de "liberar la otra acción que use esa tecla" (líneas 100-106: recorre `ActionIds`, y si otra acción ya tiene esa `key`, la pone en `Key.None`) — pero esa protección vive enteramente en la capa de UI, no en la API. Cualquier otro futuro llamador de `KeyBindings.Set()` (un importador de perfil de configuración, una herramienta de debug, un test que llame `Set` directo) queda completamente desprotegido: podría dejar DOS acciones distintas apuntando a la misma tecla física, y ambas dispararían en el mismo apretón sin ningún aviso — que es exactamente lo que hoy la UI evita a fuerza de duplicar lógica en el lugar equivocado. Nota de diseño importante: el proyecto YA reutiliza deliberadamente teclas físicas entre acciones que viven en contextos mutuamente excluyentes (por ejemplo, tanto `SubirBajarVehiculo` como `CancelarOrden` tienen `Key.X` como default — ver `KeyBindings.cs:54,64` — porque una es de a pie/FPS y la otra es de RTS). Cualquier fix de conflicto tiene que actuar sólo sobre llamadas EXPLÍCITAS a `Set()` (rebinds reales del jugador), nunca sobre la carga de los defaults (`EnsureLoaded`, que nunca pasa por `Set()`), para no romper esa convención ya existente.

**Plan de implementación:**
1. Mover la lógica de "liberar la acción conflictiva" desde `KeyRebindView.AssignKey` hacia `KeyBindings.Set()`, para que sea la ÚNICA fuente de verdad, y devolver el id de la acción liberada (si hubo alguna) para que el llamador pueda avisar al jugador:
   ```csharp
   // Devuelve el actionId que quedo sin tecla asignada por el conflicto,
   // o null si no habia ninguno. Sin esto, la proteccion contra dos
   // acciones en la misma tecla vivia SOLO en KeyRebindView -- cualquier
   // otro futuro llamador de Set() quedaba desprotegido.
   public static string Set(string actionId, Key key)
   {
       EnsureLoaded();
       string freedAction = null;
       if (key != Key.None)
       {
           foreach (var other in new List<string>(current.Keys))
           {
               if (other == actionId) continue;
               if (current[other] == key)
               {
                   current[other] = Key.None;
                   PlayerPrefs.SetInt(PrefKey(other), (int)Key.None);
                   freedAction = other;
                   break; // por diseño, en cualquier momento a lo sumo una accion tiene cada tecla
               }
           }
       }
       current[actionId] = key;
       PlayerPrefs.SetInt(PrefKey(actionId), (int)key);
       return freedAction;
   }
   ```
2. Actualizar `KeyRebindView.AssignKey` (`UI/KeyRebindView.cs:95-111`) para dejar de duplicar la lógica y, en cambio, mostrar un aviso cuando `Set` devuelva una acción liberada:
   ```csharp
   void AssignKey(Key key)
   {
       if (listeningRow < 0 || ActionIds == null) return;
       string action = ActionIds[listeningRow];

       string freed = KeyBindings.Set(action, key);
       if (freed != null)
           GameLog.Line($"{NameOf(freed)} quedo sin tecla asignada (la tomo {NameOf(action)})");

       listeningRow = -1;
       RefreshAll();
   }
   ```
3. Dado que `Set()` cambia de `void` a `string`, revisar TODOS los llamadores existentes (`KeyRebindView.cs`, y cualquier otro uso en `HeadlessTestRunner.cs` como el de la línea 1258 `SP.Player.KeyBindings.Set(SP.Player.KeyBindings.Recargar, UnityEngine.InputSystem.Key.U);`) para confirmar que ninguno depende de la firma `void` de forma incompatible (en C#, ignorar el valor de retorno de una llamada es válido sin cambios adicionales, así que esto no debería romper nada, pero vale la revisión).

**Verificación:** Agregar un `Check()` en la sección de `KeyBindings` de `HeadlessTestRunner.cs` (cerca de la línea 1256-1262, que ya prueba `Set`/`ResetToDefaults`/persistencia): `KeyBindings.ResetToDefaults()`; `KeyBindings.Set(KeyBindings.Poseer, Key.R)` (choca con `Recargar`, que por default es R); confirmar que `KeyBindings.Get(KeyBindings.Recargar) == Key.None` tras esa llamada (`Check("Set() libera la accion conflictiva en vez de dejar dos en la misma tecla", KeyBindings.Get(KeyBindings.Recargar) == Key.None)`); confirmar que el valor de retorno de `Set` es `KeyBindings.Recargar`; y `KeyBindings.ResetToDefaults()` al final para no contaminar el resto de la suite.

**Riesgo/efectos secundarios:** Confirmar que este cambio NO se dispara jamás durante `EnsureLoaded()` (la carga inicial desde `PlayerPrefs`), ya que esa ruta nunca llama a `Set()` — sigue escribiendo `current[id] = (Key)saved` directo (línea 81), así que el default duplicado en X (`SubirBajarVehiculo`/`CancelarOrden`) sigue funcionando sin que el nuevo guard lo toque. Verificar que un jugador que tenga guardado en `PlayerPrefs`, de una partida vieja (antes del fix), dos acciones en la misma tecla por algún bug previo, no quede en un estado raro al cargar — el nuevo guard sólo actúa hacia adelante (en la próxima vez que se llame `Set` para cualquiera de esas dos acciones), no migra datos viejos; si se quiere sanear datos viejos, se puede llamar `Set(action, Get(action))` para cada acción durante `EnsureLoaded()`, pero esto es opcional y fuera del alcance mínimo del bug reportado.

---

### Bug 16: los rebinds de teclas se pierden en un cierre anormal — `PlayerPrefs.Save()` nunca se llama

**Archivos:** `Player/KeyBindings.cs` (`Set`, `ResetToDefaults`).

**Causa raíz:** `KeyBindings.Set()` (línea 93-98) y `ResetToDefaults()` (línea 100-108) llaman `PlayerPrefs.SetInt`/`PlayerPrefs.DeleteKey`, que escriben en la caché en memoria de `PlayerPrefs` pero NO garantizan persistencia en disco de forma inmediata — según la documentación de Unity, los cambios se escriben a disco automáticamente cuando la aplicación se cierra de forma NORMAL, pero un cierre anormal (crash, `Alt+F4`/cerrar la ventana a la fuerza, corte de energía, kill del proceso) puede perder cualquier cambio no confirmado explícitamente con `PlayerPrefs.Save()`. Ninguno de los dos métodos de `KeyBindings.cs` llama `Save()`.

**Nota de alcance más amplio (importante):** esto NO es un problema exclusivo de `KeyBindings.cs`. Un relevamiento de todo `Assets/_Project/Scripts` confirma **cero** llamadas a `PlayerPrefs.Save()` en todo el proyecto, pese a que hay al menos 7 sitios que escriben con `PlayerPrefs.SetInt`/`SetFloat`: `Camera/CameraFxSettings.cs:36`, `Player/KeyBindings.cs:97`, `Player/PlayerInputDriver.cs:378` (`sp_used_tab`), `Presentation/AudioDirector.cs:94`, `Presentation/GameplaySceneBootstrap.cs:49` (`sp_first_action_shown`), y cuatro sitios en `Presentation/PauseController.cs:115,131,147,168,184` (volumen, sensibilidad, sensibilidad de torreta, escala de HUD, escala de mira, invertir Y). Es decir: TODA la configuración persistente del juego (no sólo los rebinds) corre el mismo riesgo de perderse en un cierre anormal. Este bug puntual está acotado a `KeyBindings.cs` porque es el archivo en cuestión, pero la solución correcta y de menor esfuerzo es centralizada, no repetida siete veces.

**Plan de implementación:**
1. Agregar `PlayerPrefs.Save()` al final de `KeyBindings.Set()` y `KeyBindings.ResetToDefaults()`:
   ```csharp
   public static void Set(string actionId, Key key)
   {
       EnsureLoaded();
       current[actionId] = key;
       PlayerPrefs.SetInt(PrefKey(actionId), (int)key);
       PlayerPrefs.Save();
   }

   public static void ResetToDefaults()
   {
       EnsureLoaded();
       foreach (var kv in defaults)
       {
           current[kv.Key] = kv.Value;
           PlayerPrefs.DeleteKey(PrefKey(kv.Key));
       }
       PlayerPrefs.Save();
   }
   ```
   (Si se aplica también el Bug 15, que hace que `Set` pueda escribir DOS entradas — la propia y la liberada por conflicto — basta con UN solo `Save()` al final de `Set`, después de ambas escrituras, ya que `Save()` vuelca toda la caché pendiente, no sólo la última clave tocada.)
2. Dado que llamar `PlayerPrefs.Save()` en CADA tecleo tiene un costo de I/O menor pero no nulo (esto no corre por frame, sólo al confirmar un rebind — un evento raro, así que el costo es aceptable sin necesidad de debounce).
3. Para el problema más amplio (los otros 6 sitios), dejarlo fuera del alcance de ESTE fix puntual pero señalado explícitamente para una tarea separada: la forma correcta de resolverlo sin duplicar `Save()` en cada sitio es un único punto central que llame `PlayerPrefs.Save()` al pausar/salir de la aplicación (`OnApplicationPause(bool)`/`OnApplicationQuit()` en algún `MonoBehaviour` de nivel de escena, por ejemplo `PauseController` o `GameplaySceneBootstrap`, que ya viven en la escena de juego) — eso cubre los 7 sitios de una vez sin que cada uno tenga que acordarse de llamar `Save()` por separado. Documentar esto como ítem de seguimiento (fuera del scope de los 18 bugs de esta tanda) en vez de implementarlo acá, ya que tocar `PauseController`/`GameplaySceneBootstrap` no está en la lista de archivos de esta tanda.

**Verificación:** No es directamente verificable en la suite headless (que corre en Edit mode, sin un ciclo de vida de aplicación real que dispare pérdida de datos). La verificación práctica es: confirmar por inspección de código que `Set()`/`ResetToDefaults()` llaman `PlayerPrefs.Save()` (un `Check()` liviano en `HeadlessTestRunner.cs` no puede probar persistencia real ante un crash, pero SÍ puede confirmar que el valor sigue en `PlayerPrefs` tras `InvalidateCache()` + relectura, que es lo que YA prueba el test existente de la línea 1256-1262 — ese test seguiría pasando igual, con o sin `Save()`, porque `PlayerPrefs` en memoria ya refleja el cambio antes de que el proceso termine). Como prueba manual real: rebindear una tecla en el juego, forzar el cierre del proceso Unity/build desde el Administrador de Tareas (sin cerrar la ventana normalmente), reabrir el juego, y confirmar que el rebind sigue aplicado.

**Riesgo/efectos secundarios:** `PlayerPrefs.Save()` es una operación de bajo riesgo y ya estándar de Unity; no debería tener efectos secundarios funcionales. Si en el futuro se implementa el punto 3 (guardado centralizado en `OnApplicationQuit`), revisar que no se dupliquen escrituras innecesarias (`Save()` llamado tanto en cada `Set()` individual como en el cierre de la app) — no es incorrecto, sólo redundante; se puede dejar así (defensa en profundidad) o quitar los `Save()` puntuales una vez que exista el guardado centralizado, según se prefiera priorizar robustez o minimizar I/O.

---

### Bug 17: la formación elegida con `[K]` es ignorada por la orden principal de movimiento (tecla `[T]`/clic derecho)

**Archivos:** `Player/PlayerInputDriver.cs:1682` (llamada a `IssueMoveOrderForSelection`) y `~1870` (`UpdateFormationPreview`, vista previa fantasma) vs. `Player/OrderService.cs:240-245`.

**Causa raíz:** `OrderService.IssueMoveOrderForSelection(IEnumerable<Soldier> selection, Vector3 point, bool queued = false)` (línea 240-245) delega SIEMPRE en `IssueFormationOrderForSelection(selection, point, Vector3.forward, FormationKind.Cuadricula, queued)` — la formación está hardcodeada a `Cuadricula`, la firma de este overload ni siquiera RECIBE un `FormationKind` como parámetro. `PlayerInputDriver.UpdateRts` llama exactamente a este overload en el camino principal de "mover la selección" (tecla `[T]` o clic derecho sin arrastre, línea 1682: `OrderService.IssueMoveOrderForSelection(Selection.Selected, result.Point, queued);`), así que **la formación que el jugador cicló con `[K]` (`currentFormation`, campo de línea 39) nunca llega a esta llamada**, sin importar en qué formación esté parado. Lo mismo pasa con la vista previa fantasma en `UpdateFormationPreview` (línea ~1870: `var spots = OrderService.FormationPoints(result.Point, Selection.Selected.Count);`), que usa el overload de 2 argumentos (`FormationPoints(Vector3 center, int count)`, línea 56-59 de `OrderService.cs`), que también delega siempre en `Cuadricula` — así que ni siquiera la VISTA PREVIA mientras se mantiene el clic derecho refleja la formación elegida, doblemente engañoso. Sólo dos caminos SÍ respetan `currentFormation`: la orden por clic en el minimapa (línea 1759: `OrderService.IssueFormationOrderForSelection(Selection.Selected, destino, Vector3.forward, currentFormation);`) y `Reagrupar`/`[Z]` (línea 1770: `OrderService.RegroupSelection(Selection.Selected, currentFormation)`).

**Plan de implementación:**
1. En `OrderService.cs`, agregar un pequeño overload público de `FormationPoints` que exponga la formación/spacing reales sin obligar al llamador a conocer el valor interno de `FormationSpacing` (privado), siguiendo el mismo patrón que ya usa el overload histórico de 2 argumentos (línea 56-59):
   ```csharp
   // Igual que la sobrecarga de 2 argumentos (Cuadricula fija), pero
   // dejando elegir la formacion real -- para la vista previa fantasma
   // en RTS, que necesita mostrar la MISMA formacion que despues emite
   // la orden real.
   public static Vector3[] FormationPoints(Vector3 center, Vector3 forward, int count, FormationKind kind)
   {
       return FormationPoints(center, forward, count, kind, FormationSpacing);
   }
   ```
2. En `PlayerInputDriver.cs`, reemplazar la línea 1682:
   ```csharp
   else OrderService.IssueMoveOrderForSelection(Selection.Selected, result.Point, queued);
   ```
   por:
   ```csharp
   else OrderService.IssueFormationOrderForSelection(Selection.Selected, result.Point, Vector3.forward, currentFormation, queued);
   ```
   (Se pasa `Vector3.forward` como frente, igual que ya hace el camino del minimapa en la línea 1759 — no hay ningún vector de arrastre real capturado para esta orden puntual de clic/tecla, así que se mantiene el mismo criterio ya establecido en el proyecto en vez de inventar una captura de dirección nueva fuera de alcance de este bug.)
3. En `UpdateFormationPreview` (línea ~1870), reemplazar:
   ```csharp
   var spots = OrderService.FormationPoints(result.Point, Selection.Selected.Count);
   ```
   por:
   ```csharp
   var spots = OrderService.FormationPoints(result.Point, Vector3.forward, Selection.Selected.Count, currentFormation);
   ```
   usando el nuevo overload del punto 1.

**Verificación:** Agregar un `Check()` en `RunPhase3`/`RunPhase2` (donde ya se prueba selección y órdenes de movimiento): seleccionar a `kes` y `doc`, ciclar la formación con el método privado equivalente a `[K]` (o setear `currentFormation` directo por reflection sobre el campo privado, con el mismo patrón que `currentSeatField` de `RunPhase7`) a `FormationKind.Linea`, invocar el camino de orden principal (simular `[T]` o llamar directamente al bloque de código migrado, si hace falta por reflection sobre un método extraído), y comparar los destinos resultantes de cada `AiBrain.CurrentOrderDestination` contra `OrderService.FormationPoints(centro, Vector3.forward, 2, FormationKind.Linea)` calculado a mano — deberían coincidir, en vez de coincidir con el patrón de `Cuadricula`.

**Riesgo/efectos secundarios:** Confirmar que el cambio no afecta el comportamiento cuando `currentFormation == FormationKind.Cuadricula` (el default): como `Cuadricula` es simétrica respecto de `forward` (según el propio comentario de `RegroupSelection`, línea 396-399), el resultado debería ser IDÉNTICO al actual en el caso por defecto, y sólo cambiar visiblemente cuando el jugador haya ciclado activamente a `Linea`/`Cuna`/`Columna` — que es exactamente el comportamiento esperado. Revisar que `IssueFormationOrderForSelection` ya filtra correctamente entre soldados vivos si se aplica también el Bug 4 (ambos fixes tocan el mismo método y deberían aplicarse en conjunto sin conflicto, ya que uno cambia el FILTRADO de la lista de entrada y el otro cambia qué overload de `PlayerInputDriver` la invoca).

---

### Bug 18: `EnterVehicleViewFromRts` posee a un ocupante sin chequear si está vivo

**Archivos:** `Player/PlayerInputDriver.cs:1279-1291`.

**Causa raíz:**
```csharp
void EnterVehicleViewFromRts(Vehicle vehicle)
{
    if (vehicle == null || vehicle.OccupantCount == 0) return;
    var occupant = vehicle.Driver ?? vehicle.Occupants[0];
    if (occupant == null) return;

    PossessionService.Swap(Brain, occupant);
    var role = vehicle.RoleOf(occupant);
    if (role == null) return;

    Rig.SetMode(ControlMode.Fps);
    EnterPossessedVehicleSeat(role.Value);
}
```
A diferencia de `TryPossess(Soldier target)` (línea 993-1053), que explícitamente rechaza a un objetivo muerto ANTES de cualquier otra cosa (línea 997-1002: `if (!target.Health.IsAlive) { DeadNotice.Show(...); OrderService.PlayRejectSound(); return false; }`), `EnterVehicleViewFromRts` nunca consulta `occupant.Health.IsAlive`. Este método es el que se invoca desde `UpdateRts` (línea 1731-1734) al apretar `[F]` apuntando con el mouse a un vehículo ocupado, como atajo para tomar el control de manejo sin tener que apuntarle al soldado puntual (los ocupantes están inactivos/ocultos mientras están montados, así que no se les puede apuntar directo). `Vehicle.Mount()` sí rechaza a un muerto al SUBIR (línea 212 de `Vehicle.cs`, corregido explícitamente como "BUG REAL" documentado en el propio archivo), pero eso sólo protege el momento de SUBIR — no hay ninguna garantía documentada en el código de que un ocupante YA montado no pueda llegar a tener `Health.IsAlive == false` en algún escenario (por ejemplo, alguna vía de daño futura que no filtre por `activeInHierarchy` como sí lo hacen hoy `Projectile.Tick`/`Explode`, o cualquier llamada directa a `Health.TakeDamage` fuera del camino normal de combate, como ya hace el propio `HeadlessTestRunner.cs:1610` con `doc.Health.TakeDamage(9999, -1)` para simular pruebas). Sin el chequeo, `EnterVehicleViewFromRts` podría terminar poseyendo un cadáver sentado, dejando al jugador controlando un soldado "muerto" sin que ningún camino de posesión válido lo hubiera permitido — exactamente la invariante que `TryPossess` existe para proteger.

**Plan de implementación:**
1. Agregar el mismo chequeo que ya usa `TryPossess`, con el mismo criterio de feedback al jugador (aviso + sonido de rechazo):
   ```csharp
   void EnterVehicleViewFromRts(Vehicle vehicle)
   {
       if (vehicle == null || vehicle.OccupantCount == 0) return;
       var occupant = vehicle.Driver ?? vehicle.Occupants[0];
       if (occupant == null) return;

       if (!occupant.Health.IsAlive)
       {
           if (DeadNotice != null) DeadNotice.Show($"{occupant.DisplayName} esta muerto: no se puede poseer");
           OrderService.PlayRejectSound();
           return;
       }

       PossessionService.Swap(Brain, occupant);
       var role = vehicle.RoleOf(occupant);
       if (role == null) return;

       Rig.SetMode(ControlMode.Fps);
       EnterPossessedVehicleSeat(role.Value);
   }
   ```
2. Evaluar (opcional, mejora de UX): si `vehicle.Driver` está muerto pero hay OTRO ocupante vivo, hoy el método simplemente rechaza sin intentar con otro ocupante — `TryPossess` tampoco hace fallback automático a otro objetivo, así que esto mantiene la paridad de comportamiento con el patrón ya establecido (rechazar y listo, no elegir por el jugador), en vez de agregar lógica de fallback que no está pedida por el bug.

**Verificación:** Agregar un `Check()` en `RunPhase7` (que ya maneja posesión de montados, ver líneas 1558-1569): montar a `kes` en el vehículo, matarla con `kes.Health.TakeDamage(9999, -1)` MIENTRAS sigue montada (sin desmontar — para simular el escenario "ocupante muerto sin haber pasado por `Vehicle.Mount()`'s guard, que sólo protege el momento de subir"), invocar `EnterVehicleViewFromRts` por reflection apuntando a ese vehículo, y confirmar `Check("EnterVehicleViewFromRts rechaza a un ocupante muerto", inputDriver.Brain.Current != kes)`; revivir a `kes` al final (`kes.Health.Initialize(kes.Id, maxHp)`) y desmontarla para no contaminar el resto de la suite, siguiendo el mismo patrón de limpieza que ya usa `RunPhase7` en el caso de `doc` (línea 1614).

**Riesgo/efectos secundarios:** Confirmar que `occupant.Health` nunca es `null` en este punto (todos los `Soldier` deberían tener `Health` inicializado por construcción, igual que asume el resto del archivo sin chequeo defensivo adicional — consistente con el resto de la clase). Verificar que este cambio no afecta el camino normal (ocupante vivo, el 100% de los casos reales in-game hoy), y que el mensaje de `DeadNotice` no compite/pisa a otro aviso simultáneo si el jugador aprieta `[F]` repetidamente contra el mismo vehículo con ocupante muerto (mismo comportamiento que ya tiene `TryPossess` en ese escenario, así que no introduce ningún caso nuevo sin cubrir).


---

# Vehículos / Demo — Planes de corrección (11 bugs)

Leído el estado actual (post-sesión) de los 9 archivos: `Vehicles/Vehicle.cs`, `Vehicles/VehicleBrain.cs`, `Vehicles/TurretAI.cs`, `Vehicles/TurretWeapon.cs`, `Vehicles/DetachedTurretFlight.cs`, `Vehicles/VehicleMotor.cs`, `Demo/AutoDemoRunner.cs`, `Player/PlayerInputDriver.cs` (método `SwitchSeat`), más el soporte (`Core/WorldSystemsRegistry.cs`, `Presentation/SafeMaterial.cs`, `Presentation/VehicleFxReactor.cs`, `Editor/HeadlessTestRunner.cs`). Todas las referencias de línea son contra el código tal cual quedó tras los cambios de esta sesión (mount-animation coroutine en `Vehicle.Mount()`, gating de ocupación en `TurretAI`/`VehicleBrain`, remoción del camera-shake en `TurretWeapon.TryFire()`).

---

### Bug 1: `targetPos` de la animación de montaje es una foto fija del vehículo
**Archivos:** `Vehicles/Vehicle.cs:247-283` (`PlayMountAnimation`), específicamente la línea `var targetPos = transform.position;` (línea 251).

**Causa raíz:** `PlayMountAnimation` es una corutina que corre hasta 0.35s (`MountAnimationSeconds`). `targetPos` se lee UNA sola vez, antes del `while`, con `transform.position` (el transform del `Vehicle`, no del soldado). Si en esos 0.35s el vehículo se mueve (conducido por `VehicleBrain.Tick()` con una orden activa, o por el jugador con `VehicleMotor.Drive()`), el `Vector3.Lerp(startPos, targetPos, eased)` de cada frame sigue apuntando al punto viejo: el soldado converge y desaparece en un lugar que el vehículo ya abandonó.

**Plan de implementación:**
1. En `PlayMountAnimation`, borrar la línea `var targetPos = transform.position;` de antes del `while`.
2. Dentro del `while (t < MountAnimationSeconds)`, justo antes de la línea `soldier.transform.position = Vector3.Lerp(...)`, agregar una lectura fresca: `Vector3 targetPos = transform.position;` (o, si se aplica el Bug 2 al mismo tiempo, `transform.position + offset` — ver más abajo). `transform` sigue siendo el del `Vehicle` (la corutina corre en `StartCoroutine` de `Vehicle`, así que `this.transform` es el chasis), así que no hace falta cachear ninguna referencia nueva.
3. Dejar `startPos` como está (se lee una sola vez a propósito: el soldado arranca desde donde estaba parado, eso no cambia).
4. No tocar el resto del cuerpo del `while` (el `eased`, el chequeo de `RoleOf(soldier) == null`, etc.) — el fix es puramente mover la lectura de `targetPos` de "una vez antes del loop" a "una vez por frame, dentro del loop".

**Verificación:** Vía Play mode real (esta corutina solo corre con `Application.isPlaying`, `HeadlessTestRunner` la evita a propósito con la rama `else soldier.gameObject.SetActive(false);` en `Mount()`, así que **no** hay un `Check()` de Edit mode posible para esto). Secuencia concreta:
1. Entrar a Play mode con la escena de juego real (o la que arma `AutoDemoRunner`/`HeadlessTestRunner` al levantar la escena).
2. Colocar el vehículo con una orden de movimiento activa: `vehicleBrain.IssueMoveOrder(destinoLejano)` (o simplemente mantener `motor.Drive(1f, 0f, dt)` unos frames) ANTES de que termine de montar.
3. Llamar `vehicle.Mount(soldado, VehicleSeatRole.Passenger1)` (o, si se prueba con el jugador, acercarse y apretar `[E]`) en el mismo instante en que el vehículo está en movimiento.
4. Cada pocos frames (o con `ScreenCapture.CaptureScreenshotAsTexture()` al estilo `CaptureStep`), loguear `Vector3.Distance(soldado.transform.position, vehicle.transform.position)`.
5. **Antes del fix:** esa distancia crece sin límite a medida que el vehículo se aleja del punto congelado — el soldado queda "clavado" lerpeando hacia un punto vacío mientras el vehículo sigue de largo.
6. **Después del fix:** la distancia se mantiene acotada (convergiendo a ~0, o al offset del asiento si también se aplicó el Bug 2) durante los 0.35s, sin importar que el vehículo se mueva.

**Riesgo/efectos secundarios:** Ninguno funcional grave — es una lectura, no un estado compartido. Único cuidado: si el vehículo es destruido (`IsDestroyed`) DURANTE el `Mount()` en curso (caso raro pero posible si un proyectil le pega justo en ese frame), `transform.position` del vehículo sigue siendo válido (el GameObject no se destruye, solo se marca `IsDestroyed`), así que no hay riesgo de `NullReferenceException`; el `if (soldier == null) yield break;` y el chequeo de `RoleOf` ya cubren la salida temprana si el soldado fue expulsado por `OnDestroyed()` (que llama `Dismount` para todos los ocupantes).

---

### Bug 2: sin offset por asiento — dos soldados que suben a la vez convergen al mismo punto
**Archivos:** `Vehicles/Vehicle.cs:203-283` (`Mount()` + `PlayMountAnimation`); comparar con `DismountOffsetFor()` en `Vehicles/Vehicle.cs:312-322`.

**Causa raíz:** `DismountOffsetFor(role)` ya resuelve este problema para la bajada (cada asiento baja a un costado distinto). Pero `PlayMountAnimation` nunca recibe el `role` asignado — usa siempre `transform.position` (el centro exacto del chasis) como destino, sea cual sea el asiento. Si dos soldados (p. ej. dos pasajeros cercanos que se auto-montan al acercarse el conductor) arrancan `PlayMountAnimation` casi al mismo tiempo, ambos lerpean hacia el MISMO punto matemático, superpuestos visualmente hasta desaparecer juntos.

**Plan de implementación:**
1. Cambiar la firma de la corutina: `IEnumerator PlayMountAnimation(Soldier soldier, VehicleSeatRole role)`.
2. En `Mount()` (línea 238), donde hoy dice `StartCoroutine(PlayMountAnimation(soldier));`, pasar el asiento ya resuelto: `StartCoroutine(PlayMountAnimation(soldier, role));` (la variable local `role` ya existe en `Mount()`, resuelta unas líneas antes en el `if (preferredRole.HasValue...) else { ... role = free.Value; }`).
3. Agregar un método hermano de `DismountOffsetFor`, con magnitud más chica (el soldado tiene que converger CERCA del chasis, no a la distancia de "parado afuera" que usa el dismount):
   ```csharp
   // Mismo criterio de lado que DismountOffsetFor, pero mas cerca del
   // chasis: es el punto de "asiento" al que converge la animacion de
   // subida, no el punto donde el soldado queda parado al bajarse.
   Vector3 MountOffsetFor(VehicleSeatRole role) => DismountOffsetFor(role) * 0.4f;
   ```
4. En `PlayMountAnimation`, combinar con el fix del Bug 1: la lectura por-frame pasa a ser
   ```csharp
   Vector3 targetPos = transform.position + MountOffsetFor(role);
   ```
5. Revisar que el `RoleOf(soldier) == null` (chequeo de corte temprano dentro del `while`) siga funcionando igual — no depende del parámetro `role` nuevo, sigue consultando el diccionario `seats` en vivo.

**Verificación:** También Play mode real (misma limitación que el Bug 1 — no hay Check() de Edit mode posible).
1. Colocar dos soldados aliados cerca uno del otro y del vehículo, de forma que ambos autose-monten al mismo tiempo (el mismo escenario que ya arma `AutoDemoRunner` Fase 4: Vega como conductor y Kes "sube sola" por estar cerca).
2. Disparar el montaje de los dos en el mismo frame (o con 1-2 frames de diferencia) — por ejemplo `vehicle.Mount(vega, VehicleSeatRole.Driver); vehicle.Mount(kes, VehicleSeatRole.Passenger1);` seguidos.
3. Congelar unos frames a mitad de la animación (por ejemplo con `Time.timeScale` bajo, o revisando `t` con un breakpoint/log en el `while`) y comparar `vega.transform.position` vs `kes.transform.position`.
4. **Antes del fix:** las dos posiciones convergen al mismo punto (`Vector3.Distance` entre ambas tiende a 0 a medida que `k` se acerca a 1).
5. **Después del fix:** las posiciones convergen a puntos distintos, separados según `MountOffsetFor(Driver)` vs `MountOffsetFor(Passenger1)` (izquierda-adelante vs izquierda-atrás), sin superposición visual.

**Riesgo/efectos secundarios:** El valor `0.4f` de `MountOffsetFor` es una elección de diseño (qué tan "adentro" converge el soldado antes de desaparecer) — ajustable a ojo en Play mode si se ve muy separado del chasis o si el soldado atraviesa visualmente el capot. Verificar también los 4 asientos (`Driver`, `Gunner`, `Passenger1`, `Passenger2`), no solo dos, porque el vehículo tiene capacidad para 4 (`Capacity => AllRoles.Length`) y en teoría los 4 podrían auto-montarse casi simultáneamente si hay 4 aliados cerca.

---

### Bug 3 (EL MÁS GRAVE DE ESTA SECCIÓN — corrupción visual permanente): doble corutina de montaje deja al soldado con escala incorrecta para siempre
**Archivos:** `Vehicles/Vehicle.cs:203-283` (`Mount()` + `PlayMountAnimation`) en combinación con `Player/PlayerInputDriver.cs:1491-1519` (`SwitchSeat`, invocable cada frame por las teclas `[1]`/`[2]` mientras se está adentro del vehículo).

**Causa raíz:** `SwitchSeat()` hace `Vehicle.Dismount(soldier)` (que NO toca `localScale`, según el propio comentario en `Dismount`) seguido de `Vehicle.Mount(soldier, newRole)`. Si esto pasa dentro de los primeros 0.35s de un montaje ya en curso, `Mount()` arranca una SEGUNDA corutina `PlayMountAnimation` sobre el MISMO soldado sin cancelar la primera — nada en el código actual lleva registro de "este soldado ya tiene una animación corriendo". La corutina vieja sigue viva porque el chequeo de corte temprano (`RoleOf(soldier) == null`) no se dispara: `RoleOf` devuelve el `newRole` recién asignado, no `null`. Las dos corutinas lerpean `localScale` de forma independiente, cada una con SU PROPIO `startScale` capturado en su propio instante de arranque — el de la segunda corutina es la escala YA achicada en el momento en que arrancó, no la escala real original. Cualquiera de las dos que termine ÚLTIMA (típicamente la segunda, que arrancó después) restaura `localScale = startScale` con ese valor intermedio corrupto, y ahí se queda: el soldado reaparece (la próxima vez que se lo baje) permanentemente mal escalado.

**Plan de implementación:**
1. Agregar a `Vehicle` dos diccionarios de seguimiento por soldado (mismo patrón de "diccionario por Soldier" que ya usa `seats`):
   ```csharp
   readonly Dictionary<Soldier, Coroutine> mountAnimations = new Dictionary<Soldier, Coroutine>();
   readonly Dictionary<Soldier, Vector3> mountTrueScale = new Dictionary<Soldier, Vector3>();
   ```
2. En `Mount()`, ANTES del bloque `if (Application.isPlaying) StartCoroutine(...)` (línea 238), insertar:
   ```csharp
   if (Application.isPlaying)
   {
       // Escala real cacheada UNA sola vez por soldado: si ya hay una
       // entrada, es porque venimos de una animacion interrumpida y
       // esta es la escala de VERDAD (antes de cualquier achicado), no
       // la volvemos a pisar.
       if (!mountTrueScale.ContainsKey(soldier)) mountTrueScale[soldier] = soldier.transform.localScale;

       // BUG REAL (el mas grave de esta tanda): sin esto, un cambio de
       // asiento rapido ([1]/[2] dentro de los 0.35s de la animacion de
       // subida) arrancaba una SEGUNDA corutina de montaje sobre el
       // mismo soldado mientras la primera seguia viva -- la que
       // terminara ultima restauraba localScale con SU PROPIO
       // startScale, que para la segunda corutina era una escala YA
       // achicada a mitad de camino, no la original. El soldado
       // quedaba enano/mal escalado para siempre. Se corta la
       // corutina vieja y se restaura la escala real ANTES de arrancar
       // la nueva.
       if (mountAnimations.TryGetValue(soldier, out var existingCo) && existingCo != null)
       {
           StopCoroutine(existingCo);
           soldier.transform.localScale = mountTrueScale[soldier];
       }

       mountAnimations[soldier] = StartCoroutine(PlayMountAnimation(soldier, role));
   }
   else soldier.gameObject.SetActive(false);
   ```
3. En `PlayMountAnimation`, cambiar `var startScale = soldier.transform.localScale;` por `var startScale = mountTrueScale[soldier];` (ya no se re-lee el transform, porque en este punto puede estar a mitad de achicar si por algún otro camino no cubierto se llamó dos veces — usar SIEMPRE la fuente única cacheada).
4. Al final de la corutina (donde hoy hace `soldier.transform.localScale = startScale; soldier.gameObject.SetActive(false);`), agregar la limpieza de los diccionarios para no acumular entradas de soldados que ya terminaron de montar (y para permitir que la PRÓXIMA vez que ese soldado suba a un vehículo — este u otro — se vuelva a cachear su escala real desde cero):
   ```csharp
   mountAnimations.Remove(soldier);
   mountTrueScale.Remove(soldier);
   ```
   Aplicar esta misma limpieza también en la rama de corte temprano (`if (RoleOf(soldier) == null) { ... yield break; }`), para que un `Dismount` a mitad de animación no deje basura en los diccionarios.
5. Revisar `Dismount()` (línea 286-306): si un soldado se baja de verdad (no como parte de un `SwitchSeat`) mientras tiene una animación de montaje corriendo, esa corutina sigue viva pero ya se corta sola por el `RoleOf(soldier) == null` — con el punto 4 ya limpia sus propias entradas, no hace falta tocar `Dismount()` directamente.

**Verificación:** Play mode real (no hay Check() de Edit mode posible — la corutina no corre fuera de Play). Secuencia:
1. Registrar la escala original del soldado antes de nada: `var originalScale = vega.transform.localScale;` (debería ser `Vector3.one` o la escala del prefab).
2. Montar a Vega de conductor: `vehicle.Mount(vega, VehicleSeatRole.Driver);` (o apretar `[E]` cerca del vehículo).
3. **Inmediatamente** (dentro de los primeros ~0.1-0.2s, bien adentro de la ventana de 0.35s), disparar un cambio de asiento: `inputDriver.SwitchSeat(VehicleSeatRole.Gunner);` (o apretar `[2]`).
4. Dejar correr unos frames más de 0.35s totales (`yield return new WaitForSeconds(0.5f);` en un script de prueba, o simplemente esperar en Play mode).
5. Bajar a Vega del vehículo: `vehicle.Dismount(vega);` (o `[E]`) y comparar `vega.transform.localScale` contra `originalScale`.
6. **Antes del fix:** `localScale` queda distinto de `originalScale` (achicado) — el soldado reaparece visualmente enano y se queda así el resto de la partida.
7. **Después del fix:** `localScale == originalScale` exactamente, sin importar cuántas veces se repita el cambio de asiento rápido dentro de la ventana de animación.
8. Repetir el mismo test presionando `[1]`/`[2]` 3-4 veces seguidas dentro de la misma ventana de 0.35s (para simular un jugador mashing las teclas) y confirmar que sigue restaurando bien — este caso ejercita más de dos corutinas encadenadas, no solo dos.

**Riesgo/efectos secundarios:** Esta es la corrección más delicada de las 11: agrega estado nuevo (dos diccionarios) que vive mientras el `Vehicle` exista, hay que asegurarse de:
- Limpiar las entradas también si el soldado MUERE mientras está montado (revisar que `OnDestroyed()` → `Dismount(occupant)` para todos los ocupantes no deje huérfanas las entradas de `mountAnimations`/`mountTrueScale` — como el `Dismount` normal ya provoca que la corutina viva se corte sola por `RoleOf == null` y limpie sus propias entradas en el punto 4, debería quedar cubierto, pero conviene verificarlo en Play mode con un vehículo destruido a mitad de un montaje).
- Si en el futuro se agrega algún otro camino que cambie `localScale` del soldado por otra razón (por ejemplo un power-up, o una animación de herida) mientras está en curso un montaje, ese camino podría pisar `mountTrueScale` sin que este sistema se entere — mantener `PlayMountAnimation` como el ÚNICO lugar que toca `localScale` de un soldado montándose.
- El fix depende de que `StopCoroutine` corte de verdad la corutina vieja en el mismo frame — confirmar que no queda un frame de por medio donde ambas corutinas escriben `localScale` a la vez (Unity corta la corutina de forma síncrona al llamar `StopCoroutine`, así que no debería haber problema, pero vale la pena confirmarlo con un log dentro del `while` mostrando cuál instancia de corutina está escribiendo).

---

### Bug 4: F9 durante un freeze de cámara dejaba `Time.timeScale` trabado en 0 para siempre
**Archivos:** `Demo/AutoDemoRunner.cs:71-76` (`StopDemo()`) + `Demo/AutoDemoRunner.cs:391-402` y `:426-437` (los dos bloques `Time.timeScale = 0f; ... yield ...; Time.timeScale = 1f;` en Fase 4 y Fase 5, armas recogibles).

**Causa raíz:** `StopDemo()` llama `StopCoroutine(running)` sin ningún `try/finally`. Cuando `DemoSequence()` está a mitad de uno de los bloques `Time.timeScale = 0f; ...; yield return CaptureStep(...); ...; Time.timeScale = 1f;` (el `yield return CaptureStep(...)` es en sí una corutina anidada que puede tardar varios frames reales — usa `WaitForSecondsRealtime` justamente para poder correr con el tiempo congelado), un `StopCoroutine` sobre la corutina externa aborta TODO el árbol de ejecución en el punto exacto donde está suspendida, sin ejecutar ninguna línea posterior — incluida la línea `Time.timeScale = 1f;` que nunca llega a correr. El juego entero queda congelado (timeScale=0) sin ningún camino en el juego para recuperarse.

**Plan de implementación:**
1. Envolver cada uno de los dos bloques de freeze en un `try/finally` (los `finally` de un iterador de C# SÍ corren cuando Unity aborta una corutina con `StopCoroutine` — internamente hace `Dispose()` sobre el enumerador, que dispara los `finally` pendientes; `yield return` dentro de un `try/finally` sin `catch` es válido en C#). Para el bloque de Fase 4 (líneas 391-402):
   ```csharp
   pickup.EquipOn(vega.Weapon, vega.Id);
   Time.timeScale = 0f;
   try
   {
       bool weaponFired = vega.Weapon.TryFire(vega.transform.position, vega.transform.forward);
       if (weaponFired && Projectile.ActiveInstances.Count > 0)
       {
           var proj = Projectile.ActiveInstances[Projectile.ActiveInstances.Count - 1];
           for (int i = 0; i < 5; i++) proj.Tick(0.03f);
       }
       TestLog.Step($"Arma {pickup.Kind} (color {pickup.Color}) equipada y disparada por {vega.DisplayName}: {weaponFired}");
       yield return CaptureStep($"fase4_arma_{pickup.Kind}_disparo");
   }
   finally
   {
       Time.timeScale = 1f;
   }
   ```
2. Aplicar el mismo patrón al segundo bloque (Fase 5, líneas 426-437, el `foreach` de `kinds`).
3. Como red de seguridad adicional (por si en el futuro se agrega un tercer freeze sin este `try/finally`, o por si hay algún otro camino de abort que no dispare `Dispose()` correctamente), reforzar `StopDemo()` para que SIEMPRE restaure el timeScale sin importar qué:
   ```csharp
   public void StopDemo()
   {
       if (running != null) StopCoroutine(running);
       IsRunning = false;
       // Red de seguridad: si el corte pasó a mitad de un freeze de
       // camara (Time.timeScale=0), el try/finally de cada bloque ya
       // debería restaurarlo -- pero esto asegura que F9 NUNCA deja el
       // juego trabado, ni siquiera si algún freeze futuro se olvida
       // del try/finally.
       Time.timeScale = 1f;
       TestLog.Warn("Demo automatico detenido a mano (F9).");
   }
   ```
4. Revisar si hay otros `Time.timeScale = 0f` en el proyecto fuera de `AutoDemoRunner` que puedan interactuar mal con este cambio (no debería, `Time.timeScale` es un estado global único, y forzar `=1f` en `StopDemo()` es seguro porque F9 solo se usa para parar la demo).

**Verificación:** Play mode real. `HeadlessTestRunner` corre en Edit mode y no ejercita `AutoDemoRunner` (que depende de `Time.timeScale`/coroutines reales), así que esto no tiene equivalente de `Check()` — se prueba a mano:
1. Entrar a Play mode con `autoPlayOnStart = true` (o arrancar la demo con F9 apenas empieza el juego).
2. Dejar correr hasta que la demo llegue a la Fase 4 (armas recogibles) — se nota porque la pantalla queda momentáneamente congelada un instante cada vez que dispara un arma para la foto.
3. Apretar F9 EXACTAMENTE durante uno de esos freezes (apuntar el timing viendo el log `TestLog.Step($"Arma {pickup.Kind}...")` en consola, que se imprime justo antes del `yield return CaptureStep(...)` congelado).
4. **Antes del fix:** tras F9, el juego queda completamente inmóvil (soldados, física de props, todo) — `Time.timeScale` se puede confirmar en 0 desde el inspector de Unity (Edit > Project Settings, o cualquier watch) y no hay forma de destrabarlo sin volver a entrar a Play mode.
5. **Después del fix:** tras F9, el juego se descongela inmediatamente (`Time.timeScale` vuelve a 1) y el jugador recupera control normal, aunque la demo se haya cortado a mitad de la Fase 4.
6. Repetir apretando F9 en el freeze de la Fase 5 también, para cubrir el segundo bloque.

**Riesgo/efectos secundarios:** Bajo. El único cuidado es no dejar el `try` demasiado angosto o demasiado ancho: tiene que cubrir exactamente el tramo entre `Time.timeScale = 0f` y `Time.timeScale = 1f` (incluyendo el `yield return CaptureStep(...)`, que es la parte que puede tardar varios frames reales). Si por error queda algo de lógica IMPORTANTE fuera del `try` pero antes del `Time.timeScale = 1f;` original, revisar que no se haya movido sin querer.

---

### Bug 5: `VehicleBrain.Tick()` no null-chequea `motor` antes de usarlo
**Archivos:** `Vehicles/VehicleBrain.cs:30` (`motor = GetComponent<VehicleMotor>();` en `Bootstrap()`), `:71` (`motor.Brake(dt)`), `:82` (`motor.Drive(1f, 0f, dt)`). Comparar con el patrón hermano en `Vehicles/TurretAI.cs:68` (`if (turret == null) return;`) y `:100` (`motor != null && !motor.IsStopped`, ese sí null-chequea).

**Causa raíz:** `Bootstrap()` cachea `motor` con un simple `GetComponent<VehicleMotor>()` sin validar el resultado. Si el prefab del vehículo no tiene el componente `VehicleMotor` (prefab mal armado, o una variante de vehículo sin motor todavía), `motor` queda `null` para siempre (el `bootstrapped` flag ya se puso en `true`, así que `Bootstrap()` no se reintenta). `Tick()` nunca chequea `motor == null` antes de llamar `motor.Brake(dt)` (línea 71, rama de "llegó a destino") ni `motor.Drive(1f, 0f, dt)` (línea 82, rama normal) — apenas exista una orden de movimiento activa (`destination.HasValue`), cualquiera de esas dos líneas revienta con `NullReferenceException` en cada `Tick()`, y como `Tick()` se llama directo desde el driver de simulación (no es un `Update()` de Unity), esto puede tirar la excepción decenas de veces por segundo.

**Plan de implementación:**
1. En `VehicleBrain.Tick()`, agregar el guard justo después del `if (!bootstrapped) Bootstrap();` (línea 50), en el mismo lugar/estilo que `TurretAI.Tick()` hace con `if (turret == null) return;` (línea 68):
   ```csharp
   public void Tick(float dt)
   {
       if (!bootstrapped) Bootstrap();
       // Mismo criterio que TurretAI con su torreta: un prefab de
       // vehiculo sin VehicleMotor (mal armado, o una variante nueva
       // todavia sin motor) no deberia poder recibir ordenes de
       // movimiento -- antes esto explotaba con NullReferenceException
       // en CADA Tick() apenas hubiera un destino pendiente, porque
       // Brake()/Drive() se llamaban sin chequear motor primero.
       if (motor == null) return;

       if ((vehicle != null && vehicle.IsDestroyed) || IsPlayerDriving || !destination.HasValue) return;
       ...
   }
   ```
2. No hace falta tocar `Bootstrap()`: el `GetComponent<VehicleMotor>()` puede seguir devolviendo `null` sin problema, ahora que `Tick()` lo respeta.
3. Opcional (no imprescindible, pero mejora la depuración): loguear una sola vez con `Debug.LogWarning` si `motor == null` tras `Bootstrap()`, para que un prefab mal armado se note en consola en vez de fallar en silencio — si se agrega, usar un flag booleano para loguear una sola vez y no spamear la consola cada `Tick()`.

**Verificación:** Se puede cubrir con un `Check()` de Edit mode en `RunPhase4` de `HeadlessTestRunner.cs` (cerca de las líneas 1055-1063, justo después de probar aceleración/frenado con el motor real), usando el mismo patrón de reflection que ya usa la suite para otros campos privados (`cdField`, `currentSeatField`, etc.):
```csharp
var motorField = typeof(VehicleBrain).GetField("motor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var originalMotor = motorField.GetValue(vBrain);
motorField.SetValue(vBrain, null);
vBrain.IssueMoveOrder(vehicle.transform.position + Vector3.forward * 5f);
bool threw = false;
try { vBrain.Tick(0.05f); } catch { threw = true; }
Check("VehicleBrain.Tick() no explota si el prefab no tiene VehicleMotor (motor null)", !threw);
motorField.SetValue(vBrain, originalMotor);
vBrain.Stop();
```
Colocar esto después de las verificaciones de frenado (línea ~1063) y antes de pasar a la prueba de la torreta (línea ~1065), para no contaminar el estado del resto de la fase.

**Riesgo/efectos secundarios:** Mínimo — es un guard puramente defensivo, no cambia ningún comportamiento cuando el motor SÍ existe (el caso normal, único caso real en el proyecto hoy ya que `P_Vehicle_Blindado.prefab` sí tiene `VehicleMotor`). Único cuidado: si el guard vuelve silenciosamente sin hacer nada, una orden de movimiento (`destination`) queda "pegada" para siempre en un vehículo sin motor (nunca llega a `arriveThreshold` porque nunca se mueve) — no es un bug nuevo (ya sería imposible moverlo de todas formas), pero si se agrega el log opcional del punto 3, ayuda a detectar el prefab roto en vez de dejarlo fallando en silencio indefinidamente.

---

### Bug 6: color de ocupación/daño/destrucción pintado sobre `sharedMaterial` — bleed entre vehículos
**Archivos:** `Vehicles/Vehicle.cs:154-160` (`CacheColorIfNeeded`), `:162-168` (`RefreshOccupancyColor`), `:102-103` (dentro de `OnDestroyed`), `:117-118` (dentro de `FinalExplosion`).

**Causa raíz:** Las cuatro escrituras de color pintan `r.sharedMaterial.color` directamente. `sharedMaterial` es, por definición de Unity, el ASSET de material compartido por TODOS los renderers/instancias que lo referencian — no una copia propia de este vehículo. El único prefab de vehículo del proyecto (`Assets/_Project/Prefabs/P_Vehicle_Blindado.prefab`) se arrastra a mano a la escena (no hay ningún spawner en runtime, a diferencia de lo que hace `HeadlessTestRunner.SpawnVehicle()` que sí instancia un material nuevo por vehículo vía `CreateFlatMaterial` — ESE camino no tiene el bug, pero es exclusivo de la suite de test). En una escena real con dos o más instancias del prefab colocadas directamente, todas comparten el mismo `Material` asset serializado: pintar el chasis de uno (al subirse gente, al recibir daño, al morir) tiñe a TODOS los vehículos que usan ese material, aunque estén sanos y vacíos.

**Plan de implementación:**
1. El fix se concentra en un solo método, `CacheColorIfNeeded()` — como las otras tres escrituras (`RefreshOccupancyColor`, `OnDestroyed`, `FinalExplosion`) YA llaman `CacheColorIfNeeded()` antes de escribir color (confirmado leyendo el código: `RefreshOccupancyColor` lo llama en su primera línea; `OnDestroyed` lo llama en la línea 101 antes de su loop de color en 102-103; `FinalExplosion` no lo llama directamente pero SIEMPRE corre después de `OnDestroyed`, que ya lo cacheó), basta con que `CacheColorIfNeeded` deje cada `Renderer` apuntando a su PROPIA instancia de material, y las otras tres escrituras quedan automáticamente aisladas sin tocarles una sola línea.
2. NO usar `SafeMaterial.Create(color)` para esto: esa utilidad clona un material de TEMPLATE genérico (un cubo primitivo con el shader default de la pipeline) — sirve para FX que no necesitan textura/shader especial, pero acá se perdería el material real del vehículo (textura, variante de shader, etc.) si el arte del vehículo alguna vez deja de ser un color plano. En vez de eso, clonar el material REAL que ya tiene cada renderer, igual a lo que hace `HeadlessTestRunner.SpawnVehicle` con `CreateFlatMaterial` pero por-instancia y por-renderer.
3. NO usar tampoco el getter `Renderer.material` (singular, sin `shared`): aunque Unity lo auto-instancia la primera vez que se accede, ese camino tira un warning de consola ("Instantiating material due to calling renderer.material...") cuando corre en Edit mode — y `CacheColorIfNeeded` puede correr en Edit mode (headless suite). Clonar a mano evita ese warning.
4. Reescribir `CacheColorIfNeeded`:
   ```csharp
   void CacheColorIfNeeded()
   {
       if (colorCached) return;
       colorCached = true;
       chassisRenderers = GetComponentsInChildren<Renderer>();

       // BUG REAL: antes esto solo LEIA sharedMaterial.color para
       // cachear baseColor, y las otras 3 escrituras (ocupacion, daño,
       // destruccion) pintaban directo sobre ese mismo sharedMaterial
       // -- el ASSET compartido por todos los vehiculos que usan el
       // mismo prefab (que es el caso normal: el prefab se arrastra a
       // mano varias veces a la escena). Pintar UNO los pintaba a
       // TODOS. Ahora cada vehiculo clona su propio material por
       // renderer, una sola vez, y reasigna sharedMaterial a ESA
       // instancia -- de ahi en mas "sharedMaterial" en este objeto ya
       // no es compartido con nadie mas, y las otras 3 escrituras
       // (que no cambian) quedan aisladas gratis.
       for (int i = 0; i < chassisRenderers.Length; i++)
       {
           var r = chassisRenderers[i];
           if (r == null || r.sharedMaterial == null) continue;
           r.sharedMaterial = new Material(r.sharedMaterial);
       }

       if (chassisRenderers.Length > 0 && chassisRenderers[0].sharedMaterial != null)
           baseColor = chassisRenderers[0].sharedMaterial.color;
   }
   ```
5. Revisar que ningún otro sistema dependa de que el chasis siga apuntando al material ORIGINAL del prefab (por ejemplo, un sistema de minimap o de LOD que compare `sharedMaterial` por referencia) — no se encontró ninguno en el código leído, pero vale un grep de `sharedMaterial` sobre `Vehicle`/`chassisRenderers` antes de dar el fix por cerrado.

**Verificación:** Sí se puede cubrir 100% en Edit mode con `Check()`, agregando una fase o extendiendo `RunPhase4`/`RunPhase7` en `HeadlessTestRunner.cs`: instanciar un SEGUNDO `Vehicle` desde el MISMO prefab (`SpawnVehicle(vehiclePrefab, otraPosicion, mismoColor, pool)` reusando el prefab ya construido por `BuildAndSaveVehiclePrefab`), ocupar/dañar solo el primero, y verificar que el segundo no cambió:
```csharp
var vehicle2 = SpawnVehicle(vehiclePrefab, vehicle.transform.position + Vector3.right * 30f, colorVehicle, pool);
var rend1 = vehicle.GetComponentInChildren<MeshRenderer>();
var rend2 = vehicle2.GetComponentInChildren<MeshRenderer>();
Color color2Antes = rend2.sharedMaterial.color;
vehicle.Mount(kes, VehicleSeatRole.Passenger1); // dispara RefreshOccupancyColor en vehicle
Check("Ocupar un vehiculo NO cambia el color del OTRO que comparte material de prefab",
    rend2.sharedMaterial.color == color2Antes);
Check("Los dos vehiculos ya NO comparten la misma instancia de Material",
    rend1.sharedMaterial != rend2.sharedMaterial || rend1.sharedMaterial == null);
vehicle.Dismount(kes);
```
Nota: en el flujo actual de `SpawnVehicle` cada vehículo YA recibe su propio material vía `CreateFlatMaterial`, así que para que este `Check()` reproduzca de verdad el bug hay que asignar a mano el MISMO `Material` a ambos renderers ANTES de llamar `Mount`, simulando el caso real (prefab arrastrado dos veces sin repintar): `rend2.sharedMaterial = rend1.sharedMaterial;` justo después de spawnear `vehicle2`, y recién ahí correr el `Check()` de arriba.

**Riesgo/efectos secundarios:** Cada vehículo en escena ahora aloja un `Material` extra por renderer que Unity no libera solo (igual que ya documenta el comentario de `VehicleSmokePuff.Drift()` sobre materiales huérfanos) — si en algún momento el `Vehicle` se destruye en runtime (hoy no pasa: el GameObject queda como carcasa para siempre, según el propio diseño de `OnDestroyed`/`FinalExplosion`), habría que `Destroy()` esos materiales clonados igual que se hace con `VehicleSmokePuff`. **Importante:** `Presentation/VehicleFxReactor.cs` (`SparkFlash()`, líneas 109-117) TAMBIÉN escribe `chassisRenderers[i].sharedMaterial.color = SparkColor;` directo — ese archivo no está en la lista de los 11 bugs a corregir ahora, pero una vez aplicado este fix, `VehicleFxReactor` seguirá escribiendo sobre lo que en ese momento YA es la instancia propia del vehículo (porque `CacheColorIfNeeded` de `Vehicle` corre antes, disparado por `TakeDamage`), así que el flash de chispa debería seguir funcionando bien sin tocarlo — pero conviene confirmarlo en Play mode con dos vehículos golpeados a la vez, por las dudas de algún orden de inicialización distinto entre `VehicleFxReactor.Bootstrap()` (que cachea sus propios `baseColors[]` por separado) y `Vehicle.CacheColorIfNeeded()`.

---

### Bug 7: `TurretAI` no limpia el blanco al ceder el control a un artillero humano
**Archivos:** `Vehicles/TurretAI.cs:88-90` (cede el control) vs `:115-120` (retargeteo) y `:78-83` (única limpieza existente, atada a `OccupantCount == 0`).

**Causa raíz:** El único lugar que hoy limpia `target` es el guard de "vehículo vacío" (línea 78-83, `if (vehicle == null || vehicle.OccupantCount == 0) { ...; target = null; return; }`). Pero cuando un artillero humano toma el asiento (`vehicle.Gunner != null`), el código simplemente hace `PublishControlChange(!hasHumanGunner); if (hasHumanGunner) return;` (línea 88-90) y sale — sin tocar `target` ni `retargetTimer`. Si ese humano se baja MÁS TARDE mientras sigue habiendo otro ocupante adentro (`OccupantCount` no llega a 0, así que el guard de la línea 78 no se dispara), la IA retoma el control con el `target` viejo (de antes de que el humano subiera) todavía asignado, y con `retargetTimer` congelado en el valor que tenía en el momento en que la IA cedió el control (nunca decrementa mientras `hasHumanGunner` es `true`, porque la línea `retargetTimer -= dt` está DESPUÉS del `return` de la línea 90). El resultado: hasta `retargetInterval` (0.4s) de disparo con un blanco no revalidado desde antes de que el humano tomara el control.

**Plan de implementación:**
1. En el bloque de la línea 85-90, limpiar el blanco en el momento exacto en que se detecta que hay un artillero humano (no solo cuando el conteo de ocupantes llega a 0):
   ```csharp
   bool hasHumanGunner = vehicle.Gunner != null;
   PublishControlChange(!hasHumanGunner);
   if (hasHumanGunner)
   {
       // BUG REAL: antes el blanco solo se limpiaba cuando el vehiculo
       // quedaba completamente vacio (OccupantCount == 0). Si un
       // artillero humano tomaba el control con un blanco ya
       // trabado, y despues se bajaba SIN que el vehiculo quedara
       // vacio (otro ocupante seguia adentro), la IA retomaba
       // disparandole a ese blanco viejo sin revalidar -- ademas
       // retargetTimer quedaba congelado (nunca decrementa mientras
       // hay artillero humano, la resta esta mas abajo), asi que
       // podia tardar hasta retargetInterval completo en reconsiderar.
       target = null;
       return;
   }
   ```
2. No hace falta tocar `retargetTimer` explícitamente: la condición de retargeteo de la línea 116 es `retargetTimer <= 0f || target == null || ...` (un OR) — con `target == null` ya alcanza para forzar un retargeteo inmediato en el próximo `Tick()` donde la IA recupere el control, sin esperar a que `retargetTimer` llegue a 0. (Opcional, solo por prolijidad/consistencia visual: también poner `retargetTimer = 0f` ahí mismo, para que el estado interno no quede con un valor viejo "mintiendo" sobre cuánto falta para el próximo retargeteo — no cambia el comportamiento observable, pero es más limpio de leer/debuggear).
3. Confirmar que `PublishControlChange(!hasHumanGunner)` sigue publicando el evento de UI/feedback correctamente antes del `return` — no se toca ese orden.

**Verificación:** Cubrible 100% en Edit mode con `Check()`, usando reflection sobre el campo privado `target` (mismo patrón ya usado en la suite para otros privados). Agregar en `RunPhase4` o `RunPhase6` de `HeadlessTestRunner.cs`, después de las pruebas existentes de la torreta:
```csharp
var targetField = typeof(TurretAI).GetField("target", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var turretAi = vehicle.GetComponentInChildren<TurretAI>();

// Un solo ocupante IA (doc), sin artillero humano: la IA debe fijar blanco
// contra algun enemigo cercano dentro de rango.
vehicle.Mount(doc, VehicleSeatRole.Driver);
turretAi.Tick(0.05f);
bool aiFijoBlanco = targetField.GetValue(turretAi) != null;
Check("TurretAI fija un blanco cuando NO hay artillero humano (setup de la prueba)", aiFijoBlanco);

// Sube un artillero humano (Vega): el control debe cederse Y limpiarse el blanco.
vehicle.Mount(vega, VehicleSeatRole.Gunner);
turretAi.Tick(0.05f);
Check("TurretAI limpia el blanco apenas un artillero humano toma el control",
    targetField.GetValue(turretAi) == null);

vehicle.Dismount(vega);
vehicle.Dismount(doc);
```

**Riesgo/efectos secundarios:** Bajo. El comportamiento nuevo es estrictamente "olvidar el blanco un poco antes" (más conservador que antes) — no introduce disparos nuevos ni cambia a quién le dispara la IA cuando SÍ tiene el control. Único cuidado: si el vehículo tiene 2+ ocupantes IA y un humano se turna seguido entre asientos, la IA va a re-adquirir blanco desde cero cada vez que recupera el control (en vez de "recordar" el blanco anterior) — es el comportamiento correcto y deseado según la descripción del bug, pero vale confirmarlo no genera un parpadeo raro de retargeteo si el jugador cicla asientos muy rápido (relacionado indirectamente con el Bug 11).

---

### Bug 8: color de calor del cañón pintado sobre `sharedMaterial` — mismo defecto que el Bug 6, en `TurretWeapon`
**Archivos:** `Vehicles/TurretWeapon.cs:339-346` (`ApplyHeatColor`, llamado cada `Tick()` desde la línea 173).

**Causa raíz:** Idéntica al Bug 6, pero sobre el renderer del cañón (`barrel`, buscado por nombre `"TurretBarrel"`) en vez del chasis. `ApplyHeatColor` cachea `barrelBaseColor` leyendo `rend.sharedMaterial.color` una sola vez (bien, es lectura), pero después escribe `rend.sharedMaterial.color = Color.Lerp(...)` EN CADA TICK sobre ese mismo `sharedMaterial` — si dos vehículos comparten el material del cañón (mismo caso: prefab arrastrado dos veces sin repintar a mano), el efecto de "cañón al rojo por sobrecalentamiento" de uno se contagia al cañón del otro, incluso si ese otro no disparó nunca.

**Plan de implementación:**
1. Mismo patrón que el Bug 6: instanciar el material del cañón UNA sola vez, en el momento del cacheo, y reasignarlo al renderer — de ahí en más las escrituras por-frame quedan aisladas sin tocarlas.
2. Reescribir el bloque de cacheo dentro de `ApplyHeatColor` (línea 344):
   ```csharp
   void ApplyHeatColor()
   {
       if (barrel == null) return;
       var rend = barrel.GetComponent<MeshRenderer>();
       if (rend == null) return;
       if (!barrelColorCached)
       {
           barrelColorCached = true;
           // Mismo bug/mismo fix que Vehicle.CacheColorIfNeeded (Bug 6):
           // sin clonar, esto pintaba sharedMaterial -- el ASSET
           // compartido entre todos los vehiculos que usan el mismo
           // prefab -- asi que el "cañon al rojo" de un tanque
           // sobrecalentado se contagiaba al cañon de cualquier otro
           // vehiculo con el mismo material.
           if (rend.sharedMaterial != null) rend.sharedMaterial = new Material(rend.sharedMaterial);
           barrelBaseColor = rend.sharedMaterial != null ? rend.sharedMaterial.color : Color.white;
       }
       rend.sharedMaterial.color = Color.Lerp(barrelBaseColor, new Color(1f, 0.25f, 0.1f), Heat);
   }
   ```
3. No hace falta ningún otro cambio: `Heat`, `HeatPerShot`, `HeatCoolPerSec` y el resto de la lógica de calor no se tocan.

**Verificación:** Cubrible en Edit mode con `Check()`, mismo patrón que el Bug 6 pero apuntando al renderer del cañón. Agregar cerca de las pruebas de torreta en `RunPhase4`/`RunPhase6`:
```csharp
var vehicle2 = SpawnVehicle(vehiclePrefab, vehicle.transform.position + Vector3.right * 40f, colorVehicle, pool);
var barrel1 = vehicle.GetComponentInChildren<TurretWeapon>().transform.Find("TurretBarrel").GetComponent<MeshRenderer>();
var barrel2 = vehicle2.GetComponentInChildren<TurretWeapon>().transform.Find("TurretBarrel").GetComponent<MeshRenderer>();
barrel2.sharedMaterial = barrel1.sharedMaterial; // simula el prefab arrastrado 2 veces sin repintar
Color colorBarril2Antes = barrel2.sharedMaterial.color;

var turret1 = vehicle.GetComponentInChildren<TurretWeapon>();
var cdField = typeof(TurretWeapon).GetField("cooldownTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
cdField.SetValue(turret1, 0f);
for (int i = 0; i < 6; i++) { turret1.TryFire(); cdField.SetValue(turret1, 0f); turret1.Tick(0.05f); } // sube el calor y aplica el color varias veces

Check("Sobrecalentar el cañon de UN vehiculo no pinta el cañon del OTRO (material compartido de prefab)",
    barrel2.sharedMaterial.color == colorBarril2Antes);
```

**Riesgo/efectos secundarios:** Mismo que el Bug 6 (material extra por vehículo que Unity no libera solo). Adicionalmente: el material del cañón en `HeadlessTestRunner.SpawnVehicle` YA se pinta con `CreateFlatMaterial(new Color(0.12f, 0.12f, 0.13f))` de forma independiente por vehículo (línea 2056) — ese camino de test específico no reproduce el bug tal cual (cada instancia ya tiene su propio material del cañón), por eso el `Check()` de arriba fuerza a mano `barrel2.sharedMaterial = barrel1.sharedMaterial;` para simular el caso real de un prefab arrastrado dos veces en una escena a mano.

---

### Bug 9: la torreta desprendida nunca se destruye — fuga sin límite en `WorldSystemsRegistry`
**Archivos:** `Vehicles/DetachedTurretFlight.cs:9-40` (toda la clase) + `Vehicles/Vehicle.cs:127-137` (`DetachTurret`).

**Causa raíz:** `DetachTurret()` hace `t.SetParent(null, true)` y le agrega `DetachedTurretFlight`, pero nunca destruye el GameObject de la torreta (ni ahora, ni después de que aterrice). `TurretWeapon` y `TurretAI` siguen vivos sobre ese GameObject, y ambos se registraron en `WorldSystemsRegistry` (`OnEnable`/`Bootstrap` → `Register`) y solo se dan de baja en su propio `OnDestroy()` (`Unregister`) — que nunca llega a correr porque nadie destruye el objeto. El `WorldSimulationDriver` sigue recorriendo `WorldSystemsRegistry.TurretWeapons`/`TurretAis` para siempre, incluyendo esta torreta zombie (`turret.enabled = false`/`ai.enabled = false` que pone `OnDestroyed()` en el vehículo original NO alcanza, porque — como ya documentan varios comentarios del propio archivo — `Tick()`/`TryFire()` se llaman por método directo desde el driver, no por el `Update()` automático de Unity, así que `enabled=false` no frena nada). Cada vehículo destruido en una partida deja UNA torreta más acumulada para siempre en esas listas.

**Plan de implementación:**
1. Aprovechar que `DetachedTurretFlight.Update()` ya detecta el aterrizaje (`landed = true` cuando `transform.position.y <= 0.25f`) — agregar un temporizador post-aterrizaje y destruir el GameObject cuando se cumple, siguiendo el mismo criterio de "restos visibles un rato y después se limpian" que ya usa `VehicleFxReactor.WreckSmokeSeconds` (8f) para el humo de la carcasa:
   ```csharp
   public class DetachedTurretFlight : MonoBehaviour
   {
       Vector3 velocity;
       Vector3 spin;
       bool landed;
       float landedTimer;

       const float Gravity = -18f;
       // Mismo criterio que VehicleFxReactor.WreckSmokeSeconds: se deja
       // el resto visible en el piso un rato (lectura RTS de "ahi cayo
       // un cañon") y despues se limpia. Sin esto, TurretWeapon/TurretAI
       // seguian registrados en WorldSystemsRegistry Y TICKEANDOSE PARA
       // SIEMPRE -- Tick()/TryFire() se llaman por metodo directo desde
       // el driver, "enabled=false" (que ya les pone Vehicle.OnDestroyed)
       // no alcanza para frenarlos. Una torreta zombie mas por cada
       // vehiculo destruido en la partida, sin limite.
       const float DestroyAfterLandedSeconds = 6f;

       public void Launch()
       {
           velocity = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(9f, 13f), Random.Range(-2.5f, 2.5f));
           spin = new Vector3(Random.Range(-260f, 260f), Random.Range(-180f, 180f), Random.Range(-260f, 260f));
       }

       void Update()
       {
           if (landed)
           {
               landedTimer += Time.deltaTime;
               if (landedTimer >= DestroyAfterLandedSeconds) Destroy(gameObject);
               return;
           }

           float dt = Time.deltaTime;
           velocity.y += Gravity * dt;
           transform.position += velocity * dt;
           transform.Rotate(spin * dt, Space.Self);

           if (transform.position.y <= 0.25f)
           {
               var p = transform.position;
               p.y = 0.25f;
               transform.position = p;
               landed = true;
           }
       }
   }
   ```
2. No hace falta tocar `Vehicle.DetachTurret()` — `TurretWeapon.OnDestroy()` y `TurretAI.OnDestroy()` YA llaman `WorldSystemsRegistry.Unregister(...)` (confirmado leyendo ambos archivos), así que un simple `Destroy(gameObject)` sobre la torreta dispara la baja del registro sola, sin que `Vehicle.cs` tenga que saber nada de `WorldSystemsRegistry` directamente (mismo desacople que ya usa el resto del proyecto).
3. Si se prefiere evitar el `Destroy` en Edit mode (donde `DetachTurret`/`FinalExplosion` también pueden correr, según el comentario de `OnDestroyed`: `else FinalExplosion(); // en Edit mode (suite headless) no hay Invoke util`), usar el mismo patrón condicional que ya usa el resto del proyecto (`Application.isPlaying ? Destroy(...) : DestroyImmediate(...)`) — aunque en la práctica `Update()` de `DetachedTurretFlight` de por sí no corre en Edit mode (no tiene `[ExecuteAlways]`), así que el temporizador nunca llega a dispararse ahí; no se requiere ningún cambio extra para ese camino, pero vale dejarlo anotado por si en el futuro se necesita simular esto en la suite headless.

**Verificación:** Se puede armar un `Check()` de Edit mode, aunque requiere invocar manualmente el `Update()` privado de `DetachedTurretFlight` por reflection (ya que no corre solo en Edit mode) para simular el paso de los `DestroyAfterLandedSeconds`:
```csharp
int turretCountAntes = WorldSystemsRegistry.TurretWeapons.Count;
int aiCountAntes = WorldSystemsRegistry.TurretAis.Count;

var detachMethod = typeof(Vehicle).GetMethod("DetachTurret", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
detachMethod.Invoke(vehicle, null); // simula la explosion final desprendiendo la torreta

var flier = UnityEngine.Object.FindAnyObjectByType<DetachedTurretFlight>();
Check("DetachTurret crea el DetachedTurretFlight y la torreta sigue registrada mientras vuela",
    flier != null && WorldSystemsRegistry.TurretWeapons.Count == turretCountAntes);

var updateMethod = typeof(DetachedTurretFlight).GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var landedField = typeof(DetachedTurretFlight).GetField("landed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
landedField.SetValue(flier, true); // salta directo a "ya aterrizo", no hace falta simular la caida
for (int i = 0; i < 130; i++) updateMethod.Invoke(flier, null); // ~6.5s a razon de Time.deltaTime real por invocacion

Check("Tras aterrizar y pasar DestroyAfterLandedSeconds, la torreta se destruye y se da de baja del registro",
    WorldSystemsRegistry.TurretWeapons.Count == turretCountAntes - 1 &&
    WorldSystemsRegistry.TurretAis.Count == aiCountAntes - 1);
```
Ojo: invocar `Update()` por reflection en Edit mode usa `Time.deltaTime` real entre llamadas sucesivas (que en Edit mode puede ser 0 o inconsistente) — si este `Check()` resulta poco confiable en la práctica, la alternativa más simple es un test en vivo: destruir un vehículo en Play mode (dispararle hasta 0 de vida), ver el cañón salir volando y aterrizar, esperar los 6 segundos configurados, y confirmar a mano (por ejemplo con un breakpoint o un log temporal) que `WorldSystemsRegistry.TurretWeapons.Count`/`TurretAis.Count` bajan en 1 cada vez.

**Riesgo/efectos secundarios:** Si `DestroyAfterLandedSeconds` queda muy corto, el cañón desaparece antes de que el jugador termine de mirarlo (rompe la lectura visual "ahí murió un tanque" a distancia, que es justamente el propósito documentado de `DetachTurret`); si queda muy largo, tarda en liberar memoria/CPU en partidas largas con muchos vehículos destruidos. 6s es un punto de partida razonable (más corto que los 8s de humo de `VehicleFxReactor`, para que la torreta no desaparezca ANTES que el humo del casco, que se ve peor: la carcasa sigue humeando pero el cañón ya no está). Ajustar a ojo en Play mode con varios vehículos destruidos seguidos.

---

### Bug 10: `CaptureStep` loguea "Captura: ..." como éxito aunque las 4 reintentos fallen y no se escriba ningún archivo
**Archivos:** `Demo/AutoDemoRunner.cs:116-141` (`CaptureStep`).

**Causa raíz:** El bucle de reintento (`for (int attempt = 0; attempt < 4; attempt++)`) puede terminar con `shot == null` si las 4 capturas dieron cuadro negro. El código de después del bucle (`if (shot != null) { File.WriteAllBytes(...); ... }`) SÍ está bien condicionado — no escribe archivo si `shot` es `null` — pero las líneas de después (`stepCounter++; TestLog.Step($"Captura: {fileName}");`) están FUERA de ese `if`, así que corren SIEMPRE, con el mismo mensaje de éxito, sin importar si el archivo realmente se escribió o no. No queda ningún rastro en el log de que esa captura en particular falló.

**Plan de implementación:**
1. Mover `stepCounter++` y el log a DENTRO de la rama de éxito, y agregar una rama explícita de fallo con `TestLog.Warn` (mismo método que ya usa el proyecto para fallas, por ejemplo en `Check()` de `HeadlessTestRunner`):
   ```csharp
   if (shot != null)
   {
       File.WriteAllBytes(path, shot.EncodeToPNG());
       UnityEngine.Object.Destroy(shot);
       stepCounter++;
       TestLog.Step($"Captura: {fileName}");
   }
   else
   {
       // BUG REAL: antes esto igual incrementaba stepCounter y logueaba
       // "Captura: ..." como si hubiera salido bien, aunque los 4
       // intentos hubieran dado cuadro negro y NO se hubiera escrito
       // ningun archivo -- no quedaba ningun rastro en el log de que
       // esa captura en particular fallo.
       stepCounter++;
       TestLog.Warn($"Captura FALLIDA tras 4 intentos (frame negro persistente, no se escribio archivo): {fileName}");
   }

   yield return new WaitForSecondsRealtime(stepGap);
   ```
2. Se mantiene `stepCounter++` en ambas ramas (no se salta el número), para que el índice de la siguiente captura no choque de nombre con esta — el archivo faltante simplemente deja un hueco en la numeración, que es preferible a que dos capturas distintas terminen con el mismo `stepCounter:00` en el nombre.
3. Opcional: si se quiere hacer más visible el fallo aún en un vistazo rápido del log completo, se puede llevar la cuenta de fallos totales en un campo (`int failedCaptures;`) y loguear un resumen al final de `DemoSequence()` (`TestLog.Warn($"{failedCaptures} capturas fallidas en total");`) — no imprescindible para el fix mínimo, pero coherente con el patrón que ya usa `HeadlessTestRunner.Check()` de contar fallas y no solo loguearlas una por una.

**Verificación:** Este bug depende del camino de fallo de `ScreenCapture.CaptureScreenshotAsTexture()` (cuadro negro), que es no determinístico según el propio comentario del código (depende de foco del Editor y del repintado del Game View) — no es razonablemente testeable con un `Check()` de Edit mode. Dos formas de probarlo en Play mode:
1. **Forzando el camino de fallo a mano:** comentar temporalmente (solo para la prueba) la línea `shot = ScreenCapture.CaptureScreenshotAsTexture();` reemplazándola por `shot = null;` dentro del `for`, correr la demo, y confirmar en consola que aparece `TestLog.Warn($"Captura FALLIDA...")` en vez de `TestLog.Step($"Captura: ...")`, y que efectivamente NO aparece el archivo `.png` correspondiente en `DemoCaptures/`. Revertir el cambio temporal después.
2. **Reproduciendo el fallo real:** correr la demo con el Editor SIN foco (clickeando otra ventana apenas arranca) durante una racha de pasos, y revisar `DemoCaptures/` al final contra el log — antes del fix, cualquier paso sin archivo real igual aparece como `TestLog.Step("Captura: ...")` en el log (falso positivo); después del fix, todo paso sin archivo real aparece como `TestLog.Warn` inconfundible.

**Riesgo/efectos secundarios:** Ninguno — es un cambio puramente de logging/contabilidad, no toca la lógica de reintento ni el contenido de las capturas exitosas. Único cuidado cosmético: si se agrega el contador opcional de fallos totales (punto 3), asegurarse de resetearlo en `DemoSequence()` junto con `stepCounter = 0;` al arrancar, para no arrastrar cuenta de una corrida anterior.

---

### Bug 11: `SwitchSeat` sin cooldown/guard contra re-entrada durante la animación de montaje
**Archivos:** `Player/PlayerInputDriver.cs:1491-1519` (`SwitchSeat`), invocado desde `UpdateInVehicle` cada frame que se detecta `[1]`/`[2]` (no se muestra el call site exacto en el fragmento leído, pero el propio comentario del rol en pantalla — línea 1483-1487 — confirma que `[1]`/`[2]` llaman a esto directo por tecla, sin ningún cooldown de por medio).

**Causa raíz:** Este es el factor habilitante del Bug 3: `SwitchSeat` no tiene ninguna guarda que impida llamarse de nuevo mientras el soldado todavía tiene una animación de montaje en curso desde la última vez que se llamó (o desde el montaje inicial). Nada en el método consulta si `soldier` ya está "ocupado" animando — simplemente hace `Vehicle.Dismount(soldier); soldier.gameObject.SetActive(false); Vehicle.Mount(soldier, newRole);` sin importar cuánto tiempo pasó desde la última vez.

**Plan de implementación:**
1. Este fix depende directamente de la infraestructura agregada en el Bug 3 (los diccionarios `mountAnimations`/`mountTrueScale` en `Vehicle`). Exponer una consulta pública mínima desde `Vehicle`:
   ```csharp
   // Usado por PlayerInputDriver.SwitchSeat para no arrancar un segundo
   // cambio de asiento mientras el soldado todavia esta a mitad de la
   // animacion de subida del cambio anterior (ver Bug 3: dos corutinas
   // de montaje corriendo a la vez sobre el mismo soldado le arruinaban
   // la escala para siempre).
   public bool IsMountAnimating(Soldier soldier) => mountAnimations.ContainsKey(soldier);
   ```
2. En `SwitchSeat`, agregar el guard como primera línea del método (línea 1491-1492, antes de tocar `Brain.Current`):
   ```csharp
   public void SwitchSeat(VehicleSeatRole newRole)
   {
       var soldier = Brain.Current;
       // BUG REAL (item 11, factor habilitante del item 3): sin este
       // guard, apretar [1]/[2] rapido dentro de los 0.35s de la
       // animacion de montaje arrancaba una SEGUNDA corutina de subida
       // sobre el mismo soldado -- la que terminaba ultima le pisaba la
       // escala real con una escala intermedia ya achicada, dejandolo
       // mal escalado para siempre.
       if (soldier != null && Vehicle.IsMountAnimating(soldier)) return;

       var vb = Vehicle.GetComponent<VehicleBrain>();
       ...
   }
   ```
3. Como el guard consulta el estado real de la animación (no un cooldown por tiempo fijo), el jugador puede volver a cambiar de asiento apenas la animación de 0.35s termina — no hace falta un valor de cooldown a mano ni riesgo de "se siente trabado" si se elige mal un número.
4. Confirmar que el guard no bloquea el PRIMER `SwitchSeat` tras subir al vehículo (`EnterVehicle`) — como `mountAnimations` solo tiene entrada mientras la corutina de montaje sigue corriendo, y el montaje inicial (`Vehicle.Mount` desde `EnterVehicle`) también pasa por el mismo `mountAnimations[soldier] = StartCoroutine(...)`, el guard SÍ bloquearía un cambio de asiento pedido en el primer 0.35s tras subir — que es exactamente el comportamiento deseado (no se puede cambiar de asiento hasta terminar de "sentarse").

**Verificación:** Play mode real (el guard depende de `Vehicle.mountAnimations`, que solo existe con `Application.isPlaying`). Secuencia:
1. Montar a Vega de conductor y, dentro de los primeros 0.2s, apretar `[2]` (o llamar `inputDriver.SwitchSeat(VehicleSeatRole.Gunner)`) dos o tres veces seguidas en frames consecutivos.
2. **Antes del fix:** cada tecla dispara un `SwitchSeat` nuevo sin condición, arrancando una corutina de montaje adicional cada vez — reproduce el Bug 3 tal cual.
3. **Después del fix:** solo el PRIMER intento (o ninguno, si cae dentro de la ventana del montaje inicial) surte efecto; los llamados repetidos mientras `Vehicle.IsMountAnimating(vega)` es `true` no hacen nada — confirmar con un log temporal dentro de `SwitchSeat` mostrando cuántas veces se ejecuta de verdad el cuerpo del método contra cuántas veces se llamó.
4. Esperar a que termine la animación en curso (>0.35s) y confirmar que un `SwitchSeat` posterior SÍ funciona con normalidad (el guard no queda pegado en `true` para siempre).
5. Repetir el mismo test de escala del Bug 3 (comparar `localScale` final contra la original) para confirmar que, con este guard puesto, ya ni siquiera hace falta llegar al camino de "restaurar desde `mountTrueScale`" del Bug 3 — directamente nunca se llega a crear la segunda corutina.

**Riesgo/efectos secundarios:** Si en algún momento se agrega una forma de forzar un cambio de asiento por código (no por tecla del jugador — por ejemplo alguna orden de IA o un evento de guion), ese camino también quedaría sujeto al mismo guard, lo cual es correcto (no se quiere una segunda corutina arrancando tampoco en esos casos) pero conviene tenerlo presente si se agrega lógica nueva que dependa de que `SwitchSeat` sea síncrono/inmediato. También revisar el mensaje de instrucciones en pantalla (línea 1483-1487, "[2] ir a la torreta") para que no quede la sensación de que la tecla "no respondió" — como la ventana de bloqueo es de apenas 0.35s, en la práctica debería ser imperceptible para un jugador humano tecleando a velocidad normal.


---

# Presentation (VFX, audio, UI de mundo) — Planes de corrección (12 bugs + 1 nota cruzada)

### Bug 1: `DecalPool.Spawn()` no purga entradas fake-null antes de indexar
**Archivos:** `Presentation/DecalPool.cs:51-111` (método `Spawn`, en particular las líneas 61-71 donde se lee `list` y se indexa `list[0]`)

**Causa raíz:** `Spawn()` obtiene `list` del diccionario `pools[kind]` y, si `list.Count >= Budget(kind)`, indexa directamente `list[0]` para reciclarlo. Pero, tal como el propio comentario de `DebrisPool.Spawn()` (líneas 115-121) y `ImpactFxPool.Purge()` (líneas 507-515) documentan en este mismo proyecto, una reconstrucción de escena destruye los `GameObject` sin vaciar las listas estáticas que los indexan: quedan referencias "fake-null" de Unity (pasan `x == null` con trampa, pero explotan al tocar `transform`/`GetComponent`). `DecalPool.Spawn()` es la única de las tres pools de FX (`DebrisPool`, `ImpactFxPool`, `DecalPool`) que NO hace `list.RemoveAll(x => x == null)` antes de operar sobre `list`, a pesar de tener el comentario que explica por qué hace falta.

**Plan de implementación:**
1. En `DecalPool.Spawn()`, inmediatamente después de resolver `list` (después de la línea `if (!pools.TryGetValue(kind, out var list)) { list = new List<GameObject>(); pools[kind] = list; }`), agregar la purga:
   ```csharp
   // Misma purga que DebrisPool.Spawn/ImpactFxPool.Take: una
   // reconstruccion de escena destruye los GameObjects pero no vacia
   // esta lista (es estatica), y quedan entradas "fake-null" que pasan
   // el chequeo == null de C# pero explotan al indexarlas.
   list.RemoveAll(x => x == null);
   ```
2. Esto debe ir **antes** de la comparación `if (list.Count >= Budget(kind))`, para que el conteo contra el cupo sea el conteo real (si no, un cupo "lleno" de entradas muertas nunca deja crear una pieza nueva).
3. No hace falta tocar `ResetIfStale()` ni `DestroyOrphans()`: esas ya cubren el caso de domain-reload completo (estáticos reiniciados); el caso nuevo a cubrir es la reconstrucción de escena en caliente donde `root` sigue vivo pero algunos hijos fueron destruidos por otro camino (por ejemplo, al recargar un chunk de escena o al hacer `DestroyImmediate` manual de un decal).

**Verificación:** Agregar un `Check()` en `RunPhase5` (sección "regresión de sistemas nuevos") de `Editor/HeadlessTestRunner.cs`, junto a los demás checks de pools/FX:
```csharp
var leaked = SP.Presentation.DecalPool.Spawn(SP.Presentation.DecalKind.BulletHole, Vector3.zero, Vector3.up, 1f);
UnityEngine.Object.DestroyImmediate(leaked); // simula el GO destruido por fuera del pool
bool threwOnRecycle = false;
try
{
    for (int i = 0; i < SP.Presentation.DecalPool.BulletHoleBudget + 2; i++)
        SP.Presentation.DecalPool.Spawn(SP.Presentation.DecalKind.BulletHole, Vector3.zero, Vector3.up, 1f);
}
catch (System.Exception) { threwOnRecycle = true; }
Check("DecalPool.Spawn purga entradas fake-null antes de reciclar (no explota)", !threwOnRecycle);
```
Sin el fix, el bucle debería lanzar `MissingReferenceException` al llegar al índice reciclado que apunta al objeto destruido.

**Riesgo/efectos secundarios:** Ninguno relevante: `RemoveAll` es O(n) sobre listas de a lo sumo 24/48 elementos, ejecutado solo al spawnear un decal (no por frame). Verificar que ningún otro lugar del código guarda una referencia externa a un `GameObject` de decal esperando que siga en `list` en un índice fijo (no lo hace: la API pública es solo `Spawn`).

---

### Bug 2: `AttackLineManager` filtra un `Material` por ciclo de enganche/desenganche
**Archivos:** `Presentation/AttackLineManager.cs:48-66` (métodos `RemoveLine` y `CreateLine`)

**Causa raíz:** `CreateLine()` (línea 62) crea un material NO compartido por línea vía `SafeMaterial.Create(LineColor)` (una instancia nueva de `Material`, no un asset). `RemoveLine()` (líneas 48-53) sólo hace `Destroy(lr.gameObject)`; nunca destruye `lr.material`. Como `LineRenderer.material` ya es la instancia propia asignada en `CreateLine` (no se vuelve a clonar al leerla porque ya es una instancia), cada ciclo "un soldado entra en `AiState.Attack` y sale" (línea 31-35, disparado desde `Update()` en cada soldado que deja de atacar) crea un material que jamás se libera hasta salir de Play mode.

**Plan de implementación:**
1. Reescribir `RemoveLine()` siguiendo el mismo patrón ya establecido en `VehicleFxReactor.VehicleSmokePuff.Drift()` (líneas 216-231 de `VehicleFxReactor.cs`), que captura el material antes de destruir el `GameObject` y lo destruye aparte:
   ```csharp
   void RemoveLine(int actorId)
   {
       if (!lines.TryGetValue(actorId, out var lr)) return;
       lines.Remove(actorId);
       if (lr == null) return;

       // CreateLine() le asigna una instancia PROPIA de Material (no un
       // asset compartido) via SafeMaterial.Create: Destroy(gameObject)
       // no la libera sola, queda huerfana en memoria hasta salir de
       // Play. Mismo patron de limpieza que VehicleSmokePuff.Drift.
       var mat = lr.material;
       if (Application.isPlaying)
       {
           if (mat != null) Destroy(mat);
           Destroy(lr.gameObject);
       }
       else
       {
           if (mat != null) DestroyImmediate(mat);
           DestroyImmediate(lr.gameObject);
       }
   }
   ```
2. Ojo con el orden: `mat` se lee de `lr.material` **antes** de destruir el `gameObject` (después de destruido, el acceso podría fallar o devolver null).
3. Extra (misma clase de fuga, alcance menor porque corre una sola vez): `Prewarm()` (líneas 72-80) también crea una línea con `CreateLine()` y sólo destruye el `gameObject`, dejando un material huérfano de arranque. Aplicar el mismo patrón ahí:
   ```csharp
   public static void Prewarm()
   {
       var lr = CreateLine();
       lr.transform.position = new Vector3(0f, -500f, 0f);
       lr.SetPosition(0, lr.transform.position);
       lr.SetPosition(1, lr.transform.position + Vector3.right * 0.01f);
       var mat = lr.material;
       if (Application.isPlaying) { if (mat != null) Destroy(mat); Object.Destroy(lr.gameObject); }
       else { if (mat != null) DestroyImmediate(mat); Object.DestroyImmediate(lr.gameObject); }
   }
   ```

**Verificación:** No es fácil contar materiales vivos desde `Editor/HeadlessTestRunner.cs` sin acceso a `Resources.FindObjectsOfTypeAll<Material>()` (que sí está disponible en Editor). Agregar un `Check()` en `RunPhase5`:
```csharp
int matsBefore = Resources.FindObjectsOfTypeAll<Material>().Length;
var alm = servicesGO.GetComponent<SP.Presentation.AttackLineManager>(); // ya está en la escena (linea 679)
// Forzar 5 ciclos crear/eliminar via reflection sobre CreateLine/RemoveLine
// privados, o -- mas simple -- via el propio Update() manipulando el
// estado Attack de un soldado real y llamando Update() a mano varias
// veces (SimStep ya tickea AiBrain). Alternativa mas directa: invocar
// CreateLine()+RemoveLine() por reflection 5 veces.
var createLine = typeof(SP.Presentation.AttackLineManager).GetMethod("CreateLine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
var removeLine = typeof(SP.Presentation.AttackLineManager).GetMethod("RemoveLine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
for (int i = 0; i < 5; i++)
{
    var lr = (LineRenderer)createLine.Invoke(null, null);
    alm.GetType().GetField("lines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    // mas simple: usar directamente lines[actorId] = lr; y luego RemoveLine(actorId)
}
```
Dado que manipular el diccionario privado `lines` por reflection es frágil, la verificación más confiable es un **test manual en Play mode**: entrar en Play, hacer que un soldado ataque y deje de atacar 10-20 veces seguidas (mover al enemigo dentro/fuera de rango), abrir el Profiler → Memory → "Material Count" (o `Resources.FindObjectsOfTypeAll<Material>().Length` desde la consola), y confirmar que el conteo de materiales NO crece de forma sostenida con cada ciclo (debe estabilizarse, no subir 1 por ciclo).

**Riesgo/efectos secundarios:** Verificar que ningún otro código guarda una referencia externa a `lr.material` de una `AttackLine` esperando que siga viva después de que el soldado deja de atacar (no la hay: la única referencia vive en el diccionario `lines` de esta misma clase). Confirmar que `Destroy(mat)` no se llama dos veces sobre el mismo material si `RemoveLine` se invocara dos veces para el mismo `actorId` (no puede pasar: la primera llamada ya hizo `lines.Remove(actorId)`, así que una segunda llamada sale por el `if (!lines.TryGetValue(...)) return;`).

---

### Bug 3: `CubeFxReactor.OnDamage()` reacciona sobre cadáveres (sin chequeo de vida)
**Archivos:** `Presentation/CubeFxReactor.cs:96-123` (método `OnDamage`)

**Causa raíz:** `OnDamage()` sólo valida `Application.isPlaying`, `IsMe(evt.TargetId)` y `gameObject.activeInHierarchy` (línea 98); no valida si el soldado sigue vivo. Si una `DamageTakenEvent` para ese mismo actor llega **después** de su `EntityDiedEvent** (overkill de un AoE, o daño resuelto en el mismo frame que la muerte pero procesado después en el bus), `OnDamage()` ejecuta `StopAllCoroutines()` (línea 121) — que corta `FallOver()` a mitad del `Quaternion.Slerp` (líneas 195-209 de `CubeFxReactor.cs`, dejando el cadáver congelado en una rotación intermedia — y arranca `FlashAndPunch()` (línea 122), que vuelve a pintar de blanco (línea 179-180) y escalar (línea 181) un cuerpo que ya debería estar quieto y en su color final.

**Plan de implementación:**
1. Agregar el chequeo de vida al comienzo de `OnDamage()`, en la misma línea de guardas ya existente, usando `soldier.Health.IsAlive` (la misma propiedad que ya usa `WorldUiDirector.RebuildFogObservers` en `WorldUiDirector.cs:259` y `PossessedMarkerView.Tick()` en `PossessedMarkerView.cs:91`):
   ```csharp
   void OnDamage(DamageTakenEvent evt)
   {
       if (!Application.isPlaying || !IsMe(evt.TargetId) || !gameObject.activeInHierarchy) return;
       // Un DamageTakenEvent puede llegar DESPUES del EntityDiedEvent del
       // mismo actor (overkill/AoE resuelto post-mortem): sin este
       // chequeo, StopAllCoroutines() cortaba FallOver() a mitad del
       // slerp (cadaver congelado a mitad de caida) y volvia a pintar el
       // flash blanco sobre un cuerpo que ya deberia estar quieto.
       if (soldier == null || soldier.Health == null || !soldier.Health.IsAlive) return;

       AudioDirector.PlayAt(SfxKind.Wounded, transform.position, 0.7f, 0.65f);
       StopAllCoroutines();
       StartCoroutine(FlashAndPunch());
   }
   ```
2. No tocar `OnDeath()`: ese método ya hace su propia restauración de color/escala antes de `FallOver()` (líneas 133-140) precisamente para el caso simétrico (muerte llega en medio del flash de daño), así que sigue siendo necesario y correcto tal cual está.
3. No hace falta un chequeo de orden de eventos más sofisticado (timestamps, etc.): el chequeo de `IsAlive` alcanza porque `OnDeath()` ya deja al soldado marcado como muerto en el `Health` antes de publicar el evento (contrato ya usado por el resto del proyecto).

**Verificación:** Difícil de cubrir 100% en la suite headless de Edit mode porque `StartCoroutine` no avanza sin Play mode real (el propio archivo lo documenta en el comentario de la línea 1308-1312 de `HeadlessTestRunner.cs`, que reserva estos casos para un `RunPlayModeProbe()` — hoy solo mencionado en un comentario, no implementado). Camino recomendado:
- **Test en Play mode real** (secuencia manual): poseer o mirar a un enemigo con vida baja; forzar dos golpes en el mismo frame lógico donde el segundo mata y el primero (o un tercero en la misma ráfaga) llega inmediatamente después del `EntityDiedEvent` — por ejemplo con un arma de AoE que hace `TakeDamage` a varios actores en el mismo frame y uno de ellos muere con el primer impacto. Observar: el cadáver debe quedar tumbado (rotación final ~90° en X) sin quedar "flasheado" blanco ni a mitad de caída.
- Alternativa headless parcial: en `RunPhase5`, después de matar a un soldado con `vega.Health.TakeDamage(999999, -1)` (patrón ya usado en la línea 1275), publicar manualmente un `DamageTakenEvent` adicional para ese mismo `TargetId` vía `EventBus.Instance.Publish(...)` y comprobar (via reflection sobre el campo privado `bootstrapped`/`soldier`, o exponiendo un método de test) que **no** se llamó a `StartCoroutine` — esto requiere instrumentar el reactor con un contador de test (`public int FlashStartedCount` incrementado dentro de `OnDamage` antes del `StartCoroutine`) para poder verificarlo sin Play mode.

**Riesgo/efectos secundarios:** Confirmar que `soldier.Health` nunca es null en el flujo normal (ya se asume en otros lugares del proyecto, p. ej. `WorldUiDirector`). Si algún día se agrega "revivir" a un soldado, este guard seguiría siendo correcto (un soldado revivido vuelve a tener `IsAlive == true` y el flash volvería a funcionar).

---

### Bug 4: `FloatingDamageTextManager.GetFromPool()` no tiene cupo duro
**Archivos:** `Presentation/FloatingDamageTextManager.cs:58-95` (método `GetFromPool`), y `OnDamage`/`RiseAndFade` (líneas 97-149) para el tracking de antigüedad.

**Causa raíz:** `GetFromPool()` recorre `pool` buscando un elemento libre (Canvas inactivo); si no encuentra ninguno, crea uno nuevo sin límite (líneas 63-94) y lo agrega a `pool` (línea 93). A diferencia de **todas** las demás pools del directorio (`DebrisPool.Budget = 64`, `DecalPool.CraterBudget/BulletHoleBudget`, `ImpactFx.SphereBudget/RingBudget`, `MuzzleLightPool.Budget = 6`, `OrderMarkerFx` — todas con cupo fijo y reciclado del más viejo), este manager no tiene ni `const int Budget` ni lógica de "robar el más viejo en uso". Bajo daño de área sostenido (muchos objetivos golpeados a la vez, cada uno con su propio texto sin fusionar porque son actores distintos) la lista `pool` crece sin tope, generando un `Canvas`+`Text`+`Outline` nuevo por objetivo distinto golpeado por primera vez en la ventana de fusión.

**Plan de implementación:**
1. Agregar el cupo y una lista de orden de uso (mismo criterio de "más viejo primero" que `DebrisPool.inUse`/`ImpactFxPool.inUse`), pero indexada por `targetId` porque acá lo que se reutiliza es la entrada activa, no un componente físico:
   ```csharp
   // Mismo criterio de cupo duro que DebrisPool/DecalPool/ImpactFx/
   // MuzzleLightPool: sin esto, un AoE sostenido contra muchos objetivos
   // distintos genera un Canvas+Text+Outline por objetivo sin limite.
   public const int Budget = 32;

   readonly List<Text> pool = new List<Text>();
   // Orden de aparicion de las entradas ACTIVAS (mas vieja primero), para
   // poder reciclar cuando el cupo esta lleno. Separado de 'pool' porque
   // ahi lo que hay que robar es la entrada de OnDamage, no el
   // componente Text en si.
   readonly List<int> activeOrder = new List<int>();
   readonly Dictionary<int, Entry> activeByTarget = new Dictionary<int, Entry>();
   ```
2. Modificar `GetFromPool()` para forzar el cupo:
   ```csharp
   Text GetFromPool()
   {
       foreach (var t in pool)
           if (t != null && !t.transform.parent.gameObject.activeSelf) return t;

       if (pool.Count >= Budget)
       {
           // Cupo agotado: se recicla la entrada activa MAS VIEJA (frente
           // de activeOrder), igual que DebrisPool recicla inUse[0]. Sin
           // esto la lista crecia sin limite bajo daño de area sostenido.
           if (activeOrder.Count == 0) return null; // red de seguridad
           int oldestTargetId = activeOrder[0];
           activeOrder.RemoveAt(0);
           if (!activeByTarget.TryGetValue(oldestTargetId, out var oldEntry)) return null;
           if (oldEntry.Routine != null) StopCoroutine(oldEntry.Routine);
           activeByTarget.Remove(oldestTargetId);
           oldEntry.Text.transform.parent.gameObject.SetActive(false);
           return oldEntry.Text;
       }

       var canvasGO = new GameObject("FloatingDamageText", typeof(Canvas));
       // ... (resto del método sin cambios)
   }
   ```
3. En `OnDamage()`, después de crear `newEntry` y antes de (o junto con) `activeByTarget[evt.TargetId] = newEntry;`, agregar el id a `activeOrder`:
   ```csharp
   activeByTarget[evt.TargetId] = newEntry;
   activeOrder.Add(evt.TargetId);
   newEntry.Routine = StartCoroutine(RiseAndFade(newEntry, evt.TargetId, target.transform));
   ```
4. En `RiseAndFade()`, al terminar normalmente (líneas 145-148), sacar también de `activeOrder` para no dejar ids fantasma:
   ```csharp
   entry.Text.transform.parent.gameObject.SetActive(false);
   entry.Text.color = new Color(color.r, color.g, color.b, 1f);
   if (activeByTarget.TryGetValue(targetId, out var current) && current == entry)
   {
       activeByTarget.Remove(targetId);
       activeOrder.Remove(targetId);
   }
   ```
5. El valor `32` es una elección razonable (misma escala que `ImpactFx.SphereBudget = 48`, mayor que la cantidad de soldados de un escuadrón típico del proyecto); ajustar si el balance de combate real lo requiere, pero el punto crítico del fix es que exista ALGÚN tope duro, no el valor exacto.

**Verificación:** Agregar un `Check()` en `RunPhase5` de `HeadlessTestRunner.cs`:
```csharp
var fdtm = servicesGO.GetComponent<SP.Presentation.FloatingDamageTextManager>(); // linea 681
for (int i = 0; i < SP.Presentation.FloatingDamageTextManager.Budget + 10; i++)
{
    // 40 objetivos DISTINTOS para evitar la fusion de OnDamage (misma
    // ventana de merge) y forzar 40 textos "nuevos" pedidos al pool.
    EventBus.Instance.Publish(new DamageTakenEvent { TargetId = 9000 + i, Amount = 5 });
}
var poolField = typeof(SP.Presentation.FloatingDamageTextManager).GetField("pool", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var poolList = (System.Collections.IList)poolField.GetValue(fdtm);
Check($"FloatingDamageTextManager.pool no supera el cupo ({poolList.Count} <= {SP.Presentation.FloatingDamageTextManager.Budget})",
    poolList.Count <= SP.Presentation.FloatingDamageTextManager.Budget);
```
(Nota: `ActorRegistry.FindById` en `OnDamage` debe devolver algo no-null para cada `TargetId` de prueba, o el evento se descarta en la línea 101 antes de tocar el pool — conviene generar los ids de prueba a partir de soldados reales ya spawneados, reutilizando pocos `Soldier` con `TargetId` distintos simulados, o extender el test para registrar actores dummy en `ActorRegistry` primero.)

**Riesgo/efectos secundarios:** Si `Budget` queda demasiado bajo, un combate masivo legítimo empezaría a "robar" texto a objetivos golpeados hace apenas un instante, lo cual es un trade-off aceptado (igual que el resto de las pools del proyecto: "se descarta/recicla en silencio, es la funcionalidad, no una falla"). Verificar que `StopCoroutine` sobre una entrada robada no deje el `Text` con alpha parcial (la línea `oldEntry.Text.transform.parent.gameObject.SetActive(false)` ya lo oculta, pero el próximo `OnDamage` que reutilice ese `Text` sobreescribe `text.color` con alpha 1 en la línea 116, así que no hace falta resetear el alpha a mano).

---

### Bug 5: `AudioDirector.DistanceToListener()` devuelve 0 (máxima audibilidad) sin listener
**Archivos:** `Presentation/AudioDirector.cs:480-503` (métodos `DistanceToListener` y `ResolveListener`)

**Causa raíz:** `DistanceToListener()` (línea 480-484) hace `return tf != null ? Vector3.Distance(...) : 0f;`. Cuando `ResolveListener()` todavía no encontró ni `Camera.main` ni ningún `AudioListener` (primeros frames antes de que la cámara exista, caso real en el arranque de escena de este proyecto), el fallback es `0f`, que es la distancia de **máxima** audibilidad (`Attenuation(0f) == 1f`, línea 122). Un sonido en verdad lejano que se reproduce en esos primeros frames gana injustamente cualquier contienda de robo de voz (`SelectVictim`) contra sonidos genuinamente audibles, porque el cálculo de `audibility` en `PlayClip` (línea 387) lo trata como si estuviera pegado al oyente.

**Plan de implementación:**
1. Extraer la lógica de distancia-o-desconocido a una función estática pura y testeable (mismo espíritu que `Attenuation`/`CutoffFor`/`SelectVictim`, que ya son estáticas y puras justamente para poder verificarlas en Edit mode sin escena real, según el propio comentario de `VoiceState` en la línea 16-18):
   ```csharp
   // Funcion pura y testeable: separada de ResolveListener (que SI
   // depende de escena/Camera.main) para poder verificarla en Edit mode.
   public static float DistanceOrUnknown(Transform listener, Vector3 position) =>
       listener != null ? Vector3.Distance(listener.position, position) : float.MaxValue;
   ```
2. Cambiar `DistanceToListener()` para usarla:
   ```csharp
   float DistanceToListener(Vector3 position) => DistanceOrUnknown(ResolveListener(), position);
   ```
3. El fallback pasa a ser `float.MaxValue` en vez de `0f`. `Attenuation(float.MaxValue)` entra por el branch `if (distance >= MaxDistance) return 0f;` (línea 123) sin overflow ni `NaN` (es una comparación simple), así que un sonido sin listener resuelto termina con `audibility <= 0f` y se descarta en silencio por el guard ya existente de la línea 388 (`if (audibility <= 0f) return false;`) — el mismo comportamiento de "se descarta, no se escucha nada" que YA es correcto cuando no hay ningún `AudioListener` habilitado en la escena (no se puede escuchar nada de verdad todavía).
4. No hace falta cambiar `ResolveListener()`: su comportamiento de cachear `listenerTf` y reintentarlo solo cuando sigue null ya es correcto; el problema estaba exclusivamente en qué valor de distancia se devolvía mientras tanto.

**Verificación:** Agregar en `RunPhase5` de `HeadlessTestRunner.cs`, junto a los demás checks de `AudioDirector` (cerca de la línea 1138-1160, sección "Attenuation / CutoffFor"):
```csharp
// --- AudioDirector.DistanceOrUnknown: sin listener no debe ganar la
// contienda de audibilidad ---
float distSinListener = SP.Presentation.AudioDirector.DistanceOrUnknown(null, Vector3.zero);
Check("DistanceOrUnknown sin listener NO devuelve la distancia de maxima audibilidad (0)",
    distSinListener > SP.Presentation.AudioDirector.MaxDistance);
Check("Sin listener, la atenuacion resultante es 0 (el sonido se descarta, no gana un robo de voz)",
    Mathf.Approximately(SP.Presentation.AudioDirector.Attenuation(distSinListener), 0f));

var dummyListenerGO = new GameObject("DummyListenerForTest");
dummyListenerGO.transform.position = new Vector3(3f, 0f, 0f);
float distConListener = SP.Presentation.AudioDirector.DistanceOrUnknown(dummyListenerGO.transform, Vector3.zero);
Check($"Con listener resuelto, la distancia es real ({distConListener:0.0} ~= 3.0)", Mathf.Abs(distConListener - 3f) < 0.01f);
UnityEngine.Object.DestroyImmediate(dummyListenerGO);
```

**Riesgo/efectos secundarios:** Ningún caller externo depende de que `DistanceToListener`/`PlayClip` devuelva `0f` como "sonido siempre audible" en ausencia de listener (sería un comportamiento indeseado, no una feature). Confirmar que ningún test o sistema llama `PlayClip`/`Play` en el primer frame de arranque ANTES de que `AudioDirector.OnEnable()` corra `EnsureVoices()` esperando que el sonido efectivamente suene: con este fix, cualquier intento de sonido posicional antes de que exista cámara/listener simplemente no se reproduce (que es el comportamiento correcto), en vez de sonar a todo volumen y robarle la voz a algo real.

---

### Bug 6: `PostFxDirector.EnsureBuilt()` reconstruye el perfil entero tras un recompile en Play
**Archivos:** `Presentation/PostFxDirector.cs:57-91` (método `EnsureBuilt`), campo `holder`/creación de `PostFxVolume` en las líneas 63-69.

**Causa raíz:** El guard rápido de `EnsureBuilt()` es `if (volume != null && profile != null) return;` (línea 59). `volume` y `profile` son campos de instancia **no serializados**; tras un recompile de scripts con domain reload en Play mode, ambos se resetean a `null` en memoria administrada, pero el `GameObject` hijo `"PostFxVolume"` (con su `Volume` component y su `VolumeProfile` asset-en-memoria) sigue vivo en la escena. `EnsureBuilt()` vuelve a encontrar el `holder` vía `transform.Find` (línea 63) y su `Volume` existente (línea 71), pero como chequea el campo `profile` (que SÍ perdió su referencia) en vez de `volume.profile` (la referencia serializada por Unity, que sobrevive al reload), toma el camino de "no está construido" y ejecuta TODO el bloque de construcción de nuevo (líneas 78-90): crea un `VolumeProfile` nuevo, llama `NeutralizeTemplateLook()` y agrega 10 overrides nuevos, dejando huérfanos los 10 anteriores y reseteando a cero el estado visual en curso (aberración por daño, blur de velocidad). Adicionalmente, el `GameObject "PostFxVolume"` se crea sin `HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild` (línea 66-69), a diferencia de **todos** los demás spawners del directorio (`DebrisPool`, `DecalPool`, `ImpactFx`, `MuzzleLightPool`), por lo que puede quedar horneado en la escena guardada.

**Plan de implementación:**
1. Cambiar el guard y la lógica de re-adquisición para basarse en `volume.profile` (la referencia serializada), y **recuperar** (no recrear) los overrides existentes con `TryGet` en vez de `Add` (que crearía duplicados):
   ```csharp
   void EnsureBuilt()
   {
       var holder = transform.Find("PostFxVolume");
       if (holder == null)
       {
           var go = new GameObject("PostFxVolume");
           go.transform.SetParent(transform, false);
           // Sin esto (ver DebrisPool/DecalPool/ImpactFx/MuzzleLightPool)
           // el objeto queda serializado en la escena/build y convive con
           // el que este director vuelve a crear en el proximo Play mode.
           go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
           holder = go.transform;
       }

       volume = holder.GetComponent<Volume>();
       if (volume == null) volume = holder.gameObject.AddComponent<Volume>();
       volume.isGlobal = true;
       volume.priority = 1000f;

       // BUG ORIGINAL: comprobar el campo 'profile' (no serializado, se
       // pierde en cada domain reload) en vez de volume.profile (la
       // referencia SERIALIZADA que Unity restaura sola) hacia que este
       // chequeo fallara aunque el Volume y su perfil siguieran vivos, y
       // reconstruia TODO de cero -- 10 overrides nuevos, huerfanos los
       // viejos, y el estado visual (aberracion por daño, blur de
       // velocidad) volvia a cero en cada recompile durante Play mode.
       if (volume.profile != null)
       {
           profile = volume.profile;
           // Recupera las referencias existentes: profile.Add<T> crearia
           // un override DUPLICADO si se llamara aca en vez de TryGet.
           profile.TryGet(out aberration);
           profile.TryGet(out motionBlur);
           return;
       }

       profile = ScriptableObject.CreateInstance<VolumeProfile>();
       profile.name = "SP_RuntimePostFx";
       volume.profile = profile;

       NeutralizeTemplateLook();

       aberration = profile.Add<ChromaticAberration>(true);
       aberration.intensity.Override(0f);

       motionBlur = profile.Add<MotionBlur>(true);
       motionBlur.intensity.Override(0f);

       ApplyWeight();
   }
   ```
2. El `HideFlags` del `holder` recién creado ya queda cubierto en el bloque de arriba (`go.hideFlags = ...` agregado dentro del `if (holder == null)`).

**Verificación:** Agregar un `Check()` en `RunPhase5` (o en la sección donde ya se prueban vehículos/cámara, cerca de donde se usa `postFxDirectorRef`/`PostFxDirector.Instance`) que simule el domain-reload sin necesidad de recompilar de verdad:
```csharp
var postFx = SP.Presentation.PostFxDirector.Instance; // ya agregado a servicesGO (linea 682) y habilitado
if (postFx != null)
{
    postFx.PulseDamageAberration(0.7f);
    // Simula el efecto del domain reload: null-ea los campos NO
    // serializados 'profile', 'aberration', 'motionBlur' via reflection,
    // dejando 'volume' (serializado) intacto -- exactamente lo que pasa
    // de verdad tras un recompile en Play mode.
    var t = typeof(SP.Presentation.PostFxDirector);
    var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
    var volumeField = t.GetField("volume", flags);
    var volumeBefore = volumeField.GetValue(postFx);
    var profileField = t.GetField("profile", flags);
    var profileAssetBefore = ((UnityEngine.Rendering.VolumeProfile)profileField.GetValue(postFx));
    int overridesBefore = profileAssetBefore.components.Count;
    profileField.SetValue(postFx, null);
    t.GetField("aberration", flags).SetValue(postFx, null);
    t.GetField("motionBlur", flags).SetValue(postFx, null);

    // Reinvoca EnsureBuilt (privado) como lo haria OnEnable tras el reload.
    t.GetMethod("EnsureBuilt", flags).Invoke(postFx, null);

    var volumeAfter = volumeField.GetValue(postFx);
    var profileAfter = (UnityEngine.Rendering.VolumeProfile)profileField.GetValue(postFx);
    Check("PostFxDirector: tras 'domain reload' simulado, el Volume sigue siendo el MISMO objeto (no se creo uno nuevo)",
        ReferenceEquals(volumeBefore, volumeAfter));
    Check("PostFxDirector: el VolumeProfile recuperado es EL MISMO asset (no se reconstruyo desde cero)",
        ReferenceEquals(profileAssetBefore, profileAfter));
    Check($"PostFxDirector: el numero de overrides del perfil no crecio ({profileAfter.components.Count} == {overridesBefore}, sin duplicados)",
        profileAfter.components.Count == overridesBefore);
    Check("PostFxDirector: el estado visual en curso (aberracion por daño) NO se reseteo a 0",
        postFx.AberrationIntensity > 0f || postFx.AberrationIntensity < 0f); // sigue habiendo un valor > 0 tras el Update siguiente
}
```
(El último check puede necesitar un `postFx.Update()`-equivalente manual si `Update()` no corrió aún en el frame de test; alternativamente, verificar directamente el campo privado `damageAberration` via reflection, que `PulseDamageAberration` sube a `0.7f` y que el fix no debe resetear.)

**Riesgo/efectos secundarios:** Verificar que `profile.TryGet<T>(out x)` nunca devuelve `false` para `ChromaticAberration`/`MotionBlur` en el camino de "perfil ya construido" (no debería, porque siempre se agregaron ambos en la construcción original) — si algún día se quita uno de los dos overrides del perfil por otro camino, este `TryGet` fallaría silenciosamente dejando `aberration`/`motionBlur` en `null`, y el `Update()` (línea 130: `if (aberration == null || motionBlur == null) return;`) simplemente dejaría de aplicar el efecto sin explotar — comportamiento aceptable pero vale la pena un comentario aclaratorio en el código.

---

### Bug 7: `ObstacleMarker.ApplyStageLook()` mutando el `Material` compartido
**Archivos:** `Presentation/ObstacleMarker.cs:86` (dentro de `ApplyStageLook`)

**Causa raíz:** `rend.sharedMaterial.color = Color.Lerp(baseColor, Color.black, darken);` escribe directamente sobre el asset de `Material` referenciado por `sharedMaterial`. Cuando varios `ObstacleMarker` (cubos de cobertura repetidos en el nivel, caso común) apuntan al mismo asset de material, dañar a UNO de ellos oscurece visualmente a TODOS los que comparten ese material, porque no hay ninguna instancia por-objeto.

**Plan de implementación:**
1. Este proyecto ya resuelve exactamente este problema para el tinte del soldado en `CubeFxReactor.WriteTint(Renderer r, Color c)` (`CubeFxReactor.cs:152-162`): un `MaterialPropertyBlock` estático que escribe `_BaseColor`/`_Color` **por renderer**, sin tocar el asset compartido y sin romper el batching. Ese método ya es `public static`, así que la corrección más chica y más alineada con la convención del proyecto es reusarlo en vez de instanciar un material nuevo por obstáculo (que abriría un problema distinto: gestión/leak de esa instancia, igual que el Bug 2/12):
   ```csharp
   void ApplyStageLook(int stage)
   {
       if (rend == null) return;
       float darken = stage * 0.22f;
       // Antes: rend.sharedMaterial.color = ... mutaba el ASSET
       // compartido; varios obstaculos con el mismo material (cobertura
       // repetida) se oscurecian juntos aunque solo uno recibiera daño.
       // CubeFxReactor.WriteTint ya resuelve esto mismo para el tinte del
       // soldado via MaterialPropertyBlock (por-renderer, no rompe
       // batching); se reusa aca en vez de duplicar la logica o crear una
       // instancia de material por obstaculo.
       CubeFxReactor.WriteTint(rend, Color.Lerp(baseColor, Color.black, darken));

       float squash = 1f - stage * 0.12f;
       transform.localScale = new Vector3(baseScale.x, baseScale.y * squash, baseScale.z);
       transform.position = new Vector3(transform.position.x, baseScale.y * squash * 0.5f, transform.position.z);

       SpawnDebris(6, 4f);
   }
   ```
2. `SpawnDebris()` (línea 104-114) usa `rend.sharedMaterial.color` (línea 107) para elegir el color de los escombros. Con el fix de arriba, `sharedMaterial.color` ya NO refleja el daño (queda en el color original del asset), así que hay que leerlo del property block en su lugar, usando `CubeFxReactor.ReadTint(Renderer)` (ya público, `CubeFxReactor.cs:164-175`, con fallback automático a `sharedMaterial.color` si nunca se escribió el block):
   ```csharp
   void SpawnDebris(int count, float speed)
   {
       var origin = transform.position + Vector3.up * baseScale.y * 0.4f;
       Color debrisColor = rend != null ? CubeFxReactor.ReadTint(rend) : baseColor;
       ...
   }
   ```
3. No hace falta tocar `CacheIfNeeded()` (línea 43-51): `baseColor` se sigue leyendo de `rend.sharedMaterial.color` en el primer frame, que en ese momento todavía no fue tocado por ningún property block, así que sigue siendo válido como "color original".

**Verificación:** Agregar un `Check()` en la sección donde ya se buildean obstáculos (`BuildObstacles()`, `HeadlessTestRunner.cs:2209-2235`) o en `RunPhase5`, con dos `ObstacleMarker` que comparten el MISMO asset de `Material`:
```csharp
var sharedMat = SP.Presentation.SafeMaterial.Create(Color.white);
var obsA = GameObject.CreatePrimitive(PrimitiveType.Cube);
obsA.GetComponent<MeshRenderer>().sharedMaterial = sharedMat;
var markerA = obsA.AddComponent<SP.Presentation.ObstacleMarker>();
var obsB = GameObject.CreatePrimitive(PrimitiveType.Cube);
obsB.GetComponent<MeshRenderer>().sharedMaterial = sharedMat;
var markerB = obsB.AddComponent<SP.Presentation.ObstacleMarker>();

markerA.TakeDamage(60); // suficiente para cruzar el primer umbral de etapa (0.66)
Check("ObstacleMarker: dañar UN obstaculo NO cambia el color del Material ASSET compartido",
    sharedMat.color == Color.white);
Check("ObstacleMarker: el obstaculo NO dañado (mismo material) sigue leyendose con su color original",
    SP.Presentation.CubeFxReactor.ReadTint(obsB.GetComponent<MeshRenderer>()) == Color.white
    || obsB.GetComponent<MeshRenderer>().sharedMaterial.color == Color.white);
UnityEngine.Object.DestroyImmediate(obsA);
UnityEngine.Object.DestroyImmediate(obsB);
UnityEngine.Object.DestroyImmediate(sharedMat);
```

**Riesgo/efectos secundarios:** `_BaseColor`/`_Color` vía property block sólo afecta el tinte de color; si en el futuro `ObstacleMarker` necesitara variar `_Smoothness`/`_Metallic` por etapa, haría falta extender `WriteTint` o agregar un método hermano — no es el caso hoy. Confirmar que el shader de los obstáculos (creados con `GameObject.CreatePrimitive` + material por defecto/`SafeMaterial`) expone efectivamente `_BaseColor` o `_Color` (URP Lit/Unlit lo hacen, y `WriteTint` ya escribe ambas por las dudas, línea 159-160). Revisar además que ningún otro sistema lea `ObstacleMarker`'s `rend.sharedMaterial.color` esperando ver el color dañado (no lo hay fuera de esta clase).

---

### Bug 8: `WorldUiDirector` — hueco en el traspaso de singleton deja `active == null` para siempre
**Archivos:** `Presentation/WorldUiDirector.cs:103-114` (`OnEnable`/`OnDisable`), campo estático `active` (línea 92)

**Causa raíz:** `OnEnable()` (línea 105) sólo toma el testigo `if (active == null) active = this;`. `OnDisable()` (línea 113) sólo lo suelta `if (active == this) active = null;`. Si existe una segunda instancia `B` cuyo `OnEnable` corrió mientras `active` ya apuntaba a la primera instancia `A` (`B` nunca se vuelve `active` porque el guard lo bloquea), y después `A` se deshabilita, `active` pasa a `null` — pero `B` ya ejecutó su `OnEnable` en el pasado y nunca lo va a volver a ejecutar solo porque `A` se apagó, así que nadie vuelve a reclamar el testigo. `LateUpdate()` (línea 120-124) hace `if (active != this) return;` para las dos instancias, así que a partir de ahí **ningún** `WorldUiDirector` corre `Tick()`: barras de vida, iconos de minimapa y marcador de poseído quedan congelados por el resto de la sesión.

**Plan de implementación:**
1. El archivo ya usa, para otro propósito, el patrón de "lista estática de instancias vivas que se llenan en `OnEnable` y vacían en `OnDisable`" (ver `healthBars`/`minimapIcons`/`possessedMarkers`, líneas 38-49). Aplicar el mismo patrón acá para poder elegir un reemplazante real al soltar el testigo:
   ```csharp
   static WorldUiDirector active;
   // Todas las instancias habilitadas ahora mismo, en orden de alta. Es
   // lo que permite, al soltar el testigo, elegir una instancia que
   // REALMENTE siga viva en vez de dejar 'active' en null para siempre.
   static readonly List<WorldUiDirector> enabledInstances = new List<WorldUiDirector>();
   ```
2. Reescribir `OnEnable`/`OnDisable`:
   ```csharp
   void OnEnable()
   {
       enabledInstances.Add(this);
       if (active == null) active = this;
       cam = null;
       nextEvaluateAt = 0f;
   }

   void OnDisable()
   {
       enabledInstances.Remove(this);
       if (active == this)
       {
           // BUG ORIGINAL: 'active = null' sin reclamo dejaba el
           // LateUpdate() de TODA la UI de mundo permanentemente apagado
           // si existia una segunda instancia (su OnEnable ya habia
           // corrido y visto 'active' ocupado, asi que nunca volveria a
           // intentar tomarlo). Se cede el testigo a la siguiente
           // instancia que siga habilitada, si la hay.
           active = enabledInstances.Count > 0 ? enabledInstances[0] : null;
       }
   }
   ```
3. Agregar una propiedad pública mínima, solo para poder verificar el fix sin depender de que `LateUpdate` corra (no corre en Edit mode sin `[ExecuteAlways]`):
   ```csharp
   // Solo para verificacion: si esta instancia es la que efectivamente
   // conduce el unico LateUpdate de UI de mundo.
   public bool IsDrivingUpdates => active == this;
   ```

**Verificación:** Dado que `OnEnable`/`OnDisable` de MonoBehaviours SÍ corren al hacer `AddComponent` en Edit mode dentro de este proyecto (mismo patrón documentado en el propio bug de `PossessedMarkerView`, ver Bug 10), se puede armar el escenario completo en `RunPhase5` de `HeadlessTestRunner.cs` sin reflection sobre el lifecycle (solo sobre el campo estático si hiciera falta inspeccionarlo):
```csharp
var goA = new GameObject("WorldUiDirector_TestA");
var wudA = goA.AddComponent<SP.Presentation.WorldUiDirector>(); // dispara OnEnable
var goB = new GameObject("WorldUiDirector_TestB");
var wudB = goB.AddComponent<SP.Presentation.WorldUiDirector>(); // OnEnable ve 'active' ocupado por A

Check("WorldUiDirector: la primera instancia habilitada toma el testigo", wudA.IsDrivingUpdates && !wudB.IsDrivingUpdates);

UnityEngine.Object.DestroyImmediate(goA); // dispara OnDisable de A

Check("WorldUiDirector: al apagar la instancia activa, la SEGUNDA instancia (ya habilitada antes) toma el testigo",
    wudB.IsDrivingUpdates);

UnityEngine.Object.DestroyImmediate(goB);
```
**Importante:** este test crea instancias de prueba ADEMÁS de la instancia real de la escena (agregada en `servicesGO` en la línea 687) — verificar que el `WorldUiDirector` real de la escena recupera el testigo (o se lo queda) al final del test, para no dejar la UI de mundo real sin conductor para el resto de la suite. Puede hacer falta guardar `bool wasReal = servicesWud.IsDrivingUpdates` antes del test y restaurarlo forzando `DestroyImmediate`+recreación si el test lo dejó en otra instancia, o simplemente ejecutar este test ANTES de que se registre ningún `HealthBarView`/`MinimapIcon` real, para minimizar el impacto si algo queda mal.

**Riesgo/efectos secundarios:** `enabledInstances` es una lista estática más para mantener sincronizada; confirmar que `DestroyImmediate`/`Destroy` de un `WorldUiDirector` siempre dispara `OnDisable` (así es, es el contrato normal de Unity) para que la lista no acumule referencias muertas. Nunca debería haber dos `WorldUiDirector` reales simultáneos en el juego (es un caso de borde defensivo, tal como ya comenta la línea 116-119 del archivo), así que este fix es puramente una red de seguridad — de todos modos vale la pena por lo grave del síntoma (congelamiento total y silencioso de la UI de mundo).

---

### Bug 9: `VehicleFxReactor.SparkFlash()` deja el chasis pegado en dorado si se interrumpe
**Archivos:** `Presentation/VehicleFxReactor.cs:109-117` (corrutina `SparkFlash`), falta `OnDisable`

**Causa raíz:** `SparkFlash()` pinta el chasis de `SparkColor` (línea 112), espera `WaitForSeconds(0.12f)` (línea 114) y recién ahí llama `RestoreBaseColors()` (línea 116). Unity mata cualquier corrutina en curso cuando el `MonoBehaviour` (o su `GameObject`) se deshabilita, SIN ejecutar el resto del método — así que si el vehículo se desactiva/destruye (o el componente se deshabilita) durante esos 0.12s, `RestoreBaseColors()` nunca corre y el chasis queda pegado en dorado. El único otro lugar que llama `RestoreBaseColors()` es el propio `OnDamage()` (línea 98, ANTES de arrancar el siguiente flash), así que si no llega un próximo impacto (vehículo destruido, o simplemente no le vuelven a pegar), el dorado queda para siempre.

**Plan de implementación:**
1. Agregar un `OnDisable()` que fuerce la restauración, con el mismo criterio defensivo que ya usa `RestoreBaseColors()` (chequea `vehicle.IsDestroyed` para no pisar el negro de un chasis realmente destruido, línea 124):
   ```csharp
   void OnDisable()
   {
       // Si el componente/vehiculo se desactiva a mitad de SparkFlash(),
       // Unity mata la corrutina SIN correr el RestoreBaseColors() del
       // final: el chasis quedaba pegado en SparkColor (dorado) hasta el
       // proximo OnDamage(), que puede no llegar nunca mas. Se fuerza
       // aca, con la misma logica defensiva (no pisar el negro de un
       // vehiculo realmente destruido).
       RestoreBaseColors();
   }
   ```
2. `RestoreBaseColors()` ya maneja el caso `vehicle == null` (cae al loop de `chassisRenderers`/`baseColors`, línea 134-135); agregar una guarda mínima extra por robustez frente a un `OnDisable` que llegara a dispararse antes de que `Bootstrap()` termine de poblar esos arrays (no debería pasar en el flujo normal, porque `Awake()` llama `Bootstrap()` antes de que nada pueda deshabilitar el componente, pero es una red de seguridad barata):
   ```csharp
   void RestoreBaseColors()
   {
       if (chassisRenderers == null || baseColors == null) return;
       if (vehicle != null && vehicle.IsDestroyed) return;
       if (vehicle != null) { vehicle.RefreshOccupancyColor(); return; }
       for (int i = 0; i < chassisRenderers.Length; i++)
           if (HasMaterial(chassisRenderers[i])) chassisRenderers[i].sharedMaterial.color = baseColors[i];
   }
   ```

**Verificación:** Requiere Play mode real porque depende de que la corrutina esté efectivamente en pausa a mitad de ejecución (en Edit mode headless, `StartCoroutine` no avanza). Secuencia manual:
1. Entrar en Play mode, ubicar la cámara sobre un vehículo con ocupante.
2. Dispararle al vehículo una vez (dispara `OnDamage` → `SparkFlash`), y ANTES de que pasen 0.12s, desactivar el `GameObject` del vehículo (o del `VehicleFxReactor`) desde el Inspector, o programáticamente.
3. Reactivarlo y observar el color del chasis: con el fix, debe volver al color de ocupación normal (`vehicle.RefreshOccupancyColor()`), no quedar dorado.
- Alternativa semi-automatizada (Play mode, no Edit mode headless): un script de test de Play mode (fuera de `HeadlessTestRunner.cs`, que corre en Edit mode) que haga `reactor.OnDamage(...)` seguido de `reactor.gameObject.SetActive(false)` en el mismo frame y compruebe `chassisRenderers[0].sharedMaterial.color != SparkColor` inmediatamente después.

**Riesgo/efectos secundarios:** `RestoreBaseColors()` ahora se puede llamar más seguido (cada vez que el componente se deshabilita, no solo antes de un nuevo flash); es idempotente y barata (un loop sobre pocos renderers, o una llamada a `vehicle.RefreshOccupancyColor()`), así que no hay costo relevante. Confirmar que `vehicle.RefreshOccupancyColor()` no dispara efectos secundarios indeseados si se lo llama justo cuando el vehículo está siendo destruido en el mismo frame (ya está cubierto por el chequeo `vehicle.IsDestroyed` antes de llegar ahí).

---

### Bug 10: `PossessedMarkerView.BuildMarker()` usa `Destroy()` sin branch de modo, y `OnEnable` corre en Edit mode
**Archivos:** `Presentation/PossessedMarkerView.cs:63` (dentro de `BuildMarker`, llamado desde `OnEnable` en la línea 35)

**Causa raíz:** `BuildMarker()` hace `if (col != null) Destroy(col);` sin condicionar por `Application.isPlaying`. Este componente construye su marcador desde `OnEnable()` (línea 35: `if (marker == null) BuildMarker();`), y en este proyecto `OnEnable` corre también en Edit mode (la escena entera de la suite headless se arma con `AddComponent<T>()` desde `Editor/HeadlessTestRunner.cs`, que dispara `OnEnable` de inmediato). `Destroy()` en Edit mode lanza el error "Destroy may not be called from edit mode! Use DestroyImmediate instead" y corta la ejecución de `BuildMarker()` en ese punto — el resto del método (escala, rotación, material, `marker = go.transform;`, `SetActive(false)`) nunca corre, dejando `marker` en `null` para siempre y generando un log de error en cada corrida de la suite. Todos los hermanos de esta clase en el mismo directorio (`MinimapIcon`, `ObstacleMarker` — ver `DecalPool.cs`/`DebrisPool.cs` también — `OrderMarkerFx`, `SquadStateIndicatorView`, `SelectionRingFx`, `VehicleMountIndicator`) ya usan el branch correcto.

**Plan de implementación:**
1. Aplicar el mismo patrón que el resto del directorio (ver por ejemplo `DecalPool.cs:86-88` o `DebrisPool.cs:92-96`):
   ```csharp
   void BuildMarker()
   {
       var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
       go.name = "PossessedMarker";
       var col = go.GetComponent<Collider>();
       if (col != null)
       {
           if (Application.isPlaying) Destroy(col);
           else DestroyImmediate(col);
       }
       go.transform.SetParent(transform, false);
       ...
   }
   ```
2. No hace falta ningún otro cambio: el resto del método ya es agnóstico de modo.

**Verificación:** Agregar un `Check()` en `RunPhase5` (o justo después de construir `possessedMarker` en la sección de setup, `HeadlessTestRunner.cs:670`), usando reflection sobre el campo privado `marker` (mismo estilo ya usado en el archivo para otros campos privados, p. ej. líneas 1319, 1351, 1379):
```csharp
var markerField = typeof(PossessedMarkerView).GetField("marker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var builtMarker = (Transform)markerField.GetValue(possessedMarker);
Check("PossessedMarkerView.BuildMarker() completo sin abortar en Edit mode (marker != null)", builtMarker != null);
if (builtMarker != null)
    Check("El marcador construido no tiene Collider colgado (se limpio con el branch de modo correcto)",
        builtMarker.GetComponent<Collider>() == null);
```
Antes del fix, el primer `Check` debería fallar (`marker` queda `null`) y además debería verse un error de consola "Destroy may not be called from edit mode" en cada corrida de la suite — confirmar que ese log desaparece tras aplicar el fix.

**Riesgo/efectos secundarios:** Ninguno: es exactamente el mismo patrón ya probado y usado en el resto del directorio, sin cambio de comportamiento en Play mode (donde `Destroy(col)` ya funcionaba). Vale la pena, de paso, revisar si existe algún otro `Destroy(...)` sin branch de modo en las clases de UI de mundo que todavía no pasaron por este audit — no está en el alcance de estos 12 bugs, pero es el mismo patrón de riesgo.

---

### Bug 11: `MuzzleLightPool.Flash()` siempre roba `all[0]`, no la luz más próxima a apagarse
**Archivos:** `Presentation/MuzzleLightPool.cs:43-61` (método `Flash`), `MuzzleFlashLight` (líneas 78-108)

**Causa raíz:** Cuando las 6 luces del cupo (`Budget = 6`) están todas encendidas (`pick == null` tras el `foreach` que busca una libre, y `all.Count < Budget` es falso), el código hace `else pick = all[0];` (línea 57) — SIEMPRE la primera luz creada en toda la partida, sin importar cuál está más cerca de apagarse sola. El comentario de la línea 54-56 dice explícitamente "se roba la mas vieja", pero el código no calcula eso: bajo fuego sostenido con más de 6 destellos simultáneos, la MISMA luz (`all[0]`) es arrancada de su posición una y otra vez en vez de rotar entre las 6 disponibles, lo cual se ve como una sola luz saltando erráticamente por el mapa.

**Plan de implementación:**
1. Exponer el instante de apagado (`offAt`, ya existe como campo privado en `MuzzleFlashLight`, línea 81) como propiedad de solo lectura, para poder comparar desde `MuzzleLightPool`:
   ```csharp
   public class MuzzleFlashLight : MonoBehaviour
   {
       Light lightRef;
       float offAt;
       public bool IsOn => lightRef != null && lightRef.enabled;
       // Instante (Time.time) en el que esta luz se apagaria sola. Sirve
       // para elegir, cuando el cupo esta agotado, la luz mas VIEJA (la
       // que se prendio hace mas tiempo, no siempre all[0]).
       public float OffAt => offAt;
       ...
   ```
2. En `MuzzleLightPool.Flash()`, reemplazar el robo fijo por una búsqueda de mínimo `OffAt` (equivalente a "la que lleva más tiempo prendida", porque todas comparten la misma duración `FlashSeconds`):
   ```csharp
   if (pick == null)
   {
       if (all.Count < Budget) pick = Create();
       else
       {
           // Cupo agotado: se recicla la luz MAS CERCA de apagarse sola
           // (la que se prendio hace mas tiempo), no siempre la primera
           // creada. Antes 'all[0]' era SIEMPRE la primera luz de toda la
           // partida: con fuego sostenido mas alla de 6 destellos
           // simultaneos, eso hacia que UNA sola luz saltara de lado a
           // lado del mapa en vez de rotar entre las 6.
           pick = all[0];
           float earliestOffAt = pick != null ? pick.OffAt : float.MaxValue;
           for (int i = 1; i < all.Count; i++)
           {
               if (all[i] != null && all[i].OffAt < earliestOffAt)
               {
                   earliestOffAt = all[i].OffAt;
                   pick = all[i];
               }
           }
       }
   }
   ```

**Verificación:** Agregar un `Check()` en `RunPhase5`, usando reflection sobre el campo privado `offAt` de `MuzzleFlashLight` para simular distintos instantes de apagado (`Time.time` no avanza en Edit mode headless, así que todas las luces tendrían el mismo `offAt` si se las prende de corrido sin forzar valores distintos):
```csharp
var allField = typeof(SP.Presentation.MuzzleLightPool).GetField("all", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
// Llenar el cupo (6 luces).
for (int i = 0; i < SP.Presentation.MuzzleLightPool.Budget; i++)
    SP.Presentation.MuzzleLightPool.Flash(new Vector3(i, 0f, 0f), Color.white);
var allLights = (System.Collections.IList)allField.GetValue(null);
Check($"MuzzleLightPool: cupo lleno tras {SP.Presentation.MuzzleLightPool.Budget} flashes ({allLights.Count} luces creadas)",
    allLights.Count == SP.Presentation.MuzzleLightPool.Budget);

// Forzar offAt distintos por reflection (simula que se prendieron en
// instantes distintos, algo que Time.time congelado en Edit mode no
// produce solo).
var offAtField = typeof(SP.Presentation.MuzzleFlashLight).GetField("offAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
for (int i = 0; i < allLights.Count; i++)
    offAtField.SetValue(allLights[i], (float)(10 + i)); // indice 0 = la que se apaga ANTES = la mas vieja
var oldestLight = allLights[0];

// 7mo flash con el cupo lleno: debe robar la de menor OffAt (indice 0),
// NO necesariamente all[0] "porque si" -- se verifica moviendo esa
// referencia a otro indice de la lista antes del robo.
// (Reordenar allLights o simplemente asignar el offAt mas bajo a un
// indice distinto de 0 para separar ambas hipotesis, p.ej. indice 3.)
for (int i = 0; i < allLights.Count; i++)
    offAtField.SetValue(allLights[i], 100f + i);
offAtField.SetValue(allLights[3], 5f); // el indice 3 es ahora el mas viejo, NO el indice 0

var newFlashPos = new Vector3(999f, 0f, 0f);
SP.Presentation.MuzzleLightPool.Flash(newFlashPos, Color.white);
var stolenLight = (SP.Presentation.MuzzleFlashLight)allLights[3];
Check("MuzzleLightPool.Flash roba la luz con MENOR OffAt (la mas vieja), no siempre all[0]",
    Vector3.Distance(stolenLight.transform.position, newFlashPos) < 0.01f);
```

**Riesgo/efectos secundarios:** El costo agregado es un `for` de a lo sumo 6 elementos, insignificante frente al resto del método. Confirmar que `OffAt` de una luz recién creada (`Create()`, nunca flasheada) no rompe la comparación: su `offAt` por defecto es `0f` (default de `float`), que siempre sería "la más vieja" — pero ese caso ya está cubierto porque una luz recién creada tiene `IsOn == false` (su `Light.enabled` arranca en `false`, línea 71) y por lo tanto es encontrada primero por el `foreach` que busca libres (línea 49), nunca llega a esta rama.

---

### Bug 12: `OrderLineManager` filtra un `Material` por ciclo (mismo defecto que el Bug 2)
**Archivos:** `Presentation/OrderLineManager.cs:44-62` (métodos `RemoveLine` y `CreateLine`)

**Causa raíz:** Idéntica a la del Bug 2 pero en el sistema de líneas de orden de movimiento en vez de líneas de ataque: `CreateLine()` (línea 51-62) crea un `Material` propio vía `SafeMaterial.Create(LineColor)` (línea 58) para cada `LineRenderer` de orden. `RemoveLine()` (línea 44-49) sólo hace `Destroy(lr.gameObject)`, sin destruir `lr.material`. Cada vez que un soldado completa o cancela una orden de movimiento (línea 27-31: `if (!destination.HasValue || !soldier.gameObject.activeInHierarchy) { RemoveLine(soldier.Id); continue; }`) se filtra un material.

**Plan de implementación:** Exactamente el mismo fix que el Bug 2, aplicado a este archivo:
1. `RemoveLine()`:
   ```csharp
   void RemoveLine(int actorId)
   {
       if (!lines.TryGetValue(actorId, out var lr)) return;
       lines.Remove(actorId);
       if (lr == null) return;

       // Mismo defecto que AttackLineManager.RemoveLine: CreateLine()
       // asigna una instancia PROPIA de Material via SafeMaterial.Create,
       // y Destroy(gameObject) no la libera sola.
       var mat = lr.material;
       if (Application.isPlaying)
       {
           if (mat != null) Destroy(mat);
           Destroy(lr.gameObject);
       }
       else
       {
           if (mat != null) DestroyImmediate(mat);
           DestroyImmediate(lr.gameObject);
       }
   }
   ```
2. `Prewarm()` (línea 67-75): mismo ajuste que en el Bug 2, capturar y destruir `lr.material` junto con `lr.gameObject`.
3. Dado que la causa raíz y el fix son un calco del Bug 2, conviene — si el equipo quiere invertir un poco más — extraer un helper común (por ejemplo un método estático `LineFx.DestroyLine(LineRenderer lr)` compartido entre `AttackLineManager` y `OrderLineManager`) para no mantener la misma lógica de limpieza duplicada en dos archivos. No es obligatorio para el fix mínimo, pero reduce el riesgo de que un futuro tercer sistema de líneas repita el mismo bug.

**Verificación:** Igual que el Bug 2: no es práctico contarlo con precisión en la suite headless sin manipular el diccionario privado `lines` por reflection. Verificación recomendada, **Play mode manual**: dar órdenes de movimiento y dejar que se completen (o cancelarlas) 10-20 veces seguidas sobre el mismo soldado, y confirmar con `Resources.FindObjectsOfTypeAll<Material>().Length` (o el Profiler de Memoria) que el conteo de materiales no crece de forma sostenida ciclo a ciclo.

**Riesgo/efectos secundarios:** Los mismos que el Bug 2 (ver esa sección) — ninguna referencia externa depende de que el material de una `OrderLine` siga vivo después de que la orden termina.

---

### Bug 14: regresiones de `Time.deltaTime` sin escalar durante `Time.timeScale = 0`

Ver también las regresiones sistémicas de `Time.deltaTime` sin escalar durante `Time.timeScale = 0`, cubiertas en el documento de UI.


---

# UI (HUD, menús, paneles) — Planes de corrección (14 bugs)

---

### Bug 1: `KeyRebindView.BeginListening` indexa `Labels[row]` sin validar contra `Labels.Length`

**Archivos:** `UI/KeyRebindView.cs:63-69`

**Causa raíz:** `BeginListening(int row)` valida `row` únicamente contra `ActionIds.Length` (`row < 0 || row >= ActionIds.Length`). Después, aunque hace `Labels != null && Labels[row] != null`, ese chequeo YA está indexando `Labels[row]` dentro de la propia condición — si `Labels.Length < ActionIds.Length` y `row` cae en la zona que sólo existe en `ActionIds`, `Labels[row]` tira `IndexOutOfRangeException` antes de que el `&&` llegue a evaluar nada. `RefreshAll()` (línea 116) sí hace bien esto mismo: `for (int i = 0; i < ActionIds.Length && i < Labels.Length; i++)`. Ese es el patrón a copiar.

**Plan de implementación:**
1. En `BeginListening`, cambiar la guarda de entrada para incluir también `Labels`:
   ```csharp
   public void BeginListening(int row)
   {
       if (ActionIds == null || Labels == null || row < 0 || row >= ActionIds.Length || row >= Labels.Length) return;
       ...
   }
   ```
2. Este cambio se hace en el mismo lugar donde se resuelve el Bug 5 (mismo método) — ver el paso siguiente para no pisar esa edición: el método completo queda con la guarda ampliada primero, y recién después el `RefreshAll()` que pide el Bug 5.
3. No hace falta tocar `HookRows()` ni `Bind()`: el problema es puramente de la validación de índice, no de cómo se conectan los arrays.

**Verificación:** Agregar un `Check()` en `RunPhase5` (cerca de los checks de `KeyBindings`/`ControlsTable`, ~línea 1266 de `HeadlessTestRunner.cs`) que arme un `KeyRebindView` de prueba con `Labels` deliberadamente más corto que `ActionIds` y confirme que `BeginListening` no explota:
```csharp
var krvGO = new GameObject("KeyRebindLenTest");
var krv = krvGO.AddComponent<SP.UI.KeyRebindView>();
krv.Bind(new Button[3], new Text[1], new[] { "a", "b", "c" }); // Labels mas corto a proposito
bool threw = false;
try { krv.BeginListening(2); } catch { threw = true; }
Check("BeginListening no explota cuando Labels es mas corto que ActionIds", !threw);
UnityEngine.Object.DestroyImmediate(krvGO);
```

**Riesgo/efectos secundarios:** Ninguno funcional: en el camino real (`BuildRebindPanel` en `HeadlessTestRunner.cs:2142-2160`) `botones`, `etiquetas` y `acciones` siempre se arman con la misma longitud, así que este bug nunca dispara hoy — es puramente defensivo contra un `Bind()` futuro con arrays desparejos (p. ej. si alguien arma el panel a mano en el Editor y olvida un label). Ojo al aplicar junto con el fix del Bug 5: ambos tocan `BeginListening`, conviene hacerlos en el mismo commit/edición para no dejar el método a mitad de camino.

---

### Bug 2: `AimUI.RecomputeCrosshairSize` pisa el tamaño real de la mirilla con un `Vector2(6,6)` fijo

**Archivos:** `UI/AimUI.cs:20,44-47` (además de los puntos donde se captura el tamaño real: `Bind()` línea 113 y `OnEnable()` línea 181)

**Causa raíz:** `crosshairBaseSize` cumple dos roles a la vez y eso es el bug. Por un lado, `Bind()` y `OnEnable()` lo llenan correctamente con `cross.rectTransform.sizeDelta` (el tamaño real de la `Image` de la escena). Por otro lado, `RecomputeCrosshairSize()` — que corre en la PRIMERA llamada a `SetCrosshairScale()` o `SetSpread01()` — reescribe ese mismo campo así: `crosshairBaseSize = new Vector2(6f, 6f) * crosshairUserScale + Vector2.one * (crosshairSpreadFraction * 9f);`. El `new Vector2(6f, 6f)` es un valor hardcodeado que reemplaza silenciosamente el tamaño real capturado. Como `PauseController.OnEnable()` (línea ~176) llama `AimUiRef.SetCrosshairScale(savedCrossScale)` para restaurar el ajuste de accesibilidad guardado en `PlayerPrefs`, esa restauración es EXACTAMENTE el disparador: apenas se aplica, la mirilla salta a 6x6 en vez de mantener su tamaño real de diseño escalado.

**Plan de implementación:**
1. Agregar un campo nuevo que guarde el tamaño CRUDO de la imagen (sin escalar), separado del tamaño efectivo:
   ```csharp
   Vector2 crosshairSpriteSize = new Vector2(6f, 6f); // sizeDelta real de la Image, sin escala de usuario ni spread
   ```
2. En `Bind(Text prompt, Image cross)` (línea 106-115), cambiar la asignación para que capture en el campo nuevo y fuerce un recálculo inmediato:
   ```csharp
   if (cross != null)
   {
       crosshairBaseColor = cross.color;
       crosshairSpriteSize = cross.rectTransform.sizeDelta;
       RecomputeCrosshairSize();
   }
   ```
3. En `OnEnable()` (bloque `if (crosshair == null)`, línea 172-184), el mismo cambio: asignar `crosshairSpriteSize = crosshair.rectTransform.sizeDelta;` en vez de `crosshairBaseSize = ...`, y llamar `RecomputeCrosshairSize();` a continuación.
4. En `RecomputeCrosshairSize()` (línea 44-47), reemplazar el literal por el campo real:
   ```csharp
   void RecomputeCrosshairSize()
   {
       crosshairBaseSize = crosshairSpriteSize * crosshairUserScale + Vector2.one * (crosshairSpreadFraction * 9f);
   }
   ```
   `crosshairBaseSize` queda como el tamaño EFECTIVO derivado (el que ya consumen `FlashHitMarker` y el resto), y `crosshairSpriteSize` es la fuente de verdad del tamaño de diseño.
5. El paso 2 y 3 (llamar `RecomputeCrosshairSize()` justo después de capturar `crosshairSpriteSize`) es necesario: sin eso, entre el `Bind()`/`OnEnable()` y la primera llamada a `SetCrosshairScale`/`SetSpread01`, `crosshairBaseSize` seguiría con el valor default `new Vector2(6f,6f)` del campo (no el tamaño real recién capturado), reproduciendo una versión más leve del mismo bug en esa ventana.

**Verificación:** Agregar un `Check()` en `RunPhase5` o cerca de los checks de `AimUI` existentes (~línea 1345, donde ya se usa `aimUiRef` con reflexión sobre `soldierInfoText`) usando reflexión sobre el campo privado `crosshairBaseSize`/`crosshairSpriteSize`:
```csharp
if (aimUiRef != null)
{
    var spriteSizeField = typeof(AimUI).GetField("crosshairSpriteSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var before = (Vector2)spriteSizeField.GetValue(aimUiRef);
    aimUiRef.SetCrosshairScale(1.5f); // simula PauseController restaurando un ajuste guardado
    var after = (Vector2)spriteSizeField.GetValue(aimUiRef);
    Check($"SetCrosshairScale no pisa el tamaño real de la mirilla capturado en Bind/OnEnable ({before} -> {after} deberian coincidir)", before == after);
    aimUiRef.SetCrosshairScale(1f); // restaura para no afectar checks posteriores
}
```
Como prueba manual complementaria en Play mode: guardar un `sp_crosshair_scale` distinto de 1 en `PlayerPrefs`, entrar a la partida, y confirmar a ojo (o con `Debug.Log` temporal de `crosshairBaseSize`) que la mirilla arranca con el tamaño escalado correcto, no en 6x6.

**Riesgo/efectos secundarios:** Revisar que ningún otro lugar del archivo siga leyendo `crosshairBaseSize` esperando el tamaño "crudo" sin escalar (por ejemplo, `FlashHitMarker` línea 318 y 325 usa `crosshairBaseSize` como el tamaño de reposo al que vuelve el flash — con el fix sigue siendo así, y sigue siendo correcto porque ese es justamente el tamaño EFECTIVO actual, que es lo que se quiere). No afecta a `crosshairBaseColor`, que sigue su propio camino sin cambios.

---

### Bug 3: `DeadNoticeView.Show()` no encola avisos — un segundo `Show()` en el mismo frame pisa al primero

**Archivos:** `UI/DeadNoticeView.cs:34-40`

**Causa raíz:** `Show()` simplemente corta la corrutina anterior (`StopCoroutine(routine)`) y arranca una nueva con el mensaje nuevo. `PlayerInputDriver.OnSquadDamage` (líneas 269-298) puede llamar `DeadNotice.Show(...)` DOS VECES en la misma invocación del handler: una vez para "esta bajo ataque" (línea 281) y, si el mismo golpe cruza el umbral de vida baja, otra vez para "tiene poca vida" (línea 292) — todo antes de que un solo frame llegue a renderizar el primer aviso. El proyecto YA tiene la infraestructura pensada exactamente para este problema: `UI/AlertQueue.cs` es una cola con prioridad construida "porque varias vistas (ModeToastView, InstructionBannerView, PhaseBannerView, KillFeedView, **DeadNoticeView**...) deciden por su cuenta cuándo escribir en pantalla y se pisan entre sí" (comentario en `AlertQueue.cs:30-35`), pero el propio comentario del archivo (línea 41-42) admite: "la cola está lista, pero NINGUNA vista está migrada todavía". `AlertQueue` ya se usa en producción (ver `PlayerInputDriver.cs:1761`, orden desde el minimapa), así que migrar `DeadNoticeView` a esta cola es agarrar una pieza ya construida y cablearla, no inventar nada nuevo.

**Plan de implementación:**
1. En `DeadNoticeView.cs`, agregar `using SP.UI;` no hace falta (ya está en el namespace `SP.UI`), pero sí referenciar `AlertQueue`/`AlertPriority` directamente.
2. Cambiar `Show(string message, float fadeSeconds = 3f)` para que, en vez de arrancar la corrutina directo, empuje el aviso a la cola:
   ```csharp
   public void Show(string message, float fadeSeconds = 3f)
   {
       if (string.IsNullOrEmpty(message)) return;
       AlertQueue.Push(message, AlertPriority.Media, fadeSeconds);
   }
   ```
   Se mantiene la firma `Show(string, float)` intacta a propósito: los 6 call sites en `PlayerInputDriver.cs` (líneas 281, 292, 472, 517, 999, 1017) no necesitan tocarse.
3. Agregar un `Update()` que consuma la cola cuando la vista no está ocupada mostrando algo:
   ```csharp
   void Update()
   {
       if (!Application.isPlaying) return;
       if (routine != null) return; // ya hay un aviso en pantalla, que termine su ciclo
       if (AlertQueue.TryDequeue(out string message, out float seconds))
           BeginShow(message, seconds);
   }
   ```
4. Extraer el cuerpo que arma la corrutina a un método privado `BeginShow`, separándolo del `Show()` público que ahora sólo encola:
   ```csharp
   void BeginShow(string message, float fadeSeconds)
   {
       if (label == null || group == null) return;
       label.text = message;
       routine = StartCoroutine(FadeOut(fadeSeconds));
   }
   ```
5. En `FadeOut`, al final (después de `group.alpha = 0f;`), limpiar `routine = null;` y llamar `AlertQueue.NotifyFinished();` para liberar el turno apenas termina de verdad (no antes) — así el próximo `Update()` puede sacar el siguiente pendiente sin esperar al reloj interno de la cola.
6. Repasar `OnDisable`/`OnEnable`: `DeadNoticeView` hoy no tiene `OnDisable`. Si el panel se desactiva a mitad de un fundido, conviene también parar la corrutina y resetear `routine = null` (esto se solapa con el patrón del Bug 9, aunque `DeadNoticeView` no está en la lista de esa sección — de todos modos, agregarlo aquí es gratis y cierra el mismo tipo de fuga: `void OnDisable() { StopAllCoroutines(); routine = null; if (group != null) group.alpha = 0f; }`).

**Verificación:** Prueba de integración en `HeadlessTestRunner.cs`, cerca de donde se construye `deadNoticeRef` (~línea 2850-2871): simular dos `Show()` seguidos en el mismo "frame lógico" (sin `yield` entre medio) y confirmar que el segundo mensaje no reemplazó al primero sin mostrarse, sino que quedó en la cola:
```csharp
if (deadNoticeRef != null)
{
    SP.UI.AlertQueue.Clear();
    deadNoticeRef.Show("X esta bajo ataque", 2f);
    deadNoticeRef.Show("X tiene poca vida", 2f);
    Check("Dos Show() seguidos en el mismo frame no se pisan: el segundo queda encolado",
        SP.UI.AlertQueue.PendingCount == 1 || SP.UI.AlertQueue.IsBusy);
}
```
(Este test corre en Edit mode, donde `StartCoroutine` no avanza — alcanza con verificar el estado de la cola, no hace falta Play mode real). Como prueba manual en Play mode: provocar un golpe a un aliado que simultáneamente dispare "bajo ataque" y cruce el umbral de vida baja, y confirmar que se leen los DOS avisos en secuencia, no que uno tapa al otro.

**Riesgo/efectos secundarios:** `AlertQueue` es estática y compartida — si en el futuro otra vista (`ModeToastView`, `KillFeedView`, etc.) también se migra a ella, dos avisos de prioridad distinta podrían competir por el mismo turno global aunque se muestren en widgets de pantalla distintos (la cola no distingue "para qué vista" es el aviso). Por ahora sólo migra `DeadNoticeView`, así que no hay colisión real todavía, pero vale dejarlo anotado para cuando se migren las demás. Repasar también que `AlertQueue.Push` usa `Time.unscaledTime` como reloj (línea 251 de `AlertQueue.cs`) — coherente con el fix del Bug 13 (evitar que la lógica de tiempo se cuelgue con `timeScale` en 0).

---

### Bug 4: `KeyRebindView.Update()` puede capturar la misma tecla (Enter/Espacio) que activó el botón por navegación de teclado

**Archivos:** `UI/KeyRebindView.cs:71-93`

**Causa raíz:** `BeginListening(row)` se dispara desde `Rows[i].onClick`, que el `EventSystem` de Unity puede invocar no sólo con clic de mouse sino con Enter/Espacio cuando el `Button` tiene el foco de navegación por teclado/gamepad. En el mismo frame (o el inmediato siguiente, según el orden de ejecución de scripts) en que `BeginListening` deja `listeningRow >= 0`, el propio `Update()` de este componente recorre `kb.allKeys` buscando `wasPressedThisFrame` (línea 87-92) sin filtrar la tecla que originó el click. Si Enter/Espacio todavía figura como recién apretado, `AssignKey` la captura de inmediato como el nuevo binding — el jugador nunca llega a soltar el botón antes de que ya se le haya asignado Enter a la acción, sin haber tocado ninguna tecla "de verdad" después de entrar al modo escucha.

**Plan de implementación:**
1. Agregar un campo que registre en qué frame arrancó la escucha:
   ```csharp
   int listenStartFrame = -1;
   ```
2. En `BeginListening`, guardar el frame actual junto con `listeningRow`:
   ```csharp
   listeningRow = row;
   listenStartFrame = Time.frameCount;
   ```
3. Extraer la decisión de "ignorar esta apretada" a un método **puro y testeable**, siguiendo la misma convención que ya usa el proyecto para su lógica de negocio aislada (`AudioDirector.SelectVictim`, `AlertQueue.SelectNext`, ambas comentadas explícitamente como "función pura" para poder probarlas sin depender de Play mode ni de hardware real):
   ```csharp
   // Funcion pura, sin estado de escena: separa la REGLA (ignorar el
   // frame en el que empezo a escuchar) de la lectura real del teclado,
   // para poder probarla sin Keyboard.current.
   public static bool ShouldIgnoreCapture(int listenStartFrame, int currentFrame) =>
       currentFrame <= listenStartFrame;
   ```
4. En `Update()`, usar ese método antes de recorrer `kb.allKeys`:
   ```csharp
   void Update()
   {
       if (!IsListening) return;
       var kb = Keyboard.current;
       if (kb == null) return;

       if (kb.escapeKey.wasPressedThisFrame) { listeningRow = -1; RefreshAll(); return; }

       if (ShouldIgnoreCapture(listenStartFrame, Time.frameCount)) return; // el click que activo la escucha no cuenta como la tecla nueva

       foreach (var control in kb.allKeys)
       {
           if (!control.wasPressedThisFrame) continue;
           AssignKey(control.keyCode);
           return;
       }
   }
   ```
   Nota: ESC sigue funcionando igual que antes, incluso en el frame de arranque — cancelar nunca debería quedar bloqueado por esta guarda.

**Verificación:** El método `ShouldIgnoreCapture` es puro, así que se puede probar directo sin `Keyboard.current` ni Play mode. Agregar en `RunPhase5` (junto a los demás checks puros de `KeyRebindView`/`KeyBindings`):
```csharp
Check("ShouldIgnoreCapture ignora el mismo frame en que arranco la escucha",
    SP.UI.KeyRebindView.ShouldIgnoreCapture(listenStartFrame: 100, currentFrame: 100));
Check("ShouldIgnoreCapture deja pasar el frame siguiente",
    !SP.UI.KeyRebindView.ShouldIgnoreCapture(listenStartFrame: 100, currentFrame: 101));
```
Como prueba manual en Play mode (la parte que sí depende de hardware real): abrir Pausa -> Controles -> Remapear, navegar con Tab/flechas hasta una fila y activarla con Enter (sin usar el mouse), y confirmar que la fila queda en "PRESIONA UNA TECLA" esperando de verdad, sin haberle asignado Enter a la acción.

**Riesgo/efectos secundarios:** Si en algún momento el orden de ejecución de scripts hace que `Update()` corra ANTES que el `onClick` del mismo frame (por ejemplo si el click llega por el Input System en un evento tardío), la guarda de un solo frame podría no alcanzar. Si en pruebas reales se ve que todavía se cuela la tecla de activación, ampliar la guarda a "frame de inicio + 1" en vez de sólo "frame de inicio" (cambiar `<=` por `< listenStartFrame + 2` en `ShouldIgnoreCapture`) es un ajuste de una línea gracias a que la regla está aislada en un método propio.

---

### Bug 5: `KeyRebindView.BeginListening` no limpia la fila anterior al cambiar de fila en escucha

**Archivos:** `UI/KeyRebindView.cs:63-69`

**Causa raíz:** Si el jugador hace clic en la fila A (que pasa a mostrar "PRESIONA UNA TECLA") y, sin terminar esa escucha, hace clic en la fila B, `BeginListening(rowB)` sólo cambia `listeningRow` y escribe el texto de B — nunca restaura el texto de A a su estado normal (`NameOf(...) + ": " + KeyBindings.DisplayName(...)`). El resultado visual es que dos filas quedan mostrando "PRESIONA UNA TECLA" a la vez, aunque sólo una (`listeningRow`) esté realmente escuchando.

**Plan de implementación:**
1. Insertar una llamada a `RefreshAll()` al principio de `BeginListening`, ANTES de fijar el nuevo `listeningRow` y de escribir el texto de "PRESIONA UNA TECLA":
   ```csharp
   public void BeginListening(int row)
   {
       if (ActionIds == null || Labels == null || row < 0 || row >= ActionIds.Length || row >= Labels.Length) return;
       RefreshAll(); // limpia cualquier "PRESIONA UNA TECLA" que hubiera quedado de una fila anterior
       listenStartFrame = Time.frameCount; // (del Bug 4, si se aplica en el mismo commit)
       listeningRow = row;
       if (Labels[row] != null)
           Labels[row].text = NameOf(ActionIds[row]) + ":  PRESIONA UNA TECLA";
   }
   ```
   La primera línea de guarda ya incluye el fix del Bug 1 (`row >= Labels.Length`); si ambos bugs se corrigen en la misma edición, el método final queda como arriba. Si se corrige este bug solo, alcanza con agregar el `RefreshAll();` sin tocar la guarda existente.
2. `RefreshAll()` ya es seguro de llamar en cualquier momento (no depende de `listeningRow`, sólo repinta `Labels[i]` a partir de `ActionIds[i]` y `KeyBindings.DisplayName`), así que no hace falta ningún guardado/restauración manual de la fila anterior.

**Verificación:** Agregar un `Check()` en `RunPhase5` que arme un `KeyRebindView` real (o reutilice uno de test), llame `BeginListening(0)`, después `BeginListening(1)`, y confirme que la fila 0 volvió a su texto normal:
```csharp
var krv2 = new GameObject("KeyRebindSwitchTest").AddComponent<SP.UI.KeyRebindView>();
var accs = new[] { SP.Player.KeyBindings.Recargar, SP.Player.KeyBindings.Interactuar };
var lbls = new Text[2];
for (int i = 0; i < 2; i++) { var go = new GameObject("Lbl" + i, typeof(Text)); lbls[i] = go.GetComponent<Text>(); }
krv2.Bind(new Button[2], lbls, accs);
krv2.BeginListening(0);
krv2.BeginListening(1);
Check("Al cambiar de fila en escucha, la fila anterior deja de mostrar 'PRESIONA UNA TECLA'",
    !lbls[0].text.Contains("PRESIONA"));
Check("La fila nueva SI muestra 'PRESIONA UNA TECLA'", lbls[1].text.Contains("PRESIONA"));
```

**Riesgo/efectos secundarios:** Ninguno esperado — `RefreshAll()` ya se usa en varios lugares del mismo archivo (`OnEnable`, tras ESC, tras `AssignKey`) sin causar overhead ni problemas de foco. Repasar que no quede una llamada duplicada a `RefreshAll()` si además se aplica el fix del Bug 1 en la misma edición (la guarda de entrada y el `RefreshAll()` son cambios independientes que conviven bien en el mismo método).

---

### Bug 6: `AimUI.OnEnable` arma `seatSquares` asumiendo un orden exacto de hijos, sin validar longitud

**Archivos:** `UI/AimUI.cs:200-203`

**Causa raíz:** `seatSquares = t.GetComponentsInChildren<Image>(true).Skip(1).Take(4).ToArray();` asume que el primer `Image` bajo `VehicleInfoPanel` es siempre el fondo del panel y que los siguientes 4 son los cuadrados de asiento en el orden Driver/Passenger1/Passenger2/Gunner. Hoy esto funciona porque `HeadlessTestRunner.BuildTurretAimUI` (o el bloque que arma `VehicleInfoPanel`, ~línea 2404-2444) crea el fondo primero y después los 4 `Seat_<Nombre>` en ese orden exacto, y ningún hijo intermedio tiene otro `Image`. Pero no hay ninguna verificación de que `squares.Length == 4` ni de que el orden coincida con `SeatOrder`: si algún día se agrega un ícono, un borde, o cualquier otro `Image` antes o entre los cuadrados de asiento, `UpdateVehicleInfo` (línea 420-438) empieza a pintar los colores de "libre/ocupado" en el cuadrado equivocado sin ningún error visible.

**Plan de implementación:**
1. En el bloque `if (vehicleInfoPanel == null)` de `OnEnable()` (línea 194-205), separar la búsqueda del `Array` en una variable local y validar su longitud contra `SeatOrder.Length` antes de aceptarla:
   ```csharp
   if (vehicleInfoPanel == null)
   {
       var t = canvasRoot.Find("VehicleInfoPanel");
       if (t != null)
       {
           vehicleInfoPanel = t.gameObject;
           var squares = t.GetComponentsInChildren<Image>(true).Skip(1).Take(4).ToArray();
           if (squares.Length == SeatOrder.Length)
           {
               seatSquares = squares;
           }
           else
           {
               seatSquares = null;
               Debug.LogWarning($"[AimUI] VehicleInfoPanel tiene {squares.Length} Image hijas tras el fondo (se esperaban {SeatOrder.Length}); el panel de asientos de vehiculo no se puede armar de forma confiable y queda desactivado.");
           }
       }
   }
   ```
2. No hace falta tocar `UpdateVehicleInfo`: ya sale temprano con `if (!show || seatSquares == null) return;` (línea 426), así que dejar `seatSquares` en `null` cuando la validación falla ya evita cualquier pintado incorrecto — simplemente el panel de asientos no se actualiza, en vez de actualizarse mal.

**Verificación:** Agregar un `Check()` cerca de donde ya se prueba `AimUI` en `RunPhase5` (o donde se construye la escena de test), leyendo `seatSquares` por reflexión tras un `OnEnable()` forzado:
```csharp
if (aimUiRef != null)
{
    var seatField = typeof(AimUI).GetField("seatSquares", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var squares = (Image[])seatField.GetValue(aimUiRef);
    Check($"AimUI arma exactamente 4 cuadrados de asiento desde VehicleInfoPanel (encontrados: {squares?.Length ?? -1})",
        squares != null && squares.Length == 4);
}
```
Como prueba manual en Play mode: apuntar al vehículo, confirmar que los 4 cuadrados cambian de verde a gris al ocupar cada asiento en el orden Conductor/Pasajero1/Pasajero2/Artillero (mismo orden que `SeatOrder`, línea 129-132).

**Riesgo/efectos secundarios:** Bajo — hoy el escenario de test siempre construye el panel con el orden correcto, así que este `Check()` va a pasar sin cambios de comportamiento visible; el valor real del fix es defensivo, para el día que alguien toque `BuildTurretAimUI`/el bloque de `VehicleInfoPanel` y meta un `Image` de más sin darse cuenta.

---

### Bug 7: `NearbySquadListView.Start()` nunca asigna `HealthFill` en el camino real de juego

**Archivos:** `UI/NearbySquadListView.cs:41-48`

**Causa raíz:** Hay dos caminos para poblar `rows`. El primero es `AddEntry()`, llamado desde `HeadlessTestRunner.BuildNearbySquadList` (líneas 3193-3244) al construir la escena en el Editor — ese sí pasa `healthFillImg` y arma `Row` completo. Pero `rows` es una `List<Row>` de C# común (no serializada), así que **no sobrevive al domain reload** al entrar en Play mode, exactamente el mismo patrón de bug que el proyecto ya documentó y resolvió en `SelectedSoldierUI` (ver comentario ahí, línea 146-152). En Play mode real, `rows.Count` arranca en 0 y `Start()` entra al camino de auto-descubrimiento (línea 41-48), que arma cada `Row` sólo con `Soldier`, `RowObject` y `Label` — el constructor de objeto nunca menciona `HealthFill`, así que ese campo queda en `null` para siempre en la partida real. `LateUpdate()` ya contempla el caso (`if (row.HealthFill != null) ...`, línea 70-71) así que no explota, pero la barra de vida simplemente nunca se dibuja ni actualiza: sólo el texto numérico funciona.

**Plan de implementación:** Este fix se hace junto con el del Bug 8 (mismo bloque de código, misma causa raíz de fondo: el auto-descubrimiento por índice ciego). En vez de emparejar por posición contra `GetComponentsInChildren<Text>`, reconstruir cada fila buscando sus hijos por NOMBRE dentro de la jerarquía real armada por `BuildNearbySquadList`, siguiendo el mismo patrón que ya usa `SelectedSoldierUI.OnEnable()` (busca `"Row_" + nombre`, y usa `child.Find("Label")?.GetComponent<Text>()` / `child.Find("BarBG/BarFill")?.GetComponent<Image>()`, líneas 158-177 de `SelectedSoldierUI.cs`). Los nombres reales de fila que arma `BuildNearbySquadList` son `$"NearbyRow_{squad[i].DisplayName}"` (línea 3197), con hijos `"Label"` (Text) y `"HealthBG/HealthFill"` (Image, líneas 3207-3239).
1. Agregar una constante en `NearbySquadListView`:
   ```csharp
   const string RowPrefix = "NearbyRow_";
   ```
2. Reescribir `Start()`:
   ```csharp
   void Start()
   {
       brain = FindFirstObjectByType<PlayerBrain>();
       if (rows.Count > 0) return;

       var content = transform.Find("Viewport/Content");
       if (content == null) return;

       foreach (Transform rowT in content)
       {
           if (!rowT.name.StartsWith(RowPrefix)) continue;
           string soldierName = rowT.name.Substring(RowPrefix.Length);

           Soldier match = null;
           foreach (var s in ActorRegistry.All)
               if (s != null && s.Team == TeamId.Player && s.DisplayName == soldierName) { match = s; break; }
           if (match == null) continue;

           rows.Add(new Row
           {
               Soldier = match,
               RowObject = rowT.gameObject,
               Label = rowT.Find("Label")?.GetComponent<Text>(),
               HealthFill = rowT.Find("HealthBG/HealthFill")?.GetComponent<Image>(),
           });
       }
   }
   ```
   Esto resuelve el Bug 7 (ahora `HealthFill` se busca explícitamente, igual que `Label`) y de paso el Bug 8 (ya no hay zip ciego por índice: cada fila se empareja por nombre real, y una fila sin soldado correspondiente simplemente se saltea en vez de mezclarse con la fila de al lado).

**Verificación:** Agregar un `Check()` en la fase donde se valida el HUD (por ejemplo junto a los checks de `squadListRef` si existen, o agregar uno nuevo en `RunPhase2`/`RunPhase5`) que fuerce un `Start()` simulado (o llame `OnEnable`/`Start` por reflexión tras limpiar `rows`) y confirme que `HealthFill` no es null para ninguna fila:
```csharp
if (squadListRef != null)
{
    var rowsField = typeof(NearbySquadListView).GetField("rows", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var rowsList = (System.Collections.IList)rowsField.GetValue(squadListRef);
    bool todasConHealthFill = true;
    var healthFillField = rowsList.GetType().GetGenericArguments()[0].GetField("HealthFill");
    foreach (var r in rowsList)
        if (healthFillField.GetValue(r) == null) todasConHealthFill = false;
    Check($"NearbySquadListView: todas las filas reconstruidas tienen HealthFill asignado ({rowsList.Count} filas)", todasConHealthFill && rowsList.Count > 0);
}
```
Como prueba manual en Play mode: entrar a la partida real (no la escena de Editor sin Play), fijarse que la lista de "escuadra cercana" (esquina inferior izquierda) muestra la barra de vida rellenándose/vaciándose, no sólo el número.

**Riesgo/efectos secundarios:** Depende de que `DisplayName` sea único dentro del equipo del jugador (ya es una asunción existente: `SelectedSoldierUI` hace lo mismo por nombre). Si dos soldados del jugador comparten `DisplayName`, el primero que aparezca en `ActorRegistry.All` gana — mismo comportamiento (y misma limitación) que `SelectedSoldierUI` ya tiene hoy, así que no es una regresión nueva. Revisar que `transform.Find("Viewport/Content")` siga siendo la ruta correcta si en algún momento se reordena la jerarquía del panel en `BuildNearbySquadList`.

---

### Bug 8: `NearbySquadListView.Start()` empareja filas y soldados por índice ciego, sin validar estructura

**Archivos:** `UI/NearbySquadListView.cs:41-48`

**Causa raíz:** `var labels = GetComponentsInChildren<Text>(true);` recolecta TODOS los `Text` bajo el panel (sin filtrar por si son realmente labels de fila), y después hace `int n = Mathf.Min(labels.Length, playerSoldiers.Count);` seguido de un `for` que empareja `labels[i]` con `playerSoldiers[i]` puramente por posición. Si el orden en que Unity devuelve los `Text` de la jerarquía no coincide exactamente con el orden de `ActorRegistry.All` filtrado por `TeamId.Player` (que no tiene ninguna garantía de orden estable), o si la cantidad de labels y de soldados no coincide (un soldado murió y se removió del registro entre la construcción de la UI y este `Start()`, o hay algún otro `Text` extra bajo el panel), el resultado es un emparejamiento silenciosamente incorrecto: la fila 2 en pantalla podría mostrar la vida y distancia del soldado 3, o directamente sobrar/faltar filas sin ningún aviso.

**Plan de implementación:** Igual que el Bug 7, se resuelve con el mismo cambio: reemplazar el emparejamiento por índice por un emparejamiento por NOMBRE, recorriendo los hijos reales de `Viewport/Content` (cada uno ya lleva el nombre del soldado incrustado: `"NearbyRow_" + DisplayName`) y buscando el `Soldier` correspondiente en `ActorRegistry.All`, en vez de confiar en que dos colecciones no relacionadas mantengan el mismo orden. Ver el bloque de código completo en el plan del Bug 7 (mismo método, mismo fix). Puntos adicionales específicos de este bug:
1. El nuevo `foreach (Transform rowT in content)` filtra explícitamente `rowT.name.StartsWith(RowPrefix)`, así que cualquier otro hijo no relacionado con una fila de soldado (por ejemplo, si en el futuro se agrega un separador o un título dentro de `Content`) no se cuenta como fila.
2. Si `match == null` (no se encontró un soldado vivo del equipo del jugador con ese nombre — por ejemplo, si `DisplayName` cambiara dinámicamente, cosa que hoy no pasa pero podría), la fila se saltea con `continue` en vez de generar una `Row` con `Soldier = null` que después otros métodos tendrían que null-chequear.

**Verificación:** Mismo `Check()` propuesto en el Bug 7, más un caso adicional que ejercite el escenario de conteos desparejos: matar a un soldado del jugador ANTES de forzar el `Start()`/reconstrucción, y confirmar que la fila correspondiente simplemente no aparece (en vez de que las filas restantes se corran y muestren datos de otro soldado):
```csharp
// Con doc muerta (por ejemplo tras un TakeDamage(999999,-1) de test), confirmar
// que la fila reconstruida de Vega sigue mostrando a VEGA y no a Doc/Kes corridos.
```

**Riesgo/efectos secundarios:** Mismo que el Bug 7 (dependencia de `DisplayName` único). Ambos bugs comparten el mismo fix, así que conviene implementarlos y verificarlos juntos en el mismo cambio a `Start()`.

---

### Bug 9: `ModeToastView` y `PhaseBannerView` no tienen `OnDisable()` — la corrutina de fade/punch queda a mitad de camino

**Archivos:** `UI/ModeToastView.cs` (archivo completo) y `UI/PhaseBannerView.cs` (archivo completo). Referencia de la solución ya aplicada en el proyecto: `UI/ScreenFlashView.cs:79-88` (`OnDisable`).

**Causa raíz:** Ninguna de las dos vistas implementa `OnDisable()`. Cuando Unity desactiva el `GameObject` que corre la corrutina (`StartCoroutine(FadeOut(...))` en `ModeToastView`, `StartCoroutine(PunchAndHide(...))` en `PhaseBannerView`), la corrutina se mata en seco sin llegar a su línea final — el `alpha` del `CanvasGroup` o la `localScale` del `RectTransform` quedan clavados en el valor intermedio del último frame ejecutado. Al reactivar el `GameObject` más tarde, ese estado "a mitad de camino" reaparece congelado: un toast semi-transparente pegado en pantalla, o un banner de fase con una escala rara, sin que nadie lo haya vuelto a mostrar. `ScreenFlashView.OnDisable()` (líneas 79-88) ya resuelve exactamente este problema para su propio caso: `StopAllCoroutines(); SetAlpha(0f); if (Instance == this) Instance = null;` — el comentario ahí mismo lo explica: "Desactivar el GameObject mata las corrutinas en seco... el alfa se quedaba clavado en el pico y la pantalla entera aparecía blanca al reactivar el HUD (pasa de verdad al abrir la pantalla de victoria)."

**Plan de implementación:**
1. En `ModeToastView.cs`, agregar (junto a `OnEnable()`, línea 23-27):
   ```csharp
   void OnDisable()
   {
       StopAllCoroutines();
       routine = null;
       if (group != null) group.alpha = 0f;
   }
   ```
2. En `PhaseBannerView.cs`, agregar (junto a `Show()`):
   ```csharp
   void OnDisable()
   {
       StopAllCoroutines();
       if (rt != null) rt.localScale = Vector3.one;
       if (label != null) label.gameObject.SetActive(false);
   }
   ```
   El estado "limpio" para `PhaseBannerView` es escala 1 (neutra) y el texto desactivado — el mismo estado final al que llega `PunchAndHide` cuando termina naturalmente (línea 45-46: `yield return ScaleOver(1f, 0.2f, 0.3f); label.gameObject.SetActive(false);`), salvo que acá se fuerza de una sola vez en vez de animado, porque el `GameObject` ya se está desactivando y no tiene sentido animar algo que no se va a ver.
3. En ambos casos, no hace falta guardar/restaurar nada más: `OnEnable()` ya vuelve a buscar `label`/`rt`/`group` si hicieran falta, y `Show()` vuelve a arrancar todo desde cero la próxima vez que se llame.

**Verificación:** Agregar `Check()`s de regresión (mismo estilo que el que ya existe para `ScreenFlashView` si lo hay, o nuevo) en `RunPhase5`/`RunPhase7`:
```csharp
if (modeToastRef != null)
{
    modeToastRef.gameObject.SetActive(true);
    modeToastRef.Show("VISTA RTS", 5f); // fade largo a proposito, para pescarlo a mitad de camino
    // Sin avanzar el tiempo (Edit mode no corre corrutinas), desactivar YA:
    modeToastRef.gameObject.SetActive(false);
    modeToastRef.gameObject.SetActive(true);
    var groupField = typeof(ModeToastView).GetField("group", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var g = (CanvasGroup)groupField.GetValue(modeToastRef);
    Check($"ModeToastView: tras des/reactivar a mitad de un fundido, el alpha queda en 0 (no clavado a mitad) ({g.alpha})", Mathf.Approximately(g.alpha, 0f));
}
```
Como prueba manual en Play mode: disparar un cambio de vista FPS/RTS (que dispara `ModeToastView.Show`) o completar una fase (que dispara `PhaseBannerView.Show`), y en pleno fundido pausar el juego o cambiar de escena/desactivar el HUD; al volver, confirmar que no quedó nada semitransparente o a mitad de escala pegado en pantalla.

**Riesgo/efectos secundarios:** Ninguno esperado — es el mismo patrón ya probado en `ScreenFlashView`. Verificar que `StopAllCoroutines()` en `OnDisable()` no interfiera con otras corrutinas si en el futuro estas clases llegaran a correr más de una a la vez (hoy no es el caso: cada una guarda una sola referencia `routine`/usa `StopAllCoroutines` de forma consistente en `Show()`).

---

### Bug 10: `ModeToastView.Bind` y `PhaseBannerView.Bind` no validan `null` antes de dereferenciar el parámetro

**Archivos:** `UI/ModeToastView.cs:20` y `UI/PhaseBannerView.cs:17`

**Causa raíz:** `ModeToastView.Bind(Text text, CanvasGroup canvasGroup)` hace `group.alpha = 0f;` en la línea 20 sin comprobar antes que `canvasGroup` no sea `null` — si algún día se llama `Bind(null, null)` (por ejemplo por un cambio futuro en cómo `HeadlessTestRunner` arma la escena, o un `Bind` manual incompleto desde el Editor), esto tira `NullReferenceException` inmediatamente. Lo mismo en `PhaseBannerView.Bind(Text text)`, línea 17: `rt = text.rectTransform;` dereferencia `text` sin chequeo. El propio archivo `ScreenFlashView.cs` (líneas 49-52) ya muestra la convención correcta para este mismo tipo de método: `public void Bind(Image image) { flash = image; if (flash == null) return; ... }` — guardar la referencia primero y cortar temprano si vino `null`, en vez de asumir que siempre viene un valor válido.

**Plan de implementación:**
1. En `ModeToastView.Bind`:
   ```csharp
   public void Bind(Text text, CanvasGroup canvasGroup)
   {
       label = text;
       group = canvasGroup;
       if (group == null) return;
       group.alpha = 0f;
   }
   ```
2. En `PhaseBannerView.Bind`:
   ```csharp
   public void Bind(Text text)
   {
       if (text == null) return;
       label = text;
       rt = text.rectTransform;
       text.gameObject.SetActive(false);
   }
   ```
   Ojo con el orden: en `PhaseBannerView` conviene cortar ANTES de asignar `label`, porque si `text` es `null` no hay nada útil que guardar y así se deja el campo en su estado previo (probablemente también `null`, coherente con lo que ya hace `Show()` al re-buscarlo por `GetComponentInChildren<Text>(true)` si `label` sigue siendo `null`).

**Verificación:** Agregar dos `Check()`s puros (no necesitan Play mode) cerca de los demás checks de construcción de UI:
```csharp
var toastTest = new GameObject("ModeToastNullBindTest").AddComponent<SP.UI.ModeToastView>();
bool toastThrew = false;
try { toastTest.Bind(null, null); } catch { toastThrew = true; }
Check("ModeToastView.Bind(null, null) no explota", !toastThrew);

var bannerTest = new GameObject("PhaseBannerNullBindTest").AddComponent<SP.UI.PhaseBannerView>();
bool bannerThrew = false;
try { bannerTest.Bind(null); } catch { bannerThrew = true; }
Check("PhaseBannerView.Bind(null) no explota", !bannerThrew);
```

**Riesgo/efectos secundarios:** Ninguno — en el camino real ambos `Bind()` siempre reciben referencias válidas (`HeadlessTestRunner.cs` líneas ~2901 y ~3150), así que el fix es puramente defensivo y no cambia ningún comportamiento visible hoy.

---

### Bug 11: `SelectedSoldierUI.Row.Background` sin null-check en la reconstrucción de escena, y `Refresh()` lo dereferencia sin chequear

**Archivos:** `UI/SelectedSoldierUI.cs:172,219,224`

**Causa raíz:** En el fallback de reconstrucción de `OnEnable()` (línea 158-177), `Label` y `HealthFill` usan el patrón seguro `child.Find(...)?.GetComponent<T>()` (líneas 173-174), que devuelve `null` sin explotar si el hijo no existe. `Background`, en cambio, usa `child.GetComponent<Image>()` (línea 172) sobre el propio `child` (la fila), lo cual en sí mismo no tira excepción — pero como no hay ningún control posterior de que el resultado no sea `null`, el `Row` se agrega igual a `rows` aunque `Background` haya quedado en `null`. El problema real aparece en `Refresh()` (líneas 210-226): ahí, a diferencia de `LateUpdate()` (que sí chequea `if (!alive && row.Background != null)` en la línea 142), las dos únicas escrituras a `row.Background.color` (línea 219, rama de soldado caído, y línea 224, rama normal) lo dereferencian sin ningún `!= null`. Si algún día la fila raíz deja de tener su propio `Image` (por ejemplo si el fondo se mueve a un hijo, como ya pasa con `Label`/`HealthFill`), `Refresh()` tira `NullReferenceException` la primera vez que se llama.

**Plan de implementación:**
1. Agregar los null-checks que faltan en `Refresh()`, siguiendo el mismo patrón defensivo que ya usa `LateUpdate()` en la línea 142:
   ```csharp
   void Refresh()
   {
       foreach (var row in rows)
       {
           if (row.Background == null) continue; // sin fondo no hay nada que pintar, y sin este check Refresh() explota
           if (row.Soldier != null && row.Soldier.Health != null && !row.Soldier.Health.IsAlive)
           {
               row.Background.color = deadColor;
               continue;
           }
           bool isPossessed = row.SoldierId == possessedId;
           bool isSelected = selectedIds.Contains(row.SoldierId);
           row.Background.color = isPossessed ? possessedColor : (isSelected ? selectedColor : normalColor);
       }
   }
   ```
2. Como refuerzo en el origen (`OnEnable()`, línea 158-177), avisar en el log si una fila reconstruida quedó sin `Background`, para que el problema de jerarquía se note en consola en vez de fallar en silencio más adelante:
   ```csharp
   foreach (Transform child in transform)
   {
       if (!child.name.StartsWith("Row_")) continue;
       string soldierName = child.name.Substring(4);

       Soldier match = null;
       foreach (var s in ActorRegistry.All)
           if (s != null && s.DisplayName == soldierName) { match = s; break; }
       if (match == null) continue;

       var background = child.GetComponent<Image>();
       if (background == null)
           Debug.LogWarning($"[SelectedSoldierUI] La fila '{child.name}' no tiene Image en su GameObject raiz; queda sin resaltado de posesion/seleccion.");

       rows.Add(new Row
       {
           SoldierId = match.Id,
           Soldier = match,
           Background = background,
           Label = child.Find("Label")?.GetComponent<Text>(),
           HealthFill = child.Find("BarBG/BarFill")?.GetComponent<Image>(),
           Brain = match.GetComponent<AiBrain>(),
       });
   }
   ```

**Verificación:** Agregar un `Check()` que arme una fila con `Background = null` a propósito y confirme que `Refresh()` (invocado indirectamente vía `OnPossession`/`OnSelection`, o directo por reflexión) no explota:
```csharp
if (rosterUiRef != null)
{
    var rowsField = typeof(SelectedSoldierUI).GetField("rows", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var refreshMethod = typeof(SelectedSoldierUI).GetMethod("Refresh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    // (construir una fila de prueba con Background=null vía reflexion sobre el tipo anidado Row, o
    //  agregar temporalmente una fila invalida a una instancia de test)
    bool threw = false;
    try { refreshMethod.Invoke(rosterUiRef, null); } catch { threw = true; }
    Check("SelectedSoldierUI.Refresh() no explota con una fila sin Background", !threw);
}
```
Como prueba manual: no debería ser observable en el escenario real (siempre hay `Image` en la raíz de cada `Row_<Nombre>`); el valor del fix es puramente defensivo para cuando cambie la jerarquía del prefab de fila.

**Riesgo/efectos secundarios:** Con el `continue` agregado en `Refresh()`, una fila sin `Background` deja de recibir CUALQUIER color (ni resaltado de posesión/selección ni gris de caído) — es el comportamiento correcto (no hay nada que pintar), pero confirmar que no rompe ninguna otra suposición aguas abajo (por ejemplo, si algo más adelante asume que todas las filas en `rows` tienen `Background` no nulo).

---

### Bug 12: `ControlsTable` no lista 8 atajos reales y funcionando — el panel [H] nunca los menciona

**Archivos:** `UI/ControlsTable.cs` (array `Entries`, líneas 103-163)

**Causa raíz:** `ControlsTable.Entries` se declara como "inventario literal de las lecturas de teclado... no hay atajos 'de diseño' que el código no lea" (comentario, líneas 46-48), pero eso dejó de ser cierto para 8 teclas que sí están implementadas y funcionando en `PlayerInputDriver.cs` y `KeyBindings.cs`, y que simplemente nunca se agregaron a esta tabla:
- **[U]** monta a un aliado de a uno (`PlayerInputDriver.cs:780` en `UpdateFps`, y de nuevo en `UpdateInVehicle` línea 1362 — funciona tanto a pie como estando adentro del vehículo).
- **[I]** baja a todos los ocupantes del vehículo (`PlayerInputDriver.cs:790`, sólo en `UpdateFps`/a pie).
- **[Y]** Reagrupar la selección dispersa (`KeyBindings.Reagrupar`, usado en `PlayerInputDriver.cs:1768` dentro del bloque RTS).
- **[B]** Retirada: alejar a la selección del enemigo más cercano (`KeyBindings.Retirada`, `PlayerInputDriver.cs:1775`).
- **[K]** Ciclar la formación con la que se emiten órdenes (`KeyBindings.CiclarFormacion`, `PlayerInputDriver.cs:1782`).
- **[J]** Seleccionar sólo a los heridos (`KeyBindings.SeleccionarHeridos`, `PlayerInputDriver.cs:1789`).
- **[N]** Seleccionar a todos los del mismo tipo en pantalla (`KeyBindings.SeleccionarMismoTipo`, `PlayerInputDriver.cs:1795`).
- **[Z]** Ciclar la posesión hacia atrás (`KeyBindings.CiclarPosesionAtras`, `PlayerInputDriver.cs:425`, junto a [Q] que sí está listada).

Como `ControlsTable.FullText()` es la única fuente del panel de pausa → Controles (`HeadlessTestRunner.cs:3534`: `controlsListTxt.text = SP.UI.ControlsTable.FullText();`), estas 8 teclas son invisibles para el jugador aunque respondan perfectamente si las aprieta — sólo se pueden descubrir por accidente o leyendo el código.

**Plan de implementación:**
1. Todas estas acciones ya tienen sus IDs y su tecla default en `KeyBindings.cs` (`Reagrupar`, `Retirada`, `CiclarFormacion`, `SeleccionarHeridos`, `SeleccionarMismoTipo`, `CiclarPosesionAtras`) salvo U/I, que están hardcodeadas (`kb.uKey`/`kb.iKey`) y no pasan por `KeyBindings` — esto no se resuelve en este bug (no está en el alcance de los 14 bugs), sólo se documentan tal cual funcionan hoy.
2. Insertar las 8 entradas nuevas en el array `Entries`, respetando el orden por relevancia descendente que ya usa la tabla (`LineFor` corta por arriba). El lugar natural es justo después de las entradas de Q/C/F1-F2-F3 (línea 156-158) y antes de H/ESC/Clic (línea 160-163), para no desplazar a las 8 primeras entradas de `ControlContext.FpsAPie`/`ControlContext.Rts` que sí se muestran en el cartel contextual corto (`DefaultLineEntries = 7`, línea 66):
   ```csharp
   new ControlEntry("Q", "ciclar la posesion al siguiente aliado vivo", APieOTactico),
   new ControlEntry("Z", "ciclar la posesion al aliado vivo anterior", APieOTactico),
   new ControlEntry("C", "poseer al aliado vivo mas cercano", APieOTactico),
   new ControlEntry("F1/F2/F3", "poseer directamente al soldado 1, 2 o 3 de la escuadra", APieOTactico),

   new ControlEntry("U", "ordenarle a un aliado que suba al vehiculo, de a uno", ControlContext.FpsAPie | AdentroDelVehiculo),
   new ControlEntry("I", "bajar a todos los aliados del vehiculo", ControlContext.FpsAPie),

   new ControlEntry("Y", "reagrupar a la seleccion dispersa", ControlContext.Rts),
   new ControlEntry("B", "retirada: alejar a la seleccion del enemigo mas cercano", ControlContext.Rts),
   new ControlEntry("K", "ciclar la formacion con la que se emiten las ordenes", ControlContext.Rts),
   new ControlEntry("J", "seleccionar solo a los heridos", ControlContext.Rts),
   new ControlEntry("N", "seleccionar a todos los del mismo tipo en pantalla", ControlContext.Rts),

   new ControlEntry("H", "abrir y cerrar esta lista de controles sin pausar el juego", Todos),
   new ControlEntry("ESC", "pausa y libera el cursor; dentro de los menus vuelve un paso atras", Todos),
   new ControlEntry("Clic", "capturar el cursor para poder mirar con el mouse", AsientosFps | ControlContext.FpsAPie)
   ```
   (La entrada `Z` se agrega junto a `Q` porque ambas comparten el mismo contexto `APieOTactico` y la misma familia de acción — "ciclar posesión" adelante/atrás —, coherente con cómo la tabla ya agrupa entradas relacionadas, p. ej. WASD con sus 3 variantes de contexto seguidas en líneas 107-109.)
2. No hace falta tocar `AllContexts`, `Todos`, `AdentroDelVehiculo`, `APieOTactico` ni ninguna otra constante: los 6 contextos y los helpers ya definidos alcanzan para las 8 entradas nuevas.
3. Repasar `KeyRebindView.nombres` (diccionario de nombres legibles, líneas 124-144 de `KeyRebindView.cs`): ya incluye `Reagrupar`, `Retirada`, `CiclarFormacion`, `SeleccionarHeridos`, `SeleccionarMismoTipo` y `CiclarPosesionAtras` — así que el panel de remapeo YA permite reasignar estas 5 teclas, sólo faltaba que `ControlsTable` las mencionara. U/I no aparecen ahí porque no son remapeables hoy (hardcodeadas) — coherente, no se agregan al diccionario de remapeo en este bug.

**Verificación:** El propio `ControlsTable.Validate()` ya corre en `RunPhase5` (`Check("ControlsTable.Validate no encuentra huecos", ...)`, línea 1268 de `HeadlessTestRunner.cs`) y sigue pasando con las entradas nuevas (cada una declara un contexto no vacío y tiene `Key`/`Description`). Agregar un `Check()` más específico que confirme que las 8 teclas están presentes:
```csharp
string[] teclasEsperadas = { "U", "I", "Y", "B", "K", "J", "N", "Z" };
bool todasPresentes = true;
foreach (var tecla in teclasEsperadas)
{
    bool encontrada = false;
    foreach (var ctx in SP.UI.ControlsTable.AllContexts)
        foreach (var e in SP.UI.ControlsTable.For(ctx))
            if (e.Key == tecla) { encontrada = true; break; }
    if (!encontrada) { todasPresentes = false; TestLog.Warn($"Falta la tecla [{tecla}] en ControlsTable"); }
}
Check("ControlsTable incluye las 8 teclas reales que faltaban (U/I/Y/B/K/J/N/Z)", todasPresentes);
```
Como prueba manual: abrir Pausa -> Controles en Play mode y confirmar visualmente que las 8 líneas nuevas aparecen bajo sus encabezados correspondientes ("A PIE (FPS)"/vehículo para U/I, "VISTA TÁCTICA (RTS)" para Y/B/K/J/N/Z).

**Riesgo/efectos secundarios:** Agregar entradas cambia el contenido de `FullText()` (panel de pausa) y potencialmente el recorte de `LineFor(ctx, DefaultLineEntries)` si se insertan ANTES de la posición 7 de algún contexto que hoy ya tiene 7 o más entradas — por eso el plan las inserta después de las últimas entradas de `APieOTactico`/`Rts` que hoy se muestran, no en cualquier lugar. Antes de mergear, revisar a ojo qué 7 entradas quedan en el cartel contextual corto de RTS (`LineFor(ControlContext.Rts)`) y confirmar que siguen siendo las más relevantes (arrastrar/seleccionar/clic/T, no las nuevas Y/B/K/J/N, que son más avanzadas) — si el orden actual ya las deja afuera del corte de 7, no hace falta tocar nada más.

---

### Bug 13 (SISTÉMICO — el de mayor impacto de esta sección): 7 vistas usan `Time.deltaTime`/`WaitForSeconds` en vez de la versión "unscaled", y se congelan a mitad de animación en cada victoria/derrota

**Archivos afectados (los 7, cada uno con su método y línea exacta):**
- `UI/AimUI.cs:323` — `FlashHitMarker`
- `UI/KillFeedView.cs:70,82,91,97` — `PunchAndFade`
- `UI/DamageVignetteView.cs:99` — `FlashAndFade`
- `UI/DamageDirectionView.cs:94` — `ShowAndHide`
- `UI/ModeToastView.cs:42,48` — `FadeOut`
- `UI/PhaseBannerView.cs:44,54` — `PunchAndHide` (línea 44 es el `WaitForSeconds`, línea 54 está en el helper `ScaleOver` que llama dos veces)
- `UI/DeadNoticeView.cs:47,53` — `FadeOut`

**Referencia de la solución ya aplicada:** `UI/ScreenFlashView.cs:141-157` (`FadeOut`), que ya usa `Time.unscaledDeltaTime` con este comentario explícito (líneas 143-149): *"unscaledDeltaTime, NO deltaTime: KillFeedbackDirector pone timeScale en 0.25 en la última baja (el destello duraba 4 veces más de lo debido) y GameOutcomeController lo pone en 0 en las pantallas finales (el destello quedaba CONGELADO tapando la pantalla de victoria, sin nada que lo volviera a bajar)."*

**Causa raíz:** Tres sistemas del proyecto tocan `Time.timeScale` en momentos donde el HUD sigue con animaciones en curso: `GameOutcomeController.cs:151,163` lo pone en `0f` al mostrar victoria/derrota; `KillFeedbackDirector.cs:184` lo pone en `SlowMotionScale` (0.25) en la cámara lenta de la última baja; `PauseController.cs:254` lo pone en `0f` al pausar con ESC. Las 7 vistas de esta lista miden el paso del tiempo de sus corrutinas con `Time.deltaTime` (que ES `Time.timeScale`-dependiente) y, cuando corresponde, esperan con `new WaitForSeconds(...)` (que también respeta `timeScale`). Con `timeScale = 0`, `Time.deltaTime` vale `0` en cada frame: el bucle `while (t < duration) { t += Time.deltaTime; ...; yield return null; }` nunca avanza `t`, así que la animación queda visiblemente congelada a mitad de camino, tapando la pantalla de resultado, en TODAS las partidas (victoria y derrota pasan siempre). Con `timeScale = 0.25` (última baja en cámara lenta), la animación no se congela pero dura 4 veces más de lo diseñado.

**Plan de implementación — el mismo cambio mecánico en los 7 archivos, siguiendo `ScreenFlashView` al pie de la letra:**

1. **`AimUI.cs`, `FlashHitMarker` (línea 308-337):** en el bucle de la línea 320-332, cambiar la línea 323:
   ```csharp
   // antes: t += Time.deltaTime;
   t += Time.unscaledDeltaTime;
   ```
   (No usa `WaitForSeconds`, así que es el único cambio necesario en este método.)

2. **`KillFeedView.cs`, `PunchAndFade` (línea 47-104):** hay TRES bucles con `Time.deltaTime` (líneas 70, 82, 97) y un `WaitForSeconds` (línea 91):
   ```csharp
   // linea 70 (punch):   t += Time.unscaledDeltaTime;
   // linea 82 (settle):  t += Time.unscaledDeltaTime;
   // linea 91:           yield return new WaitForSecondsRealtime(holdTime);
   // linea 97 (fade):    t += Time.unscaledDeltaTime;
   ```

3. **`DamageVignetteView.cs`, `FlashAndFade` (línea 87-108):** un solo bucle, línea 99:
   ```csharp
   t += Time.unscaledDeltaTime;
   ```

4. **`DamageDirectionView.cs`, `ShowAndHide` (línea 86-99):** un solo bucle, línea 94:
   ```csharp
   t += Time.unscaledDeltaTime;
   ```

5. **`ModeToastView.cs`, `FadeOut` (línea 37-53):** dos bucles, líneas 42 y 48:
   ```csharp
   // linea 42 (hold): while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }
   // linea 48 (fade): t += Time.unscaledDeltaTime;
   ```

6. **`PhaseBannerView.cs`, `PunchAndHide` + `ScaleOver` (línea 40-59):**
   ```csharp
   // linea 44: yield return new WaitForSecondsRealtime(holdSeconds);
   // linea 54 (dentro de ScaleOver, compartido por las dos llamadas de PunchAndHide): t += Time.unscaledDeltaTime;
   ```

7. **`DeadNoticeView.cs`, `FadeOut` (línea 42-58):** dos bucles, líneas 47 y 53:
   ```csharp
   // linea 47 (hold): while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }
   // linea 53 (fade): t += Time.unscaledDeltaTime;
   ```
   (Si se aplica junto con el fix del Bug 3, este cambio se hace sobre el método ya renombrado/reorganizado, pero la sustitución `deltaTime` → `unscaledDeltaTime` y `WaitForSeconds` → `WaitForSecondsRealtime` es idéntica.)

**Verificación:** Agregar un bloque de `Check()`s en `RunPhase5` o una fase dedicada que confirme, para cada vista, que su animación SIGUE avanzando con `Time.timeScale = 0` — el mismo enfoque que ya usaría cualquier test de `ScreenFlashView` si existiera uno explícito. Como las corrutinas sólo corren en Play mode real (`Application.isPlaying`), la verificación más confiable acá es una secuencia manual en Play mode, pero se puede blindar la REGLA con un chequeo estático simple que evite que el bug vuelva a colarse:
```csharp
// Chequeo de regresion barato: ninguno de estos 7 archivos deberia volver a
// tener "Time.deltaTime" o "WaitForSeconds(" (sin Realtime) en las corrutinas
// de fade/punch. Sirve como red de seguridad textual, no reemplaza probarlo
// en Play mode real con timeScale en 0.
string[] archivosAFiltrar = {
    "Assets/_Project/Scripts/UI/AimUI.cs",
    "Assets/_Project/Scripts/UI/KillFeedView.cs",
    "Assets/_Project/Scripts/UI/DamageVignetteView.cs",
    "Assets/_Project/Scripts/UI/DamageDirectionView.cs",
    "Assets/_Project/Scripts/UI/ModeToastView.cs",
    "Assets/_Project/Scripts/UI/PhaseBannerView.cs",
    "Assets/_Project/Scripts/UI/DeadNoticeView.cs",
};
// (implementar como un chequeo de texto simple sobre el contenido de cada archivo,
//  o -- mas robusto -- exponer un flag de test en cada vista que la corrutina
//  marque como "usé unscaled" la primera vez que corre.)
```
La prueba real y decisiva es en Play mode: jugar hasta ganar o perder una partida (`GameOutcomeController` fuerza `timeScale = 0`) justo en el instante en que cualquiera de estas 7 animaciones está en curso (por ejemplo, un impacto de bala justo antes de morir, o el "SOLDADO ABATIDO" del `KillFeedView` de la última baja) y confirmar que la animación TERMINA su ciclo (llega a alpha/escala final) en vez de quedar congelada tapando la pantalla de victoria/derrota. Repetir provocando la cámara lenta de la última baja (`KillFeedbackDirector.SlowMotionScale = 0.25`) y confirmar que la duración se siente igual que siempre, no 4 veces más larga.

**Riesgo/efectos secundarios:** Cambiar a `unscaledDeltaTime` significa que estas animaciones YA NO se ralentizan durante la cámara lenta de la última baja ni se congelan en pausa/pantallas finales — es el comportamiento buscado, pero repasar que ninguna de las 7 dependa a propósito de sincronizarse con el `timeScale` reducido (por ejemplo, si el "punch" de `KillFeedView` estaba pensado para verse más dramático y lento durante la slow-mo de la última baja, este fix lo vuelve a su duración normal real-time — que es justamente lo que pide el bug, pero vale confirmarlo con quien diseñó ese efecto). También revisar `WaitForSecondsRealtime` vs `WaitForSeconds`: son clases distintas de Unity, no hace falta ningún `using` adicional (`UnityEngine` ya cubre ambas), pero sí hay que tipear el nombre completo correctamente en cada `yield return`.

---

### Bug 14: `NearbySquadListView` calcula `fillAmount` sin guardia `MaxHealth > 0`

**Archivos:** `UI/NearbySquadListView.cs:71`

**Causa raíz:** `row.HealthFill.fillAmount = (float)row.Soldier.Health.Current / row.Soldier.Health.MaxHealth;` divide directo por `MaxHealth` sin comprobar que sea mayor a cero. Si algún `Soldier` llegara a tener `MaxHealth == 0` (configuración de test, un prefab mal armado, o cualquier vía que hoy no está blindada), el resultado es `NaN`, y asignar `NaN` a `Image.fillAmount` dejaba la barra en un estado visual indefinido. Cada otra barra de vida del proyecto ya se cuida de esto: `SelectedSoldierUI.cs:130` hace `if (alive && row.Soldier.Health.MaxHealth > 0)` antes de calcular `frac`; `PlayerHealthView`, `VehicleStatusView`, `WeaponStatusView` y `GroupCardsView` siguen el mismo patrón. `NearbySquadListView` es la única vista de barra de vida del proyecto que quedó afuera de esa convención.

**Plan de implementación:**
1. En `LateUpdate()` (líneas 51-73), envolver la asignación de `fillAmount` con el mismo guardia que ya usa `SelectedSoldierUI.cs:130`:
   ```csharp
   if (row.HealthFill != null && row.Soldier.Health.MaxHealth > 0)
       row.HealthFill.fillAmount = (float)row.Soldier.Health.Current / row.Soldier.Health.MaxHealth;
   ```
   (Cambio mínimo: se agrega `&& row.Soldier.Health.MaxHealth > 0` a la condición ya existente en la línea 70, sin tocar nada más del método.)

**Verificación:** Agregar un `Check()` en `RunPhase5`/`RunPhase2` que arme una fila de `NearbySquadListView` con un soldado de `MaxHealth == 0` (vía un `Soldier`/`Health` de test, o simulando el campo por reflexión si `Health.MaxHealth` no tiene setter público) y confirme que `fillAmount` nunca es `NaN`:
```csharp
if (squadListRef != null)
{
    // Construir o reusar una Row con MaxHealth=0 y forzar un LateUpdate:
    // ... (via reflexion sobre el campo privado `rows`, agregando una entrada de prueba)
    Check("NearbySquadListView: HealthFill.fillAmount nunca es NaN con MaxHealth=0",
        !float.IsNaN(/* fillAmount leido tras el LateUpdate forzado */ 0f));
}
```
Si construir el escenario de `MaxHealth == 0` resulta forzado dentro del arnés existente, alcanza con una prueba unitaria más directa del cálculo aislado (extraer la fracción a un método estático puro como hacen `SelectionController.IsWounded`/`GroupCardsView.Summarize`, que ya se prueban así en `RunPhase5` líneas 1234-1237 y 1271-1277) — por ejemplo:
```csharp
public static float HealthFraction(int current, int max) => max > 0 ? (float)current / max : 0f;
```
y then: `Check("HealthFraction con max=0 no da NaN", !float.IsNaN(SP.UI.NearbySquadListView.HealthFraction(10, 0)));`

**Riesgo/efectos secundarios:** Ninguno — `MaxHealth == 0` no ocurre hoy en el flujo real de juego (todos los soldados se crean con vida > 0), así que el fix es puramente defensivo, igual que el resto de las vistas que ya lo hacen. Si se opta por extraer el método estático `HealthFraction` para hacerlo testeable de forma aislada (recomendado, seggún la convención de `IsWounded`/`SelectVictim`/`ShouldIgnoreCapture` de los bugs anteriores), aplicar el mismo cambio en el único call site de `LateUpdate()`.


---

# Editor / Tooling de testing (HeadlessTestRunner.cs) — Planes de corrección (7 bugs)

> Archivo auditado: `Assets/_Project/Scripts/Editor/HeadlessTestRunner.cs` (~3900 líneas).
> Todas las referencias de línea están verificadas contra el archivo actual.

---

### Bug 1: Check tautológico en RunPhase1 — "el proyectil volvió al pool" NUNCA puede fallar

**⚠️ EL MÁS GRAVE DE LOS 7.** Es un assert que jamás detecta una regresión real, aunque el pool esté completamente roto.

**Línea(s):** 835-837 (dentro de `RunPhase1`)

**Causa raíz:** `int freeBefore = pool.FreeCount;` se captura en la línea 835, **después** de que `vega.Weapon.TryFire(...)` (línea 832) ya consumió un proyectil del pool. O sea que `freeBefore` ya refleja el conteo *posterior* al disparo (el pool ya está "una unidad abajo"). Como en la ventana de `SimulateSeconds(3.2f)` nada más toca este pool (el enemigo recién se crea en la línea 839, después del check), `pool.FreeCount` solo puede quedarse igual a `freeBefore` (si el proyectil nunca vuelve) o subir (si vuelve). El check `pool.FreeCount >= freeBefore` es entonces **siempre verdadero** — literalmente no existe ningún estado del sistema bajo prueba que lo haga fallar.

**Plan de implementación:**
1. Mover la captura de `freeBefore` para que ocurra **antes** de disparar, no después. Insertar `int freeBefore = pool.FreeCount;` inmediatamente antes de la línea 832 (`bool fired = vega.Weapon.TryFire(...)`), en vez de en la línea 835.
2. Cambiar la comparación posterior de `>=` a `==` (igualdad exacta), ya que con `freeBefore` capturado antes de disparar, el pool debería volver exactamente al mismo nivel tras el `SimulateSeconds(3.2f)` — no solo "no haber bajado más".
3. Código resultante (reemplazando las líneas 832-837):
   ```csharp
   int freeBefore = pool.FreeCount;
   bool fired = vega.Weapon.TryFire(vega.transform.position, vega.transform.forward);
   Check($"Disparo: se creo proyectil de {vega.DisplayName} con exito (click)", fired);

   SimulateSeconds(3.2f);
   Check("El proyectil volvio al pool", pool.FreeCount == freeBefore);
   ```
4. Opcional pero recomendable: agregar un check intermedio inmediatamente después de `fired` que confirme que el pool efectivamente bajó al disparar (`pool.FreeCount == freeBefore - 1`), para separar con claridad "se tomó del pool" de "se devolvió al pool" — dos afirmaciones distintas que hoy están mezcladas en una sola variable ambigua.

**Verificación:** Correr `Strategic Point/Construir nivel y correr test` (`RunAll()`) y revisar la consola: el check "El proyectil volvio al pool" debe seguir en verde con el comportamiento actual del juego (el proyectil sí vuelve al pool tras su vida útil). Para confirmar que el fix realmente "muerde", comentar temporalmente la llamada a `pool.Release(...)` dentro de `Projectile` (o el código que la dispara al expirar) y re-correr: ahora el check DEBE fallar y aparecer en `failedCheckMessages` / `FailedCheckCount > 0`, cosa que con el código viejo jamás pasaba.

**Riesgo/efectos secundarios:** Con `==` en vez de `>=` el check es más estricto: si por cualquier motivo el pool creciera por otra vía en esa ventana (por ejemplo si `SimulateSeconds` llegara a disparar alguna otra fuente que libere proyectiles sin haberlos tomado del pool — no debería pasar, pero conviene revisarlo), el check podría fallar en falso. Repasar que `SimulateSeconds(3.2f)` en este punto del guion no dispare ningún otro sistema que toque `pool` (en este momento de la Fase 1 todavía no existe `enemy1`, así que no hay IA enemiga disparando). Correr la suite completa una vez para confirmar que no aparecen fallos nuevos e inesperados en este check.

---

### Bug 2: Materiales y RenderTexture huérfanos — fugas de memoria nativa en cada rebuild

**Línea(s):** 2041, 2056 (`SpawnVehicle`), 2206 (`BuildGround`), 2228 (`BuildObstacles`), 2245 (`BuildLightProps`), 2268 (`BuildWeaponPickups`), 3062 (`BuildTurretAimUI`), 3701-3702 (`BuildMinimap`)

**Causa raíz:** Estas 7 llamadas a `CreateFlatMaterial(...)` (que internamente hace `new Material(...)`) y la línea 3701 que hace `new RenderTexture(384, 384, 16)` crean objetos nativos de Unity que quedan **asignados** a un `Renderer`/`LineRenderer`/`RawImage` de la escena, pero nunca guardados como asset en disco (a diferencia de los materiales de `BuildAndSaveSoldierPrefab`/`BuildAndSaveProjectilePrefab`, líneas 1860 y 1941, que sí sobreviven porque `PrefabUtility.SaveAsPrefabAsset` los embebe dentro del `.prefab` persistido). `EditorSceneManager.NewScene(...)` destruye los GameObjects de la escena anterior, pero un `Material`/`RenderTexture` es un objeto nativo independiente del GameObject que lo referenciaba: si nada más lo referencia, Unity no lo destruye automáticamente en Editor (no hay GC determinístico de objetos nativos sin `Destroy`/`DestroyImmediate` explícito). Cada corrida de "Construir nivel y correr test" / "Construir nivel para demo" en la misma sesión de Editor deja atrás un lote nuevo de estos objetos húérfanos.

**Plan de implementación:**
1. Agregar un registro estático de objetos transitorios cerca de `teamMaterials` (línea ~2174):
   ```csharp
   // Materiales/RenderTexture creados para objetos de ESCENA (no assets
   // persistidos como los prefabs) — huerfanos tras EditorSceneManager.NewScene
   // si no se destruyen a mano. Se limpian al arrancar cada rebuild.
   static readonly List<UnityEngine.Object> transientRuntimeAssets = new List<UnityEngine.Object>();

   static void DestroyTransientRuntimeAssets()
   {
       foreach (var obj in transientRuntimeAssets)
           if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
       transientRuntimeAssets.Clear();
   }
   ```
2. En cada uno de los 8 puntos de creación, agregar el objeto recién creado a la lista antes de asignarlo:
   - Línea 2041: `var mat = CreateFlatMaterial(color); transientRuntimeAssets.Add(mat);`
   - Línea 2056: `if (barrelRend != null) { var barrelMat = CreateFlatMaterial(new Color(0.12f, 0.12f, 0.13f)); transientRuntimeAssets.Add(barrelMat); barrelRend.sharedMaterial = barrelMat; }`
   - Línea 2206: separar en variable, `var groundMat = CreateFlatMaterial(...); transientRuntimeAssets.Add(groundMat); ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;`
   - Mismo patrón para 2228, 2245, 2268, 3062.
   - Línea 3701: `var rt = new RenderTexture(384, 384, 16) { name = "RT_Minimap" }; transientRuntimeAssets.Add(rt);`
3. Llamar `DestroyTransientRuntimeAssets();` como **primera línea** de cada uno de los 4 puntos de entrada que reconstruyen el mundo (los únicos que llaman `EditorSceneManager.NewScene`):
   - `BenchmarkWithUnitCount` (antes de la línea 238, `EventBus.Instance.ClearAll();`)
   - `EquivalenceCheckWithUnitCount` (antes de la línea 369)
   - `RunStressScenarios` (antes de la línea 477)
   - `BuildAndRun` (antes de la línea 567)
4. **No** tocar `GetOrCreateTeamMaterial` (línea 2177-2184, usa la línea 2181 que NO está en la lista de bugs): esa caché de materiales por color de equipo es intencional y se reutiliza entre soldados de la misma corrida; si se destruyera junto con el resto se rompería (el diccionario `teamMaterials` seguiría apuntando a un Material destruido — "fake null" — en la corrida siguiente). Tampoco tocar 1860/1941 (materiales embebidos en los prefabs guardados en disco).

**Verificación:** Con el Profiler de memoria de Unity (Window > Analysis > Memory Profiler, o simplemente `Resources.FindObjectsOfTypeAll<Material>().Length` / `<RenderTexture>().Length` tecleado en la consola de C# del Editor) tomar una captura antes y después de correr "Construir nivel y correr test" 3 veces seguidas en la misma sesión. Antes del fix, el conteo de `Material`/`RenderTexture` sube en cada corrida sin techo. Después del fix, el conteo debe quedar estable (no crecer con corridas repetidas) porque cada rebuild limpia lo que dejó la corrida anterior antes de crear lo nuevo. Correr también `RunAll()` completo para confirmar que ningún check falla por un material/textura destruido antes de tiempo (ver riesgo abajo).

**Riesgo/efectos secundarios:** El riesgo principal es un orden incorrecto: si `DestroyTransientRuntimeAssets()` se llamara DESPUÉS de que la escena nueva ya reconstruyó objetos que reutilizan el mismo material (no es el caso acá porque cada rebuild crea materiales nuevos, pero hay que mantenerlo así), se destruirían materiales en uso. Por eso el paso 3 exige que la limpieza sea la primerísima línea de cada función, antes de crear nada nuevo. También revisar que ningún otro código (por ejemplo alguna referencia cacheada estática que sobreviva entre corridas, como `minimapFollowRef` o `turretAimRef`) siga usando el `RenderTexture`/`Material` viejo después de haber sido destruido — como esas referencias son a componentes de GameObjects de la escena anterior (ya destruidos por `NewScene`), no deberían sobrevivir, pero conviene correr la suite dos veces seguidas y confirmar visualmente en el minimapa/UI que no queda nada roto (textura magenta, etc.).

---

### Bug 3: `EquivalenceCheckWithUnitCount` no limpia `Projectile.ActiveInstances`

**Línea(s):** 367-371 (declaración e inicio de `EquivalenceCheckWithUnitCount`)

**Causa raíz:** Los otros 3 puntos de entrada que reconstruyen el mundo desde cero (`BenchmarkWithUnitCount` línea 241, `RunStressScenarios` línea 480, `BuildAndRun` línea 569) limpian explícitamente `Projectile.ActiveInstances.Clear()` junto con `EventBus.Instance.ClearAll()`, `ActorRegistry.Clear()` y `WorldSystemsRegistry.Clear()`. `EquivalenceCheckWithUnitCount` (líneas 369-371) limpia los primeros tres registros pero se olvidó de `Projectile.ActiveInstances` — una simple omisión por copy-paste incompleto entre los 4 métodos, que deberían ser simétricos en su bloque de limpieza inicial.

**Plan de implementación:**
1. Localizar el bloque de limpieza al inicio de `EquivalenceCheckWithUnitCount` (líneas 369-371):
   ```csharp
   EventBus.Instance.ClearAll();
   ActorRegistry.Clear();
   SP.Core.WorldSystemsRegistry.Clear();
   ```
2. Agregar la línea faltante, en el mismo orden que usan los otros 3 métodos (después de `WorldSystemsRegistry.Clear()`):
   ```csharp
   EventBus.Instance.ClearAll();
   ActorRegistry.Clear();
   SP.Core.WorldSystemsRegistry.Clear();
   Projectile.ActiveInstances.Clear();
   ```
3. No hace falta tocar nada más del método: el resto de `EquivalenceCheckWithUnitCount` (construcción de mundo, spawns, `SpatialGrid.Rebuild()`, el bucle de `queries`) ya usa el pool y las unidades recién creadas sin depender de proyectiles previos.

**Verificación:** Correr `Strategic Point/Verificar equivalencia SpatialGrid` (`RunEquivalenceCheck()`) dos veces seguidas en la misma sesión de Editor sin recompilar entremedio, y confirmar en consola `[Equivalencia] ... 0 discrepancias` en ambas corridas. Antes del fix, si algún proyectil de una corrida anterior (de `RunAll()`, `RunPerformanceBenchmarks()` o `RunStressScenarios()`, corridos antes en la misma sesión) quedó registrado en `Projectile.ActiveInstances` apuntando a un GameObject ya destruido por `NewScene`, cualquier código que iterara esa lista (o alguna lógica de `SpatialGrid`/`ActorRegistry` que la consulte indirectamente) podría toparse con una referencia "fake-null". Para reproducir el bug, correr `RunAll()` primero (deja proyectiles activos si se corta en medio de una fase), y sin recompilar, correr enseguida `RunEquivalenceCheck()`: revisar que no haya `MissingReferenceException` ni falsos positivos atribuibles a un proyectil fantasma.

**Riesgo/efectos secundarios:** Bajo. Es una línea que ya se usa igual en otros 3 lugares del mismo archivo, así que el patrón está probado. Único cuidado: confirmar que `Projectile.ActiveInstances` es una colección estática compartida (no por instancia de pool) — si en algún momento cambiara de forma, revisar que `.Clear()` siga siendo válido ahí.

---

### Bug 4: Segundo check tautológico en RunPhase5 — no verifica que `CameraFxSettings.Enabled=false` bloquee `KickDirectional`

**Línea(s):** 1248-1251 (dentro del bloque de `CameraRig` en `RunPhase5`, líneas 1240-1253)

**Causa raíz:** El bloque de las líneas 1240-1246 ya satura `rig.ShakeOffset` al tope (`rig.MaxShakeMagnitude`) tirando 10 "kicks" con los efectos de cámara **encendidos**. El segundo check (líneas 1250-1251) vuelve a comparar `rig.ShakeOffset.magnitude <= rig.MaxShakeMagnitude + 0.001f` — la MISMA cota que ya estaba garantizada de antemano por el clamp del sistema de shake, independientemente de si `CameraFxSettings.Enabled` hace algo o no. Si `KickDirectional` ignorara por completo el flag `Enabled` y siguiera sumando shake con los efectos "apagados", el shake seguiría clampeado al mismo tope y el check pasaría igual — no está midiendo el efecto de apagar los FX, sino re-confirmando un límite que ya era cierto. Es la misma clase de bug que el Bug 1: compara contra una cota ya saturada en vez de comparar un valor antes/después. El patrón CORRECTO ya existe en el propio archivo, en `RunPhase6` (líneas 1534-1538), que sí captura `shakeBefore`/`shakeAfter` y compara igualdad exacta entre ambos.

**Plan de implementación:**
1. Usar como referencia el patrón de `RunPhase6` (líneas 1534-1538):
   ```csharp
   Vector3 shakeBefore = rig.ShakeOffset;
   turret.TryFire();
   Vector3 shakeAfter = rig.ShakeOffset;
   Check("Disparar el cañon con el jugador adentro NO mueve la vibracion de camara (se saco a pedido)",
       shakeAfter == shakeBefore);
   ```
2. Reescribir el bloque de las líneas 1248-1251. Primero hay que dejar que el shake baje del tope antes de medir el "antes", porque si se mide `shakeBefore` justo después de las 10 sacudidas saturadas, seguiría en el máximo y un nuevo kick igual no cambiaría nada visible aunque el bug NO estuviera arreglado (el shake ya está en el techo, así que "no crece" es ambiguo). Hay dos formas de resolverlo, elegir la primera por ser más fiel al comportamiento real del juego:
   - **Opción A (recomendada):** dejar que el shake decaiga primero (si `CameraRig` tiene una lógica de decay por `Tick`/`Update`, invocarla varias veces vía `SimulateSeconds` o el método que corresponda) hasta que `rig.ShakeOffset` esté claramente por debajo del tope, y recién ahí capturar `shakeBefore`, apagar FX, hacer el kick, capturar `shakeAfter`, y comprobar `shakeAfter == shakeBefore` (no se movió nada).
   - **Opción B (más simple si no hay decay expuesto para test):** en vez de re-usar el mismo `rig` ya saturado, crear un `CameraRig` fresco (o resetear su shake a `Vector3.zero` si existe un setter/método de reset) antes del segundo sub-bloque, para que la comparación antes/después sea sobre una base neutral.
3. Código resultante propuesto (Opción A, asumiendo que existe una forma de decaer el shake — revisar `CameraRig` para confirmar el método exacto, por ejemplo `rig.Tick(dt)` o similar):
   ```csharp
   SP.CameraSystem.CameraFxSettings.Enabled = false;
   // Dejar que decaiga el shake saturado del bloque anterior para que la
   // comparacion antes/despues no arranque ya en el techo (ver Bug 4 del
   // audit de HeadlessTestRunner).
   for (int i = 0; i < 60; i++) rig.Tick(0.05f); // o el metodo real de update del rig
   Vector3 shakeBeforeOff = rig.ShakeOffset;
   rig.KickDirectional(Vector3.forward, 1f);
   Vector3 shakeAfterOff = rig.ShakeOffset;
   Check("Con efectos de camara apagados, KickDirectional no acumula nada nuevo",
       shakeAfterOff == shakeBeforeOff);
   SP.CameraSystem.CameraFxSettings.Enabled = fxWasEnabled;
   ```
4. Antes de escribir el fix definitivo, revisar la clase `SP.CameraSystem.CameraRig` (buscar el método que hace decaer `ShakeOffset` por frame, y si expone algún `Reset`/setter para forzarlo a `Vector3.zero` en tests) para elegir entre Opción A y B con el método real disponible, en vez de inventar una firma que no exista.

**Verificación:** Correr `RunAll()` y revisar que el check "Con efectos de camara apagados, KickDirectional no acumula nada nuevo" siga en verde con el comportamiento actual (si `CameraFxSettings.Enabled=false` ya gatea correctamente `KickDirectional` en el código de gameplay, como sugiere el comentario del bloque). Para confirmar que el fix realmente detecta una regresión, comentar temporalmente el chequeo de `CameraFxSettings.Enabled` dentro de `CameraRig.KickDirectional` (forzar a que siempre aplique el kick) y re-correr: el check ahora DEBE fallar (`shakeAfterOff != shakeBeforeOff`), cosa que con el código viejo no pasaba nunca.

**Riesgo/efectos secundarios:** Es el punto más delicado de los 7 fixes porque depende de la API real de decaimiento de `CameraRig`, que no se confirmó línea por línea en esta pasada (solo se confirmó `ShakeOffset`, `MaxShakeMagnitude`, `KickDirectional` como miembros usados desde el test). Antes de implementar, LEER `Assets/_Project/Scripts/CameraSystem/CameraRig.cs` para encontrar el método de decaimiento real y su firma exacta. Si no decae solo por tiempo (por ejemplo si el decay ocurre en `Update()` de Unity y no hay forma de invocarlo manualmente en Edit Mode), usar la Opción B (instanciar un `CameraRig` fresco para el segundo sub-check) en vez de la A. Revisar también que el `fxWasEnabled` que se restaura al final (línea 1252 actual) siga guardando y restaurando el valor original de `CameraFxSettings.Enabled` para no filtrar estado a las fases siguientes.

---

### Bug 5: Nombres de GameObject duplicados en la leyenda del minimapa (`BuildMinimapLegend`)

**Línea(s):** 3784-3808 (bucle `for` dentro de `BuildMinimapLegend`)

**Causa raíz:** El bucle recorre las 3 entradas de `entries` (Aliado, Enemigo, Vehículo, línea 3777-3782) y en cada iteración crea un GameObject `"Swatch"` (línea 3786) y otro `"Label"` (línea 3795), ambos con el mismo nombre literal fijo en las 3 vueltas, todos como hijos directos de `legendGO`. El resultado son 3 hijos llamados `"Swatch"` y 3 llamados `"Label"` bajo el mismo padre. Cualquier `transform.Find("Swatch")` o `Find("Label")` futuro sobre `legendGO` solo puede resolver el primer hijo que coincide en orden de jerarquía (la entrada "Aliado", `i=0`), dejando inalcanzables por nombre las entradas de "Enemigo" y "Vehículo".

**Plan de implementación:**
1. En la línea 3786, cambiar el nombre fijo `"Swatch"` por uno que incluya la etiqueta de la entrada, usando el mismo `entries[i].label` que ya se usa más abajo en la línea 3799:
   ```csharp
   var swatchGO = new GameObject("Swatch_" + entries[i].label, typeof(Image));
   ```
2. En la línea 3795, mismo cambio para el label:
   ```csharp
   var labelGO = new GameObject("Label_" + entries[i].label, typeof(Text));
   ```
3. Como `entries[i].label` incluye "Vehículo" con tilde, y nombres de GameObject con tildes son válidos en Unity pero pueden ser incómodos para un `Find` futuro si alguien tipea sin tilde por error, una alternativa más robusta es usar el índice en vez del label (más corto y sin caracteres especiales):
   ```csharp
   var swatchGO = new GameObject($"Swatch_{i}", typeof(Image));
   ...
   var labelGO = new GameObject($"Label_{i}", typeof(Text));
   ```
   Elegir esta segunda variante si se prevé que algún código futuro necesite ubicar la entrada por posición (0=Aliado, 1=Enemigo, 2=Vehículo) en vez de por nombre semántico. Si se prevé buscarla por significado ("dame el swatch de Enemigo"), usar la primera variante con el label. Cualquiera de las dos resuelve el bug; se recomienda la variante con el label por legibilidad al inspeccionar la jerarquía a mano en el Editor.

**Verificación:** Correr `Strategic Point/Construir nivel y correr test` (o la demo), y en la ventana Hierarchy del Editor navegar a `Canvas > MinimapLegend` y confirmar que ahora hay 3 hijos con nombres distintos (por ejemplo `Swatch_Aliado`, `Swatch_Enemigo`, `Swatch_Vehículo` y sus labels correspondientes) en vez de 3 `Swatch`/3 `Label` repetidos. Como este método no tiene ningún `Check()` propio hoy (es solo construcción visual), no hay una aserción automática que valide esto — si se quiere blindar contra una futura regresión, se puede agregar un check nuevo en alguna fase que haga `legendGO.transform.Find("Swatch_Enemigo") != null` para confirmar que el nombre es alcanzable, aunque esto es opcional y no pedido explícitamente por el bug.

**Riesgo/efectos secundarios:** Ninguno funcional: el cambio es puramente cosmético/estructural (nombres de GameObject), no toca ninguna lógica de posicionamiento (`swRt.anchoredPosition`, `labelRt.anchoredPosition` siguen igual) ni ningún `Check()` existente depende de estos nombres hoy (se confirmó que ningún otro lugar del archivo hace `Find("Swatch")` o `Find("Label")`). Único cuidado: si en el futuro alguien ya escribió código externo a este archivo que dependa del nombre exacto `"Swatch"`/`"Label"` (por ejemplo algún otro Editor script fuera de este archivo), ese código se rompería con el rename — vale la pena un grep rápido de `"Swatch"` y `"Label"` en todo `Assets/_Project` antes de aplicar el cambio, no solo en este archivo.

---

### Bug 6: Llamadas de reflection sin verificar `null` — NullReferenceException opaca ante un rename en el código de gameplay

**Línea(s):** ~17 sitios a lo largo del archivo, entre ellos 526-527, 1319, 1351, 1379, 1500, 1528, 1566, 1577, 1584, 1593, 1621, 1625, 1649, 1657, 1662, 1675, 1677 (todos usan `GetField`/`GetMethod`/`GetProperty`, la mayoría con `BindingFlags.NonPublic`, y ninguno chequea el resultado antes de usarlo)

**Causa raíz:** Cada uno de estos sitios llama `typeof(X).GetField(...)`, `.GetMethod(...)` o `.GetProperty(...)` y guarda el resultado (`FieldInfo`/`MethodInfo`/`PropertyInfo`) en una variable que se usa inmediatamente después con `.SetValue(...)`, `.GetValue(...)` o `.Invoke(...)` — sin verificar que la búsqueda haya encontrado algo. Si el nombre del miembro privado cambia en el código de gameplay real (por ejemplo si `VehicleStatusView.speedLabel` se renombra a `speedText`), `GetField` devuelve `null` silenciosamente (no lanza excepción), y la siguiente línea revienta con un `NullReferenceException` genérico apuntando a la línea del `.GetValue`/`.Invoke`, sin decir NUNCA qué campo faltaba ni en qué tipo — hay que ir a adivinar cuál de los ~17 sitios fue.

**Plan de implementación:**
1. Agregar 3 helpers estáticos cerca de `Check` (por ejemplo justo antes o después de la definición de `Check`, líneas 1753-1759), que envuelvan `GetField`/`GetMethod`/`GetProperty` y lancen un mensaje claro si no encuentran el miembro:
   ```csharp
   // BUG REAL del audit del propio runner: GetField/GetMethod/GetProperty
   // devuelven null en silencio si el miembro no existe (por ejemplo tras un
   // rename en el codigo de gameplay), y el .SetValue/.GetValue/.Invoke de la
   // linea siguiente revienta con un NullReferenceException que no dice ni
   // el nombre del miembro ni el tipo. Estos wrappers fallan con un mensaje
   // que dice exactamente que se busco y donde.
   static System.Reflection.FieldInfo GetRequiredField(System.Type type, string name, System.Reflection.BindingFlags flags)
   {
       var fi = type.GetField(name, flags);
       if (fi == null)
           throw new InvalidOperationException($"[HeadlessTestRunner] No se encontro el campo '{name}' en {type.FullName} (flags={flags}). Se habra renombrado en el codigo de gameplay?");
       return fi;
   }

   static System.Reflection.MethodInfo GetRequiredMethod(System.Type type, string name, System.Reflection.BindingFlags flags)
   {
       var mi = type.GetMethod(name, flags);
       if (mi == null)
           throw new InvalidOperationException($"[HeadlessTestRunner] No se encontro el metodo '{name}' en {type.FullName} (flags={flags}). Se habra renombrado en el codigo de gameplay?");
       return mi;
   }

   static System.Reflection.MethodInfo GetRequiredMethod(System.Type type, string name)
       => GetRequiredMethod(type, name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

   static System.Reflection.PropertyInfo GetRequiredProperty(System.Type type, string name, System.Reflection.BindingFlags flags)
   {
       var pi = type.GetProperty(name, flags);
       if (pi == null)
           throw new InvalidOperationException($"[HeadlessTestRunner] No se encontro la propiedad '{name}' en {type.FullName} (flags={flags}). Se habra renombrado en el codigo de gameplay?");
       return pi;
   }
   ```
2. Convertir cada uno de los ~17 sitios para usar el wrapper correspondiente en vez de la llamada directa. Tres ejemplos concretos de antes/después:

   **Sitio A — línea 526-527 (`GetMethod`, dentro de `RunStressScenarios`):**
   ```csharp
   // Antes
   var onSelChanged = typeof(SP.Presentation.SelectionRingManager).GetMethod("OnSelectionChanged",
       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

   // Despues
   var onSelChanged = GetRequiredMethod(typeof(SP.Presentation.SelectionRingManager), "OnSelectionChanged",
       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
   ```

   **Sitio B — línea 1319 (`GetField`, dentro de `RunPhase5`):**
   ```csharp
   // Antes
   var speedFieldInfo = typeof(VehicleStatusView).GetField("speedLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

   // Despues
   var speedFieldInfo = GetRequiredField(typeof(VehicleStatusView), "speedLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
   ```

   **Sitio C — línea 1528 (`GetProperty`, dentro de `RunPhase6`):**
   ```csharp
   // Antes
   var instanceField = typeof(CameraRig).GetProperty("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

   // Despues
   var instanceField = GetRequiredProperty(typeof(CameraRig), "Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
   ```
3. Repetir el mismo patrón mecánico (reemplazar `typeof(X).GetField/GetMethod/GetProperty(...)` por `GetRequiredField/GetRequiredMethod/GetRequiredProperty(typeof(X), ...)`) en el resto de los sitios: 1351, 1379, 1500, 1566, 1577 (usa `GetRawConstantValue()` después, mismo wrapper de `GetRequiredField` sirve igual), 1584, 1593, 1621, 1625, 1649 (este usa la sobrecarga sin `BindingFlags` explícitos — usar el overload `GetRequiredMethod(type, name)` de 2 argumentos del paso 1), 1657, 1662, 1675, 1677.
4. No hace falta envolver también el `.SetValue`/`.GetValue`/`.Invoke` en sí — el objetivo es solo que la búsqueda del miembro falle con un mensaje útil; una vez que `GetRequiredX` devuelve no-null, el resto del código sigue exactamente igual.

**Verificación:** Correr `RunAll()` una vez para confirmar que con el código de gameplay actual (sin ningún rename) todos los `GetRequiredX` devuelven un valor válido y la suite completa sigue pasando igual que antes (0 `FailedCheckCount`, misma cantidad de checks en verde). Para confirmar que el mensaje de error mejora de verdad, renombrar temporalmente un campo privado real (por ejemplo `speedLabel` en `VehicleStatusView` a `speedLabelX`) y volver a correr: antes del fix, la excepción era un `NullReferenceException` genérico en la línea del `.GetValue`; después del fix, debe ser un `InvalidOperationException` con el mensaje `"No se encontro el campo 'speedLabel' en SP.Presentation.VehicleStatusView (...)"`, señalando exactamente qué buscar. Revertir el rename de prueba después de verificar.

**Riesgo/efectos secundarios:** Cambio mecánico y de bajo riesgo — no cambia ningún comportamiento cuando los miembros SÍ existen (que es el caso normal). El único cuidado es no equivocarse de `BindingFlags` al copiar cada sitio al wrapper (algunos usan combinaciones distintas: instancia vs. estático, con o sin público). Revisar cada sitio uno por uno al convertirlo, no hacer un buscar-reemplazar ciego, porque una `BindingFlags` incorrecta cambiaría silenciosamente qué miembro se resuelve (por ejemplo si hay overloads o miembros con el mismo nombre en clase base/derivada).

---

### Bug 7: Literales hardcodeados sin relación en `RunStressScenarios` — el tamaño del subconjunto puede reventar si cambia el conteo de soldados

**Línea(s):** 495 (conteo de soldados, dentro del `for` que llena `soldiers`/`allIds`) y 539 (`allIds.GetRange(0, 10)`)

**Causa raíz:** La línea 495 (`for (int i = 0; i < 50; i++)`) fija la cantidad de soldados de estrés en 50, literal e independiente. La línea 539 (`allIds.GetRange(0, 10)`) asume, sin ninguna relación explícita en el código, que siempre habrá al menos 10 elementos en `allIds` para poder tomar ese rango. Son dos números mágicos desconectados: si algún día se baja el 50 de la línea 495 (por ejemplo a 5, para un escenario de estrés más liviano), `List<T>.GetRange(0, 10)` en la línea 539 lanza `ArgumentException` ("count" fuera de rango) — un crash de la propia herramienta de test, no una falla de aserción legible sobre el sistema bajo prueba.

**Plan de implementación:**
1. Introducir una constante nombrada al principio de `RunStressScenarios` (antes de la línea 492, donde arranca `var rng = new System.Random(555);`), reemplazando el literal `50` tanto del `pool.Configure`/`RecommendedPrewarm` (línea 490) como del bucle (línea 495):
   ```csharp
   const int StressSoldierCount = 50;
   ```
2. Reemplazar la línea 490:
   ```csharp
   // Antes
   pool.Configure(projectilePrefab, SP.Combat.ProjectilePool.RecommendedPrewarm(50, 3f, 3f));
   // Despues
   pool.Configure(projectilePrefab, SP.Combat.ProjectilePool.RecommendedPrewarm(StressSoldierCount, 3f, 3f));
   ```
3. Reemplazar la línea 494-495:
   ```csharp
   // Antes
   var soldiers = new List<Soldier>(50);
   for (int i = 0; i < 50; i++)
   // Despues
   var soldiers = new List<Soldier>(StressSoldierCount);
   for (int i = 0; i < StressSoldierCount; i++)
   ```
4. Derivar el tamaño del subconjunto de la línea 539 a partir de `StressSoldierCount` en vez de un literal independiente, preservando el valor actual (10, para 50 soldados) pero garantizando que nunca exceda el total disponible:
   ```csharp
   // Cerca de donde arranca el bucle de 200 cambios (antes de la linea 534)
   int subsetSize = Mathf.Clamp(StressSoldierCount / 5, 1, StressSoldierCount); // 50/5=10, igual que antes

   // Linea 539, antes:
   var subset = i % 2 == 0 ? allIds : allIds.GetRange(0, 10);
   // Linea 539, despues:
   var subset = i % 2 == 0 ? allIds : allIds.GetRange(0, subsetSize);
   ```
5. El divisor `/5` es arbitrario pero preserva el comportamiento actual exacto (50/5=10) y escala razonablemente si `StressSoldierCount` cambia; si se prefiere mantener el subconjunto en un valor fijo pero clampeado sin importar el conteo total, usar en cambio `Mathf.Min(10, StressSoldierCount)` — cualquiera de las dos formas elimina el crash por `ArgumentException`, la diferencia es solo si el subconjunto debe escalar proporcionalmente o quedarse fijo en 10 mientras alcance. Se recomienda `Mathf.Clamp(StressSoldierCount / 5, 1, StressSoldierCount)` porque mantiene la proporción "20% de la escuadra completa" que threshold implícito del escenario original (50 y 10).

**Verificación:** Correr `Strategic Point/Estres con carga realista (50+)` (`RunStressScenarios()`) con el valor actual de `StressSoldierCount = 50` y confirmar en consola que `result.RingSpawnsAfter200Changes`/`RingSpawnsAfterFill` se comportan igual que antes del cambio (mismos números, porque `subsetSize` sigue dando 10). Después, como prueba de regresión, bajar temporalmente `StressSoldierCount` a un valor menor a 10 (por ejemplo 6) y volver a correr: antes del fix esto lanzaba `ArgumentException` sin llegar nunca a loggear nada útil sobre el escenario de estrés; después del fix, el método debe correr de punta a punta (con `subsetSize` clampeado a como mucho `StressSoldierCount`) y terminar con un mensaje `[Estres] ...` normal, sea que los checks pasen o fallen. Revertir el valor de prueba a 50 después de verificar.

**Riesgo/efectos secundarios:** Ninguno con el valor actual (50), porque el fix está diseñado para reproducir exactamente el comportamiento previo (`subsetSize == 10`) cuando `StressSoldierCount == 50`. El único cuidado es si en algún momento se sube `StressSoldierCount` muy por encima de 50 con el divisor `/5`: el subconjunto crecería proporcionalmente (por ejemplo 200 soldados -> subconjunto de 40), lo cual cambia el "patrón real de jugar" que describe el comentario de la línea 536-538 (alternar escuadra completa vs. subconjunto chico). Si se prefiere que el subconjunto SIEMPRE se quede en 10 (o el valor que sea) mientras la escuadra lo permita, usar la variante `Mathf.Min(10, StressSoldierCount)` del paso 5 en vez de la proporcional.


---

# Configuración / Persistencia — Planes de corrección (2 bugs)

### Bug 1: `PlayerPrefs.Save()` nunca se llama — toda la configuración depende del guardado implícito de Unity al cerrar limpio

**Archivos:**
- `Assets/_Project/Scripts/Presentation/PauseController.cs:115` (`PrefVolume`, slider de volumen)
- `Assets/_Project/Scripts/Presentation/PauseController.cs:131` (`PrefSensitivity`, slider de sensibilidad de mouse)
- `Assets/_Project/Scripts/Presentation/PauseController.cs:147` (`PrefTurretSensitivity`, slider de sensibilidad de torreta)
- `Assets/_Project/Scripts/Presentation/PauseController.cs:168` (`PrefHudScale`, slider de tamaño de HUD)
- `Assets/_Project/Scripts/Presentation/PauseController.cs:184` (`PrefCrosshairScale`, slider de tamaño de mirilla)
- `Assets/_Project/Scripts/Presentation/PauseController.cs:198` (`PrefInvertY`, toggle de invertir eje Y)
- `Assets/_Project/Scripts/Presentation/PauseController.cs:213` (toggle "EfectosDeCamara", delega en `CameraFxSettings.Enabled`)
- `Assets/_Project/Scripts/Player/KeyBindings.cs:97` (`Set`, remapeo de una tecla)
- `Assets/_Project/Scripts/Player/KeyBindings.cs:106` (`ResetToDefaults`, `PlayerPrefs.DeleteKey` por cada acción)
- `Assets/_Project/Scripts/Camera/CameraFxSettings.cs:36` (setter de `Enabled`)
- `Assets/_Project/Scripts/Presentation/AudioDirector.cs:94` (`SetGain`, volumen por canal SFX/UI/Ambiente)
- `Assets/_Project/Scripts/Presentation/GameplaySceneBootstrap.cs:49` (`sp_first_action_shown`, flag de tutorial)
- `Assets/_Project/Scripts/Player/PlayerInputDriver.cs:378` (`sp_used_tab`, flag de tutorial, dentro de `Update()`)
- Ningún archivo del proyecto define `OnApplicationQuit()` ni `OnApplicationPause(bool)` (grep sin resultados en todo `Assets/_Project/Scripts`).

**Causa raíz:** Todos los sistemas de configuración (volumen, sensibilidades, HUD, mirilla, invertir eje Y, efectos de cámara, volumen por canal, keybindings, flags de tutorial) escriben con `PlayerPrefs.SetInt/SetFloat/DeleteKey` pero ninguno de los 13 call sites llama a `PlayerPrefs.Save()`, y no existe ningún `MonoBehaviour` en el proyecto con `OnApplicationQuit`/`OnApplicationPause` que lo haga de forma centralizada. Unity solo persiste `PlayerPrefs` a disco automáticamente en un cierre limpio (`Application.Quit()` normal o salir del Editor); en un crash, un "matar proceso" (Alt+F4 duro, Task Manager, cierre de consola) o una suspensión de SO en móvil, los cambios quedan solo en la caché en memoria del subsistema nativo y se pierden.

**Plan de implementación:**
1. Elegir el hogar del hook de guardado: `Assets/_Project/Scripts/Player/PlayerInputDriver.cs`. Justificación: es el único `MonoBehaviour` del proyecto garantizado activo y habilitado durante toda la vida de la escena de gameplay (su `Update()` no tiene ningún early-return condicionado a estado de UI, solo a `kb == null`, ver comentario de clase línea 18-20: "Solo corre cuando el juego está en Play"), ya posee una de las 13 escrituras de `PlayerPrefs` (`sp_used_tab`, línea 378), y a diferencia de `PauseController` no depende de que su panel esté activo (los paneles hijos se activan/desactivan pero el propio componente driver permanece habilitado). Evitar crear un componente nuevo dedicado (p. ej. `SettingsPersistence.cs`) porque agregaría otro punto de wiring en `HeadlessTestRunner.cs` (que construye toda la escena por código) sin beneficio real, dado que ya existe un candidato natural.
2. En `PlayerInputDriver.cs`, agregar dos métodos de ciclo de vida junto a `Update()`:
   ```csharp
   void OnApplicationQuit() => PlayerPrefs.Save();
   void OnApplicationPause(bool pauseStatus)
   {
       if (pauseStatus) PlayerPrefs.Save();
   }
   ```
   Esto cubre, con un solo cambio, los 13 call sites existentes y cualquier escritura futura de `PlayerPrefs` en el proyecto, sin tener que tocar cada uno.
3. Adoptar un enfoque híbrido (belt-and-suspenders) para los call sites de baja frecuencia y alto valor, donde perder el dato es más grave y donde un `Save()` extra no cuesta rendimiento perceptible por no dispararse en cada frame/arrastre:
   - `KeyBindings.cs:97` (`Set`) y `KeyBindings.cs:106` (dentro del loop de `ResetToDefaults`, después de todos los `DeleteKey` — un solo `PlayerPrefs.Save()` al final del método, no uno por iteración): un remapeo de tecla es la pérdida más molesta para el jugador (tiene que rehacerlo a mano) y ocurre como mucho una vez por fila, no en un bucle de arrastre.
   - `CameraFxSettings.cs:36` (setter de `Enabled`): es un toggle de un solo click, no un slider continuo.
   - `GameplaySceneBootstrap.cs:49` y `PlayerInputDriver.cs:378` (flags de tutorial "ya visto"): se escriben una única vez en la vida del jugador; si se pierden, el tutorial reaparece, que es molesto pero menor — igual conviene el `Save()` inmediato porque es barato (ocurre una sola vez).
   NO agregar `Save()` inmediato en los sliders continuos (`PauseController.cs:115,131,147,168,184`, y `AudioDirector.cs:94` si en el futuro se cablea a un slider): `onValueChanged` de un `Slider` de Unity dispara en cada frame mientras se arrastra, y forzar un flush a disco (operación sincrónica y relativamente cara) en cada uno de esos frames introduciría hitches perceptibles durante el arrastre. Para esos casos alcanza con el `OnApplicationQuit`/`OnApplicationPause` del paso 2; el toggle `PrefInvertY` (línea 198) es un click discreto así que también puede sumarse al `Save()` inmediato si se prefiere uniformidad, pero no es crítico.
   - `PauseController.cs:198` (`PrefInvertY`, toggle) opcionalmente también puede recibir `Save()` inmediato por ser un click discreto, no obligatorio.
4. No agregar `Save()` inmediato en `PauseController.cs:213` (el toggle de efectos de cámara ya delega en el setter de `CameraFxSettings.Enabled`, que en el paso 3 ya guarda) para no duplicar el flush.
5. Verificar que ninguna otra escena del proyecto (`SC_MainMenu`) necesite el mismo hook: revisar `MainMenuController.cs` — no tiene escrituras de `PlayerPrefs`, así que no hace falta replicar el hook ahí; todas las escrituras ocurren dentro de la escena de gameplay, donde `PlayerInputDriver` siempre está presente.

**Verificación:**
1. En el Editor, entrar a Play en la escena de gameplay, abrir Pausa → Configuraciones, mover el slider de "Sensibilidad de mouse" a un valor distinto del default (p. ej. 0.35), y abrir Pausa → Controles → Remapear y cambiar "Ciclar posesion" de Q a M.
2. Sin salir de Play mode (para simular un "cierre no limpio" sin depender de que el proceso realmente muera), llamar a mano `SP.Player.KeyBindings.InvalidateCache()` y `SP.CameraSystem.CameraFxSettings.InvalidateCache()` para forzar una relectura de `PlayerPrefs` en la próxima consulta, y confirmar que el valor sigue en memoria (esto prueba que el valor en RAM es correcto, no que se escribió a disco).
3. Prueba real de disco: detener Play mode presionando el botón Stop del Editor (esto SÍ dispara `OnApplicationQuit` en Unity Editor) — si el hook del paso 2 del plan está bien puesto, esto ya garantiza el `Save()`. Para simular específicamente el caso "sin cierre limpio" que es el que falla hoy: hacer un build standalone (`File > Build Settings > Build`), correr el .exe, cambiar la sensibilidad y remapear una tecla, y matar el proceso desde el Administrador de tareas (no cerrar con la X ni Alt+F4 normal, que sí disparan quit limpio) inmediatamente después del cambio, SIN pasar por ningún otro trigger de guardado. Volver a abrir el build y confirmar en el slider/`KeyRebindView` que el valor persistió. Repetir la misma prueba en la versión SIN el fix (comentar temporalmente el hook) para confirmar que efectivamente reproduce la pérdida antes del fix.
4. Confirmar además que `HeadlessTestRunner.cs` (el runner de tests automatizado del proyecto) sigue pasando en modo batch, ya que ahí `Application.isBatchMode` puede afectar si `OnApplicationPause`/`Quit` se disparan igual; revisar que no rompa ningún `Check(...)` existente relacionado con `KeyBindings`/`PlayerPrefs` (línea ~1263).

**Riesgo/efectos secundarios:**
- `PlayerPrefs.Save()` es una operación de I/O sincrónica; llamarla en `OnApplicationPause` está bien (es un evento poco frecuente), pero si en el futuro alguien agrega un nuevo slider continuo y por costumbre le copia el patrón de `Save()` inmediato de los toggles/keybindings, podría introducir micro-freezes durante el arrastre — vale dejar el comentario explicativo en el código para prevenir esa regresión.
- En WebGL, `PlayerPrefs` usa `IndexedDB` de forma asíncrona por debajo; `Save()` sigue siendo la API correcta pero conviene confirmar que el proyecto no tiene un target WebGL activo con expectativas distintas de sincronía (no se detectó build target WebGL en los scripts revisados, pero no se confirmó en Player Settings).
- Si más adelante se agrega una escena adicional (p. ej. un lobby o loading screen) donde también se pueda tocar configuración fuera de `PlayerInputDriver`, ese nuevo flujo necesitará su propio hook o duplicar la llamada — documentar en el propio método que es "el guardado global de PlayerPrefs del proyecto" para que quien agregue una escena nueva lo recuerde.
- `OnApplicationPause(true)` en el Editor de Windows no se dispara nunca en la práctica (es principalmente un evento de móvil/consola); la cobertura real en PC depende de `OnApplicationQuit`, que sí cubre Alt+F4 y cerrar la ventana, pero NO cubre un `taskkill /F` o un crash — eso sigue siendo una pérdida posible incluso después del fix, y es inherente a cualquier esquema de guardado que no persista en cada escritura (por eso el paso 3 del plan agrega el `Save()` inmediato en los puntos de mayor valor).

---

### Bug 2: el panel "CONTROLES" no se actualiza tras remapear una tecla

**Archivos:**
- `Assets/_Project/Scripts/UI/ControlsTable.cs` (clase estática, `Entries[]` líneas 103-163 — todas las teclas son literales hardcodeados como `"R"`, `"Q"`, `"TAB"`, `"H"`, etc., sin ninguna referencia a `SP.Player.KeyBindings`; `FullText()` líneas 196-208)
- `Assets/_Project/Scripts/Editor/HeadlessTestRunner.cs:3534` (`controlsListTxt.text = SP.UI.ControlsTable.FullText();` — única vez que se renderiza el texto, al construir la escena)
- `Assets/_Project/Scripts/Presentation/PauseController.cs:296-302` (`ToggleControlsOverlay`, atajo [H])
- `Assets/_Project/Scripts/Presentation/PauseController.cs:304-309` (`OnControlsClicked`, botón desde el menú de pausa)
- `Assets/_Project/Scripts/Presentation/PauseController.cs:311-316` (`OnControlsBackClicked`)
- `Assets/_Project/Scripts/Presentation/PauseController.cs:320-327` (`OnRebindClicked`, sí llama `view.RefreshAll()` — el patrón correcto que falta replicar)
- `Assets/_Project/Scripts/Presentation/PauseController.cs:329-334` (`OnRebindBackClicked`, vuelve al panel de Controles ya abierto detrás)
- `Assets/_Project/Scripts/Presentation/PauseController.cs:336-342` (`OnRebindResetClicked`, sí llama `view.RefreshAll()` para el propio panel de remapeo)
- `Assets/_Project/Scripts/UI/KeyRebindView.cs:95-111` (`AssignKey`, confirma el remapeo y llama a `RefreshAll()` — pero `RefreshAll()` solo repinta las filas de `Labels[]` del propio `KeyRebindView`, no el `Text` del panel de Controles)
- `Assets/_Project/Scripts/UI/KeyRebindView.cs:113-121` (`RefreshAll`)
- Nota relacionada (no forma parte de este bug, pero es relevante para el riesgo): `Assets/_Project/Scripts/Player/PlayerInputDriver.cs:336` lee `kb.hKey` directamente en vez de `KeyBindings.WasPressed(KeyBindings.Controles)`, así que hoy remapear la acción "Controles" (H) no cambiaría la tecla que realmente abre el panel — ver Riesgos.

**Causa raíz:** `ControlsTable.Entries[]` es un array estático de structs inmutables con la tecla como **string literal** (p. ej. `new ControlEntry("Q", "ciclar la posesión...", ...)`), construido una sola vez en el inicializador estático y sin ninguna referencia a `SP.Player.KeyBindings`. `FullText()` se llama una única vez, al armar la escena en `HeadlessTestRunner.cs:3534`, y ese resultado queda fijo para siempre en `controlsListTxt.text`. Ni `OnControlsClicked`/`ToggleControlsOverlay` (que abren el panel) ni `KeyRebindView.AssignKey`/`RefreshAll` (que confirman un remapeo) vuelven a tocar ese `Text`. Aun si se llamara a `ControlsTable.FullText()` de nuevo, devolvería el mismo texto porque `Entries[]` nunca consulta `KeyBindings.Get`/`DisplayName` — el bug es doble: falta tanto el re-render como la fuente de datos dinámica.

**Plan de implementación:**
1. Extender `ControlEntry` en `ControlsTable.cs` para poder asociar (opcionalmente) una fila con un `actionId` real de `KeyBindings`: agregar un campo `public readonly string ActionId;` y un constructor que lo reciba (o sobrecarga con default `null` para no tener que tocar las ~40 filas que no corresponden a una acción remapeable, como `"Clic"`, `"Mouse"`, `"Rueda"`, `"Arrastrar"`, `"Shift+Clic"`, `"Ctrl+A"`, `"Ctrl+1..9"`, `"F1/F2/F3"`, `"1/2/3"`, que son gestos de mouse o combinaciones fijas sin entrada en `KeyBindings.AllActions`).
2. Auditar `Entries[]` fila por fila contra `KeyBindings.defaults` (`KeyBindings.cs:50-70`) y marcar `ActionId` en las filas cuya tecla mostrada corresponde 1:1 a una acción remapeable, por ejemplo: `"R"`→`KeyBindings.Recargar`, `"TAB"`→`KeyBindings.AlternarVista`, `"Q"`→`KeyBindings.CiclarPosesion`, `"C"`→`KeyBindings.PoseerMasCercano`, `"F"`→`KeyBindings.Poseer`, `"V"`→`KeyBindings.CamaraVehiculo`, `"Espacio"`→`KeyBindings.Recentrar`, `"H"`→`KeyBindings.Controles`, `"X"`→revisar con cuidado porque hoy `CancelarOrden` y `SubirBajarVehiculo` comparten default `Key.X` en `KeyBindings.cs:54,64` — documentar esa ambigüedad preexistente en un comentario y, si al remapear una de las dos el texto queda inconsistente con la otra, dejarlo fuera de este bugfix (es un bug de diseño de bindings distinto, no de refresco). Dejar sin `ActionId` (es decir, `null`, tecla literal fija) las filas de mouse/gestos y las que no tienen contraparte en `KeyBindings` (p. ej. `F1/F2/F3`, `1/2/3`, `Ctrl+1..9`, que seleccionan directo por número y no pasan por `KeyBindings`).
3. En `FormatKey`/el punto donde `LineFor` y `FullText` arman cada línea (líneas 188 y 205), cambiar `FormatKey(e.Key)` por una nueva función `DisplayKeyFor(ControlEntry e)` que devuelva `SP.Player.KeyBindings.DisplayName(e.ActionId)` (formateado con el mismo `FormatKey`) cuando `e.ActionId != null`, y si no, caiga al literal `e.Key` de siempre. Esto hace que `ControlsTable.FullText()` deje de ser 100% estático y refleje el estado actual de `KeyBindings` en cada llamada.
4. En `PauseController.cs`, cachear la referencia al `Text` de la lista de controles en `OnEnable()`, siguiendo el mismo patrón que ya usa para `pausePanel`/`settingsPanel`/`controlsPanel`/`confirmExitPanel`/`rebindPanel` (líneas 50-74): agregar un campo `Text controlsListTxt;` y, dentro del bloque de `OnEnable()`, `if (controlsListTxt == null && controlsPanel != null) controlsListTxt = controlsPanel.transform.Find("List")?.GetComponent<Text>();` (el nombre `"List"` es el que usa `HeadlessTestRunner.cs:3521` para el `GameObject` hijo de `ControlsPanel`).
5. Agregar un método privado `void RefreshControlsList() { if (controlsListTxt != null) controlsListTxt.text = SP.UI.ControlsTable.FullText(); }` en `PauseController.cs`.
6. Llamar `RefreshControlsList()` en los cuatro puntos donde el panel de Controles pasa a estar visible o puede haber cambiado por debajo suyo:
   - `OnControlsClicked()` (línea ~307, justo después de `controlsPanel.SetActive(true)`).
   - `ToggleControlsOverlay()` (línea ~301): como este método alterna con `SetActive(!controlsPanel.activeSelf)`, hay que capturar el nuevo estado y refrescar solo cuando pasa a `true` (evitar refrescar al cerrarlo, es trabajo de más pero inofensivo si se prefiere simplicidad y se llama siempre).
   - `OnRebindBackClicked()` (línea ~333, después de `rebindPanel.SetActive(false)`): al volver del sub-panel de remapeo, el panel de Controles que queda detrás puede tener texto desactualizado si se remapeó algo mientras tanto.
   - `OnRebindResetClicked()` (línea ~341, junto al `view.RefreshAll()` ya existente): un "restaurar de fábrica" también debe reflejarse en el panel de Controles, no solo en las filas del propio `KeyRebindView`.
7. Alternativa más simple si se prefiere no tocar 4 call sites: en vez de refrescar en cada transición, refrescar siempre que `controlsPanel` se vuelve activo, moviendo la lógica a un `OnEnable`/hook de activación del propio `controlsPanel` (p. ej. agregando un pequeño componente `ControlsPanelView : MonoBehaviour` con `void OnEnable() => text.text = ControlsTable.FullText();` colgado del `GameObject` "ControlsPanel"). Esto cubre automáticamente los 4 casos del paso 6 sin duplicar la llamada en `PauseController`, a costa de un componente nuevo y de tener que cablearlo en `HeadlessTestRunner.cs` donde se construye `controlsPanelGO`. Se prefiere la opción del paso 4-6 (reutilizar `PauseController`) por consistencia con el patrón ya usado para `OnRebindClicked`/`view.RefreshAll()` en el mismo archivo, pero se deja documentada esta alternativa por si el auditor prefiere evitar tocar 4 métodos.

**Verificación:**
1. En Play mode, abrir Pausa → Controles y confirmar que la fila `[Q] ciclar la posesión al siguiente aliado vivo` se ve tal cual.
2. Volver a Pausa → Controles → Remapear, hacer click en la fila "Ciclar posesion", presionar `M`, confirmar que `KeyRebindView` muestra `Ciclar posesion:  M`.
3. Volver atrás con el botón "VOLVER" del panel de remapeo (dispara `OnRebindBackClicked`) y verificar en el panel de Controles, que queda detrás, que la fila ahora dice `[M] ciclar la posesión al siguiente aliado vivo` (ya no `[Q]`).
4. Cerrar todo con ESC hasta volver al juego, presionar `H` para abrir el overlay de Controles sin pasar por pausa (`ToggleControlsOverlay`), y confirmar que también ahí aparece `[M]` y no `[Q]` (cubre el segundo punto de entrada al mismo `Text`).
5. Repetir el flujo apretando "REMAPEAR TODO A VALORES DE FABRICA" (`OnRebindResetClicked`) y confirmar que el panel de Controles vuelve a mostrar `[Q]`.
6. Si se implementa el paso 2 del plan (mapeo `ActionId` fila por fila), agregar/objetivo de test en `HeadlessTestRunner.cs` cerca de la línea 1263-1268 (`ControlsTable.Validate`) que, tras `KeyBindings.Set(KeyBindings.CiclarPosesion, Key.M)`, llame `ControlsTable.FullText()` y compruebe con `Contains("[M]")`/`!Contains("[Q]")` para la línea de "ciclar la posesión", dejando una verificación automatizada y no solo manual en el runner headless del proyecto (no lo agrega el propio plan, pero es el lugar natural para hacerlo dado que el proyecto no usa un framework de test aparte).

**Riesgo/efectos secundarios:**
- La auditoría del paso 2 (mapear cada fila de `Entries[]` a un `ActionId`) es el paso más delicado: varias filas comparten la misma tecla mostrada para distintos contextos (p. ej. `"E"` aparece tanto para "subir al vehículo... o equipar arma" como para "bajarse del vehículo", y ninguna de las dos coincide textualmente con la acción `Interactuar` de `KeyBindings` de forma obvia). Mapear mal una fila haría que el panel muestre una tecla incorrecta para esa fila específica — más peligroso que dejarla sin `ActionId` (que como mucho la deja desactualizada, el bug actual, no incorrecta con una tecla ajena). Ante la duda, dejar la fila con tecla literal fija en vez de forzar un mapeo dudoso.
- Ya existe una colisión de binding por defecto entre `CancelarOrden` y `SubirBajarVehiculo` (ambas `Key.X` en `KeyBindings.cs:54,64`) y varias acciones remapeables (`CiclarPosesionAtras`/Z, `Reagrupar`/Y, `Retirada`/B, `CiclarFormacion`/K, `SeleccionarHeridos`/J, `SeleccionarMismoTipo`/N) no tienen ninguna fila en `ControlsTable.Entries[]` en absoluto — son gaps preexistentes de contenido, no de refresco, y quedan fuera del alcance de este bugfix; no counter-indican el fix pero conviene señalarlo para no prometer que "todas las acciones remapeables se ven reflejadas en el panel" tras este cambio.
- `PlayerInputDriver.cs:336` sigue leyendo `kb.hKey` en vez de `KeyBindings.WasPressed(KeyBindings.Controles)`: si un jugador remapea la acción "Ver controles" (H) desde `KeyRebindView`, el panel ahora mostraría la tecla nueva correctamente (gracias a este fix) pero la tecla que REALMENTE abre el overlay seguiría siendo H — una inconsistencia preexistente y separada que este bugfix no corrige (no estaba en el alcance de los 2 bugs pedidos) pero que vale la pena marcar como hallazgo aparte.
- Cachear `controlsListTxt` en `OnEnable()` de `PauseController` sigue el mismo patrón ya usado para los otros paneles, así que el riesgo de romper algo por ese lado es bajo; verificar igual que `HeadlessTestRunner.cs` no renombre el `GameObject` "List" en otro lugar del archivo (una sola definición encontrada, línea 3521).

