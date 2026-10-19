using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Player;
using SP.Vehicles;

namespace SP.Presentation
{
    // Bug #219 ("me obstaculiza algo: aplica transparencia a lo que esta entre la camara y el seleccionado"): paredes, techos y
    // cuerpos altos que quedan entre la camara y un soldado seleccionado (RTS) o poseido se vuelven translucidos y recuperan su
    // aspecto apenas dejan de taparlo. Se hace con una copia transparente del material (URP) cacheada por material original y un
    // MaterialPropertyBlock por renderer para el alfa: el material original nunca se toca.
    public class TransparenciaDeOclusores : MonoBehaviour
    {
        public const float AlfaTapando = 0.22f;
        public const float VelocidadDeFundido = 6f;
        public const float PeriodoDeSondeo = 0.1f;
        public const float RadioDelSondeo = 0.08f;

        sealed class Estado
        {
            public Renderer renderer;
            public Material[] originales;
            public Material[] transparentes;
            public float alfa = 1f;
            public bool tapando;
        }

        readonly Dictionary<Renderer, Estado> estados = new Dictionary<Renderer, Estado>();
        readonly List<Renderer> aLiberar = new List<Renderer>();
        readonly HashSet<Renderer> tapandoAhora = new HashSet<Renderer>();
        readonly RaycastHit[] hits = new RaycastHit[64];
        float proximoSondeo;
        static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int IdColor = Shader.PropertyToID("_Color");

        public static TransparenciaDeOclusores Instancia { get; private set; }
        public int CantidadTransparentes { get { int n = 0; foreach (var e in estados.Values) if (e.tapando && e.alfa < 0.99f) n++; return n; } }
        public bool EstaTransparente(Renderer r) => r != null && estados.TryGetValue(r, out var e) && e.tapando && e.alfa < 0.99f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            if (Instancia != null) return;
            var go = new GameObject("TransparenciaDeOclusores") { hideFlags = HideFlags.DontSave };
            Instancia = go.AddComponent<TransparenciaDeOclusores>();
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            Restaurar();
        }

        void LateUpdate()
        {
            if (Time.unscaledTime >= proximoSondeo)
            {
                proximoSondeo = Time.unscaledTime + PeriodoDeSondeo;
                Sondear();
            }
            Fundir(Time.unscaledDeltaTime);
        }

        // Quien tiene que verse: los seleccionados en RTS y el soldado que maneja el jugador.
        void Objetivos(List<Soldier> lista)
        {
            var drv = PlayerInputDriver.Activo;
            if (drv == null) return;
            if (drv.Brain != null && drv.Brain.Current != null) lista.Add(drv.Brain.Current);
            if (drv.Selection != null)
                foreach (var s in drv.Selection.Selected) if (s != null && !lista.Contains(s)) lista.Add(s);

            // "Disparo a las torretas de vigia: cuando quiero dispararles no veo a los enemigos": postes, escudos y parapetos tapaban al
            // tirador de la torre. Tambien se destapa al enemigo que esta en la mira y a los tiradores de torre que se tienen delante.
            var cam = SP.Core.CamaraPrincipal.Actual;
            if (cam == null) return;
            propios = lista.Count;
            var mira = drv.UltimaMira;
            if (mira.Type == AimTargetType.Enemy && mira.Soldier != null && !lista.Contains(mira.Soldier)) lista.Add(mira.Soldier);
            foreach (var t in SP.Operacion.TiradorDeTorre.Todos)
            {
                var s = t != null && t.Activo ? t.soldado : null;
                if (s == null || lista.Contains(s)) continue;
                var d = s.transform.position - cam.transform.position;
                float dist = d.magnitude;
                if (dist < 4f || dist > DistanciaDeTorres || Vector3.Angle(cam.transform.forward, d) > AnguloDeTorres) continue;
                lista.Add(s);
            }
        }

        public const float DistanciaDeTorres = 90f, AnguloDeTorres = 32f;

        readonly List<Soldier> objetivos = new List<Soldier>();
        int propios;   // los primeros "propios" de la lista son tuyos (poseido/seleccionados); el resto son enemigos que se quieren ver
        // Bug #267 ("veo a traves del muro"): para destapar a un enemigo solo se aclaran postes, escudos y parapetos; las paredes largas del
        // perimetro y los containers NUNCA se vuelven transparentes por un enemigo.
        public const float LadoMaximoParaEnemigos = 3.5f;   // postes, escudos y parapetos; un container (6 m) o un muro largo no

        readonly HashSet<Renderer> tapaPecho = new HashSet<Renderer>();
        readonly HashSet<Renderer> tapaCabeza = new HashSet<Renderer>();

