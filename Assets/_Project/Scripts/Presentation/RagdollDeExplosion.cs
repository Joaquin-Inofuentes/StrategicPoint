using System.Collections.Generic;
using UnityEngine;
using SP.Actors;

namespace SP.Presentation
{
    // Ragdoll fisico (item 55) para los soldados que MUEREN por una explosion: en vez de la animacion de muerte, el
    // esqueleto humanoide se vuelve un muneco articulado (capsulas + CharacterJoint) y sale despedido lejos del
    // centro. Se arma al vuelo sobre los huesos del Animator y se desarma solo (a los ~2,5 s el cuerpo ya se oculta),
    // asi que no cuesta nada mientras nadie explota. Las muertes por bala siguen con las 6 animaciones.
    public class RagdollDeExplosion : MonoBehaviour
    {
        public const float SegundosActivo = 2.5f;
        public const float FuerzaBase = 7f;
        public bool Activo { get; private set; }
        public int CantidadDeCuerpos => cuerpos.Count;
        public static int TotalLanzados { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { TotalLanzados = 0; }

        struct Hueso { public HumanBodyBones hueso, padre, hijo; public float masa, radio; }
        static readonly Hueso[] Esqueleto =
        {
            new Hueso { hueso = HumanBodyBones.Hips,          padre = HumanBodyBones.LastBone, hijo = HumanBodyBones.Spine,        masa = 3f,   radio = 0.16f },
            new Hueso { hueso = HumanBodyBones.Spine,         padre = HumanBodyBones.Hips,     hijo = HumanBodyBones.Head,         masa = 3f,   radio = 0.16f },
            new Hueso { hueso = HumanBodyBones.Head,          padre = HumanBodyBones.Spine,    hijo = HumanBodyBones.LastBone,     masa = 1f,   radio = 0.11f },
            new Hueso { hueso = HumanBodyBones.LeftUpperArm,  padre = HumanBodyBones.Spine,    hijo = HumanBodyBones.LeftLowerArm, masa = 0.7f, radio = 0.05f },
            new Hueso { hueso = HumanBodyBones.LeftLowerArm,  padre = HumanBodyBones.LeftUpperArm,  hijo = HumanBodyBones.LeftHand, masa = 0.5f, radio = 0.045f },
            new Hueso { hueso = HumanBodyBones.RightUpperArm, padre = HumanBodyBones.Spine,    hijo = HumanBodyBones.RightLowerArm, masa = 0.7f, radio = 0.05f },
            new Hueso { hueso = HumanBodyBones.RightLowerArm, padre = HumanBodyBones.RightUpperArm, hijo = HumanBodyBones.RightHand, masa = 0.5f, radio = 0.045f },
            new Hueso { hueso = HumanBodyBones.LeftUpperLeg,  padre = HumanBodyBones.Hips,     hijo = HumanBodyBones.LeftLowerLeg, masa = 1.5f, radio = 0.07f },
            new Hueso { hueso = HumanBodyBones.LeftLowerLeg,  padre = HumanBodyBones.LeftUpperLeg,  hijo = HumanBodyBones.LeftFoot, masa = 1f,   radio = 0.055f },
            new Hueso { hueso = HumanBodyBones.RightUpperLeg, padre = HumanBodyBones.Hips,     hijo = HumanBodyBones.RightLowerLeg, masa = 1.5f, radio = 0.07f },
            new Hueso { hueso = HumanBodyBones.RightLowerLeg, padre = HumanBodyBones.RightUpperLeg, hijo = HumanBodyBones.RightFoot, masa = 1f,  radio = 0.055f },
        };

        Soldier soldier;
        Animator animator;
        readonly List<Component> creados = new List<Component>();
        readonly List<Rigidbody> cuerpos = new List<Rigidbody>();
        readonly List<Collider> colisionadores = new List<Collider>();
        float hasta;
        bool animadorApagado;

        // Convierte al soldado en muneco y lo empuja lejos de 'origen'. Devuelve false si no tiene esqueleto humanoide.
        public static bool Lanzar(Soldier s, Vector3 origen, float fuerza = FuerzaBase)
        {
            if (s == null || !Application.isPlaying) return false;
            var r = s.GetComponent<RagdollDeExplosion>();
            if (r == null) r = s.gameObject.AddComponent<RagdollDeExplosion>();
            return r.Armar(s, origen, fuerza);
        }

        bool Armar(Soldier s, Vector3 origen, float fuerza)
        {
            if (Activo) return true;
            soldier = s;
            animator = GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman) return false;

            var huesos = new Dictionary<HumanBodyBones, Rigidbody>();
            foreach (var h in Esqueleto)
            {
                var t = animator.GetBoneTransform(h.hueso);
                if (t == null) continue;
                var rb = t.gameObject.AddComponent<Rigidbody>();
                rb.mass = h.masa; rb.linearDamping = 0.2f; rb.angularDamping = 0.6f;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                creados.Add(rb); cuerpos.Add(rb); huesos[h.hueso] = rb;

                Vector3 haciaHijo;
                var th = h.hijo != HumanBodyBones.LastBone ? animator.GetBoneTransform(h.hijo) : null;
                if (th != null) haciaHijo = t.InverseTransformPoint(th.position);
                else
                {
                    var tp = animator.GetBoneTransform(h.padre);
                    var dir = tp != null ? (t.position - tp.position).normalized : Vector3.up;
                    haciaHijo = t.InverseTransformPoint(t.position + dir * 0.2f);
                }
                var cap = t.gameObject.AddComponent<CapsuleCollider>();
                float largo = Mathf.Max(0.05f, haciaHijo.magnitude);
                float ax = Mathf.Abs(haciaHijo.x), ay = Mathf.Abs(haciaHijo.y), az = Mathf.Abs(haciaHijo.z);
                cap.direction = ax > ay && ax > az ? 0 : ay > az ? 1 : 2;
                cap.center = haciaHijo * 0.5f;
                cap.radius = h.radio / Mathf.Max(0.01f, t.lossyScale.x);
                cap.height = largo + cap.radius * 2f;
                creados.Add(cap); colisionadores.Add(cap);
            }
            if (cuerpos.Count == 0) return false;

            foreach (var h in Esqueleto)
            {
                if (!huesos.TryGetValue(h.hueso, out var rb) || !huesos.TryGetValue(h.padre, out var rbPadre)) continue;
                var j = rb.gameObject.AddComponent<CharacterJoint>();
                j.connectedBody = rbPadre;
                j.anchor = Vector3.zero;
                j.enablePreprocessing = false; j.enableProjection = true;
                j.lowTwistLimit = new SoftJointLimit { limit = -25f };
                j.highTwistLimit = new SoftJointLimit { limit = 25f };
                j.swing1Limit = new SoftJointLimit { limit = 45f };
                j.swing2Limit = new SoftJointLimit { limit = 45f };
                creados.Add(j);
            }
            // Entre si los huesos no chocan (evita el temblor de las articulaciones) ni con el resto del soldado.
            for (int a = 0; a < colisionadores.Count; a++)
                for (int b = a + 1; b < colisionadores.Count; b++) Physics.IgnoreCollision(colisionadores[a], colisionadores[b]);
            foreach (var c in GetComponents<Collider>()) foreach (var m in colisionadores) Physics.IgnoreCollision(c, m);

            animator.enabled = false; animadorApagado = true;
            var caderas = huesos.TryGetValue(HumanBodyBones.Hips, out var cad) ? cad : cuerpos[0];
            var salida = caderas.position - origen; salida.y = 0f;
            salida = salida.sqrMagnitude < 0.01f ? Random.insideUnitSphere : salida.normalized;
            salida.y = 0f; salida = salida.normalized;
            var impulso = (salida + Vector3.up * 0.9f).normalized * fuerza;
            foreach (var rb in cuerpos) rb.linearVelocity = impulso * Random.Range(0.8f, 1.15f);
            caderas.angularVelocity = Random.onUnitSphere * 4f;

            Activo = true; hasta = Time.time + SegundosActivo; TotalLanzados++;
            return true;
        }

        // Quita las articulaciones, las capsulas y los cuerpos rigidos; el Animator se reactiva solo si el soldado revive.
        public void Desarmar()
        {
            // Las articulaciones primero: un Rigidbody no se puede destruir mientras otro joint depende de el.
            for (int i = creados.Count - 1; i >= 0; i--) if (creados[i] is CharacterJoint) Destroy(creados[i]);
            for (int i = creados.Count - 1; i >= 0; i--) if (creados[i] is Collider) Destroy(creados[i]);
            for (int i = creados.Count - 1; i >= 0; i--) if (creados[i] is Rigidbody) Destroy(creados[i]);
            creados.Clear(); cuerpos.Clear(); colisionadores.Clear();
            Activo = false;
        }

        void Update()
        {
            if (Activo && Time.time >= hasta) Desarmar();
            if (animadorApagado && soldier != null && soldier.Health != null && soldier.Health.IsAlive)
            {
                if (Activo) Desarmar();
                if (animator != null) animator.enabled = true;
                animadorApagado = false;
            }
        }
    }
}
