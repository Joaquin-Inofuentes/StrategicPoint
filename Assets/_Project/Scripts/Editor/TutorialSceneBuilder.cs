using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Presentation;
using SP.Tutorial;
using SP.Vehicles;

namespace SP.EditorTools
{
    // Arma SC_Tutorial: una escena NUEVA hecha a partir de SC_Gameplay (asi trae
    // el jugador, los aliados, el tanque, el HUD, la camara y todos los
    // sistemas) pero con un nivel propio, corto y lineal, hecho SOLO con cubos
    // (blocking) y pintura de terreno:
    //
    //   Z -30 .. 28   Campo de tiro   pared gris, caja destructible, enemigo quieto
    //   Z  28 .. 80   Plaza           2 aliados lejos, zonas A y B para las ordenes
    //   Z  80 .. 112  Patio           el tanque del jugador
    //   Z 112 .. 200  Corredor        chicanes, olas de enemigos y la META
    //
    // Es idempotente: cada corrida copia SC_Gameplay de cero y lo rearma.
    //   Strategic Point > Tutorial > Construir escena SC_Tutorial
    public static class TutorialSceneBuilder
    {
        const string EscenaOrigen = "Assets/_Project/Scenes/SC_Gameplay.unity";
        public const string EscenaTutorial = "Assets/_Project/Scenes/SC_Tutorial.unity";
        const string TerrenoOrigen = "Assets/_Project/Terrains/TerrainData_Main.asset";
        const string TerrenoTutorial = "Assets/_Project/Terrains/TerrainData_Tutorial.asset";
        const string PrefabEnemigo = "Assets/_Project/Prefabs/P_Soldier_Enemy.prefab";
        const string Raiz = "Tutorial_Blockout";

        // Mundo jugable del tutorial.
        static readonly Vector3 Origen = new Vector3(-35f, 0f, -30f);
        static readonly Vector3 Tam = new Vector3(70f, 30f, 210f);

        [MenuItem("Strategic Point/Tutorial/Construir escena SC_Tutorial")]
        public static void Construir()
        {
            var actual = EditorSceneManager.GetActiveScene();
            if (actual.isDirty) EditorSceneManager.SaveScene(actual);

            AssetDatabase.DeleteAsset(EscenaTutorial);
            if (!AssetDatabase.CopyAsset(EscenaOrigen, EscenaTutorial)) { Debug.LogError("[Tutorial] No pude copiar SC_Gameplay."); return; }
            var scene = EditorSceneManager.OpenScene(EscenaTutorial, OpenSceneMode.Single);

            Limpiar();
            PrepararTerreno();
            int cubos = ConstruirCubos();
            ColocarEscuadraYTanque();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabEnemigo);
            ColocarMarcadores(prefab);
            LevelBlockoutBuilder.AjustarAlcancesDeCombate();
            ConfigurarSistemas(prefab);
            LevelBlockoutBuilder.BakeNavMesh();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AgregarAlBuild();
            Debug.Log($"[Tutorial] SC_Tutorial listo: {cubos} cubos, terreno pintado, NavMesh horneado.");
        }

        // ---------------------------------------------------------------
        static void Limpiar()
        {
            foreach (var n in new[] { "Destructibles", "Weapons", "Enemies", "Waypoints", "Arte", "Blocking", "ArteMundo", "Nivel_Blockout", "PisoMundo" })
            {
                var g = GameObject.Find(n);
                if (g != null) Object.DestroyImmediate(g);
            }
            var muro = GameObject.Find("Muro");
            if (muro != null) Object.DestroyImmediate(muro);
            foreach (var n in new[] { "Tanque_Enemigo_1", "Tanque_Enemigo_2", "Tanque_Enemigo_3" })
            {
                var g = GameObject.Find(n);
                if (g != null) Object.DestroyImmediate(g);
            }

            // Iconos de minimapa cuyo objetivo ya no existe.
            var uiWorld = GameObject.Find("UI_World");
            if (uiWorld != null)
                for (int i = uiWorld.transform.childCount - 1; i >= 0; i--)
                {
                    var icon = uiWorld.transform.GetChild(i).GetComponent<MinimapIcon>();
                    if (icon != null && icon.Target == null) Object.DestroyImmediate(icon.gameObject);
                }

            var driver = Object.FindFirstObjectByType<SP.Player.PlayerInputDriver>();
            if (driver != null)
            {
                var so = new SerializedObject(driver);
                var picks = so.FindProperty("WeaponPickups");
                if (picks != null) picks.ClearArray();
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // Cartel de mision del primer frame (PrimerFramePreview): el tutorial no lo usa.
            var fase = Object.FindFirstObjectByType<SP.UI.PhaseBannerView>(FindObjectsInactive.Include);
            if (fase != null)
            {
                var t = fase.GetComponentInChildren<UnityEngine.UI.Text>(true);
                if (t != null)
                {
                    t.text = "";
                    t.gameObject.SetActive(false);
                    var link = t.GetComponent<SP.UI.FondoOpacoLink>();
                    if (link != null && link.Fondo != null) link.Fondo.SetActive(false);
                }
            }
            var bm = Object.FindFirstObjectByType<BattleManager>();
            if (bm != null)
            {
                var so = new SerializedObject(bm);
                var en = so.FindProperty("Enemies");
                if (en != null) en.ClearArray();
                so.ApplyModifiedPropertiesWithoutUndo();
                bm.enabled = false;   // el tutorial tiene su propia victoria
            }
        }

        // ---------------------------------------------------------------
        // Terreno: TerrainData propio (el del juego principal no se toca).
        // ---------------------------------------------------------------
        static void PrepararTerreno()
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) { Debug.LogWarning("[Tutorial] No hay Terrain."); return; }

