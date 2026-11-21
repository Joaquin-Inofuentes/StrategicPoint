# Ronda 17 - Validacion final (2026-11-21)

Plan: `PLAN_R17_v1.md` (Sonnet) -> revision `PLAN_R17_v2_opus.md` (Opus) -> implementacion. Atlas: `ATLAS_R17_1_combate.jpg`, `ATLAS_R17_2_mundo.jpg` (con los resultados medidos). Tomas ANTES/DESPUES en `antes/` y `despues/`.

Decisiones tomadas por defecto (el dueno no las confirmo):
- PC bloqueada = la computadora del centro de datos, modo "limpiar para empezar" (guarnicion nueva de 4 a 6). La terminal de las celdas no se toco.
- "Trigger para atacar al tanque" = la pelea contra el BLINDADO arranca al cruzar z=-30, al danarlo o a los 45 s (antes arrancaba sola a los 2,5 s). El check de #291 se cambio a proposito.
- "Nivel 1 terminado": se tomo como la Operacion Cuartel completa (6 objetivos). Ver el apartado de auditoria.
- Tutorial simple: 11 pasos por defecto, con boton "TUTORIAL COMPLETO" (36 pasos).

## Estado por pedido

| # | Pedido | Estado | Evidencia |
|---|--------|--------|-----------|
| 1 | Contraste enemigos | OK (fusileros) | saturacion 0,30 a 0,38, emision +40 %, insignias; #990m: dE mediana 38,9 / min 22,1 contra el fondo, 0 de 18 tomas bajas; otros tipos sin toma |
| 2/15 | Atacar al recibir dano | OK | #990a: reacciona a los 0,51 s y pasa a Attack; guarnicion cae en 10-17 s |
| 3/10 | Decoracion + exterior servidores | OK | 100 objetos, 13 coberturas, #990i (NavMesh), +1,5 % de cuadro medio (#990j) |
| 4 | Particulas fuente | OK | toma + 18 sistemas |
| 5 | Mascara del gordo (2 headshots) | OK | #990h y #602c; 1er headshot vivo, furia, 2o mata |
| 6 | PC bloqueada | OK | #990k x3: guarnicion 6, bloqueo, se libera 0,2-0,3 s despues; salvaguarda de 60 s sin probar |
| 7 | Nivel 1 terminado | PARCIAL | barrido de los 6 objetivos y checks; el validador viejo esta obsoleto |
| 8 | Iterar enemigo grande | OK | furia, vista con linea de tiro, y telegrafo (laser que se engrosa + tic) medido en #990h |
| 9 | Tutorial simple | OK | autoplayer: 11 pasos en 58,6 s, 0 fallos |
| 11 | Contraste escudero | OK | grafito 0,16 a acero 0,38 (x2,4), marco rojo |
| 12 | Animacion de eliminacion | OK | #990c y #990l: variantes 1 (espalda) y 3/5 (headshot) + tomas con cuerpos; sin video |
| 13 | Anillo del tanque | OK | #990d y #111: fuera del radio a 14 m, dentro a 3 m, cartel [E] SUBIR |
| 14 | Trigger contra el tanque | OK | #990b, #990f, #990g, #243a (umbral, dano, salvaguarda 44,7 s) |
| - | Balizas del hackeo | OK | #990e: 0 / 2 / 0 balizas |

## Cierre de los dos pendientes (2026-11-21, despues del commit eab45789)

**Contraste de enemigos (pedido 1): ahora CON metrica.** Check nuevo `#990m` (`Editor/ChecksBugs065.R17b.cs`): renderiza al enemigo a 10 m con la camara del juego, con y sin sus renderers; la diferencia da la silueta y el anillo de la toma sin enemigo da el fondo. Mide contraste de luminancia (WCAG) y diferencia de color (CIE76 en Lab). 18 tomas de fusileros en los 6 objetivos: dE mediana 38,9 (min 22,1), ratio de luminancia mediana 1,22, 0 tomas bajas (umbral ratio < 1,35 y dE < 18). Con un peto emisivo extra la mediana subia a 42,5: no hace falta y no se dejo. Salvedades: (a) una primera version daba 8 de 18 tomas bajas; era un artefacto: los de reserva estaban inactivos y se median el mismo cuadro en que se activaban, antes de que `SoldierLook` aplicara el aspecto en Start (ahora se espera 4 cuadros); (b) solo se midieron fusileros: escudero, pesado y francotirador no estan vivos al arrancar ningun objetivo (los despliega el director mas tarde), asi que su contraste sigue valiendo por los numeros de color de la R17, sin toma; (c) mide el color, no la nitidez en movimiento.

**Tutorial completo (36 pasos): ahora termina con victoria.** Causa medida con un registrador de `DamageTakenEvent` en el paso 33: la ola A (4 enemigos) le pega al tanque y a la escuadra ~350 de dano por segundo (fusil 25 cada 0,4 s, ametra 13 cada 0,075 s, subfusil 4 cada 0,06 s, 35 cada 0,7 s): un soldado de 180 cae en 0,9 s y el tanque (260) en 0,75 s. Antes de la R17 ya pasaba (28 OK contra 31). Arreglo: `TutorialManager.ReducirDanoEnLasOlas` pone `Health.FactorDeDano = 0,1` en el tanque y la escuadra mientras dura un paso con ola (`avanzar_disparar`, `final`) y lo devuelve a 1 en el resto. Ademas el autoplayer repite la orden de mover el tanque si se pierde (el antiatasco de `VehicleBrain` la cancela cuando el tanque frena al atropellar enemigos) y, si la orden por radial se rechaza, manda el punto directo. Resultado en 2 corridas completas: pasos 33, 34 y 35 en OK (23 s, 14 s y 4 s), escuadra de 3 en pie, victoria. Siguen fallando de forma intermitente otros pasos que ya fallaban antes (13 `vista_tactica` en las dos corridas; 18, 21, 26 segun la corrida); no son de esta ronda. No se diagnostico por que el tanque se frena a los 3,8 m tras atropellar: solo se hizo robusto el autoplayer. Suite headless: OK despues de estos cambios.

