# 09 - Revision de riesgos editor vs player (familia "level5 is corrupted")

Fecha: 2026-10-07. Tarea de solo lectura: no se toco ningun .cs/.unity/.prefab ni el editor. Los scripts de analisis estan en el scratchpad (`rev1.py`, `rev1c.py`, `rev1d.py`, `rev1f.py`, `rev2.py`, `rev3.py`, `rev3b.py`).

Contexto verificado: el build Standalone usa Mono (la carpeta `Build/` trae `MonoBleedingEdge`; `scriptingBackend` solo define Android), `managedStrippingLevel: {}` (default de plataforma) y `stripEngineCode: 1`. Escenas del build (EditorBuildSettings): SC_MainMenu, SC_Gameplay, SC_TestLevel, SC_Tutorial, SC_Loading, SC_Operacion (SC_Simulacion NO esta en el build). Calidad: `m_CurrentQuality: 1` (PC) y Standalone usa el nivel 1, o sea el mismo RP asset (PC_RPAsset) en editor y player.

---------------------------------------------------------------------

## 1. Scripts serializados: nombre de clase == nombre de archivo, GUID huerfanos

### 1.1 Escenas YAML (SC_Loading, SC_MainMenu, SC_TestLevel), prefabs y ScriptableObjects
- 156 archivos YAML con `m_Script`, 135 GUID distintos. Todos los GUID del proyecto resuelven a un `.cs` cuyo archivo contiene una clase con el mismo nombre que el archivo (0 desajustes).
- 50 GUID no estan en `Assets/`: 46 son componentes de paquetes (UGUI, URP, Input System, ai.navigation, render-pipelines.core; resueltos por `.meta` en `Library/PackageCache`) y 4 son componentes de prueba de URP dentro de `Assets/Settings/DefaultVolumeProfile.asset` (ver hallazgo I-1).
- SC_Loading solo usa `LoadingScreenController`; SC_MainMenu solo `MainMenuController`; SC_TestLevel 69 scripts del proyecto, todos con nombre correcto. Ningun script de carpeta `Editor` aparece en escenas ni prefabs.
- Ningun GUID huerfano en YAML de escenas/prefabs/materiales/ScriptableObjects de `Assets/_Project` (los unicos huerfanos son los de `Assets/Settings`, ver I-1).

### 1.2 Escenas binarias (SC_Operacion, SC_Gameplay, SC_Tutorial)
Se hizo mejor que solo mirar los builders: se parseo la tabla de externals del archivo serializado y se decodifico cada GUID (heuristica; los GUID de script se confirmaron contra `ArtilleroDeTorreta`).
- SC_Operacion: 178 externals, 67 scripts del proyecto; SC_Gameplay: 183 externals, 63 scripts; SC_Tutorial: 104 externals, 53 scripts.
- 0 externals sin `.meta` (ningun asset ni script faltante). 0 scripts de `Editor`, 0 clases abstractas/genericas, y todos los archivos referenciados tienen una clase con el nombre del archivo. `ArtilleroDeTorreta` ya aparece con su propio GUID en SC_Operacion (arreglo confirmado).
- Ningun asset de `_Recovery`, `Validacion`, `Docs`, `Registros`, `Historial` esta referenciado por las escenas del build.
- Builders (`Assets/Editor` y `Assets/_Project/Scripts/Editor`): 113 tipos del proyecto aparecen en `AddComponent<T>`/`typeof(T)`. Solo 2 incumplen la regla y ninguno va a una escena: `EscenaDelCheckAttribute` (atributo, no componente) y `MissionFlowTestBehaviour` (MissionFlowTestRunner.cs:44, es de Editor, se agrega en runtime de un test, no se guarda escena). Todos los componentes que los builders agregan a escenas/prefabs viven en un archivo homonimo.

### Hallazgo B-1 (baja, latente; pasa a ALTA si alguien las agrega desde un builder)
26 clases MonoBehaviour secundarias viven en archivos con otro nombre. Hoy solo se crean con `AddComponent<T>()` en runtime (y en tests), lo cual funciona en el player; no estan en ninguna escena ni prefab ni en builders (comprobado por GUID, por `AddComponent`/`GetComponent` y por busqueda de nombres en `Editor`). El riesgo es exactamente el de `ArtilleroDeTorreta`: si un builder guarda uno de estos componentes en una escena o prefab, la build queda con script perdido / "corrupted".

