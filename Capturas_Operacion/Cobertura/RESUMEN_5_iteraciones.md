# IA de cobertura: 5 iteraciones medidas

Banco: `Strategic Point > IA > Banco de coberturas (Play)` (`Assets/_Project/Scripts/Editor/CoberturaBench.cs`). Duelos 4 contra 4 en un campo en espejo
(6 muros bajos + pilares altos por lado), tiempo x4, mismos alcances y vida para los dos bandos, el lado donde juega cada version se alterna.
**Dif. de vida** = vida que le saco al rival menos la que perdio (de 720). Error estandar entre +-. Control none vs none: -44 +- 40 (el banco es simetrico).

| Iteracion | Que cambia (decision ponderada) | Contra la cobertura vieja | Contra NO cubrirse |
|---|---|---|---|
| 0 - la de siempre | la cobertura mas cercana con linea de tiro, siempre | (control +55 +- 52) | **-508 +- 31** |
| 0 - ponderada v1..v5 original | puntaje (disparo, distancia, vida, direccion del enemigo, multitud, amenazas...) pero SIEMPRE cubriendose | v1 +20, v2 -37, v3 -63, **v4 -225, v5 -224** (peor) | v5: -429 +- 28 |
| 1 | DECIDIR si cubrirse: presion = vida + recarga + cuantos enemigos lo ven + cercania del blanco | - | -206 +- 36 |
| 2 | + CICLO oculto/asoma segun vida (sano asoma casi siempre, herido se esconde) | - | -95 +- 51 (solo CICLO: -278) |
| 3 | + preferir cobertura alta / direccion / amenazas | - | -163, -191, -198 (no mejora: DESCARTADA) |
| 4 | umbral de presion para cubrirse: 0.6 / 0.9 / 1.3 / 1.8 | - | -165 / -95 / **-4 +- 55** / -80 -> umbral 1.3 |
| 5 - FINAL (version 5 por defecto) | DECIDIR + CICLO con umbral 1.3 | **+306 +- 43 (32 gana, 3 pierde, 5 empata, de 40)** | -37 +- 35 (paridad, N=40) |

Lecciones (la causa raiz): cubrirse CUESTA fuego -un soldado oculto no dispara y de pie sobre un muro bajo apunta x2.5 peor que agachado-, asi que
elegir "mejor" el punto no alcanzaba: lo que vale es decidir CUANDO cubrirse. La IA nueva pelea de pie mientras esta sana y sin presion, y se cubre
cuando se hiere, recarga o lo ven varios; con la cobertura cumple su rol sin regalar fuego. Contra la cobertura vieja gana 32 de 40 duelos.
