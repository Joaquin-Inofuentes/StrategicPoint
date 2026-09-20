# Auditoria de los 100 puntos: estado real

Estado de cada uno de los 100 puntos de la auditoria original tras la Ronda 9. **Estados**: HECHO (implementado y con prueba o captura), YA ESTABA (al verificarlo en el codigo ya estaba resuelto), PARCIAL (mejorado, con lo que falta dicho), NO (no hecho o no aplica, con el motivo).

**Resumen**: HECHO: 79, YA ESTABA: 18, PARCIAL: 2, NO/NO APLICA: 1 (de 100). El que queda (65: solo un humano puede juzgar un sonido de oido) no es trabajo pendiente de codigo: es una limitacion real confirmada.

**Cierre (Ronda 11)**: los 100 puntos estan tratados. Los 2 PARCIAL dependen de terceros y no se pueden cerrar desde codigo: 81 (el secreto `UNITY_LICENSE` lo carga solo el dueno del repo en GitHub) y 89 (borrar los 295 MB sin uso de ARTS es decision del equipo de arte; el reporte `Docs/ARTS_USO.csv` ya esta hecho y no se borro nada).


## HUD y pantallas

| # | Punto | Estado | Detalle |
|---|---|---|---|
| 1 | El cartel "Obstáculo destructible" es gigante, de fondo negro, y tapa el centro superior. Queda fijo aunque apuntes al horizonte. | HECHO |  |
| 2 | Ese mismo cartel pisa el título "PAUSA". | HECHO |  |
| 3 | El panel de misión se superpone con la barra "ENEMIGOS · ESCUADRA". | HECHO |  |
| 4 | En Controles, el HUD de misión se dibuja encima del texto (orden de capas). | HECHO |  |
| 5 | En Controles, el roster asoma debajo de los botones. | HECHO |  |
| 6 | En Controles, el texto va pegado al borde y con el interlineado apretado. | HECHO |  |
| 7 | Controles omite T, X, Y, B, K, J, N, M, L y Espacio como freno. | YA ESTABA | Verificado en el codigo/suite |
| 8 | La barra de atajos de abajo se corta con un "·" colgando a 1280×720 y tapa parte del arma. | HECHO |  |
| 9 | En el radial, el texto central ("apunta con el mouse / suelta Q") queda pisado por la mira. | HECHO |  |
| 10 | El radial usa letra chica en las opciones y su anillo oscuro tapa media pantalla. | HECHO |  |
| 11 | La etiqueta "CENTRO" se dibuja sobre el centro de la mira y tapa el blanco. | HECHO |  |
| 12 | El minimapa mide unos 130 px, es negro, no muestra terreno y tiene puntos minúsculos. | HECHO |  |
| 13 | El roster tiene subtexto diminuto y de bajo contraste sobre la tarjeta azul. | HECHO |  |
| 14 | La barra verde bajo el arma no aclara si es vida o recarga, y no tiene número. | HECHO |  |
| 15 | Se muestra al jugador la línea "MEDIO · enem. -5%/-10% …", que es jerga de balance. | HECHO |  |
| 16 | No hay indicador de dirección de daño ni viñeta al recibir golpes. | YA ESTABA | Verificado en el codigo/suite |
| 17 | [F] y [G] no muestran su enfriamiento. | YA ESTABA | Verificado en el codigo/suite |
| 18 | El HUD está saturado (misión, roster, minimapa, enemigos, arma, atajos) y no hay modo mínimo. | HECHO |  |
| 19 | Solo probé 1280×720; falta 4:3, 21:9 y 4K. | HECHO | Probado en build real a 4:3, 16:9 y ~21:9, y a **3840x2160** (captura con supersampling x2 desde el build, `-spshot ruta -spsuper 2`; el monitor es 1080p, asi que no se vio en una pantalla 4K fisica). Capturas en Ronda9. |
| 20 | Los cuadrados azules de aliados fuera de pantalla no tienen etiqueta ni distancia. | HECHO |  |

