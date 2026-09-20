@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ===========================================================================
rem  Strategic Point - QA TOTAL
rem  Una sola corrida que deja capturas, metricas y logs de todo tipo, indexados.
rem
rem  Uso:
rem     Tools\Qa\qa_total.bat                  corrida completa (~45 min)
rem     Tools\Qa\qa_total.bat --rapido         sin flakiness ni estres (~12 min)
rem     Tools\Qa\qa_total.bat --solo suite     una sola etapa
rem     Tools\Qa\qa_total.bat --saltear autoplay,flaky
rem     Tools\Qa\qa_total.bat --abrir          abre el informe al terminar
rem     Tools\Qa\qa_total.bat --ayuda
rem
rem  Etapas, en orden:
rem     entorno    ficha de la maquina, Unity, git, disco, tamano del proyecto
rem     estatico   Tools\Ci\verificar_estatico.py
rem     compilar   compilacion limpia en batchmode
rem     suite      HeadlessTestRunner.RunAll
rem     bench      benchmark, equivalencia de grilla, banco de balance, estres 50+
rem     flaky      100 iteraciones (fugas de estado entre corridas)
rem     build      CliBuilder.BuildWindows64
rem     metricas   el .exe con -spmetrics (fps, p95, p99, GC, memoria)
rem     capturas   el .exe con -spshot en 7 resoluciones, incluida 4K
rem     autoplay   el tutorial completo con el reproductor automatico + capturas
rem     logs       junta TODOS los logs (editor, player, proyecto, crash)
rem     indice     arma el informe: INFORME.md, INFORME.html, historico.csv
rem
rem  Salida:  Reportes\QA_aaaammdd_hhmmss\
rem  Codigo:  0 todo bien | 1 alguna etapa fallo | 2 no se pudo correr
rem
rem  Todo lo que seria una tuberia escapada a mano vive en qa_ayuda.ps1.
rem ===========================================================================

rem --------------------------------------------------------------- Rutas
set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%..\.." 2>nul || (echo ERROR: no se pudo ubicar la raiz del proyecto.& exit /b 2)
set "RAIZ=%CD%"

if not exist "%RAIZ%\Assets\_Project" (
  echo ERROR: "%RAIZ%" no parece la raiz de Strategic Point ^(falta Assets\_Project^).
  popd & exit /b 2
)

set "AYUDA=%RAIZ%\Tools\Qa\qa_ayuda.ps1"
if not exist "%AYUDA%" (
  echo ERROR: falta Tools\Qa\qa_ayuda.ps1
  popd & exit /b 2
)
rem Sin comillas exteriores a proposito: el valor YA lleva las comillas de la ruta.
set PS=powershell -NoProfile -ExecutionPolicy Bypass -File "%AYUDA%"

rem --------------------------------------------------------- Argumentos
set "MODO=completo"
set "SOLO="
set "SALTEAR=,"
set "ABRIR=0"

:args
if "%~1"=="" goto fin_args
if /i "%~1"=="--ayuda"   goto ayuda
if /i "%~1"=="-h"        goto ayuda
if /i "%~1"=="--rapido"  (set "MODO=rapido" & set "SALTEAR=!SALTEAR!flaky,estres," & shift & goto args)
if /i "%~1"=="--abrir"   (set "ABRIR=1" & shift & goto args)
if /i "%~1"=="--solo"    (set "SOLO=,%~2," & shift & shift & goto args)
if /i "%~1"=="--saltear" (set "SALTEAR=!SALTEAR!%~2," & shift & shift & goto args)
echo ERROR: argumento desconocido "%~1". Corre --ayuda.
popd & exit /b 2
:fin_args

rem ------------------------------------------------ Unity y herramientas
if not defined UNITY_VER (
  for /f "usebackq tokens=2" %%V in (`findstr /b "m_EditorVersion:" "%RAIZ%\ProjectSettings\ProjectVersion.txt" 2^>nul`) do set "UNITY_VER=%%V"
)
if not defined UNITY_VER set "UNITY_VER=6000.5.6f1"
if not defined UNITY_EXE set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\%UNITY_VER%\Editor\Unity.exe"

if not exist "%UNITY_EXE%" (
  echo ERROR: no se encontro Unity en:
  echo        %UNITY_EXE%
  echo        Defini la ruta a mano:  set "UNITY_EXE=D:\ruta\Unity.exe"
  popd & exit /b 2
)

