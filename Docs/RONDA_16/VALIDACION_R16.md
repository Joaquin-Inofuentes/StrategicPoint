# Ronda 16 - 101 bugs corregidos

Suite OK, compilacion limpia, build 20/11 y 36 capturas con 0 excepciones (ATLAS_R16_regresion.jpg). Detalle por area:

# Ronda 16 - Bugs corregidos en Assets/_Project/Scripts/Presentation

Nota comun: `SafeMaterial.Create/CreateLinea` devuelven materiales `HideAndDontSave`; destruir el GameObject NO los libera. Se agrego el helper `SafeMaterial.LiberarDe(Component)` (SafeMaterial.cs) para los duenos de materiales propios. Sin compilar (lo hace el dueno).

| n | archivo:linea | bug | correccion |
|---|---|---|---|
| 1 | MinimapAttackerRings.cs:34 | `new Material(Shader.Find("Sprites/Default"))` lanza ArgumentNullException si el shader no esta; ademas el material nunca se destruia | Se valida el shader (fallback `SafeMaterial.Create`), se guarda en `lineMaterial` y se destruye en `OnDestroy` |
| 2 | AyudaDeCaido.cs:55-56,37 | `new Material(sh ?? Shader.Find(..))` con shader nulo lanza; `Construir()` se repite si se destruye el brazo y dejaba el material anterior huerfano; sin `OnDestroy` | Fallback a `SafeMaterial.Create`, se destruye el material previo antes de recrear, `OnDestroy` lo libera |
| 3 | IndicadorDeCaidos.cs:33-34 | `MarcasVisibles`/`TieneMarca` leen `m.raiz.gameObject` aunque `raiz` este destruida (MissingReferenceException tras recarga de escena) | Chequeo `m.raiz != null` |
| 4 | IndicadorDeCaidos.cs:202 | `avisados` (HashSet) acumulaba soldados destruidos para siempre (fuga + referencias muertas) | `avisados.RemoveWhere(a => a == null)` al final de LateUpdate |
| 5 | ObstacleMarker.cs:112 | `rend.sharedMaterial.color` con material perdido -> NullReferenceException en `CacheIfNeeded` (rompe OnEnable/TakeDamage) | Chequeo `rend.sharedMaterial != null` |
| 6 | CuchilloFx.cs:99,113,122 | Cada tajo creaba 2 materiales `HideAndDontSave` que nunca se liberaban | Se guardan en `matCuchillo/matEstela` y `OnDestroy` los destruye |
| 7 | SoldierMeleeAnim.cs:92,106,189 | Igual: 2 materiales por soldado huerfanos al destruirse | Campos + `OnDestroy` |
| 8 | TrayectoriaGranadaView.cs (OnDestroy nuevo, antes de `Ocultar`) | ~7 materiales propios fugados en cada `ClearAll`/cambio de escena | `OnDestroy` -> `Destroy` de los `sharedMaterial` de sus renderers |
| 9 | PossessedMarkerView.cs:43 | Material del rombo (`SafeMaterial.Create`) nunca se liberaba | `OnDestroy` -> `SafeMaterial.LiberarDe(this)` |
| 10 | ObjectiveArrowIndicator.cs:70-77 | Malla `FlechaObjetivoMesh` y material de la flecha fugados al destruir | `OnDestroy` destruye malla y material |
| 11 | VehicleMountIndicator.cs:150-160 | Materiales de eje/cabeza/lineas y malla del cono fugados | `OnDestroy` destruye malla y materiales |
| 12 | PatrolRouteLine.cs:105,112 | `ReaplicarMaterial` se llama desde `Spawn` Y `Awake` ([ExecuteAlways]) y creaba un material nuevo cada vez, dejando el anterior huerfano | Un solo material cacheado (`matRuta`), destruido en `OnDestroy` |
| 13 | Luminaria.cs:63 | `lente.material` con renderer sin material devuelve null -> NullReferenceException en `BreakLamp` (la lampara no se rompia) | Se usa solo si `sharedMaterial != null` y se guarda `m != null` |
| 14 | Luminaria.cs:22,110 | Si el faro se desactiva a mitad del parpadeo, la corrutina muere y la luz hija "ParpadeoDeFaro" quedaba encendida para siempre | Se guarda la luz (`luzParpadeo`) y `OnDisable` la destruye |
| 15 | AudioDucking.cs:29 | Estaticos `instance`/`active` sin reset (sin domain reload quedaban apuntando a host/corrutina de la sesion anterior) | `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` que los anula |
| 16 | ShapeMarkerFx.cs:101-108 | `MatCirculo` devuelve un `Instantiate` (clon) por marcador (TutorialBeacon) que nadie destruia | Componente interno `LiberarMaterialClonado` que lo destruye en `OnDestroy` |
| 17 | FloatingDamageTextManager.cs:50 | Al desactivarse el manager mueren las corrutinas `RiseAndFade`: los numeros de dano quedaban visibles para siempre y `activeByTarget`/`activeOrder` con entradas muertas | `OnDisable` apaga los canvases del pool y limpia los diccionarios |
| 18 | SquadStateIndicatorView.cs:142 | `markerRenderer.sharedMaterial.color = ...` sin chequeo: NRE al cambiar de estado si el material se perdio (mismo bug ya corregido en EnemyAlertIndicatorView) | Se resuelve el material, con respaldo a `ownedMaterial`, antes de pintar |
| 19 | PantallaDeResultado.cs:186,217 | `Sprite.Create` en cada `CrearDegrade` (cada pantalla de victoria/derrota) sin destruirse | Sprite estatico unico `spriteDegrade` (HideAndDontSave) |


