using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Presentation;

namespace SP.EditorTools
{
    // "Blockout" del nivel de SC_Gameplay: SOLO pintura de terreno + cubos.
    //
    // Pedido: colocar el blocking usando lo pintado del terreno, y pintar un
    // nivel interesante donde se use. Todo el bloqueo es un cubo (BoxCollider
    // + ObstacleMarker, asi entra al minimapa, a las coberturas y a la
    // navegacion sin tocar nada mas); todo el "suelo" es pintura de las dos
    // capas del terreno (pasto / tierra).
    //
    // Es IDEMPOTENTE: cada corrida borra "Nivel_Blockout" y lo rearma, asi
    // que se puede retocar la tabla de abajo y volver a correr desde
    //   Strategic Point > Nivel > Construir blockout
    //
    // Mapa (x de -24,8 a 33,6, z de -22,3 a 137,7; la escuadra sale al sur):
    //   1. Patio de partida ....... z -22 ..   8   tierra, obstaculos ya existentes
    //   2. Campo de tiro .......... z  12 ..  40   pasto + coberturas bajas
    //   3. Paso del canion ........ z  46 ..  54   dos muros y un hueco de 12 m
    //   4. Aldea .................. z  60 ..  78   casas a los costados de la calle
    //   5. Fortin enemigo ......... z  80 .. 112   muralla con porton, torre y cajas
    //   6. Deposito ............... z 116 .. 136   galpones al fondo
    // Los huecos miden 12 m o mas: pasa el blindado.
    public static class LevelBlockoutBuilder
    {
        const string RootName = "Nivel_Blockout";

        // Limites del mundo jugable: los del cubo "Ground" (Environment/Ground).
        static readonly Vector3 TerrainOrigin = new Vector3(-24.8f, 0f, -22.3f);
        static readonly Vector3 TerrainSize = new Vector3(58.4f, 30f, 160f);

        const string TerrainDataPath = "Assets/_Project/Terrains/TerrainData_Main.asset";
        const string TerrainBackupPath = "Assets/_Project/Terrains/TerrainData_Main_ANTES_del_blockout.asset";
        const string MaterialFolder = "Assets/_Project/Materials/Blocking";

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

