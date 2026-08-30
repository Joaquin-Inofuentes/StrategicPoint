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

        // Para seleccionar a todos hoy hay que arrastrar un cuadro que los
        // abarque, lo que obliga a panear la camara hasta encuadrarlos.
        // Es el comando mas repetido de cualquier RTS y era el mas
        // incomodo del juego.
        public void SelectAll(IEnumerable<Soldier> squad)
        {
            SelectedVehicle = null;
            selected.Clear();
            foreach (var s in squad)
                if (s != null && s.Health != null && s.Health.IsAlive) selected.Add(s);
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
