foreach (var l in UnityEngine.Object.FindObjectsByType<UnityEngine.Light>(UnityEngine.FindObjectsSortMode.None)) if (l.name == "ZzSol") { l.intensity = 0.04f; }
UnityEngine.RenderSettings.ambientLight = new UnityEngine.Color(0.035f, 0.045f, 0.08f);
SP.EditorTools.ZzArena.Soldado(false, "Enemigo_noche", SP.Combat.RoleType.Enemy, new UnityEngine.Vector3(-8f, 0f, 0f), 90f);
var e = UnityEngine.GameObject.Find("Enemigo_noche"); e.GetComponent<SP.Ai.AiBrain>().Pasivo = true;
foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player && s.Brain != null) s.Brain.Pasivo = true;
return "ok";
