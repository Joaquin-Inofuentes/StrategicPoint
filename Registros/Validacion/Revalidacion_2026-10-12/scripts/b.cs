var dir = SP.Operacion.OperacionDirector.Instancia;
int vivos = 0; foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Enemy && s.gameObject.activeInHierarchy && s.Health != null && s.Health.IsAlive) vivos++;
return "#169 4 s despues: fase=" + dir.Fase + " enemigos del cuartel aun vivos=" + vivos + " | aviso=" + SP.UI.AvisoUnico.TextoActual;
