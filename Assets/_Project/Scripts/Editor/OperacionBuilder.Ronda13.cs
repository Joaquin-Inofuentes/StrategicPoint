using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SP.Core;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ronda 13, Carril E (herramientas de nivel). NADA de esto se ejecuta solo: la Ola 2 lo corre, de a uno y en orden, con las 4 escenas del
    // nivel abiertas. Convenciones de todas las herramientas R13:
    //   - metodo publico <Tema>R13EscenaAbierta(bool soloInforme = false): devuelve un informe de texto y lo escribe en Temp/r13_<tema>.txt;
    //   - soloInforme = true no modifica NADA (ni escenas ni prefabs ni assets): lista lo que tocaria;
    //   - idempotente: el grupo R13_<Tema> se borra y se recrea por nombre;
    //   - guarda las escenas al final (MarkAllScenesDirty + SaveOpenScenes) y respalda los prefabs en Temp/R13_prefabs_antes/.

    /// <summary>Utilidades comunes de las herramientas de escena de la Ronda 13.</summary>
    public static class EscenasR13
    {
        public const string Operacion = "Assets/_Project/Scenes/SC_Operacion.unity";
        public static readonly string[] Sectores = { "Ruta", "Ciudad", "Cuarteles" };
        public const string CarpetaGenerada = "Assets/_Project/Generated/R13";
        public const string CarpetaRespaldo = "Temp/R13_prefabs_antes";

        public static string RutaSector(string sector) => "Assets/_Project/Scenes/Operacion/SC_Operacion_" + sector + ".unity";

        public static bool EstaAbierta(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && s.path == path) return true;
            }
            return false;
        }

        public static Scene Sector(string sector) => SceneManager.GetSceneByPath(RutaSector(sector));
        public static Scene Base() => SceneManager.GetSceneByPath(Operacion);

        /// <summary>Deja abiertas las 4 escenas del nivel. Si ya lo estan no toca nada (aunque haya cambios sin guardar). Si hay que abrir alguna y hay una escena sucia, aborta.</summary>
        public static bool Asegurar(StringBuilder sb, bool soloInforme)
        {
            if (Application.isPlaying) { sb.AppendLine("FALLO el editor esta en Play"); return false; }
            bool todas = EstaAbierta(Operacion);
            foreach (var s in Sectores) todas &= EstaAbierta(RutaSector(s));
            if (todas) return true;
            if (soloInforme) { sb.AppendLine("FALLO (solo informe): faltan escenas abiertas; abrirlas con LimpiezaDeNivel.AbrirEscena o correr la herramienta sin soloInforme"); return false; }
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) { sb.AppendLine("FALLO hay una escena con cambios sin guardar: " + SceneManager.GetSceneAt(i).path); return false; }
            if (!EstaAbierta(Operacion)) EditorSceneManager.OpenScene(Operacion, OpenSceneMode.Single);
            foreach (var s in Sectores) if (!EstaAbierta(RutaSector(s))) EditorSceneManager.OpenScene(RutaSector(s), OpenSceneMode.Additive);
            return true;
        }

        public static List<Transform> Todos()
        {
            var l = new List<Transform>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (!sc.isLoaded) continue;
                foreach (var r in sc.GetRootGameObjects()) l.AddRange(r.GetComponentsInChildren<Transform>(true));
            }
            return l;
        }

        public static List<Transform> DeEscena(Scene sc)
        {
            var l = new List<Transform>();
            if (!sc.isLoaded) return l;
            foreach (var r in sc.GetRootGameObjects()) l.AddRange(r.GetComponentsInChildren<Transform>(true));
            return l;
        }

        public static string Ruta(Transform t)
        {
            if (t == null) return "(null)";
            var s = t.name; int g = 0;
            while (t.parent != null && g++ < 12) { t = t.parent; s = t.name + "/" + s; }
            return t.gameObject.scene.name + ":" + s;
        }

        public static string P(Vector3 v) => $"({v.x:0.0},{v.y:0.0},{v.z:0.0})";

        /// <summary>Borra el grupo raiz con ese nombre en la escena (si existe) y devuelve uno nuevo, vacio, en el origen.</summary>
        public static Transform GrupoNuevo(Scene sc, string nombre)
        {
            foreach (var r in sc.GetRootGameObjects()) if (r.name == nombre) Object.DestroyImmediate(r);
            var g = new GameObject(nombre);
            SceneManager.MoveGameObjectToScene(g, sc);
            return g.transform;
        }

        public static void Sucia(Scene sc) { if (sc.IsValid() && sc.isLoaded) EditorSceneManager.MarkSceneDirty(sc); }

        /// <summary>Marca un cambio hecho por script sobre una instancia de prefab para que sobreviva al guardado (override).</summary>
        public static void Tocar(Object o)
        {
            if (o == null) return;
            if (PrefabUtility.IsPartOfPrefabInstance(o)) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
            EditorUtility.SetDirty(o);
        }

        public static Transform Hijo(Transform padre, string nombre)
        {
            var g = new GameObject(nombre).transform;
            g.SetParent(padre, false);
            return g;
        }

        public static void Guardar(StringBuilder sb)
        {
            EditorSceneManager.MarkAllScenesDirty();
            bool ok = EditorSceneManager.SaveOpenScenes();
            sb.AppendLine(ok ? "escenas guardadas" : "FALLO al guardar las escenas");
        }

        /// <summary>Copia el prefab a Temp/R13_prefabs_antes/ (una sola vez por sesion de trabajo: no pisa un respaldo anterior).</summary>
        public static void Respaldar(string assetPath)
        {
            try
            {
                Directory.CreateDirectory(CarpetaRespaldo);
                string destino = Path.Combine(CarpetaRespaldo, Path.GetFileName(assetPath));
                if (!File.Exists(destino)) File.Copy(assetPath, destino);
            }
            catch (System.Exception e) { Debug.LogWarning("[R13] sin respaldo de " + assetPath + ": " + e.Message); }
        }

        /// <summary>LoadPrefabContents -> cambio -> SaveAsPrefabAsset -> Unload. Respalda antes. Devuelve false si el prefab no existe.</summary>
        public static bool EditarPrefab(string assetPath, System.Action<GameObject> cambio, StringBuilder sb)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) == null) { sb.AppendLine("FALLO no existe el prefab " + assetPath); return false; }
            Respaldar(assetPath);
            var contenido = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                cambio(contenido);
                PrefabUtility.SaveAsPrefabAsset(contenido, assetPath);
            }
            catch (System.Exception e) { sb.AppendLine("FALLO editando " + assetPath + ": " + e.Message); return false; }
            finally { PrefabUtility.UnloadPrefabContents(contenido); }
            return true;
        }

        public static void AsegurarCarpetaGenerada()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Generated")) AssetDatabase.CreateFolder("Assets/_Project", "Generated");
            if (!AssetDatabase.IsValidFolder(CarpetaGenerada)) AssetDatabase.CreateFolder("Assets/_Project/Generated", "R13");
        }

        /// <summary>Escribe el informe en Temp/<archivo> y lo devuelve.</summary>
        public static string Cerrar(StringBuilder sb, string archivo)
        {
            try { File.WriteAllText(Path.Combine("Temp", archivo), sb.ToString()); } catch { /* informe en disco opcional */ }
            return sb.ToString();
        }
    }

    public static partial class OperacionBuilder
    {
        // ====================================================================================================================
        // T8: enemigos atrapados en los cubiculos de reserva tras la 2.a puerta (Barrera_Reserva_SoloEnemigos huerfanas)
        // ====================================================================================================================
        [MenuItem("Strategic Point/Operacion/R13 T8 · Quitar barreras huerfanas")]
        static void MenuQuitarBarrerasHuerfanas() => Debug.Log(QuitarBarrerasHuerfanasEscenaAbierta());

        /// <summary>
        /// T8. Borra de SC_Operacion los GameObjects huerfanos 'Barrera_Reserva_SoloEnemigos' (en +-45,6; 1,7; -176,1 tapan la unica puerta de los cubiculos),
        /// cualquier GO con BarreraSoloEnemigos que no sea de la azotea/mansion y los NavMeshModifierVolume que quedaron en la boca de los cubiculos.
        /// Firma: OperacionBuilder.QuitarBarrerasHuerfanasEscenaAbierta(bool soloInforme = false)
        /// </summary>
        public static string QuitarBarrerasHuerfanasEscenaAbierta(bool soloInforme = false)
        {
            var sb = new StringBuilder("== R13 T8 barreras huerfanas" + (soloInforme ? " (SOLO INFORME)" : "") + "\n");
            if (!EscenasR13.Asegurar(sb, soloInforme)) return EscenasR13.Cerrar(sb, "r13_t8.txt");

            var borrar = new List<GameObject>();
            var vistos = new HashSet<GameObject>();
            int conservadas = 0;
            foreach (var t in EscenasR13.Todos())
            {
                if (t == null) continue;
                bool porNombre = t.name == "Barrera_Reserva_SoloEnemigos";
                bool porComp = t.GetComponent<BarreraSoloEnemigos>() != null;
                if (!porNombre && !porComp) continue;
                string r = EscenasR13.Ruta(t);
                if (!porNombre && (r.Contains("Azotea") || r.Contains("Mansion"))) { conservadas++; sb.AppendLine("CONSERVADA (azotea/mansion) " + r); continue; }
                if (vistos.Add(t.gameObject)) { borrar.Add(t.gameObject); sb.AppendLine($"BARRERA {r} en {EscenasR13.P(t.position)} activa={t.gameObject.activeInHierarchy}"); }
            }

            // volumenes de NavMesh: solo se lista lo que esta en la boca de los cubiculos (x +-43..48,5; z -178..-174). Los volumenes de los muros Reserva_Muro_* son legitimos
            // (cierran el cubiculo): NO se tocan. Los de las barreras borradas se van con su GameObject.
            int volumenesEnBoca = 0;
            foreach (var v in Object.FindObjectsByType<NavMeshModifierVolume>(FindObjectsInactive.Include))
            {
                var p = v.transform.TransformPoint(v.center);
                bool enBoca = Mathf.Abs(p.x) > 43f && Mathf.Abs(p.x) < 48.5f && p.z < -173f && p.z > -178f;
                if (!enBoca) continue;
                volumenesEnBoca++;
                sb.AppendLine($"NavMeshModifierVolume en la boca {EscenasR13.Ruta(v.transform)} centro={EscenasR13.P(p)} area={v.area} {(vistos.Contains(v.gameObject) ? "[de una barrera: se borra con ella]" : "[legitimo: se deja]")}");
            }

            if (!soloInforme)
            {
                foreach (var g in borrar) { if (g != null) { EscenasR13.Sucia(g.scene); Object.DestroyImmediate(g); } }
                EscenasR13.Guardar(sb);
            }
            sb.Insert(0, $"barreras a borrar={borrar.Count} conservadas={conservadas} volumenes_de_navmesh_en_la_boca={volumenesEnBoca}\n");
            return EscenasR13.Cerrar(sb, "r13_t8.txt");
        }

        // ====================================================================================================================
        // T12: camion y gasolina fuera (escena + prefab del campamento). La logica de la 1.a puerta es del Carril D.
        // ====================================================================================================================
        const string PrefabCampamentoO = "Assets/_Project/Prefabs/Escena/Elementos/P_Op_Campamento_Aproximacion_O.prefab";
        const string PrefabCampamentoE = "Assets/_Project/Prefabs/Escena/Elementos/P_Op_Campamento_Aproximacion_E.prefab";

        [MenuItem("Strategic Point/Operacion/R13 T12 · Sin camion ni gasolina")]
        static void MenuSinCamionNiGasolina() => Debug.Log(SinCamionNiGasolinaEscenaAbierta());

        /// <summary>
        /// T12. En SC_Operacion: apaga Generador_Cuartel (con su Tanque), Camion_1 y Camion_2 y deja OperacionDirector.generador = null.
        /// En los prefabs de los campamentos del inicio: el O pierde Camion_* y Tambor*; el E (6 Tambor a 47 m del inicio, que Bug981a cuenta) pierde Tambor*. Se rellena el hueco
        /// con cajas de municion y una lona en el piso. Camion_3 (z -230) queda a proposito salvo apagarCamion3 = true: Bug981a cuenta cualquier Camion* en z &lt; -200,
        /// asi que hay que decidir entre apagarlo o excluirlo del check. NO toca Camion_Patio (cobertura del obj. 4; lo rehace T16).
        /// Firma: OperacionBuilder.SinCamionNiGasolinaEscenaAbierta(bool soloInforme = false, bool apagarCamion3 = false)
        /// </summary>
        public static string SinCamionNiGasolinaEscenaAbierta(bool soloInforme = false, bool apagarCamion3 = false)
        {
            var sb = new StringBuilder("== R13 T12 sin camion ni gasolina" + (soloInforme ? " (SOLO INFORME)" : "") + "\n");
            if (!EscenasR13.Asegurar(sb, soloInforme)) return EscenasR13.Cerrar(sb, "r13_t12.txt");

            // 1) escena
            int apagados = 0;
            var nombres = new HashSet<string> { "Generador_Cuartel", "Camion_1", "Camion_2" };
            if (apagarCamion3) nombres.Add("Camion_3");
            foreach (var t in EscenasR13.Todos())
            {
                if (t == null || !nombres.Contains(t.name)) continue;
                sb.AppendLine($"APAGAR {EscenasR13.Ruta(t)} en {EscenasR13.P(t.position)} (activo={t.gameObject.activeSelf})");
                if (!soloInforme && t.gameObject.activeSelf) { t.gameObject.SetActive(false); EscenasR13.Tocar(t.gameObject); EscenasR13.Sucia(t.gameObject.scene); }
                apagados++;
            }
            var dir = Object.FindAnyObjectByType<OperacionDirector>(FindObjectsInactive.Include);
            if (dir == null) sb.AppendLine("AVISO no hay OperacionDirector en las escenas abiertas");
            else
            {
                var so = new SerializedObject(dir);
                var pg = so.FindProperty("generador");
                if (pg == null) sb.AppendLine("AVISO OperacionDirector ya no tiene el campo 'generador'");
                else
                {
                    sb.AppendLine("director.generador = " + (pg.objectReferenceValue != null ? pg.objectReferenceValue.name : "null") + " -> null");
                    if (!soloInforme) { pg.objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(dir); EscenasR13.Sucia(dir.gameObject.scene); }
                }
            }

            // 2) prefabs de los campamentos del inicio
            int quitadas = 0;
            foreach (var (ruta, esO) in new[] { (PrefabCampamentoO, true), (PrefabCampamentoE, false) })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
                if (prefab == null) { sb.AppendLine("AVISO no existe " + ruta); continue; }
                int n0 = 0;
                foreach (Transform h in prefab.transform) if (h.name.StartsWith("Camion_") || h.name.StartsWith("Tambor")) n0++;
                quitadas += n0;
                sb.AppendLine($"prefab {(esO ? "O" : "E")}: {n0} piezas Camion_*/Tambor* a quitar");
                if (soloInforme) continue;
                bool conLona = esO;
                EscenasR13.EditarPrefab(ruta, raizP =>
                {
                    var borrar = new List<GameObject>();
                    foreach (Transform h in raizP.transform)
                        if (h.name.StartsWith("Camion_") || h.name.StartsWith("Tambor") || h.name.StartsWith("R13_Relleno")) borrar.Add(h.gameObject);
                    foreach (var g in borrar) Object.DestroyImmediate(g);
                    var t = raizP.transform;
                    // hueco de los 6 tambores (x -10..-8, z -9..-8): 3 cajas de municion apiladas a medias
                    for (int i = 0; i < 3; i++) Cl(t, "R13_Relleno_CajaMunicion_" + (i + 1), -10f + i * 1.15f, -9f, 0.95f, 0.55f, 0.6f, OlivaOsc, i * 7f);
                    Cl(t, "R13_Relleno_CajaMunicion_Alta", -9.4f, -9f, 0.9f, 0.5f, 0.55f, Cobertura, 12f, 0.55f);
                    if (conLona)
                    {
                        // hueco del camion (x 8, z -9..-1): lona tendida en el piso con dos cajones
                        Cl(t, "R13_Relleno_Lona", 8f, -4f, 4.2f, 0.04f, 5.2f, LonaArena, 8f);
                        Cl(t, "R13_Relleno_Cajon_1", 7f, -4.5f, 1.2f, 0.9f, 1.0f, Cobertura, 20f, 0.04f);
                        Cl(t, "R13_Relleno_Cajon_2", 9.1f, -3.2f, 1.0f, 0.7f, 0.9f, Cobertura, -12f, 0.04f);
                    }
                }, sb);
            }

            // 3) lo que sigue activo cerca del inicio (en modo informe incluye lo que los prefabs todavia tienen)
            var spawn = new Vector3(0f, 0f, -373f); int quedan = 0;
            foreach (var t in EscenasR13.Todos())
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                string n = t.name;
                if (!(n.StartsWith("Camion") || n.StartsWith("Tambor") || n.StartsWith("Generador"))) continue;
                if (nombres.Contains(n)) continue;
                var p = t.position;
                if (!(p.z < -200f || (p - spawn).magnitude < 60f)) continue;
                if (soloInforme && (n.StartsWith("Tambor") || n.StartsWith("Camion_C") || n.StartsWith("Camion_P") || n.StartsWith("Camion_R") || n.StartsWith("Camion_L")) && EscenasR13.Ruta(t).Contains("Campamento_Aproximacion_")) continue;   // ya contados en los prefabs
                quedan++;
                sb.AppendLine($"ACTIVO TODAVIA {EscenasR13.Ruta(t)} en {EscenasR13.P(p)}" + (n == "Camion_3" ? " (queda a proposito: lo rehace T16; Bug981a lo cuenta por estar en z < -200)" : " (no previsto: revisar)"));
            }

            if (!soloInforme) { AssetDatabase.SaveAssets(); EscenasR13.Guardar(sb); }
            sb.Insert(0, $"objetos de escena apagados={apagados} piezas de prefab quitadas={quitadas} otros activos cerca del inicio={quedan}\n");
            return EscenasR13.Cerrar(sb, "r13_t12.txt");
        }

        // ====================================================================================================================
        // T13: el cuadro gris del objetivo 4 = Plaza_Cemento (cubo liso de 30x30 bajo el tanque)
        // ====================================================================================================================
        [MenuItem("Strategic Point/Operacion/R13 T13 · Patio sin cuadro gris")]
        static void MenuPatioSinCuadroGris() => Debug.Log(PatioSinCuadroGrisEscenaAbierta());

        /// <summary>
        /// T13. Apaga la instancia Plaza_Cemento y la reemplaza por R13_PatioSolado: una losa de hormigon con juntas y marcas de neumatico
        /// (textura procedural + normal map, UV en metros, sin collider, ignoreFromBuild). La barra gris del HUD la resuelve el Carril B (HudDeTanqueSimple).
        /// Firma: OperacionBuilder.PatioSinCuadroGrisEscenaAbierta(bool soloInforme = false)
        /// </summary>
        public static string PatioSinCuadroGrisEscenaAbierta(bool soloInforme = false)
        {
            var sb = new StringBuilder("== R13 T13 patio sin cuadro gris" + (soloInforme ? " (SOLO INFORME)" : "") + "\n");
            if (!EscenasR13.Asegurar(sb, soloInforme)) return EscenasR13.Cerrar(sb, "r13_t13.txt");

            Transform plaza = null;
            foreach (var t in EscenasR13.Todos()) if (t != null && t.name == "Plaza_Cemento") { plaza = t; break; }
            if (plaza == null) sb.AppendLine("AVISO no hay 'Plaza_Cemento' (ya apagada y borrada?)");
            else
            {
                var mr = plaza.GetComponentInChildren<MeshRenderer>(true);
                sb.AppendLine($"Plaza_Cemento {EscenasR13.Ruta(plaza)} en {EscenasR13.P(plaza.position)} escala={plaza.lossyScale} material={(mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "-")} activa={plaza.gameObject.activeSelf}");
            }
            if (soloInforme) { sb.AppendLine("se crearia R13_PatioSolado (losa 30x30 a y=0,08..0,12 con juntas cada 3 m y 4 huellas de neumatico)"); return EscenasR13.Cerrar(sb, "r13_t13.txt"); }

            var escena = plaza != null ? plaza.gameObject.scene : EscenasR13.Sector("Cuarteles");
            if (plaza != null && plaza.gameObject.activeSelf) { plaza.gameObject.SetActive(false); EscenasR13.Tocar(plaza.gameObject); }

            var g = EscenasR13.GrupoNuevo(escena, "R13_PatioSolado");
            var hormigon = MatTexR13("Patio_Hormigon", new Color(0.78f, 0.78f, 0.76f), "HormigonJuntas", 3f, 0.8f, 0.06f);
            var goma = Mat("R13_Goma", new Color(0.10f, 0.10f, 0.11f));
            // misma altura que la losa vieja (y 0,08..0,12): reemplazo 1 a 1, ahora con textura y juntas
            Cubo(g, "R13_Losa_Patio", 0f, 22f, 30f, 0.04f, 30f, hormigon, 0f, 0.08f, false);
            // marcas de neumatico del tanque (oruga): 4 tiras largas muy finas, sin collider
            float[] xs = { -3.2f, -2.2f, 2.2f, 3.2f };
            for (int i = 0; i < xs.Length; i++) Cubo(g, "R13_Huella_" + (i + 1), xs[i], 20f + (i % 2) * 2f, 0.35f, 0.005f, 11f + (i % 2) * 4f, goma, i % 2 == 0 ? 1.5f : -1.5f, 0.121f, false);
            EscenasR13.Guardar(sb);
            sb.Insert(0, "Plaza_Cemento apagada; creado R13_PatioSolado. El Muro_Salida_Norte NO se toco (hay que confirmar con el dueno si se viste con ArteSalidaNorte).\n");
            return EscenasR13.Cerrar(sb, "r13_t13.txt");
        }
    }
}
