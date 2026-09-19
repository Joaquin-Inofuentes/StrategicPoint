# Ronda 7 — Salto corregido, sonido y mirilla por arma, cuchillo [F], granada [G], feedback en todo y tutorial completo

Cada cambio está descrito con **qué se hizo, en qué archivo, cómo y cómo se probó**, con capturas en `Assets/Validacion/Ronda7/` (Play Mode, teclas y mouse reales). La suite headless quedó **en verde con una fase nueva (FASE 16)**. El recorrido completo del tutorial, con una captura por fase, está en [`TUTORIAL_PASO_A_PASO.md`](TUTORIAL_PASO_A_PASO.md).

| # | Pedido | Estado |
|---|---|---|
| 1 | La animación de saltar se rompía por el eje Y de base | Causa hallada y corregida |
| 2 | Mirilla con zoom que se vea bien; cada arma con su mirilla | Hecho (7 mirillas + mira de cadera propia) |
| 3 | Cada arma con su sonido de disparo, recarga y desenfunde; cohete con «fiush» y explosión con estruendo | Hecho |
| 4 | Cuchillo con **F** | Hecho |
| 5 | Granada con **G**, con la curva visible al mantener | Hecho |
| 6 | Toda acción (radial, órdenes) con feedback visual y auditivo | Hecho |
| 7 | Tutorial: usar todos los comandos de **Q** (curar … subir, bajar, poner bomba) | 35 pasos, cada comando se usa de verdad |
| 8 | Un .md del tutorial paso a paso con capturas | `TUTORIAL_PASO_A_PASO.md` |

---

## 1. Salto: el cuerpo se hundía en el piso

**Qué pasaba.** En Play, con el clip de salto en marcha, la cadera quedaba a ~0,8 m *por debajo* de la raíz (medido con `Animator.GetBoneTransform`): el soldado se enterraba durante todo el salto.

**Causa real.** Los tres FBX (`jump up`, `jump loop`, `jump down`) **nunca estuvieron en la lista `Animaciones` de `ArtSetup`**: quedaron importados como *Generic* a escala 1 (el rig es *Humanoid* a 0,2948), sin avatar compartido y sin la calibración de altura de cadera que sí tienen los otros ~40 clips. Además su curva `RootT.y` valía ~0,41–0,48 contra ~0,96 de un soldado de pie.

**Cómo se corrigió** (`Editor/ArtSetup.cs`, `Editor/ArtBuilder.cs`, `Actors/SoldierMotor.cs`):
1. `Arte/1b. Configurar clips de salto`: importa los tres como Humanoide con `CopyFromOther` del avatar del soldado (sin renombrar el clip, para no romper la referencia del controlador).
2. `Arte/1c. Hornear clips de salto`: copia cada clip a `Assets/_Project/Animation/Salto/*.anim`, le suma `+0,548` a `RootT.y` (constante medida: 0,962 − 0,414, el primer cuadro de `jump up` es la pose de pie) y reapunta `SaltoArriba/SaltoAire/SaltoAbajo` de `AC_Soldado` a esos assets. **Se hornea a `.anim` y no se edita el clip importado** porque esa edición vive en la cache de Library y un reimport la pierde (esa fue la causa de que los clips de caminar «volvieran a hundirse» en rondas anteriores; la primera versión de esta corrección tuvo ese problema y la suite lo detectó).
3. `SoldierMotor`: el aterrizaje ya no vuelve a la altura de despegue; sondea el piso de abajo (`Physics.RaycastNonAlloc`, ignorando el propio cuerpo) y aterriza sobre el de ahora (cuestas, escalones, cajones).
4. Sonido de salto y de aterrizaje + sacudida leve de cámara al caer (`PlayerInputDriver`).

| Antes (cuerpo enterrado) | Después |
|---|---|
| ![antes](../Assets/Validacion/Ronda7/jump_antes_apex.png) | ![despues](../Assets/Validacion/Ronda7/jump_despues_apex.png) |

