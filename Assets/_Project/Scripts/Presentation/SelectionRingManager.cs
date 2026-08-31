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

        IDisposable ackSub;

        void OnEnable()
        {
            sub = EventBus.Instance.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
            ackSub = EventBus.Instance.Subscribe<OrderAcknowledgedEvent>(OnOrderAcknowledged);
        }

        void OnDisable()
        {
            sub?.Dispose();
            ackSub?.Dispose();
        }

        // Solo destellan los anillos de quienes recibieron la orden: si la
        // seleccion no era la que el jugador creia, se ve en el acto.
        void OnOrderAcknowledged(OrderAcknowledgedEvent evt)
        {
            if (evt.ActorIds == null) return;
            foreach (var id in evt.ActorIds)
                if (rings.TryGetValue(id, out var ring) && ring != null) ring.FlashAcknowledge();
        }

        void OnSelectionChanged(SelectionChangedEvent evt)
        {
            var idSet = new HashSet<int>(evt.SelectedIds);

            // Pool en vez de destruir y recrear. Antes cada cambio de
            // seleccion destruia TODOS los anillos y los volvia a crear:
            // con 50 unidades eran 50 primitivas nuevas por cada clic.
            // Ahora los que sobran se guardan desactivados y se reusan.
            var toRemove = new List<int>();
            foreach (var kv in rings)
            {
                if (idSet.Contains(kv.Key)) continue;
                if (kv.Value != null) Recycle(kv.Value);
                toRemove.Add(kv.Key);
            }
            foreach (var id in toRemove) rings.Remove(id);

            foreach (var id in idSet)
            {
                if (rings.ContainsKey(id)) continue;
                var soldier = ActorRegistry.FindById(id);
                if (soldier == null) continue;
                var ring = Rent(soldier.transform);
                ring.TrackHealth(soldier);
                rings[id] = ring;
            }
        }
        // Anillos libres, desactivados. ResetIfStale no hace falta porque el
        // manager entero se recrea con la escena, pero la lista se limpia de
        // referencias muertas por las dudas (un anillo puede haber sido
        // destruido por fuera, p.ej. al cambiar de escena).
        readonly List<SelectionRingFx> pool = new List<SelectionRingFx>();

        SelectionRingFx Rent(Transform target)
        {
            while (pool.Count > 0)
            {
                var candidate = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
                if (candidate == null) continue;
                candidate.gameObject.SetActive(true);
                candidate.Target = target;
                candidate.SetColor(RingColor);
                return candidate;
            }
            return SelectionRingFx.Spawn(target, RingColor);
        }

        void Recycle(SelectionRingFx ring)
        {
            if (ring == null) return;
            ring.Target = null;
            ring.TrackHealth(null);
            ring.gameObject.SetActive(false);
            pool.Add(ring);
        }
    }
}