using System.Collections.Generic;
using UnityEngine;
using SP.Core;

namespace SP.Presentation
{
    // Animacion del golpe de cuchillo ([F]), procedural y por CODIGO sobre el esqueleto humanoide.
    //
    // Pedido explicito: "la animacion de melee esta rota, testeala y mejorala". El clip importado (AnimacionAtaqueMelee)
    // movia el hombro con el fusil todavia en la mano y un cuchillo suelto flotaba en el aire (CuchilloFx.TajoVisual). Ahora,
    // durante el golpe:
    //   * el fusil se oculta y aparece el cuchillo AGARRADO por la mano derecha;
    //   * el torso gira hacia atras (carga) y despues hacia adelante (tajo);
    //   * el brazo derecho lo resuelve un IK de dos huesos analitico: la mano recorre un arco de derecha-arriba-atras a
    //     izquierda-adelante, con el codo apuntando hacia abajo y afuera. No depende de los ejes de cada hueso (todo se
    //     calcula en el espacio del soldado), asi que vale igual para las tres variantes de cuerpo.
    // Corre en LateUpdate, o sea DESPUES de que el Animator poso el cuerpo, y solo modifica la pose de ese frame.
    // Los soldados sin rig humano (los cubos de la suite headless) no lo usan: Iniciar() devuelve false y CuchilloFx cae al
    // tajo flotante de siempre.
    public class SoldierMeleeAnim : MonoBehaviour
    {
        public const float Total = 0.72f;
        const float FinCarga = 0.17f, FinTajo = 0.37f, EntradaPeso = 0.07f, SalidaPeso = 0.24f;
        const float LargoCuchillo = 0.4f;

        // Puntos de la mano en unidades del alcance del brazo, medidos desde el hombro: (derecha, arriba, adelante).
        static readonly Vector3 PuntoDeCarga = new Vector3(0.78f, 0.82f, -0.22f);
        static readonly Vector3 PuntoDeTajo = new Vector3(-0.7f, -0.5f, 0.9f);
        static readonly Vector3 HintDelCodo = new Vector3(0.7f, -0.6f, -0.1f);

        Animator anim;
        Transform pecho, brazo, antebrazo, mano;
        Transform cuchillo, punta;
        TrailRenderer estela;
        readonly OcultadorDeArma arma = new OcultadorDeArma();
        float t = -1f;
        bool resuelto;

        public bool Activo => t >= 0f;
        public float Progreso => t < 0f ? 0f : Mathf.Clamp01(t / Total);
        public Transform Mano => mano;
        public Transform Cuchillo => cuchillo;

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

        // Devuelve true si este soldado tiene rig humano y el golpe se anima aca (el llamador no debe usar el tajo flotante).
        public bool Iniciar()
        {
            if (!Application.isPlaying || !Resolver()) return false;
            t = 0f;
            EnsureCuchillo();
            return true;
        }

        void EnsureCuchillo()
        {
            if (cuchillo != null) return;
            var prefab = RecursosCache.Cargar<GameObject>("Weapons/P_Wpn_Cuchillo");
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, mano);
                foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
                var rs = go.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    float mayor = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    if (mayor > 0.0001f) go.transform.localScale *= LargoCuchillo / mayor;
                }
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(mano, false);
                go.transform.localScale = new Vector3(0.03f, 0.03f, LargoCuchillo);
                go.GetComponent<Renderer>().sharedMaterial = SafeMaterial.Create(new Color(0.85f, 0.88f, 0.92f));
            }
            go.name = "CuchilloEnMano";
            cuchillo = go.transform;
            go.SetActive(false);

            var p = new GameObject("PuntaCuchillo");
            p.transform.SetParent(mano, false);
            punta = p.transform;
            estela = p.AddComponent<TrailRenderer>();
            estela.time = 0.22f;
            estela.startWidth = 0.11f; estela.endWidth = 0f;
            estela.minVertexDistance = 0.02f;
            estela.sharedMaterial = SafeMaterial.Create(new Color(0.85f, 0.95f, 1f));
            estela.startColor = new Color(0.85f, 0.95f, 1f, 0.9f);
            estela.endColor = new Color(0.7f, 0.85f, 1f, 0f);
            estela.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            estela.emitting = false;
        }

        static float Suave(float k) => BrazoIk.Suave(k);

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

            float w = Suave(Mathf.Min(t / EntradaPeso, (Total - t) / SalidaPeso, 1f));
            // 0 = carga (mano atras), 1 = fin del tajo (mano adelante); pasa un poco de 1 y vuelve (sobrepaso: mas exagerado).
            float x = t < FinCarga ? 0f : Mathf.Clamp01((t - FinCarga) / (FinTajo - FinCarga));
            float k = Suave(x) + 0.14f * Mathf.Sin(Mathf.PI * x) * (t < FinTajo ? 1f : 0f);

            OcultarArma(true);
            if (!cuchillo.gameObject.activeSelf) cuchillo.gameObject.SetActive(true);

            // Torso: gira hacia el lado de la carga y despues barre hacia el otro.
            if (pecho != null)
            {
                // Giro grande y cuerpo que se echa hacia atras en la carga y se tira hacia adelante en el tajo.
                float giro = Mathf.LerpUnclamped(46f, -58f, k) * w;
                float inclinacion = Mathf.LerpUnclamped(-14f, 24f, k) * w;
                pecho.rotation = Quaternion.AngleAxis(giro, transform.up) * Quaternion.AngleAxis(inclinacion, transform.right) * pecho.rotation;
            }

            ResolverBrazo(k, w);
            ColocarCuchillo(w);
        }

        void ResolverBrazo(float k, float w)
        {
            // El punto de la mano se pasa por un sobrepaso corto al final del tajo (mas exagerado) antes de asentarse.
            Vector3 p = Vector3.LerpUnclamped(PuntoDeCarga, PuntoDeTajo, k);
            BrazoIk.Resolver(transform, brazo, antebrazo, mano, p, HintDelCodo, w);
        }

        void ColocarCuchillo(float w)
        {
            Vector3 dir = mano.position - antebrazo.position;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : transform.forward;
            var rot = Quaternion.LookRotation(dir, transform.up);
            cuchillo.SetPositionAndRotation(mano.position + dir * (LargoCuchillo * 0.35f), rot);
            punta.position = mano.position + dir * (LargoCuchillo * 0.9f);
            if (estela != null) estela.emitting = w > 0.5f && t > FinCarga * 0.5f && t < FinTajo + 0.08f;
        }

        void OcultarArma(bool ocultar)
        {
            if (ocultar) arma.Ocultar(mano, transform); else arma.Restaurar();
        }

        void Terminar()
        {
            t = -1f;
            OcultarArma(false);
            if (cuchillo != null) cuchillo.gameObject.SetActive(false);
            if (estela != null) { estela.emitting = false; estela.Clear(); }
        }

        void OnDisable()
        {
            if (t >= 0f) Terminar();
        }
    }
}
