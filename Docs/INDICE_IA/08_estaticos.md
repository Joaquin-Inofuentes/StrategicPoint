# Mapa de estado estatico

> Este proyecto corre con **"Enter Play Mode sin recarga de dominio"**: los `static` sobreviven de una
> partida a la siguiente dentro de la misma sesion de Editor. Cualquier estatico mutable que no se
> restablezca es una fuga de estado entre corridas — y la causa de los falsos fallos mas dificiles de
> encontrar.

**180 estaticos mutables fuera de `Editor/`:
19 cubiertos por `ReinicioDeEstaticos`,
40 en archivos con hook propio (hay que mirar si el hook cubre ESE campo),
y 121 sin nada.**

| Campo | Tipo | Donde | Quien lo restablece |
|---|---|---|---|
| `nextId` | `int` | `Soldier.cs:18` | **nada** |
| `matCivil` | `Material` | `SoldierLook.cs:53` | **nada** |
| `Humanizada` | `bool` | `AiBrain.Disparo.cs:31` | **nada** |
| `proximoAvisoRadio` | `float` | `AiBrain.Granadas.cs:25` | **nada** |
| `ultimoAvisoDeteccion` | `float` | `AiBrain.Tactica.cs:87` | **nada** |
| `ultimoAvisoDeSeguir` | `float` | `AiBrain.Tactica.cs:219` | **nada** |
| `DistanciaParaSeguir` | `float` | `AjustesDeEscuadra.cs:31` | `ReinicioDeEstaticos` |
| `DistanciaParaDetenerse` | `float` | `AjustesDeEscuadra.cs:32` | `ReinicioDeEstaticos` |
| `SeguirDesdeCobertura` | `bool` | `AjustesDeEscuadra.cs:33` | `ReinicioDeEstaticos` |
| `Lider` | `Soldier` | `AjustesDeEscuadra.cs:37` | `ReinicioDeEstaticos` |
| `Correr` | `bool` | `AjustesDeEscuadra.cs:41` | `ReinicioDeEstaticos` |
| `LodPorDistancia` | `bool` | `WorldSimulationDriver.cs:54` | **nada** |
| `tickNumero` | `int` | `WorldSimulationDriver.cs:56` | **nada** |
| `cached` | `bool?` | `CameraFxSettings.cs:23` | **nada** |
| `RegeneracionPermitida` | `bool` | `Health.cs:33` | `ReinicioDeEstaticos` |
| `nextInstanceId` | `int` | `Projectile.cs:40` | `ReinicioDeEstaticos` |
| `ReservasActivas` | `bool` | `WeaponHolder.cs:161` | `ReinicioDeEstaticos` |
| `metralletaVehiculo` | `GameObject` | `WeaponModels.cs:127` | **nada** |
| `metralletaVehiculoCargada` | `bool` | `WeaponModels.cs:128` | **nada** |
| `All` | `IReadOnlyList<Soldier>` | `ActorRegistry.cs:42` | **nada** |
| `proximoBarrido` | `float` | `ActorRegistry.cs:49` | **nada** |
| `guardada` | `Camera` | `CamaraPrincipal.cs:9` | **nada** |
| `Puntos` | `IReadOnlyList<Vector3>` | `Coberturas.cs:34` | **nada** |
| `Duenos` | `IReadOnlyList<Collider>` | `Coberturas.cs:35` | **nada** |
| `Cantidad` | `int` | `Coberturas.cs:36` | **nada** |
| `root` | `Transform` | `Coberturas.cs:39` | **nada** |
| `marcasVisibles` | `bool` | `Coberturas.cs:310` | **nada** |
| `MarcasVisibles` | `bool` | `Coberturas.cs:311` | **nada** |
| `actual` | `NivelDificultad?` | `Dificultad.cs:31` | hook propio *(revisar si cubre este campo)* |
| `FuegoAmigoExplosivo` | `bool` | `Dificultad.cs:36` | hook propio *(revisar si cubre este campo)* |
| `PerfilActual` | `Perfil` | `Dificultad.cs:69` | hook propio *(revisar si cubre este campo)* |
| `FilePath` | `string` | `GameLog.cs:29` | **nada** |
| `NombreDelIdioma` | `string` | `Loc.cs:99` | hook propio *(revisar si cubre este campo)* |
| `rutaCaptura` | `string` | `MetricasDeBuild.cs:35` | hook propio *(revisar si cubre este campo)* |
| `superCaptura` | `int` | `MetricasDeBuild.cs:37` | hook propio *(revisar si cubre este campo)* |
| `dirty` | `bool` | `NavService.cs:52` | hook propio *(revisar si cubre este campo)* |
| `IsReady` | `bool` | `NavService.cs:55` | hook propio *(revisar si cubre este campo)* |
| `area` | `Bounds` | `NavService.cs:66` | hook propio *(revisar si cubre este campo)* |
| `hayArea` | `bool` | `NavService.cs:67` | hook propio *(revisar si cubre este campo)* |
| `built` | `bool` | `SpatialGrid.cs:45` | **nada** |
| `CellCount` | `int` | `SpatialGrid.cs:48` | **nada** |
| `watch` | `Stopwatch` | `TestLog.cs:9` | **nada** |
| `Elapsed` | `float` | `TestLog.cs:12` | **nada** |
| `VehicleBrains` | `IReadOnlyList<VehicleBrain>` | `WorldSystemsRegistry.cs:27` | **nada** |
| `TurretWeapons` | `IReadOnlyList<TurretWeapon>` | `WorldSystemsRegistry.cs:28` | **nada** |
| `TurretAis` | `IReadOnlyList<TurretAI>` | `WorldSystemsRegistry.cs:29` | **nada** |
| `Vehicles` | `IReadOnlyList<Vehicle>` | `WorldSystemsRegistry.cs:30` | **nada** |
| `Obstacles` | `IReadOnlyList<SP.Presentation.ObstacleMarker>` | `WorldSystemsRegistry.cs:31` | **nada** |
| `populated` | `bool` | `WorldSystemsRegistry.cs:54` | **nada** |
| `avisoDeCuenta` | `bool` | `EstadoDePartida.cs:28` | `ReinicioDeEstaticos` |
| `outcomeSuelto` | `GameOutcomeController` | `EstadoDePartida.cs:30` | `ReinicioDeEstaticos` |
| `Activo` | `bool` | `MisionDirector.cs:32` | `ReinicioDeEstaticos` |
| `Segundos` | `float` | `Demolicion.cs:17` | `ReinicioDeEstaticos` |
| `Todos` | `IReadOnlyList<DemoledorAsalto>` | `Demolicion.cs:101` | `ReinicioDeEstaticos` |
| `AllActions` | `IEnumerable<string>` | `KeyBindings.cs:164` | **nada** |
| `Pad` | `Gamepad` | `MandoFps.cs:16` | **nada** |
| `Conectado` | `bool` | `MandoFps.cs:17` | **nada** |
| `Mover` | `Vector2` | `MandoFps.cs:27` | **nada** |
| `Mirar` | `Vector2` | `MandoFps.cs:28` | **nada** |
| `Disparar` | `bool` | `MandoFps.cs:29` | **nada** |
| `Saltar` | `bool` | `MandoFps.cs:30` | **nada** |
| `Agachar` | `bool` | `MandoFps.cs:31` | **nada** |
| `Correr` | `bool` | `MandoFps.cs:32` | **nada** |
| `Recargar` | `bool` | `MandoFps.cs:33` | **nada** |
| `ArmaSiguiente` | `bool` | `MandoFps.cs:34` | **nada** |
| `ArmaAnterior` | `bool` | `MandoFps.cs:35` | **nada** |
| `Pausa` | `bool` | `MandoFps.cs:36` | **nada** |
| `head` | `int` | `OrderHistory.cs:35` | **nada** |
| `count` | `int` | `OrderHistory.cs:36` | **nada** |
| `Count` | `int` | `OrderHistory.cs:38` | **nada** |
| `ManejadoAMano` | `Soldier` | `OrderService.cs:38` | **nada** |
| `Activo` | `bool` | `PedidoDeCuracion.cs:36` | `ReinicioDeEstaticos` |
| `medicoPasivo` | `Soldier` | `PedidoDeCuracion.cs:41` | **nada** |
| `ProgresoDeReanimar` | `float` | `PedidoDeCuracion.cs:42` | **nada** |
| `restante` | `float` | `PedidoDeCuracion.cs:81` | **nada** |
| `acumulado` | `float` | `PedidoDeCuracion.cs:83` | **nada** |
| `atendiendo` | `bool` | `PedidoDeCuracion.cs:86` | **nada** |
| `proximoNumero` | `float` | `PedidoDeCuracion.cs:87` | **nada** |
| `BotiquinActivo` | `bool` | `PedidoDeCuracion.cs:142` | **nada** |
| `proximoEscaneo` | `float` | `PedidoDeCuracion.cs:197` | **nada** |
| `AtencionAutomatica` | `bool` | `PedidoDeCuracion.cs:200` | **nada** |
| `ReanimarEnCalma` | `bool` | `PedidoDeCuracion.cs:205` | **nada** |
| `reanimacionAutomatica` | `bool` | `PedidoDeCuracion.cs:208` | **nada** |
| `ReanimacionEsAutomatica` | `bool` | `PedidoDeCuracion.cs:209` | **nada** |
| `Activo` | `bool` | `RescateAutomatico.cs:32` | `ReinicioDeEstaticos` |
| `restante` | `float` | `RescateAutomatico.cs:33` | **nada** |
| `canalizado` | `float` | `RescateAutomatico.cs:35` | **nada** |
| `reordenar` | `float` | `RescateAutomatico.cs:36` | **nada** |
| `Puntos` | `IReadOnlyList<Vector3>` | `TrazadoDeCamino.cs:36` | **nada** |
| `Cantidad` | `int` | `TrazadoDeCamino.cs:37` | **nada** |
| `HayTrazado` | `bool` | `TrazadoDeCamino.cs:38` | **nada** |
| `instancia` | `AccionesEnCursoView` | `AccionesEnCursoView.cs:35` | hook propio *(revisar si cubre este campo)* |
| `Instancia` | `AccionesEnCursoView` | `AccionesEnCursoView.cs:37` | hook propio *(revisar si cubre este campo)* |
| `NombreActual` | `string` | `AnuncioDeZonas.cs:43` | `ReinicioDeEstaticos` |
| `Now` | `float` | `AudioDirector.cs:372` | **nada** |
| `instance` | `AudioDucking` | `AudioDucking.cs:23` | **nada** |
| `active` | `Coroutine` | `AudioDucking.cs:25` | **nada** |
| `UserVolumeCeiling` | `float` | `AudioDucking.cs:27` | **nada** |
| `instance` | `CoverHologram` | `CoverHologram.cs:18` | **nada** |
| `Instance` | `CoverHologram` | `CoverHologram.cs:20` | **nada** |
| `Visible` | `bool` | `CoverHologram.cs:21` | **nada** |
| `PuntoActual` | `Vector3` | `CoverHologram.cs:22` | **nada** |
| `ModeloActual` | `Soldier` | `CoverHologram.cs:23` | **nada** |
| `tintBlock` | `MaterialPropertyBlock` | `CubeFxReactor.cs:218` | **nada** |
| `root` | `Transform` | `DebrisPool.cs:20` | **nada** |
| `ActiveCount` | `int` | `DebrisPool.cs:22` | **nada** |
| `TotalCount` | `int` | `DebrisPool.cs:23` | **nada** |
| `root` | `Transform` | `DecalPool.cs:20` | **nada** |
| `sharedMaterial` | `Material` | `EntityStateDebugView.cs:36` | hook propio *(revisar si cubre este campo)* |
| `propertyBlock` | `MaterialPropertyBlock` | `EntityStateDebugView.cs:38` | hook propio *(revisar si cubre este campo)* |
| `SharedMaterial` | `Material` | `EntityStateDebugView.cs:41` | hook propio *(revisar si cubre este campo)* |
| `active` | `EntityStateDebugView` | `EntityStateDebugView.cs:78` | hook propio *(revisar si cubre este campo)* |
| `next` | `int` | `Feedback.cs:81` | **nada** |
| `font` | `Font` | `Feedback.cs:82` | **nada** |
| `root` | `Transform` | `Fragmentador.cs:49` | hook propio *(revisar si cubre este campo)* |
| `mallas` | `Mesh[]` | `Fragmentador.cs:50` | hook propio *(revisar si cubre este campo)* |
| `Piezas` | `IReadOnlyList<Fragmento>` | `Fragmentador.cs:53` | hook propio *(revisar si cubre este campo)* |
| `instancia` | `FuentesBelicas` | `FuentesBelicas.cs:17` | hook propio *(revisar si cubre este campo)* |
| `Titulo` | `Font` | `FuentesBelicas.cs:21` | hook propio *(revisar si cubre este campo)* |
| `Texto` | `Font` | `FuentesBelicas.cs:22` | hook propio *(revisar si cubre este campo)* |
| `Budget` | `int` | `ImpactFx.cs:51` | **nada** |
| `ActiveCount` | `int` | `ImpactFx.cs:52` | **nada** |
| `sharedMaterial` | `Material` | `ImpactFx.cs:58` | **nada** |
| `propertyBlock` | `MaterialPropertyBlock` | `ImpactFx.cs:73` | **nada** |
| `shaderWarmed` | `bool` | `ImpactFx.cs:189` | **nada** |
| `enganchado` | `bool` | `LimpiezaDeEscena.cs:27` | hook propio *(revisar si cubre este campo)* |
| `actual` | `MenuAmbiente` | `MenuAmbiente.cs:16` | hook propio *(revisar si cubre este campo)* |
| `mallaTriangulo` | `Mesh` | `MinimapIcon.cs:143` | **nada** |
| `mallaCuadrado` | `Mesh` | `MinimapIcon.cs:201` | **nada** |
| `estrategiaSource` | `AudioSource` | `MusicDirector.cs:57` | hook propio *(revisar si cubre este campo)* |
| `luchaSource` | `AudioSource` | `MusicDirector.cs:59` | hook propio *(revisar si cubre este campo)* |
| `fuentesListas` | `bool` | `MusicDirector.cs:60` | hook propio *(revisar si cubre este campo)* |
| `root` | `Transform` | `MuzzleLightPool.cs:19` | **nada** |
| `TotalCount` | `int` | `MuzzleLightPool.cs:21` | **nada** |
| `Budget` | `int` | `OrderMarkerFx.cs:38` | **nada** |
| `root` | `Transform` | `OrderMarkerFx.cs:58` | **nada** |
| `sharedMaterial` | `Material` | `OrderMarkerFx.cs:226` | **nada** |
| `propertyBlock` | `MaterialPropertyBlock` | `OrderMarkerFx.cs:241` | **nada** |
| `shaderWarmed` | `bool` | `OrderMarkerFx.cs:398` | **nada** |
| `template` | `Material` | `SafeMaterial.cs:19` | **nada** |
| `sharedMaterial` | `Material` | `SelectionRingFx.cs:30` | **nada** |
| `propertyBlock` | `MaterialPropertyBlock` | `SelectionRingFx.cs:32` | **nada** |
| `root` | `Transform` | `SpriteFx.cs:34` | hook propio *(revisar si cubre este campo)* |
| `apuntado` | `Soldier` | `SquadStateIndicatorView.cs:49` | **nada** |
| `apuntadoHasta` | `float` | `SquadStateIndicatorView.cs:50` | **nada** |
| `RegisteredCount` | `int` | `WorldUiDirector.cs:65` | hook propio *(revisar si cubre este campo)* |
| `active` | `WorldUiDirector` | `WorldUiDirector.cs:101` | hook propio *(revisar si cubre este campo)* |
| `populated` | `bool` | `WorldUiDirector.cs:316` | hook propio *(revisar si cubre este campo)* |
| `ignorarHumano` | `bool` | `EntradaVirtual.cs:19` | **nada** |
| `Activas` | `IReadOnlyList<TutorialBeacon>` | `TutorialBeacon.cs:14` | `ReinicioDeEstaticos` |
| `fuente` | `Font` | `TutorialBeacon.cs:32` | **nada** |
| `Lineas` | `IReadOnlyList<string>` | `TutorialLog.cs:17` | **nada** |
| `t0` | `float` | `TutorialLog.cs:18` | **nada** |
| `RutaArchivo` | `string` | `TutorialLog.cs:19` | **nada** |
| `PasoGuardado` | `int` | `TutorialManager.cs:1794` | `ReinicioDeEstaticos` |
| `anilloCache` | `Sprite` | `AimUI.cs:99` | **nada** |
| `PantallaCompleta` | `bool` | `AjustesDeJuego.cs:68` | hook propio *(revisar si cubre este campo)* |
| `count` | `int` | `AlertQueue.cs:60` | hook propio *(revisar si cubre este campo)* |
| `currentPriority` | `AlertPriority` | `AlertQueue.cs:65` | hook propio *(revisar si cubre este campo)* |
| `currentUntil` | `float` | `AlertQueue.cs:66` | hook propio *(revisar si cubre este campo)* |
| `PendingCount` | `int` | `AlertQueue.cs:68` | hook propio *(revisar si cubre este campo)* |
| `IsBusy` | `bool` | `AlertQueue.cs:69` | hook propio *(revisar si cubre este campo)* |
| `CurrentPriority` | `AlertPriority` | `AlertQueue.cs:70` | hook propio *(revisar si cubre este campo)* |
| `instancia` | `AliadosSinEstorbo` | `AliadosSinEstorbo.cs:16` | **nada** |
| `Instancia` | `AliadosSinEstorbo` | `AliadosSinEstorbo.cs:22` | **nada** |
| `discoCache` | `Sprite` | `CirculoDeProgreso.cs:26` | **nada** |
| `cachedWedgeTexture` | `Texture2D` | `DamageDirectionView.cs:19` | **nada** |
| `cachedTexture` | `Texture2D` | `DamageVignetteView.cs:20` | **nada** |
| `instancia` | `HudPulido` | `HudPulido.cs:13` | hook propio *(revisar si cubre este campo)* |
| `cachedTexture` | `Texture2D` | `LowHealthPulseView.cs:50` | **nada** |
| `cachedSprite` | `Sprite` | `LowHealthPulseView.cs:52` | **nada** |
| `PistaCategoria` | `int` | `MenuDeOrdenes.cs:151` | `ReinicioDeEstaticos` |
| `sprites` | `Sprite[]` | `MirillaView.cs:27` | **nada** |
| `mascara` | `Sprite` | `MirillaView.cs:29` | **nada** |
| `mascaraPeriscopio` | `Sprite` | `MirillaView.cs:204` | **nada** |
| `cuatro` | `Vector2[]` | `MirillaView.cs:339` | **nada** |
| `cache` | `Sprite` | `SpriteBlanco.cs:26` | **nada** |
| `cache` | `Sprite` | `SpriteDeTriangulo.cs:10` | **nada** |
| `Todas` | `IReadOnlyList<TorretaFija>` | `TorretaFija.cs:30` | hook propio *(revisar si cubre este campo)* |
| `cachedLoopClip` | `AudioClip` | `VehicleAudioFeedback.cs:15` | **nada** |