**Medición** (mismo instante del salto, hips respecto de la raíz): antes −0,43…−0,81 m; después +0,04 m, con los pies a 0,8–1,0 m del piso en el aire.
**Prueba automática:** FASE 16 comprueba que los tres estados usan un `.anim` horneado con altura de cadera entre 0,85 y 1,10 y que los FBX se importan como Humanoide.

---

## 2. Mirillas: una por arma, nítidas, y con el zoom despejado

Ya existían siete estilos de retícula (`WeaponCatalog.Spec.Reticle`); esta ronda los revisó y arregló lo que no se veía bien:

- **Borde oscuro** (`UI.Outline`) en la retícula con zoom (`MirillaView`): la blanca se perdía contra cielo y paredes claras.
- **Visor cerrado (francotirador y cohete):** el panel de misión y la barra «ENEMIGOS · ESCUADRA» se veían *a través* del agujero. Ahora se desvanecen mientras se mira por la óptica y vuelven al soltar; la retícula queda por encima del resto del HUD (`SetAsLastSibling`), salvo el cartel de modo dios y el arma.
- **Mira de cadera propia por arma** (`AimUI.SetCrosshairStyle`): antes todas compartían el mismo anillo; ahora cada arma dibuja su mismo estilo, chico y con borde oscuro.

| Francotirador antes | Francotirador ahora |
|---|---|
| ![antes](../Assets/Validacion/Ronda7/sight_sniper_zoom_antes.png) | ![ahora](../Assets/Validacion/Ronda7/sight_sniper_zoom_corregido.png) |

**Las siete armas: arriba mira de cadera, abajo mira con zoom** (Fusil punto+anillo · Pistola cruz · Ametralladora anillo con muescas · Metralleta chevrón · Escopeta círculo · Francotirador telescópica · Lanzacohetes mildot):

![mirillas](../Assets/Validacion/Ronda7/mirillas_por_arma_hoja.png)

Capturas individuales: `sight_<Arma>_cadera.png` y `sight_<Arma>_zoom.png`; visor del cohete corregido: `sight_rocket_zoom_corregido.png`.
**Prueba:** FASE 16 verifica 7 estilos distintos y 7 sprites distintos.

---

## 3. Sonido por arma

Todo lo que no tenía grabación real se sintetiza por código en `Presentation/SfxSintetico.cs` (determinista, normalizado al pico, con fade-out para evitar clics). Cualquier sonido se reemplaza soltando un `.wav` en `Resources/Audio/Sfx/<Clave>/` (`Reload_Rifle`, `Draw_Pistol`, `Shot_Smg`, `Explosion`…): `GenericSfx` busca primero ahí.

- **Disparos reales nuevos** (tomados de `Audio_GunshotCandidates`): metralleta = PPSh, escopeta = Model 12 y Nova, francotirador = Arisaka. Fusil, pistola y ametralladora ya usaban los suyos.
- **Lanzacohetes:** `Shot_Rocket` = golpe de la carga + ignición + «**fiushhh**» (ruido cuyo filtro se abre mientras el cohete se aleja, 1,9 s).
- **Explosión** (cohete, cañón, granada, barril): `Explosion` de 2,4 s = trueno grave + cuerpo + crack de ataque + escombros que caen. Medido en el espectro: 96% de la energía por debajo de 200 Hz (es un estruendo, no un chasquido).
- **Recarga propia por arma** (`GetWeaponReload`), con la duración exacta del catálogo: pistola (cargador, corredera), fusil (cargador, manija), metralleta, ametralladora (tapa, cinta de balas, cierre), escopeta (**cartucho por cartucho** y bombeo), francotirador (**cerrojo**), cohete (culata, tubo que se desliza, ojiva). Se dispara desde `WeaponHolder.StartReload`, también para la IA.
- **Desenfunde propio** (`GetWeaponDraw`) al cambiar con 1/2/3 o la rueda (solo para el arma que cambia el jugador).
- **Gatillo en seco** distinto por arma (`GetWeaponDry`).

Verificación sin oído: `AudioDirector.Historial` guarda los nombres de los clips que **de verdad sonaron**. En Play, cada arma disparada y recargada dejó su clip (`Shot_Smg_PPSh`, `Reload_Shotgun`…) y el cohete dejó `Shot_Rocket → Explosion`. FASE 16 comprueba que los 17 sonidos nuevos existen, no son mudos ni tienen NaN, y que cada recarga dura lo que dice el catálogo (±0,7 s).

