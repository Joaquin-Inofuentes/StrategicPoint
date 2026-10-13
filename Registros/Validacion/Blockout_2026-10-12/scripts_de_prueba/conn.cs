var sb=new System.Text.StringBuilder();
var start=UnityEngine.GameObject.Find("Zona_Aliada").transform.position;
UnityEngine.AI.NavMeshHit h; UnityEngine.AI.NavMesh.SamplePosition(start,out h,6f,UnityEngine.AI.NavMesh.AllAreas); start=h.position;
System.Func<UnityEngine.Vector3,string,string> ir=(p,n)=>{ UnityEngine.AI.NavMeshHit q; if(!UnityEngine.AI.NavMesh.SamplePosition(p,out q,2.5f,UnityEngine.AI.NavMesh.AllAreas)) return n+":SIN_MALLA "; var path=new UnityEngine.AI.NavMeshPath(); UnityEngine.AI.NavMesh.CalculatePath(start,q.position,UnityEngine.AI.NavMesh.AllAreas,path); float L=0; for(int i=1;i<path.corners.Length;i++) L+=UnityEngine.Vector3.Distance(path.corners[i-1],path.corners[i]); return n+":"+(path.status==UnityEngine.AI.NavMeshPathStatus.PathComplete?"OK":"PARCIAL")+"/"+L.ToString("F0")+"m "; };
foreach(var t in UnityEngine.Object.FindObjectsByType<UnityEngine.Transform>(UnityEngine.FindObjectsSortMode.None)){
 string n=t.name; var r=t.GetComponent<UnityEngine.MeshRenderer>();
 if(r!=null && (n.StartsWith("Nido")||n.StartsWith("Torre_")||n.StartsWith("Terraza_3")||n.StartsWith("Azotea")||n.StartsWith("Puente_Losa")||n.StartsWith("Objetivo_")||n=="Torre_Vigia")&&!n.StartsWith("Torreta"))
  sb.Append(ir(new UnityEngine.Vector3(r.bounds.center.x,r.bounds.max.y,r.bounds.center.z),n));
}
foreach(var s in UnityEngine.Object.FindObjectsByType<SP.Actors.Soldier>(UnityEngine.FindObjectsSortMode.None)) if(s.Team==SP.Combat.TeamId.Enemy && s.gameObject.activeInHierarchy) sb.Append(ir(s.transform.position,s.name.Replace("Enemigo_","E_")));
return sb.ToString();
