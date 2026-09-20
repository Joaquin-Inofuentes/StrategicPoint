# Ronda 10 — Tutorial: 22 pasos nuevos (de 36 a 58)

El tutorial de `SC_Tutorial` tiene hoy **36 pasos** (`TutorialManager.cs`, 36 `pasos.Add(new Paso...)`).
Enseña bien todo lo que es "manejar el cuerpo" y "dar órdenes con el radial", y deja afuera tres
familias enteras del juego:

1. **La capa de sistema**: pausa, configuraciones, idioma, accesibilidad, dificultad, reasignación de
   teclas, gamepad, HUD mínimo, minimapa. Nada de eso se toca nunca en el tutorial.
2. **Lo que el tutorial cuenta pero no hace probar**: munición con reservas, arsenal, trepar,
   atropellar, retirada, postura de combate, suprimir, historial de órdenes, trazado de recorrido,
   cola de órdenes con Shift, seleccionar heridos, seleccionar por tipo.
3. **La misión de verdad**: rehén, helicóptero, zonas, fases, derrota y reintento. El tutorial termina
   en una meta dorada; nunca muestra cómo es una misión.

Estos 22 pasos cubren las tres. El criterio para elegirlos fue: **un paso por cada sistema del juego que
hoy no tiene forma de fallar visiblemente**. Al terminar, el recorrido del autoplayer
(`Tools/Qa/qa_total.bat`) pasa a ser una prueba de regresión de **todo** el juego, no de la mitad.

---

## Resumen

| # nuevo | Id | Título | Teclas | Qué sistema pone a prueba |
|---|---|---|---|---|
| 37 | `pausa` | PAUSAR Y SEGUIR | Esc | `PauseController`, `Time.timeScale`, foco de UI |
| 38 | `controles` | CONSULTAR LOS CONTROLES | H | `ControlsTable`, overlay sin pausar |
| 39 | `configuracion` | CONFIGURACIONES | Esc | `AjustesDeJuego`, `PlayerPrefs`, resolución, calidad |
| 40 | `idioma` | CAMBIAR DE IDIOMA | F12 | `Loc`, `LocTextos` (321 textos), refresco vivo |
| 41 | `accesibilidad` | ACCESIBILIDAD | Esc | Daltonismo, tamaño de interfaz, subtítulos |
| 42 | `hud_minimo` | HUD MÍNIMO | F10 | `HudPulido`, capas del HUD |
| 43 | `minimapa` | EL MINIMAPA | M L | `MinimapFollow`, `MinimapIcon`, tamaños fijos |
| 44 | `rebind` | REASIGNAR UNA TECLA | Esc | `KeyBindings`, `KeyRebindView`, persistencia |
| 45 | `gamepad` | MANDO (opcional) | — | `MandoFps`, ejes y botones |
| 46 | `municion` | MUNICIÓN Y RESERVAS | R 1 2 3 | `WeaponHolder` reservas, clic seco, cambio automático |
| 47 | `arsenal_caja` | CAMBIAR EL ARMA PRINCIPAL | E ← → | `CambiarArmaPrincipal`, `CajaDeSuministros` |
| 48 | `trepar` | TREPAR UN OBSTÁCULO | Espacio | `SoldierMotor.TryVault` (item 53) |
| 49 | `supresion` | TE ESTÁN SUPRIMIENDO | Ctrl | `RecibirSupresion`, `TickSupresion` |
| 50 | `postura` | POSTURA DE COMBATE (RADIAL) | Q | `CombatStance`, correa de Defensiva, Alto el fuego |
| 51 | `suprimir` | ORDENAR SUPRIMIR (RADIAL) | Q | `OrdenSuprimir`, `OrdenLanzarGranada` |
| 52 | `cola_ordenes` | ENCADENAR ÓRDENES | Tab Shift+RMB | `orderQueue`, `QueuedOrderCount`, marcadores numerados |
| 53 | `trazado` | TRAZAR UN RECORRIDO | Tab Ctrl+RMB | `TrazadoDeCamino`, arranque con Espacio |
| 54 | `seleccion_avanzada` | SELECCIÓN AVANZADA | Tab Ctrl+A | `SelectAll`, `SelectWoundedOnly`, `SelectSameTypeOnScreen` |
| 55 | `historial` | HISTORIAL DE ÓRDENES | Q | `OrderHistory`, cancelar la última |
| 56 | `atropellar` | ATROPELLAR CON EL TANQUE | W | `Atropello`, daño por embestida |
| 57 | `rehen` | RESCATAR AL REHÉN | E | `Rehen`, `MisionDirector`, escolta |
| 58 | `extraccion` | EXTRACCIÓN EN HELICÓPTERO | Q | `Helicoptero`, `CinematicaDeVictoria`, fin de misión |

El paso 36 actual (`victoria`) pasa a ser el 59.

---

## 1. Dónde se insertan

