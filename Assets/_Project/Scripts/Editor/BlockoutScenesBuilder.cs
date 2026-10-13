using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Player;
using SP.Presentation;
using SP.Vehicles;

namespace SP.EditorTools
{
    // Cinco escenas de PURO BLOCKING para diseno de niveles (SC_Blockout_01..05): solo cubos de color plano, rampas, plataformas,
    // soldados, un blindado y torretas. Cada una pone en juego una especialidad distinta (Asalto, Francotirador, Medico, Flanker,
    // Civil) y una mecanica propia. La base es el mundo de demo de la suite (camara, HUD, input, escuadra de 3 y vehiculo ya
    // cableados): se vacia el suelo/obstaculos/patrullas y se arma el nivel nuevo encima, con NavMesh horneado.
    //
    // Idempotente: cada corrida regenera las escenas desde cero. Menu: Strategic Point > Nivel > Generar 5 escenas de blockout.
    public static class BlockoutScenesBuilder
    {
        const string Dir = "Assets/_Project/Scenes/";
        const string MatDir = "Assets/_Project/Materials/Blocking/";
        const string PrefabEnemigo = "Assets/_Project/Prefabs/P_Soldier_Enemy.prefab";
        const string PrefabTanque = "Assets/_Project/Prefabs/P_Vehicle_Blindado.prefab";

        public static readonly string[] Nombres =
        {
            "SC_Blockout_01", "SC_Blockout_02", "SC_Blockout_03", "SC_Blockout_04", "SC_Blockout_05",
        };

        static readonly Color ColEnemigo = new Color(0.95f, 0.25f, 0.20f), ColFlanker = new Color(0.95f, 0.55f, 0.15f), ColVigia = new Color(0.65f, 0.12f, 0.12f), ColCivil = new Color(1f, 0.92f, 0.2f);

        static Transform raiz;
        static readonly List<Soldier> enemigos = new List<Soldier>();
        static ProjectilePool pool;
        static GameObject prefabSoldado;
        static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

        [MenuItem("Strategic Point/Nivel/Generar 5 escenas de blockout")]
        public static void GenerarTodas() { for (int i = 0; i < Nombres.Length; i++) Debug.Log(Generar(i + 1)); }

        // ---------------------------------------------------------------- materiales y piezas
        static Material Mat(string clave, Color c)
        {
            if (matCache.TryGetValue(clave, out var m) && m != null) return m;
            if (!AssetDatabase.IsValidFolder(MatDir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/_Project/Materials", "Blocking");
            string ruta = MatDir + "M_Blockout_" + clave + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Blockout_" + clave };
                m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0.05f);
                AssetDatabase.CreateAsset(m, ruta);
            }
            matCache[clave] = m;
            return m;
        }

        static Material MatDe(string tipo)
        {
            switch (tipo)
            {
                case "Muro": return Mat("Muro", new Color(0.52f, 0.53f, 0.56f));
                case "Casa": return Mat("Casa", new Color(0.74f, 0.63f, 0.48f));
                case "Torre": return Mat("Torre", new Color(0.42f, 0.38f, 0.38f));
                case "Contenedor": return Mat("Contenedor", new Color(0.45f, 0.30f, 0.22f));
                case "Brecha": return Mat("Brecha", new Color(0.78f, 0.45f, 0.35f));        // muro debil: se rompe
                case "Altura": return Mat("Altura", new Color(0.66f, 0.70f, 0.78f));        // plataformas y rampas
                case "Piso": return Mat("Piso", new Color(0.80f, 0.82f, 0.80f));
                case "ZonaAliada": return Mat("ZonaAliada", new Color(0.35f, 0.60f, 0.95f));
                case "ZonaEnemiga": return Mat("ZonaEnemiga", new Color(0.90f, 0.35f, 0.30f));
                case "Objetivo": return Mat("Objetivo", new Color(1f, 0.90f, 0.25f));
                case "Curacion": return Mat("Curacion", new Color(0.30f, 0.85f, 0.55f));
                case "Torreta": return Mat("Torreta", new Color(0.20f, 0.22f, 0.25f));
                default: return Mat("Cobertura", new Color(0.36f, 0.45f, 0.30f));
            }
        }

        // Cubo con base en y (el piso es y=0). solido: cubo que bloquea (marcador de obstaculo + hueco en el NavMesh).
        static GameObject Cubo(string nombre, string tipo, float x, float z, float w, float h, float d, float y = 0f, bool solido = true, int vida = 999999, float rotY = 0f)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.name = nombre;
            c.transform.SetParent(raiz, false);
            c.transform.position = new Vector3(x, y + h * 0.5f, z);
            c.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            c.transform.localScale = new Vector3(w, h, d);
            c.GetComponent<MeshRenderer>().sharedMaterial = MatDe(tipo);
            if (solido)
            {
                var m = c.AddComponent<ObstacleMarker>();
                var so = new SerializedObject(m);
                so.FindProperty("maxHealth").intValue = vida;
                so.ApplyModifiedPropertiesWithoutUndo();
                LevelBlockoutBuilder.AgregarVolumenNoCaminable(c);
            }
            return c;
        }

