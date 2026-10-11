var d = SP.Player.PlayerInputDriver.Activo; var t = typeof(SP.CameraSystem.CameraRig); var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
t.GetField("rtsCurrentHeight", bf).SetValue(d.Rig, 70f); t.GetField("rtsTargetHeight", bf).SetValue(d.Rig, 70f);
d.Rig.CentrarRtsEn(new UnityEngine.Vector3(0f, 0f, -20f), 1f);
return UnityEngine.Camera.main.transform.position.ToString();
