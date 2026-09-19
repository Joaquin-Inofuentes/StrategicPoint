# Tutorial paso a paso (36 pasos, con capturas)

Recorrido completo del tutorial (`SC_Tutorial`) tal como quedó en la Ronda 8 (36 pasos; el 12 es nuevo). Cada fase tiene: qué hay que hacer, qué se aprende, cómo está implementado y capturas reales del juego en Play (teclas y mouse reales inyectados; los pasos que no cambiaron se adelantaron con `SaltarPaso` solo para llegar a los nuevos).

## Comandos del radial [Q] y dónde se practica cada uno

| Categoría | Opciones | Paso donde se usan |
|---|---|---|
| IR ALLÍ | todos / solo uno | 19 |
| CUBRIRSE | todos / solo uno | 21 |
| ATACAR (dorado) | todos / solo uno | 19 |
| POSICIÓN | quietos, sígueme, línea, cuña, retirada | 15 y 20 |
| CURAR (dorado) | curarme, al más herido, curar a este, revivir | 22, 23 y 24 |
| TANQUE | subir todos, bajar todos, tanque allí, subirme yo, bajarme yo | 29, 30, 33/34 y 35 |
| POSEER (dorado) | soldado 1/2/3, siguiente, este | 5 |
| DEMOLER (dorado) | asalto demuele, yo demuelo, cancelar | 25 y 26 |
| TORRETA FIJA (dorado) | usar, salir | 27 |

Los pasos 27, 29 y 35 desactivan el atajo [E] a propósito (avisa «USA EL RADIAL») para que el comando de [Q] se use de verdad y no se reemplace por la tecla vieja.

## Ayudas para no trabarse (Ronda 9)

Además de las **pistas** de cada paso (aparecen a los 16 s sin progreso), el tutorial ahora tiene:

| Tecla | Qué hace |
|---|---|
| **F8** | Salta el paso actual (lo da por cumplido) y trae a los aliados que hayan quedado a más de 14 m. |
| **F7** | Si cerraste el tutorial a medias, al volver aparece «PASO N GUARDADO»; con F7 (en el paso 1, durante 25 s) se avanza solo hasta ese paso. |
| Flecha de texto | A los 16 s sin progreso, en los pasos con blanco concreto (disparar, mira, arsenal, cuchillo, granada, ir y atacar, suministros) la línea de ayuda dice hacia dónde girar: `OBJETIVO a tu DERECHA >> · 12 m`. |

El progreso se guarda (`PlayerPrefs`, clave `sp_tutorial_paso`) al empezar cada paso y se borra al terminar el tutorial. Si un enemigo de práctica no tiene línea de vista libre a la distancia pedida, `PuntoConVista` lo acerca (70 %, 50 %, 35 %) en vez de dejarlo detrás de un muro.
La etiqueta «CENTRO» de la baliza de misión se vuelve transparente cuando cae sobre la mira, para no tapar el blanco.

## Paso 1 · MOVER LA CÁMARA

- **Tecla(s):** Mouse
- **Qué hacer:** Girá el mouse a los costados y hacia arriba/abajo.
- **Qué se aprende / feedback:** Se acumulan 50° de giro horizontal y 18° de vertical. Cada casilla se tilda sola.
- **Cómo está hecho:** Nada especial: solo se mide cuánto giró la cámara.

![Paso 1: el cuadro, la tecla MOUSE iluminada y las dos casillas.](../Assets/Validacion/Ronda7/tut_01_camara.png)
*Paso 1: el cuadro, la tecla MOUSE iluminada y las dos casillas.*

## Paso 2 · MOVERSE CON W A S D

- **Tecla(s):** W A S D
- **Qué hacer:** Apretá cada una de las cuatro teclas.
- **Qué se aprende / feedback:** Cada tecla se ilumina en el cuadro al apretarla y la lista de las que faltan baja.
- **Cómo está hecho:** Las teclas se leen del teclado real cada frame.

![Paso 2: las cuatro teclas del cuadro; se encienden al apretarlas.](../Assets/Validacion/Ronda7/tut_02_wasd.png)
*Paso 2: las cuatro teclas del cuadro; se encienden al apretarlas.*

## Paso 3 · CORRER CON SHIFT

