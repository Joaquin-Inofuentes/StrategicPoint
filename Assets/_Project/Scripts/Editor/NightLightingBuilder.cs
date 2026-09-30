using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SP.EditorTools
{
    // Pedido explicito: "quiero que de la imagen de noche, una noche medio iluminada por
    // lamparas y estrellas en el cielo" + "que sean SIEMPRE luces duras". Pone la escena abierta
    // de noche: el sol pasa a luz de luna (fria, sombras duras) que ademas se DIBUJA en el cielo, el ambiente baja a un
    // azul oscuro, y el skybox por defecto se reemplaza por uno propio con estrellas, via lactea y luna (una sola
    // textura equirectangular generada por codigo -- no hay ningun asset de imagen descargado).
    // Las luces de los caminos y los adornos (fogatas, reflectores) los arma AmbientacionDelNivel.
    // Es idempotente: reusa/pisa los mismos assets si ya existen en vez de duplicarlos.
    public static class NightLightingBuilder
    {
        const string CarpetaTexturas = "Assets/_Project/Textures/Skybox";
        const string RutaTexturaVieja = CarpetaTexturas + "/T_CieloNocturno.asset";
        const string RutaTextura = CarpetaTexturas + "/T_CieloNocturno.png";
        const string RutaMaterial = "Assets/_Project/Materials/M_Skybox_Noche.mat";

        // Hacia donde esta la luna (vector de mundo, sale de la camara hacia la luna): al nor-noreste y a media altura, asi
        // el jugador la ve al frente mientras avanza hacia el norte y la luz le pega de costado y de frente a los enemigos.
        public static readonly Vector3 DireccionDeLaLuna = new Vector3(0.42f, 0.36f, 0.83f).normalized;

        [MenuItem("Strategic Point/Arte/10. Poner de noche (escena abierta)")]
        public static void PonerDeNoche()
        {
            var escena = SceneManager.GetActiveScene();

            PonerLunaEnElSol();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.072f, 0.092f, 0.16f);
            RenderSettings.skybox = ConstruirMaterialDeCielo();
            DynamicGI.UpdateEnvironment();
            AplicarNiebla();
            QuitarLucesViejasDeCamino();

            // BUG REAL encontrado en vivo: la MainCamera de SC_Gameplay tiene clearFlags=SolidColor
            // (limpia a un gris/blanco fijo) -- con eso, NINGUN skybox se dibuja jamas, la escena se
            // veia de dia pase lo que pase con RenderSettings.skybox. La minimapa se deja como esta
            // (su fondo solido es intencional, no es una vista del cielo).
            var camGo = GameObject.Find("MainCamera");
            var cam = camGo != null ? camGo.GetComponent<Camera>() : Camera.main;
            if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;

            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            Debug.Log($"[NightLightingBuilder] {escena.name}: noche aplicada (luna en el cielo, ambiente oscuro, estrellas y niebla).");
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
            // Luna: fria, SIEMPRE dura (pedido explicito), nunca Soft. La luz viaja de la luna hacia el suelo.
            luz.intensity = 0.34f;
            luz.color = new Color(0.58f, 0.7f, 0.98f);
            luz.shadows = LightShadows.Hard;
            sol.transform.rotation = Quaternion.LookRotation(-DireccionDeLaLuna);
        }

        // "Niebla a lo lejos": profundidad atmosferica en la distancia sin tapar el combate cercano. Lineal (no
        // exponencial) para que el corte sea predecible: nada de niebla hasta FogStart, e invisible total recien en
        // FogEnd. El color es el mismo tono del horizonte del skybox de noche, para que la niebla se funda con el
        // cielo en vez de leerse como una pared gris pegada encima. Los caminos iluminados se ven de lejos.
        const float FogStart = 45f;
        const float FogEnd = 235f;

        static void AplicarNiebla()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.085f, 0.115f, 0.2f);
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
        }

        // Las luces de camino de la version anterior (una linea de faroles sobre la ruta de pathfinding entre los
        // objetivos de mision) las reemplazan las de AmbientacionDelNivel, que siguen cada camino del nivel.
        static void QuitarLucesViejasDeCamino()
        {
            var raiz = GameObject.Find("CaminoLights");
            if (raiz != null) Object.DestroyImmediate(raiz);
            // Pedido explicito: "quita las vallas": las barandas de madera que bordeaban el camino de mision.
            var barandas = GameObject.Find("CaminoBarandas");
            if (barandas != null) Object.DestroyImmediate(barandas);
        }

        static Material ConstruirMaterialDeCielo()
        {
            var tex = ConstruirTexturaDeCielo();
            var mat = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
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
            mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));   // el multiplicador "gris medio" del shader: 0.5 = sin cambio
            mat.SetFloat("_Exposure", 1.15f);
            mat.SetFloat("_Rotation", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Textura equirectangular 4096 x 2048 (una sola imagen para todo el cielo). Mapeo de Skybox/Panoramic:
        //   u = 0.5 - atan2(z, x) / 2pi      v = 1 - acos(y) / pi      (v = 1 es el cenit, v = 0 el nadir)
        // Contenido: degrade azul-noche con un resplandor de horizonte, via lactea tenue, ~4000 estrellas finas (algunas
        // brillantes con cruz), y la luna con halo en `DireccionDeLaLuna`.
        static Texture2D ConstruirTexturaDeCielo()
        {
            if (!AssetDatabase.IsValidFolder(CarpetaTexturas))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Textures")) AssetDatabase.CreateFolder("Assets/_Project", "Textures");
                AssetDatabase.CreateFolder("Assets/_Project/Textures", "Skybox");
            }
            // La version anterior era un Texture2D suelto (.asset, 1024 px, mapeado al reves): se reemplaza por un PNG.
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTexturaVieja) != null) AssetDatabase.DeleteAsset(RutaTexturaVieja);

            const int W = 4096, H = 2048;
            var pix = new Color[W * H];
            var zenit = new Color(0.010f, 0.018f, 0.052f);
            var medio = new Color(0.034f, 0.056f, 0.125f);
            var horizonte = new Color(0.115f, 0.155f, 0.265f);
            var tierra = new Color(0.028f, 0.038f, 0.075f);

            // Plano de la via lactea: una banda inclinada.
            var normalBanda = new Vector3(0.25f, 0.82f, -0.52f).normalized;
            var luna = DireccionDeLaLuna;

            for (int y = 0; y < H; y++)
            {
                float v = (y + 0.5f) / H;
                float lat = (1f - v) * Mathf.PI;
                float cosLat = Mathf.Cos(lat), sinLat = Mathf.Sin(lat);
                // Color base segun la altura (dir.y = cosLat).
                float elev = cosLat;
                Color baseFila;
                if (elev >= 0f)
                {
                    float t = Mathf.Pow(elev, 0.55f);
                    baseFila = t < 0.5f ? Color.Lerp(horizonte, medio, t / 0.5f) : Color.Lerp(medio, zenit, (t - 0.5f) / 0.5f);
                }
                else baseFila = Color.Lerp(horizonte, tierra, Mathf.Clamp01(-elev / 0.18f));

                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W;
                    float lon = (0.5f - u) * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(lon) * sinLat, cosLat, Mathf.Sin(lon) * sinLat);
                    var c = baseFila;

                    // Via lactea: banda suave modulada por ruido.
                    float dPlano = Vector3.Dot(dir, normalBanda);
                    float banda = Mathf.Exp(-(dPlano * dPlano) / (2f * 0.16f * 0.16f));
                    if (banda > 0.02f && elev > -0.05f)
                    {
                        float n = Mathf.PerlinNoise(u * 26f, v * 13f) * 0.6f + Mathf.PerlinNoise(u * 90f + 7f, v * 45f + 3f) * 0.4f;
                        c += new Color(0.05f, 0.06f, 0.1f) * (banda * Mathf.Clamp01(n * 1.6f - 0.25f));
                    }

                    // Luna: disco + halo.
                    float cosAng = Mathf.Clamp(Vector3.Dot(dir, luna), -1f, 1f);
                    if (cosAng > 0.6f)
                    {
                        float ang = Mathf.Acos(cosAng);
                        const float radioDisco = 0.052f;   // ~3 grados: mas grande que la luna real, se lee bien
                        float halo = Mathf.Exp(-ang / 0.08f) * 0.30f + Mathf.Exp(-(ang * ang) / (2f * 0.22f * 0.22f)) * 0.06f;
                        c += new Color(0.5f, 0.62f, 0.9f) * halo;
                        if (ang < radioDisco * 1.06f)
                        {
                            float borde = Mathf.Clamp01((radioDisco * 1.06f - ang) / (radioDisco * 0.06f));
                            // Crateres: manchas de ruido que oscurecen un poco el disco.
                            float cr = Mathf.PerlinNoise(u * 900f, v * 450f) * 0.6f + Mathf.PerlinNoise(u * 300f + 9f, v * 150f) * 0.4f;
                            float k = Mathf.Lerp(0.78f, 1.05f, cr);
                            var disco = new Color(0.92f, 0.94f, 0.88f) * k;
                            c = Color.Lerp(c, disco, borde);
                        }
                    }
                    pix[y * W + x] = c;
                }
            }

            // Estrellas: uniformes sobre la esfera (arriba del horizonte), casi ninguna abajo. Se pintan achatando en X
            // segun la latitud para que se vean redondas en el cielo (la textura equirectangular se comprime en los polos).
            var rnd = new System.Random(20260929);
            int estrellas = 4200;
            for (int i = 0; i < estrellas; i++)
            {
                float yy = Mathf.Lerp(-0.02f, 1f, (float)rnd.NextDouble());
                float lon = (float)(rnd.NextDouble() * Mathf.PI * 2f);
                float lat = Mathf.Acos(yy);
                float u = 0.5f - lon / (Mathf.PI * 2f);
                u = u - Mathf.Floor(u);
                float v = 1f - lat / Mathf.PI;
                var dir = new Vector3(Mathf.Cos(lon) * Mathf.Sin(lat), yy, Mathf.Sin(lon) * Mathf.Sin(lat));
                if (Vector3.Dot(dir, luna) > 0.985f) continue;   // ninguna sobre la luna
                float brillo = Mathf.Pow((float)rnd.NextDouble(), 2.6f) * 0.85f + 0.14f;
                float tinte = (float)rnd.NextDouble();
                var color = Color.Lerp(new Color(0.8f, 0.9f, 1f), new Color(1f, 0.9f, 0.75f), tinte);
                float radio = 0.75f + brillo * 0.9f;
                PintarEstrella(pix, W, H, u * W, v * H, Mathf.Sin(lat), radio, color, brillo, brillo > 0.8f);
            }

            var tex = new Texture2D(W, H, TextureFormat.RGB24, false, false);
            tex.SetPixels(pix);
            tex.Apply(false, false);
            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), RutaTextura), bytes);
            AssetDatabase.ImportAsset(RutaTextura, ImportAssetOptions.ForceUpdate);

            var imp = (TextureImporter)AssetImporter.GetAtPath(RutaTextura);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.mipmapEnabled = false;
            imp.wrapModeU = TextureWrapMode.Repeat;
            imp.wrapModeV = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.maxTextureSize = 4096;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTextura);
        }

        static void PintarEstrella(Color[] pix, int w, int h, float cx, float cy, float sinLat, float radio, Color color, float brillo, bool cruz)
        {
            float rx = radio / Mathf.Max(0.18f, sinLat), ry = radio;
            int ex = Mathf.CeilToInt(rx * 2.4f), ey = Mathf.CeilToInt(ry * 2.4f);
            for (int dy = -ey; dy <= ey; dy++)
            {
                int py = Mathf.FloorToInt(cy) + dy;
                if (py < 0 || py >= h) continue;
                for (int dx = -ex; dx <= ex; dx++)
                {
                    int px = ((Mathf.FloorToInt(cx) + dx) % w + w) % w;
                    float fx = (px + 0.5f - cx); if (fx > w * 0.5f) fx -= w; if (fx < -w * 0.5f) fx += w;
                    float fy = py + 0.5f - cy;
                    float d2 = (fx * fx) / (rx * rx) + (fy * fy) / (ry * ry);
                    float a = Mathf.Exp(-d2 * 1.4f) * brillo;
                    if (cruz)
                    {
                        float ax = Mathf.Abs(fx) / (rx * 5f), ay = Mathf.Abs(fy) / (ry * 5f);
                        a += brillo * 0.35f * (Mathf.Exp(-ay * ay * 60f) * Mathf.Exp(-ax * 3f) + Mathf.Exp(-ax * ax * 60f) * Mathf.Exp(-ay * 3f));
                    }
                    if (a < 0.01f) continue;
                    pix[py * w + px] += color * a;
                }
            }
        }
    }
}
