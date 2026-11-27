# Cola de pendientes (post Ronda 19)

Estado (2026-11-27): puntos 1-4 HECHOS y validados (ver Docs/RONDA_19/VALIDACION_R19.md); el punto 5 (planilla) sigue pendiente: sin acceso. Orden = el que pidió el dueño (2026-11-27). Se trabajan de a uno, con check `Bug…` y captura antes/después como en R19.

## 1. Pulir colliders de objetos de la escena
Pedido: "disparo a enemigos y la bala se frena por un collider invisible; queda el decal flotando".
- Pistas: `Combat/NoFrenaBalas.cs` (ya existe el patrón), `Combat/Projectile.cs` (raycast de impacto + decals), colliders de props en `Editor/OperacionBuilder*.cs` y `Presentation/AjustesDeNivelR17.cs`/`R19.cs` (la fachada R19 agrega colliders: revisar los "sólidos" y coberturas).
- Hacer: auditoría de colliders invisibles (collider sin renderer o mucho mayor que su malla) en SC_Operacion; barrido con raycasts desde la línea de tiro típica a enemigos; marcarlos `NoFrenaBalas` o ajustarlos; el decal no debe quedar suspendido (anclar al punto real de impacto / quitar si no hay superficie visible).
- Check: raycast de bala a N enemigos en cobertura/patios sin que impacte un collider sin renderer.

## 2. El guardado debe guardar el PROGRESO del nivel, no solo la posición
Pedido: "morí en el blindado, cargué el último punto: la computadora estaba sin explotar y la pude traspasar igual".
- Pistas: `Core/PartidaGuardada.cs` (checkpoints de SC_Operacion), `OperacionDirector` (estado por fase: puestos, computadora, muro/compuerta, `SubfaseHuida`, `TomaDelJefeHecha`).
- Hacer: serializar el estado de objetivos/estructuras (computadora hackeada/explotada, muro caído, puestos abiertos, subfase, jefe) y restaurarlo al cargar; la computadora sin explotar debe ser sólida/obstáculo coherente con su estado.
- Check: guardar en Huir tras explotar la computadora, recargar y verificar que el muro/computadora quedan en el estado guardado y no se puede traspasar; ciclo guardar→cargar para cada checkpoint.

## 3. Balancear la parte del blindado (Goliat)
Pedido: "casi imposible de ganar, con 5 bombazos ya explotó" (se interpreta: el jugador lo pierde con 5 impactos del cañón).
- Pistas: `Operacion/TanqueJefe.cs` (vida del blindado 3200, cañonazo cada 5 s, metralla, piso 5 %), `OperacionDirector.Jefe.cs`, vida/daño del tanque del jugador, `ReparacionDeTanque.cs`, R19: orden de ataque al tanque al embarcar + umbral z −8.
- Hacer: medir (bajas propias/derrotas en 3 corridas con `OperacionPrueba.Arrancar(4)`), subir la durabilidad del tanque o bajar el daño del cañonazo/cadencia, dar ventanas de reparación/cobertura; objetivo: ganable con juego razonable. Cuidado con R9 (no subir la tasa de derrota).
- Aviso: el check `#097j` está roto de antes (escena con blindado a 1600 de vida vs `VidaMaxima` 3200): arreglarlo de paso.

## 4. Emprolijar totalmente la zona final
Pedido: "emprolijar totalmente zona final" (helicóptero/extracción, objetivo 6).
- Pistas: `OperacionDirector.Extraer.cs`, `OperacionDirector.Final*.cs`, `HeliDeDescarga.cs`, builders `OperacionBuilder.*` de la zona final y capas de arte (Decorado/Decals/Prestigio).
- Hacer: recorrer la zona con capturas, listar problemas (props flotantes/enterrados, colliders, vacíos, iluminación, pop-in), vestirla como la fachada R19 y comprobar rutas y NavMesh.
- Check: auditoría de piezas flotantes (estilo #501b) y cobertura visual de la zona final.

## 5. Revisar "feedback parcial 2" (Google Sheets)
https://docs.google.com/spreadsheets/d/1V0B0jG1W-t89TS2syU5m3H6SuxlwpeBpj8nrVoOqJ4U/edit?usp=sharing
- Pendiente: leer la planilla (requiere acceso con la sesión de Drive/Chrome; no se abrió todavía), pasar cada fila a esta cola con prioridad y agruparla con los puntos 1–4 si coincide.
