param([string]$Salida = "C:\_Proyectos privados\DV_C6_StrategicPoint\Assets\Validacion~\atlas_antes_despues_2026-11-06.jpg")
Add-Type -AssemblyName System.Drawing
$val = "C:\_Proyectos privados\DV_C6_StrategicPoint\Assets\Validacion~"
$antes = "C:\Users\PC_JOACO\AppData\Local\Temp\claude\d439f83e1c25b471\524bcfde-ac62-4aed-b14d-644dffdc437e\scratchpad\hoja2\antes"

# titulo, antes, despues, texto
$filas = @(
  @("Soldado ASALTO vs FRANCOTIRADOR", "$antes\sin_foto.jpg", "$val\v4_601_clases_despues.jpg",
    "ANTES: el asalto era una mancha naranja lisa y el francotirador otra verde; las dos perdian el dibujo del trimsheet y 'se veian rotas'.`nDESPUES: cada clase recolorea las CELDAS del trimsheet (8x8). ASALTO (derecha): uniforme azul vivo, chaleco/casco dorado, pantalon pizarra. FRANCOTIRADOR (izquierda): musgo oscuro, capucha casi negra, pantalon tierra. Los dos llevan correaje azul de bando. Medico (centro) sin cambios."),
  @("#345  Terminal: gran explosion", "$antes\b345.jpg", "$val\v4_345_despues_a.jpg",
    "ANTES: al terminar el hackeo la terminal solo apagaba pantallas y quedaba en pie.`nDESPUES: estalla en cadena (fogonazo grande + 3 secundarias + humo), la maquina se hace trozos que vuelan y el muro/porton de la salida norte cae a los 0,35 s: una sola explosion grande. (captura a los 0,15 s del estallido)"),
  @("#346  Quitar flechas de la ruta", "$antes\b346.jpg", "$val\v4_346_despues.jpg",
    "ANTES: chevrones amarillos pintados y chevrones naranjas ambientales sobre la calzada.`nDESPUES: se quitaron las 53 flechas pintadas de las 4 escenas, las 4 flechas de neon de la ciudad y la generacion de chevrones de la ruta. Quedan solo las lineas."),
  @("#347  Dano al tanque / modo dios", "$antes\b347.jpg", "$val\v4_347_despues.png",
    "ANTES: el tanque llegaba al 57 % tras ~90 s de fuego (poco dano).`nDESPUES: el multiplicador de dano al tanque sube de x2 a x4 (40 impactos le sacan 132 HP = 8,3 % de la vida). Con MODO DIOS el tanque es indestructible: 400 cohetes no le sacan nada (HUD al 100 %)."),
  @("#348 / #353  Se traba todo", "$antes\b348.jpg", "$antes\b353.jpg",
    "(izq: #348, der: #353; ambas son capturas del problema, no hay 'despues') SIN CAMBIO VISUAL.Medicion en el Editor: ~37-44 fps con 15.700 renderers, 3,6 M de triangulos y 457 luces; los scripts suman 2 ms y la fisica 0,3 ms: el cuello es el dibujo, no la logica. Probe recortar luces/decorado lejano y NO mejoro (37,1 -> 37,9 fps), asi que lo retire para no arriesgar. Pendiente: bajar triangulos/luces de fondo."),
  @("#349  Faltan enemigos: que vengan a mi y se vean", "$antes\b349.jpg", "$val\v4_349_despues.png",
    "ANTES: 8 enemigos en la plaza, entraban de a uno y solo se marcaban cerca o al quedar <= 4.`nDESPUES: 16 enemigos, entran mas seguido (0,8-1,6 s), todos llevan su rombo rojo desde que entran y cada 2,5 s se les reapunta la posicion del jugador para que vengan a vos."),
  @("#350  Quitar flecha", "$antes\b350.jpg", "$val\v4_350_despues.jpg",
    "ANTES: flecha roja gigante apoyada en la calle (aviso de oleada).`nDESPUES: ya no se dibuja en el mundo; el aviso de la oleada sigue en el minimapa y en el cartel."),
  @("#351  Colision invisible", "$antes\b351.jpg", "$val\v4_351_despues.jpg",
    "ANTES: una barricada de sacos (292,1 / 274,1) tenia collider pero su arte estaba apagado: frenaba al jugador sin verse.`nDESPUES: se apagaron los colliders de las 37 props de la ciudad cuyo arte esta oculto (barricadas, palets, barriles, faroles...). Ya no queda ninguna colision sin dibujo."),
  @("#352  El obstaculo solo frena a aliados", "$antes\b352.jpg", "$val\v4_352_despues.jpg",
    "ANTES: los enemigos del techo no podian pasar barandas y equipo.`nDESPUES: 14 obstaculos del techo (barandas, equipo, claraboya) llevan BarreraSoloEnemigos: bloquean al jugador y a la escuadra pero los enemigos los atraviesan.")
)

