using System;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;
using SP.Player;

namespace SP.UI
{
    // Una fila del roster = un prefab por soldado. Pedido explicito: "un
    // prefab para cada caso... con puntero al soldado... sincronizado en
    // tiempo real por eventos". Reemplaza a SelectedSoldierUI para la
    // escena real (esa clase sigue viva solo para el roster propio de
    // HeadlessTestRunner -- ver el comentario en SelectedSoldierUI.cs):
    // antes UN script central re-emparejaba filas por nombre
    // ("Row_<Nombre>") y refrescaba TODAS por LateUpdate cada frame; ahora
    // cada fila tiene su propio Soldier (asignado una vez en Bind) y
    // reacciona sola, por evento, solo cuando ESE soldado cambia.
    public class RosterRowView : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] Text label;
        [SerializeField] Image healthFill;

        // El "puntero al soldado" pedido explicitamente: quien tenga la
        // fila puede leer directo del soldado real, no solo de lo que la
        // fila decide mostrar.
        public Soldier Soldier { get; private set; }
        public int SoldierId { get; private set; }
        public int Index { get; private set; }

        AiBrain brain;
        bool possessed;
        bool selected;
        bool alive = true;

        static readonly Color NormalColor = new Color(0f, 0f, 0f, 0.85f);
        static readonly Color PossessedColor = new Color(0.15f, 0.55f, 0.85f, 0.9f);
        static readonly Color SelectedColor = new Color(0.85f, 0.65f, 0.1f, 0.9f);
        static readonly Color DeadColor = new Color(0.12f, 0.12f, 0.13f, 0.75f);
        static readonly Color DeadTextColor = new Color(0.5f, 0.5f, 0.52f);

        IDisposable damageSub, healedSub, diedSub, stateSub, weaponSub, possessionSub, selectionSub;

        void Awake()
        {
            if (background == null) background = GetComponent<Image>();
            if (label == null) label = transform.Find("Label")?.GetComponent<Text>();
            if (healthFill == null) healthFill = transform.Find("BarBG/BarFill")?.GetComponent<Image>();
        }

        // Se llama una sola vez, apenas se instancia la fila. Deja el
        // soldado atado de por vida (una fila nunca cambia de dueño).
        public void Bind(Soldier soldier, int index)
        {
            // BUG REAL: Soldier.Id se asigna en Bootstrap() (Awake), pero a
            // diferencia de Health/Motor/Weapon/Brain la propiedad Id NO se
            // auto-bootstrapea al leerla. RosterView arma las filas desde
            // OnEnable, que no tiene garantizado correr DESPUES del Awake
            // de cada soldado -- leer soldier.Id antes de tiempo daba
            // SIEMPRE 0, y como SoldierId se guarda una sola vez aca, cada
            // fila quedaba comparando contra un id que ningun evento real
            // iba a mandar nunca (todo el sync por eventos se rompia en
            // silencio). Bootstrap() es idempotente: llamarlo de mas no
            // hace nada si Awake ya corrio.
            soldier.Bootstrap();

            Soldier = soldier;
            SoldierId = soldier.Id;
            Index = index;
            brain = soldier.GetComponent<AiBrain>();
            alive = soldier.Health == null || soldier.Health.IsAlive;

            // PossessionChangedEvent solo se publica al CAMBIAR de soldado,
            // nunca en la posesion inicial del arranque (mismo bug que ya
            // documentaba SelectedSoldierUI): se lee del brain una vez aca,
            // despues el evento alcanza para todo lo demas.
            var playerBrain = FindAnyObjectByType<PlayerBrain>();
            possessed = playerBrain != null && playerBrain.Current != null && playerBrain.Current.Id == SoldierId;

            RefreshLabel();
            RefreshHealthBar(soldier.Health != null ? soldier.Health.Current : 0, soldier.Health != null ? soldier.Health.MaxHealth : 0);
            RefreshBackground();
        }

        void OnEnable()
        {
            damageSub = EventBus.Instance.Subscribe<DamageTakenEvent>(OnDamage);
            healedSub = EventBus.Instance.Subscribe<HealedEvent>(OnHealed);
            diedSub = EventBus.Instance.Subscribe<EntityDiedEvent>(OnDied);
            stateSub = EventBus.Instance.Subscribe<AiStateChangedEvent>(OnStateChanged);
            weaponSub = EventBus.Instance.Subscribe<WeaponChangedEvent>(OnWeaponChanged);
            possessionSub = EventBus.Instance.Subscribe<PossessionChangedEvent>(OnPossession);
            selectionSub = EventBus.Instance.Subscribe<SelectionChangedEvent>(OnSelection);
        }

        void OnDisable()
        {
            damageSub?.Dispose();
            healedSub?.Dispose();
            diedSub?.Dispose();
            stateSub?.Dispose();
            weaponSub?.Dispose();
            possessionSub?.Dispose();
            selectionSub?.Dispose();
        }

        // Revivir (reanimar / Health.Initialize) no publica ningun evento: sin esto la fila quedaba en
        // "CAIDO" para siempre aunque el soldado ya estuviera de pie.
        void Update()
        {
            if (alive || Soldier == null || Soldier.Health == null || !Soldier.Health.IsAlive) return;
            alive = true;
            if (healthFill != null) healthFill.gameObject.SetActive(true);
            RefreshHealthBar(Soldier.Health.Current, Soldier.Health.MaxHealth);
            RefreshLabel();
            RefreshBackground();
        }

        void OnDamage(DamageTakenEvent evt)
        {
            if (evt.TargetId != SoldierId || !alive) return;
            RefreshHealthBar(evt.RemainingHealth, Soldier.Health.MaxHealth);
            RefreshLabel();
        }

        void OnHealed(HealedEvent evt)
        {
            if (evt.TargetId != SoldierId || !alive) return;
            RefreshHealthBar(evt.RemainingHealth, Soldier.Health.MaxHealth);
            RefreshLabel();
        }

        void OnDied(EntityDiedEvent evt)
        {
            if (evt.ActorId != SoldierId) return;
            alive = false;
            RefreshLabel();
            if (healthFill != null) healthFill.gameObject.SetActive(false);
            RefreshBackground();
        }

        void OnStateChanged(AiStateChangedEvent evt)
        {
            if (evt.ActorId != SoldierId) return;
            RefreshLabel();
        }

        void OnWeaponChanged(WeaponChangedEvent evt)
        {
            if (evt.SoldierId != SoldierId) return;
            RefreshLabel();
        }

        void OnPossession(PossessionChangedEvent evt)
        {
            bool wasPossessed = possessed;
            possessed = evt.ToId == SoldierId;
            if (possessed == wasPossessed) return;
            RefreshLabel();
            RefreshBackground();
        }

        void OnSelection(SelectionChangedEvent evt)
        {
            bool wasSelected = selected;
            selected = evt.SelectedIds != null && evt.SelectedIds.Contains(SoldierId);
            if (selected != wasSelected) RefreshBackground();
        }

        // Traduce el estado interno de AiBrain a las 3 categorias pedidas
        // (siguiendo / atacando / quieto) sin perder del todo la info: un
        // soldado en camino a una orden tiene su propia etiqueta en vez de
        // mentir metiendolo en "quieto".
        static string StateLabel(AiState state) => state switch
        {
            AiState.Follow => "Siguiendo",
            AiState.Chase => "Atacando",
            AiState.MovingToAttackOrder => "Atacando",
            AiState.Attack => "Atacando",
            AiState.MovingToOrder => "En camino",
            AiState.Patrol => "Quieto",
            AiState.Idle => "Quieto",
            _ => "",
        };

        // Formato pedido explicito: numero + profesion primero y grande,
        // nombre/vida/arma/estado abajo, chico y apagado -- la barra de
        // HealthFill ya es la vida "de un vistazo".
        void RefreshLabel()
        {
            if (label == null || Soldier == null) return;

            if (!alive)
            {
                label.text = $"<size=11><b>{Index} · {Soldier.ClassNameTitulo}</b></size>\n<size=10><color=#6f7278>{Soldier.DisplayName} — CAIDO</color></size>";
                label.color = DeadTextColor;
                return;
            }

            string marker = possessed ? "► " : "   ";
            string weapon = Soldier.Weapon != null ? Soldier.Weapon.CurrentWeaponKind.ToString() : "";
            string estado = possessed ? "(vos)" : (brain != null ? StateLabel(brain.State) : "");
            string estadoSuffix = string.IsNullOrEmpty(estado) ? "" : $"   ·   {estado}";
            var hp = Soldier.Health;
            string vida = hp != null ? $"{hp.Current}/{hp.MaxHealth}" : "";

            label.text = $"{marker}<size=11><b>{Index} · {Soldier.ClassNameTitulo}</b></size>\n<size=10><color=#9aa0ac>{Soldier.DisplayName}   ·   {vida}   ·   {weapon}{estadoSuffix}</color></size>";
            label.color = Color.white;
        }

        void RefreshHealthBar(int current, int max)
        {
            if (healthFill == null) return;
            healthFill.gameObject.SetActive(alive);
            if (!alive || max <= 0) return;
            float frac = (float)current / max;
            healthFill.fillAmount = frac;
            healthFill.color = frac > 0.6f ? new Color(0.35f, 0.85f, 0.4f)
                : frac > 0.25f ? new Color(0.95f, 0.8f, 0.25f)
                : new Color(0.95f, 0.25f, 0.2f);
        }

        void RefreshBackground()
        {
            if (background == null) return;
            background.color = !alive ? DeadColor : possessed ? PossessedColor : selected ? SelectedColor : NormalColor;
        }

        public bool IsHighlighted => possessed || selected;
    }
}
