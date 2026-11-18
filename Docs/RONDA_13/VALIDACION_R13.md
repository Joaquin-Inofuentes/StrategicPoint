# Ronda 13 — Validación ANTES / DESPUÉS

Método: la misma build (`StrategicPoint.exe -capturas`, 1600×900, semilla 1313) corrida sobre el commit `95b80a95` (ANTES, `Builds/R13_Antes`) y sobre la build final (DESPUÉS, `Builds/Windows64`).
Evidencia: `antes/` y `despues_final/` (67 tomas + `captura_info.txt`), atlas `ATLAS_A…E_*.jpg` (izquierda ANTES, derecha DESPUÉS), checks de editor `Bug958a…Bug987a`.
Leyenda: **OK** = medido y visto · **PARCIAL** = hecho pero con una parte sin cerrar · **ABIERTO** = no resuelto.

## Lista 1

| # | Pedido | Estado | Medida ANTES → DESPUÉS | Evidencia |
|---|---|---|---|---|
| 1 | Cuchillo mata de 1 golpe (pesado 2), sonido y sangre muy visible | **OK** | soldado 180→0 de 1 golpe (antes 180→120); pesado 2160→1080→0 en 2 golpes (antes 2160→2100); 48 partículas de sangre a los 0,1 s, clip `KnifeBlood` de 0,30 s y mancha en el piso; sin sangre si falla | `ATLAS_C` fila 1, `Bug971a` |
| 2 | Lanzacohetes mucho más fuerte, sobre todo contra el heli final | **OK** | daño por cohete al heli 22→495; mueren 4 cohetes en Fácil y Medio y 5 en Difícil (antes no moría con 12), comprobado por `Bug958a`. La toma de captura de Difícil falla cohetes contra el heli en movimiento y no sirve como medida | `captura_info.txt` `heli_cohetes.*`, `Bug958a` |
| 3 | Sliders de Configuraciones bien dibujados y coeficiente de daño global | **OK** | sección DAÑO dentro del panel: 100 % visible, 0 piezas fuera en 1280×720, 1600×900 y 1920×1080 (antes la tarjeta de 25 % visible quedaba fuera del borde); 12 vías de daño escaladas con 2× y 0,5× | `ATLAS_D` fila 1, `Bug972a`, `Bug973a` |
| 4 | Calles: sin calle de inicio, T tras las computadoras, mejor trazado | **OK** | calle del comienzo reemplazada por tierra, calle en T con transversal, cordones y veredas; 15 objetos del comienzo viejo apagados | `ATLAS_A` fila 1, `ATLAS_E` fila 2, `Bug978a`, `nivel_cenital_global_claro.png` |
| 5 | Mira del tanque fija por defecto + círculo de dónde vienen los tiros | **OK** | al subir: zoom 0→1,00, FOV 60→18,7 (clic derecho alterna); anillo de 320 px con marcas a −90° y 180° que desaparecen a los 2,1 s | `ATLAS_C` fila 2, `Bug974a`, `Bug975a` |
| 6 | Liberar celdas: cinemática de 2 s + feedback | **OK** | cinemática de 1,99 s (rango 1,8–2,2), reja se abre con sonido, aviso «2 MILICIANOS LIBERADOS», control devuelto | `ATLAS_C` fila 4, `Bug965a` |
| 7 | Geometría solapada en todo el nivel | **OK (con criterio)** | duplicados exactos 28→0; solapes totales C 4144→4133 y D 94→83; baldosas y MinimapIcon dejaron de ser apagados por error en juego (bug de `LimpiezaDeDuplicados`) | `solapes_antes.txt` / `solapes_despues.txt`, `Bug980a`. La categoría C son caras que se tocan entre módulos (aceptada por diseño, ver «Decisiones») |
| 8 | Perros en manada sin solaparse | **OK** | distancia mínima 0,00→0,93 m, ≥0,7 m en 100 % de los cuadros (antes 7,6 %), 0 pares a <0,4 m (antes 3) | `ATLAS_D` fila 3, `Bug967a` (4 de 4 corridas) |
| 9 | Enemigos atrapados en cubículos del inicio | **OK** | 12/12 reservas con camino (antes 12 sin ruta); 2 barreras `Barrera_Reserva_SoloEnemigos` quitadas | `captura_info.txt` `reserva_*`, `Bug977a` |
| 10 | Quitar colliders que bloquean el retroceso → cartel «Debes volver a la guerra» | **OK** | 10 compuertas sin collider; teletransporte 0 m; cartel con distancia («26 m», «147 m») | `ATLAS_D` fila 2, `Bug966a`, `Bug409a` |

