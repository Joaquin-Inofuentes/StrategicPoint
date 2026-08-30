using System;
using System.Collections.Generic;
using UnityEngine;
using SP.Core;
using SP.Actors;

namespace SP.Presentation
{
    // Escucha SelectionChangedEvent y mantiene un anillo pulsante por cada
    // soldado seleccionado, creándolos y destruyéndolos solo. No decide
    // nada de selección, solo la dibuja.
    public class SelectionRingManager : MonoBehaviour
    {
        static readonly Color RingColor = new Color(0.95f, 0.85f, 0.25f);

        readonly Dictionary<int, SelectionRingFx> rings = new Dictionary<int, SelectionRingFx>();
        IDisposable sub;

        void OnEnable() => sub = EventBus.Instance.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
        void OnDisable() => sub?.Dispose();

        void OnSelectionChanged(SelectionChangedEvent evt)
        {
            var idSet = new HashSet<int>(evt.SelectedIds);

            var toRemove = new List<int>();
            foreach (var kv in rings)
            {
                if (idSet.Contains(kv.Key)) continue;
                if (kv.Value != null) Destroy(kv.Value.gameObject);
                toRemove.Add(kv.Key);
            }
            foreach (var id in toRemove) rings.Remove(id);

            foreach (var id in idSet)
            {
                if (rings.ContainsKey(id)) continue;
                var soldier = ActorRegistry.FindById(id);
                if (soldier == null) continue;
                var ring = SelectionRingFx.Spawn(soldier.transform, RingColor);
                ring.TrackHealth(soldier);
                rings[id] = ring;
            }
        }
    }
}
