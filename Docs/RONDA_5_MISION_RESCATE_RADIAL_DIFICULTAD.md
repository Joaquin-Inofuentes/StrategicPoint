# Ronda 5 — Misión de rescate, radial concéntrico, dificultad, demolición y zoom RTS al cursor

Documento de referencia para las novedades y cambios de la **Ronda 5**. Todo se probó en Play Mode con validación visual completa guardada en `Assets/Validacion/Ronda5/` (66 capturas). El proyecto compila con 0 errores.

---

## 1. Sistema de Dificultad y Banco de Balance

- **Selector de dificultad en Menú Principal (`MainMenuController.cs`):** al presionar **JUGAR**, se despliega un panel estilizado con tres tarjetas seleccionables:
  - **FÁCIL:** Enemigos con vida al 75% y daño al 65%. Aliados con vida +35% y daño +25%. Jugador con vida +50% y daño +30%. Oleadas un 25% más chicas.
  - **MEDIO:** Configuración estándar balanceada (pequeña ventaja para la escuadra).
  - **DIFÍCIL:** Enemigos con daño y vida al 105%. Sin bonificaciones para jugador ni aliados. Oleadas un 40% más grandes.
- **Modificadores en tiempo real (`Core/Dificultad.cs`, `Combat/Health.cs`):**
  - Ajuste de vida máxima de soldados al iniciar la partida.
  - Multiplicador de daño calculado según emisor y receptor en `Health.TakeDamage`.
  - Persistencia mediante `PlayerPrefs ("sp_dificultad")`.
  - Solo afecta la partida principal (`SC_Gameplay`); tutorial y suites de test permanecen en balance neutro 1x.
- **Banco de balance (`Editor/BalanceBench.cs`):** herramienta de editor para simular duelos 1v1 entre clases y combates grupales en tiempo acelerado con informe de métricas.
- **Capturas:** [A01](../Assets/Validacion/Ronda5/A01_inicio_dificultad_hud.png), [A02](../Assets/Validacion/Ronda5/A02_inicio_cartel_dificultad.png).

---

## 2. Menú Radial Concéntrico de 8 Órdenes (`MenuDeOrdenes.cs`)

Se reemplazó el menú radial previo de 6 porciones por un diseño de **dos anillos concéntricos** que combina categorías principales y sub-opciones contextuales:

1. **Anillo interior (8 categorías principales):**
   - **1 · IR ALLÍ:** sub-opciones para mover a todos o a una clase en particular.
   - **2 · CUBRIRSE:** buscar coberturas por cuadrante o muros más cercanos.
   - **3 · MANTENER POSICIÓN:** ordenar a la escuadra plantarse y defender sin perseguir.
   - **4 · CURAR:** solicitar atención del médico o activar botiquín personal.
   - **5 · TANQUE:** subir todos, bajar todos, enviar tanque o descender.
   - **6 · DEMOLER:** plantar carga explosiva (soldado de asalto).
   - **7 · POSEER:** cambiar el control directo a cualquier miembro de la escuadra.
   - **8 · ATACAR:** designar objetivo prioritario o fuego a discreción.
2. **Anillo exterior:** abanico dinámico de sub-opciones según la porción que sobrevuela el cursor virtual o se selecciona con los números 1..8.
3. **Capturas:** [03](../Assets/Validacion/Ronda5/03_radial_categoria_ir_alli.png), [04](../Assets/Validacion/Ronda5/04_radial_capa_exterior.png), [B01](../Assets/Validacion/Ronda5/B01_radial_ir_alli.png) al [B08](../Assets/Validacion/Ronda5/B08_radial_atacar.png).

---

## 3. Demolición de Asalto y Cobertura Táctica

- **Demolición (`Player/Demolicion.cs`, `Presentation/ObstacleMarker.cs`):**
  - Habilidad exclusiva del soldado de Asalto.
  - Requiere estar agachado (`CTRL`), quieto y a menos de 9 m de un obstáculo destruible.
  - Carga sostenida de 4 segundos con anillo de progreso visual (`CirculoDeProgreso`).
  - Al completarse: detonación en área (35 de daño en 4.5 m) y derrumbe total del muro con nube de escombros (`ObstacleMarker.Demoler()`).
