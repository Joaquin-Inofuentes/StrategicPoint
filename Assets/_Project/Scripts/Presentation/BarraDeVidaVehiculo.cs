using SP.Core;
using UnityEngine;
using UnityEngine.UI;
using SP.Combat;
using SP.Vehicles;

namespace SP.Presentation
{
    // Ronda 13 (punto 9): los tanques tambien tienen barra de vida.
    //
    // HealthBarView es solo de soldados (se ata con GetComponentInParent<Soldier>() y se enciende con el evento de dano
    // de un ActorId): el Health del vehiculo se inicializa con ActorId -1 (Vehicle.Health), asi que el bus de dano no
    // permite identificarlo y la barra LEE Health.Current cada frame. Es un Canvas en el mundo sobre el casco, que mira a
    // la camara activa. Verde si es del jugador, roja si es enemigo. Se oculta al destruirse, en el tanque donde va el
    // jugador (ya tiene VehicleStatusView) y cuando la camara esta lejos.
    //
    // Se agrega sola desde Vehicle.Start (solo en Play); no hace falta cablearla en las escenas.
    [DisallowMultipleComponent]
    public class BarraDeVidaVehiculo : MonoBehaviour
    {
        public const float AnchoMundo = 3.2f, AltoMundo = 0.34f;
        public const float DistanciaMaxima = 120f, DistanciaMinima = 6f;

        Vehicle vehicle;
        Canvas canvas;
        Image fill, fondo;
        Transform raizCanvas;
        float alturaSobreElCasco = 2.6f;
        Camera cam;

        public float Relleno01 => fill != null ? fill.fillAmount : 0f;
        public bool Visible => canvas != null && canvas.enabled;
        public Color ColorDelRelleno => fill != null ? fill.color : Color.clear;

        public static BarraDeVidaVehiculo Asegurar(Vehicle v)
        {
            if (v == null || !Application.isPlaying) return null;
            var b = v.GetComponent<BarraDeVidaVehiculo>();
            if (b == null) b = v.gameObject.AddComponent<BarraDeVidaVehiculo>();
            return b;
        }

        void Awake()
        {
            vehicle = GetComponent<Vehicle>();
            Construir();
        }

        // Altura: sobre el punto mas alto de los renderers del chasis (el tanque no es alto siempre igual).
        // Bug #056: las camionetas se achican a la mitad DESPUES de instanciarse (OperacionDirector.InstanciarCamioneta) y la
        // barra quedaba a la altura de la camioneta grande, flotando ~2 m arriba. Se vuelve a medir en Start, ya escaladas.
        void MedirAltura()
        {
            float techo = 0f;
            bool hay = false;
            var b = new Bounds(transform.position, Vector3.zero);
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            }
            if (hay) techo = b.max.y - transform.position.y;
            float minimo = 1.6f * Mathf.Clamp(transform.lossyScale.y, 0.3f, 1f);
            alturaSobreElCasco = Mathf.Max(minimo, techo) + 0.9f * Mathf.Clamp(transform.lossyScale.y, 0.5f, 1f);
        }

        void Start() => MedirAltura();

        void Construir()
        {
            if (raizCanvas != null) return;

            MedirAltura();

            var go = new GameObject("BarraDeVidaVehiculo", typeof(Canvas));
            raizCanvas = go.transform;
            // No es hija del tanque: su escala/rotacion se compondrian con las del vehiculo (que gira y se inclina).
            raizCanvas.SetParent(null, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(320f, 34f);
            // Proporcional al vehiculo (bug #047: las camionetas a mitad de escala llevaban una barra de 3,2 m, mas ancha que ellas).
            go.transform.localScale = Vector3.one * (AnchoMundo * Mathf.Clamp(transform.lossyScale.x, 0.45f, 1.5f) / 320f);

            fondo = NuevaImagen("Fondo", go.transform, new Color(0f, 0f, 0f, 0.65f));
            Estirar(fondo.rectTransform, 0f);
            fill = NuevaImagen("Relleno", go.transform, new Color(0.35f, 0.9f, 0.4f));
            fill.sprite = SP.UI.SpriteBlanco.Obtener();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            Estirar(fill.rectTransform, 3f);
        }

        static Image NuevaImagen(string nombre, Transform padre, Color c)
        {
            var g = new GameObject(nombre, typeof(Image));
            g.transform.SetParent(padre, false);
            var img = g.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        static void Estirar(RectTransform r, float margen)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(margen, margen); r.offsetMax = new Vector2(-margen, -margen);
        }

        void LateUpdate() => Refrescar();

        // Publico para que la suite lo ejerza sin esperar un frame.
        public void Refrescar()
        {
            if (vehicle == null) vehicle = GetComponent<Vehicle>();
            if (raizCanvas == null) Construir();
            if (vehicle == null || canvas == null) return;

            var hp = vehicle.Health;
            float pct = hp.MaxHealth > 0 ? Mathf.Clamp01((float)hp.Current / hp.MaxHealth) : 0f;
            fill.fillAmount = pct;
            fill.color = vehicle.Bando == TeamId.Player
                ? Color.Lerp(new Color(0.95f, 0.35f, 0.25f), new Color(0.35f, 0.9f, 0.4f), Mathf.Clamp01(pct * 1.6f))
                : new Color(0.95f, 0.28f, 0.22f);

            raizCanvas.position = transform.position + Vector3.up * alturaSobreElCasco;

            if (cam == null || !cam.isActiveAndEnabled) cam = CamaraPrincipal.Actual;
            bool mostrar = !vehicle.IsDestroyed && hp.IsAlive && !vehicle.PlayerAboard;
            if (mostrar && cam != null)
            {
                float d2 = (cam.transform.position - raizCanvas.position).sqrMagnitude;
                // Pegada a la camara (una camioneta embistiendo al tanque) la barra tapaba media pantalla.
                if (d2 > DistanciaMaxima * DistanciaMaxima || d2 < DistanciaMinima * DistanciaMinima) mostrar = false;
                else raizCanvas.rotation = cam.transform.rotation;
            }
            if (canvas.enabled != mostrar) canvas.enabled = mostrar;
        }

        void OnDestroy()
        {
            if (raizCanvas == null) return;
            if (Application.isPlaying) Destroy(raizCanvas.gameObject); else DestroyImmediate(raizCanvas.gameObject);
        }

        void OnDisable()
        {
            if (canvas != null) canvas.enabled = false;
        }

        void OnEnable() { if (raizCanvas != null) Refrescar(); }
    }
}
