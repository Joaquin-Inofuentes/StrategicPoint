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
        public const int CantidadDeCategorias = 9;
        public const int CantidadDePorciones = CantidadDeCategorias;
        public const int MaxOpcionesPorCategoria = 6;
        // Item 61: ATACAR tiene, ademas de "a quien ordeno" (0..3), dos ordenes tacticas.
        public const int SubSuprimir = 4, SubGranada = 5;

        // Identificadores estables de categoria (el orden en pantalla cambia con el contexto).
        public const int IrAlli = 0, Cubrirse = 1, Atacar = 2, Posicion = 3, Curar = 4, Tanque = 5, Poseer = 6, Demoler = 7, Torreta = 8;

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
        };

        static readonly Color[] Acentos =
        {
            new Color(0.4f, 0.92f, 0.5f),  new Color(1f, 0.86f, 0.3f),  new Color(1f, 0.35f, 0.3f),  new Color(0.3f, 0.85f, 1f),
            new Color(0.95f, 0.45f, 0.8f), new Color(1f, 0.66f, 0.25f), new Color(0.7f, 0.6f, 1f),   new Color(1f, 0.55f, 0.2f),
            new Color(0.5f, 0.9f, 0.85f),
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
        const float RadioIconoCategoria = RadioInterior * 0.44f;
        const float TamanoIconoCategoria = 58f; // lado maximo del icono; Refrescar() lo achica si no entra
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
        Image cursor;
        Vector2 virtualPos;
        readonly string[] soldados = { "1", "2", "3" };
        static Sprite donaInterior, donaExterior;

        // Distribucion actual: ids de categoria en orden de pantalla (las contextuales primero).
        readonly List<int> ids = new List<int>();
        readonly List<int> opcionesActuales = new List<int>();   // sub reales visibles de la categoria resaltada
        ContextoRadial contexto;
        float paso = 120f, desfase;

        // PISTA DE TUTORIAL: categoria/opcion que hay que elegir. Se dibuja latiendo en celeste.
        public static int PistaCategoria = -1, PistaOpcion = -1;
        public static void PonerPista(int categoria, int opcion = -1) { PistaCategoria = categoria; PistaOpcion = opcion; }

        void Update()
        {
            if (Abierto && PistaCategoria >= 0) Refrescar();
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
            Distribuir(ctx);
            Abierto = true;
            virtualPos = Vector2.zero;
            Seleccion = -1;
            Sub = -1;
            hoverCat = -1; hoverSub = -1;
            opcionesActuales.Clear();
            Refrescar();
            if (group != null) group.alpha = 1f;
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
                int n = opcionesActuales.Count;
                int idx = opcionesActuales.IndexOf(sub);
                float rad = (a + (idx - (n - 1) * 0.5f) * PasoDeAbanico) * Mathf.Deg2Rad;
                virtualPos = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * 245f;
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
            float tamanoIcono = Mathf.Clamp(cuerda * 0.8f, 34f, TamanoIconoCategoria);
            for (int i = 0; i < rebanadas.Length; i++)
            {
                bool activa = i < ids.Count;
                rebanadas[i].gameObject.SetActive(activa);
                etiquetas[i].gameObject.SetActive(activa);
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
                rebanadas[i].rectTransform.localScale = Vector3.one * (sel ? 1.04f : ctx ? 1.02f : 1f);

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
                    iconosCategoria[i].rectTransform.anchoredPosition = dir * RadioIconoCategoria;
                    iconosCategoria[i].rectTransform.sizeDelta = new Vector2(tamanoIcono, tamanoIcono);
                    iconosCategoria[i].color = colorTexto;
                }
                etiquetas[i].rectTransform.anchoredPosition = dir * (RadioInterior * 0.88f);
                string marca = cat == PistaCategoria && !sel ? "▶" : ctx ? "★" : (i + 1).ToString();
                etiquetas[i].text = "<size=9>" + marca + "</size> " + Porciones[cat].Replace("\n", " ");
                etiquetas[i].color = colorTexto;
            }

            // Abanico de opciones de la categoria resaltada.
            int n = Seleccion >= 0 ? opcionesActuales.Count : 0;
            int slotSel = Seleccion >= 0 ? ids.IndexOf(Seleccion) : -1;
            for (int j = 0; j < opciones.Length; j++)
            {
                bool visible = j < n;
                opciones[j].gameObject.SetActive(visible);
                etiquetasOpcion[j].gameObject.SetActive(visible);
                if (!visible) continue;
                float centro = AnguloDeSlot(slotSel) + (j - (n - 1) * 0.5f) * PasoDeAbanico;
                bool ctx = EsContextual(Seleccion);
                var c = ctx ? Dorado : Acentos[Seleccion];
                bool elegida = opcionesActuales[j] == Sub;
                opciones[j].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -(centro - AnchoDeOpcion * 0.5f));
                opciones[j].color = elegida ? new Color(c.r, c.g, c.b, 0.97f) : ctx ? DoradoOscuro : new Color(0.09f, 0.12f, 0.17f, OpacidadDeFondo + 0.02f);
                if (!elegida && Seleccion == PistaCategoria && opcionesActuales[j] == PistaOpcion)
                {
                    float latido = (Mathf.Sin(Time.unscaledTime * 6f) + 1f) * 0.5f;
                    opciones[j].color = Color.Lerp(new Color(0.05f, 0.3f, 0.42f, 0.95f), new Color(0.25f, 0.85f, 1f, 0.97f), latido);
                }
                opciones[j].rectTransform.localScale = Vector3.one * (elegida ? 1.03f : 1f);
                float rad = centro * Mathf.Deg2Rad;
                etiquetasOpcion[j].rectTransform.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * (RadioExterior * 0.78f);
                etiquetasOpcion[j].text = Texto(OpcionesDe[Seleccion][opcionesActuales[j]]).Replace(" · ", "\n");
                etiquetasOpcion[j].color = elegida ? new Color(0.05f, 0.06f, 0.08f) : ctx ? new Color(1f, 0.9f, 0.45f) : Color.white;
            }

            if (cursor != null) cursor.rectTransform.anchoredPosition = virtualPos;
            if (lista != null)
            {
                string apunta = contexto != null && !string.IsNullOrEmpty(contexto.Apuntando) ? "\n<size=11><color=#FFD23A>" + contexto.Apuntando + "</color></size>" : "";
                if (Seleccion < 0) lista.text = "ORDENES" + apunta + "\n<size=11>apunta con el mouse · suelta Q</size>";
                else if (Sub >= 0) lista.text = Porciones[Seleccion].Replace("\n", " ") + "\n<size=13>" + Texto(OpcionesDe[Seleccion][Sub]).Replace(" · ", " ") + "</size>";
                else lista.text = Porciones[Seleccion].Replace("\n", " ") + "\n<size=11>sigue hacia afuera</size>";
            }
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
            var exterior = Dona(0.6f);
            float fillOp = AnchoDeOpcion / 360f;
            for (int j = 0; j < MaxOpcionesPorCategoria; j++)
            {
                menu.opciones[j] = NuevaRebanada(go.transform, "Opcion" + (j + 1), exterior, RadioExterior, fillOp);
                menu.etiquetasOpcion[j] = NuevoTexto(go.transform, "TextoOpcion" + (j + 1), font, TamanoLetra, new Vector2(118f, 40f));
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
