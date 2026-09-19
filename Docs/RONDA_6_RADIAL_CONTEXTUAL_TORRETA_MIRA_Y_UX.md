# Ronda 6 — Radial contextual, ametralladora fija, mira en primera persona, modo dios, correr y mejoras de UX

Cada cambio de esta ronda está descrito con **qué se hizo, en qué archivo y cómo se probó**, con capturas en `Assets/Validacion/Ronda6/` (35 imágenes). Todo se probó en Play Mode con teclas y mouse reales (Input System + `SetCursorPos/mouse_event` del SO para el RTS). La suite headless quedó en **verde con una fase nueva (FASE 15)**. Nada de esto está commiteado ni pusheado.

| # | Pedido | Estado |
|---|---|---|
| 1 | Radial simplificado y **contextual** (solo ofrece lo que se puede hacer con lo apuntado, en dorado y arriba) | Hecho y probado |
| 2 | Curar solo si apuntás a un aliado herido **o caído** y el médico vive (+ reanimar) | Hecho y probado |
| 3 | Demoler / subir al tanque / usar torreta: solo si se apunta al objeto | Hecho y probado |
| 4 | **Ametralladora fija** interactuable (usé el asset `P_Env_Emplazamiento_MG` de las torres y bunkers) | Hecho y probado |
| 5 | Coberturas y rutas de patrulla **solo mientras se mantiene una tecla** (`C`) o el radial está en CUBRIRSE | Hecho y probado |
| 6 | RTS: órdenes de mover que "no se tomaban" y UI que se rompía | Causas encontradas y corregidas |
| 7 | **F4 = modo dios** (nadie de tu bando recibe daño) | Hecho y probado |
| 8 | Mira más realista; **al mantener el clic derecho pasa a primera persona** (a pie y en el tanque: cañón y metralleta) | Hecho y probado |
| 9 | **Shift = correr**, también los aliados, con la animación acelerada | Hecho y probado |
| 10 | TAB FPS↔RTS, tanque y torreta: intentar romperlo | 36 acciones de estrés, 2 fallas reales halladas y corregidas |
| 11 | Tutorial actualizado con todo (26 pasos, más feedback) | Hecho y recorrido de punta a punta |
| 12 | Mejoras de UX generales (HUD, textos, controles, roster) | Ver §10 |

---

## 1. Radial simplificado y contextual

**Antes:** 8 categorías fijas (IR ALLÍ, CUBRIRSE, ATACAR, POSICIÓN, CURAR, TANQUE, POSEER, DEMOLER) siempre visibles, aunque no sirvieran para nada en ese momento.

**Ahora:** el anillo interior solo tiene lo que vale **en este instante**:

* **Siempre:** `IR ALLÍ`, `CUBRIRSE (hacia donde miro)`, `POSICIÓN`.
* **Solo si apuntás a algo** (contextual → va **arriba** y **en dorado** con la marca `★ AQUÍ`):

| Apuntás a… | Aparece | Opciones que salen |
|---|---|---|
| un enemigo | ATACAR | todos / solo 1 / 2 / 3 |
| un aliado **herido** (y hay un médico vivo) | CURAR | `CURAR A ESTE` |
| un aliado **caído** (y hay un médico vivo) | CURAR | `REVIVIR A ESTE` |
| un aliado (sano o no) | POSEER | `POSEER A ESTE` |
| un muro destructible (y hay un soldado de Asalto disponible) | DEMOLER | `ASALTO DEMUELE` / `YO DEMUELO` / `CANCELAR` (solo las que aplican) |
| el tanque aliado | TANQUE | `SUBIRME YO` (si no voy dentro) / `SUBIR TODOS` (si hay lugar) / `BAJAR TODOS` (si hay ocupantes) / `TANQUE ALLÍ` (si tiene conductor) |
| una ametralladora fija libre | TORRETA FIJA | `USAR LA TORRETA` |
| — (estado propio) yo herido a menos del 70 % y hay médico o botiquín | CURAR | `CURARME` |
| — voy sentado en el tanque | TANQUE | `BAJARME YO` (+ las que apliquen) |
| — estoy en una torreta | TORRETA FIJA | `SALIR DE LA TORRETA` |

