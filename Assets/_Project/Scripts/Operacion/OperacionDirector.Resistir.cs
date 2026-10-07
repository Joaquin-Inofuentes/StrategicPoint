using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Mision;
using SP.Player;
using SP.Presentation;
using SP.Tutorial;
using SP.UI;
using SP.Vehicles;

namespace SP.Operacion
{
    // Fase 5 (RESISTIR): coberturas de la plaza, refuerzos y oleadas de la ciudad, llegada del helicoptero.
    public partial class OperacionDirector
    {
        // ---- Bug #061: coberturas en la plaza ----
        // "Aqui deberian haber coberturas para cubrirse y revisa que los aliados y enemigos usen coberturas". La plaza era un
        // cuadrado abierto. Se arma (al cargar, no en plena pelea) un anillo de barricadas alrededor del helipuerto mirando
        // hacia afuera, para la escuadra, y otras en las calles de entrada mirando hacia la plaza, para los que llegan. La IA de
        // los dos bandos ya elige coberturas sola (CoberturasPuntuadas): lo que faltaba eran coberturas que elegir.
        public const float RadioAnilloDeLaPlaza = 12.5f, LibreAlrededorDelHeli = 8.5f;
        public int CoberturasDeLaPlaza { get; private set; }

        void ArmarCoberturasDeLaPlaza()
        {
            if (plaza == null || !Application.isPlaying) return;
            var c = new Vector3(plaza.position.x, 0f, plaza.position.z);
            var heliPos = heliAterrizaje != null ? new Vector3(heliAterrizaje.position.x, 0f, heliAterrizaje.position.z) : c;
            var lugares = new List<(Vector3, Vector3)>();
            for (int i = 0; i < 10; i++)
            {
                float a = (i * 36f + 18f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var p = c + dir * RadioAnilloDeLaPlaza;
                if (Plano(p, heliPos).magnitude < LibreAlrededorDelHeli) continue;
                lugares.Add((p, dir));
            }
            // Calles de entrada: a mitad de camino entre cada entrada y la plaza, corridas de costado (no tapan la calle entera).
            var vistas = new List<Vector3>();
            foreach (var e in entradasDeLaCiudad)
            {
                var ep = new Vector3(e.x, 0f, e.z);
                if (vistas.Exists(v => Plano(v, ep).magnitude < 10f)) continue;
                vistas.Add(ep);
                var haciaPlaza = (c - ep); haciaPlaza.y = 0f;
                float d = haciaPlaza.magnitude; if (d < 20f) continue;
                haciaPlaza /= d;
                var lado = Vector3.Cross(Vector3.up, haciaPlaza);
                lugares.Add((ep + haciaPlaza * (d * 0.45f) + lado * 3.2f, haciaPlaza));
                lugares.Add((ep + haciaPlaza * (d * 0.62f) - lado * 3.2f, haciaPlaza));
            }
            // Medido despues del #061: los enemigos pelean desde las 4 calles de la cruz, a 20-40 m de la plaza, y ahi no habia
            // nada detras de que cubrirse (solo paredes paralelas a la linea de tiro). Barricadas a media distancia, alternando
            // de vereda; cada una mira hacia la plaza.
            var ejes = new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            foreach (var eje in ejes)
            {
                var lado = Vector3.Cross(Vector3.up, eje);
                int k = 0;
                foreach (float dist in new[] { 21f, 31f, 42f })
                    lugares.Add((c + eje * dist + lado * ((k++ & 1) == 0 ? 3.6f : -3.6f), -eje));
            }
            CoberturasDeLaPlaza = Atrincherar.ArmarCoberturas(lugares);
        }

        // ---- Bug #049 / #050: refuerzos SECUENCIALES en la ciudad ----
        // "No me aparecen los enemigos reales, deberia haber mas y aparecer de manera secuencial" / "mas frenetismo, mas
        // enemigos". Las 3 oleadas de reserva siguen (con su aviso), y ademas entra UN enemigo cada ~2 s por alguna de las
        // entradas de la ciudad (los puntos donde esperaban los reservistas), hasta un tope de vivos para no saturar.
        [Header("Refuerzos secuenciales de la ciudad")]
        public float segundosEntreRefuerzos = 2.1f;
        public int refuerzosVivosMaximo = 16;
        public int refuerzosTotalesMaximo = 26;
        readonly List<Vector3> entradasDeLaCiudad = new List<Vector3>();
        GameObject plantillaDeRefuerzo;
        float proximoRefuerzo;
        int indiceEntrada;
        readonly List<Soldier> refuerzosCiudad = new List<Soldier>();
        public int RefuerzosCreados { get; private set; }
        public int RefuerzosVivos { get { int n = 0; foreach (var r in refuerzosCiudad) if (r != null && r.Health != null && r.Health.IsAlive) n++; return n; } }

        void PrepararRefuerzosDeLaCiudad()
        {
            entradasDeLaCiudad.Clear();
            if (oleadasDeLaCiudad == null) return;
            foreach (var o in oleadasDeLaCiudad)
            {
                if (o == null || o.soldados == null) continue;
                foreach (var r in o.soldados)
                {
                    if (r == null) continue;
                    if (plantillaDeRefuerzo == null && !r.gameObject.activeSelf)
                    {
                        // Copia intacta tomada al arrancar (inactiva: no corre Awake ni se registra). Clonar al reservista en
                        // plena fase copiaria su estado de ese momento (muerto, caido, a mitad de animacion).
                        plantillaDeRefuerzo = Instantiate(r.gameObject, r.transform.parent);
                        plantillaDeRefuerzo.name = "PlantillaDeRefuerzo_Ciudad";
                    }
                    entradasDeLaCiudad.Add(r.transform.position);
                }
            }
        }

        void TickRefuerzosDeLaCiudad()
        {
            if (plantillaDeRefuerzo == null || entradasDeLaCiudad.Count == 0 || plaza == null) return;
            // Ni en los primeros segundos (todavia se esta llegando) ni en los ultimos (el helicoptero ya baja).
            if (Reloj < 3f || ResistenciaRestante < 5f || ciudadPausada) return;
            if (Reloj < proximoRefuerzo || RefuerzosCreados >= refuerzosTotalesMaximo || RefuerzosVivos >= refuerzosVivosMaximo) return;
            proximoRefuerzo = Reloj + segundosEntreRefuerzos * UnityEngine.Random.Range(0.75f, 1.25f);

            // Entradas en orden rotado (salteando una al azar a veces) para que lleguen de todos lados, no siempre del mismo.
            // Bug #063: tambien por las calles nuevas del anillo (no solo por los 3 extremos de la cruz).
            var aparicion = AparicionesDeLaCiudad();
            indiceEntrada = (indiceEntrada + 1 + UnityEngine.Random.Range(0, 3)) % aparicion.Count;
            var pos = aparicion[indiceEntrada] + new Vector3(UnityEngine.Random.Range(-2f, 2f), 0f, UnityEngine.Random.Range(-2f, 2f));
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var h, 4f, UnityEngine.AI.NavMesh.AllAreas)) pos = h.position;

