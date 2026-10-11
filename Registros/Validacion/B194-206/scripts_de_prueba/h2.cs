var herido = (SP.Actors.Soldier)System.AppDomain.CurrentDomain.GetData("zzHerido");
var sp = UnityEngine.Camera.main.WorldToScreenPoint(herido.transform.position + new UnityEngine.Vector3(0,0.9f,0));
var m = UnityEngine.InputSystem.Mouse.current; var v = new UnityEngine.Vector2(sp.x, sp.y);
m.WarpCursorPosition(v); UnityEngine.InputSystem.LowLevel.InputState.Change(m.position, v);
return sp.ToString();
