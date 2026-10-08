# 10 — Informe final, tanda #104–#132 (Operación Cuartel)

Fecha: 2026-10-07. Sin commit, push ni build nuevo con estos cambios.

## Resumen
- Los 29 bugs/pedidos #104–#132 están implementados (P0–P11) e integrados (P12). Detalle por paso en `07_Progreso_104_132.md`.
- Pasada aislada (Play fresco por check): 118 checks distintos, todos OK al final. Detalle en `08_Resultado_integracion.md`.
- Arreglos posteriores a la tanda:
  - Crash del nivel 2 en la build: `ArtilleroDeTorreta` movida a su propio archivo (regla: nombre de clase = nombre de archivo) y escena reconstruida.
  - Intro con helicóptero rediseñada: ~12,5 s, cámara sin temblor, ~3 s de cruce a toda velocidad, frenada con giro y bajada de soldados más lenta (Bug123 actualizado).
  - Shaders `SP/ArmaEnPrimeraPersona`, `SP/MiraOptica` y `SP/OperacionTexto` agregados a Always Included Shaders.

## Cómo se validó
- Un check por bug, cada uno en un Play fresco, con capturas en `Assets/Validacion/v3_*`.
- Flaky repetidos x3: Bug079 3/3, Bug082b 3/3, Bug085 4/4 (tras ajustar el criterio), Bug074 1/3 y Bug086 2/3 (estadísticos, preexistentes).
- Rendimiento por fase: sin regresión atribuible (Infiltrar 65,9–77,9 fps entre corridas; el ruido es mayor que cualquier diferencia).

## Pendientes honestos
- Suite vieja `RunAll`: 27 fallos preexistentes (audio, regeneración, cobertura/formación, topes de código, SC_TestLevel); clasificados, no investigados hasta la causa raíz.
- Sin medir: fps de Resistir con oleadas reales y de Extraer con el heli cubriendo. Himno y recarga no se escucharon, solo se midieron propiedades.
- F4 (modo dios) sigue activo en la build porque el tutorial lo enseña; F9/F10 también. Decisión del dueño.
- De la revisión `09`: reflexión sobre campos privados en `OperacionDirector.Huida.cs`, logs en la carpeta de instalación y 26 clases con nombre ≠ archivo (agregar check previo a la build).
- `Bug101pa/pb` no se registran en el arnés (patrón de nombre de una letra).

## Estado del repositorio
Todo lo anterior está sin commitear (cambios de la tanda, arreglo del nivel 2, intro, shaders, informes 07–10, checks). La última build comprimida NO incluye la intro nueva ni los shaders.