            AssetDatabase.DeleteAsset(TerrenoTutorial);
            AssetDatabase.CopyAsset(TerrenoOrigen, TerrenoTutorial);
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrenoTutorial);
            terrain.terrainData = td;
            var tc = terrain.GetComponent<TerrainCollider>();
            if (tc != null) tc.terrainData = td;

            td.size = Tam;
            terrain.transform.position = Origen;
            td.alphamapResolution = 512;
            int res = td.alphamapResolution, capas = td.alphamapLayers;
            var mapa = new float[res, res, capas];
            for (int i = 0; i < res; i++)
            {
                float wz = Origen.z + (i + 0.5f) / res * Tam.z;
                for (int j = 0; j < res; j++)
                {
                    float wx = Origen.x + (j + 0.5f) / res * Tam.x;
                    float tierra = PesoTierra(wx, wz);
                    mapa[i, j, 0] = 1f - tierra;
                    if (capas > 1) mapa[i, j, 1] = tierra;
                }
            }
            td.SetAlphamaps(0, 0, mapa);
            int hres = td.heightmapResolution;
            td.SetHeights(0, 0, new float[hres, hres]);
            if (tc != null) { tc.terrainData = null; tc.terrainData = td; }
            EditorUtility.SetDirty(td);

            var ground = GameObject.Find("Ground");
            if (ground != null)
            {
                ground.transform.position = new Vector3(Origen.x + Tam.x * 0.5f, -0.5f, Origen.z + Tam.z * 0.5f);
                ground.transform.localScale = new Vector3(Tam.x, 1f, Tam.z);
            }
            AssetDatabase.SaveAssets();
        }

        // 0 = pasto, 1 = tierra: camino central, patios y zonas de practica.
        static float PesoTierra(float x, float z)
        {
            float ruido = (Mathf.PerlinNoise(x * 0.11f + 12f, z * 0.11f + 33f) - 0.5f) * 3f;
            float d = float.MaxValue;
            d = Mathf.Min(d, Mathf.Abs(x) - 5f);                                       // camino central
            d = Mathf.Min(d, DistCirculo(x, z, 0f, 16f, 15f));                          // campo de tiro
            d = Mathf.Min(d, DistCirculo(x, z, 18f, 62f, 9f));                          // zona A
            d = Mathf.Min(d, DistCirculo(x, z, -16f, 58f, 7f));                         // zona B
            d = Mathf.Min(d, DistCirculo(x, z, 0f, 104f, 12f));                          // patio del tanque
            d = Mathf.Min(d, DistCirculo(x, z, 0f, 164f, 12f));                         // meta
            if (z < -4f) d = Mathf.Min(d, -3f);                                         // base
            return Mathf.Clamp01(1f - Mathf.SmoothStep(0f, 1f, (d + ruido) / 2.4f + 0.5f));
        }

        static float DistCirculo(float x, float z, float cx, float cz, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz)) - r;

        // ---------------------------------------------------------------
        // Cubos (blocking)
        // ---------------------------------------------------------------
        const int Indestructible = 999999;

        struct Cubo { public string G, N, Mat; public float X, Z, W, H, D; public int Vida; }
        static readonly List<Cubo> Cubos = new List<Cubo>();
        static void C(string g, string n, string mat, float x, float z, float w, float h, float d, int vida = Indestructible)
            => Cubos.Add(new Cubo { G = g, N = n, Mat = mat, X = x, Z = z, W = w, H = h, D = d, Vida = vida });

        static Material MatCaja()
        {
            const string path = "Assets/_Project/Materials/Blocking/M_Blocking_Tutorial_Caja.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Blocking_Tutorial_Caja" };
            m.SetColor("_BaseColor", new Color(1f, 0.83f, 0.12f));
            m.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material MatMeta()
        {
            const string path = "Assets/_Project/Materials/Blocking/M_Blocking_Tutorial_Meta.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Blocking_Tutorial_Meta" };
            m.SetColor("_BaseColor", new Color(0.95f, 0.78f, 0.2f));
            m.SetFloat("_Smoothness", 0.35f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void DefinirCubos()
        {
            Cubos.Clear();
            const string a = "1_CampoDeTiro", b = "2_Plaza", c = "3_Patio", d = "4_Corredor";

            // Perimetro (todo el recorrido): muros oeste, este, sur y norte.
            C(a, "Perimetro_Oeste", "Muro", -34f, 75f, 1.5f, 3f, 210f);
            C(a, "Perimetro_Este", "Muro", 34f, 75f, 1.5f, 3f, 210f);
            C(a, "Perimetro_Sur", "Muro", 0f, -29f, 70f, 3f, 1.5f);
            C(d, "Perimetro_Norte", "Muro", 0f, 179f, 70f, 3f, 1.5f);

            // 1. Campo de tiro: pared gris, caja destructible amarilla y coberturas de adorno.
            C(a, "Tut_Pared", "Muro", -8f, 18f, 7f, 2.6f, 1f);
            C(a, "Cobertura_Tiro_1", "Cobertura", -20f, 4f, 3f, 1.2f, 1f, 300);
            C(a, "Cobertura_Tiro_2", "Cobertura", 20f, 6f, 3f, 1.2f, 1f, 300);
            C(a, "Cobertura_Tiro_3", "Cobertura", 12f, -8f, 1f, 1.2f, 3f, 300);
            C(a, "Casilla_Base", "Casa", -26f, -18f, 8f, 4f, 6f);
            C(a, "Casilla_Tiro", "Casa", 26f, 22f, 6f, 3.5f, 6f);

            // 2. Plaza: coberturas y una casilla para que las zonas de orden no queden vacias.
            C(b, "Cobertura_Plaza_1", "Cobertura", -8f, 46f, 3f, 1.2f, 1f, 300);
            C(b, "Cobertura_Plaza_2", "Cobertura", 6f, 52f, 1f, 1.2f, 3f, 300);
            C(b, "Cobertura_Plaza_3", "Cobertura", 26f, 50f, 3f, 1.2f, 1f, 300);
            C(b, "Cobertura_Plaza_4", "Cobertura", -26f, 66f, 3f, 1.2f, 1f, 300);
            C(b, "Casa_Plaza", "Casa", -28f, 54f, 6f, 4f, 8f);
            C(b, "Casa_Plaza_2", "Casa", 28f, 74f, 6f, 4f, 8f);

            // 3. Patio del tanque: garaje abierto al norte.
            C(c, "Garaje_Fondo", "Torre", -20f, 98f, 1.5f, 4f, 14f);
            C(c, "Garaje_Fondo_2", "Torre", 20f, 98f, 1.5f, 4f, 14f);
            C(c, "Cobertura_Patio_1", "Cobertura", -10f, 88f, 3f, 1.2f, 1f, 300);
            C(c, "Cobertura_Patio_2", "Cobertura", 12f, 112f, 3f, 1.2f, 1f, 300);

            // 4. Corredor de 30 m con tres chicanes alternadas (siempre queda un pasillo central de 10 m).
            C(d, "Corredor_Oeste", "Muro", -16f, 142f, 1.5f, 3f, 62f);
            C(d, "Corredor_Este", "Muro", 16f, 142f, 1.5f, 3f, 62f);
            C(d, "Chicane_1", "Muro", -10f, 124f, 10f, 3f, 2f);
            C(d, "Chicane_2", "Muro", 10f, 138f, 10f, 3f, 2f);
            C(d, "Chicane_3", "Muro", -10f, 152f, 10f, 3f, 2f);
            C(d, "Cobertura_Corredor_1", "Cobertura", 12f, 118f, 3f, 1.2f, 1f, 300);
            C(d, "Cobertura_Corredor_2", "Cobertura", -12f, 144f, 3f, 1.2f, 1f, 300);

            // Puerta de la META: dos pilares y un dintel dorado (solo blocking).
            C(d, "Meta_Pilar_Oeste", "Meta", -7f, 166f, 2f, 7f, 2f);
            C(d, "Meta_Pilar_Este", "Meta", 7f, 166f, 2f, 7f, 2f);
        }

        static int ConstruirCubos()
        {
            var previo = GameObject.Find(Raiz);
            if (previo != null) Object.DestroyImmediate(previo);
            DefinirCubos();
            var raiz = new GameObject(Raiz).transform;
            var grupos = new Dictionary<string, Transform>();
            int n = 0;
            foreach (var b in Cubos)
            {
                if (!grupos.TryGetValue(b.G, out var g))
                {
                    g = new GameObject(b.G).transform;
                    g.SetParent(raiz, false);
                    grupos[b.G] = g;
                }
                var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubo.name = b.N;
                cubo.transform.SetParent(g, false);
                cubo.transform.position = new Vector3(b.X, b.H * 0.5f, b.Z);
                cubo.transform.localScale = new Vector3(b.W, b.H, b.D);
                cubo.GetComponent<MeshRenderer>().sharedMaterial = b.Mat == "Meta" ? MatMeta() : LevelBlockoutBuilder.MaterialDe(b.Mat);
                var marca = cubo.AddComponent<ObstacleMarker>();
                var so = new SerializedObject(marca);
                so.FindProperty("maxHealth").intValue = b.Vida;
                so.ApplyModifiedPropertiesWithoutUndo();
                n++;
            }

            // La caja destructible amarilla del campo de tiro (90 de vida).
            var caja = GameObject.CreatePrimitive(PrimitiveType.Cube);
            caja.name = "Tut_Destruible";
            caja.transform.SetParent(grupos["1_CampoDeTiro"], false);
            caja.transform.position = new Vector3(0f, 0.9f, 18f);
            caja.transform.localScale = new Vector3(2.2f, 1.8f, 2.2f);
            caja.GetComponent<MeshRenderer>().sharedMaterial = MatCaja();
            var mc = caja.AddComponent<ObstacleMarker>();
            var soc = new SerializedObject(mc);
            soc.FindProperty("maxHealth").intValue = 90;
            soc.ApplyModifiedPropertiesWithoutUndo();
            n++;

            // Dintel dorado de la meta.
            var dintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dintel.name = "Meta_Dintel";
            dintel.transform.SetParent(grupos["4_Corredor"], false);
            dintel.transform.position = new Vector3(0f, 7.75f, 166f);
            dintel.transform.localScale = new Vector3(16f, 1.5f, 2f);
            dintel.GetComponent<MeshRenderer>().sharedMaterial = MatMeta();
            Object.DestroyImmediate(dintel.GetComponent<Collider>());   // decorativo: no bloquea
            n++;
            AssetDatabase.SaveAssets();
            return n;
        }

        // ---------------------------------------------------------------
        static void ColocarEscuadraYTanque()
        {
            Poner("Soldado_1_Vega", new Vector3(0f, 0.8f, -8f), 0f);
            Poner("Soldado_2_Kes", new Vector3(-26f, 0.8f, 44f), 180f);
            Poner("Soldado_3_Doc", new Vector3(26f, 0.8f, 44f), 180f);

            var tanque = GameObject.Find("Vehiculo_Blindado");
            if (tanque != null) tanque.transform.SetPositionAndRotation(new Vector3(0f, 0.6f, 104f), Quaternion.identity);

            // La camara arranca detras de Vega mirando al norte.
            var cam = GameObject.Find("MainCamera");
            if (cam != null) cam.transform.SetPositionAndRotation(new Vector3(0f, 2.3f, -10f), Quaternion.identity);
        }

        static void Poner(string nombre, Vector3 pos, float rotY)
        {
            var g = GameObject.Find(nombre);
            if (g != null) g.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rotY, 0f));
        }

        // ---------------------------------------------------------------
        // Objetos de la practica (se buscan por nombre desde TutorialManager)
        // ---------------------------------------------------------------
        static Transform Grupo()
        {
            var g = GameObject.Find("Tutorial");
            if (g == null) g = new GameObject("Tutorial");
            return g.transform;
        }

        static void ColocarMarcadores(GameObject prefabEnemigo)
        {
            var grupo = Grupo();
            foreach (var n in new[] { "Tut_Enemigo_Estatico", "Tut_ZonaA", "Tut_ZonaB", "Tut_Meta" })
            {
                var v = GameObject.Find(n);
                if (v != null) Object.DestroyImmediate(v);
            }

            // Enemigo estatico: no se mueve, no dispara ("alto el fuego") y
            // tiene poca vida para que se pueda derribar con el rifle.
            var dummy = (GameObject)PrefabUtility.InstantiatePrefab(prefabEnemigo, grupo);
            dummy.name = "Tut_Enemigo_Estatico";
            dummy.transform.SetPositionAndRotation(new Vector3(8f, 0.8f, 18f), Quaternion.Euler(0f, 180f, 0f));
            var brain = dummy.GetComponent<AiBrain>();
            brain.SetPatrolWaypoints(null);
            brain.Stance = CombatStance.AltoElFuego;
            var so = new SerializedObject(dummy.GetComponent<Soldier>());
            so.FindProperty("maxHealth").intValue = 100;
            so.ApplyModifiedPropertiesWithoutUndo();

            Marcador("Tut_ZonaA", new Vector3(18f, 0f, 62f), grupo);
            Marcador("Tut_ZonaB", new Vector3(-16f, 0f, 58f), grupo);
            Marcador("Tut_Meta", new Vector3(0f, 0f, 162f), grupo);
        }

        static void Marcador(string nombre, Vector3 pos, Transform padre)
        {
            var g = new GameObject(nombre);
            g.transform.SetParent(padre, false);
            g.transform.position = pos;
        }

        // ---------------------------------------------------------------
        static void ConfigurarSistemas(GameObject prefabEnemigo)
        {
            var sistemas = GameObject.Find("Systems");

            // El tutorial da sus propios carteles.
            var boot = Object.FindFirstObjectByType<GameplaySceneBootstrap>();
            if (boot != null)
            {
                boot.esTutorial = true;
                EditorUtility.SetDirty(boot);
            }

            // Sin seguimiento automatico: el tutorial ensena a pedirlo con [Y].
            var aj = AjustesDeEscuadra.AsegurarEnEscena();
            aj.distanciaParaSeguir = 999f;
            aj.distanciaParaDetenerse = 8f;
            EditorUtility.SetDirty(aj);

            // El enemigo estatico no ve ni dispara a nadie: rango de vision casi nulo.
            var dummy = GameObject.Find("Tut_Enemigo_Estatico");
            if (dummy != null)
            {
                var ai = dummy.GetComponent<AiBrain>();
                var soAi = new SerializedObject(ai);
                soAi.FindProperty("visionRange").floatValue = 0.5f;
                soAi.FindProperty("attackRange").floatValue = 0.5f;
                soAi.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ai);
            }

            var t = GameObject.Find("TutorialManager");
            if (t == null)
            {
                t = new GameObject("TutorialManager");
                if (sistemas != null) t.transform.SetParent(sistemas.transform, false);
            }
            var mgr = t.GetComponent<TutorialManager>() ?? t.AddComponent<TutorialManager>();
            mgr.prefabEnemigo = prefabEnemigo;
            EditorUtility.SetDirty(mgr);
            AsegurarCatalogo();
        }

        // Crea el CatalogoDelTutorial y le asigna los objetos bakeados por nombre AHORA, en el editor, que es donde
        // buscar por nombre es legitimo: en runtime el tutorial lee las referencias serializadas.
        [MenuItem("Strategic Point/Tutorial/Asegurar catalogo en la escena abierta")]
        public static void AsegurarCatalogo()
        {
            var cat = Object.FindFirstObjectByType<CatalogoDelTutorial>(FindObjectsInactive.Include);
            if (cat == null)
            {
                var go = new GameObject("CatalogoDelTutorial");
                var sistemas = GameObject.Find("Systems");
                if (sistemas != null) go.transform.SetParent(sistemas.transform, false);
                cat = go.AddComponent<CatalogoDelTutorial>();
            }
            cat.EnemigoEstatico = GameObject.Find("Tut_Enemigo_Estatico");
            cat.Pared = GameObject.Find("Tut_Pared");
            cat.Destruible = GameObject.Find("Tut_Destruible");
            cat.ZonaA = GameObject.Find("Tut_ZonaA");
            cat.ZonaB = GameObject.Find("Tut_ZonaB");
            cat.Meta = GameObject.Find("Tut_Meta");
            EditorUtility.SetDirty(cat);
            EditorSceneManager.MarkSceneDirty(cat.gameObject.scene);
            var vacios = cat.CamposVacios();
            if (vacios.Count > 0) Debug.LogError("[Tutorial] Al catalogo le faltan: " + string.Join(", ", vacios));
        }

        static void AgregarAlBuild()
        {
            var lista = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in lista) if (s.path == EscenaTutorial) return;
            lista.Add(new EditorBuildSettingsScene(EscenaTutorial, true));
            EditorBuildSettings.scenes = lista.ToArray();
        }
    }
}
