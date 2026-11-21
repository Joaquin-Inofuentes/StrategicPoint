using System;
using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;

namespace SP.Operacion
{
    // R17 (pedidos 2 y 15: "IA de enemigos que vengan a atacar despues de atacar" / "ataque de enemigos cuando reciben dano").
    //
    // Antes: AiBrain.OnAnyDamage solo reaccionaba si la victima estaba ociosa o patrullando, un headshot no avisaba a nadie (el muerto no "recibe" dano
    // visible), un francotirador a mas de 48 m no lo oia nadie y un enemigo en cobertura/Quieto se quedaba atado a su puesto.
    //
    // Ahora, solo en la Operacion (lo activa OperacionDirector; el tutorial y la mision no la usan):
    //  - REGLA V (victima): un enemigo herido por el bando jugador responde SIEMPRE: fija al atacante como blanco, sale de Quieto/atrincherado (salvo los de
    //    puesto fijo por rol: francotirador, emboscada de cohetes, pesado, que solo cambian de blanco) y pasa a cazador. Si el atacante va montado en un
    //    vehiculo (el tanque), lo atacan con la orden de vehiculo que ya existe.
    //  - REGLA T (testigos): cuando un enemigo cae, los enemigos vivos a <= RadioDeTestigos con linea de vista al caido salen a cazar al tirador.
    //    El que no ve nada no reacciona: el sigilo sigue siendo posible.
    //  - Reaccion escalonada (0,25-0,6 s segun el id) y tope de ReaccionesPorSegundo, para que se lea humano y no cueste con 100+ enemigos.
    // Todo corre con dt (Tick): la suite headless lo simula.
    public static class ReaccionAlDano
    {
        public const float RadioDeTestigos = 20f, RadioDeGrupo = 16f, DeduplicarSegundos = 1.0f;
        public const int ReaccionesPorSegundo = 16, LineasPorBaja = 6;

        struct Pendiente { public int victimaId, atacanteId; public float falta; public bool testigo; public Vector3 puntoTirador; }

        static readonly List<Pendiente> cola = new List<Pendiente>();
        static readonly Dictionary<int, float> ultimaPorVictima = new Dictionary<int, float>();
        static readonly List<int> borrar = new List<int>();
        static IDisposable subDano, subMuerte;
        static float reloj, ventana; static int aplicadasEnVentana;

