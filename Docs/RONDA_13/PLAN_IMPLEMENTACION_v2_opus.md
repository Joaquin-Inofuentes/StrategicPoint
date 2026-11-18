# Ronda 13: plan de implementación v2

**Revisión:** Opus, 2026-11-16. Este documento reemplaza al borrador `PLAN_IMPLEMENTACION.md`.

**Base de partida:**
- HEAD `95b80a95`, con el código limpio. Lo único sucio son registros en `Registros/`.
- La build de esa versión está en `Builds/Windows64` (13/11 20:47) y su paquete en `Builds/StrategicPoint.rar`.

**Fuentes:**
- Las dos listas del dueño (L1 y L2).
- `Docs/RONDA_13/VALIDACION_PARCIAL2.md`, con los criterios del Parcial 2. De ahí salen T21 a T24.
- La lectura del código real, hecha el 16/11.

**Abreviaturas de rutas:**
- `S\` = `Assets\_Project\Scripts\`
- `Ed\` = `S\Editor\`
- Escenas: `SC_Operacion` (binaria) y las aditivas `Cuarteles`, `Ruta` y `Ciudad`, que son YAML y están en `Assets\_Project\Scenes\Operacion\`.

---

## 0. Qué cambia respecto del borrador

| # | El borrador decía | Lo que dice el código | Consecuencia en el plan |
|---|---|---|---|
| 1 | T1: usar `Health.Kill` | No existe `Kill`. Los coeficientes (dificultad, modo dios ×3 y `AjustesDeDano`) se aplican **después** del número que pasa quien pega. | `Health.Ejecutar(...)` nuevo, sin coeficientes. El pesado se resuelve con un contador de 2 golpes, no con `ceil(vida/2)+1`. |
| 2 | T1: el cuchillo hoy pega a cualquiera | Solo pega a `Soldier` enemigos, **sin línea de vista**, así que atraviesa paredes finas. No toca civiles (son del bando Player) ni vehículos. | Si el cuchillo pasa a matar de un golpe, la línea de vista es obligatoria. Falta decidir qué pasa con el escudero de frente. |
| 3 | T2: el cohete quizás no impacta (radio contra altura) | El cohete **sí** choca con la caja del heli. Las causas reales son cuatro: (a) `MultiplicadorVsVehiculo 2.5` es dato muerto (`WeaponHolder.cs:748` no lo pasa); (b) el daño se mide contra el pivote de una caja de 10,5 m, y en la cola da 0; (c) el heli tiene `FactorDeResistencia 0.6`; (d) hay 9/7/5 cohetes. Hoy hacen falta de 22 a 70 cohetes. | Se cambian tres cosas: el multiplicador, el impacto directo y el factor contra explosivos. La fórmula de control está en T2. |
| 4 | T3: el coeficiente se aplica en `Health` y `Vehicle` | Se aplica solo en `Health.TakeDamage:177`. No cubre props, torres ni luminarias. **Rompe** `RestaurarVida(0)`, `DestruirPorGuion` y las heridas de guion del tutorial. | Hay tres bugs nuevos a corregir. La tarjeta DAÑO queda **75 % fuera del canvas** (borde en x≈764 de ±480). |
| 5 | T4: falta crear la vista de cabina | El periscopio ya existe, pero solo mientras se **mantiene** el click derecho. Hay un posible bug: `SetZoomed(false/true)` en cada cuadro reinicia la transición del FOV. Además ningún golpe al tanque trae su origen (ids -2/-3 y `VehicleDamagedEvent` sin posición). | Mira fija con alternar, medir el FOV y agregar `Origen` al evento. |
| 6 | T5: «varios feedbacks» | Son **11 elementos** distintos, y el médico ve dos círculos. El «cursor» de primera persona es la mira del centro; `CursorContextual` es solo del RTS. | Un engranaje centrado en la mira, filtrando en las vistas y no en `AccionesEnCurso`. |
| 7 | T6: crear la cinemática y la animación de las puertas | Ya existen `CineCorta(...)`, la animación `HundirLaReja` (1,3 s) y el aviso «N MILICIANOS LIBERADOS». Son 2 milicianos, no 4: `Bug407u` está desactualizado. | Solo falta la corrutina de 2 s que encadena lo que ya existe. |
| 8 | T7: `NavMeshAgent.avoidancePriority` | El perro **no** se mueve con NavMeshAgent: usa `NavMesh.CalculatePath` y `SoldierMotor.Move`, con el cerebro anulado, así que `AiBrain.TickSeparacion` no corre. Además todos apuntan al mismo punto. | Separación propia más puestos de manada en `PerroEnemigo.cs`. |
| 9 | T8: hay que buscar la causa | Causa encontrada: en `SC_Operacion` quedaron dos GameObjects huérfanos `Barrera_Reserva_SoloEnemigos` en (±45,6; 1,7; -176,1), que tapan la única puerta del cubículo. `NavMeshViva` los hornea al arrancar y el cubículo queda como isla. | Borrarlos y agregar una defensa en `CazaDeEnemigos`. |
| 10 | T10: eliminar colliders y crear un cartel | Los bloqueos de progresión son las compuertas invisibles de la mansión (`Compuertas.cs`) y el teletransporte de `LimitarRetrocesoEnHuida`. Ya existen `TickAlejamiento` (AvisoUnico) y `CartelVolverView` (solo nivel 1, con fuente por defecto). | Reutilizar y generalizar `CartelVolverView`. Los límites físicos (`Borde_*`, `Valle_*`) se quedan. |
| 11 | T12: «camión y gasolina» | Hay dos candidatos. (A) El campamento O del inicio, con camión y 6 tambores. (B) El generador con su tanque de combustible y `Camion_1/2`, que hoy **condicionan** la 1.ª puerta. | Se quitan los dos. La puerta queda habilitada desde t=0 y se vuela con la carga. |
| 12 | T13: «cuadro gris» | Muy probablemente es `Plaza_Cemento`: un cubo de 30×30, material `M_Op_CementoOsc`, debajo del tanque. La 2.ª hipótesis es el fondo gris claro de la barra de vida del tanque (13/11). | Atacar las dos y fotografiar ambas. |
| 13 | T15: checker = textura | No es una textura: son **100 cubos `Adoquin`** de dos grises en `P_Op_Ciudad_PlazaCivica.prefab`, y hay otro damero en 4 patios (`Patio_Losa`). Los materiales no emisivos reciben una celda plana del trimsheet, sin textura. | Solado nuevo con textura procedural y UV en metros, editando el prefab. |
| 14 | T14: medir con `PerfFrames` | `MetricasDeBuild` está fijo en `SC_Gameplay`: hoy nadie mide el nivel 2 en la build. | Agregar la toma `perf_ruta` al arnés, igual en ANTES y DESPUÉS. |
| 15 | Método: «la build actual más el arnés ampliado» | La build de `Builds/Windows64` trae el arnés **viejo**, con 5 tomas. El arnés corre con modo dios, que **triplica el daño propio** (#276) y **acelera el tiempo** (`VelocidadActiva`). | El arnés completo se escribe primero (T0) y se compila sobre `95b80a95` **sin tocar el juego**, así ANTES y DESPUÉS son comparables. Las tomas de daño apagan el modo dios. |
| 16 | T18 bloqueado | Ya hay criterios (`VALIDACION_PARCIAL2.md`). | T18 pasa a ser la auditoría Parcial 2 y se agregan T21 a T24. |
| 17 | Reconstruir con builders | `Construir()`, `Ambiente()`, `ConstruirCiudadIntegral`, `Caminos`, `Caminos2`, `Prestigio`, `SepararEntornoEnEscenas` y `OrdenarEntorno…` **no se pueden volver a correr**: duplican o pisan los ajustes en sitio. | Todo cambio de nivel va en archivos nuevos `*R13.cs`, idempotentes y con patrón «EscenaAbierta». |

---

## 1. Reglas de trabajo (valen para todo)

1. **Un solo Editor por proyecto.**
   - El código se puede escribir en paralelo, en archivos disjuntos.
   - Guardar escenas y prefabs, hornear y correr checks se hace **de a uno**.
   - `SC_Operacion.unity` es binaria: git no la mezcla. Un solo dueño por vez.
2. **Escenas.**
   - Abrir con `LimpiezaDeNivel.AbrirEscena` (`Ed\LimpiezaDeNivel.cs:20-34`). Aborta si hay alguna escena sucia y abre las cuatro.
   - Terminar con `EditorSceneManager.MarkAllScenesDirty(); EditorSceneManager.SaveOpenScenes();`.
   - Nada de `git checkout` sobre las escenas.
   - `RespaldoDeEscenas` respalda solo los `.unity`. **Los prefabs `P_Op_*`, `P_Esc_*` y `Fus_*` no tienen respaldo**: copiarlos a `Temp/R13_prefabs_antes/` antes de editarlos.
3. **Builders nuevos.**
   - Un archivo `Ed\OperacionBuilder.<Tema>R13.cs` con `public static string <Tema>R13EscenaAbierta()`.
   - Idempotente: borra y recrea su grupo `R13_<Tema>` por nombre.
   - Devuelve un informe de texto. Para prefabs: `PrefabUtility.LoadPrefabContents` → cambios → `SaveAsPrefabAsset` → `UnloadPrefabContents`.
   - Si el cambio debe sobrevivir a una reconstrucción, portarlo **también** al builder viejo (`ArmarPlazaCivica`, `PatioDelTanque`, `CampamentoMilitar`), sin ejecutar ese builder.
4. **Checks en Play** (`Ed\ChecksBugs065*.cs`).
   - Método `static IEnumerator BugNNNx()` que cierra con `Fin("OK …")` o `Fin("FALLO …")`. Regex `^Bug(\d{3})([a-z]?)$`.
   - Los ids que **matchean `reMansion`** (`ChecksBugs065.cs:128`, `9[56][0-9]a`) corren con la mansión **encendida**. Los demás, con la mansión apagada.
   - Ids libres: 958a, 959a, 964a-969a (con mansión) y 970a-999a (sin mansión). La asignación está en §8.
   - Se corren con `unity command eval "return SP.EditorTools.ChecksBugs065.CorrerLista(\"Bug970a Bug971a\");"` y después se lee `Temp/checks_progreso.txt`.
   - Sin ventana no hay `Mouse` ni `Keyboard`: se usan dispositivos virtuales y los tiempos se miden en `Time.time`. `Bug602e` solo pasa con el Editor con ventana.
5. **Commits.**
   - Solo con orden del dueño.
   - El mensaje termina con la línea `Co-Authored-By` que indique la sesión que hace el commit.
   - Los registros de `Registros/` no se commitean.
6. **Convención de nombres que lee el arnés** (§7.3). Lo nuevo se llama exactamente como figura ahí, para que el **mismo** arnés lo mida en ANTES (donde falta, y lo reporta como 0) y en DESPUÉS.

---

## 2. Método de evidencia: ANTES, implementar, DESPUÉS, comparar

**E0 · T0, arnés completo**
- Pasar `S\Core\CapturasDeBuild.cs` a `sealed partial`.
- Escribir **todas** las tomas de §7 en archivos `CapturasDeBuild.<Grupo>.cs`.
- Se programan una sola vez y antes de tocar el juego. Usan reflexión y búsqueda por nombre, así que compilan sobre `95b80a95` y sobre el código final.

**E1 · Build ANTES**
- `95b80a95` + arnés (sin ningún otro cambio). Comando: `-batchmode -executeMethod SP.EditorTools.CliBuilder.BuildWindows64`. Unity se cierra al terminar.
- Copiar la salida a `Builds/R13_Antes/`. `Builds/Windows64` se deja tal cual como referencia.
- Correr dos veces: `StrategicPoint.exe -capturas Docs/RONDA_13/antes[_b] -screen-width 1600 -screen-height 900 -screen-fullscreen 0`.
- Elegir, toma por toma, la corrida limpia. Los obuses del pesado tapan fotos (dato de una ronda anterior).

**E2 · Líneas base en el Editor**
- Lote completo de checks: `ChecksBugs065` + `HeadlessTestRunner.RunAll` → `Docs/RONDA_13/antes/checks_base.txt`, con la lista de los que **ya fallan**.
- `AuditoriaDeSolapesR13.Informe("antes")` y `AuditoriaDeColliders` corregido (T11 y T10) → `solapes_antes.txt` y `colliders_antes.txt`.
- `Bug501g` → `colisiones_501g_antes.txt`.

**E3 · Implementación** por olas (§6).

**E4 · Build DESPUÉS y comparación**
- Misma línea de comando → `Docs/RONDA_13/despues/`.
- Atlas lado a lado `Docs/RONDA_13/ATLAS_antes_despues.jpg`: una fila por toma, con su id.
- `Docs/RONDA_13/VALIDACION_R13.md`: una fila por trabajo con medida, ANTES, DESPUÉS, umbral y estado (OK / parcial / no). Plantilla en §9. No usar el nombre `VALIDACION.md`, para no confundirlo con `VALIDACION_PARCIAL2.md`.
- Repetir el lote completo de checks: **ningún** fallo nuevo fuera de los actualizados a propósito (§8.2).

---

## 3. Decisiones del dueño

Cada una tiene un valor por defecto. Si no hay respuesta, se aplica ese valor y queda anotado en `VALIDACION_R13.md`.

| # | Pregunta | Por defecto |
|---|---|---|
| D1 | ¿L1 (4, 7, 9 y 10) habla de la Operación (nivel 2) o del nivel 1 (`SC_Gameplay`)? | La Operación. Para el nivel 1 solo se revisan los límites de T10 (`BlockoutArtDresser.Perimetro`). |
| D2 | Cuchillo contra el escudero de frente: ¿bloquea? | Bloquea de frente (dentro de 62°) y mata por el costado o la espalda. |
| D3 | `CuchilladaLenta` (cámara lenta ×0,15 durante 1 s en cada baja con cuchillo) | Enfriamiento de 20 s. |
| D4 | Cohetes contra el heli de la azotea: ¿≤4 en Fácil y ≤6 en Difícil, o 4 en todas? | ≤4 / ≤5 / ≤5, con un cohete de margen sobre la munición (9/7/5). |
| D5 | ¿El coeficiente de daño propio vale también para props y torres? | Sí para `TorreDestruible` y `Vehicle`. No para luminarias, barriles ni coberturas. |
| D6 | Mira del tanque fija desde el inicio: ¿solo en la Operación? | Solo en el asiento de artillero de la Operación. El tutorial cambia el texto a «[CLIC DERECHO] ALTERNA LA MIRA». |
| D7 | Revivir: ¿se quitan también los 3 anillos de onda al terminar (`FeedbackDeRevivir`)? | Queda un solo anillo y el sonido. |
| D8 | ¿Las compuertas de la mansión (subida y bajada) dejan de bloquear? | Las dos dejan de bloquear y muestran el cartel. La lógica de pisos se mantiene. |
| D9 | Carteles «CIUDAD >>», «PLAZA >>» y «BIENVENIDOS A» | Queda «BIENVENIDOS A» (de ambientación). Se borran los otros dos. |
| D10 | T13: ¿el «cuadro gris» es la losa o la barra del HUD? | Se corrigen los dos. |
| D11 | Fuentes OFL de Google Fonts (`BlackOpsOne`, `StardosStencil`): ¿la docente las acepta? | Se mantienen y se documenta la licencia. El plan B está en T23. |
| D12 | Integrantes y roles del equipo para `INTEGRANTES.txt` | **Bloqueante para T21.** No se puede inventar. |

---

## 4. Trabajos

En cada trabajo: **Hoy** (hechos del código), **Causa**, **Cambio**, **Medida**, **Riesgos**, **Archivos** y **Depende de**.

### T0 · Arnés de capturas ampliado (nuevo, va primero)

**Hoy**
- `S\Core\CapturasDeBuild.cs` es `sealed`, con 5 tomas fijas en `Todo()` (L58-77).
- `Foto()` usa una cámara propia, oculta los marcadores y **congela** `Time.timeScale`.
- `FotoJuego()` captura la pantalla del juego, con HUD.
- Activa `ModoDios.Poner(true)` (L66), que triplica el daño del bando propio (`Health.cs:175`) y acelera el tiempo.

**Cambio**
1. Pasar a `sealed partial`. `Todo()` recorre la lista ordenada de §7.
2. Filtro `-tomas a,b,c`.
3. `UnityEngine.Random.InitState(1313)` al empezar.
4. Límite de 60 s por toma dentro de `Seguro`.
5. Tiempo en tiempo real.
6. Formato de datos parseable: `dato <toma>.<clave>=<valor>`.
7. Ayudantes nuevos:
   - `SinDios(Action)`: apaga `ModoDios` y `ModoDios.VelocidadActiva` y deja al jugador `Invulnerable = true` durante la medición.
   - `ConDificultad(n)`: fija `Dificultad.Actual` y restaura al final.
   - `ContarPorNombre(prefijos[])`.
   - `FuentesNoBelicas()`.
   - `Perf(segundos)`: devuelve el promedio y los percentiles 95 y 99 de `unscaledDeltaTime`.
   - `Resolucion(w,h)`: `Screen.SetResolution(w,h,false)` y espera 1 s.
8. Todo lo nuevo se busca **por nombre y por reflexión**, nunca por tipo nuevo. Así compila igual sobre `95b80a95`.

**Medida**
- La build ANTES produce las ~40 imágenes de §7 y `captura_info.txt` con `FIN`.
- Ninguna toma termina en `ERROR`, salvo las marcadas «solo DESPUÉS».

**Riesgos**
- Que la toma rompa el estado de la siguiente. Cada toma empieza con `Saltar(n)`.
- El tiempo total. Hoy son unos 3 min; el objetivo es menos de 12 min.

**Archivos:** `S\Core\CapturasDeBuild*.cs` (solo T0). **Depende de:** nada.

---

### Combate y armas

#### T1 · Cuchillo letal + sangre (L1-1)

**Hoy**
- `S\Combat\WeaponHolder.cs:592-595`: `KnifeRange 2.2`, `KnifeArcDeg 70`, `KnifeDamage 55`, `KnifeCooldown 0.55`.
- `TryMelee()` (L604-615) llama `best.Health.TakeDamage(55, owner.Id)`.
- `BuscarObjetivoDeCuchillo()` (L617-639) recorre `ActorRegistry.All`. No mira la línea de vista.
- Solo lo usa el jugador (`PlayerInputDriver.cs:1515`).
- Vidas:

  | Objetivo | Vida | Fuente |
  |---|---|---|
  | Enemigo | 180 | refuerzos clonados, sin `Dificultad.AjustarVida` |
  | Perro | 90 | |
  | Escudero | 360 | bloquea de frente: `EnemigoEscudero.Filtrar` L57-70 |
  | Pesado | 2160 | 180×12, aplicado en `Start` dos cuadros después |

- Cuchilladas necesarias hoy: soldado 3/3/4; pesado 30-40.
- `S\Presentation\CuchilloFx.cs:17-46`:
  - Siempre suena `KnifeSwing`.
  - Si conecta: `ImpactFx.Spawn` (rojo, 0,55) y `Feedback.Accion(KnifeHit)`.
  - `CuchilladaLenta` si mata. Su comentario #274 ya decía que la cuchillada era letal.
  - No hay sangre.

**Causa**
- El daño de 55 es plano y queda escalado por la dificultad, el modo dios y `AjustesDeDano.Propio`, que puede bajar hasta 0,1.

**Cambio**
1. **`S\Combat\Health.cs`: `public bool Ejecutar(int attackerId, bool respetarEscudo)`.**
   - Lleva `Current` a 0 sin coeficientes.
   - Respeta `Invulnerable`, `ModoDios.Protege` y `EsFuegoAmigoEnemigo`. Si `respetarEscudo`, también `FiltroDeDano`, llamado con `amount = Current`. Si el filtro devuelve 0, el golpe queda bloqueado.
   - Reusa la cola común: reinicio de la regeneración, `LastAttackerId`, `DamageTakenEvent` y `EntityDiedEvent`.
   - Para hacerlo, **extraer esa cola a un método privado `AplicarRestaFinal(int amount, int attackerId)`**, que comparten `TakeDamage` y `Ejecutar`. No usar `headshot:true`, que escribe «HEADSHOT» en el registro.
2. **`WeaponHolder.TryMelee`.**
   - Sin `EnemigoPesado`: llama `Ejecutar(owner.Id, respetarEscudo: true)`.
   - Con `EnemigoPesado`: campos nuevos en `S\Operacion\EnemigoPesado.cs`: `int GolpesDeCuchillo`, `float UltimoGolpeDeCuchillo` y `const float VentanaEntreCuchilladas = 4f`.
     - 1.er golpe: `Current = min(Current, VidaMaxima/2)`, con `Ejecutar` parcial o una resta directa por la cola común.
     - 2.º golpe dentro de la ventana: `Ejecutar`.
     - Fuera de la ventana el contador vuelve a 1.
3. **`BuscarObjetivoDeCuchillo`.**
   - `Physics.Linecast(ojoDelJugador, pechoDelBlanco)` con el criterio de `NavService.BlocksMovement`. Mismo patrón que `Projectile.LaExplosionAlcanza` L684-700.
   - Si está tapado, no hay blanco.
4. **Sangre: `S\Presentation\SangreFx.cs` nuevo** (estático, pool propio de 4 `ParticleSystem`, el objeto se llama `SangreFx_<n>`).
   - Ráfaga de 48 partículas, rojo saturado HDR (1,0; 0,05; 0,03) × 2,5, con material propio sin niebla. No reusar `Projectile.ColorSangre`, que es demasiado oscuro.
   - Gotas con gravedad 1, vida de 0,6-1,1 s y velocidad de 3-6 m/s en un cono de 50° en la dirección del tajo.
   - Destello con `ImpactFx.Spawn` rojo de 0,35 s.
   - Mancha en el piso: `DecalKind.Sangre`, agregado **al final** del enum en `DecalPool.cs:6` con su material. Dura 8 s.
   - No usar `DebrisPool`: tiene 160 piezas y lo comparten las explosiones.
   - Solo si conecta, llamado desde `CuchilloFx.Tajo` cuando `objetivo != null`.
5. **Sonido.**
   - `SfxKind.KnifeBlood` **al final** del enum (`GenericSfx.cs:25`).
   - Su `case` junto a L395-396.
   - `SfxSintetico.CuchilloSangre()` junto a L187-203: ruido rosa filtrado de 120 ms + golpe sordo de 90 Hz + chasquido húmedo.
   - `Precargar` lo toma solo.
6. Registro: `cuchillo: blanco=<nombre> vida_antes=<v> matado=<bool> pesado_golpes=<n> tapado=<bool>`.
7. `CuchilladaLenta`: enfriamiento según D3.

**Medida**
- **`Bug970a`**: en Medio, sin modo dios. Soldado de espaldas → muere de un golpe. Pesado → vivo tras el 1.º y muerto tras el 2.º (separados 0,6 s). Pesado con 5 s entre golpes → vivo. Soldado detrás de un muro a 1,5 m → `tapado=true` y sin daño. Escudero de frente → según D2.
- **`Bug971a`**: al conectar, más de 30 partículas vivas de `SangreFx_*` a los 0,1 s. Se reprodujo `KnifeBlood`. El fallo no genera sangre. Hay un decal de sangre.
- Arnés: tomas `cuchillo_soldado_*` y `cuchillo_pesado` (§7). El dato `vida_despues` debe ser 0, con 2 golpes para el pesado.

**Riesgos**
- El sigilo se desbalancea: el cuchillo mata en silencio. El alcance de 2,2 m con línea de vista lo acota.
- `HeadlessTestRunner.Fases13a17.cs:466-472` espera `100-55` (T19).
- `ChecksBugs065.Wp2.cs:44-47`: lista de clips 3D (agregar `KnifeBlood`).

**Archivos:** `Combat\WeaponHolder.cs`, `Combat\Health.cs`, `Operacion\EnemigoPesado.cs`, `Presentation\CuchilloFx.cs`, `Presentation\SangreFx.cs` (nuevo), `Presentation\GenericSfx.cs`, `Presentation\SfxSintetico.cs`, `Presentation\DecalPool.cs`, `Presentation\CuchilladaLenta.cs`.

**Depende de:** nada. **Lo necesitan:** T3, que usa `Ejecutar`.

#### T2 · Lanzacohetes contra el helicóptero jefe (L1-2)

**Hoy**
- Cohete (`S\Combat\WeaponCatalog.cs:68`): daño 95, radio 5 m, 104 m/s, `MultiplicadorVsVehiculo 2.5`. Ese campo **no lo lee nadie**: `WeaponHolder.cs:748` llama `pool.Spawn(…, espec.ProjectileSpeed)` sin el multiplicador, que vale 1 por defecto (`ProjectilePool.cs:101`).
- La regla #055 (`Projectile.cs:877`, multiplicador ≥2) nunca se activa.
- El heli jefe es `HeliDeLaAzotea` (`OperacionDirector.Mansion.cs:445/655`):
  - Vida 1600 (`VidaDeAsalto` 320×5), sin ajuste por dificultad.
  - `AjusteDeDano = golpe × 0,6` (`HelicopteroEnemigo.cs:308`).
  - `BoxCollider (0; 1,8; -1)` de tamaño (3,2; 3,0; 10,5).
- En `ExplodeAt` (L864-882) la distancia se mide contra el **pivote** (`vehicle.transform.position`): la cola queda a más de 5 m (daño 0) y la nariz a ~0,4.
- Cohetes por partida: 1 + cargadores (8/6/4) = 9/7/5.
- Necesarios hoy: 22/26/29 en el mejor caso y de 31 a 70 en el caso típico.
- El HALCÓN de `OperacionDirector.Final.cs` es de guion (invulnerable). `OperacionDirector.Jefe.cs` es el **tanque** jefe.

**Causa**
- Las cuatro causas de §0.3.

**Cambio**
1. `WeaponHolder.cs:748`: pasar `multiplicadorVsVehiculo: owner.Team == TeamId.Player ? espec.MultiplicadorVsVehiculo : 1f`. Así se evita subir de golpe los cohetes del pesado contra el tanque del jugador.
2. `Projectile.ChocoContraElMundo` (L488-493):
   - Si el collider tocado tiene `GetComponentInParent<Vehicle>()`, pasarlo a `ExplodeAt(…, Vehicle impactoDirecto = null)`.
   - En el bucle de vehículos (L870-874), para `impactoDirecto` usar cercanía 1 y no cortar por la distancia al pivote.
   - Para el resto, usar `ClosestPoint` del collider en lugar del pivote. Eso también corrige el tanque y las camionetas.
3. `HelicopteroEnemigo.Armar:308`:
   - `AjusteDeDano = g => Vehicle.DanoDeExplosionEnCurso ? round(g × FactorVsExplosivo) : max(1, round(g × FactorDeResistencia))`.
   - `const float FactorVsExplosivo = 1.6f`.
4. Fórmula de control (para el ajuste): cohetes = ⌈1600 / (95 × M × R × Dj × Propio)⌉, con M = 2,5, R = 1,6 y Dj = `DanoJugador` (1,3 / 1,1 / 1,0).
   - Fácil: 494 → 4. Medio: 418 → 4. Difícil: 380 → 5.
   - Cumple D4, con margen sobre 9/7/5.
5. Opcional, si D4 pide más margen: recargar cohetes en `ArrancarLaAzotea` (`OperacionDirector.Azotea.cs:193`).

**Medida**
- **`Bug958a`** (con mansión):
  - `SaltarALaAzotea()` → esperar `HeliDeLaAzotea` vulnerable.
  - Por cada dificultad: restaurar la vida y disparar con la **misma llamada** que el arma real (un ayudante de `WeaponHolder` expuesto por T2, `DispararEspecPrueba(espec, boca, dir)`), apuntando a la nariz, al centro y a la cola.
  - Criterio: daño mayor que 0 en los tres puntos y cohetes hasta morir ≤ 4/4/5.
- Toma `heli_cohetes` del arnés: dato `cohetes_hasta_morir.<dif>`.
  - En ANTES dará más de 12 («no murió con 12»). Es esperable y se documenta.
- Regresión: **medir otra vez** el tanque jefe (`TanqueJefe`, 3200) y las camionetas (260) con cohetes propios. El punto 2 cambia el daño contra todo vehículo. Agregar el dato en `Bug958a`: cohetes al `TanqueJefe` antes y después.

**Riesgos**
- El cambio afecta a todos los `HelicopteroEnemigo` (azotea, cuerdas, `DefensaDelCentro.cs:75`). Los de cuerdas son invulnerables hasta colgar las sogas: no cambia.
- `Bug602d` (`ChecksBugs065.Hoy.cs:772-783`) no cambia, porque no es una explosión.
- Los checks Wp9b (`ChecksBugs065.Wp9b.cs:139,153`) llaman `ExplodeAt(…, 2.5f)` directo: siguen pasando, pero revisar que el nuevo parámetro tenga valor por defecto.

**Archivos:** `Combat\WeaponHolder.cs` (lo comparte T1), `Combat\Projectile.cs` (lo comparten T4 y T3), `Operacion\HelicopteroEnemigo.cs`, opcional `OperacionDirector.Azotea.cs`.

**Depende de:** T1 (mismo archivo, mismo carril).

#### T3 · Daño: interfaz de configuración y alcance global (L1-3)

**Hoy**
- `S\Core\AjustesDeDano.cs`:
  - Rango 0,1-3. Por defecto Propio 1 y Enemigo 0,5.
  - Se guarda en PlayerPrefs (`ajuste_dano_propio` y `ajuste_dano_enemigo`) **desde el setter**.
  - `Restablecer()` funciona.
  - `Multiplicador` (L41-58): civil → 1; prop → 1.
- Se aplica solo en `Health.cs:177`.
- `S\UI\PanelDeDano.cs:16-31`: tarjeta hija del `SettingsPanel`, anclada a (1,1), con `anchoredPosition (24,0)` y tamaño 470×372.
  - El layout (`S\UI\LayoutDeAjustes.cs`) da una tarjeta de 912×645 con escala 0,80, en `CanvasPausa` de 960×540.
  - El borde derecho de DAÑO queda en 764 contra 480: **se ve el 25 %** en cualquier resolución 16:9.
  - No lo detecta nadie: `Diagnosticar` y `TieneContenido` (L241/256) no revisan los nietos, y `Ed\CapturasDeUi.cs:23-29` usa un panel **falso** de 540.
- Bugs de alcance:
  - `Health.RestaurarVida(0)` (`Health.cs:60`) no mata con los valores por defecto: (864 + 1000) × 0,585 × 0,5 < 864.
  - `Vehicle.DestruirPorGuion` (`Vehicle.cs:124`) falla con `Propio` < 0,38 (heli) o < 0,43 (tanque). Se llama desde `Final.cs:383`, `Huida.cs:563` y `HelicopteroEnemigo.cs:567`.
  - Las heridas de guion del tutorial (`TutorialManager.cs:523/761/794/824`, id -1) quedan **a la mitad**.
  - `HeadlessTestRunner.RestablecerEstaticosDeJuego` (L197-207) no reinicia `AjustesDeDano`: la suite depende de los sliders del usuario.
  - `OperacionAuto.cs:224` (el auto se daña a sí mismo, id -1) usa `Propio`.

**Cambio (interfaz)**
1. Convertir DAÑO en **sección** del layout, no en tarjeta suelta.
   - En `PanelAjustesExtra.Construir` (L38-52): `Sec_Dano` y `SecSep_Dano` en la columna derecha, después de PANTALLA. Esa columna mide 388 contra 454 de la izquierda.
   - Filas `Daño propio_Label/_Slider/_Chip/_Value` y `Daño enemigo_…`, con los nombres que espera `Poner(...)`.
   - El número editable va en el chip. Restablecer es un botón de la sección.
   - El alto llega a ~777 y la escala queda en ~0,67, que sigue siendo legible.
   - `PanelDeDano` queda como lógica: enlaza sliders y campos con `AjustesDeDano`, sin crear su propia tarjeta.
2. `LayoutDeAjustes.Diagnosticar`: medir **todos** los `RectTransform` descendientes contra las esquinas del canvas en mundo. Devolver `fuera=<n>` y `peor=<% fuera>`.
3. `Ed\CapturasDeUi.cs`: usar el panel real (`PanelAjustesExtra.Preparar` sobre `PauseController`).
4. Textos con `Loc.T`. Fuente según T22.
5. Detalle: `Firma()` (L78-87) incluye los textos de información y cada arrastre dispara `Aplicar()`. Excluir `Info_*`.

**Cambio (alcance)**
1. `RestaurarVida(0)` y `DestruirPorGuion` usan `Health.Ejecutar(-1, false)` de T1.
2. Las heridas del tutorial usan `Health.AplicarDanoDeGuion(int)`, que pasa por la cola común **sin coeficientes**, o un id de guion `AjustesDeDano.IdGuion = -9` que `Multiplicador` devuelve como 1.
3. Props, según D5: `Projectile.cs:526/797` → `ObstacleMarker.TakeDamage` aplica `AjustesDeDano.Propio` solo si `GetComponent<TorreDestruible>()` y el atacante es del bando Player.
4. `AjustesDeDano.ForzarParaPrueba(float propio, float enemigo)` no persistente (no escribe PlayerPrefs) y `Liberar()`. Tanto `HeadlessTestRunner.RestablecerEstaticosDeJuego` como los checks lo usan con 1 / 0,5.

**Medida**
- **`Bug972a`**: con `ForzarParaPrueba(2,2)` y luego `(0.5,0.5)`, en Medio y sin modo dios, aplicar 100 por cada vía de la tabla del informe de combate. Las vías son: bala, cohete directo a un vehículo, explosión a soldado, explosión a vehículo, perro, embestida, atropello, carga adosada, `TorreDestruible` y caída.
  - Esperado: ×k según el bando, ±1 por redondeo.
  - También: `RestaurarVida(0)` mata, `DestruirPorGuion` destruye con `Propio` 0,1, y la herida del tutorial de 40 quita exactamente 40.
- **`Bug973a`**: a 1280×720, 1600×900 y 1920×1080, con escala de interfaz al 100 % y al 125 % y en es/en.
  - Abrir Configuraciones → `Diagnosticar` → `fuera == 0`.
  - Arrastrar un slider → `AjustesDeDano.Propio` cambia. `Restablecer` → 1 / 0,5.
  - Extender también `HeadlessTestRunner.Fase21.cs:439-499`.
- Arnés: tomas `ajustes_dano_1280`, `_1600` y `_1920`, con el dato `dano_visible_%`. ANTES ≈ 25; DESPUÉS = 100.

**Riesgos**
- Los PlayerPrefs del usuario quedan con valores raros si un check falla a mitad: `Liberar()` va en `finally`.
- Cambiar `Health.cs` y `Vehicle.cs` toca a T1 y T4: mismo carril y orden T1 → T2 → T3.

**Archivos:** `Core\AjustesDeDano.cs`, `UI\PanelDeDano.cs`, `UI\PanelAjustesExtra.cs`, `UI\LayoutDeAjustes.cs`, `UI\EstiloDeAjustes.cs` (si hace falta), `Combat\Health.cs`, `Vehicles\Vehicle.cs`, `Tutorial\TutorialManager.cs`, `Presentation\ObstacleMarker.cs`, `Ed\CapturasDeUi.cs`, `Ed\HeadlessTestRunner.cs` y `Fase21.cs`.

**Depende de:** T1 (`Ejecutar`).

---

### Tanque y feedback

#### T4 · Mira del tanque + indicador de impactos (L1-5)

**Hoy**
- El tanque del jugador es `OperacionDirector.tanque`, el mismo `Vehicle` del jefe GOLIAT ya reparado.
- `Embarcar()` (`OperacionDirector.Huida.cs:71`) → `PonerDeArtillero` (L60-68) → asiento `Gunner`, con la metralleta quitada (`Huida.cs:135`).
- Cámara: `UpdateVehicleCameraAimed` (`S\Player\PlayerInputDriver.Vehiculo.cs:572-604`).
- Periscopio: `periscopio = conMira && Rig.EstaConZoom && Rig.AdsBlend > 0.55f` (L598).
- Click derecho **mantenido**: `Rig.SetZoomed(mouse.rightButton.isPressed)` (L371-372).
- `UpdateInVehicle` hace `SetZoomed(false)` al principio de cada cuadro (L169) y `SetZoomed` (`S\Camera\CameraRig.cs:175-181`) reinicia la transición del FOV en cada cambio. Sospecha: el zoom avanza muy lento (≈0,3 %/cuadro).
- Impactos:
  - `VehicleDamagedEvent(Vehicle, Amount, Remaining, Max)` no trae origen (`S\Core\Events.cs:149-163`).
  - Los atacantes de la huida usan los ids -2/-3 (`OperacionAuto.cs:223/260`, `OperacionAuto.Tanque.cs:59`, `HelicopteroEnemigo.cs:467`, `EmboscadaDeCohetes.cs:78`, `TanqueJefe.cs:230/256`).
  - `DamageDirectionView` no sirve: los ocupantes están `SetActive(false)`.

**Cambio**
1. **Mira fija** (`PlayerInputDriver.Vehiculo.cs`):
   - Campo `bool miraDelCanonFijada`.
   - En la rama del artillero: `if (mouse.rightButton.wasPressedThisFrame) miraDelCanonFijada = !miraDelCanonFijada; zoomDeseado = miraDelCanonFijada;`.
   - Se pone en `true` al entrar de artillero: en `SwitchSeat` (junto a L450) y en `EnterPossessedVehicleSeat` si `role == Gunner`, y solo en la Operación (D6).
   - Se pone en `false` en `ExitVehicle` y `ClearVehicleSeatState`.
   - **Corregir el doble `SetZoomed`**: calcular `zoomDeseado` en el cuadro y llamar `Rig.SetZoomed(zoomDeseado)` una sola vez al final de `UpdateInVehicle`.
   - En `CameraRig.SetZoomed`, si el valor no cambió, no reiniciar `zoomTransicionElapsed`.
   - Al volver de RTS con [TAB], `SetMode` pone `adsBlend = 0` (`CameraRig.cs:444`): si `miraDelCanonFijada`, volver a pedir el zoom (es aceptable que entre en 0,5 s).
2. **Origen del golpe**:
   - `VehicleDamagedEvent` suma `Vector3 Origen` y `bool TieneOrigen`, con un constructor sobrecargado.
   - `Vehicle.TakeDamage(int, int, Vector3 origen)` nuevo. El viejo lo llama con `TieneOrigen = false`.
   - Pasan origen:
     - `Projectile.cs:503`, impacto directo: `origenDeVuelo` o `transform.position - velocity.normalized*5`.
     - `Projectile.ExplodeAt` (L880): el punto de la explosión.
     - `OperacionAuto.cs:223`, embestida: posición del auto.
   - Si no hay origen y `attackerId >= 0`: `ActorRegistry.FindById`.
3. **Anillo** en `S\UI\HudDeTanqueSimple.cs`:
   - Hermano de `raiz`, llamado `AnilloDeImpactos`, en su Canvas overlay (`sortingOrder 60`, referencia 1920×1080).
   - Diámetro 320 px, para no pisar los anillos de `MiraDeTorreta` (124 y 92 px).
   - Hasta 8 marcas `MarcaDeImpacto_<n>` rojas (1; 0,15; 0,1), en forma de arco de 22°.
   - Ángulo = `SignedAngle(camFwdPlano, origen - vehiculo, up)`, rotación `-ang`. Se mide contra la **cámara**, porque la torreta gira.
   - Alfa 1 → 0 en 2 s, con un pulso de escala de 0,15 s.
   - Se suscribe a `VehicleDamagedEvent` con `e.Vehicle == PlayerInputDriver.Activo.Vehicle`.
   - Expone `public IReadOnlyList<float> AngulosActivos`.

**Medida**
- **`Bug974a`** (patrón `PrepararTanque`, `ChecksBugs065.Wp6.cs:40-54`):
  - Sin clic: `Rig.EstaConZoom && AdsBlend > 0.9` a los 1,0 s, y `cam.fieldOfView` a menos de 0,5° del FOV de zoom a los 1,0 s (esto detecta el bug del FOV).
  - Clic derecho simulado → sin zoom. Otro clic → zoom.
- **`Bug975a`**:
  - `tanque.TakeDamage(10, -3, origen)` con origen a -90° y a 180° de la cámara → `AngulosActivos` contiene ≈ -90 y ≈ 180, ±5°.
  - A los 2,1 s: lista vacía.
  - Un impacto real de proyectil desde la izquierda también genera la marca.
- Arnés: `tanque_mira_inicial` (dato `ads`, `fov_1s`, `fov_2s`), `tanque_impacto_izq` y `tanque_impacto_atras` (dato `marcas`).

**Riesgos**
- Tutorial: el paso «mira_tanque» (`S\Tutorial\TutorialManager.cs:1016-1035`, `f.sueltaMiraTanque`) y `TutorialAutoPlayer.GestoMiraTanqueReal` (L194) suponen que se **mantiene**: actualizar los textos y el gesto (D6).
- Los checks que leen `MiraDeTorreta` o `TurretAim` suponen tercera persona al subir (`Nov08b.cs:119-236`, `Nov11.cs`, `Bug416a`, `Nov13c`/`d`).

**Archivos:** `Player\PlayerInputDriver.Vehiculo.cs`, `Camera\CameraRig.cs`, `Core\Events.cs`, `Vehicles\Vehicle.cs` (lo comparte T3), `Combat\Projectile.cs` (lo comparte T2), `Operacion\OperacionAuto.cs`, `UI\HudDeTanqueSimple.cs` (lo comparte T13 y T22), `Tutorial\TutorialManager.cs` (lo comparte T3), `Tutorial\TutorialAutoPlayer*.cs`.

**Depende de:**
- La parte 1 (mira) y el dibujo del anillo no dependen de nada.
- La parte 2 (origen) va **después** de T2 y T3, porque esos trabajos tocan `Vehicle.cs` y `Projectile.cs`.

#### T5 · Revivir: un solo engranaje radial (L2-5)

**Hoy**
- Tiempos: `TiempoDeRevivir = 5f` (`PlayerInputDriver.cs:1688`) y médico 2,5 s (`:1808`).
- Elementos que se ven hoy al revivir:

  | # | Elemento | Dónde |
  |---|---|---|
  | 1 | `CirculoRevivir` | `:1778-1793`, en (0,5; 0,32) |
  | 2 | `CurandoAnimacion`: anillo verde y `CruzVerde` | |
  | 3 | Barra en el mundo con ícono, texto y línea | `AccionesEnCursoView.cs:241-252` |
  | 4 | Texto único «▶ REVIVIENDO» | `AccionesEnCursoView.cs:282` |
  | 5 | Dos engranajes en el mundo | `EngranajesDeAccion.cs:203-208` |
  | 6 | Rombo con engranaje | `InteractGearMarker` |
  | 7 | Pista «MANTENGA [E]…» | |
  | 8 | Texto de instrucción | `:1746` |
  | 9 | Mira dorada `Caido` | |
  | 10 | Marca de caído | `IndicadorDeCaidos.cs:149-187` |
  | 11 | `CirculoEnfoque` (solo el médico) | `:1859/1946-1962` |

- Al terminar: `FeedbackDeRevivir.AlRevivir`, con sonido y 3 anillos.
- No existe `Capturas_Operacion/revive_*.png`: las fotos viejas están en `Docs\RONDA_12\`.

**Causa**
- Cada sistema agregó su propio aviso y nadie centralizó.

**Cambio**
1. Componente nuevo `S\UI\EngranajeDeRevivir.cs` (el objeto se llama `EngranajeDeRevivir`).
   - Dos `Image` con `Sprite.Create(DiamondGizmo.TexturaEngranajeConAlfa())` (`S\Presentation\DiamondGizmo.cs:202`): fondo gris al 40 % y relleno verde `Filled/Radial360` (origen arriba, sentido horario).
   - 64 px, en (0,5; 0,5) bajo el padre de `AimUiRef`. Gira a 30°/s.
   - Una sola instancia, que usan el jugador y el médico.
2. `MostrarCirculoRevivir` y la rama del médico usan `EngranajeDeRevivir`. Se borran `CirculoRevivir` y `CirculoEnfoque`, o quedan inactivos sin crearse.
3. Filtro «revivir del líder» (verbo `REVIVIENDO` y actor `AjustesDeEscuadra.Lider`):
   - `EngranajesDeAccion.Refrescar`: saltear.
   - `AccionesEnCursoView.Refrescar`: sin barra y sin `AvisoUnico("accion")`.
   - `ActualizarPromptContextual`: sin `InteractGearMarker` ni pista **mientras se mantiene** E. Antes de empezar, la pista queda (es la única guía).
   - `SetInstructionText("")`.
   - `IndicadorDeCaidos`: ocultar la marca de **ese** caído.
   - `CurandoAnimacion.Tick(…, sinFx: true)`: conserva la postura y quita el anillo y las cruces.
   - **No** tocar `AccionesEnCurso.Reportar`: lo usan la postura, `RosterRowView` y `AiBrain.Vehiculos.cs:141`.
4. Al terminar, según D7: un solo anillo y el sonido.

**Medida**
- **`Bug976a`** (patrón `Bug088`, `ChecksBugs065.Wp5a.cs:229-328`):
  - A los 2 s de mantener E, contar objetos activos de feedback: solo `EngranajeDeRevivir` == 1, con `fillAmount` ≈ 0,40 ± 0,05.
  - En 0: `CirculoRevivir`, `CirculoEnfoque`, `CruzVerde*`, `AnilloCuracion`, `EngranajesDeAccion.Activos`, `AccionesEnCursoView.BarrasVisibles`, `InteractGearMarker`, la marca del caído y el texto «accion».
  - Repetir con el médico: `fillAmount` ≈ 0,8 a los 2 s.
- Arnés: `revivir_40` y `revivir_medico`, con el dato `feedback_total`. ANTES ≥ 6; DESPUÉS = 1.

**Riesgos**
- `Bug088` (captura `v2_088_reviviendo.png`), `Bug089` (3 anillos) y `HeadlessTestRunner.Fase21.cs:124-171` (barra de REVIVIENDO): actualizar en T19.
- `Bug066` no cambia (DETONANDO).

**Archivos:** `Player\PlayerInputDriver.cs` (lo comparte T22), `Presentation\EngranajesDeAccion.cs`, `Presentation\AccionesEnCursoView.cs` (T22), `Presentation\CurandoAnimacion.cs` (T22), `Presentation\IndicadorDeCaidos.cs` (T22), `Presentation\FeedbackDeRevivir.cs`, `UI\EngranajeDeRevivir.cs` (nuevo).

**Depende de:** T22 primero (§6), solo para no editar los mismos archivos a la vez.

#### T6 · Liberar milicianos: cinemática de 2 s + feedback (L1-6)

**Hoy**
- La terminal solo crea arte (`OperacionDirector.TerminalDeCeldas.cs:126-196`).
- La activación está en `OperacionDirector.Mansion.cs:297-314`: con nivel == 2 y `dTerminal < 3.6`, `OperacionTerminal.EToque()` → `LiberarMilicianos()` (L528-557).
- `G.PuertaDeCeldas` es una constante `Vector3(306.4, 13, 276.6)` (`MansionGeometria.cs:115`). La «puerta» real es el grupo `P2_Reja` (`Ed\OperacionBuilder.Mansion.cs:384-392`, unos 40 barrotes).
- `AbrirLaReja()` → `HundirLaReja()` (1,3 s, `Mansion.cs:506-526`) **ya existe**.
- Son 2 milicianos (`MandoTactico.CantidadDeMilicianos = 2`).
- Avisos: `Logro`, `ObjetivoCumplido` y `AlertQueue` «2 MILICIANOS LIBERADOS».
- `CineCorta(duracion, pos, mira, cadaCuadro, linea, fov)` (`Mansion.cs:586-625`) bloquea la IA, el driver y el rig, apaga los canvas y **vacía `AlertQueue`**. Al terminar devuelve todo.
- `CinematicaDeLasCeldas` (6,5 s, L632-646) es la cinemática de **llegada** al 2.º piso. No hay que confundirla.

**Cambio** (`Mansion.cs`)
1. `LiberarMilicianos(bool conCinematica = false)`.
   - La terminal lo llama con `true`.
   - `SaltarALaAzotea` y F10 sin cinemática.
2. Corrutina `CinematicaDeLiberacion()`:
   1. `AbrirLaReja()` en t=0.
   2. Sonido `ObjetivoCumplido`, o uno nuevo `RejaAbriendo` al final de `SfxKind` (metal + motor, por `SfxSintetico`).
   3. `yield return CineCorta(2f, u => Vector3.Lerp(c0, c1, SmoothStep(u)), u => Vector3.Lerp(m0, m1, u), null, null, 55f)`:
      - c0 = (312; Nivel(2)+2,6; 273,5)
      - c1 = (309; Nivel(2)+2,2; 275,5)
      - m0 = (306; Nivel(2)+1,3; 281)
      - m1 = (302; Nivel(2)+1,1; 282)
      - Son los mismos encuadres que `CinematicaDeLasCeldas`.
      - Validar la línea de vista con `Physics.Linecast` contra la mesa (`Mesa_Colision`) y el monitor. Si choca, usar c1 como punto fijo.
   4. **Después** del `CineCorta`, porque al empezar vacía la cola: el resto de `LiberarMilicianos` (estado, órdenes de seguir, `AlertQueue.Push("N MILICIANOS LIBERADOS")`, `Aviso` y sonidos).
   5. Marcadores: `WorldUiDirector.Revelar(s, 8f)` y un anillo verde pasajero de 3 s a los pies (`ImpactFx.SpawnShockwaveRing` en verde).
3. Proteger la corrutina: `SaltarALaAzotea` hace `StopAllCoroutines`. Si `CineDeMansionEnCurso`, cerrar la cinemática por el camino de restauración (método `TerminarCineCorta()` extraído del final de `CineCorta`).

**Medida**
- **`Bug965a`** (con mansión):
  - `SaltarAlSegundoPisoPorPrueba()` → jugador en `PosDeUsoDeLaTerminal` → `OperacionTerminal.PruebaToqueE = true`.
  - Duración de `CineDeMansionEnCurso == true` entre 1,8 y 2,2 s.
  - `PlayerInputDriver.Activo.enabled` en false durante y en true después.
  - `RejaAbierta` y barrotes inactivos a los 1,5 s.
  - `MilicianosLiberados == 2`.
  - `AlertQueue` contiene «2 MILICIANOS LIBERADOS» **después** de la cinemática.
  - Los dos milicianos siguen al líder (distancia decreciente a los 6 s).
- Arnés: `celdas_0..3` (solo `FotoJuego`; `Foto()` congela el tiempo y detendría la cinemática) y el dato `cine_s`.

**Riesgos**
- `Bug407u` (`ChecksBugs065.Azotea.cs:68-147`) espera 4 milicianos (`total >= 4`, `afuera >= 3`): ya estaba desactualizado. Va a T19.
- `Bug602d`, `Bug420a` y `Nov08.cs:379/455` llaman la terminal: verificar.

**Archivos:** `Operacion\OperacionDirector.Mansion.cs` (lo comparte T7 opcional), `Presentation\GenericSfx.cs` y `SfxSintetico.cs` (si se agrega el sonido; los comparte T1).

**Depende de:** T1 por `GenericSfx` (agregar al enum en orden). Por lo demás, nada.

---

### IA

#### T7 · Perros en manada sin solaparse (L1-8)

**Hoy**
- `S\Operacion\PerroEnemigo.cs`: ruta con `NavMeshPathCache` (L404-443) y movimiento con `soldado.Motor.Move` (L199-209).
- `Brain.enabled = false` y `Anulado = true` (L73).
- Collider de radio 0,36. Carga a 11 m/s.
- `BuscarBlanco()` elige al jugador más cercano y todos van a `Blanco.transform.position` exacto (L248).
- El salto cae en `blanco - dir*0,9` (L281).
- `NavService.BlocksMovement` ignora a los `Soldier` (`S\Core\NavService.cs:188`): los perros se atraviesan.
- Dónde aparecen:
  - Ciudad: `Resistir.cs:109-142`, 7 perros.
  - Mansión: `Mansion.cs:140-172`.
  - Extracción: `Extraer.cs:35-73`, hasta 2 por tanda en el mismo cuadro, desde bocas vecinas.
- Dato aparte: el francotirador de `Resistir.cs:132` (%5) nunca se crea, porque `%5` ya lo toma el escudero en L130.

**Causa**
- No hay separación (cerebro anulado y `Deslizador` sin choque entre soldados).
- Todos van al mismo punto y siguen las mismas esquinas.

**Cambio** (todo en `PerroEnemigo.cs`)
1. `static readonly List<PerroEnemigo> Vivos` (alta en `OnEnable`, baja en `OnDisable`) y `public static bool SeparacionActiva = true`.
2. `TickSeparacion(dt)` en `Update`, salvo en Salto y Muerto:
   - Vecinos perro a menos de 1,0 m.
   - Empuje `(r-d)/r × 1,8` con `Motor.Move(empuje, dt)`.
   - Si están exactamente encima, dirección fija por `Id`.
3. **Puestos de manada por blanco**:
   - Los perros que atacan al mismo blanco se ordenan por `Id`.
   - El 0 es el líder, de frente. El resto va a ±50°, ±100° y ±150° alrededor de la dirección líder→blanco, a `AlcanceDeMordida×0,85` = 1,45 m.
   - El puesto se usa en Carga (`Avanzar(puesto)`), Salto (`saltoHasta = puesto`) y Mordida (quedarse en el puesto y no avanzar al centro).
4. `DondeIr()` con un desvío de 3-5 m por perro, con semilla `Id`.
5. Aparición: `Extraer.cs:57` con 0,4 s de retraso por perro de la misma tanda.
6. Fuera de alcance de T7 pero anotado: el francotirador muerto de `Resistir.cs:132`. Corregir el orden de los `%` si el dueño lo quiere.

**Medida**
- **`Bug967a`** (con mansión, como `Bug962a`, y con la bandera `PerrosModoAntiguo` / `Temp/modo_fotos.txt`):
  - 12 perros, jugador quieto e invulnerable, durante 20 s.
  - Cada cuadro, distancia mínima en el plano entre pares vivos que no estén saltando.
  - Criterio: ≥ 0,7 m en el 99 % de los cuadros y 0 pares a menos de 0,4 m.
  - Con `SeparacionActiva = false` el mismo check tiene que **fallar**: prueba de que la medida discrimina.
- Arnés: `perros_manada` (dato `min_p01`, `pares_lt_0_4` y `pct_cuadros_ge_0_7`) y la foto cenital.

**Riesgos**
- `Bug962a` (llegan a la plaza) puede tardar más.
- `Bug952a`, `953a` y `956a` (salto y mordida de un perro solo) no cambian.

**Archivos:** `Operacion\PerroEnemigo.cs` y, opcional, `OperacionDirector.Extraer.cs`. **Depende de:** nada.

#### T8 · Enemigos atrapados en cubículos tras la 2.ª puerta (L1-9)

**Hoy**
- La 2.ª puerta es `Porton_Blindado` (0, ·, -170).
- `AlPlantarCarga` (`OperacionDirector.Puestos.cs:149-161`) → `LanzarContraataque` → `AbrirPuertaYSalir` (L186-207) → `Activar()` (`Centro.cs:369-391`).
- Reservas: `Reserva_PuestoOeste_1..6` y `Reserva_PuestoEste_1..6` en x ±44,8/±46,6 y z -171,9 / -173,2 / -174,5.
- Las aloja el «cuartel de reserva» (`Ed\OperacionBuilder.Puestos.cs` `Bunker()` L180-196 y L235-242), que tiene una sola puerta de 2,2 m en z≈-176.

**Causa (alta confianza)**
- Dos GameObjects huérfanos `Barrera_Reserva_SoloEnemigos` en `SC_Operacion.unity`, en (±45,6; 1,7; -176,1). Tienen `BoxCollider` de 2,9×3,4×2,2, `BarreraSoloEnemigos` y `NavMeshModifierVolume` activos.
- Quedaron del commit `3212f94b`. El código que los creaba se quitó en `326e24d1`.
- `NavMeshViva` rehornea al arrancar con `PhysicsColliders` (`S\Core\NavMeshViva.cs:122`) y sella la puerta → `PathPartial`.
- `VigilarInalcanzable` (`AiBrain.Navegacion.cs:131-172`) solo rescata al bando Player.

**Cambio**
1. `Ed\OperacionBuilder.Ronda13.cs` → `QuitarBarrerasHuerfanasEscenaAbierta()`.
   - Abre `SC_Operacion` y hace `DestroyImmediate` de todo GO llamado `Barrera_Reserva_SoloEnemigos`, de cualquier GO con `BarreraSoloEnemigos` fuera de la azotea (`Azotea.cs:176-189`, que se crean en juego), y de cualquier `NavMeshModifierVolume` huérfano.
   - Informe con nombre y posición. Guardar.
2. Defensa en `S\Operacion\CazaDeEnemigos.cs` `Tick`: un cazador con `PathPartial` que no avanza 1 m en 4 s → `NavMesh.SamplePosition` hacia su meta a 3 m → `Warp`. Como máximo una vez cada 10 s por enemigo y con registro en el log.
3. `Bug501a`: sacar de la lista blanca las «islas por diseño» que eran estos cubículos. Quedan solo TV1 y TV2.

**Medida**
- **`Bug977a`**:
  - `Arrancar(2)` → esperar `NavMeshViva.Version` > 0.
  - Para todo `Soldier` enemigo con y < 5, **incluidos los inactivos**: `CalculatePath(spawn → (0,0,-160))` == `PathComplete` con los portones abiertos → 0 sin ruta.
  - Dinámica: plantar la carga (flujo de `Bug082c`) → a los 12 s, los 12 `Reserva_Puesto*` a más de 4 m de su aparición y fuera de la caja x 43,2-47,8 / z -171,5..-175,9.
- Arnés: `reserva_O` y `reserva_E` (dato `barreras`, `sin_ruta`). ANTES 2 / 12; DESPUÉS 0 / 0.

**Riesgos**
- Bajo. Correr T8 **antes** de T10 y T12, porque cambia el inventario de colliders y el NavMesh de la zona.

**Archivos:** `SC_Operacion.unity`, `Ed\OperacionBuilder.Ronda13.cs` (nuevo, también lo usan T12 y T13), `Operacion\CazaDeEnemigos.cs`, `Ed\ChecksBugs065.AuditoriaDeNivel.cs`. **Depende de:** nada (primera escena en tocarse).

---

### Nivel (geometría, rutas y entorno)

#### T9 · Calles con sentido y comienzo (L1-4, L2-1, L2-2)

**Hoy**
- Inicio: `Camino_Aproximacion` (asfalto 10×40 en (0,-359), `Ed\OperacionBuilder.cs:665-676`, prefab `P_Esc_Camino_Aproximacion`).
  - `Linea_Aprox_0..4` ya están fusionadas en `Fus_Cuarteles` (`Amarillo_x0_z-400` y `Amarillo_x0_z-360`, celda **mezclada** con las líneas del cuartel).
  - 4 farolas en (±7,-370) y (±7,-346), postes de zona y de brecha.
- Cuartel: `Camino_Cuartel` 8×120 (`Cuartel.cs:87-94`).
- Calle «de golpe»: `Calle_Salida` (`Ed\OperacionBuilder.Huida.cs:31`), asfalto de 12×50 en x -6..6 y z -40..10. Nace contra `Muro_Salida_Norte` (0,-40), sin cordón ni cruce.
  - Encima: el embudo de barricadas `ArteSalidaNorte` (`PulidoFinal.cs:336-342`) y props al azar de `ArteTramoFinal` (x -46..46, z -34..170).
- Coordenadas:

  | Lugar | Coordenadas |
  |---|---|
  | Escuadra | `Entrada_Infiltrar` (0,0,-373) |
  | Centro de datos | x ±26, z -80..-40 |
  | Patio del tanque | (0,22) |
  | Autopista | (0,40) → (262,228) |
  | Plaza | (322,228) |

- La vista cenital `Docs\DISENO_NIVEL_cenital.jpg` no tiene generador en el código.

**Causa**
- Cada zona es una `Pintura` suelta, sin piezas de unión.

**Cambio:** `Ed\OperacionBuilder.CallesR13.cs` → `CallesR13EscenaAbierta()`, grupo `R13_Calles` en la escena Cuarteles (todo z < 45).
1. **Quitar la calle del comienzo.**
   - Desactivar `Camino_Aproximacion`, las 4 `Farola` de `1a_Aproximacion` y los postes `Poste_Zona_*` y `Poste_Brecha_*`.
   - Líneas fusionadas: regenerar las celdas `Amarillo_x0_z-400` y `Amarillo_x0_z-360` sin las piezas de la aproximación. Si no se puede, apagar `z-400` y cubrir el resto de `z-360` en z < -339 con una losa `R13_TierraInicio` de `Mat("Suelo")` a y = 0,09.
   - Comienzo nuevo: terreno de tierra con huellas (decals `D_Op_*`), sacos y bidones **sin gasolina** (cajas de munición) y un muro de contención caído como cobertura de entrada. Se apoya en los campamentos `Campamento_Aproximacion_O/E` (∓38,-362) de T12 sin camión.
   - El spawn (`PlayerSquad` y `Entrada_Infiltrar`, en `SC_Operacion`) se queda en (0,-373). Solo se rota si el nuevo entorno lo pide.
2. **T tras las computadoras.**
   - Acortar `Calle_Salida` a z -40..-24 (escala z 16, posición z -32).
   - Crear `Calle_T_Transversal`: E-O, z -24..-12, x -48..48.
   - Cordones `R13_Cordon_*` de 0,15 m **sin collider** (no tocan el NavMesh).
   - Veredas de 2 m y líneas de borde.
   - Isleta o cantero central en z -12..-1, frente al patio.
   - Los brazos E y O conectan con los refuerzos que llegan por x = ±44 (`Huida.cs:83-84`).
   - Quitar o mover el embudo de barricadas y `Saco_Salida_1/2` si quedan sobre la calzada.
   - Mover los props de `ArteTramoFinal` que caigan sobre la T (lista en el informe).
3. **Plaza final con sentido:** la resuelven T15 (solado y sendas) y T16 (mobiliario). Aquí solo se verifica la continuidad calle→plaza (`Calle_Principal`, `Calle_Transversal`).
4. Portar los cambios **sin ejecutar** a `Perimetro()`, `Huida.PatioDelTanque()` y `Cuartel.cs`, para que una reconstrucción futura no los revierta.
5. Vista cenital: método nuevo `Ed\VistaCenitalR13.cs` → `Generar(string etiqueta)`, con cámara ortográfica sobre las 4 escenas → `Docs/RONDA_13/<etiqueta>/cenital_nivel.png` (molde: `MapasDeCarga.cs`).

**Medida**
- **`Bug978a`**:
  - 0 renderers activos `Camino_Aproximacion` o `Linea_Aprox*` a menos de 40 m de (0,-360).
  - Existe `Calle_T_Transversal`.
  - Toda `Pintura` de asfalto del grupo `R13_Calles` y `Calle_Salida` tiene sus bordes cortos a menos de 0,5 m de otra calle, del patio, de un portón o de un muro.
  - `CalculatePath` de (0,0,-30) a (±40,0,-18) y a (0,0,22) → `Complete`.
  - `Destino_Huida` (0,0,24) y la patrulla del jefe (±14, 12..30) quedan libres de obstáculos.
- Arnés: `inicio_cenital`, `inicio_ojo`, `salida_norte_cenital`, `salida_norte_ojo` y `ciudad_entrada`.

**Riesgos**
- Celdas fusionadas mezcladas.
- `Bug409u` y `PathInvalid` por un prop mal ubicado (ya pasó con un banco, `Docs\NOTAS_OPUS_NIVEL.md:22`).
- Si se agregan cordones con collider, hay que rehornear.

**Archivos:** `SC_Operacion_Cuarteles.unity`, `Fus_Cuarteles.prefab`, `Ed\OperacionBuilder.CallesR13.cs` (nuevo), `Ed\VistaCenitalR13.cs` (nuevo) y, sin ejecutar, `OperacionBuilder.cs`, `Huida.cs` y `PulidoFinal.cs`.

**Depende de:** T12 y T13 (misma escena; van antes por orden de guardado).

#### T10 · Bloqueos de retroceso → cartel «DEBES VOLVER A LA GUERRA» (L1-10)

**Hoy**
- Límites **físicos**, que se quedan:
  - `Valle_Oeste/Este/Sur` y `Borde_*` (`Ed\OperacionBuilder.cs:656-663`).
  - `Cierre_Norte_O/E` (`Centro.cs:32-33`).
  - Muralla de la ciudad (`CierreCiudad.cs`).
- Puertas de objetivo (se quedan): `Porton_Cuartel`, `Porton_Blindado` y `Muro_Salida_Norte`.
- **Bloqueos de progresión**:
  - (a) `Mansion_Compuerta_{torre}_{subida|bajada}` (`S\Operacion\OperacionDirector.Compuertas.cs` `CrearCompuerta` L64-81): `BoxCollider` de 4,9×4,4×0,8, `NavMeshObstacle` con carve y sin render. Muestra «NO SE PUEDE VOLVER ATRAS» (L138).
  - (b) `LimitarRetrocesoEnHuida` (`OperacionDirector.Jefe.cs:150-167`): teletransporta 14 m y muestra «DEBES AVANZAR · NO RETROCEDER».
  - (c) Las barreras de T8.
- Carteles que ya existen:
  - `TickAlejamiento` (`OperacionDirector.cs:463-487`, `AvisoUnico.FijoVibrante("volver", "VOLVE AL OBJETIVO · N m")`).
  - `S\UI\CartelVolverView.cs`: solo `MisionDirector` (nivel 1), con fuente `LegacyRuntime.ttf` (L43). El panel quedó apagado (`if (true) return;`, L115) y delega en `AvisoUnico`.
- `Ed\AuditoriaDeColliders.cs` (L23-25) cuenta como visibles los renderers **apagados**: no detecta portones, sacos ni compuertas.

**Cambio**
1. **Reutilizar `CartelVolverView`, sin crear otro cartel.**
   - Interfaz `IObjetivoParaVolver { bool Activa; float Distancia(); float Umbral(); Vector3 Punto(); }`, implementada por `MisionDirector` (lo de hoy) y `OperacionDirector` (la lógica de `TickAlejamiento`: umbral 70/80, Resistir = radio + 10).
   - `CartelVolverView.Crear(IObjetivoParaVolver)`. Lo llaman `MisionHud.cs:96` y el HUD de la Operación (`OperacionHud`).
   - Texto: `Loc.T("DEBES VOLVER A LA GUERRA")`, con la distancia debajo.
   - Volver a dibujar el panel, quitando el `if (true) return;`:
     - fondo **transparente**, alfa 0,45;
     - flecha hacia el objetivo;
     - pulso;
     - fuente `FuentesBelicas.Titulo` (T22);
     - **sin collider**, solo canvas.
   - `TickAlejamiento` deja de escribir en `AvisoUnico("volver")`: un solo cartel.
2. **Compuertas de la mansión** (D8):
   - En `Poner()`, `col.enabled = false` y `obs.enabled = false` siempre.
   - Se conservan los contadores y la lógica de pisos (`TraerAliadosAlPisoNuevo`, `RescatarAliadosDeOtroPiso`).
   - Al cruzar la de bajada, `CartelVolverView` en modo forzado durante 3 s.
3. **Huida:** `LimitarRetrocesoEnHuida` deja de teletransportar. Conserva `zMaximoDeHuida` para el cartel forzado si el jugador retrocede más de 14 m.
4. **Nivel 1** (solo si D1 lo pide): `BlockoutArtDresser.Perimetro` L1158-1190 son límites físicos y quedan.
5. Corregir `AuditoriaDeColliders`:
   - Filtrar `r.enabled`.
   - Lista blanca: `Borde`, `Valle`, `Cierre`, `Ground`, `Saco`, `Porton`, `ArbolCollider`, `Mesa_Colision`, `Muro_Salida_Norte`.
   - Salida esperada: 0 invisibles fuera de la lista.

**Medida**
- **`Bug966a`** (con mansión): en el 2.º piso, el jugador cruza la compuerta de bajada (`CalculatePath` piso 2 → piso 1 `Complete`) y el cartel queda visible (alfa > 0,5).
- **`Bug979a`**:
  - Huida, a pie: mover al jugador 30 m hacia el sur. A 1 s, desplazamiento forzado = 0 y cartel visible.
  - Operación en Puestos: alejarse 90 m durante 5 s → cartel con «DEBES VOLVER A LA GUERRA».
  - Para cada fase i: `CalculatePath(entrada[i] → entrada[i-1])` `Complete`.
- Arnés: `retroceso_huida` (dato `teletransporte_m`; ANTES ≈ 14, DESPUÉS 0), `compuerta_bajada` (dato `collider_on`) y `alejamiento` (dato `texto_cartel`).
- `colliders_despues.txt`: 0 invisibles fuera de la lista blanca.

**Riesgos**
- Subir a la mansión con enemigos detrás. Hay que revisar el rescate de aliados.
- Checks a actualizar en T19: 366, 367, 384, 406 y 413 (compuertas) y `Bug370a` (`Nov08.cs`, retroceso).

**Archivos:** `UI\CartelVolverView.cs`, `Mision\MisionHud.cs`, `Operacion\OperacionHud.cs` (T22), `Operacion\OperacionDirector.cs` (lo comparte T12), `.Compuertas.cs`, `.Jefe.cs`, `Ed\AuditoriaDeColliders.cs`. Sin escenas.

**Depende de:** T8 (inventario limpio) y T22 (fuente).

#### T11 · Solapes de geometría y objetos duplicados (L1-7, L2-3, L2-8)

**Hoy**
- `Ed\DetectorDeSolapes.cs`: caras coplanares, solo mallas legibles y alineadas a un eje.
- `Presentation\LimpiezaDeDuplicados.cs`: **en juego** apaga copias exactas (`Apagados` y `CollidersApagados`).
- `Ed\PulidoNivel2Nov11.cs`: casi-duplicados a 15 cm, colliders por nombre y títulos. Informe en `Temp/pulido_nivel2.txt`.
- `Presentation\TitulosDeObjetosOcultos.cs`: apaga el renderer en juego.
- No existe ningún detector de interpenetración (`ComputePenetration` no se usa).
- Títulos que sobreviven:
  - `Rotulo_BIENVENIDOS A` (262,6,228)
  - `Rotulo_PLAZA >>` (306; 5,2; 228)
  - `Rotulo_CIUDAD >>` (232; 4,8; 206)
  - `Rotulo_AYUNTAMIENTO`
  - prefabs `P_Esc_Rotulo_*`

**Causa**
- Se limpió en juego y no en origen.
- No hay barrido de interpenetración.

**Cambio**
1. **`Ed\AuditoriaDeSolapesR13.cs` → `Informe(string etiqueta)`**, de solo lectura. Abre las 4 escenas y escribe `Temp/solapes_<etiqueta>.txt`, que se copia a `Docs/RONDA_13/`. Cuenta por categoría y por escena:

   | Categoría | Qué cuenta |
   |---|---|
   | A | Duplicados exactos, incluidos los ya apagados (lógica de `LimpiezaDeDuplicados`, sin apagar) |
   | B | Casi duplicados a 15 cm |
   | C | Coplanares (`DetectorDeSolapes` por sector, área ≥ 0,5 m², distinto material o malla) |
   | D | Interpenetración (ver abajo) |
   | E | Prop sin collider con ≥ 40 % de su AABB dentro de un collider de edificio |
   | F | Flotantes o enterrados (raycast desde `bounds.min.y`) |

   - Cajas por sector para C: Cuarteles (0,5,-170)/(130,30,440); Ruta (0,5,213)/(780,30,340); Ciudad (320,10,230)/(260,60,260).
   - Categoría D: `Physics.SyncTransforms()` → `OverlapBox(bounds)` → `ComputePenetration`, con distancia > 0,1 m y raíces de prefab distintas.
     - Excluir los pisos: `Ground`, `Losa*`, `Calle*`, `Piso*`, `Asfalto*`, `Vereda`, `R13_Solado*`.
     - Excluir los pares del mismo `ObstacleMarker`, los `Soldier`/`Vehicle` y los pares cóncavo-cóncavo.
   - Cada hallazgo con ruta jerárquica y posición, para corregirlo a mano o con lista.
2. **`Ed\CorreccionSolapesR13.cs` → `CorregirEscenaAbierta(bool aplicar)`.**
   - Modo prueba por defecto.
   - Borra los A y B en la escena o en el prefab correspondiente. Ojo: borrar dentro de un `P_Esc_*_wN` afecta a todas sus instancias, así que se informa la cantidad de instancias.
   - Mueve o borra los D y E de una lista revisada (`Docs/RONDA_13/solapes_lista_correccion.tsv`).
   - Borra **en origen** los `P_Esc_Rotulo_PUESTO_*`, `GENERADOR`, `RADIO_DE_CAMPAÑA`, `AYUNTAMIENTO`, `CARGA_*` y los de D9.
3. Una vez limpio en origen, `LimpiezaDeDuplicados` y `TitulosDeObjetosOcultos` quedan como red de seguridad y deberían contar 0.

**Medida**
- Por categoría, ANTES y DESPUÉS:
  - A = B = 0.
  - C ≤ 10 % del valor ANTES.
  - D y E: 0 fuera de la lista de falsos positivos documentada.
  - F = 0.
- **`Bug980a`** (en juego): `LimpiezaDeDuplicados.Apagados == 0`, `CollidersApagados == 0` y `TitulosDeObjetosOcultos.Ocultos == 0` a los 5 s de cargar.
- Arnés: dato global `duplicados_apagados`, `titulos_ocultos`.

**Riesgos**
- Algunos duplicados pueden tener un `ObstacleMarker` registrado por el director.
- `DestructibilidadDelNivel` y `ColisionesDeLaCiudad` clasifican por nombre.
- Hacer la corrección **al final**, después de T9 y T12-T16, para no duplicar el trabajo. El informe ANTES se hace en E2.

**Archivos:** las 4 escenas, `Prefabs\Escena\Unicos` y `Elementos`, y los nuevos `Ed\AuditoriaDeSolapesR13.cs` y `Ed\CorreccionSolapesR13.cs`.

**Depende de:** la ola de escenas (va última).

#### T12 · Camión y gasolina fuera; la 1.ª puerta solo con carga (L2-4)

**Hoy** (`S\Operacion\OperacionDirector.Infiltrar.cs`)
- Paso 1: destruir `Generador_Cuartel` (con su hijo `Tanque` de combustible y `Escape`, `HeroAssets.cs:56-97`), entre `Camion_1` (26,-233) y `Camion_2` (36,-233).
- `ApagarCuartel` (L103-112) apaga los reflectores.
- Recién ahí `cargaPorton.Habilitado = sinEnergia` (L62).
- Paso 2: `Carga_Porton_Cuartel` → `abierto` (L79) → `AbrirPorton()` → Puestos.
- Si `generador == null`, cae al camino viejo de las torretas (L79).
- Inicio: `P_Op_Campamento_Aproximacion_O.prefab` en (-38,-362), con `Camion_Cabina/_Parabrisas/_Caja/_Lona`, 6 `Camion_Rueda` y **6 `Tambor`**. Builder: `Ambiente.cs` `CampamentoMilitar` L409-445, `conCamion = true` en L370.
- Otros camiones:
  - `Camion_3` (-30; 1,4; -230), `P_Esc_Camion`, malla `Op_CamionX`.
  - `Camion_Patio` (-16; 1,4; 40), `P_Esc_Camion_Patio`, con `ObstacleMarker` y vida 700.
  - Malla procedural: `Ed\ArteDeOperacion.cs` `MallaCamion` L575-598.
- Textos viejos: aviso de `OperacionDirector.cs:314` («2 TORRETAS»), `Puestos.cs:345` y `CinematicaDeOperacion.cs:45-51/306-312` con la voz `VozDeCinematica.Torretas`.

**Cambio (código)**
1. `Infiltrar.cs`:
   - `cargaPorton.Habilitado = true` desde t=0.
   - Objetivo de un solo paso: `TituloObjetivo(1, "VOLÁ EL PORTÓN NORTE")` (L63-78).
   - `BalizaDeInfiltrar` (L28-29) apunta a la carga.
   - Aviso de L48 actualizado.
   - `abierto = cargaPorton != null ? cargaPorton.EstaVolada : <camino actual>` (L79), **sin** la rama de las torretas.
2. Revisar `SaltarA` (`OperacionDirector.cs:718`) y el guardado (L602/L693), que referencian el generador.
3. Textos de L314, `Puestos.cs:345` y la cinemática de intro: «volá el portón norte con la carga». La voz `Torretas` se quita o se regraba con `VozDeCinematica` (TTS local, si existe) o subtítulo solo.
4. Se pueden quedar como están: `Player\Demolicion.cs:21-24/290` y `PlayerInputDriver.Contextual.cs:186-191` (el caso del generador queda sin uso).

**Cambio (escena)**, en `OperacionBuilder.Ronda13.cs` → `SinCamionNiGasolinaEscenaAbierta()`:
- Desactivar `Generador_Cuartel`, `Camion_1` y `Camion_2` en `SC_Operacion` y poner `dir.generador = null` (`SerializedObject`).
- Prefab del campamento O: quitar `Camion_*` y `Tambor*`. Rellenar el hueco con 3 cajas de munición y una lona en el piso, para que no quede vacío.
- Portar a `CampamentoMilitar`: `conCamion = false` y sin tambores.
- Los camiones que quedan (`Camion_3` y `Camion_Patio`) reciben el modelo nuevo de T16. T12 **no** los borra.
- `Camion_Patio` es cobertura del obj. 4: se queda.

**Medida**
- **`Bug981a`**:
  - `Arrancar(1)` → 0 renderers activos `^(Camion|Tambor|Generador)` en z < -200 o a menos de 60 m del spawn.
  - `cargaPorton.Habilitado == true` en t=0.
  - Plantar con el Asalto (flujo de `Bug318u`) → al terminar la mecha, `Fase == Puestos` y `Porton_Cuartel` inactivo.
- Arnés: `inicio_cenital` (dato `camion_tambor_60m`), `inicio_hud` (dato `objetivo1`, `carga_habilitada`) y `porton1`.

**Riesgos**
- Sin generador no hay apagón: los reflectores quedan prendidos (se pueden romper a tiros).
- Muchos checks dependen del generador. Van a T19: `Bug303a`, `304a`, `305u`-`308u`, `318u`, `078e`, `501b`/`d`/`e`, `603d`, `710a`, `298v` y `302a`.

**Archivos:** `SC_Operacion.unity`, `P_Op_Campamento_Aproximacion_O.prefab`, `Operacion\OperacionDirector.Infiltrar.cs`, `OperacionDirector.cs` (lo comparte T10), `.Puestos.cs`, `CinematicaDeOperacion.cs` (T22), `Ed\OperacionBuilder.Ambiente.cs` (sin ejecutar) y `Ed\OperacionBuilder.Ronda13.cs`.

**Depende de:** T8 (misma escena binaria, se guarda después).

#### T13 · Cuadro gris del objetivo 4 (L2-7)

**Hoy**
- El objetivo 4 es la fase `Huir`, en el patio del tanque (0,0,22).
- Hipótesis, de más a menos probable:
  1. **`Plaza_Cemento`**: `P_Esc_Plaza_Cemento.prefab`, cubo built-in de 30×0,04×30 en (0; 0,112; 22), `M_Op_CementoOsc` (0,46/0,47/0,50), sin collider, debajo del tanque (`Huida.cs:32`).
  2. Fondo de la barra de vida del tanque, `HudDeTanqueSimple.cs:61`: gris claro (0,88) desde el 13/11.
  3. `Muro_Salida_Norte`: caja lisa de 8×5×1,5, material `Datos`.
  4. Regresión del `RomboTanque` gris (#417, `Vehicle.PintarElRombo`).
  5. `Caja_Patio`, si se le apaga el arte.

**Cambio**
1. `OperacionBuilder.Ronda13.cs` → `PatioSinCuadroGrisEscenaAbierta()`:
   - Desactivar la instancia `Plaza_Cemento`, o reemplazarla por un solado de hormigón con juntas: textura `Hormigon` de dos argumentos, con UV en metros (`Cubo` + `MallaDeCaja`) y marcas de neumático.
   - Quitar la línea 32 del builder (sin ejecutarlo).
2. `HudDeTanqueSimple.cs:61`: fondo de la barra oscuro y translúcido (0,08; 0,08; 0,08; 0,65) con borde. El texto negro del 13/11 pasa a blanco con sombra, para que se lea.
3. `Muro_Salida_Norte`: si el dueño lo confirma, vestirlo con `ArteSalidaNorte`, que ya tiene pilares y dintel.
4. Si la foto ANTES muestra otra cosa (rombo o caja), corregir eso y anotarlo.

**Medida**
- **`Bug982a`** en `Arrancar(4)`:
  - Renderers activos a menos de 40 m de (0,0,22) con malla `Cube` built-in y material `CementoOsc` o por defecto, y área de planta mayor que 4 m² → 0.
  - `Image` activas sin sprite, grises (r≈g≈b entre 0,3 y 0,9) y de más de 60×20 px en el HUD durante la carrera → 0.
- Arnés: `patio_tanque` (dato `cubos_grises_grandes`) y `tanque_hud_barra` (FotoJuego).

**Archivos:** `SC_Operacion_Cuarteles.unity`, `P_Esc_Plaza_Cemento.prefab`, `UI\HudDeTanqueSimple.cs` (lo comparten T4 y T22), `Ed\OperacionBuilder.Ronda13.cs` y `Huida.cs` (sin ejecutar).

**Depende de:** T12 (orden de guardado en Cuarteles). Para el HUD: T4 y T22 (mismo archivo).

#### T14 · Ambientación de desierto (L2-6)

**Hoy**
- `Ambiente()` (`Ed\OperacionBuilder.Ambiente.cs:260-376`): losa de arena 776×332, 170 cactus, 90 rocas, 220 arbustos secos, 560 matas y 2 campamentos.
- `ArteExtra` (`Retoque.cs:129-159`): 46 arboledas de 3-6 plantas.
- Sin partículas de ambiente, sin arbustos rodantes y sin `Terrain`.
- Nivel 1:
  - Árboles como `TreeInstance` (`BlockoutArtDresser.cs:599-607`).
  - Fondo de 6000 árboles sin collider (`FondoDelMundoBuilder.cs:27-99`).
  - Partículas de fuego y luciérnagas en `AmbientacionDelNivel.cs:430-510/656-690`.
- Sin LODGroup ni culling por capas.
- `MetricasDeBuild` mide solo `SC_Gameplay`.
- **No volver a correr `Ambiente()`**: `Terreno_Ambiente` está en las 4 escenas.

**Cambio**
1. Medir **antes**: toma `perf_ruta` en E1. Es el presupuesto de referencia.
2. `Ed\AmbienteDesiertoR13.cs` → `DesiertoR13EscenaAbierta()`, en la escena **Ruta**:
   - `Terrain` plano `Terreno_ArbolesDesierto`: solo árboles, sin `TerrainCollider`, con `NavMeshModifier ignoreFromBuild` y a y = -0,12.
   - `TreePrototype` `P_Env_ArbolA/B` y un arbusto: entre 600 y 1500 instancias.
   - Exclusiones: a menos de 24 m de la polilínea `5_Autopista/RutaTanque`, la ciudad (x ≥ 190, z ≥ 100) y el río (x -160..30, z > 296).
   - Rocas y cactus en los huecos del bucle de la ruta (informe 396-414) con `LugarLibre`.
3. Script de juego `S\Operacion\ArbustoRodante.cs`:
   - Cinemático, sin Rigidbody ni collider.
   - Pool de 10, que reaparecen a 30-60 m de la cámara, solo en el sector Ruta.
   - Malla `Op_MataSeca`. El objeto se llama `ArbustoRodante_<n>`.
4. `S\Operacion\PolvoDelDesierto.cs`:
   - `ParticleSystem` en espacio mundo que sigue a la cámara, con un máximo de 150 partículas, alfa 0,15 y viento constante.
   - 3 o 4 remolinos fijos `RemolinoDePolvo_<n>`.
   - Molde: `AmbientacionDelNivel.cs:656-690` y `ParticleMaterialFactory`.
5. Los dos los crea `OperacionDirector.AmbientarLaRuta` (`OperacionDirector.cs:413-421`).
6. No usar los nombres que apagan `AjustesDeNivelNov08` #375 (faroles y luminarias chicas) ni `AjustesDeNivelOpus` (`P_Env_*` chicos sobre la ruta).

**Medida**
- Rendimiento de `perf_ruta`, ANTES y DESPUÉS: fps promedio ≥ 60, p95 ≤ 20 ms y caída máxima de 10 % respecto de ANTES.
- `perf_plaza` y `perf_inicio` sin empeorar más de 5 %.
- **`Bug983a`**: árboles de terreno entre 600 y 1500, rodantes activos entre 8 y 12 en el sector Ruta, emisor de polvo presente y 0 árboles a menos de 24 m de la ruta del tanque.
- Arnés: `desierto_1..3`, `perf_ruta`, `perf_plaza` y `perf_inicio`.

**Riesgos**
- `Vegetacion.cs` (nivel 1) usa árboles de terreno para bloquear disparos: confirmar que solo corre con `Terrain_Main`.
- Consumo de GPU por las partículas en pantalla completa.

**Archivos:** `SC_Operacion_Ruta.unity`, `Ed\AmbienteDesiertoR13.cs` (nuevo), `Operacion\ArbustoRodante.cs` y `PolvoDelDesierto.cs` (nuevos) y `OperacionDirector.cs` (una línea; lo comparten T10 y T12).

**Depende de:** nada en escena (Ruta no la toca nadie más). El arnés de perf es de T0.

#### T15 · Plaza: solado realista en lugar del damero (L2-9)

**Hoy**
- 100 cubos `Adoquin` de 3,2 m en `P_Op_Ciudad_PlazaCivica.prefab` (`Ed\OperacionBuilder.CiudadIntegral.cs:180-182`), con materiales `AdoquinA` (0,62) y `AdoquinB` (0,50).
- Otro damero `Patio_Losa` en 4 patios (`Caminos2.cs:47`).
- `Mat()` → `AplicarTextura` → celda plana del trimsheet (`Arte.cs:93-118`): **no hay textura**.
- Las texturas `T_Op_*` solo se aplican con el método de dos argumentos (L120-141).
- Alturas a respetar:

  | Pieza | Altura (m) |
  |---|---|
  | Asfalto | 0-0,08 |
  | Rosetón | 0,23-0,30 |
  | `Incrustacion_*` | 0,23-0,26 |
  | LED | 0,23-0,30 |
  | Decals del juego | 0,075-0,09 (quedarían tapados) |

**Cambio:** `Ed\OperacionBuilder.SoladoR13.cs` → `SoladoDePlazaR13()`, editando el **prefab asset**.
1. Texturas en `TexturaProcedural` (`Arte.cs:218-369`, estilo `Ladrillo` L245-256):
   - `Granito`: losas de 0,6×0,4 trabadas con veta.
   - `Adoquin`: 0,2×0,1, en espina de pez.
   - `Vereda`: baldosa porteña de 9 panes, 0,2 m.
   - **Normal map** derivado de la misma altura (Sobel) y smoothness por material: PBR mínimo que pide el criterio C de `VALIDACION_PARCIAL2.md`.
2. Materiales:
   - `M_Op_Plaza_Granito`, `M_Op_Plaza_Guarda` (granito oscuro), `M_Op_Senda_Cebra` y `M_Op_Vereda`, con `AplicarTextura(mat, clave)` de dos argumentos.
   - `EspecDeTextura` en metros (`especDeMaterial` L20-53).
3. Geometría con UV en metros (`Cubo`/`MallaDeCaja`), sin collider y con `ignoreFromBuild`, grupo `R13_Solado`:
   - 4 cuadrantes de 16×16 con la cara superior en y = 0,23.
   - Guardas de 0,6 m en cruz y perímetro, coherentes con las `Incrustacion` doradas.
   - Cordón perimetral.
   - **Sendas peatonales** en las 4 bocas (x 304..306 / 338..340 y z 210..212 / 244..246).
   - Borrar los 100 `Adoquin`.
4. Decals que quedan tapados: subirlos a y = 0,24 en la zona de la plaza, o aceptarlo y anotarlo.
5. Repetir en los 4 patios de `Caminos2` (prefabs `P_Op_Cam_*`).
6. Portar sin ejecutar a `ArmarPlazaCivica` y `Caminos2`.
7. Referencias: fotos públicas de Lavalle y de plazas porteñas, **solo como referencia visual**, sin descargar ni usar ninguna imagen (criterio A de `VALIDACION_PARCIAL2.md`). Anotar las URLs consultadas en `Docs/RONDA_13/referencias_plaza.md`.

**Medida**
- **`Bug984a`**:
  - 0 renderers activos `Adoquin` y `Patio_Losa`.
  - Renderers en la caja de la plaza: de unos 100 a ≤ 12.
  - Los materiales del solado tienen `_MainTex` y `_BumpMap` distintos de null.
- Arnés: `plaza_cenital` y `plaza_ojo`, con el dato `adoquines` y `renderers_caja`.

**Riesgos**
- Si `ConstruirCiudadIntegral` se vuelve a correr, recrea el damero: de ahí el punto 6.
- `P_Mod_Piso_*` del kit trae juntas oscuras. No usarlo.

**Archivos:** `P_Op_Ciudad_PlazaCivica.prefab` (lo comparte T16), `P_Op_Cam_*`, `Materials\Operacion`, `Textures\Operacion`, `Ed\OperacionBuilder.Arte.cs` (lo comparte T16) y `Ed\OperacionBuilder.SoladoR13.cs` (nuevo).

**Depende de:** nada. **Antes de** T16.

#### T16 · Assets menos primitivos (L2-11)

**Hoy**
- Piezas primitivas que se ven de cerca:
  - `ArmarAuto` (2 cubos) y `ArmarArbol` (cilindro + esfera), en `CiudadIntegral.cs:147-173`.
  - Bancos, contenedores, refugios, hidrantes y semáforos (L195-261).
  - `Callejon_Cajon`, `Feria_Puesto` y `Arco_*` (`Caminos.cs`).
  - `Camion_Patio`, `Caja_Patio` y cocheras (`Huida.cs`).
  - `P_Esc_Auto_Abandonado_0..3`.
- El kit `Prefabs\ArteMundo\` (`P_Env_ArbolA/B`, `Auto_Quemado`…) ya existe.

**Cambio:** `Ed\OperacionBuilder.DetalleR13.cs`, editando los prefab assets (`P_Op_Ciudad_Mobiliario`, `P_Op_Ciudad_PlazaCivica`, `P_Op_Cam_*`, `P_Esc_Camion*`):
- Se conserva el GO raíz con su **nombre y collider**: la cobertura y el NavMesh no cambian.
- Se agregan solo hijos visuales `Detalle_R13`, sin collider, combinados en una malla por prop con `Lote`/`PonerLote` (`ArteDeOperacion.Pasada2`).
- Detalle por tipo:
  - **Auto:** capó y techo escalonados, 4 ruedas, faros y luces traseras emisivos, parabrisas oscuro y paragolpes.
  - **Árbol:** usar `P_Env_ArbolA/B`.
  - **Banco:** listones y patas.
  - **Contenedor:** tapa y ruedas.
  - **Camión** (modelo nuevo para `Camion_3` y `Camion_Patio`, pedido en T12): cabina con parabrisas, caja con lona, 6 ruedas, paragolpes y faros. Se rehace `MallaCamion` (`ArteDeOperacion.cs:575-598`) con UV en metros y normal map procedural.

**Medida**
- **`Bug985a`**:
  - Cada prop tocado tiene ≥ 5 piezas visuales o ≥ 200 triángulos.
  - El collider raíz no cambia (bounds iguales ±1 cm).
  - Renderers activos en la caja de la ciudad (x 262..380, z 162..296): como máximo 10 % más que ANTES.
- Arnés: `plaza_mobiliario`, `camion_patio_cerca` y el dato `renderers_ciudad`.

**Riesgos:** hay scripts que dependen de nombres:
- `PulidoNivel2Nov11.NombresConCuerpo`
- `ColisionesDeLaCiudad`
- `DestructibilidadDelNivel` (checks 402a y 712a)
- `DespejarServicio` (`Caminos.cs:305-317`)
- `CentrarFuente`

**Archivos:** los prefabs de arriba, `Ed\ArteDeOperacion.cs` y `Ed\OperacionBuilder.DetalleR13.cs` (nuevo).

**Depende de:** T15 (mismo prefab y `Arte.cs`) y T12 (decide qué camiones quedan).

#### T17 · Pulido general del nivel 2 (L2-3)

**Hoy**
- `Bug501g` (`ChecksBugs065.AuditoriaDeNivel.cs:311-367`) mide tres cosas:
  1. collider desalineado del arte;
  2. collider invisible sin malla;
  3. sólido visible sin collider.
- Falsos positivos conocidos: sacos, `Reflector_Baranda_*` y remaches del portón.

**Cambio**
1. Línea base de 501g en E2.
2. `Ed\PulidoNivel2R13.cs`:
   - Lista nueva de nombres sin collider (patrón `PulidoNivel2Nov11.Colliders`), pero **guardando en la escena**.
   - Corrige los «fuera de lugar» del informe F de T11 (flotantes o enterrados) y los colliders desalineados (n1).
3. Los falsos positivos se documentan en una lista blanca **dentro** de 501g, para que el número tenga sentido.

**Medida**
- `Bug501g`: n2 = 0; n1 y n3 ≤ la lista blanca.
- **`Bug986a`**: ningún renderer `Detalle_R13` o `R13_*` con collider (no deben agregar choque).

**Archivos:** escenas (las 4) y `Ed\PulidoNivel2R13.cs`. **Depende de:** T11.

---

### Parcial 2, regresión y entrega

#### T18 · Auditoría contra los criterios del Parcial 2 (antes bloqueado)

Fuente: `Docs/RONDA_13/VALIDACION_PARCIAL2.md`. Este trabajo cierra cada fila de esa tabla con evidencia.

**A · Desaprobación automática**
- Sin bugs bloqueantes → T24.
- Sin placeholders:
  - T13, T15 y T16.
  - Revisar qué muestran `ImpactFx`, `SpriteFx`, `DecalPool` y `MusicDirector` cuando dicen «placeholder» (grep y captura). Si es un fallback visible, reemplazarlo.
  - `SC_TestLevel` está en la lista de build (`EditorBuildSettings`, índice 2) porque la usa la suite. Verificar que **ningún** código de juego la carga (hoy solo aparece en comentarios) y que el menú no llega a ella. No sacarla de la lista sin probar la suite: en Play, `SceneManager.LoadScene` la necesita en la lista.
- Sin assets bajados de internet ni hechos con IA → T23.

**B · Entregables** → T21.

**C · Por bloque**
- Dirección de arte y luz: fotos de T14-T16 más una toma nocturna de lectura (`plaza_ojo`). Verificar que el emisivo resalta con `PostFxDirector`.
- Texturizado y PBR: normal y smoothness en el solado y el camión (T15 y T16).
- Fuente personalizada → T22.
- Personajes: revisar rig y skin con la captura `pesado_comparacion` (ya existe).
- Cinemáticas: T6.

**Medida:** cada fila de `VALIDACION_PARCIAL2.md` con estado final OK, o RIESGO con nota del dueño.

**Archivos:** solo documentos, salvo lo que derive a T13, T15, T16 y T21-T24.

#### T19 · Checks y regresión

1. **Actualizar** (la lista completa está en §8.2).
2. `HeadlessTestRunner.RestablecerEstaticosDeJuego`: `AjustesDeDano.ForzarParaPrueba(1, 0.5f)`. Que la suite no dependa de los PlayerPrefs.
3. Lote completo en E2 y al final.
   - Criterio: **0 fallos nuevos** respecto de `checks_base.txt`.
   - Los fallos que ya existían se listan aparte y no se esconden.
4. `Bug602e` se corre con el Editor **con ventana**.

**Depende de:** todos los trabajos de código.

#### T20 · Build, RAR y commit (solo con orden)

1. Build: `-batchmode -executeMethod SP.EditorTools.CliBuilder.BuildWindows64` → `Builds/Windows64`.
   - **No** usar `Assets/Editor/BuildAndPack.cs`: es del TP1 y genera `TP1_INOFUENTES.rar`.
2. RAR:
   - `"C:\Program Files\WinRAR\Rar.exe" a -r -ep1 Builds\StrategicPoint.rar Builds\Windows64\* Docs\RONDA_13\entrega\INTEGRANTES.txt Docs\RONDA_13\entrega\ATAJOS_DE_TESTEO.txt`.
   - Excluir `*_BurstDebugInformation_DoNotShip*` con `-x`.
3. Prueba del RAR: extraer a una carpeta temporal y correr `StrategicPoint.exe -capturas <tmp> -tomas inicio_hud`. Debe aparecer el PNG y `FIN`.
4. Commit con orden del dueño. Separar en tres commits: (a) arnés T0, (b) juego, (c) checks y documentos.

#### T21 · `INTEGRANTES.txt` y `ATAJOS_DE_TESTEO.txt` (nuevo)

- **`ATAJOS_DE_TESTEO.txt`:** se genera leyendo `S\Player\KeyBindings.cs`, `S\Operacion\MenuDeTrampas.cs` y `S\Core\ModoDios.cs`. Ejemplos: F4 modo dios, F10 saltar a la azotea, saltos de objetivo.
  - Formato: tecla, efecto, dónde funciona.
  - Aclaración: «la partida se juega completa sin estos atajos».
  - Se hace con un método de editor `Ed\AtajosDeTesteoR13.Generar()`, que recorre los atajos registrados si hay un registro central. Si no lo hay, se escribe a mano a partir del código, y cada atajo con su `archivo:línea` en un comentario del propio txt.
- **`INTEGRANTES.txt`:** nombres y roles. **Bloqueado por D12.**
- Ubicación: `Docs/RONDA_13/entrega/`. Se copian al RAR en T20.

#### T22 · Unificar fuentes del gameplay en `FuentesBelicas` (nuevo)

**Hoy**
- `S\Presentation\FuentesBelicas.cs` ya cambia la fuente de todo `Text` y `TextMesh` al cargar una escena y cada **2,5 s** (para los objetos activos).
- Quedan huecos:
  1. Textos creados en partida que viven menos de 2,5 s: `HeadshotPopup`, `FloatingDamageTextManager`, `CartelDeRacha`, avisos y `AvisoUnico`. Se ven con `LegacyRuntime.ttf`.
  2. Textos de IMGUI (`OnGUI`), que `FuentesBelicas` no toca: `AyudaDebugger`, `SesionLog` y `ReporteDeBug` son de depuración y no se ven en la partida normal.
  3. El parpadeo de fuente en el primer cuadro.
- Hay 110 usos de `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` en 57 archivos, 46 de ellos de runtime.

**Cambio**
1. Ayudante `FuentesBelicas.Para(int tamano)`: devuelve `Titulo` si `tamano >= UmbralTitulo` y `Texto` en el resto. Si no cargó, cae en `LegacyRuntime`. El ajuste de estilo Bold → Normal queda centralizado en `FuentesBelicas.Aplicar(Text)`.
2. Reemplazo mecánico en los **archivos de runtime**: cada `GetBuiltinResource<Font>("LegacyRuntime.ttf")` pasa a `FuentesBelicas.Para(<tamaño de ese texto>)`. Los de Editor (`HeadlessTestRunner`, `MenuSceneBuilder`, `CapturasDeUi`, `WeaponStatusUiPipeline`, `LoadingSceneBuilder`, `OperacionBuilder*`) quedan, salvo los que hornean texto en una escena del juego (`MenuSceneBuilder`, `LoadingSceneBuilder`): en esos usar la ruta de Resources.
3. Lo de depuración en IMGUI queda como está, documentado: solo aparece con el atajo de depuración.

**Medida**
- grep de `LegacyRuntime.ttf` en `S\` sin `Editor\`: 1 sola aparición, dentro de `FuentesBelicas.Para`.
- **`Bug987a`**:
  - Provocar un headshot, un número de daño, una racha, un aviso y el cartel de volver.
  - **En el mismo cuadro**, contar `Text` activos con `!FuentesBelicas.EsBelica(font)` → 0.
- Arnés: dato `fuentes_no_belicas` en cada toma `FotoJuego`.

**Riesgos**
- El reemplazo toca 46 archivos: hacerlo **primero** en la ola de código, en una sola pasada y con un solo agente, antes de que los carriles se repartan (§6).
- Puede cambiar el ancho de los textos, porque Stardos es más ancha: revisar que los textos largos no desborden. `Bug973a` cubre Configuraciones.

#### T23 · Origen del audio y de las fuentes (nuevo)

1. `Assets/_Project/Audio_GunshotCandidates/*.wav`:
   - Verificar que ningún script, prefab ni escena los referencia: grep de los GUID de los `.meta` en `Assets/**/*.{prefab,unity,asset}`.
   - Moverlos **fuera de `Assets`**, a `_FueraDeEntrega/Audio_GunshotCandidates/`, para que no viajen con el proyecto.
   - Si alguno se usa: documentar el origen o reemplazarlo por un sonido sintetizado (`SfxSintetico`).
2. Fuentes: `Resources/UI/Fuentes/BlackOpsOne-Regular` y `StardosStencil-Bold` (OFL).
   - Agregar `OFL.txt` y los autores en `Docs/RONDA_13/entrega/ORIGEN_DE_RECURSOS.txt`.
   - Plan B si la docente no las acepta (D11): fuente bitmap propia, con texturas de glifos stencil generadas por código y un `Font` personalizado. Es un trabajo de 1-2 días: **no** empezar sin respuesta.
3. Inventario: `Ed\InventarioDeRecursosR13.cs` lista todo audio, fuente, textura y modelo que entra en la build (`BuildReport` del último build o `AssetDatabase.GetDependencies` de las escenas en la lista de build), con su carpeta de origen → `Docs/RONDA_13/inventario_recursos.tsv`.

**Medida**
- 0 assets de la build con origen «descargado» sin licencia documentada.
- `Audio_GunshotCandidates` fuera de `Assets`.

#### T24 · Corrida de punta a punta de la build final (nuevo)

1. Automática en el Editor, **`Bug988a`**:
   - Recorrer las 6 fases con `SaltarA` y las costuras de prueba (`OperacionTerminal.PruebaToqueE`, cargas, `Embarcar`) hasta `Victoria`.
   - Criterio: 0 excepciones en la consola y cada fase con su condición de salida cumplida.
   - Si el recorrido completo no es practicable, dejar una cadena de checks existentes en orden y documentarlo.
2. Manual en la build: menú → (tutorial opcional) → Operación completa en Medio, **sin atajos ni modo dios** → victoria.
   - Registro de sesión `Registros/sesion_SC_Operacion_*.txt` y tiempo total.
   - La hace el dueño o un agente con dispositivos reales. Anotar quién la hizo.
3. Medida: `VALIDACION_R13.md` con la fila «punta a punta», con fecha, versión, duración, bugs encontrados y estado.

---

## 5. Matriz de conflictos (quién toca qué)

| Archivo / escena | Trabajos | Regla |
|---|---|---|
| `Combat\Health.cs` | T1, T3 | Carril A, orden T1 → T3 |
| `Combat\WeaponHolder.cs` | T1, T2 | Carril A |
| `Combat\Projectile.cs` | T2, T3 (props), T4 (origen) | A primero; T4.2 después |
| `Vehicles\Vehicle.cs` | T3, T4 | A primero; T4.2 después |
| `Core\Events.cs` | T4 | — |
| `Player\PlayerInputDriver.cs` | T5, T22 | T22 primero |
| `Player\PlayerInputDriver.Vehiculo.cs`, `Camera\CameraRig.cs` | T4 | — |
| `UI\HudDeTanqueSimple.cs` | T4, T13, T22 | T22 → T4 → T13 (mismo agente B) |
| `Presentation\GenericSfx.cs`, `SfxSintetico.cs` | T1, T6 | Enum al final, en orden T1 → T6 |
| `Presentation\AccionesEnCursoView.cs`, `CurandoAnimacion.cs`, `IndicadorDeCaidos.cs` | T5, T22 | T22 primero |
| `Tutorial\TutorialManager.cs` | T3, T4 | A (T3) → B (T4) |
| `Operacion\OperacionDirector.cs` | T10, T12, T14 | Carril D, orden T12 → T10 → T14 (una línea) |
| `Operacion\OperacionDirector.Mansion.cs` | T6, T7 (opcional) | Carril D |
| `UI\CartelVolverView.cs`, `Mision\MisionHud.cs`, `Operacion\OperacionHud.cs` | T10, T22 | T22 primero |
| `Core\CapturasDeBuild*.cs` | T0 | Nadie más lo toca después de E1 |
| `Ed\ChecksBugs065.R13_<T>.cs` | uno por trabajo | Archivos parciales nuevos, sin conflicto |
| `Ed\OperacionBuilder.Ronda13.cs` | T8, T12, T13 | Un método por trabajo; el mismo agente de escenas |
| `Ed\OperacionBuilder.Arte.cs` | T15, T16 | T15 primero |
| `SC_Operacion.unity` (binaria) | T8, T12, (T9 spawn), NavMesh | **Secuencial**: T8 → T12 → T9 → horneado |
| `SC_Operacion_Cuarteles.unity`, `Fus_Cuarteles` | T12 (campamento), T13, T9, T11, T17 | Secuencial |
| `SC_Operacion_Ruta.unity` | T14, T11, T17 | — |
| `SC_Operacion_Ciudad.unity` + `P_Op_Ciudad_*` | T15, T16, T11, T17 | T15 → T16 → T11 → T17 |

---

## 6. Orden óptimo (olas y carriles)

**Ola 0: evidencia ANTES** (un agente, secuencial; nada del juego cambia)
1. T0, arnés completo (§7).
2. Herramientas de solo lectura: `AuditoriaDeSolapesR13`, `AuditoriaDeColliders` corregido y `VistaCenitalR13`. Son editor puro y no cambian la build.
3. Build ANTES → `Builds/R13_Antes`, dos corridas de capturas y elegir.
4. E2: lote completo de checks, solapes, colliders, 501g y cenital → `Docs/RONDA_13/antes/`.

**Ola 1: código.** Se escribe en paralelo. Se **compila de a uno** en el Editor compartido, o cada carril en su worktree propio con su `Library` (lento la primera vez).
- **Paso único previo:** T22, reemplazo mecánico de fuentes (un agente, una sola pasada).
- **Carril A (combate):** T1 → T2 → T3.
- **Carril B (tanque y HUD):**
  - T4.1 (mira) y T4.3 (dibujo del anillo).
  - Después de A: T4.2 (origen en `Vehicle`/`Projectile`/`Events`).
  - Después, el HUD de T13 (barra).
- **Carril C (feedback):** T5. T6 solo si D lo cede (por `GenericSfx`, después de T1).
- **Carril D (director):** T12-código → T10 → T6 → T7 → T14-código (`ArbustoRodante`, `PolvoDelDesierto`, una línea en `AmbientarLaRuta`).
- **Carril E (herramientas de nivel, archivos nuevos):** escribir sin ejecutar:
  - `OperacionBuilder.Ronda13.cs` (T8, T12 y T13)
  - `CallesR13.cs`
  - `AmbienteDesiertoR13.cs`
  - `SoladoR13.cs`, que incluye las texturas de `Arte.cs`
  - `DetalleR13.cs`
  - `CorreccionSolapesR13.cs`
  - `PulidoNivel2R13.cs`
- **Carril F (docs):** T21 (`ATAJOS`), T23 (inventario y origen) y T18 (auditoría de la lista).
- **Checks nuevos:** cada carril escribe los suyos en `ChecksBugs065.R13_<T>.cs`.

**Ola 2: escenas** (un agente, **un solo Editor**, en este orden; correr el check del trabajo al terminar cada paso)
1. T8 en `SC_Operacion` (barreras) → `Bug977a`.
2. T12 en `SC_Operacion` y el prefab del campamento → `Bug981a`.
3. T13 en Cuarteles (`Plaza_Cemento`) → `Bug982a`.
4. T9 en Cuarteles → `Bug978a`.
5. T14 en Ruta → `Bug983a`.
6. T15 en el prefab de la plaza y los patios → `Bug984a`.
7. T16 en prefabs y camión → `Bug985a`.
8. T11, corrección: lista revisada, modo prueba y después aplicar → `Bug980a` + `solapes_despues`.
9. T17 → 501g y `Bug986a`.
10. **Rehorneado único** del NavMesh (`OperacionBuilder.HornearNavMeshEscenaAbierta`, con las 4 escenas abiertas) → `Bug501a`, `Bug409u`, `Bug977a` y `Bug978a`.

**Ola 3: cierre**
1. T19: actualizar los checks y correr el lote completo. 0 fallos nuevos.
2. Build DESPUÉS, capturas, atlas, `VALIDACION_R13.md` y cenital DESPUÉS.
3. T24 (automática y manual) y T18 (cerrar la tabla).
4. T20 (RAR y prueba del RAR). Commits solo con orden.

**Ruta crítica:** T0 → build ANTES → T22 → carril A → T4.2 → Ola 2 → T19 → build DESPUÉS → T24.

**Se puede paralelizar sin conflicto:** los carriles A, C, D, E y F (archivos disjuntos, §5) y la parte 1/3 de B.

---

## 7. Arnés `-capturas`: lista exacta de tomas (ANTES = DESPUÉS)

### 7.1 Condiciones comunes
- Comando: `StrategicPoint.exe -capturas <dir> -screen-width 1600 -screen-height 900 -screen-fullscreen 0 [-tomas id,id]`.
- Al inicio: `PartidaGuardada.ModoPrueba = true`, `Random.InitState(1313)` y saltear las cinemáticas.
- `ModoDios` **encendido** para las tomas de mundo y **apagado** (`SinDios`) para las de daño, con el jugador invulnerable.
- Dificultad Medio, salvo donde se indica.
- `Foto(id, pos, mira, fov, congelar)` usa cámara propia, sin HUD ni marcadores. `FotoJuego(id)` es la pantalla real con HUD.
- Cada toma escribe `== <id>` y líneas `dato <id>.<clave>=<valor>`. Global: `version`, `resolucion`, `dificultad`, `duplicados_apagados`, `colliders_apagados`, `titulos_ocultos` y `FIN <s>`.
- En cada `FotoJuego`, también `dato <id>.fuentes_no_belicas`.

### 7.2 Tomas

`G2` = `G.Nivel(2)` = 13 m.

| # | id | Trabajo | Preparación | Cámara (pos → mira, FOV) o tipo | ANTES | DESPUÉS | Datos |
|---|---|---|---|---|---|---|---|
| 1 | `inicio_cenital` | T9, T12 | `Saltar(1)`, 3 s | Foto (0,95,-345) → (0,0,-345), 60, arriba = +z | Franja de asfalto 10×40, farolas, campamento O con camión y tambores | Sin calle; tierra, coberturas; campamento sin camión | `render_aprox`, `camion_tambor_60m` |
| 2 | `inicio_ojo` | T9 | idem | Foto (0; 1,7; -378) → (0; 1,5; -340), 60 | Calle recta hacia el cuartel | Entrada natural al cuartel | — |
| 3 | `inicio_hud` | T12, T22 | idem, 1 s | FotoJuego | Objetivo 1 con generador | «VOLÁ EL PORTÓN NORTE» | `objetivo1`, `carga_habilitada`, `fuentes_no_belicas` |
| 4 | `porton1` | T12 | idem | Foto (14,5,-238) → (0,2,-222), 55 | Generador + `Camion_1/2` | Portón y carga | `generador_activo`, `camiones_activos` |
| 5 | `reserva_O` | T8 | `Saltar(2)`, 3 s | Foto (-45,6; 5; -186) → (-45,6; 1,2; -175), 55 | Puerta del cubículo | idem | `barreras`, `sin_ruta` (de 12) |
| 6 | `reserva_E` | T8 | idem | Foto (45,6; 5; -186) → (45,6; 1,2; -175), 55 | idem | idem | idem |
| 7 | `salida_norte_cenital` | T9 | `Saltar(4)`, 3 s | Foto (0,80,-12) → (0,0,-12), 60, arriba = +z | `Calle_Salida` recta contra el muro | T con cordones, veredas e isleta | `calle_t` (existe sí/no) |
| 8 | `salida_norte_ojo` | T9 | idem | Foto (0; 1,7; -37) → (0; 1,2; 0), 65 | Calle «de golpe» | Cruce legible | — |
| 9 | `patio_tanque` | T13 | idem | Foto (0,16,-6) → (0,0,22), 60 | Losa gris de 30×30 | Hormigón con juntas o sin losa | `cubos_grises_grandes` |
| 10 | `tanque_mira_inicial` | T4 | `SaltarA(Orden[3],true,1)` → `Embarcar()` → esperar `Gunner` (≤ 12 s) → 1,0 s | FotoJuego | Tercera persona | Periscopio con mirilla de artillero | `ads`, `zoom`, `fov_1s`, `fov_2s` |
| 11 | `tanque_hud_barra` | T13 | idem, a los 2 s | FotoJuego | Barra gris claro | Barra oscura translúcida | `img_grises_grandes` |
| 12 | `tanque_impacto_izq` | T4 | idem. `ProjectilePool.Activo.Spawn` desde tanque − camDer×25 + up×2 hacia el tanque, equipo Enemy, id -3, daño 30, radio 0; esperar 0,25 s | FotoJuego | Solo viñeta y sacudida | Marca roja a ≈ -90° | `marcas` (ángulos, por reflexión `AngulosActivos`; si no existe, «n/a») |
| 13 | `tanque_impacto_atras` | T4 | idem, desde tanque − camFwd×25 | FotoJuego | idem | Marca a ≈ 180° | idem |
| 14 | `revivir_40` | T5 | `Saltar(2)`, `SinDios`. Bajar a un aliado no médico (`TakeDamage(100000,-1)`) → jugador a 1,2 m mirándolo → `KeyBindings.ForzarInicioDePulsacion(Interactuar, 6)` → 2,0 s | FotoJuego | ≥ 6 elementos | Un solo engranaje al 40 % en la mira | `feedback_total`, `feedback_lista`, `engranaje_fill` |
| 15 | `revivir_medico` | T5 | idem, poseyendo a Doc → 1,0 s | FotoJuego | 2 círculos y más | Un engranaje al 40 % | idem |
| 16 | `cuchillo_soldado_0` | T1 | `Saltar(2)`, `SinDios`, Medio. Clon enemigo a 1,6 m delante, de espaldas → `TryMelee` por reflexión → 0,12 s | Foto(congelar=false) yo + der×1,5 + up×1,6 → pecho de la víctima, 55 | Chispa roja chica | Ráfaga de sangre y destello | `vida_antes`, `vida_despues`, `vivo`, `particulas` |
| 17 | `cuchillo_soldado_1` | T1 | idem, + 0,6 s | Foto, la misma cámara | Nada en el piso | Mancha de sangre | `decal_sangre` |
| 18 | `cuchillo_pesado` | T1 | Clon + `EnemigoPesado.Aplicar`, 0,3 s → `TryMelee` ×2 separados 0,6 s | Foto tras el 2.º golpe, 55 | Vivo, con 1-2 % menos de vida | Muerto | `vida_tras_1`, `vida_tras_2`, `vivo` |
| 19 | `heli_cohetes` | T2 | `SaltarALaAzotea()` → esperar `HeliDeLaAzotea` vulnerable (≤ 40 s) → `SinDios` → por dificultad (F/M/D): restaurar vida y disparar hasta 12 cohetes cada 1,3 s a nariz, centro y cola (camino del arma real, ver T2), con adelanto de puntería | Foto(congelar=false) al primer impacto: jugador + up×2 → heli, 50 | No muere con 12 | Muere con 4/4/5 | `cohetes_hasta_morir.<dif>.<punto>`, `dano_por_cohete` |
| 20 | `ajustes_dano_1280` | T3 | `Resolucion(1280,720)` → abrir pausa y Configuraciones (reflexión `PauseController`) → 1 s | FotoJuego | Tarjeta DAÑO cortada | Sección DAÑO completa | `dano_visible_pct`, `fuera` |
| 21 | `ajustes_dano_1600` | T3 | idem, 1600×900 | FotoJuego | idem | idem | idem |
| 22 | `ajustes_dano_1920` | T3 | idem, 1920×1080; volver a 1600×900 | FotoJuego | idem | idem | idem |
| 23 | `celdas_0` | T6 | `Saltar(5)` → `SaltarAlSegundoPisoPorPrueba()` → jugador en `PosDeUsoDeLaTerminal` → `OperacionTerminal.PruebaToqueE = true` → 0,3 s | FotoJuego | Celdas cerradas, sin corte | Cámara mirando la reja que baja | `cine_s`, `control_durante` |
| 24 | `celdas_1` | T6 | + 1,0 s | FotoJuego | — | Reja a media altura | — |
| 25 | `celdas_2` | T6 | + 1,8 s | FotoJuego | — | Reja abajo, milicianos | — |
| 26 | `celdas_3` | T6 | + 2,6 s | FotoJuego | Aviso | Control devuelto, «2 MILICIANOS LIBERADOS», marcadores | `liberados`, `reja_abierta`, `control_despues`, `alerta` |
| 27 | `terminal_frente` | T6 (R12) | Existente (`Terminal()`) | Existente | — | — | Existente |
| 28 | `terminal_amplia` | T6 (R12) | Existente | Existente | — | — | — |
| 29 | `perros_manada` | T7 | Existente `Ciudad()` (30 s a ×4). Durante la espera, por cuadro: distancia mínima entre perros vivos que no saltan | Foto centroide de perros + (0,14,-3) → centroide, 50 | Perros superpuestos | Perros en abanico | `min_p01`, `pares_lt_0_4`, `pct_ge_0_7`, `perros_vivos` |
| 30 | `ciudad_vista` / `ciudad_perro` | T7 (R12) | Existentes | Existentes | — | — | Existentes |
| 31 | `plaza_cenital` | T15 | `Saltar(5)`, 2 s | Foto (322,60,200) → (322,0,228), 55 | Damero de 10×10 | Solado de granito, guardas, sendas | `adoquines`, `renderers_caja` |
| 32 | `plaza_ojo` | T15, T18 | idem | Foto (322; 1,7; 205) → (322; 1,2; 228), 60 | Damero a nivel de ojos | Solado con textura y relieve | — |
| 33 | `plaza_mobiliario` | T16 | idem. Primer `Auto_*` activo a menos de 40 m de (322,228) | Foto auto + (3,5; 1,6; 3,5) → auto + up×0,8, 50 | Auto de 2 cubos | Auto con ruedas, faros y parabrisas | `piezas_auto`, `renderers_ciudad` |
| 34 | `camion_patio_cerca` | T16 | `Saltar(4)` | Foto (-10; 2,2; 34) → (-16; 1,4; 40), 55 | Camión de cubos | Camión nuevo | `piezas_camion` |
| 35 | `ciudad_entrada` | T9 | `Saltar(5)` | Foto (238,10,214) → (272,0,228), 60 | Entrada a la ciudad | idem (continuidad) | — |
| 36 | `desierto_1` | T14 | `Saltar(4)` | Foto(congelar=false) (40; 2,2; 70) → (90,1,100), 65 | Arena con cactus sueltos | Árboles, rodantes, polvo | `arboles_terreno`, `rodantes`, `polvo` |
| 37 | `desierto_2` | T14 | idem | (130; 2,2; 140) → (180,1,170), 65 | idem | idem | — |
| 38 | `desierto_3` | T14 | idem | (220; 2,2; 200) → (250,1,220), 65 | idem | idem | — |
| 39 | `perf_ruta` | T14 | `ModoDios.VelocidadActiva = false`. Cámara propia que dibuja a pantalla (depth 100; la principal apagada) y recorre la polilínea `5_Autopista/RutaTanque` (o la recta (0,40) → (262,228)) a 18 m/s y 2,5 m de altura durante 20 s | — | Referencia | ≥ 60 fps, p95 ≤ 20 ms, ≥ 90 % de ANTES | `fps_prom`, `p95_ms`, `p99_ms`, `max_ms` |
| 40 | `perf_plaza` | T14, T15, T16 | Cámara fija en `plaza_ojo`, 8 s | — | Referencia | ≥ 95 % de ANTES | idem |
| 41 | `perf_inicio` | T9, T12 | Cámara fija en `inicio_ojo`, 8 s | — | Referencia | ≥ 95 % de ANTES | idem |
| 42 | `retroceso_huida` | T10 | `Saltar(4)` → anotar la posición → mover al jugador 30 m al sur (`EnMalla`) → 1,0 s | FotoJuego | Teletransporte de 14 m + «DEBES AVANZAR» | Sin teletransporte; cartel «DEBES VOLVER A LA GUERRA» | `teletransporte_m`, `texto_cartel` |
| 43 | `compuerta_bajada` | T10 | `Saltar(5)` → `SaltarAlSegundoPisoPorPrueba()` → jugador a 1,5 m de `Mansion_Compuerta_*_bajada`, mirándola | FotoJuego | Bloqueo + «NO SE PUEDE VOLVER ATRAS» | Paso libre + cartel | `collider_on`, `obstacle_on` |
| 44 | `alejamiento` | T10, T22 | `Saltar(2)` → alejar al jugador del objetivo en 6 pasos de 15 m durante 5 s | FotoJuego | AvisoUnico «VOLVE AL OBJETIVO · N m» | Cartel único transparente con flecha | `texto_cartel`, `fuentes_no_belicas` |
| 45 | `fuentes_popups` | T22 | `Saltar(2)`, `SinDios`. Clon enemigo → `TakeDamage(10, yo.Id, true)` (headshot) → mismo cuadro | FotoJuego en el cuadro siguiente | Popup en fuente Arial | Popup en fuente bélica | `fuentes_no_belicas` |
| 46-55 | `sala_*`, `pesado_*`, `escudero_*`, `escalera_*` | Regresión R12 | Existentes | Existentes | — | Iguales o mejores | Existentes |

### 7.3 Nombres que el arnés busca (contrato para los implementadores)

| Trabajo | Nombres |
|---|---|
| T1 | `SangreFx_*` (GO de partículas), `SfxKind.KnifeBlood`, `DecalKind.Sangre` |
| T4 | `AnilloDeImpactos`, `MarcaDeImpacto_<n>`, `HudDeTanqueSimple.AngulosActivos` |
| T5 | `EngranajeDeRevivir` (el `Image` hijo de relleno se llama `Relleno`) |
| T6 | `OperacionDirector.CineDeMansionEnCurso` (ya existe), `RejaAbierta`, `MilicianosLiberados` |
| T7 | `PerroEnemigo.Vivos`, `PerroEnemigo.SeparacionActiva` |
| T9 | `R13_Calles`, `Calle_T_Transversal`, `R13_Cordon_*` |
| T10 | `CartelVolver` (GO de `CartelVolverView`), texto exacto «DEBES VOLVER A LA GUERRA» |
| T13 | `Plaza_Cemento` (ANTES; DESPUÉS inactivo o ausente) |
| T14 | `Terreno_ArbolesDesierto`, `ArbustoRodante_<n>`, `PolvoDelDesierto`, `RemolinoDePolvo_<n>` |
| T15 | `R13_Solado`, materiales `M_Op_Plaza_*` |
| T16 | Hijos `Detalle_R13` |

---

## 8. Checks de editor

### 8.1 Nuevos

Un archivo parcial por trabajo: `Ed\ChecksBugs065.R13_<T>.cs`.

**Con mansión** (los ids matchean `reMansion`):

| Id | Trabajo | Qué verifica |
|---|---|---|
| `Bug958a` | T2 | Cohetes hasta matar al heli de la azotea por dificultad y punto (≤ 4/4/5); daño mayor que 0 en la cola; regresión de cohetes al `TanqueJefe` |
| `Bug965a` | T6 | Cinemática de 1,8-2,2 s; control quitado y devuelto; reja abierta; 2 liberados; alerta después; siguen al líder |
| `Bug966a` | T10 | Compuerta de bajada atravesable; cartel visible |
| `Bug967a` | T7 | 12 perros durante 20 s: mínimo ≥ 0,7 m en el 99 %, 0 pares < 0,4 m; con `SeparacionActiva = false` debe fallar |

**Sin mansión:**

| Id | Trabajo | Qué verifica |
|---|---|---|
| `Bug970a` | T1 | Letalidad: soldado 1 golpe; pesado 2 dentro de 4 s; pesado 1 + 5 s → vivo; tapado por muro → sin daño; escudero según D2 |
| `Bug971a` | T1 | Sangre y sonido: más de 30 partículas `SangreFx_*`; `KnifeBlood` reproducido; decal; el fallo no genera sangre |
| `Bug972a` | T3 | Coeficientes por vía (×2 y ×0,5), `RestaurarVida(0)` mata, `DestruirPorGuion` con Propio 0,1, herida de tutorial exacta, PlayerPrefs intactos |
| `Bug973a` | T3 | Sección DAÑO: `fuera == 0` en 3 resoluciones × 2 escalas × 2 idiomas; slider → valor; Restablecer |
| `Bug974a` | T4 | Mira inicial sin clic (`ads > 0.9`, FOV correcto a 1 s); clic derecho alterna |
| `Bug975a` | T4 | Anillo: -90° y 180° ±5°; se apaga a 2,1 s; un impacto real de proyectil genera marca |
| `Bug976a` | T5 | Un solo elemento de feedback a los 2 s (jugador y médico), con `fillAmount` correcto |
| `Bug977a` | T8 | 0 aparición enemiga sin ruta `Complete`; las 12 reservas salen del cubículo a los 12 s |
| `Bug978a` | T9 | Sin calle de aproximación; la T existe; bordes conectados; rutas `Complete`; `Destino_Huida` libre |
| `Bug979a` | T10 | Huida sin teletransporte; cartel por alejamiento; rutas `entrada[i] → entrada[i-1]` `Complete` |
| `Bug980a` | T11 | En juego: `LimpiezaDeDuplicados.Apagados == 0`, `CollidersApagados == 0`, `TitulosDeObjetosOcultos.Ocultos == 0` |
| `Bug981a` | T12 | 0 camión, tambor o generador en el inicio; carga habilitada en t=0; la carga abre y pasa a Puestos |
| `Bug982a` | T13 | 0 cubos grises grandes cerca del patio; 0 `Image` grises grandes en el HUD del tanque |
| `Bug983a` | T14 | Árboles de terreno entre 600 y 1500, rodantes entre 8 y 12, polvo presente, 0 árboles a menos de 24 m de la ruta |
| `Bug984a` | T15 | 0 `Adoquin` ni `Patio_Losa`; ≤ 12 renderers en la caja de la plaza; materiales con `_MainTex` y `_BumpMap` |
| `Bug985a` | T16 | Detalle ≥ 5 piezas o 200 triángulos por prop; collider raíz igual; renderers de la ciudad ≤ +10 % |
| `Bug986a` | T17 | Ningún `Detalle_R13` ni `R13_*` con collider |
| `Bug987a` | T22 | 0 `Text` activos con fuente no bélica en el mismo cuadro de crear popups, avisos y el cartel |
| `Bug988a` | T24 | Recorrido de las 6 fases hasta Victoria sin excepciones |

**Herramientas de editor** (no son checks en Play):

| Herramienta | Salida |
|---|---|
| `AuditoriaDeSolapesR13.Informe(etiqueta)` | `solapes_<etiqueta>.txt` |
| `AuditoriaDeColliders` (corregida) | `colliders_<etiqueta>.txt` |
| `VistaCenitalR13.Generar(etiqueta)` | `cenital_nivel.png` |
| `InventarioDeRecursosR13` | `inventario_recursos.tsv` |
| `AtajosDeTesteoR13.Generar()` | `ATAJOS_DE_TESTEO.txt` |

### 8.2 Existentes que hay que actualizar (T19)

| Check / archivo | Motivo | Cambio |
|---|---|---|
| `Bug409u` (`ChecksBugs065.Azotea.cs:239`), `Bug960a` (`Nov13c.cs:29`), `Revision.cs:109,115` | Esperan `MultiplicadorDeDanoDelTanque == 16`; la constante es 32 (`Huida.cs:377`) | Esperar `32 × AjustesDeDano.EnemigoPorDefecto == 16` |
| `Bug407u` (`Azotea.cs:68-147`) | Espera 4 milicianos; hay 2 | `MandoTactico.CantidadDeMilicianos` |
| `HeadlessTestRunner.Fases13a17.cs:466-472` | Cuchillo = 100 − 55 | `!IsAlive` |
| `ChecksBugs065.Wp2.cs:44-47`, `Fases13a17.cs:410` | Lista de clips | Agregar `KnifeBlood` (y `RejaAbriendo`, si se crea) |
| `Bug088`, `Bug089` (`Wp5a.cs`), `HeadlessTestRunner.Fase21.cs:124-181`, `Fases8a12.cs:841-862` | Revivir con barra, círculo y 3 anillos | Esperar el engranaje y la cantidad de anillos de D7 |
| `Bug366`, `367`, `384`, `406`, `413` (compuertas), `Bug370a` (`Nov08.cs`, retroceso) | Bloqueo físico y teletransporte | Esperar el paso libre y el cartel |
| `Bug303a`, `304a`, `305u`-`308u`, `318u`, `078e`, `501b`, `501d`, `501e`, `603d`, `710a`, `298v`, `302a` | Generador y camiones del cuartel | Sin generador; la carga se habilita en t=0; quitar el generador de la lista de piezas «héroe» (`Bugs298a302.cs:145`) |
| `Bug501a` | Lista blanca de «islas por diseño» | Dejar solo TV1 y TV2 |
| `Bug501g` | Falsos positivos | Lista blanca documentada (T17) |
| Checks del tanque en tercera persona (`Nov08b.cs:119-236`, `Nov11.cs:57-66/362`, `Bug416a`, `Nov13c`, `Nov13d`) | La mira entra fija | Salir del zoom con un clic antes de medir, o medir en el periscopio |
| Tutorial `mira_tanque` (`TutorialManager.cs:1016-1035`, `TutorialAutoPlayer.GestoMiraTanqueReal:194`) | El gesto supone mantener | Gesto de alternar y texto nuevo |
| `HeadlessTestRunner.RestablecerEstaticosDeJuego` (L197-207) | No reinicia `AjustesDeDano` | `ForzarParaPrueba(1, 0.5f)` |
| `HeadlessTestRunner.Fase21.cs:439-499` | Configuraciones sin la sección DAÑO | Incluir DAÑO y el diagnóstico de nietos |
| `Ed\CapturasDeUi.cs:23-29` | Panel falso de 540 | Panel real |
| `Bug602d` (`Hoy.cs:772-783`) | Vida del heli | Sin cambio si `VidaDeAsalto` no cambia. Confirmar |
| `Bug962a` | Perros llegan a la plaza | Ampliar el tiempo de espera si la separación los demora |

---

## 9. Plantilla de `VALIDACION_R13.md`

| T | Pedido (L#) | Medida objetiva | ANTES | DESPUÉS | Umbral | Check | Toma(s) | Estado |
|---|---|---|---|---|---|---|---|---|
| T1 | L1-1 | Cuchilladas para matar soldado / pesado; partículas | 3 / 36; ~1 | 1 / 2; ≥ 30 | 1 / 2; ≥ 30 | 970a, 971a | 16-18 | |
| T2 | L1-2 | Cohetes por dificultad | > 12 | 4/4/5 | ≤ 4/≤ 5/≤ 5 | 958a | 19 | |
| T3 | L1-3 | % visible de DAÑO; vías con coeficiente correcto | 25 %; 3 bugs | 100 %; 0 | 100 %; 0 | 972a, 973a | 20-22 | |
| T4 | L1-5 | `ads` al subir; ángulo de la marca | 0; n/a | > 0,9; ±5° | | 974a, 975a | 10, 12, 13 | |
| T5 | L2-5 | Elementos de feedback | ≥ 6 | 1 | 1 | 976a | 14, 15 | |
| T6 | L1-6 | Duración de la cinemática; liberados | 0 s; 2 | 2,0 s; 2 | 1,8-2,2 | 965a | 23-26 | |
| T7 | L1-8 | Pares < 0,4 m; % ≥ 0,7 m | | 0; ≥ 99 % | | 967a | 29 | |
| T8 | L1-9 | Barreras; aparición sin ruta | 2; 12 | 0; 0 | 0; 0 | 977a | 5, 6 | |
| T9 | L1-4, L2-1/2 | Calle de aproximación; T; rutas | sí; no | no; sí; OK | | 978a | 1, 2, 7, 8, 35 + cenital | |
| T10 | L1-10 | Teletransporte; compuerta; cartel | 14 m; on; AvisoUnico | 0; off; cartel | | 966a, 979a | 42-44 | |
| T11 | L1-7, L2-3/8 | Solapes A/B/C/D/E/F; apagados en juego | | 0/0/≤ 10 %/0/0/0; 0 | | 980a + informe | global | |
| T12 | L2-4 | Camión, tambor y generador en el inicio; carga en t=0 | > 0; no | 0; sí | | 981a | 1, 3, 4 | |
| T13 | L2-7 | Cubos grises; `Image` grises | ≥ 1; ≥ 1 | 0; 0 | | 982a | 9, 11 | |
| T14 | L2-6 | fps de la ruta; árboles; rodantes | | ≥ 60; 600-1500; 8-12 | | 983a | 36-41 | |
| T15 | L2-9 | Adoquines; renderers de la plaza | 100; ~100 | 0; ≤ 12 | | 984a | 31, 32 | |
| T16 | L2-11 | Piezas por prop; renderers de la ciudad | 2; R | ≥ 5; ≤ 1,1 R | | 985a | 33, 34 | |
| T17 | L2-3 | 501g n1/n2/n3 | | ≤ lista blanca / 0 / ≤ lista blanca | | 501g, 986a | — | |
| T18 | L2-10 | Filas OK de `VALIDACION_PARCIAL2.md` | | todas | | — | — | |
| T19 | — | Fallos nuevos del lote | — | 0 | 0 | lote | — | |
| T20 | — | RAR probado | — | sí | | — | `inicio_hud` del RAR | |
| T21 | — | Dos `.txt` en el RAR | no | sí | | — | — | |
| T22 | — | `fuentes_no_belicas` | > 0 | 0 | 0 | 987a | 3, 44, 45 | |
| T23 | — | Recursos sin origen | > 0 | 0 | 0 | — | — | |
| T24 | — | Punta a punta | — | victoria sin bugs bloqueantes | | 988a + manual | — | |

---

## 10. Riesgos globales

1. **ANTES no comparable.** Si el arnés se escribe después de tocar el juego, se pierde la línea base. Por eso T0 va primero, con build propia.
2. **Modo dios en el arnés.** Triplica el daño y acelera el tiempo: los datos de T1 y T2 serían falsos. Se mide con `SinDios`.
3. **Foto con el tiempo congelado.** `Foto()` detiene el tiempo y frena las cinemáticas y las partículas. T6 usa solo `FotoJuego`; T1, T2 y T14 usan `congelar=false`.
4. **Escena binaria `SC_Operacion`.** No se puede mezclar: un solo agente y un orden fijo (§6, ola 2).
5. **Builders viejos.** Volver a correrlos duplica o pisa. Toda edición nueva va en `*R13.cs` y se porta al builder sin ejecutarlo.
6. **Prefabs sin respaldo automático.** Copiar a `Temp/R13_prefabs_antes/` antes de la ola 2.
7. **Balance.** El cuchillo letal y los cohetes más fuertes cambian la dificultad: medir el tanque jefe y las camionetas (T2) y anotarlo.
8. **Fuentes.** Si la docente rechaza las OFL, el plan B cuesta 1-2 días. Preguntar ya (D11).
9. **`INTEGRANTES.txt`** bloquea la entrega: pedir los nombres ya (D12).
10. **Variación entre corridas** (obuses del pesado, IA). Hacer dos corridas y elegir por toma, con semilla fija; las medidas numéricas se toman en checks deterministas, no en fotos.
