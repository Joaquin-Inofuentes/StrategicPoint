var d = SP.Player.PlayerInputDriver.Activo; SP.Actors.Soldier e = null;
foreach (var s in SP.Core.ActorRegistry.All) { if (s != null && s.Team == SP.Combat.TeamId.Enemy && s.Health != null && s.Health.IsAlive) { e = s; break; } }
var a = d.Brain.Current; int n = 0;
foreach (var pair in new[]{ new System.Tuple<SP.Actors.Soldier, float>(e, 0.9f), new System.Tuple<SP.Actors.Soldier, float>(a, -0.9f) }) {
  var s = pair.Item1; foreach (var smr in s.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true)) {
    var m = new UnityEngine.Mesh(); smr.BakeMesh(m);
    var go = new UnityEngine.GameObject("ZzClon"); go.AddComponent<UnityEngine.MeshFilter>().sharedMesh = m; go.AddComponent<UnityEngine.MeshRenderer>().sharedMaterial = smr.sharedMaterial;
    var rel = smr.transform.position - s.transform.position;
    go.transform.position = new UnityEngine.Vector3(pair.Item2, 500f, 0f) + UnityEngine.Quaternion.Inverse(s.transform.rotation) * rel;
    go.transform.rotation = UnityEngine.Quaternion.Inverse(s.transform.rotation) * smr.transform.rotation * UnityEngine.Quaternion.Euler(0, 200, 0);
    go.transform.localScale = smr.transform.lossyScale; n++; } }
var l = new UnityEngine.GameObject("ZzLuz2").AddComponent<UnityEngine.Light>(); l.type = UnityEngine.LightType.Directional; l.intensity = 0.9f; l.transform.rotation = UnityEngine.Quaternion.Euler(35, 160, 0);
return "piezas=" + n + " enemigo=" + e.DisplayName + " aliado=" + a.DisplayName;
