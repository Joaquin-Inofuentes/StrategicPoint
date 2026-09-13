using UnityEngine;
using SP.Core;

namespace SP.Combat
{
    public readonly struct WeaponPickedUpEvent
    {
        public readonly int SoldierId;
        public readonly WeaponKind Kind;
        public WeaponPickedUpEvent(int soldierId, WeaponKind kind)
        {
            SoldierId = soldierId;
            Kind = kind;
        }
    }

    // Cubo en el mundo que representa un arma. Al equiparla (E cerca), cambia
    // el arma del soldado: daño, cadencia y el color de lo que dispara.
    public class WeaponPickup : MonoBehaviour
    {
        [SerializeField] WeaponKind kind = WeaponKind.Rifle;
        [SerializeField] Color color = Color.white;

        // Guarda de reentrancia: EquipOn() hoy tiene un unico llamador
        // (PlayerInputDriver.Interactuar, gateado por una tecla de flanco),
        // pero la clase en si no ofrece NINGUNA defensa si el dia de
        // mañana aparece un segundo camino (ej. un boton de UI ademas de
        // la tecla de cercania). Sin esta guarda, dos llamadas superpuestas
        // a EquipOn() en el mismo pickup duplicarian EquipWeapon() y el
        // WeaponPickedUpEvent.
        bool equipping;

        public WeaponKind Kind => kind;
        public Color Color => color;

        // Antes tomaba tambien damage/cooldown y los guardaba en campos
        // propios -- muertos desde que EquipOn() (mas abajo) empezo a leer
        // SIEMPRE del catalogo fresco (ver su comentario "BUG REAL"). Un
        // llamador podia pasar cualquier numero aca y no cambiaba nada en
        // el juego, una trampa para el proximo rebalanceo. Kind y Color
        // son los dos unicos datos que este pickup necesita: cual arma da
        // y de que color se pinta el cubo en el piso.
        public void Configure(WeaponKind weaponKind, Color weaponColor)
        {
            kind = weaponKind;
            color = weaponColor;
        }

        public void EquipOn(WeaponHolder holder, int soldierId)
        {
            if (holder == null || equipping) return;
            equipping = true;
            try
            {
                // BUG REAL encontrado al rebalancear la pistola (WeaponCatalog:
                // Damage 14 -> 8): este pickup seguia entregando 14 igual,
                // porque damage/cooldown/color son una FOTO tomada por
                // Configure() y guardada aparte en la escena -- exactamente
                // lo que el comentario de WeaponCatalog promete que NO pasa
                // ("recogibles del piso... usan los mismos valores"). Cualquier
                // rebalanceo futuro se hubiera perdido en silencio para todo
                // pickup ya horneado en SC_Gameplay o SC_TestLevel. Ahora la
                // unica fuente es el catalogo, siempre fresca.
                var spec = WeaponCatalog.Get(kind);
                holder.EquipWeapon(kind, spec.Damage, spec.Cooldown, spec.Color);
                EventBus.Instance.Publish(new WeaponPickedUpEvent(soldierId, kind));
            }
            finally
            {
                equipping = false;
            }
        }
    }
}
