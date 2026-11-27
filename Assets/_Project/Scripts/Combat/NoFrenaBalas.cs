using System.Collections.Generic;
using UnityEngine;

namespace SP.Combat
{
    // Marcador (#409/#410, "bug de colliders de disparos"): el collider SIGUE frenando el movimiento (NavService.BlocksMovement no cambia) pero las
    // balas, los disparos de la mira y las explosiones lo atraviesan. Lo llevan las rejas invisibles de las compuertas de las torres de la mansion:
    // no se ven y no tienen que frenar el tiro ni dejar calcomanias flotando en el aire.
    // R20 (#995, "la bala se frena por un collider invisible; queda el decal flotando"): tambien cuenta como "no frena balas" cualquier collider
    // solido SIN superficie visible (ningun MeshRenderer/SkinnedMeshRenderer propio ni de un hermano que lo cubra). Ver SuperficieVisible.
    public sealed class NoFrenaBalas : MonoBehaviour
    {
        public static bool Tiene(Collider c) => c != null && (c.GetComponentInParent<NoFrenaBalas>() != null || SuperficieVisible.SinSuperficie(c));
    }

    // R20 (#995): que superficie dibujada respalda a un collider. Las balas solo se frenan (y solo dejan marca) donde hay algo que se ve:
    //   * collider sin ningun renderer asociado -> la bala lo atraviesa (SinSuperficie);
    //   * collider mas grande que su malla -> la bala sigue hasta la caja orientada (localBounds) del renderer y el impacto (y el decal) va ahi.
    // Renderers asociados: los del propio collider y sus hijos; si no hay, los de la familia del padre (hasta 2 niveles, <= 256 renderers) cuya
    // caja toca la del collider (el "solido" hermano de la pieza que se ve). Se cachea por collider (las piezas del nivel no cambian de malla).
    // No cuenta renderer.enabled a proposito: la vista por planta de la mansion apaga el DIBUJO de los pisos de arriba y sus losas tienen que seguir
    // frenando balas. Los triggers, el terreno, los vehiculos y los soldados quedan fuera (tienen su propio camino).
    public static class SuperficieVisible
    {
        public const float Tolerancia = 0.05f;      // el punto de impacto cuenta como "sobre la superficie" a <= 5 cm de la caja del renderer
        public const float AlcanceExtra = 3f;       // cuanto mas alla del collider se busca la malla que lo respalda
        const int MaxRenderersDeLaFamilia = 256;

        static readonly Dictionary<Collider, Renderer[]> cache = new Dictionary<Collider, Renderer[]>(512);
        static readonly List<Renderer> tmp = new List<Renderer>(64);
        static readonly Renderer[] Ninguno = new Renderer[0];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() => cache.Clear();

        public static bool Exento(Collider c)
        {
            if (c == null || c.isTrigger || c is TerrainCollider) return true;
            var t = c.transform;
            return t.GetComponentInParent<SP.Vehicles.Vehicle>() != null || t.GetComponentInParent<SP.Actors.Soldier>() != null;
        }

        public static bool SinSuperficie(Collider c)
        {
            if (Exento(c)) return false;
            var rs = De(c);
            for (int i = 0; i < rs.Length; i++) if (Sirve(rs[i])) return false;
            return true;
        }

        static bool Sirve(Renderer r) => r != null && r.gameObject.activeInHierarchy;

        static bool EsDeSuperficie(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr) return smr.sharedMesh != null;
            if (!(r is MeshRenderer)) return false;   // particulas, lineas, sprites, trazadoras: no son una superficie
            var mf = r.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null;
        }

        public static Renderer[] De(Collider c)
        {
            if (c == null) return Ninguno;
            if (cache.TryGetValue(c, out var rs)) return rs;
            tmp.Clear();
            c.GetComponentsInChildren(true, tmp);
            for (int i = tmp.Count - 1; i >= 0; i--) if (!EsDeSuperficie(tmp[i])) tmp.RemoveAt(i);
            if (tmp.Count == 0)
            {
                var caja = c.bounds; caja.Expand(0.1f);
                var p = c.transform.parent;
                for (int nivel = 0; nivel < 2 && p != null && tmp.Count == 0; nivel++, p = p.parent)
                {
                    p.GetComponentsInChildren(true, tmp);
                    if (tmp.Count > MaxRenderersDeLaFamilia) { tmp.Clear(); break; }
                    for (int i = tmp.Count - 1; i >= 0; i--)
                        if (!EsDeSuperficie(tmp[i]) || !tmp[i].bounds.Intersects(caja)) tmp.RemoveAt(i);
                }
            }
            rs = tmp.Count == 0 ? Ninguno : tmp.ToArray();
            tmp.Clear();
            if (cache.Count > 8192) cache.Clear();
            cache[c] = rs;
            return rs;
        }

