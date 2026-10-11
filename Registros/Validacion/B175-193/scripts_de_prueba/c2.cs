SP.Interaction.IOrdenableAAliados comp = null; UnityEngine.Component cc = null;
foreach (var mb in UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsSortMode.None)) { var o = mb as SP.Interaction.IOrdenableAAliados; if (o != null && o.NombreParaOrden.ToUpper().Contains("COMPUT")) { comp = o; cc = mb; break; } }
var sp = UnityEngine.Camera.main.WorldToScreenPoint(cc.transform.position + new UnityEngine.Vector3(0,0.5f,0));
var m = UnityEngine.InputSystem.Mouse.current; var v = new UnityEngine.Vector2(sp.x, sp.y);
m.WarpCursorPosition(v); UnityEngine.InputSystem.LowLevel.InputState.Change(m.position, v);
return sp.ToString();
