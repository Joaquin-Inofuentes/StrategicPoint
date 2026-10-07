using System.Collections.Generic;
using UnityEngine;
using SP.Combat;
using SP.Core;
using SP.Presentation;
using SP.Vehicles;

namespace SP.Operacion
{
    // WP9b (#097/#073): el helicoptero enemigo HALCON de la carrera. NO es el Helicoptero de la extraccion (ese tiene un singleton estatico y su propio
    // guion): es un Vehicle (vida 900, el canon y la metralleta del tanque le pegan por el camino de siempre) con este componente encima.
    //   * Entra a los 25 s de carrera, orbita al tanque a 45 m de radio y 18..24 m de altura a 25 m/s.
    //   * Cada 8 s hace una PASADA: 1,2 s antes marca tres circulos rojos sobre la ruta por delante del tanque (AnilloDeAviso) y suelta tres cohetes
    //     lentos (CoheteDeAviso, 80 de dano en el centro: ~60 al tanque). Entre pasadas, rafagas de metralleta de 6 por bala.
    //   * Al morir cae en espiral y explota en el aire; suma al conteo de destruidos del director.
    // Se instancia desde una plantilla inactiva de la escena (HelicopteroEnemigo.Iniciar), una vez por helicoptero.
    public class HelicopteroEnemigo : MonoBehaviour
    {
        public const string Nombre = "HALCON";
        public const float SegundoDeEntrada = 25f, RadioDeOrbita = 45f, AlturaMinima = 18f, AlturaMaxima = 24f, VelocidadDeVuelo = 25f;
        public const float SegundosEntrePasadas = 8f, SegundosDeAvisoDePasada = 1.2f, SeparacionDeCohetes = 7f;
        public const int CohetesPorPasada = 3, DanoDeCohete = 80, DanoDeMetralla = 6;
        public const float RadioDeCohete = 4.5f, AlcanceDeMetralla = 70f;

        public enum Estado { Dormido, Entrando, Orbitando, Cinematica, Cayendo, Muerto }

        public static HelicopteroEnemigo Instancia { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Instancia = null; Creados = 0; }
        public static int Creados { get; private set; }

        public Vehicle Vehiculo { get; private set; }
        public Estado Situacion { get; private set; } = Estado.Dormido;
        public int Pasadas { get; private set; }
        public int CohetesSoltados { get; private set; }
        public int BalasDeMetralla { get; private set; }
        public float Fraccion01 => Vehiculo != null && Vehiculo.Health != null ? Vehiculo.Health.Current / (float)Vehiculo.Health.MaxHealth : 0f;
        public bool Derribado => Situacion == Estado.Cayendo || Situacion == Estado.Muerto || (Vehiculo != null && Vehiculo.IsDestroyed);
        public float Altura => transform.position.y;
        public float VelocidadActual { get; private set; }
        public IReadOnlyList<float> InstantesDeAviso => instantesDeAviso;
        public event System.Action AlDerribarse;

        Vehicle tanque;
        VehicleMotor motorTanque;
        Transform rotorPrincipal, rotorCola;
        float angulo, proximaPasada, proximaRafaga, rafagaHasta, pausaHasta, proximoTiroMg, tCaida, giroCaida;
        float anguloDeEntrada;
        bool avisando; float avisoDesde;
        readonly List<float> instantesDeAviso = new List<float>();
        Vector3 velocidadCaida;
        Vector3 posCine; Quaternion rotCine; bool cineFija;

        // Clona la plantilla (inactiva) en el aire, a "distancia" m por detras-izquierda del tanque, y empieza la entrada.
        public static HelicopteroEnemigo Iniciar(Transform plantilla, Vehicle tanque, float distanciaDeEntrada = 160f)
        {
            if (plantilla == null || tanque == null) return null;
            var go = Instantiate(plantilla.gameObject, plantilla.parent);
            go.name = "Halcon_Enemigo_" + (++Creados);
            var fwd = tanque.transform.forward; fwd.y = 0f; fwd.Normalize();
            var lado = new Vector3(fwd.z, 0f, -fwd.x);
            var pos = tanque.transform.position - fwd * distanciaDeEntrada * 0.5f + lado * distanciaDeEntrada * 0.7f;
            pos.y = AlturaMaxima + 10f;
            go.transform.position = pos;
            go.SetActive(true);
            var h = go.GetComponent<HelicopteroEnemigo>();
            h.Armar(tanque);
            return h;
        }

        void Armar(Vehicle t)
        {
            tanque = t;
            motorTanque = t.GetComponent<VehicleMotor>();
            Vehiculo = GetComponent<Vehicle>();
            Vehiculo.MarcarBando(TeamId.Enemy);
            rotorPrincipal = transform.Find("RotorPrincipal");
            rotorCola = transform.Find("RotorCola");
            var d = transform.position - tanque.transform.position;
            anguloDeEntrada = Mathf.Atan2(d.z, d.x);
            angulo = anguloDeEntrada;
            Situacion = Estado.Entrando;
            proximaPasada = Time.time + 4f;
            Instancia = this;
            SesionLog.Evento("HALCON: entra el helicoptero enemigo");
        }

