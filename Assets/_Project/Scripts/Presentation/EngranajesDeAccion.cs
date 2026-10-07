using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Core;
using SP.Player;

namespace SP.Presentation
{
    // Bug #066: "engranaje girando sobre el operador y sobre el interactuable". Por cada accion que reporta AccionesEnCurso
    // (curar, reanimar, detonar, desactivar un panel, hackear) hay DOS engranajes en el mundo, de un pool de 8:
    //   * uno sobre el que opera (40 cm) y otro sobre el objetivo (55 cm);
    //   * giran en el eje de la vista en sentidos opuestos (140 y -110 grados por segundo), como piezas engranadas, con un pulso
    //     de escala de 1.0 a 1.08 a 2 Hz;
    //   * al terminar la accion se desvanecen en 0.25 s (la accion se da por terminada 0.15 s despues del ultimo reporte).
    // Mismo patron que AccionesEnCursoView: una instancia unica que se arma sola al cargar cada escena y lee AccionesEnCurso.
    // El engranaje apuntado por la mira (InteractGearMarker) tiene su propio giro.
    public class EngranajesDeAccion : MonoBehaviour
    {
        public const int TamanoDelPool = 8;
        public const float VelocidadActor = 140f, VelocidadObjetivo = -110f;   // grados por segundo
        public const float TamanoActor = 0.40f, TamanoObjetivo = 0.55f;        // metros (ancho del quad)
        public const float AlturaActor = 1.4f, AlturaObjetivoSoldado = 1.8f;   // sobre el pivote (que esta a 0.8 m de los pies): 2.2 y 2.6 m sobre el piso
        public const float EntradaSegundos = 0.12f, SalidaSegundos = 0.25f;
        public const float SilencioParaTerminar = 0.15f;                       // sin reportes durante este tiempo = la accion termino
        public const float PulsoHz = 2f, PulsoAmplitud = 0.08f;

        sealed class Engranaje
        {
            public GameObject go;
            public Transform tr;
            public MeshRenderer mr;
            public int actorId;
            public bool esObjetivo;
            public bool enUso;
            public bool saliendo;
            public bool visto;
            public float angulo, alfa, fase;
            public Vector3 pos;
            public Color color;
        }

        public struct Muestra
        {
            public int ActorId;
            public bool EsObjetivo;
            public float Angulo;     // acumulado, en grados (no se normaliza para poder medir el giro entre dos instantes)
            public float Alfa;
            public bool Saliendo;
            public Vector3 Posicion;
            public float Tamano;     // lado del quad en metros, con pulso y compensacion de distancia
        }

        static EngranajesDeAccion instancia;
        public static EngranajesDeAccion Instancia => instancia;