- **Cobertura táctica direccional (`Player/OrdenesDeEscuadra.cs`):**
  - Función `BuscarSegunMirada`: detecta coberturas dentro de un cono frontal de 65° y selecciona la cara protegida opuesta a la línea de visión del peligro.
- **Botiquín del Médico (`Player/PedidoDeCuracion.cs`):**
  - Curación personal de 60 puntos en 4 s con enfriamiento de 25 s.
- **Capturas:** [10](../Assets/Validacion/Ronda5/10_demolicion_carga.png), [11](../Assets/Validacion/Ronda5/11_demolicion_hecha.png).

---

## 4. Zoom RTS Anclado al Cursor (`CameraRig.cs`)

- **Anclaje al suelo bajo el cursor (`ZoomHaciaCursor`):**
  - Al usar la rueda del mouse en vista cenital RTS, el punto del mapa que está debajo del cursor se mantiene exactamente en su sitio mientras la cámara sube o baja.
  - Proyección de rayo al plano `y = 0` y compensación del foco de paneo en cada frame.
  - Lerp suave de altura (`AnimarZoom`) a velocidad angular constante sin saltos bruscos.
- **Capturas:** [09](../Assets/Validacion/Ronda5/09_rts_zoom_cursor.png), [C01](../Assets/Validacion/Ronda5/C01_rts_antes_del_zoom.png), [C02](../Assets/Validacion/Ronda5/C02_rts_despues_del_zoom.png).

---

## 5. Modo Misión de Rescate (`Mision/`)

La escena principal `SC_Gameplay` ahora integra un director narrativo completo en 4 fases de juego:

1. **Fase 1 — Infiltrar:** La escuadra avanza desde la base atravesando patrullas enemigas hasta alcanzar la plaza central del poblado (`x=4, z=119`).
2. **Fase 2 — Resistir:** Al llegar a la plaza, se activa una cuenta regresiva de 60 segundos con defensa de posición contra 3 oleadas consecutivas de refuerzos enemigos.
3. **Fase 3 — Rescatar:** Un civil atrapado sale de su refugio (`RoleType.Civilian`, sin armas, modelo diferenciado). La escuadra debe protegerlo y guiarlo.
4. **Fase 4 — Escapar:** Regreso a la zona de aterrizaje (`x=-26, z=-8`). El helicóptero de extracción enciende motores y proporciona fuego de cobertura contra los perseguidores.
5. **Cinemática de Victoria (`CinematicaDeVictoria.cs`):**
   - Transición cinemática con barras negras panorámicas.
   - El helicóptero despega elevándose y rotando sobre el valle mientras huye de la horda enemiga.
   - Pantalla final de victoria con estadísticas.
- **Audio ambiental:**
  - Grabación CC0 del rotor de helicóptero Chinook (`Chinook_flying_over_Greenwich.ogg`).
  - Pista procedural de tensión (`Tension.wav`) con modulación de volumen dinámico en `MusicDirector`.
- **Capturas:** [01](../Assets/Validacion/Ronda5/01_inicio_mision.png), [02](../Assets/Validacion/Ronda5/02_heli_en_espera.png), [05](../Assets/Validacion/Ronda5/05_resistir_oleada.png) a [08](../Assets/Validacion/Ronda5/08_llegando_al_heli_b.png), y serie [cine_1](../Assets/Validacion/Ronda5/cine_1.png) a [cine_8](../Assets/Validacion/Ronda5/cine_8.png).

---

## 6. Actualización del Tutorial (`TutorialManager.cs`)

- Se reestructuraron los pasos del tutorial para utilizar el menú radial [Q] en lugar de teclas directas sueltas.
- Se incorporaron etapas de práctica con balizas y obstáculos generados dinámicamente:
  - Práctica de cobertura táctica con sacos de arena.
  - Práctica de curación con el médico.
  - Práctica de demolición de muro con carga explosiva de asalto.
  - Comando del tanque vía radial [Q] -> TANQUE -> TANQUE ALLÍ.
