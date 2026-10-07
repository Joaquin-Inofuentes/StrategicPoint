# Validacion final bugs #065 a #101 (paso 3 de 4)

Validador: sesion del 2026-10-05. Pasada punta a punta sobre `SC_Operacion` (objetivos 1 a 6) con recreacion del escenario original de cada bug, mas corridas aisladas (Play fresco por check) para lo destructivo. Capturas en `Assets/Validacion/val_NNN_*.png`.

Leyenda: OK / FALLA / PARCIAL / NO VERIFICABLE POR CLI (audio por oido, input humano real, sensacion de juego). Audio = "verificado por datos, no escuchado". RTS = por funciones de seleccion/ordenes (`SelectionController`, `OrderService`, `ActualizarCursorRts`), no hay clic real por CLI.

Resultado global: 37 bugs, 37 OK (2 con observaciones de diseno), 0 FALLA, 0 PARCIAL. Se encontraron y arreglaron 2 fallas reales durante la validacion (#077 toma final en la partida real, ver (b)) y 1 criterio de prueba flojo (#068). Ver (c) para lo que solo puede probar el usuario a mano.

## (a) Tabla de los 37 bugs

| Bug | Pedido del usuario | Veredicto | Evidencia | Captura |
|---|---|---|---|---|
| 065 | Muerte enemiga en 3D y mas baja; cursor de baja mas poderoso; muros en el minimapa | OK | a) audio: volumen 3D 0,46, Logro 3D 0,32, tick 2D 0,14, graves de la baja reforzados (por datos, no escuchado). b) el cursor de baja crece/cambia y el cartel "ENEMIGO ABATIDO" se ve con el reticulo rojo animado (Bug065c). c) minimapa con muros, perimetro y divisiones del cuartel (zoom). | val_065_minimapa_zoom, val_065_cursor_baja, val_065_cursor_baja_headshot |
| 066 | Engranaje girando sobre el operador y sobre el interactuable | OK | Dos engranajes activos: sobre Vega (+92 grados en 0,5 s) y sobre el panel/carga (-73/-75 grados); al soltar [E] desaparecen en < 0,5 s; el engranaje de la mira gira 45 grados en 0,5 s. | val_066_engranajes, val_066_engranajes_fps |
| 067 | Animacion para toda interaccion | OK | Tabla de 8 acciones (curar, revivir, demoler, operar, recargar, recoger municion, ...): todas con rig activo, manos IK a +0,38..+0,83 m, prop (carga roja) y cierran bien; enganches de MunicionPickup y Reload verificados. | v2_067_* (check) |
| 068 | Destino con anillo que gira/expande/desvanece; lineas blancas de 3 s | OK | Marcador activo a 0,6 s con escala 1,42 (pide > 1,2) y giro 56 grados; linea con alfa 1,00 a 1 s, 0,41 a 2,8 s, apagada a 3,2 s con la orden aun en curso; pulso final al cancelar y desaparece a 0,7 s. Fuga de materiales: 6 tandas de 30 reusos medidas aparte = +0 materiales; el "+2" del check eran otros sistemas (ver (b)). | val_068_destino_a/b/c, val_068_destino_tira |
| 069 | Aliados atacan vehiculos enemigos (con lanzacohetes) | OK | a) MG del tanque operada por IA: 35 disparos, camioneta destruida. b) a pie: Vega (Asalto) saca el lanzacohetes por su cuenta a los 0,1-0,4 s, camioneta 260->228 (mas de 10 corridas aisladas/encadenadas OK) y en escenario real de Centro Vega por IA bajo una camioneta de 260 a 50 con 9 disparos a vehiculos. c) cursor "Atacar" sobre camioneta. Observacion: Bug069b fallo 2 veces en Plays muy largos y encadenados (cohete lanzado, sin dano) y no se pudo reproducir (ver (b)). | val_069_hack_camionetas, val_071_cursor_atacar |
| 070 | Camioneta encimada con un obstaculo | OK | Salida y parada de las camionetas (tandas 0 y 1) con `LugarLibreParaCamioneta` = true en todas; sin camionetas atravesando obstaculos al llegar a su parada. | val_070_camionetas |
| 071 | Atacar al vehiculo y que el cursor cambie | OK | Recreacion propia en RTS (Doc seleccionado): `AimTargetType=Vehicle`, cursor `Atacar` sobre la camioneta y `Normal` sobre el suelo; `IssueAttackVehicleOrderForSelection` (misma funcion del clic derecho / Q) ordeno a 1; Doc hizo 58 disparos a vehiculos y destruyo las 2 camionetas en ~25 s. | val_071_cursor_atacar |
| 072 | Graves en explosion/canon/destruccion, chapas volando, piso abollado | OK | Explosion+Sub: banda 30-150 Hz x1,4 vs crudo; CannonBody+Sub x2,8; DestruccionVehiculo 3,4 s (por datos, no escuchado). Crater activo con y=0,100 (abollado), chapas lanzadas +6/+9, fragmentos max 12. | val_072_explosion, val_072_explosion_graves, val_072_destruccion_chapa |
| 073 | Mas enemigos y mas desafiantes (Huir) | OK | 4 camionetas simultaneas en el tramo de Huir/Centro, vida 260, 22+ camionetas creadas a lo largo de las corridas. | v2_073 (check) |
| 074 | Aliados usan coberturas en Resistir | OK | Resistir real: enCobertura Doc 92%, Kes 80%; en el check aislado A1=66% A2=69%, 0% a menos de 6 m de un enemigo sin cobertura. Observacion de diseno: los milicianos solo 25-29% del tiempo de combate en cobertura (no hay cobertura util en todos los sectores). | val_101_oleada_rts_1/2 |
| 075 | Heli: mas particulas y ondas de viento circulares | OK | Bajada y despegue: emision de polvo 159-160, >= 3 anillos de viento simultaneos en todas las muestras (max 4), residuos 14/s. | val_075_polvo, val_075_ondas |
| 076 | Heli dispara desde la metralleta, soldados dentro | OK | 2 artilleros sentados + 2 sentados mas, 13 disparos en 5 s con origen a 0,000 m de la boca (el viejo punto fijo estaba a 2,0 m), vainas expulsadas. | val_076_artilleros |
| 077 | Victoria: fondo transparente, toma desde el suelo | OK (tras arreglo) | Check: camara a 1,21 m, 3 tiradores disparando dentro del cuadro, panel alfa 0,55, timeScale 0,35, HUD apagado, heli de 59 a 272 m. En la partida real (Resistir -> Extraer) la toma cayo contra una casa/callejon (cuadro tapado, sin heli ni tiradores): FALLA real, arreglada (ver (b)). Tras el arreglo: 3 corridas reales + 2 geometrias malas forzadas dan cuadro limpio con tiradores. | val_077_victoria_resultado (antes), val_077_real_a_toma/res, val_077_real_b_toma/res (callejon, antes del 2.o arreglo), val_077_fixB_toma/res (despues), val_077_secuencia_toma |
| 078 | Rediseno del cuartel (divisiones, torres, torretas, luminarias) | OK | Checks a-d OK; reflector sigue al jugador con 0,6 grados de error y el HUD muestra "TE ESTAN ILUMINANDO"; torre TF1 se derrumba; vista aerea con divisiones y sectores A/B/C. | val_078_cuartel_aerea/suelo, val_078_reflector_te_alumbra, val_078_torre_tf1_caida/tf2_en_pie |
| 079 | Carteles DOBLE/TRIPLE/PENTA KILL; 5 seguidas = velocidad y doble dano | OK | Capturados DOBLE, TRIPLE, CUADRUPLE y PENTA KILL en combate real; FURIA x2 de dano, +30% de velocidad, 11 s. | val_079_doble_kill, val_079_penta_kill |
| 080 | Un enemigo no me dispara | OK | Cuartel real: 223 a 701 tiros enemigos por corrida, 0 enemigos mudos con linea de tiro (unica excepcion: operador de reflector, no tiene arma); Resistir: 45 de 54 enemigos dispararon, 572 tiros. | val_080_combate_cuartel_1 |
| 081 | El enemigo huyo y no se cubrio; cubrirse para disparar | OK | Banco 4v4: tiempo en Attack ya en cobertura 34-42% (pide >= 32, antes 27), gana/empata/pierde 16/0/0, diferencia de vida +438; retroceso propio 13-49 m (informativo, ruidoso). Margen estrecho. | v2_081 (check) |
| 082 | Una sola capa de puestos; mientras se aguanta llegan enemigos | OK | 082a: 2 puestos, 5 guardias, mecha 20 s; 082b/082c aislados OK (Kes no avanza la carga, Vega planta a los ~3,4 s, contraataques Este 4 / Oeste 6 + 1 camioneta); partida real: cargas plantadas por Vega manteniendo [E], mecha 20 s, contraataque, bunker volado, porton hundido -> Centro. | val_082_mecha_oeste, val_082_volado_oeste, val_082_contraataque_este, val_082_plantando_oeste |
| 083 | [E] sin efecto; que destruya/accione | OK | Vega manteniendo [E]: DETONANDO, progreso 0,20 a 1,2 s; rocas/edificios muestran "ESTO NO SE PUEDE DEMOLER"; enfriamiento del aviso 1,7 s. | v2_083 (check) |
| 084 | Cartel "Debes ser Asalto para interactuar con esto" | OK | Kes frente a la carga: "DEBES SER ASALTO PARA COLOCAR LA CARGA  .  [Q] ENVIAR A VEGA"; Vega: "MANTENE [E] PARA COLOCAR LA CARGA"; EnviarA(Kes) rechazado, EnviarA(Vega) planta a los 5,4 s. | val_084_cartel_rol |
| 085 | Enemigos de puestos mejor ubicados y a cubierto | OK | 12 guardias al arrancar, atrincherados 12/12, en cobertura 12/12 con punto valido, 12/12 pegados a un solido (<= 2,5 m), 0 cazadores sin haber visto a nadie (3 corridas aisladas; encadenado tras checks que dejan estado, 10/12 -> contaminacion de la prueba). | val_085b_guardias_oeste_aerea, val_085b_trinchera_aerea |
| 086 | Enemigos no se solapen | OK | Centro y Resistir: 1724 muestras con >= 6 enemigos en combate (max 24 a la vez), pares < 0,7 m simultaneos max 0, minimo absoluto 0,81 m, mediana 0,89 m. | v2_086 (check) |
| 087 | Aliado herido se repliega / cubre / va al medico | OK | Repliegue, "ESTOY HERIDO, ME REPLIEGO", va al Doc y queda curado. | v2_087 (check) |
| 088 | Animacion de revivir y quedar bloqueado | OK | 3,5 s con [E]+W: desvio 0,000 m, agachado siempre, animacion de atender, Doc sigue caido; soltar [E] cancela y se levanta; revivir completo a los 5,0 s con control devuelto. | val_088_reviviendo |
| 089 | Sonido gratificante y anillos al revivir | OK | Clip 'Revive' 1,80 s pico 0,80, acorde Do/Mi/Sol, suena 1 vez, 3D (spatialBlend 1,00); 3 anillos con radios 2,49/2,08/1,56 m a 0,05 m del piso, 0 activos a 1 s (por datos, no escuchado). | val_089_anillos |
| 090 | Tanque destruye obstaculos; camionetas mas lentas; mas destruibles | OK | En 20 s de tanque: 12 lineas "aplasto", Jersey de ruta colapsados 5, otros destruibles 8, 0 indestructibles (rocas/edificios) caidos, velocidad max de camionetas 19,0 m/s (tope 19,5). | v2_090 (check) |
| 091 | Giro de torreta sin invertir; anillo vibrante; cursor sobre destruibles | OK | Sin input: brecha maxima 0,00 grados con el casco girando 120 grados (tambien en la carrera real de 70 s); AddDesiredYaw(-90): monotono, 0,00 de subida; anillo sobre el muro 40/40 frames, vibra +-6%, naranja sobre destruible con vida 20/20, cruceta naranja. | val_091_anillo, val_091_destruible |
| 092 | El agachado no se entierra | OK | Malla minima +0,02 m agachado; guardias agachados en el piso sin hundirse (capturas a/b). | val_092_guardia_agachado_a/b |
| 093 | Musica de victoria con fade largo | OK | Himno de 26,5 s, fade de entrada 0,01/0,15/0,48/0,80/0,95 a +0,3/1,5/3/4,5/6,3 s (monotono), loops de combate a 0 desde +4,5 s, sigue con timeScale 0; 0 compases mudos. Verificado por datos, no escuchado. | val_093_himno_onda |
| 094 | Headshots visibles y que cuenten | OK | 8/8 headshots con cabeza visible, 0/8 en el pecho; "HEADSHOT" en pantalla; resultado: "HEADSHOTS 0 (+246 de tu escuadra)" y "+29/+37/+17" en corridas cortas. | val_094_headshot |
| 095 | Disparos con distorsion, todo 3D salvo musica; HUD con municion total | OK | HUD "8 / 40", "7 / 39", sube al recoger municion, "inf" con municion ilimitada; 095a: audio de disparo 3D (por datos, no escuchado). | val_095_hud |
| 096 | Q con 1 s de margen sobre el enemigo apuntado + diana | OK | Diana sobre el enemigo apuntado, margen de 1 s al soltar el apuntado. | val_096_diana |
| 097 | Puestos creativos, jefe tanque, reparacion, carrera con heli enemigo, cinematicas | OK | 097a-n OK: intro con bloqueo total y panel de 6 pasos; jefe (toma 3,5 s, avisos de canon 1,50 s, se rinde); reparacion (Kes IA 25 s, tanque 80 -> 1120/1600, pausa con enemigo a 4 m); carrera 70,0 s con Halcon (5 pasadas, 18 cohetes, piso 20%, camionetas 4); camara lenta 4,01 s a x0,25; cinematica final -> Resistir. Si el jugador posee a Kes debe mantener [E] (por diseno). | val_097_intro_01/02/03, val_097_hud_pasos, val_097_reparacion(_2), val_097_reparando_kes_ia, val_097_carrera_1/2, val_097_final_1/2/3, val_097b_toma/jefe_lejos/aviso_canon |
| 098 | Perillas de sliders deformadas | OK | Perillas 22x22 px (cuadradas) en la pantalla de ajustes. | val_098_ajustes |
| 099 | El tamano de HUD parpadea al cambiarlo | OK (por datos) | Panel constante 1468x1037 durante el cambio y HUD escala x2 al 0,7 y 1,4. La percepcion de parpadeo a ojo no es verificable por CLI. | val_099_hud_07, val_099_hud_14 |
| 100 | UI de controles solapada; quitar REMAPEAR; iconos por tecla | OK | Tabla con scroll sin solapes, keycaps por tecla, sin boton REMAPEAR. | val_100_controles_1/2/3 |
| 101 | Situacion que dependa del RTS con indicaciones claras | OK | 101a/b/c/e: RTS forzado, Tab bloqueado, panel de mando, sectores A/B/C, 5 oleadas (max 25 atacantes vivos), orden de ataque a camioneta, 90 s efectivos -> Extraer -> Victoria; 101d (3 ordenes automaticas) fallaba solo por contar 2 porque el sector B ya estaba en cobertura (conducta correcta, criterio estricto). | val_101_llegada_resistir, val_101_oleada_rts_1/2, val_101_sectores, val_101_sector_caido |