- **Tecla(s):** Shift + W
- **Qué hacer:** Mantené Shift caminando 1,2 s y soltalo.
- **Qué se aprende / feedback:** Correr = 1,7× la velocidad, piernas aceleradas; los aliados que te siguen corren con vos.
- **Cómo está hecho:** SoldierMotor.Corriendo + animación acelerada (SoldierAnimatorDriver).

![Paso 3.](../Assets/Validacion/Ronda7/tut_03_correr.png)
*Paso 3.*

![Corriendo: el soldado inclinado y el cuerpo acelerado.](../Assets/Validacion/Ronda7/tut_03b_correr_en_accion.png)
*Corriendo: el soldado inclinado y el cuerpo acelerado.*

## Paso 4 · DISPARAR

- **Tecla(s):** Clic izq. / R
- **Qué hacer:** Derribá al enemigo rojo, disparale a la pared gris y rompé la caja amarilla.
- **Qué se aprende / feedback:** Tres blancos con balizas 3D de colores: enemigo (rojo), pared (no se rompe) y caja (destructible).
- **Cómo está hecho:** TutorialBeacon marca cada blanco; los hooks de ShotFired/ObstacleMarker validan.

![Paso 4: las tres balizas.](../Assets/Validacion/Ronda7/tut_04_disparar.png)
*Paso 4: las tres balizas.*

## Paso 5 · CAMBIAR DE SOLDADO (RADIAL)

- **Tecla(s):** Q
- **Qué hacer:** Mirá a un aliado, mantené Q, subí a POSEER (dorado) y salí hasta POSEER A ESTE.
- **Qué se aprende / feedback:** Aprendés el radial: siempre IR ALLÍ, CUBRIRSE y POSICIÓN; lo dorado arriba aparece solo según lo apuntado.
- **Cómo está hecho:** La opción pedida late en celeste (PonerPista). El radial ahora suena al abrirse, al pasar por cada opción y al confirmar.

![Paso 5.](../Assets/Validacion/Ronda7/tut_05_cambiar.png)
*Paso 5.*

![El radial abierto apuntando a un aliado.](../Assets/Validacion/Ronda7/tut_05b_radial_apuntando_aliado.png)
*El radial abierto apuntando a un aliado.*

![POSEER en dorado y la opción POSEER A ESTE elegida.](../Assets/Validacion/Ronda7/tut_05c_radial_poseer_a_este.png)
*POSEER en dorado y la opción POSEER A ESTE elegida.*

## Paso 6 · AGACHARSE

- **Tecla(s):** Ctrl
- **Qué hacer:** Mantené Ctrl y soltalo.
- **Qué se aprende / feedback:** Agachado: más preciso y menos visible.
- **Cómo está hecho:** SoldierMotor.IsCrouching.

![Paso 6.](../Assets/Validacion/Ronda7/tut_06_agacharse.png)
*Paso 6.*

![Soldado agachado.](../Assets/Validacion/Ronda7/tut_06b_agachado.png)
*Soldado agachado.*

## Paso 7 · SALTAR (nuevo)

- **Tecla(s):** Espacio
- **Qué hacer:** Apretá Espacio y esperá a aterrizar.
- **Qué se aprende / feedback:** Suena el salto y el golpe al caer; la cámara se sacude. Las piernas se recogen en el aire y el cuerpo ya NO se hunde en el piso.
- **Cómo está hecho:** Clips de salto horneados con la cadera a la altura de pie (Fase 1 del documento de la Ronda 7).

![Paso 7.](../Assets/Validacion/Ronda7/tut_07_saltar.png)
*Paso 7.*

![En el aire, cámara lenta: cuerpo entero sobre el piso.](../Assets/Validacion/Ronda7/tut_07b_saltando.png)
*En el aire, cámara lenta: cuerpo entero sobre el piso.*

## Paso 8 · APUNTAR: MIRA EN PRIMERA PERSONA

- **Tecla(s):** Clic der. + clic izq.
- **Qué hacer:** Mantené el clic derecho, disparando con el izquierdo, y soltá.
- **Qué se aprende / feedback:** La cámara pasa de encima del hombro a los ojos del soldado, con zoom y respiración.
- **Cómo está hecho:** CameraRig.AdsBlend.

![Paso 8.](../Assets/Validacion/Ronda7/tut_08_mira.png)
*Paso 8.*

![Mirando por los ojos: retícula propia del arma.](../Assets/Validacion/Ronda7/tut_08b_mira_ojos_del_soldado.png)
*Mirando por los ojos: retícula propia del arma.*

