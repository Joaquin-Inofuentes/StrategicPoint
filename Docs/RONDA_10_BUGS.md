# Ronda 10 — Auditoría: 100 bugs nuevos

Auditoría de código leyendo los 221 scripts de `Assets/_Project/Scripts` y `Assets/Editor`.
**Ninguno de estos 100 repite los 97 del reporte anterior** (`plan_correccion_bugs_strategic_point.md`):
aquellos ya están arreglados y sus comentarios "BUG REAL" siguen en el código; estos son los que
quedaron, o los que las propias correcciones anteriores introdujeron.

El plan de corrección paso a paso de cada uno está en
[`RONDA_10_PLAN_CORRECCION.md`](RONDA_10_PLAN_CORRECCION.md), con el mismo número de bug.

## Cómo leer la tabla de severidad

| Nivel | Qué significa |
|---|---|
| **P0** | Rompe una regla de juego o corrompe estado. Se nota jugando. |
| **P1** | Comportamiento incorrecto en un caso frecuente, o agujero de cobertura que deja un subsistema sin probar. |
| **P2** | Rendimiento, basura de GC, fugas de memoria, inconsistencias de convención. |
| **P3** | Caso de borde, código muerto, riesgo latente. |

## Resumen por subsistema

| Bloque | Bugs | P0 | P1 | P2 | P3 |
|---|---|---|---|---|---|
| A. Núcleo de simulación y estáticos | 1–12 | 2 | 6 | 2 | 2 |
| B. Combate: armas y proyectiles | 13–28 | 3 | 3 | 6 | 4 |
| C. Vida, daño y dificultad | 29–34 | 3 | 2 | 0 | 1 |
| D. Inteligencia artificial | 35–50 | 3 | 8 | 4 | 1 |
| E. Motor del soldado y física | 51–56 | 0 | 2 | 3 | 1 |
| F. Navegación | 57–60 | 0 | 1 | 2 | 1 |
| G. Servicio de órdenes | 61–70 | 1 | 4 | 4 | 1 |
| H. Entrada del jugador | 71–84 | 1 | 4 | 7 | 2 |
| I. Vehículos y torretas | 85–93 | 1 | 2 | 3 | 3 |
| J. Selección, UI y presentación | 94–100 | 0 | 4 | 3 | 0 |
| **Total** | **100** | **14** | **36** | **34** | **16** |

### Los 14 P0, de un vistazo

| Bug | Qué rompe |
|---|---|
| **1** | Estáticos sin restablecer: la segunda partida de una sesión arranca con basura de la primera |
| **4** | Un soldado montado no regenera vida ni termina de recargar |
| **13** / **37** | El arma se tickea dos veces por frame en combate: cadencia y recarga al doble |
| **14** | Cambiar de arma rellena el cargador gratis |
| **22** | Las balas atraviesan paredes para pegarle a quien está hasta 4,3 m detrás |
| **29** | Revivir no publica ningún evento: el HUD, el roster y los contadores nunca se enteran |
| **31** | Un tanque destruido publica `EntityDiedEvent(-1)`, un evento de muerte de soldado |
| **34** | El jugador cobra dos veces su ventaja de dificultad (143 de vida efectiva donde el perfil dice 125) |
| **35** | La detección de atasco no puede dispararse en Patrulla, Seguir, Rodeo ni Defensiva |
| **49** | Revivir a un soldado agachado deja su cámara hundida para siempre |
| **69** | El rescate automático (A5) nunca funcionó: manda una orden de seguir a un muerto |
| **71** | El HUD del arma desaparece para siempre después de la primera muerte |
| **86** | Cambiar de asiento puede dejar a un soldado desactivado y sin asiento: se pierde |

---

# A. Núcleo de simulación y estáticos (1–12)

### Bug 1 — `ReinicioDeEstaticos` sólo restablece 4 de los ~15 estáticos que sobreviven al Play mode · **P0**
**Archivo:** `Core/ReinicioDeEstaticos.cs:13-19`
**Síntoma:** con "Enter Play Mode sin recarga de dominio" (el modo que usa este proyecto), la segunda
partida de una sesión de Editor arranca con basura de la primera.
**Causa raíz:** `Restablecer()` toca `ModoDios`, `Health.RegeneracionPermitida`, `Demolicion.Segundos` y
`WeaponHolder.ReservasActivas`. Quedan afuera, entre otros: `SpatialGrid.cells`/`built`,
`ActorRegistry` (lista + set + `proximoBarrido`), `WorldSystemsRegistry` (5 listas + `populated`),
`EventBus.Instance.handlers`, `Soldier.nextId`, `Projectile.nextInstanceId`,
`WorldSimulationDriver.tickNumero`, `OrderService.ManejadoAMano`, `AjustesDeEscuadra.Lider`,
`RescateAutomatico.Caido/Rescatista`, `AiBrain.proximoAvisoRadio`, `AiBrain.ultimoAvisoDeSeguir`,
`AiBrain.ultimoAvisoDeteccion`. Es la clase que existe exactamente para esto y está a un tercio.
**Medido** con `Tools/Indice/indexar_ia.py` (ver `Docs/INDICE_IA/08_estaticos.md`): hay **161 estáticos
mutables fuera de `Editor/`**; 6 los cubre `ReinicioDeEstaticos`, 38 están en archivos que tienen su
propio `[RuntimeInitializeOnLoadMethod]` (hay que mirar caso por caso si el hook cubre ese campo) y
**117 no tienen absolutamente nada**.

### Bug 2 — Los avisos de radio de los aliados quedan mudos toda la partida siguiente · **P1**
**Archivo:** `Ai/AiBrain.Granadas.cs:25` (`static float proximoAvisoRadio`), `:167`
**Síntoma:** en la segunda partida de una sesión de Editor, "GRANADA! ALEJENSE!", "NOS DISPARAN!" y el
resto de las voces de radio no suenan nunca.
**Causa raíz:** `proximoAvisoRadio` es estático y se compara contra `Time.time`, que vuelve a ~0 al
entrar a Play. Si la partida anterior duró 300 s, el estático quedó en ~303 y hay que jugar 303
segundos para que vuelva a haber una voz. Mismo patrón en `ultimoAvisoDeSeguir` (`Tactica.cs:218`),
que usa `Time.unscaledTime` y por eso se cura solo — la inconsistencia entre los dos relojes es la
que hace que el bug sea difícil de ver.

### Bug 3 — El LOD por distancia tira un frame de simulación al acercarse un soldado · **P1**
**Archivo:** `Ai/WorldSimulationDriver.cs:110-120`
**Síntoma:** un soldado que cruza el umbral de 120 m hacia la cámara pierde hasta un frame de IA,
movimiento y regeneración.
**Causa raíz:** cuando está lejos acumula `s.DtLodPendiente += dt` y saltea el tick impar. Si en el
tick siguiente ya está cerca, entra por la rama `dtSoldado = dt` y acto seguido
`s.DtLodPendiente = 0f` descarta el dt acumulado sin haberlo simulado nunca.

### Bug 4 — Un soldado montado en un vehículo no regenera vida ni termina de recargar · **P0**
**Archivo:** `Ai/WorldSimulationDriver.cs:109`
**Síntoma:** subirse al tanque congela la vida y la recarga. Bajarse tres minutos después te devuelve
el mismo cargador a medias y la misma vida que tenías al subir.
**Causa raíz:** `if (s == null || !s.gameObject.activeInHierarchy) continue;` saltea el tick completo,
que es el único sitio desde donde se llaman `s.Health.Tick(dt)` y `s.Weapon.Tick(dt)`. `Vehicle.Mount`
desactiva el GameObject, así que el pasajero queda congelado. Es incoherente con el trabajo hecho para
que el montado siga contando vivo en `ActorRegistry.CountAlive` y en el roster.

### Bug 5 — `WorldSimulationDriver.Step` no llama a `WorldSystemsRegistry.EnsurePopulated()` · **P1**
**Archivo:** `Ai/WorldSimulationDriver.cs:63-160` vs. `Core/WorldSystemsRegistry.cs:56`
**Síntoma:** un vehículo, torreta o `TurretAI` que arranca la escena desactivado nunca se tickea en la
suite headless ni en Play si nadie más llamó `EnsurePopulated`.
**Causa raíz:** asimetría. `Step` sí llama a `SpatialGrid.Rebuild()`, que a su vez llama a
`ActorRegistry.EnsureAllRegistered()` (soldados). El registro equivalente de vehículos no tiene ese
enganche y depende de que alguien lo despierte antes.

### Bug 6 — `ActorRegistry.EnsureAllRegistered` mide el tiempo con un reloj que la suite no avanza · **P1**
**Archivo:** `Core/ActorRegistry.cs:80-84`
**Síntoma:** en la suite headless (Edit mode, reloj simulado con `SimulateSeconds`), el barrido
periódico corre muchísimas menos veces de las que debería; un soldado creado desactivado a mitad de un
escenario puede no aparecer nunca.
**Causa raíz:** usa `Time.realtimeSinceStartup` (reloj de pared) con `IntervaloDeBarrido = 0.5f`. Una
fase que simula 60 s de juego en 0,2 s de pared hace **un** barrido en vez de 120. El resto del
proyecto usa acumuladores de `dt` justamente por esto (ver el comentario de `Health.Tick`).

### Bug 7 — `FindById` y `FindNearest` no dan de alta a los soldados desactivados · **P1**
**Archivo:** `Core/ActorRegistry.cs:121-143`
**Síntoma:** `Dificultad.MultiplicadorDeDano` (que busca al atacante por Id), la cámara de muerte
("te mató X") y `RescateAutomatico` devuelven null para cualquier soldado que haya arrancado la escena
desactivado y todavía no lo haya levantado el barrido periódico.
**Causa raíz:** `CountAlive` y `CountDead` llaman a `EnsureAllRegistered()` primero; `FindById`,
`FindNearest` y `FindNearestEnemyInRange` no. Tres funciones del mismo archivo con dos contratos.

