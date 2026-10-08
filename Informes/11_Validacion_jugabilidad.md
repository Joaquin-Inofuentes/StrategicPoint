# Validacion de jugabilidad - Operacion Cuartel

Fecha: 2026-10-07. Dificultad por defecto (FACIL, PlayerPrefs sin tocar). Escena SC_Operacion.

## Metodo

- Jugador automatico `Assets/_Project/Scripts/Editor/ValidadorJugabilidad.cs` (herramienta de prueba, no toca el juego). Usa las mecanicas reales por las APIs publicas: caminar por NavMesh con Shift, `Fire`, recarga y cambio de arma, `EnviarA` para cargas y computadora, posesion de soldados, radio y ordenes del mando tactico, revivir con E sostenida 5 s, cohetes y cajas de suministros contra el jefe, cañon del tanque en la carrera, E en el helicoptero.
- Registro en `Temp/jugabilidad.log` y `Temp/jugabilidad_result.txt`.
- Intros salteadas (`ModoPrueba`). El trayecto del tanque corrio en tiempo real.
- Advertencia: la punteria del bot es peor que la de una persona (ver impactos/disparos), asi que el consumo de municion es una cota alta; la dificultad de Resistir esta sobreestimada o subestimada segun el bot (no coordina como un humano, pero tampoco se frustra).
- Cadena 1 a 6 sin saltos, god mode solo en la fase 5 (la regla del brief: sin humano la escuadra muere en Resistir; se apago antes de embarcar).

## Tabla por fase (cadena final, tiempos en segundos de juego)

| Fase | Tiempo | Muertes / casi-muertes | Municion (disparos del bot, impactos) | Veredicto |
|---|---|---|---|---|
| 1 Infiltrar | 94.8 | 0 / 0 | 257 (26), cargador SMG 216 -> 7 | Completable. Falta de municion al final. |
| 2 Puestos | 86.6 | 0 / 0 | 99 (22), 216 -> 117 | Completable, claro. |
| 3 Centro de Datos | 54.4 | 0 / 0 | 96 (18), 216 -> 120 | Completable, claro. |
| 4 Huir / Blindado | 192.6 | 0 / 0 | 39 (13); carrera: 19 de 23 vehiculos, 45 obuses | Completable; tiempo muerto en Reparacion si cae Kes. |
| 5 Resistir (god) | 175.0 | 0 (god) | 52 (30) | Completable; sin god depende del jugador (ver abajo). |
| 6 Extraer | 52.7 | 0 / 0 | - | Completable; 7 abordaron. Victoria. |

Total encadenado: **657.6 s de juego (659.8 s reales)**. Estado final: Victoria, 134 bajas (66 del bot, 43 de aliados), 28 vehiculos destruidos.

Otras muestras (corridas sueltas, 4 a 12 intentos por fase):
- Fase 1: 150-186 s (con versiones previas del bot; 94.8 s es el mejor caso). Fase 2: 87-91 s. Fase 3: 55-59.5 s.
- Fase 4: 166 s, 219 s, 223 s, 272 s (tres de esas con 1 muerte). Jefe 39-56 s; Reparacion 38-129 s; Abordaje menos de 2 s; Carrera 79.5 s.
- Fase 5 sin god mode, con el bot final: **238.0 s (4 caidos, 4 reanimados, 5 casi-muertes)**, 241.6 s (5 caidos, todos reanimados), 237 s y 265.5 s. Con versiones anteriores del bot hubo mas fallos (estancamientos o escuadra aniquilada). Sobre unos 8 intentos totales, 4 terminaron en exito natural. Con god mode es determinista (150-175 s).
- Fase 6 desde salto: 26.8 s.

Objetivos imposibles o trabados: ninguno que bloquee. Atascos observados: ver "Bugs y rarezas".

## Diversion

### Momentos muertos de mas de 30 s
- Fase 4, Reparacion con Kes caida: la reparacion se pausa hasta revivirla, 80-130 s sin avance (el peor momento muerto del nivel). Con Kes viva dura 38 s.
- Fase 4, tramo de carrera: hasta 92.9 s sin enemigos a menos de 40 m (el viaje de 60-70 s del tanque; es el trayecto y el cañon tiene objetivos lejanos).
- Fase 5: el reloj de 90 s se pausa 40-45 s cuando un sector cae o queda sin defensores; es tension, pero para quien no sepa recuperarlo (2 aliados dentro 4 s) se siente trabado.
- Fase 1: los ultimos 3 enemigos tardan unos 40 s en aparecer/ser alcanzados.
- Fase 6: espera ~36 s al helicoptero hasta que todos los aliados estan en la zona.
- Menores (menos de 30 s): mecha de la carga (20-24 s) y hackeo (17-26 s). Aceptables.