## Paso 9 · ARMAS: MIRA Y SONIDO PROPIOS (nuevo)

- **Tecla(s):** 1 2 3 R clic der.
- **Qué hacer:** Cambiá de arma, gastá una bala y recargá con R, y apuntá con la otra arma.
- **Qué se aprende / feedback:** Cada arma tiene su retícula, su disparo, su recarga y su desenfunde propios.
- **Cómo está hecho:** WeaponHolder.SonarDesenfunde / StartReload; GenericSfx.GetWeaponReload/Draw.

![Paso 9.](../Assets/Validacion/Ronda7/tut_09_arsenal.png)
*Paso 9.*

![La pistola con su mira de cadera propia.](../Assets/Validacion/Ronda7/tut_09b_arma_2_cadera.png)
*La pistola con su mira de cadera propia.*

![Recargando: cartel y sonido propio.](../Assets/Validacion/Ronda7/tut_09c_recargando.png)
*Recargando: cartel y sonido propio.*

![Mira con zoom de la otra arma.](../Assets/Validacion/Ronda7/tut_09d_mira_de_la_otra_arma.png)
*Mira con zoom de la otra arma.*

## Paso 10 · CUCHILLO (nuevo)

- **Tecla(s):** F
- **Qué hacer:** Dale un tajo al aire, acercate al enemigo (≤ 2 m) y apuñalalo hasta derribarlo.
- **Qué se aprende / feedback:** Arco brillante del cuchillo y silbido; si conecta: golpe sordo, chispa roja, etiqueta «¡CUCHILLADA!» y sacudida.
- **Cómo está hecho:** WeaponHolder.TryMelee + CuchilloFx. El enemigo de práctica aparece en un lugar con línea de vista libre.

![Paso 10: el enemigo marcado.](../Assets/Validacion/Ronda7/tut_10_cuchillo.png)
*Paso 10: el enemigo marcado.*

![El tajo en cámara lenta.](../Assets/Validacion/Ronda7/tut_10b_tajo.png)
*El tajo en cámara lenta.*

![La cuchillada que conecta.](../Assets/Validacion/Ronda7/tut_10c_cuchillada_conecta.png)
*La cuchillada que conecta.*

## Paso 11 · GRANADA (nuevo)

- **Tecla(s):** G
- **Qué hacer:** Mantené G (curva y radio), soltala sobre el enemigo y esperá la explosión.
- **Qué se aprende / feedback:** La curva EXACTA con cuentas, el anillo del radio real (4,5 m) donde cae, tic-tac del fusible que se acelera y estruendo.
- **Cómo está hecho:** Granada.Simular es la misma simulación que usa la granada real.

![Paso 11.](../Assets/Validacion/Ronda7/tut_11_granada.png)
*Paso 11.*

![Curva, anillo de explosión y retícula roja sobre el enemigo.](../Assets/Validacion/Ronda7/tut_11b_curva_y_radio.png)
*Curva, anillo de explosión y retícula roja sobre el enemigo.*

![Explosión.](../Assets/Validacion/Ronda7/tut_11c_explosion.png)
*Explosión.*

## Paso 12 · CAJA DE SUMINISTROS (nuevo, Ronda 8)

- **Tecla(s):** W A S D
- **Qué hacer:** Ya gastaste 2 granadas y perdiste vida: camina hasta la caja verde con cruz blanca que aparece delante.
- **Qué se aprende / feedback:** Repone las 3 granadas y cura el 60 % de la vida, suena el «curado» y sale el aviso «SUMINISTROS +2 GRANADAS +VIDA». La caja se vuelve a llenar a los 45 s y flota y gira para verse de lejos.
- **Cómo está hecho:** CajaDeSuministros: se posa en el piso (raycast + CheckBox), se recoge por cercanía (2,2 m) con el soldado que manejas; en la misión hay 3 repartidas entre la salida y la plaza.

![Aparece el paso con la caja delante y la baliza SUMINISTROS.](../Assets/Validacion/Ronda7/../Ronda8/t_suministros_inicio.png)
*Aparece el paso con la caja delante y la baliza SUMINISTROS.*

![Caja frente a la mira (granadas x1, vida 108/180).](../Assets/Validacion/Ronda7/../Ronda8/t_suministros_caja_frente.png)
*Caja frente a la mira (granadas x1, vida 108/180).*