No van todos al final: se intercalan donde tienen sentido pedagógico. El orden final queda:

```
 1 camara              15 arsenal_caja  (NUEVO)      31 postura         (NUEVO)      45 torreta
 2 wasd                16 vista_tactica               32 suprimir        (NUEVO)      46 mira_tanque
 3 correr              17 supresion     (NUEVO)       33 cubrirse                     47 avanzar_disparar
 4 saltar              18 rts                         34 curar                        48 atropellar      (NUEVO)
 5 trepar   (NUEVO)    19 cola_ordenes  (NUEVO)       35 reanimar                     49 final
 6 disparar            20 trazado       (NUEVO)       36 curarme                      50 rehen           (NUEVO)
 7 municion (NUEVO)    21 seleccion_av  (NUEVO)       37 demoler                      51 extraccion      (NUEVO)
 8 mira                22 fps                         38 bomba_aliado                 52 bajar_tanque
 9 arsenal             23 seguir                      39 torreta_fija                 53 pausa           (NUEVO)
10 cuchillo            24 seleccionar                 40 modo_dios                    54 controles       (NUEVO)
11 granada             25 mover_fps                   41 minimapa       (NUEVO)       55 configuracion   (NUEVO)
12 cambiar             26 ir_atacar                   42 hud_minimo     (NUEVO)       56 idioma          (NUEVO)
13 agacharse           27 formaciones                 43 entrar_tanque                57 accesibilidad   (NUEVO)
14 suministros         28 historial     (NUEVO)       44 aliados_tanque               58 rebind          (NUEVO)
                       29 gamepad       (NUEVO)                                       59 victoria
                       30 —
```

La capa de sistema (pausa, controles, configuraciones, idioma, accesibilidad, rebind) va **al final, con
la partida ya ganada**: son pantallas modales y ponerlas en el medio corta el ritmo. La única que va
temprano es `municion`, porque sin ella el jugador se queda sin balas en el paso 6 y no sabe por qué.

---

## 2. Los 22 pasos, uno por uno

Formato: **sub-pasos** con su **detección** (qué mira `Evaluar`) y su **bandera** nueva en
`TutorialFlags`.

---

### 37 · `pausa` — PAUSAR Y SEGUIR · `[Esc]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| Abrí la pausa con `[Esc]` | `PauseRef.IsPaused` | `pausaAbierta` |
| Mirá que el mundo esté congelado | `Time.timeScale == 0f` sostenido 1 s | `mundoCongelado` |
| Seguí jugando con CONTINUAR | `!PauseRef.IsPaused && pausaAbierta` | `pausaCerrada` |

**Por qué importa:** `PauseController` pone `Time.timeScale = 0` y `PlayerInputDriver.Update` corta con
`if (PauseRef.IsPaused) return;` (línea 640). Es el único punto del juego donde la entrada se congela
sin congelar el `Update`, y nunca se prueba. Además detecta el bug clásico de "el clic de CONTINUAR se
lo come el re-bloqueo del cursor" (`UpdateCursorLock:1063`), que ya se arregló una vez.

**En el mundo:** un cartel (`TutorialBeacon`) sobre el jugador: "PROBÁ LA PAUSA".
**Autoplayer:** `EntradaVirtual.Tecla(Key.Escape)` → esperar `IsPaused` → `ClicEn(botonContinuar)`.
Necesita que `TutorialAutoPlayer.Entrada` sepa clickear un `Button` del canvas de pausa: agregar
`ClicEnBoton(string nombre)` que busque el botón por nombre y llame `onClick.Invoke()`.

---

### 38 · `controles` — CONSULTAR LOS CONTROLES · `[H]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| Abrí la tabla de controles con `[H]` | `PauseRef.IsControlsOverlayOpen` | `controlesAbiertos` |
| Buscá una tecla que no conocías | `ControlsTable.FilasVisibles >= 20` (propiedad nueva) | `controlesLeidos` |
| Cerrala con `[H]` | `!IsControlsOverlayOpen && controlesAbiertos` | `controlesCerrados` |

