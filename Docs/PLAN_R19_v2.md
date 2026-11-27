# Plan R19 — v2 (revisión de arquitectura sobre el borrador v1)

Fecha: 2026-11-27. Base: `Docs/PLAN_R19_v1.md`. Rutas relativas a `Assets/_Project/Scripts/`. Todo lo marcado **[VERIFICADO]** se leyó en el código con archivo y línea; **[A VERIFICAR]** es una hipótesis razonada que la tarea debe confirmar antes de tocar. Los valores numéricos son los de arranque; la pasada 2 los puede ajustar con las capturas.

Pedido textual (9 puntos): Nivel 1 terminado · Iterar en enemigo grande · tutorial simple · Vestir exterior servidores · Contraste enemigo escudo · Animacion de eliminacion de enemigos · Feedback entrar al tanque circulos piso · Trigger para empezar a atacar al tanque · Ataque de enemigos cuando reciben dano.

Entrega: todo implementado, build Windows64, atlas JPG antes/después, 3 pasadas de validación. Commit/push/RAR SOLO si el usuario lo ordena.

---

## 0. El cambio de enfoque respecto al v1

**Los 9 puntos no son nuevos: son los pedidos 7 a 15 de la Ronda 17, palabra por palabra** (`Docs/RONDA_17/PLAN_R17_v1.md`, tabla A). Todos se implementaron en los commits `eab45789` / `e75cc673` con decisiones por defecto que el dueño nunca confirmó (`Docs/RONDA_17/VALIDACION_R17.md`, líneas 5-9), y hay una build posterior (`Builds/Windows64`, 24/11). Los pedidos 1 a 6 de aquella lista ya no vienen; los 7 a 15 vuelven tal cual.

Consecuencia: el v1 diagnostica cada punto como si faltara ("no hay zona/trigger", "el anillo solo se usa en Abordaje", "sin giro"), y varias de sus tareas duplican o contradicen lo que ya existe. La pregunta correcta para cada punto es: **¿por qué, con la R17 puesta, el dueño lo sigue pidiendo?** La respuesta que da el código es casi siempre la misma: la mecánica existe pero **no se percibe** (umbral invisible, anillo con el radio equivocado, reacción sin señal, retiro del cadáver a los 14 s que nadie mira) o su default fue discutible. Por eso este plan:

1. Parte del estado real post-R17 y entrega, por punto, el **delta perceptible**.
2. Hace medible cada resultado (métrica + check `Bug994x`) y lo fotografía antes/después.
3. Agrega una **guía de 1 página para el dueño** ("dónde mirar cada uno de los 9 en 5 minutos"), porque un cambio que no se encuentra en la build se vuelve a pedir.

---

## 1. Hechos verificados que corrigen o completan al v1

| # | Lo que dice u omite el v1 | Lo que hay en el código | Impacto |
|---|---|---|---|
| H1 | T8: "AiBrain.Vehiculos depende solo de visión; no hay zona/trigger" | **Falso.** La R17 ya puso el disparador de la pelea con el BLINDADO: umbral `ZParaLaToma = -30`, daño al blindado, o salvaguarda de 45 s (`Operacion/OperacionDirector.Jefe.cs:33, 203-224`). Pero el umbral es **un plano invisible a 10 m de la salida de la sala (muro en z = -40) y a 52 m del tanque (z = 22)** (`Editor/OperacionBuilder.Huida.cs:31-54`). La franja `R17_UmbralDelPatio` del plan R17 nunca se hizo (grep: 0 resultados). Para el jugador la toma "arranca sola al salir". | T8 se rediseña: mover y hacer visible el umbral, no crear otro sistema. |
| H2 | T7: "mostrar el anillo con el radio exacto de interacción (3,5 m)" | **El radio real de abordaje en la Operación es 6,5 m** en planta (`OperacionDirector.Huida.cs:29-32`, `Embarcar()` por `[E]`), pero el aro de uso se dibuja a `PlayerInputDriver.RadioDeInteraccion` = 3,5 m en 3D (`Operacion/AnilloDeSubida.cs:62, 165-171`). Entre 3,5 y 6,5 m el HUD dice "[E] SUBIR" y el círculo dice "todavía afuera". El check `#990d` midió a 14 m y a 3 m; nunca a 5 m. | Es el defecto de feedback más probable detrás del pedido 7. |
| H3 | T7: "anillo siempre que el jugador esté a <= N m de un vehículo" y "círculo extra por asiento" | `AnilloDeSubida` es **singleton** y `OperacionDirector.TickAnilloDeSubida` lo **oculta cada cuadro** si no es Huir/Abordaje (`OperacionDirector.cs:503-513`). No existen posiciones de asiento para abordar (se aborda por radio). | Un segundo emisor pelearía cuadro a cuadro. Hace falta un árbitro único. Los círculos por asiento se descartan. |
| H4 | (omitido) | **Bug real:** `TickCirculoDeBase` llama `AnilloDeSubida.Mostrar(plaza, 4.2f)` (`OperacionDirector.Resistir.cs:318`), que resuelve a la sobrecarga del tanque: cartel 3D **"[E] SUBIR AL TANQUE" en la plaza** y radio de uso 3,5 m en vez de `RadioDeInteraccionDeBase` = 5. La sobrecarga correcta existe y nadie la usa. | Se corrige dentro de T7 (1 línea). |
| H5 | (omitido) | `AnilloDeSubida.Ocultar()` dispara el destello verde + sonido de asiento si el jugador está adentro, **sea cual sea el motivo** del apagado (cambio de fase, fin de Resistir). | `Ocultar(bool subio)`. |
| H6 | T4: capa de Editor nueva `OperacionBuilder.FachadaCentro.cs` (`ArteOp_Fachada`) | La fachada **ya existe como parche de runtime**: `Presentation/AjustesDeNivelR17.cs:276-361` (`R17_Servidores_Fachada`: 6 condensadores E/O en z -76..-68, conductos, bandejas, cartel, franjas, generador). Las escenas son binarias y el pipeline tiene pasos de fusión; abrir/guardar `SC_Operacion` con el Editor sucio cuelga (memoria del proyecto). | Se extiende el parche de runtime (mismo patrón, misma raíz), no se toca la escena. |
| H7 | T4: "check: ninguna pieza en un corredor de IA" | Faltan tres restricciones reales: (a) `SalaDespejadaEnRts` oculta en vista RTS todo renderer bajo `3_CentroDeDatos` con `bounds.min.y >= 4,5` (o >= 2,8 si el nombre empieza con `Bandeja/Cable/Ducto/Baliza/Viga/LineaLuz/...`) y al volver le pone `ShadowCastingMode.On` aunque fuera `Off` (`Presentation/SalaDespejadaEnRts.cs:39-52, 76`); (b) `#501b` cuenta como "flotante" todo adorno que quede sin apoyo al demoler un destructible: ya lista 2 conductos de la R17 que nacen arriba de un condensador destructible; (c) los patios laterales son zonas de aparición (x ±46..52, z -70..-50) y la entrada sur es el camino de la oleada 1. | Reglas de nombrado, de apoyo y de zonas libres en T4. |
| H8 | T3: "ayuda a los 10 s en vez de 16 s" | `Editor/HeadlessTestRunner.Fase19.cs:33` exige `SegundosParaAyuda == 16`. Además la pista usa el literal `16f` (`Tutorial/TutorialManager.cs:1735`). | Constante nueva para el modo simple; la de 16 no se toca. |
| H9 | T3: "Total >= mínimo ajustado (Fases13a17.cs:327)" | El check hardcodea `ModoSimple ? 11 : 36`. El progreso se guarda **por índice** en `sp_tutorial_paso_simple` (`TutorialManager.cs:1679, 1817`): si la lista cambia, [F7] retoma en un paso equivocado. `Fase19.cs:426` falla si una clave de `Core/LocTextos.cs` deja de aparecer literal en runtime. El autoplayer resuelve por `Id` (`Tutorial/TutorialAutoPlayer.cs:163-208`). | Constante `PasosSimples`, clave de progreso nueva, textos viejos intactos, gestos nuevos por Id. |
| H10 | T3: "de 11 a ~8 pasos" (sin decir cuál es el problema) | El modo simple actual **obliga a usar el radial** en 3 de 11 pasos; `entrar_tanque` pone `driver.SoloRadial = true` y desactiva `[E]` (`TutorialManager.cs:962-982`), cuando la Operación enseña `[E]`. El simple termina en `mira_tanque` **sin disparar el cañón ni ver un enemigo**, y ese paso tiene un mensaje de ~420 caracteres (`:1023`). | La simplificación es de gestos y de texto, no solo de cantidad. |
| H11 | T5: "paleta azul-negro con franjas amarillas" | El **azul es el código de bando aliado** (`Actors/SoldierLook.cs:148` `TinteAliado` (0.30,0.62,1.00); asalto aliado azul vivo + **dorado** (0.94,0.72,0.18), `:301-307`). El pesado es violeta/magenta (`:310-320`). El escudero hoy es gris acero + plata + correaje amarillo (`:322-328`) con placa de acero claro (`Operacion/EnemigoEscudero.cs:129`): gris sobre hormigón gris, y **no lleva el rojo de bando en el cuerpo**. `#990q` lo midió como el tipo de menor contraste (dE mediana 32,2 contra 38,9 a 40,1 del resto). | Azul descartado. Paleta con contraste interno (claro + oscuro + rojo emisivo), elegida por medición. |
| H12 | T6: "disolución con shader de umbral o escala+alfa reemplazando el hundimiento seco" | El hundimiento ya reemplazó al "pop" en la R17 y pasa **a los 14 s** (`Presentation/RetiroDeEnemigosCaidos.cs:16-18`): casi nadie lo ve. Solo corre en `SC_Operacion*` (`:36`). El material enemigo es **uno compartido** y el bloom está en 0. Un shader propio debe quedar incluido en la build (`Shader.Find` no alcanza) y el salto de material se nota. `DebrisPool.Budget = 160` cubos compartidos con impactos (`Presentation/DebrisPool.cs:13`). | Desintegración en cubos (estilo del juego), sin shader nuevo, a los ~4 s. |
| H13 | T2: "barra de vida flotante opcional" | Los soldados ya tienen `HealthBarView` y `EnemyAlertIndicatorView`. El marcador de alerta se cuelga en `localPosition (0, 1.4, 0)` fijo (`Presentation/EnemyAlertIndicatorView.cs:96`) y `EnemigoPesado.Aplicar` solo agranda el modelo visual x2 (`Operacion/EnemigoPesado.cs:61-71`): **en el pesado el marcador queda a la altura del pecho, dentro del cuerpo** [VERIFICADO por código]; la barra de vida, misma sospecha [A VERIFICAR]. El parpadeo del láser en los últimos 0,5 s y la sacudida de pasos/muerte del plan R17 (2.2 c, e, f) **no se implementaron** (`EnemigoPesado.cs:188-212`: solo engrosa). | La iteración del pesado empieza por lo que quedó sin hacer y por legibilidad. |
| H14 | T9: "sin daño de no-Player; sin giro visible" | `ReaccionAlDano` reacciona a **todo el bando jugador** (aliados IA y artillero montado incluidos; `Operacion/ReaccionAlDano.cs:66-68, 143-147`). El giro ya ocurre a 220°/s por la IA. Lo que falta es lo que el plan R17 2.3 llamaba "Feedback" (`!` + sonido), **que no se programó**: el archivo no emite ninguna señal. Un "giro inmediato" rompería al escudero, cuyo diseño es girar a 31°/s (`EnemigoEscudero.cs:24`). | T9 = señal + medición por el camino real; el giro del escudero no se toca. |
| H15 | (omitido) | **Presupuestos de la suite headless** (`Editor/HeadlessTestRunner.BusquedasGlobales.cs:15-16, 145`): búsquedas globales de escena 38 (solo baja), reintentos por cuadro 0, `Transform.Find` por nombre 144 (solo baja). Cualquier `GameObject.Find`, `Find*ObjectByType` o `.Find("x")` nuevo en runtime fuera de la lista blanca hace fallar `RunAll`. | Regla transversal R1. El v1 usa "existe el grupo" y paneles con hijos por nombre: hay que resolverlo sin Find. |
| H16 | T1: "ShowVictory agrega NIVEL 2 COMPLETADO" | El panel de victoria es de prefab y `GameOutcomeController` ya gasta 9 `Transform.Find` (`Presentation/GameOutcomeController.cs:80-278`). | El texto nuevo entra como **fila** de `EstadisticasDeMision.Filas`, sin Find. |
| H17 | T1c: "marca ✓ en la tarjeta" | Las fuentes del juego son `FuentesBelicas`; un glifo Unicode puede no existir [A VERIFICAR]. | Texto "COMPLETADO" + `Image`, no el carácter ✓. |
| H18 | Hechos del v1 que sí son correctos | Menú: Nivel 1 = Tutorial, Nivel 2 = Operación (`Presentation/MainMenuController.cs:147-157, 219-228`). `Ganar()` en `OperacionDirector.Extraer.cs:403`. Panel de `VictoriaTutorial` con REPETIR / VOLVER AL MENÚ (`Tutorial/VictoriaTutorial.cs:126-132`). Clip de muerte por dirección y tope de 24 cadáveres (`Presentation/CubeFxReactor.cs:233, 262-288`). Próximo número de check libre: **994** (el último es `Bug993a`). | Se mantienen. |

