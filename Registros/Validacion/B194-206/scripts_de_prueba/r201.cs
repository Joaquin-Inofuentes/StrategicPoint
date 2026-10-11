var c = (UnityEngine.Vector3)System.AppDomain.CurrentDomain.GetData("zz201"); int ad = 0, at = 0, tot = 0; float maxA = 0, maxT = 0;
foreach (var f in SP.Presentation.Fragmentador.Piezas) { if (f == null || !f.EnUso) continue; tot++; float z = f.transform.position.z - c.z; if (z < -0.45f) { ad++; if (-z > maxA) maxA = -z; } else if (z > 0.45f) { at++; if (z > maxT) maxT = z; } }
return "trozos=" + tot + " lado golpe=" + ad + " hasta " + maxA.ToString("0.0") + " m; lado opuesto=" + at + " hasta " + maxT.ToString("0.0") + " m";