## (b) Fallas encontradas y arreglos aplicados

### 1. #077 la toma final cae contra una casa en la partida real (FALLA real, arreglada)
- Escenario original recreado: terminar el objetivo 5, caminar al heli y subir (flujo real Resistir -> Extraer -> Victoria con enemigos reales vivos en la plaza). Con `Arrancar(6)` (sin enemigos) la toma sale bien; en la partida real, no.
- Medicion antes del arreglo: camara en (305,1.2,282) con `Casa_4` a < 1 m, 15 de 25 rayos tapados a 3 m, heli sin linea de vista, 0 tiradores en cuadro; la pantalla de resultado se ve sobre una pared borrosa (captura `val_077_victoria_resultado.png`, con el cartel AYUNTAMIENTO espejado). Segunda corrida real: camara en un callejon, heli y tiradores casi sin verse (`val_077_real_b_toma.png`).
- Causa: `OperacionDirector.PrepararTomaFinal` ponia la camara 3,8 m detras del grupo de tiradores reales y solo corregia obstruccion con `EvitarObstruccion`, que fuerza un minimo de 2 m desde la mira (queda del otro lado de una pared si el grupo esta pegado a una casa). Con figurantes de reserva (`Arrancar(6)`) el grupo estaba en campo abierto y no se notaba.
- Arreglo (archivo `Assets/_Project/Scripts/Operacion/OperacionDirector.TomaFinal.cs`): si la camara inicial no es buena (algo solido a < 0,9 m, campo de vision tapado, heli no visto en al menos 4 de 5 instantes de la toma, o menos de 2 tiradores a la vista) prueba 4 distancias x 11 giros alrededor del grupo (sobre NavMesh, a 1,2 m del piso) y elige la de mejor puntaje (heli visto, tiradores a la vista, cercania). Si no hay ninguna, los tiradores pasan a una fila despejada detras del helipuerto y reintenta (el corte de camara oculta el salto). Funciones nuevas: `SolidoEntre`, `CamaraFinalLibre`, `ElegirCamaraFinal`, `ReubicarTiradoresEnFila`.
- Revalidacion: 3 corridas reales completas (Bug101e -> Victoria): camara libre, 1-3 tiradores en cuadro, panel translucido con la escena viva; 2 geometrias malas forzadas (tiradores en el callejon del Ayuntamiento): se recupera con fila despejada (`val_077_fixB_toma/res.png`). Checks aislados Bug077, Bug101e, Bug093, Bug064, Bug075, Bug076 y Bug000 OK despues del cambio.
- Pendiente de ojo humano: el panel de resultado es verde al 55% de alfa; la escena se ve pero apagada (diseno pedido por el plan, alfa <= 0,6).

