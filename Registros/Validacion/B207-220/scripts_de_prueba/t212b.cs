var ci = System.Globalization.CultureInfo.InvariantCulture;
var ini = new System.Collections.Generic.Dictionary<string, UnityEngine.Vector2>();
foreach (var t in UnityEditor.SessionState.GetString("zz_a", "").Split(';')) { if (t.Length < 3) continue; var kv = t.Split('='); var xy = kv[1].Split(','); ini[kv[0]] = new UnityEngine.Vector2(float.Parse(xy[0], ci), float.Parse(xy[1], ci)); }
var jug = new UnityEngine.Vector2(0f, -48f); int n = 0, llegaron = 0, trabados = 0; string det = "";
foreach (var s in SP.Core.ActorRegistry.All) {
  if (s == null || !ini.ContainsKey(s.name) || s.Health == null || !s.Health.IsAlive) continue;
  var p = s.transform.position; var p2 = new UnityEngine.Vector2(p.x, p.z); float mov = (p2 - ini[s.name]).magnitude; float dj = (p2 - jug).magnitude; n++;
  if (dj < 6f) llegaron++;
  else if (mov < 3f) { trabados++; det += s.name + "[" + s.Brain.State + " se_movio=" + mov.ToString("0.0", ci) + "m dJug=" + dj.ToString("0", ci) + "m] "; }
}
int desatascos = 0, reubic = 0; foreach (var s in SP.Core.ActorRegistry.All) { if (s == null || !ini.ContainsKey(s.name)) continue; var pd = typeof(SP.Ai.AiBrain).GetProperty("DesatascosHechos"); var pr = typeof(SP.Ai.AiBrain).GetProperty("ReubicacionesDeEnemigo"); if (pd != null) desatascos += (int)pd.GetValue(s.Brain); if (pr != null) reubic += (int)pr.GetValue(s.Brain); }
return "avisos de atasco del cerebro=" + desatascos + " teletransportes de enemigos trabados=" + reubic + " | vivos=" + n + " llegaron(<6m)=" + llegaron + " TRABADOS(<3m recorridos)=" + trabados + " | " + det;
