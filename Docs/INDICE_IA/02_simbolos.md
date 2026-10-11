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
| `SetRunning` |  | `void` | `bool correr` | 74 |
| `Jump` |  | `void` | — | 81 |
| `TryVault` |  | `bool` | — | 104 |
| `IniciarTrepaVertical` |  | `bool` | `Vector3 hasta, float segundos` | 129 |
| `ResetMotionState` |  | `void` | — | 192 |
| `DanioDeCaida` | static | `int` | `float metros` | 218 |
| `TickVertical` |  | `void` | `float dt` | 247 |
| `SetCrouching` |  | `void` | `bool agachado` | 346 |
| `Move` |  | `void` | `Vector3 worldDirection, float dt` | 368 |
| `RevisarBordeParaPrueba` |  | `void` | `float dt` | 377 |
| `RotateYaw` |  | `void` | `float yawDeltaDegrees` | 379 |
| `LookTowards` |  | `void` | `Vector3 worldPoint, float dt` | 384 |
| `MoveTowards` |  | `bool` | `Vector3 worldPoint, float arriveThreshold, float dt` | 394 |

**Propiedades:** `IsJumping`, `Atado`, `MoveSpeed`, `Corriendo`, `Vaulting`, `UltimoMotivoDeTrepa`, `UltimaCaidaMetros`, `UltimoDanioDeCaida`, `IsCrouching`, `EyeHeightDrop`, `EyeHeightDropSuave`

## `Assets/_Project/Scripts/Ai/AiBrain.Cargas.cs`

**Tipos:** `class AiBrain`

**Propiedades:** `AlejandoseDeUnaCarga`, `AlejamientosDeCarga`

## `Assets/_Project/Scripts/Ai/AiBrain.Disparo.cs`

**Tipos:** `class AiBrain`

**Propiedades:** `Herido`

## `Assets/_Project/Scripts/Ai/AiBrain.Granadas.cs`

**Tipos:** `class AiBrain`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarContadoresGranadaIA` | static | `void` | — | 33 |
| `OrdenSuprimir` |  | `void` | `Vector3 punto, float segundos = SegundosDeSupresionOrdenada` | 54 |
| `OrdenLanzarGranada` |  | `bool` | `Vector3 punto` | 77 |
| `RecibirSupresion` |  | `void` | `float segundos` | 149 |

**Propiedades:** `Suprimido`, `HuyendoDeGranada`, `GranadasLanzadasPorIA`, `HuidasDeGranada`, `SuprimiendoPorOrden`

## `Assets/_Project/Scripts/Ai/AiBrain.Herido.cs`

**Tipos:** `class AiBrain`

**Propiedades:** `EnRepliegue`, `YendoAlMedico`, `Repliegues`, `DestinoDeRepliegue`

## `Assets/_Project/Scripts/Ai/AiBrain.Navegacion.cs`

**Tipos:** `class AiBrain`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Ignorando` |  | `bool` | `Soldier s` | 240 |

**Propiedades:** `VaHaciaUnaCobertura`, `CoberturaElegida`, `ObjetivosDescartados`, `GolpesSinProgreso`, `DesatascosHechos`

## `Assets/_Project/Scripts/Ai/AiBrain.Revivir.cs`

**Tipos:** `class AiBrain`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ResetearTrasRevivir` |  | `void` | `bool soltarPosesion` | 23 |
| `CancelarCuracionLocal` |  | `void` | — | 59 |

## `Assets/_Project/Scripts/Ai/AiBrain.Sentidos.cs`

**Tipos:** `class AiBrain`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TieneLineaDeTiro` |  | `bool` | `Soldier objetivo` | 37 |

**Propiedades:** `EnVegetacion`

## `Assets/_Project/Scripts/Ai/AiBrain.Separacion.cs`

**Tipos:** `class AiBrain`

**Propiedades:** `EmpujesDeSeparacion`

## `Assets/_Project/Scripts/Ai/AiBrain.Tactica.cs`

**Tipos:** `class AiBrain`, `enum CoverSubState`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `IssueCoverOrder` |  | `void` | `Vector3 punto, Collider dueno` | 60 |
| `TomarCoberturaInicial` |  | `void` | `Vector3 punto, Collider dueno` | 76 |
| `PuntajeDeObjetivo` |  | `float` | `Soldier s` | 627 |

**Propiedades:** `MountTargetVehicle`, `EnCobertura`, `YendoACobertura`, `CoberturaPorOrden`, `CoberturaPunto`, `VersionDeCobertura`, `UltimaPresion`, `CoberturasElegidas`, `CambiosDeCobertura`, `Quieto`, `Pasivo`, `RegresosForzados`, `SiguiendoAlJugador`

## `Assets/_Project/Scripts/Ai/AiBrain.Vehiculos.cs`

**Tipos:** `class AiBrain`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `IssueAttackVehicleOrder` |  | `void` | `Vehicle v` | 42 |

**Propiedades:** `VehiculoAmenaza`, `VehiculoOrdenado`, `ConCoheteParaVehiculo`, `DisparosAVehiculos`

## `Assets/_Project/Scripts/Ai/AiBrain.cs`

**Tipos:** `enum CombatStance`, `class AiBrain`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `SetPatrolRoute` |  | `void` | `Vector3[] points` | 239 |
| `SetPatrolWaypoints` |  | `void` | `Transform[] waypoints` | 248 |
| `ReactivarNavegacion` |  | `void` | — | 356 |
| `DescribirOrden` |  | `string` | — | 390 |
| `ConfigurarAlcances` |  | `void` | `float vision, float ataque` | 430 |
| `Bootstrap` |  | `void` | — | 521 |
| `IssueMoveOrder` |  | `void` | `Vector3 point, bool queued = false` | 667 |
| `IssueMountOrder` |  | `void` | `Vehicle vehicle` | 702 |
| `IssueFollowOrder` |  | `void` | `Soldier leader, Vector3 formationOffsetLocal = default, float forzarSe` | 730 |
| `IssueAttackOrder` |  | `void` | `Soldier enemy` | 749 |
| `CancelOrder` |  | `void` | — | 769 |
| `Tick` |  | `void` | `float dt` | 797 |

**Propiedades:** `PatrolCount`, `QueuedOrderCount`, `QueuedDestinations`, `RemainingPathPoints`, `CurrentPath`, `State`, `MontadoEnVehiculo`, `IsPossessedByPlayer`, `CurrentTarget`, `TieneOrden`, `Stance`, `HomePosition`, `EffectiveVisionRange`, `BonusDeAlcance`, `EffectiveAttackRange`, `SenseIntervalTicks`, `TicksSinceLastSense`, `LastSensedTarget`, `CurrentOrderDestination`, `PendingMoveDestination`, `FollowTarget`, `LibreParaAyudar`, `SiguiendoForzado`

## `Assets/_Project/Scripts/Ai/AjustesDeEscuadra.cs`

**Tipos:** `class AjustesDeEscuadra`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 21 |
| `RegistrarActivo` |  | `void` | — | 22 |
| `Aplicar` |  | `void` | — | 55 |
| `AsegurarEnEscena` | static | `AjustesDeEscuadra` | — | 69 |

**Propiedades:** `Activo`

## `Assets/_Project/Scripts/Ai/PathPreview.cs`

**Tipos:** `class PathPreview`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Attach` |  | `void` | `WaypointGraph waypointGraph` | 42 |
| `Show` |  | `bool` | `Vector3 from, Vector3 to` | 74 |
| `Hide` |  | `void` | — | 115 |

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
| `OrbitarRts` |  | `void` | `float deltaYaw, float deltaInclinacion` | 79 |
| `SetCamera` |  | `void` | `Camera c` | 132 |
| `MostrarRutas` |  | `void` | `bool visibles` | 159 |
| `SetZoomed` |  | `void` | `bool value` | 175 |
| `SetZoomFactor` |  | `void` | `float factor` | 210 |
| `AddPitch` |  | `void` | `float delta` | 293 |
| `ResetPitch` |  | `void` | — | 294 |
| `KickRecoil` |  | `void` | `float degrees` | 310 |
| `KickDirectional` |  | `void` | `Vector3 worldDirection, float magnitude` | 332 |
| `AddFrameOffset` |  | `void` | `Vector3 offset` | 346 |
| `SetWalking` |  | `void` | `bool value` | 368 |
| `SetMode` |  | `void` | `ControlMode mode, Vector3? rtsFallbackCenter = null` | 420 |
| `RestoreOrSetRtsView` |  | `void` | `Vector3 fallbackCenter` | 469 |
| `ToggleMode` |  | `void` | `Vector3? rtsFallbackCenter = null` | 533 |
| `CentrarRtsEn` |  | `void` | `Vector3 foco, float radio` | 538 |
| `BeginTransition` |  | `void` | `Transform target, float duration = 0.35f` | 574 |
| `FollowFps` |  | `void` | `Soldier soldier` | 612 |
| `BeginFollowBlend` |  | `void` | `float seconds` | 652 |
| `FollowOverShoulder` |  | `void` | `Transform target, float distance = 4f, float height = 1.53f, float hei` | 730 |
| `FollowAnchor` |  | `void` | `Transform anchor` | 765 |
| `FollowThirdPerson` |  | `void` | `Transform target, float distance = 7f, float height = 3f` | 789 |
| `FollowThirdPersonAimed` |  | `void` | `Vector3 pivotPos, Vector3 aimForward, float distance = 7f, float heigh` | 831 |
| `SetRtsView` |  | `void` | `Vector3 center` | 860 |
| `GetForwardRay` |  | `Ray` | — | 871 |
| `MoverAlturaRts` |  | `float` | `float eje, float dt` | 905 |
| `Pan` |  | `void` | `Vector3 worldDelta` | 936 |
| `RecenterOn` |  | `void` | `Vector3 point` | 948 |
| `Zoom` |  | `void` | `float delta` | 965 |
| `ZoomHaciaCursor` |  | `void` | `float delta, Vector2 pantalla` | 992 |

**Propiedades:** `Instance`, `AlturaMinimaRts`, `Mode`, `RutasVisibles`, `AdsBlend`, `AdsBlendSuave`, `Respiracion`, `ZoomFactor`, `EstaConZoom`, `FovDeZoom`, `FovObjetivo`, `Pitch`, `RecoilPitch`, `ShakeOffset`, `MaxShakeMagnitude`, `BobOffset`, `RtsForzado`, `RtsFoco`, `IsTransitioning`, `Cam`, `PanTargetActual`, `TieneVistaRtsGuardada`, `ZoomAtLimit`, `RtsTargetHeight`, `RtsHeight`, `RtsFocus`

## `Assets/_Project/Scripts/Combat/Granada.cs`

**Tipos:** `class Granada`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `VelocidadHacia` | static | `Vector3` | `Vector3 origen, Vector3 objetivo, float rapidez = VelocidadDeLanzamien` | 50 |
| `Simular` | static | `int` | `Vector3 origen, Vector3 v, Transform ignorar, List<Vector3> puntos, ou` | 65 |
| `Sujetar` |  | `void` | — | 115 |
| `Soltar` |  | `void` | — | 122 |
| `Lanzar` | static | `Granada` | `Vector3 origen, Vector3 v, Soldier dueno` | 129 |
| `ResetearContadores` | static | `void` | — | 147 |

**Propiedades:** `Lanzadas`, `Explotadas`, `DuenoId`, `Sujeta`

## `Assets/_Project/Scripts/Combat/Health.cs`

**Tipos:** `class Health`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `RestaurarVida` |  | `void` | `int actual` | 58 |
| `Initialize` |  | `void` | `int actorId, int max` | 65 |
| `Tick` |  | `void` | `float dt` | 87 |
| `TakeDamage` |  | `void` | `int amount, int attackerId` | 127 |
| `TakeDamage` |  | `void` | `int amount, int attackerId, bool headshot` | 133 |
| `Heal` |  | `void` | `int amount` | 180 |

**Propiedades:** `MaxHealth`, `Current`, `IsAlive`, `ActorId`, `IsRegenerating`, `LastAttackerId`, `Invulnerable`, `FactorDeDano`

## `Assets/_Project/Scripts/Combat/HitboxCabeza.cs`

**Tipos:** `class HitboxCabeza`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `De` | static | `HitboxCabeza` | `Soldier s` | 61 |
| `Toca` |  | `bool` | `Vector3 punto, float radio` | 117 |
| `Cruza` |  | `bool` | `Vector3 desde, Vector3 hasta, float margen, out Vector3 punto` | 129 |

**Propiedades:** `Caja`, `Centro`

## `Assets/_Project/Scripts/Combat/Projectile.cs`

**Tipos:** `class Projectile`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Configure` |  | `void` | `ProjectilePool owningPool, Vector3 position, Vector3 direction, int sh` | 122 |
| `OnSpawn` |  | `void` | — | 223 |
| `OnDespawn` |  | `void` | — | 231 |
| `Tick` |  | `void` | `float dt` | 249 |
| `ExplodeAt` | static | `void` | `Vector3 point, float radius, int damage, int ownerId, TeamId? spareTea` | 752 |

**Propiedades:** `Gravity`, `Velocity`, `PoolGeneration`, `EsDeVehiculoEnemigo`, `DanoDeExplosionEnCurso`, `HeadshotsDelJugador`, `UltimoImpactoEnSoldado`, `UltimoImpactoFueCabeza`

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

## `Assets/_Project/Scripts/Combat/TeamCombatState.cs`

**Tipos:** `class TeamCombatState`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Init` | static | `void` | — | 11 |
| `Tick` | static | `void` | `float dt` | 18 |

**Propiedades:** `SegundosSinAccion`

## `Assets/_Project/Scripts/Combat/WeaponCatalog.cs`

**Tipos:** `class WeaponCatalog`, `struct Spec`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Get` | static | `Spec` | `WeaponKind kind` | 35 |

## `Assets/_Project/Scripts/Combat/WeaponHolder.cs`

**Tipos:** `struct WeaponChangedEvent`, `class WeaponHolder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `SetEnfoque` |  | `void` | `float progreso01` | 110 |
| `SetApuntado` |  | `void` | `float a01` | 128 |
| `RestaurarMunicion` |  | `void` | `int cargador` | 157 |
| `CapturarEstadoDeArmas` |  | `string` | — | 161 |
| `RestaurarEstadoDeArmas` |  | `void` | `string s` | 176 |
| `RestaurarGranadas` |  | `void` | `int n` | 199 |
| `FijarReservaParaPrueba` |  | `void` | `WeaponKind k, int balas` | 202 |
| `ReservaDe` |  | `int` | `WeaponKind k` | 203 |
| `ReponerMunicion` |  | `void` | — | 233 |
| `AgregarMunicion` |  | `void` | `int cargadores = 1` | 251 |
| `MunicionCompleta` |  | `bool` | — | 268 |
| `Bootstrap` |  | `void` | — | 281 |
| `SetPool` |  | `void` | `ProjectilePool projectilePool` | 311 |
| `SetTuning` |  | `void` | `int weaponDamage, float cooldown` | 313 |
| `EquipWeapon` |  | `void` | `WeaponKind kind, int weaponDamage, float cooldown, Color color` | 321 |
| `EquipFromLoadout` |  | `void` | `int index` | 501 |
| `ConfigurarCargador` |  | `void` | `int tamano, float segundosDeRecarga` | 512 |
| `CambiarArmaPrincipal` |  | `bool` | `int direccion` | 526 |
| `CycleNext` |  | `void` | — | 545 |
| `CyclePrevious` |  | `void` | — | 546 |
| `CambiarASiguienteConMunicion` |  | `bool` | — | 567 |
| `TryMelee` |  | `bool` | — | 597 |
| `HayObjetivoDeCuchilloCerca` |  | `bool` | — | 639 |
| `Tick` |  | `void` | `float dt` | 656 |
| `TryFire` |  | `bool` | `Vector3 origin, Vector3 direction` | 697 |
| `TryFire` |  | `bool` | `Vector3 origin, Vector3 direction, float dispersionMinima` | 699 |
| `ApplySpread` | static | `Vector3` | `Vector3 direction, float maxDeg` | 752 |
| `SonarDesenfunde` |  | `void` | — | 818 |
| `SonarGatilloVacio` |  | `void` | — | 825 |
| `ConsumirGranada` |  | `bool` | — | 834 |
| `ReponerGranadas` |  | `void` | `int cantidad = GranadasMaximas` | 840 |
| `Reload` |  | `bool` | — | 845 |

**Propiedades:** `Enfoque01`, `Apuntado01`, `FactorApuntandoDelArma`, `SpreadDegEfectivo`, `SpreadFraction01`, `CooldownRemaining`, `CurrentWeaponKind`, `CurrentLoadoutIndex`, `ReadinessFraction01`, `CurrentAmmo`, `MagazineSize`, `IsReloading`, `ReloadRemaining`, `UsaReservas`, `ReservaActual`, `SinMunicionTotal`, `MunicionTotal`, `KnifeCooldownRemaining`, `UltimoClipDeRecarga`, `UltimaRecargaFue2D`, `Granadas`

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
| `Configure` |  | `void` | `WeaponKind weaponKind, Color weaponColor` | 49 |
| `EquipOn` |  | `void` | `WeaponHolder holder, int soldierId` | 55 |

**Propiedades:** `Kind`, `Color`

## `Assets/_Project/Scripts/Core/ActorRegistry.cs`

**Tipos:** `class ActorRegistry`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Register` | static | `void` | `Soldier soldier` | 32 |
| `Unregister` | static | `void` | `Soldier soldier` | 42 |
| `Clear` | static | `void` | — | 52 |
| `Invalidate` | static | `void` | — | 66 |
| `EnsureAllRegistered` | static | `void` | — | 96 |
| `Rebarrer` | static | `void` | — | 106 |
| `CountAlive` | static | `int` | `TeamId team` | 121 |
| `CountDead` | static | `int` | `TeamId team` | 130 |
| `FindById` | static | `Soldier` | `int id` | 139 |
| `FindNearest` | static | `Soldier` | `Vector3 point, Func<Soldier, bool> predicate` | 144 |
| `FindNearestEnemyInRange` | static | `Soldier` | `Vector3 point, TeamId excludeTeam, float range` | 165 |

**Propiedades:** `All`

## `Assets/_Project/Scripts/Core/ApoyoEnElPiso.cs`

**Tipos:** `class ApoyoEnElPiso`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ApoyarATodos` | static | `int` | — | 53 |
| `Apoyar` | static | `bool` | `Transform cuerpo` | 68 |

## `Assets/_Project/Scripts/Core/AyudaDebugger.cs`

**Tipos:** `class AyudaDebugger`

**Propiedades:** `Instancia`, `CartelVisible`

## `Assets/_Project/Scripts/Core/BuscarHijo.cs`

**Tipos:** `class BuscarHijo`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Ruta` | static | `Transform` | `Transform raiz, string ruta` | 10 |

## `Assets/_Project/Scripts/Core/CamaraPrincipal.cs`

**Tipos:** `class CamaraPrincipal`

**Propiedades:** `Actual`

## `Assets/_Project/Scripts/Core/Coberturas.Puntuada.cs`

**Tipos:** `class CoberturasPuntuadas`, `struct Eleccion`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Presion` | static | `float` | `float vida01, bool recargando, int amenazas, float distanciaAlBlanco, ` | 58 |
| `AmenazasSobre` | static | `int` | `Soldier quien, float radio = 26f` | 66 |
| `Caracteristicas` | static | `int` | `int version` | 82 |
| `Usa` | static | `bool` | `int version, int caracteristica` | 108 |
| `TryElegir` | static | `bool` | `Vector3 desde, Soldier objetivo, Soldier quien, float radio, float ran` | 114 |

**Propiedades:** `Evaluaciones`

## `Assets/_Project/Scripts/Core/Coberturas.cs`

**Tipos:** `class Coberturas`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Solidos` | static | `List<Collider>` | — | 43 |
| `Registrar` | static | `int` | — | 67 |
| `Vigente` | static | `bool` | `Collider dueno` | 118 |
| `TryPuntoApuntado` | static | `bool` | `Vector3 puntoApuntado, Transform obstaculo, float radioPiso, out Vecto` | 128 |
| `IndicesCercanos` | static | `List<int>` | `Vector3 centro, int cantidad, float radio` | 152 |
| `FrenteDe` | static | `Vector3` | `int indice` | 175 |
| `FrenteDe` | static | `Vector3` | `Vector3 punto, Collider dueno` | 185 |
| `TryCercano` | static | `bool` | `Vector3 p, float radio, out Vector3 punto, out Collider dueno` | 195 |
| `Limpiar` | static | `void` | — | 198 |
| `TryMejorCobertura` | static | `bool` | `Vector3 desde, Soldier objetivo, Soldier quien, float radioMaximo, flo` | 211 |
| `TryMejorCobertura` | static | `bool` | `Vector3 desde, Soldier objetivo, Soldier quien, float radioMaximo, flo` | 222 |
| `TryCoberturaDeTiro` | static | `bool` | `Vector3 desde, Soldier objetivo, Soldier quien, float radio, float min` | 266 |
| `HayLineaDeTiroDesde` | static | `bool` | `Vector3 punto, Soldier objetivo, Soldier quien` | 295 |
| `PuntoDeTiro` | static | `Vector3` | `Soldier s` | 309 |
| `HayLineaDeTiroAlPecho` | static | `bool` | `Vector3 punto, Soldier objetivo, Soldier quien` | 316 |
| `MostrarMarcas` | static | `void` | `bool visibles` | 342 |

