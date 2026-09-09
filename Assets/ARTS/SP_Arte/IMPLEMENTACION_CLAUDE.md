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
  MY_Nivel.mb           DISENO DE NIVEL armado con el kit
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
| MY_Nivel.mb | 125 colocados | 1557 | 25484 | 48552 |

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

Sector de **48 x 34** unidades, 96 baldosas de piso y 114 props colocados:

- Ruta de asfalto N-S con vereda de concreto a los lados
- Edificio de dos plantas a la izquierda (muros, losas, escalera), armado con el kit
- Edificio en ruinas detras, con escombro y muro caido
- Puesto de control sobre la ruta: erizos, barreras, sacos, emplazamiento y cartel
- Tanque sobre la ruta, auto quemado, 2 crateres, poste caido, faroles, barriles
- Zona verde a la derecha con 7 arboles y 8 arbustos
- Perimetro cerrado con las 4 vallas
- El escuadron de 5 desplegado en el puesto

Cada baldosa de piso se **unifica en una sola malla** (`polyUnite`) antes de repetirse: 96
mallas de piso en vez de ~1200. Al importar las 4 librerias quedaban 4 shadingEngines (uno por
namespace); se unificaron a uno solo.

> El nivel es una **maqueta de layout**. En produccion conviene rearmarlo en Unity con los
> prefabs, que es donde se le pone colision, navegacion y spawns.

## 9. Verificacion — las 5 escenas, cara por cara

| Escena | Mallas | Caras | Sin UV | Fuera de celda | Materiales | Texturas | Nombres |
|---|---|---|---|---|---|---|---|
| MY_Ambiente.mb | 276 | 4178 | 0 | **0** | 1 | solo el trimsheet | OK |
| MY_Soldado.mb | 160 | 2886 | 0 | **0** | 1 | solo el trimsheet | OK |
| MY_Vehiculos.mb | 39 | 718 | 0 | **0** | 1 | solo el trimsheet | OK |
| MY_Modulares.mb | 396 | 2376 | 0 | **0** | 1 | solo el trimsheet | OK |
| MY_Nivel.mb | 1557 | 25484 | 0 | **0** | 1 | solo el trimsheet | OK |
| **Total** | **2428** | **35642** | **0** | **0** | **1** | **1** | **OK** |

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
