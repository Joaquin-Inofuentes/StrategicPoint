var d = SP.Player.PlayerInputDriver.Activo; if (d.Rig.Mode != SP.CameraSystem.ControlMode.Fps) d.AlternarVista();
var w = d.Brain.Current.Weapon; var t = w.GetType(); var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
t.GetField("spreadDeg", bf).SetValue(w, 6f);
var ci = System.Globalization.CultureInfo.InvariantCulture; float c = w.SpreadDegEfectivo;
return "rafaga sostenida (cono acumulado al tope): cono efectivo=" + c.ToString("0.00", ci) + " grados; a 30 m el disparo se desvia hasta " + (30f * UnityEngine.Mathf.Tan(c * UnityEngine.Mathf.Deg2Rad)).ToString("0.0", ci) + " m del punto apuntado";
