# Revalidación de los bugs del 11/10 (#168–#220) — 12/10/2026

Escena actual: con las pasadas de arte 1 y 2 (`ff96847`…`6cb7e52`) y la cobertura ordenada (`ba26ff6`).
Se volvieron a correr las mismas pruebas de ayer, más un chequeo nuevo para el #168 al #174, que no tenían script.

| Bug | Qué se midió hoy | Resultado |
|---|---|---|
| 168 / 175 / 194 | Enemigo más cercano congelado y fotografiado de frente y de costado | OK: se ve (camisa azul, casco verde) con `MAT_Trimsheet_Enemigo_Brillo` (`B194-206/revalida_1012/194_enemigo_congelado_*`) |
| 169 | Se demuelen las 2 torretas de artillería con 45 enemigos vivos | OK: la fase pasa a Puestos con 43 enemigos todavía vivos |
| 170 / 186 | 8 enemigos por un paso de 3 m | OK: 8/8, ninguna parada (ver nota 1) |
| 171 | Emplazamiento de las torretas fijas | OK: 2 torretas, 34 sacos, anillos |
| 172 | Camionetas a la vez / vida del tanque | OK: el tope es la constante `AutosSimultaneosBase`=2. El tanque arranca la huida en 1600/1600 y baja con los disparos |
| 173 | Instancias de `AvisoUnico` | OK: 1 |
| 174 | Suite headless (check "Tab entra a uno de los seleccionados") | OK (no está entre las fallas) |
| 177 / 182 | 3 aliados con orden de mover por el mismo paso | OK: 3/3, sin paradas |
| 179 | Volumen de la voz de órdenes | OK: 0,4 (antes 0,8) en `OrderService` y `OperacionDirector.Mando` |
| 180 | Clic derecho en RTS sobre la computadora | OK: `orden=True COMPUTADORA` (ver nota 3) |
| 181 | Curva de daño enemigo según tu vida | OK por código: ×2,0 con vida llena, ×0,18 con vida casi nula |
| 184 | Vehículos al empezar la huida | OK: 0 autos |
| 190 | Aliados en cobertura por orden contra 3 enemigos | OK: los 3 enemigos muertos, aliados con 1458 de vida |
| 191 | Camioneta de oleada | OK: queda a unos 16 m y no avanza hacia los soldados |
| 193 | Disparos enemigos contra paredes en Extraer | OK: 0 impactos contra paredes |
| 196, 197/198, 203, 205, 206 | Igual que ayer | OK (#197/#198 hoy con `cursor=Interactuar` y el médico enviado) |
| 195, 204 | Cono de la ráfaga y efectos con zoom | OK: 2,1° y sin fogonazo con zoom |
| 199 | Seguimiento en círculo | OK: peor distancia 3,1 m |
| 200 | Órdenes a destinos dentro de sólidos | OK: a los 37 s las dos órdenes quedaron cerradas |
| 201 | Demolición de muro | OK: 29 trozos hacia el lado del golpe y 19 hacia el opuesto |
| 202 | Cohetes del helicóptero en la huida | OK: 0 cohetes |
| 207, 208/213, 209, 210, 211, 215, 216, 217, 218, 219, 220 | Igual que ayer | OK |
| 212 / 214 | 16 reservas del centro de datos rumbo al jugador | OK y mejor que ayer: 12 llegaron (ayer 9), 0 trabados |

## Notas: tres fallos que eran de la prueba, no del juego

1. **Arena de prueba sin NavMesh.** `ZzArena` ponía la `NavMeshSurface` en un objeto vacío con `CollectObjects.Children`, así que la arena nunca tuvo malla.
   - Ayer pasaba porque la navegación vieja tenía su propio A*.
   - Hoy los soldados caminaban con el respaldo sin NavMesh, y por eso salieron 7/8 y 2/3.
   - Con la superficie en la raíz da 8/8 y 3/3.
   - Además los soldados de la arena nacían hundidos 0,7 m (pivote a 0,1 en vez de 0,8), y uno no lograba salir.
2. **#194 mal encuadrado** (ayer y hoy): `run2.sh` cortaba la salida en 80 caracteres y la Z -322,3 quedaba como -32. Corregido a 300.
3. **#180:** el mouse virtual no se movía con el editor sin foco. Con el rayo calculado en el mismo cuadro, la orden sale bien.

Scripts de la arena guardados en `B175-193/scripts_de_prueba/ZzArena.cs.txt` y `ZzMedir.cs.txt` (antes eran temporales y no estaban en git).
