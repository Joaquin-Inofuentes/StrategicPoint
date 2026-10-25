# Mapa de archivos
> 481 scripts. Generado el 2026-10-25 01:36 UTC.

## Actors — 4 scripts, 816 lineas

> El soldado: identidad, piezas, motor de movimiento y aspecto.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `SoldierMotor.cs` | 418 | SoldierMotor | Mueve el transform. Lo usan por igual el jugador y la IA detrás de la misma llamada, sin que a ninguno le importe quién … |
| `SoldierLook.cs` | 177 | SoldierLook | Viste al soldado segun su clase: cambia la malla del SkinnedMeshRenderer por la variante armada por SoldierVariantBuilde… |
| `Soldier.cs` | 155 | Soldier | Reúne las piezas del GameObject y las expone. No decide nada por sí mismo. Se auto-inicializa en Awake a partir de sus p… |
| `SoldierClasses.cs` | 66 | SoldierClasses, Def | Las clases del juego, en un solo lugar: como se llama, que modelo lleva, que armas trae (las teclas 1/2/3 son sus ranura… |

## Ai — 16 scripts, 4,202 lineas

> Cerebro de una unidad no poseida y el driver que avanza la simulacion.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `AiBrain.cs` | 1368 | CombatStance, AiBrain | Postura de combate de una unidad. Libre es el comportamiento historico y por defecto: las otras dos NO son un camino de … |
| `AiBrain.Tactica.cs` | 780 | AiBrain, CoverSubState | Parte "tactica" del cerebro: coberturas (ordenadas por el jugador o buscadas por el propio enemigo en combate), seguir a… |
| `AiBrain.Navegacion.cs` | 273 | AiBrain | AiBrain (parte): navegacion. Simple a proposito: el NavMeshAgent calcula la ruta y el cuerpo la sigue (SoldierMotor apli… |
| `AiBrain.Sentidos.cs` | 252 | AiBrain | AiBrain (parte): linea de tiro, posturas, cobertura y sensado del enemigo mas cercano. |
| `AiBrain.Herido.cs` | 245 | AiBrain | Bug #087 ("un aliado herido se manda al muere"): antes un aliado con poca vida seguia cargando contra el enemigo (Herido… |
| `AiBrain.Vehiculos.cs` | 182 | AiBrain | Pedido explicito: "que los enemigos me ataquen si estoy en el tanque". Antes esto era imposible: el jugador (o cualquier… |
| `AiBrain.Granadas.cs` | 175 | AiBrain | Granadas para la IA: (57) los aliados las lanzan contra enemigos agrupados, y los enemigos tambien en DIFICIL; (58) cual… |
| `WorldSimulationDriver.cs` | 169 | WorldSimulationDriver | Avanza IA, armas y vehículos cada frame en Play mode real. El test automático no usa esto: simula el mismo paso a mano p… |
| `AiBrain.Revivir.cs` | 157 | AiBrain | Ronda 13 (punto 1): "el companero revivido no me sigue ni obedece ninguna orden". Habia cuatro caminos para revivir (tec… |
| `PathPreview.cs` | 126 | PathPreview | Item 218: vista previa de la ruta. Antes de este item el jugador no tenia forma de saber POR DONDE iba a ir la escuadra:… |
| `AiBrain.Disparo.cs` | 123 | AiBrain | Ronda 11 (puntos 3, 12 y 13): como dispara la IA. Antes cada soldado gatillaba en el mismo tick en que entraba en Attack… |
| `AiBrain.Cargas.cs` | 110 | AiBrain | Bug #106: con una carga explosiva a punto de detonar (CargaExplosiva con la mecha encendida) los aliados de la IA no rea… |
| `AiBrain.ImpactosContraParedes.cs` | 91 | AiBrain | AiBrain (parte): "los enemigos disparan a un edificio" (bug #193). Si la linea de tiro dice que hay vista pero las balas… |
| `AjustesDeEscuadra.cs` | 78 | AjustesDeEscuadra | Ajustes de la escuadra aliada, editables desde el Inspector. GameObject: "Systems/AjustesDeEscuadra" (componente Ajustes… |
| `AiBrain.Separacion.cs` | 58 | AiBrain | Bug #086 ("los enemigos se solapan"): el NavMeshAgent solo planifica (sin evitacion, ver SyncAgentSettings) y NavService… |
| `AiState.cs` | 15 | AiState | — |

## Camera — 2 scripts, 1,135 lineas

> Rig de camara: hombro, RTS, transiciones, sacudidas, zoom.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `CameraRig.cs` | 1089 | ControlMode, CameraRig | Posee la cámara y delega su posición en el modo activo (FPS u RTS). |
| `CameraFxSettings.cs` | 46 | CameraFxSettings | Interruptor global de los efectos de camara (sacudida, balanceo al caminar, viñeta de velocidad, destellos, latido). Es … |

## Combat — 14 scripts, 3,031 lineas

> Vida, dano, armas, proyectiles y catalogo.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `Projectile.cs` | 1006 | Projectile | Viaja, comprueba su propio impacto por distancia (sin física) y se devuelve solo al pool. Nunca lo instancia nadie salvo… |
| `WeaponHolder.cs` | 892 | WeaponChangedEvent, WeaponHolder | Un solo evento para "este soldado tiene otra arma puesta", sin importar el camino (recogida del piso, ciclado 1/2/3, IA)… |
| `Granada.cs` | 276 | Granada | Granada de mano (tecla [G], ronda 7). Se tira con una parabola que ella misma calcula: la vista previa que se dibuja al … |
| `Health.cs` | 196 | Health | Única responsabilidad: llevar la cuenta de puntos de vida y avisar cuando cambian o llegan a cero. No sabe que existe un… |
| `HitboxCabeza.cs` | 163 | HitboxCabeza | Bug #044: "si le apunto y disparo en la cabeza deberia decir HEADSHOT; debe haber un cubo de collider especial para la c… |
| `WeaponModels.cs` | 140 | WeaponModels | Carga (y cachea) los prefabs de arma real armados por SP.EditorTools.WeaponPrefabBuilder a partir de los FBX de Assets/A… |
| `ProjectilePool.cs` | 126 | ProjectilePool | Dueño del pool de proyectiles. Cero Instantiate en combate: todo pasa por acá. El pool en sí (ObjectPool<T>) es estado d… |
| `WeaponCatalog.cs` | 82 | WeaponCatalog, Spec | Estadísticas y color de cada arma, en un solo lugar. Las armas recogibles del piso (WeaponPickup) y las teclas rápidas 1… |
| `WeaponPickup.cs` | 81 | WeaponPickedUpEvent, WeaponPickup | Cubo en el mundo que representa un arma. Al equiparla (E cerca), cambia el arma del soldado: daño, cadencia y el color d… |
| `TeamCombatState.cs` | 34 | TeamCombatState | — |
| `IWeapon.cs` | 13 | IWeapon | Contrato mínimo de un arma equipable. Un arma cuerpo a cuerpo puede implementar esto sin implementar recarga (eso vive e… |
| `WeaponKind.cs` | 11 | WeaponKind, ReticleStyle | Los tres primeros se mantienen (los serializa el prefab de armas del piso); los nuevos se agregan al final para no corre… |
| `RoleType.cs` | 6 | RoleType | Flanker se agrega al final: el enum se serializa en las escenas. |
| `TeamId.cs` | 5 | TeamId | — |

## Core — 38 scripts, 6,687 lineas

> Servicios sin escena: bus de eventos, registros, grilla espacial, navegacion, pools, idioma, dificultad.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `SesionLog.cs` | 908 | IEstadoGuardable, SnapSoldado, SnapVehiculo | Una mision (o cualquier sistema) con estado propio que entra en el registro de sesion y se puede restaurar. |
| `WaypointGraph.cs` | 696 | WaypointGraph, NodeMinHeap | ITEM 226 -- Grafo de waypoints (grilla + A* sobre el plano XZ). POR QUE EXISTE (y por que NO es una optimizacion). El mo… |
| `Coberturas.cs` | 427 | Coberturas | Los puntos de cobertura del mapa: donde pararse para tener un obstaculo entre uno y el enemigo. Que cuenta como obstacul… |
| `LocTextos.cs` | 344 | LocTextos | Textos del tutorial y de la mision en ingles (item 27). Loc los suma a su tabla al iniciar; la clave es el texto origina… |
| `PartidaGuardada.cs` | 317 | PartidaGuardadaDatos, PartidaGuardada, Info | P10 (#120): guardar y continuar la partida. Se apoya en SesionLog (Capturar / Restaurar: soldados, vehiculos y el estado… |
| `ReporteDeBug.cs` | 281 | ReporteDeBug | Pedido: "cuando apriete U para reportar, que me abra un cuadro de dialogo para escribir, que se ponga en PAUSA hasta que… |
| `Coberturas.Puntuada.cs` | 275 | CoberturasPuntuadas, Eleccion | Eleccion PONDERADA de cobertura (pedido: "que la IA use las coberturas al atacar con decisiones ponderadas segun su vida… |
| `NavService.cs` | 263 | NavService | EL PEGAMENTO QUE FALTABA. El proyecto ya tenia WaypointGraph (A*) y FlowField, escritos y cubiertos por la suite headles… |
| `Events.cs` | 230 | DamageTakenEvent, HealedEvent, EntityDiedEvent | Eventos: structs inmutables. Nunca llevan una referencia a un MonoBehaviour, solo ids, posiciones y valores. Es la única… |
| `FlowField.cs` | 222 | FlowField | Item 227: campo de flujo. ADVERTENCIA HONESTA SOBRE CUANDO USARLO. Este proyecto mueve a los soldados en linea recta (So… |
| `Vegetacion.cs` | 204 | Vegetacion | Arbustos y bosques del terreno jugable (la mancha incluye 1,5 m de margen alrededor de cada arbol y matorral). Pedido ex… |
| `Loc.cs` | 188 | Idioma, Loc, LocTraductor | Localizacion (item 27), espanol -> ingles. El texto original del juego es la CLAVE (no hay tablas de ids que mantener): … |
| `SpatialGrid.cs` | 187 | SpatialGrid | ActorRegistry.FindNearest(EnemyInRange) barre linealmente TODOS los soldados en cada llamada. AiBrain.Tick() llama a esa… |
| `Deslizador.cs` | 185 | Deslizador | Resolucion de colision para cuerpos que se mueven ESCRIBIENDO EL TRANSFORM, que es como se mueve todo en este proyecto: … |
| `ActorRegistry.cs` | 175 | ActorRegistry | Registro simple de soldados vivos en la escena, para consultas de distancia y de sensado. Sustituye a FindObjectOfType. |
| `Dificultad.cs` | 155 | NivelDificultad, Dificultad, Perfil | Dificultad de la partida: se elige al tocar JUGAR y reparte "potenciadores" de vida y dano a tres grupos: los ENEMIGOS, … |
| `NavMeshViva.cs` | 153 | NavMeshViva | EL NAVMESH QUE SE QUEDABA CON LOS MUROS EN PIE. El NavMesh se hornea en el editor y en partida no cambiaba nunca. Cuando… |
| `DificultadAdaptativa.cs` | 151 | DificultadAdaptativa, DificultadAdaptativaTicker | Bug #240 ("quiero un sistema de dificultad para que se ajuste y sea desafiante, y que vayan variando los danos de los en… |
| `ApoyoEnElPiso.cs` | 128 | ApoyoEnElPiso | Nadie deberia estar flotando ni enterrado, y habia de los dos. En SC_TestLevel -- la escena que arma la suite headless -… |
| `EventBus.cs` | 128 | EventBus, ActionDisposable | Bus de eventos desacoplado: los emisores publican, los oyentes se suscriben. Ninguno de los dos se conoce entre sí. |
| `ObjectPool.cs` | 117 | IPoolable, ObjectPool | Un objeto agrupable sabe reiniciarse al salir del pool y limpiarse al volver. |
| `PruebaEnPlayer.cs` | 109 | PruebaEnPlayer, Mirador | Prueba de la build SIN tocar el mouse ni el foco: "StrategicPoint.exe -pruebaop [-pruebaop-seg 60]" entra solo al nivel … |
| `MetricasDeBuild.cs` | 100 | MetricasDeBuild, Corredor | Modo de medicion para builds: `Juego.exe -spmetrics` carga SC_Gameplay desde el menu (o donde este), espera 6 s de calen… |
| `AyudaDebugger.cs` | 94 | AyudaDebugger | Bugs #045 y #048: Ctrl+Borrar es el atajo de Unity (URP) para el "Rendering Debugger" (Display Stats, Frequently Used, R… |
| `WorldSystemsRegistry.cs` | 92 | WorldSystemsRegistry | Mismo patron que ActorRegistry, pero para los tres tipos que WorldSimulationDriver necesita recorrer cada frame. Antes e… |
| `ModoDios.cs` | 80 | ModoDios, VigilanteDeVelocidadDios | MODO DIOS ([F4]): nadie del bando del jugador recibe dano. Ni el soldado que manejas, ni la escuadra, ni el civil, ni el… |
| `IluminacionTactica.cs` | 74 | IluminacionTactica | LA LUZ COMO RIESGO (diseño del nivel). De noche, un enemigo solo te ve a distancia completa si estas ILUMINADO: bajo una… |
| `ComandosDeDepuracion.cs` | 57 | ComandosDeDepuracion | Ronda 11 (punto 15): comandos de prueba para reproducir a mano el caso "muere el soldado que manejo" y ver si viene el m… |
| `InteligenciaDeEnemigos.cs` | 54 | InteligenciaDeEnemigos | Pedido explicito: "en rojo los enemigos solamente los que los aliados hayan visto o vos hayas visto o te hayan disparado… |
| `GameLog.cs` | 50 | GameLog | Log de flujo de juego (menú, pausa, victoria/derrota, ordenes de alto nivel) separado a propósito de TestLog: TestLog es… |
| `RecursosCache.cs` | 41 | RecursosCache | Resources.Load con memoria: el primer pedido carga, los siguientes salen de un diccionario. No guarda los fallos (un .mp… |
| `ReinicioDeEstaticos.cs` | 41 | ReinicioDeEstaticos | Con "Enter Play Mode sin recarga de dominio" los estaticos sobreviven entre una corrida y la otra (modo dios, regeneraci… |
| `BuscarHijo.cs` | 34 | BuscarHijo | Busca un hijo por nombre (o por ruta "A/B/C") recorriendo SOLO los hijos directos de cada nivel. Mismo resultado que Tra… |
| `RaicesDeEscena.cs` | 29 | RaicesDeEscena | Busca un objeto que es RAIZ de escena por su nombre. Recorre solo las raices de las escenas cargadas (decenas de objetos… |
| `WebGlOptimizador.cs` | 27 | WebGlOptimizador | Ajustes de ejecucion solo para la build WebGL, sin tocar los assets de calidad de PC: el navegador corre en un solo hilo… |
| `TestLog.cs` | 25 | TestLog | Logueo con timer para el test de integración. [t=0.00s] mensaje. |
| `SceneLoader.cs` | 24 | SceneLoader | Unico punto de entrada para cambiar de escena en el juego real. Antes cada pantalla (menu, victoria/derrota, pausa, tuto… |
| `CamaraPrincipal.cs` | 22 | CamaraPrincipal | Camara principal cacheada: Camera.main busca por tag en cada llamada; aqui se resuelve una vez y solo se vuelve a buscar… |

## Demo — 1 scripts, 671 lineas

> Corredor de demo automatica.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `AutoDemoRunner.cs` | 671 | AutoDemoRunner | Corre las 4 fases del guion de prueba en Play mode real (no en Edit mode como HeadlessTestRunner): usa las mismas APIs p… |

## Editor — 115 scripts, 43,561 lineas

> Herramientas de Editor: suite headless, constructores de escena, pipelines de arte.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `HeadlessTestRunner.cs` | 4351 | HeadlessTestRunner, BenchResult, StressResult | Construye el entorno de prueba (suelo, obstáculos, prefabs, 3 soldados, cámara, UI, pool de proyectiles) y corre las 3 f… |
| `HeadlessTestRunner.Fases8a12.cs` | 1614 | HeadlessTestRunner | HeadlessTestRunner (parte): fases 8 a 12 de la suite. |
| `BlockoutArtDresser.cs` | 1320 | BlockoutArtDresser | "Implementa arte en todo": viste el BLOCKOUT (cubos grises) con los prefabs de arte de Assets/_Project/Prefabs/ArteMundo… |
| `ArtSetup.cs` | 1064 | ArtSetup, Prop, RecalibrarAlturaAlReimportar | Pipeline de importacion del arte de Assets/ARTS. Es una herramienta y no un README con pasos a mano a proposito: el arte… |
| `ValidadorJugabilidad.cs` | 966 | ValidadorJugabilidad, Fs | Validador de jugabilidad de la Operacion Cuartel (Informes/11_Validacion_jugabilidad.md). Un "jugador automatico" que re… |
| `WorldArtPipeline.cs` | 915 | WorldArtPipeline, Volumen, ReglaCategoria | Pipeline del pack de arte nuevo (Assets/ARTS/SP_Arte/_FBX_Export): ~90 mallas modulares/ambiente/vehiculos/personajes/ar… |
| `ArtBuilder.cs` | 812 | ArtBuilder, Volumen, PropDef | Segunda mitad del pipeline de arte: con los FBX ya importados y materializados por ArtSetup, esto arma lo que el juego u… |
| `HeadlessTestRunner.Fase23.cs` | 799 | HeadlessTestRunner | La suite corre en Edit mode, donde Unity NO llama Awake/OnEnable/OnDisable a un MonoBehaviour comun (ver CameraRig.Ensur… |
| `AmbientacionDelNivel.cs` | 778 | AmbientacionDelNivel, EstiloDeFarola | AMBIENTACION del nivel de SC_Gameplay (pedido explicito: "que el camino este iluminado", "mejora la iluminacion" y "la a… |
| `LevelBlockoutBuilder.cs` | 762 | LevelBlockoutBuilder, Bloque, Ronda | "Blockout" del nivel de SC_Gameplay: SOLO pintura de terreno + cubos. Todo el bloqueo es un cubo (BoxCollider + Obstacle… |
| `HeadlessTestRunner.Fases13a17.cs` | 740 | HeadlessTestRunner | HeadlessTestRunner (parte): fases 13 a 17 de la suite. |
| `ArteDeOperacion.cs` | 707 | ArteDeOperacion, CeldaTrim, MapaTrim | Pedido: "agrega arte a todo, no quiero ver blocking, mejora el nivel". Cubos de SC_Operacion que seguian siendo cajas gr… |
| `ChecksBugs065.Wp1.cs` | 702 | ChecksBugs065 | WP1: combate. #094 headshots, #095 municion en el HUD, #079 rachas, #065 cursor de baja, #096 Q con margen y diana. Hay … |
| `OperacionBuilder.CiudadDeCubos.cs` | 682 | OperacionBuilder, OcupacionCc, MallaCc | Ciudad de cubos que puebla el RESTO del mapa (800 x 800) fuera del recorrido jugable: manzanas de edificios-cubo de altu… |
| `OperacionBuilder.cs` | 679 | OperacionBuilder | "Operacion Cuartel": nivel NUEVO de blockout (SC_Operacion). Todo el escenario es cubos (con colliders y NavMesh real); … |
| `ArteDeOperacion.Pasada2.cs` | 655 | ArteDeOperacion, Lote | Segunda pasada de arte ("mejora todo lo que puedas"): lo que despues de la primera seguia leyendose como blocking. de es… |
| `ChecksBugs065.Wp6.cs` | 652 | ChecksBugs065 | WP6: vehiculos y tanque. #091 giro/anillo/cruceta, #090 el tanque destruye obstaculos, #069/#071 aliados contra vehiculo… |
| `OperacionBuilder.Huida.cs` | 650 | OperacionBuilder | Zona 4: patio del tanque, autopista, plantilla de la camioneta enemiga y helicoptero (OperacionDirector.Huida/Extraer). |
| `HeadlessTestRunner.Fase21.cs` | 586 | HeadlessTestRunner | FASE 21 (Ronda 13): los doce pedidos de la ronda. Cada bloque nombra el punto del pedido que audita. 1 revivido obedece … |
| `ChecksBugs065.Wp9a.cs` | 577 | ChecksBugs065 | WP9a: rediseno de los Puestos (#082, #085 parte puestos, #084 con la carga) y la cinematica inicial (#097 parte 1). Hay … |
| `ChecksBugs104.Guardado.cs` | 574 | ChecksBugs065, CasoDeGuardado | P10 (#120): checks del guardado de partida. Todos corren en ModoPrueba (archivo partida_<escena>_prueba.json): la partid… |
| `ChecksBugs065.Wp10.cs` | 572 | ChecksBugs065 | WP10 (#101): RESISTIR dependiente del RTS, "DIRIGIR LA DEFENSA DESDE LA RADIO". Play FRESCO por check (ChecksBugs065.Cor… |
| `ChecksBugs065.Wp5a.cs` | 558 | ChecksBugs065 | WP5a: interaccion con [E]. #083/#084 rol requerido y cartel, #088 revivir con animacion y bloqueo, #089 sonido y anillos… |
| `BlockoutScenesBuilder.cs` | 554 | BlockoutScenesBuilder | Cinco escenas de PURO BLOCKING para diseno de niveles (SC_Blockout_01..05): solo cubos de color plano, rampas, plataform… |
| `ChecksBugs065.Bugs256a281.cs` | 547 | ChecksBugs065 | Tanda de bugs #256-#281 (pedidos de juego del 2026-10-19). Cada check pide un Play FRESCO sobre SC_Operacion: ChecksBugs… |
| `ChecksBugs065.Wp8.cs` | 535 | ChecksBugs065 | WP8: rediseno del cuartel (#078). Hay que estar en Play sobre SC_Operacion y conviene un Play fresco por check (ChecksBu… |
| `HierarchyPowerTools.cs` | 533 | HierarchyPowerTools | Caches de GUI y GC Alloc |
| `EstrategiaDelNivel.cs` | 521 | EstrategiaDelNivel | LA ESTRATEGIA DEL NIVEL: que cada camino pida un ROL distinto y que cambiar de soldado sea la decision clave. ASALTO (Do… |
| `ValidacionBugs.cs` | 504 | ValidacionBugs | Validacion automatica de los bugs 8-21 + el nivel completo (pedido: "itera 20 veces si o si todo el nivel y sobre los bu… |
| `ChecksBugs104.Rts.cs` | 497 | ChecksBugs065 | Tanda #104-#132, P6: RTS, controles, lectura y ordenes (#116, #121, #124, #117, #119, #112, #118, #115). En RTS no se pu… |
| `ChecksBugs065.Wp4.cs` | 493 | ChecksBugs065, W80Res | WP4: IA de combate. #080 (enemigo que no dispara), #081 (cubrirse para disparar), #074 (aliados usan coberturas), #085 (… |
| `ChecksBugs065.Wp9b.cs` | 492 | ChecksBugs065, W9bCarrera | WP9b: HUIR rediseñado (#097 parte 2 + #073 ajuste). Hay que estar en Play sobre SC_Operacion y conviene un Play FRESCO p… |
| `OperacionBuilder.Ambiente.cs` | 482 | OperacionBuilder, MallaGen | Ambiente de la Operacion (pedido: "el terreno debe ser tierra, algo de pasto, tiendas de campana y todo militar, y en la… |
| `ChecksBugs065.Wp2.cs` | 480 | ChecksBugs065 | WP2: audio. #065 muerte 3D y mas baja, #095 disparos 3D con distorsion y nada 2D salvo musica/ambiente/UI, #072 graves y… |
| `ChecksBugs104.Resistir.cs` | 472 | ChecksBugs065 | Tanda #104-#132, P7: Resistir rediseñado (#120 revivir / radio como rol / cambio con C, #122 cuentas, #114 pasos y guia)… |
| `ChecksBugs065.cs` | 465 | EscenaDelCheckAttribute, ChecksBugs065 | Escena en la que corre un check (por defecto SC_Operacion). Lo lee la maquina de fases de ChecksBugs065. |
| `OperacionBuilder.Cuartel.cs` | 465 | OperacionBuilder | Zona 1: cuartel militar (OperacionDirector.Infiltrar). Rediseno WP8 (#078): cuatro sectores con su color, divisiones int… |
| `ChecksBugs065.Wp5b.cs` | 461 | ChecksBugs065, CasoDeAccion | WP5b: feedback de acciones. #066 engranajes sobre el que opera y sobre el interactuable, #067 animacion para toda accion… |
| `TutorialSceneBuilder.cs` | 461 | TutorialSceneBuilder, Cubo | Arma SC_Tutorial: una escena NUEVA hecha a partir de SC_Gameplay (asi trae el jugador, los aliados, el tanque, el HUD, l… |
| `HeadlessTestRunner.Fase19.cs` | 432 | HeadlessTestRunner | FASE 19 (ronda 9, segunda tanda): ayudas del tutorial, radial legible, boton de menu uniforme, zoom, salto agachado, ord… |
| `ChecksBugs065.Pendientes.cs` | 416 | ChecksBugs065 | Checks de los bugs que habian quedado sin prueba propia (#242 y #251). Un Play FRESCO sobre SC_Operacion cada uno. 242a … |
| `OperacionBuilder.PulidoFinal.cs` | 402 | OperacionBuilder | Pulido final del Nivel 2 (pedido: "modelos de edificio y PC, mas arte en la parte de hackear, rework salida norte, mas a… |
| `ChecksBugs065.Bugs231a255.cs` | 396 | ChecksBugs065 | Tanda de bugs #231-#255 (pedidos de juego del 2026-10-15). Cada check pide un Play FRESCO sobre SC_Operacion: ChecksBugs… |
| `OperacionBuilder.Arte.cs` | 386 | OperacionBuilder, EspecDeTextura, CeldaTrim | Pedido ("no quiero nada de blocking, todo arte"): los cubos del nivel ya no son de color plano. Cada material del nivel … |
| `OperacionBuilder.Ciudad.cs` | 365 | OperacionBuilder | Bug #063: "mejora el diseño del nivel ... mas casas, que sea una ciudad (esta aislada) ... mejora las luminarias y quier… |
| `ChecksBugs065.Wp7.cs` | 347 | ChecksBugs065 | WP7: helicoptero de extraccion y cierre de la mision. #075 ondas de viento y polvo, #076 ametralladoras con artilleros, … |
| `OperacionBuilder.Decorado.cs` | 346 | OperacionBuilder | Pedido ("agrega arte en todo lado, que no haya nada de blocking"): ademas de las texturas, el nivel se viste con 1) deta… |
| `ChecksBugs104.Combate.cs` | 330 | ChecksBugs065 | Tanda #104-#132, P1: combate. #107/#108 headshot, #110 arma inicial / reservas / ADS, #132 sonido y animacion de recarga… |
| `TerrainSetupHelper.cs` | 319 | TerrainSetupHelper | 1. Asegurar carpeta para assets de terreno |
| `WeaponStatusUiPipeline.cs` | 309 | WeaponStatusUiPipeline | Configura e inicializa el panel de arma y municion (WeaponStatus) en modo edicion para que se vea IDENTICO a como se ve … |
| `ChecksBugs065.Wp3.cs` | 294 | ChecksBugs065 | WP3: UI de opciones, controles y minimapa (#098, #099, #100, #065 minimapa). Hay que estar en Play sobre SC_Operacion. |
| `SoldierVariantBuilder.cs` | 283 | SoldierVariantBuilder, Pieza, Variante | Arma las VARIANTES de soldado (asalto, flanqueador, medico, fusilero y francotirador) a partir de los FBX de arte de Ass… |
| `ChecksBugs104.Extraer.cs` | 281 | ChecksBugs065 | Tanda #104-#132, P8: Extraer y Victoria (#127 oleadas + helicoptero que cubre, #128 los milicianos suben, #129 himno nue… |
| `PielSoldadoAnalisis.cs` | 279 | PielSoldadoAnalisis, Cadena | Analisis y saneado de los pesos de piel de las mallas de soldado (#109). Las mallas del arte son cajas de bajo poligonaj… |
| `CoberturaBench.cs` | 276 | CoberturaBench, Trial, Seguimiento | Banco de la IA de coberturas (pedido: "iterar 5 veces con medicion"). Corre en Play (SC_Gameplay) con el tiempo acelerad… |
| `BalanceBench.cs` | 273 | BalanceBench, Resultado | Banco de balance: corre en Play mode (SC_Gameplay) y hace pelear a los soldados entre si con el tiempo acelerado, para m… |
| `NightLightingBuilder.cs` | 267 | NightLightingBuilder | Pedido explicito: "quiero que de la imagen de noche, una noche medio iluminada por lamparas y estrellas en el cielo" + "… |
| `ChecksBugs104.Guia.cs` | 260 | ChecksBugs065 | Tanda #104-#132, P4: guia y feedback en FPS (fases 1-4). #105 aliados que te siguen + carteles sin superponer + guia de … |
| `ChecksBugs104.Motor.cs` | 259 | ChecksBugs065 | Tanda #104-#132, P5: motor, efectos y lineas. #126 el soldado que queda apoyado mas alto que su piso baja (no levita a y… |
| `OperacionBuilder.Puestos.cs` | 256 | OperacionBuilder | Zona 2 (WP9a, #082/#097/#085): MURALLA DE CONTENCION. Una sola capa transversal en z = -170 (x -61..61, 5 m de alto) con… |
| `RosterUiPipeline.cs` | 247 | RosterUiPipeline | Pedido explicito: "en Roster los bloques... quiero un prefab para cada caso y q tenga puntero al soldado... y se sincron… |
| `SkinTransfer.cs` | 247 | SkinTransfer | POR QUE EXISTE ESTO. El soldado del juego venia con la textura ESTIRADA en franjas, y no era un problema del material: s… |
| `OperacionBuilder.Decals.cs` | 246 | OperacionBuilder | Pedido ("hay lineas separadoras entre los tiles; no deberian tener tiles y si decals para emular variedad y que no haya … |
| `ReadmeEditor.cs` | 243 | ReadmeEditor | Match selection color which works nicely for both light and dark skins |
| `HeadlessTestRunner.Fase20.cs` | 240 | HeadlessTestRunner | FASE 20 (Ronda 11): fin de partida por escuadra caida, IA con dispersion/rafagas/alcance de arma, umbral de Q, E tap/hol… |
| `OperacionBuilder.Retoque.cs` | 222 | OperacionBuilder | Pulido de SC_Operacion (parte 3), pensado para correr sobre la escena YA construida (reconstruirla entera tarda varios m… |
| `PrimerFramePreview.cs` | 222 | PrimerFramePreview | Deja SC_Gameplay, SIN dar Play, como se ve el primer frame del juego real: camara detras del primer soldado, minimapa mi… |
| `MenuSceneBuilder.cs` | 197 | MenuSceneBuilder | Arma la escena de menú principal: fondo, título, botón Jugar y botón Salir. Mismo patrón que HeadlessTestRunner: todo po… |
| `ChecksBugs104.Cuartel.cs` | 195 | ChecksBugs065 | Tanda #104-#132, P3: cuartel. #104 visibilidad de los enemigos en altura (torres, reflectores) y faro visible / con efec… |
| `MapasDeCarga.cs` | 190 | MapasDeCarga | P11 (#130): imagenes del mapa para la pantalla de carga. Renderiza una vista cenital ortografica del nivel (generada loc… |
| `OperacionBuilder.Mando.cs` | 189 | OperacionBuilder | WP10 (#101): "DIRIGIR LA DEFENSA DESDE LA RADIO". Lo que el objetivo 5 necesita en el nivel (en el grupo 6_Ciudad/Mando,… |
| `LoadingSceneBuilder.cs` | 185 | LoadingSceneBuilder | Arma la escena intermedia de carga: nada mas que fondo oscuro, un texto "CARGANDO" y una barra de progreso. Mismo patron… |
| `HeadlessTestRunner.BusquedasGlobales.cs` | 184 | HeadlessTestRunner | Red de seguridad del plan "eliminar los Find de runtime": el codigo de runtime solo puede BAJAR su cantidad de busquedas… |
| `ChecksBugs065.Wp11.cs` | 183 | ChecksBugs065 | WP11: integracion. Checks que faltaban: #063 (oleadas de la ciudad escalonadas y dispersas) y #064 (los aliados suben al… |
| `ChecksBugs104.Rapel.cs` | 179 | ChecksBugs065 | Tanda #104-#132, P9: cinematica de apertura con rapel desde el helicoptero (#123). El arnes de checks entra a un Play fr… |
| `AntesProbe.cs` | 175 | AntesProbe, Paso | Ronda 11: instrumento SOLO de medicion (no toca ninguna regla del juego). Sirve para dejar registrado el "antes" de cada… |
| `SondaDeRendimiento.cs` | 163 | SondaDeRendimiento | Sonda de rendimiento por CLI (en Play): eval "return SP.EditorTools.SondaDeRendimiento.Iniciar();"   -> prende el Profil… |
| `OperacionBuilder.ArteNiveles.cs` | 150 | OperacionBuilder | Arte y entorno para el Nivel 1 (SC_Gameplay) y el Tutorial (SC_Tutorial), igual que el "9_ArteExtra" del Nivel 2: arbole… |
| `MedidorDeFases.cs` | 146 | MedidorDeFases | WP11: medidor de rendimiento por fase que separa lo que cuesta el JUEGO de lo que cuesta el editor. Se usa por CLI en Pl… |
| `OperacionBuilder.Fusion.cs` | 145 | OperacionBuilder, ClaveFusion | Pulido de rendimiento de SC_Operacion (parte 2): la ciudad usa ~6.000 piezas del kit SOLO VISUALES (losetas de piso, asf… |
| `HeadlessBuilder.cs` | 141 | HeadlessBuilder | Resuelve el path de salida: variable de entorno (para CI) > EditorPrefs (configurable por herramienta) > default relativ… |
| `MonedaMunicionBuilder.cs` | 140 | MonedaMunicionBuilder | Ronda 12: la municion que sueltan los enemigos era un cubo amarillo. Ahora es una MONEDA 3D con una bala en relieve, com… |
| `RutasDelNivel.cs` | 138 | TipoDeRuta, RutaDelNivel, RutasDelNivel | Los CAMINOS del nivel de SC_Gameplay como datos (poligonales en XZ): una sola fuente de verdad para El nivel es un corre… |
| `HeadlessTestRunner.Fase18.cs` | 132 | HeadlessTestRunner | FASE 18 (ronda 9): reservas de municion, mando, accesibilidad, fuego amigo en DIFICIL, IA con granadas y supresion, esta… |
| `WeaponPrefabBuilder.cs` | 131 | WeaponPrefabBuilder | Arma los prefabs de arma REAL (rifle/pistola/pesada/metralleta) a partir de los FBX multi-parte de Assets/ARTS/SP_Arte/_… |
| `OperacionBuilder.Centro.cs` | 128 | OperacionBuilder | Zona 3: centro de datos, computadora, oleadas y torretas de la salida norte (OperacionDirector.Centro). |
| `PlaymodeCleanup.cs` | 127 | PlaymodeCleanup | BUG REAL: decals (agujeros de bala, crateres) y escombros creados en Play mode quedaban manchando la escena despues de f… |
| `FondoDelMundoBuilder.cs` | 126 | FondoDelMundoBuilder | Pedido explicito: "expandi el mapa, no debo poder ver el vacio, llena el nivel y poblalo de arboles en el fondo muy lejo… |
| `ChecksBugs104.Arte.cs` | 119 | ChecksBugs065 | Tanda #104-#132, P2: arte. #109 pesos de piel de los soldados (astillas mano->cadera con la animacion de accion). |
| `CinematicaIntroBuilder.cs` | 118 | CinematicaIntroBuilder | Arma el camino de camara de la cinematica de apertura en SC_Gameplay: un GameObject raiz "CinematicaDeIntro" (mismo nive… |
| `OperacionPrueba.cs` | 117 | OperacionPrueba | Prueba por CLI de la Operacion Cuartel: "arranca desde el objetivo X". Desde la terminal (editor abierto, SC_Operacion e… |
| `ChecksBugs104.Carga.cs` | 116 | ChecksBugs065 | P11 (#130, #131): pantalla de carga con mapa y texto de la intro. Bug130  (SC_MainMenu) SceneLoader.Cargar("SC_Gameplay"… |
| `MissionFlowTestRunner.cs` | 109 | MissionFlowTestRunner, MissionFlowTestBehaviour | Pedido explicito: "testea el completar la mision inicial" + "testea el nivel entero hasta terminarlo y quiero que diseñe… |
| `ChecksBugs104.Comun.cs` | 104 | ChecksBugs065 | Tanda #104-#132, P0: helpers comunes de los checks. Las capturas de validacion usan el prefijo v3_ + numero de bug (Asse… |
| `MinimapIconBakePipeline.cs` | 104 | MinimapIconBakePipeline | Hornea en la escena el icono de minimapa de cada soldado/vehiculo YA colocado a mano (por el level builder), para que el… |
| `AudioAnalisisReport.cs` | 101 | AudioAnalisisReport, Fila | Item 65 (Ronda 11): "los sonidos se verificaron por metricas y no de oido". Un agente no oye, pero puede medir mas que "… |
| `TexturasDeTerreno.cs` | 99 | TexturasDeTerreno | Capas extra del terreno del nivel, generadas por codigo (mismo estilo de manchas suaves de 128 px que las de pasto y tie… |
| `NavMeshSetupPipeline.cs` | 98 | NavMeshSetupPipeline | Dejar activa la escena principal |
| `AuditoriaDeColliders.cs` | 83 | AuditoriaDeColliders | — |
| `OperacionBuilder.Correcciones.cs` | 82 | OperacionBuilder | Correcciones de los bugs #236 / #239 / #252 sobre SC_Operacion ya construida (se corre despues de RetocarEscenaAbierta):… |
| `AudicionDeSonidos.cs` | 79 | AudicionDeSonidos | Item 65 (Ronda 12): el analisis de audio mide, pero el timbre y "si queda bien" solo lo juzga un oido. Esta herramienta … |
| `AudioImportFix.cs` | 76 | AudioImportFix | WP2 (#065/#095): un clip ESTEREO en una AudioSource 3D no se posiciona bien (los dos canales quedan abiertos a izquierda… |
| `CliBuilder.cs` | 76 | CliBuilder | Build de Windows x64 invocable por linea de comandos (-batchmode -executeMethod SP.EditorTools.CliBuilder.BuildWindows64… |
| `CinematicaSonda.cs` | 64 | CinematicaSonda | Sonda de la cinematica inicial (bug #136, "esta solapado"): con el juego en Play, mira cada frame si la camara de la cin… |
| `SoldierPrefabPipeline.cs` | 64 | SoldierPrefabPipeline | Pedido explicito: "Crea un prefab para cada soldado y limpia un poco". Los 3 aliados jugables (Soldado_1_Vega/2_Kes/3_Do… |
| `BuildAndPack.cs` | 62 | BuildAndPack | — |
| `OperacionBuilder.Rendimiento.cs` | 59 | OperacionBuilder | Pulido de rendimiento de SC_Operacion: la ciudad tiene ~13.000 renderers y casi ninguno era estatico (9.361 draw calls e… |
| `TutorialAutoPlayerMenu.cs` | 57 | TutorialAutoPlayerMenu | Menu de Editor para el pedido "quiero un script con timers para el tutorial que de manera mecanica haga todo el tutorial… |
| `ArtsUsoReport.cs` | 55 | ArtsUsoReport | Item 89: dice cuanto de Assets/ARTS usa de verdad el juego. Raices = todo lo que vive fuera de ARTS (escenas, prefabs, m… |
| `RespaldoDeEscenas.cs` | 55 | RespaldoDeEscenas | Item 93: SC_TestLevel, SC_Menu y SC_Tutorial se regeneran por codigo (la suite y los builders las reescriben), asi que u… |
| `OperacionBuilder.CierreCiudad.cs` | 52 | OperacionBuilder | #116: "en el mapa hay sectores no terminados". Vista cenital de los 4 bordes de la ciudad (Assets/Validacion/v3_116_*): … |
| `HeadlessTestRunner.Fase22.cs` | 51 | HeadlessTestRunner | FASE 22: eliminacion de los Find de runtime. Los servicios unicos exponen "Activo", el tutorial lee un catalogo serializ… |
| `MisionBuilder.cs` | 38 | MisionBuilder | Arma la mision de rescate en SC_Gameplay: un objeto "Mision" con el MisionDirector y las referencias a los prefabs (enem… |
| `RunOnLaunch.cs` | 35 | RunOnLaunch | Solo correr si estamos en batchmode y pasamos un argumento especial |
| `HeliFix.cs` | 24 | HeliFix | — |

## Interaction — 3 scripts, 145 lineas

> Contrato de lo interactuable.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `RolRequerido.cs` | 84 | RolRequerido | Bugs #083/#084: apretar [E] frente a algo que exige OTRO rol no hacia nada ("aprieto E y no pasa nada"). Este es el unic… |
| `IInteractable.cs` | 32 | IInteractable | Interfaz comun para "objetos con los que el jugador interactua" (cajas de suministro, puntos de cobertura manuales, rehe… |
| `IOrdenableAAliados.cs` | 29 | IOrdenableAAliados | Un interactuable que el jugador puede MANDAR a hacer a un aliado: apuntarle, tocar [Q] (o elegir INTERACTUAR en el radia… |

## Mision — 13 scripts, 3,399 lineas

> La partida: objetivos, oleadas, rehen, helicoptero, cinematica final.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `MisionDirector.cs` | 849 | FaseDeMision, MisionDirector, Carril | MISION DE RESCATE (partida principal). 1. INFILTRAR   avanzar entre las lineas enemigas hasta el CENTRO (la plaza de la … |
| `Helicoptero.cs` | 650 | Helicoptero, EstadoDePivote | El helicoptero de extraccion (geometria P_Veh_Heli del arte): helices que giran, disco de desenfoque a tope de vueltas, … |
| `CinematicaDeIntro.cs` | 379 | CinematicaDeIntro | Cinematica de APERTURA: pedido explicito ("una simple cinematica donde muestra primero donde apareces, luego a donde deb… |
| `CinematicaDeVictoria.cs` | 344 | CinematicaDeVictoria | Cinematica de victoria: la escuadra y el civil suben al helicoptero, este despega en una nube de polvo y huye hacia el o… |
| `EstadisticasDeMision.cs` | 247 | EstadisticasDeMision, Fila | Bug #064: "quiero que diga con cuantos termine vivos y metricas de cuantos eliminaron mis aliados y cuantas muertes tuve… |
| `MisionHud.cs` | 237 | MisionHud | Cartel de la mision (arriba al centro, debajo de "ENEMIGOS · ESCUADRA"): capitulo, objetivo con su distancia o su tempor… |
| `PivoteMetralleta.cs` | 178 | PivoteMetralleta, CasquillosDeMetralleta | Bug #076: la ametralladora del helicoptero ya no es decorativa ni fija. Hay una por lado (puerta izquierda y derecha), c… |
| `TripulacionDelHeli.cs` | 178 | TripulacionDelHeli, PoseSentada | Bug #076: los que viajan en el helicoptero se ven por las puertas abiertas. Dos artilleros (uno por ametralladora, giran… |
| `EstadoDePartida.cs` | 125 | CausaDeDerrota, EstadoDePartida | Ronda 11 (puntos 1 y 20). Antes la derrota se decidia UNA sola vez, en la corrutina de muerte del soldado poseido, asi q… |
| `CinematicaIntroPath.cs` | 104 | CinematicaIntroPath | Camino de la camara de la cinematica de apertura: una lista ORDENADA de CinematicaWaypoint, hijos directos de este mismo… |
| `AntenaDeRadio.cs` | 52 | AntenaDeRadio | La antena de radio del fortin: objetivo OPCIONAL que cambia el final de la partida. Mientras siga en pie, el enemigo pid… |
| `MarcadorDeAparicion.cs` | 29 | MarcadorDeAparicion | Marcador puramente visual (Gizmos, sin Renderer/Collider: cero costo e invisible en Game view/build) que queda en el pun… |
| `CinematicaWaypoint.cs` | 27 | CinematicaWaypoint | Un punto de camara de la cinematica de apertura (ver CinematicaIntroPath/CinematicaDeIntro). Pedido explicito: "cada emp… |

## Operacion — 47 scripts, 13,180 lineas

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `OperacionDirector.Mando.cs` | 1436 | OperacionDirector, OleadaDelMando, Vacio | Fase 5 (RESISTIR), WP10 (#101): "DIRIGIR LA DEFENSA DESDE LA RADIO". El objetivo solo se gana coordinando desde la vista… |
| `OperacionDirector.cs` | 770 | FaseOperacion, PuestoDeVoladura, OleadaDeReserva | WP9a (#082/#097): un puesto de la muralla de contencion = un bunker con su punto de carga. El "Asalto" coloca la carga, … |
| `CinematicaDeRapel.cs` | 649 | CinematicaDeRapel, Rapelista, PoseDeRapel | P9 (#123): cinematica de APERTURA de la Operacion Cuartel (~12,5 s): el helicoptero entra de frente a la camara, frena y… |
| `CinematicaDeOperacion.cs` | 470 | CinematicaDeOperacion, Clave | Cinematica INICIAL de la Operacion Cuartel (rediseno): UNA SOLA CAMARA que recorre todo el nivel sin cortes, de principi… |
| `CargaExplosiva.cs` | 456 | CargaExplosiva, Estado | WP9a (#082, #084): punto de carga de un puesto de la muralla. SOLO el ASALTO la coloca (mantener [E] 3,5 s a menos de 2,… |
| `OperacionDirector.Huida.cs` | 450 | OperacionDirector | Fase 4 (HUIR): subir al tanque, trayecto de 60 s con camionetas perseguidoras, llegada en calma y destruccion del tanque… |
| `OperacionHud.cs` | 449 | OperacionHud | Cartel de la Operacion Cuartel: capitulo + objetivo (arriba a la izquierda), temporizador grande al centro, barra de pro… |
| `OperacionDirector.Jefe.cs` | 425 | SubfaseHuida, OperacionDirector | Subfases del objetivo 4 (HUIR). WP9b (#097 parte 2): el tanque ya no espera con las puertas abiertas; es un JEFE que hay… |
| `OperacionDirector.Extraer.cs` | 410 | OperacionDirector | Fase 6 (EXTRAER): subida al helicoptero, cinematica de salida, fundido y victoria. |
| `OperacionDirector.Resistir.cs` | 406 | OperacionDirector | Fase 5 (RESISTIR): coberturas de la plaza, refuerzos y oleadas de la ciudad, llegada del helicoptero. |
| `OperacionDirector.Final.cs` | 387 | OperacionDirector | WP9b-2 (#097 / #073): la carrera (emboscadas del canadon, helicoptero Halcon) y la cinematica final en camara lenta. |
| `TanqueJefe.cs` | 363 | TanqueJefe, Estado | WP9b (#097): el BLINDADO GOLIAT, jefe del objetivo 4. Es el tanque de la fuga en manos enemigas (bando Enemy, Driver + P… |
| `OperacionTerminal.cs` | 328 | OperacionTerminal | Punto de interaccion de la Operacion Cuartel: el panel de cada puesto de control (se mantiene [E] y SOLO lo puede hacer … |
| `OperacionDirector.TomaFinal.cs` | 323 | OperacionDirector | Bug #077: toma final de la extraccion. En vez de planos aereos y un fundido a negro, la camara salta al piso (1,2 m), de… |
| `ReflectorVigia.cs` | 321 | ReflectorVigia | WP8 (#078): reflector de vigilancia del cuartel. Torre de 6 m con un operador (soldado enemigo en la plataforma) y una l… |
| `OperacionDirector.Extraer.Cobertura.cs` | 306 | OperacionDirector | Fase 6 (EXTRAER), #127: el helicoptero llega y se queda en ESTACIONARIO sobre la zona de aterrizaje, cubriendo con fuego… |
| `HelicopteroEnemigo.cs` | 302 | HelicopteroEnemigo, Estado | WP9b (#097/#073): el helicoptero enemigo HALCON de la carrera. NO es el Helicoptero de la extraccion (ese tiene un singl… |
| `OperacionDirector.Centro.cs` | 302 | OperacionDirector | Fase 3 (CENTRO DE DATOS): hackeo de 30 s con oleadas y camionetas de asalto. Activar()/InstanciarCamioneta() tambien los… |
| `OperacionDirector.Puestos.cs` | 290 | OperacionDirector | Fase 2 (PUESTOS, WP9a #082/#097): MURALLA DE CONTENCION. Una sola capa transversal (z -170) con dos bunkers en los flanc… |
| `FrancotiradorEnTorre.cs` | 273 | TiradorDeTorre, FrancotiradorEnTorre, Fase | WP8 (#078): tiradores fijos de las torres del cuartel. Son soldados enemigos comunes (con vida, cuerpo y baja) subidos a… |
| `TorreDestruible.cs` | 269 | TorreDestruible, Tipo, VuelcoDeTorre | WP8 (#078): torre del cuartel con alguien arriba (francotirador de 12 m o torreta vigia de 5 m). Va en la RAIZ de la tor… |
| `OperacionAuto.cs` | 267 | OperacionAuto, Tactica | CAMIONETA enemiga con metralleta que persigue al tanque por la ruta de escape (los enemigos manejan camionetas, el jugad… |
| `CineDeHuida.cs` | 266 | CineDeHuida | WP9b (#097): lienzo y bloqueo compartidos por las dos cinematicas de la huida (la toma del jefe y la cinematica final). … |
| `SectorDeDefensa.cs` | 224 | SectorDeDefensa, Estado, PuntoDeCobertura | WP10 (#101): un SECTOR de la defensa de la ciudad. Anillo amarillo de 9 m de radio en el piso (se lee desde la vista tac… |
| `AnilloDeAviso.cs` | 220 | AnilloDeAviso | WP9b (#097): telegrafia de impacto en el piso, reutilizable (cañonazo del jefe, cohetes del helicoptero). Un circulo roj… |
| `GuiaDeObjetivo.cs` | 218 | GuiaDeObjetivo | Bug #105: guia de "que hago ahora" en las fases 1 a 4 (cuartel, muralla, centro de datos, patio/tanque). Une tres ayudas… |
| `ReparacionDeTanque.cs` | 215 | ReparacionDeTanque | WP9b (#097): H2 de la huida. Kes (el Flanqueador) dice "¡Puedo repararlo, cubrime!", recibe una orden forzada de ir al t… |
| `DefensaDelCentro.cs` | 207 | DefensaDelCentro, TorretaDeDefensa | Bug #272: "quiero que haya equipo de torretas para defender este puesto y que los enemigos vengan en helicopteros; los h… |
| `OperacionAuto.Efectos.cs` | 198 | OperacionAuto | Bug #056 / #058: la camioneta enemiga se tiene que SENTIR. #058 "que los giros de las curvas no sean tan bruscos, que te… |
| `EmboscadaDeCohetes.cs` | 186 | CoheteDeAviso, EmboscadaDeCohetes | WP9b (#097/#073): cohete LENTO con aviso en el piso, comun a las emboscadas del canadon y al helicoptero Halcon. Al lanz… |
| `Atrincherar.cs` | 180 | Atrincherar | Bug #042: "estaria bueno que los enemigos esten cubiertos, asi daria mas la ilusion de guerra". Los guardias del cuartel… |
| `CivilesDeAmbiente.cs` | 163 | CivilesDeAmbiente, Ruta, Figura | Gente y autos DECORATIVOS de la ciudad de cubos (OperacionBuilder.CiudadDeCubos): civiles-cubo que caminan por las vered… |
| `MandoTactico.cs` | 146 | MandoTactico | WP10 (#101): estado y reglas del objetivo 5, "DIRIGIR LA DEFENSA DESDE LA RADIO". El director (OperacionDirector.Mando.c… |
| `GuiaDeMando.cs` | 134 | GuiaDeMando | P7 (#114): guia del tutorial del mando tactico (objetivo 5, "Aca me perdi: falta mas instrucciones o punteado del piso")… |
| `MenuDeTrampas.cs` | 121 | MenuDeTrampas | Bug #147 ("quiero modo cheat para cambiar entre momentos, saltearme escenas o ir a una parte ya avanzada"): [F10] abre u… |
| `EmplazamientoDeTorreta.cs` | 115 | EmplazamientoDeTorreta | Bug #171 ("quiero efecto de circulos vibrantes sobre estas torretas y que tengan sacos de arena"): se agrega en tiempo d… |
| `CazaDeEnemigos.cs` | 108 | CazaDeEnemigos | Bugs #17, #20 y #21: "los enemigos no se mueven / no vienen a atacarme, estan inmoviles. Falta trigger y destino fijado"… |
| `OperacionDirector.Infiltrar.cs` | 108 | OperacionDirector | Fase 1 (INFILTRAR): cuartel militar. Hundir() lo comparten el porton y las barreras de los puestos. |
| `AnilloDeSubida.cs` | 101 | AnilloDeSubida | Bug #111: en HUIR, cuando el tanque ya es nuestro y hay que subirse, un anillo vibrante en el piso lo marca (el HUD y la… |
| `VozDeCinematica.cs` | 100 | VozDeCinematica, Linea | Voz del COMANDANTE en las cinematicas de apertura (sobrevuelo + rapel). Cada linea es un clip aparte en Resources/Audio/… |
| `OperacionDirector.Guardado.cs` | 98 | OperacionDirector | P10 (#120): guardar partida. El autoguardado se programa al empezar cada objetivo y al terminar cada cinematica (apertur… |
| `BlockoutObjetivo.cs` | 97 | BlockoutObjetivo, Modo | Objetivo cumplible de las escenas de blockout (SC_Blockout_01..05). Sin el, la unica forma de ganar era matar a TODOS lo… |
| `OperacionTextoMundo.cs` | 95 | OperacionTextoMundo | Los carteles del nivel son TextMesh. Con la fuente integrada el shader de texto (GUI/Text Shader) ignora la profundidad … |
| `RampaCaminable.cs` | 68 | RampaCaminable | WP10 (#101): la rampa del campanario. El motor de los soldados camina en plano (la altura solo cambia con saltos, trepas… |
| `ArtilleroDeTorreta.cs` | 64 | ArtilleroDeTorreta | Una MonoBehaviour que se agrega a la escena desde el builder debe vivir en un archivo con su mismo nombre: si no, el pla… |
| `EntradaDeReserva.cs` | 57 | EntradaDeReserva | Las reservas aparecian en suelo abierto, sin ningun acceso (bug 12). Al activarse una reserva se levanta, una sola vez p… |
| `EscaleraTrepable.cs` | 39 | EscaleraTrepable | Bug #141 ("bug de escalera"): la escalera de la torreta de artilleria era solo decorado (aparecia el icono de trepar y n… |

## Otros — 1 scripts, 17 lineas

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `Readme.cs` | 17 | Readme, Section | — |

## Player — 28 scripts, 10,351 lineas

> Traduccion de intencion a ordenes: input, posesion, seleccion, ordenes.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `PlayerInputDriver.cs` | 3246 | PlayerInputDriver | Traduce teclado/ratón reales a los mismos métodos que usa el test automático. No decide nada nuevo: es el "pegamento" de… |
| `PlayerInputDriver.Rts.cs` | 916 | PlayerInputDriver | PlayerInputDriver (parte): vista RTS -- UpdateRts, vista previa de formacion, grupos de control y seleccion por arrastre… |
| `OrderService.cs` | 810 | FormationKind, OrderService | Disposiciones posibles de un lote de destinos. Vive AFUERA de OrderService (que es una clase estatica) para que el drive… |
| `PlayerInputDriver.Radial.cs` | 775 | PlayerInputDriver | PlayerInputDriver (parte): menu radial de [Q] y ejecucion de las ordenes de escuadra. |
| `PlayerInputDriver.Vehiculo.cs` | 698 | PlayerInputDriver | PlayerInputDriver (parte): manejo de vehiculos (entrar, asientos, camara, ordenes con [T]). |
| `PedidoDeCuracion.cs` | 565 | PedidoDeCuracion | "Necesito curarme" del menu de ordenes ([Q] sostenido). De las cinco ordenes del menu, cuatro ya existian en OrderServic… |
| `Demolicion.cs` | 417 | Demolicion, DemoledorAsalto | DEMOLER (habilidad del soldado de ASALTO): plantar una carga en un muro o cobertura. Hay que estar AGACHADO y QUIETO fre… |
| `AimTargeting.cs` | 290 | AimTargetType, AimResult, AimTargeting | B4: raiz del objeto golpeado, para el anillo generico de apuntado (SelectionRingFx necesita un Transform a quien seguir,… |
| `KeyBindings.cs` | 285 | KeyBindings | Item 208: remapeo de teclas. Las ~35 lecturas de teclado de PlayerInputDriver estaban hardcodeadas (kb.rKey, kb.eKey, ..… |
| `CambioDeAliado.cs` | 266 | CambioDeAliado | P7 (#120c): cambio de aliado con [C], en FPS. pantalla, no objetos del mundo); el candidato es el mas centrado en pantal… |
| `SelectionController.cs` | 238 | SelectionController | Selección múltiple en vista RTS. |
| `PlayerInputDriver.Contextual.cs` | 204 | PlayerInputDriver | PlayerInputDriver (parte): la orden CONTEXTUAL de un toque de [Q] y el envio de aliados a interactuar. Pedido explicito:… |
| `CajaDeSuministros.cs` | 172 | CajaDeSuministros | Caja de suministros: antes las granadas (3) y la vida no se reponian NUNCA durante la partida. Caminar hasta la caja con… |
| `PlayerInputDriver.Interaccion.cs` | 162 | PlayerInputDriver | PlayerInputDriver (parte): detector central de IInteractable. Archivo NUEVO y aditivo: no reemplaza nada de PlayerInputD… |
| `PlayerBrain.cs` | 161 | PlayerBrain | La consciencia que salta de cuerpo en cuerpo. Traduce intención en llamadas al soldado que ocupa. Es único en la escena. |
| `Rehen.cs` | 154 | Rehen, GiroDeMarcador | Hace que el civil de la mision de rescate (RoleType.Civilian, creado por MisionDirector.AparecerCivil) se distinga a sim… |
| `MunicionPickup.cs` | 147 | MunicionPickup | Municion suelta al morir un enemigo (item nuevo). 100% por codigo, sin prefab ni asset externo -- mismo espiritu que Caj… |
| `PlayerInputDriver.Escuadra.cs` | 125 | PlayerInputDriver | PlayerInputDriver (parte): orden de la escuadra. El lugar en la lista fija a que numero de roster y a que [F1..F9] respo… |
| `RescateAutomatico.cs` | 123 | RescateAutomatico | A5: "un aliado libre va a revivirte y frena el timer". Mismo patron que PedidoDeCuracion (curarse por pedido del jugador… |
| `OrdenesDeEscuadra.cs` | 109 | OrdenesDeEscuadra, PuntoDeCobertura | Ordenes nuevas del radial que no vivian en OrderService: cubrirse segun hacia donde se mira y "todos quietos". Estaticas… |
| `TrazadoDeCamino.cs` | 109 | TrazadoDeCamino | Trazar un recorrido con [Ctrl] y arrancarlo con [Espacio]. La diferencia con Shift+click (que ya encolaba ordenes) es cu… |
| `OrderHistory.cs` | 96 | OrderHistory, Entry | Item 221: historial de ordenes. El jugador daba una orden a un lote y a los pocos segundos ya no tenia forma de saber qu… |
| `AccionesEnCurso.cs` | 92 | AccionesEnCurso, Accion | Ronda 13 (puntos 2 y 3): registro de "que esta haciendo ahora cada soldado" cuando la accion tarda (reanimar, curar, det… |
| `Reanimacion.cs` | 53 | Reanimacion | Ronda 13 (puntos 1 y 8): UNICO camino para revivir a un soldado caido. Lo usan la tecla [E], la habilidad del medico ([C… |
| `PlayerInputDriver.GestoMoverAliado.cs` | 52 | PlayerInputDriver | PlayerInputDriver (parte): bug #205 ("si mantengo sobre un aliado y mantengo apretada la barra espaciadora y muevo el cu… |
| `MandoFps.cs` | 39 | MandoFps | Lectura del gamepad para la vista en primera persona. El juego lee el teclado directo (no usa InputActions), asi que el … |
| `PossessionService.cs` | 28 | PossessionService | Ejecuta la transferencia de control y la anuncia. El cuerpo abandonado no se destruye ni se congela: su AiBrain se react… |
| `PlayerInputDriver.Registro.cs` | 19 | PlayerInputDriver | Servicio unico de la escena: se registra al activarse en vez de que cada consumidor lo busque con un barrido. El suite (… |

## Presentation — 113 scripts, 22,929 lineas

> Todo lo que se ve y se oye en el mundo: VFX, audio, marcadores, pools visuales.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `MinimapIcon.cs` | 971 | MinimapIcon | Circulo chato que representa a un soldado/vehiculo en el minimapa. Vive en su propia capa (Minimap), que la cámara princ… |
| `ImpactFx.cs` | 789 | ImpactFx, FxMode, ImpactFxPool | Mini-explosion al impactar: una esfera que se agranda rapido y despues se achica hasta desaparecer, en el punto exacto d… |
| `OrderMarkerFx.cs` | 780 | OrderMarkerFx | Pedido explicito: "en el caso de la orden no quiero un cilindro en destino, quiero un sistema de particulas". Estallido … |
| `AudioDirector.cs` | 729 | SfxChannel, PerfilEspacial, VoiceState | Canales de mezcla (item 186). Van a nivel de namespace y no anidados en AudioDirector por la misma razon que SfxKind viv… |
| `GenericSfx.cs` | 710 | SfxKind, GenericSfx | Los miembros nuevos van SIEMPRE al final: el valor entero de cada uno es lo que quedaria guardado si alguna vez se seria… |
| `PauseController.cs` | 621 | PauseController | Pausa con [ESC]: congela el tiempo (Time.timeScale=0) y muestra el panel de pausa, con un sub-panel de "Configuraciones"… |
| `AnimacionDeAccion.cs` | 494 | TipoAccion, AnimacionDeAccion, AnimacionDeAccionSoldado | Bug #067: animacion para TODA accion de un soldado (curar, revivir, plantar carga, operar un panel, hackear, reparar, re… |
| `UnitLocatorCylinder.cs` | 448 | UnitLocatorCylinder | Pedido explicito: reemplazar los cilindros localizadores por un gizmo de ROMBO (dos capas: un rombo blanco de fondo, mas… |
| `SfxSintetico.cs` | 436 | SfxSintetico | Sintesis por codigo de los sonidos de combate que no tienen grabacion real (ronda 7): explosion, cohete, granada, cuchil… |
| `SoldierAnimatorDriver.cs` | 413 | SoldierAnimatorDriver | Traduce lo que el soldado YA hace a parametros del Animator. No decide nada: no mueve, no dispara, no cambia de estado. … |
| `WorldUiDirector.cs` | 397 | WorldUiDirector | Un unico recorrido para TODA la UI de mundo (barras de vida, marcador de poseido, iconos de minimapa) mas nivel de detal… |
| `MainMenuController.cs` | 378 | MainMenuController | Menú de inicio: [Jugar] carga la escena de gameplay, [Salir] cierra el juego (o sale de Play mode si esto corre en el Ed… |
| `SfxSintetico.Victoria2.cs` | 368 | SfxSintetico | #129: himno de victoria v2, sintetizado por codigo (nada descargado). ESTEREO, 44,1 kHz, ~38 s en Re mayor a 100 bpm, 15… |
| `MusicDirector.cs` | 332 | MusicDirector, MusicaVictoriaRunner | G4: dos temas que se cruzan solos segun si hay combate cerca de la camara. Este prototipo no tiene pistas de musica impo… |
| `CubeFxReactor.cs` | 325 | CubeFxReactor | Único puente entre el bus de eventos y lo que se ve/oye de un soldado. No decide nada de gameplay: solo reacciona. Se au… |
| `DestruccionDeVehiculo.cs` | 322 | DestruccionDeVehiculo, ChapaVolante, CraterPool | Bug #072 (parte visual): explosiones con CRATER en el piso (que antes quedaba escondido bajo el asfalto), chapas que sal… |
| `Fragmentador.cs` | 321 | Fragmento, Fragmentador | Ronda 12: "quiero fisicas... que las cosas se destruyan de a piezas reales, que se fragmente". Antes un obstaculo que co… |
| `GameOutcomeController.cs` | 309 | GameOutcomeController | Pantallas de victoria y derrota: UI distinta para cada una (colores, texto) pero los mismos dos botones -- Reintentar (r… |
| `DiamondGizmo.cs` | 303 | DiamondGizmo | Utilidad compartida para armar el gizmo de rombo que reemplaza a los cilindros localizadores. Pedido explicito: "esos ro… |
| `AccionesEnCursoView.cs` | 296 | AccionesEnCursoView, BarraMundo | Ronda 13 (puntos 2 y 3). Dibuja lo que reporta AccionesEnCurso: * una BARRA DE CARGA en el mundo, en el punto medio entr… |
| `ObstacleMarker.cs` | 289 | ObstacleMarker | Marca un cubo como "obstáculo" para que un proyectil lo detecte y el jugador tenga feedback distinto al pegarle a un obs… |
| `LoadingScreenController.cs` | 285 | DatosDelMapa, MapaDeCarga, LoadingScreenController | P11 (#130): lo que MapasDeCarga (Editor) deja en Mapa_<escena>.json: el recorrido de la escena normalizado (0..1, arriba… |
| `DebrisPool.cs` | 271 | DebrisPool, Debris | Escombros con presupuesto FIJO. Instanciar por evento haria que un combate masivo generase basura sin control y el recol… |
| `EntityStateDebugView.cs` | 270 | EntityStateDebugView | Pedido explicito: una esfera chica flotando arriba de cada soldado (aliado/enemigo), del vehiculo y de cada obstaculo, q… |
| `WeaponBackRack.cs` | 270 | WeaponBackRack | Pedido explicito: "quiero poder ver el soldado q manejo y sus armas en su espalda". Cuelga un cubo chico por cada arma d… |
| `EngranajesDeAccion.cs` | 269 | EngranajesDeAccion, Engranaje, Muestra | Bug #066: "engranaje girando sobre el operador y sobre el interactuable". Por cada accion que reporta AccionesEnCurso (c… |
| `KillCylinderFx.cs` | 248 | KillCylinderFx | Marca el lugar donde cae un enemigo con un cilindro delgado que sube muy por encima del mapa y se desvanece solo. Mismo … |
| `VehicleFxReactor.cs` | 244 | VehicleFxReactor, VehicleSmokePuff | Reaccion visual/sonora propia del vehiculo al recibir daño. Antes un impacto solo bajaba una barra de vida en el HUD -- … |
| `GameplaySceneBootstrap.cs` | 243 | GameplaySceneBootstrap | Marca en el log de flujo que la escena de gameplay terminó de cargar (Start corre después de que todo el resto ya se con… |
| `UnitLabelView.cs` | 239 | UnitLabelView | C1: etiqueta al pie de cada unidad con su vida, su tipo (aliado / enemigo / vehiculo / interactuable) y, si es montable,… |
| `TransparenciaDeOclusores.cs` | 234 | TransparenciaDeOclusores, Estado | Bug #219 ("me obstaculiza algo: aplica transparencia a lo que esta entre la camara y el seleccionado"): paredes, techos … |
| `SfxSintetico.Victoria.cs` | 231 | SfxSintetico | WP2 (#093): himno de victoria y salvataje, sintetizado por codigo (nada descargado). 26,5 s en Re mayor a 92 bpm, progre… |
| `PantallaDeResultado.cs` | 224 | PantallaDeResultado | Bug #064: "agrega una animacion, que no sea una pantalla de golpe, que tenga su fade o efecto". La pantalla de victoria/… |
| `CurandoAnimacion.cs` | 219 | CurandoAnimacion, Estado, AvisoCentral | Animacion de CURAR / REANIMAR (pedido del bug #8: "quiero que tenga una animacion de curarme"). Los soldados son de bloq… |
| `PostFxDirector.cs` | 215 | PostFxDirector | Items 176 (aberracion cromatica por daño) y 178 (desenfoque de movimiento en el vehiculo). Los dos necesitan post-proces… |
| `CoverHologram.cs` | 214 | CoverHologram | Holograma de cobertura. Mientras el jugador mantiene [Shift] apuntando a una cobertura se muestra, ESTATICO y 90 % trans… |
| `SfxSintetico.Operacion.cs` | 206 | SfxSintetico | Sintesis por codigo de los sonidos de la Operacion Cuartel (todo CC0 por construccion: se genera con matematica, no se d… |
| `HealthBarView.cs` | 202 | HealthBarView | Barra de vida flotante sobre un soldado. Sube y baja con la vida y siempre mira a la cámara activa (billboard) — sirve i… |
| `IndicadorDeCaidos.cs` | 201 | IndicadorDeCaidos, Marca | Bug #10: "falta un indicador para saber que necesita reanimacion y ubicacion; en el minimapa debe parpadear". Por cada a… |
| `DecalPool.cs` | 199 | DecalKind, DecalPool | Marcas persistentes en el terreno (crateres de explosion y agujeros de bala). Mismo problema que los escombros: sin un c… |
| `SpriteFx.cs` | 198 | SpritesReales, SpriteFx | Ronda 12: los impactos de granada/canon eran una esfera de color y las marcas en el piso, quads grises lisos (placeholde… |
| `KillFeedbackDirector.cs` | 196 | KillFeedbackDirector | Un solo lugar que decide como se comunica una baja. Estaba repartido (el kill feed apilaba lineas por su cuenta, la miri… |
| `RoleIconFactory.cs` | 196 | RoleIconFactory | Iconos chicos para el roster (abajo a la izquierda). Pedido explicito: "mas simplificado con iconos simples que descargu… |
| `DianaDeObjetivo.cs` | 191 | DianaDeObjetivo | Bug #096: "una diana sobre el enemigo al que mandas atacar". Marca de blanco sobre la cabeza del enemigo elegido: dos an… |
| `SonidosDeOperacion.cs` | 189 | SonidosDeOperacion | Sonidos "vivos" del nivel, todos generados por codigo (ver SfxSintetico.Operacion): como cada uno es un AudioSource 3D c… |
| `SoldierMeleeAnim.cs` | 187 | SoldierMeleeAnim | Animacion del golpe de cuchillo ([F]), procedural y por CODIGO sobre el esqueleto humanoide. Pedido explicito: "la anima… |
| `RachaDeBajas.cs` | 183 | RachaDeBajas | Bug #079: "carteles DOBLE/TRIPLE/PENTA KILL; con 5 seguidas, mas velocidad y doble dano". 3 = TRIPLE KILL, 4 = CUADRUPLE… |
| `AttackLineManager.cs` | 181 | AttackLineManager | Línea roja entre un soldado y el enemigo al que le está disparando mientras está en estado Attack. Revisa a todo el mund… |
| `EnemyAlertIndicatorView.cs` | 180 | EnemyAlertIndicatorView | Marca sobre la cabeza del enemigo que dice si ya te detecto o no. Sin esto no habia forma de saber, antes de que empiece… |
| `ArmaEnLaMano.cs` | 175 | ArmaEnLaMano | Del plan del usuario: "Los soldados no tienen armas. Deberian tener armas". Armas tenian: WeaponVisual existe en todos d… |
| `HudIconFactory.cs` | 175 | HudIconFactory | Iconos chicos para el HUD de abajo a la derecha (cuchillo/granada/ recarga). Pedido explicito: "q use iconos... intenta … |
| `SparkleBurstFx.cs` | 175 | SparkleBurstFx | Estallido generico de particulas de un solo uso, pooleado con tope duro (mismo criterio que OrderMarkerFx/DebrisPool/Imp… |
| `VehicleMountIndicator.cs` | 175 | VehicleMountIndicator | Cuando le apuntás a un vehículo, muestra una flecha (cilindro + cono, apuntando de arriba hacia abajo) sobre el vehículo… |
| `FlechaDeOleada.cs` | 174 | FlechaDeOleada | WP10 (#101): la flecha roja que anuncia por donde entra una oleada. Una flecha grande (12 m) en el piso, apoyada sobre l… |
| `MiraOptica.cs` | 172 | MiraOptica | La optica del arma en primera persona. El zoom que ya existia angosta el FOV de la camara principal (60 -> 25): eso acer… |
| `ParticleMaterialFactory.cs` | 172 | ParticleMaterialFactory | Fuente unica de materiales para ParticleSystem con blend alfa real y colorOverLifetime funcionando de verdad. BUG REAL e… |
| `Feedback.cs` | 171 | Feedback, WorldTag | Punto unico de feedback de las ACCIONES del juego. Pedido de pulido: un solo canal por accion. Feedback.Accion = sonido … |
| `FloatingDamageTextManager.cs` | 171 | FloatingDamageTextManager, Entry | Numero de daño que sube y se desvanece sobre el objetivo golpeado. Antes no habia ninguna confirmacion visual de cuanto … |
| `MinimapAttackerRings.cs` | 162 | MinimapAttackerRings, RingState | Usa un material simple para que se vea el color |
| `OrderLineManager.cs` | 160 | OrderLineManager, Linea | Linea del soldado a su destino mientras dure una orden de movimiento simple en RTS. El marcador (OrderMarkerFx) ya dice … |
| `BarraDeVidaVehiculo.cs` | 157 | BarraDeVidaVehiculo | Ronda 13 (punto 9): los tanques tambien tienen barra de vida. HealthBarView es solo de soldados (se ata con GetComponent… |
| `InteractGearMarker.cs` | 154 | InteractGearMarker | Pedido explicito: "que no aparezca texto sino arriba un rombo pero con imagen de un engranaje para saber que es interact… |
| `SquadStateIndicatorView.cs` | 152 | SquadStateIndicatorView | AiStateChangedEvent se publicaba en cada cambio pero nadie lo consumia visualmente del lado propio: el jugador no sabia … |
| `SoldierGrenadeAnim.cs` | 149 | SoldierGrenadeAnim | Animacion del lanzamiento de granada ([G]), procedural y por CODIGO sobre el esqueleto humanoide, en el mismo estilo que… |
| `RagdollDeExplosion.cs` | 146 | RagdollDeExplosion, Hueso | Ragdoll fisico (item 55) para los soldados que MUEREN por una explosion: en vez de la animacion de muerte, el esqueleto … |
| `TrayectoriaGranadaView.cs` | 145 | TrayectoriaGranadaView | Vista previa de la granada mientras se MANTIENE [G]: la curva exacta (la misma simulacion que usa la granada de verdad, … |
| `OndasDeRotor.cs` | 144 | OndasDeRotor, Onda | Bug #075: ondas de viento circulares que se expanden por el piso bajo el helicoptero. Pool propio de 10 quads planos con… |
| `SafeMaterial.cs` | 143 | SafeMaterial | Fuente unica y a prueba de fallos para materiales solidos de FX (anillos, marcadores, impactos, debris, indicadores...).… |
| `SelectionRingFx.cs` | 142 | SelectionRingFx | Anillo (cilindro chato) que sigue a un soldado seleccionado y pulsa a simple vista quién está elegido en la vista RTS. |
| `RevivePromptView.cs` | 141 | RevivePromptView | Cartel mundial "[Q] REVIVIR" sobre un aliado caido. Mismo patron que UnitLabelView (Canvas WorldSpace propio, billboard … |
| `MenuAmbiente.cs` | 135 | MenuAmbiente | Ambiente del menu principal (item 21): musica de fondo y un fondo tactico animado (mapa con cuadricula que se desplaza y… |
| `CuchilloFx.cs` | 132 | CuchilloFx, TajoVisual | Presentacion del golpe de cuchillo ([F], ronda 7): el cuchillo aparece en la mano, barre un arco con estela, suena el ta… |
| `LimpiezaDeEscena.cs` | 126 | LimpiezaDeEscena | Del plan del usuario: "Al re cargar la escena no se limpian los decals". Es cierto y no era solo de los decals. Los tres… |
| `PossessedMarkerView.cs` | 124 | PossessedMarkerView | En vista RTS el roster marcaba al poseido pero el mundo no: con la tropa dispersa habia que cruzar el nombre del roster … |
| `Luminaria.cs` | 121 | Luminaria | #104: duracion del parpadeo de la lampara agonizando (luz cosmetica, la funcional se apaga en el acto). |
| `MuzzleLightPool.cs` | 120 | MuzzleLightPool, MuzzleFlashLight | Un destello plano pegado a la boca no ilumina nada. Una Light real encendida uno o dos frames cambia por completo la per… |
| `PrevisualizacionDeCoberturas.cs` | 117 | PrevisualizacionDeCoberturas | Bug #189 ("al apuntar X soldados a coberturas, que se iluminen esas coberturas segun la cantidad de soldados y cercania,… |
| `SiluetasDeMinimapa.cs` | 111 | SiluetaMinimapa, SiluetasDeMinimapa | Marcador opcional: un builder puede agregarlo a cualquier objeto con BoxCollider para que su silueta aparezca en el mini… |
| `PatrolRouteLine.cs` | 110 | PatrolRouteLine | Marca el circuito de patrulla de un enemigo con una esfera por punto, en vez de un LineRenderer. Pedido explicito: la lí… |
| `ShapeMarkerFx.cs` | 106 | ShapeMarkerFx | Bug #068: anillo de DESTINO. Anillo con barrido angular (estela que se apaga alrededor del circulo, una punta brillante … |
| `InteractHintView.cs` | 103 | InteractHintView | Pedido explicito: "cuando me acerco a un interactuable quiero abajo un texto que diga: Apreté [E] para entrar". El rombo… |
| `ObjectiveDiamondMarker.cs` | 103 | ObjectiveDiamondMarker | Pedido explicito: el mismo gizmo de rombo (fondo blanco + interior de color) que usan enemigos/aliados, pero AMARILLO y … |
| `SelectionRingManager.cs` | 103 | SelectionRingManager | Escucha SelectionChangedEvent y mantiene un anillo pulsante por cada soldado seleccionado, creándolos y destruyéndolos s… |
| `FuentesBelicas.cs` | 101 | FuentesBelicas | Ronda 12: dos fuentes belicas (OFL, Resources/UI/Fuentes) para TODOS los textos: menu inicial, tutorial y partida. Los ~… |
| `ObjectiveArrowIndicator.cs` | 100 | ObjectiveArrowIndicator | Pedido explicito: "en la base del soldado una flecha que indique hacia donde ir" -- un decal plano en el suelo, pegado a… |
| `AnuncioDeZonas.cs` | 97 | AnuncioDeZonas, Zona | Avisa en que bloque del nivel esta el jugador (el nivel mide 320 m de largo: sin esto no hay sensacion de avance). Al en… |
| `AyudaDeCaido.cs` | 97 | AyudaDeCaido | Bug #253 ("quiero ver sus cadaveres y con animacion de pidiendo ayuda"): un aliado caido (todavia reviviable) se queda e… |
| `AudioDucking.cs` | 92 | AudioDucking | Sordera momentanea tras una explosion muy cercana (item 181). Este proyecto no tiene AudioMixer (todos los clips se gene… |
| `SfxSintetico.Graves.cs` | 85 | SfxSintetico | WP2 (#072): graves de explosion/cañon y chapa de la destruccion de un vehiculo. Todo sintetizado por codigo (determinist… |
| `Subtitulos.cs` | 80 | Subtitulos | Subtitulos de sonido (accesibilidad, item 28): con la opcion activa, los sonidos que importan en combate (disparos, expl… |
| `TomaDeVictoria.cs` | 79 | TomaDeVictoria | Bug #220 ("mejora la toma de camara"): al ganar, la camara quedaba pegada al hombro del soldado que manejabas. Ahora hac… |
| `PisoInteractableHighlight.cs` | 77 | PisoInteractableHighlight | Pedido explicito: "los interactuables en el piso resalten con un cilindro para saber que es interactuable, solamente al … |
| `SfxSintetico.Francotirador.cs` | 76 | SfxSintetico, SniperFx | Bug #241 ("el disparo del francotirador deberia tener un efecto visual y sonoro especial y la recarga un efecto sonoro f… |
| `RutaAlObjetivo.cs` | 73 | RutaAlObjetivo | Pedido explicito: "quiero q al mantener C se muestre un destino, una linea del camino hacia el objetivo". A diferencia d… |
| `CuchilladaLenta.cs` | 67 | CuchilladaLenta | Bug #274: "al dar una cuchillada el tiempo se relentiza y se escucha un desgarro, un buen efecto sonoro; mata de una cuc… |
| `ImpactCubes.cs` | 61 | ImpactSurface, ImpactCubes | Impacto de un proyectil, en cubitos (pedido: "si es soldado cubitos de distintos tamaños rojos, si es piso verde y si es… |
| `InteractableDiamond.cs` | 61 | InteractableDiamond | — |
| `FeedbackDeRevivir.cs` | 58 | FeedbackDeRevivir | Bug #089: "quiero un sonido gratificante y anillos en el piso al revivir". Reanimacion.Revivido es el camino UNICO de fi… |
| `LuzTitilante.cs` | 58 | LuzTitilante | Luz de fuego / farol viejo: la intensidad oscila con ruido suave (nunca se apaga del todo) y, si tiene `Llama`, esa mall… |
| `CursorContextual.cs` | 57 | CursorTipo, CursorContextual | Ronda 11 (punto 16): en RTS, con soldados seleccionados, el cursor cambia al apuntar algo con lo que se puede interactua… |
| `SombrasDeLuces.cs` | 53 | SombrasDeLuces | Bug #138 ("la luz no es real, no deberia pasar obstaculos"): las luces puntuales (farolas, luces de los edificios) proye… |
| `LightProp.cs` | 50 | LightProp | Objeto liviano (un bidon, una caja) que se vuelca al ser atropellado por el vehiculo. El tanque atravesaba el escenario … |
| `BalizaPulsante.cs` | 49 | BalizaPulsante | Baliza que late: la luz (y la malla emisiva, si tiene) suben y bajan de forma pareja. Sirve para el refugio del rehen (c… |
| `BattleManager.cs` | 49 | BattleManager | Condición de victoria: cuando todos los enemigos de la lista mueren y todavía queda algún soldado propio vivo, muestra l… |
| `BrazoIk.cs` | 48 | BrazoIk | IK analitico de dos huesos para el brazo derecho, en el espacio del soldado (derecha, arriba, adelante). No depende de l… |
| `LucesDelHelipuerto.cs` | 42 | LucesDelHelipuerto | Bug #063: "quiero puntitos de luminarias chiquitas donde aterriza el helicoptero". Balizas chiquitas alrededor del helip… |
| `OcultadorDeArma.cs` | 41 | OcultadorDeArma | Apaga los renderers del arma que el soldado lleva en la mano durante una animacion procedural (golpe de cuchillo, lanzam… |
| `FocoBarredor.cs` | 38 | FocoBarredor | Reflector de una torre: el haz barre de un lado a otro (yaw oscilante alrededor de `yawCentro`, con el cabeceo fijo haci… |
| `SafeMaterialTint.cs` | 38 | SafeMaterialTint | Reaplica un SafeMaterial.Create(color) a los Renderer hijos cada vez que el objeto se habilita. Existe para contenido de… |
| `SfxSintetico.Faro.cs` | 37 | SfxSintetico | #104: sonido del faro (Luminaria) al romperse: estallido de vidrio (crack seco + cascada de tintineos agudos) y el chisp… |
| `PrecalentadoDeCombate.cs` | 34 | PrecalentadoDeCombate | Bug #052/#053/#062 ("se me trabo todo", "se laguea muchisimo"): el profiler mostro que los tirones de 80-150 ms salian d… |
| `MarcadorDeMision.cs` | 27 | MarcadorDeMision | Marcador de referencia para puntos clave de la mision (plaza, helipuerto, refugio del civil): sin Renderer ni Collider, … |
| `RomboVisibilidad.cs` | 22 | RomboVisibilidad | Pedido explicito: "en la cinematica se desactivan todos los rombos, todos". Un interruptor global unico en vez de que ca… |

## Simulacion — 1 scripts, 121 lineas

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `SimulacionBatallaBootstrap.cs` | 121 | SimulacionBatallaBootstrap | SC_Simulacion: escena aparte (no SC_Gameplay) para probar rendimiento y estabilidad con muchos soldados en pantalla a la… |

## Tutorial — 13 scripts, 4,578 lineas

> Modulo de ensenanza y su reproductor automatico.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `TutorialManager.cs` | 1905 | TutorialManager, Sub, Paso | Modulo de tutorial. Recorre 35 pasos en orden; cada paso tiene sub-pasos y cada sub-paso una BANDERA booleana (ver Tutor… |
| `AutoplayRegistro.cs` | 460 | LineaLog, MuestraAutoplay, CapturaAutoplay | Registro mecanico del reproductor automatico: todo lo que pasa en una corrida queda en disco, en formato pensado para qu… |
| `TutorialAutoPlayer.cs` | 411 | TutorialAutoPlayer | Reproductor automatico y cronometrado del tutorial (pedido: "quiero un script con timers para el tutorial que de manera … |
| `TutorialUI.cs` | 363 | TutorialUI | Cuadro de dialogo del tutorial (columna derecha, debajo del minimapa): TUTORIAL · PASO 3 / 17 DISPARAR                  … |
| `TutorialAutoPlayer.Radial.cs` | 297 | TutorialAutoPlayer | Gestos de las ordenes radiales, la seleccion en FPS, el tanque y la torreta fija. La mira es real (se gira al soldado y … |
| `KeyCapView.cs` | 250 | TutorialTextures, Estado, KeyCapView | Texturas de teclas dibujadas por codigo (no hay assets: el tutorial se arma solo). Cada tecla es un RawImage con una de … |
| `VictoriaTutorial.cs` | 213 | VictoriaTutorial, Cubito, Confeti | Efecto de victoria y cierre del tutorial (paso 17): REPETIR TUTORIAL y VOLVER AL MENU |
| `TutorialAutoPlayer.Entrada.cs` | 200 | TutorialAutoPlayer | Gestos con teclado y mouse virtuales (ver EntradaVirtual): el juego los lee como si los hiciera una persona. |
| `TutorialBeacon.cs` | 167 | TutorialBeacon | Baliza del mundo para "mira aca": un anillo en el piso que pulsa y una etiqueta que siempre mira a la camara. Puede segu… |
| `EntradaVirtual.cs` | 130 | EntradaVirtual | Teclado y mouse VIRTUALES para el reproductor automatico del tutorial (item 95): el reproductor no llama a las APIs del … |
| `TutorialFlags.cs` | 85 | TutorialFlags | Todos los booleanos del tutorial, a la vista en el Inspector (componente TutorialManager > Banderas). El cuadro de dialo… |
| `TutorialLog.cs` | 57 | TutorialLog | Log claro del tutorial: una linea por INICIO de paso, por SUB-PASO cumplido, por PISTA mostrada y por PASO completado. S… |
| `CatalogoDelTutorial.cs` | 40 | CatalogoDelTutorial | Los objetos del tutorial que viven bakeados en SC_Tutorial, con referencias serializadas. Antes el tutorial y su corrida… |

## UI — 61 scripts, 12,112 lineas

> HUD y pantallas: mira, roster, minimapa, radial, menus, ajustes.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `MenuDeOrdenes.cs` | 923 | ContextoRadial, MenuDeOrdenes | Contexto del radial: que categorias y opciones tienen sentido AHORA segun lo que se apunta. Las contextuales (curar al h… |
| `AimUI.cs` | 757 | AimUI | Retículo + prompt contextual: "F: Poseer a X" o "T: Ir aquí". También resalta el retículo cuando el disparo del jugador … |
| `MinimapFollow.cs` | 479 | MinimapFollow | Cámara cenital del minimapa: sigue al objetivo actual (soldado o vehículo poseído) desde arriba, mirando siempre hacia a… |
| `WeaponStatusView.cs` | 446 | WeaponStatusView | HUD fijo (no depende de apuntar a nada) con qué arma tenés, cuánta munición te queda y una barra de recarga/enfriamiento… |
| `RadialIconFactory.cs` | 428 | RadialIconFactory | Pedido explicito: "para el radial, para todas las opciones, quiero texto chico e iconos grandes, descriptivos y claros".… |
| `RosterRowView.cs` | 423 | RosterRowView | Una fila del roster = un prefab por soldado. Pedido explicito: "un prefab para cada caso... con puntero al soldado... si… |
| `EstiloDeAjustes.cs` | 396 | EstiloDeAjustes, SliderDeAjuste, InterruptorDeAjuste | Aspecto y respuesta de la pantalla de Configuraciones. Pedido explicito: "la UI de scrolls corrigela, que sea mejor; tod… |
| `MirillaView.cs` | 344 | MirillaView | La mirilla que se ve al mantener el clic derecho (apuntar con zoom). Antes el "zoom" agrandaba x4 el anillo de la mira (… |
| `TurretAimView.cs` | 338 | TurretAimView, TipoDeImpacto, Impacto | HUD propio del artillero. Como artillero se usaba la misma mirilla de infanteria, que no comunica lo unico que importa e… |
| `LayoutDeAjustes.cs` | 312 | LayoutDeAjustes, Resultado, Fila | Acomoda toda la pantalla de Configuraciones en una sola tarjeta de dos columnas, en tiempo de ejecucion (las posiciones … |
| `VehicleStatusView.cs` | 296 | VehicleStatusView | HUD que reemplaza al de arma en cuanto se sube al vehículo: velocímetro, barra de vida del vehículo y quién está de arti… |
| `ControlsTable.cs` | 294 | ControlContext, ControlEntry, ControlsTable | Los seis contextos de entrada reales. Son flags porque un mismo atajo puede leerse en varios (por ejemplo [TAB], que se … |
| `SelectedSoldierUI.cs` | 270 | SelectedSoldierUI, Row | Roster de la escuadra: resalta al soldado poseído y a los seleccionados en vista RTS. Solo escucha el bus, nunca decide … |
| `MarcaDeImpacto.cs` | 265 | MarcaDeImpacto | Bug #059: "quiero que cuando impacte el proyectil, alrededor tenga rayitas, como indicando que el impacto llego al enemi… |
| `MiraDeTorreta.cs` | 258 | MiraDeTorreta | La mira del ARTILLERO del tanque / metralleta (pedido: "se vea la mira, la torreta no se ve bien donde apunta; el radial… |
| `AlertQueue.cs` | 257 | AlertPriority, PendingAlert, AlertQueue | Prioridad de un aviso. El orden de los valores ES la regla de desempate, asi que no se reordena ni se le meten valores n… |
| `PanelDeMando.cs` | 246 | PanelDeMando, Paso | WP10 (#101): el panel de INSTRUCCIONES del mando tactico (arriba a la derecha, bajo el minimapa). Una lista corta de pas… |
| `TablaDeControles.cs` | 246 | TablaDeControles | Tabla de CONTROLES de la pausa (bug #100): reemplaza el bloque de texto unico de ~110 lineas (que se cortaba y se pisaba… |
| `AvisoUnico.cs` | 244 | AvisoUnico, Aviso, Fijado | EL UNICO lugar de la pantalla donde aparecen los textos sorpresivos (abajo al centro). Antes cada vista ponia su cartel … |
| `DamageVignetteView.cs` | 218 | DamageVignetteView | Viñeta negra granulada que se degrada hacia el centro (oscura en los bordes, transparente en el medio) y destella cada v… |
| `LowHealthPulseView.cs` | 206 | LowHealthPulseView | Pulso rojo en los bordes de la pantalla + latido sonoro cuando al soldado poseido le queda poca vida. Hasta ahora la uni… |
| `ScreenFlashView.cs` | 202 | ScreenFlashView | Destello a pantalla completa, reutilizable para dos cosas distintas que necesitaban lo mismo (items 181 y 184 del backlo… |
| `PanelAjustesExtra.cs` | 198 | PanelAjustesExtra, SelectorSfx | Arma y viste la pantalla de Configuraciones. Los sliders y toggles ya estan en la escena (PauseController les conecta el… |
| `FondoOpaco.cs` | 188 | FondoOpaco | Del plan del usuario, tres renglones que piden lo mismo: "El cartel de F poseer a soldados fondo opaco para saber q es i… |
| `OffscreenAllyMarkerView.cs` | 188 | OffscreenAllyMarkerView | Item 63: marcas en el borde apuntando a los aliados que quedaron fuera de encuadre. En primera persona el jugador perdia… |
| `VehicleKeysPanel.cs` | 185 | VehicleKeysPanel | Panel de TECLAS dentro del vehiculo (pedido: "cuando estes en el tanque aparezcan las teclas para decir que salgan todos… |
| `Diagramador.cs` | 171 | Diagramador | Del plan del usuario, dos renglones distintos que resultaron ser el mismo defecto: "EN la pantalla de ganar y perder. Lo… |
| `DamageDirectionView.cs` | 166 | DamageDirectionView | Marca en el borde de pantalla que apunta hacia de donde vino el ultimo golpe. Antes, recibir un disparo desde fuera de c… |
| `KeyRebindView.cs` | 166 | KeyRebindView | La mitad visible del item 208. KeyBindings guarda y resuelve los mapeos, pero sin esto el jugador no tiene forma de camb… |
| `CartelVolverView.cs` | 154 | CartelVolverView | Triangle shape roughly: |
| `AjustesDeJuego.cs` | 134 | AjustesDeJuego | Opciones que no tenian donde vivir: pantalla completa, resolucion, calidad, paleta para daltonismo y HUD minimo. Se guar… |
| `CartelDeRacha.cs` | 133 | CartelDeRacha | Bug #079: carteles de bajas seguidas tipo LoL (DOBLE / TRIPLE / CUADRUPLE / PENTA KILL) y el aviso fijo de la FURIA. Pan… |
| `FlechaDeObjetivo.cs` | 126 | FlechaDeObjetivo | Bug #048: "quiero una flechita igual que las de los aliados pero para el helicoptero, que me diga donde esta y me vaya d… |
| `PerfHudView.cs` | 123 | PerfHudView | Item 235: panel de diagnostico en vivo. El backlog lo ponia ULTIMO, con dependencias en 229 y 234. La dependencia esta a… |
| `KillFeedView.cs` | 117 | KillFeedView | "SOLDADO ABATIDO": texto grande, rojo/naranja, que explota (overshoot de escala + sacudida) y se desvanece en 2 segundos… |
| `GroupCardsView.cs` | 116 | GroupCardsView | Item 215: tarjetas de grupo de control. Los grupos (Ctrl+1..9) existian pero eran INVISIBLES: el jugador tenia que acord… |
| `TextoSoloMision.cs` | 116 | TextoSoloMision | Pedido de pulido de UI: "eliminar textos: todos los textos exceptuando el de mision, y el de mision con las minimas pala… |
| `CirculoDeProgreso.cs` | 115 | CirculoDeProgreso | Anillo de progreso 0-1 reutilizable: un circulo de fondo (siempre entero) mas un circulo de relleno encima (Image.type=F… |
| `ButtonSfx.cs` | 110 | ButtonSfx | Pedido explicito: "todos los botones deben tener un sonido al apuntar al boton y otro sonido al hacerle click". Un solo … |
| `OffscreenKillMarkerView.cs` | 108 | OffscreenKillMarkerView | Las bajas fuera de encuadre solo llegaban por el feed de texto, que se pierde entre el resto de la informacion. Una flec… |
| `IndicadorDeExposicion.cs` | 106 | IndicadorDeExposicion | Icono chico abajo al centro (antes un cartel de texto): dice si el soldado que manejas esta a la LUZ (los enemigos te ve… |
| `MilitaryButtonSkin.cs` | 103 | MilitaryButtonSkin | Pedido explicito: "imagen low-poly futurista belico" para el fondo de los botones, y "mejorar el arte de la UI" en gener… |
| `ModoDiosView.cs` | 97 | ModoDiosView | Cartel fijo "MODO DIOS [F4]" mientras nadie de tu bando recibe dano: se ve siempre para que nadie juegue una partida ent… |
| `PhaseBannerView.cs` | 94 | PhaseBannerView | Cartel grande centrado al completar una fase ("Felicidades, terminaste la Fase 1..."), con una animación de tamaño (lerp… |
| `HeadshotPopup.cs` | 92 | HeadshotPopup | Bug #044: el "HEADSHOT" salia como etiqueta en el mundo, pegada a la cabeza del enemigo: a 30 m eran 4 pixeles rojos al … |
| `ModeToastView.cs` | 91 | ModeToastView | Aviso breve del modo al que se acaba de pasar ("VISTA RTS" / "VISTA FPS"). Antes, el cambio de modo era un corte de proy… |
| `RosterView.cs` | 91 | RosterView | Arma el roster instanciando UN prefab de fila (RosterRowView) por soldado del equipo del jugador -- pedido explicito: "u… |
| `DeadNoticeView.cs` | 90 | DeadNoticeView | Cartel "Está muerto" que aparece al intentar poseer a un aliado caído (F1/F2/F3 o apuntando), y se desvanece solo en 3 s… |
| `PlayerHealthView.cs` | 90 | PlayerHealthView | Vida del soldado que estás manejando ahora mismo. Antes no existía: la única forma de saber cuánta vida te quedaba era b… |
| `RespawnPromptView.cs` | 87 | RespawnPromptView | Pedido explicito: "cuando muera quiero que aparezca que diga apreta barra espaciadora para reaparecer y ese cuadro vibre… |
| `EstadoIconFactory.cs` | 83 | EstadoDeFila, EstadoIconFactory | Iconos de estado del soldado para el roster (arriba de cada tarjeta): calmado / atacando / interactuando. Formas blancas… |
| `AliadosSinEstorbo.cs` | 80 | AliadosSinEstorbo | Con la camara sobre el hombro, un aliado (vivo o caido) que se para pegado a la camara llenaba media pantalla con un blo… |
| `HudPulido.cs` | 72 | HudPulido | Ajustes de HUD que valen para TODAS las escenas de juego (gameplay, tutorial, pruebas) sin tocar cada constructor de esc… |
| `SpriteBlanco.cs` | 70 | SpriteBlanco | BUG REAL, y afectaba a TODAS las barras del juego a la vez. Una Image de uGUI con type = Filled solo respeta fillAmount … |
| `TextoMinimo.cs` | 69 | TextoMinimo | Pedido: "el texto de mision con las minimas palabras posibles". Los directores de nivel arman frases largas ("Avanza ent… |
| `CapasDeHud.cs` | 68 | CapasDeHud | En RTS no hay soldado poseido que actualice el panel de arma: muestra el arma del lider de la escuadra. |
| `MissionStatusView.cs` | 66 | MissionStatusView | Cuántos enemigos quedan y cuántos de los tuyos siguen vivos. Antes no había forma de saberlo: se peleaba a ciegas, sin s… |
| `SelectionCountView.cs` | 63 | SelectionCountView | Cuantos hay seleccionados, en un numero grande y propio -- antes solo aparecia al final del texto de ayuda de RTS, mezcl… |
| `SpriteDeTriangulo.cs` | 57 | SpriteTriangulo | Ronda 12: las flechas del HUD (aliados fuera de encuadre, bajas) eran rectangulos lisos de 16x22. Deben ser triangulos. … |
| `InstructionBannerView.cs` | 43 | InstructionBannerView | Texto persistente abajo-centro: qué tecla apretar ahora, o que no hay nada que hacer. Lo actualiza quien conduce el fluj… |
| `FondoOpacoLink.cs` | 38 | FondoOpacoLink | BUG REAL ("la UI que solapa feo" -- cajas negras vacias que se quedaban pegadas en pantalla): FondoOpaco.Poner cuelga el… |

## Vehicles — 11 scripts, 2,954 lineas

> Tanque, asientos, torretas y ametralladoras fijas.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `Vehicle.cs` | 969 | Vehicle | Vehículo con 4 asientos: conductor, artillero y dos pasajeros. Los soldados que suben quedan ocultos (su cuerpo se desac… |
| `TurretWeapon.cs` | 580 | TurretWeapon, AmmoType | Arma montada en el vehículo: gira sola (no con el chasis) y dispara proyectiles del mismo pool que las armas de mano. |
| `TurretAI.cs` | 330 | TurretAI | Apuntado automático de la torreta cuando no hay un artillero humano adentro: busca al enemigo vivo más cercano en rango,… |
| `VehicleBrain.cs` | 310 | VehicleBrain | Conduce el vehículo solo cuando nadie lo está manejando a mano. Recibe una orden de ir a un punto (clic derecho) y gira … |
| `TorretaFija.cs` | 264 | TorretaFija | AMETRALLADORA FIJA: los emplazamientos del mapa (P_Env_Emplazamiento_MG, en las torres y los bunkers) dejan de ser decor… |
| `VehicleMotor.cs` | 170 | VehicleMotor | Motor del vehículo: acelera y frena de forma progresiva (no es instantáneo como el motor de un soldado), y gira más rápi… |
| `Atropello.cs` | 146 | Atropello | Atropellar con el vehiculo en movimiento. KnockNearbyProps ya hacia este barrido para los objetos livianos (bidones, caj… |
| `VehicleAudioFeedback.cs` | 108 | VehicleAudioFeedback | El vehiculo era completamente mudo: ninguna realimentacion sonora de que estabas conduciendo ni de a que velocidad ibas.… |
| `DetachedTurretFlight.cs` | 49 | DetachedTurretFlight | El cañon desprendido en la explosion final: sale hacia arriba con algo de giro y cae. Gravedad a mano, no Rigidbody -- e… |
| `HitboxDeImpacto.cs` | 23 | HitboxDeImpacto | Marca un collider que existe SOLO para que le puedan pegar -- no es un obstaculo real. NavService.BlocksMovement lo excl… |
| `VehicleSeatRole.cs` | 5 | VehicleSeatRole | — |
