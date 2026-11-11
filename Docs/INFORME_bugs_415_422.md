# Bugs #415 a #422 (2026-11-11)

Atlas antes/después: `Docs/ATLAS_bugs_415_422.jpg`. Checks: `ChecksBugs065.Nov11.cs` (415a–422a, 806a, 899a = fotos).

| # | Causa | Arreglo |
|---|-------|---------|
| 415 | UPS, paneles y torres de servidor del centro de datos | `AjustesDeNivelNov11` los desactiva en runtime (enganchado en `AjustesDeNivelNov08.ProcesarTransform`) |
| 416 | Tanque con modo dios bajaba vida / barra; trabas medidas sin hitches (p50 14–23 ms) | `ModoDios.Protege` cubre al tanque propio; sigue publicando el feedback de impacto |
| 417 | Rombo del coche enemigo se tiñó como carrocería (cubo gris) | `Vehicle.PintarElRombo` (rojo para enemigos) y `ConvertirEnTanque` no tiñe `RomboTanque` |
| 418 | Corriendo, la subida por cuadro superaba el margen de 0,6 m y `RevisarBorde` lo hacía caer bajo la rampa | Margen por velocidad + `RampaCaminable.AjustarAlMoverse` en `SoldierMotor.Move` |
| 419 | Enemigos veían a los presos | `Soldier.Encarcelado`: fuera de `SpatialGrid`, de los sentidos, francotiradores y helicóptero |
| 420 | Punto de uso de la terminal a 7 m de la pantalla | `PosDeUsoDeLaTerminal` (0,8 m) y rombo/baliza sobre la pantalla (0,3 m) |
| 421 | Muertos en el aire no caen (el motor sólo busca piso al caminar). Causa exacta de origen no confirmada | `CaidaDeCuerpos`: los hace caer con gravedad |
| 422 | Equipo de azotea y barrotes gruesos negros | Misma lista de `AjustesDeNivelNov11` |

Notas: en el atlas, #418 es un esquema con los datos del check (la mansión tapa la cámara); los puntos intermedios son ilustrativos. Regresión #398–#414 y #700a OK (403a falló una vez por carga y pasa aislado).
