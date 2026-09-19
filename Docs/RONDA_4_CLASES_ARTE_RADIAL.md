# Ronda 4 — daño, zoom real, clases, médico, radial de órdenes y arte

Todo se probó en Play (teclas y mouse inyectados al Input System, y el mouse real del sistema operativo cuando hacía falta). Las capturas están en `Assets/Validacion/Ronda4/`. La suite headless completa sigue en **TODAS LAS FASES COMPLETADAS CON ÉXITO** (se actualizaron 3 comprobaciones que medían el tubo de óptica viejo y la etiqueta "Aliado", que cambiaron a propósito).

Índice: [1 Daño](#1-daño-al-jugador-en-la-partida-principal) · [2 Zoom real y mirillas](#2-zoom-real-y-mirillas-por-arma) · [3 Anillo de vida](#3-anillo-de-vida-del-enemigo-solo-el-contorno) · [4 Clases, modelos y armas](#4-clases-modelos-armas-y-lanzacohetes) · [5 Médico](#5-el-médico-cura) · [6 Aliados distintos](#6-los-aliados-se-diferencian-y-actúan) · [7 Radial de Q](#7-radial-de-órdenes-mantener-q) · [8 Arte en el blockout](#8-arte-en-todo-el-blockout) · [9 Vida sin repetir](#9-la-vida-ya-no-se-repite) · [10 Transparencia](#10-qué-se-probó-y-qué-no)

---

## 1. Daño al jugador en la partida principal

**Qué se probó (SC_Gameplay, partida real):**

1. Daño directo con atacante enemigo: `Health.TakeDamage(...)` → baja la vida, se ve la viñeta roja, la aberración cromática y los cubitos rojos, y la fila del roster se actualiza ([01](../Assets/Validacion/Ronda4/01_dano_recibido.png), [72](../Assets/Validacion/Ronda4/72_dano_recibido_poca_vida.png)).
2. Fuego enemigo real: se teletransportó al jugador al puesto avanzado (z = 146) y se esperó. Pasó de 180/180 a 0 en menos de 1,5 s con `LastAttackerId` = un patrullero enemigo ([70](../Assets/Validacion/Ronda4/70_dano_real_enemigos_disparan.png), [02](../Assets/Validacion/Ronda4/02_recibe_disparos_reales.png)).
3. Muerte: el roster marca al caído, el aliado libre "va a revivir" al jugador (`RescateAutomatico`) y, si nadie pide el cambio a tiempo, la vista pasa sola a RTS ([03](../Assets/Validacion/Ronda4/03_tras_muerte.png)).

**Qué se corrigió alrededor de eso:** el daño se veía repetido en tres lugares (ver §9) y las variantes nuevas de soldado conservan el efecto de cubitos al recibir daño (usan el mismo `CubeFxReactor`).

**Observación de balance (no lo cambié):** con 3 fusileros enemigos a tiro más el cañón del tanque enemigo el jugador dura ~1,5 s. Si querés que aguante más, el número está en `WeaponCatalog` (daño 26 por bala, cadencia 0,30 s) y en la vida de 180 de cada soldado.

---

## 2. Zoom real y mirillas por arma

**Antes** (mantener clic derecho): la mira se agrandaba ×4 (quedaba un anillo enorme y borroso), el fondo se difuminaba y el FOV bajaba de 60 a 25 sin relación con el arma ([05](../Assets/Validacion/Ronda4/05_zoom_actual.png)).

**Ahora:**

- **Zoom real por arma.** Cada arma declara su aumento (`WeaponCatalog.Spec.ZoomFactor`) y `CameraRig.SetZoomFactor` lo convierte a FOV con la tangente, de modo que "×6" es de verdad seis veces más cerca:

```csharp
// Camera/CameraRig.cs
public void SetZoomFactor(float factor)
{
    ZoomFactor = Mathf.Max(1.05f, factor);
    zoomFov = 2f * Mathf.Atan(Mathf.Tan(normalFov * 0.5f * Mathf.Deg2Rad) / ZoomFactor) * Mathf.Rad2Deg;
}
```

- **Sin desenfoque** al apuntar (`PostFxDirector`: `zoomBlurOn = false`) y sin agrandar la mira base.
- **Mirilla nítida propia de cada arma** (`UI/MirillaView.cs`): texturas generadas por código (256 px, bordes suavizados) que no dependen de assets; se tiñen de rojo sobre un enemigo y de verde sobre un aliado. La sensibilidad del mouse baja con el aumento (`1 / zoom^0.75`) para que apuntar con ×6 no sea nervioso.

| Arma | Aumento | Mirilla | Captura |
|---|---|---|---|
| Fusil | ×2,2 | punto dentro de un anillo fino | [31](../Assets/Validacion/Ronda4/31_zoom_fusil_x2_2_punto.png) |
| Pistola | ×1,4 | cruz con hueco central | [32](../Assets/Validacion/Ronda4/32_zoom_pistola_x1_4_cruz.png) |
| Lanzacohetes | ×2,4 | cruz con mildots y visor oscuro | [33](../Assets/Validacion/Ronda4/33_zoom_lanzacohetes_x2_4_mildot.png) |
| Francotirador | ×6 | telescópica: tubo negro, cruz fina de borde a borde, mildots | [34](../Assets/Validacion/Ronda4/34_zoom_francotirador_x6_telescopica.png) |
| Metralleta / Ametralladora / Escopeta | ×1,6 / ×1,7 / ×1,25 | cheurón / anillo grueso / círculo ancho | (mismos generadores) |

El tubo con `RenderTexture` de `MiraOptica` (el "dome" borroso) ya no se muestra; el componente sigue existiendo pero `Mostrar(false)`.

---

## 3. Anillo de vida del enemigo: solo el contorno

El círculo que "carga" con la vida del enemigo bajo la mira (`AimUI.UpdateEnemyHealthCircle`, dibujado por `CirculoDeProgreso`) era un **disco relleno** con un `Image` radial encima. Ahora es un **anillo hueco**: el sprite tiene alfa 0 en el centro y el `Image` radial lo usa de máscara, así el relleno solo pinta el contorno.

```csharp
// UI/CirculoDeProgreso.cs — el sprite es un anillo (no un disco)
float alfa = Mathf.Min(Mathf.Clamp01(radio - d), Mathf.Clamp01(d - radioInterno));
```

Además el anillo de vida quedó más grande que la mira (84 en vez de 50) para que se lea el arco por fuera. La recarga y el "revivir" usan el mismo componente, así que también son contorno. Captura con un enemigo al 40 %: [36](../Assets/Validacion/Ronda4/36_anillo_vida_enemigo_40pct.png) (arco rojo brillante del 40 % sobre el aro tenue); al 100 %: [35](../Assets/Validacion/Ronda4/35_anillo_vida_enemigo_apuntando.png).

---

## 4. Clases, modelos, armas y lanzacohetes

Las tres clases jugables y sus cargas (las teclas **1 / 2 / 3 son las ranuras** del arma de la clase que manejás):

| Clase | Modelo (arte) | 1 | 2 | 3 |
|---|---|---|---|---|
| **Asalto** (Vega) | `SM_Chr_Soldado_Comando` (casco, cinturón de cuchillo y granadas) | Fusil ×2,2 | Pistola | **Lanzacohetes** ×2,4 |
| **Flanqueador** (Kes) | `SM_Chr_Soldado_Explorador` + boina | Metralleta ×1,6 | Pistola | Escopeta (7 perdigones) |
| **Médico** (Doc) | `SM_Chr_Soldado_Medico` (gorra y mochila con cruz roja) | Fusil | Pistola | — |

Enemigos: se reparten variantes por nombre (fusilero, comando con ametralladora, explorador con metralleta, francotirador con arma de precisión ×6) usando el mismo rig pero con un material teñido de rojo.

**Cómo se cargan los modelos correctos (y por qué animan):** los FBX de `04_Personajes` son mallas estáticas sin esqueleto. `Editor/SoldierVariantBuilder.cs` (menú *Strategic Point › Arte › 8*) hace lo mismo que `SkinTransfer` hizo con el soldado base: por cada vértice del cuerpo busca el punto más cercano de la **superficie** del rig mixamo y le transfiere los pesos de hueso (baricéntricos); casco/boina/gorra se atan 100 % a `Head`, la mochila médica a `Spine2`, los cinturones a `Hips`/`Spine`. Sale **una malla por variante** (`Resources/Soldados/Mesh_*.asset`) con los mismos 25 huesos, así que el `Animator` y las animaciones no cambian. Las armas que traen esos FBX en la mano no se incluyen: se cuelga el arma real del loadout.

```csharp
// Actors/SoldierLook.cs (se agrega solo a todo Soldier en Play)
smr.sharedMesh = Resources.Load<Mesh>("Soldados/Mesh_" + def.Malla);
smr.sharedMaterial = def.Enemigo ? matEnemigo : matAliado;   // trimsheet, teñido si es enemigo
w.Loadout.Clear(); w.Loadout.AddRange(def.Loadout); w.EquipFromLoadout(0);
```

**Armas nuevas** (`WeaponKind`: Smg, Rocket, Shotgun, Sniper; prefabs `P_Wpn_*` generados por `WeaponPrefabBuilder` desde los FBX). El lanzacohetes dispara un proyectil lento que cae un poco y **explota en 5 m** (reusa `Projectile.ExplodeAt`, el mismo daño de área del tanque); la escopeta suelta 7 perdigones con cono de 4,5°:

```csharp
// Combat/WeaponHolder.cs — TryFire
if (espec.Pellets > 1)            // escopeta
    for (int p = 0; p < espec.Pellets; p++) pool.Spawn(spawnPos, ApplySpread(spreadDir, espec.PelletSpreadDeg), ...);
else if (espec.ExplosionRadius > 0f)   // lanzacohetes
    pool.Spawn(spawnPos, spreadDir, owner.Id, owner.Team, damage, projectileColor, espec.ExplosionRadius, espec.ProjectileGravity, null, espec.ProjectileSpeed);
```

Capturas: clases [10](../Assets/Validacion/Ronda4/10_clases_inicio.png) (Asalto con el roster "1 · Asalto / 2 · Flanqueador / 3 · Medico"), [90](../Assets/Validacion/Ronda4/90_clases_aliadas_y_enemigas.png) y [91 primer plano](../Assets/Validacion/Ronda4/91_clases_primer_plano.png) (Flanqueador amarillo con escopeta y boina, Médico blanco con cruz roja, enemigos teñidos de rojo). Lanzacohetes: [40 recargando tras el disparo](../Assets/Validacion/Ronda4/40_lanzacohetes_disparo_en_vuelo.png) (la esfera naranja es la explosión) y [41](../Assets/Validacion/Ronda4/41_lanzacohetes_explosion.png).

---

## 5. El médico cura

`Player/PedidoDeCuracion.cs`: además del pedido manual ("necesito curarme"), el **médico atiende solo**: cada 1,5 s busca al aliado más herido (< 75 % de vida, a ≤ 16 m) y, si no está peleando ni lo maneja el jugador, va hasta él (orden de seguir) y lo cura mientras esté a ≤ 2,5 m.

```csharp
if (medico.Role != RoleType.Medic || medico.Team != TeamId.Player) continue;
if (medico.Brain != null && medico.Brain.CurrentTarget != null) continue;   // peleando: no cura
...
OrderService.IssueFollowOrder(medico, peor);
GameLog.Line($"{medico.DisplayName} atiende solo a {peor.DisplayName} ({peor.Health.Current}/{peor.Health.MaxHealth})");
```

Prueba en SC_Gameplay: se dejó a Kes en 40/180 con Doc a 12 m. Log: `Soldado_3_Doc atiende solo a Soldado_2_Kes (40/180)`; Doc pasó a `Follow`, llegó a 0,8 m y la vida de Kes subió 40 → 89 → 145 → 180 en 3 s (`activo=True` → `False` al terminar). Capturas: [50 el médico va](../Assets/Validacion/Ronda4/50_medico_va_a_curar.png) y [51 curando](../Assets/Validacion/Ronda4/51_medico_curando_aliado.png) (la franja inferior dice "Soldado_2_Kes · Vida 142/180 · Arma Metralleta · FLANQUEADOR").

---

## 6. Los aliados se diferencian y actúan

- **Se ven distintos:** cada clase usa su propio modelo (remera, casco/boina/gorra, mochila) y sus armas en la espalda y en la mano (ver §4).
- **Se leen distintos:** el roster (abajo a la izquierda) dice "1 · Asalto / 2 · Flanqueador / 3 · Medico", la etiqueta de RTS dice la clase (`ASALTO 180/180`, enemigos `Enemigo FUSILERO …`) y la franja de la mira muestra "nombre · vida · arma · CLASE".
- **Actúan:** el médico cura solo (§5), el asalto puede disparar el lanzacohetes, y todos obedecen el radial (§7).

---

## 7. Radial de órdenes (mantener Q)

Un toque corto de **Q** sigue cambiando de soldado; **mantenerla** abre un **radial de 6 porciones** (`UI/MenuDeOrdenes.cs`, en el canvas de pantalla). Con el radial abierto la cámara se congela y el movimiento del mouse mueve un cursor virtual (como una rueda de armas); al **soltar Q** se ejecuta la porción resaltada, y los números **1–6** eligen directo.

| # | Porción | Qué hace (`PlayerInputDriver.EjecutarOrdenRadial`) |
|---|---|---|
| 1 | SUBIR AL TANQUE | manda a todos los aliados al tanque **aliado** (nunca a uno enemigo) |
| 2 | BAJAR TODOS | baja a todos los ocupantes (menos a vos) |
| 3 | ATACAR | todos atacan al enemigo apuntado (o al más cercano a ≤ 100 m) |
| 4 | IR ALLÍ · TODOS | todos van al punto apuntado, repartidos en formación |
| 5 | IR ALLÍ · SOLO EL 2 | solo el soldado 2 va al punto apuntado |
| 6 | CUBRIRSE · TODOS ALLÍ | todos toman la cobertura apuntada (si no hay, lo dice) |

Prueba de cada porción en SC_Gameplay (Q real mantenida + tecla del número, y una con el mouse):

1. Subir: `tank occ=2` a los 6 s ([80](../Assets/Validacion/Ronda4/80_radial_1_subir_al_tanque.png)).
2. Bajar: `[FLUJO] Radial: bajar todos del tanque` → `occ=0`.
3. Atacar (con el enemigo a ~40 m): Kes y Doc en `MovingToAttackOrder` sobre `Enemigo_Patrulla_1` ([83](../Assets/Validacion/Ronda4/83_radial_3_atacar.png)).
4. Ir allí todos: `Radial: 2 aliados van a (-7.16, 0.92, 10.50)` ([81](../Assets/Validacion/Ronda4/81_radial_4_ir_alli_todos.png)).
5. Solo el 2: `Radial: solo Soldado_2_Kes va a (-6.77, 0.70, 9.55)`; Doc no se movió.
6. Cubrirse: `Soldado_2_Kes va a cubrirse en (-8.50, 0.00, 8.87)` / `Soldado_3_Doc … (-7.50, 0.00, 10.13)` y luego `EN COBERTURA` ([82](../Assets/Validacion/Ronda4/82_radial_6_cubrirse.png)).

Elegir con el mouse: Q mantenida + `mdelta(40, 30)` → `Seleccion = 1` ("BAJAR TODOS") resaltada en naranja ([20](../Assets/Validacion/Ronda4/20_radial_abierto_sel1.png)); soltar la tecla la ejecuta ([21](../Assets/Validacion/Ronda4/21_radial_ejecutado_ir_alli_todos.png)).

Las 5 órdenes viejas (formación en línea/cuña, sígueme, alto, curarme) siguen existiendo en `EjecutarOrdenDelMenu(1..5)` (la suite las ejerce); "sígueme" sigue en **Y**.

---

## 8. Arte en todo el blockout

`Editor/BlockoutArtDresser.cs` (menú *Strategic Point › Arte › 9*; idempotente) viste **cada cubo del blockout** con los prefabs de `Prefabs/ArteMundo`, sin tocar la jugabilidad: el cubo conserva su collider, su `ObstacleMarker` y su NavMesh; solo se apaga su `MeshRenderer` y se le cuelga un hijo `ArteBloque` que **cancela la escala del cubo** (así los módulos se colocan en metros reales). Como el arte es hijo del cubo, **desaparece al derrumbarse** y **se aplasta con las etapas de daño**.

| Blockout (material) | Arte |
|---|---|
| Muro / Muralla | módulos `P_Mod_Muro_Recto` corridos + pilares |
| Brecha | `P_Mod_Muro_RotoAlto` (muro roto) |
| Casa / Cuartel | 4 paredes con puerta hacia el camino y ventanas alternadas, pilares y techo de losas |
| Torre | paredes + techo + emplazamiento de ametralladora arriba |
| Coberturas | barricadas de sacos o de concreto, cajas de madera, auto quemado, neumáticos, bunkers con MG |
| Bidones / obstáculos destructibles | barril y cajas apiladas |

Además reparte por el campo **vegetación, barriles, faroles, cráteres y escombros** (semilla fija, lejos de la ruta central y de los cubos, sin colliders; la cantidad se escala con el área). Resultado en SC_Gameplay: **107 cubos vestidos con 863 módulos + 236 props**; en SC_Tutorial: 31 cubos, 357 módulos y 98 props.

Capturas: [60 base](../Assets/Validacion/Ronda4/60_arte_base_inicio.png) · [61 paso del cañón](../Assets/Validacion/Ronda4/61_arte_paso_del_canon.png) · [62 aldea](../Assets/Validacion/Ronda4/62_arte_aldea.png) · [63](../Assets/Validacion/Ronda4/63_arte_aldea_2.png) · [65 fortín](../Assets/Validacion/Ronda4/65_arte_fortin.png).

---

## 9. La vida ya no se repite

Antes la vida del jugador aparecía en **tres lugares** más el roster ([00](../Assets/Validacion/Ronda4/00_estado_inicial.png): panel "VIDA 180/180" a la derecha, barra verde flotante sobre la cabeza y el roster). Ahora **queda solo abajo a la izquierda (el roster)**:

- `PlayerInputDriver`: el panel `PlayerHealthView` ("VIDA") ya no se muestra.
- `HealthBarView.Tick`: la barra flotante no se dibuja sobre el soldado que manejás (`AiBrain.IsPossessedByPlayer`).
- `VehicleStatusView`: en el tanque, la lista de tripulación ya no repite "180/180 vida" ni sus barras.

Se conservan las barras flotantes de los **demás** soldados cuando reciben daño y el anillo del enemigo bajo la mira (eso no es la vida propia).

---

## 10. Qué se probó y qué no

- Todo lo de arriba se probó en Play con teclas/botones reales inyectados (Q mantenida, dígitos, Shift, clic derecho mantenido, etc.). Para apuntar se usó el mismo ayudante que en el tutorial (solo gira la mira hacia un objeto).
- **No se probó a mano**: la escopeta ni la metralleta disparando (sí sus mirillas/loadouts están definidos y el catálogo compila; en la suite no hay pruebas específicas nuevas de ellas), el francotirador enemigo disparando, ni el tanque con el nuevo `FindTheVehicle` en el nivel 4x más allá de subir/bajar.
- El roster usa "Medico" sin tilde y la fila cambió a "1 · Asalto" con letra de 11 pt para que "Flanqueador" entre sin partirse.
- Cambios en escenas: se guardaron `SC_Gameplay`, `SC_Tutorial` y (por la suite) `SC_TestLevel`.
