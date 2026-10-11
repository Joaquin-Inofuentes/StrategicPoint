var m = UnityEngine.GameObject.Find("Seto_N").GetComponent<SP.Presentation.ObstacleMarker>();
m.TakeDamage(99999, new UnityEngine.Vector3(710f, 1f, 700f));
return "colapso=" + m.IsCollapsed;
