# Ronda 14 — reportes del 17/11 (31)

Atlas ANTES vs DESPUES: `ATLAS_R14_1_8.jpg`, `_9_16`, `_17_24`, `_25_31` (capturas en `antes/` y `despues/`, poses en `CapturasDeBuild.Reportes17.cs`).

## Aplicado (en codigo o escena)
| # | Cambio |
|---|---|
| 4 | Arbusto rodante: malla de bola low-poly propia (ya no se rompe con el viento) |
| 5 | Aliado que repara el tanque queda anclado (ReparacionDeTanque.BloquearAlReparador) |
| 6, 22 | Mira de torreta: un solo circulo que cambia de tamano |
| 7 | Zoom alejado del tanque a la mitad (SetZoomFactor 1.75) |
| 8 | Minimapa al frente del periscopio (SetAsLastSibling) |
| 9 | CameraRig.ReiniciarZoom antes de cada cinematica |
| 10 | Slow-motion residual al cerrar el reporte (EscalaAlCerrar) |
| 11 | Botones del menu de pausa sin hueco cuando CARGAR esta oculto |
| 12 | Particulas y chispas cuando el escudero dispara |
| 14, 15, 17, 27, 29, 31 | Escena: casitas x5, cabinas de calle, decoracion de entrada, columna (320,266), 84 Luz_Hada apagadas |
| 16 | Dano maximo 30 |
| 19 | 314 trozos de flecha pintada quitados de las mallas amarillas |
| 23 | Mensaje "DEBE SER EL ASALTO" al apretar E junto a la carga sin ser Asalto |
| 26 | El recorrido inicial de pasos ya no arranca solo (RecorridoInicialActivo = false); va directo al rapel |
| 28 | Apagadas piezas flotantes del Edificio_2 (aire/antena) y las 2 paredes finas |
| 30 | Scroll de zoom RTS x2 (160) |
| 2 | Fuera de la ruta >16 m durante 10 s seguidos en la huida: pierde y reintenta |

## Agregado en la segunda pasada
| # | Cambio |
|---|---|
| 13, 24 | 184 piezas con hueco (erizo, poste, muro caido, auto quemado, cartel, farol, concreto) pasan de BoxCollider a MeshCollider no convexo. Test en editor (EdicionR14.TestBalasPorHuecos): 364 de 368 dejan pasar un rayo por algun hueco; 320 frenan el rayo por el centro |
| 20 | Muro Valle_Sur oculto (su collider sigue de limite) y 82 arboles/arbustos en doble fila |
| 28 | En el dialogo de reporte, [U] oculta el dialogo y deja marcar geometrias con clic (clic derecho quita, U vuelve); viajan en la nota como [GEOMETRIAS MARCADAS: ...] |

## Decisiones de la tercera pasada
| # | Decision |
|---|---|
| 1 | La T ya existia; el tramo norte (Acceso_Patio, veredas, cordones, isleta, 2 arbustos: 9 objetos) la volvia cruz. Se apago: queda en T. El tanque sigue pasando por terreno (el NavMesh no cambio) |
| 3 | La ruta de la calle queda cubierta por la regla de #2 (mas de 10 s fuera de la calle pierde y reintenta) |
| 18 | Base = la plaza de Resistir: aro vibrante (AnilloDeSubida) que sigue en la ciudad; lejos (<=30 m) E avisa "ACERCATE A LA BASE"; a <=5 m avisa "APRETA E PARA REAGRUPAR" y E confirma "BASE ASEGURADA". El cursor cambia solo si el sistema de mira lo da: NO se agrego |
| 21 | Sin cambios: reubicar el cuartel es subjetivo y no hay un criterio escrito |
| 25 | Sin accion concreta («mira como se ve»): solo documentado en el atlas |

## Verificacion
Suite headless OK tras los cambios (13:00). Build Windows64 (13:06) y capturas DESPUES regeneradas. Probado jugando: nada. Nada commiteado.
