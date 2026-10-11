var d = SP.Player.PlayerInputDriver.Activo; if (d.Rig.Mode != SP.CameraSystem.ControlMode.Rts) d.AlternarVista();
SP.Actors.Soldier uno = null; foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player && s.gameObject.activeInHierarchy && s != d.Brain.Current) { uno = s; break; }
d.Selection.Clear(); d.Selection.SelectSingle(uno);
var cam = d.Rig.Cam; var meta = uno.transform.position + UnityEngine.Vector3.up * 0.9f; var dir = (meta - cam.transform.position); float dist = dir.magnitude; dir /= dist;
var cubo = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube); cubo.name = "ZzOclusor";
cubo.transform.position = cam.transform.position + dir * (dist * 0.45f); cubo.transform.rotation = UnityEngine.Quaternion.LookRotation(dir);
cubo.transform.localScale = new UnityEngine.Vector3(14f, 14f, 1.2f);
return "pared de prueba entre la camara y " + uno.name + " a " + (dist * 0.45f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m de la camara";