**Por qué importa:** el overlay de controles **no pausa** (sigue la simulación) pero sí congela la
entrada (`PlayerInputDriver.cs:669`). Es una combinación que no existe en ningún otro lado y no se
prueba. Además el ítem 7 de la auditoría anterior ("Controles omite T, X, Y, B, K, J, N, M, L y
Espacio") se verificó a mano: este paso lo convierte en una aserción.

**Aserción extra del paso:** `Check` interno que compara la cantidad de filas de `ControlsTable` contra
`KeyBindings.Todas.Count`, para que agregar una acción sin documentarla haga fallar el tutorial.

---

### 39 · `configuracion` — CONFIGURACIONES · `[Esc]` → CONFIGURACIONES

| Sub-paso | Detección | Bandera |
|---|---|---|
| Entrá a CONFIGURACIONES desde la pausa | `AjustesDeJuego.PanelAbierto` (nueva) | `ajustesAbiertos` |
| Bajá la sensibilidad del mouse y volvé a subirla | `driver.LookSensitivity` cambió y volvió | `sensibilidadProbada` |
| Cambiá la resolución o la calidad | `Screen.currentResolution` o `QualitySettings.GetQualityLevel()` cambió | `calidadCambiada` |
| Salí: el cambio quedó guardado | `PlayerPrefs.HasKey("sp_sens")` y panel cerrado | `ajustesGuardados` |

**Por qué importa:** el ítem 25 ("no hay opciones de resolución, pantalla completa ni calidad") se marcó
HECHO sin prueba automática. Este paso la da. También cubre que el slider de sensibilidad de verdad
afecte al juego (ítem que ya fue un bug: "sólo se veía, no afectaba nada").

---

### 40 · `idioma` — CAMBIAR DE IDIOMA · `[F12]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| Pasá a inglés con `[F12]` | `Loc.Idioma == Idioma.En` | `idiomaEn` |
| Mirá que el propio tutorial cambió de idioma | el título del paso actual está en inglés (`Loc.T(...)` devuelve la clave en `En`) | `tutorialTraducido` |
| Volvé a español | `Loc.Idioma == Idioma.Es` | `idiomaEs` |

**Por qué importa:** el ítem 27 dice que la localización cubre "el tutorial completo (títulos, pasos,
pistas)" y la misión, con 321 textos en `LocTextos.cs` y "una prueba que evita que la tabla quede
desfasada del código". Esa prueba es estática (tabla vs. código); esta es dinámica: comprueba que el
cambio **en vivo**, con el HUD ya dibujado, refresca los textos (`Loc.cs:161` barre los `Text` de la
escena — ver bug 97).

**Aserción extra:** al entrar en inglés, contar los `Text` visibles cuyo contenido sigue siendo la
cadena en español de `LocTextos`; tiene que ser 0 salvo la lista blanca documentada (contadores,
distancias y la tabla larga de controles).

---

### 41 · `accesibilidad` — ACCESIBILIDAD · `[Esc]` → CONFIGURACIONES

| Sub-paso | Detección | Bandera |
|---|---|---|
| Activá el modo daltonismo | `AjustesDeJuego.Daltonismo` | `daltonismoOn` |
| Subí el tamaño de interfaz a 150 % | `AjustesDeJuego.EscalaUi >= 1.5f` | `interfazGrande` |
| Activá los subtítulos de sonido | `Subtitulos.Activos` | `subtitulosOn` |
| Dispará y leé el subtítulo | `Subtitulos.Emitidos > 0` | `subtituloLeido` |
| Devolvé todo a como estaba | las tres vuelven a su valor inicial | `accesibilidadRestaurada` |

**Por qué importa:** el ítem 28 se marcó HECHO con "probado en la FASE 19", que es una prueba headless.
Esto lo prueba **en pantalla**, que es donde vive el problema: con el 150 % activado, las capturas del
paso revelan cualquier texto que se corte o se superponga — que es exactamente la clase de bug que
llenó la auditoría original (ítems 1 a 20).

**Capturas:** este paso saca 3 capturas, no 1: normal, daltonismo, 150 %.

---

### 42 · `hud_minimo` — HUD MÍNIMO · `[F10]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| Apagá el HUD con `[F10]` | `HudPulido.Minimo` | `hudMinimoOn` |
| Mirá que la mira y la vida siguen | `AimUiRef.Visible && rosterVisible` | `hudMinimoRevisado` |
| Volvé al HUD completo | `!HudPulido.Minimo` | `hudMinimoOff` |

**Por qué importa:** el ítem 18 ("el HUD está saturado y no hay modo mínimo"). El modo existe; nadie
comprueba **qué queda** encendido. Este paso fija el contrato: en HUD mínimo tienen que sobrevivir la
mira y la vida, y tiene que desaparecer todo lo demás.

---

### 43 · `minimapa` — EL MINIMAPA · `[M]` `[L]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| Agrandá el minimapa con `[M]` | `MinimapRef.Agrandado` | `minimapaGrande` |
| Ciclá los 3 tamaños con `[L]` | `MinimapRef.IndiceDeTamano` pasó por 0, 1 y 2 | `minimapaCiclado` |
| Encontrá al enemigo en el minimapa | hay un `MinimapIcon` rojo dentro del rect | `enemigoEnMinimapa` |
| Volvé al tamaño original | `!MinimapRef.Agrandado` | `minimapaNormal` |

**Por qué importa:** ítems 12, D2 y D3. Y `MinimapIcon.RegistrarObstaculos` hace un
`FindObjectsByType<ObstacleMarker>` (bug 97): este paso es el que hace visible si eso se vuelve lento.

---

### 44 · `rebind` — REASIGNAR UNA TECLA · `[Esc]` → CONTROLES

