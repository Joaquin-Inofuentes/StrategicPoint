# Ronda 16: bugs corregidos en Player / Camera / Combat / Actors / Interaction

Rutas relativas a `Assets/_Project/Scripts`. Lineas aproximadas (post-edicion). Sin compilar ni ejecutar Unity.

| n | archivo:linea | bug | correccion |
|---|---|---|---|
| 1 | Player/PlayerBrain.cs:67-71 | `Current?.Motor.Move(...)` / `Current?.Motor.RotateYaw(...)`: `?.` no respeta el "destruido" de Unity; con el soldado poseido destruido tiraba MissingReferenceException cada frame. | `if (Current != null && Current.Motor != null)` antes de llamar. |
| 2 | Combat/HitboxCabeza.cs:~58 | `MedirCabeza` indexaba `pesos[i]` hasta `vs.Length`; una malla con bindposes pero sin boneWeights (array vacio) revienta con IndexOutOfRange al crear la cabeza de un enemigo. | Se recorre `min(vs.Length, pesos.Length)`. |
| 3 | Actors/SoldierMotor.cs:~207 | `ResetMotionState` ponia `IsCrouching=false` sin volver a subir el `EyeAnchor` (que `SetCrouching` habia bajado): un revivido agachado quedaba con la camara baja. Tampoco limpiaba el salto buffereado (`saltoPedidoHasta`), que disparaba un salto al revivir. | Llama `SetCrouching(false)` y pone `saltoPedidoHasta = 0`. |
| 4 | Combat/WeaponHolder.cs:~156 | `ReadinessFraction01` dividia por `reloadDuration`; `ConfigurarCargador(..., 0)` lo deja en 0 y devuelve NaN a la UI mientras recarga. | Guarda `reloadDuration > 0f` (si no, 1). |
| 5 | Player/PlayerInputDriver.Contextual.cs:82,189 | `GetComponent<DemoledorAsalto>() ?? AddComponent<...>()`: `??` no ve el falso-null de Unity (en el Editor GetComponent devuelve un falso null), entonces `dem.Iniciar...` revienta en vez de agregar el componente. | `if (!TryGetComponent(out var dem)) dem = AddComponent<...>()`. |
| 6 | Player/PlayerInputDriver.Radial.cs:734,745 | Mismo patron `GetComponent ?? AddComponent` para "YO DEMUELO" y "ASALTO DEMUELE" del radial. | Igual que 5. |
| 7 | Player/SelectionController.cs:~236 | `Publish()` leia `s.Id` de cada seleccionado; un soldado destruido sin pasar por EntityDied (despawn, cambio de escena parcial) quedaba para siempre en la lista y rompia cada publicacion. | Antes de publicar se sacan los `null` de `selected`. |
| 8 | Player/SelectionController.cs:58,66 | `SelectSingle(null)` / `AddToSelection(null)` agregaban un null a la lista y `Publish` reventaba con NRE. | `if (s == null) return;`. |
| 9 | Actors/SoldierClasses.cs:~58 | `Mathf.Abs(h) % Length` con `h == int.MinValue` (Abs sigue negativo) da indice negativo: IndexOutOfRange al vestir a un enemigo cuyo hash de nombre cae ahi. | `(h == int.MinValue ? 0 : Mathf.Abs(h))`. El resto de los nombres conserva su clase. |
| 10 | Camera/CameraRig.cs:20 | `OnDisable` no reseteaba `IsTransitioning`/`transitionRoutine`: al desactivar el rig a mitad de una transicion Unity mata la corrutina, `IsTransitioning` queda en true y todos los `Follow*` (y `while (Rig.IsTransitioning)` de la camara de muerte) se frenan para siempre. | `OnDisable` pone `transitionRoutine = null; IsTransitioning = false`. |
| 11 | Player/PedidoDeCuracion.cs:~452 | Si el herido o el enfermero se destruyen a mitad de pedido, `Activo` pasa a false y `Tick` nunca llama `Cancelar()`: el otro queda `Motor.Retenido` (clavado), `Brain.Pasivo` o con `Reanimando/atendiendo` colgados. | En la rama `!Activo` de `Tick`, si queda estado a medias se llama `Cancelar()`. |
| 12 | Player/PedidoDeCuracion.cs:~166 | Estado estatico (cola, "no me cures", botiquin, retenidos, ordenManualHasta) sin reset: con Enter Play Mode sin recarga de dominio sobrevive a la corrida anterior. | `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` que lo limpia. |
| 13 | Player/KeyBindings.cs:~207 | `pressStart` (inicio de pulsaciones para WasTapped/IsHeld/HeldSeconds) estatico sin reset: una pulsacion vieja daba "mantenida" de entrada en la proxima corrida. | Reset en SubsystemRegistration. |
| 14 | Player/TrazadoDeCamino.cs:~40 | `puntos` y `marcadoresDePlan` estaticos sin reset: el primer `Ejecutar` de la siguiente corrida mandaba un recorrido fantasma y liberaba marcadores destruidos. | Reset en SubsystemRegistration. |
| 15 | Combat/Granada.cs:~31 | `Activas`, `Lanzadas`, `Explotadas` estaticos sin reset (la IA itera `Granada.Activas`). | Reset en SubsystemRegistration. |
| 16 | Player/RescateAutomatico.cs:~92 | `Caido/Rescatista/restante/canalizado` estaticos sin reset entre corridas. | Reset (`Cancelar()`) en SubsystemRegistration. |
| 17 | Combat/Projectile.cs:~845 | `ExplodeAt` recorria `ActorRegistry.All` con `foreach` mientras `TakeDamage` publica EntityDiedEvent; cualquier suscriptor que cree o de de baja un soldado modifica la lista en plena iteracion (InvalidOperationException). | `for` por indice releyendo `Count`. |
| 18 | Player/OrderService.cs:~606 | `IssueFollowOrderForSelection` no filtraba nulls: un soldado destruido en la seleccion reventaba en `IssueFollowOrder` (`soldier.GetComponent`). | `RemoveAll(s => s == null \|\| s == leader)`. |
| 19 | Player/PlayerInputDriver.Rts.cs:95 | Foco de camara sobre `Selection.Selected[0].transform` sin comprobar que el primero siga vivo como objeto Unity (MissingReferenceException). | Se agrega `Selected[0] != null` a la condicion. |

Total: 19 correcciones en 14 archivos. Sin cambios de balance ni de WeaponCatalog; `PlayerInputDriver.cs` y `PlayerInputDriver.Vehiculo.cs` no se tocaron.