---

## 2. Reglas transversales (valen para todas las tareas)

- **R1 Búsquedas.** Runtime nuevo sin `GameObject.Find`, `Find*ObjectByType` ni `.Find("nombre")`. Las referencias salen de registros (`ActorRegistry.All`, `Vehicle.Todos`, `WorldSystemsRegistry`), de campos ya resueltos (`OperacionDirector.Instancia.tanque/computadora/plaza`) o de propiedades expuestas a propósito. El arnés de capturas nuevo va a la lista blanca con su cupo y su motivo.
- **R2 Estáticos.** Todo estático nuevo (contadores, materiales compartidos, latches, relojes) se reinicia en `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`.
- **R3 Tiempo.** Lógica nueva con `dt` acumulado (`Tick(dt)`), no `Time.time`, para que la suite headless (Edit mode, `Time.time` congelado, sin `OnEnable`) la pueda simular.
- **R4 Materiales.** Compartidos y estáticos; por instancia solo `MaterialPropertyBlock`. Nunca destruir un compartido desde un `OnDestroy` de instancia.
- **R5 `SfxKind`.** Solo se agrega al final del enum y con respaldo procedural. Este plan no necesita ninguno nuevo.
- **R6 Compilar de verdad.** Tras cada edición: `AssetDatabase.Refresh()` + `CompilationPipeline.RequestScriptCompilation(CleanBuildCache)`, esperar `status: completed` y leer `errors`. "SIN CHECK BugXXX" = no compiló. Un nombre de check inexistente aborta toda la lista de `CorrerLista`.
- **R7 Un solo Editor.** Se puede editar en paralelo por carriles, pero compilación y checks son seriales (un dominio). Un error de compilación de un carril frena a todos: cada carril compila antes de soltar.
- **R8 Modo completo intacto.** Todo lo del tutorial simple va detrás de `ModoSimple`; el completo (36/37 pasos) no cambia ni un texto.
- **R9 Dificultad.** Nada de este plan debe subir la tasa de derrota: lo que agrega presión (T8-B, T9) lleva tope y se mide en la pasada 2.

---

## 3. Diseño por punto

### 3.1 Punto 1 — "Nivel 1 terminado"

**Interpretación.** Ambigua de verdad. (A) El **NIVEL 1 del menú**, que hoy es el Tutorial simple: que cierre como un nivel (se completa, queda marcado, encadena al siguiente). (B) La Operación Cuartel como "el primer nivel de verdad", que es la lectura que tomó la R17 y quedó PARCIAL. **Default: A**, porque es lo que la build del 24/11 rotula "NIVEL 1" y porque los otros 8 puntos ya pulen la Operación. Para no perder B, se le aplica la misma definición de terminado de forma acotada (45 min, solo bloqueos).

**Definición de terminado (todo medible).**
- D1. Menú → NIVEL 1 → tutorial simple → paso `victoria` → panel "NIVEL 1 COMPLETADO" con 3 botones: **JUGAR NIVEL 2** (primario), REPETIR, MENÚ.
- D2. Persistencia: `sp_nivel1_completo = 1` y `sp_nivel1_mejor_s` (mejor tiempo). El menú muestra en la tarjeta del Nivel 1 "COMPLETADO · mm:ss" y resalta la del Nivel 2 con "SIGUIENTE".
- D3. Corrida automática del simple: 0 pasos fallidos, 0 forzados, 0 excepciones ni `LogError`, duración <= 90 s.
- D4. Ningún paso puede trabar: cada uno tiene marcador en el mundo, ayuda a los 8 s y [F8].
- D5. Sin derrota posible en el simple (escuadra y tanque vivos al final).
- D6 (lectura B, acotada). `OperacionPrueba.Recorrer(8f)` pasa los 6 objetivos sin excepciones; al ganar se graba `sp_nivel2_completo` y la pantalla de resultado muestra la fila "NIVEL 2 COMPLETADO".

**Lógica y archivos.**
- Nuevo `Core/ProgresoDeNiveles.cs` (estático, puro): `Completar(int nivel, float segundos)`, `EstaCompleto(int)`, `MejorTiempo(int)`, `Borrar(int)`; claves `sp_nivel{n}_completo` y `sp_nivel{n}_mejor_s`. `Completar` solo mejora el tiempo, nunca lo empeora.
- `Tutorial/TutorialManager.cs` → `Finalizar()`: `ProgresoDeNiveles.Completar(1, TiempoTotal)` (en simple y en completo).
- `Tutorial/VictoriaTutorial.cs` → `MostrarPanel()`: título del panel "NIVEL 1 COMPLETADO" (el destello y el título del paso "¡TUTORIAL COMPLETADO!" se conservan: su clave está en `LocTextos`); panel 560x330 → 760x360; tres botones en y = -110: `BotonSiguiente` (x = 0, 260x60, dorado (0.86,0.62,0.12), texto "JUGAR NIVEL 2"), `BotonRepetir` (x = -255), `BotonMenu` (x = +255). Acción nueva `TutorialManager.IrAlNivel2()`: `Time.timeScale = 1; SceneLoader.Cargar("SC_Operacion")` (mismo destino que `MainMenuController.ElegirNivel(2)`, `:227`). Si ya estaba completo antes, una línea chica "Mejor tiempo: mm:ss".
- `Presentation/MainMenuController.cs` → `MostrarNiveles()` (`:159-217`): dentro del bucle de celdas, si `ProgresoDeNiveles.EstaCompleto(n)`, texto "COMPLETADO · mm:ss" en verde (0.45,0.95,0.55) anclado en (0.5, 0.08) + `Image` de 18x18 verde a su izquierda; si `n == 2 && EstaCompleto(1) && !EstaCompleto(2)`, borde dorado de 4 px y texto "SIGUIENTE". El panel se cachea (`:163`): se reconstruye el estado al reabrirlo (guardar los `Text` en campos, sin Find).
- `Operacion/OperacionDirector.Extraer.cs` → `Ganar()`: `ProgresoDeNiveles.Completar(2, tiempoDeMision)`. `Mision/EstadisticasDeMision.cs` → primera fila "NIVEL 2 · OPERACIÓN CUARTEL — COMPLETADO" solo si `OperacionDirector.Activo` (sin Find, H16).

**Riesgos.** `PlayerPrefs` compartido con las corridas de prueba: los checks llaman `Borrar` al empezar y al terminar. Cargar `SC_Operacion` desde el tutorial hereda `Health.RegeneracionPermitida` y `Demolicion.Segundos` si `OnDestroy` no corre antes: ya los restaura (`TutorialManager.cs:106-113`); el check lo confirma. "CONTINUAR" (`PartidaGuardada`) no se toca.

**Aceptación / checks.**
- `Bug994a` (Play, `SC_Tutorial`): saltar hasta `victoria`; a los 2 s `EstaCompleto(1)`, panel con los 3 botones no nulos, texto del primario "JUGAR NIVEL 2"; invocar su `onClick` → la escena activa pasa a `SC_Operacion` en <= 10 s y `Health.RegeneracionPermitida == true`.
- `Bug994b` (Play): menú con `sp_nivel1_completo = 1` → la celda 1 contiene "COMPLETADO" y la 2 "SIGUIENTE"; `OperacionPrueba.Arrancar(6)` + victoria → `EstaCompleto(2)` y `PantallaDeResultado.FilasMostradas` incluye la fila nueva.
- Headless `H25.1`: `ProgresoDeNiveles` guarda, no empeora el tiempo y borra.

### 3.2 Punto 2 — "Iterar en enemigo grande"

