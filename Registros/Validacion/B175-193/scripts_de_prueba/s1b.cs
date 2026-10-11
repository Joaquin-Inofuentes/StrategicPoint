var seto = UnityEngine.GameObject.Find("Seto_1");
var sp = UnityEngine.Camera.main.WorldToScreenPoint(seto.transform.position + new UnityEngine.Vector3(0,0.65f,0));
var m = UnityEngine.InputSystem.Mouse.current;
m.WarpCursorPosition(new UnityEngine.Vector2(sp.x, sp.y));
UnityEngine.InputSystem.LowLevel.InputState.Change(m.position, new UnityEngine.Vector2(sp.x, sp.y));
return sp.ToString();
