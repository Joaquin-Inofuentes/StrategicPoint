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

        // MODO TANQUE: dentro del tanque solo se ven los 3 soldados con su vida (Roster), la vida del tanque y la municion del canon.
        // LISTA BLANCA: todo lo que cuelga del Canvas principal se apaga por alfa salvo lo permitido, asi cualquier panel nuevo (objetivo,
        // guia, minimapa, avisos, kill feed, teclas...) queda oculto sin tener que nombrarlo. Se restaura al bajar.
        public static bool EnModoTanque { get; private set; }
        // Bug #374 (2026-11-08): en el tanque solo la barra de vida y una barra de recarga fina (HudDeTanqueSimple); se quitan "Roster" (aliados) y "VehicleInfoPanel".
        // Bug #377 (2026-11-08: "falta el minimapa"): el minimapa SI se ve dentro del tanque.
        static readonly string[] PermitidasEnTanque = { "MinimapBorder", "Hud_FPS", "TurretAim", "DamageVignette", "LowHealthPulse", "ScreenFlash", "DamageDirection", "GameOutcome", "PauseController", "EventSystem", "DeadNotice", "Crosshair", "AimUI" };
        static readonly string[] CanvasSueltosOcultos = { "SesionLog_Indicador", "AvisoUnico" };
        static readonly string[] HijosDeHudFpsOcultos = { "VehicleSeatBadge", "VehicleCrewPanel", "OffscreenKillMarker", "OffscreenAllies", "PlayerHealth", "WeaponStatus" };
        static readonly System.Collections.Generic.List<(CanvasGroup g, float alfa)> apagadas = new System.Collections.Generic.List<(CanvasGroup, float)>();
        static readonly System.Collections.Generic.List<Canvas> canvasApagados = new System.Collections.Generic.List<Canvas>();

        public static void ModoTanque(bool activo)
        {
            if (Instancia == null) return;
            if (activo == EnModoTanque)
            {
                if (activo)
                {
                    foreach (var (g, _) in apagadas) if (g != null) g.alpha = 0f;   // los sistemas que animan su alfa lo pisan: se reaplica
                    // Paneles que se crean DESPUES de subir (el de teclas, la insignia de asiento): se apagan apenas aparecen.
                    var cv2 = Instancia.GetComponentInParent<Canvas>();
                    if (cv2 != null) foreach (Transform hijo in cv2.transform)
                    {
                        if (System.Array.IndexOf(PermitidasEnTanque, hijo.name) >= 0) continue;
                        if (!hijo.TryGetComponent<CanvasGroup>(out var g2)) g2 = hijo.gameObject.AddComponent<CanvasGroup>();
                        if (g2.alpha == 0f) continue;
                        apagadas.Add((g2, g2.alpha)); g2.alpha = 0f;
                    }
                }
                return;
            }
            EnModoTanque = activo;
            if (!activo) HudDeTanqueSimple.Ocultar();
            if (activo)
            {
                apagadas.Clear(); canvasApagados.Clear();
                void Apagar(GameObject go)
                {
                    if (go == null) return;
                    if (!go.TryGetComponent<CanvasGroup>(out var g)) g = go.AddComponent<CanvasGroup>();
                    apagadas.Add((g, g.alpha)); g.alpha = 0f;
                }
                var cv = Instancia.GetComponentInParent<Canvas>();
                if (cv != null)
                {
                    foreach (Transform hijo in cv.transform)
                    {
                        // Tambien los inactivos: el panel de teclas y la insignia de asiento se activan DESPUES de subir.
                        if (System.Array.IndexOf(PermitidasEnTanque, hijo.name) < 0) Apagar(hijo.gameObject);
                    }
                    var fps = Instancia.Hud_FPS;
                    if (fps != null) foreach (Transform hijo in fps.transform)
                        if (System.Array.IndexOf(HijosDeHudFpsOcultos, hijo.name) >= 0) Apagar(hijo.gameObject);
                }
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                    if (c.isRootCanvas && c.enabled && System.Array.IndexOf(CanvasSueltosOcultos, c.name) >= 0) { c.enabled = false; canvasApagados.Add(c); }
            }
            else
            {
                foreach (var (g, a) in apagadas) if (g != null) g.alpha = a;
                foreach (var c in canvasApagados) if (c != null) c.enabled = true;
                apagadas.Clear(); canvasApagados.Clear();
            }
        }

        // En RTS no hay soldado poseido que actualice el panel de arma: muestra el arma del lider de la escuadra.
        void LateUpdate()
        {
            if (!EnRts || Hud_FPS == null) return;
            if (vistaArma == null) vistaArma = Hud_FPS.GetComponentInChildren<WeaponStatusView>(true);
            var vista = vistaArma;
            if (vista == null) return;
            // Bug #324: el panel (granadas y municion) sigue al soldado SELECCIONADO; sin seleccion, al lider.
            var lider = SoldadoSeleccionado() ?? SP.Ai.AjustesDeEscuadra.Lider;
            if (lider == null || lider.Health == null || !lider.Health.IsAlive)
            {
                lider = null;
                foreach (var s in SP.Core.ActorRegistry.All)
                    if (s != null && s.Team == SP.Combat.TeamId.Player && s.Role != SP.Combat.RoleType.Civilian && s.Health != null && s.Health.IsAlive) { lider = s; break; }
            }
            vista.UpdateFrom(lider != null ? lider.Weapon : null);
        }

        public static SP.Actors.Soldier SoldadoSeleccionado()
        {
            var drv = SP.Player.PlayerInputDriver.Activo;
            if (drv == null || drv.Selection == null) return null;
            foreach (var s in drv.Selection.Selected) if (s != null && s.Health != null && s.Health.IsAlive) return s;
            return null;
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
