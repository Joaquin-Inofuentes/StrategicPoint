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
            var pos = new Vector3(322f, 0f, 200f);
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
    }
}