set "PY="
where py >nul 2>&1 && set "PY=py -3"
if not defined PY (where python >nul 2>&1 && set "PY=python")

rem -------------------------------------------------- Carpeta del informe
for /f "usebackq tokens=*" %%T in (`powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"`) do set "SELLO=%%T"
set "OUT=%RAIZ%\Reportes\QA_%SELLO%"
set "L=%OUT%\logs"
set "C=%OUT%\capturas"
set "M=%OUT%\metricas"
set "A=%OUT%\autoplay"
for %%D in ("%OUT%" "%L%" "%C%" "%M%" "%A%" "%OUT%\artefactos") do if not exist "%%~D" mkdir "%%~D" >nul 2>&1

set "DIARIO=%OUT%\diario.txt"
set "ESTADO=%OUT%\estado.csv"
echo etapa;resultado;segundos;detalle> "%ESTADO%"

set "FALLOS=0"
set "CHK_OK=" & set "CHK_KO=" & set "BMB=" & set "NCAP=" & set "RES="

call :log "=========================================================="
call :log " Strategic Point - QA TOTAL"
call :log "   raiz    : %RAIZ%"
call :log "   unity   : %UNITY_EXE%"
call :log "   modo    : %MODO%"
call :log "   informe : %OUT%"
call :log "=========================================================="

rem ===========================================================================
call :correr entorno
if "%SALTAR%"=="0" (
  %PS% -Accion entorno -Out "%OUT%\00_entorno.txt" >nul 2>&1
  >>"%OUT%\00_entorno.txt" echo unity_exe     : %UNITY_EXE%
  call :fin entorno 0 "ficha escrita"
)

rem ===========================================================================
call :correr estatico
if "%SALTAR%"=="0" (
  if defined PY (
    %PY% "%RAIZ%\Tools\Ci\verificar_estatico.py" > "%L%\estatico.txt" 2>&1
    call :fin estatico !ERRORLEVEL! "ver logs\estatico.txt"
  ) else (
    echo Python no encontrado: la verificacion estatica se saltea.> "%L%\estatico.txt"
    call :fin estatico 0 "OMITIDA (sin python)"
  )
)

rem ===========================================================================
call :correr compilar
if "%SALTAR%"=="0" (
  call :unity "%L%\compilar.txt" -quit
  call :fin compilar !ERRORLEVEL! "ver logs\compilar.txt"
)

rem ===========================================================================
call :correr suite
if "%SALTAR%"=="0" (
  call :unity "%L%\suite.txt" -quit -executeMethod SP.EditorTools.HeadlessTestRunner.RunAll
  set "RC=!ERRORLEVEL!"
  %PS% -Accion suite -In "%L%\suite.txt" -Out "%M%\suite.txt" >nul 2>&1
  call :leer "%M%\suite.txt" checks_ok CHK_OK
  call :leer "%M%\suite.txt" checks_fallidos CHK_KO
  call :fin suite !RC! "checks OK=!CHK_OK! fallidos=!CHK_KO!"
)

rem ===========================================================================
call :correr bench
if "%SALTAR%"=="0" (
  set /a "RC=0"
  call :unity "%L%\bench.txt"        -quit -executeMethod SP.EditorTools.HeadlessTestRunner.RunPerformanceBenchmarks
  set /a "RC=!RC!+!ERRORLEVEL!"
  call :unity "%L%\equivalencia.txt" -quit -executeMethod SP.EditorTools.HeadlessTestRunner.RunEquivalenceCheck
  set /a "RC=!RC!+!ERRORLEVEL!"
  call :unity "%L%\balance.txt"      -quit -executeMethod SP.EditorTools.BalanceBench.Correr
  set /a "RC=!RC!+!ERRORLEVEL!"
  echo !SALTEAR! | findstr /i /c:",estres," >nul
  if errorlevel 1 (
    call :unity "%L%\estres.txt" -quit -executeMethod SP.EditorTools.HeadlessTestRunner.RunStressScenarios
    set /a "RC=!RC!+!ERRORLEVEL!"
  ) else (
    echo estres OMITIDO por --rapido> "%L%\estres.txt"
  )
  call :fin bench !RC! "bench + equivalencia + balance + estres"
)

