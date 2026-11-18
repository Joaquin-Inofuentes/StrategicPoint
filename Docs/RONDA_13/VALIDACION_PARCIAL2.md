# Validación contra los criterios del Parcial 2 (Producción de Recursos Digitales)

Fuente: `Parcial 2 - Criterios.pdf` (prof. Andrea Müller) y `TP2 - 2024.pdf` (consigna: build jugable, menú de inicio y pausa, playtesting).
Estado: **auditoría de código y archivos** del 2026-11-16 (antes de la Ronda 13). La parte visual se confirma con las capturas ANTES/DESPUÉS.
Leyenda: OK = evidencia en repo · RIESGO = hay que resolver · VERIFICAR = necesita captura/playtest.

## A. Desaprobación automática (lo más importante)
| Criterio | Estado | Evidencia / acción |
|---|---|---|
| Build con el arte integrado al gameplay | OK | `Builds/Windows64` + RAR; el arte vive en `SC_Operacion*`. |
| Sin bugs que impidan jugar de principio a fin | VERIFICAR | Correr la partida completa en la build final (hay `PruebaEnPlayer`/checks; falta corrida de punta a punta con la build nueva). |
| Ningún elemento en blocking/placeholder | **RIESGO** | Checker de la plaza (T15), cuadrado gris obj. 4 (T13), primitivas sin detalle (T16). En el build hay 3 escenas `SC_Operacion*` + `SC_TestLevel`; las `SC_Blockout_*` NO están en el build (OK). Revisar `SC_TestLevel` y `SC_Gameplay` (no deben ser accesibles). `ImpactFx/SpriteFx/DecalPool/MusicDirector` mencionan "placeholder": revisar qué muestran. |
| Sin elementos descargados de internet ni hechos con IA | **RIESGO** | Fuentes `BlackOpsOne` y `StardosStencil` (Google Fonts, descargadas): confirmar con la docente si valen o reemplazar por fuente propia. `Assets/_Project/Audio_GunshotCandidates/*.wav` (no se referencia desde scripts: sacar del proyecto entregado o documentar origen). Las referencias de fotos de la plaza (T15) se usan SOLO como referencia, no se descargan texturas. |

## B. Pautas de presentación (entregables en el .rar)
| Requisito | Estado | Acción |
|---|---|---|
| `.rar` con el nombre del proyecto | OK | `StrategicPoint.rar` (el nombre del proyecto es "StrategicPoint"). |
| `.txt` con integrantes y roles | **FALTA** | Crear `INTEGRANTES.txt` (necesito nombres y roles del equipo). |
| `.txt` con atajos de testeo | **FALTA** | Crear `ATAJOS_DE_TESTEO.txt` leyendo `KeyBindings.cs`, `MenuDeTrampas.cs`, `ModoDios.cs`; la build debe poder jugarse sin ellos. |
| Build | OK | |
| Archivos extra (FBX/Maya) | VERIFICAR | Preguntar si los pidieron; fuentes `.mb` en `Assets/ARTS`. |
| No recompilar durante la evaluación | n/a | Entregar RAR cerrado. |

## C. Por bloque de criterios
- **Dirección de arte:** estilo cúbico low-poly definido; paleta/iluminación nocturna (`AjustesDeLuzNov08`, `NightLightingBuilder`) → VERIFICAR coherencia y que la luz no dificulte la lectura (T14/T15/T16).
- **Modelado y assets:** modularidad (`OperacionBuilder.*`, módulos Maya); repetición y encaje sin errores de snapeo → T11/T17 (barrido de solapes).
- **Texturizado/PBR:** solo los árboles de `ARTS` traen Normal; el resto son materiales de color/procedurales (`ArteProcedural`). RIESGO medio: incorporar al menos roughness/normal en los assets héroe (camión nuevo, plaza).
- **Ambientación:** ciudad/desierto relleno y árboles (T14); distinguir gameplay vs decoración (rombos, anillos, colores de interactuables) → VERIFICAR; post-proceso/bloom: existe `PostFxDirector` → VERIFICAR que el emissive resalte.
- **Feedback de interacciones:** revivir con un solo engranaje (T5), cuchillo con sangre y sonido (T1), liberación de celdas (T6), indicador de disparos del tanque (T4) → cubren "feedback visual + sonoro". Rendimiento de partículas: medir (T14, T1).
- **Presentación y prolijidad:** objetos flotantes/atravesados y duplicados → T11/T17; smoothing groups → VERIFICAR en modelos propios.
- **UI:** menú principal y pausa existen; settings con sliders de daño (T3). **RIESGO:** 50 scripts de UI siguen cayendo en la fuente por defecto de Unity (`LegacyRuntime.ttf`) como respaldo, p. ej. `CartelVolverView.cs`; el criterio pide fuente personalizada en los textos del gameplay → unificar todo en `FuentesBelicas`.
- **Personajes y animación:** soldados con rig humano, clases distintas; revisar errores de rig/skin y transiciones (hay trabajos previos en Ronda 11, ver puntos 5–7 de ese plan) → VERIFICAR.
- **Cinemáticas/cámaras:** hay intro, rapel, huida; se suma la cinemática de celdas (T6) con transiciones suaves, SFX y VFX.

## D. Trabajos nuevos que salen de esta validación
- **T21** `INTEGRANTES.txt` y `ATAJOS_DE_TESTEO.txt` dentro del RAR (se generan en el paso de empaquetado).
- **T22** Unificar fuentes del gameplay en `FuentesBelicas` (eliminar el respaldo `LegacyRuntime` donde se pueda).
- **T23** Origen del audio/fuentes: documentar o quitar `Audio_GunshotCandidates` y confirmar las fuentes TTF con la docente.
- **T24** Corrida de punta a punta de la build final (menú → victoria) como prueba de "sin bugs bloqueantes".
