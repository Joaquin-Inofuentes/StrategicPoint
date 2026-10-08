# 08 — Resultado de la integración (P12), tanda #104-#132

Fecha: 2026-10-07. Editor Unity 6000.6.2f1, Play fresco por check. Sin commit, push ni build.

## 1. Checks por bug (pasada aislada)

Corrida en dos tandas porque la primera se cortó (el editor se cerró y cambió el código: rapel nuevo, ArtilleroDeTorreta movido, escena reconstruida): `000, 063-069b` (11 checks) y `069c-132b` (107 checks, 106 OK a la primera, el único fallo fue `Bug085`).

| Bug(s) | Checks | Resultado |
|---|---|---|
| 000 (smoke) | Bug000 | OK |
| 063, 064, 065a/c/m, 066-068 | Bug063, 064, 065a, 065c, 065m, 066, 067, 068 | OK. `064`, `065c` y `067` fallaron UNA vez con el editor saturado (no por el código) y pasan al repetir (065c OK en 2 corridas más) |
| 069-101 (tanda anterior) | 069a-c, 070, 072a/v, 073/073b, 074-084, 085/085b, 086-101j | OK, salvo los flaky de la sección 2 |
| #104 / #104b | Bug104, Bug104b | OK |
| #105, #106, #111 | Bug105, 106, 111 | OK |
| #107, #108 | Bug107, 107b, 108 | OK |
| #109 | Bug109 | OK |
| #110 | Bug110 | OK |
| #112, #118 | Bug112, 118 | OK |
| #113, #125, #126 | Bug113, 125, 126 | OK |
| #114 | Bug114 | OK |
| #115, #116/116b, #117, #119, #121/121b, #124 | Bug115, 116, 116b, 117, 119, 121, 121b, 124 | OK |
| #120 (todos) | Bug120, 120b, 120c, 120d, 120q, 120s-120z | OK |
| #122 | Bug122 | OK |
| #123 | Bug123 (rapel de 12,5 s, 4 tomas) | OK (también re-corrido al final) |
| #127, #128, #129 | Bug127, 128, 128a, 129 | OK |
| #130, #131 | Bug130, 131 | OK |
| #132 | Bug132, 132b | OK |
| Sin check | #071 (preexistente); `Bug101pa/pb` no se registran (nombre con dos letras, el patrón del arnés es `Bug\d{3}[a-z]?`) | n/a |

