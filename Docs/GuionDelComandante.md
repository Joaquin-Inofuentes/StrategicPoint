# Guion del Comandante — cinemática inicial de la Operación Cuartel

**Duración total: ~63 s** (antes ~82 s): sobrevuelo 49,4 s + 1 s de salida + rapel 12,5 s.
El sobrevuelo ya no recorre el trayecto de la autopista: va a los puntos clave y salta con **corte a negro** (0,35 s a cada lado) al patio del blindado, a la ciudad y al helipuerto.

Cada línea es un clip aparte. Se guarda en `Assets/_Project/Resources/Audio/Cine/<id>.mp3` (también sirve .wav u .ogg) y el juego la dispara sola en su momento. Si falta un clip, esa línea se salta sin error.

**Voz: Marcus** (referencia: https://d8j0ntlcm91z4.cloudfront.net/user_3J6IS1RdMGCj1eHz3sV6UPDuYgE/hf_20261021_163506_f9dfa164-a5cf-4cac-bbc8-87cafd0f6c6f.mp3).
Generada con Higgsfield CLI: `text2speech_v2 --variant elevenlabs`, voz preset Marcus (id `6f98d3dd-324f-4845-8c28-c1d1647a06cd`), ~0,38 créditos por línea. Duraciones medidas con `AudioClip.length`. La voz habla lento (~11 caracteres/s): si una línea se pasa de su hueco, recortar el texto y regenerarla.

| # | Archivo (id) | Entra en (s) | Ventana máx. | Texto |
|---|---|---|---|---|
| 1 | `cmd_01_inicio` | 0,0 | 5,9 s (hueco 6,8) | Escuadra, habla el comandante. Esta noche entramos al cuartel enemigo. |
| 2 | `cmd_02_torretas` | 6,8 | 9,0 s (hueco 10,0) | Primero, las dos torretas de artillería. Si siguen en pie no llegamos al centro. Cargas explosivas, y rápido. |
| 3 | `cmd_03_cuartel` | 16,8 | 4,2 s (hueco 5,7) | Después, limpien el cuartel. No dejen a nadie con vida. |
| 4 | `cmd_04_centro` | 22,5 | 6,9 s (hueco 8,1) | Luego, dos puestos y el centro de datos. Asalto coloca las cargas, los demás cubren. |
| 5 | `cmd_05_blindado` | 30,6 | 5,7 s (hueco 6,3) | Un tanque jefe custodia el patio. Derríbenlo y escapen en él. |
| 6 | `cmd_06_ciudad` | 36,9 | 6,1 s (hueco 6,7) | En la ciudad, tomen la radio y defiendan. Dos posiciones alcanzan. |
| 7 | `cmd_07_helipuerto` | 43,6 | 5,5 s (hueco 5,8) | Al final, el helicóptero. Suban todos. Comandante fuera. |
| 8 | `cmd_08_rapel_a` | 51,0 (0,6 s del rapel) | 4,5 s (hueco 5,7) | Zona de descenso a la vista. Prepárense para el rapel. |
| 9 | `cmd_09_rapel_b` | 56,7 (6,3 s del rapel) | 5,3 s (hasta el final, 6,2) | ¡Abajo, abajo! Suerte, escuadra. Los estoy mirando. |

Los tiempos 1–7 salen del propio recorrido (la voz arranca cuando la cámara llega al cuadro clave), así que se mantienen pegados al video aunque se cambie la velocidad de la cámara. Las líneas 8–9 van a segundo fijo dentro del rapel (`CinematicaDeRapel.SegundoVozPreparar/Bajar`).

## Qué muestra la pantalla en cada línea
1. Aproximación al cuartel, cartel **OPERACIÓN CUARTEL**.
2. Sobre la torreta de artillería A, cartel **PRIMERO: DETONÁ LAS 2 TORRETAS**; sigue sobre la B.
3. Cartel **DESPUÉS ELIMINÁ A TODO EL CUARTEL**.
4. Muralla y centro de datos, cartel **VOLÁ LOS 2 PUESTOS · DESCARGÁ LOS DATOS**.
5. *(corte)* Patio del blindado, cartel **ESCAPÁ EN EL BLINDADO**.
6. *(corte)* Ciudad, cartel **DEFENDÉ LA CIUDAD DESDE LA RADIO: 2 POSICIONES**.
7. *(corte)* Helipuerto, cartel **Y EXTRAÉ EN EL HELICÓPTERO**.
8–9. Rapel.

## Validación
`Bug289a` (suite de checks) comprueba la duración, los 3 cortes, las 7 líneas y que cada línea tenga hueco suficiente. Con los clips ya puestos, `VozDeCinematica.Validar()` avisa de cualquier clip que dure más que su ventana.