| Clase | Archivo donde vive (Assets/_Project/Scripts/...) |
|---|---|
| AnimacionDeAccionSoldado | Presentation/AnimacionDeAccion.cs |
| AvisoCentral | Presentation/CurandoAnimacion.cs |
| CasquillosDeMetralleta | Mision/PivoteMetralleta.cs |
| ChapaVolante, CraterVida | Presentation/DestruccionDeVehiculo.cs |
| CoheteDeAviso | Operacion/EmboscadaDeCohetes.cs |
| Corredor | Core/MetricasDeBuild.cs |
| Debris | Presentation/DebrisPool.cs |
| DemoledorAsalto | Player/Demolicion.cs |
| Fragmento | Presentation/Fragmentador.cs |
| GiroDeMarcador | Player/Rehen.cs |
| IndicadorDeGuardado | Core/PartidaGuardada.cs |
| InterruptorDeAjuste, SliderDeAjuste | UI/EstiloDeAjustes.cs |
| LocTraductor | Core/Loc.cs |
| MusicaVictoriaRunner | Presentation/MusicDirector.cs |
| MuzzleFlashLight | Presentation/MuzzleLightPool.cs |
| PoseDeRapel | Operacion/CinematicaDeRapel.cs |
| PoseSentada | Mision/TripulacionDelHeli.cs |
| SelectorSfx | UI/PanelAjustesExtra.cs |
| SiluetaMinimapa | Presentation/SiluetasDeMinimapa.cs (solo se hace GetComponent, nunca se agrega aca) |
| TajoVisual | Presentation/CuchilloFx.cs |
| TiradorDeTorre (abstracta) | Operacion/FrancotiradorEnTorre.cs:15 (base abstracta, no se adjunta; OK) |
| VehicleSmokePuff | Presentation/VehicleFxReactor.cs |
| VuelcoDeTorre | Operacion/TorreDestruible.cs:160 |
| WorldTag | Presentation/Feedback.cs |

Arreglo sugerido (no aplicado): (a) minimo, un check de Editor/`IPreprocessBuildWithReport` que falle la build si algun componente referenciado por una escena/prefab (o agregado por un builder) tiene nombre != archivo; el script `rev1.py` del scratchpad ya hace esa comprobacion y se puede portar a C#; (b) a mediano plazo, mover cada clase a su propio archivo (al menos las que algun dia puedan terminar en escena: `CasquillosDeMetralleta`, `PoseSentada`, `PoseDeRapel`, `SiluetaMinimapa`, `VuelcoDeTorre`, `WorldTag`).

---------------------------------------------------------------------

## 2. Codigo de runtime que depende del editor

Resultado: sin hallazgos de severidad alta/media.
- `UnityEditor`/`AssetDatabase`/`EditorUtility`/`EditorApplication`/`PrefabUtility`/`Handles` en codigo fuera de `Editor`: todos los usos estan dentro de `#if UNITY_EDITOR` (AutoDemoRunner.cs:124, CinematicaIntroPath.cs:3,32,82, MarcadorDeAparicion.cs:23, MainMenuController.cs:370 con `#else Application.Quit()`, MarcadorDeMision.cs:14, MinimapFollow.cs:404, WeaponStatusView.cs:149). `Gizmos.*` es UnityEngine y no rompe. (Comentarios con la palabra `Selection`/`PrefabUtility` en WeaponHolder.cs:646 y WeaponModels.cs:9 no son codigo; el `Selection` de PlayerInputDriver es el propio del juego.)
- Campos `[SerializeField]`/public dentro de `#if UNITY_EDITOR`: ninguno. Los 9 archivos con `#if` solo encierran metodos (`OnValidate`, `OnDrawGizmos`, `ContextMenu` interno, `using`); no cambian el layout serializado.
- `Resources.Load`/`LoadAll` a rutas que solo existen en `Assets/`: ninguno. Los 14 sitios (RecursosCache, GenericSfx, SpriteFx, VehicleAudioFeedback) piden rutas bajo `Assets/_Project/Resources/` y todas existen: `Soldados/Mesh_*` (6) y `MAT_Trimsheet_*`, `Weapons/P_Wpn_*` (Fusil, Pistola, Heavy, Metralleta, Lanzacohetes, Escopeta, Sniper, Granada, Cuchillo), `Audio/Music/{SA_Calma,SA_Accion,Calm,Action,Tension}`, `Audio/Heli/RotorReal_...`, `UI/Fuentes/*`, `UI/HudIcons/*` (importados como Sprite), `UI/Impactos/*` (los 12 nombres usados existen), `UI/WeaponIcons/Icono_*`, `UI/Menu/Menu_Fondo`. Los SFX por `Audio/Sfx/<Kind>` tienen respaldo procedural si la carpeta no existe.
- Hardcode de `Assets/`, `Registros/`, `Temp/`, `ProjectSettings` en runtime: ninguno. `SesionLog.Carpeta` (SesionLog.cs:116) y las partidas guardadas ya usan `persistentDataPath` fuera del editor.
- Mallas: los accesos a `mesh.vertices/uv/triangles` de mallas importadas estan protegidos con `isReadable` (HitboxCabeza.cs:36, SoldierAnimatorDriver.cs:142, Fragmentador.cs:190) y las mallas de soldado (`Resources/Soldados/Mesh_*.asset`) son `m_IsReadable: 1`; no hay diferencia editor/player.
- EnterPlayModeOptions: domain y scene reload activos (opciones = 0); no hay estaticos "sobrevivientes" que el player no tenga.

