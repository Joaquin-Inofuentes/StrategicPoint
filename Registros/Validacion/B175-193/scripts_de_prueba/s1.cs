var d = SP.Player.PlayerInputDriver.Activo;
var o = SP.EditorTools.ZzArena.Origen;
var dir = SP.Operacion.OperacionDirector.Instancia;
dir.TeletransportarEscuadra(o + new UnityEngine.Vector3(-14,0,-4), UnityEngine.Quaternion.Euler(0,90,0));
d.Selection.Clear();
SP.Actors.Soldier a = null, b = null;
foreach (var s in SP.Core.ActorRegistry.All) { if (s == null || s.Team != SP.Combat.TeamId.Player || !s.gameObject.activeInHierarchy) continue; if (a == null) a = s; else if (b == null) b = s; }
d.Selection.SelectSingle(a); d.Selection.AddToSelection(b);
for (int i=0;i<4;i++) d.Rig.ZoomHaciaCursor(1.5f, new UnityEngine.Vector2(960,540));
d.Rig.CentrarRtsEn(o + new UnityEngine.Vector3(-4,0,-2), 12f);
var seto = UnityEngine.GameObject.Find("Seto_1");
return "sel=" + d.Selection.Selected.Count + " cam=" + UnityEngine.Camera.main.transform.position;
