using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace SP.UI
{
    // Menu RADIAL de ordenes rapidas: se despliega manteniendo [Q]. Un toque
    // corto de la misma tecla sigue ciclando de soldado como siempre.
    //
    // Seis porciones, en el sentido del reloj empezando por arriba:
    //
    //   1 SUBIR AL TANQUE      todos los aliados corren al tanque y se suben
    //   2 BAJAR TODOS          todos los ocupantes bajan del tanque
    //   3 ATACAR               todos atacan al enemigo apuntado (o al mas cercano)
    //   4 IR ALLI · TODOS      todos van al punto apuntado, en formacion
    //   5 IR ALLI · SOLO EL 2  solo el soldado 2 va al punto apuntado
    //   6 CUBRIRSE · TODOS     todos toman la cobertura apuntada
    //
    // En FPS el mouse esta capturado apuntando el arma: mientras el radial esta
    // abierto la camara se congela y el movimiento del mouse mueve un cursor
    // virtual dentro del radial (como en cualquier rueda de armas). Al SOLTAR
    // [Q] se ejecuta la porcion resaltada; si no se eligio ninguna, se cancela.
    // Los numeros 1..6 eligen directo.
    public class MenuDeOrdenes : MonoBehaviour
    {
        public const int CantidadDeOpciones = 5;
        public const int CantidadDePorciones = 6;

        // Lista historica (formaciones, sigueme, alto, curarme): la sigue usando
        // EjecutarOrdenDelMenu(1..5), que la suite ejerce sin teclado.
        public static readonly string[] Opciones =
        {
            "1  FORMACION EN LINEA",
            "2  FORMACION EN CUÑA",
            "3  SIGANME",
            "4  ALTO",
            "5  NECESITO CURARME",
        };

        // Las porciones del radial, en el orden en que ve el jugador y en el que
        // las interpreta PlayerInputDriver.EjecutarOrdenRadial.
        public static readonly string[] Porciones =
        {
            "SUBIR\nAL TANQUE",
            "BAJAR\nTODOS",
            "ATACAR",
            "IR ALLI\nTODOS",
            "IR ALLI\nSOLO EL 2",
            "CUBRIRSE\nTODOS ALLI",
        };

        static readonly Color[] Acentos =
        {
            new Color(1f, 0.66f, 0.25f), new Color(1f, 0.66f, 0.25f), new Color(1f, 0.35f, 0.3f),
            new Color(0.4f, 0.92f, 0.5f), new Color(0.3f, 0.85f, 1f), new Color(1f, 0.86f, 0.3f),
        };

        const float RadioExterno = 230f;
        const float RadioCursor = 90f;          // alcance del cursor virtual
        const float ZonaMuerta = 26f;           // por debajo no se elige nada

        CanvasGroup group;
        Text lista;
        Image[] rebanadas;
        Text[] etiquetas;
        Image cursor;
        Vector2 virtualPos;
        static Sprite donaCache;

        public bool Abierto { get; private set; }
        // Porcion resaltada (0..5) o -1 si el cursor esta en el centro.
        public int Seleccion { get; private set; } = -1;
        public bool EsRadial => rebanadas != null;

        public void Bind(Text texto, CanvasGroup canvasGroup)
        {
            lista = texto;
            group = canvasGroup;
            Escribir();
            Cerrar();
        }

        void OnEnable()
        {
            if (lista == null) lista = GetComponentInChildren<Text>(true);
            if (group == null) group = GetComponent<CanvasGroup>();
            Escribir();
            // Un menu que sobrevive prendido a un domain reload o a una
            // recarga de escena taparia el HUD sin que nadie lo haya
            // abierto: arranca siempre cerrado.
            Cerrar();
        }

        void Escribir()
        {
            if (lista == null) return;
            lista.text = EsRadial ? "ORDENES" : "ORDENES\n" + string.Join("\n", Opciones);
        }

        public void Abrir()
        {
            Abierto = true;
            virtualPos = Vector2.zero;
            Seleccion = -1;
            Refrescar();
            if (group != null) group.alpha = 1f;
        }

        public void Cerrar()
        {
            Abierto = false;
            Seleccion = -1;
            virtualPos = Vector2.zero;
            if (group != null) group.alpha = 0f;
        }

        // Cursor virtual: acumula el movimiento del mouse (que en FPS esta
        // capturado) y elige la porcion hacia la que apunta.
        public void MoverSeleccion(Vector2 delta)
        {
            if (!Abierto) return;
            virtualPos += new Vector2(delta.x, delta.y) * 1.4f;
            if (virtualPos.magnitude > RadioCursor) virtualPos = virtualPos.normalized * RadioCursor;
            ElegirPorAngulo();
        }

        void ElegirPorAngulo()
        {
            if (virtualPos.magnitude < ZonaMuerta) { Seleccion = -1; Refrescar(); return; }
            // Angulo en el sentido del reloj desde arriba.
            float ang = Mathf.Atan2(virtualPos.x, virtualPos.y) * Mathf.Rad2Deg;
            if (ang < 0f) ang += 360f;
            Seleccion = Mathf.RoundToInt(ang / 60f) % CantidadDePorciones;
            Refrescar();
        }

        // Para probarlo y para elegir con los numeros: 0..5.
        public void ElegirDirecto(int porcion)
        {
            if (!Abierto) return;
            Seleccion = Mathf.Clamp(porcion, 0, CantidadDePorciones - 1);
            float a = Seleccion * 60f * Mathf.Deg2Rad;
            virtualPos = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * RadioCursor;
            Refrescar();
        }

        void Refrescar()
        {
            if (rebanadas == null) return;
            for (int i = 0; i < rebanadas.Length; i++)
            {
                bool sel = i == Seleccion;
                var c = Acentos[i];
                rebanadas[i].color = sel ? new Color(c.r, c.g, c.b, 0.96f) : new Color(0.07f, 0.1f, 0.14f, 0.86f);
                rebanadas[i].rectTransform.localScale = Vector3.one * (sel ? 1.045f : 1f);
                etiquetas[i].color = sel ? new Color(0.05f, 0.06f, 0.08f) : Color.white;
            }
            if (cursor != null) cursor.rectTransform.anchoredPosition = virtualPos * (RadioExterno * 0.62f / RadioCursor);
            if (lista != null)
                lista.text = Seleccion >= 0 ? Porciones[Seleccion].Replace("\n", " ") : "ORDENES\n<size=11>mueve el mouse · suelta Q</size>";
        }

        // Construye el panel si la escena no lo trae y deja cableado el
        // driver. Existe porque SC_Gameplay NO la arma HeadlessTestRunner
        // (esa solo escribe SC_TestLevel): sin esto la funcionalidad
        // andaria en la escena de pruebas y no en el juego.
        public static MenuDeOrdenes AsegurarEnEscena()
        {
            var driver = Object.FindAnyObjectByType<SP.Player.PlayerInputDriver>();

            // El canvas del HUD (pantalla), NO los de barras de vida / etiquetas (mundo).
            Transform raiz = driver != null && driver.AimUiRef != null ? driver.AimUiRef.transform.parent : null;
            if (raiz == null)
                foreach (var c in Object.FindObjectsByType<Canvas>())
                    if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace) { raiz = c.transform; break; }
            if (raiz == null) return null;

            var existente = Object.FindAnyObjectByType<MenuDeOrdenes>(FindObjectsInactive.Include);
            if (existente != null)
            {
                bool bienPuesto = existente.EsRadial && existente.transform.parent == raiz;
                if (bienPuesto || !Application.isPlaying)
                {
                    if (driver != null && driver.OrdenesMenu == null) driver.OrdenesMenu = existente;
                    return existente;
                }
                // Un panel viejo (lista de texto) o colgado del canvas equivocado se reconstruye.
                Destroy(existente.gameObject);
            }

            var menu = Construir(raiz);
            if (driver != null) driver.OrdenesMenu = menu;
            return menu;
        }

        // Anillo grueso (dona) de 256 px con borde suavizado.
        static Sprite Dona()
        {
            if (donaCache != null) return donaCache;
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            float R = n * 0.5f, r = R * 0.36f;
            var c = new Vector2(R, R);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float a = Mathf.Min(Mathf.Clamp01(R - d), Mathf.Clamp01(d - r));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            donaCache = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            donaCache.hideFlags = HideFlags.HideAndDontSave;
            return donaCache;
        }

        // Radial al centro de la pantalla.
        public static MenuDeOrdenes Construir(Transform padre)
        {
            var go = new GameObject("MenuDeOrdenes", typeof(RectTransform), typeof(CanvasGroup), typeof(MenuDeOrdenes));
            go.transform.SetParent(padre, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(RadioExterno * 2f, RadioExterno * 2f);
            var cg = go.GetComponent<CanvasGroup>();
            cg.interactable = false; cg.blocksRaycasts = false;

            var menu = go.GetComponent<MenuDeOrdenes>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            menu.rebanadas = new Image[CantidadDePorciones];
            menu.etiquetas = new Text[CantidadDePorciones];
            const float fill = 1f / CantidadDePorciones - 0.012f;

            for (int i = 0; i < CantidadDePorciones; i++)
            {
                float centro = i * 60f;
                var sGO = new GameObject("Porcion" + (i + 1), typeof(RectTransform), typeof(Image));
                sGO.transform.SetParent(go.transform, false);
                var img = sGO.GetComponent<Image>();
                img.sprite = Dona();
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Top;
                img.fillClockwise = true;
                img.fillAmount = fill;
                img.raycastTarget = false;
                Estirar(img.rectTransform);
                // La rebanada arranca a las 12; se gira para centrarla en su angulo.
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -(centro - fill * 180f));
                menu.rebanadas[i] = img;

                var tGO = new GameObject("Texto" + (i + 1), typeof(RectTransform), typeof(Text), typeof(Shadow));
                tGO.transform.SetParent(go.transform, false);
                var t = tGO.GetComponent<Text>();
                t.font = font; t.fontSize = 17; t.fontStyle = FontStyle.Bold;
                t.alignment = TextAnchor.MiddleCenter;
                t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                t.text = $"<size=12>{i + 1}</size>\n{Porciones[i]}";
                var trt = t.rectTransform;
                trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
                trt.sizeDelta = new Vector2(150f, 90f);
                float a = centro * Mathf.Deg2Rad;
                trt.anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (RadioExterno * 0.68f);
                menu.etiquetas[i] = t;
            }

            // Centro: nombre de la orden elegida.
            var cGO = new GameObject("Centro", typeof(RectTransform), typeof(Text));
            cGO.transform.SetParent(go.transform, false);
            var ct = cGO.GetComponent<Text>();
            ct.font = font; ct.fontSize = 15; ct.fontStyle = FontStyle.Bold;
            ct.alignment = TextAnchor.MiddleCenter; ct.color = Color.white;
            ct.horizontalOverflow = HorizontalWrapMode.Wrap; ct.verticalOverflow = VerticalWrapMode.Overflow;
            ct.raycastTarget = false;
            var crt = ct.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(140f, 90f);

            // Punto del cursor virtual.
            var pGO = new GameObject("Cursor", typeof(RectTransform), typeof(Image));
            pGO.transform.SetParent(go.transform, false);
            menu.cursor = pGO.GetComponent<Image>();
            menu.cursor.sprite = Dona();
            menu.cursor.color = Color.white;
            menu.cursor.raycastTarget = false;
            menu.cursor.rectTransform.sizeDelta = new Vector2(14f, 14f);

            menu.Bind(ct, cg);
            return menu;
        }

        static void Estirar(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // Devuelve 1..6, o 0 si este frame no se eligio nada. Estatico
        // porque no depende de la vista: la suite lo puede probar sin
        // tener un canvas armado.
        public static int LeerTecla()
        {
            var kb = Keyboard.current;
            if (kb == null) return 0;
            if (kb.digit1Key.wasPressedThisFrame) return 1;
            if (kb.digit2Key.wasPressedThisFrame) return 2;
            if (kb.digit3Key.wasPressedThisFrame) return 3;
            if (kb.digit4Key.wasPressedThisFrame) return 4;
            if (kb.digit5Key.wasPressedThisFrame) return 5;
            if (kb.digit6Key.wasPressedThisFrame) return 6;
            return 0;
        }
    }
}
