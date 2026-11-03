using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using SP.Operacion;
using G = SP.Operacion.MansionGeometria;

namespace SP.EditorTools
{
    // "Prestigio": cien pasadas de ambiente, decoracion y juego de luces de la ciudad (todo visual, sin colliders; el NavMesh no cambia).
    //   1-12   ambiente global (luz, niebla, post-proceso, luna, nubes)          -> AmbienteGlobal
    //   13-40  calles: farolas, guirnaldas, arco de entrada, mobiliario           -> PrestigioCalles   (OperacionBuilder.PrestigioCalles.cs)
    //   41-60  plaza: fuente con luces de colores, glorietas, reflectores          -> PrestigioPlaza    (OperacionBuilder.PrestigioPlaza.cs)
    //   61-80  edificios (dentro de ArmarEdificio, OperacionBuilder.CiudadIntegral.cs)
    //   81-100 fachada y entorno de la mansion                                     -> PrestigioMansion  (OperacionBuilder.PrestigioMansion.cs)
    public static partial class OperacionBuilder
    {
        const string GrupoPrestigio = "11_Prestigio";

        [MenuItem("Strategic Point/Arte/Ciudad/Prestigio (100 pasadas)")]
        static void PrestigioMenu() => Debug.Log(ConstruirPrestigio());

        static Material MatTrans(string clave, Color c, float emision, bool aditivo)
        {
            string path = $"{CarpetaMat}/M_Op_{clave}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Op_" + clave }; AssetDatabase.CreateAsset(mat, path); }
            mat.SetFloat("_Surface", 1f); mat.SetFloat("_Blend", aditivo ? 2f : 0f);
            mat.SetFloat("_SrcBlend", 5f); mat.SetFloat("_DstBlend", aditivo ? 1f : 10f); mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = 3000;
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetColor("_BaseColor", c);
            if (emision > 0f) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", new Color(c.r, c.g, c.b, 1f) * emision); }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material MNeblina => MatTrans("Prestigio_Neblina", new Color(0.7f, 0.8f, 1f, 0.08f), 0f, false);
        static Material MHaz => MatTrans("Prestigio_Haz", new Color(0.7f, 0.85f, 1f, 0.07f), 1.2f, true);
        static Material MHazDorado => MatTrans("Prestigio_HazDorado", new Color(1f, 0.8f, 0.4f, 0.08f), 1.2f, true);
        static Material MNube => MatTrans("Prestigio_Nube", new Color(0.18f, 0.22f, 0.34f, 0.55f), 0f, false);
        static Material MLuna => Mat("Prestigio_Luna", new Color(0.92f, 0.95f, 1f), 2.2f);
        static Material MLunaHalo => MatTrans("Prestigio_LunaHalo", new Color(0.7f, 0.8f, 1f, 0.12f), 1f, true);
        static Material MAguaViva => Mat("Prestigio_AguaViva", new Color(0.35f, 0.75f, 1f), 1.6f);
        static Material MBombilla => Mat("Prestigio_Bombilla", new Color(1f, 0.82f, 0.45f), 3.5f);
        static Material MFlama => Mat("Prestigio_Flama", new Color(1f, 0.5f, 0.12f), 3.5f);
        static Material MNeonRosa => Mat("Prestigio_NeonRosa", new Color(1f, 0.25f, 0.6f), 3f);
        static Material MNeonCian => Mat("Prestigio_NeonCian", new Color(0.2f, 0.9f, 1f), 3f);
        static Material MBlanco => Mat("Prestigio_Blanco", new Color(0.92f, 0.92f, 0.9f));
        static Material MFlor(int i) => Mat("Prestigio_Flor" + (i % 4), i % 4 == 0 ? new Color(0.95f, 0.3f, 0.45f) : i % 4 == 1 ? new Color(0.98f, 0.85f, 0.3f) : i % 4 == 2 ? new Color(0.75f, 0.45f, 0.95f) : new Color(1f, 0.6f, 0.25f));

        static Light FocoSpot(Transform p, string n, Vector3 pos, Vector3 mira, float angulo, float rango, float intensidad, Color c)
        {
            var g = new GameObject(n); g.transform.SetParent(p, false); g.transform.position = pos; g.transform.rotation = Quaternion.LookRotation((mira - pos).normalized);
            var l = g.AddComponent<Light>(); l.type = LightType.Spot; l.spotAngle = angulo; l.innerSpotAngle = angulo * 0.6f; l.range = rango; l.intensity = intensidad; l.color = c; l.shadows = LightShadows.None;
            return l;
        }

        static JuegoDeLuces Animar(GameObject g, JuegoDeLuces.Modo modo, float vel, float amp, Color? a = null, Color? b = null)
        {
            var j = g.AddComponent<JuegoDeLuces>(); j.modo = modo; j.velocidad = vel; j.amplitud = amp;
            if (a.HasValue) j.colorA = a.Value; if (b.HasValue) j.colorB = b.Value;
            return j;
        }

