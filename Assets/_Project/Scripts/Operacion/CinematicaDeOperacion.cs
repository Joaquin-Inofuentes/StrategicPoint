using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SP.Combat;
using SP.Core;
using SP.Player;
using SP.Presentation;
using SP.UI;

namespace SP.Operacion
{
    // Cinematica INICIAL de la Operacion Cuartel (rediseno): UNA SOLA CAMARA que recorre todo el nivel sin cortes, de principio a fin, siguiendo
    // los objetivos en orden: sobrevuela el cuartel y se detiene en las dos torretas de artilleria (lo PRIMERO que hay que detonar), pasa por la
    // muralla y el centro de datos, baja al patio del tanque, sigue la autopista entera (la curva real de la ruta del tanque) y termina sobre la
    // ciudad y el helipuerto. Los carteles salen del texto unico de abajo al centro (AvisoUnico): nada mas en pantalla salvo las barras de cine
    // y el anillo de "mantene [ESPACIO] para saltar". Al terminar encadena el rapel (CinematicaDeRapel) y de ahi pasa el control.
    //
    // Se llama CinematicaOperacion (y no CinematicaDeIntro) porque OperacionBuilder.LimpiarEscena borra la raiz CinematicaDeIntro de SC_Gameplay.
    // BLOQUEO TOTAL: driver y rig de camara apagados, IA pausada, HUD oculto y rombos suprimidos; si el componente se apaga a mitad, todo se restablece.
    // No se repite al reintentar (YaVista, estatico: sobrevive a recargar la escena pero no a un nuevo Play). OperacionPrueba.Arrancar la salta.
    public class CinematicaDeOperacion : MonoBehaviour
    {
        public static bool Activa { get; private set; }
        // P9 (#123): cualquiera de las dos cinematicas de apertura (sobrevuelo o rapel) esta en pantalla.
        public static bool IntroActiva => Activa || CinematicaDeRapel.Activa;
        public static bool YaVista;
        public static CinematicaDeOperacion Instancia { get; private set; }

        // Costuras de pruebas (el editor sin foco pierde los eventos de teclado).
        public static bool PruebaMantenerEspacio;
        public static bool PruebaTecla;

        public const float SegundosDeTitulo = 3f;
        public const float SegundosPorPaso = 4.3f;
        public const float SegundosParaSaltar = 1f;
        public const int TotalDePasos = 6;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Activa = false; YaVista = false; Instancia = null; PruebaMantenerEspacio = false; PruebaTecla = false; }

        public static readonly string[] Titulos =
        {
            "INFILTRÁ EL CUARTEL", "VOLÁ LOS DOS PUESTOS", "CENTRO DE DATOS", "EL BLINDADO", "LA CIUDAD", "EXTRACCIÓN",
        };
        public static readonly string[] Textos =
        {
            "Primero detoná las 2 torretas de artillería, después eliminá a todos.",
            "Solo el ASALTO coloca las cargas. Aguantá 20 s mientras arde la mecha.",
            "Un soldado descarga 30 s; el resto defiende las tres entradas.",
            "Derribá al tanque jefe con cohetes, cubrí a Kes mientras lo repara y escapá en él.",
            "Tomá la radio y dirigí la defensa: solo 2 posiciones, cualquier soldado sirve.",
            "Subí al helicóptero.",
        };

        // ---- Estado visible (para las pruebas) ----
        public int Paso { get; private set; } = -1;                 // indice del tramo del recorrido (-1 antes de empezar)
        public float Tiempo { get; private set; }                   // segundos desde el inicio
        public float Duracion { get; private set; }                 // segundos del recorrido completo
        public float ProgresoDeSalto { get; private set; }          // 0..1 mientras se mantiene [Espacio]
        public string TextoDelPanel => "";
        public string TextoDelCartel => AvisoUnico.TextoActual;
        public string TextoDelTitulo => "";
        public bool EsperandoTecla => false;
        public bool SaltadaPorElJugador { get; private set; }
        public Vector3 PosicionDeCamara => camaraCinematica != null ? camaraCinematica.position : Vector3.zero;
        public int CantidadDeCuadrosClave => claves.Count;

        OperacionDirector dir;
        PlayerInputDriver driver;
        Transform camaraCinematica;
        GameObject lienzo;
        Text saltarTexto;
        Image negro, anillo, barraArriba, barraAbajo;
        bool bloqueado;
        readonly List<Canvas> canvasApagados = new List<Canvas>();
        Sprite spriteAnillo;
        static Font fuente;