        void OnDestroy() { if (Instancia == this) Instancia = null; }

        // La cinematica final lo deja suspendido en un punto, sin atacar.
        public void FijarParaLaCinematica(Vector3 pos, Quaternion rot)
        {
            Situacion = Estado.Cinematica;
            cineFija = true; posCine = pos; rotCine = rot;
            avisando = false;
            if (Vehiculo != null && Vehiculo.Health != null) Vehiculo.Health.Invulnerable = true;
        }

        void Update()
        {
            if (rotorPrincipal != null) rotorPrincipal.Rotate(0f, 1500f * Time.deltaTime, 0f, Space.Self);
            if (rotorCola != null) rotorCola.Rotate(1800f * Time.deltaTime, 0f, 0f, Space.Self);
            if (Vehiculo == null) return;
            if (Situacion == Estado.Muerto) return;
            if (Vehiculo.IsDestroyed && Situacion != Estado.Cayendo) { EmpezarCaida(); }
            if (Situacion == Estado.Cayendo) { TickCaida(Time.deltaTime); return; }
            if (tanque == null || tanque.IsDestroyed && Situacion != Estado.Cinematica) return;

            float dt = Time.deltaTime;
            if (Situacion == Estado.Cinematica)
            {
                if (cineFija)
                {
                    transform.position = Vector3.Lerp(transform.position, posCine, 1f - Mathf.Exp(-3f * dt));
                    transform.rotation = Quaternion.Slerp(transform.rotation, rotCine, 1f - Mathf.Exp(-3f * dt));
                }
                return;
            }
            if (CinematicaDeOperacion.IntroActiva) return;

            var centro = tanque.transform.position;
            float vTanque = motorTanque != null ? motorTanque.CurrentSpeed : 0f;
            // Orbita alrededor del tanque (que avanza): el angulo gira a VelocidadDeVuelo / radio.
            float radio = Situacion == Estado.Entrando ? Mathf.Lerp(RadioDeOrbita * 2.2f, RadioDeOrbita, Mathf.Clamp01(Mathf.InverseLerp(0f, 5f, Time.time - (proximaPasada - 4f)))) : RadioDeOrbita;
            angulo += (VelocidadDeVuelo / Mathf.Max(20f, radio)) * dt;
            float alto = Mathf.Lerp(AlturaMinima, AlturaMaxima, 0.5f + 0.5f * Mathf.Sin(Time.time * 0.45f));
            var meta = centro + new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * radio;
            meta.y = alto;
            var antes = transform.position;
            transform.position = Vector3.MoveTowards(transform.position, meta, (VelocidadDeVuelo + Mathf.Abs(vTanque)) * 1.15f * dt);
            var vel = (transform.position - antes) / Mathf.Max(0.0001f, dt);
            VelocidadActual = vel.magnitude;
            // Mira al tanque, cabeceando hacia adelante segun la velocidad y ladeado en la curva.
            var haciaTanque = centro + Vector3.up * 1.5f - transform.position;
            var plano = haciaTanque; plano.y = 0f;
            if (plano.sqrMagnitude > 0.1f)
            {
                var rot = Quaternion.LookRotation(plano.normalized, Vector3.up) * Quaternion.Euler(8f, 0f, -12f);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-3f * dt));
            }
            if (Situacion == Estado.Entrando && Vector3.Distance(transform.position, meta) < 8f && Time.time - (proximaPasada - 4f) > 5f) Situacion = Estado.Orbitando;