            var go = Instantiate(plantillaDeRefuerzo, pos, Quaternion.LookRotation(Plano(plaza.position, pos).normalized, Vector3.up), plantillaDeRefuerzo.transform.parent);
            go.name = "Refuerzo_Ciudad_" + (++RefuerzosCreados);
            go.SetActive(true);
            var s = go.GetComponent<Soldier>();
            if (s == null) { Destroy(go); return; }
            refuerzosCiudad.Add(s);
            ApoyoEnElPiso.Apoyar(s.transform);
            if (s.Brain != null)
            {
                s.Brain.ReactivarNavegacion();
                var meta = plaza.position + new Vector3(UnityEngine.Random.Range(-6f, 6f), 0f, UnityEngine.Random.Range(-6f, 6f));
                s.Brain.SetPatrolRoute(new[] { meta, plaza.position });
            }
            CazaDeEnemigos.Marcar(new[] { s });
        }

        // ---- Bug #063: oleadas de la ciudad escalonadas y dispersas ----
        // "Que tenga mas logica que vengan ... que no sean de grupos asi sino mas dispersos y con temporizadores". Cada oleada
        // salia entera de golpe (5-6 soldados amontonados en el mismo punto). Ahora, cuando le toca a una oleada, sus soldados
        // entran DE A UNO cada 1-2,5 s, cada uno por una boca distinta de las calles de ese lado de la ciudad, y van hacia un
        // punto distinto alrededor de la plaza. El HUD muestra cuanto falta para la proxima oleada.
        // Bocas de entrada relativas a la plaza: el anillo de calles nuevo (norte, sur, este, oeste) que arma AmpliarCiudad.
        static readonly Vector2[] BocasDeLaCiudad =
        {
            new Vector2(-52f, 68f), new Vector2(-22f, 68f), new Vector2(23f, 68f), new Vector2(50f, 68f),
            new Vector2(-52f, -66f), new Vector2(-22f, -66f), new Vector2(23f, -66f), new Vector2(50f, -66f),
            new Vector2(58f, -48f), new Vector2(58f, -23f), new Vector2(58f, 22f), new Vector2(58f, 47f),
            new Vector2(-56f, -48f), new Vector2(-56f, -28f), new Vector2(-56f, 27f), new Vector2(-56f, 52f),
        };
        readonly List<(Soldier s, float t)> pendientesCiudad = new List<(Soldier, float)>();
        // WP11 (#063): la boca de calle por la que entra cada soldado de la oleada, repartida SIN repetir mientras alcancen las 4 mas cercanas (antes cada
        // uno sorteaba la suya: con 5 soldados y 4 bocas, 2 de cada 11 oleadas entraban todos por solo 2 y el reparto "disperso" fallaba por azar).
        readonly Dictionary<Soldier, Vector3> bocaDeLaOla = new Dictionary<Soldier, Vector3>();
        List<Vector3> aparicionesCiudad;
        public int PendientesDeLaCiudad => pendientesCiudad.Count;
        public int LiberadosEscalonados { get; private set; }

