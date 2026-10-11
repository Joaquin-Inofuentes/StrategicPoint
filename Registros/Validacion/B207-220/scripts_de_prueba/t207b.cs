var ci = System.Globalization.CultureInfo.InvariantCulture; SP.Actors.Soldier sol = null;
foreach (var s in UnityEngine.Object.FindObjectsByType<SP.Actors.Soldier>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)) if (s.name == "Reserva_CD1_3") sol = s;
return "3 s despues: y=" + sol.transform.position.y.ToString("0.00", ci) + " estado=" + sol.Brain.State;
