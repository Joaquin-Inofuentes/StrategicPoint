# Ronda 12: informe

Capturas en esta misma carpeta (copia de `Assets/Validacion/R12/`). Estado real, punto por punto.
**Verificado** = probado en Play con captura o log. **Codificado** = implementado y cubierto por la suite/CI pero sin captura propia.

| # | Pedido | Estado | Evidencia |
|---|--------|--------|-----------|
| 1 | El aliado revivido por el médico no tenía geometría; las flechas de aliados de la HUD deben ser triángulos | **Verificado** | Causa: `CubeFxReactor.MorirAnimado` ocultaba el cuerpo a los 2 s y nada lo volvía a mostrar. Ahora `Health.Revivido` deshace todo (`AlRevivir`). Mato a Vega, espero 4 s, `Health.Initialize` (mismo camino que médico/[E]) → 6/7 renderers activos. `revive_01`, `revive_02`. Flechas = `SpriteDeTriangulo` (visibles en `revive_02`). |
| 2 | Decal de pared/piso placeholder → sprite real | **Verificado** | `DecalPool` usa sprites Kenney (`Resources/UI/Impactos`). `decal_01_suelo` (agujeros de bala con grietas y cráter quemado). |
| 3 | 2 fuentes bélicas en toda la UI | **Verificado** | Black Ops One + Stardos Stencil (`FuentesBelicas`). `menu_fuentes_01` (menú principal), `tutorial_fuentes_01`, HUD en `revive_*`. |
| 4 | Distinguir Q sostenida de Q rápida sobre interactuables/aliados/enemigos | **Verificado** | `AccionRapidaDeQ`. `q_01..q_04` (toque → acción directa; sostenido → radial). |
| 5 | Impacto de granada/cañonazo con sprite real | **Verificado** | `SpriteFx.Explosion` en cámara lenta: `explosion_03_centro` (sprites de fuego/humo/destello). |
| 6 | Munición = moneda 3D giratoria (prefab) | **Verificado** | `MunicionPickup` + `MonedaMunicionBuilder`. `coin_01/02`. |
| 7 | El tanque destruye obstáculos | **Verificado** | `Atropello`. `frag_01`. |
| 8 | El tanque, al chocar con enemigos, los destruye | **Verificado (dato)** | 3 soldados enemigos con 300 HP frente al tanque a >3 m/s murieron los 3 (hp=0, sin disparos): log en esta sesión. `atropello_c1` muestra las armas caídas; la cámara de esa captura no encuadra el impacto exacto. |
| 9 | Física y fragmentación en piezas reales | **Verificado** | `Fragmentador` (rigid bodies por pieza). `frag_02_a..f`, restos del tanque en `tanque_enemigo_ext_*`. |
| 10 | Enemigos en tanque me disparan | **Verificado** | Log de proyectiles con `team=Enemy`, jugador muerto a 28 m. `tanque_enemigo_dispara_01..04`. |
| 11 | El tanque hacía tambalear la cámara | **Verificado (medición)** | Conduciendo W+D 15,8 s a 12 m/s: `camY` rango 0,02 m, `camZ` rms 0,009, `camX` rango 0,68 m (antes 0,93), yaw rms2 0,20 (antes 0,38), `tankY` rms2 0,0000 → el casco no rebota, no hay colliders empujando. |
| 12 | Muros con cara superior solapada (Maya) | **Verificado** | `MY_Modulares.mb` corregido con Maya 2026 batch, 7 FBX re-exportados, prefabs reimportados. `muro_01/02`; el test de la suite comprueba 34 triángulos en el muro recto. |
| 13 | Puesto de metralleta del tanque más alto y mirilla normal | **Verificado** | `mg_antes_*` vs `mg_despues_*`. |
| 14 | Mira del cañón del tanque distinta a la FPS (periscopio) | **Verificado** | `canon_01..03` (el último ajuste de la escalera del retículo no se re-fotografió). |
| 15 | Los cañonazos impactan contra mí y contra obstáculos | **Verificado** | Obuses enemigos matan al jugador (log + `tanque_enemigo_dispara_*`). Obús enemigo (radio 4, daño 150) contra una cobertura: `obus_01_antes`, `obus_06_slow_1..3` (obstáculo en trozos con cráter/anillo), `obus_05_fragmentos`. |
| 16 | Actualizar el tutorial | **Verificado** | Textos de Q, periscopio, metralleta y tanque actualizados (+ `LocTextos` ES→EN). Autoplayer: 36/36 pasos, 0 fallidos, 0 errores, 126 capturas. |

## Pruebas
- Suite headless (`Strategic Point/Run All Tests Headless`): OK.
- CI estático (`Tools/Ci/verificar_estatico.py`): OK (2886 archivos).
- Autoplayer del tutorial: 36/36.

## Lo que NO queda verificado
- Atropello de soldados: verificado por datos (3/3 muertos), sin un encuadre limpio del instante del choque.
- Escalera del retículo del cañón tras el último ajuste: sin re-fotografiar.
- Los puntos 65, 81 y 89 de la auditoría de 100 siguen fuera del alcance del código (ver `Docs/AUDITORIA_100_ITEMS.md`).