rem ===========================================================================
call :correr flaky
if "%SALTAR%"=="0" (
  call :unity "%L%\flaky.txt" -quit -executeMethod SP.EditorTools.HeadlessTestRunner.RunManyHeadless
  call :fin flaky !ERRORLEVEL! "100 iteraciones - ver logs\flaky.txt"
)

rem ===========================================================================
set "EXE=%RAIZ%\Builds\Windows64\StrategicPoint.exe"
call :correr build
if "%SALTAR%"=="0" (
  call :unity "%L%\build.txt" -quit -executeMethod SP.EditorTools.CliBuilder.BuildWindows64
  set "RC=!ERRORLEVEL!"
  if exist "%EXE%" (
    %PS% -Accion build -In "%RAIZ%\Builds\Windows64" -Out "%M%\build.txt" >nul 2>&1
    call :leer "%M%\build.txt" build_MB BMB
    call :fin build !RC! "OK - !BMB! MB"
  ) else (
    call :fin build 1 "no se genero el .exe"
  )
)

rem ===========================================================================
call :correr metricas
if "%SALTAR%"=="0" (
  if exist "%EXE%" (
    call :log "     corriendo el build con -spmetrics (unos 30 s)..."
    start /wait "" "%EXE%" -spmetrics -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "%L%\player_metricas.txt"
    %PS% -Accion persistente -Extra spmetrics.txt -Out "%M%\spmetrics.txt" >nul 2>&1
    if not exist "%M%\spmetrics.txt" (
      %PS% -Accion spmetrics -In "%L%\player_metricas.txt" -Out "%M%\spmetrics.txt" >nul 2>&1
    )
    if exist "%M%\spmetrics.txt" ( call :fin metricas 0 "spmetrics.txt recogido" ) else ( call :fin metricas 1 "sin spmetrics.txt" )
  ) else ( call :fin metricas 2 "sin build" )
)

rem ===========================================================================
rem  7 resoluciones. Delimitador ';' porque las etiquetas llevan ':' (16:9).
rem ===========================================================================
call :correr capturas
if "%SALTAR%"=="0" (
  if exist "%EXE%" (
    set "N=0"
    for %%R in (
      "800;600;4por3_minimo;1"
      "1024;768;4por3;1"
      "1280;720;16por9_base;1"
      "1600;900;16por9_medio;1"
      "1920;1080;16por9_full;1"
      "2560;1080;21por9_ultrawide;1"
      "1920;1080;4K_supersampling_x2;2"
    ) do (
      for /f "tokens=1,2,3,4 delims=;" %%W in (%%R) do (
        set /a "N+=1"
        set "NN=0!N!"
        set "NN=!NN:~-2!"
        call :log "     captura !NN!: %%Y  (%%Wx%%X, supersampling x%%Z)"
        start /wait "" "%EXE%" -spshot "%C%\!NN!_%%Y_%%Wx%%X.png" -spsuper %%Z -screen-width %%W -screen-height %%X -screen-fullscreen 0 -logFile "%L%\player_shot_!NN!.txt"
      )
    )
    %PS% -Accion capturas -In "%C%" -Out "%M%\capturas.txt" >nul 2>&1
    call :leer "%M%\capturas.txt" capturas NCAP
    if "!NCAP!"=="" set "NCAP=0"
    if !NCAP! GEQ 7 ( call :fin capturas 0 "!NCAP! capturas" ) else ( call :fin capturas 1 "solo !NCAP! de 7" )
  ) else ( call :fin capturas 2 "sin build" )
)

