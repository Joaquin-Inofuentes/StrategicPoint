using UnityEngine;
using SP.Actors;
using SP.Core;

namespace SP.Presentation
{
    // Presentacion del golpe de cuchillo ([F], ronda 7): el cuchillo aparece en la mano, barre un
    // arco con estela, suena el tajo (KnifeSwing) y, si conecta, el golpe sordo (KnifeHit) con
    // chispa roja, etiqueta y sacudida. WeaponHolder.TryMelee decide el dano; esto solo lo muestra.
    public static class CuchilloFx
    {
        public static int Tajos { get; private set; }
        public static int Aciertos { get; private set; }

        public static void ResetearContadores() { Tajos = 0; Aciertos = 0; }

        public static void Tajo(Soldier dueno, Soldier objetivo)
        {
            Tajos++;
            if (objetivo != null) Aciertos++;
            if (!Application.isPlaying || dueno == null) return;

            bool jugador = dueno.Brain != null && dueno.Brain.IsPossessedByPlayer;
            var pos = dueno.transform.position;
            AudioDirector.PlayAt(SfxKind.KnifeSwing, pos, jugador ? 0.85f : 0.6f, 0.8f);

            var go = new GameObject("TajoDeCuchillo");
            go.AddComponent<TajoVisual>().Iniciar(dueno.transform);

            if (objetivo == null) return;
            var golpe = objetivo.transform.position + Vector3.up * 0.3f;
            ImpactFx.Spawn(golpe, new Color(1f, 0.2f, 0.15f), 0.55f, 0.2f);
            if (jugador)
            {
                Feedback.Accion(SfxKind.KnifeHit, "¡CUCHILLADA!", golpe, Feedback.Bad, aviso: false, pulso: true, volumen: 0.95f);
                var rig = SP.CameraSystem.CameraRig.Instance;
                if (rig != null) rig.KickDirectional((objetivo.transform.position - pos).normalized, 0.22f);
            }
            else AudioDirector.PlayAt(SfxKind.KnifeHit, golpe, 0.8f, 0.85f);
        }

        // El barrido: un pivote en el hombro derecho gira de +80 a -60 grados (0,22 s) con el cuchillo
        // extendido y una estela. Se destruye solo.
        class TajoVisual : MonoBehaviour
        {
            const float Duracion = 0.24f;
            Transform pivote;
            float edad;

            public void Iniciar(Transform dueno)
            {
                transform.SetParent(dueno, false);
                transform.localPosition = new Vector3(0f, 0.25f, 0.1f);
                transform.localRotation = Quaternion.identity;
                pivote = transform;

                var prefab = Resources.Load<GameObject>("Weapons/P_Wpn_Cuchillo");
                GameObject cuchillo;
                if (prefab != null)
                {
                    cuchillo = Instantiate(prefab, transform);
                    var rs = cuchillo.GetComponentsInChildren<Renderer>();
                    if (rs.Length > 0)
                    {
                        var b = rs[0].bounds;
                        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                        float mayor = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                        if (mayor > 0.0001f) cuchillo.transform.localScale *= 0.42f / mayor;
                    }
                    foreach (var col in cuchillo.GetComponentsInChildren<Collider>()) Destroy(col);
                }
                else
                {
                    cuchillo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(cuchillo.GetComponent<Collider>());
                    cuchillo.transform.SetParent(transform, false);
                    cuchillo.transform.localScale = new Vector3(0.04f, 0.04f, 0.4f);
                    cuchillo.GetComponent<Renderer>().sharedMaterial = SafeMaterial.Create(new Color(0.85f, 0.88f, 0.92f));
                }
                cuchillo.transform.localPosition = new Vector3(0f, 0f, 0.85f);
                cuchillo.transform.localRotation = Quaternion.identity;

                // Punta del cuchillo con estela: dibuja el arco del tajo.
                var punta = new GameObject("Punta");
                punta.transform.SetParent(transform, false);
                punta.transform.localPosition = new Vector3(0f, 0f, 1.15f);
                var tr = punta.AddComponent<TrailRenderer>();
                tr.time = 0.28f;
                tr.startWidth = 0.28f; tr.endWidth = 0f;
                tr.minVertexDistance = 0.03f;
                tr.sharedMaterial = SafeMaterial.Create(new Color(0.75f, 0.95f, 1f));
                tr.startColor = new Color(0.75f, 0.95f, 1f, 1f);
                tr.endColor = new Color(0.6f, 0.85f, 1f, 0f);
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Aplicar(0f);
            }

            void Aplicar(float k)
            {
                // Curva de "latigazo": arranca lento, acelera y frena al final.
                float e = k * k * (3f - 2f * k);
                float yaw = Mathf.Lerp(70f, -70f, e);
                float roll = Mathf.Lerp(-20f, 25f, e);
                pivote.localRotation = Quaternion.Euler(0f, yaw, roll);
            }

            void Update()
            {
                edad += Time.deltaTime;
                float k = Mathf.Clamp01(edad / Duracion);
                Aplicar(k);
                if (edad >= Duracion + 0.22f) Destroy(gameObject);
            }
        }
    }
}
