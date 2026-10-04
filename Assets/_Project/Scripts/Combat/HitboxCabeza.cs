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
    public class HitboxCabeza : MonoBehaviour
    {
        public const float Lado = 0.34f;   // metros: un casco de soldado low poly
        public BoxCollider Caja { get; private set; }

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
                // El pivote del hueso de la cabeza esta en el cuello: el centro del craneo queda un poco arriba.
                go.transform.localPosition = Vector3.zero;
                var escala = hueso.lossyScale;
                float k = 1f / Mathf.Max(0.0001f, Mathf.Abs(escala.y));
                h.Caja.size = Vector3.one * Lado * k;
                h.Caja.center = hueso.InverseTransformVector(Vector3.up * 0.12f);
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
    }
}
