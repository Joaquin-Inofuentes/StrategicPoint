# Plan: bugs #396 a #414 (2026-11-08, sesión de las 20:27 en el Editor)

Fuente: `Registros/bugs.txt`, `Registros/bug_NNN_*.json` y `.png`. Estado del código: commit `cdbe52fd`, rama `main`.
Todos son del código actual (la sesión se jugó en el Editor), no de un build viejo. Salvo que se diga lo contrario, la posición del bug
es `jugador.pos` del JSON; para identificar "esto/aquí" hay que lanzar un raycast desde `jugador.camPos` con `jugador.camRot`.

Reglas de trabajo (las de todo el proyecto): cada arreglo lleva un check en `Editor/ChecksBugs065.*` con el patrón `W8Ok`/`Fin`,
nombre `BugNNN` más a lo sumo una letra, y la regresión (`Bug101a Bug602c Bug603g Bug128 Bug122 Bug123 Bug284a Bug368a…Bug398b`) tiene que seguir verde.
No editar C# mientras corre una lista de checks. Fotos con `FotoCamara`, a `Assets/Validacion~`.

## Pedidos, por tema

### A. HUD y apuntado en vehículo
| # | Pedido textual (resumido) | Qué se ve / hipótesis | Archivos probables |
|---|---------------------------|----------------------|--------------------|
| 396 | "La UI está duplicada y solapa el modo dios y la barra. Quiero que sea más larga y mejora la UI: sólo barra de vida, indicador de modo dios (variante) y barra de vida y de recarga del cañón." | En el tanque se ve la barra "TANQUE 1447/1600" con una segunda línea verde debajo que pisa el cartel "MODO DIOS · F4", más un panel "Cañón / MUNICIÓN EXPLOSIVA · LISTA / TANQUE" abajo a la derecha con su propia barra. Dos HUD de vehículo conviven. | `UI/HudDeTanqueSimple.cs`, `UI/VehicleStatusView.cs`, `Core/ModoDios.cs`, `Player/PlayerInputDriver.cs` |
| 399 | "La barra de vida de abajo no se actualiza." | Probablemente la barra duplicada de #396 (la fina) no recibe el evento de daño. Se resuelve al unificar. | los mismos |
| 397 | "Aquí snapea porque solapa contra el obstáculo, no me deja apuntar bien. Quiero que sea independiente: si hay obstáculo, apunta igual adonde apunto. Testeá con el ángulo y que el atlas muestre cómo se desplaza tras obstáculos sin limitarse." | El punto de mira/ángulo del cañón se corrige cuando un collider se interpone entre el cañón y el punto apuntado. Posición: (-141.4, 0.7, 266.6). | `UI/TurretAimView.cs`, `UI/AimUI.cs`, `Vehicles/TurretWeapon.cs`, `Vehicles/Vehicle.cs` |
| 400 | "Que la velocidad del proyectil sea el triple porque es más rápido." | Proyectiles del jugador (¿cañón del tanque o todas las armas?). Ver `ProjectilePool.Spawn(..., speedMultiplier)`. Decidir y documentar el alcance. | `Combat/Projectile.cs`, `Combat/ProjectilePool.cs`, `Vehicles/TurretWeapon.cs` |

### B. Combate enemigo
| # | Pedido | Hipótesis | Archivos |
|---|--------|-----------|----------|
| 398 | "Me falta feedback sonoro y visual cuando me impactan proyectiles enemigos. No veo los tanques enemigos: revisá. Quiero una captura donde se vean bien ambos, al menos 3 o 6 tanques enemigos, y los daños enemigos ×2." | Hoy sólo el 25 % de los autos enemigos desde el 3.º se convierte en tanque (`ProporcionDeTanquesEnemigos`), así que casi nunca hay. Feedback al recibir daño: viñeta/flash + sonido + dirección. Daño ×2: hoy sólo el tanque enemigo tiene `danoPorBala` ×2; el pedido es para los enemigos en general. | `Operacion/OperacionAuto*.cs`, `Operacion/OperacionDirector.Huida.cs`, `Combat/Health.cs`, `Presentation/*` (feedback), `Combat/DamageTakenEvent` |
| 401 | "Los rombos de los enemigos los debo ver aun detrás de los obstáculos." | Los marcadores sobre la cabeza de los enemigos se ocultan por la geometría. Hay que dibujarlos siempre encima (shader con `ZTest Always` o canvas en pantalla). | `Presentation/*Marker*`, `UI/*` |