# Ronda 16 - bugs corregidos en Assets/_Project/Scripts/Operacion

Cambios minimos, sin compilar (pendiente de verificar en Unity). Sin commit. Los materiales de SafeMaterial son HideAndDontSave: Unity nunca los recolecta, hay que Destroy a mano.

| n | archivo:linea (aprox.) | bug | correccion |
|---|---|---|---|
| 1 | ArbustoRodante.cs:127 | `BuscarMallaYMaterial` creaba una malla nueva en cada `Armar` y fugaba la anterior | `if (malla == null) malla = CrearMallaBola();` |
| 2 | GuiaDeObjetivo.cs:134 | `MallaDeFlecha` creaba un Mesh nuevo en cada llamada (una por cada `GuiaDeMando.Awake`), nunca liberado | cache estatico `mallaFlecha` (HideAndDontSave) y reset en `Reiniciar` |
| 3 | GuiaDeMando.cs:84 | los dos materiales `CreateLinea` (flecha y ruta, HideAndDontSave) no se destruian | `OnDestroy` destruye ambos materiales |
| 4 | EmboscadaDeCohetes.cs:51 (CoheteDeAviso) | cada cohete creaba un material `SafeMaterial.Create` que nunca se liberaba (uno por disparo) | `OnDestroy` destruye el material del renderer |
| 5 | TorreDestruible.cs:217 | `SetParent(padreOcupante)` con un padre que puede haberse destruido junto con la torre (excepcion de objeto destruido) | `padreOcupante != null ? padreOcupante : null` |
| 6 | CazaDeEnemigos.cs:34 | estado estatico (cazadores, reloj, RumbosDados) sin reset de arranque | `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` que llama a `Reiniciar()` |
| 7 | ReparacionDeTanque.cs:221 y 228 | `FijarProgreso` y `Terminar` (via `CompletarYa`) usan `Tanque.PonerVida01` sin comprobar que el tanque siga vivo (NRE/MissingReference) | guarda `if (Tanque != null)` |
| 8 | OperacionTerminal.cs:212 y 249 | `yo.Health.IsAlive` sin comprobar `Health` null | `yo.Health != null &&` |
| 9 | HelicopteroEnemigo.cs:246 | `CrearSogas` hacia `lr.material = SafeMaterial.Create(...)` por soga (material fugado y clonado de nuevo por `.material`), nunca destruido | un material compartido `matSoga` en `sharedMaterial`, destruido en `QuitarSogas` |
| 10 | HelicopteroEnemigo.cs:293 | `BlancoDeMetralla`: `s.Health.IsAlive` sin null check | `s.Health == null ||` |
| 11 | EnemigoFrancotirador.cs:42 | `Laser.material = new Material(...)` crea dos copias y ninguna se destruye | `sharedMaterial` + `OnDestroy` que lo destruye |
| 12 | FrancotiradorEnTorre.cs:176 y 189 | `new Material` del punto de mira y del laser, nunca liberados | campos `matPunto`/`matLaser` destruidos en `OnDestroy` (override del base) |
| 13 | EnemigoEscudero.cs:93-118 | 4 materiales `new Material` por escudero (`Mat()`), nunca liberados | componente `LiberarMaterialesAlDestruirse` en la raiz del escudo (cubre escudo caido que se destruye a los 40 s) |
| 14 | OperacionHud.cs:377 | `Iluminado` crea Texture2D + Sprite de la vineta sin liberarlos al destruir el HUD | campo `vinetaTex` destruido en `OnDestroy` |
| 15 | ReflectorVigia.cs:111 y 118 | malla (HideAndDontSave) y material del haz creados por reflector y nunca destruidos; ademas `rotos` retenia referencias destruidas | `mallaHaz`/`matHaz` destruidos en `OnDestroy`; `rotos.Remove(this)` |
| 16 | HeliDeDescarga.cs:71 | un `new Material` por soga, nunca destruido | un material por heli (`matSoga`) destruido en `OnDestroy` |
| 17 | CineDeHuida.cs:67 | si se destruye sin `Cerrar()` y sin bloqueo, `Lento()` dejaba `timeScale`/`fixedDeltaTime` pegados en camara lenta | `OnDestroy` llama siempre a `TiempoNormal()` |
| 18 | DefensaDelCentro.cs:123 (TorretaDeDefensa) | 3 SafeMaterial por torreta (HideAndDontSave) nunca liberados | campo `materiales` destruido en `OnDestroy` |
| 19 | DefensaDelCentro.cs:198 | `s.Health.IsAlive` sin null check al elegir blanco | `s.Health == null ||` |
| 20 | CinematicaDeOperacion.cs:185 | textura (HideAndDontSave) y sprite del anillo de salto sin liberar en cada cinematica | `OnDestroy` destruye sprite y su textura |
| 21 | CinematicaDeRapel.cs:366 | `cuerda.material = matCuerda` clona una instancia por cuerda que `LimpiarEscena` no libera | `sharedMaterial` (el original ya se destruye en LimpiarEscena) |
| 22 | CinematicaDeRapel.cs:227 | idem 20: textura y sprite del anillo sin liberar | `OnDestroy` destruye sprite y textura |
| 23 | OperacionDirector.TerminalDeCeldas.cs:~180 | `MaterialDePantallaDeTerminal()` se llamaba dos veces (pantalla y led): un material extra fugado por terminal | se crea una vez y se comparte |

