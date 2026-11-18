# Ronda 13 — Plan de implementación (borrador v1, sin mejorar)

Fuente: dos listas pegadas por el dueño el 2026-11-16 (L1 = 10 puntos de gameplay/nivel, L2 = 12 puntos de entorno/nivel 2).
Se fusionan los duplicados (calle del comienzo, solapes, colliders) para quedar en **20 trabajos (T1–T20)**.
Método pedido: (1) capturar el ESTADO ACTUAL, (2) implementar, (3) capturar el RESULTADO, (4) comparar y validar punto por punto.

## 0. Método de evidencia (común a todo)
- Arnés existente: `StrategicPoint.exe -capturas <dir>` (`Core/CapturasDeBuild.cs`) + `captura_info.txt`. Se **amplía** con tomas nuevas, mismas para "antes" y "después" (solo cambia el juego).
- ANTES: se construye la build actual (commit `95b80a95`, ya en `Builds/Windows64`) y se corre el arnés ampliado → `Docs/RONDA_13/antes/`.
- DESPUÉS: build nueva → `Docs/RONDA_13/despues/`; atlas lado a lado `Docs/RONDA_13/ATLAS_antes_despues.jpg`; tabla `VALIDACION.md` con una fila por punto (OK / parcial / no, y la medida).
- Cada trabajo define su **medida objetiva** (número en `captura_info.txt` o `Check` de editor) además de la foto.
- Reglas del repo: no commitear sin orden; guardar escenas tocadas (MarkAllScenesDirty + SaveOpenScenes); commits terminan con `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.

## 1. Trabajos

### Combate y armas
**T1 · Cuchillo letal + sangre** (L1-1)
- Hoy: `WeaponHolder.cs` `KnifeDamage = 55`, `CuchilloFx.Tajo` (swing + KnifeHit sordo).
- Cambio: una cuchillada mata a cualquier soldado/civil (daño ≥ vida máx; usar `int.MaxValue`-seguro o `Health.Kill`), al Pesado (`EnemigoPesado`) hacen falta **2** (daño = ceil(vida/2) + 1, con ventana entre golpes); no afecta vehículos/helicóptero. Sonido nuevo de sangre/carne (`SfxKind.KnifeBlood`, sintetizado como los demás en `GenericSfx`). Partículas de sangre **muy visibles**: ráfaga de 40–60 partículas rojo saturado (HDR/emisivo, fuera del postproceso oscuro), gotas con gravedad, salpicadura-decal breve en el piso, y un destello rojo corto. Solo si conecta.
- Medida: log `cuchillo: matado=N pesado_golpes=2`; foto con partículas.
**T2 · Lanzacohetes vs helicóptero jefe** (L1-2)
- Hoy: `WeaponCatalog` Rocket `Damage 95`, `MultiplicadorVsVehiculo 2.5`; `HelicopteroEnemigo` (vida, coeficientes de `AjustesDeDano`).
- Cambio: medir vida del heli final y cuántos cohetes lo matan; objetivo **≤ 4 cohetes** (Fácil) / ≤ 6 (Difícil). Subir multiplicador vs aeronave (`MultiplicadorVsAeronave`) o daño de explosión directo, comprobar que el cohete **impacta** al heli (colisión con su collider / radio de explosión 5 m vs altura de vuelo).
- Medida: `Check` que dispara N cohetes al heli y registra vida restante.
**T3 · Daño: UI de configuración y alcance global** (L1-3)
- Hoy: `PanelDeDano` (tarjeta anclada al borde derecho de Configuraciones; **no verificada en pantalla real**, puede quedar fuera de cuadro). `AjustesDeDano` aplicado en `Health.TakeDamage` y `Vehicle.TakeDamage`.
- Cambio: reubicar la tarjeta DENTRO del panel de Configuraciones (sección propia, scroll si hace falta) y comprobar en 1280×720, 1600×900 y 1920×1080; auditar que **toda** vía de daño pase por el coeficiente (cuchillo, explosiones, torretas, perros, heli, tanque, caídas, `Vehicle`, `TorreDestruible`, objetos destructibles) y que `Restablecer` funcione.
- Medida: `Check` que aplica 100 de daño por cada vía con coeficiente 2× y 0,5× y verifica el resultado.

### Tanque y feedback
**T4 · Mira del tanque** (L1-5)
- Cambio: al subir al tanque la cámara arranca **dentro de cabina/mira fijada** (equivalente a mantener click derecho; el click derecho alterna), y un **indicador circular de dirección de disparos recibidos** (anillo con marcas rojas que apuntan a la fuente, se desvanecen 2 s) en el HUD del tanque (`HudDeTanqueSimple`).
- Medida: captura de la vista inicial en el tanque; evento de daño con atacante a la izquierda/atrás muestra la marca correcta (`Check` con ángulo calculado).
**T5 · Revivir: un solo engranaje radial** (L2-5)
- Hoy: al revivir aparecen varios feedbacks (anillos, íconos, textos).
- Cambio: solo UN engranaje que se llena radialmente **en el cursor** durante `TiempoDeRevivir`; suprimir los demás elementos visuales del revivir.
- Medida: conteo de objetos de feedback activos durante el revivir == 1 (ver `revive_*.png` de `Capturas_Operacion`).
**T6 · Liberar milicianos: cinemática 2 s + feedback** (L1-6)
- Hoy: `OperacionDirector.TerminalDeCeldas.cs` (terminal, `G.PuertaDeCeldas`).
- Cambio: al activar la terminal, cámara de 2 s mostrando las puertas de las celdas abriéndose (animación de puertas), luego devuelve el control; aviso en pantalla "N MILICIANOS LIBERADOS", sonido y marcadores en los liberados.
- Medida: `Check` de duración (1,8–2,2 s), puertas abiertas, control devuelto; fotos de la secuencia.

### IA
**T7 · Perros en manada sin solaparse** (L1-8)
- Hoy: `PerroEnemigo` (agente NavMesh, sin separación entre perros).
- Cambio: separación (steering de separación/`NavMeshAgent.avoidancePriority` distinto + radio de 0,9 m), formación de manada (líder + flancos con ángulos), spawn escalonado en puntos distintos.
- Medida: `Check`: con 12 perros persiguiendo 20 s, distancia mínima par-a-par ≥ 0,7 m en el 99 % de los cuadros; 0 pares dentro de 0,4 m.
**T8 · Enemigos atrapados en cubículos** (L1-9)
- Cambio: localizar los spawns de la 2.ª puerta del comienzo, comprobar NavMesh/caminos desde cada spawn al jugador (`NavMeshPath.status == Complete`), reubicar o abrir el cubículo.
- Medida: `Check` de caminabilidad de TODOS los spawns de la fase de infiltración: 0 sin ruta.

### Nivel (geometría, rutas, entorno)
**T9 · Calles con sentido y comienzo** (L1-4, L2-1, L2-2)
- Cambio: quitar la ruta/calle del comienzo; tras la zona de computadoras la calle se bifurca en **T** (no aparece de golpe); rediseño del spawn y entorno del comienzo; plaza final con sentido.
- Medida: vista cenital antes/después (ya existe `DISENO_NIVEL_cenital.jpg` como molde).
**T10 · Colliders de bloqueo → cartel "Debes volver a la guerra"** (L1-10)
- Cambio: eliminar los colliders que impiden retroceder/derivar; poner un cartel transparente (texto mundo) que aparece al alejarse del objetivo, sin colisión. Los límites del mundo quedan solo donde son físicos.
- Medida: `Check`: el jugador puede caminar hacia atrás en todo el recorrido; cartel presente.
**T11 · Solapes de geometría y objetos duplicados** (L1-7, L2-3, L2-8)
- Cambio: barrido automático (`Physics.ComputePenetration`/bounds de renderers con misma malla y posición) en las 3 escenas aditivas; corregir o borrar duplicados; quitar títulos de zona innecesarios.
- Medida: informe `solapes_antes.txt` / `solapes_despues.txt` (cantidad por categoría).
**T12 · Camión y gasolina fuera** (L2-4)
- Cambio: eliminar el camión del comienzo y la gasolina; la 1.ª puerta se abre solo detonando el explosivo de la puerta simple. Actualizar el modelo del camión que quede en otras zonas y de otros assets en mal estado.
- Medida: 0 objetos "camion"/"gasolina" en el comienzo; la puerta se abre con la carga.
**T13 · Cuadro gris del obj. 4 y colliders** (L2-7)
- Cambio: localizar y quitar el cuadrado gris; acomodar colliders de la escena.
**T14 · Ambientación de desierto** (L2-6)
- Cambio: arbustos rodantes, partículas de ambiente (polvo/arena), árboles como en nivel 1 y más decoración; medir rendimiento antes/después (`PerfFrames`), presupuesto: no bajar de 60 fps en la media de la ruta.
**T15 · Plaza: textura de calle realista** (L2-9)
- Cambio: reemplazar el checker por una calle/plaza creíble (asfalto, vereda, líneas, adoquín) tomando referencias de fotos reales (Lavalle y centros similares) por búsqueda web; replicar en estilo cubico low-poly con más detalle.
**T16 · Assets menos primitivos** (L2-11)
- Cambio: pasada de detalle (marcos, techos, ventilaciones, ruedas, etc.) sin romper el estilo cúbico.
**T17 · Pulido general nivel 2** (L2-3)
- Cambio: colliders faltantes, modelos y elementos fuera de lugar, objetos superpuestos (se apoya en T11).
**T18 · Criterios de evaluación del Parcial 2** (L2-10)
- **Bloqueado**: el repo no contiene el documento de criterios. Se pide al dueño; mientras tanto se audita contra `Docs/AUDITORIA_100_ITEMS.md` y `FICHA_STRATEGIC_POINT.tsv`.
**T19 · Checks y regresión**
- Actualizar checks viejos (409u/960a esperaban ×16; el multiplicador del tanque es ×32), correr el lote existente.
**T20 · Build, RAR, entrega y commit** (solo con orden).

## 2. Orden (olas)
1. Estado actual (build "antes" + capturas + informes de solapes).
2. Combate: T1, T2, T3 (código independiente).
3. Tanque/feedback: T4, T5, T6.
4. IA: T7, T8.
5. Nivel: T12 → T9 → T10 → T13 → T11 → T17 → T14 → T15 → T16.
6. Checks, build, capturas "después", atlas, validación.

## 3. Riesgos
- Escenas aditivas binarias/serializadas: editar con builders y guardar; no `git checkout`.
- Perf del desierto: medir antes de agregar.
- Cuchillo mata-todo desbalancea sigilo: confirmar con el dueño si el jugador también puede morir al cuchillo enemigo (no cambia).