$W = 1900; $hFila = 380; $hTitulo = 70
$H = $hTitulo + $filas.Count * $hFila + 90
$bmp = New-Object System.Drawing.Bitmap $W, $H
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.TextRenderingHint = 'ClearTypeGridFit'
$g.Clear([System.Drawing.Color]::FromArgb(24, 26, 32))
$fT = New-Object System.Drawing.Font("Segoe UI", 26, [System.Drawing.FontStyle]::Bold)
$fH = New-Object System.Drawing.Font("Segoe UI", 15, [System.Drawing.FontStyle]::Bold)
$fX = New-Object System.Drawing.Font("Segoe UI", 12)
$fE = New-Object System.Drawing.Font("Segoe UI", 11, [System.Drawing.FontStyle]::Bold)
$blanco = [System.Drawing.Brushes]::White; $gris = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(210, 214, 222))
$oro = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 196, 70))
$g.DrawString("StrategicPoint - atlas ANTES / DESPUES  (2026-11-06)", $fT, $blanco, 20, 14)
$y = $hTitulo
foreach ($f in $filas) {
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(34, 37, 46))), 8, $y + 4, $W - 16, $hFila - 8)
  $g.DrawString($f[0], $fH, $oro, 20, $y + 10)
  $iw = 600; $ih = 320
  foreach ($k in 0, 1) {
    $ruta = $f[1 + $k]; $x = 20 + $k * ($iw + 14)
    if (Test-Path $ruta) {
      $img = [System.Drawing.Image]::FromFile($ruta)
      $esc = [Math]::Min($iw / $img.Width, ($ih - 28) / $img.Height); $dw = [int]($img.Width * $esc); $dh = [int]($img.Height * $esc)
      $g.DrawImage($img, $x, $y + 44, $dw, $dh); $img.Dispose()
    } else { $g.DrawString("(sin foto: ver texto)", $fX, $gris, $x, $y + 120) }
    $g.DrawString($(if ($k -eq 0) { "ANTES" } else { "DESPUES" }), $fE, $oro, $x, $y + 44 + $ih - 22)
  }
  $rect = New-Object System.Drawing.RectangleF(($iw * 2 + 60), ($y + 12), ($W - $iw * 2 - 90), ($hFila - 24))
  $g.DrawString($f[3], $fX, $gris, $rect)
  $y += $hFila
}
$g.DrawString("Reportes de bug: desde el EDITOR quedan solo en Registros/ (no suben al Sheet); desde el .exe se guardan y SUBEN al Sheet. Probado en Play: SubeAlSheet=false en Editor.", $fE, $oro, 20, $y + 20)
$enc = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
$p = New-Object System.Drawing.Imaging.EncoderParameters 1
$p.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), 88L
$bmp.Save($Salida, $enc, $p)
$g.Dispose(); $bmp.Dispose()
Write-Output "OK $Salida"
