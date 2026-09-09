# SP_Arte — arte del proyecto

Hecho **sobre la sesion de Maya 2026.3 abierta**, via el commandPort (`commandportDefault`,
127.0.0.1:50007, tipo MEL). Originales en `_backup_claude/`.

---

## 1. Archivos

```
SP_Arte/
  MY_Modulares.mb       KIT: 13 pisos + 9 de estructura + 4 vallas + escombros
  MY_Ambiente.mb        vegetacion, coberturas, destruccion, props, emplazamiento
  MY_Soldado.mb         escuadron de 5 + arsenal de 12 armas + malla base
  MY_Vehiculos.mb       tanque en 3 piezas
  MY_Nivel.mb           DISENO DE NIVEL: trama urbana de 51 x 131 unidades
  MY_Showcase.mb        referencia a las 4 librerias, para ver todo junto
  Trimsheet_1024.png          LA textura, 1024x1024, 64 colores, sin texto
  Trimsheet_1024_GUIA.png     hoja de referencia (documentacion)
  Trimsheet_1024_map.json     el mismo dato para scripts
  _FBX_Export/          79 FBX
  _capturas/            capturas por tema
  _backup_claude/       los .mb originales
```

## 2. Cuanto hay

| Escena | Assets | Mallas | Caras | Triangulos |
|---|---|---|---|---|
| MY_Modulares.mb | 27 | 396 | 2376 | 4752 |
| MY_Ambiente.mb | 24 | 276 | 4178 | 7372 |
| MY_Soldado.mb | 23 | 160 | 2886 | 5260 |
| MY_Vehiculos.mb | 3 | 39 | 718 | 1112 |
| **Libreria** | **77** | **871** | **10158** | **18496** |
| MY_Nivel.mb | 737 colocados | 855 | 67682 | 130008 |

## 3. Low poly

Todo se rehizo en modo low poly. Lo que mas pesaba era el **bisel**: convierte una caja de 6
caras en 26. Ahora el bisel se aplica **solo cuando define la forma** (bolsas de arena, oruga
del tanque, barril) y siempre con 1 segmento; los biseles cosmeticos de 0.01 a 0.04 se
descartan. Ademas los cilindros bajaron a 8 lados (6 si son finos) y las esferas a 6x4.

Resultado: la libreria paso de 22528 a **18496 triangulos** *habiendo agregado* 5 pisos, 4
vallas, 11 props de destruccion, 5 armas y 3 arbustos. Por asset la caida es de mas de la mitad.

Referencia: soldado completo con arma ~1000 tris, tanque entero 1112, baldosa de piso 40-150,
arbol 300-500.

## 4. Convencion de nombres

`<tipo>_<categoria>_<asset>_<parte>_<##>` — `GRP_`/`SM_` y `Env_`, `Mod_`, `Chr_`, `Wpn_`,
`Veh_`, `Nivel_`. En Unity y en el Outliner, escribir `SM_Mod_` filtra el kit entero.

## 5. Kit modular — grilla de 4

En Unity: `Edit > Grid and Snap Settings > Move 4`, rotar de a 90.

Pivote en (0,0,0) y centrado en X/Z. Los pisos tienen la **cara pisable en Y=0**, asi lo que
apoyes encima va en Y=0 sin calcular offsets. Los muros arrancan en Y=0 y suben 3.

**13 pisos** (4x4): Concreto, ConcretoRoto, Baldosa, Adoquin, Asfalto, Metal, Grava, Escombro,
Tierra, Arena, Madera, Pasto, CespedSeco.

Ninguno lleva manchas grandes asimetricas: al tilear, cualquier detalle no repetible se
convierte en un patron obvio. Los detalles son chicos y numerosos.

**9 de estructura**: Muro_Recto (4x3), Muro_Medio (2x3), Muro_Puerta (vano 1.6x2.2),
Muro_Ventana (vano 1.8, de 1.1 a 2.2), Muro_Pilar, Muro_RotoAlto, Muro_RotoBajo, Losa (4x4) y
Escalera (4 de largo, 3 de alto).

**4 vallas** de largo 4: Madera, Alambrado, Puas, Chapa.

## 6. Ambiente — 24 assets

**Vegetacion**: ArbolA (conifera, 6 faldones apilados), ArbolB (copa redonda, 9 masas),
Arbusto_Chico / _Medio / _Grande. Los dos arboles se rehicieron con **troncos de cono**
(cilindro con el anillo superior escalado), que es lo que les faltaba para tener silueta.

**Coberturas**: Barricada_Tablones, Barricada_Sacos, Barricada_Erizo, Barricada_Concreto, Barril.

**Destruccion**: Escombro_Pila_A, Escombro_Pila_B, Muro_Caido, Crater, Auto_Quemado, Poste_Caido.

**Props**: Caja_Madera, Palet, Neumaticos, Farol, Cartel.

**Emplazamiento**: tripode + ametralladora (pivote en el eje) + cajon de municiones.

## 7. Tanque, escuadron, arsenal

Tanque en 3 grupos con pivote propio: `Cuerpo` (origen), `Torreta` (anillo de giro),
`Metralleta` (base de la cupula). Oruga biselada, 6 cilindros de rodamiento por lado.

5 soldados diferenciados por remera, con pelo, casco/gorra/boina, arma y extras.
**12 armas**: Fusil, AK, Metralleta, LMG, Escopeta, Sniper, Lanzagranadas, Lanzacohetes,
Pistola, Revolver, Cuchillo, Granada. Todas sobre el ancla de la mano derecha con el pivote
del grupo ahi mismo: se parentean a cualquier soldado sin recolocar.

