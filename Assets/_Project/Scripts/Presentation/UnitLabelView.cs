using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Vehicles;

namespace SP.Presentation
{
    // C1: etiqueta al pie de cada unidad con su vida, su tipo (aliado /
    // enemigo / vehiculo / interactuable) y, si es montable, la ocupacion
    // (2/4). Solo se ve en RTS -- en FPS, a los pies de decenas de
    // soldados, seria puro ruido debajo de lo que ya muestra el HUD.
    //
    // Ya NO tiene LateUpdate propio: la recorre WorldUiDirector en el
    // mismo pase que HealthBarView y MinimapIcon.
    public class UnitLabelView : MonoBehaviour
    {
        Text label;
        Soldier soldier;
        Vehicle vehicle;
        ObstacleMarker obstaculo;
        bool bootstrapped;

        // Solo para verificacion: que texto quedo puesto, y si el Text
        // hijo esta activo ahora mismo (visible en RTS, apagado en FPS).
        public string CurrentText => label != null ? label.text : null;
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
            vehicle = soldier == null ? GetComponentInParent<Vehicle>() : null;
            obstaculo = (soldier == null && vehicle == null) ? GetComponentInParent<ObstacleMarker>() : null;
        }

        // Lo llama WorldUiDirector una vez por frame, con si la camara
        // esta en RTS ahora mismo (cam.orthographic, que CameraRig.SetMode
        // ya usa como la marca real de "estamos en RTS" -- no hace falta
        // otra fuente de verdad). Devuelve true si quedo visible.
        public bool Tick(bool enRts)
        {
            if (!bootstrapped) Bootstrap();
            if (label == null) return false;

            string texto = enRts ? BuildText() : null;
            bool visible = texto != null;
            if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
            if (visible) label.text = texto;
            return visible;
        }

        string BuildText()
        {
            if (soldier != null)
            {
                if (soldier.Health == null) return null;
                string tipo = soldier.Team == TeamId.Player ? "Aliado" : "Enemigo";
                return $"{tipo}  {soldier.Health.Current}/{soldier.Health.MaxHealth}";
            }
            if (vehicle != null)
                return $"Vehiculo  {vehicle.OccupantCount}/{vehicle.Capacity}";
            if (obstaculo != null)
                return $"Interactuable  {obstaculo.CurrentHealth}/{obstaculo.MaxHealth}";
            return null;
        }

        public void ApplyBillboard(Quaternion cameraRotation) => transform.rotation = cameraRotation;

        // La raiz de cada unidad es un cubo primitivo de Unity, centrado en
        // su propio origen local (va de -0.5 a 0.5 en cada eje ANTES de
        // escalar): -0.5 en Y siempre cae justo en la cara de abajo, sea
        // cual sea la escala real de esa unidad (soldado, enemigo o
        // vehiculo) -- Unity aplica la escala del padre al transformar
        // este offset a mundo, asi que no hace falta conocerla aca.
        const float OffsetAlPieLocal = -0.5f;

        public static UnitLabelView Construir(Transform unidad)
        {
            var anchor = new GameObject(AnchorName).transform;
            anchor.SetParent(unidad, false);
            anchor.localPosition = new Vector3(0f, OffsetAlPieLocal, 0f);

            // Contrarresta una escala no uniforme del padre (el cubo del
            // soldado es 0.9x1.6x0.9, el del vehiculo 2.2x1.4x3.6): sin
            // esto el Canvas saldria deformado, mismo motivo que
            // HealthBarAnchor en HeadlessTestRunner.BuildHealthBar.
            var parentScale = unidad.localScale;
            anchor.localScale = new Vector3(
                parentScale.x != 0f ? 1f / parentScale.x : 1f,
                parentScale.y != 0f ? 1f / parentScale.y : 1f,
                parentScale.z != 0f ? 1f / parentScale.z : 1f);

            var canvasGO = new GameObject("UnitLabelCanvas", typeof(Canvas));
            canvasGO.transform.SetParent(anchor, false);
            canvasGO.transform.localScale = Vector3.one * 0.01f;
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRt = canvasGO.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(160f, 20f);

            var textGO = new GameObject("Label", typeof(Text));
            textGO.transform.SetParent(canvasGO.transform, false);
            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 11;
            text.color = SP.UI.FondoOpaco.ColorDeTexto;
            var textRt = textGO.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            // Texto claro sobre el terreno claro del mapa es invisible --
            // el mismo bug ya medido y resuelto para el cartel de [F]
            // Poseer (ver FondoOpaco.cs). Sin esto la etiqueta se pone
            // bien pero no se ve nunca, en cualquier zoom de RTS.
            SP.UI.FondoOpaco.Poner(text);

            var view = canvasGO.AddComponent<UnitLabelView>();
            view.label = text;
            // El canvas queda activo (si no, OnEnable nunca corre y no se
            // registra en WorldUiDirector): quien se apaga/prende segun el
            // modo es el Text hijo, y eso ya lo resuelve el primer Tick().
            return view;
        }

        const string AnchorName = "UnitLabelAnchor";

        // Si construye() en un padre que ya tiene su anchor, listo -- asi
        // llamarla dos veces (una escena recargada, una segunda corrida)
        // no deja dos etiquetas superpuestas en la misma unidad.
        static void ConstruirSiFalta(Transform unidad, ref int creadas)
        {
            if (unidad.Find(AnchorName) != null) return;
            Construir(unidad);
            creadas++;
        }

        // Una etiqueta por Soldier, Vehicle y ObstacleMarker de la escena.
        // La real (SC_Gameplay) no la construye ningun test: se arma sola
        // al arrancar, igual que MinimapIcon.RegistrarObstaculos.
        public static int RegistrarTodas()
        {
            int creadas = 0;
            foreach (var s in FindObjectsByType<Soldier>(FindObjectsInactive.Include))
                ConstruirSiFalta(s.transform, ref creadas);
            foreach (var v in FindObjectsByType<Vehicle>(FindObjectsInactive.Include))
                ConstruirSiFalta(v.transform, ref creadas);
            foreach (var o in FindObjectsByType<ObstacleMarker>(FindObjectsInactive.Include))
                ConstruirSiFalta(o.transform, ref creadas);
            return creadas;
        }
    }
}