| Sub-paso | Detección | Bandera |
|---|---|---|
| Abrí la reasignación de teclas | `KeyRebindView.Abierto` | `rebindAbierto` |
| Cambiá "agacharse" de `Ctrl` a `Z` | `KeyBindings.Get(KeyBindings.Agacharse) == Key.Z` | `teclaReasignada` |
| Probala: agachate con `[Z]` | `Motor.IsCrouching` con `Z` apretada | `teclaReasignadaProbada` |
| Devolvela a `Ctrl` | vuelve al default | `teclaRestaurada` |

**Por qué importa:** `KeyRebindView` existe y no se prueba nunca. Y este paso es el que revela el bug 76
(`F4` y `P` cableadas a teclas fijas): el jugador intenta reasignar modo dios y no aparece en la lista.

---

### 45 · `gamepad` — MANDO (opcional) · sin teclas

| Sub-paso | Detección | Bandera |
|---|---|---|
| Si hay un mando conectado, movete con el stick izquierdo | `MandoFps.Mover.sqrMagnitude > 0.1f` | `mandoMovio` |
| Mirá con el stick derecho | `MandoFps.Mirar.sqrMagnitude > 0.1f` | `mandoMiro` |
| Dispará con el gatillo | `MandoFps.Disparar` | `mandoDisparo` |

**Paso salteable:** si `Gamepad.current == null`, el paso se auto-completa en 2 s con el mensaje
"No hay mando conectado: se saltea" y queda marcado como OMITIDO en el log (no como fallido). Es el
único paso del tutorial con esa semántica, y hay que agregarla: `Paso.Opcional` + `Paso.DebeCorrer`
(`Func<bool>`).

**Por qué importa:** el ítem 26 ("no hay soporte de gamepad") se marcó HECHO y nunca se ejercita.

---

### 46 · `municion` — MUNICIÓN Y RESERVAS · `[R]` `[1]` `[2]` `[3]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| Vaciá el cargador disparando | `Weapon.CurrentAmmo == 0` | `cargadorVacio` |
| Escuchá el clic seco | `Weapon.SinMunicionTotal` **o** se emitió `SfxKind.EmptyClick` | `clicSeco` |
| Recargá con `[R]` y mirá bajar la reserva | `Weapon.ReservaActual` bajó | `reservaBajo` |
| Agotá la reserva del arma | `Weapon.SinMunicionTotal` | `reservaAgotada` |
| El juego cambia solo al arma que sí tiene | `Weapon.CurrentWeaponKind` cambió por `CambiarASiguienteConMunicion` | `armaCambiadaSola` |

**Por qué importa:** las reservas (`WeaponHolder.CargadoresDeReserva`, `UsaReservas`,
`SinMunicionTotal`, `CambiarASiguienteConMunicion`) son un subsistema entero que el tutorial no toca —
y donde vive el bug 14 (cambiar de arma recarga gratis). Este paso lo detecta de frente: el sub-paso
"agotá la reserva" es **imposible de completar** con el bug 14 vivo, porque el jugador puede rellenar
indefinidamente apretando `1`, `2`, `1`.

**Requisito de escena:** `WeaponHolder.ReservasActivas = true` durante este paso (hoy el tutorial corre
con munición ilimitada). `AlEntrar` la enciende, `AlSalir` la devuelve a `false`.

---

### 47 · `arsenal_caja` — CAMBIAR EL ARMA PRINCIPAL · `[E]` `[←]` `[→]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| Acercate a la caja de suministros | distancia ≤ `CajaDeSuministros.Alcance` | `cercaDeLaCaja` |
| Abrí el arsenal con `[E]` | `CajaDeSuministros.ArsenalAbierto` | `arsenalAbierto` |
| Cambiá el arma de la ranura 1 | `Weapon.Loadout[0]` cambió | `armaPrincipalCambiada` |
| Probala: dispará 3 tiros | 3 `ShotFiredEvent` con el `WeaponKind` nuevo | `armaNuevaProbada` |
| Reponé munición y granadas | `Weapon.MunicionCompleta() && Weapon.Granadas == 3` | `suministrosCompletos` |

**Por qué importa:** `CambiarArmaPrincipal` (ítem 43) y `ReponerMunicion` no se prueban. Y este paso
hace visible el bug 18 (el arsenal elegido no sobrevive un domain reload) y el 19 (hereda el cargador
del arma anterior).

---

### 48 · `trepar` — TREPAR UN OBSTÁCULO · `[Espacio]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| Caminá hasta el cajón bajo | distancia ≤ 1,5 m del cajón de práctica | `frenteAlCajon` |
| Apretá `[Espacio]` pegado al cajón: lo trepás | `Motor.Vaulting` | `trepando` |
| Quedaste arriba | posición `y` > la base del cajón + 0,4 | `arribaDelCajon` |
| Probá saltar contra el muro alto: no se trepa | `Motor.UltimoMotivoDeTrepa == "6"` (alto fuera de rango) | `muroNoTrepable` |

