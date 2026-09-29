using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;
using SP.Player;
using SP.Presentation;
using SP.CameraSystem;

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
    public class RosterRowView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] Image background;
        [SerializeField] Text label;
        [SerializeField] Image healthFill;
        [SerializeField] Image icon;

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
        static readonly Color SelectedColor = new Color(0.95f, 0.72f, 0.1f, 0.95f);
        // Pedido explicito: en vez del cartel "N seleccionados", los seleccionados se RESALTAN aca abajo a la
        // izquierda: marco amarillo brillante, tarjeta un poco mas grande y un realce del icono.
        static readonly Color MarcoSeleccion = new Color(1f, 0.92f, 0.25f, 1f);
        static readonly Color MarcoPoseido = new Color(1f, 1f, 1f, 0.55f);
        // El marco de seleccion son 4 tiras finas DENTRO de la tarjeta: no se agranda la tarjeta (antes crecia 12% y las vecinas,
        // que se dibujan despues, la tapaban) ni se usa Outline (tine todo un Image translucido).
        RectTransform marcoRaiz;
        Image[] marcoLados;
        // Icono de estado chico arriba de la tarjeta: calmado / atacando / interactuando.
        Image estadoDisco, estadoIcono;
        EstadoDeFila estado = EstadoDeFila.Calmo;
        float proximoChequeoEstado, atacaHasta;
        static readonly Color ColorCalmo = new Color(0.5f, 0.9f, 0.6f), ColorAtacando = new Color(1f, 0.32f, 0.26f), ColorInteractuando = new Color(1f, 0.82f, 0.25f);
        public EstadoDeFila Estado => estado;
        static readonly Color DeadColor = new Color(0.12f, 0.12f, 0.13f, 0.75f);
        static readonly Color DeadTextColor = new Color(0.5f, 0.5f, 0.52f);

        IDisposable damageSub, healedSub, diedSub, stateSub, weaponSub, possessionSub, selectionSub, shotSub;

        void Awake()
        {
            if (label != null) label.horizontalOverflow = HorizontalWrapMode.Overflow;   // Ronda 11: 2 renglones fijos, sin wrap que empuje texto sobre la barra

            // BUG REAL: GameplaySceneBootstrap.Start() repara (SpriteBlanco)
            // las Image Filled sin sprite UNA SOLA VEZ al arrancar la escena
            // (ver SpriteBlanco.cs). Esa barrida cubre a las filas que ya
            // existen en ese momento, pero RosterView.Rebuild() (llamado por
            // ejemplo al reordenar la escuadra con [ y ]) destruye esas filas
            // y las vuelve a instanciar desde CERO a partir del prefab, que
            // nunca fue reparado. Sin sprite, Image.Filled ignora fillAmount
            // por completo: la barra queda mostrando el maximo para siempre
            // sin importar la vida real, exactamente en el momento en que el
            // roster se reconstruye. Reparar aca, en cada fila nueva, la
            // hace independiente del orden con el barrido global.
            SpriteBlanco.Reparar(healthFill);

            var rt = transform as RectTransform;
            if (rt != null)
            {
                ArmarMarco(rt);
                ArmarIconoDeEstado(rt);
            }
        }

        void ArmarMarco(RectTransform fila)
        {
            var go = new GameObject("MarcoSeleccion", typeof(RectTransform));
            marcoRaiz = (RectTransform)go.transform;
            marcoRaiz.SetParent(fila, false);
            marcoRaiz.anchorMin = Vector2.zero; marcoRaiz.anchorMax = Vector2.one;
            marcoRaiz.offsetMin = Vector2.zero; marcoRaiz.offsetMax = Vector2.zero;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            marcoLados = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var lado = new GameObject("Lado" + i, typeof(RectTransform), typeof(Image));
                var r = (RectTransform)lado.transform;
                r.SetParent(marcoRaiz, false);
                marcoLados[i] = lado.GetComponent<Image>();
                marcoLados[i].raycastTarget = false;
            }
            marcoRaiz.gameObject.SetActive(false);
        }

        void PonerMarco(Color color, float grosor)
        {
            if (marcoLados == null) return;
            // 0 arriba, 1 abajo, 2 izquierda, 3 derecha.
            for (int i = 0; i < 4; i++)
            {
                var r = (RectTransform)marcoLados[i].transform;
                marcoLados[i].color = color;
                switch (i)
                {
                    case 0: r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f); r.sizeDelta = new Vector2(0f, grosor); break;
                    case 1: r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(1f, 0f); r.pivot = new Vector2(0.5f, 0f); r.sizeDelta = new Vector2(0f, grosor); break;
                    case 2: r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 0.5f); r.sizeDelta = new Vector2(grosor, 0f); break;
                    default: r.anchorMin = new Vector2(1f, 0f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 0.5f); r.sizeDelta = new Vector2(grosor, 0f); break;
                }
                r.anchoredPosition = Vector2.zero;
            }
        }

        void ArmarIconoDeEstado(RectTransform fila)
        {
            var go = new GameObject("Estado", typeof(RectTransform), typeof(Image));
            var r = (RectTransform)go.transform;
            r.SetParent(fila, false);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 0f);
            r.sizeDelta = new Vector2(22f, 22f);
            r.anchoredPosition = new Vector2(0f, 5f);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            estadoDisco = go.GetComponent<Image>();
            estadoDisco.sprite = EstadoIconFactory.Disco();
            estadoDisco.color = new Color(0f, 0f, 0f, 0.78f);
            estadoDisco.raycastTarget = false;

            var gi = new GameObject("Glifo", typeof(RectTransform), typeof(Image));
            var gr = (RectTransform)gi.transform;
            gr.SetParent(r, false);
            gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one;
            gr.offsetMin = new Vector2(3f, 3f); gr.offsetMax = new Vector2(-3f, -3f);
            estadoIcono = gi.GetComponent<Image>();
            estadoIcono.raycastTarget = false;
            AplicarEstado(EstadoDeFila.Calmo, true);
        }

        void AplicarEstado(EstadoDeFila nuevo, bool forzar = false)
        {
            if (estadoIcono == null) return;
            if (!forzar && nuevo == estado && estadoIcono.sprite != null) return;
            estado = nuevo;
            estadoIcono.sprite = EstadoIconFactory.Para(nuevo);
            estadoIcono.color = nuevo == EstadoDeFila.Atacando ? ColorAtacando : nuevo == EstadoDeFila.Interactuando ? ColorInteractuando : ColorCalmo;
            if (estadoDisco != null) estadoDisco.gameObject.SetActive(alive);
        }

        // Interactuando: usa algo (revive, cura, planta, torreta, vehiculo). Atacando: en combate segun la IA o disparo hace
        // poco (el soldado que manejas no tiene IA activa). Calmo: el resto.
        EstadoDeFila CalcularEstado()
        {
            var s = Soldier;
            if (s == null || !alive) return EstadoDeFila.Calmo;
            if (AccionesEnCurso.De(s, out _) || SP.Vehicles.TorretaFija.De(s) != null || (brain != null && brain.MontadoEnVehiculo))
                return EstadoDeFila.Interactuando;
            if (Time.time < atacaHasta) return EstadoDeFila.Atacando;
            if (brain != null && (brain.State == AiState.Attack || brain.State == AiState.Chase || brain.State == AiState.MovingToAttackOrder))
                return EstadoDeFila.Atacando;
            return EstadoDeFila.Calmo;
        }

        void OnShot(ShotFiredEvent evt)
        {
            if (evt.ShooterId == SoldierId) atacaHasta = Time.time + 1.6f;
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
            var playerBrain = PlayerBrain.Activo;
            possessed = playerBrain != null && playerBrain.Current != null && playerBrain.Current.Id == SoldierId;

            // El rol no cambia en la vida de un soldado: alcanza con
            // asignarlo una vez aca en vez de en cada Refresh*.
            if (icon != null) icon.sprite = RoleIconFactory.For(soldier.Role);

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
            shotSub = EventBus.Instance.Subscribe<ShotFiredEvent>(OnShot);
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
            shotSub?.Dispose();
        }

        // Revivir (reanimar / Health.Initialize) no publica ningun evento: sin esto la fila quedaba en
        // "CAIDO" para siempre aunque el soldado ya estuviera de pie.
        void Update()
        {
            if (estadoIcono != null && Time.unscaledTime >= proximoChequeoEstado)
            {
                proximoChequeoEstado = Time.unscaledTime + 0.15f;
                AplicarEstado(CalcularEstado());
            }
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

        // Pedido explicito: "mas simplificado con iconos simples". El icono
        // (asignado en Bind) ya dice el rol de un vistazo y el resaltado de
        // background ya dice quien esta poseido/seleccionado -- ahi el
        // bloque de texto de 2 renglones (profesion + vida + arma) sobraba.
        // Queda solo el numero de vida: la barra de HealthFill es la lectura
        // rapida, el numero es el dato exacto para quien lo necesite.
        void RefreshLabel()
        {
            if (label == null || Soldier == null) return;

            if (!alive)
            {
                label.text = "<size=10><color=#6f7278>CAIDO</color></size>";
                label.color = DeadTextColor;
                return;
            }

            var hp = Soldier.Health;
            label.text = hp != null ? $"<size=12>{hp.Current}</size>" : "";
            label.color = Color.white;
        }

        void RefreshHealthBar(int current, int max)
        {
            if (healthFill == null) return;
            healthFill.gameObject.SetActive(alive);
            if (!alive || max <= 0) return;
            float frac = (float)current / max;
            healthFill.fillAmount = frac;
            healthFill.color = AjustesDeJuego.Adaptar(frac > 0.6f ? new Color(0.35f, 0.85f, 0.4f)
                : frac > 0.25f ? new Color(0.95f, 0.8f, 0.25f)
                : new Color(0.95f, 0.25f, 0.2f));
        }

        void RefreshBackground()
        {
            if (background == null) return;
            background.color = !alive ? DeadColor : possessed ? PossessedColor : selected ? SelectedColor : NormalColor;

            bool resaltar = alive && selected;
            if (marcoRaiz != null)
            {
                bool ver = alive && (selected || possessed);
                marcoRaiz.gameObject.SetActive(ver);
                if (ver) PonerMarco(resaltar ? MarcoSeleccion : MarcoPoseido, resaltar ? 5f : 2f);
            }
            if (estadoDisco != null) estadoDisco.gameObject.SetActive(alive);
        }

        public bool IsHighlighted => possessed || selected;

        float lastClickTime;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Soldier == null || !alive || eventData.button != PointerEventData.InputButton.Left) return;

            float clickTime = Time.unscaledTime;
            bool isDouble = (clickTime - lastClickTime) <= 0.3f;
            lastClickTime = clickTime;

            if (isDouble)
            {
                var brain = PlayerBrain.Activo;
                if (brain != null && brain.Current != Soldier)
                {
                    PossessionService.Swap(brain, Soldier);
                }
                if (CameraRig.Instance != null && CameraRig.Instance.Mode != ControlMode.Fps)
                {
                    CameraRig.Instance.SetMode(ControlMode.Fps);
                }
            }
            else
            {
                var sc = SelectionController.Instance;
                if (sc != null) sc.SelectSingle(Soldier);

                if (CameraRig.Instance != null && CameraRig.Instance.Mode == ControlMode.Rts)
                {
                    CameraRig.Instance.RecenterOn(Soldier.transform.position);
                }
            }
        }
    }
}
