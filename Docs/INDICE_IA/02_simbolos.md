# Mapa de simbolos

> Clases, metodos publicos y propiedades publicas de cada archivo.
> Los metodos marcados `static` se pueden llamar sin instancia.

## `Assets/_Project/Scripts/Actors/Soldier.cs`

**Tipos:** `class Soldier`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ResetIdCounterForTests` | static | `void` | — | 26 |
| `SetRole` |  | `void` | `RoleType r` | 50 |
| `SetBodyVisible` |  | `void` | `bool visible` | 80 |
| `SetBodyVisible` |  | `void` | `bool visible, bool conservarArma` | 88 |
| `Configure` |  | `void` | `string name, TeamId t, RoleType r, int max` | 114 |
| `Bootstrap` |  | `void` | — | 127 |

**Propiedades:** `Id`, `DisplayName`, `Team`, `Role`, `ClassName`, `ClassNameTitulo`, `Health`, `Motor`, `Weapon`, `Brain`

## `Assets/_Project/Scripts/Actors/SoldierClasses.cs`

**Tipos:** `class SoldierClasses`, `struct Def`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Para` | static | `Def` | `Soldier s` | 45 |
| `NombreDe` | static | `string` | `Soldier s` | 63 |

## `Assets/_Project/Scripts/Actors/SoldierLook.cs`

**Tipos:** `class SoldierLook`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Aplicar` |  | `void` | — | 23 |

**Propiedades:** `Aplicado`, `Clase`

## `Assets/_Project/Scripts/Actors/SoldierMotor.cs`

**Tipos:** `class SoldierMotor`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `SetRunning` |  | `void` | `bool correr` | 70 |
| `Jump` |  | `void` | — | 77 |
| `TryVault` |  | `bool` | — | 100 |
| `ResetMotionState` |  | `void` | — | 178 |
| `DanioDeCaida` | static | `int` | `float metros` | 204 |
| `TickVertical` |  | `void` | `float dt` | 221 |
| `SetCrouching` |  | `void` | `bool agachado` | 320 |
| `Move` |  | `void` | `Vector3 worldDirection, float dt` | 337 |
| `RotateYaw` |  | `void` | `float yawDeltaDegrees` | 345 |
| `LookTowards` |  | `void` | `Vector3 worldPoint, float dt` | 350 |
| `MoveTowards` |  | `bool` | `Vector3 worldPoint, float arriveThreshold, float dt` | 360 |

**Propiedades:** `IsJumping`, `MoveSpeed`, `Corriendo`, `Vaulting`, `UltimoMotivoDeTrepa`, `UltimaCaidaMetros`, `UltimoDanioDeCaida`, `IsCrouching`, `EyeHeightDrop`, `EyeHeightDropSuave`

## `Assets/_Project/Scripts/Ai/AiBrain.Disparo.cs`

**Tipos:** `class AiBrain`

**Propiedades:** `Herido`

## `Assets/_Project/Scripts/Ai/AiBrain.Granadas.cs`

**Tipos:** `class AiBrain`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarContadoresGranadaIA` | static | `void` | — | 33 |
| `OrdenSuprimir` |  | `void` | `Vector3 punto, float segundos = SegundosDeSupresionOrdenada` | 53 |
| `OrdenLanzarGranada` |  | `bool` | `Vector3 punto` | 76 |
| `RecibirSupresion` |  | `void` | `float segundos` | 147 |

**Propiedades:** `Suprimido`, `HuyendoDeGranada`, `GranadasLanzadasPorIA`, `HuidasDeGranada`, `SuprimiendoPorOrden`

## `Assets/_Project/Scripts/Ai/AiBrain.Navegacion.cs`

**Tipos:** `class AiBrain`

**Propiedades:** `VaHaciaUnaCobertura`, `CoberturaElegida`

## `Assets/_Project/Scripts/Ai/AiBrain.Revivir.cs`

**Tipos:** `class AiBrain`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ResetearTrasRevivir` |  | `void` | `bool soltarPosesion` | 23 |

## `Assets/_Project/Scripts/Ai/AiBrain.Sentidos.cs`

**Tipos:** `class AiBrain`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TieneLineaDeTiro` |  | `bool` | `Soldier objetivo` | 36 |

## `Assets/_Project/Scripts/Ai/AiBrain.Tactica.cs`

**Tipos:** `class AiBrain`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `IssueCoverOrder` |  | `void` | `Vector3 punto, Collider dueno` | 39 |
| `PuntajeDeObjetivo` |  | `float` | `Soldier s` | 311 |

**Propiedades:** `MountTargetVehicle`, `EnCobertura`, `YendoACobertura`, `CoberturaPorOrden`, `CoberturaPunto`, `Quieto`, `Pasivo`, `SiguiendoAlJugador`

## `Assets/_Project/Scripts/Ai/AiBrain.cs`

**Tipos:** `enum CombatStance`, `class AiBrain`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `SetPatrolRoute` |  | `void` | `Vector3[] points` | 223 |
| `SetPatrolWaypoints` |  | `void` | `Transform[] waypoints` | 232 |
| `ReactivarNavegacion` |  | `void` | — | 306 |
| `ConfigurarAlcances` |  | `void` | `float vision, float ataque` | 362 |
| `Bootstrap` |  | `void` | — | 451 |
| `IssueMoveOrder` |  | `void` | `Vector3 point, bool queued = false` | 579 |
| `IssueMountOrder` |  | `void` | `Vehicle vehicle` | 627 |
| `IssueFollowOrder` |  | `void` | `Soldier leader, Vector3 formationOffsetLocal = default` | 648 |
| `IssueAttackOrder` |  | `void` | `Soldier enemy` | 665 |
| `CancelOrder` |  | `void` | — | 685 |
| `Tick` |  | `void` | `float dt` | 712 |

**Propiedades:** `QueuedOrderCount`, `QueuedDestinations`, `RemainingPathPoints`, `CurrentPath`, `State`, `IsPossessedByPlayer`, `CurrentTarget`, `Stance`, `HomePosition`, `EffectiveVisionRange`, `EffectiveAttackRange`, `SenseIntervalTicks`, `TicksSinceLastSense`, `LastSensedTarget`, `CurrentOrderDestination`, `PendingMoveDestination`, `FollowTarget`

## `Assets/_Project/Scripts/Ai/AjustesDeEscuadra.cs`

**Tipos:** `class AjustesDeEscuadra`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 21 |
| `RegistrarActivo` |  | `void` | — | 22 |
| `Aplicar` |  | `void` | — | 51 |
| `AsegurarEnEscena` | static | `AjustesDeEscuadra` | — | 63 |

**Propiedades:** `Activo`

## `Assets/_Project/Scripts/Ai/PathPreview.cs`

**Tipos:** `class PathPreview`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Attach` |  | `void` | `WaypointGraph waypointGraph` | 41 |
| `Show` |  | `bool` | `Vector3 from, Vector3 to` | 66 |
| `Hide` |  | `void` | — | 107 |

**Propiedades:** `Instance`, `IsShowing`, `PointCount`

## `Assets/_Project/Scripts/Ai/WorldSimulationDriver.cs`

**Tipos:** `class WorldSimulationDriver`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Step` | static | `void` | `float dt` | 63 |

**Propiedades:** `Instance`, `LastSoldadosSaltadosPorLod`, `LastRebuildMs`, `LastAiWeaponMs`, `LastVehicleMs`

## `Assets/_Project/Scripts/Camera/CameraFxSettings.cs`

**Tipos:** `class CameraFxSettings`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `InvalidateCache` | static | `void` | — | 43 |

**Propiedades:** `Enabled`

## `Assets/_Project/Scripts/Camera/CameraRig.cs`

**Tipos:** `enum ControlMode`, `class CameraRig`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `EnsureInstanceForTests` | static | `void` | `CameraRig rig` | 29 |
| `SetCamera` |  | `void` | `Camera c` | 74 |
| `MostrarRutas` |  | `void` | `bool visibles` | 96 |
| `SetZoomed` |  | `void` | `bool value` | 112 |
| `SetZoomFactor` |  | `void` | `float factor` | 141 |
| `AddPitch` |  | `void` | `float delta` | 223 |
| `ResetPitch` |  | `void` | — | 224 |
| `KickRecoil` |  | `void` | `float degrees` | 240 |
| `KickDirectional` |  | `void` | `Vector3 worldDirection, float magnitude` | 262 |
| `AddFrameOffset` |  | `void` | `Vector3 offset` | 276 |
| `SetWalking` |  | `void` | `bool value` | 298 |
| `SetMode` |  | `void` | `ControlMode mode, Vector3? rtsFallbackCenter = null` | 343 |
| `RestoreOrSetRtsView` |  | `void` | `Vector3 fallbackCenter` | 374 |
| `ToggleMode` |  | `void` | `Vector3? rtsFallbackCenter = null` | 396 |
| `BeginTransition` |  | `void` | `Transform target, float duration = 0.35f` | 417 |
| `FollowFps` |  | `void` | `Soldier soldier` | 455 |
| `BeginFollowBlend` |  | `void` | `float seconds` | 495 |
| `FollowOverShoulder` |  | `void` | `Transform target, float distance = 4f, float height = 1.53f, float hei` | 573 |
| `FollowAnchor` |  | `void` | `Transform anchor` | 608 |
| `FollowThirdPerson` |  | `void` | `Transform target, float distance = 7f, float height = 3f` | 632 |
| `FollowThirdPersonAimed` |  | `void` | `Vector3 pivotPos, Vector3 aimForward, float distance = 7f, float heigh` | 674 |
| `SetRtsView` |  | `void` | `Vector3 center` | 700 |
| `GetForwardRay` |  | `Ray` | — | 711 |
| `Pan` |  | `void` | `Vector3 worldDelta` | 749 |
| `RecenterOn` |  | `void` | `Vector3 point` | 761 |
| `Zoom` |  | `void` | `float delta` | 778 |
| `ZoomHaciaCursor` |  | `void` | `float delta, Vector2 pantalla` | 805 |

**Propiedades:** `Instance`, `Mode`, `RutasVisibles`, `AdsBlend`, `AdsBlendSuave`, `Respiracion`, `ZoomFactor`, `EstaConZoom`, `FovDeZoom`, `FovObjetivo`, `Pitch`, `RecoilPitch`, `ShakeOffset`, `MaxShakeMagnitude`, `BobOffset`, `IsTransitioning`, `Cam`, `ZoomAtLimit`, `RtsTargetHeight`, `RtsHeight`, `RtsFocus`

## `Assets/_Project/Scripts/Combat/Granada.cs`

**Tipos:** `class Granada`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `VelocidadHacia` | static | `Vector3` | `Vector3 origen, Vector3 objetivo, float rapidez = VelocidadDeLanzamien` | 47 |
| `Simular` | static | `int` | `Vector3 origen, Vector3 v, Transform ignorar, List<Vector3> puntos, ou` | 62 |
| `Lanzar` | static | `Granada` | `Vector3 origen, Vector3 v, Soldier dueno` | 107 |
| `ResetearContadores` | static | `void` | — | 125 |

**Propiedades:** `Lanzadas`, `Explotadas`

## `Assets/_Project/Scripts/Combat/Health.cs`

**Tipos:** `class Health`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Initialize` |  | `void` | `int actorId, int max` | 57 |
| `Tick` |  | `void` | `float dt` | 79 |
| `TakeDamage` |  | `void` | `int amount, int attackerId` | 112 |
| `TakeDamage` |  | `void` | `int amount, int attackerId, bool headshot` | 118 |
| `Heal` |  | `void` | `int amount` | 163 |

**Propiedades:** `MaxHealth`, `Current`, `IsAlive`, `ActorId`, `IsRegenerating`, `LastAttackerId`

## `Assets/_Project/Scripts/Combat/Projectile.cs`

**Tipos:** `class Projectile`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Configure` |  | `void` | `ProjectilePool owningPool, Vector3 position, Vector3 direction, int sh` | 111 |
| `OnSpawn` |  | `void` | — | 170 |
| `OnDespawn` |  | `void` | — | 178 |
| `Tick` |  | `void` | `float dt` | 194 |
| `ExplodeAt` | static | `void` | `Vector3 point, float radius, int damage, int ownerId, TeamId? spareTea` | 630 |

**Propiedades:** `Gravity`, `Velocity`, `PoolGeneration`

## `Assets/_Project/Scripts/Combat/ProjectilePool.cs`

**Tipos:** `class ProjectilePool`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 15 |
| `Registrar` |  | `void` | — | 16 |
| `Bootstrap` |  | `void` | — | 31 |
| `Configure` |  | `void` | `Projectile projectilePrefab, int prewarmCount` | 42 |
| `RecommendedPrewarm` | static | `int` | `int unitCount, float fireRatePerSecond, float projectileLifetime` | 84 |
| `Spawn` |  | `Projectile` | `Vector3 position, Vector3 direction, int shooterId, TeamId shooterTeam` | 101 |
| `Release` |  | `void` | `Projectile p` | 116 |

**Propiedades:** `Activo`, `ExhaustedCount`, `FreeCount`

## `Assets/_Project/Scripts/Combat/WeaponCatalog.cs`

**Tipos:** `class WeaponCatalog`, `struct Spec`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Get` | static | `Spec` | `WeaponKind kind` | 34 |

## `Assets/_Project/Scripts/Combat/WeaponHolder.cs`

**Tipos:** `struct WeaponChangedEvent`, `class WeaponHolder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `SetEnfoque` |  | `void` | `float progreso01` | 110 |
| `SetApuntado` |  | `void` | `float a01` | 126 |
| `ReponerMunicion` |  | `void` | — | 180 |
| `AgregarMunicion` |  | `void` | `int cargadores = 1` | 196 |
| `MunicionCompleta` |  | `bool` | — | 203 |
| `Bootstrap` |  | `void` | — | 216 |
| `SetPool` |  | `void` | `ProjectilePool projectilePool` | 246 |
| `SetTuning` |  | `void` | `int weaponDamage, float cooldown` | 248 |
| `EquipWeapon` |  | `void` | `WeaponKind kind, int weaponDamage, float cooldown, Color color` | 256 |
| `EquipFromLoadout` |  | `void` | `int index` | 435 |
| `ConfigurarCargador` |  | `void` | `int tamano, float segundosDeRecarga` | 446 |
| `CambiarArmaPrincipal` |  | `bool` | `int direccion` | 459 |
| `CycleNext` |  | `void` | — | 478 |
| `CyclePrevious` |  | `void` | — | 479 |
| `CambiarASiguienteConMunicion` |  | `bool` | — | 500 |
| `TryMelee` |  | `bool` | — | 530 |
| `HayObjetivoDeCuchilloCerca` |  | `bool` | — | 572 |
| `Tick` |  | `void` | `float dt` | 589 |
| `TryFire` |  | `bool` | `Vector3 origin, Vector3 direction` | 626 |
| `TryFire` |  | `bool` | `Vector3 origin, Vector3 direction, float dispersionMinima` | 628 |
| `ApplySpread` | static | `Vector3` | `Vector3 direction, float maxDeg` | 676 |
| `SonarDesenfunde` |  | `void` | — | 717 |
| `SonarGatilloVacio` |  | `void` | — | 724 |
| `ConsumirGranada` |  | `bool` | — | 733 |
| `ReponerGranadas` |  | `void` | `int cantidad = GranadasMaximas` | 739 |
| `Reload` |  | `bool` | — | 744 |

