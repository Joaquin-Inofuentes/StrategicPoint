# Strategic Point

Shooter tactico en primera persona con vista RTS para dar ordenes a la escuadra. Unity 6 (6000.5.6f1), URP, Input System.

## Como jugarlo

| Tecla | Accion |
|---|---|
| WASD · Shift · Ctrl · Espacio | mover, correr, agacharse, saltar |
| Clic izq. / clic der. | disparar / apuntar (zoom propio de cada arma) |
| R · 1-3 · F · G | recargar, cambiar de arma, cuchillo, granada |
| Q (mantener) | radial de ordenes (ir alli, cubrirse, atacar, curar, tanque, demoler...) |
| Tab | alternar primera persona / vista RTS |
| Esc | pausa (controles, ajustes de pantalla, calidad y daltonismo) |
| F10 | HUD minimo |

El menu principal tiene **JUGAR** (mision de 4 objetivos) y **TUTORIAL** (36 pasos; con **F8** se salta un paso y con **F7** se retoma donde se dejo).
Hay soporte de mando (stick izquierdo mueve, derecho mira, gatillos disparan).

## Correr las pruebas

La suite (mas de 460 verificaciones escritas, varias dentro de bucles, en 19 fases) corre dentro del Editor:

* Menu **Strategic Point > Run All Tests Headless**. El resultado queda en `Temp/suite_result.txt` (`OK` o `FALLO`).
* En consola / CI: `Unity -batchmode -nographics -projectPath . -executeMethod SP.EditorTools.HeadlessTestRunner.RunAll -logFile -` (sale con codigo 0 si todo paso).
* Otras opciones del mismo menu: 100 iteraciones para cazar flakiness, benchmark de rendimiento, estres con 50+ unidades.
* Metricas de un build real: `StrategicPoint.exe -spmetrics` escribe `spmetrics.txt` (fps, GC por frame, memoria).

El codigo de la suite esta en `Assets/_Project/Scripts/Editor/HeadlessTestRunner*.cs` (una parte por rango de fases). `Assets/_Project/Tests/` queda reservado para pruebas de Unity Test Framework si algun dia se agregan.

## Escenas generadas por codigo

`SC_TestLevel`, `SC_MainMenu`, `SC_Tutorial` y parte de `SC_Gameplay` **se generan por codigo** (`Editor/*Builder.cs`, `HeadlessTestRunner`). Editarlas a mano es inutil: se pierden al volver a generarlas. Si queres un cambio permanente, hacelo en el builder correspondiente. La suite ya restaura `SC_TestLevel` y `P_Vehicle_Blindado` al terminar para no ensuciar el repositorio.

## Estructura

```
Assets/_Project/Scripts   Actors, Ai, Combat, Core, Player, UI, Presentation, Mision, Tutorial, Vehicles, Editor
Assets/_Project/Scenes    menu, gameplay, tutorial, nivel de pruebas
Assets/ARTS               fuentes de arte y modelos (ver nota abajo)
Assets/Validacion         capturas de cada ronda de validacion
Docs                      informes de cada ronda y el tutorial paso a paso
```

Nota: `Assets/ARTS` pesa ~336 MB pero solo ~35 MB estan referenciados por escenas y prefabs; el resto son fuentes del artista (`.mb`, `.psd`, capturas y un `SP_Arte.rar` de 90 MB). Estan versionados a proposito y no se borraron.
`Assets/InputSystem_Actions.inputactions` es el asset de acciones **de proyecto** de Unity; lo usa el modulo de UI (botones y menus), asi que no sobra.

## Documentos

* [Indice de la auditoria de 100 puntos](Docs/AUDITORIA_100_ITEMS.md)
* [Ronda 9](Docs/RONDA_9_MEJORAS_Y_AUDITORIA.md) · [Ronda 8](Docs/RONDA_8_AUDITORIA_Y_MEJORAS.md) · [Ronda 7](Docs/RONDA_7_SALTO_ARMAS_SONIDO_CUCHILLO_GRANADA_Y_FEEDBACK.md) · [Ronda 6](Docs/RONDA_6_RADIAL_CONTEXTUAL_TORRETA_MIRA_Y_UX.md) · [Ronda 5](Docs/RONDA_5_MISION_RESCATE_RADIAL_DIFICULTAD.md) · [Ronda 4](Docs/RONDA_4_CLASES_ARTE_RADIAL.md) · [Informe ronda 2](Docs/INFORME_RONDA_2.md)
* [Tutorial paso a paso](Docs/TUTORIAL_PASO_A_PASO.md) · [Tutorial (resumen)](Docs/TUTORIAL.md)
