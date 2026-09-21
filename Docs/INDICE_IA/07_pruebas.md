# Mapa de pruebas

## La suite headless

Corre en **Edit mode** con un reloj simulado (`SimulateSeconds` / `SimulateUntil`), llamando al mismo
`WorldSimulationDriver.Step(dt)` que el juego real. Entrada:
`Strategic Point > Run All Tests Headless` o
`-executeMethod SP.EditorTools.HeadlessTestRunner.RunAll`.

**561 aserciones (`Check`) en 6 archivos.**

| Archivo | Lineas | Fases | Checks | Algunas fases |
|---|---|---|---|---|
| `HeadlessTestRunner.cs` | 4197 | 7 | 121 | RunPhase1, RunPhase2, RunPhase3, RunPhase4, RunPhase5, RunPhase6 |
| `HeadlessTestRunner.Fase18.cs` | 129 | 1 | 34 | RunPhase18 |
| `HeadlessTestRunner.Fase19.cs` | 406 | 1 | 92 | RunPhase19 |
| `HeadlessTestRunner.Fase20.cs` | 207 | 2 | 32 | RunPhase20, RunPhase20Ronda12 |
| `HeadlessTestRunner.Fases13a17.cs` | 691 | 5 | 134 | RunPhase13, RunPhase14, RunPhase15, RunPhase16, RunPhase17 |
| `HeadlessTestRunner.Fases8a12.cs` | 1551 | 2 | 148 | RunPhase8, RunPhase9 |

## Otras entradas de prueba

| Menu | Que hace |
|---|---|
| `Run All Tests Headless` | La suite entera. Sale con 0 si todo paso, 1 si algo fallo. |
| `Correr 100 iteraciones (flakiness y fugas de estado)` | Repite la suite 100 veces. Detecta estado estatico que sobrevive entre corridas. |
| `Benchmark de rendimiento` | Mide el costo por bloque de `Step` con carga creciente. |
| `Verificar equivalencia SpatialGrid` | Compara la grilla contra el barrido lineal original. |
| `Estres con carga realista (50+)` | Escenarios con 50 o mas unidades. |
| `Tutorial > Reproducir Tutorial Automatico` | El tutorial entero con gestos reales y capturas. |

## Fuera de la suite

| Herramienta | Que cubre |
|---|---|
| `Tools/Ci/verificar_estatico.py` | `.meta` faltantes, reglas de codigo, conteos de auditoria, escenas del build. Corre en cada push, sin licencia de Unity. |
| `Tools/Qa/qa_total.bat` | Corrida completa: suite + build + metricas + capturas en 7 resoluciones + tutorial automatico + todos los logs. |
| `Tools/Autoplay/autoplay.sh` | Solo el tutorial automatico, desde git-bash. |

## Archivos de logica sin una sola asercion (85)

> Un archivo de `Ai`, `Combat`, `Player`, `Vehicles`, `Core`, `Actors` o `Mision` sin ningun `Check`
> puede estar cubierto desde otro archivo — pero vale mirarlo.

- `Assets/_Project/Scripts/Actors/Soldier.cs`
- `Assets/_Project/Scripts/Actors/SoldierClasses.cs`
- `Assets/_Project/Scripts/Actors/SoldierLook.cs`
- `Assets/_Project/Scripts/Actors/SoldierMotor.cs`
- `Assets/_Project/Scripts/Ai/AiBrain.cs`
- `Assets/_Project/Scripts/Ai/AiBrain.Disparo.cs`
- `Assets/_Project/Scripts/Ai/AiBrain.Granadas.cs`
- `Assets/_Project/Scripts/Ai/AiBrain.Navegacion.cs`
- `Assets/_Project/Scripts/Ai/AiBrain.Sentidos.cs`
- `Assets/_Project/Scripts/Ai/AiBrain.Tactica.cs`
- `Assets/_Project/Scripts/Ai/AiState.cs`
- `Assets/_Project/Scripts/Ai/AjustesDeEscuadra.cs`
- `Assets/_Project/Scripts/Ai/PathPreview.cs`
- `Assets/_Project/Scripts/Ai/WorldSimulationDriver.cs`
- `Assets/_Project/Scripts/Combat/Granada.cs`
- `Assets/_Project/Scripts/Combat/Health.cs`
- `Assets/_Project/Scripts/Combat/IWeapon.cs`
- `Assets/_Project/Scripts/Combat/Projectile.cs`
- `Assets/_Project/Scripts/Combat/ProjectilePool.cs`
- `Assets/_Project/Scripts/Combat/RoleType.cs`
- `Assets/_Project/Scripts/Combat/TeamId.cs`
- `Assets/_Project/Scripts/Combat/WeaponCatalog.cs`
- `Assets/_Project/Scripts/Combat/WeaponHolder.cs`
- `Assets/_Project/Scripts/Combat/WeaponKind.cs`
- `Assets/_Project/Scripts/Combat/WeaponModels.cs`
- `Assets/_Project/Scripts/Combat/WeaponPickup.cs`
- `Assets/_Project/Scripts/Core/ActorRegistry.cs`
- `Assets/_Project/Scripts/Core/ApoyoEnElPiso.cs`
- `Assets/_Project/Scripts/Core/CamaraPrincipal.cs`
- `Assets/_Project/Scripts/Core/Coberturas.cs`
- `Assets/_Project/Scripts/Core/ComandosDeDepuracion.cs`
- `Assets/_Project/Scripts/Core/Deslizador.cs`
- `Assets/_Project/Scripts/Core/Dificultad.cs`
- `Assets/_Project/Scripts/Core/EventBus.cs`
- `Assets/_Project/Scripts/Core/Events.cs`
- `Assets/_Project/Scripts/Core/FlowField.cs`
- `Assets/_Project/Scripts/Core/GameLog.cs`
- `Assets/_Project/Scripts/Core/Loc.cs`
- `Assets/_Project/Scripts/Core/LocTextos.cs`
- `Assets/_Project/Scripts/Core/MetricasDeBuild.cs`
- …y 45 mas
