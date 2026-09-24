using UnityEngine;
using UnityEngine.UI;
using SP.Combat;
using SP.Presentation;

namespace SP.UI
{
    // HUD fijo (no depende de apuntar a nada) con qué arma tenés, cuánta
    // munición te queda y una barra de recarga/enfriamiento.
    public class WeaponStatusView : MonoBehaviour
    {
        // Unico de la escena: se registra al activarse en vez de que cada consumidor lo busque con un barrido.
        public static WeaponStatusView Activo { get; private set; }
        public static void ReiniciarActivo() => Activo = null;
        public void RegistrarActivo()
        {
            Activo = this;
        }
        Text label;
        Image fill;

        // Icono del arma en mano (cambia con [1] [2] [3] y la rueda). Los
        // PNG viven en Resources/UI/WeaponIcons (game-icons.net, CC BY 3.0,
        // ver CREDITOS.txt). Se cargan como Texture2D y se convierten a
        // Sprite a mano: asi no depende de que el importador los haya
        // marcado como Sprite.
        [SerializeField] Image icon;
        public const string IconName = "Icono";
        const string IconFolder = "UI/WeaponIcons/Icono_";
        static readonly Sprite[] iconCache = new Sprite[8];
        WeaponKind? lastKind;
        float punch;

        public Image Icon => icon;

