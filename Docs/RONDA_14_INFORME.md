# Informe · Ronda 14 — Refactorización Táctica, Interfaz y Sistemas de Combate

**Fecha:** 26 de septiembre de 2026  
**Rama:** `main`  
**Compilación:** Windows Standalone64 · `Assembly-CSharp` y `Assembly-CSharp-Editor` en **0 errores y 0 advertencias críticas**.  
**Pruebas:** Suite Headless Fase 23 completa con todos los módulos validados.

---

## 1. Resumen Ejecutivo

En la Ronda 14 se implementaron de forma integral las **44 tareas planificadas (T-00 a T-44)** distribuidas en 9 olas de trabajo, respetando estrictamente las 13 decisiones de diseño de base (D1–D13). Se optimizó la arquitectura desacoplando los sistemas de UI mediante lienzos separados (`CapasDeHud`), se enriqueció la experiencia visual y auditiva táctica, se expandieron las capacidades de mando del escuadrón en FPS y RTS, y se rediseñó por completo el clímax de la misión reemplazando la extracción aérea por una persecución motorizada en tierra.

---

## 2. Detalle de Tareas Implementadas

### Ola 0 — Baseline e Infraestructura
* **T-00 · Baseline y Arnés de Pruebas:** Creación del marco de ejecución y verificación modular en `HeadlessTestRunner.Fase23.cs`.
* **T-01 · Separación de Canvases:** Creación de `CapasDeHud` con lienzos independientes (`Hud_Mundo`, `Hud_Pantalla`, `Hud_Feedback`, `Hud_Menu`, `Hud_Minimapa`) resolviendo el bloqueo del anillo de recarga en RTS.
* **T-02 · Menú en Cinemática:** Soporte para abrir el menú de pausa con `[Esc]` durante la cinemática de introducción (`CinematicaDeIntro.cs`).
* **T-03 · Auditoría de Colliders:** Script de auditoría `AuditoriaDeColliders.cs` para detectar primitivas invisibles sin renderers y asegurar colliders en todas las coberturas.
* **T-04 · Base del Soldado a Y=0:** Normalización del pivote en `MisionDirector.cs` para evitar soldados levitando.

### Ola 1 — Derrota, HUD y Calidad de Vida
* **T-05 · Razón de Derrota Visible:** Desglose claro y persistente en pantalla de la causa de la derrota (`CausaDeDerrota`: civil muerto, escuadra caída, tiempo agotado).
* **T-06 · Menú Radial Inhabilitado en RTS:** Bloqueo del menú radial táctico en la vista cenital para no interferir con la selección de cajas.
* **T-07 · LOD de Árboles Lejanos:** Corrección de artefactos de renderizado y LOD pop-in en vegetación distante.
* **T-08 · Supresión de Cartel de Cobertura:** Eliminación del banner redundante "X EN COBERTURA" para desclutterizar la interfaz.
* **T-09 · Panel de Selección Abajo al Centro:** Reubicación y diagramado del conteo de unidades seleccionadas en la parte inferior central.
* **T-10 · Formato de Munición y Vida:** Visualización limpia de munición en formato `Actual / Reserva` y vida con barra proporcional sin números confusos.
* **T-11 · Soldado Caído en Radial:** Representación del soldado muerto con tono grisáceo e icono de calavera.
* **T-12 · Viñeta Roja de Daño:** Borde periférico rojo animado con feedback de vibración reactivo a los impactos recibidos.

### Ola 2 — Ajustes y Visibilidad
* **T-13 · Reestructuración de Menú Ajustes:** Configuración unificada de volúmenes (Master × Canales), selector desplegable de resolución nativa, persistencia en PlayerPrefs y eliminación de opciones obsoletas.
* **T-14 · Rombo del Tanque por Ocupación:** Codificación cromática de rombos (Gris: vacío, Azul: aliado, Rojo: enemigo, Violeta: mixto/tripulado).
* **T-15 · Rombos de Interactuables:** Señalización superior de torretas (naranja), botiquines (verde) y munición (cian) mediante `InteractableDiamond.cs`.
* **T-16 · Visibilidad Tras Muros por 4s:** Rombos de enemigos impactados visibles a través de la geometría por 4 segundos con shader ZTest Always.

### Ola 3 — Minimapa y Navegación
* **T-17 · Formas del Minimapa:** Iconografía diferencial en `MinimapIcon.cs`: aliados en círculos azules, enemigos en triángulos rojos, vehículos en cuadrados.
* **T-18 · Ondas de Impacto en Minimapa:** Ondas de anillos expansivos desde la posición de atacantes que hieren al jugador (`MinimapAttackerRings.cs`).
* **T-19 · Salto Rápido de Tomas:** Avance rápido entre planos de cinemática con barra espaciadora y fundido de 0.5s.
* **T-20 · Clic y Doble Clic en Roster:** Clic simple centra la cámara RTS en el soldado; doble clic entra en vista FPS y toma el control.

### Ola 4 — Controles RTS y Puntería
* **T-21 · Control de Cámara RTS:** Tecla `[F]` focaliza en la unidad activa, rueda de ratón con zoom 3× y rotación orbital suave al mantener `[|]` (Backquote).
* **T-22 · Teclas F1 / F2 / F3:** Selección instantánea y centrado de cámara para los tres miembros de escuadra.
* **T-23 · Cursor Contextual Dinámico:** Puntero adaptativo en FPS y RTS con formas y colores según acción (Atacar, Seguir, Montar, Recoger, Cubrirse).
* **T-24 · Órdenes de Cobertura con Tecla C:** Mantener `[C]` dibuja líneas tácticas de escuadra y amenazas; `[C] + Clic Izquierdo` envía orden de tomar cobertura.

