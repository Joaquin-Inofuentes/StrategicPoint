var m = UnityEngine.Object.FindAnyObjectByType<SP.Presentation.AttackLineManager>();
var lines = (System.Collections.IDictionary)typeof(SP.Presentation.AttackLineManager).GetField("lines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(m);
int huerfanas = 0, deMuertos = 0, total = 0;
foreach (System.Collections.DictionaryEntry kv in lines) { total++; int id = (int)kv.Key; SP.Actors.Soldier dueno = null; foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Id == id) dueno = s; if (dueno == null) huerfanas++; else if (!dueno.Health.IsAlive) deMuertos++; }
return "lineas rojas dibujadas=" + total + " | de soldados muertos=" + deMuertos + " | de soldados que ya no existen=" + huerfanas;
