var d = SP.Player.PlayerInputDriver.Activo; var rig = d.Rig; var t = typeof(SP.CameraSystem.CameraRig);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
t.GetField("rtsCurrentHeight", bf).SetValue(rig, __H__f);
t.GetField("rtsTargetHeight", bf).SetValue(rig, __H__f);
rig.CentrarRtsEn(new UnityEngine.Vector3(__X__f, 0f, __Z__f), 1f);
return UnityEngine.Camera.main.transform.position.ToString();
