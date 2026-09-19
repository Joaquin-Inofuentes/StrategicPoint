using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;

namespace SP.Player
{
    // Ordenes nuevas del radial que no vivian en OrderService: cubrirse segun hacia donde
    // se mira y "todos quietos". Estaticas y sin estado de escena para poder probarlas solas.
    public static class OrdenesDeEscuadra
    {
        public struct PuntoDeCobertura { public Vector3 Punto; public Collider Dueno; public float Puntaje; }

        // Cobertura "en referencia a donde miro": busca obstaculos que quedan DELANTE (dentro de
        // un cono de 65 grados) y devuelve el lado OPUESTO al de la mirada, o sea el que queda
        // protegido de lo que viene de ahi. Ordenadas de mejor a peor, con separacion minima.
        public static List<PuntoDeCobertura> BuscarSegunMirada(Vector3 origen, Vector3 dir, int cantidad,
            float distanciaMin = 2.5f, float distanciaMax = 25f, float conoGrados = 65f)
        {
            var res = new List<PuntoDeCobertura>();
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f || Coberturas.Cantidad == 0) return res;
            dir.Normalize();

            var candidatos = new List<PuntoDeCobertura>();
            for (int i = 0; i < Coberturas.Cantidad; i++)
            {
                var pt = Coberturas.Puntos[i];
                var dueno = Coberturas.Duenos[i];
                if (dueno == null) continue;

                var haciaPunto = pt - origen; haciaPunto.y = 0f;
                float dist = haciaPunto.magnitude;
                if (dist < distanciaMin || dist > distanciaMax) continue;

                var haciaObstaculo = dueno.bounds.center - origen; haciaObstaculo.y = 0f;
                if (haciaObstaculo.sqrMagnitude < 0.01f) continue;
                float ang = Vector3.Angle(dir, haciaObstaculo);
                if (ang > conoGrados) continue;

                // Lado protegido: la cara del punto mira en contra de la mirada.
                float lado = Vector3.Dot(Coberturas.FrenteDe(i), -dir);
                if (lado < 0.45f) continue;

                candidatos.Add(new PuntoDeCobertura { Punto = pt, Dueno = dueno, Puntaje = dist + ang * 0.25f + (1f - lado) * 8f });
            }
            candidatos.Sort((a, b) => a.Puntaje.CompareTo(b.Puntaje));
            foreach (var c in candidatos)
            {
                bool cerca = false;
                foreach (var r in res) if ((r.Punto - c.Punto).sqrMagnitude < 1.5f * 1.5f) { cerca = true; break; }
                if (cerca) continue;
                if (!Alcanzable(origen, c.Punto)) continue;
                res.Add(c);
                if (res.Count >= cantidad) break;
            }
            return res;
        }

        // Un punto de cobertura solo sirve si el NavMesh llega hasta el (algunos quedan pegados a paredes).
        static bool Alcanzable(Vector3 desde, Vector3 hasta)
        {
            if (!Application.isPlaying) return true;
            if (!UnityEngine.AI.NavMesh.SamplePosition(hasta, out var h, 1.2f, UnityEngine.AI.NavMesh.AllAreas)) return false;
            if (!UnityEngine.AI.NavMesh.SamplePosition(desde, out var d, 3f, UnityEngine.AI.NavMesh.AllAreas)) return true;
            var camino = new UnityEngine.AI.NavMeshPath();
            if (!UnityEngine.AI.NavMesh.CalculatePath(d.position, h.position, UnityEngine.AI.NavMesh.AllAreas, camino)) return false;
            return camino.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
        }

        // Reparte los soldados en las mejores coberturas encontradas (si hay menos coberturas que
        // soldados, los que sobran se abren en linea junto a la mejor). Devuelve cuantos cumplen.
        public static int CubrirSegunMirada(IList<Soldier> soldados, Vector3 origen, Vector3 dir, out Vector3 primerPunto)
        {
            primerPunto = origen;
            var puntos = BuscarSegunMirada(origen, dir, soldados.Count);
            if (puntos.Count == 0) return 0;
            primerPunto = puntos[0].Punto;
            var lateral = Vector3.Cross(Vector3.up, dir).normalized;
            int n = 0;
            for (int i = 0; i < soldados.Count; i++)
            {
                var p = i < puntos.Count ? puntos[i].Punto
                    : puntos[0].Punto + lateral * ((i - puntos.Count + 1) * 1.6f);
                var dueno = i < puntos.Count ? puntos[i].Dueno : puntos[0].Dueno;
                if (OrderService.IssueCoverOrder(soldados[i], p, dueno)) n++;
            }
            return n;
        }

        // TODOS QUIETOS: cancelan lo que hacian, se plantan donde estan y dejan de seguir al
        // jugador. Siguen disparando a lo que entre en su alcance (postura defensiva).
        public static int Quietos(IEnumerable<Soldier> soldados)
        {
            int n = 0;
            foreach (var s in soldados)
            {
                if (s == null || s.Health == null || !s.Health.IsAlive || s.Brain == null) continue;
                s.Brain.CancelOrder();
                s.Brain.Quieto = true;
                n++;
            }
            return n;
        }
    }
}
