var e = UnityEngine.GameObject.Find("Enemigo_noche").GetComponent<SP.Actors.Soldier>();
SP.Actors.Soldier a = null; foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player && s.gameObject.activeInHierarchy) { a = s; break; }
e.Health.TakeDamage(99999, a.Id);
return "vivo=" + e.Health.IsAlive;
