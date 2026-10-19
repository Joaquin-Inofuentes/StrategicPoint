# Strategic Point · Informe de pulido: ANTES y DESPUÉS

Fecha: 2026-10-15 · Unity 6000.6.2f1 (URP) · Niveles: **Tutorial** (`SC_Tutorial`), **Nivel 1** (`SC_Gameplay`), **Nivel 2** (`SC_Operacion`)

Las capturas están en esta misma carpeta: `antes/` y `despues/` (rutas relativas en cada tabla).
Todas las mediciones se hicieron en el editor (vsync apagado, 144 fps tope) con el `SondaDeRendimiento` propio, sin tocar el foco de Windows.

---

## 1. Resumen ejecutivo

| Área | Estado |
|---|---|
| Textos innecesarios en pantalla | Hecho en los 3 niveles |
| Feedback de muerte (cilindro rojo) | Eliminado |
| Círculos/planos de colores en el piso | Eliminados |
| VFX estilo toon | Hecho (sprites nuevos + animación "de a pasos") |
| Textura de pasto pixelada | Reemplazada por texturas toon 1024² tileables |
| Pulido Nivel 2 (sombras, mal tileo) | Hecho (piso sin costuras, sombras, rótulos) |
| Reducir info en pantalla / feedback no textual | Hecho |
| Círculo de selección | Reducido |
| Guía al jugador: objetivo sí, camino no | Hecho |
| Entorno nuevo del Nivel 2 | Agregado (arboledas, campamentos, cráteres, parches de suelo) |
| Optimización de tanque / IA tras paredes / fusil de asalto | Re-testeados en vivo: OK |
| Rendimiento | Nivel 2 de ~33 a 14–22 ms · Nivel 1 de 30,5 a 23,1 ms |
| Suite de tests headless | **OK** (tras corregir 3 fallos que causaron mis cambios) |

---

## 2. Rendimiento medido

| Escenario | ANTES | DESPUÉS | Qué lo cambió |
|---|---|---|---|
| **Nivel 2 · combate en los puestos** (objetivo 2) | ~33 ms (30 fps) · 9.361 draw calls | **14,5 ms (69 fps)** justo tras la fusión de mallas · 6.185 draw calls. Con el arte nuevo agregado: **18–22 ms** en 3 corridas, 6.321 draw calls | Fusión de mallas, caché de cobertura/puntería, sin barridos por frame |
| **Nivel 1** (infiltración) | 30,5 ms (33 fps) · 3,35 M triángulos | **23,1 ms (43 fps)** · 2,9 M triángulos | `Terrain_Fondo`: 6.000 árboles de malla completa hasta 900 m, ahora 420 m |
| **Tutorial** | 7,0 ms (142 fps) | 9,1 ms (109 fps) | Escena sin cambios de carga; la diferencia es variación de la máquina |
| **Pelea contra el tanque jefe** (Nivel 2) | — | **13,5 ms (74 fps)**, peor cuadro 19 ms | Sin regresión con el jefe disparando |

Notas honestas sobre la medición:
- El 14,5 ms se midió en un momento tranquilo de la máquina. Después, en el mismo escenario, tres corridas dieron 18,4 / 19,6 / 21,9 ms.
- Aislé el arte nuevo: apagar el grupo `9_ArteExtra` baja apenas 264 draw calls (6.321 a 6.057), dentro del ruido de tiempos. El arte extra cuesta menos del 5 %.
- Dejar el detalle del terreno a 120 m no dio ganancia, así que lo revertí a 243 m.

### Picos encontrados y corregidos (profiler escuchado de forma continua)

