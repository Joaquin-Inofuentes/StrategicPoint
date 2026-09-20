# Ronda 11 · Después

Contraparte de `ANTES.md`. Decisiones tomadas con los valores por defecto del plan (D1–D5) y `ARTS` sin tocar. Evidencia en `evidencia_despues/`.

## Estado por punto

| # | Pedido | Resultado | Cómo se verificó |
|---|--------|-----------|------------------|
| 1 | Al morir todos no termina la partida | **Hecho.** `Mision/EstadoDePartida.cs` evalúa a toda la escuadra cada tick (`WorldSimulationDriver.Step`): con enemigos a menos de 25 m de algún caído → derrota a los 2 s; sin acción → cuenta de 4 s y revive. | Runtime (derrota mostrada) + Fase 20 |
| 2 / 15 | El médico no revive / no viene a revivirme | **Hecho.** `RescateAutomatico` ahora manda al médico a caminar hasta 0,6× el alcance de revivir (reordena cada 1 s) y revive con `HealedEvent`. `AiBrain` Follow caía a Patrol con el líder muerto (causa raíz). F9 mata al soldado que manejás (o al primero vivo), F10 revive. | Runtime: el médico camina y revive |
| 3 / 12 | Dispersión y alcance de enemigos/aliados | **Hecho** (solo en partida principal, `Dificultad.Activa`; el tutorial y la suite quedan deterministas). Causa raíz medida: la esfera de impacto del soldado es de **1 m de radio** (`Projectile.hitRadius`), así que la dispersión chica de 1–6° siempre acertaba. Piso de dispersión 3° enemigo / 2,2° aliado; alcance ×2,7 ataque / ×1,8 visión (fusil ≈ 35 m, SMG ≈ 25, escopeta ≈ 12, francotirador ≈ 56). | 8–28 m: antes 93–100 % de impactos; ahora 41 de 88 disparos (47 %) en la misma escena (`evidencia_despues/03_combate_ia`) |
| 12 / 13 | Retardo y ráfagas; herido dispara al cubrirse | **Hecho.** Reacción de 0,25–0,6 s al ver al blanco, ráfagas de 3–5 tiros con pausas de 0,4–0,9 s (`AiBrain.Disparo.cs`). Un soldado herido (< 50 %) busca cobertura y mientras corre hace pausas de 0,9 s para devolver una ráfaga de 2–3 tiros. | Log de disparos: pausas de 0,52–0,92 s; el disparo herido está cubierto solo por revisión de código (no logré provocar la cobertura en escena) |
| 4 | UI rota al darle Jugar | **Hecho.** El panel de dificultad usaba medidas de 1920×1080 sobre un lienzo de 960×540; ahora se dibuja a media escala y con fondo opaco. | Captura `00_menu_jugar/01_dificultad_c.png` |
| 5 | Glitch al terminar la caminata | **Hecho.** `SoldierAnimatorDriver` conserva la última dirección y limita el vector Adelante/Lateral a 1. Antes `Adelante` caía de 0,92 a 0,00 en un cuadro; ahora decae 0,98 → 0,72 en ~0,1 s. | `04_animaciones/log.jsonl` |
| 6 | Animación de costado con la base bajo tierra | **Hecho.** Compensación de altura de pies (mide pie más bajo en reposo y sube el modelo lo que falte, ≤ 0,1 m). Pie más bajo: izquierda 0,040 → 0,119 (reposo 0,130); derecha 0,092 → 0,125. | ídem |
| 7 | Salto aterriza mal | **Hecho.** Subida ×1,5 y bajada ×1,8, cruces de 0,05 s (`ArtBuilder.AjustarTiemposDeSalto`). Aterrizaje visual a 0,07 s del contacto (antes 0,18) y idle a 0,31 s (antes 0,74 s tarde). | ídem |
| 8 | Q mantenido radial / tap acción por defecto | **Hecho.** Umbral 0,5 s; el toque sin blanco = SÍGANME. | Fase 8 y 20 |
| 9 | Bomba del asalto a mitad de distancia | **Hecho.** `Demolicion.AlcanceMaximo` 9 → 4,5 m; el muro del tutorial a 4,8 m. | Fase 20 |
| 10 | HUD izquierdo: 3 renglones | **Hecho.** `N · Especialidad` / `vida · arma` / barra. | Captura `05_hud/02_hud_rifle.png` |
| 11 | HUD derecho arma bugueada, falta francotirador | **Hecho.** Íconos propios para Sniper, Smg, Shotgun y Rocket (`Resources/UI/WeaponIcons`, dibujados con Pillow). | Captura `05_hud/01_hud_sniper.png` |
| 14 | Quitar Ctrl para especial | **Hecho.** E mantenido ≥ 0,5 s = especial, tap = interactuar; Ctrl solo agacha. | Código + Fase 20 (constantes); no probado con teclado real |
| 16 | Cursor RTS sobre interactuables | **Hecho** (`CursorContextual`: normal / interactuable / atacar). | No probado con mouse real |
| 17 | Zoom RTS sin lerp, doble velocidad | **Hecho.** `AnimarZoom` sin suavizado, 40 → 80. | Fase 20 |
| 18 | RTS 1/2/3 y F1/F2/F3 | **Hecho.** Los grupos de control pasan a 4–9. | Código; no probado con teclado real |
| 19 | RTS resaltar cobertura apuntada | **Hecho** (`CoverHologram.ResaltarSolo`). | No probado con mouse real |
| 20 | FPS todos muertos y en calma → revivir en 4 s | **Hecho** (ver 1). | Runtime: revivieron a 4,0 s con el 50 % de vida |

## Lo que NO se pudo verificar en esta sesión
- Los puntos 14, 16, 18 y 19 dependen de teclado y mouse reales; se compilaron y se cubrieron con constantes en la suite, pero no se jugaron.
- El fuego de cobertura del herido (13) no llegó a dispararse en mis pruebas porque no encontré una cobertura válida cerca; la lógica es simple y aislada (`AiBrain.Disparo.cs`, `FuegoDeCoberturaHerido`).
- La distribución de precisión (47 %) sale de una sola escena de ~90 disparos: es orientativa, no un benchmark.

## Suite
`Run All Tests Headless`: **OK** (incluye la nueva FASE 20). `verificar_estatico.py`: OK. La Fase 19 falla si se corre dos veces seguidas sin reabrir `SC_Gameplay` (la suite deja la escena mutada en memoria): **reabrir la escena entre corridas**.

## Autoplayer del tutorial
Corrida `20260920_145634`: **COMPLETA**, 35 OK / 0 fallidos / 0 reintentos / 1 saltado (`victoria`), 309 s. Cubre Q mantenido/radial, E, demolición a 4,5 m y zoom con las nuevas teclas.
Una corrida previa había perdido en el paso 12 por un bug ya existente: `SC_Tutorial` traía un objeto `Mision` que lanzaba las líneas enemigas y la derrota de misión. `MisionDirector.Awake` ahora se destruye en el tutorial.

## Extra
`ComandosDeDepuracion.Matar()` (F9) cae al primer soldado vivo de la escuadra cuando no hay soldado manejado (RTS).
