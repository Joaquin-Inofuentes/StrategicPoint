using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Presentation;

namespace SP.EditorTools
{
    // Ronda 17c: los efectos que quedaron pendientes (polvo y arma caida al morir un enemigo; murmullo y niebla de las fuentes).
    public static partial class ChecksBugs065
    {
        static IEnumerator Bug990n()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(2.5f)) yield return x;
            var yo = W9Yo();

            // --- fuentes: cada surtidor con murmullo 3D sonando, niebla y tope de particulas
            int chorros = 0, conAudio = 0, conNiebla = 0, maxPart = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t == null || t.name != FuentesDeAgua.NombreParticulas) continue;
                chorros++;
                var au = t.GetComponent<AudioSource>();
                if (au != null && au.isPlaying && au.clip != null && au.loop && au.spatialBlend > 0.9f) conAudio++;
                if (t.Find("Niebla") != null) conNiebla++;
                int mp = 0; foreach (var ps in t.GetComponentsInChildren<ParticleSystem>(true)) mp += ps.main.maxParticles;
                if (mp > maxPart) maxPart = mp;
            }
            W8Ok(ref ok, chorros > 0, sb, $"hay {chorros} surtidores con particulas");
            W8Ok(ref ok, conAudio == chorros, sb, $"murmullo 3D sonando en {conAudio}/{chorros}");
            W8Ok(ref ok, conNiebla == chorros, sb, $"niebla en {conNiebla}/{chorros}");
            W8Ok(ref ok, maxPart > 0 && maxPart <= 120, sb, $"particulas maximas por fuente {maxPart} (<= 120)");

            // --- muerte de un enemigo con arma a la vista
            Soldier e = null; WeaponHolder wh = null;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || s.GetComponent<EnemigoPesado>() != null) continue;
                var h = s.GetComponent<WeaponHolder>();
                if (h != null && h.WeaponVisualRenderer != null && h.WeaponVisualRenderer.enabled) { e = s; wh = h; break; }
            }
            if (e == null) { sb.Append("FALLO ningun enemigo con arma visible; "); Fin("FALLO " + sb); yield break; }
            var ag = e.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag != null) ag.enabled = false;
            if (e.Brain != null) e.Brain.enabled = false;
            e.gameObject.SetActive(true);
            var fwd = yo.transform.forward; fwd.y = 0f; fwd.Normalize();
            e.transform.position = yo.transform.position + fwd * 6f; e.transform.rotation = Quaternion.LookRotation(-fwd);
            Physics.SyncTransforms();
            foreach (var x in Esperar(0.5f)) yield return x;
            int armas0 = RetiroDeEnemigosCaidos.ArmasSoltadas, polvo0 = RetiroDeEnemigosCaidos.PolvosAlCaer;
            float sueloY = e.transform.position.y;
            e.Health.TakeDamage(99999, yo.Id, false);
            foreach (var x in Esperar(0.15f)) yield return x;
            var clon = GameObject.Find("ArmaCaida");
            W8Ok(ref ok, RetiroDeEnemigosCaidos.ArmasSoltadas == armas0 + 1 && clon != null, sb, "al morir aparece el arma caida");
            W8Ok(ref ok, !wh.WeaponVisualRenderer.enabled, sb, "el arma de la mano se apaga");
            W8Ok(ref ok, clon != null && clon.GetComponentsInChildren<Collider>(true).Length == 0 && clon.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, sb, "el clon no tiene colliders ni scripts (no empuja a nadie)");
            foreach (var x in Esperar(1.6f)) yield return x;
            W8Ok(ref ok, clon != null && clon.transform.position.y <= sueloY + 0.15f && clon.transform.position.y >= sueloY - 0.05f, sb, $"el arma termina en el piso (y={(clon != null ? clon.transform.position.y - sueloY : -1f):0.00} sobre el piso)");
            W8Ok(ref ok, RetiroDeEnemigosCaidos.PolvosAlCaer == polvo0 + 1, sb, "levanto polvo al tocar el piso");
            foreach (var x in CapturarPantalla("r17_arma_caida.png")) yield return x;
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 990o: salvaguarda de 60 s de la PC (tiempo simulado: TickDeBloqueo recibe el dt)
        static IEnumerator Bug990o()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionDirector.ProbarBloqueoDelCentro = true;
            OperacionPrueba.Arrancar(3, 1);
            foreach (var x in Esperar(2f)) yield return x;
            var d = OperacionDirector.Instancia;
            var pc = d != null ? d.computadora : null;
            if (pc == null) { OperacionDirector.ProbarBloqueoDelCentro = false; Fin("FALLO no hay computadora"); yield break; }
            var original = pc.EnemigosQueBloquean;
            // dos enemigos "atascados" que nunca bajan; se suelta la salvaguarda recien al sumar 60 s sin que bajen
            pc.EnemigosQueBloquean = () => 2;
            int llamadas = 0;
            while (llamadas < 100 && !pc.SaltoLaSalvaguarda) { pc.TickDeBloqueo(0.5f); llamadas++; }
            W8Ok(ref ok, !pc.SaltoLaSalvaguarda && pc.Bloqueada, sb, $"a los {llamadas * 0.5f:0} s simulados la PC sigue bloqueada (no salta antes de tiempo)");
            while (llamadas < 140 && !pc.SaltoLaSalvaguarda) { pc.TickDeBloqueo(0.5f); llamadas++; }
            float cuando = llamadas * 0.5f;
            W8Ok(ref ok, pc.SaltoLaSalvaguarda && !pc.Bloqueada, sb, $"la salvaguarda libera la PC a los {cuando:0} s simulados (esperado ~60 s)");
            W8Ok(ref ok, cuando >= 59f && cuando <= 70f, sb, "el momento es el de SegundosDeSalvaguarda");
            pc.EnemigosQueBloquean = original;
            OperacionDirector.ProbarBloqueoDelCentro = false;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 990q: contraste de los otros tipos (escudero, pesado, francotirador)
        // Esos tipos no estan vivos al arrancar ningun objetivo (los despliega el director mas tarde), asi que se convierten fusileros con las mismas
        // funciones Aplicar() que usa el director y se miden con la misma metrica que #990m, en los objetivos 1 a 3 (distintos fondos).
        static IEnumerator Bug990q()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var porTipo = new Dictionary<string, List<MedidaContraste>>();
            for (int objetivo = 1; objetivo <= 3; objetivo++)
            {
                OperacionPrueba.Arrancar(objetivo, 1);
                ModoDios.Poner(true);
                foreach (var x in Esperar(2f)) yield return x;
                var yo = W9Yo(); if (yo == null) continue;
                var fusileros = new List<Soldier>();
                foreach (var s in ActorRegistry.All)
                {
                    if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                    if (s.GetComponent<EnemigoPesado>() != null || s.GetComponent<EnemigoEscudero>() != null || s.GetComponent<EnemigoFrancotirador>() != null) continue;
                    fusileros.Add(s);
                }
                fusileros.Sort((p, q) => (p.transform.position - yo.transform.position).sqrMagnitude.CompareTo((q.transform.position - yo.transform.position).sqrMagnitude));
                var tipos = new[] { "escudero", "pesado", "francotirador" };
                for (int i = 0; i < tipos.Length && i < fusileros.Count; i++)
                {
                    var e = fusileros[i];
                    var ag = e.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag != null) ag.enabled = false;
                    if (e.Brain != null) e.Brain.enabled = false;
                    if (tipos[i] == "escudero") EnemigoEscudero.Aplicar(e);
                    else if (tipos[i] == "pesado") EnemigoPesado.Aplicar(e);
                    else EnemigoFrancotirador.Aplicar(e);
                }
                for (int fr = 0; fr < 6; fr++) yield return null;
                for (int i = 0; i < tipos.Length && i < fusileros.Count; i++)
                {
                    var e = fusileros[i]; bool midio = false; MedidaContraste m = default;
                    for (int a = 0; a < 8 && !midio; a++)
                    {
                        float ang = a * 45f * Mathf.Deg2Rad;
                        var desde = e.transform.position + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * 10f + Vector3.up * 1.7f;
                        if (Physics.Linecast(desde, e.transform.position + Vector3.up * 1.0f, out var golpe, ~0, QueryTriggerInteraction.Ignore) && golpe.collider != null && golpe.collider.GetComponentInParent<Soldier>() != e) continue;
                        midio = R17MedirContraste(e, desde, out m);
                    }
                    if (!midio) continue;
                    m.quien = $"{tipos[i]}@obj{objetivo}";
                    if (!porTipo.TryGetValue(tipos[i], out var l)) porTipo[tipos[i]] = l = new List<MedidaContraste>();
                    l.Add(m);
                    yield return null;
                }
                ModoDios.Poner(false);
            }
            foreach (var t in new[] { "escudero", "pesado", "francotirador" })
            {
                if (!porTipo.TryGetValue(t, out var l) || l.Count == 0) { W8Ok(ref ok, false, sb, $"{t}: sin tomas con silueta visible"); continue; }
                int bajos = 0; var des = new List<float>(); var rs = new List<float>();
                foreach (var m in l) { des.Add(m.deltaE); rs.Add(m.ratio); if (m.ratio < ContrasteRatioMinimo && m.deltaE < ContrasteDeltaEMinimo) bajos++; }
                des.Sort(); rs.Sort();
                W8Ok(ref ok, bajos == 0, sb, $"{t}: {l.Count} tomas, dE mediana {des[des.Count / 2]:0.0} (min {des[0]:0.0}), ratio mediana {rs[rs.Count / 2]:0.00}, bajas {bajos}");
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