        // Segundos hasta la proxima oleada de la ciudad (-1 si ya salieron todas).
        public float ProximaOleadaDeLaCiudadEn
        {
            get
            {
                float mejor = -1f;
                if (oleadasDeLaCiudad == null) return mejor;
                for (int i = 0; i < oleadasDeLaCiudad.Length; i++)
                {
                    if (lanzadasCiudad.Contains(i)) continue;
                    float f = oleadasDeLaCiudad[i].segundo - Reloj;
                    if (mejor < 0f || f < mejor) mejor = Mathf.Max(0f, f);
                }
                return mejor;
            }
        }

        List<Vector3> bocasCiudad;

        // Las bocas del anillo de calles, sobre el NavMesh.
        List<Vector3> BocasEnElNavMesh()
        {
            if (bocasCiudad != null) return bocasCiudad;
            bocasCiudad = new List<Vector3>();
            if (plaza != null)
                foreach (var b in BocasDeLaCiudad)
                {
                    var p = plaza.position + new Vector3(b.x, 0f, b.y);
                    if (UnityEngine.AI.NavMesh.SamplePosition(p, out var h, 5f, UnityEngine.AI.NavMesh.AllAreas)) bocasCiudad.Add(h.position);
                }
            return bocasCiudad;
        }

        // Refuerzos sueltos: por las entradas de siempre y por las bocas nuevas.
        List<Vector3> AparicionesDeLaCiudad()
        {
            if (aparicionesCiudad != null) return aparicionesCiudad;
            aparicionesCiudad = new List<Vector3>(entradasDeLaCiudad);
            aparicionesCiudad.AddRange(BocasEnElNavMesh());
            return aparicionesCiudad;
        }