**Propiedades:** `Enfoque01`, `Apuntado01`, `SpreadDegEfectivo`, `SpreadFraction01`, `CooldownRemaining`, `CurrentWeaponKind`, `CurrentLoadoutIndex`, `ReadinessFraction01`, `CurrentAmmo`, `MagazineSize`, `IsReloading`, `ReloadRemaining`, `UsaReservas`, `ReservaActual`, `SinMunicionTotal`, `KnifeCooldownRemaining`, `Granadas`

## `Assets/_Project/Scripts/Combat/WeaponModels.cs`

**Tipos:** `class WeaponModels`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Get` | static | `GameObject` | `WeaponKind kind` | 15 |
| `NaturalLength` | static | `float` | `WeaponKind kind` | 50 |
| `Prisma` | static | `Vector3` | `WeaponKind kind` | 70 |
| `MeasuredNaturalSize` | static | `Vector3` | `WeaponKind kind` | 104 |
| `GetMetralletaVehiculo` | static | `GameObject` | — | 129 |

## `Assets/_Project/Scripts/Combat/WeaponPickup.cs`

**Tipos:** `struct WeaponPickedUpEvent`, `class WeaponPickup`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Configure` |  | `void` | `WeaponKind weaponKind, Color weaponColor` | 43 |
| `EquipOn` |  | `void` | `WeaponHolder holder, int soldierId` | 49 |

**Propiedades:** `Kind`, `Color`

## `Assets/_Project/Scripts/Core/ActorRegistry.cs`

**Tipos:** `class ActorRegistry`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Register` | static | `void` | `Soldier soldier` | 23 |
| `Unregister` | static | `void` | `Soldier soldier` | 29 |
| `Clear` | static | `void` | — | 35 |
| `Invalidate` | static | `void` | — | 48 |
| `EnsureAllRegistered` | static | `void` | — | 78 |
| `Rebarrer` | static | `void` | — | 88 |
| `CountAlive` | static | `int` | `TeamId team` | 103 |
| `CountDead` | static | `int` | `TeamId team` | 112 |
| `FindById` | static | `Soldier` | `int id` | 121 |
| `FindNearest` | static | `Soldier` | `Vector3 point, Func<Soldier, bool> predicate` | 128 |
| `FindNearestEnemyInRange` | static | `Soldier` | `Vector3 point, TeamId excludeTeam, float range` | 149 |

**Propiedades:** `All`

## `Assets/_Project/Scripts/Core/ApoyoEnElPiso.cs`

**Tipos:** `class ApoyoEnElPiso`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ApoyarATodos` | static | `int` | — | 53 |
| `Apoyar` | static | `bool` | `Transform cuerpo` | 68 |

## `Assets/_Project/Scripts/Core/CamaraPrincipal.cs`

**Tipos:** `class CamaraPrincipal`

**Propiedades:** `Actual`

## `Assets/_Project/Scripts/Core/Coberturas.cs`

**Tipos:** `class Coberturas`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Solidos` | static | `List<Collider>` | — | 43 |
| `Registrar` | static | `int` | — | 67 |
| `Vigente` | static | `bool` | `Collider dueno` | 114 |
| `TryPuntoApuntado` | static | `bool` | `Vector3 puntoApuntado, Transform obstaculo, float radioPiso, out Vecto` | 124 |
| `IndicesCercanos` | static | `List<int>` | `Vector3 centro, int cantidad, float radio` | 148 |
| `FrenteDe` | static | `Vector3` | `int indice` | 171 |
| `FrenteDe` | static | `Vector3` | `Vector3 punto, Collider dueno` | 181 |
| `TryCercano` | static | `bool` | `Vector3 p, float radio, out Vector3 punto, out Collider dueno` | 191 |
| `Limpiar` | static | `void` | — | 194 |
| `TryMejorCobertura` | static | `bool` | `Vector3 desde, Soldier objetivo, Soldier quien, float radioMaximo, flo` | 207 |
| `TryMejorCobertura` | static | `bool` | `Vector3 desde, Soldier objetivo, Soldier quien, float radioMaximo, flo` | 218 |
| `TryCoberturaDeTiro` | static | `bool` | `Vector3 desde, Soldier objetivo, Soldier quien, float radio, float min` | 261 |
| `HayLineaDeTiroDesde` | static | `bool` | `Vector3 punto, Soldier objetivo, Soldier quien` | 286 |
| `MostrarMarcas` | static | `void` | `bool visibles` | 313 |

**Propiedades:** `Puntos`, `Duenos`, `Cantidad`, `Version`, `MarcasVisibles`

## `Assets/_Project/Scripts/Core/ComandosDeDepuracion.cs`

**Tipos:** `class ComandosDeDepuracion`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Matar` | static | `string` | `int id = -1` | 14 |
| `Revivir` | static | `string` | — | 27 |

## `Assets/_Project/Scripts/Core/Deslizador.cs`

**Tipos:** `class Deslizador`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Resolver` | static | `Vector3` | `Transform cuerpo, Collider propio, Vector3 delta, float radio` | 37 |
| `RadioDe` | static | `float` | `Collider col, Transform cuerpo, float porDefecto = 0.4f, bool usarMayo` | 160 |

## `Assets/_Project/Scripts/Core/Dificultad.cs`

**Tipos:** `enum NivelDificultad`, `class Dificultad`, `struct Perfil`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Datos` | static | `Perfil` | `NivelDificultad n` | 56 |
| `Resumen` | static | `string` | `NivelDificultad n` | 78 |
| `AplicarVida` | static | `int` | — | 88 |
| `AjustarVida` | static | `bool` | `Soldier s` | 95 |
| `MultiplicadorDeDano` | static | `float` | `int atacanteId, Health victima` | 107 |

**Propiedades:** `Activa`, `FuegoAmigoExplosivo`, `Actual`, `PerfilActual`

## `Assets/_Project/Scripts/Core/EventBus.cs`

**Tipos:** `class EventBus`, `class ActionDisposable`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ClearAll` |  | `void` | — | 80 |
| `Dispose` |  | `void` | — | 86 |

## `Assets/_Project/Scripts/Core/FlowField.cs`

**Tipos:** `class FlowField`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Attach` |  | `void` | `WaypointGraph waypointGraph` | 64 |
| `Compute` |  | `bool` | `Vector3 destination` | 74 |
| `DirectionAt` |  | `Vector3` | `Vector3 worldPos` | 138 |
| `CostAt` |  | `float` | `Vector3 worldPos` | 151 |
| `IsReachable` |  | `bool` | `Vector3 worldPos` | 159 |

**Propiedades:** `IsComputed`, `ReachableCount`, `Destination`

## `Assets/_Project/Scripts/Core/GameLog.cs`

**Tipos:** `class GameLog`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Line` | static | `void` | `string message` | 17 |
| `Clear` | static | `void` | — | 24 |

## `Assets/_Project/Scripts/Core/Loc.cs`

**Tipos:** `enum Idioma`, `class Loc`, `class LocTraductor`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Poner` | static | `void` | `Idioma i` | 90 |
| `Alternar` | static | `void` | — | 98 |
| `T` | static | `string` | `string textoEs` | 130 |
| `TieneEntrada` | static | `bool` | `string textoEs` | 131 |
| `Ingles` | static | `string` | `string textoEs` | 132 |
| `Recorrer` | static | `void` | — | 162 |

**Propiedades:** `Actual`, `NombreDelIdioma`, `UltimasTraducciones`

## `Assets/_Project/Scripts/Core/MetricasDeBuild.cs`

**Tipos:** `class MetricasDeBuild`, `class Corredor`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Correr` |  | `IEnumerator` | — | 41 |

## `Assets/_Project/Scripts/Core/ModoDios.cs`

**Tipos:** `class ModoDios`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Poner` | static | `void` | `bool activo` | 21 |
| `Alternar` | static | `bool` | — | 29 |
| `Protege` | static | `bool` | `Health h` | 32 |

**Propiedades:** `Activo`

## `Assets/_Project/Scripts/Core/NavService.cs`

**Tipos:** `class NavService`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TryArea` | static | `bool` | `out Bounds limites` | 72 |
| `Invalidate` | static | `void` | — | 89 |
| `Reset` | static | `void` | — | 116 |
| `EnsureBuilt` | static | `void` | — | 130 |
| `HayLineaDeTiro` | static | `bool` | `Vector3 desde, Vector3 hasta, Transform ignorarA, Transform ignorarB` | 162 |
| `BlocksMovement` | static | `bool` | `Collider c` | 182 |
| `TryFindDetour` | static | `bool` | `Vector3 from, Vector3 to, List<Vector3> result` | 199 |

**Propiedades:** `Graph`, `IsReady`, `Version`

## `Assets/_Project/Scripts/Core/ObjectPool.cs`

**Tipos:** `interface IPoolable`, `class ObjectPool`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Get` |  | `T` | — | 34 |
| `Release` |  | `void` | `T instance` | 60 |
| `Clear` |  | `void` | — | 80 |

**Propiedades:** `PrestadasCount`, `FreeCount`

## `Assets/_Project/Scripts/Core/RaicesDeEscena.cs`

**Tipos:** `class RaicesDeEscena`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Buscar` | static | `GameObject` | `string nombre` | 13 |

## `Assets/_Project/Scripts/Core/RecursosCache.cs`

**Tipos:** `class RecursosCache`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Precargar` | static | `void` | — | 26 |
| `Vaciar` | static | `void` | — | 38 |

**Propiedades:** `Cargas`, `Aciertos`

## `Assets/_Project/Scripts/Core/ReinicioDeEstaticos.cs`

**Tipos:** `class ReinicioDeEstaticos`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Restablecer` | static | `void` | — | 13 |

## `Assets/_Project/Scripts/Core/SceneLoader.cs`

**Tipos:** `class SceneLoader`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Cargar` | static | `void` | `string escenaDestino` | 17 |

**Propiedades:** `Destino`

## `Assets/_Project/Scripts/Core/SpatialGrid.cs`

**Tipos:** `class SpatialGrid`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Rebuild` | static | `void` | — | 66 |
| `QueryInRange` | static | `void` | `Vector3 point, float range, List<Soldier> resultado, Func<Soldier, boo` | 128 |
| `FindNearestInRange` | static | `Soldier` | `Vector3 point, float range, Func<Soldier, bool> predicate` | 148 |

**Propiedades:** `CellCount`

## `Assets/_Project/Scripts/Core/TestLog.cs`

**Tipos:** `class TestLog`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Begin` | static | `void` | — | 11 |
| `Phase` | static | `void` | `string title` | 15 |
| `Step` | static | `void` | `string message` | 18 |
| `Warn` | static | `void` | `string message` | 21 |

## `Assets/_Project/Scripts/Core/WaypointGraph.cs`

**Tipos:** `class WaypointGraph`, `class NodeMinHeap`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `NeighborDirection` | static | `Vector3` | `int dir` | 162 |
| `Build` |  | `void` | `Vector3 min, Vector3 max, float spacing, System.Func<Vector3, bool> is` | 182 |
| `NodeIndex` |  | `int` | `int x, int z` | 274 |
| `NodeToXZ` |  | `void` | `int node, out int x, out int z` | 280 |
| `NodeAt` |  | `int` | `Vector3 worldPos` | 289 |
| `NodeToWorld` |  | `Vector3` | `int node` | 297 |
| `IsBlockedNode` |  | `bool` | `int node` | 304 |
| `IsBlockedAt` |  | `bool` | `Vector3 worldPos` | 310 |
| `TryGetNeighbor` |  | `bool` | `int node, int dir, out int neighbor, out float stepCost` | 317 |
| `FindFreeNodeNear` |  | `int` | `Vector3 worldPos` | 356 |
| `TryFindPath` |  | `bool` | `Vector3 from, Vector3 to, List<Vector3> result` | 402 |
| `HasLineOfSight` |  | `bool` | `Vector3 a, Vector3 b` | 555 |
| `PathLength` | static | `float` | `IReadOnlyList<Vector3> path` | 587 |
| `PathIsClear` |  | `bool` | `IReadOnlyList<Vector3> path` | 604 |
| `Clear` |  | `void` | — | 631 |
| `Push` |  | `void` | `int node, float key` | 650 |
| `Pop` |  | `int` | — | 672 |

**Propiedades:** `IsBuilt`, `Columns`, `Rows`, `NodeCount`, `BlockedCount`, `EdgeCount`, `Spacing`, `Origin`, `LastExpandedNodes`, `LastRawPathPoints`, `MaxSnapRings`, `SmoothPaths`, `Count`

## `Assets/_Project/Scripts/Core/WorldSystemsRegistry.cs`

**Tipos:** `class WorldSystemsRegistry`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Register` | static | `void` | `VehicleBrain v` | 33 |
| `Unregister` | static | `void` | `VehicleBrain v` | 34 |
| `Register` | static | `void` | `TurretWeapon t` | 36 |
| `Unregister` | static | `void` | `TurretWeapon t` | 37 |
| `Register` | static | `void` | `TurretAI a` | 39 |
| `Unregister` | static | `void` | `TurretAI a` | 40 |
| `Register` | static | `void` | `Vehicle v` | 42 |
| `Unregister` | static | `void` | `Vehicle v` | 43 |
| `Register` | static | `void` | `SP.Presentation.ObstacleMarker o` | 45 |
| `Unregister` | static | `void` | `SP.Presentation.ObstacleMarker o` | 46 |
| `EnsurePopulated` | static | `void` | — | 56 |
| `Clear` | static | `void` | — | 81 |

**Propiedades:** `VehicleBrains`, `TurretWeapons`, `TurretAis`, `Vehicles`, `Obstacles`

## `Assets/_Project/Scripts/Demo/AutoDemoRunner.cs`

**Tipos:** `class AutoDemoRunner`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `StartDemo` |  | `void` | — | 67 |
| `StopDemo` |  | `void` | — | 73 |

**Propiedades:** `IsRunning`

## `Assets/_Project/Scripts/Editor/AntesProbe.cs`

**Tipos:** `class AntesProbe`, `struct Paso`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Iniciar` | static | `string` | `string nombre` | 26 |
| `Marca` | static | `void` | `string que, string detalle = ""` | 72 |
| `Foto` | static | `string` | `string nombre` | 74 |
| `Guion` | static | `string` | `int id, string nombre, string texto, float duracion` | 99 |
| `Cerrar` | static | `void` | — | 166 |

**Propiedades:** `Carpeta`

## `Assets/_Project/Scripts/Editor/ArtBuilder.cs`