Si no hay médico vivo, CURAR **no aparece** (no tiene sentido pedir algo que nadie puede hacer). Si el muro es demolible pero no hay Asalto, no aparece DEMOLER y el cartel de la mira lo dice ("hace falta un soldado de ASALTO").

**Cómo está hecho**
* `UI/MenuDeOrdenes.cs` se reescribió: nuevo `ContextoRadial` (qué categorías/opciones son visibles y cuáles contextuales), la distribución del anillo se recalcula al abrirlo (`Distribuir`: `360°/N` por porción, las contextuales centradas en las 12), las opciones del abanico se filtran (`OpcionesVisibles`) y `Seleccion`/`Sub` siguen devolviendo los **ids reales** de categoría/opción, así que `EjecutarOrdenRadial(cat, sub)` (y los tests) no cambiaron de significado. Los números 1..N eligen la porción N visible (`CategoriaDeTecla`).
* `Player/PlayerInputDriver.cs`: `ConstruirContextoRadial(AimResult)` decide todo lo de la tabla; `AbrirRadial` lo pasa a `Abrir(ctx)`.
* `Player/AimTargeting.cs`: dos tipos nuevos, `Torreta` y `Caido`. Los caídos **no tienen collider** (se apaga al morir), así que se los apunta por cercanía al rayo (`CaidoBajoRayo`).
* **El cartel de la mira dice qué ofrece el radial** para lo que apuntás y se pone dorado si es una acción contextual (`AimUI.PonerPromptContextual`): `[Q] CURAR a Soldado_2_Kes (87/207)`, `[Q] DEMOLER este muro (carga de 4 s)`, `[E] USAR LA AMETRALLADORA FIJA`, `[Q] REANIMAR a …`.

| | |
|---|---|
| ![](../Assets/Validacion/Ronda6/05_radial_base_3_opciones.png) | ![](../Assets/Validacion/Ronda6/06_radial_contextual_caido.png) |
| Sin apuntar a nada: solo 3 porciones (IR ALLÍ, CUBRIRSE, POSICIÓN). | Apuntando a un aliado caído: aparece CURAR arriba, en dorado. |
| ![](../Assets/Validacion/Ronda6/21_radial_curar_a_este.png) | ![](../Assets/Validacion/Ronda6/07_radial_opcion_reanimar.png) |
| Aliado herido: CURAR y POSEER contextuales; el abanico solo trae `CURAR A ESTE`. | Caído: el abanico solo trae `REVIVIR A ESTE`. |

Prompts de la mira: [caído](../Assets/Validacion/Ronda6/08_prompt_reanimar.png) · [herido](../Assets/Validacion/Ronda6/20_prompt_curar_herido.png)

**Pruebas:** FASE 15 de la suite (`ConstruirContextoRadial` con suelo / aliado herido / aliado sano / enemigo / tanque / caído / torreta, y el orden y filtrado del menú) + Play real abriendo el radial con `Q` y eligiendo con deltas de mouse reales.

---

## 2. Curar y reanimar (solo con médico vivo)

* `PedidoDeCuracion.MedicoDisponible()` busca un médico aliado vivo. **CURAR** solo se ofrece si existe.
* **Reanimar** es nuevo: `PedidoDeCuracion.SolicitarReanimar(caido, medicoManual?)`. El médico camina hasta el caído (`IssueMoveOrder`; un caído no se "sigue", el seguimiento se corta al morir el objetivo), se queda a ≤ 2,5 m **4 s** y lo levanta (`Health.Initialize` + `Motor.ResetMotionState`). Mientras reanima queda `Brain.Pasivo = true` para que no se distraiga peleando (sin eso se iba a pelear y el caído quedaba tirado: falló así en la primera prueba y se corrigió).
* **Bug de UX encontrado y corregido:** la fila del **roster** se quedaba en "CAÍDO" para siempre aunque el soldado ya estuviera de pie (revivir no publica ningún evento). `UI/RosterRowView.cs` ahora se reactiva sola.

