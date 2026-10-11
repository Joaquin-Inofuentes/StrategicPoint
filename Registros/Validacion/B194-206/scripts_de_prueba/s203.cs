var d = SP.Player.PlayerInputDriver.Activo; if (d.Rig.Mode != SP.CameraSystem.ControlMode.Rts) d.AlternarVista();
SP.CameraSystem.CameraRig.LimiteDePaneoRts = null;
SP.Core.NavService.TryArea(out var lim);
d.Rig.RecenterOn(new UnityEngine.Vector3(lim.center.x, 0, lim.max.z + 900f));
return "borde norte del terreno z=" + lim.max.z.ToString("0") + " | foco pedido z=" + (lim.max.z + 900f).ToString("0") + " | foco real tras acotar z=" + d.Rig.PanTargetActual.z.ToString("0");
