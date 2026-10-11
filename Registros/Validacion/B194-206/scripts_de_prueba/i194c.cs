var d = SP.Player.PlayerInputDriver.Activo; SP.Actors.Soldier e = null; float best = 1e9f;
foreach (var s in SP.Core.ActorRegistry.All) { if (s == null || s.Team != SP.Combat.TeamId.Enemy || s.Health == null || !s.Health.IsAlive) continue; float dd = (s.transform.position - d.Brain.Current.transform.position).sqrMagnitude; if (dd < best) { best = dd; e = s; } }
if (e == null) return "sin enemigo activo";
e.gameObject.SetActive(true); var yo = d.Brain.Current.transform; var p = yo.position + yo.forward * 5.5f + yo.right * 1.5f;
var ag = e.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag != null && ag.enabled) ag.Warp(p); else e.transform.position = p;
e.transform.rotation = UnityEngine.Quaternion.LookRotation(-yo.forward + yo.right * 0.6f);
e.Brain.Pasivo = true; foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player && s.Brain != null) s.Brain.Pasivo = true;
return e.DisplayName + " colocado en " + p;
