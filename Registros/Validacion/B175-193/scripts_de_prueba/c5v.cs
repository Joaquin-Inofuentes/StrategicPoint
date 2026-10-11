// codigo viejo: el clic derecho sobre la computadora no la reconoce; cae en la orden de mover al punto de impacto
var d = SP.Player.PlayerInputDriver.Activo;
var ray = UnityEngine.Camera.main.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
if (!UnityEngine.Physics.Raycast(ray, out var h, 500f)) return "sin impacto";
int n = 0; foreach (var s in d.Selection.Selected) { SP.Player.OrderService.IssueMoveOrder(s, h.point + new UnityEngine.Vector3(n++ * 1.2f, 0f, 0f)); }
return "legado: mover " + n + " a " + h.point;
