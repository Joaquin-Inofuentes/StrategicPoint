# QA automático — `Tools/Qa/qa_total.bat`

Una sola corrida que deja **capturas, métricas y logs de todo tipo**, indexados en un informe que se
puede comprimir y abrir en otra máquina.

```bat
Tools\Qa\qa_total.bat                    corrida completa (~45 min)
Tools\Qa\qa_total.bat --rapido           sin flakiness ni estres (~12 min)
Tools\Qa\qa_total.bat --solo capturas    una sola etapa
Tools\Qa\qa_total.bat --saltear autoplay,flaky
Tools\Qa\qa_total.bat --abrir            abre el informe al terminar
Tools\Qa\qa_total.bat --ayuda
```

Código de salida: **0** todo bien · **1** alguna etapa falló · **2** no se pudo correr.
Por eso sirve tal cual en CI y en un gancho de pre-push.

---

## Las 12 etapas

| # | Etapa | Qué hace | Cuánto tarda | Qué deja |
|---|---|---|---|---|
| 1 | `entorno` | Ficha de la máquina: CPU, GPU, RAM, disco, rama y commit de git, tamaño del proyecto | 5 s | `00_entorno.txt` |
| 2 | `estatico` | `Tools/Ci/verificar_estatico.py` (sin licencia de Unity) | 10 s | `logs/estatico.txt` |
| 3 | `compilar` | Compilación limpia en batchmode; aborta si hay errores | 1–3 min | `logs/compilar.txt`, `logs/compilar_errores.txt` |
| 4 | `suite` | `HeadlessTestRunner.RunAll` | 3–6 min | `logs/suite.txt`, conteo de checks OK/fallidos |
| 5 | `bench` | Benchmark de rendimiento + equivalencia de grilla + banco de balance + estrés 50+ | 4–8 min | `logs/bench.txt`, `equivalencia.txt`, `balance.txt`, `estres.txt` |
| 6 | `flaky` | `RunManyHeadless` — 100 iteraciones, para cazar fugas de estado entre corridas | 10–20 min | `logs/flaky.txt` |
| 7 | `build` | `CliBuilder.BuildWindows64` | 3–8 min | `Builds/Windows64/`, peso del build |
| 8 | `metricas` | El `.exe` con `-spmetrics`: fps promedio, p95, p99, máximo, GC por frame, recolecciones, heap, memoria total, conteos de canvas y de actores, GPU y resolución | 40 s | `metricas/spmetrics.txt` |
| 9 | `capturas` | El `.exe` con `-spshot` en **7 resoluciones**: 800×600, 1024×768 (4:3), 1280×720, 1600×900, 1920×1080, 2560×1080 (21:9) y 4K por supersampling ×2 | 2 min | `capturas/*.png` + inventario con dimensiones y peso |
| 10 | `autoplay` | El **tutorial entero** con el reproductor automático: gestos reales, una captura por paso, log por sub-paso | 11–19 min | `autoplay/<runId>/{log.jsonl, resumen.txt, resumen.json, cap/*.jpg}` |
| 11 | `logs` | Junta **todo**: `Editor.log`, `Editor-prev.log`, `Player.log`, el log de cada etapa, `Logs/` del proyecto, estado de recompilación, volcados de crash de las últimas 3 h, y extrae todas las líneas con error/excepción/fallo de todos ellos a un solo archivo | 20 s | `logs/**`, `logs/_TODOS_LOS_ERRORES.txt` |
| 12 | `indice` | Arma `INFORME.md`, `INFORME.html` (galería navegable) y suma una fila a `Reportes/historico.csv` | 10 s | El informe |

---

## Qué queda en la carpeta

