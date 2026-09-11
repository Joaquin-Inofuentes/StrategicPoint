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

        // A que largo se ve CUALQUIER arma real sobre la espalda, sin
        // importar su tamaño natural -- ver el comentario de
        // WeaponModels.NaturalLength sobre el bug que esto corrige (la
        // pesada, de 1,12 m de largo, quedaba casi tan larga como el
        // soldado con la escala plana de antes).
        const float TargetBackLength = 0.5f;

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

                var go = new GameObject("BackWeapon_" + kind);
                go.transform.SetParent(espalda, false);
                // Cruzadas en diagonal sobre la espalda, cada una un poco
                // mas arriba para no superponerse entre si.
                go.transform.localPosition = new Vector3(0f, 0.05f - i * 0.12f, -0.10f);
                // Pedido explicito: el arma real (no ya un cubo simetrico)
                // tiene el cañon apuntando en +Z y el cuerpo/mira hacia
                // +Y -- este giro la acuesta de canto contra la espalda,
                // en diagonal, en vez de dejarla apuntando hacia adelante
                // como quedaba con el angulo pensado para el cubo viejo.
                go.transform.localRotation = Quaternion.Euler(80f, 100f, 20f);

                // Pedido explicito: geometria real (no un cubo de color)
                // tambien en el arma colgada de la espalda -- mismos
                // prefabs de WeaponModels que usa el arma en la mano
                // (ArmaEnLaMano/WeaponHolder). Si falta el modelo (kind sin
                // prefab armado), cae al cubo de siempre como respaldo.
                var modelo = WeaponModels.Get(kind);
                if (modelo != null)
                {
                    var real = Instantiate(modelo, go.transform);
                    real.transform.localPosition = Vector3.zero;
                    real.transform.localRotation = Quaternion.identity;
                    // Escala pareja por largo natural (no un 0,85 fijo):
                    // asi la pesada y la pistola terminan con la MISMA
                    // presencia visual sobre la espalda, no con su tamaño
                    // real de mano (que las haria de tamaños absurdamente
                    // distintos ahi).
                    float factor = TargetBackLength / WeaponModels.NaturalLength(kind);
                    var escalaReal = new Vector3(factor / escalaPadre.x, factor / escalaPadre.y, factor / escalaPadre.z);
                    real.transform.localScale = escalaReal;
                    foreach (var r in real.GetComponentsInChildren<Renderer>())
                    {
                        r.shadowCastingMode = ShadowCastingMode.Off;
                        r.sharedMaterial = SafeMaterial.Create(spec.Color);
                    }
                }
                else
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = "Cubo";
                    cube.transform.SetParent(go.transform, false);
                    var col = cube.GetComponent<Collider>();
                    if (col != null) Destroy(col);
                    var rend = cube.GetComponent<Renderer>();
                    rend.sharedMaterial = SafeMaterial.Create(spec.Color);
                    rend.shadowCastingMode = ShadowCastingMode.Off;
                    var escala = spec.VisualScale * 0.85f;
                    cube.transform.localScale = new Vector3(escala.x / escalaPadre.x, escala.y / escalaPadre.y, escala.z / escalaPadre.z);
                }

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
