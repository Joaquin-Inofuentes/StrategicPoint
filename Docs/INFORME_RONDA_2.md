# Informe · Ronda 2 — nivel 4×, coberturas tácticas, tanques enemigos y feedback total

Unity 6000.5.6f1 (URP) · escena `SC_Gameplay` · todo lo de abajo está **probado en Play** (teclado real inyectado con el Input System cuando el Editor tenía foco) y **cubierto por la suite headless: 14 fases, todas en verde**.
Las capturas están en [`Assets/Validacion/Ronda2/`](../Assets/Validacion/Ronda2/).

---

## 1. Nivel 4 veces más grande (solo *blocking* y pintura de terreno)

| | Antes | Ahora |
|---|---|---|
| Terreno | 58,4 × 160 m | **116,8 × 320 m** (2× por lado = **4× el área**) |
| Bloques | 6 | **8**, recorridos de sur a norte |
| Cubos de blocking | 34 | **103** (todos `BoxCollider` + `ObstacleMarker`) |
| Enemigos | 5 soldados | **15 soldados + 3 tanques (6 tripulantes)** |
| Pintura | pasto / tierra | pasto / tierra, caminos de 10 m, plazas, patios, manchones con ruido; alphamap 1024² |

Bloques (menú `Strategic Point ▸ Nivel ▸ Construir blockout`, idempotente):

1. **Base** — patio de partida, muros de borde, tanque propio. 
2. **Campo de tiro** — 18 coberturas bajas destructibles escalonadas + 2 ruinas.
3. **Paso del cañón** — dos muros largos, hueco de 18 m (pasa el tanque), búnkers de 700 HP y torres de vigía.
4. **Aldea** — 12 casas, calle central libre, plaza con pozo, sacos y barricadas.
5. **Puesto avanzado** — **TANQUE ENEMIGO 1** + 4 soldados, sacos de 400 HP, búnker de 600 HP.
6. **Chicane** — muros en S: el tanque tiene que girar, la infantería rodea.
7. **Fortín** — **TANQUE ENEMIGO 2** + 6 soldados, portón sur de 16 m y **brechas** (muro rojizo de 700 HP) que solo se rompen a cañonazos.
8. **Depósito final** — **TANQUE ENEMIGO 3** + 3 soldados, galpones, contenedores, muro de fondo.

Otras cosas que hubo que ajustar para que el mapa grande funcione: `Ground` y `Terrain` realineados, NavMesh re-horneado (volumen "no caminable" por cubo), grilla A* ampliada (`MaxHalfExtent` 250 → 400 m, si no el bloque 8 quedaba fuera de la grilla), alcances de combate acordes al tamaño (enemigos ven a 22 m y disparan a 13 m; aliados 20 / 12).

📷 `00_nivel_completo_cenital` · `01_play_inicio_nivel_4x` · `95_bloque_1…8_*` (vista táctica de cada bloque) · `02_editor_primer_frame_sin_play`

---

## 2. Cobertura: `T` sobre un punto de cobertura → el aliado va, se agacha y se queda

* Los puntos de cobertura son los discos celestes del piso (a 1 m de cada cara de los cubos).
* **`T`** apuntando al cubo (o a un disco): el aliado libre más cercano camina, **se agacha** y **queda ahí** hasta recibir otra orden. Dos `T` seguidos reparten al siguiente aliado a la siguiente cobertura. En RTS, `T` / clic derecho sobre una cobertura manda a **toda la selección**, un punto distinto para cada uno.
* Cualquier orden nueva (mover, atacar, seguir, subir) lo suelta y lo para de pie.
* Aunque esté en cobertura, **sigue disparando** a lo que entre en su alcance (pero no persigue: se queda).

## 3. `Shift` mantenido → holograma de la cobertura

* Al mantener **Shift** apuntando a una cobertura aparece, **estático y 90 % transparente** (alfa 0,10), **la misma geometría del aliado que la tomaría, agachado y apuntando** (clon del `Visual` del soldado con la pose del clip `idle crouching aiming`), parado en el punto exacto; el obstáculo se resalta, hay un anillo pulsante y una línea desde el aliado.
* **Shift + T** sobre la cobertura: se cubre el **aliado más cercano** (sin Shift, `T` hace lo mismo). Funciona en FPS y en RTS (holograma del primer seleccionado).

