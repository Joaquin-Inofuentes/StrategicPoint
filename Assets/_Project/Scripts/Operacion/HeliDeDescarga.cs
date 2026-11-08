using System.Collections.Generic;
using UnityEngine;
using SP.Combat;
using SP.Core;
using SP.Presentation;
using SP.Vehicles;

namespace SP.Operacion
{
    // Pedido 2026-11-07 ("los portones de la zona del tanque aparecen de la nada: quiero que sean helicopteros que sueltan enemigos en la zona"):
    // cada refuerzo llega ahora por aire. Un helicoptero (copia visual del Halcon, sin armas) entra, queda suspendido 14 m sobre el punto con dos
    // sogas colgando y polvo en el piso mientras sale el soldado, y se retira.
    // Bug #372 (2026-11-08: "aparecen y se van de manera rara, quiero animacion racional, medio realista, que sean derribables y tengan un sonido
    // distinto al del helicoptero final"):
    //  - llega frenando de a poco, siempre de frente y inclinado; suspendido cabecea y se balancea; sale de frente (antes salia de cola) acelerando y subiendo;
    //  - conserva el Vehicle/Health del Halcon: las balas y cohetes lo pegan; derribado, cae girando y explota al tocar techo, pared o piso (bug #390);
    //  - su rotor es propio: mas agudo y rapido (14 golpes/s) que el del helicoptero de extraccion.
    public sealed class HeliDeDescarga : MonoBehaviour
    {
        public const float Altura = 14f, SegundosSuspendido = 4.2f, DistanciaDeEntrada = 110f, VelocidadMaxima = 46f;
        public const int MaximoSimultaneos = 3, VidaDelHeli = 260;
        public enum Fase { Entrando, Suspendido, Saliendo, Cayendo, Muerto }
        public static int Activos { get; private set; }
        public static int Lanzados { get; private set; }
        public static int Derribados { get; private set; }

        public Fase Estado { get; private set; } = Fase.Entrando;
        public Vehicle Vehiculo { get; private set; }
        public bool TieneSonidoPropio { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Activos = 0; Lanzados = 0; Derribados = 0; Explosiones = 0; clipDeRotor = null; }

        Vector3 punto, dirEntrada, velocidadCaida; float t, tLlego = -1f, tSalida, tCaida, giroCaida, velocidad; Transform rotorP, rotorC; LineRenderer[] sogas;