**Tipos:** `class ArtBuilder`, `enum Volumen`, `struct PropDef`, `struct Plantado`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ArmarTodo` | static | `void` | — | 33 |
| `CrearAnimator` | static | `AnimatorController` | — | 70 |
| `AjustarTiemposDeSalto` | static | `void` | `AnimatorState arriba, AnimatorState aire, AnimatorState abajo, Animato` | 199 |
| `AjustarSaltoEnControllerExistente` | static | `string` | — | 210 |
| `CrearPrefabsDeProps` | static | `void` | — | 384 |
| `MontarSoldados` | static | `void` | `AnimatorController ctrl` | 459 |
| `PoblarEscena` | static | `void` | — | 753 |

## `Assets/_Project/Scripts/Editor/ArtSetup.cs`

**Tipos:** `class ArtSetup`, `struct Prop`, `class RecalibrarAlturaAlReimportar`, `class RecalibrarAlturaAlCargarDominio`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ConfigurarTodo` | static | `void` | — | 125 |
| `ConfigurarClipsDeSalto` | static | `void` | — | 463 |
| `HornearClipsDeSalto` | static | `void` | — | 729 |
| `Informe` | static | `void` | — | 894 |
| `InformeTexto` | static | `string` | — | 896 |

## `Assets/_Project/Scripts/Editor/ArtsUsoReport.cs`

**Tipos:** `class ArtsUsoReport`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Generar` | static | `string` | — | 16 |

## `Assets/_Project/Scripts/Editor/AudicionDeSonidos.cs`

**Tipos:** `class AudicionDeSonidos`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Generar` | static | `void` | — | 22 |

## `Assets/_Project/Scripts/Editor/AudioAnalisisReport.cs`

**Tipos:** `class AudioAnalisisReport`, `struct Fila`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Generar` | static | `void` | — | 31 |
| `Analizar` | static | `List<Fila>` | — | 47 |

## `Assets/_Project/Scripts/Editor/BalanceBench.cs`

**Tipos:** `class BalanceBench`, `struct Resultado`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Correr` | static | `void` | — | 38 |
| `Iniciar` | static | `void` | — | 45 |

**Propiedades:** `Corriendo`, `Informe`, `Progreso`

## `Assets/_Project/Scripts/Editor/BlockoutArtDresser.cs`

**Tipos:** `class BlockoutArtDresser`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `VestirEscenaAbierta` | static | `void` | — | 44 |

## `Assets/_Project/Scripts/Editor/CliBuilder.cs`

**Tipos:** `class CliBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `BuildWindows64` | static | `void` | — | 13 |

## `Assets/Editor/HeadlessBuilder.cs`

**Tipos:** `class HeadlessBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ResolveBuildPath` | static | `string` | — | 16 |
| `SetBuildPath` | static | `void` | `string path` | 23 |
| `BuildWebGL` | static | `void` | — | 30 |
| `BuildWebGLDesdeEditor` | static | `string` | — | 50 |

## `Assets/_Project/Scripts/Editor/HeadlessTestRunner.cs`

**Tipos:** `class HeadlessTestRunner`, `struct BenchResult`, `struct StressResult`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `RunAll` | static | `void` | — | 182 |
| `RunManyHeadless` | static | `void` | — | 278 |
| `RunMany` | static | `void` | `int iterations` | 280 |
| `RunPerformanceBenchmarks` | static | `void` | — | 348 |
| `RunEquivalenceCheck` | static | `void` | — | 491 |
| `RunStressScenarios` | static | `void` | — | 617 |
| `BuildDemoScene` | static | `void` | — | 704 |

**Propiedades:** `LastBenchmarkCsv`, `LastEquivalenceMismatches`, `LastProjectileMs`, `FailedCheckCount`, `FailedCheckMessages`

## `Assets/_Project/Scripts/Editor/LevelBlockoutBuilder.cs`

**Tipos:** `class LevelBlockoutBuilder`, `struct Bloque`, `struct Ronda`, `struct DatosTanque`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Construir` | static | `void` | — | 207 |

## `Assets/_Project/Scripts/Editor/LoadingSceneBuilder.cs`

**Tipos:** `class LoadingSceneBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `BuildLoadingScene` | static | `void` | — | 25 |
| `RegisterInBuildSettings` | static | `void` | — | 104 |

## `Assets/_Project/Scripts/Editor/MenuSceneBuilder.cs`

**Tipos:** `class MenuSceneBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `BuildMenuScene` | static | `void` | — | 20 |
| `RegisterScenesInBuildSettings` | static | `void` | — | 183 |

## `Assets/_Project/Scripts/Editor/MisionBuilder.cs`

**Tipos:** `class MisionBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Construir` | static | `void` | — | 17 |

## `Assets/_Project/Scripts/Editor/MonedaMunicionBuilder.cs`

**Tipos:** `class MonedaMunicionBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Construir` | static | `void` | — | 18 |

## `Assets/Editor/NavMeshSetupPipeline.cs`

**Tipos:** `class NavMeshSetupPipeline`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `RunFullSetup` | static | `void` | — | 13 |
| `SetupSoldierPrefabs` | static | `void` | — | 28 |
| `BakeScene` | static | `void` | `string scenePath` | 70 |

## `Assets/_Project/Scripts/Editor/NightLightingBuilder.cs`

**Tipos:** `class NightLightingBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `PonerDeNoche` | static | `void` | — | 24 |

## `Assets/_Project/Scripts/Editor/PlaymodeCleanup.cs`

**Tipos:** `class PlaymodeCleanup`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `BarrerHuerfanos` | static | `void` | — | 75 |

## `Assets/_Project/Scripts/Editor/PrimerFramePreview.cs`

**Tipos:** `class PrimerFramePreview`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Aplicar` | static | `void` | — | 29 |

## `Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs`

**Tipos:** `class ReadmeEditor`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `OnInspectorGUI` |  | `void` | — | 123 |

## `Assets/_Project/Scripts/Editor/RespaldoDeEscenas.cs`

**Tipos:** `class RespaldoDeEscenas`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Respaldar` | static | `string` | `string rutaAsset` | 26 |

**Propiedades:** `Carpeta`

## `Assets/_Project/Scripts/Editor/RosterUiPipeline.cs`

**Tipos:** `class RosterUiPipeline`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Migrar` | static | `void` | — | 30 |

## `Assets/_Project/Scripts/Editor/SkinTransfer.cs`

**Tipos:** `class SkinTransfer`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Generar` | static | `Mesh` | — | 40 |
| `Cargar` | static | `Mesh` | — | 244 |

## `Assets/_Project/Scripts/Editor/SoldierPrefabPipeline.cs`

**Tipos:** `class SoldierPrefabPipeline`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `CrearPrefabs` | static | `void` | — | 25 |

## `Assets/_Project/Scripts/Editor/SoldierVariantBuilder.cs`

**Tipos:** `class SoldierVariantBuilder`, `struct Pieza`, `class Variante`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `GenerarTodas` | static | `void` | — | 55 |

## `Assets/Editor/TerrainSetupHelper.cs`

**Tipos:** `class TerrainSetupHelper`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `FixAndSetupTerrain` | static | `void` | — | 8 |
| `CaptureSnapshot` | static | `void` | — | 203 |
| `PrintMinimapHierarchy` | static | `void` | — | 232 |
| `ResizeMinimap` | static | `void` | — | 256 |
| `EnsureGameplaySceneAndCapture` | static | `void` | — | 276 |
| `DiagnoseTestFailure` | static | `void` | — | 293 |

## `Assets/_Project/Scripts/Editor/TutorialAutoPlayerMenu.cs`

**Tipos:** `class TutorialAutoPlayerMenu`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReproducirTutorialAutomatico` | static | `void` | — | 18 |
| `RestaurarEntradaHumana` | static | `void` | — | 38 |

## `Assets/_Project/Scripts/Editor/TutorialSceneBuilder.cs`

**Tipos:** `class TutorialSceneBuilder`, `struct Cubo`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Construir` | static | `void` | — | 39 |
| `AsegurarCatalogo` | static | `void` | — | 430 |

## `Assets/_Project/Scripts/Editor/WeaponPrefabBuilder.cs`

**Tipos:** `class WeaponPrefabBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `BuildAll` | static | `void` | — | 33 |

## `Assets/_Project/Scripts/Editor/WorldArtPipeline.cs`

**Tipos:** `class WorldArtPipeline`, `enum Volumen`, `struct ReglaCategoria`, `struct Plantado`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ImportarTodo` | static | `void` | — | 64 |
| `ArmarPrefabs` | static | `void` | — | 231 |
| `ReemplazarEnEscena` | static | `void` | — | 315 |
| `AplicarVisualesVehiculo` | static | `void` | `GameObject veh` | 553 |

## `Assets/_Project/Scripts/Mision/CinematicaDeVictoria.cs`

**Tipos:** `class CinematicaDeVictoria`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Iniciar` |  | `void` | `MisionDirector m, PlayerInputDriver driver, GameOutcomeController outc` | 36 |

**Propiedades:** `EnCurso`, `Terminada`, `SoldadosDeLaHorda`, `TracerasDisparadas`, `Tiempo`, `Plano`

## `Assets/_Project/Scripts/Mision/EstadoDePartida.cs`

**Tipos:** `class EstadoDePartida`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Reiniciar` | static | `void` | — | 32 |
| `Tick` | static | `void` | `float dt` | 39 |

**Propiedades:** `SinVivos`, `CalmaAcumulada`, `Revivio`, `Perdio`

## `Assets/_Project/Scripts/Mision/Helicoptero.cs`

**Tipos:** `class Helicoptero`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Alerta` |  | `void` | `bool on` | 51 |

**Propiedades:** `Instancia`, `Vueltas`, `EnAlerta`, `DisparaCobertura`, `DisparosHechos`, `Volando`, `ClipDeRotor`

## `Assets/_Project/Scripts/Mision/MisionDirector.cs`

**Tipos:** `enum FaseDeMision`, `class MisionDirector`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `PosicionDelJugador` |  | `Vector3` | — | 114 |
| `PuntoObjetivoActual` |  | `Vector3` | — | 134 |
| `DistanciaAlObjetivo` |  | `float` | — | 145 |
| `CrearEnemigo` |  | `Soldier` | `string nombre, Vector3 pos, float yaw = 180f` | 211 |
| `Perder` |  | `void` | `string motivo` | 551 |
| `SaltarAFase` |  | `void` | `FaseDeMision f` | 562 |

**Propiedades:** `Instancia`, `Activo`, `Fase`, `Restante`, `Civil`, `CivilRescatado`, `Heli`, `OleadasLanzadas`, `SoldadosGenerados`, `TensionSonando`

## `Assets/_Project/Scripts/Mision/MisionHud.cs`

**Tipos:** `class MisionHud`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Crear` | static | `MisionHud` | `MisionDirector d` | 20 |
| `Refrescar` |  | `void` | — | 95 |

**Propiedades:** `TextoTitulo`, `TextoDetalle`, `TextoDificultad`

## `Assets/_Project/Scripts/Player/AccionesEnCurso.cs`

**Tipos:** `class AccionesEnCurso`, `struct Accion`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Reportar` | static | `void` | `Soldier actor, string verboEs, Vector3 puntoObjetivo, float progreso01` | 35 |
| `Terminar` | static | `void` | `Soldier actor` | 50 |
| `Limpiar` | static | `void` | — | 55 |
| `De` | static | `bool` | `Soldier actor, out Accion accion` | 61 |
| `Vigentes` | static | `int` | `List<Accion> destino` | 71 |

**Propiedades:** `Punto`, `Cantidad`

## `Assets/_Project/Scripts/Player/AimTargeting.cs`

**Tipos:** `enum AimTargetType`, `struct AimResult`, `class AimTargeting`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Evaluate` |  | `AimResult` | `Ray ray, Soldier excludeSelf` | 38 |
| `PisoBajoRayo` | static | `bool` | `Ray ray, out Vector3 punto` | 147 |

**Propiedades:** `MaxDistance`

## `Assets/_Project/Scripts/Player/CajaDeSuministros.cs`

**Tipos:** `class CajaDeSuministros`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `MasCercana` | static | `CajaDeSuministros` | `Vector3 pos, float radio` | 27 |
| `Crear` | static | `CajaDeSuministros` | `Vector3 pos` | 43 |

**Propiedades:** `Recogidas`, `Disponible`

## `Assets/_Project/Scripts/Player/Demolicion.cs`

**Tipos:** `class Demolicion`, `class DemoledorAsalto`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `MarcadorApuntado` | static | `ObstacleMarker` | `Transform golpeado, Vector3 punto` | 23 |
| `EsDemolible` | static | `bool` | `ObstacleMarker m, out string motivo` | 33 |
| `DistanciaA` | static | `float` | `Soldier s, ObstacleMarker m` | 44 |
| `Ejecutar` | static | `void` | `ObstacleMarker m, Soldier quien` | 57 |
| `AsegurarEnAsaltos` | static | `int` | — | 104 |
| `CancelarTodos` | static | `bool` | — | 118 |
| `IniciarComoJugador` |  | `bool` | `ObstacleMarker m, out string motivo` | 129 |
| `IniciarComoAliado` |  | `bool` | `ObstacleMarker m, out string motivo` | 138 |
| `Cancelar` |  | `void` | `string aviso` | 157 |

**Propiedades:** `Progreso`, `Objetivo`, `EstaCargando`, `Todos`, `HayEnCurso`

## `Assets/_Project/Scripts/Player/KeyBindings.cs`

**Tipos:** `class KeyBindings`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Get` | static | `Key` | `string actionId` | 120 |
| `Set` | static | `string` | `string actionId, Key key` | 126 |
| `ResetToDefaults` | static | `void` | — | 150 |
| `InvalidateCache` | static | `void` | — | 162 |
| `WasPressed` | static | `bool` | `string actionId` | 170 |
| `IsPressed` | static | `bool` | `string actionId` | 179 |
| `WasTapped` | static | `bool` | `string actionId, float holdSeconds = 0.3f` | 195 |
| `IsHeld` | static | `bool` | `string actionId, float holdSeconds = 0.3f` | 213 |
| `HeldSeconds` | static | `float` | `string actionId` | 233 |
| `ForzarInicioDePulsacion` | static | `void` | `string actionId, float segundos` | 248 |
| `HayPulsacionRegistrada` | static | `bool` | `string actionId, float holdSeconds` | 253 |
| `DisplayName` | static | `string` | `string actionId` | 259 |

**Propiedades:** `AllActions`

## `Assets/_Project/Scripts/Player/MandoFps.cs`

**Tipos:** `class MandoFps`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Zona` | static | `Vector2` | `Vector2 v` | 20 |

**Propiedades:** `Pad`, `Conectado`, `Mover`, `Mirar`, `Disparar`, `Saltar`, `Agachar`, `Correr`, `Recargar`, `ArmaSiguiente`, `ArmaAnterior`, `Pausa`

## `Assets/_Project/Scripts/Player/MunicionPickup.cs`

**Tipos:** `class MunicionPickup`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Crear` | static | `MunicionPickup` | `Vector3 pos` | 32 |

## `Assets/_Project/Scripts/Player/OrdenesDeEscuadra.cs`

