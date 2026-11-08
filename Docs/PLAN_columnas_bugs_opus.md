# Plan: columnas del reporte de bugs (Strategic Point)

Autor: Opus 5.5 · 2026-11-08 · revisión de sólo lectura. Base: `Docs/INFORME_columnas_bugs_para_opus.md`,
`Core/SubidaDeBug.cs` (`Armar`), `Core/SesionLog.cs` (`EstadoDeSesion`, `Capturar`, `Diagnosticar`, `MarcarBugInterno`),
`Core/ReporteDeBug.cs`, `Core/ModoDios.cs`, y los JSON reales `bug_373`, `bug_379`, `bug_394`, `bug_395`.

---

## 0. Lo que encontré en los datos reales (y que el plan corrige)

| # | Hallazgo | Evidencia | Consecuencia |
|---|----------|-----------|--------------|
| H1 | **#394 y #395 no tienen `PlayerInputDriver.Activo`**: el diagnóstico no trae ni la línea `JUGADOR:`. La cámara está fija en (313, 24, 254), los aliados de la escuadra están INACTIVOS (subidos al heli, `heli aterrizo=True`) y `timeScale=2`. Es casi seguro la extracción o una cinemática, y el estado no lo dice. | `bug_394/395.json` → `jugador.soldado=""`, sin línea JUGADOR | Hace falta la columna **Estado_control**. |
| H2 | **Los valores de misión salen con coma decimal**: `Operacion.heli=...pos=322,0,19,5,266,0`. `ReporteDeBug.RutinaAbrir` llama a `Capturar()` y ahí `CapturarMision` corre **fuera** de `CulturaInvariante` (sólo `resumen` y `Diagnosticar` quedan envueltos). | `bug_395.json`, `mision` | Hay que envolver **todo** `Capturar` en `CulturaInvariante`. Esto ya rompe la restauración de la posición del heli. |
| H3 | **La nota con salto de línea parte `bugs.txt`**: la línea de #395 queda cortada en dos (`nota: Se trabo no avanza⏎ | estado: ...`). | `Registros/bugs.txt`, última entrada | Hay que sanear `\r\n` antes de escribir la línea, y el CSV nuevo tiene que entrecomillar los campos. |
| H4 | **En el Editor no se llama a `Armar`** (`SubidaDeBug.Disponible` da false), así que hoy los bugs locales no tienen columnas. | `SesionLog.cs:726` | Las columnas se tienen que armar **siempre**, aparte de la subida. |
| H5 | **Dos contadores de bugs**: el Editor usa `<proyecto>/Registros/contador_bugs.txt` y el .exe usa `persistentDataPath/Registros/`. El "#395" del Editor y el "#395" del Sheet pueden ser bugs distintos. | `SesionLog.Carpeta` | Hace falta un **Bug_id** único (origen + máquina + número). |
| H6 | **Un reintento puede duplicar la fila** (lo dice el propio comentario de `SubidaDeBug`). | `SubidaDeBug.cs:33` | El script tiene que hacer *upsert* por `Bug_id`. |
| H7 | **`Vehiculos` cuenta plantillas**: `AutoPlantilla` y `Halcon_Plantilla` (inactivas, en el origen) entran en el conteo y en `Vehiculos_detalle`. | `bug_373/379/394` | Contar sólo vehículos reales y filtrar las plantillas. |
| H8 | **Casi todas las bajas dicen "por ?"** (`LastAttackerId < 0`) en #394/#395, justo cuando la nota pregunta "¿se disparan entre ellos?". Con el log de hoy esa pregunta no tiene respuesta. | `ultimasLineas` de #394/#395 | Columna **Combate_30s** sacada de `DamageTakenEvent` (que sí trae `AttackerId`). Además conviene revisar por qué se pierde el atacante (¿daño de explosión o de proyectil sin dueño?). |
| H9 | **Modo dios no se registra en ningún lado**: `ModoDios.Activo` no aparece ni en el JSON ni en el diagnóstico. El `timeScale=2.00` de #373/#394/#395 es el x2 del modo dios, pero hay que deducirlo. #393 pide "el doble de la velocidad del modo dios". | `ModoDios.cs`, `Diagnosticar` | Columna **Ajustes**. |
| H10 | **No hay versión**: `bundleVersion: 0.1.0` nunca cambió y el build no lleva el commit. | `ProjectSettings.asset` | Columna **Build** con un sello del commit escrito al compilar. |
| H11 | En el tanque, el soldado poseído queda `activo=false` y con la misma posición que el vehículo (#373/#379). El atasco de #379 sí está en el log (`HUIDA: tanque trabado ... se liberaron 0 colisiones`, 4 veces en 8 s), pero perdido entre 60 líneas. | `bug_379.json` | Columna **Eventos_30s**, y **Entorno** medido sobre el vehículo cuando hay asiento. |

---

## 1. Columnas: qué agregar, qué sacar y qué reformular

Convenciones: **tipo** `num` (número invariante, filtrable), `txt`, `bool` (TRUE/FALSE), `lista` (validación en el Sheet), `fecha`.
**Origen**: `JSON:` es un dato que ya se captura; `NUEVO:` hay que capturarlo en C#; `SHEET:` es manual o lo calcula el Apps Script.
**Costo**: bajo (< 30 líneas, sin riesgo de rendimiento), medio (toca física, eventos o el build), alto (no hay ninguno).

### 1.1 Nuevas: imprescindibles (16)

| # | Columna | Tipo | Origen | Costo | Por qué, para estos bugs |
|---|---------|------|--------|-------|--------------------------|
| 1 | `Bug_id` | txt | NUEVO: `$"{(isEditor?"ED":"EX")}-{Environment.MachineName}-{n:000}"` | bajo | H5/H6: clave única entre Editor y .exe, y clave de *upsert* para los reintentos. |
| 2 | `Build` | txt | NUEVO: `Application.version` + sha corto + `*` si había cambios sin commitear + fecha del build (sello que escribe un `IPreprocessBuildWithReport`; en el Editor se lee `.git/HEAD`) | medio | Saber si un bug ya estaba corregido en ese build. Sin esto, "Verificado" no significa nada. |
| 3 | `Estado_control` | txt | NUEVO: `FPS_a_pie` · `FPS_agachado` · `Vehiculo:Gunner@Tanque_Fuga 479/1600` · `RTS` · `Muerto` · `Sin_soldado` · `Sin_control` · `Cinematica:Operacion/Rapel/Huida` · `Toma:Victoria` (desde `PlayerInputDriver.Activo`, `Brain.Current`, `Health.IsAlive`, `Rig.Mode`, `CurrentSeat`, `CinematicaDeOperacion.Activa`, `CinematicaDeRapel.Activa`, `CineDeHuida.Activa`, `TomaDeVictoria.Activa`) | bajo | H1: #394/#395 hoy salen con el jugador vacío y la posición en (0,0,0). También sirve para los pedidos de UI ("ocultá lo de los aliados mientras estoy en el tanque", #374). |
| 4 | `Subfase` | num | JSON: `mision["Operacion.subfase"]` | bajo | `Huir/4` y `Huir/0` son momentos distintos del guion; para reproducir alcanza con Fase + Subfase + Reloj. |
| 5 | `Objeto_mirado` | txt | NUEVO: raycast desde el centro de la cámara (500 m, ignora triggers): `ruta/últimos-3-niveles (capa) d=23.4m p=(x,y,z)` | bajo | El 52 % son pedidos que dicen "esto/aquí/esta bandera/estas ventanas" (#382, #387, #392). Dice **qué objeto** sin abrir la captura. |
| 6 | `Entorno` | txt | NUEVO: alrededor del jugador, o de su vehículo si va en uno: `nav=0.1m` (`NavMesh.SamplePosition` a 5 m; `nav=NO`) · los 3 colliders más cercanos que no son propios `Barrera_03(Obstaculos) 0.4m` · `velDeseada=6.0` (`SoldierMotor.MoveSpeed`) · `atado=0` | medio | Dolor nº 1 del informe: "se trabó / no avanza / snapea" (#374, #376, #379, #395) no dice qué lo traba. |
| 7 | `Desplaz_10s_m` | num | NUEVO: metros recorridos (en el plano) por el jugador o su vehículo en los últimos 10 s; buffer de 20 muestras cada 0,5 s en `SesionLog.Update` | bajo | Distingue "estoy trabado" (≈0 m con `velDeseada>0`) de "va lento" o "no avanza el guion". En #379 el tanque se movió menos de 1 m. |
| 8 | `Ajustes` | txt | NUEVO: `dios=1 ts=2.00 dif=Medio(activa) carreraDios=4 danoPropio=x3` (`ModoDios.*`, `Time.timeScale`, `Dificultad.Actual/Activa`) | bajo | H9: 31 notas de balance más las de modo dios (#393). Hacen falta los valores vigentes para poder decir "el doble". |
| 9 | `FPS_5s` | num | NUEVO: promedio real de los últimos 5 s (buffer de `unscaledDeltaTime`). Hoy existe `fpsSuave`, pero es un lerp. | bajo | Para priorizar los bugs de lag, que hoy no se pueden ordenar. |
| 10 | `Frame_ms_max_5s` | num | NUEVO: el peor frame del mismo buffer | bajo | Un tirón (spike) con un buen promedio de FPS es otro bug (como el #052 del bloque de 5 s). |
| 11 | `Errores_log` | txt | NUEVO: `N | último: NullReferenceException: ... (Clase.Metodo)` vía `Application.logMessageReceivedThreaded` (Error/Exception/Assert de la sesión) | bajo | No figura en el informe y es lo que más acorta un "no avanza": una excepción en el director de la misión lo explica todo. |
| 12 | `Estado` | lista | SHEET (manual; el script pone `Nuevo` al insertar) | bajo | Seguimiento: Nuevo / En análisis / Corregido / Verificado / Reabierto / No reproducible / Duplicado / Descartado. |
| 13 | `Prioridad` | lista | SHEET (prellenada desde `Clasif_auto` si está vacía) | bajo | P0 bloquea el avance · P1 defecto visible · P2 balance o visual · P3 pedido o mejora. |
| 14 | `Clase` | lista | SHEET (prellenada desde `Clasif_auto`) | bajo | Defecto / Pedido / Balance / Pregunta. 207 de 395 son pedidos y no se tienen que mezclar con defectos. |
| 15 | `Duplicado_de` | num | SHEET (manual; sugerido por `Bugs_cercanos`) | bajo | #384→#386 ("continúan atrapados") y #372/#381/#390 (helicópteros) son la misma raíz. |
| 16 | `Commit_fix` | txt | SHEET (manual o vía la acción `actualizar` del script, §4.5) | bajo | Une el bug con el cambio que lo corrige; junto con `Build` permite verificar. |

### 1.2 Nuevas: útiles (9)

| # | Columna | Tipo | Origen | Costo | Por qué |
|---|---------|------|--------|-------|---------|
| 17 | `Clasif_auto` | txt | NUEVO: heurística por palabras clave sobre la nota + señales (`Errores_log>0`, `Desplaz≈0`, "no avanza") → `Defecto·Colision/Nav·P0` | bajo | Es la misma heurística del informe, pero hecha al reportar; prellena Clase, Área y Prioridad. |
| 18 | `Area` | lista | SHEET (prellenada desde `Clasif_auto`) | bajo | IA aliados · IA enemigos · Vehículos/Helis · Colisión/Nav · UI/Minimapa · Luces/Visual · Audio/FX · Balance · Misión/Guion · Rendimiento. |
| 19 | `Combate_30s` | txt | NUEVO: `bajas E←P:1 E←E:0 E←?:11 · fuegoAmigo E→E:34 P→P:0` (suscripción a `DamageTakenEvent` y `EntityDiedEvent`, en 30 cubetas de 1 s) | medio | H8: #394 "¿se disparan entre ellos?" con un número que lo confirma o lo descarta. Sirve para los 100 bugs de IA. |
| 20 | `Eventos_30s` | txt | NUEVO: conteo por tipo de los eventos de los últimos 30 s: `HUIDA tanque trabado×4 · BAJA×12 · SUBFASE×1` | bajo | H11: el atasco de #379 ya está en el log, pero escondido. |
| 21 | `Aliados_nav` | txt | NUEVO: `sinCamino=0 parcial=2 invalido=0 lejos>30m=6 regresos=29` (`NavMeshAgent.pathStatus`, distancia, `Brain.RegresosForzados`) | bajo | #384/#386 "mis aliados no pasaron / siguen atrapados". |
| 22 | `Sesion` | txt | NUEVO: `sesion_SC_Operacion_20261108_121957 · #28` (el log y el orden del bug dentro de la sesión) | bajo | Agrupa los 28 bugs de una partida y deja abrir el log correcto (hoy sólo lo tiene `bugs.txt`). |
| 23 | `Fecha_corregido` | fecha | SHEET (la pone `onEdit` al pasar a Corregido) | bajo | Seguimiento. |
| 24 | `Verificado_por` | txt | SHEET (la pone `onEdit` al pasar a Verificado: email + fecha) | bajo | Seguimiento. |
| 25 | `Notas_dev` | txt | SHEET o el agente | bajo | La causa raíz en una línea; el agente la escribe al corregir. |

### 1.3 Nuevas: opcionales (2, fuera del cupo de 25)

| # | Columna | Tipo | Origen | Costo | Por qué |
|---|---------|------|--------|-------|---------|
| 26 | `Bugs_cercanos` | txt | SHEET (el script calcula: misma escena y fase, a ≤ 15 m, en los últimos 200 bugs) → `#384(8m) #376(12m)` | medio | Sugiere duplicados y retrabajos. En el script porque ahí está toda la historia; en C# sólo se vería la de esa máquina. |
| 27 | `Origen_partida` | txt | NUEVO: `nueva` · `autoguardado:Huir/4` · `restaurada:bug#379` (string estático que ponen `PartidaGuardada` y `SesionLog.Restaurar`) | medio | Un bug después de restaurar puede ser un bug de la restauración. |

Descartada: hardware/GPU/RAM, porque prueba un solo jugador en una sola PC. Si hiciera falta, puede ir dentro de `Build`.

### 1.4 Sacar o reformular (columnas actuales)

| Actual | Acción | Motivo |
|--------|--------|--------|
| `Camara_X/Y/Z`, `Camara_RotX/Y/Z`, `Camara_FOV` (7) | **Fusionar** en `Camara` txt: `(313.0,24.0,254.0) rx=11.3 ry=36.9 fov=60 cam=<nombre>` | RotZ siempre vale ≈0, RotX duplica el pitch y nadie filtra por la cámara. Los números exactos siguen en el JSON (es lo que usa `RestaurarBug`). Le agrego el nombre de la cámara activa, que hoy falta para los pedidos de luces y UI. |
| `Tiempo_juego_s` | **Sacar** | Lo cubren `Tiempo_sesion_s` y `Reloj_operacion_s`; también está en `Diag_Tiempo`. |
| `Diag_Tiempo` | **Sacar** | Lo reemplazan `FPS_5s`, `Frame_ms_max_5s` y `Ajustes`, que además son filtrables. |
| `Diag_Curacion` | **Reemplazar** por `Diag_Minimapa` (la línea `MINIMAPA:` del diagnóstico, que ya existe) | Casi ninguna nota habla de curación; minimapa y UI aparecen en 48. La curación sigue en el JSON. |
| `Enemigos_vivos_total` | **Renombrar** a `Enemigos_reserva` = total − activos | Hoy hay que restar a mano; "reserva" es lo que dice el diagnóstico. |
| `Vehiculos`, `Vehiculos_detalle` | **Filtrar** las plantillas (`!activo && nombre contiene "Plantilla"`) | H7: ruido en cada fila. |
| `Fase` | Se queda; se agrega `Subfase` | — |
| `Descripcion` | **Reformular**: sumar `Estado_control` y `Objeto_mirado` | Es lo que lee una persona en la celda. |
| `Aliados`, `Vehiculos_detalle`, `Mision`, `Diag_Jugador`, `Diag_Enemigos` | Se quedan, **movidas al final** | Son voluminosas y útiles para el agente, no para la vista de seguimiento. |
| `Archivo_local`, `Captura_local`, `Estado_JSON` | Se quedan | Son el puente entre el Sheet y `Registros/`. |

Saldo: 25 columnas nuevas (16 + 9), 2 opcionales, 9 que se van o se fusionan (Camara×7, Tiempo_juego_s, Diag_Tiempo) y 1 reemplazada. El Sheet crece unas 16 columnas netas y queda ordenado por bloques (§4.1).

---

## 2. Cambios en C# (paso a paso)

### Paso 2.1: correcciones previas (bajo, hacerlas primero)
1. **`SesionLog.Capturar`**: envolver el cuerpo entero en `using (new CulturaInvariante())` (H2). Probar con `CurrentCulture = es-AR`.
2. **`SesionLog.MarcarBugInterno`**: sanear la nota para `bugs.txt` (`nota.Replace("\r","").Replace("\n"," ⏎ ").Trim()`) (H3). El JSON conserva la nota tal cual.
3. **Plantillas**: helper `static bool EsPlantilla(SnapVehiculo v) => !v.activo && v.nombre.Contains("Plantilla")`, usado en el conteo y en el detalle (H7).

### Paso 2.2: modelo de datos (`SesionLog.cs`)
- `EstadoDeSesion.version = 2`. Clases nuevas `[Serializable]`. JsonUtility las serializa anidadas, y un JSON v1 se lee igual (los campos nuevos quedan en su valor por defecto):
  ```csharp
  [Serializable] public class SnapContexto {
      public string bugId, build, estadoControl, sesionLog, origenPartida, ajustes, camaraNombre;
      public int ordenEnSesion, subfase = -1;
      public float fps5s, frameMsMax5s, desplaz10s, velDeseada; public bool atado, modoDios; public float timeScale;
      public string dificultad;
      public string miradoRuta, miradoCapa; public float miradoDist = -1; public Vector3 miradoPunto;
      public float navDist = -1; public List<string> colliders = new List<string>();
      public int errores; public string ultimoError;
      public string combate30s, eventos30s, aliadosNav;
      public List<Vector3> posiciones10s = new List<Vector3>();   // para graficar el atasco
  }
  public SnapContexto contexto = new SnapContexto();   // en EstadoDeSesion
  ```

### Paso 2.3: buffers en `SesionLog` (bajo)
- `float[] dtBuf = new float[300]` circular, cargado en `Update` con `Time.unscaledDeltaTime` (saltear cuando `timeScale==0`, para que el diálogo no ensucie). → `fps5s`, `frameMsMax5s`.
- `Vector3[] posBuf = new Vector3[20]` + `float proximaMuestra`: cada 0,5 s (unscaled) guarda la posición de `yo` (en el tanque coincide con la del vehículo, H11). → `desplaz10s` (suma de tramos en el plano, descartando saltos > 6 m como ya hace `metrosLapso`) y `posiciones10s`.
- **Errores**: en `OnEnable`, `Application.logMessageReceivedThreaded += OnLog` (y la baja en `OnDisable`). Con `lock`, contar `Error|Exception|Assert` y guardar el último mensaje + la primera línea del stack que contenga `SP.`. Usar `Interlocked` o lock, porque se llama desde otros hilos (la subida de red).
- **Combate**: suscribir `DamageTakenEvent` → `ActorRegistry.FindById` (es O(1), es un diccionario) para atacante y víctima. Cubetas `int[30,k]` por segundo (unscaled) con contadores `E→E, P→P, E→P, P→E, ?→*`. En `OnDeath` (ya existe) sumar el bando del atacante. Al capturar se suman las 30 cubetas. No se guarda ningún string por evento.
- **Eventos_30s**: en `EscribirLinea` encolar `(unscaledTime, clave)`, donde clave = `tipo` + las 3 primeras palabras del texto sin números (`"EVENTO HUIDA: tanque trabado"`). Se purga lo que tiene más de 30 s y se agrupa al capturar.
- `static int bugsEnSesion` (se reinicia en `Abrir()` con el archivo nuevo) → `ordenEnSesion`; `sesionLog = Path.GetFileNameWithoutExtension(archivo)`.
- `public static string OrigenPartida = "nueva"`: lo ponen `PartidaGuardada` al cargar (`"autoguardado:" + fase`) y `Restaurar` (`"restaurada:bug#" + e.numero`). Opcional.

### Paso 2.4: `SesionLog.Capturar` → nuevo método `CapturarContexto(EstadoDeSesion e, Soldier yo, PlayerInputDriver drv)` (bajo/medio)
Se llama al final de `Capturar`, dentro de `CulturaInvariante` y del `try`, igual que `Diagnosticar`. Si falla, `"(error contexto: ...)"` y no corta nada.
1. `estadoControl`: el orden de la tabla 1.1 #3. Primero la cinemática, después `drv==null`, `yo==null`, muerto, RTS, vehículo (`drv.CurrentSeat` + el vehículo cuyo `Occupants` contiene a `yo`, con hp) y por último a pie (agachado).
2. `ajustes`/`modoDios`/`timeScale`/`dificultad`: `ModoDios.Activo`, `Time.timeScale` (se captura **antes** de la pausa del diálogo: `RutinaAbrir` llama a `Capturar` antes de poner `timeScale=0`, así que ya está bien), `Dificultad.Actual` + `Dificultad.Activa`, y las constantes `ModoDios.FactorDeCarreraDelModoDios` y `MultiplicadorDeDanoPropio`.
3. **Objeto mirado**: `cam = CamaraPrincipal.Actual`; `Physics.Raycast(cam.ViewportPointToRay(new Vector3(.5f,.5f)), out hit, 500f, ~0, QueryTriggerInteraction.Ignore)`. Ruta = los últimos 3 niveles de `hit.transform` (`Edificio_04/Ventanas/Vidrio_12`), capa = `LayerMask.LayerToName`. `camaraNombre = cam.name`.
4. **Entorno**: centro = `yo.transform.position + Vector3.up` (o los `bounds` del vehículo si va montado). `NavMesh.SamplePosition(centro, out h, 5f, NavMesh.AllAreas)` → `navDist`. `Physics.OverlapSphereNonAlloc(centro, radio = 1.5f o extents+0.5f, buf[32], ~0, Ignore)` → descartar los colliders de `yo` y de su vehículo (`transform.root`) y ordenar por `ClosestPoint` para quedarse con 3: `"nombre(capa) 0.4m"`. Llamar antes a `Physics.SyncTransforms()` (trampa conocida del proyecto con `Collider.bounds`). `velDeseada = yo.Motor.MoveSpeed`, `atado = yo.Motor.Atado`.
5. **Aliados_nav**: en el mismo bucle de `NavDiag`, contar `pathStatus == PathPartial/PathInvalid`, aliados a > 30 m de `yo` y la suma de `RegresosForzados`.
6. Copiar los buffers del paso 2.3. `subfase` = parse de `Operacion.subfase`.
7. **`bugId`/`build`** no van acá: se ponen en `MarcarBugInterno` (ahí se conoce `n`).

### Paso 2.5: sello del build (medio)
- `Assets/_Project/Scripts/Editor/SelloDeBuild.cs` : `IPreprocessBuildWithReport` (order 0) ejecuta `git rev-parse --short HEAD` y `git status --porcelain` (Process, cwd = la raíz del proyecto, timeout 5 s, y si falla deja "sin-git") y escribe `Assets/_Project/Resources/build_sello.txt` = `0.1.0+a1b2c3d*|2026-11-08 11:02`. Agregarlo a `.gitignore`. Comprobar que corre con `CliBuilder`, `HeadlessBuilder` y `BuildAndPack`, que llaman a `BuildPipeline.BuildPlayer` y por lo tanto disparan la interfaz.
- Runtime: `static string BuildSello` en `SesionLog`, cargado una vez: `Resources.Load<TextAsset>("build_sello")`; en el Editor lee `.git/HEAD` → ref → `.git/refs/heads/<rama>` (o `packed-refs`) sin lanzar procesos, y queda `0.1.0+<sha>·editor`.
- Además, en `SesionLog.Abrir` escribir el sello en la cabecera del `sesion_*.txt`.

### Paso 2.6: un solo armador de columnas (bajo; es el corazón del cambio)
- Archivo nuevo `Core/ColumnasDeBug.cs`: `public static class ColumnasDeBug` con
  - `public const int Esquema = 2;`
  - `public static readonly string[] Encabezado` = **el orden final de §4.1** (bloques A, B y C, sin las columnas que sólo existen en el Sheet).
  - `public static List<KeyValuePair<string, object>> Armar(EstadoDeSesion e, string nota, string rutaJson, string rutaPng)`: valores tipados (`int`/`float`/`bool`/`string`). Mueve aquí todo el cuerpo de `SubidaDeBug.Armar` con las reformulaciones de §1.4 y los campos de `e.contexto`.
  - `public static string Clasificar(string nota, EstadoDeSesion e)` → `Clasif_auto` (palabras clave del informe en minúsculas y sin tildes: pedido = `quiero|falta|quita|deberia|agrega|pon`; balance = `vida|dano|velocidad|doble|mitad|veces`; colisión = `snap|trab|atasc|atrap|no avanza|no pasa`; etc. Prioridad: `no avanza|se trabo|bloquea` o `errores>0` → P0; defecto → P1; balance/visual → P2; pedido → P3).
  - `public static string ComoCsv(List<...>)` / `public static string ComoJson(List<...>)` (el JSON reutiliza `Columnas` de `SubidaDeBug`, movida aquí).
- `SubidaDeBug.Armar` queda como un envoltorio: `datosJson = ColumnasDeBug.ComoJson(ColumnasDeBug.Armar(...))` + la descripción reformulada.
- `MarcarBugInterno` (H4): **siempre** arma las columnas, las agrega al CSV local, y **sólo si** `SubidaDeBug.Disponible` arma el reporte de subida con la misma lista.

### Paso 2.7: datos del bug en `MarcarBugInterno`
- `e.contexto.bugId`, `e.contexto.build = BuildSello`, `e.contexto.ordenEnSesion = ++bugsEnSesion` **antes** de `JsonUtility.ToJson`, para que el JSON local también los tenga.

---

## 3. Registro local

### 3.1 `bugs.txt` (se queda, una línea por bug)
Formato nuevo (compatible: mismos separadores ` | `, campos agregados al final para no romper a quien lo parsee):
```
BUG #396 | ED-PCJOACO-396 | 2026-11-08 12:40:01 | escena SC_Operacion | sesion T+980.1s | fase=Extraer/0 | Sin_control+Cinematica:Operacion | fase=... (resumen) | nota: Se trabo no avanza | estado: bug_396_....json | captura: ....png | log: sesion_....txt | build 0.1.0+a1b2c3d* | fps 41 (max 38ms) | mira Heli_Extraccion/Cuerpo (Vehiculos) 22m | desplaz10s 0.3m | errores 2
```
La nota se sanea siempre (H3).

### 3.2 `Registros/bugs_columnas.csv` (nuevo)
- **Mismas columnas y mismo orden que el Sheet** (`ColumnasDeBug.Encabezado`), más las columnas manuales de seguimiento (`Estado`=`Nuevo`, `Prioridad`/`Clase`/`Area` prellenadas desde `Clasif_auto`, y el resto vacías). Así el agente puede llevar el seguimiento de los bugs del Editor en el CSV sin depender del Sheet.
- RFC 4180: separador `,`, todo campo con `,` `"` `\n` va entre comillas y con las `"` duplicadas; números con punto decimal; **UTF-8 con BOM** (Excel en es-AR si no, rompe las tildes); `\r\n` como fin de línea.
- Los campos multilínea (`Aliados`, `Vehiculos_detalle`, `Diag_*`) se escriben entrecomillados con `\n` dentro (es válido en CSV). Si molesta en Excel, opción de reemplazar por ` ⏎ `.
- Cabecera: si el archivo no existe, se escribe. Si existe con **otra** cabecera (cambió el esquema), se renombra a `bugs_columnas_esquema<N>_<fecha>.csv` y se empieza uno nuevo. Nunca se mezclan esquemas.
- Lo escribe `SesionLog.MarcarBugInterno` justo después de `bugs.txt`, en el mismo `try`. Un fallo del CSV no impide el JSON ni el PNG.
- En el .exe va a `persistentDataPath/Registros/`, igual que el resto.

### 3.3 Reconstrucción de los 395 bugs viejos (bajo, opcional pero recomendado)
- Menú de Editor `SP/Bugs/Regenerar bugs_columnas.csv desde los JSON` (`Editor/RegenerarColumnasDeBug.cs`): recorre `Registros/bug_*.json`, hace `JsonUtility.FromJson<EstadoDeSesion>` y llama a `ColumnasDeBug.Armar`. Las columnas nuevas quedan vacías en los v1, pero `Clasif_auto`, `Subfase`, `Camara`, `Enemigos_reserva` y el resto de los derivados salen bien. Escribe `bugs_columnas_reconstruido.csv` sin tocar el vivo. Sirve también como prueba de regresión (paso 5.2).
- También se puede correr por la CLI de Unity (`eval` de `RegenerarColumnasDeBug.Ejecutar()`).

---

## 4. Apps Script del Sheet (especificación; el código no está en el repo)

### 4.1 Orden de columnas (de izquierda a derecha)
**Bloque S: seguimiento (manual, congelado hasta `Comentarios`)**
`Bug` · `Bug_id` · `Fecha` · `Foto` · `Comentarios` · `Estado` · `Prioridad` · `Clase` · `Area` · `Duplicado_de` · `Commit_fix` · `Fecha_corregido` · `Verificado_por` · `Notas_dev` · `Clasif_auto` · `Bugs_cercanos`

**Bloque A: reproducir (automático, corto)**
`Build` · `Escena` · `Fase` · `Subfase` · `Reloj_operacion_s` · `Tiempo_sesion_s` · `Estado_control` · `Jugador` · `Modo` · `Asiento` · `Jugador_X` · `Jugador_Y` · `Jugador_Z` · `Jugador_Yaw` · `Jugador_Vida` · `Jugador_VidaMax` · `Camara` · `Objeto_mirado` · `Entorno` · `Desplaz_10s_m` · `Ajustes` · `FPS_5s` · `Frame_ms_max_5s` · `Errores_log` · `Combate_30s` · `Eventos_30s` · `Aliados_nav` · `Aliados_vivos` · `Enemigos_vivos` · `Enemigos_reserva` · `Vehiculos` · `Sesion` · `Origen_partida`

**Bloque C: volumen (automático, al final, columnas angostas con texto recortado)**
`Descripcion` · `Aliados` · `Vehiculos_detalle` · `Mision` · `Diag_Jugador` · `Diag_Enemigos` · `Diag_Minimapa` · `Estado_JSON` · `Archivo_local` · `Captura_local`

### 4.2 Escritura (`doPost`)
1. Leer la fila 1 y armar un mapa `encabezado → índice`. **Escribir siempre por nombre**, nunca por posición.
2. Una clave de `datos` que no está en el encabezado se agrega **al final del bloque A o C** (C si la clave empieza con `Diag_` o el valor tiene más de 300 caracteres). **Nunca** dentro del bloque S. Las claves viejas que ya no llegan (`Camara_X`, `Tiempo_juego_s`...) se dejan como columnas históricas: se pueden agrupar u ocultar con la migración (4.6).
3. **Upsert por `Bug_id`** (H6): si ya existe una fila con ese id, se actualizan sólo las columnas automáticas (A y C, y Foto/Estado_JSON si llegan) y se responde `{ok:true, fila, actualizada:true}`. Si no viene `Bug_id` (un cliente viejo), se cae a la clave `Bug`+`Fecha`.
4. Al **insertar**: `Estado="Nuevo"`, `Fecha=now`. `Prioridad`/`Clase`/`Area` se toman de `Clasif_auto` (formato `Clase·Area·Pn`) **sólo si la celda está vacía**. Después, `Bugs_cercanos` (4.4).
5. **Nunca** sobrescribir una celda del bloque S que no esté vacía (salvo `Bugs_cercanos` y `Clasif_auto`, que son derivadas).
6. `LockService.getScriptLock()` alrededor del upsert: dos reportes casi simultáneos no deben pisarse la fila.

### 4.3 Validación de datos
| Columna | Regla |
|---------|-------|
| `Estado` | Lista: Nuevo, En análisis, Corregido, Verificado, Reabierto, No reproducible, Duplicado, Descartado (rechazar lo que no esté). |
| `Prioridad` | Lista: P0, P1, P2, P3. |
| `Clase` | Lista: Defecto, Pedido, Balance, Pregunta. |
| `Area` | Lista: IA aliados, IA enemigos, Vehículos/Helis, Colisión/Nav, UI/Minimapa, Luces/Visual, Audio/FX, Balance, Misión/Guion, Rendimiento, Otro. |
| `Duplicado_de` | Fórmula personalizada: entero y existe en la columna `Bug` (`=COUNTIF(A:A, J2)>0`). |
| `Commit_fix` | `=OR(J2="", REGEXMATCH(J2, "^[0-9a-f]{7,40}( [0-9a-f]{7,40})*$"))` (permite varios sha separados por espacio). |
| `Fecha_corregido` | Fecha válida. |

### 4.4 Columnas calculadas por el script
- `Bugs_cercanos`: al insertar, recorrer las últimas 200 filas con la misma `Escena` y `Fase` y una distancia en el plano (`Jugador_X/Z`, o la posición de `Camara` si el jugador está vacío) ≤ 15 m → `#384(8m) #376(12m)` (máximo 5, de más cerca a más lejos).

### 4.5 Disparadores y acciones
- **`onEdit` instalable** (no el simple, porque necesita `Session.getActiveUser`):
  - Estado → `Corregido`: si `Fecha_corregido` está vacía, poner hoy. Si `Commit_fix` está vacío, poner una nota en la celda ("falta commit") **sin** bloquear.
  - Estado → `Verificado`: `Verificado_por` = email + fecha si está vacía. Si no había `Fecha_corregido`, ponerla también.
  - Estado → `Duplicado`: si `Duplicado_de` está vacío, nota de aviso.
  - Estado → `Reabierto`: agregar al principio de `Notas_dev` `"[reabierto <fecha>] "`. Las fechas se conservan como historial.
- **Acción `actualizar`** en `doPost` (con el mismo token): `{token, accion:"actualizar", bug_id, Estado?, Commit_fix?, Notas_dev?, Prioridad?, Clase?, Area?, Duplicado_de?}`. Sólo toca columnas del bloque S y aplica las mismas reglas que `onEdit`. Así el agente que corrige marca *Corregido + commit* sin abrir el Sheet. Responde `{ok, fila}`.
- **Acción `listar`** (`doGet?accion=listar&estado=Nuevo,Reabierto&token=…`): devuelve `Bug_id, Bug, Comentarios, Clase, Prioridad, Estado_JSON` para que el agente arme su cola de trabajo. Opcional.

### 4.6 Migración única (`migrarColumnas()`, se corre a mano una vez)
1. Hacer una copia de la hoja (`Bugs_backup_2026-11-08`).
2. Crear las columnas del bloque S que falten y reordenar según 4.1 con `moveColumns` (las que el script no conoce quedan al final, en un grupo colapsado "Históricas").
3. Rellenar `Estado="Nuevo"` en las filas viejas que no tengan; `Bug_id = "EX-?-" + Bug` (origen desconocido, pero único dentro del Sheet).
4. `Clasif_auto` para las filas viejas con la misma heurística, portada a JS (o importando el `bugs_columnas_reconstruido.csv` del paso 3.3 cruzado por `Bug`). Prellenar Clase, Área y Prioridad.
5. Aplicar las validaciones (4.3) y el formato (4.7) a todo el rango.

### 4.7 Formato condicional y vista
- Fila 1 congelada; columnas congeladas hasta `Comentarios`. Filtro activo.
- Fondo **de la fila completa** según `Estado`: Nuevo = blanco · En análisis = celeste · Corregido = verde claro · Verificado = verde · Reabierto = naranja · No reproducible/Descartado = gris con texto gris · Duplicado = gris tachado.
- `Prioridad = P0` rojo y negrita; P1 naranja.
- `FPS_5s` < 30 naranja, < 20 rojo. `Frame_ms_max_5s` > 50 naranja, > 100 rojo.
- `Errores_log` no vacío o distinto de `0` → texto rojo.
- `Estado_control` que contenga `Sin_control|Sin_soldado|Muerto|Cinematica` → amarillo.
- `Desplaz_10s_m` < 0.5 **y** `Clase = Defecto` → rojo claro (atasco probable).
- `Build` distinto del build más nuevo de la hoja → texto gris (un bug reportado en un build viejo).
- Vistas de filtro guardadas: "Cola P0-P1 abiertas" (Estado ∈ Nuevo/En análisis/Reabierto, Prioridad ≤ P1), "Para verificar" (Estado = Corregido), "Pedidos" (Clase = Pedido), "Rendimiento" (FPS_5s < 40).

---

## 5. Cómo testearlo localmente

### 5.1 Pruebas puras (sin Play; con `eval` de la CLI de Unity o en `ChecksBugs065` con el patrón `W8Ok`)
1. **Columnas desde un JSON real**: cargar `bug_395.json` (v1) → `ColumnasDeBug.Armar`. Verificar: las claves coinciden **exactamente** con `Encabezado` y en el mismo orden; `Vehiculos == 1` real (3 − 2 plantillas); `Enemigos_reserva == total − activos`; `Subfase == 0`; las columnas nuevas vacías, sin excepción.
2. **Cultura**: `CultureInfo.CurrentCulture = es-AR` → `ComoCsv`/`ComoJson` con `float 12.5` → `"12.5"`. Y `SesionLog.Capturar` en es-AR (en Play) → ningún valor de `mision` con el patrón `\d,\d,\d` (H2).
3. **CSV**: una nota con `"`, `,`, `\n` y tildes → una sola fila lógica; releerla con un parser RFC 4180 (por ejemplo `Microsoft.VisualBasic.FileIO.TextFieldParser` o uno mínimo) da el mismo texto. BOM presente.
4. **bugs.txt**: una nota `"linea1\nlinea2"` → la línea agregada no tiene `\n` interno (H3).
5. **Clasif_auto** contra notas reales: #395 → `Defecto·Colision/Nav·P0`; #379 → `Defecto·Colision/Nav·P0`; #393 → `Balance·Balance·P2`; #392 → `Pedido·Luces/Visual·P3`; #394 → `Defecto·IA enemigos·P1`; #386 → `Defecto·IA aliados·P0/P1`. Son fixture: si se toca la heurística, se actualizan a conciencia.
6. **Regeneración**: correr el paso 3.3 sobre los 395 JSON → 395 filas, 0 excepciones, cabecera igual a `Encabezado`. Sirve de regresión ante cualquier cambio de `Armar`.

### 5.2 En Play (agregarlo al paso 8 de `ValidacionBugs`, que ya abre el diálogo con [U] y llama a `Confirmar` con la subida apagada)
1. Contar las filas de `bugs_columnas.csv` antes y después de `Confirmar` → +1, con el mismo `Bug_id` que el JSON.
2. En esa fila: `Build` no vacío; `FPS_5s > 0`; `Frame_ms_max_5s ≥ 1000/FPS_5s`; `Estado_control == "FPS_a_pie"`; `Objeto_mirado` no vacío (mirando al piso como mínimo); `Entorno` empieza con `nav=`; `Ajustes` contiene `ts=1.00` (o `2.00` con dios).
3. **Escenarios dirigidos** (cada uno marca un bug con `ReporteDeBug.Confirmar` y mira la columna):
   - `ModoDios.Poner(true)` → `Ajustes` contiene `dios=1 ts=2.00`.
   - Subir al `Tanque_Fuga` → `Estado_control` empieza con `Vehiculo:` y `Entorno` mide alrededor del tanque (no lista colliders del propio tanque).
   - Pegar al soldado contra un obstáculo conocido de `SC_TestLevel` y dejarlo empujando 10 s → `Desplaz_10s_m < 0.5` y `Entorno` lista ese obstáculo.
   - Matar al soldado poseído, o `drv.enabled=false`/una cinemática → `Muerto` / `Sin_control` / `Cinematica:*`.
   - Hacer que un enemigo dañe a otro con `Health.TakeDamage(..., attackerId: enemigo)` → `Combate_30s` con `E→E ≥ 1`.
   - `Debug.LogError("prueba")` → `Errores_log` empieza con `1 |`.
   - Recordatorio de la memoria del proyecto: el teclado real no se puede simular desde la CLI (`wasPressedThisFrame` nunca da true). Usar `ReporteDeBug.PruebaAbrir` / `Confirmar`, como hace `ValidacionBugs`. En Edit mode, llamar a `ActorRegistry.Rebarrer()` y a `Physics.SyncTransforms()` antes de medir.
4. Rendimiento: medir `Capturar` con `Stopwatch` antes y después. Objetivo: < 3 ms extra (raycast + overlap + SamplePosition + suma de buffers). El `OnLog` y los contadores de daño no reservan memoria por evento (mirar el GC Alloc en el Profiler durante un tiroteo).

### 5.3 Apps Script (contra una copia, nunca contra el Sheet real)
1. Duplicar el Sheet y desplegar el script de la copia como una **implementación aparte** (otra URL).
2. En C#, permitir apuntar a esa URL sin recompilar: `SubidaDeBug.Url` lee primero `Registros/subida_url.txt` (o el argumento `-sheetUrl=`). Agregar `SubidaDeBug.ForzarEnEditor` (static, false por defecto) para poder subir desde el Editor a la copia.
3. Casos: (a) inserción nueva → Estado=Nuevo y Clase/Área/Prioridad prellenadas; (b) reenviar el mismo `Bug_id` (simula un reintento desde `pendientes/`) → **no** aparece una fila nueva y sí `actualizada:true`; (c) mandar una clave desconocida `Prueba_x` → columna nueva al final del bloque A, no en S; (d) editar a mano Estado→Corregido → `Fecha_corregido` se pone sola; (e) acción `actualizar` con un `curl` → cambia Estado y Commit_fix y no toca el bloque A; (f) cliente viejo sin `Bug_id` → inserta igual.
4. Para (b) y (e) alcanza con `curl -X POST <url-copia> -H "Content-Type: application/json" --data @Registros/pendientes/<n>.json` (adaptado), sin abrir el juego.

### 5.4 Prueba de punta a punta con el .exe
1. Compilar con `CliBuilder` → comprobar que el `build_sello.txt` entró (por ejemplo, que la cabecera del `sesion_*.txt` del exe muestra `0.1.0+<sha>`).
2. Abrir el exe con `subida_url.txt` apuntando a la copia, marcar 2 bugs (uno a pie, uno en el tanque) y cortar la red en el segundo → queda en `pendientes/`. Reabrir el exe → se sube sin duplicar (upsert).
3. Abrir `persistentDataPath/Registros/bugs_columnas.csv` en Excel → tildes bien, 2 filas, los mismos valores que el Sheet.

---

## 6. Orden de ejecución sugerido

| Paso | Contenido | Costo | Depende de |
|------|-----------|-------|------------|
| 1 | Correcciones 2.1 (cultura en Capturar, nota en bugs.txt, plantillas) + prueba 5.1.2/5.1.4 | bajo | — |
| 2 | `ColumnasDeBug` (2.6) con las reformulaciones de §1.4, **sin** columnas nuevas todavía; CSV local (3.2); prueba 5.1.1/5.1.3 + regeneración 3.3 | bajo | 1 |
| 3 | Modelo v2 + buffers + `CapturarContexto` (2.2–2.4) → todas las columnas imprescindibles automáticas | medio | 2 |
| 4 | Sello del build (2.5) + `Bug_id` (2.7) | medio | 2 |
| 5 | Columnas útiles automáticas (Combate, Eventos, Aliados_nav, Sesion, Clasif_auto) + prueba 5.2 | bajo/medio | 3 |
| 6 | Apps Script: upsert, bloques, validación, formato, `onEdit`, migración (§4) contra la copia (5.3) | medio | 4 (necesita Bug_id) |
| 7 | Acción `actualizar` + regla para el agente: al corregir, escribe Estado/Commit/Notas_dev (en el CSV local si es un bug del Editor, por la acción si es del .exe) | bajo | 6 |
| 8 | Punta a punta con el .exe (5.4) y recién ahí cambiar la URL de producción | — | 6 |

Nota aparte (fuera del pedido): el token del Apps Script está escrito en `SubidaDeBug.cs` y, por lo tanto, en el repo. Si el repo se comparte, conviene rotarlo y moverlo a un archivo fuera del control de versiones.