rem ===========================================================================
rem  Tutorial automatico. Necesita el Editor CON interfaz: Play mode no corre
rem  en -batchmode. Se vigila Logs\Autoplay\estado_actual.json, que escribe el
rem  propio juego una vez por segundo: no hace falta el CLI de Unity.
rem ===========================================================================
call :correr autoplay
if "%SALTAR%"=="0" (
  set "EST=%RAIZ%\Logs\Autoplay\estado_actual.json"
  if exist "!EST!" del /q "!EST!" >nul 2>&1
  call :log "     abriendo el Editor y lanzando el reproductor del tutorial..."
  start "" "%UNITY_EXE%" -projectPath "%RAIZ%" -logFile "%L%\autoplay_editor.txt" -executeMethod SP.EditorTools.TutorialAutoPlayerMenu.ReproducirTutorialAutomatico
  call :vigilar_autoplay
  call :log "     cerrando el Editor..."
  taskkill /im Unity.exe /f >nul 2>&1
  timeout /t 5 /nobreak >nul

  if defined RUNID (
    if exist "%RAIZ%\Logs\Autoplay\!RUNID!" xcopy /e /i /y /q "%RAIZ%\Logs\Autoplay\!RUNID!" "%A%\!RUNID!" >nul 2>&1
    set "RES=desconocido"
    for /f "usebackq tokens=*" %%K in (`powershell -NoProfile -Command "try{(Get-Content '%A%\!RUNID!\resumen.json' -Raw ^| ConvertFrom-Json).resultado}catch{'desconocido'}"`) do set "RES=%%K"
    for /f "usebackq tokens=*" %%K in (`powershell -NoProfile -Command "@(Get-ChildItem '%A%' -Filter *.jpg -Recurse -ErrorAction SilentlyContinue).Count"`) do echo capturas_tutorial=%%K> "%M%\autoplay.txt"
    if /i "!RES!"=="COMPLETA" ( call :fin autoplay 0 "!RES! (!RUNID!)" ) else ( call :fin autoplay 1 "!RES! (!RUNID!)" )
  ) else (
    call :fin autoplay 1 "el reproductor no llego a arrancar"
  )
)

rem ===========================================================================
call :correr logs
if "%SALTAR%"=="0" (
  call :log "     juntando logs..."

  if exist "%LOCALAPPDATA%\Unity\Editor\Editor.log"      copy /y "%LOCALAPPDATA%\Unity\Editor\Editor.log"      "%L%\Editor.log"      >nul 2>&1
  if exist "%LOCALAPPDATA%\Unity\Editor\Editor-prev.log" copy /y "%LOCALAPPDATA%\Unity\Editor\Editor-prev.log" "%L%\Editor-prev.log" >nul 2>&1

  %PS% -Accion persistente -Extra Player.log      -Out "%L%\Player.log"      >nul 2>&1
  %PS% -Accion persistente -Extra Player-prev.log -Out "%L%\Player-prev.log" >nul 2>&1

  if exist "%RAIZ%\Logs" xcopy /e /i /y /q "%RAIZ%\Logs" "%L%\proyecto_Logs" >nul 2>&1
  if exist "%RAIZ%\Temp\pipeline_recompile_status.json" copy /y "%RAIZ%\Temp\pipeline_recompile_status.json" "%L%\recompile_status.json" >nul 2>&1
  if exist "%RAIZ%\Temp\suite_result.txt"               copy /y "%RAIZ%\Temp\suite_result.txt"               "%L%\suite_result.txt"     >nul 2>&1

  for %%F in (
    "%RAIZ%\Docs\Tutorial_Log_corrida_final.txt"
    "%RAIZ%\Docs\Ronda6_TAB_Caos_Log.txt"
    "%RAIZ%\Docs\ARTS_USO.csv"
    "%RAIZ%\Docs\AUDITORIA_100_ITEMS.md"
  ) do if exist %%F copy /y %%F "%OUT%\artefactos\" >nul 2>&1

  %PS% -Accion crash   -Out "%L%"                               >  "%M%\errores.txt" 2>nul
  %PS% -Accion errores -In "%L%" -Out "%L%\_TODOS_LOS_ERRORES.txt" >> "%M%\errores.txt" 2>nul

  call :fin logs 0 "logs recogidos"
)

rem ===========================================================================
call :correr indice
if "%SALTAR%"=="0" (
  call :informe
  %PS% -Accion historico -In "%OUT%" -Out "%RAIZ%\Reportes\historico.csv" -Extra "%SELLO%;%MODO%;%FALLOS%" >nul 2>&1
  if defined PY %PY% "%RAIZ%\Tools\Qa\informe_html.py" "%OUT%" >nul 2>&1
  call :fin indice 0 "INFORME.md + INFORME.html + historico.csv"
)

rem ===========================================================================
call :log ""
call :log "=========================================================="
type "%ESTADO%"
call :log "=========================================================="
call :log "  Informe : %OUT%\INFORME.html"
call :log "  Etapas fallidas : %FALLOS%"
call :log "=========================================================="

