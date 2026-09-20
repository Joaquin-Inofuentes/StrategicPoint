<#
.SYNOPSIS
    Ayudante de Tools\Qa\qa_total.bat.

.DESCRIPTION
    Todo lo que en un .bat seria una tuberia escapada a mano vive aca: fichas del
    entorno, conteos de la suite, inventario de capturas, vigilancia del
    reproductor del tutorial, recoleccion de logs y el CSV historico.

    Se invoca siempre igual:
        powershell -NoProfile -ExecutionPolicy Bypass -File Tools\Qa\qa_ayuda.ps1 -Accion <accion> [-Out ruta] [-In ruta] [-Extra valor]

    Acciones:
        entorno      ficha de la maquina, git y tamano del proyecto      -> -Out archivo
        suite        cuenta checks OK y fallidos de un log               -> -In log   -Out archivo
        build        peso del build                                      -> -In carpeta -Out archivo
        capturas     inventario de PNG con dimensiones y peso            -> -In carpeta -Out archivo
        spmetrics    rescata el bloque SPMETRICS de un Player.log        -> -In log   -Out archivo
        persistente  copia un archivo de persistentDataPath              -> -Extra nombre -Out destino
        autoplay     lee Logs\Autoplay\estado_actual.json (una linea)    -> -In json
        errores      junta errores de todos los logs                     -> -In carpeta -Out archivo
        crash        copia volcados de crash recientes                   -> -Out carpeta
        historico    suma una fila al CSV historico                      -> -In carpeta de la corrida -Out csv -Extra "sello;modo;fallos"
        epoch        segundos desde epoch (para cronometrar etapas)

    Cada accion escribe en stdout lo que el .bat necesita leer, y nada mas.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Accion,
    [string]$Out = "",
    [string]$In = "",
    [string]$Extra = ""
)

$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference = 'SilentlyContinue'

function Escribir([string]$texto) {
    if ($Out) { $texto | Set-Content -LiteralPath $Out -Encoding UTF8 } else { Write-Output $texto }
}