![Recogida: granadas x3 y vida completa.](../Assets/Validacion/Ronda7/../Ronda8/t_suministros_recogida.png)
*Recogida: granadas x3 y vida completa.*

## Paso 13 · VER COBERTURAS Y RUTAS

- **Tecla(s):** C
- **Qué hacer:** Mantené C y soltalo.
- **Qué se aprende / feedback:** Los discos celestes de cobertura y las rutas de patrulla solo aparecen mientras la mantenés.
- **Cómo está hecho:** Coberturas.MostrarMarcas.

![Paso 12.](../Assets/Validacion/Ronda7/tut_12_vista_tactica.png)
*Paso 12.*

![Coberturas visibles con C apretada.](../Assets/Validacion/Ronda7/tut_12b_coberturas_con_C.png)
*Coberturas visibles con C apretada.*

## Paso 14 · RTS: ORDEN A LOS 2

- **Tecla(s):** Tab, arrastrar, clic der.
- **Qué hacer:** Tab; seleccioná a los 2 aliados; clic derecho en el círculo verde.
- **Qué se aprende / feedback:** Vista táctica con zoom hacia el cursor.
- **Cómo está hecho:** Ver Ronda 5 y 6.

![Paso 13.](../Assets/Validacion/Ronda7/tut_13_rts.png)
*Paso 13.*

![La vista RTS desde arriba.](../Assets/Validacion/Ronda7/tut_13b_vista_rts.png)
*La vista RTS desde arriba.*

## Paso 15 · VOLVER A PRIMERA PERSONA

- **Tecla(s):** Tab
- **Qué hacer:** Tab otra vez y un clic para capturar el mouse.
- **Qué se aprende / feedback:** TAB nunca deja estados colgados (probado con 36 acciones de estrés en la Ronda 6).
- **Cómo está hecho:** CameraRig.SetMode.

![Paso 14.](../Assets/Validacion/Ronda7/tut_14_fps.png)
*Paso 14.*

## Paso 16 · SÍGANME Y QUIETOS (RADIAL)

- **Tecla(s):** Q
- **Qué hacer:** Q → POSICIÓN → SÍGANME; luego TODOS QUIETOS.
- **Qué se aprende / feedback:** Los aliados vienen (ícono verde) y luego se plantan.
- **Cómo está hecho:** OrderService.IssueFollowOrder / Quietos.

![Paso 15.](../Assets/Validacion/Ronda7/tut_15_seguir.png)
*Paso 15.*

## Paso 17 · SELECCIONAR EN FPS

- **Tecla(s):** Shift + clic der.
- **Qué hacer:** Shift + clic derecho sobre un aliado.
- **Qué se aprende / feedback:** Selección sin salir de primera persona.

![Paso 16.](../Assets/Validacion/Ronda7/tut_16_seleccionar.png)
*Paso 16.*

## Paso 18 · ORDEN DE MOVER EN FPS

- **Tecla(s):** Clic der.
- **Qué hacer:** Clic derecho en el suelo con el aliado seleccionado.
- **Qué se aprende / feedback:** El aliado camina al punto.

![Paso 17.](../Assets/Validacion/Ronda7/tut_17_mover_fps.png)
*Paso 17.*

## Paso 19 · IR ALLÍ Y ATACAR (RADIAL) (nuevo)

- **Tecla(s):** Q
- **Qué hacer:** Q → IR ALLÍ → TODOS mirando al piso; luego mirá al enemigo y Q → ATACAR → TODOS.
- **Qué se aprende / feedback:** Los aliados caminan al punto y, recién con la orden de atacar, abren fuego (antes están en alto el fuego).
- **Cómo está hecho:** AlOrdenRadial pone la postura en Libre solo al recibir ATACAR.

![Paso 18.](../Assets/Validacion/Ronda7/tut_18_ir_atacar.png)
*Paso 18.*

![Radial sobre IR ALLÍ.](../Assets/Validacion/Ronda7/tut_18b_radial_ir_alli.png)
*Radial sobre IR ALLÍ.*

![ATACAR aparece en dorado al apuntar al enemigo.](../Assets/Validacion/Ronda7/tut_18c_radial_atacar_dorado.png)
*ATACAR aparece en dorado al apuntar al enemigo.*

