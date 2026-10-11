var dir = SP.Operacion.OperacionDirector.Instancia; int n = 0; string r = "";
foreach (var e in UnityEngine.Object.FindObjectsByType<SP.Operacion.EmboscadaDeCohetes>(UnityEngine.FindObjectsSortMode.None)) {
  n++; if (e.tiradores == null || e.tiradores.Length == 0) continue; var p = e.tiradores[0].transform.position;
  if (n == 1) { dir.tanque.transform.position = p + new UnityEngine.Vector3(0, 0, -40f); r += "tanque a 40 m de la emboscada 1 en " + p + "; armada=" + e.Armada + " "; if (!e.Armada) e.Armar(dir.tanque); r += "armada=" + e.Armada; } }
return "emboscadas=" + n + " " + r;
