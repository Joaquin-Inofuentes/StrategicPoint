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
        public const float AlturaMinimaDeLoColgante = 4.5f, AlturaMinimaDeLuces = 2.8f;
        static SalaDespejadaEnRts instancia;
        public static int PiezasOcultas { get; private set; }
        readonly List<Renderer> piezas = new List<Renderer>(256);
        // R19 (F3): el modo de sombra que tenia cada pieza antes de ocultarla. Antes se volvia siempre a On, y las piezas de arte que nacen
        // con la sombra apagada (fachada R17/R19, adornos) quedaban proyectando sombra despues de la primera visita a la vista RTS.
        readonly List<ShadowCastingMode> sombrasOriginales = new List<ShadowCastingMode>(256);
        float proximo, proximaBusqueda; bool oculto;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { instancia = null; PiezasOcultas = 0; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            if (instancia != null) return;
            var go = new GameObject("SalaDespejadaEnRts") { hideFlags = HideFlags.HideInHierarchy };
            // 2026-11-13: sin esto el vigilante moria al cargar SC_Operacion desde el menu (la build real: "piezas ocultas = 0") y las vigas y luces
            // de la sala se veian siempre; solo funcionaba abriendo la escena directo en el Editor.
            DontDestroyOnLoad(go);
            instancia = go.AddComponent<SalaDespejadaEnRts>();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => { if (instancia != null) instancia.AlCargarEscena(); };
        }

        // Escena nueva: las piezas de la anterior ya no existen; se vuelve a buscar la proxima vez que se entre a RTS.
        void AlCargarEscena() { piezas.Clear(); sombrasOriginales.Clear(); oculto = false; proximaBusqueda = 0f; PiezasOcultas = 0; }

        // La sala del centro de datos: por jerarquia (3_CentroDeDatos / 9_Servidores) o por estar dentro de su planta (x -27..27, z -81..-39), porque parte del
        // arte de la sala cuelga de otros grupos (9_PulidoFinal, 9_ArteExtra).
        static bool EnLaSala(Renderer r)
        {
            for (var p = r.transform; p != null; p = p.parent) if (p.name == "3_CentroDeDatos" || p.name == "9_Servidores") return true;
            var c = r.bounds.center;
            return Mathf.Abs(c.x) <= 27f && c.z >= -81f && c.z <= -39f;
        }

        // Lo que cuelga del techo y se ve desde arriba aunque quede mas bajo que AlturaMinimaDeLoColgante (barras de luz de los muros a 3,2 m, paneles, ductos).
        static bool EsLuzOViga(string n)
        {
            return n.StartsWith("Panel_Luz", System.StringComparison.Ordinal) || n.StartsWith("LineaLuz", System.StringComparison.Ordinal) || n.StartsWith("Travesano", System.StringComparison.Ordinal)
                || n.StartsWith("Viga", System.StringComparison.Ordinal) || n.StartsWith("Ducto", System.StringComparison.Ordinal) || n.StartsWith("Bandeja", System.StringComparison.Ordinal)
                || n.StartsWith("Cable", System.StringComparison.Ordinal) || n.StartsWith("Rejilla_Aire", System.StringComparison.Ordinal) || n.StartsWith("Baliza", System.StringComparison.Ordinal)
                || n.StartsWith("Dintel_Luz", System.StringComparison.Ordinal) || n.StartsWith("Costilla", System.StringComparison.Ordinal);
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
                    if (r == null || r is ParticleSystemRenderer || r is LineRenderer || r is SkinnedMeshRenderer || !EnLaSala(r)) continue;
                    if (r.name.StartsWith("Techo", System.StringComparison.Ordinal)) continue;   // lo maneja TechoOcultable
                    float minY = r.bounds.min.y;
                    if (minY < AlturaMinimaDeLoColgante && !(minY >= AlturaMinimaDeLuces && EsLuzOViga(r.name))) continue;
                    piezas.Add(r);
                }
            }
            if (rts == oculto) return;
            oculto = rts;
            int n = 0;
            if (rts)
            {
                sombrasOriginales.Clear();
                for (int i = 0; i < piezas.Count; i++)
                {
                    var r = piezas[i];
                    sombrasOriginales.Add(r != null ? r.shadowCastingMode : ShadowCastingMode.On);
                    if (r == null) continue;
                    r.shadowCastingMode = ShadowCastingMode.ShadowsOnly; n++;
                }
            }
            else
            {
                for (int i = 0; i < piezas.Count; i++)
                {
                    var r = piezas[i];
                    if (r == null) continue;
                    var original = i < sombrasOriginales.Count ? sombrasOriginales[i] : ShadowCastingMode.On;
                    // Solo se restaura si sigue como la dejamos (otro sistema pudo haberla tocado mientras tanto). Si ya estaba oculta por otro
                    // (ShadowsOnly de antes), al volver se deja visible: este vigilante nunca la oculto de verdad y antes tambien la prendia.
                    if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly)
                        r.shadowCastingMode = original == ShadowCastingMode.ShadowsOnly ? ShadowCastingMode.On : original;
                    n++;
                }
            }
            PiezasOcultas = rts ? n : 0;
        }
    }
}