📷 `10_holograma_cobertura_shift` · `11_orden_T_cobertura_yendo` · `60_rts_shift_holograma` · `61_rts_T_cobertura_ejecutada`

## 4. Si te alejás X metros, los aliados te siguen

* GameObject **`Systems/AjustesDeEscuadra`**, script `AjustesDeEscuadra`:
  * `distanciaParaSeguir` = **25 m** — al superarla, cada aliado libre empieza a seguirte.
  * `distanciaParaDetenerse` = **8 m** — deja de seguirte al llegar.
  * `seguirDesdeCobertura` = **true** — los que están en cobertura también la dejan y te siguen.
* Se reparten en ranuras detrás tuyo para no apilarse. Solo aplica a pie y en primera persona.

## 5. Enemigos: objetivo dinámico por distancia y visión

`AiBrain.Tactica.cs`: se elige el **mejor** blanco, no solo el más cercano.
* Dentro del radio de visión se lo "siente" (como siempre); hasta **1,6×** ese radio solo si está **dentro del cono frontal (±100°) y con línea de tiro**.
* Puntaje = distancia (+12 si no hay línea de tiro, −10 si me acaba de disparar, −3 si está herido, −5 de histéresis para el blanco actual).
* En pleno combate se re-evalúa cada 0,5 s y se cambia de blanco si aparece uno claramente mejor (una orden de atacar explícita no se cambia).

## 6. Enemigos usan coberturas, se agachan y notan cuando se destruyen

* En combate el enemigo busca una **posición de tiro**: cobertura a ≤ 12 m, con línea de tiro y a una distancia del blanco entre 45 % y 90 % de su alcance (no corre a esconderse pegado a vos). Corre (sin disparar), **se agacha y dispara** desde ahí. Grita `¡A CUBIERTO!`.
* Vigila su cobertura 4 veces por segundo: si el obstáculo se derrumba (bala, explosión, barril) **se da cuenta** (`¡COBERTURA DESTRUIDA!`), las coberturas se rehacen y **busca otra**.
* Medido en Play: `Chase → Attack → corre a cobertura → agachado` en ~4 s; al destruir la cobertura, otra en 2 s.

📷 `20_enemigo_en_cobertura_agachado_vista_rts` · `21_cobertura_destruida_el_enemigo_lo_nota` · `22_enemigo_busca_otra_cobertura`

---

## 7. Tanques enemigos y utilidad del tanque propio

* 3 tanques enemigos (casco rojo oscuro, 360 HP) con **conductor + tripulante** que arrancan adentro, **patrullan** su bloque y **frenan para disparar**. Alerta roja `¡TANQUE ENEMIGO A LA VISTA!` a menos de 70 m.
* Detalle importante: la tripulación enemiga **no usa el asiento de artillero** (para `TurretAI` un artillero sentado es "humano" y la torreta queda muda).
* La torreta automática **prioriza tanques hostiles** sobre la infantería. Medido: mi tanque destruyó al enemigo (360 → 0 HP) sin recibir daño; al caer, la tripulación sale a pelear a pie.
* **Las explosiones ahora rompen obstáculos** (3× el daño de una bala, con caída hacia el borde): el cañón derriba coberturas y **brechas del fortín**. Medido: 1 obús = −135 HP a una cobertura de 300.

📷 `40_tanque_enemigo_dispara_a_la_escuadra` · `41_mi_tanque_vs_tanque_enemigo` · `42_tanque_enemigo_destruido` · `80_canon_del_tanque_rompe_cobertura`

## 8. Dentro del tanque: panel de teclas y acciones de grupo

Panel arriba a la izquierda (se actualiza cada frame; la tecla apretada **parpadea**):

| Tecla | Acción |
|---|---|
| **G** | **TODOS SUBEN** (los aliados libres, hasta llenar los asientos) |
| **I** | **TODOS BAJAN** (menos vos) |
| U | llamar a un aliado |
| T | mandar el tanque adonde apuntás (desde cualquier asiento) |
| E | bajar del tanque · Espacio: freno |
| **1 2 3 4** | conductor / cañón / metralleta / pasajero — si está **ocupado, intercambian**; el panel dice *(ir)* o *(intercambiar)* y quién está en cada asiento |

Probado con teclas reales: `G` (2 suben), `3` `2` `1` `4` (intercambios y cambios a libre), `T`, `I`.

📷 `30_tanque_panel_de_teclas` · `31_G_todos_suben` · `32_asientos_intercambio_con_aliado` · `33_I_todos_bajan`