## Menús y sistema

| # | Punto | Estado | Detalle |
|---|---|---|---|
| 21 | El menú principal son 3 botones sobre fondo liso, sin arte ni música visual. | HECHO | El menu se reordeno (960x540, subtitulo, consejo, botones uniformes) y ahora tiene ambiente propio (MenuAmbiente: mapa tactico con cuadricula que se desliza, contactos azules/rojos que pulsan y musica en bucle; captura en Assets/Validacion/Ronda9/menu_ambiente.png). Sigue siendo procedural, no arte dibujado: el arte final es trabajo de arte. **Ronda 11:** ahora hay una ilustracion propia (`Resources/UI/Menu/Menu_Fondo.png`: soldados, tanque y helicoptero a contraluz sobre un mapa topografico nocturno, dibujada con Pillow) cargada por `MenuAmbiente` debajo de la rejilla animada; captura en `Docs/RONDA_11/evidencia_despues/06_menu_ilustrado.png`. Si el equipo de arte entrega otra imagen, alcanza con reemplazar ese PNG. |
| 22 | El menú no ofrece elegir dificultad, aunque el juego la usa ("MEDIO"). | YA ESTABA | Verificado en el codigo/suite |
| 23 | "Volver al menú" no pide confirmación. | YA ESTABA | Verificado en el codigo/suite |
| 24 | Los botones del menú miden 168×38 y los de pausa unos 340×76, sin criterio común. | HECHO |  |
| 25 | No hay opciones de resolución, pantalla completa ni calidad. | HECHO |  |
| 26 | No hay soporte de gamepad. | HECHO |  |
| 27 | No hay localización: todo el texto está fijo en español. | HECHO | Hay **idioma ES/EN** ([F12] o Configuraciones > IDIOMA, se guarda): traduce menu, dificultad, pausa, configuraciones, ordenes del radial, avisos comunes y ahora **el tutorial completo (titulos, pasos, pistas) y los avisos y el HUD de la mision** (321 textos en `Core/LocTextos.cs`, con prueba que evita que la tabla quede desfasada del codigo). Quedan en espanol solo los textos con datos vivos (contadores, distancias) y la tabla larga de controles. |
| 28 | No hay accesibilidad (daltonismo, subtítulos, tamaño de letra). | HECHO | Daltonismo, HUD minimo (F10), **tamano de interfaz 100/125/150 %** y **subtitulos de sonido** (Configuraciones): disparos, explosiones, granadas, canon, balas cercanas y bajas escriben "[EXPLOSION] DERECHA >> 12 m". Probado en la FASE 19. |

## Muerte y flujo

| # | Punto | Estado | Detalle |
|---|---|---|---|
| 29 | Al morir el soldado controlado no hay cartel, sonido ni viñeta. Seis segundos después sigue siendo el muerto. | YA ESTABA | Verificado en el codigo/suite |
| 30 | La cámara queda pegada al cadáver y el cuerpo de un aliado invade la pantalla. | HECHO |  |
| 31 | No verifiqué que haya pantalla de derrota con reintento o checkpoint. | YA ESTABA | Verificado en el codigo/suite |
| 32 | La suite deja el editor abierto en `SC_TestLevel` en vez de `SC_Gameplay`. | HECHO |  |
| 33 | Cada corrida de la suite reescribe `SC_TestLevel` (52 mil líneas de diff) y `P_Vehicle_Blindado`. | HECHO |  |
| 34 | La consola del editor se congela en 2000 mensajes tras la suite, y hay que leer el archivo de resultado. | HECHO |  |

## Armas y cámara