**Interpretación.** El PESADO (`EnemigoPesado`: modelo x2, vida x12, máscara, furia). Alternativa: el BLINDADO GOLIAT (jefe). **Default: pesado**, porque es el "gordo/grande" de los pedidos originales y el jefe ya recibe trabajo por el punto 8. "Iterar" se concreta en 4 objetivos de diseño con número: que se **lea**, que tenga **ventana de castigo**, que tenga **presencia** y que su **muerte pague**.

**Lógica (constantes nuevas en `Operacion/EnemigoPesado.cs`).**
- a. **Legibilidad sobre la cabeza.** En `Start`, después del escalado: subir el marcador de alerta y la barra de vida a `alturaDeCabeza + 0,45 m`, donde `alturaDeCabeza = HitboxCabeza.De(soldado).Centro.y - transform.position.y`. Sin Find: `EnemyAlertIndicatorView` expone `public Transform Marcador` y la barra se toma con `GetComponentInChildren<HealthBarView>(true)`. `Presentation/EnemyAlertIndicatorView.cs` (propiedad nueva, 1 línea).
- b. **Telégrafo completo.** En `Telegrafar`: en los últimos `SegundosDeParpadeo = 0.5f` del estacionamiento el láser parpadea a `HzDeParpadeo = 8f` (alfa 1 / 0,25). Era lo previsto en la R17 y no se hizo.
- c. **Ventana de castigo.** Mientras `Recargando` (recarga de 4,2 s): `soldado.Health.FactorDeDano = FactorDeDanoRecargando = 1.5f` y vuelve a 1 al terminar; un cartel de mundo "RECARGA" una vez por recarga (`Feedback.Accion(null, "RECARGA", pos, Feedback.Info, aviso: false, pulso: false)` [A VERIFICAR la firma con sonido nulo; si no admite null, `SfxKind.EmptyClick` a volumen 0,25]) y humo cada 0,5 s desde la boca del arma (`SpriteFx.Lanzar("smoke_02", ...)`, alfa 0,25). Compone con `Dificultad` y `AjustesDeDano` porque `FactorDeDano` se aplica antes (`Combat/Health.cs:182`).
- d. **Presencia.** Primer pesado que entra en Estacionando en cada tramo: `AlertQueue.Push("¡ENEMIGO PESADO! · 2 TIROS A LA CABEZA O ATACALO CUANDO RECARGA", AlertPriority.Alta, 3.5f)` con enfriamiento estático de `SegundosEntreAnuncios = 45f`. Pasos: `CameraRig.KickDirectional(Vector3.down, 0.04f * (1 - d/14))` [A VERIFICAR firma] si el poseído está a < 14 m, como mucho 1 cada 0,45 s entre todos los pesados (reloj estático).
- e. **Muerte.** Al detectar `!IsAlive` por primera vez: sacudida 0,18 si el jugador está a < 25 m; a los 0,7 s (toca el piso) `SfxKind.PasoPesado` volumen 0,9 tono 0,4 y polvo x2,5 (`smoke_02`, tamaño 1,5); la desintegración del punto 6 usa su variante grande.

