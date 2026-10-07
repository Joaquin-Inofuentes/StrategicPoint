using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Mision;
using SP.Player;
using SP.Presentation;
using SP.UI;

namespace SP.Operacion
{
    // Fase 6 (EXTRAER), #127: el helicoptero llega y se queda en ESTACIONARIO sobre la zona de aterrizaje, cubriendo con fuego pesado
    // (damage alto, radio de 60 m, gira para encarar a los atacantes) mientras llegan OLEADAS de enemigos desde lejos (a 70 m o mas de la
    // zona y fuera de la vista de la camara, cada 8-12 s, 3-4 soldados escalonados que cazan a los aliados y convergen a la zona). Baja a
    // posarse cuando TODOS los aliados vivos (escuadra + milicianos, #128) estan en la zona de aterrizaje; tope de 60 s para no trabar la
    // partida si un miliciano queda atascado. Con el helicoptero posado se sube con [E] (RutinaDeSubida, que ahora lleva a los 7).
    public partial class OperacionDirector
    {
        // Interruptores: CoberturaEstacionaria apaga todo el rediseno (el helicoptero baja a posarse como antes, solo para checks);
        // CoberturaEnSaltoDePrueba hace que OperacionPrueba.Arrancar(6) deje el helicoptero en estacionario con oleadas (Bug127/Bug128).
        public static bool CoberturaEstacionaria = true;
        public static bool CoberturaEnSaltoDePrueba;

        public const float RadioDeLaZonaDeAterrizaje = 16f;
        public const float SegundosMaximosDeCobertura = 60f;
        public const float PrimeraOlaDeExtraerEn = 3f, OlaMinima = 8f, OlaMaxima = 12f;
        public const float DistanciaMinimaDeOlaALaZona = 70f;
        public const float RadioDeFuegoDelHeli = 60f;
        public const int DanoDelHeliEnCobertura = 28;   // los enemigos tienen 180 de vida: 2-3 rafagas de ~10 balas
        public const int EnemigosVivosMaximosEnExtraer = 12, EnemigosTotalesMaximosEnExtraer = 40;

        public bool CoberturaActiva { get; private set; }
        public bool HeliBajando { get; private set; }
        public float SegundosDeCobertura { get; private set; }
        public int OleadasDeExtraer { get; private set; }
        public int EnemigosDeExtraer => enemigosExtraer.Count;
        public int BajasDelHeliEnExtraer { get; private set; }
        // Para los checks: de donde salio cada oleada y a cuanto de la zona de aterrizaje estaba.
        public readonly List<Vector3> OrigenesDeOlaDeExtraer = new List<Vector3>();
        public int AliadosEnLaZona { get; private set; }
        public int AliadosAEvacuarTotal { get; private set; }

        readonly List<Soldier> enemigosExtraer = new List<Soldier>();
        readonly HashSet<Soldier> bajasContadas = new HashSet<Soldier>();
        Vector3 heliEstacionario;
        float proximaOlaDeExtraer, proximaOrdenALaZona;
        Coroutine rutinaAterrizaje;

        public int EnemigosDeExtraerVivos
        {
            get { int n = 0; foreach (var e in enemigosExtraer) if (e != null && e.Health != null && e.Health.IsAlive) n++; return n; }
        }

        // El helicoptero queda sobre la zona (a AlturaEstacionarioHeli) con el rotor a fondo.
        void PonerHeliEnEstacionario()
        {
            if (heli == null || heliAterrizaje == null) return;
            heliEstacionario = heliAterrizaje.position + Vector3.up * AlturaEstacionarioHeli;
            heli.position = heliEstacionario;
            heli.rotation = Quaternion.Euler(0f, heli.eulerAngles.y, 0f);
            if (ScriptHeli == null) ScriptHeli = heli.GetComponent<Helicoptero>();
            if (ScriptHeli != null) ScriptHeli.Volando = true;
            HeliAterrizo = false;
        }