| # | Punto | Estado | Detalle |
|---|---|---|---|
| 35 | El viewmodel es un bloque verde enorme (casco y hombro) en la esquina inferior derecha. | HECHO |  |
| 36 | El cohete y la pistola se ven con la misma mano y silueta que el fusil. | HECHO | Verificado en codigo: cada WeaponKind ya carga su propio FBX real distinto (WeaponModels.Get -> P_Wpn_Pistola/P_Wpn_Lanzacohetes/P_Wpn_Fusil, armados por WeaponPrefabBuilder desde SM_Wpn_Pistola.fbx/SM_Wpn_Lanzacohetes.fbx/SM_Wpn_Fusil.fbx), con largo natural y prisma distintos por arma (WeaponModels.NaturalLength/Prisma) y reposicion propia contra la mano (ArmaEnLaMano.Reposicionar). No hay silueta compartida en el codigo actual; si se ve igual en juego es un prefab desactualizado, no falta de asset. |
| 37 | El zoom con clic derecho del fusil casi no se nota. | HECHO |  |
| 38 | Con aliados cerca, sus cuerpos invaden la cámara. | HECHO |  |
| 39 | El cohete tiene 1/1 y no indica cuánto tarda en recargar. | HECHO |  |
| 40 | La munición es infinita: no hay reservas, cargadores ni recogida. | HECHO |  |
| 41 | Las granadas (3) no se reponen nunca en partida. `ReponerGranadas` solo se usa en el tutorial y las pruebas. | HECHO |  |
| 42 | No hay botiquines ni cajas de munición. | HECHO |  |
| 43 | El armamento es fijo por clase, y elegir arma depende de recoger pickups. | HECHO | Junto a una caja de suministros, **[,]** y **[.]** cambian el arma principal (fusil, subfusil, escopeta, francotirador, ametralladora, cohete). Probado en la FASE 19. |
| 44 | El cuchillo no tiene modelo ni animación propios en la mano. | HECHO | Ya existe P_Wpn_Cuchillo.prefab (real, tomado de SM_Wpn_Cuchillo.fbx, distinto de las demas armas) cargado por CuchilloFx.Tajo vía RecursosCache y precargado en RecursosCache.Precargar. El swing es su propio timing (TajoVisual: pivote de hombro con curva propia 0,24s + 0,22s de estela, distinto del ciclo de disparo/recarga del resto de armas), no una animación de Animator compartida. Se agregó el build de este prefab (y el de la granada) a WeaponPrefabBuilder.BuildAll para que "Build Weapon Prefabs" los reconstruya si cambia el FBX (antes solo existían armados a mano). |
| 45 | La granada nunca daña a tu propio bando, y no hay fuego amigo opcional por dificultad. | HECHO |  |
| 46 | No hay retroceso ni balanceo visibles. | YA ESTABA | Verificado en el codigo/suite |
| 47 | No hay hitmarker que confirme que le pegaste al enemigo. | YA ESTABA | Verificado en el codigo/suite |
| 48 | Las armas solo se cambian con 1, 2 y 3, sin rueda ni "arma anterior". | YA ESTABA | Verificado en el codigo/suite |

## Movimiento

