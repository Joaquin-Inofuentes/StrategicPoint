var ci = System.Globalization.CultureInfo.InvariantCulture; var g = UnityEngine.GameObject.Find("ObstaculosDeLaRuta");
if (g == null) return "obstaculos estaticos de la ruta de huida=0 (no existe el grupo)";
var p = g.transform.GetChild(g.transform.childCount / 2).position;
System.IO.File.WriteAllText("C:/Users/PC_JOACO/AppData/Local/Temp/e4/obs.txt", p.x.ToString("0.0", ci) + " " + p.y.ToString("0.0", ci) + " " + p.z.ToString("0.0", ci));
return "obstaculos estaticos de la ruta de huida=" + g.transform.childCount + " (ejemplo en " + p + ")";