| | |
|---|---|
| ![](../Assets/Validacion/Ronda6/23_roster_caido.png) | ![](../Assets/Validacion/Ronda6/24_roster_reanimado.png) |
| Roster con el aliado caído. | Después de que el médico lo reanima (vida 207/207). |

---

## 3. Ametralladora fija (torreta fija)

**Assets:** el mapa ya tenía 9 emplazamientos `P_Env_Emplazamiento_MG` (techos de las torres de vigía y bunkers) **solo como decorado**. Ahora son jugables.

**Cómo se usa:** apuntale (cartel dorado `[E] USAR LA AMETRALLADORA FIJA`) y `E`, o `Q` → TORRETA FIJA → USAR, o simplemente parate junto a ella (a ≤ 4,5 m en el plano) y `E`. Si está lejos, el radial hace que camines solo hasta ella. Adentro:
* el soldado sube al emplazamiento (aunque sea el techo de una torre) y queda **plantado detrás del arma**; no camina ni salta;
* gira solo dentro de un **arco de ±70°** (`TorretaFija.AcotarGiro`);
* dispara una **cinta de 100 balas** (daño 14, cadencia 0,085 s, recarga 3,2 s) con `Click`; con `Click derecho` mantenido mira por la mira;
* `E` (o `Q` → SALIR DE LA TORRETA) para salir: **recupera su arma de siempre y baja a donde estaba parado**.
* Se libera sola si el soldado muere, es sacado del puesto por una orden (RTS), sube al tanque o el jugador posee a otro.

**Implementación:** `Vehicles/TorretaFija.cs` (nuevo). `InstalarEnEscena()` (llamado desde `GameplaySceneBootstrap`) agrega el componente a cada `P_Env_Emplazamiento_MG` de primer nivel bajo `ArteBloque` y le pone un `BoxCollider` **trigger** del tamaño del modelo (para poder apuntarlo sin estorbar al caminar). `WeaponHolder.ConfigurarCargador()` (nuevo) da la cinta sin tocar el catálogo. El driver (`UsarTorreta`, `SalirDeTorreta`, `ActualizarTorretaFija`) bloquea WASD/salto y acota el giro.

| | |
|---|---|
| ![](../Assets/Validacion/Ronda6/02_torreta_apuntada.png) | ![](../Assets/Validacion/Ronda6/03_en_torreta_techo.png) |
| Apuntando a la ametralladora de un bunker: la mira y el cartel se ponen dorados. | Ya arriba de la torre: `Metralleta 100/100`, cartel "MODO DIOS" abajo (era para la prueba). |
| ![](../Assets/Validacion/Ronda6/04_torreta_disparando.png) | ![](../Assets/Validacion/Ronda6/33_tutorial_torreta_disparando.png) |
| Disparando (trazadoras); el giro ya está topado a 70° a un costado. | En el tutorial (paso 18): torreta de práctica y sub-pasos tildados. |

**Límites honestos:** el modelo del emplazamiento es una malla estática, **no rota** con la puntería (solo el tirador y las balas); los **aliados no usan** torretas (solo el jugador).

---

## 4. Coberturas y rutas: solo cuando se piden

**Antes:** los cilindros celestes de cobertura y las esferas/rutas amarillas de patrulla enemiga estaban **siempre** en pantalla; el holograma de cobertura pedía `Shift`.

**Ahora:**
* `Coberturas.MostrarMarcas(bool)` (`Core/Coberturas.cs`): la raíz de marcas se crea **oculta**. `CameraRig.MostrarRutas(bool)` quita/pone la capa de rutas del `cullingMask` (antes se forzaba siempre incluida).
* Se ven **mientras se mantiene `C`** (`KeyBindings.VerTactico`) o **mientras el radial está resaltando CUBRIRSE** (que era justo el pedido: al elegir cubrirse, que se vean las coberturas, para todos / solo 2 / solo 3). Funciona en FPS y en RTS.
* El holograma del aliado que tomaría la cobertura usa la misma condición (ya no `Shift`, que ahora es correr).

