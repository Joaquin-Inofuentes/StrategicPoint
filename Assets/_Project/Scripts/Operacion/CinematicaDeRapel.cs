using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Mision;
using SP.Player;
using SP.Presentation;
using SP.UI;

namespace SP.Operacion
{
    // P9 (#123): cinematica de APERTURA de la Operacion Cuartel, 5 s y frenetica: el helicoptero entra rasante y a fondo, frena en seco
    // sobre la zona de inicio y los tres soldados bajan por cuerdas (rapel) hasta el piso; la escuadra queda en su sitio y el control
    // pasa al jugador (Kes, ver #110). Tres tomas con corte seco: (1) piso, el helicoptero pasa rugiendo; (2) al costado del helicoptero,
    // las cuerdas y los tres bajando; (3) a ras del piso, tocan tierra y el helicoptero se va. Sacudida de camara todo el tiempo.
    //
    // Los soldados REALES no se mueven (siguen en su lugar de inicio con la IA pausada): se los oculta apagando sus renderers y bajan unas
    // COPIAS VISUALES (como la tripulacion del helicoptero, bug #076) con una pose procedural de rapel; al tocar el piso la copia se borra
    // y el soldado real reaparece. Se reusa el helicoptero de la Operacion (se lo enciende para la toma y se lo apaga al terminar).
    //
    // Orden con la cinematica de pasos (WP9a): primero esta (rapel), y al terminar encadena CinematicaDeOperacion (los 6 objetivos) si no se
    // vio; de ahi pasa el control. Mantener [ESPACIO] 1 s la salta (solo esta toma: la de los pasos tiene su propio salto). YaVista propio:
    // sobrevive a recargar la escena pero no a un nuevo Play. OperacionPrueba.Arrancar la salta (Saltar()).
    public class CinematicaDeRapel : MonoBehaviour
    {
        public static bool Activa { get; private set; }
        public static bool YaVista;
        public static CinematicaDeRapel Instancia { get; private set; }

        // Costura de pruebas (el editor sin foco pierde los eventos de teclado).
        public static bool PruebaMantenerEspacio;

        public const float Duracion = 5f;
        public const float SegundosParaSaltar = 1f;
        public const float AlturaDeRapel = 11f;       // de la cabina al piso
        const float FinDeToma1 = 1.45f, FinDeToma2 = 3.25f;
        const float InicioDeCuerdas = 1.55f, SegundosDeBajada = 1.15f, EntreSoldados = 0.2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Activa = false; YaVista = false; Instancia = null; PruebaMantenerEspacio = false; }

        // ---- Estado visible (para las pruebas) ----
        public float Tiempo { get; private set; }
        public int Toma { get; private set; }                       // 1..3
        public float ProgresoDeSalto { get; private set; }
        public bool SaltadaPorElJugador { get; private set; }
        public static bool UltimaFueSaltada { get; private set; }   // la ultima que termino fue saltada por el jugador (para las pruebas: el objeto ya se destruyo)
        public int SoldadosEnElSuelo { get; private set; }
        public int CuerdasVisibles { get; private set; }
        public int CopiasColgando { get; private set; }
        public Vector3 PosicionDeCamara => camara != null ? camara.position : Vector3.zero;
        public IReadOnlyList<Soldier> Escuadra => escuadra;

        class Rapelista
        {
            public Soldier s;
            public GameObject rig, visual;
            public LineRenderer cuerda;
            public Vector3 aterrizaje, anclaLocal;
            public float pisoY, inicio, giroFinal;
            public bool abajo;
            public readonly List<Renderer> ocultos = new List<Renderer>();
        }

        OperacionDirector dir;
        PlayerInputDriver driver;
        Transform camara, heli;
        Helicoptero scriptHeli;
        Camera cam;
        float fovOriginal;
        GameObject lienzo;
        Image negro, anillo, barraArriba, barraAbajo;
        Text saltarTexto;
        bool bloqueado, encadenar = true, heliPrendido, heliEstabaActivo;
        Vector3 heliPosOriginal; Quaternion heliRotOriginal;
        readonly List<Canvas> canvasApagados = new List<Canvas>();
        readonly List<Rapelista> bajando = new List<Rapelista>();
        readonly List<Soldier> escuadra = new List<Soldier>();
        Sprite spriteAnillo;
        Material matCuerda;

