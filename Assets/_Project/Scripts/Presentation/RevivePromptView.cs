using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;

namespace SP.Presentation
{
    // Cartel mundial "[Q] REVIVIR" sobre un aliado caido. Mismo patron que
    // UnitLabelView (Canvas WorldSpace propio, billboard aplicado por
    // WorldUiDirector, alta/baja estatica): un Canvas por soldado, creado una
    // sola vez, que se prende/apaga segun si ESE soldado esta caido ahora.
    //
    // A diferencia de UnitLabelView (que solo se ve en RTS), este cartel es
    // util sobre todo en FPS: es el mismo [Q] que ResolverGestoDeQ resuelve
    // como REANIMAR (radial, categoria 4 sub 3) cuando la mira apunta a un
    // caido. Por eso no depende del modo de camara, solo de si el soldado
    // esta muerto y es del bando del jugador (los enemigos caidos no se
    // reaniman, mostrarles el cartel confundiria).
    public class RevivePromptView : MonoBehaviour
    {
        Text label;
        Soldier soldier;
        bool bootstrapped;

        public bool IsVisible => label != null && label.gameObject.activeSelf;

        void OnEnable()
        {
            Bootstrap();
            WorldUiDirector.Register(this);
        }

        void OnDisable() => WorldUiDirector.Unregister(this);

        void Start() => Bootstrap();

        public void Bootstrap()
        {
            if (bootstrapped) return;
            if (label == null) label = GetComponentInChildren<Text>(true);
            if (label == null) return;
            bootstrapped = true;
            soldier = GetComponentInParent<Soldier>();
        }

        // Llamado una vez por frame por WorldUiDirector (mismo lugar que
        // recorre UnitLabelView). Devuelve true si quedo visible.
        public bool Tick()
        {
            if (!bootstrapped) Bootstrap();
            if (label == null || soldier == null) return false;

            bool visible = soldier.Health != null && !soldier.Health.IsAlive && soldier.Team == TeamId.Player;
            if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
            return visible;
        }

        public void ApplyBillboard(Quaternion cameraRotation) => transform.rotation = cameraRotation;

        // Un poco por encima de donde queda la cabeza tirado (el cubo del
        // soldado mide 1.6 de alto, pero caido queda mas bajo -- ver
        // OffsetAlPieLocal en UnitLabelView para el mismo razonamiento de
        // por que esto se calcula en espacio LOCAL del padre).
        const float OffsetLocal = 0.9f;
        const string AnchorName = "RevivePromptAnchor";

        public static RevivePromptView Construir(Transform unidad)
        {
            var anchor = new GameObject(AnchorName).transform;
            anchor.SetParent(unidad, false);
            anchor.localPosition = new Vector3(0f, OffsetLocal, 0f);

            var parentScale = unidad.localScale;
            anchor.localScale = new Vector3(
                parentScale.x != 0f ? 1f / parentScale.x : 1f,
                parentScale.y != 0f ? 1f / parentScale.y : 1f,
                parentScale.z != 0f ? 1f / parentScale.z : 1f);

            var canvasGO = new GameObject("RevivePromptCanvas", typeof(Canvas));
            canvasGO.transform.SetParent(anchor, false);
            canvasGO.transform.localScale = Vector3.one * 0.015f;
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRt = canvasGO.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(160f, 24f);

            var textGO = new GameObject("Label", typeof(Text));
            textGO.transform.SetParent(canvasGO.transform, false);
            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 13;
            text.fontStyle = FontStyle.Bold;
            // Dorado: mismo tono que PonerPromptContextual usa para la
            // accion contextual destacada en AimUI -- consistencia entre el
            // cartel de mundo y el cartel de mira.
            text.color = new Color(1f, 0.85f, 0.25f);
            text.text = "[Q] REVIVIR";
            var textRt = textGO.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            // Mismo fondo opaco que usan los demas carteles (PromptText de
            // AimUI, UnitLabelView): sin esto el texto claro es invisible
            // sobre terreno o cielo claros.
            SP.UI.FondoOpaco.Poner(text);

            var view = canvasGO.AddComponent<RevivePromptView>();
            view.label = text;
            text.gameObject.SetActive(false);
            return view;
        }

        static void ConstruirSiFalta(Transform unidad, ref int creadas)
        {
            if (unidad.Find(AnchorName) != null) return;
            Construir(unidad);
            creadas++;
        }

        // Un cartel por Soldier del bando del jugador (los enemigos caidos
        // no se reaniman, no hace falta el componente en ellos).
        public static int RegistrarTodas()
        {
            int creadas = 0;
            foreach (var s in FindObjectsByType<Soldier>(FindObjectsInactive.Include))
            {
                if (s == null || s.Team != TeamId.Player) continue;
                ConstruirSiFalta(s.transform, ref creadas);
            }
            return creadas;
        }
    }
}
