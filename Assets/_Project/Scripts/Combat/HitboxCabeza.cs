using System.Collections.Generic;
using UnityEngine;
using SP.Actors;

namespace SP.Combat
{
    // Bug #044: "si le apunto y disparo en la cabeza deberia decir HEADSHOT; debe haber un cubo de collider especial para la
    // cabeza. Solamente para enemigos, no para aliados". Antes la "cabeza" era el 18% superior de la caja del cuerpo: un tiro
    // al casco o al hombro alto contaba igual y nunca se mostraba nada.
    //
    // Es un BoxCollider TRIGGER hijo del hueso de la cabeza (sigue la animacion: agachado, apuntando, cayendo). Al ser trigger no
    // frena a nadie ni entra en la navegacion; solo lo consulta Projectile. Se crea a demanda la primera vez que una bala
    // pasa cerca de un enemigo, asi no hace falta tocar prefabs ni escenas.
    //
    // Bug #094 ("no se ven los headshots / no cuentan"): la caja era un cubo de 0,34 con un corrimiento fijo de 0,12 en ejes
    // del hueso (puestos a ojo) y la bala decidia con UNA sola muestra de 1 m de paso: una bala al casco quedaba como tiro al
    // cuerpo porque la muestra que entraba al collider del cuerpo caia antes de llegar a la caja. Ahora (1) la caja sale de los
    // vertices que la malla tiene pesados al hueso Head (en ejes del propio hueso, asi acompana la inclinacion de la cabeza) y
    // (2) Cruza() prueba el TRAMO entero de la bala contra la caja, con la pose actual del hueso (sin esperar al paso de fisica).
    public class HitboxCabeza : MonoBehaviour
    {
        public const float Lado = 0.34f;   // metros: respaldo para rigs sin malla pesada (casco de soldado low poly)
        const float Margen = 0.04f;        // la caja sobresale de la malla visible (la bala tiene radio y el cuello tambien cuenta)
        public BoxCollider Caja { get; private set; }

        // Caja de la cabeza en el espacio local del hueso, por malla (los 100+ enemigos comparten la misma malla).
        static readonly Dictionary<Mesh, Bounds?> cache = new Dictionary<Mesh, Bounds?>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() => cache.Clear();

        // Caja (en espacio del hueso) que envuelve los vertices pesados mayormente al hueso. null si la malla no tiene pesos.
        static Bounds? MedirCabeza(SkinnedMeshRenderer smr, Transform hueso)
        {
            var malla = smr.sharedMesh;
            if (malla == null || !malla.isReadable) return null;
            if (cache.TryGetValue(malla, out var guardada)) return guardada;
            Bounds? r = null;
            int idx = System.Array.IndexOf(smr.bones, hueso);
            var poses = malla.bindposes;
            if (idx >= 0 && idx < poses.Length)
            {
                var vs = malla.vertices; var pesos = malla.boneWeights;
                bool primero = true; var b = new Bounds();
                for (int i = 0; i < vs.Length; i++)
                {
                    var w = pesos[i];
                    float peso = (w.boneIndex0 == idx ? w.weight0 : 0f) + (w.boneIndex1 == idx ? w.weight1 : 0f)
                               + (w.boneIndex2 == idx ? w.weight2 : 0f) + (w.boneIndex3 == idx ? w.weight3 : 0f);
                    if (peso < 0.5f) continue;
                    var local = poses[idx].MultiplyPoint3x4(vs[i]);
                    if (primero) { b = new Bounds(local, Vector3.zero); primero = false; } else b.Encapsulate(local);
                }
                // Menos de 8 vertices no es una cabeza: mejor el respaldo que una caja absurda.
                if (!primero && b.size.x > 0.05f && b.size.y > 0.05f && b.size.z > 0.05f) r = b;
            }
            cache[malla] = r;
            return r;
        }

        public static HitboxCabeza De(Soldier s)
        {
            if (s == null || s.Team != TeamId.Enemy) return null;
            var h = s.GetComponentInChildren<HitboxCabeza>(true);
            if (h != null) return h;

            Transform hueso = null;
            var anim = s.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman) hueso = anim.GetBoneTransform(HumanBodyBones.Head);

