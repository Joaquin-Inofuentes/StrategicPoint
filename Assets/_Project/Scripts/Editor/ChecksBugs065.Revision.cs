using System.Collections;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;

namespace SP.EditorTools
{
    // Revision punto por punto (2026-11-03): comprobaciones que no tenian check propio.
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- 401u trazadora en 1ra persona: la cola nunca queda detras del punto de disparo
        static IEnumerator Bug401u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.0f)) yield return x;
            ModoDios.Poner(true);
            var drv = PlayerInputDriver.Activo;
            drv.AlternarVista();
            for (int i = 0; i < 8; i++) yield return null;
            W8Ok(ref ok, drv.Rig.Mode == SP.CameraSystem.ControlMode.Fps, sb, "vista en primera persona");
            var yo = drv.Brain.Current; var cam = CamaraPrincipal.Actual;
            float peorCola = 0f, largoCercaVisible = 0f; int muestras = 0, balas = 0;
            for (int disparo = 0; disparo < 3; disparo++)
            {
                yo.Weapon.TryFire(cam.transform.position + cam.transform.forward * 0.4f, cam.transform.forward);
                Vector3 origen = Vector3.zero; bool tengo = false;
                for (int f = 0; f < 18; f++)
                {
                    yield return null;
                    foreach (var p in Projectile.ActiveInstances)
                    {
                        if (p == null || !p.gameObject.activeInHierarchy) continue;
                        var r = p.GetComponent<Renderer>(); if (r == null) continue;
                        if (!tengo) { origen = (Vector3)typeof(Projectile).GetField("origenDeVuelo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(p); tengo = true; balas++; }
                        var dir = p.transform.forward; float largo = p.transform.localScale.z;
                        float cola = Vector3.Dot((p.transform.position - dir * largo * 0.5f) - origen, dir);   // < 0 = la cola pasa detras del origen
                        if (cola < peorCola) peorCola = cola;
                        float dCam = (p.transform.position - cam.transform.position).magnitude;
                        if (r.enabled && dCam < 2.0f && largo > largoCercaVisible) largoCercaVisible = largo;
                        muestras++;
                        break;
                    }
                }
                for (int f = 0; f < 10; f++) yield return null;
            }
            W8Ok(ref ok, balas >= 1 && muestras >= 3, sb, $"se siguieron {balas} balas en {muestras} cuadros");
            W8Ok(ref ok, peorCola > -0.08f, sb, $"la cola nunca queda detras del punto de disparo (peor {peorCola:0.000} m)");
            W8Ok(ref ok, largoCercaVisible < 0.5f, sb, $"a menos de 2 m del lente no se ve ninguna barra larga (largo visible maximo {largoCercaVisible:0.00})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 402u no se puede entrar a los puntos de aparicion enemigos (solo pasan los enemigos)
        static IEnumerator Bug402u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.0f)) yield return x;
            var pos = new Vector3(327f, 0f, 192f);
            EntradaDeReserva.Marcar(pos);
            yield return null;
            var raiz = GameObject.Find("Compuerta_Reserva"); Transform tr = null;
            foreach (var g in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (g.name == "Compuerta_Reserva" && (g.position - pos).sqrMagnitude < 1f) tr = g;
            W8Ok(ref ok, tr != null, sb, "la compuerta de reserva se arma");
            if (tr == null) { Fin("FALLO " + sb); yield break; }
            var barrera = tr.GetComponentInChildren<BarreraSoloEnemigos>(true);
            W8Ok(ref ok, barrera != null && BarreraSoloEnemigos.Contiene(barrera.GetComponent<Collider>()), sb, "tiene barrera registrada como solo-enemigos");
            Physics.SyncTransforms();
            var adentro = tr.forward; adentro.y = 0f;
            foreach (var esEnemigo in new[] { false, true })
            {
                var cuerpo = new GameObject(esEnemigo ? "Cuerpo_Enemigo_Prueba" : "Cuerpo_Aliado_Prueba").transform;
                var cap = cuerpo.gameObject.AddComponent<CapsuleCollider>(); cap.radius = 0.4f; cap.height = 1.6f; cap.center = Vector3.zero;   // pivote a 0,8 m como los soldados
                cuerpo.position = pos - adentro * 4f + Vector3.up * 0.8f;   // afuera de la reserva, caminando hacia adentro
                Physics.SyncTransforms();
                for (int i = 0; i < 40; i++) { cuerpo.position += Deslizador.Resolver(cuerpo, cap, adentro * 0.2f, 0.4f, esEnemigo); Physics.SyncTransforms(); }
                float avance = Vector3.Dot(cuerpo.position - (pos - adentro * 4f + Vector3.up * 0.8f), adentro);
                if (esEnemigo) W8Ok(ref ok, avance > 7f, sb, $"el enemigo cruza la compuerta (avanzo {avance:0.0} de 8 m)");
                else W8Ok(ref ok, avance < 3f, sb, $"el jugador/aliado NO cruza la compuerta (avanzo {avance:0.0} de 8 m)");
                Object.DestroyImmediate(cuerpo.gameObject);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 403u balance del danio al tanque de la fuga (parejo, sin piso, nunca inmune)
        static IEnumerator Bug403u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            string r0 = OperacionPrueba.Arrancar(4, 1, 3);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            if (d == null || d.tanque == null) { Fin("FALLO faltan piezas (tanque)"); yield break; }
            d.Embarcar();
            foreach (var x in Esperar(8f)) yield return x;   // pasan los primeros 6 s (danio a la mitad)
            var t = d.tanque; float max = t.Health.MaxHealth;
            W8Ok(ref ok, t.PisoDeVida01 == 0f && t.AjusteDeDano != null, sb, $"sin piso de vida y con curva de danio (piso={t.PisoDeVida01:0.00})");
            int antes = t.Health.Current;
            t.TakeDamage(70, -1);   // un cohete
            int unCohete = antes - t.Health.Current;
            W8Ok(ref ok, unCohete > 0 && unCohete <= max * 0.07f + 1f, sb, $"un cohete de 70 baja {unCohete} HP (tope {max * 0.07f:0})");
            foreach (var x in Esperar(1.2f)) yield return x;
            int ini = t.Health.Current; float t0 = Time.realtimeSinceStartup; int balas = 0;
            float proximo = t0; while (Time.realtimeSinceStartup - t0 < 10f && !t.IsDestroyed) { if (Time.realtimeSinceStartup >= proximo) { t.TakeDamage(7, -1); balas++; proximo += 0.1f; } yield return null; }   // 10 impactos/s: una camioneta enemiga a quemarropa
            int perdida = ini - t.Health.Current; float pct = perdida / max * 100f;
            W8Ok(ref ok, pct >= 8f && pct <= 22f, sb, $"10 s de fuego continuo ({balas} impactos de 7, 10 por s) bajan {pct:0.0}% de la vida (esperado 8-22%: ~1,6 %/s con el redondeo por golpe)");
            W8Ok(ref ok, !t.IsDestroyed, sb, "el tanque sano aguanta 10 s de fuego continuo");
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 404u rutas nuevas: puertas de servicio de la mansion, escaleras exteriores y pasarelas
        static IEnumerator Bug404u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.0f)) yield return x;
            Physics.SyncTransforms();
            // Escaleras exteriores: rampa caminable que llega al piso 1 (6,5 m) y rellano a la altura de la puerta.
            foreach (var oeste in new[] { true, false })
            {
                RampaCaminable rampa = null;
                foreach (var r in RampaCaminable.Todas) if (r != null && r.name == (oeste ? "Servicio_Escalera_O" : "Servicio_Escalera_E")) rampa = r;
                W8Ok(ref ok, rampa != null, sb, (oeste ? "oeste" : "este") + ": existe la escalera de servicio");
                if (rampa == null) continue;
                W8Ok(ref ok, Mathf.Abs(rampa.alturaDelFinal - SP.Operacion.MansionGeometria.Nivel(1)) < 0.05f && rampa.alturaInicial < 0.2f, sb, $"{(oeste ? "oeste" : "este")}: sube de {rampa.alturaInicial:0.0} a {rampa.alturaDelFinal:0.0} m");
                // el piso del rellano esta a la altura del piso 1 y la puerta del muro lo deja pasar con un cuerpo de pie
                float xi = oeste ? SP.Operacion.MansionGeometria.X0 : SP.Operacion.MansionGeometria.X1;
                var p0 = new Vector3(xi + (oeste ? -2f : 2f), 6.5f + 0.8f, 264f);
                var cuerpo = new GameObject("Cuerpo_Prueba").transform; var cap = cuerpo.gameObject.AddComponent<CapsuleCollider>(); cap.radius = 0.4f; cap.height = 1.6f;
                cuerpo.position = p0; Physics.SyncTransforms();
                var adentro = new Vector3(oeste ? 1f : -1f, 0f, 0f);
                for (int i = 0; i < 40; i++) { cuerpo.position += Deslizador.Resolver(cuerpo, cap, adentro * 0.2f, 0.4f, false); Physics.SyncTransforms(); }
                float av = Vector3.Dot(cuerpo.position - p0, adentro);
                Object.DestroyImmediate(cuerpo.gameObject);
                W8Ok(ref ok, av > 6f, sb, $"{(oeste ? "oeste" : "este")}: del rellano se entra al 1.er piso (avanzo {av:0.0} de 8 m)");
                Physics.SyncTransforms();
                bool rellano = Physics.Raycast(new Vector3(xi + (oeste ? -1.5f : 1.5f), 9f, 264f), Vector3.down, out var h, 5f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(h.point.y - 6.5f) < 0.08f;
                W8Ok(ref ok, rellano, sb, $"{(oeste ? "oeste" : "este")}: el rellano esta a 6,5 m");
            }
            // Puertas de planta baja: pasa un cuerpo de afuera hacia adentro por cada lado.
            foreach (var oeste in new[] { true, false })
            {
                float xi = oeste ? SP.Operacion.MansionGeometria.X0 : SP.Operacion.MansionGeometria.X1; float z = oeste ? 264f : 268f;
                var p0 = new Vector3(xi + (oeste ? -3f : 3f), 0.8f, z);
                var cuerpo = new GameObject("Cuerpo_Prueba").transform; var cap = cuerpo.gameObject.AddComponent<CapsuleCollider>(); cap.radius = 0.4f; cap.height = 1.6f;
                cuerpo.position = p0; Physics.SyncTransforms();
                var adentro = new Vector3(oeste ? 1f : -1f, 0f, 0f);
                for (int i = 0; i < 40; i++) { cuerpo.position += Deslizador.Resolver(cuerpo, cap, adentro * 0.2f, 0.4f, false); Physics.SyncTransforms(); }
                float av = Vector3.Dot(cuerpo.position - p0, adentro);
                Object.DestroyImmediate(cuerpo.gameObject);
                W8Ok(ref ok, av > 6f, sb, $"{(oeste ? "oeste" : "este")}: la puerta de servicio de planta baja se cruza (avanzo {av:0.0} de 8 m)");
            }
            // Pasarelas: las cuatro rampas largas existen y el tramo recto queda a 4,4 m.
            int pas = 0; foreach (var r in RampaCaminable.Todas) if (r != null && r.name.StartsWith("Pasarela_")) pas++;
            W8Ok(ref ok, pas >= 8, sb, $"{pas} rampas de pasarela en los callejones largos (esperadas >= 8)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 405u patios abiertos: se atraviesan las manzanas a nivel del suelo
        static IEnumerator Bug405u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.0f)) yield return x;
            // (desde, hacia, directo maximo permitido): de la calle del anillo a la calle de afuera por el medio de cada manzana.
            var casos = new[] { ("NO", new Vector3(293f, 0f, 264f), new Vector3(268f, 0f, 264f)), ("NE", new Vector3(351f, 0f, 264f), new Vector3(377f, 0f, 264f)), ("SO", new Vector3(279f, 0f, 226f), new Vector3(268f, 0f, 213f)), ("SE", new Vector3(367f, 0f, 226f), new Vector3(377f, 0f, 213f)) };
            foreach (var (n, a, b) in casos)
            {
                var path = new UnityEngine.AI.NavMeshPath();
                bool sa = UnityEngine.AI.NavMesh.SamplePosition(a, out var ha, 3f, UnityEngine.AI.NavMesh.AllAreas), sb2 = UnityEngine.AI.NavMesh.SamplePosition(b, out var hb, 3f, UnityEngine.AI.NavMesh.AllAreas);
                bool hay = sa && sb2 && UnityEngine.AI.NavMesh.CalculatePath(ha.position, hb.position, UnityEngine.AI.NavMesh.AllAreas, path) && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
                float largo = 0f; if (hay) for (int i = 1; i < path.corners.Length; i++) largo += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                float directo = Vector3.Distance(a, b);
                W8Ok(ref ok, hay && largo < directo * 1.6f, sb, $"{n}: camino por el patio {(hay ? largo.ToString("0") : "sin ruta")} m contra {directo:0} m en linea recta (maximo x1,6)");
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
