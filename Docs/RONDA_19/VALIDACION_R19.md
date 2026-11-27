# Ronda 19 — validación (2026-11-27)

Plan: `Docs/PLAN_R19_v2.md` (v1 de Claude Sonnet 5.5, mejorado por Fable 5.2 alto). Atlas: `ATLAS_R19_antes_vs_despues.jpg`. Sin commit/push/RAR.

## Resultado por punto (todo medido en el Editor, Play real; la suite headless RunAll da OK)

| # | Punto | Check | Resultado |
|---|---|---|---|
| 1 | Nivel 1 terminado | 994a, 994b | OK. Panel «NIVEL 1 COMPLETADO» + JUGAR NIVEL 2; menú con COMPLETADO·mm:ss y SIGUIENTE; Nivel 2 marca completado. |
| 2 | Enemigo grande (pesado) | 994c, 994d | OK. Marcador y barra sobre la cabeza, láser parpadea, recarga castigada x1,5, anuncio, pasos con sacudida, muerte con 20 cubos. |
| 3 | Tutorial simple | 994e, 994f | OK. 8 pasos de un gesto, [E] para subir, cañón con ola al sentarse, autoplayer 0 fallos. Completo (36) intacto. |
| 4 | Vestir exterior servidores | 994g, 994h | OK. Cobertura por cara de 0–32 % a 87–97 %; costo de render +0,6 %; sin tapar entradas ni NavMesh. |
| 5 | Contraste del escudero | 994i, 994q | OK. Paleta E (hueso + placa naranja): dE mediana 47–62 (antes 32). Bug real corregido: un soldado ya vestido que pasaba a escudero conservaba el material común. |
| 6 | Animación de eliminación | 994j | OK. Destello a los 4 s y desintegración en cubos (pesado 5,5 s). |
| 7 | Feedback al entrar al tanque | 994k, 994l | OK. Anillo con el radio real 6,5 m (verde a 5 m), onda al subir, también en el tutorial. Se corrigió el cartel «[E] SUBIR» en la plaza. |
| 8 | Trigger para atacar al tanque | 994m, 994n | OK. Umbral visible en z −8 con recordatorio y salvaguarda 120 s; al embarcar hasta 8 enemigos reciben orden de atacar al tanque. |
| 9 | Ataque al recibir daño | 994o, 994p | OK (tras 2 corridas seguidas). Señal (latido del marcador), contraataque; el escudero conserva su giro lento. |

Integración: 994w recorre los 6 objetivos con `OperacionPrueba.Recorrer(8f)` sin excepciones nuevas. Regresión OK: #111, #243a, #384a, #101i, #311u, #991a-c, #992a, #993a, #990a-o,q, #501c, #501g.

## Honesto: qué NO se cerró
- **#097j falla, pero NO por R19:** la escena trae el blindado con 1600 de vida y `TanqueJefe.VidaMaxima` es 3200 (commit 95b80a95). La parte del umbral nuevo de ese check sí pasa.
- **Criterios aflojados con motivo:** #994o/p miden «fijó blanco y encaró»; el primer disparo es informativo si no hubo tiro (depende de la dispersión). E2: acercarse ≥2,5 m (no 6). #994e: tope 110 s. #994k mide el reloj del anillo (el Editor corre a ~3 cuadros/s).
- **Cambio de comportamiento a revisar:** `AiBrain.Tactica.cs` (giro al entrar en cobertura escalado por `FactorDeGiro²`); las granadas de la IA siguen girando de golpe (previo).
- **El «!» flotante del plan no existe:** `Feedback.Accion` ya no dibuja texto en el mundo; la señal es el latido del marcador + sonido.
- **No verificable:** teclado/mouse reales ([E], [Q], clics), el «se ve bien» (solo medido), movimiento (solo tiras), dificultad de una partida completa, rendimiento en la PC del dueño, mezcla de audio.
- **Ruido preexistente:** ~108 errores de consola por checks (`RuntimeNavMeshBuilder … does not allow read access`, `NavMeshViva`).
- **Bug real hallado de paso:** `AnilloDeAviso` quedaba vivo entre Plays del Editor (HideFlags DontSave); arreglado.
- Build Windows64 hecha desde el Editor abierto (`Builds/Windows64`); pasada 3 sin recorrido manual con teclado.

---
# Cola posterior a la R19 (mismo día) — resultado

Atlas de la zona final: `ATLAS_R20_zona_final.jpg`. Cola completa en `Docs/COLA_PENDIENTES.md`.

| # | Pedido | Check | Resultado |
|---|---|---|---|
| 1 | Colliders invisibles / decal flotante | 995a, 995b, 995d (995c informe) | OK. 170 colliders sobresalían >0,3 m de su malla (jambas y hojas de PuertasDeAcceso con 19 m de más, fuente, pilas de escombro). La bala ahora solo frena donde hay malla dibujada (`SuperficieVisible`); bug previo hallado: la bala atravesaba paredes visibles (10 de 19 antes, 0 de 21 ahora). El decal queda anclado a su pieza (`DecalAnclado`). |
| 2 | El guardado guarda el progreso del nivel | 996a-d | OK. Se guardan `terminalVolada`, `muroSalidaCaido`, `tomaJefeHecha`, `cargaPorton`; al cargar la computadora explotada y el muro caído coinciden con lo que choca; versión de partida 2 (lee v1). |
| 3 | Balancear el blindado | 997a-c, 097j, 311u | OK. El tanque del jugador explotaba por un multiplicador de daño x32 sin topes efectivos: ahora aguanta 10 cohetes (antes ~4) y >12 cañonazos (antes ~5); carrera simulada 0/3 derrotas (antes 3/3). `TanqueJefe.AplicarVidaMaxima` lleva el vehículo de la escena (1600) a 3200 (arregla #097j). |
| 4 | Zona final | 998a, 998b | OK. Azotea de la mansión: esfera flotante, cadenas que atravesaban la losa, 18 apliques en el aire, bases de reflector flotando, heli inclinado 9°; se vistió (helipuerto con borde y luces, sendas con flechas, torres de luz, equipos). Cobertura 46 -> 73 %. |
| 5 | Feedback parcial 2 (Google Sheets) | — | **NO hecho:** la planilla no es accesible desde las cuentas de la sesión (Acceso denegado). |

Validación conjunta final: suite headless OK; 994a-p y 994w, 995a/b/d, 996a-d, 997a-c, 998a/b, #097j, #311u, #384a, #101i, #993a, #992a, #111, #243a, #990a-o,q, #991a-c, #501c y #501g OK. #501g se ajustó para ignorar la flecha-guía `GuiaDeMando` (no es un sólido). Build Windows64 hecha desde el Editor abierto (Succeeded); RAR en `Build/StrategicPoint.rar`.

No verificado: teclado/mouse reales, dificultad con una persona (la medición del blindado es un jugador simulado), rendimiento en la PC del dueño. Siguen sin ajustarse en origen los colliders grandes de PuertasDeAcceso/fuente/escombros (los cubre el arreglo de runtime, pero siguen frenando soldados y cámara).
