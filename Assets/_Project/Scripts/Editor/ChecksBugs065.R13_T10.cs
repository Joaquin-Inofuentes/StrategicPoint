using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using SP.Actors;
using SP.Core;
using SP.Operacion;
using SP.UI;
using G = SP.Operacion.MansionGeometria;

namespace SP.EditorTools
{
    // Ronda 13, T10: los bloqueos de progresion (compuertas de la mansion, retroceso en la huida, lejania) ya no frenan al jugador:
    // solo sale el cartel unico «DEBES VOLVER A LA GUERRA» (CartelVolverView, canvas sin collider).
    public static partial class ChecksBugs065
    {
        const string TextoDeVolver = "DEBES VOLVER A LA GUERRA";

        static bool CartelDeVolverVisible(out string texto)
        {
            var c = CartelVolverView.Actual; texto = c != null ? c.TextoActual.Replace('\n', ' ') : "(sin cartel)";
            return c != null && c.Alfa > 0.5f && c.TextoActual.Contains(TextoDeVolver);
        }

        // ---------------------------------------------------------------- 966a compuerta de bajada de la mansion: atravesable y con cartel
        static IEnumerator Bug966a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.5f)) yield return x;
            ModoDios.Poner(true);
            var d = OperacionDirector.Instancia;
            if (d == null) { Fin("FALLO sin director"); yield break; }
            d.SaltarAlSegundoPisoPorPrueba();
            if (CinematicaDeRapel.Activa) CinematicaDeRapel.Saltar();   // SaltarA(Resistir) arranca el rapel, que apaga el driver; las pruebas lo cortan
            foreach (var x in Esperar(1.0f)) yield return x;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 20f && d.CineDeMansionEnCurso) yield return null;
            foreach (var x in Esperar(1.0f)) yield return x;
            var yo = Poseido();
            if (yo == null) { var dv = SP.Player.PlayerInputDriver.Activo; Fin($"FALLO sin jugador [driver={(dv != null)} brain={(dv != null && dv.Brain != null)} fase={d.Fase} etapa={d.EtapaMansion} cine={d.CineDeMansionEnCurso}]"); yield break; }
            // las compuertas (todas, de subida y de bajada) tienen el collider y el obstaculo apagados
            var lista = (IList)typeof(OperacionDirector).GetField("compuertasDeTorre", NoPublico).GetValue(d);
            int total = 0, conCollider = 0, conObstaculo = 0; object bajada = null; GameObject goBajada = null;
            foreach (var c in lista)
            {
                var t = c.GetType(); total++;
                var col = (BoxCollider)t.GetField("col").GetValue(c); var obs = (NavMeshObstacle)t.GetField("obs").GetValue(c);
                if (col != null && col.enabled) conCollider++;
                if (obs != null && obs.enabled) conObstaculo++;
                bool subida = (bool)t.GetField("subida").GetValue(c); int nb = (int)t.GetField("nivelBase").GetValue(c);
                if (!subida && nb == 1) { bajada = c; goBajada = (GameObject)t.GetField("go").GetValue(c); }
            }
            W8Ok(ref ok, total >= 6 && conCollider == 0 && conObstaculo == 0, sb, $"{total} compuertas; con collider {conCollider}, con NavMeshObstacle {conObstaculo} (0 y 0)");
            if (bajada == null || goBajada == null) { Fin("FALLO sin compuerta de bajada del 2.o piso. " + sb); yield break; }
            // piso 2 -> piso 1 con camino completo
            var a = G.Centro(2) + Vector3.up * 0.5f; var b = G.Centro(1) + Vector3.up * 0.5f;
            bool hayA = NavMesh.SamplePosition(a, out var ha, 4f, NavMesh.AllAreas), hayB = NavMesh.SamplePosition(b, out var hb, 4f, NavMesh.AllAreas);
            var camino = new NavMeshPath(); bool hay = hayA && hayB && NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, camino);
            W8Ok(ref ok, hay && camino.status == NavMeshPathStatus.PathComplete, sb, $"camino piso 2 -> piso 1 completo (muestras {hayA}/{hayB}, estado {(hay ? camino.status.ToString() : "sin calcular")})");
            // trabada la de bajada y el jugador cruzandola: sale el cartel
            bajada.GetType().GetField("trabada").SetValue(bajada, true);
            yo.transform.position = goBajada.transform.position - Vector3.up * (goBajada.transform.position.y - G.Nivel(2) - 0.9f); Physics.SyncTransforms();
            Vector3 antes = yo.transform.position;
            foreach (var x in Esperar(1.2f)) yield return x;
            bool vis = CartelDeVolverVisible(out string tx);
            var cartel = CartelVolverView.Actual;
            W8Ok(ref ok, vis, sb, $"al cruzar la compuerta de bajada el cartel queda visible (alfa {(cartel != null ? cartel.Alfa : 0f):0.00}, texto «{tx}»)");
            W8Ok(ref ok, cartel != null && !cartel.TieneCollider, sb, "el cartel no tiene collider (solo canvas)");
            W8Ok(ref ok, Vector3.Distance(antes, yo.transform.position) < 3f, sb, $"no hay teletransporte ni bloqueo ({Vector3.Distance(antes, yo.transform.position):0.0} m)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 979a huida y lejania: sin teletransporte, con cartel; rutas entre fases
        static IEnumerator Bug979a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            // -- B) Puestos: alejarse 90 m durante 5 s
            OperacionPrueba.Arrancar(2); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var d = OperacionDirector.Instancia; var yo = Poseido();
            if (d == null || yo == null) { Fin("FALLO sin director o jugador"); yield break; }
            Vector3 obj; bool hayObj = d.GuiaPermitida(out obj);
            if (!hayObj) obj = yo.transform.position;
            var dir = yo.transform.position - obj; dir.y = 0f; dir = dir.sqrMagnitude < 1f ? Vector3.back : dir.normalized;
            var inicio = yo.transform.position; float t = 0f; float distFinal = 0f;
            while (t < 6f)
            {
                yield return null; t += Time.deltaTime;
                yo = Poseido() ?? yo;
                yo.transform.position = inicio + dir * (90f * Mathf.Min(1f, t / 5f)); Physics.SyncTransforms();   // 18 m/s: se aleja de verdad
                distFinal = Vector3.Distance(new Vector3(yo.transform.position.x, 0f, yo.transform.position.z), new Vector3(obj.x, 0f, obj.z));
            }
            foreach (var x in Esperar(0.5f)) yield return x;
            bool vis = CartelDeVolverVisible(out string tx);
            { var cv = CartelVolverView.Actual; var tD = typeof(OperacionDirector); sb.Append($"[diag puestos: fase={d.Fase} cartel={(cv != null)} alfa={(cv != null ? cv.Alfa : -1f):0.00} alejandose={(cv != null ? cv.SegundosAlejandose : -1f):0.0}s activa={tD.GetField("volverActiva", NoPublico).GetValue(d)} dist={tD.GetField("volverDistancia", NoPublico).GetValue(d)} umbral={tD.GetField("volverUmbral", NoPublico).GetValue(d)} guia={hayObj} obj={obj:0} yo={yo.transform.position:0} enRadio={MandoTactico.EnRadio}] "); }
            W8Ok(ref ok, vis, sb, $"Puestos: tras alejarse (a {distFinal:0} m del objetivo) el cartel dice «{tx}»");
            // -- A) huida a pie: 30 m hacia el sur de la linea de no retroceso (despues de B: Arrancar(4) deja la escena en la huida)
            OperacionPrueba.Arrancar(4); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;

            yo = Poseido() ?? yo;
            var p0 = yo.transform.position;
            yo.transform.position = p0 + new Vector3(0f, 0f, 30f); Physics.SyncTransforms();
            foreach (var x in Esperar(1.5f)) yield return x;
            { float tc = Time.realtimeSinceStartup; while (CineDeHuida.Activa && Time.realtimeSinceStartup - tc < 15f) yield return null; }   // al acercarse al jefe arranca su toma: el control vuelve al terminar
            foreach (var x in Esperar(0.5f)) yield return x;
            yo = Poseido() ?? yo;
            float linea = d.LineaDeNoRetrocesoZ;
            var destino = new Vector3(p0.x, p0.y, linea - 30f);
            yo.transform.position = destino; Physics.SyncTransforms();
            foreach (var x in Esperar(1.0f)) yield return x;
            yo = Poseido() ?? yo;
            float forzado = Vector3.Distance(new Vector3(yo.transform.position.x, 0f, yo.transform.position.z), new Vector3(destino.x, 0f, destino.z));
            vis = CartelDeVolverVisible(out tx);
            { var cv = CartelVolverView.Actual; sb.Append($"[diag huida: cartel={(cv != null)} activo={(cv != null && cv.isActiveAndEnabled)} alfa={(cv != null ? cv.Alfa : -1f):0.00} forzado={CartelVolverView.Forzado} impedidos={d.RetrocesosImpedidos} fase={d.Fase} sub={d.Subfase} tanque={d.EnTanque} z={yo.transform.position.z:0.0} linea={d.LineaDeNoRetrocesoZ:0.0} timescale={Time.timeScale} driver={SP.Player.PlayerInputDriver.Activo != null} cineHuida={CineDeHuida.Activa} rapel={CinematicaDeRapel.Activa} intro={CinematicaDeOperacion.IntroActiva} vivo={(yo.Health != null && yo.Health.IsAlive)}] "); }
            W8Ok(ref ok, forzado < 0.5f, sb, $"huida: a 1 s el desplazamiento forzado es {forzado:0.00} m (0)");
            W8Ok(ref ok, vis, sb, $"huida: el cartel esta visible (texto «{tx}»)");
            // -- C) rutas hacia atras entre fases
            var entradas = new Vector3[7]; var hayEntrada = new bool[7];
            for (int i = 1; i <= 6; i++)
            {
                OperacionPrueba.Arrancar(i);
                foreach (var x in Esperar(1.5f)) yield return x;
                var q = Poseido(); if (q == null) continue;
                hayEntrada[i] = NavMesh.SamplePosition(q.transform.position, out var h, 8f, NavMesh.AllAreas); if (hayEntrada[i]) entradas[i] = h.position;
            }
            var malas = new StringBuilder(); var notas = new StringBuilder();
            for (int i = 2; i <= 6; i++)
            {
                var camino = new NavMeshPath();
                bool c = hayEntrada[i] && hayEntrada[i - 1] && NavMesh.CalculatePath(entradas[i], entradas[i - 1], NavMesh.AllAreas, camino) && camino.status == NavMeshPathStatus.PathComplete;
                // El Porton_Blindado (puerta de objetivo, se queda por diseno) separa el Centro de datos de la huida: ese tramo se anota y no cuenta como bloqueo de progresion.
                var porton = i == 4 ? GameObject.Find("Porton_Blindado") : null;
                if (!c && porton != null && porton.activeInHierarchy) { notas.Append($" {i}->{i - 1} cortado por Porton_Blindado (puerta de objetivo)"); continue; }
                string tope = ""; if (!c && hayEntrada[i] && hayEntrada[i - 1] && camino.corners.Length > 0)
                {
                    var fin = camino.corners[camino.corners.Length - 1]; var nm = new StringBuilder();
                    foreach (var cc in Physics.OverlapSphere(fin + Vector3.up, 4f, ~0, QueryTriggerInteraction.Ignore)) if (cc != null) nm.Append(cc.name).Append(cc.gameObject.activeInHierarchy ? "" : "(off)").Append(',');
                    tope = $" corta en {fin:0} junto a {nm}";
                }
                if (!c && (tope.Contains("Muro_Salida_Norte") || tope.Contains("Porton_"))) { notas.Append($" {i}->{i - 1}{tope} (puerta de objetivo: se queda por diseno)"); continue; }
                if (!c) malas.Append($" {i}->{i - 1}[{(hayEntrada[i] && hayEntrada[i - 1] ? camino.status.ToString() : "sin muestra")} de {entradas[i]:0} a {entradas[i - 1]:0}{tope}]");
            }
            W8Ok(ref ok, malas.Length == 0, sb, "ruta completa entrada[i] -> entrada[i-1] en las 6 fases" + (malas.Length > 0 ? " (fallan:" + malas + ")" : "") + (notas.Length > 0 ? " [notas:" + notas + "]" : ""));
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
