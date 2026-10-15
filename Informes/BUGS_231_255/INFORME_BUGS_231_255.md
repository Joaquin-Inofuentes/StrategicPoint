# Bugs #231–#255 y arte de Nivel 1 / Tutorial — informe de resolución

Fecha: 2026-10-15. Cada bug tiene su captura **antes** (la que marcaste en el juego, en `antes/bug_NNN.png`) y, cuando es visible, **después** (en `despues/`). Los checks automáticos están en `Editor/ChecksBugs065.Bugs231a255.cs` (resultado crudo en `resultado_checks.txt`) y los checks pasan en un Play fresco cada uno (ver `resultado_checks.txt`). La suite headless completa dio OK en 8 corridas seguidas.

## Resumen

| # | Pedido | Estado | Cómo se validó |
|---|---|---|---|
| 231 | Revividos se entierran | Resuelto | check 231a |
| 232 | "Se rompió, mirá su cuerpo" | Resuelto en su causa probable (cuerpo enterrado); el cuerpo deformado en sí no se reproduce | check 231a + 232b + captura |
| 233 | La cámara se tapa con muros | Resuelto | check 233a |
| 234 | Dispara pero no llegan los tiros | Resuelto | check 234a |
| 235 | Enemigos que oyen disparos van al origen | Resuelto | check 235a |
| 236 | Coberturas a escala | Resuelto | check 236a + captura |
| 237 | Panel inferior derecho (granada + arma) | Resuelto | captura |
| 238 | Impactos como decals | Resuelto | captura |
| 239 | Escombro de piso sin collider | Resuelto | check 236a |
| 240 | Dificultad adaptativa | Resuelto | check 240a |
| 241 | Disparo y recarga de francotirador | Resuelto | sonido medido + captura del destello |
| 242 | Operador de hackeo no se distrae | Resuelto | check 242a + captura |
| 243 | Cinemática al caer el muro | Resuelto | check 243a + captura |
| 244 | Fusil: doble cadencia, triple cargador | Resuelto | check 244a |
| 245/247 | Anillo de torreta fijo, sin cruceta | Resuelto | check 246cap + captura |
| 246 | Explosión a la mitad | Resuelto | captura del obús en el anillo |
| 248 | Helicóptero solo en el final | Resuelto | check 248a |
| 249 | "¡FUEGO!" abajo y centrado | Resuelto | captura |
| 250 | RTS por defecto, 2 equipos a 2 puntos, minimapa | Resuelto | check 250a + capturas |
| 251 | Una cobertura por defensor y asomarse | Resuelto | check 251a + 2 capturas |
| 252 | Quitar el muro que estorba | Resuelto | check 236a |
| 253 | Cadáveres visibles pidiendo ayuda | Resuelto | check 253a + capturas |
| 254 | Cámara al asesino 3 s | Resuelto | check 254a + captura |
| 255 | "Tenés muertos, recuperalos" | Resuelto | check 255a + captura |

## Hallazgo principal: los cuerpos caídos quedaban enterrados (#231, #253 y probablemente #232)

Al morir se apaga el collider del soldado. `Collider.bounds` de un collider apagado está vacío, y cada 0,5 s `ApoyoEnElPiso.Apoyar` "apoyaba" el cadáver con esos bounds: lo bajaba de y=0,8 a y=0 y la malla quedaba 0,67 m bajo el piso. Solo asomaban el arma y las manos. Ahora se enciende el collider un instante para medirlo. Medido: cadáver a y=0,80 y malla con mínimo en y=0,13; el revivido apoya la base en y=0,000.

## Por bug

**#231** `Core/ApoyoEnElPiso.cs`, `Player/Reanimacion.cs`. Antes: `antes/bug_231.png`.

**#232** El cuerpo deformado en sí no se reproduce: con la torreta volada y el aliado parado entre escombros no se inclina (0,0°), no cambia de escala y mide 1,68 m (check 232a), y los pies quedan a 0,01–0,04 m del piso de pie y agachado en los 3 soldados (check 232b). Su causa más probable es la de #231 (cuerpo enterrado tras revivir, ya corregido y medido en 231a). Captura: `despues/v232_cuerpo_entre_escombros.png`. Si vuelve a pasar, marcalo con [U] en ese lugar.

