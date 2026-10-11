using UnityEngine;
using SP.Actors;
using SP.CameraSystem;
using SP.Player;

namespace SP.Presentation
{
    // Bug #220 ("mejora la toma de camara"): al ganar, la camara quedaba pegada al hombro del soldado que manejabas. Ahora hace una
    // toma de cierre: orbita despacio alrededor de la escuadra (plano medio, algo en contrapicado) mientras corre la pantalla de
    // victoria. Usa el tiempo SIN escala porque la victoria corre con timeScale en camara lenta o en cero.
    public class TomaDeVictoria : MonoBehaviour
    {
        public const float Radio = 9f, Altura = 2.6f, GradosPorSegundo = 9f, AlturaDelFoco = 1.2f;

        public static TomaDeVictoria Activa { get; private set; }

        CameraRig rig;
        Camera cam;
        Vector3 foco;
        float angulo;
        bool rigEstabaActivo;

        public static void Iniciar()
        {
            if (Activa != null) return;
            var go = new GameObject("TomaDeVictoria") { hideFlags = HideFlags.DontSave };
            Activa = go.AddComponent<TomaDeVictoria>();
        }

        public static void Detener()
        {
            if (Activa != null) Destroy(Activa.gameObject);
        }

        void Start()
        {
            rig = CameraRig.Instance;
            cam = SP.Core.CamaraPrincipal.Actual;
            if (rig == null || cam == null) { Destroy(gameObject); return; }
            rigEstabaActivo = rig.enabled;
            rig.enabled = false;
            foco = CentroDeLaEscuadra();
            // Arranca desde donde ya miraba la camara: sin salto brusco.
            var d = cam.transform.position - foco; d.y = 0f;
            angulo = d.sqrMagnitude > 0.01f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : 0f;
        }

        static Vector3 CentroDeLaEscuadra()
        {
            var drv = PlayerInputDriver.Activo;
            Vector3 suma = Vector3.zero; int n = 0;
            if (drv != null && drv.Squad != null)
                foreach (var s in drv.Squad)
                    if (s != null && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy) { suma += s.transform.position; n++; }
            if (n == 0 && drv != null && drv.Brain != null && drv.Brain.Current != null) { suma = drv.Brain.Current.transform.position; n = 1; }
            return n > 0 ? suma / n : Vector3.zero;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            // El foco sigue al grupo con suavidad (se mueven aunque sea poco).
            foco = Vector3.Lerp(foco, CentroDeLaEscuadra(), 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
            angulo += GradosPorSegundo * Time.unscaledDeltaTime;
            float rad = angulo * Mathf.Deg2Rad;
            var objetivo = foco + Vector3.up * AlturaDelFoco;
            var pos = foco + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * Radio + Vector3.up * (Altura + 0.5f * Mathf.Sin(Time.unscaledTime * 0.3f));
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(objetivo - pos, Vector3.up);
        }

        void OnDestroy()
        {
            if (Activa == this) Activa = null;
            if (rig != null && rigEstabaActivo) rig.enabled = true;
        }
    }
}
