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
        // Unico de la escena: se registra al activarse en vez de que cada consumidor lo busque con un barrido.
        public static AjustesDeEscuadra Activo { get; private set; }
        public static void ReiniciarActivo() => Activo = null;
        public void RegistrarActivo()
        {
            Activo = this;
        }
        // Ronda 13 (punto 4): "seguirme" a la MITAD de la distancia anterior (25 / 8 m pasan a 12,5 / 4 m).
        public float distanciaParaSeguir = 12.5f;
        public float distanciaParaDetenerse = 4f;
        public bool seguirDesdeCobertura = true;

        public static float DistanciaParaSeguir = 12.5f;
        public static float DistanciaParaDetenerse = 4f;
        public static bool SeguirDesdeCobertura = true;

        // El soldado que maneja el jugador en primera persona (null en RTS o
        // sin nadie): PlayerInputDriver lo actualiza cada frame.
        public static Soldier Lider;

        // [Shift] apretado a pie: los aliados libres que van con el jugador (siguen, cumplen
        // una orden de mover o se retiran) tambien corren.
        public static bool Correr;

        void OnDisable() { if (Activo == this) Activo = null; }
        void OnEnable()
        {
            RegistrarActivo();
            Aplicar();
        }
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
            if (Activo != null) return Activo;
            var a = new GameObject("AjustesDeEscuadra").AddComponent<AjustesDeEscuadra>();
            a.RegistrarActivo();
            return a;
        }
    }
}