### Bug 8 — `SpatialGrid.FindNearestInRange` no es determinista al empatar · **P1**
**Archivo:** `Core/SpatialGrid.cs:173` (`if (sqr <= bestSqr)`)
**Síntoma:** dos corridas idénticas de la suite pueden elegir blancos distintos y divergir.
**Causa raíz:** con `<=`, en un empate exacto gana el **último** candidato recorrido, y ese orden lo
fija el orden de iteración del `Dictionary<long, List<Soldier>>`, que depende de hashes y de inserción.
`ActorRegistry.FindNearest` (la versión que esta reemplaza) usa `<` — gana el primero. Los comentarios
del archivo prometen repetibilidad ("determinista, así dos corridas idénticas dan el mismo resultado").

### Bug 9 — La grilla espacial no se invalida al cambiar de escena · **P2**
**Archivo:** `Core/SpatialGrid.cs:45` (`static bool built`), `:117-120`
**Síntoma:** en la escena nueva, la primera consulta que llegue antes del primer `Rebuild()` responde
con las celdas de la escena anterior (referencias fake-null de soldados destruidos).
**Causa raíz:** `built` y `cells` son estáticos sin `RuntimeInitializeOnLoadMethod` ni enganche a
`sceneLoaded`. `NavService.Reset` (`Core/NavService.cs:115`) resolvió exactamente este problema para su
propia grilla y dejó a esta afuera.

### Bug 10 — Los predicados de la grilla derreferencian `Health` sin comprobarlo · **P3**
**Archivo:** `Core/ActorRegistry.cs:151-153`, `Combat/Projectile.cs:749-752`, `Ai/AiBrain.Granadas.cs:135`
**Síntoma:** `NullReferenceException` dentro de `QueryInRange`/`FindNearestInRange` si un soldado a
medio construir entra en la grilla; aborta el tick de **todos** los soldados de ese frame porque
`WorldSimulationDriver.Step` recorre sin `try/catch`.
**Causa raíz:** `SpatialGrid.Rebuild` filtra `s.Health == null`, pero un soldado puede perder el
componente (o entrar por `EnsureAllRegistered` entre dos `Rebuild`) y los predicados escriben
`s.Health.IsAlive` directo. `AiBrain.MejorObjetivoVisible` (`Tactica.cs:337`) sí lo comprueba: tres
predicados, dos contratos.

### Bug 11 — `ObjectPool.Clear()` termina con una asignación muerta · **P3**
**Archivo:** `Core/ObjectPool.cs:82` y `:107`
**Síntoma:** ninguno hoy, pero el comentario de 14 líneas describe un arreglo que el código no hace.
**Causa raíz:** `int prestadas = prestadasCount;` se captura al entrar, el bucle no toca
`prestadasCount`, y al final `prestadasCount = prestadas;` le asigna su propio valor. Es un no-op que
parece la corrección de un bug.