## Las dos formas validas de restablecer

1. **`ReinicioDeEstaticos.Restablecer()`** (`Core/ReinicioDeEstaticos.cs`), que corre en
   `SubsystemRegistration`, antes de que cargue ninguna escena. Es el sitio para el estado de juego.
2. **Un `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` propio** en
   la clase duena del estado. Es el sitio para el estado de un subsistema que se basta solo.

Un estatico que no esta en ninguna de las dos es deuda. Ver `Docs/RONDA_10_BUGS.md`, bug 1.

## Los dos relojes, y por que importa cual se usa

| Reloj | Avanza en Edit mode | Respeta pausa | Cuando usarlo |
|---|---|---|---|
| `Time.time` | **No** (congelado) | Sí | Casi nunca en logica de juego |
| `Time.unscaledTime` | **No** | No | Interfaz, avisos |
| `Time.realtimeSinceStartup` | Sí (reloj de pared) | No | Perfilado |
| **Acumulador de `dt`** | **Sí** (simulado) | Sí | **Toda la logica de juego** |

La convencion del proyecto, documentada en `Combat/Health.cs`, es el acumulador de `dt`: es el unico
que avanza igual en el juego real y en la suite headless. Un `Time.time` en logica de juego significa
que ese codigo **no se puede probar**.
