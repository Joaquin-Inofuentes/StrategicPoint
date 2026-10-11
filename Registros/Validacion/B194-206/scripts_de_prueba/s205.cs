// gesto de llevar a un aliado: se simula el estado "agarrado" y la liberacion de [Espacio] con la mira sobre un punto del piso
var d = SP.Player.PlayerInputDriver.Activo; var t = d.GetType(); var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var mi = t.GetMethod("TickGestoMoverAliado", bf); if (mi == null) return "CODIGO VIEJO: no existe el gesto [Espacio] sobre un aliado";
if (d.Rig.Mode != SP.CameraSystem.ControlMode.Rts) d.AlternarVista();
SP.Actors.Soldier a = null; foreach (var s in SP.Core.ActorRegistry.All) { if (s != null && s.Team == SP.Combat.TeamId.Player && s.gameObject.activeInHierarchy && s != d.Brain.Current) { a = s; break; } }
var destino = a.transform.position + new UnityEngine.Vector3(12f, 0, 4f);
var rt = typeof(SP.CameraSystem.CameraRig); rt.GetField("rtsCurrentHeight", bf).SetValue(d.Rig, 30f); rt.GetField("rtsTargetHeight", bf).SetValue(d.Rig, 30f); d.Rig.CentrarRtsEn(a.transform.position + new UnityEngine.Vector3(6f, 0, 2f), 1f);
t.GetField("gestoAliado", bf).SetValue(d, a); t.GetField("gestoDesde", bf).SetValue(d, UnityEngine.Time.time - 1f);
var rayo = new UnityEngine.Ray(destino + UnityEngine.Vector3.up * 30f, UnityEngine.Vector3.down);
mi.Invoke(d, new object[]{ UnityEngine.InputSystem.Keyboard.current, default(SP.Player.AimResult), rayo });
System.AppDomain.CurrentDomain.SetData("zz205", a);
return a.DisplayName + " en " + a.transform.position + " recibe orden hacia " + destino;
