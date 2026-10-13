# 5 escenas de blockout (12/10/2026)

Generadas por codigo: `Strategic Point > Nivel > Generar 5 escenas de blockout`
(`Assets/_Project/Scripts/Editor/BlockoutScenesBuilder.cs`, idempotente). Solo cubos de color plano, rampas y plataformas; base
de camara/HUD/input/escuadra de 3 + blindado tomada del mundo de demo de la suite. NavMesh horneado (colliders como fuente).
Colores: azul = zona aliada, rojo = zona enemiga, amarillo = objetivo, verde = curacion, naranja = muro debil (se rompe).
No estan en Build Settings (el proyecto tampoco lista SC_Operacion ni SC_Simulacion).

| Escena | Objetivo | Especialidades | Gancho |
|---|---|---|---|
| SC_Blockout_01 Fortin del cuello de botella | Tomar la RADIO al fondo del fortin | Asalto (rompe las 2 brechas), Medico (2 fosos de curacion), Francotirador (vs torre enemiga) | Tres formas de entrar: puerta custodiada, brechas debiles o pasos laterales angostos |
| SC_Blockout_02 Valle del francotirador | Llegar al DEPOSITO al norte del valle | Francotirador (nido aliado en altura), torreta fija a mitad de valle, Flankers enemigos por corredores | Duelo de alturas: 2 nidos enemigos con rampa trasera |
| SC_Blockout_03 Convoy blindado | Destruir el tanque enemigo y llegar al deposito | Vehiculo aliado, torreta fija cubriendo la brecha, 2 vigias en torres | Muro debil que solo rompe el blindado y puente obligado de una sola losa |
| SC_Blockout_04 Rescate en la aldea | Llegar al REHEN (plaza central) y volver a la zona azul | Civil/Rehen, Medico, 3 Flankers en la periferia, vigia en azotea | Ir y volver cruzando una aldea de 8 casas con calles de 8 m |
| SC_Blockout_05 Asedio de la colina | Resistir y eliminar a los atacantes desde la cima | 2 torretas fijas, zona de curacion en la cima, blindado al pie | Defensa en altura (3 terrazas con rampas) con 3 frentes + francotirador norte |

## Verificacion (Play, cada escena)
0 soldados fuera del NavMesh, 2 camaras, 0 errores de consola, 45-79 fps con el editor sin foco.
Capturas: `bNN_1_arranque.png` (vista del jugador) y `bNN_2_vista_aerea.png` (camara temporal, destruida al terminar).
Suite headless: OK.

## Pasada de mejora y prueba de jugabilidad (2da vuelta)
Hallazgos medidos en Play y corregidos:
| Hallazgo | Medicion | Arreglo |
|---|---|---|
| Plataformas y nidos inalcanzables en las 5 escenas | `NavMesh.CalculatePath` desde el spawn daba PathPartial a toda plataforma elevada | Las rampas terminaban 2 m dentro de la plataforma (escalon de 0,6 m con `agentClimb` 0,3): ahora acaban en su borde |
| Nidos enemigos de la escena 2 aislados | parapeto de 6 m cruzaba justo donde la rampa llega a la losa | parapeto movido al lado opuesto de la rampa |
| Escena 3 sin paso para la infanteria | la barricada cerraba todo el ancho (la brecha es no caminable) | paso de 6 m a x -26..-20, el blindado no cabe |
| Enemigo del deposito dentro de un muro (escena 3) | `SamplePosition` sin malla | movido 5 m |
| El "objetivo" era solo un letrero | solo se ganaba matando a todos | nuevo `BlockoutObjetivo` (llegar a zona / rehen vivo en zona azul / resistir) |
| Escena 5 demasiado facil | 4 de 11 enemigos caian en 13 s | segunda oleada de 6 con rutas que suben a la colina (17 enemigos) |

Resultado: las 5 escenas con 0 tramos inalcanzables (aliados, plataformas, objetivo y todos los enemigos), 0 errores de consola, 78-98 fps.
Objetivos probados por simulacion (`scripts_de_prueba/warp.cs`): 1 y 2 ganan al entrar en la zona; 3 no gana hasta destruir el tanque y gana despues;
4 gana con el rehen vivo en la zona azul y pierde si el rehen muere.
Combate simulado con un bot (`bot.cs` + `runbot.sh`: posee a Kes, manda a Vega y Doc por el NavMesh): en la 1 el avance a ciegas cuesta 2 de 3 soldados por 3 bajas;
en la 5 el asedio cuesta 2 aliados con 8 de 17 enemigos caidos en 90 s. El bot no usa coberturas, asi que es una cota pesimista.
