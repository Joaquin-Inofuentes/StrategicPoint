# Resumen del plan: bugs #065 a #101 (37 bugs, 14 paquetes)

El detalle completo está en `01_Plan_bugs_065_101.md`. Los paquetes se ejecutan en serie: un solo agente toca el editor a la vez.

| Orden | Paquete | Bugs | Esfuerzo | Por qué |
|---|---|---|---|---|
| 1 | WP0 Columna Operación | (habilitante) | alto | Partials del director y del builder, subfases, numeración n/6, APIs de HUD, torretas del centro portadas al builder, `ChecksBugs065`. |
| 2 | WP3 UI opciones / minimapa | 098, 099, 100, 065 (minimapa) | bajo | Causas medidas; cambios locales de UI. |
| 3 | WP1 Combate | 094, 095 (HUD), 079, 065 (cursor), 096 | alto | Geometría del headshot sin reabrir el "100% headshot"; buff ×2. |
| 4 | WP2 Audio | 065 (3D), 095 (distorsión / 3D), 072 (graves), 093 | bajo | Llamadas mapeadas 1 a 1; se verifica con Historial. |
| 5 | WP4 IA de combate | 080, 081, 074, 085, 086, 087 | alto | Núcleo táctico; validar con CoberturaBench. |
| 6 | WP5a Interacción / revivir / agachado | 083, 084, 088, 089, 092 | alto | Clip agachado (trampa de la caché de Library) y bloqueo de movimiento. |
| 7 | WP5b Engranajes / animaciones / destinos | 066, 067, 068 | alto | IK procedural para todas las acciones. |
| 8 | WP6 Vehículos (sistemas) | 091, 090, 069, 071, 070, 072 (visual), 073 (tuning) | alto | Torreta estabilizada, IA contra vehículos, rebuild. |
| 9 | WP7 Heli de extracción y victoria | 075, 076, 077 | bajo | Visual y cámara; parámetro opcional en ShowVictory. |
| 10 | WP8 Rediseño del cuartel | 078 (+085 ubicación) | alto | 4 sectores, 2 torres de francotirador, 2 torretas vigía, 4 reflectores con operador. |
| 11 | WP9a Puestos "2 voladuras" + intro con pasos | 082, 097 (parte 1), 084, 085 | alto | Muralla única, cargas solo del ASALTO, contraataque durante la mecha de 20 s, cinemática con bloqueo. |
| 12 | WP9b Huir: jefe / reparación / carrera / final | 097 (parte 2), 073 | alto | GOLIAT con piso del 5%, Kes repara 25 s, carrera de 70 s con cañadón y heli HALCÓN, cámara lenta final. |
| 13 | WP10 Resistir con RTS obligatorio | 101 (+074) | alto | Radio + vista táctica forzada; el reloj solo corre con los sectores cubiertos; tutorial paso a paso. |
| 14 | WP11 Integración / regresión | todos | alto | `CorrerTodos`, suite headless, perf y pasada de punta a punta 1→6. |

## Reproducido en el editor (Play)
- **#094:** la hitbox de la cabeza está en el cuello (pivote+0.25). 8 tiros a la cabeza visible: 0 headshots. Además no se muestra en ninguna parte.
- **#092:** de pie la malla flota +0.13 m; agachado se entierra −0.11 m.
- **#095:** munición infinita en la Operación; el HUD muestra solo el cargador.
- **#098:** perillas de 22x46.
- **#099:** el panel vive en el canvas del HUD y cambia de tamaño en pantalla con el slider.
- **#100:** lista de 112 líneas, preferred 2405 px en una caja de 300.
- **#086:** hasta 9 pares de enemigos a menos de 0.7 m (mínimo 0.03 m).
- **#091 (parcial):** el casco zigzaguea y arrastra a la torreta hasta 11°.
- **#065:** el minimapa no tiene muros (captura `diag_065_minimapa.png`).

## Sin reproducción directa (confirmados por código o datos)
- **#080/#081:** situación puntual; la medición de altura de tiro no fue concluyente. Hipótesis fuerte: rayo a 0.8 m contra cobertura evaluada a 1.3 m.
- **#087:** falta la lógica.
- **#069/#071:** filtro `OccupantCount==0`.
- **Audio (065/072/089/093/095):** no se escucha por CLI; confirmado por spatialBlend y clips.
- **#070:** datos del json contra coordenadas del builder.
- **#083/#084:** filtro de rol sin feedback.
- **#066/#067/#068/#075/#076/#077:** pedidos confirmados por código y png.
- **#073/#078/#082/#097/#101:** diseño.

## Decisiones clave
- Ningún rediseño agrega fases al enum: se usan subfases internas y se preservan los 6 objetivos, "n/6" y la cadena de victoria.
- Fronteras sin solape: WP8 z < −220 · WP9a z −220..−100 · WP9b patio, ruta y cañadón · WP10 ciudad.
- Anclas de zona para que la intro se adapte sola. Nunca editar SC_Operacion a mano (el builder es idempotente y la reconstruye).
- #101: se eligió volver RTS-obligatorio el objetivo 5 en lugar de agregar un 7.º, para no romper la numeración ni las pruebas.

## Riesgos principales
1. Rebuilds del builder: perdían las torretas del centro (#043); WP0 las porta.
2. Regresión de headshot al 100%: el check exige 0 headshots al pecho.
3. Balance de la IA nueva y de la FURIA ×2.
4. Rendimiento en el cuartel (4 SpotLight) y en la ciudad (milicianos y oleadas; ya hubo lag en #062).
5. Tamaño de WP9b: se puede partir en 9b-1 (jefe + reparación) y 9b-2 (carrera + heli + final).
6. Pruebas de RTS sin clic real: usar las APIs de selección y órdenes.
