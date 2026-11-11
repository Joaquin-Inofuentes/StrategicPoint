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
        public float alturaInicial = 0f;                           // altura del inicio (las escaleras de la mansion arrancan en un piso de arriba)
        public float alturaDelFinal = 6.5f;
        public float anchoMedio = 1.15f;
        public const float MargenDeAltura = 0.6f;
        public static bool MargenPorVelocidad = true;   // #418: solo lo apaga el check para reproducir el bug original

        static readonly List<RampaCaminable> todas = new List<RampaCaminable>();
        public static IReadOnlyList<RampaCaminable> Todas => todas;
        public static int SoldadosEnRampa { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { todas.Clear(); SoldadosEnRampa = 0; frameDeLaLista = frameDelConteo = -1; vivos.Clear(); }

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
            y = Mathf.Lerp(alturaInicial, alturaDelFinal, Mathf.Clamp01(s / largo));
            return true;
        }

        // Perfil del 2026-11-11: hay una RampaCaminable por escalera/rampa (decenas) y cada una recorria TODOS los soldados cada frame consultando
        // activeInHierarchy, Health.IsAlive y Motor (~1,2 ms por frame en total). Ahora la lista de soldados vivos se arma UNA vez por frame y se
        // comparte, y cada rampa descarta primero por caja en planta (barata) antes de tocar el motor.
        static readonly List<Soldier> vivos = new List<Soldier>();
        static int frameDeLaLista = -1, frameDelConteo = -1;

        static List<Soldier> VivosDeEsteFrame()
        {
            if (frameDeLaLista == Time.frameCount) return vivos;
            frameDeLaLista = Time.frameCount; vivos.Clear();
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var s = todos[i];
                if (s == null || !s.gameObject.activeInHierarchy || s.Health == null || !s.Health.IsAlive || s.Motor == null) continue;
                vivos.Add(s);
            }
            return vivos;
        }

        // Margen de altura con el que la rampa agarra a un soldado. #418: suma lo que la cuesta sube en lo que el soldado puede avanzar en un cuadro
        // (corriendo, x1,7, x2 con el modo dios, y con el Editor cargado son varios metros por cuadro).
        float MargenActual(SoldierMotor m)
        {
            if (!MargenPorVelocidad) return MargenDeAltura;
            var eje = arriba - abajo; eje.y = 0f; float largo = eje.magnitude;
            float pendiente = largo > 0.01f ? Mathf.Abs(alturaDelFinal - alturaInicial) / largo : 0f;
            return MargenDeAltura + pendiente * m.MoveSpeed * Mathf.Clamp(Time.deltaTime, 0.02f, 0.25f) * 1.5f;
        }

        // Bug #418 ("si me desplazo con shift atravieso la rampa"): SoldierMotor.Move mira el piso (RevisarBorde) justo despues de avanzar, ANTES de que la rampa
        // lo levante en el LateUpdate. Si en ese cuadro avanzo mas de ~0,3 m de altura de la cuesta, el rayo del piso arrancaba por debajo de la superficie, no
        // encontraba la rampa, y el soldado "caia" al suelo, metiendose debajo. Ahora el motor le pide a las rampas que lo apoyen en el acto; si esta sobre una,
        // no se revisa el borde.
        public static bool AjustarAlMoverse(Transform t, SoldierMotor m)
        {
            if (todas.Count == 0 || m == null || m.IsJumping || m.Vaulting) return false;
            var p = t.position;
            for (int i = 0; i < todas.Count; i++)
            {
                var r = todas[i]; if (r == null) continue;
                float margen = r.anchoMedio + 0.4f;
                if (p.x < Mathf.Min(r.abajo.x, r.arriba.x) - margen || p.x > Mathf.Max(r.abajo.x, r.arriba.x) + margen ||
                    p.z < Mathf.Min(r.abajo.z, r.arriba.z) - margen || p.z > Mathf.Max(r.abajo.z, r.arriba.z) + margen) continue;
                if (!r.Superficie(p, out float ry)) continue;
                float objetivo = ry + SoldierMotor.PivoteSobrePiso;
                if (Mathf.Abs(p.y - objetivo) > r.MargenActual(m)) continue;
                t.position = new Vector3(p.x, objetivo, p.z);
                return true;
            }
            return false;
        }

        void LateUpdate()
        {
            if (frameDelConteo != Time.frameCount) { frameDelConteo = Time.frameCount; SoldadosEnRampa = 0; }
            float margen = anchoMedio + 0.4f;
            float xMin = Mathf.Min(abajo.x, arriba.x) - margen - 0.4f, xMax = Mathf.Max(abajo.x, arriba.x) + margen + 0.4f;
            float zMin = Mathf.Min(abajo.z, arriba.z) - margen - 0.4f, zMax = Mathf.Max(abajo.z, arriba.z) + margen + 0.4f;
            var lista = VivosDeEsteFrame();
            // Bug #418 ("si me desplazo con shift atravieso la rampa"): corriendo (x1,7, x2 con el modo dios) y con el Editor a ~17 FPS un soldado avanza varios metros
            // por cuadro; en una cuesta eso son mas de 0,6 m de altura entre un cuadro y el siguiente, la rampa dejaba de agarrarlo y se metia adentro. El margen ahora
            // suma lo que la cuesta sube en lo que el soldado puede avanzar en un cuadro.
            for (int i = 0; i < lista.Count; i++)
            {
                var s = lista[i];
                if (s == null) continue;
                var p = s.transform.position;
                if (p.x < xMin || p.x > xMax || p.z < zMin || p.z > zMax) continue;   // fuera de la caja de esta rampa
                var m = s.Motor;
                if (m == null || m.IsJumping || m.Vaulting) continue;
                if (!Superficie(p, out float ry)) continue;
                float objetivo = ry + SoldierMotor.PivoteSobrePiso;
                if (Mathf.Abs(p.y - objetivo) > MargenActual(m)) continue;
                s.transform.position = new Vector3(p.x, objetivo, p.z);
                SoldadosEnRampa++;
            }
        }
    }
}