        static void Cob(float x, float z, bool horizontal = true) => Cubo("Cobertura", "Cobertura", x, z, horizontal ? 3f : 1f, 1.2f, horizontal ? 1f : 3f, 0f, true, 300);
        static void Muro(string n, float x0, float x1, float z, float alto = 4f, float grosor = 2f, string tipo = "Muro", int vida = 999999) => Cubo(n, tipo, (x0 + x1) * 0.5f, z, x1 - x0, alto, grosor, 0f, true, vida);
        static void MuroZ(string n, float z0, float z1, float x, float alto = 4f, float grosor = 2f, string tipo = "Muro", int vida = 999999) => Cubo(n, tipo, x, (z0 + z1) * 0.5f, grosor, alto, z1 - z0, 0f, true, vida);

        // Plataforma sin hueco de NavMesh: su techo se camina (se sube por rampa). Solo se pone el marcador de obstaculo no.
        static void Plataforma(string n, float x, float z, float w, float d, float alto) => Cubo(n, "Altura", x, z, w, alto, d, 0f, false);

        // Rampa que sube hacia +Z local (rotY la orienta). (x,z) = centro del arranque; sube "alto" a lo largo de "largo".
        static void Rampa(string n, float x, float z, float ancho, float largo, float alto, float rotY, float y0 = 0f)
        {
            var pivote = new GameObject(n).transform;
            pivote.SetParent(raiz, false);
            pivote.position = new Vector3(x, y0, z);
            pivote.rotation = Quaternion.Euler(0f, rotY, 0f);
            float hip = Mathf.Sqrt(largo * largo + alto * alto), ang = Mathf.Atan2(alto, largo) * Mathf.Rad2Deg;
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.name = n + "_Losa";
            c.transform.SetParent(pivote, false);
            c.transform.localPosition = new Vector3(0f, alto * 0.5f - 0.1f, largo * 0.5f);
            c.transform.localRotation = Quaternion.Euler(-ang, 0f, 0f);
            c.transform.localScale = new Vector3(ancho, 0.2f, hip);
            c.GetComponent<MeshRenderer>().sharedMaterial = MatDe("Altura");
        }

