int id = UnityEditor.SessionState.GetInt("zz_ali", -1);
foreach (var s in UnityEngine.Object.FindObjectsByType<SP.Actors.Soldier>(UnityEngine.FindObjectsSortMode.None)) if (s.Id == id) { s.Health.TakeDamage(99999, 0); UnityEngine.Object.Destroy(s.gameObject); }
return "enemigo muerto y retirado del mundo";