| | |
|---|---|
| ![](../Assets/Validacion/Ronda6/14_sin_coberturas.png) | ![](../Assets/Validacion/Ronda6/15_con_coberturas_C.png) |
| Mapa limpio (RTS) sin tocar nada. | Manteniendo `C`: puntos celestes de cobertura y la ruta de patrulla enemiga (línea roja). |

En el tutorial: [radial sobre CUBRIRSE con las coberturas visibles](../Assets/Validacion/Ronda6/31_tutorial_cubrirse_coberturas_visibles.png).

---

## 5. RTS: "a veces no toma la orden de mover" / "se rompe la UI"

Las causas concretas que encontré leyendo `UpdateRts` y probando con el mouse real:

1. **La orden de mover exigía que el cursor cayera justo sobre el PISO.** Con el cursor sobre un aliado, un caído, una torreta, un techo o un muro, no pasaba nada y **no avisaba**. Ahora el destino es el **piso bajo el cursor** (`AimTargeting.PisoBajoRayo`), salvo enemigos y vehículos (que tienen su orden propia). Probado: clic derecho real sobre un aliado → `order=(10.25, 0, 30.17) state=MovingToOrder`.
2. **Cualquier gráfico con "raycast" (un aviso, un panel decorativo) bloqueaba selección y órdenes** en toda la pantalla (`EventSystem.IsPointerOverGameObject`). Ahora solo bloquean los elementos **interactuables** (`Selectable`) y el minimapa (`PunteroSobreUiInteractiva`).
3. **Sin selección no pasaba nada**: ahora avisa `NADIE SELECCIONADO: ARRASTRA UN CUADRO O CLICK SOBRE UN ALIADO`.
4. Con las torretas nuevas (trigger sobre las torres) el caso 1 se habría vuelto más frecuente; quedó cubierto por el arreglo.

**Zoom hacia el cursor (pendiente de la ronda anterior) verificado con rueda real:** el punto del piso bajo el cursor no se mueve al hacer zoom (altura 30 → 12, punto `(12.04, 0, 33.96)` antes y después). Velocidades de paneo y zoom ×2 (ronda 5) intactas.

| | |
|---|---|
| ![](../Assets/Validacion/Ronda6/10_rts_antes_del_zoom.png) | ![](../Assets/Validacion/Ronda6/11_rts_despues_del_zoom.png) |
| RTS, cursor sobre un punto del piso. | Dos giros de rueda: la vista se acercó y ese punto sigue bajo el cursor. |

---

## 6. F4 = modo dios

* `Core/ModoDios.cs` (nuevo): `Activo`, `Alternar()`, `Protege(Health)`. **Nadie de tu bando** recibe daño: el soldado que manejás, tu escuadra, el civil y el tanque propio. Los enemigos siguen igual.
* Enganches: `Health.TakeDamage` y `Vehicle.TakeDamage` (una línea cada uno).
* Feedback: `UI/ModoDiosView.cs` (cartel dorado que late, abajo al centro: `★ MODO DIOS · [F4] apagar`), aviso al activar/apagar y sonido. Se apaga solo al reiniciar/salir de la partida (estado estático reseteado en `SubsystemRegistration`).
* Probado en la suite (aliado sin daño, enemigo con daño, tanque sin daño, vuelve al apagar) y en Play (la muestra de "MODO DIOS" aparece en casi todas las capturas de esta ronda porque se usó para poder probar sin morir).

---

## 7. Mira más realista y primera persona al apuntar

