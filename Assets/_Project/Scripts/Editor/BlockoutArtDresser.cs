using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SP.Presentation;

namespace SP.EditorTools
{
    // "Implementa arte en todo": viste el BLOCKOUT (cubos grises) con los prefabs
    // de arte de Assets/_Project/Prefabs/ArteMundo (modulos de muro, ventanas,
    // puertas, losas, barricadas de sacos, cajas, autos, emplazamientos...).
    //
    // Cada cubo del blockout conserva su collider, su ObstacleMarker y todo lo que
    // lo hace jugable; solo se apaga su MeshRenderer y se le cuelga un hijo
    // "ArteBloque" que CANCELA la escala del cubo (asi los modulos se colocan en
    // metros reales) con los modulos del arte. Como el arte es hijo del cubo:
    //   - al derrumbarse el obstaculo (SetActive(false)) el arte desaparece con el,
    //   - en las etapas de daño (que aplastan la escala Y del cubo) el arte se aplasta.
    // A los modulos se les quita el collider: manda el del cubo.
    //
    // Ademas se reparte vegetacion, barriles, faroles, cráteres y escombros por el
    // campo (ArteMundo/Ambiente), lejos de la ruta central y de los cubos.
    //
    // Es idempotente: correrlo de nuevo borra lo anterior y lo rehace.
    public static class BlockoutArtDresser
    {
        const string Dir = "Assets/_Project/Prefabs/ArteMundo/";
        const string RaizNombre = "ArteBloque";

        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        static GameObject P(string nombre)
        {
            if (!cache.TryGetValue(nombre, out var g) || g == null)
            {
                g = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + nombre + ".prefab");
                cache[nombre] = g;
            }
            return g;
        }

        [MenuItem("Strategic Point/Arte/9. Vestir el blockout con arte (escena abierta)")]
        public static void VestirEscenaAbierta()
        {
            cache.Clear();
            var escena = SceneManager.GetActiveScene();
            int vestidos = 0, modulos = 0;

            // 1) limpiar lo anterior
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t != null && t.name == RaizNombre) Object.DestroyImmediate(t.gameObject);
            var ambienteViejo = GameObject.Find("ArteMundo/Ambiente");
            if (ambienteViejo != null)
                for (int i = ambienteViejo.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(ambienteViejo.transform.GetChild(i).gameObject);

            // 2) vestir cada cubo del blockout
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                if (mr == null || mr.sharedMaterial == null) continue;
                string mat = mr.sharedMaterial.name;
                string clave = null;
                if (mat.StartsWith("M_Blocking_")) clave = mat.Substring("M_Blocking_".Length);
                else if (mr.transform.parent != null && mr.transform.parent.name == "Destructibles")
                    clave = mr.name.StartsWith("Bidon") ? "Bidon" : "Caja";
                if (clave == null) continue;
                if (mr.transform.rotation != Quaternion.identity) continue;   // solo cubos sin girar

                int n = Vestir(mr, clave);
                if (n > 0) { vestidos++; modulos += n; }
            }

