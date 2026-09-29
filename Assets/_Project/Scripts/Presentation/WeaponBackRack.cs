using System.Collections;
using System.Collections.Generic;
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
        WeaponKind[] AllKinds = { WeaponKind.Rifle, WeaponKind.Pistol, WeaponKind.Heavy };

        // A que largo se ve CUALQUIER arma real sobre la espalda, sin
        // importar su tamaño natural -- ver el comentario de
        // WeaponModels.NaturalLength sobre el bug que esto corrige (la
        // pesada, de 1,12 m de largo, quedaba casi tan larga como el
        // soldado con la escala plana de antes).
        const float TargetBackLength = 0.5f;
        const float SeparacionDelCuerpo = 0.015f, SeparacionEntreArmas = 0.05f, MargenLateral = 0.04f, AlturaDeEspalda = 0.45f;

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

            // El loadout depende de la clase: se cuelgan las armas que de verdad tiene.
            if (holder != null && holder.Loadout.Count > 0) AllKinds = holder.Loadout.ToArray();
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
                // BUG REAL que esto corrige: con 80 grados de pitch el
                // cañon (eje +Z local) quedaba casi vertical -- el arma
                // apuntaba para arriba y se salia por encima de la cabeza
                // en vez de quedar acostada en diagonal contra la espalda.
                // Medido en juego con captura de la Scene View (mismo
                // metodo que el resto de la sesion: nunca solo mirar
                // numeros). 35 grados la acuesta mucho mas plana.
                go.transform.localRotation = Quaternion.Euler(35f, 100f, 20f);

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
                    // El modelo real ya trae el material del trimsheet
                    // (WeaponPrefabBuilder); antes se lo pisaba con un
                    // SafeMaterial.Create(spec.Color) por renderer -- mismo
                    // problema que ArmaEnLaMano/WeaponHolder: tapaba la
                    // textura real y clonaba un Material nuevo por arma por
                    // soldado. Se deja solo apagar la sombra.
                    foreach (var r in real.GetComponentsInChildren<Renderer>())
                        r.shadowCastingMode = ShadowCastingMode.Off;
                }
                else
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = "Cubo";
                    cube.transform.SetParent(go.transform, false);
                    // Mismo bug de Destroy()-en-Edit-mode que WeaponHolder/
                    // PatrolRouteLine (ver sus comentarios): en Edit mode no
                    // borra nada y deja un collider vivo colgando de la
                    // espalda. Este camino en particular casi nunca corre
                    // (las 3 armas del loadout tienen modelo real), pero si
                    // el dia de mañana falta un modelo, mejor que el
                    // respaldo tambien sea correcto.
                    var col = cube.GetComponent<Collider>();
                    if (col != null)
                    {
                        if (Application.isPlaying) Destroy(col);
                        else DestroyImmediate(col);
                    }
                    var rend = cube.GetComponent<Renderer>();
                    rend.sharedMaterial = SafeMaterial.Create(spec.Color);
                    rend.shadowCastingMode = ShadowCastingMode.Off;
                    var escala = spec.VisualScale * 0.85f;
                    cube.transform.localScale = new Vector3(escala.x / escalaPadre.x, escala.y / escalaPadre.y, escala.z / escalaPadre.z);
                }

                slots[i] = go.transform;
            }

            SepararDelCuerpo();
            colgado = true;
        }

        // Pedido explicito: "revisa que las armas de la espalda no solapen el cuerpo". Un offset/giro fijo en el hueso del pecho
        // dejaba las armas metidas en la chaqueta o clavadas hacia atras segun el cuerpo. Con el cuerpo ya en pose se acuesta
        // cada arma PLANA contra la espalda (el largo sobre el plano del torso, en diagonal, la mira hacia afuera) y se la
        // apoya justo detras de la cara trasera medida en los vertices horneados del cuerpo, en el espacio del soldado.
        void SepararDelCuerpo()
        {
            var raiz = transform;
            var cuerpo = new List<Vector3>();
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                var m = new Mesh();
                smr.BakeMesh(m);
                var mat = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                foreach (var v in m.vertices) cuerpo.Add(raiz.InverseTransformPoint(mat.MultiplyPoint3x4(v)));
                if (Application.isPlaying) Destroy(m); else DestroyImmediate(m);
            }
            if (cuerpo.Count == 0) return;

            // Inclinaciones (grados desde la vertical, hacia la derecha del soldado) para que dos o tres armas se crucen sin
            // pisarse, y su centro sobre la espalda.
            float[] inclinacion = { 28f, -28f, 0f };
            float[] lateral = { -0.05f, 0.05f, 0f };
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;
                int j = Mathf.Min(i, inclinacion.Length - 1);
                // El eje mas largo del modelo sube en diagonal por la espalda y el mas fino mira hacia atras (queda plana).
                var largo = Quaternion.AngleAxis(inclinacion[j], Vector3.back) * Vector3.up;
                slot.localRotation = Quaternion.identity;
                Vector3 ejeLargo, ejeFino;
                if (!EjesDelModelo(slot, out ejeLargo, out ejeFino)) continue;
                slot.rotation = raiz.rotation * Quaternion.LookRotation(largo, Vector3.back) * Quaternion.Inverse(Quaternion.LookRotation(ejeLargo, ejeFino));

                Vector3 min, max;
                if (!CajaEnEspacioDelSoldado(slot, out min, out max)) continue;
                var centro = (min + max) * 0.5f;
                var deseado = new Vector3(lateral[j], AlturaDeEspalda, centro.z);
                slot.position += raiz.TransformVector(deseado - centro);
                min += deseado - centro; max += deseado - centro;

                float zEspalda = float.MaxValue;
                foreach (var c in cuerpo)
                {
                    if (c.x < min.x - MargenLateral || c.x > max.x + MargenLateral || c.y < min.y - MargenLateral || c.y > max.y + MargenLateral) continue;
                    if (c.z < zEspalda) zEspalda = c.z;
                }
                if (zEspalda == float.MaxValue) continue;

                // Apoyada sobre la cara trasera, con el hueco justo; las que van cruzadas se separan un poco entre si.
                float invasion = max.z + SeparacionDelCuerpo + i * SeparacionEntreArmas - zEspalda;
                slot.position -= raiz.forward * invasion;
            }
        }

        // Ejes locales (del slot) mas largo y mas fino de la caja del arma; el modelo puede venir acostado en cualquier eje.
        bool EjesDelModelo(Transform slot, out Vector3 largo, out Vector3 fino)
        {
            largo = Vector3.forward; fino = Vector3.up;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            bool alguna = false;
            foreach (var mf in slot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var caja = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var esquina = caja.center + Vector3.Scale(caja.extents, new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f));
                    var p = slot.InverseTransformPoint(mf.transform.TransformPoint(esquina));
                    min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                    alguna = true;
                }
            }
            if (!alguna) return false;
            var tam = max - min;
            int iL = 0, iF = 0;
            for (int a = 1; a < 3; a++) { if (tam[a] > tam[iL]) iL = a; if (tam[a] < tam[iF]) iF = a; }
            if (iL == iF) iF = (iL + 1) % 3;
            largo = new Vector3(iL == 0 ? 1f : 0f, iL == 1 ? 1f : 0f, iL == 2 ? 1f : 0f);
            fino = new Vector3(iF == 0 ? 1f : 0f, iF == 1 ? 1f : 0f, iF == 2 ? 1f : 0f);
            return true;
        }

        // Las mallas de arma no son legibles (isReadable=false): se usa la caja de la malla, sus 8 esquinas, ya rotada el arma.
        bool CajaEnEspacioDelSoldado(Transform slot, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            bool alguna = false;
            foreach (var mf in slot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var caja = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var esquina = caja.center + Vector3.Scale(caja.extents, new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f));
                    var p = transform.InverseTransformPoint(mf.transform.TransformPoint(esquina));
                    min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                    alguna = true;
                }
            }
            return alguna;
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
