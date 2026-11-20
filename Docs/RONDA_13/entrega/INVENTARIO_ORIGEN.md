# Inventario de origen y licencia de recursos (T23, borrador del carril F)

Fecha: 2026-11-16. Solo lectura del proyecto. Regla de la cátedra: **nada descargado de internet ni hecho con IA**.
Riesgo: ALTO = puede ser desaprobación automática; MEDIO = hay que declararlo o confirmarlo; BAJO = propio o procedural.

Hallazgo global: casi todo el audio y varias piezas visuales son **descargados de internet con licencia libre (CC0/CC-BY/OFL)**. La regla se puede leer de forma estricta («nada descargado») o laxa («nada sin licencia/origen»). Hay que **preguntarle a la docente** cuál aplica (D11) antes de armar el RAR. Si es la estricta, el audio y las animaciones hay que rehacerlos.

## 1. Fuentes (TTF)
| Archivo | Origen | Licencia | Uso | Riesgo |
|---|---|---|---|---|
| `Assets/_Project/Resources/UI/Fuentes/BlackOpsOne-Regular.ttf` | Google Fonts (github.com/google/fonts, ofl/blackopsone), James Grieshaber, bajada 2026-09-20 | SIL OFL 1.1 | `FuentesBelicas.Titulo` | MEDIO: descargada. Hay `OFL.txt` y `CREDITOS.txt` en la carpeta. |
| `.../StardosStencil-Bold.ttf` | Google Fonts, Vernon Adams / Stardos Stencil Project Authors | SIL OFL 1.1 | `FuentesBelicas.Texto` (HUD) | MEDIO: ídem. El `OFL.txt` es el de Black Ops One; falta el copyright propio de Stardos. |

Respaldo: `LegacyRuntime.ttf` (interna de Unity) en `FuentesBelicas.cs:29`; es la única aparición en runtime fuera de Editor (T22 ya aplicado).

## 2. Audio (`Assets/_Project/Resources/Audio`, 88 archivos de audio)
Documento: `Resources/Audio/CREDITS.txt` (cubre casi todo).
| Grupo | Origen / licencia | Riesgo |
|---|---|---|
| Sfx UI, pasos, impactos, Order, Swap, EmptyClick, GrenadeBounce, UiHover/UiClick | Kenney Impact + Interface Sounds, CC0 | MEDIO (descargado) |
| Sfx/Shot_Rifle, Pistol, Heavy, Shoot, Smg, Shotgun, Sniper | «The Free Firearm Sound Library» (OpenGameArt), grabaciones reales, CC0, recortadas | MEDIO (descargado) |
| Sfx/Death, Wounded | «Male grunt/yelling sounds» (OpenGameArt), CC0 / OGA-BY 3.0 | MEDIO; OGA-BY exige atribución |
| Sfx/OrderBark | Voicepack «AAron - Aussie Announcer» (itch.io, Azzurro): «no credit required but appreciated» | ALTO-MEDIO: no es CC0, licencia informal |
| Music/Calm.mp3, Action.mp3 | Kevin MacLeod (incompetech), **CC BY 3.0** | MEDIO: exige atribución visible; no encontré créditos dentro del juego |
| **Music/SA_Calma.mp3, SA_Accion.mp3** | **SIN ORIGEN DOCUMENTADO**: no figuran en `CREDITS.txt` ni en Docs. MP3 LAME sin etiquetas. `MusicDirector.cs:253-254` las prefiere sobre Calm/Action, o sea que **son la música que suena**. | **ALTO**: origen desconocido (descarga o IA) |
| Music/Tension.wav | Generada por script, sin material de terceros | BAJO |
| Heli/RotorReal_Freesound_qubodup.wav | Freesound (qubodup), CC0, bajado del CDN de previews | MEDIO |
| Shot_Rocket, Orugas, OrugasChirrido, CannonBody, Explosion (EOD/Voladura/Blast), Shoot_M240, BulletWhizz, ImpactoPesado, DestruccionVehiculo, BalaImpacto, ImpactDirt, ImpactStone, MotorCamioneta, AmbienteDesierto | Freesound (qubodup, klangfabrik, greatmganga, christerljung, InMotionAudio, worthahep88, cedarstudios, ReadeOnly, DarkShroom), CC0. El propio CREDITS.txt dice «descargado de internet» | MEDIO-ALTO: es la mayor cantidad de material descargado |
| Barks en español, recargas y varios SFX de feedback | Sintéticos por código (`GenericSfx` / `SfxSintetico`) | BAJO (propio) |
| **`Assets/_Project/Audio_GunshotCandidates/*.wav`** (20 .wav + .meta) | Lista corta de la «Free Firearm Sound Library» (CC0), citada en CREDITS.txt y `Docs/RONDA_7_...md`. **No se usa**: 0 referencias por GUID desde prefab/escena/asset/cs/mat; los usados son copias recortadas en `Resources/Audio/Sfx/Shot_*`. No hay README en la carpeta. | MEDIO: no entra en la build (fuera de Resources) pero viaja con el proyecto. Mover a `_FueraDeEntrega/` (T23). |

Nota: `Resources/` entra completo en la build, así que todo `Resources/Audio` viaja.

