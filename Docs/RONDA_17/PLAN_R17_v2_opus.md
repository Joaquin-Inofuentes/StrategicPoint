# RONDA 17 - Plan v2 (revision Opus 5.5) - 2026-11-20

Base: `PLAN_R17_v1.md` (15 pedidos de Joaquin del 20/11). Todo lo de la seccion 1 se verifico contra el codigo, con archivo y linea. Las rutas son relativas a `Assets/_Project/Scripts/`. Las suposiciones van marcadas **[SUPUESTO]** y las decisiones que tiene que tomar Joaquin, **[DECIDE JOAQUIN]**.

Leyenda de prioridad: **[IMP]** imprescindible para dar el pedido por cumplido · **[OPC]** opcional, suma calidad, se puede cortar.

---

## 1. Correcciones al diagnostico del v1

| # | Lo que dice el v1 | Lo que hay de verdad en el codigo | Impacto en el plan |
|---|---|---|---|
| 1 | Emision enemiga (0.30,0.06,0.04) y tinte (1,0.78,0.7) | Correcto (`Actors/SoldierLook.cs:69,79`). Ademas el material es **uno solo compartido** (`matEnemigoVisible` estatico) y el bloom esta en intensidad 0 (`Presentation/PostFxDirector.cs:125`): una emision HDR no "brilla", solo aclara. No existe ningun contorno 3D en el proyecto (los `Outline` que aparecen son de UI). | El contraste no puede depender del bloom. Un contorno con casco invertido duplicaria el skinning de mas de 100 mallas: se descarta. Se resuelve con el material compartido (no cuesta nada) y una insignia emisiva por soldado. |
| 11 | El escudo tiene emision casi nula y no lleva el rojo del bando | La placa casi no emite (`EnemigoEscudero.cs:122`), pero la **franja amarilla y la ventana naranja si emiten** (l.124-125). El uniforme es grafito 0.16 con emision gris (0.10,0.10,0.12) (`SoldierLook.cs:261-267`). Se crean **4 materiales `new Material` por escudero** (l.122-125): rompe el batching. **Bug que el v1 no vio:** un headshot de frente contra el escudo muestra el cartel HEADSHOT y la sangre del headshot mortal, aunque el escudo anula el dano (`Projectile.cs:362-372` anuncia antes de que `Health.FiltroDeDano` corte en `Health.cs:165`). | El cartel tiene que saber si el golpe pasa ANTES de anunciarlo (ver 2.5). Materiales compartidos, y el destello de la ventana con `MaterialPropertyBlock`. |
| 2/15 | "CazaDeEnemigos convierte en cazadores solo a los marcados" | **Falso.** `CazaDeEnemigos.Tick` convierte en cazador a **cualquier** enemigo herido (`Current < Max`) o que tenga blanco, y alerta a su grupo a 16 m (`CazaDeEnemigos.cs:73-94`). Los huecos reales: (a) solo revisa cada **2 s**; (b) `Libre()` exige `!Quieto` (l.62): un cazador Quieto nunca recibe rumbo; (c) **un headshot mata en el acto** y al muerto no lo procesa nadie (`IsAlive` en l.76): el grupo no se entera; (d) `OnAnyDamage` solo hace que el vecino ayude si el atacante esta a <= 30 m de el (`AiBrain.cs:592`) y el oido llega a 48 m del tirador (`RadioDeOidoEnemigo`, l.33/631). Resultado: un francotirador a mas de 48 m mata sin que nadie reaccione; (e) el que persigue suelta el blanco a los **5 s** sin verlo (`tiempoMaximoSinVisibilidad`, l.56/866); (f) Quieto pone la postura en Defensiva, que lo ata a 8 m de su puesto (`AiBrain.Tactica.cs:96-111`, `Sentidos.cs:135-146`). | El componente nuevo se apoya en `CazaDeEnemigos` y no la duplica. Se agregan los testigos de una baja y una salida explicita de Quieto, salvo para los que estan quietos por su rol. |
| 2/15 | "Sacarla de Quieto/Pasivo" | Peligroso. Varios enemigos estan Quieto **por diseno**: el francotirador (`EnemigoFrancotirador.cs:64`), la emboscada de cohetes (`EmboscadaDeCohetes.cs:143`) y el pesado (`EnemigoPesado.cs:140,148`). Y Pasivo es el que baja colgado de la soga del helicoptero (`HelicopteroEnemigo.cs:219`). | Se agrega a `AiBrain` una bandera `PuestoFijoPorRol`: esos enemigos se dan vuelta y disparan, pero no salen de su puesto. A los Pasivo no se los toca. |
| 14 | "No hay zona que lance el asalto contra el tanque" | Ya existe el fuego de oportunidad contra vehiculos (`AiBrain.Vehiculos.cs:136`) y una orden de atacar un vehiculo que **sirve tambien para enemigos** (`IssueAttackVehicleOrder`, l.42). Los enemigos no usan `EmboscadaDeCohetes` ni `OperacionAuto` para esto, como dice el v1. Y hay una lectura que el v1 no considero: la pelea contra el tanque jefe (BLINDADO GOLIAT) **arranca sola** 2,5 s despues de que cae el muro, con el jugador todavia adentro del centro de datos (`OperacionDirector.Jefe.cs:212-213`). Solo el permiso de disparar espera a que el jugador pase z = -30 (l.220-224). | Dos interpretaciones (ver 2.10). Se recomienda un disparador espacial para la pelea contra el jefe. |
| 5 | "Projectile: una sola linea (ExentoDeInstakill)" | No alcanza. El instakill se decide en `Projectile.cs:362`, pero el anuncio, el efecto mortal y el `HeadshotEvent` salen antes (l.364-371). Ademas `CapturasDeBuild.Combate.cs:469` llama a `TakeDamage(10, id, true)` directo y se saltea `Projectile`. | La mascara se modela en `Health` (capas de cabeza), que cubre todos los caminos, y `Projectile` la consulta para elegir cartel y efecto. |
| 8 | Pesado: 3 fases, vida x12, sin telegrafo | Correcto, pero faltan tres defectos: (a) `ve` no mira la linea de tiro (`EnemigoPesado.cs:136`). Si le pegan desde una cobertura, se planta en Frenesi tirando contra la pared unos 8 s (5 s hasta soltar el blanco + 3 s para relajarse); (b) todo usa `Time.time` dentro de `Update` (l.140-150), asi que la suite headless no lo puede simular; (c) en Frenesi saca el lanzacohetes (l.105) y las fotos salen tapadas por explosiones (memoria del 13/11). Vida real: 100 x 12 = 1200, unos 46 tiros de fusil (26 de dano). | Primero se corrigen la vista y los timers (pasan a `Tick(dt)`), y despues se itera. La "barra de vida propia" del v1 **no se recomienda**: `hud.BarraDeJefe` es una sola y puede haber varios pesados a la vez (cada 6.o refuerzo, cada 8.o en la mansion). |
| 6 | "`OperacionTerminal.Habilitado` ya existe; Centro la usa por fase" | **Falso.** Nadie escribe `OperacionTerminal.Habilitado` (grep: solo `CargaExplosiva.Habilitado` se escribe). Con `Habilitado=false`, `Update` sale en silencio (l.199), `CanInteract` esconde el cartel (l.86) y `EnviarA` responde "YA ESTA HECHO" (l.120): el bloqueo seria invisible. Hay una sola `OperacionTerminal` en el juego, la COMPUTADORA del centro de datos (`Editor/OperacionBuilder.Centro.cs:69-70`). La "PC de las celdas" no es una `OperacionTerminal`: es un `[E]` a mano en `OperacionDirector.Mansion.cs:308-311`. Y el **bug #333 pidio a proposito** que se abriera "aunque queden guardias" (l.305). **Lo mas importante:** el centro de datos **no tiene guardias antes del hackeo**. Las 3 oleadas son reservas inactivas que se activan al empezar el hackeo (`OperacionDirector.Centro.cs:127-137`) y `SaltarA` apaga a los enemigos de las fases anteriores (`OperacionDirector.cs:713-725`). | Bloquear "hasta matar a todos" hoy no bloquea nada, o deja el juego trabado si se cuentan las reservas. Ver el diseno anti-bloqueo en 2.6: se agrega una guarnicion y un bloqueo que se suelta una sola vez. |
| 3/10 | Interior "sin vida (LEDs, cables, cartelera)"; exterior liso | El builder ya pone gabinetes con LEDs, bandejas y cables en el techo, ductos, rejillas, cinta amarilla, flechas en el piso, paredes de video, puestos de operador, 3 puertas con lector y baliza, y extintor (`Editor/OperacionBuilder.Servidores.cs:28-210`). `SalaCubierta` agrega el techo, los remates, las antenas, los equipos de aire y el rotulo (`OperacionBuilder.SalaCubierta.cs:12-18`). | Lo que falta es **movimiento** (los LEDs estan fijos), el piso (cables y equipos), el sonido, la reaccion al hackeo y el exterior **a la altura del suelo**. Lo que propone el v1 repite lo que ya existe. |
| 12 | "Muerte del Animator, sin reaccion direccional" | El Animator ya tiene **6 clips direccionales**: "death from the front / back / right", "front headshot", "back headshot", "crouching headshot front" (`Editor/ArtBuilder.cs:64-67`). Pero `CubeFxReactor.MorirAnimado` **sortea uno al azar** (`Presentation/CubeFxReactor.cs:254-255`). Ademas hay un tope de 24 cadaveres (l.233) y el retiro a los 14 s es encoger el cuerpo a 0,02 (`RetiroDeEnemigosCaidos.cs:62-67`), que se ve como un "pop". | La mejora principal cuesta cero arte: elegir el clip por la direccion del golpe y por headshot. El "empujon con giro" del v1 pelearia con el clip: se descarta. |
| 13 | Anillo solo en Abordaje | Correcto, pero falta esto: `AnilloDeSubida` es un **singleton que tambien usa el circulo de la plaza** (`OperacionDirector.Resistir.cs:316-318`; el v1 queria convertirlo en un "modo generico" y chocaria). Ademas el aro late entre 0,8 y 1,4 x 3,4 m y no marca el radio real de uso (`interactRadius` = 3,5 m medido desde el pivote del tanque, `Player/PlayerInputDriver.cs:102,1651`). Tampoco tiene un estado "ya estas en rango". | Aro fijo del radio de uso, mas aros de llamada que se cierran al acercarse. La API publica de la instancia se mantiene. |
| 9 | "1904 lineas, 22 pasos de R10" | Son **37 pasos** (`TutorialManager.cs:271-1120`; el README dice 36). El autoplayer los resuelve **por Id** (`TutorialAutoPlayer.cs:168-202`), asi que filtrar por Id es viable. El paso guardado es un **indice** en PlayerPrefs (l.1657/1794): si cambia la lista, retoma en el paso equivocado. | Modo simple = filtro por Ids despues de `DefinirPasos`, con una clave de progreso propia. |
| 7 | Nivel 1 = Mision (`SC_Gameplay`) | El menu llama NIVEL 1 a "STRATEGIC POINT" (`SC_Gameplay`) y NIVEL 2 a "OPERACION CUARTEL" (`Presentation/MainMenuController.cs:122-130,192-200`). **Los otros 14 pedidos son todos de la Operacion** (servidores, PC, tanque, pesado, escudero, fuente). | Ver la interpretacion en 2.12. |
| 4 | Fuentes sin particulas | Correcto. Estan en `SC_Operacion_Ciudad`. Ya hay mallas fijas `Fuente_Chorro` y `Fuente_Gota` (`FuentesDeAgua.cs:29-30`) y fabricas reutilizables (`ParticleMaterialFactory`, `PolvoDelDesierto` arma un `ParticleSystem` en runtime). | Se agregan sistemas hijos con presupuesto fijo. |

