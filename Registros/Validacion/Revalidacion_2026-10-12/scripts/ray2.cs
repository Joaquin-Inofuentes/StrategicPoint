SP.Interaction.IOrdenableAAliados comp = null; UnityEngine.Component cc = null;
foreach (var mb in UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsSortMode.None)) { var o = mb as SP.Interaction.IOrdenableAAliados; if (o != null && o.NombreParaOrden.ToUpper().Contains("COMPUT")) { comp = o; cc = mb; break; } }
var cam = UnityEngine.Camera.main;
var sp = cam.WorldToScreenPoint(cc.transform.position + new UnityEngine.Vector3(0,0.5f,0));
var ray = cam.ScreenPointToRay(sp);
var hits = UnityEngine.Physics.RaycastAll(ray, 200f, ~0, UnityEngine.QueryTriggerInteraction.Collide);
System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
var sb = new System.Text.StringBuilder("cam=" + cam.transform.position + " sp=" + sp + " hits:");
foreach (var h in hits) sb.Append(" " + h.collider.name + "(" + (h.collider.transform.parent != null ? h.collider.transform.parent.name : "-") + ",d=" + h.distance.ToString("0.0") + ")");
sb.Append(" | colliders de la computadora:");
foreach (var c in cc.GetComponentsInChildren<UnityEngine.Collider>(true)) sb.Append(" " + c.name + "(en=" + c.enabled + ",trig=" + c.isTrigger + ",b=" + c.bounds.center + "/" + c.bounds.size + ")");
var d = SP.Player.PlayerInputDriver.Activo; var o2 = d.OrdenableBajoRayoRts(ray);
sb.Append(" | OrdenableBajoRayoRts=" + (o2 != null ? o2.NombreParaOrden : "null"));
return sb.ToString();
