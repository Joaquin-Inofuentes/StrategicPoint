# Backlog UX — 20 iteraciones

Encontrado jugando como usuario promedio. Estado: [ ] pendiente · [x] hecho

## IT1 — Vida propia (hueco crítico de FPS: no sabés cuánta vida tenés)
- [x] 1. HUD de vida propia (barra + número) del soldado poseído
- [x] 2. Color según nivel: verde / amarillo / rojo
- [x] 3. Pulso cuando la vida es crítica (<25%)
- [x] 4. Se oculta en RTS igual que la mirilla (consistencia de modo)

## IT2 — Estado de misión (hueco crítico de RTS: no sabés cuánto falta)
- [x] 5. Contador de enemigos vivos restantes
- [x] 6. Contador de escuadra viva
- [x] 7. Visible en AMBOS modos (es info estratégica, no de puntería)
- [x] 8. Se actualiza por EntityDiedEvent, no por polling

## IT3 — Roster utilizable
- [x] 9. Barra de vida por fila
- [x] 10. Fila en gris cuando el soldado murió
- [x] 11. Marca "►" clara en el soldado poseído
- [x] 12. Mostrar el arma de cada soldado

## IT4 — Munición
- [x] 13. Aviso "SIN MUNICION" al llegar a 0
- [x] 14. Contador en rojo con poca munición (<30%)
- [x] 15. Barra de recarga más legible
- [x] 16. Tecla [R] de recarga manual

## IT5 — Daño recibido
- [x] 17. Indicador de dirección del daño
- [x] 18. Vignette más fuerte con poca vida
- [x] 19. Número de daño flotante sobre el objetivo
- [x] 20. Sonido/flash distinto al recibir vs. dar daño

## IT6 — Minimapa
- [x] 21. Marco/leyenda de colores del minimapa
- [x] 22. Flecha de orientación del jugador
- [x] 23. Íconos de enemigos distinguibles
- [x] 24. Ocultar enemigos no vistos (niebla simple)

## IT7 — Órdenes RTS legibles
- [x] 25. Marcador de destino visible al ordenar mover
- [x] 26. Línea del soldado a su destino
- [x] 27. Confirmación en texto de la orden dada
- [x] 28. Cancelar órdenes con [Esc]/click derecho

## IT8 — Selección RTS
- [x] 29. Contador de seleccionados más visible
- [x] 30. [Ctrl+A] seleccionar toda la escuadra
- [x] 31. Doble click selecciona todos los del mismo rol
- [x] 32. Anillo de selección con color por estado de vida

## IT9 — Transición FPS↔RTS
- [x] 33. Aviso breve del modo al cambiar ("VISTA RTS")
- [x] 34. Lerp de cámara al cambiar de modo (no corte seco)
- [x] 35. Recordar posición de cámara RTS al volver
- [x] 36. Instrucciones distintas y claras por modo

## IT10 — Vehículo
- [x] 37. Aviso de asiento ocupado al intentar entrar
- [x] 38. Barra de vida del vehículo siempre visible al ir adentro
- [x] 39. Indicador de velocidad más legible
- [x] 40. Aviso al destruirse el vehículo

## IT11 — Pantallas de fin
- [x] 41. Estadísticas en victoria/derrota (bajas, tiempo)
- [x] 42. Distinguir mejor victoria de derrota visualmente
- [x] 43. Foco por defecto en botón Reintentar
- [x] 44. Navegación por teclado en los botones

## IT12 — Pausa y configuración
- [x] 45. Mostrar controles en el menú de pausa
- [x] 46. Persistir configuración entre partidas
- [x] 47. Botón de volver al menú desde pausa
- [x] 48. Confirmar antes de salir al menú

## IT13 — Onboarding
- [x] 49. Cartel inicial con el objetivo de la misión
- [x] 50. Recordatorio de [TAB] las primeras veces
- [x] 51. Lista de controles con [H]
- [x] 52. Resaltar la primera acción sugerida

## IT14 — Legibilidad del HUD
- [x] 53. Contorno/sombra en textos sobre fondo claro
- [x] 54. Tamaños consistentes entre paneles
- [x] 55. Evitar solapamiento de paneles
- [x] 56. Márgenes consistentes

## IT15 — Feedback de disparo
- [x] 57. Retroceso visual del viewmodel al disparar
- [x] 58. Fogonazo en la punta del arma
- [x] 59. Marcador de impacto en el mundo
- [x] 60. Confirmación de baja distinta a la de impacto

## IT16 — Estado de la escuadra
- [x] 61. Aviso cuando un aliado está bajo ataque
- [x] 62. Aviso cuando un aliado tiene poca vida
- [x] 63. Indicador fuera de pantalla de aliados
- [x] 64. Resaltar al aliado más cercano poseíble

## IT17 — Enemigos
- [x] 65. Barra de vida del enemigo al apuntarle
- [x] 66. Indicador de enemigo fuera de pantalla que te dispara
- [x] 67. Distinguir enemigo alerta vs. desprevenido
- [x] 68. Marcar al enemigo objetivo del arma

## IT18 — Cámara
- [x] 69. Límites de paneo en RTS (no perderse en el vacío)
- [x] 70. Botón/tecla para recentrar en la escuadra
- [x] 71. Zoom con límites claros
- [x] 72. Suavizado del paneo

## IT19 — Accesibilidad
- [x] 73. Sensibilidad separada FPS / RTS
- [x] 74. Opción de tamaño de HUD
- [x] 75. Opción de invertir eje Y
- [x] 76. Mirilla configurable

## IT20 — Pulido final
- [x] 77. Revisión de todos los textos en español consistente
- [x] 78. Sin estados de UI colgados en ningún flujo
- [x] 79. Regresión completa
- [x] 80. Suite automatizada en verde

## Bugs de fondo encontrados durante IT1-IT2 (no estaban en la lista)
- [x] B1. Victoria saltaba con 3 enemigos vivos: solo miraba la sublista de patrulla (4) y no los 7 del mapa
- [x] B2. Soldado inactivo al cargar la escena nunca se registraba (Awake no corre en inactivos): invisible para IA, victoria y contadores
- [x] B3. Soldado montado en el vehiculo desaparecia del conteo de escuadra
- [x] B4. El roster nunca se actualizaba en Play: la lista `rows` no sobrevive al domain reload (ni el resaltado de posesion que ya existia funcionaba)
- [x] B5. El soldado poseido al arrancar no salia marcado: PossessionChangedEvent solo se publica al CAMBIAR, no en la posesion inicial
