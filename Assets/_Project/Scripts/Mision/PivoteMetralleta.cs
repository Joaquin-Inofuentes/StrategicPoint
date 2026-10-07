using UnityEngine;
using SP.Presentation;

namespace SP.Mision
{
    // Bug #076: la ametralladora del helicoptero ya no es decorativa ni fija. Hay una por lado (puerta izquierda y derecha), cada
    // una montada en un pivote que gira en yaw hacia el blanco (+-70 grados desde su lado) y cabecea en pitch; el artillero
    // (clon visual de un soldado aliado, ver TripulacionDelHeli) vive dentro del pivote y gira con el arma como en una plataforma.
    // El disparo sale de la boca real (Boca). El yaw se mide en el espacio del helicoptero: 0 = proa, +90 = derecha, -90 = izquierda.
    public class PivoteMetralleta : MonoBehaviour
    {
        public const float ArcoMaximo = 70f;           // grados a cada lado del eje lateral
        public const float PitchAbajo = 55f, PitchArriba = 20f;
        public const float VelocidadDeGiro = 170f;     // grados/s
        public const float AlturaDelArma = 1.2f;       // del pivote (piso) al eje del arma

        public int lado = 1;                           // +1 derecha, -1 izquierda
        public Transform arma;                         // nodo que cabecea
        public Transform boca;                         // punta del canon

        float yaw, pitch;
        bool iniciado;

        public Transform Boca => boca != null ? boca : transform;
        public float YawLocal { get { Iniciar(); return yaw; } }
        public float PitchLocal { get { Iniciar(); return pitch; } }
        public float YawDeReposo => 90f * (lado >= 0 ? 1 : -1);
        public Vector3 CentroDeGiro => transform.position + transform.up * AlturaDelArma;

        void Iniciar()
        {
            if (iniciado) return;
            iniciado = true;
            yaw = YawDeReposo;
            Aplicar();
        }

        void Awake() => Iniciar();

