var d = SP.Player.PlayerInputDriver.Activo; int i = UnityEditor.SessionState.GetInt("zz_paso", 0); UnityEditor.SessionState.SetInt("zz_paso", i + 1);
float a = i * 0.35f; var p = new UnityEngine.Vector3(18f * UnityEngine.Mathf.Sin(a), 0.8f, -48f - 18f + 18f * UnityEngine.Mathf.Cos(a));
d.Brain.Current.transform.position = p; return "ok";
