using UnityEngine;
using UnityEngine.UI;
using SP.Vehicles;

namespace SP.UI
{
    // HUD propio del artillero. Como artillero se usaba la misma mirilla de
    // infanteria, que no comunica lo unico que importa en una torreta que
    // gira lento: si el cañon ya llego a donde apuntas, cuanto falta para
    // que llegue, cuanto falta para poder volver a disparar, y que area va
    // a cubrir la explosion.
    public class TurretAimView : MonoBehaviour
    {
        Image reticle;
        Image gapMarker;
        Image cooldownFill;

        // Circulo en el mundo (no en la UI): marca en el suelo el radio de
        // explosion REAL leido del arma, no un valor cosmetico duplicado.
        LineRenderer radiusRing;

        static readonly Color OnTargetColor = new Color(0.35f, 1f, 0.45f);
        static readonly Color TurningColor = new Color(1f, 0.75f, 0.25f);
        const float AimToleranceDeg = 4f;
        // A cuantos grados de brecha la marca llega al borde del HUD: mas
        // que esto y se queda clavada en el borde (la direccion sigue
        // siendo legible aunque la magnitud sature).
        const float GapFullScaleDeg = 45f;
        const float GapMaxOffsetPx = 90f;

        public void Bind(Image reticleImage, Image gap, Image cooldown, LineRenderer ring)
        {
            reticle = reticleImage;
            gapMarker = gap;
            cooldownFill = cooldown;
            radiusRing = ring;
        }

        void OnEnable()
        {
            if (reticle == null)
            {
                var t = transform.Find("Reticle");
                if (t != null) reticle = t.GetComponent<Image>();
            }
            if (gapMarker == null)
            {
                var t = transform.Find("GapMarker");
                if (t != null) gapMarker = t.GetComponent<Image>();
            }
            if (cooldownFill == null)
            {
                var t = transform.Find("CooldownBG/CooldownFill");
                if (t != null) cooldownFill = t.GetComponent<Image>();
            }
            if (radiusRing == null)
            {
                var go = GameObject.Find("TurretRadiusRing");
                if (go != null) radiusRing = go.GetComponent<LineRenderer>();
            }
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (radiusRing != null && radiusRing.enabled != visible) radiusRing.enabled = visible;
        }

        public void UpdateFrom(TurretWeapon turret)
        {
            if (turret == null) { SetVisible(false); return; }
            SetVisible(true);

            float gap = turret.YawGapDeg;
            bool onTarget = Mathf.Abs(gap) <= AimToleranceDeg;

            if (reticle != null)
            {
                reticle.color = onTarget ? OnTargetColor : TurningColor;
                // Dos estados bien distintos, no solo un cambio de tinte:
                // el reticulo se cierra al llegar y esta abierto mientras
                // gira, para que se lea de reojo sin mirarlo fijo.
                float size = onTarget ? 14f : 26f;
                reticle.rectTransform.sizeDelta = new Vector2(size, size);
            }

            // Marca del angulo objetivo separada del cañon actual: se ve
            // la brecha cerrarse durante el giro, y las dos coinciden
            // (marca escondida) cuando el cañon llego.
            if (gapMarker != null)
            {
                bool showGap = !onTarget;
                if (gapMarker.gameObject.activeSelf != showGap) gapMarker.gameObject.SetActive(showGap);
                if (showGap)
                {
                    float offset = Mathf.Clamp(gap / GapFullScaleDeg, -1f, 1f) * GapMaxOffsetPx;
                    gapMarker.rectTransform.anchoredPosition = new Vector2(offset, 0f);
                }
            }

            if (cooldownFill != null)
            {
                float frac = turret.CooldownFraction01;
                cooldownFill.fillAmount = frac;
                cooldownFill.color = frac >= 1f ? OnTargetColor : new Color(0.55f, 0.6f, 0.7f);
            }

            UpdateRadiusRing(turret);
        }

        void UpdateRadiusRing(TurretWeapon turret)
        {
            if (radiusRing == null) return;
            var target = PredictedImpactPoint(turret);
            // Con municion perforante el radio es 0: el anillo pasa a ser
            // una marca chica de punto de caida, no una zona de daño.
            float radius = turret.ExplosionRadius > 0f ? turret.ExplosionRadius : 0.5f;
            DrawCircle(target, radius);
        }

        // Con balistica de arco, apuntar sin ninguna ayuda seria pura
        // adivinanza. Se SIMULA la trayectoria real (misma gravedad y
        // velocidad que usara el proyectil) en vez de estimarla con una
        // formula aparte que podria desincronizarse del vuelo real.
        public static Vector3 PredictedImpactPoint(TurretWeapon turret)
        {
            Vector3 origin = turret.Muzzle != null ? turret.Muzzle.position : turret.transform.position;
            Vector3 dir = turret.transform.forward;
            float g = turret.ProjectileGravity;
            // Debe coincidir con Projectile.groundImpactHeight: el
            // proyectil expira al bajar de esa altura, no al llegar a y=0.
            // Predecir contra y=0 metia un error sistematico de casi un
            // metro en un tiro de 18 m.
            const float groundY = 0.15f;

            if (g <= 0f)
            {
                if (Mathf.Abs(dir.y) > 0.001f && (groundY - origin.y) / dir.y > 0f)
                    return new Vector3(origin.x + dir.x * ((groundY - origin.y) / dir.y), groundY + 0.05f,
                                       origin.z + dir.z * ((groundY - origin.y) / dir.y));
                var flat = origin + dir * 30f;
                return new Vector3(flat.x, groundY + 0.05f, flat.z);
            }

            // Forma cerrada de la parabola, no una simulacion paso a paso:
            // el resultado no depende de ningun dt, asi que coincide con
            // el vuelo real (que integra la misma parabola de forma
            // exacta) sin importar los fps.
            //   y(t) = y0 + vy*t - 0.5*g*t^2 = 0
            Vector3 vel = dir * (TurretWeapon.ProjectileSpeed * TurretWeapon.SpeedMultiplier);
            float y0 = origin.y - groundY;
            float disc = vel.y * vel.y + 2f * g * y0;
            if (disc < 0f) disc = 0f;
            float tImpact = (vel.y + Mathf.Sqrt(disc)) / g;
            return new Vector3(origin.x + vel.x * tImpact, groundY + 0.05f, origin.z + vel.z * tImpact);
        }

        void DrawCircle(Vector3 center, float radius)
        {
            const int segments = 40;
            if (radiusRing.positionCount != segments) radiusRing.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                radiusRing.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
        }
    }
}