        public static Sprite IconFor(WeaponKind kind)
        {
            int i = (int)kind;
            if (i < 0 || i >= iconCache.Length) return null;
            if (iconCache[i] != null) return iconCache[i];
            // Las armas nuevas comparten el dibujo de la familia mas parecida.
            // Ronda 11 (punto 11): cada arma tiene su propio dibujo (antes Sniper/Smg/Shotgun mostraban el fusil de asalto). Si falta el
            // PNG propio se cae al de la familia mas parecida.
            var tex = SP.Core.RecursosCache.Cargar<Texture2D>(IconFolder + kind);
            if (tex == null)
            {
                var familia = kind switch { WeaponKind.Smg => WeaponKind.Rifle, WeaponKind.Shotgun => WeaponKind.Rifle, WeaponKind.Sniper => WeaponKind.Rifle, WeaponKind.Rocket => WeaponKind.Heavy, _ => kind };
                tex = SP.Core.RecursosCache.Cargar<Texture2D>(IconFolder + familia);
            }
            if (tex == null) return null;
            iconCache[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            iconCache[i].name = "WeaponIcon_" + kind;
            return iconCache[i];
        }

        // Arma el hijo "Icono" a la izquierda del panel. Idempotente: si ya
        // existe solo lo devuelve. Pedido explicito (ronda 2): "2 renglones:
        // la barra de municion [abajo] y arriba municion texto y F icono
        // de cuchillo y G icono de granada". El panel quedo en 226x54 (ver
        // constantes de abajo): el icono del arma ocupa la columna
        // izquierda abarcando las dos filas, y a la derecha el renglon de
        // arriba (texto + F/cuchillo + G/granada) y el de abajo (barra).
        // WeaponStatusUiPipeline.cs deja el panel/Text/BarBG de la escena
        // con ese mismo tamaño para que esto encaje.
        public const float AnchoPanel = 226f, AltoPanel = 54f;

        public Image EnsureIcon()
        {
            if (icon != null) return icon;
            var existing = transform.Find(IconName);
            if (existing != null) { icon = existing.GetComponent<Image>(); if (icon != null) return icon; }

            var go = new GameObject(IconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(36f, 40f);
            rt.anchoredPosition = new Vector2(4f, 8f);
            icon = go.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.sprite = IconFor(WeaponKind.Rifle);
            return icon;
        }

        public void Bind(Text text, Image fillImage)
        {
            label = text;
            fill = fillImage;
        }

        void OnDisable() { if (Activo == this) Activo = null; }
        void OnEnable()
        {
            RegistrarActivo();
            if (label == null) label = GetComponentInChildren<Text>(true);
            if (icon == null)   // se resuelve al habilitar (antes: 2 Transform.Find por frame en UpdateFrom mientras faltaba)
            {
                var ti = transform.Find(IconName);
                if (ti != null) icon = ti.GetComponent<Image>();
            }
            if (fill == null)
            {
                var barFill = transform.Find("BarBG/BarFill");
                if (barFill != null) fill = barFill.GetComponent<Image>();
            }
        }

        public void UpdateFrom(WeaponHolder weapon)
        {
            if (weapon == null)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);

            if (icon != null)
            {
                var kind = weapon.CurrentWeaponKind;
                if (lastKind != kind)
                {
                    // Primer frame: se asigna sin "golpe"; los cambios
                    // siguientes agrandan el icono un instante para que se
                    // note el cambio de arma.
                    if (lastKind.HasValue) punch = 1f;
                    lastKind = kind;
                    icon.sprite = IconFor(kind);
                }
                punch = Mathf.MoveTowards(punch, 0f, 5f * Time.unscaledDeltaTime);
                icon.rectTransform.localScale = Vector3.one * (1f + 0.3f * punch);
                icon.color = weapon.IsReloading ? new Color(1f, 0.75f, 0.35f) : Color.white;
            }

            if (label != null)
            {
                // Pedido explicito: "intenta evitar textos" -- se cae el
                // nombre del arma (ya lo dice el icono), la tecla [N] y la
                // palabra "RECARGANDO"/"SIN MUNICION" (la barra de abajo y
                // el reloj de arena ya avisan de eso sin palabras). Quedan
                // solo los numeros: cargador/reserva.
                string reserva = weapon.UsaReservas ? $" · {weapon.ReservaActual}" : "";
                label.text = $"{weapon.CurrentAmmo}/{weapon.MagazineSize}{reserva}";

                // El contador quedaba blanco fijo hasta llegar a cero, sin
                // ningun aviso previo de que se estaba por acabar. Rojo
                // por debajo del 30% de la carga, para que se note antes
                // de quedarse en seco en medio de un tiroteo.
                float frac = weapon.MagazineSize > 0 ? (float)weapon.CurrentAmmo / weapon.MagazineSize : 1f;
                label.color = (!weapon.IsReloading && frac < 0.3f) || weapon.SinMunicionTotal ? new Color(0.95f, 0.25f, 0.2f) : Color.white;
            }
            ActualizarReloj(weapon);
            ActualizarExtras(weapon);
            if (fill != null)
            {
                fill.fillAmount = weapon.ReadinessFraction01;
                fill.color = AjustesDeJuego.Adaptar(weapon.IsReloading ? new Color(0.95f, 0.6f, 0.2f) : new Color(0.4f, 0.85f, 0.45f));
            }
        }

        // Pedido explicito: "intenta evitar textos" -- la barra ya no lleva
        // un rotulo de palabras ("LISTA"/"RECARGA 1.2s"/"ENFRIANDO"): un
        // pequeño reloj de arena aparece como insignia sobre el icono del
        // arma mientras la barra no esta llena (recargando o enfriando), y
        // desaparece cuando esta lista. El color de la barra (verde/naranja,
        // ya existia) sigue siendo la señal principal.
        Image reloj;
        void ActualizarReloj(WeaponHolder weapon)
        {
            if (reloj == null)
            {
                var t = transform.Find("Reloj");
                if (t == null)
                {
                    var go = new GameObject("Reloj", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.transform.SetParent(transform, false);
                    var rt = (RectTransform)go.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                    rt.pivot = new Vector2(0f, 0f);
                    rt.sizeDelta = new Vector2(16f, 16f);
                    rt.anchoredPosition = new Vector2(24f, 6f);
                    var im = go.GetComponent<Image>();
                    im.raycastTarget = false;
                    im.sprite = HudIconFactory.Reloj();
                    im.color = new Color(1f, 0.75f, 0.35f);
                    t = go.transform;
                }
                reloj = t.GetComponent<Image>();
            }
            if (reloj == null) return;
            reloj.gameObject.SetActive(weapon.ReadinessFraction01 < 0.999f);
        }

        // Pedido explicito: "F icono de cuchillo y G icono de granada" -- en
        // el renglon de arriba, junto al numero de municion. La letra de la
        // tecla es parte del pedido (no es texto descriptivo, es el atajo).
        static Text CrearEtiqueta(Transform padre, string nombre, string letra, Vector2 pos)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(padre, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(12f, 24f);
            rt.anchoredPosition = pos;
            var tx = go.GetComponent<Text>();
            tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tx.fontSize = 12; tx.fontStyle = FontStyle.Bold;
            tx.alignment = TextAnchor.MiddleCenter;
            tx.color = new Color(0.75f, 0.78f, 0.82f);
            tx.raycastTarget = false;
            tx.text = letra;
            return tx;
        }

        static Image CrearIconoExtra(Transform padre, string nombre, Sprite sprite, Vector2 pos)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(padre, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(20f, 20f);
            rt.anchoredPosition = pos;
            var im = go.GetComponent<Image>();
            im.raycastTarget = false;
            im.preserveAspect = true;
            im.sprite = sprite;
            return im;
        }

        Image extrasCuchillo, extrasGranada;
        Text extrasGranadaCount;
        void ActualizarExtras(WeaponHolder weapon)
        {
            if (extrasCuchillo == null)
            {
                var t = transform.Find("ExtrasCuchillo");
                extrasCuchillo = t != null ? t.GetComponent<Image>()
                    : CrearIconoExtra(transform, "ExtrasCuchillo", HudIconFactory.Cuchillo(), new Vector2(142f, 28f));
                if (transform.Find("ExtrasCuchilloLabel") == null) CrearEtiqueta(transform, "ExtrasCuchilloLabel", "F", new Vector2(128f, 26f));
            }
            if (extrasGranada == null)
            {
                var t = transform.Find("ExtrasGranada");
                extrasGranada = t != null ? t.GetComponent<Image>()
                    : CrearIconoExtra(transform, "ExtrasGranada", HudIconFactory.Granada(), new Vector2(180f, 28f));
                if (transform.Find("ExtrasGranadaLabel") == null) CrearEtiqueta(transform, "ExtrasGranadaLabel", "G", new Vector2(166f, 26f));
            }
            if (extrasGranadaCount == null)
            {
                var t = transform.Find("ExtrasGranadaCount");
                GameObject go;
                if (t == null)
                {
                    go = new GameObject("ExtrasGranadaCount", typeof(RectTransform), typeof(Text));
                    go.transform.SetParent(transform, false);
                    var rt = (RectTransform)go.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                    rt.pivot = new Vector2(0f, 0f);
                    rt.sizeDelta = new Vector2(20f, 24f);
                    rt.anchoredPosition = new Vector2(202f, 26f);
                    var tx = go.GetComponent<Text>();
                    tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    tx.fontSize = 15; tx.fontStyle = FontStyle.Bold;
                    tx.alignment = TextAnchor.MiddleLeft;
                    tx.raycastTarget = false;
                    t = go.transform;
                }
                extrasGranadaCount = t.GetComponent<Text>();
            }

            bool cuchilloListo = weapon.KnifeCooldownRemaining <= 0f;
            extrasCuchillo.color = cuchilloListo ? new Color(0.9f, 0.94f, 0.98f) : new Color(0.45f, 0.48f, 0.52f);

            bool tieneGranadas = weapon.Granadas > 0;
            var colorGranada = tieneGranadas ? new Color(1f, 0.79f, 0.29f) : new Color(0.45f, 0.48f, 0.52f);
            extrasGranada.color = colorGranada;
            extrasGranadaCount.color = colorGranada;
            extrasGranadaCount.text = weapon.Granadas.ToString();
        }
    }
}
