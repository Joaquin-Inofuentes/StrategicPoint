using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SP.UI
{
    // Pedido: "el texto de mision con las minimas palabras posibles". Los directores de nivel arman frases largas ("Avanza entre las lineas
    // enemigas hasta el CENTRO · 119 m"); aca se reducen a lo que cambia de verdad: el titulo a una palabra y el detalle a sus cifras
    // ("119 m", "0/2", "24 s"). Un solo lugar, asi cada fase nueva que alguien agregue sale corta sin tocarla.
    public static class TextoMinimo
    {
        // #279: el objetivo de arriba a la izquierda tiene que ser DESCRIPTIVO: el texto corto queda apagado (los textos completos se ven).
        public static bool Activo = false;

        static readonly Dictionary<string, string> titulos = new Dictionary<string, string>
        {
            { "INFILTRAR", "INFILTRAR" },
            { "RESISTIR EN EL CENTRO", "RESISTIR" },
            { "RESCATAR AL CIVIL", "RESCATAR" },
            { "ESCAPAR EN EL HELICOPTERO", "ESCAPAR" },
            { "INFILTRAR EL CUARTEL", "INFILTRAR" },
            { "DETONÁ LAS 2 TORRETAS", "TORRETAS" },
            { "VOLAR LOS PUESTOS", "PUESTOS" },
            { "CENTRO DE DATOS", "HACKEAR" },
            { "HUIR EN EL TANQUE", "HUIR" },
            { "DERRIBAR AL BLINDADO", "BLINDADO" },
            { "REPARAR EL BLINDADO", "REPARAR" },
            { "DIRIGIR LA DEFENSA", "DEFENSA" },
            { "RESISTIR EN LA CIUDAD", "RESISTIR" },
            { "SUBIR AL HELICOPTERO", "HELICOPTERO" },
            { "CUBRIR LA EXTRACCION", "EXTRAER" },
        };

        // "1/6 · DETONÁ LAS 2 TORRETAS" -> "1/6 · TORRETAS"   ("OBJETIVO 1/4 · RESISTIR EN EL CENTRO" -> "1/4 · RESISTIR")
        public static string Titulo(string t)
        {
            if (string.IsNullOrEmpty(t) || !Activo) return t;
            t = t.Replace("OBJETIVO ", "").Trim();
            int p = t.IndexOf(" · ");
            if (p < 0) return t;
            string numero = t.Substring(0, p), nombre = t.Substring(p + 3).Trim();
            if (!titulos.TryGetValue(nombre, out var corto))
            {
                int sp = nombre.IndexOf(' ');
                corto = sp > 2 ? nombre.Substring(0, sp) : nombre;
            }
            return numero + " · " + corto;
        }

        static readonly Regex reCifra = new Regex(@"\d+(?:[/.,]\d+)?\s?(?:m|s|%|x)?\b", RegexOptions.Compiled);
        static readonly Regex reEntreParentesis = new Regex(@"\([^)]*\)|\[[^\]]*\]|<[^>]*>", RegexOptions.Compiled);

        // Solo las cifras (con su unidad): "Resiste 24 s · oleadas 1/3" -> "24 s · 1/3". Sin cifras no queda nada: el titulo alcanza.
        public static string Detalle(string d)
        {
            if (string.IsNullOrWhiteSpace(d)) return "";
            if (!Activo) return d;
            d = reEntreParentesis.Replace(d, " ");
            var sb = new StringBuilder();
            foreach (Match m in reCifra.Matches(d))
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(m.Value.Trim());
            }
            return sb.ToString();
        }
    }
}
