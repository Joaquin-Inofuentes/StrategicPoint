var d = SP.Player.PlayerInputDriver.Activo;
SP.Interaction.IOrdenableAAliados comp = null;
var ray = UnityEngine.Camera.main.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
comp = d.OrdenableBajoRayoRts(ray);
if (comp == null) return "el rayo no toca la computadora";
bool ok = d.EnviarAInteractuarDesdeRts(comp);
return "orden=" + ok + " " + comp.NombreParaOrden;
