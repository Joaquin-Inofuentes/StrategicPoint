using UnityEngine;
using UnityEngine.InputSystem;
using SP.Actors;
using SP.UI;

namespace SP.Player
{
    // PlayerInputDriver (parte): orden de la escuadra. El lugar en la lista fija a que numero de roster y a que [F1..F9]
    // responde cada soldado; [ y ] suben o bajan al soldado que manejas.
    public partial class PlayerInputDriver
    {
        void TickReordenarEscuadra(Keyboard kb)
        {
            if (kb.leftBracketKey.wasPressedThisFrame) MoverEnEscuadra(Brain != null ? Brain.Current : null, -1);
            if (kb.rightBracketKey.wasPressedThisFrame) MoverEnEscuadra(Brain != null ? Brain.Current : null, +1);
        }

        float proximoAvisoArsenal;

        // [,] y [.] cambian el arma principal (ranura 1) cuando estas junto a una caja de suministros.
        void TickArsenal(Keyboard kb)
        {
            var yo = Brain != null ? Brain.Current : null;
            if (yo == null || yo.Weapon == null) return;
            bool cerca = CajaDeSuministros.MasCercana(yo.transform.position, CajaDeSuministros.RadioDeArsenal) != null;
            if (cerca && Time.time >= proximoAvisoArsenal)
            {
                proximoAvisoArsenal = Time.time + 12f;
                SP.UI.AlertQueue.Push("ARSENAL: [,] [.] CAMBIAN EL ARMA PRINCIPAL", SP.UI.AlertPriority.Baja, 3f);
            }
            int dir = kb.commaKey.wasPressedThisFrame ? -1 : kb.periodKey.wasPressedThisFrame ? +1 : 0;
            if (dir == 0) return;
            if (!cerca) { SP.UI.AlertQueue.Push("ACERCATE A UNA CAJA DE SUMINISTROS PARA CAMBIAR DE ARMA", SP.UI.AlertPriority.Media, 2f); return; }
            if (yo.Weapon.CambiarArmaPrincipal(dir))
                SP.UI.AlertQueue.Push("ARMA PRINCIPAL: " + yo.Weapon.CurrentWeaponKind.ToString().ToUpperInvariant(), SP.UI.AlertPriority.Media, 2f);
        }

        // Intercambia al soldado con su vecino (direccion -1 = hacia arriba). Devuelve si cambio algo.
        public bool MoverEnEscuadra(Soldier s, int direccion)
        {
            if (Squad == null || s == null) return false;
            int i = Squad.IndexOf(s);
            int j = i + direccion;
            if (i < 0 || j < 0 || j >= Squad.Count) return false;
            (Squad[i], Squad[j]) = (Squad[j], Squad[i]);
            var roster = FindAnyObjectByType<RosterView>();
            if (roster != null) roster.Rebuild();
            SP.UI.AlertQueue.Push($"{s.DisplayName.ToUpperInvariant()} ES AHORA EL Nº {j + 1}", SP.UI.AlertPriority.Baja, 1.6f);
            return true;
        }
    }
}
