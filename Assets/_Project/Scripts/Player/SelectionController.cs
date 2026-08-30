using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Core;
using SP.Vehicles;

namespace SP.Player
{
    // Selección múltiple en vista RTS.
    public class SelectionController : MonoBehaviour
    {
        readonly List<Soldier> selected = new List<Soldier>();
        public IReadOnlyList<Soldier> Selected => selected;

        // El vehículo es seleccionable, pero por separado de los soldados
        // (mutuamente excluyente, como en cualquier RTS: no tiene sentido
        // arrastrar una selección mixta de tropas + tanque).
        public Vehicle SelectedVehicle { get; private set; }

        public void SelectSingle(Soldier s)
        {
            SelectedVehicle = null;
            selected.Clear();
            selected.Add(s);
            Publish();
        }

        public void AddToSelection(Soldier s)
        {
            SelectedVehicle = null;
            if (!selected.Contains(s)) selected.Add(s);
            Publish();
        }

        public void SelectVehicle(Vehicle v)
        {
            selected.Clear();
            SelectedVehicle = v;
            Publish();
        }

        public void Clear()
        {
            selected.Clear();
            SelectedVehicle = null;
            Publish();
        }

        void Publish()
        {
            var ids = new List<int>();
            foreach (var s in selected) ids.Add(s.Id);
            EventBus.Instance.Publish(new SelectionChangedEvent(ids));
        }
    }
}