Otros hechos que el v1 no tiene en cuenta y condicionan el plan:
- **Costuras que hay que reutilizar:** `CameraRig.KickDirectional` (sacudida con tope global de 0,45, `Camera/CameraRig.cs:366`), `EnemyAlertIndicatorView` (marca "!" del enemigo alertado), `AvisoCentral`, `OperacionHud.Aviso/Prompt/Objetivo`, `VivosDeLaMansion` / `RevelarEnemigosDeLaMansion` / `RescatarEnemigosAtascados` (`OperacionDirector.Mansion.cs:174,344,366`), `Atrincherar.Aplicar`, `EntradaDeReserva.Marcar`, `SpriteFx.Lanzar("smoke_02", ...)`, `ParticleMaterialFactory.CreateTransparent`, y los `SfxKind` que ya existen: `EnemySpotted`, `ImpactoEscudo`, `ImpactMetal`, `EmptyClick`, `HoloOn`, `PasoPesado`, `Logro`.
- **Dos suites distintas.** `HeadlessTestRunner` (Edit mode, `SimStep(dt)`, `Time.time` congelado, `OnEnable` no corre, hay que llamar a `ActorRegistry.Rebarrer()`) y `ChecksBugs065.*` (Play mode, `OperacionPrueba.Arrancar`). La logica pura nueva va a una `RunPhase24` headless. La integracion va a checks `ChecksBugs065.R17_*`.
- **Checks que van a cambiar de contrato:** el del bug #291 en `ChecksBugs065.Bugs231a255.cs:204-210` exige que la toma del jefe arranque unos 2,5 s despues de que se abre la salida, sin cruzar z = -30. Hay 19 usos de `computadora.Enganchar` / `PruebaToqueE` en checks (Bugs298a302, Bugs303a316, R13_T12, R13_T6, Wp10, Wp5a/b, ChecksBugs104.*). Los de `HeadshotMataAlInstante` estan en `Wp1.cs:101,476`.
- **Un nombre que ya esta usado:** `CapturasDeBuild.Reportes17.cs` son los reportes del 17/11. El grupo de tomas nuevo se llama **`ronda17`** (`CapturasDeBuild.Ronda17.cs`, registrado en `CapturasDeBuild.Lista.cs:62`).

---

## 2. Diseno por item

Orden de los checks: el numero es el del pedido, y el sufijo, el tipo de verificacion (H = headless, P = Play, T = toma de captura).

### 2.1 Mascara del pesado: headshot en dos etapas (pedido 5) [IMP]

**Regla:** el 1.er headshot del bando jugador rompe la mascara: hace el dano normal x2, no mata y deja la cabeza expuesta. Recien el 2.o headshot es el instakill. El cuchillo y el dano al cuerpo no cambian.

**Mecanica, generica y en `Combat`:**
- `Combat/Health.cs`: `public int CapasDeCabeza { get; set; }` (0 = normal) y `public event System.Action CabezaExpuesta;`. En `TakeDamage`, dentro del `if (headshot)` (l.185): si `CapasDeCabeza > 0`, entonces `CapasDeCabeza--; headshot = false; CabezaExpuesta?.Invoke();` y sigue como dano normal. Va **despues** de `FiltroDeDano` (l.165): un escudo que anula el golpe no gasta la capa. `Initialize` **no** reinicia las capas: las pone el dueno, que corre despues de Initialize. Sirve para los tres caminos: Projectile, la captura que llama directo y cualquier instakill futuro.
- `Combat/Projectile.cs:357-372`, en este orden:
  1. `bool bloqueado = hit.Health.BloqueaGolpeDe?.Invoke(ownerId) == true;`. Es un predicado **puro** nuevo en Health (`Func<int,bool>`), que el escudero llena con `ProtegeContra`. Si `bloqueado`, entonces `headshot = false`: no hay cartel ni efecto. Esto corrige el bug del escudo.
  2. `bool mascara = headshot && hit.Health.CapasDeCabeza > 0;`
  3. `instakill = headshot && !mascara && HeadshotMataAlInstante && ...` (resto igual).
  4. El `HeadshotEvent` se publica igual cuando hay mascara (es un tiro preciso; `EstadisticasDeMision` lo cuenta). `AnunciarHeadshot` recibe un estado: `MataAlInstante / Mascara / Normal`. Para `Mascara` hay un cartel nuevo en `UI/HeadshotPopup.cs`, `MostrarMascara()`: "¡MASCARA ROTA!" en ambar. No hay `EfectoDeHeadshotMortal`.
  5. Se llama `TakeDamage(finalDamage, ownerId, headshot && !bloqueado)`. Health decide entre capa e instakill: se le pasa `headshot`, no `instakill`, y `HeadshotMataAlInstante=false` se respeta pasando `false`.
- `Operacion/EnemigoPesado.cs`: en `Start`, despues de `Initialize` (l.77): `soldado.Health.CapasDeCabeza = 1; soldado.Health.CabezaExpuesta += RomperMascara;`. Se desuscribe en `OnDestroy` (hoy no hay `OnDestroy`: agregarlo).

