# Plan v2: bugs #285 a #297 (Strategic Point, escena SC_Operacion)

Proyecto: `C:\_Proyectos privados\DV_C6_StrategicPoint` (Unity 6000.6.2f1, URP Forward+, rama `Nueva_Completa_Rota`).

Es la revision del plan v1. Cada hipotesis se contrasto con el codigo real (`Assets/_Project/Scripts/**`), con las capturas `Registros/bug_NNN_*.png` y con los json de cada bug.

Las rutas de este documento son relativas a `Assets/_Project/Scripts/` salvo que se indique otra cosa. Las lineas son las del arbol al 2026-10-26: antes de editar, buscar por el nombre del metodo.

Reglas generales que valen para todo el plan:
- SC_Operacion es una escena BINARIA de 13 MB. No se puede grepear: se inspecciona y se retoca en el editor con eval o con builders idempotentes. Nunca se reconstruye entera (`OperacionBuilder.Construir` queda PROHIBIDO).
- Ningun `.cs` se edita con el editor en Play.
- Para reproducir un caso: `SesionLog.RestaurarBug(n)`. La camara del "antes" esta en `jugador.camPos` / `jugador.camRot` del json.

---

## 0. Cambios de fondo respecto del v1 (resumen)

| # | El v1 decia | Lo que realmente pasa (v2) |
|---|---|---|
| 285 | "SpotLight sin sombras" | La luz SI tiene sombras (Soft). Se escapan por dos motivos: la distancia de sombras de URP es de 50 m y la resolucion del mapa de sombras es baja. Ademas el haz visible es una malla fija de 40 m que nunca se recorta. |
| 286 | Solo el engranaje | Aparecen DOS marcas. Una es el rombo chato del `MunicionPickup`, echado en el piso. La otra es el engranaje de `InteractGearMarker`, a +1,1 m. |
| 287 | Bob del pickup | Hay un bug concreto en `MunicionPickup.Apoyar`: toma el punto MAS ALTO bajo el pickup (sacos, escombros) y lo pone a +0,5 m. Ademas `CraterPool` dibuja un disco con 8 bloques de borde levantados, y `AlturaDelPiso` / `DecalPool` aceptan apoyar la marca sobre obstaculos. |
| 288 | Disco de `CargaExplosiva` | El circulo amarillo NO es de la carga. Es el anillo de la baliza `TutorialBeacon` que pone `BalizaDelPuesto`: un Quad de 5,2 m de diametro con textura de circulo. |
| 291 | "No hace nada visible" | El hackeo termina y pasa a Huir, y el texto pide que el ASALTO demuela `Muro_Salida_Norte`. Ese texto aparece en 3 lugares y hay una prueba (Bug243a) que depende de que el muro caiga. |
| 292 | Quitar del director | `TorretaFija.InstalarEnEscena` arma TODO `P_Env_Emplazamiento_MG` que encuentre en la escena. Hay que BORRAR las 2 instancias de la escena, no solo desenchufarlas del director. |
| 293 | Rango de niebla | Causa confirmada en el json: los soldados montados quedan con la posicion de cuando subieron (Vega en (-12,29) con el tanque en (61,222)). Ademas el radio de vision es de 10 m y el vehiculo propio no tiene cono. |
| 294 | Rombos de sector | Las marcas son otras: el engranaje informativo del tanque DESTRUIDO a +3,6 m y los rombos de aliado de `UnitLocatorCylinder`, a 2,4 m sobre el pivote. |
| 295 | `DefensaDelCentro` | `DefensaDelCentro` es la defensa del HACKEO, no de la ciudad. Resistir vive en `OperacionDirector.Mando.cs` + `MandoTactico` + `SectorDeDefensa` (2 sectores, A y B). |
| 296 | TorreDestruible | Es `Casa_1` (280,255): se ve el vestido de PulidoFinal y ArteOp, pero no el cubo de la casa. Lo mas probable es que lo haya apagado la limpieza de "mallas duplicadas" (commits ef1f23dc y 08317863, solo escena). |
| 297 | "Reemplazar el sonido" | Cada bala de las 2 ametralladoras de puerta reproduce `Shot_Heavy`: 16 disparos por segundo de una escopeta Mossberg distorsionada. |

---

## 1. Bug por bug

### #285: "la luz no deberia traspasar muros" (reflectores de vigia del cuartel)

**Causa raiz confirmada.** Captura: reflector R2 en (56, 7,15, -304) con la camara en (5,5, 2,8, -318,4), a unos 52 m de la lampara. Son tres problemas.

1. **El haz visible no se recorta.** `Operacion/ReflectorVigia.cs:95-121` (`CrearHaz`) arma un cono fijo de 40 m con material `Sprites/Default` (ZWrite off). El cono hace depth test, pero su volumen sigue de largo a traves del muro, y la parte que queda del lado de la camara se dibuja encima de todo. A eso se refiere el usuario con "revisa el shader".
2. **Falta de sombras a distancia.** `Assets/Settings/PC_RPAsset.asset` tiene `m_ShadowDistance: 50`. URP apaga las sombras de las luces adicionales en los pixeles a mas de unos 45 a 50 m de la camara. Desde la camara de la captura, todo el piso iluminado al otro lado del muro esta a mas de 50 m: la luz pasa sin sombra.
3. **Mapa de sombras de baja resolucion.** La luz se arma en `Editor/OperacionBuilder.Cuartel.cs:412-462` (`Reflector`: spot, rango 45, angulo 18/12, intensidad 9, `shadows = Soft`). No define la resolucion por luz, asi que usa el tier bajo del atlas (256). Con muros de 0,4 a 2 m, el bias normal de 0,5 hace "peter-panning" y la luz se escapa por el pie del muro.

**Cambio propuesto.**
1. En `ReflectorVigia.Update`, dentro de `Aplicar` (linea 169), recortar el haz:
   - Cada 0,05 s, `Physics.Raycast(cabeza.position, cabeza.forward, out hit, 40f, mascaraSolidos, QueryTriggerInteraction.Ignore)`. La mascara es `~0` menos la capa de soldados. Se ignoran los colliders hijos del propio reflector (`Lampara`) y los de `Soldier`.
   - Largo = `hit.distance + 0.3f` (minimo 2 m, maximo 40). Se aplica como `haz.localScale.z = largo / 40f`, sin tocar la escala x/y para no achicar la boca del cono.
   - Para que el corte no se lea como una tapa dura, el vertex color del extremo lejano ya tiene alfa 0: con el largo recortado se desvanece antes.
   - Opcional: 4 rayos extra en los bordes del cono (angulo 12) y usar el minimo si el central pega mas lejos que los bordes por mas de 6 m.
2. Ajustes de la luz, en dos lugares: en el builder (`Reflector`) y en el retoque en sitio de la escena (eval sobre los 4 `Reflector_*`/`Luz`):
   - `shadows = LightShadows.Hard`, `shadowNearPlane = 0.5`;
   - `UniversalAdditionalLightData`: `usePipelineSettings = false`, `additionalLightsShadowResolutionTier = High` (1024);
   - `shadowBias = 0.05`, `shadowNormalBias = 0.2`.
   - No cambiar `range` (45) ni `spotAngle` (18): los verifica `ChecksBugs065.Wp8.cs:131`.
3. En `PC_RPAsset`:
   - `m_ShadowDistance` de 50 a 75;
   - ajustar las cascadas para que la primera siga cubriendo de 0 a 7 m: splits `0.09 / 0.24 / 0.5`, es decir unos 6,8 m, 18 m y 37,5 m.
   - Medir fps antes y despues en la misma camara (la referencia del json es fps~78). Si cae mas de 8 %, dejar la distancia en 60.
4. Plan B sin tocar el asset (si el paso 3 cuesta demasiado): en `Aplicar`, atenuar `luz.intensity` linealmente a 0 entre 48 y 60 m de distancia de la camara a la lampara. El haz recortado sigue visible.

**Riesgos y pruebas afectadas.**
- `ChecksBugs065.Wp8.cs:131` verifica rango 45 y angulo 18: no se tocan.
- `ChecksBugs065.Wp8.cs:289` verifica `HazVisible == false` al romper la lampara: el recorte no debe reactivar el haz si `rota`.
- Agregar al check Wp8 que, con R2 apuntando a un muro a menos de 10 m, `haz.localScale.z * 40 < 12`.
- Bajar el bias puede producir acne de sombra en el piso del cuartel: revisar la captura.
- Rendimiento: el atlas de 4096 admite 16 tiles de 1024. Son 4 reflectores mas las 8 point lights con sombra de `SombrasDeLuces`, asi que entra. Verificar `Frame Debugger > Additional Lights Shadow`.

**Verificacion en Play.**
- Reproducir con `RestaurarBug(285)` y forzar la camara a los valores del json.
- Captura ANTES: el PNG del usuario. Captura DESPUES desde la misma camara: el haz termina en el muro y del otro lado el piso queda oscuro.
- Segunda captura de control desde 30 m: la sombra del muro se ve nitida.

---

### #286: "la marca de municion deberia estar arriba y no deberia tener engranaje"

**Causa raiz confirmada.** Hay dos marcas superpuestas.
1. **El engranaje.** `Player/PlayerInputDriver.cs:3223-3231`: el caso `AimTargetType.Recoger` hace `ancla = HitTransform.position + up*1.1` y en la linea 3234 llama a `InteractGearMarker.Mostrar(ancla, destacado)`. `InteractGearMarker` es SIEMPRE el engranaje (rombo con engranaje que gira).
2. **El rombo chato.** `Player/MunicionPickup.cs`, en `Crear(pos)`, recibe el pivote del soldado muerto (0,8 m sobre los pies) y llama a `InteractableDiamond.Agregar(raiz, up*0.15, ColorMunicion, enSuelo:true, 0.5f)`. Ese rombo queda echado en el piso: es la "marca de abajo" que el usuario quiere arriba.

**Cambio propuesto.**
1. En `PlayerInputDriver.ActualizarPromptContextual`, caso `Recoger`:
   - poner `mostrar = false` (sin engranaje) y dejar solo `pista = "PASÁ POR ENCIMA PARA RECOGER"`;
   - quedan `InteractGearMarker.Ocultar()` e `InteractHintView.Mostrar(pista)`.
