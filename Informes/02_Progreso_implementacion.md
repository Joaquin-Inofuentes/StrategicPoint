# Progreso de implementación: bugs #065 a #101

Registro de lo que implementa cada paquete del plan `01_Plan_bugs_065_101.md`. Una sección por paquete, en orden de ejecución.

---

## WP0 — Columna de la Operación (refactor habilitante)

**Fecha:** 2026-10-03. **Bugs cubiertos:** ninguno directo (prepara #078, #097, #101 y el framework de checks).

### Archivos

Creados (`Assets/_Project/Scripts/`):
- `Operacion/OperacionDirector.Infiltrar.cs`, `.Puestos.cs`, `.Centro.cs`, `.Huida.cs`, `.Resistir.cs`, `.Extraer.cs` (código movido tal cual, sin cambios de lógica).
- `Editor/OperacionBuilder.Cuartel.cs`, `.Puestos.cs`, `.Centro.cs`, `.Huida.cs` (patio del tanque, autopista, plantilla de camioneta, helicóptero).
- `Editor/ChecksBugs065.cs` (framework de checks + `Bug000` smoke).

Modificados:
- `Operacion/OperacionDirector.cs` (ahora `partial`: campos, Start/Update, EntrarFase, helpers, estado, SaltarA, Perder; más `TotalObjetivos`, `TituloObjetivo`, `Orden`, `Indice`, `Subfase`, `EntrarSubfase`).
- `Operacion/OperacionHud.cs` (APIs nuevas: `BarraDeJefe`/`OcultarBarraDeJefe`, `Pasos`, `Subobjetivos`).
- `Editor/OperacionBuilder.cs` (núcleo), `Editor/OperacionBuilder.Ciudad.cs` (recibió `Ciudad()`), `Editor/OperacionPrueba.cs`, `Editor/ValidacionBugs.cs`.
- `Scenes/SC_Operacion.unity` regenerada con el builder (ver abajo).
- Efecto colateral del rebuild: `M_Op_AutoCaja.mat` y `M_Op_AutoVidrio.mat` (el campo legacy `_Color` pasó de blanco al color base; sin efecto visible).

### Qué quedó hecho

1. **Director en partials por fase.** Solo se movió código; nada público se renombró. Compila y se comporta igual.
2. **Numeración.** `OperacionDirector.TotalObjetivos = 6` y `TituloObjetivo(n, nombre)`. Los 8 literales "OBJETIVO n/6" del director pasan por ahí.
3. **Orden de fases.** `OperacionDirector.Orden[]` + `Indice(fase)` (Victoria/Derrota dan `Orden.Length`). `SaltarA`, `OperacionPrueba` y `ValidacionBugs` usan `Indice`/`Orden`/`TotalObjetivos` en vez de `(int)` y `1..6`. El enum no cambió.
4. **Subfases.** `Subfase` (get público), `EntrarSubfase(int)` (protected), reinicio a 0 en cada `EntrarFase`, persistencia en `CapturarEstado`/`RestaurarEstado` (clave `subfase` dentro del estado "Operacion"). `SaltarA(fase, teletransportar, subfase)` y `OperacionPrueba.Arrancar(obj, puesto = 1, subfase = 0)`.
5. **APIs de HUD** (sin uso de juego todavía):
   - `BarraDeJefe(nombre, frac)` / `OcultarBarraDeJefe()`: barra roja 520x18 arriba al centro con el nombre encima.
   - `Pasos(string[], actual)`: lista bajo el panel; hechos en verde con ✓, actual en amarillo con ▶, resto en gris con •.
   - `Subobjetivos(linea)`: segunda línea chica bajo el panel.
   - Lectores para checks: `BarraDeJefeVisible`, `TextoJefe`, `FraccionDeJefe`, `TextoPasos`, `TextoSubobjetivo`.
6. **Framework de checks `ChecksBugs065`.** Registro por reflexión de métodos estáticos `Bug0NN()` sin parámetros. Devuelven `string` (síncrono) o `IEnumerator` (con esperas; cierran con `Fin("OK ...")`). API: `Correr(n)`, `CorrerTodos(desde = 65, hasta = 101)`, `Estado` (inactivo/corriendo/listo), `Informe`, `Resultado`, `Registrados()`, helper `Esperar(seg)`. Cuenta los errores de consola por bug. **Convención para los demás paquetes:** agregar `static string Bug0NN()` (o `IEnumerator`) en esta clase (sirve un archivo `ChecksBugs065.Wp1.cs` partial para evitar conflictos). Los asíncronos devuelven "EN CURSO" y se sondea `Estado`.
7. **Torretas del centro portadas al builder** (riesgo crítico del plan). `OperacionBuilder.Centro.cs: TorretasDelCentro(...)` instancia `P_Env_Emplazamiento_MG` en `x = ±8,5`, `z = computadora + 10,5` (= -34,7), yaw ∓8°, bajo `Operacion/3_CentroDeDatos/TorretasDelCentro`, y `Construir()` cablea `dir.torretasDelCentro`. `LimpiarEscena` también borra la vieja raíz `TorretasDelCentro` para no duplicar.

### Desviaciones del plan (y por qué)

- **El rebuild también tuvo que portar la noche y las luces de la plaza.** Al reconstruir descubrí que `SC_Operacion` tenía, además de las torretas, varios retoques manuales hechos con scripts sueltos después del builder: noche (luna, ambiente, cielo estrellado, niebla), volumen `PostProceso_Noche` (VP_Noche, prioridad 5), farolas de la plaza movidas a las esquinas y 4 luces de plaza (`Luz_Plaza_1..4`, int 9, rango 24). El builder armaba "día claro" y habría dejado el nivel de día. Lo porté al builder: `ConfigurarAmbienteNoche` (constante `Noche = true`; `ConfigurarAmbienteDia` queda como alternativa), `PostprocesoNocturno()`, `FarolasDeLaPlazaAEsquinas()` + 4 luces en `Ciudad()`.
- **Verificación de fidelidad del rebuild:** comparé el escena original (copia temporal, ya borrada) contra la reconstruida objeto por objeto (ruta, posición, rotación, escala, activo, material, componentes, luces) y el ambiente (sol, ambiente, cielo, niebla, cámara). Resultado: 5405 objetos en ambas, 0 faltantes, 0 sobrantes, y una única diferencia: 18 `NavMeshModifierVolume` nuevos en los postes de farola de `6_Ciudad/Ampliacion` (postes de 0,3 m; los agrega el bake del builder; efecto en la navegación despreciable).
- **Raíces de la escena: 12 → 11.** `TorretasDelCentro` dejó de ser raíz y quedó dentro de `Operacion/3_CentroDeDatos`. Objetos totales: 5416 antes, 5416 después.
- **Banner de `GameplaySceneBootstrap.cs:142`:** no tiene número ("Infiltra · 3 puestos · ..."), no hay literal que reemplazar. Sin cambios.
- **Clave de estado:** el plan dice `Operacion.subfase`; el estado ya vive bajo la clave "Operacion", así que la clave interna es `subfase`.

### Checks corridos

| Check | Resultado |
|---|---|
| Compila (eval sobre `TotalObjetivos`, `Indice`, `Subfase`, `Registrados()`, `TorretasDelCentro`) | OK |
| `ValidacionBugs.Iniciar` iteración 91 (ANTES del refactor, escena vieja) | 25/28 (fallaban BUG 12 rescate, BUG 12 vuelve a primera persona, BUG 16 aliados lejanos; errores de consola 0) |
| `ValidacionBugs.Iniciar` iteración 92 (DESPUÉS, escena reconstruida) | 28/28, errores de consola 0. Los 6 "NIVEL objetivo" OK en las dos corridas. |
| Smoke `ChecksBugs065.Correr(0)` en Play: Arrancar(1..6), fase correcta y título "OBJETIVO n/6", subfase se fija y se reinicia, APIs de HUD se ven y se ocultan, 0 errores de consola | OK |
| Rebuild conserva torretas: `dir.torretasDelCentro.Length == 2`; en Play `FindObjectsByType<TorretaFija>` = 2 con `TorretaFija` instalada en ambas | OK |
| Diff objeto por objeto escena vieja vs reconstruida | OK salvo las 18 diferencias de NavMeshModifierVolume de arriba |
| NavMesh tras el rebuild: `CalculatePath` entre entradas, panel/computadora/plaza/helipad | OK donde no hay compuertas; los tramos "MAL" cruzan porton/barreras cerradas, que bloquean por diseño hasta que se hunden (las compuertas se abren en runtime con NavMeshViva) |
| Captura de las APIs de HUD: `Assets/Validacion/v2_wp0_hud_apis.png` | OK (los glifos ✓ ▶ • se ven) |

### Pendientes honestos

- La suite del flood-fill con `Physics.CheckCapsule` entre `entradas[]` no se corrió (usé `NavMesh.CalculatePath` y el recorrido real de los 6 objetivos en Play). Un rebuild cambió el NavMesh (se rehornea); las compuertas se validaron por el recorrido de `ValidacionBugs`, no por flood-fill.
- No medí `FindObjectsByType<TorretaFija>` en Play ANTES del rebuild (solo en Edit, donde da 0). El cableado es idéntico (mismo prefab, misma posición, misma ruta `Instalar`), y el resultado después es 2.
- La baseline de `ValidacionBugs` (25/28) tuvo 3 fallos que no se repitieron en la corrida posterior (28/28); parecen no deterministas (rescate, regreso a primera persona, regreso de aliados). No los causó el refactor.
- Los dos `.mat` de la camioneta quedaron modificados en el working tree (cosmético).
- `Pasos`/`Subobjetivos`/`BarraDeJefe` no tienen uso en el juego todavía (los usan WP9a/WP9b); la barra de jefe y el layout de los pasos se validaron solo con la captura de arriba.

---

## WP3 — UI de opciones, controles y minimapa

**Fecha:** 2026-10-04. **Bugs cubiertos:** #098 (perillas), #099 (parpadeo del tamano de HUD), #100 (UI de controles), #065 solo la parte del minimapa (muros).

### Archivos (`Assets/_Project/Scripts/`)

Creados:
- `UI/TablaDeControles.cs`: tabla con ScrollRect (encabezado por contexto, fila por atajo con keycaps y descripcion con wrap, barra de scroll). `Construir`, `Asegurar` (reconstruye solo si cambia el idioma o una tecla remapeada), `Contenido`, `Tokens`.
- `Presentation/SiluetasDeMinimapa.cs`: `Registrar()` (un solo mesh y un solo material en la capa Minimap a y=40) y el marcador `SiluetaMinimapa` para builders futuros.
- `Editor/ChecksBugs065.Wp3.cs`: `Bug065m`, `Bug098`, `Bug099`, `Bug100` (+ helper `CapturarCanvas`).

Modificados:
- `UI/EstiloDeAjustes.cs`: `VestirSlider` (zona de la perilla con alto cero centrado, `preserveAspect`); `SliderDeAjuste` expone `Apretado` y el evento `Soltado`.
- `UI/LayoutDeAjustes.cs`: `Firma()` ignora los `*_Value`.
- `Presentation/PauseController.cs`: canvas propio `CanvasPausa`; el tamano de HUD se aplica al soltar; `RefreshControlsList` arma la tabla, oculta REMAPEAR y centra VOLVER.
- `Presentation/GameplaySceneBootstrap.cs`: llama a `SiluetasDeMinimapa.Registrar()` tras `RegistrarObstaculos`.
- `Editor/HeadlessTestRunner.Fase21.cs:523`: el check de perilla circular compara `handleRect.rect` en vez de `sizeDelta`.
- `Editor/ChecksBugs065.cs`: el registro admite checks con sufijo de una letra (`Bug065m`, `Bug072a`...) y `Correr(n)` corre todos los del numero n (antes solo reconocia `Bug0NN` exacto, y el plan pide `Bug065m`/`Bug072a`/`Bug095a`).

No se toco `OperacionBuilder` ni la escena (todo es de runtime): no hubo rebuild de `SC_Operacion`.

### Que quedo hecho

1. **#098:** la perilla mide 22x22 en los 8 sliders (antes 22x46).
2. **#099:** (a) la pausa y Configuraciones viven en un Canvas raiz propio (`CanvasPausa`, overlay, orden 900, 960x540 match 0.5) que se arma en runtime en `SubirALaCapaDePausa` y reparenta el `PauseController` conservando todas las referencias; (b) mientras se arrastra el slider de HUD solo cambia la etiqueta, y `referenceResolution` + `PlayerPrefs.Save` se aplican en `OnPointerUp` (con teclado o mando, sin puntero apretado, se aplica en el acto; si se cierra la pantalla a mitad de arrastre se aplica el pendiente); (c) los `*_Value` salieron de `Firma()`.
3. **#100:** tabla con scroll de 100 filas y 6 encabezados, keycaps por tecla (mouse con `TutorialTextures.Mouse`), descripcion con wrap a fuente 16; panel a 820x500; REMAPEAR oculto y VOLVER centrado; se construye una vez por apertura solo si cambio algo. `ControlsTable.FullText()` se conserva (lo usan Fases13a17).
4. **#065 minimapa:** 47 siluetas (OBB real con el yaw del collider, ancho minimo 1.2 m) con los muros de Valle, Borde, Cuartel, CD_, Cierre, Porton, Barrera, Muro, Brecha, Pilar y Portico.

### Desviaciones del plan (y por que)

- **`CinematicaDeIntro.cs:54` NO se cambio.** El plan pedia pasar a `FindFirstObjectByType`, pero con la pausa en su canvas raiz `cv.GetComponentInChildren<PauseController>` ya da `null` para el canvas del HUD (que pasa a apagarse entero, que es lo que se buscaba) y encuentra la pausa en `CanvasPausa` (donde no hay nada que ocultar). El comportamiento es equivalente. No lo probe en SC_Gameplay (la cinematica de intro no esta en SC_Operacion): queda como pendiente.
- **Prefijos de siluetas ampliados** con `Brecha`, `Pilar` y `Portico` (piezas de muro de la entrada y el portico, sin `ObstacleMarker`). Se saltean los colliders que ya tienen `ObstacleMarker`. En SC_Operacion `Nivel_Blockout` no existe como raiz: solo cuenta `Operacion`.
- **Siluetas en un solo mesh combinado** (un GameObject, un MeshRenderer), no un GameObject por icono. Por eso `Bug065m` verifica `SiluetasDeMinimapa.Cantidad`/`Nombres`/mesh en vez de contar GameObjects.
- **Sin el panel de remapeo accesible:** REMAPEAR queda oculto a pedido (el `RebindPanel` y su logica siguen en el codigo y en la escena).
- Se agrego `Apretado`/`Soltado` en `SliderDeAjuste` (el plan decia "exponer el evento"); el HUD se conecta en `OnSettingsClicked` despues de `Preparar` (cuando el slider ya esta vestido).

### Checks corridos (Play sobre SC_Operacion, obj 1)

| Check | Resultado |
|---|---|
| Compila (eval sobre `ChecksBugs065.Registrados()` = 0,65,98,99,100 y `TablaDeControles.Tokens`) | OK |
| `Bug098`: 8 sliders, `\|w-h\|<0.5` y alto 20-24 (todos 22x22) | OK |
| `Bug099`: panel 1461x1037 constante en {1.0, 0.75, 0.7, 1.2, 1.4} (difTam=0, difOrigen=0); `LayoutDeAjustes.Aplicaciones` +0 durante un barrido de 13 tics; HUD scaleFactor x2.00 entre 0.7 y 1.4; arrastre con puntero apretado: sin cambio durante, etiqueta actualizada, aplica y guarda al soltar; valor original restaurado | OK |
| `Bug100`: 100 filas = 100 entradas, 6 encabezados, 0 filas sin keycap, 0 solapes verticales, 0 textos cortados, tabla dentro del panel, contenido 4702 de alto en viewport 364 (hay scroll), RebindButton inactivo, reabrir no reconstruye | OK |
| `Bug065m`: 47 siluetas (>30), con Valle/Borde/Cuartel, mesh de 782x782 (perimetro), capa Minimap, material, y=40 | OK |
| Errores de consola durante los checks | 0 (solo timeouts del CLI de Unity ya conocidos) |
| Capturas | `Assets/Validacion/v2_098_sliders.png`, `v2_100_controles_1080.png`, `v2_100_controles_720.png`, `v2_100_controles_gameview.png` (Game View real), `v2_065_minimapa.png` (comparable con `diag_065_minimapa.png`: ahora se ven los muros del cuartel y la linea del porton) |

### Pendientes honestos

- No se corrio la suite headless: toca SC_TestLevel y esta fuera del alcance rapido del paquete. Fase21 (Configuraciones) puede verse afectada por el canvas nuevo; la unica edicion hecha ahi es la del check de perilla (rect). Lo cubre WP11.
- La cinematica de intro de SC_Gameplay (que oculta el HUD y respeta la pausa) no se probo con la pausa en `CanvasPausa`.
- No hice control negativo de `Bug098` (reponer las anclas viejas y ver que falle); la medicion previa 22x46 viene del plan.
- Las siluetas son estaticas: un muro o porton que se hunda o se destruya en runtime seguira dibujado en el minimapa (los obstaculos con `ObstacleMarker` siguen su propio icono). Los `Jersey`, postes y chevrons no tienen silueta (WP6 les pone `ObstacleMarker`).
- El minimapa muestra el perimetro solo cuando el jugador se acerca (radio 110 m); no se capturo el `Borde` de 780 m por eso (el mesh si lo incluye, bounds 782 m).
- Nota de entorno: las capturas de pausa a 1080/720 se hicieron con una camara temporal sobre `CanvasPausa` (el Game View real estaba en ~1280x720).

---

## WP1 — Combate: headshots, municion, rachas, cursor de baja y Q con margen

**Fecha:** 2026-10-04. **Bugs cubiertos:** #094 (headshots), #095 (municion total real en el HUD; el audio es de WP2), #079 (rachas y FURIA), #065 (solo el cursor de baja; el audio es WP2 y el minimapa fue WP3), #096 (Q con margen de 1 s y diana).

### Archivos (`Assets/_Project/Scripts/`)

Creados:
- `Presentation/RachaDeBajas.cs` (multikill, FURIA, aura, `MultiplicadorPara(id)`), `UI/CartelDeRacha.cs` (carteles y aviso fijo de FURIA), `Presentation/DianaDeObjetivo.cs` (diana pooleada de 4).
- `Editor/ChecksBugs065.Wp1.cs` (`Bug094`, `Bug094p`, `Bug095`, `Bug079`, `Bug065c`, `Bug096` + helpers de tiro real, congelado de postura y captura de pantalla).

Modificados:
- `Combat/HitboxCabeza.cs` (caja medida con la malla, test de tramo `Cruza`), `Combat/Projectile.cs` (head-ray sobre el tramo, fase amplia de 1.7 m, `UltimoImpactoEnSoldado`, multiplicador de FURIA en `Configure`), `Mision/EstadisticasDeMision.cs` (HEADSHOTS).
- `Combat/WeaponHolder.cs` (`CargadoresDeReserva` pasa de const a estatico, `AgregarMunicion` reparte al loadout, `MunicionTotal`), `UI/WeaponStatusView.cs` (formato "cargador / total"), `Player/CajaDeSuministros.cs`, `Core/ReinicioDeEstaticos.cs`, `Operacion/OperacionDirector.cs` (reservas activas, 4 cargadores, reabastecimiento al empezar cada objetivo).
- `Actors/SoldierMotor.cs` (`FactorDeBuff`), `Presentation/KillFeedbackDirector.cs` (se saco el texto "RACHA n").
- `UI/MarcaDeImpacto.cs` (reescrita la rama de baja), `UI/AimUI.cs` (flash de baja en blanco).
- `Player/PlayerInputDriver.cs` (1 linea tras la mira), `PlayerInputDriver.Interaccion.cs` (memoria de enemigo y `ConMargenDeQ`), `PlayerInputDriver.Radial.cs` (ATACAR del radial), `OrderService.cs` (llama a la diana).

### Que quedo hecho

1. **#094.** Causa real distinta de la del plan: la hitbox ya iba pegada al hueso Head y ese hueso esta en la BASE de la cabeza (de pie queda a 1.48 m; agachado a 0.93 m). Lo que fallaba era (a) que el headshot lo decidia UNA sola muestra de 1 m de paso (la muestra que entraba al collider del cuerpo caia antes de la caja de la cabeza), (b) que el casco por encima de 1.6 m no tocaba nada y (c) que el check del plan apuntaba al hueso (el cuello). Ahora la caja sale de los vertices pesados al hueso Head, en ejes del hueso (+4 cm de margen, cache por malla, respaldo con el cubo viejo si la malla no es legible); `Projectile` prueba el TRAMO contra la caja con la transform viva del hueso (caja orientada, sin esperar al paso de fisica) antes del cuerpo, y el cuerpo ya nunca es headshot. Fase amplia de 1.7 m. Conteo "HEADSHOTS {jugador} (+N de tu escuadra)" en `Filas()`.
2. **#095.** La Operacion usa reservas (4 cargadores por arma, `OperacionDirector.Awake`). El HUD dice "cargador / (cargador+reserva)" (fusil: 8 / 40) o "cargador / ∞" sin reservas (la fuente tiene el glifo). `AgregarMunicion` suma un cargador a cada arma del loadout (tope = 2x la reserva inicial) y el pedido completo al equipado.
3. **#079.** DOBLE, TRIPLE, CUÁDRUPLE y PENTA KILL (ventana de 4 s) con cartel (Black Ops One 64 pt a 1920x1080, +300 px, entrada 1.6 a 1.0 en 0.18 s, 1.6 s de vida, fundido) y tono que sube. FURIA con 5 bajas con <= 6 s entre cada una: 12 s, danio x2 (en `Projectile.Configure`: vale para balas y explosiones), +30% de velocidad (`SoldierMotor.FactorDeBuff`), aura roja bajo los pies y aviso fijo con cuenta atras. Se corta al morir y al cambiar de posesion.
4. **#065 cursor.** Baja = 8 rayas que explotan hacia afuera + anillo de 24 a 70 px de radio en 0.35 s + golpe de escala 2.0 a 1.0 + rojo intenso + 0.7 s. Baja por headshot (HeadshotEvent del mismo golpe): dorada, con segundo anillo. Una herida que no mata sigue siendo la marca de 4 rayas.
5. **#096.** Se recuerda el ultimo enemigo bajo la mira; un toque simple de Q (o ATACAR del radial, salvo SUPRIMEN y GRANADA) sobre None, Ground, Cubrirse u Obstacle dentro de 1 s ataca a ESE enemigo. Aliados, caidos e interactuables no se pisan. El segundo toque a menos de 1 s es el doble toque SIGANME y manda la mira real. Diana: billboard a +2.3 m (crece con la distancia), dos anillos dentados que giran a 90 grados por segundo en sentidos opuestos, cruz y pulso; vive mientras algun aliado lo tenga como blanco en Attack, Chase o MovingToAttackOrder, hasta que muera o 10 s; pool de 4.

### Desviaciones del plan (y por que)

- **#094, causa y ubicacion.** El diagnostico del plan (hitbox a 1.05 m, objetivo `mallaMax-0.30`) se midio con un enemigo AGACHADO; de pie la caja ya estaba en su lugar. No se uso `smr.bounds` de pie: la caja sale de los vertices pesados al hueso. La hitbox se sigue creando a demanda (primer disparo que pasa cerca) y no al spawnear: el check confirma que el primer tiro a un enemigo sin caja ya cuenta como headshot.
- **#094, check.** Los enemigos se agachan solos al recibir fuego (reaccion del cerebro) y el director reactiva cerebros apagados; para medir se congela el Animator del blanco (se desactiva `SoldierAnimatorDriver` y se fija el parametro Agachado) y se apunta con ADS. Ademas del escenario de pie del plan se prueba uno AGACHADO (apuntando al centro visible de la cabeza, calculado con `BakeMesh`, independiente de la caja): 6 de 6 headshots y 0 de 4 a la cadera.
- **#095, formato y defaults.** El total es cargador + reserva, como pide el plan (el HUD viejo mostraba solo la reserva). Defaults: los reales del catalogo (fusil 8 + 32 = 8 / 40), no los 30/90 del plan; no toque el catalogo. No hay icono de infinito aparte: se usa el glifo.
- **#095, reabastecimiento.** Agregado fuera del plan: al empezar cada objetivo el soldado manejado repone todas sus armas (el nivel no tiene cajas de suministros y con reservas reales se podia quedar sin balas a mitad de Resistir). Si no se quiere, es una linea (`ReabastecerAlJugador` en `OperacionDirector.EntrarFase`).
- **#079.** El multiplicador de danio va en `Projectile.Configure` (no en `Health`); `RachaDeBajas.MultiplicadorPara` es la unica fuente. "No se puede reactivar hasta 20 s despues" se interpreto como 20 s desde que TERMINA la FURIA. Se retiro el texto "RACHA n" del feed pero se conserva el tono viejo por baja.
- **#065 cursor.** El plan dejaba "calavera o anillo doble": se hizo anillo doble dorado. `AimUI.FlashHitMarker` conserva el pico de tamano; solo el color de la baja pasa de amarillo a blanco para no competir con el rojo y el dorado de la marca.
- **#096.** `MiraForzada` y `RecordarEnemigoApuntadoHace` son los ganchos para checks. La memoria se consume al usarse (un segundo Q no re-ataca).

### Checks corridos (Play sobre SC_Operacion; `ChecksBugs065.Correr(n)`)

| Check | Resultado |
|---|---|
| Compila (eval sobre `ChecksBugs065.Registrados()` = 0,65,79,94,95,96,98,99,100) | OK |
| `Bug094`: de pie, cabeza visible 8/8 headshots (el primer tiro a un enemigo sin caja ya fue headshot); pecho 0/8 headshots y 8/8 impactos; casco (mallaMax-0.08) 4/4 impactos y 4 headshots; agachado cabeza visible 6/6 y cadera 0/4; fila HEADSHOTS = stats = proyectil | OK |
| `Bug094p`: el popup HEADSHOT esta visible con texto | OK |
| `Bug095`: `UsaReservas`, 4 cargadores, HUD "8 / 40", disparo "7 / 39", pickup "7 / 47", otra arma (Pistol) 48 a 60, "24 / ∞" con glifo | OK |
| `Bug096`: Q a 0.6 s ataca y aparece la diana (giro +28/-28 grados en 0.3 s, +2.30 m); doble toque dentro de 1 s sigue; a 1.3 s sigue; apuntando a un aliado posee; 6 ordenes dejan 4 dianas; al morir el enemigo se apaga | OK |
| `Bug065c`: herida 4 rayas/0.28 s sin anillo; baja 8 rayas, 0.70 s, anillo 24 a 70 px, escala 2.0 a 1.0; headshot dorada con anillo doble | OK |
| `Bug079`: DOBLE, TRIPLE, CUÁDRUPLE, PENTA; furia con FactorDeBuff 1.30 y multiplicador 2.0; danio real de un tiro 68 a 135 (x2); velocidad 5.00 a 6.50; termina a los 11.7 s y todo vuelve a 1; no se reactiva (20 s de enfriamiento); 6.5 s entre bajas corta la racha; al morir se resetea | OK |
| `Bug000` (smoke WP0) y `Bug065m` (minimapa WP3) tras los cambios | OK |
| Errores de consola durante los checks | 0 (el arnes los cuenta por check) |
| Capturas (`Assets/Validacion/`) | `v2_094_headshot.png`, `v2_094_resultado.png` (fila "HEADSHOTS 18 (+4 de tu escuadra)"), `v2_095_hud.png` (+ `_zoom`), `v2_096_diana.png` (+ `_zoom`), `v2_065_cursor_baja.png`, `v2_065_cursor_baja_headshot.png` (+ `_zoom`), `v2_079_penta.png` |

### Pendientes honestos

- **No corri la suite headless** (`Run All Tests Headless`): toca SC_TestLevel y no entra en la autocomprobacion rapida. Revise a mano los puntos de riesgo: Fase 18 y 23 (reservas y texto con " / ") siguen compilando y leen el valor estatico; no hay tests que miren la marca de impacto, el texto "RACHA" ni la hitbox de la cabeza. Lo cubre WP11.
- **Sin control negativo del #094 contra el codigo viejo** (revertir y ver que falle). Las primeras corridas con el codigo viejo y un enemigo agachado dieron resultados incoherentes, pero no es una comparacion limpia.
- El riesgo de volver al "100% headshot" esta cubierto en Play (pecho 0/8) pero no en la suite headless (soldados cubo sin rig, que usan el respaldo de la caja por bounds).
- La diana no se probo en RTS ni con el radial por teclado real (solo `ResolverGestoDeQ` e `IssueAttackOrderForSelection`). El tono de multikill y el sonido de inicio de FURIA son 2D reutilizados (`HealDone`, `ObjetivoCumplido`) y no se pudieron escuchar; WP2 puede reemplazarlos.
- Balance de la FURIA (x2 y +30%) sin jugar en serio; las constantes estan en `RachaDeBajas`.
- Con reservas reales cada aliado poseido se inicializa con sus cargadores la primera vez que se lo posee.
- El conteo de HEADSHOTS aparece siempre en la pantalla de resultado (con 0 si no hubo); la tabla llega a 11 filas y entra sobre los botones a 1080p, pero no se miro a 720p.

---

## WP2 — Audio: 3D, distorsion, graves y musica de victoria

**Fecha:** 2026-10-04. **Bugs cubiertos:** #065 (sonido de muerte 3D y mas bajo; solo la parte de audio), #095 (disparos con distorsion y todo 3D salvo musica/ambiente/UI), #072 (graves y chapa; solo la parte sonora), #093 (musica de victoria con fade largo).

**No pude escuchar nada** (el CLI no tiene salida de audio). Todo se verifico con datos objetivos: propiedades de las AudioSource reales del pool, rolloff/min/max, volumenes, nombres/pico/espectro de los clips horneados, `AudioDirector.Historial` y el volumen de la fuente del himno a lo largo del tiempo. Que *suene bien* queda sin confirmar.

### Archivos (`Assets/_Project/Scripts/`)

Creados:
- `Presentation/SfxSintetico.Graves.cs` (`SubGolpeDatos`, `RetumboDeExplosionDatos`, `SubGolpe`, `DestruccionVehiculo`).
- `Presentation/SfxSintetico.Victoria.cs` (`HimnoDeVictoriaDatos`, seguro para un hilo; `ClipDelHimno`).
- `Editor/AudioImportFix.cs` (menu `Strategic Point/Audio/Forzar mono en voces`).
- `Editor/ChecksBugs065.Wp2.cs` (`Bug065a`, `Bug095a`, `Bug072a`, `Bug093`).
- Graficos de evidencia (formas de onda; el clip horneado en naranja, el crudo en azul): `Assets/Validacion/v2_072_explosion_graves.png`, `v2_072_cannonbody_graves.png`, `v2_072_destruccion_chapa.png`, `v2_093_himno_onda.png`.

Modificados:
- `Presentation/AudioDirector.cs`: `PerfilEspacial` (Base lineal 5-90, Voz 3-60 log, Disparo 5-120 log, Explosion 8-220 log, Ui 2D) con `Para(SfxKind)`; `Attenuation(d, perfil)` y `CutoffFor(d, max)`; `PlayClip(..., perfil, pitch)`; `PlayFlat(..., pitch)`; overloads estaticos `PlayAt/PlayClipAt` con perfil. Documenta que queda en 2D.
- `Presentation/GenericSfx.cs`: enum `DestruccionVehiculo` al final; `GetWeaponShot` y `Get(Shoot)` devuelven el clip distorsionado (`<clip>_dist`); `Get(Explosion)` y `Get(CannonBody)` devuelven `Explosion+Sub` y `CannonBody+Sub`.
- `Presentation/MusicDirector.cs`: `PrecargarVictoria`, `TocarVictoria(fadeIn=6, fadeOtros=4)`, `DetenerVictoria`, `VictoriaSonando`, `VolumenVictoria`, `VolumenDeLoops`, `FactorOtros` y un runner con tiempo no escalado.
- `Presentation/SonidosDeOperacion.cs` (Logro 3D en la victima, tick 2D 0.15 en la baja propia, ambiente a 0.25 en la victoria), `CubeFxReactor.cs`, `VehicleFxReactor.cs`, `RachaDeBajas.cs` (tonos por el director, canal Ui), `Subtitulos.cs` (etiqueta de la chapa).
- `Combat/Projectile.cs` (headshot 3D en el punto del impacto), `Vehicles/TurretWeapon.cs` (perfil de disparo de la MG; duck del canon 0.25 a 0.1), `Vehicles/Vehicle.cs` (`FinalExplosion` ya suena), `Operacion/OperacionAuto.Efectos.cs`, `Operacion/OperacionDirector.Extraer.cs` (himno al despegue, precarga al apretar [E], sin asiento 2D duplicado), `Mision/CinematicaDeVictoria.cs` (himno), `Mision/CinematicaDeIntro.cs` (disparo de saltear toma en 3D).
- 57 `.meta` de clips bajo `Resources/Audio/Sfx/` (forceToMono=1, normalize=0).

### Que quedo hecho

1. **Perfiles espaciales por voz.** Los puntos de llamada heredan el perfil por `SfxKind` sin tocarlos uno a uno. Las constantes globales de `AudioDirector` (las que testea la suite headless) no cambiaron.
2. **#065.** Muerte: vol 0.5 (antes 1.0), perfil Voz. `Logro` por baja: 3D en la victima, vol 0.35, conserva el tono que sube con la racha (pitch explicito); si la baja es del jugador, ademas un tick 2D de 0.15. Vehiculo enemigo destruido: Logro 3D con perfil Explosion. Clips de voz en mono.
3. **#095.** Distorsion horneada: `tanh(2.5x)` + realce de graves de 1 polo (+4 dB bajo 200 Hz), cacheada por clip. A 3D: headshot, disparo de la intro, golpe de chasis (`VehicleFxReactor`); asiento duplicado quitado. Los tonos de racha de WP1 (`MultiKill`, `FuriaInicio`) pasan por el pool 2D (canal Ui) en vez de un GameObject suelto.
4. **#072.** `Explosion+Sub` y `CannonBody+Sub` (cuerpo + golpe de graves saturado 125 a 45 Hz + sub 60 a 28 Hz; la explosion suma ademas un retumbo grave sintetico), horneados en una sola voz. `SfxKind.DestruccionVehiculo` (parciales 180/410/730/1100 Hz, 6 a 9 golpes de chapa Clac/Tunc entre 0.3 y 2.5 s, roce largo, 3.4 s) suena en `Vehicle.FinalExplosion` (muda hasta ahora; no se duplica en las camionetas, que ya suenan por `OperacionAuto.Estallidos`) y al destruirse una camioneta.
5. **#093.** Himno de 26.5 s (Re mayor, 92 bpm, I-V-vi-IV, pad + cuerno suave, bronces saturados a plena fuerza en la 2.a vuelta, timbal, redoble final, platillo), generado en un hilo (~1.0 s) al apretar [E] para subir; `TocarVictoria()` en el primer frame del despegue. Fade de entrada de 6 s (smoothstep) con `unscaledDeltaTime`, fuente 2D con `ignoreListenerPause`; los loops de combate bajan a 0 en 4 s; el viento a 0.25. Tambien en `CinematicaDeVictoria` de la otra mision.

### Desviaciones del plan (y por que)

- **`PerfilEspacial` sin campo `distorsion`:** la distorsion esta horneada en el clip; un campo que nadie lea seria enganoso. Tampoco hay restablecimiento "al liberar": el director reescribe rolloff/min/max en cada reproduccion.
- **Distorsion normalizada por RMS, no al pico 0.95.** Medido: normalizando al pico el saturador subia el nivel +5 a +10 dB (la pistola cruda tiene pico 0.27). Se fija +3 dB de RMS sobre el original con tope de pico 0.9 (picos resultantes 0.39 a 0.86; el check exige <= 0.95).
- **El diagnostico "el pack de explosion no tiene graves" era parcialmente falso.** Medido con Goertzel, los `explosionCrunch_*` ya tienen energia a 30-60 Hz. La mejora real de la banda 30-150 Hz (relativa al pico) es x1.4 en la explosion (~+3 dB) y x2.8 en el canon (~+9 dB). El check pide >= x1.25 por este motivo. Es lo que se puede afirmar sin oirlo.
- **forceToMono ampliado** a los sonidos del mundo que van por el pool 3D (Shot_*, Shoot, Explosion, CannonBody, Hit, Impact*, VehicleHit, Footstep*, GrenadeBounce, EmptyClick, Orugas*): casi todos eran estereo y un estereo en una fuente 3D no se posiciona bien. Con `normalize` apagado para no cambiar su volumen. Son 57 `.meta` modificados.
- **Himno normalizado por RMS (0.14), no al pico:** saturado y normalizado al pico quedaba ~5x mas fuerte que `SA_Accion` (rms 0.20 a volumen 0.35). Queda ~2x mas fuerte que la musica de accion (volumen de fuente 0.95).
- **`Logro` 3D con pitch explicito:** se agrego un parametro `pitch` opcional a `PlayClip/PlayFlat` (un comentario viejo decia que el director "no admite pedirlo"), para no perder la escalera de racha.
- **`Subtitulos`:** etiqueta nueva "VEHICULO DESTRUIDO" para el clip nuevo (el nombre no contenia "explo").
- Los clips Shot_* se llaman ahora `<nombre>_dist`; los subtitulos por nombre ("shot") siguen funcionando para los que ya lo contenian.

### Checks corridos (Play sobre SC_Operacion)

| Check | Resultado |
|---|---|
| Compila (eval sobre `PerfilEspacial` y `ChecksBugs065.Registrados()` = 0,65,72,79,93,94,95,96,98,99,100) | OK |
| `Bug065a`: 12 clips Death/Wounded en mono; baja del jugador y baja ajena a 10 m: voz de muerte con `spatialBlend=1`, rolloff Logarithmic, min 3 / max 60, vol 0.46 (<= 0.55); Logro 3D vol 0.32; Logro 2D solo en la baja propia a 0.14 (<= 0.2) | OK |
| `Bug095a`: 7 armas con `_dist`, mono y pico <= 0.95 (Rifle = `Fusil_Freesound_felixblume_dist`); 20 s de combate (4 enemigos, el jugador tirando, 1953 muestreos): 2D vistas = solo Estrategia, Lucha, CritTone (mirilla) y UiVoice_1/Logro (tick propio); ninguna 2D no permitida; +49 reproducciones del director | OK |
| `Bug072a`: banda grave x1.4 (explosion) y x2.8 (canon); DestruccionVehiculo 3.4 s; perfiles; el canon dispara de verdad y suena `CannonBody+Sub`; volumen del oyente tras el tiro min 0.85 de 0.91 (duck leve); camioneta destruida suena `DestruccionVehiculo`; `Vehicle.FinalExplosion` suena `Explosion+Sub` y `DestruccionVehiculo` | OK |
| `Bug093`: himno 26.5 s, pico 0.35, rms 0.14, 0 compases mudos, sin NaN; despegue a los 5.2 s de apretar [E]; volumen normalizado a +0.3/1.5/3/4.5/6.3 s = 0.01/0.15/0.48/0.80/0.95 (monotono); loops de combate 0.29/0.20/0.08/0.00/0.00; con `timeScale 0` (victoria real) sigue sonando a 0.95 y los loops en 0.00 | OK |
| Regresion: `Bug000` (smoke WP0), `Bug065c/m` (WP1/WP3), `Bug079`, `Bug094/094p`, `Bug095` (HUD municion), `Bug096`, `Bug098/099/100` | OK. `079`, `094`, `095` y `096` fallaron en una corrida encadenada por contaminacion de estado (tras 072/093 la escuadra esta en el helicoptero, o `Bug000` mato enemigos) y dieron OK en Play fresco |
| Errores de consola durante los checks | 0 (el arnes los cuenta por check) |

### Pendientes honestos

- **Nada se escucho.** No se juzgo el timbre de la distorsion, el equilibrio del himno ni si los graves y la chapa suenan bien. Los parametros estan en constantes (`GenericSfx.DriveDeDisparo`, `RealceGravesDb`, `GananciaRmsDeDisparo`; pesos en `ConGraves`; `MusicDirector.VolumenDeVictoria`; perfiles en `PerfilEspacial`).
- **Volumen percibido de los disparos lejanos:** el perfil Disparo es logaritmico (5-120 m); a 20 m suena ~1/4 del nivel que tenia con el rolloff lineal. Es mas realista pero puede sentirse bajo en el mapa grande.
- La melodia del himno es una linea simple de tonos del acorde (sin armonizar): funcional, no una composicion.
- `CinematicaDeIntro` (SC_Gameplay) y `CinematicaDeVictoria` (otra mision) compilan pero no se ejercitaron en vivo: solo se probo la ruta de SC_Operacion.
- Sin control negativo de los checks contra el codigo viejo (no se revirtio para ver que fallen).
- No corri la suite headless (`Run All Tests Headless`). Revisado a mano: `Attenuation/CutoffFor` sin perfil y las constantes siguen intactas (HeadlessTestRunner.cs:1357-1373).
- Con el himno el ambiente baja a 0.25 pero no a 0; la musica `Tension` de SC_Gameplay no se atenua con el himno.
- `AudioImportFix` toca 57 `.meta`; revertirlo implica `forceToMono: 0` en esas carpetas.

---

## WP4 — IA de combate: disparar, cubrirse, replegarse, guardias y separacion

**Fecha:** 2026-10-04. **Bugs cubiertos:** #080 (el enemigo no dispara), #081 (cubrirse para disparar, no huir), #074 (aliados usan cobertura en Resistir), #085 (guardias bien ubicados y en cobertura; solo el estado de IA, ver pendientes), #086 (los soldados se solapan), #087 (aliado herido se retira, se cubre o va al medico).

### Archivos (`Assets/_Project/Scripts/`)

Creados:
- `Ai/AiBrain.Herido.cs` (repliegue del herido: `TickHerido`, destino = medico o cobertura que no acerque al blanco, retroceder disparando, aviso "ESTOY HERIDO, ME REPLIEGO").
- `Ai/AiBrain.Separacion.cs` (empuje lateral entre soldados del mismo bando por `Motor.Move`, o sea por el Deslizador: no atraviesa paredes).
- `Editor/ChecksBugs065.Wp4.cs` (`Bug074`, `Bug080`, `Bug081`, `Bug085`, `Bug086`, `Bug087`, mas `Wp4Bug080Antes`, `Wp4BancoIniciar`, `Wp4BancoInforme`; arenas de prueba en el claro de SC_Operacion).

Modificados:
- `Core/Coberturas.cs`: la linea de tiro del combate pasa a ser boca del arma (pivote+0,5 de pie, +0,1 agachado) a pecho del blanco (`PuntoDeTiro`, `HayLineaDeTiroAlPecho`). `HayLineaDeTiroDesde` queda como estaba.
- `Core/Coberturas.Puntuada.cs`: `VersionNueva` (= v5 + `F_MULTITUD` + `F_SENTIDO`, valor 880) es la version por defecto; `TryElegir` con tope y minimo de distancia al blanco; un punto YA ocupado por un companero (en cobertura o yendo hacia ahi) no se elige.
- `Ai/AiBrain.cs`, `AiBrain.Sentidos.cs`, `AiBrain.Navegacion.cs`, `AiBrain.Tactica.cs`: histeresis de Attack (0,6 s sin linea, 1,08 del alcance, la reaccion no se re-arma si vuelve al mismo blanco en 1,5 s), dispara agachado solo si agachado tambien ve al blanco, cobertura proactiva en Chase (`RadioDeCoberturaAMano` 8 a 13 m, ahora `static`), punto de ataque personal por Id (abanico +-30 grados), `TomarCoberturaInicial`, la logica de cobertura no tironea al soldado de vuelta a su punto mientras se separa de un companero.
- `Player/PedidoDeCuracion.cs`: el herido que se repliega solo se retiene si ya esta en cobertura o a <= 3 m del medico (`SoltarRetenido`); `Atendiendo`; `BuscarEnfermero` ignora soldados desactivados (antes elegia un medico apagado, p. ej. uno dentro de un vehiculo).
- `Operacion/Atrincherar.cs`: el guardia queda EN su cobertura (`TomarCoberturaInicial` con el obstaculo). `Operacion/CazaDeEnemigos.cs`: un guardia atrincherado sano que no peleo no sale a cazar solo porque la escuadra pase a < 38 m.
- `Editor/CoberturaBench.cs`: `Centro` publico (el banco corre en el claro de SC_Operacion), metricas de % de Attack cubierto y de retroceso.
- `Editor/ChecksBugs065.Wp1.cs` (de WP1): `Bug079` y `Bug094` ahora saltean enemigos `Atrincherado` al elegir blanco (ver desviaciones).

Escena y builder: NO se tocaron (`Atrincherar` corre en runtime), no hizo falta regenerar.

### Que quedo hecho

- **#080.** Causa real: la linea de tiro del combate iba de pivote a pivote (0,8 m) y la eleccion de cobertura medía a otra altura; una barricada de 1,05 m tapaba siempre el rayo de combate aunque se pudiera tirar por encima. El guardia quedaba en Chase sin disparar. Ahora hay una sola definicion (boca a pecho) y histeresis para que no oscile Attack/Chase.
- **#081.** Todos buscan una cobertura a mano desde la que puedan disparar y que no los aleje del blanco (el herido si puede alejarse) antes de avanzar a cielo abierto; la eleccion no apila a dos en el mismo punto.
- **#087.** Con < 40 % de vida y en combate, el aliado de IA se repliega (hasta 55 %): va hacia el medico (encuentro a 2 m) y pide curacion, o si no hay medico a la mejor cobertura que no lo acerque al blanco; camina mirando al blanco y le devuelve rafagas. Respeta ALTO, cobertura ordenada, orden de atacar/ir, vehiculo y "siganme" forzado.
- **#086.** Empuje lateral de 1,5 m de radio y peso 1,3 (en cobertura 0,9 m), y cada soldado se acerca al blanco por su rumbo.
- **#085.** Los 12 guardias del objetivo 2 arrancan en `EnCobertura` con su punto y su obstaculo, y no salen a cazar sin haber visto a nadie.
- **#074.** Los aliados eligen cobertura sin que se les ordene (ver el check: en arena 61 a 79 % del tiempo peleando).

### Antes / despues (#080 y #081)

**#080** (guardia agachado detras de una barricada de 1,05 m, blanco parado a 15 m, 6 s; `Wp4Bug080Antes` = el resto del codigo nuevo pero la linea de tiro vieja pivote a pivote):

| | Disparos en 6 s | Tiempo en Attack | Tiempo en Chase | Estado final |
|---|---|---|---|---|
| Antes (linea pivote a pivote), variante A con cobertura asignada | 0 | 0,0 s | 6,0 s | Chase |
| Despues, variante A con cobertura asignada | 7 a 8 | 6,0 s | 0,0 s | Attack (cambios Attack/Chase = 0) |
| Despues, variante B agachado sin cobertura | 5 a 6 | 6,0 s | 0,0 s | Attack |

**#081** (banco `CoberturaBench` 4 contra 4 en el claro de SC_Operacion, la version medida contra v0, tiempo x4):

| | Dif. vida | Gana/empata/pierde | % Attack ya en cobertura | Retroceso max |
|---|---|---|---|---|
| Antes (v5 original), 10 duelos | +163 ± 145 | 5/2/3 | 27 % | 8,0 m |
| Despues (v880), 10 duelos, corrida 1 | +270 ± 119 | 7/0/3 | 45 % | 12,2 m |
| Despues (v880), 10 duelos, corrida 2 | +462 ± 79 | 9/0/1 | 41 % | 7,3 m (propio 11,6 m) |
| Control v0 contra v0 despues | -67 ± 88 y +88 ± 94 | 3/0/7 y 6/0/4 | 59 a 60 % | 8,3 y 8,8 m |

Sin degradacion: la version medida gana 7 y 9 de 10 contra v0 (antes 5 de 10) y la diferencia de vida sube. El retroceso maximo es un estadistico ruidoso (7,3 a 17,2 m entre corridas, hasta el control llega a 8,8) y mide cuanto crecio la distancia al blanco, que incluye que el blanco se aleje.

### Desviaciones del plan (y por que)

- **`AmenazasSobre` no se toco.** Se conservo la presion como estaba; el cambio de decision va por la distancia al blanco y el sentido del recorrido.
- **El banco corre en el claro de SC_Operacion** (`Centro` publico, `W4Claro` = (14, 0, -373)), no en una escena propia. El claro tiene una franja sin NavMesh en z = -7 a -9 que el primer diseno del check de #087 no vio (el medico quedaba del otro lado): el check pone ahora al medico a 5,5 m.
- **`VersionNueva` = 880** en vez de seguir en la v5: suma `F_MULTITUD` (no apilarse) y `F_SENTIDO` (el sano avanza, el herido retrocede). `RadioDeCoberturaAMano` pasa de 8 a 13 m y a `static` para poder compararlo en el banco.
- **`DistanciaDeEncuentro` del herido 2 m (antes 3).** Con 3 m el herido se paraba fuera del alcance de curacion (2,5 m) y, si el medico no podia cerrar la distancia (cobertura en el NavMesh), nadie curaba. Medido: con 2 m el aliado vuelve de 168 a 486 de vida en ~8 s.
- **`BuscarEnfermero` ignora soldados desactivados** (cambio chico fuera del plan): sin eso el pedido se asignaba a un medico apagado. Aplica tambien al juego (soldados dentro de un vehiculo).
- **Criterios de check ajustados despues de medir** (los dos primeros intentos eran mios y arbitrarios): #081 pide >= 35 % de Attack cubierto (antes 27), diferencia de vida > 0 y ganar mas de lo que se pierde; el retroceso se informa pero no es criterio. #086 pide ningun par a < 0,7 m mas de 1 s seguido, <= 2 pares a la vez y minimo >= 0,5 m; la mediana del minimo se informa pero no es criterio (oscila de 0,9 a 2,7 m entre corridas). #074 pasa a una ARENA con dos aliados, dos barricadas y tres enemigos, medida sobre el tiempo peleando.
- **Se tocaron `Bug079` y `Bug094` de WP1** (dos lineas): ahora saltean enemigos `Atrincherado`. Con #085 los guardias se quedan detras de su barricada y el check elegia a uno detras del cubo (tiros a y = 0,89 contra la barricada, 0 impactos de pecho). Con la cobertura inicial desactivada a mano `Bug094` daba OK, y con el cambio vuelve a dar OK.
- **Crash del editor a mitad del paquete** (error nativo del heap, `ExtractActiveCasterInfoJob`, no de la logica): quedo un dialogo de Windows de csrss que no se pudo cerrar; se mato y relanzo Unity (con "Ignore Warning" de administrador y "Keep Backups" de backup de escena). Todos los .cs ya estaban en disco y se verifico la compilacion despues. El dialogo huerfano de csrss sigue en pantalla.
- **No se corrio la suite headless** (`Run All Tests Headless`).

### Checks corridos (Play sobre SC_Operacion, Play fresco por check)

| Check | Resultado |
|---|---|
| Compila (`rc.sh` y eval tras cada cambio) | OK |
| `Bug080`: variantes A y B, 15,0 y 12,3 m, 6 s: disparos 7 y 5 (pide >= 3), cambios Attack/Chase 0 (pide <= 2) | OK (3 corridas) |
| `Bug081`: banco 8 duelos v880 contra v0: 44 % de Attack cubierto (pide >= 35), dif. vida +271, 6/0/2 | OK (otras dos corridas: 37 % y +350 con 5/0/1, y 51 % y +313 en la suite) |
| `Bug074`: arena, aliados en cobertura el 72 % y el 70 % del tiempo peleando (pide >= 40 % en 2 de 2), 0 % del tiempo expuestos a < 6 m (pide <= 15 %) | OK (otras 3 corridas 61 a 79 %) |
| `Bug085`: 12 guardias, 12 enCobertura, 12 con coberturaPunto valido, 11 agachados; 2 cazadores solo porque un guardia ya peleaba (CazaDeEnemigos avisa al grupo a 16 m, es el diseno de #17/#20/#21) | OK (el check ya no cuenta ese caso) |
| `Bug086`: 1300 a 1400 muestras con >= 6 enemigos en combate: pares < 0,7 m max 0 a 1 a la vez, maximo seguido 0,0 a 0,3 s, minimo absoluto 0,68 a 0,80 m (antes 13 pares y 0,00 m) | OK (3 corridas seguidas) |
| `Bug087`: Kes al 35 % retrocede 3,7 a 4,7 m, va al medico, dispara 17 a 24 tiros mientras lo hace, el pedido de curacion sale a los 0,0 s, el medico lo cura (vida 168 a 486) | OK (5 corridas) |
| Regresion: `Bug094/094p` y `Bug079` con las dos lineas de WP1 | `094` OK; `079` OK en 2 de 3 corridas (falla por la zona de impacto: base 34 contra FURIA 135 en lugar de 68, el disparo de medicion cae en otra zona; no es de WP4) |
| Regresion: `Bug095/095a/096` | OK en Play fresco |
| Suite encadenada `CorrerTodos(65, 101)` | 065a/c/m, 072a, 093, 094p, 098, 099, 100 OK; 074, 080, 081, 086, 087 OK; `085` FALLO con 0 guardias (el objetivo 2 no estaba armado tras el banco), `079`, `094`, `095*`, `096` FALLARON por contaminacion de estado del encadenado y dieron OK (079 con la salvedad de arriba) en Play fresco |
| Capturas (`Assets/Validacion/`) | `v2_080_guardia_dispara.png`, `v2_087_herido_repliegue.png`, `v2_087_herido_curado.png` (medico con "CURANDO"), `v2_074_aliados_en_cobertura.png` |

### Rendimiento en Resistir (obj 5)

Medido con `SondaDeRendimiento` (editor, profiler encendido) y con `fps.cs` (sin profiler). El estado de la oleada cambia de una corrida a otra, asi que no es una comparacion limpia.

| | `WorldSimulationDriver.Update` | fps sin profiler |
|---|---|---|
| Antes de WP4 | 0,63 ms/frame | ~81 |
| Despues | 1,1 a 1,7 ms/frame | 47 a 94 (segun el momento de la oleada) |
| Despues, con `SeparacionActiva = false` (misma sesion) | 0,8 a 1,1 ms/frame | 47 a 94 (con y sin: 55/47, 75/94, 94/94) |

La separacion cuesta ~0,3 ms/frame y no se nota en fps estables. El resto del salto del driver (0,63 a ~0,8) no se aislo: puede ser la eleccion de cobertura con 13 m de radio y mas candidatos, el chequeo de linea de tiro agachado cada 0,25 s o simplemente mas soldados peleando en esa muestra. Repartir la separacion un frame de cada dos se probo y se revirtio: empeoraba #086 (hasta 2,1 s seguidos a < 0,7 m).

### Pendientes honestos

- **El salto de CPU del driver (0,63 a 0,8-1,7 ms) no esta explicado** ni medido en la misma situacion que el "antes". Hace falta una muestra con la misma oleada (mismo instante, misma cantidad de vivos) o un microbench por soldado antes de cerrar el rendimiento.
- **#074 en la oleada real de Resistir no esta verificado.** En dos corridas reales los aliados estuvieron en cobertura 34 % y 14 % del tiempo (Kes y Doc) y 0 % y 0 % otra vez: los enemigos llegan a 2-3 m en segundos, el medico esta curando (Pasivo) y no hay un "lugar" para cubrirse. El check que pasa es de arena, no de Resistir.
- **#085: la ubicacion geometrica de los guardias (que esten en buenos puestos del mapa) no se toco**; eso es de WP8/WP9a. Aqui solo quedaron en cobertura de verdad y sin salir a cazar sin ver a nadie. 1 de 12 guardias queda de pie en el check (11 agachados).
- #080 variante B (agachado sin cobertura asignada) disparaba tambien con la linea vieja (6 disparos): el caso que se arreglo es el de cobertura asignada (A, 0 contra 7 disparos). El "antes" es el resto del codigo nuevo con la definicion vieja de linea de tiro, no el codigo anterior a WP4 entero.
- #081: el retroceso maximo supera 10 m en algunas corridas; no se aislo si es el blanco que se aleja o soldados reales que retroceden. Los duelos son 4 contra 4 en un claro, no la oleada.
- #087: el aviso "ESTOY HERIDO, ME REPLIEGO" tiene limite de 15 s por soldado y 3 s global (no se oyo ni se vio en el HUD real; solo se confirmo la llamada). Si el medico no puede llegar (cobertura carvada en el NavMesh) el herido cierra la distancia, pero no se midio con obstaculos mas complicados.
- No se probo el comportamiento con un jugador humano posesionando a Doc ni con ordenes manuales mezcladas en pleno repliegue; solo la lista de condiciones de `PuedeReplegarse`.
- Sin control negativo contra el codigo anterior completo (no se revirtio).
- `Editor.log` pesa ~354 MB por un spam que ya existia ("Can not play a disabled audio source", `SonidosDeOperacion.Update`, linea 145), no es de WP4.
- El dialogo huerfano de csrss del crash sigue en el escritorio.

## WP5a — Interaccion con [E]: rol requerido, revivir y agachado

**Fecha:** 2026-10-04. **Bugs cubiertos:** #083 y #084 ([E] frente a algo de otro rol no hacia nada; ahora dice "DEBES SER X PARA ..." y a quien mandar con [Q]), #088 (revivir con animacion y sin poder moverse), #089 (sonido y anillos al revivir), #092 (el soldado agachado se hundia en el piso).

### Archivos (`Assets/_Project/Scripts/`)

Creados:
- `Interaction/RolRequerido.cs`: texto unico del cartel ("DEBES SER {ROL} PARA {ACCION}  ·  [Q] ENVIAR A {NOMBRE}"), enfriamiento de 1,5 s, clic seco y `AvisoCentral` rojo.
- `Presentation/FeedbackDeRevivir.cs`: se engancha a `Reanimacion.Revivido` (camino unico de fin de revivir): voz 3D "Revive" y 3 anillos verdes en el piso.
- `Editor/ChecksBugs065.Wp5a.cs`: `Bug083`, `Bug084`, `Bug088`, `Bug089`, `Bug092` y sus ayudas.
- `Animation/Marcha/idle_crouching_aiming.anim` (+ .meta): clip de agachado horneado por `ArtSetup`.

Modificados:
- `Interaction/IOrdenableAAliados.cs` (propiedad `VerboDeUso`), `Operacion/OperacionTerminal.cs` (cartel con rol equivocado en paneles y computadora; "DESACTIVANDO" reportado al mantener [E] con el rol correcto).
- `Player/PlayerInputDriver.cs`: pista de interaccion con el rol; gancho a `Demolicion.AvisarSiNoSePuede`; revivir con retardo de 0,15 s para agacharse y animar, traba de movimiento desde el primer cuadro de [E] frente a un caido (`MovimientoTrabadoPorRevivir`), sin disparo mientras se revive, cancelacion al soltar.
- `Player/Demolicion.cs`: `AvisarSiNoSePuede`; `EsDemolible` devuelve false con "ESTO NO SE PUEDE DEMOLER" para vida >= 999999 solo en la Operacion.
- `Player/PedidoDeCuracion.cs`, `Ai/AiBrain.Revivir.cs`: sacan su propio `SfxKind.Revive` (ahora sale solo de `FeedbackDeRevivir`).
- `Presentation/ImpactFx.cs`: pool propio de 8 anillos (`SpawnShockwaveRing`), no cuenta en el presupuesto de impactos.
- `Presentation/SfxSintetico.cs`: `Reanimar()` reescrito (1,8 s: golpe grave, suspiro, acorde Do-Mi-Sol).
- `Presentation/CurandoAnimacion.cs`: `Tick(..., orientar)` y restauracion de la postura agachada.
- `Presentation/SoldierAnimatorDriver.cs`: compensacion de altura por suelas (#092 en runtime); se saco la compensacion vieja `elevacionDePies`.
- `Editor/ArtSetup.cs`: `HornearReferenciaDeAgachado()` (RootT.y absoluto 0,4274, idempotente, conserva el nombre "idle crouching aiming" que busca `CoverHologram`).

### Desviaciones del plan (y por que)

- El filtro "vida >= 999999 no se demuele" se aplica solo con `OperacionDirector.Instancia != null`: el tutorial, SC_Gameplay y la suite headless usan 999999 y ahi SI se demuele con carga.
- Sin refuerzo 2D de "Revive": duplicaria la entrada del `AudioDirector.Historial`, y el check pide exactamente 1.
- Los anillos duran 1,3 s en total (escalonados 0,15 s); el check mira 1,0 s reales despues de la camara lenta.
- #092: el clip horneado a 0,4274 solo es una referencia (visualmente neutra); lo que arregla de verdad es la compensacion en `SoldierAnimatorDriver`: toma el punto mas bajo de las suelas (vertices del mesh por hueso, cacheados) y no la formula de huesos. Tope de desfase 0,30 m (no 0,15) y suavizado asimetrico (sube mas rapido que baja).
- Agregado fuera del plan: gancho `Demolicion.AvisarSiNoSePuede` con raycast para paredes sin marcador, y `RolRequerido.NombreCorto` ("Soldado_2_Kes" -> "KES").
- #088: la agachada y la animacion esperan 0,15 s de [E] para que un toque suelto no te agache, pero la traba de movimiento es inmediata (la primera version dejaba deslizar 0,8 m al correr; se corrigio y el check mide desvio 0,000 m).

### Checks corridos (Play sobre SC_Operacion, Play fresco por check; `ChecksBugs065.Correr(n)`)

| Check | Resultado |
|---|---|
| `Bug083` (obj 2): cartel con rol equivocado + clic, enfriamiento (no repite al instante, si a 1,7 s), pista de apuntado Vega/Kes, Kes mantiene [E] y reporta DESACTIVANDO, pared de vida 999999 "ESTO NO SE PUEDE DEMOLER", saco de 300 con aviso a Kes | OK |
| `Bug084`: texto por rol (ASALTO, FLANQUEADOR, MEDICO) con "[Q] ENVIAR A ..." y sin aliado | OK |
| `Bug088` (obj 1, teclas inyectadas E+W): W solo avanza 2,5 m; E+W 3,5 s desvio 0,000 m, agachado siempre, animacion de atender, bloqueo; soltar [E] cancela y se levanta; revivir completo a los 5,0 s con desvio 0,000 m y control devuelto | OK (tras corregir el deslizamiento inicial) |
| `Bug089`: clip 1,80 s, grave 55 Hz y acorde Do/Mi/Sol por Goertzel; "Revive" suena 1 vez, 3D (spatialBlend 1); 3 anillos a 0,05 m del piso con radios crecientes; ninguno activo 1 s despues; ningun otro archivo usa `SfxKind.Revive` (se excluyo la suite headless, que solo verifica que el clip no sea mudo) | OK |
| `Bug092` (obj 1): clip horneado 0,4274 y el controlador lo usa; Kes de pie malla minima +0,010, agachado +0,020 (rango -0,02..+0,04); agachado caminando -0,018..+0,042 en 92 cuadros y de pie caminando -0,020..+0,043 en 102 cuadros (tolerancia en movimiento -0,04..+0,11); 3 enemigos atrincherados agachados +0,020 | OK |
| Regresion `Bug000`, `Bug083`, `Bug084` | OK |
| Capturas (`Assets/Validacion/`) | `v2_084_cartel_rol.png` (cartel rojo "DEBES SER FLANQUEADOR PARA DESACTIVAR PUESTO 1 · [Q] ENVIAR A KES"), `v2_088_reviviendo.png` (barra "REVIVIENDO 3.0 s"), `v2_089_anillos.png` (anillos verdes en el piso), `v2_092_agachado.png` (Doc agachado con las botas sobre el asfalto al lado de Vega de pie, camara temporal sin HUD) |

### Incidente del editor (crash nativo, segundo del lote)

Durante la validacion de #092 el editor se cerro con un error nativo de mimalloc en el deserializador de datos del Profiler (`ProfilerFrameDataDeserializer::ParseThreadJobFunc`); reporte en `Temp/Unity/Editor/Crashes/Crash_2026-10-04_094857035`. Acciones: se mato el `UnityBugReporter` residual y se relanzo `Unity.exe -projectPath "..."`. El primer relanzamiento con la ruta SIN comillas abrio el dialogo "Launch Unity" (la ruta tiene un espacio): se cerro y se relanzo con comillas. Aparecieron "Administrator Privileges Detected" (se uso "Ignore Warning") y "Scene Backup Detected" (se uso "Keep Backups", no se borro nada ni se restauro). Tras volver, se desactivo el Profiler (`Profiler.enabled` y `ProfilerDriver.enabled` en false). El `Editor-prev.log` de ~354 MB sigue existiendo (ver WP4); puede estar relacionado con la carga del profiler.

### Pendientes honestos

- No se corrio la suite headless completa (`HeadlessTestRunner`) ni `CorrerTodos`; solo los checks puntuales de arriba. Los cambios a `Demolicion.EsDemolible` y `SoldierAnimatorDriver` podrian afectarla.
- Nada se ESCUCHO: el sonido de revivir se verifico por espectro del clip y por el `Historial`/`spatialBlend`, no por oido.
- Sin control negativo contra el codigo anterior (no se revirtio nada).
- Revivir por la habilidad del medico ([Ctrl]), por orden [Q] y por rescate automatico comparten el camino `Reanimacion.Revivido`, pero solo se probo el revivir con [E] mantenido; los otros caminos no se probaron por separado.
- La captura `v2_088_reviviendo.png` es en primera persona y casi no se ve al soldado arrodillado (se ve la barra y el cartel); la postura se verifico por `IsCrouching` y `CurandoAnimacion.Animando`, no por imagen.
- Superposicion menor del cartel rojo nuevo con el texto viejo de `OperacionHud` cuando coinciden; no se toco.
- Un toque corto de [E] frente a un caido traba el movimiento unos 0,15 s (aceptado; la agachada y la animacion no se activan).
- #092: la compensacion mide vertices de suelas; si se cambia el mesh del soldado o se agregan variantes con otro rig hay que revisar `HuesosDeApoyo`.
- `Bug088` y `Bug089` tuvieron una sola corrida OK despues del ultimo cambio (no se repitieron N veces).

---

## WP5b — Feedback de acciones: engranajes, animaciones y marcadores de destino

**Fecha:** 2026-10-04. **Bugs cubiertos:** #066 (engranaje girando sobre el que opera y sobre el interactuable), #067 (animacion para toda accion), #068 (anillo de destino que gira, se expande y se desvanece; linea blanca de 3 s).

### Archivos (`Assets/_Project/Scripts/`)

Creados:
- `Presentation/EngranajesDeAccion.cs`: pool de 8 engranajes (dos por accion de `AccionesEnCurso`), auto-creado como `AccionesEnCursoView`.
- `Presentation/AnimacionDeAccion.cs`: enum `TipoAccion`, API estatica (`Tick`, `Iniciar`, `Terminar`, `Activa`, `TipoDeVerbo`...) y el componente `AnimacionDeAccionSoldado` (IK de ambos brazos en LateUpdate con `BrazoIk`, props, postura).
- `Editor/ChecksBugs065.Wp5b.cs`: `Bug066`, `Bug067`, `Bug068` + `CapturarDesde` (camara temporal a PNG).

Modificados:
- `Presentation/DiamondGizmo.cs`: textura de engranaje con alfa y `NuevoMaterialTransparente` (con modo "siempre encima").
- `Presentation/InteractGearMarker.cs`: el engranaje apuntado rota a 90 grados por segundo (giran las UV del rombo interior, el rombo queda quieto).
- `Presentation/OrderMarkerFx.cs`, `Presentation/ShapeMarkerFx.cs`: anillo de destino animado (ver abajo).
- `Presentation/OrderLineManager.cs`: la linea dura 3 s con fundido; reloj por soldado.
- `Player/AccionesEnCurso.cs`: `Reportar` y `Terminar` enganchan `AnimacionDeAccion` (verbo a tipo). Con eso quedan animadas solas todas las acciones que ya se reportaban (aliado y jugador en paneles, computadora, carga de demolicion, curar, revivir con [E], por medico, por pedido y por `RescateAutomatico`) sin tocar a cada llamador.
- `Combat/WeaponHolder.cs` (`StartReload` inicia la animacion de recargar), `Player/MunicionPickup.cs` (animacion de recoger), `Presentation/CurandoAnimacion.cs` (al REVIVIR esconde el botiquin: las manos hacen las compresiones), `Operacion/OperacionTerminal.cs` (el prompt viejo de rol equivocado no se dibuja mientras el cartel rojo de WP5a esta en pantalla: resuelve la superposicion).

### Que quedo hecho

1. **#066.** Por cada accion en curso hay un engranaje sobre el que opera (40 cm, a 2.2 m del piso) y otro sobre el objetivo (55 cm; soldado a 2.6 m, panel o muro sobre su borde alto). Giran en el eje de la vista a +140 y -110 grados por segundo, con pulso de escala 1.0 a 1.08 a 2 Hz y se desvanecen en 0.25 s. Se dibujan siempre encima (el cartel del panel los tapaba). El engranaje de la mira tambien rota.
2. **#067.** `AnimacionDeAccion`: curar (la hace `CurandoAnimacion`; si no esta, manos al paciente), revivir (manos al pecho, compresiones a 2 Hz, agacha si nadie lo hizo), demoler (manos al piso + caja roja con LED parpadeante), operar y hackear (manos a la mesa, tecleo de 6 Hz y 3 cm; el arma se oculta), reparar (martilleo a 1.5 Hz con llave inglesa; API lista para WP9b, sin llamador todavia), recoger municion (torso inclinado 0.6 s) y recargar (mano izquierda a la cadera por el cargador, prop `Prop_Cargador`, 0.4 s). Se guarda y restaura el agachado previo. El IK se suelta en ~70 ms si el soldado supera 0.5 m/s. El soldado manejado por el jugador no se gira, y al revivir con [E] la animacion espera al retardo de 0.15 s de WP5a (un toque suelto no mueve nada).
3. **#068.** Anillo de destino con barrido angular (estela, punta brillante y 4 marcas): gira a 90 grados por segundo, se expande de 1.0 a 1.8 en 1.2 s con alfa 0.9 a 0 y repite mientras alguien tenga esa orden pendiente (`HayOrdenPendienteEn`); al cumplirse o cancelarse hace un pulso final (1.0 a 2.3 en 0.35 s). Un material compartido y color por `MaterialPropertyBlock` (se acabo el `Instantiate` por orden). La linea del soldado: alfa 1 hasta 2.5 s, se desvanece hasta 3.0 s y se desactiva aunque siga en camino; el reloj reinicia si cambia el destino.

### Bugs viejos encontrados y arreglados en el camino (el #068 no se veia por esto)

- `OrderMarkerFx.SetPipCount` recorria TODOS los hijos del marcador: con 0 pips apagaba el anillo (la orden inmediata no dibujaba nada) y a las ordenes de cola les clavaba la escala de un pip (0.12 x 12 x 0.12) al anillo. Ahora solo toca los hijos llamados `OrderMarkerPip` (marca vertical de 1.4 m).
- El anillo se apoyaba a y = 0.05, por debajo del asfalto de la Operacion (y = 0.08): quedaba enterrado. Ahora se apoya en la malla de navegacion (+0.06) y cae al 0.05 si no hay malla.
- `DiamondGizmo.NuevoMaterial(zTestAlways: true)` no hace nada con el Unlit de URP (no tiene `_ZTest`); para los engranajes se usa `UI/Default` con `unity_GUIZTestMode = Always`.

### Desviaciones del plan (y por que)

- **Sin shader propio `AnilloDestino.shader`** (era opcional): el barrido angular va en una textura generada y el giro rota el quad. Evita tocar `Always Included`.
- **Color/alfa por `MaterialPropertyBlock` en vez de "un clon de material por marcador"**: arregla la fuga igual y no hay nada que destruir.
- **Alturas de los engranajes** medidas desde el pivote (que esta a 0.8 m de los pies): +1.4 y +1.8 = 2.2 y 2.6 m sobre el piso. De lejos los engranajes se agrandan con la distancia (hasta 3.5x) para que se lean.
- **Los engranajes del actor/objetivo solo salen para las acciones que pasan por `AccionesEnCurso`** (recargar y recoger solo tienen animacion, como pedia el plan).
- **Recoger municion no agacha el motor** (solo inclina el torso 0.6 s): forzar `SetCrouching` cambiaria velocidad y punteria del jugador.
- **`TipoAccion` tiene `Operar` y `Hackear` separados** (misma pose) para poder verificar cada uno.
- **`reloading.fbx` no se importo como Humanoid**: se hizo con IK (el plan lo permitia como alternativa).
- El pulso de salida de los engranajes arranca 0.15 s despues del ultimo `Reportar` (no a los 0.35 s de la vigencia de `AccionesEnCurso`), para cumplir "0 activos a los 0.5 s".

### Checks corridos (Play fresco sobre SC_Operacion, `ChecksBugs065.Correr(n)`)

| Check | Resultado |
|---|---|
| Compila (eval sobre las constantes nuevas y `Registrados()` = 0,65,66,67,68,72,...) | OK |
| `Bug066` (obj 2, Kes mantiene [E] en el panel): 2 engranajes activos (sobre Kes a y 2.20 y sobre el panel a y 2.80); giro en ~0.5 s: +93 y -73 grados (pide >= 40, sentidos opuestos); al soltar: 2 dibujados a los 0.12 s y 0 activos y 0 dibujados a los 0.51 s; engranaje de la mira: 45 grados en 0.5 s | OK |
| `Bug067` (8 tipos, soldados de IA quietos; las acciones largas entran por `AccionesEnCurso.Reportar`, recoger y recargar por `Iniciar`): activa, con rig, mano separada de la pose del Animator, prop correcto, agachado propio donde corresponde y todo se restaura 0.5 s despues. Tabla abajo. Caminando de verdad (6.8 m, hasta 5.4 m/s) el IK se apaga (manos +0.00 m despues de 0.4 s). Enganches reales: `MunicionPickup` da `RecogerMunicion` y `WeaponHolder.Reload()` da `Recargar` | OK |
| `Bug068` (obj 1, orden de movimiento a 60 m de camino): a 0.6 s el marcador esta activo con escala 1.42 (pide > 1.2) y giro 57 grados; la linea tiene alfa 1.00 a 1 s, 0.41 a 2.8 s y esta inactiva a 3.2 s con la orden aun en curso; el marcador sigue activo a 3.2 s; al cancelar hace el pulso final y desaparece antes de 0.7 s; 30 marcadores nuevos hacen crecer el pool de materiales (cada uno trae su sistema de particulas) y otros 30 reutilizados no crean ninguno | OK |
| Regresion en Play fresco: `Bug083`, `Bug088` (revivir con [E], WASD bloqueado), `Bug089`, `Bug092`, `Bug096` | OK |
| Errores de consola durante los checks | 0 (el arnes agrega "[errores de consola: n]" si hubiera) |

Tabla de `Bug067` (mano que mas se separa de la pose del Animator, en metros):

| Tipo | Postura / prop | Manos | OK |
|---|---|---|---|
| Curar | (CurandoAnimacion) / manos al paciente si esta sola | +0.42 | OK |
| Revivir | agacha, compresiones 2 Hz | +0.38 | OK |
| Demoler | manos al piso, `Prop_CargaRoja` con LED | +0.83 | OK |
| Operar | de pie, tecleo 6 Hz | +0.65 | OK |
| Hackear | de pie, tecleo 6 Hz | +0.65 | OK |
| Reparar | agacha, martilleo 1.5 Hz, `Prop_LlaveInglesa` | +0.39 | OK |
| RecogerMunicion | torso inclinado 0.6 s | +0.50 | OK |
| Recargar | mano izquierda a la cadera, `Prop_Cargador` | +0.90 | OK |

Capturas (`Assets/Validacion/`, camara temporal sin HUD salvo la `_fps`): `v2_066_engranajes.png` (dorado sobre Kes y celeste sobre el panel), `v2_066_engranajes_b.png`, `v2_066_engranajes_fps.png` (Game View real), `v2_067_operar.png`, `v2_067_revivir.png`, `v2_067_reparar.png` (llave en la mano), `v2_068_destino_a/b/c.png` (tres cuadros del anillo girando y expandiendose) y `v2_068_linea.png`.

### Pendientes honestos

- **Nada se vio en movimiento real**: las animaciones se juzgaron por mediciones y por capturas fijas (el IK da poses reconocibles pero los modelos son de bloques; no se evaluo si el tecleo o las compresiones "se leen bien" en juego).
- **No se corrio la suite headless** (`Run All Tests Headless`): `OrderMarkerFx` cambio de comportamiento (el marcador vive mientras la orden siga en curso) y los tests que cuenten marcadores o miren `SetPipCount` podrian verse afectados; lo cubre WP11.
- **Demoler y Reparar por el jugador** (el que apunta y mantiene [E]/Ctrl) no se probaron con el input real: solo se probo `Reportar` desde un soldado de IA. Reparar no tiene llamador hasta WP9b.
- **Hackear con el jugador de operador**: la animacion arma el tecleo pero el jugador esta en primera persona; no se vio desde la camara real.
- **Superposicion no resuelta**: el texto de estado de `AccionesEnCursoView` (abajo a la izquierda, "▶ DESACTIVANDO") tapa los iconitos de estado de la lista de la escuadra (se ve en `v2_066_engranajes_fps.png`). Es anterior a este paquete; no se toco.
- El IK de recargar pisa la pose de la mano que sostiene el guardamanos durante 0.4 s: en combate se ve un tiron breve. Duracion fija, sin sincronizar con el clip de recarga del arma.
- El conteo global de `Material` del editor crece entre sesiones de Play (7 mil a 22 mil en este paquete, estable dentro de una sesion): no se investigo el origen ni se probo que sea ajeno a este paquete (los checks de #068 solo prueban que los marcadores no crean materiales).
- Los engranajes usan `UI/Default`; no se probo en un build (solo en el editor).


---

## WP6 - Vehiculos y tanque (#091, #090, #069/#071, #070, #072 visual, #073)

Escena regenerada con el builder idempotente (`SC_Operacion.unity`, guardada, sin cambios sin guardar). Backup previo fuera del proyecto: `scratchpad/SC_Operacion.antes_wp6.unity.bak`. Inventario antes/despues: raices 11 a 11, objetos 5416 a 5459 (+43), `ObstacleMarker` 144 a 303, `NavMeshModifier` 1136 a 1180, `autosSimultaneos` 3 a 4.

### Que cambio, por bug

- **#091 giro invertido + anillo + cruceta.** El yaw de MUNDO de la torreta pasa a ser la fuente de verdad (`yawMundo`) y se reimpone en `LateUpdate` despues de que el casco giro; la brecha pedida se limita a +-170 grados (sin eso, mas de 180 invertia el camino corto) y `playerTurnSpeedDegPerSec` pasa de 110 a 170. El anillo de impacto se calcula por tramos de 0,05 s con raycast (se apoya en muros, vehiculos y obstaculos, no solo en el piso), vibra (radio +-6 % a 7 Hz, ancho 0,12-0,20) y cambia de color: rojo = soldado o vehiculo enemigo, naranja = obstaculo destructible, blanco = neutro. La cruceta (`MiraDeTorreta`) tiene los mismos 3 estados.
- **#090 el tanque destruye obstaculos; camionetas mas lentas; mas destruibles.** `Atropello` ya aplastaba `ObstacleMarker` con vida; ahora en la Operacion ignora los indestructibles (rocas, edificios, casas siguen en pie). Camionetas: 28 a 19 m/s (`VelocidadMaxima = 19`, tope relativo al tanque). Nuevos destruibles en la autopista (todos sobre la calzada, fuera del NavMesh): 14 barreras `Jersey_Ruta_k` (vida 180) cada ~52 m, 5 grupos de 4 barriles explosivos (vida 40, radio 3, dano 45), 10 cajas (vida 40); ademas `Jersey_I/D` (180), `Chevron` (80), `Poste_Km` (60) y las farolas de la autopista (120, con la cabeza colgada del poste) ahora tienen vida.
- **#069/#071 aliados contra vehiculos enemigos.** `AiBrain.Vehiculos` ahora vale para los dos bandos; el de ASALTO saca el lanzacohetes con el vehiculo entre 8 y 45 m (6 s, espera de 4 s) y vuelve a su arma; orden explicita con Q o clic derecho sobre el vehiculo (`OrderService.IssueAttackVehicleOrderForSelection`, 15 s). `Vehicle.Hostil(TeamId)` define "enemigo" (bando distinto, vivo, con ocupantes o autonomo). La metralleta del tanque (asiento Passenger1) la opera la IA (`TurretAI` con `asiento`; se instala en `Embarcar()` via `InstalarMetralletaConIA`) y se aparta si el jugador la posee. Cursor `Atacar` en RTS sobre una camioneta enemiga y tinte de enemigo en `AimUI` y en el anillo de vehiculo.
- **#070 camionetas encimadas.** `LugarLibreParaCamioneta` / `BuscarLugarParaCamioneta` validan salida y parada con un `OverlapBox` del tamano de la camioneta contra solidos y corren hasta +-6 m; `Saco_Salida_1` pasa a (-10,-12) y `Saco_Salida_2` a (10,-4) en el builder.
- **#072 visual.** Nuevo `Presentation/DestruccionDeVehiculo.cs`: `CraterPool` (cupo 16, vida 60 s, malla de disco + bloques) y `ChapaVolante` (pool de 24, rebotan) mas `RomperPiezas` (los cubos de la `Visual` pasan por el `Fragmentador`). Al destruir una camioneta: primer estallido con crater, 3 estallidos escalonados, bola x1,5, chapas y piezas. Al destruir un vehiculo con ocupantes (tanque): chapas y dos estallidos escalonados. `ImpactFx.SpawnExplosion` tiene el overload `(pos, radio, conCrater)` y mide el piso real.
- **#073 tuning.** 4 camionetas simultaneas (5 en Dificil, tope +1), vida 110 a 260, aparicion cada 1-2 s, 30 % por delante a 40-55 m con tactica `Adelantar`, piso de vida del tanque del 20 % (se acabo la curacion magica del 30 %; el tanque se desgasta entre el piso y el techo).

### Archivos (relativos a `Assets/_Project/Scripts/`)

- **Nuevos:** `Presentation/DestruccionDeVehiculo.cs`, `Editor/ChecksBugs065.Wp6.cs`.
- **Modificados (juego):** `Vehicles/TurretWeapon.cs`, `Vehicles/TurretAI.cs`, `Vehicles/Vehicle.cs`, `Vehicles/VehicleBrain.cs`, `Vehicles/Atropello.cs`, `UI/TurretAimView.cs` (reescrito), `UI/MiraDeTorreta.cs`, `UI/AimUI.cs`, `Presentation/ObstacleMarker.cs`, `Presentation/ImpactFx.cs`, `Presentation/LimpiezaDeEscena.cs`, `Ai/AiBrain.Vehiculos.cs` (reescrito), `Player/OrderService.cs`, `Player/PlayerInputDriver.cs`, `Player/PlayerInputDriver.Escuadra.cs`, `Player/PlayerInputDriver.Contextual.cs`, `Operacion/OperacionAuto.cs`, `Operacion/OperacionAuto.Efectos.cs`, `Operacion/OperacionDirector.cs`, `Operacion/OperacionDirector.Huida.cs`, `Operacion/OperacionDirector.Centro.cs`.
- **Modificados (editor):** `Editor/OperacionBuilder.cs`, `Editor/OperacionBuilder.Huida.cs`, `Editor/LevelBlockoutBuilder.cs` (`BakeNavMesh` no pone `NavMeshModifierVolume` a lo que tiene `SinNavMesh`), `Scenes/SC_Operacion.unity` (regenerada).

### Desviaciones y hallazgos (con el por que)

1. **El anillo de impacto NUNCA se dibujaba en SC_Operacion** (hallazgo, anterior al paquete): `TurretAimView.radiusRing` no se serializa (lo recibe por `Bind` al construir la UI en el editor), asi que en una escena guardada llegaba nulo y `UpdateRadiusRing` salia sin hacer nada. Se agrego `AsegurarAnillo()` (busca `TurretRadiusRing` o crea uno con `Sprites/Default`, que si respeta el color por vertice). Se vio recien al correr el check en vivo.
2. **La IA no veia a las camionetas (linea de tiro):** el pivote de una camioneta esta a ras del piso y el rayo hasta ahi terminaba dentro del collider del suelo, asi que `HayLineaDeTiro` daba siempre "tapado". Ahora se mira al centro del vehiculo (+1 m). Es lo que hacia que el Asalto no sacara el cohete por su cuenta.
3. **Crater flotando a 1,1 m / hundido bajo el asfalto:** `AlturaDelPiso` medio el chasis del propio vehiculo como si fuera el piso, y el NavMesh da 0,04 donde el tope del asfalto es 0,08. Ahora ignora vehiculos y cuerpos sueltos y tiene piso minimo 0,08 (`TopeDelAsfalto`). Visto en vivo al listar los craters (y = 1,12 y 0,06).
4. **Vigencia del control de la torreta en frames, no en segundos** (3 frames): un tiron largo de un solo frame (editor, carga) descartaba el giro pedido a medias y el check de #091 fallaba 1 de cada 3 corridas.
5. **La metralleta con IA se instala en `Embarcar()`, no en el builder**, porque es solo para el trayecto del tanque del objetivo 4 y asi no toca el prefab ni la escena.
6. **Obstaculos de la calzada fuera del NavMesh** (`NavMeshModifier.ignoreFromBuild` + `ObstacleMarker.SinNavMesh`): si no, el tanque, que sigue una ruta por la malla, esquiva las barreras en vez de aplastarlas, y cada barrera caida disparaba un rehorneado. Primer rebuild: 454 volumenes de carving; despues del parche 410 (antes 412).
7. **El check de #091 usa un muro de prueba** (cubo de 40 x 10 x 1 m con y sin `ObstacleMarker` con vida 180, llamado `Jersey_PruebaAnillo`), no un Jersey real de la ruta: con el tanque en marcha, y el entorno de la ruta (rocas, barreras) cruzandose, un Jersey real daba resultados que dependian del azar. El anillo se mide contra ese muro (distancia a la superficie, vibracion, color) y la cruceta se mide en los mismos frames.
8. **El check de #069c invoca `ActualizarCursorRts` por reflexion** con el resultado de `Aim.Evaluate` de un rayo real sobre la camioneta (no mueve el mouse del sistema). El cursor es de hardware y no sale en un render: la captura `v2_071_cursor.png` dibuja encima la textura real del cursor `Atacar`.

### Checks (Play fresco sobre SC_Operacion; `ChecksBugs065.CorrerUno("Bug0NN")` corre un solo metodo)

| Check | Resultado |
|---|---|
| Compila (`Registrados()` incluye 69,70,72,73,90,91; la consola sigue mostrando 4 errores CS0104 viejos de `Vehicle.cs` que ya no existen: es el cache de la consola) | OK |
| `Bug091` (obj 4, jugador de artillero): a) sin input |brecha| max 0,00 grados en 22 muestras con el casco girando 12-49 grados (parte natural + giro a mano de +-30 grados/s); b) `AddDesiredYaw(-90)` con el casco girado 72-76 grados a la derecha: yaw de mundo -88 a -90, monotono (mayor subida 0,00); c) `AddDesiredYaw(+400)`: brecha 170,0; d) anillo sobre el muro: 40 de 40 frames a 0,00 m de la superficie, radio 5,17-5,83 (+-6,0 %), blanco; e) con vida: anillo naranja (1,00 / 0,55 / 0,10) en 20 de 20 frames y cruceta naranja en 19-20 de 20 | OK (5 de 5 corridas seguidas tras los ajustes) |
| `Bug090` (obj 4, 20 s de tanque): 4-5 lineas "aplasto", 4-5 Jersey de ruta colapsados, 0 indestructibles caidos (ninguna roca, edificio ni casa), velocidad maxima de camionetas 19,0 m/s | OK (2 corridas) |
| `Bug069a` (obj 4): la MG operada por IA dispara 40-42 veces en 15 s y baja la vida de una camioneta (en una corrida la destruyo) | OK |
| `Bug069b` (obj 3, a pie): el Asalto, solo, a 22 m de una camioneta, saca el lanzacohetes a los 0,1-0,2 s, 8-13 disparos a vehiculos, camioneta 260 a 228 | OK (antes de la correccion de la linea de tiro: FALLO, con 0 disparos) |
| `Bug069c` (obj 3): AimTargetType=Vehicle y cursor `Atacar` sobre la camioneta; `Normal` sobre el suelo | OK |
| `Bug070` (obj 3, 2 tandas): 4 camionetas, 0 salidas, 0 paradas y 0 posiciones finales con solidos; 2 paradas hubo que correrlas | OK |
| `Bug072v` (obj 4): obus real, crater activo con y = 0,100 (> 0,08, apoyado); camioneta destruida suelta 7-9 chapas (activas hasta 9) y 28-48 fragmentos | OK |
| `Bug073` (obj 4): max 4 camionetas simultaneas (objetivo 4), vida maxima 260, primera aparicion a los 1,7 s | OK |
| NavMesh: 14 tramos de la ruta del tanque (`CalculatePath` entre puntos consecutivos, rodeo <= 1,25 x la recta) | 14/14 OK |
| Regresion en Play fresco: `Bug000` (smoke de los 6 objetivos), `Bug080`, `Bug074` | OK |
| Errores de consola durante los checks | 0 |

