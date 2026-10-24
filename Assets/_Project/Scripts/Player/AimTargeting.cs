using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Vehicles;
using SP.Presentation;

namespace SP.Player
{
    public enum AimTargetType { None, Ally, Enemy, Vehicle, Ground, Obstacle, Torreta, Caido, Recoger, Interactuar, Cubrirse, Radio }

    public struct AimResult
    {
        public AimTargetType Type;
        public Soldier Soldier;
        public Vehicle Vehicle;
        public TorretaFija Torreta;
        public Vector3 Point;
        // B4: raiz del objeto golpeado, para el anillo generico de apuntado
        // (SelectionRingFx necesita un Transform a quien seguir, y Point es
        // solo el punto de impacto -- no sirve para seguir a algo que se
        // mueve). Null en Ground/None: ahi no hay "algo" a marcar en su base.
        public Transform HitTransform;
    }

    // Qué hay bajo el retículo o el cursor: un aliado poseíble, un vehículo,
    // o el suelo. Publica el resaltado para que la capa de presentación lo pinte.
    public class AimTargeting : MonoBehaviour
    {
        [SerializeField] float maxDistance = 200f;
        // Expuesto para quien necesite un punto de mundo valido incluso
        // cuando no se golpeo nada (apuntando al cielo): un punto lejano
        // sobre el mismo rayo, en vez de Vector3.zero.
        public float MaxDistance => maxDistance;

        int lastHighlightedId = -1;

        public const float AlturaDelRombo = 2.4f, AnguloDelRombo = 3.2f, DistanciaMaxDelRombo = 160f;

        // Aliado vivo cuyo rombo (sobre la cabeza) esta bajo la mira, el mas centrado. Null si ninguno.
        public static Soldier AliadoBajoSuRombo(Ray ray, Soldier excludeSelf)
        {
            Soldier mejor = null; float mejorAng = AnguloDelRombo;
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var s = todos[i];
                if (s == null || s == excludeSelf || s.Team != TeamId.Player || s.Role == RoleType.Civilian || s.Health == null || !s.Health.IsAlive) continue;
                if (!s.gameObject.activeInHierarchy) continue;
                var v = s.transform.position + Vector3.up * AlturaDelRombo - ray.origin;
                float d = v.magnitude;
                if (d < 6f || d > DistanciaMaxDelRombo) continue;   // pegado a vos el rombo casi no se ve: ahi se apunta al cuerpo
                float ang = Vector3.Angle(ray.direction, v);
                if (ang >= mejorAng) continue;
                // Bug #142: "unicamente para los aliados, si estan obstaculizados por objetos igual podre ver sus rombos y cambiar a ellos":
                // el rombo del aliado se dibuja por encima de todo, asi que tambien se lo puede apuntar a traves de paredes.
                mejor = s; mejorAng = ang;
            }
            return mejor;
        }

        // Pulido de rendimiento: Evaluate corre CADA frame y hacia 8 GetComponentInParent (uno de interfaz, el mas lento) mas un name.StartsWith
        // que reserva un string. Lo que hay "colgado" de un collider no cambia de un frame al otro: se resuelve una vez por collider.
        sealed class InfoDeCollider
        {
            public SP.Interaction.IInteractable interactable; public WeaponPickup wp; public MunicionPickup mp; public CajaDeSuministros cs;
            public Soldier soldier; public TorretaFija torreta; public Vehicle vehicle; public ObstacleMarker obstaculo; public bool suelo;
        }
        readonly System.Collections.Generic.Dictionary<Collider, InfoDeCollider> infoPorCollider = new System.Collections.Generic.Dictionary<Collider, InfoDeCollider>(128);
        Collider ultimoCollider; InfoDeCollider ultimaInfo;

