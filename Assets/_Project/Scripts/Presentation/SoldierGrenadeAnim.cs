using UnityEngine;
using SP.Actors;
using SP.Core;
using SP.Combat;

namespace SP.Presentation
{
    // Animacion del lanzamiento de granada ([G]), procedural y por CODIGO sobre el esqueleto humanoide, en el mismo estilo que
    // SoldierMeleeAnim (IK de dos huesos del brazo derecho en LateUpdate, ver BrazoIk):
    //   * el fusil se oculta y la granada queda AGARRADA en la mano derecha (Granada.Sujetar);
    //   * carga: el torso se echa hacia atras y gira, la mano sube por detras del hombro;
    //   * tiro: el torso barre hacia adelante y la mano describe un arco de arriba-atras a adelante-arriba;
    //   * en el punto justo del arco (Suelta) la granada sale de la mano con la velocidad que ya traia (Granada.Soltar);
    //   * seguimiento: la mano sigue de largo hacia abajo y el brazo vuelve a la pose del Animator.
    // Los soldados sin rig humano (los cubos de la suite headless) no la usan: la granada sale de inmediato como siempre.
    public class SoldierGrenadeAnim : MonoBehaviour
    {
        public const float Total = 0.74f;
        public const float FinCarga = 0.22f, Suelta = 0.4f;
        const float EntradaPeso = 0.08f, SalidaPeso = 0.22f;

        // Puntos de la mano en unidades del alcance del brazo, desde el hombro: (derecha, arriba, adelante).
        static readonly Vector3 PuntoDeCarga = new Vector3(0.5f, 0.95f, -0.5f);
        static readonly Vector3 PuntoDeSuelta = new Vector3(0.22f, 0.8f, 0.95f);
        static readonly Vector3 PuntoDeCierre = new Vector3(-0.25f, -0.3f, 0.8f);
        static readonly Vector3 HintDelCodo = new Vector3(0.9f, -0.2f, -0.4f);

        Soldier soldado;
        Animator anim;
        Transform pecho, brazo, antebrazo, mano;
        readonly OcultadorDeArma arma = new OcultadorDeArma();
        Granada granada;
        float t = -1f;
        bool resuelto;
        System.IDisposable suscripcion;

        public bool Activo => t >= 0f;
        public float Progreso => t < 0f ? 0f : Mathf.Clamp01(t / Total);
        public Transform Mano => mano;

        bool Resolver()
        {
            if (resuelto) return mano != null;
            anim = GetComponentInChildren<Animator>(true);
            if (anim == null || !anim.isHuman) return false;
            brazo = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            antebrazo = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            mano = anim.GetBoneTransform(HumanBodyBones.RightHand);
            pecho = anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Spine);
            if (brazo == null || antebrazo == null || mano == null) return false;
            resuelto = true;
            return true;
        }

        void OnEnable()
        {
            soldado = GetComponent<Soldier>();
            if (suscripcion == null && EventBus.Instance != null) suscripcion = EventBus.Instance.Subscribe<GrenadeThrownEvent>(AlLanzar);
        }

        void AlLanzar(GrenadeThrownEvent e)
        {
            if (soldado == null || e.OwnerId != soldado.Id || !Application.isPlaying || !Resolver()) return;
            // La granada recien creada es la ultima de la lista y es de este soldado.
            Granada g = null;
            for (int i = Granada.Activas.Count - 1; i >= 0; i--)
                if (Granada.Activas[i] != null && Granada.Activas[i].DuenoId == e.OwnerId) { g = Granada.Activas[i]; break; }
            if (g == null) return;
            if (granada != null) granada.Soltar();
            granada = g;
            granada.Sujetar();
            t = 0f;
            LlevarGranadaALaMano();
        }

        void LlevarGranadaALaMano()
        {
            if (granada == null || mano == null) return;
            granada.transform.position = mano.position + mano.up * 0.03f;
        }

        void LateUpdate()
        {
            if (t < 0f) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            t += dt;
            if (t >= Total || mano == null)
            {
                Terminar();
                return;
            }

            float w = BrazoIk.Suave(Mathf.Min(t / EntradaPeso, (Total - t) / SalidaPeso, 1f));
            arma.Ocultar(mano, transform);

            // 0..1 en cada tramo: carga, tiro, cierre.
            float kCarga = BrazoIk.Suave(t / FinCarga);
            float kTiro = BrazoIk.Suave((t - FinCarga) / (Suelta - FinCarga));
            float kCierre = BrazoIk.Suave((t - Suelta) / (Total - Suelta));

            Vector3 p;
            float giro, inclinacion;
            if (t < FinCarga)
            {
                p = PuntoDeCarga;
                giro = Mathf.Lerp(0f, 42f, kCarga);
                inclinacion = Mathf.Lerp(0f, -12f, kCarga);
            }
            else if (t < Suelta)
            {
                p = Vector3.Lerp(PuntoDeCarga, PuntoDeSuelta, kTiro);
                giro = Mathf.Lerp(42f, -30f, kTiro);
                inclinacion = Mathf.Lerp(-12f, 20f, kTiro);
            }
            else
            {
                p = Vector3.Lerp(PuntoDeSuelta, PuntoDeCierre, kCierre);
                giro = Mathf.Lerp(-30f, -38f, kCierre);
                inclinacion = Mathf.Lerp(20f, 26f, kCierre);
            }

            if (pecho != null)
                pecho.rotation = Quaternion.AngleAxis(giro * w, transform.up) * Quaternion.AngleAxis(inclinacion * w, transform.right) * pecho.rotation;
            BrazoIk.Resolver(transform, brazo, antebrazo, mano, p, HintDelCodo, w);

            if (granada != null)
            {
                if (t < Suelta) LlevarGranadaALaMano();
                else { granada.Soltar(); granada = null; }
            }
        }

        void Terminar()
        {
            t = -1f;
            arma.Restaurar();
            if (granada != null) { granada.Soltar(); granada = null; }
        }

        void OnDisable()
        {
            suscripcion?.Dispose();
            suscripcion = null;
            if (t >= 0f) Terminar();
        }
    }
}
