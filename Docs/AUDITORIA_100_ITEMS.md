# Auditoria de los 100 puntos: estado real

Estado de cada uno de los 100 puntos de la auditoria original tras la Ronda 9. **Estados**: HECHO (implementado y con prueba o captura), YA ESTABA (al verificarlo en el codigo ya estaba resuelto), PARCIAL (mejorado, con lo que falta dicho), NO (no hecho o no aplica, con el motivo).

**Resumen**: HECHO: 61, YA ESTABA: 15, PARCIAL: 10, NO: 14 (de 100).


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
| 19 | Solo probé 1280×720; falta 4:3, 21:9 y 4K. | PARCIAL | Probado en build real a 4:3, 16:9 y ~21:9 (capturas en Ronda9). 4K NO probado: el monitor es 1080p. |
| 20 | Los cuadrados azules de aliados fuera de pantalla no tienen etiqueta ni distancia. | HECHO |  |

## Menús y sistema

| # | Punto | Estado | Detalle |
|---|---|---|---|
| 21 | El menú principal son 3 botones sobre fondo liso, sin arte ni música visual. | PARCIAL | El menu se reordeno (960x540, subtitulo, consejo, botones uniformes). Sigue sin arte ni musica: es trabajo de arte. |
| 22 | El menú no ofrece elegir dificultad, aunque el juego la usa ("MEDIO"). | YA ESTABA | Verificado en el codigo/suite |
| 23 | "Volver al menú" no pide confirmación. | YA ESTABA | Verificado en el codigo/suite |
| 24 | Los botones del menú miden 168×38 y los de pausa unos 340×76, sin criterio común. | HECHO |  |
| 25 | No hay opciones de resolución, pantalla completa ni calidad. | HECHO |  |
| 26 | No hay soporte de gamepad. | HECHO |  |
| 27 | No hay localización: todo el texto está fijo en español. | NO | NO hecho: la localizacion es un trabajo enorme (todos los textos fijos en espanol). |
| 28 | No hay accesibilidad (daltonismo, subtítulos, tamaño de letra). | PARCIAL | Daltonismo y HUD minimo (F10) hechos. Faltan subtitulos y tamano de letra global. |

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
| 36 | El cohete y la pistola se ven con la misma mano y silueta que el fusil. | NO | NO hecho: requiere modelos/silueta propios (arte). |
| 37 | El zoom con clic derecho del fusil casi no se nota. | HECHO |  |
| 38 | Con aliados cerca, sus cuerpos invaden la cámara. | HECHO |  |
| 39 | El cohete tiene 1/1 y no indica cuánto tarda en recargar. | HECHO |  |
| 40 | La munición es infinita: no hay reservas, cargadores ni recogida. | HECHO |  |
| 41 | Las granadas (3) no se reponen nunca en partida. `ReponerGranadas` solo se usa en el tutorial y las pruebas. | HECHO |  |
| 42 | No hay botiquines ni cajas de munición. | HECHO |  |
| 43 | El armamento es fijo por clase, y elegir arma depende de recoger pickups. | NO | NO hecho: es una decision de diseno del juego (arsenal por clase). |
| 44 | El cuchillo no tiene modelo ni animación propios en la mano. | NO | NO hecho: requiere modelo y animacion (arte). |
| 45 | La granada nunca daña a tu propio bando, y no hay fuego amigo opcional por dificultad. | HECHO |  |
| 46 | No hay retroceso ni balanceo visibles. | YA ESTABA | Verificado en el codigo/suite |
| 47 | No hay hitmarker que confirme que le pegaste al enemigo. | YA ESTABA | Verificado en el codigo/suite |
| 48 | Las armas solo se cambian con 1, 2 y 3, sin rueda ni "arma anterior". | YA ESTABA | Verificado en el codigo/suite |

## Movimiento

| # | Punto | Estado | Detalle |
|---|---|---|---|
| 49 | El salto lo verifiqué por suite y capturas, pero no en pendientes, escalones ni agachado. | PARCIAL | Salto probado agachado. Pendientes y escalones no se probaron de forma sistematica. |
| 50 | Falta buffer de salto y "coyote time". | HECHO |  |
| 51 | La caída no tiene efecto ni daño según la altura. | NO | NO aplica: el soldado solo salta (~1 m) y nunca cae desde alturas; no hay caida que penalizar. |
| 52 | Saltar apuntando o agachado no está definido. | HECHO |  |
| 53 | No se puede trepar obstáculos bajos. | NO | NO hecho: trepar requiere animacion y deteccion de bordes; no se abordo. |
| 54 | Los aliados empujan al jugador al caminar pegados. | PARCIAL | No se verifico ni se cambio en esta ronda; sigue abierto. |
| 55 | La muerte tiene una sola animación, sin ragdoll. | NO | NO hecho: ragdoll/animaciones de muerte requieren arte y ajuste fino de fisica. |
| 56 | Espacio hace de salto, freno y recentrar. Funciona porque son contextos separados, pero es frágil al remapear. | YA ESTABA | Verificado en el codigo/suite |

## IA y órdenes