if "%ABRIR%"=="1" (
  if exist "%OUT%\INFORME.html" (start "" "%OUT%\INFORME.html") else (start "" "%OUT%\INFORME.md")
)

popd
if %FALLOS% GTR 0 (exit /b 1) else (exit /b 0)

rem ===========================================================================
rem  Subrutinas
rem ===========================================================================

:ayuda
echo.
echo   Tools\Qa\qa_total.bat [opciones]
echo.
echo     --rapido            sin flakiness ni estres
echo     --solo ETAPA        corre una sola etapa
echo     --saltear A,B,C     saltea esas etapas
echo     --abrir             abre el informe al terminar
echo     --ayuda             esto
echo.
echo   Etapas: entorno estatico compilar suite bench flaky build metricas
echo           capturas autoplay logs indice
echo.
echo   Variables de entorno:
echo     UNITY_EXE   ruta al Unity.exe (por defecto: la de ProjectVersion.txt)
echo.
popd & exit /b 0

rem --- :correr ETAPA   decide si se corre (deja SALTAR) y arranca el reloj
:correr
set "ETAPA=%~1"
set "SALTAR=0"
if defined SOLO (
  echo !SOLO! | findstr /i /c:",%ETAPA%," >nul || set "SALTAR=1"
)
echo !SALTEAR! | findstr /i /c:",%ETAPA%," >nul && set "SALTAR=1"
if "!SALTAR!"=="1" (
  call :log "[ -- ] %ETAPA%  OMITIDA"
  >>"%ESTADO%" echo %ETAPA%;OMITIDA;0;saltada por argumento
  exit /b 0
)
call :log ""
call :log "[ .. ] %ETAPA%"
for /f "usebackq tokens=*" %%T in (`%PS% -Accion epoch`) do set "TINI=%%T"
exit /b 0

rem --- :fin ETAPA RC DETALLE
:fin
for /f "usebackq tokens=*" %%T in (`%PS% -Accion epoch`) do set "TFIN=%%T"
set /a "SEG=!TFIN!-!TINI!"
if "%~2"=="0" (
  call :log "[ OK ] %~1  [!SEG! s]  %~3"
  >>"%ESTADO%" echo %~1;OK;!SEG!;%~3
) else (
  set /a "FALLOS+=1"
  call :log "[FALL] %~1  [!SEG! s]  rc=%~2  %~3"
  >>"%ESTADO%" echo %~1;FALLO;!SEG!;rc=%~2 %~3
)
exit /b 0

rem --- :unity LOGFILE [args...]
:unity
set "ULOG=%~1"
shift
set "UARGS="
:unity_args
if "%~1"=="" goto unity_go
set "UARGS=!UARGS! %1"
shift
goto unity_args
:unity_go
"%UNITY_EXE%" -batchmode -nographics -projectPath "%RAIZ%" -logFile "%ULOG%" !UARGS!
exit /b %ERRORLEVEL%

rem --- :leer ARCHIVO CLAVE VARIABLE   (lee "clave=valor" de un archivo)
:leer
set "%~3="
for /f "usebackq tokens=2 delims==" %%N in (`findstr /b /c:"%~2=" "%~1" 2^>nul`) do set "%~3=%%N"
exit /b 0

rem --- :vigilar_autoplay   sigue estado_actual.json hasta que termine
:vigilar_autoplay
set "RUNID="
set "PREV="
set "SIN_CAMBIO=0"
set "VUELTAS=0"
:vigilar_bucle
set /a "VUELTAS+=1"
if !VUELTAS! GTR 2400 (
  call :log "     ERROR: 40 min de tope alcanzados. Se corta."
  exit /b 1
)
set "LINEA="
for /f "usebackq tokens=*" %%K in (`%PS% -Accion autoplay -In "%EST%"`) do set "LINEA=%%K"
if defined LINEA (
  for /f "tokens=1,2,3,4,5,6,7,8 delims=;" %%a in ("!LINEA!") do (
    set "RUNID=%%a"
    if not "!PREV!"=="%%c/%%e" (
      call :log "     paso %%c/%%d  %%e   fallidos=%%f errores=%%g fps=%%h"
      set "PREV=%%c/%%e"
      set "SIN_CAMBIO=0"
    )
    if /i "%%b"=="False" exit /b 0
  )
)
set /a "SIN_CAMBIO+=1"
if !SIN_CAMBIO! GTR 300 (
  call :log "     ERROR: 300 s sin novedades del reproductor. Se corta."
  exit /b 1
)
timeout /t 1 /nobreak >nul
goto vigilar_bucle

