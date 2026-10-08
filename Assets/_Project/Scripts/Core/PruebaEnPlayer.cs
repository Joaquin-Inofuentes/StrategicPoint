using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SP.Core
{
    // Prueba de la build SIN tocar el mouse ni el foco: "StrategicPoint.exe -pruebaop [-pruebaop-seg 60]" entra solo al nivel Operacion Cuartel
    // por el mismo camino que el menu (SceneLoader), saca una captura cada 3 s y escribe un resumen (escena, fps, errores, excepciones) en
    // la carpeta de datos del usuario (Application.persistentDataPath/PruebaOp). Pensada para validar la build desde afuera (CLI).
    // No hace nada si el argumento no esta: un jugador nunca la dispara.
    public class PruebaEnPlayer : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Arrancar()
        {
            var args = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(args, "-pruebaop") < 0) return;
            int seg = 60;
            int i = System.Array.IndexOf(args, "-pruebaop-seg");
            if (i >= 0 && i + 1 < args.Length) int.TryParse(args[i + 1], out seg);
            string escena = "SC_Operacion";
            int ie = System.Array.IndexOf(args, "-pruebaop-escena");
            if (ie >= 0 && ie + 1 < args.Length) escena = args[ie + 1];
            var go = new GameObject("PruebaEnPlayer");
            DontDestroyOnLoad(go);
            go.AddComponent<PruebaEnPlayer>().StartCoroutine(go.GetComponent<PruebaEnPlayer>().Correr(Mathf.Clamp(seg, 10, 600), escena));
        }

        // Apunta la camara a un cartel (se ejecuta despues del rig de camara) para poder fotografiar los textos del mundo.
        [DefaultExecutionOrder(32000)]
        class Mirador : MonoBehaviour
        {
            public Transform objetivo;
            void LateUpdate()
            {
                var c = Camera.main; if (c == null || objetivo == null) return;
                c.transform.position = objetivo.position - objetivo.forward * 9f + Vector3.up * 0.5f;
                c.transform.rotation = Quaternion.LookRotation(objetivo.position - c.transform.position, Vector3.up);
            }
        }

        readonly StringBuilder errores = new StringBuilder();
        int cantErrores; bool cinematicasCortadas;

        void OnEnable() { Application.logMessageReceived += AlLog; }
        void OnDisable() { Application.logMessageReceived -= AlLog; }
        void AlLog(string msg, string pila, LogType tipo)
        {
            if (tipo != LogType.Error && tipo != LogType.Exception && tipo != LogType.Assert) return;
            cantErrores++;
            if (errores.Length < 20000) errores.AppendLine("[" + tipo + "] " + msg + "\n" + pila);
        }

        IEnumerator Correr(int segundos, string escena)
        {
            var ci = CultureInfo.InvariantCulture;
            string dir = Path.Combine(Application.persistentDataPath, "PruebaOp");
            try { Directory.CreateDirectory(dir); foreach (var f in Directory.GetFiles(dir)) File.Delete(f); dir = Path.Combine(dir, escena); Directory.CreateDirectory(dir); } catch (System.Exception) { }
            var res = new StringBuilder();
            res.AppendLine("inicio escena=" + SceneManager.GetActiveScene().name + " pedida=" + escena);
            yield return new WaitForSecondsRealtime(2f);
            SceneLoader.Cargar(escena);
            float t0 = Time.realtimeSinceStartup; int n = 0; float proxima = 6f; int frames0 = Time.frameCount;
            while (Time.realtimeSinceStartup - t0 < segundos)
            {
                yield return null;
                float t = Time.realtimeSinceStartup - t0;
                // Las cinematicas (rapel ~12 s + pasos ~30 s) terminan esperando una tecla: se corta ahi para pasar al juego (por CLI no hay teclado).
                if (!cinematicasCortadas && t >= 56f) { cinematicasCortadas = true; SP.Operacion.CinematicaDeRapel.Saltar(); SP.Operacion.CinematicaDeOperacion.Saltar(); res.AppendLine("t=" + t.ToString("0", ci) + " cinematicas cortadas"); }
                if (t >= proxima)
                {
                    proxima += 3f;
                    yield return new WaitForEndOfFrame();
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    try { File.WriteAllBytes(Path.Combine(dir, "cap_" + (n++).ToString("00", ci) + "_t" + t.ToString("0", ci) + ".png"), tex.EncodeToPNG()); } catch (System.Exception) { }
                    Destroy(tex);
                    res.AppendLine("t=" + t.ToString("0.0", ci) + " escena=" + SceneManager.GetActiveScene().name + " timeScale=" + Time.timeScale.ToString("0.00", ci) + " fps~" + (1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)).ToString("0", ci) + " errores=" + cantErrores);
                }
            }
            // Carteles: se apunta la camara a los 4 TextMesh mas cercanos al jugador y se fotografian (para ver letras mezcladas).
            var mir = gameObject.AddComponent<Mirador>();
            var textos = new System.Collections.Generic.List<TextMesh>();
            var cam0 = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            foreach (var tm in FindObjectsByType<TextMesh>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (tm.text != null && tm.text.Trim().Length >= 5 && tm.GetComponent<MeshRenderer>() != null && tm.GetComponent<MeshRenderer>().bounds.size.x > 2.5f) textos.Add(tm);
            textos.Sort((a, b) => Vector3.Distance(a.transform.position, cam0).CompareTo(Vector3.Distance(b.transform.position, cam0)));
            int desajustados = 0; var fuenteUsada = textos.Count > 0 ? textos[0].font : null;
            var mpb = new MaterialPropertyBlock();
            foreach (var tm in textos) { var mr = tm.GetComponent<MeshRenderer>(); mr.GetPropertyBlock(mpb); var tx = mpb.GetTexture("_MainTex"); if (tx == null && mr.sharedMaterial != null) tx = mr.sharedMaterial.mainTexture; if (tm.font == null || tx != tm.font.material.mainTexture) desajustados++; }
            res.AppendLine("carteles=" + textos.Count + " con textura distinta a la de su fuente=" + desajustados);
            for (int k = 0; k < Mathf.Min(4, textos.Count); k++)
            {
                mir.objetivo = textos[k].transform;
                yield return new WaitForSecondsRealtime(1.5f); yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                try { File.WriteAllBytes(Path.Combine(dir, "cartel_" + k + ".png"), tex.EncodeToPNG()); } catch (System.Exception) { }
                Destroy(tex);
                res.AppendLine("cartel " + k + ": '" + textos[k].text.Replace('\n', '/') + "'");
            }
            res.AppendLine("FIN errores=" + cantErrores + " cuadros=" + (Time.frameCount - frames0));
            res.AppendLine(errores.ToString());
            try { File.WriteAllText(Path.Combine(dir, "resumen.txt"), res.ToString()); } catch (System.Exception) { }
            Application.Quit();
        }
    }
}