![espectrogramas](../Assets/Validacion/Ronda7/sonidos_espectrogramas.png)

*Espectrogramas de la explosión, el cohete, el rebote de granada, el tajo/impacto del cuchillo, el aterrizaje, cinco recargas y la reanimación.*

Tabla completa de duración, pico y centroide de cada clip: al final de este documento.

---

## 4. Cuchillo con [F]

- `KeyBindings`: `AtaqueCuchillo` pasa de **V a F**; el viejo «poseer con F» (heredado) queda sin tecla porque poseer es del radial.
- `WeaponHolder.TryMelee` (55 de daño, 2,2 m, arco de 70°, enfriamiento 0,55 s) llama a `Presentation/CuchilloFx`:
  - el **cuchillo** (`P_Wpn_Cuchillo`, copiado a `Resources/Weapons`) barre un arco con **estela brillante** (0,24 s) delante del soldado;
  - sonido de **tajo** al aire (`KnifeSwing`) y, si conecta, **golpe sordo** (`KnifeHit`) + chispa roja + etiqueta «¡CUCHILLADA!» + sacudida de cámara;
  - el HUD del arma muestra `[F] CUCHILLO` (se apaga mientras se enfría).

![tajo](../Assets/Validacion/Ronda7/knife_swing_2.png)

*El tajo en cámara lenta (arco celeste a la derecha del soldado).*

---

## 5. Granada con [G]

`Combat/Granada.cs`, `Presentation/TrayectoriaGranadaView.cs`, `PlayerInputDriver.ActualizarGranada`:

- **Mantener G:** suena el seguro (`GrenadePin`) y aparece la **curva exacta** (línea + cuentas) y, donde cae, un **anillo con el radio real de la explosión** (4,5 m, rojo) más un anillo interior. Es la misma simulación (`Granada.Simular`) que usa la granada real, así que lo que se ve es lo que pasa.
- **Soltar G:** se lanza. Parábola resuelta para caer en el punto apuntado (`VelocidadHacia`, 17 m/s, fuera de alcance = 45°). **Rebota** contra lo sólido (sonido de rebote según la velocidad de impacto), luz roja de fusible que parpadea cada vez más rápido y **tic-tac que se acelera** durante 2,4 s.
- **Explota** con `Projectile.ExplodeAt` (la misma que cohete y barril: daño con caída al borde, línea de vista, empuje, sacudida, estruendo). **No lastima al propio bando.**
- Clic derecho o Esc guardan la granada sin gastarla. Tres por soldado; el HUD muestra `[G] GRANADA x3`.
- Eventos `GrenadeThrownEvent` / `GrenadeExplodedEvent` para el tutorial.

![curva](../Assets/Validacion/Ronda7/grenade_aim_far.png)
![curva y radio en el tutorial](../Assets/Validacion/Ronda7/tut_11b_curva_y_radio.png)
![en vuelo](../Assets/Validacion/Ronda7/grenade_en_vuelo.png)
![HUD](../Assets/Validacion/Ronda7/hud_extras.png)

*Curva a 26 m, curva y anillo sobre un enemigo en el tutorial (la retícula se pone roja), la granada en vuelo con su estela y el HUD con [F] y [G].*

**Prueba:** FASE 16 verifica velocidad de lanzamiento, 45° fuera de alcance, cuenta de 3 granadas y que la explosión hiere al enemigo del centro y **no** al aliado pegado. En el tutorial la curva cayó a 0,6 m del enemigo apuntado.

---

## 6. Feedback visual y auditivo en toda acción

Antes de esta ronda el radial y varias acciones tácticas eran mudas (solo un cartel). Ahora:

