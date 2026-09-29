using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    // Pedido explicito: "expandi el mapa, no debo poder ver el vacio, llena el nivel y poblalo de arboles en el fondo muy
    // lejos asi no veo el vacio cuando roto la vista RTS y el helicoptero de huida debe verse bien".
    //
    // El nivel jugable es un terreno de ~117 x 320 m: desde la camara RTS orbitando (o desde el helicoptero al despegar) se
    // veia el skybox por debajo del horizonte. Este builder rodea el terreno con un SEGUNDO terreno enorme (1600 x 1600 m)
    // que solo hace de fondo:
    //   * plano dentro del rectangulo del nivel y alrededor, y desde ahi sube en lomas suaves hasta ~26 m: el horizonte queda
    //     tapado de cerca y el helicoptero, que sube a 38 m, siempre pasa por encima;
    //   * mismos pastos y mismos arboles que el terreno principal, ~6000 arboles mas densos cerca del borde y mas ralos lejos;
    //   * sin collider (no lo tocan rayos de punteria ni fisica) y con NavMeshModifier ignoreFromBuild (el NavMeshSurface
    //     recolecta toda la escena: sin esto el rehorneado lo tomaria como piso caminable).
    // Idempotente: reemplaza el anterior. Correr una vez con SC_Gameplay abierta.
    public static class FondoDelMundoBuilder
    {
        const string NombreObjeto = "Terrain_Fondo";
        const string RutaDatos = "Assets/_Project/Terrains/TerrainData_Fondo.asset";
        const float Lado = 1600f, AlturaMax = 40f, AlturaLomas = 26f, BajoElPiso = 0.12f;
        const int Resolucion = 513, CantidadDeArboles = 6000;

        [MenuItem("Strategic Point/Arte/10. Fondo del mundo (terreno lejano + arboles)")]
        public static void Construir()
        {
            var principal = Terrain.activeTerrains.FirstOrDefault(t => t.name == "Terrain_Main");
            if (principal == null) { Debug.LogWarning("[FondoDelMundo] Abri SC_Gameplay (no hay Terrain_Main)."); return; }

            var previo = GameObject.Find(NombreObjeto);
            if (previo != null) Object.DestroyImmediate(previo);
            AssetDatabase.DeleteAsset(RutaDatos);

            var pp = principal.transform.position;
            var ps = principal.terrainData.size;
            float xMin = pp.x, xMax = pp.x + ps.x, zMin = pp.z, zMax = pp.z + ps.z;
            var centro = new Vector2((xMin + xMax) * 0.5f, (zMin + zMax) * 0.5f);

            var td = new TerrainData { heightmapResolution = Resolucion };
            td.size = new Vector3(Lado, AlturaMax, Lado);
            var origen = new Vector3(centro.x - Lado * 0.5f, -BajoElPiso, centro.y - Lado * 0.5f);

            // --- relieve: lomas que suben con la distancia al rectangulo del nivel
            var alturas = new float[Resolucion, Resolucion];
            for (int iz = 0; iz < Resolucion; iz++)
                for (int ix = 0; ix < Resolucion; ix++)
                {
                    float x = origen.x + Lado * ix / (Resolucion - 1f);
                    float z = origen.z + Lado * iz / (Resolucion - 1f);
                    float d = DistanciaAlRectangulo(x, z, xMin, xMax, zMin, zMax);
                    float subida = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(70f, 380f, d));
                    float grande = 0.62f + 0.38f * Mathf.PerlinNoise(x * 0.0035f + 11f, z * 0.0035f + 5f);
                    float chico = (Mathf.PerlinNoise(x * 0.03f, z * 0.03f) - 0.5f) * 2.4f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(25f, 120f, d));
                    float h = AlturaLomas * subida * grande + chico;
                    alturas[iz, ix] = Mathf.Clamp01(Mathf.Max(0f, h) / AlturaMax);
                }
            td.SetHeights(0, 0, alturas);

            // --- pasto del terreno principal
            var capas = principal.terrainData.terrainLayers;
            if (capas != null && capas.Length > 0) td.terrainLayers = new[] { capas[0] };

            // --- mismos arboles que el principal, mas densos cerca del borde
            var protos = principal.terrainData.treePrototypes;
            td.treePrototypes = protos.Select(p => new TreePrototype { prefab = p.prefab }).ToArray();
            var rnd = new System.Random(20260929);
            var arboles = new System.Collections.Generic.List<TreeInstance>(CantidadDeArboles);
            int intentos = 0;
            while (arboles.Count < CantidadDeArboles && intentos++ < CantidadDeArboles * 60)
            {
                float x = origen.x + (float)rnd.NextDouble() * Lado;
                float z = origen.z + (float)rnd.NextDouble() * Lado;
                float d = DistanciaAlRectangulo(x, z, xMin, xMax, zMin, zMax);
                if (d < 6f) continue;
                if (rnd.NextDouble() > Mathf.Exp(-d / 300f)) continue;
                float escala = 0.85f + (float)rnd.NextDouble() * 0.9f;
                arboles.Add(new TreeInstance
                {
                    position = new Vector3((x - origen.x) / Lado, 0f, (z - origen.z) / Lado),
                    prototypeIndex = rnd.Next(protos.Length),
                    widthScale = escala, heightScale = escala * (0.9f + (float)rnd.NextDouble() * 0.3f),
                    rotation = (float)rnd.NextDouble() * Mathf.PI * 2f,
                    color = Color.white, lightmapColor = Color.white,
                });
            }
            td.SetTreeInstances(arboles.ToArray(), true);

            AssetDatabase.CreateAsset(td, RutaDatos);
            var go = Terrain.CreateTerrainGameObject(td);
            go.name = NombreObjeto;
            go.transform.position = origen;
            var padre = GameObject.Find("Environment");
            if (padre != null) go.transform.SetParent(padre.transform, true);
            var col = go.GetComponent<TerrainCollider>();
            if (col != null) Object.DestroyImmediate(col);

            var terreno = go.GetComponent<Terrain>();
            terreno.materialTemplate = principal.materialTemplate;
            terreno.treeDistance = 900f;
            terreno.treeBillboardDistance = 900f;
            terreno.drawInstanced = true;
            terreno.heightmapPixelError = 24f;
            terreno.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            var mod = go.GetComponent<Unity.AI.Navigation.NavMeshModifier>();
            if (mod == null) mod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
            mod.ignoreFromBuild = true;

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log($"[FondoDelMundo] Terreno de fondo {Lado} m con {arboles.Count} arboles, lomas de hasta {AlturaLomas} m.");
        }

        static float DistanciaAlRectangulo(float x, float z, float xMin, float xMax, float zMin, float zMax)
        {
            float dx = Mathf.Max(Mathf.Max(xMin - x, 0f), x - xMax);
            float dz = Mathf.Max(Mathf.Max(zMin - z, 0f), z - zMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
