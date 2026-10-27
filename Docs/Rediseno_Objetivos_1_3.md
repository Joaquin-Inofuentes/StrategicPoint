# Rediseño de los objetivos 1 a 3 (Infiltrar, Puestos, Centro de datos)

Proyecto: `DV_C6_StrategicPoint`, escena `SC_Operacion`. Documento de diseño del 2026-10-27. Las coordenadas son las del nivel actual (eje z de sur a norte).

## 1. Diagnóstico del recorrido actual

| # | Objetivo hoy | Verbo real | Zona | Problema |
|---|---|---|---|---|
| 1 | Volar las torretas para abrir el portón | destruir | Cuartel z -340..-220 (4 sectores, 28 enemigos) | Es "destruir cosas" y el cuartel es un laberinto sin historia: contenedores, barracas y patio sin una razón de ser. |
| 2 | Volar los 2 puestos (cargas Oeste y Este) | destruir | Muralla z -170 (2 bunkers + portón blindado) | Mismo verbo que el 1. Las dos cargas son idénticas y simétricas (x ±38): se juega dos veces lo mismo. |
| 3 | Hackear la computadora (30 s con oleadas) | defender / tiempo | Centro de datos z -80..-40 | Es el único objetivo con un verbo distinto, y llega tarde. |

Resultado: dos tercios del recorrido piden lo mismo ("poné carga / rompé"), y el jugador no entiende por qué las torretas abren un portón ni por qué hay que volar dos bunkers para llegar a una sala de servidores.

Además hay repetición de assets: solo el kit modular reparte `SM_Mod_Muro_Recto` x3.036, `Muro_Pilar` x851 y `Losa` x322 por todo el nivel. Los cuatro sectores del cuartel se diferencian por el color de piso (gris, ocre, oliva, azul) y no por la forma.

## 2. Referencia real: la incursión de Bruneval (febrero de 1942)

Paracaidistas británicos asaltaron de noche una estación de radar alemana (Würzburg) en un acantilado de la costa francesa. La estructura real de esa operación es exactamente la que necesita el nivel:

1. **Aproximación en silencio** hasta el perímetro de alambre y los puestos de guardia.
2. **Ruptura de la defensa exterior**: un grupo limpia la villa y los puestos de ametralladora; otro asegura la ruta de retirada.
3. **Objetivo técnico**: los especialistas desmontan el radar y se llevan sus componentes (acá: el centro de datos y el hackeo).
4. **Extracción** por la playa bajo fuego.

Traducción al juego: el cuartel es el perímetro de la estación, la muralla es la línea de contención con el portón, el centro de datos es la sala del radar. Todo el nivel pasa a ser "una instalación de alerta temprana": antenas, grupo electrógeno, alambre, torre de vigilancia, sala de operaciones.

## 3. Rediseño: tres actos con tres verbos distintos

| Acto | Verbo | Objetivo en pantalla | Cómo se juega |
|---|---|---|---|
| 1. PERÍMETRO | Sigilo y flanqueo | "Cortá la energía del cuartel" | Hay UN generador (grupo electrógeno) en el sector D. Al destruirlo se apagan los 4 reflectores y se abre el portón norte del cuartel. Las 4 torres dejan de ser el objetivo: pasan a ser francotiradores que se pueden evitar con la sombra (los reflectores ya apagados no las ayudan). |
| 2. LA MURALLA | Demolición con escolta | "Volá el portón blindado" | UNA sola carga, en el portón del centro. Los dos bunkers son la defensa (zona de muerte frontal). Quien flanquea y silencia un bunker abre un atajo lateral hacia la carga. Se elimina la segunda carga y la simetría. |
| 3. LA SALA DE RADAR | Defensa y tiempo | "Hackeá la computadora" | Se mantiene el hackeo de 30 s con oleadas. Sube la tensión porque el acto 2 terminó con una explosión y el cuartel ya está en alerta: las oleadas llegan por las 3 puertas. |

