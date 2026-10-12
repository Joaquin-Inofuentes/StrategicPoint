var sb = new System.Text.StringBuilder();
foreach (var t in SP.Operacion.TorreDestruible.Todas) if (t != null) sb.Append(t.name + "[" + t.tipo + " caida=" + t.EstaCaida + " cayendo=" + t.Cayendo + " vida=" + t.Vida + "/" + t.VidaMaxima + " activa=" + t.gameObject.activeInHierarchy + "] ");
sb.Append(" fase=" + SP.Operacion.OperacionDirector.Instancia.Fase);
return sb.ToString();
