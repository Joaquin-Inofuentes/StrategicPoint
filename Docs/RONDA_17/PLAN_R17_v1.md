# RONDA 17 - Plan de implementacion v1 (Sonnet 5.5) - 2026-11-20

Pedido (mensajes de Joaquin del 20/11 + lista de pendientes). 15 items, agrupados en 6 frentes. El nivel se arma por codigo, asi que casi todo se
aplica con PARCHES DE RUNTIME (patron `AjustesDeNivelOpus`: `RuntimeInitializeOnLoadMethod` + watcher cada 2 s, idempotente) y componentes nuevos,
sin reconstruir escenas. Cada item deja un check en la suite (`ChecksBugs065.*`) y, si es visual, una toma en `CapturasDeBuild` (-tomas).

## A. Diagnostico por item (que hay hoy y por que falla)

| # | Pedido | Estado actual en el codigo | Causa / hueco |
|---|--------|----------------------------|---------------|
| 1 | Mas contraste para enemigos | `SoldierLook`: emision enemigo (0.30,0.06,0.04), tinte (1,0.78,0.7) sobre trimsheet oxido oscuro | De noche/sombra el rojo oxido oscuro se confunde con el fondo; el brillo es tenue |
| 11 | Contraste del enemigo escudo | `EnemigoEscudero`: uniforme grafito (0.16), casco plata, escudo acero claro (0.66,0.72,0.8) con emision casi nula | Grafito sobre asfalto oscuro: silueta perdida; el escudo no lleva el codigo rojo de bando |
| 2/15 | Enemigos que vienen a atacar despues de recibir ataque / al recibir dano | `AiBrain.OnAnyDamage`: solo si estaba Idle/Patrol/MovingToOrder/Follow pasa a Chase y solo si el atacante esta vivo y a <=alertRadius para los vecinos. `CazaDeEnemigos` convierte guardias en cazadores si reciben dano, pero solo los marcados | Enemigos `Quieto`/`Pasivo`/en cobertura/pesado estacionado ignoran el golpe; los vecinos del grupo no se alertan fuera de cazadores; un tiro de francotirador lejano no genera busqueda |
| 14 | Trigger para empezar a atacar al tanque | `AnilloDeSubida` y `TanqueJefe`/`OperacionAuto` existen; no hay zona que lance el asalto contra el tanque del jugador | Falta un disparador espacial: el jugador se sube/aproxima y los enemigos del sector no cambian de blanco al vehiculo |
| 5 | Gordo (pesado) en etapas: 1.er headshot rompe la mascara, 2.o mata | `Projectile`: `instakill = headshot && ... ` y `Health.TakeDamage(..., headshot)` -> `amount = Current` | El headshot mata siempre; el pesado no tiene mascara ni estado intermedio |
| 8 | Iterar el enemigo grande | `EnemigoPesado`: 3 fases (Patrulla/Estacionando/Frenesi), vida x12, sin telegrafo, sin punto debil visible | Falta lectura: ni telegrafo de carga ni feedback al romper la mascara ni fase "furia" |
| 6 | Bloquear la PC hasta matar a todos los enemigos | `OperacionTerminal.Habilitado` ya existe (bool); `OperacionDirector.Centro` solo la usa por fase | La PC del centro de datos y la de las celdas se pueden usar con enemigos vivos |
| 3/10 | Decoracion del ambiente de servidores + vestir el exterior | `OperacionBuilder.Servidores` arma racks, bandejas, ductos; `AjustesDeNivelNov08` viste puertas | Interior repetitivo, sin vida (LEDs, cables, cartelera); fachada exterior lisa |
| 4 | Particulas para fuente | `FuentesDeAgua` solo aplica shader `SP/AguaDeFuente` a las mallas "Fuente*Agua/Chorro" | No hay `ParticleSystem`: sin salpicadura, niebla ni gotas |
| 12 | Animacion de eliminacion de enemigos | `CaidaDeCuerpos` (solo corrige cuerpos flotando), `KillCylinderFx`, `RetiroDeEnemigosCaidos` | La muerte es la del Animator + retirada seca; sin reaccion direccional, polvo ni disolucion |
| 13 | Feedback al entrar al tanque: circulos en el piso | `AnilloDeSubida` (2 aros) solo en la subfase Abordaje de HUIR | No hay anillo de abordaje cuando el tanque es subible en otras fases |
| 7 | Nivel 1 terminado | Nivel 1 = Mision (`SC_Gameplay`, `MisionDirector`, victoria/derrota, resultado) | Sin criterio escrito de "terminado": hay que fijarlo (flujo completo intro->victoria->resultado) y verificarlo de punta a punta |
| 9 | Tutorial simple | `TutorialManager` 1904 lineas, `CatalogoDelTutorial` (22 pasos nuevos en R10, autoplayer) | Demasiado largo para un primer contacto |

