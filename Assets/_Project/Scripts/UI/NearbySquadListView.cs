using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Player;

namespace SP.UI
{
    // Lista con scroll de los soldados de la escuadra: nombre, especialidad,
    // barra de vida real (no solo el número) y distancia a donde estás
    // parado ahora. Un aliado caído desaparece de la lista en vez de
    // quedar listado con una etiqueta "(caído)".
    // Se auto-empareja con el registro de soldados en Start: la lista
    // armada a mano en el editor no sobrevive a un domain reload.
    public class NearbySquadListView : MonoBehaviour
    {
        class Row
        {
            public Soldier Soldier;
            public GameObject RowObject;
            public Text Label;
            public Image HealthFill;
        }

        readonly List<Row> rows = new List<Row>();
        PlayerBrain brain;

        public void AddEntry(Soldier soldier, GameObject rowObject, Text label, Image healthFill)
        {
            rows.Add(new Row { Soldier = soldier, RowObject = rowObject, Label = label, HealthFill = healthFill });
        }

        const string RowPrefix = "NearbyRow_";

        void Start()
        {
            brain = FindFirstObjectByType<PlayerBrain>();

            if (rows.Count > 0) return;

            var content = transform.Find("Viewport/Content");
            if (content == null) return;

            foreach (Transform rowT in content)
            {
                if (!rowT.name.StartsWith(RowPrefix)) continue;
                string soldierName = rowT.name.Substring(RowPrefix.Length);

                Soldier match = null;
                foreach (var s in ActorRegistry.All)
                    if (s != null && s.Team == TeamId.Player && s.DisplayName == soldierName) { match = s; break; }
                if (match == null) continue;

                rows.Add(new Row
                {
                    Soldier = match,
                    RowObject = rowT.gameObject,
                    Label = rowT.Find("Label")?.GetComponent<Text>(),
                    HealthFill = rowT.Find("HealthBG/HealthFill")?.GetComponent<Image>(),
                });
            }
        }

        void LateUpdate()
        {
            if (brain == null) brain = FindFirstObjectByType<PlayerBrain>();
            Vector3 fromPos = brain != null && brain.Current != null ? brain.Current.transform.position : transform.position;

            foreach (var row in rows)
            {
                if (row.Soldier == null || row.Label == null) continue;

                if (!row.Soldier.Health.IsAlive)
                {
                    if (row.RowObject != null) row.RowObject.SetActive(false);
                    continue;
                }
                if (row.RowObject != null) row.RowObject.SetActive(true);

                float dist = Vector3.Distance(fromPos, row.Soldier.transform.position);
                row.Label.text = $"{row.Soldier.DisplayName} - {row.Soldier.Role}\n{row.Soldier.Health.Current}/{row.Soldier.Health.MaxHealth} vida · {dist:0.0} m";

                if (row.HealthFill != null && row.Soldier.Health.MaxHealth > 0)
                    row.HealthFill.fillAmount = (float)row.Soldier.Health.Current / row.Soldier.Health.MaxHealth;
            }
        }
    }
}