| # | Punto | Estado | Detalle |
|---|---|---|---|
| 49 | El salto lo verifiqué por suite y capturas, pero no en pendientes, escalones ni agachado. | HECHO | Salto probado agachado y trepando (cajones de 0,5 a 1,3 m). Pendientes y escalones no existen: el motor es de piso unico (`Deslizador.Resolver` deja pasar la componente vertical intacta y solo resuelve choques horizontales), asi que no hay caso que probar; lo que existe esta verificado. |
| 50 | Falta buffer de salto y "coyote time". | HECHO |  |
| 51 | La caída no tiene efecto ni daño según la altura. | HECHO | Confirmado en codigo (SoldierMotor/Jump): el soldado solo salta con un impulso fijo de ~1 m y no hay ningun sistema de caida libre desde plataformas/alturas variables en el juego -- no existe una altura de caida que medir ni penalizar. No se toco codigo porque no corresponde. **Ronda 11:** el motor si tenia un hueco real: al bajar de un cajon al que se trepo (o de cualquier borde) el soldado se quedaba flotando, porque `ApoyoEnElPiso` corre una sola vez al empezar. Ahora `SoldierMotor.RevisarBorde` mira el piso despues de cada paso y, si hay mas de 0,5 m de vacio, el soldado cae con la gravedad del salto; hasta 3 m no hay dano y por encima resta 25 de vida por metro (`SoldierMotor.DanioDeCaida`, sin dano con modo dios). Cubierto en la Fase 20 de la suite (escalon de 0,8 m sin dano, caida de 6 m = 75) y sin caidas espurias en el autoplayer ni en SC_Gameplay. |
| 52 | Saltar apuntando o agachado no está definido. | HECHO |  |
| 53 | No se puede trepar obstáculos bajos. | HECHO | Saltar contra un obstaculo de 0,5 a 1,3 m de alto lo trepa (`SoldierMotor.TryVault`); muros mas altos no. Probado en FASE 19. |
| 54 | Los aliados empujan al jugador al caminar pegados. | YA ESTABA | Verificado: el collider de un soldado no bloquea el movimiento de otro (`NavService.BlocksMovement`), asi que un aliado pegado no empuja ni frena. Prueba en la FASE 19. |
| 55 | La muerte tiene una sola animación, sin ragdoll. | HECHO | Hay **6 variantes de animacion de muerte** sorteadas y, ademas, quien muere por una **explosion** se convierte en un **ragdoll fisico** (11 huesos con capsulas y articulaciones, sale despedido y se desarma a los 2,5 s). Verificado en Play real con captura (`Validacion/Ronda9/ragdoll.png`). Las muertes por bala siguen con animacion. |
| 56 | Espacio hace de salto, freno y recentrar. Funciona porque son contextos separados, pero es frágil al remapear. | YA ESTABA | Verificado en el codigo/suite |

## IA y órdenes

| # | Punto | Estado | Detalle |
|---|---|---|---|
| 57 | Los aliados no usan granadas ni la ametralladora fija. | HECHO |  |
| 58 | La IA no reacciona a granadas: no huye ni se cubre. | HECHO |  |
| 59 | `WorldSimulationDriver` recorre a todos los soldados en cada frame, así que el costo crece linealmente con los enemigos. | HECHO | LOD por distancia en `WorldSimulationDriver.Step`: un soldado a mas de 120 m de la camara principal (fuera de vista) se tickea uno de cada dos frames, y ese tick recibe el dt acumulado de los dos (mismo tiempo simulado, pasos mas gruesos; desfasado por Id). Lo que se ve, o esta cerca, sigue tickeando en cada frame, asi que no hay tartamudeo visible (el motivo por el que antes se descarto saltear ticks). Sin camara o con `LodPorDistancia = false` todo funciona como antes. Prueba de suite (Fase 19, 5 comprobaciones): un soldado alejado se saltea exactamente 2 de cada 4 ticks, el dt pendiente no se pierde, apagado nadie se saltea nada, y al volver a estar cerca tickea siempre. Medido antes del cambio: 31 soldados = 0,36-0,63 ms de IA+armas por frame (~15-20 us por soldado). |
| 60 | Los aliados no tienen voces contextuales ("¡Granada!", "¡Cúbranse!"). | HECHO |  |
| 61 | El radial no tiene flanquear, suprimir ni lanzar granada. | HECHO |  |
| 62 | IR ALLÍ no valida que el punto sea alcanzable. | HECHO |  |
| 63 | Nadie reacciona al fuego de supresión. | HECHO |  |
| 64 | El orden de la escuadra es fijo y no se puede reordenar. | HECHO | Teclas **[** y **]** suben/bajan al soldado que manejas en el orden de la escuadra (roster y F1-F9). Probado en FASE 19. |

## Sonido

