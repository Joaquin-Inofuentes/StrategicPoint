UnityEngine.Time.timeScale = 1f;
int vivosA = 0, vivosE = 0; float vidaA = 0;
foreach (var s in SP.Core.ActorRegistry.All) { if (s == null || !s.gameObject.activeInHierarchy || !s.Health.IsAlive) continue; if (s.Team == SP.Combat.TeamId.Player) { vivosA++; vidaA += s.Health.Current; } else if (s.name.StartsWith("E190")) vivosE++; }
return "tiros aliados=" + SP.EditorTools.ZzMedir.disparos[0] + " enemigos=" + SP.EditorTools.ZzMedir.disparos[1] + " | aliados vivos=" + vivosA + " (vida " + vidaA + ") enemigos vivos=" + vivosE;