**Propiedades:** `Puntos`, `Duenos`, `Cantidad`, `Version`, `MarcasVisibles`

## `Assets/_Project/Scripts/Core/ComandosDeDepuracion.cs`

**Tipos:** `class ComandosDeDepuracion`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Matar` | static | `string` | `int id = -1` | 14 |
| `Revivir` | static | `string` | — | 28 |

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
| `Datos` | static | `Perfil` | `NivelDificultad n` | 62 |
| `Resumen` | static | `string` | `NivelDificultad n` | 92 |
| `AplicarVida` | static | `int` | — | 102 |
| `AjustarVida` | static | `bool` | `Soldier s` | 109 |
| `MultiplicadorDeDano` | static | `float` | `int atacanteId, Health victima` | 121 |
| `FactorPorVidaDelJugador` | static | `float` | `float fraccion` | 150 |

**Propiedades:** `Activa`, `FuegoAmigoExplosivo`, `Actual`, `PerfilActual`

## `Assets/_Project/Scripts/Core/EventBus.cs`

**Tipos:** `class EventBus`, `class ActionDisposable`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ClearAll` |  | `void` | — | 110 |
| `Dispose` |  | `void` | — | 120 |

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
| `Line` | static | `void` | `string message` | 22 |
| `Clear` | static | `void` | — | 30 |

## `Assets/_Project/Scripts/Core/IluminacionTactica.cs`

**Tipos:** `class IluminacionTactica`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Registrar` | static | `void` | `Light l` | 31 |
| `Quitar` | static | `void` | `Light l` | 36 |
| `EstaIluminado` | static | `bool` | `Vector3 p` | 41 |
| `VisionContra` | static | `float` | `Vector3 p, float vision` | 67 |

**Propiedades:** `CantidadDeLuces`

## `Assets/_Project/Scripts/Core/InteligenciaDeEnemigos.cs`

**Tipos:** `class InteligenciaDeEnemigos`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Revelar` | static | `void` | `int enemigoId` | 48 |
| `EstaRevelado` | static | `bool` | `int enemigoId` | 49 |

## `Assets/_Project/Scripts/Core/Loc.cs`

**Tipos:** `enum Idioma`, `class Loc`, `class LocTraductor`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Poner` | static | `void` | `Idioma i` | 91 |
| `Alternar` | static | `void` | — | 99 |
| `T` | static | `string` | `string textoEs` | 131 |
| `TieneEntrada` | static | `bool` | `string textoEs` | 132 |
| `Ingles` | static | `string` | `string textoEs` | 133 |
| `Recorrer` | static | `void` | — | 163 |

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

## `Assets/_Project/Scripts/Core/NavMeshViva.cs`

**Tipos:** `class NavMeshViva`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Solicitar` | static | `void` | — | 57 |
| `Tick` | static | `void` | `float dt` | 65 |

**Propiedades:** `Version`, `Pedidos`, `UltimaDuracion`, `Rehaciendo`

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
| `TryFindDetour` | static | `bool` | `Vector3 from, Vector3 to, List<Vector3> result` | 200 |

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

## `Assets/_Project/Scripts/Core/PartidaGuardada.cs`

**Tipos:** `class PartidaGuardadaDatos`, `class PartidaGuardada`, `struct Info`, `class IndicadorDeGuardado`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `RutaDe` | static | `string` | `string escena` | 64 |
| `UltimaInfo` | static | `Info` | `string escena = EscenaDeLaOperacion` | 75 |
| `HayPartida` | static | `bool` | `string escena = EscenaDeLaOperacion` | 104 |
| `Cargar` | static | `PartidaGuardadaDatos` | `string escena = EscenaDeLaOperacion` | 106 |
| `Borrar` | static | `void` | `string escena = EscenaDeLaOperacion` | 108 |
| `MotivoDeBloqueo` | static | `string` | — | 123 |
| `Guardar` | static | `bool` | `string motivo` | 140 |
| `GuardarManual` | static | `string` | — | 184 |
| `AutoGuardar` | static | `bool` | `string motivo` | 193 |
| `Continuar` | static | `bool` | `string escena = EscenaDeLaOperacion` | 203 |
| `ContinuarSinPantallaDeCarga` | static | `bool` | `string escena = EscenaDeLaOperacion` | 215 |
| `ProgramarRestauracion` | static | `void` | — | 229 |
| `Mostrar` | static | `void` | `string t, float segundos, bool ok` | 278 |

**Propiedades:** `Restaurando`, `PendienteDeRestaurar`, `UltimoAviso`, `UltimoMotivoGuardado`, `GuardadosHechos`, `GuardadosPorMotivoAuto`, `CarpetaDePartidas`, `Texto`, `HayPendiente`, `TextoActual`, `Visible`

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

## `Assets/_Project/Scripts/Core/ReporteDeBug.cs`

**Tipos:** `class ReporteDeBug`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `ReporteDeBug` | — | 35 |
| `Abrir` | static | `void` | — | 51 |
| `InicioDePalabraAnterior` | static | `int` | `string s, int fin` | 128 |
| `Confirmar` | static | `int` | `string nota` | 138 |
| `Cancelar` | static | `void` | — | 147 |

**Propiedades:** `Instancia`, `Abierto`, `NumeroPendiente`, `CapturaPendiente`

## `Assets/_Project/Scripts/Core/SceneLoader.cs`

**Tipos:** `class SceneLoader`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Cargar` | static | `void` | `string escenaDestino` | 17 |

**Propiedades:** `Destino`

## `Assets/_Project/Scripts/Core/SesionLog.cs`

**Tipos:** `interface IEstadoGuardable`, `class SnapSoldado`, `class SnapVehiculo`, `class SnapClave`, `class SnapJugador`, `class EstadoDeSesion`, `class SesionLog`, `class CulturaInvariante`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Dispose` |  | `void` | — | 142 |
| `Evento` | static | `void` | `string texto` | 145 |
| `Orden` | static | `void` | `string que, IEnumerable<Soldier> destinatarios, string detalle = null` | 151 |
| `Orden` | static | `void` | `string que, Soldier destinatario, string detalle = null` | 164 |
| `Capturar` |  | `EstadoDeSesion` | `string nota = ""` | 445 |
| `MarcarBug` | static | `int` | `string nota = ""` | 650 |
| `MarcarBugConCaptura` | static | `int` | `string nota, Texture2D captura, EstadoDeSesion estado` | 657 |
| `ArchivoDeBug` | static | `string` | `int n` | 762 |
| `RestaurarBug` | static | `string` | `int n` | 770 |
| `RestaurarArchivo` | static | `string` | `string ruta` | 777 |
| `Restaurar` | static | `string` | `EstadoDeSesion e` | 801 |

**Propiedades:** `Instancia`, `DialogoAbierto`, `ProximoNumeroDeBug`, `UltimoBug`, `UltimoArchivoBug`, `Carpeta`, `TextoIndicadorDeBug`

## `Assets/_Project/Scripts/Core/SpatialGrid.cs`

**Tipos:** `class SpatialGrid`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Rebuild` | static | `void` | — | 66 |
| `QueryInRange` | static | `void` | `Vector3 point, float range, List<Soldier> resultado, Func<Soldier, boo` | 133 |
| `FindNearestInRange` | static | `Soldier` | `Vector3 point, float range, Func<Soldier, bool> predicate` | 153 |

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

## `Assets/_Project/Scripts/Core/Vegetacion.cs`

**Tipos:** `class Vegetacion`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Invalidar` | static | `void` | — | 33 |
| `Dentro` | static | `bool` | `Vector3 p` | 41 |
| `DentroSinRegla` | static | `bool` | `Vector3 p` | 49 |
| `TryPuntoLibre` | static | `bool` | `Vector3 desde, out Vector3 libre` | 63 |
| `DespejarEnemigosIniciales` | static | `int` | `float maxMetros = 9f` | 160 |
| `Resumen` | static | `string` | — | 194 |

**Propiedades:** `Rige`, `HayMapa`

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

## `Assets/_Project/Scripts/Editor/AmbientacionDelNivel.cs`

**Tipos:** `class AmbientacionDelNivel`, `struct EstiloDeFarola`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Ambientar` | static | `void` | — | 46 |
| `RehacerNivelCompleto` | static | `void` | — | 79 |

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
| `CrearPrefabsDeProps` | static | `void` | — | 387 |
| `MontarSoldados` | static | `void` | `AnimatorController ctrl` | 462 |
| `PoblarEscena` | static | `void` | — | 747 |

## `Assets/_Project/Scripts/Editor/ArtSetup.cs`

**Tipos:** `class ArtSetup`, `struct Prop`, `class RecalibrarAlturaAlReimportar`, `class RecalibrarAlturaAlCargarDominio`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ConfigurarTodo` | static | `void` | — | 125 |
| `ConfigurarClipsDeSalto` | static | `void` | — | 463 |
| `HornearClipsDeSalto` | static | `void` | — | 781 |
| `Informe` | static | `void` | — | 946 |
| `InformeTexto` | static | `string` | — | 948 |

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

## `Assets/_Project/Scripts/Editor/AudioImportFix.cs`

**Tipos:** `class AudioImportFix`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Menu` | static | `void` | — | 26 |
| `Rutas` | static | `IEnumerable<string>` | `IEnumerable<string> carpetas` | 28 |
| `EsMono` | static | `bool` | `string ruta` | 43 |
| `Aplicar` | static | `string` | `bool tambienMundo = true` | 46 |

## `Assets/_Project/Scripts/Editor/AuditoriaDeColliders.cs`

**Tipos:** `class AuditoriaDeColliders`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Auditar` | static | `void` | — | 11 |

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
| `VestirEscenaAbierta` | static | `void` | — | 69 |
| `RehornearNavMesh` | static | `void` | — | 341 |
| `RepararCollidersDeArteBloque` | static | `void` | — | 355 |
| `GenerarCollidersDeArbolMenu` | static | `void` | — | 628 |
| `AgregarColliderYDestruccion` | static | `void` | `GameObject inst, bool esDestructible, int vida, bool esBarril` | 1112 |

## `Assets/Editor/BuildAndPack.cs`

**Tipos:** `class BuildAndPack`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Run` | static | `void` | — | 11 |

## `Assets/_Project/Scripts/Editor/ChecksBugs065.Wp11.cs`

**Tipos:** `class ChecksBugs065`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `CapturaDesde` | static | `string` | `Vector3 pos, Vector3 mira, string archivo, int w = 1600, int h = 900, ` | 22 |
| `CapturaConHud` | static | `string` | `string archivo, int w = 1920, int h = 1080` | 27 |

## `Assets/_Project/Scripts/Editor/ChecksBugs065.Wp4.cs`

**Tipos:** `class ChecksBugs065`, `class W80Res`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Wp4Bug080Antes` | static | `string` | — | 161 |
| `Wp4BancoIniciar` | static | `string` | `string versiones, int trials, int rival` | 466 |
| `Wp4BancoInforme` | static | `string` | — | 480 |

## `Assets/_Project/Scripts/Editor/ChecksBugs065.Wp6.cs`

**Tipos:** `class ChecksBugs065`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `CorrerUno` | static | `string` | `string nombre` | 27 |

## `Assets/_Project/Scripts/Editor/ChecksBugs065.Wp7.cs`

**Tipos:** `class ChecksBugs065`

**Propiedades:** `Wp7Reales`

## `Assets/_Project/Scripts/Editor/ChecksBugs065.cs`

**Tipos:** `class EscenaDelCheckAttribute`, `class ChecksBugs065`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Registrados` | static | `int[]` | — | 84 |
| `Fin` | static | `void` | `string texto` | 92 |
| `Esperar` | static | `IEnumerable` | `float segundosReales` | 96 |
| `Correr` | static | `string` | `int bug` | 123 |
| `CorrerTodos` | static | `string` | `int desde = 0, int hasta = 132, bool aislar = true` | 159 |
| `CorrerLista` | static | `string` | `string nombres, bool aislar = true` | 175 |

**Propiedades:** `Estado`, `Informe`, `Resultado`

## `Assets/_Project/Scripts/Editor/ChecksBugs104.Arte.cs`

**Tipos:** `class ChecksBugs065`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Iniciar109` | static | `string` | `int tipo` | 91 |
| `Render109` | static | `string` | `string prefijo` | 102 |

## `Assets/_Project/Scripts/Editor/ChecksBugs104.Cuartel.cs`

**Tipos:** `class ChecksBugs065`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Medir104` | static | `string` | `string nombre = "Enemigo_Artillero_TV2"` | 39 |
| `Medir104Anillo` | static | `string` | `string nombre = "Enemigo_Artillero_TV2", float radio = 35f` | 74 |

## `Assets/_Project/Scripts/Editor/CinematicaIntroBuilder.cs`

**Tipos:** `class CinematicaIntroBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Construir` | static | `void` | — | 19 |
| `ActualizarSubtitulos` | static | `string` | — | 82 |

## `Assets/_Project/Scripts/Editor/CinematicaSonda.cs`

**Tipos:** `class CinematicaSonda`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Iniciar` | static | `string` | — | 18 |

**Propiedades:** `Informe`

## `Assets/_Project/Scripts/Editor/CliBuilder.cs`

**Tipos:** `class CliBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `BuildWindows64` | static | `void` | — | 13 |

## `Assets/_Project/Scripts/Editor/CoberturaBench.cs`

**Tipos:** `class CoberturaBench`, `struct Trial`, `class Seguimiento`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Correr` | static | `void` | — | 47 |
| `Iniciar` | static | `void` | — | 54 |
| `Abortar` | static | `void` | — | 61 |

**Propiedades:** `Corriendo`, `Informe`, `Progreso`

## `Assets/_Project/Scripts/Editor/EstrategiaDelNivel.cs`

**Tipos:** `class EstrategiaDelNivel`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Construir` | static | `void` | — | 30 |

## `Assets/_Project/Scripts/Editor/FondoDelMundoBuilder.cs`

**Tipos:** `class FondoDelMundoBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Construir` | static | `void` | — | 28 |

## `Assets/Editor/HeadlessBuilder.cs`

**Tipos:** `class HeadlessBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ResolveBuildPath` | static | `string` | — | 16 |
| `SetBuildPath` | static | `void` | `string path` | 23 |
| `BuildWebGL` | static | `void` | — | 30 |
| `BuildWebGLDesdeEditor` | static | `string` | — | 50 |

## `Assets/_Project/Scripts/Editor/HeadlessTestRunner.Fase23.cs`

**Tipos:** `class HeadlessTestRunner`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `RunPhase23` | static | `void` | `SP.Player.PlayerInputDriver inputDriver, SP.Vehicles.Vehicle vehicle, ` | 10 |

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

## `Assets/Editor/HeliFix.cs`

**Tipos:** `class HeliFix`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Fix` | static | `void` | — | 4 |

## `Assets/_Project/Scripts/Editor/LevelBlockoutBuilder.cs`

**Tipos:** `class LevelBlockoutBuilder`, `struct Bloque`, `struct Ronda`, `struct DatosTanque`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TerrenoPrincipal` | static | `Terrain` | — | 53 |
| `Construir` | static | `void` | — | 283 |

## `Assets/_Project/Scripts/Editor/LoadingSceneBuilder.cs`

**Tipos:** `class LoadingSceneBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `BuildLoadingScene` | static | `void` | — | 25 |
| `RegisterInBuildSettings` | static | `void` | — | 176 |

## `Assets/_Project/Scripts/Editor/MapasDeCarga.cs`

**Tipos:** `class MapasDeCarga`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `RutaPng` | static | `string` | `string escena` | 22 |
| `RutaJson` | static | `string` | `string escena` | 23 |
| `GenerarGameplayMenu` | static | `void` | — | 26 |
| `GenerarOperacionMenu` | static | `void` | — | 29 |
| `Generar` | static | `string` | `string escena` | 32 |

## `Assets/_Project/Scripts/Editor/MedidorDeFases.cs`

**Tipos:** `class MedidorDeFases`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Materiales` | static | `string` | — | 28 |
| `Origenes` | static | `string` | `bool encender` | 50 |
| `InformeOrigenes` | static | `string` | `int top = 25` | 57 |
| `Iniciar` | static | `string` | `string nombre` | 68 |
| `Cerrar` | static | `string` | — | 114 |
| `Entorno` | static | `string` | — | 133 |

## `Assets/_Project/Scripts/Editor/MenuSceneBuilder.cs`

**Tipos:** `class MenuSceneBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `BuildMenuScene` | static | `void` | — | 20 |
| `RegisterScenesInBuildSettings` | static | `void` | — | 183 |

## `Assets/_Project/Scripts/Editor/MinimapIconBakePipeline.cs`

**Tipos:** `class MinimapIconBakePipeline`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Hornear` | static | `void` | — | 28 |

## `Assets/_Project/Scripts/Editor/MisionBuilder.cs`

**Tipos:** `class MisionBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Construir` | static | `void` | — | 17 |

## `Assets/_Project/Scripts/Editor/MissionFlowTestRunner.cs`

**Tipos:** `class MissionFlowTestRunner`, `class MissionFlowTestBehaviour`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ProbarFlujoCompleto` | static | `void` | — | 28 |

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
| `PonerDeNoche` | static | `void` | — | 28 |

## `Assets/_Project/Scripts/Editor/OperacionBuilder.Ambiente.cs`

**Tipos:** `class OperacionBuilder`, `class MallaGen`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Tri` |  | `void` | `Vector3 a, Vector3 b, Vector3 c, Vector3 centro, bool dosCaras = false` | 45 |
| `Quad` |  | `void` | `Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 centro, bool dosCa` | 53 |
| `Tubo` |  | `void` | `Vector3 p0, Vector3 p1, float r0, float r1, int lados` | 59 |
| `Crear` |  | `Mesh` | `string nombre` | 75 |
| `Ambiente` | static | `void` | `Transform op, Transform[] ruta` | 260 |

**Propiedades:** `ResumenAmbiente`

## `Assets/_Project/Scripts/Editor/OperacionBuilder.Arte.cs`

**Tipos:** `class OperacionBuilder`, `struct EspecDeTextura`, `class CeldaTrim`, `class MapaTrim`, `class RuidoT`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Octava` |  | `float` | `int o, float u, float v, int estirarU = 1, int estirarV = 1` | 198 |
| `Fbm` |  | `float` | `float u, float v` | 207 |

## `Assets/_Project/Scripts/Editor/OperacionBuilder.CierreCiudad.cs`

**Tipos:** `class OperacionBuilder`

**Propiedades:** `EdificiosDeCierre`

## `Assets/_Project/Scripts/Editor/OperacionBuilder.Ciudad.cs`

**Tipos:** `class OperacionBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `AmpliarCiudadEnEscena` | static | `void` | — | 34 |

## `Assets/_Project/Scripts/Editor/OperacionBuilder.CiudadDeCubos.cs`

**Tipos:** `class OperacionBuilder`, `class OcupacionCc`, `class MallaCc`, `class RegionesCc`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Rect` |  | `void` | `float x0, float z0, float x1, float z1, float margen` | 42 |
| `Disco` |  | `void` | `float x, float z, float radio` | 47 |
| `Segmento` |  | `void` | `Vector2 p, Vector2 q, float radio` | 57 |
| `Libre` |  | `bool` | `float x0, float z0, float x1, float z1` | 63 |
| `Ocupadas` |  | `int` | — | 69 |
| `OcuparFuera` |  | `void` | `float x0, float z0, float x1, float z1` | 71 |
| `Quad` |  | `void` | `Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal` | 158 |
| `Plano` |  | `void` | `float x, float y, float z, float w, float d, float yaw` | 167 |
| `Caja` |  | `void` | `float x, float y0, float z, float sx, float sy, float sz, float yaw` | 174 |
| `Construir` |  | `Mesh` | `string nombre` | 185 |
| `De` |  | `MallaCc` | `float x, float z, string mat` | 198 |
| `ReconstruirCiudadDeCubos` | static | `void` | — | 266 |
| `CiudadDeCubos` | static | `void` | `Transform padre` | 277 |

**Propiedades:** `ResumenCiudadDeCubos`, `EdificiosDeCubos`

## `Assets/_Project/Scripts/Editor/OperacionBuilder.Decals.cs`

**Tipos:** `class OperacionBuilder`

**Propiedades:** `ResumenDecals`

## `Assets/_Project/Scripts/Editor/OperacionBuilder.Decorado.cs`

**Tipos:** `class OperacionBuilder`

**Propiedades:** `ResumenDecorado`

## `Assets/_Project/Scripts/Editor/OperacionBuilder.Mando.cs`

**Tipos:** `class OperacionBuilder`

**Propiedades:** `SacosDeMando`

## `Assets/_Project/Scripts/Editor/OperacionBuilder.cs`

**Tipos:** `class OperacionBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Construir` | static | `void` | — | 300 |

## `Assets/_Project/Scripts/Editor/OperacionPrueba.cs`

