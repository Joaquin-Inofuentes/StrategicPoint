var ci = System.Globalization.CultureInfo.InvariantCulture;
var dir = SP.Operacion.OperacionDirector.Instancia;
dir.TeletransportarEscuadra(new UnityEngine.Vector3(0f, 0.8f, -48f), UnityEngine.Quaternion.identity);
UnityEngine.Time.timeScale = 1f;
var sb = new System.Text.StringBuilder(); int n = 0;
foreach (var s in UnityEngine.Object.FindObjectsByType<SP.Actors.Soldier>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)) {
  if (!s.name.StartsWith("Reserva_CD")) continue;
  s.gameObject.SetActive(true); n++; SP.Core.ApoyoEnElPiso.Apoyar(s.transform); s.Brain.Pasivo = false;
  var p = s.transform.position; sb.Append(s.name + "=" + p.x.ToString("0.0", ci) + "," + p.z.ToString("0.0", ci) + ";");
}
foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player) { s.Brain.Pasivo = true; s.Health.Invulnerable = true; }
foreach (var s in UnityEngine.Object.FindObjectsByType<SP.Actors.Soldier>(UnityEngine.FindObjectsSortMode.None)) if (s.name.StartsWith("Reserva_CD")) s.Brain.IssueMoveOrder(new UnityEngine.Vector3(0f, 0.8f, -48f));
UnityEditor.SessionState.SetString("zz_a", sb.ToString());
return "reservas del centro de datos activadas=" + n + " con orden de ir al jugador (aliados pasivos e invulnerables)";