**Tipos:** `class OrdenesDeEscuadra`, `struct PuntoDeCobertura`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `BuscarSegunMirada` | static | `List<PuntoDeCobertura>` | `Vector3 origen, Vector3 dir, int cantidad, float distanciaMin = 2.5f, ` | 19 |
| `CubrirSegunMirada` | static | `int` | `IList<Soldier> soldados, Vector3 origen, Vector3 dir, out Vector3 prim` | 75 |
| `Quietos` | static | `int` | `IEnumerable<Soldier> soldados` | 95 |

## `Assets/_Project/Scripts/Player/OrderHistory.cs`

**Tipos:** `class OrderHistory`, `struct Entry`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Record` | static | `void` | `string description, int actorCount` | 40 |
| `TryGet` | static | `bool` | `int index, out Entry entry` | 51 |
| `Clear` | static | `void` | — | 60 |
| `RecentText` | static | `string` | `int maxLines = 5` | 67 |
| `Snapshot` | static | `IReadOnlyList<Entry>` | — | 84 |

**Propiedades:** `Count`

## `Assets/_Project/Scripts/Player/OrderService.cs`

**Tipos:** `enum FormationKind`, `class OrderService`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `LoManejaElJugador` | static | `bool` | `Soldier s` | 42 |
| `FindNearestFreeAlly` | static | `Soldier` | `Vector3 point, TeamId team, Soldier exclude` | 64 |
| `PuntoAlcanzable` | static | `bool` | `Vector3 desde, Vector3 punto, out Vector3 ajustado` | 112 |
| `IssueMoveOrder` | static | `void` | `Soldier soldier, Vector3 point, bool queued = false` | 122 |
| `IssueCoverOrder` | static | `bool` | `Soldier soldier, Vector3 point, Collider owner` | 150 |
| `FormationPoints` | static | `Vector3[]` | `Vector3 center, int count` | 176 |
| `FormationPoints` | static | `Vector3[]` | `Vector3 center, Vector3 forward, int count, FormationKind kind` | 185 |
| `FormationPoints` | static | `Vector3[]` | `Vector3 center, Vector3 forward, int count, FormationKind kind, float ` | 194 |
| `SpreadOf` | static | `float` | `IReadOnlyList<Vector3> positions` | 287 |
| `IsValidDestination` | static | `bool` | `Vector3 point` | 335 |
| `IssueAttackOrder` | static | `void` | `Soldier soldier, Soldier enemy` | 361 |
| `IssueAttackOrderForSelection` | static | `void` | `IEnumerable<Soldier> selection, Soldier enemy` | 382 |
| `IssueMoveOrderForSelection` | static | `void` | `IEnumerable<Soldier> selection, Vector3 point, bool queued = false` | 393 |
| `IssueFormationOrderForSelection` | static | `void` | `IEnumerable<Soldier> selection, Vector3 center, Vector3 forward, Forma` | 406 |
| `PlayRejectSound` | static | `void` | — | 483 |
| `IssueFollowOrder` | static | `void` | `Soldier soldier, Soldier leader, Vector3 formationOffsetLocal = defaul` | 490 |
| `IssueFollowOrderForSelection` | static | `void` | `IEnumerable<Soldier> selection, Soldier leader` | 522 |
| `IssueMountOrder` | static | `void` | `Soldier soldier, Vehicle vehicle` | 539 |
| `IssueMountOrderForSelection` | static | `void` | `IEnumerable<Soldier> selection, Vehicle vehicle` | 547 |
| `RegroupSelection` | static | `Vector3[]` | `IEnumerable<Soldier> selection, FormationKind kind = FormationKind.Cua` | 565 |
| `IssueRetreatOrderForSelection` | static | `void` | `IEnumerable<Soldier> selection, float distance = RetreatDistance, floa` | 644 |

## `Assets/_Project/Scripts/Player/PedidoDeCuracion.cs`

**Tipos:** `class PedidoDeCuracion`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `MedicoDisponible` | static | `Soldier` | `Soldier excluir = null` | 44 |
| `SolicitarReanimar` | static | `bool` | `Soldier caido, Soldier medicoManual = null` | 57 |
| `Solicitar` | static | `bool` | `Soldier herido, Soldier medicoManual = null` | 101 |
| `Botiquin` | static | `bool` | `Soldier medico` | 129 |
| `BuscarEnfermero` | static | `Soldier` | `Soldier herido` | 165 |
| `Cancelar` | static | `void` | — | 177 |
| `HayCalma` | static | `bool` | `Vector3 punto` | 211 |
| `Tick` | static | `void` | `float dt` | 271 |

**Propiedades:** `Herido`, `Enfermero`, `Activo`, `Reanimando`, `ProgresoDeReanimar`, `BotiquinDe`, `BotiquinListoEn`, `BotiquinActivo`, `ReanimacionEsAutomatica`

## `Assets/_Project/Scripts/Player/PlayerBrain.cs`

**Tipos:** `class PlayerBrain`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 16 |
| `Registrar` |  | `void` | — | 17 |
| `Possess` |  | `bool` | `Soldier soldier` | 39 |
| `Move` |  | `void` | `Vector3 worldDirection, float dt` | 67 |
| `RotateYaw` |  | `void` | `float yawDeltaDegrees` | 69 |
| `Fire` |  | `bool` | `Vector3? aimPoint = null` | 89 |

**Propiedades:** `Activo`, `Current`

## `Assets/_Project/Scripts/Player/PlayerInputDriver.Escuadra.cs`

**Tipos:** `class PlayerInputDriver`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `MoverEnEscuadra` |  | `bool` | `Soldier s, int direccion` | 44 |
| `SeleccionarSoldadoDeEscuadra` |  | `bool` | `int indice, bool sumar` | 79 |

## `Assets/_Project/Scripts/Player/PlayerInputDriver.Interaccion.cs`

**Tipos:** `class PlayerInputDriver`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ObjetivoInteractuable` |  | `IInteractable` | — | 34 |
| `PromptDeInteraccion` |  | `string` | — | 61 |
| `TryInteractuarConMira` |  | `bool` | — | 114 |

**Propiedades:** `UltimaAccionRapida`

## `Assets/_Project/Scripts/Player/PlayerInputDriver.Radial.cs`

**Tipos:** `class PlayerInputDriver`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ResolverGestoDeQ` |  | `void` | `bool toque, bool sostenido, bool sigueApretada` | 48 |
| `DestinatariosDeOrden` |  | `List<Soldier>` | — | 137 |
| `EjecutarOrdenDelMenu` |  | `bool` | `int opcion` | 155 |
| `EjecutarOrdenRadial` |  | `bool` | `int categoria, int sub = 0` | 220 |

**Propiedades:** `RadialAbierto`

## `Assets/_Project/Scripts/Player/PlayerInputDriver.Registro.cs`

**Tipos:** `class PlayerInputDriver`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 10 |
| `Registrar` |  | `void` | — | 11 |

**Propiedades:** `Activo`

## `Assets/_Project/Scripts/Player/PlayerInputDriver.Vehiculo.cs`

**Tipos:** `class PlayerInputDriver`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `EnterVehicle` |  | `void` | `Vehicle vehicle` | 29 |
| `ExitVehicle` |  | `void` | — | 136 |
| `SwitchSeat` |  | `void` | `VehicleSeatRole newRole` | 419 |

**Propiedades:** `CurrentSeat`

## `Assets/_Project/Scripts/Player/PlayerInputDriver.cs`

**Tipos:** `class PlayerInputDriver`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `MostrarProgresoDemolicion` |  | `void` | `float f01` | 308 |
| `OcultarProgresoDemolicion` |  | `void` | — | 309 |
| `SetDestination` |  | `void` | `Vector3 punto` | 445 |
| `CancelDestination` |  | `void` | — | 467 |
| `ToggleVehicleCameraView` |  | `void` | — | 469 |
| `ShowTutorialMessage` |  | `void` | `string text, float holdSeconds = 1.4f` | 478 |
| `ReclamarControl` |  | `void` | `Soldier s` | 808 |
| `TryRevivir` |  | `bool` | `Soldier caido, bool sostenidoLoSuficiente` | 1555 |
| `TryResolverCobertura` |  | `bool` | `AimResult r, out Vector3 punto, out Collider dueno` | 1834 |
| `IssueCoverOrderT` |  | `bool` | `Vector3 punto, Collider dueno` | 1871 |
| `IssueCoverOrderForSelection` |  | `int` | `IReadOnlyList<Soldier> seleccion, Vector3 puntoApuntado, Transform obs` | 1895 |
| `SubirATodos` |  | `int` | `Vehicle v` | 1924 |
| `BajarATodos` |  | `int` | `Vehicle v` | 1945 |
| `IssueGroundOrderT` |  | `void` | `Vector3 punto, bool shiftHeld` | 1990 |
| `GOrderOnVehicle` |  | `void` | `Vehicle vehicle` | 2100 |
| `SoldadoUnicoSeleccionado` |  | `Soldier` | — | 2218 |
| `TryPossess` |  | `bool` | `Soldier target` | 2232 |
| `EquipSlot` |  | `void` | `int slot` | 2380 |
| `EquipWeaponHotkey` |  | `void` | `WeaponKind kind` | 2404 |
| `TryIssueVehicleMoveOrder` |  | `bool` | `Vector3 point, Vehicle vehicle = null` | 2430 |
| `UsarTorreta` |  | `bool` | `TorretaFija t` | 2541 |
| `SalirDeTorreta` |  | `void` | — | 2568 |
| `ConstruirContextoRadial` |  | `ContextoRadial` | `AimResult aim` | 2622 |

**Propiedades:** `LookSensitivity`, `TurretSensitivity`, `InvertLookY`, `UltimaMira`, `DemolicionEnCurso`, `GranadaApuntando`, `GranadasLanzadasPorElJugador`, `Mira`, `ApuntadoVisual`, `TieneDestino`, `DestinoActual`, `IsHandlingDeath`, `EnTorretaFija`, `TorretaActual`

## `Assets/_Project/Scripts/Player/PossessionService.cs`

**Tipos:** `class PossessionService`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Swap` | static | `bool` | `PlayerBrain brain, Soldier target` | 15 |

## `Assets/_Project/Scripts/Player/Reanimacion.cs`

**Tipos:** `class Reanimacion`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Ejecutar` | static | `bool` | `Soldier caido, float fraccionDeVida = 1f` | 18 |

## `Assets/_Project/Scripts/Player/RescateAutomatico.cs`

**Tipos:** `class RescateAutomatico`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Solicitar` | static | `bool` | `Soldier caido` | 50 |
| `Cancelar` | static | `void` | — | 79 |
| `Tick` | static | `void` | `float dt` | 87 |

**Propiedades:** `Caido`, `Rescatista`, `Activo`

## `Assets/_Project/Scripts/Player/SelectionController.cs`

**Tipos:** `class SelectionController`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `SelectSingle` |  | `void` | `Soldier s` | 50 |
| `AddToSelection` |  | `void` | `Soldier s` | 58 |
| `SelectVehicle` |  | `void` | `Vehicle v` | 65 |
| `SelectAll` |  | `void` | `IEnumerable<Soldier> squad` | 76 |
| `SelectWoundedOnly` |  | `bool` | `float threshold01 = 0.5f` | 92 |
| `IsWounded` | static | `bool` | `int current, int max, float threshold01` | 132 |
| `SelectSameTypeOnScreen` |  | `void` | `Soldier reference, Camera cam` | 150 |
| `Clear` |  | `void` | — | 217 |

**Propiedades:** `Selected`, `SelectedVehicle`

## `Assets/_Project/Scripts/Player/TrazadoDeCamino.cs`

**Tipos:** `class TrazadoDeCamino`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Marcar` | static | `bool` | `Vector3 punto` | 42 |
| `Limpiar` | static | `void` | — | 58 |
| `Ejecutar` | static | `int` | `IReadOnlyList<Soldier> seleccion` | 75 |

**Propiedades:** `Puntos`, `Cantidad`, `HayTrazado`

## `Assets/_Project/Scripts/Presentation/AccionesEnCursoView.cs`

**Tipos:** `class AccionesEnCursoView`, `class BarraMundo`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `AccionesEnCursoView` | — | 86 |
| `Refrescar` |  | `void` | — | 218 |
| `Linea` | static | `string` | `in AccionesEnCurso.Accion a, bool propia` | 276 |

**Propiedades:** `Instancia`, `BarrasVisibles`, `TextoDeEstado`, `TextoDeEstadoCompleto`, `HudVisible`, `RaizDelHud`, `PosicionesDeBarras`, `RellenoDeLaPrimeraBarra`, `TiempoDeLaPrimeraBarra`

## `Assets/_Project/Scripts/Presentation/AnuncioDeZonas.cs`

**Tipos:** `class AnuncioDeZonas`, `struct Zona`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 15 |
| `RegistrarActivo` |  | `void` | — | 16 |
| `Asegurar` | static | `AnuncioDeZonas` | — | 51 |

**Propiedades:** `Activo`, `ZonaActual`, `NombreActual`

## `Assets/_Project/Scripts/Presentation/ArmaEnLaMano.cs`

**Tipos:** `class ArmaEnLaMano`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Colgar` |  | `bool` | — | 93 |
| `Reposicionar` |  | `void` | `WeaponKind kind` | 150 |

## `Assets/_Project/Scripts/Presentation/AttackLineManager.cs`

**Tipos:** `class AttackLineManager`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Prewarm` | static | `void` | — | 116 |

## `Assets/_Project/Scripts/Presentation/AudioDirector.cs`

**Tipos:** `enum SfxChannel`, `struct VoiceState`, `class AudioDirector`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `GainFor` | static | `float` | `SfxChannel c` | 87 |
| `SetGain` | static | `void` | `SfxChannel c, float v` | 95 |
| `InvalidateGainCache` | static | `void` | — | 107 |
| `Attenuation` | static | `float` | `float distance` | 127 |
| `CutoffFor` | static | `float` | `float distance` | 142 |
| `NextPitch` | static | `float` | — | 162 |
| `SelectVictim` | static | `int` | `VoiceState[] voices, float newAudibility, float now` | 188 |
| `SonoRecien` | static | `bool` | `string nombreDeClip, int ultimos = 12` | 243 |
| `EnsureVoices` |  | `void` | — | 270 |
| `Play` |  | `void` | `SfxKind kind, Vector3 position, float volume, float priority` | 378 |
| `Play` |  | `void` | `SfxKind kind, Vector3 position, float volume` | 383 |
| `PlayClip` |  | `bool` | `AudioClip clip, Vector3 position, float volume, float priority` | 390 |
| `PlayClip` |  | `bool` | `AudioClip clip, Vector3 position, float volume, float priority, SfxCha` | 396 |
| `PlayFlat` |  | `bool` | `AudioClip clip, SfxChannel channel, float volume, float priority` | 455 |
| `PlayUi` |  | `bool` | `SfxKind kind, float volume, float priority` | 494 |
| `PlayVoice` |  | `bool` | `SfxKind kind, float volume, float priority` | 500 |
| `PlayAt` | static | `bool` | `SfxKind kind, Vector3 position, float volume, float priority = 1f` | 507 |
| `PlayClipAt` | static | `bool` | `AudioClip clip, Vector3 position, float volume, float priority = 1f` | 510 |
| `PlayUi2D` | static | `bool` | `SfxKind kind, float volume, float priority = 1f` | 513 |
| `PlayVoice2D` | static | `bool` | `SfxKind kind, float volume, float priority = 1f` | 516 |
| `DistanceOrUnknown` | static | `float` | `Transform listener, Vector3 position` | 519 |
| `Ocluido` |  | `bool` | `Vector3 position` | 527 |
| `ResetStats` |  | `void` | — | 633 |

**Propiedades:** `Instance`, `DroppedCount`, `TotalReproducidos`, `UltimoSonidoTapado`, `VoiceCount`, `FreeVoiceCount`, `ActiveVoiceCount`, `Active3DVoiceCount`, `Active2DVoiceCount`

## `Assets/_Project/Scripts/Presentation/AudioDucking.cs`

**Tipos:** `class AudioDucking`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Duck` | static | `void` | `float intensity01` | 33 |