        InfoDeCollider Resolver(Collider c)
        {
            if (c == ultimoCollider && ultimaInfo != null) return ultimaInfo;
            if (!infoPorCollider.TryGetValue(c, out var i))
            {
                if (infoPorCollider.Count > 600) infoPorCollider.Clear();
                i = new InfoDeCollider
                {
                    interactable = c.GetComponentInParent<SP.Interaction.IInteractable>(),
                    wp = c.GetComponentInParent<WeaponPickup>(),
                    mp = c.GetComponentInParent<MunicionPickup>(),
                    cs = c.GetComponentInParent<CajaDeSuministros>(),
                    soldier = c.GetComponentInParent<Soldier>(),
                    torreta = c.GetComponentInParent<TorretaFija>(),
                    vehicle = c.GetComponentInParent<Vehicle>(),
                    obstaculo = c.GetComponentInParent<ObstacleMarker>(),
                    suelo = c.gameObject.name.StartsWith("Ground"),
                };
                infoPorCollider[c] = i;
            }
            ultimoCollider = c; ultimaInfo = i;
            return i;
        }

        // Fix disparo de asalto (con y sin mira): el punto de tiro era AimResult.Point, que NO siempre es donde pega el rayo de la cruz:
        // contra un muro/obstaculo es el punto de COBERTURA (en el piso, al pie), y con un aliado delante de la mira es su cuerpo. La bala
        // salia hacia ahi (medido: 11 grados de desvio con un aliado a 1 m, ~1,4 m abajo a 175 m contra un muro) y con el zoom se ve.
        // El punto de tiro es el primer solido que toca el rayo de la camara, atravesando a la escuadra propia y los disparadores.
        static readonly RaycastHit[] bufferDeTiro = new RaycastHit[48];
        public Vector3 PuntoDeTiro(Ray ray, Soldier excludeSelf)
        {
            int n = Physics.RaycastNonAlloc(ray, bufferDeTiro, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            float mejor = float.MaxValue;
            var punto = ray.origin + ray.direction * maxDistance;
            for (int i = 0; i < n; i++)
            {
                var h = bufferDeTiro[i];
                if (h.collider == null || h.distance >= mejor || h.distance < 0.05f) continue;
                var info = Resolver(h.collider);
                if (info.soldier != null && (info.soldier == excludeSelf || info.soldier.Team == TeamId.Player)) continue;
                if (info.vehicle != null && info.vehicle.Bando == TeamId.Player) continue;
                mejor = h.distance;
                punto = h.point;
            }
            return punto;
        }

        public AimResult Evaluate(Ray ray, Soldier excludeSelf)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Aim.Sync");
            Physics.SyncTransforms();
            UnityEngine.Profiling.Profiler.EndSample();
            UnityEngine.Profiling.Profiler.BeginSample("Aim.Raycast");
            bool golpeo = Physics.Raycast(ray, out var hit, maxDistance, ~0, QueryTriggerInteraction.Collide);
            UnityEngine.Profiling.Profiler.EndSample();

            // Los aliados CAIDOS no tienen collider (se apaga al morir): se los apunta por cercania al rayo.
            UnityEngine.Profiling.Profiler.BeginSample("Aim.Caido");
            var caido = CaidoBajoRayo(ray, excludeSelf, golpeo ? hit.distance : maxDistance, out var puntoCaido);
            UnityEngine.Profiling.Profiler.EndSample();
            if (caido != null)
                return new AimResult { Type = AimTargetType.Caido, Soldier = caido, Point = puntoCaido, HitTransform = caido.transform };

            // Pedido: "los rombos de aliados para cambiar entre ellos: que apunte y me pueda cambiar a el". El rombo flota sobre la
            // cabeza y a distancia el cuerpo es un punto: apuntar al ROMBO cuenta como apuntar al aliado (y [F] lo posee).
            UnityEngine.Profiling.Profiler.BeginSample("Aim.Rombo");
            var bajoRombo = AliadoBajoSuRombo(ray, excludeSelf);
            UnityEngine.Profiling.Profiler.EndSample();
            if (bajoRombo != null)
                return new AimResult { Type = AimTargetType.Ally, Soldier = bajoRombo, Point = bajoRombo.transform.position + Vector3.up * AlturaDelRombo, HitTransform = bajoRombo.transform };

            if (golpeo)
            {
                // #119: en la vista RTS la radio de campana es un objetivo propio (cursor de interactuar, clic derecho = operarla).
                var rigRts = SP.CameraSystem.CameraRig.Instance;
                if (rigRts != null && rigRts.Mode == SP.CameraSystem.ControlMode.Rts && SP.Operacion.MandoTactico.EsParteDeLaRadio(hit.collider, hit.point))
                    return new AimResult { Type = AimTargetType.Radio, Point = hit.point, HitTransform = hit.collider.transform };

                var info = Resolver(hit.collider);
                var interactable = info.interactable;
                if (interactable != null && interactable.CanInteract(PlayerInputDriver.Activo))
                    return new AimResult { Type = AimTargetType.Interactuar, Point = hit.point, HitTransform = (interactable as MonoBehaviour)?.transform };

                var wp = info.wp;
                if (wp != null) return new AimResult { Type = AimTargetType.Recoger, Point = hit.point, HitTransform = wp.transform };
                var mp = info.mp;
                if (mp != null) return new AimResult { Type = AimTargetType.Recoger, Point = hit.point, HitTransform = mp.transform };
                var cs = info.cs;
                if (cs != null && cs.Disponible) return new AimResult { Type = AimTargetType.Recoger, Point = hit.point, HitTransform = cs.transform };

                var soldier = info.soldier;
                if (soldier != null && soldier != excludeSelf && soldier.Health.IsAlive)
                {
                    if (soldier.Team == TeamId.Player)
                    {
                        Highlight(soldier.Id);
                        return new AimResult { Type = AimTargetType.Ally, Soldier = soldier, Point = hit.point, HitTransform = soldier.transform };
                    }

                    Highlight(soldier.Id);
                    return new AimResult { Type = AimTargetType.Enemy, Soldier = soldier, Point = hit.point, HitTransform = soldier.transform };
                }

                // Un aliado CAIDO (muerto) se apunta aparte: se lo puede reanimar.
                if (soldier != null && soldier != excludeSelf && !soldier.Health.IsAlive && soldier.Team == TeamId.Player)
                    return new AimResult { Type = AimTargetType.Caido, Soldier = soldier, Point = hit.point, HitTransform = soldier.transform };

                ClearHighlight();

                var torreta = info.torreta;
                if (torreta != null)
                    return new AimResult { Type = AimTargetType.Torreta, Torreta = torreta, Point = hit.point, HitTransform = torreta.transform };

                var vehicle = info.vehicle;
                if (vehicle != null)
                    return new AimResult { Type = AimTargetType.Vehicle, Vehicle = vehicle, Point = hit.point, HitTransform = vehicle.transform };

                var obstaculo = info.obstaculo;
                if (obstaculo != null)
                {
                    if (Coberturas.TryPuntoApuntado(hit.point, obstaculo.transform, 1.5f, out var puntoCob, out _))
                        return new AimResult { Type = AimTargetType.Cubrirse, Point = puntoCob, HitTransform = obstaculo.transform };
                    return new AimResult { Type = AimTargetType.Obstacle, Point = hit.point, HitTransform = obstaculo.transform };
                }

                if (info.suelo)
                    return new AimResult { Type = AimTargetType.Ground, Point = hit.point };

                // CUALQUIER OTRO SOLIDO cuenta como suelo para el mando, con
                // el punto proyectado sobre el piso. Antes esto devolvia
                // None, y como todas las ordenes de RTS (mover, marcador,
                // fantasmas de formacion, vista previa de ruta) exigen
                // AimTargetType.Ground, un click derecho sobre un arbol,
                // una barricada o un barril no hacia NADA: ni orden, ni
                // marcador, ni aviso. El jugador clickea al lado de una
                // cobertura para reposicionarse y la escuadra lo ignora.
                //
                // Se ordena sobre el piso debajo del click y no sobre el
                // punto de impacto: si no, el destino quedaria a un metro
                // de altura arriba de la barricada.
                if (TryPuntoEnElPiso(ray, out var puntoPiso))
                {
                    // Un muro alto tapa el piso que hay detras: el punto del
                    // piso quedaria al otro lado de la pared (bug "Q atraviesa
                    // paredes"). Si el piso cae mas de 2 m despues del impacto,
                    // se ordena al pie del muro, del lado del jugador.
                    if (Vector3.Distance(ray.origin, puntoPiso) > hit.distance + 2f)
                    {
                        var haciaAtras = ray.origin - hit.point; haciaAtras.y = 0f;
                        haciaAtras = haciaAtras.sqrMagnitude > 0.0001f ? haciaAtras.normalized : Vector3.zero;
                        puntoPiso = new Vector3(hit.point.x, AlturaDelPiso, hit.point.z) + haciaAtras * 0.8f;
                    }
                    if (Coberturas.TryPuntoApuntado(puntoPiso, null, 1.5f, out var puntoCob, out var duenoCob))
                        return new AimResult { Type = AimTargetType.Cubrirse, Point = puntoCob, HitTransform = duenoCob != null ? duenoCob.transform : null };
                    return new AimResult { Type = AimTargetType.Ground, Point = puntoPiso };
                }

                return new AimResult { Type = AimTargetType.None };
            }

            ClearHighlight();
            return new AimResult { Type = AimTargetType.None };
        }

