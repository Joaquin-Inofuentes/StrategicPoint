using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using SP.Mision;
using SP.Presentation;

namespace SP.EditorTools
{
    // AMBIENTACION del nivel de SC_Gameplay (pedido explicito: "que el camino este iluminado", "mejora la iluminacion" y
    // "la ambientacion"). Todo cuelga de un GameObject raiz "Ambientacion" que se borra y se rearma en cada corrida
    // (idempotente), y todo es adorno: sin colliders en los postes, con NavMeshModifier "ignorar" en la raiz para que
    // nada de esto toque la navegacion.
    //
    //   - FAROLAS que siguen cada camino de RutasDelNivel, cada camino con su luz:
    //       Carretera ... sodio calido, postes altos a ambos lados (escalonados): el camino mas iluminado.
    //       Bosque ...... faroles bajos de luz fria verdosa: se ve el sendero, pero queda penumbra entre los arboles.
    //       Servicio .... reflectores industriales blanco-anaranjados, en un solo lado.
    //       Conectores .. faroles chicos y tenues.
    //     Cada foco es un objeto con Luminaria (se rompe de un disparo) y material emisivo (brilla con el bloom).
    //   - FOGATAS en los campamentos y BARRILES ARDIENDO en los puestos de control (luz que titila + brasas).
    //   - REFLECTORES barridos en cada torre.
    //   - REFUGIO DEL REHEN: baliza cian que late, luces de balizaje hasta la puerta y reflectores en el patio.
    //   - HELIPUERTO: plataforma con anillo de luces azules.
    //   - LUCIERNAGAS en el sendero del bosque.
    //   - POSTPROCESO nocturno (bloom, vineta, color).
    //   - Mueve el marcador de mision del rehen y su toma de la cinematica al refugio.
    public static class AmbientacionDelNivel
    {
        const string NombreRaiz = "Ambientacion";
        const string CarpetaMateriales = "Assets/_Project/Materials/Ambiente";
        const string RutaPerfil = "Assets/_Project/Lighting/VP_Noche.asset";
        const string Prefabs = "Assets/_Project/Prefabs/ArteMundo/";

        static Transform raiz;
        static readonly List<Vector2> faroles = new List<Vector2>();
        static int luces;

        // ---------------------------------------------------------------
        // Menus
        // ---------------------------------------------------------------
        [MenuItem("Strategic Point/Nivel/Ambientar (caminos iluminados, fogatas, reflectores, postproceso)")]
        public static void Ambientar()
        {
            var escena = SceneManager.GetActiveScene();
            if (escena.name != "SC_Gameplay") { Debug.LogWarning("[Ambientacion] Abri SC_Gameplay antes (escena activa: " + escena.name + ")."); return; }

            Physics.SyncTransforms();
            faroles.Clear();
            luces = 0;

            var previa = GameObject.Find(NombreRaiz);
            if (previa != null) Object.DestroyImmediate(previa);
            raiz = new GameObject(NombreRaiz).transform;
            var mod = raiz.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
            mod.ignoreFromBuild = true;
            mod.applyToChildren = true;

            var luminarias = Sub("Farolas");
            foreach (var r in RutasDelNivel.Todas) PonerFarolasEnRuta(r, luminarias);

            PonerFuegos(Sub("Fuegos"));
            PonerReflectoresDeTorre(Sub("Reflectores"));
            PonerRefugio(Sub("Refugio"));
            PonerHelipuerto(Sub("Helipuerto"));
            PonerLuciernagas(Sub("Luciernagas"));
            AplicarPostproceso();
            AjustarMarcadoresDeMision();

            NightLightingBuilder.PonerDeNoche();   // luna, cielo, niebla; guarda la escena
            Debug.Log($"[Ambientacion] {luces} luces puestas (farolas, fuegos, reflectores, balizas). Postproceso y noche aplicados.");
        }

        // Rehace el nivel entero en el orden correcto: cubos y terreno -> arte y arboles -> ambientacion -> NavMesh.
        [MenuItem("Strategic Point/Nivel/REHACER NIVEL COMPLETO (blockout + arte + ambientacion + NavMesh)")]
        public static void RehacerNivelCompleto()
        {
            var escena = SceneManager.GetActiveScene();
            if (escena.name != "SC_Gameplay") { Debug.LogWarning("[Ambientacion] Abri SC_Gameplay antes (escena activa: " + escena.name + ")."); return; }
            LevelBlockoutBuilder.Construir();
            BlockoutArtDresser.VestirEscenaAbierta();
            Ambientar();
            LevelBlockoutBuilder.BakeNavMesh();
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            Debug.Log("[Ambientacion] NIVEL COMPLETO rehecho y guardado.");
        }

