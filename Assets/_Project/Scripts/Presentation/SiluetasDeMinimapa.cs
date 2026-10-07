using System.Collections.Generic;
using UnityEngine;
using SP.Core;

namespace SP.Presentation
{
    // Marcador opcional: un builder puede agregarlo a cualquier objeto con BoxCollider para que su silueta aparezca en el minimapa
    // aunque el nombre no entre en la lista de SiluetasDeMinimapa.
    public class SiluetaMinimapa : MonoBehaviour { }

    // Bug #065 (minimapa sin muros): los muros, bordes y paredes del nivel se arman con cajas sin ObstacleMarker, asi que
    // MinimapIcon.RegistrarObstaculos no les daba icono y el mapa no mostraba el perimetro ni las paredes del cuartel. Aca se les arma
    // una SILUETA: por cada BoxCollider solido (no trigger) con nombre de muro, un rectangulo con la huella REAL (caja orientada con
    // su yaw, no el AABB) en la capa Minimap, al mismo gris claro de los obstaculos. Todos van en UN solo mesh y UN solo material
    // (un lote), a y=40: debajo de los iconos de unidades (55) y de la camara del minimapa. No usa ObstacleMarker a proposito: eso
    // generaria coberturas tacticas en los bordes de 780 m. Se llama al arrancar la escena (GameplaySceneBootstrap); es idempotente.
    public static class SiluetasDeMinimapa
    {
        public const string NombreRaiz = "SiluetasMinimapaRoot";
        public const float Altura = 40f;
        const float LadoMinimo = 1.2f;

        // Raices de escena que se recorren y prefijos de nombre (distingue mayusculas) que cuentan como "muro".
        static readonly string[] Raices = { "Nivel_Blockout", "Operacion" };
        static readonly string[] Prefijos = { "Muro", "Borde", "Valle", "Cuartel", "Cierre", "CD_", "Porton", "Barrera", "Puesto", "Brecha", "Pilar", "Portico" };

        static readonly List<string> nombres = new List<string>();
        public static IReadOnlyList<string> Nombres => nombres;
        public static int Cantidad => nombres.Count;
        public static GameObject Raiz { get; private set; }

        static bool EsMuro(string n)
        {
            foreach (var p in Prefijos) if (n.StartsWith(p, System.StringComparison.Ordinal)) return true;
            return false;
        }

        // Devuelve cuantas siluetas armo.
        public static int Registrar()
        {
            nombres.Clear();
            var previo = RaicesDeEscena.Buscar(NombreRaiz);
            if (previo != null) { if (Application.isPlaying) Object.Destroy(previo); else Object.DestroyImmediate(previo); }
            Raiz = null;

            var verts = new List<Vector3>();
            var tris = new List<int>();
            foreach (var nombreRaiz in Raices)
            {
                var raiz = RaicesDeEscena.Buscar(nombreRaiz);
                if (raiz == null) continue;
                foreach (var bc in raiz.GetComponentsInChildren<BoxCollider>(false))
                {
                    if (bc == null || bc.isTrigger || !bc.enabled) continue;
                    bool marcado = bc.GetComponent<SiluetaMinimapa>() != null;
                    if (!marcado && !EsMuro(bc.name)) continue;
                    if (bc.GetComponent<ObstacleMarker>() != null) continue;   // ya tiene su icono de obstaculo
                    Agregar(bc, verts, tris);
                }
            }
            if (nombres.Count == 0) return 0;

            int layer = LayerMask.NameToLayer("Minimap");
            if (layer < 0) layer = 8;   // TagManager trae "Minimap" fijo en el indice 8.

            var mesh = new Mesh { name = "SiluetasMinimapa", hideFlags = HideFlags.HideAndDontSave };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            var normales = new Vector3[verts.Count];
            for (int i = 0; i < normales.Length; i++) normales[i] = Vector3.up;
            mesh.normals = normales;
            mesh.RecalculateBounds();

            var go = new GameObject(NombreRaiz) { layer = layer };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = DiamondGizmo.NuevoMaterial(MinimapIcon.ObstacleMinimapColor);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Raiz = go;
            return nombres.Count;
        }

        // Cuatro esquinas de la caja en planta (con el yaw y la escala reales del objeto) como un quad a y=Altura.
        static void Agregar(BoxCollider bc, List<Vector3> verts, List<int> tris)
        {
            var t = bc.transform;
            var c = bc.center; var h = bc.size * 0.5f;
            var e = new Vector3[4];
            e[0] = t.TransformPoint(new Vector3(c.x - h.x, c.y, c.z - h.z));
            e[1] = t.TransformPoint(new Vector3(c.x - h.x, c.y, c.z + h.z));
            e[2] = t.TransformPoint(new Vector3(c.x + h.x, c.y, c.z + h.z));
            e[3] = t.TransformPoint(new Vector3(c.x + h.x, c.y, c.z - h.z));
            for (int i = 0; i < 4; i++) e[i].y = Altura;

            // Las cajas finas (muros de 0.3 m) se ensanchan hasta un lado minimo para que no desaparezcan en el mapa.
            var centro = (e[0] + e[2]) * 0.5f;
            var ejeA = e[3] - e[0]; var ejeB = e[1] - e[0];
            if (ejeA.magnitude < LadoMinimo && ejeA.sqrMagnitude > 1e-6f) { var d = ejeA.normalized * LadoMinimo; e[0] = centro - d * 0.5f - ejeB * 0.5f; e[3] = centro + d * 0.5f - ejeB * 0.5f; e[1] = centro - d * 0.5f + ejeB * 0.5f; e[2] = centro + d * 0.5f + ejeB * 0.5f; ejeA = e[3] - e[0]; ejeB = e[1] - e[0]; }
            if (ejeB.magnitude < LadoMinimo && ejeB.sqrMagnitude > 1e-6f) { var d = ejeB.normalized * LadoMinimo; e[0] = centro - ejeA * 0.5f - d * 0.5f; e[3] = centro + ejeA * 0.5f - d * 0.5f; e[1] = centro - ejeA * 0.5f + d * 0.5f; e[2] = centro + ejeA * 0.5f + d * 0.5f; }

            int b = verts.Count;
            verts.AddRange(e);
            // Dos caras (el material no culea pero asi no depende del sentido de giro de la caja).
            tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3, b, b + 2, b + 1, b, b + 3, b + 2 });
            nombres.Add(bc.name);
        }
    }
}