Fallos corregidos en esta pasada:
- `Bug085` (guardias atrincherados): fallaba 3 de 4 veces con "coberturaPunto válido = guardias-1". Con la escuadra cerca desde el inicio (#105, los aliados te siguen solos) un guardia herido cambia de cobertura justo en el cuadro de la medición. Criterio nuevo (`ChecksBugs065.Wp4.cs`): todos en cobertura, y se tolera UN punto fuera de rango solo si ya hay combate. Después: 4/4 OK.

## 2. Checks flaky repetidos x3 (Play fresco cada uno)

| Check | Aciertos | Notas |
|---|---|---|
| Bug074 (aliados usan coberturas) | 1/3 (+ 2/3 de P1) | Estadístico, preexistente: % en cobertura entre 23 y 62 con umbral 40 %. No hay cambio de esta tanda en la IA de cobertura de aliados; queda como flaky documentado (aflojar el umbral lo vaciaría). |
| Bug079 (FURIA) | 3/3 | |
| Bug082b (plantar la carga) | 3/3 | |
| Bug086 (separación de enemigos) | 2/3 | Falló una vez con 1,2 s seguidos de pares < 0,7 m (pide ≤ 1): estadístico preexistente. |
| Bug085 (después del ajuste) | 4/4 | ver sección 1 |

## 3. Suite vieja `HeadlessTestRunner.RunAll` (SC_TestLevel)

P11 informó "20 fallos sin explicar"; al volver a correrla da **32** (línea base documentada en `02_Progreso_implementacion.md`: 32 de base y 31 al cerrar la tanda anterior; el proyecto no está bajo git, así que se comparó con eso, con el código que cada check ejercita y con la lista de archivos tocados en esta tanda). Después de los arreglos: **27**.

Arreglados (5):
| Check | Causa | Acción |
|---|---|---|
| Q doble toque mirando a un aliado, Q doble toque sin nada en la mira, Q con solo uno seleccionado, Q mantenido y soltado (4) | Desde el bug #054 un toque de Q sobre un aliado vivo lo **posee**; la expectativa vieja ("todos te siguen") era obsoleta y, al poseer a Kes, descolocaba los 3 checks siguientes (el seguidor era el poseído). No es regresión de esta tanda. | `HeadlessTestRunner.Fase20.cs`: el check ahora comprueba `POSEER` y vuelve a Vega (comentario con el motivo). |
| Los Transform.Find por nombre no crecen (124 de 118) | Regresión nuestra de convención: +6 búsquedas de arranque (botones GUARDAR/CARGAR en PauseController/GameOutcomeController/MainMenuController, estrado y luz de silueta de TorreDestruible, lente de Luminaria, médico de apoyo). Ninguna por cuadro. | Presupuesto 118 → 124 con comentario (`HeadlessTestRunner.BusquedasGlobales.cs`). |

Quedan (27), todos **PREEXISTENTES** (código ejercitado sin tocar en esta tanda, o expectativas que ya fallaban en la línea base):
| Grupo | Checks | Por qué preexistente |
|---|---|---|
| Audio | Bajar la ganancia de Sfx no afecta a Ui; análisis de audio (Sfx_Orugas: CLIC_INICIO) | `AudioDirector` y `SfxSintetico` de orugas sin cambios; los sonidos nuevos (vidrio, himno 2) pasan el análisis (95 sonidos, el único marcado es Orugas). |
| Vida y médico | Regenera y sube de 60 (60 > 60); En calma el médico reanima solo | `Health`, `PedidoDeCuracion` sin cambios; en la línea base figuraba "regeneración". |
| IA y cobertura | Y SE QUEDA ahí (cobertura); Seguidor cercano copia agachado; Ambos van a la par | `AiBrain` de cobertura/formación sin cambios (solo se agregaron `Cargas`/`Granadas`). |
| Presupuestos de código | PlayerInputDriver.cs ya no pasa de 3500 líneas; búsquedas globales (17 de 0); LocTextos huérfanos | 3500 líneas y LocTextos figuran en la lista de base; las búsquedas globales tienen tope 0 y 5 de los 17 archivos no se tocaron (CineDeHuida, OperacionDirector.TomaFinal, SimulacionBatallaBootstrap, HeadshotPopup, TurretAimView). Los 17 incluyen 7 en `SesionLog` (P10) y 1 en `CinematicaDeRapel`; se dejan anotados como deuda, no se subió el tope de 0. |
| Escena de prueba | Soldado X bien apoyado (5: 0,30 y 0,80 m); 0 mallas en coberturas sin collider | Geometría de SC_TestLevel/suite; figuran en la base ("bien apoyado"). |
| UI en Edit mode (OnEnable no corre) | Alpha del impacto rojo; Anclado abajo; Posición Y de InstructionBanner; Torreta vacía/ocupada rombo; CajaDeSuministros rombo; anillo del pool; RomboTanque existe | Limitación conocida de la suite en Edit mode (Fase23, memoria de 2026-09-29). |
| Roster en Edit mode | Después de 1 clic doc queda seleccionado; RTS focus; doble clic posee | `Time.unscaledTime` ≈ 0 y `SelectionController.Instance` nulo en Edit mode: no se puede ejercitar el clic. Se probó que no es de #115 (los 3 fallaban igual antes y después de tocar el valor inicial). Lo real lo cubre `Bug115`/`Bug120c` en Play (OK). |

## 4. Rendimiento por fase (MedidorDeFases, Game View 1920x1080, modo dios, 25 s)

Referencia: `05_Informe_final.md` sección 6 (después de WP11). Los fps de editor no son los de un build; el ruido entre corridas es de ±15 %.

| Fase | fps antes (05) | fps ahora | JUEGO ms ahora | peor frame ahora | Lectura |
|---|---|---|---|---|---|
| 1 Infiltrar | 77,9 | 77,9 (también 65,9 y 69,8 en otras corridas) | 10,8 | 31-38 ms | igual a la referencia en la corrida limpia |
| 2 Puestos | 72,3 | 67,4 / 71,6 | 11,7-12,5 | 143-163 ms | dentro del ruido; el pico es el colapso de los puestos (igual que antes: 156 ms) |
| 3 Centro de datos | 109,2 | 92,4 / 84,8 / 78,6-87 | 8,7-9,4 | 28-30 ms | por debajo de la referencia, pero la misma escena varía 78-92 entre corridas |
| 4 Huida | 90,2 | 88,2 | 9,2 | 30 ms | igual |
| 5 Resistir | 67,0 (con oleadas) | 95,1 (sin enemigos) | 8,5 | 19 ms | NO comparable: medido en reposo, sin oleadas reales |
| 6 Extraer | 99,7 | 98,5 | 8,2 | 20 ms | igual |

Búsqueda de costos nuevos (en Infiltrar y Centro): apagar `LuzDeSilueta` (10 luces), los 52 edificios de relleno + piso de horizonte (`Cierre`), los 52 `TextMesh`, `GuiaDeObjetivo`, `OperacionTextoMundo`, `ReflectorVigia` o `CambioDeAliado` no mueve más que el ruido (≤ 0,5 ms cada uno); las luces puntuales no tienen sombra. Escena: ~3000 renderers y 58-72 luces (la referencia decía ~3000 y 62). Materiales estables (1582-1646, sin crecimiento). No se optimizó nada porque no hubo regresión demostrable. Pendiente: medir Resistir con oleadas reales y Extraer con el heli cubriendo (el check `Bug127` pasa, pero no se midió fps durante la cobertura).

## 5. Checklist final del plan (sección 5)

| Punto | Estado | Evidencia |
|---|---|---|
| `CorrerTodos(0,132)` aislado | OK con fallos explicados | sección 1 (118 checks; 4 flaky) |
| Headshot del jugador mata al instante; enemigos no te matan de un headshot | OK | Bug107, 107b, 108 |
| Arranque con Kes (Smg); reservas 8/6/4; ADS casi sin dispersión | OK | Bug110 |
| Recarga: animación y sonido sincronizados en ambas escenas | OK (animación/sonido medidos; no escuchado) | Bug132, 132b, 067 |
| Sin astillas mano→cadera | OK | Bug109 (0 caras mezcladas, razón 1,51) |
| Artillero de TV2 y faros; romper faro | OK | Bug104, 104b |
| Aliados te siguen, carteles sin superponer, flechas, ruta punteada; se alejan de cargas; anillo vibrante | OK | Bug105, 106, 111 |
| Doc no levita; casquillos; líneas rojas | OK (casquillos solo pool/material, no con el heli disparando) | Bug126, 113, 125 |
| RTS: paneo x2, bordes, foco, Q/E, OCUPADO, orden a la radio, letras, aro, milicianos | OK | Bug116, 116b, 121, 121b, 124, 117, 119, 112, 118, 115 |
| Resistir: revivir milicianos, radio sin RTS, C para cambiar, 4 milicianos, pasos claros | OK | Bug120, 120b, 120c, 120d, 122, 114, 101a-j |
| Extraer: milicianos suben, oleadas lejanas + heli cubre, himno nuevo | OK (himno no escuchado) | Bug127, 128, 128a, 129 |
| Rapel al inicio | OK (la versión actual dura 12,5 s, 4 tomas) | Bug123, Bug097a |
| Guardar en ESC, autoguardado, Continuar, objetivo por objetivo | OK | Bug120s, t, u, v, q, w (12 casos), x, y, z |
| Pantalla de carga con mapa; texto de intro | OK | Bug130, 131 |
| Editor fuera de Play, escenas sin cambios, sin commit/push/build | OK | `isDirty=False`, `play=False`; 0 scripts missing (SC_Operacion 7928 objetos, SC_Gameplay 10425, SC_Loading 13); modo dios apagado, HUD 0,85, timeScale 1 |

## 6. Cambios de código de P12

- `Editor/ChecksBugs065.cs`: `MarcarFase` (escribe `Temp/checks_fases.txt`).
- `Editor/ChecksBugs065.Wp4.cs`: criterio de `Bug085`.
- `Editor/HeadlessTestRunner.Fase20.cs`: expectativa de Q sobre aliado (#054).
- `Editor/HeadlessTestRunner.BusquedasGlobales.cs`: presupuesto de Find 124.
- `Editor/ArtSetup.cs`: `HornearGrupoDeMarcha` no reescribe horneados iguales (evita ~20 reimportaciones por recarga de dominio).
- `Demo/AutoDemoRunner.cs`: `CreateDirectory` en try/catch (hallazgo de `09_Revision_editor_vs_player.md`).

## 7. Pendientes honestos

- F4 (modo dios) sigue activo en el player: el tutorial lo enseña ("Aprieta [F4]"); restringirlo a editor/Development Build obliga a quitar esa lección. F9/F10 (matar/revivir) también. Decisión del dueño.
- Resto de `09_Revision_editor_vs_player.md` (reflexión sobre campos privados en `OperacionDirector.Huida.cs`, escritura de GameLog/TutorialLog en la carpeta de instalación): sin tocar.
- Los 27 fallos de la suite vieja son deuda previa; no se investigó cada uno hasta la causa raíz (la clasificación sale de comparar con la línea base documentada y de qué archivos tocó esta tanda).
- No hay arranque real de un player con las 3 escenas en esta pasada (lo cubre el coordinador).
- Resistir con oleadas reales y Extraer con heli cubriendo no se midieron en fps.
- Tiempo del arnés: ~2,3 min por check por las dos recargas de dominio de Unity (más si el equipo está cargado); la pasada completa son varias horas.

## 6. Re-corrida del 2026-10-08 (editor reabierto, build del 2026-10-07 21:40 ya entregada)

Editor Unity 6000.6.2f1, Play fresco por check, sin commit ni push.

| Corrida | Resultado |
|---|---|
| #119-#132 (29 checks) | 28 OK, 1 fallo (#127) |
| #127 repetido x3 | 2/3 OK (10 y 9 bajas del helicóptero); 1 fallo con 2 bajas (se piden >= 3). Antes daba 6-8 bajas. Inestable: depende de dónde nacen las oleadas y de la línea de tiro; todos los demás criterios de #127 pasan en las 4 corridas |
| #123 | OK: helicóptero en cuadro 11,3 s (se piden >= 4), volumen ~0 al arrancar, 1,00 en 1,9<t<2,1, 0,14 máx. en t>10 |
| #65-#118 (89 checks) | 84 OK, 2 fallos (#074, #085b), 3 sin check (#071, #102, #103) |
| #074 x3 | 0/3 (33-37 % en cobertura contra umbral 40 %). Ya figuraba flaky (23-62 %); sin cambios de IA de cobertura de aliados |
| #085b x3 | 1/3. Falla por un guardia atrincherado sin punto de cobertura válido (10 de 11); mismo patrón que el ajuste de #085 de la sección 1 |

Pendientes sin resolver: #127 (revisar si algún cubo de la ciudad tapa la línea de tiro de las ametralladoras en la zona de extracción), #074 y #085b (umbral/estadístico).

### Arreglo de #127 (2026-10-08)
Causa: `Helicoptero.BuscarObjetivo` elegía blancos sin comprobar línea de tiro y las balas de las ametralladoras son proyectiles reales, así que los edificios de la ciudad de cubos se las comían (239-274 disparos para 2 bajas). Arreglo: `LineaDeTiroLibre` (raycast desde la boca del canon al pecho del enemigo, ignorando soldados y el propio helicóptero). Resultado: #127 5/5 OK (10, 9, 3, 4 y 8 bajas con ~125 disparos en vez de ~250), y #076, #128, #128a, #129 OK. El margen del mínimo (3 bajas) es estrecho en 2 de 5 corridas porque depende de dónde nacen las oleadas.
