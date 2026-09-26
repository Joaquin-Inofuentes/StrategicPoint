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
            var malla = SP.Core.RecursosCache.Cargar<Mesh>("Soldados/Mesh_" + def.Malla);
            if (smr != null && malla != null)
            {
                if (matAliado == null) matAliado = SP.Core.RecursosCache.Cargar<Material>("Soldados/MAT_Trimsheet_Aliado");
                if (matEnemigo == null) matEnemigo = SP.Core.RecursosCache.Cargar<Material>("Soldados/MAT_Trimsheet_Enemigo");
                var mat = def.Enemigo ? matEnemigo : matAliado;
                if (s.Role == RoleType.Civilian) mat = MaterialCivil();
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
            if (s.Role == RoleType.Civilian) OcultarArma();
            Aplicado = true;
        }

        static Material matCivil;
        static Material MaterialCivil()
        {
            if (matCivil != null) return matCivil;
            if (matAliado == null) return null;
            matCivil = new Material(matAliado) { name = "MAT_Civil" };
            var tinte = new Color(0.2f, 0.6f, 1.0f); // Azul claro para distinguirlo
            if (matCivil.HasProperty("_BaseColor")) matCivil.SetColor("_BaseColor", tinte);
            if (matCivil.HasProperty("_Color")) matCivil.SetColor("_Color", tinte);
            return matCivil;
        }

        // El civil no lleva arma: se apagan todos los modelos de arma del cuerpo.
        void OcultarArma()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is SkinnedMeshRenderer) continue;
                r.enabled = false;
            }
        }
    }
}
