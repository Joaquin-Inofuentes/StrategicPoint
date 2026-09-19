using UnityEngine;
using UnityEngine.UI;
using SP.Combat;

namespace SP.UI
{
    // HUD fijo (no depende de apuntar a nada) con qué arma tenés, cuánta
    // munición te queda y una barra de recarga/enfriamiento.
    public class WeaponStatusView : MonoBehaviour
    {
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
            var familia = kind switch { WeaponKind.Smg => WeaponKind.Rifle, WeaponKind.Shotgun => WeaponKind.Rifle, WeaponKind.Sniper => WeaponKind.Rifle, WeaponKind.Rocket => WeaponKind.Heavy, _ => kind };
            var tex = Resources.Load<Texture2D>(IconFolder + familia);
            if (tex == null) return null;
            iconCache[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            iconCache[i].name = "WeaponIcon_" + kind;
            return iconCache[i];
        }

        // Arma el hijo "Icono" a la izquierda del panel y le hace lugar al
        // texto. Idempotente: si ya existe solo lo devuelve.
        public Image EnsureIcon()
        {
            if (icon != null) return icon;
            var existing = transform.Find(IconName);
            if (existing != null) { icon = existing.GetComponent<Image>(); if (icon != null) return icon; }

            var go = new GameObject(IconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(52f, 36f);
            rt.anchoredPosition = new Vector2(6f, 4f);
            icon = go.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.sprite = IconFor(WeaponKind.Rifle);

            var panel = (RectTransform)transform;
            panel.sizeDelta = new Vector2(Mathf.Max(panel.sizeDelta.x, 270f), panel.sizeDelta.y);
            var t = label != null ? label : GetComponentInChildren<Text>(true);
            if (t != null)
            {
                var trt = t.rectTransform;
                trt.offsetMin = new Vector2(trt.offsetMin.x + 60f, trt.offsetMin.y);
            }
            return icon;
        }

        public void Bind(Text text, Image fillImage)
        {
            label = text;
            fill = fillImage;
        }

        void OnEnable()
        {
            if (label == null) label = GetComponentInChildren<Text>(true);
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

            if (icon == null) icon = transform.Find(IconName) != null ? transform.Find(IconName).GetComponent<Image>() : null;
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
                string status = weapon.IsReloading ? "  ·  RECARGANDO" : "";
                int slot = weapon.Loadout.IndexOf(weapon.CurrentWeaponKind) + 1;
                string tecla = slot > 0 ? $"[{slot}] " : "";
                label.text = $"{tecla}{WeaponCatalog.Get(weapon.CurrentWeaponKind).DisplayName}   {weapon.CurrentAmmo}/{weapon.MagazineSize}{status}";

                // El contador quedaba blanco fijo hasta llegar a cero, sin
                // ningun aviso previo de que se estaba por acabar. Rojo
                // por debajo del 30% de la carga, para que se note antes
                // de quedarse en seco en medio de un tiroteo.
                float frac = weapon.MagazineSize > 0 ? (float)weapon.CurrentAmmo / weapon.MagazineSize : 1f;
                label.color = (!weapon.IsReloading && frac < 0.3f) ? new Color(0.95f, 0.25f, 0.2f) : Color.white;
            }
            if (fill != null)
            {
                fill.fillAmount = weapon.ReadinessFraction01;
                fill.color = weapon.IsReloading ? new Color(0.95f, 0.6f, 0.2f) : new Color(0.4f, 0.85f, 0.45f);
            }
        }
    }
}
