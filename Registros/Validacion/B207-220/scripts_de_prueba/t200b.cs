var d = SP.Player.PlayerInputDriver.Activo; var ci = System.Globalization.CultureInfo.InvariantCulture; string r = "";
foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player && s != d.Brain.Current && s.gameObject.activeInHierarchy) r += s.name + "[" + s.Brain.State + (s.Brain.State == SP.Ai.AiState.MovingToOrder ? " SIGUE INTENTANDO" : " orden cerrada") + "] ";
return "a los " + (UnityEngine.Time.time - float.Parse(UnityEditor.SessionState.GetString("zz_t0", "0"), ci)).ToString("0", ci) + " s de juego: " + r;
