var d = SP.Player.PlayerInputDriver.Activo; var herido = (SP.Actors.Soldier)System.AppDomain.CurrentDomain.GetData("zzHerido");
string r = "cursor=" + SP.Presentation.CursorContextual.Actual;
var mi = d.GetType().GetMethod("EnviarACurarDesdeRts");
if (mi == null) return r + " | clic derecho sobre el herido: NO hay orden de curar (el codigo viejo lo trata como mover)";
bool ok = (bool)mi.Invoke(d, new object[]{ herido });
return r + " | EnviarACurarDesdeRts=" + ok + " medico=" + (SP.Player.PedidoDeCuracion.Enfermero != null ? SP.Player.PedidoDeCuracion.Enfermero.DisplayName : "-");