        const float RadioDeCaido = 0.9f;

        // Aliado muerto mas cercano a la linea de mira (dentro de 'limite' metros).
        static Soldier CaidoBajoRayo(Ray ray, Soldier excluir, float limite, out Vector3 punto)
        {
            punto = default;
            Soldier mejor = null; float mejorT = float.MaxValue;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s == excluir || s.Team != TeamId.Player || s.Health == null || s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                if (s.Role == RoleType.Civilian) continue;
                var centro = s.transform.position + Vector3.up * 0.2f;
                var v = centro - ray.origin;
                float t = Vector3.Dot(v, ray.direction);
                if (t < 0.5f || t > limite) continue;
                if (Vector3.Cross(ray.direction, v).magnitude > RadioDeCaido) continue;
                if (t < mejorT) { mejorT = t; mejor = s; punto = centro; }
            }
            return mejor;
        }

        void Highlight(int soldierId)
        {
            if (lastHighlightedId == soldierId) return;
            lastHighlightedId = soldierId;
            EventBus.Instance.Publish(new SwapTargetHighlightedEvent(soldierId));
        }

        void ClearHighlight()
        {
            if (lastHighlightedId == -1) return;
            lastHighlightedId = -1;
            EventBus.Instance.Publish(new SwapTargetClearedEvent());
        }

        // Altura del piso jugable. El suelo de la escena tiene su cara
        // superior en y=0; se cruza el rayo contra ese plano.
        const float AlturaDelPiso = 0f;

        // Punto del piso bajo un rayo (para ordenar "ir alli" aunque el cursor caiga sobre algo).
        public static bool PisoBajoRayo(Ray ray, out Vector3 punto) => TryPuntoEnElPiso(ray, out punto);

        static bool TryPuntoEnElPiso(Ray ray, out Vector3 punto)
        {
            punto = default;
            // Rayo paralelo al piso (o apuntando hacia arriba): no lo cruza.
            if (ray.direction.y >= -0.0001f) return false;
            float t = (AlturaDelPiso - ray.origin.y) / ray.direction.y;
            if (t < 0f) return false;
            punto = ray.origin + ray.direction * t;
            return true;
        }
    }
}
