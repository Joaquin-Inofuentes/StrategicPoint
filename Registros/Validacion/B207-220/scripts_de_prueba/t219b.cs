var t = System.Type.GetType("SP.Presentation.TransparenciaDeOclusores, Assembly-CSharp");
if (t == null) return "CODIGO VIEJO: no existe transparencia de oclusores";
var inst = t.GetProperty("Instancia").GetValue(null); int n = (int)t.GetProperty("CantidadTransparentes").GetValue(inst);
return "objetos translucidos entre la camara y el seleccionado=" + n;