### Hallazgo B-2 (baja): escrituras al directorio de instalacion del player
- GameLog.cs:39 (`Application.dataPath/../GameFlowLog.txt`), TutorialLog.cs:20 (`dataPath/../Logs/Tutorial_Log.txt`), AutoplayRegistro.cs:114 (primer intento en `Logs/Autoplay`, con respaldo a `persistentDataPath`: bien) y AutoDemoRunner.cs:111 (`dataPath/../DemoCaptures`, SIN try/catch).
- En el editor eso cae en la carpeta del proyecto; en una build instalada en una ruta de solo lectura (Program Files) el log se pierde en silencio (GameLog y TutorialLog capturan la excepcion) y, solo si se usa `-autodemo`, `Directory.CreateDirectory` puede lanzar y matar la corrutina. Ademas `GameLog.buffer` es un `StringBuilder` sin tope que se reescribe entero cada ~1 s (crece con la sesion).
- Arreglo: usar `Application.persistentDataPath` (como SesionLog) y acotar el buffer (p. ej. ultimas N lineas) o escribir con `File.AppendAllText`.

### Hallazgo B-3 (baja): rutas solo-player nunca ejercitadas en el editor
- AjustesDeJuego.cs:64 y :115 (`if (Application.isEditor) return;` antes de `Screen.SetResolution`/pantalla completa por defecto) y MetricasDeBuild.cs:21 solo corren en el player; no hay forma de probarlas en el editor. `PrefRes` guarda un indice de `Screen.resolutions` (AjustesDeJuego.cs:101): si cambia el monitor, el indice apunta a otra resolucion.
- Arreglo: probarlas en la proxima build (primera ejecucion con PlayerPrefs vacio y con PlayerPrefs viejos); guardar ancho/alto en vez del indice.

---------------------------------------------------------------------

## 3. Shaders y recursos por nombre

`GraphicsSettings.m_AlwaysIncludedShaders` contiene solo los 7 por defecto de Unity: Legacy Diffuse (fileID 7), Hidden/Cube* (15104-15106), **Sprites/Default (10753)**, **UI/Default (10770)** y VideoDecode (10783). Por eso `Shader.Find("Sprites/Default")` y `Shader.Find("UI/Default")` (usados en ~15 sitios) estan cubiertos. Los `Shader.Find("Universal Render Pipeline/{Lit,Unlit,Particles/Unlit}")` estan cubiertos porque hay materiales que los usan (123 Lit, 4 Unlit y 3 Particles/Unlit en `Assets`), incluidos en SC_Operacion/SC_Gameplay.

### Hallazgo M-1 (MEDIA): `SP/ArmaEnPrimeraPersona` no entra en la build
- Uso: PlayerInputDriver.cs:219 (`Shader.Find("SP/ArmaEnPrimeraPersona")`).
- Evidencia: el shader (`Assets/_Project/Shaders/ArmaEnPrimeraPersona.shader`, guid 099332fc...) no esta en `Resources/`, no esta en Always Included y NINGUN `.mat`/escena/prefab lo referencia (busqueda de GUID en texto y en binario). Unity solo incluye shaders referenciados por assets de la build, asi que en el player `Shader.Find` devuelve `null`. El log de la build del 25/09 (`Logs/build-StandaloneWindows64-1790373291943.log`, seccion "Used Assets") tampoco lo lista.
- Efecto: en el player, `if (shaderArma != null)` (linea 220) no se cumple: el visor del arma conserva el material por defecto del cubo (URP/Lit normal), pierde el ZTest Always y el arma en primera persona se mete dentro de las paredes (exactamente lo que ese shader existe para evitar). No crashea, pero es una diferencia visible solo en la build.
- Arreglo: agregar el shader a Project Settings > Graphics > Always Included Shaders (en `ProjectSettings/GraphicsSettings.asset`, `m_AlwaysIncludedShaders`), o crear un material `M_ArmaFP.mat` referenciado desde una escena/prefab/Resources y cargarlo con `Resources.Load` en vez de `Shader.Find`.