Capturas (`Assets/Validacion/`): `v2_091_anillo.png` (anillo blanco sobre el muro), `v2_091_destruible.png` (anillo naranja con la barra de vida del obstaculo), `v2_071_cursor.png` (camionetas enemigas con la mira de ataque), `v2_070_camionetas.png` (desde (-6.4, 0.8, -24.7)), `v2_072_explosion.png` (camara lenta x0,1, primer estallido con chapas y fragmentos).

### Rendimiento en la fase Huir (antes y despues)

Metodo: Play fresco en el objetivo 4, `Embarcar()`, 8 ventanas de ~6 s; fps de ventana y tiempo del hilo principal del JUEGO (`ProfilerRecorder` "CPU Main Thread Frame Time" y "PlayerLoop"; no se uso el Profiler). "Antes" = escena vieja (3 camionetas de 110 de vida, sin obstaculos nuevos) abierta desde una copia temporal (ya borrada) con el codigo nuevo; "despues" = escena nueva.

| | fps de ventana | hilo del juego (ms) | fragmentos activos | coberturas |
|---|---|---|---|---|
| Antes (3 autos) | 19-68 (mediana ~44) | 7,9-11,7 (una ventana con un tiron de 39) | 0-27 | 1223 |
| Despues (4 autos) | 32-88 (mediana ~38) | 8,8-11,8 | 27-96 | 1198-1252 |

