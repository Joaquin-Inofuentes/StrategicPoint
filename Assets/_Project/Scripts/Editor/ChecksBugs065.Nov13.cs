using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.Presentation;
using G = SP.Operacion.MansionGeometria;

namespace SP.EditorTools
{
    // Revision del 2026-11-13 de la lista de pendientes de la tanda del 07/11: lo que no tenia medicion propia (snap al disparar el canon,
    // enemigos trabados al aparecer, escaleras de la mansion). Fotos en Assets/Validacion~/v11_*.
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- 930a el canon del tanque no pega saltos de punteria al disparar
        static IEnumerator Bug930a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            foreach (var x in PrepararTanque()) yield return x;
            var d = OperacionDirector.Instancia;
            W8Ok(ref ok, embarqueOk, sb, "el jugador quedo de artillero");
            var canon = d != null && d.tanque != null ? d.tanque.TorretaCanon : null;
            var cam = Camera.main;
            if (canon == null || cam == null) { Fin("FALLO sin canon o camara " + sb); yield break; }
            float yawPrev = canon.DesiredYaw, pitchPrev = canon.DesiredPitch;
            Quaternion camPrev = cam.transform.rotation;
            float maxFuego = 0f, maxBase = 0f, maxCam = 0f; int disparos = 0; int hasta = -1;
            for (int f = 0; f < 240; f++)
            {
                if (f % 50 == 20 && canon.TryFire()) { disparos++; hasta = f + 4; }
                yield return null;
                float dy = Mathf.Abs(Mathf.DeltaAngle(yawPrev, canon.DesiredYaw)), dp = Mathf.Abs(Mathf.DeltaAngle(pitchPrev, canon.DesiredPitch));
                float dc = Quaternion.Angle(camPrev, cam.transform.rotation);
                float paso = Mathf.Max(dy, dp);
                if (f <= hasta && f >= hasta - 4) { maxFuego = Mathf.Max(maxFuego, paso); maxCam = Mathf.Max(maxCam, dc); } else maxBase = Mathf.Max(maxBase, paso);
                yawPrev = canon.DesiredYaw; pitchPrev = canon.DesiredPitch; camPrev = cam.transform.rotation;
                if (f == 22) foreach (var x in FotoPrincipal("v11_snap_tanque.jpg")) yield return x;
            }
            W8Ok(ref ok, disparos >= 3, sb, $"el canon disparo {disparos} veces");
            W8Ok(ref ok, maxFuego <= Mathf.Max(2f, maxBase * 2f), sb, $"salto maximo de punteria en los 5 cuadros de cada disparo: {maxFuego:0.00} grados (fuera del disparo {maxBase:0.00})");
            W8Ok(ref ok, maxCam <= 2f, sb, $"la camara no salta al disparar ({maxCam:0.00} grados por cuadro)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 893a mansion: enemigos que aparecen libres (sin quedar dentro de geometria y con camino al jugador) y escaleras utilizables
        static IEnumerator Bug893a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var sal = new OperacionDirector[1];
            foreach (var x in ArrancarMansion(sal)) yield return x;
            var d = sal[0];
            // los enemigos de cada piso esperan a que el jugador se acerque o venza su plazo: se adelanta el reloj de la mansion para que aparezcan todos
            typeof(OperacionDirector).GetField("relojMansion", NoPublico).SetValue(d, 100000f);
            foreach (var x in Esperar(10f)) yield return x;
            var yo = Poseido(); var objetivo = yo != null ? yo.transform.position : Vector3.zero;
            int total = 0, dentro = 0, sinCamino = 0, sinMalla = 0; var det = new StringBuilder();
            Soldier muestra = null;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                total++; if (muestra == null) muestra = s;
                var p = s.transform.position;
                var cols = Physics.OverlapCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.4f, 0.28f, ~0, QueryTriggerInteraction.Ignore);
                foreach (var c in cols)
                {
                    if (c == null || c.transform.IsChildOf(s.transform) || c.attachedRigidbody != null || c.GetComponentInParent<Soldier>() != null || c.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                    dentro++; det.Append($" [{s.name} dentro de {c.name}]"); break;
                }
                if (!UnityEngine.AI.NavMesh.SamplePosition(p, out _, 0.8f, UnityEngine.AI.NavMesh.AllAreas)) { sinMalla++; det.Append($" [{s.name} sin malla@{p:0}]"); continue; }
                var camino = new UnityEngine.AI.NavMeshPath();
                bool hay = UnityEngine.AI.NavMesh.CalculatePath(p, objetivo, UnityEngine.AI.NavMesh.AllAreas, camino);
                if (!hay || camino.status != UnityEngine.AI.NavMeshPathStatus.PathComplete) { sinCamino++; det.Append($" [{s.name} sin camino ({camino.status})]"); }
            }
            W8Ok(ref ok, total > 0, sb, $"hay enemigos activos en la mansion ({total})");
            W8Ok(ref ok, dentro == 0, sb, $"ninguno aparece dentro de geometria ({dentro}){det}");
            W8Ok(ref ok, sinMalla == 0, sb, $"todos aparecen sobre la malla de navegacion ({sinMalla} fuera)");
            W8Ok(ref ok, sinCamino == 0, sb, $"todos tienen camino completo hasta el jugador ({total - sinCamino}/{total})");
            // se mueven: los que persiguen se desplazan en 12 s (un enemigo que sigue clavado en su punto con el jugador a la vista esta trabado)
            var inicio = new Dictionary<Soldier, Vector3>();
            foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Enemy && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy && s.Brain != null) inicio[s] = s.transform.position;
            foreach (var x in Esperar(12f)) yield return x;
            int movidos = 0, quietos = 0;
            foreach (var kv in inicio) { if (kv.Key == null || kv.Key.Health == null || !kv.Key.Health.IsAlive) { movidos++; continue; } if ((kv.Key.transform.position - kv.Value).sqrMagnitude > 0.25f * 0.25f) movidos++; else quietos++; }
            sb.Append($"[de {inicio.Count} enemigos con cerebro, {movidos} se movieron o murieron en 12 s y {quietos} siguen en su puesto] ");
            if (muestra != null)
            {
                var p = muestra.transform.position + Vector3.up * 1f; Vector3 cp = p + new Vector3(3.5f, 1.2f, -5.5f);
                foreach (var o in new[] { new Vector3(0f, 1.2f, -5f), new Vector3(0f, 1.2f, 5f), new Vector3(5f, 1.2f, 0f), new Vector3(-5f, 1.2f, 0f), new Vector3(3.5f, 1.2f, -3.5f), new Vector3(-3.5f, 1.2f, 3.5f), new Vector3(0f, 1.6f, -3f) })
                    if (!Physics.Linecast(p + o, p, ~0, QueryTriggerInteraction.Ignore)) { cp = p + o; break; }
                foreach (var x in FotoCamara("v11_spawn_enemigos.jpg", cp, p, 55f, 1280, 720)) yield return x;
            }

            // escaleras: cada torre se recorre de la entrada a la salida por la malla de navegacion, y sube un piso entero
            int buenas = 0; var detE = new StringBuilder();
            for (int t = 0; t < G.Torres.Length; t++)
            {
                var ent = G.EntradaDeTorre(t); var sal2 = G.SalidaDeTorre(t);
                var cam = new UnityEngine.AI.NavMeshPath();
                bool a0 = UnityEngine.AI.NavMesh.SamplePosition(ent, out var h1, 1.5f, UnityEngine.AI.NavMesh.AllAreas);
                bool a1 = UnityEngine.AI.NavMesh.SamplePosition(sal2, out var h2, 1.5f, UnityEngine.AI.NavMesh.AllAreas);
                bool completo = a0 && a1 && UnityEngine.AI.NavMesh.CalculatePath(h1.position, h2.position, UnityEngine.AI.NavMesh.AllAreas, cam) && cam.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
                bool sube = sal2.y - ent.y > 3f;
                if (completo && sube) buenas++; else detE.Append($" [torre {G.Torres[t].nombre}: malla={a0 && a1} camino={completo} sube={sube}]");
            }
            W8Ok(ref ok, buenas == G.Torres.Length, sb, $"escaleras de la mansion recorribles de la entrada a la salida {buenas}/{G.Torres.Length}{detE}; enemigos que bajaron por escalera: {d.EnemigosBajadosPorEscalera}");
            foreach (var x in FotoCamara("v11_escalera.jpg", new Vector3(286f, 8f, 248f), new Vector3(294f, 4f, 254f), 65f, 1280, 720)) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
