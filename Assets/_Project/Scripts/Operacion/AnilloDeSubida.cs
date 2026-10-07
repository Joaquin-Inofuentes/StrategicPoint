using UnityEngine;

namespace SP.Operacion
{
    // Bug #111: en HUIR, cuando el tanque ya es nuestro y hay que subirse, un anillo vibrante en el piso lo marca (el HUD y la baliza no
    // alcanzaban: "no se entiende que hay que subir"). Dos aros emisivos amarillo/naranja que giran y laten entre 0,8 y 1,4 veces su
    // tamano a 3 Hz, uno en contrafase del otro. Se prende solo en la subfase Abordaje mientras el jugador NO esta en el tanque y se
    // apaga al subir (o al salir de la fase). Lo gobierna OperacionDirector.Update (TickAnilloDeSubida); no hay nada en la escena.
    public class AnilloDeSubida : MonoBehaviour
    {
        public const float FrecuenciaHz = 3f, EscalaMin = 0.8f, EscalaMax = 1.4f, GiroGradosPorSeg = 140f;
        static AnilloDeSubida instancia;
        public static AnilloDeSubida Instancia => instancia;
        public static bool EstaActivo => instancia != null && instancia.gameObject.activeSelf;

        Transform a, b;
        MeshRenderer ra, rb;
        MaterialPropertyBlock bloque;
        Transform sigue;
        float radioBase = 3.4f, t;
        public float EscalaActual { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { instancia = null; }

        // Muestra el anillo en el piso bajo 'objetivo' (sigue su posicion mientras exista). Devuelve la instancia.
        public static AnilloDeSubida Mostrar(Transform objetivo, float radio = 3.4f)
        {
            if (!Application.isPlaying || objetivo == null) return null;
            if (instancia == null) instancia = Crear();
            instancia.sigue = objetivo; instancia.radioBase = radio;
            if (!instancia.gameObject.activeSelf) { instancia.t = 0f; instancia.gameObject.SetActive(true); }
            instancia.Ubicar();
            instancia.Pintar();
            return instancia;
        }

        public static void Ocultar()
        {
            if (instancia != null && instancia.gameObject.activeSelf) instancia.gameObject.SetActive(false);
        }

        static AnilloDeSubida Crear()
        {
            var go = new GameObject("AnilloDeSubida") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            var r = go.AddComponent<AnilloDeSubida>();
            r.bloque = new MaterialPropertyBlock();
            r.a = NuevoAro(go.transform, "AroA", out r.ra);
            r.b = NuevoAro(go.transform, "AroB", out r.rb);
            go.SetActive(false);
            return r;
        }

        static Transform NuevoAro(Transform padre, string nombre, out MeshRenderer r)
        {
            var q = new GameObject(nombre);
            q.transform.SetParent(padre, false);
            q.AddComponent<MeshFilter>().sharedMesh = AnilloDeAviso.MallaPlana();
            r = q.AddComponent<MeshRenderer>();
            r.sharedMaterial = AnilloDeAviso.MaterialDeAro();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return q.transform;
        }

        void Ubicar()
        {
            if (sigue == null) return;
            var p = sigue.position;
            transform.position = new Vector3(p.x, AnilloDeAviso.AlturaDelPisoEn(p) + 0.12f, p.z);
        }

        void Update()
        {
            if (sigue == null) { gameObject.SetActive(false); return; }
            t += Time.unscaledDeltaTime;
            Ubicar();
            Pintar();
        }

        void Pintar()
        {
            float fase = t * FrecuenciaHz * Mathf.PI * 2f;
            float k = 0.5f + 0.5f * Mathf.Sin(fase);
            float k2 = 1f - k;
            float esc = Mathf.Lerp(EscalaMin, EscalaMax, k);
            EscalaActual = esc;
            float diametro = radioBase * 2f;
            a.localScale = new Vector3(diametro * esc, 1f, diametro * esc);
            float esc2 = Mathf.Lerp(EscalaMin, EscalaMax, k2) * 0.8f;
            b.localScale = new Vector3(diametro * esc2, 1f, diametro * esc2);
            a.localRotation = Quaternion.Euler(0f, t * GiroGradosPorSeg, 0f);
            b.localRotation = Quaternion.Euler(0f, -t * GiroGradosPorSeg * 1.4f, 0f);
            var amarillo = new Color(1f, 0.92f, 0.1f, Mathf.Lerp(0.7f, 1f, k));
            var naranja = new Color(1f, 0.45f, 0.05f, Mathf.Lerp(0.7f, 1f, k2));
            bloque.SetColor("_BaseColor", amarillo); bloque.SetColor("_Color", amarillo); ra.SetPropertyBlock(bloque);
            bloque.SetColor("_BaseColor", naranja); bloque.SetColor("_Color", naranja); rb.SetPropertyBlock(bloque);
        }
    }
}
