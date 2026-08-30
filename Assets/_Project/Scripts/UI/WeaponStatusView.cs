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

            if (label != null)
            {
                string status = weapon.IsReloading ? "  ·  RECARGANDO" : "";
                label.text = $"{weapon.CurrentWeaponKind}   {weapon.CurrentAmmo}/{weapon.MagazineSize}{status}";
            }
            if (fill != null)
            {
                fill.fillAmount = weapon.ReadinessFraction01;
                fill.color = weapon.IsReloading ? new Color(0.95f, 0.6f, 0.2f) : new Color(0.4f, 0.85f, 0.45f);
            }
        }
    }
}