**Propiedades:** `UserVolumeCeiling`

## `Assets/_Project/Scripts/Presentation/BarraDeVidaVehiculo.cs`

**Tipos:** `class BarraDeVidaVehiculo`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `BarraDeVidaVehiculo` | `Vehicle v` | 35 |
| `Refrescar` |  | `void` | — | 105 |

**Propiedades:** `Relleno01`, `Visible`, `ColorDelRelleno`

## `Assets/_Project/Scripts/Presentation/CoverHologram.cs`

**Tipos:** `class CoverHologram`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Mostrar` | static | `void` | `Soldier quien, Vector3 p, Vector3 frente, Transform obstaculo` | 96 |
| `ResaltarSolo` | static | `void` | `Transform obstaculo` | 124 |
| `Ocultar` | static | `void` | — | 126 |

**Propiedades:** `Instance`, `Visible`, `PuntoActual`, `ModeloActual`

## `Assets/_Project/Scripts/Presentation/CubeFxReactor.cs`

**Tipos:** `class CubeFxReactor`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bootstrap` |  | `void` | — | 52 |
| `WriteTint` | static | `void` | `Renderer r, Color c` | 228 |
| `ReadTint` | static | `Color` | `Renderer r` | 240 |

## `Assets/_Project/Scripts/Presentation/CuchilloFx.cs`

**Tipos:** `class CuchilloFx`, `class TajoVisual`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ResetearContadores` | static | `void` | — | 15 |
| `Tajo` | static | `void` | `Soldier dueno, Soldier objetivo` | 17 |
| `Iniciar` |  | `void` | `Transform dueno` | 50 |

**Propiedades:** `Tajos`, `Aciertos`

## `Assets/_Project/Scripts/Presentation/CursorContextual.cs`

**Tipos:** `enum CursorTipo`, `class CursorContextual`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Aplicar` | static | `void` | `CursorTipo tipo` | 16 |
| `Restaurar` | static | `void` | — | 28 |

**Propiedades:** `Actual`

## `Assets/_Project/Scripts/Presentation/DebrisPool.cs`

**Tipos:** `class DebrisPool`, `class Debris`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ResetIfStale` | static | `void` | — | 28 |
| `ClearAll` | static | `void` | — | 46 |
| `Prewarm` | static | `void` | — | 98 |
| `Spawn` | static | `Debris` | `Vector3 position, Vector3 velocity, Color color, float size, float lif` | 135 |
| `Release` | static | `void` | `Debris d` | 170 |
| `Launch` |  | `void` | `Vector3 position, Vector3 initialVelocity, Color color, float size, fl` | 193 |
| `Recycle` |  | `void` | — | 224 |

**Propiedades:** `ActiveCount`, `TotalCount`

## `Assets/_Project/Scripts/Presentation/DecalPool.cs`

**Tipos:** `enum DecalKind`, `class DecalPool`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Budget` | static | `int` | `DecalKind kind` | 22 |
| `CountOf` | static | `int` | `DecalKind kind` | 23 |
| `ResetIfStale` | static | `void` | — | 25 |
| `ClearAll` | static | `void` | — | 46 |
| `Spawn` | static | `GameObject` | `DecalKind kind, Vector3 position, Vector3 normal, float size` | 77 |

## `Assets/_Project/Scripts/Presentation/EnemyAlertIndicatorView.cs`

**Tipos:** `class EnemyAlertIndicatorView`

**Propiedades:** `IsBlinkingLowHealth`

## `Assets/_Project/Scripts/Presentation/EntityStateDebugView.cs`

**Tipos:** `class EntityStateDebugView`

**Propiedades:** `Visible`

## `Assets/_Project/Scripts/Presentation/Feedback.cs`

**Tipos:** `class Feedback`, `class WorldTag`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Reset` | static | `void` | — | 28 |
| `Visual` | static | `void` | `string texto, Vector3? enMundo = null, Color? color = null, bool aviso` | 32 |
| `Accion` | static | `void` | `SfxKind sonido, string texto, Vector3? enMundo = null, Color? color = ` | 50 |
| `ClearAll` | static | `void` | — | 98 |
| `Spawn` | static | `WorldTag` | `Vector3 pos, string text, Color color` | 112 |

**Propiedades:** `Contador`, `UltimoTexto`, `UltimoSonido`, `ActiveCount`

## `Assets/_Project/Scripts/Presentation/Fragmentador.cs`

**Tipos:** `class Fragmento`, `class Fragmentador`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Liberar` |  | `void` | — | 34 |
| `LimpiarTodo` | static | `void` | — | 58 |
| `Romper` | static | `int` | `Transform objeto, Vector3 origen, float fuerza, int piezas = 0` | 206 |

**Propiedades:** `Lanzados`, `Activos`, `Piezas`

## `Assets/_Project/Scripts/Presentation/FuentesBelicas.cs`

**Tipos:** `class FuentesBelicas`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `EsBelica` | static | `bool` | `Font f` | 45 |
| `Aplicar` | static | `int` | — | 48 |
| `Aplicar` | static | `bool` | `Text t, Font titulo, Font texto` | 61 |
| `Aplicar` | static | `bool` | `TextMesh m, Font texto` | 73 |

**Propiedades:** `Cambiados`, `Titulo`, `Texto`

## `Assets/_Project/Scripts/Presentation/GameOutcomeController.cs`

**Tipos:** `class GameOutcomeController`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 19 |
| `Registrar` |  | `void` | — | 20 |
| `Bind` |  | `void` | `GameObject victory, GameObject defeat` | 49 |
| `ShowVictory` |  | `void` | — | 148 |
| `ShowDefeat` |  | `void` | — | 161 |
| `OnRetryClicked` |  | `void` | — | 180 |
| `OnExitClicked` |  | `void` | — | 189 |

**Propiedades:** `Activo`, `IsShowing`

## `Assets/_Project/Scripts/Presentation/GameplaySceneBootstrap.cs`

**Tipos:** `class GameplaySceneBootstrap`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 18 |
| `RegistrarActivo` |  | `void` | — | 19 |

**Propiedades:** `Activo`

## `Assets/_Project/Scripts/Presentation/GenericSfx.cs`

**Tipos:** `enum SfxKind`, `class GenericSfx`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Get` | static | `AudioClip` | `SfxKind kind` | 54 |
| `GetWeaponShot` | static | `AudioClip` | `WeaponKind kind` | 68 |
| `GetWeaponReload` | static | `AudioClip` | `WeaponKind kind` | 107 |
| `GetWeaponDraw` | static | `AudioClip` | `WeaponKind kind` | 118 |
| `GetWeaponDry` | static | `AudioClip` | `WeaponKind kind` | 129 |
| `PlayOneShot2D` | static | `void` | `AudioClip clip, float volume, float pitch, string name = "OneShotTone"` | 152 |

## `Assets/_Project/Scripts/Presentation/HealthBarView.cs`

**Tipos:** `class HealthBarView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `SetLodAllowed` |  | `void` | `bool value` | 73 |
| `Bootstrap` |  | `void` | — | 120 |
| `Tick` |  | `bool` | — | 158 |
| `ApplyBillboard` |  | `void` | `Quaternion cameraRotation` | 199 |

**Propiedades:** `LodAllowed`

## `Assets/_Project/Scripts/Presentation/ImpactCubes.cs`

**Tipos:** `enum ImpactSurface`, `class ImpactCubes`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ColorOf` | static | `Color` | `ImpactSurface s` | 16 |
| `CountFor` | static | `int` | `ImpactSurface s, int damage` | 28 |
| `Spawn` | static | `int` | `Vector3 point, Vector3 normal, ImpactSurface surface, int damage` | 34 |

## `Assets/_Project/Scripts/Presentation/ImpactFx.cs`

**Tipos:** `class ImpactFx`, `enum FxMode`, `class ImpactFxPool`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ClearAll` | static | `void` | — | 153 |
| `RecycleAll` | static | `void` | — | 175 |
| `Spawn` | static | `void` | `Vector3 position, Color color, float peakScale = 0.55f, float duration` | 183 |
| `Prewarm` | static | `void` | — | 195 |
| `SpawnScaledByDamage` | static | `void` | `Vector3 position, Color color, int damage` | 208 |
| `SpawnArmorSparks` | static | `void` | `Vector3 position, Vector3 surfaceNormal` | 217 |
| `SpawnExplosion` | static | `void` | `Vector3 position, float radius` | 231 |
| `SpawnShockwaveRing` | static | `void` | `Vector3 center, float radius` | 258 |
| `Recycle` |  | `void` | — | 470 |
| `Contains` |  | `bool` | `ImpactFx fx` | 507 |
| `ResetIfStale` |  | `void` | — | 512 |
| `Take` |  | `ImpactFx` | — | 543 |
| `Release` |  | `void` | `ImpactFx fx` | 576 |
| `LimpiarTodo` |  | `void` | — | 586 |
| `RecycleAll` |  | `void` | — | 606 |

**Propiedades:** `Budget`, `ActiveCount`, `Budget`, `ActiveCount`, `TotalCount`

## `Assets/_Project/Scripts/Presentation/KillCylinderFx.cs`

**Tipos:** `class KillCylinderFx`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ResetIfStale` | static | `void` | — | 33 |
| `Spawn` | static | `void` | `Vector3 position` | 145 |
| `Prewarm` | static | `void` | — | 152 |
| `LimpiarTodo` | static | `void` | — | 171 |
| `Recycle` |  | `void` | — | 239 |

**Propiedades:** `Budget`, `ActiveCount`, `TotalCount`

## `Assets/_Project/Scripts/Presentation/KillFeedbackDirector.cs`

**Tipos:** `class KillFeedbackDirector`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `FeedText` |  | `string` | — | 187 |

**Propiedades:** `Instance`, `GroupedKills`, `Streak`, `LastKillWasPlayer`, `LastKiller`, `SlowMotionActive`

## `Assets/_Project/Scripts/Presentation/LightProp.cs`

**Tipos:** `class LightProp`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Knock` |  | `void` | `Vector3 fromDirection` | 26 |

**Propiedades:** `IsKnocked`, `KnockRadius`

## `Assets/_Project/Scripts/Presentation/LimpiezaDeEscena.cs`

**Tipos:** `class LimpiezaDeEscena`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Limpiar` | static | `int` | — | 50 |
| `ContarHuerfanos` | static | `int` | — | 104 |

## `Assets/_Project/Scripts/Presentation/MainMenuController.cs`

**Tipos:** `class MainMenuController`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `OnPlayClicked` |  | `void` | — | 48 |
| `MostrarDificultad` |  | `void` | — | 62 |
| `IniciarPartida` |  | `void` | `NivelDificultad nivel` | 126 |
| `OnTutorialClicked` |  | `void` | — | 135 |
| `OnExitClicked` |  | `void` | — | 146 |
| `OnConfirmExitNo` |  | `void` | — | 154 |
| `OnConfirmExitYes` |  | `void` | — | 160 |

## `Assets/_Project/Scripts/Presentation/MenuAmbiente.cs`

**Tipos:** `class MenuAmbiente`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Crear` | static | `MenuAmbiente` | `Scene escena` | 36 |

**Propiedades:** `Existe`

## `Assets/_Project/Scripts/Presentation/MinimapIcon.cs`

**Tipos:** `class MinimapIcon`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `EnableFogOfWar` |  | `void` | — | 47 |
| `TickFollow` |  | `bool` | — | 93 |
| `ApplyFog` |  | `void` | `bool spotted` | 112 |
| `EnableDirectionMarker` |  | `void` | `int layer, float iconRadius` | 172 |
| `ConvertirEnTriangulo` |  | `bool` | — | 177 |
| `ConvertirEnCuadrado` |  | `bool` | — | 230 |
| `RegistrarObstaculos` | static | `int` | `Color color, float radius = 1.4f` | 253 |
| `Spawn` | static | `MinimapIcon` | `Transform target, Color color, int layer, float radius = 1.6f` | 290 |

**Propiedades:** `FogEnabled`, `IsRendered`, `TargetPosition`, `EsCuadrado`

## `Assets/_Project/Scripts/Presentation/MiraOptica.cs`

**Tipos:** `class MiraOptica`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `MiraOptica` | `Transform camaraPrincipal, Transform visorDelArma` | 40 |
| `Configurar` |  | `void` | `WeaponKind arma` | 106 |
| `Mostrar` |  | `void` | `bool apuntando` | 140 |
| `Seguir` |  | `void` | `Camera principal, float fovObjetivo` | 156 |

**Propiedades:** `Optica`, `Textura`, `Tubo`, `Forma`, `Amplia`

## `Assets/_Project/Scripts/Presentation/MusicDirector.cs`

**Tipos:** `class MusicDirector`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Tick` | static | `void` | `float dt` | 79 |

**Propiedades:** `GananciaLucha`, `Atenuacion`

## `Assets/_Project/Scripts/Presentation/MuzzleLightPool.cs`

**Tipos:** `class MuzzleLightPool`, `class MuzzleFlashLight`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Flash` | static | `void` | `Vector3 position, Color color, float intensity = 9f, float range = 14f` | 43 |
| `Flash` |  | `void` | `Vector3 position, Color color, float intensity, float range, float sec` | 94 |
| `ForceOff` |  | `void` | — | 113 |

**Propiedades:** `TotalCount`, `ActiveCount`, `IsOn`, `OffAt`

## `Assets/_Project/Scripts/Presentation/ObjectiveArrowIndicator.cs`

**Tipos:** `class ObjectiveArrowIndicator`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Crear` | static | `ObjectiveArrowIndicator` | — | 15 |
| `Actualizar` |  | `void` | `Vector3 origenJugador, Vector3 objetivo` | 37 |
| `Ocultar` |  | `void` | — | 68 |

## `Assets/_Project/Scripts/Presentation/ObstacleMarker.cs`

**Tipos:** `class ObstacleMarker`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Tick` | static | `void` | `float dt` | 49 |
| `TakeDamage` |  | `void` | `int amount, Vector3? desde = null` | 119 |
| `Demoler` |  | `void` | `Vector3? desde = null, float fuerza = 9f` | 143 |

