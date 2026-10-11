# Mapa de dependencias

> Derivado de los `using SP.*` de cada archivo. Un subsistema con muchos **entrantes** es caro de
> cambiar: tocarlo mueve a todos los que lo usan.

```mermaid
flowchart LR
    Actors --> Ai
    Actors --> Combat
    Actors --> Core
    Ai --> Actors
    Ai --> Combat
    Ai --> Core
    Ai --> Player
    Ai --> Presentation
    Ai --> UI
    Ai --> Vehicles
    Camera --> Actors
    Camera --> Presentation
    Combat --> Actors
    Combat --> Core
    Combat --> Presentation
    Combat --> Vehicles
    Core --> Actors
    Core --> Ai
    Core --> Camera
    Core --> Combat
    Core --> Mision
    Core --> Player
    Core --> Vehicles
    Demo --> Actors
    Demo --> Ai
    Demo --> Camera
    Demo --> Combat
    Demo --> Core
    Demo --> Player
    Demo --> Vehicles
    Editor --> Actors
    Editor --> Ai
    Editor --> Camera
    Editor --> Combat
    Editor --> Core
    Editor --> Interaction
    Editor --> Mision
    Editor --> Player
    Editor --> Presentation
    Editor --> Tutorial
    Editor --> UI
    Editor --> Vehicles
    Interaction --> Actors
    Interaction --> Combat
    Interaction --> Core
    Interaction --> Player
    Interaction --> Presentation
    Mision --> Actors
    Mision --> Ai
    Mision --> Combat
    Mision --> Core
    Mision --> Player
    Mision --> Presentation
    Mision --> Tutorial
    Mision --> UI
    Mision --> Vehicles
    Operacion --> Actors
    Operacion --> Ai
    Operacion --> Camera
    Operacion --> Combat
    Operacion --> Core
    Operacion --> Interaction
    Operacion --> Mision
    Operacion --> Player
    Operacion --> Presentation
    Operacion --> Tutorial
    Operacion --> UI
    Operacion --> Vehicles
    Player --> Actors
    Player --> Ai
    Player --> Camera
    Player --> Combat
    Player --> Core
    Player --> Interaction
    Player --> Presentation
    Player --> UI
    Player --> Vehicles
    Presentation --> Actors
    Presentation --> Ai
    Presentation --> Camera
    Presentation --> Combat
    Presentation --> Core
    Presentation --> Mision
    Presentation --> Player
    Presentation --> UI
    Presentation --> Vehicles
    Simulacion --> Actors
    Simulacion --> Combat
    Simulacion --> Core
    Tutorial --> Actors
    Tutorial --> Ai
    Tutorial --> Camera
    Tutorial --> Combat
    Tutorial --> Core
    Tutorial --> Player
    Tutorial --> Presentation
    Tutorial --> Vehicles
    UI --> Actors
    UI --> Ai
    UI --> Camera
    UI --> Combat
    UI --> Core
    UI --> Mision
    UI --> Player
    UI --> Presentation
    UI --> Tutorial
    UI --> Vehicles
    Vehicles --> Actors
    Vehicles --> Ai
    Vehicles --> Combat
    Vehicles --> Core
    Vehicles --> Presentation
```

## Cuanto pesa cambiar cada subsistema

| Subsistema | Depende de | Lo usan | Sus dependencias |
|---|---|---|---|
| Actors | 3 | 15 | Ai, Combat, Core |
| Ai | 7 | 11 | Actors, Combat, Core, Player, Presentation, UI, Vehicles |
| Camera | 2 | 8 | Actors, Presentation |
| Combat | 4 | 14 | Actors, Core, Presentation, Vehicles |
| Core | 7 | 14 | Actors, Ai, Camera, Combat, Mision, Player, Vehicles |
| Demo | 7 | 0 | Actors, Ai, Camera, Combat, Core, Player, Vehicles |
| Editor | 12 | 0 | Actors, Ai, Camera, Combat, Core, Interaction, Mision, Player, Presentation, Tutorial, UI, Vehicles |
| Interaction | 5 | 3 | Actors, Combat, Core, Player, Presentation |
| Mision | 9 | 5 | Actors, Ai, Combat, Core, Player, Presentation, Tutorial, UI, Vehicles |
| Player | 9 | 10 | Actors, Ai, Camera, Combat, Core, Interaction, Presentation, UI, Vehicles |
| Presentation | 9 | 11 | Actors, Ai, Camera, Combat, Core, Mision, Player, UI, Vehicles |
| Tutorial | 8 | 4 | Actors, Ai, Camera, Combat, Core, Player, Presentation, Vehicles |
| UI | 10 | 6 | Actors, Ai, Camera, Combat, Core, Mision, Player, Presentation, Tutorial, Vehicles |
| Vehicles | 5 | 11 | Actors, Ai, Combat, Core, Presentation |

## Como leerlo

- **`Core` con muchos entrantes** es lo esperado: es la base sin escena.
- **`Presentation` y `UI` no deberian tener salientes hacia `Player` o `Ai`**: son capas de salida.
  Si aparecen, es acoplamiento al reves y vale revisarlo.
- **`Editor` puede depender de todo**; nada puede depender de `Editor`.