        public static bool Activa { get; private set; }
        public static int Aplicadas { get; private set; }
        public static int Testigos { get; private set; }
        public static int Descartadas { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ReiniciarAlArrancar() { Desactivar(); Aplicadas = Testigos = Descartadas = 0; }

        public static void Activar()
        {
            Desactivar();
            Activa = true;
            subDano = EventBus.Instance.Subscribe<DamageTakenEvent>(AlRecibirDano);
            subMuerte = EventBus.Instance.Subscribe<EntityDiedEvent>(AlMorir);
        }

        public static void Desactivar()
        {
            Activa = false;
            subDano?.Dispose(); subDano = null;
            subMuerte?.Dispose(); subMuerte = null;
            cola.Clear(); ultimaPorVictima.Clear(); reloj = 0f; ventana = 0f; aplicadasEnVentana = 0;
        }

        static float Retraso(int id) => 0.25f + (Mathf.Abs(id) % 8) * 0.05f;

        static void AlRecibirDano(DamageTakenEvent e)
        {
            var victima = ActorRegistry.FindById(e.TargetId);
            if (victima == null || victima.Team != TeamId.Enemy) return;
            var atacante = ActorRegistry.FindById(e.AttackerId);
            if (atacante == null || atacante.Team != TeamId.Player) return;
            // la escopeta genera varios eventos por tiro: uno por victima cada DeduplicarSegundos
            if (ultimaPorVictima.TryGetValue(victima.Id, out float t) && reloj - t < DeduplicarSegundos) return;
            ultimaPorVictima[victima.Id] = reloj;
            cola.Add(new Pendiente { victimaId = victima.Id, atacanteId = atacante.Id, falta = Retraso(victima.Id), testigo = false });
        }

        static void AlMorir(EntityDiedEvent e)
        {
            var caido = ActorRegistry.FindById(e.ActorId);
            if (caido == null || caido.Team != TeamId.Enemy || caido.Health == null) return;
            var atacante = ActorRegistry.FindById(caido.Health.LastAttackerId);
            if (atacante == null || atacante.Team != TeamId.Player) return;
            var puntoCaido = caido.transform.position + Vector3.up * 1.2f;
            var todos = ActorRegistry.All;
            int lineas = 0;
            for (int i = 0; i < todos.Count && lineas < LineasPorBaja; i++)
            {
                var o = todos[i];
                if (o == null || o == caido || o.Team != TeamId.Enemy || o.Health == null || !o.Health.IsAlive || !o.gameObject.activeInHierarchy) continue;
                if (o.Brain == null || o.Brain.Pasivo || o.Brain.MontadoEnVehiculo) continue;
                if ((o.transform.position - caido.transform.position).sqrMagnitude > RadioDeTestigos * RadioDeTestigos) continue;
                lineas++;
                var ojos = o.transform.position + Vector3.up * 1.5f;
                if (Physics.Linecast(ojos, puntoCaido, out var golpe, ~0, QueryTriggerInteraction.Ignore) && golpe.collider != null
                    && golpe.collider.GetComponentInParent<Soldier>() != caido) continue;   // algo se interpone: no lo vio caer
                cola.Add(new Pendiente { victimaId = o.Id, atacanteId = atacante.Id, falta = Retraso(o.Id) + 0.2f, testigo = true, puntoTirador = atacante.transform.position });
            }
        }

        public static void Tick(float dt)
        {
            if (!Activa) return;
            reloj += dt;
            ventana -= dt;
            if (ventana <= 0f) { ventana = 1f; aplicadasEnVentana = 0; }
            for (int i = 0; i < cola.Count; i++)
            {
                var p = cola[i]; p.falta -= dt; cola[i] = p;
            }
            for (int i = cola.Count - 1; i >= 0; i--)
            {
                var p = cola[i];
                if (p.falta > 0f) continue;
                if (aplicadasEnVentana >= ReaccionesPorSegundo) { if (p.falta < -2f) { cola.RemoveAt(i); Descartadas++; } continue; }
                cola.RemoveAt(i);
                aplicadasEnVentana++;
                Aplicar(p);
            }
        }

        static void Aplicar(Pendiente p)
        {
            var v = ActorRegistry.FindById(p.victimaId);
            var a = ActorRegistry.FindById(p.atacanteId);
            if (v == null || v.Health == null || !v.Health.IsAlive || v.Brain == null || !v.gameObject.activeInHierarchy) return;
            var b = v.Brain;
            if (b.Pasivo || a == null || a.Health == null || !a.Health.IsAlive) return;
            Aplicadas++;
            if (p.testigo) Testigos++;

            if (b.PuestoFijoPorRol)
            {
                if (!p.testigo) b.ReaccionarAGolpe(a);   // solo el herido le cambia el blanco a su puesto
                return;
            }

            if (!p.testigo || b.CurrentTarget == null)
            {
                b.Quieto = false;
                if (!p.testigo) b.Atrincherado = false;   // solo se suelta el atrincherado que RECIBIO dano (WP4 #085), no el que lo escucho
            }
            b.SegundosSinVerParaSoltar = 9f;
            CazaDeEnemigos.Marcar(new[] { v });

            if (a.Brain != null && a.Brain.MontadoEnVehiculo && !p.testigo)
            {
                var veh = a.GetComponentInParent<SP.Vehicles.Vehicle>();
                if (veh != null) { b.IssueAttackVehicleOrder(veh); return; }
            }
            if (p.testigo)
            {
                // sale hacia el tirador con algo de ruido para que no lleguen en fila
                var ruido = new Vector3(Mathf.Sin(v.Id * 2.1f), 0f, Mathf.Cos(v.Id * 2.1f)) * 3f;
                b.SetPatrolRoute(new[] { p.puntoTirador + ruido });
            }
            else b.ReaccionarAGolpe(a);

            // un herido avisa a su grupo (los guardias quietos ya lo hacian solo por RadioDeGrupo; ahora tambien los que peleaban sin blanco)
            if (!p.testigo)
            {
                var todos = ActorRegistry.All;
                for (int i = 0; i < todos.Count; i++)
                {
                    var o = todos[i];
                    if (o == null || o == v || o.Team != TeamId.Enemy || o.Health == null || !o.Health.IsAlive || !o.gameObject.activeInHierarchy || o.Brain == null) continue;
                    if (o.Brain.Pasivo || o.Brain.PuestoFijoPorRol || o.Brain.MontadoEnVehiculo || o.Brain.Atrincherado) continue;
                    var d = o.transform.position - v.transform.position; d.y = 0f;
                    if (d.sqrMagnitude <= RadioDeGrupo * RadioDeGrupo) CazaDeEnemigos.Marcar(new[] { o });
                }
            }
        }
    }
}
