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

## Teclas leidas directamente, por archivo

> Si un archivo que no es `KeyBindings.cs` ni `ControlsTable.cs` aparece aca con teclas de juego,
> conviene mirar si deberia pasar por `KeyBindings`.

| Archivo | Teclas que nombra |
|---|---|
| `HeadlessTestRunner.Fase18.cs` | `F` |
| `HeadlessTestRunner.Fase19.cs` | `Replace` |
| `HeadlessTestRunner.Fase21.cs` | `name`, `text`, `transform` |
| `HeadlessTestRunner.Fases13a17.cs` | `C`, `F`, `G`, `None` |
| `HeadlessTestRunner.cs` | `U` |
| `KeyBindings.cs` | `B`, `C`, `E`, `F`, `G`, `H`, `J`, `K`, `L`, `M`, `N`, `None`, `O`, `Q`, `R`, `Space`, `Tab`, `X` |
| `PlayerInputDriver.cs` | `None` |
| `TutorialAutoPlayer.Entrada.cs` | `A`, `C`, `D`, `Digit2`, `F`, `G`, `LeftCtrl`, `LeftShift`, `R`, `S`, `W` |
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