        void ActivarEscalonada(OleadaDeReserva o)
        {
            if (o == null || o.soldados == null) return;
            if (hud != null && !string.IsNullOrEmpty(o.aviso)) hud.Aviso(o.aviso, 2.5f);
            else if (!string.IsNullOrEmpty(o.aviso)) AlertQueue.Push(o.aviso, AlertPriority.Alta, 3f);
            float t = Reloj;
            var cercanas = new List<Vector3>(BocasEnElNavMesh());
            Soldier primero = null; foreach (var s0 in o.soldados) if (s0 != null) { primero = s0; break; }
            if (primero != null)
            {
                var o0 = primero.transform.position;
                cercanas.Sort((a, b) => Plano(a, o0).sqrMagnitude.CompareTo(Plano(b, o0).sqrMagnitude));
                if (cercanas.Count > 4) cercanas.RemoveRange(4, cercanas.Count - 4);
                for (int i = cercanas.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (cercanas[i], cercanas[j]) = (cercanas[j], cercanas[i]); }
            }
            int turno = 0;
            foreach (var s in o.soldados)
            {
                if (s == null) continue;
                if (cercanas.Count > 0) bocaDeLaOla[s] = cercanas[turno++ % cercanas.Count];
                pendientesCiudad.Add((s, t));
                t += UnityEngine.Random.Range(1f, 2.5f);
            }
        }

        void TickPendientesDeLaCiudad()
        {
            for (int i = pendientesCiudad.Count - 1; i >= 0; i--)
            {
                var (s, t) = pendientesCiudad[i];
                if (Reloj < t) continue;
                pendientesCiudad.RemoveAt(i);
                if (s != null) LiberarEnLaCiudad(s);
            }
        }

