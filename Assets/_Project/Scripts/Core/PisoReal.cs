using UnityEngine;
using UnityEngine.AI;

namespace SP.Core
{
    // Bugs #287/#294 (cosas que "levitan" tras una explosion; marcas en el aire): el piso REAL bajo un punto, sin contar lo que se apoya sobre
    // el (sacos, escombros, cuerpos, vehiculos, el zocalo de una torre que despues cae). Se toma el impacto MAS BAJO que sea geometria fija
    // y de cara hacia arriba; si no hay, la malla de navegacion y por ultimo el tope del asfalto (0,08 m).
    public static class PisoReal
    {
        public const float TopeDelAsfalto = 0.08f;
        static readonly RaycastHit[] golpes = new RaycastHit[16];

        // Devuelve true si encontro piso por rayo o por malla de navegacion. y/normal siempre quedan con un valor usable.
        public static bool Altura(Vector3 p, out float y, out Vector3 normal, float desde = 3f, float largo = 40f)
        {
            y = TopeDelAsfalto; normal = Vector3.up;
            int n = Physics.RaycastNonAlloc(new Vector3(p.x, p.y + desde, p.z), Vector3.down, golpes, largo, ~0, QueryTriggerInteraction.Ignore);
            // Se arranca por la superficie valida MAS ALTA y se baja mientras haya otra a menos de 1,3 m debajo: lo bajo apoyado sobre el piso (sacos,
            // escombros, un zocalo) no es el piso, pero un techo con 5 m de vacio debajo si lo es (la explosion en una azotea marca la azotea).
            float mejorY = float.NegativeInfinity; Vector3 mejorN = Vector3.up; bool hay = false;
            for (int i = 0; i < n; i++)
            {
                var h = golpes[i];
                if (!EsPiso(h.collider) || h.normal.y < 0.8f) continue;
                if (h.point.y > p.y + desde) continue;
                if (!hay || h.point.y > mejorY) { mejorY = h.point.y; mejorN = h.normal; hay = true; }
            }
            for (bool bajo = hay; bajo;)
            {
                bajo = false; float siguiente = float.NegativeInfinity; Vector3 sn = Vector3.up;
                for (int i = 0; i < n; i++)
                {
                    var h = golpes[i];
                    if (!EsPiso(h.collider) || h.normal.y < 0.8f) continue;
                    if (h.point.y < mejorY - 0.02f && mejorY - h.point.y <= 1.3f && h.point.y > siguiente) { siguiente = h.point.y; sn = h.normal; bajo = true; }
                }
                if (bajo) { mejorY = siguiente; mejorN = sn; }
            }
            if (hay) { y = mejorY; normal = mejorN; }
            if (NavMesh.SamplePosition(new Vector3(p.x, hay ? y : p.y, p.z), out var nh, 3f, NavMesh.AllAreas) && (!hay || Mathf.Abs(nh.position.y - y) < 0.35f))
            {
                // El asfalto de la Operacion son cubos SIN collider: el rayo da 0 y la malla (que si lo sigue) da ~0,08.
                if (!hay) { y = nh.position.y; hay = true; }
                else y = Mathf.Max(y, nh.position.y);
            }
            y = Mathf.Max(y, TopeDelAsfalto);
            return hay;
        }

        public static float Altura(Vector3 p) { Altura(p, out var y, out _); return y; }

        // Geometria fija: ni soldados, ni vehiculos, ni cuerpos sueltos con fisica, ni escombros, ni lo que se puede derrumbar.
        static bool EsPiso(Collider c)
        {
            if (c == null || c.isTrigger) return false;
            if (!NavService.BlocksMovement(c)) return false;
            var rb = c.attachedRigidbody;
            if (rb != null && !rb.isKinematic) return false;
            if (c.GetComponentInParent<SP.Actors.Soldier>() != null) return false;
            if (c.GetComponentInParent<SP.Vehicles.Vehicle>() != null) return false;
            if (c.GetComponentInParent<SP.Operacion.TorreDestruible>() != null) return false;
            var marca = c.GetComponentInParent<SP.Presentation.ObstacleMarker>();
            if (marca != null && marca.EsDestruible && marca.MaxHealth < 5000) return false;   // sacos, barriles, muros demolibles: cosas que se van, no el piso
            return true;
        }
    }
}
