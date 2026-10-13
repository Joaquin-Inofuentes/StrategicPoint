# Bugs de hoy (#221-#231) - revision y validacion (2026-10-13)

| Bug | Nota del jugador | Estado | Evidencia |
|---|---|---|---|
| #221 | No se ven los muros | **Corregido**: 13 muros (Muralla_E/O, Cierre_Norte_E/O, Bunker_Cuerpo x2, Borde_*, Valle_*) tenian el cubo apagado y SIN su arte (invisibles con collider). `ArteDeOperacion.Vestir` ahora rescata esos cubos huerfanos: 16 -> 29 muros vestidos | `221_antes.png`, `221_despues.png`, `221_muralla_E.png`, `221_bunker.png` |
| #230, #231 | Se entierran al ser revividos | **Corregido**: el revivido quedaba con la base del collider a -0,80 m. `Reanimacion.Ejecutar` ahora enciende el collider y lo apoya en el piso (y=0,80, base 0,00) | `revivir_0_vivo.png`, `revivir_1_caido.png`, `revivir_2_revivido.png`, `revivir_3_revivido_3s.png` |
| #225 | Se solapan los asfaltos de las calles | **Corregido** (z-fighting): 22 pares de pisos/calles coplanares -> 0; cada piso sube una fraccion distinta, calles +5 cm sobre patios | `asfalto_calles_despues.png` |
| #223 | En RTS no deja mandar a subir a la torreta | **Corregido**: el clic derecho en RTS no mandaba caminar al soldado (solo esperaba que llegara). Ahora emite la orden de movimiento | verificado el movimiento del soldado hacia la torreta |
| #229 | Milicianos con AK-47 y mas rafaga | **Hecho**: Rifle (AK) en vez de subfusil, rafagas x2 y pausas ~40 % mas cortas | milicianos: arma=Rifle, rafaga x2, y=0,80 |
| #222 | Medico roto al seguirme | **Sin corregir**: la captura muestra al medico con una pose torcida apuntando; no se reproduce con los datos del reporte |  |
| #224 | Minimapa: icono propio / enemigos / tanque | **Corregido**: el vehiculo que manejas es un TRIANGULO amarillo grande que gira con el (antes un cuadrado azul igual al de un aliado y tu soldado quedaba escondido debajo); el soldado montado ya no duplica icono; enemigos siguen en triangulos rojos | `minimapa_en_tanque.png` |
| #226 | Civiles por defecto con los milicianos | **Sin hacer** (pedido ambiguo, hay que confirmarlo) |  |
| #227 | Que usen coberturas, se agachen al recargar | **Mejorado** (parcial): se agachan al recargar y, al azar (50 %, <= 0,5 s), entre rafagas a cielo abierto; busqueda de cobertura a mano 13 -> 18 m y decision mas rapida (1,2 -> 0,7 s). Medido en el blockout con IA humanizada: 45 % de los soldados en combate cubiertos o yendo a cubrirse (muestra de 54 tomas, ruidosa; el agachado a cielo abierto sigue siendo minoritario) | medicion en esta nota |
| #228 | "Fijate" | **Sin accion** (nota sin contenido; la captura solo muestra el aviso del bug #227) |  |

Suite headless completa: OK (tras corregir que el repintado del vehiculo forzaba la forma cuadrada).
