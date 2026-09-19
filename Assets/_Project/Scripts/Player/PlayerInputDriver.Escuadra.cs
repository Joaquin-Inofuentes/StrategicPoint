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
