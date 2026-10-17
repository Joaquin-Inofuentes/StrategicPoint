using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace SP.UI
{
    // Contexto del radial: que categorias y opciones tienen sentido AHORA segun lo que se apunta.
    // Las contextuales (curar al herido apuntado, demoler el muro apuntado, subir al tanque
    // apuntado, usar la torreta apuntada...) van arriba y con otro color; el resto solo se ofrece
    // si hay algo que hacer. Sin contexto (null) el radial ofrece todas, como antes.
    public class ContextoRadial
    {
        public readonly bool[] Visible = new bool[MenuDeOrdenes.CantidadDeCategorias];
        public readonly bool[] Contextual = new bool[MenuDeOrdenes.CantidadDeCategorias];
        public readonly bool[][] OpcionVisible = new bool[MenuDeOrdenes.CantidadDeCategorias][];
        // "Apuntando a: Alfa (herido 40/100)": lo que el jugador tiene en la mira, para el centro.
        public string Apuntando = "";

        public ContextoRadial()
        {
            for (int c = 0; c < MenuDeOrdenes.CantidadDeCategorias; c++)
            {
                OpcionVisible[c] = new bool[MenuDeOrdenes.OpcionesDe[c].Length];
                for (int o = 0; o < OpcionVisible[c].Length; o++) OpcionVisible[c][o] = true;
            }
        }

        public void Mostrar(int cat, bool contextual, params int[] opciones)
        {
            Visible[cat] = true;
            Contextual[cat] = contextual;
            if (opciones == null || opciones.Length == 0) return;
            for (int o = 0; o < OpcionVisible[cat].Length; o++) OpcionVisible[cat][o] = System.Array.IndexOf(opciones, o) >= 0;
        }
    }

    // Menu RADIAL de ordenes por CAPAS: se despliega manteniendo [Q]. Un toque corto
    // de la misma tecla cicla de soldado como siempre.
    //
    //   Capa interior : las categorias que valen AHORA. Siempre estan IR ALLI, CUBRIRSE y
    //                   POSICION; el resto (CURAR, DEMOLER, TANQUE, TORRETA, ATACAR, POSEER)
    //                   solo aparece si se apunta a algo con lo que se pueda interactuar, y esas
    //                   van ARRIBA y en dorado.
    //   Capa exterior : al resaltar una categoria se abre, justo afuera de ella, un
    //                   abanico con sus opciones (todos / solo 1 / solo 2 / solo 3...).
    //
    // En FPS el mouse esta capturado apuntando el arma: mientras el radial esta abierto
    // la camara se congela y el movimiento del mouse mueve un cursor virtual. Se elige
    // la categoria apuntando en su direccion y se sigue hacia AFUERA para elegir la
    // opcion. Al SOLTAR [Q] se ejecuta lo resaltado; si el cursor esta en el centro (o
    // en una zona vacia del anillo exterior) se cancela. Los numeros 1..N eligen una
    // categoria directo (con su primera opcion).
    public class MenuDeOrdenes : MonoBehaviour
    {
        // Unico de la escena: se registra al activarse en vez de que cada consumidor lo busque con un barrido.
        public static MenuDeOrdenes Activo { get; private set; }
        public static void ReiniciarActivo() => Activo = null;
        public void RegistrarActivo()
        {
            Activo = this;
        }
        public const int CantidadDeOpciones = 5;
        public const int CantidadDeCategorias = 10;
        public const int CantidadDePorciones = CantidadDeCategorias;
        public const int MaxOpcionesPorCategoria = 6;
        // Item 61: ATACAR tiene, ademas de "a quien ordeno" (0..3), dos ordenes tacticas.
        public const int SubSuprimir = 4, SubGranada = 5;

        // Identificadores estables de categoria (el orden en pantalla cambia con el contexto).
        public const int IrAlli = 0, Cubrirse = 1, Atacar = 2, Posicion = 3, Curar = 4, Tanque = 5, Poseer = 6, Demoler = 7, Torreta = 8, Interactuar = 9;

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

        // Nombre de cada categoria.
        public static readonly string[] Porciones =
        {
            "IR\nALLI",
            "CUBRIRSE\n(HACIA DONDE MIRO)",
            "ATACAR\nAL APUNTADO",
            "POSICION",
            "CURAR",
            "TANQUE",
            "POSEER",
            "DEMOLER",
            "TORRETA\nFIJA",
            "INTERACTUAR",
        };

        // Opciones de cada categoria (anillo exterior). {1}{2}{3} = clase del soldado N.
        public static readonly string[][] OpcionesDe =
        {
            new[] { "TODOS", "SOLO {1}", "SOLO {2}", "SOLO {3}" },
            new[] { "TODOS", "SOLO {1}", "SOLO {2}", "SOLO {3}" },
            new[] { "TODOS", "SOLO {1}", "SOLO {2}", "SOLO {3}", "TODOS SUPRIMEN", "GRANADA ALLI" },
            new[] { "TODOS QUIETOS", "SIGANME", "FORMAR LINEA", "FORMAR CUÑA", "RETIRADA" },
            new[] { "CURARME", "AL MAS HERIDO", "CURAR A ESTE", "REVIVIR A ESTE" },
            new[] { "SUBIR TODOS", "BAJAR TODOS", "TANQUE ALLI", "SUBIRME YO", "BAJARME YO" },
            new[] { "SOLDADO {1}", "SOLDADO {2}", "SOLDADO {3}", "SIGUIENTE", "POSEER A ESTE" },
            new[] { "ASALTO DEMUELE", "YO DEMUELO", "CANCELAR" },
            new[] { "USAR LA TORRETA", "SALIR DE LA TORRETA", "QUE LO MONTEN" },
            new[] { "EL INDICADO VA", "SOLO {1}", "SOLO {2}", "SOLO {3}" },
        };

        static readonly Color[] Acentos =
        {
            new Color(0.4f, 0.92f, 0.5f),  new Color(1f, 0.86f, 0.3f),  new Color(1f, 0.35f, 0.3f),  new Color(0.3f, 0.85f, 1f),
            new Color(0.95f, 0.45f, 0.8f), new Color(1f, 0.66f, 0.25f), new Color(0.7f, 0.6f, 1f),   new Color(1f, 0.55f, 0.2f),
            new Color(0.5f, 0.9f, 0.85f),
            new Color(0.4f, 1f, 0.7f),
        };

        // Las categorias contextuales (lo que se apunta) se pintan de este dorado.
        static readonly Color Dorado = new Color(1f, 0.82f, 0.18f);
        static readonly Color DoradoOscuro = new Color(0.32f, 0.25f, 0.03f, 0.95f);

        // Pedido explicito: "para el radial, para todas las opciones, quiero
        // texto chico e iconos grandes, descriptivos y claros" -- el texto
        // baja (15 -> 11, ahora que cada porcion ya tiene un icono grande
        // que dice de que se trata) y el icono de categoria ocupa la mayor
        // parte de la porcion (ver TamanoIconoCategoria en Construir()).
        public const int TamanoLetra = 11;
        public const float OpacidadDeFondo = 0.78f;
        const float RadioInterior = 175f;     // borde exterior del anillo de categorias
        const float RadioExterior = 310f;     // borde exterior del abanico de opciones
        // Ubicacion de iconos y etiquetas: el icono va en el MEDIO de la banda del anillo (el hueco del centro mide 0,36 del radio
        // interior y 0,6 del exterior) con la etiqueta chica de una linea justo DEBAJO, como una unidad; antes el icono quedaba
        // pegado al centro (pisaba el texto "ORDENES") y la etiqueta flotaba afuera, tapando el abanico.
        const float RadioIconoCategoria = RadioInterior * 0.68f;
        const float SubidaDelIcono = 9f, BajadaDeLaEtiqueta = 20f;
        const float RadioIconoOpcion = RadioExterior * 0.80f;
        const float SubidaDelIconoOpcion = 10f, BajadaDeLaEtiquetaOpcion = 18f;
        const float TamanoIconoCategoria = 58f; // lado maximo del icono; Refrescar() lo achica si no entra
        const float TamanoIconoOpcion = 50f;
        const float RadioMaximoCursor = 300f; // alcance del cursor virtual
        const float ZonaMuerta = 34f;         // por debajo no se elige nada
        const float PasoDeAbanico = 32f;      // grados entre opciones del abanico
        const float AnchoDeOpcion = 30f;

        CanvasGroup group;
        Text lista;
        Image[] rebanadas;
        Image[] iconosCategoria;
        Text[] etiquetas;
        Image[] opciones;
        Text[] etiquetasOpcion;
        Image[] iconosOpcion;
        Image cursor;
        Vector2 virtualPos;
        readonly string[] soldados = { "1", "2", "3" };
        readonly bool[] soldadosMuertos = new bool[3];
        static Sprite donaInterior, donaExterior, donaFina;

        // Distribucion actual: ids de categoria en orden de pantalla (las contextuales primero).
        readonly List<int> ids = new List<int>();
        readonly List<int> opcionesActuales = new List<int>();   // sub reales visibles de la categoria resaltada
        ContextoRadial contexto;
        float paso = 120f, desfase;

        // PISTA DE TUTORIAL: categoria/opcion que hay que elegir. Se dibuja latiendo en celeste.
        public static int PistaCategoria = -1, PistaOpcion = -1;
        public static void PonerPista(int categoria, int opcion = -1) { PistaCategoria = categoria; PistaOpcion = opcion; }

        // Pedido: "reducir el circulo de Q a la mitad, con animacion y mas efectos; eliminar los textos". El radial se dibuja a la mitad
        // (EscalaPedida), sin etiquetas (solo iconos y color) y con: apertura con rebote, onda al abrir, escalonado de las porciones,
        // anillo exterior giratorio, brillo de la porcion elegida y un "pop" cada vez que el cursor cambia de opcion.
        public const float EscalaPedida = 0.5f;
        public const float SegundosDeApertura = 0.24f, SegundosDeCierre = 0.12f, SegundosDeOnda = 0.45f, SegundosDePop = 0.16f;
        float aperturaT = 1f, cierreT = 1f, ondaT = 1f, popT = 1f, escalaBase = 1f;
        bool cerrando;
        Image brillo, anilloGiratorio, anilloInterno, onda;
        public float Apertura01 => aperturaT;
        public bool Cerrandose => cerrando;

        static float RebotePositivo(float t)
        {
            // easeOutBack: sube pasandose un poco y se asienta
            const float c1 = 1.70158f, c3 = c1 + 1f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (Abierto)
            {
                aperturaT = Mathf.Min(1f, aperturaT + dt / SegundosDeApertura);
                ondaT = Mathf.Min(1f, ondaT + dt / SegundosDeOnda);
                popT = Mathf.Min(1f, popT + dt / SegundosDePop);
                AplicarAnimacion();
                // Mientras hay algo animandose (o una pista de tutorial latiendo) se redibuja cada cuadro; si no, solo cuando se mueve el cursor.
                if (PistaCategoria >= 0 || aperturaT < 1f || popT < 1f || Seleccion >= 0) Refrescar();
            }
            else if (cerrando)
            {
                cierreT = Mathf.Min(1f, cierreT + dt / SegundosDeCierre);
                AplicarAnimacion();
                if (cierreT >= 1f) { cerrando = false; if (group != null) group.alpha = 0f; }
            }
        }

        // Escala, transparencia y giro del conjunto. Se llama cada cuadro mientras el radial esta animandose o abierto.
        void AplicarAnimacion()
        {
            float k;
            if (Abierto) k = RebotePositivo(aperturaT);
            else k = Mathf.Lerp(1f, 0.82f, cierreT);
            transform.localScale = Vector3.one * escalaBase * Mathf.Lerp(0.55f, 1f, Mathf.Clamp(k, 0f, 1.12f));
            if (group != null) group.alpha = Abierto ? Mathf.Clamp01(aperturaT * 3f) : 1f - cierreT;
            if (anilloGiratorio != null) anilloGiratorio.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 22f);
            if (anilloInterno != null) anilloInterno.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 38f);
            if (onda != null)
            {
                bool viva = Abierto && ondaT < 1f;
                onda.gameObject.SetActive(viva);
                if (viva)
                {
                    float e = 1f - (1f - ondaT) * (1f - ondaT);
                    float lado = RadioExterior * 2f * Mathf.Lerp(0.55f, 1.35f, e);
                    onda.rectTransform.sizeDelta = new Vector2(lado, lado);
                    var cc = onda.color; cc.a = 0.55f * (1f - ondaT); onda.color = cc;
                }
            }
        }

        // Efectos que se suman al radial (idempotente: el radial guardado en la escena puede venir de antes de que existieran).
        void AsegurarEfectos()
        {
            if (anilloGiratorio != null && brillo != null) return;
            var exterior = Dona(0.6f);
            var fino = Dona(0.95f);
            Image Nuevo(string nombre, Sprite sp, float lado, Color c, bool lleno)
            {
                var existente = SP.Core.BuscarHijo.Ruta(transform, nombre);
                GameObject g = existente != null ? existente.gameObject : new GameObject(nombre, typeof(RectTransform), typeof(Image));
                g.transform.SetParent(transform, false);
                var img = g.GetComponent<Image>();
                img.sprite = sp; img.color = c; img.raycastTarget = false;
                if (lleno) { img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Radial360; img.fillOrigin = (int)Image.Origin360.Top; img.fillClockwise = true; }
                var r = img.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = Vector2.zero;
                r.sizeDelta = new Vector2(lado, lado);
                return img;
            }
            // Detras de todo, en este orden: onda, anillo giratorio, anillo interno, brillo de la seleccion.
            onda = Nuevo("EfectoOnda", fino, RadioExterior * 2f, new Color(1f, 1f, 1f, 0f), false);
            anilloGiratorio = Nuevo("EfectoAnilloExterior", fino, (RadioExterior + 22f) * 2f, new Color(0.55f, 0.85f, 1f, 0.35f), false);
            anilloInterno = Nuevo("EfectoAnilloInterior", fino, (RadioInterior + 10f) * 2f, new Color(1f, 1f, 1f, 0.16f), false);
            brillo = Nuevo("EfectoBrillo", Dona(0.36f), RadioInterior * 2f * 1.16f, new Color(1f, 1f, 1f, 0f), true);
            // Los dibujos del anillo exterior sin relleno: dientes (se parte en tramos con un patron de fill para que parezca dentado).
            anilloGiratorio.type = Image.Type.Simple;
            onda.transform.SetAsFirstSibling();
            anilloGiratorio.transform.SetSiblingIndex(1);
            anilloInterno.transform.SetSiblingIndex(2);
            brillo.transform.SetSiblingIndex(3);
            onda.gameObject.SetActive(false);
        }

        public bool Abierto { get; private set; }
        // Categoria resaltada (id 0..8) o -1 si el cursor esta en el centro.
        public int Seleccion { get; private set; } = -1;
        // Opcion resaltada dentro de la categoria (indice REAL de OpcionesDe) o -1 si el cursor sigue en el anillo interior.
        public int Sub { get; private set; } = -1;
        public bool EsRadial => rebanadas != null;
        public bool EnAnilloExterior => virtualPos.magnitude >= RadioInterior + 6f;
        public IReadOnlyList<int> CategoriasVisibles => ids;
        public bool EsVisible(int categoria) => ids.Contains(categoria);
        public bool EsContextual(int categoria) => contexto != null && categoria >= 0 && categoria < CantidadDeCategorias && contexto.Contextual[categoria];
        public IReadOnlyList<int> OpcionesVisibles => opcionesActuales;

        public void Bind(Text texto, CanvasGroup canvasGroup)
        {
            lista = texto;
            group = canvasGroup;
            Escribir();
            Cerrar();
        }

        void OnDisable() { if (Activo == this) Activo = null; }
        void OnEnable()
        {
            RegistrarActivo();
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
        public void PonerSoldados(string s1, string s2, string s3, bool m1 = false, bool m2 = false, bool m3 = false)
        {
            soldados[0] = string.IsNullOrEmpty(s1) ? "1" : s1;
            soldados[1] = string.IsNullOrEmpty(s2) ? "2" : s2;
            soldados[2] = string.IsNullOrEmpty(s3) ? "3" : s3;
            soldadosMuertos[0] = m1;
            soldadosMuertos[1] = m2;
            soldadosMuertos[2] = m3;
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

        // Nombre de una sola linea para la etiqueta del anillo interior ("CUBRIRSE (HACIA DONDE MIRO)" -> "CUBRIRSE"; el detalle
        // de lo apuntado ya lo muestra el texto del centro).
        public static string NombreCorto(int categoria)
        {
            if (categoria < 0 || categoria >= Porciones.Length) return "";
            string s = Porciones[categoria];
            int p = s.IndexOf('(');
            if (p > 0) s = s.Substring(0, p);
            return s.Replace("AL APUNTADO", "").Replace("\n", " ").Trim();
        }

        // Parte una etiqueta larga en dos lineas por el espacio mas cercano al medio (las que ya traen salto no se tocan).
        public static string Partir(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Contains("\n") || s.Length <= 9) return s;
            int mejor = -1, medio = s.Length / 2;
            for (int i = 0; i < s.Length; i++)
                if (s[i] == ' ' && (mejor < 0 || Mathf.Abs(i - medio) < Mathf.Abs(mejor - medio))) mejor = i;
            return mejor < 0 ? s : s.Substring(0, mejor) + "\n" + s.Substring(mejor + 1);
        }

        // El radial se achica si la pantalla es baja: el anillo exterior nunca sale del canvas.
        void AjustarEscala()
        {
            var padre = transform.parent as RectTransform;
            if (padre == null) return;
            float e = Mathf.Clamp(padre.rect.height * 0.92f / (RadioExterior * 2f), 0.4f, 1f);
            escalaBase = e * EscalaPedida;
            transform.localScale = Vector3.one * escalaBase;
        }

        // Ordena las categorias visibles: contextuales primero (arriba, centradas en las 12), luego el resto.
        void Distribuir(ContextoRadial ctx)
        {
            contexto = ctx;
            ids.Clear();
            if (ctx == null)
            {
                for (int c = 0; c < CantidadDeCategorias; c++) if (c != Torreta) ids.Add(c);
            }
            else
            {
                for (int c = 0; c < CantidadDeCategorias; c++) if (ctx.Visible[c] && ctx.Contextual[c]) ids.Add(c);
                for (int c = 0; c < CantidadDeCategorias; c++) if (ctx.Visible[c] && !ctx.Contextual[c]) ids.Add(c);
            }
            if (ids.Count == 0) ids.Add(IrAlli);
            int nCtx = 0;
            foreach (int c in ids) if (EsContextual(c)) nCtx++;
            paso = 360f / ids.Count;
            desfase = nCtx > 0 ? -(nCtx - 1) * 0.5f * paso : 0f;
        }

        float AnguloDeSlot(int slot) => desfase + slot * paso;

        public void Abrir() => Abrir(null);

        // Ultima categoria/opcion sobre la que sono el "tic" de hover (para sonar solo al CAMBIAR).
        int hoverCat = -1, hoverSub = -1;

        public void Abrir(ContextoRadial ctx)
        {
            AjustarEscala();
            AsegurarDiscoDelCentro();
            AsegurarEfectos();
            Distribuir(ctx);
            Abierto = true;
            cerrando = false;
            aperturaT = 0f; ondaT = 0f; popT = 1f;
            virtualPos = Vector2.zero;
            Seleccion = -1;
            Sub = -1;
            hoverCat = -1; hoverSub = -1;
            opcionesActuales.Clear();
            Refrescar();
            AplicarAnimacion();
            if (group != null) group.alpha = 0.01f;
            SP.Presentation.AudioDirector.PlayUi2D(SP.Presentation.SfxKind.RadialOpen, 0.6f, 0.9f);

            // BUG REAL ("no reconoce el atacar al enemigo con Q"): cuando lo apuntado
            // ofrece UNA sola categoria contextual (p.ej. ATACAR sobre un enemigo), el
            // jugador espera que apuntar + mantener Q + soltar sin mover el mouse ya
            // alcance para confirmarla -- es la unica accion posible sobre eso. Antes
            // el cursor virtual arrancaba en el centro (Seleccion = -1) y soltar ahi
            // siempre cancelaba, asi que habia que mover el mouse a ciegas hasta la
            // porcion dorada antes de soltar. Ahora, si hay una unica contextual, arranca
            // preseleccionada (como si el cursor ya estuviera sobre ella); mover el mouse
            // sigue permitiendo elegir otra categoria u opcion, o cancelar yendo al centro.
            int unicaContextual = -1;
            if (ctx != null)
                foreach (int c in ids)
                    if (ctx.Contextual[c]) { unicaContextual = unicaContextual < 0 ? c : -2; }
            if (unicaContextual >= 0) ElegirDirecto(unicaContextual);
        }

        // Un "tic" cada vez que el cursor pasa a otra categoria u otra opcion: se OYE que el menu responde.
        void SonarHover()
        {
            if (!Abierto) return;
            if (Seleccion == hoverCat && Sub == hoverSub) return;
            bool hayAlgo = Seleccion >= 0;
            hoverCat = Seleccion; hoverSub = Sub;
            popT = 0f;
            if (hayAlgo) SP.Presentation.AudioDirector.PlayUi2D(SP.Presentation.SfxKind.RadialTick, Sub >= 0 ? 0.55f : 0.4f, 0.5f);
        }

        // Cursor al centro sin cerrar: soltar Q ahora cancela (no ejecuta nada).
        public void CancelarSeleccion()
        {
            virtualPos = Vector2.zero;
            Seleccion = -1;
            Sub = -1;
            opcionesActuales.Clear();
            Refrescar();
        }

        public void Cerrar()
        {
            bool estabaAbierto = Abierto;
            Abierto = false;
            Seleccion = -1;
            Sub = -1;
            virtualPos = Vector2.zero;
            // Cierre animado (encoge y se desvanece); si ya estaba cerrado (OnEnable, Bind) queda cerrado de una.
            if (estabaAbierto && Application.isPlaying) { cerrando = true; cierreT = 0f; }
            else { cerrando = false; if (group != null) group.alpha = 0f; }
        }

        // Cursor virtual: acumula el movimiento del mouse (que en FPS esta capturado).
        public void MoverSeleccion(Vector2 delta)
        {
            if (!Abierto) return;
            // El radial se dibuja a la mitad: sin compensar, el cursor viajaria la mitad de rapido sobre la pantalla.
            virtualPos += delta * 1.5f / Mathf.Max(0.25f, escalaBase);
            if (virtualPos.magnitude > RadioMaximoCursor) virtualPos = virtualPos.normalized * RadioMaximoCursor;
            Resolver();
        }

        static float AnguloDe(Vector2 v)
        {
            float ang = Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg;   // desde arriba, sentido del reloj
            return ang < 0f ? ang + 360f : ang;
        }

        void CargarOpciones(int cat)
        {
            opcionesActuales.Clear();
            if (cat < 0) return;
            for (int o = 0; o < OpcionesDe[cat].Length; o++)
                if (contexto == null || contexto.OpcionVisible[cat][o]) opcionesActuales.Add(o);
        }

        void Resolver()
        {
            float r = virtualPos.magnitude;
            if (r < ZonaMuerta) { Seleccion = -1; Sub = -1; opcionesActuales.Clear(); Refrescar(); return; }

            float ang = AnguloDe(virtualPos);
            if (r < RadioInterior + 6f || Seleccion < 0)
            {
                float rel = Mathf.Repeat(ang - desfase, 360f);
                int slot = Mathf.RoundToInt(rel / paso) % ids.Count;
                Seleccion = ids[slot];
                CargarOpciones(Seleccion);
                Sub = -1;
            }
            else
            {
                // Anillo exterior: la categoria queda fija y se elige la opcion por angulo.
                int n = opcionesActuales.Count;
                float centro = AnguloDeSlot(ids.IndexOf(Seleccion));
                float d = Mathf.DeltaAngle(centro, ang);
                float pos = d / PasoDeAbanico + (n - 1) * 0.5f;
                int idx = Mathf.RoundToInt(pos);
                Sub = idx >= 0 && idx < n && Mathf.Abs(pos - idx) * PasoDeAbanico <= AnchoDeOpcion * 0.5f + 2f ? opcionesActuales[idx] : -1;
                if (Sub >= 0 && Seleccion == Poseer && Sub < 3 && soldadosMuertos[Sub]) Sub = -1;
            }
            Refrescar();
            SonarHover();
        }

        // Para probarlo y para elegir con los numeros: categoria (id) y, si se pide, su opcion (real).
        public void ElegirDirecto(int categoria, int sub = -1)
        {
            if (!Abierto || !ids.Contains(categoria)) return;
            Seleccion = categoria;
            CargarOpciones(Seleccion);
            float a = AnguloDeSlot(ids.IndexOf(Seleccion));
            if (sub < 0 || !opcionesActuales.Contains(sub))
            {
                Sub = -1;
                float rad = a * Mathf.Deg2Rad;
                virtualPos = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * 120f;
            }
            else
            {
                Sub = sub;
                if (Sub >= 0 && Seleccion == Poseer && Sub < 3 && soldadosMuertos[Sub])
                {
                    Sub = -1;
                    float rad = a * Mathf.Deg2Rad;
                    virtualPos = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * 120f;
                }
                else
                {
                    int n = opcionesActuales.Count;
                    int idx = opcionesActuales.IndexOf(sub);
                    float rad = (a + (idx - (n - 1) * 0.5f) * PasoDeAbanico) * Mathf.Deg2Rad;
                    virtualPos = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * 245f;
                }
            }
            Refrescar();
            SonarHover();
        }

        // Categoria (id) de la tecla numerica 1..N que se apreto, o -1.
        public int CategoriaDeTecla(int tecla) => tecla >= 1 && tecla <= ids.Count ? ids[tecla - 1] : -1;

        void Refrescar()
        {
            if (rebanadas == null) return;
            float relleno = paso / 360f - 0.006f;
            // BUG REAL (reportado: "en el radial los iconos... estan solapados"): el icono medía
            // siempre TamanoIconoCategoria (58 px) sin importar cuantas categorias hubiera activas.
            // La distancia real entre dos iconos vecinos es la cuerda del circulo que forman
            // (2 * radio * sin(paso/2)); con 8-9 categorias esa cuerda da ~53-59 px, MENOR que el
            // icono de 58 px -> se pisan. Se achica el icono para que siempre quede holgado.
            float cuerda = 2f * RadioIconoCategoria * Mathf.Sin(paso * 0.5f * Mathf.Deg2Rad);
            float tamanoIcono = Mathf.Clamp(cuerda * 0.5f, 38f, TamanoIconoCategoria);
            for (int i = 0; i < rebanadas.Length; i++)
            {
                bool activa = i < ids.Count;
                rebanadas[i].gameObject.SetActive(activa);
                etiquetas[i].gameObject.SetActive(false);   // sin textos: el radial se lee por icono y color
                if (!activa) continue;

                int cat = ids[i];
                bool ctx = EsContextual(cat);
                bool sel = cat == Seleccion;
                var c = ctx ? Dorado : Acentos[cat];
                rebanadas[i].fillAmount = relleno;
                float centro = AnguloDeSlot(i);
                rebanadas[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -(centro - relleno * 180f));
                rebanadas[i].color = sel ? new Color(c.r, c.g, c.b, 0.96f)
                    : ctx ? DoradoOscuro : new Color(0.07f, 0.1f, 0.14f, OpacidadDeFondo);
                bool esPista = cat == PistaCategoria && !sel;
                if (esPista)
                {
                    float latido = (Mathf.Sin(Time.unscaledTime * 6f) + 1f) * 0.5f;
                    rebanadas[i].color = Color.Lerp(new Color(0.05f, 0.3f, 0.42f, 0.95f), new Color(0.25f, 0.85f, 1f, 0.97f), latido);
                }
                // Escalonado de apertura (cada porcion entra un instante despues de la anterior) y "pop" al elegir.
                float ent = Mathf.Clamp01((aperturaT - i * 0.035f) / 0.65f);
                float pop = sel ? 1.04f + 0.08f * (1f - popT) * (1f - popT) + 0.012f * Mathf.Sin(Time.unscaledTime * 7f) : ctx ? 1.02f : 1f;
                rebanadas[i].rectTransform.localScale = Vector3.one * pop * Mathf.Lerp(0.4f, 1f, RebotePositivo(ent));
                var cr = rebanadas[i].color; cr.a *= ent; rebanadas[i].color = cr;

                // Pedido explicito: "iconos grandes descriptivos y claros,
                // texto chico" -- el icono ocupa el centro de la porcion (mas
                // cerca del medio del anillo) y el texto pasa a ser una
                // etiqueta chica de UNA sola linea, mas afuera, que ya no
                // necesita repetir el nombre completo porque el icono ya lo dice.
                var colorTexto = sel ? new Color(0.05f, 0.06f, 0.08f) : ctx ? new Color(1f, 0.9f, 0.45f) : Color.white;
                float rad = centro * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
                if (iconosCategoria != null && iconosCategoria[i] != null)
                {
                    iconosCategoria[i].rectTransform.anchoredPosition = dir * RadioIconoCategoria + Vector2.up * SubidaDelIcono;
                    iconosCategoria[i].rectTransform.sizeDelta = new Vector2(tamanoIcono, tamanoIcono);
                    iconosCategoria[i].color = new Color(colorTexto.r, colorTexto.g, colorTexto.b, ent);
                    iconosCategoria[i].rectTransform.localScale = Vector3.one * (sel ? pop : 1f);
                }
                etiquetas[i].rectTransform.anchoredPosition = dir * RadioIconoCategoria + Vector2.down * BajadaDeLaEtiqueta;
                string marca = cat == PistaCategoria && !sel ? "▶" : ctx ? "★" : (i + 1).ToString();
                etiquetas[i].text = "<size=9>" + marca + "</size> " + NombreCorto(cat);
                etiquetas[i].color = colorTexto;
            }

            // Brillo de la porcion elegida y color del anillo exterior (siguen el acento de la categoria).
            if (brillo != null)
            {
                bool hay = Seleccion >= 0 && ids.Contains(Seleccion);
                brillo.gameObject.SetActive(hay);
                var ac = hay ? (EsContextual(Seleccion) ? Dorado : Acentos[Seleccion]) : new Color(0.55f, 0.85f, 1f);
                if (hay)
                {
                    brillo.fillAmount = relleno;
                    brillo.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -(AnguloDeSlot(ids.IndexOf(Seleccion)) - relleno * 180f));
                    brillo.color = new Color(ac.r, ac.g, ac.b, 0.30f + 0.12f * Mathf.Sin(Time.unscaledTime * 8f));
                }
                if (anilloGiratorio != null) anilloGiratorio.color = new Color(ac.r, ac.g, ac.b, hay ? 0.55f : 0.30f);
                if (anilloInterno != null) anilloInterno.color = new Color(ac.r, ac.g, ac.b, hay ? 0.30f : 0.14f);
            }

            // Abanico de opciones de la categoria resaltada.
            int n = Seleccion >= 0 ? opcionesActuales.Count : 0;
            int slotSel = Seleccion >= 0 ? ids.IndexOf(Seleccion) : -1;
            for (int j = 0; j < opciones.Length; j++)
            {
                bool visible = j < n;
                opciones[j].gameObject.SetActive(visible);
                etiquetasOpcion[j].gameObject.SetActive(false);
                if (iconosOpcion != null && iconosOpcion[j] != null) iconosOpcion[j].gameObject.SetActive(false);
                if (!visible) continue;
                etiquetasOpcion[j].rectTransform.sizeDelta = new Vector2(92f, 30f);   // el radial guardado en la escena puede traer la caja vieja (118 x 40)
                float centro = AnguloDeSlot(slotSel) + (j - (n - 1) * 0.5f) * PasoDeAbanico;
                bool ctx = EsContextual(Seleccion);
                var c = ctx ? Dorado : Acentos[Seleccion];
                int sub = opcionesActuales[j];
                bool esMuerto = Seleccion == Poseer && sub >= 0 && sub < 3 && soldadosMuertos[sub];
                bool elegida = opcionesActuales[j] == Sub;
                
                opciones[j].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -(centro - AnchoDeOpcion * 0.5f));
                
                if (esMuerto)
                {
                    opciones[j].color = new Color(0.12f, 0.12f, 0.12f, OpacidadDeFondo);
                    opciones[j].rectTransform.localScale = Vector3.one;
                    float rad = centro * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
                    if (iconosOpcion != null && iconosOpcion[j] != null)
                    {
                        iconosOpcion[j].gameObject.SetActive(true);
                        iconosOpcion[j].color = new Color(0.5f, 0.5f, 0.5f);
                        iconosOpcion[j].rectTransform.anchoredPosition = dir * RadioIconoOpcion + Vector2.up * SubidaDelIconoOpcion;
                    }
                    etiquetasOpcion[j].rectTransform.anchoredPosition = dir * RadioIconoOpcion + Vector2.down * BajadaDeLaEtiquetaOpcion;
                    etiquetasOpcion[j].text = $"<size=9>{sub + 1}</size>\n{SP.Core.Loc.T("MUERTO")}";
                    etiquetasOpcion[j].color = new Color(0.5f, 0.5f, 0.5f);
                }
                else
                {
                    opciones[j].color = elegida ? new Color(c.r, c.g, c.b, 0.97f) : ctx ? DoradoOscuro : new Color(0.09f, 0.12f, 0.17f, OpacidadDeFondo + 0.02f);
                    if (!elegida && Seleccion == PistaCategoria && opcionesActuales[j] == PistaOpcion)
                    {
                        float latido = (Mathf.Sin(Time.unscaledTime * 6f) + 1f) * 0.5f;
                        opciones[j].color = Color.Lerp(new Color(0.05f, 0.3f, 0.42f, 0.95f), new Color(0.25f, 0.85f, 1f, 0.97f), latido);
                    }
                    opciones[j].rectTransform.localScale = Vector3.one * (elegida ? 1.03f + 0.06f * (1f - popT) * (1f - popT) : 1f);
                    float rad = centro * Mathf.Deg2Rad;
                    var dirOp = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
                    var colorOp = elegida ? new Color(0.05f, 0.06f, 0.08f) : ctx ? new Color(1f, 0.9f, 0.45f) : Color.white;
                    if (iconosOpcion != null && iconosOpcion[j] != null)
                    {
                        iconosOpcion[j].gameObject.SetActive(true);
                        iconosOpcion[j].sprite = RadialIconFactory.ForOpcion(Seleccion, sub);
                        iconosOpcion[j].color = colorOp;
                        iconosOpcion[j].rectTransform.anchoredPosition = dirOp * RadioIconoOpcion + Vector2.up * SubidaDelIconoOpcion;
                        iconosOpcion[j].rectTransform.sizeDelta = new Vector2(TamanoIconoOpcion, TamanoIconoOpcion);
                    }
                    etiquetasOpcion[j].rectTransform.anchoredPosition = dirOp * RadioIconoOpcion + Vector2.down * BajadaDeLaEtiquetaOpcion;
                    etiquetasOpcion[j].text = Partir(Texto(OpcionesDe[Seleccion][sub]).Replace(" · ", "\n"));
                    etiquetasOpcion[j].color = colorOp;
                }
            }

            if (cursor != null) cursor.rectTransform.anchoredPosition = virtualPos;
            if (lista != null) { lista.text = ""; lista.gameObject.SetActive(false); }   // sin textos
        }

        // Construye el panel si la escena no lo trae y deja cableado el driver. Existe
        // porque SC_Gameplay NO la arma HeadlessTestRunner (esa solo escribe SC_TestLevel).
        public static MenuDeOrdenes AsegurarEnEscena()
        {
            var driver = SP.Player.PlayerInputDriver.Activo;

            // El canvas del HUD (pantalla), NO los de barras de vida / etiquetas (mundo).
            Transform raiz = driver != null && driver.AimUiRef != null ? driver.AimUiRef.transform.parent : null;
            if (raiz == null)
                foreach (var c in Object.FindObjectsByType<Canvas>())
                    if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace) { raiz = c.transform; break; }
            if (raiz == null) return null;

            var existente = Activo;
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
            bool fino = hueco > 0.9f;
            if (fino && donaFina != null) return donaFina;
            if (!fino && interior && donaInterior != null) return donaInterior;
            if (!fino && !interior && donaExterior != null) return donaExterior;
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
            if (fino) donaFina = sp; else if (interior) donaInterior = sp; else donaExterior = sp;
            return sp;
        }

        // Disco oscuro detras del texto del centro: sin el, el texto se mezclaba con la mira y con el paisaje.
        // Se asegura tambien al abrir: el radial guardado en SC_Gameplay puede venir de antes de que existiera.
        void AsegurarDiscoDelCentro()
        {
            if (SP.Core.BuscarHijo.Ruta(transform, "CentroFondo") != null) return;
            var discoGO = new GameObject("CentroFondo", typeof(RectTransform), typeof(Image));
            discoGO.transform.SetParent(transform, false);
            discoGO.transform.SetAsFirstSibling();
            var disco = discoGO.GetComponent<Image>();
            disco.sprite = Disco();
            disco.color = new Color(0.03f, 0.05f, 0.08f, 0.82f);
            disco.raycastTarget = false;
            disco.rectTransform.anchorMin = disco.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            disco.rectTransform.anchoredPosition = Vector2.zero;
            float lado = RadioInterior * 0.36f * 2f - 2f;
            disco.rectTransform.sizeDelta = new Vector2(lado, lado);
        }

        static Sprite discoCentro;
        static Sprite Disco()
        {
            if (discoCentro != null) return discoCentro;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            float R = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(R, R));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(R - d) * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            discoCentro = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            discoCentro.hideFlags = HideFlags.HideAndDontSave;
            return discoCentro;
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

        static Image NuevoIcono(Transform padre, string nombre, Sprite sprite, float lado)
        {
            var g = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(padre, false);
            var img = g.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.preserveAspect = true;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(lado, lado);
            return img;
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
            menu.RegistrarActivo();   // en Edit mode (suite, builders) no corre OnEnable
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            menu.rebanadas = new Image[CantidadDePorciones];
            menu.iconosCategoria = new Image[CantidadDePorciones];
            menu.etiquetas = new Text[CantidadDePorciones];
            float fill = 120f / 360f - 0.006f;
            var interior = Dona(0.36f);

            for (int i = 0; i < CantidadDePorciones; i++)
            {
                var img = NuevaRebanada(go.transform, "Categoria" + (i + 1), interior, RadioInterior, fill);
                menu.rebanadas[i] = img;
                menu.iconosCategoria[i] = NuevoIcono(go.transform, "IconoCategoria" + (i + 1), RadialIconFactory.ForCategoria(i), TamanoIconoCategoria);
                var t = NuevoTexto(go.transform, "TextoCategoria" + (i + 1), font, TamanoLetra, new Vector2(150f, 22f));
                t.text = $"<size=9>{i + 1}</size> {Porciones[i].Replace("\n", " ")}";
                menu.etiquetas[i] = t;
            }

            // Anillo exterior: un abanico reutilizable de hasta 5 opciones.
            menu.opciones = new Image[MaxOpcionesPorCategoria];
            menu.etiquetasOpcion = new Text[MaxOpcionesPorCategoria];
            menu.iconosOpcion = new Image[MaxOpcionesPorCategoria];
            var exterior = Dona(0.6f);
            float fillOp = AnchoDeOpcion / 360f;
            for (int j = 0; j < MaxOpcionesPorCategoria; j++)
            {
                menu.opciones[j] = NuevaRebanada(go.transform, "Opcion" + (j + 1), exterior, RadioExterior, fillOp);
                menu.iconosOpcion[j] = NuevoIcono(go.transform, "IconoOpcion" + (j + 1), RadialIconFactory.Calavera(), TamanoIconoOpcion);
                menu.etiquetasOpcion[j] = NuevoTexto(go.transform, "TextoOpcion" + (j + 1), font, TamanoLetra, new Vector2(92f, 30f));
                menu.opciones[j].gameObject.SetActive(false);
                menu.iconosOpcion[j].gameObject.SetActive(false);
                menu.etiquetasOpcion[j].gameObject.SetActive(false);
            }

            menu.AsegurarDiscoDelCentro();

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
            menu.Distribuir(null);
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
            if (kb.digit9Key.wasPressedThisFrame) return 9;
            return 0;
        }
    }
}
