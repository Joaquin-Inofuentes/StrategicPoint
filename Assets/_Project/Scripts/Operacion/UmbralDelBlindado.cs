using UnityEngine;
using SP.Presentation;

namespace SP.Operacion
{
    // Ronda 19 (plan R19 v2, punto 8-A, tarea E3): la MARCA VISIBLE del umbral que dispara la pelea contra el BLINDADO.
    // Antes el umbral era un plano invisible en z = -30 pegado a la salida del Centro de Datos: para el jugador la toma "arrancaba sola".
    // Ahora la linea esta en OperacionDirector.ZParaLaToma (z = -8, a ~30 m del tanque) y se ve:
    //   - franja roja/negra en el piso de AnchoDeFranja x 0,03 x FondoDeFranja m, en Tramos tramos alternados;
    //   - 2 postes de AltoDePoste m en x = +-XDePostes con una luz roja emisiva arriba (sin Light real);
    //   - cartel "ZONA DEL BLINDADO · AL CRUZAR EMPIEZA EL COMBATE" (TextMesh, mismo material de texto que los carteles del nivel).
    // Se crea al caer el muro de la salida norte, late a 1 Hz y se apaga con un fundido de 0,5 s cuando arranca la toma (para no convivir con el
    // anillo verde del abordaje). Sin collider, sin Find (R1), un solo material compartido + MaterialPropertyBlock (R4), estaticos reiniciados (R2),
    // avance con dt (R3). Lo gobierna OperacionDirector (Jefe.cs): Asegurar / Apagar / Quitar.
    public class UmbralDelBlindado : MonoBehaviour
    {
        public const float AnchoDeFranja = 16f, AltoDeFranja = 0.03f, FondoDeFranja = 1.2f, AltoDePoste = 2.2f, XDePostes = 8f;
        public const int Tramos = 8;
        public const float HzDeLatido = 1f, SegundosDeFundido = 0.5f;
        public const string TextoDelCartel = "ZONA DEL BLINDADO · AL CRUZAR EMPIEZA EL COMBATE";
        static readonly Color Rojo = new Color(1f, 0.12f, 0.08f, 1f), Negro = new Color(0.05f, 0.05f, 0.06f, 1f), Poste = new Color(0.16f, 0.16f, 0.18f, 1f);

        static UmbralDelBlindado instancia;
        static Material matCompartido;
        static Mesh cubo;
        public static UmbralDelBlindado Instancia => instancia;
        public static int CreadasEnLaPartida { get; private set; }
        // Existe y no se esta apagando.
        public static bool Activa => instancia != null && instancia.gameObject.activeSelf && instancia.fundido < 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { instancia = null; matCompartido = null; cubo = null; CreadasEnLaPartida = 0; }

        // Pura, para la suite headless (H25.4): cruzo la linea si su z esta POR DELANTE (hacia el tanque, +Z) del umbral.
        public static bool Cruzo(float z, float umbral = OperacionDirector.ZParaLaToma) => z > umbral;

        MeshRenderer[] rojos, negros, postes, luces;
        TextMesh cartel;
        MaterialPropertyBlock bloque;
        float t, fundido = -1f;
        public float Z { get; private set; }
        public bool EnFundido => fundido >= 0f;
        public string TextoMostrado => cartel != null ? cartel.text : "";
        public int RenderersEncendidos
        {
            get
            {
                int n = 0;
                foreach (var r in GetComponentsInChildren<MeshRenderer>(false)) if (r != null && r.enabled) n++;
                return n;
            }
        }
        // Alfa actual de la marca (1 encendida, baja a 0 en el fundido).
        public float Alfa => fundido < 0f ? 1f : Mathf.Clamp01(1f - fundido / SegundosDeFundido);

        // Crea la marca si no existe (idempotente). x = centro de la franja (la salida norte esta en x = 0).
        public static UmbralDelBlindado Asegurar(float z, float x = 0f)
        {
            if (!Application.isPlaying) return null;
            if (instancia != null)
            {
                if (!instancia.gameObject.activeSelf || instancia.fundido >= 0f) { instancia.fundido = -1f; instancia.t = 0f; instancia.gameObject.SetActive(true); }
                if (Mathf.Abs(instancia.Z - z) > 0.01f) instancia.Ubicar(x, z);
                return instancia;
            }
            instancia = Crear(x, z);
            CreadasEnLaPartida++;
            return instancia;
        }

        // Fundido de 0,5 s y despues se desactiva (arranco la toma).
        public static void Apagar()
        {
            var i = instancia;
            if (i == null || !i.gameObject.activeSelf || i.fundido >= 0f) return;
            i.fundido = 0f;
        }

        // Sin fundido (reinicio de la huida, saltos de prueba).
        public static void Quitar()
        {
            if (instancia == null) return;
            Destroy(instancia.gameObject);
            instancia = null;
        }

        static Material Material()
        {
            if (matCompartido == null)
            {
                matCompartido = ParticleMaterialFactory.CreateTransparent(Color.white);
                if (matCompartido.HasProperty("_Cull")) matCompartido.SetInt("_Cull", 0);
                matCompartido.name = "MAT_R19_UmbralDelBlindado";
            }
            return matCompartido;
        }