**A pie**
* Antes: el clic derecho solo angostaba el FOV **sin salir de la vista por encima del hombro** (a 4 m detrás).
* Ahora: mantener el clic derecho **desliza la cámara al ojo del soldado** (`CameraRig.AdsBlend`, `FollowOverShoulder(..., ojo)`), oculta el cuerpo **pero conserva el arma** (`Soldier.SetBodyVisible(false, conservarArma: true)`), aplica el zoom real del arma y dibuja su retícula (`MirillaView`, una por arma). Al soltar vuelve al hombro.
* **Respiración:** al apuntar la mira **no está quieta** (`CameraRig.AplicarRespiracion`, ruido suave que crece con el aumento, se reduce agachado y aumenta caminando). Mueve la cámara de verdad, así que también mueve el tiro.
* Al pasar a RTS el "apuntado" se reinicia (`SetMode`).

**En el tanque (cañón y metralleta)**
* Con el clic derecho la cámara pasa a la **mira del cañón** (periscopio sobre el techo de la torreta, retícula telescópica con tubo negro y zoom 3,5×) o a la **mira de la metralleta** (desde atrás y arriba del arma, retícula de anillo, zoom 2,2×). Sin apuntar sigue la tercera persona. El panel de teclas se esconde mientras se mira. (`UpdateVehicleCameraAimed`, `FollowThirdPersonAimed(miraPos, miraRot)`.)
* Se probaron **con y sin zoom** los dos puestos, y en el tutorial (paso 23) con teclas reales `2`, `3` y clic derecho.

| | |
|---|---|
| ![](../Assets/Validacion/Ronda6/00_normal_sobre_el_hombro.png) | ![](../Assets/Validacion/Ronda6/01_apuntando_primera_persona.png) |
| Sin apuntar: cámara sobre el hombro. | Clic derecho mantenido: ojos del soldado, zoom y retícula del fusil. |
| ![](../Assets/Validacion/Ronda6/16_tanque_tercera_persona.png) | ![](../Assets/Validacion/Ronda6/17_tanque_mira_canon.png) |
| Artillero del cañón, tercera persona. | Con el clic derecho: mira del cañón (óptica telescópica). |
| ![](../Assets/Validacion/Ronda6/18_tanque_mira_metralleta.png) | ![](../Assets/Validacion/Ronda6/36_tutorial_mira_canon.png) |
| Mira de la metralleta del tanque. | La misma mira del cañón, ya en el tutorial. |

---

## 8. Shift = correr (y los aliados también)

* `Actors/SoldierMotor.cs`: `SetRunning`, `Corriendo`, `FactorDeCarrera = 1.7` (5 → 8,5 m/s). No se corre agachado ni en el aire. `MoveSpeed` ya devuelve la velocidad efectiva, así que la IA que acota el paso a lo que falta la respeta.
* `Presentation/SoldierAnimatorDriver.cs`: el ciclo de piernas llegaba a su tope caminando; ahora `animator.speed` sube hasta **×1,7** en proporción a lo que se corre de más.
* Jugador: `Shift` + hacia adelante, de pie y sin apuntar. **Aliados libres** (siguen / cumplen una orden de mover) corren mientras el jugador corre (`AjustesDeEscuadra.Correr`, `AiBrain.ActualizarCarrera`); en combate o quietos caminan normal.
* Medido en Play: caminar `speed=5`, correr `speed=8,5 animSpeed=1,7`; aliado siguiendo: `corriendo=True speed=8,5 animSpd=1,7`.

| | |
|---|---|
| ![](../Assets/Validacion/Ronda6/12_correr.png) | ![](../Assets/Validacion/Ronda6/13_aliados_corren.png) |
| Corriendo (el cartel dorado de la mira dice que ese muro se puede demoler). | Los aliados que te siguen también corren. |

---

## 9. TAB, tanque y torreta: intentar romperlo