### C. Aliados y órdenes
| # | Pedido | Hipótesis | Archivos |
|---|--------|-----------|----------|
| 406 | "Siguen trabados. Quiero que tras pasar cada nivel reaparezcan atrás mío o en un spawn cercano los aliados, y una captura que demuestre cómo lograron pasar." | Extender `RescatarAliadosDeOtroPiso`/`AyudarALaEscuadraAlHeli` a un teletransporte de cortesía a cada cambio de piso (`Nivel`), con un check y una foto. | `Operacion/OperacionDirector.Resistir.cs`, `...Compuertas.cs`, `...Mansion.cs` |
| 413 | "Quedaron atrapados, solucionalo." (posición 334.9, 20.3, 265.3, azotea) | Mismo mecanismo que 406 pero en la azotea (2 aliados atrapados). Reproducir con el JSON (`Aliados` + `Aliados_nav`). | igual |
| 414 | "Que sean sólo 2 milicianos." | Los aliados de Resistir son 3 de la escuadra + **4 prisioneros** (`Prisionero_1..4`, `MILICIA` en el HUD). Pedido: dejar 2 prisioneros/milicianos. (`CantidadDeMilicianos=2` ya existe para otra cosa: hay que unificar.) | `Operacion/OperacionDirector.*` (celdas), `Editor/OperacionBuilder.Cuartel.cs`, `MandoTactico` |
| 412 | "Las órdenes están rotas, que cada piso tenga su plano." | El menú de órdenes/mapa táctico usa un solo plano; en la mansión de 3 pisos tiene que mostrar el plano del piso en que está el jugador (y las órdenes deben apuntar a ese piso). | `UI/MenuDeOrdenes.cs`, `Player/PlayerInputDriver.Radial.cs`, `Presentation/SiluetasDeMinimapa.cs`, `Core/PisoReal.cs` |

