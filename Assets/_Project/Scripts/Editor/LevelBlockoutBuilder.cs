using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Presentation;
using SP.Vehicles;

namespace SP.EditorTools
{
    // "Blockout" del nivel de SC_Gameplay: SOLO pintura de terreno + cubos.
    //
    // Todo el bloqueo es un cubo (BoxCollider + ObstacleMarker, asi entra al
    // minimapa, a las coberturas y a la navegacion sin tocar nada mas); todo
    // el "suelo" es pintura de las capas del terreno (pasto / tierra / grava / hojarasca).
    //
    // NIVEL DE 116,8 x 320 m en ocho bloques que se recorren de sur a norte, con TRES CAMINOS hasta el rehen (ver
    // RutasDelNivel): la carretera central (grava, iluminada, con los tanques), el sendero del bosque (oeste) y el camino
    // de servicio (este). Los muros del cañon y de la chicane tienen una puerta para cada flanco, la aldea tiene calles
    // que cruzan los tres caminos y el fortin se puede rodear o entrar por sus brechas. El REHEN esta en el extremo norte
    // del mapa (el refugio), a ~300 m de la base: ir a buscarlo y volver es la mitad dura de la mision.
    //
    // Es IDEMPOTENTE: cada corrida borra "Nivel_Blockout" y lo rearma, asi que
    // se puede retocar la tabla de abajo y volver a correr desde
    //   Strategic Point > Nivel > Construir blockout
    //
    //   1. Base ................ z -22 ..  14   patio de partida (escuadra + tanque propio + helipuerto)
    //   2. Campo de tiro ....... z  16 ..  64   coberturas bajas destructibles y un puesto de control
    //   3. Paso del cañon ...... z  68 ..  88   muros con tres pasos: sendero (O), hueco central y camino de servicio (E)
    //   4. Aldea ............... z  90 .. 142   casas, calle central, plaza y dos calles transversales
    //   5. Puesto avanzado ..... z 148 .. 190   TANQUE ENEMIGO 1 + 4 soldados
    //   6. Chicane ............. z 194 .. 220   muros en S para el centro y una puerta para cada flanco
    //   7. Fortin .............. z 224 .. 270   TANQUE ENEMIGO 2 + 6 soldados, porton sur/norte y brechas laterales
    //   8. Refugio del rehen ... z 274 .. 298   TANQUE ENEMIGO 3 + guardias, casa del rehen y tres entradas
    public static class LevelBlockoutBuilder
    {
        const string RootName = "Nivel_Blockout";

        // Limites del mundo jugable (los del cubo "Ground").
        public static readonly Vector3 TerrainOrigin = new Vector3(-54.2f, 0f, -22.3f);
        public static readonly Vector3 TerrainSize = new Vector3(116.8f, 30f, 320f);

        const string TerrainDataPath = "Assets/_Project/Terrains/TerrainData_Main.asset";
        const string TerrainBackupPath = "Assets/_Project/Terrains/TerrainData_Main_ANTES_del_blockout.asset";
        const string MaterialFolder = "Assets/_Project/Materials/Blocking";
        const string PrefabEnemigo = "Assets/_Project/Prefabs/P_Soldier_Enemy.prefab";
        const string PrefabTanque = "Assets/_Project/Prefabs/P_Vehicle_Blindado.prefab";

        const int HpIndestructible = 999999;