**Tipos:** `class OperacionPrueba`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Arrancar` | static | `string` | `int objetivo, int puesto = 1, int subfase = 0` | 37 |
| `Resumen` | static | `string` | — | 55 |
| `Recorrer` | static | `void` | `float segundosPorObjetivo = 4f` | 74 |

**Propiedades:** `Corriendo`, `Informe`, `Progreso`

## `Assets/_Project/Scripts/Editor/PielSoldadoAnalisis.cs`

**Tipos:** `class PielSoldadoAnalisis`, `enum Cadena`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `NombresDeHueso` | static | `string[]` | — | 19 |
| `CadenaDe` | static | `Cadena` | `string hueso` | 29 |
| `EsAntebrazoOMano` | static | `bool` | `string hueso` | 41 |
| `EsCadera` | static | `bool` | `string hueso` | 42 |
| `CarasMezcladas` | static | `int` | `Mesh m, string[] n, out List<int> vertices` | 72 |
| `Sanear` | static | `int` | `Mesh m, string[] n` | 105 |
| `Diagnostico` | static | `string` | `string nombreMalla` | 226 |
| `Islas` | static | `List<List<int>>` | `Mesh m` | 250 |

## `Assets/_Project/Scripts/Editor/PlaymodeCleanup.cs`

**Tipos:** `class PlaymodeCleanup`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `BarrerMaterialesHuerfanos` | static | `int` | — | 88 |
| `BarrerHuerfanos` | static | `void` | — | 112 |

## `Assets/_Project/Scripts/Editor/PrimerFramePreview.cs`

**Tipos:** `class PrimerFramePreview`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Aplicar` | static | `void` | — | 29 |
| `InicializarUiInferior` | static | `void` | — | 115 |

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
| `SimplificarConIconos` | static | `void` | — | 103 |

## `Assets/_Project/Scripts/Editor/RutasDelNivel.cs`

**Tipos:** `enum TipoDeRuta`, `struct RutaDelNivel`, `class RutasDelNivel`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Muestrear` |  | `void` | `float d, out Vector2 punto, out Vector2 tangente` | 36 |
| `DistanciaABorde` | static | `float` | `float x, float z, out int indice` | 97 |
| `DistanciaABorde` | static | `float` | `float x, float z` | 114 |
| `DistanciaABorde` | static | `float` | `float x, float z, TipoDeRuta tipo` | 117 |
| `DistSegmento` | static | `float` | `Vector2 p, Vector2 a, Vector2 b` | 130 |

**Propiedades:** `Largo`

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
| `GenerarTodas` | static | `void` | — | 62 |

## `Assets/_Project/Scripts/Editor/SondaDeRendimiento.cs`

**Tipos:** `class SondaDeRendimiento`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Iniciar` | static | `string` | — | 19 |
| `Detener` | static | `string` | — | 27 |
| `Picos` | static | `string` | `int cuantos = 6, int porFrame = 10, int maxFrames = 400` | 34 |
| `Informe` | static | `string` | `int top = 30, int maxFrames = 300` | 71 |
| `Arbol` | static | `string` | `double minMs = 3, int cuantos = 2, int maxFrames = 400, int maxLineas ` | 118 |

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

## `Assets/_Project/Scripts/Editor/TexturasDeTerreno.cs`

**Tipos:** `class TexturasDeTerreno`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Grava` | static | `TerrainLayer` | — | 18 |
| `Hojarasca` | static | `TerrainLayer` | — | 28 |

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

## `Assets/_Project/Scripts/Editor/ValidacionBugs.cs`

**Tipos:** `class ValidacionBugs`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Iniciar` | static | `string` | `int numero, bool capturas = false` | 47 |

**Propiedades:** `Estado`, `UltimoInforme`

## `Assets/_Project/Scripts/Editor/ValidadorJugabilidad.cs`

**Tipos:** `class ValidadorJugabilidad`, `class Fs`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Iniciar` | static | `string` | `int desde = 1, int hasta = 6, bool encadenar = true, bool modoDios = f` | 70 |
| `Detener` | static | `void` | — | 90 |

**Propiedades:** `Corriendo`, `Estado`, `Resultado`

## `Assets/_Project/Scripts/Editor/WeaponPrefabBuilder.cs`

**Tipos:** `class WeaponPrefabBuilder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `BuildAll` | static | `void` | — | 33 |

## `Assets/_Project/Scripts/Editor/WeaponStatusUiPipeline.cs`

**Tipos:** `class WeaponStatusUiPipeline`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Simplificar` | static | `void` | — | 29 |
| `ConstruirLayoutCompleto` | static | `void` | `GameObject panel` | 53 |

## `Assets/_Project/Scripts/Editor/WorldArtPipeline.cs`

**Tipos:** `class WorldArtPipeline`, `enum Volumen`, `struct ReglaCategoria`, `struct Plantado`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ImportarTodo` | static | `void` | — | 64 |
| `ArmarPrefabs` | static | `void` | — | 231 |
| `ReemplazarEnEscena` | static | `void` | — | 315 |
| `AplicarVisualesVehiculo` | static | `void` | `GameObject veh` | 553 |
| `RepintarPisoEnEscena` | static | `void` | — | 641 |

## `Assets/_Project/Scripts/Interaction/RolRequerido.cs`

**Tipos:** `class RolRequerido`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Reiniciar` | static | `void` | — | 23 |
| `Nombre` | static | `string` | `RoleType rol` | 25 |
| `AliadoCon` | static | `Soldier` | `RoleType rol, Soldier excluir = null` | 39 |
| `NombreCorto` | static | `string` | `Soldier s` | 53 |
| `Texto` | static | `string` | `RoleType rol, string accion, Soldier yo` | 62 |
| `Avisar` | static | `bool` | `Soldier yo, RoleType rol, string accion = "INTERACTUAR CON ESTO"` | 71 |

**Propiedades:** `UltimoTexto`, `Cantidad`

## `Assets/_Project/Scripts/Mision/AntenaDeRadio.cs`

**Tipos:** `class AntenaDeRadio`

**Propiedades:** `Destruida`, `Activa`

## `Assets/_Project/Scripts/Mision/CinematicaDeIntro.cs`

**Tipos:** `class CinematicaDeIntro`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Iniciar` |  | `void` | `PlayerInputDriver driver, CinematicaIntroPath ruta, System.Action alTe` | 77 |
| `Saltar` |  | `void` | — | 88 |

**Propiedades:** `EnCurso`, `Terminada`, `WaypointActual`

## `Assets/_Project/Scripts/Mision/CinematicaDeVictoria.cs`

**Tipos:** `class CinematicaDeVictoria`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Iniciar` |  | `void` | `MisionDirector m, PlayerInputDriver driver, GameOutcomeController outc` | 36 |
| `EvitarObstruccion` | static | `Vector3` | `Vector3 mira, Vector3 posDeseada` | 81 |

**Propiedades:** `EnCurso`, `Terminada`, `SoldadosDeLaHorda`, `TracerasDisparadas`, `Tiempo`, `Plano`

## `Assets/_Project/Scripts/Mision/CinematicaIntroPath.cs`

**Tipos:** `class CinematicaIntroPath`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Reconectar` |  | `void` | — | 23 |

## `Assets/_Project/Scripts/Mision/EstadisticasDeMision.cs`

**Tipos:** `class EstadisticasDeMision`, `struct Fila`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Limpiar` |  | `void` | — | 60 |
| `Serializar` |  | `string` | — | 71 |
| `Restaurar` |  | `void` | `string s` | 83 |
| `Filas` | static | `List<Fila>` | `bool victoria` | 202 |

**Propiedades:** `Instancia`, `BajasPorMi`, `BajasPorAliados`, `BajasOtras`, `MisMuertes`, `CaidosDeLaEscuadra`, `Reanimaciones`, `VehiculosDestruidos`, `Disparos`, `Impactos`, `HeadshotsJugador`, `HeadshotsEscuadra`, `Inicio`, `BajasEnemigas`

## `Assets/_Project/Scripts/Mision/EstadoDePartida.cs`

**Tipos:** `enum CausaDeDerrota`, `class EstadoDePartida`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Reiniciar` | static | `void` | — | 41 |
| `Tick` | static | `void` | `float dt` | 48 |
| `RegistrarDerrotaExterna` | static | `void` | `CausaDeDerrota causa` | 109 |

**Propiedades:** `SinVivos`, `CalmaAcumulada`, `Revivio`, `Perdio`, `Causa`

## `Assets/_Project/Scripts/Mision/Helicoptero.cs`

**Tipos:** `class Helicoptero`, `class EstadoDePivote`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `RestaurarCobertura` |  | `void` | — | 48 |
| `SilenciarYa` |  | `void` | — | 109 |
| `ApagarSonido` |  | `void` | — | 111 |
| `RotorAFondo` |  | `void` | — | 119 |
| `PararRotor` |  | `void` | — | 120 |
| `Alerta` |  | `void` | `bool on` | 122 |

**Propiedades:** `Instancia`, `Vueltas`, `EnAlerta`, `DisparaCobertura`, `DanoDeCobertura`, `RadioDeBlancoActual`, `DispersionDeCobertura`, `FactorDePausa`, `DisparosHechos`, `Volando`, `Altura`, `PuntoDePiso`, `EmisionDePolvo`, `EmisionDeResiduos`, `ParticulasDePolvo`, `ParticulasDeResiduos`, `MaximoDePolvo`, `Ondas`, `Tripulacion`, `Pivotes`, `UltimoOrigenDeDisparo`, `UltimoPivoteQueDisparo`, `VolumenExtra`, `VolumenDelRotor`, `SonidoApagado`, `ClipDeRotor`

## `Assets/_Project/Scripts/Mision/MisionDirector.cs`

**Tipos:** `enum FaseDeMision`, `class MisionDirector`, `enum Carril`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `PosicionDelJugador` |  | `Vector3` | — | 177 |
| `PuntoObjetivoActual` |  | `Vector3` | — | 197 |
| `DistanciaAlObjetivo` |  | `float` | — | 208 |
| `CrearEnemigo` |  | `Soldier` | `string nombre, Vector3 pos, float yaw = 180f` | 274 |
| `CarrilDe` | static | `Carril` | `Vector3 p` | 456 |
| `Perder` |  | `void` | `string motivo` | 816 |
| `SaltarAFase` |  | `void` | `FaseDeMision f` | 834 |

**Propiedades:** `Instancia`, `Activo`, `Plaza`, `Helipuerto`, `RefugioDelCivil`, `Fase`, `Restante`, `Civil`, `CivilRescatado`, `Heli`, `OleadasLanzadas`, `SoldadosGenerados`, `TensionSonando`, `CarrilDeIda`, `CarrilCerrado`

## `Assets/_Project/Scripts/Mision/MisionHud.cs`

**Tipos:** `class MisionHud`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Crear` | static | `MisionHud` | `MisionDirector d` | 31 |
| `Refrescar` |  | `void` | — | 181 |

**Propiedades:** `TextoTitulo`, `TextoDetalle`, `TextoDificultad`

## `Assets/_Project/Scripts/Mision/PivoteMetralleta.cs`

**Tipos:** `class PivoteMetralleta`, `class CasquillosDeMetralleta`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `RumboHacia` |  | `float` | `Vector3 puntoMundo, out float cabeceo` | 41 |
| `EnArco` |  | `bool` | `float rumbo, float tolerancia = 0f` | 51 |
| `Apuntar` |  | `float` | `Vector3 puntoMundo, float dt` | 55 |
| `Reposo` |  | `void` | `float dt` | 67 |
| `Expulsar` | static | `void` | `Vector3 pos, Vector3 lado` | 142 |

**Propiedades:** `Boca`, `YawLocal`, `PitchLocal`, `YawDeReposo`, `CentroDeGiro`, `MaterialDeLaton`, `ColorDeEmision`, `Expulsados`, `Activos`

## `Assets/_Project/Scripts/Mision/TripulacionDelHeli.cs`

**Tipos:** `class TripulacionDelHeli`, `class PoseSentada`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `SentarCopia` |  | `bool` | `Soldier s` | 118 |
| `LimpiarCopias` |  | `void` | — | 130 |

**Propiedades:** `Artilleros`, `Sentados`, `SentadosBase`, `CopiasDeEscuadra`, `Armada`, `ListaDeArtilleros`, `ListaDeSentados`

## `Assets/_Project/Scripts/Operacion/AnilloDeAviso.cs`

**Tipos:** `class AnilloDeAviso`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Mostrar` | static | `AnilloDeAviso` | `Vector3 centro, float radio, float segundos, Color? color = null` | 42 |
| `CancelarTodos` | static | `void` | — | 50 |
| `Cancelar` |  | `void` | — | 186 |

**Propiedades:** `Activo`, `Progreso01`, `Edad`, `Duracion`, `Radio`, `Centro`, `Activos`, `Todos`

## `Assets/_Project/Scripts/Operacion/AnilloDeSubida.cs`

**Tipos:** `class AnilloDeSubida`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Mostrar` | static | `AnilloDeSubida` | `Transform objetivo, float radio = 3.4f` | 27 |
| `Ocultar` | static | `void` | — | 38 |

**Propiedades:** `Instancia`, `EstaActivo`, `EscalaActual`

## `Assets/_Project/Scripts/Operacion/ArtilleroDeTorreta.cs`

**Tipos:** `class ArtilleroDeTorreta`

**Propiedades:** `Rafagas`

## `Assets/_Project/Scripts/Operacion/Atrincherar.cs`

**Tipos:** `class Atrincherar`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Aplicar` | static | `int` | `IEnumerable<Soldier> enemigos, Vector3 amenaza` | 28 |
| `ArmarCoberturas` | static | `int` | `IList<(Vector3 punto, Vector3 haciaAmenaza` | 124 |

**Propiedades:** `Barricadas`, `Atrincherados`

## `Assets/_Project/Scripts/Operacion/CargaExplosiva.cs`

**Tipos:** `class CargaExplosiva`, `enum Estado`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `EstaArmada` | static | `bool` | `CargaExplosiva c` | 65 |
| `Reloj` | static | `string` | `float segundos` | 103 |
| `GetPrompt` |  | `string` | `PlayerInputDriver player` | 123 |
| `CanInteract` |  | `bool` | `PlayerInputDriver player` | 124 |
| `Interact` |  | `void` | `PlayerInputDriver player` | 125 |
| `PuedeOperar` |  | `bool` | `Soldier s` | 134 |
| `ElegirOperador` |  | `Soldier` | `IList<Soldier> candidatos` | 136 |
| `EnviarA` |  | `bool` | `Soldier s, out string motivo` | 159 |
| `Avanzar` |  | `void` | `float dt, Soldier quien = null` | 275 |
| `PlantarPorPrueba` |  | `bool` | `Soldier s` | 283 |
| `VolarYa` |  | `void` | — | 424 |
| `AcortarMecha` |  | `void` | `float restante` | 439 |
| `RestaurarMecha` |  | `void` | `float restante` | 445 |

**Propiedades:** `Situacion`, `Progreso01`, `MechaRestante`, `Habilitado`, `Plantador`, `Asignado`, `EstaPlantada`, `EstaVolada`, `UltimoMensaje`, `Bips`, `Armadas`, `Posicion`, `RadioDePeligro`, `Titulo`, `TextoDeEstado`, `NombreParaOrden`, `QuienDebe`, `VerboDeUso`, `Raiz`

## `Assets/_Project/Scripts/Operacion/CazaDeEnemigos.cs`

**Tipos:** `class CazaDeEnemigos`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `EsCazador` | static | `bool` | `Soldier s` | 31 |
| `Reiniciar` | static | `void` | — | 34 |
| `Marcar` | static | `void` | `IEnumerable<Soldier> soldados` | 36 |
| `Tick` | static | `void` | `float dt` | 62 |

**Propiedades:** `Cazadores`, `RumbosDados`

## `Assets/_Project/Scripts/Operacion/CineDeHuida.cs`

**Tipos:** `class CineDeHuida`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Abrir` | static | `CineDeHuida` | `bool bloquear = true` | 38 |
| `Cancelar` | static | `void` | — | 50 |
| `Cerrar` |  | `void` | — | 55 |
| `Lento` |  | `void` | `float escala` | 104 |
| `TiempoNormal` |  | `void` | — | 111 |
| `Barras` |  | `void` | `float k` | 219 |
| `TarjetaDeJefe` |  | `void` | `string nombre, string linea, float alfa` | 227 |
| `Subtitulo` |  | `void` | `string texto, float alfa` | 234 |
| `Cartel` |  | `void` | `string texto, float alfa, Color? color = null` | 241 |
| `Fundido` |  | `void` | `float alfa` | 249 |
| `PonerCamara` |  | `void` | `Vector3 pos, Quaternion rot, float fov = -1f` | 254 |

**Propiedades:** `Activa`, `Instancia`, `Camara`, `CampoDeVisionOriginal`, `TextoDelSubtitulo`, `TextoDelCartel`, `TextoDeLaTarjeta`, `AlturaDeBarras`, `EscalaDeTiempoPropia`

## `Assets/_Project/Scripts/Operacion/CinematicaDeOperacion.cs`

**Tipos:** `class CinematicaDeOperacion`, `struct Clave`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Iniciar` | static | `CinematicaDeOperacion` | `OperacionDirector director` | 81 |
| `Saltar` | static | `void` | — | 93 |
| `ReiniciarPorPrueba` | static | `CinematicaDeOperacion` | — | 102 |

**Propiedades:** `Activa`, `IntroActiva`, `Instancia`, `Paso`, `Tiempo`, `Duracion`, `ProgresoDeSalto`, `TextoDelPanel`, `TextoDelCartel`, `TextoDelTitulo`, `EsperandoTecla`, `SaltadaPorElJugador`, `PosicionDeCamara`, `CantidadDeCuadrosClave`

## `Assets/_Project/Scripts/Operacion/CinematicaDeRapel.cs`

**Tipos:** `class CinematicaDeRapel`, `class Rapelista`, `class PoseDeRapel`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `VolumenDelRotorEn` | static | `float` | `float t` | 51 |
| `Iniciar` | static | `CinematicaDeRapel` | `OperacionDirector director, bool encadenarConLosPasos = true` | 105 |
| `Saltar` | static | `void` | — | 117 |
| `ReiniciarPorPrueba` | static | `CinematicaDeRapel` | `bool encadenarConLosPasos = false` | 125 |

**Propiedades:** `Activa`, `Instancia`, `Tiempo`, `Toma`, `ProgresoDeSalto`, `SaltadaPorElJugador`, `UltimaFueSaltada`, `SoldadosEnElSuelo`, `CuerdasVisibles`, `CopiasColgando`, `PosicionDeCamara`, `Camara`, `Escuadra`

## `Assets/_Project/Scripts/Operacion/CivilesDeAmbiente.cs`

**Tipos:** `class CivilesDeAmbiente`, `class Ruta`, `class Figura`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ColocarInicial` |  | `void` | — | 67 |

**Propiedades:** `ActivosAhora`, `MaximoActivosVisto`

## `Assets/_Project/Scripts/Operacion/EmboscadaDeCohetes.cs`

**Tipos:** `class CoheteDeAviso`, `class EmboscadaDeCohetes`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Lanzar` | static | `CoheteDeAviso` | `Vector3 desde, Vector3 hasta, float segundos, int dano, float radio, b` | 31 |
| `Armar` |  | `void` | `Vehicle tanque` | 105 |
| `Silenciar` |  | `void` | — | 125 |
| `Desarmar` |  | `void` | — | 126 |

**Propiedades:** `Lanzados`, `Impactos`, `Destino`, `Progreso01`, `Armada`, `CohetesLanzados`, `TiradoresVivos`, `TiradoresTotales`

## `Assets/_Project/Scripts/Operacion/FrancotiradorEnTorre.cs`

**Tipos:** `class TiradorDeTorre`, `class FrancotiradorEnTorre`, `enum Fase`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Pecho` | static | `Vector3` | `Soldier s` | 70 |

**Propiedades:** `Todos`, `Objetivo`, `Disparos`, `Activo`, `Boca`, `LaserActivo`, `LaserEncendidoDesde`, `UltimoDisparoT`, `LaserAntesDelUltimoTiro`, `Apuntando`

## `Assets/_Project/Scripts/Operacion/GuiaDeMando.cs`

**Tipos:** `class GuiaDeMando`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `GuiaDeMando` | — | 39 |

**Propiedades:** `Instancia`, `Activa`, `PasoGuiado`, `FlechaVisible`, `PosicionDeLaFlecha`, `RutaActiva`, `PuntosDeRuta`, `FinDeLaRuta`, `InicioDeLaRuta`, `AnchoDeLaRuta`

## `Assets/_Project/Scripts/Operacion/GuiaDeObjetivo.cs`

**Tipos:** `class GuiaDeObjetivo`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `GuiaDeObjetivo` | — | 46 |
| `MallaFlecha` | static | `Mesh` | — | 121 |
| `TexturaPunteado` | static | `Texture2D` | — | 122 |

**Propiedades:** `Instancia`, `Activa`, `RutaActiva`, `PuntosDeRuta`, `FlechaVisible`, `RomboVisible`, `TextoDelRombo`, `PosicionDelRomboEnPantalla`, `PosicionDeLaFlecha`, `FlechaDelPiesVisible`, `ObjetivoActual`

## `Assets/_Project/Scripts/Operacion/HelicopteroEnemigo.cs`

**Tipos:** `class HelicopteroEnemigo`, `enum Estado`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Iniciar` | static | `HelicopteroEnemigo` | `Transform plantilla, Vehicle tanque, float distanciaDeEntrada = 160f` | 55 |
| `FijarParaLaCinematica` |  | `void` | `Vector3 pos, Quaternion rot` | 91 |
| `ExplotarEnElAire` |  | `void` | — | 247 |

