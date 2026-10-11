var d = SP.Player.PlayerInputDriver.Activo; if (d.Rig.Mode != SP.CameraSystem.ControlMode.Rts) d.AlternarVista();
var dir = SP.Operacion.OperacionDirector.Instancia; var tq = dir.tanque;
SP.Actors.Soldier uno = null; foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player && s.gameObject.activeInHierarchy && s != d.Brain.Current) { uno = s; break; }
d.Selection.Clear(); d.Selection.SelectSingle(uno);
var mi = typeof(SP.Player.PlayerInputDriver).GetMethod("MontarDesdeRts");
if (mi == null) return "CODIGO VIEJO: clic derecho sobre el tanque no da ninguna orden de subir";
int n = (int)mi.Invoke(d, new object[] { new SP.Player.AimResult { Type = SP.Player.AimTargetType.Vehicle, Vehicle = tq } });
return "clic derecho sobre el tanque con " + uno.name + " seleccionado: ordenados a subir=" + n + " estado=" + uno.Brain.State;
