using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    // Segunda mitad del pipeline de arte: con los FBX ya importados y
    // materializados por ArtSetup, esto arma lo que el juego usa de verdad
    // -- el Animator del soldado, los prefabs de props, el montaje del
    // modelo sobre los prefabs de soldado que ya existian, y el poblado de
    // SC_Gameplay.
    //
    // Todo idempotente: se puede volver a correr entero sin duplicar nada.
    public static class ArtBuilder
    {
        const string Pack = "Assets/ARTS/Slim Shooter Pack";
        const string SpArte = "Assets/ARTS/SP_Arte";
        const string AnimDir = "Assets/_Project/Animation";
        const string PrefabDir = "Assets/_Project/Prefabs/Arte";
        const string ControllerPath = AnimDir + "/AC_Soldado.controller";
        const string MaskPath = AnimDir + "/Mask_TrenSuperior.mask";

        // Raiz que agrupa todo lo que planta esta herramienta en la escena.
        // Existe para que volver a correrla sepa exactamente que borrar:
        // sin un contenedor propio habria que adivinar cuales objetos son
        // suyos y cuales puso alguien a mano.
        const string RaizEscena = "Arte";

        [MenuItem("Strategic Point/Arte/2. Armar animator, prefabs y escena")]
        public static void ArmarTodo()
        {
            var ctrl = CrearAnimator();
            CrearPrefabsDeProps();
            MontarSoldados(ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ArtBuilder] Animator, prefabs y soldados listos.");
        }

        // ------------------------------------------------------------------
        // Animator
        // ------------------------------------------------------------------
        // Dos capas y un solo parametro de gameplay.
        //
        //   Capa 0 (Locomocion): un blend tree 1D contra "Velocidad".
        //   Idle -> caminar -> correr. Un blend tree y no tres estados con
        //   transiciones porque la velocidad es continua: con estados, un
        //   soldado que va a media marcha elegiria uno de los dos ciclos y
        //   se veria acelerado o arrastrado.
        //
        //   Capa 1 (Disparo): la animacion de disparo con una mascara de
        //   tren superior, y el PESO manejado desde codigo
        //   (SoldierAnimatorDriver). Asi el soldado dispara MIENTRAS
        //   camina, que es justo lo que el attack-move hace en gameplay:
        //   las piernas siguen en la capa 0 y los brazos los pisa la 1.
        //   El peso va por codigo y no por una transicion con parametro
        //   bool porque una capa con peso fijo 1 y un estado vacio impone
        //   la pose de reposo sobre el tren superior aunque no dispare.
        public static AnimatorController CrearAnimator()
        {
            Directory.CreateDirectory(AnimDir);

            var idle = Clip("rifle aiming idle");
            var walk = Clip("walking");
            var run = Clip("rifle run");
            var fire = Clip("firing rifle");
            if (idle == null || walk == null || run == null || fire == null)
            {
                Debug.LogError("[ArtBuilder] Faltan clips; corre antes 'Arte/1. Configurar importacion y materiales'.");
                return null;
            }

            var mask = CrearMascaraTrenSuperior();

            AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter(SP.Presentation.SoldierAnimatorDriver.ParamVelocidad, AnimatorControllerParameterType.Float);

            ctrl.layers[0].name = "Locomocion";
            var bt = new BlendTree
            {
                name = "Locomocion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = SP.Presentation.SoldierAnimatorDriver.ParamVelocidad,
                useAutomaticThresholds = false,
            };
            AssetDatabase.AddObjectToAsset(bt, ctrl);
            bt.AddChild(idle, 0f);
            bt.AddChild(walk, 0.45f);
            bt.AddChild(run, 1f);

            var sm0 = ctrl.layers[0].stateMachine;
            var estado = sm0.AddState("Locomocion");
            estado.motion = bt;
            sm0.defaultState = estado;

            var sm1 = new AnimatorStateMachine { name = "Disparo", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(sm1, ctrl);
            var disparo = sm1.AddState("Disparar");
            disparo.motion = fire;
            sm1.defaultState = disparo;

            ctrl.AddLayer(new AnimatorControllerLayer
            {
                name = "Disparo",
                stateMachine = sm1,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 0f,
                iKPass = false,
            });

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static AvatarMask CrearMascaraTrenSuperior()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask == null) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, MaskPath); }

            // La raiz apagada es lo que impide que la animacion de disparo
            // arrastre al personaje por el piso peleando contra el motor.
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, false);
            EditorUtility.SetDirty(mask);
            return mask;
        }

        static AnimationClip Clip(string nombre)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Pack + "/" + nombre + ".fbx"))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            return null;
        }

        // ------------------------------------------------------------------
        // Prefabs de props
        // ------------------------------------------------------------------
        enum Volumen { Capsula, Caja }

        struct PropDef
        {
            public string fbx;
            public string prefab;
            public Volumen volumen;
            // Fraccion del ancho del modelo que ocupa el collider. Un arbol
            // mide 5 m de copa y 40 cm de tronco: un collider del ancho del
            // modelo cerraria media pantalla al paso.
            public float fraccionAncho;
        }

        static readonly PropDef[] Definiciones =
        {
            new PropDef { fbx = SpArte + "/M_Arbol 1.fbx",   prefab = "P_Arte_Arbol1",    volumen = Volumen.Capsula, fraccionAncho = 0.30f },
            new PropDef { fbx = SpArte + "/M_Arbol 2.fbx",   prefab = "P_Arte_Arbol2",    volumen = Volumen.Capsula, fraccionAncho = 0.30f },
            new PropDef { fbx = SpArte + "/M_Arbol 3.fbx",   prefab = "P_Arte_Arbol3",    volumen = Volumen.Capsula, fraccionAncho = 0.16f },
            new PropDef { fbx = SpArte + "/M_Barricada.fbx", prefab = "P_Arte_Barricada", volumen = Volumen.Caja,    fraccionAncho = 1f },
            new PropDef { fbx = SpArte + "/M_Barril 1.fbx",  prefab = "P_Arte_Barril",    volumen = Volumen.Capsula, fraccionAncho = 1f },
            new PropDef { fbx = SpArte + "/M_Soldado.fbx",   prefab = "P_Arte_Soldado",   volumen = Volumen.Caja,    fraccionAncho = 0.55f },
        };

        public static void CrearPrefabsDeProps()
        {
            Directory.CreateDirectory(PrefabDir);
            foreach (var d in Definiciones)
            {
                var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(d.fbx);
                if (modelo == null) { Debug.LogWarning("[ArtBuilder] Falta " + d.fbx); continue; }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
                go.name = d.prefab;
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;

                // PIVOTE. El arte viene con el origen donde lo dejo Maya,
                // que casi nunca es la base. Se envuelve el modelo en una
                // raiz propia y se lo baja para que el pivote del prefab
                // quede al ras del piso: asi plantarlo es poner y=0 y no
                // adivinar un offset distinto por asset.
                var raiz = new GameObject(d.prefab);
                go.transform.SetParent(raiz.transform, true);

                var b = BoundsDe(go);
                go.transform.position = new Vector3(-b.center.x, -b.min.y, -b.center.z);

                b = BoundsDe(go);
                AgregarVolumen(raiz, d, b);

                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabDir + "/" + d.prefab + ".prefab");
                Object.DestroyImmediate(raiz);
            }
        }

        static Bounds BoundsDe(GameObject go)
        {
            bool primero = true;
            Bounds b = default;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (primero) { b = r.bounds; primero = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        static void AgregarVolumen(GameObject raiz, PropDef d, Bounds b)
        {
            float alto = Mathf.Max(0.05f, b.size.y);
            float ancho = Mathf.Max(0.05f, Mathf.Min(b.size.x, b.size.z)) * d.fraccionAncho;

            if (d.volumen == Volumen.Capsula)
            {
                var c = raiz.AddComponent<CapsuleCollider>();
                c.direction = 1; // eje Y
                c.radius = ancho * 0.5f;
                c.height = alto;
                c.center = new Vector3(0f, alto * 0.5f, 0f);
            }
            else
            {
                var c = raiz.AddComponent<BoxCollider>();
                c.size = new Vector3(b.size.x * d.fraccionAncho, alto, b.size.z * d.fraccionAncho);
                c.center = new Vector3(0f, alto * 0.5f, 0f);
            }
        }

        // ------------------------------------------------------------------
        // Soldados
        // ------------------------------------------------------------------
        static readonly string[] PrefabsDeSoldado =
        {
            "Assets/_Project/Prefabs/P_Soldier_Ally.prefab",
            "Assets/_Project/Prefabs/P_Soldier_Enemy.prefab",
            "Assets/_Project/Prefabs/P_Soldier_Base.prefab",
        };

        public static void MontarSoldados(AnimatorController ctrl)
        {
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ArtSetup.SoldadoRig);
            if (modelo == null) { Debug.LogError("[ArtBuilder] No aparece " + ArtSetup.SoldadoRig); return; }

            foreach (var ruta in PrefabsDeSoldado)
            {
                var raiz = PrefabUtility.LoadPrefabContents(ruta);
                if (raiz == null) { Debug.LogWarning("[ArtBuilder] Falta " + ruta); continue; }

                // El cubo deja de dibujarse pero el volumen de colision
                // NO se toca: el BoxCollider del root es lo que
                // SoldierMotor usa para no atravesar el Muro, y el modelo
                // nuevo entra justo adentro de esa caja de 0.9 x 1.6 x 0.9.
                var mr = raiz.GetComponent<MeshRenderer>();
                if (mr != null) Object.DestroyImmediate(mr, true);
                var mf = raiz.GetComponent<MeshFilter>();
                if (mf != null) Object.DestroyImmediate(mf, true);

                var previo = raiz.transform.Find("Visual");
                if (previo != null) Object.DestroyImmediate(previo.gameObject, true);

                NormalizarEscala(raiz);

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(modelo, raiz.transform);
                visual.name = "Visual";
                visual.transform.SetSiblingIndex(0);

                var e = raiz.transform.localScale;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                // ALTURA. Se mide, no se estima: el origen del FBX no
                // coincide con la planta del pie (Mixamo deja la malla
                // colgando un poco por debajo del nodo raiz), asi que un
                // offset a ojo deja al soldado enterrado hasta el tobillo o
                // flotando. Se baja el modelo hasta que su base coincide
                // con la base del collider, que es el volumen con el que el
                // soldado pisa y choca.
                var caja = raiz.GetComponent<BoxCollider>();
                float baseCollider = caja != null
                    ? (caja.center.y - caja.size.y * 0.5f) * e.y
                    : -0.8f;

                float baseModelo = BoundsDe(visual).min.y;
                float ajuste = baseCollider - baseModelo;
                visual.transform.localPosition = new Vector3(
                    0f, Mathf.Approximately(e.y, 0f) ? ajuste : ajuste / e.y, 0f);

                // BOUNDS DEL SKINNED MESH -- esto es lo que hacia que los
                // soldados fueran INVISIBLES en Play mientras sus barras de
                // vida, anillos y marcadores se veian perfecto. Un
                // SkinnedMeshRenderer calcula su caja de culling desde el
                // hueso raiz en pose de bind, y la de este rig se quedaba
                // clavada a varios metros del soldado (medido: el cuerpo en
                // (5.9, 12.7) con sus bounds en (1.9, 8.9)). El frustum
                // culling descartaba a un soldado que estaba en el centro
                // exacto de la pantalla.
                //
                // Se ancla la caja al nodo del modelo, que no se anima, en
                // vez de prender updateWhenOffscreen: esa bandera lo
                // arregla igual pero obliga a recalcular la piel de CADA
                // soldado en CADA frame aunque no se vea, y este juego
                // apunta a 50 unidades en pantalla.
                // La malla se cambia por la que tiene las UVs con las que
                // se pinto la textura. Ver SkinTransfer: lego.fbx trae el
                // esqueleto pero un mapeo de caja generico, y el camuflaje
                // le salia untado en tiras.
                var mallaBuena = SkinTransfer.Cargar();
                foreach (var sk in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (mallaBuena != null) sk.sharedMesh = mallaBuena;
                    sk.updateWhenOffscreen = false;
                    sk.rootBone = visual.transform;
                    sk.localBounds = new Bounds(new Vector3(0f, 0.95f, 0f), new Vector3(1.4f, 2.1f, 1.4f));
                }

                var anim = visual.GetComponent<Animator>();
                if (anim == null) anim = visual.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                // Los soldados pelean fuera de camara todo el tiempo (la
                // vista RTS mira una esquina del mapa). Con el culling por
                // defecto, uno que estuvo fuera de plano aparece congelado
                // en la pose del frame en que salio.
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var driver = raiz.GetComponent<SP.Presentation.SoldierAnimatorDriver>();
                if (driver == null) driver = raiz.AddComponent<SP.Presentation.SoldierAnimatorDriver>();
                var so = new SerializedObject(driver);
                so.FindProperty("animator").objectReferenceValue = anim;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }

        // ESCALA NO UNIFORME: la razon por la que los soldados salian
        // INVISIBLES aun teniendo renderer activo, material y bounds
        // correctos.
        //
        // El root traia (0.9, 1.6, 0.9) para estirar un cubo hasta que
        // pareciera una persona. Unity NO soporta rigs humanoides bajo una
        // cadena de escala no uniforme: el retargeting deja de cerrar y los
        // huesos se van a cualquier lado (medido: con el soldado en y=0.8,
        // la cabeza terminaba en y=67 y el pie en y=-48). Contra-escalar el
        // hijo no alcanza -- su lossyScale daba (1,1,1) y el rig igual
        // explotaba, porque lo que rompe es la escala no uniforme EN LA
        // CADENA, no el resultado final.
        //
        // La forma se mueve entonces del Transform al BoxCollider, que es
        // donde de verdad importa: ese collider es el volumen con el que
        // SoldierMotor choca contra el Muro. Los hijos que ya estaban
        // (mira, boca del arma, ancla de la barra de vida) se compensan
        // multiplicando por la escala vieja, asi ninguno se mueve un
        // milimetro en el mundo.
        static void NormalizarEscala(GameObject raiz)
        {
            var vieja = raiz.transform.localScale;
            if (EsUniforme(vieja)) return;

            var caja = raiz.GetComponent<BoxCollider>();
            if (caja != null)
            {
                caja.size = Multiplicar(caja.size, vieja);
                caja.center = Multiplicar(caja.center, vieja);
            }

            foreach (Transform h in raiz.transform)
            {
                h.localPosition = Multiplicar(h.localPosition, vieja);
                h.localScale = Multiplicar(h.localScale, vieja);
            }

            raiz.transform.localScale = Vector3.one;
        }

        // La escala vieja del prefab de soldado. Se necesita en la escena
        // para traducir los overrides de instancia: un enemigo puesto a
        // (0.979, 1.741, 0.979) no era "no uniforme a proposito", era el
        // prefab (0.9, 1.6, 0.9) multiplicado por 1.088.
        static readonly Vector3 EscalaViejaDeSoldado = new Vector3(0.9f, 1.6f, 0.9f);

        static bool EsUniforme(Vector3 v) =>
            Mathf.Abs(v.x - v.y) < 0.001f && Mathf.Abs(v.y - v.z) < 0.001f;

        static Vector3 Multiplicar(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);

        static void NormalizarSoldadosDeEscena()
        {
            foreach (var s in Object.FindObjectsByType<SP.Actors.Soldier>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var t = s.transform;
                if (EsUniforme(t.localScale)) continue;

                var factor = new Vector3(
                    t.localScale.x / EscalaViejaDeSoldado.x,
                    t.localScale.y / EscalaViejaDeSoldado.y,
                    t.localScale.z / EscalaViejaDeSoldado.z);

                // Si el override no es un multiplo limpio del prefab viejo,
                // alguien lo deformo a mano: se avisa y se toma el promedio
                // en vez de romperlo en silencio.
                if (!EsUniforme(factor))
                    Debug.LogWarning("[ArtBuilder] " + s.name + " tenia una escala que no es multiplo del prefab (" +
                                     t.localScale + "); se uniformiza al promedio.");

                float u = (factor.x + factor.y + factor.z) / 3f;

                // Los hijos agregados EN LA ESCENA (indicadores de estado,
                // de alerta) tenian sus medidas en el espacio estirado del
                // root viejo; los que vienen del prefab ya los arreglo
                // NormalizarEscala y no hay que tocarlos dos veces.
                foreach (Transform h in t)
                {
                    if (PrefabUtility.GetCorrespondingObjectFromSource(h.gameObject) != null) continue;
                    h.localPosition = Multiplicar(h.localPosition, EscalaViejaDeSoldado);
                    h.localScale = Multiplicar(h.localScale, EscalaViejaDeSoldado);
                }

                t.localScale = new Vector3(u, u, u);
            }
        }

        // ------------------------------------------------------------------
        // Escena
        // ------------------------------------------------------------------
        struct Plantado
        {
            public string prefab;
            public Vector3 pos;
            public float giro;
            public string nombre;
        }

        // Una fila de exhibicion con UNO de cada asset (lo pedido: "1 por
        // asset", para que se vean todos juntos y con su textura), y
        // despues los mismos props repartidos por el mapa haciendo de
        // cobertura de verdad -- con collider, o sea que ahora tapan el
        // paso igual que el Muro.
        static readonly Plantado[] Exhibicion =
        {
            new Plantado { prefab = "P_Arte_Arbol1",    pos = new Vector3(-6f, 0f, -12f), giro = 0f,   nombre = "Muestra_Arbol1" },
            new Plantado { prefab = "P_Arte_Arbol2",    pos = new Vector3(-1f, 0f, -12f), giro = 25f,  nombre = "Muestra_Arbol2" },
            new Plantado { prefab = "P_Arte_Arbol3",    pos = new Vector3( 4f, 0f, -12f), giro = 0f,   nombre = "Muestra_Arbol3" },
            new Plantado { prefab = "P_Arte_Barricada", pos = new Vector3( 9f, 0f, -12f), giro = 0f,   nombre = "Muestra_Barricada" },
            new Plantado { prefab = "P_Arte_Barril",    pos = new Vector3(12f, 0f, -12f), giro = 0f,   nombre = "Muestra_Barril" },
            new Plantado { prefab = "P_Arte_Soldado",   pos = new Vector3(15f, 0f, -12f), giro = 180f, nombre = "Muestra_Soldado" },
        };

        static readonly Plantado[] Ambiente =
        {
            // Arboleda del borde oeste
            new Plantado { prefab = "P_Arte_Arbol3", pos = new Vector3(-14f, 0f,  6f),  giro = 15f },
            new Plantado { prefab = "P_Arte_Arbol1", pos = new Vector3(-17f, 0f, 13f),  giro = 200f },
            new Plantado { prefab = "P_Arte_Arbol2", pos = new Vector3(-12f, 0f, 19f),  giro = 90f },
            new Plantado { prefab = "P_Arte_Arbol3", pos = new Vector3(-18f, 0f, 26f),  giro = 130f },
            new Plantado { prefab = "P_Arte_Arbol1", pos = new Vector3(-11f, 0f, 33f),  giro = 40f },
            // Arboleda del borde este
            new Plantado { prefab = "P_Arte_Arbol1", pos = new Vector3( 30f, 0f, 12f),  giro = 60f },
            new Plantado { prefab = "P_Arte_Arbol3", pos = new Vector3( 28f, 0f, 30f),  giro = 210f },
            new Plantado { prefab = "P_Arte_Arbol2", pos = new Vector3( 31f, 0f, 22f),  giro = 300f },
            // Barricadas: cobertura sobre la linea de avance
            new Plantado { prefab = "P_Arte_Barricada", pos = new Vector3(  8f, 0f, 14f), giro = 0f },
            new Plantado { prefab = "P_Arte_Barricada", pos = new Vector3( 13f, 0f, 18f), giro = 35f },
            new Plantado { prefab = "P_Arte_Barricada", pos = new Vector3( 19f, 0f, 11f), giro = 290f },
            new Plantado { prefab = "P_Arte_Barricada", pos = new Vector3( -3f, 0f, 22f), giro = 75f },
            // Barriles sueltos
            new Plantado { prefab = "P_Arte_Barril", pos = new Vector3(  6.2f, 0f, 15.1f), giro = 0f },
            new Plantado { prefab = "P_Arte_Barril", pos = new Vector3( 14.4f, 0f, 19.6f), giro = 0f },
            new Plantado { prefab = "P_Arte_Barril", pos = new Vector3( 20.6f, 0f, 12.3f), giro = 0f },
            new Plantado { prefab = "P_Arte_Barril", pos = new Vector3( 21.4f, 0f, 13.4f), giro = 0f },
            new Plantado { prefab = "P_Arte_Barril", pos = new Vector3( -1.8f, 0f,  8.4f), giro = 0f },
            new Plantado { prefab = "P_Arte_Barril", pos = new Vector3( 24.8f, 0f, 24.2f), giro = 0f },
        };

        // Los "Bidon_*" de la escena eran cilindros primitivos verdes sin
        // collider: exactamente el mismo objeto que el barril de arte, pero
        // en placeholder. Se les cambia el CUERPO y se les deja el
        // GameObject: asi conservan su nombre, su lugar en la jerarquia y
        // cualquier referencia que alguien les haya puesto desde el
        // inspector. Y de paso se les da collider, que no tenian -- eran
        // decorado atravesable.
        static void ReemplazarBidones(Transform padreDeRespaldo)
        {
            var barril = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/P_Arte_Barril.prefab");
            if (barril == null) return;

            var destructibles = GameObject.Find("Destructibles");
            if (destructibles == null) return;

            foreach (Transform bidon in destructibles.transform)
            {
                if (!bidon.name.StartsWith("Bidon_")) continue;

                var mr = bidon.GetComponent<MeshRenderer>();
                if (mr != null) Object.DestroyImmediate(mr);
                var mf = bidon.GetComponent<MeshFilter>();
                if (mf != null) Object.DestroyImmediate(mf);

                var previo = bidon.Find("Visual");
                if (previo != null) Object.DestroyImmediate(previo.gameObject);

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(barril, bidon);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                // El bidon estaba escalado (0.55, 0.45, 0.55) para deformar
                // un cilindro primitivo; el modelo ya viene con su forma,
                // asi que se le aplica la inversa y llega al mundo 1:1.
                var e = bidon.localScale;
                visual.transform.localScale = new Vector3(
                    Mathf.Approximately(e.x, 0f) ? 1f : 1f / e.x,
                    Mathf.Approximately(e.y, 0f) ? 1f : 1f / e.y,
                    Mathf.Approximately(e.z, 0f) ? 1f : 1f / e.z);

                // El pivote del prefab de arte esta en la BASE; el del
                // bidon, en el centro de un cilindro primitivo, que mide 2
                // unidades de alto en su propio espacio. Bajar una unidad
                // local deja la base del barril donde estaba la del bidon,
                // sea cual sea la escala que le hayan puesto.
                visual.transform.localPosition = new Vector3(0f, -1f, 0f);

                // El collider viaja dentro del prefab de arte y, gracias a
                // la contra-escala de arriba, llega al mundo sin deformar.
                // Los bidones no tenian ninguno: eran decorado que se
                // atravesaba caminando.
            }
        }

        [MenuItem("Strategic Point/Arte/3. Poblar SC_Gameplay")]
        public static void PoblarEscena()
        {
            var escena = EditorSceneManager.GetActiveScene();
            if (!escena.name.Contains("Gameplay"))
            {
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/SC_Gameplay.unity");
                escena = EditorSceneManager.GetActiveScene();
            }

            var previa = GameObject.Find(RaizEscena);
            if (previa != null) Object.DestroyImmediate(previa);

            var raiz = new GameObject(RaizEscena);
            var muestras = new GameObject("Muestras"); muestras.transform.SetParent(raiz.transform, false);
            var ambiente = new GameObject("Ambiente"); ambiente.transform.SetParent(raiz.transform, false);

            int n = 0;
            n += Plantar(Exhibicion, muestras.transform);
            n += Plantar(Ambiente, ambiente.transform);
            ReemplazarBidones(ambiente.transform);
            NormalizarSoldadosDeEscena();
            ApoyarSoldadosEnElPiso();

            EditorSceneManager.MarkSceneDirty(escena);
            Debug.Log("[ArtBuilder] " + n + " props plantados en " + escena.name + ".");
        }

        // Los tres aliados estaban puestos en y=0.6 con una caja de 1.6 de
        // alto: la base quedaba 20 cm BAJO el piso. Con un cubo no se
        // notaba -- el borde inferior de un cubo gris sobre suelo gris no
        // dice nada -- pero con un modelo con piernas se ve enterrado
        // hasta los tobillos. Se apoya cada uno sobre el piso usando su
        // propio collider, que es el mismo volumen que usa SoldierMotor.
        static void ApoyarSoldadosEnElPiso()
        {
            foreach (var s in Object.FindObjectsByType<SP.Actors.Soldier>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var col = s.GetComponent<BoxCollider>();
                if (col == null) continue;
                float mitad = col.size.y * Mathf.Abs(s.transform.lossyScale.y) * 0.5f;
                var p = s.transform.position;
                if (Mathf.Abs(p.y - mitad) < 0.001f) continue;
                s.transform.position = new Vector3(p.x, mitad, p.z);
                EditorUtility.SetDirty(s.transform);
            }
        }

        static int Plantar(Plantado[] lista, Transform padre)
        {
            int n = 0;
            foreach (var p in lista)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + p.prefab + ".prefab");
                if (prefab == null) { Debug.LogWarning("[ArtBuilder] Falta el prefab " + p.prefab); continue; }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
                go.transform.position = p.pos;
                go.transform.rotation = Quaternion.Euler(0f, p.giro, 0f);
                if (!string.IsNullOrEmpty(p.nombre)) go.name = p.nombre;
                n++;
            }
            return n;
        }
    }
}