## Lista 2

| # | Pedido | Estado | Medida / nota | Evidencia |
|---|---|---|---|---|
| 1–2 | Sentido del entorno, rutas, spawn y plaza final | **PARCIAL** | inicio rediseñado (carga del portón desde el inicio, un solo objetivo «VOLÁ EL PORTÓN NORTE», sin generador); plaza con solado nuevo | `ATLAS_A`. Sin vista cenital global |
| 3 | Pulido nivel 2: colliders, duplicados, títulos de zona | **PARCIAL** | 9 carteles de zona borrados, 4 pilares duplicados apagados, colliders desalineados 1/10/65 → 0/0/0 en `501g` (con lista blanca de piezas decorativas) | `Bug986a`, `colisiones_501g_despues.txt` |
| 4 | Camión del inicio y gasolina fuera; camión nuevo en el resto | **OK** | generador y Camion_1/2/3 apagados, 22 piezas Camion/Tambor de prefabs quitadas, camión nuevo de 684 triángulos en el patio. En la toma quedan 16 objetos de nombre «tambor» a 60 m (antes 38): son barriles decorativos, no gasolina | `ATLAS_D` fila 4, `Bug981a`, `Bug985a` |
| 5 | Revivir: UN solo engranaje radial en el cursor | **OK** | el engranaje llena 0,40 a los 2 s (jugador) y 0,80 (médico); el feedback propio del revivir es 1 solo objeto. Siguen la marca «caído» y la pista [E], que son indicadores aparte | `ATLAS_C` fila 3, `Bug976a` |
| 6 | Ambiente de desierto: rodantes, partículas, árboles, decoración, medir perf | **OK** | árboles 0→1450 instancias (envoltura de malla única; en la primera build no se dibujaban), 10 arbustos rodantes, polvo, 4 remolinos, 36 cactus y 30 rocas. Perf ruta: 142,3→141,5 fps, p95 7,33→7,63 ms | `ATLAS_B` filas 2–4, `Bug983a` |
| 7 | Cuadro gris del objetivo 4 y colliders | **OK** | `Plaza_Cemento` 30×30 liso reemplazado por losa con juntas; el «gris» del HUD era el marco del minimapa | `ATLAS_E` fila 1, `Bug982a` |
| 8 | Ambiente: assets superpuestos | **PARCIAL** | ver Lista 1 punto 7 | — |
| 9 | Plaza: textura de tablero → calle/plaza realista | **OK** | 100 Adoquín→0, solado de 4 renderers con textura y normal; referencias en `referencias_plaza.md` (sin descargar texturas) | `ATLAS_A` fila 4, `ATLAS_B` fila 1, `Bug984a` |
| 10 | Criterios del Parcial 2 | **PARCIAL** | auditoría en `VALIDACION_PARCIAL2.md`. Hecho: `INTEGRANTES.txt`, `ATAJOS_DE_TESTEO.txt`, fuentes unificadas (`Bug987a`) y RAR. Pendiente: las decisiones con la docente (fuentes de Google Fonts, Mixamo, audio descargado) | ver ese archivo |
| 11 | Assets menos primitivos, estilo cúbico | **OK** | 73 props con detalle (12 autos, 38 árboles, 12 hidrantes, 4 contenedores, 4 bancos, 7 puestos); renderers de la ciudad 6523→6615 (+1,4 %) | `ATLAS_D` fila 4, `Bug985a` |

## Rendimiento (mismo arnés)

