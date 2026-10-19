# Mapa del bus de eventos

> `EventBus.Instance.Publish<T>` / `Subscribe<T>`. Los emisores no conocen a los oyentes: este mapa es
> la unica forma de ver la conexion completa.

**24 tipos de evento.**

| Evento | Publica | Escucha | Quien lo publica | Quien lo escucha |
|---|---|---|---|---|
| `AiStateChangedEvent` | 1 | 4 | `AiBrain.cs` | `ChecksBugs065.Wp4.cs`, `EnemyAlertIndicatorView.cs`, `SquadStateIndicatorView.cs`, `RosterRowView.cs` |
| `DamageTakenEvent` | 3 | 14 | `Health.cs`, `HeadlessTestRunner.Fase23.cs`, `HeadlessTestRunner.Fases8a12.cs` | `AiBrain.cs`, `TeamCombatState.cs`, `InteligenciaDeEnemigos.cs`, `EstadisticasDeMision.cs`, `PlayerInputDriver.cs`, `CubeFxReactor.cs` |
| `EntityDiedEvent` | 1 | 12 | `Health.cs` | `SesionLog.cs`, `EstadisticasDeMision.cs`, `MunicionPickup.cs`, `PlayerInputDriver.cs`, `SelectionController.cs`, `BattleManager.cs` |
| `EnvironmentHitEvent` | 1 | 2 | `Projectile.cs` | `AiBrain.ImpactosContraParedes.cs`, `AimUI.cs` |
| `GrenadeExplodedEvent` | 1 | 1 | `Granada.cs` | `TutorialManager.cs` |
| `GrenadeThrownEvent` | 1 | 2 | `Granada.cs` | `SoldierGrenadeAnim.cs`, `TutorialManager.cs` |
| `HeadshotEvent` | 2 | 2 | `Projectile.cs`, `ChecksBugs065.Wp1.cs` | `EstadisticasDeMision.cs`, `MarcaDeImpacto.cs` |
| `HealedEvent` | 3 | 1 | `Health.cs`, `EstadoDePartida.cs`, `RescateAutomatico.cs` | `RosterRowView.cs` |
| `LuminariaRotaEvent` | 1 | 0 | `Luminaria.cs` | — ⚠ nadie lo escucha |
| `MeleeAttackEvent` | 1 | 2 | `WeaponHolder.cs` | `SoldierAnimatorDriver.cs`, `TutorialManager.cs` |
| `MoveOrderIssuedEvent` | 1 | 2 | `OrderService.cs` | `OperacionDirector.Mando.cs`, `TutorialManager.cs` |
| `OrderAcknowledgedEvent` | 1 | 1 | `OrderService.cs` | `SelectionRingManager.cs` |
| `OrderCompletedEvent` | 1 | 0 | `AiBrain.cs` | — ⚠ nadie lo escucha |
| `PossessionChangedEvent` | 1 | 4 | `PossessionService.cs` | `SesionLog.cs`, `PossessedMarkerView.cs`, `RosterRowView.cs`, `SelectedSoldierUI.cs` |
| `ProjectileReturnedEvent` | 1 | 0 | `Projectile.cs` | — ⚠ nadie lo escucha |
| `SelectionChangedEvent` | 1 | 4 | `SelectionController.cs` | `SelectionRingManager.cs`, `RosterRowView.cs`, `SelectedSoldierUI.cs`, `SelectionCountView.cs` |
| `ShotFiredEvent` | 4 | 12 | `WeaponHolder.cs`, `HeadlessTestRunner.Fases8a12.cs`, `CinematicaDeVictoria.cs`, `OperacionDirector.TomaFinal.cs` | `AiBrain.cs`, `SesionLog.cs`, `ChecksBugs065.Wp4.cs`, `ChecksBugs065.Wp8.cs`, `HeadlessTestRunner.Fase21.cs`, `EstadisticasDeMision.cs` |
| `SwapTargetClearedEvent` | 1 | 0 | `AimTargeting.cs` | — ⚠ nadie lo escucha |
| `SwapTargetHighlightedEvent` | 1 | 0 | `AimTargeting.cs` | — ⚠ nadie lo escucha |
| `TurretControlChangedEvent` | 1 | 1 | `TurretAI.cs` | `PlayerInputDriver.cs` |
| `VehicleDamagedEvent` | 1 | 2 | `Vehicle.cs` | `VehicleFxReactor.cs`, `MarcaDeImpacto.cs` |
| `VehicleDestroyedEvent` | 1 | 4 | `Vehicle.cs` | `EstadisticasDeMision.cs`, `PlayerInputDriver.cs`, `SonidosDeOperacion.cs`, `VehicleFxReactor.cs` |
| `WeaponChangedEvent` | 1 | 1 | `WeaponHolder.cs` | `RosterRowView.cs` |
| `WeaponPickedUpEvent` | 1 | 0 | `WeaponPickup.cs` | — ⚠ nadie lo escucha |

## Eventos con un solo lado (6)

- `LuminariaRotaEvent`
- `OrderCompletedEvent`
- `ProjectileReturnedEvent`
- `SwapTargetClearedEvent`
- `SwapTargetHighlightedEvent`
- `WeaponPickedUpEvent`

Un evento que se publica y nadie escucha es trabajo tirado; uno que se escucha y nadie publica es una
feature que no llega a activarse. Los dos casos valen una revision.

## Reglas del bus

1. **Guardar el `IDisposable` en un campo y liberarlo en `OnDestroy`/`OnDisable`.** Un oyente que no se
   da de baja queda como referencia fake-null y `Publish` lo reporta como excepcion capturada.
2. **`Publish` es sincrono.** Lo que haga un oyente pasa dentro del `Publish`: cuidado con el orden.
3. **Una excepcion en un oyente no corta a los demas** (`EventBus.cs:54-67`), pero se loguea con nombre.
