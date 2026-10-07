using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using SP.Actors;
using SP.Ai;
using SP.CameraSystem;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.Presentation;

namespace SP.EditorTools
{
    // Tanda #104-#132, P4: guia y feedback en FPS (fases 1-4). #105 aliados que te siguen + carteles sin superponer + guia de objetivo,
    // #106 los aliados se alejan de una carga a punto de estallar, #111 anillo vibrante para subir al tanque.
    public static partial class ChecksBugs065
    {
        // Rectangulo en pantalla (pixeles) de un renderer: proyecta las 8 esquinas de su caja.
        static bool RectaEnPantalla(Renderer r, Camera cam, out Rect rect)
        {
            rect = default;
            if (r == null || cam == null) return false;
            var b = r.bounds;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var sp = cam.WorldToScreenPoint(c);
                if (sp.z <= 0f) return false;
                minX = Mathf.Min(minX, sp.x); maxX = Mathf.Max(maxX, sp.x);
                minY = Mathf.Min(minY, sp.y); maxY = Mathf.Max(maxY, sp.y);
            }
            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        static float DistPlana(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return (a - b).magnitude; }

        // Los aliados del jugador (sin el poseido), vivos y activos.
        static List<Soldier> AliadosDelPoseido()
        {
            var yo = Poseido();
            var l = new List<Soldier>();
            foreach (var s in Soldados(TeamId.Player))
                if (s != yo && s.Role != RoleType.Civilian && !MandoTactico.EsMiliciano(s)) l.Add(s);
            return l;
        }

        // ---------------------------------------------------------------- #105 aliados te siguen, carteles sin superponer, guia de objetivo
        static IEnumerator Bug105()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            ArrancarEn(2);
            // Sin enemigos vivos (si no, los aliados se enganchan en combate y no se puede medir el Follow): se los mata por el camino del combate.
            foreach (var e in Soldados(TeamId.Enemy)) ComandosDeDepuracion.Matar(e.Id);
            int auto0 = OperacionDirector.SeguimientosAutomaticos;
            var sb = new StringBuilder(); bool ok = true;
            foreach (var x in Esperar(3.5f)) yield return x;
            var yo = Poseido();
            if (yo == null) { Fin("FALLO sin poseido"); yield break; }
            var aliados = AliadosDelPoseido();
            if (aliados.Count < 2) { Fin("FALLO se esperaban >= 2 aliados, hay " + aliados.Count); yield break; }

            // (1) cada aliado sin orden esta en Follow al jugador a los ~3 s del inicio del objetivo.
            int siguen = 0; var detalle = new StringBuilder();
            foreach (var a in aliados)
            {
                var br = a.Brain; bool sigue = br != null && br.State == AiState.Follow && br.FollowTarget == yo;
                bool combate = br != null && (br.State == AiState.Chase || br.State == AiState.Attack);
                // Un aliado que "persigue" a un enemigo del cuartel que el salto de prueba dejo desactivado (fantasma) cuenta como libre: en el juego real esos enemigos estan muertos.
                bool fantasma = combate && br.CurrentTarget != null && !br.CurrentTarget.gameObject.activeInHierarchy;
                if (sigue) siguen++;
                else if (!fantasma) ok = false;
                detalle.Append($"{a.DisplayName}:{(br != null ? br.State.ToString() : "?")}{(sigue ? "(sigue)" : " " + br.DescribirOrden())} ");
            }
            sb.Append($"aliados {aliados.Count}: [{detalle.ToString().Trim()}] siguen={siguen}, ordenes automaticas al empezar={OperacionDirector.SeguimientosAutomaticos - auto0}; ");
            if (OperacionDirector.SeguimientosAutomaticos - auto0 < aliados.Count) ok = false;

