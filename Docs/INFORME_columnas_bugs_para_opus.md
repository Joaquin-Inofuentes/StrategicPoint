# Informe para Opus 5.5: qué columnas le faltan al reporte de bugs de Strategic Point

## Contexto
Juego Unity 6 (Strategic Point, FPS+RTS). El jugador aprieta [U] y se guarda: captura, nota libre, y un .json de estado. Desde el .exe sube a un Google Sheet vía Apps Script; desde el Editor queda sólo en `Registros/` (bugs.txt + bug_N_*.json/png). Hay 395 bugs reportados. Hoy (2026-11-08) entraron 28 (#368-#395) y los corrige un agente (Claude) leyendo la nota + captura + JSON de estado.

## Columnas que hoy viajan al Sheet (SubidaDeBug.Armar)
Bug, Escena, Fase, Tiempo_sesion_s, Tiempo_juego_s, Reloj_operacion_s, Jugador, Modo, Asiento, Jugador_X/Y/Z/Yaw, Jugador_Vida, Jugador_VidaMax, Camara_X/Y/Z, Camara_RotX/Y/Z, Camara_FOV, Aliados_vivos, Enemigos_vivos, Enemigos_vivos_total, Vehiculos, Aliados (texto por línea), Vehiculos_detalle, Mision (k=v; ...), Diag_Tiempo, Diag_Jugador, Diag_Enemigos, Diag_Curacion, Archivo_local, Captura_local, más Descripcion, Comentarios, Estado_JSON (link a Drive) y la foto.

## Lo que trae el JSON de estado y NO llega a columnas
`jugador`(9 campos), `soldados`(~228 con equipo/rol/vida/pos/estadoIa/arma/municion/vehiculo), `vehiculos`, `mision`(19 pares), `diagnostico`(~51 líneas), `ultimasLineas`(~60 líneas de log). 

## Estadística de las 395 notas (heurística por palabras clave; una nota puede caer en varias)

- Pedido nuevo / feature ('quiero', 'falta', 'quita', 'deberia'): 207
- Visual / luces / UI / minimapa: 48
- IA / aliados / enemigos: 100
- Balance (vida, daño, velocidad): 31
- Helicópteros / vehículos / tanques: 47
- Colisión / atasco / snap: 34
- Audio / partículas / efectos: 36

Por fase: Resistir=97, Huir=90, Infiltrar=66, Puestos=63, CentroDeDatos=43, Extraer=27, Victoria=8, ?=1

## Ejemplos reales por tipo (nº | fase | pos | nota)

### Pedido nuevo / feature ('quiero', 'falta', 'quita', 'deberia')
- #388 | Resistir | Deberian ser solamente 2 milicianos menos y simplificados
- #389 | Resistir | Quiero q tenga 3 veces la vida q tiene ahora y sus disparos sean de 2 tipos cuando esta en movimientos disparo rafaga continua
pero cuando se detiene q sean explosivos como tanques
- #390 | Resistir | No veo la explosion del helicotpero al impactar quiero verla cuando imapcata con el edificio
- #391 | Extraer | FAltan mas enemigos aqui
- #392 | Extraer | Quita esta bandera
- #393 | Extraer | En el modo dios nose q velocidad tiena ahora pero quiero q sea el doble de la de ahora
### Visual / luces / UI / minimapa
- #377 | Huir | Su rombo enemigo no se ve  corrijelo y falta el minimapa
- #380 | Resistir | En el minimapa los obstaculos deberian verse transparentes y grises leves mientras q los enemigos deben hacer mucho contraste
- #382 | Resistir | Baja el brillo de las ventanas es mucho
- #383 | Resistir | Falta alinear mejor las lineas de agua de la fuente
- #387 | Resistir | Esto deberia estar en una pared y facil de ver y el resto de luces deberian tener menos intensidad
- #392 | Extraer | Quita esta bandera
### IA / aliados / enemigos
- #378 | Huir | Quiero ver los disparos de los vehiculos enemigos y quiero q haya tanques enemigos tambien con otro color y mas vida y mas daño
- #380 | Resistir | En el minimapa los obstaculos deberian verse transparentes y grises leves mientras q los enemigos deben hacer mucho contraste
- #381 | Resistir | Quiero poder derribar helicotperos enemigos y quiero q haya flancotiradores
- #384 | Resistir | Mis aliados no pasaron quiero q puedan pasar tras el obstaculo la primera ves y despues queden atrapados en este sector con migo
- #391 | Extraer | FAltan mas enemigos aqui
- #394 | Extraer | Creo q los enemigos se disparan entre ellos revisa q pasa y solucionalo
### Balance (vida, daño, velocidad)
- #374 | Huir | Se me snapea al obstaculo Corrijelo quiero q no se snapee o sino quita esos obstaculos y quiero q la UI la barra de vida aparesca en el centro abajo y oculta lo de los aliados temporalmente mientras estoy en el tanque. Cuando esto
- #378 | Huir | Quiero ver los disparos de los vehiculos enemigos y quiero q haya tanques enemigos tambien con otro color y mas vida y mas daño
- #385 | Resistir | El pesado deberia tener el doble de la vida q tiene ahora y su cedencia deberia ser excesiva y con particulas
- #388 | Resistir | Deberian ser solamente 2 milicianos menos y simplificados
- #389 | Resistir | Quiero q tenga 3 veces la vida q tiene ahora y sus disparos sean de 2 tipos cuando esta en movimientos disparo rafaga continua
pero cuando se detiene q sean explosivos como tanques
- #393 | Extraer | En el modo dios nose q velocidad tiena ahora pero quiero q sea el doble de la de ahora
### Helicópteros / vehículos / tanques
- #372 | Huir | Los helicopteros aparecen y se van de manera rara. Quiero q no se vayan de manera tan rara. Q tengan su animacion racional medio realista su animacion y sean derribables y tengan sonido disitnot al del helicotpero final
- #374 | Huir | Se me snapea al obstaculo Corrijelo quiero q no se snapee o sino quita esos obstaculos y quiero q la UI la barra de vida aparesca en el centro abajo y oculta lo de los aliados temporalmente mientras estoy en el tanque. Cuando esto
- #378 | Huir | Quiero ver los disparos de los vehiculos enemigos y quiero q haya tanques enemigos tambien con otro color y mas vida y mas daño
- #381 | Resistir | Quiero poder derribar helicotperos enemigos y quiero q haya flancotiradores
- #389 | Resistir | Quiero q tenga 3 veces la vida q tiene ahora y sus disparos sean de 2 tipos cuando esta en movimientos disparo rafaga continua
pero cuando se detiene q sean explosivos como tanques
- #390 | Resistir | No veo la explosion del helicotpero al impactar quiero verla cuando imapcata con el edificio
### Colisión / atasco / snap
- #374 | Huir | Se me snapea al obstaculo Corrijelo quiero q no se snapee o sino quita esos obstaculos y quiero q la UI la barra de vida aparesca en el centro abajo y oculta lo de los aliados temporalmente mientras estoy en el tanque. Cuando esto
- #376 | Huir | Aqui se nota mucho el snapeo solucionalo y testealo quiero q no snapee
- #379 | Huir | Se bugeo no avanza en esta parte
- #384 | Resistir | Mis aliados no pasaron quiero q puedan pasar tras el obstaculo la primera ves y despues queden atrapados en este sector con migo
- #386 | Resistir | Continuan atrapados. Solucionalo. Quiza q re apaerscan en cada escena o cada planta
- #395 | Extraer | Se trabo no avanza
### Audio / partículas / efectos
- #319 | Huir | La barra de arriba falta elimianrla tras la explosion
- #338 | Resistir | Libere a los milicianos perop ahora no me siguen y quiero un sonido de logro tras liberarlos
- #345 | Huir | AL destruir la computadora deberia destruirse el porton y la maquina ambas cosas. Osea deberia ser una gran explosion
- #372 | Huir | Los helicopteros aparecen y se van de manera rara. Quiero q no se vayan de manera tan rara. Q tengan su animacion racional medio realista su animacion y sean derribables y tengan sonido disitnot al del helicotpero final
- #385 | Resistir | El pesado deberia tener el doble de la vida q tiene ahora y su cedencia deberia ser excesiva y con particulas
- #390 | Resistir | No veo la explosion del helicotpero al impactar quiero verla cuando imapcata con el edificio

## Dolores concretos al corregir
- En 'se trabó / no avanza / snapea' el reporte no dice QUÉ lo traba: falta el collider/objeto más cercano, si hay NavMesh bajo los pies, velocidad real vs deseada, y las últimas N posiciones (¿está quieto?).
- Los bugs #394/#395 salieron con jugador vacío y posición (0,0,0): el estado no distingue 'jugador muerto / cámara libre / en RTS / en vehículo'.
- No se sabe la versión/commit del build, ni FPS/ms de frame (los bugs 'lag' no se pueden priorizar), ni resolución/calidad gráfica.
- No hay estado de resolución del bug: columnas Estado (Nuevo/En análisis/Corregido/No reproducible), Commit de la corrección, Prioridad, Tipo (clasificación), Duplicado_de, Fecha_corregido, Verificado_por.
- Muchas notas son pedidos de balance (vida/daño/velocidad) y falta ver los valores vigentes (multiplicadores, modo dios, time scale).
- Los pedidos de luces/UI/minimapa no registran la cámara activa (FPS/RTS/tanque), el zoom del minimapa ni la configuración de UI.