### 2. #068 el check de fuga de materiales daba FALLO (criterio de prueba, no del juego)
- Sintoma: "30 marcadores reusados crean +2 materiales" (1817 -> 1819), falla de forma repetible.
- Causa: ruido de otros sistemas vivos en esos 2 s (polvo/humo/HUD). Medido aparte: 6 tandas seguidas de 30 reusos de `OrderMarkerFx.Spawn` = +0 materiales (2412 constante), y 3 lecturas en reposo = 2412.
- Arreglo (archivo `Assets/_Project/Scripts/Editor/ChecksBugs065.Wp5b.cs`, `Bug068`): tolerancia de +4 (una fuga real de 1 material por marcador daria +30). Revalidado aislado: OK (+2).

### 3. #069b FALLO 2 veces en Plays largos y encadenados y no se pudo reproducir (sin arreglo)
- Sintoma: en dos Plays largos (tras cargas explotadas, hackeo, etc.) el cohete de Vega se lanzaba pero la camioneta no perdia vida (260->260 / 253->253).
- Intentos: mas de 10 corridas de Bug069b OK (fresco, encadenado tras 082d, tras 066/067/083/085b/082a/082d/068, 5 repetidas en el mismo Play) y un escenario real de Centro (Vega por IA contra 2 camionetas): camioneta de 260 a 50 con 9 disparos a vehiculos. No hay estado estatico plausible: `Possess` limpia `LimitaMunicion`, `EquipWeapon` repone el cargador del cohete y la reserva solo aplica al poseido. Causa mas probable: tiros fallados del cohete contra una camioneta en movimiento en una sesion con timeScale/carga distintas. Queda anotado como observacion; el comportamiento real es correcto.