        void LiberarEnLaCiudad(Soldier s)
        {
            // Por una de las 4 bocas mas cercanas a donde esperaba (respeta "por la calle este", etc.), al azar.
            var origen = s.transform.position;
            Vector3 pos;
            if (bocaDeLaOla.TryGetValue(s, out var asignada)) { pos = asignada; bocaDeLaOla.Remove(s); }
            else
            {
                var bocas = new List<Vector3>(BocasEnElNavMesh());
                bocas.Sort((a, b) => Plano(a, origen).sqrMagnitude.CompareTo(Plano(b, origen).sqrMagnitude));
                pos = bocas.Count > 0 ? bocas[UnityEngine.Random.Range(0, Mathf.Min(4, bocas.Count))] : origen;
            }
            pos += new Vector3(UnityEngine.Random.Range(-2.5f, 2.5f), 0f, UnityEngine.Random.Range(-2.5f, 2.5f));
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var h, 4f, UnityEngine.AI.NavMesh.AllAreas)) pos = h.position;
            s.transform.position = new Vector3(pos.x, origen.y, pos.z);
            if (plaza != null) s.transform.rotation = Quaternion.LookRotation(Plano(plaza.position, pos).normalized, Vector3.up);
            s.gameObject.SetActive(true);
            ApoyoEnElPiso.Apoyar(s.transform);
            CazaDeEnemigos.Marcar(new[] { s });
            if (s.Brain != null && plaza != null)
            {
                s.Brain.ReactivarNavegacion();
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f), r = UnityEngine.Random.Range(14f, 24f);
                var meta = plaza.position + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                s.Brain.SetPatrolRoute(new[] { meta, plaza.position });
            }
            LiberadosEscalonados++;
        }

        // ---- 5. ciudad ----
        void ArrancarHeli()
        {
            if (heli == null) return;
            heli.gameObject.SetActive(true);
            VolarHeli(0f);
            if (ScriptHeli == null) ScriptHeli = heli.GetComponent<Helicoptero>();
            if (ScriptHeli != null) { ScriptHeli.Volando = true; ScriptHeli.Alerta(true); ScriptHeli.DisparaCobertura = true; ScriptHeli.RestaurarCobertura(); }
            HeliAterrizo = false;
            lanzadasCiudad.Clear();
            pendientesCiudad.Clear();
            bocaDeLaOla.Clear();
            entroALaCiudad = false;
            // Bug #048: flecha con la distancia al helicoptero desde que sale hasta que la escuadra sube.
            // P7 (#114): en Resistir con mando tactico NO se muestra (el heli esta fuera del mapa, "2138 m" confundia); aparece en Extraer.
            if (!MandoActivo) MostrarFlechaDelHeli();
        }

        SP.UI.FlechaDeObjetivo flechaHeli;
        public SP.UI.FlechaDeObjetivo FlechaHeli => flechaHeli;
        // #114: el marcador HELICOPTERO apunta al helicoptero si esta cerca; si esta fuera del mapa (> 600 m) apunta a la zona de aterrizaje.
        public const float DistanciaMaximaDelMarcadorDelHeli = 600f;
        void MostrarFlechaDelHeli()
        {
            if (flechaHeli != null || heli == null) return;
            var yo = Poseido();
            Transform objetivo = heli;
            if (heliAterrizaje != null && yo != null && Vector3.Distance(yo.transform.position, heli.position) > DistanciaMaximaDelMarcadorDelHeli) objetivo = heliAterrizaje;
            flechaHeli = SP.UI.FlechaDeObjetivo.Crear(objetivo, "HELICOPTERO", new Color(0.35f, 1f, 0.5f, 0.95f));
        }

        void QuitarFlechaHeli() { if (flechaHeli != null) { flechaHeli.Quitar(); flechaHeli = null; } }

        readonly HashSet<int> lanzadasCiudad = new HashSet<int>();
        bool ciudadPausada, entroALaCiudad;

        void TickResistir(float dt)
        {
            if (MandoActivo)
            {
                // WP10 (#101): el reloj del helicoptero es el "reloj efectivo" del mando (OperacionDirector.Mando.cs).
                if (Subfase == 0) TickLlegadaAlMando(dt); else TickMando(dt);
                return;
            }
            var yo = Poseido();
            float dCiudad = yo != null ? Plano(yo.transform.position, plaza.position).magnitude : 0f;
            bool dentro = dCiudad <= radioDeLaCiudad;
            // El reloj de la fase solo corre mientras se resiste dentro de la ciudad.
            if (!dentro) { Reloj -= dt; ciudadPausada = true; } else { ciudadPausada = false; entroALaCiudad = true; }
            // Si el tanque frena antes del pueblo, todavia no se "salio" de la ciudad: se pide ENTRAR, no VOLVER.
            string pausa = entroALaCiudad ? "VOLVE A LA CIUDAD" : "ENTRA A LA CIUDAD";
            Reloj = Mathf.Max(0f, Reloj);

            for (int i = 0; i < oleadasDeLaCiudad.Length; i++)
            {
                if (lanzadasCiudad.Contains(i) || Reloj < oleadasDeLaCiudad[i].segundo) continue;
                lanzadasCiudad.Add(i);
                ActivarEscalonada(oleadasDeLaCiudad[i]);
            }
            TickPendientesDeLaCiudad();

            TickRefuerzosDeLaCiudad();
            VolarHeli(Mathf.Clamp01(Reloj / segundosDeResistencia));

            int seg = Mathf.CeilToInt(ResistenciaRestante);
            if (hud != null)
            {
                float proxima = ProximaOleadaDeLaCiudadEn;
                string oleadas = proxima >= 0f ? $"proxima oleada en {Mathf.CeilToInt(proxima)} s" : $"oleadas {lanzadasCiudad.Count}/{oleadasDeLaCiudad.Length}";
                hud.Objetivo(TituloObjetivo(5, "RESISTIR EN LA CIUDAD"), ciudadPausada ? (entroALaCiudad ? "VOLVE A LA CIUDAD: el temporizador esta detenido" : "Camina hasta el pueblo: la resistencia empieza al entrar") : $"Resiste {seg} s · {oleadas} · el helicoptero esta llegando", Reloj / segundosDeResistencia, ciudadPausada ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.55f, 0.25f));
                hud.Timer(ciudadPausada ? pausa : "RESISTI", seg, seg <= 5 ? new Color(1f, 0.3f, 0.25f) : Color.white);
            }
            if (Reloj >= segundosDeResistencia)
            {
                heli.position = heliAterrizaje.position;
                if (ScriptHeli != null) ScriptHeli.Volando = false;
                HeliAterrizo = true;
                if (hud != null) hud.OcultarTimer();
                EntrarFase(FaseOperacion.Extraer);
            }
        }

        // Bug #050: "la llegada del helicoptero deberia ser mas realista, con curva de velocidad". Antes arrancaba QUIETO a
        // 170 m (aceleraba desde 0 con un SmoothStep) y cruzaba a ~5 m/s: un helicoptero de rescate llega volando.
        // Ahora viene de lejos (~1 km) a velocidad de crucero (40 m/s, 144 km/h) y a 55 m de altura, con la nariz baja;
        // a mitad de la aproximacion empieza a frenar con desaceleracion constante, levanta la nariz (flare) y baja de
        // altura, queda en estacionario sobre la plaza y desciende despacio a posarse justo al terminar la cuenta.
        public const float VelocidadCruceroHeli = 40f, AlturaCruceroHeli = 55f, AlturaEstacionarioHeli = 16f;
        const float FraccionAproximacion = 0.78f, FraccionCrucero = 0.55f;
        public float VelocidadHeli { get; private set; }

        void VolarHeli(float u)
        {
            float T = Mathf.Max(5f, segundosDeResistencia);
            float t = Mathf.Clamp01(u) * T;
            float T1 = T * FraccionAproximacion;          // hasta quedar en estacionario sobre la plaza
            float tc = T1 * FraccionCrucero, td = T1 - tc; // crucero + frenado
            float v = VelocidadCruceroHeli;
            float D = v * tc + v * td * 0.5f;
            var sobre = heliAterrizaje.position + Vector3.up * AlturaEstacionarioHeli;
            var fin = heliAterrizaje.position;
            var haciaInicio = Plano(heliInicio.position, sobre);
            if (haciaInicio.sqrMagnitude < 1f) haciaInicio = Vector3.forward;
            haciaInicio.Normalize();
            var yaw = Quaternion.LookRotation(-haciaInicio, Vector3.up).eulerAngles.y;

            Vector3 pos; float inclinacion;
            if (t < T1)
            {
                float recorrido, vel, frenado;
                if (t < tc) { recorrido = v * t; vel = v; frenado = 0f; }
                else { float tau = t - tc; recorrido = v * tc + v * tau - v * tau * tau / (2f * td); vel = v * (1f - tau / td); frenado = 1f - tau / td; }
                float falta = D - recorrido;
                var plano = new Vector3(sobre.x, 0f, sobre.z) + haciaInicio * falta;
                // Altura: crucero hasta que empieza a frenar; despues baja hacia la del estacionario.
                float bajada = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(tc * 0.85f, T1, t));
                pos = new Vector3(plano.x, Mathf.Lerp(heliAterrizaje.position.y + AlturaCruceroHeli, sobre.y, bajada), plano.z);
                // Nariz baja a velocidad de crucero; al frenar la levanta (flare) y vuelve a nivel en el estacionario.
                float avanceDelFrenado = 1f - frenado;   // 0 al empezar a frenar, 1 en el estacionario
                inclinacion = t < tc ? 9f
                    : Mathf.Lerp(9f, 0f, Mathf.SmoothStep(0f, 1f, avanceDelFrenado / 0.2f)) - 11f * Mathf.Sin(avanceDelFrenado * Mathf.PI);
                VelocidadHeli = vel;
            }
            else
            {
                float k = Mathf.Clamp01((t - T1) / Mathf.Max(0.01f, T - T1));
                // #127: con el mando (el juego real) el helicoptero se queda en estacionario sobre la zona cubriendo con fuego pesado y baja a
                // posarse cuando todos los aliados estan en la zona (TickCoberturaDeExtraer). Sin mando (flujo viejo, checks) baja como siempre.
                pos = MandoActivo && CoberturaEstacionaria ? sobre : Vector3.Lerp(sobre, fin, Mathf.SmoothStep(0f, 1f, k));
                inclinacion = 0f;
                VelocidadHeli = 0f;
            }
            heli.position = pos;
            heli.rotation = Quaternion.Euler(inclinacion, yaw, 0f);
        }
    }
}