![Los aliados disparan.](../Assets/Validacion/Ronda7/tut_18d_aliados_disparan.png)
*Los aliados disparan.*

## Paso 20 · FORMACIONES Y RETIRADA (RADIAL) (nuevo)

- **Tecla(s):** Q
- **Qué hacer:** Q → POSICIÓN → FORMAR LÍNEA, FORMAR CUÑA y RETIRADA.
- **Qué se aprende / feedback:** Se usan los tres comandos que faltaban de POSICIÓN.
- **Cómo está hecho:** Un sub-paso por comando, marcados por OrdenRadialEjecutada.

![Paso 19.](../Assets/Validacion/Ronda7/tut_19_formaciones.png)
*Paso 19.*

![Eligiendo FORMAR CUÑA.](../Assets/Validacion/Ronda7/tut_19b_radial_formar_cuna.png)
*Eligiendo FORMAR CUÑA.*

## Paso 21 · CUBRIRSE HACIA DONDE MIRO (RADIAL)

- **Tecla(s):** Q
- **Qué hacer:** Mirá los sacos, Q → CUBRIRSE → TODOS.
- **Qué se aprende / feedback:** Los discos de cobertura se ven mientras el radial está en CUBRIRSE.

![Paso 20.](../Assets/Validacion/Ronda7/tut_20_cubrirse.png)
*Paso 20.*

![Radial con los discos celestes visibles.](../Assets/Validacion/Ronda7/tut_20b_radial_cubrirse_con_discos.png)
*Radial con los discos celestes visibles.*

![Aliados a cubierto.](../Assets/Validacion/Ronda7/tut_20c_aliados_en_cobertura.png)
*Aliados a cubierto.*

## Paso 22 · CURAR A UN ALIADO (RADIAL)

- **Tecla(s):** Q
- **Qué hacer:** Apuntá al herido, Q → CURAR → CURAR A ESTE.
- **Qué se aprende / feedback:** El médico va, suena el «ya voy», sale un «+12» verde por segundo y al terminar suena la campanita. La regeneración automática se apaga en este paso para que el médico tenga algo que hacer.
- **Cómo está hecho:** Health.RegeneracionPermitida = false; PedidoDeCuracion con Feedback.

![Paso 21: aliado herido.](../Assets/Validacion/Ronda7/tut_21_curar.png)
*Paso 21: aliado herido.*

![CURAR aparece solo porque el aliado está herido y hay médico.](../Assets/Validacion/Ronda7/tut_21b_radial_curar_a_este.png)
*CURAR aparece solo porque el aliado está herido y hay médico.*

![El médico curando.](../Assets/Validacion/Ronda7/tut_21c_medico_curando.png)
*El médico curando.*

## Paso 23 · REANIMAR A UN CAÍDO (RADIAL)

- **Tecla(s):** Q
- **Qué hacer:** Apuntá al caído, Q → CURAR → REVIVIR A ESTE.
- **Qué se aprende / feedback:** El médico corre, se queda 4 s y suena la descarga + campanitas al volver a la vida.
- **Cómo está hecho:** PedidoDeCuracion.SolicitarReanimar.

![Paso 22.](../Assets/Validacion/Ronda7/tut_22_reanimar.png)
*Paso 22.*

![REVIVIR A ESTE.](../Assets/Validacion/Ronda7/tut_22b_radial_revivir_a_este.png)
*REVIVIR A ESTE.*

![Reanimando.](../Assets/Validacion/Ronda7/tut_22c_medico_reanimando.png)
*Reanimando.*

## Paso 24 · CURARME (RADIAL) (nuevo)

- **Tecla(s):** Q
- **Qué hacer:** Con vida baja (< 70%): Q → CURAR → CURARME.
- **Qué se aprende / feedback:** CURAR aparece solo, sin apuntar a nadie; el médico viene por vos.
- **Cómo está hecho:** Sin regeneración automática hasta que llega el médico.

![Paso 23: barra de vida baja.](../Assets/Validacion/Ronda7/tut_23_curarme.png)
*Paso 23: barra de vida baja.*

![CURARME.](../Assets/Validacion/Ronda7/tut_23b_radial_curarme.png)
*CURARME.*

![El médico te cura.](../Assets/Validacion/Ronda7/tut_23c_medico_te_cura.png)
*El médico te cura.*

## Paso 25 · DEMOLER UN MURO (ASALTO)