2. En `MunicionPickup.Crear`:
   - cambiar el rombo a `enSuelo:false`, con offset `up * 0.85f` sobre el piso real (ver #287, `alturaBase = suelo`);
   - escala 0,35, billboard vertical como el de la `CajaDeSuministros`;
   - opcional: icono de bala (`RoleIconFactory` o el sprite de municion del HUD) dentro del rombo.
3. Mismo criterio para `Combat/WeaponPickup.cs`, que usa `PisoInteractableHighlight`: dejar ese realce en el piso y no agregar engranaje (ya lo cubre el paso 1).

**Riesgos y pruebas afectadas.**
- `HeadlessTestRunner.Fase23.cs:641-644` solo pide que el pickup tenga `InteractableDiamond`: sigue pasando.
- `ChecksBugs065.Wp5b.cs:134-152` (`AnguloActual` del engranaje) no depende de Recoger: sigue pasando.
- Buscar checks que esperen el engranaje sobre un pickup: `grep "Recoger" Editor/` devuelve 0 casos de prueba sobre el engranaje.

**Verificacion en Play.** `RestaurarBug(286)` y mirar la municion caida. ANTES: engranaje y rombo chato. DESPUES: un solo rombo de color municion a unos 0,85 m del piso, sin engranaje, y la pista de texto abajo.

---

### #287: "sigue levitando ... y las marcas de explosion deberian estar en el piso"

**Causa raiz confirmada.**
1. **`MunicionPickup.Apoyar`** (`Player/MunicionPickup.cs:52-62`, del commit dc75489d) tiene cuatro defectos:
   - el rayo sale de `alturaBase + 0.5`;
   - toma `Mathf.Max` de TODOS los impactos, asi que se queda con la cima de un saco, de un escombro del `Fragmentador` o del zocalo de la torre;
   - fija `alturaBase = mejor + 0.5f` (0,5 m de aire);
   - solo corrige si la diferencia supera 1,2 m.

   Cuando el escombro desaparece, el pickup queda flotando hasta 1,2 m mas el bob de 0,07. En la captura: gear y pickup arriba de un crater, objetivo 1 "torre AA 1/2", jugador en (44,2, -278,5).
2. **Marcas de explosion levantadas.**
   - `Presentation/ImpactFx.cs:319-347` (`SpawnExplosion`) usa `pisoY = DestruccionDeVehiculo.AlturaDelPiso(position)`.
   - `DestruccionDeVehiculo.cs:19-39` devuelve el primer impacto que `BlocksMovement`: sacos, el zocalo de la torre o la torre misma antes de caer.
   - Con esa altura, `CraterPool.Crear(position, pisoY, radius*0.55)` (`DestruccionDeVehiculo.cs:184-304`) dibuja un disco oscuro con 8 bloques de borde levantados (lo que el usuario ve "no asi").
   - `DecalPool.cs:207-215` lanza el rayo del crater desde +1,5 con `~0` y acepta cualquier normal con y > 0,8, asi que la quemadura queda sobre el techo de un saco, una camioneta o un cuerpo.

**Cambio propuesto.**
1. Nuevo helper `Core/PisoReal.cs`: `public static bool Altura(Vector3 p, out float y, out Vector3 normal, float desde = 3f, float largo = 40f)`.
   - Usa `RaycastNonAlloc` y elige el impacto MAS BAJO que cumpla todo esto:
     - collider no trigger;
     - sin `Rigidbody` no cinematico;
     - sin `Soldier`, `Vehicle` ni `OperacionAuto` en los padres;
     - sin `Fragmentador`/debris;
     - no es `ObstacleMarker` destructible ni `TorreDestruible`;
     - normal.y > 0,8.
   - Respaldo: `NavMesh.SamplePosition(p, out h, 3f, AllAreas)`, que da h.position.y (el piso navegable esta a unos 0,08).
2. Usarlo en tres lugares:
   - `DestruccionDeVehiculo.AlturaDelPiso`, conservando la firma para no romper llamadores;
   - el rayo de `DecalPool.Spawn` para Crater;
   - `MunicionPickup.Apoyar`.
3. `MunicionPickup`:
   - `alturaBase = suelo + 0.12f` (la caja apoyada);
   - re-apoyar cada 0,5 s si la diferencia supera 0,05 m;
   - bob de 0,07 a 0,02 (o eliminarlo: el rombo de #286 ya marca el pickup);
   - el rombo flota a +0,85 sobre `alturaBase`.
4. `CraterPool`:
   - eliminar los 8 bloques del borde (o dejarlos con alto de 0,03 y sin sombra);
   - el disco va a `pisoY + 0.02` y solo si `PisoReal` encontro piso a menos de 1,5 m del punto de la explosion;
   - si la explosion fue en altura (torre, techo), NO se crea crater.
   - Alternativa mas simple y de menos riesgo: dejar solo el decal de `DecalPool` (quemadura translucida que se desvanece, add55b15) y apagar `CraterPool` con `const bool UsarCrater3D = false`.

**Riesgos y pruebas afectadas.**
- `ChecksBugs065.Wp6.cs:571-578` mira `CraterPool.Lanzados`, `Activos` e `yMax`:
  - si se apaga `CraterPool`, cambiar el check a `DecalPool.CountOf(Crater) > 0` y verificar que la `y` del decal quede a menos de 0,05 del `PisoReal`;
  - si se conserva, ajustar `yMax` a 0,06.
- Hay checks de municion flotante por el commit dc75489d: buscar `MunicionPickup` en `Editor/ChecksBugs*.cs` y ajustar las alturas esperadas (de +0,5 a +0,12).
- Rendimiento: `RaycastNonAlloc` con buffer de 8, cada 0,5 s por pickup (hay pocos).

**Verificacion en Play.**
- `RestaurarBug(287)` con la camara del json. Volar la torre AA restante con granada o cohete a 10 m de sacos y de un enemigo muerto.
- Captura DESPUES:
  - la municion queda apoyada en el piso aunque desaparezcan los escombros (esperar 6 s);
  - la quemadura queda a ras del piso, sin bloques ni discos encima de los sacos.
- Medir por eval: `(pickup.y - PisoReal.y) < 0.15` y `(decal.y - PisoReal.y) < 0.05` para todos los decals Crater activos.

---

### #288: "la marca del piso eliminala, ese circulo eliminalo" (CARGA OESTE y CARGA ESTE)

**Causa raiz confirmada.** Captura: un disco amarillo de unos 5 m bajo la caja `Caja_Carga`, con las bandas negras a 45 grados.
- El circulo es la baliza: `Operacion/OperacionDirector.Puestos.cs:98`, en `BalizaDelPuesto`, llama a `PonerBaliza(titulo, (1, 0.82, 0.3), carga.pos, null, 2.6f)`.
- Eso termina en `OperacionDirector.cs:403-407`, `TutorialBeacon.Crear(...)`.
- `Tutorial/TutorialBeacon.cs:75-91` arma el marcador con `ShapeMarkerFx.CrearMarcador` (`Presentation/ShapeMarkerFx.cs:90-103`), que es un Quad con textura de circulo de radio x 2 = 5,2 m.
- Debajo hay ademas una placa cuadrada amarilla de 3,6 x 3,6 m con 3 bandas negras (`Editor/OperacionBuilder.Puestos.cs:203-204`, "Placa" y "Banda_*"). No es "el circulo", pero forma parte de la misma "marca del piso".

**Cambio propuesto.**
1. En `TutorialBeacon`:
   - agregar el parametro opcional `bool conAnillo = true` a `Crear` y, si es false, `anillo.SetActive(false)`;
   - mantener la entrada en `MarcaDeObjetivosEnMinimapa` (el minimapa sigue marcando el puesto).
2. En `OperacionDirector.PonerBaliza`: agregar el parametro `bool conAnillo = true`. En `BalizaDelPuesto`, pasar `conAnillo:false`.
3. Interpretacion: "esa marca del piso" es el circulo. La placa cuadrada con bandas se conserva porque es el arte del punto de carga y no tiene forma circular. Si en la captura DESPUES la placa todavia se lee como "marca", desactivar `Carga_*/Placa` y `Carga_*/Banda_*` en la escena con eval y comentarlos en el builder.

**Riesgos y pruebas afectadas.**
- `HeadlessTestRunner.Fase19.cs:113-115` solo prueba `AlfaSegunDistanciaAMira`: sigue pasando.
- Ninguna prueba cuenta los anillos de las balizas de puestos: `grep "TutorialBeacon" Editor/` devuelve 2 archivos, ninguno sobre el anillo.

**Verificacion en Play.** `RestaurarBug(288)` (objetivo 2/6). ANTES: el disco amarillo. DESPUES: el piso con la placa cuadrada, sin el circulo. Hacer la misma toma en CARGA ESTE.

---

### #289 y #290: "falta el techo, debe estar cubierto, baja el brillo de las barras de los muros" y "dale mas altura"

**Causa raiz confirmada.**
- **Sin techo y con 5 m de alto.** `Editor/OperacionBuilder.Centro.cs:31-44` arma la sala sin techo a proposito (comentario "sin techo: vista libre"): `MuroX`/`MuroZ` de 5 m de alto y 1,5 de grosor (`CD_Sur_O/E`, `CD_Norte_O/E`, `CD_Oeste_N/S`, `CD_Este_N/S`, y `Muro_Salida_Norte` destructible de 8 x 5 x 1,5).
- **Las "barras" brillantes.** Son tres grupos:
  - `PulidoFinal.ArteCentroDeDatos`, apartado e) (`Editor/OperacionBuilder.PulidoFinal.cs:167-185`): `LineaLuz` a 2,7 m con `leds[1] = Mat("LedCian", ..., 2.2f)`;
  - los LEDs de los racks (lineas 104-123, 2,2);
  - en `Editor/OperacionBuilder.Servidores.cs`: LEDs de gabinete 2,2, `Dintel_Luz` (`leds[2]`) y `Panel_Luz` de techo con emision 2,0.
- El arte "de techo" de Servidores (bandejas a 4,25, ductos a 4,55, paneles a 4,18) cuelga hoy a cielo abierto.
- Los "remates" de PulidoFinal (mastiles 6 a 10,4 m, `Equipo_Aire` a 6,45, antenas a 7,4) quedan sobre el muro.

**Cambio propuesto.** Un solo builder nuevo e idempotente: `Editor/OperacionBuilder.SalaCubierta.cs`, menu `Strategic Point/Arte/Sala cubierta (SC_Operacion)`.

1. **Altura de 5 a 8 m** (+3 m), en la escena viva y en `OperacionBuilder.Centro.cs` para que un rebuild futuro coincida:
   - los 8 muros `CD_*`: `localScale.y = 8` y `position.y = 4`;
   - `Muro_Salida_Norte` se queda en 5 m (es la puerta);
   - nuevo `Solido` "Dintel_Salida_Norte" de x -4..4, y 5..8, z -40, grosor 1,5, con material `Datos`;
   - `Physics.SyncTransforms()` despues de escalar.
2. **Re-vestir** con ArteOp los muros que crecieron:
   - primero sondear con eval que objetos de ArteOp tienen bounds que tocan `CD_*` (cornisa, parapeto y pilares de `ArteDeOperacion.VestirEdificio`);
   - subir +3 m los que estan arriba de y = 4,5 y escalar los pilares;
   - si ArteOp tiene un re-vestido por objeto, usarlo solo sobre esos 9 muros. Nunca correr ArteOp sobre toda la escena.
3. **Techo**:
   - `Techo_CD` de x -26..26, z -80..-40, y 8,0..8,3, material `Mat("TechoSala", (0.22, 0.24, 0.28))`;
   - con BoxCollider (para que no entren granadas ni obuses) y `NavMeshModifier { overrideArea = true, ignoreFromBuild = true }`, asi no nace NavMesh arriba y no hace falta rehornear;
   - `shadowCastingMode = On`.
4. **Que el techo no tape la vista tactica.** Componente nuevo `Presentation/TechoOcultable.cs`, puesto en `Techo_CD` y en los remates:
   - si `CameraRig.Instance.Mode == ControlMode.Rts`, o si la camara esta a mas de y = 8,5 dentro del rectangulo de la sala, sus renderers pasan a `ShadowCastingMode.ShadowsOnly`;
   - en FPS y TPS vuelven a `On`;
   - se evalua cada 0,25 s, sin GetComponent por frame.
5. **Subir +3 m el arte de Servidores** (constantes en `OperacionBuilder.Servidores.cs`, apartado e, lineas 143-162):
   - `Bandeja` de 4,25 a 7,25, `Cable_R/A` de 4,33 a 7,33, `Ducto` de 4,55 a 7,55, `Rejilla_Aire` de 4,22 a 7,22, `Panel_Luz` de 4,18 a 7,18 (este va debajo del techo);
   - `Cable_Subida` de los gabinetes: alto 1,9 a 4,9, centro 3,05 a 4,55;
   - correr `ServidoresEscenaAbierta()`, que es idempotente.
6. **Subir +3 m los "Remates" de PulidoFinal** (apartado f, lineas 187-196): `Mastil` 8,2 a 11,2, `Mastil_Luz` 10,5 a 13,5, `Antena_Plato` 7,4 a 10,4, `Equipo_Aire` 6,45 a 8,75 (sobre el techo).
   - Mover tambien el `Rotulo "CENTRO DE DATOS"` (Centro.cs:46, y 8,2) a y 9,6.
   - No regenerar todo PulidoFinal por esto: ver la trampa T7. Mover en la escena con eval los objetos de `9_PulidoFinal/CentroDeDatos/Remates` y cambiar las constantes del builder.
7. **Bajar el brillo**:
   - `LineaLuz`: material propio `Mat("LedCianTenue", (0.2, 0.8, 1), 0.6f)` (antes compartia `LedCian` 2,2 con racks y gabinetes) y altura 2,7 a 3,2;
   - LEDs de rack y gabinete: 2,2 a 1,2;
   - `Panel_Luz` de techo: 2,0 a 1,1;
   - `Dintel_Luz`: 2,2 a 1,0.

   Como `Mat(nombre, color, emision)` probablemente cachea por nombre, crear materiales NUEVOS ("...Tenue") para no cambiar otros usos de `LedCian` fuera de la sala.
8. **Luz interior con techo**:
   - `Luz_CD_1/2` de y 5,5 a 7,2, rango 22 a 26, intensidad 3,5 a 4,2;
   - agregar `Luz_CD_3` en (0, 7,2, -70) y `Luz_CD_4` en (0, 7,2, -46), color (0.7, 0.9, 1);
   - `Luz_PC` sin cambios.

**Riesgos y pruebas afectadas.**
- No hay pruebas sobre `CD_*`, `Panel_Luz`, `Remates` ni `9_Servidores` (grep en `ChecksBugs*`, `HeadlessTestRunner*` y `Validacion*`: 0 casos).
- **NavMesh.** Muros mas altos no cambian el area caminable y el techo ignora el horneado, asi que no hace falta rehornear. Verificar igual con `NavMesh.CalculateTriangulation().vertices.Length` antes y despues (debe ser igual) y con un `CalculatePath` de la entrada sur a la computadora.
- **Helicopteros de la defensa** (`DefensaDelCentro`, hoy deshabilitados) y obuses: con techo con collider, un obus pega arriba. Es lo deseado.
- **Camara TPS dentro de la sala.** Si el rig sube por encima de 8 m al alejarse, `TechoOcultable` lo cubre.
- **Sombras.** Con el techo, la sala queda sin luz de luna. Por eso las 4 point lights; `SombrasDeLuces` le da sombra a las 8 mas cercanas.

**Verificacion en Play.**
- `RestaurarBug(289)` y `RestaurarBug(290)` con las camaras de los json. Captura DESPUES: techo visible, sala mas alta (los gabinetes de 2,2 m se ven chicos), lineas cian suaves.
- Una toma en RTS sobre la sala: el techo no tapa y proyecta sombra.
- Hackear 30 s completos con oleadas para verificar que los enemigos siguen entrando por las 3 puertas.

---

### #291: "al hackear esto deberia explotar y tambien abrir la puerta"

**Causa raiz confirmada.**
- Al completarse (`Operacion/OperacionTerminal.cs:294-302`, `Terminar` y el evento `Completado`), `OperacionDirector.Centro.cs:156-161` solo cierra la defensa y llama a `EntrarFase(Huir)`.
- En `OperacionDirector.cs:323-328` el aviso dice "EL ASALTO DEBE DEMOLER EL MURO DE LA SALIDA NORTE".
- `OperacionDirector.Jefe.cs:183` escribe el objetivo 4/6 "El ASALTO debe DEMOLER el muro de la salida norte (apuntalo y [Q] > DEMOLER) · despues avanza al patio".
- El muro es `Muro_Salida_Norte` (`ObstacleMarker` con vida 900, `OperacionBuilder.Centro.cs:39`, bug #159) y lleva el rotulo "SALIDA NORTE · DEMOLER" (linea 40).
- La presentacion del jefe arranca cuando cae el muro (`Jefe.cs:184-187`, bug #243; `ZParaLaToma = -30`).

Que debe cambiar: el muro deja de ser una tarea del ASALTO. El hackeo hace explotar la terminal y abre la salida norte. El objetivo 4 pasa a "Sali por la salida norte y avanza al patio".

**Cambio propuesto.**
1. Nuevo metodo `OperacionDirector.AbrirSalidaNorte(bool conExplosion)`, en `OperacionDirector.Centro.cs`.
   - Si `conExplosion`:
     - en la terminal, `ImpactFx.SpawnExplosion(pc + up*1.2, 3.2f, conCrater:false)`, que es solo visual y no hace dano;
     - `AudioDirector.PlayAt(SfxKind.Explosion, pc, 0.8f)`;
     - el `Monitor` pasa a material negro, se apaga `BarraProgreso`, `Luz_PC` parpadea 1,5 s y se apaga;
     - chispas en bucle durante 20 s (`SpriteFx.Lanzar("spark_*"...)` o el mismo efecto de `ImpactFx.SpawnSparks` si existe), mas humo `smoke_02`.
   - 1,2 s despues, tanto con explosion como sin ella:
     - `muroDeSalida.Demoler(desde: pc, fuerza: 6f)`, que reusa `ObstacleMarker.Demoler`/`Collapse` (`Presentation/ObstacleMarker.cs:170-234`). Eso ya incluye escombros, sonido, `NavService.Invalidate`, `NavMeshViva.Solicitar` y `Coberturas.AlDerrumbarse`;
     - sin explosion (restaurar partida o atajo de prueba) se llama al instante.
   - Se eligio la voladura porque reusa un camino probado (Fragmentador, NavMesh, coberturas, prueba Bug243a). Una compuerta corrediza seria codigo nuevo, con NavMesh, arte y guardado propios.
2. Llamadas:
   - en `TickCentro`, al `computadora.Completo` (lineas 156-161), `AbrirSalidaNorte(true)` ANTES de `EntrarFase(Huir)`;
   - en `EntrarFase`, case `Huir`, `if (MuroDeSalidaEnPie()) AbrirSalidaNorte(false)`. Cubre `OperacionPrueba.Arrancar(4,...)`, la restauracion de partida y `RestaurarBug`. Para que no se duplique con la llamada del paso anterior, `AbrirSalidaNorte` es idempotente con la bandera `salidaAbierta`.
3. Textos:
   - `OperacionDirector.cs:327`: Aviso("DATOS DESCARGADOS\nLA SALIDA NORTE SE ABRIO\nAVANZA AL PATIO: UN BLINDADO ENEMIGO CUSTODIA LA FUGA");
   - `Jefe.cs:183`: siempre "Sali por la salida norte y avanza al patio · hay un BLINDADO enemigo custodiando la fuga". La rama `muro ?` queda solo como respaldo si el muro sigue en pie: "La salida norte se esta abriendo...";
   - `Jefe.cs:180`: la baliza "MURO DE SALIDA" pasa a "SALIDA NORTE";
   - rotulo de la escena y del builder: "SALIDA NORTE · DEMOLER" pasa a "SALIDA NORTE" (Centro.cs:40, y por eval el `TextMesh` del rotulo en la escena).
4. Momento de la toma del jefe (`Jefe.cs:186-187`): `muroCaido && Reloj >= 3f || z > ZParaLaToma`. Asi se ve la explosion antes de la cinematica, que hoy arrancaria en el mismo frame.

**Riesgos y pruebas afectadas.**
- `ChecksBugs065.Bugs231a255.cs:192-210` (Bug243a) espera, tras `Arrancar(4,1)`, que el muro siga en pie y que la toma arranque al `Demoler`. Reescribirlo:
  - tras `Arrancar(4,1)`, `Muro_Salida_Norte` esta inactivo o `IsCollapsed` en menos de 1,5 s;
  - la toma arranca entre 2,5 y 6 s sin pasar z = -30;
  - Subfase == Acercamiento a 1 s.
- Prueba nueva (Bug291a), en el centro de datos: forzar `computadora.Avanzar(999)` y verificar en menos de 3 s:
  - `Fase == Huir`;
  - el muro cayo;
  - `AudioDirector.SonoRecien("Explosion", 30)`;
  - el monitor ya no tiene el material `Pantalla`.
- Guardado (`ChecksBugs104.Guardado.cs`): las partidas viejas de fase 4 con el muro en pie se arreglan solas por el paso 2, porque `EntrarFase(Huir)` lo abre.
- La IA del ASALTO podia tener una orden pendiente de "DEMOLER" sobre ese muro: `Demolicion.EsDemolible` devuelve `IsCollapsed`, asi que la cancela solo.
- `ArteDeOperacion.cs:235` viste `Muro_Salida_Norte`. Si sus piezas de arte no son hijas del muro, quedan flotando tras el colapso. Verificarlo en Play y, si pasa, agregar esas piezas a `ObstacleMarker.arte` (para que se apaguen con el muro) en el builder `SalaCubierta`.

**Verificacion en Play.**
- `RestaurarBug(291)`, o el atajo de prueba "objetivo 3" mas `computadora.Avanzar(29)`.
- Captura ANTES: el PNG con el objetivo "DEMOLER".
- Capturas DESPUES:
  1. 0,5 s despues de completar, la explosion de la terminal;
  2. 2 s despues, la salida norte abierta con escombros y el objetivo 4 con el texto nuevo.

---

### #292: "quita esas 2 torretas"

**Causa raiz confirmada.**
- `Editor/OperacionBuilder.Centro.cs:91` y `95-117` (`TorretasDelCentro`) instancian `P_Env_Emplazamiento_MG` como `Torreta_Centro_Oeste/Este`, en x ±8,5 y z = computadora + 10,5.
- El builder las cablea al director en `OperacionBuilder.cs:42` y `:383` (`dir.torretasDelCentro`).
- `OperacionDirector.cs:83` declara el campo y la `:221` llama a `TorretaFija.Instalar` (que agrega el rombo naranja de `InteractableDiamond`).
- Ademas `TorretaFija.InstalarEnEscena` instala TODOS los emplazamientos de la escena (ver el comentario en `HeadlessTestRunner.BusquedasGlobales.cs:33`). Desenchufarlas del director no alcanza: hay que borrar las instancias.
- Textos que las mencionan:
  - `OperacionDirector.Centro.cs:150` ("¡N vehiculos! usa la ametralladora");
  - `:254` ("USA LAS AMETRALLADORAS FIJAS");
  - `:255` ("TOMA UNA AMETRALLADORA FIJA [E]").

**Cambio propuesto.**
1. Escena, por eval o con el metodo idempotente `OperacionBuilder.Correcciones.QuitarTorretasDelCentro()` y el patron de marcador `__sin_torretas_centro`:
   - `DestroyImmediate` de `3_CentroDeDatos/TorretasDelCentro` y sus 2 hijos;
   - `OperacionDirector.torretasDelCentro = new GameObject[0]`;
   - `EditorSceneManager.MarkSceneDirty` y guardar.
2. Builder: borrar la llamada de la linea 91 y la funcion `TorretasDelCentro` con `PrefabEmplazamientoMg` (o dejarla vacia con un comentario del bug #292). El campo del director queda (null-safe) para no romper la serializacion.
3. Textos:
   - linea 150: "· ¡{n} vehiculos! cohetes o granadas";
   - linea 254: "¡VEHICULOS ENEMIGOS POR LA RUTA NORTE! COHETES Y GRANADAS";
   - linea 255: "¡VEHICULOS ENEMIGOS!\nUSA COHETES O GRANADAS".
4. Las camionetas siguen llegando (`LanzarCamionetasDelCentro`). Sin las MG del centro quedan como amenaza fuerte.
   - Bajar `camionetasPorTanda` en 1 si la prueba de juego muestra que es demasiado.
   - O dejar una `CajaDeSuministros` con cohetes en (0, -50).
   - Decision: probar primero sin cambiar el balance.
5. NavMesh: el emplazamiento tiene collider, y la malla se horneo con su hueco. Rehornear con `OperacionBuilder.HornearNavMeshEscenaAbierta()` (nunca `bake_navmesh` legacy) DESPUES de #291 y #289/#290, en un solo horneado.

**Riesgos y pruebas afectadas.**
- Ninguna prueba usa `torretasDelCentro` ni `Torreta_Centro_*` (grep en `Editor/` da solo el builder).
- `HeadlessTestRunner.Fases13a17.cs:229-249` y `Fase23.cs:621` crean su propia torreta de prueba: no se afectan.
- `ChecksBugs065.Wp6.cs:71/516` lanza camionetas del centro: no depende de las MG.
- `HeadlessTestRunner.BusquedasGlobales.cs:33` cuenta llamadas en `TorretaFija.cs`: no se toca ese archivo.
- `LocTextos.cs:228-230` ("AMETRALLADORA FIJA") es del tutorial: no se toca.

**Verificacion en Play.** Atajo "objetivo 4" con la camara del json 292. ANTES: 2 emplazamientos con rombos naranjas frente al porton. DESPUES: el patio sin ellos. Al hackear, los avisos sin "ametralladoras fijas".

---

### #293: "deberian aparecer los enemigos en el minimapa y yo y mi cono de vision" (manejando el tanque)

**Causa raiz confirmada** (json `bug_293`: jugador Vega, Gunner del `Tanque_Fuga`, que esta en (61,2, 222,5)).
1. **Observadores en la posicion de abordaje.**
   - `Presentation/WorldUiDirector.cs:299-311` (`RebuildFogObservers`) usa `s.transform.position` de cada soldado propio vivo. El comentario de la linea 297 asume que el montado "sigue viendo".
   - Pero el json muestra a Vega en (-12,3, 0,7, 29,1) "MONTADO" y a Kes y Doc tambien en (-14,33) y (-7,34): unos 200 m detras del tanque.
   - Asi, la niebla (`IsSpotted`, lineas 316-324) solo revela alrededor del punto donde subieron.
2. **Los enemigos tienen niebla.** `HeadlessTestRunner.cs:2201-2209` (fabrica de soldados, con la que se armaron los prefabs) llama a `minimapIcon.EnableFogOfWar()` en los enemigos, y `MinimapIcon.FogVisionRange = 10f` (`MinimapIcon.cs:65`). Revela 10 m: inutil a la velocidad del tanque y para enemigos en cornisas a 55 m (Enemigo#55 en (9,3, 6, 210,3)).

   OJO: `ChecksBugs065.Wp10.cs:424` afirma que "la niebla del minimapa NO esta activa en esta escena". Hay que medirlo en Huir (ver la sonda).
3. **Sin cono para el vehiculo propio.** `MinimapIcon.PintarVehiculo` (lineas 155-176) convierte el icono propio en triangulo amarillo pero nunca crea el cono (`CrearOActualizarConoDeVision` solo se llama para soldados, en las lineas 131 y 199). El triangulo rota con `Target.eulerAngles.y`, que es el casco, no la torreta.

**Sonda previa en Play** (atajo "objetivo 4" y `Embarcar()`). Desde eval, contar:
- `MinimapIcon` con `Target` en un `Soldier` enemigo activo y vivo;
- cuantos tienen `FogEnabled`;
- cuantos `IsRendered`;
- la escala de cada icono;
- la capa (`Minimap`).

Tambien leer `SP.UI.MinimapFollow.Activo.MinimapCamera.orthographicSize` y `cullingMask`.

**Cambio propuesto.**
1. `WorldUiDirector.RebuildFogObservers`:
   - pasar de `List<Vector3>` a `List<(Vector3 pos, float r2)>`;
   - soldado propio NO montado: radio `Mathf.Clamp(brain.EffectiveVisionRange, 18f, 35f)` (el valor de 10 m no se elimina, sirve de minimo);
   - soldado montado (`Brain.MontadoEnVehiculo`): se omite;
   - cada `Vehicle` con `Bando == Player` y ocupantes vivos: su posicion con radio 45 m (el tanque tiene periscopio y artillero).
   - Recorrer los vehiculos con `WorldSystemsRegistry` o `Vehicle.Todos` si existe. Si no, cachear la lista de vehiculos en `EnsurePopulated`.
2. Ademas, un enemigo que esta atacando al jugador (`Brain` con objetivo del bando Player y en estado Attack o Chase) se revela siempre (`IsSpotted` gana `|| icon.EnemigoEnCombate`). Es la regla clasica: "si te dispara, lo ves".
3. Cono del vehiculo propio, en `MinimapIcon.PintarVehiculo`:
   - si `mio`, `CrearOActualizarConoDeVision(ColorObjetivo)` con radio fijo de 45 m;
   - en `TickFollow`, rotar el cono (no el icono) con el yaw de mundo de `Tanque.TorretaCanon` si existe, y si no con el del casco;
   - al dejar de ser "mio", apagarlo;
   - `ActualizarRadioCono` usa 45 si `vehiculoMioPintado`.
4. El icono propio del vehiculo: el triangulo amarillo a escala 1,5 se mantiene (bug #224).

**Riesgos y pruebas afectadas.**
- `HeadlessTestRunner.Fases8a12.cs:603-640` prueba la niebla registrando un icono y llamando a `ApplyFog(IsSpotted)`. Con el radio minimo de 18 m, revisar que su enemigo "lejano" siga mas lejos de 35 m del observador; si no, alejarlo en la prueba.
- `ChecksBugs065.Wp10.cs:395-440` (Bug101i y W10EnemigosLejanosVisibles) cuenta iconos con niebla: sigue igual (`RevelarEnemigos` del campanario gana).
- Rendimiento: N vehiculos (menos de 10) en el pase de 4 Hz. Despreciable.
- `MinimapIcon` es `[ExecuteAlways]`: el codigo del cono de vehiculo debe estar protegido con `Application.isPlaying`, igual que `PintarVehiculo` en `TickFollow:525`.

**Verificacion en Play.**
- `RestaurarBug(293)` (TRAYECTO 25 S, Gunner).
- Captura DESPUES del minimapa: el triangulo amarillo con su cono amarillo hacia donde apunta el canon, y triangulos rojos en las cornisas (x ≈ 6 a 9, z ≈ 210 a 215) y las camionetas `Auto_Perseguidor_*` en rojo.
- Girar la torreta 90 grados: el cono gira y el triangulo sigue al casco.

---

### #294: "que las marcas sigan en el piso, no en el aire" (Resistir, ciudad)

**Causa raiz confirmada** (json: camara (256,2, 3,08, 234), yaw 140,6; `mira=Vehicle:Tanque_Fuga` destruido en (261,6, 0,7, 228)).
1. **El engranaje grande arriba** es el informativo de vehiculo DESTRUIDO: `PlayerInputDriver.cs:3188-3192`, con `ancla = vehiculo + up * AlturaGearVehiculo (3.6)` y `destacado=false`. No hay ninguna accion posible: es ruido.
2. **Los rombos azules en el aire** son los localizadores de aliado `Presentation/UnitLocatorCylinder.cs`:
   - linea 52: `const float Altura = 2.4f` sobre el pivote, que ya esta a 0,8 m, asi que quedan a unos 3,2 m del piso;
   - Update en las lineas 320-443;
   - se ven a traves de paredes;
   - en la captura son los de Kes (271,3, 222,7) y Doc (275,3, 231,4).

   `Player/AimTargeting.cs:38` usa `AlturaDelRombo = 2.4f, AnguloDelRombo = 3.2f` (lineas 50 y 141) para apuntar al rombo y poseer con F.
3. Los aros de sector ya estan apagados (`SectorDeDefensa.AroEnElPiso = false`) y las letras del piso estan a y 0,09: no son el problema.

**Cambio propuesto.**
1. `PlayerInputDriver.ActualizarPromptContextual`, case Vehicle: la rama `IsDestroyed` (3188-3192) pasa a `mostrar = false`.
2. Para todas las anclas de engranaje (constantes en 3106-3109 y los +2,6/+2,2 de 3209/3219), un helper `AnclaSobre(Transform t, float extra)`:
   - devuelve `min(bounds.max.y + 0.5, PisoReal + 2.6)`;
   - `bounds` es el de los renderers del objetivo, cacheado por transform;
   - asi ninguna marca flota mas de 0,5 m sobre el objeto ni mas de 2,6 m sobre el piso.
3. `UnitLocatorCylinder`, modo de dibujo segun la camara:
   - **FPS y TPS:**
     - el rombo pasa a "marca de piso": plano y echado (rotacion x 90), escala 0,9;
     - a `PisoReal(pos) + 0.05`, debajo del soldado, con ZTest normal (no a traves de paredes);
     - mismo color de equipo y pulso suave;
     - si el aliado esta ocluido y a menos de 40 m, se dibuja ademas una flecha de borde en la HUD (existe para enemigos de oleada: `FlechaDeOleada`; para aliados alcanza con nada, porque el pedido es "en el piso").
   - **RTS:** sin cambios (rombo alto). Lo exige `ChecksBugs065.Pendientes.cs:265-293`, Bug049rts: rombos RTS visibles con escala > 0,5.
   - Exponer `public Vector3 PosicionDelRombo` y hacer que `AimTargeting` (lineas 50 y 141) la use en vez de `AlturaDelRombo` fijo. En FPS el apuntado para poseer con F pasa a ser el cuerpo del soldado (o el disco del piso), con el mismo cono angular de 3,2 grados.
4. Dejar `AlturaDelRombo = 2.4f` como constante de RTS para no romper referencias.

**Riesgos y pruebas afectadas.**
- `ChecksBugs065.Pendientes.cs:265-293` (Bug049rts, `OperacionPrueba.Arrancar(5,1)` en RTS): sigue en el rombo alto.
- `HeadlessTestRunner.cs:2191` solo agrega el componente.
- Prueba nueva: buscar checks de posesion apuntando al rombo (`grep "AlturaDelRombo|AnguloDelRombo" Editor/`) y adaptarlos al nuevo `PosicionDelRombo`.
- `ChecksBugs065.Wp5b.cs:134-152` (giro del engranaje): sin cambios.
- Cuidado con `UnitLocatorCylinder` en soldados montados o caidos: se mantienen las reglas actuales de ocultar.

**Verificacion en Play.** `RestaurarBug(294)` con la camara del json. ANTES: el engranaje sobre el tanque y 2 rombos azules en el aire. DESPUES: sin engranaje, y 2 marcas azules echadas en el piso a los pies de Kes y Doc. Pasar a RTS (captura extra): los rombos altos siguen ahi.

---

### #295: rediseno de la defensa (Resistir)

**Nota del usuario, literal:** "Corrije q todos queden aqui y todos empiezen a atcar hacia aqui si o si q sea 1 solo puesto a defeneder y el civil en el centro y cada uno debo poder desplegarlo pero idealemnte deben quedarse alli y quiero q los enemigos aparescan en las azoteas y terrass como flancotiradores q aprescan de a poco y quiero ver desde donde vienen osea q aparescan desde afuera del mapa y vengan hacia a mi".

Captura: jugador en (309,227) mirando la plaza y el helipuerto "H" en (322,228).

**Como esta hoy (codigo real).**
- **Flujo con mando:** `Operacion/OperacionDirector.Mando.cs` (1448 lineas).
  - `IniciarMando` en la linea 199.
  - `VistaTacticaInicialDeResistir` (242-298) manda escuadra y milicia a los sectores A (322,292, amenaza -x) y B (272,228, amenaza +z), definidos en `Editor/OperacionBuilder.Mando.cs`, `SectoresDeLaDefensa`.
  - `PlanDeOleadas` (111-118): 5 oleadas por sector.
  - `TickMando` (542-617), `ActualizarAmenazas`, `ContarAliadosPorSector` y `TickCaidaDeSectores` (645-717).
  - `LiberarEnSector`: bocas a 24 a 80 m del lado de la amenaza. `TomarAtacante`: primero reservas, despues clon de plantilla.
  - `TickRumboDeLosAtacantes` (883-904), `TickCoberturaAutomatica` (919-951), `TickPosturaEnSector` (957-978), campanario (1017-1034).
  - HUD "Sectores cubiertos" (1219-1258).
  - `CapturarMando` y `RestaurarMando` (1330-1418).
- **`MandoTactico.cs`:**
  - `ModoDirecto = true`, `CantidadDeSectores = 2`, `SegundosDeAnuncio = 12`, `SegundosParaCaer = 10`, `AliadosParaRecuperar = 1`;
  - 4 milicianos;
  - texto "CUBRI LOS 2 PUESTOS · A Y B".
- **Flujo viejo sin mando** (`OperacionDirector.Resistir.cs`, `ResistirSinMando`):
  - `ArmarCoberturasDeLaPlaza`: anillo de 10 barricadas a R 12,5 alrededor de la plaza. Es REUTILIZABLE.
  - `BocasDeLaCiudad`, `TickRefuerzosDeLaCiudad`, `LiberarEnLaCiudad`, `VolarHeli`, `segundosDeResistencia`.
- `DefensaDelCentro.cs` es la defensa del HACKEO (torretas `TorretaDeDefensa`, helis apagados). No se usa aca: solo se toma su patron de "anillos de ataque" como referencia.
- **Geografia** (`OperacionBuilder.Ciudad.cs` y `CierreCiudad.cs`):
  - plaza de 34 x 34 centrada en (322,228);
  - `LimiteDeLaZonaJugable = Rect(258,158,128,146)`;
  - muralla de `Relleno_*` de 13 a 22 m al norte (z 313-327), al sur (129-143), al oeste (x 233-247, con la autopista libre en z 208-248) y al este (x 390-404), con huecos de 0,3 m, es decir NO transitable;
  - la UNICA entrada real desde afuera es la autopista oeste;
  - reservas `Reserva_Ciudad1_*` en (372-376, 222-234), `Ciudad2` en (316-328, 284-287) y `Ciudad3` en (316-326, 170-174).
- No existe ningun civil en SC_Operacion.

**Diseno nuevo** ("UN puesto: LA PLAZA").

1. **Un solo sector.**
   - `MandoTactico.CantidadDeSectores = 1`.
   - En `OperacionBuilder.Mando.SectoresDeLaDefensa`, un unico `SectorDeDefensa` "PLAZA" en el centro (322,228), radio de captura de 16 m, letra "P" en el piso a y 0,09 (`LetraEnElPiso`).
   - La amenaza es "todas las direcciones": `SectorDeDefensa.DireccionDeAmenaza = Vector3.zero` y su uso pasa a "hacia el centro".
   - Los sectores A y B se desactivan en la escena (no se borran, para que las partidas viejas no rompan indices) y se excluyen del arreglo `sectoresDeDefensa`.
   - El HUD "Sectores cubiertos" (1219-1258) pasa a "PLAZA: N defensores · CIVIL: vida%".
2. **Puestos fijos en la plaza.** Reusar `ArmarCoberturasDeLaPlaza` (`OperacionDirector.Resistir.cs`) para crear o asegurar el anillo de 10 barricadas a R 12,5.
   - Mejor horneado en un builder idempotente, `Editor/OperacionBuilder.DefensaDeLaPlaza.cs` y grupo `9_DefensaPlaza`, con `Saco`/Jersey con collider y `NavMeshModifier` normal, asi que hay que rehornear.
   - Cada barricada aporta 1 o 2 `PuntosDeCobertura` con la normal hacia afuera.
   - `VistaTacticaInicialDeResistir` ya no reparte A/B: asigna a cada aliado (escuadra y 4 milicianos) un punto de cobertura del anillo, el mas cercano libre, y le da la orden de mover y ponerse a cubierto (`OrderService.IssueMoveOrder` mas postura Defensiva).
3. **"Cada uno debo poder desplegarlo, pero idealmente quedarse alli."**
   - Las ordenes RTS del jugador mandan sobre todo: el aliado va adonde se lo mande y se queda mientras la orden este activa.
   - **Ancla.** `TickPosturaEnSector` pasa a `TickAnclaDeLaPlaza`: si un aliado SIN orden activa del jugador (o con la orden cumplida hace mas de 20 s) esta a mas de 22 m del centro, vuelve a su punto de cobertura. Ordenes nuevas reinician el reloj.
   - El poseido por el jugador nunca se ancla.
   - Constantes nuevas en `MandoTactico`: `RadioDeAncla = 22f`, `SegundosAntesDeVolver = 20f`.
4. **Civil al centro.** Nuevo `Operacion/CivilDeLaPlaza.cs`, que copia el patron de `MisionDirector.AparecerCivil` (lineas 579-600).
   - Instanciar `P_Soldier_Ally.prefab` y llamar a `Configure("Civil", TeamId.Player, RoleType.Civilian, 600)`.
   - `Brain.Pasivo = true` y `Brain.Quieto = true`; componente `Player/Rehen.cs`.
   - Posicion: (322, ~0,08, 231), 3 m al norte de la H, para no estorbar el aterrizaje.
   - Animacion agachado (`Motor.SetCrouching(true)`).
   - Decision de la ambiguedad: el civil es VULNERABLE con 600 de vida y dano recibido x0,5.
     - Los francotiradores no lo eligen: `TiradorDeTorre.Valido` ya excluye `RoleType.Civilian`.
     - Las balas perdidas y las granadas si le pegan.
     - Si muere: `EntrarFase(Derrota)` con "EL CIVIL MURIO".
     - La constante `CivilVulnerable = true` permite apagarlo si el balance lo pide.
   - En Extraer, el civil sube al helicoptero con la escuadra: se lo agrega a la lista de embarque, o se lo teletransporta al asiento al despegar.
5. **"Todos atacan hacia aqui si o si."**
   - En `LiberarEnSector` y `TickRumboDeLosAtacantes`, el destino es SIEMPRE el centro de la plaza mas un jitter de ±6 m.
   - Los atacantes de Resistir se sacan de `CazaDeEnemigos` (`CazaDeEnemigos.Marcar` no se aplica en Resistir), asi no se van a cazar a un aliado desplegado lejos.
   - Solo cambian de blanco a un aliado si esta a menos de 25 m de su ruta (la IA normal de combate ya lo hace).
6. **"Que aparezcan desde afuera del mapa y vengan hacia mi"** (oleadas a pie y en camioneta).
   - **Bocas de entrada nuevas, Fase A (sin tocar la muralla):**
     - 4 puntos en la franja ENTRE las ultimas casas y la muralla de relleno, fuera de `LimiteDeLaZonaJugable` y sin vista desde la plaza: norte en unos (322,309), sur en (322,150), este en (387,228) y oeste por la autopista en (250,228), donde entran las camionetas (`CallesDeCamioneta`);
     - validar cada una con `NavMesh.SamplePosition(<=3 m)` y `NavMesh.CalculatePath(boca, plaza) == PathComplete`;
     - si alguna no conecta, correrla al tramo de calle mas cercano que si conecte. La validacion va en el builder, que loguea las coordenadas finales.
   - **Fase B (opcional, solo si en la prueba de juego las de la Fase A "no se leen como afuera"):**
     - abrir 3 callejones de 7 m en la muralla (norte y sur en x ≈ 322, este en z ≈ 228): partir el `Relleno_*` que cruza el eje en dos mas angostos;
     - agregar un piso con collider de 25 m afuera (incluido en el NavMesh) y un arco de porton decorativo;
     - rehornear. Costo: rehorneado completo y riesgo de que la camara tactica vea el fondo `Fondo_Horizonte`.
   - **Marcha visible:**
     - los atacantes aparecen en la boca durante el anuncio de 12 s (`SegundosDeAnuncio`) con `FlechaDeOleada` apuntando a esa boca y el aviso "OLEADA N · DESDE EL NORTE";
     - caminan (no corren) los primeros 25 m (`Motor.SetRunning(false)`) y despues cargan;
     - mientras marchan se revelan en el minimapa (`WorldUiDirector`: el flag `EnemigoEnMarcha`, analogo al de #293).
   - **`PlanDeOleadas` nuevo** (reemplaza A/B por bocas; mismas fracciones 0,13 a 0,84 de `segundosDeResistencia`):
     1. N, 5 a pie;
     2. E, 6 a pie;
     3. O (autopista), 4 a pie y 1 camioneta;
     4. N y S, 4 y 4;
     5. E y O, 5 a pie y 1 camioneta.
   - `TomarAtacante` se mantiene: primero reservas `Reserva_Ciudad*`, que se TELETRANSPORTAN a la boca al liberarse, y despues clones de la plantilla.
7. **Francotiradores en azoteas y terrazas, "de a poco".**
   - Nueva subclase `Operacion/FrancotiradorDeAzotea.cs : FrancotiradorEnTorre`. Hoy `Dano` es `const 95` y `Equipar` es `protected override`.
     - Agregar `protected virtual int DanoPorTiro => Dano;` y `protected virtual float CadenciaDeTiro => Cadencia;`, y usarlos en `Equipar`.
     - En la subclase: dano 40, cadencia 4,0 s, alcance 75 m. Se mantiene la telegrafia de laser de 1,0 s.
   - Puestos: 6 `PuestoDeAzotea` (Transform con `pies` y `yawBase` hacia la plaza) en techos de `Casa_*` (5 a 9 m) y de `Edificio_Borde` (10 a 14 m), a 35 a 60 m de la plaza, repartidos en los 4 cuadrantes.
     - Los genera el builder `DefensaDeLaPlaza`, que toma los bounds reales de cada edificio y pone `pies.y = bounds.max.y` (encima del parapeto ArteOp si existe).
     - Verificar la linea de tiro a la plaza con `NavService.HayLineaDeTiro`.
   - Aparicion gradual:
     - el primero a los 25 s; despues uno cada 30 a 40 s; maximo 3 vivos a la vez; total 6;
     - cada uno "asoma": nace 1,2 m por debajo del borde y sube en 1,0 s (`PuestoElevado.Montar` con pies interpolados);
     - aviso "FRANCOTIRADOR EN LA AZOTEA · NORESTE";
     - icono rojo en el minimapa, siempre revelado mientras el laser esta activo.
   - Soldados: plantilla enemiga clonada fuera de la vista y montada con `PuestoElevado.Montar` (el `NavMeshAgent` se apaga). No necesitan NavMesh en el techo.
8. **Fin y caida.**
   - La plaza "cae" si durante `SegundosParaCaer` (10 s) no hay ningun aliado vivo dentro de los 16 m y hay al menos un enemigo dentro. `TickCaidaDeSectores` funciona igual con 1 sector.
   - Caida o muerte del civil: Derrota.
   - Al cumplir `segundosDeResistencia` llega el heli (`ArrancarHeli`, sin cambios) y se pasa a Extraer.
   - El texto de `OperacionDirector.cs:338` pasa a "TANQUE DESTRUIDO\nDEFENDE LA PLAZA Y AL CIVIL HASTA QUE LLEGUE EL HELICOPTERO".
9. **Guardado** (`CapturarMando` y `RestaurarMando`, y `Core/PartidaGuardada.cs`).
   - Se conservan: `relojEfectivo`, `radioTomada`, `pasosMando`, `operador`, `primerIngreso`, `oleadasMandoHechas`, `relojOleada`, `milicianos`.
   - `sectoresCaidos` sigue siendo una lista de indices: con 1 sector es "0" o vacio.
   - `unidadA` y `unidadC` se siguen leyendo pero se ignoran (compatibilidad). `ordenSector` pasa a `anclas` = "nombre=indicePuntoCobertura;...".
   - Claves nuevas:
     - `civilVida` (float) y `civilVivo`;
     - `azoteasActivadas` (mascara de bits int de 6) y `azoteasMuertas` (mascara);
     - `proximaAzotea` (reloj).
   - Al restaurar:
     - se recrea el civil con su vida;
     - los francotiradores activados y vivos se montan de nuevo; los muertos no vuelven;
     - las oleadas en curso se rebobinan como hoy (`relojOleada`).
   - Las partidas viejas (con A/B) cargan sin claves nuevas: civil a vida completa y azoteas en 0.

**Riesgos y pruebas afectadas** (todas estas suites hay que revisarlas y adaptarlas, no borrarlas):
- `ChecksBugs104.Resistir.cs`:
  - Bug120, 120b, 120c, 120d, 122 y 114;
  - las lineas 341 y 355 usan `CantidadDeSectores * AliadosParaRecuperar` (con 1 sector siguen siendo validas, pero revisar el umbral `libres.Count >= 6`).
- `ChecksBugs065.Wp10.cs`:
  - Bug101a, b, c, d, e, i, j, pa, pb, g y h;
  - en la linea 245, `capSectores` y `FlechaDeOleada` siguen existiendo;
  - Bug101i usa el campanario y `RevelarEnemigos`: se conserva el campanario.
- `ChecksBugs065.Bugs231a255.cs:254`: lee `sectoresDeDefensa` por reflexion y espera 2. Pasar a esperar 1.
- `ChecksBugs065.Pendientes.cs:265-293` (Bug049rts, `Arrancar(5,1)`): rombos RTS. Sin cambios.
- `ChecksBugs104.Guardado.cs`: Bug120s, t, u, z, v, q, w, x e y. Hay casos de guardado 5.1 con 2 sectores: actualizarlos a 1 sector y agregar un caso con civil herido y 2 azoteas activas.
- `ChecksBugs104.Extraer.cs:89-114` (bajas del heli en Extraer): el civil no debe contar como blanco ni como baja.
- `ChecksBugs065.Wp8.cs` (FrancotiradorEnTorre del cuartel): la subclase no debe cambiar el comportamiento del base. Solo se agregan propiedades virtuales con los mismos valores.
- NavMesh: el anillo de barricadas (si se hornea) y la Fase B exigen rehornear. Verificar `CalculatePath` desde las 4 bocas y desde las 3 reservas.
- Rendimiento: con 5 oleadas, 6 azoteas y la milicia, el pico de enemigos vivos queda en unos 18 o menos. Medir fps en la plaza (referencia del json: unos 78).

**Verificacion en Play.**
- `RestaurarBug(295)` o el atajo "objetivo 5" (`OperacionPrueba.Arrancar(5,1)`).
- Captura DESPUES 1, en (309,227) yaw del json: el civil agachado junto a la H, la escuadra y los milicianos en el anillo de barricadas, y la "P" en el piso.
- Captura 2: la oleada del norte entrando por la boca con la flecha.
- Captura 3: el laser de un francotirador en una azotea.
- Captura 4: el minimapa con los enemigos en marcha.
- Prueba de despliegue: mandar a Kes a 40 m con una orden RTS. Se queda mientras dura la orden; 20 s despues de cumplirla vuelve a su barricada.

---

### #296: "se rompio la geometria del edificio, reparalo"

**Causa raiz** (identificada; falta confirmar el mecanismo con una sonda).
- **Datos del json.** Camara en (287,2, 3,03, 238,8), yaw 338; jugador en (285,3, 242,3); mira `Cubrirse` en (287, 0, 255).
- **Edificio.** `Casa_1` (`Editor/OperacionBuilder.Ciudad.cs`, arreglo de casas en las lineas 300-306): centro (280,255), 12 x 5 x 10, es decir x 274-286 y z 250-260.
- **Lo que muestra la captura.**
  - Se ve el armazon: 4 pilastras de esquina, cornisa, zocalo, marcos oscuros de ventana con `VentanaOscura`, y ventanas iluminadas flotando.
  - Ese armazon es exactamente el arte de `PulidoFinal.ArteCiudadPF` (`Editor/OperacionBuilder.PulidoFinal.cs:201-302`: ventanas en `b.max/min ± 0.03`, Marco, Cornisa, Zocalo, Pilastra x4, Toldo, equipo de techo) mas el de `ArteDeOperacion.VestirEdificio` (`ArteDeOperacion.cs:360-395`: zocalo, cornisa, parapeto y pilares `P_Mod_Muro_Pilar`).
  - Falta el cubo de la casa: su MeshRenderer no dibuja.
  - La captura de #294 muestra el mismo aspecto en otras casas al sureste (`Casa_9`, `Casa_11` y `Casa_12`, a confirmar).
- **Descartado** (revisado en el codigo):
  - demolicion y atropello: las casas son `Edificio` indestructible 999999;
  - dano de proyectil;
  - `TransparenciaDeOclusores` (seleccion 0, camara a 4 m);
  - Fusion (salta los objetos con collider);
  - el winding de `MallaDeCaja`;
  - oclusion horneada (no hay carpeta de lighting de SC_Operacion).
- **Sospechoso principal.** Las limpiezas de escena hechas por eval sin codigo, que tocaron solo el binario:
  - ef1f23dc "casas duplicadas";
  - 08317863 "elimina mallas duplicadas restantes";
  - dc75489d (desactiva `Relleno_28/1`).

  Si una casa y un `Relleno` o una pieza de ArteOp tenian bounds parecidos, la limpieza pudo apagar el `MeshRenderer` (o el GameObject) de la casa equivocada. Alternativas: `sharedMesh` en null (la `MallaDeCaja` cacheada destruida) o material roto.

**Sonda en el editor (Edit mode, con la escena abierta).** Recorrer `6_Ciudad` y para cada `Casa_*`, `Edificio_Borde_*`, `Ayuntamiento` y `Relleno_*` loguear:
- `activeSelf`;
- `MeshRenderer.enabled`;
- `sharedMesh` (nombre y si es null);
- `sharedMaterials` (nombres y nulls);
- `renderer.bounds`;
- `BoxCollider.enabled` y su `size`;
- `forceRenderingOff`;
- `shadowCastingMode` (ShadowsOnly hace que solo se vea la sombra).

Comparar contra la escena de antes de las limpiezas:
- `git show 2d39457f:Assets/_Project/Scenes/SC_Operacion.unity > Assets/_Tmp/SC_Op_2d39457f.unity`;
- abrirla aditiva (`EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)`), correr la misma sonda y diferenciar;
- cerrarla SIN guardar y borrar `Assets/_Tmp` (con su `.meta`).

**Cambio propuesto.**
1. Metodo idempotente `OperacionBuilder.Correcciones.RepararCasasSinCuerpo()` con el marcador `__casas_ok`. Para cada `Casa_*` y `Edificio_*` de la ciudad que tenga collider pero no dibuje (`!renderer.enabled`, mesh null, `ShadowsOnly`, material null o `forceRenderingOff`):
   - reponer `enabled = true` y `ShadowCastingMode.On`;
   - reponer la malla `MallaDeCaja()` (o `Resources.GetBuiltinResource<Mesh>("Cube.fbx")`) y el material de su tipo (`Mat` de la fachada de `Ciudad.cs`).
2. Si la limpieza apago un `GameObject` de casa entero (y lo que queda es ArteOp y PF huerfanos): reactivarlo, verificar que no haya otro cuerpo superpuesto con bounds iguales a ±0,05 m, y si lo hay apagar EL OTRO.
3. Loguear la lista de reparados. Si son mas de 3, revisar a mano 2 vistas por casa.
4. No hace falta regenerar PulidoFinal: el armazon ya esta bien ubicado sobre los bounds de la casa.

**Riesgos y pruebas afectadas.**
- Las casas tienen collider. Si ademas se reactiva un GameObject, cambia el NavMesh: rehornear y probar `CalculatePath` en la calle de Casa_1.
- Si se reactiva un cuerpo duplicado, puede volver el z-fighting que motivo la limpieza: la verificacion del paso 2 lo evita.
- No hay pruebas que toquen `Casa_*` por nombre (verificar con `grep "Casa_" Editor/ChecksBugs*`). Si hay alguna que cuente edificios, revisarla.

**Verificacion.** En Edit mode, una camara temporal con RenderTexture (receta de capturas limpias) en la camara del json 296 y en la del 294. DESPUES: fachadas solidas con las ventanas pegadas. En Play, `RestaurarBug(296)`.

---

### #297: "el sonido de disparos del helicoptero es molesto, reemplazalo por uno mejor"

**Causa raiz confirmada.**
- `Mision/Helicoptero.cs:549-566` (`Disparar`, ametralladoras de puerta del bug #076). Cada bala, a 8 por segundo por pivote (`CadenciaPorSegundo = 8`, linea 510) y con 2 pivotes, reproduce `GenericSfx.GetWeaponShot(WeaponKind.Heavy)` (linea 562).
- Ese clip sale de `Resources/Audio/Sfx/Shot_Heavy/{Shot_Heavy_CD.wav, Shot_Heavy_Mossberg.wav}`, pasado por `Distorsionar` (`Presentation/GenericSfx.cs:212-243`). Una escopeta distorsionada 16 veces por segundo, a volumen 0,6 y con perfil `PerfilEspacial.Disparo`.
- Captura: el heli de extraccion de Resistir, que dispara cuando `ScriptHeli.DisparaCobertura = true` en `ArrancarHeli`.

**Cambio propuesto.**
1. Clip de rafaga propio. Sintetico, en `Presentation/SfxSintetico.Operacion.cs`: `public static AudioClip RafagaDeHeli()`, de 1,2 s (igual a `DuracionDeRafaga`):
   - 10 golpes por segundo de un "thump" grave: seno de 90 Hz con caida de 18 ms mas un ruido filtrado pasa-bajos de unos 1,2 kHz de 12 ms;
   - jitter de ±6 ms entre golpes y ganancia por golpe al azar entre 0,8 y 1;
   - pico de 0,7, sin distorsion, con cola de 150 ms.
   - Con prioridad para un `.wav` real bajo `Resources/Audio/Sfx/RafagaHeli/` (mismo mecanismo de `GenericSfx.PickReal`), asi el usuario puede soltar uno de una minigun o M60. Agregar la carpeta a `Editor/AudioImportFix.cs:20`.
2. En `Helicoptero.TickPivote`, al empezar una rafaga (donde se fija `e.rafagaHasta`, unas 1,2 s), un SOLO `AudioDirector.PlayClipAt(RafagaDeHeli(), p.Boca.position, 0.32f, ...)` con min y max distance de 10 y 140 y pitch de 0,95 a 1,05.
   - Si el otro pivote empezo una rafaga hace menos de 0,4 s, el segundo no suena (alcanza con un sonido "de helicoptero").
   - En `Disparar`, quitar la linea 562-563. Conservar fogonazo, luz, casquillos y el evento `AlDisparar` (lo usa `ChecksBugs065.Wp7.cs:192`).
3. `CoberturaFija(dt)` (heli sin pivotes): aplicar el mismo criterio si hoy dispara sonido por bala.

**Riesgos y pruebas afectadas.**
- `ChecksBugs065.Wp7.cs:192-217` escucha `AlDisparar`: se conserva.
- `ChecksBugs104.Extraer.cs:89-114` cuenta `DisparosHechos`: se conserva.
- Buscar con `SonoRecien("Shot_Heavy"` si alguna prueba espera el sonido del heli (grep: 0). Prueba nueva: tras 3 s de cobertura, `AudioDirector.Historial` contiene "RafagaHeli" y como mucho 1 entrada cada 0,8 s.

**Verificacion.** Es auditiva. En Play, `RestaurarBug(297)` y escuchar 10 s. El sonido pasa de un tableteo agudo y saturado a una rafaga grave y espaciada. Captura de `AudioDirector.Historial` (eval) antes y despues: de unas 16 entradas por segundo a 1 por rafaga.

---

## 2. Orden de ejecucion, dependencias y paralelismo

Restricciones de la herramienta:
- Hay UN solo editor y UNA sola escena binaria.
- Se puede escribir codigo en paralelo entre pistas que no comparten archivos.
- La compilacion, los retoques de escena, los rehorneados, la suite y las capturas se hacen en serie, en una cola de integracion.

**Paso 0. Base.**
- `git status` limpio.
- Copia de seguridad de `Assets/_Project/Scenes/SC_Operacion.unity` en `Temp/backup_SC_Operacion_<fecha>.unity`.
- Suite completa (`Strategic Point/Run All Tests Headless`, `Temp/suite_result.txt` en OK) para tener la linea base; despues `git checkout` de SC_TestLevel si la suite la ensucio.
- Medir fps en 4 camaras (cuartel, centro de datos, tanque, plaza).

**Paso 1. Sondas, solo lectura** (en serie, en el editor):
- 296: sonda de casas y diff con 2d39457f;
- 293: iconos con niebla en Huir;
- 285: distancias camara-piso;
- 289/290: piezas de ArteOp sobre `CD_*` y `ObstacleMarker.arte` de `Muro_Salida_Norte`.

**Paso 2. Pistas de codigo en paralelo** (archivos disjuntos dentro de cada pista):

| Pista | Bugs (en orden) | Archivos |
|---|---|---|
| A, aislados | 297, 288, 285 | `Mision/Helicoptero.cs`, `Presentation/SfxSintetico.Operacion.cs`, `Editor/AudioImportFix.cs`; `Tutorial/TutorialBeacon.cs`, `Operacion/OperacionDirector.Puestos.cs`, el `PonerBaliza` de `OperacionDirector.cs` (*); `Operacion/ReflectorVigia.cs`, `Editor/OperacionBuilder.Cuartel.cs`, `Assets/Settings/PC_RPAsset.asset` |
| B, marcas | 287, despues 286, despues 294 | `Core/PisoReal.cs` (nuevo), `Presentation/ImpactFx.cs`, `DestruccionDeVehiculo.cs`, `DecalPool.cs`, `Player/MunicionPickup.cs`, `Player/PlayerInputDriver.cs`, `Presentation/UnitLocatorCylinder.cs`, `Player/AimTargeting.cs`, `Presentation/InteractableDiamond.cs` |
| C, centro de datos | 292, despues 291, despues 289/290 | `Editor/OperacionBuilder.Centro.cs`, `OperacionBuilder.cs`, `OperacionBuilder.Correcciones.cs`, `Operacion/OperacionDirector.Centro.cs`, `OperacionDirector.Jefe.cs`, `OperacionDirector.cs` (*), `Editor/OperacionBuilder.Servidores.cs`, `OperacionBuilder.PulidoFinal.cs` (solo constantes de Remates/LineaLuz), `OperacionBuilder.SalaCubierta.cs` (nuevo), `Presentation/TechoOcultable.cs` (nuevo) |
| D, minimapa | 293 | `Presentation/WorldUiDirector.cs`, `Presentation/MinimapIcon.cs` |
| E, defensa | 295 | `Operacion/OperacionDirector.Mando.cs`, `MandoTactico.cs`, `SectorDeDefensa.cs`, `OperacionDirector.Resistir.cs`, `FrancotiradorEnTorre.cs` (solo virtuales), `FrancotiradorDeAzotea.cs` (nuevo), `CivilDeLaPlaza.cs` (nuevo), `Editor/OperacionBuilder.Mando.cs`, `OperacionBuilder.DefensaDeLaPlaza.cs` (nuevo) |
| F, escena | 296 | Solo `Editor/OperacionBuilder.Correcciones.cs` (metodo nuevo) mas el retoque de escena |

(*) `OperacionDirector.cs` lo tocan A (la firma de `PonerBaliza`), C (avisos de Huir y torretas) y E (el texto de Resistir en la linea 338). Lo edita SOLO la pista C. A y E le pasan sus cambios de una linea a C, o los hacen despues de que C mergee.

`OperacionBuilder.Correcciones.cs` lo tocan C (`QuitarTorretasDelCentro`) y F (`RepararCasasSinCuerpo`): metodos separados, pero hay que mergear en serie.

`PulidoFinal.cs` solo lo toca C. F no lo necesita.

`MinimapIcon.cs` y `WorldUiDirector.cs` los toca D. E necesita el flag "enemigo en marcha revelado": se hace en D como API generica (`WorldUiDirector.Revelar(Soldier, float segundos)`) y E solo la llama. Por eso D va antes que E.

**Paso 3. Cola de integracion** (en serie, de menor a mayor riesgo):
1. A, mas compilar, mas la suite parcial de Wp8 y Fase19, mas las capturas de 285, 288 y 297.
2. B, mas las suites Wp6, Fase23, Pendientes y Wp5b, mas las capturas de 287, 286 y 294.
3. D, mas las suites Fases8a12 y Wp10, mas la captura de 293.
4. F, la sonda y el retoque de casas, mas la captura de 296 en Edit mode y en Play.
5. C, en este orden:
   - escena: quitar torretas;
   - codigo de 291;
   - builder `SalaCubierta` (muros, techo, servidores, remates, emision);
   - UN solo rehorneado (`HornearNavMeshEscenaAbierta`);
   - verificar `CalculatePath` (entrada sur a la computadora, y la computadora al patio norte con el muro caido);
   - capturas de 289/290, 291 y 292;
   - Bug243a reescrito y Bug291a nuevo.
6. E, el mayor:
   - builder `DefensaDeLaPlaza` (anillo, sector unico, bocas validadas, puestos de azotea);
   - rehorneado;
   - codigo y pruebas (Resistir, Wp10, Guardado, Bugs231a255:254, Extraer);
   - las 4 capturas de 295.
7. Suite completa en modo estricto (`Temp/suite_result.txt` en OK), fps en las 4 camaras del paso 0 y hoja comparativa ANTES/DESPUES por bug, con las mismas camaras de los json.
8. Commit y push: solo si el usuario lo pide (confirmar antes).

---

## 3. Ambiguedades del pedido y la decision tomada

1. **#286 "deberia estar arriba":** el rombo de municion se para a unos 0,85 m sobre el piso, en lugar de echado en el piso, y sin engranaje. No "arriba del todo" como el engranaje a 1,1 m o mas.
2. **#287 "re crea esta misma situacion":** se reproduce con `RestaurarBug(287)` y una explosion junto a sacos y municion. No se pide un escenario nuevo.
3. **#288 "ese circulo":** se elimina el anillo de la baliza. La placa cuadrada del punto de carga se conserva salvo que la captura demuestre que tambien molesta.
4. **#290 "mas altura":** se aplica a la sala del centro de datos, que pasa de 5 a 8 m de muro y techo. No se aplica a los gabinetes ni a la camara.
5. **#291 "abrir la puerta":** la salida norte se abre por voladura automatica (`Demoler`) tras la explosion de la terminal, en vez de con una compuerta corrediza nueva. Es el camino de menos riesgo.
6. **#292:** solo las 2 MG del centro de datos (`Torreta_Centro_*`). Los demas emplazamientos del mapa (torres del cuartel, etc.) no se tocan.
7. **#293 "yo y mi cono":** el cono va en el icono del vehiculo cuando lo maneja el jugador, orientado a la torreta. A pie el cono ya existe.
8. **#294 "en el piso":** en FPS y TPS las marcas de aliado van al piso. En RTS se mantienen altas, porque es la vista tactica y la protege una prueba.
9. **#295 "aparezcan desde afuera del mapa":** con la Fase A, las bocas quedan fuera del limite jugable y detras de las ultimas casas, sin abrir la muralla. La Fase B (abrir callejones) solo si la prueba de juego lo pide.
10. **#295 "civil en el centro":** el civil es vulnerable y, si muere, es derrota. Queda apagable con la constante `CivilVulnerable`.
11. **#295 "desplegarlo pero quedarse alli":** las ordenes del jugador se respetan. 20 s despues de cumplidas, o sin orden y a mas de 22 m, el aliado vuelve a su barricada.
12. **#295 "francotiradores de a poco":** 6 en total, uno cada 30 a 40 s desde los 25 s, como mucho 3 a la vez, y con menos dano que el de la torre (40 contra 95).
13. **#297 "uno mejor":** un clip de rafaga grave y sintetico, con prioridad para un `.wav` real si el usuario suelta uno en `Resources/Audio/Sfx/RafagaHeli/`.

---

## 4. Checklist final de aceptacion

- [ ] 285: desde la camara del json, el haz termina en el muro y el piso del otro lado no queda iluminado. Pasan Wp8:131 y Wp8:289. Los fps no caen mas de 8 %.
- [ ] 286: la municion caida muestra un solo rombo de color municion parado a unos 0,85 m y ningun engranaje.
- [ ] 287: tras una explosion junto a sacos y escombros, la municion queda a menos de 0,15 m del piso real 6 s despues. Las quemaduras quedan a menos de 0,05 m del piso real y no hay bloques de crater. El check Wp6 esta actualizado y pasa.
- [ ] 288: no hay circulo amarillo en CARGA OESTE ni en CARGA ESTE. El minimapa sigue marcando el puesto.
- [ ] 289: la sala del centro de datos esta techada. Las lineas de luz de los muros, los LEDs y los paneles tienen el brillo bajado. En RTS el techo no tapa la vista.
- [ ] 290: los muros de la sala miden 8 m; el arte de techo y los remates estan reubicados, sin piezas flotando ni enterradas.
- [ ] 291: al completar el hackeo, la terminal explota (efecto y sonido) y la salida norte se abre sola. El objetivo 4 ya no menciona DEMOLER. Pasan Bug243a (reescrito) y Bug291a (nuevo).
- [ ] 292: no quedan `Torreta_Centro_*` en la escena ni rombos naranjas frente a la salida. Ningun texto menciona "AMETRALLADORAS FIJAS" en el centro de datos.
- [ ] 293: en el tanque, el minimapa muestra a los enemigos cercanos (en cornisas y camionetas) y el cono amarillo del jugador, que gira con la torreta.
- [ ] 294: en Resistir (FPS) no hay engranaje sobre el tanque destruido. Las marcas de aliado estan en el piso. En RTS los rombos siguen altos (Bug049rts en OK).
- [ ] 295:
  - un solo puesto (la plaza) y el civil al centro;
  - toda la escuadra y la milicia en el anillo al empezar;
  - todas las oleadas van a la plaza y entran por las bocas con flecha y marcha visible;
  - los francotiradores aparecen de a poco en las azoteas;
  - el despliegue manual funciona y los aliados vuelven solos;
  - guardar y cargar a mitad de la defensa restaura civil, azoteas y oleadas;
  - las suites de Resistir, Wp10, Guardado y Extraer estan en OK.
- [ ] 296: `Casa_1` (y cualquier otra casa afectada) se ve solida, con las ventanas pegadas a la fachada. La sonda no reporta casas con collider y sin render.
- [ ] 297: el heli de extraccion suena con una rafaga por tanda (no 16 golpes por segundo) y el volumen es mas bajo. Pasan Wp7:192 y Extraer:89.
- [ ] General:
  - suite headless estricta en OK (`Temp/suite_result.txt`);
  - consola sin errores ni warnings nuevos al entrar en Play en SC_Operacion;
  - `git status` sin SC_TestLevel modificada;
  - NavMesh verificado (`CalculatePath` en los 6 tramos clave: entrada del cuartel, puestos, entrada sur del centro de datos a la computadora, salida norte al patio, autopista a la plaza, y cada boca y cada reserva a la plaza);
  - hoja ANTES/DESPUES con las 13 parejas de capturas en las camaras de los json.

---

## 5. Trampas

- **T1.** No editar `.cs` con el editor en Play: el dominio recarga a mitad del Play y deja estados estaticos corruptos. Salir de Play, editar, esperar a que compile y despues entrar.
- **T2.** SC_Operacion se retoca EN SITIO con builders o metodos idempotentes (patron de marcador `__xxx_ok` de `OperacionBuilder.Correcciones`, o "borrar el grupo y rehacerlo" como `9_Servidores` y `9_PulidoFinal`). NUNCA correr `OperacionBuilder.Construir` (reconstruye la escena entera y borra los retoques hechos por eval).
- **T3.** La escena es BINARIA. No se puede grepear ni mergear: dos personas o agentes no pueden retocarla en paralelo. Antes de cada retoque, copia de seguridad; despues, guardar y hacer un commit chico.
- **T4.** NavMesh: rehornear solo con `LevelBlockoutBuilder.BakeNavMesh` / `OperacionBuilder.HornearNavMeshEscenaAbierta()`, nunca con `bake_navmesh` legacy. Juntar los cambios con collider (torretas, dintel, techo, barricadas, bocas) y hornear UNA vez por pista. Verificar con `CalculatePath`. El techo y el arte llevan `NavMeshModifier ignoreFromBuild`.
- **T5.** La suite abre SC_TestLevel y la puede dejar modificada: `git checkout` de esa escena despues de correrla. Resultado en `Temp/suite_result.txt`.
- **T6.** Los estaticos sobreviven entre Plays (`RevelarEnemigos`, `MandoTactico.*`, pools). Las pruebas nuevas deben resetearlos.
- **T7.** `PulidoFinalEscenaAbierta()` regenera las 4 secciones con un `System.Random(20261018)` COMPARTIDO. Si se agregan llamadas a `rnd` en `ArteCentroDeDatos`, cambian las ventanas y toldos de TODA la ciudad (`ArteCiudadPF` corre despues). O no se agregan llamadas a `rnd`, o se le da a cada seccion su propio `Random`. Para 289/290 conviene mover los remates en sitio por eval y cambiar solo las constantes del builder.
- **T8.** `Collider.bounds` y los raycasts quedan viejos hasta llamar a `Physics.SyncTransforms()` despues de escalar o mover por codigo (muros de 8 m, techo y casas reparadas).
- **T9.** El eval de la CLI corta a los 5 s pero la operacion sigue corriendo: no relanzar un rehorneado o un builder porque "dio timeout". Esperar y verificar el resultado (log, marcador, conteos).
- **T10.** Usar siempre `--project-path`. No llamar a `editor_focus`, que le roba el foco del sistema al usuario. Si el Play queda congelado sin foco, poner `Application.runInBackground = true` por eval.
- **T11.** Si en Play hay deriva, pausa o modo dios sin explicacion, es que el usuario esta jugando: frenar y preguntar.
- **T12.** `Mat(nombre, ...)` cachea por nombre. Bajar la emision de `LedCian` cambia todos sus usos: crear materiales nuevos "...Tenue" para la sala.
- **T13.** `TorretaFija.InstalarEnEscena` arma cualquier `P_Env_Emplazamiento_MG` de la escena. Para quitar torretas hay que borrar las instancias.
- **T14.** `MinimapIcon` es `[ExecuteAlways]`: el codigo nuevo de cono o niebla debe ir protegido con `Application.isPlaying` donde toque vehiculos, o el minimapa del editor cambia y queda guardado en la escena.
- **T15.** Partidas viejas: `RestaurarMando` debe aceptar claves faltantes (civil, azoteas) y claves sobrantes (`unidadA`/`unidadC`, `ordenSector` con 2 sectores) sin romperse.
- **T16.** La escena de comparacion de #296 (`Assets/_Tmp/SC_Op_2d39457f.unity`) se cierra SIN guardar y se borra con su `.meta` antes de cualquier commit.
- **T17.** Los objetos de prueba creados en Edit mode (camaras temporales, sondas) llevan `HideFlags.DontSave` y se destruyen al terminar. Un commit anterior guardo 49 camaras ZzCam en la escena y los fps bajaron de 76 a 5.
