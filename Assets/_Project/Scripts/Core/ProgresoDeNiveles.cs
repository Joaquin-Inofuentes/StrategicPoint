using UnityEngine;

namespace SP.Core
{
    // Ronda 19 (plan R19 v2, punto 1, tarea A1): que niveles del menu se terminaron y el mejor tiempo de cada uno.
    // Estatico y sin estado propio (todo vive en PlayerPrefs): no hay nada que reiniciar con el dominio (R2).
    //   sp_nivel{n}_completo = 1   (el nivel se termino al menos una vez)
    //   sp_nivel{n}_mejor_s        (mejor tiempo en segundos; Completar solo lo MEJORA, nunca lo empeora)
    // Nivel 1 = tutorial (TutorialManager.Finalizar), Nivel 2 = Operacion Cuartel (OperacionDirector.Ganar).
    public static class ProgresoDeNiveles
    {
        public static string ClaveCompleto(int nivel) => "sp_nivel" + nivel + "_completo";
        public static string ClaveMejorTiempo(int nivel) => "sp_nivel" + nivel + "_mejor_s";

        public static void Completar(int nivel, float segundos)
        {
            PlayerPrefs.SetInt(ClaveCompleto(nivel), 1);
            if (segundos > 0f && !float.IsNaN(segundos) && !float.IsInfinity(segundos))
            {
                float previo = MejorTiempo(nivel);
                if (previo <= 0f || segundos < previo) PlayerPrefs.SetFloat(ClaveMejorTiempo(nivel), segundos);
            }
            PlayerPrefs.Save();
            GameLog.Line($"[Progreso] Nivel {nivel} completado en {Formato(segundos)} (mejor {Formato(MejorTiempo(nivel))})");
        }

        public static bool EstaCompleto(int nivel) => PlayerPrefs.GetInt(ClaveCompleto(nivel), 0) == 1;

        // Mejor tiempo en segundos, o -1 si no hay ninguno guardado.
        public static float MejorTiempo(int nivel) => PlayerPrefs.HasKey(ClaveMejorTiempo(nivel)) ? PlayerPrefs.GetFloat(ClaveMejorTiempo(nivel), -1f) : -1f;

        public static void Borrar(int nivel)
        {
            PlayerPrefs.DeleteKey(ClaveCompleto(nivel));
            PlayerPrefs.DeleteKey(ClaveMejorTiempo(nivel));
            PlayerPrefs.Save();
        }

        // "mm:ss" (o "--:--" sin tiempo).
        public static string Formato(float segundos)
        {
            if (segundos < 0f || float.IsNaN(segundos) || float.IsInfinity(segundos)) return "--:--";
            int m = Mathf.FloorToInt(segundos / 60f), s = Mathf.FloorToInt(segundos % 60f);
            return $"{m:00}:{s:00}";
        }
    }
}
