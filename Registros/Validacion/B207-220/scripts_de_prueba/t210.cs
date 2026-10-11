string r = ""; foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Player && s.gameObject.activeInHierarchy) r += s.name + "=" + SP.Actors.SoldierClasses.NombreDe(s) + "(" + s.Role + ") ";
bool mismoIcono = SP.Presentation.RoleIconFactory.For(SP.Combat.RoleType.Flanker) == SP.Presentation.RoleIconFactory.For(SP.Combat.RoleType.Sniper);
return r + "| icono del francotirador de la escuadra es la mira=" + mismoIcono + " | medico cura solo (AtencionAutomatica)=" + SP.Player.PedidoDeCuracion.AtencionAutomatica;