Por qué es mejor:
- **Un verbo por acto**: sigilo, demolición, defensa. Hoy son destruir, destruir, defender.
- **Causa y efecto legibles**: la energía apaga las luces y abre el portón; la carga rompe el portón; el hackeo es el final. Cada objetivo se explica solo con lo que se ve.
- **Menos objetivos, más variedad**: de 2 cargas + 4 torretas + 1 hackeo pasa a 1 generador + 1 carga + 1 hackeo.
- **El ASALTO tiene un rol claro**: el generador es de la escuadra de asalto y la carga del portón también; el jugador decide a cuál acompañar.

## 4. Diseño espacial por acto

### Acto 1: perímetro (z -340..-220), "la estación"
- Entrada sur por la brecha (x -8..8), que se mantiene.
- **Alambre y control de acceso (z -339..-312)**: se mantiene la chicana de Jersey, pero se baja a una sola línea de visión larga con una garita. Sirve para enseñar que la luz importa: el reflector R1 barre el paso.
- **Dos rutas con riesgo distinto**:
  - Oeste (barracas): corta, estrecha, con guardias a corta distancia. Premia el cuchillo y el sigilo.
  - Este (depósito): larga, con contenedores y cobertura buena, pero pasa bajo la torre de vigilancia. Premia el tiro a distancia.
- **Patio de armas central**: se reduce a una zona de muerte visible desde las dos rutas. Es el riesgo de cortar camino.
- **Sector D (comando y parque motor)**: el generador va al fondo, con tres camiones como cobertura. Es el "premio" del acto y un punto de reunión de las dos rutas.
- Al destruir el generador: apagón (se apagan los reflectores), el portón norte se abre y suena una alarma baja. Ese apagón es el único cambio de iluminación del nivel y marca el paso al acto 2.