**Presentacion (`EnemigoPesado`, metodo `ArmarMascara()`):** 3 cubos primitivos (placa frontal, visor y filtro lateral), hijos del hueso `Head` (mismo camino que `HitboxCabeza.De`: `anim.GetBoneTransform(HumanBodyBones.Head)`), **sin collider** (layer 2). Material **compartido estatico**, metal oscuro (0.12,0.12,0.14), con un visor emisivo rojo tenue (0.6,0.08,0.05). La caja de la cabeza no cambia de tamano.
- `RomperMascara()`: las piezas se sueltan (SetParent null, Rigidbody masa 1, impulso hacia el tirador + arriba 2 m/s, capa `Fragmentador.LayerSinRayos`, `Destroy` a los 8 s), `ChispasDeMetal.Spawn` en la cara, `AudioDirector.PlayAt(SfxKind.ImpactMetal, cabeza, 1f, 0.75f)`, `CameraRig.KickDirectional(dir, 0.12)` si el jugador esta a menos de 15 m, ojos emisivos rojos (1.6,0.15,0.1) con un cubo chico en la cara (para que el 2.o tiro tenga a que apuntar) y **aturdimiento** (ver 2.2).
- Desde ese momento la mascara rota se ve: el pesado lleva la cara al aire.

**Riesgos:** (a) `HeadshotMataAlInstante=false` en `Wp1.cs:476`: con `false` no se gastan capas, porque se pasa `headshot && HeadshotMataAlInstante`. Hay que documentarlo en el comentario. (b) `FiltroDeDano` es un solo delegado; si algun dia un pesado lleva escudo, hay que encadenarlos (hoy los dos son excluyentes por el `else if` en Resistir/Mansion/Extraer). (c) Las piezas sueltas por pesado son 3 Rigidbody por 8 s, despreciable.

**Aceptacion:**
- `H24.5a` (headless, sin escena): soldado con `Health.Initialize(id,1200)` y `CapasDeCabeza=1`. `TakeDamage(52, jugador, true)`: sigue vivo, `CapasDeCabeza==0`, se disparo `CabezaExpuesta`. Despues `TakeDamage(52, jugador, true)`: muere. Con `Invulnerable` o con un `FiltroDeDano` que devuelve 0, la capa no se gasta.
- `P R17_5b` (Play, `Arrancar(5)` con un pesado del refuerzo): dos balas a `HitboxCabeza.Centro`. Tras la 1.a: vivo, `HeadshotPopup` en modo mascara y 3 piezas sueltas. Tras la 2.a: muerto y `HeadshotsDelJugador` subio en 2.
- `P R17_11c` (bug del escudo): bala de frente a la cabeza del escudero. `HeadshotsDelJugador` no cambia, `Bloqueados` sube en 1 y no se ve cartel.
- `T ronda17_pesado_mascara` / `ronda17_pesado_expuesto`: primer plano a 4 m antes y despues.

### 2.2 Iterar el enemigo grande (pedido 8) [IMP los puntos a-c, OPC d-f]

**[SUPUESTO]** "enemigo grande" = el pesado, el "gordo" del pedido 5. No es el tanque Goliat.

a. **Timers con dt.** `Update()` pasa a ser `Tick(Time.deltaTime)`, publico. Todos los `Time.time` de la maquina de estados (`desde`, `ultimoVisto`, l.140-150) pasan a ser acumuladores. `InicioDeEstacionar` e `InicioDeFrenesi` quedan como lecturas para los checks (siguen con `Time.time`, solo informativos). Asi la suite headless lo puede simular.

b. **Ver de verdad.** Hoy `ve = objetivo vivo` (l.136). Pasa a ser `ve = objetivo vivo && cerebro.TieneLineaDeTiro(objetivo)` (ya es publico, `AiBrain.Sentidos.cs:37`). Si le pegan sin que vea al tirador, **avanza** con la minigun hacia el ultimo punto conocido (`SetPatrolRoute`) en vez de plantarse.

c. **Telegrafo del Frenesi (los 2 s de Estacionando).** Un laser rojo desde la boca de la minigun hasta el blanco: `LineRenderer` con material compartido, mismo recurso que el laser de `EnemigoFrancotirador.cs:42`. En los ultimos 0,5 s parpadea a 8 Hz. Suena `SfxKind.MinigunCarga`, nuevo y agregado **al final** del enum (con respaldo procedural en `GenericSfx`): sube el tono de 0,6 a 1,2 en 2 s. Es lo que el jugador usa para cubrirse.

d. **Aturdimiento y Furia al romper la mascara** (a partir de 2.1). Aturdimiento de 0,9 s: `cerebro.Quieto=true`, no dispara (corta la rafaga con `Weapon` sin tick) y suena `SfxKind.Wounded` con tono bajo. Despues, Furia hasta que muere: `SegundosDeEstacionamiento` pasa de 2,0 a **1,2 s**, `SegundosSinVerParaRelajarse` de 3 a **6 s** y `FactorDeVelocidad` de 0,72 a **0,85**. No cambia ni la cadencia ni los cohetes. El v1 proponia x1,25 de cadencia: con cohetes de 5 m de radio seria injusto.

e. **[OPC] Pasos con peso.** `CameraRig.KickDirectional(Vector3.down, 0.04 x (1 - d/14))` si el jugador esta a menos de 14 m, como mucho 1 cada 0,45 s **entre todos los pesados** (un reloj estatico). El tope de 0,45 de CameraRig evita que se sumen.

f. **[OPC] Muerte propia.** Clip elegido por direccion (2.9), sacudida de 0,18, `SfxKind.PasoPesado` con tono 0,4, una nube de polvo de 1,5 m al tocar el piso (~0,7 s) y la minigun que cae como fragmento.

**Balance (valores iniciales):** vida x12 sin cambios (1200). TTK al cuerpo con fusil: unos 46 tiros. Por la cabeza: 2 tiros. Hace falta tener eso en cuenta: el pesado pasa a ser un enemigo de "punteria bajo presion" (2 tiros precisos, con el telegrafo como ventana) en vez de una esponja de balas. Si en la prueba se siente facil, la palanca, que no se implementa de entrada, es que el 2.o headshot solo mate cuando el pesado **no** esta en Frenesi: obliga a esperar la recarga o el estacionamiento.

**Aceptacion:**
- `H24.8a`: pesado headless con un blanco detras de una pared (sin linea). 10 s de `Tick(0.05)`: nunca entra en `Frenesi`, y `Quieto==false` al final.
- `H24.8b`: con linea de tiro, `Estacionando` dura 2,0 +- 0,05 s y luego `Frenesi`. Tras `CabezaExpuesta`, el `Estacionando` siguiente dura 1,2 +- 0,05 s.
- `P R17_8c`: el laser esta activo exactamente mientras dura `Estacionando` (LineRenderer.enabled igual a Estado).
- `T ronda17_pesado_telegrafo`: el laser hacia un aliado, desde atras y en diagonal.

### 2.3 Reaccion al dano y venir a atacar (pedidos 2 y 15) [IMP]

**[SUPUESTO]** 15 = "la victima de un golpe responde siempre". 2 = "despues de que el jugador ataca (dispara o mata), los enemigos de la zona vienen a atacar". Se resuelven con **un** componente y dos reglas. Lectura alternativa del 2, que no se implementa: "el enemigo que dispara despues avanza". Se puede consultar.

**Componente nuevo: `Operacion/ReaccionAlDano.cs` (clase estatica, del ciclo de vida de la Operacion).**
- Se suscribe a `DamageTakenEvent` y `EntityDiedEvent` desde `OperacionDirector` (OnEnable/OnDisable). **No corre** en Mision ni en el Tutorial: el muneco del tutorial no tiene que reaccionar. Tiene `Reiniciar()` en `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` (patron de la R16).
- `Tick(dt)` se llama desde `OperacionDirector.Update`, al lado de `CazaDeEnemigos.Tick(dt)` (`OperacionDirector.cs:446`) y con la misma condicion de fase. Todo va con dt: la suite headless lo simula.
- **Cola con retraso:** cada reaccion se encola con un retraso deterministico de `0.25 + (Id % 8) x 0.05` s (0,25-0,6 s), para que no reaccionen 20 en el mismo cuadro y se lea humano. Se deduplica por victima durante 1,0 s (la escopeta genera 7 eventos por tiro). Tope global de **16 reacciones por segundo**: con mas de 100 enemigos, el peor caso queda acotado.
- **Regla V (victima viva)**, si `victima.Team==Enemy` y el atacante es del bando jugador:
  - `Pasivo` (soga del heli): no se hace nada.
  - `PuestoFijoPorRol` (francotirador, emboscada, pesado): solo `Brain.ReaccionarAGolpe(atacante)`, que fija el blanco y lo encara. No libera Quieto.
  - El resto: `ReaccionarAGolpe` + `Quieto=false` + `Atrincherado=false` + `CazaDeEnemigos.Marcar(new[]{v})`.
  - Si el atacante va **montado** (`Brain.MontadoEnVehiculo`): `IssueAttackVehicleOrder(vehiculo)` (ya existe, `AiBrain.Vehiculos.cs:42`). Esta es la parte del pedido 14 que pide "atacar al tanque cuando el tanque ataca".