        // Renderers altos (>1 m) que corta un rayo fino desde 'origen' hasta 'meta' (sin contar soldados ni vehiculos).
        void BloqueadoresHacia(Vector3 origen, Vector3 meta, HashSet<Renderer> salida, float ladoMax)
        {
            var dir = meta - origen; float dist = dir.magnitude;
            if (dist < 0.1f) return;
            int n = Physics.SphereCastNonAlloc(origen, RadioDelSondeo, dir / dist, hits, dist - 0.6f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || c.GetComponentInParent<Soldier>() != null || c.GetComponentInParent<Vehicle>() != null) continue;
                var r = c.GetComponent<MeshRenderer>();
                if (r == null) r = c.GetComponentInParent<MeshRenderer>();
                if (r == null || !r.enabled) continue;
                if (r.bounds.size.y < 1f) continue;   // pisos y cosas bajas no tapan
                if (r.bounds.Contains(origen)) continue;
                if (Mathf.Max(r.bounds.size.x, r.bounds.size.z) > ladoMax) continue;
                salida.Add(r);
            }
        }

        void Sondear()
        {
            tapandoAhora.Clear();
            var cam = SP.Core.CamaraPrincipal.Actual;
            if (cam != null)
            {
                objetivos.Clear();
                Objetivos(objetivos);
                var origen = cam.transform.position;
                for (int oi = 0; oi < objetivos.Count; oi++)
                {
                    var s = objetivos[oi];
                    float ladoMax = oi < propios ? float.MaxValue : LadoMaximoParaEnemigos;
                    if (s == null || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                    // Bug #258: el container de al lado se hacia transparente sin tapar nada. Solo cuenta lo que tapa de verdad: debe cortar
                    // el rayo (fino) hacia el pecho Y el rayo hacia la cabeza; un objeto que roza el borde de la vista no se toca.
                    float dist = Vector3.Distance(origen, s.transform.position + Vector3.up * 0.9f);
                    if (dist < 2.5f) continue;   // en primera persona la camara esta dentro del soldado: no hay nada entre medio
                    tapaPecho.Clear(); tapaCabeza.Clear();
                    BloqueadoresHacia(origen, s.transform.position + Vector3.up * 0.9f, tapaPecho, ladoMax);
                    if (tapaPecho.Count == 0) continue;
                    BloqueadoresHacia(origen, s.transform.position + Vector3.up * 1.6f, tapaCabeza, ladoMax);
                    foreach (var r in tapaPecho) if (tapaCabeza.Contains(r)) tapandoAhora.Add(r);
                }
            }

            foreach (var r in tapandoAhora)
            {
                if (!estados.TryGetValue(r, out var e))
                {
                    e = Preparar(r);
                    if (e == null) continue;
                    estados[r] = e;
                }
                e.tapando = true;
            }
            foreach (var kv in estados) if (!tapandoAhora.Contains(kv.Key)) kv.Value.tapando = false;
        }

        Estado Preparar(Renderer r)
        {
            var orig = r.sharedMaterials;
            if (orig == null || orig.Length == 0) return null;
            var copia = new Material[orig.Length];
            for (int i = 0; i < orig.Length; i++)
            {
                copia[i] = CopiaTransparente(orig[i]);
                if (copia[i] == null) return null;
            }
            return new Estado { renderer = r, originales = orig, transparentes = copia };
        }

        static Material CopiaTransparente(Material m)
        {
            if (m == null) return null;
            var c = new Material(m) { name = m.name + " (transparente)", hideFlags = HideFlags.HideAndDontSave };
            if (c.HasProperty("_Surface"))
            {
                c.SetFloat("_Surface", 1f);
                c.SetFloat("_Blend", 0f);
                if (c.HasProperty("_SrcBlend")) c.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (c.HasProperty("_DstBlend")) c.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (c.HasProperty("_SrcBlendAlpha")) c.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                if (c.HasProperty("_DstBlendAlpha")) c.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (c.HasProperty("_ZWrite")) c.SetFloat("_ZWrite", 0f);
                c.SetOverrideTag("RenderType", "Transparent");
                c.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                c.DisableKeyword("_ALPHATEST_ON");
                c.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return c;
        }

        void Fundir(float dt)
        {
            aLiberar.Clear();
            foreach (var kv in estados)
            {
                var e = kv.Value;
                if (e.renderer == null) { aLiberar.Add(kv.Key); continue; }
                float meta = e.tapando ? AlfaTapando : 1f;
                float antes = e.alfa;
                e.alfa = Mathf.MoveTowards(e.alfa, meta, VelocidadDeFundido * dt);
                if (e.alfa >= 0.999f && !e.tapando)
                {
                    e.renderer.sharedMaterials = e.originales;
                    foreach (var mt in e.transparentes) if (mt != null) Destroy(mt);
                    aLiberar.Add(kv.Key);
                    continue;
                }
                if (antes >= 0.999f && e.alfa < 0.999f) e.renderer.sharedMaterials = e.transparentes;
                for (int i = 0; i < e.transparentes.Length; i++)
                {
                    var mt = e.transparentes[i]; var mo = e.originales[i];
                    if (mt == null || mo == null) continue;
                    if (mo.HasProperty(IdBaseColor)) { var col = mo.GetColor(IdBaseColor); col.a = e.alfa; mt.SetColor(IdBaseColor, col); }
                    else if (mo.HasProperty(IdColor)) { var col = mo.GetColor(IdColor); col.a = e.alfa; mt.SetColor(IdColor, col); }
                }
            }
            foreach (var r in aLiberar) estados.Remove(r);
        }

        // Devuelve todo a como estaba (al salir del juego o al destruir el componente).
        void Restaurar()
        {
            foreach (var kv in estados)
                if (kv.Value.renderer != null) { kv.Value.renderer.sharedMaterials = kv.Value.originales; foreach (var mt in kv.Value.transparentes) if (mt != null) Destroy(mt); }
            estados.Clear();
        }
    }
}
