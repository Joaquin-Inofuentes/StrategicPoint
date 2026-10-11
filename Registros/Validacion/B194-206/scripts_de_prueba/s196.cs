// destino bloqueado: un punto sobre un solido; antes se rechazaba, ahora va al lugar libre mas cercano
var d = SP.Player.PlayerInputDriver.Activo; var me = d.Brain.Current.transform.position;
UnityEngine.Collider sol = null; float best = 1e9f;
foreach (var c in UnityEngine.Physics.OverlapSphere(me, 40f, ~0, UnityEngine.QueryTriggerInteraction.Ignore)) { if (!SP.Core.NavService.BlocksMovement(c) || c.bounds.size.y < 2f || c.bounds.size.x < 3f) continue; float dd = (c.bounds.center - me).sqrMagnitude; if (dd < best) { best = dd; sol = c; } }
if (sol == null) return "sin solido cerca";
var p = new UnityEngine.Vector3(sol.bounds.center.x, 0.9f, sol.bounds.center.z);
bool valido = SP.Player.OrderService.IsValidDestination(p);
var mi = typeof(SP.Player.OrderService).GetMethod("TryResolveDestination");
string r = sol.name + " centro=" + p + " IsValidDestination=" + valido;
UnityEngine.Vector3 destino = p; bool hay = false;
if (mi != null) { var a = new object[]{ p, null }; hay = (bool)mi.Invoke(null, a); destino = (UnityEngine.Vector3)a[1]; r += " | TryResolveDestination=" + hay + " -> " + destino + " (a " + (destino - p).magnitude.ToString("0.0") + " m)"; }
else r += " | CODIGO VIEJO: se muestra 'DESTINO BLOQUEADO' y no se ordena nada";
System.AppDomain.CurrentDomain.SetData("zz196", hay ? (object)destino : null);
if (d.Rig.Mode != SP.CameraSystem.ControlMode.Rts) d.AlternarVista();
d.Selection.Clear(); int n = 0; foreach (var s in SP.Core.ActorRegistry.All) { if (s == null || s.Team != SP.Combat.TeamId.Player || !s.gameObject.activeInHierarchy || s == d.Brain.Current) continue; if (n++ == 0) d.Selection.SelectSingle(s); else d.Selection.AddToSelection(s); }
if (hay) { int k = 0; foreach (var s2 in d.Selection.Selected) SP.Player.OrderService.IssueMoveOrder(s2, destino + new UnityEngine.Vector3(k++ * 1.5f, 0, 0)); }
var t = typeof(SP.CameraSystem.CameraRig); var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
t.GetField("rtsCurrentHeight", bf).SetValue(d.Rig, 28f); t.GetField("rtsTargetHeight", bf).SetValue(d.Rig, 28f); d.Rig.CentrarRtsEn(p, 1f);
return r;
