using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SP.Core;
using SP.Mision;

namespace SP.EditorTools
{
    // Pedido explicito: "quiero que de la imagen de noche, una noche medio iluminada por
    // lamparas y estrellas en el cielo" + "que sean SIEMPRE luces duras". Pone la escena abierta
    // de noche: el sol pasa a luz de luna (tenue, fria, sombras duras), el ambiente baja a un
    // azul oscuro, y el skybox por defecto se reemplaza por uno propio con estrellas (una sola
    // textura equirectangular generada por codigo -- no hay ningun asset de imagen descargado).
    // Es idempotente: reusa/pisa los mismos assets si ya existen en vez de duplicarlos.
    public static class NightLightingBuilder
    {
        const string CarpetaTexturas = "Assets/_Project/Textures/Skybox";
        const string RutaTextura = CarpetaTexturas + "/T_CieloNocturno.asset";
        const string RutaMaterial = "Assets/_Project/Materials/M_Skybox_Noche.mat";

        [MenuItem("Strategic Point/Arte/10. Poner de noche (escena abierta)")]
        public static void PonerDeNoche()
        {
            var escena = SceneManager.GetActiveScene();

            PonerLunaEnElSol();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.08f, 0.14f);
            RenderSettings.skybox = ConstruirMaterialDeCielo();
            DynamicGI.UpdateEnvironment();
            AplicarNiebla();
            PonerLucesDeCamino();
            QuitarBarandasDeCamino();

            // BUG REAL encontrado en vivo: la MainCamera de SC_Gameplay tiene clearFlags=SolidColor
            // (limpia a un gris/blanco fijo) -- con eso, NINGUN skybox se dibuja jamas, la escena se
            // veia de dia pase lo que pase con RenderSettings.skybox. La minimapa se deja como esta
            // (su fondo solido es intencional, no es una vista del cielo).
            var camGo = GameObject.Find("MainCamera");
            var cam = camGo != null ? camGo.GetComponent<Camera>() : Camera.main;
            if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;

            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            Debug.Log($"[NightLightingBuilder] {escena.name}: noche aplicada (sol=luna, ambiente oscuro, cielo con estrellas).");
        }

        static void PonerLunaEnElSol()
        {
            var sol = GameObject.Find("SunLight");
            if (sol == null)
            {
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
                    if (l.type == LightType.Directional) { sol = l.gameObject; break; }
            }
            if (sol == null) return;
            var luz = sol.GetComponent<Light>();
            // Luna: tenue y fria, no la luz del dia atenuada -- y SIEMPRE dura (pedido explicito),
            // nunca Soft, a diferencia de como estaba antes.
            luz.intensity = 0.28f;
            luz.color = new Color(0.55f, 0.68f, 0.95f);
            luz.shadows = LightShadows.Hard;
        }

        // "Niebla a lo lejos": profundidad atmosferica en la distancia sin
        // tapar el combate cercano. Lineal (no exponencial) para que el
        // corte sea predecible: nada de niebla hasta FogStart, e
        // invisible total recien en FogEnd. El color es el mismo tono del
        // horizonte del skybox de noche, para que la niebla se funda con el
        // cielo en vez de leerse como una pared gris pegada encima.
        const float FogStart = 55f;
        const float FogEnd = 210f;