### Hallazgo M-2 (MEDIA): `SP/MiraOptica` no entra en la build
- Uso: MiraOptica.cs:86-87 (`Shader.Find("SP/MiraOptica")` con respaldo a `SafeMaterial.Create(Color.white)`).
- Evidencia: igual que M-1 (guid ef92b3f3..., sin materiales ni referencias).
- Efecto: en el player el tubo de la mira cae siempre al respaldo: cilindro blanco liso sin la textura/retícula del shader propio (el comentario de la linea 82-85 explica que el shader del arma tampoco sirve para esto). Solo se ve en la build.
- Arreglo: mismo que M-1 (Always Included o material referenciado).

### Hallazgo B-4 (baja): `SP/OperacionTexto` entra solo por una referencia indirecta
- `Assets/_Project/Materials/Operacion/M_Op_Texto.mat` usa `SP/OperacionTexto` y SC_Operacion referencia ese material (confirmado por GUID), por lo que entra hoy. Es fragil: OperacionBuilder.cs:82-85 hace `Shader.Find("SP/OperacionTexto")` solo en el Editor; si una reconstruccion de SC_Operacion dejara de asignar el material, el shader se recorta sin aviso.
- Arreglo: sumar los tres shaders SP/* a Always Included (junto con M-1 y M-2).

### Hallazgo B-5 (baja, verificar en build): variantes de keyword creadas en runtime sobre URP/Lit
- Hay materiales de Lit con `_EMISSION` en la build (27 materiales), asi que las variantes de emision existen. En cambio `_SURFACE_TYPE_TRANSPARENT` solo lo tienen 3 materiales de Particles/Unlit; los materiales transparentes que se crean en runtime sobre Lit/Unlit (CoverHologram.cs:46, DiamondGizmo.cs:264, ParticleMaterialFactory.cs:52) dependen de variantes que Unity puede haber descartado (`m_StripUnusedVariants: 1` en la config de URP). En URP 17 el alpha sale de la propiedad `_Surface` (ShaderVariablesFunctions.hlsl:177), asi que el efecto esperable es solo estetico (SSAO/sombras/GI sobre transparentes), no que dejen de ser transparentes.
- Ademas `Shader.Find("Universal Render Pipeline/Unlit")` y `.../Particles/Unlit` solo se cubren porque SC_Gameplay tiene materiales con esos shaders; si esa escena sale del build, los respaldos (`Unlit/Color`, `Particles/Standard Unlit`, tampoco incluidos) devuelven null y se cae a Sprites/Default.
- Arreglo: tras la proxima build revisar visualmente holograma de cobertura, rombos y particulas; si falla, agregar una `ShaderVariantCollection` en Preloaded Shaders con Lit/Unlit transparentes, o un material "semilla" transparente en una escena.

`Resources.Load`/`LoadAll` por nombre: sin faltantes (ver seccion 2).

---------------------------------------------------------------------

## 4. Stripping y reflexion

- `managedStrippingLevel: {}` = default de plataforma (Mono Standalone, Minimal segun el default de Unity 6; confirmarlo en Player Settings > Other > Managed Stripping Level) y no existe `link.xml` ni `[Preserve]` en el proyecto. Con Minimal no se recortan miembros de `Assembly-CSharp`; el riesgo aparece si se sube a Low/Medium/High para achicar la build.
- Uso de reflexion en runtime (no Editor): solo estos:
  - OperacionDirector.Huida.cs:114, :121, :142-145, :232: `GetField("maxSpeed"/"acceleration"/"damage"/"explosionRadius"/"fireCooldown", NonPublic)` sobre `VehicleMotor` y `TurretWeapon`. Los campos existen, son `[SerializeField]` y se leen en el propio codigo, asi que se conservan. El problema es que todo esta protegido con `f != null`/`?.SetValue`: si un dia se renombra el campo o el linker lo recorta, el tanque pierde la velocidad/danio nuevos SIN error (solo diferiria la jugabilidad del objetivo "Huir").
  - AutoplayRegistro.cs:206 y CatalogoDelTutorial.cs:34: `GetFields(Public|Instance)` sobre tipos propios (solo diagnostico/checks).
  - KeyCapView.cs:208: `Enum.Parse(typeof(Key), "Digit"+t)` sobre enum del Input System (los miembros de enum no se recortan).
  - `SendMessage`, `Invoke("nombre")`, `StartCoroutine("nombre")`, `Activator`, `Type.GetType`, `AddComponent("...")`: no hay ninguno.
  - `JsonUtility` sobre tipos propios (PartidaGuardadaDatos, EstadoDeSesion, DatosDelMapa, TutorialFlags): campos publicos usados en codigo; seguros en Minimal.
- Hallazgo B-6 (baja): reflexion silenciosa sobre campos privados (Huida.cs, ver arriba). Arreglo: exponer setters publicos en `VehicleMotor` (p. ej. `FijarVelocidad(float)`, `FijarAceleracion(float)`) y en `TurretWeapon` (`Potenciar(int, float, float)`), o como minimo marcar los campos con `[UnityEngine.Scripting.Preserve]`/agregar un `Assets/link.xml` con `<assembly fullname="Assembly-CSharp" preserve="all"/>` si se sube el stripping. Si hace falta mantener la reflexion, loguear un error cuando `GetField` devuelva null.
- `ChecksBugs*`/HeadlessTestRunner son de Editor y no cuentan.

---------------------------------------------------------------------

## Otros hallazgos (informativos / baja)

- I-1 (info, baja): `Assets/Settings/DefaultVolumeProfile.asset` referencia 4 componentes que no existen (`OutlineVolumeComponent`, `TestAnimationCurveVolumeComponent`, `OasisFogVolumeComponent`, `TestVolume`; clases de prueba del template URP, GUID 60f3b30c, 0fd9ee27, 5a00a63f, 74955a4b) y `Assets/Settings/PC_Renderer.asset` 7 GUID de recursos de debug de Probe Volumes que no resuelven (lineas 20-26). Es igual en editor y player, pero puede imprimir avisos de "missing script" al cargar el perfil. Arreglo: quitar los 4 componentes del perfil desde el Inspector.
- I-2 (info, baja): el log de la build (`Logs/build-StandaloneWindows64-*.log`) muestra que se empaquetan los recursos `Resources` de `com.unity.ai.inference` (Sentis: `ConvGeneric.compute` 6,4 MB, etc.) y el aviso "No RuntimePipelineConfig asset found" de `com.unity.pipeline`; nada de eso se usa en runtime. Arreglo: si no se usa Sentis/ai.assistant/pipeline en el juego, quitarlos del `manifest.json` para achicar la build.
- I-3 (info): el modo dios (F4) sigue activo en el player (PlayerInputDriver.cs:768); es una decision de diseno, pero conviene saberlo antes de distribuir.
- Limitaciones: la lectura de externals de las escenas binarias es heuristica (valido: 0 faltantes, el parser reconstruye 178/183/104 externals, y el unico GUID que no resolvio fue 1 builtin); no se ejecuto el player. El unico `Player.log` existente (LocalLow/JOACO/Strategic Point) solo muestra el bug ya corregido (`level5 is corrupted`).

---------------------------------------------------------------------

## Resumen (conteo por severidad)

- Alta: 0. Media: 2. Baja: 6 (B-1 a B-6; B-1 pasa a alta si se agrega algun componente secundario a una escena/prefab). Informativos: 3 (I-1, I-2, I-3).
- Categoria 1 (nombre de clase == archivo, GUID huerfanos): escenas YAML/prefabs/SO y las 3 escenas binarias del build limpios; solo el riesgo latente B-1 (26 clases secundarias).
- Categoria 2 (codigo de editor en runtime, campos en `#if`, rutas de Assets): sin hallazgos graves, solo B-2/B-3 (escrituras en el directorio de instalacion y rutas solo-player).
- Categoria 3 (shaders/Resources): 2 shaders propios (`SP/ArmaEnPrimeraPersona`, `SP/MiraOptica`) se pierden en la build; todos los `Resources.Load` tienen su archivo.
- Categoria 4 (stripping): sin `link.xml`, stripping default (Minimal); reflexion silenciosa en OperacionDirector.Huida.cs (B-6).
- Los 3 mas importantes: (1) M-1 `SP/ArmaEnPrimeraPersona` ausente en el player (PlayerInputDriver.cs:219); (2) M-2 `SP/MiraOptica` ausente (MiraOptica.cs:86); (3) B-1 las 26 clases MonoBehaviour en archivos con otro nombre, a cubrir con un check de pre-build.
