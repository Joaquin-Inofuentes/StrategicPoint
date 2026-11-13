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
using SP.Vehicles;

namespace SP.EditorTools
{
    // Tanda del 2026-11-13 (tanque, barricadas de la ruta, perros, fuente, azotea).
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- 960a los disparos enemigos le pegan al tanque del jugador (diagnostico + dano x4)
        static IEnumerator Bug960a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            foreach (var x in PrepararTanque()) yield return x;
            var d = OperacionDirector.Instancia;
            W8Ok(ref ok, embarqueOk, sb, "el jugador quedo de artillero");
            if (d == null || d.tanque == null) { Fin("FALLO sin tanque " + sb); yield break; }
            ModoDios.Poner(false);
            var tq = d.tanque;
            W8Ok(ref ok, OperacionDirector.MultiplicadorDeDanoDelTanque == 16f, sb, $"el dano que recibe el tanque es x16: el doble de antes del 13/11, la mitad de lo que se habia subido ({OperacionDirector.MultiplicadorDeDanoDelTanque})");
            // se frena al tanque para medir el fuego de los infantes sin que se aleje
            var motor = tq.GetComponent<VehicleMotor>();
            int golpes = 0, golpesDeBala = 0; int vida0 = tq.Health.Current;
            var sub = EventBus.Instance.Subscribe<VehicleDamagedEvent>(e => { if (e.Vehicle == tq) golpes++; });
            var enemigos = new List<Soldier>();
            Vector3 f = tq.transform.forward; f.y = 0f; f.Normalize(); Vector3 r = Vector3.Cross(Vector3.up, f);
            float[] dist = { 14f, 20f, 26f, 32f }; float[] lat = { -4f, 5f, -7f, 3f };
            for (int i = 0; i < dist.Length; i++)
            {
                var s = Wp7ColocarEnemigo(tq.transform.position + f * dist[i] + r * lat[i]);
                if (s == null) break;
                if (s.Brain != null) { s.Brain.Pasivo = false; s.Brain.Quieto = false; }
                s.transform.rotation = Quaternion.LookRotation(-f);
                enemigos.Add(s);
            }
            W8Ok(ref ok, enemigos.Count >= 3, sb, $"enemigos colocados frente al tanque ({enemigos.Count})");
            foreach (var x in Esperar(15f)) yield return x;
            sub.Dispose();
            int disp = 0, conAmenaza = 0; var det = new StringBuilder();
            foreach (var s in enemigos)
            {
                if (s == null || s.Brain == null) continue;
                disp += s.Brain.DisparosAVehiculos; if (s.Brain.VehiculoAmenaza != null) conAmenaza++;
                det.Append($" [{s.name} vivo={s.Health.IsAlive} estado={s.Brain.State} amenaza={(s.Brain.VehiculoAmenaza != null)} disparosAVeh={s.Brain.DisparosAVehiculos} dist={Vector3.Distance(s.transform.position, tq.transform.position):0}]");
            }
            sb.Append($"[tanque {vida0} -> {tq.Health.Current}; golpes {golpes};{det}] ");
            W8Ok(ref ok, disp > 0, sb, $"los enemigos le disparan al tanque ({disp} disparos)");
            W8Ok(ref ok, golpes > 0 && tq.Health.Current < vida0, sb, $"y le pegan: {golpes} golpes, vida {vida0} -> {tq.Health.Current}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