        static void AplicarNiebla()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.10f, 0.13f, 0.22f);
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
        }

        // "Que las luces destaquen el camino": el unico "camino" que el
        // codigo conoce de verdad (en vez de adivinar sobre el arte del
        // nivel) es la ruta que el propio pathfinding del juego calcularia
        // para ir de un objetivo de mision al siguiente (Helipuerto ->
        // Plaza -> RefugioDelCivil, los tres puntos de MisionDirector). Se
        // usa NavService.Graph.TryFindPath (el mismo grafo que usan los
        // soldados) para no arriesgarse a poner un farol adentro de un
        // edificio con una interpolacion en linea recta.
        const float EspaciadoLuces = 18f;
        const float AlturaLuces = 3f;
        const int MaxLuces = 14;

        static void PonerLucesDeCamino()
        {
            var raiz = GameObject.Find("CaminoLights");
            if (raiz != null) Object.DestroyImmediate(raiz);

            var director = Object.FindFirstObjectByType<MisionDirector>();
            if (director == null)
            {
                Debug.LogWarning("[NightLightingBuilder] No hay MisionDirector en la escena: se omiten las luces de camino.");
                return;
            }

            var puntosDelCamino = new List<Vector3>();
            AgregarTramo(director.Helipuerto, director.Plaza, puntosDelCamino);
            AgregarTramo(director.Plaza, director.RefugioDelCivil, puntosDelCamino);
            if (puntosDelCamino.Count == 0) return;

            var raizGo = new GameObject("CaminoLights");
            int puestas = 0;
            Vector3 ultima = puntosDelCamino[0] + Vector3.one * 999f; // fuerza la primera luz
            foreach (var p in puntosDelCamino)
            {
                if (puestas >= MaxLuces) break;
                if (Vector3.Distance(new Vector3(ultima.x, 0f, ultima.z), new Vector3(p.x, 0f, p.z)) < EspaciadoLuces) continue;
                CrearFarol(raizGo.transform, p);
                ultima = p;
                puestas++;
            }
            Debug.Log($"[NightLightingBuilder] {puestas} luces puestas a lo largo del camino de mision.");
        }

        // Junta los puntos de esquina del pathfinding real entre "desde" y
        // "hasta", re-muestreados cada EspaciadoLuces unidades a lo largo de
        // cada tramo recto -- el grafo solo devuelve las esquinas, y entre
        // dos esquinas lejanas (tramos rectos largos) no habria ningun
        // farol en el medio.
        static void AgregarTramo(Vector3 desde, Vector3 hasta, List<Vector3> destino)
        {
            var esquinas = new List<Vector3>();
            if (!NavService.Graph.TryFindPath(desde, hasta, esquinas) || esquinas.Count == 0)
            {
                destino.Add(desde);
                destino.Add(hasta);
                return;
            }

            Vector3 anterior = desde;
            foreach (var esquina in esquinas)
            {
                float largo = Vector3.Distance(anterior, esquina);
                int pasos = Mathf.Max(1, Mathf.RoundToInt(largo / EspaciadoLuces));
                for (int i = 0; i <= pasos; i++) destino.Add(Vector3.Lerp(anterior, esquina, i / (float)pasos));
                anterior = esquina;
            }
        }

        static void CrearFarol(Transform padre, Vector3 puntoDelCamino)
        {
            var go = new GameObject("Farol");
            go.layer = LayerMask.NameToLayer("Obstacle");
            go.transform.SetParent(padre, false);
            go.transform.position = new Vector3(puntoDelCamino.x, puntoDelCamino.y + AlturaLuces, puntoDelCamino.z);

            var luz = go.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = new Color(1f, 0.78f, 0.5f);
            luz.intensity = 3.5f;
            luz.range = 14f;
            luz.shadows = LightShadows.None; // un farol por luz dinamica con sombras cada 18 m es caro y no se nota a esa escala

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.5f, 0.5f, 0.5f);

            var luminaria = go.AddComponent<SP.Presentation.Luminaria>();
        }

        // Pedido explicito: "quita las vallas" -- las barandas de madera que
        // antes bordeaban el camino de mision (P_Mod_Valla_Madera) molestaban
        // y se sacan. Esto NO solo deja de ponerlas: las escenas horneadas en
        // sesiones anteriores ya las tenian guardadas, asi que se borra
        // tambien la raiz vieja si aparece (idempotente).
        static void QuitarBarandasDeCamino()
        {
            var raizVieja = GameObject.Find("CaminoBarandas");
            if (raizVieja != null) Object.DestroyImmediate(raizVieja);
        }

        static Material ConstruirMaterialDeCielo()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
            var tex = ConstruirTexturaDeCielo();
            if (mat == null)
            {
                var shader = Shader.Find("Skybox/Panoramic");
                mat = new Material(shader) { name = "M_Skybox_Noche" };
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials"))
                    AssetDatabase.CreateFolder("Assets/_Project", "Materials");
                AssetDatabase.CreateAsset(mat, RutaMaterial);
            }
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_Mapping", 1f);      // Latitude Longitude Layout
            mat.SetFloat("_ImageType", 0f);    // 360 Degrees
            mat.SetColor("_Tint", new Color(0.6f, 0.6f, 0.6f));
            mat.SetFloat("_Exposure", 1.1f);
            mat.SetFloat("_Rotation", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Textura equirectangular unica (1 sola imagen para todo el cielo, en vez de 6 caras de
        // cubemap: menos piezas, menos puntos donde romperse). Fila 0 = cenit (arriba), fila
        // Height-1 = nadir (abajo, no se ve nunca -- queda pareja). Degrade azul oscuro con mas
        // estrellas cerca del cenit y un resplandor tenue de horizonte a mitad de imagen.
        static Texture2D ConstruirTexturaDeCielo()
        {
            if (!AssetDatabase.IsValidFolder(CarpetaTexturas))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Textures")) AssetDatabase.CreateFolder("Assets/_Project", "Textures");
                AssetDatabase.CreateFolder("Assets/_Project/Textures", "Skybox");
            }

            const int W = 1024, H = 512;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTextura);
            bool nueva = tex == null;
            if (nueva) tex = new Texture2D(W, H, TextureFormat.RGBA32, false, true) { name = "T_CieloNocturno" };
            tex.wrapModeU = TextureWrapMode.Repeat;
            tex.wrapModeV = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var pix = new Color32[W * H];
            var noche = new Color(0.02f, 0.03f, 0.07f);
            var horizonte = new Color(0.10f, 0.13f, 0.22f);
            var cenit = new Color(0.01f, 0.015f, 0.04f);
            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1); // 0 = cenit, 1 = nadir
                Color fila;
                if (v < 0.5f) fila = Color.Lerp(cenit, horizonte, v / 0.5f);            // cenit -> horizonte
                else fila = Color.Lerp(horizonte, noche, Mathf.Clamp01((v - 0.5f) / 0.2f)); // horizonte -> tierra (abajo, plano)
                for (int x = 0; x < W; x++) pix[y * W + x] = fila;
            }

            // Estrellas: mas densas y brillantes cerca del cenit (v chico), casi ninguna bajo el
            // horizonte (v > 0.55, mirando al suelo). Semilla fija: resultado igual en cada corrida.
            var rnd = new System.Random(20260922);
            int estrellas = 2200;
            for (int i = 0; i < estrellas; i++)
            {
                float v = Mathf.Pow((float)rnd.NextDouble(), 1.6f) * 0.62f; // sesgado hacia el cenit
                int y = Mathf.Clamp(Mathf.RoundToInt(v * (H - 1)), 0, H - 1);
                int x = rnd.Next(0, W);
                float brillo = 0.55f + (float)rnd.NextDouble() * 0.45f;
                float tinte = (float)rnd.NextDouble();
                var color = Color.Lerp(new Color(0.85f, 0.9f, 1f), new Color(1f, 0.92f, 0.8f), tinte) * brillo;
                color.a = 1f;
                PintarEstrella(pix, W, H, x, y, color, rnd.NextDouble() < 0.12);
            }

            tex.SetPixels32(pix);
            tex.Apply(false, false);
            if (nueva) AssetDatabase.CreateAsset(tex, RutaTextura);
            else EditorUtility.SetDirty(tex);
            return tex;
        }

        static void PintarEstrella(Color32[] pix, int w, int h, int x, int y, Color color, bool grande)
        {
            Poner(pix, w, h, x, y, color);
            if (!grande) return;
            // Una estrella de cada ~8 se pinta con una cruz de 1px para que destaque un poco.
            var tenue = color * 0.5f; tenue.a = 1f;
            Poner(pix, w, h, x - 1, y, tenue); Poner(pix, w, h, x + 1, y, tenue);
            Poner(pix, w, h, x, y - 1, tenue); Poner(pix, w, h, x, y + 1, tenue);
        }

        static void Poner(Color32[] pix, int w, int h, int x, int y, Color color)
        {
            x = ((x % w) + w) % w; // el cielo es 360: envuelve en X
            if (y < 0 || y >= h) return;
            pix[y * w + x] = color;
        }
    }
}
