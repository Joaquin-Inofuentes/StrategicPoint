using UnityEngine;
using SP.Combat;

namespace SP.Actors
{
    // Viste al soldado segun su clase: cambia la malla del SkinnedMeshRenderer
    // por la variante armada por SoldierVariantBuilder (mismo esqueleto, mismo
    // Animator), le pone el material del trimsheet (teñido de rojo si es
    // enemigo) y le da el loadout de la clase.
    //
    // Se aplica en Start (no en Awake): asi corre despues de Soldier.Configure,
    // que los spawners llaman recien instanciado.
    [DisallowMultipleComponent]
    public class SoldierLook : MonoBehaviour
    {
        public bool Aplicado { get; private set; }
        public string Clase { get; private set; }

        static Material matAliado, matEnemigo;

        void Start() => Aplicar();

        public void Aplicar()
        {
            var s = GetComponent<Soldier>();
            if (s == null) return;
            var def = SoldierClasses.Para(s);
            Clase = def.Nombre;

            var smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
            var malla = Resources.Load<Mesh>("Soldados/Mesh_" + def.Malla);
            if (smr != null && malla != null)
            {
                if (matAliado == null) matAliado = Resources.Load<Material>("Soldados/MAT_Trimsheet_Aliado");
                if (matEnemigo == null) matEnemigo = Resources.Load<Material>("Soldados/MAT_Trimsheet_Enemigo");
                var mat = def.Enemigo ? matEnemigo : matAliado;
                smr.sharedMesh = malla;
                if (mat != null) smr.sharedMaterial = mat;
                smr.localBounds = malla.bounds;
            }

            var w = s.Weapon;
            if (w != null && def.Loadout.Length > 0)
            {
                w.Loadout.Clear();
                w.Loadout.AddRange(def.Loadout);
                w.EquipFromLoadout(0);
            }
            Aplicado = true;
        }
    }
}
