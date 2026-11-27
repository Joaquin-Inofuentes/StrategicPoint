using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Presentation;

namespace SP.EditorTools
{
    // Ronda 19, carriles C y D (PLAN_R19_v2 3.5 y 3.6).
    //   #994i  contraste del escudero con la paleta vigente (default A "antidisturbios") + escudo nuevo: dE, ratio, separacion de color, materiales.
    //   #994q  comparacion de las paletas A/B/C del escudero con la misma metrica (informe: elige la de mayor dE minima; empate -> A). No cambia el codigo:
    //          el ganador se fija a mano en SoldierLook.PaletaPorDefecto.
    //   #994j  desintegracion en cubos de los caidos (RetiroDeEnemigosCaidos): tiempos, cubos, tope del pool, revivir a mitad de camino, FaseEn.
    // Play sobre SC_Operacion; conviene un Play fresco por check (CorrerLista con aislar = true).
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- utilidades
        // Igual que R17MedirContraste, pero devuelve ademas el color medio de la silueta en Lab (para la separacion entre tipos).
        static bool R19dMedir(Soldier e, Vector3 desde, out MedidaContraste m, out Vector3 labCuerpo)
        {
            m = new MedidaContraste { quien = e.name }; labCuerpo = Vector3.zero;
            var go = new GameObject("_R19d_CamContraste");
            var cam = go.AddComponent<Camera>();
            var main = Camera.main; if (main != null) cam.CopyFrom(main);
            cam.fieldOfView = 28f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;
            go.transform.position = desde; go.transform.LookAt(e.transform.position + Vector3.up * 0.95f);
            const int N = 384;
            var rt = new RenderTexture(N, N, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            var tex = new Texture2D(N, N, TextureFormat.RGB24, false);
            var rends = e.GetComponentsInChildren<Renderer>(false);
            var estado = new bool[rends.Length]; for (int i = 0; i < rends.Length; i++) estado[i] = rends[i].enabled;
            Color32[] con, sin;
            var previo = RenderTexture.active;
            try
            {
                cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, N, N), 0, 0); con = tex.GetPixels32();
                for (int i = 0; i < rends.Length; i++) if (rends[i] != null) rends[i].enabled = false;
                cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, N, N), 0, 0); sin = tex.GetPixels32();
            }
            finally
            {
                for (int i = 0; i < rends.Length; i++) if (rends[i] != null) rends[i].enabled = estado[i];
                RenderTexture.active = previo; cam.targetTexture = null;
                Object.Destroy(go); rt.Release(); Object.Destroy(rt); Object.Destroy(tex);
            }
            int minX = N, minY = N, maxX = -1, maxY = -1, n = 0; Vector3 suma = Vector3.zero;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int i = y * N + x; var a = con[i]; var b = sin[i];
                    if (Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b))) <= 10) continue;
                    n++; suma += new Vector3(a.r, a.g, a.b) / 255f;
                    if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
            m.pixeles = n;
            if (n < 170) return false;
            var cuerpo = suma / n;
            int mx = Mathf.Max(6, (maxX - minX) / 3), my = Mathf.Max(6, (maxY - minY) / 3), nf = 0; Vector3 sf = Vector3.zero;
            for (int y = Mathf.Max(0, minY - my); y <= Mathf.Min(N - 1, maxY + my); y++)
                for (int x = Mathf.Max(0, minX - mx); x <= Mathf.Min(N - 1, maxX + mx); x++)
                {
                    if (x >= minX - 3 && x <= maxX + 3 && y >= minY - 3 && y <= maxY + 3) continue;
                    var b = sin[y * N + x]; sf += new Vector3(b.r, b.g, b.b) / 255f; nf++;
                }
            if (nf < 120) return false;
            var fondo = sf / nf;
            float l1 = R17Lum(cuerpo), l2 = R17Lum(fondo);
            m.ratio = (Mathf.Max(l1, l2) + 0.05f) / (Mathf.Min(l1, l2) + 0.05f);
            labCuerpo = R17Lab(cuerpo);
            m.deltaE = Vector3.Distance(labCuerpo, R17Lab(fondo));
            m.visible = true;
            return true;
        }

        // Mide a 'e' a 10 m mirandolo de frente (o de espaldas): se gira al soldado hacia la camara y se prueban 8 posiciones hasta tener la silueta limpia.
        static bool R19dTomar(Soldier e, bool deEspaldas, out MedidaContraste m, out Vector3 lab)
        {
            m = default; lab = Vector3.zero;
            foreach (float dist in new[] { 10f, 7f, 13f })
            for (int a = 0; a < 16; a++)
            {
                float ang = a * 22.5f * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                var desde = e.transform.position + dir * dist + Vector3.up * 1.7f;
                if (Physics.Linecast(desde, e.transform.position + Vector3.up * 1.0f, out var golpe, ~0, QueryTriggerInteraction.Ignore) && golpe.collider != null && golpe.collider.GetComponentInParent<Soldier>() != e) continue;
                e.transform.rotation = Quaternion.LookRotation(deEspaldas ? -dir : dir);
                Physics.SyncTransforms();
                if (R19dMedir(e, desde, out m, out lab)) return true;
            }
            return false;
        }

        static void R19dQuieto(Soldier e)
        {
            var ag = e.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag != null) ag.enabled = false;
            if (e.Brain != null) e.Brain.enabled = false;
        }

        static bool R19dEsEspecial(Soldier s) => s.GetComponent<EnemigoPesado>() != null || s.GetComponent<EnemigoEscudero>() != null || s.GetComponent<EnemigoFrancotirador>() != null;

        static List<Soldier> R19dFusileros(Soldier yo)
        {
            var l = new List<Soldier>();
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy || R19dEsEspecial(s)) continue;
                if (s.GetComponent<PerroEnemigo>() != null) continue;
                l.Add(s);
            }
            if (yo != null) l.Sort((p, q) => (p.transform.position - yo.transform.position).sqrMagnitude.CompareTo((q.transform.position - yo.transform.position).sqrMagnitude));
            return l;
        }

        // Pone la paleta pedida a los escuderos vivos (sin volver a correr SoldierLook.Aplicar, que reequipa el arma).
        static void R19dVestirEscuderos(SoldierLook.Paleta p)
        {
            SoldierLook.PaletaDeEscudero = p;
            var mat = SoldierLook.MaterialDelEscuderoActual;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.GetComponent<EnemigoEscudero>() == null) continue;
                var smr = s.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null && mat != null) smr.sharedMaterial = mat;
            }
            EnemigoEscudero.RepintarSegunPaleta();
        }

        struct R19dResultado { public List<float> de, ratio; public int bajas, tomas; public Vector3 labEsc, labCom, labPes, labAli; public int nEsc, nCom, nPes, nAli; }

        // Convierte fusileros en escudero/pesado en los objetivos 1 a 3 y mide frente y espalda de 2 escuderos por objetivo con CADA paleta pedida (mismo
        // lugar y misma luz para todas); ademas el color medio de un comun, el pesado y un aliado (no dependen de la paleta).
        static IEnumerable R19dMedirEscuderos(SoldierLook.Paleta[] paletas, R19dResultado[] salida, StringBuilder diag)
        {
            for (int i = 0; i < paletas.Length; i++) salida[i] = new R19dResultado { de = new List<float>(), ratio = new List<float>() };
            Vector3 labCom = Vector3.zero, labPes = Vector3.zero, labAli = Vector3.zero; int nCom = 0, nPes = 0, nAli = 0;
            for (int objetivo = 1; objetivo <= 3; objetivo++)
            {
                OperacionPrueba.Arrancar(objetivo, 1);
                ModoDios.Poner(true);
                foreach (var x in Esperar(2f)) yield return x;
                var yo = W9Yo(); if (yo == null) { diag.Append($"obj{objetivo}: sin jugador; "); continue; }
                var fus = R19dFusileros(yo);
                if (fus.Count < 4)
                {
                    // los de reserva (inactivos) tambien sirven: se activan
                    foreach (var s in ActorRegistry.All)
                    {
                        if (fus.Count >= 4) break;
                        if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || s.gameObject.activeSelf || R19dEsEspecial(s) || s.GetComponent<PerroEnemigo>() != null) continue;
                        s.gameObject.SetActive(true); if (s.gameObject.activeInHierarchy) fus.Add(s);
                    }
                }
                if (fus.Count < 3) { diag.Append($"obj{objetivo}: {fus.Count} comunes; "); continue; }
                Soldier esc = fus[0], pes = fus[1], com = fus[2], esc2 = fus.Count > 3 ? fus[3] : null;
                R19dQuieto(esc); R19dQuieto(pes); R19dQuieto(com);
                EnemigoEscudero.Aplicar(esc); EnemigoPesado.Aplicar(pes);
                if (esc2 != null) { R19dQuieto(esc2); EnemigoEscudero.Aplicar(esc2); }
                for (int fr = 0; fr < 8; fr++) yield return null;   // SoldierLook aplica en Start; el escudo se arma (y se reviste) 2 cuadros despues
                int antes = salida[0].tomas;
                for (int ip = 0; ip < paletas.Length; ip++)
                {
                    var r = salida[ip];
                    R19dVestirEscuderos(paletas[ip]);
                    yield return null; yield return null;
                    foreach (var e in new[] { esc, esc2 })
                    {
                        if (e == null) continue;
                        foreach (bool espalda in new[] { false, true })
                        {
                            if (!R19dTomar(e, espalda, out var m, out var lab)) continue;
                            r.tomas++; r.de.Add(m.deltaE); r.ratio.Add(m.ratio);
                            if (m.ratio < ContrasteRatioMinimo && m.deltaE < ContrasteDeltaEMinimo) r.bajas++;
                            if (!espalda) { r.labEsc += lab; r.nEsc++; }
                        }
                    }
                    salida[ip] = r;
                    yield return null;
                }
                diag.Append($"obj{objetivo}: {salida[0].tomas - antes} tomas; ");
                if (R19dTomar(com, false, out _, out var lc)) { labCom += lc; nCom++; }
                if (R19dTomar(pes, false, out _, out var lp)) { labPes += lp; nPes++; }
                Soldier ali = null;
                foreach (var s in ActorRegistry.All)
                    if (s != null && s != yo && s.Team == TeamId.Player && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy && s.Role != RoleType.Civilian) { ali = s; break; }
                if (ali != null && R19dTomar(ali, false, out _, out var la)) { labAli += la; nAli++; }
                ModoDios.Poner(false);
                yield return null;
            }
            for (int i = 0; i < paletas.Length; i++)
            {
                var r = salida[i];
                r.de.Sort(); r.ratio.Sort();
                r.labCom = labCom; r.nCom = nCom; r.labPes = labPes; r.nPes = nPes; r.labAli = labAli; r.nAli = nAli;
                salida[i] = r;
            }
            SoldierLook.PaletaDeEscudero = SoldierLook.PaletaPorDefecto; EnemigoEscudero.RepintarSegunPaleta();
        }

        static float R19dMediana(List<float> l) => l.Count == 0 ? 0f : l[l.Count / 2];
        static float R19dSep(Vector3 a, int na, Vector3 b, int nb) => na == 0 || nb == 0 ? -1f : Vector3.Distance(a / na, b / nb);

        // ---------------------------------------------------------------- 994i: contraste del escudero (paleta vigente + escudo nuevo)
        static IEnumerator Bug994i()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var paleta = SoldierLook.PaletaPorDefecto;
            var res = new R19dResultado[1]; var diag = new StringBuilder();
            foreach (var x in R19dMedirEscuderos(new[] { paleta }, res, diag)) yield return x;
            var r = res[0];
            sb.Append($"paleta {paleta} ({diag}); ");
            W8Ok(ref ok, r.tomas >= 6, sb, $"{r.tomas} tomas (>= 6: frente y espalda de 2 escuderos en 3 objetivos)");
            if (r.tomas > 0)
            {
                W8Ok(ref ok, R19dMediana(r.de) >= 40f, sb, $"dE mediana {R19dMediana(r.de):0.0} (>= 40; antes 32,2)");
                W8Ok(ref ok, r.de[0] >= 25f, sb, $"dE minima {r.de[0]:0.0} (>= 25)");
                W8Ok(ref ok, R19dMediana(r.ratio) >= 1.5f, sb, $"ratio de luminancia mediana {R19dMediana(r.ratio):0.00} (>= 1,5)");
                W8Ok(ref ok, r.bajas == 0, sb, $"tomas bajas {r.bajas}");
            }
            float sCom = R19dSep(r.labEsc, r.nEsc, r.labCom, r.nCom), sPes = R19dSep(r.labEsc, r.nEsc, r.labPes, r.nPes), sAli = R19dSep(r.labEsc, r.nEsc, r.labAli, r.nAli);
            W8Ok(ref ok, sCom >= 25f, sb, $"separacion contra el fusilero comun {sCom:0.0} (>= 25)");
            W8Ok(ref ok, sPes >= 25f, sb, $"separacion contra el pesado {sPes:0.0} (>= 25)");
            W8Ok(ref ok, sAli >= 40f, sb, $"separacion contra un aliado {sAli:0.0} (>= 40)");

            // Materiales: 5 escuderos vivos -> materiales distintos del escudo <= 5 compartidos + 5 ventanas
            var yo = W9Yo();
            // Se cuentan solo escuderos activos y con su escudo armado; si faltan, se convierten enemigos comunes vivos (activando los de reserva).
            var escs = new List<EnemigoEscudero>();
            foreach (var s in ActorRegistry.All) { if (escs.Count >= 5) break; var e = s != null && s.gameObject.activeInHierarchy && s.Health != null && s.Health.IsAlive ? s.GetComponent<EnemigoEscudero>() : null; if (e != null && e.Escudo != null) escs.Add(e); }
            foreach (var s in new List<Soldier>(ActorRegistry.All))
            {
                if (escs.Count >= 5) break;
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || R19dEsEspecial(s) || s.GetComponent<PerroEnemigo>() != null) continue;
                if (!s.gameObject.activeSelf) s.gameObject.SetActive(true);
                if (!s.gameObject.activeInHierarchy) continue;
                R19dQuieto(s); escs.Add(EnemigoEscudero.Aplicar(s));
            }
            for (int fr = 0; fr < 8; fr++) yield return null;
            var mats = new HashSet<Material>(); int conEscudo = 0;
            foreach (var e in escs)
            {
                if (e == null || e.Escudo == null) continue;
                conEscudo++;
                foreach (var rr in e.Escudo.GetComponentsInChildren<Renderer>(true)) if (rr.sharedMaterial != null) mats.Add(rr.sharedMaterial);
            }
            W8Ok(ref ok, conEscudo >= 5, sb, $"{conEscudo} escuderos con escudo");
            W8Ok(ref ok, mats.Count <= 5 + conEscudo && EnemigoEscudero.MaterialesCompartidos <= 5, sb, $"materiales de escudo distintos {mats.Count} (<= 5 + {conEscudo} ventanas), compartidos {EnemigoEscudero.MaterialesCompartidos}");
            // El cuerpo lleva el material de la paleta (no el trimsheet crudo) y el escudo los colores nuevos
            var e0 = escs.Count > 0 ? escs[0] : null;
            var smr = e0 != null ? e0.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            W8Ok(ref ok, smr != null && smr.sharedMaterial != null && smr.sharedMaterial.name.StartsWith("MAT_Trimsheet_Escudero"), sb, $"cuerpo con {(smr != null && smr.sharedMaterial != null ? smr.sharedMaterial.name : "null")}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 994q: comparacion de las paletas A/B/C del escudero (informe para elegir)
        static IEnumerator Bug994q()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var paletas = new[] { SoldierLook.Paleta.A_Antidisturbios, SoldierLook.Paleta.B_R17, SoldierLook.Paleta.C_NegroBlancoRojo, SoldierLook.Paleta.D_Claro, SoldierLook.Paleta.E_ClaroPlacaNaranja, SoldierLook.Paleta.F_Naranja };
            var minimos = new float[paletas.Length]; var tomas = new int[paletas.Length]; var medianas = new float[paletas.Length];
            var res = new R19dResultado[paletas.Length]; var diag = new StringBuilder();
            foreach (var x in R19dMedirEscuderos(paletas, res, diag)) yield return x;
            sb.Append($"({diag}) ");
            for (int i = 0; i < paletas.Length; i++)
            {
                var r = res[i];
                tomas[i] = r.tomas; minimos[i] = r.tomas > 0 ? r.de[0] : -1f; medianas[i] = R19dMediana(r.de);
                sb.Append($"{paletas[i]}: {r.tomas} tomas, dE mediana {R19dMediana(r.de):0.0} min {minimos[i]:0.0}, ratio mediana {R19dMediana(r.ratio):0.00}, bajas {r.bajas}, sep comun {R19dSep(r.labEsc, r.nEsc, r.labCom, r.nCom):0.0} pesado {R19dSep(r.labEsc, r.nEsc, r.labPes, r.nPes):0.0} aliado {R19dSep(r.labEsc, r.nEsc, r.labAli, r.nAli):0.0}; ");
            }
            SoldierLook.PaletaDeEscudero = SoldierLook.PaletaPorDefecto; EnemigoEscudero.RepintarSegunPaleta();
            int gana = 0;
            for (int i = 1; i < paletas.Length; i++) if (minimos[i] > minimos[gana] + 0.5f) gana = i;   // empate (0,5 dE) -> A
            for (int i = 0; i < paletas.Length; i++) W8Ok(ref ok, tomas[i] >= 6, sb, $"{paletas[i]} con >= 6 tomas");
            sb.Append($"GANA {paletas[gana]} (mayor dE minima; vigente {SoldierLook.PaletaPorDefecto}); ");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 994j: desintegracion en cubos de los caidos
        static int r19dExcepciones;
        static void R19dLog(string msg, string stack, LogType t) { if (t == LogType.Exception) r19dExcepciones++; }

        static bool R19dVisible(Soldier s)
        {
            foreach (var r in s.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.enabled) return true;
            return false;
        }

        static bool R19dTodoApagado(Soldier s)
        {
            foreach (var r in s.GetComponentsInChildren<Renderer>(true)) if (r.enabled && r.name != "AlertIndicator") return false;   // el marcador lo apaga el estado Dead del AiBrain, que el check desactiva
            return true;
        }

        static Color R19dTinte(Soldier s)
        {
            var smr = s.GetComponentInChildren<SkinnedMeshRenderer>(true);
            return smr != null ? CubeFxReactor.ReadTint(smr) : Color.clear;
        }

        static bool R19dDistinto(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > 0.08f;

        static Soldier R19dPonerDelante(Soldier yo, Soldier e, float metros, float lateral = 0f)
        {
            R19dQuieto(e);
            var fwd = yo.transform.forward; fwd.y = 0f; fwd.Normalize();
            var der = Vector3.Cross(Vector3.up, fwd);
            e.transform.position = yo.transform.position + fwd * metros + der * lateral;
            e.transform.rotation = Quaternion.LookRotation(-fwd);
            return e;
        }

        static IEnumerator Bug994j()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            // Tabla pura (la misma que usara H25.3)
            float w = RetiroDeEnemigosCaidos.SegundosAntesDeRetirar;
            bool tabla = RetiroDeEnemigosCaidos.FaseEn(0f, w) == RetiroDeEnemigosCaidos.Fase.Caido && RetiroDeEnemigosCaidos.FaseEn(w - 0.01f, w) == RetiroDeEnemigosCaidos.Fase.Caido
                && RetiroDeEnemigosCaidos.FaseEn(w + 0.1f, w) == RetiroDeEnemigosCaidos.Fase.Destello && RetiroDeEnemigosCaidos.FaseEn(w + 0.5f, w) == RetiroDeEnemigosCaidos.Fase.Desintegrando
                && RetiroDeEnemigosCaidos.FaseEn(w + 1.01f, w) == RetiroDeEnemigosCaidos.Fase.Retirado;
            W8Ok(ref ok, tabla && Mathf.Approximately(w, 4f) && Mathf.Approximately(RetiroDeEnemigosCaidos.SegundosAntesDeRetirarPesado, 5.5f), sb, "FaseEn: Caido < 4 s, Destello 0,2 s, Desintegrando 0,8 s, Retirado (pesado 5,5 s)");

            OperacionPrueba.Arrancar(2, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var yo = W9Yo();
            if (yo == null) { Fin("FALLO sin jugador"); yield break; }
            var fus = R19dFusileros(yo);
            if (fus.Count < 3) { Fin($"FALLO solo {fus.Count} enemigos comunes vivos"); yield break; }
            r19dExcepciones = 0;
            Application.logMessageReceived += R19dLog;

            // --- 1 baja comun
            var e = R19dPonerDelante(yo, fus[0], 8f);
            Physics.SyncTransforms();
            foreach (var x in Esperar(0.3f)) yield return x;
            int des0 = RetiroDeEnemigosCaidos.Desintegrados, ret0 = RetiroDeEnemigosCaidos.Retirados;
            e.Health.TakeDamage(99999, yo.Id, false);
            float t0 = Time.time;
            while (Time.time - t0 < 2f) yield return null;
            var tinte0 = R19dTinte(e);
            W8Ok(ref ok, R19dVisible(e), sb, "a los 2 s el cuerpo sigue a la vista");
            while (Time.time - t0 < 3.95f) yield return null;
            bool antesQuieto = !R19dDistinto(R19dTinte(e), tinte0) && R19dVisible(e);
            bool vioDestello = false, vioEnCurso = false; int cubos = -1;
            while (Time.time - t0 < 5.2f)
            {
                float t = Time.time - t0;
                if (t <= 4.35f && RetiroDeEnemigosCaidos.EnCurso >= 1) vioEnCurso = true;
                if (t <= 4.35f && R19dDistinto(R19dTinte(e), tinte0)) vioDestello = true;
                if (cubos < 0 && !R19dVisible(e)) cubos = RetiroDeEnemigosCaidos.UltimosCubos;   // el cuadro en que se apaga el cuerpo
                yield return null;
            }
            W8Ok(ref ok, antesQuieto, sb, "hasta los 3,95 s sin destello");
            W8Ok(ref ok, vioEnCurso && vioDestello, sb, $"entre 4,0 y 4,35 s hay desintegracion en curso ({vioEnCurso}) y el tinte cambio ({vioDestello})");
            W8Ok(ref ok, R19dTodoApagado(e), sb, "a los 5,2 s todos sus renderers estan apagados");
            W8Ok(ref ok, RetiroDeEnemigosCaidos.Desintegrados >= des0 + 1 && RetiroDeEnemigosCaidos.Retirados >= ret0 + 1, sb, $"Desintegrados +{RetiroDeEnemigosCaidos.Desintegrados - des0}, Retirados +{RetiroDeEnemigosCaidos.Retirados - ret0}");
            W8Ok(ref ok, cubos >= 6 && cubos <= 12, sb, $"cubos lanzados {cubos} (6..12)");

            // --- revivir a los 3 s (Caido) y en pleno destello
            fus = R19dFusileros(yo);
            if (fus.Count >= 2)
            {
                var r1 = R19dPonerDelante(yo, fus[0], 8f, -2f); var r2 = R19dPonerDelante(yo, fus[1], 8f, 2f);
                Physics.SyncTransforms();
                yield return null;
                var tin1 = R19dTinte(r1); var tin2 = R19dTinte(r2);
                int canc0 = RetiroDeEnemigosCaidos.Cancelados;
                r1.Health.TakeDamage(99999, yo.Id, false); r2.Health.TakeDamage(99999, yo.Id, false);
                // Se verifica 0,2 s despues de revivir (luego los aliados le vuelven a tirar: un enemigo vivo y quieto delante dura poco).
                float tr = Time.time, tv1 = -1f, tv2 = -1f; bool revivio2 = false; string m1 = "sin medir", m2 = "sin medir"; bool ok1 = false, ok2 = false;
                while (Time.time - tr < 5.5f && (tv1 < 0f || Time.time - tv1 < 0.2f || tv2 < 0f || Time.time - tv2 < 0.2f))
                {
                    float t = Time.time - tr;
                    if (tv1 < 0f && t >= 3f && !r1.Health.IsAlive) { r1.Health.Initialize(r1.Id, Mathf.Max(1, r1.Health.MaxHealth)); r1.Health.FactorDeDano = 0.01f; tv1 = Time.time; }
                    if (!revivio2 && !r2.Health.IsAlive && R19dDistinto(R19dTinte(r2), tin2) && R19dVisible(r2)) { r2.Health.Initialize(r2.Id, Mathf.Max(1, r2.Health.MaxHealth)); r2.Health.FactorDeDano = 0.01f; revivio2 = true; tv2 = Time.time; }
                    yield return null;
                    if (tv1 >= 0f && m1 == "sin medir" && Time.time - tv1 >= 0.2f) { ok1 = r1.Health.IsAlive && R19dVisible(r1) && !R19dDistinto(R19dTinte(r1), tin1); m1 = $"vivo {r1.Health.IsAlive}, visible {R19dVisible(r1)}, tinte {R19dTinte(r1)} vs {tin1}"; }
                    if (tv2 >= 0f && m2 == "sin medir" && Time.time - tv2 >= 0.2f) { ok2 = r2.Health.IsAlive && R19dVisible(r2) && !R19dDistinto(R19dTinte(r2), tin2); m2 = $"vivo {r2.Health.IsAlive}, visible {R19dVisible(r2)}, tinte {R19dTinte(r2)} vs {tin2}, fase {RetiroDeEnemigosCaidos.FaseDe(r2)}"; }
                }
                r1.Health.FactorDeDano = 1f; r2.Health.FactorDeDano = 1f;
                W8Ok(ref ok, ok1, sb, $"revivido a los 3 s: conserva renderers y tinte ({m1})");
                W8Ok(ref ok, revivio2 && ok2, sb, $"revivido en pleno destello ({revivio2}; {m2}): renderers y tinte restaurados");
                W8Ok(ref ok, RetiroDeEnemigosCaidos.Cancelados >= canc0 + 1, sb, $"cancelados +{RetiroDeEnemigosCaidos.Cancelados - canc0}");
                r1.Health.TakeDamage(99999, yo.Id, false); r2.Health.TakeDamage(99999, yo.Id, false);   // no dejar vivos quietos delante
            }
            else sb.Append("(sin enemigos para revivir); ");

            // --- 6 bajas en el mismo cuadro: tope del pool y escalonado
            fus = R19dFusileros(yo);
            var seis = new List<Soldier>();
            for (int i = 0; i < fus.Count && seis.Count < 6; i++) seis.Add(R19dPonerDelante(yo, fus[i], 10f + (i / 3) * 1.5f, (i % 3 - 1) * 1.8f));
            Physics.SyncTransforms();
            yield return null;
            int maxActivos = 0;
            foreach (var s in seis) s.Health.TakeDamage(99999, yo.Id, false);
            float t6 = Time.time;
            while (Time.time - t6 < 6.5f) { maxActivos = Mathf.Max(maxActivos, DebrisPool.ActiveCount); yield return null; }
            int apagados = 0; var detalle = new StringBuilder();
            foreach (var s in seis)
            {
                if (s != null && R19dTodoApagado(s)) { apagados++; continue; }
                if (s == null) { detalle.Append("[destruido] "); continue; }
                var prendidos = new List<string>(); foreach (var r in s.GetComponentsInChildren<Renderer>(true)) if (r.enabled) prendidos.Add(r.name);
                detalle.Append($"[{s.name} vivo {s.Health.IsAlive} fase {RetiroDeEnemigosCaidos.FaseDe(s)} prendidos {string.Join("/", prendidos)}] ");
            }
            W8Ok(ref ok, seis.Count >= 6, sb, $"{seis.Count} bajas simultaneas");
            W8Ok(ref ok, apagados == seis.Count, sb, $"{apagados}/{seis.Count} desintegrados a los 6,5 s {detalle}");
            W8Ok(ref ok, maxActivos <= DebrisPool.Budget, sb, $"DebrisPool.ActiveCount maximo {maxActivos} (<= {DebrisPool.Budget})");
            Application.logMessageReceived -= R19dLog;
            W8Ok(ref ok, r19dExcepciones == 0, sb, $"excepciones {r19dExcepciones}");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