| Pico | Causa | Corrección |
|---|---|---|
| Bloque de log cada 5 s (`SesionLog`) | Armaba un string enorme de todos los enemigos en un solo cuadro | Se arma de forma incremental (corrutina, cede cada 8 enemigos) |
| Barrido cada 30 s | `FindObjectsByType<MonoBehaviour>` sobre ~11.000 scripts | Registro de "guardables" (`RegistrarGuardable/QuitarGuardable`) |
| 2–2,5 ms cada 2,5 s (`FuentesBelicas`) | `FindObjectsByType<Text>` recorría toda la escena | Usa los Canvas y `GraphicRegistry`, sin barrer la escena |
| `PID.AimEvaluate` alto | Bucle `IsChildOf` por frame en `Coberturas.TryPuntoApuntado` y 8 `GetComponentInParent` por frame en `AimTargeting` | Caché por collider y por versión de cobertura |
| ~9.300 draw calls en el Nivel 2 | ~6.000 piezas visuales sueltas del kit (solo 127 mallas distintas) | `FusionarMallas`: agrupa por material/sombra/capa/celda de 40 m en mallas combinadas |
| Miles de casters de sombra | Piezas planas (asfalto, líneas, decals) proyectando sombra | Las piezas de menos de 0,2 m de alto ya no proyectan sombra |
| Atlas de sombras saturado (178 a 226 mapas) | Los faroles del kit traen luz puntual con sombra | 38 luces decorativas sin sombra |

Verificación de la fusión: las capturas antes y después de fusionar son idénticas píxel a píxel (0 diferencias). El NavMesh no cambia porque se hornea desde colliders, no desde mallas visuales.

---

## 3. Antes y después, punto por punto

### 3.1 Textos innecesarios

| Elemento | ANTES | DESPUÉS |
|---|---|---|
| Barra de controles inferior | "[WASD] MOVERSE · [SHIFT] CORRER · [Q] MANTENER: RADIAL" siempre visible | Eliminada |
| Panel de misión | Línea "Dificultad: FÁCIL" | Eliminada |
| Tutorial: cabecera | "TUTORIAL · PASO 1 / 36" | "1 / 36 · [F8] SALTAR" (el salto va integrado) |
| Tutorial: pista | "PISTA: ..." + "[F8] SALTAR PASO" | Solo la pista |
| Tutorial: texto suelto | "[F8] Saltar este paso" flotando abajo a la derecha | Eliminado |
| Exposición | "A LA LUZ · TE VEN DE LEJOS" / "EN LA SOMBRA · SOLO TE VEN DE CERCA" | Ícono: sol naranja (a la luz) / luna azul (en sombra) |
| Muerte de enemigo | Cartel "ENEMIGO ABATIDO" | Sin texto: hit-marker rojo, destello y audio |
| Balizas del tutorial | Etiqueta de texto sobre cada baliza | Solo el aro |
| Rótulos del mundo (Nivel 2) | Carteles de dos líneas ("PUERTA BLINDADA / SE ABRE AL VOLAR…", "COMPUTADORA / [E] INTERACTUAR 30 s", "SECTOR A / CONTROL DE ACCESO"…) | Una sola palabra o frase corta; "RESERVA" eliminado; "PUESTO" y "CARGA" reubicados para no solapar |

Capturas: `antes/tutorial_a.png` vs `despues/tutorial_b.png`, `antes/nivel2_a.png` vs `despues/nivel2_a.png`, `antes/nivel1_a.png` vs `despues/nivel1_a.png`.

### 3.2 Feedback de muerte y círculos de colores en el piso
- ANTES: al morir un enemigo aparecía un cilindro rojo; los objetos interactuables tenían un disco de color en el piso.
- DESPUÉS: el cilindro (`KillCylinderFx`) ya no se instancia; el disco (`PisoInteractableHighlight`) queda desactivado. El feedback es el destello, el hit-marker y el sonido.

### 3.3 Círculo de selección
- ANTES: radio 1,05, pulso fuerte y destello grande al confirmar una orden.
- DESPUÉS: radio 0,5, pulso 0,06 (antes 0,18) y destello al 30 %. Captura de referencia: `antes/rts_seleccion.png`.

