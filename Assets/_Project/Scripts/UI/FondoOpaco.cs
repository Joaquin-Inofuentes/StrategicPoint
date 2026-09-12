using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Del plan del usuario, tres renglones que piden lo mismo:
    //
    //   "El cartel de F poseer a soldados fondo opaco para saber q es
    //    interactuable"
    //   "Mismo fondo opaco q sea para todos"
    //   "El mensaje del comienzo. Fondo opaco para ver q dice y arriba
    //    centro"
    //
    // Medido en SC_Gameplay: el cartel de [F] Poseer es un Text de 420x30
    // colgado directo del Canvas, SIN NINGUN fondo -- texto claro sobre el
    // terreno claro del mapa, que es donde justamente aparece. Y la barra
    // de instrucciones si tenia fondo, pero blanco al 80% de opacidad y
    // abajo de todo, no arriba.
    //
    // Un solo lugar define como es "el fondo": eso es lo que hace que sea
    // EL MISMO para todos, y no tres tonos parecidos que se van separando
    // con cada retoque.
    public static class FondoOpaco
    {
        // Casi negro y opaco de verdad. Con alfa 0,8 sobre el terreno claro
        // del mapa el texto blanco seguia costando de leer. Subido a 1
        // (antes 0,94): con Screen Space - Camera, ese 6% restante de
        // transparencia deja pasar el cielo/terreno de fondo -- medido en
        // captura real, el panel del cartel de instrucciones salia
        // notablemente mas claro (~(62,69,71) de 255) que el negro casi
        // puro que pedia el color (~(15,18,23)). Opaco de punta a punta
        // saca esa fuga de contraste de la ecuacion.
        public static readonly Color Color = new Color(0.06f, 0.07f, 0.09f, 1f);

        // Contorno oscuro para el TEXTO en si, no solo el fondo: aunque el
        // fondo sea opaco, un texto sin borde se lee peor apenas roza un
        // borde de panel, una transicion de escala o cualquier variacion
        // de brillo detras. El contorno lo hace legible SIEMPRE, sin
        // depender de que el fondo rinda exactamente como su valor
        // nominal.
        public static readonly Color ColorDeContorno = new Color(0f, 0f, 0f, 0.85f);

        // Cuanto sobresale el fondo del texto, en pixeles de UI.
        public const float MargenX = 18f;
        public const float MargenY = 10f;

        // Poner un fondo oscuro detras de un texto que ya era oscuro lo
        // deja PEOR que antes. Paso exactamente eso: la barra de
        // instrucciones tenia el texto en (0,08 0,10 0,12) porque vivia
        // sobre un fondo BLANCO al 80%; al cambiarle el fondo por este
        // casi negro (0,06 0,07 0,09) el contraste quedo en practicamente
        // 1 a 1 y el cartel se volvio ilegible. El fondo y el color del
        // texto son una sola decision, asi que se toman en el mismo lugar.
        public static readonly Color ColorDeTexto = new Color(0.94f, 0.96f, 0.98f);

        // Luminancia relativa, para decidir si el texto se ve o no.
        static float Luz(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        public static void AsegurarContraste(Text texto)
        {
            if (texto == null) return;
            // Solo se toca si de verdad no se lee: un texto que ya venia
            // claro, o de color a proposito (un aviso rojo), se respeta.
            if (Mathf.Abs(Luz(texto.color) - Luz(Color)) < 0.35f)
                texto.color = ColorDeTexto;
        }

        const string NombreDelFondo = "FondoOpaco";

        // Le cuelga un fondo al texto y lo deja DETRAS. Devuelve el fondo,
        // o null si no habia nada que hacer.
        public static Image Poner(Text texto)
        {
            if (texto == null) return null;

            // ANTES de la salida temprana: si el fondo ya estaba puesto
            // pero el texto sigue siendo ilegible, arreglarlo es
            // justamente lo que hace falta. Con el contraste despues del
            // return, una segunda llamada (OnEnable corre mas de una vez)
            // se saltaba el arreglo y el cartel quedaba negro sobre negro.
            AsegurarContraste(texto);

            var contorno = texto.GetComponent<Outline>();
            if (contorno == null) contorno = texto.gameObject.AddComponent<Outline>();
            contorno.effectColor = ColorDeContorno;
            contorno.effectDistance = new Vector2(1.3f, -1.3f);

            var rtTexto = texto.rectTransform;
            var padre = rtTexto.parent;
            var previo = padre != null ? padre.Find(NombreDelFondo) : texto.transform.Find(NombreDelFondo);
            if (previo != null) return previo.GetComponent<Image>();

            // BUG REAL (el cartel de instrucciones se leia con el texto
            // "lavado", casi invisible, y DESPUES de subir el fondo a
            // opaco total paso a taparlo por completo): esto colgaba el
            // fondo como HIJO del propio texto para que lo siguiera si lo
            // movian. Pero en uGUI un hijo se dibuja SIEMPRE despues (por
            // lo tanto encima) de su padre, sin importar el orden de
            // hermanos entre sus propios hijos -- SetAsFirstSibling()
            // solo ordena entre hermanos DEL MISMO padre, y el fondo no
            // tenia hermanos ahi (era hijo unico del texto). O sea que el
            // fondo quedaba SIEMPRE adelante del texto, no detras. Con el
            // alfa viejo (0,94) se colaba un 6% del texto por debajo (por
            // eso se leia mal, no invisible); apenas el fondo llego a
            // opaco total lo tapo entero.
            //
            // Ahora el fondo es HERMANO del texto (mismo padre), copia su
            // posicion/tamaño una sola vez (los llamadores actuales ya
            // terminaron de acomodar el texto antes de llamar a Poner) y
            // se inserta un lugar ANTES en la jerarquia -- eso si lo deja
            // detras de verdad.
            var go = new GameObject(NombreDelFondo, typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(padre, false);
            rt.anchorMin = rtTexto.anchorMin;
            rt.anchorMax = rtTexto.anchorMax;
            rt.pivot = rtTexto.pivot;
            rt.anchoredPosition = rtTexto.anchoredPosition;
            rt.sizeDelta = rtTexto.sizeDelta + new Vector2(MargenX * 2f, MargenY * 2f);
            rt.SetSiblingIndex(rtTexto.GetSiblingIndex());

            var img = go.GetComponent<Image>();
            img.color = Color;
            // Un fondo que se come los clicks taparia los botones que
            // tenga debajo. No decide nada, solo se ve.
            img.raycastTarget = false;
            return img;
        }

        // Donde entra el cartel de instrucciones arriba. No son numeros de
        // gusto: se midieron las cajas de TODO el HUD superior sobre el
        // Canvas de 969x1266 (x va de -485 a 485, y de -633 a 633).
        //
        //   roster        x -469..-229   y 467..617
        //   medidor perf  x -469.. -49   y 507..617
        //   estado mision x -180.. 180   y 585..619
        //   seleccion     x -100.. 100   y 551..579
        //   aviso de modo x -130.. 130   y 503..543
        //   minimapa      x  241.. 469   y 389..617
        //   tarjetas      x  289.. 469   y 173..373
        //
        // O sea que arriba NO queda ninguna franja libre de punta a punta:
        // el cartel de 900 de ancho, pegado al borde, caia encima del
        // roster (lo probe y se ve en la captura). La unica ventana que
        // queda es el centro por debajo del aviso de modo y entre el
        // roster y el minimapa: x +-225, y 400..440.
        public const float AlturaLibreArriba = 193f;
        public const float AnchoLibreArriba = 450f;

        // "y arriba centro": lo lleva a la unica ventana libre de arriba.
        public static void LlevarArribaAlCentro(RectTransform rt, float margenDesdeArriba = AlturaLibreArriba)
        {
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -margenDesdeArriba);
            rt.sizeDelta = new Vector2(AnchoLibreArriba, Mathf.Max(30f, rt.sizeDelta.y));
        }
    }
}