- **Tecla(s):** Q, Ctrl
- **Qué hacer:** Apuntá al muro, Q → DEMOLER → YO DEMUELO y mantené Ctrl quieto 4 s.
- **Qué se aprende / feedback:** Anillo de progreso, pitidos de la carga que se aceleran y estallido.
- **Cómo está hecho:** DemoledorAsalto con TicDeCarga (SfxKind.BombTick) y BombPlant.

![Paso 24.](../Assets/Validacion/Ronda7/tut_24_demoler.png)
*Paso 24.*

![DEMOLER → YO DEMUELO.](../Assets/Validacion/Ronda7/tut_24b_radial_demoler_yo_demuelo.png)
*DEMOLER → YO DEMUELO.*

![Plantando la carga.](../Assets/Validacion/Ronda7/tut_24c_plantando_carga.png)
*Plantando la carga.*

![Muro demolido.](../Assets/Validacion/Ronda7/tut_24d_muro_demolido.png)
*Muro demolido.*

## Paso 26 · EL ASALTO ALIADO PONE LA BOMBA (RADIAL) (nuevo)

- **Tecla(s):** Q
- **Qué hacer:** Con otro soldado: Q → DEMOLER → ASALTO DEMUELE, luego CANCELAR mientras va, y otra vez ASALTO DEMUELE.
- **Qué se aprende / feedback:** Se usan las tres opciones de DEMOLER. El muro está a 24 m y la carga dura 9 s en este paso para dar tiempo a cancelar.
- **Cómo está hecho:** Demolicion.Segundos ajustable; si el muro vuela antes de cancelar se crea otro.

![Paso 25.](../Assets/Validacion/Ronda7/tut_25_bomba_aliado.png)
*Paso 25.*

![ASALTO DEMUELE.](../Assets/Validacion/Ronda7/tut_25b_radial_asalto_demuele.png)
*ASALTO DEMUELE.*

![El asalto corre al muro.](../Assets/Validacion/Ronda7/tut_25c_asalto_va_al_muro.png)
*El asalto corre al muro.*

![CANCELAR (con el 75% de progreso mostrado).](../Assets/Validacion/Ronda7/tut_25d_radial_cancelar.png)
*CANCELAR (con el 75% de progreso mostrado).*

![Plantando otra vez.](../Assets/Validacion/Ronda7/tut_25e_asalto_plantando.png)
*Plantando otra vez.*

![El muro vuela.](../Assets/Validacion/Ronda7/tut_25f_muro_volado.png)
*El muro vuela.*

## Paso 27 · AMETRALLADORA FIJA (radial)

- **Tecla(s):** Q
- **Qué hacer:** Q → TORRETA FIJA → USAR, disparar y Q → TORRETA FIJA → SALIR.
- **Qué se aprende / feedback:** En este paso [E] está desactivado y avisa «USA EL RADIAL»: hay que practicar el comando.
- **Cómo está hecho:** PlayerInputDriver.SoloRadial.

![Paso 26.](../Assets/Validacion/Ronda7/tut_26_torreta_fija.png)
*Paso 26.*

![[E] bloqueado en este paso.](../Assets/Validacion/Ronda7/tut_26b_E_bloqueado.png)
*[E] bloqueado en este paso.*

![USAR LA TORRETA en dorado.](../Assets/Validacion/Ronda7/tut_26c_radial_torreta_usar.png)
*USAR LA TORRETA en dorado.*

![Disparando la ametralladora.](../Assets/Validacion/Ronda7/tut_26d_disparando_torreta.png)
*Disparando la ametralladora.*

![SALIR DE LA TORRETA.](../Assets/Validacion/Ronda7/tut_26f_radial_salir.png)
*SALIR DE LA TORRETA.*

## Paso 28 · MODO DIOS

- **Tecla(s):** F4
- **Qué hacer:** F4 y F4 otra vez.
- **Qué se aprende / feedback:** Cartel dorado abajo; nadie de tu bando recibe daño.
- **Cómo está hecho:** ModoDios.

![Paso 27.](../Assets/Validacion/Ronda7/tut_27_modo_dios.png)
*Paso 27.*

![Modo dios activo.](../Assets/Validacion/Ronda7/tut_27b_modo_dios_activo.png)
*Modo dios activo.*

## Paso 29 · ENTRAR AL TANQUE (RADIAL)