**Propiedades:** `EsExplosivo`, `EstaEncendido`, `MaxHealth`, `CurrentHealth`, `IsCollapsed`, `Stage`

## `Assets/_Project/Scripts/Presentation/OrderLineManager.cs`

**Tipos:** `class OrderLineManager`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Prewarm` | static | `void` | — | 79 |

## `Assets/_Project/Scripts/Presentation/OrderMarkerFx.cs`

**Tipos:** `class OrderMarkerFx`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ResetIfStale` | static | `void` | — | 63 |
| `LimpiarTodo` | static | `void` | — | 192 |
| `RecycleAll` | static | `void` | — | 212 |
| `Spawn` | static | `void` | `Vector3 position, Color color, int orderIndex, float duration = 0.6f` | 274 |
| `Spawn` | static | `void` | `Vector3 position, Color color, float duration = 0.6f` | 283 |
| `ClearQueuedMarkers` | static | `void` | — | 294 |
| `SpawnPlan` | static | `OrderMarkerFx` | `Vector3 position, Color color, int orderIndex` | 326 |
| `ReleasePlan` | static | `void` | `OrderMarkerFx m` | 336 |
| `HayOrdenPendienteEn` | static | `bool` | `Vector3 punto` | 356 |
| `PurgarCompletados` | static | `int` | — | 382 |
| `Prewarm` | static | `void` | — | 407 |
| `Recycle` |  | `void` | — | 556 |

**Propiedades:** `Budget`, `ActiveCount`, `TotalCount`

## `Assets/_Project/Scripts/Presentation/PatrolRouteLine.cs`

**Tipos:** `class PatrolRouteLine`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Spawn` | static | `PatrolRouteLine` | `Vector3[] points, Color color, float height = 0.05f` | 31 |

**Propiedades:** `Markers`

## `Assets/_Project/Scripts/Presentation/PauseController.cs`

**Tipos:** `class PauseController`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `GameObject pause, GameObject settings` | 49 |
| `ShowPause` |  | `void` | — | 302 |
| `OnContinueClicked` |  | `void` | — | 326 |
| `OnSettingsClicked` |  | `void` | — | 345 |
| `OnSettingsBackClicked` |  | `void` | — | 356 |
| `ToggleControlsOverlay` |  | `void` | — | 381 |
| `OnControlsClicked` |  | `void` | — | 391 |
| `OnControlsBackClicked` |  | `void` | — | 400 |
| `OnRebindClicked` |  | `void` | — | 409 |
| `OnRebindBackClicked` |  | `void` | — | 418 |
| `OnRebindResetClicked` |  | `void` | — | 426 |
| `OnMenuClicked` |  | `void` | — | 439 |
| `OnConfirmExitNo` |  | `void` | — | 445 |
| `OnConfirmExitYes` |  | `void` | — | 452 |

**Propiedades:** `IsPaused`, `IsControlsOverlayOpen`

## `Assets/_Project/Scripts/Presentation/PossessedMarkerView.cs`

**Tipos:** `class PossessedMarkerView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `SetLodAllowed` |  | `void` | `bool value` | 31 |
| `TryGetLodProbe` |  | `bool` | `out Vector3 position` | 51 |
| `Tick` |  | `bool` | — | 90 |
| `SetInitial` |  | `void` | `Soldier soldier` | 121 |

**Propiedades:** `LodAllowed`

## `Assets/_Project/Scripts/Presentation/PostFxDirector.cs`

**Tipos:** `class PostFxDirector`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `PulseDamageAberration` |  | `void` | `float amount01` | 144 |
| `SetSpeedBlur` |  | `void` | `float amount01` | 151 |
| `EnableOnCamera` | static | `void` | `Camera cam` | 206 |

**Propiedades:** `Instance`, `AberrationIntensity`, `BlurIntensity`, `VolumeWeight`

## `Assets/_Project/Scripts/Presentation/RagdollDeExplosion.cs`

**Tipos:** `class RagdollDeExplosion`, `struct Hueso`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Lanzar` | static | `bool` | `Soldier s, Vector3 origen, float fuerza = FuerzaBase` | 47 |
| `Desarmar` |  | `void` | — | 124 |

**Propiedades:** `Activo`, `CantidadDeCuerpos`, `TotalLanzados`

## `Assets/_Project/Scripts/Presentation/RevivePromptView.cs`

**Tipos:** `class RevivePromptView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bootstrap` |  | `void` | — | 38 |
| `Tick` |  | `bool` | — | 49 |
| `ApplyBillboard` |  | `void` | `Quaternion cameraRotation` | 59 |
| `Construir` | static | `RevivePromptView` | `Transform unidad` | 68 |
| `RegistrarTodas` | static | `int` | — | 126 |

**Propiedades:** `IsVisible`

## `Assets/_Project/Scripts/Presentation/SafeMaterial.cs`

**Tipos:** `class SafeMaterial`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Create` | static | `Material` | `Color color` | 77 |
| `CreateShared` | static | `Material` | — | 108 |

## `Assets/_Project/Scripts/Presentation/SelectionRingFx.cs`

**Tipos:** `class SelectionRingFx`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TrackHealth` |  | `void` | `Soldier soldier` | 25 |
| `SetColor` |  | `void` | `Color c` | 48 |
| `ResetSpawnCount` | static | `void` | — | 64 |
| `Spawn` | static | `SelectionRingFx` | `Transform target, Color color, float radius = 0.75f` | 66 |
| `FlashAcknowledge` |  | `void` | — | 100 |

**Propiedades:** `SpawnCount`

## `Assets/_Project/Scripts/Presentation/SfxSintetico.cs`

**Tipos:** `class SfxSintetico`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Explosion` | static | `AudioClip` | — | 129 |
| `LanzamientoDeCohete` | static | `AudioClip` | — | 147 |
| `GranadaSeguro` | static | `AudioClip` | — | 162 |
| `GranadaLanzada` | static | `AudioClip` | — | 171 |
| `GranadaRebote` | static | `AudioClip` | — | 179 |
| `CuchilloTajo` | static | `AudioClip` | — | 187 |
| `CuchilloImpacto` | static | `AudioClip` | — | 196 |
| `Salto` | static | `AudioClip` | — | 208 |
| `Aterrizaje` | static | `AudioClip` | — | 216 |
| `RadialAbrir` | static | `AudioClip` | — | 228 |
| `RadialTick` | static | `AudioClip` | — | 236 |
| `RadialConfirmar` | static | `AudioClip` | — | 243 |
| `RadialCancelar` | static | `AudioClip` | — | 251 |
| `CuracionInicio` | static | `AudioClip` | — | 259 |
| `CuracionFin` | static | `AudioClip` | — | 267 |
| `Reanimar` | static | `AudioClip` | — | 276 |
| `CargaPlantada` | static | `AudioClip` | — | 288 |
| `CargaTic` | static | `AudioClip` | — | 295 |
| `Recarga` | static | `AudioClip` | `WeaponKind arma` | 306 |
| `Desenfundar` | static | `AudioClip` | `WeaponKind arma` | 401 |
| `GatilloVacio` | static | `AudioClip` | `WeaponKind arma` | 419 |

## `Assets/_Project/Scripts/Presentation/SoldierAnimatorDriver.cs`

**Tipos:** `class SoldierAnimatorDriver`

**Propiedades:** `PesoCapaDisparoActual`

## `Assets/_Project/Scripts/Presentation/SpriteFx.cs`

**Tipos:** `class SpritesReales`, `class SpriteFx`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Obtener` | static | `Sprite` | `string nombre` | 17 |
| `LimpiarTodo` | static | `void` | — | 46 |
| `Lanzar` | static | `SpriteFx` | `string sprite, Vector3 pos, Color color, float tam0, float tam1, float` | 80 |
| `Explosion` | static | `int` | `Vector3 pos, float radio` | 131 |
| `Golpe` | static | `void` | `Vector3 pos, Color color, float tam` | 158 |

**Propiedades:** `Activos`, `Lanzados`

## `Assets/_Project/Scripts/Presentation/SquadStateIndicatorView.cs`

**Tipos:** `class SquadStateIndicatorView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Apuntar` | static | `void` | `Soldier aliado` | 52 |

## `Assets/_Project/Scripts/Presentation/Subtitulos.cs`

**Tipos:** `class Subtitulos`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Poner` | static | `void` | `bool v` | 21 |
| `Etiqueta` | static | `string` | `string nombreDeClip` | 30 |
| `Lado` | static | `string` | `Vector3 oyente, float yawGrados, Vector3 fuente` | 44 |
| `Describir` | static | `string` | `string nombreDeClip, Vector3 oyente, float yawGrados, Vector3 fuente` | 53 |
| `Anunciar` | static | `void` | `AudioClip clip, Vector3 posicion` | 63 |

**Propiedades:** `Activos`, `Emitidos`

## `Assets/_Project/Scripts/Presentation/TrayectoriaGranadaView.cs`

**Tipos:** `class TrayectoriaGranadaView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `TrayectoriaGranadaView` | — | 29 |
| `ClearAll` | static | `void` | — | 42 |
| `Mostrar` |  | `void` | `Vector3 origen, Vector3 velocidad, Transform ignorar` | 96 |
| `Ocultar` |  | `void` | — | 134 |

**Propiedades:** `Instancia`, `Visible`, `Caida`, `CantidadDePuntos`

## `Assets/_Project/Scripts/Presentation/UnitLabelView.cs`

**Tipos:** `class UnitLabelView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bootstrap` |  | `void` | — | 51 |
| `Tick` |  | `bool` | `bool enRts` | 66 |
| `ApplyBillboard` |  | `void` | `Quaternion cameraRotation` | 97 |
| `Construir` | static | `UnitLabelView` | `Transform unidad` | 107 |
| `RegistrarTodas` | static | `int` | — | 173 |

**Propiedades:** `CurrentText`, `IsVisible`

## `Assets/_Project/Scripts/Presentation/VehicleFxReactor.cs`

**Tipos:** `class VehicleFxReactor`, `class VehicleSmokePuff`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bootstrap` |  | `void` | — | 33 |
| `Begin` |  | `void` | `Vector3 origin` | 198 |

## `Assets/_Project/Scripts/Presentation/VehicleMountIndicator.cs`

**Tipos:** `class VehicleMountIndicator`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Create` | static | `VehicleMountIndicator` | — | 27 |
| `Show` |  | `void` | `Vehicle vehicle, IEnumerable<Soldier> incomingAllies, bool puedeMontar` | 129 |
| `Hide` |  | `void` | — | 149 |

**Propiedades:** `UltimoPuedeMontar`

## `Assets/_Project/Scripts/Presentation/WorldUiDirector.cs`

**Tipos:** `class WorldUiDirector`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Register` | static | `void` | `HealthBarView v` | 45 |
| `Unregister` | static | `void` | `HealthBarView v` | 46 |
| `Register` | static | `void` | `MinimapIcon v` | 48 |
| `Unregister` | static | `void` | `MinimapIcon v` | 49 |
| `Register` | static | `void` | `PossessedMarkerView v` | 51 |
| `Unregister` | static | `void` | `PossessedMarkerView v` | 52 |
| `Register` | static | `void` | `UnitLabelView v` | 54 |
| `Unregister` | static | `void` | `UnitLabelView v` | 55 |
| `Register` | static | `void` | `RevivePromptView v` | 57 |
| `Unregister` | static | `void` | `RevivePromptView v` | 58 |
| `Tick` |  | `void` | — | 145 |
| `EnsurePopulated` | static | `void` | — | 323 |
| `Clear` | static | `void` | — | 340 |

**Propiedades:** `RegisteredCount`, `VisibleCount`, `CulledCount`, `IsDrivingUpdates`

## `Assets/_Project/Scripts/Tutorial/AutoplayRegistro.cs`

**Tipos:** `class LineaLog`, `class MuestraAutoplay`, `class CapturaAutoplay`, `class PasoAutoplay`, `class CorridaAutoplay`, `class EstadoVivoAutoplay`, `class AutoplayRegistro`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Iniciar` | static | `AutoplayRegistro` | `GameObject donde, PlayerInputDriver driver, TutorialManager tm, bool c` | 89 |
| `Evento` |  | `void` | `string tipo, string detalle` | 170 |
| `PasoInicio` |  | `void` | `int indice, TutorialManager.Paso p` | 225 |
| `Reintento` |  | `void` | `string motivo` | 235 |
| `ActualizarIntentos` |  | `void` | `int n` | 236 |
| `Tope` |  | `void` | `float s` | 237 |
| `IniciarDurante` |  | `void` | — | 239 |
| `PasoFin` |  | `IEnumerator` | `string resultado` | 259 |
| `Capturar` |  | `IEnumerator` | `string fase` | 301 |
| `Finalizar` |  | `void` | `string resultado` | 428 |

**Propiedades:** `Actual`, `RunId`, `Carpeta`, `Corrida`

## `Assets/_Project/Scripts/Tutorial/CatalogoDelTutorial.cs`

**Tipos:** `class CatalogoDelTutorial`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 13 |
| `RegistrarActivo` |  | `void` | — | 14 |
| `CamposVacios` |  | `System.Collections.Generic.List<string>` | — | 31 |

**Propiedades:** `Activo`

## `Assets/_Project/Scripts/Tutorial/EntradaVirtual.cs`

**Tipos:** `class EntradaVirtual`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `RestaurarHumano` | static | `void` | — | 56 |
| `Asegurar` | static | `EntradaVirtual` | `GameObject donde` | 67 |
| `Apretar` |  | `void` | `params Key[] teclas` | 91 |
| `Soltar` |  | `void` | `params Key[] teclas` | 92 |
| `BotonIzquierdo` |  | `void` | `bool abajo` | 93 |
| `BotonDerecho` |  | `void` | `bool abajo` | 94 |
| `Mover` |  | `void` | `Vector2 delta` | 96 |
| `SoltarTodo` |  | `void` | — | 97 |
| `EstaApretada` |  | `bool` | `Key k` | 98 |

**Propiedades:** `Actual`, `IgnorarHumano`

## `Assets/_Project/Scripts/Tutorial/KeyCapView.cs`

**Tipos:** `class TutorialTextures`, `enum Estado`, `class KeyCapView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Tecla` | static | `Texture2D` | `int w, int h, Estado e` | 27 |
| `Mouse` | static | `Texture2D` | `int modo` | 61 |
| `Casilla` | static | `Texture2D` | `bool hecha` | 97 |
| `Crear` | static | `KeyCapView` | `Transform padre, string token, Font fuente` | 154 |

**Propiedades:** `Token`, `Encendida`, `Hecha`

## `Assets/_Project/Scripts/Tutorial/TutorialAutoPlayer.cs`

**Tipos:** `class TutorialAutoPlayer`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Lanzar` | static | `TutorialAutoPlayer` | `int desdePaso = 0, bool ignorarHumano = true, bool capturas = true` | 48 |

**Propiedades:** `RunId`, `Carpeta`