            // (2) carteles: el aviso de seguimiento sale UNO solo y agrupado; y varios carteles que nacen juntos no se superponen en pantalla.
            var cam = CamaraPrincipal.Actual;
            // Los aliados se acomodan a ~6 m delante de la camara, uno al lado del otro, para que los carteles queden en cuadro.
            var adelante = yo.transform.forward; adelante.y = 0f; adelante.Normalize();
            var lateral = Vector3.Cross(Vector3.up, adelante);
            for (int i = 0; i < aliados.Count; i++)
            {
                var p = yo.transform.position + adelante * 7f + lateral * ((i - (aliados.Count - 1) * 0.5f) * 1.3f);
                aliados[i].transform.position = new Vector3(p.x, yo.transform.position.y, p.z);
            }
            WorldTag.ClearAll();
            yield return null;
            OrderService.IssueFollowOrderForSelection(aliados, yo);
            yield return null; yield return null;
            int agrupados = 0; string textoAgrupado = "";
            foreach (var t in WorldTag.Todas) if (t != null && t.gameObject.activeSelf && t.Texto.Contains("SIGUE")) { agrupados++; textoAgrupado = t.Texto; }
            bool nombresTodos = true;
            foreach (var a in aliados) if (!textoAgrupado.Contains(OrderService.NombreCorto(a))) nombresTodos = false;
            if (agrupados != 1 || !nombresTodos) ok = false;
            sb.Append($"carteles de seguimiento tras la orden = {agrupados} (se pide 1) '{textoAgrupado}'{(agrupados == 1 && nombresTodos ? "" : " MAL")}; ");

            // ANTES (bug reproducido): sin apilar, los carteles individuales que nacen juntos se superponen.
            int solapesAntes = 0;
            WorldTag.ApilarActivo = false;
            try
            {
                WorldTag.ClearAll();
                yield return null;
                foreach (var a in aliados) Feedback.Accion(SfxKind.FollowCall, OrderService.NombreCorto(a) + " TE SIGUE", a.transform.position, Feedback.Ok, aviso: false, pulso: false, volumen: 0.01f);
                yield return null; yield return null;
                var rectsAntes = new List<Rect>();
                foreach (var t in WorldTag.Todas)
                {
                    if (t == null || !t.gameObject.activeSelf || !t.Texto.Contains("SIGUE")) continue;
                    if (RectaEnPantalla(t.Malla.GetComponent<Renderer>(), cam, out var r0)) rectsAntes.Add(r0);
                }
                for (int i = 0; i < rectsAntes.Count; i++) for (int j = i + 1; j < rectsAntes.Count; j++) if (rectsAntes[i].Overlaps(rectsAntes[j])) solapesAntes++;
                RenderTemporal("v3_105_carteles_antes", cam.transform.position, cam.transform.position + cam.transform.forward * 10f, cam.fieldOfView);
            }
            finally { WorldTag.ApilarActivo = true; }
            sb.Append($"ANTES (sin apilar): {solapesAntes} superposiciones; ");
            // DESPUES: se apilan.
            WorldTag.ClearAll();
            yield return null;
            foreach (var a in aliados) Feedback.Accion(SfxKind.FollowCall, OrderService.NombreCorto(a) + " TE SIGUE", a.transform.position, Feedback.Ok, aviso: false, pulso: false, volumen: 0.01f);
            yield return null; yield return null;
            var rects = new List<Rect>();
            foreach (var t in WorldTag.Todas)
            {
                if (t == null || !t.gameObject.activeSelf || !t.Texto.Contains("SIGUE")) continue;
                if (RectaEnPantalla(t.Malla.GetComponent<Renderer>(), cam, out var r)) rects.Add(r);
            }
            int solapes = 0;
            for (int i = 0; i < rects.Count; i++) for (int j = i + 1; j < rects.Count; j++) if (rects[i].Overlaps(rects[j])) solapes++;
            if (rects.Count != aliados.Count || solapes > 0) ok = false;
            sb.Append($"carteles individuales en pantalla {rects.Count}/{aliados.Count}, superposiciones={solapes}{(solapes == 0 && rects.Count == aliados.Count ? "" : " MAL")}; ");
            if (CamaraPrincipal.Actual != null) RenderTemporal("v3_105_carteles_despues", cam.transform.position, cam.transform.position + cam.transform.forward * 10f, cam.fieldOfView);

