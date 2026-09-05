using UnityEngine;

namespace SP.UI
{
    // Del plan del usuario, dos renglones distintos que resultaron ser el
    // mismo defecto:
    //
    //   "EN la pantalla de ganar y perder. Los botones 2 veces mas
    //    grandes y mejor diagramado"
    //   "y en la pantalla de confiramcion de salir q los botones esten
    //    mejor diagramados q sean legibles por q ahora el problema es que
    //    se solapan los 2"
    //
    // Los dos vienen de que cada elemento se coloco con un anchoredPosition
    // escrito a mano, sin que nadie comprobara que las cajas no se pisan.
    // Medido en SC_Gameplay:
    //
    //   * Confirmar salida: CANCELAR en x=-90 y SALIR en x=+90, los dos de
    //     260 de ancho. Van de -220 a +40 y de -40 a +220: se solapan
    //     80 pixeles. En esa franja del medio el click se lo lleva el que
    //     se dibuja ultimo, asi que apuntarle a CANCELAR podia salir.
    //     Ademas 2 x 260 no entran en un panel de 420 de ancho: no habia
    //     posicion posible que no se solapara.
    //
    //   * Ganar/perder: el titulo y el boton REINTENTAR estan los dos en
    //     y=0, y las estadisticas y el boton SALIR los dos en y=-70. Los
    //     botones se dibujan ENCIMA del texto.
    //
    // Se acomoda en runtime y no solo en el constructor de la escena
    // porque las posiciones ya estan guardadas en el .unity: arreglar el
    // builder deja la escena real igual de rota.
    public static class Diagramador
    {
        // Alto y ancho de un boton "grande". El pedido es explicito: el
        // doble. De 260x56 (14.560 px2) a 380x88 (33.440 px2).
        public const float AnchoGrande = 380f;
        public const float AltoGrande = 88f;

        // Separacion minima entre dos cajas. Por debajo de esto se leen
        // como una sola.
        public const float Aire = 22f;

        public static void AcomodarConfirmarSalida(GameObject panel)
        {
            if (panel == null) return;
            var rt = panel.GetComponent<RectTransform>();
            var texto = Buscar(panel, "Text");
            var no = Buscar(panel, "NoButton");
            var si = Buscar(panel, "YesButton");
            if (no == null || si == null) return;

            float anchoPanel = rt != null && rt.sizeDelta.x > 1f ? rt.sizeDelta.x : 420f;

            // Los dos botones tienen que ENTRAR: se calcula el ancho a
            // partir del panel en vez de dejarlo fijo, para que no vuelva
            // a pasar si algun dia el panel cambia de tamanio.
            float ancho = Mathf.Min(200f, (anchoPanel - Aire * 3f) * 0.5f);
            float x = (ancho + Aire) * 0.5f;

            if (texto != null) texto.anchoredPosition = new Vector2(0f, -18f);

            no.sizeDelta = new Vector2(ancho, 64f);
            si.sizeDelta = new Vector2(ancho, 64f);
            no.anchoredPosition = new Vector2(-x, -58f);
            si.anchoredPosition = new Vector2(x, -58f);
        }

        public static void AcomodarResultado(GameObject panel)
        {
            if (panel == null) return;
            var titulo = Buscar(panel, "Title");
            var stats = Buscar(panel, "Stats");
            var reintentar = Buscar(panel, "RetryButton");
            var salir = Buscar(panel, "ExitButton");

            // De arriba hacia abajo y sin que dos cajas compartan franja:
            // titulo, estadisticas, y recien despues los botones.
            if (titulo != null) titulo.anchoredPosition = new Vector2(0f, 150f);
            if (stats != null) stats.anchoredPosition = new Vector2(0f, 50f);

            if (reintentar != null)
            {
                reintentar.sizeDelta = new Vector2(AnchoGrande, AltoGrande);
                reintentar.anchoredPosition = new Vector2(0f, -60f);
            }
            if (salir != null)
            {
                salir.sizeDelta = new Vector2(AnchoGrande, AltoGrande);
                salir.anchoredPosition = new Vector2(0f, -60f - AltoGrande - Aire);
            }
        }

        // Cuantos pares de cajas se pisan. Es la medida del defecto y el
        // criterio de exito: tiene que dar cero.
        public static int ContarSolapes(GameObject panel)
        {
            if (panel == null) return 0;
            var hijos = new System.Collections.Generic.List<RectTransform>();
            foreach (Transform h in panel.transform)
            {
                var rt = h as RectTransform;
                if (rt != null && rt.sizeDelta.x > 1f && rt.sizeDelta.y > 1f) hijos.Add(rt);
            }
            int n = 0;
            for (int i = 0; i < hijos.Count; i++)
                for (int j = i + 1; j < hijos.Count; j++)
                    if (SePisan(hijos[i], hijos[j])) n++;
            return n;
        }

        static bool SePisan(RectTransform a, RectTransform b)
        {
            return Caja(a).Overlaps(Caja(b));
        }

        // La caja EN COORDENADAS DEL PANEL. Comparar anchoredPosition a
        // secas no sirve y da falsos positivos: el titulo esta anclado al
        // borde de arriba y los botones al centro, asi que sus "y" no son
        // el mismo cero. Aca se pasa todo al mismo origen antes de
        // comparar -- si no, la medida que decide si el arreglo funciono
        // esta rota, que es peor que no medir.
        static Rect Caja(RectTransform rt)
        {
            var padre = rt.parent as RectTransform;
            var tamPadre = padre != null ? padre.rect.size : Vector2.zero;

            // Centro del ancla, respecto del centro del panel.
            var centroAncla = new Vector2(
                ((rt.anchorMin.x + rt.anchorMax.x) * 0.5f - 0.5f) * tamPadre.x,
                ((rt.anchorMin.y + rt.anchorMax.y) * 0.5f - 0.5f) * tamPadre.y);

            // Con anclas estiradas el tamanio real no es sizeDelta.
            var tam = new Vector2(
                rt.sizeDelta.x + (rt.anchorMax.x - rt.anchorMin.x) * tamPadre.x,
                rt.sizeDelta.y + (rt.anchorMax.y - rt.anchorMin.y) * tamPadre.y);

            var centro = centroAncla + rt.anchoredPosition + new Vector2(
                (0.5f - rt.pivot.x) * tam.x,
                (0.5f - rt.pivot.y) * tam.y);

            return new Rect(centro.x - tam.x * 0.5f, centro.y - tam.y * 0.5f, tam.x, tam.y);
        }

        static RectTransform Buscar(GameObject panel, string nombre)
        {
            var t = panel.transform.Find(nombre);
            return t != null ? t as RectTransform : null;
        }
    }
}
