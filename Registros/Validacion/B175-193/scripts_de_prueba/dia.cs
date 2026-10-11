foreach (var l in UnityEngine.Object.FindObjectsByType<UnityEngine.Light>(UnityEngine.FindObjectsSortMode.None)) if (l.name == "ZzSol") { l.intensity = 2.0f; }
UnityEngine.RenderSettings.ambientLight = new UnityEngine.Color(0.55f,0.58f,0.62f);
var g = UnityEngine.GameObject.Find("Enemigo_noche"); if (g != null) UnityEngine.Object.Destroy(g);
foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player && s.Brain != null) s.Brain.Pasivo = false;
return "dia";