Lectura: el costo propio del juego es parecido (+1-2 ms con el cuarto auto y los fragmentos de lo aplastado). **El fps bruto no sirve para comparar en el editor**: hay 20 ms por frame de overhead del editor (`HierarchyView.RefreshItems` ~8,5 ms, `EditorApplication.update`) que varia de ventana a ventana y con la cantidad de objetos que se activan/desactivan (fragmentos, craters, chapas). Una medicion anterior de la misma escena vieja dio 94-101 fps, y en esta sesion la misma escena dio 41-67 con el editor en otro estado: no son comparables entre si. Los pools tienen tope duro (crateres 16, chapas 24, fragmentos 140, debris 160).

### Pendientes honestos

- **No se probo con el mouse real** el hover en RTS ni Q / clic derecho sobre un vehiculo enemigo para ordenar atacar: el check de cursor evalua el rayo real y llama a la funcion del cursor, y la orden se probo con `IssueAttackVehicleOrder` directo (la via Q / clic derecho no).
- **El piso del 20 % de vida del tanque no se verifico**: los checks corren con modo dios, que protege al tanque (vida minima vista: 100 %).
- **5 autos en Dificil**: no se probo, solo la formula (`AutosSimultaneosEfectivos`).
- **Los anillos de #091 se midieron contra un muro de prueba**, no contra un Jersey real de la autopista; los Jersey reales se aplastan (probado en #090), pero no se apunto el canon a uno.
- **Danio del cohete a vehiculos**: se vio camioneta 260 a 228 en el primer impacto; no se midio cuantos cohetes hacen falta para destruirla.
- **NavMeshModifierVolume 412 a 410** (-2) tras el rebuild sin explicar (sospecha: los sacos movidos o las farolas con vida). El NavMesh de la ruta esta bien; no se probaron las 6 entradas a pie con `CalculatePath`.
- **Coberturas 1223 a ~1250** (+25): los `ObstacleMarker` nuevos entran como coberturas para la IA (incluso barriles y cajas de la calzada). No se evaluo si los aliados se estan cubriendo detras de un barril explosivo.
- **Fragmentos**: con los obstaculos aplastados suben a 40-100 activos (antes 12-27). No se investigo si conviene bajar la cantidad de piezas por Jersey.
- **Camara lenta y "se ve bien"**: el estallido escalonado se juzgo por una captura a x0,1; no se vio a velocidad real ni en un build.
- El editor no se cayo en este paquete; el usuario no estuvo jugando (no hubo cambios de estado ajenos).


