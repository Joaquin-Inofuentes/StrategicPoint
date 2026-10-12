var d = SP.Player.PlayerInputDriver.Activo; if (d.Rig.Mode == SP.CameraSystem.ControlMode.Rts) d.AlternarVista();
var dir = SP.Operacion.OperacionDirector.Instancia; dir.TeletransportarEscuadra(new UnityEngine.Vector3(0f, 0.8f, -48f), UnityEngine.Quaternion.identity);
UnityEngine.Time.timeScale = 1f;
foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player) { s.Health.Invulnerable = true; s.Brain.Pasivo = false; }
int n = d.SeguirTodosSiOSi(); UnityEditor.SessionState.SetInt("zz_paso", 0);
return "SIGANME a " + n + " aliados; el jugador (" + d.Brain.Current.name + ") empieza a caminar en circulo de 18 m";
