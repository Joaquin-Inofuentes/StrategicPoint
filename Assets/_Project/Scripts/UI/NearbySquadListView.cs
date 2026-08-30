using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.UI
{
    // Lista con scroll de los soldados de la escuadra y el atajo para
    // apuntarlos y poseerlos. Abajo a la izquierda de la pantalla.
    // Se auto-empareja con el registro de soldados en Start: la lista
    // armada a mano en el editor no sobrevive a un domain reload.
    public class NearbySquadListView : MonoBehaviour
    {
        class Row
        {
            public Soldier Soldier;
            public Text Label;
        }

        readonly List<Row> rows = new List<Row>();

        public void AddEntry(Soldier soldier, Text label)
        {
            rows.Add(new Row { Soldier = soldier, Label = label });
        }

        void Start()
        {
            if (rows.Count > 0) return;

            var labels = GetComponentsInChildren<Text>(true);
            var playerSoldiers = new List<Soldier>();
            foreach (var s in ActorRegistry.All)
                if (s.Team == TeamId.Player) playerSoldiers.Add(s);

            int n = Mathf.Min(labels.Length, playerSoldiers.Count);
            for (int i = 0; i < n; i++) rows.Add(new Row { Soldier = playerSoldiers[i], Label = labels[i] });
        }

        void LateUpdate()
        {
            foreach (var row in rows)
            {
                if (row.Soldier == null || row.Label == null) continue;
                string aliveTag = row.Soldier.Health.IsAlive ? "" : " (caido)";
                row.Label.text = $"{row.Soldier.DisplayName} - {row.Soldier.Role}{aliveTag}\n{row.Soldier.Health.Current}/{row.Soldier.Health.MaxHealth} vida  ·  [F] apuntar + poseer";
            }
        }
    }
}
