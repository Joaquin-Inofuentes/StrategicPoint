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
    // el "suelo" es pintura de las dos capas del terreno (pasto / tierra).
    //
    // NIVEL 4 VECES MAS GRANDE que la primera version (116,8 x 320 m contra
    // 58,4 x 160 m: el doble por lado = 4x el area), en ocho bloques que se
    // recorren de sur a norte. Los tanques enemigos estan en los bloques 5, 7
    // y 8; el cañon del propio tanque derriba coberturas y las "brechas" de la
    // muralla (las explosiones dañan obstaculos).
    //
    // Es IDEMPOTENTE: cada corrida borra "Nivel_Blockout" y lo rearma, asi que
    // se puede retocar la tabla de abajo y volver a correr desde
    //   Strategic Point > Nivel > Construir blockout
    //
    //   1. Base ................ z -22 ..  14   patio de partida (escuadra + tanque propio)
    //   2. Campo de tiro ....... z  16 ..  64   coberturas bajas destructibles
    //   3. Paso del cañon ...... z  68 ..  88   dos muros largos y un hueco de 18 m
    //   4. Aldea ............... z  90 .. 142   casas, calle central y plaza
    //   5. Puesto avanzado ..... z 148 .. 190   TANQUE ENEMIGO 1 + 4 soldados
    //   6. Chicane ............. z 194 .. 220   muros en S (el tanque gira, la infanteria rodea)
    //   7. Fortin .............. z 224 .. 270   TANQUE ENEMIGO 2 + 6 soldados, porton y brechas
    //   8. Deposito final ...... z 274 .. 298   TANQUE ENEMIGO 3 + 3 soldados
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

        static void DefinirBloques()
        {
            Bloques.Clear();

            // ---------------- 1. BASE ----------------
            const string base1 = "1_Base";
            B(base1, "Base_Muro_Oeste", "Muro", -52f, -4f, 1.5f, 3f, 36f);
            B(base1, "Base_Muro_Este", "Muro", 60f, -4f, 1.5f, 3f, 36f);
            B(base1, "Base_Muro_Sur", "Muro", 4f, -21.5f, 111f, 3f, 1.5f);
            B(base1, "Casilla_Base", "Casa", -40f, 4f, 8f, 4f, 6f);
            C(base1, "Cobertura_Base_1", -24f, 9f);
            C(base1, "Cobertura_Base_2", -8f, 11f);
            C(base1, "Cobertura_Base_3", 20f, 10f);
            C(base1, "Cobertura_Base_4", 38f, 8f, false);

            // ---------------- 2. CAMPO DE TIRO ----------------
            const string campo = "2_CampoDeTiro";
            C(campo, "Cob_A1", -44f, 22f); C(campo, "Cob_A2", -24f, 24f, false); C(campo, "Cob_A3", 18f, 22f); C(campo, "Cob_A4", 34f, 24f); C(campo, "Cob_A5", 52f, 22f, false);
            C(campo, "Cob_B1", -34f, 32f); C(campo, "Cob_B2", -12f, 34f, false); C(campo, "Cob_B3", 24f, 33f, false); C(campo, "Cob_B4", 42f, 32f); C(campo, "Cob_B5", 56f, 34f);
            C(campo, "Cob_C1", -46f, 42f, false); C(campo, "Cob_C2", -20f, 43f); C(campo, "Cob_C3", 30f, 44f); C(campo, "Cob_C4", 52f, 42f, false);
            C(campo, "Cob_D1", -30f, 54f, false); C(campo, "Cob_D2", -8f, 55f); C(campo, "Cob_D3", 18f, 54f); C(campo, "Cob_D4", 44f, 56f, false);
            B(campo, "Ruina_Campo_1", "Casa", -38f, 38f, 4f, 3f, 4f);
            B(campo, "Ruina_Campo_2", "Casa", 46f, 48f, 4f, 3f, 4f);

            // ---------------- 3. PASO DEL CAÑON: hueco central x -6..12 ----------------
            const string canon = "3_PasoDelCanon";
            B(canon, "Muro_Canon_Oeste", "Muro", -30.1f, 72f, 48.2f, 4f, 3f);
            B(canon, "Muro_Canon_Este", "Muro", 37.3f, 72f, 50.6f, 4f, 3f);
            B(canon, "Bunker_Canon_Oeste", "Cobertura", -9f, 79f, 5f, 2f, 3f, 700);
            B(canon, "Bunker_Canon_Este", "Cobertura", 15f, 79f, 5f, 2f, 3f, 700);
            B(canon, "Torre_Vigia_Oeste", "Torre", -22f, 79f, 3f, 5f, 3f);
            B(canon, "Torre_Vigia_Este", "Torre", 30f, 79f, 3f, 5f, 3f);
            C(canon, "Sacos_Canon_1", -36f, 82f); C(canon, "Sacos_Canon_2", 46f, 82f);

            // ---------------- 4. ALDEA: calle libre x -14..20 ----------------
            const string aldea = "4_Aldea";
            B(aldea, "Casa_O1", "Casa", -42f, 98f, 9f, 5f, 7f);
            B(aldea, "Casa_O2", "Casa", -28f, 96f, 8f, 5f, 7f);
            B(aldea, "Casa_O3", "Casa", -40f, 114f, 8f, 5f, 8f);
            B(aldea, "Casa_O4", "Casa", -24f, 116f, 9f, 5f, 7f);
            B(aldea, "Casa_O5", "Casa", -38f, 132f, 8f, 5f, 7f);
            B(aldea, "Casa_O6", "Casa", -22f, 134f, 8f, 5f, 6f);
            B(aldea, "Casa_E1", "Casa", 28f, 98f, 9f, 5f, 7f);
            B(aldea, "Casa_E2", "Casa", 44f, 96f, 8f, 5f, 7f);
            B(aldea, "Casa_E3", "Casa", 30f, 116f, 9f, 5f, 7f);
            B(aldea, "Casa_E4", "Casa", 46f, 114f, 8f, 5f, 8f);
            B(aldea, "Casa_E5", "Casa", 28f, 134f, 8f, 5f, 7f);
            B(aldea, "Casa_E6", "Casa", 46f, 132f, 8f, 5f, 7f);
            B(aldea, "Pozo", "Cobertura", 4f, 115f, 2f, 1f, 2f, 400);
            C(aldea, "Sacos_Aldea_1", -8f, 104f, false, 400);
            C(aldea, "Sacos_Aldea_2", 16f, 126f, true, 400);
            C(aldea, "Barricada_Aldea", -6f, 124f, false);
            B(aldea, "Carro_Aldea", "Cobertura", 10f, 106f, 3f, 1.4f, 1.6f, 300);
            C(aldea, "Cob_Callejon_1", -10f, 130f); C(aldea, "Cob_Callejon_2", 14f, 100f);

            // ---------------- 5. PUESTO AVANZADO ENEMIGO ----------------
            const string puesto = "5_PuestoAvanzado";
            C(puesto, "Sacos_Puesto_1", -16f, 156f, true, 400); C(puesto, "Sacos_Puesto_2", 24f, 156f, true, 400);
            B(puesto, "Torre_Puesto", "Torre", -30f, 168f, 4f, 6f, 4f);
            B(puesto, "Muro_Puesto_Oeste", "Muro", -40f, 172f, 1.5f, 3f, 20f);
            B(puesto, "Muro_Puesto_Este", "Muro", 50f, 172f, 1.5f, 3f, 20f);
            B(puesto, "Bunker_Puesto", "Cobertura", 4f, 178f, 6f, 2f, 2.5f, 600);
            C(puesto, "Caja_P1", -8f, 170f); C(puesto, "Caja_P2", 18f, 166f); C(puesto, "Caja_P3", 32f, 182f, false); C(puesto, "Caja_P4", -22f, 184f); C(puesto, "Caja_P5", 8f, 186f);

            // ---------------- 6. CHICANE ----------------
            const string chicane = "6_Chicane";
            B(chicane, "Muro_Chicane_1", "Muro", -20f, 198f, 68.4f, 4f, 2.5f);   // x -54,2 .. 14,2  (hueco al este)
            B(chicane, "Muro_Chicane_2", "Muro", 26.3f, 210f, 72.6f, 4f, 2.5f);  // x -10 .. 62,6   (hueco al oeste)
            C(chicane, "Cob_Chi_1", 30f, 203f); C(chicane, "Cob_Chi_2", -30f, 204f); C(chicane, "Cob_Chi_3", 0f, 216f, false); C(chicane, "Cob_Chi_4", 48f, 216f);

            // ---------------- 7. FORTIN: x -30..46, z 224..270, porton x -2..14 ----------------
            const string fortin = "7_Fortin";
            B(fortin, "Muralla_Sur_Oeste", "Muro", -16f, 224f, 28f, 3.5f, 1.5f);
            B(fortin, "Muralla_Sur_Este", "Muro", 30f, 224f, 32f, 3.5f, 1.5f);
            B(fortin, "Muralla_Oeste_A", "Muro", -30f, 232f, 1.5f, 3.5f, 16f);
            B(fortin, "Brecha_Oeste", "Brecha", -30f, 244f, 1.5f, 3.5f, 8f, 700);
            B(fortin, "Muralla_Oeste_B", "Muro", -30f, 259f, 1.5f, 3.5f, 22f);
            B(fortin, "Muralla_Este_A", "Muro", 46f, 232f, 1.5f, 3.5f, 16f);
            B(fortin, "Brecha_Este", "Brecha", 46f, 244f, 1.5f, 3.5f, 8f, 700);
            B(fortin, "Muralla_Este_B", "Muro", 46f, 259f, 1.5f, 3.5f, 22f);
            B(fortin, "Muralla_Norte_A", "Muro", -18f, 270f, 24f, 3.5f, 1.5f);
            B(fortin, "Brecha_Norte", "Brecha", -2f, 270f, 8f, 3.5f, 1.5f, 700);
            B(fortin, "Muralla_Norte_B", "Muro", 24f, 270f, 44f, 3.5f, 1.5f);
            B(fortin, "Torre_Fortin", "Torre", 8f, 249f, 4f, 7f, 4f);
            B(fortin, "Cuartel_O", "Casa", -18f, 258f, 10f, 4f, 8f);
            B(fortin, "Cuartel_E", "Casa", 32f, 258f, 10f, 4f, 8f);
            C(fortin, "Caja_F1", -20f, 236f); C(fortin, "Caja_F2", 28f, 236f); C(fortin, "Caja_F3", -4f, 240f, false); C(fortin, "Caja_F4", 16f, 262f);
            C(fortin, "Caja_F5", -10f, 250f); C(fortin, "Caja_F6", 22f, 248f, false); C(fortin, "Caja_F7", 4f, 264f); C(fortin, "Caja_F8", 38f, 240f);

            // ---------------- 8. DEPOSITO FINAL ----------------
            const string deposito = "8_Deposito";
            B(deposito, "Galpon_Oeste", "Casa", -34f, 284f, 12f, 4f, 8f);
            B(deposito, "Galpon_Este", "Casa", 42f, 286f, 12f, 4f, 8f);
            B(deposito, "Contenedor_1", "Torre", -12f, 291f, 6f, 3f, 2.5f);
            B(deposito, "Contenedor_2", "Torre", 24f, 291f, 6f, 3f, 2.5f);
            B(deposito, "Muro_Fondo", "Muro", 4f, 296.5f, 100f, 3f, 1.5f);
            C(deposito, "Cob_Dep_1", -6f, 280f); C(deposito, "Cob_Dep_2", 16f, 281f); C(deposito, "Cob_Dep_3", -22f, 290f, false); C(deposito, "Cob_Dep_4", 34f, 278f, false);
        }

        // ---------------------------------------------------------------
        // Enemigos y tanques
        // ---------------------------------------------------------------
        struct Ronda { public string Nombre; public float X, Z; public float MediaX, MediaZ; }

        // Infantes: los cinco primeros son los que YA estaban en la escena
        // (se reubican); el resto se crea desde el prefab de enemigo.
        static readonly Ronda[] Infantes =
        {
            new Ronda { Nombre = "Enemigo_Patrulla_1", X = -6f,  Z = 172f, MediaX = 6f, MediaZ = 4f },
            new Ronda { Nombre = "Enemigo_Patrulla_2", X = 20f,  Z = 172f, MediaX = 6f, MediaZ = 4f },
            new Ronda { Nombre = "Enemigo_Patrulla_3", X = -24f, Z = 178f, MediaX = 5f, MediaZ = 4f },
            new Ronda { Nombre = "Enemigo_Patrulla_4", X = -10f, Z = 244f, MediaX = 6f, MediaZ = 4f },
            new Ronda { Nombre = "Enemigo_Patrulla_5", X = 22f,  Z = 252f, MediaX = 6f, MediaZ = 4f },
            new Ronda { Nombre = "Enemigo_Puesto_4",   X = 36f,  Z = 176f, MediaX = 5f, MediaZ = 5f },
            new Ronda { Nombre = "Enemigo_Chicane_1",  X = -22f, Z = 203f, MediaX = 8f, MediaZ = 3f },
            new Ronda { Nombre = "Enemigo_Chicane_2",  X = 40f,  Z = 214f, MediaX = 8f, MediaZ = 3f },
            new Ronda { Nombre = "Enemigo_Fortin_1",   X = -20f, Z = 262f, MediaX = 5f, MediaZ = 4f },
            new Ronda { Nombre = "Enemigo_Fortin_2",   X = 34f,  Z = 264f, MediaX = 5f, MediaZ = 4f },
            new Ronda { Nombre = "Enemigo_Fortin_3",   X = 4f,   Z = 232f, MediaX = 6f, MediaZ = 3f },
            new Ronda { Nombre = "Enemigo_Fortin_4",   X = 34f,  Z = 240f, MediaX = 5f, MediaZ = 4f },
            new Ronda { Nombre = "Enemigo_Deposito_1", X = -22f, Z = 284f, MediaX = 6f, MediaZ = 3f },
            new Ronda { Nombre = "Enemigo_Deposito_2", X = 32f,  Z = 283f, MediaX = 6f, MediaZ = 3f },
            new Ronda { Nombre = "Enemigo_Deposito_3", X = 4f,   Z = 279f, MediaX = 6f, MediaZ = 2f },
        };

        struct DatosTanque { public string Nombre; public float X, Z, Rot; public Vector2[] Ruta; }

        static readonly DatosTanque[] Tanques =
        {
            new DatosTanque { Nombre = "Tanque_Enemigo_1", X = 30f, Z = 162f, Rot = 180f,
                Ruta = new[] { new Vector2(30f, 162f), new Vector2(-2f, 162f), new Vector2(-2f, 186f), new Vector2(40f, 188f) } },
            new DatosTanque { Nombre = "Tanque_Enemigo_2", X = 10f, Z = 240f, Rot = 180f,
                Ruta = new[] { new Vector2(14f, 238f), new Vector2(-14f, 240f), new Vector2(-14f, 256f), new Vector2(30f, 246f) } },
            new DatosTanque { Nombre = "Tanque_Enemigo_3", X = 4f, Z = 288f, Rot = 180f,
                Ruta = new[] { new Vector2(-20f, 284f), new Vector2(28f, 284f) } },
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
            Debug.Log($"[Blockout] Nivel 4x listo: {cubos} cubos, {enemigos} soldados enemigos, {tanques} tanques enemigos, terreno pintado y NavMesh horneado.");
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
            var terrain = Object.FindFirstObjectByType<Terrain>();
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

            int res = td.alphamapResolution;
            int capas = td.alphamapLayers;
            var mapa = new float[res, res, capas];
            for (int i = 0; i < res; i++)
            {
                float wz = TerrainOrigin.z + (i + 0.5f) / res * TerrainSize.z;
                for (int j = 0; j < res; j++)
                {
                    float wx = TerrainOrigin.x + (j + 0.5f) / res * TerrainSize.x;
                    float tierra = PesoDeTierra(wx, wz);
                    mapa[i, j, 0] = 1f - tierra;                 // GrassLayer
                    if (capas > 1) mapa[i, j, 1] = tierra;       // DirtLayer
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

        // 0 = pasto, 1 = tierra. Formas suaves + ruido para que los bordes no
        // sean rectas de regla.
        static readonly Vector2[] CaminoPrincipal =
        {
            new Vector2(4f, -10f), new Vector2(4f, 20f), new Vector2(-8f, 48f), new Vector2(4f, 74f), new Vector2(4f, 100f),
            new Vector2(2f, 140f), new Vector2(10f, 166f), new Vector2(4f, 200f), new Vector2(4f, 226f), new Vector2(6f, 262f),
            new Vector2(4f, 284f),
        };

        static float PesoDeTierra(float x, float z)
        {
            float ruido = (Mathf.PerlinNoise(x * 0.09f + 40f, z * 0.09f + 90f) - 0.5f) * 3.4f;
            float d = float.MaxValue;
            var p = new Vector2(x, z);

            // Base
            d = Mathf.Min(d, DistRect(x, z, -54.2f, -22.3f, 62.6f, 8f));
            // Camino principal, 10 m de ancho
            for (int k = 0; k < CaminoPrincipal.Length - 1; k++)
                d = Mathf.Min(d, DistSegmento(p, CaminoPrincipal[k], CaminoPrincipal[k + 1]) - 5f);
            // Calle transversal de la aldea y sendas del campo de tiro
            d = Mathf.Min(d, DistSegmento(p, new Vector2(-46f, 115f), new Vector2(52f, 115f)) - 3f);
            d = Mathf.Min(d, DistSegmento(p, new Vector2(-40f, 40f), new Vector2(50f, 30f)) - 2f);
            d = Mathf.Min(d, DistSegmento(p, new Vector2(-30f, 22f), new Vector2(0f, 60f)) - 1.5f);
            // Plazas y patios
            d = Mathf.Min(d, DistRect(x, z, -6f, 66f, 14f, 90f));
            d = Mathf.Min(d, Vector2.Distance(p, new Vector2(4f, 115f)) - 14f);
            d = Mathf.Min(d, Vector2.Distance(p, new Vector2(10f, 167f)) - 22f);
            d = Mathf.Min(d, DistRect(x, z, -30f, 224f, 46f, 270f));
            d = Mathf.Min(d, Vector2.Distance(p, new Vector2(10f, 285f)) - 17f);
            d = Mathf.Min(d, DistRect(x, z, -50f, 194f, 60f, 222f) + 10f);   // mancha en la chicane
            // Manchones sueltos
            d = Mathf.Min(d, Vector2.Distance(p, new Vector2(-34f, 30f)) - 6f);
            d = Mathf.Min(d, Vector2.Distance(p, new Vector2(40f, 46f)) - 5f);
            d = Mathf.Min(d, Vector2.Distance(p, new Vector2(-36f, 150f)) - 7f);
            d = Mathf.Min(d, Vector2.Distance(p, new Vector2(48f, 150f)) - 6f);

            float borde = 2.6f;
            return Mathf.Clamp01(1f - Mathf.SmoothStep(0f, 1f, (d + ruido) / borde + 0.5f));
        }

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

        static float DistSegmento(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }

        // ---------------------------------------------------------------
        // Cubos
        // ---------------------------------------------------------------
        static Material MaterialDe(string clave)
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
        static void AjustarAlcancesDeCombate()
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

        static void AgregarVolumenNoCaminable(GameObject cubo)
        {
            if (cubo.GetComponent<Unity.AI.Navigation.NavMeshModifierVolume>() != null) return;
            var vol = cubo.AddComponent<Unity.AI.Navigation.NavMeshModifierVolume>();
            vol.center = new Vector3(0f, -0.03f, 0f);
            vol.size = new Vector3(1.02f, 1.1f, 1.02f);
            vol.area = AreaNoCaminable;
        }

        static void BakeNavMesh()
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
