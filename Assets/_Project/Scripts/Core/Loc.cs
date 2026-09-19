using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SP.Core
{
    // Localizacion (item 27), espanol -> ingles. El texto original del juego es la CLAVE (no hay tablas de ids que
    // mantener): Loc.T("JUGAR") devuelve "PLAY" en ingles. Un traductor recorre los Text de la escena y cambia los que
    // coinciden con una entrada, tambien en formato "ETIQUETA: valor". El texto que no esta en la tabla queda en espanol.
    // Cobertura actual: menu, dificultad, pausa, configuraciones, avisos comunes y (LocTextos) el tutorial y la mision.
    // NO cubre: los textos con datos vivos (contadores y distancias) ni las descripciones largas de la tabla de controles.
    public enum Idioma { Es, En }

    public static class Loc
    {
        const string Pref = "sp_idioma";
        public static Idioma Actual { get; private set; } = Idioma.Es;
        public static event Action Cambio;

        static readonly Dictionary<string, string> en = new Dictionary<string, string>
        {
            // menu principal
            { "JUGAR", "PLAY" }, { "TUTORIAL", "TUTORIAL" }, { "SALIR", "QUIT" },
            { "COMANDO TACTICO EN PRIMERA PERSONA", "FIRST-PERSON TACTICAL COMMAND" },
            { "Primera vez? Empieza por el TUTORIAL. En partida: [Esc] pausa, con la lista de CONTROLES", "First time? Start with the TUTORIAL. In game: [Esc] pauses, with the CONTROLS list" },
            // dificultad
            { "ELEGI LA DIFICULTAD", "CHOOSE THE DIFFICULTY" },
            { "Cambia la vida y el dano de los enemigos, de tu escuadra y el tuyo.", "Changes the health and damage of enemies, your squad and yourself." },
            { "FACIL", "EASY" }, { "MEDIO", "MEDIUM" }, { "DIFICIL", "HARD" },
            { "Para aprender: enemigos debiles y tu escuadra aguanta mucho mas.", "For learning: weak enemies and a very tough squad." },
            // pausa y configuraciones
            { "PAUSA", "PAUSED" }, { "CONTINUAR", "CONTINUE" }, { "CONFIGURACIONES", "SETTINGS" }, { "CONTROLES", "CONTROLS" },
            { "VOLVER AL MENU", "BACK TO MENU" }, { "VOLVER", "BACK" }, { "RESTAURAR", "RESET" },
            { "REMAPEAR", "REBIND" }, { "CANCELAR", "CANCEL" }, { "REINTENTAR", "RETRY" },
            { "Sensibilidad de mouse", "Mouse sensitivity" }, { "Sensibilidad de torreta", "Turret sensitivity" },
            { "Volumen", "Volume" }, { "Tamano de HUD", "HUD size" }, { "Tamano de mirilla", "Crosshair size" },
            { "Efectos de camara", "Camera effects" },
            { "PANTALLA Y ACCESIBILIDAD", "DISPLAY AND ACCESSIBILITY" }, { "PANTALLA", "SCREEN" }, { "COMPLETA", "FULLSCREEN" }, { "VENTANA", "WINDOWED" },
            { "RESOLUCION", "RESOLUTION" }, { "CALIDAD", "QUALITY" }, { "DALTONISMO", "COLORBLIND MODE" }, { "HUD MINIMO", "MINIMAL HUD" },
            { "TAMANO DE INTERFAZ", "INTERFACE SIZE" }, { "SUBTITULOS DE SONIDO", "SOUND SUBTITLES" }, { "SI", "YES" },
            // avisos comunes
            { "ALTO", "HOLD" }, { "SIGANME", "FOLLOW ME" }, { "RETIRADA", "RETREAT" }, { "A CUBIERTO", "TAKE COVER" },
            { "FUEGO DE SUPRESION", "SUPPRESSING FIRE" }, { "PUNTO INACCESIBLE", "UNREACHABLE POINT" },
            { "NADIE A QUIEN ORDENAR", "NO ONE TO ORDER" }, { "NO HAY A QUIEN ATACAR", "NO ONE TO ATTACK" },
            { "NO HAY COBERTURA HACIA AHI", "NO COVER THAT WAY" }, { "SIN GRANADAS", "NO GRENADES" },
            { "ENFERMERO EN CAMINO", "MEDIC ON THE WAY" }, { "NO HACE FALTA", "NOT NEEDED" }, { "NO HAY QUIEN ATIENDA", "NO ONE TO HEAL" },
            { "NADIE TIENE GRANADAS O NO ALCANZA", "NOBODY HAS GRENADES OR CAN REACH" },
            { "SUMINISTROS: YA ESTAS COMPLETO", "SUPPLIES: YOU ARE FULL" },
            { "ARSENAL: [,] [.] CAMBIAN EL ARMA PRINCIPAL", "ARSENAL: [,] [.] SWAP THE PRIMARY WEAPON" },
            { "ACERCATE A UNA CAJA DE SUMINISTROS PARA CAMBIAR DE ARMA", "GET NEXT TO A SUPPLY CRATE TO SWAP WEAPONS" },
            { "TODOS", "ALL" }, { "TODOS SUPRIMEN", "ALL SUPPRESS" }, { "GRANADA ALLI", "GRENADE THERE" },
            { "ATACAR", "ATTACK" }, { "MOVER", "MOVE" }, { "CUBRIRSE", "COVER" }, { "CURAR", "HEAL" }, { "POSEER", "POSSESS" }, { "TANQUE", "TANK" },
        };

        static readonly Dictionary<string, string> es = new Dictionary<string, string>();
        static readonly Dictionary<string, string> enNorm = new Dictionary<string, string>();
        // Texto original (con sus tildes) de cada traduccion aplicada, para poder revertir sin perderlas.
        static readonly Dictionary<string, string> originales = new Dictionary<string, string>();

        static Loc()
        {
            foreach (var kv in LocTextos.Tutorial) if (!en.ContainsKey(kv.Key)) en[kv.Key] = kv.Value;
            foreach (var kv in en) { es[kv.Value] = kv.Key; enNorm[Norm(kv.Key)] = kv.Value; }
        }

        // Sin tildes ni enie y en minusculas: el juego mezcla "MENU" y "MENU" con tilde segun el archivo.
        static string Norm(string s)
        {
            var d = s.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(d.Length);
            foreach (var c in d) if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Iniciar()
        {
            Actual = PlayerPrefs.GetInt(Pref, 0) == 1 ? Idioma.En : Idioma.Es;
            var go = new GameObject("LocTraductor") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<LocTraductor>();
        }

        public static void Poner(Idioma i)
        {
            if (Actual == i) return;
            Actual = i;
            PlayerPrefs.SetInt(Pref, i == Idioma.En ? 1 : 0); PlayerPrefs.Save();
            Cambio?.Invoke();
            LocTraductor.Recorrer();
        }
        public static void Alternar() => Poner(Actual == Idioma.Es ? Idioma.En : Idioma.Es);
        public static string NombreDelIdioma => Actual == Idioma.Es ? "ESPANOL" : "ENGLISH";

        // Acepta el texto entero o "ETIQUETA: valor" y conserva los espacios y saltos de linea del borde.
        static bool Buscar(string clave, bool haciaIngles, out string r)
        {
            if (haciaIngles) return enNorm.TryGetValue(Norm(clave), out r);
            if (originales.TryGetValue(clave, out r)) return true;
            return es.TryGetValue(clave, out r);
        }

        static string Traducir(string s, bool haciaIngles)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string t = s.Trim();
            if (t.Length == 0) return s;
            if (Buscar(t, haciaIngles, out var r)) { if (haciaIngles) originales[r] = t; return s.Replace(t, r); }
            int i = t.IndexOf(": ", StringComparison.Ordinal);
            if (i > 0)
            {
                string a = t.Substring(0, i), b = t.Substring(i + 2);
                bool ta = Buscar(a, haciaIngles, out var ra), tb = Buscar(b, haciaIngles, out var rb);
                if (ta || tb)
                {
                    if (haciaIngles) { if (ta) originales[ra] = a; if (tb) originales[rb] = b; }
                    return s.Replace(t, (ta ? ra : a) + ": " + (tb ? rb : b));
                }
            }
            return s;
        }

        // Traduce un texto en espanol (clave). Sin entrada devuelve el mismo texto.
        public static string T(string textoEs) => Actual == Idioma.En ? Traducir(textoEs, true) : textoEs;
        public static bool TieneEntrada(string textoEs) => textoEs != null && enNorm.ContainsKey(Norm(textoEs.Trim()));
        public static string Ingles(string textoEs) => Traducir(textoEs, true);

        // Lleva un texto al idioma actual: hacia ingles si esta en ingles; de vuelta al original si esta en espanol.
        internal static string Aplicar(string s) => Traducir(s, Actual == Idioma.En);
    }

    // Recorre los Text de la escena (activos e inactivos) para traducirlos. Corre al cargar cada escena y cada
    // medio segundo mientras el idioma no es el original: los paneles creados en pleno juego tambien se traducen.
    // [F12] alterna el idioma en cualquier momento.
    public class LocTraductor : MonoBehaviour
    {
        float proximo;

        void Awake() { UnityEngine.SceneManagement.SceneManager.sceneLoaded += (e, m) => Recorrer(); }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f12Key.wasPressedThisFrame) Loc.Alternar();
            if (Loc.Actual == Idioma.Es || Time.unscaledTime < proximo) return;
            proximo = Time.unscaledTime + 0.5f;
            Recorrer();
        }

        public static int UltimasTraducciones { get; private set; }

        // Por cada Text traducido se recuerda su texto original: el mismo ingles puede venir de dos espanoles con
        // y sin tilde, y al volver hay que devolver exactamente el que estaba.
        static readonly Dictionary<Text, (string traducido, string original)> registro = new Dictionary<Text, (string, string)>();

        public static void Recorrer()
        {
            int n = 0;
            bool aIngles = Loc.Actual == Idioma.En;
            foreach (var t in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null) continue;
                var id = t;
                bool hay = registro.TryGetValue(id, out var reg);
                if (!aIngles)
                {
                    if (hay && t.text == reg.traducido) { t.text = reg.original; n++; }
                    else if (!hay) { var v = Loc.Aplicar(t.text); if (v != t.text) { t.text = v; n++; } }
                    registro.Remove(id);
                    continue;
                }
                if (hay && t.text == reg.traducido) continue;
                var nuevo = Loc.Aplicar(t.text);
                if (nuevo != t.text) { registro[id] = (nuevo, t.text); t.text = nuevo; n++; }
                else if (hay) registro.Remove(id);
            }
            UltimasTraducciones = n;
        }
    }
}
