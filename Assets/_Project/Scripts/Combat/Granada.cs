using System.Collections.Generic;
using UnityEngine;
using SP.Core;
using SP.Actors;
using SP.Presentation;

namespace SP.Combat
{
    // Granada de mano (tecla [G], ronda 7). Se tira con una parabola que ella misma calcula: la
    // vista previa que se dibuja al MANTENER [G] usa exactamente la misma simulacion (Simular), asi
    // que lo que muestra la curva es lo que hace la granada. Rebota contra lo solido, tiene un
    // fusible de 2,4 s (tic-tac cada vez mas rapido y luz roja parpadeante) y explota con la misma
    // rutina que el cohete y el barril (Projectile.ExplodeAt): dano con caida, linea de vista,
    // sacudida de camara y estruendo. No lastima a los del propio bando.
    public class Granada : MonoBehaviour
    {
        public const float VelocidadDeLanzamiento = 17f;
        public const float Gravedad = 9.81f;
        public const float RadioDeExplosion = 4.5f;
        public const int Dano = 100;
        public const float Fusible = 2.4f;
        public const float Rebote = 0.42f;
        public const float RadioFisico = 0.12f;
        public const float AlcanceMaximo = 26f;

        public static readonly List<Granada> Activas = new List<Granada>();
        public static int Lanzadas { get; private set; }
        public static int Explotadas { get; private set; }

        static readonly RaycastHit[] Golpes = new RaycastHit[8];

        Vector3 velocidad;
        float edad;
        float proximoTic;
        float sinRebote;
        bool quieta;
        int duenoId;
        TeamId duenoBando;
        Transform duenoTf;
        Transform luz;

        // ------------------------------------------------------------------
        // Simulacion (compartida con la vista previa)
        // ------------------------------------------------------------------
        // Velocidad inicial para que la granada llegue (en parabola baja) a "objetivo" desde "origen".
        // Fuera de alcance: 45 grados hacia el objetivo (el maximo alcance posible).
        public static Vector3 VelocidadHacia(Vector3 origen, Vector3 objetivo, float rapidez = VelocidadDeLanzamiento)
        {
            var d = objetivo - origen;
            var plano = new Vector3(d.x, 0f, d.z);
            float dist = plano.magnitude;
            if (dist < 0.05f) return Vector3.up * rapidez;
            var dirPlano = plano / dist;
            float v2 = rapidez * rapidez;
            float disc = v2 * v2 - Gravedad * (Gravedad * dist * dist + 2f * d.y * v2);
            float ang = disc >= 0f ? Mathf.Atan((v2 - Mathf.Sqrt(disc)) / (Gravedad * dist)) : Mathf.PI * 0.25f;
            return (dirPlano * Mathf.Cos(ang) + Vector3.up * Mathf.Sin(ang)) * rapidez;
        }

        // Camino de la granada hasta su primer golpe (o 3 s): puntos cada 40 ms. Devuelve donde
        // cae y la normal del piso ahi.
        public static int Simular(Vector3 origen, Vector3 v, Transform ignorar, List<Vector3> puntos, out Vector3 caida, out Vector3 normal)
        {
            puntos.Clear();
            puntos.Add(origen);
            var p = origen;
            caida = origen; normal = Vector3.up;
            const float dt = 0.04f;
            for (int i = 0; i < 90; i++)
            {
                v.y -= Gravedad * dt;
                var paso = v * dt;
                float largo = paso.magnitude;
                if (TryGolpe(p, paso / Mathf.Max(largo, 1e-5f), largo, ignorar, out var hit))
                {
                    p = hit.point + hit.normal * RadioFisico;
                    puntos.Add(p);
                    caida = p; normal = hit.normal;
                    return puntos.Count;
                }
                p += paso;
                puntos.Add(p);
                caida = p;
            }
            return puntos.Count;
        }

        static bool TryGolpe(Vector3 desde, Vector3 dir, float largo, Transform ignorar, out RaycastHit mejor)
        {
            mejor = default;
            int n = Physics.SphereCastNonAlloc(desde, RadioFisico, dir, Golpes, largo, ~0, QueryTriggerInteraction.Ignore);
            float mejorDist = float.MaxValue;
            bool hay = false;
            for (int i = 0; i < n; i++)
            {
                var c = Golpes[i].collider;
                if (c == null || (ignorar != null && c.transform.IsChildOf(ignorar))) continue;
                if (Golpes[i].distance <= 0f && Golpes[i].point == Vector3.zero) continue;   // ya empezo adentro: no cuenta
                if (Golpes[i].distance < mejorDist) { mejorDist = Golpes[i].distance; mejor = Golpes[i]; hay = true; }
            }
            return hay;
        }

        // ------------------------------------------------------------------
        // Lanzamiento
        // ------------------------------------------------------------------
        public static Granada Lanzar(Vector3 origen, Vector3 v, Soldier dueno)
        {
            var go = new GameObject("Granada");
            go.transform.position = origen;
            var g = go.AddComponent<Granada>();
            g.velocidad = v;
            g.duenoId = dueno != null ? dueno.Id : -1;
            g.duenoBando = dueno != null ? dueno.Team : TeamId.Player;
            g.duenoTf = dueno != null ? dueno.transform : null;
            g.proximoTic = 0.6f;
            g.ConstruirVisual();
            Activas.Add(g);
            Lanzadas++;
            AudioDirector.PlayAt(SfxKind.GrenadeThrow, origen, 0.8f, 0.8f);
            EventBus.Instance.Publish(new GrenadeThrownEvent(g.duenoId));
            return g;
        }

