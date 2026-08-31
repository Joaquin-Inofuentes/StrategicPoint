using System.Collections;
using UnityEngine;

namespace SP.Presentation
{
    // Sordera momentanea tras una explosion muy cercana (item 181).
    //
    // Este proyecto no tiene AudioMixer (todos los clips se generan por
    // codigo y se reproducen en AudioSource sueltos), asi que un filtro
    // pasa-bajos real exigiria crear el mixer y re-rutear cada AudioSource
    // del juego: un rediseño, no un item. El sustituto honesto es bajar y
    // recuperar AudioListener.volume.
    //
    // La trampa, y la razon por la que esto vive en su propia clase: el
    // volumen maestro es propiedad de PauseController, que lo escribe desde
    // el slider de Volumen y lo persiste en PlayerPrefs. Restaurar a 1f
    // "porque si" le devolveria el volumen a tope a un jugador que lo habia
    // dejado en 0.3. Siempre se recupera hacia el TECHO DEL USUARIO, leido
    // de la misma clave de PlayerPrefs que usa el slider.
    public class AudioDucking : MonoBehaviour
    {
        const string PrefVolume = "sp_volume";

        static AudioDucking instance;
        static Coroutine active;

        public static float UserVolumeCeiling => PlayerPrefs.GetFloat(PrefVolume, 1f);

        // Cuanto se atenua como maximo (0.25 = queda el 25% del techo).
        const float MaxDuckFactor = 0.25f;
        const float RecoverSeconds = 1.5f;

        public static void Duck(float intensity01)
        {
            if (!Application.isPlaying) return;
            if (!SP.CameraSystem.CameraFxSettings.Enabled) return;

            EnsureHost();
            if (instance == null) return;

            if (active != null) instance.StopCoroutine(active);
            active = instance.StartCoroutine(instance.DuckRoutine(Mathf.Clamp01(intensity01)));
        }

        static void EnsureHost()
        {
            if (instance != null) return;
            // Host propio y oculto: no depende de que nadie lo cablee en la
            // escena, asi que tampoco puede perderse en un domain reload.
            var go = new GameObject("AudioDuckingHost");
            go.hideFlags = HideFlags.HideAndDontSave;
            instance = go.AddComponent<AudioDucking>();
        }

        IEnumerator DuckRoutine(float intensity01)
        {
            float ceiling = UserVolumeCeiling;
            float floor = Mathf.Lerp(ceiling, ceiling * MaxDuckFactor, intensity01);

            AudioListener.volume = floor;

            float t = 0f;
            while (t < RecoverSeconds)
            {
                // unscaled: la camara lenta de la ultima baja no debe
                // estirar la sordera a varios segundos reales.
                t += Time.unscaledDeltaTime;
                // Se relee el techo cada frame: si el jugador mueve el
                // slider de Volumen mientras dura la sordera, la
                // recuperacion converge al valor NUEVO y no al viejo.
                float target = UserVolumeCeiling;
                AudioListener.volume = Mathf.Lerp(floor, target, t / RecoverSeconds);
                yield return null;
            }

            AudioListener.volume = UserVolumeCeiling;
            active = null;
        }

        void OnDisable()
        {
            // Si esto se apaga a mitad de la sordera, el volumen quedaria
            // atenuado para siempre.
            if (active != null) { StopCoroutine(active); active = null; }
            AudioListener.volume = UserVolumeCeiling;
        }
    }
}
