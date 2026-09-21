# Mapa del bus de eventos

> `EventBus.Instance.Publish<T>` / `Subscribe<T>`. Los emisores no conocen a los oyentes: este mapa es
> la unica forma de ver la conexion completa.

**22 tipos de evento.**

| Evento | Publica | Escucha | Quien lo publica | Quien lo escucha |
|---|---|---|---|---|
| `AiStateChangedEvent` | 1 | 3 | `AiBrain.cs` | `EnemyAlertIndicatorView.cs`, `SquadStateIndicatorView.cs`, `RosterRowView.cs` |
| `DamageTakenEvent` | 1 | 9 | `Health.cs` | `AiBrain.cs`, `PlayerInputDriver.cs`, `CubeFxReactor.cs`, `FloatingDamageTextManager.cs`, `KillFeedbackDirector.cs`, `AimUI.cs` |
| `EntityDiedEvent` | 1 | 9 | `Health.cs` | `MunicionPickup.cs`, `PlayerInputDriver.cs`, `SelectionController.cs`, `BattleManager.cs`, `CubeFxReactor.cs`, `KillFeedbackDirector.cs` |
| `EnvironmentHitEvent` | 1 | 1 | `Projectile.cs` | `AimUI.cs` |
| `GrenadeExplodedEvent` | 1 | 1 | `Granada.cs` | `TutorialManager.cs` |
| `GrenadeThrownEvent` | 1 | 1 | `Granada.cs` | `TutorialManager.cs` |
| `HealedEvent` | 3 | 1 | `Health.cs`, `EstadoDePartida.cs`, `RescateAutomatico.cs` | `RosterRowView.cs` |
| `MeleeAttackEvent` | 1 | 1 | `WeaponHolder.cs` | `TutorialManager.cs` |
| `MoveOrderIssuedEvent` | 1 | 1 | `OrderService.cs` | `TutorialManager.cs` |
| `OrderAcknowledgedEvent` | 1 | 1 | `OrderService.cs` | `SelectionRingManager.cs` |
| `OrderCompletedEvent` | 2 | 0 | `AiBrain.Navegacion.cs`, `AiBrain.cs` | — ⚠ nadie lo escucha |
| `PossessionChangedEvent` | 1 | 3 | `PossessionService.cs` | `PossessedMarkerView.cs`, `RosterRowView.cs`, `SelectedSoldierUI.cs` |
| `ProjectileReturnedEvent` | 1 | 0 | `Projectile.cs` | — ⚠ nadie lo escucha |
| `SelectionChangedEvent` | 1 | 4 | `SelectionController.cs` | `SelectionRingManager.cs`, `RosterRowView.cs`, `SelectedSoldierUI.cs`, `SelectionCountView.cs` |
| `ShotFiredEvent` | 2 | 5 | `WeaponHolder.cs`, `HeadlessTestRunner.Fases8a12.cs` | `AiBrain.cs`, `PlayerInputDriver.cs`, `CubeFxReactor.cs`, `SoldierAnimatorDriver.cs`, `TutorialManager.cs` |
| `SwapTargetClearedEvent` | 1 | 0 | `AimTargeting.cs` | — ⚠ nadie lo escucha |
| `SwapTargetHighlightedEvent` | 1 | 0 | `AimTargeting.cs` | — ⚠ nadie lo escucha |
| `TurretControlChangedEvent` | 1 | 1 | `TurretAI.cs` | `PlayerInputDriver.cs` |
| `VehicleDamagedEvent` | 1 | 1 | `Vehicle.cs` | `VehicleFxReactor.cs` |
| `VehicleDestroyedEvent` | 1 | 2 | `Vehicle.cs` | `PlayerInputDriver.cs`, `VehicleFxReactor.cs` |
| `WeaponChangedEvent` | 1 | 1 | `WeaponHolder.cs` | `RosterRowView.cs` |
| `WeaponPickedUpEvent` | 1 | 0 | `WeaponPickup.cs` | — ⚠ nadie lo escucha |

## Eventos con un solo lado (5)

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