Prueba de estrés con invariantes comprobados tras cada acción (poseído, asiento coherente, torreta coherente, cámara sin NaN…): **36 acciones** entre TAB, apuntar con clic derecho, entrar/salir del tanque (incluido subir+bajar+subir en un solo frame), cambiar de asiento, usar/salir de la torreta, posesión de otro soldado estando en la torreta, ordenar a toda la escuadra desde RTS estando adentro del tanque, y morir dentro de la torreta. Log completo: [`Docs/Ronda6_TAB_Caos_Log.txt`](Ronda6_TAB_Caos_Log.txt).

Hallazgos reales, corregidos y re-probados:
1. **Con el clic derecho apretado, TAB a RTS dejaba el "apuntado" colgado (ads=1)** al volver → `CameraRig.SetMode` lo reinicia.
2. **Morir dentro de la torreta** dejaba al driver creyendo que seguía en ella (el estado se limpiaba solo en `UpdateFps`, que no corre durante la cámara de muerte) → se limpia en `OnEntityDied`.
3. (Ya en el punto 2) el roster que no se levantaba tras reanimar.

Verificado sin cambios: ordenar a todos desde RTS con el jugador sentado en el tanque no lo baja ni lo "maneja solo" (arreglo de la ronda 5 intacto). [Captura](../Assets/Validacion/Ronda6/19_tab_tras_orden_rts.png).

---

## 10. Mejoras de UX (además de lo pedido)

* **HUD de misión** (`Mision/MisionHud.cs`): ya no se monta sobre la barra de estado superior; ahora es un panel compacto de 340 px arriba a la izquierda con texto que se ajusta.
* **Panel de teclas del tanque** movido debajo del de misión (se superponían) y oculto al mirar por la mira.
* **Tutorial:** el cuadro ahora **calcula su alto según el mensaje** (antes la instrucción tenía 3 renglones fijos y un texto largo pisaba las casillas) y los **títulos largos se achican** en vez de partirse en dos (`Tutorial/TutorialUI.cs`).
* **Tabla de controles (`H`)** (`UI/ControlsTable.cs`) reescrita: sacadas las teclas heredadas (T, G, F, U, I, F1-3…) que ya no existen; agregadas Shift, C, F4, mira, torreta, radial contextual.
* **Textos y avisos** actualizados: consejos de zona ("Mantené [C] para ver las coberturas"), instrucciones FPS/RTS, "[G] TODOS SUBEN" → "[Q] RADIAL > TANQUE > SUBIR TODOS".
* Se avisa cuando lo apuntado **no** se puede usar ("Muro destructible: hace falta un soldado de ASALTO", "…está caído (no queda médico)").

---

## 11. Tutorial actualizado (26 pasos)

Detalle en [`Docs/TUTORIAL.md`](TUTORIAL.md). Resumen de lo nuevo:
* Pasos nuevos: **CORRER**, **VER COBERTURAS Y RUTAS**, **REANIMAR**, **AMETRALLADORA FIJA**, **MODO DIOS**, **MIRA DEL CAÑÓN Y LA METRALLETA**.
* Pasos cambiados: MIRA en primera persona (4 sub-pasos), CAMBIAR DE SOLDADO / CURAR / ENTRAR AL TANQUE (ahora **apuntando**, contextual), CUBRIRSE (con las coberturas a la vista).
* **Más feedback:** la opción del radial que hay que elegir **late en celeste con `▶ ELEGÍ ESTA`** (`MenuDeOrdenes.PonerPista`), cada sub-paso cumplido suena, se tilda y flashea, balizas sobre los objetivos (la de la torreta se retira al ocuparla), y pistas a los 16 s.
* Recorrido de punta a punta con gestos reales en los pasos nuevos/cambiados (los pasos que no cambiaron —cámara, WASD, disparar, agacharse, RTS básico— se dieron por cumplidos con `SaltarPaso` porque ya estaban validados en las rondas anteriores).