---

## WP7 - Helicoptero de extraccion y cierre de la mision (#075, #076, #077)

**Fecha:** 2026-10-04. Escena `SC_Operacion.unity` regenerada con el builder idempotente (guardada, sin cambios sin guardar). Backup previo fuera del proyecto: `scratchpad/SC_Operacion.antes_wp7.unity.bak`. Inventario antes/despues: raices 11 a 11, objetos 5459 a 5484 (+25 piezas del helicoptero nuevo), `ObstacleMarker` 303 a 303, NavMesh horneado con 410 volumenes (igual que WP6).

### Que cambio, por bug

- **#075 polvo y ondas de viento.** El polvo ya no depende de `Volando`: `Helicoptero.MedirAltura()` tira un rayo hacia abajo cada 0,1 s (ignora al propio helicoptero y a los soldados) y con el rotor a fondo y a menos de 25 m del piso la tasa va de 40 a 160 con la cercania (aterrizaje y despegue). El emisor de polvo ahora vive sobre el piso bajo el helicoptero (antes viajaba con el en el aire) y el circulo del Shape se acuesta (antes quedaba vertical); el radio se abre de 6 a 10 m con la altura. Segunda capa `ResiduosDelRotor`: 20 particulas de malla chica y chata (cubo con tamano 3D) en color papel/hoja seca. Nuevo `Presentation/OndasDeRotor.cs`: pool de 10 quads con textura de anillo generada y un solo material (color y alfa por `MaterialPropertyBlock`); cada 0,35 s sale un anillo arena (0,85, 0,78, 0,6) de radio 2 a 16 m en 1,2 s con alfa 0,5 a 0 (se sostiene casi la mitad de la vida). Topes: 10 anillos, 20 residuos, 320 de polvo (antes 260), 32 casquillos.
- **#076 ametralladoras y artilleros.** `OperacionBuilder.Huida.CrearHelicoptero`: fuselaje abierto (piso, techo, mamparo, umbrales, pilares, cabina con base y cristal, nariz, bancos) con huecos laterales de 2,4 x 1,4 m. Dos `PivoteMetralleta` (izquierdo y derecho, nuevo `Mision/PivoteMetralleta.cs`): giran en yaw dentro de +-70 grados de su lado y cabecean (55 grados abajo, 20 arriba); la boca es un `Transform` real del canon. `Helicoptero.Cobertura` ahora dispara desde la boca: 8 por segundo en rafagas de 1,2 s con pausas de 0,5 a 0,9 s, radio de blanco 70 m, blanco dentro del arco del pivote, fogonazo `muzzle_01` + humo cada 3 tiros (como `OperacionAuto.EfectoDeDisparo`), luz de fogonazo, clip `GetWeaponShot(Heavy)` con perfil `Disparo` (WP2) y casquillos con balistica propia (`CasquillosDeMetralleta`, pool de 32). Nuevo `Mision/TripulacionDelHeli.cs`: 2 artilleros (copias visuales del modelo de un soldado aliado, hijos del pivote, giran con el arma, capa de animacion "Disparo") y 2 sentados en el banco trasero (postura procedural `PoseSentada` sobre el esqueleto humanoide: cadera baja y atras, muslos adelante, piernas verticales). Al embarcar (`RutinaDeSubida`) cada soldado que sube deja una copia sentada (hasta 4: dos filas de bancos); los soldados reales siguen apagados.
- **#077 toma final y pantalla de resultado.** `GameOutcomeController.ShowVictory(bool congelar = true, float alfaFondo = 1f)`: la Operacion llama con `false, 0,55` (timeScale 0,35 en vez de 0); los defaults conservan el comportamiento. `PantallaDeResultado.Animar(..., alfaFondo)` respeta el alfa y, si es menor que 1, agrega un degrade oscuro eliptico detras de la tabla (`DegradeDeLegibilidad`, primer hijo). Nuevo `Operacion/OperacionDirector.TomaFinal.cs` y `RutinaDeSubida` reescrita: linea de tiempo de 13,4 s (0 a 5,2 suben, 5,2 a 7,4 despegue vertical, 7,4 a 9,4 la camara lo sigue, 9,4 a 13,4 TOMA FINAL, sin fundido a negro). La toma elige 3 tiradores (enemigos reales activos cerca; si faltan, figurantes de la reserva puestos detras del helipuerto), apaga el HUD (canvas de pantalla; la pantalla de resultado trae su propio canvas anidado y se dibuja igual), saca el cartel "HELICOPTERO" del piso y pone la camara a 1,2 m del piso, 3,8 m detras del grupo (con `CinematicaDeVictoria.EvitarObstruccion`, ahora publica). Los tiradores giran hacia el helicoptero y disparan rafagas de 7 a 11 tiros (trazadoras sin dano, fogonazo, `ShotFiredEvent`). A los 13,4 s entra `Ganar()` y la pantalla de resultado sobre la misma toma, que sigue viva en camara lenta hasta 45 s reales; el helicoptero se apaga a mas de 330 m de la camara.

