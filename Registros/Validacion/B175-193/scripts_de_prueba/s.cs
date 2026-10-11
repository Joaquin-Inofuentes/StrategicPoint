foreach (var n in new[]{"Montanas","Fondo_Horizonte"}) { var g = UnityEngine.GameObject.Find(n); if (g != null) g.SetActive(false); }
SP.CameraSystem.CameraRig.LimiteDePaneoRts = new UnityEngine.Rect(-3000,-3000,6000,6000);
var r = SP.EditorTools.ZzArena.Construir(1);
var o = SP.EditorTools.ZzArena.Origen;
var l = new UnityEngine.GameObject("ZzSol").AddComponent<UnityEngine.Light>();
l.type = UnityEngine.LightType.Directional; l.intensity = 2.0f; l.shadows = UnityEngine.LightShadows.Soft;
l.transform.rotation = UnityEngine.Quaternion.Euler(55,-30,0);
UnityEngine.RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
UnityEngine.RenderSettings.ambientLight = new UnityEngine.Color(0.55f,0.58f,0.62f);
UnityEngine.RenderSettings.fog = false;
var dir = SP.Operacion.OperacionDirector.Instancia;
dir.TeletransportarEscuadra(o + new UnityEngine.Vector3(0,0,-30), UnityEngine.Quaternion.identity);
var d = SP.Player.PlayerInputDriver.Activo;
if (d.Rig.Mode != SP.CameraSystem.ControlMode.Rts) d.AlternarVista();
d.Rig.CentrarRtsEn(o + new UnityEngine.Vector3(0,0,-5), 30f);
return r;
