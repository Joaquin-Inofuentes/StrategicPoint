using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using SP.Mision;
using SP.Operacion;
using SP.Presentation;

namespace SP.EditorTools
{
    // P11 (#130): imagenes del mapa para la pantalla de carga. Renderiza una vista cenital ortografica del nivel (generada localmente, nada se
    // descarga) a Assets/_Project/Art/UI/Mapa_<escena>.png y guarda al lado el recorrido (inicio -> objetivos -> helicoptero) normalizado en
    // Mapa_<escena>.json; LoadingSceneBuilder los toma de ahi. Todo se vuelve a generar con los menus de abajo.
    public static class MapasDeCarga
    {
        public const string Carpeta = "Assets/_Project/Art/UI";

        public static string RutaPng(string escena) => $"{Carpeta}/Mapa_{escena}.png";
        public static string RutaJson(string escena) => $"{Carpeta}/Mapa_{escena}.json";

        [MenuItem("Strategic Point/Mapas de carga/Generar mapa de SC_Gameplay")]
        public static void GenerarGameplayMenu() { Debug.Log("[MapasDeCarga] " + Generar("SC_Gameplay")); }

        [MenuItem("Strategic Point/Mapas de carga/Generar mapa de SC_Operacion")]
        public static void GenerarOperacionMenu() { Debug.Log("[MapasDeCarga] " + Generar("SC_Operacion")); }

        // Abre la escena (si hace falta), renderiza y escribe PNG + JSON. Devuelve un resumen.
        public static string Generar(string escena)
        {
            if (EditorApplication.isPlaying) return "ERROR: salir de Play antes de generar el mapa";
            var activa = EditorSceneManager.GetActiveScene();
            string ruta = $"Assets/_Project/Scenes/{escena}.unity";
            string previa = activa.path;
            if (activa.name != escena)
            {
                if (activa.isDirty) return "ERROR: la escena activa tiene cambios sin guardar";
                EditorSceneManager.OpenScene(ruta);
            }
            try
            {
                var puntos = new List<Vector3>(); var idx = new List<int>(); var etiquetas = new List<string>(); var colores = new List<string>();
                string leyenda;
                if (escena == "SC_Gameplay") ReunirGameplay(puntos, idx, etiquetas, colores, out leyenda);
                else ReunirOperacion(puntos, idx, etiquetas, colores, out leyenda);
                if (puntos.Count < 3) return "ERROR: el recorrido de " + escena + " tiene menos de 3 puntos";

                // Limites: el NavMesh + el recorrido, con margen.
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                void Incluir(Vector3 p) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
                var tri = NavMesh.CalculateTriangulation();
                foreach (var v in tri.vertices) Incluir(v);
                foreach (var p in puntos) Incluir(p);
                const float margen = 18f;
                minX -= margen; maxX += margen; minZ -= margen; maxZ += margen;
                float sx = maxX - minX, sz = maxZ - minZ;

                var tex = Renderizar(minX, maxX, minZ, maxZ, 1400);
                Directory.CreateDirectory(Carpeta);
                File.WriteAllBytes(RutaPng(escena), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);

                var d = new DatosDelMapa
                {
                    escena = escena, aspecto = sx / sz, metrosX = sx, metrosZ = sz,
                    recorrido = new Vector2[puntos.Count],
                    indicesDeHitos = idx.ToArray(), etiquetasDeHitos = etiquetas.ToArray(), colores = colores.ToArray(), leyenda = leyenda,
                };
                for (int i = 0; i < puntos.Count; i++) d.recorrido[i] = new Vector2((puntos[i].x - minX) / sx, (puntos[i].z - minZ) / sz);
                File.WriteAllText(RutaJson(escena), JsonUtility.ToJson(d, true));
                AssetDatabase.ImportAsset(RutaPng(escena), ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(RutaJson(escena), ImportAssetOptions.ForceUpdate);
                var imp = AssetImporter.GetAtPath(RutaPng(escena)) as TextureImporter;
                if (imp != null)
                {
                    imp.textureType = TextureImporterType.Default; imp.mipmapEnabled = false; imp.maxTextureSize = 2048;
                    imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.npotScale = TextureImporterNPOTScale.None;
                    imp.SaveAndReimport();
                }
                return $"OK {escena}: {puntos.Count} puntos de recorrido, {idx.Count} hitos, mapa {sx:0}x{sz:0} m";
            }
            finally
            {
                if (!string.IsNullOrEmpty(previa) && previa != ruta && EditorSceneManager.GetActiveScene().path != previa) EditorSceneManager.OpenScene(previa);
            }
        }

        // ---------------------------------------------------------------- recorridos
        static void ReunirGameplay(List<Vector3> pts, List<int> idx, List<string> et, List<string> col, out string leyenda)
        {
            var squad = GameObject.Find("PlayerSquad");
            var md = UnityEngine.Object.FindFirstObjectByType<MisionDirector>(FindObjectsInactive.Include);
            Vector3 inicio = squad != null ? squad.transform.position : new Vector3(0f, 0f, -20f);
            Vector3 rehen = md != null ? md.RefugioDelCivil : new Vector3(-2f, 0f, 124f);
            Vector3 heli = md != null ? md.Helipuerto : new Vector3(-26f, 0f, -8f);
            var a = Camino(inicio, rehen); var b = Camino(rehen, heli);
            pts.AddRange(a); idx.Add(0); et.Add("INICIO"); col.Add("#4FC3F7");
            idx.Add(pts.Count - 1); et.Add("REHEN"); col.Add("#FFD54F");
            for (int i = 1; i < b.Count; i++) pts.Add(b[i]);
            idx.Add(pts.Count - 1); et.Add("HELICOPTERO"); col.Add("#81C784");
            leyenda = "RESCATA AL REHEN QUE ESTA AL FONDO DE LA BASE Y VUELVE AL HELICOPTERO";
        }

        static void ReunirOperacion(List<Vector3> pts, List<int> idx, List<string> et, List<string> col, out string leyenda)
        {
            var d = UnityEngine.Object.FindFirstObjectByType<OperacionDirector>(FindObjectsInactive.Include);
            string[] nombres = { "CUARTEL", "MURALLA", "CENTRO DE DATOS", "TANQUE", "CIUDAD", "HELICOPTERO" };
            string[] colores = { "#4FC3F7", "#FF8A65", "#BA68C8", "#FFD54F", "#F06292", "#81C784" };
            if (d != null && d.anclasDeZona != null)
            {
                Vector3? anterior = null;
                for (int i = 0; i < d.anclasDeZona.Length && i < nombres.Length; i++)
                {
                    var t = d.anclasDeZona[i]; if (t == null) continue;
                    var p = t.position;
                    if (anterior.HasValue)
                    {
                        // Entre el patio del tanque y la plaza el recorrido sigue la ruta real del tanque.
                        if (i == 4 && d.rutaDelTanque != null && d.rutaDelTanque.Length > 1)
                            foreach (var r in d.rutaDelTanque) if (r != null) pts.Add(r.position);
                        else { var c = Camino(anterior.Value, p); for (int k = 1; k < c.Count; k++) pts.Add(c[k]); }
                    }
                    else pts.Add(p);
                    if (anterior.HasValue && pts[pts.Count - 1] != p) pts.Add(p);
                    idx.Add(pts.Count - 1); et.Add(nombres[i]); col.Add(colores[i]);
                    anterior = p;
                }
            }
            leyenda = "INFILTRATE, ABRI LOS PUESTOS, HACKEA, HUI EN EL TANQUE, RESISTI Y SUBI AL HELICOPTERO";
        }

        // Camino por el NavMesh entre dos puntos (esquinas); si no hay, linea recta.
        static List<Vector3> Camino(Vector3 a, Vector3 b)
        {
            var r = new List<Vector3>();
            var path = new NavMeshPath();
            bool ok = NavMesh.SamplePosition(a, out var ha, 8f, NavMesh.AllAreas) && NavMesh.SamplePosition(b, out var hb, 8f, NavMesh.AllAreas) && NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, path) && path.corners.Length >= 2;
            if (ok) { r.Add(a); for (int i = 1; i < path.corners.Length - 1; i++) r.Add(path.corners[i]); r.Add(b); }
            else { r.Add(a); r.Add(Vector3.Lerp(a, b, 0.5f)); r.Add(b); }
            return r;
        }

