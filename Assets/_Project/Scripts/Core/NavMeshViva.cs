using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace SP.Core
{
    // EL NAVMESH QUE SE QUEDABA CON LOS MUROS EN PIE.
    //
    // El NavMesh se hornea en el editor y en partida no cambiaba nunca. Cuando un obstaculo se derrumbaba (carga del asalto,
    // disparos, aplastamiento) NavService rehacia su grilla, pero el NavMeshAgent seguia planificando contra la malla vieja:
    // alrededor de un muro caido todos rodeaban un obstaculo que ya no existe. Medido con la muralla este del fortin: el hueco
    // de 2 m dejaba una ruta de 117 m (153 m con el porton del norte); el civil la recorria y se trababa en una esquina de la
    // muralla sur, mientras los aliados cruzaban porque en combate caminan directo.
    //
    // Aca se rehace el NavMesh en juego, tras un derrumbe, con los COLLIDERS de la escena como fuente (la malla visual no se
    // puede leer en un build). El NavMeshSurface lo hace entero y en segundo plano: 7-50 ms en el hilo principal (juntar las
    // fuentes) y ~0,1 s hasta terminar. Rehacer solo los tiles cercanos NO sirve: la grilla de tiles no se conoce desde afuera y un
    // tile reconstruido con fuentes parciales queda vacio.
    public static class NavMeshViva
    {
        // Espera tras el ultimo derrumbe: una explosion en cadena tira varios obstaculos juntos y alcanza con un solo rehorneado.
        public const float EsperaTrasDerrumbe = 0.8f;
        // Y entre un rehorneado y el siguiente (un tiroteo contra coberturas puede tirar una cada pocos segundos).
        public const float IntervaloMinimo = 2f;
        // Capa de los fragmentos que salen de un muro roto (Fragmentador.LayerSinRayos): cuerpos sueltos, no paredes.
        const int CapaDeFragmentos = SP.Presentation.Fragmentador.LayerSinRayos;

        static float reloj;
        static float ultimoRehacer = -999f;
        static float rehacerEn = -1f;
        static readonly List<AsyncOperation> enCurso = new List<AsyncOperation>();
        static readonly HashSet<NavMeshSurface> preparadas = new HashSet<NavMeshSurface>();

        // Cuantas veces se pidio / termino un rehorneado. AiBrain compara contra esta Version para rehacer la ruta del agent
        // con la malla nueva en vez de seguir la vieja.
        public static int Version { get; private set; }
        public static int Pedidos { get; private set; }
        public static float UltimaDuracion { get; private set; }   // segundos de reloj desde que se lanzo hasta que termino
        static float inicio;
        public static bool Rehaciendo => enCurso.Count > 0 || rehacerEn >= 0f;

        // Los estaticos sobreviven a "Enter Play Mode" sin recarga de dominio.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetearAlCargar()
        {
            reloj = 0f;
            ultimoRehacer = -999f;
            rehacerEn = -1f;
            enCurso.Clear();
            preparadas.Clear();
            Version = 0;
            Pedidos = 0;
        }

        // Un obstaculo cayo (o aparecio uno nuevo): hay que rehacer la malla de navegacion.
        public static void Solicitar()
        {
            if (!Application.isPlaying) return;
            Pedidos++;
            rehacerEn = reloj + EsperaTrasDerrumbe;
        }

        // Mismo camino de simulacion que el resto (WorldSimulationDriver.Step).
        public static void Tick(float dt)
        {
            reloj += dt;

            for (int i = enCurso.Count - 1; i >= 0; i--)
            {
                if (enCurso[i] != null && !enCurso[i].isDone) continue;
                enCurso.RemoveAt(i);
                if (enCurso.Count == 0)
                {
                    Version++;   // terminaron todas: las rutas planeadas con la malla vieja ya no valen
                    UltimaDuracion = Time.realtimeSinceStartup - inicio;
                }
            }

            if (rehacerEn < 0f || reloj < rehacerEn || enCurso.Count > 0) return;
            if (reloj - ultimoRehacer < IntervaloMinimo) return;   // el pedido sigue pendiente: sale apenas pase el intervalo
            rehacerEn = -1f;
            ultimoRehacer = reloj;
            Rehacer();
        }

        static void Rehacer()
        {
            var surfaces = NavMeshSurface.activeSurfaces;
            if (surfaces.Count == 0) return;

            IgnorarVehiculos();
            inicio = Time.realtimeSinceStartup;
            for (int i = 0; i < surfaces.Count; i++)
            {
                var s = surfaces[i];
                if (s == null || s.navMeshData == null) continue;
                Preparar(s);
                enCurso.Add(s.UpdateNavMesh(s.navMeshData));
            }
            if (enCurso.Count == 0) return;
            GameLog.Line("NavMesh: se rehace tras un derrumbe");
        }

        static void Preparar(NavMeshSurface s)
        {
            if (!preparadas.Add(s)) return;

            // Se trabaja sobre una COPIA de los datos horneados: rehornear el asset original lo dejaria modificado (en el editor al
            // salir de Play; en un build, hasta que se descargue) y reiniciar la mision arrancaria con los muros ya abiertos.
            var copia = Object.Instantiate(s.navMeshData);
            copia.name = s.navMeshData.name + " (viva)";
            s.RemoveData();
            s.navMeshData = copia;
            s.AddData();
            // El horneado del editor uso la malla visual; en juego no se puede leer (isReadable en un build), asi que se usan los colliders.
            s.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            s.layerMask = s.layerMask.value & ~(1 << CapaDeFragmentos);
            // Los soldados llevan collider y NavMeshAgent: no son paredes.
            s.ignoreNavMeshAgent = true;
            IgnorarArboles();
        }

        // ~1000 capsulas "ArbolCollider" (ArteMundo/ArbolesColliders) frenan el cuerpo del soldado pero NUNCA estuvieron en la malla
        // horneada (no tienen render). Con colliders entran todas: la malla triplica sus poligonos y las rutas largas (base -> refugio,
        // 300 m) salen PathPartial. Medido: sin ellas queda con los mismos caminos y menos poligonos que la horneada.
        static void IgnorarArboles()
        {
            var arte = RaicesDeEscena.Buscar("ArteMundo");
            var arboles = arte != null ? arte.transform.Find("ArbolesColliders") : null;
            if (arboles == null || arboles.GetComponent<NavMeshModifier>() != null) return;
            var m = arboles.gameObject.AddComponent<NavMeshModifier>();
            m.ignoreFromBuild = true;
            m.applyToChildren = true;
        }

        // Un vehiculo se mueve: su chasis no tiene que quedar horneado como pared en el lugar donde estaba.
        static void IgnorarVehiculos()
        {
            WorldSystemsRegistry.EnsurePopulated();
            var vehiculos = WorldSystemsRegistry.Vehicles;
            for (int i = 0; i < vehiculos.Count; i++)
            {
                var v = vehiculos[i];
                if (v == null || v.GetComponent<NavMeshModifier>() != null) continue;
                var m = v.gameObject.AddComponent<NavMeshModifier>();
                m.ignoreFromBuild = true;
                m.applyToChildren = true;
            }
        }
    }
}
