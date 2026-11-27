# Plan R19 — v1 (borrador de Claude Sonnet 5.5, a mejorar por Fable 5.2 alto)

Pedido del usuario (9 puntos): Nivel 1 terminado · Iterar en enemigo grande · Tutorial simple · Vestir exterior servidores · Contraste enemigo escudo · Animación de eliminación de enemigos · Feedback entrar al tanque (círculos en el piso) · Trigger para empezar a atacar al tanque · Ataque de enemigos cuando reciben daño.

Entrega final: todo implementado, build, atlas JPG antes/después, validado 3 veces con mejoras entre pasadas. Commit/push SOLO si el usuario lo ordena.

## Hechos del código (verificados por exploración)
- Menú: Nivel 1 = TUTORIAL (SC_Tutorial, modo simple ya por defecto con 11 de 37 pasos), Nivel 2 = Operación Cuartel (SC_Operacion). Niveles 3-4 "EN DESARROLLO" (MainMenuController.cs:157 NivelDisponible).
- Fin de operación: OperacionDirector.Extraer.cs:403 Ganar() -> GameOutcomeController.ShowVictory -> PantallaDeResultado (EstadisticasDeMision.Filas). Botones Reintentar/Salir; NO hay "Siguiente nivel", ni marca de nivel completado persistente.
- Fin de tutorial: TutorialManager paso "victoria" -> VictoriaTutorial (confeti, panel VOLVER AL MENU / Reintentar). Progreso solo en PlayerPrefs sp_tutorial_paso[_simple].
- Enemigo grande: EnemigoPesado.cs (x2 tamaño, vida x12, minigun, máscara, furia) y TanqueJefe.cs (Goliat, jefe obj. 4).
- Escudero: EnemigoEscudero.cs (escudo procedural acero claro + marco rojo), cuerpo SoldierLook.MaterialEscudero (grises claros) -> poco contraste en desierto/hormigón.
- Muerte: Health.AplicarRestaFinal -> EntityDiedEvent -> CubeFxReactor.OnDeath (6 clips de muerte, chispas); RetiroDeEnemigosCaidos hunde el cadáver a los 14 s. No hay desvanecido/disolución.
- Tanque: AnilloDeSubida.Mostrar(Transform, radio) ya existe (3 aros, cartel [E]) pero solo se muestra en HUIR/Abordaje y Resistir; el tutorial (paso entrar_tanque) solo usa TutorialBeacon.
- Ataque al tanque: AiBrain.Vehiculos.BuscarVehiculoAmenaza depende solo de visión; no hay zona/trigger.
- Daño: ReaccionAlDano (R17) ya hace que enemigo herido por el Player cace al atacante y avise al grupo (solo en Operación; sin giro/anim visible; sin daño de no-Player).
- Checks: ChecksBugs065.*.cs, método Bug0NNx sincrónico (string) o IEnumerator con Fin(); CorrerLista("BugNNNx,...", true) con el Editor en Play. Capas de arte del Editor idempotentes sobre escena abierta.

## Tareas

### T1 — Nivel 1 terminado (Tutorial)
Interpretación: "Nivel 1" del menú = Tutorial. Hacer que se sienta cerrado.
- T1a: Persistir completado: PlayerPrefs `sp_nivel1_completo` (+ fecha/tiempo) al llegar a "victoria" del tutorial; análogo `sp_nivel2_completo` en Ganar() de la Operación.
- T1b: VictoriaTutorial: título "NIVEL 1 COMPLETADO", resumen (tiempo, pasos), botón primario "SIGUIENTE NIVEL" -> SC_Operacion, secundarios Menú / Reintentar.
- T1c: Menú: marca ✓ en la tarjeta del Nivel 1 completado; el Nivel 2 se resalta como siguiente.
- T1d: Operación (Nivel 2): ShowVictory agrega "NIVEL 2 COMPLETADO" y botón Menú; sin "siguiente" (nivel 3 en desarrollo).
- Check: Bug_N1 verifica flags, textos y que el botón existe; el Total del tutorial >= 11 se mantiene.

### T2 — Iterar enemigo grande (Pesado; Goliat solo lectura salvo que sobre tiempo)
- Telegrafía de fases legible: aro/aura al estacionar, brillo de la minigun al entrar en frenesí, humo al recargar.
- Feedback de daño: destello de impacto en el cuerpo, barra de vida flotante opcional (solo si ya se golpeó), pasos con polvo.
- Muerte propia: caída lenta con sacudida y piezas de máscara/arma que caen; sonido grave.
- Check: fases siguen Patrulla->Estacionando->Frenesi; furia al romper máscara; muerte dispara el efecto.

