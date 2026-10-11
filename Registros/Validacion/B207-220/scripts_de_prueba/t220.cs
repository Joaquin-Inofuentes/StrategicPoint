var d = SP.Player.PlayerInputDriver.Activo; var cam = SP.Core.CamaraPrincipal.Actual; var ci = System.Globalization.CultureInfo.InvariantCulture;
var t = System.Type.GetType("SP.Presentation.TomaDeVictoria, Assembly-CSharp");
if (t != null) t.GetMethod("Iniciar").Invoke(null, null);
return (t != null ? "toma de victoria activa" : "CODIGO VIEJO: camara sin toma de cierre") + " | modo=" + d.Rig.Mode + " camPos=" + cam.transform.position.ToString("0.0");