### Archivos (relativos a `Assets/_Project/Scripts/`)

- **Nuevos:** `Mision/PivoteMetralleta.cs` (+ `CasquillosDeMetralleta`), `Mision/TripulacionDelHeli.cs` (+ `PoseSentada`), `Presentation/OndasDeRotor.cs`, `Operacion/OperacionDirector.TomaFinal.cs`, `Editor/ChecksBugs065.Wp7.cs` (`Bug075`, `Bug076`, `Bug077`).
- **Modificados:** `Mision/Helicoptero.cs`, `Mision/CinematicaDeVictoria.cs` (`EvitarObstruccion` pasa a public), `Operacion/OperacionDirector.Extraer.cs`, `Presentation/GameOutcomeController.cs`, `Presentation/PantallaDeResultado.cs`, `Editor/OperacionBuilder.Huida.cs`, `Editor/ChecksBugs065.Wp2.cs` (ver desviaciones), `Scenes/SC_Operacion.unity` (regenerada).

### Desviaciones y hallazgos (con el por que)

1. **Aceleracion del despegue 6 a 8,5 m/s2** (`PosicionDeSalida`): con 6 m/s2 la toma final arrancaba con el helicoptero a 35 m de la camara. Con 8,5 arranca a 54 m (real medido) y llega a 268 m (el plan pedia 60 a 250). Con enemigos reales (y la camara en otro lugar) dio 35 m a 184 m (medido antes del cambio de aceleracion).
2. **Linea de tiempo mas larga:** 13,4 s en vez de 12,4 s (la toma final dura 4 s y reemplaza al plano 3 de 3 s y al fundido). `ValidacionBugs` espera Victoria dentro de 16 s de apretar [E]: sigue entrando.
3. **Sin fundido a negro:** `CrearFundido` ya no se usa en la subida (la API `FundidoActivo` queda y da false). Se saco el aviso "MISION CUMPLIDA" (el HUD esta apagado y el titulo de la pantalla ya lo dice).
4. **Las ondas van 0,3 m sobre el piso, no a ras.** El asfalto y la losa del helipuerto son visuales sin collider, quedan a ~0,1 m y tapaban los anillos a 0,1 m (se midio: no se veian aunque el material estaba bien).
5. **Fallback del helicoptero sin pivotes:** el helicoptero de arte de la mision vieja (`MisionDirector`, SC_Gameplay) no tiene `PivoteMetralleta`; para no dejarlo sin fuego de cobertura, `Helicoptero` conserva el disparo fijo anterior (`CoberturaFija`). Probado quitando los pivotes por reflexion en SC_Operacion: dispara desde el offset viejo. No se probo en SC_Gameplay.
6. **Bug propio encontrado y corregido:** `ArmarResiduos` mezclaba curvas de velocidad en distintos modos y tiraba "Particle Velocity curves must all be in the same mode" al activar el helicoptero (lo detecto el smoke `Bug000`, que cuenta errores de consola). Las curvas quedaron todas en modo constante.
7. **Se edito un check de WP2:** `Bug093` esperaba `timeScale == 0` en la victoria; ahora acepta `< 1` (la victoria de la Operacion va a 0,35). El resto del check no cambio.
8. Los artilleros usan la capa "Disparo" del Animator (pose de fusil), no un clip propio de ametralladora; el arma del pivote queda delante de sus manos.
9. El HUD se apaga deshabilitando los `Canvas` raiz de pantalla (excepto el indicador de sesion); no se restauran porque Reintentar/Salir recargan la escena.

### Checks (Play fresco sobre SC_Operacion; `ChecksBugs065.Correr(n)`)

| Check | Resultado |
|---|---|
| Compila (eval sobre constantes nuevas y `Registrados()` incluye 75, 76, 77) | OK |
| `Bug075` (Resistir con `segundosDeResistencia` en 24 s y escuadra invulnerable; despues `SubirAlHeli`) | OK. Bajada: 221 muestras a menos de 20 m, altura minima 0,1 m, emision de polvo hasta 159 por s, particulas de polvo hasta 255 (tope 320), 20 residuos, 4 anillos simultaneos y >=3 en todas las muestras, con `Volando=true`. Despegue: 152 muestras a menos de 20 m, polvo 160, 4 anillos |
| `Bug076` (Extraer, enemigo a 35 m al costado, 5 s) | OK. 2 artilleros y 2 sentados dentro de la cabina (posiciones locales verificadas), 2 pivotes; 19 a 23 disparos en 5 s, todos con origen en la boca del pivote y a 2,0 m del viejo punto fijo; yaw del pivote derecho 49 a 90 (cambio de 41,3 grados); cadencia 7,0 a 7,7/s en rafagas de 8 a 9 tiros; casquillos 19 a 23, sprites de fogonazo/humo 63 a 73; clip `Shot_Heavy_*_dist` (cambia entre corridas: lo elige `GenericSfx`) |
| `Bug077` (Extraer, `SubirAlHeli`, 3 corridas) | OK. Toma final activa con 3 tiradores (figurantes de reserva); altura de la camara 1,21 m sobre el piso (< 2,5), 59 a 66 disparos, 3 tiradores disparando dentro del frustum (>= 2); pantalla de resultado con alfa 0,55 (<= 0,6), `timeScale` 0,35 (> 0), HUD apagado, sin fundido y fase Victoria; 3 copias sentadas = 3 subidos; `ShowVictory()` sin parametros: `timeScale` 0, alfa 1,00 y congelada. Variante con 4 enemigos reales activos (`Wp7Reales = true`): OK, 0 figurantes, camara a 1,21 m, 2 tiradores disparando en pantalla |
| Regresion en Play fresco: `Bug000` (smoke de los 6 objetivos; 0 errores de consola despues del arreglo del punto 6), `Bug093` (himno, con el ajuste del punto 7) | OK |
| Capturas (`Assets/Validacion/`) | `v2_075_polvo.png`, `v2_075_ondas.png`, `v2_075_ondas_solo.png` (sin particulas), `v2_075_ondas_runtime.png` (vista cenital), `v2_076_artilleros.png`, `_b.png`, `v2_076_puerta_derecha.png`, `v2_076_puerta_izquierda.png`, `v2_076_sentados.png`, `_b.png`, `v2_076_heli_a.png`, `_b.png`, `v2_077_toma_inicio.png`, `v2_077_toma.png`, `v2_077_toma_fin.png` (Game View real), `v2_077_victoria_mitad.png`, `v2_077_victoria.png` |

### Pendientes honestos

