using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Core;
using SP.Presentation;

namespace SP.Operacion
{
    // Bug #042: "estaria bueno que los enemigos esten cubiertos, asi daria mas la ilusion de guerra". Los guardias del
    // cuartel y de los puestos arrancaban parados al descubierto en medio de la ruta. Al empezar la fase, cada guardia
    // toma la cobertura mas cercana que lo tape del lado por donde viene la escuadra, agachado; si no hay ninguna a mano
    // se le arma una barricada de bolsas de arena delante. En combate la IA ya busca coberturas sola (TickCoberturaTactica):
    // esto es la posicion de partida.
    public static class Atrincherar
    {
        public const float RadioDeBusqueda = 9f;
        // Que tanto tiene que estar el obstaculo entre el punto y la amenaza (coseno): 0,35 ~ hasta 70 grados de costado.
        const float AlineacionMinima = 0.35f;
        const float SeparacionEntreGuardias = 1.3f;

        public static int Barricadas { get; private set; }
        public static int Atrincherados { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Barricadas = 0; Atrincherados = 0; }

        // amenaza: de donde viene la escuadra (la posicion del jugador al empezar la fase).
        public static int Aplicar(IEnumerable<Soldier> enemigos, Vector3 amenaza)
        {
            if (enemigos == null) return 0;
            if (Coberturas.Cantidad == 0) Coberturas.Registrar();
            var tomados = new List<Vector3>();
            var pendientes = new List<(Soldier s, Vector3 punto, Vector3 haciaAmenaza)>();
            bool creoBarricadas = false;
            int n = 0;

            foreach (var s in enemigos)
            {
                if (s == null || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy || s.Brain == null) continue;
                var pos = s.transform.position;
                var hacia = amenaza - pos; hacia.y = 0f;
                if (hacia.sqrMagnitude < 0.01f) hacia = Vector3.back;
                hacia.Normalize();

                if (!MejorCobertura(pos, hacia, tomados, out var punto))
                {
                    // Sin cobertura cerca: bolsas de arena delante del guardia, mirando a la amenaza. Si otro guardia ya quedo
                    // ahi (medido: dos guardias del puesto 1 terminaban en el mismo punto), se corre de costado.
                    var lado = Vector3.Cross(Vector3.up, hacia).normalized;
                    for (int intento = 0; intento < 6 && Ocupado(pos, tomados); intento++)
                        pos += lado * (intento % 2 == 0 ? 1f : -1f) * SeparacionEntreGuardias * (1 + intento / 2);
                    CrearBarricada(pos + hacia * 1.15f, hacia);
                    creoBarricadas = true;
                    punto = pos;
                }
                tomados.Add(punto);
                pendientes.Add((s, punto, hacia));
            }

            foreach (var (s, punto, hacia) in pendientes)
            {
                var p = punto;
                if (UnityEngine.AI.NavMesh.SamplePosition(p, out var h, 1.5f, UnityEngine.AI.NavMesh.AllAreas)) p = new Vector3(h.position.x, s.transform.position.y, h.position.z);
                s.transform.position = new Vector3(p.x, s.transform.position.y, p.z);
                ApoyoEnElPiso.Apoyar(s.transform);
                s.transform.rotation = Quaternion.LookRotation(hacia);
                s.Brain.ReactivarNavegacion();
                s.Brain.SetPatrolRoute(new[] { s.transform.position, s.transform.position });
                if (s.Motor != null) s.Motor.SetCrouching(true);
                s.Brain.Atrincherado = true;
                n++;
            }

            if (creoBarricadas)
            {
                NavService.Invalidate();
                NavMeshViva.Solicitar();
                Coberturas.Registrar();
            }
            Atrincherados += n;
            GameLog.Line($"[Operacion] {n} guardias atrincherados ({Barricadas} barricadas en total)");
            return n;
        }