Revisados y descartados (no son bug o son de diseno): EmboscadaDeCohetes.Update (codigo muerto por `CohetesActivos = false`), CargaExplosiva/OperacionTerminal/OperacionHud/UiDeInteraccion (cambios de hoy preservados; solo los null-checks del punto 8).


# Ronda 16 - Bugs en UI y Tutorial (19 corregidos)

Alcance: Assets/_Project/Scripts/UI y Assets/_Project/Scripts/Tutorial. Sin compilar ni ejecutar Unity. Sin commit.

| n | archivo:linea | bug | correccion |
|---|---------------|-----|------------|
| 1 | UI/DamageDirectionView.cs:116 | OnDamage sigue suscripto con la vista desactivada (la suscripcion solo se libera en OnDestroy) y llama a StartCoroutine sobre un objeto inactivo: error. | `if (!isActiveAndEnabled) return;` |
| 2 | UI/DamageDirectionView.cs:107 | Al desactivarse, la corrutina muere sin ocultar la flecha y `routine` queda apuntando a una corrutina muerta. | OnDisable: `routine = null` y oculta la flecha. |
| 3 | UI/DamageVignetteView.cs:71 | OnDisable no limpia `routine` ni `currentRedAlpha`: el alfa fantasma se suma al proximo golpe. | OnDisable los resetea a null y 0. |
| 4 | UI/DamageVignetteView.cs:88 | `brain.Current.Health.MaxHealth` revienta (NullReference) si el soldado no tiene Health. | Guarda `Health == null` antes de leer MaxHealth. |
| 5 | UI/CapasDeHud.cs:121 | Estado estatico sin reset (EnModoTanque, EnRts, listas apagadas/canvasApagados): tras recargar la escena dentro del tanque, ModoTanque(true) salia sin apagar el HUD. | OnDestroy resetea los estaticos (solo si `Instancia == this`). |
| 6 | UI/ButtonSfx.cs:66 | AnimarA hace StartCoroutine desde OnPointerEnter/Exit sin comprobar que el boton este activo: error si el puntero llega con el objeto desactivado. | `if (!isActiveAndEnabled) return;` |
| 7 | UI/AimUI.cs:411 | Si AimUI se desactiva a mitad de un flash, la corrutina muere antes de bajar `flashing` y Update() deja de escribir tamano/color de la mira para siempre. | Nuevo OnDisable: `flashing = false`. |
| 8 | UI/AimUI.cs:473 | OnDamage (suscripcion viva hasta OnDestroy) llama StopAllCoroutines/StartCoroutine con la vista inactiva. | Guarda `isActiveAndEnabled`. |
| 9 | UI/AimUI.cs:519 | Idem en OnEnvironmentHit. | Guarda `isActiveAndEnabled`. |
| 10 | UI/AimUI.cs:255 | Division por cero: `Health.Current / Health.MaxHealth` con MaxHealth 0 da NaN/Infinito en el anillo de vida del enemigo. | Si MaxHealth <= 0 el progreso es 0. |
| 11 | UI/AimUI.cs (caso AimTargetType.Vehicle de UpdateFromAimResult) | `result.Vehicle.IsDestroyed` sin comprobar null (vehiculo destruido entre el raycast y el uso): NullReference. | `result.Vehicle == null ? VehicleTint : ...` |
| 12 | Tutorial/TutorialBeacon.cs:94,176 | Fuga de Material: ShapeMarkerFx.MatCirculo clona un material por baliza y nunca se destruia. | Se guarda `matAnillo` y se destruye en OnDestroy. |
| 13 | Tutorial/TutorialBeacon.cs:154 | `Quaternion.LookRotation` con vector cero (camara sobre la etiqueta) tira warning y rota basura. | Solo rota si `sqrMagnitude > 1e-6`. |
| 14 | Tutorial/MarcaDeObjetivosEnMinimapa.cs:89 | `puntos.AddRange(Extras())` revienta si el delegado devuelve null. | Se guarda el resultado y se comprueba null. |
| 15 | Tutorial/MarcaDeObjetivosEnMinimapa.cs:35 | El delegado estatico `Extras` no se reinicia (retiene cierres/objetos de la partida anterior; con Enter Play Mode sin domain reload devuelve posiciones de objetos destruidos). | Reiniciar() tambien hace `Extras = null`. |
| 16 | UI/PanelDeDano.cs:61 | `onValueChanged.RemoveAllListeners()` en los sliders de dano tambien quitaba el listener AlCambiar de SliderDeAjuste: se perdia el tic sonoro y el destello del chip. | Se guardan las dos acciones propias y solo se quita/agrega esa (RemoveListener/AddListener). |
| 17 | UI/FondoOpacoLink.cs:37 | El fondo es hermano del texto: si el texto se destruye, el fondo queda huerfano (desactivado) para siempre. | OnDestroy: `Destroy(Fondo)`. |
| 18 | UI/RosterView.cs:49 | Rebuild destruye las filas viejas con Destroy diferido: siguen activas, suscriptas al EventBus y en el layout junto a las nuevas hasta fin de frame (doble fila/doble evento). | `SetActive(false)` antes de `Destroy`. |
| 19 | UI/RosterRowView.cs:282,290 | OnDamage/OnHealed leen `Soldier.Health.MaxHealth` sin comprobar: NullReference/MissingReference si el soldado fue destruido o no tiene Health. | Guarda `Soldier == null || Soldier.Health == null` en ambos. |

Revisado y descartado (sin bug real o dudoso, no tocado): KeyRebindView, HudPulido, AjustesDeJuego, AlertQueue, AvisoUnico, MenuDeOrdenes, MinimapFollow, WeaponStatusView, TutorialManager (suscripciones y Desuscribir correctos), EntradaVirtual, VictoriaTutorial, TutorialUI.


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



