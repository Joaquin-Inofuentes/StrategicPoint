var best = ""; 
foreach(var a in UnityEngine.Object.FindObjectsByType<SP.Operacion.OperacionAuto>(UnityEngine.FindObjectsSortMode.None)) {
  if (!a.name.StartsWith("Camioneta_Oleada")) continue;
  float dm = 9999f; foreach (var s in SP.Core.ActorRegistry.All) { if (s==null || s.Team!=SP.Combat.TeamId.Player || !s.gameObject.activeInHierarchy) continue; float dd = (s.transform.position - a.transform.position).magnitude; if (dd < dm) dm = dd; }
  best += a.name + " v=" + a.VelocidadActual.ToString("0.0") + " aSoldadoMasCercano=" + dm.ToString("0.0") + " pos=" + a.transform.position + " | ";
}
return SP.Operacion.OperacionDirector.Instancia.Fase + " " + best;