        static void DefinirBloques()
        {
            Bloques.Clear();

            // --- 2. Campo de tiro: coberturas bajas, destructibles ---
            const string campo = "2_CampoDeTiro";
            B(campo, "Cobertura_1", "Cobertura", -12f, 16f, 3f, 1.2f, 1f, 300);
            B(campo, "Cobertura_2", "Cobertura", 10f, 20f, 3f, 1.2f, 1f, 300);
            B(campo, "Cobertura_3", "Cobertura", -4f, 26f, 1f, 1.2f, 3f, 300);
            B(campo, "Cobertura_4", "Cobertura", 14f, 30f, 3f, 1.2f, 1f, 300);
            B(campo, "Cobertura_5", "Cobertura", -16f, 34f, 3f, 1.2f, 1f, 300);
            B(campo, "Cobertura_6", "Cobertura", 4f, 36f, 4f, 1.2f, 1f, 300);

            // --- 3. Paso del canion: hueco central x -3..9 ---
            const string canon = "3_PasoDelCanon";
            B(canon, "Muro_Canon_Oeste", "Muro", -13.9f, 50f, 21.8f, 4f, 2.5f);
            B(canon, "Muro_Canon_Este", "Muro", 21.3f, 50f, 24.6f, 4f, 2.5f);
            B(canon, "Bunker_Canon_Oeste", "Cobertura", -6f, 58f, 4f, 2f, 2f, 600);
            B(canon, "Bunker_Canon_Este", "Cobertura", 12f, 58f, 4f, 2f, 2f, 600);

            // --- 4. Aldea: calle libre entre x -8 y x 14 ---
            const string aldea = "4_Aldea";
            B(aldea, "Casa_Oeste_1", "Casa", -14f, 66f, 8f, 5f, 7f);
            B(aldea, "Casa_Oeste_2", "Casa", -14f, 75f, 8f, 5f, 6f);
            B(aldea, "Casa_Este_1", "Casa", 22f, 67f, 9f, 5f, 7f);
            B(aldea, "Casa_Este_2", "Casa", 21f, 75f, 7f, 5f, 5f);
            B(aldea, "Pozo", "Cobertura", 4f, 70f, 2f, 1f, 2f, 400);
            B(aldea, "Sacos_Oeste", "Cobertura", -4f, 64f, 1f, 1.1f, 3f, 400);
            B(aldea, "Sacos_Este", "Cobertura", 12f, 74f, 1f, 1.1f, 3f, 400);

            // --- 5. Fortin enemigo: x -14..22, z 80..112, porton x -4..8 ---
            const string fortin = "5_FortinEnemigo";
            B(fortin, "Muralla_Sur_Oeste", "Muro", -9f, 80f, 10f, 3f, 1.5f);
            B(fortin, "Muralla_Sur_Este", "Muro", 15f, 80f, 14f, 3f, 1.5f);
            B(fortin, "Muralla_Norte_Oeste", "Muro", -5f, 112f, 18f, 3f, 1.5f);
            B(fortin, "Muralla_Norte_Este", "Muro", 13f, 112f, 18f, 3f, 1.5f);
            B(fortin, "Muralla_Oeste_Sur", "Muro", -14f, 88f, 1.5f, 3f, 16f);
            B(fortin, "Muralla_Oeste_Norte", "Muro", -14f, 104f, 1.5f, 3f, 16f);
            B(fortin, "Muralla_Este_Sur", "Muro", 22f, 88f, 1.5f, 3f, 16f);
            B(fortin, "Muralla_Este_Norte", "Muro", 22f, 104f, 1.5f, 3f, 16f);
            B(fortin, "Torre_Central", "Torre", 4f, 97.5f, 3f, 6f, 3f);
            B(fortin, "Caja_1", "Cobertura", 14f, 92f, 2f, 1.2f, 2f, 300);
            B(fortin, "Caja_2", "Cobertura", -9f, 100f, 2f, 1.2f, 2f, 300);
            B(fortin, "Caja_3", "Cobertura", 15f, 104f, 2f, 1.2f, 2f, 300);
            B(fortin, "Caja_4", "Cobertura", -2f, 108f, 2f, 1.2f, 2f, 300);
            B(fortin, "Caja_5", "Cobertura", 9f, 108f, 2f, 1.2f, 2f, 300);

            // --- 6. Deposito ---
            const string deposito = "6_Deposito";
            B(deposito, "Galpon_Oeste", "Casa", -10f, 125f, 8f, 4f, 7f);
            B(deposito, "Galpon_Este", "Casa", 18f, 127f, 8f, 4f, 7f);
            B(deposito, "Muro_Fondo", "Muro", 4f, 134f, 20f, 3f, 1.5f);
        }

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
            AlinearYPintarTerreno();
            int cubos = ConstruirCubos();
            int rutas = ConstruirRutasDeEnemigos();
            BakeNavMesh();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Blockout] Nivel listo: {cubos} cubos, {rutas} rutas de patrulla, terreno pintado y NavMesh horneado.");
        }

        // ---------------------------------------------------------------
        // Terreno
        // ---------------------------------------------------------------
        static void AlinearYPintarTerreno()
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) { Debug.LogWarning("[Blockout] No hay Terrain."); return; }
            var td = terrain.terrainData;

            // Respaldo del terreno original, una sola vez.
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainBackupPath) == null)
                AssetDatabase.CopyAsset(TerrainDataPath, TerrainBackupPath);

            // El terreno era de 200x100 y arrancaba en (0,0): solo tapaba una
            // esquina del mapa jugable. Se lo ajusta a la caja "Ground".
            td.size = TerrainSize;
            terrain.transform.position = TerrainOrigin;

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

            // Alturas: planas (solo se pinta).
            int hres = td.heightmapResolution;
            td.SetHeights(0, 0, new float[hres, hres]);

            var tc = terrain.GetComponent<TerrainCollider>();
            if (tc != null) { tc.terrainData = null; tc.terrainData = td; }

            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();
        }

        // 0 = pasto, 1 = tierra. Suma de formas suaves + ruido para que los
        // bordes no sean rectas de regla.
        static float PesoDeTierra(float x, float z)
        {
            float ruido = (Mathf.PerlinNoise(x * 0.11f + 40f, z * 0.11f + 90f) - 0.5f) * 3.2f;
            float d = float.MaxValue;

            // Patio de partida
            d = Mathf.Min(d, DistRect(x, z, -24.8f, -22.3f, 33.6f, 6f));
            // Camino principal (tierra) de sur a norte, 8 m de ancho
            var camino = new[]
            {
                new Vector2(2f, 0f), new Vector2(2f, 18f), new Vector2(-5f, 32f), new Vector2(1f, 46f),
                new Vector2(4f, 62f), new Vector2(4f, 80f), new Vector2(4f, 100f),
            };
            for (int k = 0; k < camino.Length - 1; k++)
                d = Mathf.Min(d, DistSegmento(new Vector2(x, z), camino[k], camino[k + 1]) - 4f);
            // Paso del canion y plaza de la aldea
            d = Mathf.Min(d, DistRect(x, z, -3f, 44f, 9f, 56f));
            d = Mathf.Min(d, Vector2.Distance(new Vector2(x, z), new Vector2(4f, 70f)) - 12f);
            // Piso del fortin y del deposito
            d = Mathf.Min(d, DistRect(x, z, -14f, 80f, 22f, 112f));
            d = Mathf.Min(d, Vector2.Distance(new Vector2(x, z), new Vector2(-10f, 125f)) - 7f);
            d = Mathf.Min(d, Vector2.Distance(new Vector2(x, z), new Vector2(18f, 127f)) - 7f);
            // Manchones sueltos en el campo
            d = Mathf.Min(d, Vector2.Distance(new Vector2(x, z), new Vector2(-14f, 24f)) - 5f);
            d = Mathf.Min(d, Vector2.Distance(new Vector2(x, z), new Vector2(18f, 38f)) - 4f);

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
        // Rutas de patrulla: cada enemigo ronda CERCA de donde nace
        // ---------------------------------------------------------------
        // Antes 4 de los 5 enemigos heredaban la ronda por defecto del
        // prefab, (20..30, 20..30): un cuadrado a ~60 m de donde nacen, del
        // lado de la escuadra. Se leia como "el enemigo va directo a mi".
        // Ahora cada uno da vueltas por un rectangulo de ~8 x 6 m dentro
        // del fortin, alrededor de su punto de partida.
        static readonly Dictionary<string, Vector2[]> Rondas = new Dictionary<string, Vector2[]>
        {
            { "Enemigo_Patrulla_2", new[] { new Vector2(9f, 87f), new Vector2(17f, 87f), new Vector2(17f, 96f), new Vector2(9f, 96f) } },
            { "Enemigo_Patrulla_3", new[] { new Vector2(-11f, 90f), new Vector2(-6f, 90f), new Vector2(-6f, 98f), new Vector2(-11f, 98f) } },
            { "Enemigo_Patrulla_4", new[] { new Vector2(-2f, 85f), new Vector2(8f, 85f), new Vector2(8f, 89f), new Vector2(-2f, 89f) } },
            { "Enemigo_Patrulla_5", new[] { new Vector2(-11f, 84f), new Vector2(-6f, 84f), new Vector2(-6f, 88f), new Vector2(-11f, 88f) } },
        };

        static int ConstruirRutasDeEnemigos()
        {
            var rootRutas = GameObject.Find("Waypoints");
            int n = 0;
            foreach (var kv in Rondas)
            {
                var go = GameObject.Find(kv.Key);
                var brain = go != null ? go.GetComponent<AiBrain>() : null;
                if (brain == null) continue;

                string nombre = "PatrolRoute_" + kv.Key.Replace("Enemigo_", "");
                var previa = GameObject.Find(nombre);
                if (previa != null) Object.DestroyImmediate(previa);

                var puntos = new Vector3[kv.Value.Length];
                for (int i = 0; i < puntos.Length; i++) puntos[i] = new Vector3(kv.Value[i].x, 0f, kv.Value[i].y);

                var linea = PatrolRouteLine.Spawn(puntos, new Color(0.95f, 0.6f, 0.2f));
                linea.gameObject.name = nombre;
                if (rootRutas != null) linea.transform.SetParent(rootRutas.transform, true);
                brain.SetPatrolWaypoints(linea.Markers);
                EditorUtility.SetDirty(brain);
                n++;
            }
            return n;
        }

        // ---------------------------------------------------------------
        // NavMesh
        // ---------------------------------------------------------------
        // Recast solo rasteriza las CARAS de una malla, no su volumen: bajo un
        // cubo alto (un muro, una casa) el piso queda "caminable" en el
        // centro, porque la erosion por radio solo come el borde. Medido: un
        // punto en el medio de la muralla estaba sobre el NavMesh y una ruta
        // la atravesaba. Un NavMeshModifierVolume "No caminable" del tamaño
        // del cubo lo vuelve solido de verdad (y de paso borra la isla
        // inalcanzable que quedaba sobre el techo).
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

            // Todo cubo solido de la escena (los del nivel y los que ya
            // estaban: Muro, Obstaculo_*). Se excluyen el piso, los
            // soldados, los proyectiles y los vehiculos.
            int volumenes = 0;
            foreach (var c in Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (c.isTrigger || c.name == "Ground") continue;
                if (c.GetComponentInParent<Soldier>() != null) continue;
                if (c.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                if (c.GetComponentInParent<SP.Combat.Projectile>() != null) continue;
                if (c.bounds.size.y < 0.8f) continue;
                if (c.GetComponent<Unity.AI.Navigation.NavMeshModifierVolume>() == null) volumenes++;
                AgregarVolumenNoCaminable(c.gameObject);
            }

            // El vehiculo se mueve: hornearlo dejaria un agujero fijo en el
            // lugar donde estaba parado. Ya frena a los soldados por fisica
            // (Deslizador), no hace falta que ademas recorte el NavMesh.
            foreach (var v in Object.FindObjectsByType<SP.Vehicles.Vehicle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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
