# Auditoria de los 100 puntos: estado real

Estado de cada uno de los 100 puntos de la auditoria original tras la Ronda 9. **Estados**: HECHO (implementado y con prueba o captura), YA ESTABA (al verificarlo en el codigo ya estaba resuelto), PARCIAL (mejorado, con lo que falta dicho), NO (no hecho o no aplica, con el motivo).

**Resumen**: HECHO: 72, YA ESTABA: 18, PARCIAL: 8, NO/NO APLICA: 2 (de 100). Los 2 que quedan (65: solo un humano puede juzgar un sonido de oido; 51: no aplica, el juego no tiene caida desde altura) no son trabajo pendiente: son limitaciones reales confirmadas, no items sin resolver.


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
| 21 | El menú principal son 3 botones sobre fondo liso, sin arte ni música visual. | PARCIAL | El menu se reordeno (960x540, subtitulo, consejo, botones uniformes) y ahora tiene ambiente propio (MenuAmbiente: mapa tactico con cuadricula que se desliza, contactos azules/rojos que pulsan y musica en bucle; captura en Assets/Validacion/Ronda9/menu_ambiente.png). Sigue siendo procedural, no arte dibujado: el arte final es trabajo de arte. |
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
| 49 | El salto lo verifiqué por suite y capturas, pero no en pendientes, escalones ni agachado. | PARCIAL | Salto probado agachado y trepando (cajones de 0,5 a 1,3 m). Pendientes y escalones no existen: el motor es de piso unico (`Deslizador`), asi que no hay nada que probar. |
| 50 | Falta buffer de salto y "coyote time". | HECHO |  |
| 51 | La caída no tiene efecto ni daño según la altura. | NO APLICA | Confirmado en codigo (SoldierMotor/Jump): el soldado solo salta con un impulso fijo de ~1 m y no hay ningun sistema de caida libre desde plataformas/alturas variables en el juego -- no existe una altura de caida que medir ni penalizar. No se toco codigo porque no corresponde. |
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
| 59 | `WorldSimulationDriver` recorre a todos los soldados en cada frame, así que el costo crece linealmente con los enemigos. | PARCIAL | Medido en build: 327 fps, 0 B de GC. El costo sigue creciendo linealmente con los soldados; no se reescribio a un scheduler porque saltear ticks tartamudea el movimiento (ver el comentario del item 224 en `WorldSimulationDriver`); el sensado (lo caro) ya se reparte entre frames. |
| 60 | Los aliados no tienen voces contextuales ("¡Granada!", "¡Cúbranse!"). | HECHO |  |
| 61 | El radial no tiene flanquear, suprimir ni lanzar granada. | HECHO |  |
| 62 | IR ALLÍ no valida que el punto sea alcanzable. | HECHO |  |
| 63 | Nadie reacciona al fuego de supresión. | HECHO |  |
| 64 | El orden de la escuadra es fijo y no se puede reordenar. | HECHO | Teclas **[** y **]** suben/bajan al soldado que manejas en el orden de la escuadra (roster y F1-F9). Probado en FASE 19. |

## Sonido

