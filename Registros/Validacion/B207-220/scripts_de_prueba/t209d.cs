var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance; int n = 0; SP.Operacion.FrancotiradorEnTorre ft = null;
foreach (var f in UnityEngine.Object.FindObjectsByType<SP.Operacion.FrancotiradorEnTorre>(UnityEngine.FindObjectsInactive.Include)) { n++; if (ft == null) ft = f; }
if (ft == null) return "no hay francotiradores de torre en este tramo (n=0)";
var sol = (SP.Actors.Soldier)typeof(SP.Operacion.TiradorDeTorre).GetField("soldado", bf).GetValue(ft);
typeof(SP.Operacion.FrancotiradorEnTorre).GetMethod("PonerLaser", bf).Invoke(ft, new object[] { true });
string nombre = sol != null ? sol.name : "?";
if (sol != null) { sol.Health.TakeDamage(99999, 0); UnityEngine.Object.Destroy(sol.gameObject); }
return "francotirador " + nombre + " (de " + n + ") con el laser encendido=" + ft.LaserActivo + "; muerto y retirado del mundo";