| Toma | fps ANTES | fps DESPUÉS | p95 ANTES | p95 DESPUÉS |
|---|---|---|---|---|
| ruta | 142,3 | 141,5 | 7,33 ms | 7,63 ms |
| plaza | 118,6 | 119,5 | 10,41 ms | 9,92 ms |
| inicio | 75,4 | 75,9 | 16,98 ms | 15,06 ms |

Se mantiene sobre 60 fps de promedio. El cuadro más lento (`max_ms`) de plaza (~87 ms) **no es una regresión**: corrí plaza e inicio dos veces con la build del ANTES y dos con la final, en la misma máquina y a la vez, y el ANTES dio 87 y 90 ms en plaza mientras la final dio 25 ms en ambas (carpetas `perf_antes_*` / `perf_despues_*`).

## Checks de regresión (estado final)

- Lote `Bug950a…Bug987a` completo sobre la build final: 35 pasan, 0 fallan, 3 números sin check (959, 968, 969).
- Pasan además: `Bug958a` (heli: 4 cohetes en Fácil y Medio, 5 en Difícil), `403a` (5 de 5), `967a` (4 de 4), `101e` y `120v` (cadena completa hasta Victoria: 5 subidos al helicóptero, partida guardada borrada al ganar), `406u`, `407u`, `409a`, `409u`, `893a`, `961a`, `952a`, `303a`–`308u`, `318u`, `078e`, `384a`, `413a`, `603d`, `710a`, `298v`, `302a`, `501c`, `501g`.
- Suite headless (`Run All Tests Headless`): **OK**.
- Checks viejos actualizados por cambio intencional: `961a` (barra del tanque oscura, se exige contraste), `409a` (las compuertas ya no frenan), `893a` (tolerancia vertical del NavMesh 0,8→1,2 m), `983a` (árboles a ≥13 m de la ruta, antes 24 m), `403a` (IA pausada y enemigos apagados durante la espera), presupuestos de búsquedas de la suite alineados con lo medido (`Transform.Find` 140, búsquedas globales 38).
- Siguen fallando, **igual que en el ANTES y fuera de lo pedido en esta ronda**: `501a`, `501b`, `501d`, `501e`, `501f`, `602b`. `501a` mejoró (islas de NavMesh 10→8); los restantes son los nidos y artilleros fijos de torre (Nido_O/E, TV1/TV2) fuera del NavMesh, piezas flotantes tras explosiones y huecos de perros en la mansión.

## Decisiones tomadas para cerrar

- **Heli en Difícil:** 5 cohetes (tope 5 de `958a`); la toma del arnés fallaba cohetes contra el helicóptero en movimiento.
- **Solapes de categoría C:** son caras que se tocan entre módulos (muro contra losa, cordón contra vereda; el informe repite cada par 3–4 veces), no superposiciones que parpadeen. Se acepta por diseño modular y se verificó que ninguna de las 67 capturas muestra parpadeo de superficies. Los duplicados exactos (A) y casi duplicados (B) quedaron en 0.
- **Muro_Salida_Norte:** se deja liso; no estaba entre los pedidos.
- **Vista cenital:** `despues_final/nivel_cenital_global_claro.png` (render ortográfico del nivel completo desde el Editor).
- **Origen de recursos:** `entrega/ORIGEN_DE_RECURSOS.txt` dentro del RAR. No hay pantalla de créditos en el juego.

## Qué sigue abierto y es decisión del equipo (no se puede cerrar desde acá)

- Origen de `SA_Calma.mp3` / `SA_Accion.mp3` (música por defecto), de `Menu_Fondo.png` y de los íconos HUD/rol: no está documentado.
- Si la docente acepta assets de licencia libre (fuentes de Google Fonts, animaciones de Mixamo, audio de Kenney/OpenGameArt/Freesound, música de Kevin MacLeod que exige atribución visible).
- Los nombres de `INTEGRANTES.txt` son los de los personajes (Vega, Kes, Doc); hay que cambiarlos por los reales.

## Entrega

`entrega/StrategicPoint.rar` (89 MB, verificado con `rar t`): build + `INTEGRANTES.txt` + `ATAJOS_DE_TESTEO.txt` + `ORIGEN_DE_RECURSOS.txt`. Nada commiteado.
