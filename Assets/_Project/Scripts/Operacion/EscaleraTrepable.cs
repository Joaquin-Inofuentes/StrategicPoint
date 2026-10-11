using UnityEngine;
using SP.Actors;
using SP.Player;
using SP.UI;

namespace SP.Operacion
{
    // Bug #141 ("bug de escalera"): la escalera de la torreta de artilleria era solo decorado (aparecia el icono de trepar y no pasaba nada). Ahora
    // se sube y se baja: al pie, [E] trepa hasta la plataforma (el soldado sube parado, sin arco, 1,8 s); arriba, cerca del borde, [E] baja.
    // El motor camina en plano, asi que la trepa es un movimiento guiado (SoldierMotor.IniciarTrepaVertical), como el salto sobre un cajon.
    public class EscaleraTrepable : MonoBehaviour
    {
        public Vector3 pie;     // al ras del piso, delante de los travesaños (plano)
        public Vector3 cima;    // sobre la plataforma, junto al borde (altura = pivote del soldado parado)
        public const float AlcanceHorizontal = 1.5f, SegundosDeSubida = 1.8f;
        TorreDestruible torre;

        void Awake() { torre = GetComponentInParent<TorreDestruible>(); }

        void Update()
        {
            if (torre != null && torre.EstaCaida) return;
            var drv = PlayerInputDriver.Activo;
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            if (yo == null || yo.Motor == null || yo.Health == null || !yo.Health.IsAlive || yo.Motor.Vaulting || yo.Motor.IsJumping || drv.CurrentSeat.HasValue) return;
            var p = yo.transform.position;
            bool abajo = p.y < cima.y - 2.5f;
            var objetivo = abajo ? pie : cima;
            var d = objetivo - p; d.y = 0f;
            if (d.magnitude > AlcanceHorizontal) return;
            if (!abajo && p.y < cima.y - 1f) return;   // en el aire / sobre otra cosa
            AvisoUnico.Mostrar(abajo ? "[E] TREPAR LA ESCALERA" : "[E] BAJAR POR LA ESCALERA", 0.25f, new Color(1f, 0.9f, 0.6f), AvisoUnico.PrioridadBaja);
            if (!KeyBindings.WasPressed(KeyBindings.Interactuar)) return;
            var destino = abajo ? cima : new Vector3(pie.x, pie.y + 0.8f, pie.z);
            yo.Motor.IniciarTrepaVertical(destino, SegundosDeSubida);
        }
    }
}
