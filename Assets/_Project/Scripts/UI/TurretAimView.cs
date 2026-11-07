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
        [SerializeField] Image reticle;
        [SerializeField] Image gapMarker;
        [SerializeField] Image cooldownFill;

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

        // El anillo NO se serializa (Bind lo recibe al construir la UI en el editor): en una escena guardada llegaba nulo y el anillo
        // nunca se dibujaba (bug #091). Si falta se busca el que dejo el constructor o se crea uno nuevo, plano y sin luz (Sprites/Default
        // respeta el color por vertice).
        void AsegurarAnillo()
        {
            if (radiusRing != null) return;
            var go = GameObject.Find("TurretRadiusRing");
            if (go == null) go = new GameObject("TurretRadiusRing");
            radiusRing = go.GetComponent<LineRenderer>();
            if (radiusRing == null) radiusRing = go.AddComponent<LineRenderer>();
            radiusRing.loop = true;
            radiusRing.useWorldSpace = true;
            radiusRing.widthMultiplier = 0.12f;
            radiusRing.positionCount = 0;
            radiusRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            radiusRing.receiveShadows = false;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) radiusRing.sharedMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            radiusRing.startColor = radiusRing.endColor = ColorNeutro;
            radiusRing.enabled = gameObject.activeSelf;
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (visible) AsegurarAnillo();
            if (radiusRing != null && radiusRing.enabled != visible) radiusRing.enabled = visible;
        }

        public void UpdateFrom(TurretWeapon turret)
        {
            if (turret == null) { SetVisible(false); return; }
            SetVisible(true);

            float gap = turret.YawGapDeg;
            bool onTarget = Mathf.Abs(gap) <= AimToleranceDeg;

            // El cuadradito de 14 px y la barra de recarga quedaron reemplazados por MiraDeTorreta (cruceta + anillos).
            if (reticle != null && reticle.enabled) reticle.enabled = false;
            if (cooldownFill != null)
            {
                if (cooldownFill.enabled) cooldownFill.enabled = false;
                var fondo = cooldownFill.transform.parent;
                if (fondo != null && fondo != transform)
                {
                    var im = fondo.GetComponent<Image>();
                    if (im != null && im != reticle && im.enabled) im.enabled = false;
                }
            }
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

        // Bug #091: el anillo del punto de impacto VIBRA (radio +-6 % a 7 Hz, ancho de 0,12 a 0,2) y cambia de color segun lo que
        // hay bajo el punto: rojo = vehiculo o soldado enemigo, naranja = obstaculo destructible, blanco = neutro.
        public const float VibracionHz = 7f, VibracionRadio = 0.06f, AnchoMin = 0.12f, AnchoMax = 0.2f, AnchoFijo = 0.18f;
        public static bool Vibra = false;            // bug #247
        public static bool SoloAnilloDeZona = true;  // bug #245: sin cruceta, rombo ni anillos de UI: solo el circulo en el piso
        public static readonly Color ColorEnemigo = new Color(1f, 0.2f, 0.15f);
        public static readonly Color ColorDestructible = new Color(1f, 0.55f, 0.1f);
        public static readonly Color ColorNeutro = Color.white;
        public enum TipoDeImpacto { Neutro, Destructible, Enemigo }

        // Ultimo anillo dibujado (lo leen los checks).
        public static TipoDeImpacto UltimoTipo { get; private set; }
        public static Vector3 UltimoPunto { get; private set; }
        public static Vector3 UltimaNormal { get; private set; }
        public static float UltimoRadioDibujado { get; private set; }
        public static float UltimoAnchoDibujado { get; private set; }
        public static Color UltimoColor { get; private set; }

        void UpdateRadiusRing(TurretWeapon turret)
        {
            if (radiusRing == null) return;
            var impacto = Predecir(turret);
            // Con municion perforante el radio es 0: el anillo pasa a ser
            // una marca chica de punto de caida, no una zona de daño.
            float radio = turret.ExplosionRadius > 0f ? turret.ExplosionRadius : 0.5f;
            // Bug #247: el anillo ya no vibra (queda fijo); con Vibra = true vuelve el pulso del bug #091.
            float fase = Vibra ? Mathf.Sin(Time.unscaledTime * VibracionHz * Mathf.PI * 2f) : 0f;
            radio *= 1f + VibracionRadio * fase;
            float ancho = Vibra ? Mathf.Lerp(AnchoMin, AnchoMax, 0.5f + 0.5f * fase) : AnchoFijo;
            var color = ColorDe(impacto.Tipo);
            // Bug #245: el anillo de la zona es lo UNICO que queda del cursor del canon: mientras recarga se atenua (en vez del anillo de recarga de la UI).
            if (SoloAnilloDeZona && turret.CooldownFraction01 < 0.999f) color.a = 0.4f;
            // Bug #360 (2026-11-06: "que cambie el tamano del circulo cuando recarga, cuando dispara y cuando impacta, que cambie de color y vibre"):
            // recargando se achica y se pone naranja hasta llenarse; al disparar da un golpe (grande, amarillo) y vibra; al impactar otro (blanco) y vibra;
            // al quedar listo hace un pulso verde.
            float escalaFeedback = AplicarFeedback(turret, impacto, ref color, ref ancho);
            radio *= escalaFeedback;
            radiusRing.widthMultiplier = ancho;
            radiusRing.startColor = color; radiusRing.endColor = color;
            // Pedido 2026-10-21: "la mira del tanque siempre contra el suelo, jamas vertical, asi siempre es legible". Contra un muro, un
            // vehiculo o un edificio el anillo ya no se pega a la cara: se apoya en el piso al pie de lo que se apunta, siempre horizontal.
            var centro = PuntoEnElPiso(impacto.Punto);
            if (sacudida > 0.001f) centro += new Vector3(Mathf.Sin(Time.unscaledTime * 83f), 0f, Mathf.Cos(Time.unscaledTime * 71f)) * (0.3f * sacudida);
            UltimoTipo = impacto.Tipo; UltimoPunto = impacto.Punto; UltimaNormal = Vector3.up; UltimoCentroDibujado = centro;
            UltimoRadioDibujado = radio; UltimoAnchoDibujado = ancho; UltimoColor = color;
            DrawCircle(centro, radio, Vector3.up);
        }

        public static Vector3 UltimoCentroDibujado { get; private set; }

        // ---- Bug #360: feedback del circulo (estado por vista; se anima con el tiempo sin escalar)
        float cdPrevio = 1f, golpeDeDisparo, golpeDeImpacto, pulsoDeListo, sacudida, impactoEn = -1f;
        public static float UltimaEscalaDeFeedback { get; private set; } = 1f;
        public static readonly Color ColorRecarga = new Color(1f, 0.45f, 0.1f), ColorDisparo = new Color(1f, 0.95f, 0.35f), ColorImpacto = Color.white, ColorListo = new Color(0.35f, 1f, 0.45f);

        float AplicarFeedback(TurretWeapon turret, Impacto impacto, ref Color color, ref float ancho)
        {
            float cd = turret.CooldownFraction01, ahora = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            if (cdPrevio >= 0.999f && cd < 0.999f) { golpeDeDisparo = 1f; sacudida = 1f; impactoEn = ahora + Mathf.Max(0.2f, impacto.Tiempo); }   // acaba de disparar
            if (cdPrevio < 0.999f && cd >= 0.999f) pulsoDeListo = 1f;                                                                            // recarga completa
            if (impactoEn > 0f && ahora >= impactoEn) { impactoEn = -1f; golpeDeImpacto = 1f; sacudida = Mathf.Max(sacudida, 0.8f); }          // el obus llego
            cdPrevio = cd;
            golpeDeDisparo = Mathf.MoveTowards(golpeDeDisparo, 0f, dt * 3.5f);
            golpeDeImpacto = Mathf.MoveTowards(golpeDeImpacto, 0f, dt * 3f);
            pulsoDeListo = Mathf.MoveTowards(pulsoDeListo, 0f, dt * 3f);
            sacudida = Mathf.MoveTowards(sacudida, 0f, dt * 2.5f);

            float escala = 1f;
            if (cd < 0.999f)
            {
                escala *= Mathf.Lerp(0.55f, 1f, cd);                                        // recargando: el circulo se achica y crece al llenarse
                var c = Color.Lerp(ColorRecarga, color, cd * cd); c.a = Mathf.Max(color.a, 0.4f + 0.3f * cd); color = c;
            }
            if (pulsoDeListo > 0f) { color = Color.Lerp(color, ColorListo, pulsoDeListo); escala *= 1f + 0.15f * pulsoDeListo; ancho *= 1f + 0.6f * pulsoDeListo; }
            if (golpeDeDisparo > 0f) { color = Color.Lerp(color, ColorDisparo, golpeDeDisparo); escala *= 1f + 0.45f * golpeDeDisparo; ancho *= 1f + 1.2f * golpeDeDisparo; }
            if (golpeDeImpacto > 0f) { color = Color.Lerp(color, ColorImpacto, golpeDeImpacto); escala *= 1f + 0.35f * golpeDeImpacto; ancho *= 1f + 1.0f * golpeDeImpacto; }
            if (sacudida > 0.001f) escala *= 1f + 0.10f * sacudida * Mathf.Sin(ahora * 70f);   // vibra
            UltimaEscalaDeFeedback = escala;
            return escala;
        }

        static readonly RaycastHit[] golpesDePiso = new RaycastHit[8];
        // El piso justo bajo (o al pie de) el punto de impacto: el primer golpe hacia abajo que sea suelo (no soldado ni vehiculo).
        public static Vector3 PuntoEnElPiso(Vector3 punto)
        {
            var desde = punto + Vector3.up * 0.3f;
            int n = Physics.RaycastNonAlloc(desde, Vector3.down, golpesDePiso, 80f, ~0, QueryTriggerInteraction.Ignore);
            float mejor = float.MaxValue; float y = punto.y;
            for (int i = 0; i < n; i++)
            {
                var h = golpesDePiso[i];
                if (h.collider == null || h.distance >= mejor || h.normal.y < 0.5f) continue;
                if (h.collider.GetComponentInParent<SP.Actors.Soldier>() != null || h.collider.GetComponentInParent<Vehicle>() != null) continue;
                mejor = h.distance; y = h.point.y;
            }
            return new Vector3(punto.x, y + 0.06f, punto.z);
        }

        public static Color ColorDe(TipoDeImpacto t) => t == TipoDeImpacto.Enemigo ? ColorEnemigo : t == TipoDeImpacto.Destructible ? ColorDestructible : ColorNeutro;

        public struct Impacto { public Vector3 Punto; public Vector3 Normal; public TipoDeImpacto Tipo; public Collider Collider; public float Tiempo; }

        // Con balistica de arco, apuntar sin ninguna ayuda seria pura
        // adivinanza. Se SIMULA la trayectoria real (misma gravedad y
        // velocidad que usara el proyectil) en vez de estimarla con una
        // formula aparte que podria desincronizarse del vuelo real.
        // Bug #091: antes era la parabola contra el piso, ignorando muros y vehiculos: el anillo caia detras de lo que el obus
        // iba a pegar. Ahora se integra el arco por tramos de 0,05 s y cada tramo se tira como rayo.
        public static Vector3 PredictedImpactPoint(TurretWeapon turret) => Predecir(turret).Punto;

        const float PasoDelArco = 0.05f;
        const float TiempoMaximoDelArco = 8f;
        static readonly RaycastHit[] golpesArco = new RaycastHit[16];
        static readonly Collider[] cercanos = new Collider[24];
        static TurretWeapon ultimaTorreta;
        static int ultimoFrame = -1;
        static Impacto ultimoImpacto;

        // Una sola prediccion por frame y torreta (la piden el anillo del mundo y el rombo de la mira).
        public static Impacto Predecir(TurretWeapon turret)
        {
            if (turret == null) return default;
            if (ultimoFrame == Time.frameCount && ultimaTorreta == turret) return ultimoImpacto;
            var r = Calcular(turret);
            ultimaTorreta = turret; ultimoFrame = Time.frameCount; ultimoImpacto = r;
            return r;
        }

        static Impacto Calcular(TurretWeapon turret)
        {
            Vector3 origin = turret.Muzzle != null ? turret.Muzzle.position : turret.transform.position;
            Vector3 dir = turret.transform.forward;
            float g = turret.ProjectileGravity;
            // Debe coincidir con Projectile.groundImpactHeight: el
            // proyectil expira al bajar de esa altura, no al llegar a y=0.
            // Predecir contra y=0 metia un error sistematico de casi un
            // metro en un tiro de 18 m.
            const float groundY = 0.15f;
            Vector3 vel = dir * (TurretWeapon.ProjectileSpeed * TurretWeapon.SpeedMultiplier);
            var propio = turret.GetComponentInParent<Vehicle>();
            var propioT = propio != null ? propio.transform : null;
            var res = new Impacto { Punto = origin + dir * 30f, Normal = Vector3.up };

            Vector3 a = origin;
            for (float t = PasoDelArco; t <= TiempoMaximoDelArco + 0.001f; t += PasoDelArco)
            {
                Vector3 b = origin + vel * t + 0.5f * g * t * t * Vector3.down;
                Vector3 d = b - a;
                float largo = d.magnitude;
                if (largo < 1e-4f) { a = b; continue; }
                Vector3 u = d / largo;

                // Corte contra el plano del piso (el proyectil expira a groundY) y contra lo que haya en el tramo; gana el mas cercano.
                float distPiso = float.MaxValue;
                if (b.y <= groundY && a.y > groundY && Mathf.Abs(u.y) > 1e-5f) distPiso = Mathf.Clamp((groundY - a.y) / u.y, 0f, largo);
                else if (a.y <= groundY) distPiso = 0f;

                int n = Physics.RaycastNonAlloc(a, u, golpesArco, Mathf.Min(largo, distPiso), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);   // WP11: sin la capa Ignore Raycast, donde viven los fragmentos sueltos (el obus no los toca y el anillo si)
                float mejor = float.MaxValue; Collider mejorC = null; Vector3 mejorP = default, mejorN = Vector3.up;
                for (int i = 0; i < n; i++)
                {
                    var h = golpesArco[i];
                    if (h.collider == null || h.distance >= mejor || h.distance < 0.01f) continue;
                    if (propioT != null && h.collider.transform.IsChildOf(propioT)) continue;
                    mejor = h.distance; mejorC = h.collider; mejorP = h.point; mejorN = h.normal;
                }
                if (mejorC != null)
                {
                    res.Punto = mejorP; res.Normal = mejorN; res.Collider = mejorC; res.Tiempo = t;
                    res.Tipo = Clasificar(mejorC, mejorP);
                    return res;
                }
                if (distPiso < float.MaxValue)
                {
                    res.Punto = new Vector3(a.x + u.x * distPiso, groundY + 0.05f, a.z + u.z * distPiso);
                    res.Normal = Vector3.up; res.Tiempo = t;
                    res.Tipo = Clasificar(null, res.Punto);
                    return res;
                }
                a = b;
            }
            res.Punto = a;
            return res;
        }

        // Que hay bajo el punto: un vehiculo o soldado enemigo (aunque el obus caiga en el piso a su lado, la explosion lo alcanza)
        // pesa mas que un obstaculo destructible, y este mas que el piso.
        static TipoDeImpacto Clasificar(Collider directo, Vector3 punto)
        {
            var tipo = TipoDeImpacto.Neutro;
            if (directo != null) tipo = ClasificarCollider(directo);
            if (tipo == TipoDeImpacto.Enemigo) return tipo;
            int n = Physics.OverlapSphereNonAlloc(punto, 1.5f, cercanos, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = cercanos[i];
                if (c == null) continue;
                var t = ClasificarCollider(c);
                if (t == TipoDeImpacto.Enemigo) return t;
                if (t == TipoDeImpacto.Destructible) tipo = t;
            }
            return tipo;
        }

        public static TipoDeImpacto ClasificarCollider(Collider c)
        {
            if (c == null) return TipoDeImpacto.Neutro;
            var s = c.GetComponentInParent<SP.Actors.Soldier>();
            if (s != null && s.Team == SP.Combat.TeamId.Enemy && s.Health != null && s.Health.IsAlive) return TipoDeImpacto.Enemigo;
            var v = c.GetComponentInParent<Vehicle>();
            if (v != null && v.Bando == SP.Combat.TeamId.Enemy && !v.IsDestroyed) return TipoDeImpacto.Enemigo;
            var m = c.GetComponentInParent<SP.Presentation.ObstacleMarker>();
            if (m != null && !m.IsCollapsed && m.EsDestruible) return TipoDeImpacto.Destructible;
            return TipoDeImpacto.Neutro;
        }

        void DrawCircle(Vector3 center, float radius, Vector3 normal)
        {
            const int segments = 40;
            if (radiusRing.positionCount != segments) radiusRing.positionCount = segments;
            // Plano del anillo: sobre el piso es horizontal; contra un muro queda pegado a su cara.
            Vector3 ux, uz;
            if (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.95f) { ux = Vector3.right; uz = Vector3.forward; }
            else { ux = Vector3.Cross(normal, Vector3.up).normalized; uz = Vector3.Cross(normal, ux).normalized; }
            bool horizontal = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.95f;
            for (int i = 0; i < segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                var p = center + (ux * Mathf.Cos(a) + uz * Mathf.Sin(a)) * radius;
                // Bug #245: sobre el piso cada punto del anillo se apoya en lo que haya debajo (cordon, rampa, escombro grande): se adapta a la zona.
                if (horizontal) p = ApoyarEnElPiso(p, center.y);
                radiusRing.SetPosition(i, p);
            }
        }

        static readonly RaycastHit[] golpesAnillo = new RaycastHit[8];
        Vector3 ApoyarEnElPiso(Vector3 p, float yBase)
        {
            var desde = new Vector3(p.x, yBase + 2.5f, p.z);
            int n = Physics.RaycastNonAlloc(desde, Vector3.down, golpesAnillo, 5f, ~0, QueryTriggerInteraction.Ignore);
            float mejor = float.MaxValue; float y = yBase;
            for (int i = 0; i < n; i++)
            {
                var h = golpesAnillo[i];
                if (h.collider == null || h.distance >= mejor) continue;
                if (h.collider.GetComponentInParent<SP.Actors.Soldier>() != null || h.collider.GetComponentInParent<Vehicle>() != null) continue;
                mejor = h.distance; y = h.point.y + 0.06f;
            }
            p.y = y;
            return p;
        }
    }
}
