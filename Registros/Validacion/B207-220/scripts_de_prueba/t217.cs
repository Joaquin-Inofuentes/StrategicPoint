var ci = System.Globalization.CultureInfo.InvariantCulture; var dir = SP.Operacion.OperacionDirector.Instancia; var tq = dir.tanque;
typeof(SP.Operacion.OperacionDirector).GetMethod("PotenciarCanon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(dir, null);
var t = tq.TorretaCanon;
var p = t.transform.position + new UnityEngine.Vector3(0f, 22f, 30f); t.ElevarHacia(p);
return "limite de elevacion del canon=" + (-t.MinPitch).ToString("0", ci) + " grados | helicoptero a 22 m de alto y 30 m pide " + (-t.DesiredPitch).ToString("0.0", ci) + " grados | llega a apuntarle=" + (-t.DesiredPitch > (-t.MinPitch) - 0.01f ? "NO (tope)" : "SI");