## 8. Diseno de nivel — `MY_Nivel.mb`

Sector de **51 x 131** unidades, cuatro veces mas largo hacia adelante que la primera version.
No es un laberinto: es una **trama urbana regular**, espaciosa y despejada.

| Elemento | Medida |
|---|---|
| Avenida central | 8 de ancho, todo el largo |
| Veredas de la avenida | 4 a cada lado |
| Calles transversales | 8 de ancho, tres cruces |
| Manzanas | 16 x 24 |
| Edificio por manzana | 8 x 16, centrado, deja 4 de vereda alrededor |

**Por que se cambio.** La version anterior era un laberinto generado con DFS y trenzado. Se
veia bien en planta pero a ras de suelo era claustrofobico: pasillos de 4 y paredes de 3 en
todas las direcciones. La trama de manzanas da la misma superficie con lineas de vision largas
y varias rutas obvias, que es lo que hace falta para moverse y flanquear.

**Los 6 edificios salen del mismo molde** (`edificio()`): perimetro de muros de 4, una puerta
centrada en el frente, ventanas alternadas en los laterales, pilares en las esquinas y losa de
techo. Dos de los seis se generan como ruina (muros rotos, sin techo, con escombro adentro), lo
que rompe la uniformidad sin desordenarla.

**La cobertura esta pautada, no desparramada**: sacos y barreras en las esquinas de cada
manzana, erizos en los cruces, y barriles / cajas / palets / neumaticos alineados sobre las
veredas a intervalo fijo. Arbolado cada 8 sobre los bordes, faroles cada 24 sobre la avenida,
arbustos en las cuatro esquinas de cada edificio.

| | |
|---|---|
| Baldosas de piso | 384 |
| Edificios | 6 |
| Coberturas | 39 |
| Total colocado | 737 |
| Mallas | 855 |
| Triangulos | 130008 |

Paso de 171 coberturas sueltas a 39 puestas a proposito, y de 210780 a **130008 triangulos**.

**Optimizacion.** Cada asset estatico se **unifica en una sola malla** (`polyUnite`) antes de
repetirse: 737 objetos colocados dan 855 mallas en vez de las ~7000 que habria sin unificar.
La baldosa de asfalto trae la linea pintada a lo largo de Z, asi que en las calles
transversales se gira 90 grados para que la linea siga la calle.

Al importar las 4 librerias quedan 4 shadingEngines (uno por namespace); se unifican a uno.

> El nivel es una **maqueta de layout**. En produccion conviene rearmarlo en Unity con los
> prefabs, que es donde se le pone colision, navegacion y spawns.

## 9. Verificacion — las 5 escenas, cara por cara

| Escena | Mallas | Caras | Sin UV | Fuera de celda | Materiales | Texturas | Nombres |
|---|---|---|---|---|---|---|---|
| MY_Ambiente.mb | 276 | 4178 | 0 | **0** | 1 | solo el trimsheet | OK |
| MY_Soldado.mb | 160 | 2886 | 0 | **0** | 1 | solo el trimsheet | OK |
| MY_Vehiculos.mb | 39 | 718 | 0 | **0** | 1 | solo el trimsheet | OK |
| MY_Modulares.mb | 396 | 2376 | 0 | **0** | 1 | solo el trimsheet | OK |
| MY_Nivel.mb | 855 | 67682 | 0 | **0** | 1 | solo el trimsheet | OK |
| **Total** | **1726** | **77840** | **0** | **0** | **1** | **1** | **OK** |

Se verifica que cada cara caiga entera dentro de una sola celda con el 90% del margen hasta el
borde, que exista un unico shadingEngine, que el unico nodo `file` apunte al trimsheet, y que
todas las mallas sigan la convencion de nombres.

## 10. Unity

Proyecto **Unity 6000.5.6f1 con URP 17.5**. `Assets/Editor/SP_ArteSetup.cs`:

- **Automatico**: el trimsheet se importa con filtro **Point**, sin compresion, wrap Clamp. Es
  un atlas de color plano: con filtro bilineal o DXT las celdas se mezclan y aparecen colores
  que no existen. Los FBX entran sin material y sin soldar vertices.
- **Menu** `Strategic Point > Arte > Trimsheet > Hacer todo`: crea `MAT_Trimsheet` (URP/Lit) y
  un prefab por FBX con ese material.

Convive con `ArtSetup.cs` y `ArtBuilder.cs` que ya estaban: distinto namespace, distinta clase
y submenu propio.

## 11. Bugs encontrados en el camino

1. `colorSet1` heredado de Substance: Viewport 2.0 dibuja colores de vertice en vez de la textura.
2. `uvLink` colgando del UV set equivocado.
3. `cmds.duplicate` copia `visibility`: los cuerpos nacian invisibles.
4. Botiquin del medico dentro de la mano; brazalete dentro del brazo.
5. **Pintar antes de biselar** deja caras nuevas sin asignar.
6. Borrar el grupo contenedor antes de usar la malla que vive adentro.
7. Al importar varias escenas, cada namespace trae **su propio** shadingEngine.
8. Filtrar por prefijo sobre nodos referenciados falla: llevan `NS:` adelante.

## 12. Pendiente

- **Verlo corriendo**: la unica instancia de Unity abierta es de otro proyecto
  (`C:\.TBT\Proyectos\_MIP_Unity`). Queda abrir el proyecto, correr el menu y armar la escena.
- **`_capturas/` y `_backup_claude/` se importan como assets**. `_backup_claude/` quedo
  ignorado por git; si tambien molesta en Unity, renombrar a `_backup_claude~`.