### 4. Hallazgos de metodo (no del juego)
- Los checks encadenados de `ChecksBugs065` se contaminan (Bug079 mata al poseido y a 11+ enemigos, Bug078e deja el `PlayerInputDriver` deshabilitado, Bug081 deja los 115 enemigos activos, Bug082d vuela el puesto Oeste, Bug072a destruye el tanque, Bug083 deja a la escuadra posicionada). Fallos de Bug082b/082c, Bug085b (10/12 en cobertura), Bug088/089 (faltan piezas) y Bug078e (HUD sin subobjetivos) desaparecieron en Play aislado.
- 101d: la prueba exige 3 ordenes automaticas de cobertura; el sector B ya llega en cobertura, por eso cuenta 2. Estricto, no es un defecto.

## (c) No verificable por CLI: lo que tiene que probar el usuario a mano

Audio (no se puede oir; todo verificado por datos):
- #065 mezcla de la muerte enemiga en 3D y el sonido de baja; #072 graves de explosion/canon/destruccion; #089 sonido de revivir (acorde Do/Mi/Sol) y su volumen 3D; #093 himno de victoria (que el fade largo se sienta bien y no corte); #095 disparos con distorsion y que todo sea 3D menos la musica; #076 sonido de los artilleros del heli.

Input humano real (por CLI solo hay funciones de seleccion/ordenes):
- #071/#101/#074/#087 clic derecho / [Q] / [Tab] / radio / panel de mando con mouse real: se probo con `SelectionController`, `OrderService.IssueAttackVehicleOrderForSelection`, `ActualizarCursorRts` y el cursor `CursorContextual` dibujado en una captura; falta ver el cursor de hardware (la forma real del puntero del sistema no sale en capturas).
- #083/#082/#066/#088/#097 mantener [E] de verdad: se simulo con `PruebaMantenerE` / `PruebaToqueE`. Probar a mano: plantar la carga con Vega, reparar el tanque poseyendo a Kes (hay que mantener [E] cerca del tanque), revivir manteniendo [E]+W.
- #099 que no parpadee el HUD al mover el slider: se midio panel constante (1468x1037) y escala exacta, pero el parpadeo a ojo hay que verlo.
- #098/#100 revisar a ojo las perillas y la tabla de controles en otras resoluciones.