**Propiedades:** `Instancia`, `Creados`, `Vehiculo`, `Situacion`, `Pasadas`, `CohetesSoltados`, `BalasDeMetralla`, `Fraccion01`, `Derribado`, `Altura`, `VelocidadActual`, `InstantesDeAviso`

## `Assets/_Project/Scripts/Operacion/MandoTactico.cs`

**Tipos:** `class MandoTactico`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `EsOperadorDeRadio` | static | `bool` | `Soldier s` | 50 |
| `EsMiliciano` | static | `bool` | `Soldier s` | 51 |
| `EsParteDeLaRadio` | static | `bool` | `Collider c, Vector3 punto` | 67 |
| `Establecer` | static | `void` | `Soldier operador` | 77 |
| `Soltar` | static | `void` | — | 84 |
| `BloqueaElCambioDeVista` | static | `bool` | — | 99 |
| `BloqueaLaPosesion` | static | `bool` | — | 100 |
| `AliadoParaRelevar` | static | `Soldier` | `Soldier operador` | 103 |
| `RelojCorre` | static | `bool` | `int pasosCompletos, bool enRadio, bool algunSectorCaido, int amenazado` | 121 |
| `MotivoDePausa` | static | `string` | `int pasosCompletos, bool enRadio, bool algunSectorCaido, int amenazado` | 125 |
| `TextoDeRecuperar` | static | `string` | — | 135 |
| `DebeCaer` | static | `bool` | `float segundosVacio` | 138 |
| `AliadosNecesarios` | static | `int` | `int aliadosVivosSinOperador` | 141 |
| `SeRecupera` | static | `bool` | `int aliadosAdentro, int aliadosVivosSinOperador, float segundosCumplid` | 142 |

**Propiedades:** `Operador`, `UltimoOperador`, `RadioTomada`, `EnRadio`, `OperadorEsElJugador`

## `Assets/_Project/Scripts/Operacion/MenuDeTrampas.cs`

**Tipos:** `class MenuDeTrampas`

**Propiedades:** `Abierto`

## `Assets/_Project/Scripts/Operacion/OperacionAuto.Efectos.cs`

**Tipos:** `class OperacionAuto`

**Propiedades:** `GiroActual`, `EstallidoHecho`, `HuellasActivas`, `VolumenRuedas`

## `Assets/_Project/Scripts/Operacion/OperacionAuto.cs`

**Tipos:** `class OperacionAuto`, `enum Tactica`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `FijarVelocidadInicial` |  | `void` | `float v` | 33 |
| `Retirarse` |  | `void` | — | 154 |
| `ConfigurarAsalto` |  | `void` | `Vector3 punto` | 174 |

**Propiedades:** `Vehiculo`, `Muerto`, `Disparos`, `Embestidas`, `VelocidadActual`, `TacticaActual`, `Retirado`

## `Assets/_Project/Scripts/Operacion/OperacionDirector.Centro.cs`

**Tipos:** `class OperacionDirector`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `LugarLibreParaCamioneta` | static | `bool` | `Vector3 centro, Quaternion rot, Transform ignorar = null` | 86 |
| `BuscarLugarParaCamioneta` | static | `Vector3` | `Vector3 deseado, Quaternion rot, float soloLado = 0f, Transform ignora` | 104 |

**Propiedades:** `CamionetasDelCentroCreadas`, `CamionetasDelCentroVivas`, `ParadasCorridas`

## `Assets/_Project/Scripts/Operacion/OperacionDirector.Extraer.Cobertura.cs`

**Tipos:** `class OperacionDirector`

**Propiedades:** `CoberturaActiva`, `HeliBajando`, `SegundosDeCobertura`, `OleadasDeExtraer`, `EnemigosDeExtraer`, `BajasDelHeliEnExtraer`, `AliadosEnLaZona`, `AliadosAEvacuarTotal`, `EnemigosDeExtraerVivos`

## `Assets/_Project/Scripts/Operacion/OperacionDirector.Extraer.cs`

**Tipos:** `class OperacionDirector`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `SubirAlHeli` |  | `void` | — | 36 |

**Propiedades:** `HeridosRescatados`, `SubidosAlHeli`, `FundidoActivo`

## `Assets/_Project/Scripts/Operacion/OperacionDirector.Final.cs`

**Tipos:** `class OperacionDirector`

**Propiedades:** `Halcon`, `HalconesCreados`, `HalconesDerribados`, `FinalActiva`, `FinalTerminada`, `FinalSegundosLentos`, `FinalSegundosTotales`, `FinalOtroHelicoptero`, `FinalDisparo`, `FinalDisparoAutomatico`, `FinalEscalaMinima`, `FinalEnPieAlTerminar`, `FinalDistanciaMaximaDeLaEscuadra`, `FinalPosicionDelTanque`, `FinalSubtitulo`, `FinalSegundoDeInicio`, `EmbosCohetesLanzados`, `EmbosTiradoresVivos`

## `Assets/_Project/Scripts/Operacion/OperacionDirector.Guardado.cs`

**Tipos:** `class OperacionDirector`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ProgramarAutoguardado` |  | `void` | `string motivo, float demora = 1.5f` | 24 |
| `FijarRelojEfectivoParaPrueba` |  | `void` | `float s` | 64 |
| `AlRestaurarPartida` |  | `void` | `PartidaGuardadaDatos d` | 67 |

**Propiedades:** `MotivoAutoPendiente`, `AutoguardadoPendiente`

## `Assets/_Project/Scripts/Operacion/OperacionDirector.Huida.cs`

**Tipos:** `class OperacionDirector`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `PistaDeLanzacohetes` |  | `string` | `Soldier yo` | 45 |
| `Embarcar` |  | `void` | — | 71 |
| `InstalarMetralletaConIA` |  | `TurretAI` | — | 183 |

**Propiedades:** `AutosSimultaneosEfectivos`, `AutosVivosAhora`, `MaximoDeAutosVivos`, `EnLlegada`, `BajaronDelTanque`, `AutosPorDelante`

## `Assets/_Project/Scripts/Operacion/OperacionDirector.Infiltrar.cs`

**Tipos:** `class OperacionDirector`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TorresCaidas` |  | `int` | `TorreDestruible.Tipo tipo` | 70 |
| `LineaDeSubobjetivos` |  | `string` | `int vivos` | 82 |

**Propiedades:** `ReflectoresRotos`

## `Assets/_Project/Scripts/Operacion/OperacionDirector.Jefe.cs`

**Tipos:** `enum SubfaseHuida`, `class OperacionDirector`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `SaltearJefeYReparacion` |  | `void` | — | 389 |

**Propiedades:** `SubfaseDeHuida`, `Jefe`, `Reparacion`, `TomaDelJefeActiva`, `TomaDelJefeHecha`, `ReparacionCompletada`, `OleadasDeReparacionLanzadas`, `EnemigosDeLasOleadas`, `CamionetasDeReparacion`, `RelojDeSubfase`

## `Assets/_Project/Scripts/Operacion/OperacionDirector.Mando.cs`

**Tipos:** `class OperacionDirector`, `class OleadaDelMando`, `struct Vacio`, `struct GuiaDePaso`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TiempoDeLanzamiento` |  | `float` | `float total` | 91 |
| `Pendientes` |  | `int` | `OperacionDirector d` | 93 |
| `TomarLaRadioPorPrueba` |  | `void` | `bool ubicarSolos = false` | 177 |
| `OrdenarOperarLaRadio` |  | `bool` | `IReadOnlyList<Soldier> seleccion, out string motivo` | 413 |
| `RelevarOperador` |  | `void` | `Soldier nuevo` | 444 |
| `PuntosDeCoberturaDelSector` |  | `List<SectorDeDefensa.PuntoDeCobertura>` | `int i` | 834 |
| `GuiaDelPaso` |  | `bool` | `out GuiaDePaso g` | 1036 |

**Propiedades:** `MandoActivo`, `RelojEfectivo`, `RelojDelMandoCorre`, `MotivoDeLaPausa`, `SaltoDePrueba`, `UnidadA`, `UnidadC`, `MedicoDeApoyo`, `Milicianos`, `PasoHecho`, `PasoActualDelMando`, `CampanarioOcupado`, `SectoresCaidosTotales`, `CoberturasAutomaticas`, `OleadasDelMandoLanzadas`, `OleadasDelMandoAnunciadas`, `OleadasDelMando`, `ForzarHud`, `Vivos`, `VehiculosVivos`, `OperadorPendiente`, `ESostenidaParaSoltar`, `PendientesDelMando`, `AnillosDeGuia`

## `Assets/_Project/Scripts/Operacion/OperacionDirector.Puestos.cs`

**Tipos:** `class OperacionDirector`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ContarVolados` |  | `int` | — | 60 |
| `GuardiasDeLosPuestos` |  | `List<Soldier>` | — | 68 |
| `AlTerminarIntro` |  | `void` | — | 283 |

**Propiedades:** `ContraatacantesActivados`, `CamionetasDePuestosCreadas`, `CamionetasDePuestosVivas`

## `Assets/_Project/Scripts/Operacion/OperacionDirector.Resistir.cs`

**Tipos:** `class OperacionDirector`

**Propiedades:** `CoberturasDeLaPlaza`, `RefuerzosCreados`, `RefuerzosVivos`, `PendientesDeLaCiudad`, `LiberadosEscalonados`, `ProximaOleadaDeLaCiudadEn`, `FlechaHeli`, `VelocidadHeli`

## `Assets/_Project/Scripts/Operacion/OperacionDirector.TomaFinal.cs`

**Tipos:** `class OperacionDirector`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TiradoresDisparandoEnPantalla` |  | `int` | `float ventana = 1f` | 38 |

**Propiedades:** `TomaFinalActiva`, `CamaraFinal`, `TiradoresFinales`, `DisparosDeLaTomaFinal`, `FigurantesDeLaTomaFinal`, `DistanciaInicialCamaraHeli`, `DistanciaMaximaCamaraHeli`, `DistanciaCamaraHeli`, `AlturaDeCamaraSobrePiso`

## `Assets/_Project/Scripts/Operacion/OperacionDirector.cs`

**Tipos:** `enum FaseOperacion`, `class PuestoDeVoladura`, `class OleadaDeReserva`, `class OperacionDirector`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TituloObjetivo` | static | `string` | `int n, string nombre` | 123 |
| `Indice` | static | `int` | `FaseOperacion f` | 129 |
| `CargadoresPorDificultad` | static | `int` | `NivelDificultad n` | 169 |
| `CargadoresPorDificultad` | static | `int` | — | 178 |
| `EscuadraDeLaOperacion` | static | `List<Soldier>` | — | 257 |
| `AliadosAEvacuar` | static | `List<Soldier>` | `bool soloVivos = true` | 261 |
| `EntrarFase` |  | `void` | `FaseOperacion f` | 286 |
| `GuiaPermitida` |  | `bool` | `out Vector3 objetivo` | 460 |
| `Perder` |  | `void` | `string motivo` | 520 |
| `CapturarEstado` |  | `void` | `Dictionary<string, string> kv` | 543 |
| `RestaurarEstado` |  | `void` | `Dictionary<string, string> kv` | 593 |
| `SaltarA` |  | `void` | `FaseOperacion destino, bool teletransportar = true, int subfase = 0` | 676 |
| `SaltarAPuesto` |  | `void` | `int indice` | 720 |
| `TeletransportarEscuadra` |  | `void` | `Vector3 punto, Quaternion rot` | 749 |

**Propiedades:** `Instancia`, `Activo`, `Fase`, `PuestoActual`, `Reloj`, `EnTanque`, `AutosDestruidos`, `AutosCreados`, `TrayectoRestante`, `ResistenciaRestante`, `HeliAterrizo`, `HeliEnLaZona`, `SubiendoAlHeli`, `ScriptHeli`, `Motivo`, `Subfase`, `SeguimientosAutomaticos`, `ClaveEstado`

## `Assets/_Project/Scripts/Operacion/OperacionHud.cs`

**Tipos:** `class OperacionHud`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Crear` | static | `OperacionHud` | — | 27 |
| `Objetivo` |  | `void` | `string tit, string det, float progreso01, Color color` | 176 |
| `Subobjetivos` |  | `void` | `string linea` | 218 |
| `AlternarPasos` |  | `void` | — | 242 |
| `Pasos` |  | `void` | `string[] pasos, int actual` | 244 |
| `BarraDeJefe` |  | `void` | `string nombre, float frac` | 300 |
| `OcultarBarraDeJefe` |  | `void` | — | 342 |
| `Iluminado` |  | `void` | `bool si` | 358 |
| `Timer` |  | `void` | `string etiqueta, int segundos, Color color` | 396 |
| `OcultarTimer` |  | `void` | — | 402 |
| `Prompt` |  | `void` | `string texto, float progreso01 = -1f` | 408 |
| `Aviso` |  | `void` | `string texto, float segundos = 3.5f` | 418 |

**Propiedades:** `Instancia`, `TextoTitulo`, `TextoDetalle`, `TextoPrompt`, `TextoTimer`, `TextoSubobjetivo`, `TextoPasos`, `PasosPlegados`, `BarraDeJefeVisible`, `TextoJefe`, `FraccionDeJefe`, `IluminadoVisible`, `TextoIluminado`, `AlfaDeIluminacion`

## `Assets/_Project/Scripts/Operacion/OperacionTerminal.cs`

**Tipos:** `class OperacionTerminal`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `EMantenida` | static | `bool` | — | 44 |
| `EToque` | static | `bool` | — | 45 |
| `PuedeOperar` |  | `bool` | `Soldier s` | 70 |
| `GetPrompt` |  | `string` | `PlayerInputDriver player` | 82 |
| `CanInteract` |  | `bool` | `PlayerInputDriver player` | 83 |
| `Interact` |  | `void` | `PlayerInputDriver player` | 84 |
| `ElegirOperador` |  | `Soldier` | `IList<Soldier> candidatos` | 86 |
| `EnviarA` |  | `bool` | `Soldier s, out string motivo` | 113 |
| `Enganchar` |  | `void` | `Soldier s` | 267 |
| `Avanzar` |  | `void` | `float dt, Soldier quien = null` | 284 |
| `ResetearProgreso` |  | `void` | — | 301 |

**Propiedades:** `Progreso01`, `Completo`, `Habilitado`, `Operador`, `Asignado`, `UltimoMensaje`, `NombreParaOrden`, `QuienDebe`, `Raiz`, `VerboDeUso`

## `Assets/_Project/Scripts/Operacion/OperacionTextoMundo.cs`

**Tipos:** `class OperacionTextoMundo`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `AsignarATodos` |  | `void` | — | 63 |

**Propiedades:** `TextosCorregidos`

## `Assets/_Project/Scripts/Operacion/RampaCaminable.cs`

**Tipos:** `class RampaCaminable`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Superficie` |  | `bool` | `Vector3 p, out float y` | 31 |

**Propiedades:** `Todas`, `SoldadosEnRampa`

## `Assets/_Project/Scripts/Operacion/ReflectorVigia.cs`

**Tipos:** `class ReflectorVigia`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `EstaAlumbrando` |  | `bool` | `Soldier s` | 192 |
| `RomperYa` |  | `void` | — | 283 |
| `FactorDeDispersionContraIluminado` | static | `float` | `Vector3 origen, Vector3 direccion` | 294 |
| `Iluminado` | static | `bool` | `Soldier s` | 310 |

**Propiedades:** `Todos`, `Rotos`, `UltimoAlumbradoDelPoseidoT`, `PoseidoIluminado`, `EstaRoto`, `Blanco`, `Siguiendo`, `Alertas`, `Luz`, `DireccionDelHaz`, `OperadorVivo`, `HazVisible`

## `Assets/_Project/Scripts/Operacion/ReparacionDeTanque.cs`

**Tipos:** `class ReparacionDeTanque`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Iniciar` | static | `ReparacionDeTanque` | `Vehicle tanque` | 48 |
| `BuscarFlanqueador` | static | `Soldier` | — | 73 |
| `EnemigosCerca` | static | `int` | `Vector3 punto, float radio` | 87 |
| `CompletarYa` |  | `void` | — | 190 |
| `FijarProgreso` |  | `void` | `float p` | 198 |

**Propiedades:** `Activa`, `Tanque`, `Reparador`, `Progreso01`, `Pausada`, `MotivoDePausa`, `Completa`, `ElJugadorRepara`, `SegundosPausados`, `AvisosBajoFuego`, `Intervalos`, `TextoDeBarra`

## `Assets/_Project/Scripts/Operacion/SectorDeDefensa.cs`

**Tipos:** `class SectorDeDefensa`, `enum Estado`, `struct PuntoDeCobertura`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Contiene` |  | `bool` | `Vector3 p, float margen = 0f` | 35 |
| `Mostrar` |  | `void` | `bool si` | 160 |
| `PuntosDeCobertura` |  | `List<PuntoDeCobertura>` | `int max = 4` | 175 |

**Propiedades:** `Amenazado`, `Caido`, `AliadosDentro`, `SegundosVacio`, `SegundosRecuperando`, `Indice`, `Centro`, `Situacion`, `ColorActual`, `Dibujado`, `AlfaActual`, `AlfaMaximoPintado`

## `Assets/_Project/Scripts/Operacion/TanqueJefe.cs`

**Tipos:** `class TanqueJefe`, `enum Estado`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Preparar` | static | `TanqueJefe` | `Vehicle v` | 80 |
| `Reposo` |  | `void` | — | 96 |
| `Despertar` |  | `void` | `Vector3[] puntosDePatrulla` | 109 |
| `Soltar` |  | `void` | — | 127 |
| `AdosarCarga` |  | `void` | — | 276 |
| `BajarTripulacion` |  | `void` | — | 326 |
| `RendirYa` |  | `void` | — | 339 |

**Propiedades:** `Instancia`, `Situacion`, `Vehiculo`, `Canonazos`, `BalasDeMetralla`, `BalasAbsorbidas`, `CartelesDeBalas`, `CargasAdosadas`, `VelocidadActual`, `TripulacionBajo`, `AnilloActual`, `PuntoDeImpacto`, `ProgresoDeCarga01`, `Fraccion01`

## `Assets/_Project/Scripts/Operacion/TorreDestruible.cs`

**Tipos:** `class TorreDestruible`, `enum Tipo`, `class VuelcoDeTorre`, `class PuestoElevado`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Matar` | static | `void` | `Soldier s, int atacante` | 120 |
| `CaerYa` |  | `void` | — | 170 |
| `Montar` | static | `void` | `Soldier s, Vector3 pies, float yaw` | 231 |

**Propiedades:** `Todas`, `Marca`, `EstaCaida`, `Cayendo`, `Vida`, `VidaMaxima`, `Cima`, `AlturaDeLaCima`

## `Assets/_Project/Scripts/Player/AccionesEnCurso.cs`

