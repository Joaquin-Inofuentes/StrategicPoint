using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using SP.Combat;

namespace SP.Presentation
{
    // Pedido explicito: "quiero poder ver el soldado q manejo y sus armas
    // en su espalda". Cuelga un cubo chico por cada arma del Loadout
    // publico de WeaponHolder que el soldado NO tiene en la mano ahora
    // mismo -- la que está equipada no aparece acá, esa ya se ve colgada
    // de la mano por ArmaEnLaMano. Mismo truco de esa clase (colgar de un
    // hueso recien cuando el Animator ya poso el cuerpo), pero del hueso
    // de la espalda en vez de la mano.
    [RequireComponent(typeof(WeaponHolder))]
    public class WeaponBackRack : MonoBehaviour
    {
        // Mismo orden que WeaponHolder.Loadout por defecto. Si el dia de
        // mañana el loadout deja de ser fijo, esto sigue andando: cada
        // arma de esta lista que no esté en Loadout simplemente no se
        // instancia.
        static readonly WeaponKind[] AllKinds = { WeaponKind.Rifle, WeaponKind.Pistol, WeaponKind.Heavy };

        WeaponHolder holder;
        Transform[] slots;
        bool colgado;

        void Start()
        {
            holder = GetComponent<WeaponHolder>();
            StartCoroutine(ColgarCuandoElCuerpoEsteEnPose());
        }

        IEnumerator ColgarCuandoElCuerpoEsteEnPose()
        {
            yield return null;
            Colgar();
        }

        void Colgar()
        {
            var anim = GetComponentInChildren<Animator>(true);
            if (anim == null || !anim.isHuman) return;

            // Chest si existe (mas alto, mas "entre los omoplatos"); Spine
            // como respaldo para un rig sin ese hueso mapeado.
            var espalda = anim.GetBoneTransform(HumanBodyBones.Chest);
            if (espalda == null) espalda = anim.GetBoneTransform(HumanBodyBones.Spine);
            if (espalda == null) return;

            var escalaPadre = espalda.lossyScale;
            if (Mathf.Abs(escalaPadre.x) < 0.0001f) escalaPadre.x = 1f;
            if (Mathf.Abs(escalaPadre.y) < 0.0001f) escalaPadre.y = 1f;
            if (Mathf.Abs(escalaPadre.z) < 0.0001f) escalaPadre.z = 1f;

            slots = new Transform[AllKinds.Length];
            for (int i = 0; i < AllKinds.Length; i++)
            {
                var kind = AllKinds[i];
                var spec = WeaponCatalog.Get(kind);

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "BackWeapon_" + kind;
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var rend = go.GetComponent<Renderer>();
                rend.sharedMaterial = SafeMaterial.Create(spec.Color);
                rend.shadowCastingMode = ShadowCastingMode.Off;

                go.transform.SetParent(espalda, false);
                // Cruzadas en diagonal sobre la espalda, cada una un poco
                // mas arriba para no superponerse entre si.
                go.transform.localPosition = new Vector3(0f, 0.05f - i * 0.12f, -0.10f);
                go.transform.localRotation = Quaternion.Euler(15f, 90f, 25f);
                var escala = spec.VisualScale * 0.85f;
                go.transform.localScale = new Vector3(escala.x / escalaPadre.x, escala.y / escalaPadre.y, escala.z / escalaPadre.z);

                slots[i] = go.transform;
            }

            colgado = true;
        }

        void LateUpdate()
        {
            if (!colgado || holder == null) return;
            for (int i = 0; i < AllKinds.Length; i++)
            {
                if (slots[i] == null) continue;
                bool visible = AllKinds[i] != holder.CurrentWeaponKind && holder.Loadout.Contains(AllKinds[i]);
                if (slots[i].gameObject.activeSelf != visible) slots[i].gameObject.SetActive(visible);
            }
        }
    }
}