| | |
|---|---|
| ![](../Assets/Validacion/Ronda6/34_tutorial_poseer_radial.png) | ![](../Assets/Validacion/Ronda6/30_tutorial_mira_primera_persona.png) |
| Paso 5: el radial contextual con POSEER en dorado y la pista celeste latiendo. | Paso 7: la mira en primera persona; el título largo ya cabe. |
| ![](../Assets/Validacion/Ronda6/39_tutorial_reanimar.png) | ![](../Assets/Validacion/Ronda6/40_tutorial_demoler.png) |
| Paso 16: reanimar al caído. | Paso 17: demoler (carga de 4 s, agachado y quieto). |
| ![](../Assets/Validacion/Ronda6/38_tutorial_modo_dios.png) | ![](../Assets/Validacion/Ronda6/35_tutorial_tanque_radial.png) |
| Paso 19: F4. | Paso 20: apuntando al tanque el radial ofrece TANQUE (`SUBIRME YO`, `SUBIR TODOS`). |

---

## 12. Pruebas automáticas y resultado

* **Suite headless (`Strategic Point > Run All Tests Headless`): TODAS LAS FASES COMPLETADAS CON ÉXITO.**
  * 3 pruebas viejas dejaron de cumplirse **por los cambios pedidos** y se actualizaron: marcas de cobertura (ahora ocultas por defecto y visibles al pedirlas), cartel de ataque (`[F]` → `[Q] radial`), panel de teclas del tanque (`[G]/[I]` → radial).
  * **FASE 15 nueva** (37 comprobaciones): teclas nuevas, tabla de controles, modo dios (aliado, enemigo, tanque, apagado), correr (velocidad, agachado, soltar), contexto del radial (suelo, herido, sano, enemigo, tanque, caído, torreta), orden/filtrado del menú, reanimar con médico, torreta fija (ocupar, cinta, arco, salir, recuperar arma, `MasCercana`), caído sin collider apuntable y coberturas ocultas.
* Errores de compilación: 0.
* Nota: la suite reconstruye y guarda `SC_TestLevel.unity` (diff grande esperado) y regenera IDs de `P_Vehicle_Blindado.prefab`; ese ruido del prefab se revirtió.

---

## 13. Cosas que conviene saber

* **No se commiteó ni pusheó nada** (no lo pediste).
* Los **aliados no usan la ametralladora fija** ni corren en combate; y la malla de la ametralladora no rota (solo el tirador).
* Los tests de "aliado herido" en Play dependen de la regeneración de vida (12/s tras 3 s sin daño) y del médico automático: para capturas hubo que desactivar `PedidoDeCuracion.AtencionAutomatica`.
* `C` reemplaza al viejo "poseer al más cercano" (que ya estaba apagado); la vista táctica se puede remapear desde `KeyBindings` como el resto.
* Los servidores MCP de GitHub, Sentry y Supabase requieren autorización desde la configuración de conectores de claude.ai; no se usaron en esta ronda.

## Archivos tocados

Nuevos: `Core/ModoDios.cs`, `Vehicles/TorretaFija.cs`, `UI/ModoDiosView.cs`.
Modificados: `UI/MenuDeOrdenes.cs`, `Player/PlayerInputDriver.cs`, `Player/AimTargeting.cs`, `Player/PedidoDeCuracion.cs`, `Player/KeyBindings.cs`, `Player/Demolicion.cs`, `Camera/CameraRig.cs`, `Actors/SoldierMotor.cs`, `Actors/Soldier.cs`, `Ai/AiBrain.cs`, `Ai/AjustesDeEscuadra.cs`, `Presentation/SoldierAnimatorDriver.cs`, `Presentation/GameplaySceneBootstrap.cs`, `Presentation/AnuncioDeZonas.cs`, `Core/Coberturas.cs`, `Combat/Health.cs`, `Combat/WeaponHolder.cs`, `Vehicles/Vehicle.cs`, `Mision/MisionHud.cs`, `UI/AimUI.cs`, `UI/ControlsTable.cs`, `UI/RosterRowView.cs`, `UI/VehicleKeysPanel.cs`, `Tutorial/TutorialManager.cs`, `Tutorial/TutorialFlags.cs`, `Tutorial/TutorialUI.cs`, `Editor/HeadlessTestRunner.cs`.
