Add-Type -AssemblyName System.Drawing
$v = "C:\_Proyectos privados\DV_C6_StrategicPoint\Assets\Validacion~"
$out = "C:\_Proyectos privados\DV_C6_StrategicPoint\Docs\RONDA_17\ATLAS_R17_4_opciones_feedback.jpg"
$W = 1300
$bg = [System.Drawing.Color]::FromArgb(24,24,28)
$amar = [System.Drawing.Color]::FromArgb(255,200,60)
$verde = [System.Drawing.Color]::FromArgb(120,230,140)
$blanco = [System.Drawing.Color]::FromArgb(235,235,240)
$gris = [System.Drawing.Color]::FromArgb(170,170,180)
$fT = New-Object System.Drawing.Font('Segoe UI', 24, [System.Drawing.FontStyle]::Bold)
$fH = New-Object System.Drawing.Font('Segoe UI', 17, [System.Drawing.FontStyle]::Bold)
$fL = New-Object System.Drawing.Font('Segoe UI', 12, [System.Drawing.FontStyle]::Bold)
$fN = New-Object System.Drawing.Font('Segoe UI', 12)

function Img($n) { [System.Drawing.Image]::FromFile((Join-Path $v "$n.png")) }

# Alto estimado: titulo 60 + A 560 + B 560 + C 360 + D 470
$H = 2250
$bmp = New-Object System.Drawing.Bitmap($W, $H)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.TextRenderingHint = 'ClearTypeGridFit'
$g.Clear($bg)
function Txt($t, $f, $c, $x, $y) { $b = New-Object System.Drawing.SolidBrush($c); $g.DrawString($t, $f, $b, $x, $y); $b.Dispose() }
function Foto($n, $x, $y, $w, $rot) {
  $i = Img $n; $h = [int]($w * $i.Height / $i.Width)
  $g.DrawImage($i, $x, $y, $w, $h); $i.Dispose()
  $bk = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(190,0,0,0)); if ($rot) { $sz = $g.MeasureString($rot, $fL); $g.FillRectangle($bk, $x, $y, $sz.Width + 10, $sz.Height + 4); Txt $rot $fL $blanco ($x + 5) ($y + 2) }
  return $h
}

Txt "Ronda 17 (4/4) - opciones en partida y feedback de acciones" $fT $blanco 20 14
$y = 70

# A. Menu de opciones
Txt "A. Menu de pausa > CONFIGURACIONES: resolucion, sensibilidad, y NUEVO calidad, iluminacion y brillo" $fH $amar 20 $y
Txt "OK (#991a)" $fH $verde 1150 $y
$y += 38
$h = Foto 'r17_opciones_graficos' 20 $y 760 ''
Txt "Ya existian:`r`n  Resolucion (desplegable + completa/ventana`r`n  + APLICAR) y sensibilidad de mouse y torreta.`r`n`r`nNUEVO - seccion GRAFICOS:`r`n  Calidad BAJA / MEDIA / ALTA: escala de render,`r`n  MSAA, texturas, LOD y filtrado (copia del asset`r`n  URP; el del proyecto no se toca). BAJA medido:`r`n  escala 0,70, MSAA 1, lodBias 2,00 -> 1,40.`r`n  Iluminacion BAJA (sin sombras) / MEDIA / ALTA.`r`n  Brillo 80 / 100 / 120 / 140 % (sol 0,16 -> 0,19,`r`n  ambiente 1,00 -> 1,20).`r`n`r`nSe guarda en PlayerPrefs. Vive en el menu de`r`npausa; la calidad se reaplica sola al arrancar`r`nsolo en builds." $fN $blanco 800 ($y + 4)
$y += $h + 24

# B. Comparacion
Txt "B. La misma vista con otros ajustes (#991c). Ojo: la explosion de la toma BAJA es del nivel, no del ajuste" $fH $amar 20 $y
$y += 38
$w2 = 630
$h1 = Foto 'r17_calidad_alta_ilum_alta' 20 $y $w2 "CALIDAD ALTA + ILUMINACION ALTA"
$null = Foto 'r17_calidad_baja_ilum_baja' 650 $y $w2 "CALIDAD BAJA + ILUMINACION BAJA (sin sombras)"
$y += $h1 + 8
$null = Foto 'r17_brillo_80' 20 $y $w2 "BRILLO 80 %"
$null = Foto 'r17_brillo_140' 650 $y $w2 "BRILLO 140 %"
$y += $h1 + 24

# C. Escena
Txt "C. Efectos de la tercera pasada: el enemigo suelta el arma, polvo al caer" $fH $amar 20 $y
$y += 38
$w3 = 420
$h3 = Foto 'r17_muerte_cayendo' 20 $y $w3 "cayendo"
$null = Foto 'r17_muerte_en_el_piso' 450 $y $w3 "en el piso"
$null = Foto 'r17_arma_caida' 880 $y $w3 "arma caida"
$y += $h3 + 24

# D. Feedback
Txt "D. Feedback de acciones: los 12 avisos nuevos (4 primeros + 8 de los 7 huecos)" $fH $amar 20 $y
Txt "OK (#991b + suite)" $fH $verde 1060 $y
$y += 40
$filas = @(
 @("Recargar con cargador lleno / sin reserva", "CARGADOR LLENO / SIN MUNICION DE RESERVA + sonido de rechazo"),
 @("Rueda del mouse cambia de arma", "muestra [n] NOMBRE del arma"),
 @("Clic del canon del tanque en recarga", "CANON RECARGANDO"),
 @("[B] Reagrupar / Retirada sin seleccion", "NADIE SELECCIONADO"),
 @("Radio: poseer a otro soldado", "EN LA RADIO NO SE CAMBIA DE SOLDADO"),
 @("Radio: cambiar de vista [TAB]", "EN LA RADIO NO SE CAMBIA DE VISTA"),
 @("Tanque: tecla [3] sin metralleta", "ESTE VEHICULO NO TIENE METRALLETA"),
 @("[N] sin nada seleccionado", "NADIE SELECCIONADO"),
 @("Cancelar orden sin nada que cancelar", "NO HAY NADA QUE CANCELAR"),
 @("Medico: [E] mantenido sin heridos cerca", "NADIE HERIDO CERCA (una vez por mantenida)"),
 @("Mover con todos los puntos inaccesibles", "PUNTO INACCESIBLE + sonido de rechazo (ya no el de exito)"),
 @("Minimapa [M]/[L] cambia de tamano", "clic: mas agudo al agrandar")
)
foreach ($f in $filas) {
  Txt $f[0] $fN $gris 30 $y
  Txt $f[1] $fL $blanco 520 ($y + 1)
  $y += 28
}
$y += 10
Txt "Verificado: check #991b (mover inaccesible = false, cercano = true, minimapa informa su tamano) y suite headless en OK." $fN $gris 20 $y
$y += 26
Txt "Sin simular: las teclas en si (los avisos estan comprobados por lectura de codigo, no apretando teclas)." $fN $gris 20 $y
$y += 40

$final = New-Object System.Drawing.Bitmap($W, $y)
$g2 = [System.Drawing.Graphics]::FromImage($final); $g2.DrawImage($bmp, 0, 0); $g2.Dispose()
$codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
$ep = New-Object System.Drawing.Imaging.EncoderParameters(1)
$ep.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter([System.Drawing.Imaging.Encoder]::Quality, 88L)
$final.Save($out, $codec, $ep)
$g.Dispose(); $bmp.Dispose(); $final.Dispose()
"listo $out $W x $y"
