// WARP: nombre_soldado|objeto  (separados por ; )
var res=new System.Text.StringBuilder();
foreach(var par in "SPEC".Split(';')){ var a=par.Split('|'); var go=UnityEngine.GameObject.Find(a[1]); if(go==null){ res.Append("noObj:"+a[1]+" "); continue; }
 foreach(var s in SP.Core.ActorRegistry.All){ if(s!=null && s.name.Contains(a[0])){ var ag=s.GetComponent<UnityEngine.AI.NavMeshAgent>(); var p=go.transform.position; if(ag!=null&&ag.enabled) ag.Warp(p); else s.transform.position=p; res.Append(a[0]+"->"+a[1]+" "); } } }
return res.ToString();