        // Reflector que barre el cielo: foco + haz visible (cilindro aditivo) que gira.
        static void Reflector(Transform p, string n, Vector3 pos, float yaw, float inclinacion, Color c, JuegoDeLuces.Modo modo, float vel)
        {
            var g = new GameObject(n); g.transform.SetParent(p, false); g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(-inclinacion, yaw, 0f);
            var l = g.AddComponent<Light>(); l.type = LightType.Spot; l.spotAngle = 14f; l.range = 90f; l.intensity = 6f; l.color = c; l.shadows = LightShadows.None;
            var haz = GameObject.CreatePrimitive(PrimitiveType.Cylinder); haz.name = "Haz"; haz.transform.SetParent(g.transform, false);
            haz.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); haz.transform.localPosition = new Vector3(0f, 0f, 45f); haz.transform.localScale = new Vector3(3.2f, 45f, 3.2f);
            haz.GetComponent<MeshRenderer>().sharedMaterial = MHaz; Object.DestroyImmediate(haz.GetComponent<Collider>());
            var j = Animar(g, modo, vel, 1f); j.eje = Vector3.up;
            Cilindro(p, n + "_Base", pos.x, pos.y - 0.6f, pos.y + 0.2f, pos.z, 1.1f, MBarrote);
        }

        // Pasadas 1-12: ambiente global.
        static void AmbienteGlobal(StringBuilderLite res, Transform grupo)
        {
            // 1-3: luz ambiente mas calida y luna mas clara; niebla azul algo mas espesa para dar profundidad.
            RenderSettings.ambientLight = new Color(0.17f, 0.19f, 0.30f);
            RenderSettings.fogColor = new Color(0.10f, 0.13f, 0.24f); RenderSettings.fogDensity = 0.0075f;
            var sol = RenderSettings.sun; if (sol != null) { sol.intensity = 0.55f; sol.color = new Color(0.62f, 0.74f, 1f); }
            // 4-7: post-proceso: bloom fuerte para las luces, vignette suave, color mas vivo y exposicion levemente arriba.
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
            {
                if (v.sharedProfile == null || v.name != "PostProceso_Noche") continue;
                if (v.sharedProfile.TryGet(out Bloom b)) { b.intensity.Override(0.9f); b.threshold.Override(0.85f); b.scatter.Override(0.7f); }
                if (v.sharedProfile.TryGet(out Vignette vg)) vg.intensity.Override(0.28f);
                if (v.sharedProfile.TryGet(out ColorAdjustments ca)) { ca.postExposure.Override(0.35f); ca.saturation.Override(14f); ca.contrast.Override(10f); }
                EditorUtility.SetDirty(v.sharedProfile);
            }
            // 8: luz de relleno calida del lado opuesto a la luna (sin sombras).
            var rel = new GameObject("LuzDeRelleno"); rel.transform.SetParent(grupo, false); rel.transform.rotation = Quaternion.Euler(35f, 26f, 0f);
            var lr = rel.AddComponent<Light>(); lr.type = LightType.Directional; lr.intensity = 0.16f; lr.color = new Color(1f, 0.7f, 0.5f); lr.shadows = LightShadows.None;
            // 9-11: luna con halo y banco de nubes altas.
            var cen = new Vector3(322f, 0f, 230f);
            Vector3 dirLuna = sol != null ? -sol.transform.forward : new Vector3(0.4f, 0.5f, 0.7f).normalized;
            var luna = new Vector3(cen.x, 0f, cen.z) + dirLuna * 520f; luna.y = Mathf.Max(luna.y, 150f);
            Esfera(grupo, "Luna", luna, 55f, MLuna); Esfera(grupo, "Luna_Halo", luna, 130f, MLunaHalo);
            var rnd = new System.Random(5);
            for (int i = 0; i < 14; i++)
            {
                var nube = GameObject.CreatePrimitive(PrimitiveType.Sphere); nube.name = "Nube"; nube.transform.SetParent(grupo, false);
                nube.transform.position = new Vector3(120f + (float)rnd.NextDouble() * 420f, 210f + (float)rnd.NextDouble() * 30f, -40f + (float)rnd.NextDouble() * 480f);
                nube.transform.localScale = new Vector3(70f + (float)rnd.NextDouble() * 60f, 7f, 28f + (float)rnd.NextDouble() * 20f);
                nube.GetComponent<MeshRenderer>().sharedMaterial = MNube; Object.DestroyImmediate(nube.GetComponent<Collider>());
            }
            res.Add("1-12 ambiente global: luz, niebla, bloom, luna con halo, 14 nubes, relleno calido");
        }

        public static string ConstruirPrestigio()
        {
            var res = new StringBuilderLite();
            var esc = SceneManager.GetSceneByPath(RutaEscenaDeSector("Ciudad"));
            if (!esc.IsValid() || !esc.isLoaded) return "La escena Ciudad no esta abierta (aisl.sh Completa).";
            Transform ciudad = null; foreach (var r in esc.GetRootGameObjects()) if (r.name == "Ciudad") ciudad = r.transform;
            if (ciudad == null) return "Sin raiz Ciudad.";
            var viejo = ciudad.Find(GrupoPrestigio); if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
            var grupo = new GameObject(GrupoPrestigio).transform; grupo.SetParent(ciudad, false);
            AsegurarCarpeta(CarpetaElementos);

            void Instanciar(GameObject escenaria, string nombrePrefab)
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(escenaria, CarpetaElementos + "/" + nombrePrefab + ".prefab");
                Object.DestroyImmediate(escenaria);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, esc);
                inst.transform.SetParent(grupo, false); inst.transform.position = Vector3.zero;
            }

            var ambiente = new GameObject("P_Op_Pres_Ambiente"); AmbienteGlobal(res, ambiente.transform); Instanciar(ambiente, "P_Op_Pres_Ambiente");
            var calles = new GameObject("P_Op_Pres_Calles"); PrestigioCalles(res, calles.transform); Instanciar(calles, "P_Op_Pres_Calles");
            var plaza = new GameObject("P_Op_Pres_Plaza"); PrestigioPlaza(res, plaza.transform); Instanciar(plaza, "P_Op_Pres_Plaza");
            var mans = new GameObject("P_Op_Pres_Mansion"); PrestigioMansion(res, mans.transform); Instanciar(mans, "P_Op_Pres_Mansion");
            EditorSceneManager.MarkSceneDirty(esc);
            AssetDatabase.SaveAssets();
            return res.ToString();
        }
    }
}