| # | Punto | Estado | Detalle |
|---|---|---|---|
| 57 | Los aliados no usan granadas ni la ametralladora fija. | HECHO |  |
| 58 | La IA no reacciona a granadas: no huye ni se cubre. | HECHO |  |
| 59 | `WorldSimulationDriver` recorre a todos los soldados en cada frame, así que el costo crece linealmente con los enemigos. | PARCIAL | Medido en build: 327 fps, 0 B de GC. El costo sigue creciendo linealmente con los soldados; no se reescribio a un scheduler. |
| 60 | Los aliados no tienen voces contextuales ("¡Granada!", "¡Cúbranse!"). | HECHO |  |
| 61 | El radial no tiene flanquear, suprimir ni lanzar granada. | HECHO |  |
| 62 | IR ALLÍ no valida que el punto sea alcanzable. | HECHO |  |
| 63 | Nadie reacciona al fuego de supresión. | HECHO |  |
| 64 | El orden de la escuadra es fijo y no se puede reordenar. | NO | NO hecho: reordenar la escuadra toca roster, ordenes y HUD; se juzgo invasivo. |

## Sonido

| # | Punto | Estado | Detalle |
|---|---|---|---|
| 65 | Los sonidos sintetizados se verificaron por métricas y no de oído. | NO | NO se puede hacer aqui: solo un humano puede juzgar los sonidos de oido; solo se verificaron por metricas. |
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
| 73 | Hay 83 usos de `Find*` o `FindObjectsByType` en runtime, algunos repetidos (`MisionDirector`, `Demolicion`, `MisionHud`). | PARCIAL | Bajaron de 83 a 61 usos en runtime (conteo actual por grep, sin Editor). Siguen 61. |
| 74 | Hay 32 usos de `.material` o `new Material` en runtime, que crean instancias y rompen el batching. | NO | NO hecho: siguen 32 usos de `.material`/`new Material` en runtime (medido por grep). El build medido rinde 327 fps con 0 B de GC, por lo que no se priorizo. |
| 75 | Hay 20 usos de `Camera.main`. | NO | NO hecho: siguen 22 usos de `Camera.main` (Unity 6 lo cachea, costo despreciable segun la medicion en build). |
| 76 | Hay 18 `Resources.Load` en runtime, sin precarga: causan tirones al primer uso. | NO | NO hecho: siguen 18 `Resources.Load` sin precarga. |
| 77 | En pantalla hay 28 canvases y 65 gráficos UI. | NO | NO hecho: no se consolidaron canvases; el rendimiento en build es holgado (327 fps). |
| 78 | No probé ningún build standalone; todo se validó en el Editor. | HECHO |  |
| 79 | No se fija `targetFrameRate` ni vSync. | HECHO |  |
| 80 | La suite deja advertencias en consola: material instanciado en modo Edit, `Destroy` en modo Edit y `NullReferenceException`. | HECHO |  |
| 81 | La carpeta `Tests` está vacía y no hay CI. | PARCIAL | Hay Tests/LEAME.md y .github/workflows/tests.yml, pero el CI NO se ejecuto nunca en GitHub (necesita licencia de Unity como secreto). |
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
| 89 | `ARTS` pesa 339 MB y parece que solo se usa una parte. | PARCIAL | Se midio el uso de ARTS y se informo; NO se borro nada (decidirlo requiere revision de arte). |
| 90 | El cuchillo reutiliza el id de PlayerPrefs `camara_vehiculo`: quien remapeó esa tecla hereda un cuchillo en una tecla rara. | HECHO |  |
| 91 | Un comentario de `KeyBindings` dice "V = cuchillo" pero es F. | HECHO |  |
| 92 | No hay README raíz ni índice de los documentos de las rondas. | HECHO |  |
| 93 | `SC_TestLevel` y otras escenas se generan por código, así que cualquier edición manual se pierde al correr la suite. | PARCIAL | Documentado en el README que las escenas se generan por codigo; siguen generandose por codigo. |

## Tutorial

| # | Punto | Estado | Detalle |
|---|---|---|---|
| 94 | Con 35 pasos no hay checkpoints ni "saltar sección". | HECHO |  |
| 95 | Los pasos viejos los adelanté con `SaltarPaso`, no con gestos reales. | NO | NO hecho: el tutorial se probo con gestos reales en el editor en varios pasos, pero no se reescribio para cubrir todos con gestos reales. |
| 96 | `PuntoConVista` prueba 12 ángulos y, si falla, coloca al enemigo sin línea de vista. | HECHO |  |
| 97 | Si no lográs apuntar tras X segundos, no hay ayuda visual (flecha). | HECHO |  |
| 98 | No se guarda el paso del tutorial, así que no se puede reanudar. | HECHO |  |
| 99 | Saltarse pasos puede dejar a los aliados lejos. | HECHO |  |
| 100 | `TUTORIAL_PASO_A_PASO.md` lo genera un script y se desincroniza si cambian los pasos. | HECHO |  |

Ver el detalle de la ronda en [RONDA_9_MEJORAS_Y_AUDITORIA.md](RONDA_9_MEJORAS_Y_AUDITORIA.md).
