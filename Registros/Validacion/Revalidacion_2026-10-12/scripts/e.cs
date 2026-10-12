var d = SP.Player.PlayerInputDriver.Activo; SP.Actors.Soldier e = null; float best = 1e9f;
foreach (var s in SP.Core.ActorRegistry.All) { if (s == null || s.Team != SP.Combat.TeamId.Enemy || !s.gameObject.activeInHierarchy || !s.Health.IsAlive) continue; float dd = (s.transform.position - d.Brain.Current.transform.position).sqrMagnitude; if (dd < best) { best = dd; e = s; } }
UnityEngine.Time.timeScale = 0f;
var p = e.transform.position; System.AppDomain.CurrentDomain.SetData("zzE", p);
var sb = new System.Text.StringBuilder(e.name + " pos=" + p + " renderers:");
foreach (var r in e.GetComponentsInChildren<UnityEngine.Renderer>(true)) sb.Append(" " + r.name + "(en=" + r.enabled + ",act=" + r.gameObject.activeInHierarchy + ",vis=" + r.isVisible + ",sh=" + r.shadowCastingMode + ",mat=" + (r.sharedMaterial != null ? r.sharedMaterial.name : "null") + ")");
return sb.ToString();
