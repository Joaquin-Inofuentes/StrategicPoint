# Ronda 15 — validación de la lista de pendientes (16 pedidos)

Atlas JPG (ANTES / DESPUES de cada caso): `ATLAS_R15_0_estado.jpg` (tabla de estado), `_1_cuchillo_tanque`, `_2_celdas_terminal`, `_3_ui`, `_4_calle_solapes`, `_5_spawns_colliders_perros`, `_6_armas` (DPS). Capturas crudas en `antes/` (build del 18/11) y `despues/` (build limpia del 20/11); poses en `CapturasDeBuild.R15.cs`.

| Pedido | Estado | Qué se hizo |
|---|---|---|
| A1 Daño de cuchillo | OK | Letal: 55 de daño, 0,55 s; pesado cae en 2 golpes (R13). Sin cambios, validado en captura |
| A2 Balancear armas | Parcial | Tabla de DPS sostenido medida. Sin cambios: subir la cadencia del Fusil contradice el pedido #244 del usuario |
| A3 Credibilidad a las calles | Parcial | La calle arranca en el patio del tanque (R14); el resto es subjetivo |
| A4 Mira del tanque | OK | Inclinación inicial 10°→3°, periscopio 0,85→1,15 m |
| A5 Feedback PC celdas | OK | Pulso de luz que late con el progreso + destello verde al terminar |
| A6 Solapamientos | OK | `ValidacionR15.Solapados` (ComputePenetration ≥ 25 cm): 2 pares reales, apagados |
| A7 Perros en manada | OK | Verificado en captura, sin falla reproducible |
| A8 Enemigos bloqueados en spawn | OK | 114 spawns: 0 fuera de NavMesh, 0 dentro de collider, 0 encerrados, 0 en bolsillo |
| A9 Colliders innecesarios | OK | 108 CapsuleCollider de arbustos quitados; 184 piezas con hueco ya en MeshCollider (R14) |
| B1 Environment pulido | Parcial | Subjetivo; se limpió lo medible |
| B2 Feedback de interacciones | OK | Pulso/destello en terminal; engranaje de revivir ya existía |
| B3 Pulido nivel 2 | Parcial | Barrido automático de solapes y colliders; el resto exige revisión humana |
| B4 Apagar UI al interactuar | OK | `UiDeInteraccion`: el panel de objetivo baja a 12 % al revivir/hackear/armar carga y vuelve solo |
| B5 Assets menos primitivos | **No** | Requiere arte nuevo (modelos); no se resuelve por código |
| B6 Conos de minimapa | OK | Textura 256 px bilineal con alfa anti-aliasing |
| B7 Criterios de parcial 2 | **No** | No hay archivo de criterios en el repositorio; falta que se pasen |

## Bug real encontrado
`EnemyAlertIndicatorView` lanzaba `NullReferenceException` repetida (10 por corrida) al pintar el marcador; ahora `PintarMarcador` tolera material nulo. Corrida final: 0 excepciones.

## Verificación
Suite headless OK, build limpia 20/11 (Windows64), 37 capturas con 0 excepciones. Probado jugando: nada.
Pendiente: reportes #429, #430, #431 de la planilla sin atender.