        static Transform Sub(string nombre)
        {
            var g = new GameObject(nombre).transform;
            g.SetParent(raiz, false);
            return g;
        }

        // ---------------------------------------------------------------
        // Materiales (assets persistentes: la escena los referencia)
        // ---------------------------------------------------------------
        static Material Mat(string nombre, Color color, Color emision = default, float k = 0f, float suavidad = 0.15f)
        {
            if (!AssetDatabase.IsValidFolder(CarpetaMateriales)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "Ambiente");
            string ruta = $"{CarpetaMateriales}/M_{nombre}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_" + nombre };
                AssetDatabase.CreateAsset(m, ruta);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", suavidad);
            if (k > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emision * k);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else m.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            return m;
        }

        // Particulas aditivas con un sprite redondo y suave (generado por codigo): las brasas, las llamas y las
        // luciernagas se ven como puntos de luz y no como cuadrados.
        const string RutaSprite = "Assets/_Project/Textures/Fx/T_Particula_Suave.png";

        static Texture2D SpriteSuave()
        {
            var existente = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaSprite);
            if (existente != null) return existente;
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Textures/Fx")) AssetDatabase.CreateFolder("Assets/_Project/Textures", "Fx");
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);       // borde suave
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), RutaSprite), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(RutaSprite, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(RutaSprite);
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(RutaSprite);
        }

        static Material MatParticula(string nombre)
        {
            if (!AssetDatabase.IsValidFolder(CarpetaMateriales)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "Ambiente");
            string ruta = $"{CarpetaMateriales}/M_{nombre}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "M_" + nombre };
                AssetDatabase.CreateAsset(m, ruta);
            }
            m.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            m.SetFloat("_Surface", 1f);                                   // transparente
            m.SetFloat("_Blend", 2f);                                     // aditivo
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SoftParticlesEnabled", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            m.SetTexture("_BaseMap", SpriteSuave());
            m.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Color Tono(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        static Material MetalPoste() => Mat("Farola_Poste", new Color(0.13f, 0.14f, 0.16f), default, 0f, 0.35f);

        // ---------------------------------------------------------------
        // Primitivas sin collider
        // ---------------------------------------------------------------
        static GameObject Prim(PrimitiveType tipo, string nombre, Transform padre, Vector3 posLocal, Vector3 escala, Material mat, bool conCollider = false)
        {
            var go = GameObject.CreatePrimitive(tipo);
            go.name = nombre;
            if (!conCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(padre, false);
            go.transform.localPosition = posLocal;
            go.transform.localScale = escala;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static Light NuevaLuz(GameObject go, LightType tipo, Color color, float intensidad, float rango)
        {
            var l = go.AddComponent<Light>();
            l.type = tipo;
            l.color = color;
            l.intensity = intensidad;
            l.range = rango;
            l.shadows = LightShadows.None;
            luces++;
            return l;
        }

        // ---------------------------------------------------------------
        // Farolas por camino
        // ---------------------------------------------------------------
        struct EstiloDeFarola
        {
            public string Nombre;
            public float Altura, Brazo, Paso, DistanciaAlBorde;
            public Vector3 Foco;
            public Color Luz, Emision;
            public float Intensidad, Rango;
            public bool UnSoloLado;
        }

        static EstiloDeFarola EstiloDe(TipoDeRuta tipo)
        {
            switch (tipo)
            {
                case TipoDeRuta.Carretera:
                    return new EstiloDeFarola { Nombre = "FarolaCalle", Altura = 4.8f, Brazo = 1.3f, Paso = 15f, DistanciaAlBorde = 1.2f, Foco = new Vector3(0.5f, 0.12f, 0.36f),
                        Luz = new Color(1f, 0.74f, 0.42f), Emision = new Color(1f, 0.72f, 0.4f), Intensidad = 3.8f, Rango = 17f };
                case TipoDeRuta.Bosque:
                    return new EstiloDeFarola { Nombre = "FarolBosque", Altura = 2.8f, Brazo = 0.5f, Paso = 16f, DistanciaAlBorde = 1.6f, Foco = new Vector3(0.3f, 0.36f, 0.3f),
                        Luz = new Color(0.5f, 0.95f, 0.85f), Emision = new Color(0.4f, 0.9f, 0.8f), Intensidad = 2.5f, Rango = 11f };
                case TipoDeRuta.Servicio:
                    return new EstiloDeFarola { Nombre = "ReflectorServicio", Altura = 5.6f, Brazo = 1f, Paso = 19f, DistanciaAlBorde = 1.6f, Foco = new Vector3(0.7f, 0.16f, 0.5f),
                        Luz = new Color(1f, 0.9f, 0.74f), Emision = new Color(1f, 0.88f, 0.7f), Intensidad = 4.2f, Rango = 19f, UnSoloLado = true };
                default:
                    return new EstiloDeFarola { Nombre = "FarolChico", Altura = 2.5f, Brazo = 0.4f, Paso = 22f, DistanciaAlBorde = 1.3f, Foco = new Vector3(0.26f, 0.3f, 0.26f),
                        Luz = new Color(1f, 0.68f, 0.42f), Emision = new Color(1f, 0.66f, 0.4f), Intensidad = 2.1f, Rango = 10f, UnSoloLado = true };
            }
        }

        static bool EsLibre(Vector2 p, float radio)
        {
            if (p.x < -48.4f || p.x > 57.2f || p.y < -16.5f || p.y > 293f) return false;
            if (Physics.CheckSphere(new Vector3(p.x, 1.4f, p.y), radio)) return false;
            foreach (var f in faroles) if ((f - p).sqrMagnitude < 5.5f * 5.5f) return false;
            return true;
        }

        static void PonerFarolasEnRuta(RutaDelNivel r, Transform padre)
        {
            var e = EstiloDe(r.Tipo);
            var contenedor = new GameObject(r.Nombre).transform;
            contenedor.SetParent(padre, false);
            float largo = r.Largo;
            int i = 0, puestas = 0;
            for (float d = e.Paso * 0.5f; d < largo - 2f; d += e.Paso, i++)
            {
                r.Muestrear(d, out var p, out var t);
                var normal = new Vector2(-t.y, t.x);
                float lado = (i % 2 == 0) ? 1f : -1f;
                if (e.UnSoloLado) lado = (r.Tipo == TipoDeRuta.Servicio) ? (p.x > 20f ? -1f : 1f) : lado;
                for (int intento = 0; intento < 2; intento++)
                {
                    float sgn = intento == 0 ? lado : -lado;
                    var pos = p + normal * sgn * (r.Ancho * 0.5f + e.DistanciaAlBorde);
                    if (!EsLibre(pos, 0.9f)) continue;
                    var haciaCamino = (-normal * sgn);
                    CrearFarola(contenedor, $"{e.Nombre}_{puestas + 1}", pos, haciaCamino, e);
                    faroles.Add(pos);
                    puestas++;
                    break;
                }
            }
        }

        static void CrearFarola(Transform padre, string nombre, Vector2 pos, Vector2 haciaCamino, EstiloDeFarola e)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.position = new Vector3(pos.x, 0f, pos.y);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(haciaCamino.x, 0f, haciaCamino.y), Vector3.up);

            var poste = MetalPoste();
            Prim(PrimitiveType.Cylinder, "Poste", go.transform, new Vector3(0f, e.Altura * 0.5f, 0f), new Vector3(e.Altura > 4f ? 0.2f : 0.14f, e.Altura * 0.5f, e.Altura > 4f ? 0.2f : 0.14f), poste);
            if (e.Brazo > 0.6f)
                Prim(PrimitiveType.Cube, "Brazo", go.transform, new Vector3(0f, e.Altura - 0.05f, e.Brazo * 0.5f), new Vector3(0.09f, 0.09f, e.Brazo), poste);
            Prim(PrimitiveType.Cube, "Base", go.transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.42f, 0.3f, 0.42f), poste);

            var matFoco = Mat(e.Nombre + "_Foco", Tono(e.Emision, 0.5f), e.Emision, 3.2f, 0.2f);
            var foco = Prim(PrimitiveType.Cube, "Foco", go.transform, new Vector3(0f, e.Altura - 0.02f, e.Brazo), e.Foco, matFoco, true);
            var col = foco.GetComponent<BoxCollider>();
            col.size = Vector3.one;
            var luz = NuevaLuz(foco, LightType.Point, e.Luz, e.Intensidad, e.Rango);
            foco.AddComponent<Luminaria>();
            // El foco vive en la capa de obstaculos (como las luminarias de antes): un disparo lo rompe.
            int capa = LayerMask.NameToLayer("Obstacle");
            if (capa >= 0) foco.layer = capa;
            // Los faroles de fuego titilan apenas (los de calle no: una farola de sodio es pareja).
            if (e.Nombre == "FarolBosque" || e.Nombre == "FarolChico")
            {
                var t = foco.AddComponent<LuzTitilante>();
                t.Configurar(luz, null, 0.08f, 3f);
            }
        }

        // ---------------------------------------------------------------
        // Fuegos: barriles ardiendo y fogatas
        // ---------------------------------------------------------------
        static void PonerFuegos(Transform padre)
        {
            var matBrasa = MatParticula("Fx_Brasa");
            var matFuego = MatParticula("Fx_Llama");
            var matSuperficie = Mat("Fuego_Superficie", new Color(0.6f, 0.2f, 0.05f), new Color(1f, 0.42f, 0.1f), 3.2f, 0f);

            // Barriles ardiendo en los puestos de control.
            var barriles = new[]
            {
                new Vector2(3f, 56f), new Vector2(14f, 150f), new Vector2(-13f, 201f), new Vector2(-12f, 232f), new Vector2(20f, 232f),
                new Vector2(-8f, 279f), new Vector2(16f, 279f),
            };
            int nb = 0;
            foreach (var b in barriles)
            {
                var pos = LibreCerca(b, 1f);
                CrearBarrilArdiendo(padre, $"BarrilArdiendo_{++nb}", pos, matSuperficie, matFuego, matBrasa);
            }

            // Fogatas en los campamentos.
            int nf = 0;
            foreach (var c in LevelBlockoutBuilder.Campamentos)
                CrearFogata(padre, $"Fogata_{++nf}", c, matFuego, matBrasa);
        }

        static Vector2 LibreCerca(Vector2 deseado, float radio)
        {
            if (!Physics.CheckSphere(new Vector3(deseado.x, 1f, deseado.y), radio)) return deseado;
            for (float r = 1.5f; r < 7f; r += 1f)
                for (int a = 0; a < 12; a++)
                {
                    float ang = a * Mathf.PI * 2f / 12f;
                    var p = deseado + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                    if (!Physics.CheckSphere(new Vector3(p.x, 1f, p.y), radio)) return p;
                }
            return deseado;
        }

        static void CrearBarrilArdiendo(Transform padre, string nombre, Vector2 pos, Material matSuperficie, Material matFuego, Material matBrasa)
        {
            var go = new GameObject(nombre).transform;
            go.SetParent(padre, false);
            go.position = new Vector3(pos.x, 0f, pos.y);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "P_Env_Barril.prefab");
            float alto = 1.0f;
            if (prefab != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go);
                foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localScale = Vector3.one * 0.9f;
                var rs = inst.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); alto = b.max.y; }
                // Collider propio (no destructible: es parte del decorado del puesto).
                var col = go.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, alto * 0.5f, 0f);
                col.size = new Vector3(0.7f, alto, 0.7f);
            }
            // Superficie incandescente adentro del barril + llamas de particulas encima.
            Prim(PrimitiveType.Cylinder, "Brasa", go, new Vector3(0f, alto - 0.03f, 0f), new Vector3(0.5f, 0.02f, 0.5f), matSuperficie);
            CrearLlama(go, new Vector3(0f, alto, 0f), matFuego, 0.16f, 0.42f);

            var luzGo = new GameObject("Luz");
            luzGo.transform.SetParent(go, false);
            luzGo.transform.localPosition = new Vector3(0f, alto + 0.7f, 0f);
            var luz = NuevaLuz(luzGo, LightType.Point, new Color(1f, 0.56f, 0.24f), 3.6f, 13f);
            luzGo.AddComponent<LuzTitilante>().Configurar(luz, null, 0.3f, 7.5f);
            CrearBrasas(go, new Vector3(0f, alto + 0.3f, 0f), matBrasa, 0.15f);
        }

        static void CrearFogata(Transform padre, string nombre, Vector2 pos, Material matFuego, Material matBrasa)
        {
            var go = new GameObject(nombre).transform;
            go.SetParent(padre, false);
            go.position = new Vector3(pos.x, 0f, pos.y);

            var madera = Mat("Fogata_Lena", new Color(0.24f, 0.15f, 0.09f), default, 0f, 0.05f);
            var piedra = Mat("Fogata_Piedra", new Color(0.32f, 0.32f, 0.34f), default, 0f, 0.1f);
            for (int i = 0; i < 3; i++)
            {
                var tronco = Prim(PrimitiveType.Cube, "Lena_" + i, go, new Vector3(0f, 0.12f, 0f), new Vector3(0.14f, 0.14f, 1f), madera);
                tronco.transform.localRotation = Quaternion.Euler(0f, i * 60f + 15f, 0f);
            }
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI * 2f / 9f;
                var p = Prim(PrimitiveType.Sphere, "Piedra_" + i, go, new Vector3(Mathf.Cos(a) * 0.62f, 0.1f, Mathf.Sin(a) * 0.62f), new Vector3(0.26f, 0.2f, 0.26f), piedra);
                p.transform.localRotation = Quaternion.Euler(0f, i * 40f, 0f);
            }
            CrearLlama(go, new Vector3(0f, 0.2f, 0f), matFuego, 0.26f, 0.75f);

            // Dos cajas y un neumatico como asientos improvisados alrededor.
            PonerPrefab(go, "P_Env_Caja_Madera", new Vector3(1.9f, 0f, 0.4f), 20f, 0.9f);
            PonerPrefab(go, "P_Env_Caja_Madera", new Vector3(-1.7f, 0f, -0.9f), -40f, 0.8f);
            PonerPrefab(go, "P_Env_Neumaticos", new Vector3(0.3f, 0f, -2.0f), 0f, 0.8f);

            var luzGo = new GameObject("Luz");
            luzGo.transform.SetParent(go, false);
            luzGo.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var luz = NuevaLuz(luzGo, LightType.Point, new Color(1f, 0.55f, 0.22f), 4.4f, 15f);
            luzGo.AddComponent<LuzTitilante>().Configurar(luz, null, 0.32f, 7f);
            CrearBrasas(go, new Vector3(0f, 0.7f, 0f), matBrasa, 0.2f);
        }

        // Llama de particulas aditivas (sprite suave): sube, se achica y pasa de amarillo a rojo.
        static void CrearLlama(Transform padre, Vector3 local, Material mat, float radio, float tamano)
        {
            var go = new GameObject("Llama");
            go.transform.SetParent(padre, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(tamano * 0.7f, tamano);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.78f, 0.32f, 0.8f), new Color(1f, 0.5f, 0.15f, 0.8f));
            main.gravityModifier = -0.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 40;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var em = ps.emission;
            em.rateOverTime = 26f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 6f;
            sh.radius = radio;
            var tam = ps.sizeOverLifetime;
            tam.enabled = true;
            tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.75f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.1f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.12f), 0.45f), new GradientColorKey(new Color(0.8f, 0.12f, 0.02f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.15f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        static void PonerPrefab(Transform padre, string prefab, Vector3 local, float yaw, float escala)
        {
            var g = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + prefab + ".prefab");
            if (g == null) return;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(g, padre);
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            inst.transform.localPosition = local;
            inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            inst.transform.localScale = Vector3.one * escala;
        }

        static void CrearBrasas(Transform padre, Vector3 local, Material mat, float radio)
        {
            var go = new GameObject("Brasas");
            go.transform.SetParent(padre, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.2f, 1f), new Color(1f, 0.85f, 0.4f, 1f));
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            var em = ps.emission;
            em.rateOverTime = 9f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 16f;
            sh.radius = radio;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var nz = ps.noise;
            nz.enabled = true;
            nz.strength = 0.6f;
            nz.frequency = 0.5f;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        // ---------------------------------------------------------------
        // Reflectores barridos en las torres
        // ---------------------------------------------------------------
        static void PonerReflectoresDeTorre(Transform padre)
        {
            var blk = GameObject.Find("Nivel_Blockout");
            if (blk == null) return;
            var carcasa = Mat("Reflector_Carcasa", new Color(0.2f, 0.21f, 0.23f), default, 0f, 0.4f);
            var lente = Mat("Reflector_Lente", new Color(0.9f, 0.95f, 1f), new Color(0.85f, 0.95f, 1f), 3f, 0.5f);
            var rnd = new System.Random(77);
            int n = 0;
            foreach (var mr in blk.GetComponentsInChildren<Transform>())
            {
                if (!mr.name.StartsWith("Torre_")) continue;
                var s = mr.lossyScale;
                if (s.y < 4.5f) continue;
                var arriba = new Vector3(mr.position.x, mr.position.y + s.y * 0.5f + 1.55f, mr.position.z);   // encima del emplazamiento

                var go = new GameObject($"Reflector_{mr.name}");
                go.transform.SetParent(padre, false);
                go.transform.position = arriba;
                Prim(PrimitiveType.Cylinder, "Mastil", go.transform, new Vector3(0f, -0.45f, 0f), new Vector3(0.12f, 0.45f, 0.12f), carcasa);
                var cabezal = new GameObject("Cabezal");
                cabezal.transform.SetParent(go.transform, false);
                Prim(PrimitiveType.Cube, "Carcasa", cabezal.transform, Vector3.zero, new Vector3(0.5f, 0.42f, 0.7f), carcasa);
                Prim(PrimitiveType.Cube, "Lente", cabezal.transform, new Vector3(0f, 0f, 0.36f), new Vector3(0.4f, 0.32f, 0.06f), lente);
                var luzGo = new GameObject("Haz");
                luzGo.transform.SetParent(cabezal.transform, false);
                luzGo.transform.localPosition = new Vector3(0f, 0f, 0.4f);
                var luz = NuevaLuz(luzGo, LightType.Spot, new Color(0.9f, 0.97f, 1f), 14f, 62f);
                luz.spotAngle = 34f;
                luz.innerSpotAngle = 20f;

                // Mira hacia el eje central del mapa, barriendo a los costados.
                float dx = 4f - mr.position.x;
                float yawCentro = Mathf.Abs(dx) > 6f ? (dx > 0f ? 90f : 270f) : 180f;
                var foco = cabezal.AddComponent<FocoBarredor>();
                foco.Configurar(yawCentro, 20f, 58f, 0.11f + (float)rnd.NextDouble() * 0.06f, (float)rnd.NextDouble());
                n++;
            }
            Debug.Log($"[Ambientacion] {n} reflectores de torre.");
        }

        // ---------------------------------------------------------------
        // Refugio del rehen
        // ---------------------------------------------------------------
        static void PonerRefugio(Transform padre)
        {
            var c = RutasDelNivel.RefugioDelCivil;
            var cian = new Color(0.3f, 0.95f, 1f);

            // Baliza cian que late sobre el punto donde aparece el rehen.
            var baliza = new GameObject("Baliza_Rehen").transform;
            baliza.SetParent(padre, false);
            baliza.position = new Vector3(c.x, 0f, c.z + 0.5f);
            var matBaliza = Mat("Baliza_Cian", Tono(cian, 0.5f), cian, 3.5f, 0.3f);
            var fuste = Prim(PrimitiveType.Cylinder, "Fuste", baliza, new Vector3(0f, 2.4f, 0f), new Vector3(0.12f, 2.4f, 0.12f), matBaliza);
            var luzGo = new GameObject("Luz");
            luzGo.transform.SetParent(baliza, false);
            luzGo.transform.localPosition = new Vector3(0f, 3.4f, 0f);
            var luz = NuevaLuz(luzGo, LightType.Point, cian, 5.5f, 17f);
            luzGo.AddComponent<BalizaPulsante>().Configurar(luz, fuste.GetComponent<Renderer>(), cian * 3.5f, 0.55f, 0.3f, 0f);

            // Balizaje al ras del piso: dos filas de luces cian hasta la puerta (ultimos 24 m de la carretera).
            var matSuelo = Mat("BalizaSuelo_Cian", Tono(cian, 0.5f), cian, 3.2f, 0.3f);
            int i = 0;
            for (float z = 259f; z <= 279f; z += 5f)
                foreach (float dx in new[] { -3.4f, 3.4f })
                {
                    var p = new Vector2(4f + dx, z);
                    if (!EsLibre(p, 0.4f)) continue;
                    var go = new GameObject($"BalizaSuelo_{++i}").transform;
                    go.SetParent(padre, false);
                    go.position = new Vector3(p.x, 0f, p.y);
                    var cubo = Prim(PrimitiveType.Cube, "Luz", go, new Vector3(0f, 0.12f, 0f), new Vector3(0.22f, 0.24f, 0.22f), matSuelo);
                    var l = NuevaLuz(cubo, LightType.Point, cian, 1.7f, 6f);
                    cubo.AddComponent<BalizaPulsante>().Configurar(l, cubo.GetComponent<Renderer>(), cian * 3.2f, 0.8f, 0.4f, z * 0.05f);
                    faroles.Add(p);
                }

            // Reflectores del patio (blanco frio).
            var e = new EstiloDeFarola { Nombre = "ReflectorPatio", Altura = 6f, Brazo = 0.9f, Foco = new Vector3(0.8f, 0.2f, 0.6f),
                Luz = new Color(0.85f, 0.95f, 1f), Emision = new Color(0.8f, 0.92f, 1f), Intensidad = 4.6f, Rango = 22f };
            var puntos = new[] { new Vector2(-8f, 279.5f), new Vector2(16f, 279.5f), new Vector2(-34f, 283f), new Vector2(44f, 283f) };
            int k = 0;
            foreach (var pt in puntos)
            {
                var p = LibreCerca(pt, 0.9f);
                CrearFarola(padre, $"ReflectorPatio_{++k}", p, new Vector2(4f - p.x, 285f - p.y).normalized, e);
                faroles.Add(p);
            }
        }

        // ---------------------------------------------------------------
        // Helipuerto: plataforma + anillo de luces azules
        // ---------------------------------------------------------------
        static void PonerHelipuerto(Transform padre)
        {
            var h = RutasDelNivel.Helipuerto;
            var azul = new Color(0.45f, 0.7f, 1f);
            var plataforma = new GameObject("Plataforma").transform;
            plataforma.SetParent(padre, false);
            plataforma.position = new Vector3(h.x, 0f, h.z);
            var pad = Mat("Helipuerto_Piso", new Color(0.11f, 0.12f, 0.13f), default, 0f, 0.2f);
            Prim(PrimitiveType.Cylinder, "Piso", plataforma, new Vector3(0f, 0.025f, 0f), new Vector3(11f, 0.025f, 11f), pad);
            var anillo = Mat("Helipuerto_Anillo", new Color(0.8f, 0.8f, 0.8f), new Color(0.9f, 0.9f, 0.9f), 1.2f, 0.2f);
            Prim(PrimitiveType.Cylinder, "Anillo", plataforma, new Vector3(0f, 0.05f, 0f), new Vector3(9.4f, 0.005f, 9.4f), anillo);
            Prim(PrimitiveType.Cylinder, "Centro", plataforma, new Vector3(0f, 0.058f, 0f), new Vector3(8.8f, 0.005f, 8.8f), pad);
            // "H" de aterrizaje.
            Prim(PrimitiveType.Cube, "H_Izq", plataforma, new Vector3(-1.4f, 0.07f, 0f), new Vector3(0.45f, 0.01f, 3.6f), anillo);
            Prim(PrimitiveType.Cube, "H_Der", plataforma, new Vector3(1.4f, 0.07f, 0f), new Vector3(0.45f, 0.01f, 3.6f), anillo);
            Prim(PrimitiveType.Cube, "H_Cruce", plataforma, new Vector3(0f, 0.07f, 0f), new Vector3(3f, 0.01f, 0.45f), anillo);

            var matLuz = Mat("BalizaSuelo_Azul", Tono(azul, 0.5f), azul, 3.2f, 0.3f);
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                var go = new GameObject($"LuzAterrizaje_{i + 1}").transform;
                go.SetParent(padre, false);
                go.position = new Vector3(h.x + Mathf.Cos(a) * 5.9f, 0f, h.z + Mathf.Sin(a) * 5.9f);
                var cubo = Prim(PrimitiveType.Cube, "Luz", go, new Vector3(0f, 0.12f, 0f), new Vector3(0.24f, 0.24f, 0.24f), matLuz);
                var l = NuevaLuz(cubo, LightType.Point, azul, 1.9f, 7f);
                cubo.AddComponent<BalizaPulsante>().Configurar(l, cubo.GetComponent<Renderer>(), azul * 3.2f, 0.7f, 0.35f, i * 0.1f);
            }
        }

        // ---------------------------------------------------------------
        // Luciernagas en el sendero del bosque
        // ---------------------------------------------------------------
        static void PonerLuciernagas(Transform padre)
        {
            RutaDelNivel bosque = default;
            foreach (var r in RutasDelNivel.Todas) if (r.Tipo == TipoDeRuta.Bosque) { bosque = r; break; }
            if (bosque.Puntos == null) return;
            var mat = MatParticula("Fx_Luciernaga");
            float largo = bosque.Largo;
            int n = 0;
            for (float d = 25f; d < largo - 10f; d += 48f)
            {
                bosque.Muestrear(d, out var p, out var t);
                var go = new GameObject($"Luciernagas_{++n}");
                go.transform.SetParent(padre, false);
                go.transform.position = new Vector3(p.x, 1.2f, p.y);
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 1f, 0.4f, 1f), new Color(0.95f, 1f, 0.6f, 1f));
                main.maxParticles = 22;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission;
                em.rateOverTime = 3.5f;
                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(12f, 2.2f, 24f);
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0.15f, 0.5f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                var nz = ps.noise;
                nz.enabled = true;
                nz.strength = 0.7f;
                nz.frequency = 0.35f;
                var rend = go.GetComponent<ParticleSystemRenderer>();
                rend.sharedMaterial = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }
        }

        // ---------------------------------------------------------------
        // Postproceso nocturno
        // ---------------------------------------------------------------
        static void AplicarPostproceso()
        {
            var previo = GameObject.Find("PostProceso_Noche");
            if (previo != null) Object.DestroyImmediate(previo);

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Lighting")) AssetDatabase.CreateFolder("Assets/_Project", "Lighting");
            var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaPerfil);
            if (perfil != null) AssetDatabase.DeleteAsset(RutaPerfil);
            perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(perfil, RutaPerfil);

            T Agregar<T>() where T : VolumeComponent
            {
                var c = perfil.Add<T>(true);
                AssetDatabase.AddObjectToAsset(c, perfil);
                return c;
            }

            var bloom = Agregar<Bloom>();
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.96f, 0.92f));

            var vineta = Agregar<Vignette>();
            vineta.intensity.Override(0.3f);
            vineta.smoothness.Override(0.5f);

            var color = Agregar<ColorAdjustments>();
            color.postExposure.Override(0.18f);
            color.contrast.Override(14f);
            color.saturation.Override(-4f);
            color.colorFilter.Override(new Color(0.94f, 0.98f, 1.04f));

            var tono = Agregar<Tonemapping>();
            tono.mode.Override(TonemappingMode.Neutral);

            EditorUtility.SetDirty(perfil);
            AssetDatabase.SaveAssets();

            var go = new GameObject("PostProceso_Noche");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1f;
            vol.sharedProfile = perfil;

            var cam = GameObject.Find("MainCamera");
            var data = cam != null ? cam.GetComponent<UniversalAdditionalCameraData>() : null;
            if (data != null) { data.renderPostProcessing = true; EditorUtility.SetDirty(data); }
        }

        // ---------------------------------------------------------------
        // Mision: el rehen esta en el refugio (extremo norte)
        // ---------------------------------------------------------------
        static void AjustarMarcadoresDeMision()
        {
            var c = RutasDelNivel.RefugioDelCivil;
            var marcador = GameObject.Find("Marcador_RefugioCivil");
            if (marcador != null) { marcador.transform.position = c; EditorUtility.SetDirty(marcador.transform); }

            var director = Object.FindFirstObjectByType<MisionDirector>();
            if (director != null)
            {
                var so = new SerializedObject(director);
                var p = so.FindProperty("refugioDelCivilPorDefecto");
                if (p != null) p.vector3Value = c;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(director);
            }

            // Toma de la cinematica de apertura sobre el rehen: ahora esta a ~300 m, hay que darle mas tiempo de viaje.
            var raiz = GameObject.Find("CinematicaDeIntro");
            if (raiz != null)
            {
                var w = raiz.transform.Find("3_Rehen");
                if (w != null)
                {
                    w.position = c + new Vector3(-9f, 5.5f, -14f);
                    var cw = w.GetComponent<CinematicaWaypoint>();
                    if (cw != null) { cw.mirarPunto = c + new Vector3(0f, 1.5f, 6f); cw.segundosParaLlegar = 5.5f; cw.segundosDeEspera = 2.4f; EditorUtility.SetDirty(cw); }
                }
                var path = raiz.GetComponent<CinematicaIntroPath>();
                if (path != null) path.Reconectar();
            }
        }
    }
}
