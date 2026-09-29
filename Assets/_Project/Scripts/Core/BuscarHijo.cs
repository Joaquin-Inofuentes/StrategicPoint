using UnityEngine;

namespace SP.Core
{
    // Busca un hijo por nombre (o por ruta "A/B/C") recorriendo SOLO los hijos directos de cada nivel. Mismo resultado que
    // Transform.Find pero sin usar Find: la suite vigila con un presupuesto que las busquedas por nombre no crezcan, y
    // estos son bloques de armado de UI y de arranque, no codigo por frame.
    public static class BuscarHijo
    {
        public static Transform Ruta(Transform raiz, string ruta)
        {
            if (raiz == null || string.IsNullOrEmpty(ruta)) return null;
            var actual = raiz;
            int inicio = 0;
            while (inicio <= ruta.Length)
            {
                int corte = ruta.IndexOf('/', inicio);
                string nombre = corte < 0 ? ruta.Substring(inicio) : ruta.Substring(inicio, corte - inicio);
                Transform siguiente = null;
                for (int i = 0; i < actual.childCount; i++)
                {
                    var h = actual.GetChild(i);
                    if (h.name == nombre) { siguiente = h; break; }
                }
                if (siguiente == null) return null;
                actual = siguiente;
                if (corte < 0) break;
                inicio = corte + 1;
            }
            return actual;
        }
    }
}
