using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Core;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ronda 13, T7: perros en manada sin solaparse. 12 perros contra un jugador quieto e invulnerable: distancia minima entre pares >= 0,7 m en el
    // 99 % de los cuadros y ningun par a menos de 0,4 m. Con SeparacionActiva = false el mismo experimento tiene que FALLAR (prueba de que la medida discrimina).
    public static partial class ChecksBugs065
    {
        struct MedidaDeManada { public int cuadros, bajo07, bajo04; public float minimo; public int perros; public int mordidas; }

        static IEnumerator MedirManada(OperacionDirector d, Soldier yo, bool separacion, float segundos, List<PerroEnemigo> perros, MedidaDeManada[] salida)
        {
            PerroEnemigo.SeparacionActiva = separacion;
            var plantilla = (GameObject)typeof(OperacionDirector).GetField("plantillaDeRefuerzo", NoPublico).GetValue(d);
            const int N = 12;
            for (int i = 0; i < N; i++)
            {
                float ang = i * (360f / N) * Mathf.Deg2Rad, r = 18f + (i % 2) * 6f;
                var pos = yo.transform.position + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var h, 6f, UnityEngine.AI.NavMesh.AllAreas)) pos = h.position;
                var go = Object.Instantiate(plantilla, pos, Quaternion.LookRotation(Vector3.ProjectOnPlane(yo.transform.position - pos, Vector3.up).normalized, Vector3.up), plantilla.transform.parent);
                go.SetActive(true);
                var s = go.GetComponent<Soldier>(); if (s == null) { Object.Destroy(go); continue; }
                var pr = PerroEnemigo.Aplicar(s); if (pr == null) continue;
                ApoyoEnElPiso.Apoyar(s.transform);
                perros.Add(pr);
            }
            yield return null; yield return null; yield return null;
            var m = new MedidaDeManada { minimo = 99f, perros = perros.Count };
            float t0 = Time.time;
            while (Time.time - t0 < segundos)
            {
                yield return null;
                float minimo = 99f; int vivos = 0;
                for (int i = 0; i < perros.Count; i++)
                {
                    var a = perros[i]; if (a == null || a.Estado == PerroEnemigo.Fase.Muerto || a.Estado == PerroEnemigo.Fase.Salto || a.Modelo == null) continue;
                    vivos++;
                    for (int j = i + 1; j < perros.Count; j++)
                    {
                        var b = perros[j]; if (b == null || b.Estado == PerroEnemigo.Fase.Muerto || b.Estado == PerroEnemigo.Fase.Salto || b.Modelo == null) continue;
                        var pa = a.transform.position; var pb = b.transform.position; pa.y = pb.y = 0f;
                        minimo = Mathf.Min(minimo, Vector3.Distance(pa, pb));
                    }
                }
                if (vivos < 2) continue;
                m.cuadros++; if (minimo < 0.7f) m.bajo07++; if (minimo < 0.4f) m.bajo04++; m.minimo = Mathf.Min(m.minimo, minimo);
            }
            foreach (var p in perros) if (p != null) m.mordidas += p.Mordidas;
            salida[0] = m;
        }

        // ---------------------------------------------------------------- 967a perros en manada: distancia minima entre pares
        static IEnumerator Bug967a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            LeerModoDeFotos();
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(3f)) yield return x;
            var d = OperacionDirector.Instancia; var yo = Poseido();
            if (d == null || yo == null) { Fin("FALLO sin director o jugador"); yield break; }
            var inv = Wp7HacerInvulnerable();
            ModoDios.Poner(true);
            var perrosA = new List<PerroEnemigo>(); var perrosB = new List<PerroEnemigo>();
            var medA = new MedidaDeManada[1]; var medB = new MedidaDeManada[1];
            bool antes = PerroEnemigo.SeparacionActiva;
            var rutA = MedirManada(d, yo, true, 20f, perrosA, medA); while (rutA.MoveNext()) yield return rutA.Current;
            foreach (var p in perrosA) if (p != null) Object.Destroy(p.gameObject);
            yield return null; yield return null;
            var rutB = MedirManada(d, yo, false, 10f, perrosB, medB); while (rutB.MoveNext()) yield return rutB.Current;
            foreach (var p in perrosB) if (p != null) Object.Destroy(p.gameObject);
            PerroEnemigo.SeparacionActiva = antes;
            foreach (var h in inv) if (h != null) h.Invulnerable = false;
            var a = medA[0]; var b = medB[0];
            float pctA = a.cuadros > 0 ? 100f * (a.cuadros - a.bajo07) / a.cuadros : 0f, pctB = b.cuadros > 0 ? 100f * (b.cuadros - b.bajo07) / b.cuadros : 0f;
            sb.Append($"[con separacion: {a.perros} perros, {a.cuadros} cuadros, minimo {a.minimo:0.00} m, >=0,7 m en {pctA:0.0}%, pares<0,4 m en {a.bajo04} cuadros, mordidas {a.mordidas}] ");
            sb.Append($"[sin separacion: {b.perros} perros, {b.cuadros} cuadros, minimo {b.minimo:0.00} m, >=0,7 m en {pctB:0.0}%, pares<0,4 m en {b.bajo04} cuadros] ");
            W8Ok(ref ok, a.perros >= 12 && a.cuadros > 200, sb, $"se midio una manada de {a.perros} perros durante {a.cuadros} cuadros");
            W8Ok(ref ok, pctA >= 99f, sb, $"distancia minima >= 0,7 m en {pctA:0.0}% de los cuadros (>= 99)");
            W8Ok(ref ok, a.bajo04 == 0, sb, $"{a.bajo04} cuadros con algun par a menos de 0,4 m (0)");
            W8Ok(ref ok, b.cuadros > 0 && (pctB < 99f || b.bajo04 > 0), sb, $"con SeparacionActiva = false la medida FALLA (>=0,7 m en {pctB:0.0}%, pares<0,4 m en {b.bajo04} cuadros): discrimina");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
