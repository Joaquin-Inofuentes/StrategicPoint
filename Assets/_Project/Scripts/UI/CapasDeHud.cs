using UnityEngine;
using SP.CameraSystem;

namespace SP.UI
{
    public class CapasDeHud : MonoBehaviour
    {
        public static CapasDeHud Instancia { get; private set; }
        public static bool EnRts { get; private set; }

        public GameObject Hud_FPS;
        public GameObject Hud_RTS;
        public GameObject Hud_Menu;
        public GameObject Hud_Resultado;
        public GameObject Hud_Feedback;

        void Awake()
        {
            Instancia = this;
        }

        WeaponStatusView vistaArma;

        // En RTS no hay soldado poseido que actualice el panel de arma: muestra el arma del lider de la escuadra.
        void LateUpdate()
        {
            if (!EnRts || Hud_FPS == null) return;
            if (vistaArma == null) vistaArma = Hud_FPS.GetComponentInChildren<WeaponStatusView>(true);
            var vista = vistaArma;
            if (vista == null) return;
            var lider = SP.Ai.AjustesDeEscuadra.Lider;
            if (lider == null || lider.Health == null || !lider.Health.IsAlive)
            {
                lider = null;
                foreach (var s in SP.Core.ActorRegistry.All)
                    if (s != null && s.Team == SP.Combat.TeamId.Player && s.Role != SP.Combat.RoleType.Civilian && s.Health != null && s.Health.IsAlive) { lider = s; break; }
            }
            vista.UpdateFrom(lider != null ? lider.Weapon : null);
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        public static void ActivarModo(ControlMode modo)
        {
            if (Instancia == null) return;
            EnRts = modo == ControlMode.Rts;
            // En RTS el panel de arma/municion/granadas (WeaponStatus) tambien se queda visible: antes colgaba de Hud_FPS y desaparecia
            // (bug "UI perdida"). El resto de Hud_FPS se oculta por alfa, no por SetActive, porque otros sistemas lo prenden y apagan.
            if (Instancia.Hud_FPS != null)
            {
                Instancia.Hud_FPS.SetActive(true);
                bool fps = modo == ControlMode.Fps;
                foreach (Transform hijo in Instancia.Hud_FPS.transform)
                {
                    if (hijo.name == "WeaponStatus") continue;
                    // TryGetComponent y no "?? AddComponent": en el editor GetComponent devuelve un falso null que "??" no detecta.
                    if (!hijo.TryGetComponent<CanvasGroup>(out var g)) g = hijo.gameObject.AddComponent<CanvasGroup>();
                    g.alpha = fps ? 1f : 0f; g.blocksRaycasts = fps; g.interactable = fps;
                }
            }
            if (Instancia.Hud_RTS != null) Instancia.Hud_RTS.SetActive(modo == ControlMode.Rts);
        }
    }
}