```
Reportes/QA_20260920_143012/
├── INFORME.html          galeria navegable: etapas, metricas, capturas, errores
├── INFORME.md            lo mismo en texto
├── estado.csv            una fila por etapa (etapa;resultado;segundos;detalle)
├── diario.txt            la consola completa de la corrida
├── 00_entorno.txt        maquina, GPU, git, tamano del proyecto
├── capturas/             01_4:3_minimo_800x600.png ... 07_4K_supersampling_x2.png
├── autoplay/<runId>/     log.jsonl, resumen.txt, resumen.json, cap/paso_NN.jpg
├── metricas/
│   ├── spmetrics.txt     fps, p95, p99, GC, memoria, conteos de escena
│   ├── suite.txt         checks_ok / checks_fallidos
│   ├── build.txt         peso del build en MB
│   ├── capturas.txt      inventario: nombre;WxH;peso
│   └── errores.txt       lineas con error, archivos de log, peso total
├── logs/
│   ├── Editor.log, Editor-prev.log, Player.log, Player-prev.log
│   ├── compilar.txt, suite.txt, bench.txt, build.txt, flaky.txt, ...
│   ├── player_metricas.txt, player_shot_01..07.txt
│   ├── proyecto_Logs/    todo lo que el juego escribe en Logs/
│   ├── crash_*.dmp       volcados, si los hubo
│   └── _TODOS_LOS_ERRORES.txt
└── artefactos/           documentos que la suite genera o actualiza
```

Y, fuera de la carpeta de la corrida:

```
Reportes/historico.csv    una fila por corrida:
                          corrida;fecha;modo;etapas_fallidas;checks_ok;checks_fallidos;
                          fps_prom;p95_ms;p99_ms;heap_MB;build_MB;capturas;autoplay
```

Ese CSV es el que permite contestar **"¿esta ronda mejoró o empeoró?"** sin abrir nada: el p99 y el
heap de la ronda 10 al lado de los de la ronda 9.

---

## Requisitos

| | |
|---|---|
| **Unity** | Se toma de `ProjectSettings/ProjectVersion.txt`. Para forzar otra: `set "UNITY_EXE=D:\ruta\Unity.exe"` |
| **Python** | Opcional. Sin él se saltean `estatico` y el HTML bonito (queda el HTML de respaldo que arma PowerShell) |
| **PowerShell** | Viene con Windows. Se usa para JSON, fechas y medición de archivos |
| **Licencia de Unity** | Sólo para las etapas que abren Unity (3–7 y 10) |

La etapa **`autoplay` abre el Editor con interfaz**: Play mode no corre en `-batchmode`. Mientras corre,
el teclado y el mouse reales quedan desactivados (`EntradaVirtual.IgnorarHumano`); el `.bat` cierra el
Editor al terminar. Si algo se corta a mitad y el teclado queda tomado, la salida de emergencia es el
menú `Strategic Point > Tutorial > Restaurar entrada humana`.

---

## Cómo se vigila el tutorial sin depender del CLI de Unity

El reproductor escribe `Logs/Autoplay/estado_actual.json` **una vez por segundo desde el propio juego**.
El `.bat` lo lee con PowerShell y va imprimiendo el avance:

```
    paso 12/59  granada     fallidos=0 errores=0 fps=143
    paso 13/59  suministros fallidos=0 errores=0 fps=139
```

Si el archivo se congela 300 s, corta y marca la etapa como fallida. **No** consulta al Editor por
IPC, así que no necesita el CLI de Unity ni git-bash: funciona con un `.bat` pelado.

---

## Integración con CI

`.github/workflows/tests.yml` corre hoy dos trabajos: `estatico` (siempre) y `suite` (si hay licencia).
Las etapas 8 a 10 de este `.bat` **no** tienen sentido en un runner sin GPU ni pantalla. El reparto
natural es:

| Dónde | Qué |
|---|---|
| **GitHub Actions** | `estatico`, `compilar`, `suite`, `bench` — lo que corre sin pantalla |
| **Máquina local, antes de cerrar una ronda** | La corrida completa, incluidas capturas, métricas de build y el tutorial |

Para el gancho de pre-push alcanza con:

```bat
Tools\Qa\qa_total.bat --solo estatico,compilar,suite || exit /b 1
```

---

## Relación con los otros documentos de la ronda 10

| Documento | Qué aporta a esta corrida |
|---|---|
| `Docs/RONDA_10_BUGS.md` | Los 100 bugs que esta corrida tendría que empezar a detectar |
| `Docs/RONDA_10_PLAN_CORRECCION.md` | Los criterios de aceptación que el `.bat` verifica (§3 de ese documento) |
| `Docs/RONDA_10_TUTORIAL_22_PASOS_NUEVOS.md` | Los 22 pasos nuevos: la etapa `autoplay` pasa de 36 a 59 pasos y de 36 a 61 capturas |
| `Docs/INDICE_IA/` | El índice del proyecto, para saber qué toca cada arreglo antes de correr nada |
