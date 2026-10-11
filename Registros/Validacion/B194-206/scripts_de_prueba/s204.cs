var d = SP.Player.PlayerInputDriver.Activo; if (d.Rig.Mode != SP.CameraSystem.ControlMode.Fps) d.AlternarVista();
var f = typeof(SP.CameraSystem.CameraRig).GetField("zoomed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
f.SetValue(d.Rig, __Z__);
int a0 = SP.Presentation.ImpactFx.ActiveCount; bool ok = d.Brain.Fire(d.Brain.Current.transform.position + d.Brain.Current.transform.forward * 25f + UnityEngine.Vector3.up * 1.4f);
int a1 = SP.Presentation.ImpactFx.ActiveCount; f.SetValue(d.Rig, false);
return "zoom=__Z__ tiro=" + ok + " efectos antes=" + a0 + " despues del tiro=" + a1;
