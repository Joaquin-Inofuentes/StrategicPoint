using UnityEngine;

namespace SP.Presentation
{
    public class InteractableDiamond : MonoBehaviour
    {
        public System.Func<bool> Condicion;
        GameObject marcador;
        Material matInterior;
        bool enSuelo;   // bug #192: el rombo de la municion caida va acostado en el piso, sin girar hacia la camara

        public static InteractableDiamond Agregar(Transform padre, Vector3 localOffset, Color color, bool enSuelo = false, float escala = 1f)
        {
            var go = new GameObject("InteractableDiamond");
            go.transform.SetParent(padre, false);
            go.transform.localPosition = localOffset;
            var rombo = go.AddComponent<InteractableDiamond>();
            rombo.enSuelo = enSuelo;

            rombo.marcador = new GameObject("Rombo");
            rombo.marcador.transform.SetParent(go.transform, false);
            rombo.marcador.transform.localScale = Vector3.one * escala;
            if (enSuelo) rombo.marcador.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            DiamondGizmo.CrearCara("Borde", rombo.marcador.transform, 0.4f, DiamondGizmo.NuevoMaterial(Color.white));
            rombo.matInterior = DiamondGizmo.NuevoMaterial(color);
            var interior = DiamondGizmo.CrearCara("Interior", rombo.marcador.transform, 0.28f, rombo.matInterior);
            interior.transform.localPosition = new Vector3(0f, 0f, -0.02f);

            var layer = LayerMask.NameToLayer("Minimap");
            if (layer < 0) layer = 8;
            var icon = MinimapIcon.Spawn(padre, color, layer, 1.2f);
            icon.ConvertirEnRombo();
            var root = SP.Core.RaicesDeEscena.Buscar("InteractuablesIconosRoot");
            if (root == null) root = new GameObject("InteractuablesIconosRoot");
            icon.transform.SetParent(root.transform, true);

            return rombo;
        }

        void LateUpdate()
        {
            bool visible = Condicion == null || Condicion();
            if (marcador.activeSelf != visible) marcador.SetActive(visible);
            if (!visible) return;

            var cam = SP.Core.CamaraPrincipal.Actual;
            if (cam != null && !enSuelo) marcador.transform.rotation = cam.transform.rotation;
        }

        void OnDestroy()
        {
            if (matInterior != null)
            {
                if (Application.isPlaying) Destroy(matInterior);
                else DestroyImmediate(matInterior);
            }
        }
    }
}
