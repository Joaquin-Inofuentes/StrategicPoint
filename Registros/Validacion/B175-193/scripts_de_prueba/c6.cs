SP.Interaction.IOrdenableAAliados comp = null;
foreach (var mb in UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsSortMode.None)) { var o = mb as SP.Interaction.IOrdenableAAliados; if (o != null && o.NombreParaOrden.ToUpper().Contains("COMPUT")) { comp = o; break; } }
var all = new System.Collections.Generic.List<SP.Actors.Soldier>(); foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player && s.gameObject.activeInHierarchy) all.Add(s);
var op = comp.ElegirOperador(all); string m; bool ok = comp.EnviarA(op, out m); return "hack manual " + op.DisplayName + " " + ok + " " + m;