rem --- :informe   arma INFORME.md
:informe
set "MD=%OUT%\INFORME.md"
> "%MD%" echo # Strategic Point - Informe de QA
>>"%MD%" echo.
>>"%MD%" echo **Corrida:** `QA_%SELLO%` ^| **Modo:** %MODO% ^| **Maquina:** %COMPUTERNAME% ^| **Etapas fallidas:** %FALLOS%
>>"%MD%" echo.
>>"%MD%" echo ## Etapas
>>"%MD%" echo.
>>"%MD%" echo ^| Etapa ^| Resultado ^| Segundos ^| Detalle ^|
>>"%MD%" echo ^|---^|---^|---^|---^|
for /f "usebackq skip=1 tokens=1,2,3,4 delims=;" %%a in ("%ESTADO%") do >>"%MD%" echo ^| %%a ^| %%b ^| %%c ^| %%d ^|
>>"%MD%" echo.
>>"%MD%" echo ## Metricas del build real
>>"%MD%" echo.
>>"%MD%" echo ```
if exist "%M%\spmetrics.txt" (type "%M%\spmetrics.txt" >> "%MD%") else (>>"%MD%" echo sin metricas)
>>"%MD%" echo ```
>>"%MD%" echo.
>>"%MD%" echo ## Capturas
>>"%MD%" echo.
for %%P in ("%C%\*.png") do (
  >>"%MD%" echo ### %%~nP
  >>"%MD%" echo.
  >>"%MD%" echo ^^![%%~nP]^(capturas/%%~nxP^)
  >>"%MD%" echo.
)
>>"%MD%" echo ## Tutorial automatico
>>"%MD%" echo.
>>"%MD%" echo ```
for /f "usebackq tokens=*" %%K in (`powershell -NoProfile -Command "Get-ChildItem '%A%' -Filter resumen.txt -Recurse -ErrorAction SilentlyContinue ^| Select-Object -First 1 -ExpandProperty FullName"`) do if exist "%%K" type "%%K" >> "%MD%"
>>"%MD%" echo ```
>>"%MD%" echo.
>>"%MD%" echo ## Errores en todos los logs
>>"%MD%" echo.
>>"%MD%" echo ```
if exist "%L%\_TODOS_LOS_ERRORES.txt" (
  powershell -NoProfile -Command "Get-Content '%L%\_TODOS_LOS_ERRORES.txt' -TotalCount 200" >> "%MD%" 2>nul
) else (
  >>"%MD%" echo sin archivo de errores
)
>>"%MD%" echo ```
>>"%MD%" echo.
>>"%MD%" echo ## Donde esta cada cosa
>>"%MD%" echo.
>>"%MD%" echo ^| Carpeta ^| Que hay ^|
>>"%MD%" echo ^|---^|---^|
>>"%MD%" echo ^| `capturas/` ^| El build en 7 resoluciones, de 4:3 a 4K por supersampling ^|
>>"%MD%" echo ^| `autoplay/` ^| Recorrido del tutorial: log.jsonl, resumen y una captura por paso ^|
>>"%MD%" echo ^| `metricas/` ^| spmetrics.txt, conteos de la suite, peso del build, inventario de capturas ^|
>>"%MD%" echo ^| `logs/` ^| Editor.log, Player.log, log de cada etapa, Logs/ del proyecto, volcados de crash ^|
>>"%MD%" echo ^| `logs/_TODOS_LOS_ERRORES.txt` ^| Todas las lineas con error o excepcion, de todos los logs juntos ^|
>>"%MD%" echo ^| `artefactos/` ^| Documentos que la suite genera o actualiza ^|
>>"%MD%" echo ^| `estado.csv` ^| Una fila por etapa ^|
>>"%MD%" echo ^| `../historico.csv` ^| Una fila por corrida, para comparar rondas entre si ^|
exit /b 0

rem --- :log TEXTO
:log
echo %~1
>>"%DIARIO%" echo %~1
exit /b 0
