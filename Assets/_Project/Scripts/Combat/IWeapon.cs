using UnityEngine;

namespace SP.Combat
{
    // Contrato mínimo de un arma equipable. Un arma cuerpo a cuerpo puede
    // implementar esto sin implementar recarga (eso vive en otra interfaz).
    public interface IWeapon
    {
        bool TryFire(Vector3 origin, Vector3 direction);
        float CooldownRemaining { get; }
    }
}