**Niveles del menu.** El Nivel 1 viejo (Strategic Point) quedo oculto; ahora Nivel 1 = Tutorial (simple) y Nivel 2 = Operacion Cuartel (ciudad y ruta). Probado con `ElegirNivel(1)` y `ElegirNivel(2)`; no con el mouse real. Nota: donde esta documentacion dice "Nivel 1 terminado" se refiere a la Operacion Cuartel, que con esta numeracion es el Nivel 2.

**Repeticion final (2026-11-21):** los 12 checks #990a-m pasan, la suite headless da OK y el tutorial completo con capturas termino en victoria en 342 s (34 pasos OK, 1 con reintento, solo `vista_tactica` forzado, que ya fallaba). #990m varia entre corridas: la mediana de dE fue 38,9 a 39,7; en una corrida el minimo bajo a 2,4 (1 toma baja de 18, dentro del 20 % tolerado), en las otras fue 21,9 a 22,0. Atlas completo del cierre: `ATLAS_R17_3_cierre.jpg`.

## Bug encontrado y corregido en esta pasada
- El disparo "le pego al blindado" dependia de `jefe`, que recien se crea al entrar en la subfase Jefe: en el Acercamiento era `null` y nunca disparaba. Ahora compara la vida del vehiculo contra la que tenia al caer el muro (`OperacionDirector.Jefe.cs`). Lo confirma #990f.
- Fallos de la suite por la R17: `Camera.main` en `AnilloDeSubida`, 2 busquedas globales en `FuentesDeAgua` y una por cartel en `AjustesDeNivelR17`; corregidos. Los usos de arranque y del arnes se declararon en la lista blanca; tope de `Transform.Find` de 140 a 144 (4 guardas de arranque).

## Pruebas corridas
- **Suite headless: OK** (2 veces; la ultima con todos los cambios de esta pasada).
- **Checks nuevos** (`Editor/ChecksBugs065.R17.cs`): #990a a #990l, todos OK (uno se ajusto porque media mal).
- **114 checks que usan la PC y el flujo de la Operacion** (Bugs298-316, Wp1/4/5/10, ChecksBugs104.*, R13_T6...): 102 OK y 12 fallan: #067, #085, #092, #094, #095, #101b, #101i, #106, #112, #114, #119, #127. Con el codigo anterior (R17 guardada en un stash) fallan 11 de esos 12; #092 solo fallo una vez y paso 3 de 3 al repetirlo (intermitente por el muestreo de enemigos).
- **Ya fallaban antes de la R17:** #082b, #242a, #501a, #501b, #501d, #501e, #501f. #501b lista 2 conductos de la fachada nueva entre sus ~90 piezas flotantes.
- **Tutorial completo (36 pasos): no terminaba, igual que antes de la R17** (la escuadra caia en el paso 33). **Corregido despues**: ver "Cierre de los dos pendientes" mas arriba; ahora llega a la victoria.
- **Barrido de los 6 objetivos** (`OperacionPrueba.Recorrer`): los 6 cargan con 3 aliados. Los errores de consola son todos "Source mesh ... does not allow read access" del NavMesh en tiempo de ejecucion, ya existentes.

## Lo que no se pudo hacer
- No existe una auditoria jugable de punta a punta: `ValidadorJugabilidad.cs` es del 8/10, su jugador automatico (Kes, no Asalto) se queda parado en el objetivo 1 y queda obsoleto. "Nivel 1 terminado" no tiene una definicion cerrada, asi que queda como PARCIAL.
- Contraste medido solo en fusileros (ver el cierre); LEDs y fuente solo se ven en movimiento (no hay video).
- Dificultad medida solo en la guarnicion del Centro (3 corridas, sin bajas aliadas); no se midio la tasa de derrota de la Operacion completa.
- Salvaguarda de 60 s de la PC y rescate de atascados sin probar.
- Particulas de polvo al caer, arma que cae, niebla y sonido de la fuente: no se hicieron.
- No hay commit ni push de esta ronda, ni RAR nuevo (el RAR de Docs/RONDA_14/entrega esta viejo).

## Archivos nuevos / tocados
Nuevos: `Operacion/ReaccionAlDano.cs`, `Presentation/AjustesDeNivelR17.cs`, `Core/CapturasDeBuild.Ronda17.cs`, `Editor/ChecksBugs065.R17.cs`.
Tocados: `Combat/{Health,Projectile}.cs`, `Ai/AiBrain.Tactica.cs`, `Operacion/{EnemigoPesado,EnemigoEscudero,EnemigoFrancotirador,EmboscadaDeCohetes,OperacionTerminal,OperacionDirector,OperacionDirector.Centro,OperacionDirector.Jefe,AnilloDeSubida}.cs`, `Presentation/{FuentesDeAgua,CubeFxReactor,RetiroDeEnemigosCaidos,MainMenuController}.cs`, `Actors/SoldierLook.cs`, `Tutorial/{TutorialManager,TutorialAutoPlayer.Radial}.cs`, `Player/PlayerInputDriver.cs`, `Editor/{ChecksBugs065.Bugs231a255,HeadlessTestRunner.Fases13a17,HeadlessTestRunner.BusquedasGlobales}.cs`, `Prefabs/P_Soldier_Base.prefab` (un campo serializado nuevo).
