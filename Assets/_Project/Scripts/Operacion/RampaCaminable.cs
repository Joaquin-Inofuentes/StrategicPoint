using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Core;

namespace SP.Operacion
{
    // WP10 (#101): la rampa del campanario. El motor de los soldados camina en plano (la altura solo cambia con saltos, trepas y caidas), asi que una
    // cuesta no se puede subir sola. Esta marca hace dos cosas: (1) NavService.BlocksMovement ignora su collider (si no, el deslizador lo toma por una
    // pared y nadie sube); (2) mientras un soldado esta sobre la franja de la rampa y a menos de 0,6 m de su superficie, la altura la fija la rampa. Si sale de la
    // franja de costado queda en el aire y el motor lo hace caer (RevisarBorde), como con cualquier borde.
    public class RampaCaminable : MonoBehaviour
    {
        public Vector3 abajo = new Vector3(323f, 0f, 265.3f);     // inicio (a ras del piso)
        public Vector3 arriba = new Vector3(337f, 0f, 265.3f);    // fin (borde de la plataforma)
        public float alturaDelFinal = 6.5f;
        public float anchoMedio = 1.15f;
        public const float MargenDeAltura = 0.6f;

        static readonly List<RampaCaminable> todas = new List<RampaCaminable>();
        public static IReadOnlyList<RampaCaminable> Todas => todas;
        public static int SoldadosEnRampa { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { todas.Clear(); SoldadosEnRampa = 0; }

        void OnEnable() { if (!todas.Contains(this)) todas.Add(this); }
        void OnDisable() { todas.Remove(this); }

        // Altura de la superficie bajo p (solo si p cae sobre la franja).
        public bool Superficie(Vector3 p, out float y)
        {
            y = 0f;
            var eje = arriba - abajo; eje.y = 0f;
            float largo = eje.magnitude;
            if (largo < 0.01f) return false;
            eje /= largo;
            var d = p - abajo; d.y = 0f;
            float s = Vector3.Dot(d, eje);
            if (s < -0.3f || s > largo + 0.3f) return false;
            float lateral = Mathf.Abs(d.x * eje.z - d.z * eje.x);
            if (lateral > anchoMedio) return false;
            y = Mathf.Lerp(0f, alturaDelFinal, Mathf.Clamp01(s / largo));
            return true;
        }

        void LateUpdate()
        {
            int n = 0;
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var s = todos[i];
                if (s == null || !s.gameObject.activeInHierarchy || s.Health == null || !s.Health.IsAlive) continue;
                var m = s.Motor;
                if (m == null || m.IsJumping || m.Vaulting) continue;
                var p = s.transform.position;
                if (!Superficie(p, out float ry)) continue;
                float objetivo = ry + SoldierMotor.PivoteSobrePiso;
                if (Mathf.Abs(p.y - objetivo) > MargenDeAltura) continue;
                s.transform.position = new Vector3(p.x, objetivo, p.z);
                n++;
            }
            SoldadosEnRampa = n;
        }
    }
}