**Tipos:** `class AccionesEnCurso`, `struct Accion`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Reportar` | static | `void` | `Soldier actor, string verboEs, Vector3 puntoObjetivo, float progreso01` | 35 |
| `Terminar` | static | `void` | `Soldier actor` | 54 |
| `Limpiar` | static | `void` | — | 61 |
| `De` | static | `bool` | `Soldier actor, out Accion accion` | 67 |
| `Vigentes` | static | `int` | `List<Accion> destino` | 77 |

**Propiedades:** `Punto`, `Cantidad`

## `Assets/_Project/Scripts/Player/AimTargeting.cs`

**Tipos:** `enum AimTargetType`, `struct AimResult`, `class AimTargeting`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `AliadoBajoSuRombo` | static | `Soldier` | `Ray ray, Soldier excludeSelf` | 41 |
| `Evaluate` |  | `AimResult` | `Ray ray, Soldier excludeSelf` | 62 |
| `PisoBajoRayo` | static | `bool` | `Ray ray, out Vector3 punto` | 201 |

**Propiedades:** `MaxDistance`

## `Assets/_Project/Scripts/Player/CajaDeSuministros.cs`

**Tipos:** `class CajaDeSuministros`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `MasCercana` | static | `CajaDeSuministros` | `Vector3 pos, float radio` | 27 |
| `Crear` | static | `CajaDeSuministros` | `Vector3 pos, float segundosDeReposicion` | 43 |
| `Crear` | static | `CajaDeSuministros` | `Vector3 pos` | 51 |

**Propiedades:** `Recogidas`, `Disponible`, `SegundosPropios`

## `Assets/_Project/Scripts/Player/CambioDeAliado.cs`

**Tipos:** `class CambioDeAliado`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `CambioDeAliado` | `PlayerInputDriver d` | 49 |
| `Actualizar` |  | `void` | `bool apretada, float dt` | 87 |
| `Cancelar` |  | `void` | — | 108 |
| `MasCercano` |  | `Soldier` | — | 134 |
| `MasCentrado` |  | `Soldier` | `Camera cam` | 150 |

**Propiedades:** `Instancia`, `Resaltando`, `Candidato`, `RombosVisibles`, `Pista`, `Cambios`, `UltimoElegido`

## `Assets/_Project/Scripts/Player/Demolicion.cs`

**Tipos:** `class Demolicion`, `class DemoledorAsalto`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `MarcadorApuntado` | static | `ObstacleMarker` | `Transform golpeado, Vector3 punto` | 27 |
| `MarcadorDeLaMira` | static | `ObstacleMarker` | `AimResult aim` | 40 |
| `EsDemolible` | static | `bool` | `ObstacleMarker m, out string motivo` | 50 |
| `DistanciaA` | static | `float` | `Soldier s, ObstacleMarker m` | 64 |
| `AvisarSiNoSePuede` | static | `string` | `Soldier yo, AimResult aim, Ray rayoDeMira, bool apretoE` | 78 |
| `Ejecutar` | static | `void` | `ObstacleMarker m, Soldier quien` | 119 |
| `AsegurarEnAsaltos` | static | `int` | — | 166 |
| `CancelarTodos` | static | `bool` | — | 180 |
| `IniciarComoJugador` |  | `bool` | `ObstacleMarker m, out string motivo` | 191 |
| `IniciarComoAliado` |  | `bool` | `ObstacleMarker m, out string motivo` | 200 |
| `Cancelar` |  | `void` | `string aviso` | 220 |

**Propiedades:** `Progreso`, `Objetivo`, `EstaCargando`, `Todos`, `HayEnCurso`

## `Assets/_Project/Scripts/Player/KeyBindings.cs`

**Tipos:** `class KeyBindings`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Get` | static | `Key` | `string actionId` | 139 |
| `Set` | static | `string` | `string actionId, Key key` | 145 |
| `ResetToDefaults` | static | `void` | — | 169 |
| `InvalidateCache` | static | `void` | — | 181 |
| `WasPressed` | static | `bool` | `string actionId` | 189 |
| `IsPressed` | static | `bool` | `string actionId` | 198 |
| `WasTapped` | static | `bool` | `string actionId, float holdSeconds = 0.3f` | 214 |
| `IsHeld` | static | `bool` | `string actionId, float holdSeconds = 0.3f` | 232 |
| `HeldSeconds` | static | `float` | `string actionId` | 252 |
| `ForzarInicioDePulsacion` | static | `void` | `string actionId, float segundos` | 267 |
| `HayPulsacionRegistrada` | static | `bool` | `string actionId, float holdSeconds` | 272 |
| `DisplayName` | static | `string` | `string actionId` | 278 |

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
| `Crear` | static | `MunicionPickup` | `Vector3 pos` | 30 |

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
| `FindNearestFreeAlly` | static | `Soldier` | `Vector3 point, TeamId team, Soldier exclude` | 86 |
| `PuntoAlcanzable` | static | `bool` | `Vector3 desde, Vector3 punto, out Vector3 ajustado` | 134 |
| `IssueMoveOrder` | static | `void` | `Soldier soldier, Vector3 point, bool queued = false` | 144 |
| `IssueCoverOrder` | static | `bool` | `Soldier soldier, Vector3 point, Collider owner` | 172 |
| `FormationPoints` | static | `Vector3[]` | `Vector3 center, int count` | 198 |
| `FormationPoints` | static | `Vector3[]` | `Vector3 center, Vector3 forward, int count, FormationKind kind` | 207 |
| `FormationPoints` | static | `Vector3[]` | `Vector3 center, Vector3 forward, int count, FormationKind kind, float ` | 216 |
| `SpreadOf` | static | `float` | `IReadOnlyList<Vector3> positions` | 309 |
| `IsValidDestination` | static | `bool` | `Vector3 point` | 357 |
| `IssueAttackOrder` | static | `void` | `Soldier soldier, Soldier enemy` | 383 |
| `IssueAttackOrderForSelection` | static | `void` | `IEnumerable<Soldier> selection, Soldier enemy` | 404 |
| `IssueAttackVehicleOrderForSelection` | static | `int` | `IEnumerable<Soldier> selection, Vehicle vehiculo` | 418 |
| `IssueMoveOrderForSelection` | static | `void` | `IEnumerable<Soldier> selection, Vector3 point, bool queued = false` | 439 |
| `IssueFormationOrderForSelection` | static | `void` | `IEnumerable<Soldier> selection, Vector3 center, Vector3 forward, Forma` | 452 |
| `PlayRejectSound` | static | `void` | — | 531 |
| `IssueFollowOrder` | static | `void` | `Soldier soldier, Soldier leader, Vector3 formationOffsetLocal = defaul` | 540 |
| `NombreCorto` | static | `string` | `Soldier s` | 555 |
| `TextoDeSeguimiento` | static | `string` | `IList<Soldier> soldados` | 562 |
| `IssueFollowOrderForSelection` | static | `void` | `IEnumerable<Soldier> selection, Soldier leader, float forzarSegundos =` | 610 |
| `IssueMountOrder` | static | `void` | `Soldier soldier, Vehicle vehicle` | 634 |
| `IssueMountOrderForSelection` | static | `void` | `IEnumerable<Soldier> selection, Vehicle vehicle` | 642 |
| `RegroupSelection` | static | `Vector3[]` | `IEnumerable<Soldier> selection, FormationKind kind = FormationKind.Cua` | 660 |
| `IssueRetreatOrderForSelection` | static | `void` | `IEnumerable<Soldier> selection, float distance = RetreatDistance, floa` | 739 |

**Propiedades:** `RechazosPorOcupado`, `UltimoRechazoPorOcupado`

## `Assets/_Project/Scripts/Player/PedidoDeCuracion.cs`

**Tipos:** `class PedidoDeCuracion`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `CuracionPorSegundoDe` | static | `int` | `Soldier herido` | 31 |
| `RespetandoOrden` | static | `bool` | `Soldier s` | 43 |
| `SoltarSiEsElEnfermero` | static | `void` | `Soldier s` | 45 |
| `CurandoAlJugador` | static | `bool` | `Soldier poseido` | 79 |
| `MedicoDisponible` | static | `Soldier` | `Soldier excluir = null` | 95 |
| `RescatistaCercano` | static | `Soldier` | `Soldier caido` | 109 |
| `SolicitarReanimar` | static | `bool` | `Soldier caido, Soldier medicoManual = null` | 113 |
| `LimpiarCola` | static | `void` | — | 165 |
| `Solicitar` | static | `bool` | `Soldier herido, Soldier medicoManual = null` | 178 |
| `Botiquin` | static | `bool` | `Soldier medico` | 222 |
| `BuscarEnfermero` | static | `Soldier` | `Soldier herido` | 258 |
| `RechazoActivo` | static | `bool` | `Soldier s` | 275 |
| `CancelarPorElJugador` | static | `void` | — | 277 |
| `Cancelar` | static | `void` | — | 316 |
| `HayCalma` | static | `bool` | `Vector3 punto` | 357 |
| `Tick` | static | `void` | `float dt` | 445 |

**Propiedades:** `Herido`, `Enfermero`, `Activo`, `Reanimando`, `ProgresoDeReanimar`, `Atendiendo`, `EnCola`, `BotiquinDe`, `BotiquinListoEn`, `BotiquinActivo`, `Retenido`, `ReanimacionEsAutomatica`

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

## `Assets/_Project/Scripts/Player/PlayerInputDriver.Contextual.cs`

**Tipos:** `class PlayerInputDriver`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `OrdenableEnLaMira` |  | `IOrdenableAAliados` | `AimResult aim` | 18 |
| `EnviarAInteractuar` |  | `bool` | `IOrdenableAAliados o, int sub` | 25 |
| `AccionContextualDeQ` |  | `string` | `AimResult aim` | 56 |

## `Assets/_Project/Scripts/Player/PlayerInputDriver.Escuadra.cs`

**Tipos:** `class PlayerInputDriver`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `MoverEnEscuadra` |  | `bool` | `Soldier s, int direccion` | 44 |
| `SeleccionarSoldadoDeEscuadra` |  | `bool` | `int indice, bool sumar` | 81 |

## `Assets/_Project/Scripts/Player/PlayerInputDriver.Interaccion.cs`

**Tipos:** `class PlayerInputDriver`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ObjetivoInteractuable` |  | `IInteractable` | — | 34 |
| `PromptDeInteraccion` |  | `string` | — | 61 |
| `RecordarEnemigoApuntadoHace` |  | `void` | `Soldier s, float haceSegundos` | 107 |
| `TryInteractuarConMira` |  | `bool` | — | 144 |

**Propiedades:** `UltimaAccionRapida`, `UltimoEnemigoApuntado`

## `Assets/_Project/Scripts/Player/PlayerInputDriver.Radial.cs`

**Tipos:** `class PlayerInputDriver`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ResolverGestoDeQ` |  | `void` | `bool toque, bool sostenido, bool sigueApretada` | 65 |
| `SeguirTodosSiOSi` |  | `int` | — | 140 |
| `DestinatariosDeOrden` |  | `List<Soldier>` | — | 174 |
| `EjecutarOrdenDelMenu` |  | `bool` | `int opcion` | 192 |
| `EjecutarOrdenRadial` |  | `bool` | `int categoria, int sub = 0` | 257 |
| `UsarTorreta` |  | `bool` | `TorretaFija t` | 582 |
| `SalirDeTorreta` |  | `void` | — | 609 |

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
| `ExitVehicle` |  | `void` | — | 137 |
| `SwitchSeat` |  | `void` | `VehicleSeatRole newRole` | 420 |
| `PuntoBajoLaMira` |  | `bool` | `out Vector3 punto` | 619 |

**Propiedades:** `CurrentSeat`, `InclinacionArtillero`

## `Assets/_Project/Scripts/Player/PlayerInputDriver.cs`

**Tipos:** `class PlayerInputDriver`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `MostrarProgresoDemolicion` |  | `void` | `float f01` | 324 |
| `OcultarProgresoDemolicion` |  | `void` | — | 325 |
| `SetDestination` |  | `void` | `Vector3 punto` | 492 |
| `CancelDestination` |  | `void` | — | 514 |
| `ToggleVehicleCameraView` |  | `void` | — | 516 |
| `ShowTutorialMessage` |  | `void` | `string text, float holdSeconds = 1.4f` | 525 |
| `ReclamarControl` |  | `void` | `Soldier s` | 833 |
| `FindNearestDownedAlly` |  | `Soldier` | — | 1638 |
| `TryRevivir` |  | `bool` | `Soldier caido, bool sostenidoLoSuficiente` | 1661 |
| `TryResolverCobertura` |  | `bool` | `AimResult r, out Vector3 punto, out Collider dueno` | 1974 |
| `IssueCoverOrderT` |  | `bool` | `Vector3 punto, Collider dueno` | 2011 |
| `IssueCoverOrderForSelection` |  | `int` | `IReadOnlyList<Soldier> seleccion, Vector3 puntoApuntado, Transform obs` | 2035 |
| `SubirATodos` |  | `int` | `Vehicle v` | 2064 |
| `BajarATodos` |  | `int` | `Vehicle v` | 2085 |
| `IssueGroundOrderT` |  | `void` | `Vector3 punto, bool shiftHeld` | 2133 |
| `GOrderOnVehicle` |  | `void` | `Vehicle vehicle` | 2267 |
| `AlternarVista` |  | `void` | — | 2396 |
| `PanearRts` |  | `void` | `Vector3 direccion, bool rapido, float dt` | 2478 |
| `CentrarRtsEnElPoseido` |  | `void` | `Vector3 foco` | 2488 |
| `CentrarRtsEnLaEscuadra` |  | `void` | — | 2493 |
| `SoldadoUnicoSeleccionado` |  | `Soldier` | — | 2520 |
| `TryPossess` |  | `bool` | `Soldier target` | 2534 |
| `EquipSlot` |  | `void` | `int slot` | 2683 |
| `EquipWeaponHotkey` |  | `void` | `WeaponKind kind` | 2707 |
| `TryIssueVehicleMoveOrder` |  | `bool` | `Vector3 point, Vehicle vehicle = null` | 2733 |
| `ConstruirContextoRadial` |  | `ContextoRadial` | `AimResult aim` | 2862 |
| `AvisarRechazo` |  | `void` | `string reason` | 3706 |
| `AliadoBajoElRombo` |  | `Soldier` | `Vector2 pantalla` | 3928 |

**Propiedades:** `LookSensitivity`, `TurretSensitivity`, `InvertLookY`, `EspecialSostenido`, `UltimaMira`, `DemolicionEnCurso`, `GranadaApuntando`, `GranadasLanzadasPorElJugador`, `MiraInteractivaActual`, `Mira`, `ApuntadoVisual`, `TieneDestino`, `DestinoActual`, `IsHandlingDeath`, `EsperandoRescate`, `ReviviendoActivo`, `MovimientoTrabadoPorRevivir`, `AnimandoRevivir`, `EnTorretaFija`, `TorretaActual`

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

## `Assets/_Project/Scripts/Player/Rehen.cs`

**Tipos:** `class Rehen`, `class GiroDeMarcador`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `DesactivarMarcador` |  | `void` | — | 92 |

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
| `SelectSingle` |  | `void` | `Soldier s` | 56 |
| `AddToSelection` |  | `void` | `Soldier s` | 64 |
| `SelectVehicle` |  | `void` | `Vehicle v` | 71 |
| `SelectAll` |  | `void` | `IEnumerable<Soldier> squad` | 82 |
| `SelectWoundedOnly` |  | `bool` | `float threshold01 = 0.5f` | 98 |
| `IsWounded` | static | `bool` | `int current, int max, float threshold01` | 138 |
| `SelectSameTypeOnScreen` |  | `void` | `Soldier reference, Camera cam` | 156 |
| `Clear` |  | `void` | — | 223 |

**Propiedades:** `Instance`, `Selected`, `SelectedVehicle`

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
| `Refrescar` |  | `void` | — | 219 |
| `Linea` | static | `string` | `in AccionesEnCurso.Accion a, bool propia` | 281 |

**Propiedades:** `Instancia`, `BarrasVisibles`, `TextoDeEstado`, `TextoDeEstadoCompleto`, `HudVisible`, `RaizDelHud`, `PosicionesDeBarras`, `RellenoDeLaPrimeraBarra`, `TiempoDeLaPrimeraBarra`

## `Assets/_Project/Scripts/Presentation/AnimacionDeAccion.cs`

**Tipos:** `enum TipoAccion`, `class AnimacionDeAccion`, `class AnimacionDeAccionSoldado`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TipoDeVerbo` | static | `TipoAccion?` | `string verboEs` | 40 |
| `Tick` | static | `void` | `Soldier actor, Vector3 objetivo, TipoAccion tipo, float dt` | 64 |
| `Iniciar` | static | `void` | `Soldier actor, TipoAccion tipo, Vector3 objetivo, float duracion` | 70 |
| `ProgresoDeRecarga` | static | `void` | `Soldier actor, float progreso01, Vector3 objetivo` | 78 |
| `Terminar` | static | `void` | `Soldier actor` | 86 |
| `Activa` | static | `bool` | `Soldier actor` | 92 |
| `TipoActivo` | static | `TipoAccion?` | `Soldier actor` | 98 |
| `TieneRig` | static | `bool` | `Soldier actor` | 105 |
| `DesplazamientoDeManos` | static | `float` | `Soldier actor` | 106 |
| `DesplazamientoMaximoDeManos` | static | `float` | `Soldier actor` | 107 |
| `ReiniciarMedicion` | static | `void` | `Soldier actor` | 108 |
| `AgachadaPropia` | static | `bool` | `Soldier actor` | 109 |
| `Prop` | static | `string` | `Soldier actor` | 110 |
| `Mantener` |  | `void` | `TipoAccion nuevo, Vector3 destino, float segundos` | 170 |
| `Cortar` |  | `void` | — | 190 |

**Propiedades:** `Activa`, `Tipo`, `ProgresoExterno`, `TieneRig`, `DesplazamientoActual`, `DesplazamientoMaximo`, `AgachadaPropia`, `NombreDelProp`

## `Assets/_Project/Scripts/Presentation/AnuncioDeZonas.cs`

**Tipos:** `class AnuncioDeZonas`, `struct Zona`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 15 |
| `RegistrarActivo` |  | `void` | — | 16 |
| `Asegurar` | static | `AnuncioDeZonas` | — | 54 |

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
| `DebeDibujar` | static | `bool` | `float distancia, float rangoDelArma, bool enemigoVisible, out float op` | 30 |
| `TieneLinea` |  | `bool` | `int soldadoId` | 116 |
| `Prewarm` | static | `void` | — | 159 |

**Propiedades:** `LineasActivas`

## `Assets/_Project/Scripts/Presentation/AudioDirector.cs`

**Tipos:** `enum SfxChannel`, `struct PerfilEspacial`, `struct VoiceState`, `class AudioDirector`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Para` | static | `PerfilEspacial` | `SfxKind kind` | 40 |
| `GainFor` | static | `float` | `SfxChannel c` | 139 |
| `SetGain` | static | `void` | `SfxChannel c, float v` | 148 |
| `SetMasterGain` | static | `void` | `float v` | 157 |
| `InvalidateGainCache` | static | `void` | — | 164 |
| `Attenuation` | static | `float` | `float distance` | 185 |
| `Attenuation` | static | `float` | `float distance, PerfilEspacial p` | 193 |
| `CutoffFor` | static | `float` | `float distance, float max` | 202 |
| `CutoffFor` | static | `float` | `float distance` | 216 |
| `NextPitch` | static | `float` | — | 236 |
| `SelectVictim` | static | `int` | `VoiceState[] voices, float newAudibility, float now` | 262 |
| `SonoRecien` | static | `bool` | `string nombreDeClip, int ultimos = 12` | 317 |
| `EnsureVoices` |  | `void` | — | 344 |
| `Play` |  | `void` | `SfxKind kind, Vector3 position, float volume, float priority` | 452 |
| `Play` |  | `void` | `SfxKind kind, Vector3 position, float volume` | 457 |
| `PlayClip` |  | `bool` | `AudioClip clip, Vector3 position, float volume, float priority` | 464 |
| `PlayClip` |  | `bool` | `AudioClip clip, Vector3 position, float volume, float priority, SfxCha` | 470 |
| `PlayClip` |  | `bool` | `AudioClip clip, Vector3 position, float volume, float priority, SfxCha` | 474 |
| `PlayFlat` |  | `bool` | `AudioClip clip, SfxChannel channel, float volume, float priority, floa` | 540 |
| `PlayUi` |  | `bool` | `SfxKind kind, float volume, float priority` | 579 |
| `PlayVoice` |  | `bool` | `SfxKind kind, float volume, float priority` | 585 |
| `PlayAt` | static | `bool` | `SfxKind kind, Vector3 position, float volume, float priority = 1f` | 594 |
| `PlayAt` | static | `bool` | `SfxKind kind, Vector3 position, float volume, float priority, PerfilEs` | 597 |
| `PlayClipAt` | static | `bool` | `AudioClip clip, Vector3 position, float volume, float priority = 1f` | 600 |
| `PlayClipAt` | static | `bool` | `AudioClip clip, Vector3 position, float volume, float priority, Perfil` | 603 |
| `PlayUi2D` | static | `bool` | `SfxKind kind, float volume, float priority = 1f` | 606 |
| `PlayVoice2D` | static | `bool` | `SfxKind kind, float volume, float priority = 1f` | 609 |
| `DistanceOrUnknown` | static | `float` | `Transform listener, Vector3 position` | 612 |
| `Ocluido` |  | `bool` | `Vector3 position` | 620 |
| `ResetStats` |  | `void` | — | 726 |

**Propiedades:** `Instance`, `DroppedCount`, `TotalReproducidos`, `UltimoSonidoTapado`, `VoiceCount`, `FreeVoiceCount`, `ActiveVoiceCount`, `Active3DVoiceCount`, `Active2DVoiceCount`

## `Assets/_Project/Scripts/Presentation/AudioDucking.cs`

**Tipos:** `class AudioDucking`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Duck` | static | `void` | `float intensity01` | 33 |

**Propiedades:** `UserVolumeCeiling`

## `Assets/_Project/Scripts/Presentation/BalizaPulsante.cs`

**Tipos:** `class BalizaPulsante`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Configurar` |  | `void` | `Light l, Renderer r, Color emisionHdr, float ciclos, float min, float ` | 21 |

## `Assets/_Project/Scripts/Presentation/BarraDeVidaVehiculo.cs`

**Tipos:** `class BarraDeVidaVehiculo`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `BarraDeVidaVehiculo` | `Vehicle v` | 35 |
| `Refrescar` |  | `void` | — | 116 |

**Propiedades:** `Relleno01`, `Visible`, `ColorDelRelleno`

## `Assets/_Project/Scripts/Presentation/BrazoIk.cs`

