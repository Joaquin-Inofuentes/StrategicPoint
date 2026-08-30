using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Core;

namespace SP.Player
{
    // Selección múltiple en vista RTS.
    public class SelectionController : MonoBehaviour
    {
        readonly List<Soldier> selected = new List<Soldier>();
        public IReadOnlyList<Soldier> Selected => selected;

        public void SelectSingle(Soldier s)
        {
            selected.Clear();
            selected.Add(s);
            Publish();
        }

        public void AddToSelection(Soldier s)
        {
            if (!selected.Contains(s)) selected.Add(s);
            Publish();
        }

        public void Clear()
        {
            selected.Clear();
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
