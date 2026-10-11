SP.EditorTools.ZzArena.LimpiarSoldados();
var o = SP.EditorTools.ZzArena.Origen;
if (UnityEngine.GameObject.Find("Pared_W") == null) {
  SP.EditorTools.ZzArena.Muro("Pared_W", new UnityEngine.Vector3(-16.5f, 0f, -10f), new UnityEngine.Vector3(27f, 3f, 1f));
  SP.EditorTools.ZzArena.Muro("Pared_E", new UnityEngine.Vector3(16.5f, 0f, -10f), new UnityEngine.Vector3(27f, 3f, 1f));
  SP.EditorTools.ZzArena.Rehornear();
}
var dir = SP.Operacion.OperacionDirector.Instancia;
dir.TeletransportarEscuadra(o + new UnityEngine.Vector3(55f, 0f, 55f), UnityEngine.Quaternion.identity);
foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player && s.Brain != null) { s.Brain.Pasivo = true; s.Brain.CancelOrder(); }
var meta = o + new UnityEngine.Vector3(0f, 0f, 12f);
var grupo = new System.Collections.Generic.List<SP.Actors.Soldier>();
for (int i = 0; i < 8; i++) {
  SP.EditorTools.ZzArena.Soldado(false, "E186_" + i, SP.Combat.RoleType.Enemy, new UnityEngine.Vector3(-7f + i * 2f, 0f, -30f), 0f);
}
foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Enemy && s.name.StartsWith("E186")) { grupo.Add(s); s.Brain.Stance = SP.Ai.CombatStance.Libre; s.Brain.SetPatrolRoute(new[] { meta, meta }); }
SP.EditorTools.ZzMedir.Contar(); SP.EditorTools.ZzMedir.Seguir(grupo, meta);
UnityEngine.Time.timeScale = 3f;
return "8 enemigos hacia " + meta;
