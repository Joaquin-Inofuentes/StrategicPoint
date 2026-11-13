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
                var mat = def.Nombre == "PESADO" ? MaterialPesado() : def.Nombre == "ESCUDERO" ? MaterialEscudero() : def.Enemigo ? MaterialEnemigoVisible() : (def.Nombre == "MEDICO" ? MaterialMedico() : def.Nombre == "ASALTO" ? MaterialAsalto() : def.Nombre == "FRANCOTIRADOR" ? MaterialFrancotirador() : MaterialAliado());
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
            if (s.Role == RoleType.Civilian || def.Nombre == "PERRO") OcultarArma();
            if (def.Nombre == "PERRO" && smr != null) smr.enabled = false;   // el perro lleva su propio modelo (PerroEnemigo)
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
            // Etapa A del rediseño de objetivos: el tinte rojo multiplicaba un trimsheet azul/verde y a 10 m el enemigo
            // se leia igual que el aliado. Los azules y verdes pasan a rojo oxido oscuro en una copia de la textura.
            if (matEnemigoVisible.HasProperty("_BaseMap") && matEnemigoVisible.GetTexture("_BaseMap") is Texture2D txE)
            {
                var oxido = AzulYVerdeAOxido(txE);
                if (oxido != null) { matEnemigoVisible.SetTexture("_BaseMap", oxido); Teñir(matEnemigoVisible, new Color(1f, 0.78f, 0.7f)); }
            }
            if (matEnemigoVisible.HasProperty("_EmissionColor"))
            {
                matEnemigoVisible.EnableKeyword("_EMISSION");
                // Bug #194 ("se rompieron las esteticas de los enemigos"): un brillo plano los dejaba como una silueta lisa. La emision usa la
                // MISMA textura del trimsheet (aclara el dibujo sin borrarlo) y un color tenue.
                if (matEnemigoVisible.HasProperty("_BaseMap") && matEnemigoVisible.GetTexture("_BaseMap") != null && matEnemigoVisible.HasProperty("_EmissionMap"))
                    matEnemigoVisible.SetTexture("_EmissionMap", matEnemigoVisible.GetTexture("_BaseMap"));
                // Acento rojo tenue (antes gris): la familia calida se lee tambien de noche.
                matEnemigoVisible.SetColor("_EmissionColor", new Color(0.30f, 0.06f, 0.04f));
                matEnemigoVisible.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return matEnemigoVisible;
        }

        // Pedido de pulido: "distinguir enemigos de aliados, mejorar el codigo de colores". Un solo criterio: ALIADO = frio (azul),
        // ENEMIGO = calido (rojo). Se multiplica sobre el trimsheet, asi no se pierde el dibujo de cada clase.
        public static readonly Color TinteAliado = new Color(0.30f, 0.62f, 1.00f);
        public static readonly Color TinteEnemigo = new Color(1.00f, 0.30f, 0.24f);

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
                // El verde militar (casco, pantalon) tambien pasa a azul: aliado y enemigo vestian el mismo verde.
                else if (p.g > 40 && p.g > p.r * 1.15f && p.g > p.b * 1.15f) { byte g = p.g; p.g = p.b; p.b = g; px[i] = p; }
            }
            res.SetPixels32(px);
            res.Apply(true, false);
            return res;
        }

        // Copia del trimsheet para el enemigo: lo azul o verde se vuelve rojo oxido oscuro (la luminosidad se conserva, asi
        // el dibujo de cada clase sigue legible); los rojos y los neutros (negro, blanco) quedan como estan.
        static Texture2D AzulYVerdeAOxido(Texture2D src)
        {
            var copia = LeerCopia(src, "_Enemigo");
            if (copia == null) return null;
            var px = copia.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var p = px[i];
                bool azul = p.b > 40 && p.b > p.r * 1.15f && p.b >= p.g;
                bool verde = p.g > 40 && p.g > p.r * 1.15f && p.g > p.b;
                if (!azul && !verde) continue;
                float lum = (0.3f * p.r + 0.59f * p.g + 0.11f * p.b) / 255f;
                p.r = (byte)Mathf.Clamp(lum * 255f * 1.5f, 0f, 255f);
                p.g = (byte)Mathf.Clamp(lum * 255f * 0.42f, 0f, 255f);
                p.b = (byte)Mathf.Clamp(lum * 255f * 0.34f, 0f, 255f);
                px[i] = p;
            }
            copia.SetPixels32(px);
            copia.Apply(true, false);
            return copia;
        }

        static Texture2D LeerCopia(Texture2D src, string sufijo)
        {
            if (!Application.isPlaying) return null;
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previa = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                var res = new Texture2D(src.width, src.height, TextureFormat.RGBA32, true) { name = src.name + sufijo, hideFlags = HideFlags.HideAndDontSave };
                res.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
                return res;
            }
            finally { RenderTexture.active = previa; RenderTexture.ReleaseTemporary(rt); }
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

        // Bug #270 ("el material se rompio, al soldado le falta algo"): con el tinte azul pleno el uniforme blanco del medico quedaba un azul lavado
        // y las cruces se perdian. El medico lleva un tinte mas suave: blanco azulado con las cruces azules bien marcadas.
        static Material matMedico;
        static Material MaterialMedico()
        {
            if (matMedico != null) return matMedico;
            var baseAzul = MaterialAliado();
            if (baseAzul == null) return null;
            matMedico = new Material(baseAzul) { name = "MAT_Trimsheet_Aliado_Medico" };
            Teñir(matMedico, new Color(0.78f, 0.88f, 1.00f));
            if (matMedico.HasProperty("_EmissionColor")) matMedico.SetColor("_EmissionColor", new Color(0.02f, 0.05f, 0.12f));
            return matMedico;
        }

        // Asalto y francotirador se confundian (mismo azul, solo cambiaba la malla) y la version por familias de color los dejo como dos
        // manchas lisas ("se ve medio roto"). El trimsheet es una paleta de 8x8 celdas de 128 px y cada pieza del soldado usa UNA celda
        // (uniforme, chaleco/casco, pantalon, correaje...): en vez de teñir pixeles se cambia el color de las celdas de cada clase en una copia
        // de la paleta. Asi cada pieza conserva su color propio y la silueta se lee: ASALTO = uniforme azul vivo con chaleco/casco dorado,
        // FRANCOTIRADOR = musgo oscuro con capucha casi negra. Los dos llevan correaje azul claro (identifica al bando aliado).
        // La celda c = columna + 8 * fila contando desde ABAJO (igual que el UV y que Texture2D.GetPixels).
        static Material matAsalto, matFrancotirador;
        static readonly Color AzulBando = new Color(0.30f, 0.65f, 1.00f);
        static Material MaterialPorCeldas(ref Material cache, string nombre, (int celda, Color color)[] celdas, Color emision)
        {
            if (cache != null) return cache;
            if (matAliado == null) return null;
            cache = new Material(matAliado) { name = nombre };
            Teñir(cache, Color.white);
            if (matAliado.HasProperty("_BaseMap") && matAliado.GetTexture("_BaseMap") is Texture2D tx)
            {
                var copia = LeerCopia(tx, "_" + nombre);
                if (copia != null)
                {
                    int lado = copia.width / 8;
                    foreach (var (celda, color) in celdas)
                    {
                        int x0 = (celda % 8) * lado, y0 = (celda / 8) * lado;
                        var bloque = new Color[lado * lado];
                        for (int i = 0; i < bloque.Length; i++) bloque[i] = color;
                        copia.SetPixels(x0, y0, lado, lado, bloque);
                    }
                    copia.Apply(true, false);
                    cache.SetTexture("_BaseMap", copia);
                    if (cache.HasProperty("_MainTex")) cache.SetTexture("_MainTex", copia);
                    // Brillo tenue con la misma paleta (de noche no se apaga a negro): proporcional a cada color, sin teñir todo parejo.
                    if (cache.HasProperty("_EmissionMap")) cache.SetTexture("_EmissionMap", copia);
                }
            }
            if (cache.HasProperty("_EmissionColor")) { cache.EnableKeyword("_EMISSION"); cache.SetColor("_EmissionColor", emision); cache.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            return cache;
        }
        static Material MaterialAsalto() => MaterialPorCeldas(ref matAsalto, "MAT_Trimsheet_Aliado_Asalto", new[]
        {
            (8, new Color(0.15f, 0.40f, 0.90f)),    // uniforme: azul vivo
            (52, new Color(0.94f, 0.72f, 0.18f)),   // chaleco y casco: dorado
            (19, new Color(0.13f, 0.17f, 0.26f)),   // pantalon: azul pizarra
            (32, AzulBando), (33, AzulBando),       // correaje de bando
        }, new Color(0.22f, 0.22f, 0.26f));
        // Pesado (2026-11-13): violeta profundo con chaleco/casco de acero casi negro y correaje magenta; ya no se parece a un enemigo comun (rojo) ni a un aliado (azul).
        static Material matPesado, matEscudero;
        static Material MaterialPesado() => MaterialPorCeldas(ref matPesado, "MAT_Trimsheet_Pesado", new[]
        {
            // Mesh_Pesado usa sus propias celdas (medidas por altura de los vertices): 12 torso y brazos, 34 piernas, 25 chaleco, 24 casco, 38/1 correaje y botas
            (12, new Color(0.45f, 0.13f, 0.72f)),   // uniforme: violeta
            (34, new Color(0.19f, 0.08f, 0.30f)),   // pantalon: violeta oscuro
            (25, new Color(0.13f, 0.11f, 0.18f)),   // chaleco blindado: acero casi negro
            (24, new Color(0.17f, 0.15f, 0.22f)),   // casco
            (38, new Color(0.10f, 0.09f, 0.13f)),   // guantes y correaje
            (1, new Color(0.14f, 0.09f, 0.20f)),    // botas y piezas varias
            (28, new Color(0.95f, 0.15f, 0.62f)), (29, new Color(0.95f, 0.15f, 0.62f)), (46, new Color(0.95f, 0.15f, 0.62f)), (60, new Color(0.30f, 0.12f, 0.45f)),   // correaje de bando: magenta
        }, new Color(0.16f, 0.04f, 0.26f));
        // Escudero (2026-11-13): grafito con casco y chaleco plateados (antidisturbios).
        static Material MaterialEscudero() => MaterialPorCeldas(ref matEscudero, "MAT_Trimsheet_Escudero", new[]
        {
            (8, new Color(0.16f, 0.17f, 0.20f)), (10, new Color(0.16f, 0.17f, 0.20f)), (12, new Color(0.16f, 0.17f, 0.20f)),   // uniforme: grafito
            (52, new Color(0.80f, 0.84f, 0.88f)),   // casco y chaleco: plata
            (19, new Color(0.09f, 0.10f, 0.12f)),   // pantalon: negro
            (32, new Color(0.95f, 0.75f, 0.1f)), (33, new Color(0.95f, 0.75f, 0.1f)),     // correaje: amarillo
        }, new Color(0.10f, 0.10f, 0.12f));
        static Material MaterialFrancotirador() => MaterialPorCeldas(ref matFrancotirador, "MAT_Trimsheet_Aliado_Francotirador", new[]
        {
            (10, new Color(0.20f, 0.31f, 0.17f)),   // uniforme: musgo
            (12, new Color(0.20f, 0.31f, 0.17f)),   // (el flanqueador usa la misma malla con otra celda de uniforme)
            (52, new Color(0.07f, 0.09f, 0.06f)),   // capucha / boina: casi negra
            (19, new Color(0.27f, 0.21f, 0.12f)),   // pantalon: tierra
            (32, AzulBando), (33, AzulBando),
        }, new Color(0.22f, 0.22f, 0.24f));

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
