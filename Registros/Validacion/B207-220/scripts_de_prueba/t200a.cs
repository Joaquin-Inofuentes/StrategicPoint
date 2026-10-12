var d = SP.Player.PlayerInputDriver.Activo; var ci = System.Globalization.CultureInfo.InvariantCulture;
var dir = SP.Operacion.OperacionDirector.Instancia; dir.TeletransportarEscuadra(new UnityEngine.Vector3(0f, 0.8f, -48f), UnityEngine.Quaternion.identity);
UnityEngine.Time.timeScale = 3f; string r = ""; int k = 0;
var destinos = new UnityEngine.Vector3[] { new UnityEngine.Vector3(-46.9f, 0.8f, -53.9f), new UnityEngine.Vector3(-15f, 0.8f, -80f) };
foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player && s != d.Brain.Current && s.gameObject.activeInHierarchy && k < 2) { s.Health.Invulnerable = true; s.Brain.Pasivo = false; s.Brain.IssueMoveOrder(destinos[k]); r += s.name + " -> destino dentro de un solido " + destinos[k] + "; "; k++; }
UnityEditor.SessionState.SetString("zz_t0", UnityEngine.Time.time.ToString(ci)); return r;
