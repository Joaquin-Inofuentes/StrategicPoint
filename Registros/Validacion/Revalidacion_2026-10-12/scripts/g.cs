var o = SP.EditorTools.ZzArena.Origen; var e = UnityEngine.GameObject.Find("E186_0");
if (e == null) return "no E186_0";
var ag = e.GetComponent<UnityEngine.AI.NavMeshAgent>(); var st = UnityEngine.AI.NavMesh.GetSettingsByID(ag.agentTypeID); var col = e.GetComponent<UnityEngine.Collider>();
var sb = new System.Text.StringBuilder();
sb.Append("agentRadius(bake)=" + st.agentRadius + " ag.radius=" + ag.radius + " cuerpo=" + col.bounds.size + " pos=" + (e.transform.position - o) + " steer=" + (ag.steeringTarget - o) + " esquinas:");
foreach (var c in ag.path.corners) sb.Append(" " + (c - o).ToString("0.0"));
var hits = UnityEngine.Physics.OverlapSphere(e.transform.position, 0.9f); sb.Append(" | toca:");
foreach (var h in hits) if (h.gameObject != e) sb.Append(" " + h.name);
return sb.ToString();