**Por qué importa:** el ítem 53 (trepar obstáculos bajos) está implementado con siete motivos de
rechazo distintos (`UltimoMotivoDeTrepa` va de `"1"` a `"7"`) y **cero** cobertura: `SoldierMotor.Update`
no corre en la suite (bug 51). Este paso es la única forma actual de ejercitarlo, y el sub-paso 4
verifica que el rechazo también funcione.

**Requisito de escena:** un cajón de 0,9 m de alto (dentro de `[0.5, 1.3]`) y el muro de práctica de
2 m que ya existe.

---

### 49 · `supresion` — TE ESTÁN SUPRIMIENDO · `[Ctrl]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| Un enemigo abre fuego cerca tuyo | balas pasando a < 2 m (`RadioDeSupresion`) | `balasCerca` |
| Mirá cómo tu aliado se agacha solo | un aliado con `Brain.Suprimido` | `aliadoSuprimido` |
| Escuchá el aviso de radio | `AlertQueue` emitió "NOS DISPARAN! A CUBIERTO!" | `avisoDeSupresion` |
| Cubrite vos también y esperá a que pare | `Motor.IsCrouching` sostenido 2 s | `teCubriste` |

**Por qué importa:** el ítem 63 (fuego de supresión) sólo existe en Play (bug 27) y usa `Time.time`
(bug 40). Sin este paso, un cambio que lo rompa no da ninguna señal.

**Requisito de escena:** un enemigo en una posición fija que abra fuego contra el aliado, con
`CombatStance.AltoElFuego` en el jugador para que no lo mate antes de tiempo.

---

### 50 · `postura` — POSTURA DE COMBATE (RADIAL) · `[Q]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| `[Q]` → POSICIÓN → DEFENSIVA | los 2 aliados en `CombatStance.Defensiva` | `posturaDefensiva` |
| Mirá cómo no persiguen más allá de su puesto | un aliado en `Chase` que frena a ≤ `defensiveLeashRadius` de `HomePosition` | `correaRespetada` |
| `[Q]` → POSICIÓN → ALTO EL FUEGO | los 2 en `CombatStance.AltoElFuego` | `posturaAltoElFuego` |
| Mirá que encaran pero no disparan | 3 s sin `ShotFiredEvent` de ellos, con enemigo a la vista | `altoElFuegoRespetado` |
| Volvé a LIBRE | los 2 en `CombatStance.Libre` | `posturaLibre` |

**Por qué importa:** `CombatStance` (ítem 212) tiene tres posturas, una correa, un
`StanceVisionMultiplier`, un `StanceAllowsPursuit` y un `StanceAllowsFire` — y el tutorial no la
menciona. Es la función táctica más profunda del juego y el jugador no se entera de que existe.

---

### 51 · `suprimir` — ORDENAR SUPRIMIR (RADIAL) · `[Q]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| Apuntá al parapeto enemigo y `[Q]` → ATACAR → SUPRIMIR | un aliado con `Brain.SuprimiendoPorOrden` | `ordenDeSuprimir` |
| Mirá cómo dispara al punto aunque no vea a nadie | ≥ 5 `ShotFiredEvent` de ese aliado en 3 s | `supresionEnCurso` |
| `[Q]` → ATACAR → GRANADA AHÍ | `AiBrain.GranadasLanzadasPorIA` subió | `ordenDeGranadaIA` |
| El enemigo sale de la cobertura | `Brain.HuyendoDeGranada` en el enemigo | `enemigoHuyendo` |

**Por qué importa:** `OrdenSuprimir` y `OrdenLanzarGranada` (ítem 61) están implementadas, expuestas en
el radial y sin probar. Y este paso es el que revela el bug 41 (la supresión ordenada se salta el gate
de fuego amigo): el sub-paso 1 hay que hacerlo con un aliado **entre** el que suprime y el punto.

---

### 52 · `cola_ordenes` — ENCADENAR ÓRDENES · `[Tab]` `[Shift]`+`[Clic der.]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| Pasá a RTS y seleccioná a los 2 | `Rig.Mode == Rts && Selection.Selected.Count == 2` | `seleccionParaCola` |
| Shift + clic derecho en 3 puntos distintos | `Brain.QueuedOrderCount >= 2` en un aliado | `ordenesEncoladas` |
| Mirá los marcadores numerados en el piso | 3 `OrderMarkerFx` con índice 0, 1 y 2 | `marcadoresNumerados` |
| Esperá a que recorra los 3 | `QueuedOrderCount == 0` y llegó al último | `colaCumplida` |

**Por qué importa:** la cola de órdenes (`orderQueue`, `QueuedDestinations`, el índice del marcador) es
una función completa que el tutorial no nombra. Y ejercita el camino donde `OrderCompletedEvent` tiene
que dispararse 3 veces.