### D. Escena: quitar, ocultar, destruir
| # | Pedido | Qué es (a confirmar con raycast) | Archivos |
|---|--------|----------------------------------|----------|
| 404 | "Quitá esa línea punteada." | Fila de cuadrados amarillos sobre la alfombra roja del salón, planta baja (330.2, 0.8, 261.1). Parece la guía del objetivo. | `Operacion/GuiaDeObjetivo.cs` (y el que la crea) |
| 407 | "Quitá esas 3 luminarias de piso." | Lámparas de pie (pie dorado, pantalla blanca) del 2.º piso (337.6, 13.8, 267.6). | `Editor/OperacionBuilder.MansionLujo2.cs`, `OperacionBuilder.PrestigioMansion.cs` o `AjustesDeNivelNov08.cs` (el patrón de "quitar" ya existe, ver #392 bandera) |
| 408 | "Quitá esto de la base que no colisiona y no aporta nada." | Plataforma/alfombra oscura elevada con borde dorado, en el 2.º piso (301.7, 13.8, 272.0). | mismos |
| 411 | "Ocultá o hacé algo con ese bloque negro para que no se vea." | Cajas negras enormes en la azotea (339.5, 18.0, 279.4), probablemente el volumen de algo (¿tanque de agua, helipuerto?) o un `BlockoutObjetivo` sin material. | `Operacion/BlockoutObjetivo.cs`, `Editor/OperacionBuilder.*` |
| 403 | "Hay partículas trabadas, solucionalas." | Sistemas de partículas que no terminan (humo/chispas en loop, `stopAction` o duración infinita). Barrer todos los `ParticleSystem` y verificar que mueren. | `Operacion/OperacionAuto.Efectos.cs`, `Presentation/*FX*` |
| 405 | "La geometría solapada se rompe (z-fighting): fijate si hay un shader o algo que lo solucione para todos." | Caras coplanares. Opciones: `Offset` en el shader/material, separar 1–2 mm en el builder, o un chequeo de auditoría que las encuentre. Evaluar y aplicar la de menor riesgo. | materiales URP, `Editor/OperacionBuilder.*` |
| 402 | "Todo aquí debería ser destruible. Testeá que todos los elementos del ambiente sean destruibles y que estén en prefabs bien armados." | Auditoría + conversión: listar los objetos del nivel que no tienen componente destruible y qué tipo son; decidir qué cuenta como "ambiente"; convertirlos o documentar los que no. Es el pedido más grande: acotarlo por categorías (mobiliario, vehículos, columnas, vallas) y por rendimiento. | `Operacion/TorreDestruible.cs`, `Presentation/ObstacleMarker.cs`, `Editor/ChecksBugs065.AuditoriaDeNivel.cs` |
| 409 | "Bug de colliders de disparos, solucionalo y testealo." | En las capturas hay marcas de impacto sobre paredes y bultos. Sospecha: disparos que se frenan en colliders que no deberían (triggers, "Sala", volúmenes) o que los atraviesan. Hace falta reproducirlo con el log de `ultimasLineas`. | `Combat/Projectile.cs`, capas de colisión |
| 410 | "Revisá qué pasó con eso de disparos." | Continuación del anterior (mismo sector, T+644 s). | igual |

## Orden propuesto de ejecución
1. **Triage y reproducción** (sin tocar código): para cada bug de D y 408/409/410, identificar el objeto con un raycast desde la cámara guardada; escribir el nombre real y la ruta en el plan.
2. **Lote 1, rápido y de bajo riesgo:** 404, 407, 408, 411 (quitar/ocultar), 414 (2 prisioneros), 400 (velocidad).
3. **Lote 2, HUD:** 396 + 399 (un solo HUD de vehículo: barra de vida larga, indicador de modo dios, barra de recarga del cañón).
4. **Lote 3, combate:** 398 (tanques visibles ≥3, daño ×2, feedback), 401 (rombos visibles), 397 (apuntado independiente del obstáculo).
5. **Lote 4, aliados:** 406 + 413 (reaparición por nivel), 412 (plano por piso).
6. **Lote 5, calidad:** 403 (partículas), 405 (z-fighting), 409 + 410 (disparos), 402 (destruible, auditoría).
7. **Cierre:** regresión completa, atlas JPG antes/después con una fila por bug (los de #397 y #398 con las fotos específicas pedidas), commit y push.

## Entregables que se pidieron explícitamente
- Atlas JPG con el desplazamiento tras obstáculos (#397).
- Captura con 3 a 6 tanques enemigos bien visibles y el feedback de impacto (#398).
- Captura que demuestre cómo pasan los aliados de piso (#406).

## Riesgos y dudas abiertas (para que las resuelva quien revise el plan)
- #400 y #398 (daño ×2): ¿se aplican a todo o sólo al tanque/vehículo? Hoy el tanque enemigo ya tiene ×2. Duplicar de nuevo cambia el balance entero.
- #402: "todo destruible" puede ser costoso (rendimiento y física). Proponer un alcance razonable.
- #409/#410: sin descripción exacta; puede ser un bug de capas o de que los proyectiles de aliados chocan con aliados.
- #412: ¿"plano" es el minimapa o la pantalla de órdenes? Confirmar mirando `MenuDeOrdenes`.
- #414: confirmar si son los prisioneros o los "milicianos" del `MandoTactico` (`CantidadDeMilicianos`).
