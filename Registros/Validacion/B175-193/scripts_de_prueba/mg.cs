var pool = UnityEngine.Object.FindAnyObjectByType<SP.Combat.ProjectilePool>();
float t0 = UnityEngine.Time.realtimeSinceStartup; float last = 0f; int n = 0;
UnityEditor.EditorApplication.CallbackFunction f = null;
f = () => {
  float t = UnityEngine.Time.realtimeSinceStartup - t0;
  if (t > 4f) { UnityEditor.EditorApplication.update -= f; return; }
  if (t - last < 0.06f) return; last = t; n++;
  var from = new UnityEngine.Vector3(676f, 6f, 700f + UnityEngine.Mathf.Sin(t * 3f) * 2f);
  var dir = (new UnityEngine.Vector3(689f, 0f, 696f + UnityEngine.Mathf.Sin(t * 5f) * 3f) - from).normalized;
  pool.Spawn(from, dir, -3, SP.Combat.TeamId.Enemy, 5, null);
};
UnityEditor.EditorApplication.update += f;
return "rafaga de MG enemiga 4 s";
