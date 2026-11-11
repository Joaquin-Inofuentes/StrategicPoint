using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    // Ajustes del nivel de los bugs #415 y #422 (2026-11-11). Lo recorre AjustesDeNivelNov08.ProcesarTransform, con el mismo ritmo que el resto de los parches.
    //   #415 ("quita estos cubos raros"): en el centro de datos, los dos UPS (cubos negros sin forma, con su panel y su led) y la torre de servidor negra
    //        pegada a la computadora. La computadora (mesa, pantallas, barra de progreso) sigue donde esta.
    //   #422 ("quita esos bloques negros de obstaculos"): los tres Azotea_Equipo: cajas negras (material Barrote) que estorbaban en la azotea.
    public static class AjustesDeNivelNov11
    {
        static readonly HashSet<string> Siempre = new HashSet<string> { "UPS", "UPS_Panel", "UPS_Led", "Azotea_Equipo" };
        public static int Quitados { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Quitados = 0; }

        public static bool DebeQuitarse(Transform t)
        {
            string n = t.name;
            if (Siempre.Contains(n)) return true;
            if (n != "Torre_Servidor") return false;
            for (var p = t.parent; p != null; p = p.parent) if (p.name == "3_CentroDeDatos") return true;
            return false;
        }

        // true si lo apago (el que llama no sigue con ese transform).
        public static bool Quitar(Transform t)
        {
            if (!t.gameObject.activeSelf || !DebeQuitarse(t)) return false;
            t.gameObject.SetActive(false); Quitados++;
            return true;
        }
    }
}
