var d = SP.Player.PlayerInputDriver.Activo; var ci = System.Globalization.CultureInfo.InvariantCulture; string r = ""; float peor = 0f;
foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player && s != d.Brain.Current && s.gameObject.activeInHierarchy) { float dist = (s.transform.position - d.Brain.Current.transform.position).magnitude; if (dist > peor) peor = dist; r += s.name + "[" + s.Brain.State + " a " + dist.ToString("0.0", ci) + " m] "; }
return "peor distancia al jugador=" + peor.ToString("0.0", ci) + " m | " + r;
