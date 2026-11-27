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
    // La API anterior (Mostrar/Ocultar/EstaActivo/Instancia/EscalaActual) se mantiene: Mostrar(t) / Mostrar(t, r) / Mostrar(t, r, radioDeUso) y Ocultar().
    //
    // Ronda 19 (plan R19 v2, punto 7, tarea E1):
    //   - API nueva Mostrar(objetivo, radioDeUso, cartel): el que llama dice el radio REAL con el que se puede subir y el cartel (o "" sin cartel).
    //     Con radioDeUso <= 0 usa el radio de interaccion del jugador (como antes).
    //   - La distancia al jugador se mide EN PLANTA (XZ), igual que OperacionDirector.TickAntesDelTanque.
    //   - Ocultar(subio): la confirmacion ("onda" + sonido de asiento) SOLO con subio == true. Ocultar() / Ocultar(false) apaga en el acto, sin
    //     sonido (antes destellaba ante cualquier apagado con el jugador adentro, p. ej. un cambio de fase).
    //   - La onda: 3 aros concentricos que se abren de 1,0 a 1,6 / 1,9 / 2,2 veces el radio en DuracionDeOnda (0,6 s), desfasados 0,1 s, de verde a
    //     transparente. Reusa los 3 aros que ya existen (no crea objetos).
    //   - Con radio de uso >= RadioGrande (5 m) la escala lejana de los aros de llamada es 1,3 en vez de 1,6 (no cubren medio patio).
    public class AnilloDeSubida : MonoBehaviour
    {
        public const float FrecuenciaHz = 3f, EscalaMin = 0.8f, EscalaMax = 1.4f, GiroGradosPorSeg = 140f;   // legado (los usan checks viejos)
        public const float DistanciaLejos = 25f, DistanciaCerca = 4f, EscalaLejos = 1.6f, EscalaCerca = 1.0f, HzLejos = 1.5f, HzCerca = 4f;
        public const float DuracionDestello = 0.25f, AmplitudDelLatido = 0.23f;   // DuracionDestello: legado (la onda la reemplaza)
        public const string CartelDelTanque = "[E] SUBIR AL TANQUE";
        // R19
        public const float RadioGrande = 5f, EscalaLejosRadioGrande = 1.3f;
        public const float DuracionDeOnda = 0.6f, DesfaseDeOnda = 0.1f;
        public static readonly float[] EscalasFinalesDeOnda = { 1.6f, 1.9f, 2.2f };

        static AnilloDeSubida instancia;
        public static AnilloDeSubida Instancia => instancia;
        public static bool EstaActivo => instancia != null && instancia.gameObject.activeSelf;
        // R19: contadores para los checks (se reinician con el dominio)
        public static int OndasLanzadas { get; private set; }
        public static int OcultadosSinOnda { get; private set; }

        Transform a, b, uso, cartelT;
        MeshRenderer ra, rb, ru;
        TextMesh cartel;
        MaterialPropertyBlock bloque;
        Transform sigue;
        float radioBase = 3.4f, radioUso = 3.5f, t, fase, onda = -1f;
        bool usoAutomatico = true;

        public float EscalaActual { get; private set; }
        // Aro de uso (R17 plan 2.11)
        public float RadioDeUso => radioUso;
        public bool DentroDelRadio { get; private set; }
        public float DistanciaAlJugador { get; private set; }   // R19: en planta
        public Color ColorDeUso { get; private set; }
        public bool LlamadaActiva => a != null && a.gameObject.activeSelf && onda < 0f;
        public bool EnDestello => onda >= 0f;   // legado: ahora es "en la onda de subida"
        public bool EnOnda => onda >= 0f;
        public float SegundosDeOnda => onda < 0f ? 0f : onda;
        public Transform Objetivo => sigue;
        public float EscalaLejosEfectiva => radioUso >= RadioGrande ? EscalaLejosRadioGrande : EscalaLejos;
        public string TextoDelCartel => cartel != null ? cartel.text : "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { instancia = null; OndasLanzadas = 0; OcultadosSinOnda = 0; materialDeCartel = null; materialDeCartelBuscado = false; }

        // Muestra el anillo en el piso bajo 'objetivo' (sigue su posicion mientras exista). El radio de uso es el de interaccion del jugador.
        public static AnilloDeSubida Mostrar(Transform objetivo, float radio = 3.4f) => Mostrar(objetivo, radio, 0f, CartelDelTanque);

        // Con radio de uso propio (la plaza de la base usa RadioDeInteraccionDeBase). Sin cartel de 3D: el HUD ya dice que hacer en la plaza.
        public static AnilloDeSubida Mostrar(Transform objetivo, float radio, float radioDeUso) => Mostrar(objetivo, radio, radioDeUso, "");

        // R19 (E1): API nueva. radioDeUso = radio REAL de subida de la escena (<= 0: el de interaccion del jugador); cartel: texto 3D o "" sin cartel.
        // Idempotente: si ya muestra lo mismo no repinta (el arbitro del director la puede llamar cada cuadro).
        public static AnilloDeSubida Mostrar(Transform objetivo, float radioDeUso, string cartel) => Mostrar(objetivo, 3.4f, radioDeUso, cartel);

        static AnilloDeSubida Mostrar(Transform objetivo, float radio, float radioDeUso, string cartel)
        {
            if (!Application.isPlaying || objetivo == null) return null;
            if (instancia == null) instancia = Crear();
            var i = instancia;
            cartel = cartel ?? "";
            bool auto = radioDeUso <= 0f;
            if (i.gameObject.activeSelf && i.onda < 0f && i.sigue == objetivo && i.usoAutomatico == auto
                && (auto || Mathf.Abs(i.radioUso - radioDeUso) < 0.001f) && i.TextoDelCartel == cartel)
                return i;
            i.sigue = objetivo; i.radioBase = radio;
            i.usoAutomatico = auto;
            i.radioUso = auto ? RadioDeInteraccionDelJugador() : radioDeUso;
            if (i.cartel != null) i.cartel.text = cartel;
            if (i.onda >= 0f) i.TerminarOnda();
            if (!i.gameObject.activeSelf) { i.t = 0f; i.fase = 0f; i.gameObject.SetActive(true); }
            i.Ubicar();
            i.Pintar(0f);
            return i;
        }

        // Apaga en el acto, sin onda ni sonido (cambio de fase, fin de Resistir, etc.).
        public static void Ocultar() => Ocultar(false);

        // R19 (E1): subio == true -> onda de 3 aros + sonido de asiento y recien despues se apaga. subio == false -> apagado inmediato.
        public static void Ocultar(bool subio)
        {
            var i = instancia;
            if (i == null || !i.gameObject.activeSelf) return;
            if (i.onda >= 0f) return;   // ya esta en la onda de salida: que termine
            if (subio && Application.isPlaying)
            {
                i.onda = 0f;
                OndasLanzadas++;
                if (i.cartelT != null && i.cartelT.gameObject.activeSelf) i.cartelT.gameObject.SetActive(false);
                AudioDirector.PlayAt(SfxKind.SeatChange, i.transform.position, 0.4f);
                i.PintarOnda();
                return;
            }
            OcultadosSinOnda++;
            i.gameObject.SetActive(false);
        }

        void TerminarOnda()
        {
            onda = -1f;
            if (uso != null) uso.localRotation = Quaternion.identity;
        }

        // Escala (relativa al diametro de uso) y alfa del aro 'k' (0 = uso, 1 = A, 2 = B) a los 'e' segundos de onda. Pura, para la suite.
        public static void EstadoDeOnda(int k, float e, out float escala, out float alfa)
        {
            float ini = DesfaseDeOnda * k, dur = Mathf.Max(0.05f, DuracionDeOnda - ini);
            float p = Mathf.Clamp01((e - ini) / dur);
            escala = Mathf.Lerp(1f, EscalasFinalesDeOnda[Mathf.Clamp(k, 0, 2)], 1f - (1f - p) * (1f - p));   // sale rapido y se asienta
            alfa = e < ini ? 0f : 1f - p;
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
                var m = MaterialDeCartel();
                if (m != null) mr.sharedMaterial = m;
            }
            q.SetActive(false);
            return q.transform;
        }

        // Material de texto con prueba de profundidad (el de OperacionTextoMundo). R19: compartido con UmbralDelBlindado; la busqueda global se hace
        // UNA sola vez por partida (es la misma que ya estaba en la lista blanca de la suite para este archivo).
        static Material materialDeCartel; static bool materialDeCartelBuscado;
        internal static Material MaterialDeCartel()
        {
            if (materialDeCartelBuscado && materialDeCartel != null) return materialDeCartel;
            materialDeCartelBuscado = true;
            var otm = FindFirstObjectByType<OperacionTextoMundo>();
            materialDeCartel = otm != null ? otm.material : null;
            return materialDeCartel;
        }

        void Ubicar()
        {
            if (sigue == null) return;
            var p = sigue.position;
            transform.position = new Vector3(p.x, AnilloDeAviso.AlturaDelPisoEn(p) + 0.12f, p.z);
        }

        // R19: el dt del cuadro sale del reloj real (no de unscaledDeltaTime, que con cuadros largos -Editor sin foco, tirones al subir- queda por
        // debajo del tiempo real y la onda de 0,6 s llegaba a durar 1,7 s). Tick(dt) sigue siendo el punto de entrada para simular.
        float ultimoReal = -1f;
        void OnEnable() { ultimoReal = -1f; }
        void Update()
        {
            float ahora = Time.realtimeSinceStartup;
            float dt = ultimoReal < 0f ? Time.unscaledDeltaTime : Mathf.Clamp(ahora - ultimoReal, 0f, 0.5f);
            ultimoReal = ahora;
            Tick(dt);
        }

        // R19: avance con dt explicito (R3), para poder simularlo.
        public void Tick(float dt)
        {
            if (onda >= 0f)
            {
                onda += dt;
                if (onda >= DuracionDeOnda) { TerminarOnda(); gameObject.SetActive(false); return; }
                if (sigue != null) Ubicar();
                PintarOnda();
                return;
            }
            if (sigue == null) { gameObject.SetActive(false); return; }
            t += dt;
            Ubicar();
            Pintar(dt);
        }

        void PintarOnda()
        {
            float diametro = radioUso * 2f;
            if (!a.gameObject.activeSelf) a.gameObject.SetActive(true);
            if (!b.gameObject.activeSelf) b.gameObject.SetActive(true);
            PintarAroDeOnda(uso, ru, 0, diametro);
            PintarAroDeOnda(a, ra, 1, diametro);
            PintarAroDeOnda(b, rb, 2, diametro);
            ColorDeUso = new Color(0.3f, 1f, 0.45f, 1f);
        }

        void PintarAroDeOnda(Transform aro, MeshRenderer r, int k, float diametro)
        {
            EstadoDeOnda(k, onda, out float esc, out float alfa);
            aro.localScale = new Vector3(diametro * esc, 1f, diametro * esc);
            var c = new Color(0.3f, 1f, 0.45f, alfa);
            bloque.SetColor("_BaseColor", c); bloque.SetColor("_Color", c); r.SetPropertyBlock(bloque);
        }

        static Soldier JugadorActual()
        {
            var drv = PlayerInputDriver.Activo;
            return drv != null && drv.Brain != null ? drv.Brain.Current : null;
        }

        void Pintar(float dt)
        {
            if (usoAutomatico) radioUso = RadioDeInteraccionDelJugador();

            // R19: distancia del jugador al objetivo EN PLANTA, la misma medida con la que el director decide si puede subir
            var yo = JugadorActual();
            float dist = 999f;
            if (yo != null && sigue != null)
            {
                var d = yo.transform.position - sigue.position; d.y = 0f;
                dist = d.magnitude;
            }
            DistanciaAlJugador = dist;
            bool dentro = dist <= radioUso;
            DentroDelRadio = dentro;

            // aros de llamada: se cierran sobre el aro de uso al acercarse
            float k = Mathf.Clamp01((dist - DistanciaCerca) / (DistanciaLejos - DistanciaCerca));
            float cierre = Mathf.Pow(k, 0.35f);   // se cierra rapido al principio y se asienta al llegar
            float baseEsc = Mathf.Lerp(EscalaCerca, EscalaLejosEfectiva, cierre);
            float hz = Mathf.Lerp(HzCerca, HzLejos, k);
            fase += dt * hz;
            float s = Mathf.Sin(fase * Mathf.PI * 2f);
            float escA = baseEsc + AmplitudDelLatido * s;
            float escB = (baseEsc - AmplitudDelLatido * s) * 0.9f;
            EscalaActual = escA;
            float diametro = radioUso * 2f;
            bool llamada = !dentro;
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
            Color cu;
            if (dentro) cu = new Color(0.2f, 1f, 0.35f, 1f);
            else cu = Color.Lerp(Color.white, new Color(1f, 0.72f, 0.12f, 1f), 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f));
            ColorDeUso = cu;
            uso.localScale = new Vector3(diametro, 1f, diametro);
            bloque.SetColor("_BaseColor", cu); bloque.SetColor("_Color", cu); ru.SetPropertyBlock(bloque);

            // cartel 3D: solo adentro del radio y si hay texto
            bool verCartel = dentro && cartel != null && !string.IsNullOrEmpty(cartel.text);
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