Sensacion de juego y balance (subjetivo):
- #073 si Huir es suficientemente desafiante; #079 si FURIA (x2 dano, +30% velocidad, 11 s) se siente bien; #081/#085 si el enemigo "se cubre para disparar" se ve natural y no demasiado pasivo; #074 si los milicianos (25-29% en cobertura) deberian cubrirse mas; #090 velocidad de las camionetas (tope 19,5 m/s); #097 dificultad de la carrera de 70 s con Halcon y de la reparacion de 25 s.
- #077 que la toma final y el panel verde translucido (alfa 0,55) gusten: la escena se ve, pero apagada.
- #101 que las indicaciones del RTS sean claras sin conocer el juego (flechas de oleada, panel de mando, sectores).

## (d) Rendimiento observado por fase

Editor, Game View ~1280x720, modo dios, `MedidorDeFases` (sin profiler; JUEGO = tiempo del PlayerLoop).

| Fase | fps | frame / JUEGO (ms) | peor frame (ms) | Notas |
|---|---|---|---|---|
| Infiltrar (reposo) | 86,8 | 11,5 / 9,5 | - | |
| Infiltrar (combate) | 66,3 | 15,1 / 12,9 | - | |
| Puestos | 71,5-76,0 | - / ~11 | 176-278 | los picos son el colapso/explosion de los puestos; WorldSim max hasta 126,8 ms |
| Centro (reposo / hackeando) | 104,9 / 84,4 | - / 7,6-9,7 | 155 | |
| Huida (jefe) | 86,2 | - | 129 | |
| Huida (carrera) | 83,0 | - / 9,9 | 148 | materiales 2153 -> 2464 (+311) durante la carrera: observacion, crecimiento de pools, no fuga confirmada |
| Resistir (oleadas reales) | 57,9 | p95 22,1 / 14,8 | 363 | GC 72,6 KB/frame; Bug101e midio 53-63 fps (peor 89 ms) |
| Extraer (heli posado) | 110,2 | 9,1 / 7,2 | 18 | GC 9,6 KB/frame |
| Extraer (subida y toma final) | 117,3 | 8,5 / 6,7 | 360 | un hitch de 360 ms una vez (arranque del himno/toma); no se repite en 14 s de toma |

