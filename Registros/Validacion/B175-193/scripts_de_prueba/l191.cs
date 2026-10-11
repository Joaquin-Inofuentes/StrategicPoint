var d = SP.Operacion.OperacionDirector.Instancia; var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var lst = (System.Collections.IList)d.GetType().GetField("oleadasMando", bf).GetValue(d);
string r = "oleadas=" + lst.Count + " ";
foreach (var o in lst) { var t = o.GetType(); int cam = (int)t.GetField("camionetas").GetValue(o); bool lanz = (bool)t.GetField("lanzada").GetValue(o); r += cam + "/" + lanz + " ";
  if (cam > 0 && !lanz) { d.GetType().GetMethod("LanzarOleada", bf).Invoke(d, new object[]{o}); r += "[LANZADA] "; break; } }
return r;
