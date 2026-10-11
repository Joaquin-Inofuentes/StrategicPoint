var d = SP.Player.PlayerInputDriver.Activo; if (d.Rig.Mode == SP.CameraSystem.ControlMode.Rts) d.AlternarVista();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance; var fi = typeof(SP.UI.AimUI).GetField("crosshair", bf);
var img = (UnityEngine.UI.Image)fi.GetValue(d.AimUiRef);
d.AimUiRef.UpdateFromAimResult(new SP.Player.AimResult { Type = SP.Player.AimTargetType.Ground }); string suelo = img.sprite != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(img.sprite).ToString() : "null";
d.AimUiRef.UpdateFromAimResult(new SP.Player.AimResult { Type = SP.Player.AimTargetType.Interactuar }); string inter = img.sprite != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(img.sprite).ToString() : "null";
d.AimUiRef.UpdateFromAimResult(new SP.Player.AimResult { Type = SP.Player.AimTargetType.Ground }); string vuelve = img.sprite != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(img.sprite).ToString() : "null";
return "FPS mira sobre suelo=" + suelo + " | sobre interactuable=" + inter + " | forma distinta=" + (suelo != inter) + " | vuelve al soltar=" + (vuelve == suelo);