        static Mesh Cubo()
        {
            if (cubo != null) return cubo;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubo = tmp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(tmp);   // ni un cuadro con su BoxCollider en el origen
            return cubo;
        }

        static UmbralDelBlindado Crear(float x, float z)
        {
            var go = new GameObject("R19_UmbralDelBlindado") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            var u = go.AddComponent<UmbralDelBlindado>();
            u.bloque = new MaterialPropertyBlock();
            var raiz = go.transform;
            float ancho = AnchoDeFranja / Tramos;
            int nr = (Tramos + 1) / 2, nn = Tramos / 2;
            u.rojos = new MeshRenderer[nr]; u.negros = new MeshRenderer[nn];
            int ir = 0, inn = 0;
            for (int k = 0; k < Tramos; k++)
            {
                float lx = -AnchoDeFranja * 0.5f + ancho * (k + 0.5f);
                var r = Pieza(raiz, "R19_Umbral_Tramo_" + k, new Vector3(lx, AltoDeFranja * 0.5f + 0.02f, 0f), new Vector3(ancho, AltoDeFranja, FondoDeFranja));
                if (k % 2 == 0) u.rojos[ir++] = r; else u.negros[inn++] = r;
            }
            u.postes = new MeshRenderer[2]; u.luces = new MeshRenderer[2];
            for (int s = 0; s < 2; s++)
            {
                float px = s == 0 ? -XDePostes : XDePostes;
                u.postes[s] = Pieza(raiz, "R19_Umbral_Poste_" + s, new Vector3(px, AltoDePoste * 0.5f, 0f), new Vector3(0.16f, AltoDePoste, 0.16f));
                u.luces[s] = Pieza(raiz, "R19_Umbral_Luz_" + s, new Vector3(px, AltoDePoste + 0.12f, 0f), new Vector3(0.28f, 0.24f, 0.28f));
            }
            u.cartel = NuevoCartel(raiz);
            u.Ubicar(x, z);
            u.Pintar();
            return u;
        }

        static MeshRenderer Pieza(Transform padre, string nombre, Vector3 pos, Vector3 escala)
        {
            var q = new GameObject(nombre);
            q.transform.SetParent(padre, false);
            q.transform.localPosition = pos; q.transform.localScale = escala;
            q.AddComponent<MeshFilter>().sharedMesh = Cubo();
            var r = q.AddComponent<MeshRenderer>();
            r.sharedMaterial = Material();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        static TextMesh NuevoCartel(Transform padre)
        {
            var q = new GameObject("R19_Umbral_Cartel");
            q.transform.SetParent(padre, false);
            q.transform.localPosition = new Vector3(0f, AltoDePoste + 0.05f, 0f);   // entre los postes; legible desde el sur (de donde viene el jugador)
            var tm = q.AddComponent<TextMesh>();
            var fuente = FuentesBelicas.Para(0);
            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = fuente; tm.text = TextoDelCartel; tm.fontSize = 64; tm.characterSize = 0.045f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.fontStyle = FontStyle.Bold;
            tm.color = new Color(1f, 0.35f, 0.28f);
            var mr = q.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var m = AnilloDeSubida.MaterialDeCartel();   // el material de texto con profundidad (OperacionTextoMundo); se busca una sola vez
                if (m != null) mr.sharedMaterial = m;
            }
            return tm;
        }

        void Ubicar(float x, float z)
        {
            Z = z;
            var p = new Vector3(x, 0f, z);
            transform.position = new Vector3(x, AnilloDeAviso.AlturaDelPisoEn(p), z);
        }

        void Update() => Tick(Time.unscaledDeltaTime);

        // R3: avance con dt explicito.
        public void Tick(float dt)
        {
            t += dt;
            if (fundido >= 0f)
            {
                fundido += dt;
                if (fundido >= SegundosDeFundido) { fundido = -1f; gameObject.SetActive(false); return; }
            }
            Pintar();
        }

        void Pintar()
        {
            if (bloque == null) bloque = new MaterialPropertyBlock();
            float a = Alfa;
            float lat = 0.5f + 0.5f * Mathf.Sin(t * HzDeLatido * Mathf.PI * 2f);   // 0..1 a 1 Hz
            var rojo = Color.Lerp(Rojo * 0.55f, Rojo, lat); rojo.a = a;
            var negro = Negro; negro.a = a * 0.92f;
            var poste = Poste; poste.a = a;
            var luz = Color.Lerp(new Color(0.45f, 0.04f, 0.03f), new Color(1f, 0.25f, 0.18f), lat); luz.a = a;
            Colorear(rojos, rojo); Colorear(negros, negro); Colorear(postes, poste); Colorear(luces, luz);
            if (cartel != null && Mathf.Abs(cartel.color.a - a) > 0.001f) { var c = cartel.color; c.a = a; cartel.color = c; }   // solo en el fundido (no rearmar la malla cada cuadro)
        }

        void Colorear(MeshRenderer[] rs, Color c)
        {
            if (rs == null) return;
            bloque.SetColor("_BaseColor", c); bloque.SetColor("_Color", c);
            for (int i = 0; i < rs.Length; i++) if (rs[i] != null) rs[i].SetPropertyBlock(bloque);
        }

        void OnDestroy() { if (instancia == this) instancia = null; }
    }
}
