using UnityEngine;
using UnityEngine.Rendering;
using SP.CameraSystem;

namespace SP.Presentation
{
    // Bug #289: la sala del centro de datos ahora tiene techo. Para que no tape la vista tactica (RTS, camara cenital) ni una camara de tercera persona que
    // se aleje por encima del techo, el techo deja de dibujarse (sigue proyectando sombra y con su collider) en esos casos. En FPS y TPS normal se ve entero.
    // Se evalua cada 0,25 s; no hay GetComponent por cuadro.
    public sealed class TechoOcultable : MonoBehaviour
    {
        public const float AlturaDeCamaraParaOcultar = 8.6f;
        MeshRenderer[] renders;
        float proximo;
        public bool Oculto { get; private set; }

        void Awake() { renders = GetComponentsInChildren<MeshRenderer>(true); }

        void Update()
        {
            if (!Application.isPlaying || Time.unscaledTime < proximo) return;
            proximo = Time.unscaledTime + 0.25f;
            if (renders == null || renders.Length == 0) renders = GetComponentsInChildren<MeshRenderer>(true);
            var rig = CameraRig.Instance;
            bool rts = rig != null && rig.Mode == ControlMode.Rts;
            var cam = SP.Core.CamaraPrincipal.Actual;
            bool arriba = cam != null && cam.transform.position.y > AlturaDeCamaraParaOcultar;
            bool ocultar = rts || arriba;
            if (ocultar == Oculto) return;
            Oculto = ocultar;
            var modo = ocultar ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            for (int i = 0; i < renders.Length; i++) if (renders[i] != null) renders[i].shadowCastingMode = modo;
        }
    }
}
