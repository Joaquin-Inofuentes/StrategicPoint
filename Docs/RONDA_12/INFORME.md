# Ronda 12: informe

Capturas en esta misma carpeta (copia de `Assets/Validacion/R12/`). Estado real, punto por punto.
**Verificado** = probado en Play con captura o log. **Codificado** = implementado y cubierto por la suite/CI pero sin captura propia.

| # | Pedido | Estado | Evidencia |
|---|--------|--------|-----------|
| 1 | El aliado revivido por el médico no tenía geometría; las flechas de aliados de la HUD deben ser triángulos | **Verificado** | Causa: `CubeFxReactor.MorirAnimado` ocultaba el cuerpo a los 2 s y nada lo volvía a mostrar. Ahora `Health.Revivido` deshace todo (`AlRevivir`). Mato a Vega, espero 4 s, `Health.Initialize` (mismo camino que médico/[E]) → 6/7 renderers activos. `revive_01`, `revive_02`. Flechas = `SpriteDeTriangulo` (visibles en `revive_02`). |
| 2 | Decal de pared/piso placeholder → sprite real | Codificado | `DecalPool` usa sprites Kenney (`Resources/UI/Impactos`). Sin captura dedicada. |
| 3 | 2 fuentes bélicas en toda la UI | Verificado en HUD y tutorial | Black Ops One + Stardos Stencil (`FuentesBelicas`). `tutorial_fuentes_01`, HUD en `revive_*`. Menú principal no fotografiado. |
| 4 | Distinguir Q sostenida de Q rápida sobre interactuables/aliados/enemigos | **Verificado** | `AccionRapidaDeQ`. `q_01..q_04` (toque → acción directa; sostenido → radial). |
| 5 | Impacto de granada/cañonazo con sprite real | Codificado | `SpriteFx`. Sin captura dedicada. |
| 6 | Munición = moneda 3D giratoria (prefab) | **Verificado** | `MunicionPickup` + `MonedaMunicionBuilder`. `coin_01/02`. |
| 7 | El tanque destruye obstáculos | **Verificado** | `Atropello`. `frag_01`. |
| 8 | El tanque, al chocar con enemigos, los destruye | Codificado | Usa `Atropello.Barrer` sobre soldados; sin captura dedicada. |
| 9 | Física y fragmentación en piezas reales | **Verificado** | `Fragmentador` (rigid bodies por pieza). `frag_02_a..f`, restos del tanque en `tanque_enemigo_ext_*`. |
| 10 | Enemigos en tanque me disparan | **Verificado** | Log de proyectiles con `team=Enemy`, jugador muerto a 28 m. `tanque_enemigo_dispara_01..04`. |
| 11 | El tanque hacía tambalear la cámara | Corregido, sin re-medir | Offset de `CameraRig` corregido. No repetí la medición numérica de yaw. |
| 12 | Muros con cara superior solapada (Maya) | **Verificado** | `MY_Modulares.mb` corregido con Maya 2026 batch, 7 FBX re-exportados, prefabs reimportados. `muro_01/02`; el test de la suite comprueba 34 triángulos en el muro recto. |
| 13 | Puesto de metralleta del tanque más alto y mirilla normal | **Verificado** | `mg_antes_*` vs `mg_despues_*`. |
| 14 | Mira del cañón del tanque distinta a la FPS (periscopio) | **Verificado** | `canon_01..03` (el último ajuste de la escalera del retículo no se re-fotografió). |
| 15 | Los cañonazos impactan contra mí y contra obstáculos | Parcial | Obuses enemigos matan al jugador (log + `tanque_enemigo_dispara_*`). Sin captura limpia de un obús destruyendo un obstáculo. |
| 16 | Actualizar el tutorial | **Verificado** | Textos de Q, periscopio, metralleta y tanque actualizados (+ `LocTextos` ES→EN). Autoplayer: 36/36 pasos, 0 fallidos, 0 errores, 126 capturas. |

## Pruebas
- Suite headless (`Strategic Point/Run All Tests Headless`): OK.
- CI estático (`Tools/Ci/verificar_estatico.py`): OK (2886 archivos).
- Autoplayer del tutorial: 36/36.

## Lo que NO queda verificado
- Sprites de decal e impacto de explosión (2 y 5): sin captura.
- Menú principal con las fuentes nuevas.
- Cámara del tanque: sin medición posterior a la corrección.
- Tanque aplastando soldados enemigos y obús destruyendo un obstáculo, en captura.