        void IniciarCoberturaDeExtraer()
        {
            if (heli == null || heliAterrizaje == null) return;
            if (rutinaAterrizaje != null) { StopCoroutine(rutinaAterrizaje); rutinaAterrizaje = null; }
            heliEstacionario = heliAterrizaje.position + Vector3.up * AlturaEstacionarioHeli;
            CoberturaActiva = true; HeliBajando = false;
            SegundosDeCobertura = 0f;
            proximaOlaDeExtraer = PrimeraOlaDeExtraerEn; proximaOrdenALaZona = 0f;
            OleadasDeExtraer = 0; BajasDelHeliEnExtraer = 0;
            enemigosExtraer.Clear(); bajasContadas.Clear(); OrigenesDeOlaDeExtraer.Clear();
            if (ScriptHeli != null)
            {
                ScriptHeli.Volando = true; ScriptHeli.Alerta(true); ScriptHeli.DisparaCobertura = true;
                ScriptHeli.DanoDeCobertura = DanoDelHeliEnCobertura;
                ScriptHeli.RadioDeBlancoActual = RadioDeFuegoDelHeli;
                ScriptHeli.DispersionDeCobertura = 0.012f; ScriptHeli.FactorDePausa = 0.4f;   // fuego pesado sostenido y mas preciso
            }
            MusicDirector.CombateForzado = true;   // "mas frenetismo": la musica de combate al maximo mientras dura la cobertura
            Aviso("¡EL HELI NOS CUBRE!\nLLEVEN A TODOS A LA ZONA DE ATERRIZAJE");
            // La escuadra de IA sigue al jugador (que es quien los lleva a la zona); los milicianos van solos.
            var yo = Poseido();
            if (yo != null) foreach (var s in Escuadra()) if (s != yo && s.Brain != null && !s.Brain.MontadoEnVehiculo) OrderService.IssueFollowOrder(s, yo, default, 0f, true);
            OrdenarALaZona();
        }

        // Los aliados sin orden que quedaron fuera de la zona (los milicianos en sus sectores, algun soldado de la escuadra trabado peleando) van a la zona de aterrizaje.
        void OrdenarALaZona()
        {
            if (heliAterrizaje == null) return;
            foreach (var m in AliadosAEvacuar(true))
            {
                if (m == null || m.Health == null || !m.Health.IsAlive || !m.gameObject.activeInHierarchy || m.Brain == null) continue;
                if (m.Brain.IsPossessedByPlayer || m.Brain.MontadoEnVehiculo) continue;
                if (Plano(m.transform.position, heliAterrizaje.position).magnitude <= RadioDeLaZonaDeAterrizaje * 0.7f) continue;
                if (m.Brain.TieneOrden) continue;
                var lado = new Vector3(((m.Id * 37) % 7 - 3) * 1.1f, 0f, ((m.Id * 53) % 5 - 2) * 1.1f);
                var destino = heliAterrizaje.position + lado;
                if (UnityEngine.AI.NavMesh.SamplePosition(destino, out var h, 4f, UnityEngine.AI.NavMesh.AllAreas)) destino = h.position;
                m.Brain.Quieto = false; m.Brain.Atrincherado = false; m.Brain.Pasivo = false;
                m.Brain.IssueMoveOrder(destino);
            }
        }

        // Cuantos de los aliados vivos (escuadra + milicianos) ya estan dentro del radio de la zona de aterrizaje.
        bool TodosEnLaZona()
        {
            var todos = AliadosAEvacuar(true);
            int en = 0;
            foreach (var a in todos) if (Plano(a.transform.position, heliAterrizaje.position).magnitude <= RadioDeLaZonaDeAterrizaje) en++;
            AliadosEnLaZona = en; AliadosAEvacuarTotal = todos.Count;
            return todos.Count > 0 && en >= todos.Count;
        }

