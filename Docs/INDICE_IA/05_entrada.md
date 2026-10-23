# Mapa de entrada

## Acciones reasignables (`Player/KeyBindings.cs`)

> Cada constante es una accion con tecla configurable, guardada en `PlayerPrefs`.
> Una accion que NO esta en esta tabla pero si se lee con `Keyboard.current.xKey` **no se puede
> reasignar**: eso es un bug (ver `Docs/RONDA_10_BUGS.md`, bug 76).

| Constante | Clave de PlayerPrefs |
|---|---|
| `Disparar` | `disparar` |
| `Recargar` | `recargar` |
| `Interactuar` | `interactuar` |
| `SubirBajarVehiculo` | `vehiculo_entrar_salir` |
| `Poseer` | `poseer` |
| `CiclarPosesion` | `ciclar_posesion` |
| `CiclarPosesionAtras` | `ciclar_posesion_atras` |
| `PoseerMasCercano` | `poseer_cercano` |
| `AlternarVista` | `alternar_vista` |
| `Controles` | `controles` |
| `Frenar` | `frenar` |
| `VerTactico` | `ver_tactico` |
| `AtaqueCuchillo` | `ataque_cuchillo` |
| `Granada` | `granada` |
| `Recentrar` | `recentrar` |
| `CancelarOrden` | `cancelar_orden` |
| `Reagrupar` | `reagrupar` |
| `Retirada` | `retirada` |
| `CiclarFormacion` | `ciclar_formacion` |
| `SeleccionarHeridos` | `seleccionar_heridos` |
| `SeleccionarMismoTipo` | `seleccionar_mismo_tipo` |
| `MinimapAgrandar` | `minimap_agrandar` |
| `MinimapCiclarTamano` | `minimap_ciclar_tamano` |
| `FocalizarRts` | `focalizar_rts` |
| `RotarCamaraRts` | `rotar_camara_rts` |
| `RadialEspacio` | `radial_espacio` |
| `MarcarBug` | `marcar_bug` |
| `PlegarPasos` | `plegar_pasos` |
| `CambiarAliado` | `cambiar_aliado` |
| `VerRuta` | `ver_ruta` |

## Teclas leidas directamente, por archivo

> Si un archivo que no es `KeyBindings.cs` ni `ControlsTable.cs` aparece aca con teclas de juego,
> conviene mirar si deberia pasar por `KeyBindings`.

| Archivo | Teclas que nombra |
|---|---|
| `SesionLog.cs` | `StartsWith`, `Substring` |
| `ChecksBugs065.Wp5a.cs` | `W` |
| `ChecksBugs065.Wp6.cs` | `IsDestroyed` |
| `ChecksBugs065.Wp9a.cs` | `D`, `Digit2`, `E`, `F`, `LeftShift`, `Q`, `R`, `Tab`, `W` |
| `ChecksBugs104.Resistir.cs` | `C` |
| `ChecksBugs104.Rts.cs` | `D` |
| `HeadlessTestRunner.Fase18.cs` | `F` |
| `HeadlessTestRunner.Fase19.cs` | `Replace` |
| `HeadlessTestRunner.Fase21.cs` | `name`, `text`, `transform` |
| `HeadlessTestRunner.Fases13a17.cs` | `C`, `F`, `G`, `I`, `None`, `T` |
| `HeadlessTestRunner.cs` | `U` |
| `OperacionBuilder.Fusion.cs` | `capa`, `cx`, `cz`, `mat`, `sombra` |
| `OperacionBuilder.Retoque.cs` | `bounds` |
| `ValidacionBugs.cs` | `Space`, `U`, `W` |
| `ValidadorJugabilidad.cs` | `LeftShift` |
| `EstadisticasDeMision.cs` | `Replace` |
| `MenuDeTrampas.cs` | `Digit1`, `Digit2`, `Digit3`, `Digit4`, `Digit5`, `Digit6`, `Digit7` |
| `KeyBindings.cs` | `B`, `Backquote`, `C`, `E`, `F`, `G`, `H`, `I`, `J`, `K`, `L`, `M`, `N`, `None`, `O`, `Q`, `R`, `Space` |
| `PlayerInputDriver.cs` | `None` |
| `TutorialAutoPlayer.Entrada.cs` | `A`, `D`, `Digit2`, `F`, `G`, `LeftCtrl`, `LeftShift`, `R`, `S`, `Tab`, `W` |
| `TutorialAutoPlayer.Radial.cs` | `Digit2`, `Digit3`, `LeftCtrl`, `LeftShift` |
| `AliadosSinEstorbo.cs` | `shadowCastingMode` |

## Los tres modos de entrada

| Modo | Quien lo atiende | Que manda |
|---|---|---|
| **FPS a pie** | `PlayerInputDriver.UpdateFps` | WASD, mouse, `[Q]` radial, `[Tab]` cambia de modo |
| **RTS** | `PlayerInputDriver.UpdateRts` | Arrastre de seleccion, clic derecho = orden, rueda = zoom |
| **En vehiculo** | `PlayerInputDriver.UpdateInVehicle` | Asientos `1/2/3`, mira del canon, `[E]` bajar |

Los tres son **ramas mutuamente excluyentes** del mismo `Update()`: una tecla puede significar cosas
distintas en cada uno sin colisionar.
