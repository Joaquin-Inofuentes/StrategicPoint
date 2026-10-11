var d = SP.Player.PlayerInputDriver.Activo;
if (d.Rig.Mode != SP.CameraSystem.ControlMode.Rts) d.AlternarVista();
d.Selection.Clear(); int n = 0; var sel = new System.Collections.Generic.List<SP.Actors.Soldier>();
foreach (var s in SP.Core.ActorRegistry.All) { if (s == null || s.Team != SP.Combat.TeamId.Player || !s.gameObject.activeInHierarchy || s == d.Brain.Current) continue; if (n++ == 0) d.Selection.SelectSingle(s); else d.Selection.AddToSelection(s); }
string antes = "RTS sel=" + d.Selection.Selected.Count;
d.AlternarVista();
return antes + " -> modo=" + d.Rig.Mode + " sel=" + d.Selection.Selected.Count;
