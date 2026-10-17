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
                var mat = def.Enemigo ? MaterialEnemigoVisible() : MaterialAliado();
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

        // Bugs #168/#175 ("sigo sin ver al enemigo"): de noche el trimsheet rojo oscuro se pierde contra el fondo. En vez de iluminar al que
        // dispara (luz que quedaba sobre el cadaver, bug #176) el enemigo lleva un brillo propio tenue: se lo distingue siempre, sin luces sueltas.
        static Material matEnemigoVisible;
        static Material MaterialEnemigoVisible()
        {
            if (matEnemigoVisible != null) return matEnemigoVisible;
            if (matEnemigo == null) return null;
            matEnemigoVisible = new Material(matEnemigo) { name = "MAT_Trimsheet_Enemigo_Brillo" };
            Teñir(matEnemigoVisible, TinteEnemigo);
            if (matEnemigoVisible.HasProperty("_EmissionColor"))
            {
                matEnemigoVisible.EnableKeyword("_EMISSION");
                // Bug #194 ("se rompieron las esteticas de los enemigos"): un brillo plano los dejaba como una silueta lisa. La emision usa la
                // MISMA textura del trimsheet (aclara el dibujo sin borrarlo) y un color tenue.
                if (matEnemigoVisible.HasProperty("_BaseMap") && matEnemigoVisible.GetTexture("_BaseMap") != null && matEnemigoVisible.HasProperty("_EmissionMap"))
                    matEnemigoVisible.SetTexture("_EmissionMap", matEnemigoVisible.GetTexture("_BaseMap"));
                matEnemigoVisible.SetColor("_EmissionColor", new Color(0.2f, 0.2f, 0.2f));
                matEnemigoVisible.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return matEnemigoVisible;
        }

        // Pedido de pulido: "distinguir enemigos de aliados, mejorar el codigo de colores". Un solo criterio: ALIADO = frio (azul),
        // ENEMIGO = calido (rojo). Se multiplica sobre el trimsheet, asi no se pierde el dibujo de cada clase.
        public static readonly Color TinteAliado = new Color(0.30f, 0.62f, 1.00f);
        public static readonly Color TinteEnemigo = new Color(1.00f, 0.52f, 0.46f);

        static void Teñir(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        static Texture2D RojoAAzul(Texture2D src)
        {
            if (!Application.isPlaying) return null;
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previa = RenderTexture.active;
            Texture2D res;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                res = new Texture2D(src.width, src.height, TextureFormat.RGBA32, true) { name = src.name + "_Aliado", hideFlags = HideFlags.HideAndDontSave };
                res.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            }
            finally { RenderTexture.active = previa; RenderTexture.ReleaseTemporary(rt); }
            var px = res.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var p = px[i];
                if (p.r > 70 && p.r > p.g * 1.5f && p.r > p.b * 1.5f) { byte r = p.r; p.r = p.b; p.b = r; px[i] = p; }
            }
            res.SetPixels32(px);
            res.Apply(true, false);
            return res;
        }

        static Material matAliadoTenido;
        static Material MaterialAliado()
        {
            if (matAliadoTenido != null) return matAliadoTenido;
            if (matAliado == null) return null;
            matAliadoTenido = new Material(matAliado) { name = "MAT_Trimsheet_Aliado_Azul" };
            Teñir(matAliadoTenido, TinteAliado);
            // Los rojos del trimsheet (camisa de Kes) se confundian con el enemigo: se cambian rojo<->azul en una copia de la textura.
            if (matAliadoTenido.HasProperty("_BaseMap") && matAliadoTenido.GetTexture("_BaseMap") is Texture2D tx)
            {
                var azul = RojoAAzul(tx);
                if (azul != null) matAliadoTenido.SetTexture("_BaseMap", azul);
            }
            // Brillo frio tenue (el mismo recurso que ya usa el enemigo, pero azul): de noche el aliado se lee azul aunque vista rojo.
            if (matAliadoTenido.HasProperty("_EmissionColor"))
            {
                matAliadoTenido.EnableKeyword("_EMISSION");
                if (matAliadoTenido.HasProperty("_BaseMap") && matAliadoTenido.GetTexture("_BaseMap") != null && matAliadoTenido.HasProperty("_EmissionMap"))
                    matAliadoTenido.SetTexture("_EmissionMap", matAliadoTenido.GetTexture("_BaseMap"));
                matAliadoTenido.SetColor("_EmissionColor", new Color(0.05f, 0.14f, 0.34f));
                matAliadoTenido.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return matAliadoTenido;
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
