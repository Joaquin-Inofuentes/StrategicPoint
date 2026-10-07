# Iteracion 1 y 2 - decidir si cubrirse (DECIDIR) y ciclo segun vida (CICLO), contra un rival que NO se cubre (30 duelos 4v4)

v356 = DECIDIR - v612 = CICLO - v868 = DECIDIR + CICLO. Dif. = vida que le saco al rival menos la que perdio (de 720); +- = error estandar.

| Version | Dif. vida | Gana | Empata | Pierde | Vivos prop. | Vivos rival | Segundos |
|---|---|---|---|---|---|---|---|
| v0 (cobertura de siempre) | -508 +- 31 | 0 | 9 | 21 | 0,3 | 3,6 | 21 |
| v5 (ponderada original) | -429 +- 28 | 1 | 11 | 18 | 0,4 | 3,4 | 24 |
| v356 DECIDIR | -206 +- 36 | 4 | 1 | 25 | 0,3 | 2,2 | 14 |
| v612 CICLO | -278 +- 39 | 4 | 0 | 26 | 0,2 | 2,2 | 11 |
| v868 DECIDIR + CICLO | -95 +- 51 | 9 | 3 | 18 | 0,7 | 1,5 | 15 |

Controles del banco: none vs none = -44 +- 40; v356 con umbral infinito (nunca se cubre) vs none = +51 +- 34 -> el banco es simetrico.
