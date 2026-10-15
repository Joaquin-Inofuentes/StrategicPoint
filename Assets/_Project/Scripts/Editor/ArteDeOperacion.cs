using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SP.EditorTools
{
    // Pedido: "agrega arte a todo, no quiero ver blocking, mejora el nivel".
    //
    // Cubos de SC_Operacion que seguian siendo cajas grises (muros, perimetro, edificios, rocas del canadon, barreras de
    // la autopista) se visten con el kit de arte del proyecto (Assets/_Project/Prefabs/ArteMundo, material trimsheet) y con
    // mallas procedurales guardadas como asset (rocas, barrera jersey, baranda de la autopista).
    //
    //  - Muros (Muralla, Cierre, Bunker, Borde, Valle): el cubo conserva collider, ObstacleMarker y NavMeshModifierVolume
    //    pero apaga su MeshRenderer; un hijo "ArteOp" cancela la escala del cubo (metros reales) y arma el muro con modulos
    //    (caras, remate, pilares y puas arriba).
    //  - Edificios: el cubo SIGUE dibujandose (conserva las ventanas encendidas de DetalleDeEdificios); el hijo "ArteOp" le
    //    agrega zocalo, cornisa, parapeto, pilares de esquina y puerta, para que deje de leerse como una caja.
    //  - Rocas / barreras: se cambia la malla del MeshFilter por una procedural; collider y escala no se tocan.
    //
    // Idempotente: cada corrida borra los hijos "ArteOp" de la anterior y vuelve a vestir. No toca colliders ni NavMesh.
    public static partial class ArteDeOperacion
    {
        const string Dir = "Assets/_Project/Prefabs/ArteMundo/";
        const string Raiz = "ArteOp";
        const string CarpetaMallas = "Assets/_Project/Models/ArteProcedural";

        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
        public static string Resumen { get; private set; } = "";

        static GameObject P(string nombre)
        {
            if (!cache.TryGetValue(nombre, out var g) || g == null)
            {
                g = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + nombre + ".prefab");
                cache[nombre] = g;
            }
            return g;
        }

        [MenuItem("Strategic Point/Arte/13. Vestir SC_Operacion con arte (muros, edificios, rocas, barreras)")]
        public static void VestirMenu() { Vestir(); }

        public static string Vestir()
        {
            cache.Clear();
            ordenDePiso = 0;
            contenedoresLimpios.Clear();
            var escena = SceneManager.GetActiveScene();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t != null && t.name == Raiz)
                {
                    var dueno = t.parent != null ? t.parent.GetComponent<MeshRenderer>() : null;
                    if (dueno != null) dueno.enabled = true;      // el cubo vuelve a dibujarse hasta que se lo vuelva a vestir
                    Object.DestroyImmediate(t.gameObject);
                }

            // Bug #221 ("no se ven los muros"): un muro con el cubo apagado y SIN su ArteOp (se perdio el arte pero el cubo siguio apagado) quedaba
            // invisible para siempre: el bucle de abajo salta los renderers apagados. Se vuelven a encender para que se vistan de nuevo.
            foreach (var huerfano in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (huerfano == null || huerfano.enabled || huerfano.GetComponent<Collider>() == null) continue;
                var mfh = huerfano.GetComponent<MeshFilter>();
                if (mfh == null || mfh.sharedMesh == null || mfh.sharedMesh.name != "Cube" || huerfano.transform.Find(Raiz) != null) continue;
                string claveH = Regex.Replace(huerfano.transform.name, @"[\s_()0-9]+$", "");
                if (EsMuro(claveH, huerfano.transform.localScale)) huerfano.enabled = true;
            }

            var rocas = MallasDeRoca();
            var jerseyX = MallaJersey(true); var jerseyZ = MallaJersey(false);
            var baranda = MallaBaranda(false); var barandaX = MallaBaranda(true);
            var contX = MallaContenedor(true); var contZ = MallaContenedor(false); var camX = MallaCamion(true); var camZ = MallaCamion(false);
            var rackX = MallaRack(true); var rackZ = MallaRack(false); var silo = MallaSilo();
            PrepararTechos();

            int pisos = 0, muros = 0, edificios = 0, rocasN = 0, barreras = 0, barandas = 0, props = 0, modulos = 0, surcos = 0, torres = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (mr == null || !mr.enabled) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                string mesh = mf.sharedMesh.name;
                bool propia = mesh.StartsWith("Op_RocaCanadon_") || mesh.StartsWith("Op_Jersey") || mesh.StartsWith("Op_Baranda") || mesh.StartsWith("Op_Contenedor")
                              || mesh.StartsWith("Op_Camion") || mesh.StartsWith("Op_Rack") || mesh == "Op_Silo" || mesh.StartsWith("Op_Surco") || mesh.StartsWith("Op_TorrePata");
                var t = mr.transform;
                string clave = Regex.Replace(t.name, @"[\s_()0-9]+$", "");
                bool relleno = clave == "Relleno" && mesh.StartsWith("Caja_");    // torres de fondo de la ciudad: malla propia de caja con ventanas
                if (mesh != "Cube" && !propia && !relleno) continue;
                if (t.GetComponentInParent<SP.Actors.Soldier>() != null) continue;
                var s = t.localScale;
                if ((t.lossyScale - s).sqrMagnitude > 0.01f) continue;   // el cubo vive bajo un padre escalado: no se viste

                if (relleno) { int n = VestirEdificio(mr, clave, false); if (n > 0) { edificios++; modulos += n; } }
                else if (EsPiso(clave, s, t)) { int n = VestirPiso(mr, clave); if (n > 0) { pisos++; modulos += n; } }
                else if (EsMuro(clave, s)) { int n = VestirMuro(mr, clave); if (n > 0) { muros++; modulos += n; } }
                else if (EsEdificio(clave, s)) { int n = VestirEdificio(mr, clave); if (n > 0) { edificios++; modulos += n; } }
                else if (clave == "Roca_Canadon" || clave == "Cornisa" || (clave == "Roca" && mesh == "Cube"))
                {
                    mf.sharedMesh = rocas[Mathf.Abs(Hash(t.name + (int)(t.position.x * 3f))) % rocas.Length]; rocasN++;
                }
                else if (clave.StartsWith("Jersey_") || clave == "Barrera_Jersey")
                {
                    mf.sharedMesh = s.x >= s.z ? jerseyX : jerseyZ; barreras++;
                }
                else if (clave == "Baranda" && Mathf.Min(s.x, s.z) < 0.6f && Mathf.Max(s.x, s.z) > 3f)
                {
                    mf.sharedMesh = s.z >= s.x ? baranda : barandaX; barandas++;
                }
                else if (clave.StartsWith("Contenedor") && s.y > 1.5f && Mathf.Max(s.x, s.z) > 3f) { mf.sharedMesh = s.x >= s.z ? contX : contZ; props++; }
                else if (clave == "Camion" || clave == "Camion_Patio" || clave == "Camion_Caja") { mf.sharedMesh = s.x >= s.z ? camX : camZ; props++; }
                else if (clave == "Rack" && s.y > 2f) { mf.sharedMesh = s.x >= s.z ? rackX : rackZ; props++; }
                else if (clave == "Silo") { mf.sharedMesh = silo; props++; }
                else if (clave == "Auto_Abandonado" || clave == "Auto_Viejo") { int n = VestirAuto(mr); if (n > 0) { props++; modulos += n; } }
                else if (clave.StartsWith("Caja_") && s.y >= 0.9f && Mathf.Min(s.x, s.z) >= 1.4f) { int n = VestirCajas(mr); if (n > 0) { props++; modulos += n; } }
                else if (clave == "Barricada_Cemento") { int n = VestirBarricada(mr); if (n > 0) { props++; modulos += n; } }
                else if (clave == "Surco") { if (VestirSurco(mr)) surcos++; }
                else if (clave == "Torre_Pata") { if (VestirTorrePata(mr)) torres++; }
            }
            string suelo = VestirSuelo();
            LimpiarSurcosViejos();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            Resumen = $"{pisos} pisos con losetas, {muros} muros y {edificios} edificios vestidos con {modulos} modulos del kit ({techosN} techos con equipos), {rocasN} rocas, {barreras} barreras jersey, {barandas} barandas, {props} props (contenedores, camiones, racks, silos, autos, barricadas), {surcos} surcos con plantas, {torres} patas de torre reticuladas; suelo: {suelo}";
            Debug.Log("[ArteDeOperacion] " + Resumen);
            return Resumen;
        }

        // ------------------------------------------------------------------
        // Clasificacion
        // ------------------------------------------------------------------
        // Pisos y calles planas (cubos finos alineados a los ejes): se cubren de losetas del kit con relieve real.
        static readonly Regex rePiso = new Regex(@"^(Patio_|Piso|CD_Piso|Plaza|Asfalto_Corredor|Camino|Calle)", RegexOptions.Compiled);
        static bool EsPiso(string clave, Vector3 s, Transform t)
        {
            if (!rePiso.IsMatch(clave) || s.y > 0.7f || Mathf.Min(s.x, s.z) < 3f) return false;
            float yaw = Mathf.Repeat(t.eulerAngles.y, 90f);
            if (yaw > 0.5f && yaw < 89.5f) return false;                      // solo alineados a los ejes
            return Mathf.Abs(t.eulerAngles.x) < 0.5f && Mathf.Abs(t.eulerAngles.z) < 0.5f;
        }

        [System.Serializable] sealed class CeldaTrim { public string name; public int[] rgb; public float[] uv_center; }
        [System.Serializable] sealed class MapaTrim { public CeldaTrim[] cells; }
        static MapaTrim mapaTrim;
        static readonly Dictionary<string, Material> tintes = new Dictionary<string, Material>();

        // Color de zona del cubo: la celda del trimsheet a la que apunta su material (offset del _BaseMap) -> rgb.
        static Color ColorDeZona(Material m)
        {
            if (m == null || !m.HasProperty("_BaseMap")) return new Color(0.5f, 0.5f, 0.5f);
            if (mapaTrim == null)
                mapaTrim = JsonUtility.FromJson<MapaTrim>(File.ReadAllText("Assets/ARTS/SP_Arte/Trimsheet_1024_map.json"));
            var off = m.GetTextureOffset("_BaseMap");
            CeldaTrim mejor = null; float md = 1e9f;
            foreach (var c in mapaTrim.cells)
            {
                float d = (c.uv_center[0] - off.x) * (c.uv_center[0] - off.x) + (c.uv_center[1] - off.y) * (c.uv_center[1] - off.y);
                if (d < md) { md = d; mejor = c; }
            }
            if (mejor == null || md > 0.002f) { var bc = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.gray; return bc; }
            return new Color(mejor.rgb[0] / 255f, mejor.rgb[1] / 255f, mejor.rgb[2] / 255f);
        }

        // Copia de MAT_Trimsheet teñida con el color de la zona (las losetas conservan el relieve y el reparto de celdas del kit).
        static Material TinteDeZona(Material zona)
        {
            string key = zona != null ? zona.name : "neutro";
            if (tintes.TryGetValue(key, out var hecho) && hecho != null) return hecho;
            var baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/ArteMundo/MAT_Trimsheet.mat");
            string ruta = "Assets/_Project/Materials/ArteMundo/MAT_Trimsheet_Zona_" + key + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null) { mat = new Material(baseMat); AssetDatabase.CreateAsset(mat, ruta); }
            var c = ColorDeZona(zona);
            // las celdas de concreto del kit rondan 0,55 de luminosidad: se escala para que el resultado quede cerca del color de la zona
            float k = 1f / 0.55f;
            var tinte = new Color(Mathf.Clamp(c.r * k, 0.35f, 1.5f), Mathf.Clamp(c.g * k, 0.35f, 1.5f), Mathf.Clamp(c.b * k, 0.35f, 1.5f), 1f);
            mat.SetColor("_BaseColor", tinte); mat.SetColor("_Color", tinte);
            EditorUtility.SetDirty(mat);
            tintes[key] = mat;
            return mat;
        }

        // Cubre un piso con losetas (cuadricula exacta). El cubo deja de dibujarse; su collider queda igual.
        // Bug #225 ("se solapan los asfaltos de las calles"): 22 pares de pisos/calles quedaban coplanares (misma altura) y las losetas se peleaban
        // el pixel (z-fighting). Cada piso sube una fraccion distinta (calles +5 cm sobre patios) sin pasar de la cara del cubo.
        static int ordenDePiso;
        public static bool ConLosetas = false;
        static int VestirPiso(MeshRenderer mr, string clave)
        {
            // Pulido: los pisos ya NO se cubren de losetas. Cada loseta del kit dejaba juntas oscuras a intervalos fijos (costuras muy visibles);
            // el piso queda como un plano continuo de color liso y la variedad la ponen los decals (OperacionBuilder.Decals.cs).
            if (!ConLosetas) return 0;
            var cubo = mr.transform; var s = cubo.localScale;
            bool calle = clave.StartsWith("Calle") || clave.StartsWith("Camino") || clave.StartsWith("Asfalto");
            int nx = Mathf.Max(1, Mathf.RoundToInt(s.x / 3.83f)), nz = Mathf.Max(1, Mathf.RoundToInt(s.z / 3.83f));
            if (nx * nz > 900) return 0;                                      // pisos gigantes: se dejan como estan
            var raiz = Raiz_(cubo, s);
            float cx = s.x / nx, cz = s.z / nz, yBase = s.y * 0.5f - 0.16f + (calle ? 0.05f : 0f) + (ordenDePiso++ % 24) * 0.002f;   // el plano de la loseta queda apenas bajo la cara del cubo: las lineas de calle y flechas siguen viendose
            var mat = TinteDeZona(mr.sharedMaterial);
            int hash = Mathf.Abs(Hash(cubo.name + cubo.position.x.ToString("0")));
            int n = 0;
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    int h = (hash + ix * 7 + iz * 13 + ix * iz) % 10;
                    string prefab;
                    if (calle) prefab = "P_Mod_Piso_Asfalto";
                    else if (clave.StartsWith("CD_Piso") || clave.StartsWith("Plaza")) prefab = "P_Mod_Piso_Baldosa";
                    else prefab = h < 6 ? "P_Mod_Piso_Concreto" : h < 8 ? "P_Mod_Piso_ConcretoRoto" : h < 9 ? "P_Mod_Piso_ConcretoRoto_Var90" : "P_Mod_Piso_ConcretoRoto_Var180";
                    var g = P(prefab); if (g == null) continue;
                    var rs = g.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) continue;
                    var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    var pos = new Vector3(-s.x * 0.5f + (ix + 0.5f) * cx, yBase, -s.z * 0.5f + (iz + 0.5f) * cz);
                    var t = Poner(raiz, prefab, pos, 0f, new Vector3(cx / Mathf.Max(0.01f, b.size.x), 1f, cz / Mathf.Max(0.01f, b.size.z)));
                    if (t == null) continue;
                    foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
                    n++;
                }
            if (n > 0) mr.enabled = false; else Object.DestroyImmediate(raiz.gameObject);
            return n;
        }

        static bool EsMuro(string clave, Vector3 s)
        {
            switch (clave)
            {
                case "Muralla_O": case "Muralla_E": case "Cierre_Norte_E": case "Cierre_Norte_O":
                case "Bunker_Cuerpo": case "Borde_Norte": case "Borde_Sur": case "Borde_Este": case "Borde_Oeste":
                case "Valle_Oeste": case "Valle_Este": case "Valle_Sur":
                case "CD_Norte_O": case "CD_Norte_E": case "CD_Sur_O": case "CD_Sur_E": case "CD_Oeste_N": case "CD_Oeste_S":
                case "CD_Este_N": case "CD_Este_S": case "Muro_Salida_Norte":
                    return true;
            }
            if ((clave.StartsWith("Cuartel_Interno_") || clave.StartsWith("Cuartel_MuroBajo")) && s.y >= 1.5f) return true;
            return false;
        }

        static bool EsEdificio(string clave, Vector3 s)
        {
            if (s.y < 2.4f || Mathf.Min(s.x, s.z) < 3f || Mathf.Max(s.x, s.z) > 90f) return false;
            switch (clave)
            {
                case "Casa": case "Casa_A": case "Casa_B": case "Casa_C": case "Casita": case "Cochera_O": case "Cochera_E":
                case "Barraca_O_A": case "Barraca_O_B": case "Barraca_O_C": case "Comedor": case "Comando": case "Parque_Motor":
                case "Cuartel_Norte_O": case "Cuartel_Norte_E": case "Cuartel_Sur_O": case "Cuartel_Sur_E":
                case "Ayuntamiento": case "Edificio": case "Edificio_Borde": case "Garita_A":
                    return true;
            }
            return false;
        }

        static int Hash(string n) { int h = 17; foreach (char c in n) h = h * 31 + c; return h; }

        // ------------------------------------------------------------------
        // Colocacion de modulos (metros locales del cubo, ya sin su escala)
        // ------------------------------------------------------------------
        static Transform Raiz_(Transform cubo, Vector3 s)
        {
            var raiz = new GameObject(Raiz).transform;
            raiz.SetParent(cubo, false);
            raiz.localPosition = Vector3.zero; raiz.localRotation = Quaternion.identity;
            raiz.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            // El horneado del editor (LevelBlockoutBuilder) junta MALLAS VISUALES: sin esto, volver a hornear meteria pilares, puas,
            // equipos de techo y arbustos en la navegacion. Los cubos ya marcan lo no caminable con sus NavMeshModifierVolume.
            var mod = raiz.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
            mod.ignoreFromBuild = true; mod.applyToChildren = true;
            return raiz;
        }

        static Transform Poner(Transform raiz, string prefab, Vector3 pos, float yaw, Vector3 escala)
        {
            var g = P(prefab);
            if (g == null) return null;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(g, raiz);
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // SIN BatchingStatic: casi todo lo vestido cuelga de un ObstacleMarker, que achata el cubo al dañarlo; una malla metida
            // en un lote estatico queda congelada en su forma original y el muro "dañado" se veria entero.
            var t = inst.transform;
            t.localPosition = pos; t.localRotation = Quaternion.Euler(0f, yaw, 0f); t.localScale = escala;
            return t;
        }

        // Medidas del kit (m): Muro_Recto 3,83 x 2,87 x 0,39 | Pilar 0,56 x 2,87 | Losa 3,83 x 0,37 x 3,83 | Valla_Puas 3,83 x 1,0 | Puerta 3,0 x 5,0 x 0,6
        const float ML = 3.83f, MH = 2.87f, MT = 0.39f;

        // Muro: dos caras de modulos (o una si es fino), remate de losas, pilares cada tramo y puas arriba.
        static int VestirMuro(MeshRenderer mr, string clave)
        {
            var cubo = mr.transform; var s = cubo.localScale;
            bool aX = s.x >= s.z;
            float L = aX ? s.x : s.z, T = aX ? s.z : s.x, H = s.y;
            if (L < 4f || H < 1.5f) return 0;
            var raiz = Raiz_(cubo, s);
            float suelo = -s.y * 0.5f;
            bool perimetro = clave.StartsWith("Borde") || clave.StartsWith("Valle");
            float largoTramo = perimetro ? 16f : 7.66f;
            int cant = Mathf.Max(1, Mathf.RoundToInt(L / largoTramo));
            float tramo = L / cant;
            int cursos = Mathf.Max(1, Mathf.RoundToInt(H / MH));
            float sy = H / cursos / MH, sx = tramo / ML;
            bool dosCaras = T > 0.9f;
            float szCara = dosCaras ? 1.5f : Mathf.Clamp(T / MT, 1f, 2.2f);
            int n = 0;

            for (int i = 0; i < cant; i++)
            {
                float u = -L * 0.5f + (i + 0.5f) * tramo;
                for (int c = 0; c < cursos; c++)
                {
                    float y = suelo + c * H / cursos;
                    string mod = (!perimetro && (i + c) % 3 == 1) ? "P_Mod_Muro_Medio" : "P_Mod_Muro_Recto";
                    float sxm = mod == "P_Mod_Muro_Medio" ? sx * 2f : sx;
                    if (dosCaras)
                    {
                        float off = T * 0.5f - MT * szCara * 0.5f;
                        foreach (float lado in new[] { -1f, 1f })
                        {
                            var pos = aX ? new Vector3(u, y, lado * off) : new Vector3(lado * off, y, u);
                            if (Poner(raiz, mod, pos, aX ? 0f : 90f, new Vector3(sxm, sy, szCara)) != null) n++;
                        }
                    }
                    else
                    {
                        var pos = aX ? new Vector3(u, y, 0f) : new Vector3(0f, y, u);
                        if (Poner(raiz, mod, pos, aX ? 0f : 90f, new Vector3(sxm, sy, szCara)) != null) n++;
                    }
                }
                // remate: losa sobre todo el espesor
                if (dosCaras || T > 0.5f)
                {
                    var posL = aX ? new Vector3(u, suelo + H - 0.37f, 0f) : new Vector3(0f, suelo + H - 0.37f, u);
                    var esc = aX ? new Vector3(tramo / ML, 1f, T / ML) : new Vector3(T / ML, 1f, tramo / ML);
                    if (Poner(raiz, "P_Mod_Losa", posL, 0f, esc) != null) n++;
                }
                // puas arriba
                if (!perimetro || i % 2 == 0)
                {
                    var posP = aX ? new Vector3(u, suelo + H, 0f) : new Vector3(0f, suelo + H, u);
                    if (Poner(raiz, "P_Mod_Valla_Puas", posP, aX ? 0f : 90f, new Vector3(sx, 1f, 1f)) != null) n++;
                }
            }
            // pilares en cada union y en los extremos
            float ps = Mathf.Min((T + 0.4f) / 0.56f, 4f);
            for (int k = 0; k <= cant; k++)
            {
                float u = -L * 0.5f + k * tramo;
                var pos = aX ? new Vector3(u, suelo, 0f) : new Vector3(0f, suelo, u);
                if (Poner(raiz, "P_Mod_Muro_Pilar", pos, 0f, new Vector3(ps, H / MH, ps)) != null) n++;
            }
            if (n > 0) mr.enabled = false; else Object.DestroyImmediate(raiz.gameObject);
            return n;
        }

        // Edificio: el cubo sigue dibujandose; se le suman zocalo, cornisa, parapeto, pilares de esquina y puerta.
        static int VestirEdificio(MeshRenderer mr, string clave, bool conPuerta = true)
        {
            var cubo = mr.transform; var s = cubo.localScale;
            var raiz = Raiz_(cubo, s);
            float suelo = -s.y * 0.5f, H = s.y;
            int n = 0;
            float hZ = Mathf.Min(1.0f, H * 0.18f), hC = Mathf.Min(0.55f, H * 0.1f);

            for (int lado = 0; lado < 4; lado++)
            {
                bool aX = lado < 2;
                float sign = (lado % 2 == 0) ? 1f : -1f;
                float L = aX ? s.x : s.z, D = aX ? s.z : s.x;
                float yaw = aX ? 0f : 90f;
                Vector3 En(float u, float y, float desp) => aX ? new Vector3(u, y, sign * (D * 0.5f + desp)) : new Vector3(sign * (D * 0.5f + desp), y, u);
                float anchoMod = L / ML;

                // zocalo (base ancha)
                if (Poner(raiz, "P_Mod_Muro_Recto", En(0f, suelo, 0.28f), yaw, new Vector3(anchoMod + 0.35f, hZ / MH, 2.1f)) != null) n++;
                // cornisa (resalto arriba)
                if (Poner(raiz, "P_Mod_Muro_Recto", En(0f, suelo + H - hC, 0.34f), yaw, new Vector3(anchoMod + 0.35f, hC / MH, 2.5f)) != null) n++;
                // parapeto sobre el techo
                if (H >= 3f && Poner(raiz, "P_Mod_Muro_Recto", En(0f, suelo + H, -0.28f), yaw, new Vector3(anchoMod, 0.7f / MH, 1.2f)) != null) n++;
                // puerta en la cara que mira al camino central
                bool carasPuerta = aX ? (lado == 0 && s.x >= 6f) : (s.z >= 6f && ((sign > 0f) == (cubo.position.x < 4f)));
                if (conPuerta && carasPuerta && H >= 3f)
                    if (Poner(raiz, "P_Mod_Muro_Puerta", En(0f, suelo, 0.18f), yaw, new Vector3(1f, Mathf.Min(H * 0.62f, 3.3f) / 5f, 1.1f)) != null) n++;
            }
            // pilares de esquina
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    if (Poner(raiz, "P_Mod_Muro_Pilar", new Vector3(sx * (s.x * 0.5f + 0.08f), suelo, sz * (s.z * 0.5f + 0.08f)), 0f, new Vector3(1.7f, (H + 0.1f) / MH, 1.7f)) != null) n++;
            n += VestirTecho(raiz, mr, s);
            if (n == 0) Object.DestroyImmediate(raiz.gameObject);
            return n;
        }

        // ------------------------------------------------------------------
        // Mallas procedurales (guardadas como asset)
        // ------------------------------------------------------------------
        sealed class Gen
        {
            public readonly List<Vector3> v = new List<Vector3>(); public readonly List<int> t = new List<int>(); public readonly List<Vector2> uv = new List<Vector2>();
            // Triangulo con normal plana: vertices propios.
            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.Add(i); t.Add(i + 1); t.Add(i + 2);
                uv.Add(new Vector2(a.x + a.z, a.y)); uv.Add(new Vector2(b.x + b.z, b.y)); uv.Add(new Vector2(c.x + c.z, c.y));
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(a, b, c); Tri(a, c, d); }
            // Orienta el triangulo para que mire lejos de `centro`.
            public void TriFuera(Vector3 a, Vector3 b, Vector3 c, Vector3 centro)
            {
                var n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, (a + b + c) / 3f - centro) < 0f) Tri(a, c, b); else Tri(a, b, c);
            }
            public void QuadFuera(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 centro) { TriFuera(a, b, c, centro); TriFuera(a, c, d, centro); }
            // Gira todo 90 grados alrededor de Y: (x,z) -> (z,-x). Para tener la variante con el eje largo en Z.
            public void GirarY90()
            {
                for (int i = 0; i < v.Count; i++) v[i] = new Vector3(v[i].z, v[i].y, -v[i].x);
                for (int i = 0; i < uv.Count; i++) uv[i] = new Vector2(v[i].x + v[i].z, v[i].y);
            }
            public void Caja(float x0, float x1, float y0, float y1, float z0, float z1)
            {
                var p = new[] { new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1) };
                var cc = (p[0] + p[6]) * 0.5f;
                QuadFuera(p[0], p[1], p[2], p[3], cc); QuadFuera(p[4], p[5], p[6], p[7], cc);
                QuadFuera(p[0], p[1], p[5], p[4], cc); QuadFuera(p[3], p[2], p[6], p[7], cc);
                QuadFuera(p[0], p[3], p[7], p[4], cc); QuadFuera(p[1], p[2], p[6], p[5], cc);
            }
            // Prisma/tronco de cono vertical de `lados` caras (eje Y), con tapas.
            public void Prisma(float cx, float cz, float y0, float y1, float rx0, float rz0, float rx1, float rz1, int lados, bool tapaArriba = true)
            {
                var centro = new Vector3(cx, (y0 + y1) * 0.5f, cz);
                for (int i = 0; i < lados; i++)
                {
                    float a0 = i * Mathf.PI * 2f / lados, a1 = (i + 1) * Mathf.PI * 2f / lados;
                    var b0 = new Vector3(cx + Mathf.Cos(a0) * rx0, y0, cz + Mathf.Sin(a0) * rz0); var b1 = new Vector3(cx + Mathf.Cos(a1) * rx0, y0, cz + Mathf.Sin(a1) * rz0);
                    var t0 = new Vector3(cx + Mathf.Cos(a0) * rx1, y1, cz + Mathf.Sin(a0) * rz1); var t1 = new Vector3(cx + Mathf.Cos(a1) * rx1, y1, cz + Mathf.Sin(a1) * rz1);
                    QuadFuera(b0, b1, t1, t0, centro);
                    TriFuera(new Vector3(cx, y0, cz), b0, b1, centro);
                    if (tapaArriba) TriFuera(new Vector3(cx, y1, cz), t0, t1, centro);
                }
            }
            public Mesh Crear(string nombre)
            {
                var m = new Mesh { name = nombre };
                m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv);
                m.RecalculateNormals(); m.RecalculateBounds();
                return m;
            }
        }

        static Mesh Guardar(Mesh nuevo)
        {
            if (!Directory.Exists(CarpetaMallas)) Directory.CreateDirectory(CarpetaMallas);
            string ruta = $"{CarpetaMallas}/{nuevo.name}.asset";
            var existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
            if (existente == null) { AssetDatabase.CreateAsset(nuevo, ruta); return nuevo; }
            existente.Clear(); EditorUtility.CopySerialized(nuevo, existente);
            Object.DestroyImmediate(nuevo);
            return existente;
        }

        // Ruido de valor suave determinista (sin Mathf.PerlinNoise para que no dependa de la semilla del editor).
        static float Ruido(float x, float y, float z, int semilla)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y), iz = Mathf.FloorToInt(z);
            float fx = x - ix, fy = y - iy, fz = z - iz;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy); fz = fz * fz * (3f - 2f * fz);
            float H(int a, int b, int c) { unchecked { int h = a * 374761393 + b * 668265263 + c * 2147483647 + semilla * 1274126177; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }
            float x00 = Mathf.Lerp(H(ix, iy, iz), H(ix + 1, iy, iz), fx), x10 = Mathf.Lerp(H(ix, iy + 1, iz), H(ix + 1, iy + 1, iz), fx);
            float x01 = Mathf.Lerp(H(ix, iy, iz + 1), H(ix + 1, iy, iz + 1), fx), x11 = Mathf.Lerp(H(ix, iy + 1, iz + 1), H(ix + 1, iy + 1, iz + 1), fx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz);
        }

        // Roca de canadon: bloque de caras facetadas (sin cara inferior), mas angosto arriba, dentro del cubo unidad para que
        // el collider de caja siga conteniendola. Se hacen varias variantes y se reparten por nombre/posicion.
        static Mesh[] MallasDeRoca()
        {
            var res = new Mesh[5];
            for (int k = 0; k < res.Length; k++) res[k] = Guardar(Roca(k + 1));
            return res;
        }

        static Mesh Roca(int semilla)
        {
            const int N = 7;
            var g = new Gen();
            // 5 caras de un cubo [-0.5,0.5]^3 subdividido (sin la de abajo)
            var ejes = new[] { (Vector3.right, Vector3.up, Vector3.forward), (-Vector3.right, Vector3.up, -Vector3.forward), (Vector3.forward, Vector3.up, -Vector3.right), (-Vector3.forward, Vector3.up, Vector3.right), (Vector3.up, Vector3.right, Vector3.forward) };
            var rejilla = new Vector3[N + 1, N + 1];
            foreach (var (n, a, b) in ejes)
            {
                for (int i = 0; i <= N; i++)
                    for (int j = 0; j <= N; j++)
                        rejilla[i, j] = Desplazar(n * 0.5f + a * (i / (float)N - 0.5f) + b * (j / (float)N - 0.5f), semilla);
                for (int i = 0; i < N; i++)
                    for (int j = 0; j < N; j++)
                        g.QuadFuera(rejilla[i, j], rejilla[i + 1, j], rejilla[i + 1, j + 1], rejilla[i, j + 1], new Vector3(0f, 0f, 0f));
            }
            return g.Crear("Op_RocaCanadon_" + semilla);
        }

        static Vector3 Desplazar(Vector3 p, int semilla)
        {
            float alto = p.y + 0.5f;                       // 0 base .. 1 cima
            float afinar = Mathf.Lerp(1f, 0.78f, alto * alto);
            p.x *= afinar; p.z *= afinar;
            var dir = p.normalized;
            float r = (Ruido(p.x * 3.2f + 9f, p.y * 3.2f, p.z * 3.2f, semilla) - 0.5f) * 0.20f
                    + (Ruido(p.x * 7.5f, p.y * 7.5f + 3f, p.z * 7.5f, semilla + 7) - 0.5f) * 0.07f;
            // las bandas horizontales dan estratos
            r += Mathf.Round(alto * 5f) / 5f * 0.0f;
            p += dir * r;
            if (alto < 0.02f) p.y = -0.5f;
            p.x = Mathf.Clamp(p.x, -0.5f, 0.5f); p.y = Mathf.Clamp(p.y, -0.5f, 0.5f); p.z = Mathf.Clamp(p.z, -0.5f, 0.5f);
            return p;
        }

        // Barrera jersey: perfil clasico (base ancha, talud, fuste). Se extruye a lo largo de X o de Z del cubo unidad.
        static Mesh MallaJersey(bool largoEnX)
        {
            var perfil = new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, -0.28f), new Vector2(0.26f, 0.08f), new Vector2(0.20f, 0.5f), new Vector2(-0.20f, 0.5f), new Vector2(-0.26f, 0.08f), new Vector2(-0.5f, -0.28f) };
            var g = new Gen(); var c = Vector3.zero;
            Vector3 V(Vector2 p, float e) => largoEnX ? new Vector3(e, p.y, p.x) : new Vector3(p.x, p.y, e);
            for (int i = 0; i < perfil.Length; i++)
            {
                var a = perfil[i]; var b = perfil[(i + 1) % perfil.Length];
                g.QuadFuera(V(a, -0.5f), V(b, -0.5f), V(b, 0.5f), V(a, 0.5f), c);
            }
            for (int i = 1; i < perfil.Length - 1; i++)
            {
                g.TriFuera(V(perfil[0], -0.5f), V(perfil[i], -0.5f), V(perfil[i + 1], -0.5f), c);
                g.TriFuera(V(perfil[0], 0.5f), V(perfil[i], 0.5f), V(perfil[i + 1], 0.5f), c);
            }
            return Guardar(g.Crear(largoEnX ? "Op_JerseyX" : "Op_JerseyZ"));
        }


        // Baranda de la autopista: viga W arriba y postes cada ~3 m (el cubo mide 0,3 x 1,1 x ~18 m; largo en Z, o en X si largoEnX).
        static Mesh MallaBaranda(bool largoEnX)
        {
            var g = new Gen();
            g.Caja(-0.5f, 0.5f, 0.0f, 0.32f, -0.5f, 0.5f);          // viga: de 0,55 a 0,90 m del suelo sobre 1,1 m
            for (int i = 0; i <= 6; i++)
            {
                float z = -0.5f + i / 6f;
                g.Caja(-0.25f, 0.25f, -0.5f, 0.0f, z - 0.004f, z + 0.004f);   // postes
            }
            if (largoEnX) g.GirarY90();
            return Guardar(g.Crear(largoEnX ? "Op_BarandaAutopistaX" : "Op_BarandaAutopista"));
        }

        // Contenedor de carga 6 x 2,6 x 2,4 m: cuerpo con costillas verticales en las dos caras largas y barras de puerta en un extremo.
        static Mesh MallaContenedor(bool largoEnX)
        {
            var g = new Gen();
            g.Caja(-0.5f, 0.47f, -0.5f, 0.5f, -0.46f, 0.46f);
            for (int i = 0; i < 22; i++)
            {
                float x = -0.47f + i * (0.94f / 21f);
                g.Caja(x - 0.012f, x + 0.012f, -0.46f, 0.46f, 0.46f, 0.5f);
                g.Caja(x - 0.012f, x + 0.012f, -0.46f, 0.46f, -0.5f, -0.46f);
            }
            g.Caja(0.47f, 0.5f, -0.46f, 0.46f, -0.44f, -0.03f); g.Caja(0.47f, 0.5f, -0.46f, 0.46f, 0.03f, 0.44f);   // hojas de la puerta
            g.Caja(-0.5f, 0.5f, 0.46f, 0.5f, -0.5f, 0.5f);                                                          // marco superior
            if (!largoEnX) g.GirarY90();
            return Guardar(g.Crear(largoEnX ? "Op_ContenedorX" : "Op_ContenedorZ"));
        }

        // Camion 7 x 2,8 x 3 m (frente en +X): caja de carga, cabina con parabrisas inclinado, chasis y 6 ruedas.
        static Mesh MallaCamion(bool largoEnX)
        {
            var g = new Gen();
            g.Caja(-0.5f, 0.14f, -0.28f, 0.5f, -0.46f, 0.46f);                 // caja de carga
            g.Caja(-0.5f, 0.5f, -0.34f, -0.26f, -0.38f, 0.38f);                 // chasis
            // cabina: perfil extruido en Z
            var perfil = new[] { new Vector2(0.17f, -0.28f), new Vector2(0.5f, -0.28f), new Vector2(0.5f, 0.0f), new Vector2(0.40f, 0.26f), new Vector2(0.17f, 0.26f) };
            var dentro = new Vector3(0.33f, 0f, 0f);
            for (int i = 0; i < perfil.Length; i++)
            {
                var a = perfil[i]; var b = perfil[(i + 1) % perfil.Length];
                g.QuadFuera(new Vector3(a.x, a.y, -0.46f), new Vector3(b.x, b.y, -0.46f), new Vector3(b.x, b.y, 0.46f), new Vector3(a.x, a.y, 0.46f), dentro);
            }
            for (int i = 1; i < perfil.Length - 1; i++)
            {
                g.TriFuera(new Vector3(perfil[0].x, perfil[0].y, -0.46f), new Vector3(perfil[i].x, perfil[i].y, -0.46f), new Vector3(perfil[i + 1].x, perfil[i + 1].y, -0.46f), dentro);
                g.TriFuera(new Vector3(perfil[0].x, perfil[0].y, 0.46f), new Vector3(perfil[i].x, perfil[i].y, 0.46f), new Vector3(perfil[i + 1].x, perfil[i + 1].y, 0.46f), dentro);
            }
            foreach (float x in new[] { -0.34f, -0.14f, 0.32f })                // ruedas: ejes a lo largo de Z
                foreach (float z in new[] { -0.5f, 0.38f })
                    RuedaZ(g, x, -0.5f + 0.18f, z, z + 0.12f, 0.075f, 0.18f);
            if (!largoEnX) g.GirarY90();
            return Guardar(g.Crear(largoEnX ? "Op_CamionX" : "Op_CamionZ"));
        }

        static void RuedaZ(Gen g, float cx, float cy, float z0, float z1, float rx, float ry)
        {
            const int N = 8; var centro = new Vector3(cx, cy, (z0 + z1) * 0.5f);
            for (int i = 0; i < N; i++)
            {
                float a0 = i * Mathf.PI * 2f / N, a1 = (i + 1) * Mathf.PI * 2f / N;
                var p0 = new Vector2(cx + Mathf.Cos(a0) * rx, cy + Mathf.Sin(a0) * ry); var p1 = new Vector2(cx + Mathf.Cos(a1) * rx, cy + Mathf.Sin(a1) * ry);
                g.QuadFuera(new Vector3(p0.x, p0.y, z0), new Vector3(p1.x, p1.y, z0), new Vector3(p1.x, p1.y, z1), new Vector3(p0.x, p0.y, z1), centro);
                g.TriFuera(new Vector3(cx, cy, z0), new Vector3(p0.x, p0.y, z0), new Vector3(p1.x, p1.y, z0), centro);
                g.TriFuera(new Vector3(cx, cy, z1), new Vector3(p0.x, p0.y, z1), new Vector3(p1.x, p1.y, z1), centro);
            }
        }

        // Fila de racks de servidores 1,4 x 2,8 x 7 m (largo en Z, frentes en +-X): puertas separadas por juntas, zocalo y cubierta.
        static Mesh MallaRack(bool largoEnX)
        {
            var g = new Gen();
            g.Caja(-0.46f, 0.46f, -0.5f, 0.44f, -0.5f, 0.5f);
            g.Caja(-0.5f, 0.5f, -0.5f, -0.44f, -0.5f, 0.5f);                  // zocalo
            g.Caja(-0.5f, 0.5f, 0.44f, 0.5f, -0.5f, 0.5f);                    // cubierta
            const int N = 8;
            for (int i = 0; i < N; i++)
            {
                float z0 = -0.5f + i / (float)N + 0.006f, z1 = -0.5f + (i + 1) / (float)N - 0.006f;
                g.Caja(0.46f, 0.5f, -0.42f, 0.40f, z0, z1);
                g.Caja(-0.5f, -0.46f, -0.42f, 0.40f, z0, z1);
                for (int k = 0; k < 4; k++)                                    // ranuras de ventilacion en cada puerta
                {
                    float y = -0.30f + k * 0.18f;
                    g.Caja(0.5f, 0.505f, y, y + 0.05f, z0 + 0.02f, z1 - 0.02f);
                    g.Caja(-0.505f, -0.5f, y, y + 0.05f, z0 + 0.02f, z1 - 0.02f);
                }
            }
            if (largoEnX) g.GirarY90();
            return Guardar(g.Crear(largoEnX ? "Op_RackX" : "Op_RackZ"));
        }

        // Silo 10 x 12 x 10 m: cuerpo cilindrico de 16 caras con anillos de refuerzo y techo conico.
        static Mesh MallaSilo()
        {
            var g = new Gen();
            g.Prisma(0f, 0f, -0.5f, 0.28f, 0.46f, 0.46f, 0.46f, 0.46f, 16, false);
            foreach (float y in new[] { -0.45f, -0.22f, 0.02f, 0.24f })
                g.Prisma(0f, 0f, y, y + 0.03f, 0.5f, 0.5f, 0.5f, 0.5f, 16, false);
            g.Prisma(0f, 0f, 0.28f, 0.44f, 0.5f, 0.5f, 0.06f, 0.06f, 16, true);
            g.Prisma(0f, 0f, 0.44f, 0.5f, 0.07f, 0.07f, 0.07f, 0.07f, 8, true);   // remate
            return Guardar(g.Crear("Op_Silo"));
        }

        // Autos: el cubo apaga su render y se estira el modelo de auto del kit para llenar la caja.
        static int VestirAuto(MeshRenderer mr)
        {
            var cubo = mr.transform; var s = cubo.localScale;
            var raiz = Raiz_(cubo, s);
            int n = Ajustar(raiz, "P_Env_Auto_Quemado", Vector3.zero, s, -s.y * 0.5f, s.x > s.z ? 90f : 0f);
            if (n > 0) mr.enabled = false; else Object.DestroyImmediate(raiz.gameObject);
            return n;
        }

        // Cajon de cobertura: pila de cajas de madera del kit que llena el cubo (cada caja con un giro distinto para que no parezca una grilla).
        static int VestirCajas(MeshRenderer mr)
        {
            var cubo = mr.transform; var s = cubo.localScale;
            var raiz = Raiz_(cubo, s); float suelo = -s.y * 0.5f; int n = 0;
            int nx = Mathf.Max(1, Mathf.RoundToInt(s.x / 0.94f)), nz = Mathf.Max(1, Mathf.RoundToInt(s.z / 0.94f)), ny = Mathf.Max(1, Mathf.RoundToInt(s.y / 0.77f));
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                    for (int iy = 0; iy < ny; iy++)
                    {
                        var pos = new Vector3(-s.x * 0.5f + (ix + 0.5f) * s.x / nx, suelo + iy * s.y / ny, -s.z * 0.5f + (iz + 0.5f) * s.z / nz);
                        if (Poner(raiz, "P_Env_Caja_Madera", pos, ((ix + iz * 2 + iy * 3) % 4) * 6f, new Vector3(s.x / nx / 0.94f, s.y / ny / 0.77f, s.z / nz / 0.94f)) != null) n++;
                    }
            if (n > 0) mr.enabled = false; else Object.DestroyImmediate(raiz.gameObject);
            return n;
        }

        // Barricada de cemento: bloques jersey del kit corridos a lo largo del cubo.
        static int VestirBarricada(MeshRenderer mr)
        {
            var cubo = mr.transform; var s = cubo.localScale;
            bool aX = s.x >= s.z; float L = aX ? s.x : s.z, T = aX ? s.z : s.x;
            var raiz = Raiz_(cubo, s); float suelo = -s.y * 0.5f; int n = 0;
            int k = Mathf.Max(1, Mathf.RoundToInt(L / 2.34f));
            for (int i = 0; i < k; i++)
            {
                float u = -L * 0.5f + (i + 0.5f) * L / k;
                var pos = aX ? new Vector3(u, suelo, 0f) : new Vector3(0f, suelo, u);
                if (Poner(raiz, "P_Env_Barricada_Concreto", pos, aX ? 0f : 90f, new Vector3(L / k / 2.34f, s.y / 0.86f, Mathf.Min(T / 0.73f, 1.8f))) != null) n++;
            }
            if (n > 0) mr.enabled = false; else Object.DestroyImmediate(raiz.gameObject);
            return n;
        }

        // Estira un modulo para llenar una caja (ancho x alto x largo) centrada en `centro`.
        static int Ajustar(Transform raiz, string prefab, Vector3 centro, Vector3 caja, float suelo, float yaw)
        {
            var g = P(prefab);
            if (g == null) return 0;
            var rs = g.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return 0;
            var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            if (b.size.x < 0.01f || b.size.y < 0.01f || b.size.z < 0.01f) return 0;
            bool girado = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) > 0.5f;
            float cx = girado ? caja.z : caja.x, cz = girado ? caja.x : caja.z;
            return Poner(raiz, prefab, new Vector3(centro.x, suelo, centro.z), yaw, new Vector3(cx / b.size.x, caja.y / b.size.y, cz / b.size.z)) != null ? 1 : 0;
        }
    }
}
