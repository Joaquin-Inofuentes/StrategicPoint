using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Presentation;

namespace SP.EditorTools
{
    // Tanda #104-#132, P2: arte. #109 pesos de piel de los soldados (astillas mano->cadera con la animacion de accion).
    public static partial class ChecksBugs065
    {
        // Mayor razon arista horneada / arista en bind de la malla de un SkinnedMeshRenderer (mismo calculo del diagnostico p109c).
        static float PeorRazonDeArista(SkinnedMeshRenderer smr, out int caras, out int a, out int b)
        {
            var m = smr.sharedMesh; var baked = new Mesh(); smr.BakeMesh(baked, true);
            var vb = m.vertices; var vk = baked.vertices; var tr = m.triangles;
            float peor = 0f; a = -1; b = -1; caras = 0;
            for (int t = 0; t < tr.Length; t += 3)
            {
                bool mala = false;
                for (int e = 0; e < 3; e++)
                {
                    int i = tr[t + e], j = tr[t + (e + 1) % 3];
                    float lb = (vb[i] - vb[j]).magnitude;
                    if (lb < 0.004f) continue;
                    float r = (vk[i] - vk[j]).magnitude / lb;
                    if (r > peor) { peor = r; a = i; b = j; }
                    if (r > 2.2f) mala = true;
                }
                if (mala) caras++;
            }
            Object.Destroy(baked);
            return peor;
        }

        // ---------------------------------------------------------------- #109 pesos de piel sin astillas mano->cadera
        static IEnumerator Bug109()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            // (1) estatico: las 3 mallas no tienen caras con pesos mano+cadera mezclados
            var nombres = PielSoldadoAnalisis.NombresDeHueso();
            foreach (var nm in new[] { "Mesh_Asalto", "Mesh_Flanqueador", "Mesh_Medico" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Resources/Soldados/" + nm + ".asset");
                if (m == null || nombres == null) { ok = false; sb.Append(nm + " no existe; "); continue; }
                int mezcladas = PielSoldadoAnalisis.CarasMezcladas(m, nombres, out _);
                if (mezcladas != 0) ok = false;
                sb.Append($"{nm} mezcladas={mezcladas} bindposes={m.bindposes.Length} verts={m.vertexCount}; ");
            }
            // (2) en Play: soldados del bando jugador en pleno Hackear / Operar / Recargar, razon de arista maxima < 2.2
            ArrancarEn(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var soldados = Soldados(TeamId.Player);
            if (soldados.Count < 3) { Fin("FALLO soldados del bando jugador: " + soldados.Count); yield break; }
            var tipos = new[] { TipoAccion.Hackear, TipoAccion.Operar, TipoAccion.Recargar };
            float peorGlobal = 0f; int carasMalas = 0;
            foreach (var tipo in tipos)
            {
                foreach (var s in soldados)
                {
                    var obj = s.transform.position + s.transform.forward * 0.8f + Vector3.up * 0.2f;
                    AnimacionDeAccion.Iniciar(s, tipo, obj, 20f);
                }
                foreach (var x in Esperar(0.25f)) yield return x;
                for (int muestra = 0; muestra < 4; muestra++)
                {
                    foreach (var x in Esperar(0.25f)) yield return x;
                    foreach (var s in soldados)
                        foreach (var smr in s.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                        {
                            if (smr.sharedMesh == null) continue;
                            float r = PeorRazonDeArista(smr, out int caras, out _, out _);
                            if (r > peorGlobal) peorGlobal = r;
                            if (caras > carasMalas) carasMalas = caras;
                        }
                }
                foreach (var s in soldados) AnimacionDeAccion.Terminar(s);
            }
            sb.Append($"peorRazon={peorGlobal:0.00} carasEstiradas={carasMalas}");
            if (peorGlobal >= 2.2f) ok = false;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // Utilidad de validacion visual (se llama desde la terminal, en Play): inicia la accion 'tipo' (0 ninguna/idle, 1 Hackear, 2 Operar, 3 Recargar)
        // en los soldados del bando jugador y, tras 'espera' en otra llamada, Render109 fotografia a Doc y a Kes de cerca (v3_109_<prefijo>_<nombre>.png).
        public static string Iniciar109(int tipo)
        {
            var tipos = new[] { TipoAccion.Hackear, TipoAccion.Operar, TipoAccion.Recargar };
            foreach (var s in Soldados(TeamId.Player))
            {
                if (tipo == 0) { AnimacionDeAccion.Terminar(s); continue; }
                AnimacionDeAccion.Iniciar(s, tipos[tipo - 1], s.transform.position + s.transform.forward * 0.8f + Vector3.up * 0.2f, 30f);
            }
            return "ok";
        }

        public static string Render109(string prefijo)
        {
            var sb = new StringBuilder();
            foreach (var s in Soldados(TeamId.Player))
            {
                var c = s.transform.position + Vector3.up * 0.55f;
                // de frente-costado, como la captura del bug, y de cerca de la cadera
                var pos = c + s.transform.right * 2.0f + s.transform.forward * 0.7f + Vector3.up * 0.4f;
                var luz = new GameObject("LuzTemp109").AddComponent<Light>(); luz.type = LightType.Point; luz.range = 8f; luz.intensity = 4f;
                luz.transform.position = pos + Vector3.up * 0.5f;
                sb.AppendLine(RenderTemporal($"v3_109_{prefijo}_{s.name}", pos, c, 45f));
                Object.Destroy(luz.gameObject);
            }
            return sb.ToString();
        }
    }
}
