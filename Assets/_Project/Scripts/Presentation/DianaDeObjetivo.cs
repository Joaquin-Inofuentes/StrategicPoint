using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;

namespace SP.Presentation
{
    // Bug #096: "una diana sobre el enemigo al que mandas atacar". Marca de blanco sobre la cabeza del enemigo elegido: dos
    // anillos rojos concentricos que giran en sentidos opuestos, una cruz central y un pulso de escala. Mira siempre a la camara
    // y crece con la distancia para que se lea desde lejos.
    //
    // Vive mientras algun aliado tenga a ese enemigo como blanco en Attack/Chase/MovingToAttackOrder (el mismo criterio que
    // AttackLineManager), hasta que muera, o 10 s como maximo. Pool de 4, 100% por codigo (sin prefab).
    public class DianaDeObjetivo : MonoBehaviour
    {
        public const int Cupo = 4;
        public const float AlturaSobreElPivote = 2.3f;
        public const float VidaMaxima = 10f;
        const float GraciaInicial = 0.6f;        // la orden tarda un frame en llegar a los cerebros
        const float GradosPorSegundo = 90f;
        static readonly Color Rojo = new Color(0.95f, 0.16f, 0.12f, 1f);

        static readonly List<DianaDeObjetivo> pool = new List<DianaDeObjetivo>();

        Soldier enemigo;
        float edad, proximoChequeo;
        bool hayAtacantes;
        Transform anilloExterior, anilloInterior;
        LineRenderer[] lineas;
        Camera camara;
        public Soldier Enemigo => enemigo;
        public float Edad => edad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() => pool.Clear();

        // Pone (o refresca) la diana sobre este enemigo. Devuelve la diana, o null si no se puede.
        public static DianaDeObjetivo Mostrar(Soldier enemigo)
        {
            if (!Application.isPlaying || enemigo == null || enemigo.Health == null || !enemigo.Health.IsAlive) return null;
            pool.RemoveAll(d => d == null);
            DianaDeObjetivo libre = null, masVieja = null;
            foreach (var d in pool)
            {
                if (d.enemigo == enemigo && d.gameObject.activeSelf) { d.Reiniciar(enemigo); return d; }
                if (!d.gameObject.activeSelf) { if (libre == null) libre = d; }
                else if (masVieja == null || d.edad > masVieja.edad) masVieja = d;
            }
            var elegida = libre;
            if (elegida == null && pool.Count < Cupo) { elegida = Construir(); pool.Add(elegida); }
            if (elegida == null) elegida = masVieja;   // cupo lleno: se recicla la mas vieja
            if (elegida == null) return null;
            elegida.Reiniciar(enemigo);
            return elegida;
        }

        // Dianas vivas ahora mismo (para checks y para el limite).
        public static int Activas { get { pool.RemoveAll(d => d == null); int n = 0; foreach (var d in pool) if (d.gameObject.activeSelf) n++; return n; } }

        public static DianaDeObjetivo SobreElEnemigo(Soldier s)
        {
            foreach (var d in pool) if (d != null && d.gameObject.activeSelf && d.enemigo == s) return d;
            return null;
        }

        static DianaDeObjetivo Construir()
        {
            var go = new GameObject("DianaDeObjetivo");
            var d = go.AddComponent<DianaDeObjetivo>();
            var lista = new List<LineRenderer>();
            d.anilloExterior = Anillo(go.transform, "Exterior", 0.85f, 0.11f, lista);
            d.anilloInterior = Anillo(go.transform, "Interior", 0.55f, 0.09f, lista);
            // Cruz central: dos trazos que se cortan en el centro, con un hueco para que el blanco se vea.
            Trazo(go.transform, "CruzH", new Vector3(-0.32f, 0f, 0f), new Vector3(0.32f, 0f, 0f), 0.08f, lista);
            Trazo(go.transform, "CruzV", new Vector3(0f, -0.32f, 0f), new Vector3(0f, 0.32f, 0f), 0.08f, lista);
            d.lineas = lista.ToArray();
            go.SetActive(false);
            return d;
        }

        static Transform Anillo(Transform padre, string nombre, float radio, float ancho, List<LineRenderer> lista)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            var lr = go.AddComponent<LineRenderer>();
            int n = 40;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                // Ranuras: cada octavo del anillo se achica un poco para que el giro se note (un circulo liso girando no se ve).
                float r = radio * (i % 10 < 2 ? 0.86f : 1f);
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
            Estilo(lr, ancho);
            lista.Add(lr);
            return go.transform;
        }

        static void Trazo(Transform padre, string nombre, Vector3 a, Vector3 b, float ancho, List<LineRenderer> lista)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.SetPosition(0, a); lr.SetPosition(1, b);
            Estilo(lr, ancho);
            lista.Add(lr);
        }

        static void Estilo(LineRenderer lr, float ancho)
        {
            lr.widthMultiplier = ancho;
            lr.material = SafeMaterial.CreateLinea(Rojo);
            lr.startColor = lr.endColor = Rojo;
            lr.alignment = LineAlignment.TransformZ;   // el plano de la linea es el de la diana, que mira a la camara
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
        }

        void Reiniciar(Soldier e)
        {
            enemigo = e; edad = 0f; proximoChequeo = GraciaInicial; hayAtacantes = true;
            gameObject.SetActive(true);
            Seguir();
        }

        void Seguir()
        {
            if (enemigo == null) return;
            if (camara == null) camara = CamaraPrincipal.Actual;   // WP11: la suite prohibe Camera.main en runtime
            var pos = enemigo.transform.position + Vector3.up * AlturaSobreElPivote;
            transform.position = pos;
            float k = 1f;
            if (camara != null)
            {
                transform.rotation = Quaternion.LookRotation(camara.transform.forward, camara.transform.up);
                // Crece con la distancia para que se vea de lejos (hasta x3).
                k = Mathf.Clamp(Vector3.Distance(camara.transform.position, pos) / 14f, 1f, 3f);
            }
            float pulso = 1f + 0.15f * (0.5f + 0.5f * Mathf.Sin(edad * 7f));
            transform.localScale = Vector3.one * (k * pulso);
        }

        void Update()
        {
            edad += Time.unscaledDeltaTime;
            if (enemigo == null || enemigo.Health == null || !enemigo.Health.IsAlive || !enemigo.gameObject.activeInHierarchy || edad >= VidaMaxima)
            { Apagar(); return; }
            float giro = GradosPorSegundo * Time.unscaledDeltaTime;
            if (anilloExterior != null) anilloExterior.Rotate(0f, 0f, giro, Space.Self);
            if (anilloInterior != null) anilloInterior.Rotate(0f, 0f, -giro, Space.Self);

            if (edad >= proximoChequeo)
            {
                proximoChequeo = edad + 0.25f;
                hayAtacantes = AlguienLoAtaca(enemigo);
                if (!hayAtacantes) { Apagar(); return; }
            }
            Seguir();
        }

        void LateUpdate() { if (enemigo != null) Seguir(); }

        // El mismo criterio que AttackLineManager: blanco fijado en Attack / Chase / MovingToAttackOrder.
        public static bool AlguienLoAtaca(Soldier objetivo)
        {
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Player || !s.gameObject.activeInHierarchy) continue;
                var b = s.Brain;
                if (b == null || b.CurrentTarget != objetivo) continue;
                if (b.State == AiState.Attack || b.State == AiState.Chase || b.State == AiState.MovingToAttackOrder) return true;
            }
            return false;
        }

        void Apagar()
        {
            enemigo = null;
            gameObject.SetActive(false);
        }
    }
}
