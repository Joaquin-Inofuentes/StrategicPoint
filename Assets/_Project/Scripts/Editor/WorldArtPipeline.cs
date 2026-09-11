using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    // Pipeline del pack de arte nuevo (Assets/ARTS/SP_Arte/_FBX_Export):
    // ~90 mallas modulares/ambiente/vehiculos/personajes/armas que
    // reemplazan el grayboxing de SC_Gameplay (Ground, Muro, Obstaculo_N,
    // Bidon_N, Arma_N, Vehiculo_Blindado) y las props viejas de
    // Assets/ARTS/SP_Arte (raiz) que planta ArtBuilder.
    //
    // Mismo patron que ArtSetup/ArtBuilder: todo por codigo, idempotente,
    // separado en pasos con su propio item de menu.
    //
    // Dos cosas que este pack NO trae y que ArtSetup.cs ya documenta para
    // el pack viejo, pero que aca se resuelven distinto:
    //
    //  1. ESCALA. Mismo exportador de Maya, mismo problema (ver el
    //     comentario 1 de ArtSetup). Pero esta vez es UN kit modular: una
    //     puerta tiene que seguir midiendo lo que mide un soldado al lado.
    //     Corregir la altura de cada malla por separado (como hace
    //     ArtSetup con arboles/barriles sueltos) rompe esa relacion. Se
    //     mide UN solo factor contra un soldado de referencia y se aplica
    //     igual a las ~90 mallas.
    //
    //  2. MATERIAL. El pack viejo trae una carpeta de texturas por prop.
    //     Este trae un unico Trimsheet (00_Texturas~/Trimsheet_1024.png):
    //     un atlas de colores solidos compartido por TODA la malla del
    //     kit. Un solo material remapeado a todas alcanza; no hace falta
    //     crear uno por prop.
    public static class WorldArtPipeline
    {
        const string FbxRoot = "Assets/ARTS/SP_Arte/_FBX_Export";
        // NO se referencia dentro de _FBX_Export/00_Texturas~: cualquier
        // carpeta cuyo nombre termina en "~" queda invisible para el
        // AssetDatabase de Unity (convencion de Unity para carpetas de
        // cache/temporales) -- nunca se le genera .meta ni aparece en
        // ningun Load*, asi que el material quedaba sin textura en
        // silencio. Se copia una vez a una carpeta normal del proyecto.
        const string TrimsheetTex = "Assets/_Project/Textures/ArteMundo/Trimsheet_1024.png";
        const string MatDir = "Assets/_Project/Materials/ArteMundo";
        const string PrefabDir = "Assets/_Project/Prefabs/ArteMundo";
        const string TrimsheetMatName = "MAT_Trimsheet";
        const string ScenePath = "Assets/_Project/Scenes/SC_Gameplay.unity";

        // Referencia para el factor de escala global (ver comentario 1 de
        // la clase). 1.75 m es la misma altura objetivo que ArtSetup usa
        // para M_Soldado.fbx -- mismo personaje, misma decision de diseño.
        const string SoldadoReferencia = FbxRoot + "/04_Personajes/SM_Chr_Soldado_Fusilero.fbx";
        const float AlturaSoldadoM = 1.75f;

        static readonly string[] Categorias =
        {
            "01_Modulares", "02_Ambiente", "03_Vehiculos", "04_Personajes", "05_Armas", "06_Nivel",
        };

        // ------------------------------------------------------------------
        // Paso 4: importacion + material compartido
        // ------------------------------------------------------------------
        [MenuItem("Strategic Point/Arte/4. Importar arte de mundo nuevo")]
        public static void ImportarTodo()
        {
            var fbxPaths = TodosLosFbx();
            if (fbxPaths.Count == 0)
            {
                Debug.LogError("[WorldArtPipeline] No se encontro ningun FBX en " + FbxRoot);
                return;
            }

            // Se mide el soldado de referencia crudo (escala 1:1) antes de
            // tocar nada mas: el factor global sale de ahi.
            ConfigurarImportBasico(SoldadoReferencia, 1f);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var refGo = AssetDatabase.LoadAssetAtPath<GameObject>(SoldadoReferencia);
            float alturaCruda = refGo != null ? AlturaCombinada(refGo) : 0f;
            if (alturaCruda < 0.0001f)
            {
                Debug.LogError("[WorldArtPipeline] No se pudo medir " + SoldadoReferencia + "; abortando.");
                return;
            }
            float factor = AlturaSoldadoM / alturaCruda;

            var material = CrearMaterialTrimsheet();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in fbxPaths)
                    ConfigurarImportBasico(path, factor);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // FUERA del bloque de edicion por lotes: el remap necesita los
            // sub-assets de Material que el import de arriba genera, y
            // dentro de StartAssetEditing los SaveAndReimport quedan en
            // cola (mismo motivo que ArtSetup.ConfigurarAnimaciones).
            foreach (var path in fbxPaths)
                RemapMateriales(path, material);
            AssetDatabase.SaveAssets();

            Debug.Log($"[WorldArtPipeline] {fbxPaths.Count} FBX importados. Factor de escala global: {factor:F4} " +
                      $"(medido con {Path.GetFileName(SoldadoReferencia)} = {AlturaSoldadoM} m).");
        }

        static List<string> TodosLosFbx()
        {
            var lista = new List<string>();
            foreach (var cat in Categorias)
            {
                string carpeta = FbxRoot + "/" + cat;
                if (!Directory.Exists(carpeta)) continue;
                foreach (var f in Directory.GetFiles(carpeta, "*.fbx", SearchOption.AllDirectories))
                    lista.Add(f.Replace('\\', '/'));
            }
            lista.Sort();
            return lista;
        }

        static string CarpetaDe(string fbxPath)
        {
            string rel = fbxPath.Substring(FbxRoot.Length + 1);
            int slash = rel.IndexOf('/');
            return slash < 0 ? rel : rel.Substring(0, slash);
        }

        static void ConfigurarImportBasico(string path, float factor)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { Debug.LogWarning("[WorldArtPipeline] No se encontro " + path); return; }

            if (imp.useFileScale) { imp.useFileScale = false; imp.globalScale = 1f; imp.SaveAndReimport(); }

            imp.globalScale = factor;
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.CalculateMikk;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            // Nada de este pack trae esqueleto (todo son mallas SM_ =
            // static mesh, incluidos los "personajes": son piezas de
            // exhibicion, no el rig que se anima -- ese sigue siendo
            // lego.fbx via ArtSetup/ArtBuilder).
            imp.importAnimation = false;
            imp.animationType = ModelImporterAnimationType.None;
            imp.SaveAndReimport();
        }

        static Material CrearMaterialTrimsheet()
        {
            Directory.CreateDirectory(MatDir);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogError("[WorldArtPipeline] No aparece el shader URP/Lit."); return null; }

            string ruta = MatDir + "/" + TrimsheetMatName + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, ruta); }
            mat.shader = shader;

            var timp = AssetImporter.GetAtPath(TrimsheetTex) as TextureImporter;
            if (timp != null && !timp.sRGBTexture) { timp.sRGBTexture = true; timp.SaveAndReimport(); }

            var tex = AssetDatabase.LoadAssetAtPath<Texture>(TrimsheetTex);
            if (tex != null) { mat.SetTexture("_BaseMap", tex); mat.SetColor("_BaseColor", Color.white); }
            else Debug.LogWarning("[WorldArtPipeline] No se encontro " + TrimsheetTex);

            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.3f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void RemapMateriales(string path, Material material)
        {
            if (material == null) return;
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) return;

            bool alguno = false;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is Material src)
                {
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), material);
                    alguno = true;
                }
            }
            if (alguno) imp.SaveAndReimport();
        }

        // ------------------------------------------------------------------
        // Paso 5: prefabs (uno por FBX, pivote a la base + collider segun
        // categoria -- mismo patron que ArtBuilder.CrearPrefabsDeProps)
        // ------------------------------------------------------------------
        enum Volumen { Capsula, Caja, Ninguno }

        struct ReglaCategoria { public string carpeta; public Volumen volumen; public float fraccionAncho; }

        static readonly ReglaCategoria[] Reglas =
        {
            new ReglaCategoria { carpeta = "01_Modulares",  volumen = Volumen.Caja,    fraccionAncho = 1f },
            new ReglaCategoria { carpeta = "02_Ambiente",   volumen = Volumen.Caja,    fraccionAncho = 1f },
            // Las piezas de vehiculo se componen dentro del prefab del
            // tanque que ya existe (ver ReemplazarVehiculo); sueltas no
            // necesitan collider propio.
            new ReglaCategoria { carpeta = "03_Vehiculos",  volumen = Volumen.Ninguno, fraccionAncho = 1f },
            new ReglaCategoria { carpeta = "04_Personajes", volumen = Volumen.Caja,    fraccionAncho = 0.55f },
            new ReglaCategoria { carpeta = "05_Armas",      volumen = Volumen.Caja,    fraccionAncho = 1f },
            // Nivel completo: fondo decorativo, no un obstaculo solido.
            new ReglaCategoria { carpeta = "06_Nivel",      volumen = Volumen.Ninguno, fraccionAncho = 1f },
        };

        // Excepciones puntuales: lo organico no quiere un collider del
        // ancho de su propia silueta (un arbol tapa media pantalla si el
        // collider mide lo que mide la copa). Mismo motivo que las
        // fracciones de ArtBuilder.Definiciones para los arboles viejos.
        static readonly Dictionary<string, float> FraccionCapsula = new Dictionary<string, float>
        {
            { "SM_Env_ArbolA", 0.22f }, { "SM_Env_ArbolB", 0.22f },
            { "SM_Env_Arbusto_Chico", 0.5f }, { "SM_Env_Arbusto_Medio", 0.5f }, { "SM_Env_Arbusto_Grande", 0.4f },
        };

        [MenuItem("Strategic Point/Arte/5. Armar prefabs de mundo nuevo")]
        public static void ArmarPrefabs()
        {
            Directory.CreateDirectory(PrefabDir);
            int n = 0;
            foreach (var path in TodosLosFbx())
            {
                var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (modelo == null) { Debug.LogWarning("[WorldArtPipeline] No cargo " + path); continue; }

                string carpeta = CarpetaDe(path);
                var regla = System.Array.Find(Reglas, r => r.carpeta == carpeta);
                string nombreBase = Path.GetFileNameWithoutExtension(path);
                string nombrePrefab = "P_" + (nombreBase.StartsWith("SM_") ? nombreBase.Substring(3) : nombreBase);

                var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
                go.name = nombrePrefab;
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;

                var raiz = new GameObject(nombrePrefab);
                go.transform.SetParent(raiz.transform, true);

                var b = BoundsDe(go);
                if (b.size == Vector3.zero)
                {
                    Debug.LogWarning("[WorldArtPipeline] " + path + " no tiene Renderer; se omite.");
                    Object.DestroyImmediate(raiz);
                    continue;
                }
                go.transform.position = new Vector3(-b.center.x, -b.min.y, -b.center.z);
                b = BoundsDe(go);

                bool esCapsula = FraccionCapsula.TryGetValue(nombreBase, out var fraccionOverride);
                var volumen = esCapsula ? Volumen.Capsula : regla.volumen;
                float fraccion = esCapsula ? fraccionOverride : regla.fraccionAncho;
                if (volumen != Volumen.Ninguno) AgregarVolumen(raiz, volumen, fraccion, b);

                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabDir + "/" + nombrePrefab + ".prefab");
                Object.DestroyImmediate(raiz);
                n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[WorldArtPipeline] {n} prefabs creados en {PrefabDir}.");
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

        static float AlturaCombinada(GameObject go) => BoundsDe(go).size.y;

        static void AgregarVolumen(GameObject raiz, Volumen volumen, float fraccionAncho, Bounds b)
        {
            float alto = Mathf.Max(0.05f, b.size.y);
            float ancho = Mathf.Max(0.05f, Mathf.Min(b.size.x, b.size.z)) * fraccionAncho;

            if (volumen == Volumen.Capsula)
            {
                var c = raiz.AddComponent<CapsuleCollider>();
                c.direction = 1;
                c.radius = ancho * 0.5f;
                c.height = alto;
                c.center = new Vector3(0f, alto * 0.5f, 0f);
            }
            else
            {
                var c = raiz.AddComponent<BoxCollider>();
                c.size = new Vector3(b.size.x * fraccionAncho, alto, b.size.z * fraccionAncho);
                c.center = new Vector3(0f, alto * 0.5f, 0f);
            }
        }

        // ------------------------------------------------------------------
        // Paso 6: reemplazo en SC_Gameplay
        // ------------------------------------------------------------------
        [MenuItem("Strategic Point/Arte/6. Reemplazar assets viejos en SC_Gameplay")]
        public static void ReemplazarEnEscena()
        {
            var escena = EditorSceneManager.GetActiveScene();
            if (!escena.name.Contains("Gameplay"))
            {
                EditorSceneManager.OpenScene(ScenePath);
                escena = EditorSceneManager.GetActiveScene();
            }

            // No se borra: se apaga. Reversible por si algo del roster
            // nuevo queda mal ubicado y hay que volver a lo viejo mientras
            // se ajusta.
            var arteViejo = GameObject.Find("Arte");
            if (arteViejo != null) arteViejo.SetActive(false);

            ReemplazarMurosYObstaculos();
            ReemplazarBidonesMundo();
            ReemplazarArmas();
            ReemplazarVehiculo();
            ReemplazarGround();
            PoblarAmbienteNuevo();
            PoblarMuestrasNuevo();

            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            Debug.Log("[WorldArtPipeline] SC_Gameplay actualizada con el arte nuevo y guardada.");
        }

        static GameObject CargarPrefab(string nombre)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + nombre + ".prefab");
            if (go == null) Debug.LogWarning("[WorldArtPipeline] Falta el prefab " + nombre);
            return go;
        }

        // Reemplazo generico para cualquier objeto de la escena que tenga
        // BoxCollider: el collider (y cualquier script que dependa de su
        // tamaño, como ObstacleMarker o WeaponPickup) queda intacto, solo
        // se apaga el MeshRenderer viejo y se cuelga la malla nueva como
        // hijo "VisualMundo", escalada para llenar exactamente el mismo
        // volumen que el collider. Mismo espiritu que
        // ArtBuilder.ReemplazarBidones: cambiar el CUERPO, no el
        // GameObject, para no perder referencias ni posicion.
        static void ReemplazarVisualEnCollider(GameObject objetivo, GameObject prefabNuevo)
        {
            if (objetivo == null || prefabNuevo == null) return;

            var anterior = objetivo.transform.Find("VisualMundo");
            if (anterior != null) Object.DestroyImmediate(anterior.gameObject);

            var mr = objetivo.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;

            var box = objetivo.GetComponent<BoxCollider>();
            Vector3 tamMundo = box != null ? Vector3.Scale(box.size, objetivo.transform.lossyScale) : objetivo.transform.lossyScale;

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefabNuevo, objetivo.transform);
            visual.name = "VisualMundo";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            var tamModelo = BoundsDe(visual).size; // bounds mundiales con localScale=1: ya arrastra la escala del padre
            visual.transform.localScale = new Vector3(
                tamModelo.x > 0.0001f ? tamMundo.x / tamModelo.x : 1f,
                tamModelo.y > 0.0001f ? tamMundo.y / tamModelo.y : 1f,
                tamModelo.z > 0.0001f ? tamMundo.z / tamModelo.z : 1f);

            if (box != null)
                visual.transform.localPosition = new Vector3(box.center.x, box.center.y - box.size.y * 0.5f, box.center.z);

            EditorUtility.SetDirty(objetivo);
        }

        static void ReemplazarMurosYObstaculos()
        {
            string[] variantes = { "P_Mod_Muro_Recto", "P_Mod_Muro_Puerta", "P_Mod_Muro_Ventana", "P_Mod_Muro_Pilar", "P_Mod_Muro_RotoAlto" };
            var objetivos = new List<GameObject>();
            var muro = GameObject.Find("Muro");
            if (muro != null) objetivos.Add(muro);
            for (int i = 1; i <= 4; i++)
            {
                var o = GameObject.Find("Obstaculo_" + i);
                if (o != null) objetivos.Add(o);
            }

            int n = 0;
            for (int i = 0; i < objetivos.Count; i++)
            {
                var prefab = CargarPrefab(variantes[i % variantes.Length]);
                if (prefab == null) continue;
                ReemplazarVisualEnCollider(objetivos[i], prefab);
                n++;
            }
            Debug.Log($"[WorldArtPipeline] {n} muros/obstaculos reskineados.");
        }

        // Los "Bidon_N" son cilindros primitivos (radio 0.5, alto 2,
        // pivote al centro) sin collider propio -- igual que documenta
        // ArtBuilder.ReemplazarBidones para el pack viejo. Se replica esa
        // logica de escala inversa por eje, apuntando al barril nuevo, y
        // de paso se les agrega el BoxCollider que nunca tuvieron.
        static void ReemplazarBidonesMundo()
        {
            var barril = CargarPrefab("P_Env_Barril");
            if (barril == null) return;
            var destructibles = GameObject.Find("Destructibles");
            if (destructibles == null) return;

            int n = 0;
            foreach (Transform bidon in destructibles.transform)
            {
                if (!bidon.name.StartsWith("Bidon_")) continue;

                var previoMundo = bidon.Find("VisualMundo");
                if (previoMundo != null) Object.DestroyImmediate(previoMundo.gameObject);
                var previoViejo = bidon.Find("Visual"); // dejado por ArtBuilder.ReemplazarBidones, si corrio
                if (previoViejo != null) previoViejo.gameObject.SetActive(false);

                var mr = bidon.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;

                if (bidon.GetComponent<Collider>() == null)
                {
                    var c = bidon.gameObject.AddComponent<BoxCollider>();
                    c.size = new Vector3(1f, 2f, 1f);
                    c.center = Vector3.zero;
                }

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(barril, bidon);
                visual.name = "VisualMundo";

                var e = bidon.localScale;
                visual.transform.localScale = new Vector3(
                    Mathf.Approximately(e.x, 0f) ? 1f : 1f / e.x,
                    Mathf.Approximately(e.y, 0f) ? 1f : 1f / e.y,
                    Mathf.Approximately(e.z, 0f) ? 1f : 1f / e.z);
                visual.transform.localPosition = new Vector3(0f, -1f, 0f);
                visual.transform.localRotation = Quaternion.identity;
                n++;
            }
            Debug.Log($"[WorldArtPipeline] {n} bidones reskineados.");
        }

        static void ReemplazarArmas()
        {
            ReemplazarVisualEnCollider(GameObject.Find("Arma_Rifle"), CargarPrefab("P_Wpn_Fusil"));
            ReemplazarVisualEnCollider(GameObject.Find("Arma_Pistola"), CargarPrefab("P_Wpn_Pistola"));
            ReemplazarVisualEnCollider(GameObject.Find("Arma_Pesada"), CargarPrefab("P_Wpn_LMG"));
            Debug.Log("[WorldArtPipeline] Armas del mundo reskineadas.");
        }

        // El cuerpo se estira para llenar el BoxCollider existente (mismo
        // metodo que el resto): el hitbox del vehiculo es una decision de
        // gameplay ya hecha y no se toca. La torreta y el cañon, en
        // cambio, se cuelgan a su escala real (la del factor global del
        // paso 4): son piezas articuladas -- TurretAI las rota sobre
        // TurretPivot -- y estirarlas para llenar una caja arbitraria las
        // desalinearia del punto de disparo (Muzzle).
        static void ReemplazarVehiculo()
        {
            var veh = GameObject.Find("Vehiculo_Blindado");
            if (veh == null) { Debug.LogWarning("[WorldArtPipeline] No se encontro Vehiculo_Blindado en la escena."); return; }

            ReemplazarVisualEnCollider(veh, CargarPrefab("P_Veh_Tanque_Cuerpo"));

            var pivot = veh.transform.Find("TurretMount/TurretPivot");
            if (pivot == null) { Debug.LogWarning("[WorldArtPipeline] Vehiculo_Blindado no tiene TurretPivot."); return; }

            var torreta = CargarPrefab("P_Veh_Tanque_Torreta");
            var turretVisual = pivot.Find("TurretVisual");
            if (torreta != null)
            {
                var previo = pivot.Find("VisualMundo");
                if (previo != null) Object.DestroyImmediate(previo.gameObject);
                if (turretVisual != null)
                {
                    var mrT = turretVisual.GetComponent<MeshRenderer>();
                    if (mrT != null) mrT.enabled = false;
                }
                var v = (GameObject)PrefabUtility.InstantiatePrefab(torreta, pivot);
                v.name = "VisualMundo";
                v.transform.localPosition = Vector3.zero;
                v.transform.localRotation = Quaternion.identity;
            }

            var canon = CargarPrefab("P_Veh_Tanque_Metralleta");
            var barrel = pivot.Find("TurretBarrel");
            if (canon != null && barrel != null)
            {
                var previo = barrel.Find("VisualMundo");
                if (previo != null) Object.DestroyImmediate(previo.gameObject);
                var mrB = barrel.GetComponent<MeshRenderer>();
                if (mrB != null) mrB.enabled = false;
                var v = (GameObject)PrefabUtility.InstantiatePrefab(canon, barrel);
                v.name = "VisualMundo";
                v.transform.localPosition = Vector3.zero;
                v.transform.localRotation = Quaternion.identity;
            }

            Debug.Log("[WorldArtPipeline] Vehiculo_Blindado reskineado (cuerpo/torreta/cañon).");
        }

        // Piso modular en vez del plano gris: se mide UN tile y se llena
        // el rectangulo del Ground viejo con una grilla. Si el tile sale
        // chico contra un Ground de 58x160 la cuenta se dispara a miles de
        // objetos -- hay un techo de seguridad que, si se pasa, deja el
        // Ground original como esta (con su collider intacto siempre;
        // acá sólo se decide si además se agrega el piso visual encima).
        const int TilesMaximos = 900;

        static void ReemplazarGround()
        {
            var ground = GameObject.Find("Ground");
            if (ground == null) { Debug.LogWarning("[WorldArtPipeline] No se encontro Ground."); return; }

            var pisoPrefab = CargarPrefab("P_Mod_Piso_Concreto");
            if (pisoPrefab == null) return;

            var muestra = (GameObject)PrefabUtility.InstantiatePrefab(pisoPrefab);
            var bTile = BoundsDe(muestra);
            float tileX = Mathf.Max(0.5f, bTile.size.x);
            float tileZ = Mathf.Max(0.5f, bTile.size.z);
            Object.DestroyImmediate(muestra);

            var t = ground.transform;
            var col = ground.GetComponent<BoxCollider>();
            Vector3 tam = col != null ? Vector3.Scale(col.size, t.lossyScale) : t.lossyScale;
            Vector3 centro = col != null ? t.TransformPoint(col.center) : t.position;

            int columnas = Mathf.Max(1, Mathf.RoundToInt(tam.x / tileX));
            int filas = Mathf.Max(1, Mathf.RoundToInt(tam.z / tileZ));

            if (columnas * filas > TilesMaximos)
            {
                Debug.LogWarning($"[WorldArtPipeline] El piso modular necesitaria {columnas * filas} tiles " +
                                  $"(limite {TilesMaximos}); se deja el Ground original sin tocar.");
                return;
            }

            var previo = GameObject.Find("PisoMundo");
            if (previo != null) Object.DestroyImmediate(previo);
            var raiz = new GameObject("PisoMundo");

            float xIni = centro.x - tam.x * 0.5f + tileX * 0.5f;
            float zIni = centro.z - tam.z * 0.5f + tileZ * 0.5f;
            float y = centro.y + tam.y * 0.5f;

            for (int fz = 0; fz < filas; fz++)
                for (int cx = 0; cx < columnas; cx++)
                {
                    var tile = (GameObject)PrefabUtility.InstantiatePrefab(pisoPrefab, raiz.transform);
                    tile.transform.position = new Vector3(xIni + cx * tileX, y, zIni + fz * tileZ);

                    // BUG REAL que esto corrige: P_Mod_Piso_Concreto (y las
                    // variantes CespedSeco/Grava) traen su propio BoxCollider
                    // -- util cuando ese prefab se usa como obstaculo suelto,
                    // pero acá se instancia CIENTOS de veces solo como piso
                    // VISUAL sobre el area que el BoxCollider de Ground ya
                    // cubre entero. Con todos esos colliders vivos, cualquier
                    // cuerpo con radio de barrido grande (el tanque, ~1,8 m)
                    // queda solapado con el piso constantemente -- el sphere
                    // cast de Deslizador no distingue "vertical, es el suelo"
                    // de "horizontal, es una pared", asi que el tanque se
                    // trababa contra su propio piso a los pocos segundos de
                    // arrancar a andar. Medido en SC_Gameplay: 630 tiles, los
                    // 630 con collider propio. El collider de Ground (unico,
                    // uno solo) ya alcanza para lo unico que hace falta que
                    // el piso resuelva: el rayo hacia abajo de ApoyoEnElPiso.
                    var tileCol = tile.GetComponent<Collider>();
                    if (tileCol != null) Object.DestroyImmediate(tileCol);
                }

            var groundMr = ground.GetComponent<MeshRenderer>();
            if (groundMr != null) groundMr.enabled = false; // el collider del Ground sigue vivo

            Debug.Log($"[WorldArtPipeline] Piso nuevo: {columnas * filas} tiles ({columnas}x{filas}).");
        }

        // ------------------------------------------------------------------
        // Ambientacion nueva (props con collider, cobertura real) y
        // muestras (exhibicion sin collider, "una de cada asset" -- mismo
        // pedido que ya resolvio ArtBuilder.Exhibicion para el pack viejo).
        // ------------------------------------------------------------------
        struct Plantado { public string prefab; public Vector3 pos; public float giro; }

        static readonly Plantado[] AmbienteNuevo =
        {
            // Arboleda oeste/este: mismas posiciones que ArtBuilder.Ambiente
            // (pack viejo), prefabs nuevos.
            new Plantado { prefab = "P_Env_ArbolA", pos = new Vector3(-14f, 0f,  6f),  giro = 15f },
            new Plantado { prefab = "P_Env_ArbolB", pos = new Vector3(-17f, 0f, 13f),  giro = 200f },
            new Plantado { prefab = "P_Env_ArbolA", pos = new Vector3(-12f, 0f, 19f),  giro = 90f },
            new Plantado { prefab = "P_Env_ArbolB", pos = new Vector3(-18f, 0f, 26f),  giro = 130f },
            new Plantado { prefab = "P_Env_ArbolA", pos = new Vector3(-11f, 0f, 33f),  giro = 40f },
            new Plantado { prefab = "P_Env_ArbolA", pos = new Vector3( 30f, 0f, 12f),  giro = 60f },
            new Plantado { prefab = "P_Env_ArbolB", pos = new Vector3( 28f, 0f, 30f),  giro = 210f },
            new Plantado { prefab = "P_Env_ArbolA", pos = new Vector3( 31f, 0f, 22f),  giro = 300f },
            // Barricadas sobre la linea de avance.
            new Plantado { prefab = "P_Env_Barricada_Sacos", pos = new Vector3(  8f, 0f, 14f), giro = 0f },
            new Plantado { prefab = "P_Env_Barricada_Sacos", pos = new Vector3( 13f, 0f, 18f), giro = 35f },
            new Plantado { prefab = "P_Env_Barricada_Sacos", pos = new Vector3( 19f, 0f, 11f), giro = 290f },
            new Plantado { prefab = "P_Env_Barricada_Sacos", pos = new Vector3( -3f, 0f, 22f), giro = 75f },
            // Barriles sueltos.
            new Plantado { prefab = "P_Env_Barril", pos = new Vector3(  6.2f, 0f, 15.1f), giro = 0f },
            new Plantado { prefab = "P_Env_Barril", pos = new Vector3( 14.4f, 0f, 19.6f), giro = 0f },
            new Plantado { prefab = "P_Env_Barril", pos = new Vector3( 20.6f, 0f, 12.3f), giro = 0f },
            new Plantado { prefab = "P_Env_Barril", pos = new Vector3( 21.4f, 0f, 13.4f), giro = 0f },
            new Plantado { prefab = "P_Env_Barril", pos = new Vector3( -1.8f, 0f,  8.4f), giro = 0f },
            new Plantado { prefab = "P_Env_Barril", pos = new Vector3( 24.8f, 0f, 24.2f), giro = 0f },
            // Roster nuevo que el pack viejo no tenia.
            new Plantado { prefab = "P_Env_Barricada_Concreto", pos = new Vector3(  2f, 0f,  2f), giro = 0f },
            new Plantado { prefab = "P_Env_Barricada_Erizo",    pos = new Vector3( 26f, 0f,  6f), giro = 0f },
            new Plantado { prefab = "P_Env_Barricada_Tablones", pos = new Vector3(  0f, 0f, 27f), giro = 20f },
            new Plantado { prefab = "P_Env_Neumaticos",         pos = new Vector3( 10f, 0f,  4f), giro = 0f },
            new Plantado { prefab = "P_Env_Palet",              pos = new Vector3( -8f, 0f,  4f), giro = 0f },
            new Plantado { prefab = "P_Env_Caja_Madera",        pos = new Vector3( 16f, 0f,  5f), giro = 0f },
            new Plantado { prefab = "P_Env_Auto_Quemado",       pos = new Vector3( 23f, 0f, 20f), giro = 40f },
            new Plantado { prefab = "P_Env_Farol",              pos = new Vector3( -2f, 0f, 12f), giro = 0f },
            new Plantado { prefab = "P_Env_Cartel",             pos = new Vector3(  5f, 0f, -3f), giro = 0f },
            new Plantado { prefab = "P_Env_Escombro_Pila_A",    pos = new Vector3( -6f, 0f, 24f), giro = 0f },
        };

        static void PoblarAmbienteNuevo()
        {
            var previo = GameObject.Find("ArteMundo");
            if (previo != null) Object.DestroyImmediate(previo);

            var raiz = new GameObject("ArteMundo");
            var ambiente = new GameObject("Ambiente"); ambiente.transform.SetParent(raiz.transform, false);

            int n = 0;
            foreach (var p in AmbienteNuevo)
            {
                var prefab = CargarPrefab(p.prefab);
                if (prefab == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ambiente.transform);
                go.transform.position = p.pos;
                go.transform.rotation = Quaternion.Euler(0f, p.giro, 0f);
                n++;
            }
            Debug.Log($"[WorldArtPipeline] {n} props de ambiente nuevos plantados.");
        }

        // Una de CADA prefab nuevo (lo pedido: "cargá todas ... revisá
        // todas las que hay") en una grilla al norte del area jugable
        // (Ground llega hasta z=137: sobra lugar sin pisarse con nada de
        // lo de arriba). Se arma por descubrimiento de PrefabDir, no a
        // mano: con ~90 assets, tipear cada nombre a mano es donde se
        // cuelan los errores de tipeo.
        static void PoblarMuestrasNuevo()
        {
            var raiz = GameObject.Find("ArteMundo");
            if (raiz == null) raiz = new GameObject("ArteMundo");
            var previo = raiz.transform.Find("Muestras");
            if (previo != null) Object.DestroyImmediate(previo.gameObject);
            var muestras = new GameObject("Muestras"); muestras.transform.SetParent(raiz.transform, false);

            var excluir = new HashSet<string>
            {
                "P_Nivel_Completo", "P_Veh_Tanque_Cuerpo", "P_Veh_Tanque_Torreta", "P_Veh_Tanque_Metralleta",
            };
            var nombres = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string nombre = Path.GetFileNameWithoutExtension(path);
                if (!excluir.Contains(nombre)) nombres.Add(nombre);
            }
            nombres.Sort();

            const int columnas = 14;
            const float paso = 3f;
            const float origenX = -22f;
            const float origenZ = 60f;

            int i = 0;
            foreach (var nombre in nombres)
            {
                var prefab = CargarPrefab(nombre);
                if (prefab == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, muestras.transform);
                go.name = "Muestra_" + nombre;
                int col = i % columnas;
                int fila = i / columnas;
                go.transform.localPosition = new Vector3(origenX + col * paso, 0f, origenZ + fila * paso);
                i++;
            }
            Debug.Log($"[WorldArtPipeline] {i} muestras nuevas plantadas (fila de exhibicion, z>=60).");
        }
    }
}
