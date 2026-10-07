# Por que el nivel funciona como escenario RTS (objetivo 3, centro de datos)

![RTS centro de datos](rts_centro_de_datos.png)

- **Un solo punto a defender, varias entradas.** El centro de datos tiene una puerta de frente y las calles laterales: obliga a *repartir* la escuadra (uno en la computadora, los otros cubriendo accesos) en vez de amontonarse. En RTS (Tab) se ven las tres lineas de ataque a la vez.
- **El hackeo es un "timer" que la escuadra protege.** Mientras Kes/el rol indicado mantiene [E], las oleadas llegan por distintos lados; el jugador decide desde arriba a quien mandar a cubrir que puerta (Q contextual / radial / arrastre) y cuando curar con el medico.
- **Coberturas legibles desde arriba.** Los contenedores, muros bajos y pilares dan puntos de cobertura claros; con la IA nueva la escuadra solo se cubre cuando esta herida, recarga o la ven varios (ver `Cobertura/RESUMEN_5_iteraciones.md`), asi que ordenar una cobertura en RTS tiene sentido tactico y no es automatico.
- **Informacion para decidir.** El minimapa muestra los conos de vision enemigos (rojo), el HUD de escuadra muestra vida/rol y los puestos de control tienen cartel y barrera visibles: todo lo necesario para planear sin estar en el suelo.
- **Ritmo.** Infiltrar (sigilo/FPS) -> puestos (ordenes a roles) -> centro de datos (defensa en RTS) -> tanque (accion FPS/artillero) -> resistir (RTS+FPS) -> extraccion: se alterna el modo de control en cada objetivo.
