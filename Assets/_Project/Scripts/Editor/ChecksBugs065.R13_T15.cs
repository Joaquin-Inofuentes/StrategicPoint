using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ronda 13, T15: la plaza con solado real. 0 'Adoquin' ni 'Patio_Losa' activos, a lo sumo 12 renderers de piso en la caja de la plaza y materiales del solado con
    // textura y normal map.
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- 984a solado de la plaza
        static IEnumerator Bug984a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(3f)) yield return x;

            int adoq = 0, losas = 0, solado = 0, enCaja = 0;
            var mats = new HashSet<Material>();
            var caja = new Bounds(new Vector3(322f, 0.2f, 228f), new Vector3(40f, 2f, 40f));   // la plaza (32 x 32) mas las sendas de las 4 bocas
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r.name == "Adoquin") adoq++;
                if (r.name == "Patio_Losa") losas++;
                if (r.name.StartsWith("R13_Solado")) { solado++; foreach (var m in r.sharedMaterials) if (m != null) mats.Add(m); }
                var b = r.bounds;
                // piezas de piso de la plaza: planas, de hasta 40 m de lado y con el centro dentro de la caja. No cuentan los pisos de otros edificios (Mansion), los fusionados de calles
                // (Asfalto_*/Amarillo_*: pintura de las calles que desembocan) ni el horizonte/arena del mundo, que existian igual con el damero (ANTES ~100 + 29 de esos).
                if (caja.Contains(new Vector3(b.center.x, 0.2f, b.center.z)) && b.size.y < 0.4f && b.size.x > 1.5f && b.size.z > 1.5f && b.max.y < 0.5f && b.size.x <= 40f && b.size.z <= 40f
                    && !r.name.StartsWith("Asfalto_") && !r.name.StartsWith("Amarillo_") && !r.name.StartsWith("Fondo_") && !r.name.StartsWith("Losa_Arena") && r.name != "Vereda")
                {
                    bool deMansion = false; for (var t = r.transform; t != null && !deMansion; t = t.parent) deMansion = t.name.Contains("Mansion");
                    if (!deMansion) enCaja++;
                }
            }
            W8Ok(ref ok, adoq == 0, sb, $"{adoq} renderers Adoquin activos (pide 0)");
            W8Ok(ref ok, losas == 0, sb, $"{losas} renderers Patio_Losa activos (pide 0)");
            W8Ok(ref ok, solado >= 4, sb, $"{solado} renderers R13_Solado* en la escena (pide al menos los 4 de la plaza)");
            W8Ok(ref ok, enCaja <= 12, sb, $"{enCaja} renderers de piso en la caja de la plaza (pide <= 12; antes unos 100)");
            int sinTextura = 0; var det = new StringBuilder();
            foreach (var m in mats)
            {
                bool base0 = m.mainTexture != null || (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null);
                bool normal = m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") != null;
                if (!base0 || !normal) { sinTextura++; det.Append(m.name + " "); }
            }
            W8Ok(ref ok, mats.Count >= 3 && sinTextura == 0, sb, $"{mats.Count} materiales del solado, {sinTextura} sin textura base o sin normal map ({det})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