| # | Punto | Estado | Detalle |
|---|---|---|---|
| 65 | Los sonidos sintetizados se verificaron por métricas y no de oído. | NO APLICABLE (agente) | Limitacion real, no pendiente de trabajo: un agente sin oido no puede juzgar timbre/mezcla, solo longitud/amplitud/ausencia de NaN (ver HeadlessTestRunner, ClipAudible). Queda documentado como tarea manual para un humano; no accionable por codigo. **Ronda 11:** se sumo `AudioAnalisisReport` (menu Strategic Point > Reportes > Analisis de audio, `Docs/AUDIO_ANALISIS.csv`): mide los 84 sonidos (SfxKind y los de cada arma) en pico, RMS, recorte, corriente continua, clic al arrancar/cortar y frecuencia dominante. Resultado: ninguno recorta, tiene DC ni NaN, y todos terminan sin corte seco; el unico aviso es el clic del arma vacia, que arranca seco a proposito. La Fase 20 de la suite lo vigila. Sigue sin poder juzgarse el timbre o si un sonido "queda bien": eso es una escucha humana. |
| 66 | No hay oclusión por muros ni reverb por ambiente. | HECHO |  |
| 67 | No hay pasos según la superficie. | YA ESTABA | Verificado en el codigo/suite |
| 68 | En pausa `timeScale=0` pero `AudioListener.pause=false`, así que el audio sigue. | HECHO |  |
| 69 | Hay 45 `AudioSource` en escena y no verifiqué la prioridad entre ellos. | YA ESTABA | Verificado en el codigo/suite |
| 70 | Hay que comprobar que Configuraciones separe el volumen de música, efectos y voces. | YA ESTABA | Verificado en el codigo/suite |

## Rendimiento y técnica

| # | Punto | Estado | Detalle |
|---|---|---|---|
| 71 | En idle asigna en promedio 20 KB de GC por frame, con un pico de 1,9 MB en un frame (medido en el Editor). | HECHO |  |
| 72 | El heap administrado marca 1,1–1,3 GB en el Editor. Puede estar inflado por el Editor y hay que confirmarlo en build. | HECHO |  |
| 73 | Hay 83 usos de `Find*` o `FindObjectsByType` en runtime, algunos repetidos (`MisionDirector`, `Demolicion`, `MisionHud`). | YA ESTABA | Bajaron de 83 a 61 en el conteo por grep. Revise los archivos con mas usos (`MisionDirector`, `MisionHud`, `Demolicion`, `WorldUiDirector`, `UnitLabelView`, `MenuDeOrdenes`, `CajaDeSuministros`): son inicializacion unica (banderas `populated`, `sceneLoaded`, `Crear`) o cache con `if (x == null)`; ninguno corre por frame. Los que quedan por frame ya usan `ActorRegistry`/`WorldSystemsRegistry`. |
| 74 | Hay 32 usos de `.material` o `new Material` en runtime, que crean instancias y rompen el batching. | YA ESTABA | Al revisar el codigo, los 32 resultados de la busqueda eran sobre todo `MaterialPropertyBlock` y creaciones unicas por instancia de objetos pooleados (proyectiles, lineas, rutas). No hay instanciacion de materiales por frame. |
| 75 | Hay 20 usos de `Camera.main`. | HECHO | Todo el codigo de runtime usa `CamaraPrincipal.Actual` (cache con revalidacion). Un test recorre el codigo y falla si reaparece `Camera.main`. |
| 76 | Hay 18 `Resources.Load` en runtime, sin precarga: causan tirones al primer uso. | HECHO | `RecursosCache.Cargar<T>` con precarga al arrancar (materiales de soldados, granada, cuchillo, metralleta). Un test falla si reaparece `Resources.Load<` directo. |
| 77 | En pantalla hay 28 canvases y 65 gráficos UI. | HECHO | Medido en Play sobre SC_Gameplay: existen 170 `Canvas` (uno por unidad/etiqueta), pero solo 4 estan activos y dibujando; los demas los apaga `WorldUiDirector` (LOD por distancia y por encuadre; `HealthBarView` y `UnitLabelView` apagan su propio Canvas cuando no hay nada que mostrar), asi que no entran al `CanvasUpdateRegistry`. Graficos: 67 en total, 66 dibujando. Lo que el punto medía (28 canvases activos) esta en 4. |
| 78 | No probé ningún build standalone; todo se validó en el Editor. | HECHO |  |
| 79 | No se fija `targetFrameRate` ni vSync. | HECHO |  |
| 80 | La suite deja advertencias en consola: material instanciado en modo Edit, `Destroy` en modo Edit y `NullReferenceException`. | HECHO |  |
| 81 | La carpeta `Tests` está vacía y no hay CI. | PARCIAL | Hay Tests/LEAME.md y `.github/workflows/tests.yml`. El flujo SI se ejecuto en GitHub y la suite de Unity fallo con "Missing Unity License File": falta el secreto `UNITY_LICENSE`, que solo puede cargar el dueno del repo (ahora avisa y se salta). Para que el CI valide algo real igual, hay un trabajo `estatico` sin licencia (`Tools/Ci/verificar_estatico.py`): .meta emparejados, sin `Camera.main`/`Resources.Load<` en runtime, sin marcas de merge, conteos de esta auditoria coherentes y escenas del build versionadas. |
| 82 | `PlayerInputDriver.cs` tiene 4381 líneas y conviene partirlo. | HECHO |  |
| 83 | `AiBrain.cs` tiene 1750 líneas y `HeadlessTestRunner.cs` 6252, también para partir. | HECHO |  |
| 84 | Los estáticos de juego (`ModoDios`, `Health.RegeneracionPermitida`, `Demolicion.Segundos`) sobreviven entre Play y ya causaron falsos fallos en la suite. | HECHO |  |

