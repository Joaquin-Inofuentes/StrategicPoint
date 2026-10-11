var c = (int[])System.AppDomain.CurrentDomain.GetData("zz193c");
var d = SP.Operacion.OperacionDirector.Instancia; int vivos = 0, aliados = 0; foreach (var s in SP.Core.ActorRegistry.All) { if (s == null || !s.gameObject.activeInHierarchy || !s.Health.IsAlive) continue; if (s.Team == SP.Combat.TeamId.Enemy) vivos++; else if (s.Team == SP.Combat.TeamId.Player) aliados++; }
return "fase=" + d.Fase + " disparosEnemigos=" + c[0] + " impactosEnEntorno=" + c[1] + " (contra paredes/altos=" + c[2] + ", en piso=" + c[3] + ") enemigosVivos=" + vivos + " aliadosVivos=" + aliados;
