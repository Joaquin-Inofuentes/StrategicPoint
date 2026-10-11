var d = SP.Player.PlayerInputDriver.Activo; SP.Actors.Soldier e = null; float best = 1e9f;
foreach (var s in SP.Core.ActorRegistry.All) { if (s == null || s.Team != SP.Combat.TeamId.Enemy || !s.gameObject.activeInHierarchy || !s.Health.IsAlive) continue; float dd = (s.transform.position - d.Brain.Current.transform.position).sqrMagnitude; if (dd < best) { best = dd; e = s; } }
if (e == null) return "sin enemigos";
foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Brain != null) s.Brain.Pasivo = true;
var p = e.transform.position; System.AppDomain.CurrentDomain.SetData("zzE", p);
var l = new UnityEngine.GameObject("ZzLuz").AddComponent<UnityEngine.Light>(); l.type = UnityEngine.LightType.Directional; l.intensity = 0.55f; l.transform.rotation = UnityEngine.Quaternion.Euler(50, 40, 0);
return e.DisplayName + " " + p + " mat=" + e.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>().sharedMaterial.name;
