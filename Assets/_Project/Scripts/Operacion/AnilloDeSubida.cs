using UnityEngine;
using SP.Actors;
using SP.Player;
using SP.Presentation;

namespace SP.Operacion
{
    // Bug #111 + Ronda 17 (plan 2.11): el anillo del piso que marca "aca se sube". Hoy son TRES aros bajo el objetivo (el tanque en HUIR/Abordaje, o la
    // plaza de la base en Resistir). Sigue siendo un singleton gobernado por OperacionDirector (TickAnilloDeSubida / TickCirculoDeBase):
    //   - ARO DE USO (fijo): su radio es EXACTAMENTE el radio con el que el jugador puede interactuar (PlayerInputDriver.RadioDeInteraccion, el
    //     interactRadius serializado de la instancia; en la plaza, el que se pasa a la sobrecarga). Blanco/ambar mientras el jugador esta afuera,
    //     VERDE (fijo) cuando esta adentro, con el cartel "[E] SUBIR AL TANQUE".
    //   - AROS DE LLAMADA (los 2 que latian): se CIERRAN hacia el aro de uso a medida que el jugador se acerca (de 25 m a 4 m la escala base va
    //     de 1,6 a 1,0 y la frecuencia sube de 1,5 a 4 Hz). Adentro del radio se apagan.
    //   - Al subir (Ocultar con el jugador adentro): destello verde de 0,25 s y el sonido de cambio de asiento, y recien ahi se apaga.
    // La API anterior (Mostrar/Ocultar/EstaActivo/Instancia/EscalaActual) se mantiene tal cual.
    public class AnilloDeSubida : MonoBehaviour
    {
        public const float FrecuenciaHz = 3f, EscalaMin = 0.8f, EscalaMax = 1.4f, GiroGradosPorSeg = 140f;   // legado (los usan checks viejos)
        public const float DistanciaLejos = 25f, DistanciaCerca = 4f, EscalaLejos = 1.6f, EscalaCerca = 1.0f, HzLejos = 1.5f, HzCerca = 4f;
        public const float DuracionDestello = 0.25f, AmplitudDelLatido = 0.23f;
        public const string CartelDelTanque = "[E] SUBIR AL TANQUE";

        static AnilloDeSubida instancia;
        public static AnilloDeSubida Instancia => instancia;
        public static bool EstaActivo => instancia != null && instancia.gameObject.activeSelf;

        Transform a, b, uso, cartelT;
        MeshRenderer ra, rb, ru;
        TextMesh cartel;
        MaterialPropertyBlock bloque;
        Transform sigue;
        float radioBase = 3.4f, radioUso = 3.5f, t, fase, destello;
        bool usoAutomatico = true;

        public float EscalaActual { get; private set; }
        // Aro de uso (R17 plan 2.11)
        public float RadioDeUso => radioUso;
        public bool DentroDelRadio { get; private set; }
        public float DistanciaAlJugador { get; private set; }
        public Color ColorDeUso { get; private set; }
        public bool LlamadaActiva => a != null && a.gameObject.activeSelf;
        public bool EnDestello => destello > 0f;
        public string TextoDelCartel => cartel != null ? cartel.text : "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { instancia = null; }

        // Muestra el anillo en el piso bajo 'objetivo' (sigue su posicion mientras exista). El radio de uso es el de interaccion del jugador.
        public static AnilloDeSubida Mostrar(Transform objetivo, float radio = 3.4f) => Mostrar(objetivo, radio, 0f, CartelDelTanque);

        // Con radio de uso propio (la plaza de la base usa RadioDeInteraccionDeBase). Sin cartel de 3D: el HUD ya dice que hacer en la plaza.
        public static AnilloDeSubida Mostrar(Transform objetivo, float radio, float radioDeUso) => Mostrar(objetivo, radio, radioDeUso, "");

        static AnilloDeSubida Mostrar(Transform objetivo, float radio, float radioDeUso, string cartel)
        {
            if (!Application.isPlaying || objetivo == null) return null;
            if (instancia == null) instancia = Crear();
            var i = instancia;
            i.sigue = objetivo; i.radioBase = radio;
            i.usoAutomatico = radioDeUso <= 0f;
            i.radioUso = i.usoAutomatico ? RadioDeInteraccionDelJugador() : radioDeUso;
            if (i.cartel != null) i.cartel.text = cartel ?? "";
            i.destello = 0f;
            if (!i.gameObject.activeSelf) { i.t = 0f; i.fase = 0f; i.gameObject.SetActive(true); }
            i.Ubicar();
            i.Pintar(0f);
            return i;
        }

        public static void Ocultar()
        {
            var i = instancia;
            if (i == null || !i.gameObject.activeSelf) return;
            if (i.destello > 0f) return;   // ya esta en el destello de salida
            if (i.DentroDelRadio && Application.isPlaying)
            {
                // subio: destello verde de 0,25 s + sonido de asiento, y despues se apaga
                i.destello = DuracionDestello;
                AudioDirector.PlayAt(SfxKind.SeatChange, i.transform.position, 0.4f);
                return;
            }
            i.gameObject.SetActive(false);
        }

        static float RadioDeInteraccionDelJugador()
        {
            var drv = PlayerInputDriver.Activo;
            return drv != null ? drv.RadioDeInteraccion : 3.5f;
        }