switch ($Accion) {

    'epoch' {
        [int][double]::Parse((Get-Date -UFormat %s))
    }

    'entorno' {
        $sb = [System.Text.StringBuilder]::new()
        $n = { param($t) [void]$sb.AppendLine($t) }

        & $n "=== Strategic Point - ficha del entorno ==="
        & $n ("fecha         : " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
        & $n ("maquina       : $env:COMPUTERNAME   usuario: $env:USERNAME")
        & $n ("raiz          : " + (Get-Location).Path)
        & $n ""

        & $n "--- git ---"
        & $n ("rama   : " + (git rev-parse --abbrev-ref HEAD 2>$null))
        & $n ("commit : " + (git log -1 --pretty=format:'%H  %ad  %s' 2>$null))
        $sucio = git status --porcelain 2>$null
        & $n ("sin commitear : " + (@($sucio).Count) + " archivos")
        if ($sucio) { $sucio | ForEach-Object { & $n ("    " + $_) } }
        & $n ""

        & $n "--- hardware ---"
        $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
        & $n ("cpu    : {0}  ({1} nucleos, {2} hilos)" -f $cpu.Name, $cpu.NumberOfCores, $cpu.NumberOfLogicalProcessors)
        Get-CimInstance Win32_VideoController | ForEach-Object {
            & $n ("gpu    : {0}  driver {1}" -f $_.Name, $_.DriverVersion)
        }
        $os = Get-CimInstance Win32_OperatingSystem
        & $n ("so     : {0}  {1}" -f $os.Caption, $os.Version)
        & $n ("ram    : {0:N1} GB totales, {1:N1} GB libres" -f ($os.TotalVisibleMemorySize / 1MB), ($os.FreePhysicalMemory / 1MB))
        Get-PSDrive -PSProvider FileSystem | ForEach-Object {
            if ($_.Used -ne $null) { & $n ("disco {0}: {1:N1} GB usados, {2:N1} GB libres" -f $_.Name, ($_.Used / 1GB), ($_.Free / 1GB)) }
        }
        & $n ""

        & $n "--- tamano del proyecto ---"
        $cs = Get-ChildItem -Path 'Assets' -Filter *.cs -Recurse -File
        $lineas = ($cs | Get-Content | Measure-Object -Line).Lines
        & $n ("scripts_cs : " + $cs.Count)
        & $n ("lineas_cs  : " + $lineas)
        & $n ("escenas    : " + (Get-ChildItem 'Assets' -Filter *.unity -Recurse -File).Count)
        & $n ("prefabs    : " + (Get-ChildItem 'Assets' -Filter *.prefab -Recurse -File).Count)
        & $n ("assets_MB  : {0:N1}" -f ((Get-ChildItem 'Assets' -Recurse -File | Measure-Object Length -Sum).Sum / 1MB))
        & $n ""

        & $n "--- unity ---"
        $pv = Get-Content 'ProjectSettings/ProjectVersion.txt' -ErrorAction SilentlyContinue
        $pv | ForEach-Object { & $n ("  " + $_) }

        Escribir $sb.ToString()
    }

    'suite' {
        $t = Get-Content -LiteralPath $In -ErrorAction SilentlyContinue
        $ok = @($t | Select-String -Pattern '\[OK\]' -SimpleMatch).Count
        $ko = @($t | Select-String -Pattern '\[FALLO\]', '\[FAIL\]' -SimpleMatch).Count
        $exc = @($t | Select-String -Pattern 'Exception', 'NullReference' -SimpleMatch).Count
        Escribir "checks_ok=$ok`nchecks_fallidos=$ko`nexcepciones=$exc"
    }

    'build' {
        $b = (Get-ChildItem -LiteralPath $In -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
        Escribir ("build_MB={0:N1}" -f ($b / 1MB))
    }

    'capturas' {
        Add-Type -AssemblyName System.Drawing -ErrorAction SilentlyContinue
        $f = @(Get-ChildItem -LiteralPath $In -Filter *.png -ErrorAction SilentlyContinue | Sort-Object Name)
        $l = @("capturas=" + $f.Count)
        foreach ($p in $f) {
            $dim = '?'
            try {
                $img = [System.Drawing.Image]::FromFile($p.FullName)
                $dim = "$($img.Width)x$($img.Height)"
                $img.Dispose()
            } catch { }
            $l += ("{0};{1};{2:N0}KB" -f $p.Name, $dim, ($p.Length / 1KB))
        }
        Escribir ($l -join "`n")
    }

    'spmetrics' {
        $t = Get-Content -LiteralPath $In -Raw -ErrorAction SilentlyContinue
        if ($t -and $t -match '(?s)SPMETRICS\s*(.*?)(?:\r?\n\r?\n|$)') {
            Escribir $Matches[1].Trim()
        } else {
            Write-Output "sin bloque SPMETRICS"
        }
    }

    'persistente' {
        # persistentDataPath = %USERPROFILE%\AppData\LocalLow\<Compania>\<Producto>
        $base = Join-Path $env:USERPROFILE 'AppData\LocalLow'
        $f = Get-ChildItem -LiteralPath $base -Filter $Extra -Recurse -File -ErrorAction SilentlyContinue |
             Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($f) {
            Copy-Item -LiteralPath $f.FullName -Destination $Out -Force
            Write-Output $f.FullName
        } else {
            Write-Output ""
        }
    }

    'autoplay' {
        # Una sola linea: runId;corriendo;paso;total;id;fallidos;errores;fps
        try {
            $j = Get-Content -LiteralPath $In -Raw -ErrorAction Stop | ConvertFrom-Json
            '{0};{1};{2};{3};{4};{5};{6};{7}' -f `
                $j.runId, $j.corriendo, ($j.indice + 1), $j.total, $j.id, $j.fallidos, $j.errores, [int]$j.fps
        } catch {
            Write-Output ""
        }
    }

    'errores' {
        $patrones = 'error', 'exception', 'fallo', 'failed', 'nullreference', 'missingreference', 'assertion', 'stacktrace'
        $sb = [System.Text.StringBuilder]::new()
        [void]$sb.AppendLine("=== Errores, excepciones y fallos de todos los logs de esta corrida ===")
        [void]$sb.AppendLine("")
        $total = 0
        # Where-Object y no -Include: -Include con -LiteralPath es inconsistente entre versiones.
        $archivos = Get-ChildItem -LiteralPath $In -Recurse -File -ErrorAction SilentlyContinue |
                    Where-Object { ($_.Extension -eq '.txt' -or $_.Extension -eq '.log') -and $_.Name -ne '_TODOS_LOS_ERRORES.txt' } |
                    Sort-Object Name
        foreach ($a in $archivos) {
            $hits = Select-String -LiteralPath $a.FullName -Pattern $patrones -SimpleMatch -ErrorAction SilentlyContinue
            if (-not $hits) { continue }
            $total += @($hits).Count
            [void]$sb.AppendLine("---------- $($a.Name)  ($(@($hits).Count) lineas) ----------")
            foreach ($h in ($hits | Select-Object -First 400)) {
                [void]$sb.AppendLine(("{0,6}: {1}" -f $h.LineNumber, $h.Line.Trim()))
            }
            if (@($hits).Count -gt 400) { [void]$sb.AppendLine("       ... y $((@($hits).Count) - 400) lineas mas") }
            [void]$sb.AppendLine("")
        }
        if ($total -eq 0) { [void]$sb.AppendLine("Ninguna. Esta corrida no dejo errores en ningun log.") }
        $sb.ToString() | Set-Content -LiteralPath $Out -Encoding UTF8

        $peso = (Get-ChildItem -LiteralPath $In -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
        $pesoMB = '{0:N2}' -f ($peso / 1MB)
        $cuantos = @($archivos).Count
        Write-Output "lineas_con_error=$total"
        Write-Output "archivos_de_log=$cuantos"
        Write-Output "peso_logs_MB=$pesoMB"
    }

    'crash' {
        # Solo las carpetas donde Unity y Mono de verdad dejan volcados, y con
        # profundidad acotada: recorrer %LOCALAPPDATA% entero tarda minutos.
        $desde = (Get-Date).AddHours(-4)
        $patronesCrash = 'crash.dmp', 'error.log', 'mono_crash*.json', '*.crash'
        $raices = @(
            $env:TEMP
            (Join-Path $env:LOCALAPPDATA 'Temp')
            (Join-Path $env:LOCALAPPDATA 'CrashDumps')
            (Join-Path $env:LOCALAPPDATA 'Unity\Editor')
            (Get-Location).Path
            (Join-Path (Get-Location).Path 'Builds')
        ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -Unique

        $encontrados = foreach ($r in $raices) {
            Get-ChildItem -LiteralPath $r -File -Depth 2 -ErrorAction SilentlyContinue |
                Where-Object {
                    $nombre = $_.Name
                    $_.LastWriteTime -gt $desde -and @($patronesCrash | Where-Object { $nombre -like $_ }).Count -gt 0
                }
        }
        foreach ($f in $encontrados) {
            Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $Out ("crash_" + $f.Name)) -Force -ErrorAction SilentlyContinue
        }
        Write-Output ("volcados_de_crash=" + @($encontrados).Count)
    }

    'historico' {
        $partes = $Extra -split ';'
        $sello = $partes[0]; $modo = $partes[1]; $fallos = $partes[2]

        function Val([string]$archivo, [string]$clave) {
            $t = Get-Content -LiteralPath (Join-Path $In $archivo) -ErrorAction SilentlyContinue
            foreach ($l in $t) {
                foreach ($tok in ($l -split '\s+')) {
                    if ($tok -like "$clave=*") { return ($tok -split '=', 2)[1] }
                }
            }
            return ''
        }

        $m = 'metricas'
        $fila = @(
            "QA_$sello"
            (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
            $modo
            $fallos
            (Val "$m\suite.txt" 'checks_ok')
            (Val "$m\suite.txt" 'checks_fallidos')
            (Val "$m\spmetrics.txt" 'fps_prom')
            (Val "$m\spmetrics.txt" 'p95_ms')
            (Val "$m\spmetrics.txt" 'p99_ms')
            (Val "$m\spmetrics.txt" 'heap_gestionado_MB')
            (Val "$m\build.txt" 'build_MB')
            (Val "$m\capturas.txt" 'capturas')
            (Val "$m\autoplay.txt" 'capturas_tutorial')
            (Val "$m\errores.txt" 'lineas_con_error')
        ) -join ';'

        if (-not (Test-Path -LiteralPath $Out)) {
            'corrida;fecha;modo;etapas_fallidas;checks_ok;checks_fallidos;fps_prom;p95_ms;p99_ms;heap_MB;build_MB;capturas;capturas_tutorial;lineas_con_error' |
                Set-Content -LiteralPath $Out -Encoding UTF8
        }
        Add-Content -LiteralPath $Out -Value $fila -Encoding UTF8
        Write-Output $fila
    }

    default {
        Write-Error "Accion desconocida: $Accion"
        exit 2
    }
}