## `Assets/_Project/Scripts/Tutorial/TutorialBeacon.cs`

**Tipos:** `class TutorialBeacon`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `AlfaSegunDistanciaAMira` | static | `float` | `float distanciaPx` | 28 |
| `Crear` | static | `TutorialBeacon` | `string texto, Color color, Vector3 posicion, Transform sigue = null, f` | 34 |
| `QuitarTodas` | static | `void` | — | 47 |
| `Quitar` |  | `void` | — | 54 |

**Propiedades:** `Activas`, `Texto`, `AlfaEtiqueta`

## `Assets/_Project/Scripts/Tutorial/TutorialFlags.cs`

**Tipos:** `class TutorialFlags`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Reiniciar` |  | `void` | — | 82 |

## `Assets/_Project/Scripts/Tutorial/TutorialLog.cs`

**Tipos:** `class TutorialLog`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Reiniciar` | static | `void` | — | 22 |
| `Escribir` | static | `void` | `string prefijo, string mensaje` | 31 |
| `Paso` | static | `void` | `int indice, int total, string titulo, string mensaje` | 40 |
| `Sub` | static | `void` | `int indice, int total, string titulo, int sub, int subTotal, string te` | 43 |

**Propiedades:** `Lineas`

## `Assets/_Project/Scripts/Tutorial/TutorialManager.cs`

**Tipos:** `class TutorialManager`, `class Sub`, `class Paso`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `CoberturaDePractica` |  | `GameObject` | `int numero` | 247 |
| `SaltarPaso` |  | `void` | — | 1782 |
| `BorrarProgreso` | static | `void` | — | 1795 |
| `SaltarPasoDelJugador` |  | `void` | — | 1815 |
| `Retomar` |  | `bool` | — | 1824 |
| `RegruparAliados` |  | `void` | `float distanciaMaxima = 14f` | 1834 |
| `DireccionHacia` | static | `string` | `Vector3 desde, float yawGrados, Vector3 objetivo` | 1872 |
| `Finalizar` |  | `void` | — | 1884 |
| `Reintentar` |  | `void` | — | 1892 |
| `VolverAlMenu` |  | `void` | — | 1898 |

**Propiedades:** `Instance`, `Indice`, `Total`, `Terminado`, `EnPausa`, `Ui`, `TiempoTotal`, `PasoActual`, `DuracionPorPaso`, `Bajas`, `TextoActual`, `Dummy`, `Pared`, `Destruible`, `ZonaA`, `ZonaB`, `Meta`, `EnemigoCuchillo`, `EnemigoGranada`, `EnemigoAtaque`, `MuroDePractica`, `TorretaDePractica`, `PasoGuardado`, `Retomando`

## `Assets/_Project/Scripts/Tutorial/TutorialUI.cs`

**Tipos:** `class TutorialUI`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Crear` | static | `TutorialUI` | `Transform canvas` | 43 |
| `MostrarPaso` |  | `void` | `int indice, int total, string tituloPaso, string[] textosSub, string l` | 192 |
| `Refrescar` |  | `void` | `string textoInstruccion, bool[] hechas, string[] textosSub, string tex` | 238 |
| `TeclaEncendida` |  | `bool` | `string token` | 306 |
| `TeclaHecha` |  | `bool` | `string token` | 312 |
| `OcultarPanel` |  | `void` | — | 318 |
| `DestelloDePaso` |  | `void` | `string texto, Color color` | 320 |
| `Mensaje` |  | `void` | `string texto, Color color` | 327 |

**Propiedades:** `TextoInstruccion`, `TextoTitulo`, `TextoCabecera`, `TextoPista`, `TextoBanner`, `Teclas`, `SubsVisibles`, `Panel`

## `Assets/_Project/Scripts/Tutorial/VictoriaTutorial.cs`

**Tipos:** `class VictoriaTutorial`, `struct Cubito`, `class Confeti`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Iniciar` | static | `VictoriaTutorial` | `TutorialManager m, Vector3 centro` | 40 |

**Propiedades:** `PanelVisible`, `CubitosVivos`, `ConfetiActivo`, `BotonRepetir`, `BotonMenu`, `Actual`

## `Assets/_Project/Scripts/UI/AimUI.cs`

**Tipos:** `class AimUI`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `SetCrosshairScale` |  | `void` | `float scale` | 28 |
| `SetCrosshairZoomScale` |  | `void` | `float multiplier` | 42 |
| `SetSpread01` |  | `void` | `float fraction` | 53 |
| `SetCrosshairStyle` |  | `void` | `Sprite sprite, float lado` | 62 |
| `SetBaseCrosshairHidden` |  | `void` | `bool hidden` | 78 |
| `SetCrosshairColor` |  | `void` | `Color color` | 135 |
| `BindAmmoWarning` |  | `void` | `Text text` | 148 |
| `UpdateReloadCircle` |  | `void` | `SP.Combat.WeaponHolder weapon` | 195 |
| `UpdateAmmoWarning` |  | `void` | `SP.Combat.WeaponHolder weapon` | 228 |
| `SetVisible` |  | `void` | `bool visible` | 253 |
| `Bind` |  | `void` | `Text prompt, Image cross` | 266 |
| `BindSoldierInfo` |  | `void` | `GameObject panel, Text info` | 282 |
| `BindVehicleInfo` |  | `void` | `GameObject panel, Image[] squares` | 297 |
| `Initialize` |  | `void` | — | 305 |
| `SetWatchedShooter` |  | `void` | `int soldierId` | 395 |
| `UpdateFromAimResult` |  | `void` | `AimResult result` | 557 |
| `PonerPromptContextual` |  | `void` | `string texto, bool destacado` | 650 |

**Propiedades:** `CurrentAimTintColor`, `CurrentPrompt`, `IsVisible`, `CurrentPulseFrequency`

## `Assets/_Project/Scripts/UI/AjustesDeJuego.cs`

**Tipos:** `class AjustesDeJuego`, `class PanelAjustesExtra`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TextoEscala` | static | `string` | — | 25 |
| `SiguienteEscala` | static | `void` | — | 26 |
| `PonerEscala` | static | `void` | `float e` | 31 |
| `AplicarEscala` | static | `void` | — | 37 |
| `Resoluciones` | static | `List<Resolution>` | — | 70 |
| `IndiceResolucion` | static | `int` | — | 84 |
| `TextoResolucion` | static | `string` | — | 92 |
| `AlternarPantallaCompleta` | static | `void` | — | 94 |
| `SiguienteResolucion` | static | `void` | — | 95 |
| `AplicarPantalla` | static | `void` | — | 97 |
| `TextoCalidad` | static | `string` | — | 106 |
| `SiguienteCalidad` | static | `void` | — | 107 |
| `PonerDaltonismo` | static | `void` | `bool v` | 116 |
| `PonerHudMinimo` | static | `void` | `bool v` | 117 |
| `Adaptar` | static | `Color` | `Color c` | 120 |
| `Preparar` | static | `void` | `GameObject settingsPanel` | 137 |
| `Refrescar` | static | `void` | `Transform extra` | 175 |
| `AplicarHudMinimo` | static | `void` | `Transform canvas` | 224 |

**Propiedades:** `Daltonismo`, `HudMinimo`, `Escala`, `PantallaCompleta`

## `Assets/_Project/Scripts/UI/AlertQueue.cs`

**Tipos:** `enum AlertPriority`, `struct PendingAlert`, `class AlertQueue`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Clear` | static | `void` | — | 78 |
| `Push` | static | `void` | `string message, AlertPriority priority, float seconds` | 86 |
| `PushAt` | static | `void` | `string message, AlertPriority priority, float seconds, float now` | 92 |
| `TryDequeue` | static | `bool` | `out string message, out float seconds` | 129 |
| `NotifyFinished` | static | `void` | — | 159 |
| `SelectNext` | static | `int` | `PendingAlert[] pending, float now` | 177 |
| `IsStale` | static | `bool` | `PendingAlert alert, float now` | 200 |

**Propiedades:** `PendingCount`, `IsBusy`, `CurrentPriority`

## `Assets/_Project/Scripts/UI/AliadosSinEstorbo.cs`

**Tipos:** `class AliadosSinEstorbo`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `void` | — | 24 |
| `Actualizar` |  | `void` | `Camera cam` | 32 |

**Propiedades:** `Ocultos`, `Instancia`

## `Assets/_Project/Scripts/UI/ButtonSfx.cs`

**Tipos:** `class ButtonSfx`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `OnPointerEnter` |  | `void` | `PointerEventData eventData` | 51 |
| `OnPointerExit` |  | `void` | `PointerEventData eventData` | 58 |
| `Attach` | static | `void` | `Button button` | 87 |

## `Assets/_Project/Scripts/UI/CirculoDeProgreso.cs`

**Tipos:** `class CirculoDeProgreso`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Construir` | static | `CirculoDeProgreso` | `Transform padre, float diametro, Color colorFondo, Color colorRelleno` | 75 |
| `SetProgreso` |  | `void` | `float valor01` | 107 |
| `SetVisible` |  | `void` | `bool visible` | 112 |

**Propiedades:** `Relleno`, `Fondo`

## `Assets/_Project/Scripts/UI/ControlsTable.cs`

**Tipos:** `enum ControlContext`, `struct ControlEntry`, `class ControlsTable`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `For` | static | `IEnumerable<ControlEntry>` | `ControlContext ctx` | 172 |
| `LineFor` | static | `string` | `ControlContext ctx` | 182 |
| `LineFor` | static | `string` | `ControlContext ctx, int maxEntries` | 185 |
| `FullText` | static | `string` | — | 201 |
| `DisplayKeyFor` | static | `string` | `ControlEntry e` | 215 |
| `HeaderFor` | static | `string` | `ControlContext ctx` | 224 |
| `FormatKey` | static | `string` | `string key` | 239 |
| `Validate` | static | `bool` | `out string problem` | 251 |

## `Assets/_Project/Scripts/UI/DamageDirectionView.cs`

**Tipos:** `class DamageDirectionView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Image arrowImage, PlayerBrain playerBrain` | 21 |
| `Initialize` |  | `void` | — | 105 |

## `Assets/_Project/Scripts/UI/DamageVignetteView.cs`

**Tipos:** `class DamageVignetteView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Image img, PlayerBrain playerBrain` | 22 |
| `SetSpeedFraction` |  | `void` | `float frac01` | 74 |

**Propiedades:** `CurrentAlpha`

## `Assets/_Project/Scripts/UI/DeadNoticeView.cs`

**Tipos:** `class DeadNoticeView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Text text, CanvasGroup canvasGroup` | 15 |
| `Show` |  | `void` | `string message, float fadeSeconds = 3f` | 41 |

## `Assets/_Project/Scripts/UI/Diagramador.cs`

**Tipos:** `class Diagramador`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `AcomodarConfirmarSalida` | static | `void` | `GameObject panel` | 43 |
| `AcomodarResultado` | static | `void` | `GameObject panel` | 68 |
| `ContarSolapes` | static | `int` | `GameObject panel` | 95 |

## `Assets/_Project/Scripts/UI/FondoOpaco.cs`

**Tipos:** `class FondoOpaco`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `AsegurarContraste` | static | `void` | `Text texto` | 59 |
| `Poner` | static | `Image` | `Text texto` | 72 |
| `LlevarArribaAlCentro` | static | `void` | `RectTransform rt, float margenDesdeArriba = AlturaLibreArriba` | 178 |

## `Assets/_Project/Scripts/UI/GroupCardsView.cs`

**Tipos:** `class GroupCardsView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Text[] slots` | 48 |
| `SetGroups` |  | `void` | `IReadOnlyList<List<Soldier>> groups` | 56 |
| `Refresh` |  | `void` | — | 73 |
| `Summarize` | static | `void` | `List<Soldier> group, out int vivos, out float vidaPromedio` | 112 |

## `Assets/_Project/Scripts/UI/InstructionBannerView.cs`

**Tipos:** `class InstructionBannerView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Text text` | 22 |
| `SetText` |  | `void` | `string message` | 24 |

## `Assets/_Project/Scripts/UI/KeyRebindView.cs`

**Tipos:** `class KeyRebindView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TextOfRow` |  | `string` | `int i` | 32 |
| `Bind` |  | `void` | `Button[] rows, Text[] labels, string[] actionIds` | 44 |
| `BeginListening` |  | `void` | `int row` | 66 |
| `ShouldIgnoreCapture` | static | `bool` | `int listenStartFrame, int currentFrame` | 79 |
| `RefreshAll` |  | `void` | — | 121 |
| `NameOf` | static | `string` | `string actionId` | 155 |

**Propiedades:** `IsListening`

## `Assets/_Project/Scripts/UI/KillFeedView.cs`

**Tipos:** `class KillFeedView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Text text` | 20 |
| `ShowKill` |  | `void` | — | 40 |

## `Assets/_Project/Scripts/UI/LayoutDeAjustes.cs`

**Tipos:** `class LayoutDeAjustes`, `struct Resultado`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `LayoutDeAjustes` | `GameObject settingsPanel` | 47 |
| `AreaDelCanvas` |  | `Vector2` | — | 86 |
| `Aplicar` |  | `void` | — | 94 |
| `Aplicar` |  | `void` | `Vector2 area` | 96 |
| `Diagnosticar` | static | `List<string>` | `GameObject settingsPanel, Vector2 area` | 272 |

**Propiedades:** `Ultimo`, `Aplicaciones`

## `Assets/_Project/Scripts/UI/LowHealthPulseView.cs`

**Tipos:** `class LowHealthPulseView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Image image` | 58 |

**Propiedades:** `CurrentAlpha`

## `Assets/_Project/Scripts/UI/MenuDeOrdenes.cs`

**Tipos:** `class ContextoRadial`, `class MenuDeOrdenes`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Mostrar` |  | `void` | `int cat, bool contextual, params int[] opciones` | 29 |
| `ReiniciarActivo` | static | `void` | — | 58 |
| `RegistrarActivo` |  | `void` | — | 59 |
| `PonerPista` | static | `void` | `int categoria, int opcion = -1` | 152 |
| `EsVisible` |  | `bool` | `int categoria` | 167 |
| `EsContextual` |  | `bool` | `int categoria` | 168 |
| `Bind` |  | `void` | `Text texto, CanvasGroup canvasGroup` | 171 |
| `PonerSoldados` |  | `void` | `string s1, string s2, string s3` | 198 |
| `NombreDeOpcion` | static | `string` | `int categoria, int sub` | 210 |
| `Abrir` |  | `void` | — | 249 |
| `Abrir` |  | `void` | `ContextoRadial ctx` | 254 |
| `CancelarSeleccion` |  | `void` | — | 295 |
| `Cerrar` |  | `void` | — | 304 |
| `MoverSeleccion` |  | `void` | `Vector2 delta` | 314 |
| `ElegirDirecto` |  | `void` | `int categoria, int sub = -1` | 365 |
| `CategoriaDeTecla` |  | `int` | `int tecla` | 390 |
| `AsegurarEnEscena` | static | `MenuDeOrdenes` | — | 466 |
| `Construir` | static | `MenuDeOrdenes` | `Transform padre` | 555 |
| `LeerTecla` | static | `int` | — | 618 |

