SP.Operacion.FrancotiradorEnTorre ft = null;
foreach (var f in UnityEngine.Object.FindObjectsByType<SP.Operacion.FrancotiradorEnTorre>(UnityEngine.FindObjectsInactive.Include)) { ft = f; break; }
return ft == null ? "sin francotirador" : "laser del francotirador muerto sigue encendido=" + ft.LaserActivo;
