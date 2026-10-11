using UnityEngine;
using UnityEngine.AI;
using SP.CameraSystem;
using SP.Player;
using SP.Presentation;
using UnityEngine.UI;

namespace SP.Operacion
{
    // Bug #105: guia de "que hago ahora" en las fases 1 a 4 (cuartel, muralla, centro de datos, patio/tanque). Une tres ayudas sobre el
    // mismo objetivo, que es el de la baliza activa del director:
    //   - una flecha 3D grande y pulsante flotando sobre el objetivo (se ve de lejos, escala con la distancia);
    //   - una ruta punteada AUTOMATICA en el piso por el NavMesh, desde el jugador hasta el objetivo (sin tener que mantener ninguna tecla);
    //   - la flecha plana a los pies del jugador (ObjectiveArrowIndicator, la misma de la Mision) apuntando al objetivo.
    // Solo en FPS, a pie y fuera de cinematicas; se apaga al estar encima del objetivo. Lo crea OperacionDirector.Start (nada en la escena).
    public class GuiaDeObjetivo : MonoBehaviour
    {
        public const float DistanciaParaOcultarRuta = 7f, DistanciaParaOcultarFlecha = 3f, AlturaFlecha = 5.5f, IntervaloDeRuta = 0.3f;
        static GuiaDeObjetivo instancia;
        public static GuiaDeObjetivo Instancia => instancia;

        // Pedido: "un rombo de objetivo claro que ya no sea la linea en el piso". La ruta punteada, la flecha plana a los pies y la
        // flecha 3D se sacaron: queda UN rombo amarillo con borde blanco sobre el objetivo y su distancia (pegado al borde si esta fuera de pantalla).
        RectTransform rombo;
        Image romboBorde, romboInterior;
        Text romboTexto;
        Canvas canvasRombo;
        float t;
        static Texture2D texPunteado;
        static Sprite spriteRombo;

