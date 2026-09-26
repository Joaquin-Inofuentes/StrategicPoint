using UnityEngine;
using SP.CameraSystem;

namespace SP.UI
{
    public class CapasDeHud : MonoBehaviour
    {
        public static CapasDeHud Instancia { get; private set; }

        public GameObject Hud_FPS;
        public GameObject Hud_RTS;
        public GameObject Hud_Menu;
        public GameObject Hud_Resultado;
        public GameObject Hud_Feedback;

        void Awake()
        {
            Instancia = this;
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        public static void ActivarModo(ControlMode modo)
        {
            if (Instancia == null) return;
            if (Instancia.Hud_FPS != null) Instancia.Hud_FPS.SetActive(modo == ControlMode.Fps);
            if (Instancia.Hud_RTS != null) Instancia.Hud_RTS.SetActive(modo == ControlMode.Rts);
        }
    }
}