**Tipos:** `class BrazoIk`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Resolver` | static | `void` | `Transform raiz, Transform brazo, Transform antebrazo, Transform mano, ` | 13 |
| `Suave` | static | `float` | `float k` | 45 |

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
| `ReiniciarActivo` | static | `void` | — | 91 |
| `WriteTint` | static | `void` | `Renderer r, Color c` | 256 |
| `ReadTint` | static | `Color` | `Renderer r` | 268 |

## `Assets/_Project/Scripts/Presentation/CuchilloFx.cs`

**Tipos:** `class CuchilloFx`, `class TajoVisual`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ResetearContadores` | static | `void` | — | 15 |
| `Tajo` | static | `void` | `Soldier dueno, Soldier objetivo` | 17 |
| `Iniciar` |  | `void` | `Transform dueno` | 64 |

**Propiedades:** `Tajos`, `Aciertos`

## `Assets/_Project/Scripts/Presentation/CurandoAnimacion.cs`

**Tipos:** `class CurandoAnimacion`, `class Estado`, `class AvisoCentral`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Animando` | static | `bool` | `Soldier medico` | 28 |
| `CrucesVivas` | static | `int` | `Soldier medico` | 29 |
| `Tick` | static | `void` | `Soldier medico, Soldier herido, float dt, bool orientar = true` | 83 |
| `Terminar` | static | `void` | `Soldier medico` | 143 |
| `Mostrar` | static | `void` | `string mensaje, float segundos, Color? colorDeFondo = null` | 197 |
| `Ocultar` | static | `void` | — | 208 |

**Propiedades:** `TextoActual`

## `Assets/_Project/Scripts/Presentation/CursorContextual.cs`

**Tipos:** `enum CursorTipo`, `class CursorContextual`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Aplicar` | static | `void` | `CursorTipo tipo` | 16 |
| `Restaurar` | static | `void` | — | 32 |

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
| `Prewarm` | static | `void` | — | 87 |
| `Spawn` | static | `GameObject` | `DecalKind kind, Vector3 position, Vector3 normal, float size` | 93 |

## `Assets/_Project/Scripts/Presentation/DestruccionDeVehiculo.cs`

**Tipos:** `class DestruccionDeVehiculo`, `class ChapaVolante`, `class CraterPool`, `class CraterVida`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `AlturaDelPiso` | static | `float` | `Vector3 p` | 19 |
| `Chapas` | static | `void` | `Vector3 centro, int cantidad, float fuerza` | 45 |
| `RomperPiezas` | static | `int` | `Transform visual, string[] nombres, Vector3 origen, float fuerza, int ` | 57 |
| `LimpiarTodo` | static | `void` | — | 86 |
| `Lanzar` | static | `void` | `Vector3 pos, Vector3 velocidad, float sueloY` | 129 |
| `LimpiarTodo` | static | `void` | — | 201 |
| `Crear` | static | `CraterVida` | `Vector3 punto, float pisoY, float diametro` | 288 |

**Propiedades:** `ChapasActivas`, `ChapasLanzadas`, `Lanzadas`, `Activas`, `Activos`, `Lanzados`, `Piezas`

## `Assets/_Project/Scripts/Presentation/DiamondGizmo.cs`

**Tipos:** `class DiamondGizmo`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `CrearCara` | static | `GameObject` | `string nombre, Transform padre, float tamano, Material mat` | 68 |
| `NuevoMaterialCompartido` | static | `Material` | `Color color` | 118 |
| `NuevoMaterial` | static | `Material` | `Color color, bool zTestAlways = false` | 127 |
| `TexturaEngranajeConAlfa` | static | `Texture2D` | — | 201 |
| `NuevoMaterialTransparente` | static | `Material` | `Color color, Texture textura, bool zTestAlways = false` | 234 |
| `NuevoMaterialConEngranaje` | static | `Material` | `Color color` | 273 |

## `Assets/_Project/Scripts/Presentation/DianaDeObjetivo.cs`

**Tipos:** `class DianaDeObjetivo`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Mostrar` | static | `DianaDeObjetivo` | `Soldier enemigo` | 40 |
| `SobreElEnemigo` | static | `DianaDeObjetivo` | `Soldier s` | 62 |
| `AlguienLoAtaca` | static | `bool` | `Soldier objetivo` | 172 |

**Propiedades:** `Enemigo`, `Edad`, `Activas`

## `Assets/_Project/Scripts/Presentation/EnemyAlertIndicatorView.cs`

**Tipos:** `class EnemyAlertIndicatorView`

**Propiedades:** `IsBlinkingLowHealth`

## `Assets/_Project/Scripts/Presentation/EngranajesDeAccion.cs`

**Tipos:** `class EngranajesDeAccion`, `class Engranaje`, `struct Muestra`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Muestras` | static | `List<Muestra>` | — | 92 |
| `Asegurar` | static | `EngranajesDeAccion` | — | 118 |
| `Refrescar` |  | `void` | — | 188 |

**Propiedades:** `Instancia`, `Activos`, `Visibles`

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
| `ClearAll` | static | `void` | — | 103 |
| `Spawn` | static | `WorldTag` | `Vector3 pos, string text, Color color` | 137 |

**Propiedades:** `Contador`, `UltimoTexto`, `UltimoSonido`, `Inicio`, `Texto`, `Malla`, `Todas`, `ActiveCount`

## `Assets/_Project/Scripts/Presentation/FeedbackDeRevivir.cs`

**Tipos:** `class FeedbackDeRevivir`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Reiniciar` | static | `void` | — | 34 |

**Propiedades:** `Cantidad`, `Ultimo`

## `Assets/_Project/Scripts/Presentation/FlechaDeOleada.cs`

**Tipos:** `class FlechaDeOleada`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Mostrar` | static | `FlechaDeOleada` | `Vector3 centro, Vector3 direccion, float segundos` | 35 |
| `QuitarTodas` | static | `void` | — | 43 |
| `Apagar` |  | `void` | — | 144 |

**Propiedades:** `Activa`, `Centro`, `Direccion`, `Edad`, `Activas`, `Todas`

## `Assets/_Project/Scripts/Presentation/FocoBarredor.cs`

**Tipos:** `class FocoBarredor`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Configurar` |  | `void` | `float yaw, float cabeceoGrados, float barridoGrados, float ciclosPorSe` | 17 |

## `Assets/_Project/Scripts/Presentation/Fragmentador.cs`

**Tipos:** `class Fragmento`, `class Fragmentador`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Liberar` |  | `void` | — | 46 |
| `LimpiarTodo` | static | `void` | — | 71 |
| `Precalentar` | static | `void` | — | 132 |
| `Romper` | static | `int` | `Transform objeto, Vector3 origen, float fuerza, int piezas = 0` | 232 |

**Propiedades:** `Lanzados`, `Activos`, `Piezas`

## `Assets/_Project/Scripts/Presentation/FuentesBelicas.cs`

**Tipos:** `class FuentesBelicas`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `EsBelica` | static | `bool` | `Font f` | 46 |
| `Aplicar` | static | `int` | — | 49 |
| `Aplicar` | static | `int` | `FindObjectsInactive inactivos` | 51 |
| `Aplicar` | static | `bool` | `Text t, Font titulo, Font texto` | 64 |
| `Aplicar` | static | `bool` | `TextMesh m, Font texto` | 76 |

**Propiedades:** `Cambiados`, `Titulo`, `Texto`

## `Assets/_Project/Scripts/Presentation/GameOutcomeController.cs`

**Tipos:** `class GameOutcomeController`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 19 |
| `Registrar` |  | `void` | — | 20 |
| `Bind` |  | `void` | `GameObject victory, GameObject defeat` | 50 |
| `ShowVictory` |  | `void` | `bool congelar = true, float alfaFondo = 1f` | 196 |
| `ShowDefeat` |  | `void` | `string motivo = null` | 232 |
| `OnCargarGuardadoClicked` |  | `void` | — | 279 |
| `OnRetryClicked` |  | `void` | — | 289 |
| `OnExitClicked` |  | `void` | — | 298 |

**Propiedades:** `Activo`, `IsShowing`, `AlfaFondoDeVictoria`, `VictoriaCongelada`, `BotonCargar`

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
| `Precargar` | static | `int` | — | 56 |
| `Get` | static | `AudioClip` | `SfxKind kind` | 73 |
| `GetWeaponShot` | static | `AudioClip` | `WeaponKind kind` | 212 |
| `GetWeaponReload` | static | `AudioClip` | `WeaponKind kind` | 251 |
| `GetWeaponDraw` | static | `AudioClip` | `WeaponKind kind` | 262 |
| `GetWeaponDry` | static | `AudioClip` | `WeaponKind kind` | 273 |
| `PlayOneShot2D` | static | `void` | `AudioClip clip, float volume, float pitch, string name = "OneShotTone"` | 296 |

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

## `Assets/_Project/Scripts/Presentation/HudIconFactory.cs`

**Tipos:** `class HudIconFactory`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Cuchillo` | static | `Sprite` | — | 24 |
| `Granada` | static | `Sprite` | — | 25 |
| `Reloj` | static | `Sprite` | — | 26 |
| `Escudo` | static | `Sprite` | — | 27 |
| `TexturaGenerada` | static | `Texture2D` | `string nombre` | 40 |

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
| `ClearAll` | static | `void` | — | 190 |
| `RecycleAll` | static | `void` | — | 214 |
| `Spawn` | static | `void` | `Vector3 position, Color color, float peakScale = 0.55f, float duration` | 223 |
| `Prewarm` | static | `void` | — | 235 |
| `SpawnScaledByDamage` | static | `void` | `Vector3 position, Color color, int damage` | 248 |
| `SpawnArmorSparks` | static | `void` | `Vector3 position, Vector3 surfaceNormal` | 257 |
| `SpawnExplosion` | static | `void` | `Vector3 position, float radius` | 271 |
| `SpawnMuzzleBlast` | static | `void` | `Vector3 pos, Vector3 dir, float size, Color? tinte = null` | 279 |
| `SpawnExplosion` | static | `void` | `Vector3 position, float radius, bool conCrater` | 302 |
| `MarcarBando` | static | `void` | `Vector3 position, float radius, SP.Combat.TeamId? bando` | 336 |
| `SpawnMuzzleBlastLite` | static | `void` | `Vector3 pos, Vector3 dir, float size, Color? tinte = null` | 348 |
| `SpawnShockwaveRing` | static | `void` | `Vector3 center, float radius` | 363 |
| `SpawnShockwaveRing` | static | `void` | `Vector3 center, Color color, float radioFinal, float duracion, float d` | 372 |
| `Recycle` |  | `void` | — | 623 |
| `Contains` |  | `bool` | `ImpactFx fx` | 660 |
| `ResetIfStale` |  | `void` | — | 665 |
| `Take` |  | `ImpactFx` | — | 696 |
| `Release` |  | `void` | `ImpactFx fx` | 729 |
| `LimpiarTodo` |  | `void` | — | 739 |
| `RecycleAll` |  | `void` | — | 759 |

**Propiedades:** `Budget`, `ActiveCount`, `ReviveRingsActive`, `Budget`, `ActiveCount`, `TotalCount`

## `Assets/_Project/Scripts/Presentation/IndicadorDeCaidos.cs`

**Tipos:** `class IndicadorDeCaidos`, `class Marca`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TieneMarca` |  | `bool` | `Soldier s` | 34 |
| `TextoDe` |  | `string` | `Soldier s` | 35 |
| `Asegurar` | static | `IndicadorDeCaidos` | — | 47 |

**Propiedades:** `Instancia`, `MarcasVisibles`

## `Assets/_Project/Scripts/Presentation/InteractGearMarker.cs`

**Tipos:** `class InteractGearMarker`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Mostrar` | static | `void` | `Vector3 ancla, bool destacado` | 80 |
| `Ocultar` | static | `void` | — | 131 |

**Propiedades:** `AnguloActual`

## `Assets/_Project/Scripts/Presentation/InteractHintView.cs`

**Tipos:** `class InteractHintView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `InteractHintView` | — | 31 |
| `Mostrar` | static | `void` | `string mensaje` | 87 |
| `Ocultar` | static | `void` | — | 95 |

## `Assets/_Project/Scripts/Presentation/InteractableDiamond.cs`

**Tipos:** `class InteractableDiamond`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Agregar` | static | `InteractableDiamond` | `Transform padre, Vector3 localOffset, Color color` | 11 |

## `Assets/_Project/Scripts/Presentation/KillCylinderFx.cs`

**Tipos:** `class KillCylinderFx`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ResetIfStale` | static | `void` | — | 33 |
| `Spawn` | static | `void` | `Vector3 position` | 147 |
| `Prewarm` | static | `void` | — | 154 |
| `LimpiarTodo` | static | `void` | — | 173 |
| `Recycle` |  | `void` | — | 241 |

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
| `ContarHuerfanos` | static | `int` | — | 106 |

## `Assets/_Project/Scripts/Presentation/LoadingScreenController.cs`

**Tipos:** `class DatosDelMapa`, `class MapaDeCarga`, `class LoadingScreenController`

**Propiedades:** `MapaVisible`, `EscenaDelMapa`, `TexturaDelMapa`, `PuntosDelRecorrido`, `PuntosRevelados`, `Hitos`

## `Assets/_Project/Scripts/Presentation/Luminaria.cs`

**Tipos:** `class Luminaria`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TakeDamage` |  | `void` | `int amount, Vector3 hitPoint` | 22 |

**Propiedades:** `Rota`

## `Assets/_Project/Scripts/Presentation/LuzTitilante.cs`

**Tipos:** `class LuzTitilante`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Configurar` |  | `void` | `Light l, Transform malla, float amp = 0.28f, float vel = 7f` | 23 |

## `Assets/_Project/Scripts/Presentation/MainMenuController.cs`

**Tipos:** `class MainMenuController`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `OnContinuarClicked` |  | `void` | — | 89 |
| `OnPlayClicked` |  | `void` | — | 106 |
| `MostrarNiveles` |  | `void` | — | 132 |
| `ElegirNivel` |  | `void` | `int n` | 192 |
| `MostrarEnDesarrollo` |  | `void` | `int n` | 204 |
| `MostrarDificultad` |  | `void` | — | 254 |
| `IniciarPartida` |  | `void` | `NivelDificultad nivel` | 331 |
| `OnTutorialClicked` |  | `void` | — | 340 |
| `OnExitClicked` |  | `void` | — | 351 |
| `OnConfirmExitNo` |  | `void` | — | 359 |
| `OnConfirmExitYes` |  | `void` | — | 365 |

**Propiedades:** `BotonContinuar`, `PanelNiveles`, `PopupEnDesarrollo`, `UltimoNivelPedido`

## `Assets/_Project/Scripts/Presentation/MenuAmbiente.cs`

**Tipos:** `class MenuAmbiente`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Crear` | static | `MenuAmbiente` | `Scene escena` | 36 |

**Propiedades:** `Existe`

## `Assets/_Project/Scripts/Presentation/MinimapAttackerRings.cs`

**Tipos:** `class MinimapAttackerRings`, `class RingState`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 12 |

**Propiedades:** `Activo`

## `Assets/_Project/Scripts/Presentation/MinimapIcon.cs`

**Tipos:** `class MinimapIcon`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `EnableFogOfWar` |  | `void` | — | 69 |
| `RepintarPorEquipo` |  | `void` | `bool forzar = false` | 149 |
| `TickFollow` |  | `bool` | — | 447 |
| `ApplyFog` |  | `void` | `bool spotted` | 520 |
| `EnableDirectionMarker` |  | `void` | `int layer, float iconRadius` | 586 |
| `ConvertirEnTriangulo` |  | `bool` | — | 591 |
| `ConvertirEnTrianguloAnidado` |  | `bool` | `Color colorInterior` | 641 |
| `ConvertirEnCirculo` |  | `bool` | — | 710 |
| `ConvertirEnRombo` |  | `bool` | — | 746 |
| `ConvertirEnCuadrado` |  | `bool` | — | 792 |
| `RegistrarVehiculosLibres` | static | `int` | — | 818 |
| `RegistrarObstaculos` | static | `int` | `Color color, float radius = 2.8f` | 844 |
| `Spawn` | static | `MinimapIcon` | `Transform target, Color color, int layer, float radius = 1.6f` | 882 |
| `ExisteIconoPara` | static | `bool` | `Transform target` | 917 |
| `BuscarPorTarget` | static | `MinimapIcon` | `Transform target` | 926 |

**Propiedades:** `Target`, `FogEnabled`, `CaidoParpadeando`, `IsRendered`, `TargetPosition`, `EsEstatico`, `UltimoRenderizado`, `EsCuadrado`

## `Assets/_Project/Scripts/Presentation/MiraOptica.cs`

**Tipos:** `class MiraOptica`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `MiraOptica` | `Transform camaraPrincipal, Transform visorDelArma` | 40 |
| `Configurar` |  | `void` | `WeaponKind arma` | 107 |
| `Mostrar` |  | `void` | `bool apuntando` | 141 |
| `Seguir` |  | `void` | `Camera principal, float fovObjetivo` | 157 |

**Propiedades:** `Optica`, `Textura`, `Tubo`, `Forma`, `Amplia`

## `Assets/_Project/Scripts/Presentation/MusicDirector.cs`

**Tipos:** `class MusicDirector`, `class MusicaVictoriaRunner`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `PrecargarVictoria` | static | `void` | — | 88 |
| `TocarVictoria` | static | `void` | `float fadeIn = 6f, float fadeOtros = 4f` | 94 |
| `DetenerVictoria` | static | `void` | — | 111 |
| `Tick` | static | `void` | `float dt` | 187 |

**Propiedades:** `GananciaLucha`, `Atenuacion`, `VictoriaSonando`, `VictoriaLista`, `VictoriaGenerando`, `VolumenVictoria`, `VictoriaReproduciendo`, `VolumenDeLoops`, `FactorOtros`

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

## `Assets/_Project/Scripts/Presentation/ObjectiveDiamondMarker.cs`

**Tipos:** `class ObjectiveDiamondMarker`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Crear` | static | `ObjectiveDiamondMarker` | `System.Func<Vector3> obtenerPunto` | 34 |

## `Assets/_Project/Scripts/Presentation/ObstacleMarker.cs`

**Tipos:** `class ObstacleMarker`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Tick` | static | `void` | `float dt` | 49 |
| `ConfigurarVida` |  | `void` | `int vida` | 65 |
| `ConfigurarExplosivo` |  | `void` | `float radio, int dano, float demora = 2.5f` | 68 |
| `ConfigurarSinNavMesh` |  | `void` | — | 76 |
| `ConfigurarSoloExplosiones` |  | `void` | — | 140 |
| `TakeDamage` |  | `void` | `int amount, Vector3? desde = null` | 142 |
| `Demoler` |  | `void` | `Vector3? desde = null, float fuerza = 9f` | 167 |

**Propiedades:** `EsExplosivo`, `EstaEncendido`, `SinNavMesh`, `MaxHealth`, `EsDestruible`, `CurrentHealth`, `IsCollapsed`, `Stage`, `SoloExplosiones`

## `Assets/_Project/Scripts/Presentation/OcultadorDeArma.cs`

**Tipos:** `class OcultadorDeArma`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Ocultar` |  | `void` | `Transform mano, Transform raiz` | 15 |
| `Restaurar` |  | `void` | — | 30 |

**Propiedades:** `Oculta`

## `Assets/_Project/Scripts/Presentation/OndasDeRotor.cs`

**Tipos:** `class OndasDeRotor`, `class Onda`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Tick` |  | `void` | `bool emitir, Vector3 centro, float alfa01, float dt` | 101 |

**Propiedades:** `Emitidas`, `Activas`

## `Assets/_Project/Scripts/Presentation/OrderLineManager.cs`

**Tipos:** `class OrderLineManager`, `class Linea`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Estado` |  | `bool` | `int soldadoId, out float alfa, out bool activa, out float edad` | 43 |
| `AlfaSegunEdad` | static | `float` | `float edad` | 53 |
| `Prewarm` | static | `void` | — | 148 |

**Propiedades:** `Instancia`

## `Assets/_Project/Scripts/Presentation/OrderMarkerFx.cs`

**Tipos:** `class OrderMarkerFx`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ResetIfStale` | static | `void` | — | 66 |
| `LimpiarTodo` | static | `void` | — | 235 |
| `RecycleAll` | static | `void` | — | 255 |
| `Spawn` | static | `void` | `Vector3 position, Color color, int orderIndex, float duration = 0.6f` | 317 |
| `Spawn` | static | `void` | `Vector3 position, Color color, float duration = 0.6f` | 326 |
| `ClearQueuedMarkers` | static | `void` | — | 337 |
| `SpawnPlan` | static | `OrderMarkerFx` | `Vector3 position, Color color, int orderIndex` | 369 |
| `ReleasePlan` | static | `void` | `OrderMarkerFx m` | 379 |
| `HayOrdenPendienteEn` | static | `bool` | `Vector3 punto` | 399 |
| `PurgarCompletados` | static | `int` | — | 425 |
| `Prewarm` | static | `void` | — | 450 |
| `MasCercanoA` | static | `OrderMarkerFx` | `Vector3 punto, float radio = 1.5f` | 606 |
| `Recycle` |  | `void` | — | 755 |

**Propiedades:** `Budget`, `ActiveCount`, `TotalCount`, `EscalaActual`, `AlfaActual`, `AnguloActual`, `PulsoFinal`, `PuntoDeOrden`

## `Assets/_Project/Scripts/Presentation/PantallaDeResultado.cs`

**Tipos:** `class PantallaDeResultado`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Animar` | static | `IEnumerator` | `GameObject panel, List<EstadisticasDeMision.Fila> filas, Text statsVie` | 30 |

