# Notas de trabajo de la sesión opus-nivel (memoria persistente)

Releer al arrancar cada turno. Sesión: Opus (nombre `opus-nivel`), tarea del 2026-11-11.

## Estado del entorno (15:46)
- HEAD real es `41c51a39` (hay 4 commits después de 9041e019: Bug700a, Bug701a, regresión).
- El working tree trae trabajo AJENO sin commitear (perf, Nivel2 #415-422: `AjustesDeNivelNov11`, `Nov11.cs`, `PerfFrames`, Soldier.cs, Vehicle.cs, RampaCaminable...). NO commitear eso: sólo mis archivos.
- El Editor estaba ocupado (editor_status timeout) a las 15:46 con una corrida ajena que escribe `Registros/sesion_*`. Esperar a que quede libre antes de correr checks.
- `reMansion` (ChecksBugs065.cs) sólo acepta 700a|701a y 8xx: para mis checks 7xx hay que ampliar el regex a `7[0-9][0-9][a-h]`.

## Tarea 1 — hallazgos (15:5x)
- (a) Bug602c: el diff cdbe52fd..9041e019 NO toca registro de actores, Pesado, Soldier ni activacion de enemigos de mansion; `602[a-z]` ya estaba en reMansion en cdbe52fd. => no es regresion de codigo de juego: el check tomaba como molde al primer enemigo del ActorRegistry, que puede ser un Mansion_Enemigo inactivo en la jerarquia (orden fragil). Arreglo del check (exigir activeInHierarchy) es correcto. No pude correr el codigo viejo (sin 2.o Editor).
- (b) `Bug402a` es tautologico: clasifica con las MISMAS funciones del conversor, `sinClasificar==0` esta garantizado. Dump real (Bug710a): 169 convertidos, 0 referenciados por campos del Director, 0 con nombre de logica, 0 NavMeshObstacle/Rigidbody => sin props de logica convertidos por error. PERO el filtro `Estructura` (prefijos P1_/P2_/PB_/Azotea) deja INDESTRUCTIBLE todo el mobiliario de la mansion (PB_Sofa, P1_Mesa, P2_Piano, P2_Barra, Azotea_Equipo...): contradice #402. Los 169 son de la ciudad/plaza (Arco_Pilar, Tronco, Cabina...). Ver Bug712a.
- (c) Bug711a: los 63 objetos movidos 1 cm por nombre estan todos DENTRO de la huella de la mansion; 0 fuera. Marco_S x24 (filtro z=246 ok). Sin hallazgo.
- Bug401a mide la bandera `SiempreEncima`, no el render; Bug403a no reproduce nada (causa no confirmada).
- Dump: Assets/Validacion~/v10_710a_convertidos.txt, v10_711a_desplazados.txt

## Tarea 2 — implementado (16:5x-17:1x)
- Parche `Presentation/AjustesDeNivelOpus.cs` (watcher propio, cada 2 s) + builder `OperacionBuilder.Mansion.cs` (mismos puntos/nombre `P1_Banco_Galeria`): 6 bancos en la galeria del 1.er piso; apaga props `P_Env_*` chicos sobre la ruta del tanque (4 neumaticos en 206,231).
- `DestructibilidadDelNivel.EsMobiliario`: 32 piezas de mobiliario de la mansion ahora destructibles.
- Checks: Bug710a (volcado convertidos), 711a (desplazados 1 cm), 712a (mobiliario), 713a (fotos), 714a (ruta tanque), 715a (galeria). `reMansion` ampliado a `7[0-9][0-9][a-h]`.
- Error propio: banco norte en (304;270,4) tapaba la salida de la torre A -> Bug409u PathInvalid; movido, 715a mide distancia >= 3 m.
- Regresion pedida: toda en verde (3 tandas) + 712a/714a/715a.

## Qué falta
- Commit propio (sin push); el dueño confirma el push.
- Ideas no hechas: franja de color por piso en torres, mas cobertura 2.o piso, ambientacion dentro del bucle de la ruta.
- Cambios ajenos sin commitear en el working tree (rendimiento/Nivel2): no tocados.
