# Plan: bugs #396 a #414 (revisión Opus, 2026-11-08). Reemplaza a `PLAN_bugs_396_414.md`

Fuente: `Registros/bugs.txt` (líneas 711–729, sesión `sesion_SC_Operacion_20261108_202752.txt`), `Registros/bugs_columnas.csv`
(columna `Objeto_mirado`, que es el raycast real desde la cámara al reportar), `Registros/bug_NNN_*.json` y `.png`.
Código: commit `cdbe52fd`. #396–#400 son de la fase **Huir** (en el tanque, asiento Gunner); #401–#414 de **Resistir** (mansión).
Todos los reportes tienen `ts=0.00` (el reporte pausa el juego): lo que se ve "quieto" en una captura no prueba que esté trabado.

Convenciones de este plan:
- **[VETABLE]** = interpretación mía de un pedido ambiguo; el dueño puede pedir la otra.
- **[VERIFICAR EN UNITY]** = no se puede confirmar leyendo código (escenas binarias); va el procedimiento exacto.
- Rutas relativas a `Assets/_Project/Scripts/`.

---

## 0. Reglas que el plan original no tenía en cuenta (leer antes de escribir un check)

1. **Nombres de check ya usados (no reutilizar):** `Bug396a`, `Bug397a`–`e`, `Bug398a`–`b` (Nov08.cs, son de los bugs #369–#395),
   `Bug401u`–`Bug405u` (Revision.cs), `Bug406u`–`Bug410u` (Azotea.cs). Los checks nuevos se llaman:
   `Bug396b`, `Bug397f`/`g`, `Bug398c`/`d`, y `Bug399a`, `Bug400a`, `Bug401a` … `Bug414a` (en 399–414 no hay ninguna letra `a`).
2. **La mansión está apagada en los checks salvo que el nombre matchee `reMansion`** (`Editor/ChecksBugs065.cs:128`,
   `^Bug((406|407|408|409|410)u|601e|601g|602[a-z])$`; `PrepararModoDelMando` pone `OperacionDirector.ApagarMansion = !match`).
   Primer cambio de todo el trabajo: ampliar a
   `^Bug((406|407|408|409|410)u|(40[3-9]|41[0-4])[a-h]|601e|601g|602[a-z])$`.
   Sin eso, `Bug406a`, `Bug409a`, `Bug411a`, `Bug413a` y los demás corren sin mansión y fallan o dan falso OK.
   Los checks de mansión siguen el modelo de `Bug409u` (Azotea.cs:219): `OperacionPrueba.Arrancar(5)`, luego reflexión sobre
   `AbrirElPorton`, `EntrarPrimerPiso`, `EntrarSegundoPiso` y `EntrarAzotea`.
3. **Nombre inexistente en la lista = "SIN CHECK" y la lista se corta** (`ChecksBugs065.cs:204`). Registrar el método antes de
   agregarlo a cualquier lista. No editar C# mientras corre una lista.
4. **El editor va a unos 17 FPS.** Nada de "esperar 2 s y medir": usar bucles
   `while (cond == false && Time.realtimeSinceStartup - t0 < tope) yield return null;`, con tope generoso (≥ 20 s reales) y midiendo
   **contadores** (por ejemplo `AliadosReubicadosDePiso`), no tiempos. Ojo: con `ModoDios.Poner(true)` la escala de tiempo puede ser
   2 (`ModoDios.EscalaDeTiempo`), así que `Time.time` avanza el doble que el reloj real.
5. Plantilla: `W8Ok(ref ok, cond, sb, "qué mide")` y `Fin((ok ? "OK " : "FALLO ") + sb)`. Fotos con
   `FotoCamara(archivo, pos, mira, fov)` (Hoy.cs:18), a `Assets/Validacion~/v9_NNN_*.jpg`.

---

## 1. Ficha por bug

### #396: HUD de tanque duplicado; pide uno solo y más largo
- **Causa (verificada en código).** En el tanque conviven tres cosas:
  - `UI/HudDeTanqueSimple.cs`: barras abajo al centro. Raíz en (0, 34), 520×40. `Vida` 520×22 en y = 14. `Recarga` 520×6 en y = 4.
  - `UI/VehicleStatusView.cs`: GameObject `VehicleStatus`, armado en `Editor/HeadlessTestRunner.cs:3194`. Es un panel de 220×88 abajo a
    la derecha con "Cañón / MUNICIÓN EXPLOSIVA · LISTA / TANQUE" y su propia `HealthBarFill`. Es el HUD duplicado.
  - `UI/ModoDiosView.cs`: un cartel de 190×22 en y = 8, abajo al centro. Se pisa con las barras.

  `UI/CapasDeHud.ModoTanque(true)` oculta hijos del Canvas raíz, pero `VehicleStatus` no figura en `HijosDeHudFpsOcultos` ni en
  `PermitidasEnTanque`. Lo actualiza `PlayerInputDriver.Vehiculo.cs` en `UpdateInVehicle`, alrededor de la línea 215
  (`VehicleStatus.UpdateFrom`).
- **[VERIFICAR EN UNITY].** Quién es el padre de `VehicleStatus` en `SC_Operacion`. Hacerlo con `unity command eval`:
  `GameObject.Find("VehicleStatus").transform.parent.name` y `CapasDeHud`.
- **Cambio.**
  1. Agregar `CapasDeHud.ModoTanque`: ocultar por tipo (`VehicleStatusView` y `ModoDiosView`), no por nombre. Así no depende de la jerarquía.
  2. `HudDeTanqueSimple`:
     - raíz de 760×64;
     - barra de vida de 760×22 con texto "TANQUE 1447/1600";
     - barra de recarga de 760×10 con rótulo "CAÑÓN · LISTO / RECARGANDO";
     - insignia propia de modo dios a la izquierda de la vida (la "variante": escudo + "DIOS · F4"), visible si `ModoDios.Activo`.
  3. `ModoDiosView` sigue igual a pie.
- **Riesgo.** Bajo. `Bug374a` (HUD del tanque) mide nombres o posiciones: revisarlo y actualizarlo si pide 520 px.
- **Check `Bug396b`** (en el tanque, `OperacionPrueba.Arrancar(4,1,3)` + `d.Embarcar()`, esperar a `PlayerInputDriver.Activo.Vehicle != null`):
  - `VehicleStatusView` con alpha 0 o inactivo;
  - `ModoDiosView` oculto;
  - ancho de la barra de vida ≥ 700;
  - los rectángulos en pantalla de las barras y de la insignia de dios no se intersecan (`RectTransformUtility` a coordenadas de pantalla);
  - con dios activo, la insignia está visible.
- **Foto:** `v9_396_hud_tanque.jpg`, captura de pantalla completa (no `FotoCamara`, que no dibuja el Canvas overlay).

### #399: "la barra de vida de abajo no se actualiza"
- **Corrección al original.** No es una segunda barra de vida: es la **barra de recarga** fina de `HudDeTanqueSimple` (verde y llena
  en las capturas).
- **Causa probable (verificada parcialmente).** `HudDeTanqueSimple.Mostrar(Vehicle v)` toma
  `torreta = GetComponentInChildren<TurretWeapon>(true)`, que puede ser la **ametralladora** y no el cañón. Si es la ametralladora,
  `CooldownFraction01` nunca baja cuando dispara el cañón.
- **Cambio.** `torreta = v.TorretaCanon ?? GetComponentInChildren<TurretWeapon>(true)`. La barra se rehace en #396: más gruesa,
  con rótulo y con color (gris cargando, verde listo).
- **Check `Bug399a`.** Montado de artillero:
  - `HudDeTanqueSimple` apunta a `v.TorretaCanon`;
  - disparar con `TryFire` del cañón;
  - esperar (con bucle) a que la fracción de recarga mostrada sea < 0.5 y que después vuelva a 1;
  - medir el `fillAmount` o el ancho de la barra, no el campo.

### #397: el apuntado "snapea" contra obstáculos
- **Causa (verificada).** `PlayerInputDriver.Vehiculo.cs`:
  - `PuntoBajoLaMira(out punto)` (línea 632) tira un `RaycastNonAlloc` desde `Rig.GetForwardRay()` hasta 350 m e ignora sólo el
    vehículo y el soldado, así que se queda con el **primer** impacto;
  - la cámara artillera está unos 8 m atrás y 3.5 m arriba del tanque;
  - una barrera Jersey cercana (ver el anillo naranja de `MiraDeTorreta` sobre la barrera en la captura) se convierte en el blanco, y
    `ApuntarConLaCamara` (línea 616) → `TurretWeapon.ElevarHacia` baja el cañón de golpe.
- **Cambio** en `PuntoBajoLaMira`:
  - descartar los impactos con distancia a lo largo del rayo menor que (distancia cámara→boca del cañón + 2 m);
  - descartar los impactos con colliders de cobertura baja (`ObstacleMarker` o altura de bounds < 1.6 m) a menos de 15 m del tanque;
  - si no queda impacto, usar el punto del rayo a 120 m.

  Resultado: la mira "sigue de largo" por encima o detrás de los obstáculos. `MiraDeTorreta` tiene que usar el mismo punto (hoy hay
  que revisar si hace su propio rayo).
- **Riesgo.** Medio:
  - apuntar a un enemigo pegado a una barrera ahora pasa de largo;
  - mitigación: no descartar si el collider es un `Soldier` o un `Vehicle` enemigo.
- **Check `Bug397f`:**
  - poner el tanque frente a una barrera a 6 m;
  - barrer el pitch de la cámara de −10° a +10° en 21 pasos (fijando el rig, sin input);
  - en cada paso, esperar a que la elevación converja (|Δ| < 0.1° entre frames, tope de 3 s reales);
  - se mide que la elevación del cañón crezca de forma monótona y sin saltos > 3° entre pasos contiguos.
- **Atlas pedido `Bug397g`:**
  - 6 cuadros `FotoCamara` laterales del tanque, a pitch −10, −5, 0, +3, +6 y +10, con la barrera en cuadro;
  - se compone en `v9_397_atlas.jpg` (2×3, `Texture2D.SetPixels` + `EncodeToJPG`);
  - cada cuadro lleva el ángulo de elevación escrito en el nombre del archivo individual.

### #400: proyectil al triple
- **Decisión [VETABLE]:** sólo el **cañón del tanque del jugador**, de 130 a 390 m/s.
  - Hoy, `TurretWeapon.SpeedMultiplier = 0.5f` es un `const` compartido por todo `TurretWeapon` (cañón y ametralladora, y también
    las torretas de la IA).
  - "Porque es más rápido" se refiere al tanque.
  - La alternativa es "todas las balas del jugador".
- **Cambio** en `Vehicles/TurretWeapon.cs`:
  - nueva propiedad de instancia `MultiplicadorDeVelocidad => (v.Bando == Player && v.TorretaCanon == this) ? SpeedMultiplier * 3f : SpeedMultiplier`,
    con el mismo patrón que `FactorDeCadencia`;
  - reemplazar `SpeedMultiplier` por esa propiedad en `TryFire` (línea 415, `pool.Spawn(..., speedMultiplier:)`), `ElevarHacia`
    (línea 200) y `AimAt` (línea 302);
  - además en `UI/TurretAimView.cs:260` y `UI/MiraDeTorreta.cs` (balística de la mira);
  - `Ai/TurretAI.cs:253` queda con el const (la IA no cambia).
  - `Projectile.Tick` ya barre el tramo (`BuscarBlancoEnElTramo` + `ChocoContraElMundo` con RaycastNonAlloc): a 390 m/s no hay túnel.
- **Riesgo.** `Editor/HeadlessTestRunner.cs:1819–1825` afirma `velocidad == base × SpeedMultiplier`. Hay que actualizarlo para el
  cañón del jugador, o el headless queda rojo.
- **Check `Bug400a`:**
  - disparar el cañón del tanque del jugador y leer `effectiveSpeed` del `Projectile` creado: debe ser ≈ 390;
  - disparar la ametralladora: velocidad sin cambios;
  - la mira predice el punto de impacto a 100 m con error < 1 m (disparo a un plano).

### #398: feedback de impacto, tanques enemigos visibles (3 a 6), daño enemigo ×2
- **Causas (verificadas):**
  - **No hay feedback en el tanque.** `UI/DamageVignetteView.cs`, `DamageDirectionView.cs` y `MarcaDeImpacto.cs` escuchan
    `DamageTakenEvent` del soldado poseído. El daño al tanque viaja como `VehicleDamagedEvent(Vehicle, Amount, RemainingHealth, MaxHealth)`
    (`Core/Events.cs:149`, se publica en `Vehicle.TakeDamage` línea 102). Nadie lo muestra.
  - **Casi no hay tanques.** `OperacionDirector.Huida.cs` `CrearAuto()` (línea 504) hace
    `if (AutosCreados >= 3 && Random.value < ProporcionDeTanquesEnemigos /*0.25*/) a.ConvertirEnTanque();`, así que sale 0 o 1 tanque
    por fuga.
- **Cambio:**
  1. **Feedback.** Un manejador de `VehicleDamagedEvent` en `DamageVignetteView` (o un componente nuevo `ImpactoEnVehiculoView`)
     cuando `e.Vehicle == PlayerInputDriver.Activo.Vehicle` dispara:
     - viñeta roja proporcional a `Amount/MaxHealth`;
     - sonido 2D de impacto metálico (con un enfriamiento de 0.12 s);
     - `CameraRig.KickDirectional`;
     - destello de la barra de vida de `HudDeTanqueSimple`;
     - flecha de dirección si el evento trae origen (si no lo trae, sin flecha).
  2. **Tanques garantizados [VETABLE].** Determinístico:
     - desde el auto #2, uno de cada dos es tanque;
     - mínimo 3 y máximo 6 por fuga (contador `TanquesEnemigosCreados`);
     - reemplaza a `ProporcionDeTanquesEnemigos`.
  3. **Daño ×2 [VETABLE]**, interpretado como "el daño que recibe el tanque del jugador": `MultiplicadorDeDanoDelTanque` pasa de 4 a 8
     (`Huida.cs:376`). Escala el daño crudo y los topes por golpe y por segundo, que son los que mandan.

     Duplicar `danoPorBala` no alcanza: el tope por segundo lo recorta.
- **Riesgos (altos):**
  - el tanque tiene `PisoDeVida01 = 0` en la Huida (línea 85). Con ×8 y 3 a 6 tanques, la fuga puede volverse imposible sin modo dios;
  - `Bug409u` (Azotea.cs:239) exige `MultiplicadorDeDanoDelTanque == 4f`: hay que cambiarlo a 8;
  - `Bug403u` (Revision.cs:103–114) escala el rango con el multiplicador, pero afirma que "el tanque sano aguanta 10 s de fuego
    continuo". Con ×8 son unos 12.8 %/s, así que muere. Bajar esa parte a 5 s, o bajar los topes;
  - **decidir antes con el dueño.**
- **Checks:**
  - `Bug398c`:
    - simular la fuga con `CrearAuto` por reflexión (como `Bug378a`) hasta 12 autos y contar `TanquesEnemigosCreados` en [3, 6];
    - `TakeDamage(70)` al tanque del jugador sube el contador del feedback (por ejemplo `ImpactoEnVehiculoView.Mostrados`) y la
      viñeta queda con alpha > 0;
    - el daño por segundo con fuego continuo es el doble que con ×4 (medir 3 s de `TakeDamage(7)` cada 0.1 s con ambos valores, o
      contra la constante).
  - `Bug398d`, foto pedida `v9_398_tanques.jpg`:
    - `FotoCamara` alta detrás del tanque del jugador, con al menos 3 tanques enemigos en cuadro (se colocan en la ruta a 30–60 m)
      y la viñeta activa;
    - el check mide los tanques dentro del frustum (`GeometryUtility.TestPlanesAABB`) y que sean ≥ 3.

### #401: rombos de enemigos detrás de obstáculos
- **Causa (verificada).** `Presentation/UnitLocatorCylinder.cs:146` `SiempreEncima => equipoPintado != TeamId.Enemy`. Se decidió en
  el #174 que los enemigos respetaran la profundidad. Sólo se ve si `InteligenciaDeEnemigos.EstaRevelado`.
- **Cambio.** `SiempreEncima => true`. Se mantiene la compuerta de revelado, así que lo no revelado sigue oculto y no hay wallhack gratis.
- **[VETABLE].** Contradice el #174. Si el dueño quiere wallhack sólo dentro de N m, agregar una distancia.
- **Riesgo.** Bajo. Revisar algún check del #174 que afirme lo contrario (buscar `SiempreEncima` en `Editor/`).
- **Check `Bug401a`:**
  - revelar un enemigo detrás de un muro;
  - el material del rombo usa `ZTest Always`: comparar `sharedMaterial.GetInt("_ZTest") == (int)CompareFunction.Always`, o que sea la
    instancia de `DiamondGizmo.NuevoMaterialSiempreVisible`;
  - un enemigo no revelado no tiene el rombo activo.

### #406 y #413: aliados trabados entre pisos y en la azotea
- **Causa de #413 (verificada con el JSON y el código).**
  - `OperacionDirector.Compuertas.cs` `TodosLosAliadosYaSubieron(nivelDestino)` (línea 45) sólo mira `y >= Nivel(destino) − 0.8`.
  - Doc, Kes y Prisionero_1 estaban en (338.4–340.8, **20.2–20.3**, 273), arriba del tramo B **dentro de la torre C**, del lado de la
    torre respecto de la reja `Mansion_Compuerta_C_bajada`.
  - Por altura cuentan como "subidos", así que la reja se traba (línea 119, `c.trabada = true`) y los deja encerrados.
  - `RescatarAliadosDeOtroPiso` (línea 144) no los rescata: la diferencia de altura es 0 y están a menos de 40 m. **No ve la reja.**
  - Además, la caja `Azotea_Equipo` en (338, 278) cuelga sobre el hueco de la torre C y les pega en la cabeza en el tramo B (ver #411).
- **#406 (verificado en parte).**
  - Al reportar, Doc estaba en planta baja (y 1.5) y Kes dentro de la torre A (y 6.0). El jugador estaba afuera en el 1.er piso.
  - El primer piso había empezado sólo 4 s antes (T+540). El rescate genérico de 8 s todavía no había corrido.
  - El collider de `Mansion_Compuerta_A_bajada` estaba activo (el raycast del reporte le pegó a 4.1 m).

  **[VERIFICAR EN UNITY]:** por qué estaba trabada con Doc abajo. Hipótesis: Doc no está en `AliadosAEvacuar(true)` en ese momento
  o quedó inactivo. Reproducir con las posiciones del JSON.
- **Cambio (todo en `Operacion/OperacionDirector.Compuertas.cs`):**
  1. `TodosLosAliadosYaSubieron`: un aliado cuenta como subido sólo si `G.NivelDe(pos) >= destino && !G.EnTorre(pos, out _, 0.8f)`.
  2. **Red de rescate por reja.** En `RescatarAliadosDeOtroPiso`, `lejos ||= EncerradoTrasCompuerta(a)`, donde la condición es:
     - existe una compuerta de bajada `trabada` de la torre `i`, y el aliado está en `G.EnTorre(pos, out i)` o en un nivel menor que
       el del jugador;
     - el umbral es de 3 s y no de 8.
     - Reaparece en `G.SalidaDeTorre(i) − v·2.5` del lado del piso, sobre el NavMesh (`ColocarEnElPiso`).
     - Contador nuevo: `AliadosLiberadosDeCompuerta`.
  3. **#406 "reaparecer tras cada nivel" [VETABLE: "atrás mío" en lugar de "spawn cercano"].**
     - Cuando cambia `pisoDeLasCompuertas` (línea 103) y el jugador lleva `SegundosDeCortesiaDePiso = 6` s de juego afuera de toda
       torre en el piso nuevo, todo aliado vivo con `NivelDe < piso` o `EnTorre` reaparece a 2–4 m detrás del jugador
       (`yo.forward × −3`, NavMesh del piso).
     - Contador: `AliadosTraidosAlCambiarDePiso`.
     - Los 6 s respetan `Bug409u` línea 270: comprueba la bajada abierta 2 s reales después de `EntrarPrimerPiso`, que con dios ×2
       son 4 s de juego.
- **Riesgos:**
  - con el cambio 1, un aliado que se quede parado en el rellano evita que la bajada se trabe. El jugador podría volver a bajar
    (regresión del #366). La red 2 y la cortesía 3 lo sacan en ≤ 6 s; aceptable;
  - `Bug409u` (líneas 270 y 274), `Bug384a` (contador `AliadosReubicadosDePiso`, no tocar su semántica) y `Bug395a`/`Bug398b`
    (azotea con heli) tienen que seguir verdes;
  - no usar `Random` sin semilla en el check: el lugar de reaparición tiene que ser medible.
- **Checks:**
  - `Bug413a` (mansión):
    - `EntrarAzotea` por reflexión, el jugador en (334.9, 20.3, 265.3) y 3 aliados en (338.4, 20.2, 273.5), (339.6, 20.3, 273.1)
      y (340.8, 20.3, 273.0), dentro de la torre C;
    - medir `!CompuertaDeBajadaCerrada(torre C)` mientras sigan adentro;
    - con un bucle de hasta 20 s reales, que `AliadosLiberadosDeCompuerta` o `AliadosTraidosAlCambiarDePiso` suba y que los 3 queden
      `!EnTorre` y con `NivelDe == 3`;
    - después, la bajada se traba.
  - `Bug406a` (mansión):
    - posiciones del JSON de #406 (Doc en PB, Kes dentro de la torre A) y el jugador afuera en el 1.er piso;
    - esperar a que los dos aliados estén en `NivelDe == 1` y `!EnTorre`, con un tope de 25 s reales;
    - repetir para 1→2 y 2→azotea.
  - **Foto pedida `v9_406_aliados_pasan.jpg`:** `FotoCamara` en el 1.er piso, mirando a la salida de la torre A, con el jugador y
    los aliados reaparecidos detrás. Más un "antes" (aliados en la torre) `v9_406_antes.jpg`. Ojo con `PlantasDeLaMansion`: la cámara
    tiene que estar debajo del techo del piso mostrado, o se recortan los renderers.

### #412: "las órdenes están rotas, que cada piso tenga su plano"
- **Hallazgo.**
  - En el JSON del #412, el grupo de la azotea (Doc, Kes, Prisionero_1) estaba en `MovingToOrder/Follow` **encerrado detrás de la
    reja C** (es el #413). Las órdenes "no se cumplían" porque no había camino.
  - Segundo problema (verificado en código): en RTS, `Operacion/PlantasDeLaMansion.cs` deja los pisos superiores en `ShadowsOnly`
    pero **sus colliders siguen activos**, y `Player/AimTargeting.Evaluate` hace un solo `Physics.Raycast`. Una orden dada mirando el
    piso 1 cae sobre la losa oculta del piso 2.
- **Interpretación [VETABLE].** "Plano por piso" es la vista RTS por planta (`PlantasDeLaMansion.NivelMostrado`). No es el minimapa.
- **Cambio:**
  1. (depende de #413) liberar a los encerrados;
  2. en `AimTargeting.Evaluate`, cuando `PlantasDeLaMansion` recorta (RTS o cámara alta dentro de la mansión), usar `RaycastNonAlloc`
     y descartar impactos con `y > G.Nivel(NivelMostrado + 1) − 0.4` dentro de `G.Dentro`;
  3. **opcional [VETABLE]:** RePág/AvPág para cambiar `NivelMostrado` a mano en RTS.

     El minimapa por piso queda fuera del alcance salvo pedido.
- **Riesgo.** Medio: `AimTargeting` lo usan también el apuntado FPS y la IA. Limitar el filtro a cuando el recorte está activo.
- **Check `Bug412a`** (mansión):
  - escuadra en el piso 1 y jugador en RTS con `NivelMostrado = 1`;
  - rayo desde la cámara RTS hacia (322, Nivel(1), 262): el punto de orden tiene `NivelDe == 1` y no 2;
  - una orden de mover a ese punto deja al aliado a < 2 m del destino (bucle, tope de 25 s).

### #414: "que sean sólo 2 milicianos"
- **Respuesta a la duda: son los prisioneros.** `OperacionDirector.Mansion.cs` `PonerPrisioneros()` (línea 451) crea `Prisionero_1..4`
  con nombre visible "MILICIANO n", 160 HP, uno por celda (`G.CeldasDeMilicianos`, 4). `LiberarMilicianos` los suma a
  `MandoTactico.Milicianos`. `MandoTactico.CantidadDeMilicianos = 2` (`MandoTactico.cs:26`) es el mismo concepto en el Mando; no hay
  dos sistemas distintos.
- **Cambio.** En `PonerPrisioneros`, la condición del bucle agrega `i < MandoTactico.CantidadDeMilicianos`.

  Revisar `MilicianosLiberados`, el conteo de `pisoLimpio` (Compuertas.cs:105) y los textos "LIBERA A LOS MILICIANOS" que cuenten 4.
- **Riesgo.** Bajo. Buscar en `Editor/` asserts de "4 prisioneros" o `CeldasDeMilicianos.Length` y actualizarlos.
- **Check `Bug414a`** (mansión):
  - tras `IniciarMansion`, existen exactamente 2 `Prisionero_*` activos;
  - tras `LiberarMilicianos`, `MandoTactico.Milicianos.Count == 2`;
  - el piso 2 da `pisoLimpio` con esos 2.

### #407: "quitá esas 3 luminarias de piso"
- **Identificado.** Lámparas de pie de `Editor/OperacionBuilder.MansionLujo2.cs` `LuzDeLujoIter11a17`, líneas 16–22. Son 6, en
  (309, 254), (335, 254), (309, 268), (335, 270), (322, 250) y (322, 270), en el piso 2.
  - Cada una se compone de `Lampara_Pie_Poste` (sin collider), `Lampara_Pie_Pantalla` (sin collider) y la luz `Luz_Lampara_Pie`.
  - Desde la cámara del #407, en (341.5, 16, 268) mirando al oeste, se ven las 3 de la fila norte: (335, 270), (322, 270) y (309, 268).
- **Decisión [VETABLE]:** quitar las **6** (son el mismo objeto y el dueño las ve de a 3). La alternativa es quitar sólo las 3 del norte.
- **Cambio persistente:**
  - en tiempo de ejecución, sumar `Lampara_Pie_Poste`, `Lampara_Pie_Pantalla` y `Luz_Lampara_Pie` a `Quitar` en
    `Presentation/AjustesDeNivelNov08.cs` (el patrón del #392);
  - **y** borrar la llamada en el builder, para que una reconstrucción no las traiga de vuelta;
  - sacar `Luz_Lampara_Pie` de `LucesBajar`.
- **Check `Bug407a`:**
  - no hay `Lampara_Pie_*` ni `Luz_Lampara_Pie` activos dentro de `G.Dentro` (`FindObjectsByType<Transform>(Include)` + `activeInHierarchy`);
  - foto `v9_407_despues.jpg` desde (341.5, 16, 268) mirando a (309, 14, 268).

### #408: "quitá esto de la base que no colisiona y no aporta nada"
- **Identificado.**
  - El raycast del reporte pasó **a través** del objeto (no tiene collider) y le pegó a `Losa_2_1`.
  - Es el escenario del salón del piso 2: `Editor/OperacionBuilder.MansionLujo.cs` líneas 198–200, con `Escenario`
    (x 296.8–301.8, z 262–272, y2 a y2+0.5), `Escenario_Borde` (dorado) y `Escenario_Telon`. Ninguno tiene collider.
- **Trampa.** El nombre `Escenario` **también es un grupo** de la ruta de fuga (`OperacionBuilder.Huida.cs:210`, `Grupo(g, "Escenario")`).
  Agregar "Escenario" a `Quitar` por nombre apagaría la escenografía de la Huida.
- **Cambio.**
  - Nuevo bloque en `AjustesDeNivelNov08.Aplicar`: apagar `Escenario`, `Escenario_Borde` y `Escenario_Telon` **sólo** si
    `G.Dentro(pos)` y |y − Nivel(2)| < 1.5 (o si el padre es `Detalle` bajo `P_Op_8_Mansion`);
  - borrar las 3 líneas en el builder.
  - **[VETABLE]** el telón: se quita con la plataforma porque queda colgando sin ella.
- **Check `Bug408a`:**
  - ninguno de los tres está activo dentro de la mansión;
  - **y** el grupo `Escenario` de la Huida sigue activo (o, si arranca apagado por fase, sigue con `activeSelf` intacto);
  - foto `v9_408_despues.jpg` desde la cámara del JSON, (302.8, 17.3, 275.3), yaw 206 y pitch 30.

### #411: "ocultá o hacé algo con ese bloque negro"
- **Corrección al original.** No es un `BlockoutObjetivo`. Es `P_Op_8_Mansion/Interior/Azotea_Equipo`, el que ve el raycast del reporte
  en (338.9, 19.8, 278.9).
  - `Editor/OperacionBuilder.Mansion.cs` líneas 394–395 crean 8 cajas `Azotea_Equipo` (material `MBarrote`, con collider) en
    (308, 254), (336, 254), (306, 278), (338, 278), (322, 252), (322, 281), (312, 266) y (332, 266).
  - Tres de ellas tapan huecos de escalera:
    - (338, 278) sobre la torre C, que es el bloque negro del #411 y además el techo bajo que traba el tramo B (#413);
    - (322, 281) sobre la torre E;
    - (308, 254) sobre la torre D.
- **Cambio.**
  - Mover esas 3 cajas fuera de los huecos, a 6 m hacia el centro del techo y verificando `!G.EnTorre`. Esto mantiene la cobertura de
    la azotea.
  - Ruta persistente: en el builder (cambiar coordenadas) **y** en tiempo de ejecución, en `AjustesDeNivelNov08`: si un `Azotea_Equipo`
    tiene bounds que intersecan el hueco de una torre (`G.EnTorre` del centro con margen), se lo traslada.
  - **[VETABLE]:** borrar en vez de mover.
- **Riesgos:**
  - `Presentation/LimpiezaDeDuplicados.SepararClaraboya` corre la `Claraboya` 8 m si está cerca de un `Azotea_Equipo`, y `Bug410u`
    exige que no se intersequen. Correr `Bug410u` después del cambio;
  - `HacerPasablesParaEnemigosLosObstaculosDelTecho` (Azotea.cs) busca por nombre: sigue funcionando.
- **Check `Bug411a`** (mansión):
  - ningún `Azotea_Equipo` activo tiene bounds que intersequen el volumen de un hueco de torre (x/z del recuadro de la torre, y
    Nivel(3) ± 2);
  - un `CapsuleCast` de soldado (radio 0.35, alto 1.8) a lo largo del tramo B de la torre C llega sin chocar;
  - foto `v9_411_despues.jpg` desde (339.2, 18.2, 283.2), pitch −20 y yaw 184.

### #409 y #410: "bug de colliders de disparos"
- **Causa (verificada).**
  - El raycast del #409 le pegó a `Mansion_Compuerta_D_subida` en (300.7, 14.8, 260.2); el del #410, al lado, a `Torre_D_Tramo_A`.
  - Las compuertas son `BoxCollider` invisibles de 4.9×4.4×0.8 (desde el #366 no tienen placa visible), y `Poner(c, cerrada)`
    (Compuertas.cs:80) habilita el collider cuando están cerradas.
  - `Combat/Projectile.ChocoContraElMundo` frena la bala en todo collider que cumpla `Core/NavService.BlocksMovement(c)` (línea 182),
    y la compuerta lo cumple. Resultado: balas frenadas en el aire con calcomanías flotando.
- **Cambio.**
  - Componente marcador nuevo `NoFrenaBalas` (vacío), agregado en `CrearCompuerta`;
  - `Projectile.ChocoContraElMundo`, `Player/AimTargeting.PuntoDeTiro` / `Evaluate` y el chequeo de alcance de explosiones
    (`LaExplosionAlcanza`) ignoran colliders con `NoFrenaBalas`;
  - `NavService.BlocksMovement` **no cambia** (la compuerta tiene que seguir frenando el movimiento).
- **Riesgo.** Bajo:
  - los enemigos podrán dispararse a través de la reja: es lo esperado, porque no se ve;
  - `ChocoContraElMundo` lo toca también #400: hacer #409 primero.
- **Check `Bug409a`** (mansión):
  - con la subida D cerrada, `Projectile` del jugador disparado a través del plano de la compuerta hacia un muro a 10 m;
  - medir que la calcomanía o el impacto quede en el muro (distancia a la compuerta > 2 m) y no en la compuerta;
  - `NavService.BlocksMovement(colliderDeLaCompuerta)` sigue en true.
- **Check `Bug410a`:** lo mismo con un enemigo detrás de la compuerta. La bala le pega (`DamageTakenEvent` del enemigo) y los
  `AimTargeting` del jugador lo ven a través de la reja.

### #404: "quitá esa línea punteada"
- **Corrección al original.** No es `GuiaDeObjetivo`: su línea ya fue quitada.
- **Estado: NO IDENTIFICADO solo con código.**
  - Se ve una fila de cuadrados amarillos (unos 0.6 m, separados unos 0.75 m) a lo largo de x ≈ 322. Va desde la plaza (z ≈ 230,
    también se ve en la captura del #403) sobre `Alfombra_Gala` (PrestigioMansion.cs:33) y sigue dentro de la planta baja sobre
    `PB_Alfombra` (Mansion.cs:322).
  - Está por encima de las alfombras y algunos cuadrados se ven dobles o inclinados.
- **Descartados por código:**

  | Candidato | Por qué no |
  |---|---|
  | `GuiaDeObjetivo` | línea quitada |
  | `GuiaDeMando` (mismo punteado `TexturaPunteado`) | exige `Subfase ≥ 1 && EnRadio`; el JSON tiene `radioTomada=False` y `subfase=0` |
  | `RutaAlObjetivo` | necesita `MisionDirector` |
  | `PathPreview` | blanco |
  | `AttackLineManager` | rojo, sólo RTS |
  | `Linea_T` / `Linea_P` de `OperacionBuilder.Ciudad.cs:285–286`, `Linea` de `CiudadDeCubos.cs:335` | cada 9 m, 4 m de largo, y en y 0.02–0.10 debajo de la alfombra |
  | `OrderMarkerFx`, `TrazadoDeCamino`, `FlechaDeOleada` | no coinciden |

- **Procedimiento de identificación (primer paso del lote 2, con `unity command eval` en Play, fase Resistir, `OperacionPrueba.Arrancar(5)`):**
  1. Listar todo `Renderer` activo con bounds que intersequen la caja x 320–324, z 225–285, y 0–0.8:
     - nombre, ruta completa (`GetComponentsInParent`), tipo (`MeshRenderer` / `LineRenderer` / `ParticleSystemRenderer`);
     - `sharedMaterial.name`, color y `mainTexture.name`.
  2. Filtrar por color amarillo (r > 0.7, g > 0.6, b < 0.4) o por textura `Punteado`.
  3. Repetir en `SubFase 0` y en `SubFase 1` (por si es `GuiaDeMando` con una condición distinta de la que leí).
  4. Anotar el creador (buscar el nombre en `Scripts/`) y recién ahí elegir la ruta:
     - si es del builder: `AjustesDeNivelNov08` + builder;
     - si es de tiempo de ejecución: apagar el componente o su condición.
- **Check `Bug404a`:** ningún renderer amarillo o punteado activo en esa caja durante Resistir (subfases 0 y 1), con el mismo filtro del
  paso 2. Foto `v9_404_despues.jpg` desde (332.7, 4.3, 263.6), yaw 234 y pitch 29.

### #403: "hay partículas trabadas"
- **Estado: NO IDENTIFICADO solo con código.**
  - La captura (jugador en (323.9, 0.8, 226.2), plaza frente a la escalinata) muestra **astillas o cuadraditos dorados quietos en el
    piso** alrededor del jugador, y la línea del #404.
  - Ojo: `ts=0` en el reporte, todo sale quieto.
- **Candidatos a revisar:**
  - `Presentation/Fragmentador` (`Fragmento`): queda asentado en kinematic sin collider y debería hundirse a los `Vida` s. Si `Vida`
    es grande, o el pool de 200 recicla tarde, quedan "trabados";
  - chispas o casquillos de `Presentation/ImpactFx.cs`;
  - sistemas en bucle de `Editor/AmbientacionDelNivel.cs` (`Llama` 432, `Brasas` 482, `Luciernagas_n` 658 con vida de 4–8 s);
  - los del helicóptero (`Mision/Helicoptero.cs` 292 y 366).
- **Procedimiento:**
  1. En Play, tras 60 s de combate en la plaza, listar a menos de 12 m de (323.9, 0.8, 226.2):
     - `Fragmento` con `EnUso` (`Edad`, `Vida`, `Asentado`);
     - `ParticleSystem` vivos (`particleCount`, `main.loop`, `main.duration`, `isPaused`, `main.simulationSpeed`, `collision.enabled`);
     - decals de `MarcaDeImpacto`.
  2. Volver a listar 20 s después **sin disparar**. Lo que siga igual (misma posición y misma `Edad` o el mismo conteo) está trabado.
- **Cambio** (según lo que aparezca):
  - para `Fragmento`, un tope de vida absoluto (por ejemplo 12 s) y hundimiento aunque esté asentado;
  - para partículas, `stopAction = Destroy` / `Disable` y `loop = false` en los efectos de impacto.
- **Check `Bug403a`:**
  - disparar 60 balas y 2 granadas contra una barrera en la plaza;
  - esperar 25 s reales sin fuego;
  - medir `Fragmentador.Activos == 0` (o ≤ N del ambiente fijo) y que ningún `ParticleSystem` de impacto tenga `particleCount > 0`.

### #405: z-fighting ("fijate si hay un shader que lo solucione para todos")
- **Identificado el lugar.** `P_Op_8_Mansion/Escaleras/Torre_A_Puerta_D2` en (304.4, 1.7, 272.8), la puerta de la torre A, vista
  desde el oeste a 15 m. Hay caras coplanares entre `Torre_A_Puerta_D2`, `Torre_A_Puerta_D2_Relleno` y el muro (`ArmarTorre` en
  `Editor/OperacionBuilder.Mansion.cs`).
- **Respuesta al "shader para todos" [VETABLE].** No hay arreglo global seguro:
  - `Offset -1,-1` en el material URP compartido mueve todas las superficies por igual, así que no resuelve dos caras con el mismo material;
  - subir el `nearClipPlane` (si hoy está en 0.1 o menos) reduce el z-fighting lejano en toda la escena;
  - evaluarlo, pero no reemplaza separar geometría.
- **Cambio:**
  1. correr `Editor/DetectorDeSolapes.Informe(centro, tamano, maximo, soloVerticales)` (sólo lectura, del #298) sobre la mansión
     completa y la plaza;
  2. separar en el builder las piezas reportadas 2–3 mm (los `Relleno` hacia adentro), o escalarlas 0.998 en tiempo de ejecución
     desde `AjustesDeNivelNov08` sólo para deco sin collider;
  3. revisar `Camera.nearClipPlane` de la cámara de juego: subirlo a 0.2–0.3 si no rompe la vista FPS.
- **Check `Bug405a`:** `DetectorDeSolapes.Informe` sobre las bounds de `P_Op_8_Mansion` devuelve 0 pares coplanares con materiales
  distintos (o menos de N, con la lista en el resultado). Foto `v9_405_despues.jpg` desde (293.3, 2.6, 262.4) mirando a (304.4, 1.7, 272.8).

### #402: "todo destructible": alcance acotado
- **Alcance propuesto [VETABLE].**

  | Categoría | Ejemplos | Decisión |
  |---|---|---|
  | A. Destructible (ya o a convertir) | vallas, barreras Jersey, cajas, macetas, bancos, quioscos y puestos de la plaza (`ObstacleMarker` + `Fragmentador`), autos | sí |
  | B. Destructible por partes | columnas de la plaza y fuentes ornamentales (`TorreDestruible` o colapso por golpes) | sí, con tope de cantidad |
  | C. Indestructible por diseño | suelo, calles, casco y losas de la mansión, muros portantes, torres de escalera, compuertas, edificios de fondo (NavMesh y recorrido) | no; documentado |
  | D. Decorativo sin collider | lámparas, alfombras, luces | no aplica (no se le puede pegar) |

- **Criterio de "listo":**
  - todo renderer **con collider** a menos de 60 m de la plaza o dentro de la mansión es de la categoría A o B (tiene `ObstacleMarker`,
    `TorreDestruible` o `Vehicle`) o figura en la lista blanca de la categoría C (por prefijo de nombre: `Ground`, `Losa_`, `Muro_`,
    `Torre_`, `Mansion_Compuerta_`, `Edificio_`, `Calle`);
  - **cero objetos sin clasificar.**
- **Rendimiento:**
  - `Fragmentador.Cupo = 200` ya pone tope;
  - no agregar Rigidbody a objetos quietos: se fragmentan sólo al romperse;
  - medir FPS en la plaza con 20 destrucciones seguidas (no bajar más de 10 % respecto de la línea base).
- **"Prefabs bien armados".** El nivel se arma por código (`OperacionBuilder.*`), no hay prefabs por pieza. Se interpreta como "cada
  destructible tiene collider, `ObstacleMarker` con vida y material del arte" y eso se audita.
- **Check `Bug402a`** (auditoría, sin Play largo): lista por categoría, falla si hay objetos sin clasificar en el radio y los imprime
  (máximo 30). Se apoya en `Editor/ChecksBugs065.AuditoriaDeNivel.cs` (501a–f), sin cambiar esos checks.

---

## 2. Lotes (reordenados por dependencia y riesgo)

| Lote | Bugs | Archivos que toca | Paralelo con |
|---|---|---|---|
| **L0 Preparación** | — | `Editor/ChecksBugs065.cs` (regex `reMansion`); identificación de #404 y #403 con eval (sólo lectura) | nada (va primero) |
| **L1 Mansión: lógica** | #413 → #406 → #412 | `Operacion/OperacionDirector.Compuertas.cs`, `Player/AimTargeting.cs`, `Operacion/PlantasDeLaMansion.cs` | L3 y L4 |
| **L2 Mansión: datos y escena** | #411 (antes de cerrar #413), #414, #407, #408, #404 | `Presentation/AjustesDeNivelNov08.cs`, `Editor/OperacionBuilder.Mansion.cs`, `.MansionLujo.cs`, `.MansionLujo2.cs`, `Operacion/OperacionDirector.Mansion.cs` | L3 y L4 |
| **L2b Disparos** | #409 + #410 | `Operacion/OperacionDirector.Compuertas.cs` (`CrearCompuerta`), `Combat/Projectile.cs`, `Player/AimTargeting.cs` | después de L1 (mismos archivos) |
| **L3 HUD del tanque** | #396 + #399 | `UI/HudDeTanqueSimple.cs`, `UI/VehicleStatusView.cs`, `UI/CapasDeHud.cs`, `UI/ModoDiosView.cs` | L1 y L2 |
| **L4 Combate del tanque** | #400 → #397 → #398, #401 | `Vehicles/TurretWeapon.cs`, `Player/PlayerInputDriver.Vehiculo.cs`, `UI/TurretAimView.cs`, `UI/MiraDeTorreta.cs`, `Combat/Projectile.cs` (sólo lectura si L2b ya entró), `Operacion/OperacionDirector.Huida.cs`, `UI/DamageVignetteView.cs`, `Presentation/UnitLocatorCylinder.cs`, `Editor/HeadlessTestRunner.cs` | L1 y L2; **#398 feedback toca `HudDeTanqueSimple` (destello): después de L3** |
| **L5 Calidad** | #403, #405, #402 | según la identificación; `Presentation/Fragmentador.cs`, builders, `AjustesDeNivelNov08.cs` | último (toca builders y AjustesDeNivel que usa L2) |
| **Cierre** | — | — | — |

Archivos compartidos (no en paralelo):
- `OperacionDirector.Compuertas.cs`: L1 y L2b. `AimTargeting.cs`: L1 (#412) y L2b (#409).
- `HudDeTanqueSimple.cs`: L3 y L4 (#398). `TurretWeapon.cs` y `PlayerInputDriver.Vehiculo.cs`: #400 y #397.
- `AjustesDeNivelNov08.cs`: L2 (#407, #408, #411, #404) y L5 (#405).
- `OperacionBuilder.*`: L2 y L5.

Con un solo agente que compile Unity, "paralelo" significa preparar el código en ramas o diffs separados y compilar uno por vez: hay un
solo editor.

Orden dentro de L4: #400 antes que #397, porque el punto de mira de #397 usa la balística nueva. #398 se decide con el dueño antes
de empezar (riesgo de balance).

---

## 3. Criterios de aceptación globales
1. Cada bug tiene su check nuevo, registrado y en verde en `CorrerLista`, y los de mansión matchean `reMansion`.
2. Regresión verde: `Bug101a Bug602c Bug603g Bug128 Bug122 Bug123 Bug284a` y todo `Bug368a`…`Bug398b`. Esto incluye los ya existentes
   `Bug396a`, `Bug397a`–`e`, `Bug398a`–`b`, más:
   - `Bug374a` (HUD del tanque);
   - `Bug384a` (rescate de piso);
   - `Bug395a`;
   - `Bug403u` y `Bug409u` (multiplicador de daño y compuertas; se actualizan **a propósito** si se aprueba ×2);
   - `Bug410u` (claraboya / `Azotea_Equipo`);
   - `Bug378a` (conversión a tanque).
3. Headless (`HeadlessTestRunner`) verde, incluidos los asserts de velocidad de `TurretWeapon` actualizados para #400.
4. Fotos en `Assets/Validacion~`: `v9_396_hud_tanque`, `v9_397_atlas`, `v9_398_tanques`, `v9_404/405/406(antes+después)/407/408/411_despues`.
   Atlas de cierre con una fila por bug (antes = PNG del reporte, después = foto del check).
5. Ninguna reconstrucción de escena es obligatoria para que el arreglo funcione: lo que se quita o mueve se hace en tiempo de ejecución
   (`AjustesDeNivelNov08`) **y** en el builder. Si se reconstruye, el resultado tiene que ser el mismo.
6. Sin errores nuevos en consola durante una partida de Resistir completa.

## 4. Qué NO tocar
- `NavService.BlocksMovement`: las compuertas tienen que seguir frenando el movimiento; sólo se exime a las balas.
- La semántica de `AliadosReubicadosDePiso` y `SegundosDeRezagoDePiso` (lo mide `Bug384a`): agregar contadores nuevos, no reusar.
- `TurretWeapon.SpeedMultiplier` como const (lo usan la IA y los tests): se agrega una propiedad de instancia al lado.
- `TurretAI` y la velocidad de las balas enemigas (el #400 es sólo para el jugador).
- Las escenas `.unity` y los prefabs a mano: todo pasa por los builders o por `AjustesDeNivelNov08`.
- El grupo `Escenario` de la Huida (`OperacionBuilder.Huida.cs:210`).
- `Claraboya` / `SepararClaraboya`, salvo para mantener verde `Bug410u`.
- `RomboVisibilidad.Suprimidos` y `CineCorta` (las cinemáticas siguen ocultando rombos).
- `DetectorDeSolapes` es de sólo lectura: no convertirlo en algo que modifique la escena.
- `Bug403u` y `Bug409u` se editan sólo si el dueño aprueba el ×2 de #398, y en el mismo commit que la constante.

## 5. Decisiones que el dueño puede vetar (resumen)
- #400: triple sólo para el cañón del tanque del jugador.
- #398:
  - ×2 = daño recibido por el tanque del jugador (`MultiplicadorDeDanoDelTanque` de 4 a 8, que rompe y obliga a ajustar
    `Bug403u`/`Bug409u`);
  - tanques determinísticos de 3 a 6 (uno de cada dos autos desde el #2).
- #401: rombos de enemigos siempre encima, sólo si están revelados (revierte el #174).
- #406: reaparición "detrás del jugador" a los 6 s de entrar a un piso nuevo.
- #407: se quitan las 6 lámparas de pie, no sólo 3.
- #408: se quita también el telón.
- #411: mover (no borrar) las 3 `Azotea_Equipo` que tapan huecos de escalera.
- #412: "plano" = vista RTS por planta. El filtro de losas ocultas se hace; RePág/AvPág es opcional.
- #402: alcance por categorías A–D con lista blanca de indestructibles.
- #405: no hay "shader para todos". Se separa geometría y, como mucho, se ajusta `nearClipPlane`.
