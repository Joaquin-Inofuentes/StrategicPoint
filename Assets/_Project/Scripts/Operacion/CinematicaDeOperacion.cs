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
    // WP9a (#097, primera mitad): cinematica INICIAL de la Operacion Cuartel. Muestra, paso a paso, los 6 objetivos que hay que cumplir
    // (dolly aereo desde 35 m hasta 18 m sobre el ancla de cada zona) con un panel lateral que lista los 6 (el actual resaltado, los vistos
    // con tilde) y un cartel abajo con el texto del paso. Dura ~29 s (3 s de titulo + 6 pasos de 4,3 s) y cierra con "PRESIONA UNA TECLA PARA
    // EMPEZAR". Mantener [ESPACIO] 1 s la salta (con un anillo de progreso visible). Se llama CinematicaOperacion (y no CinematicaDeIntro)
    // porque OperacionBuilder.LimpiarEscena borra la raiz CinematicaDeIntro de SC_Gameplay.
    //
    // BLOQUEO TOTAL, igual que CinematicaDeIntro.Rutina: el driver y el rig de camara apagados (no hay input ni movimiento), la IA de todos
    // pausada, el HUD oculto y el rombo de la escuadra suprimido. Si el componente se apaga a mitad (cambio de escena, Stop) todo se restablece.
    // No se repite al reintentar (YaVista, estatico: sobrevive a recargar la escena pero no a un nuevo Play). OperacionPrueba.Arrancar la salta.
    public class CinematicaDeOperacion : MonoBehaviour
    {
        public static bool Activa { get; private set; }
        // P9 (#123): cualquiera de las dos cinematicas de apertura (rapel o pasos) esta en pantalla.
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
            "Eliminá a todos. Rompé los reflectores que te alumbran y derribá las torres con explosivos.",
            "Solo el ASALTO coloca las cargas. Aguantá 20 s mientras arde la mecha.",
            "Un soldado descarga 30 s; el resto defiende las tres entradas.",
            "Derribá al tanque jefe con cohetes, cubrí a Kes mientras lo repara y escapá en él.",
            "Tomá la radio y dirigí la defensa desde la VISTA TÁCTICA.",
            "Subí al helicóptero.",
        };

        // ---- Estado visible (para las pruebas) ----
        public int Paso { get; private set; } = -1;                 // -1 titulo, 0..5 pasos, 6 "presiona una tecla"
        public float Tiempo { get; private set; }                   // segundos desde el inicio
        public float ProgresoDeSalto { get; private set; }          // 0..1 mientras se mantiene [Espacio]
        public string TextoDelPanel => panelTexto != null ? panelTexto.text : "";
        public string TextoDelCartel => cartelTexto != null ? cartelTexto.text : "";
        public string TextoDelTitulo => tituloTexto != null && tituloGrupo != null && tituloGrupo.alpha > 0.05f ? tituloTexto.text : "";
        public bool EsperandoTecla { get; private set; }
        public bool SaltadaPorElJugador { get; private set; }
        public Vector3 PosicionDeCamara => camaraCinematica != null ? camaraCinematica.position : Vector3.zero;

        OperacionDirector dir;
        PlayerInputDriver driver;
        Transform camaraCinematica;
        GameObject lienzo;
        Text panelTexto, cartelTexto, tituloTexto, subtituloTexto, finalTexto, saltarTexto;
        CanvasGroup tituloGrupo, cartelGrupo, finalGrupo, panelGrupo;
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

        // Para las pruebas y para no dejar nunca una sesion automatizada colgada esperando una tecla: corta la cinematica YA y devuelve el control.
        public static void Saltar()
        {
            SP.Core.PartidaGuardada.ModoPrueba = true;   // P10: Saltar() es de pruebas/CLI: nunca tocan la partida guardada real
            CinematicaDeRapel.Saltar();   // P9 (#123): el rapel tambien (sin encadenar)
            if (Activa && Instancia != null) Instancia.Terminar();
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

        void Terminar()
        {
            if (!Activa) return;
            StopAllCoroutines();
            Restaurar();
            Activa = false;
            YaVista = true;
            EsperandoTecla = false;
            if (lienzo != null) { Destroy(lienzo); lienzo = null; }
            if (Instancia == this) Instancia = null;
            if (dir != null) dir.AlTerminarIntro();
            Destroy(gameObject);
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
        // Interfaz
        // ---------------------------------------------------------------
        static Text NuevaTexto(Transform padre, string nombre, int tam, TextAnchor ancla, Color color, bool borde = true)
        {
            var g = new GameObject(nombre, typeof(RectTransform), typeof(Text));
            g.transform.SetParent(padre, false);
            var t = g.GetComponent<Text>();
            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.font = fuente; t.fontSize = tam; t.fontStyle = FontStyle.Bold; t.alignment = ancla; t.color = color;
            t.raycastTarget = false; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            if (borde)
            {
                var o = g.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.9f); o.effectDistance = new Vector2(2f, -2f);
            }
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

        // Anillo (textura generada) para el progreso de "mantener ESPACIO".
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
            cv.sortingOrder = 850;       // debajo de la pausa (900): ESC sigue funcionando
            var sc = lienzo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 0.5f;
            var raiz = lienzo.transform;

            // Barras de cine.
            barraArriba = NuevaImagen(raiz, "BarraArriba", Color.black);
            Anclar(barraArriba.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 0));
            barraAbajo = NuevaImagen(raiz, "BarraAbajo", Color.black);
            Anclar(barraAbajo.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 0));

            // Panel lateral izquierdo con los 6 pasos.
            var panel = new GameObject("PanelDePasos", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            panel.transform.SetParent(raiz, false);
            panelGrupo = panel.GetComponent<CanvasGroup>(); panelGrupo.alpha = 0f;
            var pi = panel.GetComponent<Image>(); pi.color = new Color(0.03f, 0.05f, 0.08f, 0.72f); pi.raycastTarget = false;
            Anclar(panel.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(60f, 20f), new Vector2(520f, 380f));
            var filete = NuevaImagen(panel.transform, "Filete", new Color(1f, 0.82f, 0.3f, 0.95f));
            Anclar(filete.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(5f, 0f));
            var cab = NuevaTexto(panel.transform, "Cabecera", 22, TextAnchor.UpperLeft, new Color(1f, 0.82f, 0.3f), false);
            Anclar(cab.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(26f, -14f), new Vector2(-40f, 34f));
            cab.text = "OBJETIVOS DE LA OPERACIÓN";
            panelTexto = NuevaTexto(panel.transform, "Pasos", 28, TextAnchor.UpperLeft, Color.white, false);
            panelTexto.lineSpacing = 1.25f;
            Anclar(panelTexto.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(26f, -56f), new Vector2(-40f, -70f));
            panelTexto.rectTransform.offsetMin = new Vector2(26f, 14f); panelTexto.rectTransform.offsetMax = new Vector2(-14f, -56f);

            // Cartel de abajo (titulo del paso + texto).
            var cartel = new GameObject("CartelDelPaso", typeof(RectTransform), typeof(CanvasGroup));
            cartel.transform.SetParent(raiz, false);
            cartelGrupo = cartel.GetComponent<CanvasGroup>(); cartelGrupo.alpha = 0f;
            Anclar(cartel.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0f, 150f), new Vector2(1500f, 230f));
            var fondoCartel = NuevaImagen(cartel.transform, "Fondo", new Color(0.03f, 0.05f, 0.08f, 0.6f));
            Anclar(fondoCartel.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            cartelTexto = NuevaTexto(cartel.transform, "Texto", 38, TextAnchor.MiddleCenter, Color.white);
            Anclar(cartelTexto.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            cartelTexto.rectTransform.offsetMin = new Vector2(24f, 12f); cartelTexto.rectTransform.offsetMax = new Vector2(-24f, -12f);
            cartelTexto.resizeTextForBestFit = true; cartelTexto.resizeTextMinSize = 22; cartelTexto.resizeTextMaxSize = 40;

            // Titulo central (primeros 3 s).
            var tit = new GameObject("TituloCentral", typeof(RectTransform), typeof(CanvasGroup));
            tit.transform.SetParent(raiz, false);
            tituloGrupo = tit.GetComponent<CanvasGroup>(); tituloGrupo.alpha = 0f;
            Anclar(tit.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1500f, 300f));
            tituloTexto = NuevaTexto(tit.transform, "Titulo", 110, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.45f));
            Anclar(tituloTexto.rectTransform, new Vector2(0, 0.35f), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            tituloTexto.horizontalOverflow = HorizontalWrapMode.Overflow;
            tituloTexto.text = "OPERACIÓN CUARTEL";
            subtituloTexto = NuevaTexto(tit.transform, "Subtitulo", 46, TextAnchor.MiddleCenter, Color.white);
            Anclar(subtituloTexto.rectTransform, new Vector2(0, 0), new Vector2(1, 0.35f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            subtituloTexto.text = "Seis pasos para salir con vida";

            // "Presiona una tecla" (final).
            var fin = new GameObject("PresionaUnaTecla", typeof(RectTransform), typeof(CanvasGroup));
            fin.transform.SetParent(raiz, false);
            finalGrupo = fin.GetComponent<CanvasGroup>(); finalGrupo.alpha = 0f;
            Anclar(fin.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0f, 420f), new Vector2(1500f, 90f));
            finalTexto = NuevaTexto(fin.transform, "Texto", 54, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.6f));
            Anclar(finalTexto.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            finalTexto.text = "PRESIONÁ UNA TECLA PARA EMPEZAR";

            // Saltar: anillo + texto abajo a la derecha.
            spriteAnillo = CrearSpriteDeAnillo();
            var fondoAnillo = NuevaImagen(raiz, "AnilloFondo", new Color(1f, 1f, 1f, 0.22f));
            fondoAnillo.sprite = spriteAnillo;
            Anclar(fondoAnillo.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60f, 130f), new Vector2(86f, 86f));
            anillo = NuevaImagen(fondoAnillo.transform, "Anillo", new Color(1f, 0.85f, 0.3f, 1f));
            anillo.sprite = spriteAnillo; anillo.type = Image.Type.Filled; anillo.fillMethod = Image.FillMethod.Radial360; anillo.fillOrigin = 2; anillo.fillClockwise = true; anillo.fillAmount = 0f;
            Anclar(anillo.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            saltarTexto = NuevaTexto(raiz, "SaltarTexto", 26, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, 0.85f));
            Anclar(saltarTexto.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-162f, 148f), new Vector2(520f, 50f));
            saltarTexto.text = "MANTENÉ [ESPACIO] 1 s PARA SALTAR";

            // Negro de transicion entre tomas (encima de todo menos de las barras).
            negro = NuevaImagen(raiz, "Negro", new Color(0f, 0f, 0f, 1f));
            Anclar(negro.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            barraArriba.transform.SetAsLastSibling(); barraAbajo.transform.SetAsLastSibling();
        }

        // ---------------------------------------------------------------
        // Tomas
        // ---------------------------------------------------------------
        Vector3 PosicionDelAncla(int k)
        {
            var anclas = dir != null ? dir.anclasDeZona : null;
            if (anclas != null && k >= 0 && k < anclas.Length && anclas[k] != null) return anclas[k].position;
            return Vector3.zero;
        }

        // Dolly: desde 35 m de altura hasta 18 m sobre el ancla, mirandola; la camara bordea apenas hacia un costado. La toma de la muralla es mas
        // abierta (se ven los dos puestos a 76 m uno del otro).
        void PoseDeToma(int k, float u, out Vector3 pos, out Quaternion rot)
        {
            var ancla = PosicionDelAncla(k);
            float s = u * u * (3f - 2f * u);
            float h0 = 35f, h1 = 18f, d0 = 40f, d1 = 24f;
            if (k == 1) { h0 = 55f; h1 = 32f; d0 = 62f; d1 = 42f; }
            float h = Mathf.Lerp(h0, h1, s), d = Mathf.Lerp(d0, d1, s);
            var lado = Quaternion.Euler(0f, Mathf.Lerp(-24f, 24f, s), 0f) * Vector3.back;
            pos = ancla + lado * d + Vector3.up * h;
            rot = Quaternion.LookRotation((ancla + Vector3.up * 3f) - pos, Vector3.up);
        }

        IEnumerator Rutina()
        {
            Bloquear();
            ArmarLienzo();
            var cam = CamaraPrincipal.Actual;
            if (cam == null && driver != null && driver.Rig != null) cam = driver.Rig.Cam;
            camaraCinematica = cam != null ? cam.transform : null;
            Tiempo = 0f;

            // Barras de cine hacia adentro (0,5 s).
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime; Tiempo += Time.deltaTime;
                float b = Mathf.Clamp01(t / 0.5f) * 110f;
                barraArriba.rectTransform.sizeDelta = new Vector2(0f, b);
                barraAbajo.rectTransform.sizeDelta = new Vector2(0f, b);
                if (RevisarSalto()) yield break;
                yield return null;
            }

            // Titulo (3 s): toma amplia del cuartel.
            Paso = -1;
            cartelGrupo.alpha = 0f; panelGrupo.alpha = 0f;
            ActualizarPanel(-1);
            t = 0f;
            while (t < SegundosDeTitulo)
            {
                t += Time.deltaTime; Tiempo += Time.deltaTime;
                float u = Mathf.Clamp01(t / SegundosDeTitulo);
                PonerCamara(0, u, 1.5f);
                tituloGrupo.alpha = Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((SegundosDeTitulo - t) / 0.5f);
                negro.color = new Color(0f, 0f, 0f, Mathf.Clamp01(1f - t / 0.6f));
                if (RevisarSalto()) yield break;
                yield return null;
            }
            tituloGrupo.alpha = 0f;

            // Los 6 pasos.
            for (int k = 0; k < TotalDePasos; k++)
            {
                Paso = k;
                ActualizarPanel(k);
                cartelTexto.text = $"<color=#FFD24D>PASO {k + 1}/{TotalDePasos} · {Titulos[k]}</color>\n<size=30>{Textos[k]}</size>";
                t = 0f;
                while (t < SegundosPorPaso)
                {
                    t += Time.deltaTime; Tiempo += Time.deltaTime;
                    float u = Mathf.Clamp01(t / SegundosPorPaso);
                    PonerCamara(k, u, 1f);
                    // Entra de negro (0,4 s) y el panel y el cartel aparecen con la toma.
                    negro.color = new Color(0f, 0f, 0f, Mathf.Clamp01(1f - t / 0.4f));
                    panelGrupo.alpha = Mathf.Clamp01(t / 0.4f + (k > 0 ? 1f : 0f));
                    cartelGrupo.alpha = Mathf.Clamp01(t / 0.5f);
                    if (RevisarSalto()) yield break;
                    yield return null;
                }
            }

            // Cierre: ultimo cuadro quieto y "presiona una tecla".
            Paso = TotalDePasos;
            ActualizarPanel(TotalDePasos);
            cartelGrupo.alpha = 0f;
            EsperandoTecla = true;
            saltarTexto.gameObject.SetActive(false); anillo.transform.parent.gameObject.SetActive(false);
            while (true)
            {
                Tiempo += Time.deltaTime;
                finalGrupo.alpha = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 4f);
                if (HayTecla()) { Terminar(); yield break; }
                yield return null;
            }
        }

        void PonerCamara(int k, float u, float rapidez)
        {
            if (camaraCinematica == null) return;
            PoseDeToma(k, u, out var pos, out var rot);
            camaraCinematica.SetPositionAndRotation(pos, rot);
        }

        void ActualizarPanel(int actual)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < TotalDePasos; i++)
            {
                if (i > 0) sb.Append('\n');
                string marca = i < actual ? "✓" : i == actual ? "▶" : "•";
                string color = i < actual ? "#5BE37A" : i == actual ? "#FFD24D" : "#8E98A3";
                sb.Append("<color=").Append(color).Append(">").Append(marca).Append("  ").Append(i + 1).Append("  ").Append(Titulos[i]).Append("</color>");
            }
            if (panelTexto != null) panelTexto.text = sb.ToString();
        }

        // Mantener [Espacio] 1 s salta toda la cinematica; true = ya termino.
        bool RevisarSalto()
        {
            bool espacio = PruebaMantenerEspacio || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);
            if (espacio) ProgresoDeSalto = Mathf.Min(1f, ProgresoDeSalto + Time.unscaledDeltaTime / SegundosParaSaltar);
            else ProgresoDeSalto = Mathf.Max(0f, ProgresoDeSalto - Time.unscaledDeltaTime * 2f);
            if (anillo != null) anillo.fillAmount = ProgresoDeSalto;
            if (ProgresoDeSalto >= 1f)
            {
                SaltadaPorElJugador = true;
                SonarSalteo();
                Terminar();
                return true;
            }
            return false;
        }

        static bool HayTecla()
        {
            if (PruebaTecla) { PruebaTecla = false; return true; }
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }

        static void SonarSalteo()
        {
            var cam = CamaraPrincipal.Actual;
            if (cam == null) return;
            AudioDirector.PlayClipAt(GenericSfx.GetWeaponShot(WeaponKind.Rifle), cam.transform.position + cam.transform.forward * 6f, 0.5f, 1f, PerfilEspacial.Disparo);
        }
    }
}
