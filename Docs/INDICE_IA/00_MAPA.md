# Mapa del proyecto — indice para IA

> Generado por `Tools/Indice/indexar_ia.py` el 2026-09-20 07:30 UTC.
> No editar a mano: se regenera. Si algo esta mal, esta mal el generador.

**221 scripts · 58,195 lineas · 5 escenas · 132 prefabs**

## Como leer este indice

| Archivo | Para contestar |
|---|---|
| [`01_archivos.md`](01_archivos.md) | "¿donde vive X?" — cada script con su subsistema y que hace |
| [`02_simbolos.md`](02_simbolos.md) | "¿que puedo llamar?" — clases, metodos publicos y propiedades |
| [`03_dependencias.md`](03_dependencias.md) | "¿si toco esto, que se rompe?" — grafo entre subsistemas |
| [`04_eventos.md`](04_eventos.md) | "¿quien se entera de esto?" — publicadores y oyentes del bus |
| [`05_entrada.md`](05_entrada.md) | "¿que hace esta tecla?" — mapa completo de input |
| [`06_escenas.md`](06_escenas.md) | "¿que hay en el mundo?" — escenas, prefabs, materiales, shaders |
| [`07_pruebas.md`](07_pruebas.md) | "¿esto esta cubierto?" — fases de la suite y sus checks |
| [`08_estaticos.md`](08_estaticos.md) | "¿esto sobrevive a la partida?" — estado estatico mutable |
| [`indice.json`](indice.json) | lo mismo, para consultar con codigo |

## La arquitectura en un parrafo

El juego es una simulacion con **un solo camino**: `SP.Ai.WorldSimulationDriver.Step(dt)`. Ese metodo
—y nada mas— avanza la IA, las armas, la vida, los vehiculos y las torretas. El juego real lo llama
desde `Update()`; la suite headless lo llama a mano con un reloj simulado. Todo lo que quiera correr en
los dos lados tiene que entrar por ahi: un `Update()` propio queda fuera de las pruebas.

Alrededor hay tres capas que no deciden nada:
- **Registros** (`Core`): `ActorRegistry`, `WorldSystemsRegistry`, `SpatialGrid`, `NavService`.
  Contestan "que existe y donde esta" sin barrer la escena.
- **Bus de eventos** (`Core/EventBus`): los emisores no conocen a los oyentes. Ver `04_eventos.md`.
- **Presentacion** (`Presentation`, `UI`): sin logica de juego, solo escucha el bus y dibuja.

La entrada del jugador (`Player/PlayerInputDriver`) **traduce**, no decide: llama a los mismos metodos
que la IA y que las pruebas.

## Subsistemas

| Subsistema | Scripts | Lineas | Que hace |
|---|---|---|---|
| **Presentation** | 52 | 10745 | Todo lo que se ve y se oye en el mundo: VFX, audio, marcadores, pools visuales. |
| **UI** | 40 | 6912 | HUD y pantallas: mira, roster, minimapa, radial, menus, ajustes. |
| **Editor** | 30 | 13984 | Herramientas de Editor: suite headless, constructores de escena, pipelines de arte. |
| **Core** | 22 | 3608 | Servicios sin escena: bus de eventos, registros, grilla espacial, navegacion, pools, idioma, dificultad. |
| **Player** | 21 | 7720 | Traduccion de intencion a ordenes: input, posesion, seleccion, ordenes. |
| **Combat** | 12 | 2438 | Vida, dano, armas, proyectiles y catalogo. |
| **Tutorial** | 12 | 4480 | Modulo de ensenanza y su reproductor automatico. |
| **Vehicles** | 10 | 2317 | Tanque, asientos, torretas y ametralladoras fijas. |
| **Ai** | 9 | 2709 | Cerebro de una unidad no poseida y el driver que avanza la simulacion. |
| **Actors** | 4 | 612 | El soldado: identidad, piezas, motor de movimiento y aspecto. |
| **Mision** | 4 | 1083 | La partida: objetivos, oleadas, rehen, helicoptero, cinematica final. |
| **Camera** | 2 | 869 | Rig de camara: hombro, RTS, transiciones, sacudidas, zoom. |
| **Otros** | 1 | 17 |  |
| **Demo** | 1 | 669 | Corredor de demo automatica. |
| **Interaction** | 1 | 32 | Contrato de lo interactuable. |

## Los 12 archivos mas grandes

| Archivo | Lineas | Subsistema | Que hace |
|---|---|---|---|
| `Assets/_Project/Scripts/Editor/HeadlessTestRunner.cs` | 4195 | Editor | Construye el entorno de prueba (suelo, obstáculos, prefabs, 3 soldados, cámara, UI, pool d… |
| `Assets/_Project/Scripts/Player/PlayerInputDriver.cs` | 3470 | Player | Traduce teclado/ratón reales a los mismos métodos que usa el test automático. No decide na… |
| `Assets/_Project/Scripts/Tutorial/TutorialManager.cs` | 1880 | Tutorial | Modulo de tutorial. Recorre 35 pasos en orden; cada paso tiene sub-pasos y cada sub-paso u… |
| `Assets/_Project/Scripts/Editor/HeadlessTestRunner.Fases8a12.cs` | 1546 | Editor | HeadlessTestRunner (parte): fases 8 a 12 de la suite. |
| `Assets/_Project/Scripts/Ai/AiBrain.cs` | 1233 | Ai | Postura de combate de una unidad. Libre es el comportamiento historico y por defecto: las … |
| `Assets/_Project/Scripts/Editor/ArtSetup.cs` | 891 | Editor | Pipeline de importacion del arte de Assets/ARTS. Es una herramienta y no un README con pas… |
| `Assets/_Project/Scripts/Editor/WorldArtPipeline.cs` | 865 | Editor | Pipeline del pack de arte nuevo (Assets/ARTS/SP_Arte/_FBX_Export): ~90 mallas modulares/am… |
| `Assets/_Project/Scripts/Combat/Projectile.cs` | 839 | Combat | Viaja, comprueba su propio impacto por distancia (sin física) y se devuelve solo al pool. … |
| `Assets/_Project/Scripts/Camera/CameraRig.cs` | 823 | Camera | Posee la cámara y delega su posición en el modo activo (FPS u RTS). |
| `Assets/_Project/Scripts/Editor/ArtBuilder.cs` | 785 | Editor | Segunda mitad del pipeline de arte: con los FBX ya importados y materializados por ArtSetu… |
| `Assets/_Project/Scripts/Combat/WeaponHolder.cs` | 735 | Combat | Un solo evento para "este soldado tiene otra arma puesta", sin importar el camino (recogid… |
| `Assets/_Project/Scripts/Core/WaypointGraph.cs` | 696 | Core | ITEM 226 -- Grafo de waypoints (grilla + A* sobre el plano XZ). POR QUE EXISTE (y por que … |

## Documentos de contexto que NO genera este indice

| Documento | Que tiene |
|---|---|
| `Docs/RONDA_10_BUGS.md` | Los 100 bugs abiertos, con archivo y linea |
| `Docs/RONDA_10_PLAN_CORRECCION.md` | Como se arregla cada uno y como se verifica |
| `Docs/RONDA_10_TUTORIAL_22_PASOS_NUEVOS.md` | Los 22 pasos nuevos del tutorial |
| `Docs/AUDITORIA_100_ITEMS.md` | Estado de los 100 puntos de la auditoria original |
| `Docs/TUTORIAL.md` | Como esta armado el tutorial, paso por paso |
| `Tools/Qa/qa_total.bat` | Corrida de QA: capturas, metricas y logs de todo tipo |
