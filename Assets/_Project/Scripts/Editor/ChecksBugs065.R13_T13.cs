using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ronda 13, T13: el "cuadro gris" del objetivo 4. Dos hipotesis: la losa Plaza_Cemento (cubo liso de 30 x 30 bajo el tanque) y el fondo gris claro de la barra
    // de vida del tanque. Se miden las dos.
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- 982a sin cubos grises grandes en el patio ni Image grises grandes en el HUD del tanque
        static IEnumerator Bug982a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4);
            foreach (var x in Esperar(2f)) yield return x;
            foreach (var x in PrepararTanque()) yield return x;
            foreach (var x in Esperar(3f)) yield return x;
            Physics.SyncTransforms();

            // 1) renderers activos a menos de 40 m de (0,0,22) con malla Cube built-in, material CementoOsc o por defecto y mas de 4 m2 de planta
            var c = new Vector3(0f, 0f, 22f); int cubos = 0; var lista = new StringBuilder();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds;
                if (b.size.x * b.size.z <= 4f) continue;
                if (Vector2.Distance(new Vector2(b.center.x, b.center.z), new Vector2(c.x, c.z)) >= 40f) continue;
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || mf.sharedMesh.name != "Cube") continue;
                string mat = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                bool gris = mat.Contains("CementoOsc") || mat.Contains("Default") || r.sharedMaterial == null;
                if (!gris) continue;
                cubos++; if (lista.Length < 300) lista.Append(r.name + "[" + mat + "] ");
            }
            W8Ok(ref ok, cubos == 0, sb, $"{cubos} cubos grises grandes (Cube + CementoOsc/por defecto, planta > 4 m2) a menos de 40 m del patio ({lista})");

            // 2) Image activas, sin sprite, grises (r ~ g ~ b entre 0,3 y 0,9), de mas de 60 x 20 px, en el HUD durante la carrera
            int imgs = 0; var li = new StringBuilder();
            foreach (var im in Object.FindObjectsByType<Image>(FindObjectsInactive.Exclude))
            {
                if (im == null || !im.enabled || !im.gameObject.activeInHierarchy || im.sprite != null) continue;
                if (im.name == "MinimapBorder") continue;   // marco del minimapa: gris claro por diseno y cubierto por la RawImage del mapa (se ve como borde de 8 px, ver antes/tanque_hud_barra.png)
                var col = im.color; if (col.a < 0.3f) continue;
                bool gris = Mathf.Abs(col.r - col.g) < 0.06f && Mathf.Abs(col.g - col.b) < 0.06f && col.r > 0.3f && col.r < 0.9f;
                if (!gris) continue;
                var sz = im.rectTransform.rect.size; var cv = im.canvas; float k = cv != null ? cv.scaleFactor : 1f;
                if (sz.x * k <= 60f || sz.y * k <= 20f) continue;
                imgs++; if (li.Length < 300) li.Append(im.name + $"[{col.r:0.00} {sz.x * k:0}x{sz.y * k:0}] ");
            }
            W8Ok(ref ok, imgs == 0, sb, $"{imgs} Image grises grandes sin sprite en el HUD ({li})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