**Propiedades:** `Animando`, `FilasMostradas`, `AlfaDeFondoUltimo`

## `Assets/_Project/Scripts/Presentation/ParticleMaterialFactory.cs`

**Tipos:** `class ParticleMaterialFactory`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `CreateTransparent` | static | `Material` | `Color color, Texture2D textura = null` | 36 |
| `MallaEsfera` | static | `Mesh` | — | 89 |
| `MallaCubo` | static | `Mesh` | — | 122 |

## `Assets/_Project/Scripts/Presentation/PatrolRouteLine.cs`

**Tipos:** `class PatrolRouteLine`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Spawn` | static | `PatrolRouteLine` | `Vector3[] points, Color color, float height = 0.05f` | 36 |

**Propiedades:** `Markers`

## `Assets/_Project/Scripts/Presentation/PauseController.cs`

**Tipos:** `class PauseController`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `GameObject pause, GameObject settings` | 49 |
| `ShowPause` |  | `void` | — | 360 |
| `RefrescarBotonesDeGuardado` |  | `void` | — | 438 |
| `OnGuardarClicked` |  | `void` | — | 450 |
| `OnCargarClicked` |  | `void` | — | 459 |
| `OnContinueClicked` |  | `void` | — | 469 |
| `OnSettingsClicked` |  | `void` | — | 489 |
| `OnSettingsBackClicked` |  | `void` | — | 503 |
| `ToggleControlsOverlay` |  | `void` | — | 535 |
| `OnControlsClicked` |  | `void` | — | 545 |
| `OnControlsBackClicked` |  | `void` | — | 554 |
| `OnRebindClicked` |  | `void` | — | 563 |
| `OnRebindBackClicked` |  | `void` | — | 572 |
| `OnRebindResetClicked` |  | `void` | — | 580 |
| `OnMenuClicked` |  | `void` | — | 593 |
| `OnConfirmExitNo` |  | `void` | — | 599 |
| `OnConfirmExitYes` |  | `void` | — | 606 |

**Propiedades:** `IsPaused`, `BotonGuardar`, `BotonCargar`, `TextoDeLaNota`, `IsControlsOverlayOpen`

## `Assets/_Project/Scripts/Presentation/PisoInteractableHighlight.cs`

**Tipos:** `class PisoInteractableHighlight`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Agregar` | static | `PisoInteractableHighlight` | `Transform objetivo, float radio, float yOffsetAlPiso = -0.4f` | 29 |

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

## `Assets/_Project/Scripts/Presentation/PrecalentadoDeCombate.cs`

**Tipos:** `class PrecalentadoDeCombate`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Ejecutar` | static | `void` | — | 17 |

**Propiedades:** `Hecho`, `Resumen`

## `Assets/_Project/Scripts/Presentation/RachaDeBajas.cs`

**Tipos:** `class RachaDeBajas`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `MultiplicadorPara` | static | `float` | `int duenoId` | 42 |
| `NombreDeRacha` | static | `string` | `int n` | 48 |
| `SinEnfriamiento` |  | `void` | — | 129 |

**Propiedades:** `Instancia`, `Multikill`, `Racha`, `FuriaActiva`, `FuriaRestante`, `EnfriamientoRestante`, `SoldadoConFuria`, `AuraVisible`

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
| `ApplyBillboard` |  | `void` | `Quaternion cameraRotation` | 61 |
| `Construir` | static | `RevivePromptView` | `Transform unidad` | 70 |
| `RegistrarTodas` | static | `int` | — | 128 |

**Propiedades:** `IsVisible`

## `Assets/_Project/Scripts/Presentation/RoleIconFactory.cs`

**Tipos:** `class RoleIconFactory`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `For` | static | `Sprite` | `RoleType role` | 21 |
| `WorldIconTexture` | static | `Texture2D` | `RoleType role` | 49 |
| `WorldIconTextureForWeapon` | static | `Texture2D` | `WeaponKind kind` | 73 |

## `Assets/_Project/Scripts/Presentation/RomboVisibilidad.cs`

**Tipos:** `class RomboVisibilidad`

**Propiedades:** `Suprimidos`

## `Assets/_Project/Scripts/Presentation/RutaAlObjetivo.cs`

**Tipos:** `class RutaAlObjetivo`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Crear` | static | `RutaAlObjetivo` | — | 22 |

## `Assets/_Project/Scripts/Presentation/SafeMaterial.cs`

**Tipos:** `class SafeMaterial`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `RegistrarOrigen` | static | `void` | `int saltar = 2` | 26 |
| `Create` | static | `Material` | `Color color` | 91 |
| `CreateLinea` | static | `Material` | `Color color` | 125 |
| `CreateShared` | static | `Material` | — | 140 |

## `Assets/_Project/Scripts/Presentation/SafeMaterialTint.cs`

**Tipos:** `class SafeMaterialTint`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `SetColor` |  | `void` | `Color c` | 23 |

## `Assets/_Project/Scripts/Presentation/SelectionRingFx.cs`

**Tipos:** `class SelectionRingFx`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TrackHealth` |  | `void` | `Soldier soldier` | 25 |
| `SetColor` |  | `void` | `Color c` | 48 |
| `ResetSpawnCount` | static | `void` | — | 64 |
| `Spawn` | static | `SelectionRingFx` | `Transform target, Color color, float radius = 0.75f` | 66 |
| `FlashAcknowledge` |  | `void` | — | 101 |

**Propiedades:** `SpawnCount`

## `Assets/_Project/Scripts/Presentation/SfxSintetico.Faro.cs`

**Tipos:** `class SfxSintetico`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `VidrioRoto` | static | `AudioClip` | — | 9 |

## `Assets/_Project/Scripts/Presentation/SfxSintetico.Graves.cs`

**Tipos:** `class SfxSintetico`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `SubGolpeDatos` | static | `float[]` | `int sr` | 13 |
| `RetumboDeExplosionDatos` | static | `float[]` | `int sr` | 33 |
| `SubGolpe` | static | `AudioClip` | — | 57 |
| `DestruccionVehiculo` | static | `AudioClip` | — | 65 |

## `Assets/_Project/Scripts/Presentation/SfxSintetico.Operacion.cs`

**Tipos:** `class SfxSintetico`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Logro` | static | `AudioClip` | — | 39 |
| `ObjetivoCumplido` | static | `AudioClip` | — | 52 |
| `ImpactoPesado` | static | `AudioClip` | — | 70 |
| `BalaImpacto` | static | `AudioClip` | — | 86 |
| `Orugas` | static | `AudioClip` | — | 117 |
| `MotorCamioneta` | static | `AudioClip` | — | 142 |
| `RuedasCamioneta` | static | `AudioClip` | — | 165 |
| `AmbienteDesierto` | static | `AudioClip` | — | 184 |

## `Assets/_Project/Scripts/Presentation/SfxSintetico.Victoria.cs`

**Tipos:** `class SfxSintetico`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `HimnoDeVictoriaDatos` | static | `float[]` | `int sr = HimnoSR` | 107 |
| `ClipDelHimno` | static | `AudioClip` | `float[] datos, int sr = HimnoSR` | 223 |

## `Assets/_Project/Scripts/Presentation/SfxSintetico.Victoria2.cs`

**Tipos:** `class SfxSintetico`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `HimnoEpicoDatos` | static | `float[]` | `int sr = HimnoEpicoSR` | 167 |
| `ClipDelHimnoEpico` | static | `AudioClip` | `float[] datos, int sr = HimnoEpicoSR` | 360 |

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
| `Reanimar` | static | `AudioClip` | — | 277 |
| `CargaPlantada` | static | `AudioClip` | — | 291 |
| `CargaTic` | static | `AudioClip` | — | 298 |
| `Recarga` | static | `AudioClip` | `WeaponKind arma` | 309 |
| `Desenfundar` | static | `AudioClip` | `WeaponKind arma` | 404 |
| `GatilloVacio` | static | `AudioClip` | `WeaponKind arma` | 422 |

## `Assets/_Project/Scripts/Presentation/ShapeMarkerFx.cs`

**Tipos:** `class ShapeMarkerFx`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `MatCirculo` | static | `Material` | `Color c` | 10 |
| `MaterialDeDestino` | static | `Material` | — | 46 |
| `CrearMarcador` | static | `GameObject` | `Color c, float radio` | 90 |

## `Assets/_Project/Scripts/Presentation/SiluetasDeMinimapa.cs`

**Tipos:** `class SiluetaMinimapa`, `class SiluetasDeMinimapa`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Registrar` | static | `int` | — | 39 |

**Propiedades:** `Nombres`, `Cantidad`, `Raiz`

## `Assets/_Project/Scripts/Presentation/SoldierAnimatorDriver.cs`

**Tipos:** `class SoldierAnimatorDriver`

**Propiedades:** `PesoCapaDisparoActual`, `AlturaMinimaDeApoyo`, `DesfaseDelModelo`

## `Assets/_Project/Scripts/Presentation/SoldierGrenadeAnim.cs`

**Tipos:** `class SoldierGrenadeAnim`

**Propiedades:** `Activo`, `Progreso`, `Mano`

## `Assets/_Project/Scripts/Presentation/SoldierMeleeAnim.cs`

**Tipos:** `class SoldierMeleeAnim`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Iniciar` |  | `bool` | — | 59 |

**Propiedades:** `Activo`, `Progreso`, `Mano`, `Cuchillo`

## `Assets/_Project/Scripts/Presentation/SonidosDeOperacion.cs`

**Tipos:** `class SonidosDeOperacion`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `IniciarAmbiente` | static | `void` | `float volumen = 0.5f` | 89 |
| `DetenerAmbiente` | static | `void` | — | 107 |

**Propiedades:** `Instancia`, `Logros`, `VehiculosConSonido`, `AmbienteActivo`

## `Assets/_Project/Scripts/Presentation/SparkleBurstFx.cs`

**Tipos:** `class SparkleBurstFx`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Spawn` | static | `void` | `Vector3 position, Color color, float duration = 0.8f, float radius = 2` | 100 |
| `Tick` | static | `void` | — | 145 |
| `ClearAll` | static | `void` | — | 158 |

**Propiedades:** `ActiveCount`

## `Assets/_Project/Scripts/Presentation/SpriteFx.cs`

**Tipos:** `class SpritesReales`, `class SpriteFx`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Obtener` | static | `Sprite` | `string nombre` | 17 |
| `Precargar` | static | `int` | — | 31 |
| `LimpiarTodo` | static | `void` | — | 57 |
| `Precalentar` | static | `void` | — | 65 |
| `Lanzar` | static | `SpriteFx` | `string sprite, Vector3 pos, Color color, float tam0, float tam1, float` | 100 |
| `Explosion` | static | `int` | `Vector3 pos, float radio` | 151 |
| `Golpe` | static | `void` | `Vector3 pos, Color color, float tam` | 182 |

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
| `Lado` | static | `string` | `Vector3 oyente, float yawGrados, Vector3 fuente` | 45 |
| `Describir` | static | `string` | `string nombreDeClip, Vector3 oyente, float yawGrados, Vector3 fuente` | 54 |
| `Anunciar` | static | `void` | `AudioClip clip, Vector3 posicion` | 64 |

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

## `Assets/_Project/Scripts/Presentation/UnitLocatorCylinder.cs`

**Tipos:** `class UnitLocatorCylinder`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 97 |

**Propiedades:** `EscalaActual`, `MarcadorVisible`

## `Assets/_Project/Scripts/Presentation/VehicleFxReactor.cs`

**Tipos:** `class VehicleFxReactor`, `class VehicleSmokePuff`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bootstrap` |  | `void` | — | 33 |
| `Begin` |  | `void` | `Vector3 origin` | 204 |

## `Assets/_Project/Scripts/Presentation/VehicleMountIndicator.cs`

**Tipos:** `class VehicleMountIndicator`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Create` | static | `VehicleMountIndicator` | — | 27 |
| `Show` |  | `void` | `Vehicle vehicle, IEnumerable<Soldier> incomingAllies, bool puedeMontar` | 131 |
| `Hide` |  | `void` | — | 151 |

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
| `EnsurePopulated` | static | `void` | — | 331 |
| `Clear` | static | `void` | — | 348 |

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
| `AlfaSegunDistanciaAMira` | static | `float` | `float distanciaPx` | 30 |
| `Crear` | static | `TutorialBeacon` | `string texto, Color color, Vector3 posicion, Transform sigue = null, f` | 40 |
| `QuitarTodas` | static | `void` | — | 53 |
| `Quitar` |  | `void` | — | 60 |
| `OcultarColumna` |  | `void` | — | 72 |

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
| `SetVisible` |  | `void` | `bool visible` | 243 |
| `Bind` |  | `void` | `Text prompt, Image cross` | 258 |
| `BindSoldierInfo` |  | `void` | `GameObject panel, Text info` | 274 |
| `BindVehicleInfo` |  | `void` | `GameObject panel, Image[] squares` | 289 |
| `Initialize` |  | `void` | — | 297 |
| `SetWatchedShooter` |  | `void` | `int soldierId` | 387 |
| `UpdateFromAimResult` |  | `void` | `AimResult result` | 553 |

**Propiedades:** `CurrentAimTintColor`, `CurrentPrompt`, `IsVisible`, `CurrentPulseFrequency`

## `Assets/_Project/Scripts/UI/AjustesDeJuego.cs`

**Tipos:** `class AjustesDeJuego`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TextoEscala` | static | `string` | — | 26 |
| `SiguienteEscala` | static | `void` | — | 27 |
| `PonerEscala` | static | `void` | `float e` | 32 |
| `AplicarEscala` | static | `void` | — | 38 |
| `Resoluciones` | static | `List<Resolution>` | — | 84 |
| `IndiceResolucion` | static | `int` | — | 98 |
| `TextoResolucion` | static | `string` | — | 106 |
| `AlternarPantallaCompleta` | static | `void` | — | 108 |
| `SiguienteResolucion` | static | `void` | — | 109 |
| `AplicarPantalla` | static | `void` | — | 111 |
| `PonerDaltonismo` | static | `void` | `bool v` | 120 |
| `PonerHudMinimo` | static | `void` | `bool v` | 121 |
| `Adaptar` | static | `Color` | `Color c` | 124 |

**Propiedades:** `Daltonismo`, `HudMinimo`, `Escala`, `PantallaCompleta`

## `Assets/_Project/Scripts/UI/AlertQueue.cs`

**Tipos:** `enum AlertPriority`, `struct PendingAlert`, `class AlertQueue`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Clear` | static | `void` | — | 79 |
| `Push` | static | `void` | `string message, AlertPriority priority, float seconds` | 87 |
| `PushAt` | static | `void` | `string message, AlertPriority priority, float seconds, float now` | 93 |
| `TryDequeue` | static | `bool` | `out string message, out float seconds` | 132 |
| `NotifyFinished` | static | `void` | — | 162 |
| `SelectNext` | static | `int` | `PendingAlert[] pending, float now` | 180 |
| `IsStale` | static | `bool` | `PendingAlert alert, float now` | 203 |

**Propiedades:** `PendingCount`, `IsBusy`, `CurrentPriority`

## `Assets/_Project/Scripts/UI/AliadosSinEstorbo.cs`

**Tipos:** `class AliadosSinEstorbo`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `void` | — | 24 |
| `Actualizar` |  | `void` | `Camera cam` | 32 |

**Propiedades:** `Ocultos`, `Instancia`

## `Assets/_Project/Scripts/UI/AvisoUnico.cs`

**Tipos:** `class AvisoUnico`, `struct Aviso`, `struct Fijado`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Normalizar` | static | `string` | `string t` | 58 |
| `Mostrar` | static | `void` | `string mensaje, float segundos = 3f, Color? color = null, int priorida` | 66 |
| `Fijo` | static | `void` | `string clave, string mensaje, Color? color = null` | 88 |
| `FijoPermanente` | static | `void` | `string clave, string mensaje, Color? color = null` | 99 |
| `FijoVibrante` | static | `void` | `string clave, string mensaje, Color? color = null` | 106 |
| `QuitarFijo` | static | `void` | `string clave` | 112 |
| `Limpiar` | static | `void` | — | 113 |

**Propiedades:** `TextoActual`, `Visible`, `Historial`, `HayTemporal`

## `Assets/_Project/Scripts/UI/ButtonSfx.cs`

**Tipos:** `class ButtonSfx`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `OnPointerEnter` |  | `void` | `PointerEventData eventData` | 51 |
| `OnPointerExit` |  | `void` | `PointerEventData eventData` | 58 |
| `Attach` | static | `void` | `Button button` | 87 |

## `Assets/_Project/Scripts/UI/CapasDeHud.cs`

**Tipos:** `class CapasDeHud`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ActivarModo` | static | `void` | `ControlMode modo` | 26 |

**Propiedades:** `Instancia`

## `Assets/_Project/Scripts/UI/CartelDeRacha.cs`

**Tipos:** `class CartelDeRacha`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Mostrar` | static | `void` | `string texto, int nivelDeRacha` | 72 |
| `MostrarFuria` | static | `void` | `string texto` | 86 |

**Propiedades:** `Instancia`, `Visible`, `Texto`, `FuriaVisible`, `TextoFuria`, `EscalaActual`

## `Assets/_Project/Scripts/UI/CartelVolverView.cs`

**Tipos:** `class CartelVolverView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Crear` | static | `CartelVolverView` | — | 20 |

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
| `For` | static | `IEnumerable<ControlEntry>` | `ControlContext ctx` | 183 |
| `LineFor` | static | `string` | `ControlContext ctx` | 193 |
| `LineFor` | static | `string` | `ControlContext ctx, int maxEntries` | 196 |
| `FullText` | static | `string` | — | 212 |
| `DisplayKeyFor` | static | `string` | `ControlEntry e` | 226 |
| `HeaderFor` | static | `string` | `ControlContext ctx` | 235 |
| `FormatKey` | static | `string` | `string key` | 250 |
| `Validate` | static | `bool` | `out string problem` | 262 |

## `Assets/_Project/Scripts/UI/DamageDirectionView.cs`

**Tipos:** `class DamageDirectionView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Image arrowImage, PlayerBrain playerBrain` | 21 |
| `Initialize` |  | `void` | — | 99 |

## `Assets/_Project/Scripts/UI/DamageVignetteView.cs`

**Tipos:** `class DamageVignetteView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Image img, PlayerBrain playerBrain` | 23 |
| `SetSpeedFraction` |  | `void` | `float frac01` | 121 |

**Propiedades:** `CurrentAlpha`, `CurrentRedAlpha`

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
| `AcomodarSelectionCount` | static | `void` | `GameObject countPanel` | 96 |
| `ContarSolapes` | static | `int` | `GameObject panel` | 115 |

## `Assets/_Project/Scripts/UI/EstadoIconFactory.cs`

**Tipos:** `enum EstadoDeFila`, `class EstadoIconFactory`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Para` | static | `Sprite` | `EstadoDeFila e` | 15 |
| `Disco` | static | `Sprite` | — | 26 |

## `Assets/_Project/Scripts/UI/EstiloDeAjustes.cs`

**Tipos:** `class EstiloDeAjustes`, `class SliderDeAjuste`, `class InterruptorDeAjuste`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Redondeado` | static | `Sprite` | — | 33 |
| `Contorno` | static | `Sprite` | — | 38 |
| `Circulo` | static | `Sprite` | — | 43 |
| `AcentoDe` | static | `Color` | `string nombre` | 88 |
| `Vestir` | static | `void` | `RectTransform panel` | 96 |
| `VestirBoton` | static | `void` | `Transform boton, bool principal = false` | 259 |
| `Configurar` |  | `void` | `Slider s, Image pistaIm, RectTransform perillaRt, Image nucleoIm, Imag` | 295 |
| `OnPointerEnter` |  | `void` | `PointerEventData e` | 323 |
| `OnPointerExit` |  | `void` | `PointerEventData e` | 324 |
| `OnPointerDown` |  | `void` | `PointerEventData e` | 325 |
| `OnPointerUp` |  | `void` | `PointerEventData e` | 326 |
| `Configurar` |  | `void` | `Toggle t, Image fondoIm, RectTransform perillaRt` | 359 |
| `OnPointerEnter` |  | `void` | `PointerEventData e` | 375 |
| `OnPointerExit` |  | `void` | `PointerEventData e` | 376 |

**Propiedades:** `Apretado`

## `Assets/_Project/Scripts/UI/FlechaDeObjetivo.cs`

**Tipos:** `class FlechaDeObjetivo`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Crear` | static | `FlechaDeObjetivo` | `Transform objetivo, string nombre, Color color` | 28 |
| `Quitar` |  | `void` | — | 61 |

**Propiedades:** `Visible`, `EnBorde`, `TextoActual`

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
| `Bind` |  | `void` | `Text[] slots` | 34 |
| `SetGroups` |  | `void` | `IReadOnlyList<List<Soldier>> groups` | 42 |
| `Refresh` |  | `void` | — | 59 |
| `Summarize` | static | `void` | `List<Soldier> group, out int vivos, out float vidaPromedio` | 98 |

