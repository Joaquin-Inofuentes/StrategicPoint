using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using SP.Mision;

namespace SP.Presentation
{
    // Pedido explicito: "quiero q al mantener C se muestre un destino, una
    // linea del camino hacia el objetivo". A diferencia de
    // ObjectiveArrowIndicator (una flecha fija a los pies que siempre esta
    // prendida y solo señala en linea recta), esto es bajo demanda -- se
    // mantiene [C] apretada -- y sigue el camino REAL del NavMesh (rodea
    // obstaculos, no atraviesa muros).
    public class RutaAlObjetivo : MonoBehaviour
    {
        LineRenderer linea;
        NavMeshPath path;
        float proximoRecalculo;
        const float IntervaloRecalculo = 0.25f;
        const float AlturaSobrePiso = 0.2f;

        public static RutaAlObjetivo Crear()
        {
            var go = new GameObject("RutaAlObjetivo");
            return go.AddComponent<RutaAlObjetivo>();
        }

        void Awake()
        {
            path = new NavMeshPath();
            linea = gameObject.AddComponent<LineRenderer>();
            linea.material = SafeMaterial.Create(new Color(1f, 0.82f, 0.3f, 0.85f));
            linea.widthMultiplier = 0.18f;
            linea.numCornerVertices = 2;
            linea.textureMode = LineTextureMode.Tile;
            linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            linea.receiveShadows = false;
            linea.useWorldSpace = true;
            linea.enabled = false;
        }

        void Update()
        {
            var kb = Keyboard.current;
            bool sostenida = kb != null && kb.cKey.isPressed;
            if (!sostenida) { if (linea.enabled) linea.enabled = false; return; }

            if (MisionDirector.Instancia == null) { linea.enabled = false; return; }

            proximoRecalculo -= Time.deltaTime;
            if (proximoRecalculo > 0f && linea.enabled) return;
            proximoRecalculo = IntervaloRecalculo;

            var origen = MisionDirector.Instancia.PosicionDelJugador();
            if (origen == Vector3.zero) { linea.enabled = false; return; }
            var destino = MisionDirector.Instancia.PuntoObjetivoActual();

            if (!NavMesh.SamplePosition(origen, out var hitOrigen, 4f, NavMesh.AllAreas)) { linea.enabled = false; return; }
            if (!NavMesh.SamplePosition(destino, out var hitDestino, 8f, NavMesh.AllAreas)) { linea.enabled = false; return; }
            if (!NavMesh.CalculatePath(hitOrigen.position, hitDestino.position, NavMesh.AllAreas, path) || path.corners.Length < 2)
            {
                linea.enabled = false;
                return;
            }

            linea.enabled = true;
            linea.positionCount = path.corners.Length;
            for (int i = 0; i < path.corners.Length; i++)
                linea.SetPosition(i, path.corners[i] + Vector3.up * AlturaSobrePiso);
        }
    }
}
