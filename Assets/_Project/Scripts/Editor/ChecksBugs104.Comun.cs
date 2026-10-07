using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;

namespace SP.EditorTools
{
    // Tanda #104-#132, P0: helpers comunes de los checks. Las capturas de validacion usan el prefijo v3_ + numero de bug (Assets/Validacion/).
    public static partial class ChecksBugs065
    {
        // Envuelve OperacionPrueba.Arrancar (que ya salta la cinematica). Devuelve el texto de la prueba ("ERROR..." si no se pudo).
        static string ArrancarEn(int objetivo, int puesto = 1, int subfase = 0) => OperacionPrueba.Arrancar(objetivo, puesto, subfase);

        // Soldados vivos y activos de un bando.
        static List<Soldier> Soldados(TeamId team)
        {
            var l = new List<Soldier>();
            foreach (var a in ActorRegistry.All)
            {
                var s = a as Soldier;
                if (s != null && s.Team == team && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy) l.Add(s);
            }
            return l;
        }

        static Camera CamaraTemporal(Vector3 pos, Vector3 mira, float fov, RenderTexture rt, out GameObject go)
        {
            go = new GameObject("CamChecksTemp");
            var cam = go.AddComponent<Camera>();
            var principal = CamaraPrincipal.Actual;
            if (principal != null) cam.CopyFrom(principal);
            cam.fieldOfView = fov; cam.farClipPlane = 600f;
            cam.transform.position = pos; cam.transform.LookAt(mira);
            cam.targetTexture = rt;
            return cam;
        }

        static Texture2D Leer(RenderTexture rt)
        {
            var previo = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = previo;
            return tex;
        }

        // Render limpio (sin HUD) desde 'pos' mirando a 'mira' a Assets/Validacion/<nombre>.png (usar prefijo v3_NNN_...). Devuelve la ruta.
        static string RenderTemporal(string nombre, Vector3 pos, Vector3 mira, float fov = 60f, int ancho = 1280, int alto = 720)
        {
            var rt = new RenderTexture(ancho, alto, 24);
            var cam = CamaraTemporal(pos, mira, fov, rt, out var go);
            cam.Render();
            var tex = Leer(rt);
            string ruta = RutaValidacion(nombre.EndsWith(".png") ? nombre : nombre + ".png");
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(go); Object.Destroy(tex);
            return ruta;
        }

        // Cuantos pixeles del renderer se ven desde 'cam' (a ancho x alto) teniendo en cuenta la oclusion real. Se renderiza dos veces: con el
        // renderer apagado (fondo) y con el renderer teñido de un color plano (Unlit); se cuentan los pixeles que cambian. Comparar contra
        // el fondo (y no buscar un color exacto) es inmune al postprocesado/niebla de la camara, que corre los colores (un magenta puro salia
        // verde palido en la Operacion). Restaura los materiales y el estado del renderer.
        static int ContarPixelesVisibles(Renderer r, Camera cam, int ancho = 1280, int alto = 720)
        {
            if (r == null || cam == null) return 0;
            var sh = Shader.Find("Unlit/Color") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(sh); mat.color = Color.magenta;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.magenta);
            var originales = r.sharedMaterials;
            bool habilitado = r.enabled;
            var temporales = new Material[originales.Length];
            for (int i = 0; i < temporales.Length; i++) temporales[i] = mat;
            var rt = new RenderTexture(ancho, alto, 24);
            var antes = cam.targetTexture; var antesPp = cam.allowMSAA;
            cam.targetTexture = rt; cam.allowMSAA = false;
            r.enabled = false;
            cam.Render();
            var t1 = Leer(rt); var fondo = t1.GetPixels32(); Object.Destroy(t1);
            r.enabled = true; r.sharedMaterials = temporales;
            cam.Render();
            var t2 = Leer(rt); var conPieza = t2.GetPixels32(); Object.Destroy(t2);
            cam.targetTexture = antes; cam.allowMSAA = antesPp;
            r.sharedMaterials = originales; r.enabled = habilitado;
            int n = 0;
            for (int i = 0; i < conPieza.Length; i++)
            {
                int d = Mathf.Abs(conPieza[i].r - fondo[i].r) + Mathf.Abs(conPieza[i].g - fondo[i].g) + Mathf.Abs(conPieza[i].b - fondo[i].b);
                if (d > 90) n++;
            }
            Object.Destroy(rt); Object.Destroy(mat);
            return n;
        }

        // Para sumarle a los checks asincronos: espera real y devuelve (usar con foreach (var x in EsperarSeg(1f)) yield return x;).
        static IEnumerable EsperarSeg(float s) => Esperar(s);
    }
}