- **Regla T (testigos de una baja)**, con `EntityDiedEvent` de un enemigo: los enemigos vivos y activos a <= **20 m** del caido, que tengan linea de vista **al caido** (un `Physics.Linecast` por candidato, como mucho 6 por baja), pasan a cazadores. Su primer rumbo es la posicion del tirador con +-3 m de ruido. Si el tirador esta a <= 48 m (ya se lo escucha), se agrega el grupo de 16 m del caido. Esto cierra el hueco de "headshot silencioso de francotirador": el que **ve** caer a un companero reacciona. El que no ve nada, no: el sigilo sigue siendo posible.
- **Nuevo en `AiBrain`** (`AiBrain.Tactica.cs`): `public bool PuestoFijoPorRol;` (lo ponen `EnemigoFrancotirador`, `EmboscadaDeCohetes` y `EnemigoPesado` en su `Start`) y `public void ReaccionarAGolpe(Soldier atacante)`, que hace lo mismo que el bloque `evt.TargetId == self.Id` de `OnAnyDamage`, pero **tambien en Chase/Attack**: cambia de blanco si el actual no tiene linea de tiro y el atacante si.
- **Persecucion:** a los enemigos **cazadores**, `tiempoMaximoSinVisibilidad` les sube de 5 a **9 s** (un campo `SegundosSinVerParaSoltar` que se lee desde CazaDeEnemigos). A los otros no se les cambia.
- **Feedback:** el primero que reacciona en un grupo dispara `SfxKind.EnemySpotted` 3D en su posicion (con un enfriamiento de 4 s por grupo de 16 m) y prende el "!" de `EnemyAlertIndicatorView` durante 1,5 s.