        public static void ResetearContadores() { Lanzadas = 0; Explotadas = 0; }

        void ConstruirVisual()
        {
            var prefab = SP.Core.RecursosCache.Cargar<GameObject>("Weapons/P_Wpn_Granada");
            GameObject modelo;
            if (prefab != null)
            {
                modelo = Instantiate(prefab, transform);
                modelo.transform.localPosition = Vector3.zero;
                modelo.transform.localRotation = Quaternion.identity;
                // El FBX viene a la escala del arte: se lleva a ~0,22 m de largo.
                var rs = modelo.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    float mayor = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    if (mayor > 0.0001f) modelo.transform.localScale *= 0.22f / mayor;
                    modelo.transform.localPosition = transform.InverseTransformPoint(transform.position + (transform.position - b.center));
                }
                foreach (var col in modelo.GetComponentsInChildren<Collider>()) Destroy(col);
            }
            else
            {
                modelo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(modelo.GetComponent<Collider>());
                modelo.transform.SetParent(transform, false);
                modelo.transform.localScale = Vector3.one * 0.2f;
                modelo.GetComponent<Renderer>().sharedMaterial = SafeMaterial.Create(new Color(0.22f, 0.3f, 0.18f));
            }
            modelo.name = "Modelo";

            // Luz roja de fusible: parpadea cada vez mas rapido.
            var l = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(l.GetComponent<Collider>());
            l.name = "LuzDeFusible";
            l.transform.SetParent(transform, false);
            l.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            l.transform.localScale = Vector3.one * 0.07f;
            l.GetComponent<Renderer>().sharedMaterial = SafeMaterial.Create(new Color(1f, 0.15f, 0.1f));
            l.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            luz = l.transform;

            var tr = gameObject.AddComponent<TrailRenderer>();
            tr.time = 0.5f;
            tr.startWidth = 0.09f; tr.endWidth = 0f;
            tr.minVertexDistance = 0.1f;
            tr.sharedMaterial = SafeMaterial.Create(new Color(1f, 0.75f, 0.3f));
            tr.startColor = new Color(1f, 0.8f, 0.4f, 0.9f);
            tr.endColor = new Color(1f, 0.5f, 0.1f, 0f);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------
        // Vuelo, rebotes, fusible
        // ------------------------------------------------------------------
        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            edad += dt;

            if (!quieta) Avanzar(dt);
            transform.Rotate(velocidad.magnitude * 40f * dt, 0f, velocidad.magnitude * 25f * dt, Space.Self);

            // Tic-tac y luz: la cuenta regresiva se OYE y se VE cada vez mas rapida.
            float restante = Fusible - edad;
            if (edad >= proximoTic && restante > 0.05f)
            {
                AudioDirector.PlayAt(SfxKind.BombTick, transform.position, Mathf.Lerp(0.35f, 0.9f, 1f - restante / Fusible), 0.5f);
                proximoTic = edad + Mathf.Lerp(0.5f, 0.12f, 1f - restante / Fusible);
            }
            if (luz != null)
            {
                float f = Mathf.Lerp(3f, 16f, edad / Fusible);
                luz.localScale = Vector3.one * (Mathf.Sin(edad * f * Mathf.PI) > 0f ? 0.1f : 0.045f);
            }

            if (edad >= Fusible) Detonar();
        }

        void Avanzar(float dt)
        {
            velocidad.y -= Gravedad * dt;
            var paso = velocidad * dt;
            float largo = paso.magnitude;
            sinRebote -= dt;
            if (largo > 1e-5f && TryGolpe(transform.position, paso / largo, largo, duenoTf, out var hit))
            {
                var n = hit.normal;
                float vn = Vector3.Dot(velocidad, n);
                if (vn < 0f)
                {
                    var normalV = n * vn;
                    var tangente = velocidad - normalV;
                    if (-vn > 2.2f && sinRebote <= 0f)
                    {
                        AudioDirector.PlayAt(SfxKind.GrenadeBounce, hit.point, Mathf.Clamp01(-vn / 8f) * 0.8f + 0.2f, 0.75f);
                        ImpactFx.Spawn(hit.point, new Color(1f, 0.9f, 0.6f), 0.18f, 0.12f);
                        sinRebote = 0.08f;
                    }
                    velocidad = tangente * 0.72f - normalV * Rebote;
                }
                transform.position = hit.point + n * (RadioFisico + 0.01f);
                if (velocidad.sqrMagnitude < 0.5f && n.y > 0.5f) { quieta = true; velocidad = Vector3.zero; }
            }
            else transform.position += paso;
        }

        void Detonar()
        {
            var pos = transform.position;
            Explotadas++;
            Activas.Remove(this);
            Projectile.ExplodeAt(pos + Vector3.up * 0.15f, RadioDeExplosion, Dano, duenoId, duenoBando);
            EventBus.Instance.Publish(new GrenadeExplodedEvent(duenoId, pos));
            Destroy(gameObject);
        }

        void OnDestroy() => Activas.Remove(this);
    }
}
