var dir = SP.Operacion.OperacionDirector.Instancia;
int tf = 0, dec = 0, sacos = 0;
foreach (var t in UnityEngine.Object.FindObjectsByType<SP.Vehicles.TorretaFija>(UnityEngine.FindObjectsSortMode.None)) { tf++; var e = t.GetComponent<SP.Operacion.EmplazamientoDeTorreta>(); if (e != null) { dec++; sacos += e.SacosCreados; } }
int avisos = UnityEngine.Object.FindObjectsByType<SP.UI.AvisoUnico>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None).Length;
int vivos = 0; foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Enemy && s.gameObject.activeInHierarchy && s.Health != null && s.Health.IsAlive) vivos++;
int torretas = 0;
foreach (var t in SP.Operacion.TorreDestruible.Todas) if (t != null && t.tipo == SP.Operacion.TorreDestruible.Tipo.Torreta && t.Marca != null) { torretas++; t.Marca.Demoler(t.transform.position + UnityEngine.Vector3.forward * 3f); }
return "#171 torretas fijas=" + tf + " con emplazamiento=" + dec + " sacos=" + sacos + " | #173 componentes AvisoUnico=" + avisos + " | #172 autosSimultaneos=" + dir.autosSimultaneos + " | #169 fase=" + dir.Fase + " enemigos vivos=" + vivos + " torretas de artilleria derribadas ahora=" + torretas;