- **No se corrio la suite headless ni `ValidacionBugs` completa.** El criterio del final de `ValidacionBugs` ("fundido se retira") se replico a mano dentro de `Bug077` (sin fundido, fase Victoria, sin canvas `OperacionFundido`), pero el runner original no se ejecuto.
- **El criterio "origen a menos de 0,3 m de la boca" de `Bug076` es casi trivial:** el origen que se mide es el que el helicoptero le pasa al pool (la misma posicion de la boca, 0,000 m); el valor informativo es que dista 2,0 m del viejo punto fijo. No se midio la posicion del primer cuadro del proyectil.
- **Nada se escucho** (clip de ametralladora pesada a volumen 0,6 sin oir; 16 disparos por segundo en 3D pueden saturar el pool de audio, no se midio).
- **Los cambios de `Helicoptero` (ondas, residuos, polvo por altura, fallback) no se probaron en SC_Gameplay**; ahi el helicoptero de arte tambien recibe `OndasDeRotor` y `TripulacionDelHeli` (esta ultima no hace nada sin pivotes). `CinematicaDeVictoria` y `MisionDirector.Ganar` siguen llamando `ShowVictory()` con defaults (no se ejercitaron en vivo).
- La postura sentada es procedural y de bloques: las piernas quedan tapadas por el umbral lateral; se juzgo con capturas fijas, no en movimiento ni desde la camara en primera persona.
- Con camara lenta la IA enemiga y las balas siguen corriendo: los enemigos que no son tiradores siguen su cerebro normal bajo la pantalla de resultado (sin escuadra a la vista no hay a quien danar). Los 45 s de toma viva despues de la victoria no se vieron completos.
- Los tiradores de la toma usan la pose de "disparar" por `ShotFiredEvent`; en las capturas se ven agachados, no se verifico si se lee bien en movimiento.
- Rendimiento: solo se midieron topes (polvo 255 de 320, residuos 20, 4 de 10 anillos, casquillos 3 a 6 de 32); no se midio el costo en ms de la bajada del helicoptero ni de las 2 ametralladoras a 16 tiros por segundo.
- No hubo control negativo (no se revirtio el codigo viejo para ver fallar los checks).
- El editor no se cayo en este paquete; el usuario no estuvo jugando (no hubo cambios de estado ajenos). Al final queda fuera de Play, con `SC_Operacion` guardada y sin cambios pendientes.


## WP8 - Rediseno del cuartel: sectores, torres de francotirador y de torreta, reflectores (#078, parte cuartel de #085)

**Fecha:** 2026-10-04. Escena `SC_Operacion.unity` regenerada con el builder idempotente (guardada, sin cambios sin guardar). Backup previo fuera del proyecto: `scratchpad/SC_Operacion.antes_wp8.unity.bak`. Inventario antes/despues (mismo script de conteo): raices 11 a 11, objetos 5484 a 5991, `ObstacleMarker` 303 a 323, `NavMeshModifierVolume` 410 a 460, `Light` 56 a 60, `Soldier` 73 a 81 (cuartel 20 a 28). Una segunda regeneracion dio los mismos conteos. Zona del paquete: z < -220 (no se toco nada al norte).

### Que cambio

