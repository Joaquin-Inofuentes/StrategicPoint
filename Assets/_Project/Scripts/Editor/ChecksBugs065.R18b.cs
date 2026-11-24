using System.Collections;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Core;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ronda 18, BUG #017 ("estan enterrados"): la guarnicion del Centro de datos (y sus refuerzos) nacian con el pivote a piso + 0,2 m en vez de piso + 0,8 m
    // (SoldierMotor.PivoteSobrePiso): medio cuerpo bajo el piso de la sala. El check mide el hundimiento de cada uno y deja una captura de cerca de los pies.
    public static partial class ChecksBugs065
    {
        const string R18bPrefijo = "Assets/Validacion~/r18_enterrados_";

        // Distancia que le falta al pivote para apoyarse bien: positivo = hundido. Ignora el propio cuerpo y a otros soldados.
        static float R18bHundimiento(Soldier s)
        {
            var desde = s.transform.position + Vector3.up * 3f;
            var hits = Physics.RaycastAll(desde, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
            float piso = float.NegativeInfinity;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(s.transform) || h.collider.GetComponentInParent<Soldier>() != null) continue;
                if (h.point.y > piso && h.point.y <= s.transform.position.y + 0.9f) piso = h.point.y;
            }
            if (float.IsNegativeInfinity(piso)) return 0f;
            return piso + SoldierMotor.PivoteSobrePiso - s.transform.position.y;
        }

        static void R18bFoto(string archivo, Vector3 foco, Vector3 haciaLaCamara)
        {
            var go = new GameObject("R18bCamara"); var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 55f; cam.clearFlags = CameraClearFlags.Skybox; cam.nearClipPlane = 0.05f;
            // se busca un angulo con la linea de vista libre (la guarnicion esta entre filas de gabinetes)
            Vector3 mejor = foco + haciaLaCamara.normalized * 3.4f + Vector3.up * 1.0f; float mejorLibre = -1f;
            foreach (float dist in new[] { 3.4f, 2.8f, 2.2f })
                for (int k = 0; k < 12; k++)
                {
                    var dir = Quaternion.Euler(0f, k * 30f, 0f) * Vector3.forward;
                    var pos = foco + dir * dist + Vector3.up * 1.0f; var objetivo = foco + Vector3.up * 0.6f;
                    float libre = Physics.Raycast(objetivo, (pos - objetivo).normalized, out var imp, dist + 0.2f, ~0, QueryTriggerInteraction.Ignore) ? imp.distance : dist + 0.2f;
                    if (libre > mejorLibre + 0.01f) { mejorLibre = libre; mejor = pos; }
                }
            go.transform.position = mejor;
            go.transform.LookAt(foco + Vector3.up * 0.5f);
            var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(archivo, tex.EncodeToPNG());
            Object.DestroyImmediate(tex); cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
        }

        // 993a: la guarnicion del Centro de datos nace apoyada en el piso (no hundida)
        static IEnumerator Bug993a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionDirector.ProbarBloqueoDelCentro = true;
            OperacionPrueba.Arrancar(3, 1);
            var d = OperacionDirector.Instancia;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 15f && (d.GuarnicionDelCentro == null || d.GuarnicionDelCentro.Count == 0)) yield return null;
            if (d.GuarnicionDelCentro == null || d.GuarnicionDelCentro.Count == 0) { OperacionDirector.ProbarBloqueoDelCentro = false; Fin("FALLO no se armo la guarnicion"); yield break; }
            Physics.SyncTransforms();
            // Se congela a la guarnicion (sin IA) para medir y fotografiar donde NACIO.
            foreach (var s in d.GuarnicionDelCentro) if (s != null && s.Brain != null) s.Brain.Pasivo = true;
            float peor = 0f; int n = 0; string quien = "";
            foreach (var s in d.GuarnicionDelCentro)
            {
                if (s == null) continue;
                n++; float h = R18bHundimiento(s);
                if (h > peor) { peor = h; quien = s.name; }
            }
            // y los refuerzos (clones inactivos): su posicion es la de nacimiento
            int nRef = 0; float peorRef = 0f;
            foreach (var s in Object.FindObjectsByType<Soldier>(FindObjectsInactive.Include))
            {
                if (s == null || !s.name.StartsWith("Reserva_CD_Refuerzo_")) continue;
                nRef++; float h = R18bHundimiento(s); if (h > peorRef) peorRef = h;
            }
            var primero = d.GuarnicionDelCentro[0];
            R18bFoto(R18bPrefijo + UnityEditor.SessionState.GetString("R18bEtiqueta", "foto") + ".png", primero.transform.position, new Vector3(0.6f, 0f, -0.8f));
            W8Ok(ref ok, n >= 4 && peor < 0.15f, sb, $"{n} de la guarnicion; mayor hundimiento {peor:0.00} m ({quien}); tolerancia 0,15 m");
            W8Ok(ref ok, nRef > 0 && peorRef < 0.15f, sb, $"{nRef} refuerzos del centro; mayor hundimiento {peorRef:0.00} m");
            OperacionDirector.ProbarBloqueoDelCentro = false;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