            TickPasada();
            TickMetralla(dt);
        }

        // ---- Pasada de cohetes ----
        void TickPasada()
        {
            if (Situacion != Estado.Orbitando && Situacion != Estado.Entrando) return;
            if (!avisando && Time.time >= proximaPasada - 0.01f && Situacion == Estado.Orbitando)
            {
                avisando = true; avisoDesde = Time.time;
                instantesDeAviso.Add(Time.time);
                LanzarCohetes();
            }
            if (avisando && Time.time - avisoDesde >= SegundosDeAvisoDePasada)
            {
                avisando = false;
                Pasadas++;
                proximaPasada = Time.time + SegundosEntrePasadas - SegundosDeAvisoDePasada;
            }
        }

        // Tres circulos en fila sobre la ruta por delante del tanque, donde va a estar cuando lleguen los cohetes.
        void LanzarCohetes()
        {
            float v = motorTanque != null ? Mathf.Max(3f, motorTanque.CurrentSpeed) : 8f;
            var fwd = tanque.transform.forward; fwd.y = 0f; fwd.Normalize();
            var baseP = tanque.transform.position + fwd * (v * (SegundosDeAvisoDePasada + 0.15f));
            baseP.y = tanque.transform.position.y - 0.6f;
            var boca = transform.position + transform.forward * 1.5f + Vector3.down * 0.8f;
            for (int k = 0; k < CohetesPorPasada; k++)
            {
                var p = baseP + fwd * ((k - 1) * SeparacionDeCohetes);
                CoheteDeAviso.Lanzar(boca + transform.right * ((k - 1) * 0.9f), p, SegundosDeAvisoDePasada, DanoDeCohete, RadioDeCohete);
                CohetesSoltados++;
            }
            AvisoCentral.Mostrar("¡COHETES DEL HALCON! SALI DE LOS CIRCULOS ROJOS", 1.6f, new Color(0.55f, 0.07f, 0.05f, 0.9f));
        }

        // ---- Metralleta ----
        void TickMetralla(float dt)
        {
            if (avisando || tanque == null) return;
            var objetivo = tanque.transform.position + Vector3.up * 1.4f;
            var origen = transform.position + transform.forward * 1.2f + Vector3.down * 0.7f;
            if (Vector3.Distance(origen, objetivo) > AlcanceDeMetralla) return;
            if (Time.time >= pausaHasta && Time.time >= rafagaHasta) { rafagaHasta = Time.time + 1.0f; pausaHasta = rafagaHasta + Random.Range(1.2f, 2.2f); }
            if (Time.time >= proximoTiroMg && Time.time < rafagaHasta)
            {
                proximoTiroMg = Time.time + 0.1f;
                var pool = ProjectilePool.Activo;
                if (pool == null) return;
                var dir = (objetivo - origen).normalized;
                dir = (dir + Random.insideUnitSphere * 0.05f).normalized;
                pool.Spawn(origen, dir, -3, TeamId.Enemy, DanoDeMetralla, new Color(1f, 0.35f, 0.2f));
                MuzzleLightPool.Flash(origen, new Color(1f, 0.6f, 0.3f), 4f, 7f);
                BalasDeMetralla++;
                if ((BalasDeMetralla & 1) == 0) AudioDirector.PlayClipAt(GenericSfx.GetWeaponShot(WeaponKind.Heavy), origen, 0.6f, 0.75f, PerfilEspacial.Disparo);
            }
        }

        // ---- Caida ----
        void EmpezarCaida()
        {
            Situacion = Estado.Cayendo;
            tCaida = 0f;
            giroCaida = Random.value < 0.5f ? -1f : 1f;
            velocidadCaida = transform.forward * Mathf.Max(8f, VelocidadActual * 0.6f);
            avisando = false;
            AlDerribarse?.Invoke();
            SesionLog.Evento("HALCON: derribado");
            AvisoCentral.Mostrar("¡HALCON DERRIBADO!", 2f, new Color(0.08f, 0.3f, 0.2f, 0.92f));
        }

        void TickCaida(float dt)
        {
            tCaida += dt;
            velocidadCaida += Vector3.down * 9f * dt;
            transform.position += velocidadCaida * dt;
            transform.Rotate(0f, giroCaida * 420f * dt, 0f, Space.World);
            transform.Rotate(0f, 0f, giroCaida * 90f * dt, Space.Self);
            if (Time.time >= proxChispa)
            {
                proxChispa = Time.time + 0.08f;
                SpriteFx.Lanzar("smoke_04", transform.position, new Color(0.1f, 0.1f, 0.1f, 0.9f), 1.0f, 3.4f, 1.6f, Random.Range(-25f, 25f), Vector3.up * 1.5f, 0.3f, 2);
                if (Random.value < 0.4f) SpriteFx.Lanzar("fire_02", transform.position, new Color(1f, 0.5f, 0.15f, 0.9f), 0.6f, 1.4f, 0.5f, Random.Range(-50f, 50f), Vector3.up, 0.4f, 3);
            }
            if (transform.position.y <= 1.2f || tCaida > 3.2f) Estallar();
        }
        float proxChispa;

        void Estallar()
        {
            if (Situacion == Estado.Muerto) return;
            Situacion = Estado.Muerto;
            var p = transform.position; p.y = Mathf.Max(p.y, 1.2f);
            ImpactFx.SpawnExplosion(p, 6f, false);
            AudioDirector.PlayAt(SfxKind.Explosion, p, 1f, 0.9f, PerfilEspacial.Explosion);
            foreach (var r in GetComponentsInChildren<Renderer>(true)) if (r != null) r.enabled = false;
            var col = GetComponent<Collider>(); if (col != null) col.enabled = false;
            Destroy(gameObject, 2.5f);
        }

        // Para el guion de la cinematica final: revienta ya, en el aire, sin pasar por la caida en espiral.
        public void ExplotarEnElAire()
        {
            if (Situacion == Estado.Muerto) return;
            if (Situacion != Estado.Cayendo) { Situacion = Estado.Cayendo; AlDerribarse?.Invoke(); }
            if (Vehiculo != null && !Vehiculo.IsDestroyed) Vehiculo.DestruirPorGuion();
            Estallar();
        }
    }
}