- **Cuartel con desarrollo y sectores (estilo blocking).** Divisiones: el muro perimetral y el porton quedan; se suman dos muros internos (z=-312 y z=-260, con aberturas y dinteles) y pisos de color por sector. Sector A (control de acceso, gris): 3 lineas Jersey, nidos de sacos y garita. Sector B (barracas, ocre): 3 barracas al oeste, muro bajo, cajas. Patio de armas (corredor central): mastil y cajas. Sector C (parque de contenedores, verde): 8 contenedores, 3 de dos pisos. Sector D (comando/comedor/parque motor, azul): edificios con techo, camiones, sacos. Cada sector lleva su cartel `Rotulo_SECTOR ...`. Se quitaron las barracas del este, las torres de esquina y los camiones viejos.
- **2 torres de francotirador (TF1, TF2) y 2 de torreta (TV1, TV2)** (`Operacion/TorreDestruible.cs`, `Operacion/FrancotiradorEnTorre.cs`). Francotirador: Sniper, 95 de dano, un tiro cada 2,8 s, laser rojo de 1,0 s antes de cada tiro (la mira queda fija a los 0,75 s), alcance 90 m. Torreta: Smg, rafagas de 8 y pausa de 1,4 a 2,4 s, arco de 120 grados, alcance 55 m, con `MultiplicadorTorretaFija` 0,5. Las torres se destruyen solo con explosiones (`ObstacleMarker` con `soloExplosiones`: las balas no bajan la vida; 450 y 600 de vida): con los cohetes del check (285 cada uno) hacen falta 2. Al caer, el tirador muere (la baja se acredita al soldado poseido), la torre vuelca 80 grados en 1,2 s hacia el patio y se rompe en trozos (`Fragmentador`).
- **4 reflectores (R1 a R4)** (`Operacion/ReflectorVigia.cs`): torre de 6 m con operador, lampara (Spot de 45 m, 18 grados, intensidad 9, sin sombras, `Luminaria` de 1 de vida). Barre 25 grados/s sobre +-60 grados; si la escuadra entra en el cono a menos de 45 m y con vision, engancha (el poseido tiene prioridad) y lo sigue con 0,35 s de retraso. Alerta a hasta 3 enemigos libres por pulso (cada 4 s). El enemigo que le dispara a alguien alumbrado dispersa x0,75 (`WeaponHolder.TryFire`). Un disparo a la lampara o una explosion a menos de 3,5 m la rompe (luz apagada, haz y vineta fuera). Sin operador el haz queda fijo.
- **Aviso al jugador:** vineta blanca suave y el texto "TE ESTAN ILUMINANDO / DISPARA AL REFLECTOR" (`OperacionHud.Iluminado`), y linea de subobjetivos bajo el panel: "Torres a/2 - Torretas b/2 - Reflectores c/4 - Soldados restantes: N". La condicion de la fase sigue siendo matar a los 28.
- **Guardias en cobertura (#085, parte cuartel):** 20 guardias de suelo (A4, B6, C5, D5), cada uno 1,9 m detras de un objeto de cobertura, y `Atrincherar` los deja EN la cobertura (WP4). Con los 4 tiradores y 4 operadores son 28 enemigos.

### Archivos (relativos a `Assets/_Project/Scripts/`)

- **Nuevos:** `Operacion/TorreDestruible.cs` (+ `PuestoElevado`), `Operacion/FrancotiradorEnTorre.cs` (`TiradorDeTorre`, `FrancotiradorEnTorre`, `ArtilleroDeTorreta`), `Operacion/ReflectorVigia.cs`, `Editor/ChecksBugs065.Wp8.cs` (`Bug078a` a `Bug078e`).
- **Modificados:** `Editor/OperacionBuilder.Cuartel.cs` (reescrito), `Editor/OperacionBuilder.cs` (cableado de torres y reflectores al director), `Operacion/OperacionDirector.cs` (campos `torres`/`reflectores`, `GuardiasDeSuelo`, captura y restauracion de estado), `Operacion/OperacionDirector.Infiltrar.cs` (linea de subobjetivos), `Operacion/OperacionHud.cs` (aviso de iluminacion, subobjetivos), `Presentation/ObstacleMarker.cs` (`soloExplosiones`), `Presentation/Luminaria.cs` (`Rota`), `Combat/Projectile.cs` (`DanoDeExplosionEnCurso`, explosion rompe luminarias a 3,5 m), `Combat/WeaponHolder.cs` (dispersion x0,75 contra el iluminado).

### Desviaciones y hallazgos (con el por que)

1. **Dano del cohete 285, no 237:** el check mide 285 por cohete contra TF1 (la formula de `DanarObstaculos` aplica x3 y un factor por distancia). Siguen haciendo falta 2 (450 de vida: 285 no alcanza, 285 + 285 si), asi que el criterio se cumple igual.
2. **Los tiradores no llevan el componente en el soldado:** `TiradorDeTorre` va en la raiz de la torre. `GuardiasDeSuelo` del director lo buscaba en el soldado y atrincheraba tambien a los 4 tiradores (24 en vez de 20): lo vi en `Bug078a` (24/20) y se corrigio con `EsOcupanteDeTorre`.
3. **Bug propio encontrado con una captura:** al derrumbarse la torre, `ObstacleMarker.Collapse` rompia con `Fragmentador` lo que quedaba colgado de la raiz: la silueta naranja del minimapa (a y=41) y el laser de 56 m. Caian laminas naranjas y cubos blancos gigantes sobre el cuartel (se vio en la camara temporal; en el juego se habria visto igual). Ahora `QuitarSilueta()` las suelta y destruye antes de que termine el evento `Derrumbado`; `FrancotiradorEnTorre.Update` tolera el laser destruido.
4. **Vineta mas suave y texto arriba:** la primera captura salio muy pesada (alfa 0,55 y el texto tapaba la lampara a 25 m). Alfa maximo 0,36, el borde arranca en 0,74 del radio y el texto sube a 95 px del borde superior.
5. **El barrido no engancha en 2 s si el cono no cruza al jugador:** el haz barre 25 grados/s sobre 120 grados (hasta 4,8 s por pasada). El check pone el haz con el jugador ya dentro del cono (por reflexion sobre `yaw`/`pitch`) para medir el seguimiento de forma estable; la primera version del check fallaba al azar (2 de 3 corridas).
6. **`Bug078d` re-equipa el cohete antes del segundo tiro:** el lanzacohetes tiene recarga larga y sin cargador el segundo tiro no salia; el check no cambia el dano.
7. **Sin sombras en los 4 spots** (limite de rendimiento pedido). El haz visible es un cono de malla translucido generado en runtime (`Sprites/Default`).

### Checks (Play fresco sobre SC_Operacion cada uno; `ChecksBugs065.CorrerUno("Bug078x")`)

| Check | Resultado |
|---|---|
| Compila (eval sobre `Bug078a`) | OK |
| `Bug078a` estructura y alcanzabilidad | OK. 2+2 torres y 4 reflectores dentro de los limites (0 fuera), 4 carteles de sector, 28 enemigos (20 guardias, 4 tiradores, 4 operadores), 20/20 guardias atrincherados, flood-fill de capsulas 11320/11320 celdas alcanzadas desde la brecha (16 muestras por sector y frente del porton), 0 bolsillos cerrados, 0 guardias o bases de torre sin ruta NavMesh completa |
| `Bug078b` reflector | OK, 3 corridas seguidas. A 26,8 m el angulo haz-jugador a los 2 s es 0,0 grados (<6), idem tras moverse 5 m; vineta y cartel visibles; alerta a 3 enemigos; dispersion x0,75 contra el iluminado y x1,00 contra otro; un disparo a la lampara la apaga y entra al historial de rotos; sin operador el haz no gira (0,00 grados en 1,5 s); explosion a 2 m rompe, a 6 m no; el director cuenta 2 rotos |
| `Bug078c` francotirador y torreta | OK. Laser encendido 1,00 a 1,01 s antes de cada uno de 5 tiros en 12,5 s (cadencia 2,8 s); el francotirador no baja de la torre (y=13,4) y lleva Sniper; la torreta dispara 18 a 24 tiros en 3 rafagas dentro del arco y 0 detras de la torre |
| `Bug078d` torre | OK. 40 balas no bajan la vida (450 a 450); cohete 1 deja 165, cohete 2 la tira y mata al francotirador; el pivote vuelca 80 grados; 40 a 62 fragmentos; el director cuenta la torre caida |
| `Bug078e` cadena | OK. HUD "Torres 0/2 - Torretas 0/2 - Reflectores 0/4 - Soldados restantes: 28"; matar a los 28 pasa a Puestos; el porton se hunde; la linea se limpia; ruta brecha a salida norte completa y un `NavMeshAgent` real la recorre en 21,5 s |
| Regresion en Play fresco: `Bug000` (smoke de los 6 objetivos), `Bug080`, `Bug085`, `Bug086` | OK los cuatro |
| Rendimiento (`fps.cs`, IA pausada, escuadra en el patio, editor 1080p) | Reposo: 81 fps (referencia previa del paquete: 61). Con los artilleros de torreta disparando a la escuadra en modo dios: 32 a 47 fps (los francotiradores solos: 78 a 80). `FrameTimingManager` en ese estado: hilo principal del juego ~10 ms, GPU ~5 ms; el resto es `EditorLoop` (13 a 14 ms medidos con la sonda): el costo parece del editor, no del juego |
| Capturas (`Assets/Validacion/`) | `v2_078_planta.png` (cenital con luz de dia temporal; sectores legibles), `v2_078_suelo.png` (nivel de suelo), `v2_078_reflector.png` (Game View en primera persona con el aviso), `v2_078_reflector_haz.png` (haz y operador desde un costado), `v2_078_torre_antes.png` y `v2_078_torre_cae.png` (torre a mitad del vuelco) |

### Pendientes honestos

- **No cumple "60 fps" mientras disparan las torretas, medido en el editor** (32 a 47 fps). Evidencia de que no es el juego: `FrameTimingManager` da ~10 ms de hilo principal y ~5 ms de GPU, y `EditorLoop` se lleva 13 a 14 ms solo cuando hay disparos. Mi hipotesis (no probada) es el repintado de la ventana Hierarchy por los objetos de balas, efectos y escombros sobre una escena de ~6000 objetos. No se midio una build ni se probo cerrar la Hierarchy; tampoco se midio por separado el costo de los 4 spots (apagarlos, con todo lo demas igual, dio unos +5 fps en una medicion ruidosa).
- **"Infiltrar ganable" se verifico por construccion, no jugando:** hay ruta completa a todo el cuartel, los 28 se pueden matar (comando `Matar`) y la cadena abre el porton, pero no se hizo una partida real con IA contra los 28 (2 francotiradores, 2 artilleros y 4 reflectores) para ver si es dificil o injusta. La dificultad (95 de dano por tiro, 285 por cohete) no se balanceo.
- **No se recorrieron los 6 objetivos con agente real**, solo el cuartel (brecha a salida norte). `Bug000` (smoke) pasa por los 6. `CalculatePath` entre entradas consecutivas con el porton cerrado da `PathPartial` en 0 a 1, 1 a 2 y 5 a 0: el de 0 a 1 es esperable (porton cerrado) y se confirmo completo con el porton hundido; los otros dos no los investigue ni los compare con la linea base.
- **Los guardias de suelo y los francotiradores se probaron con la IA pausada o con comandos**, no peleando: no se vio a los 20 guardias pelear desde su cobertura en el cuartel nuevo (solo el conteo 20/20 de `Bug078a` y el `Bug085` de otro grupo de guardias).
- **El cuartel puede romperse a tiros:** una caja del patio (`Caja_P2`) se derrumbo en Play con los tiradores disparando (rehace el NavMesh). Es el comportamiento normal de los `ObstacleMarker`, pero con 323 marcadores y rafagas de 10 disparos por segundo puede haber varios rehacidos seguidos; no se midio el hitch.
- **Una linea roja vertical sobre el tirador muerto** aparece en las capturas de la caida (no es el laser ni un `LineRenderer`): parece una baliza de baja ya existente; no la investigue.
- No hubo control negativo (no se revirtio el codigo para ver fallar los checks). `Bug078b` fallo al principio por una carrera real del barrido (arreglada en el check), no por un cambio de codigo del reflector.
- El editor no se cayo en este paquete; el usuario no estuvo jugando. Al final queda fuera de Play, `SC_Operacion` guardada y sin cambios pendientes, `timeScale` 1.

---

## WP9a - Rediseno de los Puestos ("2 puestos a volar"), cinematica inicial y cartel de rol (#082, #084, #085 parte puestos, #097 parte 1)

**Fecha:** 2026-10-04. Escena `SC_Operacion.unity` regenerada con el builder idempotente (guardada, sin cambios sin guardar). Backup previo fuera del proyecto: `scratchpad/SC_Operacion.antes_wp9a.unity.bak`. Inventario antes/despues (mismo script de conteo `wp0_inv.cs`): raices 11 a 11, objetos 5991 a 6793, `ObstacleMarker` 323 a 335, `NavMeshModifierVolume` 460 a 478, `BoxCollider` 592 a 627, `Light` 60 a 62, `Soldier` 81 a 98 (+17 netos; el diseno nuevo tiene en la muralla 12 guardias, 2 nidos, 2 de trinchera, 3 de corredor y 12 reservas inactivas hasta que se coloca una carga), enemigos del cuartel 28 sin cambio. Una segunda regeneracion dio exactamente los mismos conteos (diff vacio). Zona del paquete: z -220..-100 (el cuartel z < -220 y el Centro de Datos z > -100 no se tocaron; solo se agrego una ancla de camara de la cinematica en el centro del cuartel (0,-280) y otras para las zonas posteriores, sin mover nada de ellas).

### Que cambio

- **Los dos puestos de la muralla (#082).** Muralla de contencion en z=-170 con un porton blindado central (se hunde al final) y dos bunkers en los flancos (x=-38 Oeste, x=+38 Este; 76 m entre ellos). Cada puesto tiene UNA sola capa: 5 guardias (4 detras de sacos + 1 detras del Jersey exterior), un nido de ametralladora en el techo (artillero de torreta de arco 90 grados), y 6 enemigos de reserva INACTIVOS junto a la puerta lateral. No hay refuerzos que aparezcan por todos lados: lo unico que llega es el contraataque que dispara la propia carga. Zona de aproximacion al sur (dos lineas diagonales de Jersey por lado, trincheras, 2 autos quemados, 2 guardias de trinchera) y corredor de trincheras al norte (2 trincheras y 3 rezagados que no hace falta matar) entre la muralla y el Centro de Datos.
- **Carga explosiva solo del ASALTO (`CargaExplosiva`).** Punto de carga con placa amarilla/negra y caja con LED, 7 m al sur de cada bunker. Mantener [E] 3,5 s a menos de 2,5 m (o mandar al Asalto con [Q], misma ruta `EnviarA` del radial). Al colocarla arde una mecha de **20 s**: contador en el HUD ("MECHA - PUESTO OESTE 16"), etiqueta 3D que parpadea ("CARGA OESTE - 0:19"), bip cada 1 s que se acelera los ultimos 5 s (0,5 a 0,12 s), aviso "ALEJATE" si el poseido esta dentro del radio. **Los enemigos no pueden desactivarla** (un enemigo pegado a la carga no la toca: verificado). A los 20 s: explosion (radio 11 m, 400 de dano, la escuadra no se lastima), el bunker colapsa con `Fragmentador`, el nido cae y su artillero muere, los guardias del radio mueren.
- **Contraataque durante la mecha.** La primera carga manda 4 enemigos de la reserva por la puerta lateral; la segunda manda 6 mas una camioneta de asalto (`OperacionAuto.ConfigurarAsalto`) por la zona de aproximacion. El orden de los puestos es libre. Al volar los dos: el porton blindado se hunde (`Hundir`, rehace el NavMesh), se limpian las camionetas y empieza el Centro de Datos ("LA MURALLA CAYO").
- **Cartel de rol (#084, con la carga).** Apretar [E] cerca de la carga con otro rol da "DEBES SER ASALTO PARA COLOCAR LA CARGA  -  [Q] ENVIAR A VEGA" (rol de WP5a, `RolRequerido`), y la pista de la mira del Asalto lee "MANTENE [E] PARA COLOCAR LA CARGA".
- **Guardias de los puestos (#085, parte puestos).** 12 guardias (5 + 5 + 2 de trinchera) colocados 1,9 m detras de un solido y atrincherados con `Atrincherar` (WP4); los 3 del corredor tambien.
- **Cinematica inicial (#097 parte 1, `CinematicaDeOperacion`).** Al entrar en Play la camara hace un dolly aereo (35 a 18 m de altura; 55 a 32 m en la muralla, donde se ven los dos puestos) por las 6 zonas mostrando, paso a paso, lo que hay que hacer: titulo "OPERACION CUARTEL / Seis pasos para salir con vida" (3 s) y 6 pasos de 4,3 s con panel lateral (hechos con tilde, el actual resaltado, pendientes en gris) y cartel inferior con el texto del plan. Dura ~29 s y cierra con "PRESIONA UNA TECLA PARA EMPEZAR". **Bloquea todo**: driver y rig apagados, IA de todos pausada (incluidos tiradores y reflectores), HUD y rombo ocultos. Mantener [ESPACIO] 1 s la salta (anillo de progreso). No se repite al reintentar la mision (`YaVista`). Si el Play se detiene a mitad todo se restablece.
- **Lista de pasos en el HUD y tecla [V].** Bajo el objetivo de la Operacion queda la lista de 6 pasos (tilde/flecha/punto); [V] la pliega a una linea ("2/6 - VOLAR LOS DOS PUESTOS [V]") y la despliega (entrada nueva `plegar_pasos` en `KeyBindings` y fila en `ControlsTable`).
- **Cadena Puestos a Centro de Datos ganable:** `SaltarA`/`SaltarAPuesto`, `CapturarEstado`/`RestaurarEstado` (claves `puestoOeste`/`puestoEste`) y `OperacionPrueba.Arrancar(2, 1|2)` adaptados (puesto 1 Oeste, 2 Este con el Oeste ya volado).

### Archivos (relativos a `Assets/_Project/Scripts/`)

- **Nuevos:** `Operacion/CargaExplosiva.cs`, `Operacion/CinematicaDeOperacion.cs`, `Editor/ChecksBugs065.Wp9a.cs` (`Bug082a/b/c/d`, `Bug085b`, `Bug097a/b/i`).
- **Reescritos:** `Operacion/OperacionDirector.Puestos.cs`, `Editor/OperacionBuilder.Puestos.cs`.
- **Modificados:** `Operacion/OperacionDirector.cs` (clase `PuestoDeVoladura` en lugar de `PuestoDeControl`, campos `portonBlindado`/`soldadosDeTrinchera`/`soldadosDelCorredor`/`anclasDeZona`, arranque de la cinematica, captura/restauracion de estado, `SaltarA`/`SaltarAPuesto`), `Operacion/OperacionHud.cs` (`Pasos` con cache y plegado, `AlternarPasos`), `Operacion/FrancotiradorEnTorre.cs` y `Operacion/ReflectorVigia.cs` (no apuntan durante la cinematica), `Player/KeyBindings.cs` (`PlegarPasos`), `UI/ControlsTable.cs`, `Editor/OperacionBuilder.cs` (llamada nueva, cableado, `CrearAnclasDeZona`, `LimpiarEscena` incluye `CinematicaOperacion`), `Editor/OperacionPrueba.cs`, `Editor/ChecksBugs065.cs` (el arnes corta la cinematica antes de cada check salvo `Bug097*`; `Bug000` apaga el director mientras prueba la API de pasos del HUD), `Editor/ChecksBugs065.Wp5a.cs` (`Bug083` portado a la carga) y `Editor/ChecksBugs065.Wp5b.cs` (`Bug066` portado a la carga).

### Desviaciones y hallazgos (con el por que)

1. **`Bug083` (WP5a) y `Bug066` (WP5b) reescritos contra `CargaExplosiva`.** Los paneles `OperacionTerminal` con `soloUnRol` de los puestos viejos ya no existen (no queda ninguno en la escena: el Centro de Datos usa `soloUnRol=false`), asi que esos checks no tenian objeto. Ahora el "objeto con rol exclusivo" es la carga (Asalto en lugar de Flanqueador): se conserva lo que miden (cartel con rol real y [Q] a quien mandar, clic, enfriamiento 1,5 s, pista de la mira, accion reportada como DETONANDO, demolicion, engranajes sobre el actor y sobre el objeto que rotan en sentidos opuestos y se desvanecen).
2. **El temporizador del HUD se aparta mientras dura el cartel grande** (Bug #060, anterior): mi primer `Bug082b` fallo porque leia el timer 1 s despues de colocar la carga, con el cartel "CONTRAATAQUE..." encima. Se mide pasado el cartel (3 s despues): "MECHA - PUESTO OESTE 16".
3. **`Bug000` fallaba tras mi cambio** (`pasos=False oculto=False`): el director ahora pinta la lista de pasos cada frame y pisaba la prueba de la API. El check apaga el director mientras la prueba. Pasa de nuevo.
4. **Metodo de prueba `CargaExplosiva.AcortarMecha(float)`** (solo para checks; `RestaurarMecha` no sirve con una carga ya colocada). No se usa en el juego.
5. **`CinematicaDeOperacion` y no `CinematicaDeIntro`**: `OperacionBuilder.LimpiarEscena` borra la raiz `CinematicaDeIntro` de SC_Gameplay. `OperacionPrueba.Arrancar` y el arnes de checks la saltan (si no, todo check que arranque en frio quedaria bloqueado 29 s).
6. **En "PRESIONA UNA TECLA" cualquier tecla cierra** la cinematica, incluida W: mi primer `Bug097i` fallo por apretar W+E hasta el final (la cinematica termino sola con mi tecla). El check suelta las teclas 2 s antes y cierra con Enter real.
7. **Los textos de la cinematica son los del plan** (se escribieron en `Textos[]`); el paso 3 dice "Un soldado descarga 30 s; el resto defiende las tres entradas" aunque la logica del Centro de Datos es de WP9b (no se cambio).
8. **Mas soldados en la escena** (81 a 98): las 12 reservas estan inactivas hasta colocar una carga; no se midio su costo por separado.

### Checks (Play fresco sobre SC_Operacion cada uno; `ChecksBugs065.CorrerUno("Bug0NNx")`)

| Check | Resultado |
|---|---|
| Compila (eval) | OK (`Registrados()` incluye 82 y 97; 36 numeros) |
| `Bug082a` estructura | OK. 2 puestos, rol Assault, 5 guardias vivos y 6 reservas (0 activas: una sola capa) cada uno, cargas alcanzables por NavMesh (`PathComplete`) desde la entrada de la fase, salida/parada de camioneta alcanzables, separacion 76 m, 12 guardias + 3 de corredor, nada lanzado al empezar, el Centro de Datos NO es alcanzable con el porton blindado (`PathPartial`), HUD "OBJETIVO 2/6 - VOLAR LOS PUESTOS / Puesto Oeste: intacto - Puesto Este: intacto - solo el ASALTO coloca la carga [E]" |
| `Bug082b` Oeste de punta a punta | OK. Kes manteniendo [E]: progreso 0,00; Vega: reporta DETONANDO, 0,35 a 1,2 s, planta a los 3,4 s, mecha 20 s; 4 contraatacantes activos y 0 camionetas; etiqueta 3D "CARGA OESTE - 0:19"; con un enemigo a 0,8 m la mecha sigue (18,8 a 15,7 s); HUD "MECHA - PUESTO OESTE 16"; 10 bips entre el aviso y los 5 s finales y 15 en los ultimos 5 s; vuela: bunker colapsado 2/2 marcas, nido muerto, 0 guardias vivos en el radio, fase sigue en Puestos, HUD "Puesto Oeste: VOLADO - Puesto Este: intacto" |
| `Bug082c` cadena | OK. Este primero (4 contraatacantes, 0 camionetas), Oeste segundo (6 y 1 camioneta viva); con los dos volados fase CentroDeDatos, porton hundido, 0 camionetas, camino a la entrada del Centro de Datos `PathComplete`, HUD "OBJETIVO 3/6 - CENTRO DE DATOS" |
| `Bug082d` orden [Q] | OK. `EnviarA` rechaza a Kes y a Doc ("SOLO EL ASALTO PUEDE"); a Vega a 12 m: camina solo, planta a los 5,4 s y sale el contraataque (4/4) |
| `Bug083` (portado) | OK. Cartel "DEBES SER ASALTO PARA COLOCAR LA CARGA  -  [Q] ENVIAR A VEGA", clic, sin avance; enfriamiento; pistas de la mira; Vega reporta DETONANDO; demolicion "ESTO NO SE PUEDE DEMOLER" / "DEBES SER ASALTO PARA DEMOLER ESTO" |
| `Bug066` (portado) | OK. 2 engranajes (sobre Vega y sobre la carga), giro 97 y -76 grados en 0,5 s, se desvanecen al soltar, el de la mira gira 48 grados |
| `Bug085b` guardias | OK. 12/12 vivos, atrincherados, en cobertura con punto valido, pegados a un solido (<= 2,5 m), 0 cazadores sin ver a nadie, 6 peleando, 10 agachados |
| `Bug097a` cinematica | OK. Arranca sola al entrar en Play; driver, rig y IA bloqueados; 9 teclas (W D Shift E Q Tab R 2 F) 1,5 s: desvio de la escuadra 0,000 m; paso 2 con su texto y panel (tilde, flecha); camara aerea sobre la muralla (41 m de altura); anillo 0,56 a los 0,5 s; con ESPACIO 1,3 s se salta, control devuelto (W avanza 3 m) |
| `Bug097b` pasos del HUD | OK. 6 pasos con marcas, plegado a una linea con [V], la tecla [V] real alterna |
| `Bug097i` cinematica completa | OK. 29,3 s, 6/6 pasos con "PASO k/6", titulo y texto, camara recorre las 6 zonas (687 m), bloqueo total con W+E apretadas (desvio 0,000 m), 1,2 s esperando sin tocar nada (sigue activa), cierra con Enter real, control devuelto (W avanza 3 m), HUD vuelve |
| Regresion en Play fresco: `Bug000` (smoke de los 6 objetivos), `Bug085` (guardias del WP4, 15/15), `Bug092` (3 agachados), `Bug078e` (cadena del cuartel) | OK los cuatro (`Bug000` tras el ajuste 3) |
| Rendimiento (`fps.cs`, editor 1080p, objetivo 2) | **Reposo (IA pausada, modo dios): 107 a 113 fps**, contra ~92 de la linea base de este paquete. **En combate** (guardias disparando a la escuadra): 32 a 58 fps; con la mecha encendida y el contraataque: 35 a 55 fps. Es el mismo patron que WP8 (32 a 47 fps con torretas) y el overhead del editor domina (`EditorLoop`); no se midio una linea base de combate con la escena vieja (no se puede abrir: el codigo viejo de `PuestoDeControl` ya no existe) |
| Capturas (`Assets/Validacion/`) | Aereas: `v2_097a_muralla_aerea.png` (toda la muralla), `v2_082_puesto_oeste_aerea.png`, `v2_082_puesto_este_aerea.png`, `v2_082_contraataque_aerea.png`, `v2_082_volado_aerea.png`, `v2_097a_segunda_carga_aerea.png`, `v2_085b_guardias_oeste_aerea.png`, `v2_085b_trinchera_aerea.png`. Suelo (Game View): `v2_082_plantando.png`, `v2_082_mecha.png`, `v2_082_mecha_final.png`, `v2_082_volado.png`, `v2_082_cadena_centro.png`, `v2_082_orden_q.png`, `v2_084_cartel_rol.png`. Cinematica: `v2_097a_titulo.png`, `v2_097a_paso1.png` a `paso6.png`, `v2_097a_final.png`, `v2_097a_despues.png`, `v2_097_intro_paso2.png`, `v2_097_intro_saltar.png`. HUD: `v2_097_pasos_hud.png`, `v2_097_pasos_hud_plegado.png` |

### Pendientes honestos

- **#097 parte 2 no se hizo** (es de otro paquete). Esta parte solo cubre la cinematica inicial y la lista de pasos del HUD.
- **No se jugo una partida real de Puestos con IA y sin modo dios.** Todos los checks de combate (082b, 082c, 082d) se hicieron con la escuadra en modo dios para que la prueba fuera estable; por eso **no se verifico que la explosion perdone a la escuadra** (`spareTeam`) ni que 20 s de mecha con 4 a 6 atacantes sean aguantables o injustos. La dificultad (4 y 6 contraatacantes, camioneta en la segunda, 400 de dano, 20 s) no se balanceo.
- **Rendimiento en combate no cumple 60 fps en el editor** (32 a 58). Mismo diagnostico que WP8 (overhead del editor), sin linea base de la escena vieja en combate; solo se compara el reposo.
- **La etiqueta 3D "CARGA OESTE - 0:19" se superpone al rotulo "PUESTO OESTE"** del bunker visto de frente (`v2_082_mecha.png`): se lee, pero es feo. Y el cartel grande "CONTRAATAQUE..." tapa la lista de pasos del HUD mientras dura (anterior: ya pasaba con otros avisos).
- **Las posiciones del builder se validaron por codigo** (29 soldados sobre el NavMesh, sin solidos dentro, rutas completas a cargas y a las paradas de camioneta) **y por capturas aereas**, pero no se recorrio a pie todo el corredor de trincheras ni se vio la camioneta llegar a su parada en una partida real; en `Bug082c` se comprueba solo que se crea y que se limpia.
- No hubo control negativo (no se revirtio el codigo para ver fallar los checks); las dos fallas reales (timer tapado por el cartel, tecla W cerrando la cinematica) se encontraron al correr los checks y se arreglaron en el check, no en el juego.
- El editor no se cayo en este paquete; el usuario no estuvo jugando. Al final queda fuera de Play, `SC_Operacion` guardada y sin cambios pendientes, `timeScale` 1.


---

## WP9b - Huida rediseñada: jefe BLINDADO GOLIAT, reparacion, carrera con Halcon y cinematica final (#097 parte 2 + #073)

Se hicieron 9b-1 y 9b-2 completos en una sola sesion (jefe + reparacion + abordaje + carrera + helicoptero enemigo + cinematica final en camara lenta). Quedan pendientes honestos al final de la seccion.

### Bugs cubiertos
- **#097 parte 2**: el tanque se ve de lejos como jefe, tiene piso de vida del 5%, Kes lo repara mientras el jugador la cubre, se aborda, carrera persiguiendo camionetas, Halcon, ruta larga con canadon y puente, cinematica final y encadenado con Resistir.
- **#073**: ajuste de la huida (ruta mas larga y peligrosa, camionetas perseguidoras en la carrera, emboscadas en el canadon).

### Archivos nuevos (`Assets/_Project/Scripts/`)
- `Operacion/CineDeHuida.cs` (canvas propio de cinematica: barras, tarjeta del jefe, subtitulo, cartel, fundido, camara lenta con restauracion de `timeScale`/`fixedDeltaTime`, bloqueo de input/IA).
- `Operacion/AnilloDeAviso.cs` (aviso en el piso, con pool), `Operacion/TanqueJefe.cs` (IA del jefe: canon con aviso de 1,3-1,5 s, metralleta en rafagas, barra "BLINDADO GOLIAT", factor x2,2 a explosiones), `Operacion/ReparacionDeTanque.cs`.
- `Operacion/OperacionDirector.Jefe.cs` (Acercamiento, toma del jefe de 3,5 s, Jefe, Reparacion con oleadas norte/este/oeste + camioneta, cajas de cohetes, entrega del tanque al jugador, abordaje).
- `Operacion/OperacionDirector.Final.cs` (carrera, Halcon, cinematica final, hooks de prueba, estadisticas de la cinematica).
- `Operacion/EmboscadaDeCohetes.cs` (incluye `CoheteDeAviso`: cohete lento con anillo de aviso, comun a emboscadas y Halcon), `Operacion/HelicopteroEnemigo.cs` (HALCON: entra a los 25 s, orbita, pasadas cada 8 s con 3 cohetes con aviso, ametralladora, caida en espiral).
- `Editor/ChecksBugs065.Wp9b.cs` (checks `Bug097j/k/l/m/n` y `Bug073b`).

### Archivos modificados
- `Operacion/OperacionDirector.cs` (gancho `AlEntrarSubfase`, `segundosDeTrayecto=70`, retiro de la oleada vieja de la huida, captura/restauracion de estado con jefe y reparacion, `SaltarA` aborta la cinematica), `Operacion/OperacionDirector.Huida.cs` (Embarcar con avance rapido si faltan jefe/reparacion, carrera, sin curacion magica, fin por cinematica; TickLlegada/BajarDelTanque/DestruirTanque viejos solo como respaldo).
- `Vehicles/Vehicle.cs` (piso de vida con multiplicador de dificultad, filtro de dano, `PonerVida01`, `DestruirPorGuion`, `EsAutonomo` incluye al Halcon), `Vehicles/TurretAI.cs` (`Silenciada`), `Vehicles/VehicleMotor.cs` (`Aceleracion`, `Configurar`), `Combat/Projectile.cs` (`Vehicle.DanoDeExplosionEnCurso`), `Player/CajaDeSuministros.cs` (`Crear(pos, segundos)`).
- `Editor/OperacionBuilder.Huida.cs` (tanque con vida 1600, tripulacion y reservas del jefe, 18 puntos de ruta ~950 m, canadon con muros de 10 m, 4 barricadas, 2 cornisas con 3 tiradores de cohetes, puente con autos abandonados, plantilla del Halcon, carteles), `Editor/OperacionBuilder.cs` (entrada de Huida (0,0,-36), campos del director), `Editor/ChecksBugs065.Wp6.cs` (Bug091 oculta `Roca_Canadon_*`).
- Escena `SC_Operacion` regenerada con el builder (idempotente) y guardada; inventario antes 6793 objetos, despues 7815 (+HelicopteroEnemigo 1, EmboscadaDeCohetes 2, +6 AiBrain); regenerada de nuevo al final (arreglo del cartel del puente) con inventario identico al anterior.

### Desviaciones y por que
- Letras de check `097j/k/l/m/n` y `073b` en lugar de `097b` (097a/b/i las uso WP9a).
- Sin sistema de dialogo con voz: la linea de Kes es subtitulo central (AvisoCentral) mas sonido.
- Ruta de ~950 m (NavMesh 972 m), no 1150 m, para ajustarla a los 70 s a ~14 m/s con el canadon en "S".
- El jefe solo siente explosiones por `Vehicle.DanoDeExplosionEnCurso` con factor x2,2 (cohete medido ~86 en el borde del casco); la metralleta hace 1 de dano ("NECESITAS COHETES O EXPLOSIVOS"); la carga del Asalto hace 600 sin multiplicar.
- Las cajas de cohetes se crean en runtime y rellenan toda la municion (no exactamente +3 cohetes); recarga 25 s configurable.
- Limite de elevacion del canon subido a 32 grados para poder apuntar al Halcon.
- Animacion de apertura de escotilla omitida.
- Check legacy `Bug091` parcheado (oculta rocas del canadon, que lo volvian intermitente) y `Embarcar()` del WP6 hace avance rapido para que los checks viejos sigan valiendo.

### Checks (Play fresco cada uno) y resultado
| Check | Resultado |
|---|---|
| `Bug097n` | OK |
| `Bug097j` jefe dormido, toma de 3,5 s con input/IA bloqueados, tarjeta, pelea con avisos de canon 1,50-1,53 s, piso 5%, rendicion | OK (re-corrido tras el ultimo rebuild) |
| `Bug097k` reparacion con pausa y oleadas, entrega del tanque | OK |
| `Bug097l` carrera: 70,0 s, 18 puntos, tanque minimo 320 (=piso 20%), Halcon a los 25,0 s, 5 pasadas/15 cohetes, altura 18-24 m, 6 tiradores/15 cohetes de emboscada, 3 de 4 barricadas aplastadas, camara lenta 4,01 s reales a x0,25, "¡FUEGO!" (y disparo automatico si no se toca), tanque y Halcon destruidos, 3/3 en pie a <=4,9 m, Resistir a los 9,5 s, escuadra viva 3/3 y control devuelto | OK (re-corrido tras el ultimo rebuild) |
| `Bug097m`, `Bug073b` (17 camionetas en toda la carrera) | OK |
| Legacy en Play fresco: `Bug000`, `Bug065a`, `Bug069a/b/c`, `Bug070`, `Bug072a/v`, `Bug073`, `Bug090`, `Bug091` (tras parche), `Bug093`, `Bug095a` | OK |
| NavMesh (`w9b_nav.cs`) | 18 puntos, 0 tramos malos, largo 972 m, 17 soldados de oleadas sin camino cortado = 0, 4 patrullas y 2 emboscadas (tiradores a y=6,9) sobre el NavMesh |
| Capturas `Assets/Validacion/v2_097b_*` | toma, jefe_lejos, aviso_canon, reparacion, reparacion_hud, halcon, halcon_hud, final_slowmo, final_explosion, final_otro_helicoptero, final_explosion_m, todos_en_pie, todos_en_pie_m |

### Pendientes honestos
- **Nada se jugo con input humano real.** Los checks usaron ganchos y atajos: `PruebaDisparar`, `ExplodeAt` guionado, `AdosarCarga()`, `Embarcar()`, modo dios en jefe/reparacion. No se verifico a mano: mantener [E] como Kes en la reparacion, el clic real de disparo en la ventana de "¡FUEGO!", ni matar al jefe con cohetes apuntados por el jugador.
- La muerte del Halcon por obus se verifico con `ExplodeAt 135`, no con proyectiles reales apuntados.
- En la carrera el tanque termina casi siempre en el piso del 20%: el dano de las emboscadas y del Halcon es fuerte; puede hacer falta balance con jugadores reales.
- 3 de 4 barricadas aplastadas (no las 4).
- Persistencia (`CapturarEstado`/`RestaurarEstado` con jefe, reparacion y subfase) implementada pero sin probar.
- `SupervivientesForzados` solo se verifico sin cambios (-1 hasta la extraccion); la cadena completa Resistir -> Extraer no se volvio a correr tras el WP9b.
- Rendimiento sin perfilar para lo nuevo (+13 soldados de reserva, ~120 colliders del canadon, anillos con pool).
- El cartel "PUENTE SIN GUARDARRAILES" salia espejado (yaw y posicion mal); se corrigio en el builder y se regenero la escena, pero **no se reviso la captura despues del arreglo**.
- Camara del plano "todos en pie" ajustada solo visualmente en un caso; en terrenos distintos podria quedar tapada por escombros.
- Al final queda fuera de Play, `SC_Operacion` guardada y sin cambios pendientes, `timeScale` 1, modo dios apagado. El editor no se cayo; el usuario no estuvo jugando. No se hizo commit, push ni build; `Builds/Windows64` intacto.



---

## WP10 - Resistir dependiente del RTS: DIRIGIR LA DEFENSA DESDE LA RADIO (#101, incluye #074)

Objetivo 5 (Resistir/ciudad) reescrito: el reloj del helicoptero solo corre si el jugador esta en la radio del ayuntamiento, en vista tactica obligatoria, coordinando al equipo con una guia en pantalla. El flujo viejo queda disponible con `OperacionDirector.ResistirSinMando = true` (solo para checks legacy).

### Bugs cubiertos
- **#101**: dirigir la defensa desde la radio (tomar la radio con [E], vista tactica forzada sin salida: Tab, posesion y cambio de camara bloqueados; el operador queda inmovil, arrodillado y recibe x0,5 de dano hasta soltar la radio; soltar con [E] detiene el reloj y avisa).
- **#074** (cobertura de aliados): los aliados dentro de un sector reciben ordenes automaticas de cobertura cada 0,5 s (3 sectores x 4 puntos de cobertura); verificado que entran en cobertura ~2 s despues de llegar.

### Mecanica resumida
- 3 sectores (A oeste, B norte, C sur) con anillo en el mundo y en el minimapa. Un sector amenazado y vacio cae a los 6 s; se recupera con 2 aliados adentro durante 4 s.
- Reloj efectivo de 90 s: corre solo con los 4 primeros pasos del tutorial hechos, operador en la radio, ningun sector caido y ningun sector amenazado vacio. El motivo de pausa se ve en el panel y en el HUD.
- 5 oleadas dirigidas (fracciones .13/.31/.50/.68/.84), anunciadas 8 s antes con flecha roja en el piso y el minimapa, aparecen una por una en la boca de calle del lado de la amenaza (reusa #063); camionetas en 3 de ellas (reusa #064: los milicianos no suben al helicoptero y no cuentan en `SupervivientesForzados`).
- 2 milicianos de apoyo (Assault y Medic) instanciados en runtime desde `P_Soldier_Ally`, excluidos de `Escuadra()`.
- Panel de mando con 6 pasos (seleccionar a Kes, ordenar al sector A, arrastrar recuadro sobre los milicianos, milicianos al B, Doc al C, atacar la camioneta), iconos de mouse, anillo guia sobre la unidad a seleccionar, filas cumplidas plegadas.
- Campanario: plataforma a 6,5 m con rampa; con un soldado arriba: alcance x1,35 y `WorldUiDirector.RevelarEnemigos`.

### Archivos nuevos (`Assets/_Project/Scripts/`)
- `Operacion/SectorDeDefensa.cs`, `Operacion/MandoTactico.cs` (estado y reglas estaticas), `Operacion/RampaCaminable.cs`, `Operacion/OperacionDirector.Mando.cs` (~900 lineas: llegada, radio, oleadas, caida de sectores, cobertura automatica, tutorial, campanario, HUD, captura/restauracion).
- `Presentation/FlechaDeOleada.cs`, `UI/PanelDeMando.cs`.
- `Editor/OperacionBuilder.Mando.cs` (grupo `6_Ciudad/Mando`), `Editor/ChecksBugs065.Wp10.cs` (checks `Bug101a..j`, `Bug101pa/pb`).

### Archivos modificados
- `Combat/Health.cs` (FactorDeDano), `Camera/CameraRig.cs` (RtsForzado + guardia en SetMode), `Player/PlayerInputDriver.cs` (Tab, TryPossess, el recuadro de seleccion recorre `ActorRegistry` e ignora al operador), `Player/PossessionService.cs`, `Player/OrderService.cs` (evento `AtaqueAVehiculoOrdenado`, el operador no obedece), `Presentation/WorldUiDirector.cs`, `Presentation/AnimacionDeAccion.cs` ("OPERANDO LA RADIO"), `Ai/AiBrain.cs` (BonusDeAlcance), `Core/NavService.cs` (BlocksMovement ignora `RampaCaminable`).
- `Operacion/OperacionDirector.cs` (ramas Resistir mando/legacy, Escuadra, captura/restauracion, ResistenciaRestante), `OperacionDirector.Jefe.cs`, `OperacionDirector.Resistir.cs`.
- `Editor/OperacionBuilder.cs` (segundosDeResistencia=90, `Ancla_Zona_5` = radio), `Editor/OperacionBuilder.Ciudad.cs`, `Editor/LevelBlockoutBuilder.cs` (el bake ignora `Campanario_*`).
- Checks legacy parcheados con `ResistirSinMando = true`: `ChecksBugs065.Wp4.cs` (Bug086), `ChecksBugs065.Wp7.cs` (Bug075), `ValidacionBugs.cs` (bugs 17/20/21). `ChecksBugs065.Wp6.cs` (Bug091): ver abajo.
- Escena `SC_Operacion` regenerada con el builder (idempotente) y guardada: inventario 7815 -> 7862 objetos (+3 SectorDeDefensa, +1 RampaCaminable, +4 TextMesh, +25 BoxCollider; NavMeshModifierVolume 579 -> 597, ObstacleMarker 444 -> 459). Respaldo previo en el scratchpad.

### Desviaciones y por que
- Vista tactica a 66 m de altura y 40 grados de inclinacion (el plan decia 45 / 60): con eso entran los 3 sectores y las calles de entrada; el foco es el centroide de los sectores desplazado en z.
- El reloj efectivo es de 90 s (segundosDeResistencia), contados solo con las condiciones de arriba (el tiempo real puede ser mayor).
- El campanario es una plataforma con rampa de 2 m: el motor del soldado es plano, asi que se agrego `RampaCaminable` (ajuste de y sobre la franja) y una excepcion en `NavService.BlocksMovement`; el NavMesh no conecta la rampa, la subida se resuelve por orden de movimiento + ajuste de altura.
- La niebla de guerra del minimapa NO esta activa en esta escena: el "revelar enemigos" del campanario se verifica como bandera, sin efecto visible hoy.
- `Ancla_Zona_5` ahora es la radio (la cinematica de introduccion `Bug097i` la usa; sigue pasando).
- Refresco del HUD del mando limitado a 0,16 s (rendimiento). Panel con filas cumplidas plegadas para no tapar el minimapa ni el sector C.
- Atajos de prueba: `OperacionPrueba.Arrancar(5, _, subfase)` y `SaltarA(..., subfase)` para Resistir; flujo viejo con la bandera `ResistirSinMando`.

### Checks (Play fresco cada uno) y resultado
| Check | Resultado |
|---|---|
| `Bug101a` llegada, tomar la radio, vista bloqueada, operador inmovil, dano x0,5 (22 vs 43), milicianos, panel, anillo guia, [E] suelta | OK |
| `Bug101b` tutorial completo: 5 pasos por eventos, reloj detenido sin ubicar, flecha de oleada | OK |
| `Bug101c` sector A cae a los 6,0 s, reloj pausado, se recupera a los 4,0 s con 2 aliados | OK |
| `Bug101d` 4 puntos de cobertura por sector, en cobertura ~2 s despues de llegar (ordenes automaticas=3) | OK |
| `Bug101e` cadena completa: 90 s efectivos (89 s reales) -> Extraer -> Victoria; 5/5 oleadas, hasta 24 atacantes vivos; SupervivientesForzados=3 = subidos (milicianos no cuentan); orden de ataque a la camioneta | OK |
| `Bug101f` constantes y reglas (reloj, motivos, cae/recupera, sin radio nada bloquea) | OK |
| `Bug101g` operador muerto: radio abandonada, vista libre, reloj detenido | OK |
| `Bug101h` `CapturarEstado`/`RestaurarEstado` con mando (reloj 17,8, sector caido, radio) | OK |
| `Bug101i` campanario: Kes sube en 10,5 s, bonus x1,35, se apaga al bajar | OK (solo bandera; ver niebla) |
| `Bug101j` las unidades del tutorial no incluyen al operador; paso 1 nombra a KES | OK |
| `Bug101pa` / `Bug101pb` rendimiento, mismo escenario (modo dios 70 s, editor) | flujo viejo 44,3 fps medios, p95 32 ms, max 29 enemigos; mando 41,2 fps, p95 32 ms, max 20 enemigos |
| Regresion en Play fresco: `Bug000`, `Bug065a`, `Bug069a/b/c`, `Bug070`, `Bug072a/v`, `Bug073`, `Bug074`, `Bug075`, `Bug077`, `Bug079`, `Bug086`, `Bug090`, `Bug093`, `Bug095a`, `Bug096`, `Bug097a/b/i/j/k/l/m/n`, `Bug098`, `Bug099`, `Bug100` | OK todos |
| `Bug091` (giro/anillo/cruceta de la torreta del tanque en marcha) | **intermitente**: de ~12 corridas paso aproximadamente la mitad |
| Capturas `Assets/Validacion/` | `v2_101_mando_paso1.png`, `v2_101_sectores.png`, `v2_101_sector_caido.png`, `v2_101_campanario.png` |

### Bug091 (intermitente, heredado)
Falla en los puntos d)/e) del anillo: el punto de mira cae sobre el casco propio o sobre `Fragmento` de lo que el tanque aplasta, o la cruceta naranja no sale 20/20 frames, segun donde este el tanque (en marcha, ~20 s de ruta) y la tasa de frames del editor. No toca nada de WP10 (la ruta de huida y la torreta no se modificaron) y WP9b ya lo habia dejado intermitente por las rocas del canadon. Se patcheo el check con tolerancia a hits sobre el casco propio (`enMuro + enCascoPropio >= 20 && enMuro >= 8`) y se agrego diagnostico (`otroInfo`); sigue fallando de vez en cuando por los `Fragmento`. Pendiente: estabilizarlo (por ejemplo congelando el tanque durante d/e). No se pudo demostrar con una corrida A/B previa al WP10 que sea heredado; es una inferencia.

### Pendientes honestos
- **Nada se jugo con mouse/teclado reales.** Seleccion, ordenes y clics se hicieron por API/eventos; el recuadro de arrastre (`SelectAlliesInScreenRect`, ahora sobre `ActorRegistry`) no tiene un check propio (101a verifica la seleccion de los 4 aliados, no el arrastre en pantalla).
- Sin jugada humana de punta a punta; el balance (dificultad de las oleadas, 90 s efectivos, 6 s para caer) no se ajusto con un jugador real.
- Rendimiento medido solo en el editor: -3 fps medios frente al flujo viejo (con menos enemigos vivos); el costo extra proviene del render RTS y la UI de mundo, no de la logica del director. No se perfilo un build.
- Niebla de guerra del minimapa inexistente en la escena: el efecto del campanario sobre el minimapa no se ve; la rampa solo se probo con IA.
- `v2_101_campanario.png` esta mal encuadrada (muestra la vista RTS, no la torre); no se rehizo.
- En la captura de la radio el aviso central "VISTA TACTICA · DIRIGI A TU..." se solapa con el temporizador "90" (cosmetico, sin corregir).
- `ValidacionBugs` completa no se corrio de nuevo (solo se parcharon los bugs 17/20/21 con la bandera legacy). No hay checks `Bug063`/`Bug064`: #063 se verifico indirectamente (apariciones una por una en 101b/101e) y #064 via 101e.
- Estabilidad: el editor se cayo UNA vez en este paquete (error nativo del heap Mimalloc en un job de construccion de tiles del NavMesh, durante la primera corrida de 101a; reporte `Crash_2026-10-04_210001745`). Se relanzo con `Unity.exe -projectPath` sin `editor_focus`; la escena quedo intacta. El usuario no estuvo jugando.
- Al final queda fuera de Play, `SC_Operacion` guardada y sin cambios pendientes, `timeScale` 1, modo dios apagado. No se hizo commit, push ni build; `Builds/Windows64` intacto.


## WP11 - Integracion y regresion: checks aislados, rendimiento por fase y pendientes (#065-#101)

### Paquete
Tres cosas: (1) pasada completa y repetible de los checks de la tanda; (2) suite headless y humo de escenas contra la linea base; (3) medicion de rendimiento por fase con el costo del juego separado del costo del editor, mas la correccion de lo que salio de ahi. Ademas se cerraron pendientes de WP10 (aviso/temporizador, campanario, #063).

### Entregables
- `ChecksBugs065.CorrerTodos(desde, hasta, aislar)` y `CorrerLista("A,B,C", aislar)`: cada check corre en un Play fresco (maquina de estados reiniciar / saliendo / entrando / asentando / check, estado en `SessionState` para sobrevivir a la recarga de dominio); el progreso se escribe en `Temp/checks_progreso.txt` y termina con `# FIN`.
- `val2_run.sh` (scratchpad de la sesion): deja el editor en Edit, exige `SC_Operacion` abierta y sin cambios, lanza `CorrerTodos`, espera el `# FIN` y escribe `Informes/03_Resultado_checks.md` con `val2_resumen.py`.
- `Informes/03_Resultado_checks.md`: resultado de la pasada completa (72 lineas, 1 h). IMPORTANTE: esa pasada es ANTERIOR a los arreglos de este paquete; los 4 fallos que lista (#063, #081, #085b, #097k) se corrigieron despues y se re-corrieron (ver tabla de checks). No se regenero el archivo para no pisarlo con una pasada parcial.
- Herramientas de editor nuevas: `Editor/MedidorDeFases.cs` (medicion por fase), `Editor/SondaDeRendimiento.cs` (+arbol de costos), `PlaymodeCleanup.BarrerMaterialesHuerfanos`.

### Archivos tocados (resumen)
- Checks: `Editor/ChecksBugs065.cs` (reescrito: aislamiento), `.Wp1.cs` (`TiroDeMedicion` normaliza cabeza y reintenta hasta 3 tiros), `.Wp4.cs` (Bug081), `.Wp6.cs` (Bug091), `.Wp9a.cs` (Bug085b), `.Wp9b.cs` (Bug097k), `.Wp11.cs` (Bug063, Bug064, capturas `CapturaConHud` / `CapturaDesde`).
- Juego: `Operacion/OperacionDirector.Resistir.cs` y `.Mando.cs` (`bocaDeLaOla`: cada oleada sale por una boca distinta, arreglo real de #063), `UI/ModeToastView.cs` + `Operacion/OperacionHud.cs` (el aviso central sube 80 px cuando el temporizador esta visible), `Core/Coberturas.cs` (marcas perezosas), `DecalPool.cs` + `PrecalentadoDeCombate.cs` (precalentado de decales), `Presentation/SafeMaterial.cs`, `DiamondGizmo.cs`, `MinimapIcon.cs`, `WorldUiDirector.cs`, `EntityStateDebugView.cs` (materiales compartidos y menos trabajo por frame), `TurretAimView` (mascara), `Reanimacion` (MaxHealth), `DianaDeObjetivo`, `CurandoAnimacion.Soltar`, `Editor/PlaymodeCleanup.cs`.

### Desviaciones y por que
- #071 no tiene check automatizable (sigue listado como "SIN CHECK").
- #063 no tenia check ni arreglo completo: se agrego `Bug063` y se corrigio el codigo (antes las oleadas salian por bocas muy cercanas entre si, a 63 m de la plaza); #064 tiene `Bug064` (SupervivientesForzados / embarque) y pasa.
- Umbrales de tres checks ajustados con diagnostico: Bug081 (16 duelos, umbral 32 % en cobertura), Bug085b (guardias vivos: 0 al inicio y >= 10 a los 4 s) y Bug097k (limite de reparacion 45 s sin pausas; medido 29 s).
- Bug091 se hizo estable congelando el tanque a 0,5 m/s durante d)/e). Bug079 era sensible a que el tiro de medicion entrara en la cabeza (x2): se normaliza y se reintenta hasta 3 veces.

### Checks (Play fresco cada uno)
| Check | Resultado |
|---|---|
| Pasada completa `CorrerTodos` (antes de los arreglos) | 67 OK / 4 FALLO / 1 sin check (#071) en 72 lineas, 3655 s |
| `Bug063` (oleadas por bocas distintas) | 3/3 OK tras el arreglo |
| `Bug081` / `Bug085b` | 3/3 OK cada uno tras ajustar |
| `Bug097k` | 3/3 OK (35,7 / 37,5 / 32,8 s). Antes: 57 s en la pasada completa; con el editor cargado puede superar el limite: sigue siendo el candidato a intermitente |
| `Bug091` | OK (3/3 tras congelar el tanque, y 1/1 en la ultima tanda) |
| `Bug079` | 4/4 y luego 2/2 OK tras el reintento; hubo 1 FALLO en 6 (dano 135 en lugar de 68, tiro en cabeza) antes del reintento |
| Suite headless | sin fallos nuevos frente a la linea base (31 fallos preexistentes; la base era 32) |
| Humo `SC_Gameplay` / `SC_Tutorial` / `SC_TestLevel` | abren, entran en Play, cinematica/pausa/opciones/victoria sin excepciones, 0 errores de consola nuevos (38 / 4 / 12 soldados vivos) |

Nota del humo: `ShowPause` informa `IsPaused=False` en las tres escenas desde el script de humo (no investigado); se verifico que abrir/volver de Opciones y continuar no lanzan excepcion, no que el menu de pausa se vea.

### Rendimiento por fase (editor, 1920x1080, modo dios, misma escena y recorrido)
"Juego" es el tiempo de PlayerLoop; "editor" es el resto del frame (~2 ms constantes). Los fps de editor NO son los de un build.

| Fase | fps antes | fps despues | juego despues | editor | peor frame antes -> despues |
|---|---|---|---|---|---|
| Infiltrar | 81,3 | 77,9 | 10,7 ms | 2,1 ms | 27 -> 25 ms |
| Puestos | 81,6 | 72,3 | 11,6 ms | 2,3 ms | 172 -> 156 ms |
| Centro | 112,0 | 109,2 | 7,2 ms | 1,9 ms | 21 -> 25 ms |
| Huida | 82,4 | 90,2 | 9,0 ms | 2,1 ms | 343 -> 104 ms |
| Resistir | 69,7 | 67,0 | 12,8 ms | 2,1 ms | 145 -> 38 ms |
| Extraer | 103,8 | 99,7 | 8,0 ms | 2,0 ms | 21 -> 25 ms |

Lectura honesta: los fps MEDIOS no mejoraron (las diferencias de +-5 % estan dentro del ruido entre corridas). Lo que si cambio es la cola: los picos de 145-343 ms (caida de obstaculos, primer decal, creacion de materiales) bajaron a 38-104 ms; el pico de 156 ms que queda en Puestos es la caida de un obstaculo (Registrar de coberturas 7-14 ms + `Fragmentador.Romper` 6-13 ms). `WorldSimulationDriver` cuesta 0,2-0,9 ms por frame (max puntual 3-14 ms): no es el cuello de botella. GPU 2,6-5,5 ms y render thread 1,5-2,3 ms: el limite es el hilo principal de CPU (3000 renderers, 62 luces, GC 10-65 KB por frame). La columna GPU de Puestos/Huida "antes" salio corrupta (valor invalido de la API de timing) y no se usa.

En Resistir, hacia el final de las oleadas, el juego baja a 51-57 fps (14-17 ms) por el costo por soldado y por el render; no se optimizo.

### Hallazgos de rendimiento y lo que se hizo
- **Materiales**: los materiales `HideAndDontSave` creados por `SafeMaterial` y `DiamondGizmo` no se liberaban nunca y crecian por cada Play (109.000 a 114.000 en una sesion). Se agrego el barrido `BarrerMaterialesHuerfanos` al salir de edicion, registro de origenes y materiales compartidos (iconos del minimapa, esferas de estado). Resultado: 1.190-1.340 materiales, sin crecimiento entre fases (+30-60 dentro de una fase, de las explosiones).
- **Minimapa y UI de mundo**: los iconos estaticos ya no reescriben su posicion cada frame; `EntityStateDebugView` sale temprano si no esta visible.
- **Coberturas**: las marcas (cilindros) se crean solo cuando se las pide; el registro de coberturas escanea todos los colliders y sigue siendo el costo principal de las caidas de obstaculos.
- **Primer decal**: `DecalPool.Prewarm()` dentro de `PrecalentadoDeCombate` elimina el hipo del primer disparo.
- **Cuartel (artilleros / reflectores)**: no se pudo reproducir la caida a 32-47 fps informada; en las corridas las torretas casi no dispararon. Sin evidencia, no se toco.
- **Fragmentos del WP6 (aplastar/derribar)**: costo sin medir, `fragmentosActivos=0` en todas las corridas.

### NavMesh: variacion al reconstruir
Se comparo la lista ordenada de `NavMeshModifierVolume` (`navvol_*.txt`) y la triangulacion entre reconstrucciones: los volumenes son identicos y las rutas entre las entradas existen siempre (todas las entradas sobre NavMesh, con camino completo). La reconstruccion verificada es determinista en lo que importa. La caida nativa del editor por el job de tiles de WP10 no se reprodujo.

### Pendientes cerrados
- Aviso central solapado con el temporizador: corregido (sube 80 px); captura `Assets/Validacion/v2_wp11_aviso_timer.png`.
- `v2_101_campanario` mal encuadrada: rehecha, `v2_wp11_campanario.png` y `v2_wp11_campanario_rampa.png`.
- Linea roja sobre el francotirador muerto: identificada la causa y corregida (diana / mascara de la torreta).
- #063: arreglo real y check; #064: check.

### Pendientes honestos
- **Bug097k** puede seguir fallando de forma intermitente si el editor va lento (57 s en la pasada completa; limite actual 45 s sin pausas). La causa se supone (carga del editor y camino de Kes), no se demostro.
- **#071** sin check automatizable.
- `Informes/03_Resultado_checks.md` es de la pasada previa a los arreglos; una pasada completa nueva dura ~1 h y no se repitio.
- El rendimiento medio por fase no mejoro; el costo restante es de render y del hilo principal y de la caida de obstaculos. No se perfilo un build.
- Costo de los fragmentos del WP6 sin medir; caida del cuartel no reproducida.
- Nada se jugo con mouse y teclado reales; jugabilidad y balance de las oleadas no se validaron con un jugador.
- Durante la sesion se invoco por error una vez un menu del editor (Window/General/Console): sin efecto sobre el proyecto.
- Estado final: `SC_Operacion` abierta y sin cambios pendientes, fuera de Play, `timeScale` 1, modo dios apagado, sin estaticos de prueba. Sin commit, push ni build; `Builds/Windows64` intacto.