        public static HeliDeDescarga Soltar(Vector3 punto)
        {
            var d = OperacionDirector.Instancia;
            var plantilla = d != null ? d.halcon : null;
            if (plantilla == null || Activos >= MaximoSimultaneos) return null;
            var go = Instantiate(plantilla.gameObject, plantilla.parent);   // la plantilla esta INACTIVA: nada corre hasta que se limpia el clon
            go.name = "HeliDeDescarga";
            // Se queda con el Vehicle y su Health (para que se lo pueda derribar) y con los colliders (para que las balas lo toquen); el resto de los
            // scripts del Halcon (IA, armas, rotores propios) se saca.
            for (int pasada = 0; pasada < 4; pasada++)
            {
                foreach (var m in go.GetComponentsInChildren<MonoBehaviour>(true))
                    if (m != null && !(m is Vehicle) && !(m is Health)) { try { DestroyImmediate(m); } catch { } }
                foreach (var r in go.GetComponentsInChildren<Rigidbody>(true)) if (r != null) { try { DestroyImmediate(r); } catch { } }
                foreach (var a in go.GetComponentsInChildren<AudioSource>(true)) if (a != null) DestroyImmediate(a);
            }
            var h = go.AddComponent<HeliDeDescarga>();
            h.punto = punto;
            float ang = Random.Range(0f, Mathf.PI * 2f);
            h.dirEntrada = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            go.transform.position = punto + h.dirEntrada * DistanciaDeEntrada + Vector3.up * (Altura + 10f);
            go.transform.rotation = Quaternion.LookRotation(-h.dirEntrada, Vector3.up);
            go.SetActive(true);
            h.Vehiculo = go.GetComponent<Vehicle>();
            if (h.Vehiculo != null)
            {
                h.Vehiculo.MarcarBando(TeamId.Enemy);
                if (h.Vehiculo.Health != null) { h.Vehiculo.Health.Initialize(-1, VidaDelHeli); h.Vehiculo.Health.Invulnerable = false; }
            }
            h.rotorP = go.transform.Find("RotorPrincipal"); h.rotorC = go.transform.Find("RotorCola");
            h.sogas = new LineRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var so = new GameObject("Soga").AddComponent<LineRenderer>(); so.transform.SetParent(go.transform, false);
                so.positionCount = 2; so.startWidth = so.endWidth = 0.06f; so.useWorldSpace = true; so.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                so.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.12f, 0.1f, 0.08f) };
                so.enabled = false; h.sogas[i] = so;
            }
            var fuente = go.AddComponent<AudioSource>();
            fuente.clip = ClipDeRotor(); fuente.loop = true; fuente.spatialBlend = 1f; fuente.minDistance = 12f; fuente.maxDistance = 150f; fuente.volume = 0.8f;
            fuente.rolloffMode = AudioRolloffMode.Linear; fuente.dopplerLevel = 0.4f; fuente.Play();
            h.TieneSonidoPropio = fuente.clip != null;
            Activos++; Lanzados++;
            return h;
        }

        void OnDestroy() { Activos = Mathf.Max(0, Activos - 1); }

        // Rotor propio: ruido filtrado modulado a 14 golpes por segundo (el de extraccion es un loop grabado, mas grave y lento).
        static AudioClip clipDeRotor;
        static AudioClip ClipDeRotor()
        {
            if (clipDeRotor != null) return clipDeRotor;
            const int hz = 22050; const float seg = 1f, golpes = 14f;
            var datos = new float[(int)(hz * seg)];
            var rng = new System.Random(7); float lp = 0f;
            for (int i = 0; i < datos.Length; i++)
            {
                float ruido = (float)(rng.NextDouble() * 2 - 1);
                lp += (ruido - lp) * 0.12f;
                float fase = (i / (float)hz * golpes) % 1f;
                float env = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(fase * Mathf.PI * 2f)), 3f) * 0.8f + 0.2f;
                float tono = Mathf.Sin(i / (float)hz * 2f * Mathf.PI * 190f) * 0.25f;
                datos[i] = (lp * 2.2f + tono) * env * 0.55f;
            }
            clipDeRotor = AudioClip.Create("HeliDeDescarga_Rotor", datos.Length, 1, hz, false);
            clipDeRotor.SetData(datos, 0);
            return clipDeRotor;
        }

        void Update()
        {
            float dt = Time.deltaTime; t += dt;
            if (Estado != Fase.Cayendo && Estado != Fase.Muerto)
            {
                if (rotorP != null) rotorP.Rotate(0f, 1500f * dt, 0f, Space.Self);
                if (rotorC != null) rotorC.Rotate(1800f * dt, 0f, 0f, Space.Self);
                if (Vehiculo != null && Vehiculo.IsDestroyed) { Derribar(); }
            }
            var meta = punto + Vector3.up * Altura;
            switch (Estado)
            {
                case Fase.Entrando:
                {
                    var al = meta - transform.position; float dist = al.magnitude;
                    float objetivo = Mathf.Clamp(dist * 0.9f, 5f, VelocidadMaxima);   // frena de a poco al acercarse
                    velocidad = Mathf.MoveTowards(velocidad, objetivo, 30f * dt);
                    transform.position = Vector3.MoveTowards(transform.position, meta, velocidad * dt);
                    var mira = Quaternion.LookRotation(-dirEntrada, Vector3.up) * Quaternion.Euler(Mathf.Lerp(0f, 12f, Mathf.Clamp01(dist / 40f)), 0f, 0f);   // inclinado mientras avanza
                    transform.rotation = Quaternion.Slerp(transform.rotation, mira, 1f - Mathf.Exp(-2.5f * dt));
                    if (dist < 1.5f) { Estado = Fase.Suspendido; tLlego = t; foreach (var s in sogas) s.enabled = true; }
                    break;
                }
                case Fase.Suspendido:
                {
                    var flota = meta + new Vector3(0f, Mathf.Sin(t * 1.7f) * 0.35f, 0f);
                    transform.position = Vector3.Lerp(transform.position, flota, 1f - Mathf.Exp(-3f * dt));
                    var mira = Quaternion.LookRotation(-dirEntrada, Vector3.up) * Quaternion.Euler(Mathf.Sin(t * 1.3f) * 2f, Mathf.Sin(t * 0.6f) * 6f, Mathf.Sin(t * 1.1f) * 3f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, mira, 1f - Mathf.Exp(-3f * dt));
                    if (t - tLlego >= SegundosSuspendido) { Estado = Fase.Saliendo; tSalida = t; velocidad = 2f; foreach (var s in sogas) s.enabled = false; }
                    break;
                }
                case Fase.Saliendo:
                {
                    // sale de frente, en la misma direccion en que entro: arranca despacio, acelera y sube
                    velocidad = Mathf.MoveTowards(velocidad, VelocidadMaxima * 1.2f, 22f * dt);
                    var dir = (-dirEntrada + Vector3.up * Mathf.Lerp(0.1f, 0.4f, Mathf.Clamp01((t - tSalida) / 3f))).normalized;
                    transform.position += dir * velocidad * dt;
                    var mira = Quaternion.LookRotation(-dirEntrada, Vector3.up) * Quaternion.Euler(Mathf.Lerp(0f, 14f, Mathf.Clamp01(velocidad / 40f)), 0f, 0f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, mira, 1f - Mathf.Exp(-3f * dt));
                    if ((transform.position - punto).sqrMagnitude > (DistanciaDeEntrada * 1.1f) * (DistanciaDeEntrada * 1.1f) || t > 40f) { Destroy(gameObject); return; }
                    break;
                }
                case Fase.Cayendo: TickCaida(dt); break;
            }
            if (sogas != null && sogas[0] != null && sogas[0].enabled)
            {
                var d = transform.right * 0.7f;
                sogas[0].SetPosition(0, transform.position - Vector3.up * 0.8f - d); sogas[0].SetPosition(1, punto + d * 0.3f);
                sogas[1].SetPosition(0, transform.position - Vector3.up * 0.8f + d); sogas[1].SetPosition(1, punto - d * 0.3f);
                if (Time.frameCount % 12 == 0) ImpactFx.Spawn(punto + new Vector3(Random.Range(-1.5f, 1.5f), 0.3f, Random.Range(-1.5f, 1.5f)), new Color(0.6f, 0.55f, 0.45f), 1.6f, 0.6f);
            }
        }

        // ---- derribo: cae girando y explota contra lo primero que toca (techo, pared, piso) ----
        void Derribar()
        {
            if (Estado == Fase.Cayendo || Estado == Fase.Muerto) return;
            Estado = Fase.Cayendo; Derribados++;
            foreach (var s in sogas) if (s != null) s.enabled = false;
            giroCaida = Random.value < 0.5f ? -1f : 1f;
            velocidadCaida = transform.forward * Mathf.Max(8f, velocidad * 0.6f);
            AvisoCentral.Mostrar("¡HELICOPTERO ENEMIGO DERRIBADO!", 2f, new Color(0.08f, 0.3f, 0.2f, 0.92f));
            SesionLog.Evento("HELI DE DESCARGA: derribado");
        }

        float proxChispa;
        void TickCaida(float dt)
        {
            tCaida += dt;
            velocidadCaida += Vector3.down * 9f * dt;
            transform.Rotate(0f, giroCaida * 420f * dt, 0f, Space.World);
            transform.Rotate(0f, 0f, giroCaida * 90f * dt, Space.Self);
            if (Time.time >= proxChispa)
            {
                proxChispa = Time.time + 0.08f;
                SpriteFx.Lanzar("smoke_04", transform.position, new Color(0.1f, 0.1f, 0.1f, 0.9f), 1.0f, 3.4f, 1.6f, Random.Range(-25f, 25f), Vector3.up * 1.5f, 0.3f, 2);
            }
            var paso = velocidadCaida * dt;
            if (tCaida > 0.15f && paso.sqrMagnitude > 1e-6f)
            {
                foreach (var h in Physics.SphereCastAll(transform.position, 1.2f, paso.normalized, paso.magnitude, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h.collider == null || h.collider.transform.IsChildOf(transform) || h.collider.GetComponentInParent<SP.Actors.Soldier>() != null || h.collider.GetComponentInParent<Vehicle>() != null) continue;
                    Estallar(transform.position + paso.normalized * Mathf.Max(0f, h.distance)); return;
                }
            }
            transform.position += paso;
            if (transform.position.y <= 1.2f || tCaida > 4f) Estallar(transform.position);
        }

        void Estallar(Vector3 p)
        {
            if (Estado == Fase.Muerto) return;
            Estado = Fase.Muerto;
            p.y = Mathf.Max(p.y, 1.2f);
            ImpactFx.SpawnExplosion(p, 9f, false);
            ImpactFx.SpawnExplosion(p + Vector3.up * 1.5f, 6f, false);
            AudioDirector.PlayAt(SfxKind.Explosion, p, 1f, 0.9f, PerfilEspacial.Explosion);
            for (int i = 0; i < 6; i++)
                SpriteFx.Lanzar("fire_02", p + Random.insideUnitSphere * 1.5f, new Color(1f, 0.55f, 0.15f, 0.95f), 1.2f, 3.2f, 1.1f, Random.Range(-60f, 60f), Vector3.up * Random.Range(2f, 5f), 0.5f, 4);
            Explosiones++;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) if (r != null) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>(true)) if (c != null) c.enabled = false;
            var au = GetComponent<AudioSource>(); if (au != null) au.Stop();
            Destroy(gameObject, 2.5f);
        }
        public static int Explosiones { get; private set; }
    }
}