        void TickCoberturaDeExtraer(float dt)
        {
            SegundosDeCobertura += dt;
            ContarBajasDelHeli();
            if (HeliBajando) { TickHudDeCobertura(); return; }

            // Estacionario: leve cabeceo vertical y giro lento hacia el enemigo mas cercano (las ametralladoras solo cubren +-70 grados de cada lado).
            if (heli != null)
            {
                var objetivo = EnemigoMasCercanoAlHeli(out float dist);
                if (objetivo != null && dist < RadioDeFuegoDelHeli)
                {
                    var haciaEl = objetivo.transform.position - heli.position; haciaEl.y = 0f;
                    if (haciaEl.sqrMagnitude > 4f)
                    {
                        float rumbo = Quaternion.LookRotation(haciaEl.normalized, Vector3.up).eulerAngles.y;
                        float actual = heli.eulerAngles.y;
                        // El enemigo tiene que quedar de costado: la proa a +-90 grados del rumbo (la mas cercana a la actual).
                        float a = rumbo + 90f, b = rumbo - 90f;
                        float meta = Mathf.Abs(Mathf.DeltaAngle(actual, a)) < Mathf.Abs(Mathf.DeltaAngle(actual, b)) ? a : b;
                        float yaw = Mathf.MoveTowardsAngle(actual, meta, 45f * dt);
                        heli.rotation = Quaternion.Euler(0f, yaw, 0f);
                    }
                }
                heli.position = heliEstacionario + Vector3.up * (Mathf.Sin(SegundosDeCobertura * 1.3f) * 0.35f);
            }

            // Oleadas lejanas.
            if (SegundosDeCobertura >= proximaOlaDeExtraer && enemigosExtraer.Count < EnemigosTotalesMaximosEnExtraer && EnemigosDeExtraerVivos < EnemigosVivosMaximosEnExtraer)
            {
                proximaOlaDeExtraer = SegundosDeCobertura + Random.Range(OlaMinima, OlaMaxima);
                LanzarOlaDeExtraer();
            }
            if (SegundosDeCobertura >= proximaOrdenALaZona) { proximaOrdenALaZona = SegundosDeCobertura + 3f; OrdenarALaZona(); }

            TickHudDeCobertura();
            bool todos = TodosEnLaZona();
            if (todos || SegundosDeCobertura >= SegundosMaximosDeCobertura)
            {
                if (!todos) GameLog.Line("[Operacion] Extraer: tope de espera de la cobertura, el helicoptero baja igual");
                IniciarAterrizajeDeExtraer();
            }
        }

        void TickHudDeCobertura()
        {
            if (hud == null) return;
            hud.Objetivo(TituloObjetivo(6, "CUBRIR LA EXTRACCION"),
                HeliBajando ? "El helicoptero baja a posarse · mantene la posicion" : $"Lleva a TODOS a la zona de aterrizaje · {AliadosEnLaZona}/{AliadosAEvacuarTotal} en la zona · el heli cubre desde arriba",
                Mathf.Clamp01(AliadosAEvacuarTotal > 0 ? AliadosEnLaZona / (float)AliadosAEvacuarTotal : 0f), new Color(0.35f, 1f, 0.5f));
        }

        Soldier EnemigoMasCercanoAlHeli(out float dist)
        {
            Soldier mejor = null; dist = float.MaxValue;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                var d = Plano(s.transform.position, heli.position).magnitude;
                if (d < dist) { dist = d; mejor = s; }
            }
            return mejor;
        }

        // Bajas del heli: enemigos de las oleadas que murieron por balas de las ametralladoras (atacante -1, igual que el helicoptero).
        void ContarBajasDelHeli()
        {
            foreach (var e in enemigosExtraer)
            {
                if (e == null || e.Health == null || e.Health.IsAlive || bajasContadas.Contains(e)) continue;
                bajasContadas.Add(e);
                if (e.Health.LastAttackerId == -1) BajasDelHeliEnExtraer++;
            }
        }

        // ---- oleadas ----
        static bool EnVistaDeLaCamara(Vector3 p)
        {
            var cam = CamaraPrincipal.Actual;
            if (cam == null) return false;
            var v = cam.WorldToViewportPoint(p + Vector3.up * 1.5f);
            return v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }

