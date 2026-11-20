# T18 · Tabla contra VALIDACION_PARCIAL2.md (borrador del carril F, 2026-11-16)

Estado: OK = evidencia en archivo; RIESGO = abierto; VERIFICAR = falta captura/corrida. Sin cambios al proyecto (solo lectura).

## A. Desaprobación automática
| Criterio | Estado | Evidencia de archivo / nota |
|---|---|---|
| Build con el arte integrado | OK (a confirmar en la build final) | `ProjectSettings/EditorBuildSettings.asset`: 6 escenas (`SC_MainMenu`, `SC_Gameplay`, `SC_TestLevel`, `SC_Tutorial`, `SC_Loading`, `SC_Operacion`). **Corrección a VALIDACION**: no hay «3 `SC_Operacion*`», hay una sola `SC_Operacion`. Ninguna `SC_Blockout_*` en la lista. |
| Sin bugs que impidan jugar | VERIFICAR | Depende de T24 (`Bug988a` + corrida manual). Sin evidencia nueva. |
| Nada en blocking/placeholder | **RIESGO** | (a) **`SC_Gameplay` es alcanzable desde el menú**: `MainMenuController.cs:196-200` (`ElegirNivel(1)` → `MostrarDificultad`) → `IniciarPartida` → `SceneLoader.Cargar("SC_Gameplay")` (líneas 331-337). Es el «Nivel 1» (misión vieja). Hay que revisar que se vea terminado, o esconderlo. (b) Niveles 3 y 4: `NivelDisponible = {true,true,false,false}` (línea 130) muestran «en desarrollo» (`MostrarEnDesarrollo`): visible como placeholder. (c) `SC_TestLevel` no la carga ningún código de juego (solo comentarios); `-spmetrics` carga `SC_Gameplay` solo con flag (`MetricasDeBuild.cs:48`). (d) `ImpactFx.cs:321`, `SpriteFx.cs:6`, `DecalPool.cs:222`: la palabra «placeholder» está solo en comentarios que dicen que **ya se reemplazó** por sprites reales. `MusicDirector.cs:259` también es comentario. Pendientes de T13/T15/T16 (cuadrado gris obj. 4, plaza, primitivas): no auditados aquí. |
| Sin descargado de internet / hecho con IA | **RIESGO ALTO** | Ver `INVENTARIO_ORIGEN.md`. Hallazgos nuevos respecto de VALIDACION: **(1) 61 FBX de animación de Mixamo** (`ARTS/Slim Shooter Pack`), sin crédito; **(2) música `SA_Calma/SA_Accion.mp3` sin origen**, y es la que suena; **(3) ~60 clips de audio descargados** (Freesound/OGA/Kenney/itch), según `Resources/Audio/CREDITS.txt`; **(4)** iconos game-icons.net y Kenney particles; **(5)** `ARTS/SP_Arte/IMPLEMENTACION_CLAUDE.md`: modelado en Maya «vía commandPort», podría leerse como hecho con IA; **(6)** `Menu_Fondo.png` sin autor. `Audio_GunshotCandidates` tiene 0 referencias por GUID (verificado): sacarla del proyecto. Fuentes OFL: licencia presente en `Resources/UI/Fuentes/OFL.txt` y `CREDITOS.txt`. |

## B. Entregables
| Requisito | Estado | Evidencia |
|---|---|---|
| `.rar` con el nombre del proyecto | OK (por generar) | T20: `Builds\StrategicPoint.rar`. |
| `.txt` integrantes y roles | OK | `Docs/RONDA_13/entrega/INTEGRANTES.txt` (existe; contiene 3 roles de personajes del juego: Vega Médico, Kes Francotirador, Doc Asalto; **ojo: son nombres/roles de la escuadra del juego, no de las personas del equipo**; confirmar con el dueño si es lo que pide la docente). |
| `.txt` atajos de testeo | OK | `entrega/ATAJOS_DE_TESTEO.txt` (nuevo, a partir de `KeyBindings.cs`, `MenuDeTrampas.cs`, `ModoDios.cs`, `PlayerInputDriver.cs`, `Loc.cs`, `TutorialManager.cs`, `SesionLog.cs`). |
| Build | OK (a rehacer con CliBuilder) | |
| Archivos extra (FBX/Maya) | VERIFICAR | Fuentes en `Assets/ARTS/SP_Arte/_FBX_Export` y `MY_*.mb`. Preguntar a la docente si se piden. |
| No recompilar durante la evaluación | n/a | |
| Créditos/atribuciones de assets | **FALTA (nuevo)** | No existe `ORIGEN_DE_RECURSOS.txt` en `entrega/`; CC-BY exige atribución. |

## C. Por bloque
| Bloque | Estado | Evidencia |
|---|---|---|
| Dirección de arte | VERIFICAR | `NightLightingBuilder.cs`, `PostFxDirector`; falta captura `plaza_ojo`. |
| Modelado y assets | OK / VERIFICAR | Módulos propios (`ARTS/SP_Arte/_FBX_Export`, 107 usados según `Docs/ARTS_USO.csv`); barrido de solapes T11/T17. |
| Texturizado / PBR | RIESGO medio | Hay normales procedurales (`T_Op_*_N.png`, 11 materiales con `_N`) y Normal/Mask de árboles, barril, barricada, soldado en `ARTS/SP_Arte`. Camión y plaza: pendientes T15/T16. |
| Ambientación | VERIFICAR | T14; `PostFxDirector`. |
| Feedback de interacciones | VERIFICAR | T1, T4, T5, T6; los efectos de impacto ya usan sprites (`ImpactFx/SpriteFx/DecalPool`). |
| Presentación y prolijidad | VERIFICAR | T11/T17. |
| UI: fuente personalizada | OK en gran parte | `grep LegacyRuntime` en runtime (sin Editor): **1 aparición**, `FuentesBelicas.cs:29` (el respaldo). VALIDACION decía 50 scripts; T22 parece ya aplicado. Falta `Bug987a` y la medida en cuadro real. IMGUI de depuración (`MenuDeTrampas`, `SesionLog`/`ReporteDeBug`) usa la fuente por defecto: solo aparece con atajos. |
| Personajes y animación | VERIFICAR | `pesado_comparacion`; las animaciones son de Mixamo (ver A). |
| Cinemáticas | VERIFICAR | `CinematicaDeOperacion`, T6. |

## D. Riesgos extra hallados (no estaban en VALIDACION)
1. **Reporte de bug a Google Sheets en la build**: `SubidaDeBug.cs:38` tiene una URL de Apps Script y `SubeAlSheet => !Application.isEditor` (línea 69), `Habilitada = true`. En el exe del RAR, la tecla **U o 8** abre el diálogo de reporte y sube datos al Sheet. Para la entrega: deshabilitar o avisar; además la **tecla 8 abre el diálogo** en gameplay (excepto con el radial abierto).
2. **Atajos activos en la build** sin protección (F4 modo dios, F10 trampas, F9 matar): conocido; documentados en `ATAJOS_DE_TESTEO.txt`. Colisión de F10 en la Operación: menú de trampas + `HudPulido` alterna el HUD mínimo a la vez (`HudPulido.cs:64`).
3. `Assets/_Recovery` (10 `.unity`, 102 MB) dentro de `Assets`: escenas de recuperación que viajan con el proyecto aunque no estén en la build; conviene sacarlas si se entrega el proyecto.
4. `INTEGRANTES.txt`: ver nota en B.