### Bug 12 — `EventBus` no soporta que un suscriptor se dé de baja durante el `Publish` · **P2**
**Archivo:** `Core/EventBus.cs:46-67`
**Síntoma:** un suscriptor que llama `Dispose()` dentro de su propio handler (patrón normal: "reacciono
una vez y me bajo") igual recibe el evento en los handlers que ya estaban en la lista de invocación, y
`ClearAll()` deja vivos `IDisposable` que ya no representan nada.
**Causa raíz:** `GetInvocationList()` toma una foto antes de invocar, y `ClearAll()` vacía el
diccionario sin avisarle a los `ActionDisposable` entregados.

---

# B. Combate: armas y proyectiles (13–28)

### Bug 13 — El arma se tickea DOS veces por frame mientras el soldado ataca · **P0**
**Archivo:** `Ai/AiBrain.cs:1065` (`self.Weapon.Tick(dt)`) + `Ai/WorldSimulationDriver.cs:122`
**Síntoma:** la cadencia real de un soldado en `AiState.Attack` es el **doble** de la que dice el
catálogo, la recarga tarda la mitad y la dispersión (`spreadDeg`) decae al doble de rápido. Sólo pasa
en combate, que es cuando importa: el arma miente sobre todas sus estadísticas justo ahí.
**Causa raíz:** `WorldSimulationDriver.Step` ya llama a `s.Weapon.Tick(dtSoldado)` para cada soldado
activo; el `case AiState.Attack` de `AiBrain.Tick` lo vuelve a llamar con el mismo `dt`.

### Bug 14 — Cambiar de arma recarga gratis el cargador · **P0**
**Archivo:** `Combat/WeaponHolder.cs:261` (`CurrentAmmo = magazineSize;`)
**Síntoma:** munición efectivamente infinita sin gastar reservas: apretar `1`, `2`, `1` deja el rifle
lleno. Y con las reservas activas, re-equipar la **misma** arma (`cambioDeArma == false`) también la
rellena, porque la restauración del cargador guardado sólo corre `if (UsaReservas && cambioDeArma)`.
**Causa raíz:** la asignación incondicional de la línea 261 corre antes de la restauración condicional
de 262-267.

### Bug 15 — Equipar un arma tiñe el arma de todos los soldados que comparten material · **P1**
**Archivo:** `Combat/WeaponHolder.cs:555-561` (`WeaponVisualRenderer.sharedMaterial.color = color`)
**Síntoma:** al cambiar de arma, el cubo-pivote del arma de media escuadra cambia de color.
**Causa raíz:** escribe sobre `sharedMaterial`, que desde el item 230 es compartido por equipo. El
propio proyecto documenta esta trampa en `PlayerInputDriver.cs:2029` y la resuelve con
`MaterialPropertyBlock` (`CubeFxReactor.WriteTint`), pero este camino quedó con la versión vieja.
*(El síntoma visible está atenuado porque `ApplyWeaponVisualModel` apaga el Renderer del cubo — ver
bug 83 — así que hoy se tiñe algo invisible; el material compartido sí se ensucia igual.)*

### Bug 16 — Los arrays de reserva de munición son de 16 y dos métodos no comprueban la cota · **P3**
**Archivo:** `Combat/WeaponHolder.cs:153-155`, `:170-180` (`ReponerMunicion`), `:186-191` (`AgregarMunicion`)
**Síntoma:** `IndexOutOfRangeException` el día que `WeaponKind` pase de 16 valores.
**Causa raíz:** `InicializarReserva` comprueba `i < reservaPorArma.Length`; `ReponerMunicion` escribe
`reservaInicializada[i]` y `reservaPorArma[i]` sin comprobar nada.

### Bug 17 — `ReadinessFraction01` puede devolver NaN · **P3**
**Archivo:** `Combat/WeaponHolder.cs:138-140`, habilitado por `:436-443` (`ConfigurarCargador`)
**Síntoma:** la barra de recarga del HUD se vuelve NaN y desaparece.
**Causa raíz:** la rama de enfriamiento se protege con `fireCooldown > 0f`; la rama de recarga divide
por `reloadDuration` sin protección, y `ConfigurarCargador(tamano, 0f)` es una llamada legal.

### Bug 18 — El arsenal elegido en la caja de suministros se pierde al entrar a Play · **P2**
**Archivo:** `Combat/WeaponHolder.cs:133` (`public readonly List<WeaponKind> Loadout = ...`)
**Síntoma:** cambiar el arma principal en una caja de suministros (`CambiarArmaPrincipal`) no sobrevive
un domain reload ni una recarga de escena.
**Causa raíz:** es un campo `readonly` inicializado en línea, sin `[SerializeField]`. El archivo
documenta la regla contraria para `patrolRoute` en `AiBrain.cs:154`.

### Bug 19 — Cambiar el arma principal hereda el cargador a medias de la anterior · **P3**
**Archivo:** `Combat/WeaponHolder.cs:449-466`
**Síntoma:** cambiar a un arma nueva en la caja te la da con el cargador que tenía el arma que ocupaba
esa ranura.
**Causa raíz:** `CambiarArmaPrincipal` resetea `reservaInicializada[(int)candidata] = false` pero no
`cargadorPorArma[(int)candidata]`, y `EquipWeapon` lee ese valor guardado.

### Bug 20 — La velocidad del proyectil del catálogo se ignora salvo en el lanzacohetes · **P2**
**Archivo:** `Combat/WeaponHolder.cs:620-632`
**Síntoma:** un arma a la que se le configure `ProjectileSpeed` distinta de 1 en `WeaponCatalog`
dispara a la velocidad base igual.
**Causa raíz:** sólo la rama `espec.ExplosionRadius > 0f` pasa `espec.ProjectileSpeed` a `pool.Spawn`.
La rama de perdigones y la normal usan el default `speedMultiplier = 1f`.

### Bug 21 — `ApplyWeaponVisualModel` instancia objetos en Edit mode · **P2**
**Archivo:** `Combat/WeaponHolder.cs:359` (`Instantiate(prefab, ...)`)
**Síntoma:** cada corrida de la suite headless deja instancias `RealModel` colgadas de los prefabs y de
la escena, que se serializan al guardar.
**Causa raíz:** el método ya tuvo el arreglo simétrico (`DestroyImmediate` en Edit mode, líneas
353-357) pero el `Instantiate` no está condicionado ni marcado con `HideFlags.DontSave`.

### Bug 22 — Las balas atraviesan paredes para pegarle a quien está detrás · **P0**
**Archivo:** `Combat/Projectile.cs:246-266` (soldados) antes de `:283` (`ChocoContraElMundo`)
**Síntoma:** a 260 m/s y 60 fps la bala avanza **4,3 m por frame**. Cualquier soldado a menos de 4,3 m
detrás de una pared recibe el tiro a través de ella. El jugador se cubre y le pegan igual.
**Causa raíz:** el barrido de soldados del tramo corre **antes** que el barrido del mundo sólido, y
cuando encuentra a alguien hace `Expire(); return;` sin haber comprobado nunca si había una pared en el
medio. El comentario de la línea 280 justifica el orden ("un enemigo pegado a la pared tiene que seguir
recibiendo el tiro") pero ese caso se resuelve comparando **distancias**, no salteando el chequeo.

### Bug 23 — El barrido de mundo de la bala trunca a 8 colliders · **P2**
**Archivo:** `Combat/Projectile.cs:324` (`RaycastHit[8]`), `:334`
**Síntoma:** en una zona con mucha geometría (follaje, escombros, barricadas) la bala puede no ver la
pared más cercana y atravesarla.
**Causa raíz:** `RaycastNonAlloc` devuelve los primeros N hits **sin ordenar**; con 8 slots y más de 8
colliders en el tramo, el más cercano puede quedar fuera del recorte. Mismo patrón en
`NavService.BufferDeTiro` (8), `Projectile.BufferExplosion` (8) y `SoldierMotor.SondeoPiso` (12).

### Bug 24 — Cada proyectil clona un `Material` que nadie destruye nunca · **P2**
**Archivo:** `Combat/Projectile.cs:152-168` (`new Material(baseMat)`)
**Síntoma:** con un pool de 128 proyectiles son 128 materiales huérfanos por partida; se acumulan
sesión tras sesión en el Editor sin recarga de dominio.
**Causa raíz:** `AsegurarMaterial` clona y marca `ownMaterialReady`, pero no hay `OnDestroy` que llame
a `Destroy(cachedRenderer.sharedMaterial)`. El proyecto sí lo hace bien en
`PlayerInputDriver.CleanupDeathSequence:1029` y en `KillFeedbackDirector`.

### Bug 25 — Un proyectil sin pool queda vivo en la escena para siempre · **P2**
**Archivo:** `Combat/Projectile.cs:830-836` (`pool?.Release(this)`)
**Síntoma:** objeto huérfano activo, fuera de `ActiveInstances`, que ya no se mueve ni se puede ver
desde el contador de `PerfHudView`.
**Causa raíz:** `Expire()` pone `active = false` y llama `pool?.Release(this)`. Con `pool == null` (un
proyectil creado a mano por un test o una cinemática) nunca corre `OnDespawn()`, que es quien lo saca
de `ActiveInstances` y lo desactiva.

### Bug 26 — `OnDespawn` no resetea todo el estado por-disparo del proyectil · **P3**
**Archivo:** `Combat/Projectile.cs:178-190`
**Síntoma:** un proyectil reciclado hereda `proximaSupresion` (reloj de supresión) y
`ultimoImpactoFueCabeza` del disparo anterior.
**Causa raíz:** resetea `gravity`, `ignoreVehicle`, `whizzPlayed` y la escala, pero no los otros tres
campos mutables. En Edit mode, donde `Time.time` está congelado, `proximaSupresion` heredado significa
que la supresión queda apagada para siempre en esa instancia.

### Bug 27 — El fuego de supresión no corre en la suite headless · **P1**
**Archivo:** `Combat/Projectile.cs:471` (`if (!Application.isPlaying ...) return;`)
**Síntoma:** `SuprimirCercanos` (item 63: balas cercanas que agachan al enemigo) tiene **cero
cobertura** automatizada, igual que `AiBrain.RecibirSupresion` y `TickSupresion` río abajo.
**Causa raíz:** la guarda de Play mode se puso para no generar audio, pero abarca también el cálculo de
supresión, que es lógica de juego pura.

### Bug 28 — La explosión empuja soldados dentro de las paredes, y también en modo dios · **P1**
**Archivo:** `Combat/Projectile.cs:680` (`s.transform.position += away.normalized * strength * 2.2f`)
**Síntoma:** una granada cerca de un muro deja al soldado **adentro** del muro; desde ahí el
deslizamiento de `Deslizador` no lo saca porque el empuje perpendicular da cero. Y con `[F4]` el
soldado protegido no recibe daño pero igual sale despedido 2,2 m.
**Causa raíz:** escribe `transform.position` directo, sin pasar por `Deslizador.Resolver` (que es el
único camino de colisión del proyecto, ver `SoldierMotor.Resolve`). Y el bloque de empuje está fuera de
la guarda de `ModoDios.Protege`, que vive dentro de `Health.TakeDamage`.

---

# C. Vida, daño y dificultad (29–34)

### Bug 29 — Revivir a un soldado no avisa a nadie · **P0**
**Archivo:** `Combat/Health.cs:52-66` (`Initialize`), usado por `Player/RescateAutomatico.cs:97`,
`Player/PlayerInputDriver.cs:1538` (`TryRevivir`), `Core/Dificultad.cs:101`
**Síntoma:** al revivir a un caído: la barra de vida sigue mostrando 0, el roster sigue marcándolo como
caído, `MisionDirector` y `GameOutcomeController` siguen contando una baja, el kill feed nunca lo
corrige y `SelectionController` no lo vuelve a aceptar.
**Causa raíz:** `TakeDamage` publica `DamageTakenEvent` y `EntityDiedEvent`; `Heal` publica
`HealedEvent`. `Initialize` — que es el **único** camino de revivir del juego — no publica nada. No
existe un `EntityRevivedEvent` en `Core/Events.cs`.

### Bug 30 — `Heal()` no puede revivir, pero su propio comentario dice que sí · **P3**
**Archivo:** `Combat/Health.cs:22-27` (comentario) vs. `:157` (`if (!IsAlive || amount <= 0) return;`)
**Síntoma:** cualquiera que siga el comentario ("eso ya lo hace `Heal()` para revivir") escribe código
que no hace nada en silencio.
**Causa raíz:** la guarda `!IsAlive` corta antes. El comentario quedó de una versión anterior.

### Bug 31 — Un tanque destruido publica `EntityDiedEvent(-1)`, un evento de muerte de soldado · **P0**
**Archivo:** `Vehicles/Vehicle.cs:35` (`health.Initialize(-1, maxHealth)`) → `Combat/Health.cs:149-152`
**Síntoma:** al destruirse cualquier vehículo, todo lo suscrito a `EntityDiedEvent` y
`DamageTakenEvent` recibe eventos con `ActorId = -1`: `PlayerInputDriver.OnEntityDied`,
`SelectionController.OnEntityDied`, `BattleManager`, `KillFeedView`, `AiBrain.OnAnyDamage` (que hace
`ActorRegistry.FindById(-1)`). El vehículo ya tiene sus propios `VehicleDamagedEvent` y
`VehicleDestroyedEvent`: estos son un duplicado con un Id que no corresponde a nadie.
**Causa raíz:** `Vehicle` reutiliza el componente `Health` de los soldados, que publica
incondicionalmente.

### Bug 32 — La dificultad no se aplica a los soldados que arrancan montados · **P1**
**Archivo:** `Core/Dificultad.cs:88-93` (`AplicarVida`)
**Síntoma:** la tripulación inicial de los tanques enemigos pelea con la vida base, sin el
multiplicador de dificultad, cuando se baja.
**Causa raíz:** recorre `ActorRegistry.All` sin llamar a `EnsureAllRegistered()` primero. Un soldado
guardado dentro de un vehículo arranca con el GameObject desactivado, no corre `Awake`, y no está en la
lista todavía. `SelectionController.CollectLivingAllies:187` sí lo hace bien.

### Bug 33 — `AplicarVida()` no es idempotente: llamarla dos veces compone el multiplicador · **P1**
**Archivo:** `Core/Dificultad.cs:95-103` (`AjustarVida`)
**Síntoma:** reintentar la misión sin recargar la escena deja a los enemigos con `0.95 × 0.95 = 0.90`
de vida; tres intentos, `0.857`. Cada reintento hace el juego más fácil sin decirlo.
**Causa raíz:** `s.Health.Initialize(s.Id, RoundToInt(s.Health.MaxHealth * k))` multiplica el máximo
**actual**, no un máximo base guardado. No hay ninguna marca de "ya ajustado".

### Bug 34 — El jugador cobra dos veces su ventaja de dificultad · **P0**
**Archivo:** `Core/Dificultad.cs:99` + `:124`
**Síntoma:** en MEDIO, el soldado que maneja el jugador tiene 115 de vida máxima (por `VidaAliados`) y
además recibe el daño dividido por 1,25 (por `VidaJugador`). Efectivo: 143 de vida donde el perfil
documenta 125. En FÁCIL: 135 × 1,5 = 202 donde el documento dice 150.
**Causa raíz:** el comentario del encabezado dice que `VidaJugador` "equivale a más vida **sin tocar los
números del HUD**", o sea que es la **alternativa** a subir la vida máxima. Pero `AjustarVida` no
excluye al soldado poseído del multiplicador `VidaAliados`, así que recibe los dos. Se agrava con el
bug 7: `MultiplicadorDeDano` llama a `FindById` sin `EnsureAllRegistered`, así que un atacante montado
no aporta su multiplicador y la asimetría crece.

---

# D. Inteligencia artificial (35–50)

### Bug 35 — La detección de atasco no puede dispararse nunca en Patrulla, Seguir, Rodeo ni Defensiva · **P0**
**Archivo:** `Ai/AiBrain.Navegacion.cs:130-145` (`AvanzarConRodeo`) + `:19-24` (`PlanPathTo` →
`ResetStuckWatch`) + `Ai/AiBrain.cs:200` (`StuckSeconds = 1f`)
**Síntoma:** un soldado trabado contra un obstáculo en ronda de patrulla, siguiendo al jugador,
rodeando a un enemigo o volviendo a su puesto en postura Defensiva empuja la pared **para siempre**.
Nunca replanifica y nunca da la orden por cumplida.
**Causa raíz:** `AvanzarConRodeo` llama a `PlanPathTo(objetivo)` cada `RefrescoDeRodeo = 0.5f`
segundos; `PlanPathTo` llama a `ResetStuckWatch()`, que pone `stuckTimer = 0f`. `TickStuckWatch` exige
`stuckTimer >= StuckSeconds` (1 s) para siquiera mirar el progreso. 0,5 < 1, así que el temporizador se
reinicia antes de llegar al umbral, en un bucle infinito. Sólo `MovingToOrder` (que planifica una vez y
no refresca) llega a detectar atascos — que es exactamente el único caso que la suite prueba.

### Bug 36 — Recibir fuego mientras seguís al jugador cancela la orden de seguir para siempre · **P1**
**Archivo:** `Ai/AiBrain.cs:508-515`
**Síntoma:** ordenás "SÍGANME", el primer enemigo abre fuego, y cuando termina el tiroteo los aliados
se quedan parados en el lugar en vez de retomar el seguimiento. Se lee como una IA que "se olvida".
**Causa raíz:** en `OnAnyDamage`, con `State == AiState.Follow`, corre
`if (State != AiState.MovingToOrder) hasOrder = false;` **y** `followTarget = null;`. Cuando el combate
termina, el bloque de las líneas 793-823 evalúa `else if (hasOrder)` → false, `orderQueue` vacía →
`SetState(AiState.Patrol)`. La orden se perdió. La guarda protege a `MovingToOrder` y se olvida de
`Follow`.

### Bug 37 — (ver bug 13) El `Tick` del arma duplicado vive del lado de la IA · **P0**
**Archivo:** `Ai/AiBrain.cs:1065`
Se cuenta aparte porque el arreglo correcto es del lado de `AiBrain` (borrar la línea), no del driver.

### Bug 38 — Cada cambio de estado de IA asigna un `string` · **P2**
**Archivo:** `Ai/AiBrain.cs:493` (`new AiStateChangedEvent(self.Id, next.ToString())`)
**Síntoma:** basura de GC proporcional a los cambios de estado. El propio archivo documenta un caso
medido de "300 cambios de estado en 300 ticks" por soldado: con 50 soldados eso son 3.000 strings por
segundo.
**Causa raíz:** `AiStateChangedEvent` lleva el estado como `string` en vez de como el `enum AiState`.

### Bug 39 — Cada disparo del juego cuesta O(n²) en los oyentes de la IA · **P1**
**Archivo:** `Ai/AiBrain.cs:450-451` (una suscripción por soldado), `:496-541`, `:551-566`
**Síntoma:** con 50 soldados disparando ~3 veces por segundo cada uno, el `ShotFiredEvent` se publica
150 veces por segundo; cada publicación invoca 50 handlers; cada handler hace
`ActorRegistry.FindById(...)`, que es un barrido **lineal** de los 50 soldados. Son 375.000
comparaciones por segundo sólo para decidir si alguien oyó un tiro.
**Causa raíz:** es el mismo `O(n·m)` que `SpatialGrid` vino a resolver para el sensado y para los
proyectiles; este camino nunca se pasó a la grilla. Encima `FindById` es lineal (bug 7) cuando el
registro ya tiene un `HashSet` para el alta.

### Bug 40 — Granadas, supresión y avisos de la IA usan `Time.time`, que la suite no avanza · **P1**
**Archivo:** `Ai/AiBrain.Granadas.cs:29,30,50,57,88,94,108,117,128,142,151,167`,
`Ai/AiBrain.Tactica.cs:94,95,318`
**Síntoma:** en la suite headless (Edit mode) `Time.time` está congelado. `Suprimido` es
`Time.time < suprimidoHasta`: una vez suprimido, el soldado queda suprimido **para siempre**;
`proximaGranadaIA` nunca vence, así que la IA no vuelve a tirar una granada. Todo el subsistema de
granadas y supresión es incomprobable.
**Causa raíz:** el proyecto ya estableció la convención contraria y la documenta en `Health.cs:26-30`:
"en SEGUNDOS de `dt` acumulado, no `Time.time`... para poder simularse a mano en la suite headless".
Este archivo no la sigue.

### Bug 41 — La orden de suprimir se salta la máquina de estados, la postura y la línea de tiro · **P1**
**Archivo:** `Ai/AiBrain.Granadas.cs:60-73` (`TickSuprimirOrdenado`), invocado desde `AiBrain.cs:758`
**Síntoma:** un soldado con orden de suprimir dispara contra el punto aunque haya un aliado en el
medio, aunque su postura sea `AltoElFuego`, y durante 6 s no actualiza su objetivo ni reacciona a nada.
**Causa raíz:** devuelve `true`, y `TickGranadas` devuelve `true`, y `AiBrain.Tick` hace `return` en la
línea 758 — antes del `switch` entero. Además llama a `w.TryFire(...)` directo, sin pasar por
`StanceAllowsFire` ni por `TieneLineaDeTiro`, y gira con `LookTowards(punto, 20f)` (dt = 20 s, o sea
giro instantáneo).

### Bug 42 — Si la cobertura se destruye por completo, el soldado cree que sigue en pie · **P1**
**Archivo:** `Ai/AiBrain.Tactica.cs:139` (`if (coberturaDueno == null || Coberturas.Vigente(...)) return;`)
**Síntoma:** un soldado que se cubrió detrás de un barril que después explota se queda agachado detrás
de la nada, sin buscar otra cobertura y sin el aviso "¡COBERTURA DESTRUIDA!".
**Causa raíz:** la condición está invertida. Un `Collider` destruido en Unity da `== null` en la
comparación sobrecargada, así que `coberturaDueno == null` es precisamente el caso "mi cobertura ya no
existe" — y la línea lo trata como "todo bien, no hagas nada".

### Bug 43 — La ruta hacia el vehículo se planifica al pivote pero se camina al punto de abordaje · **P1**
**Archivo:** `Ai/AiBrain.cs:629-630` (`orderDestination = vehicle.transform.position; PlanPathTo(...)`)
vs. `:879` (`mountTarget.ClosestBoardingPoint(...)`)
**Síntoma:** con el chasis ya sólido para el movimiento, el A* devuelve una ruta cuyo último tramo
apunta **adentro** del casco. El soldado sigue los waypoints intermedios y se traba contra el vehículo
en el último.
**Causa raíz:** `IssueMountOrder` planifica contra `transform.position`; el `case MovingToOrder` camina
contra `ClosestBoardingPoint`. Dos destinos distintos para la misma orden.

### Bug 44 — Salir de rango de ataque con una orden de MOVER manda al soldado a `MovingToAttackOrder` · **P2**
**Archivo:** `Ai/AiBrain.cs:1047` (`SetState(hasOrder ? AiState.MovingToAttackOrder : AiState.Chase)`)
y `:1088`
**Síntoma:** el soldado entra en un estado que `Tick` trata como "orden protegida" (línea 828) y deja
de sensar blancos mejores, aunque la orden que tiene sea de movimiento, no de ataque.
**Causa raíz:** usa `hasOrder` (¿tengo alguna orden?) donde correspondía `orderIsAttack` (¿es una orden
de ataque?). El campo `orderIsAttack` existe justamente para distinguirlos.

### Bug 45 — Dibujar gizmos en la vista Scene inicializa soldados y les consume Ids · **P1**
**Archivo:** `Ai/AiBrain.cs:1178-1193` (`OnDrawGizmos` → `EffectiveAttackRange` →
`RoleAttackRangeMultiplier` → `self.Weapon` → `Soldier.Bootstrap()`)
**Síntoma:** con la escena abierta y los gizmos activados, cada repintado de la vista Scene
bootstrapea soldados fuera de Play: consume `Soldier.nextId`, los da de alta en `ActorRegistry`, llama
a `Health.Initialize` (reinicia la vida) y les agrega un `SoldierLook`. Rompe `ResetIdCounterForTests` y
cualquier comparación entre corridas.
**Causa raíz:** `Soldier.Weapon` es un getter con auto-bootstrap (ver el comentario de
`Soldier.cs:63-69`), y `OnDrawGizmos` lo alcanza por una cadena de tres propiedades.

### Bug 46 — Elegir blanco cuesta hasta dos raycasts por candidato, y el retarget dos más de regalo · **P2**
**Archivo:** `Ai/AiBrain.Tactica.cs:332-354` (`MejorObjetivoVisible`), `:310-322` (`PuntajeDeObjetivo`),
`:359-372` (`TickRetarget`)
**Síntoma:** con 8 candidatos en rango, un solo sensado puede costar 16 raycasts; `TickRetarget` corre
`MejorObjetivoVisible()` **y después** `PuntajeDeObjetivo(mejor)` y `PuntajeDeObjetivo(target)`, que
repiten raycasts ya hechos.
**Causa raíz:** `TieneLineaDeTiro` se llama en la rama de visión extendida (línea 348) y otra vez
adentro de `PuntajeDeObjetivo` (línea 315), sin cachear el resultado.

### Bug 47 — Las suscripciones al `EventBus` de la IA se acumulan entre corridas de la suite · **P2**
**Archivo:** `Ai/AiBrain.cs:444-465` (`Bootstrap` suscribe, `OnDestroy` libera)
**Síntoma:** en Edit mode `OnDestroy` no corre de forma fiable al desmontar la escena de un escenario;
la corrida siguiente arranca con los handlers de la anterior apuntando a `AiBrain` fake-null. Con
`RunMany(100)` eso son miles de handlers muertos que `EventBus.Publish` recorre y reporta como
excepción capturada (`EventBus.cs:60-66`).
**Causa raíz:** no hay un `EventBus.Instance.ClearAll()` entre escenarios de la suite, y `Bootstrap` no
comprueba si ya había una suscripción viva.

### Bug 48 — Agacharse no tiene ningún costo de velocidad · **P1**
**Archivo:** `Actors/SoldierMotor.cs:60` (`MoveSpeed => moveSpeed * (Corriendo ? 1.7f : 1f)`)
**Síntoma:** agachado se camina exactamente igual de rápido que de pie, y agachado se dispara un 60 %
más preciso (`FactorAgachado = 0.4f`). O sea: agacharse es puro beneficio, sin contrapartida. La IA
además se agacha durante todo `AiState.Attack` (`AiBrain.cs:1062`) y hace attack-move a velocidad
plena mientras dispara agachada.
**Causa raíz:** `MoveSpeed` contempla correr pero no agacharse.

### Bug 49 — Revivir a un soldado agachado deja la cámara hundida para siempre · **P0**
**Archivo:** `Actors/SoldierMotor.cs:175-182` (`ResetMotionState` pone `IsCrouching = false` directo)
vs. `:251-266` (`SetCrouching`, que es quien restaura `EyeAnchor.localPosition`)
**Síntoma:** si un soldado muere agachado y lo reviven (`RescateAutomatico.Tick:98`,
`PlayerInputDriver.TryRevivir:1539`), su `EyeAnchor` queda al 60 % de la altura. Al poseerlo, la cámara
en primera persona queda hundida en el pecho y `EyeHeightDrop` devuelve 0 porque `IsCrouching` ya es
false: no hay forma de recuperarlo sin volver a agacharse y levantarse.
**Causa raíz:** `SetCrouching` es el único sitio que mueve el `EyeAnchor`, y `ResetMotionState` escribe
la bandera sin pasar por él (y sin poder hacerlo: `SetCrouching` sale temprano si `IsCrouching == agachado`).

### Bug 50 — El buffer de salto sobrevive a la muerte y produce un salto fantasma · **P3**
**Archivo:** `Actors/SoldierMotor.cs:175-182` (`ResetMotionState` no limpia `saltoPedidoHasta` ni
`alturaDePivote`), consumido en `:202`
**Síntoma:** un soldado que muere con el buffer de salto armado (`Espacio` apretado en el aire) salta
solo al aterrizar la próxima vez que lo revivan.
**Causa raíz:** `ResetMotionState` limpia `IsJumping`, `Vaulting`, `verticalVelocity`, `IsCrouching` y
`pideCorrer`, pero no los dos campos que sobreviven.

---

# E. Motor del soldado y física (51–56)

### Bug 51 — Saltar y trepar no se simulan nunca en la suite headless · **P1**
**Archivo:** `Actors/SoldierMotor.cs:184-206` (`Update`), `:135-143` (`TickTrepa`), ambos con `Time.deltaTime`
**Síntoma:** `Jump`, `TryVault` y el aterrizaje sobre terreno variable tienen **cero** cobertura
automatizada. `Update` no corre en Edit mode y ningún camino de `WorldSimulationDriver.Step` los toca.
**Causa raíz:** el resto del motor (movimiento, giro, colisión) sí recibe `dt` por parámetro desde el
driver de simulación; la parábola de salto se quedó integrando por `Update`.

### Bug 52 — El sondeo de piso trunca a 12 hits y comparte buffer con el sondeo de obstáculos · **P3**
**Archivo:** `Actors/SoldierMotor.cs:146` (`static readonly RaycastHit[] SondeoPiso = new RaycastHit[12]`),
usado por `BuscarPiso:153` y `SondearObstaculo:124`
**Síntoma:** sobre geometría densa, el piso real puede quedar fuera del recorte y el soldado aterriza
flotando o hundido.
**Causa raíz:** `RaycastNonAlloc` no ordena por distancia y el buffer está compartido entre dos
sondeos distintos del mismo archivo (hoy nunca anidados, pero es una trampa a un refactor de distancia).

### Bug 53 — Cambiar de arma en primera persona rompe el ocultado del cuerpo · **P1**
**Archivo:** `Actors/Soldier.cs:80-101` (`bodyRenderers` se cachea una sola vez)
**Síntoma:** apuntando (cámara al ojo, cuerpo oculto con `SetBodyVisible(false, conservarArma: true)`),
si el jugador cambia de arma el modelo nuevo (`RealModel`, instanciado en ese momento por
`WeaponHolder.ApplyWeaponVisualModel`) no está en el cacheo: queda visible cuando debería ocultarse, o
invisible cuando debería verse, hasta la próxima posesión.
**Causa raíz:** `if (bodyRenderers == null) bodyRenderers = GetComponentsInChildren<Renderer>(true);`
se evalúa una vez en la vida del soldado. Nadie lo invalida al cambiar de arma.

### Bug 54 — `Soldier.Configure` cura a tope si se lo llama en partida · **P2**
**Archivo:** `Actors/Soldier.cs:114-123` (`health.Initialize(Id, maxHealth)`)
**Síntoma:** cualquier reconfiguración de un soldado en caliente (cambio de rol, refuerzos que se
reciclan) le devuelve la vida llena y le borra `LastAttackerId`.
**Causa raíz:** el arreglo que resincroniza `Health` con el `max` pedido usa `Initialize`, que además
de fijar el máximo resetea `Current`. Falta un camino "cambiar el máximo sin curar".

### Bug 55 — `Soldier.nextId` no se resetea entre partidas · **P2**
**Archivo:** `Actors/Soldier.cs:19-26`
**Síntoma:** los Ids crecen sin parar dentro de una sesión de Editor. Eso corre el desfasaje de
sensado (`(tickCount + Id % N) % N`, `AiBrain.Sentidos.cs:140`), el desfasaje de LOD
(`(tickNumero + s.Id) & 1`, `WorldSimulationDriver.cs:116`) y las ranuras de formación al seguir
(`(self.Id % 3)`, `Tactica.cs:265`): dos partidas "idénticas" reparten la carga distinto.
**Causa raíz:** el reset existe (`ResetIdCounterForTests`) pero sólo lo llama la suite, no
`ReinicioDeEstaticos`.

### Bug 56 — El cañón que sale volando es una pared invisible que nadie le avisa al navegador · **P2**
**Archivo:** `Vehicles/Vehicle.cs:149-168` (`DetachTurret` → `t.SetParent(null, true)`),
`Vehicles/DetachedTurretFlight.cs:39-45`
**Síntoma:** el cañón desprendido conserva sus colliders y deja de ser hijo del `Vehicle`, así que
`NavService.BlocksMovement` lo cuenta como **escenario sólido**: bloquea la línea de tiro mientras
vuela, y donde aterriza (6 s de vida) actúa como un muro. Nadie llama a `NavService.Invalidate()` ni al
aterrizar ni al destruirse. Además `transform.position.y <= 0.25f` ignora la altura del terreno: en una
cuesta el cañón se hunde en el suelo.
**Causa raíz:** sólo `ObstacleMarker` (demolición) y el cambio de escena invalidan la grilla de
navegación; cualquier otro objeto sólido que aparezca o desaparezca queda fuera del contrato.

---

# F. Navegación (57–60)

### Bug 57 — El rodeo de obstáculos está apagado en la suite headless · **P1**
**Archivo:** `Core/NavService.cs:199` (`if (!Application.isPlaying) return false;`)
**Síntoma:** todo el A* (`WaypointGraph`, `TryFindDetour`, `PlanPathTo`, `RodearHasta`, `CubrirseDe`,
`AvanzarConRodeo`) devuelve "sin desvío" en Edit mode. La suite valida una simulación en la que los
soldados **siempre van en línea recta**, que no es la que corre el juego. El bug 35 (detección de
atasco muerta) vive detrás de esta cortina.
**Causa raíz:** la guarda se puso porque `Build()` usa `FindObjectsByType`, pero eso funciona
perfectamente en Edit mode.

### Bug 58 — Demoler un muro reconstruye la grilla entera en medio de un tick de IA · **P2**
**Archivo:** `Core/NavService.cs:89-94` (`Invalidate`) → `:130-135` (`EnsureBuilt`) → `:212-242` (`Build`)
**Síntoma:** pico de frame al demoler. Con el piso actual (58 × 160 m, spacing 2) son ~2.400 nodos ×
un `Physics.OverlapBoxNonAlloc` cada uno, **más** un `FindObjectsByType<Collider>` de toda la escena,
todo síncrono dentro del primer `AdvanceTo` que consulte la versión.
**Causa raíz:** `Invalidate` es "todo o nada": no existe una invalidación por región (sólo los nodos
dentro del `bounds` del obstáculo caído).

### Bug 59 — El sondeo de nodo trunca a 16 colliders · **P3**
**Archivo:** `Core/NavService.cs:51` (`Collider[16]`), `:250`
**Síntoma:** un nodo con más de 16 colliders encima puede quedar marcado como caminable.
**Causa raíz:** `OverlapBoxNonAlloc` recorta sin avisar; el código no compara `n` contra la capacidad
del buffer.

### Bug 60 — El área jugable incluye a los soldados · **P2**
**Archivo:** `Core/NavService.cs:217-223`
**Síntoma:** los límites que usan la cámara RTS, el minimapa y la orden de retirada se estiran hasta
donde haya un soldado, no hasta donde llega el terreno.
**Causa raíz:** el bucle que calcula `area` sólo descarta triggers. `BlocksMovement` (que sí descarta
soldados y proyectiles) se usa después, en `IsBlockedAt`, pero no acá.

---

# G. Servicio de órdenes (61–70)

### Bug 61 — Cada orden a la escuadra barre la escena una vez por soldado · **P1**
**Archivo:** `Player/OrderService.cs:98` (`FindObjectsByType<Vehicle>(FindObjectsInactive.Include)`)
**Síntoma:** con la escuadra entera seleccionada, un solo clic derecho hace tantos barridos completos
de la escena como soldados seleccionados.
**Causa raíz:** el propio comentario de arriba (líneas 87-95) explica por qué eso no se puede hacer y
propone el filtro barato — y después lo hace igual. `WorldSystemsRegistry.Vehicles` existe exactamente
para esto y `PlayerInputDriver.FindVehicleContaining:2287` sí lo usa.

### Bug 62 — La orden "SÍGANME" se la come también un cadáver y el soldado que manejás · **P1**
**Archivo:** `Player/OrderService.cs:521-536` (`IssueFollowOrderForSelection`)
**Síntoma:** el aviso dice "Se dio la orden de seguir a 4 soldados" cuando dos están muertos; y el
soldado que el jugador tiene en las manos recibe la orden (aunque `IssueFollowOrder` la rechace
después, ya contó en el lote y en las ranuras de formación).
**Causa raíz:** es la **única** orden de lote que usa `new List<Soldier>(selection)` en vez de
`AliveOnly(selection)`. El filtro existe y las otras seis lo usan; el comentario de `AliveOnly:53-58`
explica por qué el conteo tiene que ser honesto.

### Bug 63 — La validación de destino por física sólo corre en RTS · **P1**
**Archivo:** `Player/OrderService.cs:334-354` (`IsValidDestination`), usado únicamente en
`Player/PlayerInputDriver.cs:2931`
**Síntoma:** ordenar "IR ALLÍ" desde el radial `[Q]`, desde `[T]`, desde `Shift+[T]`, desde el trazado
de recorrido o desde la retirada acepta destinos dentro de un muro sin avisar. Sólo el clic derecho de
RTS lo comprueba.
**Causa raíz:** `IssueMoveOrder` valida con `PuntoAlcanzable` (NavMesh) y no con `IsValidDestination`
(física). Son dos validaciones distintas en dos capas distintas, y ninguna de las dos está en todos los
caminos.

### Bug 64 — Sin NavMesh horneado, la validación de destino acepta todo · **P1**
**Archivo:** `Player/OrderService.cs:111-119` (`PuntoAlcanzable`)
**Síntoma:** en la suite headless y en cualquier escena sin bake, `IssueMoveOrder` nunca rechaza nada:
el item 62 ("IR ALLÍ ya no acepta cualquier punto") no tiene cobertura y no aplica.
**Causa raíz:** `if (!NavMesh.SamplePosition(desde, out _, 4f, ...)) return true;` — sin malla cerca
del soldado devuelve "alcanzable" por diseño. Combinado con el bug 63 (la validación por física no se
llama acá) no queda ninguna comprobación en pie.

### Bug 65 — `OrderService` usa `GetComponent<AiBrain>()` en vez del `Brain` cacheado · **P2**
**Archivo:** `Player/OrderService.cs:133`, `:154`, `:362`, `:492`, `:540`
**Síntoma:** cinco `GetComponent` por soldado por orden; con la escuadra entera son 250 llamadas por
clic.
**Causa raíz:** `Soldier.Brain` se agregó justamente para evitar esto (ver `Soldier.cs:55-60`:
"`WorldSimulationDriver` hacía `GetComponent<AiBrain>()` por soldado en cada frame"). `OrderService`
quedó con la versión vieja en los cinco sitios.

### Bug 66 — `IssueMountOrder` no comprueba nada · **P2**
**Archivo:** `Player/OrderService.cs:538-543`
**Síntoma:** se le puede ordenar subir al tanque a un cadáver, a un civil o al soldado que el jugador
está manejando a mano, y se dibuja el marcador igual.
**Causa raíz:** es la única `Issue*` sin `LoManejaElJugador`, sin comprobar `Health.IsAlive` y sin
guarda de null. `IssueMountOrderForSelection` sí filtra con `AliveOnly`, así que el agujero está sólo en
la versión individual.

### Bug 67 — Dos `Issue*` pueden reventar con `NullReferenceException` · **P3**
**Archivo:** `Player/OrderService.cs:126` (`soldier.transform.position`), `:374` (`enemy.transform.position`)
**Causa raíz:** `LoManejaElJugador(null)` devuelve `false` en vez de cortar, así que `IssueMoveOrder`
sigue adelante y derreferencia `soldier`. `IssueAttackOrder` derreferencia `enemy` para el marcador.

### Bug 68 — Cada orden de lote asigna tres colecciones · **P2**
**Archivo:** `Player/OrderService.cs:448` (`list.ConvertAll(s => s.Id).ToArray()`)
**Causa raíz:** `ConvertAll` crea una `List<int>`, `ToArray` crea el array, y el lambda captura. Hay
además un `new List<Soldier>()` por cada `AliveOnly`. Con órdenes encadenadas en RTS es basura por clic.

### Bug 69 — El rescatista automático nunca sale a rescatar · **P0**
**Archivo:** `Player/RescateAutomatico.cs:51` (`OrderService.IssueFollowOrder(rescatista, caido)`) vs.
`Ai/AiBrain.cs:927-933`
**Síntoma:** la función A5 completa ("un aliado libre va a revivirte y frena el timer") no funciona
nunca. El timer de los 5 s de la cámara de muerte se para (porque `RescateAutomatico.Activo` es true),
pero el rescatista se queda donde está y a los 30 s (`EsperaMaxima`) el rescate se cae solo. El jugador
espera media hora mirando su propio cadáver.
**Causa raíz:** manda una orden de **seguir** a un soldado **muerto**. El `case AiState.Follow` de
`AiBrain.Tick` arranca con
`if (followTarget == null || !followTarget.Health.IsAlive || ...) { hasOrder = false; followTarget = null; SetState(Patrol); break; }`
— o sea que la suelta en el primer tick, por definición, porque el caído siempre está muerto.

### Bug 70 — Los estáticos del rescate sobreviven a la partida · **P2**
**Archivo:** `Player/RescateAutomatico.cs:30-35`
**Síntoma:** la partida siguiente arranca con `Activo == true` apuntando a un `Soldier` fake-null; la
cámara de muerte cree que te están reviviendo y no baja nunca el timer de A3.
**Causa raíz:** `Caido`, `Rescatista`, `restante` y `canalizado` son estáticos sin
`RuntimeInitializeOnLoadMethod` y fuera de `ReinicioDeEstaticos` (bug 1).

---

# H. Entrada del jugador (71–84)

### Bug 71 — El HUD del arma desaparece para siempre después de la primera muerte · **P0**
**Archivo:** `Player/PlayerInputDriver.cs:889` (`WeaponStatus.gameObject.SetActive(false)`) sin
contraparte en `UpdateFps`
**Síntoma:** morís una vez, te reviven o poseés a otro, y el panel de arma/munición de la esquina ya no
vuelve nunca. El resto de la partida se juega sin saber cuántas balas quedan.
**Causa raíz:** `DeathSequence` apaga `WeaponStatus`, `VehicleStatus`, `TurretAim`, `AimUiRef`,
`weaponViewmodel` y `PlayerHealth`. `UpdateFps` vuelve a encender `AimUiRef` (línea 1106) y el
viewmodel, y llama a `WeaponStatus.UpdateFrom(...)` (línea 1262) **sin** volver a activarlo.

### Bug 72 — Los aliados se quedan corriendo para siempre si pasás a RTS esprintando · **P1**
**Archivo:** `Player/PlayerInputDriver.cs:1170` (`AjustesDeEscuadra.Correr = correr;`)
**Síntoma:** `Shift` + `Tab` deja a toda la escuadra libre corriendo el resto de la partida (y en la
siguiente, por el bug 1).
**Causa raíz:** `Correr` es un estático que sólo se escribe dentro de `UpdateFps`. En RTS, dentro de un
vehículo, en la ametralladora fija o durante la cámara de muerte, `UpdateFps` no corre y el valor queda
congelado en el último `true`. `AiBrain.ActualizarCarrera:698` lo lee en cada tick.

### Bug 73 — La rueda del mouse cambia de arma con el radial abierto · **P1**
**Archivo:** `Player/PlayerInputDriver.cs:1333-1337`
**Síntoma:** mientras elegís una orden en el radial `[Q]`, cualquier roce de la rueda te cambia el arma
en la mano.
**Causa raíz:** el bloque de las teclas `1/2/3` está protegido con `if (!menuDeOrdenesAbierto)` (líneas
1317-1327) precisamente por este motivo, y el bloque de la rueda quedó afuera de esa protección.
`Rig.SetZoomed(mouse.rightButton.isPressed)` (línea 1413) tiene el mismo problema.

### Bug 74 — La posesión inicial no publica `PossessionChangedEvent` · **P1**
**Archivo:** `Player/PlayerInputDriver.cs:490` (`Brain.Possess(Squad[0])` en vez de
`PossessionService.Swap`)
**Síntoma:** ninguna vista suscrita a `PossessionChangedEvent` (marcador de poseído, minimapa,
`DamageVignetteView`, `SelectedSoldierUI`, `LowHealthPulseView`) se entera de quién es el soldado del
jugador hasta el primer cambio manual.
**Causa raíz:** `PossessionService.Swap` existe para publicar el evento; el arranque lo saltea y lo
parchea con un `FindAnyObjectByType<RosterView>().Rebuild()` (líneas 497-498) que cubre **una** de las
vistas afectadas.

### Bug 75 — `OnDisable` limpia un estático y se olvida del otro · **P2**
**Archivo:** `Player/PlayerInputDriver.cs:538` (limpia `OrderService.ManejadoAMano`) vs. `:633`
(escribe `AjustesDeEscuadra.Lider`)
**Síntoma:** `AjustesDeEscuadra.Lider` queda apuntando al soldado de la partida anterior; los aliados de
la partida nueva intentan seguir a un fantasma hasta que `TickSeguirAlJugador` detecta el fake-null.
**Causa raíz:** el comentario del `OnDisable` explica exactamente este riesgo y sólo cubre un caso.

### Bug 76 — Modo dios y panel de diagnóstico están cableados a teclas fijas · **P2**
**Archivo:** `Player/PlayerInputDriver.cs:649` (`kb.pKey`), `:674` (`kb.f4Key`)
**Síntoma:** `[F4]` y `[P]` no se pueden reasignar desde `KeyRebindView`, a diferencia de las ~20
acciones que sí pasan por `KeyBindings`.
**Causa raíz:** lectura directa de `Keyboard.current` en vez de `KeyBindings.WasPressed(...)`.

### Bug 77 — RTS lanza cuatro raycasts de puntería por frame · **P2**
**Archivo:** `Player/PlayerInputDriver.cs:2825`, `:2868`, `:2879`, más `UpdateCoverPreviewRts:1891`
**Síntoma:** cuatro `Aim.Evaluate` (cada uno un `Physics.Raycast`) por frame en vista RTS.
**Causa raíz:** el comentario de la línea 2821 dice explícitamente "reusado... sin pagar un segundo
`Physics.Raycast` por frame" — y después tres sitios más lo vuelven a calcular con el mismo rayo.

### Bug 78 — Orden de mover al vehículo sin `VehicleBrain` revienta · **P3**
**Archivo:** `Player/PlayerInputDriver.cs:2427-2431`
**Causa raíz:** `var vb = vehicle.GetComponent<VehicleBrain>();` y dos líneas después `vb.HasOrder`
sin comprobar null. El método sí comprueba `vehicle == null` y `vehicle.Driver == null`.

### Bug 79 — El médico cura a través de todo el mapa · **P1**
**Archivo:** `Player/PlayerInputDriver.cs:1660-1679`
**Síntoma:** apuntando a un aliado herido a 80 m y sosteniendo `[Ctrl]`, el médico lo cura a 25 de vida
por segundo sin moverse.
**Causa raíz:** `Soldier herido = (result.Type == Ally && Herido(result.Soldier)) ? result.Soldier :
FindNearestWoundedAlly();` — la rama del "más cercano" respeta `interactRadius` (3,5 m), la rama de la
mira no comprueba distancia ninguna. `Aim.MaxDistance` es el alcance del rayo de cámara.

### Bug 80 — Las pisadas consultan la física sin filtrar triggers · **P2**
**Archivo:** `Player/PlayerInputDriver.cs:2192`
**Síntoma:** parado encima de un pickup de munición, de una zona trigger o de la caja de apuntado de una
ametralladora fija, el sonido de pisada cambia a "pasto".
**Causa raíz:** `Physics.Raycast(pos + up*0.4f, down, out hit, 1.5f)` sin `QueryTriggerInteraction.Ignore`
ni máscara de capa, cuando el resto del proyecto siempre pasa `~0, QueryTriggerInteraction.Ignore`.

### Bug 81 — El clic seco suena en cada auto-recarga · **P2**
**Archivo:** `Player/PlayerInputDriver.cs:1283-1296` + `Combat/WeaponHolder.cs:604-609`
**Síntoma:** al vaciar el cargador con el gatillo apretado suena el "clic de arma vacía" **además** del
sonido de recarga, aunque el arma sí está recargando normalmente.
**Causa raíz:** `emptyBeforeFire` es `CurrentAmmo <= 0 && !IsReloading`; `TryFire` con 0 balas llama a
`StartReload()` y devuelve `false`. Las dos condiciones del `if (!fired && emptyBeforeFire ...)` se
cumplen en el mismo frame. El comentario dice que sólo debería sonar "si de verdad no disparó por falta
de munición", que es el caso `SinMunicionTotal`.

### Bug 82 — Guarda muerta en el camino de órdenes de RTS · **P3**
**Archivo:** `Player/PlayerInputDriver.cs:2898-2902`
**Causa raíz:** dentro del bloque `if (pidioOrden) { ... }` se hace `pidioOrden = false;` para abortar,
pero ninguna de las ramas siguientes vuelve a leer la variable. Funciona de casualidad porque los
`else if` comprueban `Selection.Selected.Count > 0`.

### Bug 83 — El viewmodel del arma y la óptica se recalculan cada frame sin dibujarse · **P2**
**Archivo:** `Player/PlayerInputDriver.cs:226` (`weaponViewmodelRenderer.enabled = false`), `:246`
(le asigna color igual), `:284-290` (crea la `MiraOptica` y después `Mostrar(false)`)
**Síntoma:** un `GameObject` con `MeshRenderer`, un `Material` con shader propio, un tubo de óptica y
toda su matemática de encuadre corren cada frame para producir exactamente nada en pantalla.
**Causa raíz:** dos features se reemplazaron (el cubo por `ArmaEnLaMano`, la óptica por `MirillaView`)
sin retirar el código anterior, que quedó vivo como pivote de algo que también está apagado.

### Bug 84 — `Shift+[T]` no acusa recibo · **P2**
**Archivo:** `Player/PlayerInputDriver.cs:1982-1997`
**Síntoma:** la orden de repartir a toda la escuadra en formación no suena, no dibuja el destello del
anillo (`OrderAcknowledgedEvent`), no entra en `OrderHistory` y no emite la voz de acuse.
**Causa raíz:** llama a `OrderService.IssueMoveOrder` en bucle en vez de a
`IssueFormationOrderForSelection`, saltándose `AnnounceBatch` — el cierre común que el propio
`OrderService` documenta como obligatorio para toda orden de lote.

---

# I. Vehículos y torretas (85–93)

### Bug 85 — Bajarse del tanque te cambia de tamaño · **P1**
**Archivo:** `Vehicles/Vehicle.cs:546` (`soldier.transform.localScale = Vector3.one;`)
**Síntoma:** un soldado con escala no uniforme (el cuerpo del proyecto es 0.9/1.6/0.9, ver el comentario
de `WeaponHolder.cs:274`) sale del tanque con escala 1/1/1: más bajo y más gordo que el resto.
**Causa raíz:** `Dismount` descarta la escala real en vez de usar `mountTrueScale[soldier]`, que es el
diccionario que `Mount` llena justamente para eso (`Vehicle.cs:179`, `:363`).

### Bug 86 — Cambiar de asiento puede perder a un soldado · **P0**
**Archivo:** `Vehicles/Vehicle.cs:570-587` (`SwapSeats`), `:595-602` (`MoveToSeat`)
**Síntoma:** el soldado queda con el GameObject desactivado, sin asiento, sin `AiBrain` y sin forma de
volver a aparecer. Está vivo para `ActorRegistry` pero no existe en el mundo.
**Causa raíz:** los dos métodos hacen `Dismount(...)`, `SetActive(false)` y después `Mount(...)`
**sin comprobar el valor de retorno**. `Mount` devuelve `false` si el vehículo se destruyó en el medio
(`IsDestroyed`), si el soldado murió, o si no quedó asiento libre. No hay rollback.

### Bug 87 — `Vehicle.Occupants` asigna una lista nueva en cada lectura · **P2**
**Archivo:** `Vehicles/Vehicle.cs:226-234`
**Síntoma:** basura por frame. `VehicleStatusView`, `VehicleKeysPanel`, `SetDestination`,
`FindVehicleContaining` y `PlayerInputDriver.BajarATodos` lo leen; `OnDestroyed` encima hace
`new List<Soldier>(Occupants)` (dos listas de una).
**Causa raíz:** el getter construye la lista a demanda en vez de mantener una cacheada que sólo cambie
en `Mount`/`Dismount`.

### Bug 88 — El tanque clona el material de todo lo que cuelgue de él · **P2**
**Archivo:** `Vehicles/Vehicle.cs:189-201` (`GetComponentsInChildren<Renderer>()` sin filtro)
**Síntoma:** `chassisRenderers` incluye la torreta, la metralleta y — cuando hay un artillero de pie en
la escotilla, que se cuelga del chasis (`Vehicle.cs:462`) — también al **soldado**. `RefreshOccupancyColor`
le escribe el color del chasis encima: el artillero se pone del color del tanque. Y después de
`DetachTurret`, el array sigue apuntando a la torreta ya desprendida, así que `FinalExplosion` la pinta
de negro en pleno vuelo.
**Causa raíz:** no se excluyen los renderers de soldados ni se recalcula el array al cambiar la
jerarquía.

### Bug 89 — Bajarse del tanque no resetea el estado de movimiento · **P3**
**Archivo:** `Vehicles/Vehicle.cs:524-560` (`Dismount`)
**Causa raíz:** un soldado que subió a mitad de salto (o suprimido, o agachado) baja con `IsJumping`
activo y el `EyeAnchor` bajado. `ResetMotionState()` existe y `RescateAutomatico` sí lo llama.

### Bug 90 — `ResetTodos` limpia una lista de vehículos y deja la otra sucia · **P2**
**Archivo:** `Vehicles/Vehicle.cs:88-89` (`Todos.Clear()`) vs. `Core/WorldSystemsRegistry.cs:81-89`
**Síntoma:** `WorldSystemsRegistry.Vehicles` arranca la partida siguiente con referencias fake-null;
`Projectile.ExplodeAt:686` y `PlayerInputDriver.FindTheVehicle:2308` las recorren.
**Causa raíz:** hay dos registros paralelos de vehículos (`Vehicle.Todos` y `WorldSystemsRegistry`) y
sólo uno tiene reset.

### Bug 91 — Instalar las ametralladoras fijas modifica la escena desde Edit mode · **P1**
**Archivo:** `Vehicles/TorretaFija.cs:168-203` (`InstalarEnEscena` / `Instalar`)
**Síntoma:** cada corrida de la suite agrega un `TorretaFija` y un `BoxCollider` a cada emplazamiento;
si alguien guarda la escena después, quedan horneados. Es la misma clase de bug que el ítem 33 de la
auditoría anterior ("cada corrida reescribe SC_TestLevel, 52 mil líneas de diff").
**Causa raíz:** `AddComponent` sin `Undo`, sin `HideFlags.DontSave` y sin marcar la escena como
temporal.

### Bug 92 — `Ocupar` derreferencia el arma sin comprobarla · **P3**
**Archivo:** `Vehicles/TorretaFija.cs:102-103` (`var arma = s.Weapon; armaPreviaIndice = arma.CurrentLoadoutIndex;`)
**Causa raíz:** comprueba `s == null` y `s.Health`, pero no `s.Weapon`. `Liberar` sí lo comprueba
(`if (s.Weapon != null)`): el mismo archivo con dos contratos.

### Bug 93 — Descargar la escena teletransporta al ocupante de la ametralladora · **P3**
**Archivo:** `Vehicles/TorretaFija.cs:50-54` (`OnDisable` → `Liberar()` → `:126`)
**Causa raíz:** `Liberar` reposiciona al soldado en `posicionPrevia`. Al descargar la escena, `OnDisable`
corre en un orden indefinido y puede mover a un soldado que ya se está destruyendo (o que otro sistema
está guardando).

---

# J. Selección, UI y presentación (94–100)

### Bug 94 — `SelectSingle(null)` revienta al publicar · **P1**
**Archivo:** `Player/SelectionController.cs:31-37` y `:205-210` (`Publish` → `s.Id`)
**Síntoma:** `NullReferenceException` en `Publish`, que además deja la selección con un null adentro
que van a derreferenciar todas las órdenes siguientes.
**Causa raíz:** `SelectSingle` y `AddToSelection` no comprueban null; `SelectAll` y `SelectWoundedOnly`
sí (`s != null && s.Health != null && s.Health.IsAlive`).

### Bug 95 — La selección conserva a los que se suben al tanque · **P1**
**Archivo:** `Player/SelectionController.cs:19-24` (sólo reacciona a `EntityDiedEvent`)
**Síntoma:** seleccionás a los tres, dos se suben al tanque, y las órdenes siguientes se emiten a
soldados desactivados: el conteo del HUD miente, el marcador aparece en el piso y nadie se mueve.
**Causa raíz:** no hay ningún evento de "montó / desmontó" al que suscribirse, y la lista no se
saneaa en `Publish`.

### Bug 96 — Cada cambio de selección asigna una `List<int>` · **P2**
**Archivo:** `Player/SelectionController.cs:205-210`
**Causa raíz:** `var ids = new List<int>();` por publicación, más el `List<Soldier>` que
`SelectWoundedOnly` y `SelectSameTypeOnScreen` crean por llamada. En un arrastre de selección esto
corre por frame.

### Bug 97 — Hay 34 `FindAnyObjectByType` / `FindObjectsByType` en caminos de runtime · **P2**
**Archivos:** `UI/RosterRowView.cs:93`, `UI/RosterView.cs:38`, `UI/DamageVignetteView.cs:38`,
`UI/DamageDirectionView.cs:101`, `UI/SelectedSoldierUI.cs:111`, `UI/LowHealthPulseView.cs:105`,
`UI/OffscreenAllyMarkerView.cs:51,128`, `UI/MirillaView.cs:120,122`, `UI/MenuDeOrdenes.cs:459,464,468`,
`UI/AliadosSinEstorbo.cs:74`, `UI/HudPulido.cs:43`, `UI/AjustesDeJuego.cs:39`,
`Presentation/PauseController.cs:95,96`, `Presentation/MinimapIcon.cs:266`,
`Presentation/UnitLabelView.cs:175,177,179`, `Presentation/RevivePromptView.cs:128`,
`Presentation/WorldUiDirector.cs:328-336`, `Mision/MisionDirector.cs:81,82,103,145,321`,
`Mision/MisionHud.cs:22`, `Mision/Helicoptero.cs:164`, `Mision/CinematicaDeVictoria.cs:92,133,250`,
`Player/Rehen.cs:86`, `Player/Demolicion.cs:186`, `Player/CajaDeSuministros.cs:94,137`,
`Player/MunicionPickup.cs:60`, `Player/PlayerInputDriver.cs:497`,
`Player/PlayerInputDriver.Escuadra.cs:46`, `Core/Loc.cs:161`, `Core/Coberturas.cs:46`,
`Core/ApoyoEnElPiso.cs:56,61`, `Tutorial/TutorialAutoPlayer.Entrada.cs:76,191`,
`Tutorial/TutorialAutoPlayer.Radial.cs:128`
**Síntoma:** varios son cacheados perezosos con `if (x == null)` y están bien; pero
`RosterRowView.cs:93` corre en el refresco de **cada fila** del roster, `MisionDirector.cs:321` y
`MinimapIcon.cs:266` corren en bucles de actualización, y `Loc.cs:161` barre todos los `Text` de la
escena al cambiar de idioma.
**Causa raíz:** no hay una regla verificable. `Tools/Ci/verificar_estatico.py` sería el lugar natural
para prohibirlo fuera de `Awake`/`Start`/`Asegurar*`.

### Bug 98 — Tres vistas más escriben sobre `sharedMaterial` compartido · **P1**
**Archivo:** `Vehicles/TurretWeapon.cs:433-436` (calor del cañón),
`Presentation/SquadStateIndicatorView.cs:142` (cubo de estado de escuadra),
`Presentation/EnemyAlertIndicatorView.cs:128,166,176` (el "!" de alerta enemiga)
**Síntoma:** `TurretWeapon` se protege clonando el material (línea 433) — pero sólo la primera vez, y
si `rend.sharedMaterial` era null no clona y después escribe igual. `SquadStateIndicatorView` y
`EnemyAlertIndicatorView` no clonan nada: **todos** los indicadores de alerta del mapa cambian de color
a la vez cuando uno solo se alerta.
**Causa raíz:** el proyecto ya resolvió esto dos veces con `MaterialPropertyBlock`
(`CubeFxReactor.WriteTint`, `SelectionRingFx.cs:132`, `OrderMarkerFx.cs:246`, `ImpactFx.cs:78`) y estas
tres vistas quedaron con la versión vieja.

### Bug 99 — El indicador de abordaje tiñe el material de todos los `LineRenderer` · **P2**
**Archivo:** `Presentation/VehicleMountIndicator.cs:69-75`
**Causa raíz:** escribe `shaftRenderer.sharedMaterial.color`, `headRenderer.sharedMaterial.color` y
`lr.sharedMaterial.color` en bucle. Si los `LineRenderer` salen de `SafeMaterial.CreateShared()` (que es
el patrón del proyecto), todos apuntan al mismo material.

### Bug 100 — Los contadores estáticos que la suite usa como aserción no se resetean entre iteraciones · **P1**
**Archivo:** `Presentation/Feedback.cs:24-25` (`Contador`, `UltimoTexto`),
`Presentation/CuchilloFx.cs:12-13` (`Tajos`, `Aciertos`),
`Presentation/SelectionRingFx.cs:63` (`SpawnCount`),
`Presentation/AudioDirector.cs:236` (`TotalReproducidos`),
`Presentation/Subtitulos.cs:22` (`Emitidos`),
`Ai/AiBrain.Granadas.cs:31-32` (`GranadasLanzadasPorIA`, `HuidasDeGranada`)
**Síntoma:** `RunMany(100)` (la corrida de flakiness) arrastra los contadores de una iteración a la
siguiente. Una aserción de la forma "después de esta fase, `CuchilloFx.Aciertos == 1`" pasa en la
primera iteración y falla en las 99 restantes, o al revés: una que compara "> 0" pasa siempre porque
nunca vuelve a cero.
**Causa raíz:** `AiBrain.ReiniciarContadoresGranadaIA()` existe pero nadie lo llama entre escenarios;
los demás ni siquiera tienen un método de reset.

---

## Cómo se verificó cada hallazgo

Los 100 salen de lectura de código, no de suposición. Para cada uno está el archivo y la línea exacta,
y en los casos en que el propio proyecto documenta la regla que se rompe, está citado el comentario que
la establece (por ejemplo: `Health.cs:26-30` para la convención de `dt` acumulado contra `Time.time`;
`PlayerInputDriver.cs:2029` para `MaterialPropertyBlock` contra `sharedMaterial`; `Soldier.cs:55-60`
para el `Brain` cacheado contra `GetComponent`).

El plan de corrección, con la verificación concreta de cada uno (qué `Check()` agregar a
`HeadlessTestRunner` o qué secuencia correr en Play), está en
[`RONDA_10_PLAN_CORRECCION.md`](RONDA_10_PLAN_CORRECCION.md).
