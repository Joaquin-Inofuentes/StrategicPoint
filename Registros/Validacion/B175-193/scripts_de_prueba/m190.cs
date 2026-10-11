SP.EditorTools.ZzArena.LimpiarSoldados();
var o = SP.EditorTools.ZzArena.Origen;
var d = SP.Player.PlayerInputDriver.Activo;
var dir = SP.Operacion.OperacionDirector.Instancia;
dir.TeletransportarEscuadra(o + new UnityEngine.Vector3(-9f, 0f, 6f), UnityEngine.Quaternion.Euler(0, 90, 0));
var aliados = new System.Collections.Generic.List<SP.Actors.Soldier>();
foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player && s.gameObject.activeInHierarchy) { aliados.Add(s); s.Brain.Pasivo = false; s.Brain.Stance = SP.Ai.CombatStance.Defensiva; }
var seto = UnityEngine.GameObject.Find("Seto_1");
d.Selection.Clear(); foreach (var s in aliados) d.Selection.AddToSelection(s);
var n = d.IssueCoverOrderForSelection(aliados, seto.transform.position + new UnityEngine.Vector3(0.5f, 0f, 0f), seto.transform);
for (int i = 0; i < 3; i++) { SP.EditorTools.ZzArena.Soldado(false, "E190_" + i, SP.Combat.RoleType.Enemy, new UnityEngine.Vector3(16f, 0f, 3f + i * 3f), 270f); }
foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Enemy && s.name.StartsWith("E190")) { s.Brain.Stance = SP.Ai.CombatStance.Libre; s.Brain.Pasivo = false; }
SP.EditorTools.ZzMedir.Contar();
UnityEngine.Time.timeScale = 3f;
return "ordenes de cobertura=" + n + " aliados=" + aliados.Count;
