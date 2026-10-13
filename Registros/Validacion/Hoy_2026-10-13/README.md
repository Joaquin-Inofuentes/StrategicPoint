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
| #227 | Que usen coberturas, se agachen al recargar | **Mejorado**: se agachan al recargar y entre rafagas (siempre si no hay cobertura a mano); cobertura hasta 4 m mas lejos del blanco, 12 candidatas, radio 18 m, decide a los 0,15 s de empezar a disparar. Medido con IA humanizada en el blockout 2: 75 % -> 99 % cubiertos; en SC_Operacion (combate real, ~100 enemigos): 38 de 66 soldados en combate cubiertos o yendo a cubrirse y solo 2 de ellos en Attack sin cobertura (el resto son perseguidores fuera de alcance). Blockout 4 (ruta abierta): sin cobertura posible, limite del mapa | `227_cobertura_ia.png` |
| #228 | "Fijate" | **Sin accion** (nota sin contenido; la captura solo muestra el aviso del bug #227) |  |

Suite headless completa: OK (tras corregir que el repintado del vehiculo forzaba la forma cuadrada).

## Ronda de verificacion y rendimiento (SC_Operacion, en Play)
- #221: 0 muros invisibles con collider; 118 cubos vestidos.
- #230/#231: Vega, Kes y Doc muertos y revividos en el nivel real: y=0,80, collider encendido, base a 0,00.
- Rendimiento medido en el editor con ~100 enemigos: frame medio 20 ms (peor 39 ms), sin picos de 200 ms; los scripts del juego cuestan ~4 ms, el resto es render (7.700 draw calls, 93 luces puntuales ~4 ms). El "5 fps" del #225 no se reprodujo. Static batching probado a mano y descartado (no mejoro y empeoro el culling).
- Pendiente de decidir por el jugador: #226 (que significa "civiles por defecto con los milicianos"), #222 (medico torcido: no reproducible).