            // 3) ambiente
            int props = Repartir();

            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            Debug.Log($"[BlockoutArtDresser] {escena.name}: {vestidos} cubos vestidos con {modulos} modulos de arte + {props} props de ambiente.");
        }

        // ------------------------------------------------------------------
        static int Vestir(MeshRenderer mr, string clave)
        {
            var cubo = mr.transform;
            var s = cubo.localScale;
            var raiz = new GameObject(RaizNombre).transform;
            raiz.SetParent(cubo, false);
            raiz.localPosition = Vector3.zero;
            raiz.localRotation = Quaternion.identity;
            raiz.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);   // cancela la escala: metros reales
            float suelo = -s.y * 0.5f;
            int n = 0;
            string nombre = cubo.name;
            int hash = 0; foreach (char c in nombre) hash = hash * 31 + c;
            hash = Mathf.Abs(hash);

            switch (clave)
            {
                case "Muro": n = Muro(raiz, s, suelo, "P_Mod_Muro_Recto", true); break;
                case "Brecha": n = Muro(raiz, s, suelo, "P_Mod_Muro_RotoAlto", false); break;
                case "Casa": n = Edificio(raiz, s, suelo, false, cubo.position); break;
                case "Torre": n = Edificio(raiz, s, suelo, true, cubo.position); break;
                case "Bidon": n = Ajustar(raiz, "P_Env_Barril", Vector3.zero, s, suelo, 0f); break;
                case "Caja": n = Cajas(raiz, s, suelo); break;
                default: n = Cobertura(raiz, s, suelo, nombre, hash); break;
            }

            if (n > 0) mr.enabled = false;
            else Object.DestroyImmediate(raiz.gameObject);
            return n;
        }

        // Pone un modulo, sin colliders, con escala/posicion/giro dados (en metros locales del cubo).
        static Transform Poner(Transform raiz, string prefab, Vector3 pos, float yaw, Vector3 escala)
        {
            var g = P(prefab);
            if (g == null) return null;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(g, raiz);
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var t = inst.transform;
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            t.localScale = escala;
            return t;
        }

        // Estira un modulo para llenar exactamente una caja (ancho x alto x largo) centrada en `centro`.
        static int Ajustar(Transform raiz, string prefab, Vector3 centro, Vector3 caja, float suelo, float yaw)
        {
            var g = P(prefab);
            if (g == null) return 0;
            var b = BoundsDe(g);
            if (b.size.x < 0.01f || b.size.y < 0.01f || b.size.z < 0.01f) return 0;
            float k = 1f;
            bool girado = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) > 0.5f;
            float cx = girado ? caja.z : caja.x, cz = girado ? caja.x : caja.z;
            var escala = new Vector3(cx / b.size.x, caja.y / b.size.y * k, cz / b.size.z);
            return Poner(raiz, prefab, new Vector3(centro.x, suelo, centro.z), yaw, escala) != null ? 1 : 0;
        }

        static Bounds BoundsDe(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>(true);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // Muro corrido a lo largo del eje mayor del cubo.
        static int Muro(Transform raiz, Vector3 s, float suelo, string modulo, bool pilares)
        {
            bool aX = s.x >= s.z;
            float L = aX ? s.x : s.z, T = aX ? s.z : s.x;
            int cant = Mathf.Max(1, Mathf.RoundToInt(L / 3.83f));
            float sx = L / (cant * 3.83f);
            float sz = Mathf.Min(T / 0.39f, 2.2f);
            float sy = s.y / 2.87f;
            int n = 0;
            for (int i = 0; i < cant; i++)
            {
                float u = -L * 0.5f + (i + 0.5f) * L / cant;
                var pos = aX ? new Vector3(u, suelo, 0f) : new Vector3(0f, suelo, u);
                if (Poner(raiz, modulo, pos, aX ? 0f : 90f, new Vector3(sx, sy, sz)) != null) n++;
            }
            if (pilares)
            {
                float ps = Mathf.Min(T / 0.56f, 2.6f);
                for (int k = 0; k <= cant; k += Mathf.Max(1, Mathf.Min(cant, 6)))
                {
                    float u = -L * 0.5f + k * L / cant;
                    var pos = aX ? new Vector3(u, suelo, 0f) : new Vector3(0f, suelo, u);
                    if (Poner(raiz, "P_Mod_Muro_Pilar", pos, 0f, new Vector3(ps, sy, ps)) != null) n++;
                }
            }
            return n;
        }

        // Casa / cuartel / torre: 4 paredes de modulos (puerta mirando a la ruta central,
        // ventanas alternadas), pilares en las esquinas y techo de losas.
        static int Edificio(Transform raiz, Vector3 s, float suelo, bool torre, Vector3 mundo)
        {
            float t = 0.39f * 1.5f;
            float sy = s.y / 2.87f;
            int n = 0;
            bool puertaEste = mundo.x < 4f;    // la puerta mira hacia el camino central

            // lados largos en X (caras +Z y -Z) y en Z (caras +X y -X)
            for (int lado = 0; lado < 4; lado++)
            {
                bool aX = lado < 2;
                float sign = (lado % 2 == 0) ? 1f : -1f;
                float L = aX ? s.x : s.z;
                float D = aX ? s.z : s.x;
                int cant = Mathf.Max(1, Mathf.RoundToInt(L / 3.83f));
                float sx = L / (cant * 3.83f);
                bool conPuerta = !torre && !aX && ((sign > 0f) == puertaEste);
                for (int i = 0; i < cant; i++)
                {
                    float u = -L * 0.5f + (i + 0.5f) * L / cant;
                    string mod = "P_Mod_Muro_Recto";
                    if (conPuerta && i == cant / 2) mod = "P_Mod_Muro_Puerta";
                    else if (!torre && i % 2 == 1) mod = "P_Mod_Muro_Ventana";
                    var pos = aX ? new Vector3(u, suelo, sign * (D * 0.5f - t * 0.5f)) : new Vector3(sign * (D * 0.5f - t * 0.5f), suelo, u);
                    if (Poner(raiz, mod, pos, aX ? 0f : 90f, new Vector3(sx, sy, 1.5f)) != null) n++;
                }
            }
            // pilares
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    if (Poner(raiz, "P_Mod_Muro_Pilar", new Vector3(sx * (s.x * 0.5f - 0.2f), suelo, sz * (s.z * 0.5f - 0.2f)), 0f, new Vector3(1.2f, sy, 1.2f)) != null) n++;
            // techo
            int nx = Mathf.Max(1, Mathf.CeilToInt(s.x / 3.83f)), nz = Mathf.Max(1, Mathf.CeilToInt(s.z / 3.83f));
            float lx = s.x / (nx * 3.83f), lz = s.z / (nz * 3.83f);
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    var pos = new Vector3(-s.x * 0.5f + (ix + 0.5f) * s.x / nx, suelo + s.y - 0.37f, -s.z * 0.5f + (iz + 0.5f) * s.z / nz);
                    if (Poner(raiz, "P_Mod_Losa", pos, 0f, new Vector3(lx, 1f, lz)) != null) n++;
                }
            if (torre && Poner(raiz, "P_Env_Emplazamiento_MG", new Vector3(0f, suelo + s.y, 0f), 0f, Vector3.one * 1.6f) != null) n++;
            return n;
        }

        static int Cajas(Transform raiz, Vector3 s, float suelo)
        {
            int nx = Mathf.Max(1, Mathf.RoundToInt(s.x / 0.94f)), nz = Mathf.Max(1, Mathf.RoundToInt(s.z / 0.94f)), ny = Mathf.Max(1, Mathf.RoundToInt(s.y / 0.77f));
            int n = 0;
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                    for (int iy = 0; iy < ny; iy++)
                    {
                        var pos = new Vector3(-s.x * 0.5f + (ix + 0.5f) * s.x / nx, suelo + iy * s.y / ny, -s.z * 0.5f + (iz + 0.5f) * s.z / nz);
                        if (Poner(raiz, "P_Env_Caja_Madera", pos, ((ix + iz + iy) % 3) * 7f, new Vector3(s.x / nx / 0.94f, s.y / ny / 0.77f, s.z / nz / 0.94f)) != null) n++;
                    }
            return n;
        }

        static int Cobertura(Transform raiz, Vector3 s, float suelo, string nombre, int hash)
        {
            bool aX = s.x >= s.z;
            float L = aX ? s.x : s.z, T = aX ? s.z : s.x;

            if (nombre.StartsWith("Caja")) return Cajas(raiz, s, suelo);
            if (nombre.StartsWith("Carro"))
                return Ajustar(raiz, "P_Env_Auto_Quemado", Vector3.zero, s, suelo, aX ? 90f : 0f);
            if (nombre.StartsWith("Pozo"))
                return Ajustar(raiz, "P_Env_Neumaticos", Vector3.zero, s, suelo, 0f);
            if (nombre.StartsWith("Bunker"))
            {
                int k = Mathf.Max(1, Mathf.RoundToInt(L / 2.34f));
                int total = 0;
                for (int i = 0; i < k; i++)
                {
                    float u = -L * 0.5f + (i + 0.5f) * L / k;
                    var pos = aX ? new Vector3(u, suelo, 0f) : new Vector3(0f, suelo, u);
                    var t = Poner(raiz, "P_Env_Barricada_Concreto", pos, aX ? 0f : 90f, new Vector3(L / k / 2.34f, s.y / 0.86f, Mathf.Min(T / 0.73f, 2.4f)));
                    if (t != null) total++;
                }
                if (Poner(raiz, "P_Env_Emplazamiento_MG", new Vector3(0f, suelo + s.y, 0f), aX ? 0f : 90f, Vector3.one * 1.4f) != null) total++;
                return total;
            }

            // Barricadas bajas: sacos de arena o bloques de concreto, segun el nombre.
            bool concreto = nombre.StartsWith("Barricada") || (hash % 3 == 0);
            string mod = concreto ? "P_Env_Barricada_Concreto" : "P_Env_Barricada_Sacos";
            float largoMod = concreto ? 2.34f : 3.74f, altoMod = concreto ? 0.86f : 0.67f, gruesoMod = concreto ? 0.73f : 0.36f;
            int cant = Mathf.Max(1, Mathf.RoundToInt(L / largoMod));
            int res = 0;
            for (int i = 0; i < cant; i++)
            {
                float u = -L * 0.5f + (i + 0.5f) * L / cant;
                var pos = aX ? new Vector3(u, suelo, 0f) : new Vector3(0f, suelo, u);
                if (Poner(raiz, mod, pos, aX ? 0f : 90f, new Vector3(L / cant / largoMod, s.y / altoMod, Mathf.Min(T / gruesoMod, concreto ? 1.6f : 2.6f))) != null) res++;
            }
            return res;
        }

        // ------------------------------------------------------------------
        // Ambiente: vegetacion, barriles, faroles, crateres y escombros.
        // ------------------------------------------------------------------
        static int Repartir()
        {
            var ambiente = GameObject.Find("ArteMundo/Ambiente");
            if (ambiente == null)
            {
                var arte = GameObject.Find("ArteMundo");
                if (arte == null) arte = new GameObject("ArteMundo");
                ambiente = new GameObject("Ambiente");
                ambiente.transform.SetParent(arte.transform, false);
            }
            Physics.SyncTransforms();
            var terreno = Terrain.activeTerrain;
            var rnd = new System.Random(4242);

            // Rectangulo jugable: se toma del terreno (o de los cubos si no hay).
            float x0 = -50f, x1 = 58f, z0 = -18f, z1 = 292f;
            if (terreno != null)
            {
                var o = terreno.transform.position; var sz = terreno.terrainData.size;
                x0 = o.x + 4f; x1 = o.x + sz.x - 4f; z0 = o.z + 4f; z1 = o.z + sz.z - 4f;
            }
            float ejeX = 4f;   // centro de la ruta
            // La cantidad de props se escala con el area jugable: el campo de tiro del tutorial
            // (70 x 210) no puede tener la misma densidad de arboles que el nivel completo (117 x 320).
            float factorArea = Mathf.Clamp(((x1 - x0) * (z1 - z0)) / 33000f, 0.25f, 1f);

            var lista = new (string prefab, int cantidad, bool lejosDeRuta, float escalaMin, float escalaMax)[]
            {
                ("P_Env_ArbolA", 46, true, 0.9f, 1.4f), ("P_Env_ArbolB", 38, true, 0.9f, 1.4f),
                ("P_Env_Arbusto_Grande", 34, true, 0.9f, 1.3f), ("P_Env_Arbusto_Medio", 44, true, 0.9f, 1.4f),
                ("P_Env_Barril", 10, false, 0.5f, 0.6f), ("P_Env_Neumaticos", 8, false, 0.9f, 1.1f),
                ("P_Env_Palet", 8, false, 1f, 1.2f), ("P_Env_Escombro_Pila_A", 10, false, 0.9f, 1.3f),
                ("P_Env_Crater", 12, false, 0.8f, 1.5f), ("P_Env_Poste_Caido", 4, true, 1f, 1f),
                ("P_Env_Cartel", 6, false, 1f, 1.2f),
            };

            int total = 0;
            foreach (var (prefab, cantidad, lejos, emin, emax) in lista)
            {
                var g = P(prefab);
                if (g == null) continue;
                int meta = Mathf.Max(3, Mathf.RoundToInt(cantidad * factorArea));
                int puestos = 0, intentos = 0;
                while (puestos < meta && intentos++ < meta * 40)
                {
                    float x = Mathf.Lerp(x0, x1, (float)rnd.NextDouble()), z = Mathf.Lerp(z0, z1, (float)rnd.NextDouble());
                    if (lejos && Mathf.Abs(x - ejeX) < 11f) continue;
                    if (!lejos && Mathf.Abs(x - ejeX) < 3f) continue;
                    float y = terreno != null ? terreno.SampleHeight(new Vector3(x, 0f, z)) + terreno.transform.position.y : 0f;
                    bool libre = true;
                    foreach (var c in Physics.OverlapSphere(new Vector3(x, y + 1.5f, z), 3.2f))
                        if (!(c is TerrainCollider) && c.name != "Ground") { libre = false; break; }
                    if (!libre) continue;

                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(g, ambiente.transform);
                    foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    inst.transform.position = new Vector3(x, y, z);
                    inst.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                    inst.transform.localScale = Vector3.one * Mathf.Lerp(emin, emax, (float)rnd.NextDouble());
                    puestos++; total++;
                }
            }

            // Faroles a lo largo de la ruta, uno cada 30 m a cada lado. Pedido explicito: "usa el
            // prefab de luces, y que sean siempre luces duras" -- P_Env_Farol era solo el mastil
            // (sin luz real); cada instancia se cuelga ahora una Light de verdad en la punta.
            var farol = P("P_Env_Farol");
            if (farol != null)
                for (float z = z0 + 10f; z < z1 - 6f; z += 30f)
                    foreach (var lado in new[] { -9f, 17f })
                    {
                        float x = ejeX + lado;
                        float y = terreno != null ? terreno.SampleHeight(new Vector3(x, 0f, z)) + terreno.transform.position.y : 0f;
                        bool libre = true;
                        foreach (var c in Physics.OverlapSphere(new Vector3(x, y + 1.5f, z), 1.6f))
                            if (!(c is TerrainCollider) && c.name != "Ground") { libre = false; break; }
                        if (!libre) continue;
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(farol, ambiente.transform);
                        foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                        inst.transform.position = new Vector3(x, y, z);
                        inst.transform.rotation = Quaternion.Euler(0f, lado < 0f ? 90f : -90f, 0f);
                        AgregarLuzDeFarol(inst.transform);
                        total++;
                    }

            // Perimetro: pedido explicito "el perimetro debe estar tapado entre arboles y
            // obstaculos y debe estar cerrado". Fuera de la zona de la base (que ya tiene sus
            // propios muros, ver LevelBlockoutBuilder), los lados este/oeste del nivel quedaban
            // abiertos al campo vacio del terreno.
            total += Perimetro(ambiente.transform, x0, x1, z0, z1, terreno, rnd);
            return total;
        }

        // Luz calida de lampara: siempre dura (pedido explicito), rango corto -- ilumina el
        // charco de piso alrededor del farol, no satura la escena.
        static void AgregarLuzDeFarol(Transform farolRaiz)
        {
            var luzGo = new GameObject("Luz");
            luzGo.transform.SetParent(farolRaiz, false);
            luzGo.transform.localPosition = new Vector3(0f, 4.0f, 0f);
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = new Color(1f, 0.78f, 0.45f);
            luz.intensity = 2.2f;
            luz.range = 11f;
            luz.shadows = LightShadows.Hard;
        }

        // Cierra el rectangulo jugable con un muro FISICO invisible (BoxCollider + ObstacleMarker,
        // igual que cualquier obstaculo del blockout) exactamente sobre el borde -- garantiza que
        // no quede ningun hueco pase lo que pase con el arbolado, que es la parte VISIBLE del
        // mismo limite. Encima se reparte una fila densa de arboles/arbustos (con su propio
        // collider, a diferencia del resto de Repartir()) y algun vehiculo quemado de tanto en
        // tanto, para que se vea "tapado" y no una pared invisible en la nada.
        static int Perimetro(Transform ambiente, float x0, float x1, float z0, float z1, Terrain terreno, System.Random rnd)
        {
            var muro = new GameObject("Perimetro_Muro").transform;
            muro.SetParent(ambiente, false);
            const float altoMuro = 6f, espesorMuro = 2f;
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
            float baseY = terreno != null ? terreno.transform.position.y : 0f;
            void Tira(string nombre, Vector3 centro, Vector3 tamano)
            {
                var go = new GameObject(nombre);
                go.transform.SetParent(muro, false);
                go.transform.position = centro;
                var col = go.AddComponent<BoxCollider>();
                col.size = tamano;
                var marca = go.AddComponent<ObstacleMarker>();
                var so = new SerializedObject(marca);
                so.FindProperty("maxHealth").intValue = 999999;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            float y = baseY + altoMuro * 0.5f;
            Tira("Oeste", new Vector3(x0, y, cz), new Vector3(espesorMuro, altoMuro, z1 - z0 + espesorMuro));
            Tira("Este", new Vector3(x1, y, cz), new Vector3(espesorMuro, altoMuro, z1 - z0 + espesorMuro));
            Tira("Sur", new Vector3(cx, y, z0), new Vector3(x1 - x0 + espesorMuro, altoMuro, espesorMuro));
            Tira("Norte", new Vector3(cx, y, z1), new Vector3(x1 - x0 + espesorMuro, altoMuro, espesorMuro));

            var visual = new GameObject("Perimetro_Vegetacion").transform;
            visual.SetParent(ambiente, false);
            var props = new[] { "P_Env_ArbolA", "P_Env_ArbolB", "P_Env_Arbusto_Grande", "P_Env_Arbusto_Medio" };
            const string obstaculo = "P_Env_Auto_Quemado";
            const float paso = 2.4f;
            float perimetroLargo = 2f * ((x1 - x0) + (z1 - z0));
            int cantidad = Mathf.RoundToInt(perimetroLargo / paso);
            int total = 0;
            for (int i = 0; i < cantidad; i++)
            {
                BordeDelRectangulo(i * paso, x0, x1, z0, z1, out var p, out var yawAfuera);
                float jitter = (float)(rnd.NextDouble() * 1.4 - 0.2); // sobre el borde o un poco afuera, nunca adentro
                var dirFuera = Quaternion.Euler(0f, yawAfuera, 0f) * Vector3.forward;
                var pos = new Vector3(p.x, 0f, p.z) + dirFuera * jitter;
                float suelo = terreno != null ? terreno.SampleHeight(pos) + terreno.transform.position.y : 0f;

                bool esObstaculo = i % 9 == 4;
                string prefab = esObstaculo ? obstaculo : props[(i + (int)(rnd.NextDouble() * props.Length)) % props.Length];
                var g = P(prefab);
                if (g == null) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(g, visual);
                // A diferencia del resto de Repartir(): el collider de estos NO se destruye -- es
                // parte del cierre fisico del perimetro, ademas del muro invisible de arriba.
                inst.transform.position = new Vector3(pos.x, suelo, pos.z);
                inst.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                inst.transform.localScale = Vector3.one * (esObstaculo ? 1f : Mathf.Lerp(1f, 1.3f, (float)rnd.NextDouble()));
                total++;
            }
            return total;
        }

        // Punto sobre el borde del rectangulo [x0,x1]x[z0,z1] a distancia `d` recorrida en sentido
        // horario desde la esquina suroeste, mas el yaw cuyo "adelante" mira hacia AFUERA de ese
        // borde (para el jitter de Perimetro).
        static void BordeDelRectangulo(float d, float x0, float x1, float z0, float z1, out Vector3 p, out float yawAfuera)
        {
            float anchoX = x1 - x0, anchoZ = z1 - z0;
            float perim = 2f * (anchoX + anchoZ);
            d = ((d % perim) + perim) % perim;
            if (d < anchoZ) { p = new Vector3(x0, 0f, z0 + d); yawAfuera = -90f; }
            else if ((d -= anchoZ) < anchoX) { p = new Vector3(x0 + d, 0f, z1); yawAfuera = 0f; }
            else if ((d -= anchoX) < anchoZ) { p = new Vector3(x1, 0f, z1 - d); yawAfuera = 90f; }
            else { d -= anchoZ; p = new Vector3(x1 - d, 0f, z0); yawAfuera = 180f; }
        }
    }
}
