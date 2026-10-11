var d = SP.Player.PlayerInputDriver.Activo; var dir = SP.Operacion.OperacionDirector.Instancia; SP.Actors.Soldier e = null; float best = 1e9f;
foreach (var s in SP.Core.ActorRegistry.All) { if (s == null || s.Team != SP.Combat.TeamId.Enemy || s.Health == null || !s.Health.IsAlive) continue; float dd = (s.transform.position - d.Brain.Current.transform.position).sqrMagnitude; if (dd < best) { best = dd; e = s; } }
if (e == null) return "sin enemigos";

var p = e.transform.position; var f = e.transform.forward; f.y = 0; f.Normalize();
dir.TeletransportarEscuadra(p + f * 7f, UnityEngine.Quaternion.LookRotation(-f));
if (d.Rig.Mode != SP.CameraSystem.ControlMode.Fps) d.AlternarVista();
return e.DisplayName + " " + p + " mat=" + e.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>().sharedMaterial.name + " renderers=" + e.GetComponentsInChildren<UnityEngine.Renderer>().Length;
