using UnityEngine;

namespace SP.CameraSystem
{
    // Interruptor global de los efectos de camara (sacudida, balanceo al
    // caminar, viñeta de velocidad, destellos, latido). Es estatico y se
    // persiste en PlayerPrefs a proposito, por dos razones:
    //
    // 1) Esquiva de raiz el bug recurrente del proyecto: un campo de
    //    componente asignado al construir la escena no sobrevive el domain
    //    reload al entrar a Play. Un estatico con respaldo en PlayerPrefs
    //    no tiene ese problema.
    // 2) Lo consultan sistemas muy repartidos (CameraRig, Projectile,
    //    PlayerInputDriver, las vistas de HUD). Cablear una referencia a
    //    cada uno seria mucha superficie para un solo booleano.
    //
    // Los efectos de camara son la principal causa de mareo (motion
    // sickness) en un FPS, asi que tiene que poder apagarse sin apagar
    // nada mas del juego.
    public static class CameraFxSettings
    {
        const string Pref = "sp_camera_fx";

        static bool? cached;

        public static bool Enabled
        {
            get
            {
                if (cached == null) cached = PlayerPrefs.GetInt(Pref, 1) == 1;
                return cached.Value;
            }
            set
            {
                cached = value;
                PlayerPrefs.SetInt(Pref, value ? 1 : 0);
            }
        }

        // Solo para tests: vuelve a leer de PlayerPrefs en la proxima
        // consulta, sin arrastrar el valor cacheado de una corrida previa.
        public static void InvalidateCache() => cached = null;
    }
}