## `Assets/_Project/Scripts/UI/HeadshotPopup.cs`

**Tipos:** `class HeadshotPopup`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Mostrar` | static | `void` | `bool mata` | 16 |

**Propiedades:** `Visible`, `Texto`

## `Assets/_Project/Scripts/UI/IndicadorDeExposicion.cs`

**Tipos:** `class IndicadorDeExposicion`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Crear` | static | `IndicadorDeExposicion` | `Transform raiz` | 18 |

## `Assets/_Project/Scripts/UI/InstructionBannerView.cs`

**Tipos:** `class InstructionBannerView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Text text` | 22 |
| `SetText` |  | `void` | `string message` | 32 |

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
| `NameOf` | static | `string` | `string actionId` | 162 |

**Propiedades:** `IsListening`

## `Assets/_Project/Scripts/UI/KillFeedView.cs`

**Tipos:** `class KillFeedView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Text text` | 20 |
| `ShowKill` |  | `void` | — | 40 |

## `Assets/_Project/Scripts/UI/LayoutDeAjustes.cs`

**Tipos:** `class LayoutDeAjustes`, `struct Resultado`, `struct Fila`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `LayoutDeAjustes` | `GameObject settingsPanel` | 48 |
| `AreaDelCanvas` |  | `Vector2` | — | 89 |
| `Aplicar` |  | `void` | — | 97 |
| `Aplicar` |  | `void` | `Vector2 area` | 99 |
| `Diagnosticar` | static | `List<string>` | `GameObject settingsPanel, Vector2 area` | 241 |

**Propiedades:** `Ultimo`, `Aplicaciones`

## `Assets/_Project/Scripts/UI/LowHealthPulseView.cs`

**Tipos:** `class LowHealthPulseView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Image image` | 58 |

**Propiedades:** `CurrentAlpha`

## `Assets/_Project/Scripts/UI/MarcaDeImpacto.cs`

**Tipos:** `class MarcaDeImpacto`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Mostrar` |  | `void` | `bool esBaja, bool conHeadshot = false` | 165 |

**Propiedades:** `Instancia`, `Mostradas`, `UltimaFueBaja`, `UltimaFueHeadshot`, `Visible`, `EsBaja`, `EsHeadshot`, `DuracionActual`, `AnilloVisible`, `AnilloDobleVisible`, `RadioDelAnillo`, `RayasActivas`, `EscalaActual`

## `Assets/_Project/Scripts/UI/MenuDeOrdenes.cs`

**Tipos:** `class ContextoRadial`, `class MenuDeOrdenes`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Mostrar` |  | `void` | `int cat, bool contextual, params int[] opciones` | 29 |
| `ReiniciarActivo` | static | `void` | — | 58 |
| `RegistrarActivo` |  | `void` | — | 59 |
| `PonerPista` | static | `void` | `int categoria, int opcion = -1` | 171 |
| `EsVisible` |  | `bool` | `int categoria` | 186 |
| `EsContextual` |  | `bool` | `int categoria` | 187 |
| `Bind` |  | `void` | `Text texto, CanvasGroup canvasGroup` | 190 |
| `PonerSoldados` |  | `void` | `string s1, string s2, string s3, bool m1 = false, bool m2 = false, boo` | 217 |
| `NombreDeOpcion` | static | `string` | `int categoria, int sub` | 232 |
| `NombreCorto` | static | `string` | `int categoria` | 241 |
| `Partir` | static | `string` | `string s` | 251 |
| `Abrir` |  | `void` | — | 292 |
| `Abrir` |  | `void` | `ContextoRadial ctx` | 297 |
| `CancelarSeleccion` |  | `void` | — | 339 |
| `Cerrar` |  | `void` | — | 348 |
| `MoverSeleccion` |  | `void` | `Vector2 delta` | 358 |
| `ElegirDirecto` |  | `void` | `int categoria, int sub = -1` | 410 |
| `CategoriaDeTecla` |  | `int` | `int tecla` | 444 |
| `AsegurarEnEscena` | static | `MenuDeOrdenes` | — | 575 |
| `Construir` | static | `MenuDeOrdenes` | `Transform padre` | 716 |
| `LeerTecla` | static | `int` | — | 786 |

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
| `TryMinimapPointToWorld` |  | `bool` | `Vector2 localPoint, out Vector3 world` | 156 |
| `TryWorldToMinimapPoint` |  | `bool` | `Vector3 world, out Vector2 localPoint` | 196 |
| `AplicarTamanoInicial` |  | `void` | — | 349 |
| `AplicarTamanoGuardado` |  | `void` | — | 370 |
| `AlternarTamano` |  | `bool` | — | 375 |
| `TamanoFijo` |  | `Vector2` | `int indice` | 389 |
| `CiclarTamanoFijo` |  | `int` | — | 395 |

**Propiedades:** `Activo`, `MinimapRect`, `MinimapCamera`, `GroundY`, `MapHalfExtent`, `Giro`, `Agrandado`, `Marco`, `IndiceTamanoFijo`

## `Assets/_Project/Scripts/UI/MiraDeTorreta.cs`

**Tipos:** `class MiraDeTorreta`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Ocultar` | static | `void` | — | 46 |
| `Actualizar` | static | `void` | `Transform canvasRoot, Camera cam, TurretWeapon arma, Transform vehicul` | 53 |

**Propiedades:** `Visible`, `TextoDelBlanco`, `ApuntaAEnemigo`, `EstadoDeCruceta`, `ColorDeCruceta`, `VidaDelBlanco01`, `Recarga01`, `PosicionRomboPx`

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
| `Refresh` |  | `void` | — | 40 |

## `Assets/_Project/Scripts/UI/ModeToastView.cs`

**Tipos:** `class ModeToastView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `SubirSobreElTemporizador` | static | `void` | `bool subir` | 21 |
| `Bind` |  | `void` | `Text text, CanvasGroup canvasGroup` | 33 |
| `Show` |  | `void` | `string text, float fadeSeconds = 1f` | 54 |

## `Assets/_Project/Scripts/UI/ModoDiosView.cs`

**Tipos:** `class ModoDiosView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 14 |
| `RegistrarActivo` |  | `void` | — | 15 |
| `Asegurar` | static | `ModoDiosView` | `Transform canvasRoot` | 31 |

**Propiedades:** `Activo`

## `Assets/_Project/Scripts/UI/OffscreenAllyMarkerView.cs`

**Tipos:** `class OffscreenAllyMarkerView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Image[] arrows` | 80 |
| `SetSquad` |  | `void` | `IEnumerable<Soldier> soldiers` | 86 |

**Propiedades:** `VisibleMarkerCount`

## `Assets/_Project/Scripts/UI/OffscreenKillMarkerView.cs`

**Tipos:** `class OffscreenKillMarkerView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Image image` | 23 |
| `Report` |  | `void` | `Vector3 worldPosition` | 30 |

**Propiedades:** `IsShowing`, `ArrowPosition`

## `Assets/_Project/Scripts/UI/PanelAjustesExtra.cs`

**Tipos:** `class PanelAjustesExtra`, `class SelectorSfx`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Preparar` | static | `void` | `GameObject settingsPanel` | 18 |
| `Refrescar` | static | `void` | `Transform panel` | 159 |
| `AplicarHudMinimo` | static | `void` | `Transform canvas` | 178 |
| `OnPointerEnter` |  | `void` | `UnityEngine.EventSystems.PointerEventData e` | 194 |
| `OnPointerClick` |  | `void` | `UnityEngine.EventSystems.PointerEventData e` | 195 |

## `Assets/_Project/Scripts/UI/PanelDeMando.cs`

**Tipos:** `class PanelDeMando`, `struct Paso`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `PanelDeMando` | — | 43 |
| `Quitar` | static | `void` | — | 53 |
| `MostrarBotonSoltar` |  | `void` | `bool si` | 121 |
| `PulsarBotonSoltar` |  | `void` | — | 122 |
| `Fijar` |  | `void` | `IList<Paso> pasos, IList<bool> hechos, int actual` | 125 |
| `Aviso` |  | `void` | `string texto, Color color` | 151 |
| `Mostrar` |  | `void` | `bool si` | 162 |

**Propiedades:** `Instancia`, `Actual`, `Hechos`, `TextoActual`, `TextoAviso`, `Visible`, `Filas`, `RectDelPanel`, `BotonSoltarVisible`

## `Assets/_Project/Scripts/UI/PerfHudView.cs`

**Tipos:** `class PerfHudView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Text text` | 42 |
| `Toggle` |  | `void` | — | 48 |

**Propiedades:** `Visible`, `MedianMs`, `P95Ms`

## `Assets/_Project/Scripts/UI/PhaseBannerView.cs`

**Tipos:** `class PhaseBannerView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Text text` | 14 |
| `Show` |  | `void` | `string message, float holdSeconds = 2.2f` | 43 |

## `Assets/_Project/Scripts/UI/PlayerHealthView.cs`

**Tipos:** `class PlayerHealthView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Text text, Image fillImage` | 24 |
| `UpdateFrom` |  | `void` | `Soldier soldier` | 48 |

## `Assets/_Project/Scripts/UI/RadialIconFactory.cs`

**Tipos:** `class RadialIconFactory`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Calavera` | static | `Sprite` | — | 26 |
| `ForCategoria` | static | `Sprite` | `int categoria` | 29 |
| `ForOpcion` | static | `Sprite` | `int categoria, int sub` | 49 |

## `Assets/_Project/Scripts/UI/RespawnPromptView.cs`

**Tipos:** `class RespawnPromptView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Mostrar` | static | `void` | — | 59 |
| `Ocultar` | static | `void` | — | 68 |
| `Tick` | static | `void` | — | 78 |

## `Assets/_Project/Scripts/UI/RosterRowView.cs`

**Tipos:** `class RosterRowView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ClicSimple` |  | `void` | — | 190 |
| `Bind` |  | `void` | `Soldier soldier, int index, bool milicia = false` | 198 |
| `OnPointerClick` |  | `void` | `PointerEventData eventData` | 399 |

**Propiedades:** `Soldier`, `SoldierId`, `Index`, `Estado`, `EsMilicia`, `IsHighlighted`

## `Assets/_Project/Scripts/UI/RosterView.cs`

**Tipos:** `class RosterView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ReiniciarActivo` | static | `void` | — | 17 |
| `RegistrarActivo` |  | `void` | — | 18 |
| `SetRowPrefab` |  | `void` | `RosterRowView prefab` | 26 |
| `Rebuild` |  | `void` | — | 45 |

**Propiedades:** `Activo`

## `Assets/_Project/Scripts/UI/ScreenFlashView.cs`

**Tipos:** `class ScreenFlashView`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Image image` | 50 |
| `Flash` |  | `void` | `Color color, float peakAlpha, float seconds` | 88 |
| `Explosion` | static | `void` | `float intensity01` | 169 |
| `ModeChange` | static | `void` | — | 195 |

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
| `SetModeVisible` |  | `void` | `bool allowed` | 39 |

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

## `Assets/_Project/Scripts/UI/TablaDeControles.cs`

**Tipos:** `class TablaDeControles`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Firma` | static | `int` | — | 28 |
| `Existente` | static | `RectTransform` | `RectTransform contenedor` | 37 |
| `Contenido` | static | `RectTransform` | `RectTransform contenedor` | 44 |
| `Construir` | static | `RectTransform` | `RectTransform contenedor, float arriba = 60f, float abajo = 76f, float` | 54 |
| `Asegurar` | static | `RectTransform` | `RectTransform contenedor, float arriba = 60f, float abajo = 76f, float` | 125 |
| `Tokens` | static | `List<string>` | `string crudo` | 165 |

## `Assets/_Project/Scripts/UI/TurretAimView.cs`

**Tipos:** `class TurretAimView`, `enum TipoDeImpacto`, `struct Impacto`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bind` |  | `void` | `Image reticleImage, Image gap, Image cooldown, LineRenderer ring` | 31 |
| `SetVisible` |  | `void` | `bool visible` | 61 |
| `UpdateFrom` |  | `void` | `TurretWeapon turret` | 68 |
| `ColorDe` | static | `Color` | `TipoDeImpacto t` | 156 |
| `PredictedImpactPoint` | static | `Vector3` | `TurretWeapon turret` | 166 |
| `Predecir` | static | `Impacto` | `TurretWeapon turret` | 177 |
| `ClasificarCollider` | static | `TipoDeImpacto` | `Collider c` | 262 |

**Propiedades:** `UltimoTipo`, `UltimoPunto`, `UltimaNormal`, `UltimoRadioDibujado`, `UltimoAnchoDibujado`, `UltimoColor`

## `Assets/_Project/Scripts/UI/VehicleKeysPanel.cs`

**Tipos:** `class VehicleKeysPanel`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Asegurar` | static | `VehicleKeysPanel` | `Transform canvasRaiz` | 46 |
| `SetExpandido` |  | `void` | `bool v` | 142 |
| `SetVisible` |  | `void` | `bool v` | 147 |
| `Destellar` |  | `void` | `string tecla` | 153 |
| `Actualizar` |  | `void` | `Vehicle v, VehicleSeatRole? miAsiento, int aliadosEnCamino` | 162 |

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
| `ReiniciarActivo` | static | `void` | — | 15 |
| `RegistrarActivo` |  | `void` | — | 18 |
| `IconFor` | static | `Sprite` | `WeaponKind kind` | 39 |
| `EnsureIcon` |  | `Image` | — | 73 |
| `Bind` |  | `void` | `Text text, Image fillImage` | 105 |
| `UpdateFrom` |  | `void` | `WeaponHolder weapon` | 168 |
| `EnsureExtras` |  | `void` | — | 323 |

**Propiedades:** `Activo`, `Texto`, `FuenteDelContador`, `Icon`

## `Assets/_Project/Scripts/Vehicles/Atropello.cs`

**Tipos:** `class Atropello`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Barrer` | static | `int` | `Transform vehiculo, Collider casco, float velocidad, Vehicle datos` | 37 |
| `AplastarObstaculos` | static | `int` | `Transform vehiculo, float velocidad, float radioDelCasco` | 89 |
| `EquipoDeLaTripulacion` | static | `TeamId?` | `Vehicle datos` | 117 |
| `Derribar` | static | `void` | `Soldier victima, Vector3 direccion` | 130 |

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
| `De` | static | `TorretaFija` | `Soldier s` | 84 |
| `MasCercana` | static | `TorretaFija` | `Vector3 p, float radio` | 91 |
| `Ocupar` |  | `bool` | `Soldier s, out string motivo` | 106 |
| `Liberar` |  | `void` | — | 142 |
| `AcotarGiro` |  | `void` | `Soldier s` | 159 |
| `FraccionDeArco` |  | `float` | `Soldier s` | 169 |
| `InstalarEnEscena` | static | `int` | — | 205 |
| `Instalar` | static | `TorretaFija` | `GameObject go` | 221 |

**Propiedades:** `Todas`, `Ocupante`, `Libre`, `Puesto`, `YawCentro`

## `Assets/_Project/Scripts/Vehicles/TurretAI.cs`

**Tipos:** `class TurretAI`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `ConfigurarAsiento` |  | `void` | `VehicleSeatRole nuevo, float alcance` | 21 |
| `Bootstrap` |  | `void` | — | 85 |
| `Tick` |  | `void` | `float dt` | 108 |

**Propiedades:** `Asiento`, `Disparos`, `Silenciada`, `IsEngaging`, `TargetVehicle`

## `Assets/_Project/Scripts/Vehicles/TurretWeapon.cs`

**Tipos:** `class TurretWeapon`, `enum AmmoType`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `CycleAmmo` |  | `void` | — | 62 |
| `IsOnTarget` |  | `bool` | `float toleranceDeg = 4f` | 125 |
| `Bootstrap` |  | `void` | — | 134 |
| `AddDesiredYaw` |  | `void` | `float delta` | 164 |
| `AddDesiredPitch` |  | `void` | `float delta` | 179 |
| `ElevarHacia` |  | `void` | `Vector3 punto` | 188 |
| `FijarLimitesDeElevacion` |  | `void` | `float arriba, float abajo` | 200 |
| `TickPlayerAim` |  | `void` | `float dt` | 210 |
| `SetPool` |  | `void` | `ProjectilePool projectilePool` | 262 |
| `RotateYaw` |  | `void` | `float yawDelta` | 264 |
| `AimAt` |  | `void` | `Vector3 worldPoint, float dt` | 274 |
| `IsAimedAt` |  | `bool` | `Vector3 worldPoint, float toleranceDeg = 4f` | 308 |
| `Tick` |  | `void` | `float dt` | 317 |
| `ResolverTirador` |  | `void` | `out int shooterId, out TeamId team` | 352 |
| `TryFire` |  | `bool` | — | 366 |

**Propiedades:** `PitchActual`, `MinPitch`, `MaxPitch`, `Ammo`, `ExplosionRadius`, `CurrentDamage`, `CurrentProjectileColor`, `ProjectileGravity`, `Heat`, `EffectiveCooldown`, `CooldownFraction01`, `DesiredYaw`, `DesiredPitch`, `YawMundo`, `YawGapDeg`, `CuerpoNivelado`, `MuzzleIsNearGround`

## `Assets/_Project/Scripts/Vehicles/Vehicle.cs`

**Tipos:** `class Vehicle`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `TakeDamage` |  | `void` | `int amount, int attackerId` | 59 |
| `PonerVida01` |  | `void` | `float f` | 99 |
| `DestruirPorGuion` |  | `void` | — | 103 |
| `AsignarBando` |  | `void` | `SP.Combat.TeamId bando, Color tinte` | 184 |
| `MarcarBando` |  | `void` | `SP.Combat.TeamId bando` | 202 |
| `FinalExplosion` |  | `void` | — | 244 |
| `IsMountAnimating` |  | `bool` | `Soldier soldier` | 320 |
| `RefreshOccupancyColor` |  | `void` | — | 356 |
| `Hostil` |  | `bool` | `SP.Combat.TeamId para` | 412 |
| `ClosestBoardingPoint` |  | `Vector3` | `Vector3 desde` | 447 |
| `IsSeatFree` |  | `bool` | `VehicleSeatRole role` | 467 |
| `FirstFreeSeat` |  | `VehicleSeatRole?` | — | 469 |
| `ConfigurarTripulacion` |  | `void` | `Soldier[] tripulantes, VehicleSeatRole[] roles, bool enemigo` | 484 |
| `PuedeAbordar` |  | `bool` | `Soldier soldier, out string motivo` | 510 |
| `PuedeAbordar` |  | `bool` | `Soldier soldier` | 518 |
| `Mount` |  | `bool` | `Soldier soldier, VehicleSeatRole? preferredRole = null, bool instantan` | 520 |
| `Dismount` |  | `bool` | `Soldier soldier` | 758 |
| `SwapSeats` |  | `bool` | `Soldier a, Soldier b` | 865 |
| `MoveToSeat` |  | `bool` | `Soldier soldier, VehicleSeatRole role` | 890 |
| `RoleOf` |  | `VehicleSeatRole?` | `Soldier soldier` | 932 |
| `SoldierInSeat` |  | `Soldier` | `VehicleSeatRole role` | 957 |

**Propiedades:** `Health`, `IsDestroyed`, `PisoDeVida01`, `PisoDeVida`, `DanoDeExplosionEnCurso`, `IsInAgony`, `TorretaCanon`, `TorretaMetralleta`, `FinalExplosionDone`, `Bando`, `PlayerAboard`, `Capacity`, `OccupantCount`, `EsAutonomo`, `HasAnyRoom`, `Driver`, `Gunner`, `Occupants`, `EsTanqueEnemigo`, `AllSeatRoles`

## `Assets/_Project/Scripts/Vehicles/VehicleAudioFeedback.cs`

**Tipos:** `class VehicleAudioFeedback`

**Propiedades:** `UsaGrabacionReal`

## `Assets/_Project/Scripts/Vehicles/VehicleBrain.cs`

**Tipos:** `class VehicleBrain`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Bootstrap` |  | `void` | — | 38 |
| `IssueMoveOrder` |  | `void` | `Vector3 point` | 67 |
| `ConfigurarPatrulla` |  | `void` | `Vector3[] puntos, bool frenarSiCombate, float gasTope` | 86 |
| `Stop` |  | `void` | — | 120 |
| `Tick` |  | `void` | `float dt` | 197 |

**Propiedades:** `HasOrder`, `CurrentDestination`, `IsPlayerDriving`, `Route`, `RouteIndex`, `TienePatrulla`, `IndiceDePatrulla`

## `Assets/_Project/Scripts/Vehicles/VehicleMotor.cs`

**Tipos:** `class VehicleMotor`

**Metodos publicos**

| Metodo |  | Devuelve | Argumentos | Linea |
|---|---|---|---|---|
| `Configurar` |  | `void` | `float velocidadMaxima, float aceleracionNueva = -1f` | 29 |
| `Drive` |  | `void` | `float throttle, float steer, float dt` | 36 |
| `Brake` |  | `void` | `float dt` | 58 |
| `Nudge` |  | `void` | `Vector3 worldDelta` | 162 |

**Propiedades:** `CurrentSpeed`, `MaxSpeed`, `Aceleracion`, `IsStopped`
