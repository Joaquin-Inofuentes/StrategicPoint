using UnityEngine;
using SP.Actors;

namespace SP.Ai
{
    // Ajustes de la escuadra aliada, editables desde el Inspector.
    // GameObject: "Systems/AjustesDeEscuadra" (componente AjustesDeEscuadra).
    //
    //   distanciaParaSeguir : si el soldado que manejas se aleja MAS de esto de
    //                         un aliado libre, ese aliado empieza a seguirte.
    //   distanciaParaDetenerse : el aliado deja de seguirte al llegar a esto.
    //   seguirDesdeCobertura : los que estan en cobertura tambien te siguen.
    //
    // Los valores viven en estaticos (AiBrain los lee cada tick sin buscar
    // nada); este componente solo los copia al arrancar y al editarlos.
    [ExecuteAlways]
    public class AjustesDeEscuadra : MonoBehaviour
    {
        public float distanciaParaSeguir = 25f;
        public float distanciaParaDetenerse = 8f;
        public bool seguirDesdeCobertura = true;

        public static float DistanciaParaSeguir = 25f;
        public static float DistanciaParaDetenerse = 8f;
        public static bool SeguirDesdeCobertura = true;

        // El soldado que maneja el jugador en primera persona (null en RTS o
        // sin nadie): PlayerInputDriver lo actualiza cada frame.
        public static Soldier Lider;

        // [Shift] apretado a pie: los aliados libres que van con el jugador (siguen, cumplen
        // una orden de mover o se retiran) tambien corren.
        public static bool Correr;

        void OnEnable() => Aplicar();
        void OnValidate() => Aplicar();

        public void Aplicar()
        {
            DistanciaParaSeguir = Mathf.Max(2f, distanciaParaSeguir);
            DistanciaParaDetenerse = Mathf.Clamp(distanciaParaDetenerse, 1f, DistanciaParaSeguir - 1f);
            SeguirDesdeCobertura = seguirDesdeCobertura;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetearLider() { Lider = null; Correr = false; }

        // Garantiza que exista en la escena (SC_Gameplay lo trae puesto; las
        // escenas de prueba no).
        public static AjustesDeEscuadra AsegurarEnEscena()
        {
            var a = FindFirstObjectByType<AjustesDeEscuadra>(FindObjectsInactive.Include);
            if (a != null) return a;
            var go = new GameObject("AjustesDeEscuadra");
            return go.AddComponent<AjustesDeEscuadra>();
        }
    }
}