**Qué NO se hace.** Barra de jefe en el HUD (es una sola y puede haber varios pesados), más cadencia o más vida (la R17 ya lo dejó en 2 headshots o ~46 tiros de fusil), cambio de armas (el cohete en Frenesí es un pedido explícito, bug #389).

**Riesgos.** Las fotos del pesado salen tapadas por sus explosiones (memoria 13/11): para fotografiar se lo pone `Brain.Pasivo = true`. `FactorDeDano` lo usa el tutorial sobre la escuadra propia (`TutorialManager.cs:1641-1653`), nunca sobre enemigos: no hay cruce. El pesado no puede llevar escudo (los dos `Aplicar` son excluyentes en Resistir/Mansión/Extraer).

**Aceptación / checks.**
- `Bug994c` (Play, `Arrancar(5)`, fusilero convertido con `EnemigoPesado.Aplicar`): `Marcador.position.y` y la barra quedan >= `bounds.max.y + 0,10` del `SkinnedMeshRenderer`; con `Weapon.IsReloading` forzado, `Health.FactorDeDano == 1,5` y vuelve a 1,0 en <= 0,3 s tras la recarga; en los últimos 0,5 s de Estacionando el láser alterna `enabled`/alfa al menos 3 veces; el anuncio sale 1 vez con 2 pesados en 10 s.
- `Bug994d` (Play): matar al pesado con el jugador a 10 m → contador `SacudidasDeMuerte` +1, `PolvosDeMuerte` +1 a los 0,7 ± 0,2 s, y desintegración grande (>= 18 cubos).

### 3.3 Punto 3 — "tutorial simple"

**Interpretación.** Ya hay un modo simple de 11 pasos por defecto. Que se pida de nuevo dice que **sigue sin ser simple**: gestos difíciles (radial obligatorio), textos largos y un final sin combate (H10). Default: rehacer el simple como **8 pasos de un gesto cada uno**, con `[E]` y teclas directas, y un final donde se dispara el cañón. Alternativa descartada: solo acortar textos manteniendo los 11.

**Lista nueva (`IdsSimples`, 8).**

| # | Id | Qué pide | Cambio respecto a hoy |
|---|---|---|---|
| 1 | `camara` | mover el mouse | igual |
| 2 | `wasd` | caminar | igual |
| 3 | `disparar` | derribar al enemigo quieto | en simple, 1 sub-paso (se quitan pared y caja); texto + "A LA CABEZA = 1 TIRO" |
| 4 | `cambiar` | cambiar de soldado | en simple acepta la tecla directa de cambio de aliado además del radial (la condición ya es `Brain.Current != soldadoInicial`, `:382`) [A VERIFICAR el nombre en `KeyBindings`] |
| 5 | `rts` | Tab + ordenar a los 2 | se le suma el sub-paso final "Tab: volvé a primera persona" (absorbe `fps`) |
| 6 | `entrar_tanque` | subir al tanque | en simple `SoloRadial = false`: "Acercate al círculo y apretá [E]"; con `AnilloDeSubida` (punto 7) |
| 7 | `canon_simple` (nuevo) | [2] al cañón y derribar 3 | reemplaza a `mira_tanque`; ver abajo |
| 8 | `victoria` | cierre | panel del punto 1 |

Salen del simple: `mira`, `seguir`, `fps` (absorbido), `mira_tanque` (reemplazado). Siguen en el completo sin cambios.

**`canon_simple`.** Sub 1 "Apretá [2]: pasás al cañón" (`f.enLaTorreta`). Sub 2 "Derribá 3 enemigos con el cañón (clic izquierdo)" (`f.enemigosEliminados`, contador vivo n/3). Al cumplirse el sub 1 —**ese es el disparador**— `LanzarOlaA()` (4 enemigos a 36 m que avanzan al tanque, `:1568`) y `PostureDeAliados(AltoElFuego)`; `Evaluar` reusa `RellenarOlaA()`. `Protecciones()` (`:1658`) suma `canon_simple` a `enOla` para aplicar `FactorDeDanoEnLasOlas = 0,1`.

**Reglas del modo simple.** Mensaje de cada sub-paso <= 110 caracteres; total de sub-pasos <= 13; ayuda (pista + flecha) a los `SegundosParaAyudaSimple = 8f`; ningún paso exige el radial; cada paso tiene un marcador en el mundo (`TutorialBeacon` o anillo).

**Archivos.**
- `Tutorial/TutorialManager.cs`: la clase pasa a `partial` (1 palabra); `IdsSimples` con la lista nueva; `public const int PasosSimples = 8`; `ClaveProgresoSimple = "sp_tutorial_paso_simple2"` (el índice viejo deja de valer, H9); en `Start`, tras el filtro (`:166`), `if (ModoSimple) AplicarVariantesSimples();`; propiedad `SegundosDeAyudaActual => ModoSimple ? SegundosParaAyudaSimple : SegundosParaAyuda` usada en `:1735` y `:1746`; `Protecciones` y `ObjetivoDeAyuda` conocen `canon_simple`. **`SegundosParaAyuda` sigue en 16.**
- Nuevo `Tutorial/TutorialManager.Simple.cs`: `AplicarVariantesSimples()` reemplaza `Subs` / `AlEntrar` / `AlSalir` de `disparar`, `cambiar`, `rts`, `entrar_tanque` e inserta `canon_simple` antes de `victoria`. Los textos del completo quedan literales en el archivo original (no hay claves huérfanas).
- `Core/LocTextos.cs`: claves en inglés para los textos nuevos.
- `Tutorial/TutorialAutoPlayer.cs` (+ `.Entrada.cs`): `case "rts"` ejecuta `GestoRts` y, en simple, `GestoFps`; `case "canon_simple"`: `GestoCambiarATorretaReal` + la parte de apuntar y disparar de `GestoAvanzarYDispararReal`; tope de 100 s para ese Id (`:107`). `entrar_tanque` sigue con su gesto por radial (el radial no se desactiva).
- `Editor/HeadlessTestRunner.Fases13a17.cs:327`: `ModoSimple ? TutorialManager.PasosSimples : 36`.

**Riesgos.** `rts` deja al jugador en vista RTS si el sub-paso final se salta con [F8]: `AlSalir` fuerza FPS. La ola A tarda en llegar: si a los 20 s no hay bajas, `RellenarOlaA` ya repone; el sub-paso tiene tope de ayuda. Autoplayer sin ventana: los flancos de teclado virtual solo valen en el update que procesa el evento (memoria): los gestos nuevos reusan los existentes, no inventan entrada.

**Aceptación / checks.**
- `Bug994e` (Play, `SC_Tutorial`, simple): `tm.Total == 8`; ningún `Sub.Mensaje` del simple supera 110 caracteres y la suma de sub-pasos <= 13; en `entrar_tanque`, `driver.SoloRadial == false`; al cumplirse el sub 1 de `canon_simple` hay >= 3 enemigos vivos de la ola en <= 2 s; autoplayer: 8/8 OK sin FALLIDO_FORZADO, <= 90 s, escuadra y tanque vivos.
- `Bug994f` (Play, completo): `tm.Total >= 36`, textos de `entrar_tanque` y `mira_tanque` idénticos a los actuales (hash), `SegundosParaAyuda == 16`. La corrida completa del autoplayer (12-18 min) va en la pasada 2, no en cada iteración.
- Headless `H25.2`: `IdsSimples.Count == PasosSimples`, la clave simple no es la vieja, `Fase19` y `Fases13a17` siguen en OK.

### 3.4 Punto 4 — "Vestir exterior servidores"

**Interpretación.** El exterior del edificio del Centro de datos (planta x -26..26, z -80..-40, muros de 8 m). La R17 vistió solo **a la altura del suelo, la mitad sur de los lados este y oeste y la esquina SE**. Queda desnuda la **fachada norte** (la que se ve al salir hacia el tanque), la **banda alta de 5 a 8 m** (los muros se subieron de 5 a 8 después del arte), la mitad norte de E/O y el ritmo general de los cuatro paños. Alternativa: vestir los patios (losas vacías de 120x36 y 34x42); se cubre en parte con una vereda perimetral y queda como opcional lo demás.

**Decisión de arquitectura.** Parche de runtime, no capa de Editor (H6). Nuevo `Presentation/AjustesDeNivelR19.cs` con grupo `R19_Servidores_Exterior`, hijo de la misma raíz. Sin vigilante propio ni Find: `AjustesDeNivelR17` expone `internal static Transform RaizDeLaFachada` (la que ya encuentra en `ArmarFachada`) y sus ayudantes `Mat / Caja / Pieza / Cobertura / Texto / Combinar / Suelo / Grupo` pasan de `private` a `internal`; `AjustesDeNivelR17.Aplicar` llama `AjustesDeNivelR19.Aplicar()` (latch `Listo`, idempotente por nombre de grupo con `transform` hijo cacheado, no `Find`).

**Caras exteriores:** sur z = -80,75 · norte z = -39,25 · oeste x = -26,75 · este x = 26,75.
**Zonas libres (nada, ni arte):** puerta sur x ∈ [-4,6; 4,6]; salida norte x ∈ [-7; 7] (ahí explota el muro); puertas laterales z ∈ [-62,6; -55,4]; todo lo que tenga collider se queda a <= 3 m del muro y con |x| >= 18 en la cara norte.

**Kit (todo con prefijo `R19_`; cubo y cilindro compartidos como en la R17).**

| Pieza | Dónde | Medidas y cantidad | Collider |
|---|---|---|---|
| K1 Zócalo oscuro (0.10,0.11,0.13) | 4 caras, cortado en las puertas | alto 0,55, saliente 0,10 · 8 tramos | no |
| K2 Cornisa clara (0.62,0.66,0.72) | 4 caras, y 8,00 a 8,35 | saliente 0,30 · 4 tramos | no |
| K3 Pilastras (0.20,0.22,0.26) | cada 8,67 m; esquinas incluidas; se saltean las puertas | 0,70 x 8 x 0,25 · ~20 | no |
| K4 Franja corporativa cian emisiva (0.2,0.8,1.0) x 0,9 | 4 caras, y 7,2 | alto 0,12 · 8 tramos | no |
| K5 Rejas de ventilación (marco + 3 lamas) | una por paño entre pilastras, y 6,0 | 2,4 x 1,2 x 0,08 · 16 (64 cubos) | no |
| K6 Luminarias de pared emisivas blanco frío | paños alternos, y 4,4 | 0,5 x 0,18 x 0,20 · 8; **solo 2 `Light` spot reales** en la cara norte (`shadows = None`, alcance 9, intensidad 2,6) | no |
| K7 Norte: tubería triple horizontal + 2 bajadas + 2 tableros eléctricos | tubos y 5,2 / 5,5 / 5,8 de x -25 a -8 y de 8 a 25; tableros en (±20, -38,6) | tubos ⌀0,18; tableros 1,2 x 1,8 x 0,5 (cobertura, vida 300) | solo tableros |
| K8 Sur: marquesina sobre la entrada + tótem lector + 6 bolardos | marquesina y 4,9 (base >= 4,78: se oculta sola en RTS); bolardos en z -84,5, x = ±6, ±8,5, ±11 | losa 10 x 0,25 x 2,4; bolardos ⌀0,35 x 0,9 | bolardos y tótem, con carving |
| K9 E/O mitad norte: 2 torres de enfriamiento por lado + escalera de gato + cámara | torres en (±28,6, -47) y (±28,6, -50,5); escalera en z -44 | torre ⌀2,0 x 2,4 (cobertura, vida 400); escalera 2 largueros + 10 peldaños | solo torres |
| K10 Luces rojas de obstrucción | 4 esquinas, y 8,6 | esfera ⌀0,3 emisiva | no |
| K11 Vereda perimetral clara (0.42) + 2 textos "NODO 3 · SECTOR N/E" | caras N, E y O | 1,2 de ancho, +0,02 sobre el piso | no |

Presupuesto: <= 240 objetos, <= 6 materiales nuevos, +2 luces sin sombra; todo combinado por grupo (`StaticBatchingUtility.Combine`).

**Reglas que el v1 no tiene (H7).**
- Ningún nombre empieza con `Bandeja`, `Cable`, `Ducto`, `Baliza`, `Viga`, `LineaLuz`, `Panel_Luz`, `Rejilla_Aire`, `Costilla`, `Dintel_Luz`, `Travesano` ni `Techo` (prefijos que `SalaDespejadaEnRts` trata aparte). Lo que queda por encima de 4,5 m se ocultará en RTS: es coherente con el techo, se acepta.
- Todo adorno apoya en algo indestructible (muro o piso, empotrado 2 cm). Lo que dependa de una cobertura destructible es **hijo** de ella. Corrección incluida: los 2 `Conducto_Vertical` de la R17 bajan hasta el piso por detrás del condensador (hoy flotan al demolerlo, `#501b`).
- Antes de crear una pieza sin collider: comprobar contra las cajas de las zonas libres y contra `SM_Mod_Muro_Pilar` ya existentes (bounds de los renderers del perímetro, recogidos una vez desde la raíz) para no encimar pilastras.
- `Cobertura()` ya se niega a crear si el lugar está ocupado (`AjustesDeNivelR17.cs:132-143`): se respeta.
- Mejora barata: `SalaDespejadaEnRts` guarda el `shadowCastingMode` original y lo restaura (hoy deja `On` a piezas que eran `Off`).

**Riesgos.** Colliders nuevos cortando el NavMesh o la aparición de refuerzos (mitigado con carving + check de caminos). Rendimiento (la R17 midió +1,5 % con 100 objetos). Mallas no legibles impiden combinar (las de cubo/cilindro compartidas sí lo son). El arte es de cubos: suma lectura, no realismo.

**Aceptación / checks.**
- `Bug994g` (Play, `Arrancar(3)`): **cobertura de fachada** — cada cara se parte en celdas de 4 m x 3 bandas de altura (0-2,7 / 2,7-5,3 / 5,3-8) y una celda cuenta como vestida si algún renderer `R17_*`/`R19_*` la interseca hasta 1,5 m hacia afuera. Se exige >= 70 % de celdas vestidas por cara y ningún tramo de más de 8 m con las 3 bandas vacías (se registra el valor ANTES en T0). Además: 0 renderers y 0 colliders `R19_` dentro de las cajas de las 4 aberturas; `NavMesh.CalculatePath` completo desde (0,-104), (-49,-60), (49,-60) y (0,-90) hasta el destino de las oleadas (0,-54); `AjustesDeNivelOpus.BloqueosDeLaRuta` sin entradas nuevas; reaplicar no duplica (`ObjetosCreados` estable); `#501b` no suma piezas `R19_` y baja en 2 por los conductos.
- `Bug994h` (Play): mediana de tiempo de cuadro mirando la cara norte desde (0, 1,7, -20), antes y después: no empeora más de 3 %.

### 3.5 Punto 5 — "Contraste enemigo escudo"

**Interpretación.** El escudero se confunde con el entorno y no se lee como enemigo: es gris sobre hormigón, y la placa clara es lo más grande que se ve de frente (H11). Alternativa: que se confunda con el pesado (no aplica: violeta y doble de tamaño).

**Principio.** Contraste **interno**: un objeto con una zona muy clara, una muy oscura y un borde emisivo saturado se despega de cualquier fondo (desierto, hormigón, asfalto de noche, sala azulada). Y el tono debe caer en la familia del bando enemigo sin pisar el óxido del común ni el dorado/azul aliado.

**Paleta por defecto "antidisturbios" (`Actors/SoldierLook.cs`, `MaterialEscudero`, `:322-328`).** Uniforme (celdas 8/10/12) carmesí oscuro (0.50,0.06,0.06) · casco y chaleco (52) blanco hueso (0.90,0.90,0.86) · pantalón (19) negro (0.06,0.06,0.07) · correaje (32/33) rojo vivo (0.95,0.12,0.08) en lugar del amarillo (que se parece al dorado del asalto aliado) · emisión (0.22,0.03,0.03). Las insignias rojas de la R17 se conservan.

**Escudo (`Operacion/EnemigoEscudero.cs`, `ConstruirEscudo`, `:119-149`).** Placa de acero claro → **gunmetal oscuro** (0.10,0.11,0.13), metal 0,3, sin emisión. Marco: grosor 0,08 → 0,12, color (0.60,0.05,0.04), emisión (1.6,0.14,0.08). Las dos franjas amarillas se reemplazan por **3 chevrones blancos** (0.92) de 0,10 m, rotados 35° en Z, en la mitad inferior. Ventana 0,62x0,16 → 0,70x0,20 con emisión base (1.2,0.40,0.10). Reverso (opcional): el agarre en ámbar emisivo para que de atrás se lea "lado abierto". Siguen siendo materiales estáticos compartidos (5 como máximo, sin importar cuántos escuderos haya); la ventana por instancia se mantiene.

**Elección por medición.** Se prueban 3 candidatas con el medidor de `#990m/#990q` (`R17MedirContraste`): A antidisturbios (default), B la actual con solo el escudo nuevo, C negro + blanco + rojo. `SoldierLook.PaletaDeEscudero` (estático de prueba, con su reset y vaciado de caché) permite alternarlas desde el check. Regla: gana la de mayor dE mínima; empate → A.

**Riesgos.** Un tono demasiado cercano al del común hace que se confundan a distancia: lo cubre la métrica de separación. La emisión sin bloom solo aclara: tope 1,6 en un canal. La trampa conocida de medición: `SoldierLook` aplica en `Start`, medir el mismo cuadro da el trimsheet crudo (esperar 6 cuadros).

**Aceptación / check.** `Bug994i` (Play, objetivos 1 a 3, frente y espalda, >= 6 tomas): dE mediana >= 40 (antes 32,2), dE mínima >= 25, ratio de luminancia mediana >= 1,5, 0 tomas bajas; **separación** del color medio de la silueta en Lab: >= 25 contra el fusilero común, >= 25 contra el pesado, >= 40 contra un aliado; con 5 escuderos vivos, materiales distintos de escudo <= 5 + 5 ventanas. `#990q` sigue en OK.

### 3.6 Punto 6 — "Animacion de eliminacion de enemigos"

**Interpretación.** Dice "eliminación", no "muerte": el momento en que el enemigo **desaparece**. La muerte ya tiene clip por dirección, polvo y arma que cae; lo que no tiene es un cierre visible (el cuerpo se hunde 0,6 m a los 14 s). Default: **desintegración en cubos a los ~4 s**, que es el lenguaje visual del juego (`ImpactCubes`, `DebrisPool`, confeti de cubitos). Alternativa: disolución por shader; se descarta por riesgo de build y por el salto de material (H12).

**Máquina de estados por cadáver (`Presentation/RetiroDeEnemigosCaidos.cs`).**

| Estado | Duración | Qué pasa |
|---|---|---|
| `Caido` | `SegundosAntesDeRetirar` = **4,0** (pesado 5,5) | corre el clip; polvo a los 0,6 s y arma caída, como hoy |
| `Destello` | `SegundosDeDestello` = 0,20 | tinte por `CubeFxReactor.WriteTint` de su color a (1.0,0.45,0.30) x 1,6; tapa el corte |
| `Desintegrando` | `SegundosDeCubos` = 0,80 | renderers del cuerpo apagados; cubos desde los huesos |
| `Retirado` | — | `Retirados++` (el contador se conserva) |

- **Cubos:** uno por hueso de `Animator.GetBoneTransform` para 12 huesos (cabeza, pecho, columna, cadera, brazos y antebrazos, muslos y piernas) → `CubosPorCuerpo = 12` (pesado 20, tamaño x2). `DebrisPool.Spawn(pos, vel, color, tam, vida)` con `vel = up * [1,2..2,4] + radial * [0,4..1,0]`, `tam` 0,16-0,26, `vida` 0,7-1,0 s, color = tinte del tipo (común (1.00,0.30,0.24); pesado violeta; escudero el de su paleta). Sin `Animator`: 8 cubos dentro de los límites.
- **Tope:** si `DebrisPool.ActiveCount > 110` (de 160) se lanzan 6; como mucho 4 desintegraciones nuevas cada 0,25 s (el resto espera su turno).
- **Sonido:** `AudioDirector.PlayAt(SfxKind.ImpactDirt, pos, 0.35f, 1.4f)` solo si la cámara está a < 30 m.
- **Guardas:** si revive (`IsAlive`) en cualquier estado se cancela y se restauran tinte y renderers; si el soldado se destruye o cambia la escena, la corrutina sale. Con `RagdollDeExplosion` activo se espera a que termine [A VERIFICAR cómo expone su fin; si no lo expone, +1,5 s].
- **Se quita** el hundimiento (`MetrosDeHundimiento`): los cubos lo reemplazan. `ArmaCaida` se destruye junto con el cuerpo (su `Destroy` ya usa las dos constantes).
- **Alcance:** `Asegurar` acepta también `SC_Tutorial` (el Nivel 1 debe mostrar lo mismo).
- Función pura para la suite: `FaseEn(float t, float espera) → enum`.
- Contadores: `Desintegrados`, `CubosLanzados`, `EnCurso`.

**Archivos.** `Presentation/RetiroDeEnemigosCaidos.cs` (máquina y cubos; se puede extraer a `Presentation/DesintegracionDeCaido.cs` si pasa de ~200 líneas). `CubeFxReactor.cs` no se toca (se usa `WriteTint`, que es público).

**Riesgos.** Fotos y checks que miran cadáveres después de 4 s (tomas "muerte en el piso": se toman a los 2 s). `#990n` (arma caída y polvo) espera 0,6 s: no cambia. El tope de 24 cadáveres casi nunca se alcanzará; se deja. Auditorías `#501b/d/e`: los cubos son de pool y `ArmaCaida` ya está excluida. Mansión/PC bloqueada cuentan vivos, no cadáveres.

**Aceptación / check.** `Bug994j` (Play, `Arrancar(2)`): matar a un común → a los 2 s sigue visible; entre 4,0 y 4,3 s `EnCurso >= 1` y el tinte cambió; a los 5,2 s todos sus renderers están apagados, `Desintegrados` +1 y `CubosLanzados` entre 6 y 12; con 6 bajas simultáneas `DebrisPool.ActiveCount <= 160` y 0 excepciones; un caído revivido con `Health.Initialize` a los 3 s conserva renderers y tinte. Headless `H25.3`: tabla de `FaseEn`.

### 3.7 Punto 7 — "Feedback entrar al tanque circulos piso"

**Interpretación.** Círculos en el piso que digan "acá se sube" y confirmen la subida. Existen desde la R17, pero **mienten el radio** (H2), no aparecen en el tutorial y muestran el cartel del tanque en la plaza (H4). Alternativa: círculo de impacto del cañón al ir adentro del tanque; no es el default (el texto dice "entrar").

**Lógica.**
- **Una sola verdad para el radio.** `OperacionDirector.RadioDeAbordaje = 6.5f` (constante nueva; es el valor de hoy, no cambia la jugabilidad). La usan `TickAntesDelTanque` (`OperacionDirector.Huida.cs:29`) y el anillo. La distancia del anillo pasa a medirse **en planta** (igual que el director), no en 3D.
- **API de `Operacion/AnilloDeSubida.cs`:** sobrecarga pública `Mostrar(Transform objetivo, float radioDeUso, string cartel)`; `Ocultar(bool subio = false)`: el destello solo con `subio == true`; con radio de uso >= 5 m, `EscalaLejos` efectiva 1,3 en vez de 1,6 (para no cubrir medio patio); `AmplitudDelLatido` se queda en 0,23 (el check `#111` exige variación > 0,40).
- **Confirmación de subida (la "onda"):** en vez de un destello de 0,25 s, **3 aros concéntricos** que se expanden de 1,0 a 1,6 / 1,9 / 2,2 veces el radio en `DuracionDeOnda = 0.6f`, desfasados 0,1 s, de verde a transparente, más el sonido de asiento que ya suena.
- **Árbitro único** en `OperacionDirector.cs` (reemplaza a `TickAnilloDeSubida`, `:503-513`): `bool ObjetivoDelAnillo(out Transform t, out float radioDeUso, out string cartel)` decide por fase — Huir/Abordaje sin estar a bordo → tanque, 6,5, "[E] SUBIR AL TANQUE"; Resistir sin mansión ni mando → plaza, 5, "" (corrige H4; `TickCirculoDeBase` deja de llamar a `Mostrar`); opcional Extraer con el helicóptero posado y sin caídos → heli, 11, "[E] SUBIR AL HELICÓPTERO". Un solo `Mostrar`/`Ocultar` por cuadro; `Ocultar(true)` solo en el cuadro en que `EnTanque` pasa a verdadero.
- **Tutorial (`TutorialManager.Simple.cs` y paso completo):** en `AlEntrar` de `entrar_tanque`, `AnilloDeSubida.Mostrar(driver.Vehicle.transform, driver.RadioDeInteraccion, "[E] SUBIR AL TANQUE")` (en el tutorial `[E]` usa el camino genérico de 3,5 m, `Player/PlayerInputDriver.cs:1664`: el anillo dice la verdad de esa escena) y `balizaTanque.OcultarAnillo()` para no encimar dos marcas; en `AlSalir`, `Ocultar(subio: driver.Vehicle.PlayerAboard)`. En el completo el cartel es "USA [Q] → TANQUE" porque ahí `[E]` está desactivado.

**Riesgos.** Checks que usan el anillo: `#111` (`Editor/ChecksBugs104.Guia.cs:233-262`), `#990d`, toma `r17_anillo_*`: la API vieja se mantiene. Unificar los tres radios de subida (E 3,5 genérico, radial 7,0, Operación 6,5) tocaría `PlayerInputDriver` (2.900 líneas, muchos checks): **no se hace**; queda anotado.

**Aceptación / checks.**
- `Bug994k` (Play, Abordaje): `RadioDeUso == RadioDeAbordaje ± 0,05`; con el jugador a 5,0 m `DentroDelRadio == true` y color verde; a 7,5 m falso y aros de llamada activos; `Embarcar()` → onda de 3 aros durante 0,6 ± 0,15 s y después apagado; forzar Resistir sin mando: anillo en la plaza con `TextoDelCartel == ""` y radio 5; cambiar de fase estando adentro no dispara onda ni sonido.
- `Bug994l` (Play, `SC_Tutorial`, paso `entrar_tanque`): `AnilloDeSubida.EstaActivo`, sigue al tanque, radio = `RadioDeInteraccion`; `EnterVehicle` → onda y apagado; el anillo de la baliza está oculto.
- Regresión: `#111`, `#990d`.

### 3.8 Punto 8 — "Trigger para empezar a atacar al tanque"

**Interpretación.** (A) El **inicio de la pelea contra el BLINDADO** debe depender de una decisión espacial del jugador, visible. (B) Que **los enemigos empiecen a atacar el tanque del jugador** a partir de un disparador. La R17 hizo A con un plano invisible pegado a la salida (H1) y B solo como reacción al daño. **Default: A rehecho como umbral visible en la boca del patio**, más **B barato** (orden de ataque al embarcar y disparador del final del tutorial). Se cubren las dos lecturas porque B cuesta poco y su mecánica ya existe (`AiBrain.IssueAttackVehicleOrder`, `Ai/AiBrain.Vehiculos.cs:42`).

**A. Umbral del blindado (`Operacion/OperacionDirector.Jefe.cs`).**
- `ZParaLaToma`: -30 → **-8** (32 m de aproximación desde el muro, a 30 m del tanque, entre los sacos de z -12 y -4 y antes del pórtico "TANQUE ^" de z 3). El jugador ve al blindado dormido, puede acomodar la escuadra y **decide** cruzar.
- Nueva `ZFuegoDelJefe = -30f` para el latch de "ya salió del Centro" (`:233`, bug #311): ese comportamiento no cambia.
- Cruza **cualquiera de la escuadra viva** (no solo el poseído): evita el caso "mando a los aliados por RTS y el jefe sigue dormido mientras le disparan".
- `SegundosDeSalvaguardaDeLaToma`: 45 → **120**. Un arranque automático a los 45 s contradice el pedido; a los `SegundosDeRecordatorioDeLaToma = 25f` (y cada 25 s) se repite el aviso "CRUZÁ LA LÍNEA ROJA PARA ENFRENTAR AL BLINDADO" y nada más. El disparo por daño al blindado se mantiene.
- **Marca visible** (nuevo `Operacion/UmbralDelBlindado.cs`, runtime, sin collider): franja roja/negra de 16 x 0,03 x 1,2 m en z = -8 (8 tramos alternados), 2 postes de 2,2 m en x = ±8 con luz roja emisiva, y cartel "ZONA DEL BLINDADO · AL CRUZAR EMPIEZA EL COMBATE" (`OperacionTextoMundo`). Se crea al caer el muro, late a 1 Hz, y se apaga con fundido de 0,5 s cuando arranca la toma (para no convivir con el anillo verde del abordaje). `Cruzo(float z)` puro para la suite.
- `AcomodarEscuadraHuida` (`:462-466`) deja a la escuadra en z -12 para subfase <= 1: queda 4 m **antes** de la línea, que es lo deseado.

**B. Los enemigos van por el tanque.**
- `OperacionDirector.Huida.cs` → al final de `Embarcar()`: `OrdenarAtaqueAlTanque()`: enemigos vivos, activos, de a pie, no `Pasivo`, no `PuestoFijoPorRol`, sin `PerroEnemigo`, a <= 60 m del tanque; como mucho `MaximoContraElTanque = 8`, escalonados `0,3 + 0,15 * i` s (corrutina), cada uno `Brain.IssueAttackVehicleOrder(tanque)` + `CazaDeEnemigos.Marcar`. Contador `OrdenesContraElTanque`.
- Tutorial simple: el disparador es sentarse en el cañón (punto 3, `canon_simple`).

**Riesgos.** Checks con z relativa a la constante (`#990b`, `#990f`, `#990g`, `Bugs231a255.cs:200-212`) siguen valiendo; `#990g` pasa a tardar ~2 min. **Hay que barrer `Editor/` por teletransportes con z literal** (`-29`, `-28`, `-25`, `z > -30`) usados para forzar la toma, y por usos de `FuegoHabilitado`. `OperacionPrueba.Recorrer` puede depender de la salvaguarda [A VERIFICAR]: si es así, que cruce la línea. La orden de vehículo dura 15 s (`SegundosDeOrdenDeVehiculo`): el tanque se va, la orden caduca sola.

**Aceptación / checks.**
- `Bug994m` (Play, `Arrancar(4,1)` + `AbrirSalidaNortePorPrueba`): con el jugador en z -20 durante 6 s no hay toma y la marca existe y está visible (franja con renderers encendidos, cartel con texto); a los 25 ± 3 s sale el recordatorio; mover al jugador a z -6 → toma en <= 1 s y la marca se apaga en <= 1 s; variante: jugador en z -45 y un aliado en z -6 → también arranca; `ZFuegoDelJefe == -30`.
- `Bug994n` (Play, Abordaje con 4 enemigos de a pie a 30 m): `Embarcar()` → en <= 2,5 s al menos 3 tienen `VehiculoOrdenado == tanque`; nunca más de 8; perros y `PuestoFijoPorRol` sin orden.
- Regresión: `#990b/f/g`, `#243a`, `#291`. Headless `H25.4`: `UmbralDelBlindado.Cruzo`.

### 3.9 Punto 9 — "Ataque de enemigos cuando reciben dano"

**Interpretación.** La regla existe (`ReaccionAlDano`, R17): el herido fija al atacante, sale de su puesto y avisa al grupo. Que se pida otra vez indica que **no se nota** o que hay casos reales que el check `#990a` no cubre (usa `TakeDamage` directo, con el jugador cerca). Default: **señal + medición por el camino real + corrección de lo que falle**. Alternativa: extender la regla al tutorial; no es el default (el enemigo de práctica debe quedarse quieto).

**Lógica (`Operacion/ReaccionAlDano.cs`).**
- **Señal al aplicar la reacción de una víctima** (no testigos): `Feedback.Accion(SfxKind.EnemySpotted, "!", v.transform.position, Feedback.Bad, aviso: false, pulso: false, volumen: 0.45f)` con enfriamiento global `SegundosEntreSenales = 0.6f` y por víctima de 4 s. También para los de puesto fijo (pesado, francotirador): giran y marcan aunque no se muevan. Contador `Senales`.
- **Sin giro forzado.** La IA ya gira a 220°/s; el escudero a 31°/s por diseño (H14). No se toca `SoldierMotor`.
- **Limpieza:** `ultimaPorVictima` nunca se poda (la lista `borrar` está declarada y sin usar): se poda al morir la víctima.
- **Contadores para medir:** `UltimaReaccionSegundos`, `Senales`.
- **Medición por el camino real (define si hay que arreglar algo más).** Cinco escenarios con proyectil real (`ProjectilePool`), no con `TakeDamage`:

| Escenario | Se exige |
|---|---|
| E1 guardia atrincherado, con línea de tiro, a 25 m | primer disparo hacia el atacante en <= 2,5 s; ángulo al atacante <= 25° en <= 1,2 s |
| E2 guardia sin línea de tiro, a 45 m | estado Chase y >= 6 m recorridos hacia el atacante en 5 s |
| E3 pesado | `CurrentTarget == atacante` en <= 1,0 s; sigue `Quieto` |
| E4 escudero herido por la espalda | `CurrentTarget == atacante`; tarda >= 3 s en encarar (el giro lento se conserva) |
| E5 herido por un aliado IA (no el poseído) | igual que E1 |

  Lo que no cumpla se corrige en `AiBrain.ReaccionarAGolpe` (`Ai/AiBrain.Tactica.cs:125-140`) o en `Aplicar`, y se anota qué era. Candidatos conocidos por lectura: el que está en `Attack` contra otro blanco con línea no cambia (correcto); el compañero de grupo `Quieto` no atrincherado queda marcado cazador pero `CazaDeEnemigos.Libre` exige `!Quieto` y nunca recibe rumbo (`Operacion/CazaDeEnemigos.cs:62`) — si E2 lo muestra, soltar `Quieto` en los compañeros no atrincherados y sin puesto fijo.

**Riesgos.** Dificultad (R9): medir bajas propias en `Arrancar(1)` y `Arrancar(3)`, 3 corridas, antes y después. Ruido de señales con 100 enemigos: los enfriamientos lo acotan. WP4 #085 (solo se suelta el atrincherado que recibió daño) se respeta.

**Aceptación / checks.**
- `Bug994o` (Play, `Arrancar(2)`): E1 y E4 con proyectil real; `Senales` +1 por víctima y nunca 2 en menos de 0,6 s; el escudero no encara antes de 3 s.
- `Bug994p` (Play): E2, E3 y E5; 40 impactos de escopeta en un cuadro → <= 16 reacciones por segundo y 0 excepciones; el diccionario de víctimas queda vacío tras matar a todas.
- Regresión: `#990a`, `Wp4` (#085).

---

## 4. Interacciones entre puntos (y cómo quedan resueltas)

| Cruce | Riesgo | Resolución |
|---|---|---|
| 8-A umbral ↔ 7 anillo | dos marcas de piso a la vez en el patio (roja y verde) | la franja se funde al arrancar la toma; el anillo recién aparece en Abordaje |
| 8-A umbral ↔ 9 reacción | cohete al blindado dormido desde la sala | sigue arrancando la toma por daño; el jefe no dispara hasta `z > ZFuegoDelJefe` (bug #311 intacto) |
| 8-B orden al embarcar ↔ 9 regla V | doble orden al mismo enemigo | `IssueAttackVehicleOrder` es idempotente (renueva los 15 s); tope 8 |
| 7 anillo ↔ árbitro ↔ plaza/heli | emisores peleando cuadro a cuadro; cartel del tanque en la plaza | un solo `ObjetivoDelAnillo`; `Ocultar(subio)` |
| 7 anillo ↔ 3 tutorial | radio distinto en cada escena (3,5 vs 6,5) | cada anillo muestra el radio verdadero de su escena; unificación anotada como pendiente |
| 6 desintegración ↔ retiro R17 | mismo archivo, dos animaciones | la desintegración reemplaza al hundimiento; el contador `Retirados` se conserva |
| 6 ↔ escudo caído, máscara, insignias, arma | restos flotando | escudo y máscara ya se desparentan y tienen su propio `Destroy` (40 s / 8 s); insignias son hijas de hueso (se apagan con el cuerpo); el arma se destruye con el cuerpo |
| 6 ↔ 2 muerte del pesado | dos efectos encimados | el pesado solo aporta sacudida/polvo/sonido; los cubos son de la variante grande del punto 6 |
| 6 ↔ 9 testigos | el testigo necesita "ver" al caído | la regla T evalúa en el instante de la muerte; no depende de que el cadáver siga |
| 6 ↔ `DebrisPool` | granada que mata a 5: 60 cubos + impactos | tope 110/160 y 4 inicios cada 0,25 s |
| 5 escudero ↔ pesado ↔ común ↔ aliados | paleta que pisa otro código de color | familias: común óxido, pesado violeta, escudero blanco/negro/carmesí con marco rojo; azul y dorado prohibidos; métrica de separación en `Bug994i` |
| 5 ↔ 9 | "giro inmediato" anula la debilidad del escudero | no hay giro forzado; `Bug994o` exige >= 3 s |
| 5 ↔ 6 | cubos del escudero de otro color | toman el tinte de su paleta |
| 2 ↔ 9 | `ReaccionAlDano` saca de `Quieto` al pesado | ya es `PuestoFijoPorRol`; solo cambia de blanco |
| 2 `FactorDeDano` ↔ tutorial | el tutorial usa `FactorDeDano` | solo sobre la escuadra propia; sin cruce |
| 3 tutorial ↔ checks que cuentan pasos | `>= 11`, `== 16`, claves huérfanas, índice guardado | `PasosSimples`, constante de ayuda aparte, textos viejos intactos, clave `..._simple2` |
| 3 ↔ 1 | el final del simple es el cierre del Nivel 1 | `canon_simple` → `victoria` → panel con JUGAR NIVEL 2 |
| 4 fachada ↔ `SalaDespejadaEnRts` | piezas altas ocultas en RTS; sombras que quedan en `On` | prefijos prohibidos, se acepta el ocultado, se corrige el restaurado |
| 4 ↔ `#501b` | adornos flotantes al demoler | regla de apoyo; conductos R17 corregidos |
| 4 ↔ oleadas del Centro y salida norte | colliders en el camino o en la explosión del muro | zonas libres + check de caminos |
| Todos ↔ presupuestos de Find | `RunAll` en FALLO | R1; lista blanca solo para el arnés de capturas |

---

## 5. Tareas atómicas

Estimación: S < 45 min · M 45-120 min · L > 120 min. "Carril" = conjunto de archivos con un solo dueño; dos tareas del mismo carril no van en paralelo.

| Id | Tarea | Archivos | Dep. | Est. | Carril |
|---|---|---|---|---|---|
| T0.1 | Línea base: `RunAll`, lista de regresión, anotar fallos previos | — | — | S | V |
| T0.2 | `Bug994z` (fotos con etiqueta `antes`/`despues`) + tomas ANTES | nuevo `Editor/ChecksBugs065.R19z.cs` | — | M | V |
| T0.3 | Métricas ANTES: contraste del escudero (`#990q`), cobertura de fachada (994g en modo informe), tiempos de reacción E1-E5 | — | T0.2 | S | V |
| E1 | Anillo: API `Mostrar(obj, radioDeUso, cartel)`, distancia en planta, `Ocultar(subio)`, onda de 3 aros | `Operacion/AnilloDeSubida.cs` | — | M | E |
| E2 | Árbitro `ObjetivoDelAnillo` + `RadioDeAbordaje` + plaza corregida (+ heli opcional) | `OperacionDirector.cs`, `.Huida.cs`, `.Resistir.cs` | E1 | S | E |
| E3 | Umbral: `ZParaLaToma -8`, `ZFuegoDelJefe`, cruce por escuadra, recordatorio, salvaguarda 120, marca visible | `OperacionDirector.Jefe.cs`, nuevo `Operacion/UmbralDelBlindado.cs` | — | M | E |
| E4 | Orden de ataque al tanque al embarcar | `OperacionDirector.Huida.cs` | E2 | S | E |
| A1 | `ProgresoDeNiveles` + `H25.1` | nuevo `Core/ProgresoDeNiveles.cs` | — | S | A |
| A2 | Tutorial simple v2: `partial`, lista de 8, variantes, ayuda 8 s, clave nueva | `Tutorial/TutorialManager.cs`, nuevo `TutorialManager.Simple.cs`, `Core/LocTextos.cs` | — | L | A |
| A3 | Anillo y `[E]` en `entrar_tanque` | `TutorialManager.Simple.cs` (y paso completo) | A2, E1 | S | A |
| A4 | `canon_simple` con ola al sentarse | `TutorialManager.Simple.cs`, `TutorialManager.cs` (`Protecciones`) | A2 | M | A |
| A5 | Autoplayer: `rts`+`fps`, `canon_simple` | `Tutorial/TutorialAutoPlayer*.cs` | A2, A4 | M | A |
| A6 | Panel "NIVEL 1 COMPLETADO" + JUGAR NIVEL 2 | `Tutorial/VictoriaTutorial.cs`, `TutorialManager.cs` | A1 | S | A |
| A7 | Menú: COMPLETADO y SIGUIENTE | `Presentation/MainMenuController.cs` | A1 | S | A |
| A8 | Ajuste de checks viejos (`PasosSimples`) | `Editor/HeadlessTestRunner.Fases13a17.cs` | A2 | S | A |
| A9 | Operación: `Completar(2)` + fila de resultado | `OperacionDirector.Extraer.cs`, `Mision/EstadisticasDeMision.cs` | A1 | S | A (no choca con E: otro archivo parcial) |
| B1 | Reacción: señal `!`, enfriamientos, poda, contadores | `Operacion/ReaccionAlDano.cs` | — | S | B |
| B2 | Medición E1-E5 y corrección de lo que falle | `ReaccionAlDano.cs`, `Ai/AiBrain.Tactica.cs`, `Operacion/CazaDeEnemigos.cs` | B1, T0.3 | M | B |
| B3 | Pesado: marcador y barra sobre la cabeza | `Operacion/EnemigoPesado.cs`, `Presentation/EnemyAlertIndicatorView.cs` | — | S | B |
| B4 | Pesado: parpadeo del láser + ventana de recarga | `EnemigoPesado.cs` | B3 | S | B |
| B5 | Pesado: anuncio, pasos con sacudida, muerte | `EnemigoPesado.cs` | B4, D1 | M | B |
| D1 | Desintegración en cubos (máquina, tope, guardas, `FaseEn`) | `Presentation/RetiroDeEnemigosCaidos.cs` | — | M | D |
| D2 | Variantes pesado/escudero + tutorial incluido | `RetiroDeEnemigosCaidos.cs` | D1 | S | D |
| C1 | Escudo: placa oscura, marco, chevrones, ventana | `Operacion/EnemigoEscudero.cs` | — | S | C |
| C2 | Paletas A/B/C conmutables + medición + elección | `Actors/SoldierLook.cs` | C1, T0.3 | M | C |
| F1 | Infraestructura R19 (raíz expuesta, ayudantes `internal`, latch) + K1-K4 | `Presentation/AjustesDeNivelR17.cs`, nuevo `AjustesDeNivelR19.cs` | — | M | F |
| F2 | K5-K11 por cara | `AjustesDeNivelR19.cs` | F1 | M | F |
| F3 | Conductos R17 al piso + restaurar sombras en `SalaDespejadaEnRts` | `AjustesDeNivelR17.cs`, `Presentation/SalaDespejadaEnRts.cs` | F1 | S | F |
| G1 | Checks 994a, b, e, f, l | nuevo `Editor/ChecksBugs065.R19a.cs` | carril A | M | G-A |
| G2 | Checks 994c, d, i, j, o, p | nuevo `Editor/ChecksBugs065.R19b.cs` | B, C, D | M | G-B |
| G3 | Checks 994g, h, k, m, n | nuevo `Editor/ChecksBugs065.R19c.cs` | E, F | M | G-C |
| G4 | `RunPhase25` headless (H25.1-4) | `Editor/HeadlessTestRunner.Fase25.cs` (nuevo) | A1, D1, E3 | S | G |
| G5 | Tomas de build `r19` + lista blanca de búsquedas | nuevo `Core/CapturasDeBuild.Ronda19.cs`, `Core/CapturasDeBuild.Lista.cs`, `Editor/HeadlessTestRunner.BusquedasGlobales.cs` | todo | M | G |
| V1-V3 | Tres pasadas, build, atlas, `VALIDACION_R19.md`, guía del dueño | `Docs/RONDA_19/*` | todo | L | V |

**Paralelizable (edición):** los carriles A, B, C, D, E y F no comparten archivos entre sí, salvo los puntos de contacto ya ordenados: A3 espera a E1; B5 espera a D1; G1/G2/G3 van en tres archivos distintos justamente para no chocar. **Mismos archivos (no paralelizar):** A2-A6 (`TutorialManager*.cs`), B3-B5 (`EnemigoPesado.cs`), B1-B2 (`ReaccionAlDano.cs`), E2-E4 (`OperacionDirector.Huida.cs`), F1-F3 (`AjustesDeNivelR17.cs`), D1-D2.

**Orden recomendado (una sola mano):** T0 → E1 → A1, A2, A3, A4, A5, A6, A7, A8 (el punto 1 es el primero del pedido y A2 es la tarea más larga: no dejarla para el final) → E2, E3, E4 → B1, B2 → D1, D2 → B3, B4, B5 → C1, C2 → F1, F2, F3 → A9 → G4, G5 → V. Los checks G1-G3 se escriben con cada carril, no al final.

**Qué se corta si falta tiempo, en este orden:** anillo del helicóptero; K9-K11 de la fachada; B5; E4; paletas B y C (queda A sin comparar); reverso ámbar del escudo. No se cortan: A2-A6, E1-E3, D1, B1, C1, F1.

---

## 6. Plan de verificación

### 6.1 Tres pasadas (qué cambia en cada una)

| Pasada | Dónde y cómo | Qué busca | Qué se permite cambiar |
|---|---|---|---|
| **1. Por punto** | Editor en Play, atajos de prueba (`OperacionPrueba.Arrancar`, `SaltarPaso`), modo dios. Tras cada carril: compilar (R6), sus checks 994 y los de regresión de su zona. Al cerrar: `RunAll` headless. | que cada mecánica haga lo que dice su criterio | todo |
| **2. Integración y ajuste** | Editor en Play **sin atajos donde se pueda**: entrar a Huir por el camino real (hackeo → explota el muro → caminar a la línea), tutorial simple y completo con el autoplayer, `OperacionPrueba.Recorrer(8f)` por los 6 objetivos. Lista completa 994a-p con `aislar = true` (cada check en un Play fresco). Regresión: `#111`, `#243a`, `#291`, `#990a-q`, `#991a-c`, `#992a`, `#993a`, Wp4 (#085), `#501a-g`, `Fase19`, `Fases13a17`. Matriz de la sección 4. Medición de dificultad (bajas propias en `Arrancar(1)` y `(3)`, 3 corridas) y de rendimiento (`#990j`, `Bug994h`). | regresiones, cruces entre puntos, números mal elegidos | números y defaults (paleta, radios, tiempos, cantidad de piezas), correcciones; no se agregan funciones |
| **3. Build** | Unity cerrado, `CliBuilder.BuildWindows64` (el batch se cierra solo). `StrategicPoint.exe -capturas <dir> -screen-width 1600 -screen-height 900 -screen-fullscreen 0` con el grupo `r19`. Recorrido manual guiado por la guía del dueño. Repetir en el Editor los checks de lo que se toque. | lo que solo falla en la build (shaders, vigilantes que mueren al cargar desde el menú, `DontDestroyOnLoad`, fuentes) | solo bloqueos; cada arreglo vuelve a correr su check y `RunAll` |

Resultado: `Docs/RONDA_19/VALIDACION_R19.md` con una fila por punto (OK / PARCIAL / NO, check, número medido, captura) y la lista honesta de lo no verificado. Los fallos que ya existían en T0.1 se listan aparte y no se cuentan como regresión.

### 6.2 Capturas antes/después y atlas

- **ANTES primero.** `Bug994z` se escribe y se corre en T0.2, antes de tocar una sola mecánica, con `SessionState "R19Etiqueta" = "antes"`. Usa cámara propia a `RenderTexture` (patrón `R18bFoto`, `Editor/ChecksBugs065.R18b.cs:31-53`) con posiciones fijas; salida en `Assets/Validacion~/r19_<toma>_<etiqueta>.png`. Lo que todavía no existe se lee por reflexión y se anota "(n/a: antes)".
- **DESPUÉS** con la misma función y etiqueta `despues`, al cerrar la pasada 2.
- **Tomas (16):** `nivel1_panel`, `menu_niveles`, `pesado_marcador`, `pesado_recarga`, `tutorial_tanque_anillo`, `tutorial_canon`, `fachada_norte`, `fachada_sur`, `fachada_este`, `fachada_diagonal`, `escudero_frente_dia`, `escudero_frente_sala`, `muerte_tira` (3 cuadros: 2 s, 4,1 s, 4,5 s), `anillo_5m` (verde con radio real), `umbral_blindado` (desde z -20 mirando al tanque), `reaccion_senal`.
- **Movimiento sin video:** tiras de 3 cuadros (desintegración, onda del anillo) con `Time.timeScale = 0` entre tomas.
- **Trampas conocidas:** pesado fotografiado con `Brain.Pasivo = true` (si no, lo tapan sus explosiones); esperar 6 cuadros tras activar o convertir un enemigo; `capture_game_view` escribe relativo a `Assets/` (no usarlo); no abrir otra escena con la actual sucia.
- **Atlas:** script de PowerShell con `System.Drawing` (patrón `Docs/RONDA_17/hacer_atlas_4.ps1`): dos columnas ANTES / DESPUÉS por toma, etiqueta con el número medido y el check, salida `Docs/RONDA_19/ATLAS_R19_1_nivel_tutorial_tanque.jpg` y `ATLAS_R19_2_enemigos_fachada.jpg` (ancho 1300, calidad 88).
- **Prueba de build:** las tomas del grupo `r19` de `-capturas` van en una tercera hoja "en la build", solo DESPUÉS (la build anterior no trae ese grupo).

### 6.3 Lo que NO se puede verificar

- Teclado y mouse reales: `[E]`, mantener `[Q]`, `[2]`, `[F7]/[F8]`, clics en los botones del menú y del panel (se invoca `onClick` / los métodos; los flancos `wasPressedThisFrame` no llegan desde el CLI).
- Que "se vea bien": el contraste y la fachada se miden con números, pero el gusto del dueño no. La métrica de color no mide nitidez en movimiento.
- Animaciones y efectos en movimiento (no hay video): solo tiras de cuadros.
- Mezcla de audio y sensación de la sacudida de cámara.
- Dificultad y tasa de derrota de una partida completa jugada por una persona (solo muestras de 3 corridas por tramo con atajos).
- Rendimiento en la PC del dueño y en pantalla completa exclusiva.
- **La intención real de los puntos ambiguos (1, 2, 8).** Se resuelven con default y se dejan a la vista en la sección 9.

### 6.4 Guía para el dueño (entregable de 1 página)

`Docs/RONDA_19/DONDE_MIRAR.md`: por cada uno de los 9 puntos, 2 líneas: cómo llegar en menos de 1 minuto (nivel, objetivo, atajo de salto si existe) y qué debería ver. Es la respuesta directa a que estos 9 puntos ya se habían hecho una vez y volvieron.

---

## 7. Mejoras baratas incluidas (y dónde)

| Mejora | Dónde | Costo |
|---|---|---|
| Cartel "[E] SUBIR AL TANQUE" que aparecía en la plaza (bug) | E2 | 1 línea |
| El círculo del tanque marca el radio real (6,5 m, no 3,5) | E1-E2 | S |
| Destello/sonido de "subiste" que sonaba al cambiar de fase | E1 | S |
| Marcador de alerta del pesado dentro del cuerpo | B3 | S |
| Parpadeo del láser del pesado (pendiente de la R17) | B4 | S |
| Botón JUGAR NIVEL 2 al terminar el tutorial y marca en el menú | A6-A7 | S |
| Final del tutorial simple con cañón y enemigos | A4 | M |
| `[E]` para subir al tanque en el tutorial (igual que en la Operación) | A3 | S |
| Recordatorio en vez de arranque automático de la pelea del jefe | E3 | S |
| 2 conductos de la R17 que flotaban al demoler un condensador (`#501b`) | F3 | S |
| `SalaDespejadaEnRts` restaura el modo de sombra original | F3 | S |
| Poda del diccionario de víctimas de `ReaccionAlDano` | B1 | S |
| Página "dónde mirar" para el dueño | V | S |

Opcionales no incluidas por defecto: anillo en el helicóptero; unificar los tres radios de subida; mochila ámbar del escudero; vestir los patios (contenedores, garita) porque agrega cobertura y cambia el combate de la entrada.

---

## 8. Cambios respecto a v1

1. Se reconoce que los 9 puntos son los pedidos 7-15 de la R17 ya implementados: cada tarea es el **delta perceptible**, no una primera versión.
2. T8 deja de crear un sistema nuevo: **mueve el umbral de z -30 a z -8, lo hace visible**, baja el protagonismo del reloj (salvaguarda 45 → 120 s) y agrega la orden de ataque al tanque como complemento.
3. T7: se corrige el dato del radio (**6,5 m reales contra 3,5 dibujados**), se agrega un árbitro único, se descartan los círculos por asiento y se arregla el cartel del tanque en la plaza.
4. T4: parche de **runtime sobre la fachada R17 existente** en lugar de una capa de Editor nueva; kit con medidas, zonas libres, reglas de nombrado/apoyo y una **métrica de cobertura** por cara.
5. T5: se descarta el azul (código aliado); paleta con contraste interno elegida **por medición** entre 3 candidatas, con métrica de separación contra común, pesado y aliados.
6. T6: **desintegración en cubos a los 4 s** con máquina de estados y tope de pool, en vez de shader de disolución; se extiende al tutorial.
7. T3: el simple se rehace por gestos (sin radial obligatorio, `[E]`, final con cañón), 8 pasos, mensajes <= 110 caracteres; se protegen los checks que el v1 rompía (`SegundosParaAyuda == 16`, `>= 11`, claves huérfanas, progreso por índice).
8. T9: no hay "giro inmediato" (rompía al escudero); se agrega la **señal** que faltaba y una **medición con proyectil real** en 5 escenarios que decide qué más corregir.
9. T2: la iteración del pesado arranca por lo que la R17 dejó sin hacer (parpadeo, sacudidas, muerte) y por un defecto concreto (marcador dentro del cuerpo); se agrega la ventana de castigo en la recarga.
10. T1: definición de terminado medible (D1-D6), `ProgresoDeNiveles`, fila de resultado sin `Transform.Find`, sin glifo ✓.
11. Reglas transversales nuevas: presupuestos de búsquedas (38 / 144 / 0), estáticos, `dt`, materiales compartidos, un solo Editor.
12. Tareas atómicas con dependencias, carriles por archivo, orden y lista de recortes; checks repartidos en 3 archivos para poder trabajar en paralelo.
13. Verificación: las 3 pasadas tienen contenido distinto (por punto / integración sin atajos / build), las tomas ANTES se sacan antes de tocar código, y hay una guía para el dueño.
14. Dos bugs y tres defectos menores hallados durante la revisión quedan incluidos como mejoras baratas (sección 7).

---

## 9. Preguntas abiertas que igual resolví con un default

| # | Pregunta | Default tomado | Por qué | Costo de cambiar de opinión |
|---|---|---|---|---|
| 1 | "Nivel 1" ¿es el Tutorial (lo que el menú llama NIVEL 1) o la Operación Cuartel? | Tutorial, con la misma definición de terminado aplicada de forma acotada a la Operación (D6) | es lo que rotula la build; los otros 8 puntos ya pulen la Operación | bajo: `ProgresoDeNiveles` y la fila de resultado ya cubren el Nivel 2; faltaría una auditoría jugable completa |
| 2 | "Enemigo grande" ¿es el pesado o el BLINDADO GOLIAT? | Pesado | es el "gordo/grande" de los pedidos originales; el jefe recibe trabajo por el punto 8 | medio: el jefe necesitaría su propia iteración |
| 3 | "Trigger para empezar a atacar al tanque" ¿el jugador empieza la pelea con el blindado, o los enemigos empiezan a atacar el tanque del jugador? | Las dos: A (umbral visible) como principal, B (orden al embarcar + final del tutorial) como complemento barato | la ambigüedad es real y B cuesta una tarea S | nulo |
| 4 | ¿Dónde va la línea del blindado? | z = -8 (antes -30) | deja 32 m de aproximación viendo al tanque dormido, con cobertura a mano | trivial: una constante |
| 5 | ¿La pelea puede arrancar sola por tiempo? | Solo a los 120 s; antes, recordatorios cada 25 s | un arranque a los 45 s contradice "trigger" | trivial |
| 6 | ¿Se achica el radio de abordaje del tanque para que el círculo no sea tan grande? | No: queda en 6,5 m y el círculo lo muestra | no cambiar jugabilidad ni arriesgar checks | trivial (constante `RadioDeAbordaje`; probar 5,0 en la pasada 2 si la toma lo muestra desproporcionado) |
| 7 | ¿El tutorial simple baja a 8 pasos o solo se acortan los textos? | 8 pasos, sin radial obligatorio | ya se pidió "simple" dos veces con 11 pasos | medio: volver a 11 es restaurar `IdsSimples`; las variantes de texto sirven igual |
| 8 | ¿`[E]` para subir al tanque en el tutorial simple, cuando el completo enseña el radial? | Sí en el simple; el completo no cambia | la Operación usa `[E]` | trivial |
| 9 | ¿Los cadáveres desaparecen a los 4 s en vez de a los 14 s? | Sí (pesado 5,5 s) | una "animación de eliminación" que pasa a los 14 s no se ve | trivial: `SegundosAntesDeRetirar` |
| 10 | ¿Disolución por shader o cubos? | Cubos | estilo del juego, sin riesgo de build | medio: el shader sería una tarea M aparte con prueba en build |
| 11 | Paleta del escudero | "Antidisturbios" (carmesí, blanco, negro, marco rojo), salvo que la medición prefiera otra | contraste interno y familia de color enemiga | bajo: 3 candidatas ya conmutables |
| 12 | Fachada: ¿runtime o escena? | Runtime, extendiendo el parche de la R17 | escenas binarias, pipeline de fusión, diálogos que cuelgan el Editor | alto si se quiere "horneado": habría que rehacerlo como capa de Editor |
| 13 | ¿Se viste también el patio (contenedores, garita)? | No (solo vereda perimetral y bolardos) | agrega cobertura y cambia el combate de la entrada | bajo: son piezas del mismo kit |
| 14 | ¿La reacción al daño se extiende al tutorial? | No | el enemigo de práctica debe quedarse quieto; la ola del final ya ataca | bajo |
| 15 | ¿Anillo de subida también en el helicóptero? | Opcional, primero en la lista de recortes | no está pedido; es coherencia de lenguaje | trivial (una rama del árbitro) |
