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
