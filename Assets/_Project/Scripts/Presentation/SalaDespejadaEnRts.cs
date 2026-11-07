using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SP.CameraSystem;

namespace SP.Presentation
{
    // Pedido 2026-11-07: "eliminar luces y vigas para vista desde arriba, en la zona de servidores y computadoras, SOLO en la vista RTS".
    // En RTS las piezas que cuelgan por encima de 4,5 m en la sala del centro de datos y la de servidores (paneles de luz, travesanos, vigas, ductos)
    // dejan de dibujarse (siguen proyectando sombra, no se tocan los colliders ni las luces reales: la sala sigue iluminada). En FPS/TPS se ven enteras.
    public sealed class SalaDespejadaEnRts : MonoBehaviour
    {
        public const float AlturaMinimaDeLoColgante = 4.5f;
        static SalaDespejadaEnRts instancia;
        public static int PiezasOcultas { get; private set; }
        readonly List<Renderer> piezas = new List<Renderer>(256);
        float proximo, proximaBusqueda; bool oculto;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { instancia = null; PiezasOcultas = 0; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            if (instancia != null) return;
            var go = new GameObject("SalaDespejadaEnRts") { hideFlags = HideFlags.HideInHierarchy };
            instancia = go.AddComponent<SalaDespejadaEnRts>();
        }

        static bool EnLaSala(Transform t)
        {
            for (var p = t; p != null; p = p.parent) if (p.name == "3_CentroDeDatos" || p.name == "9_Servidores") return true;
            return false;
        }

        void Update()
        {
            if (!Application.isPlaying || Time.unscaledTime < proximo) return;
            proximo = Time.unscaledTime + 0.25f;
            var rig = CameraRig.Instance;
            bool rts = rig != null && rig.Mode == ControlMode.Rts;
            if (!oculto && rts && Time.unscaledTime >= proximaBusqueda)
            {
                proximaBusqueda = Time.unscaledTime + 3f;
                piezas.Clear();
                foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                {
                    if (r == null || r is ParticleSystemRenderer || r is LineRenderer || !EnLaSala(r.transform)) continue;
                    if (r.name.StartsWith("Techo", System.StringComparison.Ordinal)) continue;   // lo maneja TechoOcultable
                    if (r.bounds.min.y < AlturaMinimaDeLoColgante) continue;
                    piezas.Add(r);
                }
            }
            if (rts == oculto) return;
            oculto = rts;
            var modo = rts ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            int n = 0;
            for (int i = 0; i < piezas.Count; i++) if (piezas[i] != null) { piezas[i].shadowCastingMode = modo; n++; }
            PiezasOcultas = rts ? n : 0;
        }
    }
}
