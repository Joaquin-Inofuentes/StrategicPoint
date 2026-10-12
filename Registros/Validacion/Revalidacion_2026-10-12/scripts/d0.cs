var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var fa = typeof(SP.Ai.AiBrain).GetField("agent", bf); var ft = typeof(SP.Ai.AiBrain).GetField("agentTieneDestino", bf);
var sb = new System.Text.StringBuilder("t=" + UnityEngine.Time.time.ToString("0.0") + " ");
var o = SP.EditorTools.ZzArena.Origen;
foreach (var s in SP.Core.ActorRegistry.All) {
  if (s == null || !s.gameObject.activeInHierarchy || !s.name.StartsWith("E186")) continue;
  var p = s.transform.position - o; var ag = fa.GetValue(s.Brain) as UnityEngine.AI.NavMeshAgent;
  sb.Append(s.name.Substring(5) + "[" + s.Brain.State + " (" + p.x.ToString("0.0") + "," + p.z.ToString("0.0") + ")");
  if (ag != null && ag.enabled) sb.Append(" on=" + ag.isOnNavMesh + " td=" + ft.GetValue(s.Brain) + " pend=" + ag.pathPending + " path=" + ag.hasPath + " " + ag.pathStatus + " rem=" + ag.remainingDistance.ToString("0") + " nx=(" + (ag.nextPosition - o).x.ToString("0.0") + "," + (ag.nextPosition - o).z.ToString("0.0") + ")"); else sb.Append(" sinAgent");
  sb.Append("] ");
}
return sb.ToString();