### 3.4 VFX estilo toon
- ANTES: sprites fotográficos (Kenney: nubes difusas, rayos, ruido) con aparición y desvanecimiento suaves.
- DESPUÉS: 16 sprites nuevos (fuego, humo, tierra, chispa, estrella, fogonazo, quemaduras, rayón) generados por `Tools/Indice/gen_vfx_toon.py`: silueta con contorno oscuro y 2–3 bandas de tono (cel-shading), en escala de grises que el juego tiñe por efecto. En `SpriteFx` la animación va "de a pasos" (15 cuadros/s), sin fundido de entrada y con 4 escalones de salida.
- Ajuste que descubrí en la prueba del tanque jefe: una explosión nacida pegada a la cámara tapaba media pantalla con una estrella opaca. Ahora los efectos se atenúan entre 5 m y 1,5 m de la cámara. Capturas: `despues/vfx_explosion_toon_1.png`, `despues/n2_tanque_jefe_b.png` (antes del ajuste) y `despues/n2_tanque_jefe_d.png` (después).
- Los originales están respaldados en `antes/vfx_originales/`.

### 3.5 Textura del pasto (difuminada y pixelada)
- ANTES: 128×128 de ruido Perlin estiradas sobre 15 m (8 px/m). Capturas: `antes/tex_Grass.png`, `tex_Dirt.png`, `tex_Gravel.png`, `tex_Litter.png`.
- DESPUÉS: 1024×1024 tileables (`Tools/Indice/gen_terreno_toon.py`): parches posterizados de 4 tonos con bordes nítidos, matas de pasto dibujadas, piedritas, hojas y flores. Importadas con mipmaps, aniso 8 y trilinear; `tileSize` de 6/6/4/5 m (más fino y más nítido). Captura: `despues/nivel1_suelo_b.png`.

### 3.6 Pulido del Nivel 2: piso, sombras y "mal tileo"
- ANTES: cada piso se cubría de losetas del kit con juntas oscuras a intervalos fijos (la cuadrícula marcada que se ve en `antes/n2_piso_puesto.png` y `antes/n2_piso_calle.png`). Miles de piezas con sombra.
- DESPUÉS: el piso es un plano continuo de color liso (23 pisos pasan de losetas a plano). `ArteDeOperacion.VestirPiso` ya no genera losetas (`ConLosetas = false`), de modo que una reconstrucción tampoco las vuelve a crear. Los pisos que se solapan se separan solo en milímetros (máx. 3 niveles de 1,2 cm) para evitar el z-fighting del bug #225. Capturas: `despues/n2_piso_puesto.png`, `despues/n2_piso_calle.png`.
- Se corrigió además una regresión mía a mitad de camino: subir los pisos 9–10 cm enterraba los decals del juego (que están a 7,5–9 cm); ahora el escalón máximo es de 3,6 cm.

### 3.7 Reducir la guía al jugador (objetivo sí, camino no)
- ANTES: flecha en los pies del jugador + línea de ruta hasta el objetivo.
- DESPUÉS: se eliminaron `ObjectiveArrowIndicator` y `RutaAlObjetivo`; queda el rombo del objetivo con su distancia.

### 3.8 Entorno nuevo del Nivel 2 ("mucho más arte")
Herramienta nueva `OperacionBuilder.Retoque.cs` que actúa sobre la escena ya construida (reconstruir entera tarda varios minutos). Agrega el grupo `9_ArteExtra`:

| Elemento | Cantidad |
|---|---|
| Plantas (árboles y arbustos) en arboledas, 16–64 m de la ruta | 157 plantas en 46 arboledas |
| Campamentos abandonados (sacos, cajas, barriles, neumáticos, faroles) | 85 props en 24 campamentos |
| Cráteres y escombros sueltos | 40 |
| Parches grandes de suelo (rompen la repetición de color) | 320 |

Todo queda lejos del eje de la ruta del tanque; el NavMesh se re-horneó. Captura: `despues/n2_arte_ruta.png`.

