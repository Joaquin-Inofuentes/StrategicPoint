using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Ai;
using SP.Core;
using SP.Player;

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
            // El roster antes era solo un nombre y un color de fondo: no
            // decía la vida, ni el arma, ni si el soldado seguía vivo.
            // Para elegir a quién poseer con [F1]/[F2]/[F3] había que
            // adivinar o cruzar con el panel de abajo.
            public Soldier Soldier;
            public Text Label;
            public Image HealthFill;
            // Item pedido: indicador de que esta haciendo CADA soldado
            // (siguiendo / atacando / quieto), no solo su vida y arma.
            // Se cachea el AiBrain porque GetComponent en LateUpdate,
            // sobre 50 filas cada frame, es plata tirada.
            public AiBrain Brain;
        }

        readonly List<Row> rows = new List<Row>();
        readonly HashSet<int> selectedIds = new HashSet<int>();
        int possessedId = -1;

        // Antes 0.55 de alpha: sobre fondos claros (cielo, piso) o el
        // magenta de un shader faltante, el texto blanco se perdia. El
        // resto de colores (posesion/seleccion) ya estaban altos (0.9);
        // este era el unico bajo.
        static readonly Color normalColor = new Color(0f, 0f, 0f, 0.85f);
        static readonly Color possessedColor = new Color(0.15f, 0.55f, 0.85f, 0.9f);
        static readonly Color selectedColor = new Color(0.85f, 0.65f, 0.1f, 0.9f);

        IDisposable possessionSub, selectionSub;

        public void AddRow(Soldier soldier, Image background, Text label, Image healthFill = null)
        {
            rows.Add(new Row
            {
                SoldierId = soldier.Id,
                Background = background,
                Soldier = soldier,
                Label = label,
                HealthFill = healthFill,
                Brain = soldier.GetComponent<AiBrain>(),
            });
            label.text = $"{soldier.DisplayName} ({soldier.Role})";
            background.color = normalColor;
        }

        // Traduce el estado interno de AiBrain a las 3 categorias que se
        // pidieron (siguiendo / atacando / quieto), sin perder del todo
        // la info: un soldado en camino a una orden no es ni una ni otra,
        // asi que tiene su propia etiqueta en vez de mentir metiendolo en
        // "quieto".
        static string StateLabel(AiState state)
        {
            switch (state)
            {
                case AiState.Follow: return "Siguiendo";
                case AiState.Chase:
                case AiState.MovingToAttackOrder:
                case AiState.Attack: return "Atacando";
                case AiState.MovingToOrder: return "En camino";
                case AiState.Patrol:
                case AiState.Idle: return "Quieto";
                default: return "";
            }
        }

        static readonly Color deadColor = new Color(0.12f, 0.12f, 0.13f, 0.75f);
        static readonly Color deadTextColor = new Color(0.5f, 0.5f, 0.52f);

        // La vida y el arma cambian todo el tiempo, así que esta parte sí
        // va por frame (los colores de posesión/selección siguen yendo por
        // evento, que es cuando de verdad cambian).
        PlayerBrain brain;

        void LateUpdate()
        {
            // possessedId solo se enteraba por PossessionChangedEvent, que
            // se publica al CAMBIAR de soldado -- nunca en la posesión
            // inicial del arranque. Resultado: al empezar la partida
            // ninguna fila aparecía marcada como "este sos vos" hasta que
            // poseías a otro. Se lee del brain, que es la fuente real.
            if (brain == null) brain = FindAnyObjectByType<PlayerBrain>();
            if (brain != null && brain.Current != null && brain.Current.Id != possessedId)
            {
                possessedId = brain.Current.Id;
                Refresh();
            }

            foreach (var row in rows)
            {
                if (row.Soldier == null || row.Label == null) continue;

                bool alive = row.Soldier.Health != null && row.Soldier.Health.IsAlive;
                bool isPossessed = row.SoldierId == possessedId;

                // "►" delante del que estás manejando: el color de fondo
                // solo no alcanzaba para distinguirlo del seleccionado.
                string marker = isPossessed && alive ? "► " : "   ";
                string weapon = row.Soldier.Weapon != null ? row.Soldier.Weapon.CurrentWeaponKind.ToString() : "";
                // Al soldado poseido lo maneja el jugador, no su AiBrain
                // (que esta en pausa mientras dure la posesion): mostrarle
                // un estado de IA seria mentira, "(vos)" es lo honesto.
                string estado = isPossessed ? "(vos)" : (row.Brain != null ? StateLabel(row.Brain.State) : "");
                string estadoSuffix = string.IsNullOrEmpty(estado) ? "" : $"   ·   {estado}";

                row.Label.text = alive
                    ? $"{marker}{row.Soldier.DisplayName} ({row.Soldier.Role})\n     {row.Soldier.Health.Current}/{row.Soldier.Health.MaxHealth}   ·   {weapon}{estadoSuffix}"
                    : $"   {row.Soldier.DisplayName} — CAIDO";
                row.Label.color = alive ? Color.white : deadTextColor;

                if (row.HealthFill != null)
                {
                    row.HealthFill.gameObject.SetActive(alive);
                    if (alive && row.Soldier.Health.MaxHealth > 0)
                    {
                        float frac = (float)row.Soldier.Health.Current / row.Soldier.Health.MaxHealth;
                        row.HealthFill.fillAmount = frac;
                        row.HealthFill.color = frac > 0.6f ? new Color(0.35f, 0.85f, 0.4f)
                            : frac > 0.25f ? new Color(0.95f, 0.8f, 0.25f)
                            : new Color(0.95f, 0.25f, 0.2f);
                    }
                }

                // Un soldado muerto no puede estar poseído ni seleccionado:
                // su fila se apaga en gris y deja de competir por atención.
                if (!alive && row.Background != null) row.Background.color = deadColor;
            }
        }

        // `rows` se llena con AddRow() al armar la escena en el Editor,
        // pero es una lista de objetos C# comunes: NO sobrevive al domain
        // reload al entrar en Play mode. Quedaba vacía, así que ni el
        // resaltado del soldado poseído funcionaba en el juego real (se
        // veía bien solo en la escena de editor). Se reconstruye desde la
        // jerarquía: cada hijo "Row_<Nombre>" se vuelve a atar a su
        // soldado buscándolo por nombre en el registro.
        void OnEnable()
        {
            Initialize();
            if (rows.Count > 0) return;

            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("Row_")) continue;
                string soldierName = child.name.Substring(4);

                Soldier match = null;
                foreach (var s in ActorRegistry.All)
                    if (s != null && s.DisplayName == soldierName) { match = s; break; }
                if (match == null) continue;

                rows.Add(new Row
                {
                    SoldierId = match.Id,
                    Soldier = match,
                    Background = child.GetComponent<Image>(),
                    Label = child.Find("Label")?.GetComponent<Text>(),
                    HealthFill = child.Find("BarBG/BarFill")?.GetComponent<Image>(),
                    Brain = match.GetComponent<AiBrain>(),
                });
            }
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
                // Un caído no se resalta ni como poseído ni como
                // seleccionado: LateUpdate lo deja en gris y acá no hay
                // que volver a pintarlo de azul/amarillo por un evento.
                if (row.Soldier != null && row.Soldier.Health != null && !row.Soldier.Health.IsAlive)
                {
                    row.Background.color = deadColor;
                    continue;
                }
                bool isPossessed = row.SoldierId == possessedId;
                bool isSelected = selectedIds.Contains(row.SoldierId);
                row.Background.color = isPossessed ? possessedColor : (isSelected ? selectedColor : normalColor);
            }
        }

        public bool IsHighlighted(int soldierId) => soldierId == possessedId || selectedIds.Contains(soldierId);
    }
}
