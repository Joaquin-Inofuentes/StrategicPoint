using System.Collections.Generic;
using UnityEngine;

namespace SP.Core
{
    // Resources.Load con memoria: el primer pedido carga, los siguientes salen de un diccionario. No guarda los fallos
    // (un .mp3 que aun no se importo se reintenta). Precargar() sube lo critico durante la carga y no en pleno combate.
    public static class RecursosCache
    {
        static readonly Dictionary<string, Object> cache = new Dictionary<string, Object>();
        public static int Cargas { get; private set; }
        public static int Aciertos { get; private set; }

        public static T Cargar<T>(string ruta) where T : Object
        {
            string clave = typeof(T).Name + ":" + ruta;
            if (cache.TryGetValue(clave, out var o) && o != null) { Aciertos++; return (T)o; }
            var r = Resources.Load<T>(ruta);
            Cargas++;
            if (r != null) cache[clave] = r;
            return r;
        }

        public static bool EstaEnCache<T>(string ruta) where T : Object => cache.TryGetValue(typeof(T).Name + ":" + ruta, out var o) && o != null;

        public static void Precargar()
        {
            Cargar<Material>("Soldados/MAT_Trimsheet_Aliado");
            Cargar<Material>("Soldados/MAT_Trimsheet_Enemigo");
            Cargar<GameObject>("Weapons/P_Wpn_Granada");
            Cargar<GameObject>("Weapons/P_Wpn_Cuchillo");
            Cargar<GameObject>("Weapons/P_Wpn_Metralleta");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ArrancarPrecarga() { Vaciar(); Precargar(); }

        public static void Vaciar() { cache.Clear(); Cargas = 0; Aciertos = 0; }
    }
}