        public static CinematicaDeOperacion Iniciar(OperacionDirector director)
        {
            if (Activa && Instancia != null) return Instancia;
            var go = new GameObject("CinematicaOperacion");
            var c = go.AddComponent<CinematicaDeOperacion>();
            c.dir = director;
            Instancia = c;
            c.StartCoroutine(c.Rutina());
            return c;
        }

        // Para las pruebas y para no dejar nunca una sesion automatizada colgada: corta la cinematica YA y devuelve el control.
        public static void Saltar()
        {
            SP.Core.PartidaGuardada.ModoPrueba = true;   // P10: Saltar() es de pruebas/CLI: nunca tocan la partida guardada real
            CinematicaDeRapel.Saltar();   // P9 (#123): el rapel tambien (sin encadenar)
            if (Activa && Instancia != null) Instancia.Terminar();
            YaVista = true;
        }

        // Para las pruebas: vuelve a empezar desde 0 (aunque ya se haya visto).
        public static CinematicaDeOperacion ReiniciarPorPrueba()
        {
            Saltar();
            YaVista = false;
            return Iniciar(OperacionDirector.Instancia);
        }

        // ---------------------------------------------------------------
        // Bloqueo y restablecimiento
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
                if (c.name == "SesionLog_Indicador" || c.name == "CanvasPausa" || c.name == "AvisoUnico") continue;
                c.enabled = false;
                canvasApagados.Add(c);
            }
            AlertQueue.Clear();
            AvisoUnico.Limpiar();
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

        void Terminar()
        {
            if (!Activa) return;
            StopAllCoroutines();
            Restaurar();
            Activa = false;
            YaVista = true;
            AvisoUnico.Limpiar();
            if (lienzo != null) { Destroy(lienzo); lienzo = null; }
            if (Instancia == this) Instancia = null;
            var d = dir;
            Destroy(gameObject);
            // Encadena en el mismo cuadro (sin un cuadro de HUD en el medio): el rapel vuelve a bloquear todo.
            if (d != null)
            {
                if (!CinematicaDeRapel.YaVista) CinematicaDeRapel.Iniciar(d);
                else d.AlTerminarIntro();
            }
        }

        // Si el componente se apaga a mitad (parar el Play, cambiar de escena) no queda la IA pausada ni el HUD oculto.
        void OnDisable()
        {
            if (!bloqueado) return;
            Restaurar();
            Activa = false;
            if (Instancia == this) Instancia = null;
        }

        void OnDestroy()
        {
            if (bloqueado) { Restaurar(); Activa = false; }
            if (Instancia == this) Instancia = null;
            if (lienzo != null) Destroy(lienzo);
        }

        // ---------------------------------------------------------------
        // Interfaz (barras de cine, negro de entrada/salida y anillo de salto)
        // ---------------------------------------------------------------
        static Text NuevaTexto(Transform padre, string nombre, int tam, TextAnchor ancla, Color color)
        {
            var g = new GameObject(nombre, typeof(RectTransform), typeof(Text), typeof(Outline));
            g.transform.SetParent(padre, false);
            var t = g.GetComponent<Text>();
            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.font = fuente; t.fontSize = tam; t.fontStyle = FontStyle.Bold; t.alignment = ancla; t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var o = g.GetComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.9f); o.effectDistance = new Vector2(2f, -2f);
            return t;
        }

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
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "AnilloDeSalto", hideFlags = HideFlags.HideAndDontSave };
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
            lienzo = new GameObject("CinematicaOperacionLienzo", typeof(Canvas), typeof(CanvasScaler));
            var cv = lienzo.GetComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 850;       // debajo de la pausa (900) y del texto unico (860): ESC sigue funcionando
            var sc = lienzo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 0.5f;
            var raiz = lienzo.transform;

            barraArriba = NuevaImagen(raiz, "BarraArriba", Color.black);
            Anclar(barraArriba.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 0));
            barraAbajo = NuevaImagen(raiz, "BarraAbajo", Color.black);
            Anclar(barraAbajo.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 0));

            // Saltar: anillo + texto abajo a la derecha.
            spriteAnillo = CrearSpriteDeAnillo();
            var fondoAnillo = NuevaImagen(raiz, "AnilloFondo", new Color(1f, 1f, 1f, 0.22f));
            fondoAnillo.sprite = spriteAnillo;
            Anclar(fondoAnillo.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60f, 130f), new Vector2(86f, 86f));
            anillo = NuevaImagen(fondoAnillo.transform, "Anillo", new Color(1f, 0.85f, 0.3f, 1f));
            anillo.sprite = spriteAnillo; anillo.type = Image.Type.Filled; anillo.fillMethod = Image.FillMethod.Radial360; anillo.fillOrigin = 2; anillo.fillClockwise = true; anillo.fillAmount = 0f;
            Anclar(anillo.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            saltarTexto = NuevaTexto(raiz, "SaltarTexto", 24, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, 0.85f));
            Anclar(saltarTexto.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-162f, 148f), new Vector2(520f, 50f));
            saltarTexto.text = "MANTENÉ [ESPACIO] 1 s PARA SALTAR";

            // Negro de entrada y de salida.
            negro = NuevaImagen(raiz, "Negro", new Color(0f, 0f, 0f, 1f));
            Anclar(negro.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            barraArriba.transform.SetAsLastSibling(); barraAbajo.transform.SetAsLastSibling();
        }

        // ---------------------------------------------------------------
        // El recorrido: cuadros clave (posicion de camara, punto al que mira, velocidad en m/s, cartel) unidos por una curva Catmull-Rom.
        // La camara avanza por LONGITUD de la curva con la velocidad interpolada entre cuadros: lenta sobre las torretas, rapida sobre la autopista.
        // ---------------------------------------------------------------
        struct Clave { public Vector3 pos, mira; public float velocidad; public string cartel; public Color color; }
        readonly List<Clave> claves = new List<Clave>();
        // Muestras densas de la curva: posicion, indice fraccional de cuadro (para la mirada), longitud y tiempo acumulados.
        readonly List<Vector3> muestraPos = new List<Vector3>();
        readonly List<float> muestraK = new List<float>(), muestraT = new List<float>();
        const int MuestrasPorTramo = 24;
        static readonly Color Dorado = new Color(1f, 0.9f, 0.4f), Celeste = new Color(0.6f, 0.85f, 1f);

        void C(Vector3 pos, Vector3 mira, float velocidad, string cartel = null, Color? color = null)
            => claves.Add(new Clave { pos = pos, mira = mira, velocidad = velocidad, cartel = cartel, color = color ?? Dorado });

        static Vector3 Plano(Vector3 v, float y) => new Vector3(v.x, y, v.z);

        void ArmarRecorrido()
        {
            claves.Clear();
            var d = dir;
            Vector3 inicio = new Vector3(0f, 0f, -373f);
            var tiros = new List<Vector3>();
            if (d != null && d.torres != null)
                foreach (var t in d.torres) if (t != null && t.tipo == TorreDestruible.Tipo.Torreta) tiros.Add(t.transform.position);
            tiros.Sort((a, b) => a.z.CompareTo(b.z));
            Vector3 tA = tiros.Count > 0 ? tiros[0] : new Vector3(45f, 0f, -272f);
            Vector3 tB = tiros.Count > 1 ? tiros[1] : new Vector3(-45f, 0f, -232f);
            Vector3 alto(Vector3 p, float h) => new Vector3(p.x, h, p.z);

            // 1) Aproximacion y cuartel: se frena en cada torreta de artilleria (lo primero que hay que detonar).
            C(alto(inicio, 34f) + new Vector3(0f, 0f, -26f), new Vector3(0f, 4f, -330f), 10f, "OPERACIÓN CUARTEL");
            C(new Vector3(-14f, 26f, -346f), tA + Vector3.up * 5f, 9f);
            C(tA + new Vector3(-26f, 15f, -22f), tA + Vector3.up * 5f, 7f, "PRIMERO: DETONÁ LAS 2 TORRETAS DE ARTILLERÍA", new Color(1f, 0.55f, 0.3f));
            C(tA + new Vector3(-23f, 17f, 16f), tA + Vector3.up * 5f, 7f);
            C(new Vector3(0f, 24f, -250f), Vector3.Lerp(tA, tB, 0.7f) + Vector3.up * 5f, 9f);
            C(tB + new Vector3(24f, 15f, -16f), tB + Vector3.up * 5f, 7f);
            C(tB + new Vector3(24f, 17f, 20f), tB + Vector3.up * 5f, 9f, "DESPUÉS ELIMINÁ A TODO EL CUARTEL");

            // 2) Muralla, centro de datos y patio del tanque.
            C(new Vector3(0f, 28f, -196f), new Vector3(0f, 3f, -150f), 20f);
            C(new Vector3(0f, 30f, -160f), new Vector3(0f, 3f, -110f), 24f, "VOLÁ LOS 2 PUESTOS · DESCARGÁ LOS DATOS");
            C(new Vector3(0f, 34f, -100f), new Vector3(0f, 2f, -50f), 30f);
            C(new Vector3(0f, 32f, -40f), new Vector3(0f, 2f, 10f), 30f);
            var tanquePos = d != null && d.tanque != null ? d.tanque.transform.position : new Vector3(0f, 0f, 22f);
            C(tanquePos + new Vector3(8f, 24f, -34f), tanquePos + Vector3.up * 2f, 36f, "ESCAPÁ EN EL BLINDADO", Celeste);

            // 3) La autopista entera (la curva real de la ruta): la camara va detras y arriba, mirando adelante.
            var ruta = d != null ? d.rutaDelTanque : null;
            if (ruta != null && ruta.Length > 4)
            {
                for (int i = 2; i < ruta.Length - 1; i += 3)
                {
                    var p = ruta[i].position; var adel = ruta[Mathf.Min(ruta.Length - 1, i + 3)].position; var atras = ruta[i - 2].position;
                    var fwd = Plano(adel - atras, 0f).normalized;
                    C(Plano(p, 38f) - fwd * 6f, adel + Vector3.up * 2f, 68f);
                }
            }

            // 4) Ciudad y helipuerto.
            Vector3 plaza = d != null && d.plaza != null ? d.plaza.position : new Vector3(322f, 0f, 228f);
            Vector3 helipad = d != null && d.heliAterrizaje != null ? d.heliAterrizaje.position : plaza;
            C(plaza + new Vector3(-62f, 40f, -40f), plaza + Vector3.up * 2f, 28f, "DEFENDÉ LA CIUDAD DESDE LA RADIO: 2 POSICIONES", Celeste);
            C(helipad + new Vector3(34f, 30f, -30f), helipad + Vector3.up * 3f, 18f, "Y EXTRAÉ EN EL HELICÓPTERO", new Color(0.55f, 1f, 0.6f));
            C(helipad + new Vector3(22f, 26f, 20f), helipad + Vector3.up * 3f, 10f);

            // Curva densa: posiciones Catmull-Rom con indice fraccional y tiempo acumulado (dt = ds / velocidad interpolada).
            muestraPos.Clear(); muestraK.Clear(); muestraT.Clear();
            float tiempo = 0f; Vector3 previa = Vector3.zero; bool primera = true;
            for (int i = 0; i < claves.Count - 1; i++)
            {
                for (int s = 0; s < MuestrasPorTramo; s++)
                {
                    float u = (float)s / MuestrasPorTramo;
                    var p = CatmullRom(claves, i, u, c => c.pos);
                    float v = Mathf.Lerp(claves[i].velocidad, claves[i + 1].velocidad, Mathf.SmoothStep(0f, 1f, u));
                    if (!primera) tiempo += Vector3.Distance(p, previa) / Mathf.Max(1f, v);
                    muestraPos.Add(p); muestraK.Add(i + u); muestraT.Add(tiempo);
                    previa = p; primera = false;
                }
            }
            muestraPos.Add(claves[claves.Count - 1].pos); muestraK.Add(claves.Count - 1);
            muestraT.Add(tiempo + Vector3.Distance(claves[claves.Count - 1].pos, previa) / Mathf.Max(1f, claves[claves.Count - 1].velocidad));
            Duracion = muestraT[muestraT.Count - 1];
        }

        static Vector3 CatmullRom(List<Clave> c, int i, float u, System.Func<Clave, Vector3> f)
        {
            Vector3 p0 = f(c[Mathf.Max(0, i - 1)]), p1 = f(c[i]), p2 = f(c[i + 1]), p3 = f(c[Mathf.Min(c.Count - 1, i + 2)]);
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * ((2f * p1) + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }

        // Pose en el instante t (segundos desde el inicio del recorrido).
        void PoseEn(float t, out Vector3 pos, out Vector3 mira, out float k)
        {
            int lo = 0, hi = muestraT.Count - 1;
            while (hi - lo > 1) { int mid = (lo + hi) >> 1; if (muestraT[mid] <= t) lo = mid; else hi = mid; }
            float t0 = muestraT[lo], t1 = muestraT[hi];
            float f = t1 > t0 ? Mathf.Clamp01((t - t0) / (t1 - t0)) : 0f;
            pos = Vector3.Lerp(muestraPos[lo], muestraPos[hi], f);
            k = Mathf.Lerp(muestraK[lo], muestraK[hi], f);
            int ik = Mathf.Clamp(Mathf.FloorToInt(k), 0, claves.Count - 2);
            mira = CatmullRom(claves, ik, Mathf.Clamp01(k - ik), c => c.mira);
        }

        IEnumerator Rutina()
        {
            Bloquear();
            ArmarLienzo();
            ArmarRecorrido();
            var cam = CamaraPrincipal.Actual;
            if (cam == null && driver != null && driver.Rig != null) cam = driver.Rig.Cam;
            camaraCinematica = cam != null ? cam.transform : null;
            Tiempo = 0f; Paso = -1;

            float t = 0f; int ultimoCuadro = -1;
            Quaternion rotAnterior = Quaternion.identity; bool conRot = false;
            const float Entrada = 0.8f, Salida = 1.0f;
            float final = Duracion;
            while (t < final + Salida)
            {
                float dt = Time.deltaTime;
                t += dt; Tiempo = t;
                float tc = Mathf.Min(t, final);
                PoseEn(tc, out var pos, out var mira, out float k);
                if (camaraCinematica != null)
                {
                    var rot = Quaternion.LookRotation((mira - pos).sqrMagnitude > 0.01f ? (mira - pos) : Vector3.forward, Vector3.up);
                    // Giro suave: la mirada nunca salta aunque cambie el objetivo (de una torreta a la otra).
                    rotAnterior = conRot ? Quaternion.Slerp(rotAnterior, rot, 1f - Mathf.Exp(-5f * dt)) : rot;
                    conRot = true;
                    camaraCinematica.SetPositionAndRotation(pos, rotAnterior);
                }
                int cuadro = Mathf.Clamp(Mathf.FloorToInt(k), 0, claves.Count - 1);
                if (cuadro != ultimoCuadro)
                {
                    for (int q = ultimoCuadro + 1; q <= cuadro; q++)
                        if (!string.IsNullOrEmpty(claves[q].cartel)) { AvisoUnico.Limpiar(); AvisoUnico.Mostrar(claves[q].cartel, 4f, claves[q].color, AvisoUnico.PrioridadAlta); }
                    ultimoCuadro = cuadro; Paso = cuadro;
                }
                float b = Mathf.Clamp01(t / 0.5f) * 110f;
                barraArriba.rectTransform.sizeDelta = new Vector2(0f, b);
                barraAbajo.rectTransform.sizeDelta = new Vector2(0f, b);
                float a = Mathf.Clamp01(1f - t / Entrada);                       // entra de negro
                if (t > final) a = Mathf.Max(a, Mathf.Clamp01((t - final) / Salida));   // sale a negro
                negro.color = new Color(0f, 0f, 0f, a);
                if (RevisarSalto()) yield break;
                yield return null;
            }
            Terminar();
        }

        // Mantener [Espacio] 1 s salta toda la cinematica; true = ya termino.
        bool RevisarSalto()
        {
            bool espacio = PruebaMantenerEspacio || PruebaTecla || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);
            if (espacio) ProgresoDeSalto = Mathf.Min(1f, ProgresoDeSalto + Time.unscaledDeltaTime / SegundosParaSaltar);
            else ProgresoDeSalto = Mathf.Max(0f, ProgresoDeSalto - Time.unscaledDeltaTime * 2f);
            if (anillo != null) anillo.fillAmount = ProgresoDeSalto;
            if (ProgresoDeSalto >= 1f)
            {
                SaltadaPorElJugador = true;
                PruebaTecla = false;
                CinematicaDeRapel.YaVista = true;   // saltar el recorrido salta tambien el rapel
                SonarSalteo();
                Terminar();
                return true;
            }
            return false;
        }

        static void SonarSalteo()
        {
            var cam = CamaraPrincipal.Actual;
            if (cam == null) return;
            AudioDirector.PlayClipAt(GenericSfx.GetWeaponShot(WeaponKind.Rifle), cam.transform.position + cam.transform.forward * 6f, 0.5f, 1f, PerfilEspacial.Disparo);
        }
    }
}
