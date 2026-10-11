# Mapa de estado estatico

> Este proyecto corre con **"Enter Play Mode sin recarga de dominio"**: los `static` sobreviven de una
> partida a la siguiente dentro de la misma sesion de Editor. Cualquier estatico mutable que no se
> restablezca es una fuga de estado entre corridas — y la causa de los falsos fallos mas dificiles de
> encontrar.

**407 estaticos mutables fuera de `Editor/`:
40 cubiertos por `ReinicioDeEstaticos`,
158 en archivos con hook propio (hay que mirar si el hook cubre ESE campo),
y 209 sin nada.**

| Campo | Tipo | Donde | Quien lo restablece |
|---|---|---|---|
| `nextId` | `int` | `Soldier.cs:18` | **nada** |
| `matCivil` | `Material` | `SoldierLook.cs:53` | **nada** |
| `DescensoDeEscalonActivo` | `bool` | `SoldierMotor.cs:224` | **nada** |
| `AlejarseDeCargasActivo` | `bool` | `AiBrain.Cargas.cs:19` | **nada** |
| `Humanizada` | `bool` | `AiBrain.Disparo.cs:31` | **nada** |
| `proximoAvisoRadio` | `float` | `AiBrain.Granadas.cs:25` | **nada** |
| `ultimoAnuncioDeHerido` | `float` | `AiBrain.Herido.cs:36` | hook propio *(revisar si cubre este campo)* |
| `LineaDeTiroPivoteAPivote` | `bool` | `AiBrain.Sentidos.cs:48` | **nada** |
| `SeparacionActiva` | `bool` | `AiBrain.Separacion.cs:19` | **nada** |
| `RadioDeCoberturaAMano` | `float` | `AiBrain.Tactica.cs:39` | **nada** |
| `DefaultVersionCobertura` | `int` | `AiBrain.Tactica.cs:45` | **nada** |
| `IAPausada` | `bool` | `AiBrain.Tactica.cs:118` | **nada** |
| `ultimoAvisoDeteccion` | `float` | `AiBrain.Tactica.cs:132` | **nada** |
| `ultimoAvisoDeSeguir` | `float` | `AiBrain.Tactica.cs:438` | **nada** |
| `RadioDeRegresoForzado` | `float` | `AiBrain.Tactica.cs:440` | **nada** |
| `RadioDeSeguirEnCombateCubierto` | `float` | `AiBrain.Tactica.cs:441` | **nada** |
| `DistanciaParaSeguir` | `float` | `AjustesDeEscuadra.cs:33` | `ReinicioDeEstaticos` |
| `DistanciaParaDetenerse` | `float` | `AjustesDeEscuadra.cs:34` | `ReinicioDeEstaticos` |
| `SeguirDesdeCobertura` | `bool` | `AjustesDeEscuadra.cs:35` | `ReinicioDeEstaticos` |
| `DistanciaLateralFormacion` | `float` | `AjustesDeEscuadra.cs:36` | `ReinicioDeEstaticos` |
| `DistanciaAtrasFormacion` | `float` | `AjustesDeEscuadra.cs:37` | `ReinicioDeEstaticos` |
| `Lider` | `Soldier` | `AjustesDeEscuadra.cs:41` | `ReinicioDeEstaticos` |
| `Correr` | `bool` | `AjustesDeEscuadra.cs:45` | `ReinicioDeEstaticos` |
| `LodPorDistancia` | `bool` | `WorldSimulationDriver.cs:54` | **nada** |
| `tickNumero` | `int` | `WorldSimulationDriver.cs:56` | **nada** |
| `cached` | `bool?` | `CameraFxSettings.cs:23` | **nada** |
| `ProveedorDeRtsForzado` | `System.Func<bool>` | `CameraRig.cs:417` | **nada** |
| `RtsForzado` | `bool` | `CameraRig.cs:418` | **nada** |
| `LimiteDePaneoRts` | `Rect?` | `CameraRig.cs:898` | **nada** |
| `RegeneracionPermitida` | `bool` | `Health.cs:33` | `ReinicioDeEstaticos` |
| `nextInstanceId` | `int` | `Projectile.cs:40` | `ReinicioDeEstaticos` |
| `HeadshotMataAlInstante` | `bool` | `Projectile.cs:928` | `ReinicioDeEstaticos` |
| `damageSub` | `System.IDisposable` | `TeamCombatState.cs:9` | `ReinicioDeEstaticos` |
| `CargadoresDeReserva` | `int` | `WeaponHolder.cs:213` | `ReinicioDeEstaticos` |
| `ReservasActivas` | `bool` | `WeaponHolder.cs:214` | `ReinicioDeEstaticos` |
| `metralletaVehiculo` | `GameObject` | `WeaponModels.cs:127` | **nada** |
| `metralletaVehiculoCargada` | `bool` | `WeaponModels.cs:128` | **nada** |
| `All` | `IReadOnlyList<Soldier>` | `ActorRegistry.cs:60` | **nada** |
| `proximoBarrido` | `float` | `ActorRegistry.cs:67` | **nada** |
| `CartelVisible` | `bool` | `AyudaDebugger.cs:17` | hook propio *(revisar si cubre este campo)* |
| `guardada` | `Camera` | `CamaraPrincipal.cs:9` | **nada** |
| `UmbralDeCubrirse` | `float` | `Coberturas.Puntuada.cs:54` | **nada** |
| `PesoTipoAlta` | `float` | `Coberturas.Puntuada.cs:55` | **nada** |
| `PesoVida` | `float` | `Coberturas.Puntuada.cs:56` | **nada** |
| `Puntos` | `IReadOnlyList<Vector3>` | `Coberturas.cs:34` | **nada** |
| `Duenos` | `IReadOnlyList<Collider>` | `Coberturas.cs:35` | **nada** |
| `Cantidad` | `int` | `Coberturas.cs:36` | **nada** |
| `root` | `Transform` | `Coberturas.cs:39` | **nada** |
| `marcasVisibles` | `bool` | `Coberturas.cs:339` | **nada** |
| `MarcasVisibles` | `bool` | `Coberturas.cs:340` | **nada** |
| `actual` | `NivelDificultad?` | `Dificultad.cs:32` | hook propio *(revisar si cubre este campo)* |
| `FuegoAmigoExplosivo` | `bool` | `Dificultad.cs:37` | hook propio *(revisar si cubre este campo)* |
| `PerfilActual` | `Perfil` | `Dificultad.cs:83` | hook propio *(revisar si cubre este campo)* |
| `ultimaEscritura` | `float` | `GameLog.cs:19` | hook propio *(revisar si cubre este campo)* |
| `pendiente` | `bool` | `GameLog.cs:20` | hook propio *(revisar si cubre este campo)* |
| `FilePath` | `string` | `GameLog.cs:38` | hook propio *(revisar si cubre este campo)* |
| `Activa` | `bool` | `IluminacionTactica.cs:24` | `ReinicioDeEstaticos` |
| `CantidadDeLuces` | `int` | `IluminacionTactica.cs:38` | hook propio *(revisar si cubre este campo)* |
| `subDano` | `IDisposable` | `InteligenciaDeEnemigos.cs:26` | hook propio *(revisar si cubre este campo)* |
| `NombreDelIdioma` | `string` | `Loc.cs:100` | `ReinicioDeEstaticos` |
| `rutaCaptura` | `string` | `MetricasDeBuild.cs:35` | hook propio *(revisar si cubre este campo)* |
| `superCaptura` | `int` | `MetricasDeBuild.cs:37` | hook propio *(revisar si cubre este campo)* |
| `reloj` | `float` | `NavMeshViva.cs:28` | hook propio *(revisar si cubre este campo)* |
| `ultimoRehacer` | `float` | `NavMeshViva.cs:30` | hook propio *(revisar si cubre este campo)* |
| `rehacerEn` | `float` | `NavMeshViva.cs:31` | hook propio *(revisar si cubre este campo)* |
| `inicio` | `float` | `NavMeshViva.cs:40` | `ReinicioDeEstaticos` |
| `Rehaciendo` | `bool` | `NavMeshViva.cs:41` | hook propio *(revisar si cubre este campo)* |
| `dirty` | `bool` | `NavService.cs:52` | hook propio *(revisar si cubre este campo)* |
| `IsReady` | `bool` | `NavService.cs:55` | hook propio *(revisar si cubre este campo)* |
| `area` | `Bounds` | `NavService.cs:66` | hook propio *(revisar si cubre este campo)* |
| `hayArea` | `bool` | `NavService.cs:67` | hook propio *(revisar si cubre este campo)* |
| `ModoPrueba` | `bool` | `PartidaGuardada.cs:42` | hook propio *(revisar si cubre este campo)* |
| `AutoguardadoActivo` | `bool` | `PartidaGuardada.cs:44` | hook propio *(revisar si cubre este campo)* |
| `CarpetaDePartidas` | `string` | `PartidaGuardada.cs:63` | hook propio *(revisar si cubre este campo)* |
| `HayPendiente` | `bool` | `PartidaGuardada.cs:225` | hook propio *(revisar si cubre este campo)* |
| `instancia` | `IndicadorDeGuardado` | `PartidaGuardada.cs:269` | hook propio *(revisar si cubre este campo)* |
| `TextoActual` | `string` | `PartidaGuardada.cs:270` | hook propio *(revisar si cubre este campo)* |
| `Visible` | `bool` | `PartidaGuardada.cs:271` | hook propio *(revisar si cubre este campo)* |
| `PruebaAbrir` | `bool` | `ReporteDeBug.cs:23` | **nada** |
| `PruebaMarcarBug` | `bool` | `SesionLog.cs:96` | hook propio *(revisar si cubre este campo)* |
| `DialogoAbierto` | `bool` | `SesionLog.cs:98` | hook propio *(revisar si cubre este campo)* |
| `proximaBusquedaDeGuardables` | `float` | `SesionLog.cs:420` | hook propio *(revisar si cubre este campo)* |
| `fpsSuave` | `float` | `SesionLog.cs:529` | hook propio *(revisar si cubre este campo)* |
| `TextoIndicadorDeBug` | `string` | `SesionLog.cs:635` | hook propio *(revisar si cubre este campo)* |
| `built` | `bool` | `SpatialGrid.cs:45` | **nada** |
| `CellCount` | `int` | `SpatialGrid.cs:48` | **nada** |
| `watch` | `Stopwatch` | `TestLog.cs:9` | **nada** |
| `Elapsed` | `float` | `TestLog.cs:12` | **nada** |
| `mapa` | `bool[,]` | `Vegetacion.cs:16` | hook propio *(revisar si cubre este campo)* |
| `origen` | `Vector2` | `Vegetacion.cs:19` | hook propio *(revisar si cubre este campo)* |
| `armado` | `bool` | `Vegetacion.cs:20` | hook propio *(revisar si cubre este campo)* |
| `intentado` | `bool` | `Vegetacion.cs:21` | hook propio *(revisar si cubre este campo)* |
| `Rige` | `bool` | `Vegetacion.cs:36` | hook propio *(revisar si cubre este campo)* |
| `VehicleBrains` | `IReadOnlyList<VehicleBrain>` | `WorldSystemsRegistry.cs:27` | **nada** |
| `TurretWeapons` | `IReadOnlyList<TurretWeapon>` | `WorldSystemsRegistry.cs:28` | **nada** |
| `TurretAis` | `IReadOnlyList<TurretAI>` | `WorldSystemsRegistry.cs:29` | **nada** |
| `Vehicles` | `IReadOnlyList<Vehicle>` | `WorldSystemsRegistry.cs:30` | **nada** |
| `Obstacles` | `IReadOnlyList<SP.Presentation.ObstacleMarker>` | `WorldSystemsRegistry.cs:31` | **nada** |
| `populated` | `bool` | `WorldSystemsRegistry.cs:54` | **nada** |
| `proximo` | `float` | `RolRequerido.cs:15` | **nada** |
| `SupervivientesForzados` | `int` | `EstadisticasDeMision.cs:197` | hook propio *(revisar si cubre este campo)* |
| `avisoDeCuenta` | `bool` | `EstadoDePartida.cs:37` | `ReinicioDeEstaticos` |
| `outcomeSuelto` | `GameOutcomeController` | `EstadoDePartida.cs:39` | `ReinicioDeEstaticos` |
| `Activo` | `bool` | `MisionDirector.cs:32` | `ReinicioDeEstaticos` |
| `MaterialDeLaton` | `Material` | `PivoteMetralleta.cs:91` | hook propio *(revisar si cubre este campo)* |
| `ColorDeEmision` | `Color` | `PivoteMetralleta.cs:92` | hook propio *(revisar si cubre este campo)* |
| `instancia` | `CasquillosDeMetralleta` | `PivoteMetralleta.cs:93` | hook propio *(revisar si cubre este campo)* |
| `PisoY` | `float` | `PivoteMetralleta.cs:94` | hook propio *(revisar si cubre este campo)* |
| `material` | `Material` | `PivoteMetralleta.cs:103` | hook propio *(revisar si cubre este campo)* |
| `malla` | `Mesh` | `AnilloDeAviso.cs:18` | hook propio *(revisar si cubre este campo)* |
| `raizDelPool` | `GameObject` | `AnilloDeAviso.cs:19` | hook propio *(revisar si cubre este campo)* |
| `Todos` | `IReadOnlyList<AnilloDeAviso>` | `AnilloDeAviso.cs:36` | hook propio *(revisar si cubre este campo)* |
| `instancia` | `AnilloDeSubida` | `AnilloDeSubida.cs:12` | hook propio *(revisar si cubre este campo)* |
| `Instancia` | `AnilloDeSubida` | `AnilloDeSubida.cs:13` | hook propio *(revisar si cubre este campo)* |
| `EstaActivo` | `bool` | `AnilloDeSubida.cs:14` | hook propio *(revisar si cubre este campo)* |
| `Armadas` | `IReadOnlyList<CargaExplosiva>` | `CargaExplosiva.cs:64` | `ReinicioDeEstaticos` |
| `reloj` | `float` | `CazaDeEnemigos.cs:29` | **nada** |
| `Cazadores` | `int` | `CazaDeEnemigos.cs:30` | **nada** |
| `fuente` | `Font` | `CineDeHuida.cs:36` | hook propio *(revisar si cubre este campo)* |
| `IntroActiva` | `bool` | `CinematicaDeOperacion.cs:27` | hook propio *(revisar si cubre este campo)* |
| `YaVista` | `bool` | `CinematicaDeOperacion.cs:28` | hook propio *(revisar si cubre este campo)* |
| `PruebaMantenerEspacio` | `bool` | `CinematicaDeOperacion.cs:32` | hook propio *(revisar si cubre este campo)* |
| `PruebaTecla` | `bool` | `CinematicaDeOperacion.cs:33` | hook propio *(revisar si cubre este campo)* |
| `fuente` | `Font` | `CinematicaDeOperacion.cs:79` | hook propio *(revisar si cubre este campo)* |
| `YaVista` | `bool` | `CinematicaDeRapel.cs:32` | hook propio *(revisar si cubre este campo)* |
| `PruebaMantenerEspacio` | `bool` | `CinematicaDeRapel.cs:36` | hook propio *(revisar si cubre este campo)* |
| `fuente` | `Font` | `CinematicaDeRapel.cs:234` | hook propio *(revisar si cubre este campo)* |
| `Todos` | `IReadOnlyList<TiradorDeTorre>` | `FrancotiradorEnTorre.cs:24` | hook propio *(revisar si cubre este campo)* |
| `instancia` | `GuiaDeMando` | `GuiaDeMando.cs:16` | hook propio *(revisar si cubre este campo)* |
| `Instancia` | `GuiaDeMando` | `GuiaDeMando.cs:17` | hook propio *(revisar si cubre este campo)* |
| `instancia` | `GuiaDeObjetivo` | `GuiaDeObjetivo.cs:19` | hook propio *(revisar si cubre este campo)* |
| `Instancia` | `GuiaDeObjetivo` | `GuiaDeObjetivo.cs:20` | hook propio *(revisar si cubre este campo)* |
| `texPunteado` | `Texture2D` | `GuiaDeObjetivo.cs:29` | hook propio *(revisar si cubre este campo)* |
| `spriteRombo` | `Sprite` | `GuiaDeObjetivo.cs:30` | hook propio *(revisar si cubre este campo)* |
| `MilicianosAActivar` | `int` | `MandoTactico.cs:28` | hook propio *(revisar si cubre este campo)* |
| `RadioObligaARts` | `bool` | `MandoTactico.cs:29` | hook propio *(revisar si cubre este campo)* |
| `SoltarConToque` | `bool` | `MandoTactico.cs:30` | hook propio *(revisar si cubre este campo)* |
| `ModoDirecto` | `bool` | `MandoTactico.cs:38` | hook propio *(revisar si cubre este campo)* |
| `EnRadio` | `bool` | `MandoTactico.cs:46` | hook propio *(revisar si cubre este campo)* |
| `OperadorEsElJugador` | `bool` | `MandoTactico.cs:49` | hook propio *(revisar si cubre este campo)* |
| `PosicionDeLaRadio` | `Vector3?` | `MandoTactico.cs:64` | hook propio *(revisar si cubre este campo)* |
| `PrimerIngresoHecho` | `bool` | `MandoTactico.cs:95` | hook propio *(revisar si cubre este campo)* |
| `materialHuella` | `Material` | `OperacionAuto.Efectos.cs:27` | **nada** |
| `CoberturaEstacionaria` | `bool` | `OperacionDirector.Extraer.Cobertura.cs:23` | **nada** |
| `CoberturaEnSaltoDePrueba` | `bool` | `OperacionDirector.Extraer.Cobertura.cs:24` | **nada** |
| `PruebaDisparar` | `bool` | `OperacionDirector.Final.cs:46` | **nada** |
| `ResistirSinMando` | `bool` | `OperacionDirector.Mando.cs:36` | **nada** |
| `PasosSecuenciales` | `bool` | `OperacionDirector.Mando.cs:945` | **nada** |
| `Activo` | `bool` | `OperacionDirector.cs:58` | `ReinicioDeEstaticos` |
| `MilicianosSuben` | `bool` | `OperacionDirector.cs:260` | **nada** |
| `PruebaMantenerE` | `bool` | `OperacionTerminal.cs:42` | **nada** |
| `PruebaToqueE` | `bool` | `OperacionTerminal.cs:43` | **nada** |
| `fuente` | `Font` | `OperacionTextoMundo.cs:12` | **nada** |
| `bloque` | `MaterialPropertyBlock` | `OperacionTextoMundo.cs:28` | **nada** |
| `Todas` | `IReadOnlyList<RampaCaminable>` | `RampaCaminable.cs:21` | hook propio *(revisar si cubre este campo)* |
| `Todos` | `IReadOnlyList<ReflectorVigia>` | `ReflectorVigia.cs:41` | hook propio *(revisar si cubre este campo)* |
| `Rotos` | `IReadOnlyList<ReflectorVigia>` | `ReflectorVigia.cs:43` | hook propio *(revisar si cubre este campo)* |
| `PoseidoIluminado` | `bool` | `ReflectorVigia.cs:45` | hook propio *(revisar si cubre este campo)* |
| `texAro` | `Texture2D` | `SectorDeDefensa.cs:43` | `ReinicioDeEstaticos` |
| `quad` | `Mesh` | `SectorDeDefensa.cs:45` | `ReinicioDeEstaticos` |
| `Todas` | `IReadOnlyList<TorreDestruible>` | `TorreDestruible.cs:35` | hook propio *(revisar si cubre este campo)* |
| `PruebaApretada` | `bool` | `CambioDeAliado.cs:26` | hook propio *(revisar si cubre este campo)* |
| `Segundos` | `float` | `Demolicion.cs:19` | `ReinicioDeEstaticos` |
| `proximoAvisoE` | `float` | `Demolicion.cs:72` | `ReinicioDeEstaticos` |
| `Todos` | `IReadOnlyList<DemoledorAsalto>` | `Demolicion.cs:163` | `ReinicioDeEstaticos` |
| `AvisosDeNoSePuede` | `int` | `Demolicion.cs:247` | `ReinicioDeEstaticos` |
| `AllActions` | `IEnumerable<string>` | `KeyBindings.cs:183` | **nada** |
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
| `ultimoAvisoOcupado` | `float` | `OrderService.cs:54` | **nada** |
| `emitiendoOrden` | `bool` | `PedidoDeCuracion.cs:35` | **nada** |
| `Activo` | `bool` | `PedidoDeCuracion.cs:87` | `ReinicioDeEstaticos` |
| `medicoPasivo` | `Soldier` | `PedidoDeCuracion.cs:92` | **nada** |
| `ProgresoDeReanimar` | `float` | `PedidoDeCuracion.cs:93` | **nada** |
| `restante` | `float` | `PedidoDeCuracion.cs:139` | **nada** |
| `acumulado` | `float` | `PedidoDeCuracion.cs:141` | **nada** |
| `atendiendo` | `bool` | `PedidoDeCuracion.cs:144` | **nada** |
| `Atendiendo` | `bool` | `PedidoDeCuracion.cs:145` | **nada** |
| `proximoNumero` | `float` | `PedidoDeCuracion.cs:146` | **nada** |
| `EnCola` | `int` | `PedidoDeCuracion.cs:164` | **nada** |
| `BotiquinActivo` | `bool` | `PedidoDeCuracion.cs:235` | **nada** |
| `rechazado` | `Soldier` | `PedidoDeCuracion.cs:274` | **nada** |
| `retenido` | `Soldier` | `PedidoDeCuracion.cs:286` | **nada** |
| `Retenido` | `Soldier` | `PedidoDeCuracion.cs:287` | **nada** |
| `medicoRetenido` | `Soldier` | `PedidoDeCuracion.cs:298` | **nada** |
| `proximoReSeguir` | `float` | `PedidoDeCuracion.cs:300` | **nada** |
| `proximoEscaneo` | `float` | `PedidoDeCuracion.cs:343` | **nada** |
| `AtencionAutomatica` | `bool` | `PedidoDeCuracion.cs:346` | **nada** |
| `ReanimarEnCalma` | `bool` | `PedidoDeCuracion.cs:351` | **nada** |
| `reanimacionAutomatica` | `bool` | `PedidoDeCuracion.cs:354` | **nada** |
| `ReanimacionEsAutomatica` | `bool` | `PedidoDeCuracion.cs:355` | **nada** |
| `SoloEscuadraParaRevivir` | `bool` | `PlayerInputDriver.cs:1637` | `ReinicioDeEstaticos` |
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
| `Desactivado` | `bool` | `AnuncioDeZonas.cs:52` | `ReinicioDeEstaticos` |
| `TopeActivo` | `bool` | `AttackLineManager.cs:27` | **nada** |
| `masterGainCache` | `float?` | `AudioDirector.cs:126` | **nada** |
| `Now` | `float` | `AudioDirector.cs:446` | **nada** |
| `instance` | `AudioDucking` | `AudioDucking.cs:23` | **nada** |
| `active` | `Coroutine` | `AudioDucking.cs:25` | **nada** |
| `UserVolumeCeiling` | `float` | `AudioDucking.cs:27` | **nada** |
| `instance` | `CoverHologram` | `CoverHologram.cs:18` | **nada** |
| `Instance` | `CoverHologram` | `CoverHologram.cs:20` | **nada** |
| `Visible` | `bool` | `CoverHologram.cs:21` | **nada** |
| `PuntoActual` | `Vector3` | `CoverHologram.cs:22` | **nada** |
| `ModeloActual` | `Soldier` | `CoverHologram.cs:23` | **nada** |
| `tintBlock` | `MaterialPropertyBlock` | `CubeFxReactor.cs:252` | **nada** |
| `instancia` | `AvisoCentral` | `CurandoAnimacion.cs:158` | `ReinicioDeEstaticos` |
| `TextoActual` | `string` | `CurandoAnimacion.cs:163` | `ReinicioDeEstaticos` |
| `root` | `Transform` | `DebrisPool.cs:20` | **nada** |
| `ActiveCount` | `int` | `DebrisPool.cs:22` | **nada** |
| `TotalCount` | `int` | `DebrisPool.cs:23` | **nada** |
| `root` | `Transform` | `DecalPool.cs:20` | **nada** |
| `ChapasActivas` | `int` | `DestruccionDeVehiculo.cs:42` | hook propio *(revisar si cubre este campo)* |
| `ChapasLanzadas` | `int` | `DestruccionDeVehiculo.cs:43` | hook propio *(revisar si cubre este campo)* |
| `raiz` | `Transform` | `DestruccionDeVehiculo.cs:78` | hook propio *(revisar si cubre este campo)* |
| `material` | `Material` | `DestruccionDeVehiculo.cs:79` | hook propio *(revisar si cubre este campo)* |
| `raiz` | `Transform` | `DestruccionDeVehiculo.cs:190` | hook propio *(revisar si cubre este campo)* |
| `Piezas` | `IReadOnlyList<CraterVida>` | `DestruccionDeVehiculo.cs:196` | hook propio *(revisar si cubre este campo)* |
| `caraCompartida` | `Mesh` | `DiamondGizmo.cs:29` | **nada** |
| `shaderUnlit` | `Shader` | `DiamondGizmo.cs:100` | **nada** |
| `texturaEngranaje` | `Texture2D` | `DiamondGizmo.cs:160` | **nada** |
| `texturaEngranajeAlfa` | `Texture2D` | `DiamondGizmo.cs:200` | **nada** |
| `instancia` | `EngranajesDeAccion` | `EngranajesDeAccion.cs:52` | hook propio *(revisar si cubre este campo)* |
| `Instancia` | `EngranajesDeAccion` | `EngranajesDeAccion.cs:54` | hook propio *(revisar si cubre este campo)* |
| `sharedMaterial` | `Material` | `EntityStateDebugView.cs:36` | hook propio *(revisar si cubre este campo)* |
| `propertyBlock` | `MaterialPropertyBlock` | `EntityStateDebugView.cs:38` | hook propio *(revisar si cubre este campo)* |
| `SharedMaterial` | `Material` | `EntityStateDebugView.cs:41` | hook propio *(revisar si cubre este campo)* |
| `active` | `EntityStateDebugView` | `EntityStateDebugView.cs:78` | hook propio *(revisar si cubre este campo)* |
| `Todas` | `System.Collections.Generic.IReadOnlyList<WorldTag>` | `Feedback.cs:82` | **nada** |
| `next` | `int` | `Feedback.cs:86` | **nada** |
| `font` | `Font` | `Feedback.cs:87` | **nada** |
| `ApilarActivo` | `bool` | `Feedback.cs:120` | **nada** |
| `tex` | `Texture2D` | `FlechaDeOleada.cs:13` | hook propio *(revisar si cubre este campo)* |
| `quad` | `Mesh` | `FlechaDeOleada.cs:15` | hook propio *(revisar si cubre este campo)* |
| `raizDelPool` | `GameObject` | `FlechaDeOleada.cs:16` | hook propio *(revisar si cubre este campo)* |
| `Todas` | `IReadOnlyList<FlechaDeOleada>` | `FlechaDeOleada.cs:29` | hook propio *(revisar si cubre este campo)* |
| `root` | `Transform` | `Fragmentador.cs:62` | hook propio *(revisar si cubre este campo)* |
| `mallas` | `Mesh[]` | `Fragmentador.cs:63` | hook propio *(revisar si cubre este campo)* |
| `Piezas` | `IReadOnlyList<Fragmento>` | `Fragmentador.cs:66` | hook propio *(revisar si cubre este campo)* |
| `instancia` | `FuentesBelicas` | `FuentesBelicas.cs:17` | hook propio *(revisar si cubre este campo)* |
| `Titulo` | `Font` | `FuentesBelicas.cs:21` | hook propio *(revisar si cubre este campo)* |
| `Texto` | `Font` | `FuentesBelicas.cs:22` | hook propio *(revisar si cubre este campo)* |
| `Budget` | `int` | `ImpactFx.cs:51` | **nada** |
| `ActiveCount` | `int` | `ImpactFx.cs:52` | **nada** |
| `ReviveRingsActive` | `int` | `ImpactFx.cs:61` | **nada** |
| `materialDeAnillo` | `Material` | `ImpactFx.cs:62` | **nada** |
| `sharedMaterial` | `Material` | `ImpactFx.cs:73` | **nada** |
| `propertyBlock` | `MaterialPropertyBlock` | `ImpactFx.cs:88` | **nada** |
| `shaderWarmed` | `bool` | `ImpactFx.cs:229` | **nada** |
| `instancia` | `InteractGearMarker` | `InteractGearMarker.cs:19` | hook propio *(revisar si cubre este campo)* |
| `AnguloActual` | `float` | `InteractGearMarker.cs:29` | hook propio *(revisar si cubre este campo)* |
| `instancia` | `InteractHintView` | `InteractHintView.cs:18` | hook propio *(revisar si cubre este campo)* |
| `Budget` | `int` | `KillCylinderFx.cs:23` | **nada** |
| `root` | `Transform` | `KillCylinderFx.cs:30` | **nada** |
| `sharedMaterial` | `Material` | `KillCylinderFx.cs:31` | **nada** |
| `propertyBlock` | `MaterialPropertyBlock` | `KillCylinderFx.cs:78` | **nada** |
| `enganchado` | `bool` | `LimpiezaDeEscena.cs:27` | hook propio *(revisar si cubre este campo)* |
| `cuadrado` | `Sprite` | `LoadingScreenController.cs:94` | **nada** |
| `circulo` | `Sprite` | `LoadingScreenController.cs:147` | **nada** |
| `actual` | `MenuAmbiente` | `MenuAmbiente.cs:16` | hook propio *(revisar si cubre este campo)* |
| `mallaConoQuad` | `Mesh` | `MinimapIcon.cs:267` | **nada** |
| `texturaCono` | `Texture2D` | `MinimapIcon.cs:293` | **nada** |
| `shaderTransparente` | `Shader` | `MinimapIcon.cs:350` | **nada** |
| `FraccionDelRadio` | `float` | `MinimapIcon.cs:381` | **nada** |
| `mallaTriangulo` | `Mesh` | `MinimapIcon.cs:557` | **nada** |
| `mallaTrianguloChico` | `Mesh` | `MinimapIcon.cs:616` | **nada** |
| `mallaCirculo` | `Mesh` | `MinimapIcon.cs:681` | **nada** |
| `mallaRombo` | `Mesh` | `MinimapIcon.cs:724` | **nada** |
| `mallaCuadrado` | `Mesh` | `MinimapIcon.cs:763` | **nada** |
| `estrategiaSource` | `AudioSource` | `MusicDirector.cs:57` | hook propio *(revisar si cubre este campo)* |
| `luchaSource` | `AudioSource` | `MusicDirector.cs:59` | hook propio *(revisar si cubre este campo)* |
| `fuentesListas` | `bool` | `MusicDirector.cs:60` | hook propio *(revisar si cubre este campo)* |
| `combateUltimo` | `bool` | `MusicDirector.cs:61` | hook propio *(revisar si cubre este campo)* |
| `VictoriaLista` | `bool` | `MusicDirector.cs:70` | hook propio *(revisar si cubre este campo)* |
| `VictoriaGenerando` | `bool` | `MusicDirector.cs:71` | hook propio *(revisar si cubre este campo)* |
| `VolumenVictoria` | `float` | `MusicDirector.cs:73` | hook propio *(revisar si cubre este campo)* |
| `VictoriaReproduciendo` | `bool` | `MusicDirector.cs:74` | hook propio *(revisar si cubre este campo)* |
| `VolumenDeLoops` | `float` | `MusicDirector.cs:76` | hook propio *(revisar si cubre este campo)* |
| `himnoClip` | `AudioClip` | `MusicDirector.cs:80` | hook propio *(revisar si cubre este campo)* |
| `himnoTarea` | `System.Threading.Tasks.Task<float[]>` | `MusicDirector.cs:82` | hook propio *(revisar si cubre este campo)* |
| `victoriaSource` | `AudioSource` | `MusicDirector.cs:83` | hook propio *(revisar si cubre este campo)* |
| `runnerVictoria` | `MusicaVictoriaRunner` | `MusicDirector.cs:84` | hook propio *(revisar si cubre este campo)* |
| `CombateForzado` | `bool` | `MusicDirector.cs:201` | hook propio *(revisar si cubre este campo)* |
| `MusicaDeAccionActiva` | `bool` | `MusicDirector.cs:202` | hook propio *(revisar si cubre este campo)* |
| `root` | `Transform` | `MuzzleLightPool.cs:19` | **nada** |
| `TotalCount` | `int` | `MuzzleLightPool.cs:21` | **nada** |
| `material` | `Material` | `OndasDeRotor.cs:17` | hook propio *(revisar si cubre este campo)* |
| `texturaAnillo` | `Texture2D` | `OndasDeRotor.cs:18` | hook propio *(revisar si cubre este campo)* |
| `mallaPlana` | `Mesh` | `OndasDeRotor.cs:19` | hook propio *(revisar si cubre este campo)* |
| `instancia` | `OrderLineManager` | `OrderLineManager.cs:36` | **nada** |
| `Instancia` | `OrderLineManager` | `OrderLineManager.cs:37` | **nada** |
| `Budget` | `int` | `OrderMarkerFx.cs:41` | **nada** |
| `root` | `Transform` | `OrderMarkerFx.cs:61` | **nada** |
| `sharedMaterial` | `Material` | `OrderMarkerFx.cs:269` | **nada** |
| `propertyBlock` | `MaterialPropertyBlock` | `OrderMarkerFx.cs:284` | **nada** |
| `shaderWarmed` | `bool` | `OrderMarkerFx.cs:441` | **nada** |
| `bloqueAnillo` | `MaterialPropertyBlock` | `OrderMarkerFx.cs:547` | **nada** |
| `texturaDegrade` | `Texture2D` | `PantallaDeResultado.cs:154` | **nada** |
| `shaderCache` | `Shader` | `ParticleMaterialFactory.cs:23` | **nada** |
| `template` | `Material` | `SafeMaterial.cs:19` | **nada** |
| `sharedMaterial` | `Material` | `SelectionRingFx.cs:30` | **nada** |
| `propertyBlock` | `MaterialPropertyBlock` | `SelectionRingFx.cs:32` | **nada** |
| `matCirculo` | `Material` | `ShapeMarkerFx.cs:8` | **nada** |
| `matDestino` | `Material` | `ShapeMarkerFx.cs:43` | **nada** |
| `texturaDestino` | `Texture2D` | `ShapeMarkerFx.cs:44` | **nada** |
| `Nombres` | `IReadOnlyList<string>` | `SiluetasDeMinimapa.cs:28` | **nada** |
| `Cantidad` | `int` | `SiluetasDeMinimapa.cs:29` | **nada** |
| `AmbienteActivo` | `bool` | `SonidosDeOperacion.cs:21` | hook propio *(revisar si cubre este campo)* |
| `root` | `Transform` | `SparkleBurstFx.cs:20` | **nada** |
| `root` | `Transform` | `SpriteFx.cs:45` | hook propio *(revisar si cubre este campo)* |
| `apuntado` | `Soldier` | `SquadStateIndicatorView.cs:49` | **nada** |
| `apuntadoHasta` | `float` | `SquadStateIndicatorView.cs:50` | **nada** |
| `materialBordeCompartido` | `Material` | `UnitLocatorCylinder.cs:86` | `ReinicioDeEstaticos` |
| `materialSombraCompartido` | `Material` | `UnitLocatorCylinder.cs:98` | `ReinicioDeEstaticos` |
| `RegisteredCount` | `int` | `WorldUiDirector.cs:65` | hook propio *(revisar si cubre este campo)* |
| `active` | `WorldUiDirector` | `WorldUiDirector.cs:101` | hook propio *(revisar si cubre este campo)* |
| `RevelarEnemigos` | `bool` | `WorldUiDirector.cs:309` | hook propio *(revisar si cubre este campo)* |
| `populated` | `bool` | `WorldUiDirector.cs:324` | hook propio *(revisar si cubre este campo)* |
| `ignorarHumano` | `bool` | `EntradaVirtual.cs:19` | **nada** |
| `Activas` | `IReadOnlyList<TutorialBeacon>` | `TutorialBeacon.cs:16` | `ReinicioDeEstaticos` |
| `fuente` | `Font` | `TutorialBeacon.cs:34` | **nada** |
| `Lineas` | `IReadOnlyList<string>` | `TutorialLog.cs:17` | **nada** |
| `t0` | `float` | `TutorialLog.cs:18` | **nada** |
| `RutaArchivo` | `string` | `TutorialLog.cs:19` | **nada** |
| `PasoGuardado` | `int` | `TutorialManager.cs:1794` | `ReinicioDeEstaticos` |
| `anilloCache` | `Sprite` | `AimUI.cs:99` | **nada** |
| `PantallaCompleta` | `bool` | `AjustesDeJuego.cs:82` | hook propio *(revisar si cubre este campo)* |
| `count` | `int` | `AlertQueue.cs:60` | hook propio *(revisar si cubre este campo)* |
| `currentPriority` | `AlertPriority` | `AlertQueue.cs:65` | hook propio *(revisar si cubre este campo)* |
| `currentUntil` | `float` | `AlertQueue.cs:66` | hook propio *(revisar si cubre este campo)* |
| `PendingCount` | `int` | `AlertQueue.cs:69` | hook propio *(revisar si cubre este campo)* |
| `IsBusy` | `bool` | `AlertQueue.cs:70` | hook propio *(revisar si cubre este campo)* |
| `CurrentPriority` | `AlertPriority` | `AlertQueue.cs:71` | hook propio *(revisar si cubre este campo)* |
| `instancia` | `AliadosSinEstorbo` | `AliadosSinEstorbo.cs:16` | **nada** |
| `Instancia` | `AliadosSinEstorbo` | `AliadosSinEstorbo.cs:22` | **nada** |
| `instancia` | `AvisoUnico` | `AvisoUnico.cs:23` | hook propio *(revisar si cubre este campo)* |
| `ordenFijo` | `int` | `AvisoUnico.cs:28` | hook propio *(revisar si cubre este campo)* |
| `TextoActual` | `string` | `AvisoUnico.cs:36` | hook propio *(revisar si cubre este campo)* |
| `Visible` | `bool` | `AvisoUnico.cs:37` | hook propio *(revisar si cubre este campo)* |
| `Historial` | `IReadOnlyList<string>` | `AvisoUnico.cs:38` | hook propio *(revisar si cubre este campo)* |
| `HayTemporal` | `bool` | `AvisoUnico.cs:39` | hook propio *(revisar si cubre este campo)* |
| `instancia` | `CartelDeRacha` | `CartelDeRacha.cs:14` | hook propio *(revisar si cubre este campo)* |
| `Instancia` | `CartelDeRacha` | `CartelDeRacha.cs:15` | hook propio *(revisar si cubre este campo)* |
| `Visible` | `bool` | `CartelDeRacha.cs:97` | hook propio *(revisar si cubre este campo)* |
| `Texto` | `string` | `CartelDeRacha.cs:98` | hook propio *(revisar si cubre este campo)* |
| `FuriaVisible` | `bool` | `CartelDeRacha.cs:99` | hook propio *(revisar si cubre este campo)* |
| `TextoFuria` | `string` | `CartelDeRacha.cs:100` | hook propio *(revisar si cubre este campo)* |
| `EscalaActual` | `float` | `CartelDeRacha.cs:101` | hook propio *(revisar si cubre este campo)* |
| `InstanteForzado` | `float?` | `CartelDeRacha.cs:104` | hook propio *(revisar si cubre este campo)* |
| `discoCache` | `Sprite` | `CirculoDeProgreso.cs:26` | **nada** |
| `cachedWedgeTexture` | `Texture2D` | `DamageDirectionView.cs:19` | **nada** |
| `cachedTexture` | `Texture2D` | `DamageVignetteView.cs:21` | **nada** |
| `instancia` | `HeadshotPopup` | `HeadshotPopup.cs:12` | **nada** |
| `Visible` | `bool` | `HeadshotPopup.cs:61` | **nada** |
| `Texto` | `string` | `HeadshotPopup.cs:62` | **nada** |
| `instancia` | `HudPulido` | `HudPulido.cs:13` | hook propio *(revisar si cubre este campo)* |
| `cachedTexture` | `Texture2D` | `LowHealthPulseView.cs:50` | **nada** |
| `cachedSprite` | `Sprite` | `LowHealthPulseView.cs:52` | **nada** |
| `PistaCategoria` | `int` | `MenuDeOrdenes.cs:170` | `ReinicioDeEstaticos` |
| `discoCentro` | `Sprite` | `MenuDeOrdenes.cs:646` | `ReinicioDeEstaticos` |
| `sharedSprite` | `Sprite` | `MilitaryButtonSkin.cs:24` | **nada** |
| `raiz` | `GameObject` | `MiraDeTorreta.cs:22` | **nada** |
| `textoBlanco` | `Text` | `MiraDeTorreta.cs:25` | **nada** |
| `spriteCruceta` | `Sprite` | `MiraDeTorreta.cs:26` | **nada** |
| `Visible` | `bool` | `MiraDeTorreta.cs:37` | **nada** |
| `TextoDelBlanco` | `string` | `MiraDeTorreta.cs:38` | **nada** |
| `ColorDeCruceta` | `Color` | `MiraDeTorreta.cs:41` | **nada** |
| `sprites` | `Sprite[]` | `MirillaView.cs:27` | **nada** |
| `mascara` | `Sprite` | `MirillaView.cs:29` | **nada** |
| `mascaraPeriscopio` | `Sprite` | `MirillaView.cs:204` | **nada** |
| `cuatro` | `Vector2[]` | `MirillaView.cs:339` | **nada** |
| `subirPedido` | `bool` | `ModeToastView.cs:20` | **nada** |
| `raiz` | `GameObject` | `RespawnPromptView.cs:17` | hook propio *(revisar si cubre este campo)* |
| `caja` | `RectTransform` | `RespawnPromptView.cs:18` | hook propio *(revisar si cubre este campo)* |
| `pulso` | `float` | `RespawnPromptView.cs:19` | hook propio *(revisar si cubre este campo)* |
| `MostrarCartel` | `bool` | `SelectionCountView.cs:25` | **nada** |
| `cache` | `Sprite` | `SpriteBlanco.cs:26` | **nada** |
| `cache` | `Sprite` | `SpriteDeTriangulo.cs:10` | **nada** |
| `Fuente` | `Font` | `TablaDeControles.cs:24` | **nada** |
| `ultimaTorreta` | `TurretWeapon` | `TurretAimView.cs:172` | **nada** |
| `ultimoFrame` | `int` | `TurretAimView.cs:173` | **nada** |
| `ultimoImpacto` | `Impacto` | `TurretAimView.cs:174` | **nada** |
| `Todas` | `IReadOnlyList<TorretaFija>` | `TorretaFija.cs:31` | hook propio *(revisar si cubre este campo)* |
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