        // Parche de color plano en el piso (sin collider): marca zonas (aliada, enemiga, objetivo, curacion).
        static void Zona(string nombre, string tipo, float x, float z, float w, float d, float y = 0f)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.name = nombre;
            c.transform.SetParent(raiz, false);
            c.transform.position = new Vector3(x, y + 0.03f, z);
            c.transform.localScale = new Vector3(w, 0.06f, d);
            c.GetComponent<MeshRenderer>().sharedMaterial = MatDe(tipo);
            Object.DestroyImmediate(c.GetComponent<Collider>());
        }

        static void Letrero(string texto, float x, float z, float y = 6f, float tam = 0.6f)
        {
            var g = new GameObject("Letrero_" + texto.Split('\n')[0]);
            g.transform.SetParent(raiz, false);
            g.transform.position = new Vector3(x, y, z);
            var t = g.AddComponent<TextMesh>();
            t.text = texto; t.fontSize = 48; t.characterSize = tam * 0.12f; t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center;
            t.color = Color.white;
        }

        static void Erizo(float x, float z) => Cubo("Erizo", "Cobertura", x, z, 2.2f, 1.6f, 2.2f, 0f, true, 500);

        // ---------------------------------------------------------------- unidades
        static MethodInfo spawn;

        static Soldier Enemigo(string nombre, RoleType rol, float x, float z, float y = 0f, Vector2[] ronda = null, int vida = 180)
        {
            spawn = spawn ?? typeof(HeadlessTestRunner).GetMethod("SpawnSoldier", BindingFlags.NonPublic | BindingFlags.Static);
            var col = rol == RoleType.Flanker ? ColFlanker : (nombre.Contains("Vigia") ? ColVigia : ColEnemigo);
            var s = (Soldier)spawn.Invoke(null, new object[] { prefabSoldado, nombre, TeamId.Enemy, rol, new Vector3(x, y + 0.8f, z), col, pool, vida });
            s.transform.SetParent(Hijo("Enemigos"), true);
            var brain = s.GetComponent<AiBrain>();
            if (ronda != null && ronda.Length > 1)
            {
                var pts = new Vector3[ronda.Length];
                for (int i = 0; i < pts.Length; i++) pts[i] = new Vector3(ronda[i].x, y + 0.8f, ronda[i].y);
                var linea = PatrolRouteLine.Spawn(pts, new Color(0.95f, 0.6f, 0.2f));
                linea.transform.SetParent(Hijo("Rondas"), true);
                brain.SetPatrolWaypoints(linea.Markers);
            }
            enemigos.Add(s);
            return s;
        }

        static Vector2[] Rect(float cx, float cz, float hx, float hz) => new[] { new Vector2(cx - hx, cz - hz), new Vector2(cx + hx, cz - hz), new Vector2(cx + hx, cz + hz), new Vector2(cx - hx, cz + hz) };

        static Transform Hijo(string nombre)
        {
            var t = raiz.Find(nombre);
            if (t == null) { t = new GameObject(nombre).transform; t.SetParent(raiz, false); }
            return t;
        }

        static GameObject Torreta(string nombre, float x, float z, float rotY, float y = 0f)
        {
            var g = new GameObject(nombre);
            g.transform.SetParent(raiz, false);
            g.transform.position = new Vector3(x, y, z);
            g.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            var caja = g.AddComponent<BoxCollider>(); caja.center = new Vector3(0f, 0.55f, 0f); caja.size = new Vector3(2.2f, 1.1f, 1.6f);
            var baseT = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseT.name = "Base"; baseT.transform.SetParent(g.transform, false);
            baseT.transform.localPosition = new Vector3(0f, 0.55f, 0f); baseT.transform.localScale = new Vector3(2f, 1.1f, 1.4f);
            baseT.GetComponent<MeshRenderer>().sharedMaterial = MatDe("Torreta"); Object.DestroyImmediate(baseT.GetComponent<Collider>());
            var canon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            canon.name = "Canon"; canon.transform.SetParent(g.transform, false);
            canon.transform.localPosition = new Vector3(0f, 1.25f, 0.7f); canon.transform.localScale = new Vector3(0.25f, 0.25f, 1.6f);
            canon.GetComponent<MeshRenderer>().sharedMaterial = MatDe("Torreta"); Object.DestroyImmediate(canon.GetComponent<Collider>());
            LevelBlockoutBuilder.AgregarVolumenNoCaminable(g);
            TorretaFija.Instalar(g);
            return g;
        }

        static void TanqueEnemigo(string nombre, float x, float z, float rot, Vector2[] ruta)
        {
            var prefabTanque = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabTanque);
            var prefabEnemigo = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabEnemigo);
            var tanque = (GameObject)PrefabUtility.InstantiatePrefab(prefabTanque, Hijo("Enemigos"));
            tanque.name = nombre;
            tanque.transform.SetPositionAndRotation(new Vector3(x, 0.6f, z), Quaternion.Euler(0f, rot, 0f));
            var tripulacion = new Soldier[2];
            string[] suf = { "_Conductor", "_Artillero" };
            for (int i = 0; i < 2; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabEnemigo, Hijo("Enemigos"));
                go.name = nombre + suf[i];
                go.transform.position = tanque.transform.position;
                go.GetComponent<AiBrain>().SetPatrolWaypoints(null);
                tripulacion[i] = go.GetComponent<Soldier>();
                enemigos.Add(tripulacion[i]);
            }
            var v = tanque.GetComponent<Vehicle>();
            v.ConfigurarTripulacion(tripulacion, new[] { VehicleSeatRole.Driver, VehicleSeatRole.Passenger2 }, true);
            var so = new SerializedObject(v); so.FindProperty("maxHealth").intValue = 360; so.ApplyModifiedPropertiesWithoutUndo();
            var pts = new Vector3[ruta.Length];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector3(ruta[i].x, 0.6f, ruta[i].y);
            tanque.GetComponent<VehicleBrain>().ConfigurarPatrulla(pts, true, 0.5f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(v);
            EditorUtility.SetDirty(v);
        }

        // ---------------------------------------------------------------- base comun
        static List<Soldier> escuadra;
        static Vehicle blindado;

        static void Base(string nombre, float anchoX, float largoZ, Vector3 spawnEscuadra)
        {
            matCache.Clear(); enemigos.Clear(); spawn = null;
            HeadlessTestRunner.BuildDemoScene();

            // Vaciar el mundo de demo: obstaculos, bidones, patrullas y sus rondas (los iconos huerfanos del minimapa tambien).
            foreach (var o in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (o == null) continue;
                string n = o.name;
                if (o.parent == null && (n.StartsWith("Obstaculo_") || n.StartsWith("Bidon_") || n.StartsWith("Enemigo_Patrulla_") || n == "Enemigo_Demo" || n == "PatrolRoute"))
                    Object.DestroyImmediate(o.gameObject);
            }
            foreach (var i in Object.FindObjectsByType<MinimapIcon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (i != null && i.Target == null) Object.DestroyImmediate(i.gameObject);

            raiz = new GameObject("Nivel_" + nombre).transform;

            var suelo = GameObject.Find("Ground");
            suelo.transform.position = new Vector3(0f, -0.5f, 0f);
            suelo.transform.localScale = new Vector3(anchoX, 1f, largoZ);
            suelo.GetComponent<MeshRenderer>().sharedMaterial = MatDe("Piso");
            var surface = suelo.GetComponent<NavMeshSurface>() ?? suelo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;

            pool = Object.FindFirstObjectByType<ProjectilePool>();
            escuadra = new List<Soldier>();
            Soldier vega = null;
            foreach (var s in Object.FindObjectsByType<Soldier>(FindObjectsSortMode.None))
                if (s.Team == TeamId.Player) { escuadra.Add(s); if (s.name.Contains("Vega")) vega = s; }
            prefabSoldado = PrefabUtility.GetCorrespondingObjectFromSource(vega.gameObject);
            string[] orden = { "Vega", "Kes", "Doc" };
            for (int i = 0; i < orden.Length; i++)
                foreach (var s in escuadra)
                    if (s.name.Contains(orden[i])) s.transform.position = spawnEscuadra + new Vector3((i - 1) * 2.2f, 0.8f, i == 0 ? 0f : -1.5f);

            blindado = Object.FindFirstObjectByType<Vehicle>();
            blindado.transform.position = spawnEscuadra + new Vector3(8f, 0.6f, -2f);
            blindado.transform.rotation = Quaternion.identity;
            foreach (var p in Object.FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))
                p.transform.position = spawnEscuadra + new Vector3(-6f + (p.name.EndsWith("Rifle") ? 0f : p.name.EndsWith("Pistola") ? 1.2f : 2.4f), 0.4f, 4f);

            Zona("Zona_Aliada", "ZonaAliada", spawnEscuadra.x, spawnEscuadra.z, 14f, 10f, spawnEscuadra.y);
        }

        // Cierra la escena: persiste materiales transitorios, enlaza enemigos, hornea NavMesh y guarda.
        static string Cerrar(string nombre, string resumen)
        {
            LevelBlockoutBuilder.AjustarAlcancesDeCombate();

            // Enlazar la lista de enemigos en todo componente que la tenga (BattleManager, estado de mision...).
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                var f = mb.GetType().GetField("Enemies", BindingFlags.Public | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(List<Soldier>)) f.SetValue(mb, new List<Soldier>(enemigos));
                var pr = mb.GetType().GetProperty("Enemies", BindingFlags.Public | BindingFlags.Instance);
                if (pr != null && pr.CanWrite && pr.PropertyType == typeof(List<Soldier>)) pr.SetValue(mb, new List<Soldier>(enemigos));
            }

            PersistirMateriales();
            Physics.SyncTransforms();
            Object.FindFirstObjectByType<NavMeshSurface>().BuildNavMesh();
            SP.Core.NavService.Invalidate(); SP.Core.ActorRegistry.Invalidate();

            var escena = SceneManager.GetActiveScene();
            string ruta = Dir + nombre + ".unity";
            EditorSceneManager.SaveScene(escena, ruta);
            return $"{nombre}: {enemigos.Count} enemigos. {resumen}";
        }

        // Los materiales de la base de demo son transitorios (se destruyen entre corridas de la suite): se copian a assets.
        static void PersistirMateriales()
        {
            var copias = new Dictionary<Material, Material>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r.GetComponent<TextMesh>() != null) continue;
                var mats = r.sharedMaterials; bool cambio = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m))) continue;
                    if (!copias.TryGetValue(m, out var copia))
                    {
                        string clave = "Auto_" + m.shader.name.Replace('/', '_').Replace(' ', '_') + "_" + (m.HasProperty("_BaseColor") ? ColorUtility.ToHtmlStringRGBA(m.GetColor("_BaseColor")) : "x");
                        string ruta = MatDir + "M_" + clave + ".mat";
                        copia = AssetDatabase.LoadAssetAtPath<Material>(ruta);
                        if (copia == null) { copia = new Material(m) { name = "M_" + clave }; AssetDatabase.CreateAsset(copia, ruta); }
                        copias[m] = copia;
                    }
                    mats[i] = copia; cambio = true;
                }
                if (cambio) r.sharedMaterials = mats;
            }
        }

        public static string Generar(int n)
        {
            switch (n)
            {
                case 1: return Escena1();
                case 2: return Escena2();
                case 3: return Escena3();
                case 4: return Escena4();
                default: return Escena5();
            }
        }

        // =====================================================================================================================
        // 1. EL FORTIN DEL CUELLO DE BOTELLA: Asalto (abre brechas) + Medico (fosos de curacion) + Francotirador (contra la torre).
        // =====================================================================================================================
        static string Escena1()
        {
            Base("Blockout_01", 90f, 100f, new Vector3(0f, 0f, -42f));
            Letrero("FORTIN DEL CUELLO DE BOTELLA\nObjetivo: tomar la RADIO (zona amarilla) al fondo", 0f, -34f, 7f);
            // Campo de tiro con coberturas dispersas y dos fosos de curacion en el flanco aliado.
            Cob(-22f, -26f); Cob(-6f, -22f, false); Cob(14f, -24f); Cob(28f, -18f, false); Cob(-30f, -14f); Cob(2f, -12f); Cob(-14f, -8f, false);
            foreach (var sx in new[] { -1f, 1f })
            {
                Cubo("Foso_Fondo", "Cobertura", sx * 32f, -32f, 6f, 1.2f, 1f, 0f, true, 300);
                Cubo("Foso_Lado1", "Cobertura", sx * 35f, -29.5f, 1f, 1.2f, 4f, 0f, true, 300);
                Cubo("Foso_Lado2", "Cobertura", sx * 29f, -29.5f, 1f, 1.2f, 4f, 0f, true, 300);
                Zona("Zona_Curacion", "Curacion", sx * 32f, -29.5f, 5f, 4f);
            }
            Letrero("FOSO DE CURACION\n(Medico)", -32f, -26f, 3f, 0.3f); Letrero("FOSO DE CURACION\n(Medico)", 32f, -26f, 3f, 0.3f);
            // Muralla con puerta central (cuello de botella), dos BRECHAS rompibles (Asalto) y dos pasos laterales angostos.
            const float zm = 0f;
            Muro("Muralla_O_Lejos", -45f, -38f, zm); Muro("Muralla_O", -36f, -26f, zm); Muro("Brecha_O", -26f, -22f, zm, 4f, 2f, "Brecha", 220);
            Muro("Muralla_Centro_O", -22f, -4f, zm); Muro("Muralla_Centro_E", 4f, 22f, zm);
            Muro("Brecha_E", 22f, 26f, zm, 4f, 2f, "Brecha", 220); Muro("Muralla_E", 26f, 36f, zm); Muro("Muralla_E_Lejos", 38f, 45f, zm);
            Erizo(-2.5f, -3f); Erizo(2.5f, -3f);
            Cubo("Serpenteo_O", "Cobertura", -37f, -3f, 1f, 1.6f, 5f, 0f, true, 500); Cubo("Serpenteo_E", "Cobertura", 37f, 3f, 1f, 1.6f, 5f, 0f, true, 500);
            Zona("Zona_Enemiga", "ZonaEnemiga", 0f, 20f, 60f, 36f);
            // Patio interior: contenedores, torre de vigia con rampa, muro trasero con puerta.
            Cubo("Contenedor_1", "Contenedor", -24f, 12f, 6f, 2.8f, 2.5f); Cubo("Contenedor_2", "Contenedor", 22f, 14f, 2.5f, 2.8f, 6f); Cubo("Contenedor_3", "Contenedor", -10f, 30f, 6f, 2.8f, 2.5f);
            Cob(10f, 8f); Cob(-4f, 16f, false); Cob(16f, 24f); Cob(-18f, 22f, false);
            Plataforma("Torre_Vigia", 0f, 24f, 8f, 8f, 3f);
            Rampa("Rampa_Torre_O", 0f, 14f, 3f, 8f, 3f, 0f);   // sube de sur a norte hasta la torre
            Cubo("Parapeto_Torre", "Cobertura", 0f, 27.5f, 8f, 1f, 1f, 3f, false);
            Muro("Muro_Trasero_O", -45f, -4f, 36f); Muro("Muro_Trasero_E", 4f, 45f, 36f);
            Zona("Objetivo_Radio", "Objetivo", 0f, 42f, 12f, 10f);
            Cubo("Radio_Caseta", "Torre", 0f, 46f, 4f, 3f, 3f);
            Letrero("RADIO", 0f, 42f, 5f, 0.8f);
            // Guarnicion: 2 guardias en la puerta, 3 de ronda, francotirador en la torre, 2 flanqueadores, 1 en la radio.
            Enemigo("Enemigo_Guardia_Puerta_1", RoleType.Enemy, -6f, 4f); Enemigo("Enemigo_Guardia_Puerta_2", RoleType.Enemy, 6f, 4f);
            Enemigo("Enemigo_Ronda_1", RoleType.Enemy, -28f, 20f, 0f, Rect(-28f, 20f, 6f, 5f)); Enemigo("Enemigo_Ronda_2", RoleType.Enemy, 26f, 22f, 0f, Rect(26f, 22f, 6f, 5f));
            Enemigo("Enemigo_Ronda_3", RoleType.Enemy, 0f, 12f, 0f, Rect(0f, 12f, 8f, 3f));
            Enemigo("Enemigo_Vigia_Torre", RoleType.Sniper, 0f, 24f, 3f);
            Enemigo("Enemigo_Flanker_O", RoleType.Flanker, -34f, 28f, 0f, Rect(-34f, 28f, 3f, 6f)); Enemigo("Enemigo_Flanker_E", RoleType.Flanker, 34f, 30f, 0f, Rect(34f, 30f, 3f, 6f));
            Enemigo("Enemigo_Radio", RoleType.Enemy, 0f, 40f);
            return Cerrar("SC_Blockout_01", "Asalto: brechas. Medico: fosos. Francotirador: contra la torre. Gancho: 3 formas de entrar (puerta / brechas / flancos).");
        }

        // =====================================================================================================================
        // 2. VALLE DEL FRANCOTIRADOR: nido aliado en altura, torreta fija a mitad de valle, dos nidos enemigos con rampa trasera.
        // =====================================================================================================================
        static string Escena2()
        {
            Base("Blockout_02", 70f, 150f, new Vector3(0f, 0f, -68f));
            Letrero("VALLE DEL FRANCOTIRADOR\nObjetivo: llegar al DEPOSITO (zona amarilla) al norte", 0f, -60f, 7f);
            // Crestas que encierran el valle (x=+-30) con dos aberturas a corredores laterales (z=-5 y z=45).
            foreach (var sx in new[] { -1f, 1f })
            {
                MuroZ("Cresta_1", -75f, -8f, sx * 30f, 5f, 3f); MuroZ("Cresta_2", -2f, 42f, sx * 30f, 5f, 3f); MuroZ("Cresta_3", 48f, 75f, sx * 30f, 5f, 3f);
                Cubo("Corredor_Muro", "Muro", sx * 33.5f, 20f, 1f, 3f, 120f);
            }
            // Nido del francotirador aliado (-24,-40) con rampa por el sur.
            Plataforma("Nido_Aliado", -24f, -42f, 7f, 6f, 3f); Rampa("Rampa_Nido_Aliado", -24f, -52f, 3f, 7f, 3f, 0f);
            Cubo("Parapeto_Nido", "Cobertura", -24f, -39.5f, 7f, 1f, 1f, 3f, false);
            Letrero("NIDO DEL FRANCOTIRADOR", -24f, -42f, 6f, 0.4f);
            // Torreta fija aliada a mitad de valle, tras sacos.
            Torreta("Torreta_Valle", 0f, -20f, 0f); Cubo("Sacos_Torreta", "Cobertura", 0f, -17.5f, 6f, 0.9f, 1f, 0f, true, 300);
            Letrero("TORRETA FIJA (E)", 0f, -20f, 4f, 0.4f);
            // Rocas/coberturas escalonadas cada ~10 m.
            float[][] rocas = { new[] { -14f, -30f }, new[] { 12f, -28f }, new[] { -8f, -6f }, new[] { 14f, 0f }, new[] { -16f, 8f }, new[] { 4f, 16f }, new[] { -10f, 28f }, new[] { 16f, 34f }, new[] { 0f, 44f } };
            foreach (var r in rocas) Cubo("Roca", "Cobertura", r[0], r[1], 2.4f, 1.8f, 2.4f, 0f, true, 500);
            // Nidos enemigos con rampa TRASERA (norte): (22,10) y (-22,35).
            Plataforma("Nido_Enemigo_E", 22f, 10f, 6f, 6f, 3.5f); Rampa("Rampa_Nido_E", 22f, 21f, 3f, 8f, 3.5f, 180f);
            Plataforma("Nido_Enemigo_O", -22f, 35f, 6f, 6f, 3.5f); Rampa("Rampa_Nido_O", -22f, 46f, 3f, 8f, 3.5f, 180f);
            Cubo("Parapeto_E", "Cobertura", 22f, 12f, 6f, 1f, 1f, 3.5f, false); Cubo("Parapeto_O", "Cobertura", -22f, 37f, 6f, 1f, 1f, 3.5f, false);
            Zona("Zona_Enemiga", "ZonaEnemiga", 0f, 20f, 50f, 70f);
            // Deposito.
            Cubo("Deposito_Edificio", "Casa", 0f, 66f, 14f, 5f, 8f); Cubo("Deposito_Caja1", "Contenedor", -10f, 58f, 4f, 2.8f, 2.5f); Cubo("Deposito_Caja2", "Contenedor", 10f, 60f, 4f, 2.8f, 2.5f);
            Zona("Objetivo_Deposito", "Objetivo", 0f, 56f, 14f, 8f); Letrero("DEPOSITO", 0f, 56f, 5f, 0.8f);
            Enemigo("Enemigo_Vigia_E", RoleType.Sniper, 22f, 10f, 3.5f); Enemigo("Enemigo_Vigia_O", RoleType.Sniper, -22f, 35f, 3.5f);
            Enemigo("Enemigo_Guardia_E1", RoleType.Enemy, 18f, 6f); Enemigo("Enemigo_Guardia_E2", RoleType.Enemy, 26f, 6f);
            Enemigo("Enemigo_Guardia_O1", RoleType.Enemy, -26f, 31f); Enemigo("Enemigo_Guardia_O2", RoleType.Enemy, -18f, 31f);
            Enemigo("Enemigo_Deposito_1", RoleType.Enemy, -6f, 54f, 0f, Rect(-6f, 54f, 6f, 3f)); Enemigo("Enemigo_Deposito_2", RoleType.Enemy, 6f, 56f, 0f, Rect(6f, 56f, 6f, 3f)); Enemigo("Enemigo_Deposito_3", RoleType.Enemy, 0f, 50f);
            Enemigo("Enemigo_Flanker_O", RoleType.Flanker, -32f, 10f, 0f, Rect(-32f, 20f, 1.5f, 25f)); Enemigo("Enemigo_Flanker_E", RoleType.Flanker, 32f, 40f, 0f, Rect(32f, 20f, 1.5f, 25f));
            return Cerrar("SC_Blockout_02", "Francotirador: nido aliado vs 2 nidos enemigos. Torreta fija a mitad. Flankers por los corredores. Gancho: duelo de alturas.");
        }

        // =====================================================================================================================
        // 3. CONVOY BLINDADO: el blindado aliado rompe un muro, cruza el puente y destruye al tanque enemigo.
        // =====================================================================================================================
        static string Escena3()
        {
            Base("Blockout_03", 64f, 170f, new Vector3(0f, 0f, -76f));
            Letrero("CONVOY BLINDADO\nObjetivo: destruir el TANQUE ENEMIGO y llegar al DEPOSITO", 0f, -68f, 7f);
            blindado.transform.position = new Vector3(0f, 0.6f, -70f);
            // Muralla de la carretera con muro DEBIL al centro (lo rompe el blindado) y paso de infanteria por el oeste.
            Muro("Barricada_O", -32f, -8f, -40f, 3f); Muro("Barricada_Brecha", -8f, 8f, -40f, 3f, 2f, "Brecha", 400); Muro("Barricada_E", 8f, 32f, -40f, 3f);
            Cubo("Paso_Peatonal_Cob", "Cobertura", -29f, -37f, 3f, 1.2f, 1f, 0f, true, 300);
            Torreta("Torreta_Brecha", -14f, -50f, 0f); Cubo("Sacos_Torreta", "Cobertura", -14f, -47.5f, 5f, 0.9f, 1f, 0f, true, 300);
            Letrero("TORRETA FIJA: cubre la brecha", -14f, -50f, 4f, 0.4f);
            // Carretera central con barreras y coches.
            Zona("Carretera", "Torreta", 0f, 0f, 14f, 160f);
            foreach (var z in new[] { -24f, -4f, 14f }) { Cubo("Barrera", "Contenedor", -9f, z, 2.5f, 1.4f, 5f, 0f, true, 400); Cubo("Barrera", "Contenedor", 9f, z + 6f, 2.5f, 1.4f, 5f, 0f, true, 400); }
            // PUENTE: dos muros laterales obligan a cruzar por la losa elevada (rampas a los dos lados).
            Muro("Puente_Muro_O", -32f, -8f, 38f, 5f, 6f); Muro("Puente_Muro_E", 8f, 32f, 38f, 5f, 6f);
            Plataforma("Puente_Losa", 0f, 38f, 16f, 6f, 1.6f); Rampa("Puente_Rampa_S", 0f, 26f, 8f, 9f, 1.6f, 0f); Rampa("Puente_Rampa_N", 0f, 51f, 8f, 10f, 1.6f, 180f);
            // Torres de vigia junto a la salida del puente.
            Plataforma("Torre_O", -24f, 54f, 6f, 6f, 3f); Rampa("Rampa_Torre_O", -24f, 44f, 3f, 7f, 3f, 0f);
            Plataforma("Torre_E", 24f, 54f, 6f, 6f, 3f); Rampa("Rampa_Torre_E", 24f, 44f, 3f, 7f, 3f, 0f);
            Zona("Zona_Enemiga", "ZonaEnemiga", 0f, 62f, 50f, 24f);
            Cubo("Contenedor_Salida_O", "Contenedor", -12f, 62f, 6f, 2.8f, 2.5f); Cubo("Contenedor_Salida_E", "Contenedor", 14f, 66f, 6f, 2.8f, 2.5f);
            Cubo("Deposito", "Casa", 0f, 82f, 18f, 5f, 8f); Zona("Objetivo_Deposito", "Objetivo", 0f, 74f, 16f, 8f); Letrero("DEPOSITO", 0f, 74f, 5f, 0.8f);
            TanqueEnemigo("Tanque_Enemigo", 0f, 60f, 180f, new[] { new Vector2(0f, 62f), new Vector2(0f, 72f), new Vector2(-6f, 66f) });
            Enemigo("Enemigo_Vigia_O", RoleType.Sniper, -24f, 54f, 3f); Enemigo("Enemigo_Vigia_E", RoleType.Sniper, 24f, 54f, 3f);
            Enemigo("Enemigo_Contenedor_O", RoleType.Enemy, -14f, 64f); Enemigo("Enemigo_Contenedor_E", RoleType.Enemy, 14f, 64f);
            Enemigo("Enemigo_Camino_1", RoleType.Enemy, -9f, 0f, 0f, Rect(-9f, 0f, 3f, 12f)); Enemigo("Enemigo_Camino_2", RoleType.Enemy, 9f, 12f, 0f, Rect(9f, 12f, 3f, 12f));
            Enemigo("Enemigo_Flanker_O", RoleType.Flanker, -26f, 20f, 0f, Rect(-26f, 10f, 2f, 20f)); Enemigo("Enemigo_Deposito", RoleType.Enemy, 0f, 78f);
            return Cerrar("SC_Blockout_03", "Vehiculo: romper la brecha y cruzar el puente. Torreta fija cubre la brecha. Gancho: duelo de blindados y puente obligado.");
        }

        // =====================================================================================================================
        // 4. RESCATE EN LA ALDEA: el civil esta en la plaza; sacarlo vivo hasta la zona aliada. Medico + Flankers.
        // =====================================================================================================================
        static string Escena4()
        {
            Base("Blockout_04", 104f, 104f, new Vector3(-44f, 0f, -44f));
            Letrero("RESCATE EN LA ALDEA\nObjetivo: llegar al REHEN (centro) y volver a la ZONA AZUL", -30f, -38f, 7f);
            // Cuadricula de casas 3x3 (calles de 8 m); la central es la PLAZA abierta (muros bajos en U, puerta al sur).
            float[] c = { -28f, 0f, 28f };
            foreach (var cx in c)
                foreach (var cz in c)
                {
                    if (cx == 0f && cz == 0f) continue;
                    if (cx == -28f && cz == -28f) continue;   // deja libre la salida del spawn
                    if (cx == 28f && cz == 28f) continue;     // ahi va la azotea con rampa
                    Cubo($"Casa_{cx}_{cz}", "Casa", cx, cz, 14f, 5f, 12f);
                }
            Cubo("Plaza_Muro_N", "Muro", 0f, 6f, 12f, 1.6f, 1f, 0f, true, 400); Cubo("Plaza_Muro_O", "Muro", -6f, 0f, 1f, 1.6f, 12f, 0f, true, 400); Cubo("Plaza_Muro_E", "Muro", 6f, 0f, 1f, 1.6f, 12f, 0f, true, 400);
            Cubo("Plaza_Muro_S_O", "Muro", -4f, -6f, 4f, 1.6f, 1f, 0f, true, 400); Cubo("Plaza_Muro_S_E", "Muro", 4f, -6f, 4f, 1.6f, 1f, 0f, true, 400);
            Zona("Objetivo_Rehen", "Objetivo", 0f, 0f, 10f, 10f); Letrero("REHEN", 0f, 0f, 4f, 0.6f);
            // Azotea con rampa en la esquina NE (francotirador enemigo) y barricadas en las calles.
            Plataforma("Azotea_NE", 28f, 28f, 14f, 12f, 5f); Rampa("Rampa_Azotea", 28f, 15f, 3f, 9f, 5f, 0f);
            Cob(-14f, -14f); Cob(14f, -14f, false); Cob(-14f, 14f, false); Cob(14f, 14f); Cob(0f, -22f); Cob(-22f, 0f, false); Cob(22f, 0f, false); Cob(0f, 22f);
            Zona("Zona_Enemiga", "ZonaEnemiga", 0f, 0f, 70f, 70f);
            // Rehen: civil del equipo aliado que sigue al jugador una vez liberado.
            spawn = typeof(HeadlessTestRunner).GetMethod("SpawnSoldier", BindingFlags.NonPublic | BindingFlags.Static);
            var civil = (Soldier)spawn.Invoke(null, new object[] { prefabSoldado, "Rehen_Civil", TeamId.Player, RoleType.Civilian, new Vector3(0f, 0.8f, 0f), ColCivil, pool, 60 });
            civil.transform.SetParent(Hijo("Rehen"), true); civil.gameObject.AddComponent<Rehen>();
            Enemigo("Enemigo_Vigia_Azotea", RoleType.Sniper, 28f, 28f, 5f);
            Enemigo("Enemigo_Calle_N", RoleType.Enemy, -14f, 24f, 0f, Rect(-14f, 24f, 12f, 2f)); Enemigo("Enemigo_Calle_E", RoleType.Enemy, 24f, -14f, 0f, Rect(24f, -14f, 2f, 12f));
            Enemigo("Enemigo_Plaza_1", RoleType.Enemy, -3f, 4f); Enemigo("Enemigo_Plaza_2", RoleType.Enemy, 3f, 4f);
            Enemigo("Enemigo_Flanker_1", RoleType.Flanker, -40f, 10f, 0f, Rect(-40f, 10f, 3f, 24f)); Enemigo("Enemigo_Flanker_2", RoleType.Flanker, 10f, -40f, 0f, Rect(10f, -40f, 24f, 3f));
            Enemigo("Enemigo_Flanker_3", RoleType.Flanker, 40f, 0f, 0f, Rect(40f, 0f, 3f, 24f));
            return Cerrar("SC_Blockout_04", "Civil: el rehen se une al escuadron. Medico: curar en el regreso. Flankers por la periferia. Gancho: ir y volver con el rehen herido.");
        }

        // =====================================================================================================================
        // 5. ASEDIO DE LA COLINA: la escuadra defiende la cima con dos torretas fijas; ataques desde 3 frentes + francotirador.
        // =====================================================================================================================
        static string Escena5()
        {
            Base("Blockout_05", 110f, 110f, new Vector3(0f, 4.5f, -3f));
            Letrero("ASEDIO DE LA COLINA\nObjetivo: resistir y eliminar a los atacantes desde la cima", 0f, -8f, 11f);
            // Colina en 3 terrazas (el techo de cada una se camina) con rampas sur y norte que arrancan sobre la terraza de abajo.
            Plataforma("Terraza_1", 0f, 0f, 36f, 36f, 1.5f); Plataforma("Terraza_2", 0f, 0f, 24f, 24f, 3f); Plataforma("Terraza_3", 0f, 0f, 14f, 14f, 4.5f);
            Rampa("Rampa_S1", 0f, -28f, 5f, 10f, 1.5f, 0f); Rampa("Rampa_S2", 0f, -18f, 5f, 6f, 1.5f, 0f, 1.5f); Rampa("Rampa_S3", 0f, -12f, 5f, 6f, 1.5f, 0f, 3f);
            Rampa("Rampa_N1", 0f, 28f, 5f, 10f, 1.5f, 180f); Rampa("Rampa_N2", 0f, 18f, 5f, 6f, 1.5f, 180f, 1.5f); Rampa("Rampa_N3", 0f, 12f, 5f, 6f, 1.5f, 180f, 3f);
            Torreta("Torreta_Cima_O", -5f, 2f, 180f, 4.5f); Torreta("Torreta_Cima_E", 5f, 2f, 180f, 4.5f);
            Letrero("TORRETAS FIJAS (E)", 0f, 3f, 9f, 0.5f);
            Cubo("Sacos_Cima_N", "Cobertura", 0f, 6.5f, 10f, 0.9f, 1f, 4.5f, false); Zona("Zona_Curacion", "Curacion", 0f, 3f, 6f, 3f, 4.5f);
            blindado.transform.position = new Vector3(-12f, 0.6f, -34f);
            // Cobertura al pie de la colina, repartida en anillo (los atacantes se cubren al acercarse).
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f, r = 30f;
                Cubo("Roca_Anillo", "Cobertura", Mathf.Cos(a) * r, Mathf.Sin(a) * r, 3f, 1.4f, 2f, 0f, true, 500, -a * Mathf.Rad2Deg);
            }
            Plataforma("Nido_Norte", 0f, 46f, 8f, 6f, 3.5f); Rampa("Rampa_Nido", 0f, 38f, 3f, 7f, 3.5f, 0f);
            Zona("Zona_Enemiga", "ZonaEnemiga", 0f, 40f, 80f, 24f);
            // Tres frentes: oeste, este y norte. Rondas cortas hacia la colina; vision 22 los hace asediar al verla.
            Enemigo("Enemigo_Vigia_Norte", RoleType.Sniper, 0f, 46f, 3.5f);
            Enemigo("Enemigo_Oeste_1", RoleType.Enemy, -44f, 4f, 0f, Rect(-40f, 4f, 6f, 6f)); Enemigo("Enemigo_Oeste_2", RoleType.Enemy, -44f, -6f, 0f, Rect(-40f, -6f, 6f, 6f)); Enemigo("Enemigo_Oeste_3", RoleType.Enemy, -48f, 0f);
            Enemigo("Enemigo_Este_1", RoleType.Enemy, 44f, 4f, 0f, Rect(40f, 4f, 6f, 6f)); Enemigo("Enemigo_Este_2", RoleType.Enemy, 44f, -6f, 0f, Rect(40f, -6f, 6f, 6f)); Enemigo("Enemigo_Este_3", RoleType.Enemy, 48f, 0f);
            Enemigo("Enemigo_Norte_1", RoleType.Enemy, -10f, 36f, 0f, Rect(-10f, 36f, 6f, 4f)); Enemigo("Enemigo_Norte_2", RoleType.Enemy, 10f, 36f, 0f, Rect(10f, 36f, 6f, 4f));
            Enemigo("Enemigo_Flanker_O", RoleType.Flanker, -30f, 30f, 0f, Rect(-30f, 30f, 6f, 6f)); Enemigo("Enemigo_Flanker_E", RoleType.Flanker, 30f, 30f, 0f, Rect(30f, 30f, 6f, 6f));
            return Cerrar("SC_Blockout_05", "Torretas fijas en la cima. Medico: zona de curacion en la cima. Francotirador enemigo en el nido norte. Gancho: defensa en altura con 3 frentes.");
        }
    }
}