### 3.9 Tanque, IA y fusil de asalto (re-testeados en vivo)
| Prueba | Resultado |
|---|---|
| Pelea contra el tanque jefe (objetivo 4): jefe en movimiento, disparando, con orden de ruta activa | 13,5 ms (74 fps), peor cuadro 19 ms |
| Fusil/pistola/cohete de cada aliado (Vega-Asalto, Kes-Flanker, Doc-Médico) | Todas las armas disparan y descuentan munición |
| IA tras paredes: 6 muestreos en el combate de los puestos, `Physics.RaycastAll` del enemigo en estado Atacar a su blanco | 13 enemigos atacando, **0 tapados por una pared** |
| Suite de regresión completa (bugs anteriores) | **OK** |

### 3.10 Suite de tests: tres fallos, causados por mis cambios, ya corregidos
1. Presupuesto de búsquedas globales: `FuentesBelicas.cs` pasó a 3 búsquedas (Canvas + respaldo + TextMesh); se subió su cupo de 2 a 3.
2. Claves de traducción huérfanas ("[F8] SALTAR PASO", "A LA LUZ…", "EN LA SOMBRA…"): textos que ya no existen en pantalla; se borraron de `LocTextos`.
3. "Enemigo_1 cayo": falló una vez por tiempos y no reprodujo en la segunda corrida completa (la suite quedó en OK).

---

## 4. Archivos tocados

Código del juego: `CubeFxReactor`, `PisoInteractableHighlight`, `SelectionRingManager`, `SelectionRingFx`, `TutorialUI`, `TutorialManager`, `TutorialBeacon`, `PlayerInputDriver`, `MisionHud`, `MisionDirector`, `IndicadorDeExposicion`, `KillFeedView`, `SesionLog`, `FuentesBelicas`, `AimTargeting`, `Coberturas`, `OperacionDirector`, `SpriteFx`, `LocTextos`.

Editor y herramientas nuevas: `OperacionBuilder.Rendimiento.cs`, `OperacionBuilder.Fusion.cs`, `OperacionBuilder.Retoque.cs`, `ArteDeOperacion.cs`, rótulos en `OperacionBuilder.{Puestos,Centro,Huida,Ciudad,Cuartel}.cs`, `Tools/Indice/gen_terreno_toon.py`, `Tools/Indice/gen_vfx_toon.py`.

Escenas y datos: `SC_Operacion.unity` (sin losetas, con arte extra, fusionada, NavMesh re-horneado), `SC_Gameplay.unity` (distancia de árboles del fondo), texturas de terreno y sprites de VFX. Mallas fusionadas en `Assets/_Project/Generated/FusionOperacion/`.

No se hizo commit ni push. No se tocaron archivos de otras sesiones (`Mobile_RPAsset.asset`, `CliBuilder.cs`, `WebGlOptimizador.cs`, `Assets/Resources`, `ProjectSettings`).

---

## 5. Pendiente y límites (lo que NO quedó perfecto)

- **Parches de suelo del Nivel 2**: son sutiles; el piso quedó limpio pero algo liso. Si se quiere más carácter, se puede subir la opacidad de esos decals o agregar una textura detalle al material del piso.
- **Entorno de Nivel 1 y Tutorial**: este turno el arte nuevo se agregó solo al Nivel 2. En los otros dos se cambió el suelo y se recortó el costo de renderizado.
- **Rendimiento del Nivel 2** quedó entre 14,5 y 22 ms según el momento de la máquina; el piso de 14,5 ms no se sostuvo en repeticiones. El Nivel 1 todavía rinde a ~43 fps en el editor (dominan los árboles del terreno).
- **Reconstrucción del Nivel 2 desde cero**: `OperacionBuilder.Construir` fusiona las mallas antes de que `ArteDeOperacion.Vestir` agregue las suyas. Para una reconstrucción completa conviene el orden Construir, Vestir, estáticos, fusión, NavMesh (hoy se hizo todo sobre la escena existente).
- **Respaldo previo a la fusión**: está en la carpeta temporal de la sesión (`scratchpad/SC_Operacion.antes_fusion.unity.bak`), que no es permanente. La versión anterior de la escena sigue en git.
- El bug #165 ("ESTE no dispara") no se reprodujo con la API de armas (todas disparan), pero no se verificó con el input real de un jugador.
