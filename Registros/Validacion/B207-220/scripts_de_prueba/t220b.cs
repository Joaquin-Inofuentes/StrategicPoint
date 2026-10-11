var d = SP.Player.PlayerInputDriver.Activo; if (d.Rig.Mode == SP.CameraSystem.ControlMode.Rts) d.AlternarVista(); var cam = SP.Core.CamaraPrincipal.Actual; var yo = d.Brain.Current.transform.position;
return "camara a " + (cam.transform.position - yo).magnitude.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m del soldado que manejabas";
