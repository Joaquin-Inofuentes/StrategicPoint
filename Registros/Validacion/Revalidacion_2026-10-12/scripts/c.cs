var sb = new System.Text.StringBuilder("#172 vida de vehiculos al empezar la huida: ");
foreach (var v in UnityEngine.Object.FindObjectsByType<SP.Vehicles.Vehicle>(UnityEngine.FindObjectsSortMode.None)) if (v.gameObject.activeInHierarchy && v.Health != null) sb.Append(v.name + "=" + (100f * v.Health.Current / v.Health.MaxHealth).ToString("0") + "% ");
sb.Append("| reparacion activa=" + (SP.Operacion.ReparacionDeTanque.Activa != null));
return sb.ToString();
