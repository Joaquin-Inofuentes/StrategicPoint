var ci = System.Globalization.CultureInfo.InvariantCulture; SP.Actors.Soldier sol = null;
foreach (var s in UnityEngine.Object.FindObjectsByType<SP.Actors.Soldier>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)) if (s.name == "Reserva_CD1_3") sol = s;
sol.gameObject.SetActive(true); sol.transform.position = new UnityEngine.Vector3(0f, 0.8f, -80f); SP.Core.ApoyoEnElPiso.Apoyar(sol.transform);
UnityEngine.Time.timeScale = 1f; float y0 = sol.transform.position.y;
sol.Health.TakeDamage(99999, 0); sol.transform.position += UnityEngine.Vector3.up * 3f;
return "cuerpo caido: y_suelo=" + y0.ToString("0.00", ci) + " levantado a y=" + sol.transform.position.y.ToString("0.00", ci) + " vivo=" + sol.Health.IsAlive;