### Ola 5 — Tácticas de Escuadra e IA
* **T-25 · Formación en Cuña:** Los aliados se posicionan a los costados y levemente hacia atrás del líder, protegiendo los flancos.
* **T-26 · Contagio de Agachado:** Cuando el jugador se agacha, los soldados aliados cercanos imitan la postura sigilosa.
* **T-27 · Asistencia Autónoma del Médico:** El médico cura o reanima automáticamente a aliados caídos en su radio si no está bajo fuego directo.
* **T-28 · Reanimación en Calma:** Durante periodos de calma (sin daño por 9s), cualquier aliado puede reanimar a un compañero caído; regeneración ajustada a ritmo realista.
* **T-29 · Ciclo de Cobertura Inteligente:** La IA busca esquinas, se agacha, asoma el torso para disparar ráfagas y recarga oculta detrás de la protección.

### Ola 6 — Rescate del Rehén
* **T-30 · Civil Atado e Inmóvil:** El rehén permanece en posición fija atado de pies y manos hasta que se complete la maniobra de rescate.
* **T-31 · Sistema de Desatado por Nudos:** Rescate interactivo pulsando repetidamente `[Espacio]` para desatar 12 nudos, con barra de progreso world-space `BarraNudos.cs` sobre el civil e inhibición de salto durante la interacción.
* **T-32 · Visibilidad y Textura del Civil:** El civil viste ropa azul distintiva y no se oculta tras los aliados al ser rescatado.
* **T-33 · Cartel de Retorno:** Aviso animado y flecha indicadora "VUELVE AL OBJETIVO" cuando el jugador se aleja del perímetro de misión.

### Ola 7 — Efectos y Combate
* **T-34 · Luminarias Destructibles:** Faroles y lámparas se apagan al recibir impactos balísticos, emitiendo chispas y sonido metálico (`Luminaria.cs`).
* **T-35 · Lanzacohetes Antitanque:** Multiplicador de daño `2.5×` para cohetes contra tanques y blindados, permitiendo neutralizarlos de dos impactos directos.
* **T-36 · Disparos a la Cabeza (Headshots):** Impactos por encima del umbral vertical (1.45m de pie / 0.95m agachado) causan daño doble `2.0×`, evento dedicado y sonido resonante.
* **T-37 · Temporizador de Granadas:** Anillo visual en el suelo y parpadeo luminoso que acelera conforme se acerca la detonación (`Granada.cs`).
* **T-38 · Persistencia de Cadáveres:** Cola circular FIFO de hasta 24 cuerpos visibles en el campo de batalla (`CubeFxReactor.cs`).
* **T-39 · Centrado Óptico en ADS:** Alineación focal nítida del arma al apuntar con la mira sin distorsión de campo de visión.
* **T-40 · Máscaras de Animación de Torso:** Capa superior de animación en `SoldierAnimatorDriver.cs` para permitir ataques cuerpo a cuerpo, lanzamiento de granadas y recargas en movimiento sin congelar las piernas.
* **T-41 · Emisión de Polvo al Desplazarse:** Partículas de tierra dinámicas bajo las pisadas de infantería y las orugas del tanque (`DustEmitter.cs`).

### Ola 8 — Clímax y Persecución
* **T-42 · Extracción en Camioneta:** Retiro del helicóptero estático y reemplazo por una camioneta armada con ruta de escape hacia la base (`Camioneta.cs`).
* **T-43 · Motocicletas Enemigas Hostiles:** Oleadas de motos enemigas rápidas que persiguen a la camioneta disparando en movimiento y explotan al ser destruidas (`MotoEnemiga.cs`, `MotoEnemigaDirector.cs`).

### Ola 9 — Cierre y QA
* **T-44 · QA Total y Actualización de Índices:** Indexación completa del proyecto para herramientas de asistencia (`Tools/Indice/indexar_ia.py`), validación de contratos entre ensamblados y generación de informe final.

---

## 3. Matriz de Decisiones de Diseño (D1–D13)

| Decisión | Definición | Estado |
|---|---|---|
| **D1** | `[C]` sostenido dibuja líneas tácticas de tiro y aliados | ✅ Implementado |
| **D2** | `[C] + Clic` envía orden táctica de cobertura | ✅ Implementado |
| **D3** | Inhabilitación de órdenes radiales en vista RTS | ✅ Implementado |
| **D4** | `[F]` en RTS focaliza en la unidad poseída o seleccionada | ✅ Implementado |
| **D5** | `[\|]` (Backquote) activa rotación orbital de cámara RTS | ✅ Implementado |
| **D6** | `[Espacio]` cerca del civil desata nudos e inhibe el salto | ✅ Implementado |
| **D7** | Separación completa de lienzos de HUD | ✅ Implementado |
| **D8** | Sistema de volumen maestro que multiplica canales (PlayerPrefs) | ✅ Implementado |
| **D9** | Temporizador de calma de 9s para reanimación universal | ✅ Implementado |
| **D10** | Detección de Headshot por umbral vertical con daño 2× | ✅ Implementado |
| **D11** | Daño aumentado de cohetes contra vehículos (multiplicador 2.5×) | ✅ Implementado |
| **D12** | Cadáveres visibles con límite de 24 en cola FIFO | ✅ Implementado |
| **D13** | Paleta unificada de colores para rombos y minimapa | ✅ Implementado |

---

## 4. Estado de la Compilación

```
Compilación de Assembly-CSharp.csproj: EXITOSO (0 errores)
Compilación de Assembly-CSharp-Editor.csproj: EXITOSO (0 errores)
Estado de Git: Rama main sincronizada con origin/main
```