| Acción | Sonido | Visual |
|---|---|---|
| Abrir el radial | `RadialOpen` | anillo |
| Pasar por una opción | `RadialTick` (solo al **cambiar**) | resaltado |
| Confirmar una orden | `RadialConfirm` | nombre de la opción flotando en el mundo + pulso del color de su categoría (`ColorDeCategoria`) en el punto de la acción |
| Soltar sin elegir | `RadialCancel` | — |
| Rechazo | clic seco (ya existía) | cartel del motivo |
| Curar: el médico llega | `HealStart` | «CURANDO…» + «+12» verde cada segundo |
| Curado / botiquín | `HealDone` | «¡X CURADO!» + pulso |
| Reanimar | `HealStart` y al levantar `Revive` (descarga + campanitas) | «REANIMANDO…», «¡X DE VUELTA!» |
| Plantar bomba | `BombPlant` + tic que se acelera (`BombTick`) | anillo de progreso, «PLANTANDO CARGA…» |
| Saltar / aterrizar | `Jump` / `Land` | sacudida de cámara |
| Cambiar de arma / recargar / gatillo en seco | desenfunde, recarga y clic propios de cada arma | icono que «late», cartel «RECARGANDO» |
| Cuchillo / granada | tajo, impacto, seguro, lanzamiento, rebote, tic-tac, explosión | ver secciones 4 y 5 |

Archivos: `UI/MenuDeOrdenes.cs`, `Player/PlayerInputDriver.cs` (`EjecutarOrdenRadial`/`ConfirmarOrdenRadial`), `Player/PedidoDeCuracion.cs`, `Player/Demolicion.cs`, `Presentation/Feedback.cs` (nuevo `Feedback.Visual` para acciones cuyo sonido ya lo pone otro sistema), `Presentation/AudioDirector.cs`.

---

## 7. Tutorial: todos los comandos de [Q] se usan de verdad (26 → 35 pasos)

Pasos nuevos: **7 Saltar, 9 Armas (mira y sonido propios), 10 Cuchillo, 11 Granada, 18 Ir allí y atacar, 19 Formaciones y retirada, 23 Curarme, 25 El asalto aliado pone la bomba, 34 Bajar del tanque.** Pasos cambiados: **24 Demoler** (ahora exige `Q → DEMOLER → YO DEMUELO`), **26 Torreta** (`Q → TORRETA FIJA → USAR` y `→ SALIR`), **28 Entrar al tanque** (`SUBIRME YO`).

Cobertura de los comandos del radial (tabla completa en el documento del tutorial): IR ALLÍ, CUBRIRSE, ATACAR, POSICIÓN (5 opciones), CURAR (4), TANQUE (5), POSEER, DEMOLER (3) y TORRETA (2).

Cómo se fuerza el uso del radial:
- `PlayerInputDriver.SoloRadial`: en los pasos 26, 28 y 34 el atajo [E] avisa «USA EL RADIAL: …» y no actúa.
- Cada comando lo marca `OrdenRadialEjecutada` (solo si la orden **salió bien**).

Problemas reales que apareció al correr el tutorial completo, y su arreglo:
| Problema | Arreglo |
|---|---|
| El herido se curaba solo en 3 s (regeneración automática) y el médico no tenía nada que hacer | `Health.RegeneracionPermitida` se apaga en los pasos de curar |
| Los aliados mataban al enemigo del paso «atacar» antes de recibir la orden | Quedan en alto el fuego hasta que llega la orden ATACAR |
| Enemigos de práctica aparecían detrás de un muro y no se podían apuntar | `PuntoConVista` busca un lugar con línea de vista libre |
| Cancelar una demolición era imposible: el muro volaba en 4 s | En ese paso el muro está a 24 m y la carga dura 9 s (`Demolicion.Segundos` ajustable, vuelve a 4); si vuela antes se crea otro |
| Sin aliados a bordo `BAJAR TODOS` no se ofrece y el paso se trababa | Se da por hecho tras 3 s |

Recorrido completo con capturas: [`TUTORIAL_PASO_A_PASO.md`](TUTORIAL_PASO_A_PASO.md).

---

## 8. Pruebas

