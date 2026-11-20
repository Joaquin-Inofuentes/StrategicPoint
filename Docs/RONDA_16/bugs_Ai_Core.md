# Ronda 16 - bugs Ai / Vehicles / Mision / Core / Demo / Simulacion

Rutas relativas a `Assets/_Project/Scripts`. No se compilo ni se ejecuto Unity.

| n | archivo:linea | bug | correccion |
|---|---|---|---|
| 1 | Core/WorldSystemsRegistry.cs:84 | Sin recarga de dominio, las listas (con objetos ya destruidos) y `populated` sobrevivian entre corridas de Play: `EnsurePopulated` no volvia a poblar y el driver recorria vehiculos/torretas muertos. | `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` que llama a `Clear()`. |
| 2 | Core/SpatialGrid.cs:49 | `cells` y `built` sin reset: la grilla arrancaba con soldados de la corrida anterior y `EnsureBuilt` no reconstruia. | Reset de `cells` y `built` al registrar el subsistema. |
| 3 | Vehicles/Vehicle.cs:598 | `Mount` leia `soldier.Health.IsAlive` sin chequear `Health == null` (NRE). | Se agrega `soldier.Health == null`. |
| 4 | Vehicles/Vehicle.cs:444 | `PintarElRombo` leia `s.Team` de cada ocupante; un soldado destruido a bordo lanzaba MissingReference en `RefreshOccupancyColor` (cortaba Mount/Dismount). | `if (s == null) continue;`. |
| 5 | Vehicles/Vehicle.cs:835 | `Dismount` con un ocupante destruido hacia `SetActive` sobre un objeto destruido: excepcion en `OnDestroyed` del vehiculo (nunca terminaba de destruirse ni expulsar al resto). | Si `soldier == null` libera el asiento y retorna sin reposicionar. |
| 6 | Vehicles/Vehicle.cs:900-917 | Un soldado destruido estando a bordo dejaba su asiento ocupado para siempre (vehiculo "lleno", IA de torreta/tripulacion mal contada). | `PurgarOcupantesDestruidos()` en `LateUpdate` quita asientos con ocupante destruido. |
| 7 | Ai/AiBrain.cs:316 | `SincronizarAgente`: si `Warp` fallaba, `agent.nextPosition = pies` se escribia con el agent fuera del NavMesh. | Solo si `AgentActivo`. |
| 8 | Ai/AiBrain.Navegacion.cs:32 | `PlanPathTo`: tras `SincronizarAgente` (que puede dejar al agent fuera del NavMesh) se llamaba `SetDestination` sin re-chequear. | Re-chequeo de `AgentActivo` tras sincronizar. |
| 9 | Ai/AiBrain.Navegacion.cs:202 | `AdvanceTo`: mismo caso (`SetDestination`/`pathPending`/`remainingDistance` sobre un agent fuera del NavMesh). | Si ya no esta activo cae a `AvanzarSinNavMesh`. |
| 10 | Ai/AiBrain.Herido.cs:211 | `MoverMirando` (repliegue): mismo caso tras `SincronizarAgente`. | Si no esta activo marca `agentTieneDestino = false` y no llama `SetDestination`. |
| 11 | Ai/AiBrain.Tactica.cs:537 | El "perro guardian" anti-trabado usaba `agent.Warp(...)` sin devolver el pivote (Warp escribe el transform a los pies: soldado hundido `baseOffset`) ni invalidar el destino; ignoraba el resultado. | Solo si `Warp` devuelve true: `transform.position = pos + up*baseOffset` y `agentTieneDestino = false`. |
| 12 | Mision/Helicoptero.cs:72 | `AlDisparar` (evento estatico) no se limpiaba al entrar a Play: handlers de objetos ya destruidos. | `AlDisparar = null` en el reset. |
| 13 | Core/Loc.cs:85 | `Loc.Cambio` (evento estatico) conservaba suscriptores de UI destruida entre corridas. | Reset de `Cambio` en `SubsystemRegistration`. |
| 14 | Core/GameLog.cs:19-28 | El buffer del log crecia sin limite toda la sesion y cada volcado reescribia el archivo entero (memoria y E/S crecientes). | Tope de 400 000 caracteres; al pasarlo se descarta lo mas viejo. |
| 15 | Core/GameLog.cs:40 | Lambda anonima enganchada a `Application.quitting` en cada Play (acumulaba handlers). | Metodo con nombre, desuscribe antes de suscribir. |
| 16 | Core/Coberturas.cs:231 | `marcasVisibles`, `root` y cache de obstaculo sin reset: el primer `Registrar` de la corrida siguiente redibujaba ~1200 cilindros. | Se reinician en `ReiniciarAlEntrarAPlay`. |
| 17 | Core/NavService.cs:163 | `HayLineaDeTiro`: `RaycastNonAlloc` no ordena y el buffer era de 8; con mas colliders sobre el rayo la pared quedaba afuera ("hay linea de tiro" a traves de ella). | Buffer 32. |
| 18 | Core/NavService.cs:52 | `IsBlockedAt`: `OverlapBoxNonAlloc` con buffer 16; un nodo cargado de colliders dejaba afuera al bloqueante y quedaba transitable. | Buffer 64. |
| 19 | Vehicles/TurretAI.cs:263 | Mismo problema de buffer (8) en la linea de tiro de la torreta. | Buffer 32. |
| 20 | Mision/Helicoptero.cs:69 | Mismo problema de buffer (12) en `LineaDeTiroLibre` del helicoptero. | Buffer 32. |
| 21 | Core/Deslizador.cs:33 | `SphereCastNonAlloc`/`OverlapSphereNonAlloc` con buffers de 16: en multitudes la pared real podia no entrar y el cuerpo la atravesaba. | Buffers 48. |

Archivos de otras carpetas, `SubidaDeBug.cs` y `CapturasDeBuild*.cs` no se tocaron.
