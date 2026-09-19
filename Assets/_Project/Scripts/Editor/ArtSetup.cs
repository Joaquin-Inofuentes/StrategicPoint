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
        //
        // "Todas las que hay" -- pedido explicito -- menos las que no
        // tienen ningun gatillo real en este juego, y usarlas igual seria
        // simular un movimiento que nunca pasa:
        //   * jump up/down/loop: no hay salto.
        //   * turn 90 left/right, crouching turn 90 left/right: el soldado
        //     gira de forma continua (SoldierMotor.LookTowards/RotateYaw),
        //     nunca en un giro discreto de 90 grados.
        //   * sprint *: una sola velocidad de movimiento en todo el juego
        //     (SoldierMotor.moveSpeed); no existe un segundo cambio de
        //     marcha que la dispare.
        //   * idle (la relajada, sin apuntar): el soldado SIEMPRE tiene el
        //     arma en la mano (ArmaEnLaMano se agrega solo), asi que el
        //     idle que se usa es siempre "rifle aiming idle".
        //   * strafe / strafe (2): ambiguas y sin usar desde que se
        //     agregaron; "walk left"/"walk right", con nombre claro, cubren
        //     lo mismo dentro del blend 2D de abajo.
        //   * lego.fbx (el rig en si) y M_Soldado.fbx (la pieza de
        //     exhibicion estatica): no son animaciones, ya se usan aparte
        //     (ver SoldadoRig y ArtBuilder.Definiciones).
        static readonly string[] Animaciones =
        {
            "walking", "rifle run", "rifle aiming idle", "firing rifle", "reloading",
            // Blend 2D de caminar/correr (layer 0, de pie): las 8 direcciones
            // de cada velocidad menos la de avance, que ya cubren "walking"
            // y "rifle run" de arriba.
            "walk backward", "walk left", "walk right",
            "walk forward left", "walk forward right", "walk backward left", "walk backward right",
            "run backward", "run left", "run right",
            "run forward left", "run forward right", "run backward left", "run backward right",
            // Agachado (layer 0, estado alterno): idle + las 8 direcciones
            // de caminar agachado que trae el pack.
            "idle crouching aiming",
            "walk crouching forward", "walk crouching backward", "walk crouching left", "walk crouching right",
            "walk crouching forward left", "walk crouching forward right",
            "walk crouching backward left", "walk crouching backward right",
            // Muerte: variantes elegidas al azar en CubeFxReactor.OnDeath.
            "death from the front", "death from the back", "death from right",
            "death from front headshot", "death from back headshot", "death crouching headshot front",
        };

        static readonly HashSet<string> NoLoop = new HashSet<string>
        {
            "reloading",
            "death from the front", "death from the back", "death from right",
            "death from front headshot", "death from back headshot", "death crouching headshot front",
        };

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
                    bool loopea = !NoLoop.Contains(nombre);
                    clips[i].loopTime = loopea;
                    // BUG REAL reportado por el usuario: "la animacion no
                    // termina bien" -- el ciclo pega un salto visible al
                    // reiniciar. loopTime por si solo (arriba) hace que
                    // Mecanim vuelva al frame 0 al llegar al final, pero NO
                    // ajusta nada para que el frame final y el frame inicial
                    // sean la MISMA pose -- si un export de Mixamo no cierra
                    // el ciclo exacto (lo habitual), el salto se nota. Loop
                    // Pose (loopPose) es justamente el algoritmo de Mecanim
                    // que empareja cierre y apertura del ciclo; estaba
                    // apagado (loopBlend:0) en los .meta de los 34 clips que
                    // administra este archivo, para ninguno se habia tocado
                    // nunca desde el importer.
                    clips[i].loopPose = loopea;
                    // Sin esto la animacion arrastra al personaje: el juego
                    // mueve el transform por su cuenta (SoldierMotor) y la
                    // raiz de la animacion pelearia contra el.
                    //
                    // BUG REAL medido por el usuario: con lockRootPositionXZ
                    // en true (que es "Root Transform Position (XZ): Bake
                    // Into Pose" tildado) el desplazamiento del ciclo de
                    // caminar queda HORNEADO en la propia pose -- la cadera
                    // avanza como parte de la animacion, no como root
                    // motion -- asi que anim.applyRootMotion = false (en
                    // ArtBuilder.MontarSoldados) no tiene NADA que
                    // interceptar y el modelo se desliza igual, separandose
                    // del collider que sí se queda quieto. En false, el
                    // desplazamiento se extrae como root motion (curvas
                    // RootT.x/RootT.z) en vez de hornearse en la pose, que
                    // es lo que applyRootMotion=false SI puede anular.
                    clips[i].lockRootRotation = true;
                    clips[i].keepOriginalOrientation = true;
                    clips[i].lockRootHeightY = true;
                    clips[i].heightFromFeet = true;
                    clips[i].keepOriginalPositionY = true;
                    clips[i].lockRootPositionXZ = false;
                    clips[i].keepOriginalPositionXZ = false;
                }
                imp.clipAnimations = clips;
                imp.SaveAndReimport();

                // Doble seguro, y no redundancia de mas: no hay forma de
                // confirmar sin Play mode que el toggle de arriba alcanza
                // por si solo (la semantica exacta de Bake Into Pose no se
                // puede probar por lectura de codigo). Esto anula el
                // desplazamiento horizontal en la fuente de verdad final --
                // las curvas de root motion del clip ya importado -- sin
                // depender de acertarle a ese toggle: si algun clip todavia
                // trae X/Z de root motion (horneado o extraido, cualquiera
                // haya sido), queda en cero aca sin excepcion.
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ruta))
                    if (o is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                        AnularDesplazamientoHorizontal(clip);
            }

            CorregirAlturaDeCadera();
        }

        // ------------------------------------------------------------------
        // Clips de SALTO (ronda 7)
        // ------------------------------------------------------------------
        // BUG REAL: "la animacion de saltar se rompe por el eje Y de base". Los
        // tres FBX del salto NUNCA estuvieron en la lista Animaciones: quedaron
        // importados como Generic a escala 1 (el rig del soldado es Humanoid a
        // 0,2948), o sea sin avatar compartido, sin las correcciones de
        // altura de cadera y sin lockRoot*. Medido en Play con los huesos del
        // Humanoid: en el aire la cadera quedaba ~1,1 m por DEBAJO de donde
        // esta de pie y el soldado se hundia en el piso durante todo el salto.
        //
        // El nombre del clip NO se cambia a proposito: el controlador ya lo
        // referencia por (guid, fileID) y el fileID sale del nombre.
        static readonly string[] ClipsDeSalto = { "jump up", "jump loop", "jump down" };

        // El vuelo lo pone SoldierMotor.Update (parabola a mano). El clip solo
        // tiene que poner la POSE: la altura absoluta de la cadera se calibra
        // aparte contra "walking" (ver CorregirAlturaDeSalto).
        [MenuItem("Strategic Point/Arte/1b. Configurar clips de salto")]
        public static void ConfigurarClipsDeSalto()
        {
            var rigImp = AssetImporter.GetAtPath(SoldadoRig) as ModelImporter;
            Avatar srcAvatar = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(SoldadoRig)) if (o is Avatar a) srcAvatar = a;
            if (srcAvatar == null) { Debug.LogError("[ArtSetup] lego.fbx no genero Avatar; revisa el rig."); return; }
            float escalaRig = rigImp != null ? rigImp.globalScale : 1f;

            foreach (var nombre in ClipsDeSalto)
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
                    // Solo "jump loop" es un ciclo; despegue y aterrizaje son de una sola pasada.
                    bool loopea = nombre == "jump loop";
                    clips[i].loopTime = loopea;
                    clips[i].loopPose = loopea;
                    clips[i].lockRootRotation = true;
                    clips[i].keepOriginalOrientation = true;
                    clips[i].lockRootHeightY = true;
                    clips[i].heightFromFeet = true;
                    clips[i].keepOriginalPositionY = true;
                    clips[i].lockRootPositionXZ = false;
                    clips[i].keepOriginalPositionXZ = false;
                }
                imp.clipAnimations = clips;
                imp.SaveAndReimport();

                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ruta))
                    if (o is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                        AnularDesplazamientoHorizontal(clip);
            }
            CorregirAlturaDeCadera();
        }

        // Los 14 clips del blend 2D de pie (Caminar/Correr, ver
        // ArtBuilder.CrearBlendDePie) menos "walking" -- ese es el punto de
        // referencia, no algo a corregir.
        //
        // "rifle run" SE AGREGA A CORREGIR (antes se trataba como segunda
        // referencia, junto con "walking"): medido, trae RootT.y ~0.895
        // contra ~0.946 de "walking" -- 0.05 de diferencia, la misma
        // magnitud de descalibracion que el resto de este grupo, no un
        // segundo punto de referencia válido.
        static readonly string[] ClipsDeMarchaACorregir =
        {
            "rifle run",
            "walk backward", "walk left", "walk right",
            "walk forward left", "walk forward right", "walk backward left", "walk backward right",
            "run backward", "run left", "run right",
            "run forward left", "run forward right", "run backward left", "run backward right",
        };

        // Mismo bug, mismo arreglo, grupo agachado: los 8 "walk crouching
        // *" no vienen calibrados contra "idle crouching aiming" (que se
        // usa como pose base del agachado, ArtBuilder.CrearBlendDeAgachado).
        // Medido: dispersion de hasta 0.023 en RootT.y, todos POR ENCIMA de
        // "idle crouching aiming" (mismo signo en los 8, tipico de exports
        // Mixamo sin calibrar entre si -- misma causa raiz que el grupo de
        // pie, escala menor porque agachado ya arranca mas cerca del piso).
        static readonly string[] ClipsAgachadoACorregir =
        {
            "walk crouching forward", "walk crouching backward", "walk crouching left", "walk crouching right",
            "walk crouching forward left", "walk crouching forward right",
            "walk crouching backward left", "walk crouching backward right",
        };

        // BUG REAL medido por el usuario: "camina para cualquier lado que
        // no sea adelante y se entierra". Ninguno de los ajustes del
        // importer (lockRootHeightY, heightFromFeet, keepOriginalPositionY)
        // cambio esto de verdad -- medido en juego forzando el blend a
        // cada direccion, la cadera quedaba a MENOS DE LA MITAD de su
        // altura real (Y=0.34-0.42) en cualquier direccion que no fuera
        // "walking", sin importar la combinacion de esos flags.
        //
        // La causa real, medida leyendo la curva ya importada
        // (AnimationUtility.GetEditorCurve, RootT.y): "walking" trae
        // RootT.y ~0.94-0.95 (espacio de musculo normalizado de Mecanim) y
        // "walk right" trae ~0.37-0.39 -- los ~20 exports de Mixamo de
        // este pack, uno por clip, NO vienen calibrados a la misma altura
        // de referencia entre si. Y a diferencia del arrastre horizontal
        // (que applyRootMotion=false SI anula si sale como root motion
        // puro), la altura de RootT.y participa de como Mecanim reconstruye
        // la POSE humanoide completa cuadro a cuadro -- aplica igual con
        // applyRootMotion en false, medido probando ambas formas.
        //
        // El arreglo real es el mismo principio que ya usa
        // AnularDesplazamientoHorizontal: corregir la curva ya importada
        // en la fuente de verdad final, no confiar en que el importer la
        // calibre solo. Se DESPLAZA (no se reemplaza) la curva entera de
        // cada clip por una constante, para conservar el bamboleo natural
        // del paso -- solo se corrige el nivel base para que coincida con
        // "walking", que es el que ya se mide correcto en juego.
        internal static void CorregirAlturaDeCadera()
        {
            CorregirGrupoDeAltura("walking", ClipsDeMarchaACorregir);
            CorregirPisoDeAgachado();
            CorregirGrupoDeAltura("idle crouching aiming", ClipsAgachadoACorregir);
            AssetDatabase.SaveAssets();
        }

        // BUG REAL reportado por el usuario: "cuando se agacha con Ctrl no
        // se entierre". El grupo agachado entero (CorregirGrupoDeAltura de
        // abajo) se calibra CONTRA "idle crouching aiming", pero esa
        // calibracion solo garantiza que los 9 clips agachados queden
        // CONSISTENTES entre si -- nunca valido que la referencia misma
        // tocara el piso de verdad. Medido en Play mode con los huesos del
        // Humanoid (Animator.GetBoneTransform): con el clip tal como lo
        // exporta Mixamo, LeftToes/RightToes quedan en Y=-0.087/-0.083 --
        // POR DEBAJO del piso (Y=0) -- mientras de pie ("walking", ya
        // validado en juego) quedan en +0.13. Mismo principio que
        // CorregirGrupoDeAltura (desplazar la curva entera por una
        // constante, no reemplazarla) pero el objetivo es una ALTURA
        // ABSOLUTA fija, no relativa a otro clip: asi corregirla dos veces
        // (por ejemplo en dos cargas de dominio distintas) converge al
        // mismo resultado en vez de acumular el offset cada vez, que es
        // justo el riesgo de cualquier correccion que sume sin mirar el
        // estado actual.
        //
        // Constante medida: RootT.y promedio del clip crudo = 0.32742; para
        // levantar los dedos del pie ~0.10 (de -0.085 de promedio a +0.015,
        // un margen chico por encima del piso) el promedio objetivo es
        // 0.32742 + 0.10 = 0.4274.
        const float AlturaDePisoObjetivoAgachado = 0.4274f;

        // La correccion NO se hace sobre el clip importado (eso vive en la cache de Library y un reimport la
        // pierde: ese fue el bug de "se vuelve a hundir" de los clips de caminar). Se HORNEA a tres assets
        // .anim propios (Assets/_Project/Animation/Salto) y el controlador apunta a ellos: sobreviven a
        // reimportar, a borrar Library y a un build.
        internal const string SaltoDir = "Assets/_Project/Animation/Salto";
        internal static string RutaDeSaltoHorneado(string nombre) => SaltoDir + "/" + nombre.Replace(' ', '_') + ".anim";

        [MenuItem("Strategic Point/Arte/1c. Hornear clips de salto")]
        public static void HornearClipsDeSalto()
        {
            System.IO.Directory.CreateDirectory(SaltoDir);
            foreach (var nombre in ClipsDeSalto)
            {
                var origen = CargarClip(nombre);
                if (origen == null) { Debug.LogWarning("[ArtSetup] No hay clip importado de '" + nombre + "': corre Arte/1b."); continue; }

                var copia = Object.Instantiate(origen);
                copia.name = nombre;
                if (TryPromedioRootTy(copia, out float promedio) && promedio < UmbralSaltoCrudo)
                {
                    foreach (var binding in AnimationUtility.GetCurveBindings(copia))
                    {
                        if (binding.propertyName != "RootT.y") continue;
                        var curva = AnimationUtility.GetEditorCurve(copia, binding);
                        var keys = curva.keys;
                        for (int k = 0; k < keys.Length; k++) keys[k].value += DeltaAlturaSalto;
                        curva.keys = keys;
                        AnimationUtility.SetEditorCurve(copia, binding, curva);
                    }
                }
                var ajustes = AnimationUtility.GetAnimationClipSettings(origen);
                ajustes.loopTime = nombre == "jump loop";
                ajustes.loopBlendOrientation = ajustes.loopBlendPositionY = ajustes.loopBlendPositionXZ = false;
                AnimationUtility.SetAnimationClipSettings(copia, ajustes);

                string ruta = RutaDeSaltoHorneado(nombre);
                var existente = AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta);
                if (existente != null) { EditorUtility.CopySerialized(copia, existente); Object.DestroyImmediate(copia); EditorUtility.SetDirty(existente); }
                else AssetDatabase.CreateAsset(copia, ruta);
            }
            AssetDatabase.SaveAssets();
            ReapuntarControladorASaltoHorneado();
        }

        // Los tres estados de salto del controlador pasan a usar el clip horneado.
        internal static void ReapuntarControladorASaltoHorneado()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/_Project/Animation/AC_Soldado.controller");
            if (ctrl == null || ctrl.layers.Length == 0) return;
            var estados = ctrl.layers[0].stateMachine.states;
            var mapa = new System.Collections.Generic.Dictionary<string, string>
            {
                { "SaltoArriba", "jump up" }, { "SaltoAire", "jump loop" }, { "SaltoAbajo", "jump down" },
            };
            foreach (var e in estados)
            {
                if (!mapa.TryGetValue(e.state.name, out var clip)) continue;
                var horneado = AssetDatabase.LoadAssetAtPath<AnimationClip>(RutaDeSaltoHorneado(clip));
                if (horneado != null) e.state.motion = horneado;
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
        }

        // Los tres clips de salto vienen del mismo export crudo: RootT.y ~0,41-0,48
        // contra ~0,96 de "rifle aiming idle" (de pie). La cadera del soldado se
        // hundia ~1 m durante todo el salto. Se DESPLAZAN las tres curvas por la
        // misma constante (medida: 0,962 - 0,414 = 0,548, el primer cuadro de
        // "jump up" es la pose de pie) para que el empalme despegue -> aire ->
        // aterrizaje siga continuo. Idempotente: un clip ya subido (promedio
        // por encima de 0,7) no se toca de nuevo.
        const float DeltaAlturaSalto = 0.548f;
        const float UmbralSaltoCrudo = 0.7f;

        static void CorregirPisoDeAgachado()
        {
            var clip = CargarClip("idle crouching aiming");
            if (clip == null) return;
            if (!TryPromedioRootTy(clip, out float alturaActual)) return;

            float delta = AlturaDePisoObjetivoAgachado - alturaActual;
            if (Mathf.Abs(delta) < 0.005f) return; // ya calibrado, no tocar keys de mas

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.propertyName != "RootT.y") continue;
                var curva = AnimationUtility.GetEditorCurve(clip, binding);
                var keys = curva.keys;
                for (int k = 0; k < keys.Length; k++) keys[k].value += delta;
                curva.keys = keys;
                AnimationUtility.SetEditorCurve(clip, binding, curva);
            }
            EditorUtility.SetDirty(clip);
            Debug.Log($"[ArtSetup] 'idle crouching aiming': piso corregido de {alturaActual:F4} a {AlturaDePisoObjetivoAgachado:F4} (delta {delta:F4}).");
        }

        static void CorregirGrupoDeAltura(string nombreReferencia, string[] clipsACorregir)
        {
            var referencia = CargarClip(nombreReferencia);
            if (referencia == null)
            {
                Debug.LogWarning($"[ArtSetup] No se encontro el clip '{nombreReferencia}' de referencia; se omite la correccion de altura de su grupo.");
                return;
            }
            if (!TryPromedioRootTy(referencia, out float alturaReferencia))
            {
                Debug.LogWarning($"[ArtSetup] '{nombreReferencia}' no tiene curva RootT.y; se omite la correccion de altura de su grupo.");
                return;
            }

            foreach (var nombre in clipsACorregir)
            {
                var clip = CargarClip(nombre);
                if (clip == null) continue;
                if (!TryPromedioRootTy(clip, out float alturaPropia)) continue;

                float delta = alturaReferencia - alturaPropia;
                if (Mathf.Abs(delta) < 0.01f) continue; // ya calibrado, no tocar keys de mas

                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (binding.propertyName != "RootT.y") continue;
                    var curva = AnimationUtility.GetEditorCurve(clip, binding);
                    var keys = curva.keys;
                    for (int k = 0; k < keys.Length; k++) keys[k].value += delta;
                    curva.keys = keys;
                    AnimationUtility.SetEditorCurve(clip, binding, curva);
                }
                EditorUtility.SetDirty(clip);
                Debug.Log($"[ArtSetup] '{nombre}': cadera corregida de {alturaPropia:F3} a {alturaReferencia:F3} (delta {delta:F3}), referencia '{nombreReferencia}'.");
            }
        }

        static AnimationClip CargarClip(string nombre)
        {
            string ruta = Pack + "/" + nombre + ".fbx";
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ruta))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            return null;
        }

        static bool TryPromedioRootTy(AnimationClip clip, out float promedio)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.propertyName != "RootT.y") continue;
                var curva = AnimationUtility.GetEditorCurve(clip, binding);
                if (curva == null || curva.length == 0) continue;
                float suma = 0f;
                foreach (var k in curva.keys) suma += k.value;
                promedio = suma / curva.length;
                return true;
            }
            promedio = 0f;
            return false;
        }

        static void AnularDesplazamientoHorizontal(AnimationClip clip)
        {
            bool tocado = false;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.propertyName != "RootT.x" && binding.propertyName != "RootT.z") continue;
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, clip.length, 0f));
                tocado = true;
            }
            if (tocado) EditorUtility.SetDirty(clip);
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

        // BUG REAL, encontrado de nuevo por el usuario en una sesion
        // posterior a la que "arreglo" esto: CorregirAlturaDeCadera edita
        // la curva RootT.y YA IMPORTADA de cada clip (AnimationUtility.
        // SetEditorCurve) -- eso vive en la cache de Library, NO en el
        // .fbx ni en su .meta. Confirmado con git status: correr la
        // correccion no deja NINGUN archivo modificado bajo Assets/ARTS.
        // Cualquier cosa que dispare un reimport real del pack (clonar el
        // repo de nuevo, borrar Library, a veces incluso solo reabrir el
        // proyecto) hace que Unity reconstruya esas curvas desde el FBX
        // crudo sin calibrar, y el "arreglado" vuelve a hundirse en
        // silencio -- sin que ningun diff de git lo delate. Antes esto
        // dependia de que alguien se acordara de correr ConfigurarTodo()
        // a mano despues. Ahora se reaplica sola: en cuanto Unity termina
        // de reimportar un FBX del pack, este postprocessor la corre de
        // nuevo. delayCall (no directo) porque OnPostprocessAllAssets no
        // es un lugar seguro para tocar AssetDatabase todavia -- el
        // import en curso no termino de asentarse.
        class RecalibrarAlturaAlReimportar : AssetPostprocessor
        {
            static bool encolado;

            static void OnPostprocessAllAssets(string[] importados, string[] borrados, string[] movidos, string[] movidosDesde)
            {
                // MEDIDO: EditorApplication.delayCall no llega a dispararse
                // de forma confiable bajo la automatizacion que reimporta
                // estos FBX en esta maquina (probado explicitamente: el
                // callback nunca corrio, ni esperando varios segundos ni
                // encadenando otra llamada). Un editor interactivo normal
                // si lo procesaria, pero no hay que depender de eso.
                // Llamar directo funciona: la correccion solo edita curvas
                // ya importadas (AnimationUtility.SetEditorCurve) y guarda
                // con AssetDatabase.SaveAssets -- no dispara otro reimport
                // de modelos, asi que no hay riesgo real de reentrancia
                // contra OnPostprocessAllAssets.
                if (encolado) return;
                bool tocoElPack = false;
                foreach (var p in importados)
                {
                    if (p.StartsWith(Pack, System.StringComparison.Ordinal) && p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    {
                        tocoElPack = true;
                        break;
                    }
                }
                if (!tocoElPack) return;

                encolado = true;
                try { CorregirAlturaDeCadera(); }
                finally { encolado = false; }
            }
        }

        // BUG REAL, tercera vuelta: el usuario reporto la cadera hundida
        // de nuevo en SC_Gameplay DESPUES de que el postprocessor de
        // arriba ya estuviera en el proyecto. Medido: "walking" seguia en
        // 0.9457 (su propio valor, no depende de nada) pero "walk
        // backward"/"walk right"/"walk left"/etc volvieron a su altura
        // cruda de Mixamo (~0.38-0.40) -- exactamente el patron de antes.
        // RecalibrarAlturaAlReimportar de arriba SOLO reacciona a un
        // reimport EN VIVO de un FBX del pack: si la cache de Library ya
        // nacio sin la correccion (por ejemplo, un entorno que no
        // persiste Library entre sesiones, que es lo que parece haber
        // pasado aca) no hay ningun reimport que lo dispare, y el bug
        // queda asentado hasta que alguien se acuerde de correr
        // ConfigurarTodo() a mano -- que es justo el problema original
        // que todo este archivo existe para evitar.
        //
        // [InitializeOnLoad] corre en CADA carga de dominio (arranque del
        // Editor, recompilacion de scripts, entrar o salir de Play mode)
        // sin depender de que algo dispare un reimport: una red de
        // seguridad adicional, no un reemplazo del postprocessor de
        // arriba (que sigue haciendo falta para el caso de un reimport
        // suelto en pleno uso del Editor, que no recarga el dominio).
        // Llamada directa y no por delayCall, mismo motivo medido que en
        // RecalibrarAlturaAlReimportar: delayCall no es confiable en esta
        // automatizacion. CorregirAlturaDeCadera ya sale rapido si algo
        // no esta importado todavia (CargarClip devuelve null) o si el
        // grupo ya esta calibrado (delta < 0.01), asi que correrla en
        // cada carga de dominio no es trabajo de mas en el caso normal.
        [InitializeOnLoad]
        static class RecalibrarAlturaAlCargarDominio
        {
            static RecalibrarAlturaAlCargarDominio()
            {
                CorregirAlturaDeCadera();
            }
        }
    }
}