### Acto 2: la muralla (z -218..-100), "la línea de contención"
- Aproximación abierta (z -218..-173) con Jersey en diagonal hacia cada bunker, sacos y autos quemados. El centro es zona de muerte.
- **Portón blindado** (x -8..8): el objetivo. La carga se coloca en su cara sur, con la caja y el LED actuales (sin placa ni disco, ver #299 y #300).
- **Bunker Oeste** y **Bunker Este**: ya no se vuelan. Cada uno tiene su nido de ametralladora y una puerta lateral. Si se limpia un bunker aparece un atajo lateral que acorta la ruta a la carga en 25 m. Cada bunker se vuelve una decisión: asaltarlo o cruzar el campo abierto.
- **Corredor de trincheras (z -165..-100)**: se conserva como descompresión, con guardias rezagados opcionales.

### Acto 3: sala de radar (z -80..-40), "el corazón"
- Sin cambios de forma: la sala cubierta de 8 m, el techo con bandejas, los racks como cobertura.
- Mejora: el hackeo ya no es una cuenta regresiva abstracta. La computadora tiene un indicador de progreso visible desde la entrada y la salida norte se abre con la voladura al terminar (ya implementado en #291).

## 5. Evitar la repetición de assets

Reglas para el nivel, aplicables en los builders y en los retoques en sitio:

1. **Un material y una forma dominante por sector**. Hoy el sector se distingue por el color del piso; pasa a distinguirse por silueta:
   - Perímetro de acceso: hormigón y alambre, líneas largas y bajas.
   - Barracas (oeste): techos a dos aguas, ventanas, madera y chapa.
   - Depósito (este): contenedores apilados a 2 y 3 alturas, rampas, grúa.
   - Comando y parque motor (norte): estructura metálica, antenas, generador.
2. **Un "héroe" por zona**: una pieza única que nunca se repite (la grúa del depósito, la antena parabólica del comando, el generador). Es lo que el jugador recuerda y usa para orientarse.
3. **Tope de repetición**: ninguna pieza del kit idéntica más de 3 veces seguidas a la vista; se alterna con una variante (rotación de 90°, espejo, escala en y de ±15 %, variante de la pieza `Puerta` o `Medio`).
4. **Props con rol, no relleno**: cada caja, barril y saco tiene que ser cobertura, pista de ruta o decoración de una escena (un puesto de guardia con mesa y radio). Se eliminan los grupos de props que no cumplan ninguna de las tres.
5. **Medición**: un check (`DetectorDeRepeticion`) cuenta, por sector, cuántas piezas iguales hay en el mismo radio de 20 m y falla si pasa de 3.

## 6. Colores de enemigos y aliados

Estado actual (`SoldierLook`, `DiamondGizmo`): los aliados ya llevan tinte azul (`TinteAliado` 0,12/0,42/1,0) y los enemigos usan el material base oscuro (camuflaje verde/negro) con un brillo gris. Hipótesis a confirmar con capturas (el editor estaba ocupado): de noche, el camuflaje oscuro y el azul oscuro se funden a más de 30 m y ambos bandos quedan como siluetas oscuras. Los marcadores (rombos y minimapa) ya separan bien: enemigo rojo (1,0.05,0.05), aliado azul (0,1/0,45/1), jugador amarillo.

Propuesta de paleta (dos familias, por tono y por luminosidad):

| Grupo | Uniforme | Detalle | Marcador | Luminosidad |
|---|---|---|---|---|
| Aliados y jugador | Azul acero + blanco | Cinta blanca en el casco, parche azul claro | Azul cian (0,1/0,45/1) | Alta (se lee de noche) |
| Jugador | Igual que los aliados | Cinta dorada en el casco (el amarillo ya es "yo") | Amarillo (1,0,85/0,05) | Alta |
| Enemigos | Rojo óxido y negro | Casco oscuro con visor rojo emisivo | Rojo (1,0,05/0,05) | Media-baja, con un acento emisivo tenue |
| Civiles | Verde pastel | Sin armas | Verde | Media |

Reglas:
- **Separación de tono mayor a 120°** entre las dos familias y **una sola familia por bando** (hoy el médico es azul claro y casi blanco; queda dentro de la familia azul).
- **Luminosidad distinta**: los aliados son más claros que su entorno nocturno; los enemigos son más oscuros pero con un acento emisivo para no desaparecer.
- **Forma además de color**: los enemigos con casco cerrado, los aliados con gorra o boina. Es lo que se lee cuando hay daltonismo o poca luz.
- **Proyectiles y destellos**: trazadoras rojo-naranja para enemigos, cian para aliados.
- **Barras de vida**: aliados cian, enemigos rojo.

Puntos de código: `Actors/SoldierLook.cs` (materiales por equipo y médico), `Presentation/DiamondGizmo.cs` (colores de marcadores), `Presentation/UnitLocatorCylinder.cs`, `Presentation/MinimapIcon.cs`, `Combat/Projectile.cs` (color de la trazadora), `Editor/SoldierVariantBuilder.cs` (variantes por clase).

## 7. Plan de implementación por etapas

| Etapa | Qué | Archivos | Riesgo | Verificación |
|---|---|---|---|---|
| A | Paleta aliados/enemigos: material rojo óxido para enemigos, azul acero para aliados, trazadoras y barras de vida por bando | `SoldierLook`, `Projectile`, `HealthBarView`, `DiamondGizmo` | Bajo | Captura de una escuadra y un grupo enemigo a 10, 30 y 60 m, de noche; check de contraste |
| B | Acto 2: una sola carga en el portón, bunkers sin carga, atajo lateral | `OperacionBuilder.Puestos`, `OperacionDirector.Puestos`, `PuestoDeVoladura` | Medio: cambia la cuenta de `volados` y los checks `Bug082`/`Bug097` | Reescribir esos checks y correr `OperacionPrueba.Arrancar(2)` |
| C | Acto 1: generador, apagón de reflectores y apertura del portón; las torres dejan de ser objetivo | `OperacionDirector.Infiltrar`, `ReflectorVigia`, `TorreDestruible` | Medio: cambia el texto de objetivos y el `TorresDeTorreta` | Check del apagón y de la apertura |
| D | Silueta y héroe por sector, tope de repetición | Retoques en sitio de `SC_Operacion` (nunca `Construir`) | Medio: escena binaria de 13 MB | `DetectorDeRepeticion` y capturas antes/después |

Orden recomendado: A (rápido, visible), B, C, D. Cada etapa se commitea por separado y trae sus checks.

## 8. Decisiones abiertas para el equipo
- ¿Se conserva la torre AA como objetivo opcional (puntos extra) o desaparece del todo?
- ¿El atajo lateral de cada bunker es un objetivo secundario con recompensa (munición, apoyo) o solo una ruta?
- ¿El apagón del acto 1 se mantiene hasta el final del nivel o los reflectores vuelven al activarse la alarma del acto 3?