## B. Plan de implementacion (orden y archivos)

Fase 1 - Combate y IA (mayor riesgo, primero)
- `Operacion/ReaccionAlDano.cs` (nuevo): suscribe `DamageTakenEvent`; si la victima es enemiga y el atacante es del bando jugador: la saca de `Quieto/Pasivo`, `target = atacante`, estado Chase, la marca cazadora (`CazaDeEnemigos.Marcar`) y alerta al grupo a `RadioDeGrupo` (16 m) con un retraso de 0,2-0,6 s al azar (reaccion escalonada). Si el atacante no es visible (francotirador) manda a 2-3 vecinos al ultimo punto conocido (`ultimoImpacto`).
- `Operacion/EnemigoPesado.cs`: etapas de cabeza -> `Mascara` (Transform hijo del hueso Head, 3 piezas), `EtapaDeCabeza {Enmascarado, Descubierto}`; metodo `AbsorbeHeadshot(out bool rompio)` que `Projectile` consulta antes del instakill. Primer headshot: dano x2 normal (no instakill), la mascara sale despedida (Rigidbody + chispas + sonido metal), el pesado entra en "furia" (x1,25 velocidad de giro y cadencia, ojos/visera emisivos). Segundo headshot: instakill normal.
- `Combat/Projectile.cs`: una sola linea de consulta (`ExentoDeInstakill`).
- Iteracion del grande (item 8): telegrafo de 0,6 s antes del frenesi (laser rojo + parpadeo del casco), temblor de camara corto en cada paso pesado cerca del jugador, barra de vida propia con "ruptura de armadura".
- Tanque: `Operacion/DisparadorContraTanque.cs` - zonas esfericas ancladas a cada sector de enemigos; cuando el tanque del jugador (o el jugador a pie cerca del tanque) entra, los guardias del sector pasan a cazar con el tanque como `target` de vehiculo (misma via que `EmboscadaDeCohetes`/`OperacionAuto`). Un solo disparo por zona.

Fase 2 - Nivel: PC bloqueada, tutorial, Nivel 1
- `OperacionTerminal`: `BloqueadaHastaLimpiar` (Func<int> enemigosVivos); mientras > 0 `Habilitado=false`, luz roja, texto "BLOQUEADA - quedan N enemigos", contador en el HUD; al llegar a 0 destello verde + sonido. Se aplica a la PC del centro de datos (todos los enemigos del sector) y a la de las celdas.
- `Tutorial`: modo SIMPLE por defecto (6 pasos: mover, apuntar+disparar, cobertura, cambiar de soldado, subir al tanque, objetivo) reutilizando los pasos existentes; el completo queda en el menu.
- Nivel 1: checklist de "terminado" (flujo intro -> objetivos -> victoria -> resultado sin excepciones, 0 enemigos bloqueados, audio y HUD) corrido con la suite + `-capturas`.

Fase 3 - Arte y feedback
- `Presentation/AjustesDeNivelNov20.cs` (nuevo, patron AjustesDeNivelOpus): (a) decoracion interior de `9_Servidores`: LEDs que parpadean en los racks, cables de piso con cinta, bandejas, cartelera "SALA DE SERVIDORES", extractor, luz de emergencia; (b) vestir exterior: condensadores de aire, tuberias, marquesina de la puerta, franjas de advertencia, reflector, vallas bajas.
- `Presentation/FuentesDeAgua.cs`: `ParticleSystem` por fuente (chorro, salpicadura, niebla) con material URP particle aditivo.
- `Presentation/AnimacionDeBajaEnemiga.cs`: al morir un enemigo: empujon en la direccion del ultimo golpe, caida con giro, nube de polvo, y a los ~6 s disolucion (encoje + baja al piso) antes del retiro. Respeta `RetiroDeEnemigosCaidos`.
- `Operacion/AnilloDeSubida.cs`: modo generico `AnilloDeAbordaje` para el tanque propio libre: anillo que late, se cierra al acercarse y se vuelve verde en el radio de uso.
- `Actors/SoldierLook.cs`: mas emision/contraste del enemigo comun (contorno), escudero: emision en marco y franja del escudo, uniforme mas claro.

## C. Verificacion
Cada item: 1 check en la suite (Edit mode) + 1 toma ANTES/DESPUES en `CapturasDeBuild`; atlas JPG final. Build limpia al final.