### T3 — Tutorial simple
- Reducir a lo esencial y fusionar sub-pasos: de 11 a ~8 pasos legibles (mover, apuntar/disparar, cambiar de soldado, mando RTS, entrar al tanque, disparar del tanque, victoria), cada uno con UNA instrucción y un check visible.
- Mantener compatibilidad: `IdsSimples`, Total>=mínimo ajustado en los checks existentes (Fases13a17.cs:327), cadenas "f8Key","f7Key","RegruparAliados" en TutorialManager.cs, claves de progreso.
- Texto más corto, ayuda a los 10 s en vez de 16 s.

### T4 — Vestir exterior de servidores (Centro de datos)
- Nueva capa Editor idempotente `OperacionBuilder.FachadaCentro.cs` (hijo `ArteOp_Fachada` en 3_CentroDeDatos): zócalo, pilastras, cornisa, ventanas ciegas con luz fría, conductos y tuberías, tableros eléctricos, aires acondicionados, generadores, vallas bajas, rótulo iluminado, luminarias de pared, cables.
- Reusar prefabs ArteMundo y el helper Mat/Cubo/Decor del builder; sin colisión salvo muros ya existentes; no tapar puertas ni rutas de la IA (verificar NavMesh).
- Check: existe el grupo, cuenta de piezas >= N, ninguna dentro de un corredor de IA, sin colliders nuevos bloqueando entradas.

### T5 — Contraste del escudero
- Cuerpo: paleta propia más oscura y saturada (azul-negro con franjas amarillas) y emisión de borde para separarlo del desierto/hormigón y de otros enemigos.
- Escudo: acero más oscuro, marco rojo más emisivo, franja amarilla de aviso, ventana de impacto naranja intensa.
- Check: luminancia/contraste medido contra fondo (captura) >= umbral y distinto del enemigo común.

### T6 — Animación de eliminación de enemigos
- Disolución al final de la vida del cadáver (shader de umbral o escala+alfa) reemplazando el hundimiento seco en RetiroDeEnemigosCaidos; destellos de partículas en la disolución.
- Muerte: sacudida de impacto + caída más legible; refuerzo por headshot (chorro/chispa) y por explosión (ragdoll ya existe).
- Respetar tope de 24 cadáveres y rendimiento.
- Check: tras morir y pasar el tiempo, el cadáver pasa por estado "disolviendo" y se apaga sin errores.

### T7 — Feedback al entrar al tanque (círculos en el piso)
- Mostrar AnilloDeSubida en el tutorial (paso entrar_tanque) y siempre que el jugador esté a <= N m de un vehículo aliado subible en cualquier fase (Operación y tutorial), con el radio exacto de interacción (3,5 m), cambio de color al entrar y destello al subir.
- Círculo extra en cada asiento secundario si el vehículo tiene varios.
- Check: el anillo aparece dentro del radio de aviso, cambia a verde dentro del radio de uso, desaparece al subir.

### T8 — Trigger para empezar a atacar al tanque
- Nueva zona (BoxCollider trigger/anillo) en el patio del tanque: cuando el tanque aliado con jugador/ocupantes entra, o el jugador sube, los enemigos del sector pasan a objetivo "tanque" (IssueAttackVehicleOrder + CazaDeEnemigos.Marcar) con retraso escalonado.
- Visualizar la zona solo en depuración; log en SesionLog.
- Check: antes del trigger los enemigos no lo atacan; después sí (órdenes asignadas).

### T9 — Ataque de enemigos al recibir daño
- Extender ReaccionAlDano: giro inmediato hacia el atacante (animación de sobresalto breve), contraataque, también cuando el daño viene de aliados/tanque/explosión (atacante conocido), y en el tutorial/SC_Gameplay si hay enemigos.
- Fuego de supresión corto al localizar al atacante; límites de reacción/segundo se mantienen.
- Check: herido por aliado -> el enemigo fija objetivo en <1,2 s y gira hacia él.

## Verificación
- Checks nuevos Bug 994a..: uno por tarea (T1..T9) + suite existente (RunAll "OK") + checks previos relacionados (#384a, #101i, #993a, ReaccionAlDano R17).
- Capturas antes/después por tarea (ArnésCapturas -capturas / R18bFoto) y atlas JPG con etiquetas ANTES / DESPUÉS.
- Tres pasadas de validación; cada pasada corrige lo hallado y busca mejoras extra; informe honesto de lo no verificable (teclado real, etc.).
- Build CLI con el Editor cerrado, RAR de la carpeta Builds/Windows64.
