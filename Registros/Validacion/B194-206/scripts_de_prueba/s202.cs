var dir = SP.Operacion.OperacionDirector.Instancia;
return "fase=" + dir.Fase + " reloj=" + dir.Reloj.ToString("0") + " cohetes lanzados=" + SP.Operacion.CoheteDeAviso.Lanzados + " en vuelo=" + SP.Operacion.CoheteDeAviso.Vivos.Count + " anillos de aviso=" + SP.Operacion.AnilloDeAviso.Activos;