        // Geometria de la escena: centro de la escuadra, rumbo de la linea y posicion de vuelo estacionario.
        Vector3 centro, adelante, derecha, hover;

        public static CinematicaDeRapel Iniciar(OperacionDirector director, bool encadenarConLosPasos = true)
        {
            if (Activa && Instancia != null) return Instancia;
            var go = new GameObject("CinematicaRapel");
            var c = go.AddComponent<CinematicaDeRapel>();
            c.dir = director; c.encadenar = encadenarConLosPasos;
            Instancia = c;
            c.StartCoroutine(c.Rutina());
            return c;
        }

        // Corta la cinematica YA, sin encadenar la de los pasos (OperacionPrueba y el arnes de checks la saltan siempre).
        public static void Saltar()
        {
            SP.Core.PartidaGuardada.ModoPrueba = true;   // P10: de pruebas/CLI
            if (Activa && Instancia != null) { Instancia.encadenar = false; Instancia.Terminar(); }
            YaVista = true;
        }

        // Para las pruebas: vuelve a empezar (aunque ya se haya visto).
        public static CinematicaDeRapel ReiniciarPorPrueba(bool encadenarConLosPasos = false)
        {
            Saltar();
            YaVista = false;
            return Iniciar(OperacionDirector.Instancia, encadenarConLosPasos);
        }