        // ---------------------------------------------------------------- render cenital
        static Texture2D Renderizar(float minX, float maxX, float minZ, float maxZ, int pxLargo)
        {
            float sx = maxX - minX, sz = maxZ - minZ;
            int w, h;
            if (sx >= sz) { w = pxLargo; h = Mathf.Max(64, Mathf.RoundToInt(pxLargo * sz / sx)); }
            else { h = pxLargo; w = Mathf.Max(64, Mathf.RoundToInt(pxLargo * sx / sz)); }
            var go = new GameObject("MapaDeCargaCam");
            var luzGO = new GameObject("MapaDeCargaLuz");
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Color ambPrevio = RenderSettings.ambientLight; var modoPrevio = RenderSettings.ambientMode; float intPrevia = RenderSettings.ambientIntensity;
            var fogPrevio = RenderSettings.fog;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true; cam.orthographicSize = sz * 0.5f; cam.aspect = sx / sz;
                go.transform.SetPositionAndRotation(new Vector3((minX + maxX) * 0.5f, 600f, (minZ + maxZ) * 0.5f), Quaternion.Euler(90f, 0f, 0f));
                cam.nearClipPlane = 1f; cam.farClipPlane = 1500f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.07f, 0.09f, 0.12f);
                cam.allowHDR = false; cam.allowMSAA = false;
                cam.targetTexture = rt;
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; data.renderShadows = false;
                // Luz cenital pareja (los niveles son de noche: sin esto el mapa saldria negro) y ambiente alto.
                var luz = luzGO.AddComponent<Light>(); luz.type = LightType.Directional; luz.intensity = 1.4f; luz.color = Color.white; luz.shadows = LightShadows.None;
                luzGO.transform.rotation = Quaternion.Euler(80f, 20f, 0f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.68f); RenderSettings.fog = false;
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                return tex;
            }
            finally
            {
                RenderSettings.ambientMode = modoPrevio; RenderSettings.ambientLight = ambPrevio; RenderSettings.ambientIntensity = intPrevia; RenderSettings.fog = fogPrevio;
                UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(luzGO);
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
        }
    }
}
