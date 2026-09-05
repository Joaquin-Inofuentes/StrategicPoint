using UnityEngine;

namespace SP.Core
{
    // Resolucion de colision para cuerpos que se mueven ESCRIBIENDO EL
    // TRANSFORM, que es como se mueve todo en este proyecto: ni el soldado
    // ni el vehiculo tienen Rigidbody, asi que la fisica nunca los frena
    // por su cuenta. Sin esto, "tener un BoxCollider bien puesto" no
    // significa nada: el collider sirve para los rayos (mira, balas,
    // linea de tiro) y para nada mas.
    //
    // Vive aca y no adentro de un motor concreto porque son DOS los que lo
    // necesitan -- SoldierMotor y VehicleMotor -- y una segunda copia se
    // habria ido separando de la primera. Ya paso con la definicion de
    // "que es una pared", que ahora tambien es una sola
    // (NavService.BlocksMovement) para los cuatro caminos que la
    // consultan.
    public static class Deslizador
    {
        // Margen que se deja SIEMPRE entre el cuerpo y lo que choca. Sin
        // el, el cuerpo queda apoyado a distancia exactamente 0 y el
        // barrido del frame siguiente devuelve un impacto a distancia 0:
        // se traba en vez de deslizar.
        public const float Piel = 0.03f;

        // Cuantas veces se reproyecta el movimiento restante al chocar.
        // 1 alcanza para una pared, 2 para una esquina; la tercera es el
        // caso raro de quedar encajado entre tres caras.
        const int MaxDeslizamientos = 3;

        static readonly RaycastHit[] BufferImpactos = new RaycastHit[16];
        static readonly Collider[] BufferSolapes = new Collider[16];

        // Toma el desplazamiento pedido y devuelve el que de verdad se
        // puede hacer. La componente vertical pasa intacta: este es un
        // juego sobre un piso unico, sin salto ni gravedad que resolver.
        public static Vector3 Resolver(Transform cuerpo, Collider propio, Vector3 delta, float radio)
        {
            if (cuerpo == null) return delta;

            float vertical = delta.y;
            var horizontal = new Vector3(delta.x, 0f, delta.z);
            float restante = horizontal.magnitude;
            if (restante < 0.00001f) return delta;

            var dir = horizontal / restante;
            var inicio = cuerpo.position;

            // Primero salir de adentro de algo, si ya estaba metido: un
            // cuerpo superpuesto devuelve impactos a distancia 0 con una
            // normal que no sirve, y sin esto quedaria clavado ahi.
            var pos = inicio + Despenetrar(cuerpo, propio, inicio, radio);

            for (int i = 0; i < MaxDeslizamientos && restante > 0.00001f; i++)
            {
                if (!Barrer(cuerpo, pos, dir, restante + Piel, radio, out float distancia, out var normal))
                {
                    pos += dir * restante;
                    restante = 0f;
                    break;
                }

                float avance = Mathf.Max(0f, distancia - Piel);
                pos += dir * avance;
                restante -= avance;

                var n = new Vector3(normal.x, 0f, normal.z);
                if (n.sqrMagnitude < 0.000001f) break; // cara horizontal: no hay por donde deslizar
                n.Normalize();

                // Se le saca al movimiento restante la componente que entra
                // en la pared. Lo que queda corre PARALELO a ella: por eso
                // el cuerpo se desliza en vez de frenar en seco.
                var deslizado = Vector3.ProjectOnPlane(dir * restante, n);
                deslizado.y = 0f;
                restante = deslizado.magnitude;
                if (restante < 0.00001f) break;
                dir = deslizado / restante;
            }

            var resultado = pos - inicio;
            resultado.y = vertical;
            return resultado;
        }

        static bool Barrer(Transform cuerpo, Vector3 pos, Vector3 dir, float distancia, float radio,
                           out float distanciaImpacto, out Vector3 normal)
        {
            distanciaImpacto = 0f;
            normal = Vector3.zero;

            int n = Physics.SphereCastNonAlloc(pos, radio, dir, BufferImpactos, distancia, ~0, QueryTriggerInteraction.Ignore);
            float mejor = float.MaxValue;
            bool hay = false;

            for (int i = 0; i < n; i++)
            {
                var h = BufferImpactos[i];
                if (h.collider == null) continue;
                if (h.collider.transform.IsChildOf(cuerpo)) continue;
                // distancia 0 significa que la esfera ya arrancaba
                // superpuesta: la normal que devuelve Unity ahi no es la de
                // la cara. De eso se ocupa Despenetrar, no este barrido.
                if (h.distance <= 0f) continue;
                if (h.distance >= mejor) continue;
                if (!NavService.BlocksMovement(h.collider)) continue;

                mejor = h.distance;
                normal = h.normal;
                hay = true;
            }

            distanciaImpacto = mejor;
            return hay;
        }

        static Vector3 Despenetrar(Transform cuerpo, Collider propio, Vector3 pos, float radio)
        {
            if (propio == null) return Vector3.zero;

            int n = Physics.OverlapSphereNonAlloc(pos, radio, BufferSolapes, ~0, QueryTriggerInteraction.Ignore);
            var empuje = Vector3.zero;

            for (int i = 0; i < n; i++)
            {
                var otro = BufferSolapes[i];
                if (otro == null || otro == propio) continue;
                if (otro.transform.IsChildOf(cuerpo)) continue;
                if (!NavService.BlocksMovement(otro)) continue;

                if (!Physics.ComputePenetration(propio, pos, cuerpo.rotation,
                                                otro, otro.transform.position, otro.transform.rotation,
                                                out var dir, out float dist))
                    continue;

                dir.y = 0f;
                if (dir.sqrMagnitude < 0.000001f) continue;
                empuje += dir.normalized * (dist + Piel);
            }

            return empuje;
        }

        // Radio horizontal util de un collider, tomado de la ESCALA y no de
        // bounds: bounds es una caja alineada al mundo y crece al girar el
        // cuerpo, asi que un tanque en diagonal se creeria 40% mas ancho y
        // frenaria antes de tocar nada.
        public static float RadioDe(Collider col, Transform cuerpo, float porDefecto = 0.4f)
        {
            if (col is BoxCollider caja)
            {
                var e = cuerpo.lossyScale;
                float lado = Mathf.Min(Mathf.Abs(caja.size.x * e.x), Mathf.Abs(caja.size.z * e.z));
                return Mathf.Max(0.05f, lado * 0.5f - Piel);
            }
            if (col is CapsuleCollider capsula)
            {
                var e = cuerpo.lossyScale;
                return Mathf.Max(0.05f, capsula.radius * Mathf.Max(Mathf.Abs(e.x), Mathf.Abs(e.z)) - Piel);
            }
            if (col != null)
            {
                var ext = col.bounds.extents;
                return Mathf.Max(0.05f, Mathf.Min(ext.x, ext.z) - Piel);
            }
            return porDefecto;
        }
    }
}
