using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace SP.UI
{
    // Menu RADIAL de ordenes por CAPAS: se despliega manteniendo [Q]. Un toque corto
    // de la misma tecla cicla de soldado como siempre.
    //
    //   Capa interior : 8 categorias (ir alli, cubrirse, atacar, posicion, curar,
    //                   tanque, poseer, demoler), en el sentido del reloj desde arriba.
    //   Capa exterior : al resaltar una categoria se abre, justo afuera de ella, un
    //                   abanico con sus opciones (todos / solo 1 / solo 2 / solo 3...).
    //
    // En FPS el mouse esta capturado apuntando el arma: mientras el radial esta abierto
    // la camara se congela y el movimiento del mouse mueve un cursor virtual. Se elige
    // la categoria apuntando en su direccion y se sigue hacia AFUERA para elegir la
    // opcion. Al SOLTAR [Q] se ejecuta lo resaltado; si el cursor esta en el centro (o
    // en una zona vacia del anillo exterior) se cancela. Los numeros 1..8 eligen una
    // categoria directo (con su primera opcion).
    public class MenuDeOrdenes : MonoBehaviour
    {
        public const int CantidadDeOpciones = 5;
        public const int CantidadDePorciones = 8;
        public const int MaxOpcionesPorCategoria = 5;

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

        // Nombre de cada categoria del anillo interior.
        public static readonly string[] Porciones =
        {
            "IR\nALLI",
            "CUBRIRSE\n(HACIA DONDE MIRO)",
            "ATACAR",
            "POSICION",
            "CURAR",
            "TANQUE",
            "POSEER",
            "DEMOLER",
        };

        // Opciones de cada categoria (anillo exterior). {1}{2}{3} = clase del soldado N.
        public static readonly string[][] OpcionesDe =
        {
            new[] { "TODOS", "SOLO {1}", "SOLO {2}", "SOLO {3}" },
            new[] { "TODOS", "SOLO {1}", "SOLO {2}", "SOLO {3}" },
            new[] { "TODOS", "SOLO {1}", "SOLO {2}", "SOLO {3}" },
            new[] { "TODOS QUIETOS", "SIGANME", "FORMAR LINEA", "FORMAR CUÑA", "RETIRADA" },
            new[] { "CURARME", "CURAR ALIADO", "CURAR AL APUNTADO" },
            new[] { "SUBIR TODOS", "BAJAR TODOS", "TANQUE ALLI", "SUBIRME YO", "BAJARME YO" },
            new[] { "SOLDADO {1}", "SOLDADO {2}", "SOLDADO {3}", "SIGUIENTE" },
            new[] { "ASALTO DEMUELE", "YO DEMUELO", "CANCELAR" },
        };

        static readonly Color[] Acentos =
        {
            new Color(0.4f, 0.92f, 0.5f),  new Color(1f, 0.86f, 0.3f),  new Color(1f, 0.35f, 0.3f),  new Color(0.3f, 0.85f, 1f),
            new Color(0.95f, 0.45f, 0.8f), new Color(1f, 0.66f, 0.25f), new Color(0.7f, 0.6f, 1f),   new Color(1f, 0.55f, 0.2f),
        };

        const float RadioInterior = 175f;     // borde exterior del anillo de categorias
        const float RadioExterior = 310f;     // borde exterior del abanico de opciones
        const float RadioMaximoCursor = 300f; // alcance del cursor virtual
        const float ZonaMuerta = 34f;         // por debajo no se elige nada
        const float PasoDeAbanico = 32f;      // grados entre opciones del abanico
        const float AnchoDeOpcion = 30f;

        CanvasGroup group;
        Text lista;
        Image[] rebanadas;
        Text[] etiquetas;
        Image[] opciones;
        Text[] etiquetasOpcion;
        Image cursor;
        Vector2 virtualPos;
        readonly string[] soldados = { "1", "2", "3" };
        static Sprite donaInterior, donaExterior;

        public bool Abierto { get; private set; }
        // Categoria resaltada (0..7) o -1 si el cursor esta en el centro.
        public int Seleccion { get; private set; } = -1;
        // Opcion resaltada dentro de la categoria (0..n-1) o -1 si el cursor sigue en el anillo interior.
        public int Sub { get; private set; } = -1;
        public bool EsRadial => rebanadas != null;
        public bool EnAnilloExterior => virtualPos.magnitude >= RadioInterior + 6f;

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
            // Un menu que sobrevive prendido a un domain reload o a una recarga de
            // escena taparia el HUD sin que nadie lo haya abierto.
            Cerrar();
        }

        void Escribir()
        {
            if (lista == null) return;
            lista.text = EsRadial ? "ORDENES" : "ORDENES\n" + string.Join("\n", Opciones);
        }

        // "Solo Asalto", "Solo Flanqueador"...: el driver pasa la clase de cada soldado.
        public void PonerSoldados(string s1, string s2, string s3)
        {
            soldados[0] = string.IsNullOrEmpty(s1) ? "1" : s1;
            soldados[1] = string.IsNullOrEmpty(s2) ? "2" : s2;
            soldados[2] = string.IsNullOrEmpty(s3) ? "3" : s3;
        }

        string Texto(string plantilla)
        {
            return plantilla.Replace("{1}", "1 · " + soldados[0]).Replace("{2}", "2 · " + soldados[1]).Replace("{3}", "3 · " + soldados[2]);
        }

        public static string NombreDeOpcion(int categoria, int sub)
        {
            if (categoria < 0 || categoria >= OpcionesDe.Length) return "";
            var o = OpcionesDe[categoria];
            return sub >= 0 && sub < o.Length ? o[sub] : "";
        }

        // El radial se achica si la pantalla es baja: el anillo exterior nunca sale del canvas.
        void AjustarEscala()
        {
            var padre = transform.parent as RectTransform;
            if (padre == null) return;
            float e = Mathf.Clamp(padre.rect.height * 0.92f / (RadioExterior * 2f), 0.4f, 1f);
            transform.localScale = Vector3.one * e;
        }

        public void Abrir()
        {
            AjustarEscala();
            Abierto = true;
            virtualPos = Vector2.zero;
            Seleccion = -1;
            Sub = -1;
            Refrescar();
            if (group != null) group.alpha = 1f;
        }

        // Cursor al centro sin cerrar: soltar Q ahora cancela (no ejecuta nada).
        public void CancelarSeleccion()
        {
            virtualPos = Vector2.zero;
            Seleccion = -1;
            Sub = -1;
            Refrescar();
        }

        public void Cerrar()
        {
            Abierto = false;
            Seleccion = -1;
            Sub = -1;
            virtualPos = Vector2.zero;
            if (group != null) group.alpha = 0f;
        }

        // Cursor virtual: acumula el movimiento del mouse (que en FPS esta capturado).
        public void MoverSeleccion(Vector2 delta)
        {
            if (!Abierto) return;
            virtualPos += delta * 1.5f;
            if (virtualPos.magnitude > RadioMaximoCursor) virtualPos = virtualPos.normalized * RadioMaximoCursor;
            Resolver();
        }

        static float AnguloDe(Vector2 v)
        {
            float ang = Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg;   // desde arriba, sentido del reloj
            return ang < 0f ? ang + 360f : ang;
        }

        void Resolver()
        {
            float r = virtualPos.magnitude;
            if (r < ZonaMuerta) { Seleccion = -1; Sub = -1; Refrescar(); return; }

            float ang = AnguloDe(virtualPos);
            if (r < RadioInterior + 6f || Seleccion < 0)
            {
                Seleccion = Mathf.RoundToInt(ang / 45f) % CantidadDePorciones;
                Sub = -1;
            }
            else
            {
                // Anillo exterior: la categoria queda fija y se elige la opcion por angulo.
                int n = OpcionesDe[Seleccion].Length;
                float centro = Seleccion * 45f;
                float d = Mathf.DeltaAngle(centro, ang);
                float pos = d / PasoDeAbanico + (n - 1) * 0.5f;
                int idx = Mathf.RoundToInt(pos);
                Sub = idx >= 0 && idx < n && Mathf.Abs(pos - idx) * PasoDeAbanico <= AnchoDeOpcion * 0.5f + 2f ? idx : -1;
            }
            Refrescar();
        }

        // Para probarlo y para elegir con los numeros: categoria 0..7 y, si se pide, su opcion.
        public void ElegirDirecto(int categoria, int sub = -1)
        {
            if (!Abierto) return;
            Seleccion = Mathf.Clamp(categoria, 0, CantidadDePorciones - 1);
            float a = Seleccion * 45f;
            if (sub < 0)
            {
                Sub = -1;
                float rad = a * Mathf.Deg2Rad;
                virtualPos = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * 120f;
            }
            else
            {
                int n = OpcionesDe[Seleccion].Length;
                Sub = Mathf.Clamp(sub, 0, n - 1);
                float rad = (a + (Sub - (n - 1) * 0.5f) * PasoDeAbanico) * Mathf.Deg2Rad;
                virtualPos = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * 245f;
            }
            Refrescar();
        }

        void Refrescar()
        {
            if (rebanadas == null) return;
            for (int i = 0; i < rebanadas.Length; i++)
            {
                bool sel = i == Seleccion;
                var c = Acentos[i];
                rebanadas[i].color = sel ? new Color(c.r, c.g, c.b, 0.96f) : new Color(0.07f, 0.1f, 0.14f, 0.88f);
                rebanadas[i].rectTransform.localScale = Vector3.one * (sel ? 1.04f : 1f);
                etiquetas[i].color = sel ? new Color(0.05f, 0.06f, 0.08f) : Color.white;
            }

            // Abanico de opciones de la categoria resaltada.
            int n = Seleccion >= 0 ? OpcionesDe[Seleccion].Length : 0;
            for (int j = 0; j < opciones.Length; j++)
            {
                bool visible = j < n;
                opciones[j].gameObject.SetActive(visible);
                etiquetasOpcion[j].gameObject.SetActive(visible);
                if (!visible) continue;
                float centro = Seleccion * 45f + (j - (n - 1) * 0.5f) * PasoDeAbanico;
                var c = Acentos[Seleccion];
                bool elegida = j == Sub;
                opciones[j].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -(centro - AnchoDeOpcion * 0.5f));
                opciones[j].color = elegida ? new Color(c.r, c.g, c.b, 0.97f) : new Color(0.09f, 0.12f, 0.17f, 0.9f);
                opciones[j].rectTransform.localScale = Vector3.one * (elegida ? 1.03f : 1f);
                float rad = centro * Mathf.Deg2Rad;
                etiquetasOpcion[j].rectTransform.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * (RadioExterior * 0.78f);
                etiquetasOpcion[j].text = Texto(OpcionesDe[Seleccion][j]).Replace(" · ", "\n");
                etiquetasOpcion[j].color = elegida ? new Color(0.05f, 0.06f, 0.08f) : Color.white;
            }

            if (cursor != null) cursor.rectTransform.anchoredPosition = virtualPos;
            if (lista != null)
            {
                if (Seleccion < 0) lista.text = "ORDENES\n<size=11>apunta con el mouse · suelta Q</size>";
                else if (Sub >= 0) lista.text = Porciones[Seleccion].Replace("\n", " ") + "\n<size=13>" + Texto(OpcionesDe[Seleccion][Sub]).Replace(" · ", " ") + "</size>";
                else lista.text = Porciones[Seleccion].Replace("\n", " ") + "\n<size=11>sigue hacia afuera</size>";
            }
        }

        // Construye el panel si la escena no lo trae y deja cableado el driver. Existe
        // porque SC_Gameplay NO la arma HeadlessTestRunner (esa solo escribe SC_TestLevel).
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
                bool bienPuesto = existente.EsRadial && existente.opciones != null && existente.transform.parent == raiz;
                if (bienPuesto || !Application.isPlaying)
                {
                    if (driver != null && driver.OrdenesMenu == null) driver.OrdenesMenu = existente;
                    return existente;
                }
                // Un panel viejo (lista de texto, radial de una capa) se reconstruye.
                Destroy(existente.gameObject);
            }

            var menu = Construir(raiz);
            if (driver != null) driver.OrdenesMenu = menu;
            return menu;
        }

        // Anillo de 256 px con borde suavizado; 'hueco' = radio interior / radio exterior.
        static Sprite Dona(float hueco)
        {
            bool interior = hueco < 0.5f;
            if (interior && donaInterior != null) return donaInterior;
            if (!interior && donaExterior != null) return donaExterior;
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            float R = n * 0.5f, r = R * hueco;
            var c = new Vector2(R, R);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float a = Mathf.Min(Mathf.Clamp01(R - d), Mathf.Clamp01(d - r));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sp.hideFlags = HideFlags.HideAndDontSave;
            if (interior) donaInterior = sp; else donaExterior = sp;
            return sp;
        }

        static Text NuevoTexto(Transform padre, string nombre, Font font, int tam, Vector2 caja)
        {
            var tGO = new GameObject(nombre, typeof(RectTransform), typeof(Text), typeof(Shadow));
            tGO.transform.SetParent(padre, false);
            var t = tGO.GetComponent<Text>();
            t.font = font; t.fontSize = tam; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var trt = t.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = caja;
            return t;
        }

        static Image NuevaRebanada(Transform padre, string nombre, Sprite sprite, float radio, float fill)
        {
            var sGO = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            sGO.transform.SetParent(padre, false);
            var img = sGO.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = true;
            img.fillAmount = fill;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(radio * 2f, radio * 2f);
            rt.anchoredPosition = Vector2.zero;
            return img;
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
            rt.sizeDelta = new Vector2(RadioExterior * 2f, RadioExterior * 2f);
            var cg = go.GetComponent<CanvasGroup>();
            cg.interactable = false; cg.blocksRaycasts = false;

            var menu = go.GetComponent<MenuDeOrdenes>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            menu.rebanadas = new Image[CantidadDePorciones];
            menu.etiquetas = new Text[CantidadDePorciones];
            float fill = 45f / 360f - 0.006f;
            var interior = Dona(0.36f);

            for (int i = 0; i < CantidadDePorciones; i++)
            {
                float centro = i * 45f;
                var img = NuevaRebanada(go.transform, "Categoria" + (i + 1), interior, RadioInterior, fill);
                // La rebanada arranca a las 12; se gira para centrarla en su angulo.
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -(centro - fill * 180f));
                menu.rebanadas[i] = img;

                var t = NuevoTexto(go.transform, "TextoCategoria" + (i + 1), font, 13, new Vector2(120f, 60f));
                t.text = $"<size=10>{i + 1}</size>\n{Porciones[i]}";
                float a = centro * Mathf.Deg2Rad;
                t.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (RadioInterior * 0.68f);
                menu.etiquetas[i] = t;
            }

            // Anillo exterior: un abanico reutilizable de hasta 5 opciones.
            menu.opciones = new Image[MaxOpcionesPorCategoria];
            menu.etiquetasOpcion = new Text[MaxOpcionesPorCategoria];
            var exterior = Dona(0.6f);
            float fillOp = AnchoDeOpcion / 360f;
            for (int j = 0; j < MaxOpcionesPorCategoria; j++)
            {
                menu.opciones[j] = NuevaRebanada(go.transform, "Opcion" + (j + 1), exterior, RadioExterior, fillOp);
                menu.etiquetasOpcion[j] = NuevoTexto(go.transform, "TextoOpcion" + (j + 1), font, 13, new Vector2(120f, 52f));
                menu.opciones[j].gameObject.SetActive(false);
                menu.etiquetasOpcion[j].gameObject.SetActive(false);
            }

            // Centro: nombre de la orden elegida.
            var ct = NuevoTexto(go.transform, "Centro", font, 14, new Vector2(112f, 80f));
            ct.color = Color.white;
            ct.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Punto del cursor virtual.
            var pGO = new GameObject("Cursor", typeof(RectTransform), typeof(Image));
            pGO.transform.SetParent(go.transform, false);
            menu.cursor = pGO.GetComponent<Image>();
            menu.cursor.sprite = interior;
            menu.cursor.color = Color.white;
            menu.cursor.raycastTarget = false;
            menu.cursor.rectTransform.sizeDelta = new Vector2(14f, 14f);

            menu.Bind(ct, cg);
            return menu;
        }

        // Devuelve 1..8, o 0 si este frame no se eligio nada. Estatico porque no depende
        // de la vista: la suite lo puede probar sin tener un canvas armado.
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
            if (kb.digit7Key.wasPressedThisFrame) return 7;
            if (kb.digit8Key.wasPressedThisFrame) return 8;
            return 0;
        }
    }
}
