# Ronda 16 - bugs corregidos en Assets/_Project/Scripts/Operacion

Cambios minimos, sin compilar (pendiente de verificar en Unity). Sin commit. Los materiales de SafeMaterial son HideAndDontSave: Unity nunca los recolecta, hay que Destroy a mano.

| n | archivo:linea (aprox.) | bug | correccion |
|---|---|---|---|
| 1 | ArbustoRodante.cs:127 | `BuscarMallaYMaterial` creaba una malla nueva en cada `Armar` y fugaba la anterior | `if (malla == null) malla = CrearMallaBola();` |
| 2 | GuiaDeObjetivo.cs:134 | `MallaDeFlecha` creaba un Mesh nuevo en cada llamada (una por cada `GuiaDeMando.Awake`), nunca liberado | cache estatico `mallaFlecha` (HideAndDontSave) y reset en `Reiniciar` |
| 3 | GuiaDeMando.cs:84 | los dos materiales `CreateLinea` (flecha y ruta, HideAndDontSave) no se destruian | `OnDestroy` destruye ambos materiales |
| 4 | EmboscadaDeCohetes.cs:51 (CoheteDeAviso) | cada cohete creaba un material `SafeMaterial.Create` que nunca se liberaba (uno por disparo) | `OnDestroy` destruye el material del renderer |
| 5 | TorreDestruible.cs:217 | `SetParent(padreOcupante)` con un padre que puede haberse destruido junto con la torre (excepcion de objeto destruido) | `padreOcupante != null ? padreOcupante : null` |
| 6 | CazaDeEnemigos.cs:34 | estado estatico (cazadores, reloj, RumbosDados) sin reset de arranque | `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` que llama a `Reiniciar()` |
| 7 | ReparacionDeTanque.cs:221 y 228 | `FijarProgreso` y `Terminar` (via `CompletarYa`) usan `Tanque.PonerVida01` sin comprobar que el tanque siga vivo (NRE/MissingReference) | guarda `if (Tanque != null)` |
| 8 | OperacionTerminal.cs:212 y 249 | `yo.Health.IsAlive` sin comprobar `Health` null | `yo.Health != null &&` |
| 9 | HelicopteroEnemigo.cs:246 | `CrearSogas` hacia `lr.material = SafeMaterial.Create(...)` por soga (material fugado y clonado de nuevo por `.material`), nunca destruido | un material compartido `matSoga` en `sharedMaterial`, destruido en `QuitarSogas` |
| 10 | HelicopteroEnemigo.cs:293 | `BlancoDeMetralla`: `s.Health.IsAlive` sin null check | `s.Health == null ||` |
| 11 | EnemigoFrancotirador.cs:42 | `Laser.material = new Material(...)` crea dos copias y ninguna se destruye | `sharedMaterial` + `OnDestroy` que lo destruye |
| 12 | FrancotiradorEnTorre.cs:176 y 189 | `new Material` del punto de mira y del laser, nunca liberados | campos `matPunto`/`matLaser` destruidos en `OnDestroy` (override del base) |
| 13 | EnemigoEscudero.cs:93-118 | 4 materiales `new Material` por escudero (`Mat()`), nunca liberados | componente `LiberarMaterialesAlDestruirse` en la raiz del escudo (cubre escudo caido que se destruye a los 40 s) |
| 14 | OperacionHud.cs:377 | `Iluminado` crea Texture2D + Sprite de la vineta sin liberarlos al destruir el HUD | campo `vinetaTex` destruido en `OnDestroy` |
| 15 | ReflectorVigia.cs:111 y 118 | malla (HideAndDontSave) y material del haz creados por reflector y nunca destruidos; ademas `rotos` retenia referencias destruidas | `mallaHaz`/`matHaz` destruidos en `OnDestroy`; `rotos.Remove(this)` |
| 16 | HeliDeDescarga.cs:71 | un `new Material` por soga, nunca destruido | un material por heli (`matSoga`) destruido en `OnDestroy` |
| 17 | CineDeHuida.cs:67 | si se destruye sin `Cerrar()` y sin bloqueo, `Lento()` dejaba `timeScale`/`fixedDeltaTime` pegados en camara lenta | `OnDestroy` llama siempre a `TiempoNormal()` |
| 18 | DefensaDelCentro.cs:123 (TorretaDeDefensa) | 3 SafeMaterial por torreta (HideAndDontSave) nunca liberados | campo `materiales` destruido en `OnDestroy` |
| 19 | DefensaDelCentro.cs:198 | `s.Health.IsAlive` sin null check al elegir blanco | `s.Health == null ||` |
| 20 | CinematicaDeOperacion.cs:185 | textura (HideAndDontSave) y sprite del anillo de salto sin liberar en cada cinematica | `OnDestroy` destruye sprite y su textura |
| 21 | CinematicaDeRapel.cs:366 | `cuerda.material = matCuerda` clona una instancia por cuerda que `LimpiarEscena` no libera | `sharedMaterial` (el original ya se destruye en LimpiarEscena) |
| 22 | CinematicaDeRapel.cs:227 | idem 20: textura y sprite del anillo sin liberar | `OnDestroy` destruye sprite y textura |
| 23 | OperacionDirector.TerminalDeCeldas.cs:~180 | `MaterialDePantallaDeTerminal()` se llamaba dos veces (pantalla y led): un material extra fugado por terminal | se crea una vez y se comparte |

Revisados y descartados (no son bug o son de diseno): EmboscadaDeCohetes.Update (codigo muerto por `CohetesActivos = false`), CargaExplosiva/OperacionTerminal/OperacionHud/UiDeInteraccion (cambios de hoy preservados; solo los null-checks del punto 8).