- **Tecla(s):** Q
- **Qué hacer:** Apuntá al tanque: Q → TANQUE → SUBIRME YO.
- **Qué se aprende / feedback:** [E] desactivado en este paso para practicar el radial.
- **Cómo está hecho:** SoloRadial.

![Paso 28.](../Assets/Validacion/Ronda7/tut_28_entrar_tanque.png)
*Paso 28.*

![[E] bloqueado.](../Assets/Validacion/Ronda7/tut_28b_E_bloqueado.png)
*[E] bloqueado.*

![SUBIRME YO.](../Assets/Validacion/Ronda7/tut_28c_radial_tanque_subirme_yo.png)
*SUBIRME YO.*

## Paso 30 · ALIADOS AL TANQUE (RADIAL)

- **Tecla(s):** Q
- **Qué hacer:** Q → TANQUE → SUBIR TODOS.
- **Qué se aprende / feedback:** Los aliados corren y se sientan.

![Paso 29.](../Assets/Validacion/Ronda7/tut_29_aliados_tanque.png)
*Paso 29.*

![SUBIR TODOS.](../Assets/Validacion/Ronda7/tut_29b_radial_subir_todos.png)
*SUBIR TODOS.*

## Paso 31 · CAMBIAR A LA TORRETA

- **Tecla(s):** 2
- **Qué hacer:** Apretá 2.
- **Qué se aprende / feedback:** Pasás al cañón (intercambio con el ocupante si hace falta).

![Paso 30.](../Assets/Validacion/Ronda7/tut_30_torreta.png)
*Paso 30.*

## Paso 32 · MIRA DEL CAÑÓN Y LA METRALLETA

- **Tecla(s):** Clic der.
- **Qué hacer:** Clic derecho en el cañón [2]; [3] y clic derecho en la metralleta; soltá y volvé al [2].
- **Qué se aprende / feedback:** Mira de óptica con zoom.

![Paso 31.](../Assets/Validacion/Ronda7/tut_31_mira_tanque.png)
*Paso 31.*

![Mira del cañón.](../Assets/Validacion/Ronda7/tut_31b_mira_del_canon.png)
*Mira del cañón.*

![Mira de la metralleta.](../Assets/Validacion/Ronda7/tut_31c_mira_de_la_metralleta.png)
*Mira de la metralleta.*

## Paso 33 · AVANZAR Y DISPARAR

- **Tecla(s):** Q, clic izq.
- **Qué hacer:** Q → TANQUE → TANQUE ALLÍ y derribá 3 enemigos.
- **Qué se aprende / feedback:** Se usa TANQUE ALLÍ.

![Paso 32.](../Assets/Validacion/Ronda7/tut_32_avanzar_disparar.png)
*Paso 32.*

## Paso 34 · LLEGAR AL FINAL

- **Tecla(s):** Q
- **Qué hacer:** Mandá el tanque a la meta dorada.
- **Qué se aprende / feedback:** Segunda tanda de enemigos opcional.

![Paso 33.](../Assets/Validacion/Ronda7/tut_33_final.png)
*Paso 33.*

## Paso 35 · BAJAR DEL TANQUE (RADIAL) (nuevo)

- **Tecla(s):** Q
- **Qué hacer:** Q → TANQUE → BAJAR TODOS y luego BAJARME YO.
- **Qué se aprende / feedback:** Se completan los cinco comandos de TANQUE. Si no hay aliados a bordo, BAJAR TODOS se da por hecho para no trabar.
- **Cómo está hecho:** SoloRadial.

![Paso 34.](../Assets/Validacion/Ronda7/tut_34_bajar_tanque.png)
*Paso 34.*

![BAJAR TODOS.](../Assets/Validacion/Ronda7/tut_34b_radial_bajar_todos.png)
*BAJAR TODOS.*

![BAJARME YO.](../Assets/Validacion/Ronda7/tut_34c_radial_bajarme_yo.png)
*BAJARME YO.*

## Paso 36 · ¡TUTORIAL COMPLETADO!

- **Tecla(s):** -
- **Qué hacer:** Nada: victoria.
- **Qué se aprende / feedback:** Resumen de todo lo aprendido.
- **Cómo está hecho:** VictoriaTutorial.

![Pantalla de victoria.](../Assets/Validacion/Ronda7/tut_35_victoria.png)
*Pantalla de victoria.*
