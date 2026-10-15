using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    // Arte y entorno para el Nivel 1 (SC_Gameplay) y el Tutorial (SC_Tutorial), igual que el "9_ArteExtra" del Nivel 2:
    // arboledas en los bordes, campamentos abandonados, crateres y escombros, y parches grandes de suelo (macro-variacion).
    // Se corre sobre la escena ABIERTA (reconstruir los niveles tarda mucho) y es idempotente: reemplaza el grupo "ArteExtra".
    // Reglas heredadas de los pulidos anteriores: nada a menos de 6 m de un soldado/waypoint/vehiculo/marcador de mision, los props con
    // colision no entran al NavMesh, los decals de suelo van a 7,5-9 cm del piso y las luces decorativas no proyectan sombra.
    public static partial class OperacionBuilder
    {
        public static string ArteExtraNivelEscenaAbierta()
        {
            var escena = EditorSceneManager.GetActiveScene();
            string nombre = escena.name;
            bool tutorial = nombre == "SC_Tutorial";
            if (nombre != "SC_Gameplay" && !tutorial) return "escena no soportada: " + nombre;

            var viejo = GameObject.Find("ArteExtra");
            if (viejo != null) Object.DestroyImmediate(viejo);
            var raiz = new GameObject("ArteExtra");
            var g = raiz.transform;

            // Zona jugable (de las mediciones del nivel) y cantidades.
            float x0 = tutorial ? -32f : -52f, x1 = tutorial ? 32f : 60f, z0 = tutorial ? -26f : -18f, z1 = tutorial ? 176f : 292f;
            int nArboledas = tutorial ? 7 : 20, nCampamentos = tutorial ? 6 : 14, nCrateres = tutorial ? 12 : 30, nParches = tutorial ? 70 : 190;

            // Puntos a respetar: todo lo que se mueve o marca algo del juego.
            var evitar = new List<Vector3>();
            foreach (var n in new[] { "Waypoints", "Enemies", "PlayerSquad", "Vehicles", "UI_World", "Mision", "Tutorial", "CaminoLights", "Estrategia" })
            {
                var o = GameObject.Find(n);
                if (o == null) continue;
                foreach (var t in o.GetComponentsInChildren<Transform>(true)) evitar.Add(t.position);
            }
            Physics.SyncTransforms();
            var rnd = new System.Random(tutorial ? 20261016 : 20261017);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            bool CercaDeAlgo(Vector3 p, float radio)
            {
                float r2 = radio * radio;
                for (int i = 0; i < evitar.Count; i++)
                {
                    float dx = evitar[i].x - p.x, dz = evitar[i].z - p.z;
                    if (dx * dx + dz * dz < r2) return true;
                }
                return false;
            }
            bool EsSuelo(Vector3 p, out float y)
            {
                y = 0f;
                if (!Physics.Raycast(new Vector3(p.x, 3f, p.z), Vector3.down, out var h, 6f, ~0, QueryTriggerInteraction.Ignore)) return false;
                y = h.point.y;
                return h.normal.y >= 0.95f && h.point.y < 0.6f;
            }

            // a) arboledas en las bandas laterales (el centro es el camino de combate)
            var gVerde = Grupo(g, "Arboledas");
            string[] verde = { "P_Env_ArbolA", "P_Env_ArbolB", "P_Env_Arbusto_Grande", "P_Env_Arbusto_Medio", "P_Env_Arbusto_Chico", "P_Env_Arbusto_Medio" };
            float banda = tutorial ? 8f : 11f;
            int nVerde = 0, bosquecillos = 0;
            for (int i = 0, intentos = 0; i < nArboledas && intentos < 1500; intentos++)
            {
                bool izq = rnd.Next(2) == 0;
                var c = new Vector3(izq ? R(x0, x0 + banda) : R(x1 - banda, x1), 0f, R(z0, z1));
                if (!EsSuelo(c, out _) || CercaDeAlgo(c, 6f)) continue;
                int cuantas = 3 + rnd.Next(3), puestas = 0;
                for (int k = 0; k < cuantas; k++)
                {
                    var pos = c + new Vector3(R(-4f, 4f), 0f, R(-4f, 4f));
                    int ix = rnd.Next(verde.Length);
                    float esc = ix < 2 ? R(1.2f, 1.8f) : R(0.9f, 1.4f);
                    if (pos.x < x0 || pos.x > x1 || !EsSuelo(pos, out float y) || CercaDeAlgo(pos, 6f) || !LugarLibre(pos, 1.0f * esc, 1.0f * esc, 0f, 3f)) continue;
                    pos.y = y;
                    if (PonerArte(gVerde, verde[ix], pos, R(0f, 360f), esc, true) != null) { nVerde++; puestas++; Physics.SyncTransforms(); }
                }
                if (puestas > 0) { i++; bosquecillos++; }
            }

            // b) campamentos abandonados (cobertura baja a escala real: sacos x1,4 de alto, concreto x1,2, barriles finos)
            var gCamp = Grupo(g, "Campamentos");
            string[] kit = { "P_Env_Barricada_Sacos", "P_Env_Caja_Madera", "P_Env_Caja_Madera", "P_Env_Barril", "P_Env_Neumaticos", "P_Env_Palet", "P_Env_Farol", "P_Env_Barricada_Concreto" };
            float[] ek = { 1.2f, 1.2f, 1.1f, 0.5f, 1.1f, 1f, 1.1f, 1.2f };
            int nCamp = 0, campamentos = 0;
            for (int i = 0, intentos = 0; i < nCampamentos && intentos < 1200; intentos++)
            {
                var c = new Vector3(R(x0 + 3f, x1 - 3f), 0f, R(z0, z1));
                if (!EsSuelo(c, out _) || CercaDeAlgo(c, 9f)) continue;
                int puestas = 0;
                for (int k = 0; k < 6; k++)
                {
                    var pos = c + new Vector3(R(-3.5f, 3.5f), 0f, R(-3.5f, 3.5f));
                    int ix = rnd.Next(kit.Length);
                    float yaw = R(0f, 360f);
                    if (!EsSuelo(pos, out float y) || CercaDeAlgo(pos, 7f) || !LugarLibre(pos, 1.0f * ek[ix], 1.0f * ek[ix], yaw, 1.4f)) continue;
                    pos.y = y;
                    var go = PonerArte(gCamp, kit[ix], pos, yaw, ek[ix], true);
                    if (go == null) continue;
                    if (kit[ix].StartsWith("P_Env_Barricada_Sacos")) go.transform.localScale = new Vector3(ek[ix], ek[ix] * 1.4f, ek[ix] * 1.5f);
                    nCamp++; puestas++; Physics.SyncTransforms();
                }
                if (puestas > 0) { i++; campamentos++; }
            }

            // c) crateres y escombros sueltos (sin colision: los prefabs ya no la traen)
            var gCrat = Grupo(g, "Crateres");
            string[] danio = { "P_Env_Crater", "P_Env_Crater", "P_Env_Escombro_Pila_A", "P_Env_Escombro_Pila_B", "P_Env_Muro_Caido" };
            float[] ed = { 1.3f, 1.8f, 1.2f, 1.2f, 1.2f };
            int nCrat = 0;
            for (int i = 0, intentos = 0; i < nCrateres && intentos < 1200; intentos++)
            {
                var pos = new Vector3(R(x0, x1), 0f, R(z0, z1));
                int ix = rnd.Next(danio.Length);
                if (!EsSuelo(pos, out float y) || CercaDeAlgo(pos, 5f) || !LugarLibre(pos, 1.6f * ed[ix], 1.6f * ed[ix], 0f, 1.2f)) continue;
                pos.y = y;
                if (PonerArte(gCrat, danio[ix], pos, R(0f, 360f), ed[ix], true) != null) { i++; nCrat++; Physics.SyncTransforms(); }
            }

            // d) parches grandes de suelo (macro-variacion sin juntas), sobre el piso mas alto y junto a los decals del juego
            var gSuelo = Grupo(g, "ParchesDeSuelo");
            string[] tipos = { "Parche", "Parche", "Grava", "Polvo", "Arena", "Mancha", "Huellas" };
            int nSuelo = 0;
            for (int i = 0, intentos = 0; i < nParches && intentos < 4000; intentos++)
            {
                var p = new Vector3(R(x0, x1), 0f, R(z0, z1));
                if (!EsSuelo(p, out float y)) continue;
                if (Physics.CheckSphere(new Vector3(p.x, y + 0.6f, p.z), 0.4f, ~0, QueryTriggerInteraction.Ignore)) continue;   // algo encima (prop, edificio)
                string tipo = tipos[rnd.Next(tipos.Length)];
                float ancho = tutorial ? R(3f, 7f) : R(3.5f, 9f), alto = ancho * R(0.6f, 1f);
                var pos = new Vector3(p.x, y + 0.075f + (float)rnd.NextDouble() * 0.015f, p.z);
                if (PonerDecal(gSuelo, tipo, pos, Vector3.up, R(0f, 360f), ancho, alto) != null) { i++; nSuelo++; }
            }

            // Las luces decorativas no proyectan sombra (178 -> 226 mapas de sombra con 85 faroles extra en el Nivel 2).
            int luces = 0;
            foreach (var l in g.GetComponentsInChildren<Light>(true)) { if (l.shadows != LightShadows.None) { l.shadows = LightShadows.None; luces++; } }
            foreach (var t in g.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            AssetDatabase.SaveAssets();
            return $"{nombre}: {nVerde} plantas en {bosquecillos} arboledas, {nCamp} props en {campamentos} campamentos, {nCrat} crateres/escombros, {nSuelo} parches de suelo, {luces} luces sin sombra";
        }
    }
}
