# Informe: bugs #396–#414 (2026-11-08/09)

Plan: `Docs/PLAN_bugs_396_414.md` → mejorado por Opus 5.5 high en `Docs/PLAN_bugs_396_414_opus.md` → implementado aquí.
Checks nuevos en `Assets/_Project/Scripts/Editor/ChecksBugs065.Nov08b.cs`. Corren con `ChecksBugs065.CorrerLista("<nombres>")`.

## 1. Resumen por bug

| # | Qué pidió el reporte | Qué se hizo | Check | Estado |
|---|---|---|---|---|
| 396 | HUD del tanque: barras anchas, insignia de dios sin pisarse | `HudDeTanqueSimple` reescrito: vida 760×28, recarga 760×18, insignia de dios a la izquierda; el panel viejo `VehicleStatusView` y el cartel `ModoDiosView` quedan ocultos en el tanque | `Bug396b` | OK |
| 397 | La mira del artillero "snapea" contra obstáculos | `PuntoBajoLaMira` descarta impactos pegados al casco y la cobertura baja y chica a menos de 15 m (enemigos y vehículos siempre cuentan) | `Bug397f` | OK |
| 398 | Sin feedback al recibir daño; pocos tanques enemigos; daño ×2 | `ImpactoEnVehiculoView` (viñeta, sonido, sacudida, destello de la barra); tanques enemigos deterministas (3 a 6 por fuga); `MultiplicadorDeDanoDelTanque` 4 → 8 | `Bug398c` | OK |
| 399 | La barra de abajo no se actualiza | Era la barra de **recarga** y seguía a la ametralladora: ahora sigue a `v.TorretaCanon` | `Bug399a` | OK |
| 400 | Proyectil al triple | Sólo el cañón del tanque del jugador: 130 → 390 m/s (`TurretWeapon.MultiplicadorDeVelocidad`); ametralladora y torretas de la IA sin cambio | `Bug400a` | OK |
| 401 | Rombos de enemigos detrás de obstáculos | `UnitLocatorCylinder.SiempreEncima => true` (revierte #174; sigue la compuerta de revelado) | `Bug401a` | OK |
| 402 | Todo destructible | `DestructibilidadDelNivel`: ObstacleMarker con vida por volumen a todo collider con malla a < 60 m de la plaza o dentro de la mansión que no sea estructura ni tenga lógica propia | `Bug402a` | OK |
| 403 | Partículas / fragmentos trabados | No se reprodujo (0 fragmentos tras 10 s de juego, 0 partículas trabadas). Las "cuadrículas doradas" de la captura eran la línea del #404 | `Bug403a` | OK (causa no confirmada) |
| 404 | Línea punteada amarilla | Era `GuiaDeMando/RutaDelPaso`. Se sigue calculando pero no se dibuja; `Asegurar()` destruye copias huérfanas | `Bug404a` | OK |
| 405 | Z-fighting | `DetectorDeSolapes` sobre la mansión: de 142 pares a 0 con área ≥ 1 m² (caras verticales). Se hunden 1 cm los rellenos de puertas, muros de torre, cuadros, estantes, etc. (`AjustesDeNivelNov08.DesplazamientosZFighting`) | `Bug405a` | OK |
| 406 | Aliados no pasan de piso | `RescatarAliadosDeOtroPiso` + reaparición detrás del jugador al cambiar de piso | `Bug406a` | OK |
| 407 | 3 luminarias de piso | Las 6 lámparas de pie fuera (parche + builder) | `Bug407a` | OK |
| 408 | Escenario del salón sin colisión | `Escenario`, `Escenario_Borde`, `Escenario_Telon` apagados sólo en el salón del 2.º piso (el grupo `Escenario` de la Huida queda) | `Bug408a` | OK |
| 409/410 | Colliders de disparos | Marcador `NoFrenaBalas` en las compuertas: ni las balas ni el apuntado las ven; el movimiento sigue frenado | `Bug409a` | OK |
| 411 | Bloque negro | 3 cajas `Azotea_Equipo` movidas fuera del hueco de las torres | `Bug411a` | OK |
| 412 | Órdenes rotas, un plano por piso | `AimTargeting` descarta impactos sobre el cielo raso del piso mostrado en RTS | `Bug412a` | OK |
| 413 | Aliados encerrados en la azotea | `TodosLosAliadosYaSubieron` exige estar fuera de la torre; rescate por reja | `Bug413a` | OK |
| 414 | Sólo 2 milicianos | `PonerPrisioneros` respeta `MandoTactico.CantidadDeMilicianos` | `Bug414a` | OK |

## 2. Decisiones que quedaron vetables (el dueño puede revertirlas)
1. #400: triple sólo para el cañón del jugador, no para todas las balas.
2. #398: ×2 interpretado como "daño que recibe el tanque del jugador" (4 → 8). Con 3 a 6 tanques enemigos, la fuga sin modo dios puede ser muy dura. `Bug403u` se ajustó a 4 s de fuego continuo.
3. #401: contradice el #174 (los enemigos respetaban la profundidad).
4. #402: es una regla automática, no una revisión pieza por pieza. La vida sale del volumen (120–500).
5. #407: se quitaron las 6 lámparas, no sólo las 3 de la fila norte.
6. #406: los aliados reaparecen detrás del jugador tras 6 s de cortesía.

## 3. Problemas encontrados en el camino
- **#404** tardó: las 3 líneas eran restos de corridas anteriores (`GuiaDeMando` sin instancia, con `hideFlags` DontSave y sin recarga de dominio).
- **#403a** medía con segundos reales: los fragmentos envejecen con `Time.deltaTime`, y el Editor corre a ~17 FPS. Ahora espera tiempo de juego.
- **#370a** (regresión): fallaba porque el control había pasado a otro soldado de la escuadra y el check seguía moviendo al primero. Ahora toma el poseído en ese momento. **Pendiente de entender:** por qué cambia el control en esa corrida.
- **#403u** (regresión) topaba un cohete en el 7 % de la vida, calibrado con ×4; con ×8 es el 14 %: el tope ahora escala con `MultiplicadorDeDanoDelTanque`.
- **#602c** (regresión) **sigue fallando**: el pesado clonado a mano desde un enemigo molde queda con `MaxHealth` 180 (esperaba ×12) y con el controlador `AC_Soldado`. El camino real sí funciona (`Bug389a` pasa: 180 → 2160). Sin evidencia de que lo cause este cambio; **hay que compararlo contra `cdbe52fd`** (puede ser una carrera: `Aplicar` corre antes del `Start` del clon).
- **`Bug402a`** necesitaba estar en `reMansion` (regex de checks con mansión).
- El Editor se cerró solo a mitad de una regresión larga (apagado ordenado, sin traza de crash en el log). Tandas de ≤ 8 checks.
- La carpeta temporal de la sesión se vació al reabrir el Editor: los scripts auxiliares (`c.sh`, atlas) se rehicieron.

## 4. Riesgos abiertos
- `DestructibilidadDelNivel` convierte unos 120 objetos; no se probó a mano jugar una partida completa con ellos. Revisar que ningún prop con lógica propia se haya vuelto destructible (el chequeo de "lógica propia" dio 0, algo a mirar: puede que el filtro por namespace sea demasiado estrecho).
- El z-fighting se corrige con desplazamientos de 1 cm hardcodeados por nombre en el parche; si el builder cambia las posiciones, hay que volver a pasar `Bug405a`.
- `AjustesDeNivelNov08.Aplicar` ahora recorre todos los colliders cada 3 s (una vez por collider con `HashSet`, pero el `FindObjectsByType` es por pasada).
- Rebuild del .exe/.rar pendiente (necesita OK del dueño; antes colgó el Editor).
- El token del Apps Script sigue embebido en `SubidaDeBug.cs`.

## 5. Cómo reproducir
```
unity command eval "return SP.EditorTools.ChecksBugs065.CorrerLista(\"Bug396b Bug399a Bug400a Bug401a Bug398c\");"
```
Fotos en `Assets/Validacion~/v9_*`.

## 6. Regresión final
En verde: `Bug369a Bug369d Bug370a Bug372a Bug374a Bug375d Bug378a Bug379a Bug381a Bug383a Bug384a Bug387a Bug389a Bug394a Bug395a Bug396a Bug397a-e Bug398a Bug398b Bug403u Bug409u Bug410u Bug101a Bug603g Bug128 Bug284a` y todos los nuevos (`Bug396b Bug397f Bug398c Bug398d Bug399a Bug400a Bug401a Bug402a Bug403a Bug404a Bug405a Bug406a Bug407a Bug408a Bug409a Bug411a Bug412a Bug413a Bug414a`).
En rojo: `Bug602c` (ver arriba). No se corrieron `Bug122` ni `Bug123` (no existen con ese nombre en `ChecksBugs065*`).
Atlas: `Docs/ATLAS_bugs_396_414.jpg`.