---

### 53 · `trazado` — TRAZAR UN RECORRIDO · `[Tab]` `[Ctrl]`+`[Clic der.]` `[Espacio]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| Ctrl + clic derecho: marcá 3 puntos | `TrazadoDeCamino.Cantidad == 3` | `puntosMarcados` |
| Mirá la línea azul de la ruta | `PathPreview.Instance.Visible` | `rutaPrevisualizada` |
| Arrancá el recorrido con `[Espacio]` | `TrazadoDeCamino.EnCurso` | `recorridoArrancado` |
| Mirá cómo rodea el muro en vez de chocarlo | el aliado pasa por un punto a > 2 m del muro | `rodeoVisible` |

**Por qué importa:** `TrazadoDeCamino` + `PathPreview` + `NavService` es la cadena completa de
navegación, y el sub-paso 4 es **la prueba visual del bug 57** (el A* apagado fuera de Play) y del
bug 35 (detección de atasco muerta): si cualquiera de los dos vuelve, el aliado se clava contra el muro
y el paso no se completa.

---

### 54 · `seleccion_avanzada` — SELECCIÓN AVANZADA · `[Tab]` `[Ctrl+A]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| Ctrl+A: toda la escuadra | `Selection.Selected.Count == escuadraViva` | `seleccionTotal` |
| Seleccioná sólo a los heridos | `SelectWoundedOnly()` devolvió true y la selección son los heridos | `seleccionHeridos` |
| Seleccioná a los de tu misma clase en pantalla | `SelectSameTypeOnScreen` dejó ≥ 2 del mismo `Role` | `seleccionPorTipo` |
| Reagrupalos | `RegroupSelection` devolvió puntos y la dispersión bajó | `escuadraReagrupada` |

**Por qué importa:** `SelectWoundedOnly` (ítem 220), `SelectSameTypeOnScreen` (ítem 214) y
`RegroupSelection` son tres funciones públicas, con pruebas unitarias de su matemática
(`IsWounded`, `SpreadOf`) y **sin ningún camino de entrada probado**. El sub-paso 4 verifica el
criterio objetivo que el propio código documenta: `SpreadOf(antes) > SpreadOf(después)`.

---

### 55 · `historial` — HISTORIAL DE ÓRDENES · `[Q]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| Dá 3 órdenes cualesquiera | `OrderHistory.Cantidad >= 3` | `historialLleno` |
| Abrí el historial | panel visible | `historialAbierto` |
| Cancelá la última orden | `Brain.State == Patrol` en quien la tenía | `ordenCancelada` |

**Por qué importa:** `OrderHistory` (ítem 221) registra una entrada por lote y nadie lo mira. Este paso
además verifica la granularidad correcta: 3 órdenes a 2 soldados tienen que dar **3** entradas, no 6
(es exactamente lo que el comentario de `AnnounceBatch` promete) — y es lo que rompe el bug 84
(`Shift+T` no pasa por `AnnounceBatch`, así que no entra en el historial).

---

### 56 · `atropellar` — ATROPELLAR CON EL TANQUE · `[W]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| Sentate al volante y acelerá | `currentSeat == Driver && VehicleMotor.Velocidad > 4` | `tanqueAcelerando` |
| Atropellá al enemigo de la calle | `Atropello.Atropellados > 0` | `enemigoAtropellado` |
| Derribá la barricada de madera embistiéndola | el `ObstacleMarker` colapsó por embestida | `barricadaEmbestida` |

**Por qué importa:** `Atropello.cs` (108 líneas) no aparece en ningún paso ni en ninguna fase. Es daño
por contacto con un umbral de velocidad: sin prueba, cualquier cambio en `VehicleMotor` lo desactiva en
silencio.

---

### 57 · `rehen` — RESCATAR AL REHÉN · `[E]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| Encontrá al rehén (baliza blanca) | distancia ≤ 6 m | `rehenEncontrado` |
| Eliminá a sus dos guardias | los 2 guardias muertos | `guardiasEliminados` |
| Liberalo con `[E]` | `Rehen.Liberado` | `rehenLiberado` |
| Escoltalo hasta la zona segura | el rehén dentro de la zona | `rehenEscoltado` |
| No dejes que lo maten | el rehén sigue vivo al llegar | `rehenVivo` |

**Por qué importa:** `Rehen.cs` y la lógica de escolta del `MisionDirector` son el objetivo central de
la misión real, y el tutorial nunca muestra cómo se juega una misión. Un jugador que termina el
tutorial de hoy no sabe qué se le va a pedir. Además el rehén es `RoleType.Civilian`, o sea que
ejercita todas las ramas `Role != RoleType.Civilian` que hay repartidas por `OrderService`,
`AiBrain.Pasivo`, `Dificultad.AjustarVida` y `RescateAutomatico.BuscarRescatistaLibre`.