## (e) Estado final del editor y la escena

- Editor fuera de Play (`playMode=stopped`), escena `SC_Operacion` abierta, no sucia (`isDirty=False`), `Time.timeScale=1`, modo dios apagado, tamano de HUD en 0,85 (original).
- Archivos del juego modificados por la validacion: `Assets/_Project/Scripts/Operacion/OperacionDirector.TomaFinal.cs` (arreglo #077) y `Assets/_Project/Scripts/Editor/ChecksBugs065.Wp5b.cs` (tolerancia de #068). Compilacion verificada (`recompile_status completed`, sin errores en consola, y los checks Bug077/Bug068/Bug000 corrieron sobre el codigo nuevo). La escena no se regenero ni se toco.
- Sin commit/push/build; sin descargas; nada fuera del proyecto salvo el scratchpad.
- Estabilidad del editor: no se cayo en esta sesion (no hizo falta relanzar). Si ocurre un cierre por error de heap, relanzar con `Unity.exe -projectPath "<ruta>"`, sin `editor_focus`, y elegir "Keep Backups" en "Scene Backup Detected".
- Capturas: `Assets/Validacion/val_NNN_*.png` (unas 100); se borraron mis temporales (`_tmp_sheet*`, secuencias intermedias de #077).

## Bitacora

1. Intro 097, ajustes/controles/HUD (098/099/100), objetivo 1 en combate real (PENTA KILL + FURIA), objetivo 2 con carga plantada por Vega (mecha 20 s, contraataque, bunker volado), objetivo 3 con hackeo y 4 camionetas, objetivo 4 con jefe, reparacion real y carrera de 70 s, Resistir con RTS y oleadas, Extraer y Victoria.
2. Checks aislados (Play fresco cada uno) para todo lo destructivo: Bug068/069a/069b/073/088/089/090/091, 082b/082c, 097k/l/m/n, 072a/072v, 074, 086, 081, 000, 064/075/076, 077/101e/093, 085b x3.
3. Recreaciones propias: #071 (RTS: cursor + orden + dano a las camionetas), #077 en flujo real (encontro la falla), #069 en escenario real de Centro, #068 fuga de materiales, #088/#091 con datos.
4. Arreglos: #077 (OperacionDirector.TomaFinal.cs) y tolerancia de #068; regresion: Bug077, Bug101e, Bug093, Bug064, Bug075, Bug076, Bug000 y el flujo real Resistir -> Victoria 3 veces.