        // Rumbo (en el espacio del helicoptero) hacia un punto del mundo, y distancia plana.
        public float RumboHacia(Vector3 puntoMundo, out float cabeceo)
        {
            var padre = transform.parent;
            var local = padre != null ? padre.InverseTransformPoint(puntoMundo) : puntoMundo;
            var d = local - (transform.localPosition + Vector3.up * AlturaDelArma);
            float hor = new Vector2(d.x, d.z).magnitude;
            cabeceo = Mathf.Atan2(-d.y, Mathf.Max(0.01f, hor)) * Mathf.Rad2Deg;   // + = apunta hacia abajo
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        public bool EnArco(float rumbo, float tolerancia = 0f)
            => Mathf.Abs(Mathf.DeltaAngle(YawDeReposo, rumbo)) <= ArcoMaximo + tolerancia;

        // Gira hacia el punto (limitado al arco). Devuelve el error angular que queda en yaw.
        public float Apuntar(Vector3 puntoMundo, float dt)
        {
            Iniciar();
            float rumbo = RumboHacia(puntoMundo, out float cab);
            float objetivo = YawDeReposo + Mathf.Clamp(Mathf.DeltaAngle(YawDeReposo, rumbo), -ArcoMaximo, ArcoMaximo);
            yaw = Mathf.MoveTowardsAngle(yaw, objetivo, VelocidadDeGiro * dt);
            pitch = Mathf.MoveTowards(pitch, Mathf.Clamp(cab, -PitchArriba, PitchAbajo), VelocidadDeGiro * dt);
            Aplicar();
            return Mathf.Abs(Mathf.DeltaAngle(yaw, rumbo));
        }

        // Sin blanco: vuelve despacio al eje lateral.
        public void Reposo(float dt)
        {
            Iniciar();
            yaw = Mathf.MoveTowardsAngle(yaw, YawDeReposo, 60f * dt);
            pitch = Mathf.MoveTowards(pitch, 8f, 40f * dt);
            Aplicar();
        }

        void Aplicar()
        {
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (arma != null) arma.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    // Casquillos de las ametralladoras del helicoptero: pool fijo de 32 cubitos de laton con balistica propia (sin Rigidbody).
    public class CasquillosDeMetralleta : MonoBehaviour
    {
        public const int Cupo = 32;
        const float Vida = 1.0f;
        // Bug #113: los casquillos se veian como bastones negros "flotando" (laton Lit sin emision: de noche, negro; ademas largos y de vida
        // larga). Ahora son mas chicos, con emision calida de laton, viven 1 s y solo se expulsan si la camara esta cerca del arma.
        public const float DistanciaMaximaDeExpulsion = 40f;
        public static readonly Vector3 Tamano = new Vector3(0.025f, 0.025f, 0.07f);
        public static Material MaterialDeLaton => material;
        public static Color ColorDeEmision => new Color(0.55f, 0.36f, 0.08f);
        static CasquillosDeMetralleta instancia;
        public static float PisoY = float.NegativeInfinity;      // lo fija el helicoptero (rayo al piso)
        public static int Expulsados { get; private set; }
        public static int Activos { get { var i = instancia; if (i == null) return 0; int n = 0; for (int k = 0; k < i.activo.Length; k++) if (i.activo[k]) n++; return n; } }

        Transform[] piezas;
        Vector3[] vel;
        float[] edad;
        bool[] activo;
        int cursor;
        static Material material;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { instancia = null; PisoY = float.NegativeInfinity; Expulsados = 0; material = null; }

        static CasquillosDeMetralleta Obtener()
        {
            if (instancia != null) return instancia;
            var go = new GameObject("CasquillosDeMetralleta") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            instancia = go.AddComponent<CasquillosDeMetralleta>();
            instancia.Armar();
            return instancia;
        }

        void Armar()
        {
            if (material == null)
            {
                material = SafeMaterial.Create(new Color(0.85f, 0.65f, 0.2f));
                if (material.HasProperty("_EmissionColor")) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", ColorDeEmision); material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            }
            piezas = new Transform[Cupo]; vel = new Vector3[Cupo]; edad = new float[Cupo]; activo = new bool[Cupo];
            for (int i = 0; i < Cupo; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = "Casquillo";
                Destroy(p.GetComponent<Collider>());
                p.transform.SetParent(transform, false);
                p.transform.localScale = Tamano;
                var r = p.GetComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                p.SetActive(false);
                piezas[i] = p.transform;
            }
        }

        // Expulsa un casquillo desde pos hacia afuera (lado = direccion lateral del arma).
        public static void Expulsar(Vector3 pos, Vector3 lado)
        {
            if (!Application.isPlaying) return;
            var cam = SP.Core.CamaraPrincipal.Actual;
            if (cam != null && (cam.transform.position - pos).sqrMagnitude > DistanciaMaximaDeExpulsion * DistanciaMaximaDeExpulsion) return;   // #113: lejos no se ven y solo ensucian
            var c = Obtener();
            int i = c.cursor; c.cursor = (c.cursor + 1) % Cupo;
            c.piezas[i].position = pos;
            c.piezas[i].rotation = Random.rotation;
            c.vel[i] = lado.normalized * Random.Range(1.6f, 3.0f) + Vector3.up * Random.Range(1.2f, 2.4f) + Random.insideUnitSphere * 0.35f;
            c.edad[i] = 0f; c.activo[i] = true;
            c.piezas[i].gameObject.SetActive(true);
            Expulsados++;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < Cupo; i++)
            {
                if (!activo[i]) continue;
                edad[i] += dt;
                if (edad[i] >= Vida) { activo[i] = false; piezas[i].gameObject.SetActive(false); continue; }
                vel[i] += Vector3.down * 9.8f * dt;
                var p = piezas[i].position + vel[i] * dt;
                if (p.y < PisoY + 0.02f && vel[i].y < 0f)
                {
                    p.y = PisoY + 0.02f;
                    vel[i] = new Vector3(vel[i].x * 0.4f, -vel[i].y * 0.3f, vel[i].z * 0.4f);
                }
                piezas[i].position = p;
                piezas[i].Rotate(vel[i].magnitude * 120f * dt, 0f, 0f, Space.Self);
            }
        }
    }
}
