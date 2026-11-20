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
