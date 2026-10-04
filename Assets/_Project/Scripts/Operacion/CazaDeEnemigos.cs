using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;

namespace SP.Operacion
{
    // Bugs #17, #20 y #21: "los enemigos no se mueven / no vienen a atacarme, estan inmoviles. Falta trigger y destino fijado".
    //
    // Antes: una oleada se activaba con una patrulla entre dos puntos alrededor de un destino FIJO (la computadora, la plaza) y
    // un guardia solo reaccionaba si VEIA a alguien. Si la escuadra estaba en otro lado los enemigos daban vueltas sin venir.
    //
    // Ahora hay dos disparadores y un destino que se actualiza:
    //  - DISPARADOR 1 (oleadas): todo soldado de una oleada activada es CAZADOR desde que aparece.
    //  - DISPARADOR 2 (guardias): un guardia quieto se vuelve cazador si un soldado de la escuadra entra a RadioDeAlerta
    //    (aunque no lo vea: lo oye) o si recibe dano; y avisa a los de su grupo a RadioDeGrupo.
    //  - DESTINO: cada IntervaloDeRumbo segundos, el cazador sin blanco recibe como patrulla la posicion ACTUAL del aliado mas
    //    cercano (con un poco de dispersion para que no lleguen en fila). Apenas ve a alguien su IA normal toma el control
    //    (Chase/Attack/coberturas) y al perderlo vuelve a cazar.
    public static class CazaDeEnemigos
    {
        public const float RadioDeAlerta = 38f;
        public const float RadioDeGrupo = 16f;
        public const float IntervaloDeRumbo = 2f;

        static readonly HashSet<Soldier> cazadores = new HashSet<Soldier>();
        static float reloj;
        public static int Cazadores => cazadores.Count;
        public static bool EsCazador(Soldier s) => s != null && cazadores.Contains(s);
        public static int RumbosDados { get; private set; }

        public static void Reiniciar() { cazadores.Clear(); reloj = 0f; RumbosDados = 0; }

        public static void Marcar(IEnumerable<Soldier> soldados)
        {
            if (soldados == null) return;
            foreach (var s in soldados) if (s != null && s.Team == TeamId.Enemy) cazadores.Add(s);
            reloj = 0f;   // el primer rumbo sale en el proximo Tick
        }

        static Soldier AliadoMasCercano(Vector3 p, out float dist)
        {
            Soldier mejor = null; dist = float.MaxValue;
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var a = todos[i];
                if (a == null || a.Team != TeamId.Player || a.Role == RoleType.Civilian || a.Health == null || !a.Health.IsAlive) continue;
                if (!a.gameObject.activeInHierarchy && !(a.Brain != null && a.Brain.MontadoEnVehiculo)) continue;
                var d = a.transform.position - p; d.y = 0f;
                float m = d.magnitude;
                if (m < dist) { dist = m; mejor = a; }
            }
            return mejor;
        }

        static bool Libre(AiBrain b) => b != null && b.CurrentTarget == null && !b.TieneOrden && !b.Pasivo && !b.Quieto
            && (b.State == AiState.Patrol || b.State == AiState.Idle);

        public static void Tick(float dt)
        {
            reloj -= dt;
            if (reloj > 0f) return;
            reloj = IntervaloDeRumbo;

            var todos = ActorRegistry.All;
            // Disparador 2: guardias que oyen a la escuadra cerca o que ya fueron heridos.
            for (int i = 0; i < todos.Count; i++)
            {
                var s = todos[i];
                if (s == null || s.Team != TeamId.Enemy || cazadores.Contains(s) || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                if (s.Brain != null && s.Brain.MontadoEnVehiculo) continue;
                AliadoMasCercano(s.transform.position, out float d);
                bool herido = s.Health.Current < s.Health.MaxHealth;
                bool peleando = s.Brain != null && s.Brain.CurrentTarget != null;
                if (d > RadioDeAlerta && !herido && !peleando) continue;
                cazadores.Add(s);
                // Avisa a su grupo.
                for (int j = 0; j < todos.Count; j++)
                {
                    var o = todos[j];
                    if (o == null || o == s || o.Team != TeamId.Enemy || !o.gameObject.activeInHierarchy || o.Health == null || !o.Health.IsAlive) continue;
                    var dd = o.transform.position - s.transform.position; dd.y = 0f;
                    if (dd.magnitude <= RadioDeGrupo) cazadores.Add(o);
                }
            }

            // Rumbo: a la posicion actual del aliado mas cercano.
            cazadores.RemoveWhere(s => s == null || s.Health == null || !s.Health.IsAlive);
            foreach (var s in cazadores)
            {
                if (!s.gameObject.activeInHierarchy || !Libre(s.Brain)) continue;
                var objetivo = AliadoMasCercano(s.transform.position, out float d);
                if (objetivo == null) continue;
                var meta = objetivo.transform.position;
                var lado = new Vector3(Mathf.Sin(s.Id * 2.3f), 0f, Mathf.Cos(s.Id * 2.3f)) * Mathf.Min(6f, d * 0.25f);
                s.Brain.SetPatrolRoute(new[] { meta + lado, meta - lado * 0.5f });
                RumbosDados++;
            }
        }
    }
}