- **Suite headless: todas las fases en verde**, con la **FASE 16** nueva (teclas F/G, salto horneado, 17 sonidos válidos, 7 disparos/recargas/desenfundes distintos y con duración de catálogo, mirillas, cuchillo, granadas, explosión que no lastima al propio bando, regeneración apagable, feedback).
- En Play con teclas y mouse reales: salto (baked), cuchillo, granada (mantener/soltar/explotar), las 7 armas (disparo, recarga, cambio, mira), radial completo, y el tutorial de 35 pasos hasta la victoria.

## 9. Límites conocidos (honestos)

- Los sonidos sintetizados los verifiqué por métricas (duración, pico, espectro, que sonaran), **no de oído**: si alguno no te convence, soltá un `.wav` con el nombre de la clave y lo reemplaza sin tocar código.
- Los pasos del tutorial que no cambiaron se adelantaron con `SaltarPaso` para llegar a los nuevos; los nuevos y cambiados se hicieron con gestos reales (con posicionamiento por script solo para apuntar).
- Las granadas no las usan los aliados ni la IA; los aliados siguen sin usar la ametralladora fija.
- En el tutorial, al saltarse pasos, los aliados pueden quedar lejos (p. ej. en «cubrirse» tardan mucho); en una partida seguida no pasa.

## Tabla de sonidos (duración, pico, centroide espectral)

Nota: el centroide se ve sesgado hacia agudos por los transitorios; la energía por banda es más fiable (explosión 96% < 200 Hz; cohete 80% < 200 Hz; aterrizaje 93% < 200 Hz).

| Sonido | Duración (s) | Pico | Centroide (Hz) |
|---|---|---|---|
| BombPlant | 0.45 | 0.60 | 2031 |
| BombTick | 0.09 | 0.50 | 1663 |
| Draw_Heavy | 0.40 | 0.70 | 6606 |
| Draw_Pistol | 0.40 | 0.70 | 8157 |
| Draw_Rifle | 0.40 | 0.70 | 8189 |
| Draw_Rocket | 0.40 | 0.70 | 6027 |
| Draw_Shotgun | 0.40 | 0.70 | 7870 |
| Draw_Smg | 0.40 | 0.70 | 7901 |
| Draw_Sniper | 0.40 | 0.70 | 7952 |
| Explosion | 2.40 | 0.95 | 5463 |
| GrenadeBounce | 0.30 | 0.85 | 4192 |
| GrenadePin | 0.34 | 0.85 | 6489 |
| GrenadeThrow | 0.40 | 0.75 | 7501 |
| HealDone | 0.70 | 0.60 | 1891 |
| HealStart | 0.50 | 0.60 | 801 |
| Jump | 0.22 | 0.55 | 6869 |
| KnifeHit | 0.34 | 0.90 | 7821 |
| KnifeSwing | 0.26 | 0.70 | 9467 |
| Land | 0.30 | 0.80 | 3500 |
| RadialCancel | 0.18 | 0.55 | 806 |
| RadialConfirm | 0.30 | 0.70 | 1466 |
| RadialOpen | 0.22 | 0.60 | 4800 |
| RadialTick | 0.04 | 0.55 | 2051 |
| Reload_Heavy | 2.20 | 0.85 | 6812 |
| Reload_Pistol | 1.00 | 0.80 | 7392 |
| Reload_Rifle | 1.50 | 0.80 | 7277 |
| Reload_Rocket | 2.80 | 0.85 | 7079 |
| Reload_Shotgun | 2.40 | 0.85 | 7594 |
| Reload_Smg | 1.60 | 0.80 | 7371 |
| Reload_Sniper | 2.40 | 0.85 | 7562 |
| Revive | 0.95 | 0.70 | 5599 |
| Shot_Heavy_CD | 0.59 | 0.82 | 3249 |
| Shot_Pistol_1911 | 0.59 | 0.86 | 3660 |
| Shot_Rifle_Marlin336 | 0.59 | 0.53 | 3260 |
| Shot_Rocket | 1.90 | 0.95 | 5728 |
| Shot_Shotgun_Nova | 0.59 | 0.89 | 3738 |
| Shot_Smg_PPSh | 0.59 | 0.50 | 4371 |
| Shot_Sniper_Arisaka | 0.59 | 0.59 | 3196 |
