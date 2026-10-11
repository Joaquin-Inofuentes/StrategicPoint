var pool = UnityEngine.Object.FindAnyObjectByType<SP.Combat.ProjectilePool>();
var from = new UnityEngine.Vector3(676f, 8f, 700f);
var dir = (new UnityEngine.Vector3(689f, 0f, 697f) - from).normalized;
pool.Spawn(from, dir, -3, SP.Combat.TeamId.Enemy, 60, null, 4f, 0f);
return "obus";
