var d = SP.Player.PlayerInputDriver.Activo; if (d.Rig.Mode != SP.CameraSystem.ControlMode.Rts) d.AlternarVista();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance; var mi = typeof(SP.Player.PlayerInputDriver).GetMethod("ActualizarCursorRts", bf);
d.Selection.Clear(); SP.Presentation.CursorContextual.Restaurar();
mi.Invoke(d, new object[] { new SP.Player.AimResult { Type = SP.Player.AimTargetType.Interactuar } });
string a = "sin seleccion, sobre un interactuable: cursor=" + SP.Presentation.CursorContextual.Actual;
UnityEngine.Transform ob = null; foreach (var m in UnityEngine.Object.FindObjectsByType<SP.Presentation.ObstacleMarker>(UnityEngine.FindObjectsSortMode.None)) { ob = m.transform; break; }
SP.Actors.Soldier uno = null; foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player && s.gameObject.activeInHierarchy && s != d.Brain.Current) { uno = s; break; }
d.Selection.SelectSingle(uno); SP.Presentation.CursorContextual.Restaurar();
mi.Invoke(d, new object[] { new SP.Player.AimResult { Type = SP.Player.AimTargetType.Obstacle, HitTransform = ob } });
return a + " | con 1 seleccionado, sobre un muro demolible: cursor=" + SP.Presentation.CursorContextual.Actual;
