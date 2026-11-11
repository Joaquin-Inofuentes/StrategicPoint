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
- **#602c** (regresión): **resuelto, era del check.** Tomaba como molde al primer enemigo del registro, que era un `Mansion_Enemigo_1` inactivo en la jerarquia; su clon nunca corría `Start` y quedaba con vida 180 y `AC_Soldado`. Ahora exige un molde activo y pasa (180 → 2160, `AC_Pesado`).
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
`Bug602c` en verde tras corregir el molde del check. No se corrieron `Bug122` ni `Bug123` (no existen con ese nombre en `ChecksBugs065*`).
Atlas: `Docs/ATLAS_bugs_396_414.jpg`.

## 7. Recorrido completo instrumentado (`Bug700a`, 2026-11-09)
Salta por los 6 objetivos en modo dios, deja correr 20 s de juego en cada uno y mide: errores/excepciones de consola, soldados bajo el mapa o con NaN, jugador poseído. Resultado: **0 errores en consola, 0 anomalias**; el unico "fallo" inicial era una regla del check (en el objetivo 6 la partida termina en Victoria y no hay soldado poseido). No es una partida jugada a mano: el bot no mueve al jugador ni dispara, asi que no cubre combate real, puntería ni sensación de manejo.

## 8. Revision Opus (2026-11-11)
Revision independiente (Opus 5.5) de las afirmaciones de este informe y mejora del diseno del nivel. Notas completas: `Docs/NOTAS_OPUS_NIVEL.md`. Checks nuevos en `ChecksBugs065.NivelOpus.cs` (Bug710a-715a); fotos `Assets/Validacion~/v10_*`.

### 8.1 Verificacion de lo afirmado
- **Bug602c no es una regresion de este commit.** El diff `cdbe52fd..9041e019` no toca el registro de actores, `EnemigoPesado`, `Soldier` ni la activacion de enemigos de la mansion, y `602[a-z]` ya estaba en `reMansion` antes. El check tomaba como molde al primer enemigo del `ActorRegistry` (orden fragil: podia ser un `Mansion_Enemigo` inactivo en la jerarquia). Arreglar el check (molde activo) es correcto. Limite: no se pudo correr el codigo viejo (no hay segundo Editor); la conclusion sale de leer el diff.
- **#402 (destructibilidad): 169 convertidos, ninguno con logica de juego.** `Bug710a` vuelca cada objeto convertido: 0 referenciados por campos del Director, 0 con nombre de terminal/baliza/heli/cobertura/etc., 0 `NavMeshObstacle`, 0 `Rigidbody`. El "0" de `TieneLogicaPropia` era cierto, pero **`Bug402a` no lo demostraba**: clasifica con las mismas funciones que el conversor, asi que `sinClasificar == 0` esta garantizado por construccion (no puede fallar).
- **Bug real encontrado en #402:** el filtro de estructura por prefijo (`PB_`, `P1_`, `P2_`, `Azotea`) dejaba **indestructible todo el mobiliario de la mansion** (32 piezas: sofas, mesas, piano, barra, mostrador, mamparas, equipos de azotea; 0/32 con `ObstacleMarker`). Los 169 convertidos eran todos de la ciudad/plaza. Corregido con una lista de mobiliario en `DestructibilidadDelNivel.EsMobiliario` (`Bug712a`: 0/32 -> 32/32).
- **#405 (1 cm por nombre): sin hallazgos.** `Bug711a`: los 63 objetos con nombre de la tabla estan todos dentro de la huella de la mansion, 0 fuera (el unico nombre ambiguo, `Marco_S` x24, ya filtra por z=246).
- Otros checks que miden menos de lo que dicen: `Bug401a` comprueba la bandera `SiempreEncima`, no el render; `Bug403a` no reproduce nada (causa no confirmada, como ya decia el informe).

### 8.2 Diseno del nivel: auditoria y mejoras
Auditoria (mosaico cenital, fotos por piso, censo de props, recorrido de la ruta del tanque con una caja del tamano del tanque):
- La ruta del tanque (1259 m) estaba despejada salvo **4 neumaticos de ambientacion** (`P_Env_Neumaticos`) sobre la ruta en (206, 231).
- La **galeria del 1.er piso** (14 x 50 m, donde salen las emboscadas de los seis cuartos) tenia 3 muebles: una pista vacia sin cobertura.
- La mansion tiene proposito claro por piso (PB vestibulo con pilares, 1.o galeria + 6 cuartos, 2.o salon + celdas, azotea helipuerto con 8 coberturas); un solo ascenso por piso en esquinas opuestas obliga a cruzar cada nivel en diagonal (bien).

Implementado (parche en juego `Presentation/AjustesDeNivelOpus.cs`, builder `OperacionBuilder.Mansion.cs` crea lo mismo con el mismo nombre; no se reconstruyo la escena):
1. **6 bancos bajos en la galeria** alternados a ambos lados de la alfombra: destructibles, con carving de NavMesh, registrados como cobertura de la IA, carril central libre, >= 3,8 m de los puntos de aparicion, camino entre torres A y B 41,3 m para 40,7 m en recta, y a >= 3,4 m de la salida de la torre A y la entrada de la B. `Bug715a`. **Error propio cazado por la regresion:** la primera posicion del banco norte (304; 270,4) tapaba la salida de la torre A y `Bug409u` (hay camino por la torre A con la reja abierta) dio `PathInvalid`; se movio el banco y `Bug715a` ahora mide esa distancia.
2. **Ruta del tanque despejada:** se apagan los props `P_Env_*` chicos que caen sobre la ruta. `Bug714a` (rojo antes: 4 neumaticos; verde despues).
3. **Mobiliario de la mansion destructible** (ver 8.1). `Bug712a`.

### 8.3 Pendiente / no cubierto
- No se jugo una partida a mano; los checks miden estructura, no sensacion.
- Ideas no implementadas (riesgo/valor): senalizacion por color de piso en las torres, mas cobertura en el 2.o piso, props de ambientacion en el interior del bucle de la ruta del tanque (zona vacia en el mosaico cenital; el trabajo de Nivel 2 ya toco la ambientacion).
- El working tree tenia cambios ajenos sin commitear (rendimiento, Nivel 2 #415-422); no se incluyeron en el commit de esta revision.
