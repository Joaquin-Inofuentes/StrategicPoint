SP.Interaction.IOrdenableAAliados comp = null; UnityEngine.Component cc = null;
foreach (var mb in UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsSortMode.None)) { var o = mb as SP.Interaction.IOrdenableAAliados; if (o != null && o.NombreParaOrden.ToUpper().Contains("COMPUT")) { cc = mb; break; } }
var sp = UnityEngine.Camera.main.WorldToScreenPoint(cc.transform.position + new UnityEngine.Vector3(0,0.5f,0));
var m = UnityEngine.InputSystem.Mouse.current; var st = new UnityEngine.InputSystem.LowLevel.MouseState(); st.position = new UnityEngine.Vector2(sp.x, sp.y);
st = st.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right, __B__); UnityEngine.InputSystem.InputSystem.QueueStateEvent(m, st); return "ok " + sp;