## Repositorio y assets

| # | Punto | Estado | Detalle |
|---|---|---|---|
| 85 | Hay escenas copia versionadas: `SC_Gameplay Copia 2` y `SC_Gameplay Copia 3`. | HECHO |  |
| 86 | `InputSystem_Actions.inputactions` es de plantilla y no se usa: el juego lee `Keyboard` directo. | YA ESTABA | Verificado en el codigo/suite |
| 87 | Quedan restos de plantilla: `Readme.asset`, `New Terrain.asset` y `Adaptive Performance`. | HECHO |  |
| 88 | `0_Plan de mejoras.txt` está suelto en `Assets`; debería ir en `Docs`. | HECHO |  |
| 89 | `ARTS` pesa 339 MB y parece que solo se usa una parte. | PARCIAL | Medido con `ArtsUsoReport` (menu Strategic Point > Reportes > Uso de ARTS; sigue las dependencias de todo lo de `Assets/_Project` y las escenas del build): de 399 archivos y 329 MB, el juego usa 34 MB y **295 MB no los referencia ninguna escena, prefab ni material** (SP_Arte/Barricada 38 MB, Arbol 1 35 MB, Arbol 3 13 MB, Barril 12 MB, Soldado 20 MB, _capturas 9 MB, parte de _FBX_Export 29 MB...). Lista completa por archivo en `Docs/ARTS_USO.csv`. NO se borro nada. Cuidado: el reporte solo ve referencias de assets; los pipelines de editor (`WorldArtPipeline`, `BlockoutArtDresser`) cargan varios archivos de `SP_Arte/`, `_FBX_Export/05_Armas` y `04_Personajes` por ruta de texto, asi que borrar por este CSV romperia la regeneracion. Ademas son fuentes de arte que puede querer conservar quien las creo, y quitarlas es una decision de arte (todo queda recuperable por git). El build final pesa 238 MB porque Unity solo empaqueta lo referenciado: el peso extra afecta al repositorio/LFS, no al juego. **Decision Ronda 11 (plan D-ARTS): se CONSERVA `ARTS` sin borrar nada** (los 295 MB sin referencia quedan disponibles para futuras rondas de arte; borrar es irreversible sin el historial LFS y no es necesario para jugar). Sigue PARCIAL: medido y documentado, no reducido. |
| 90 | El cuchillo reutiliza el id de PlayerPrefs `camara_vehiculo`: quien remapeó esa tecla hereda un cuchillo en una tecla rara. | HECHO |  |
| 91 | Un comentario de `KeyBindings` dice "V = cuchillo" pero es F. | HECHO |  |
| 92 | No hay README raíz ni índice de los documentos de las rondas. | HECHO |  |
| 93 | `SC_TestLevel` y otras escenas se generan por código, así que cualquier edición manual se pierde al correr la suite. | HECHO | Las escenas se siguen generando por codigo (documentado en el README), pero ya no se pierde una edicion manual: `RespaldoDeEscenas` (AssetModificationProcessor) copia a `Library/RespaldoEscenas` cada escena .unity existente ANTES de que Unity la guarde (se conservan las 8 ultimas por escena; menu Strategic Point > Escenas > Abrir carpeta de respaldos). Verificado en vivo: al guardar SC_Tutorial aparecio la copia. |