        static readonly Color ColorDelActor = new Color(1f, 0.82f, 0.2f, 1f);
        static readonly Color ColorDelObjetivo = new Color(0.78f, 0.95f, 1f, 1f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        readonly List<Engranaje> pool = new List<Engranaje>();
        readonly List<AccionesEnCurso.Accion> acciones = new List<AccionesEnCurso.Accion>();
        Material material;
        Mesh malla;
        MaterialPropertyBlock bloque;
        float reloj;

        // Engranajes visibles y NO desvaneciendose = los que corresponden a una accion en curso.
        public static int Activos
        {
            get
            {
                if (instancia == null) return 0;
                int n = 0;
                foreach (var g in instancia.pool) if (g.enUso && !g.saliendo) n++;
                return n;
            }
        }

        // Engranajes dibujados ahora (incluye los que se estan desvaneciendo).
        public static int Visibles
        {
            get
            {
                if (instancia == null) return 0;
                int n = 0;
                foreach (var g in instancia.pool) if (g.enUso) n++;
                return n;
            }
        }

        public static List<Muestra> Muestras()
        {
            var l = new List<Muestra>();
            if (instancia == null) return l;
            foreach (var g in instancia.pool)
            {
                if (!g.enUso) continue;
                l.Add(new Muestra
                {
                    ActorId = g.actorId, EsObjetivo = g.esObjetivo, Angulo = g.angulo, Alfa = g.alfa, Saliendo = g.saliendo,
                    Posicion = g.pos, Tamano = g.tr != null ? g.tr.localScale.x : 0f,
                });
            }
            return l;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetearInstancia() => instancia = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            if (!Application.isPlaying) return;
            Asegurar();
        }

        public static EngranajesDeAccion Asegurar()
        {
            if (instancia != null) return instancia;
            var go = new GameObject("EngranajesDeAccion");
            DontDestroyOnLoad(go);
            instancia = go.AddComponent<EngranajesDeAccion>();
            return instancia;
        }

        void Awake()
        {
            if (instancia != null && instancia != this) { Destroy(gameObject); return; }
            instancia = this;
            malla = new Mesh { name = "EngranajeQuad", hideFlags = HideFlags.HideAndDontSave };
            malla.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            malla.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            malla.colors = new[] { Color.white, Color.white, Color.white, Color.white };   // UI/Default multiplica por el color de vertice
            malla.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            malla.RecalculateNormals();
            malla.RecalculateBounds();
            // ZTest siempre: el engranaje del objetivo no puede quedar tapado por el cartel o el cuerpo del panel (ni por un muro en RTS).
            material = DiamondGizmo.NuevoMaterialTransparente(Color.white, DiamondGizmo.TexturaEngranajeConAlfa(), zTestAlways: true);
            bloque = new MaterialPropertyBlock();
            for (int i = 0; i < TamanoDelPool; i++)
            {
                var go = new GameObject("Engranaje" + i);
                go.transform.SetParent(transform, false);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = malla;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                go.SetActive(false);
                pool.Add(new Engranaje { go = go, tr = go.transform, mr = mr });
            }
        }

        void OnDestroy()
        {
            if (instancia == this) instancia = null;
            if (material != null) Destroy(material);
            if (malla != null) Destroy(malla);
        }

        Engranaje Buscar(int actorId, bool esObjetivo)
        {
            foreach (var g in pool) if (g.enUso && g.actorId == actorId && g.esObjetivo == esObjetivo) return g;
            return null;
        }

        Engranaje Tomar(int actorId, bool esObjetivo)
        {
            var g = Buscar(actorId, esObjetivo);
            if (g != null) return g;
            foreach (var libre in pool)
            {
                if (libre.enUso) continue;
                libre.enUso = true; libre.saliendo = false; libre.actorId = actorId; libre.esObjetivo = esObjetivo;
                libre.alfa = 0f; libre.angulo = Random.Range(0f, 45f); libre.fase = Random.value;
                libre.color = esObjetivo ? ColorDelObjetivo : ColorDelActor;
                libre.go.SetActive(true);
                return libre;
            }
            return null;
        }

        void LateUpdate() => Refrescar();

        // Publico: la suite lo ejerce sin esperar un frame.
        public void Refrescar()
        {
            float dt = Time.unscaledDeltaTime;
            reloj += dt;
            var cam = CamaraPrincipal.Actual;
            float ahora = Time.realtimeSinceStartup;
            foreach (var g in pool) g.visto = false;

            AccionesEnCurso.Vigentes(acciones);
            for (int i = 0; i < acciones.Count; i++)
            {
                var a = acciones[i];
                if (a.Actor == null || !a.Actor.gameObject.activeInHierarchy) continue;
                if (ahora - a.Estampa > SilencioParaTerminar) continue;   // ya dejo de reportarse: sus engranajes se van desvaneciendo

                var ga = Tomar(a.Actor.Id, false);
                var go = Tomar(a.Actor.Id, true);
                if (ga == null || go == null) continue;
                ga.pos = a.Actor.transform.position + Vector3.up * AlturaActor;
                go.pos = AnclaDelObjetivo(a);
                ga.visto = go.visto = true;
            }

            foreach (var g in pool)
            {
                if (!g.enUso) continue;
                g.saliendo = !g.visto;
                float objetivoAlfa = g.saliendo ? 0f : 1f;
                float vel = g.saliendo ? 1f / SalidaSegundos : 1f / EntradaSegundos;
                g.alfa = Mathf.MoveTowards(g.alfa, objetivoAlfa, vel * dt);
                if (g.saliendo && g.alfa <= 0f)
                {
                    g.enUso = false;
                    g.go.SetActive(false);
                    continue;
                }
                g.angulo += (g.esObjetivo ? VelocidadObjetivo : VelocidadActor) * dt;

                float pulso = 1f + PulsoAmplitud * 0.5f * (1f + Mathf.Sin((reloj + g.fase) * Mathf.PI * 2f * PulsoHz));
                float basePx = g.esObjetivo ? TamanoObjetivo : TamanoActor;
                float k = 1f;
                if (cam != null)
                {
                    // De lejos 40 cm no se leen: se agranda con la distancia (sin pasar de 3.5x).
                    float d = Vector3.Distance(cam.transform.position, g.pos);
                    k = Mathf.Clamp(d / 12f, 1f, 3.5f);
                    g.tr.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, g.angulo);
                }
                g.tr.position = g.pos;
                g.tr.localScale = Vector3.one * (basePx * pulso * k);

                var c = g.color; c.a = 0.95f * g.alfa;
                g.mr.GetPropertyBlock(bloque);
                bloque.SetColor(BaseColorId, c);
                bloque.SetColor(ColorId, c);
                g.mr.SetPropertyBlock(bloque);
            }
        }

        // Sobre el objetivo: un soldado (caido, herido) lleva el engranaje a 2.6 m del piso; un panel o un muro, sobre su borde alto.
        static Vector3 AnclaDelObjetivo(in AccionesEnCurso.Accion a)
        {
            var p = a.Punto;
            var t = a.Objetivo;
            if (t != null && t.GetComponentInParent<Soldier>() != null) return t.position + Vector3.up * AlturaObjetivoSoldado;
            float tope = p.y + 2.6f;
            if (t != null)
            {
                var rs = t.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    tope = Mathf.Clamp(Mathf.Max(b.max.y + 0.6f, p.y + 2.2f), p.y + 2.2f, p.y + 3.4f);
                    return new Vector3(p.x, tope, p.z);
                }
            }
            return new Vector3(p.x, tope, p.z);
        }
    }
}
