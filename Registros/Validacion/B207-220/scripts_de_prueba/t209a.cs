var d = SP.Player.PlayerInputDriver.Activo; if (d.Rig.Mode != SP.CameraSystem.ControlMode.Rts) d.AlternarVista();
SP.Actors.Soldier e = null, aliado = null;
foreach (var s in UnityEngine.Object.FindObjectsByType<SP.Actors.Soldier>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)) { if (s.name == "Reserva_CD1_1") e = s; if (s.Team == SP.Combat.TeamId.Player && s != d.Brain.Current && aliado == null && s.gameObject.activeInHierarchy) aliado = s; }
e.gameObject.SetActive(true); e.transform.position = aliado.transform.position + new UnityEngine.Vector3(9f, 0f, 0f); SP.Core.ApoyoEnElPiso.Apoyar(e.transform);
SP.Core.InteligenciaDeEnemigos.Revelar(e.Id);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
typeof(SP.Ai.AiBrain).GetField("target", bf).SetValue(e.Brain, aliado);
typeof(SP.Ai.AiBrain).GetMethod("SetState", bf).Invoke(e.Brain, new object[] { SP.Ai.AiState.Chase });
UnityEditor.SessionState.SetInt("zz_ali", e.Id);
return "enemigo vivo " + e.name + " apuntando a " + aliado.name + " a 9 m";
