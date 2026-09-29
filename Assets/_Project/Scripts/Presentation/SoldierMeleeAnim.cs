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
        public const float Total = 0.55f;
        const float FinCarga = 0.09f, FinTajo = 0.27f, EntradaPeso = 0.06f, SalidaPeso = 0.16f;
        const float LargoCuchillo = 0.34f;

        // Puntos de la mano en unidades del alcance del brazo, medidos desde el hombro: (derecha, arriba, adelante).
        static readonly Vector3 PuntoDeCarga = new Vector3(0.55f, 0.45f, 0.15f);
        static readonly Vector3 PuntoDeTajo = new Vector3(-0.42f, -0.12f, 0.88f);
        static readonly Vector3 HintDelCodo = new Vector3(0.7f, -0.6f, -0.1f);

        Animator anim;
        Transform pecho, brazo, antebrazo, mano;
        Transform cuchillo, punta;
        TrailRenderer estela;
        Renderer[] renderersDelArma;
        bool[] armaEstabaVisible;
        bool armaOculta;
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
            estela.time = 0.16f;
            estela.startWidth = 0.06f; estela.endWidth = 0f;
            estela.minVertexDistance = 0.02f;
            estela.sharedMaterial = SafeMaterial.Create(new Color(0.85f, 0.95f, 1f));
            estela.startColor = new Color(0.85f, 0.95f, 1f, 0.9f);
            estela.endColor = new Color(0.7f, 0.85f, 1f, 0f);
            estela.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            estela.emitting = false;
        }

        static float Suave(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }

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
            // 0 = carga (mano atras), 1 = fin del tajo (mano adelante).
            float k = t < FinCarga ? 0f : Suave((t - FinCarga) / (FinTajo - FinCarga));

            OcultarArma(true);
            if (!cuchillo.gameObject.activeSelf) cuchillo.gameObject.SetActive(true);

            // Torso: gira hacia el lado de la carga y despues barre hacia el otro.
            if (pecho != null)
            {
                float giro = Mathf.Lerp(26f, -30f, k) * w;
                pecho.rotation = Quaternion.AngleAxis(giro, transform.up) * pecho.rotation;
            }

            ResolverBrazo(k, w);
            ColocarCuchillo(w);
        }

        void ResolverBrazo(float k, float w)
        {
            Vector3 hombro = brazo.position;
            float l1 = Vector3.Distance(brazo.position, antebrazo.position);
            float l2 = Vector3.Distance(antebrazo.position, mano.position);
            float alcance = l1 + l2;
            if (alcance < 0.05f) return;

            Vector3 p = Vector3.Lerp(PuntoDeCarga, PuntoDeTajo, k);
            Vector3 d = (transform.right * p.x + transform.up * p.y + transform.forward * p.z) * alcance;
            float dist = Mathf.Clamp(d.magnitude, 0.3f * alcance, 0.98f * alcance);
            Vector3 dirN = d.sqrMagnitude > 1e-6f ? d.normalized : transform.forward;
            Vector3 objetivo = hombro + dirN * dist;

            // Codo: interseccion de las dos esferas (largo del brazo y del antebrazo), del lado que marca el hint.
            float a = (l1 * l1 - l2 * l2 + dist * dist) / (2f * dist);
            float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - a * a));
            Vector3 hint = transform.right * HintDelCodo.x + transform.up * HintDelCodo.y + transform.forward * HintDelCodo.z;
            Vector3 lado = Vector3.ProjectOnPlane(hint, dirN);
            if (lado.sqrMagnitude < 1e-6f) lado = transform.right;
            Vector3 codo = hombro + dirN * a + lado.normalized * h;

            Vector3 viejaDirBrazo = (antebrazo.position - brazo.position).normalized;
            Vector3 nuevaDirBrazo = (codo - hombro).normalized;
            var rotBrazo = Quaternion.FromToRotation(viejaDirBrazo, nuevaDirBrazo) * brazo.rotation;
            brazo.rotation = Quaternion.Slerp(brazo.rotation, rotBrazo, w);

            Vector3 viejaDirAntebrazo = (mano.position - antebrazo.position).normalized;
            Vector3 nuevaDirAntebrazo = (objetivo - antebrazo.position).normalized;
            var rotAntebrazo = Quaternion.FromToRotation(viejaDirAntebrazo, nuevaDirAntebrazo) * antebrazo.rotation;
            antebrazo.rotation = Quaternion.Slerp(antebrazo.rotation, rotAntebrazo, w);
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
            if (ocultar == armaOculta) return;
            if (ocultar)
            {
                var arma = (mano != null ? BuscarHijo.Ruta(mano, "WeaponVisual") : null) ?? BuscarHijo.Ruta(transform, "WeaponVisual");
                if (arma == null) return;
                renderersDelArma = arma.GetComponentsInChildren<Renderer>(true);
                armaEstabaVisible = new bool[renderersDelArma.Length];
                for (int i = 0; i < renderersDelArma.Length; i++)
                {
                    armaEstabaVisible[i] = renderersDelArma[i] != null && renderersDelArma[i].enabled;
                    if (renderersDelArma[i] != null) renderersDelArma[i].enabled = false;
                }
                armaOculta = true;
            }
            else
            {
                if (renderersDelArma != null)
                    for (int i = 0; i < renderersDelArma.Length; i++)
                        if (renderersDelArma[i] != null && armaEstabaVisible[i]) renderersDelArma[i].enabled = true;
                renderersDelArma = null;
                armaOculta = false;
            }
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