## 3. Texturas
| Grupo | Origen | Riesgo |
|---|---|---|
| `Textures/Operacion/T_Op_*` (hormigón, ladrillo, chapa, madera, lona, asfalto, normales `_N`…) y `D_Op_*` (decals) | **Procedurales** por código con ruido fBm (`OperacionBuilder.Arte.cs:218`, `OperacionBuilder.Decals.cs:112`), con normales | BAJO (propio) |
| `Textures/Skybox/T_CieloNocturno` | Generada por script (`NightLightingBuilder.cs:20`) | BAJO |
| `Textures/Estrategia/T_Cartel_*`, `Textures/Fx/T_Particula_Suave` | Sin documento de origen (probablemente generadas en Editor) | BAJO; confirmar |
| `Textures/ArteMundo/Trimsheet_1024.png`, `ARTS/SP_Arte/Trimsheet*` | Trimsheet de 64 colores hecha en el proyecto (`IMPLEMENTACION_CLAUDE.md`) | BAJO, ver «IA» en §4 |
| `ARTS/SP_Arte/{Arbol 1, Arbol 3, Barricada, Barril, Soldado}` (BaseMap/MaskMap/Normal, `.spp`) | Substance Painter propio (hay `.spp`) | BAJO |
| `Resources/UI/Impactos/*.png` | **Kenney «Particle Pack 1.1»**, CC0, descargado 2026-09-20 | MEDIO (descargado) |
| `Resources/UI/WeaponIcons/Icono_Rifle, Pistol, Heavy` | **game-icons.net**, CC BY 3.0 (Skoll, John Colburn) | MEDIO-ALTO: descargado y con atribución obligatoria; no vi créditos en el juego |
| `WeaponIcons/Icono_Sniper, Smg, Shotgun, Rocket` | Dibujados con Pillow (script), siluetas propias | BAJO |
| `Resources/UI/Menu/Menu_Fondo.png` («ilustración propia», soldados/tanque/helicóptero a contraluz) | `MainMenuController.cs` dice «propia», pero **no hay script generador ni autor documentado** | **ALTO si se hizo con IA**; el dueño debe confirmarlo |
| `Resources/UI/HudIcons/*`, `RoleIcons/*` | Sin documento de origen | MEDIO: confirmar |

## 4. Modelos y animaciones
| Grupo | Origen | Riesgo |
|---|---|---|
| `ARTS/SP_Arte/_FBX_Export` y `MY_*.mb` (módulos, ambiente, vehículos, soldados, armas) | Modelados en Maya. `ARTS/SP_Arte/IMPLEMENTACION_CLAUDE.md` dice «hecho **sobre la sesión de Maya abierta vía commandPort**», con originales en `_backup_claude/` | **ALTO-MEDIO**: el documento y la carpeta nombran a Claude. Si el modelado lo hizo un asistente de IA por scripts, es el punto más sensible de «hecho con IA». |
| `_Project/Models/ArteProcedural`, `Art/MallasEscena`, `Art/MallasLimpias`, `Generated/FusionOperacion` (276 .asset), `Resources/Soldados/Mesh_*.asset` | Mallas generadas por código/Editor a partir de los FBX propios | BAJO (misma salvedad de Maya) |
| `ARTS/Slim Shooter Pack/*.fbx` (61) + `Pro Rifle Pack.zip` | **Animaciones de Mixamo** (lo dicen `PesadoAnimacion.cs`, `ArtSetup.cs`, esqueleto «mixamorig:»; «Gunplay» y «Reloading» bajadas de Mixamo). Licencia Adobe Mixamo: gratuita, sin regalías; no es CC. | **ALTO**: descargado de internet, sin crédito. Es todo el movimiento de los soldados (caminar, correr, disparar, morir). |
| `Resources/Weapons/*.prefab`, `Arte/Pickups` | Prefabs armados en el proyecto con mallas propias | BAJO |
| `Assets/TutorialInfo/*` | Plantilla de Unity (no es gameplay) | BAJO; quitar del RAR si se quiere |

## 5. Riesgos de desaprobación automática, por orden
1. **Mixamo (61 FBX de animación)**: descargado y sin crédito; afecta a todos los personajes.
2. **`SA_Calma.mp3` / `SA_Accion.mp3`**: son la música activa y no tienen origen. Averiguar quién las hizo o volver a Calm/Action o a música procedural.
3. **Audio descargado de Freesound/OpenGameArt/Kenney/itch.io** (~60 clips): legal, pero choca con la lectura estricta de la regla.
4. **Atribuciones obligatorias sin mostrar**: Kevin MacLeod (CC BY 3.0), game-icons.net (CC BY 3.0), OGA-BY. Falta pantalla de créditos o `ORIGEN_DE_RECURSOS.txt` en el RAR.
5. **Modelado vía Claude/Maya** (`IMPLEMENTACION_CLAUDE.md`, `_backup_claude`): se puede leer como «hecho con IA».
6. **`Menu_Fondo.png`** sin autor documentado.
7. **Fuentes TTF (OFL)**: descargadas, licencia incluida.
8. **`Audio_GunshotCandidates`**: no se usa pero viaja con el proyecto.

## 6. Qué es propio
Texturas T_Op/D_Op y cielo (procedurales), `Tension.wav`, SFX sintéticos (`SfxSintetico`), mallas fusionadas, iconos Sniper/Smg/Shotgun/Rocket, los `.spp`, y todo el código.

## 7. Acciones sugeridas
- Preguntar a la docente si valen assets libres con crédito (D11).
- Crear `ORIGEN_DE_RECURSOS.txt` dentro del RAR con esta tabla (autor, licencia, URL) y las atribuciones CC-BY.
- Identificar el origen de SA_Calma/SA_Accion y de Menu_Fondo.png.
- Mover `Audio_GunshotCandidates` a `_FueraDeEntrega/` (0 referencias, verificado por GUID).
- Si la docente es estricta: plan B para animaciones y audio (trabajo de varios días).