        // Punto de entrada de la oleada: una boca de las calles de la ciudad a 70 m o mas de la zona de aterrizaje y fuera de la vista.
        bool ElegirOrigenDeOla(out Vector3 origen)
        {
            origen = Vector3.zero;
            var bocas = BocasEnElNavMesh();
            var buenas = new List<Vector3>(); var lejos = new List<Vector3>();
            foreach (var b in bocas)
            {
                if (Plano(b, heliAterrizaje.position).magnitude < DistanciaMinimaDeOlaALaZona) continue;
                lejos.Add(b);
                if (!EnVistaDeLaCamara(b)) buenas.Add(b);
            }
            var lista = buenas.Count > 0 ? buenas : lejos;
            if (lista.Count == 0) return false;
            origen = lista[Random.Range(0, lista.Count)];
            return true;
        }

        void LanzarOlaDeExtraer()
        {
            if (plantillaDeRefuerzo == null || heliAterrizaje == null) return;
            if (!ElegirOrigenDeOla(out var origen)) return;
            OleadasDeExtraer++;
            OrigenesDeOlaDeExtraer.Add(origen);
            int cantidad = OleadasDeExtraer >= 3 ? 4 : 3;
            AlertQueue.Push(OleadasDeExtraer == 1 ? "¡VIENEN ENEMIGOS! EL HELI NOS CUBRE" : "¡OTRA OLEADA! EL HELI NOS CUBRE", AlertPriority.Alta, 2.6f);
            StartCoroutine(RutinaDeOlaDeExtraer(origen, cantidad, OleadasDeExtraer));
        }

        IEnumerator RutinaDeOlaDeExtraer(Vector3 origen, int cantidad, int numero)
        {
            for (int i = 0; i < cantidad; i++)
            {
                if (!CoberturaActiva || HeliBajando) yield break;
                var pos = origen + new Vector3(Random.Range(-2.5f, 2.5f), 0f, Random.Range(-2.5f, 2.5f));
                if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var h, 4f, UnityEngine.AI.NavMesh.AllAreas)) pos = h.position;
                var go = Instantiate(plantillaDeRefuerzo, pos, Quaternion.LookRotation(Plano(heliAterrizaje.position, pos).normalized, Vector3.up), plantillaDeRefuerzo.transform.parent);
                go.name = $"Extraer_Ola{numero}_{i + 1}";
                go.SetActive(true);
                var s = go.GetComponent<Soldier>();
                if (s == null) { Destroy(go); continue; }
                enemigosExtraer.Add(s);
                ApoyoEnElPiso.Apoyar(s.transform);
                if (s.Brain != null)
                {
                    s.Brain.ReactivarNavegacion();
                    var meta = heliAterrizaje.position + new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f));
                    s.Brain.SetPatrolRoute(new[] { meta, heliAterrizaje.position });
                }
                CazaDeEnemigos.Marcar(new[] { s });
                yield return new WaitForSeconds(Random.Range(0.4f, 0.9f));
            }
        }

        // ---- aterrizaje ----
        void IniciarAterrizajeDeExtraer()
        {
            if (HeliBajando) return;
            HeliBajando = true;
            Aviso("¡EL HELI ATERRIZA!\nTODOS EN LA ZONA · SUBE CON [E]");
            rutinaAterrizaje = StartCoroutine(RutinaDeAterrizaje());
        }

        IEnumerator RutinaDeAterrizaje()
        {
            var desde = heli.position;
            float t = 0f; const float dur = 4f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                heli.position = Vector3.Lerp(desde, heliAterrizaje.position, k);
                yield return null;
            }
            PosarElHeli();
        }

        void PosarElHeli()
        {
            rutinaAterrizaje = null;
            heli.position = heliAterrizaje.position;
            if (ScriptHeli != null) ScriptHeli.Volando = false;
            HeliAterrizo = true; HeliBajando = false; CoberturaActiva = false;
            MusicDirector.CombateForzado = false;
            Aviso("EL HELICOPTERO ATERRIZO\nSUBE CON [E]");
        }

        // Atajo para los checks (y para SubirAlHeli llamado en el aire): posa el helicoptero ya.
        void PosarElHeliYa()
        {
            if (rutinaAterrizaje != null) { StopCoroutine(rutinaAterrizaje); rutinaAterrizaje = null; }
            PosarElHeli();
        }

        void ApagarCoberturaDeExtraer()
        {
            CoberturaActiva = false; HeliBajando = false;
            MusicDirector.CombateForzado = false;
        }
    }
}