**#233** `Camera/CameraRig.cs`: la cámara hace un spherecast desde el soldado y se acerca si hay un muro. Con un muro a 0,9 m detrás, la cámara queda a 0,62 m y nada tapa la línea cámara→soldado.

**#234** `Player/PlayerBrain.cs`, `Combat/WeaponHolder.cs`. Causa: la cámara ve por encima del saco pero la boca del arma queda por debajo del borde, así que la bala nacía dentro de la cobertura. Si el tramo boca→punto está tapado, la bala sale de la cámara. Con una cobertura de 1,05 m la bala nace a y=1,19.

**#235** `Ai/AiBrain.cs`: los enemigos oyen hasta 48 m (antes 30). Un enemigo a 40 m pasa de Patrulla a Chase al oír el tiro.

**#236 / #239 / #252** `Editor/OperacionBuilder.Correcciones.cs`: sacos ×1,4 de alto, concreto ×1,2, barriles finos, 23 vallas de tablones de 14 m eliminadas, colliders quitados de cráteres y escombros de piso, NavMesh recalculado.

**#237** `UI/WeaponStatusView.cs`: dos tarjetas (granada | arma con icono arriba y munición abajo). Ver `despues/v237_panel_arma_dos_tarjetas.png`.

**#238** `DecalPool`, `Projectile`, `ImpactCubes`: sprite procedural (núcleo oscuro, astillas, grietas), 120 de presupuesto, a +3 cm sobre el piso. Ver `despues/v238_decals_pared.png`.

**#240** `Core/DificultadAdaptativa.cs`: factor del daño enemigo entre ×0,7 y ×1,6 según vida de la escuadra, caídos recientes y tiempo sin daño, más una ola lenta de ±12 % y un rasgo fijo por enemigo de ±15 %. Solo rige fuera del tutorial. Medido: escuadra sana → ×1,60; con 2 caídos → ×0,80.

**#241** `SfxSintetico.Francotirador.cs`: estampido de 2,6 s (crack supersónico, boom grave, ecos), pico 0,95; recarga más fuerte. El destello (fogonazo grande, onda azul por el piso y estela) se ve en `despues/v241_destello_a.png` y `v241_destello_b.png`.

**#242** `OperacionTerminal.cs`: el operador del hackeo y el soldado asignado quedan `Pasivo` mientras trabajan y se restaura al soltar. Check 242a: con un enemigo a 14 m el operador queda Pasivo y Quieto, no pasa a Attack/Chase, no se mueve (0,00 m) y al terminar vuelve a su postura. El check encontró un agujero real: el seguimiento automático del arranque (1,2 s) lo sacaba de la computadora; ahora ignora a quien ya está operando. Captura: `despues/v242_hacker_concentrado.png`.

**#243** `OperacionDirector.Jefe.cs`: la toma del jefe arranca apenas cae el muro (0,0 s) aunque el jugador siga antes de z=-30. Ver `despues/v243_toma_tras_demoler.png`.

**#244** `WeaponCatalog.cs`: fusil de 0,30 s a 0,15 s de cadencia y cargador de 8 a 24. "Fusil" es el nombre en el juego del arma `Rifle` (`DisplayName = "Fusil"`), así que no queda ambigüedad.

**#245 / #246 / #247** `TurretAimView`, `MiraDeTorreta`, `ImpactFx`: solo queda el anillo de zona, fijo (no vibra), proyectado al terreno; la explosión visual pasa a la mitad de tamaño. Check 246cap en el tanque, de artillero, contra un muro a 22 m: anillo dibujado 40/40 cuadros, radio 5,50–5,50 m (no vibra) y sin cruceta ni rombo en pantalla (el check antes medía solo que el panel estuviera activo; ahora mide `CrucetaVisible` y `RomboVisible`, ambos falsos). Capturas: `despues/v245_anillo_fijo_y_cruceta.png`, `v246_explosion_a.png`, `v246_explosion_b.png`.

**#248** `OperacionDirector.Final.cs`: el Halcón ya no entra en la carrera (solo en la cinemática final). En 42 s de carrera no apareció ni se creó ninguno. Los checks viejos 097l/097m activan `HalconEnLaCarrera` para medir su variante.

**#249** `CineDeHuida.cs`: "¡FUEGO!" anclado abajo al centro; el subtítulo sube. Ver `despues/v249_fuego_abajo.png`.