**Propiedades:** `Activo`, `Abierto`, `Seleccion`, `Sub`, `EsRadial`, `EnAnilloExterior`, `CategoriasVisibles`, `OpcionesVisibles`

## `Assets/_Project/Scripts/UI/MilitaryButtonSkin.cs`

**Tipos:** `class MilitaryButtonSkin`

**Propiedades:** `Shared`

## `Assets/_Project/Scripts/UI/MinimapFollow.cs`

**Tipos:** `class MinimapFollow`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 25 |
| `RegistrarActivo` |  | `void` | — | 26 |
| `TryMinimapPointToWorld` |  | `bool` | `Vector2 localPoint, out Vector3 world` | 111 |
| `TryWorldToMinimapPoint` |  | `bool` | `Vector3 world, out Vector2 localPoint` | 152 |
| `AplicarTamanoInicial` |  | `void` | — | 282 |
| `AplicarTamanoGuardado` |  | `void` | — | 296 |
| `AlternarTamano` |  | `bool` | — | 301 |
| `TamanoFijo` |  | `Vector2` | `int indice` | 315 |
| `CiclarTamanoFijo` |  | `int` | — | 321 |

**Propiedades:** `Activo`, `MinimapRect`, `MinimapCamera`, `GroundY`, `MapHalfExtent`, `Agrandado`, `Marco`, `IndiceTamanoFijo`

## `Assets/_Project/Scripts/UI/MirillaView.cs`

**Tipos:** `class MirillaView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `MirillaView` | `Transform canvasRoot` | 43 |
| `Actualizar` |  | `void` | `bool zoom, ReticleStyle estilo, Color tinte` | 92 |
| `Ocultar` |  | `void` | — | 164 |
| `SpriteDe` | static | `Sprite` | `ReticleStyle e` | 173 |

**Propiedades:** `Visible`, `Estilo`, `Alfa`, `Reticula`, `Instancia`

## `Assets/_Project/Scripts/UI/MissionStatusView.cs`

**Tipos:** `class MissionStatusView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Refresh` |  | `void` | — | 39 |

## `Assets/_Project/Scripts/UI/ModeToastView.cs`

**Tipos:** `class ModeToastView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Text text, CanvasGroup canvasGroup` | 16 |
| `Show` |  | `void` | `string text, float fadeSeconds = 1f` | 37 |

## `Assets/_Project/Scripts/UI/ModoDiosView.cs`

**Tipos:** `class ModoDiosView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 13 |
| `RegistrarActivo` |  | `void` | — | 14 |
| `Asegurar` | static | `ModoDiosView` | `Transform canvasRoot` | 23 |

**Propiedades:** `Activo`

## `Assets/_Project/Scripts/UI/OffscreenAllyMarkerView.cs`

**Tipos:** `class OffscreenAllyMarkerView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Image[] arrows` | 93 |
| `SetSquad` |  | `void` | `IEnumerable<Soldier> soldiers` | 99 |

**Propiedades:** `VisibleMarkerCount`

## `Assets/_Project/Scripts/UI/OffscreenKillMarkerView.cs`

**Tipos:** `class OffscreenKillMarkerView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Image image` | 23 |
| `Report` |  | `void` | `Vector3 worldPosition` | 38 |

**Propiedades:** `IsShowing`, `ArrowPosition`

## `Assets/_Project/Scripts/UI/PerfHudView.cs`

**Tipos:** `class PerfHudView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Text text` | 50 |
| `Toggle` |  | `void` | — | 56 |

**Propiedades:** `Visible`, `MedianMs`, `P95Ms`

## `Assets/_Project/Scripts/UI/PhaseBannerView.cs`

**Tipos:** `class PhaseBannerView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Text text` | 14 |
| `Show` |  | `void` | `string message, float holdSeconds = 2.2f` | 29 |

## `Assets/_Project/Scripts/UI/PlayerHealthView.cs`

**Tipos:** `class PlayerHealthView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Text text, Image fillImage` | 24 |
| `UpdateFrom` |  | `void` | `Soldier soldier` | 41 |

## `Assets/_Project/Scripts/UI/RosterRowView.cs`

**Tipos:** `class RosterRowView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Soldier soldier, int index` | 70 |

**Propiedades:** `Soldier`, `SoldierId`, `Index`, `IsHighlighted`

## `Assets/_Project/Scripts/UI/RosterView.cs`

**Tipos:** `class RosterView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 16 |
| `RegistrarActivo` |  | `void` | — | 17 |
| `SetRowPrefab` |  | `void` | `RosterRowView prefab` | 25 |
| `Rebuild` |  | `void` | — | 44 |

**Propiedades:** `Activo`

## `Assets/_Project/Scripts/UI/ScreenFlashView.cs`

**Tipos:** `class ScreenFlashView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Image image` | 49 |
| `Flash` |  | `void` | `Color color, float peakAlpha, float seconds` | 92 |
| `Explosion` | static | `void` | `float intensity01` | 173 |
| `ModeChange` | static | `void` | — | 199 |

**Propiedades:** `Instance`, `CurrentAlpha`

## `Assets/_Project/Scripts/UI/SelectedSoldierUI.cs`

**Tipos:** `class SelectedSoldierUI`, `class Row`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `AddRow` |  | `void` | `Soldier soldier, Image background, Text label, Image healthFill = null` | 60 |
| `Initialize` |  | `void` | — | 220 |
| `IsHighlighted` |  | `bool` | `int soldierId` | 267 |

## `Assets/_Project/Scripts/UI/SelectionCountView.cs`

**Tipos:** `class SelectionCountView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Text text` | 15 |
| `SetModeVisible` |  | `void` | `bool allowed` | 37 |

## `Assets/_Project/Scripts/UI/SpriteBlanco.cs`

**Tipos:** `class SpriteBlanco`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Obtener` | static | `Sprite` | — | 28 |
| `Reparar` | static | `void` | `Image img` | 46 |
| `RepararTodo` | static | `int` | `GameObject raiz` | 56 |

## `Assets/_Project/Scripts/UI/SpriteDeTriangulo.cs`

**Tipos:** `class SpriteTriangulo`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Obtener` | static | `Sprite` | — | 12 |
| `Aplicar` | static | `void` | `Image img` | 38 |

## `Assets/_Project/Scripts/UI/TurretAimView.cs`

**Tipos:** `class TurretAimView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Image reticleImage, Image gap, Image cooldown, LineRenderer ring` | 31 |
| `SetVisible` |  | `void` | `bool visible` | 58 |
| `UpdateFrom` |  | `void` | `TurretWeapon turret` | 64 |
| `PredictedImpactPoint` | static | `Vector3` | `TurretWeapon turret` | 120 |

## `Assets/_Project/Scripts/UI/VehicleKeysPanel.cs`

**Tipos:** `class VehicleKeysPanel`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `VehicleKeysPanel` | `Transform canvasRaiz` | 34 |
| `SetVisible` |  | `void` | `bool v` | 75 |
| `Destellar` |  | `void` | `string tecla` | 81 |
| `Actualizar` |  | `void` | `Vehicle v, VehicleSeatRole? miAsiento, int aliadosEnCamino` | 90 |

**Propiedades:** `UltimoTexto`, `Visible`

## `Assets/_Project/Scripts/UI/VehicleStatusView.cs`

**Tipos:** `class VehicleStatusView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Text speed, Image health, Text gunner` | 43 |
| `BindCrew` |  | `void` | `GameObject panelRoot, GameObject[] rows, Image[] icons, Text[] iconLab` | 54 |
| `RoleIconText` | static | `string` | `SP.Vehicles.VehicleSeatRole role` | 172 |
| `RoleIconColor` | static | `Color` | `SP.Vehicles.VehicleSeatRole role` | 180 |
| `SetSeat` |  | `void` | `SP.Vehicles.VehicleSeatRole? role` | 188 |
| `UpdateFrom` |  | `void` | `Vehicle vehicle, VehicleMotor motor, bool braking = false` | 201 |

## `Assets/_Project/Scripts/UI/WeaponStatusView.cs`

**Tipos:** `class WeaponStatusView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 13 |
| `RegistrarActivo` |  | `void` | — | 14 |
| `IconFor` | static | `Sprite` | `WeaponKind kind` | 35 |
| `EnsureIcon` |  | `Image` | — | 57 |
| `Bind` |  | `void` | `Text text, Image fillImage` | 86 |
| `UpdateFrom` |  | `void` | `WeaponHolder weapon` | 109 |

**Propiedades:** `Activo`, `Icon`

## `Assets/_Project/Scripts/Vehicles/Atropello.cs`

**Tipos:** `class Atropello`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Barrer` | static | `int` | `Transform vehiculo, Collider casco, float velocidad, Vehicle datos` | 37 |
| `AplastarObstaculos` | static | `int` | `Transform vehiculo, float velocidad, float radioDelCasco` | 85 |
| `EquipoDeLaTripulacion` | static | `TeamId?` | `Vehicle datos` | 110 |
| `Derribar` | static | `void` | `Soldier victima, Vector3 direccion` | 123 |

## `Assets/_Project/Scripts/Vehicles/DetachedTurretFlight.cs`

**Tipos:** `class DetachedTurretFlight`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Launch` |  | `void` | — | 19 |

## `Assets/_Project/Scripts/Vehicles/TorretaFija.cs`

**Tipos:** `class TorretaFija`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `De` | static | `TorretaFija` | `Soldier s` | 83 |
| `MasCercana` | static | `TorretaFija` | `Vector3 p, float radio` | 90 |
| `Ocupar` |  | `bool` | `Soldier s, out string motivo` | 105 |
| `Liberar` |  | `void` | — | 142 |
| `AcotarGiro` |  | `void` | `Soldier s` | 159 |
| `FraccionDeArco` |  | `float` | `Soldier s` | 169 |
| `InstalarEnEscena` | static | `int` | — | 205 |
| `Instalar` | static | `TorretaFija` | `GameObject go` | 219 |

**Propiedades:** `Todas`, `Ocupante`, `Libre`, `Puesto`, `YawCentro`

## `Assets/_Project/Scripts/Vehicles/TurretAI.cs`

**Tipos:** `class TurretAI`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bootstrap` |  | `void` | — | 72 |
| `Tick` |  | `void` | `float dt` | 95 |

**Propiedades:** `IsEngaging`, `TargetVehicle`

## `Assets/_Project/Scripts/Vehicles/TurretWeapon.cs`

**Tipos:** `class TurretWeapon`, `enum AmmoType`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `CycleAmmo` |  | `void` | — | 57 |
| `IsOnTarget` |  | `bool` | `float toleranceDeg = 4f` | 107 |
| `Bootstrap` |  | `void` | — | 116 |
| `AddDesiredYaw` |  | `void` | `float delta` | 133 |
| `AddDesiredPitch` |  | `void` | `float delta` | 146 |
| `TickPlayerAim` |  | `void` | `float dt` | 160 |
| `SetPool` |  | `void` | `ProjectilePool projectilePool` | 173 |
| `RotateYaw` |  | `void` | `float yawDelta` | 175 |
| `AimAt` |  | `void` | `Vector3 worldPoint, float dt` | 181 |
| `IsAimedAt` |  | `bool` | `Vector3 worldPoint, float toleranceDeg = 4f` | 204 |
| `Tick` |  | `void` | `float dt` | 213 |
| `ResolverTirador` |  | `void` | `out int shooterId, out TeamId team` | 248 |
| `TryFire` |  | `bool` | — | 262 |

**Propiedades:** `Ammo`, `ExplosionRadius`, `CurrentDamage`, `CurrentProjectileColor`, `ProjectileGravity`, `Heat`, `EffectiveCooldown`, `CooldownFraction01`, `DesiredYaw`, `DesiredPitch`, `YawGapDeg`, `MuzzleIsNearGround`

## `Assets/_Project/Scripts/Vehicles/Vehicle.cs`

**Tipos:** `class Vehicle`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TakeDamage` |  | `void` | `int amount, int attackerId` | 48 |
| `AsignarBando` |  | `void` | `SP.Combat.TeamId bando, Color tinte` | 112 |
| `FinalExplosion` |  | `void` | — | 149 |
| `IsMountAnimating` |  | `bool` | `Soldier soldier` | 197 |
| `RefreshOccupancyColor` |  | `void` | — | 219 |
| `ClosestBoardingPoint` |  | `Vector3` | `Vector3 desde` | 272 |
| `IsSeatFree` |  | `bool` | `VehicleSeatRole role` | 292 |
| `FirstFreeSeat` |  | `VehicleSeatRole?` | — | 294 |
| `ConfigurarTripulacion` |  | `void` | `Soldier[] tripulantes, VehicleSeatRole[] roles, bool enemigo` | 309 |
| `PuedeAbordar` |  | `bool` | `Soldier soldier, out string motivo` | 335 |
| `PuedeAbordar` |  | `bool` | `Soldier soldier` | 343 |
| `Mount` |  | `bool` | `Soldier soldier, VehicleSeatRole? preferredRole = null, bool instantan` | 345 |
| `Dismount` |  | `bool` | `Soldier soldier` | 556 |
| `SwapSeats` |  | `bool` | `Soldier a, Soldier b` | 606 |
| `MoveToSeat` |  | `bool` | `Soldier soldier, VehicleSeatRole role` | 631 |
| `RoleOf` |  | `VehicleSeatRole?` | `Soldier soldier` | 656 |
| `SoldierInSeat` |  | `Soldier` | `VehicleSeatRole role` | 681 |

**Propiedades:** `Health`, `IsDestroyed`, `IsInAgony`, `TorretaCanon`, `TorretaMetralleta`, `FinalExplosionDone`, `Bando`, `PlayerAboard`, `Capacity`, `OccupantCount`, `HasAnyRoom`, `Driver`, `Gunner`, `Occupants`, `EsTanqueEnemigo`, `AllSeatRoles`

## `Assets/_Project/Scripts/Vehicles/VehicleBrain.cs`

**Tipos:** `class VehicleBrain`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bootstrap` |  | `void` | — | 38 |
| `IssueMoveOrder` |  | `void` | `Vector3 point` | 66 |
| `ConfigurarPatrulla` |  | `void` | `Vector3[] puntos, bool frenarSiCombate, float gasTope` | 85 |
| `Stop` |  | `void` | — | 119 |
| `Tick` |  | `void` | `float dt` | 196 |

**Propiedades:** `HasOrder`, `CurrentDestination`, `IsPlayerDriving`, `Route`, `RouteIndex`, `TienePatrulla`, `IndiceDePatrulla`

## `Assets/_Project/Scripts/Vehicles/VehicleMotor.cs`

**Tipos:** `class VehicleMotor`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Drive` |  | `void` | `float throttle, float steer, float dt` | 29 |
| `Brake` |  | `void` | `float dt` | 51 |
| `Nudge` |  | `void` | `Vector3 worldDelta` | 155 |

**Propiedades:** `CurrentSpeed`, `MaxSpeed`, `IsStopped`