**Es el paso que puede fallar de más formas**, y eso es exactamente lo que se busca de él.

---

### 58 · `extraccion` — EXTRACCIÓN EN HELICÓPTERO · `[Q]`

| Sub-paso | Detección | Bandera |
|---|---|---|
| Pedí la extracción desde el radial | `Helicoptero.Llamado` | `extraccionPedida` |
| Defendé la zona hasta que llegue | el helicóptero aterrizó y nadie de la escuadra murió | `zonaDefendida` |
| Subí con el rehén y la escuadra | todos a bordo | `todosEnElHelicoptero` |
| Mirá la cinemática de victoria | `CinematicaDeVictoria` terminó | `cinematicaVista` |

**Por qué importa:** `Helicoptero.cs` y `CinematicaDeVictoria.cs` (13 KB) son el final de la misión y
nunca se corren en ninguna prueba. `CinematicaDeVictoria` hace tres `FindObjectsByType<Canvas>`
(bug 97) y toca `Time.timeScale`: es el sitio más probable de que algo se rompa y el menos observado.

---

## 3. Qué hay que tocar para implementarlos

### 3.1 `TutorialFlags.cs` — 78 banderas nuevas
```csharp
// Ronda 10 — capa de sistema
public bool pausaAbierta, mundoCongelado, pausaCerrada;
public bool controlesAbiertos, controlesLeidos, controlesCerrados;
public bool ajustesAbiertos, sensibilidadProbada, calidadCambiada, ajustesGuardados;
public bool idiomaEn, tutorialTraducido, idiomaEs;
public bool daltonismoOn, interfazGrande, subtitulosOn, subtituloLeido, accesibilidadRestaurada;
public bool hudMinimoOn, hudMinimoRevisado, hudMinimoOff;
public bool minimapaGrande, minimapaCiclado, enemigoEnMinimapa, minimapaNormal;
public bool rebindAbierto, teclaReasignada, teclaReasignadaProbada, teclaRestaurada;
public bool mandoMovio, mandoMiro, mandoDisparo;
// Ronda 10 — combate y equipo
public bool cargadorVacio, clicSeco, reservaBajo, reservaAgotada, armaCambiadaSola;
public bool cercaDeLaCaja, arsenalAbierto, armaPrincipalCambiada, armaNuevaProbada, suministrosCompletos;
public bool frenteAlCajon, trepando, arribaDelCajon, muroNoTrepable;
public bool balasCerca, aliadoSuprimido, avisoDeSupresion, teCubriste;
// Ronda 10 — mando tactico
public bool posturaDefensiva, correaRespetada, posturaAltoElFuego, altoElFuegoRespetado, posturaLibre;
public bool ordenDeSuprimir, supresionEnCurso, ordenDeGranadaIA, enemigoHuyendo;
public bool seleccionParaCola, ordenesEncoladas, marcadoresNumerados, colaCumplida;
public bool puntosMarcados, rutaPrevisualizada, recorridoArrancado, rodeoVisible;
public bool seleccionTotal, seleccionHeridos, seleccionPorTipo, escuadraReagrupada;
public bool historialLleno, historialAbierto, ordenCancelada;
// Ronda 10 — mision
public bool tanqueAcelerando, enemigoAtropellado, barricadaEmbestida;
public bool rehenEncontrado, guardiasEliminados, rehenLiberado, rehenEscoltado, rehenVivo;
public bool extraccionPedida, zonaDefendida, todosEnElHelicoptero, cinematicaVista;
```

### 3.2 `TutorialManager.Paso` — dos campos nuevos
```csharp
public bool Opcional;                 // no cuenta como fallido si se saltea
public Func<bool> DebeCorrer;         // null = siempre; false = se saltea con motivo
public string MotivoDeSalteo;         // "no hay mando conectado", etc.
```
`EmpezarPaso(i)` comprueba `DebeCorrer` y, si da false, loguea `OMITIDO` y avanza. `TutorialLog` gana
un tercer estado además de COMPLETADO y FALLIDO.

### 3.3 Propiedades nuevas que hay que exponer

Ninguna de estas es lógica nueva: son ventanas de sólo lectura sobre estado que ya existe, para que el
tutorial pueda **detectar** en vez de adivinar.

| Clase | Propiedad nueva |
|---|---|
| `PauseController` | `bool IsControlsOverlayOpen` (ya existe), `Button BotonContinuar` |
| `ControlsTable` | `int FilasVisibles` |
| `AjustesDeJuego` | `bool PanelAbierto`, `bool Daltonismo`, `float EscalaUi` |
| `KeyRebindView` | `bool Abierto` |
| `MinimapFollow` | `bool Agrandado`, `int IndiceDeTamano` |
| `HudPulido` | `bool Minimo` |
| `CajaDeSuministros` | `bool ArsenalAbierto`, `float Alcance` |
| `SoldierMotor` | `string UltimoMotivoDeTrepa` (ya existe) |
| `Atropello` | `int Atropellados` |
| `Rehen` | `bool Liberado`, `bool EnZonaSegura` |
| `Helicoptero` | `bool Llamado`, `bool Aterrizado`, `bool Despego` |
| `TrazadoDeCamino` | `bool EnCurso` (ya existe `Cantidad`) |
| `PathPreview` | `bool Visible` |
| `OrderHistory` | `int Cantidad`, `Entrada Ultima` |
| `VehicleMotor` | `float Velocidad` |
| `AiBrain` | `int RecalculosPorAtasco` (también la pide el plan, bug 35) |