### Picos de dificultad injustos
- Fase 5 sin god mode: la combinacion de camiones en B/C, oleadas dobles A+C y la caida de sector en 6 s es el unico pico duro. Con el bot hubo hasta 9 muertes de milicianos en una corrida (reanimadas). Un humano coordinando mejor lo pasa, pero el anuncio de 8 s deja poco tiempo para reubicar.
- Fase 4 jefe: sin cajas de suministros el cohete no alcanza en Dificil (ver municion).

### Municion y curacion
- FACIL (8 cargadores): sobra en fases 2, 3 y 4; en la fase 1 el SMG queda en 7 balas (216 -> 7) y el bot debio pasar a pistola. Se recoge municion de los caidos (`MunicionPickup`, 45 s de vida).
- En DIFICIL el SMG tiene 120 balas y 5 cohetes; el jefe necesita cerca de 7 cohetes, asi que depende totalmente de las cajas.
- Curacion: reanimar por E 5 s funciona, pero en Reparacion no hay quien lo haga solo (se quiere un medico automatico o un mensaje claro).

### Claridad del HUD
- Los textos de objetivo coinciden con la accion requerida en las 6 fases.
- Unica ambiguedad: fase 2 dice "solo el ASALTO coloca la carga [E]" mientras el jugador es Kes; el aviso junto a la carga ("SOLO EL ASALTO COLOCA LA CARGA · APUNTALE Y TOCA [Q]...") lo aclara. Se sugiere que la linea de objetivo ya mencione [Q].
- Fase 5: "SECTOR CAIDO: RECUPERALO CON 2 ALIADOS" es claro.

### Ajustes propuestos (no aplicados)
1. `MandoTactico.SegundosParaCaer`: 6 -> 10 (Operacion/MandoTactico.cs). Da margen para reubicar y baja el estancamiento del reloj en fase 5.
2. `MandoTactico.SegundosDeAnuncio`: 8 -> 12 (mismo archivo, linea 20). Mas tiempo para mover aliados antes de cada oleada.
3. Vida de milicianos en `OperacionDirector.ActivarMilicianos` (`s.Configure(..., 100)` en OperacionDirector.Mando.cs): 100 -> 160. Reduce las bajas de milicianos (hasta 9 en una corrida).
4. `OperacionDirector.CargadoresIniciales`: 4 -> 6 (valor de dificultad dificil). Con la punteria del bot se agota la municion contra el jefe.
5. Fase 4 Reparacion (OperacionDirector.Huida.cs): no pausar la reparacion indefinidamente si Kes cae; revivirla automaticamente tras ~15 s o reducir el daño de la oleada durante la reparacion. Quita 80-130 s de tiempo muerto.

## Bugs y rarezas

### Bugs del juego que bloqueen la finalizacion
Ninguno. No se modifico codigo del juego.

### Rarezas observadas
- Enemigo en estado Chase a menos de 20 m sin linea de vista que no llega a la escuadra: dejo a Kes sin objetivo mas de 130 s en la fase 1 (el bot lo resolvio caminando hacia su posicion). Un jugador lo resolveria igual, pero puede dar sensacion de nivel trabado.
- Camion enemigo estacionado mantiene viva una oleada hasta 50 s.
- La vida minima del tanque medida es 0.00 por la destruccion guionada al final (muestra no confiable).
- `rc.sh` muestra errores de consola viejos tras recompilar; se verifico por eval.

### Errores del bot corregidos (herramienta de prueba, no del juego)
Runner de corrutinas sin pila; `GetInstanceID` obsoleto; mantenimiento de sectores que oscilaba; `IniciarCampo` antes de completar los pasos guiados (PasoActualDelMando inicia en -1); recarga del cohete congelada por equipar arma cada frame; ordenes de ataque a camiones con milicianos de SMG; bloqueo con enemigo sin linea de vista; Kes caida sin reanimar; arma no reequipada entre fases.

El archivo `ValidadorJugabilidad.cs` queda en `Assets/_Project/Scripts/Editor/`: es util para repetir la prueba (`ValidadorJugabilidad.Iniciar(desde, hasta, encadenar, modoDios, diosSoloFase)`); se puede borrar si no se quiere conservar.

## Estado final del editor
Fuera de Play, SC_Operacion abierta, 0 scripts faltantes, god mode apagado, timeScale 1, validador detenido.