**#250** `OperacionDirector.Mando.cs`, `UI/MinimapFollow.cs`: Resistir arranca en RTS centrado entre los sectores A y B; la escuadra va a uno y los milicianos al otro (7 de 7 llegaron). El minimapa se centra en el foco RTS, lo sigue al panear y dibuja el recuadro de la región visible. Ver `despues/v250_*.png`.

**#251** `SectorDeDefensa.cs`, `AiBrain.Tactica.cs`: un defensor por obstáculo y asomarse cada 3–6 s durante 1,2–1,9 s. Check 251a en Resistir: 7 defensores en cobertura, 0 coberturas compartidas y los 7 se asoman a vigilar sin blanco (en 40 s). Capturas del mismo miliciano asomado y agachado: `despues/v251_defensor_asomado.png`, `v251_defensor_oculto.png`.

**#253** `Presentation/AyudaDeCaido.cs`: el aliado caído queda visible, levanta un brazo que saluda y muestra "¡AYUDA!". El enemigo muerto no lo hace; al revivir se apaga. Ver `despues/v253_*.png`.

**#254** `PlayerInputDriver.cs`: durante 3 s la cámara queda tras el cuerpo mirando al asesino (desvío de 3,6° a 18 m) con aviso "TE MATÓ" y anillo; después pasa al aliado libre más cercano. Ver `despues/v254_a_mirando_asesino.png`.

**#255** `OperacionDirector.Extraer.cs`: con caídos se muestra "TENÉS N MUERTOS · RECUPERALOS Y VOLVÉ AQUÍ" y el [E] no embarca. Ver `despues/v255_prompt_con_muertos.png`.

## Arte y entorno en Nivel 1 y Tutorial

`Editor/OperacionBuilder.ArteNiveles.cs` agrega un grupo raíz `ArteExtra` a cada escena (idempotente). Respeta una distancia de 6–9 m a soldados, waypoints, vehículos y marcadores de misión.

- **Tutorial** (`antes/tut_antes_*`, `despues/tut_despues_*`): 16 plantas en arboledas, 20 props en campamentos, 12 cráteres/escombros, 70 parches de suelo.
- **Nivel 1** (`antes/n1_antes_*`, `despues/n1_despues_*`): 37 plantas, 41 props, 30 cráteres/escombros, 190 parches de suelo. El Nivel 1 ya era un bosque denso, así que el cambio se nota sobre todo en el piso del campo de combate (manchas, escombros, campamentos).

Costo medido en las escenas guardadas (`ChecksBugs065.MedirArteNiveles`, abre cada escena en aditivo, cuenta y cierra sin guardar):

| Escena | Objetos | Renderers (con sombra) | Triángulos | Materiales | Colliders | Luces (con sombra) | Escena entera |
|---|---|---|---|---|---|---|---|
| Nivel 1 | 738 | 298 (108) | 36.888 | 25 | 86 | 3 (0) | 2.596 mallas, 355.666 tris |
| Tutorial | 317 | 118 (48) | 13.040 | 22 | 41 | 2 (0) | 387 mallas, 30.262 tris |

Es chico en términos absolutos (unos 37 mil triángulos en el Nivel 1, 13 mil en el Tutorial, pocas decenas de materiales). No se midieron cuadros por segundo en el reproductor: es un conteo de geometría, no un profiler. Los props con colisión quedan fuera del NavMesh y las luces decorativas no proyectan sombra.

## Rombos de aliados en RTS

Check 049rts (Resistir, vista RTS): rombo visible en 7 de 7 aliados vivos, también sobre el soldado que controlás, con escala 3,6–5,0 (crece con la distancia de la cámara, como pide el #049). Los "rombos gigantes" de una captura anterior eran un artefacto de mi cámara temporal (los rombos miran a la cámara principal); con la cámara real son normales (`despues/rombos_rts_camara_real.png`, `rombos_fps_camara_real.png`).

## Cambios en tests viejos

- 097l/097m: activan `HalconEnLaCarrera`.
- Presupuesto de `Transform.Find` por nombre: 126 → 127 (un `Find` de arranque en `WeaponStatusView`).
- `FullHeal` de la suite reanima a los caídos: con el oído enemigo a 48 m (#235) Vega llegaba muerta a la fase de vehículos en 1 de cada 4 corridas.
- Fase 21 (médico en calma): aparta a los enemigos del lugar de la prueba, porque ahora oyen los tiros de fases anteriores.
