var d = SP.Player.PlayerInputDriver.Activo; var me = d.Brain.Current.transform.position;
var go = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube); go.name = "ZzMuro";
go.transform.position = me + new UnityEngine.Vector3(0, 2f, 14f); go.transform.localScale = new UnityEngine.Vector3(7f, 4f, 0.8f);
var c0 = go.transform.position; var origen = c0 + new UnityEngine.Vector3(0, 0, -3f);
UnityEngine.Time.timeScale = 0.3f;
int n = SP.Presentation.Fragmentador.Romper(go.transform, origen, 9f);
UnityEngine.Object.Destroy(go);
int adelante = 0, atras = 0; float vA = 0, vT = 0;
foreach (var f in SP.Presentation.Fragmentador.Piezas) { if (f == null || !f.EnUso) continue; float vz = f.Cuerpo.linearVelocity.z; if (vz < 0) { adelante++; vA += -vz; } else { atras++; vT += vz; } }
var ci = System.Globalization.CultureInfo.InvariantCulture;
return "trozos=" + n + " hacia el lado del golpe=" + adelante + " (vel media " + (adelante > 0 ? vA / adelante : 0).ToString("0.0", ci) + " m/s) hacia el lado opuesto=" + atras + " (vel media " + (atras > 0 ? vT / atras : 0).ToString("0.0", ci) + " m/s) cx=" + c0.x.ToString("0.0", ci) + " cy=" + c0.y.ToString("0.0", ci) + " cz=" + c0.z.ToString("0.0", ci);