### 3.4 `TutorialSceneBuilder.cs` — 7 objetos nuevos en `SC_Tutorial`

| Objeto | Para qué paso | Detalle |
|---|---|---|
| `Cajon_Trepable` | 48 | Cubo de 0,9 m de alto, dentro del rango `[0.5, 1.3]` de `TryVault` |
| `Enemigo_Supresor` | 49 | Enemigo fijo, `CombatStance.Defensiva`, con munición infinita, que dispara al aliado |
| `Parapeto_Enemigo` | 51 | Cobertura con 2 enemigos detrás, para la orden de suprimir |
| `Barricada_Embestible` | 56 | `ObstacleMarker` de madera en la calle del tanque |
| `Rehen` + `Guardia_A` + `Guardia_B` | 57 | El rehén con `RoleType.Civilian` y sus dos guardias |
| `ZonaSegura` | 57 | Trigger de llegada para la escolta |
| `ZonaDeExtraccion` + `Helicoptero` | 58 | Punto de aterrizaje y el helicóptero |

El constructor ya es idempotente (`Strategic Point > Tutorial > Construir escena SC_Tutorial`): los 7
se agregan con el mismo patrón que los existentes.

### 3.5 `TutorialAutoPlayer` — 6 acciones virtuales nuevas

`TutorialAutoPlayer.Entrada.cs` sabe hoy apretar teclas, mover el mouse y clickear en el mundo. Le
faltan:

| Acción | Firma propuesta | La usan los pasos |
|---|---|---|
| Clickear un botón del canvas | `void ClicEnBoton(string nombre)` | 37, 39, 41, 44 |
| Mover un slider | `void MoverSlider(string nombre, float valor01)` | 39, 41 |
| Activar un toggle | `void AlternarToggle(string nombre)` | 41, 42 |
| Elegir de un desplegable | `void ElegirEnDropdown(string nombre, int indice)` | 39 |
| Esperar a que un panel esté visible | `IEnumerator EsperarPanel(string nombre, float timeout)` | 37–44 |
| Simular un mando | `void MandoVirtual(Vector2 mover, Vector2 mirar, bool disparar)` | 45 |

Los cuatro primeros se resuelven con un único helper que busca el `GameObject` por nombre bajo el
canvas raíz e invoca el componente de UI correspondiente. Es el mismo patrón que
`TutorialAutoPlayer.Radial.cs` usa para el radial.

---

## 4. Cuánto crece el recorrido

| | Hoy | Con los 22 nuevos |
|---|---|---|
| Pasos | 36 | **59** (incluida la victoria) |
| Sub-pasos | ~95 | **~180** |
| Banderas | 62 | **140** |
| Duración estimada de una corrida completa del autoplayer | ~11 min | **~19 min** |
| Capturas por corrida | 36 | **61** (los pasos 41 y 57 sacan 3 cada uno) |
| Sistemas del juego con al menos un paso | 14 de 24 | **24 de 24** |

Los 10 sistemas que pasan de 0 a cubiertos: pausa, configuraciones, localización, accesibilidad,
minimapa, reasignación de teclas, gamepad, reservas de munición, trepar, y la misión completa
(rehén + helicóptero + cinemática).

---

## 5. Orden de implementación sugerido

1. **Infraestructura primero** (§3.2 y §3.5): `Paso.Opcional`/`DebeCorrer` y las 6 acciones virtuales
   del autoplayer. Sin esto, la mitad de los pasos no se pueden recorrer solos y el `.bat` no sirve.
2. **Las propiedades de lectura** (§3.3): 16 propiedades, todas triviales, todas necesarias para que la
   detección no sea por adivinanza.
3. **Los 7 objetos de escena** (§3.4), con el constructor idempotente.
4. **Los pasos, en tres tandas**, cada una cerrada con una corrida completa del autoplayer:
   - Tanda A (los que no necesitan escena nueva): 37–47, 50, 52, 53, 54, 55 — 16 pasos.
   - Tanda B (los que sí): 48, 49, 51, 56 — 4 pasos.
   - Tanda C (la misión): 57, 58 — 2 pasos, los más largos y los más valiosos.

Cada tanda tiene que dejar el autoplayer en **0 pasos fallidos** antes de empezar la siguiente.