---

## 9. Proyectiles más rápidos y con impacto en cubitos

* Velocidad base **160 → 260 m/s** (el obús sale a la mitad, 130, para que el arco siga siendo legible); la estela se estira más. Se corrigió que el prefab tenía la velocidad vieja serializada.
* **Impacto según a qué le pegás**, en cubitos de tamaños distintos (de 6 a 16 cubitos según el daño):
  * soldado → **rojos** · piso → **verdes** · obstáculo → **amarillos**.
* Bug encontrado en el camino: `DebrisPool` pintaba con el material *compartido*, así que todos los cubitos habrían salido del último color; ahora el color va por pieza.

📷 `90_impacto_soldado` · `91_impactos_rojo_verde_amarillo`

---

## 10. Feedback visual + sonoro en absolutamente toda acción

Todo pasa por `Feedback.Accion(...)`: **sonido** (3D o 2D) + **etiqueta flotante en el mundo** + **aviso arriba** + pulso en el piso + línea de log. 13 sonidos nuevos (barridos procedurales, misma paleta).

| Acción | Feedback |
|---|---|
| Ordenar cobertura / llegar a cobertura | marcador celeste, acuse de voz, `EN COBERTURA` |
| Mantener Shift sobre cobertura | tick de holograma, resaltado |
| Cobertura destruida | golpe grave + `¡COBERTURA DESTRUIDA!` |
| Aliados te siguen | `ALIADOS TE SIGUEN` (+ pulso) |
| Enemigo te detecta | `!` rojo sobre su cabeza + aviso |
| Enemigo se cubre | `¡A CUBIERTO!` |
| Tanque: subir / bajar / todos suben / todos bajan / cambio o intercambio de asiento | sonido propio + aviso + parpadeo de la tecla |
| Cambiar arma 1/2/3 | `[2] PISTOL` + sonido (además del icono) |
| Agacharse / pararse / saltar / recargar | sonido + etiqueta |
| Seleccionar en RTS / cancelar orden | sonido + `N SELECCIONADOS` / `ORDEN CANCELADA` |
| Entrar en un bloque del nivel | `BLOQUE 4 · ALDEA` (+ consejo, o alerta si hay tanque enemigo) |

Además: el aviso de modo se subió para no taparse con la etiqueta de mira.

📷 `50_arma_2_pistola_feedback` · `51_agacharse_ctrl_feedback` · `70_aviso_de_bloque_y_tanque_enemigo`

---

## Pruebas

* **Suite headless** (`Strategic Point ▸ Run All Tests Headless`): 14 fases, `TODAS LAS FASES COMPLETADAS CON EXITO`. La nueva **FASE 14** verifica: colores/cantidad de cubitos, velocidad de bala, punto de cobertura apuntado, orden → llega → agachado → se queda, cobertura destruida → lo nota y avisa, orden nueva lo para de pie, explosión daña obstáculos, holograma (visible, 90 % transparente, sin scripts ni animador, se oculta), seguir al jugador (empieza y termina por distancia), objetivo dinámico (cerca > lejos; cono y línea de tiro), `G`/`I` del tanque, panel de teclas, bando y patrulla del tanque enemigo, y el feedback central.
* Dos checks viejos se actualizaron por los cambios pedidos: velocidad del obús (ahora 130 m/s = base × 0,5) y "aliado enterado" (ahora vale Chase **o** Attack, porque con visión extendida puede pasar directo a Attack).
* **En Play**, con teclado real: Shift (holograma), Shift+T, T en RTS con el mouse sobre la cobertura, Tab, E, G, I, 1–4, Ctrl, 2 (arma). Con la API pública porque no hay forma de inyectar el mouse de otra manera: disparos de prueba, teletransportes para sacar cada bloque, destrucción forzada de una cobertura.

## Cosas para tener en cuenta

* Los aliados que te siguen atraviesan el mapa: si te teletransportás lejos (como hice para las capturas) llegan solos a los bloques con enemigos y pueden morir.
* La escena guarda la vista previa del primer frame (`Strategic Point ▸ Vista previa ▸ Preparar primer frame`) y el cartel de misión.
* `ProjectSettings.asset` lo toca solo el Editor (Adaptive Performance); no se commitea.
* La suite deja abierta `SC_TestLevel`: hay que reabrir `SC_Gameplay`.
