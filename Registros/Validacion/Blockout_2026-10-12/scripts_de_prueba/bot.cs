var P=SP.Combat.TeamId.Player; var sb=new System.Text.StringBuilder();
var objGo=UnityEngine.GameObject.Find("OBJ"); 
var all=SP.Core.ActorRegistry.All; SP.Actors.Soldier kes=null; var aliados=new System.Collections.Generic.List<SP.Actors.Soldier>(); int en=0,enAlive=0;
foreach(var s in all){ if(s==null) continue; var h=s.GetComponent<SP.Combat.Health>(); bool vivo=h!=null&&h.IsAlive;
 if(s.Team==SP.Combat.TeamId.Enemy){ en++; if(vivo) enAlive++; continue; }
 if(s.Role==SP.Combat.RoleType.Civilian) continue; if(s.name.Contains("Kes")) kes=s; if(vivo) aliados.Add(s); }
var pb=UnityEngine.Object.FindFirstObjectByType<SP.Player.PlayerBrain>();
if(kes!=null && pb.Current!=kes && kes.GetComponent<SP.Combat.Health>().IsAlive) pb.Possess(kes);
if(objGo!=null){ var tgt=objGo.transform.position;
 foreach(var s in aliados){ if(s==pb.Current) continue; var path=new UnityEngine.AI.NavMeshPath(); UnityEngine.AI.NavMesh.CalculatePath(s.transform.position,tgt,UnityEngine.AI.NavMesh.AllAreas,path); UnityEngine.Vector3 next=tgt; float acc=0; for(int i=1;i<path.corners.Length;i++){ acc+=UnityEngine.Vector3.Distance(path.corners[i-1],path.corners[i]); if(acc>=14f){ next=path.corners[i]; break; } }
  SP.Player.OrderService.IssueMoveOrder(s,next); } }
foreach(var s in aliados){ sb.Append(s.name.Replace("Soldado_","")+"="+s.GetComponent<SP.Combat.Health>().Current+"@"+s.transform.position.x.ToString("F0")+","+s.transform.position.z.ToString("F0")+" "); }
var obj=UnityEngine.Object.FindFirstObjectByType<SP.Operacion.BlockoutObjetivo>();
return "t="+UnityEngine.Time.timeSinceLevelLoad.ToString("F0")+" aliados="+aliados.Count+" enemigos="+enAlive+"/"+en+" ts="+UnityEngine.Time.timeScale+" obj="+(obj!=null&&obj.Terminado)+" | "+sb;
