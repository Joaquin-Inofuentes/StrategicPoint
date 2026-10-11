var d = SP.Player.PlayerInputDriver.Activo; var ci = System.Globalization.CultureInfo.InvariantCulture; string r = "";
SP.Actors.Soldier lejos = null; int k = 0;
foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player && s.gameObject.activeInHierarchy && s != d.Brain.Current) { if (k++ == 1) lejos = s; }
d.Selection.Clear();
var me = d.Brain.Current.transform.position; lejos.transform.position = me + new UnityEngine.Vector3(0f, 0f, 90f);
foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player && s.gameObject.activeInHierarchy && s != d.Brain.Current) r += s.name + "=" + (s.transform.position - me).magnitude.ToString("0", ci) + "m ";
int n = d.SeguirTodosSiOSi(); r += "| Squad=" + (d.Squad != null ? d.Squad.Count : -1);
return "aliados a: " + r + "| SIGANME llego a " + n + " aliado(s)";