**Riesgos:** (a) **Regresion de dificultad.** Mas enemigos vienen a la vez. Hay que medir las tandas del hackeo (#273 las bajo a la mitad) y bajar `RefuerzosDelCentro` si la derrota sube. (b) **No pisar los checks del atrincherado** (WP4 #085, `Wp4.cs:137`): solo se suelta el atrincherado que **recibio dano**, no el que lo escucho. (c) `OnAnyDamage` sigue suscripto por cerebro (mas de 100 suscriptores): no se agrega otro por cerebro.

**Aceptacion:**
- `H24.15a`: guardia Quieto (sin rol), dano del bando jugador. A los 0,6 s simulados: `Quieto==false`, blanco = atacante, `CazaDeEnemigos.EsCazador`.
- `H24.15b`: francotirador con `PuestoFijoPorRol`. Despues del golpe: blanco = atacante y `Quieto==true`.
- `H24.15c`: 3 enemigos a 10 m del caido, 1 con una pared en medio. Headshot desde 70 m: 2 cazadores y 1 no.
- `H24.15d`: 40 eventos de escopeta en el mismo tick: <= 16 reacciones aplicadas y ningun error.
- `P R17_2e` (integracion): `Arrancar(1)`, matar desde 60 m a un guardia del cuartel. En 6 s, al menos 1 enemigo del grupo esta en Chase o con rumbo hacia la escuadra.
- `T ronda17_reaccion`: vista tactica con los "!" prendidos despues de un disparo.

### 2.4 Contraste de los enemigos (pedido 1) [IMP]

**Medir antes de tocar.** Sin una metrica, "mas contraste" no se puede aceptar.
- Herramienta nueva `Core/MedidorDeContraste.cs`, que usan las capturas: renderiza la camara a un RenderTexture dos veces, con el enemigo y con sus renderers apagados. La mascara es `|A-B| > 0,02`. Contraste de Weber = `|L(A) - L(B)| / max(L(B), 0,02)` dentro de la mascara, con luminancia Rec.709. Tambien reporta el **tono medio** de la mascara.
- Escenarios fijos (Operacion): patio del cuartel de dia a pleno sol, sombra de la muralla, interior del centro de datos, y la calle de la ciudad. Distancias de 10 y 30 m. Se corre con el material **actual** y queda la linea base en `Docs/RONDA_17/contraste_antes.txt`.

**Cambios, todos en material compartido (costo de rendimiento cero):**
1. `SoldierLook.MaterialEnemigoVisible`: la emision pasa de (0.30,0.06,0.04) a **(0.42,0.08,0.05)**. El mapa de emision sigue siendo el trimsheet, para que el dibujo se mantenga (el bug #194 rompio esto con un brillo plano). Tinte base de (1,0.78,0.7) a **(1,0.72,0.62)** (mas saturado).
2. **Insignia del bando:** dos cubos chicos emisivos rojos (1.0,0.12,0.06)x1.4: un brazal en `LeftUpperArm` (0,08 x 0,10 x 0,10) y una franja en `Head` (0,22 x 0,03 x 0,18, sobre el casco). Material **unico estatico**, `ShadowCastingMode.Off`, sin collider y en layer 2. Se arman en `SoldierLook.Aplicar` si `def.Enemigo` y no es perro. Son independientes del UV, asi que no hay riesgo de que se ilumine una celda equivocada del trimsheet. Costo: 2 renderers por enemigo. El SRP Batcher los agrupa, porque comparten material.
3. No se usa contorno, ni rim con shader propio, ni bloom.

**Riesgos:** con demasiada emision de dia se ve una silueta plana: la emision tiene tope de 0,5 por canal. Los aliados no se tocan: la separacion de tono (rojo contra azul) es la que manda.

**Aceptacion:**
- `P R17_1a`: Weber despues >= 1,4 x antes en los 4 escenarios a 30 m, y >= 0,5 en valor absoluto en la sombra y en el interior. Tono de la mascara enemiga en [340°, 25°] y de la aliada en [190°, 250°].
- `P R17_1b`: cada enemigo vivo (no perro) tiene 2 insignias, y todas comparten el mismo `sharedMaterial`.
- `T ronda17_contraste_{dia,sombra,interior,calle}`: ANTES y DESPUES con la misma camara.

### 2.5 Contraste del escudero (pedido 11) [IMP]

- `SoldierLook.MaterialEscudero`: uniforme de grafito 0.16 a **gris acero claro (0.38,0.40,0.45)**. Es la silueta clara contra el asfalto oscuro, y deja que el rojo de la insignia se lea como bando. Casco plata sin cambios, emision de (0.10,0.10,0.12) a **(0.16,0.07,0.07)**. Lleva la insignia de 2.4.
- `EnemigoEscudero.ConstruirEscudo`: el marco pasa a **rojo emisivo** (0.55,0.06,0.04) con emision (0.9,0.1,0.05). La franja amarilla y la placa de acero claro se mantienen. La ventana cambia de naranja a rojo anaranjado (1,0.35,0.1)x0.8. Los 4 materiales pasan a ser **estaticos compartidos** (se crean una vez), y el destello de la ventana (l.169-170) va por `MaterialPropertyBlock` sobre el renderer `Ventana`. `LiberarMaterialesAlDestruirse` deja de liberarlos: son compartidos, no se destruyen por instancia.
- **[OPC] Punto debil en la espalda:** una mochila con una luz ambar intermitente a 1 Hz (cubo emisivo en `Spine`). Le dice al jugador que lo flanquee sin texto.
- Corrige el bug del cartel de headshot en el escudo (2.1, paso 1): `BloqueaGolpeDe = id => id >= 0 && ActorRegistry.FindById(id) is Soldier a && ProtegeContra(a.transform.position)`.

**Aceptacion:** `P R17_11a`: Weber del escudero en la calle a 30 m >= 1,4 x antes. `P R17_11b`: con 5 escuderos vivos, los materiales del escudo son 4 en total (no 20). `P R17_11c` (2.1). `T ronda17_escudero_{frente,espalda}`.

### 2.6 PC bloqueada hasta limpiar (pedido 6) [IMP]

**[SUPUESTO] "la PC"** = la COMPUTADORA del centro de datos (la unica `OperacionTerminal`, con baliza "COMPUTADORA"). **[DECIDE JOAQUIN]** la terminal de las celdas de la mansion: el #333 pidio lo contrario. Queda con una constante `BloquearCeldasHastaLimpiar = false`. Activarla es trivial, porque `faltan` ya existe (`Mansion.cs:250`).

**Interpretacion recomendada (A): "limpiar para empezar".** La PC no se puede **empezar** a hackear con enemigos vivos en la sala. Una vez que se suelta, **no se vuelve a bloquear nunca** (latch). Asi las oleadas que salen con el hackeo no pueden trabar el juego. Como la sala hoy esta vacia antes del hackeo, el bloqueo necesita una **guarnicion**, porque si no es un no-op.

**Variante (B), no recomendada como default:** "limpiar para terminar". A 100 % la salida norte no se abre hasta que mueran las oleadas ya activas. Las reservas que todavia no salieron se cancelan al llegar a 100 %, porque si no, el modo dios (hackeo de 3 s) nunca terminaria. Cambia mucho el ritmo de la fase.

**Diseno A:**
- `Operacion/OperacionTerminal.cs`:
  - `public System.Func<int> EnemigosQueBloquean;` (null = sin bloqueo) y `bool desbloqueada;` (latch).
  - `public int Restantes { get; private set; }`, que se recalcula cada **0,5 s** en `Update` (no en cada consulta).
  - `public bool Bloqueada => EnemigosQueBloquean != null && !desbloqueada && !Completo && Progreso01 <= 0f && Operador == null;`. Al primer `Restantes == 0` se pone `desbloqueada = true`, con destello verde (`Pintar(Verde)` 0,6 s y despues Rojo), `SfxKind.HoloOn` y el aviso "SALA ASEGURADA · YA PODES HACKEAR".
  - **No** se usa `Habilitado`: deja el cartel invisible. `UpdateHacker` (l.247-261): con `Bloqueada` y el jugador al lado, el cartel en rojo dice "COMPUTADORA BLOQUEADA · QUEDAN {N} ENEMIGOS EN LA SALA", y `[E]` hace sonar `SfxKind.EmptyClick` con `AvisoCentral`. `EnviarA` devuelve el motivo "BLOQUEADA: QUEDAN {N}". La luz de la pantalla late en rojo a 1 Hz (`PulsoDeBloqueo`, con el MPB que ya existe).
  - `Avanzar()` **no** mira el bloqueo: los checks y la restauracion lo usan directo.
- `Operacion/OperacionDirector.Centro.cs`:
  - `ArmarGuarnicionDelCentro()` al entrar en la fase (en `EntrarFase(CentroDeDatos)`, `OperacionDirector.cs:328`). Clona la plantilla de reservas (mismo camino que `ReforzarOleadasDelCentro`, l.67): **4/5/6 guardias** segun la dificultad (FACIL/MEDIO/DIFICIL), en puntos fijos entre las filas de gabinetes (a disenar sobre el plano x -25..25, z -79..-41, lejos de las flechas del pasillo frio). La composicion en MEDIO: 3 comunes, 1 escudero y 1 comun en la mesa del operador. Se atrincheran con `Atrincherar.Aplicar(guarnicion, entradaSur)`. Los nombres son `CD_Guarnicion_N`, idempotentes.
  - `computadora.EnemigosQueBloquean = ContarGuarnicionYSala;`: cuenta **por identidad** la guarnicion viva, y **ademas** cualquier enemigo vivo, activo y no montado dentro de `Bounds(x -27..27, y -1..9, z -81..-39)`. **No cuenta** reservas inactivas, vehiculos, Pasivo ni a los que van montados. Si la guarnicion sale de la sala persiguiendo a la escuadra, **sigue contando** (por eso se cuenta por identidad).
  - Mientras `Bloqueada`: `hud.Objetivo(.., "Limpia la sala de servidores · quedan N")`. La baliza "COMPUTADORA" **recien aparece al soltarse**; antes la baliza marca la entrada sur.
- **Anti-bloqueo (las 6 reglas):**
  1. Latch: se suelta una vez y no se vuelve a bloquear.
  2. Solo se mira antes del primer progreso (`Progreso01 <= 0` y sin Operador).
  3. **Revelar:** con `Restantes <= 3` se muestran los rombos rojos a traves de las paredes. Para eso se generaliza `RevelarEnemigosDeLaMansion` a `RevelarEnemigos(IEnumerable<Soldier>)`.
  4. **Rescate:** si un enemigo que cuenta no se movio mas de 0,5 m en 20 s y no tiene camino por NavMesh a la PC, se lo marca como cazador y se lo mueve al punto de NavMesh mas cercano dentro de la sala. Es el patron de `RescatarEnemigosAtascados` (`Mansion.cs:366`).
  5. **Ultimo recurso:** si `Restantes <= 2` y no bajo en 60 s, se suelta con la radio diciendo "Sala asegurada, el resto huyo" y se deja `GameLog.Line("[R17] desbloqueo por salvaguarda ...")` para QA.
  6. **Atajos:** `SaltarA` / F10 / `PartidaGuardada.ModoPrueba` dejan `desbloqueada=true`, salvo que el check ponga `OperacionDirector.ProbarBloqueoDelCentro=true` (patron de `SaltoDePrueba`). Asi los 19 checks que enganchan la PC no se rompen. Al restaurar con `progreso > 0`, queda suelta.
- **Ritmo:** la guarnicion es la "entrada" del objetivo 3: limpiar 5 en una sala con coberturas (gabinetes con collider), y despues defender 30 s. Es el mejor lugar para que aparezca el escudero (pedido 11) y para que se lea la decoracion (pedidos 3/10).

**Aceptacion:**
- `H24.6a` (pura, terminal con un `Func` falso): `Restantes=3` => `Bloqueada`. 0 => suelta y latch. Si despues vuelve a 3 => sigue suelta. Con `Progreso01 > 0` nunca se bloquea.
- `P R17_6b`: entrar a la fase por el camino real (Puestos -> Centro, con `ProbarBloqueoDelCentro`). 5 guardias, `[E]` (`PruebaToqueE`) no engancha, el cartel contiene "QUEDAN 5". Se matan con `Health.Ejecutar`: queda suelta y `[E]` engancha.
- `P R17_6c`: guardia atascado (se lo congela con `Motor` apagado fuera del NavMesh) => rescate en <= 22 s, o salvaguarda en <= 62 s.
- `P R17_6d`: los 19 checks de PC existentes siguen en OK (regresion).
- `T ronda17_pc_bloqueada` / `ronda17_pc_libre`.

### 2.7 Decoracion de servidores, interior y exterior (pedidos 3 y 10) [IMP 3a-d y 10a-c; OPC el resto]

Va en un parche de runtime nuevo, **`Presentation/AjustesDeNivelR17.cs`**, con el patron de `AjustesDeNivelOpus` (`RuntimeInitializeOnLoadMethod(AfterSceneLoad)` + un vigilante cada 2 s + latches `Listo*`, idempotente por nombre de grupo). No se reconstruye la escena. Todo el arte va **sin collider**, salvo lo que se marca como cobertura, y con materiales compartidos. Al terminar, `StaticBatchingUtility.Combine(grupo)` por grupo, que funciona en runtime con MeshRenderer.

**Interior (`R17_Servidores_Vida`, como hijo de `9_Servidores`):**
- 3a. **LEDs vivos:** se buscan los renderers "Led" y "Torre_Led" de `9_Servidores` y se les reparten **4 materiales de fase** (clones de `LedVerdeTenue/LedCianTenue/LedAmbarTenue`). Un unico componente `ParpadeoDeLeds` modula la emision de esos 4 materiales (on/off con patrones distintos, de 2 a 7 Hz). Son 4 `SetColor` por cuadro, sin importar cuantos LEDs haya.
- 3b. **Piso:** 6 mazos de cable en el piso, entre los gabinetes y el pasillo, con cinta amarilla encima (cubos de 0,06 de alto, sin collider), 2 carros de servidores y 3 cajas de equipos contra las paredes (estos **con collider** + `NavMeshObstacle carving` + `Coberturas.Registrar()`, como los bancos de `AjustesDeNivelOpus.cs:79-85`). Tambien un gabinete abierto con dos unidades afuera, en el suelo.
- 3c. **Senaletica:** "SALA DE SERVIDORES · ACCESO RESTRINGIDO" sobre la puerta sur por dentro, y etiquetas A1..D8 en las cabeceras de las filas. Se usa `OperacionTextoMundo` (ya lo hace con MPB, legible en la build).
- 3d. **Reaccion al hackeo:** cuando `computadora.Operador != null` por primera vez, 2 balizas rojas giratorias (una sola `Light` spot, sin sombras, y una malla emisiva) y los paneles del techo bajan a 0,6. Al completar el hackeo, se apagan. Es lo que mas "vida" da al cuarto, porque esta atado al juego.
- [OPC] 3e. Vapor frio que sale de 4 rejillas del piso (`ParticleSystem`, 10 particulas cada una, alfa 0,08, sin colision).
- [OPC] 3f. Zumbido de los servidores: `SfxKind.ZumbidoServidores` (nuevo, al final del enum, con respaldo procedural de ruido marron). Un loop 3D en el centro de la sala, de 4 a 30 m. Hay que verificar si `AudioDirector` soporta loops; si no, se usa un `AudioSource` propio con `outputAudioMixerGroup` de ambiente.

**Exterior (`R17_Servidores_Fachada`, como hijo de `3_CentroDeDatos`), a la altura del suelo:**
- 10a. **Condensadores de aire** (6, de 1,8 x 1,4 x 1 m) contra los muros este y oeste, **fuera** de las puertas laterales (z -62..-56) y de los patios de aparicion (patio oeste x -60..-52 y este x 52..60, z -88..-44, `Centro.cs:61-62`): van pegados al muro, en x ±26,9 a ±28,5. Con collider (son cobertura) + carving.
- 10b. **Conductos y bandejas** en las fachadas, de los condensadores al techo (sin collider). Cartel "CENTRO DE DATOS · NODO 3" sobre la entrada sur, con un reflector (1 `Light` spot, `shadows=None`).
- 10c. **Franjas de advertencia** en el piso frente a las 3 puertas y un **generador con tanque de gasoil** en la esquina SE (x 20..24, z -86..-83; hay que verificar que no pise la esquina de aparicion SE x 44..56).
- [OPC] 10d. Vallas bajas que no toquen la ruta de las camionetas (al norte, z -40..+30) ni la del tanque (`AjustesDeNivelOpus.BloqueosDeLaRuta` sirve de verificacion).

**Riesgos:** los colliders nuevos pueden cortar el NavMesh o tapar las entradas o la aparicion. Se mitiga con el carving y con un check de caminos. Rendimiento: unos 60 objetos combinados por material, y +2 luces sin sombra.

**Aceptacion:**
- `P R17_3a`: `ParpadeoDeLeds.MaterialesDeFase == 4` y al menos 80 % de los LEDs de `9_Servidores` usan uno de ellos.
- `P R17_10a`: `NavMesh.CalculatePath` **completo** desde cada uno de los 5 tipos de aparicion de refuerzos y desde la entrada sur hasta la PC. `AjustesDeNivelOpus.BloqueosDeLaRuta(d)` no devuelve nada nuevo. Los checks 501a-g (flotantes, solapes, colisiones) siguen en 0 sobre los grupos R17.
- `P R17_3d`: con `Operador` puesto, las 2 balizas encendidas; con el hackeo completo, apagadas.
- `P R17_perf`: `PerfFrames` en la sala con el hackeo en curso. La mediana de antes y despues no cae mas de 5 %.
- `T ronda17_servidores_{pasillo,fila,hackeo}` y `ronda17_fachada_{sur,este,oeste}`: ANTES y DESPUES.

### 2.8 Particulas de la fuente (pedido 4) [IMP a-b, OPC c-d]

En `Presentation/FuentesDeAgua.cs`, metodo nuevo `ArmarParticulas()`, que se llama al final de `Aplicar()`. Es idempotente: un hijo `R17_Particulas` por cada `Fuente_Chorro`.
- a. **Chorro:** `ParticleSystem` en la boca del chorro, `startSpeed` 3,5, gravedad 1, vida 0,9 s, 40/s, `maxParticles` 60, tamano 0,06-0,12, color (0.75,0.88,1,0.55).
- b. **Salpicadura:** en la superficie del cuenco (el `bounds.max.y` de `Fuente_Agua` que ya calcula `CorregirSolapes`), con forma de anillo de radio 0,4, 25/s, `maxParticles` 40, vida 0,5 s.
- c. **Niebla:** 4/s, tamano 0,8-1,2, alfa 0,06, vida 2,5 s, `maxParticles` 12.
- d. **Sonido:** `SfxKind.AguaDeFuente` (nuevo, ruido blanco filtrado como respaldo), loop 3D de 2 a 14 m.
- En todos: material `ParticleMaterialFactory.CreateTransparent` **compartido**, `cullingMode = Automatic`, sin colision, sin luces y `simulationSpace = World`. El `Fuente_Gota` fijo se apaga si hay particulas en su chorro, porque si no, se ve doble.

**Aceptacion:** `P R17_4a`: cada `Fuente_Chorro` tiene exactamente 1 `R17_Particulas` con 2-3 sistemas, y la suma de `maxParticles` por fuente es <= 120. Reaplicar no duplica nada. `T ronda17_fuente` (primer plano a 5 m) y `ronda17_fuente_lejos` (25 m: no tiene que leerse como una mancha).

### 2.9 Animacion de baja de los enemigos (pedido 12) [IMP a-b, OPC c-e]

- a. **Clip por direccion y causa** (`Presentation/CubeFxReactor.cs:254`): se reemplaza el `Random.Range` por `ElegirMuerte(soldier)`. Se toma la posicion del atacante (`Health.LastAttackerId`; si va montado, la del vehiculo) y el angulo respecto del `forward` de la victima:
  - frente (|a| < 60°) => 0, o 3 si fue headshot, o 5 si fue headshot y estaba agachado;
  - espalda (|a| > 120°) => 1, o 4 si fue headshot;
  - derecha => 2;
  - izquierda => 0. No hay clip de izquierda: [OPC] agregar "death from left" al pack.
  - Sin atacante, al azar como hoy.
  - "Fue headshot" se guarda en `Health`, con una propiedad `MurioPorLaCabeza` que se pone cuando el instakill ejecuta (l.185-187).
- b. **Retiro sin "pop"** (`RetiroDeEnemigosCaidos.Retirar`): en vez de encoger a 0,02, el cuerpo **se hunde** 0,6 m en 1,4 s con un poco de polvo (`SpriteFx.Lanzar("smoke_02", ..., alfa 0,35)`). Despues se apagan los renderers, igual que hoy. `CaidaDeCuerpos` no interfiere: solo corrige huecos **por encima** del piso (`HuecoMinimo` 0,6).
- [OPC] c. **Polvo al caer** a los 0,6 s del clip (pesado: x2,5 + sacudida, ver 2.2 f).
- [OPC] d. **Arma que cae** como fragmento (`Fragmentador.LayerSinRayos`), 1 por baja, que se destruye a los 10 s. Tope de 12 a la vez.
- [OPC] e. Pesado y escudero: el escudero ya suelta el escudo (`EnemigoEscudero.SoltarEscudo`); se respeta.

**Aceptacion:** `H24.12a` (pura): `ElegirMuerte` con 8 direcciones x headshot si/no da la tabla esperada. `P R17_12b`: un tiro por la espalda a un comun => `MuerteVariante == 1`, y headshot de frente => 3. `P R17_12c`: a los 14 + 1,4 s el cuerpo quedo 0,6 m mas abajo y con los renderers apagados, y `localScale` sin cambios. `T ronda17_muerte_{frente,espalda,cabeza}` (3 tomas a los 0,8 s del impacto).

### 2.10 Disparador para empezar a atacar al tanque (pedido 14) [IMP A, OPC B]

**[SUPUESTO] Interpretacion A (recomendada): la pelea contra el BLINDADO GOLIAT empieza cuando el jugador decide entrar al patio, no sola.** Hoy la toma arranca 2,5 s despues de que cae el muro, aunque el jugador siga adentro (`Jefe.cs:212-213`).
- `OperacionDirector.Jefe.cs` / `TickAcercamiento`: se quita `muroCaido` como disparador. La toma arranca con lo **primero** que pase de esto:
  1. el jugador cruza el umbral `ZParaLaToma` (-30);
  2. el jugador le pega al tanque (`DamageTakenEvent` / `Vehicle` con el atacante del bando jugador);
  3. **[salvaguarda]** pasaron 45 s desde que cayo el muro.
- En el mismo instante se pone `FuegoHabilitado=true` (un solo latch para las dos cosas).
- **Umbral visible:** franja de piso roja y negra a lo ancho de la salida del patio en z = -30 (`R17_UmbralDelPatio`, sin collider) y un cartel "ZONA DEL BLINDADO". Desde que cae el muro, la torreta del tanque sigue al jugador (`aiCanon` apunta pero esta `Silenciada`): es la amenaza antes de la pelea.
- **Contrato nuevo del #291:** hay que actualizar el check de `Bugs231a255.cs:204-210`: la toma ya no arranca sola a los 2,5 s, sino al cruzar el umbral, o a los 45 s por la salvaguarda. **[DECIDE JOAQUIN]** confirmar que acepta deshacer el #243/#291.

**Interpretacion B (complemento, casi gratis con 2.3):** los enemigos atacan al tanque del jugador. Se cubre con la regla "si el atacante va montado, `IssueAttackVehicleOrder`" (2.3 V). [OPC] En `TickTrayecto` (Carrera), cada sector de la ruta con enemigos de a pie lanza `IssueAttackVehicleOrder(tanque)` a como mucho 4 de ellos cuando el tanque entra a 45 m, una vez por sector.

**Aceptacion:** `P R17_14a`: muro caido + jugador quieto en la sala durante 10 s => `TomaDelJefeActiva == false`. Mover al jugador a z = -29 => la toma arranca en <= 1 cuadro de logica y `FuegoHabilitado`. `P R17_14b`: muro caido + 45 s quieto => la toma arranca (salvaguarda). `P R17_14c` (B): el jugador en el tanque le dispara a un enemigo de a pie => ese enemigo tiene `VehiculoOrdenado == tanque` en <= 1 s. `T ronda17_umbral_patio`.

### 2.11 Feedback al entrar al tanque: circulos en el piso (pedido 13) [IMP]

En `Operacion/AnilloDeSubida.cs`. Sigue siendo un singleton y mantiene `Mostrar/Ocultar/EstaActivo/Instancia/EscalaActual`, que usan los 3 checks que ya existen. Se agrega a la instancia:
- **Aro de uso fijo:** radio exacto = `PlayerInputDriver.RadioDeInteraccion`, una propiedad publica nueva que devuelve el `interactRadius` **serializado de la instancia**, no una constante (la escena puede tener otro valor). Blanco y ambar afuera, **verde** adentro.
- **Aros de llamada:** los 2 que hoy laten, pero que **se cierran hacia el aro de uso** a medida que el jugador se acerca (de 25 m a 4 m: la escala va de 1,6 a 1,0, y la frecuencia sube de 1,5 a 4 Hz). Adentro del radio se apagan y queda solo el verde fijo, con el cartel "[E] SUBIR AL TANQUE".
- Al subir: un destello verde de 0,25 s y `SfxKind.SeatChange` antes de `Ocultar`.
- **La plaza** (`TickCirculoDeBase`) usa el mismo objeto con `Mostrar(plaza, 4.2f)`. Se le pasa `RadioDeUso = RadioDeInteraccionDeBase (5)` con una sobrecarga `Mostrar(objetivo, radio, radioDeUso)`, para que el aro verde tambien marque su radio. No hace falta crear un segundo singleton.
- [OPC] Mismo anillo para cualquier vehiculo propio libre en el radial "ENTRAR AL TANQUE" (fuera de Abordaje).

**Aceptacion:** `P R17_13a`: jugador a 20 m => aros de llamada activos y escala > 1,3. A 3 m => `DentroDelRadio == true`, color verde y aros de llamada apagados. El radio del aro de uso es igual a `RadioDeInteraccion` +-0,05. `P R17_13b`: la plaza en Resistir sigue mostrando su circulo (regresion R14 #18). `T ronda17_anillo_{lejos,borde,dentro}`.

### 2.12 "Nivel 1 terminado" (pedido 7)

**[SUPUESTO] + [DECIDE JOAQUIN].** Hay tres lecturas: (a) `SC_Gameplay`, que el menu llama NIVEL 1. Es la opcion recomendada, porque la R13 (T18) ya marco que es alcanzable y tiene que verse terminado o esconderse. (b) La Operacion entera, como "el primer nivel de verdad". (c) Una nota de estado ("ya esta terminado"), sin tarea. El plan toma (a) con una **definicion de terminado**. Si Joaquin dice (b), la misma lista se corre sobre la Operacion.

**Definicion de terminado (todo medible):**
1. Corrida completa automatica (`AutoDemoRunner` o un `PruebaEnPlayer -nivel1`): menu -> dificultad -> intro -> INFILTRAR -> RESISTIR -> RESCATAR -> ESCAPAR -> victoria -> `PantallaDeResultado` -> menu, **con las 3 dificultades**. 0 excepciones y 0 `LogError` en `Player.log`.
2. Derrota alcanzable y que se puede reintentar (todos caidos con enemigos cerca => derrota en 2 s, `EstadoDePartida.cs:14`) y la pantalla de resultado correcta.
3. Ningun texto "placeholder", "TODO" ni "PROXIMAMENTE" visible: se barren los `Text`/`TMP` activos durante la corrida.
4. Rendimiento: mediana >= 60 fps a 1600x900 en `-spmetrics` (ya carga `SC_Gameplay`).
5. HUD y objetivo siempre con texto, y musica y voz por fase. La corrida registra los cambios de fase.
6. Las 3 tomas de nivel 1 que ya existen (`n1_despues_*`) se repiten sin regresiones, mas 4 nuevas (una por fase).
- **Limite de tiempo:** 1 hora de auditoria que produce una lista. Solo se arreglan los bloqueos (excepciones, fases que no avanzan). El resto queda para la R18.

### 2.13 Tutorial simple (pedido 9) [IMP]

**[SUPUESTO]** Es el tutorial de `SC_Tutorial`, en version corta y por defecto desde el menu. **[OPC]** Pistas contextuales dentro de la Operacion la primera vez (no se planifican ahora).
- `Tutorial/TutorialManager.cs`: `public static bool ModoSimple = true;` y `static readonly HashSet<string> IdsSimples`. Despues de `DefinirPasos()` (l.164): `if (ModoSimple) pasos.RemoveAll(p => !IdsSimples.Contains(p.Id));`.
- **Lista propuesta (11):** `camara, wasd, disparar, mira, cambiar, seguir, rts, fps, entrar_tanque, mira_tanque, victoria`. Hay que validar que cada paso que queda no dependa del estado de un paso que se quito (`AlEntrar` de `rts`, que necesita a los 2 aliados; `entrar_tanque`, que necesita `tanquePosInicio`). Si alguno depende, se mueve esa inicializacion a `Start`.
- [OPC] El texto de `disparar` suma "APUNTA A LA CABEZA: HEADSHOT", que conecta con la mascara del pesado.
- **Progreso guardado:** `ClaveProgreso` lleva el sufijo `_simple` en modo simple (l.1657/1794), y el guardado pasa a ser **por Id** en vez de por indice.
- **Menu** (`MainMenuController.OnTutorialClicked`, l.340): `TUTORIAL` = simple. Se agrega un boton chico "TUTORIAL COMPLETO" debajo, o una tecla en la pantalla del tutorial.
- **Aceptacion:** `P R17_9a`: el autoplayer completa el modo simple con `pasos.Count == 11`, ningun paso trabado (`sinProgreso` < umbral) y el tiempo del autoplayer <= 150 s. `P R17_9b`: el modo completo sigue en 37 pasos y su corrida del autoplayer sigue en OK (regresion de `HeadlessTestRunner.Fase22` y `TutorialAutoPlayerMenu`). `T ronda17_tutorial_{inicio,final}`.

---

## 3. Interacciones entre items (y como se resuelven)

| Combinacion | Riesgo | Resolucion |
|---|---|---|
| Pesado + headshot + escudo | Un headshot bloqueado gasta la mascara o anuncia HEADSHOT | Orden fijo en Projectile: `BloqueaGolpeDe` -> capa de cabeza -> instakill. En Health, la capa va despues de `FiltroDeDano`. |
| Pesado + ReaccionAlDano + Quieto | ReaccionAlDano suelta Quieto y la maquina del pesado lo vuelve a poner en cada cuadro (l.148) | Pesado = `PuestoFijoPorRol`: solo se cambia el blanco. Su propia maquina (con la vista corregida, 2.2 b) decide si avanza. |
| Mascara rota + Furia + Frenesi con cohetes | Furia + cadencia x1,25 = cohetes a repeticion | La Furia solo acorta el estacionamiento y sube la velocidad. La cadencia no se toca. |
| Headshot instantaneo + reaccion | El muerto no avisa a nadie | Regla T (testigos con linea de vista al caido). |
| PC bloqueada + oleadas del hackeo | Si se cuentan las reservas, el juego se traba (las reservas salen recien al hackear) | Diseno A: se cuenta la guarnicion por identidad y la sala por volumen, sin reservas inactivas, con latch y sin re-bloqueo. La variante B cancela las reservas pendientes al llegar a 100 %. |
| PC bloqueada + ReaccionAlDano | La guarnicion herida sale a cazar fuera de la sala y "desaparece" del conteo por volumen | Se cuenta por identidad. Rescate y salvaguarda. |
| PC bloqueada + checks existentes | 19 checks enganchan la PC despues de `SaltarA` | Los atajos dejan la PC suelta y un check nuevo lo activa a proposito. |
| Guarnicion + decoracion | Los carros y las cajas con collider tapan caminos o puntos de la guarnicion | Primero se ubica la decoracion y despues la guarnicion. El check de caminos `R17_10a` va antes de `R17_6b`. |
| Disparador del jefe + check #291 | El contrato cambia | Se actualiza el check (2.10) con la confirmacion de Joaquin. |
| Anillo de subida + circulo de la plaza | Singleton compartido | Sobrecarga `Mostrar(.., radioDeUso)`, sin dos instancias. |
| Contraste + muerte + retiro | Las insignias quedan flotando al hundirse el cuerpo, si no son hijas de los huesos | Son hijas de huesos: bajan con el cuerpo y se apagan con los renderers del soldado (`GetComponentsInChildren<Renderer>`). |
| Contraste + tutorial | El muneco del tutorial cambia de aspecto | Esta bien que cambie (es enemigo). Hay que revisar la toma del tutorial. |
| ReaccionAlDano + Tutorial/Mision | Reacciones fuera de la Operacion | Solo se suscribe desde `OperacionDirector`. |

---

## 4. Orden de ejecucion (una sesion, un desarrollador)

Cada fase cierra con: compilacion limpia (`RequestScriptCompilation(CleanBuildCache)` y esperar `completed`: no confiar en el "up_to_date", ver la memoria del proyecto), los checks de la fase y la suite de regresion de su area.

| Fase | Contenido | Tiempo aprox. | Dependencias |
|---|---|---|---|
| **F0 Preparar** | Consultar a Joaquin las 4 decisiones (seccion 6). Tomas ANTES del grupo `ronda17` con la build actual. `MedidorDeContraste` y la linea base. | 45 min | - |
| **F1 Combate base [IMP]** | `Health.CapasDeCabeza/BloqueaGolpeDe/MurioPorLaCabeza`, el orden en Projectile, el cartel de mascara y la correccion del escudo. `RunPhase24` (H24.5a). | 60 min | - |
| **F2 Pesado [IMP a-d]** | `Tick(dt)`, la vista, el telegrafo, la mascara visual, el aturdimiento y la Furia. Checks 5b, 8a-c. | 75 min | F1 |
| **F3 Reaccion [IMP]** | `ReaccionAlDano`, `PuestoFijoPorRol`, `ReaccionarAGolpe`, los testigos y el tope. Checks 15a-d y 2e. Medir la tasa de derrota en `Arrancar(3)` con 3 corridas. | 75 min | F2 (el pesado ya tiene rol) |
| **F4 PC y guarnicion [IMP]** | El bloqueo en `OperacionTerminal`, la guarnicion, las 6 reglas anti-bloqueo y el HUD. Checks 6a-d. | 75 min | F3, F5 (la decoracion con collider se ubica antes) |
| **F5 Servidores [IMP 3a-d, 10a-c]** | `AjustesDeNivelR17`, el check de caminos y el de rendimiento. | 60 min | va antes de que se fije la guarnicion de F4 |
| **F6 Feedback [IMP]** | Clip de muerte por direccion y retiro hundido (12a-b), anillo de subida (13) y disparador del jefe (14A). | 60 min | F1 (MurioPorLaCabeza) |
| **F7 Contraste [IMP]** | Material enemigo, insignias y escudero (con materiales compartidos). Medicion DESPUES. | 45 min | F0 (linea base) |
| **F8 Fuente [IMP a-b]** | Particulas. | 25 min | - |
| **F9 Tutorial simple [IMP]** | Filtro por Id, clave propia y menu. Autoplayer simple y completo. | 40 min | - |
| **F10 Nivel 1 [limite de tiempo]** | La auditoria de la definicion de terminado (2.12). Solo se arreglan bloqueos. | 60 min | - |
| **F11 Cierre** | Suite completa (headless + ChecksBugs065), build, tomas DESPUES, atlas JPG `Docs/RONDA_17/ATLAS_R17.jpg` y `VALIDACION_R17.md`. | 45 min | todo |

Orden real recomendado: F0 -> F1 -> F2 -> F3 -> **F5 -> F4** -> F6 -> F7 -> F8 -> F9 -> F10 -> F11. Si falta tiempo, se cortan en este orden: F10 (pasa a la R18), los OPC de F5, F8 c-d, 2.2 e-f, 2.9 c-e y 2.10 B.

---

## 5. Riesgos transversales

1. **Dificultad:** la reaccion (2.3) + la guarnicion (2.6) + la Furia (2.2) hacen la Operacion mas dura. Se mide con 3 corridas de `Arrancar(3)` y `Arrancar(5)` en MEDIO, contando derrotas y bajas propias. La palanca es `RefuerzosDelCentro` (7 -> 5) y el tamano de la guarnicion.
2. **Rendimiento con mas de 100 enemigos:** ReaccionAlDano con tope de 16 por segundo y como mucho 6 linecasts por baja; insignias = +2 renderers por enemigo con material compartido; LEDs = 4 materiales; particulas <= 120 por fuente con culling. Se toma `PerfFrames` en la mansion (la zona con mas enemigos) antes y despues: no puede caer mas de 5 %.
3. **Suite Edit mode:** todo lo nuevo con logica usa dt (`ReaccionAlDano.Tick`, `EnemigoPesado.Tick`, el reloj del bloqueo de `OperacionTerminal`). En las pruebas headless hay que llamar a `ActorRegistry.Rebarrer()` (no corre `OnEnable`).
4. **Estaticos:** cada clase estatica nueva (`ReaccionAlDano`, el reloj de pasos del pesado, los materiales compartidos) lleva un reset con `SubsystemRegistration` (es la regla de la R16).
5. **Materiales HideAndDontSave:** los que se crean en runtime son estaticos y no se liberan por instancia. No hay que destruir uno compartido desde un `OnDestroy` de instancia (riesgo nuevo en el escudero).
6. **Enum `SfxKind`:** se agrega **solo al final** (`MinigunCarga`, `ZumbidoServidores`, `AguaDeFuente`) y cada uno con respaldo procedural.
7. **Guardado:** el estado del bloqueo y de la mascara no se guarda. Al restaurar se recalcula: el bloqueo queda suelto si hay progreso, y la mascara vuelve a estar entera (el pesado se crea de nuevo).

---

## 6. Decisiones que hay que pedir a Joaquin (antes de F1)

1. **PC bloqueada:** ¿A ("limpiar para empezar", con guarnicion nueva en la sala) o B ("limpiar para terminar")? ¿Tambien la terminal de las celdas, deshaciendo el #333?
2. **Tanque:** ¿"Trigger para empezar a atacar al tanque" es la pelea contra el BLINDADO (A), que ahora empezaria al cruzar el umbral y no sola a los 2,5 s, deshaciendo el #243/#291? ¿O es que los enemigos ataquen al tanque del jugador (B)?
3. **Nivel 1:** ¿`SC_Gameplay` (el NIVEL 1 del menu), la Operacion entera o "ya esta terminado"?
4. **Tutorial simple:** ¿reemplaza al completo como opcion por defecto del menu? ¿Esta bien la lista de 11 pasos?

---

## 7. Resumen de checks y tomas

- Headless (`RunPhase24`): 5a, 6a, 8a, 8b, 12a, 15a, 15b, 15c, 15d.
- Play (`ChecksBugs065.R17_*`): 1a, 1b, 2e, 3a, 3d, 4a, 5b, 6b, 6c, 6d, 8c, 9a, 9b, 10a, 11a, 11b, 11c, 12b, 12c, 13a, 13b, 14a, 14b, 14c, perf.
- Checks existentes que se actualizan a proposito: el #291 (`Bugs231a255.cs:204-210`). Y se revisan: `Wp1.cs:101/476` (comentario de `HeadshotMataAlInstante`) y `Wp4.cs:137` (atrincherado).
- Tomas (`CapturasDeBuild.Ronda17.cs`, grupo `ronda17`, ANTES y DESPUES): pesado_mascara, pesado_expuesto, pesado_telegrafo, reaccion, contraste_{dia,sombra,interior,calle}, escudero_{frente,espalda}, pc_bloqueada, pc_libre, servidores_{pasillo,fila,hackeo}, fachada_{sur,este,oeste}, fuente, fuente_lejos, muerte_{frente,espalda,cabeza}, umbral_patio, anillo_{lejos,borde,dentro}, tutorial_{inicio,final}, y 4 de nivel 1.
- Entregables: `Docs/RONDA_17/VALIDACION_R17.md` (tabla por item: OK/PARCIAL, check, toma), `ATLAS_R17.jpg`, `contraste_antes.txt` / `contraste_despues.txt` y una build limpia.
