var c = new int[4]; // 0=disparos enemigos, 1=impactos enemigos en entorno no-vehiculo, 2=impactos enemigos contra pared/edificio (Kind distinto de Ground), 3=impactos en piso
var s1 = SP.Core.EventBus.Instance.Subscribe<SP.Core.ShotFiredEvent>(e => { var a = SP.Core.ActorRegistry.FindById(e.ShooterId); if (a != null && a.Team == SP.Combat.TeamId.Enemy) c[0]++; });
var s2 = SP.Core.EventBus.Instance.Subscribe<SP.Core.EnvironmentHitEvent>(e => { var a = SP.Core.ActorRegistry.FindById(e.ShooterId); if (a == null || a.Team != SP.Combat.TeamId.Enemy) return; if (e.Kind == SP.Core.EnvironmentHitKind.Vehicle) return; c[1]++; if (e.Point.y > 0.45f) c[2]++; else c[3]++; });
System.AppDomain.CurrentDomain.SetData("zz193c", c); System.AppDomain.CurrentDomain.SetData("zz193s", new System.IDisposable[]{s1, s2});
return "suscrito";