        static AnilloDeSubida Crear()
        {
            var go = new GameObject("AnilloDeSubida") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            var r = go.AddComponent<AnilloDeSubida>();
            r.bloque = new MaterialPropertyBlock();
            r.a = NuevoAro(go.transform, "AroA", out r.ra);
            r.b = NuevoAro(go.transform, "AroB", out r.rb);
            r.uso = NuevoAro(go.transform, "AroDeUso", out r.ru);
            r.a.localPosition = Vector3.zero; r.b.localPosition = new Vector3(0f, 0.01f, 0f); r.uso.localPosition = new Vector3(0f, 0.02f, 0f);
            r.cartelT = NuevoCartel(go.transform, out r.cartel);
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

        // Cartel 3D "[E] SUBIR AL TANQUE": un TextMesh que mira a la camara, solo visible con el jugador adentro del radio.
        static Transform NuevoCartel(Transform padre, out TextMesh tm)
        {
            var q = new GameObject("Cartel");
            q.transform.SetParent(padre, false);
            q.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            tm = q.AddComponent<TextMesh>();
            var fuente = FuentesBelicas.Para(0);
            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = fuente; tm.text = ""; tm.fontSize = 64; tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.fontStyle = FontStyle.Bold;
            tm.color = new Color(0.55f, 1f, 0.65f);
            var mr = q.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var otm = FindFirstObjectByType<OperacionTextoMundo>();
                if (otm != null && otm.material != null) mr.sharedMaterial = otm.material;
            }
            q.SetActive(false);
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
            float dt = Time.unscaledDeltaTime;
            t += dt;
            Ubicar();
            Pintar(dt);
        }

        static Soldier JugadorActual()
        {
            var drv = PlayerInputDriver.Activo;
            return drv != null && drv.Brain != null ? drv.Brain.Current : null;
        }

        void Pintar(float dt)
        {
            if (usoAutomatico) radioUso = RadioDeInteraccionDelJugador();

            // distancia del jugador al objetivo: la misma medida (3D, desde el pivote) con la que el controlador decide si puede subir
            var yo = JugadorActual();
            float dist = yo != null && sigue != null ? Vector3.Distance(yo.transform.position, sigue.position) : 999f;
            DistanciaAlJugador = dist;
            bool dentro = dist <= radioUso;
            DentroDelRadio = dentro;

            // aros de llamada: se cierran sobre el aro de uso al acercarse
            float k = Mathf.Clamp01((dist - DistanciaCerca) / (DistanciaLejos - DistanciaCerca));
            float cierre = Mathf.Pow(k, 0.35f);   // se cierra rapido al principio y se asienta al llegar
            float baseEsc = Mathf.Lerp(EscalaCerca, EscalaLejos, cierre);
            float hz = Mathf.Lerp(HzCerca, HzLejos, k);
            fase += dt * hz;
            float s = Mathf.Sin(fase * Mathf.PI * 2f);
            float escA = baseEsc + AmplitudDelLatido * s;
            float escB = (baseEsc - AmplitudDelLatido * s) * 0.9f;
            EscalaActual = escA;
            float diametro = radioUso * 2f;
            bool llamada = !dentro && destello <= 0f;
            if (a.gameObject.activeSelf != llamada) { a.gameObject.SetActive(llamada); b.gameObject.SetActive(llamada); }
            if (llamada)
            {
                a.localScale = new Vector3(diametro * escA, 1f, diametro * escA);
                b.localScale = new Vector3(diametro * escB, 1f, diametro * escB);
                a.localRotation = Quaternion.Euler(0f, t * GiroGradosPorSeg, 0f);
                b.localRotation = Quaternion.Euler(0f, -t * GiroGradosPorSeg * 1.4f, 0f);
                float k1 = 0.5f + 0.5f * s, k2 = 1f - k1;
                var amarillo = new Color(1f, 0.92f, 0.1f, Mathf.Lerp(0.7f, 1f, k1));
                var naranja = new Color(1f, 0.45f, 0.05f, Mathf.Lerp(0.7f, 1f, k2));
                bloque.SetColor("_BaseColor", amarillo); bloque.SetColor("_Color", amarillo); ra.SetPropertyBlock(bloque);
                bloque.SetColor("_BaseColor", naranja); bloque.SetColor("_Color", naranja); rb.SetPropertyBlock(bloque);
            }

            // aro de uso: fijo, del tamano exacto del radio de interaccion
            Color cu; float escUso = 1f;
            if (destello > 0f)
            {
                destello -= dt;
                float p = Mathf.Clamp01(1f - destello / DuracionDestello);   // 0 -> 1 durante el destello
                escUso = 1f + 0.35f * p;
                cu = new Color(0.45f, 1f, 0.55f, 1f - p * 0.85f);
                if (destello <= 0f) { destello = 0f; gameObject.SetActive(false); return; }
            }
            else if (dentro) cu = new Color(0.2f, 1f, 0.35f, 1f);
            else cu = Color.Lerp(Color.white, new Color(1f, 0.72f, 0.12f, 1f), 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f));
            ColorDeUso = cu;
            uso.localScale = new Vector3(diametro * escUso, 1f, diametro * escUso);
            bloque.SetColor("_BaseColor", cu); bloque.SetColor("_Color", cu); ru.SetPropertyBlock(bloque);

            // cartel 3D: solo adentro del radio y si hay texto
            bool verCartel = dentro && destello <= 0f && cartel != null && !string.IsNullOrEmpty(cartel.text);
            if (cartelT != null)
            {
                if (cartelT.gameObject.activeSelf != verCartel) cartelT.gameObject.SetActive(verCartel);
                if (verCartel)
                {
                    var cam = SP.Core.CamaraPrincipal.Actual;
                    if (cam != null) cartelT.rotation = Quaternion.LookRotation(cartelT.position - cam.transform.position);
                }
            }
        }
    }
}