        // El terreno del nivel (no el "Terrain_Fondo" lejano, que tambien es un Terrain de la escena).
        public static Terrain TerrenoPrincipal()
        {
            Terrain elegido = null;
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "Terrain_Fondo") continue;
                if (elegido == null || t.name == "Terrain_Main") elegido = t;
            }
            return elegido;
        }

        struct Bloque
        {
            public string Grupo, Nombre, Material;
            public float X, Z, Ancho, Alto, Fondo;
            public int Vida;
        }

        static readonly List<Bloque> Bloques = new List<Bloque>();

        static void B(string grupo, string nombre, string mat, float x, float z, float ancho, float alto, float fondo, int vida = HpIndestructible)
            => Bloques.Add(new Bloque { Grupo = grupo, Nombre = nombre, Material = mat, X = x, Z = z, Ancho = ancho, Alto = alto, Fondo = fondo, Vida = vida });

        // Cobertura baja destructible (300 de vida): 3x1 o 1x3.
        static void C(string grupo, string nombre, float x, float z, bool horizontal = true, int vida = 300)
            => B(grupo, nombre, "Cobertura", x, z, horizontal ? 3f : 1f, 1.2f, horizontal ? 1f : 3f, vida);

        // Muro corrido entre dos X (o dos Z) a una Z (o X) dada.
        static void MuroX(string grupo, string nombre, float x0, float x1, float z, float alto = 4f, float grosor = 3f, string mat = "Muro", int vida = HpIndestructible)
            => B(grupo, nombre, mat, (x0 + x1) * 0.5f, z, x1 - x0, alto, grosor, vida);

        // Erizo checo: cobertura chica y destructible en los pasos.
        static void Erizo(string grupo, string nombre, float x, float z)
            => B(grupo, nombre, "Cobertura", x, z, 2.2f, 1.6f, 2.2f, 500);

        static void DefinirBloques()
        {
            Bloques.Clear();

            // ---------------- 1. BASE ----------------
            const string base1 = "1_Base";
            B(base1, "Base_Muro_Oeste", "Muro", -52f, -4f, 1.5f, 3f, 36f);
            B(base1, "Base_Muro_Este", "Muro", 60f, -4f, 1.5f, 3f, 36f);
            B(base1, "Base_Muro_Sur", "Muro", 4f, -21.5f, 111f, 3f, 1.5f);
            B(base1, "Casilla_Base", "Casa", -33f, 7f, 8f, 4f, 6f);
            C(base1, "Cobertura_Base_1", -24f, 9f);
            C(base1, "Cobertura_Base_2", -8f, 11f);
            C(base1, "Cobertura_Base_3", 20f, 10f);
            C(base1, "Cobertura_Base_4", 38f, 8f, false);
            B(base1, "Contenedor_Base_1", "Contenedor", 44f, -14f, 6f, 2.8f, 2.5f);
            B(base1, "Contenedor_Base_2", "Contenedor", 50f, -8f, 2.5f, 2.8f, 6f);

            // ---------------- 2. CAMPO DE TIRO ----------------
            const string campo = "2_CampoDeTiro";
            C(campo, "Cob_A1", -44f, 22f); C(campo, "Cob_A2", -24f, 24f, false); C(campo, "Cob_A3", 18f, 22f); C(campo, "Cob_A4", 34f, 24f); C(campo, "Cob_A5", 52f, 22f, false);
            C(campo, "Cob_B1", -34f, 32f); C(campo, "Cob_B2", -12f, 34f, false); C(campo, "Cob_B3", 24f, 33f, false); C(campo, "Cob_B4", 42f, 32f); C(campo, "Cob_B5", 56f, 34f);
            C(campo, "Cob_C1", -46f, 42f, false); C(campo, "Cob_C2", -20f, 43f); C(campo, "Cob_C3", 30f, 44f); C(campo, "Cob_C4", 52f, 42f, false);
            C(campo, "Cob_D1", -30f, 54f, false); C(campo, "Cob_D2", -8f, 55f); C(campo, "Cob_D3", 18f, 54f); C(campo, "Cob_D4", 44f, 56f, false);
            B(campo, "Ruina_Campo_1", "Casa", -31f, 38f, 4f, 3f, 4f);
            B(campo, "Ruina_Campo_2", "Casa", 42f, 48f, 4f, 3f, 4f);
            // Puesto de control sobre la carretera: dos erizos y un hueco de 8 m (pasa el tanque).
            Erizo(campo, "Erizo_Campo_1", -2f, 60f); Erizo(campo, "Erizo_Campo_2", 10f, 60f);
            // Alambrado que encauza el sendero del bosque y el camino de servicio.
            B(campo, "Valla_Campo_O1", "Cobertura", -36f, 14f, 1f, 2f, 10f, 500);
            B(campo, "Valla_Campo_O2", "Cobertura", -35f, 54f, 1f, 2f, 10f, 500);
            B(campo, "Valla_Campo_E1", "Cobertura", 41f, 16f, 1f, 2f, 10f, 500);
            B(campo, "Valla_Campo_E2", "Cobertura", 43f, 52f, 1f, 2f, 12f, 500);

            // ---------------- 3. PASO DEL CAÑON: tres pasos (sendero O x -48..-38, hueco central x -6..12, servicio E x 46..56) ----------------
            const string canon = "3_PasoDelCanon";
            MuroX(canon, "Muro_Canon_Oeste_A", -54.2f, -48f, 72f);
            MuroX(canon, "Muro_Canon_Oeste_B", -38f, -6f, 72f);
            MuroX(canon, "Muro_Canon_Este_A", 12f, 46f, 72f);
            MuroX(canon, "Muro_Canon_Este_B", 56f, 62.6f, 72f);
            B(canon, "Bunker_Canon_Oeste", "Cobertura", -9f, 79f, 5f, 2f, 3f, 700);
            B(canon, "Bunker_Canon_Este", "Cobertura", 15f, 79f, 5f, 2f, 3f, 700);
            B(canon, "Torre_Vigia_Oeste", "Torre", -22f, 79f, 3f, 5f, 3f);
            B(canon, "Torre_Vigia_Este", "Torre", 30f, 79f, 3f, 5f, 3f);
            // Los pasos laterales: un obstaculo en el medio de cada uno obliga a serpentear (y da donde cubrirse).
            C(canon, "Sacos_Paso_Oeste_1", -44f, 78f); C(canon, "Sacos_Paso_Oeste_2", -40f, 82f, false); Erizo(canon, "Erizo_Paso_Oeste", -46f, 84f);
            C(canon, "Sacos_Paso_Este_1", 50f, 78f); C(canon, "Sacos_Paso_Este_2", 54f, 82f, false); Erizo(canon, "Erizo_Paso_Este", 48f, 84f);

            // ---------------- 4. ALDEA: calle libre x -14..20; carriles laterales libres (x < -38 y x > 47) ----------------
            const string aldea = "4_Aldea";
            B(aldea, "Casa_O1", "Casa", -33.5f, 98f, 9f, 5f, 7f);
            B(aldea, "Casa_O2", "Casa", -20f, 96f, 8f, 5f, 7f);
            B(aldea, "Casa_O3", "Casa", -32f, 114f, 8f, 5f, 8f);
            B(aldea, "Casa_O4", "Casa", -20f, 116f, 9f, 5f, 7f);
            B(aldea, "Casa_O5", "Casa", -31f, 132f, 8f, 5f, 7f);
            B(aldea, "Casa_O6", "Casa", -19f, 134f, 8f, 5f, 6f);
            B(aldea, "Casa_E1", "Casa", 28f, 98f, 9f, 5f, 7f);
            B(aldea, "Casa_E2", "Casa", 40f, 96f, 8f, 5f, 7f);
            B(aldea, "Casa_E3", "Casa", 30f, 116f, 9f, 5f, 7f);
            B(aldea, "Casa_E4", "Casa", 41f, 114f, 8f, 5f, 8f);
            B(aldea, "Casa_E5", "Casa", 28f, 134f, 8f, 5f, 7f);
            B(aldea, "Casa_E6", "Casa", 41f, 132f, 8f, 5f, 7f);
            B(aldea, "Pozo", "Cobertura", 4f, 115f, 2f, 1f, 2f, 400);
            C(aldea, "Sacos_Aldea_1", -8f, 104f, false, 400);
            C(aldea, "Sacos_Aldea_2", 16f, 126f, true, 400);
            B(aldea, "Barricada_Aldea", "Cobertura", -6f, 124f, 1f, 1.2f, 3f, 300);
            B(aldea, "Carro_Aldea", "Cobertura", 10f, 106f, 3f, 1.4f, 1.6f, 300);
            C(aldea, "Cob_Callejon_1", -10f, 130f); C(aldea, "Cob_Callejon_2", 14f, 100f);
            // Ruinas y erizos en las calles transversales (donde se cruzan los tres caminos).
            B(aldea, "MuroCaido_Aldea_1", "Cobertura", -28f, 106f, 3.6f, 1.3f, 1.8f, 300);
            B(aldea, "MuroCaido_Aldea_2", "Cobertura", 36f, 124f, 3.6f, 1.3f, 1.8f, 300);
            Erizo(aldea, "Erizo_Aldea_1", -2f, 108f); Erizo(aldea, "Erizo_Aldea_2", 12f, 122f);
            // Alambrado a los costados de la aldea: los carriles quedan a la vista pero separados.
            B(aldea, "Valla_Aldea_O", "Cobertura", -39.5f, 88f, 1f, 2f, 12f, 500);
            B(aldea, "Valla_Aldea_E", "Cobertura", 46.5f, 138f, 1f, 2f, 12f, 500);

            // ---------------- 5. PUESTO AVANZADO ENEMIGO ----------------
            const string puesto = "5_PuestoAvanzado";
            C(puesto, "Sacos_Puesto_1", -16f, 156f, true, 400); C(puesto, "Sacos_Puesto_2", 24f, 156f, true, 400);
            B(puesto, "Torre_Puesto", "Torre", -30f, 168f, 4f, 6f, 4f);
            B(puesto, "Muro_Puesto_Oeste", "Muro", -34f, 172f, 1.5f, 3f, 20f);
            B(puesto, "Muro_Puesto_Este", "Muro", 42f, 172f, 1.5f, 3f, 20f);
            B(puesto, "Bunker_Puesto", "Cobertura", 4f, 178f, 6f, 2f, 2.5f, 600);
            C(puesto, "Caja_P1", -8f, 170f); C(puesto, "Caja_P2", 18f, 166f); C(puesto, "Caja_P3", 32f, 182f, false); C(puesto, "Caja_P4", -22f, 184f); C(puesto, "Caja_P5", 8f, 192f);
            Erizo(puesto, "Erizo_Puesto_1", -10f, 150f); Erizo(puesto, "Erizo_Puesto_2", 18f, 150f);

            // ---------------- 6. CHICANE: S para el centro + puerta oeste (x -48..-38) y puerta este (x 46..56) ----------------
            const string chicane = "6_Chicane";
            MuroX(chicane, "Muro_Chicane_1_Oeste", -54.2f, -48f, 198f, 4f, 2.5f);
            MuroX(chicane, "Muro_Chicane_1_Centro", -38f, 14.2f, 198f, 4f, 2.5f);    // hueco central-este: x 14 .. 62
            MuroX(chicane, "Muro_Chicane_2_Centro", -10f, 46f, 210f, 4f, 2.5f);      // hueco oeste: x -54 .. -10
            MuroX(chicane, "Muro_Chicane_2_Este", 56f, 62.6f, 210f, 4f, 2.5f);
            C(chicane, "Cob_Chi_1", 30f, 203f); C(chicane, "Cob_Chi_2", -30f, 204f); C(chicane, "Cob_Chi_3", 0f, 216f, false); C(chicane, "Cob_Chi_4", 48f, 216f);
            Erizo(chicane, "Erizo_Chicane_O", -43f, 202f); Erizo(chicane, "Erizo_Chicane_E", 51f, 206f);

            // ---------------- 7. FORTIN: x -30..46, z 224..270, porton sur x -2..14, porton norte x 0..8, brechas laterales ----------------
            const string fortin = "7_Fortin";
            B(fortin, "Muralla_Sur_Oeste", "Muro", -16f, 224f, 28f, 3.5f, 1.5f);
            B(fortin, "Muralla_Sur_Este", "Muro", 30f, 224f, 32f, 3.5f, 1.5f);
            B(fortin, "Muralla_Oeste_A", "Muro", -30f, 232f, 1.5f, 3.5f, 16f);
            B(fortin, "Brecha_Oeste", "Brecha", -30f, 244f, 1.5f, 3.5f, 8f, 700);
            B(fortin, "Muralla_Oeste_B", "Muro", -30f, 259f, 1.5f, 3.5f, 22f);
            B(fortin, "Muralla_Este_A", "Muro", 46f, 232f, 1.5f, 3.5f, 16f);
            B(fortin, "Brecha_Este", "Brecha", 46f, 244f, 1.5f, 3.5f, 8f, 700);
            B(fortin, "Muralla_Este_B", "Muro", 46f, 259f, 1.5f, 3.5f, 22f);
            B(fortin, "Muralla_Norte_A", "Muro", -15f, 270f, 30f, 3.5f, 1.5f);
            B(fortin, "Muralla_Norte_B", "Muro", 27f, 270f, 38f, 3.5f, 1.5f);
            B(fortin, "Torre_Fortin", "Torre", 8f, 249f, 4f, 7f, 4f);
            B(fortin, "Torre_Fortin_SO", "Torre", -27f, 228f, 4f, 7f, 4f);
            B(fortin, "Torre_Fortin_SE", "Torre", 43f, 228f, 4f, 7f, 4f);
            B(fortin, "Torre_Fortin_NO", "Torre", -27f, 266f, 4f, 7f, 4f);
            B(fortin, "Torre_Fortin_NE", "Torre", 43f, 266f, 4f, 7f, 4f);
            B(fortin, "Cuartel_O", "Casa", -18f, 258f, 10f, 4f, 8f);
            B(fortin, "Cuartel_E", "Casa", 32f, 258f, 10f, 4f, 8f);
            C(fortin, "Caja_F1", -20f, 236f); C(fortin, "Caja_F2", 28f, 236f); C(fortin, "Caja_F3", 0f, 240f, false); C(fortin, "Caja_F4", 16f, 264f);
            C(fortin, "Caja_F5", -16f, 250f); C(fortin, "Caja_F6", 26f, 246f, false); C(fortin, "Caja_F7", 4f, 264f); C(fortin, "Caja_F8", 38f, 240f);
            // Los flancos por fuera de la muralla: cobertura y un alambrado que no tapa el paso.
            C(fortin, "Cob_FlancoO_1", -37f, 232f, false); C(fortin, "Cob_FlancoO_2", -46f, 252f); C(fortin, "Cob_FlancoE_1", 55f, 234f, false); C(fortin, "Cob_FlancoE_2", 55f, 254f);

            // ---------------- 8. REFUGIO DEL REHEN: patio z 274..297, casa del rehen al fondo, entradas oeste / centro / este ----------------
            const string refugio = "8_Refugio";
            B(refugio, "Casa_Refugio", "Casa", 4f, 291f, 14f, 5f, 7f);
            B(refugio, "Galpon_Oeste", "Casa", -32f, 289f, 12f, 4f, 8f);
            B(refugio, "Galpon_Este", "Casa", 42f, 289f, 12f, 4f, 8f);
            B(refugio, "Contenedor_Ref_1", "Contenedor", -16f, 285f, 6f, 2.8f, 2.5f);
            B(refugio, "Contenedor_Ref_2", "Contenedor", 24f, 285f, 6f, 2.8f, 2.5f);
            B(refugio, "Contenedor_Ref_3", "Contenedor", -22f, 294f, 2.5f, 2.8f, 6f);
            B(refugio, "Contenedor_Ref_4", "Contenedor", 30f, 294f, 2.5f, 2.8f, 6f);
            B(refugio, "Torre_Refugio_O", "Torre", -9f, 294f, 4f, 7f, 4f);
            B(refugio, "Torre_Refugio_E", "Torre", 17f, 294f, 4f, 7f, 4f);
            B(refugio, "Muro_Fondo", "Muro", 4f, 296.5f, 100f, 3f, 1.5f);
            MuroX(refugio, "Muro_Refugio_Oeste", -49f, -43f, 274f, 3f, 1.5f);
            MuroX(refugio, "Muro_Refugio_Este", 50f, 57f, 274f, 3f, 1.5f);
            C(refugio, "Cob_Ref_1", -8f, 289f, false); C(refugio, "Cob_Ref_2", 16f, 289f, false);
            C(refugio, "Cob_Ref_3", -26f, 282f); C(refugio, "Cob_Ref_4", 34f, 282f); C(refugio, "Cob_Ref_5", -4f, 281f);
            Erizo(refugio, "Erizo_Ref_O", -36f, 277f); Erizo(refugio, "Erizo_Ref_E", 44f, 278f); Erizo(refugio, "Erizo_Ref_C", 12f, 272f);
        }

        // ---------------------------------------------------------------
        // Enemigos y tanques
        // ---------------------------------------------------------------
        struct Ronda { public string Nombre; public float X, Z; public float MediaX, MediaZ; }

        // Infantes: los primeros quince son los que YA estaban en la escena (se reubican); el resto se crea desde el
        // prefab de enemigo. El centro esta muy guarnecido; los flancos, menos (pero hay quien los vigile).
        static readonly Ronda[] Infantes =
        {
            new Ronda { Nombre = "Enemigo_Patrulla_1", X = -6f,  Z = 172f, MediaX = 6f, MediaZ = 4f },
            new Ronda { Nombre = "Enemigo_Patrulla_2", X = 20f,  Z = 172f, MediaX = 6f, MediaZ = 4f },
            new Ronda { Nombre = "Enemigo_Patrulla_3", X = -24f, Z = 178f, MediaX = 5f, MediaZ = 4f },
            new Ronda { Nombre = "Enemigo_Patrulla_4", X = -12f, Z = 246f, MediaX = 5f, MediaZ = 4f },
            new Ronda { Nombre = "Enemigo_Patrulla_5", X = 18f,  Z = 250f, MediaX = 4f, MediaZ = 4f },
            new Ronda { Nombre = "Enemigo_Puesto_4",   X = 34f,  Z = 176f, MediaX = 5f, MediaZ = 5f },
            new Ronda { Nombre = "Enemigo_Chicane_1",  X = -18f, Z = 203f, MediaX = 8f, MediaZ = 3f },
            new Ronda { Nombre = "Enemigo_Chicane_2",  X = 36f,  Z = 215f, MediaX = 6f, MediaZ = 3f },
            new Ronda { Nombre = "Enemigo_Fortin_1",   X = -6f,  Z = 265f, MediaX = 5f, MediaZ = 3f },
            new Ronda { Nombre = "Enemigo_Fortin_2",   X = 34f,  Z = 265.5f, MediaX = 5f, MediaZ = 3f },
            new Ronda { Nombre = "Enemigo_Fortin_3",   X = 4f,   Z = 232f, MediaX = 6f, MediaZ = 3f },
            new Ronda { Nombre = "Enemigo_Fortin_4",   X = 32f,  Z = 242f, MediaX = 4f, MediaZ = 3f },
            new Ronda { Nombre = "Enemigo_Deposito_1", X = -26f, Z = 279f, MediaX = 5f, MediaZ = 2f },
            new Ronda { Nombre = "Enemigo_Deposito_2", X = 30f,  Z = 279f, MediaX = 5f, MediaZ = 2f },
            new Ronda { Nombre = "Enemigo_Deposito_3", X = 9f,   Z = 281.5f, MediaX = 3f, MediaZ = 1.5f },
            // Sendero del bosque (oeste)
            new Ronda { Nombre = "Enemigo_Sendero_1",  X = -43f, Z = 58f,  MediaX = 3f, MediaZ = 6f },
            new Ronda { Nombre = "Enemigo_Sendero_2",  X = -43f, Z = 140f, MediaX = 3f, MediaZ = 6f },
            new Ronda { Nombre = "Enemigo_Sendero_3",  X = -41f, Z = 236f, MediaX = 3f, MediaZ = 6f },
            // Camino de servicio (este)
            new Ronda { Nombre = "Enemigo_Servicio_1", X = 52f,  Z = 62f,  MediaX = 3f, MediaZ = 6f },
            new Ronda { Nombre = "Enemigo_Servicio_2", X = 52f,  Z = 150f, MediaX = 3f, MediaZ = 6f },
            new Ronda { Nombre = "Enemigo_Servicio_3", X = 52f,  Z = 236f, MediaX = 3f, MediaZ = 6f },
            // Guardias de las entradas del refugio
            new Ronda { Nombre = "Enemigo_Refugio_1",  X = -40f, Z = 280f, MediaX = 3f, MediaZ = 3f },
            new Ronda { Nombre = "Enemigo_Refugio_2",  X = 48f,  Z = 281f, MediaX = 2.5f, MediaZ = 2.5f },
        };

        struct DatosTanque { public string Nombre; public float X, Z, Rot; public Vector2[] Ruta; }

        static readonly DatosTanque[] Tanques =
        {
            new DatosTanque { Nombre = "Tanque_Enemigo_1", X = 30f, Z = 162f, Rot = 180f,
                Ruta = new[] { new Vector2(30f, 162f), new Vector2(-2f, 162f), new Vector2(-2f, 186f), new Vector2(40f, 188f) } },
            new DatosTanque { Nombre = "Tanque_Enemigo_2", X = 14f, Z = 232f, Rot = 180f,
                Ruta = new[] { new Vector2(14f, 232f), new Vector2(-6f, 236f), new Vector2(-6f, 258f), new Vector2(22f, 258f), new Vector2(22f, 238f) } },
            new DatosTanque { Nombre = "Tanque_Enemigo_3", X = 4f, Z = 277f, Rot = 180f,
                Ruta = new[] { new Vector2(-14f, 277f), new Vector2(22f, 277f) } },
        };

        // ---------------------------------------------------------------
        [MenuItem("Strategic Point/Nivel/Construir blockout (terreno + cubos + rutas + NavMesh)")]
        public static void Construir()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "SC_Gameplay")
            {
                Debug.LogWarning("[Blockout] Abri SC_Gameplay antes de construir el nivel (escena activa: " + scene.name + ").");
                return;
            }

            DefinirBloques();
            AlinearSuelo();
            AlinearYPintarTerreno();
            int cubos = ConstruirCubos();
            int enemigos = ColocarEnemigos();
            int tanques = ColocarTanques();
            AjustarAlcancesDeCombate();
            AsegurarAjustesDeEscuadra();
            BakeNavMesh();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Blockout] Nivel listo: {cubos} cubos, {enemigos} soldados enemigos, {tanques} tanques enemigos, terreno pintado y NavMesh horneado.");
        }

        // ---------------------------------------------------------------
        // Suelo
        // ---------------------------------------------------------------
        static void AlinearSuelo()
        {
            var ground = GameObject.Find("Ground");
            if (ground == null) { Debug.LogWarning("[Blockout] No hay 'Ground'."); return; }
            ground.transform.position = new Vector3(TerrainOrigin.x + TerrainSize.x * 0.5f, -0.5f, TerrainOrigin.z + TerrainSize.z * 0.5f);
            ground.transform.localScale = new Vector3(TerrainSize.x, 1f, TerrainSize.z);
        }

        static void AlinearYPintarTerreno()
        {
            var terrain = TerrenoPrincipal();
            if (terrain == null) { Debug.LogWarning("[Blockout] No hay Terrain."); return; }
            var td = terrain.terrainData;

            // Respaldo del terreno original, una sola vez.
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainBackupPath) == null)
                AssetDatabase.CopyAsset(TerrainDataPath, TerrainBackupPath);

            td.size = TerrainSize;
            terrain.transform.position = TerrainOrigin;

            // El terreno ya no es cuadrado (117 x 320): con 512 muestras la
            // pintura quedaba a 0,6 m por pixel en Z. 1024 la deja a 0,3 m.
            td.alphamapResolution = 1024;

            // Cuatro capas: 0 pasto, 1 tierra (las de siempre), 2 grava (caminos y patios), 3 hojarasca (bosque).
            AjustarCapasDeTerreno(td);

            int res = td.alphamapResolution;
            int capas = td.alphamapLayers;
            var mapa = new float[res, res, capas];
            var w = new float[4];
            for (int i = 0; i < res; i++)
            {
                float wz = TerrainOrigin.z + (i + 0.5f) / res * TerrainSize.z;
                for (int j = 0; j < res; j++)
                {
                    float wx = TerrainOrigin.x + (j + 0.5f) / res * TerrainSize.x;
                    PesosDeCapas(wx, wz, w);
                    for (int c = 0; c < capas && c < 4; c++) mapa[i, j, c] = w[c];
                }
            }
            td.SetAlphamaps(0, 0, mapa);

            int hres = td.heightmapResolution;
            td.SetHeights(0, 0, new float[hres, hres]);

            var tc = terrain.GetComponent<TerrainCollider>();
            if (tc != null) { tc.terrainData = null; tc.terrainData = td; }

            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();
        }

        static void AjustarCapasDeTerreno(TerrainData td)
        {
            var layers = td.terrainLayers;
            if (layers != null && layers.Length > 0 && layers[0] != null)
            {
                layers[0].tileSize = new Vector2(15f, 15f);
                layers[0].smoothness = 0.12f;
                EditorUtility.SetDirty(layers[0]);
            }
            if (layers != null && layers.Length > 1 && layers[1] != null)
            {
                layers[1].tileSize = new Vector2(11f, 11f);
                EditorUtility.SetDirty(layers[1]);
            }
            var todas = new TerrainLayer[4];
            todas[0] = layers != null && layers.Length > 0 ? layers[0] : null;
            todas[1] = layers != null && layers.Length > 1 ? layers[1] : null;
            todas[2] = TexturasDeTerreno.Grava();
            todas[3] = TexturasDeTerreno.Hojarasca();
            td.terrainLayers = todas;
        }

        // 0 = pasto, 1 = tierra, 2 = grava, 3 = hojarasca (suman 1). Formas suaves + ruido para que los bordes no sean
        // rectas de regla; cada camino tiene su piso: la carretera y el camino de servicio son de grava, el sendero del
        // bosque de tierra pisada, y el bosque a los costados de hojarasca oscura.
        static void PesosDeCapas(float x, float z, float[] w)
        {
            // Dos octavas: la gruesa da la ondulacion general del borde, la fina le suma mordidas chicas e irregulares.
            float ruidoGrueso = (Mathf.PerlinNoise(x * 0.09f + 40f, z * 0.09f + 90f) - 0.5f) * 3.4f;
            float ruidoFino = (Mathf.PerlinNoise(x * 0.35f + 500f, z * 0.35f + 700f) - 0.5f) * 1.1f;
            float ruido = ruidoGrueso + ruidoFino;
            var p = new Vector2(x, z);
            const float borde = 2.6f;

            float Mezcla(float d) => Mathf.Clamp01(1f - Mathf.SmoothStep(0f, 1f, (d + ruido) / borde + 0.5f));

            // ---- grava: carretera, camino de servicio, base, plaza, patios ----
            float dGrava = float.MaxValue;
            dGrava = Mathf.Min(dGrava, RutasDelNivel.DistanciaABorde(x, z, TipoDeRuta.Carretera));
            dGrava = Mathf.Min(dGrava, RutasDelNivel.DistanciaABorde(x, z, TipoDeRuta.Servicio) + 0.8f);
            dGrava = Mathf.Min(dGrava, DistRect(x, z, -54.2f, -22.3f, 62.6f, 4f));                      // patio de la base
            dGrava = Mathf.Min(dGrava, Vector2.Distance(p, new Vector2(4f, 119f)) - 13f);               // plaza de la aldea
            dGrava = Mathf.Min(dGrava, DistRect(x, z, -30f, 224f, 46f, 270f));                          // patio del fortin
            dGrava = Mathf.Min(dGrava, DistRect(x, z, -40f, 275f, 54f, 287f));                          // patio del refugio
            dGrava = Mathf.Min(dGrava, DistRect(x, z, -2f, 214f, 14f, 226f));                           // porton sur del fortin
            float grava = Mezcla(dGrava);

            // ---- tierra: sendero del bosque, conectores, hombros de los caminos, puesto avanzado, campamentos ----
            float dTierra = float.MaxValue;
            dTierra = Mathf.Min(dTierra, RutasDelNivel.DistanciaABorde(x, z, TipoDeRuta.Bosque));
            dTierra = Mathf.Min(dTierra, RutasDelNivel.DistanciaABorde(x, z, TipoDeRuta.Conector));
            dTierra = Mathf.Min(dTierra, RutasDelNivel.DistanciaABorde(x, z, TipoDeRuta.Carretera) - 2.2f);   // hombro de la carretera
            dTierra = Mathf.Min(dTierra, RutasDelNivel.DistanciaABorde(x, z, TipoDeRuta.Servicio) - 1.6f);
            dTierra = Mathf.Min(dTierra, Vector2.Distance(p, new Vector2(10f, 167f)) - 20f);            // puesto avanzado
            dTierra = Mathf.Min(dTierra, Vector2.Distance(p, new Vector2(4f, 119f)) - 17f);             // borde de la plaza
            dTierra = Mathf.Min(dTierra, DistRect(x, z, -50f, 194f, 60f, 222f) + 12f);                  // mancha de la chicane
            dTierra = Mathf.Min(dTierra, DistRect(x, z, -36f, 3f, -28f, 11f) - 3f);                     // alrededor de la casilla
            foreach (var c in Campamentos) dTierra = Mathf.Min(dTierra, Vector2.Distance(p, c) - 4.5f);
            float tierra = Mezcla(dTierra);

            // ---- hojarasca: el bosque de los flancos (fuera de los caminos) ----
            float bosqueO = Mathf.Clamp01((-30f - x) / 7f);
            float bosqueE = Mathf.Clamp01((x - 44f) / 7f);
            float zonaBosque = Mathf.Max(bosqueO, bosqueE);
            // La aldea y el fortin no tienen bosque encima: se apaga con la distancia a los patios.
            float enBase = Mathf.Clamp01((z - 12f) / 10f);
            float manchas = Mathf.PerlinNoise(x * 0.06f + 300f, z * 0.06f + 800f);
            float hojarasca = zonaBosque * enBase * Mathf.Lerp(0.55f, 1f, manchas);

            // Manchones de tierra sueltos y sutiles en el pasto abierto (un campo 100% parejo se lee como una alfombra).
            float manchaAncha = Mathf.PerlinNoise(x * 0.05f + 1000f, z * 0.05f + 2000f);
            float manchaFina = Mathf.PerlinNoise(x * 0.22f + 3000f, z * 0.22f + 4000f);
            float desgaste = Mathf.Clamp01((manchaAncha - 0.62f) * 6f) * Mathf.Clamp01((manchaFina - 0.4f) * 3f) * 0.35f;

            // Prioridad: grava > tierra > hojarasca > desgaste > pasto.
            float wg = grava;
            float wt = Mathf.Min(tierra, 1f - wg);
            float wh = Mathf.Min(hojarasca, 1f - wg - wt);
            float wd = Mathf.Min(desgaste, 1f - wg - wt - wh);
            w[2] = wg;
            w[1] = wt + wd;
            w[3] = wh;
            w[0] = Mathf.Max(0f, 1f - w[1] - w[2] - w[3]);
        }

        // Campamentos con fogata (claros de tierra pisada); las fogatas las pone AmbientacionDelNivel en estos mismos puntos.
        public static readonly Vector2[] Campamentos =
        {
            new Vector2(-36f, 148f), new Vector2(-40f, 232f), new Vector2(47f, 92f), new Vector2(48f, 232f), new Vector2(-8f, 63f),
        };

        // Distancia (negativa adentro) de un punto a un rectangulo XZ.
        static float DistRect(float x, float z, float x0, float z0, float x1, float z1)
        {
            float dx = Mathf.Max(x0 - x, 0f, x - x1);
            float dz = Mathf.Max(z0 - z, 0f, z - z1);
            float fuera = Mathf.Sqrt(dx * dx + dz * dz);
            if (fuera > 0f) return fuera;
            float adentro = Mathf.Min(Mathf.Min(x - x0, x1 - x), Mathf.Min(z - z0, z1 - z));
            return -adentro;
        }

        // ---------------------------------------------------------------
        // Cubos
        // ---------------------------------------------------------------
        internal static Material MaterialDe(string clave)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "Blocking");

            string path = $"{MaterialFolder}/M_Blocking_{clave}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Color color;
            switch (clave)
            {
                case "Muro": color = new Color(0.52f, 0.53f, 0.56f); break;
                case "Casa": color = new Color(0.74f, 0.63f, 0.48f); break;
                case "Torre": color = new Color(0.42f, 0.38f, 0.38f); break;
                case "Contenedor": color = new Color(0.45f, 0.30f, 0.22f); break;
                case "Brecha": color = new Color(0.62f, 0.45f, 0.40f); break;   // muro debil: el tanque lo rompe
                default: color = new Color(0.36f, 0.45f, 0.30f); break; // Cobertura
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader) { name = "M_Blocking_" + clave };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static int ConstruirCubos()
        {
            var previo = GameObject.Find(RootName);
            if (previo != null) Object.DestroyImmediate(previo);

            var root = new GameObject(RootName).transform;
            var grupos = new Dictionary<string, Transform>();
            int n = 0;
            foreach (var b in Bloques)
            {
                if (!grupos.TryGetValue(b.Grupo, out var g))
                {
                    g = new GameObject(b.Grupo).transform;
                    g.SetParent(root, false);
                    grupos[b.Grupo] = g;
                }

                var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubo.name = b.Nombre;
                cubo.transform.SetParent(g, false);
                cubo.transform.position = new Vector3(b.X, TerrainOrigin.y + b.Alto * 0.5f, b.Z);
                cubo.transform.localScale = new Vector3(b.Ancho, b.Alto, b.Fondo);
                cubo.GetComponent<MeshRenderer>().sharedMaterial = MaterialDe(b.Material);

                var marca = cubo.AddComponent<ObstacleMarker>();
                var so = new SerializedObject(marca);
                so.FindProperty("maxHealth").intValue = b.Vida;
                so.ApplyModifiedPropertiesWithoutUndo();
                n++;
            }
            AssetDatabase.SaveAssets();
            return n;
        }

        // ---------------------------------------------------------------
        // Enemigos: cada uno ronda CERCA de donde nace
        // ---------------------------------------------------------------
        static Transform Raiz(string nombre)
        {
            var g = GameObject.Find(nombre);
            if (g == null) g = new GameObject(nombre);
            return g.transform;
        }

        static Vector3[] RectanguloDeRonda(Ronda r)
        {
            return new[]
            {
                new Vector3(r.X - r.MediaX, 0f, r.Z - r.MediaZ), new Vector3(r.X + r.MediaX, 0f, r.Z - r.MediaZ),
                new Vector3(r.X + r.MediaX, 0f, r.Z + r.MediaZ), new Vector3(r.X - r.MediaX, 0f, r.Z + r.MediaZ),
            };
        }

        static GameObject EnemigoNuevo(string nombre, Transform padre)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabEnemigo);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
            go.name = nombre;
            return go;
        }

        static int ColocarEnemigos()
        {
            var enemigos = Raiz("Enemies");
            var rootRutas = Raiz("Waypoints");
            int n = 0;
            foreach (var r in Infantes)
            {
                var go = GameObject.Find(r.Nombre);
                if (go == null) go = EnemigoNuevo(r.Nombre, enemigos);
                go.transform.position = new Vector3(r.X, 0.8f, r.Z);
                go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                var brain = go.GetComponent<AiBrain>();
                if (brain == null) continue;

                string nombre = "PatrolRoute_" + r.Nombre.Replace("Enemigo_", "");
                var previa = GameObject.Find(nombre);
                if (previa != null) Object.DestroyImmediate(previa);
                var linea = PatrolRouteLine.Spawn(RectanguloDeRonda(r), new Color(0.95f, 0.6f, 0.2f));
                linea.gameObject.name = nombre;
                linea.transform.SetParent(rootRutas, true);
                brain.SetPatrolWaypoints(linea.Markers);
                EditorUtility.SetDirty(brain);
                n++;
            }

            // La ronda vieja (esferas sueltas del nivel chico) ya no la usa
            // nadie: se borra para no dejar esferas flotando en la aldea.
            var vieja = GameObject.Find("PatrolRoute");
            if (vieja != null && vieja.transform.parent == rootRutas) Object.DestroyImmediate(vieja);
            return n;
        }

        static int ColocarTanques()
        {
            var prefabTanque = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabTanque);
            var padreVehiculos = Raiz("Vehicles");
            var padreEnemigos = Raiz("Enemies");
            int n = 0;
            foreach (var t in Tanques)
            {
                var previo = GameObject.Find(t.Nombre);
                if (previo != null) Object.DestroyImmediate(previo);
                foreach (var suf in new[] { "_Conductor", "_Artillero" })
                {
                    var c = GameObject.Find(t.Nombre + suf);
                    if (c != null) Object.DestroyImmediate(c);
                }

                var tanque = (GameObject)PrefabUtility.InstantiatePrefab(prefabTanque, padreVehiculos);
                tanque.name = t.Nombre;
                tanque.transform.SetPositionAndRotation(new Vector3(t.X, 0.6f, t.Z), Quaternion.Euler(0f, t.Rot, 0f));

                var conductor = EnemigoNuevo(t.Nombre + "_Conductor", padreEnemigos);
                var artillero = EnemigoNuevo(t.Nombre + "_Artillero", padreEnemigos);
                conductor.transform.position = tanque.transform.position;
                artillero.transform.position = tanque.transform.position;
                // Sin ronda propia: viven adentro del tanque y salen si lo destruyen.
                conductor.GetComponent<AiBrain>().SetPatrolWaypoints(null);
                artillero.GetComponent<AiBrain>().SetPatrolWaypoints(null);

                var v = tanque.GetComponent<Vehicle>();
                v.ConfigurarTripulacion(
                    new[] { conductor.GetComponent<Soldier>(), artillero.GetComponent<Soldier>() },
                    new[] { VehicleSeatRole.Driver, VehicleSeatRole.Passenger2 }, true);   // sin "Gunner": si no, TurretAI cree que hay un artillero humano y no dispara
                var so = new SerializedObject(v);
                so.FindProperty("maxHealth").intValue = 360;
                so.ApplyModifiedPropertiesWithoutUndo();

                var ruta = new Vector3[t.Ruta.Length];
                for (int i = 0; i < ruta.Length; i++) ruta[i] = new Vector3(t.Ruta[i].x, 0.6f, t.Ruta[i].y);
                tanque.GetComponent<VehicleBrain>().ConfigurarPatrulla(ruta, true, 0.5f);

                PrefabUtility.RecordPrefabInstancePropertyModifications(v);
                PrefabUtility.RecordPrefabInstancePropertyModifications(tanque.GetComponent<VehicleBrain>());
                EditorUtility.SetDirty(v);
                n++;
            }
            return n;
        }

        // El mapa es 4 veces mas grande: con los alcances originales (vision
        // 10, tiro 6) el combate era "cara a cara". Enemigos: ven a 22 y
        // disparan a 13; aliados: 20 y 12.
        internal static void AjustarAlcancesDeCombate()
        {
            foreach (var s in Object.FindObjectsByType<Soldier>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var ai = s.GetComponent<AiBrain>();
                if (ai == null) continue;
                var so = new SerializedObject(ai);
                bool enemigo = s.Team == SP.Combat.TeamId.Enemy;
                so.FindProperty("visionRange").floatValue = enemigo ? 22f : 20f;
                so.FindProperty("attackRange").floatValue = enemigo ? 13f : 12f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ai);
            }
        }

        static void AsegurarAjustesDeEscuadra()
        {
            var a = AjustesDeEscuadra.AsegurarEnEscena();
            var sistemas = GameObject.Find("Systems");
            if (sistemas != null && a.transform.parent != sistemas.transform) a.transform.SetParent(sistemas.transform, false);
            EditorUtility.SetDirty(a);
        }

        // ---------------------------------------------------------------
        // NavMesh
        // ---------------------------------------------------------------
        // Recast solo rasteriza las CARAS de una malla, no su volumen: bajo un
        // cubo alto (un muro, una casa) el piso queda "caminable" en el
        // centro. Un NavMeshModifierVolume "No caminable" del tamaño del cubo
        // lo vuelve solido de verdad.
        const int AreaNoCaminable = 1;

        internal static void AgregarVolumenNoCaminable(GameObject cubo)
        {
            if (cubo.GetComponent<Unity.AI.Navigation.NavMeshModifierVolume>() != null) return;
            var vol = cubo.AddComponent<Unity.AI.Navigation.NavMeshModifierVolume>();
            vol.center = new Vector3(0f, -0.03f, 0f);
            vol.size = new Vector3(1.02f, 1.1f, 1.02f);
            vol.area = AreaNoCaminable;
        }

        internal static void BakeNavMesh()
        {
            var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>(FindObjectsInactive.Include);
            if (surface == null) { Debug.LogWarning("[Blockout] No hay NavMeshSurface."); return; }

            int volumenes = 0;
            foreach (var c in Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (c.isTrigger || c.name == "Ground") continue;
                if (c.GetComponentInParent<Soldier>() != null) continue;
                if (c.GetComponentInParent<Vehicle>() != null) continue;
                if (c.GetComponentInParent<SP.Combat.Projectile>() != null) continue;
                if (c.bounds.size.y < 0.8f) continue;
                if (c.GetComponent<Unity.AI.Navigation.NavMeshModifierVolume>() == null) volumenes++;
                AgregarVolumenNoCaminable(c.gameObject);
            }

            foreach (var v in Object.FindObjectsByType<Vehicle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mod = v.GetComponent<Unity.AI.Navigation.NavMeshModifier>();
                if (mod == null) mod = v.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
                mod.ignoreFromBuild = true;
                mod.applyToChildren = true;
            }

            Physics.SyncTransforms();
            surface.BuildNavMesh();
            EditorUtility.SetDirty(surface);
            Debug.Log($"[Blockout] NavMesh horneado ({volumenes} volumenes nuevos).");
        }
    }
}
