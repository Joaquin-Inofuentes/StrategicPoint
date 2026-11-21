using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ronda 17b: metrica de contraste enemigo/fondo (el punto 1 de la R17 quedo PARCIAL porque no habia numero).
    // Se renderiza al enemigo desde la distancia de combate con la camara del juego, dos veces: con y sin sus renderers. La diferencia entre las dos tomas
    // da la silueta; el anillo de la toma sin enemigo da el fondo. Se mide contraste de luminancia (WCAG) y diferencia de color (CIE76 en Lab).
    public static partial class ChecksBugs065
    {
        public const float ContrasteRatioMinimo = 1.35f, ContrasteDeltaEMinimo = 18f;
        // Guarda cada toma medida en Assets/Validacion~/contraste (carpeta ignorada). Se prende con EditorPrefs "r17_volcar_contraste" (sobrevive a la recarga de Play).
        public static bool VolcarTomasDeContraste => UnityEditor.EditorPrefs.GetBool("r17_volcar_contraste", false);
        static int r17Foto;

        static float R17Lin(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        static float R17Lum(Vector3 rgb) => 0.2126f * R17Lin(rgb.x) + 0.7152f * R17Lin(rgb.y) + 0.0722f * R17Lin(rgb.z);

        static Vector3 R17Lab(Vector3 rgb)
        {
            float r = R17Lin(rgb.x), g = R17Lin(rgb.y), b = R17Lin(rgb.z);
            float x = (0.4124f * r + 0.3576f * g + 0.1805f * b) / 0.95047f, y = 0.2126f * r + 0.7152f * g + 0.0722f * b, z = (0.0193f * r + 0.1192f * g + 0.9505f * b) / 1.08883f;
            System.Func<float, float> f = t => t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;
            return new Vector3(116f * f(y) - 16f, 500f * (f(x) - f(y)), 200f * (f(y) - f(z)));
        }

        struct MedidaContraste { public string quien; public float ratio, deltaE, pixeles; public bool visible; }

        // Devuelve false si el enemigo no se ve desde ahi (tapado): se descarta, no cuenta como fallo.
        static bool R17MedirContraste(Soldier e, Vector3 desde, out MedidaContraste m)
        {
            m = new MedidaContraste { quien = e.name };
            var go = new GameObject("_R17_CamContraste");
            var cam = go.AddComponent<Camera>();
            var main = Camera.main; if (main != null) cam.CopyFrom(main);
            cam.fieldOfView = 28f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;
            var foco = e.transform.position + Vector3.up * 0.95f;
            go.transform.position = desde; go.transform.LookAt(foco);
            var rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            var tex = new Texture2D(512, 512, TextureFormat.RGB24, false);
            var rends = e.GetComponentsInChildren<Renderer>(false);
            var estado = new bool[rends.Length]; for (int i = 0; i < rends.Length; i++) estado[i] = rends[i].enabled;
            Color32[] con, sin; byte[] png = null;
            var previo = RenderTexture.active;
            try
            {
                cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); con = tex.GetPixels32();
                if (VolcarTomasDeContraste) png = tex.EncodeToPNG();
                for (int i = 0; i < rends.Length; i++) if (rends[i] != null) rends[i].enabled = false;
                cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); sin = tex.GetPixels32();
            }
            finally
            {
                for (int i = 0; i < rends.Length; i++) if (rends[i] != null) rends[i].enabled = estado[i];
                RenderTexture.active = previo; cam.targetTexture = null;
                Object.Destroy(go); rt.Release(); Object.Destroy(rt); Object.Destroy(tex);
            }
            // silueta = pixeles que cambian al ocultar al enemigo
            int minX = 512, minY = 512, maxX = -1, maxY = -1, n = 0; var mask = new bool[512 * 512]; Vector3 suma = Vector3.zero;
            for (int y = 0; y < 512; y++)
                for (int x = 0; x < 512; x++)
                {
                    int i = y * 512 + x; var a = con[i]; var b = sin[i];
                    if (Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b))) <= 10) continue;
                    mask[i] = true; n++; suma += new Vector3(a.r, a.g, a.b) / 255f;
                    if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
            m.pixeles = n;
            if (n < 300) return false;
            var cuerpo = suma / n;
            // fondo = anillo alrededor de la silueta en la toma SIN enemigo
            int mx = Mathf.Max(8, (maxX - minX) / 3), my = Mathf.Max(8, (maxY - minY) / 3); int nf = 0; Vector3 sf = Vector3.zero;
            for (int y = Mathf.Max(0, minY - my); y <= Mathf.Min(511, maxY + my); y++)
                for (int x = Mathf.Max(0, minX - mx); x <= Mathf.Min(511, maxX + mx); x++)
                {
                    bool dentro = x >= minX - 3 && x <= maxX + 3 && y >= minY - 3 && y <= maxY + 3;
                    if (dentro) continue;
                    var b = sin[y * 512 + x]; sf += new Vector3(b.r, b.g, b.b) / 255f; nf++;
                }
            if (nf < 200) return false;
            var fondo = sf / nf;
            float l1 = R17Lum(cuerpo), l2 = R17Lum(fondo);
            m.ratio = (Mathf.Max(l1, l2) + 0.05f) / (Mathf.Min(l1, l2) + 0.05f);
            m.deltaE = Vector3.Distance(R17Lab(cuerpo), R17Lab(fondo));
            m.visible = true;
            if (png != null)
            {
                try
                {
                    var dir = System.IO.Path.Combine(Application.dataPath, "Validacion~/contraste"); System.IO.Directory.CreateDirectory(dir);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"{++r17Foto:00}_{e.name}_dE{m.deltaE:0}.png"), png);
                }
                catch (System.Exception ex) { Debug.LogWarning("[R17b] no se pudo volcar la toma: " + ex.Message); }
            }
            return true;
        }

        // ---------------------------------------------------------------- 990m contraste medido de los enemigos contra el fondo (los 6 objetivos)
        static IEnumerator Bug990m()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var todas = new List<MedidaContraste>(); var porTipo = new Dictionary<string, List<MedidaContraste>>();
            int sinVista = 0;
            for (int objetivo = 1; objetivo <= 6; objetivo++)
            {
                OperacionPrueba.Arrancar(objetivo, 1);
                ModoDios.Poner(true);
                foreach (var x in Esperar(2f)) yield return x;
                var yo = W9Yo(); if (yo == null) continue;
                var porTipoAqui = new Dictionary<string, int>();
                var lista = new List<Soldier>();
                foreach (var s in ActorRegistry.All)
                    if (s != null && s.Team == TeamId.Enemy && s.Health != null && s.Health.IsAlive) lista.Add(s);   // tambien los inactivos (aun no desplegados)
                lista.Sort((p, q) => (p.transform.position - yo.transform.position).sqrMagnitude.CompareTo((q.transform.position - yo.transform.position).sqrMagnitude));
                // Candidatos: hasta 4 por tipo (los 3 que se miden y uno de repuesto). Los que estan desplegados en reserva (inactivos) se activan y se deja pasar
                // unos cuadros: SoldierLook aplica el aspecto en Start, y medirlos el mismo cuadro daba el trimsheet crudo (falso contraste bajo).
                var candidatos = new List<Soldier>(); var cuenta = new Dictionary<string, int>(); var reactivados = new List<GameObject>();
                string TipoDe(Soldier q) => q.GetComponent<EnemigoPesado>() != null ? "pesado" : q.GetComponent<EnemigoEscudero>() != null ? "escudero" : q.GetComponent<EnemigoFrancotirador>() != null ? "francotirador" : "fusilero";
                foreach (var q in lista)
                {
                    string tq = TipoDe(q); cuenta.TryGetValue(tq, out int cq); if (cq >= 4) continue;
                    cuenta[tq] = cq + 1; candidatos.Add(q);
                    if (!q.gameObject.activeSelf) { q.gameObject.SetActive(true); reactivados.Add(q.gameObject); }
                }
                for (int fr = 0; fr < 4; fr++) yield return null;
                foreach (var e in candidatos)
                {
                    string tipo = TipoDe(e);
                    porTipoAqui.TryGetValue(tipo, out int hechos);
                    if (hechos >= 3) continue;
                    bool estabaActivo = true;
                    // varios angulos a 10 m: el primero con la silueta visible (sin pared delante) vale
                    bool midio = false; MedidaContraste m = default;
                    for (int a = 0; a < 8 && !midio; a++)
                    {
                        float ang = a * 45f * Mathf.Deg2Rad;
                        var desde = e.transform.position + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * 10f + Vector3.up * 1.7f;
                        if (Physics.Linecast(desde, e.transform.position + Vector3.up * 1.0f, out var golpe, ~0, QueryTriggerInteraction.Ignore) && golpe.collider != null && golpe.collider.GetComponentInParent<Soldier>() != e) continue;
                        midio = R17MedirContraste(e, desde, out m);
                    }
                    if (!estabaActivo) e.gameObject.SetActive(false);
                    if (midio)
                    {
                        m.quien = $"{tipo}@obj{objetivo}";
                        todas.Add(m); porTipoAqui[tipo] = hechos + 1;
                        if (!porTipo.TryGetValue(tipo, out var l)) porTipo[tipo] = l = new List<MedidaContraste>();
                        l.Add(m);
                    }
                    else sinVista++;
                    yield return null;
                }
                foreach (var go in reactivados) if (go != null) go.SetActive(false);
                ModoDios.Poner(false);
            }
            if (todas.Count < 6) { Fin("FALLO muy pocas tomas con silueta visible: " + todas.Count); yield break; }
            var ratios = new List<float>(); var des = new List<float>(); int bajos = 0;
            foreach (var m in todas)
            {
                ratios.Add(m.ratio); des.Add(m.deltaE);
                if (m.ratio < ContrasteRatioMinimo && m.deltaE < ContrasteDeltaEMinimo) bajos++;
            }
            ratios.Sort(); des.Sort();
            sb.Append($"{todas.Count} tomas ({sinVista} sin silueta visible): ratio de luminancia mediana {ratios[ratios.Count / 2]:0.00} (min {ratios[0]:0.00}), dE mediana {des[des.Count / 2]:0.0} (min {des[0]:0.0}); bajos (ratio<{ContrasteRatioMinimo} y dE<{ContrasteDeltaEMinimo}): {bajos}; ");
            foreach (var kv in porTipo)
            {
                var r = new List<float>(); var d2 = new List<float>(); foreach (var m in kv.Value) { r.Add(m.ratio); d2.Add(m.deltaE); }
                r.Sort(); d2.Sort();
                sb.Append($"[{kv.Key} n={kv.Value.Count} ratio med {r[r.Count / 2]:0.00} dE med {d2[d2.Count / 2]:0.0} dE min {d2[0]:0.0}] ");
            }
            foreach (var m in todas) if (m.ratio < ContrasteRatioMinimo && m.deltaE < ContrasteDeltaEMinimo) sb.Append($"BAJO {m.quien} ratio {m.ratio:0.00} dE {m.deltaE:0.0}; ");
            W8Ok(ref ok, bajos * 5 <= todas.Count, sb, $"a lo sumo 20 % de las tomas con contraste bajo ({bajos}/{todas.Count})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
