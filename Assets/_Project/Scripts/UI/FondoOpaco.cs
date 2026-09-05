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
        // del mapa el texto blanco seguia costando de leer.
        public static readonly Color Color = new Color(0.06f, 0.07f, 0.09f, 0.94f);

        // Cuanto sobresale el fondo del texto, en pixeles de UI.
        public const float MargenX = 18f;
        public const float MargenY = 10f;

        const string NombreDelFondo = "FondoOpaco";

        // Le cuelga un fondo al texto y lo deja DETRAS. Devuelve el fondo,
        // o null si no habia nada que hacer.
        public static Image Poner(Text texto)
        {
            if (texto == null) return null;

            var rtTexto = texto.rectTransform;
            var previo = texto.transform.Find(NombreDelFondo);
            if (previo != null) return previo.GetComponent<Image>();

            // Hijo del propio texto y estirado sobre el: asi lo sigue a
            // donde lo muevan, sin que nadie tenga que acordarse de mover
            // dos cosas. Y va primero en la lista de hijos para dibujarse
            // por debajo (uGUI dibuja en orden de jerarquia).
            var go = new GameObject(NombreDelFondo, typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(rtTexto, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-MargenX, -MargenY);
            rt.offsetMax = new Vector2(MargenX, MargenY);
            rt.SetAsFirstSibling();

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