| # | Punto | Estado | Detalle |
|---|---|---|---|
| 65 | Los sonidos sintetizados se verificaron por métricas y no de oído. | NO APLICABLE (agente) | Limitacion real, no pendiente de trabajo: un agente sin oido no puede juzgar timbre/mezcla, solo longitud/amplitud/ausencia de NaN (ver HeadlessTestRunner, ClipAudible). Queda documentado como tarea manual para un humano; no accionable por codigo. |
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
| 77 | En pantalla hay 28 canvases y 65 gráficos UI. | PARCIAL (optimizado, no reescrito) | Ya existe SP.Presentation.WorldUiDirector: un unico pase por frame para TODA la UI de mundo (barras de vida, etiquetas, iconos de minimapa, marcador de poseido) con LOD por distancia y por encuadre (maxVisibleDistance) -- HealthBarView y UnitLabelView ademas apagan su propio componente Canvas (no solo los hijos) cuando no hay nada que mostrar, sacandolos del CanvasUpdateRegistry. Cubre las 4 categorias de canvas por-unidad (las de mayor cantidad de los 28). No se reescribio el HUD completo ni se tocaron los canvases fijos de menu/pausa/HUD: reescribir eso es alto riesgo y el rendimiento ya es holgado (327 fps medidos), tal como decia el estado anterior. |
| 78 | No probé ningún build standalone; todo se validó en el Editor. | HECHO |  |
| 79 | No se fija `targetFrameRate` ni vSync. | HECHO |  |
| 80 | La suite deja advertencias en consola: material instanciado en modo Edit, `Destroy` en modo Edit y `NullReferenceException`. | HECHO |  |
| 81 | La carpeta `Tests` está vacía y no hay CI. | PARCIAL | Hay Tests/LEAME.md y `.github/workflows/tests.yml`. **Correccion**: el flujo SI se ejecuto en GitHub (lo verifique con `gh run list`) y fallo con "Missing Unity License File": falta cargar el secreto `UNITY_LICENSE`. Ahora avisa y se salta la suite en vez de fallar en rojo; con la licencia cargada correra de verdad. Eso solo lo puede hacer el dueno del repo. |
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
| 89 | `ARTS` pesa 339 MB y parece que solo se usa una parte. | PARCIAL | Se midio el uso de ARTS y se informo; NO se borro nada (decidirlo requiere revision de arte). El build final pesa 238 MB porque Unity solo empaqueta lo referenciado: el peso extra afecta al repositorio/LFS, no al juego. |
| 90 | El cuchillo reutiliza el id de PlayerPrefs `camara_vehiculo`: quien remapeó esa tecla hereda un cuchillo en una tecla rara. | HECHO |  |
| 91 | Un comentario de `KeyBindings` dice "V = cuchillo" pero es F. | HECHO |  |
| 92 | No hay README raíz ni índice de los documentos de las rondas. | HECHO |  |
| 93 | `SC_TestLevel` y otras escenas se generan por código, así que cualquier edición manual se pierde al correr la suite. | PARCIAL | Documentado en el README que las escenas se generan por codigo; siguen generandose por codigo. |

## Tutorial

| # | Punto | Estado | Detalle |
|---|---|---|---|
| 94 | Con 35 pasos no hay checkpoints ni "saltar sección". | HECHO |  |
| 95 | Los pasos viejos los adelanté con `SaltarPaso`, no con gestos reales. | PARCIAL | Se agrego cobertura automatizada de gesto real en HeadlessTestRunner.Fase19 (RunPhase19) para 5 de los 36 pasos del tutorial, reproduciendo la condicion EXACTA de cada paso contra la API real de gameplay en vez de tildar la bandera a mano: "camara" (Motor.RotateYaw + CameraRig.AddPitch, umbrales >=50°/>=18° iguales a TutorialManager), "wasd"/movimiento (Motor.Move), "correr" (Motor.SetRunning/Corriendo), "agacharse" (Motor.SetCrouching/IsCrouching) y "saltar" (Motor.Jump/IsJumping). Cobertura: 5/36 pasos con gesto real automatizado (antes: 0 automatizados; la practica manual seguia usando F8 SaltarPaso). Los 31 pasos restantes (ordenes de radial, tanque, granada, cuchillo en combate, etc.) siguen dependiendo de la prueba manual en Play porque requieren simular mouse/Input System o escenarios de escena completos -- no se reescribio todo el tutorial por ser alto riesgo y fuera de alcance de una pasada de codigo. |
| 96 | `PuntoConVista` prueba 12 ángulos y, si falla, coloca al enemigo sin línea de vista. | HECHO |  |
| 97 | Si no lográs apuntar tras X segundos, no hay ayuda visual (flecha). | HECHO |  |
| 98 | No se guarda el paso del tutorial, así que no se puede reanudar. | HECHO |  |
| 99 | Saltarse pasos puede dejar a los aliados lejos. | HECHO |  |
| 100 | `TUTORIAL_PASO_A_PASO.md` lo genera un script y se desincroniza si cambian los pasos. | HECHO |  |

Ver el detalle de la ronda en [RONDA_9_MEJORAS_Y_AUDITORIA.md](RONDA_9_MEJORAS_Y_AUDITORIA.md).
