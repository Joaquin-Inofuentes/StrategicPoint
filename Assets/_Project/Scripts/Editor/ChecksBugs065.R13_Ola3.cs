using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace SP.EditorTools
{
    // Ronda 13, T19: la fuente del gameplay sale de FuentesBelicas; la integrada (LegacyRuntime) queda solo como respaldo ahi.
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- 987a ninguna UI de gameplay usa LegacyRuntime salvo el respaldo de FuentesBelicas.cs
        static IEnumerator Bug987a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var raiz = Path.Combine(Application.dataPath, "_Project", "Scripts");
            var infractores = new StringBuilder(); int respaldos = 0;
            foreach (var f in Directory.GetFiles(raiz, "*.cs", SearchOption.AllDirectories))
            {
                var rel = f.Replace(Path.DirectorySeparatorChar, '/');
                if (rel.Contains("/Editor/")) continue;
                if (!File.ReadAllText(f).Contains("LegacyRuntime")) continue;
                if (Path.GetFileName(f) == "FuentesBelicas.cs") respaldos++;
                else infractores.Append(Path.GetFileName(f)).Append(' ');
            }
            W8Ok(ref ok, infractores.Length == 0 && respaldos == 1, sb, $"el codigo de juego nombra LegacyRuntime solo en FuentesBelicas.cs (respaldos {respaldos}, otros: {infractores})");
            OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(3f)) yield return x;
            int textos = 0, integradas = 0; var quien = new StringBuilder();
            foreach (var t in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || !t.gameObject.scene.IsValid()) continue;
                textos++;
                var fn = t.font != null ? t.font.name : "";
                if (fn == "LegacyRuntime" || fn == "Arial" || t.font == null) { integradas++; if (quien.Length < 400) quien.Append(RutaDeObjeto601(t.transform)).Append(" [").Append(fn).Append("]; "); }
            }
            W8Ok(ref ok, textos > 0 && integradas == 0, sb, $"{integradas} de {textos} Text de las escenas usan la fuente integrada ({quien})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
