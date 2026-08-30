using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Core;

namespace SP.UI
{
    // Roster de la escuadra: resalta al soldado poseído y a los seleccionados
    // en vista RTS. Solo escucha el bus, nunca decide nada de gameplay.
    public class SelectedSoldierUI : MonoBehaviour
    {
        class Row
        {
            public int SoldierId;
            public Image Background;
        }

        readonly List<Row> rows = new List<Row>();
        readonly HashSet<int> selectedIds = new HashSet<int>();
        int possessedId = -1;

        static readonly Color normalColor = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color possessedColor = new Color(0.15f, 0.55f, 0.85f, 0.9f);
        static readonly Color selectedColor = new Color(0.85f, 0.65f, 0.1f, 0.9f);

        IDisposable possessionSub, selectionSub;

        public void AddRow(Soldier soldier, Image background, Text label)
        {
            rows.Add(new Row { SoldierId = soldier.Id, Background = background });
            label.text = $"{soldier.DisplayName} ({soldier.Role})";
            background.color = normalColor;
        }

        // Se llama explícitamente al construir la UI. No depende de OnEnable:
        // en modo Edit (fuera de Play) esa suscripción implícita no es fiable.
        public void Initialize()
        {
            possessionSub?.Dispose();
            selectionSub?.Dispose();
            possessionSub = EventBus.Instance.Subscribe<PossessionChangedEvent>(OnPossession);
            selectionSub = EventBus.Instance.Subscribe<SelectionChangedEvent>(OnSelection);
        }

        void OnDestroy()
        {
            possessionSub?.Dispose();
            selectionSub?.Dispose();
        }

        void OnPossession(PossessionChangedEvent evt)
        {
            possessedId = evt.ToId;
            Refresh();
        }

        void OnSelection(SelectionChangedEvent evt)
        {
            selectedIds.Clear();
            if (evt.SelectedIds != null)
                foreach (var id in evt.SelectedIds) selectedIds.Add(id);
            Refresh();
        }

        void Refresh()
        {
            foreach (var row in rows)
            {
                bool isPossessed = row.SoldierId == possessedId;
                bool isSelected = selectedIds.Contains(row.SoldierId);
                row.Background.color = isPossessed ? possessedColor : (isSelected ? selectedColor : normalColor);
            }
        }

        public bool IsHighlighted(int soldierId) => soldierId == possessedId || selectedIds.Contains(soldierId);
    }
}