        public bool Activa { get; private set; }
        public bool RutaActiva => false;
        public int PuntosDeRuta => 0;
        public bool FlechaVisible => rombo != null && rombo.gameObject.activeSelf;
        public bool RomboVisible => FlechaVisible;
        public string TextoDelRombo => romboTexto != null && FlechaVisible ? romboTexto.text : "";
        public Vector2 PosicionDelRomboEnPantalla => rombo != null ? (Vector2)rombo.position : Vector2.zero;
        public Vector3 PosicionDeLaFlecha => ObjetivoActual;
        public bool FlechaDelPiesVisible => false;
        public Vector3 ObjetivoActual { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { instancia = null; texPunteado = null; spriteRombo = null; }

        public static GuiaDeObjetivo Asegurar()
        {
            if (!Application.isPlaying) return null;
            if (instancia != null) return instancia;
            // Restos de una sesion anterior (el rombo vive en un canvas propio): se borran para que no queden rombos congelados en pantalla.
            foreach (var viejo in Resources.FindObjectsOfTypeAll<GameObject>())
                if (viejo != null && viejo.name == "GuiaDeObjetivo") Destroy(viejo);
            var go = new GameObject("GuiaDeObjetivo");   // sin DontSave: con esas banderas el editor lo deja huerfano al salir de Play y se acumulan rombos congelados
            instancia = go.AddComponent<GuiaDeObjetivo>();
            return instancia;
        }

        void Awake()
        {
            var cgo = new GameObject("RomboDeObjetivoCanvas", typeof(Canvas), typeof(CanvasScaler));
            cgo.transform.SetParent(transform, false);
            canvasRombo = cgo.GetComponent<Canvas>();
            canvasRombo.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasRombo.sortingOrder = 45;
            var cs = cgo.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            cs.matchWidthOrHeight = 0.5f;

            var r = new GameObject("Rombo", typeof(RectTransform));
            r.transform.SetParent(cgo.transform, false);
            rombo = (RectTransform)r.transform;
            rombo.anchorMin = rombo.anchorMax = Vector2.zero;
            rombo.pivot = new Vector2(0.5f, 0.5f);
            rombo.sizeDelta = new Vector2(64f, 64f);
            var sp = SpriteDelRombo();
            var b = new GameObject("Borde", typeof(RectTransform), typeof(Image));
            b.transform.SetParent(r.transform, false);
            romboBorde = b.GetComponent<Image>(); romboBorde.sprite = sp; romboBorde.color = Color.white; romboBorde.raycastTarget = false;
            var brt = romboBorde.rectTransform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            var inn = new GameObject("Interior", typeof(RectTransform), typeof(Image));
            inn.transform.SetParent(r.transform, false);
            romboInterior = inn.GetComponent<Image>(); romboInterior.sprite = sp; romboInterior.color = DiamondGizmo.ColorObjetivo; romboInterior.raycastTarget = false;
            var irt = romboInterior.rectTransform; irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f); irt.sizeDelta = new Vector2(44f, 44f);
            var tg = new GameObject("Distancia", typeof(RectTransform), typeof(Text), typeof(Outline));
            tg.transform.SetParent(r.transform, false);
            romboTexto = tg.GetComponent<Text>();
            romboTexto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            romboTexto.fontSize = 22; romboTexto.fontStyle = FontStyle.Bold; romboTexto.alignment = TextAnchor.UpperCenter;
            romboTexto.color = Color.white; romboTexto.raycastTarget = false;
            romboTexto.horizontalOverflow = HorizontalWrapMode.Overflow; romboTexto.verticalOverflow = VerticalWrapMode.Overflow;
            var trt = romboTexto.rectTransform; trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0f); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -2f); trt.sizeDelta = new Vector2(200f, 28f);
            var o = tg.GetComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.95f); o.effectDistance = new Vector2(2f, -2f);
            r.SetActive(false);
        }

        // Rombo lleno (el borde blanco y el interior amarillo comparten el sprite: solo cambia el tamano y el color).
        static Sprite SpriteDelRombo()
        {
            if (spriteRombo != null) return spriteRombo;
            const int n = 96;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = (Mathf.Abs(x + 0.5f - n * 0.5f) + Mathf.Abs(y + 0.5f - n * 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01((1f - d) * (n * 0.5f) * 0.7f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            spriteRombo = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            spriteRombo.hideFlags = HideFlags.HideAndDontSave;
            return spriteRombo;
        }

        void OnDestroy() { if (instancia == this) instancia = null; }

        // P7 (#114): la guia del mando tactico reusa la malla de la flecha y la textura del punteado.
        public static Mesh MallaFlecha() => MallaDeFlecha();
        public static Texture2D TexturaPunteado() => Punteado();

        static Texture2D Punteado()
        {
            if (texPunteado != null) return texPunteado;
            var tex = new Texture2D(8, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            for (int i = 0; i < 8; i++) tex.SetPixel(i, 0, i < 4 ? Color.white : new Color(1f, 1f, 1f, 0f));
            tex.Apply(false, true);
            return texPunteado = tex;
        }

        // Flecha 3D apuntando hacia abajo: cabeza de piramide de 4 caras y mastil cuadrado. Colores de vertice (arriba claro, abajo oscuro).
        static Mesh MallaDeFlecha()
        {
            var m = new Mesh { name = "FlechaGrande" };
            var v = new System.Collections.Generic.List<Vector3>();
            var c = new System.Collections.Generic.List<Color>();
            var tr = new System.Collections.Generic.List<int>();
            Color claro = new Color(1f, 1f, 1f, 1f), medio = new Color(0.82f, 0.82f, 0.82f, 1f), oscuro = new Color(0.62f, 0.62f, 0.62f, 1f);
            void Tri(Vector3 a, Vector3 b, Vector3 d, Color ca, Color cb, Color cd)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(d); c.Add(ca); c.Add(cb); c.Add(cd);
                tr.Add(i); tr.Add(i + 1); tr.Add(i + 2);
            }
            float h = 0.75f;
            Vector3 punta = Vector3.zero;
            var b0 = new Vector3(-h, 1f, -h); var b1 = new Vector3(-h, 1f, h); var b2 = new Vector3(h, 1f, h); var b3 = new Vector3(h, 1f, -h);
            Tri(punta, b0, b1, oscuro, medio, medio); Tri(punta, b1, b2, oscuro, medio, medio);
            Tri(punta, b2, b3, oscuro, medio, medio); Tri(punta, b3, b0, oscuro, medio, medio);
            Tri(b0, b2, b1, claro, claro, claro); Tri(b0, b3, b2, claro, claro, claro);
            float w = 0.3f;
            var s0 = new Vector3(-w, 1f, -w); var s1 = new Vector3(-w, 1f, w); var s2 = new Vector3(w, 1f, w); var s3 = new Vector3(w, 1f, -w);
            var t0 = new Vector3(-w, 2.4f, -w); var t1 = new Vector3(-w, 2.4f, w); var t2 = new Vector3(w, 2.4f, w); var t3 = new Vector3(w, 2.4f, -w);
            void Cara(Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color col) { Tri(a, b, d, col, col, col); Tri(a, d, e, col, col, col); }
            Cara(s0, t0, t1, s1, medio); Cara(s1, t1, t2, s2, oscuro); Cara(s2, t2, t3, s3, medio); Cara(s3, t3, t0, s0, oscuro);
            Cara(t0, t3, t2, t1, claro);
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(tr, 0);
            m.RecalculateBounds();
            return m;
        }

        void Update()
        {
            t += Time.deltaTime;
            var d = OperacionDirector.Instancia;
            bool ok = d != null && d.GuiaPermitida(out var objetivo0);
            var objetivo = Vector3.zero;
            if (ok) d.GuiaPermitida(out objetivo);
            var drv = PlayerInputDriver.Activo;
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            if (ok && yo != null && yo.Health != null && yo.Health.IsAlive)
            {
                var rig = CameraRig.Instance;
                if (drv.CurrentSeat.HasValue) ok = false;
                if (rig != null && rig.Mode != ControlMode.Fps) ok = false;
                if (CinematicaDeOperacion.IntroActiva) ok = false;
                if (SP.Core.SesionLog.DialogoAbierto) ok = false;
                if (ok) { Mostrar(yo.transform.position, objetivo); return; }
            }
            Apagar();
        }

        void Apagar()
        {
            Activa = false;
            if (rombo != null && rombo.gameObject.activeSelf) rombo.gameObject.SetActive(false);
        }

        void Mostrar(Vector3 yo, Vector3 objetivo)
        {
            Activa = true;
            ObjetivoActual = objetivo;
            var plano = objetivo - yo; plano.y = 0f;
            float dist = plano.magnitude;
            var cam = SP.Core.CamaraPrincipal.Actual;
            bool ver = dist > DistanciaParaOcultarFlecha && cam != null;
            if (rombo.gameObject.activeSelf != ver) rombo.gameObject.SetActive(ver);
            if (!ver) return;

            // Sobre el objetivo (a la altura de la flecha de antes); fuera de pantalla o detras de la camara: pegado al borde.
            var punto = new Vector3(objetivo.x, Mathf.Max(objetivo.y, 0f) + AlturaFlecha * 0.6f, objetivo.z);
            var vp = cam.WorldToScreenPoint(punto);
            float w = Screen.width, h = Screen.height, margen = 70f;
            if (vp.z < 0f) { vp.x = w - vp.x; vp.y = h - vp.y; vp.x = vp.x < w * 0.5f ? margen : w - margen; vp.y = Mathf.Clamp(vp.y, margen, h - margen); }
            vp.x = Mathf.Clamp(vp.x, margen, w - margen);
            vp.y = Mathf.Clamp(vp.y, margen + 60f, h - margen);
            if (vp.x > w * 0.80f && vp.y > h * 0.66f) vp.y = h * 0.66f;   // no tapar el minimapa (arriba a la derecha)
            rombo.position = new Vector2(vp.x, vp.y);
            float pulso = 1f + 0.1f * Mathf.Sin(t * 5f);
            rombo.localScale = Vector3.one * pulso;
            romboTexto.text = Mathf.RoundToInt(dist) + " m";
            romboInterior.color = Color.Lerp(DiamondGizmo.ColorObjetivo, new Color(1f, 0.6f, 0.1f), 0.5f + 0.5f * Mathf.Sin(t * 5f));
        }
    }
}
