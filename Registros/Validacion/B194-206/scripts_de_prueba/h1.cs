SP.Player.PedidoDeCuracion.AtencionAutomatica = false; var d = SP.Player.PlayerInputDriver.Activo; var dir = SP.Operacion.OperacionDirector.Instancia;
if (d.Rig.Mode != SP.CameraSystem.ControlMode.Rts) d.AlternarVista();
SP.Actors.Soldier herido = null, otro = null;
foreach (var s in SP.Core.ActorRegistry.All) { if (s == null || s.Team != SP.Combat.TeamId.Player || !s.gameObject.activeInHierarchy || s == d.Brain.Current) continue; if (herido == null && s.Role != SP.Combat.RoleType.Medic) herido = s; else if (otro == null) otro = s; }
herido.Health.RestaurarVida(herido.Health.MaxHealth / 4);
d.Selection.Clear(); d.Selection.SelectSingle(otro);
var t = typeof(SP.CameraSystem.CameraRig); var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
t.GetField("rtsCurrentHeight", bf).SetValue(d.Rig, 16f); t.GetField("rtsTargetHeight", bf).SetValue(d.Rig, 16f);
d.Rig.CentrarRtsEn(herido.transform.position, 1f);
System.AppDomain.CurrentDomain.SetData("zzHerido", herido);
return herido.DisplayName + " vida=" + herido.Health.Current + "/" + herido.Health.MaxHealth + " otro=" + otro.DisplayName + " pos=" + herido.transform.position;