            // (3) guia de objetivo: ruta punteada, flecha 3D sobre el objetivo y flecha a los pies.
            yo.transform.position = yo.transform.position - adelante * 14f;
            foreach (var x in Esperar(1.5f)) yield return x;
            var g = GuiaDeObjetivo.Instancia;
            bool ruta = g != null && g.RutaActiva, flecha = g != null && g.FlechaVisible, pies = g != null && g.FlechaDelPiesVisible;
            if (!ruta || !flecha || !pies) ok = false;
            sb.Append($"guia: activa={(g != null && g.Activa)} ruta punteada={ruta} ({(g != null ? g.PuntosDeRuta : 0)} puntos) flecha 3D={flecha} flecha a los pies={pies}{(ruta && flecha && pies ? "" : " MAL")}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #106 los aliados se alejan de una carga que va a estallar
        // Escenario: el jugador a 18 m y los aliados junto a la carga (2,5 a 4 m), sin orden; se planta, mecha a 5,9 s y se mide la distancia
        // de cada uno a los 3 s de simulacion.
        static IEnumerable Escenario106(CargaExplosiva carga, Soldier yo, List<Soldier> aliados, float[] inicial, float[] fin, bool[] registrada)
        {
            var cp = carga.transform.position;
            NavMesh.SamplePosition(cp + new Vector3(0f, 0f, -18f), out var hj, 6f, NavMesh.AllAreas);
            yo.transform.position = new Vector3(hj.position.x, yo.transform.position.y, hj.position.z);
            for (int i = 0; i < aliados.Count; i++)
            {
                NavMesh.SamplePosition(cp + new Vector3(i == 0 ? 2.5f : -2.5f, 0f, -3f), out var h, 4f, NavMesh.AllAreas);
                aliados[i].transform.position = new Vector3(h.position.x, aliados[i].transform.position.y, h.position.z);
                aliados[i].Brain.CancelOrder();
            }
            yield return null;
            for (int i = 0; i < aliados.Count; i++) inicial[i] = DistPlana(aliados[i].transform.position, cp);
            carga.PlantarPorPrueba(yo);
            carga.AcortarMecha(5.9f);
            registrada[0] = CargaExplosiva.EstaArmada(carga);
            float s0 = Time.time, r0 = Time.realtimeSinceStartup;   // 3 s de tiempo de simulacion (el editor pesado va mas lento que el real)
            while (Time.time - s0 < 3.0f && Time.realtimeSinceStartup - r0 < 30f) yield return null;
            for (int i = 0; i < aliados.Count; i++) fin[i] = DistPlana(aliados[i].transform.position, cp);
        }

        static IEnumerator Bug106()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            ArrancarEn(2);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            var yo = Poseido();
            if (d.puestos == null || d.puestos.Length < 2 || d.puestos[0].carga == null || d.puestos[1].carga == null || yo == null) { Fin("FALLO sin cargas o sin poseido"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var aliados = AliadosDelPoseido();
            if (aliados.Count < 2) { Fin("FALLO se esperaban >= 2 aliados"); yield break; }
            float radio = CargaExplosiva.RadioDeExplosion;
            int n = aliados.Count;

            // ANTES (bug reproducido): con la reaccion apagada se quedan dentro del radio (carga del puesto Este).
            var ini0 = new float[n]; var fin0 = new float[n]; var reg0 = new bool[1];
            AiBrain.AlejarseDeCargasActivo = false;
            try { foreach (var x in Escenario106(d.puestos[1].carga, yo, aliados, ini0, fin0, reg0)) yield return x; }
            finally { AiBrain.AlejarseDeCargasActivo = true; }
            int dentroAntes = 0; for (int i = 0; i < n; i++) if (fin0[i] <= radio) dentroAntes++;
            sb.Append($"ANTES (sin reaccion): {dentroAntes}/{n} aliados siguen dentro del radio a los 3 s; ");
            if (dentroAntes == 0) ok = false;
            // Se espera a que esa carga estalle y a que el NavMesh se rehaga (el bunker se derrumba) antes del segundo escenario.
            float rEspera = Time.realtimeSinceStartup;
            while (d.puestos[1].carga.Situacion != CargaExplosiva.Estado.Volada && Time.realtimeSinceStartup - rEspera < 20f) yield return null;
            foreach (var x in Esperar(3f)) yield return x;

            // DESPUES: con la reaccion, todos fuera del radio a los 3 s (carga del puesto Oeste).
            var ini = new float[n]; var fin = new float[n]; var reg = new bool[1];
            var carga = d.puestos[0].carga;
            foreach (var x in Escenario106(carga, yo, aliados, ini, fin, reg)) yield return x;
            bool todosFuera = true;
            for (int i = 0; i < n; i++) if (fin[i] <= radio) todosFuera = false;
            if (!reg[0] || !todosFuera) ok = false;
            sb.Append($"carga registrada={reg[0]}; radio={radio:0}; ");
            for (int i = 0; i < n; i++) sb.Append($"{aliados[i].DisplayName}: {ini[i]:0.0} m -> {fin[i]:0.0} m a los 3 s (alejandose={aliados[i].Brain.AlejandoseDeUnaCarga}); ");
            sb.Append(todosFuera ? "todos fuera del radio" : "ALGUNO SIGUE DENTRO DEL RADIO (MAL)");
            // Despues de estallar, vuelven a su flujo normal (no quedan alejandose).
            foreach (var x in Esperar(5f)) yield return x;
            bool liberados = true;
            foreach (var a in aliados) if (a.Brain.AlejandoseDeUnaCarga) liberados = false;
            if (!liberados || carga.Situacion != CargaExplosiva.Estado.Volada) ok = false;
            sb.Append($"; tras estallar: volada={carga.Situacion == CargaExplosiva.Estado.Volada} liberados={liberados}{(liberados ? "" : " MAL")}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #111 anillo vibrante en el tanque
        static IEnumerator Bug111()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            ArrancarEn(4, 1, (int)SubfaseHuida.Abordaje);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            var sb = new StringBuilder(); bool ok = true;
            bool activo = AnilloDeSubida.EstaActivo;
            if (!activo) ok = false;
            // Escala medida en 1 s de tiempo real (cada cuadro).
            float mn = float.MaxValue, mx = float.MinValue; int cuadros = 0;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 1f)
            {
                yield return null;
                var a = AnilloDeSubida.Instancia;
                if (a == null) continue;
                mn = Mathf.Min(mn, a.EscalaActual); mx = Mathf.Max(mx, a.EscalaActual); cuadros++;
            }
            if (mx - mn <= 0.4f) ok = false;
            sb.Append($"anillo activo={activo}; escala {mn:0.00}..{mx:0.00} (variacion {mx - mn:0.00}, se pide > 0,40) en {cuadros} cuadros{(mx - mn > 0.4f ? "" : " MAL")}; ");
            var tanque = d.tanque;
            var cam = CamaraPrincipal.Actual;
            var yo = Poseido();
            if (yo != null) RenderTemporal("v3_111_anillo", tanque.transform.position + new Vector3(-7f, 4.5f, -9f), tanque.transform.position, 55f);
            // Subir: el anillo se apaga.
            d.Embarcar();
            foreach (var x in Esperar(1f)) yield return x;
            bool apagado = !AnilloDeSubida.EstaActivo;
            if (!apagado || !d.EnTanque) ok = false;
            sb.Append($"al subir: EnTanque={d.EnTanque} anillo apagado={apagado}{(apagado ? "" : " MAL")}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
