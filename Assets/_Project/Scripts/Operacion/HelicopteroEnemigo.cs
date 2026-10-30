using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
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
        public const float SegundoDeEntrada = 60f, RadioDeOrbita = 45f, AlturaMinima = 18f, AlturaMaxima = 24f, VelocidadDeVuelo = 25f;
        public const float SegundosEntrePasadas = 8f, SegundosDeAvisoDePasada = 1.2f, SeparacionDeCohetes = 7f;
        public const int CohetesPorPasada = 3, DanoDeCohete = 80, DanoDeMetralla = 6;
        public const float RadioDeCohete = 4.5f, AlcanceDeMetralla = 70f;

        public enum Estado { Dormido, Entrando, Orbitando, Cinematica, Cayendo, Muerto, Desplegando }

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
        Transform ancla;   // #272: helicoptero de asalto del centro de datos: orbita este punto en vez de un tanque
        public bool DeAsalto => ancla != null;
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

        // #272: helicoptero de asalto contra el centro de datos. Entra desde lejos, orbita la computadora (el ancla) y ametralla a los soldados
        // propios; el Vehicle tiene vida reducida y se lo derriba a tiros desde lejos (torretas aliadas, fusiles, el tanque).
        public const int VidaDeAsalto = 320;
        public static HelicopteroEnemigo IniciarSobre(Transform plantilla, Transform punto, float anguloGrados, float distanciaDeEntrada = 150f)
        {
            if (plantilla == null || punto == null) return null;
            var go = Instantiate(plantilla.gameObject, plantilla.parent);
            go.name = "Halcon_Asalto_" + (++Creados);
            var dir = new Vector3(Mathf.Cos(anguloGrados * Mathf.Deg2Rad), 0f, Mathf.Sin(anguloGrados * Mathf.Deg2Rad));
            var pos = punto.position + dir * distanciaDeEntrada;
            pos.y = AlturaMaxima + 10f;
            go.transform.position = pos;
            go.SetActive(true);
            var h = go.GetComponent<HelicopteroEnemigo>();
            if (h == null) { Destroy(go); return null; }
            h.Armar(null, punto);
            if (h.Vehiculo != null && h.Vehiculo.Health != null) h.Vehiculo.Health.Initialize(-1, VidaDeAsalto);
            return h;
        }

        // ---- Bug #321: helicoptero de DESPLIEGUE ----
        // "Quiero ver helicopteros enemigos que se acercan para soltar enemigos en cuerdas y que luego vengan a atacarme, que sea frenetico."
        // Vuela hasta un punto de la ruta del tanque, queda suspendido a 10 m, cuelga dos cuerdas y baja a N soldados por ellas (uno cada 0,6 s);
        // al terminar recoge las cuerdas y pasa a lo de siempre: orbita al tanque y lo ametralla. Vida reducida (VidaDeAsalto): se lo derriba.
        public const float AlturaDeDespliegue = 10f, SegundosEntreSoldados = 0.6f, VelocidadDeDescenso = 7f;
        public bool EsDeDespliegue { get; private set; }
        public int SoldadosBajados { get; private set; }
        public int SoldadosPedidos { get; private set; }
        public bool SogasColgadas { get; private set; }
        public Vector3 PuntoDeDespliegue { get; private set; }
        GameObject plantillaSoldado; System.Action<Soldier> alBajar;
        float proximoSoldado, tFinDeDespliegue; LineRenderer[] sogas;
        readonly List<Soldier> bajando = new List<Soldier>();
        readonly List<Soldier> entregados = new List<Soldier>();
        public IReadOnlyList<Soldier> SoldadosDelDespliegue => entregados;

        public static HelicopteroEnemigo IniciarDespliegue(Transform plantillaHeli, Vehicle tanque, GameObject plantillaDeSoldado, Vector3 punto, int soldados, System.Action<Soldier> alBajar = null, float distanciaDeEntrada = 110f)
        {
            if (plantillaHeli == null || tanque == null || plantillaDeSoldado == null) return null;
            var go = Instantiate(plantillaHeli.gameObject, plantillaHeli.parent);
            go.name = "Halcon_Despliegue_" + (++Creados);
            var fwd = tanque.transform.forward; fwd.y = 0f; fwd.Normalize();
            var lado = new Vector3(fwd.z, 0f, -fwd.x);
            var pos = punto + lado * distanciaDeEntrada * 0.8f + fwd * distanciaDeEntrada * 0.5f;
            pos.y = AlturaMaxima + 8f;
            go.transform.position = pos;
            go.SetActive(true);
            var h = go.GetComponent<HelicopteroEnemigo>();
            if (h == null) { Destroy(go); return null; }
            h.EsDeDespliegue = true;
            h.Armar(tanque);
            if (h.Vehiculo != null && h.Vehiculo.Health != null) h.Vehiculo.Health.Initialize(-1, VidaDeAsalto);
            h.plantillaSoldado = plantillaDeSoldado; h.alBajar = alBajar; h.SoldadosPedidos = soldados; h.PuntoDeDespliegue = punto;
            h.Situacion = Estado.Desplegando;
            return h;
        }

        void TickDespliegue(float dt)
        {
            var meta = PuntoDeDespliegue + Vector3.up * AlturaDeDespliegue;
            var antes = transform.position;
            transform.position = Vector3.MoveTowards(transform.position, meta, VelocidadDeVuelo * 1.7f * dt);
            VelocidadActual = ((transform.position - antes) / Mathf.Max(0.0001f, dt)).magnitude;
            var mira = SogasColgadas && tanque != null ? tanque.transform.position - transform.position : meta - transform.position;
            mira.y = 0f;
            if (mira.sqrMagnitude > 0.1f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(mira.normalized, Vector3.up) * Quaternion.Euler(SogasColgadas ? 0f : 10f, 0f, 0f), 1f - Mathf.Exp(-3f * dt));
            if (!SogasColgadas && Vector3.Distance(transform.position, meta) < 2.5f)
            {
                SogasColgadas = true; proximoSoldado = Time.time + 0.4f; tFinDeDespliegue = 0f;
                CrearSogas();
                AvisoCentral.Mostrar("¡HELICOPTERO ENEMIGO SOLTANDO TROPAS EN CUERDAS!", 2f, new Color(0.55f, 0.07f, 0.05f, 0.9f));
                SesionLog.Evento("HALCON: despliegue sobre " + PuntoDeDespliegue);
            }
            if (!SogasColgadas) return;
            ActualizarSogas();
            if (SoldadosBajados < SoldadosPedidos && Time.time >= proximoSoldado)
            {
                proximoSoldado = Time.time + SegundosEntreSoldados;
                BajarSoldado();
            }
            for (int i = bajando.Count - 1; i >= 0; i--)
            {
                var s = bajando[i];
                if (s == null) { bajando.RemoveAt(i); continue; }
                var p = s.transform.position;
                float sueloY = PuntoDeDespliegue.y;
                p.y = Mathf.MoveTowards(p.y, sueloY, VelocidadDeDescenso * dt);
                s.transform.position = p;
                if (p.y <= sueloY + 0.02f) { bajando.RemoveAt(i); Aterrizar(s); }
            }
            if (SoldadosBajados >= SoldadosPedidos && bajando.Count == 0)
            {
                if (tFinDeDespliegue <= 0f) tFinDeDespliegue = Time.time;
                if (Time.time - tFinDeDespliegue > 1.2f) TerminarDespliegue();
            }
        }

        void BajarSoldado()
        {
            int i = SoldadosBajados++;
            var arriba = transform.position + transform.right * (i % 2 == 0 ? -1f : 1f) * 1.0f + Vector3.down * 1.1f;
            var haciaTanque = tanque != null ? tanque.transform.position - arriba : transform.forward; haciaTanque.y = 0f;
            var go = Instantiate(plantillaSoldado, arriba, Quaternion.LookRotation(haciaTanque.sqrMagnitude > 0.01f ? haciaTanque.normalized : Vector3.forward, Vector3.up), plantillaSoldado.transform.parent);
            go.name = $"Rapelero_{Creados}_{i + 1}";
            var s = go.GetComponent<Soldier>();
            if (s == null) { Destroy(go); return; }
            go.SetActive(true);
            if (s.Brain != null) s.Brain.Pasivo = true;
            s.transform.position = arriba;
            bajando.Add(s); entregados.Add(s);
            WorldUiDirector.Revelar(s, 6f);
        }

        void Aterrizar(Soldier s)
        {
            ApoyoEnElPiso.Apoyar(s.transform);
            if (s.Brain != null)
            {
                s.Brain.Pasivo = false;
                s.Brain.ReactivarNavegacion();
                if (tanque != null) s.Brain.SetPatrolRoute(new[] { tanque.transform.position, tanque.transform.position + tanque.transform.forward * 20f });
            }
            alBajar?.Invoke(s);
        }

        void CrearSogas()
        {
            sogas = new LineRenderer[2];
            for (int k = 0; k < 2; k++)
            {
                var go = new GameObject("Soga_" + k);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true; lr.positionCount = 2; lr.startWidth = lr.endWidth = 0.07f;
                lr.material = SafeMaterial.Create(new Color(0.12f, 0.1f, 0.07f));
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sogas[k] = lr;
            }
            ActualizarSogas();
        }

        void ActualizarSogas()
        {
            if (sogas == null) return;
            for (int k = 0; k < sogas.Length; k++)
            {
                if (sogas[k] == null) continue;
                var arriba = transform.position + transform.right * (k == 0 ? -1f : 1f) * 1.0f + Vector3.down * 0.9f;
                sogas[k].SetPosition(0, arriba);
                sogas[k].SetPosition(1, new Vector3(arriba.x, PuntoDeDespliegue.y + 0.05f, arriba.z));
            }
        }

        void QuitarSogas()
        {
            if (sogas != null) foreach (var l in sogas) if (l != null) Destroy(l.gameObject);
            sogas = null; SogasColgadas = false;
        }

        void TerminarDespliegue()
        {
            QuitarSogas();
            var d = transform.position - tanque.transform.position;
            anguloDeEntrada = Mathf.Atan2(d.z, d.x); angulo = anguloDeEntrada;
            Situacion = Estado.Entrando;
            proximaPasada = Time.time + 4f;
            SesionLog.Evento($"HALCON: despliegue terminado ({SoldadosBajados} soldados), pasa a atacar");
        }

        // Blanco de la metralleta: el tanque en la carrera; en el asalto, el soldado propio vivo mas cercano al helicoptero.
        Vector3 BlancoDeMetralla()
        {
            if (ancla == null) return tanque.transform.position + Vector3.up * 1.4f;
            if (Time.time >= proximoRetarget)
            {
                proximoRetarget = Time.time + 0.5f;
                blancoDeAsalto = null;
                float mejor = float.MaxValue;
                foreach (var s in ActorRegistry.All)
                {
                    if (s == null || s.Team != TeamId.Player || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                    float d = (s.transform.position - transform.position).sqrMagnitude;
                    if (d < mejor) { mejor = d; blancoDeAsalto = s.transform; }
                }
            }
            return blancoDeAsalto != null ? blancoDeAsalto.position + Vector3.up * 1.2f : ancla.position + Vector3.up * 1.4f;
        }
        float proximoRetarget; Transform blancoDeAsalto;

        void Armar(Vehicle t, Transform puntoFijo = null)
        {
            tanque = t; ancla = puntoFijo;
            motorTanque = t != null ? t.GetComponent<VehicleMotor>() : null;
            Vehiculo = GetComponent<Vehicle>();
            Vehiculo.MarcarBando(TeamId.Enemy);
            rotorPrincipal = transform.Find("RotorPrincipal");
            rotorCola = transform.Find("RotorCola");
            var d = transform.position - (ancla != null ? ancla.position : tanque.transform.position);
            anguloDeEntrada = Mathf.Atan2(d.z, d.x);
            angulo = anguloDeEntrada;
            Situacion = Estado.Entrando;
            proximaPasada = Time.time + 4f;
            if (ancla == null && !EsDeDespliegue) Instancia = this;   // los de asalto no son "el" Halcon de la carrera (barra de jefe, cinematica)
            SesionLog.Evento("HALCON: entra el helicoptero enemigo");
        }

        void OnDestroy() { if (Instancia == this) Instancia = null; QuitarSogas(); }

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
            if (ancla == null && (tanque == null || tanque.IsDestroyed && Situacion != Estado.Cinematica)) return;

            float dt = Time.deltaTime;
            if (Situacion == Estado.Desplegando && !CinematicaDeOperacion.IntroActiva) { TickDespliegue(dt); return; }
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

            var centro = ancla != null ? ancla.position : tanque.transform.position;
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
        // Bug #202 ("hay cohetes enemigos que no deberian estar, solo deberian estar las metralletas"): el helicoptero enemigo ya no suelta cohetes
        // (ni sus circulos de aviso); sigue con las rafagas de metralleta.
        public const bool CohetesActivos = false;
        void LanzarCohetes()
        {
            if (!CohetesActivos) return;
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
            if (avisando || (tanque == null && ancla == null)) return;
            var objetivo = BlancoDeMetralla();
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
