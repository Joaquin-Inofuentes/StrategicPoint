using UnityEngine;

namespace SP.Combat
{
    // Marcador (#409/#410, "bug de colliders de disparos"): el collider SIGUE frenando el movimiento (NavService.BlocksMovement no cambia) pero las
    // balas, los disparos de la mira y las explosiones lo atraviesan. Lo llevan las rejas invisibles de las compuertas de las torres de la mansion:
    // no se ven y no tienen que frenar el tiro ni dejar calcomanias flotando en el aire.
    public sealed class NoFrenaBalas : MonoBehaviour
    {
        public static bool Tiene(Collider c) => c != null && c.GetComponentInParent<NoFrenaBalas>() != null;
    }
}
