SP.EditorTools.ZzArena.LimpiarSoldados();
var o = SP.EditorTools.ZzArena.Origen;
if (UnityEngine.GameObject.Find("Pared_W") == null) {
  SP.EditorTools.ZzArena.Muro("Pared_W", new UnityEngine.Vector3(-16.5f, 0f, -10f), new UnityEngine.Vector3(27f, 3f, 1f));
  SP.EditorTools.ZzArena.Muro("Pared_E", new UnityEngine.Vector3(16.5f, 0f, -10f), new UnityEngine.Vector3(27f, 3f, 1f));
  SP.EditorTools.ZzArena.Rehornear();
}
var dir = SP.Operacion.OperacionDirector.Instancia;
dir.TeletransportarEscuadra(o + new UnityEngine.Vector3(0f, 0f, -30f), UnityEngine.Quaternion.identity);
var meta = o + new UnityEngine.Vector3(0f, 0f, 12f);
var grupo = new System.Collections.Generic.List<SP.Actors.Soldier>();
foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player && s.gameObject.activeInHierarchy) { grupo.Add(s); s.Brain.Pasivo = false; s.Brain.Stance = SP.Ai.CombatStance.Libre; }
foreach (var s in grupo) SP.Player.OrderService.IssueMoveOrder(s, meta + new UnityEngine.Vector3((grupo.IndexOf(s) - 1) * 1.8f, 0f, 0f));
SP.EditorTools.ZzMedir.Contar(); SP.EditorTools.ZzMedir.Seguir(grupo, meta);
UnityEngine.Time.timeScale = 3f;
return "3 aliados hacia " + meta;
