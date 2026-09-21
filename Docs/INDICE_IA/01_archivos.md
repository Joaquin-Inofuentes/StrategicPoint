# Mapa de archivos
> 246 scripts. Generado el 2026-09-21 20:55 UTC.

## Actors — 4 scripts, 682 lineas

> El soldado: identidad, piezas, motor de movimiento y aspecto.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `SoldierMotor.cs` | 384 | SoldierMotor | Mueve el transform. Lo usan por igual el jugador y la IA detrás de la misma llamada, sin que a ninguno le importe quién … |
| `Soldier.cs` | 155 | Soldier | Reúne las piezas del GameObject y las expone. No decide nada por sí mismo. Se auto-inicializa en Awake a partir de sus p… |
| `SoldierLook.cs` | 77 | SoldierLook | Viste al soldado segun su clase: cambia la malla del SkinnedMeshRenderer por la variante armada por SoldierVariantBuilde… |
| `SoldierClasses.cs` | 66 | SoldierClasses, Def | Las clases del juego, en un solo lugar: como se llama, que modelo lleva, que armas trae (las teclas 1/2/3 son sus ranura… |

## Ai — 11 scripts, 2,893 lineas

> Cerebro de una unidad no poseida y el driver que avanza la simulacion.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `AiBrain.cs` | 1242 | CombatStance, AiBrain | Postura de combate de una unidad. Libre es el comportamiento historico y por defecto: las otras dos NO son un camino de … |
| `AiBrain.Navegacion.cs` | 413 | AiBrain | AiBrain (parte): rutas, rodeo de obstaculos, deteccion de trabas y avance hacia un punto. |
| `AiBrain.Tactica.cs` | 376 | AiBrain | Parte "tactica" del cerebro: coberturas (ordenadas por el jugador o buscadas por el propio enemigo en combate), seguir a… |
| `AiBrain.Granadas.cs` | 173 | AiBrain | Granadas para la IA: (57) los aliados las lanzan contra enemigos agrupados, y los enemigos tambien en DIFICIL; (58) cual… |
| `WorldSimulationDriver.cs` | 165 | WorldSimulationDriver | Avanza IA, armas y vehículos cada frame en Play mode real. El test automático no usa esto: simula el mismo paso a mano p… |
| `AiBrain.Sentidos.cs` | 160 | AiBrain | AiBrain (parte): linea de tiro, posturas, cobertura y sensado del enemigo mas cercano. |
| `PathPreview.cs` | 118 | PathPreview | Item 218: vista previa de la ruta. Antes de este item el jugador no tenia forma de saber POR DONDE iba a ir la escuadra:… |
| `AiBrain.Disparo.cs` | 102 | AiBrain | Ronda 11 (puntos 3, 12 y 13): como dispara la IA. Antes cada soldado gatillaba en el mismo tick en que entraba en Attack… |
| `AjustesDeEscuadra.cs` | 72 | AjustesDeEscuadra | Ajustes de la escuadra aliada, editables desde el Inspector. GameObject: "Systems/AjustesDeEscuadra" (componente Ajustes… |
| `AiBrain.Revivir.cs` | 57 | AiBrain | Ronda 13 (punto 1): "el companero revivido no me sigue ni obedece ninguna orden". Habia cuatro caminos para revivir (tec… |
| `AiState.cs` | 15 | AiState | — |

## Camera — 2 scripts, 896 lineas

> Rig de camara: hombro, RTS, transiciones, sacudidas, zoom.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `CameraRig.cs` | 850 | ControlMode, CameraRig | Posee la cámara y delega su posición en el modo activo (FPS u RTS). |
| `CameraFxSettings.cs` | 46 | CameraFxSettings | Interruptor global de los efectos de camara (sacudida, balanceo al caminar, viñeta de velocidad, destellos, latido). Es … |

## Combat — 12 scripts, 2,474 lineas

> Vida, dano, armas, proyectiles y catalogo.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `Projectile.cs` | 839 | Projectile | Viaja, comprueba su propio impacto por distancia (sin física) y se devuelve solo al pool. Nunca lo instancia nadie salvo… |
| `WeaponHolder.cs` | 752 | WeaponChangedEvent, WeaponHolder | Un solo evento para "este soldado tiene otra arma puesta", sin importar el camino (recogida del piso, ciclado 1/2/3, IA)… |
| `Granada.cs` | 248 | Granada | Granada de mano (tecla [G], ronda 7). Se tira con una parabola que ella misma calcula: la vista previa que se dibuja al … |
| `Health.cs` | 178 | Health | Única responsabilidad: llevar la cuenta de puntos de vida y avisar cuando cambian o llegan a cero. No sabe que existe un… |
| `WeaponModels.cs` | 140 | WeaponModels | Carga (y cachea) los prefabs de arma real armados por SP.EditorTools.WeaponPrefabBuilder a partir de los FBX de Assets/A… |
| `ProjectilePool.cs` | 126 | ProjectilePool | Dueño del pool de proyectiles. Cero Instantiate en combate: todo pasa por acá. El pool en sí (ObjectPool<T>) es estado d… |
| `WeaponCatalog.cs` | 81 | WeaponCatalog, Spec | Estadísticas y color de cada arma, en un solo lugar. Las armas recogibles del piso (WeaponPickup) y las teclas rápidas 1… |
| `WeaponPickup.cs` | 75 | WeaponPickedUpEvent, WeaponPickup | Cubo en el mundo que representa un arma. Al equiparla (E cerca), cambia el arma del soldado: daño, cadencia y el color d… |
| `IWeapon.cs` | 13 | IWeapon | Contrato mínimo de un arma equipable. Un arma cuerpo a cuerpo puede implementar esto sin implementar recarga (eso vive e… |
| `WeaponKind.cs` | 11 | WeaponKind, ReticleStyle | Los tres primeros se mantienen (los serializa el prefab de armas del piso); los nuevos se agregan al final para no corre… |
| `RoleType.cs` | 6 | RoleType | Flanker se agrega al final: el enum se serializa en las escenas. |
| `TeamId.cs` | 5 | TeamId | — |

## Core — 24 scripts, 3,715 lineas

> Servicios sin escena: bus de eventos, registros, grilla espacial, navegacion, pools, idioma, dificultad.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `WaypointGraph.cs` | 696 | WaypointGraph, NodeMinHeap | ITEM 226 -- Grafo de waypoints (grilla + A* sobre el plano XZ). POR QUE EXISTE (y por que NO es una optimizacion). El mo… |
| `Coberturas.cs` | 355 | Coberturas | Los puntos de cobertura del mapa: donde pararse para tener un obstaculo entre uno y el enemigo. Que cuenta como obstacul… |
| `LocTextos.cs` | 336 | LocTextos | Textos del tutorial y de la mision en ingles (item 27). Loc los suma a su tabla al iniciar; la clave es el texto origina… |
| `NavService.cs` | 260 | NavService | EL PEGAMENTO QUE FALTABA. El proyecto ya tenia WaypointGraph (A*) y FlowField, escritos y cubiertos por la suite headles… |
| `FlowField.cs` | 222 | FlowField | Item 227: campo de flujo. ADVERTENCIA HONESTA SOBRE CUANDO USARLO. Este proyecto mueve a los soldados en linea recta (So… |
| `Events.cs` | 208 | DamageTakenEvent, HealedEvent, EntityDiedEvent | Eventos: structs inmutables. Nunca llevan una referencia a un MonoBehaviour, solo ids, posiciones y valores. Es la única… |
| `Loc.cs` | 187 | Idioma, Loc, LocTraductor | Localizacion (item 27), espanol -> ingles. El texto original del juego es la CLAVE (no hay tablas de ids que mantener): … |
| `Deslizador.cs` | 185 | Deslizador | Resolucion de colision para cuerpos que se mueven ESCRIBIENDO EL TRANSFORM, que es como se mueve todo en este proyecto: … |
| `SpatialGrid.cs` | 182 | SpatialGrid | ActorRegistry.FindNearest(EnemyInRange) barre linealmente TODOS los soldados en cada llamada. AiBrain.Tick() llama a esa… |
| `ActorRegistry.cs` | 156 | ActorRegistry | Registro simple de soldados vivos en la escena, para consultas de distancia y de sensado. Sustituye a FindObjectOfType. |
| `Dificultad.cs` | 130 | NivelDificultad, Dificultad, Perfil | Dificultad de la partida: se elige al tocar JUGAR y reparte "potenciadores" de vida y dano a tres grupos: los ENEMIGOS, … |
| `ObjectPool.cs` | 117 | IPoolable, ObjectPool | Un objeto agrupable sabe reiniciarse al salir del pool y limpiarse al volver. |
| `ApoyoEnElPiso.cs` | 105 | ApoyoEnElPiso | Nadie deberia estar flotando ni enterrado, y habia de los dos. En SC_TestLevel -- la escena que arma la suite headless -… |
| `MetricasDeBuild.cs` | 100 | MetricasDeBuild, Corredor | Modo de medicion para builds: `Juego.exe -spmetrics` carga SC_Gameplay desde el menu (o donde este), espera 6 s de calen… |
| `EventBus.cs` | 94 | EventBus, ActionDisposable | Bus de eventos desacoplado: los emisores publican, los oyentes se suscriben. Ninguno de los dos se conoce entre sí. |
| `WorldSystemsRegistry.cs` | 92 | WorldSystemsRegistry | Mismo patron que ActorRegistry, pero para los tres tipos que WorldSimulationDriver necesita recorrer cada frame. Antes e… |
| `ComandosDeDepuracion.cs` | 56 | ComandosDeDepuracion | Ronda 11 (punto 15): comandos de prueba para reproducir a mano el caso "muere el soldado que manejo" y ver si viene el m… |
| `ModoDios.cs` | 41 | ModoDios | MODO DIOS ([F4]): nadie del bando del jugador recibe dano. Ni el soldado que manejas, ni la escuadra, ni el civil, ni el… |
| `RecursosCache.cs` | 41 | RecursosCache | Resources.Load con memoria: el primer pedido carga, los siguientes salen de un diccionario. No guarda los fallos (un .mp… |
| `GameLog.cs` | 39 | GameLog | Log de flujo de juego (menú, pausa, victoria/derrota, ordenes de alto nivel) separado a propósito de TestLog: TestLog es… |
| `ReinicioDeEstaticos.cs` | 37 | ReinicioDeEstaticos | Con "Enter Play Mode sin recarga de dominio" los estaticos sobreviven entre una corrida y la otra (modo dios, regeneraci… |
| `RaicesDeEscena.cs` | 29 | RaicesDeEscena | Busca un objeto que es RAIZ de escena por su nombre. Recorre solo las raices de las escenas cargadas (decenas de objetos… |
| `TestLog.cs` | 25 | TestLog | Logueo con timer para el test de integración. [t=0.00s] mensaje. |
| `CamaraPrincipal.cs` | 22 | CamaraPrincipal | Camara principal cacheada: Camera.main busca por tag en cada llamada; aqui se resuelve una vez y solo se vuelve a buscar… |

## Demo — 1 scripts, 669 lineas

> Corredor de demo automatica.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `AutoDemoRunner.cs` | 669 | AutoDemoRunner | Corre las 4 fases del guion de prueba en Play mode real (no en Edit mode como HeadlessTestRunner): usa las mismas APIs p… |

## Editor — 38 scripts, 15,503 lineas

> Herramientas de Editor: suite headless, constructores de escena, pipelines de arte.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `HeadlessTestRunner.cs` | 4200 | HeadlessTestRunner, BenchResult, StressResult | Construye el entorno de prueba (suelo, obstáculos, prefabs, 3 soldados, cámara, UI, pool de proyectiles) y corre las 3 f… |
| `HeadlessTestRunner.Fases8a12.cs` | 1551 | HeadlessTestRunner | HeadlessTestRunner (parte): fases 8 a 12 de la suite. |
| `ArtSetup.cs` | 891 | ArtSetup, Prop, RecalibrarAlturaAlReimportar | Pipeline de importacion del arte de Assets/ARTS. Es una herramienta y no un README con pasos a mano a proposito: el arte… |
| `WorldArtPipeline.cs` | 865 | WorldArtPipeline, Volumen, ReglaCategoria | Pipeline del pack de arte nuevo (Assets/ARTS/SP_Arte/_FBX_Export): ~90 mallas modulares/ambiente/vehiculos/personajes/ar… |
| `ArtBuilder.cs` | 814 | ArtBuilder, Volumen, PropDef | Segunda mitad del pipeline de arte: con los FBX ya importados y materializados por ArtSetup, esto arma lo que el juego u… |
| `HeadlessTestRunner.Fases13a17.cs` | 691 | HeadlessTestRunner | HeadlessTestRunner (parte): fases 13 a 17 de la suite. |
| `LevelBlockoutBuilder.cs` | 590 | LevelBlockoutBuilder, Bloque, Ronda | "Blockout" del nivel de SC_Gameplay: SOLO pintura de terreno + cubos. Todo el bloqueo es un cubo (BoxCollider + Obstacle… |
| `HierarchyPowerTools.cs` | 533 | HierarchyPowerTools | Caches de GUI y GC Alloc |
| `HeadlessTestRunner.Fase21.cs` | 529 | HeadlessTestRunner | FASE 21 (Ronda 13): los doce pedidos de la ronda. Cada bloque nombra el punto del pedido que audita. 1 revivido obedece … |
| `TutorialSceneBuilder.cs` | 461 | TutorialSceneBuilder, Cubo | Arma SC_Tutorial: una escena NUEVA hecha a partir de SC_Gameplay (asi trae el jugador, los aliados, el tanque, el HUD, l… |
| `HeadlessTestRunner.Fase19.cs` | 419 | HeadlessTestRunner | FASE 19 (ronda 9, segunda tanda): ayudas del tutorial, radial legible, boton de menu uniforme, zoom, salto agachado, ord… |
| `BlockoutArtDresser.cs` | 366 | BlockoutArtDresser | "Implementa arte en todo": viste el BLOCKOUT (cubos grises) con los prefabs de arte de Assets/_Project/Prefabs/ArteMundo… |
| `TerrainSetupHelper.cs` | 319 | TerrainSetupHelper | 1. Asegurar carpeta para assets de terreno |
| `BalanceBench.cs` | 273 | BalanceBench, Resultado | Banco de balance: corre en Play mode (SC_Gameplay) y hace pelear a los soldados entre si con el tiempo acelerado, para m… |
| `SoldierVariantBuilder.cs` | 273 | SoldierVariantBuilder, Pieza, Variante | Arma las VARIANTES de soldado (asalto, flanqueador, medico, fusilero y francotirador) a partir de los FBX de arte de Ass… |
| `SkinTransfer.cs` | 247 | SkinTransfer | POR QUE EXISTE ESTO. El soldado del juego venia con la textura ESTIRADA en franjas, y no era un problema del material: s… |
| `ReadmeEditor.cs` | 243 | ReadmeEditor | Match selection color which works nicely for both light and dark skins |
| `HeadlessTestRunner.Fase20.cs` | 207 | HeadlessTestRunner | FASE 20 (Ronda 11): fin de partida por escuadra caida, IA con dispersion/rafagas/alcance de arma, umbral de Q, E tap/hol… |
| `AntesProbe.cs` | 175 | AntesProbe, Paso | Ronda 11: instrumento SOLO de medicion (no toca ninguna regla del juego). Sirve para dejar registrado el "antes" de cada… |
| `MenuSceneBuilder.cs` | 158 | MenuSceneBuilder | Arma la escena de menú principal: fondo, título, botón Jugar y botón Salir. Mismo patrón que HeadlessTestRunner: todo po… |
| `HeadlessTestRunner.BusquedasGlobales.cs` | 154 | HeadlessTestRunner | Red de seguridad del plan "eliminar los Find de runtime": el codigo de runtime solo puede BAJAR su cantidad de busquedas… |
| `PrimerFramePreview.cs` | 154 | PrimerFramePreview | Deja SC_Gameplay, SIN dar Play, como se ve el primer frame del juego real: camara detras del primer soldado, minimapa mi… |
| `HeadlessBuilder.cs` | 141 | HeadlessBuilder | Resuelve el path de salida: variable de entorno (para CI) > EditorPrefs (configurable por herramienta) > default relativ… |
| `MonedaMunicionBuilder.cs` | 140 | MonedaMunicionBuilder | Ronda 12: la municion que sueltan los enemigos era un cubo amarillo. Ahora es una MONEDA 3D con una bala en relieve, com… |
| `HeadlessTestRunner.Fase18.cs` | 132 | HeadlessTestRunner | FASE 18 (ronda 9): reservas de municion, mando, accesibilidad, fuego amigo en DIFICIL, IA con granadas y supresion, esta… |
| `WeaponPrefabBuilder.cs` | 131 | WeaponPrefabBuilder | Arma los prefabs de arma REAL (rifle/pistola/pesada/metralleta) a partir de los FBX multi-parte de Assets/ARTS/SP_Arte/_… |
| `RosterUiPipeline.cs` | 117 | RosterUiPipeline | Pedido explicito: "en Roster los bloques... quiero un prefab para cada caso y q tenga puntero al soldado... y se sincron… |
| `AudioAnalisisReport.cs` | 101 | AudioAnalisisReport, Fila | Item 65 (Ronda 11): "los sonidos se verificaron por metricas y no de oido". Un agente no oye, pero puede medir mas que "… |
| `NavMeshSetupPipeline.cs` | 98 | NavMeshSetupPipeline | Dejar activa la escena principal |
| `PlaymodeCleanup.cs` | 88 | PlaymodeCleanup | BUG REAL: decals (agujeros de bala, crateres) y escombros creados en Play mode quedaban manchando la escena despues de f… |
| `AudicionDeSonidos.cs` | 79 | AudicionDeSonidos | Item 65 (Ronda 12): el analisis de audio mide, pero el timbre y "si queda bien" solo lo juzga un oido. Esta herramienta … |
| `SoldierPrefabPipeline.cs` | 64 | SoldierPrefabPipeline | Pedido explicito: "Crea un prefab para cada soldado y limpia un poco". Los 3 aliados jugables (Soldado_1_Vega/2_Kes/3_Do… |
| `TutorialAutoPlayerMenu.cs` | 57 | TutorialAutoPlayerMenu | Menu de Editor para el pedido "quiero un script con timers para el tutorial que de manera mecanica haga todo el tutorial… |
| `ArtsUsoReport.cs` | 55 | ArtsUsoReport | Item 89: dice cuanto de Assets/ARTS usa de verdad el juego. Raices = todo lo que vive fuera de ARTS (escenas, prefabs, m… |
| `RespaldoDeEscenas.cs` | 55 | RespaldoDeEscenas | Item 93: SC_TestLevel, SC_Menu y SC_Tutorial se regeneran por codigo (la suite y los builders las reescriben), asi que u… |
| `HeadlessTestRunner.Fase22.cs` | 51 | HeadlessTestRunner | FASE 22: eliminacion de los Find de runtime. Los servicios unicos exponen "Activo", el tutorial lee un catalogo serializ… |
| `CliBuilder.cs` | 43 | CliBuilder | Build de Windows x64 invocable por linea de comandos (-batchmode -executeMethod SP.EditorTools.CliBuilder.BuildWindows64… |
| `MisionBuilder.cs` | 38 | MisionBuilder | Arma la mision de rescate en SC_Gameplay: un objeto "Mision" con el MisionDirector y las referencias a los prefabs (enem… |

## Interaction — 1 scripts, 32 lineas

> Contrato de lo interactuable.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `IInteractable.cs` | 32 | IInteractable | Interfaz comun para "objetos con los que el jugador interactua" (cajas de suministro, puntos de cobertura manuales, rehe… |

## Mision — 5 scripts, 1,202 lineas

> La partida: objetivos, oleadas, rehen, helicoptero, cinematica final.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `MisionDirector.cs` | 499 | FaseDeMision, MisionDirector | MISION DE RESCATE (partida principal). 1. INFILTRAR   avanzar entre las lineas enemigas hasta el CENTRO (la plaza de la … |
| `CinematicaDeVictoria.cs` | 259 | CinematicaDeVictoria | Cinematica de victoria: la escuadra y el civil suben al helicoptero, este despega en una nube de polvo y huye hacia el o… |
| `Helicoptero.cs` | 194 | Helicoptero | El helicoptero de extraccion (geometria P_Veh_Heli del arte): helices que giran, disco de desenfoque a tope de vueltas, … |
| `MisionHud.cs` | 141 | MisionHud | Cartel de la mision (arriba al centro, debajo de "ENEMIGOS · ESCUADRA"): capitulo, objetivo con su distancia o su tempor… |
| `EstadoDePartida.cs` | 109 | EstadoDePartida | Ronda 11 (puntos 1 y 20). Antes la derrota se decidia UNA sola vez, en la corrutina de muerte del soldado poseido, asi q… |

## Otros — 1 scripts, 17 lineas

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `Readme.cs` | 17 | Readme, Section | — |

## Player — 24 scripts, 8,174 lineas

> Traduccion de intencion a ordenes: input, posesion, seleccion, ordenes.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `PlayerInputDriver.cs` | 3498 | PlayerInputDriver | Traduce teclado/ratón reales a los mismos métodos que usa el test automático. No decide nada nuevo: es el "pegamento" de… |
| `OrderService.cs` | 696 | FormationKind, OrderService | Disposiciones posibles de un lote de destinos. Vive AFUERA de OrderService (que es una clase estatica) para que el drive… |
| `PlayerInputDriver.Vehiculo.cs` | 655 | PlayerInputDriver | PlayerInputDriver (parte): manejo de vehiculos (entrar, asientos, camara, ordenes con [T]). |
| `PlayerInputDriver.Radial.cs` | 585 | PlayerInputDriver | PlayerInputDriver (parte): menu radial de [Q] y ejecucion de las ordenes de escuadra. |
| `PedidoDeCuracion.cs` | 333 | PedidoDeCuracion | "Necesito curarme" del menu de ordenes ([Q] sostenido). De las cinco ordenes del menu, cuatro ya existian en OrderServic… |
| `Demolicion.cs` | 330 | Demolicion, DemoledorAsalto | DEMOLER (habilidad del soldado de ASALTO): plantar una carga en un muro o cobertura. Hay que estar AGACHADO y QUIETO fre… |
| `KeyBindings.cs` | 266 | KeyBindings | Item 208: remapeo de teclas. Las ~35 lecturas de teclado de PlayerInputDriver estaban hardcodeadas (kb.rKey, kb.eKey, ..… |
| `SelectionController.cs` | 232 | SelectionController | Selección múltiple en vista RTS. |
| `AimTargeting.cs` | 161 | AimTargetType, AimResult, AimTargeting | B4: raiz del objeto golpeado, para el anillo generico de apuntado (SelectionRingFx necesita un Transform a quien seguir,… |
| `CajaDeSuministros.cs` | 147 | CajaDeSuministros | Caja de suministros: antes las granadas (3) y la vida no se reponian NUNCA durante la partida. Caminar hasta la caja con… |
| `Rehen.cs` | 140 | Rehen, GiroDeMarcador | Hace que el civil de la mision de rescate (RoleType.Civilian, creado por MisionDirector.AparecerCivil) se distinga a sim… |
| `PlayerInputDriver.Interaccion.cs` | 132 | PlayerInputDriver | PlayerInputDriver (parte): detector central de IInteractable. Archivo NUEVO y aditivo: no reemplaza nada de PlayerInputD… |
| `RescateAutomatico.cs` | 123 | RescateAutomatico | A5: "un aliado libre va a revivirte y frena el timer". Mismo patron que PedidoDeCuracion (curarse por pedido del jugador… |
| `PlayerInputDriver.Escuadra.cs` | 118 | PlayerInputDriver | PlayerInputDriver (parte): orden de la escuadra. El lugar en la lista fija a que numero de roster y a que [F1..F9] respo… |
| `MunicionPickup.cs` | 117 | MunicionPickup | Municion suelta al morir un enemigo (item nuevo). 100% por codigo, sin prefab ni asset externo -- mismo espiritu que Caj… |
| `PlayerBrain.cs` | 113 | PlayerBrain | La consciencia que salta de cuerpo en cuerpo. Traduce intención en llamadas al soldado que ocupa. Es único en la escena. |
| `OrdenesDeEscuadra.cs` | 109 | OrdenesDeEscuadra, PuntoDeCobertura | Ordenes nuevas del radial que no vivian en OrderService: cubrirse segun hacia donde se mira y "todos quietos". Estaticas… |
| `TrazadoDeCamino.cs` | 109 | TrazadoDeCamino | Trazar un recorrido con [Ctrl] y arrancarlo con [Espacio]. La diferencia con Shift+click (que ya encolaba ordenes) es cu… |
| `OrderHistory.cs` | 96 | OrderHistory, Entry | Item 221: historial de ordenes. El jugador daba una orden a un lote y a los pocos segundos ya no tenia forma de saber qu… |
| `AccionesEnCurso.cs` | 86 | AccionesEnCurso, Accion | Ronda 13 (puntos 2 y 3): registro de "que esta haciendo ahora cada soldado" cuando la accion tarda (reanimar, curar, det… |
| `Reanimacion.cs` | 43 | Reanimacion | Ronda 13 (puntos 1 y 8): UNICO camino para revivir a un soldado caido. Lo usan la tecla [E], la habilidad del medico ([C… |
| `MandoFps.cs` | 39 | MandoFps | Lectura del gamepad para la vista en primera persona. El juego lee el teclado directo (no usa InputActions), asi que el … |
| `PossessionService.cs` | 27 | PossessionService | Ejecuta la transferencia de control y la anuncia. El cuerpo abandonado no se destruye ni se congela: su AiBrain se react… |
| `PlayerInputDriver.Registro.cs` | 19 | PlayerInputDriver | Servicio unico de la escena: se registra al activarse en vez de que cada consumidor lo busque con un barrido. El suite (… |

## Presentation — 58 scripts, 11,862 lineas

> Todo lo que se ve y se oye en el mundo: VFX, audio, marcadores, pools visuales.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `AudioDirector.cs` | 636 | SfxChannel, VoiceState, AudioDirector | Canales de mezcla (item 186). Van a nivel de namespace y no anidados en AudioDirector por la misma razon que SfxKind viv… |
| `ImpactFx.cs` | 620 | ImpactFx, FxMode, ImpactFxPool | Mini-explosion al impactar: una esfera que se agranda rapido y despues se achica hasta desaparecer, en el punto exacto d… |
| `OrderMarkerFx.cs` | 569 | OrderMarkerFx | Cilindro que aparece en el punto de una orden y se achica con un lerp hasta desaparecer. El color indica que tipo de ord… |
| `GenericSfx.cs` | 551 | SfxKind, GenericSfx | Los miembros nuevos van SIEMPRE al final: el valor entero de cada uno es lo que quedaria guardado si alguna vez se seria… |
| `PauseController.cs` | 464 | PauseController | Pausa con [ESC]: congela el tiempo (Time.timeScale=0) y muestra el panel de pausa, con un sub-panel de "Configuraciones"… |
| `SfxSintetico.cs` | 428 | SfxSintetico | Sintesis por codigo de los sonidos de combate que no tienen grabacion real (ronda 7): explosion, cohete, granada, cuchil… |
| `WorldUiDirector.cs` | 384 | WorldUiDirector | Un unico recorrido para TODA la UI de mundo (barras de vida, marcador de poseido, iconos de minimapa) mas nivel de detal… |
| `MinimapIcon.cs` | 314 | MinimapIcon | Circulo chato que representa a un soldado/vehiculo en el minimapa. Vive en su propia capa (Minimap), que la cámara princ… |
| `AccionesEnCursoView.cs` | 291 | AccionesEnCursoView, BarraMundo | Ronda 13 (puntos 2 y 3). Dibuja lo que reporta AccionesEnCurso: * una BARRA DE CARGA en el mundo, en el punto medio entr… |
| `Fragmentador.cs` | 284 | Fragmento, Fragmentador | Ronda 12: "quiero fisicas... que las cosas se destruyan de a piezas reales, que se fragmente". Antes un obstaculo que co… |
| `CubeFxReactor.cs` | 282 | CubeFxReactor | Único puente entre el bus de eventos y lo que se ve/oye de un soldado. No decide nada de gameplay: solo reacciona. Se au… |
| `DebrisPool.cs` | 271 | DebrisPool, Debris | Escombros con presupuesto FIJO. Instanciar por evento haria que un combate masivo generase basura sin control y el recol… |
| `SoldierAnimatorDriver.cs` | 269 | SoldierAnimatorDriver | Traduce lo que el soldado YA hace a parametros del Animator. No decide nada: no mueve, no dispara, no cambia de estado. … |
| `EntityStateDebugView.cs` | 253 | EntityStateDebugView | Pedido explicito: una esfera chica flotando arriba de cada soldado (aliado/enemigo), del vehiculo y de cada obstaculo, q… |
| `VehicleFxReactor.cs` | 238 | VehicleFxReactor, VehicleSmokePuff | Reaccion visual/sonora propia del vehiculo al recibir daño. Antes un impacto solo bajaba una barra de vida en el HUD -- … |
| `ObstacleMarker.cs` | 231 | ObstacleMarker | Marca un cubo como "obstáculo" para que un proyectil lo detecte y el jugador tenga feedback distinto al pegarle a un obs… |
| `PostFxDirector.cs` | 215 | PostFxDirector | Items 176 (aberracion cromatica por daño) y 178 (desenfoque de movimiento en el vehiculo). Los dos necesitan post-proces… |
| `CoverHologram.cs` | 214 | CoverHologram | Holograma de cobertura. Mientras el jugador mantiene [Shift] apuntando a una cobertura se muestra, ESTATICO y 90 % trans… |
| `MusicDirector.cs` | 210 | MusicDirector | G4: dos temas que se cruzan solos segun si hay combate cerca de la camara. Este prototipo no tiene pistas de musica impo… |
| `HealthBarView.cs` | 202 | HealthBarView | Barra de vida flotante sobre un soldado. Sube y baja con la vida y siempre mira a la cámara activa (billboard) — sirve i… |
| `GameOutcomeController.cs` | 200 | GameOutcomeController | Pantallas de victoria y derrota: UI distinta para cada una (colores, texto) pero los mismos dos botones -- Reintentar (r… |
| `KillFeedbackDirector.cs` | 196 | KillFeedbackDirector | Un solo lugar que decide como se comunica una baja. Estaba repartido (el kill feed apilaba lineas por su cuenta, la miri… |
| `UnitLabelView.cs` | 188 | UnitLabelView | C1: etiqueta al pie de cada unidad con su vida, su tipo (aliado / enemigo / vehiculo / interactuable) y, si es montable,… |
| `EnemyAlertIndicatorView.cs` | 180 | EnemyAlertIndicatorView | Marca sobre la cabeza del enemigo que dice si ya te detecto o no. Sin esto no habia forma de saber, antes de que empiece… |
| `ArmaEnLaMano.cs` | 175 | ArmaEnLaMano | Del plan del usuario: "Los soldados no tienen armas. Deberian tener armas". Armas tenian: WeaponVisual existe en todos d… |
| `VehicleMountIndicator.cs` | 173 | VehicleMountIndicator | Cuando le apuntás a un vehículo, muestra una flecha (cilindro + cono, apuntando de arriba hacia abajo) sobre el vehículo… |
| `GameplaySceneBootstrap.cs` | 172 | GameplaySceneBootstrap | Marca en el log de flujo que la escena de gameplay terminó de cargar (Start corre después de que todo el resto ya se con… |
| `FloatingDamageTextManager.cs` | 171 | FloatingDamageTextManager, Entry | Numero de daño que sube y se desvanece sobre el objetivo golpeado. Antes no habia ninguna confirmacion visual de cuanto … |
| `MiraOptica.cs` | 171 | MiraOptica | La optica del arma en primera persona. El zoom que ya existia angosta el FOV de la camara principal (60 -> 25): eso acer… |
| `SpriteFx.cs` | 165 | SpritesReales, SpriteFx | Ronda 12: los impactos de granada/canon eran una esfera de color y las marcas en el piso, quads grises lisos (placeholde… |
| `WeaponBackRack.cs` | 161 | WeaponBackRack | Pedido explicito: "quiero poder ver el soldado q manejo y sus armas en su espalda". Cuelga un cubo chico por cada arma d… |
| `SquadStateIndicatorView.cs` | 152 | SquadStateIndicatorView | AiStateChangedEvent se publicaba en cada cambio pero nadie lo consumia visualmente del lado propio: el jugador no sabia … |
| `Feedback.cs` | 148 | Feedback, WorldTag | Punto unico de feedback de las ACCIONES del juego (pedido: "toda accion tenga feedback visual y auditivo"). Cada accion … |
| `MainMenuController.cs` | 146 | MainMenuController | Menú de inicio: [Jugar] carga la escena de gameplay, [Salir] cierra el juego (o sale de Play mode si esto corre en el Ed… |
| `RagdollDeExplosion.cs` | 146 | RagdollDeExplosion, Hueso | Ragdoll fisico (item 55) para los soldados que MUEREN por una explosion: en vez de la animacion de muerte, el esqueleto … |
| `BarraDeVidaVehiculo.cs` | 144 | BarraDeVidaVehiculo | Ronda 13 (punto 9): los tanques tambien tienen barra de vida. HealthBarView es solo de soldados (se ata con GetComponent… |
| `SelectionRingFx.cs` | 141 | SelectionRingFx | Anillo (cilindro chato) que sigue a un soldado seleccionado y pulsa a simple vista quién está elegido en la vista RTS. |
| `RevivePromptView.cs` | 139 | RevivePromptView | Cartel mundial "[Q] REVIVIR" sobre un aliado caido. Mismo patron que UnitLabelView (Canvas WorldSpace propio, billboard … |
| `DecalPool.cs` | 136 | DecalKind, DecalPool | Marcas persistentes en el terreno (crateres de explosion y agujeros de bala). Mismo problema que los escombros: sin un c… |
| `MenuAmbiente.cs` | 135 | MenuAmbiente | Ambiente del menu principal (item 21): musica de fondo y un fondo tactico animado (mapa con cuadricula que se desplaza y… |
| `TrayectoriaGranadaView.cs` | 134 | TrayectoriaGranadaView | Vista previa de la granada mientras se MANTIENE [G]: la curva exacta (la misma simulacion que usa la granada de verdad, … |
| `AttackLineManager.cs` | 128 | AttackLineManager | Línea roja entre un soldado y el enemigo al que le está disparando mientras está en estado Attack. Revisa a todo el mund… |
| `PossessedMarkerView.cs` | 124 | PossessedMarkerView | En vista RTS el roster marcaba al poseido pero el mundo no: con la tropa dispersa habia que cruzar el nombre del roster … |
| `MuzzleLightPool.cs` | 120 | MuzzleLightPool, MuzzleFlashLight | Un destello plano pegado a la boca no ilumina nada. Una Light real encendida uno o dos frames cambia por completo la per… |
| `CuchilloFx.cs` | 117 | CuchilloFx, TajoVisual | Presentacion del golpe de cuchillo ([F], ronda 7): el cuchillo aparece en la mano, barre un arco con estela, suena el ta… |
| `LimpiezaDeEscena.cs` | 111 | LimpiezaDeEscena | Del plan del usuario: "Al re cargar la escena no se limpian los decals". Es cierto y no era solo de los decals. Los tres… |
| `SafeMaterial.cs` | 111 | SafeMaterial | Fuente unica y a prueba de fallos para materiales solidos de FX (anillos, marcadores, impactos, debris, indicadores...).… |
| `PatrolRouteLine.cs` | 105 | PatrolRouteLine | Marca el circuito de patrulla de un enemigo con una esfera por punto, en vez de un LineRenderer. Pedido explicito: la lí… |
| `SelectionRingManager.cs` | 99 | SelectionRingManager | Escucha SelectionChangedEvent y mantiene un anillo pulsante por cada soldado seleccionado, creándolos y destruyéndolos s… |
| `AnuncioDeZonas.cs` | 93 | AnuncioDeZonas, Zona | Avisa en que bloque del nivel esta el jugador (el nivel mide 320 m de largo: sin esto no hay sensacion de avance). Al en… |
| `AudioDucking.cs` | 92 | AudioDucking | Sordera momentanea tras una explosion muy cercana (item 181). Este proyecto no tiene AudioMixer (todos los clips se gene… |
| `OrderLineManager.cs` | 91 | OrderLineManager | Linea del soldado a su destino mientras dure una orden de movimiento simple en RTS. El marcador (OrderMarkerFx) ya dice … |
| `FuentesBelicas.cs` | 84 | FuentesBelicas | Ronda 12: dos fuentes belicas (OFL, Resources/UI/Fuentes) para TODOS los textos: menu inicial, tutorial y partida. Los ~… |
| `Subtitulos.cs` | 79 | Subtitulos | Subtitulos de sonido (accesibilidad, item 28): con la opcion activa, los sonidos que importan en combate (disparos, expl… |
| `ImpactCubes.cs` | 59 | ImpactSurface, ImpactCubes | Impacto de un proyectil, en cubitos (pedido: "si es soldado cubitos de distintos tamaños rojos, si es piso verde y si es… |
| `CursorContextual.cs` | 53 | CursorTipo, CursorContextual | Ronda 11 (punto 16): en RTS, con soldados seleccionados, el cursor cambia al apuntar algo con lo que se puede interactua… |
| `LightProp.cs` | 50 | LightProp | Objeto liviano (un bidon, una caja) que se vuelca al ser atropellado por el vehiculo. El tanque atravesaba el escenario … |
| `BattleManager.cs` | 47 | BattleManager | Condición de victoria: cuando todos los enemigos de la lista mueren y todavía queda algún soldado propio vivo, muestra l… |

## Tutorial — 13 scripts, 4,544 lineas

> Modulo de ensenanza y su reproductor automatico.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `TutorialManager.cs` | 1905 | TutorialManager, Sub, Paso | Modulo de tutorial. Recorre 35 pasos en orden; cada paso tiene sub-pasos y cada sub-paso una BANDERA booleana (ver Tutor… |
| `AutoplayRegistro.cs` | 460 | LineaLog, MuestraAutoplay, CapturaAutoplay | Registro mecanico del reproductor automatico: todo lo que pasa en una corrida queda en disco, en formato pensado para qu… |
| `TutorialAutoPlayer.cs` | 411 | TutorialAutoPlayer | Reproductor automatico y cronometrado del tutorial (pedido: "quiero un script con timers para el tutorial que de manera … |
| `TutorialUI.cs` | 338 | TutorialUI | Cuadro de dialogo del tutorial (columna derecha, debajo del minimapa): TUTORIAL · PASO 3 / 17 DISPARAR                  … |
| `TutorialAutoPlayer.Radial.cs` | 297 | TutorialAutoPlayer | Gestos de las ordenes radiales, la seleccion en FPS, el tanque y la torreta fija. La mira es real (se gira al soldado y … |
| `KeyCapView.cs` | 249 | TutorialTextures, Estado, KeyCapView | Texturas de teclas dibujadas por codigo (no hay assets: el tutorial se arma solo). Cada tecla es un RawImage con una de … |
| `VictoriaTutorial.cs` | 213 | VictoriaTutorial, Cubito, Confeti | Efecto de victoria y cierre del tutorial (paso 17): REPETIR TUTORIAL y VOLVER AL MENU |
| `TutorialAutoPlayer.Entrada.cs` | 200 | TutorialAutoPlayer | Gestos con teclado y mouse virtuales (ver EntradaVirtual): el juego los lee como si los hiciera una persona. |
| `TutorialBeacon.cs` | 159 | TutorialBeacon | Baliza del mundo para "mira aca": una columna translucida, un anillo en el piso que pulsa y una etiqueta que siempre mir… |
| `EntradaVirtual.cs` | 130 | EntradaVirtual | Teclado y mouse VIRTUALES para el reproductor automatico del tutorial (item 95): el reproductor no llama a las APIs del … |
| `TutorialFlags.cs` | 85 | TutorialFlags | Todos los booleanos del tutorial, a la vista en el Inspector (componente TutorialManager > Banderas). El cuadro de dialo… |
| `TutorialLog.cs` | 57 | TutorialLog | Log claro del tutorial: una linea por INICIO de paso, por SUB-PASO cumplido, por PISTA mostrada y por PASO completado. S… |
| `CatalogoDelTutorial.cs` | 40 | CatalogoDelTutorial | Los objetos del tutorial que viven bakeados en SC_Tutorial, con referencias serializadas. Antes el tutorial y su corrida… |

## UI — 42 scripts, 7,438 lineas

> HUD y pantallas: mira, roster, minimapa, radial, menus, ajustes.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `AimUI.cs` | 693 | AimUI | Retículo + prompt contextual: "F: Poseer a X" o "T: Ir aquí". También resalta el retículo cuando el disparo del jugador … |
| `MenuDeOrdenes.cs` | 635 | ContextoRadial, MenuDeOrdenes | Contexto del radial: que categorias y opciones tienen sentido AHORA segun lo que se apunta. Las contextuales (curar al h… |
| `MinimapFollow.cs` | 347 | MinimapFollow | Cámara cenital del minimapa: sigue al objetivo actual (soldado o vehículo poseído) desde arriba, mirando siempre hacia a… |
| `LayoutDeAjustes.cs` | 345 | LayoutDeAjustes, Resultado | Ronda 13 (punto 6): el panel de Configuraciones se rompia con la resolucion, con el tamano de interfaz y al cambiar de i… |
| `MirillaView.cs` | 344 | MirillaView | La mirilla que se ve al mantener el clic derecho (apuntar con zoom). Antes el "zoom" agrandaba x4 el anillo de la mira (… |
| `VehicleStatusView.cs` | 296 | VehicleStatusView | HUD que reemplaza al de arma en cuanto se sube al vehículo: velocímetro, barra de vida del vehículo y quién está de arti… |
| `ControlsTable.cs` | 283 | ControlContext, ControlEntry, ControlsTable | Los seis contextos de entrada reales. Son flags porque un mismo atajo puede leerse en varios (por ejemplo [TAB], que se … |
| `SelectedSoldierUI.cs` | 270 | SelectedSoldierUI, Row | Roster de la escuadra: resalta al soldado poseído y a los seleccionados en vista RTS. Solo escucha el bus, nunca decide … |
| `AlertQueue.cs` | 254 | AlertPriority, PendingAlert, AlertQueue | Prioridad de un aviso. El orden de los valores ES la regla de desempate, asi que no se reordena ni se le meten valores n… |
| `RosterRowView.cs` | 249 | RosterRowView | Una fila del roster = un prefab por soldado. Pedido explicito: "un prefab para cada caso... con puntero al soldado... si… |
| `AjustesDeJuego.cs` | 237 | AjustesDeJuego, PanelAjustesExtra | Opciones que no tenian donde vivir: pantalla completa, resolucion, calidad, paleta para daltonismo y HUD minimo. Se guar… |
| `WeaponStatusView.cs` | 232 | WeaponStatusView | HUD fijo (no depende de apuntar a nada) con qué arma tenés, cuánta munición te queda y una barra de recarga/enfriamiento… |
| `LowHealthPulseView.cs` | 211 | LowHealthPulseView | Pulso rojo en los bordes de la pantalla + latido sonoro cuando al soldado poseido le queda poca vida. Hasta ahora la uni… |
| `ScreenFlashView.cs` | 206 | ScreenFlashView | Destello a pantalla completa, reutilizable para dos cosas distintas que necesitaban lo mismo (items 181 y 184 del backlo… |
| `OffscreenAllyMarkerView.cs` | 199 | OffscreenAllyMarkerView | Item 63: marcas en el borde apuntando a los aliados que quedaron fuera de encuadre. En primera persona el jugador perdia… |
| `FondoOpaco.cs` | 188 | FondoOpaco | Del plan del usuario, tres renglones que piden lo mismo: "El cartel de F poseer a soldados fondo opaco para saber q es i… |
| `TurretAimView.cs` | 165 | TurretAimView | HUD propio del artillero. Como artillero se usaba la misma mirilla de infanteria, que no comunica lo unico que importa e… |
| `KeyRebindView.cs` | 159 | KeyRebindView | La mitad visible del item 208. KeyBindings guarda y resuelve los mapeos, pero sin esto el jugador no tiene forma de camb… |
| `DamageDirectionView.cs` | 158 | DamageDirectionView | Marca en el borde de pantalla que apunta hacia de donde vino el ultimo golpe. Antes, recibir un disparo desde fuera de c… |
| `Diagramador.cs` | 151 | Diagramador | Del plan del usuario, dos renglones distintos que resultaron ser el mismo defecto: "EN la pantalla de ganar y perder. Lo… |
| `DamageVignetteView.cs` | 147 | DamageVignetteView | Viñeta negra granulada que se degrada hacia el centro (oscura en los bordes, transparente en el medio) y destella cada v… |
| `PerfHudView.cs` | 131 | PerfHudView | Item 235: panel de diagnostico en vivo. El backlog lo ponia ULTIMO, con dependencias en 229 y 234. La dependencia esta a… |
| `GroupCardsView.cs` | 130 | GroupCardsView | Item 215: tarjetas de grupo de control. Los grupos (Ctrl+1..9) existian pero eran INVISIBLES: el jugador tenia que acord… |
| `OffscreenKillMarkerView.cs` | 116 | OffscreenKillMarkerView | Las bajas fuera de encuadre solo llegaban por el feed de texto, que se pierde entre el resto de la informacion. Una flec… |
| `CirculoDeProgreso.cs` | 115 | CirculoDeProgreso | Anillo de progreso 0-1 reutilizable: un circulo de fondo (siempre entero) mas un circulo de relleno encima (Image.type=F… |
| `VehicleKeysPanel.cs` | 113 | VehicleKeysPanel | Panel de TECLAS dentro del vehiculo (pedido: "cuando estes en el tanque aparezcan las teclas para decir que salgan todos… |
| `KillFeedView.cs` | 107 | KillFeedView | "SOLDADO ABATIDO": texto grande, rojo/naranja, que explota (overshoot de escala + sacudida) y se desvanece en 2 segundos… |
| `PlayerHealthView.cs` | 86 | PlayerHealthView | Vida del soldado que estás manejando ahora mismo. Antes no existía: la única forma de saber cuánta vida te quedaba era b… |
| `DeadNoticeView.cs` | 83 | DeadNoticeView | Cartel "Está muerto" que aparece al intentar poseer a un aliado caído (F1/F2/F3 o apuntando), y se desvanece solo en 3 s… |
| `AliadosSinEstorbo.cs` | 80 | AliadosSinEstorbo | Con la camara sobre el hombro, un aliado (vivo o caido) que se para pegado a la camara llenaba media pantalla con un blo… |
| `PhaseBannerView.cs` | 78 | PhaseBannerView | Cartel grande centrado al completar una fase ("Felicidades, terminaste la Fase 1..."), con una animación de tamaño (lerp… |
| `ModoDiosView.cs` | 75 | ModoDiosView | Cartel fijo "MODO DIOS [F4]" mientras nadie de tu bando recibe dano: se ve siempre para que nadie juegue una partida ent… |
| `HudPulido.cs` | 72 | HudPulido | Ajustes de HUD que valen para TODAS las escenas de juego (gameplay, tutorial, pruebas) sin tocar cada constructor de esc… |
| `SpriteBlanco.cs` | 70 | SpriteBlanco | BUG REAL, y afectaba a TODAS las barras del juego a la vez. Una Image de uGUI con type = Filled solo respeta fillAmount … |
| `MissionStatusView.cs` | 65 | MissionStatusView | Cuántos enemigos quedan y cuántos de los tuyos siguen vivos. Antes no había forma de saberlo: se peleaba a ciegas, sin s… |
| `RosterView.cs` | 65 | RosterView | Arma el roster instanciando UN prefab de fila (RosterRowView) por soldado del equipo del jugador -- pedido explicito: "u… |
| `ModeToastView.cs` | 64 | ModeToastView | Aviso breve del modo al que se acaba de pasar ("VISTA RTS" / "VISTA FPS"). Antes, el cambio de modo era un corte de proy… |
| `SelectionCountView.cs` | 59 | SelectionCountView | Cuantos hay seleccionados, en un numero grande y propio -- antes solo aparecia al final del texto de ayuda de RTS, mezcl… |
| `SpriteDeTriangulo.cs` | 57 | SpriteTriangulo | Ronda 12: las flechas del HUD (aliados fuera de encuadre, bajas) eran rectangulos lisos de 16x22. Deben ser triangulos. … |
| `FondoOpacoLink.cs` | 38 | FondoOpacoLink | BUG REAL ("la UI que solapa feo" -- cajas negras vacias que se quedaban pegadas en pantalla): FondoOpaco.Poner cuelga el… |
| `InstructionBannerView.cs` | 34 | InstructionBannerView | Texto persistente abajo-centro: qué tecla apretar ahora, o que no hay nada que hacer. Lo actualiza quien conduce el fluj… |
| `ButtonSfx.cs` | 31 | ButtonSfx | Pedido explicito: "todos los botones deben tener un sonido al apuntar al boton y otro sonido al hacerle click". Un solo … |

## Vehicles — 10 scripts, 2,406 lineas

> Tanque, asientos, torretas y ametralladoras fijas.

| Archivo | Lineas | Tipos | Que hace |
|---|---|---|---|
| `Vehicle.cs` | 684 | Vehicle | Vehículo con 4 asientos: conductor, artillero y dos pasajeros. Los soldados que suben quedan ocultos (su cuerpo se desac… |
| `TurretWeapon.cs` | 459 | TurretWeapon, AmmoType | Arma montada en el vehículo: gira sola (no con el chasis) y dispara proyectiles del mismo pool que las armas de mano. |
| `VehicleBrain.cs` | 308 | VehicleBrain | Conduce el vehículo solo cuando nadie lo está manejando a mano. Recibe una orden de ir a un punto (clic derecho) y gira … |
| `TurretAI.cs` | 302 | TurretAI | Apuntado automático de la torreta cuando no hay un artillero humano adentro: busca al enemigo vivo más cercano en rango,… |
| `TorretaFija.cs` | 206 | TorretaFija | AMETRALLADORA FIJA: los emplazamientos del mapa (P_Env_Emplazamiento_MG, en las torres y los bunkers) dejan de ser decor… |
| `VehicleMotor.cs` | 163 | VehicleMotor | Motor del vehículo: acelera y frena de forma progresiva (no es instantáneo como el motor de un soldado), y gira más rápi… |
| `Atropello.cs` | 139 | Atropello | Atropellar con el vehiculo en movimiento. KnockNearbyProps ya hacia este barrido para los objetos livianos (bidones, caj… |
| `VehicleAudioFeedback.cs` | 91 | VehicleAudioFeedback | El vehiculo era completamente mudo: ninguna realimentacion sonora de que estabas conduciendo ni de a que velocidad ibas.… |
| `DetachedTurretFlight.cs` | 49 | DetachedTurretFlight | El cañon desprendido en la explosion final: sale hacia arriba con algo de giro y cae. Gravedad a mano, no Rigidbody -- e… |
| `VehicleSeatRole.cs` | 5 | VehicleSeatRole | — |
