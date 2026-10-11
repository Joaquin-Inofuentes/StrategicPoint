var d = SP.Player.PlayerInputDriver.Activo; var dir = SP.Operacion.OperacionDirector.Instancia;
SP.Interaction.IOrdenableAAliados comp = null; UnityEngine.Component cc = null;
foreach (var mb in UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsSortMode.None)) { var o = mb as SP.Interaction.IOrdenableAAliados; if (o != null && o.NombreParaOrden.ToUpper().Contains("COMPUT")) { comp = o; cc = mb; break; } }
if (comp == null) return "sin computadora";
var p = cc.transform.position;
dir.TeletransportarEscuadra(p + new UnityEngine.Vector3(0,0,-9), UnityEngine.Quaternion.identity);
if (d.Rig.Mode != SP.CameraSystem.ControlMode.Rts) d.AlternarVista();
d.Selection.Clear();
int n = 0; foreach (var s in SP.Core.ActorRegistry.All) { if (s == null || s.Team != SP.Combat.TeamId.Player || !s.gameObject.activeInHierarchy) continue; if (n++ == 0) d.Selection.SelectSingle(s); else d.Selection.AddToSelection(s); }
var t = typeof(SP.CameraSystem.CameraRig); var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
t.GetField("rtsCurrentHeight", bf).SetValue(d.Rig, 22f); t.GetField("rtsTargetHeight", bf).SetValue(d.Rig, 22f);
d.Rig.CentrarRtsEn(p + new UnityEngine.Vector3(0,0,-4), 1f);
return comp.NombreParaOrden + " " + p + " sel=" + d.Selection.Selected.Count + " habilitado=" + comp.Habilitado + " quien=" + comp.QuienDebe;