        // Con static batching (todo el decorado en Play) la malla del MeshFilter pasa a ser la combinada de la escena y localBounds deja de
        // describir la pieza (medido: la caja quedaba a 80-250 m del collider). Ahi, y en las mallas con piel, vale la caja del mundo (bounds).
        static bool UsaCajaDelMundo(Renderer r) => r is SkinnedMeshRenderer || r.isPartOfStaticBatch;

        // Distancia (m) de p a la caja orientada del renderer (0 adentro).
        public static float Distancia(Renderer r, Vector3 p)
        {
            if (UsaCajaDelMundo(r)) return Mathf.Sqrt(r.bounds.SqrDistance(p));
            var t = r.transform; var lb = r.localBounds;
            var q = t.InverseTransformPoint(p);
            var cerca = new Vector3(Mathf.Clamp(q.x, lb.min.x, lb.max.x), Mathf.Clamp(q.y, lb.min.y, lb.max.y), Mathf.Clamp(q.z, lb.min.z, lb.max.z));
            return (t.TransformPoint(cerca) - p).magnitude;
        }

        // Distancia de p a la superficie visible mas cercana de c (float.MaxValue si no tiene).
        public static float DistanciaALaSuperficie(Collider c, Vector3 p)
        {
            var rs = De(c); float mejor = float.MaxValue;
            for (int i = 0; i < rs.Length; i++) if (Sirve(rs[i])) mejor = Mathf.Min(mejor, Distancia(rs[i], p));
            return mejor;
        }

        // Entrada del rayo o + d*t (d unitario) en la caja orientada del renderer, con t en [tMin, tMax].
        static bool Entrada(Renderer r, Vector3 o, Vector3 d, float tMin, float tMax, out float tEntrada, out Vector3 normal)
        {
            tEntrada = 0f; normal = -d;
            Transform t = r.transform; Bounds lb; Vector3 lo, ld;
            if (UsaCajaDelMundo(r)) { lb = r.bounds; lo = o; ld = d; t = null; }
            else { lb = r.localBounds; lo = t.InverseTransformPoint(o); ld = t.InverseTransformVector(d); }
            float t0 = float.NegativeInfinity, t1 = float.PositiveInfinity; int eje = -1; float signo = 0f;
            for (int k = 0; k < 3; k++)
            {
                float od = ld[k], oo = lo[k], mn = lb.min[k], mx = lb.max[k];
                if (Mathf.Abs(od) < 1e-8f) { if (oo < mn || oo > mx) return false; continue; }
                float a = (mn - oo) / od, b = (mx - oo) / od;
                float s = -Mathf.Sign(od);
                if (a > b) { var x = a; a = b; b = x; }
                if (a > t0) { t0 = a; eje = k; signo = s; }
                if (b < t1) t1 = b;
                if (t0 > t1) return false;
            }
            if (t1 < tMin || t0 > tMax) return false;
            tEntrada = Mathf.Max(t0, tMin);
            if (eje >= 0 && t0 >= tMin)
            {
                var n = Vector3.zero; n[eje] = signo;
                normal = t != null ? (t.rotation * n).normalized : n;
            }
            return true;
        }

        // Donde frena de verdad una bala que el fisico mando contra 'c' a distancia 'dist' (d unitario desde o). false = no hay superficie que la
        // frene en el alcance: la bala sigue de largo. Si el punto fisico ya esta sobre la malla se devuelve tal cual.
        public static bool Impacto(Collider c, Vector3 o, Vector3 d, float dist, Vector3 puntoFisico, Vector3 normalFisica, out float distReal, out Vector3 normal)
        {
            distReal = dist; normal = normalFisica;
            var rs = De(c);
            bool hay = false;
            for (int i = 0; i < rs.Length; i++)
            {
                if (!Sirve(rs[i])) continue;
                hay = true;
                if (Distancia(rs[i], puntoFisico) <= Tolerancia) return true;
            }
            if (!hay) return false;
            float mejor = float.MaxValue; Vector3 nMejor = normalFisica;
            for (int i = 0; i < rs.Length; i++)
            {
                if (!Sirve(rs[i])) continue;
                if (Entrada(rs[i], o, d, dist, dist + AlcanceExtra, out var te, out var n) && te < mejor) { mejor = te; nMejor = n; }
            }
            if (mejor == float.MaxValue) return false;
            distReal = mejor; normal = nMejor;
            return true;
        }
    }
}
