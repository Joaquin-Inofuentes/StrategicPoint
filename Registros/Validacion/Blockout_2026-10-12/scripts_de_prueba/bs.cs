var s=UnityEngine.Object.FindObjectsByType<SP.Actors.Soldier>(UnityEngine.FindObjectsSortMode.None);
int p=0,e=0,sinNav=0,vivos=0; foreach(var x in s){ if(x.Team==SP.Combat.TeamId.Player)p++; else e++; var a=x.GetComponent<UnityEngine.AI.NavMeshAgent>(); if(a!=null&&a.enabled&&!a.isOnNavMesh) sinNav++; }
var tri=UnityEngine.AI.NavMesh.CalculateTriangulation();
var tf=UnityEngine.Object.FindObjectsByType<SP.Vehicles.TorretaFija>(UnityEngine.FindObjectsSortMode.None).Length;
return "escena="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name+" cams="+UnityEngine.Camera.allCamerasCount+" aliados="+p+" enemigos="+e+" sinNavMesh="+sinNav+" triNav="+(tri.indices.Length/3)+" torretas="+tf+" fps="+(1f/UnityEngine.Time.smoothDeltaTime).ToString("F0");