        // ---------------------------------------------------------------
        // Bloqueo y restablecimiento (mismo criterio que CinematicaDeOperacion)
        // ---------------------------------------------------------------
        void Bloquear()
        {
            bloqueado = true;
            Activa = true;
            SP.Ai.AiBrain.IAPausada = true;
            RomboVisibilidad.Suprimidos = true;
            driver = PlayerInputDriver.Activo;
            if (driver != null)
            {
                driver.enabled = false;
                if (driver.Rig != null) driver.Rig.enabled = false;
            }
            canvasApagados.Clear();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c == null || !c.enabled || c.renderMode == RenderMode.WorldSpace) continue;
                if (lienzo != null && c.gameObject == lienzo) continue;
                if (c.name == "SesionLog_Indicador" || c.name == "CanvasPausa") continue;
                c.enabled = false;
                canvasApagados.Add(c);
            }
            AlertQueue.Clear();
        }

        void Restaurar()
        {
            if (!bloqueado) return;
            bloqueado = false;
            SP.Ai.AiBrain.IAPausada = false;
            RomboVisibilidad.Suprimidos = false;
            if (driver != null)
            {
                driver.enabled = true;
                if (driver.Rig != null) driver.Rig.enabled = true;
            }
            foreach (var c in canvasApagados) if (c != null) c.enabled = true;
            canvasApagados.Clear();
        }

        // Soldados reales visibles otra vez, copias y cuerdas borradas, helicoptero apagado como estaba.
        void LimpiarEscena()
        {
            foreach (var r in bajando)
            {
                foreach (var rend in r.ocultos) if (rend != null) rend.enabled = true;
                r.ocultos.Clear();
                if (r.rig != null) Destroy(r.rig);
                if (r.cuerda != null) Destroy(r.cuerda.gameObject);
            }
            bajando.Clear();
            if (matCuerda != null) { Destroy(matCuerda); matCuerda = null; }
            if (heli != null && heliPrendido)
            {
                heliPrendido = false;
                if (scriptHeli != null) { scriptHeli.Volando = false; scriptHeli.DisparaCobertura = false; scriptHeli.PararRotor(); }
                heli.position = heliPosOriginal; heli.rotation = heliRotOriginal;
                heli.gameObject.SetActive(heliEstabaActivo);
            }
            if (cam != null && fovOriginal > 1f) cam.fieldOfView = fovOriginal;
        }

        void Terminar()
        {
            if (!Activa) return;
            StopAllCoroutines();
            LimpiarEscena();
            Restaurar();
            Activa = false;
            YaVista = true; UltimaFueSaltada = SaltadaPorElJugador;
            if (lienzo != null) { Destroy(lienzo); lienzo = null; }
            if (Instancia == this) Instancia = null;
            var d = dir; bool seguir = encadenar;
            Destroy(gameObject);
            // Encadena en el mismo cuadro (sin un cuadro de HUD en el medio): la cinematica de los pasos vuelve a bloquear todo.
            if (d != null)
            {
                if (seguir && !CinematicaDeOperacion.YaVista) CinematicaDeOperacion.Iniciar(d);
                else d.AlTerminarIntro();
            }
        }

        void OnDisable()
        {
            if (!bloqueado) return;
            LimpiarEscena();
            Restaurar();
            Activa = false;
            if (Instancia == this) Instancia = null;
        }

        void OnDestroy()
        {
            if (bloqueado) { LimpiarEscena(); Restaurar(); Activa = false; }
            if (Instancia == this) Instancia = null;
            if (lienzo != null) Destroy(lienzo);
        }

        // ---------------------------------------------------------------
        // Interfaz minima: barras de cine, negro de transicion y el anillo de "mantene ESPACIO"
        // ---------------------------------------------------------------
        static Font fuente;
        static Image NuevaImagen(Transform padre, string nombre, Color color)
        {
            var g = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(padre, false);
            var i = g.GetComponent<Image>(); i.color = color; i.raycastTarget = false;
            return i;
        }

        static void Anclar(RectTransform r, Vector2 aMin, Vector2 aMax, Vector2 pivote, Vector2 pos, Vector2 tam)
        {
            r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = pivote; r.anchoredPosition = pos; r.sizeDelta = tam;
        }

        Sprite CrearSpriteDeAnillo()
        {
            const int n = 96;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "AnilloDeSaltoRapel", hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((r - 0.70f) / 0.04f) * Mathf.Clamp01((1f - r) / 0.04f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        void ArmarLienzo()
        {
            lienzo = new GameObject("CinematicaRapelLienzo", typeof(Canvas), typeof(CanvasScaler));
            var cv = lienzo.GetComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 850;
            var sc = lienzo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 0.5f;
            var raiz = lienzo.transform;
            barraArriba = NuevaImagen(raiz, "BarraArriba", Color.black);
            Anclar(barraArriba.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 90f));
            barraAbajo = NuevaImagen(raiz, "BarraAbajo", Color.black);
            Anclar(barraAbajo.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 90f));
            spriteAnillo = CrearSpriteDeAnillo();
            var fondoAnillo = NuevaImagen(raiz, "AnilloFondo", new Color(1f, 1f, 1f, 0.22f));
            fondoAnillo.sprite = spriteAnillo;
            Anclar(fondoAnillo.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60f, 110f), new Vector2(86f, 86f));
            anillo = NuevaImagen(fondoAnillo.transform, "Anillo", new Color(1f, 0.85f, 0.3f, 1f));
            anillo.sprite = spriteAnillo; anillo.type = Image.Type.Filled; anillo.fillMethod = Image.FillMethod.Radial360; anillo.fillOrigin = 2; anillo.fillClockwise = true; anillo.fillAmount = 0f;
            Anclar(anillo.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var g = new GameObject("SaltarTexto", typeof(RectTransform), typeof(Text));
            g.transform.SetParent(raiz, false);
            saltarTexto = g.GetComponent<Text>();
            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            saltarTexto.font = fuente; saltarTexto.fontSize = 26; saltarTexto.fontStyle = FontStyle.Bold; saltarTexto.alignment = TextAnchor.MiddleRight;
            saltarTexto.color = new Color(1f, 1f, 1f, 0.85f); saltarTexto.raycastTarget = false; saltarTexto.horizontalOverflow = HorizontalWrapMode.Overflow;
            var o = g.AddComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.9f); o.effectDistance = new Vector2(2f, -2f);
            Anclar(saltarTexto.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-162f, 128f), new Vector2(520f, 50f));
            saltarTexto.text = "MANTENÉ [ESPACIO] 1 s PARA SALTAR";
            negro = NuevaImagen(raiz, "Negro", new Color(0f, 0f, 0f, 1f));
            Anclar(negro.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            barraArriba.transform.SetAsLastSibling(); barraAbajo.transform.SetAsLastSibling();
        }

        // ---------------------------------------------------------------
        // Escena
        // ---------------------------------------------------------------
        static float PisoDe(Soldier s)
        {
            var a = s.GetComponentInChildren<Animator>(true);
            return a != null ? a.transform.position.y : s.transform.position.y - 0.8f;
        }

        void Preparar()
        {
            escuadra.Clear();
            escuadra.AddRange(OperacionDirector.EscuadraDeLaOperacion());
            // Los lugares de aterrizaje son los de inicio: el que maneja el jugador va primero (el del medio).
            var yo = driver != null && driver.Brain != null ? driver.Brain.Current : null;
            if (yo != null && escuadra.Remove(yo)) escuadra.Insert(Mathf.Min(1, escuadra.Count), yo);
            centro = Vector3.zero; foreach (var s in escuadra) centro += s.transform.position;
            if (escuadra.Count > 0) centro /= escuadra.Count;
            // Rumbo: el eje de la escuadra (de un extremo al otro, en el plano); si estan juntos, hacia donde mira el primero.
            Vector3 eje = Vector3.zero;
            float mejor = 0f;
            for (int i = 0; i < escuadra.Count; i++)
                for (int j = i + 1; j < escuadra.Count; j++)
                {
                    var v = escuadra[j].transform.position - escuadra[i].transform.position; v.y = 0f;
                    if (v.sqrMagnitude > mejor) { mejor = v.sqrMagnitude; eje = v; }
                }
            var mira = escuadra.Count > 0 ? escuadra[0].transform.forward : Vector3.forward; mira.y = 0f;
            if (mejor < 1f) eje = mira;
            adelante = eje.normalized;
            if (Vector3.Dot(adelante, mira) < 0f) adelante = -adelante;
            derecha = Vector3.Cross(Vector3.up, adelante);
            float piso = escuadra.Count > 0 ? PisoDe(escuadra[0]) : centro.y - 0.8f;
            // La puerta derecha esta a 1,55 m del centro del helicoptero: el helicoptero se corre a la izquierda para que las cuerdas caigan sobre los soldados.
            hover = new Vector3(centro.x, piso + AlturaDeRapel + 1.1f, centro.z) - derecha * 1.55f;

            matCuerda = SafeMaterial.CreateLinea(new Color(0.12f, 0.1f, 0.08f, 1f));
            int idx = 0;
            foreach (var s in escuadra)
            {
                var r = new Rapelista { s = s, pisoY = PisoDe(s), aterrizaje = new Vector3(s.transform.position.x, PisoDe(s), s.transform.position.z) };
                r.inicio = InicioDeCuerdas + 0.15f + EntreSoldados * idx++;
                float t = Mathf.Clamp(Vector3.Dot(s.transform.position - centro, adelante), -1.4f, 1.4f);
                r.anclaLocal = new Vector3(1.55f, 1.0f, t);
                r.giroFinal = Random.Range(-35f, 35f);
                foreach (var rend in s.GetComponentsInChildren<Renderer>(true)) if (rend.enabled) { r.ocultos.Add(rend); rend.enabled = false; }
                // Copia visual: el modelo (Visual con su Animator) de este soldado, colgando de un nodo propio (el origen es el piso).
                var anim = s.GetComponentInChildren<Animator>(true);
                r.rig = new GameObject("RapelRig_" + s.name);
                r.rig.SetActive(false);
                if (anim != null)
                {
                    r.visual = Instantiate(anim.gameObject, r.rig.transform, false);
                    r.visual.name = "RapelVisual";
                    r.visual.transform.localPosition = Vector3.zero; r.visual.transform.localRotation = Quaternion.identity;
                    r.visual.SetActive(true);
                    foreach (var rend in r.visual.GetComponentsInChildren<Renderer>(true)) rend.enabled = true;
                    var a = r.visual.GetComponent<Animator>();
                    if (a != null) { a.cullingMode = AnimatorCullingMode.AlwaysAnimate; a.applyRootMotion = false; a.enabled = true; }
                    foreach (var c in r.visual.GetComponentsInChildren<Collider>(true)) Destroy(c);
                    r.visual.AddComponent<PoseDeRapel>();
                }
                var lg = new GameObject("RapelCuerda_" + s.name);
                r.cuerda = lg.AddComponent<LineRenderer>();
                r.cuerda.material = matCuerda; r.cuerda.useWorldSpace = true; r.cuerda.positionCount = 2;
                r.cuerda.startWidth = r.cuerda.endWidth = 0.05f; r.cuerda.numCapVertices = 2;
                r.cuerda.enabled = false;
                bajando.Add(r);
            }
        }

        // Posicion y cabeceo del helicoptero en el tiempo t: entra rasante por la popa a toda velocidad, frena en seco sobre la zona, se queda
        // balanceandose mientras bajan los soldados y al final sale hacia adelante y arriba.
        void PoseDelHeli(float t, out Vector3 pos, out Quaternion rot)
        {
            const float tLlegada = 1.6f, tSalida = 3.9f;
            Vector3 p; float pitch = 0f, roll = 0f;
            if (t < tLlegada)
            {
                float u = Mathf.Clamp01(t / tLlegada);
                float d = 150f * Mathf.Pow(1f - u, 2.4f);
                p = hover - adelante * d + Vector3.down * (3f * (1f - u));
                pitch = 16f * (1f - u) - 10f * Mathf.Sin(u * Mathf.PI);      // nariz abajo a toda velocidad, flare al frenar
                roll = Mathf.Sin(u * 9f) * 3f * (1f - u);
            }
            else if (t < tSalida)
            {
                float k = t - tLlegada;
                p = hover + Vector3.up * (Mathf.Sin(k * 1.9f) * 0.25f) + derecha * (Mathf.Sin(k * 1.3f) * 0.2f);
                pitch = Mathf.Sin(k * 2.1f) * 1.2f; roll = Mathf.Sin(k * 2.6f) * 2.2f;
            }
            else
            {
                float k = t - tSalida;
                p = hover + adelante * (0.5f * 34f * k * k) + Vector3.up * (3f * k + 14f * k * k);
                pitch = Mathf.Lerp(0f, 22f, Mathf.Clamp01(k / 0.6f));
                roll = -6f * Mathf.Clamp01(k / 0.5f);
            }
            pos = p;
            rot = Quaternion.LookRotation(adelante, Vector3.up) * Quaternion.Euler(pitch, 0f, roll);
        }

        // Altura de la cabina: por donde salen las cuerdas (puerta derecha).
        Vector3 AnclaMundo(Rapelista r) => heli.TransformPoint(r.anclaLocal);

        void ActualizarRapelistas(float t)
        {
            int enSuelo = 0, cuerdas = 0, colgando = 0;
            foreach (var r in bajando)
            {
                if (r.s == null) continue;
                float u = (t - r.inicio) / SegundosDeBajada;
                var ancla = AnclaMundo(r);
                if (t < r.inicio - 0.12f) { if (r.cuerda != null) r.cuerda.enabled = false; continue; }
                // La cuerda cae (0,12 s) antes de que el soldado se tire.
                float caida = Mathf.Clamp01((t - (r.inicio - 0.12f)) / 0.12f);
                var puntaSuelta = Vector3.Lerp(ancla, new Vector3(r.aterrizaje.x, r.pisoY, r.aterrizaje.z), caida);
                if (!r.abajo && u < 1f)
                {
                    float uu = Mathf.Clamp01(u);
                    // Cae rapido y frena al llegar (descenso controlado): 1 - (1-u)^2.
                    float e = 1f - (1f - uu) * (1f - uu);
                    float y = Mathf.Lerp(ancla.y - 1.9f, r.pisoY, e);
                    float lateral = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(uu / 0.35f));
                    var xz = Vector3.Lerp(new Vector3(ancla.x, 0f, ancla.z), new Vector3(r.aterrizaje.x, 0f, r.aterrizaje.z), lateral);
                    float vaiven = Mathf.Sin(t * 9f + r.s.Id) * 4f * (1f - uu);
                    if (r.rig != null)
                    {
                        if (!r.rig.activeSelf) r.rig.SetActive(true);
                        r.rig.transform.position = new Vector3(xz.x, y, xz.z);
                        var haciaAfuera = derecha;
                        r.rig.transform.rotation = Quaternion.LookRotation(adelante, Vector3.up) * Quaternion.Euler(0f, 0f, vaiven) * Quaternion.Euler(0f, Mathf.Lerp(-25f, 0f, uu), 0f);
                    }
                    if (r.cuerda != null)
                    {
                        r.cuerda.enabled = true; cuerdas++;
                        r.cuerda.SetPosition(0, ancla); r.cuerda.SetPosition(1, new Vector3(xz.x, y + 1.55f, xz.z));
                    }
                    colgando++;
                }
                else
                {
                    if (!r.abajo) AlTocarElPiso(r);
                    // Ya en el piso: la cuerda se suelta y se recoge hacia el helicoptero (0,5 s).
                    float recoge = Mathf.Clamp01((t - (r.inicio + SegundosDeBajada + 0.25f)) / 0.5f);
                    if (r.cuerda != null && recoge < 1f)
                    {
                        r.cuerda.enabled = true; cuerdas++;
                        r.cuerda.SetPosition(0, ancla);
                        r.cuerda.SetPosition(1, Vector3.Lerp(new Vector3(r.aterrizaje.x, r.pisoY + 2f, r.aterrizaje.z), ancla, recoge * recoge));
                    }
                    else if (r.cuerda != null) r.cuerda.enabled = false;
                    // El soldado real encara hacia donde cubre (giro corto de toma de posicion).
                    if (r.s != null)
                    {
                        float giro = Mathf.Clamp01((t - (r.inicio + SegundosDeBajada)) / 0.5f);
                        var rotFinal = Quaternion.LookRotation(Quaternion.Euler(0f, r.giroFinal, 0f) * adelante, Vector3.up);
                        r.s.transform.rotation = Quaternion.Slerp(r.s.transform.rotation, rotFinal, Mathf.Clamp01(giro * 0.35f));
                    }
                    enSuelo++;
                }
            }
            SoldadosEnElSuelo = enSuelo; CuerdasVisibles = cuerdas; CopiasColgando = colgando;
        }

        void AlTocarElPiso(Rapelista r)
        {
            r.abajo = true;
            foreach (var rend in r.ocultos) if (rend != null) rend.enabled = true;
            r.ocultos.Clear();
            if (r.rig != null) { Destroy(r.rig); r.rig = null; }
            // Polvo y golpe seco del aterrizaje.
            var p = r.aterrizaje + Vector3.up * 0.1f;
            for (int i = 0; i < 4; i++)
                SpriteFx.Lanzar("smoke_02", p + new Vector3(Random.Range(-0.4f, 0.4f), 0.05f, Random.Range(-0.4f, 0.4f)), new Color(0.7f, 0.65f, 0.55f, 0.55f), 0.5f, 1.8f, 0.7f,
                    Random.Range(-60f, 60f), new Vector3(Random.Range(-1.4f, 1.4f), 0.5f, Random.Range(-1.4f, 1.4f)), 2.2f, 1);
            AudioDirector.PlayAt(SfxKind.Land, p, 0.9f, 1f, PerfilEspacial.Base, 0.85f + 0.1f * Random.value);
        }

        // ---------------------------------------------------------------
        // Camara: tres tomas con corte seco y sacudida
        // ---------------------------------------------------------------
        void PonerCamara(float t)
        {
            if (camara == null) return;
            Vector3 pos, mira; float roll = 0f, amp = 0.1f, fov = 62f;
            var piso = new Vector3(centro.x, centro.y - 0.8f, centro.z);
            if (t < FinDeToma1)
            {
                // Toma 1: a ras del piso, al costado de la trayectoria; sigue al helicoptero que entra rugiendo.
                Toma = 1;
                pos = piso + derecha * 30f - adelante * 10f + Vector3.up * 1.6f;   // campo abierto al costado (adelante hay un cartel que tapa)
                mira = heli.position + Vector3.down * 1f;
                roll = Mathf.Lerp(7f, -4f, t / FinDeToma1); amp = 0.16f; fov = Mathf.Lerp(74f, 58f, Mathf.Clamp01(t / FinDeToma1));
            }
            else if (t < FinDeToma2)
            {
                // Toma 2: al costado de la puerta, mirando las cuerdas y a los tres bajando; orbita despacio.
                Toma = 2;
                float k = (t - FinDeToma1) / (FinDeToma2 - FinDeToma1);
                float giro = Mathf.Lerp(-12f, 14f, k) * Mathf.Deg2Rad;
                var lado = derecha * Mathf.Cos(giro) + adelante * Mathf.Sin(giro);
                pos = piso + lado * 12f + Vector3.up * Mathf.Lerp(3.0f, 3.8f, k);
                mira = piso + Vector3.up * 5.4f;
                roll = Mathf.Lerp(-6f, 4f, k); amp = 0.07f; fov = 68f;
            }
            else
            {
                // Toma 3: a ras del piso, mirando hacia arriba: tocan tierra y el helicoptero se va.
                Toma = 3;
                float k = (t - FinDeToma2) / (Duracion - FinDeToma2);
                pos = piso - adelante * 5.5f + derecha * (-2.5f + 5f * k) + Vector3.up * 0.8f;
                mira = Vector3.Lerp(piso + Vector3.up * 1.6f, heli.position, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.15f) / 0.85f) * 0.8f));
                roll = Mathf.Lerp(5f, -3f, k); amp = Mathf.Lerp(0.1f, 0.2f, k); fov = Mathf.Lerp(66f, 76f, k);
            }
            float n = t * 17f;
            var sacudida = new Vector3(Mathf.PerlinNoise(n, 0.3f) - 0.5f, Mathf.PerlinNoise(n, 7.1f) - 0.5f, Mathf.PerlinNoise(n, 13.7f) - 0.5f) * 2f * amp;
            camara.position = pos + sacudida;
            camara.rotation = Quaternion.LookRotation((mira - camara.position).normalized, Vector3.up) * Quaternion.Euler(0f, 0f, roll + (Mathf.PerlinNoise(n, 21.3f) - 0.5f) * 5f * (amp / 0.1f));
            if (cam != null) cam.fieldOfView = fov;
        }

        // ---------------------------------------------------------------
        IEnumerator Rutina()
        {
            Bloquear();
            ArmarLienzo();
            cam = CamaraPrincipal.Actual;
            if (cam == null && driver != null && driver.Rig != null) cam = driver.Rig.Cam;
            camara = cam != null ? cam.transform : null;
            fovOriginal = cam != null ? cam.fieldOfView : 0f;
            heli = dir != null ? dir.heli : null;
            scriptHeli = dir != null ? dir.ScriptHeli : null;
            Preparar();
            if (heli == null || escuadra.Count == 0) { Terminar(); yield break; }
            // El helicoptero de la Operacion (esta apagado hasta Resistir): se lo enciende para la toma y se lo devuelve a su sitio al terminar.
            heliEstabaActivo = heli.gameObject.activeSelf; heliPosOriginal = heli.position; heliRotOriginal = heli.rotation;
            heli.gameObject.SetActive(true); heliPrendido = true;
            if (scriptHeli != null) { scriptHeli.Volando = true; scriptHeli.RotorAFondo(); scriptHeli.DisparaCobertura = false; }
            PoseDelHeli(0f, out var p0, out var r0); heli.SetPositionAndRotation(p0, r0);
            Tiempo = 0f; Toma = 1;
            bool cuerdasSonaron = false;
            while (Tiempo < Duracion)
            {
                float dt = Time.deltaTime;
                Tiempo += dt;
                PoseDelHeli(Tiempo, out var p, out var r);
                heli.SetPositionAndRotation(p, r);
                if (!cuerdasSonaron && Tiempo >= InicioDeCuerdas)
                {
                    cuerdasSonaron = true;
                    GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.CameraSwoosh), 0.7f * AudioDirector.GainFor(SfxChannel.Sfx), 1.15f, "RapelSoga");
                }
                ActualizarRapelistas(Tiempo);
                PonerCamara(Tiempo);
                // Negro: entra de negro (0,3 s), un parpadeo en cada corte y sale al final.
                float a = Mathf.Clamp01(1f - Tiempo / 0.3f);
                a = Mathf.Max(a, Parpadeo(Tiempo, FinDeToma1), Parpadeo(Tiempo, FinDeToma2));
                if (negro != null) negro.color = new Color(0f, 0f, 0f, a);
                if (RevisarSalto()) yield break;
                yield return null;
            }
            Terminar();
        }

        static float Parpadeo(float t, float corte)
        {
            float d = Mathf.Abs(t - corte);
            return d < 0.05f ? 1f - d / 0.05f : 0f;
        }

        // Mantener [Espacio] 1 s salta la cinematica; true = ya termino.
        bool RevisarSalto()
        {
            bool espacio = PruebaMantenerEspacio || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);
            if (espacio) ProgresoDeSalto = Mathf.Min(1f, ProgresoDeSalto + Time.unscaledDeltaTime / SegundosParaSaltar);
            else ProgresoDeSalto = Mathf.Max(0f, ProgresoDeSalto - Time.unscaledDeltaTime * 2f);
            if (anillo != null) anillo.fillAmount = ProgresoDeSalto;
            if (ProgresoDeSalto >= 1f)
            {
                SaltadaPorElJugador = true;
                Terminar();
                return true;
            }
            return false;
        }
    }

    // Pose procedural de rapel sobre el esqueleto humanoide de la copia: brazos arriba sujetando la cuerda, piernas adelante y rodillas apenas flexionadas.
    // Corre despues del Animator (LateUpdate), igual que PoseSentada.
    [DefaultExecutionOrder(1000)]   // despues de los IK de brazo (BrazoIk) y de la animacion de accion
    public class PoseDeRapel : MonoBehaviour
    {
        public float brazos = -150f, muslos = -38f, rodillas = 28f;
        Animator anim;
        Transform brazoI, brazoD, antebrazoI, antebrazoD, muslo1, muslo2, pierna1, pierna2, cadera;
        bool resuelto;

        void Resolver()
        {
            resuelto = true;
            anim = GetComponent<Animator>();
            if (anim == null || !anim.isHuman) return;
            brazoI = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm); brazoD = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            antebrazoI = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm); antebrazoD = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            muslo1 = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg); muslo2 = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            pierna1 = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg); pierna2 = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            cadera = anim.GetBoneTransform(HumanBodyBones.Hips);
        }

        void LateUpdate()
        {
            if (!resuelto) Resolver();
            if (brazoI == null || brazoD == null || muslo1 == null || muslo2 == null) return;
            var eje = transform.right;
            brazoI.rotation = Quaternion.AngleAxis(brazos, eje) * brazoI.rotation;
            brazoD.rotation = Quaternion.AngleAxis(brazos, eje) * brazoD.rotation;
            muslo1.rotation = Quaternion.AngleAxis(muslos, eje) * muslo1.rotation;
            muslo2.rotation = Quaternion.AngleAxis(muslos, eje) * muslo2.rotation;
            if (pierna1 != null) pierna1.rotation = Quaternion.AngleAxis(-rodillas, eje) * pierna1.rotation;
            if (pierna2 != null) pierna2.rotation = Quaternion.AngleAxis(-rodillas, eje) * pierna2.rotation;
        }
    }
}