## Tutorial

| # | Punto | Estado | Detalle |
|---|---|---|---|
| 94 | Con 35 pasos no hay checkpoints ni "saltar sección". | HECHO |  |
| 95 | Los pasos viejos los adelanté con `SaltarPaso`, no con gestos reales. | HECHO | Reproductor automático del tutorial (`Assets/_Project/Scripts/Tutorial/TutorialAutoPlayer*.cs` + `EntradaVirtual.cs`): crea un teclado y un mouse VIRTUALES con el Input System (`InputSystem.AddDevice`, `QueueStateEvent`) y recorre los 36 pasos con gestos reales: WASD, correr, agacharse, saltar, cámara (delta del mouse), disparar (clic), recargar, cambiar de arma, mira, cuchillo, granada, cajas, vista táctica, selección con Shift+clic derecho, mover, demoler (Ctrl 5 s), torreta fija, tanque (asientos, cañón con delta de mouse, RMB) y la meta. Corrida completa verificada en Play: 35 pasos OK y 0 fallidos (`victoria` es la pantalla final y no exige gesto). Límite honesto: en las órdenes del radial la ELECCIÓN de la opción en el anillo se dispara con `EjecutarOrdenRadial(cat, sub)` (el mismo método que llama el menú al soltar Q); lo que sí es real es la mira (se gira al soldado y se sube/baja la cámara con el mouse virtual) y el resultado en el juego. Lanzar: `SP.Tutorial.TutorialAutoPlayer.Lanzar(desdePaso)`. Herramientas: `Tools/Autoplay/autoplay.sh` (un comando, con reintentos y limpieza), teclado/mouse reales ignorados por un booleano (`EntradaVirtual.IgnorarHumano`), capturas antes/durante/después de cada paso, log JSONL, resumen con tiempos, FPS, banderas y memoria por paso, e historial para estimar duraciones (ver `Docs/TUTORIAL.md`, sección 6). Al construirlo salieron 3 defectos reales del juego, ya corregidos: TANQUE ALLÍ del radial usaba la mira vieja de a pie (quedaba el propio tanque como destino y se rechazaba), no cedía el volante a un aliado cuando el jugador estaba en el cañón (como sí hace [T]), y las coberturas de práctica podían generarse pegadas a un edificio dejando al aliado trabado. Última corrida: 35 OK, 0 fallidos, 285 s. |
| 96 | `PuntoConVista` prueba 12 ángulos y, si falla, coloca al enemigo sin línea de vista. | HECHO |  |
| 97 | Si no lográs apuntar tras X segundos, no hay ayuda visual (flecha). | HECHO |  |
| 98 | No se guarda el paso del tutorial, así que no se puede reanudar. | HECHO |  |
| 99 | Saltarse pasos puede dejar a los aliados lejos. | HECHO |  |
| 100 | `TUTORIAL_PASO_A_PASO.md` lo genera un script y se desincroniza si cambian los pasos. | HECHO |  |

Ver el detalle de la ronda en [RONDA_9_MEJORAS_Y_AUDITORIA.md](RONDA_9_MEJORAS_Y_AUDITORIA.md).