            var go = new GameObject("HitboxCabeza");
            // Ignore Raycast: la mira, las balas contra el mundo y los sensores no la tienen que ver como algo solido.
            go.layer = 2;
            h = go.AddComponent<HitboxCabeza>();
            h.Caja = go.AddComponent<BoxCollider>();
            h.Caja.isTrigger = true;
            if (hueso != null)
            {
                go.transform.SetParent(hueso, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                Bounds? medida = null;
                foreach (var smr in s.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    medida = MedirCabeza(smr, hueso);
                    if (medida.HasValue) break;
                }
                if (medida.HasValue)
                {
                    h.Caja.center = medida.Value.center;
                    h.Caja.size = medida.Value.size + Vector3.one * (Margen * 2f);
                }
                else
                {
                    // Sin pesos: el centro del craneo queda un poco arriba del pivote del hueso (que esta en el cuello).
                    var escala = hueso.lossyScale;
                    float k = 1f / Mathf.Max(0.0001f, Mathf.Abs(escala.y));
                    h.Caja.size = Vector3.one * Lado * k;
                    h.Caja.center = hueso.InverseTransformVector(Vector3.up * 0.12f);
                }
            }
            else
            {
                // Sin rig humanoide: arriba de la caja visible del cuerpo.
                go.transform.SetParent(s.transform, false);
                var r = s.GetComponent<Renderer>();
                var b = r != null ? r.bounds : new Bounds(s.transform.position + Vector3.up, new Vector3(0.5f, 1.8f, 0.5f));
                go.transform.position = new Vector3(b.center.x, b.max.y - Lado * 0.5f, b.center.z);
                var e = s.transform.lossyScale;
                h.Caja.size = new Vector3(Lado / Mathf.Max(0.0001f, e.x), Lado / Mathf.Max(0.0001f, e.y), Lado / Mathf.Max(0.0001f, e.z));
            }
            return h;
        }

        // La bala (radio de tolerancia incluido) toca la caja de la cabeza en este punto.
        public bool Toca(Vector3 punto, float radio)
        {
            if (Caja == null || !Caja.enabled) return false;
            var c = Caja.ClosestPoint(punto);
            return (c - punto).sqrMagnitude <= radio * radio;
        }

        // Centro de la cabeza en el mundo con la pose de ESTE frame (Collider.bounds se queda con la del ultimo paso de fisica).
        public Vector3 Centro => Caja != null ? Caja.transform.TransformPoint(Caja.center) : transform.position;

        // El segmento desde->hasta (con "margen" de metros de tolerancia) atraviesa la caja. Es el test de tramo de Projectile:
        // matematica de caja orientada con la transform viva del hueso, no un Raycast de fisica (que va con un paso de atraso).
        public bool Cruza(Vector3 desde, Vector3 hasta, float margen, out Vector3 punto)
        {
            punto = desde;
            if (Caja == null || !Caja.enabled) return false;
            var t = Caja.transform;
            var m = t.worldToLocalMatrix;
            var a = m.MultiplyPoint3x4(desde) - Caja.center;
            var d = m.MultiplyPoint3x4(hasta) - Caja.center - a;
            var e = t.lossyScale;
            var medio = new Vector3(
                Caja.size.x * 0.5f + margen / Mathf.Max(0.0001f, Mathf.Abs(e.x)),
                Caja.size.y * 0.5f + margen / Mathf.Max(0.0001f, Mathf.Abs(e.y)),
                Caja.size.z * 0.5f + margen / Mathf.Max(0.0001f, Mathf.Abs(e.z)));
            float t0 = 0f, t1 = 1f;
            for (int i = 0; i < 3; i++)
            {
                float o = a[i], dir = d[i], lim = medio[i];
                if (Mathf.Abs(dir) < 1e-6f) { if (o < -lim || o > lim) return false; continue; }
                float ta = (-lim - o) / dir, tb = (lim - o) / dir;
                if (ta > tb) { var x = ta; ta = tb; tb = x; }
                if (ta > t0) t0 = ta;
                if (tb < t1) t1 = tb;
                if (t0 > t1) return false;
            }
            punto = Vector3.Lerp(desde, hasta, t0);
            return true;
        }
    }
}
