using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    // Pulido de SC_Operacion (parte 3), pensado para correr sobre la escena YA construida (reconstruirla entera tarda varios minutos):
    //  1) pisos: se sacan las losetas del kit (dejaban juntas oscuras a intervalos fijos = "mal tileo") y el cubo vuelve a dibujarse liso;
    //  2) rotulos: los que sobreviven quedan con una sola linea corta y el de "RESERVA" se elimina;
    //  3) arte extra: arboledas, campamentos, crateres y parches grandes de suelo (macro-variacion) para romper la repeticion.
    // Todo el arte extra es solo visual o con colision chica, lejos del eje de la ruta del tanque.
    public static partial class OperacionBuilder
    {
        static readonly Regex rePisoRetoque = new Regex(@"^(Patio_|Piso|CD_Piso|Plaza|Asfalto_Corredor|Camino|Calle)", RegexOptions.Compiled);

        public static string RetocarEscenaAbierta()
        {
            var activa = EditorSceneManager.GetActiveScene();
            var o = GameObject.Find("Operacion");
            if (o == null) return "no hay un objeto 'Operacion' en " + activa.name;
            Physics.SyncTransforms();
            int pisos = QuitarLosetas(o.transform);
            string rot = RetocarRotulos(o.transform);
            string extra = ArteExtra(o.transform);
            EditorSceneManager.MarkSceneDirty(activa);
            EditorSceneManager.SaveScene(activa);
            AssetDatabase.SaveAssets();
            return $"{pisos} pisos sin losetas; rotulos: {rot}; {extra}";
        }

        // Los faroles del kit traen una luz puntual CON sombra: 85 faroles extra llevaron el atlas de sombras de 178 a 226 mapas (el motor
        // baja la resolucion de todas). Las luces del arte decorativo no proyectan sombra; las del juego (torretas, alarmas) no se tocan.
        public static string SombrasDeFarolesEscenaAbierta()
        {
            var o = GameObject.Find("Operacion");
            if (o == null) return "no hay 'Operacion'";
            int n = 0;
            foreach (var l in o.GetComponentsInChildren<Light>(true))
            {
                if (l.shadows == LightShadows.None) continue;
                bool decorativa = false;
                for (var t = l.transform; t != null && t != o.transform; t = t.parent)
                    if (t.name == "7_Arte" || t.name == "9_ArteExtra") { decorativa = true; break; }
                if (!decorativa) continue;
                l.shadows = LightShadows.None; n++;
            }
            var activa = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activa); EditorSceneManager.SaveScene(activa);
            return n + " luces decorativas sin sombra";
        }

        public static void HornearNavMeshEscenaAbierta() => LevelBlockoutBuilder.BakeNavMesh(true);

        // 1) Pisos -------------------------------------------------------------
        static int QuitarLosetas(Transform raiz)
        {
            var aBorrar = new List<GameObject>();
            var cubos = new List<MeshRenderer>();
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "ArteOp" || t.parent == null) continue;
                var cubo = t.parent;
                var mr = cubo.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                var s = cubo.localScale;
                string clave = Regex.Replace(cubo.name, @"\s*\(\d+\)$", "");
                if (!rePisoRetoque.IsMatch(clave) || s.y > 0.7f || Mathf.Min(s.x, s.z) < 3f) continue;
                aBorrar.Add(t.gameObject); cubos.Add(mr);
            }
            foreach (var go in aBorrar) Object.DestroyImmediate(go);
            // Bug #225 (z-fighting entre pisos coplanares): solo se separan los pisos que se SOLAPAN, de a 1,2 cm por nivel (max 3 niveles) y las
            // calles quedan sobre los patios. Un piso subido mas de ~6 cm taparia los decals del juego (a 7,5-9 cm): por eso no hay un escalon fijo.
            var lista = new List<MeshRenderer>();
            foreach (var mr in cubos) if (mr != null) { mr.enabled = true; lista.Add(mr); }
            bool EsCalle(MeshRenderer m) => m.name.StartsWith("Calle") || m.name.StartsWith("Camino") || m.name.StartsWith("Asfalto");
            float Area(MeshRenderer m) => m.bounds.size.x * m.bounds.size.z;
            lista.Sort((x, y) => { int c = EsCalle(x).CompareTo(EsCalle(y)); return c != 0 ? c : Area(y).CompareTo(Area(x)); });
            var nivel = new Dictionary<MeshRenderer, int>();
            int n = 0;
            foreach (var mr in lista)
            {
                int lv = 0; var b = mr.bounds;
                foreach (var kv in nivel)
                {
                    var o = kv.Key.bounds;
                    if (o.min.x >= b.max.x || o.max.x <= b.min.x || o.min.z >= b.max.z || o.max.z <= b.min.z) continue;
                    if (Mathf.Abs(o.max.y - b.max.y) > 0.06f) continue;
                    lv = Mathf.Max(lv, kv.Value + 1);
                }
                lv = Mathf.Min(lv, 3);
                nivel[mr] = lv;
                if (lv > 0) { var p = mr.transform.position; mr.transform.position = new Vector3(p.x, p.y + lv * 0.012f, p.z); }
                n++;
            }
            return n;
        }

        // 2) Rotulos -----------------------------------------------------------
        static string RetocarRotulos(Transform raiz)
        {
            int cortados = 0, movidos = 0, borrados = 0;
            var aBorrar = new List<GameObject>();
            foreach (var tm in raiz.GetComponentsInChildren<TextMesh>(true))
            {
                var r = tm.transform.parent; if (r == null || !r.name.StartsWith("Rotulo_")) continue;
                string txt = tm.text;
                if (txt == "RESERVA") { aBorrar.Add(r.gameObject); continue; }
                if (txt.StartsWith("PUESTO ")) { r.position = new Vector3(r.position.x, 4.8f, r.position.z); movidos++; }
                else if (txt.StartsWith("CARGA ")) { r.position = new Vector3(r.position.x, 1.8f, r.position.z); movidos++; }
                int nl = txt.IndexOf('\n');
                if (nl > 0)
                {
                    int lineas = txt.Split('\n').Length;
                    tm.text = txt.Substring(0, nl);
                    r.name = "Rotulo_" + tm.text;
                    var placa = r.Find("Placa");
                    if (placa != null) placa.localScale = new Vector3(placa.localScale.x, placa.localScale.y / lineas, placa.localScale.z);
                    cortados++;
                }
            }
            foreach (var go in aBorrar) { Object.DestroyImmediate(go); borrados++; }
            return $"{cortados} cortados a una linea, {movidos} movidos, {borrados} eliminados";
        }

        // 3) Arte extra --------------------------------------------------------
        static string ArteExtra(Transform op)
        {
            var viejo = op.Find("9_ArteExtra");
            if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
            var g = Grupo(op, "9_ArteExtra");
            var rutaT = op.Find("5_Autopista/RutaTanque");
            var pts = new List<Vector2>();
            if (rutaT != null) foreach (Transform w in rutaT) pts.Add(new Vector2(w.position.x, w.position.z));
            var ptsA = pts.ToArray();
            var rnd = new System.Random(20261015);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            float DRuta(Vector3 p) => ptsA.Length > 1 ? DistanciaAPolilinea2(ptsA, new Vector2(p.x, p.z)) : 999f;
            bool EnElRio(Vector3 q) => q.x > -160f && q.x < 30f && q.z > 296f;
            bool EnLaCiudad(Vector3 q) => q.x > 195f && q.z > 120f;

            // a) arboledas: grupos de 3-6 plantas a 14-60 m de la ruta (fuera de la ciudad y del rio)
            var gVerde = Grupo(g, "Arboledas");
            string[] verde = { "P_Env_ArbolA", "P_Env_ArbolB", "P_Env_Arbusto_Grande", "P_Env_Arbusto_Medio", "P_Env_Arbusto_Chico", "P_Env_Arbusto_Medio" };
            int nVerde = 0, bosquecillos = 0;
            for (int i = 0, intentos = 0; i < 46 && intentos < 1200; intentos++)
            {
                var c = new Vector3(R(-370f, 370f), 0f, R(40f, 370f));
                float d = DRuta(c);
                if (d < 16f || d > 64f || EnElRio(c) || EnLaCiudad(c)) continue;
                int cuantas = 3 + rnd.Next(4), puestas = 0;
                for (int k = 0; k < cuantas; k++)
                {
                    var pos = c + new Vector3(R(-6f, 6f), 0f, R(-6f, 6f));
                    int ix = rnd.Next(verde.Length);
                    float esc = ix < 2 ? R(1.3f, 2.1f) : R(1.0f, 1.6f);
                    if (DRuta(pos) < 12f || !LugarLibre(pos, 1.1f * esc, 1.1f * esc, 0f, 3f)) continue;
                    if (PonerArte(gVerde, verde[ix], pos, R(0f, 360f), esc, true) != null) { nVerde++; puestas++; Physics.SyncTransforms(); }
                }
                if (puestas > 0) { i++; bosquecillos++; }
            }

            // b) campamentos abandonados: sacos + cajas + barriles + farol en un radio de 4 m, a 12-50 m de la ruta
            var gCamp = Grupo(g, "Campamentos");
            string[] kit = { "P_Env_Barricada_Sacos", "P_Env_Caja_Madera", "P_Env_Caja_Madera", "P_Env_Barril", "P_Env_Neumaticos", "P_Env_Palet", "P_Env_Farol", "P_Env_Barricada_Concreto" };
            float[] ek = { 1.2f, 1.2f, 1.1f, 0.5f, 1.1f, 1f, 1.1f, 1.2f };
            int nCamp = 0, campamentos = 0;
            for (int i = 0, intentos = 0; i < 24 && intentos < 900; intentos++)
            {
                var c = new Vector3(R(-370f, 370f), 0f, R(40f, 370f));
                float d = DRuta(c);
                if (d < 12f || d > 50f || EnElRio(c)) continue;
                int puestas = 0;
                for (int k = 0; k < 6; k++)
                {
                    var pos = c + new Vector3(R(-4f, 4f), 0f, R(-4f, 4f));
                    int ix = rnd.Next(kit.Length);
                    float yaw = R(0f, 360f);
                    if (DRuta(pos) < 10f || !LugarLibre(pos, 1.0f * ek[ix], 1.0f * ek[ix], yaw, 1.4f)) continue;
                    if (PonerArte(gCamp, kit[ix], pos, yaw, ek[ix], true) != null) { nCamp++; puestas++; Physics.SyncTransforms(); }
                }
                if (puestas > 0) { i++; campamentos++; }
            }

            // c) crateres y escombros sueltos en el campo y la ciudad
            var gCrat = Grupo(g, "Crateres");
            string[] danio = { "P_Env_Crater", "P_Env_Crater", "P_Env_Escombro_Pila_A", "P_Env_Escombro_Pila_B", "P_Env_Muro_Caido" };
            float[] ed = { 1.3f, 1.8f, 1.2f, 1.2f, 1.2f };
            int nCrat = 0;
            for (int i = 0, intentos = 0; i < 40 && intentos < 900; intentos++)
            {
                var pos = new Vector3(R(-370f, 370f), 0f, R(30f, 378f));
                bool ciudad = EnLaCiudad(pos);
                if (EnElRio(pos) || (!ciudad && DRuta(pos) < 11f)) continue;
                int ix = rnd.Next(danio.Length);
                if (!LugarLibre(pos, 1.6f * ed[ix], 1.6f * ed[ix], 0f, 1.2f)) continue;
                if (PonerArte(gCrat, danio[ix], pos, R(0f, 360f), ed[ix], true) != null) { i++; nCrat++; Physics.SyncTransforms(); }
            }

            // d) parches grandes de suelo: rompen la repeticion de color de los pisos lisos (macro-variacion, sin juntas)
            var gSuelo = Grupo(g, "ParchesDeSuelo");
            string[] tipos = { "Parche", "Parche", "Grava", "Polvo", "Arena", "Mancha", "Huellas" };
            int nSuelo = 0;
            for (int i = 0, intentos = 0; i < 320 && intentos < 4000; intentos++)
            {
                var p = new Vector3(R(-376f, 376f), 0f, R(-376f, 376f));
                if (EnElRio(p)) continue;
                if (!Physics.Raycast(new Vector3(p.x, 3f, p.z), Vector3.down, out var h, 6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (h.normal.y < 0.95f || h.point.y > 0.6f) continue;            // solo suelo llano (no techos ni rampas)
                string tipo = tipos[rnd.Next(tipos.Length)];
                float ancho = R(3.5f, 9f), alto = ancho * R(0.6f, 1f);
                var pos = new Vector3(p.x, 0.07f + (float)rnd.NextDouble() * 0.02f, p.z);   // sobre el piso mas alto (<= 6 cm) y junto a los decals del juego
                var d = PonerDecal(gSuelo, tipo, pos, Vector3.up, R(0f, 360f), ancho, alto);
                if (d != null) { i++; nSuelo++; }
            }
            return $"arte extra: {nVerde} plantas en {bosquecillos} arboledas, {nCamp} props en {campamentos} campamentos, {nCrat} crateres/escombros, {nSuelo} parches de suelo";
        }
    }
}