        static bool MejorCobertura(Vector3 pos, Vector3 haciaAmenaza, List<Vector3> tomados, out Vector3 punto)
        {
            punto = pos;
            float mejor = float.MaxValue;
            var puntos = Coberturas.Puntos;
            var duenos = Coberturas.Duenos;
            for (int i = 0; i < puntos.Count; i++)
            {
                var p = puntos[i];
                float d = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(pos.x, 0f, pos.z));
                if (d > RadioDeBusqueda || Mathf.Abs(p.y - pos.y) > 2.5f) continue;
                if (duenos[i] == null) continue;
                // El obstaculo tiene que quedar ENTRE el punto y la amenaza: el frente del punto mira para el otro lado.
                var haciaObstaculo = -Coberturas.FrenteDe(i);
                if (Vector3.Dot(haciaObstaculo, haciaAmenaza) < AlineacionMinima) continue;
                if (Ocupado(p, tomados)) continue;
                if (d < mejor) { mejor = d; punto = p; }
            }
            return mejor < float.MaxValue;
        }

        static bool Ocupado(Vector3 p, List<Vector3> tomados)
        {
            foreach (var t in tomados)
            {
                var d = t - p; d.y = 0f;
                if (d.sqrMagnitude < SeparacionEntreGuardias * SeparacionEntreGuardias) return true;
            }
            return false;
        }

        static readonly Color ColorBolsas = new Color(0.55f, 0.47f, 0.32f);

        // Bug #061: "aqui deberian haber coberturas para cubrirse" (plaza de la ciudad). Arma barricadas en los puntos pedidos
        // (cada una mirando hacia su amenaza), salteando las que chocarian con algo, y registra las coberturas una sola vez.
        public static int ArmarCoberturas(IList<(Vector3 punto, Vector3 haciaAmenaza)> lugares)
        {
            int n = 0;
            foreach (var (p, h) in lugares)
            {
                var hacia = h; hacia.y = 0f; if (hacia.sqrMagnitude < 0.01f) continue; hacia.Normalize();
                var centro = new Vector3(p.x, 0.6f, p.z);
                if (Physics.CheckBox(centro, new Vector3(1.4f, 0.45f, 0.6f), Quaternion.LookRotation(hacia), ~0, QueryTriggerInteraction.Ignore)) continue;
                CrearBarricada(p, hacia);
                n++;
            }
            if (n > 0)
            {
                NavService.Invalidate();
                NavMeshViva.Solicitar();
                Coberturas.Registrar();
            }
            GameLog.Line($"[Operacion] {n} barricadas de cobertura armadas");
            return n;
        }

        static void CrearBarricada(Vector3 centro, Vector3 haciaAmenaza)
        {
            var raiz = new GameObject("Barricada_BolsasDeArena");
            raiz.transform.position = new Vector3(centro.x, 0f, centro.z);
            raiz.transform.rotation = Quaternion.LookRotation(haciaAmenaza);
            ApoyoEnElPiso.Apoyar(raiz.transform);
            // Un solo collider para la cobertura y la navegacion; las bolsas son solo visuales.
            var col = raiz.AddComponent<BoxCollider>();
            col.size = new Vector3(2.3f, 1.05f, 0.7f);
            col.center = new Vector3(0f, 0.52f, 0f);
            var mat = SafeMaterial.Create(ColorBolsas);
            for (int fila = 0; fila < 3; fila++)
            {
                int cuantas = 3 - (fila == 2 ? 1 : 0);
                for (int k = 0; k < cuantas; k++)
                {
                    var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.Destroy(b.GetComponent<Collider>());
                    b.name = "Bolsa";
                    b.transform.SetParent(raiz.transform, false);
                    float x = (k - (cuantas - 1) * 0.5f) * 0.74f + (fila == 1 ? 0.18f : 0f);
                    b.transform.localPosition = new Vector3(x, 0.17f + fila * 0.34f, Random.Range(-0.04f, 0.04f));
                    b.transform.localRotation = Quaternion.Euler(0f, Random.Range(-6f, 6f), 0f);
                    b.transform.localScale = new Vector3(0.72f, 0.33f, 0.62f);
                    b.GetComponent<Renderer>().sharedMaterial = mat;
                }
            }
            var obstaculo = raiz.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstaculo.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstaculo.size = col.size; obstaculo.center = col.center; obstaculo.carving = true;
            Barricadas++;
        }
    }
}
