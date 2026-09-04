using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Pipeline de importacion del arte de Assets/ARTS.
    //
    // Es una herramienta y no un README con pasos a mano a proposito: el
    // arte se reimporta solo cada vez que alguien toca un FBX o cambia de
    // rama, y una configuracion que vive unicamente en los .meta se pierde
    // en silencio en cuanto alguien borra la carpeta Library. Aca queda
    // escrito QUE configuracion quiere el proyecto y por que, y se puede
    // volver a aplicar entera con un click.
    //
    // Tres cosas que el arte crudo NO trae y hay que ponerle:
    //
    //  1. ESCALA. Todo viene de Maya a ~1/31 del tamaño real (el soldado
    //     mide 0.055 unidades). Se corrige por importador, calculando el
    //     factor contra una altura objetivo, en vez de escalar el
    //     Transform en la escena: escalar el transform de un personaje con
    //     Animator descuadra las animaciones.
    //
    //  2. RIG. M_Soldado.fbx NO tiene esqueleto (un solo nodo con
    //     MeshRenderer): es la version para pintar en Substance. El que si
    //     esta riggeado es lego.fbx del Slim Shooter Pack -- misma malla
    //     (pCube1), mismas UVs, mismo set de texturas (lego_Material_*) y
    //     el esqueleto mixamo que comparten las 7 animaciones. Ese es el
    //     que se usa para los soldados; M_Soldado queda como pieza de
    //     exhibicion estatica.
    //
    //  3. CANALES. Substance exporta un MaskMap empaquetado
    //     (R=metallic, G=oclusion, B=detalle, A=smoothness). URP/Lit lee
    //     exactamente esos canales si se enchufa el MISMO archivo en
    //     _MetallicGlossMap (R + A) y en _OcclusionMap (G), con
    //     _SmoothnessTextureChannel en 0 (alpha del metallic). Por eso el
    //     mask va dos veces y en lineal, no en sRGB.
    public static class ArtSetup
    {
        const string ArtRoot = "Assets/ARTS";
        const string SpArte = ArtRoot + "/SP_Arte";
        const string Pack = ArtRoot + "/Slim Shooter Pack";
        const string MatDir = "Assets/_Project/Materials/Arte";
        const string PrefabDir = "Assets/_Project/Prefabs/Arte";
        const string AnimDir = "Assets/_Project/Animation";

        // El modelo riggeado que de verdad se anima. Ver el comentario 2.
        public const string SoldadoRig = Pack + "/lego.fbx";

        // Alturas objetivo en metros. Son decisiones de diseño, no datos
        // del FBX: el arte no trae escala util (ver el comentario 1).
        struct Prop
        {
            public string fbx;
            public string texturas;   // carpeta con BaseMap/MaskMap/Normal
            public string material;   // nombre del material a crear
            public float alturaM;
        }

        static readonly Prop[] Props =
        {
            new Prop { fbx = SpArte + "/M_Arbol 1.fbx",    texturas = SpArte + "/Arbol 1",   material = "MAT_Arbol1",    alturaM = 5.5f },
            new Prop { fbx = SpArte + "/M_Arbol 2.fbx",    texturas = SpArte + "/Arbol 1",   material = "MAT_Arbol1",    alturaM = 4.8f },
            new Prop { fbx = SpArte + "/M_Arbol 3.fbx",    texturas = SpArte + "/Arbol 3",   material = "MAT_Arbol3",    alturaM = 7.0f },
            new Prop { fbx = SpArte + "/M_Barricada.fbx",  texturas = SpArte + "/Barricada", material = "MAT_Barricada", alturaM = 1.1f },
            new Prop { fbx = SpArte + "/M_Barril 1.fbx",   texturas = SpArte + "/Barril",    material = "MAT_Barril",    alturaM = 1.0f },
            new Prop { fbx = SpArte + "/M_Soldado.fbx",    texturas = SpArte + "/Soldado",   material = "MAT_Soldado",   alturaM = 1.75f },
            new Prop { fbx = SoldadoRig,                   texturas = SpArte + "/Soldado",   material = "MAT_Soldado",   alturaM = 1.75f },
        };

        // Las que se reproducen en bucle. "firing rifle" tambien: el
        // soldado dispara en rafagas mientras el gatillo siga apretado, y
        // una animacion de disparo que corre una sola vez se congela en el
        // ultimo frame apenas la segunda bala sale del caño.
        static readonly string[] Animaciones =
        {
            "walking", "rifle run", "rifle aiming idle", "firing rifle", "reloading", "strafe", "strafe (2)"
        };

        static readonly HashSet<string> NoLoop = new HashSet<string> { "reloading" };

        [MenuItem("Strategic Point/Arte/1. Configurar importacion y materiales")]
        public static void ConfigurarTodo()
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                ConfigurarTexturas();
                var materiales = CrearMateriales();
                ConfigurarModelos(materiales);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // FUERA del bloque de edicion por lotes, y no es un detalle:
            // dentro de StartAssetEditing los SaveAndReimport quedan en
            // cola, asi que lego.fbx TODAVIA no genero su Avatar y las
            // animaciones no tendrian de donde copiarlo. Antes esto fallaba
            // en silencio -- las 7 animaciones quedaban en Generic sin
            // avatar y el Animator no reproducia nada.
            ConfigurarAnimaciones();
            AssetDatabase.SaveAssets();
            Debug.Log("[ArtSetup] Importacion y materiales configurados.");
        }

        // ------------------------------------------------------------------
        // Texturas
        // ------------------------------------------------------------------
        // El color base va en sRGB; el mask y el normal NO. Un mask leido
        // como sRGB devuelve metallic/AO/smoothness con la curva de gamma
        // aplicada: no falla, no avisa, solo deja todo mate y sucio.
        static void ConfigurarTexturas()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;

                bool esNormal = path.EndsWith("_Normal.png");
                bool esMask = path.EndsWith("_MaskMap.png");
                bool esBase = path.EndsWith("_BaseMap.png");
                if (!esNormal && !esMask && !esBase) continue; // capturas de Substance, trimsheets: no se tocan

                bool cambio = false;
                var tipo = esNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (imp.textureType != tipo) { imp.textureType = tipo; cambio = true; }

                bool srgb = esBase;
                if (!esNormal && imp.sRGBTexture != srgb) { imp.sRGBTexture = srgb; cambio = true; }

                if (cambio) imp.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------
        // Materiales
        // ------------------------------------------------------------------
        static Dictionary<string, Material> CrearMateriales()
        {
            Directory.CreateDirectory(MatDir);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogError("[ArtSetup] No aparece el shader URP/Lit."); return new Dictionary<string, Material>(); }

            var creados = new Dictionary<string, Material>();
            foreach (var p in Props)
            {
                if (creados.ContainsKey(p.material)) continue;

                string ruta = MatDir + "/" + p.material + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, ruta); }
                mat.shader = shader;

                var baseMap = BuscarTextura(p.texturas, "_BaseMap");
                var mask = BuscarTextura(p.texturas, "_MaskMap");
                var normal = BuscarTextura(p.texturas, "_Normal");

                if (baseMap != null) { mat.SetTexture("_BaseMap", baseMap); mat.SetColor("_BaseColor", Color.white); }

                if (normal != null)
                {
                    mat.SetTexture("_BumpMap", normal);
                    mat.SetFloat("_BumpScale", 1f);
                    mat.EnableKeyword("_NORMALMAP");
                }

                // El mismo archivo dos veces, a proposito: URP lee R+A de
                // uno y G del otro. Ver el comentario 3 de la clase.
                if (mask != null)
                {
                    mat.SetTexture("_MetallicGlossMap", mask);
                    mat.SetTexture("_OcclusionMap", mask);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    mat.EnableKeyword("_OCCLUSIONMAP");
                    mat.SetFloat("_SmoothnessTextureChannel", 0f); // 0 = alpha del metallic
                    mat.SetFloat("_Metallic", 1f);                 // el mapa manda; el escalar solo lo multiplica
                    mat.SetFloat("_Smoothness", 1f);
                    mat.SetFloat("_OcclusionStrength", 1f);
                }
                else
                {
                    mat.SetFloat("_Metallic", 0f);
                    mat.SetFloat("_Smoothness", 0.25f);
                }

                EditorUtility.SetDirty(mat);
                creados[p.material] = mat;
            }
            return creados;
        }

        static Texture BuscarTextura(string carpeta, string sufijo)
        {
            if (!Directory.Exists(carpeta)) return null;
            foreach (var f in Directory.GetFiles(carpeta, "*" + sufijo + ".png"))
                return AssetDatabase.LoadAssetAtPath<Texture>(f.Replace('\\', '/'));
            return null;
        }

        // ------------------------------------------------------------------
        // Modelos
        // ------------------------------------------------------------------
        static void ConfigurarModelos(Dictionary<string, Material> materiales)
        {
            foreach (var p in Props)
            {
                var imp = AssetImporter.GetAtPath(p.fbx) as ModelImporter;
                if (imp == null) { Debug.LogWarning("[ArtSetup] No se encontro " + p.fbx); continue; }

                // useFileScale = false ES EL ARREGLO, no una preferencia.
                // Con las unidades del archivo activas, la malla salia del
                // tamaño pedido pero el AVATAR humanoide se generaba contra
                // otra escala: Animator.humanScale daba 86.76 en un
                // personaje de 1.75 m, y al aplicar el primer clip el
                // esqueleto explotaba (cabeza en y=65, pie en y=-59) y el
                // soldado desaparecia de pantalla. Apagando las unidades
                // del archivo, la escala pasa a estar en un solo lugar
                // -- globalScale -- y malla y avatar quedan de acuerdo.
                if (imp.useFileScale)
                {
                    imp.useFileScale = false;
                    imp.globalScale = 1f;
                    imp.SaveAndReimport();
                }

                imp.globalScale = FactorDeEscala(p.fbx, imp.globalScale, p.alturaM);
                imp.importNormals = ModelImporterNormals.Import;
                imp.importTangents = ModelImporterTangents.CalculateMikk;
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

                // Solo el rig del soldado lleva esqueleto humanoide. Los
                // props no tienen animacion ni huesos: dejarlos en Generic
                // les crea un Avatar vacio que no usa nadie.
                imp.importAnimation = false;

                if (materiales.TryGetValue(p.material, out var mat) && mat != null)
                    foreach (var nombre in NombresDeMaterial(p.fbx))
                        imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), nombre), mat);

                if (p.fbx == SoldadoRig)
                {
                    // El avatar se TIRA y se vuelve a generar, en dos
                    // pasadas. Un avatar ya existente sobrevive a un cambio
                    // de escala sin regenerarse -- se queda con las
                    // proporciones del import anterior -- y eso es
                    // exactamente lo que dejaba al esqueleto peleado con su
                    // propia malla.
                    imp.animationType = ModelImporterAnimationType.Generic;
                    imp.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                    imp.SaveAndReimport();

                    imp.animationType = ModelImporterAnimationType.Human;
                    imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                }
                else
                {
                    imp.animationType = ModelImporterAnimationType.None;
                }

                imp.SaveAndReimport();
            }
        }

        // El factor nuevo es el actual reescalado por (objetivo / actual):
        // asi es idempotente -- volver a correr la herramienta con el
        // modelo ya en su tamaño no lo mueve.
        static float FactorDeEscala(string fbx, float escalaActual, float alturaObjetivo)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (go == null) return escalaActual;

            // Bounds COMBINADOS y no el maximo de cada renderer: la
            // barricada son 24 tablones sueltos y el arbol 3 son copa y
            // tronco por separado. Midiendo renderer por renderer, la
            // "altura" del modelo era la del tablon mas alto -- unos
            // centimetros -- y el factor salia disparado: la barricada
            // terminaba de 32 metros de largo dentro de la escena mientras
            // el informe juraba que medina 1,10.
            float alto = AlturaCombinada(go);
            if (alto < 0.0001f) return escalaActual;

            return escalaActual * (alturaObjetivo / alto);
        }

        static float AlturaCombinada(GameObject go)
        {
            bool primero = true;
            Bounds b = default;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (primero) { b = r.bounds; primero = false; }
                else b.Encapsulate(r.bounds);
            }
            return primero ? 0f : b.size.y;
        }

        static IEnumerable<string> NombresDeMaterial(string fbx)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx))
                if (o is Material) yield return o.name;
        }

        // ------------------------------------------------------------------
        // Animaciones
        // ------------------------------------------------------------------
        // Todas comparten el esqueleto de lego.fbx, asi que copian SU
        // avatar en vez de generar uno propio: dos avatares humanoides
        // creados por separado sobre el mismo esqueleto no son el mismo
        // avatar, y el retargeting entre ellos introduce deriva.
        static void ConfigurarAnimaciones()
        {
            var rigImp = AssetImporter.GetAtPath(SoldadoRig) as ModelImporter;
            var avatar = AssetDatabase.LoadAllAssetsAtPath(SoldadoRig);
            Avatar srcAvatar = null;
            foreach (var o in avatar) if (o is Avatar a) srcAvatar = a;
            if (srcAvatar == null) { Debug.LogError("[ArtSetup] lego.fbx no genero Avatar; revisa el rig."); return; }

            float escalaRig = rigImp != null ? rigImp.globalScale : 1f;

            foreach (var nombre in Animaciones)
            {
                string ruta = Pack + "/" + nombre + ".fbx";
                var imp = AssetImporter.GetAtPath(ruta) as ModelImporter;
                if (imp == null) { Debug.LogWarning("[ArtSetup] Falta " + ruta); continue; }

                imp.useFileScale = false;
                imp.globalScale = escalaRig;
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                imp.sourceAvatar = srcAvatar;
                imp.importAnimation = true;
                imp.materialImportMode = ModelImporterMaterialImportMode.None;

                var clips = imp.defaultClipAnimations;
                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i].name = nombre;
                    clips[i].loopTime = !NoLoop.Contains(nombre);
                    // Sin esto la animacion arrastra al personaje: el juego
                    // mueve el transform por su cuenta (SoldierMotor) y la
                    // raiz de la animacion pelearia contra el.
                    clips[i].lockRootRotation = true;
                    clips[i].keepOriginalOrientation = true;
                    clips[i].lockRootHeightY = true;
                    clips[i].keepOriginalPositionY = true;
                    clips[i].lockRootPositionXZ = true;
                    clips[i].keepOriginalPositionXZ = false;
                }
                imp.clipAnimations = clips;
                imp.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------
        // Informe
        // ------------------------------------------------------------------
        [MenuItem("Strategic Point/Arte/Informe")]
        public static void Informe() => Debug.Log(InformeTexto());

        public static string InformeTexto()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in Props)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p.fbx);
                if (go == null) { sb.AppendLine(p.fbx + ": NO EXISTE"); continue; }

                float alto = AlturaCombinada(go);
                var vistos = new HashSet<string>();
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials) vistos.Add(m != null ? m.name : "NULO");
                string mats = string.Join(", ", vistos);
                sb.AppendLine(Path.GetFileNameWithoutExtension(p.fbx) + ": alto=" + alto.ToString("F2") + " m | materiales: " + mats);
            }

            foreach (var nombre in Animaciones)
            {
                string ruta = Pack + "/" + nombre + ".fbx";
                int n = 0; float dur = 0f; bool loop = false;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ruta))
                    if (o is AnimationClip c && !c.name.StartsWith("__preview__")) { n++; dur = c.length; loop = c.isLooping; }
                sb.AppendLine("anim " + nombre + ": clips=" + n + " dur=" + dur.ToString("F2") + "s loop=" + loop);
            }
            return sb.ToString();
        }
    }
}
