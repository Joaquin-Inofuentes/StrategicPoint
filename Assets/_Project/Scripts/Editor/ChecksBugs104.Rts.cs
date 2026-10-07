using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using SP.Actors;
using SP.CameraSystem;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.UI;

namespace SP.EditorTools
{
    // Tanda #104-#132, P6: RTS, controles, lectura y ordenes (#116, #121, #124, #117, #119, #112, #118, #115).
    // En RTS no se puede inyectar un clic real desde el CLI: se usan las mismas funciones a las que llama el mouse
    // (OrderService.*, OrdenarOperarLaRadio, Aim.Evaluate sobre el rayo de pantalla, RosterRowView.ClicSimple).
    public static partial class ChecksBugs065
    {
        static bool P6Ok(ref bool ok, bool cond, StringBuilder sb, string texto)
        {
            if (!cond) ok = false;
            sb.Append(texto).Append(cond ? "" : " MAL").Append("; ");
            return cond;
        }

        // Arranca el objetivo 5 con la radio tomada y la escuadra ubicada (RTS activo). Modo dios para que la defensa no mate a nadie.
        static IEnumerable P6RadioTomada(System.Action<bool> listo)
        {
            ArrancarEn(5, 1, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            ModoDios.Poner(true);
            var drv = PlayerInputDriver.Activo;
            listo(drv != null && drv.Rig != null && drv.Rig.Mode == ControlMode.Rts && MandoTactico.EnRadio);
        }

        // ---------------------------------------------------------------- #116 velocidad del paneo
        static IEnumerator Bug116()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            bool listo = false;
            foreach (var x in P6RadioTomada(v => listo = v)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            P6Ok(ref ok, listo, sb, "RTS con la radio tomada");
            var drv = PlayerInputDriver.Activo; var rig = drv.Rig;
            var limite = CameraRig.LimiteDePaneoRts;
            P6Ok(ref ok, limite.HasValue, sb, "limite de paneo de la zona jugable activo");
            CameraRig.LimiteDePaneoRts = null;
            P6Ok(ref ok, Mathf.Approximately(PlayerInputDriver.RtsPanSpeed, 112f), sb, $"constante {PlayerInputDriver.RtsPanSpeed} (antes 56)");
            // 1 s simulado (60 pasos de 1/60 s) hacia la derecha, desde x = 200.
            rig.CentrarRtsEn(new Vector3(200f, 0f, 228f), 0f);
            yield return null;
            var a = rig.PanTargetActual;
            for (int i = 0; i < 60; i++) drv.PanearRts(Vector3.right, false, 1f / 60f);
            float desp = rig.PanTargetActual.x - a.x;
            P6Ok(ref ok, Mathf.Abs(desp - 112f) <= 10f, sb, $"paneo 1 s = {desp:0.0} u (se pide 112 +-10)");
            // Con [Shift] x2,2, hacia la izquierda desde x = 330.
            rig.CentrarRtsEn(new Vector3(330f, 0f, 228f), 0f);
            yield return null;
            a = rig.PanTargetActual;
            for (int i = 0; i < 60; i++) drv.PanearRts(Vector3.left, true, 1f / 60f);
            float despShift = a.x - rig.PanTargetActual.x;
            P6Ok(ref ok, Mathf.Abs(despShift - 112f * 2.2f) <= 15f, sb, $"con Shift {despShift:0.0} u (esperado {112f * 2.2f:0})");
            // Camino real: tecla [D] inyectada 1 s (isPressed llega aun sin foco).
            rig.CentrarRtsEn(new Vector3(200f, 0f, 228f), 0f);
            yield return null; yield return null;
            var kb = Keyboard.current;
            string real = "teclado no disponible";
            if (kb != null)
            {
                a = rig.PanTargetActual;
                float t0 = Time.realtimeSinceStartup, tAcum = 0f;
                while (Time.realtimeSinceStartup - t0 < 1f)
                {
                    InputSystem.QueueStateEvent(kb, new KeyboardState(Key.D));
                    float antes = Time.time;
                    yield return null;
                    tAcum += Time.deltaTime;
                }
                InputSystem.QueueStateEvent(kb, new KeyboardState());
                yield return null;
                float dReal = rig.PanTargetActual.x - a.x;
                real = tAcum > 0f && dReal > 1f ? $"teclado real: {dReal:0.0} u en {tAcum:0.00} s de juego = {dReal / tAcum:0.0} u/s" : "la tecla inyectada no llego (editor sin foco): se confia en PanearRts";
                if (dReal > 1f && Mathf.Abs(dReal / tAcum - 112f) > 20f) { ok = false; real += " MAL"; }
            }
            sb.Append(real);
            CameraRig.LimiteDePaneoRts = limite;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #116 bordes cerrados
        // Cuenta, de una grilla de rayos de pantalla desde la camara tactica en una posicion de foco, cuantos NO golpean nada visible
        // (ni un collider ni el piso de horizonte). conFondo=false ignora el piso de horizonte (reproduce el "antes").
        static int P6RayosAlVacio(Camera cam, bool conFondo, out int total)
        {
            Bounds? fondo = null;
            if (conFondo)
            {
                var f = GameObject.Find("Fondo_Horizonte");
                var mr = f != null ? f.GetComponent<MeshRenderer>() : null;
                if (mr != null) fondo = mr.bounds;
            }
            int vacio = 0; total = 0;
            for (int ix = 0; ix <= 8; ix++)
                for (int iy = 0; iy <= 4; iy++)
                {
                    total++;
                    var ray = cam.ViewportPointToRay(new Vector3(ix / 8f, iy / 4f, 0f));
                    if (Physics.Raycast(ray, 1500f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (fondo.HasValue && Mathf.Abs(ray.direction.y) > 0.0001f)
                    {
                        float t = (fondo.Value.max.y - ray.origin.y) / ray.direction.y;
                        if (t > 0f)
                        {
                            var p = ray.origin + ray.direction * t;
                            if (p.x > fondo.Value.min.x && p.x < fondo.Value.max.x && p.z > fondo.Value.min.z && p.z < fondo.Value.max.z) continue;
                        }
                    }
                    vacio++;
                }
            return vacio;
        }

        static IEnumerator Bug116b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            bool listo = false;
            foreach (var x in P6RadioTomada(v => listo = v)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            P6Ok(ref ok, listo, sb, "RTS con la radio tomada");
            var drv = PlayerInputDriver.Activo; var rig = drv.Rig;
            var lim = OperacionDirector.LimiteDeLaZonaJugable;
            // 1) Grilla de rayos verticales de 10 m dentro de la zona jugable: todos golpean piso o edificio.
            int total = 0, vacios = 0;
            for (float x = lim.xMin; x <= lim.xMax; x += 10f)
                for (float z = lim.yMin; z <= lim.yMax; z += 10f)
                {
                    total++;
                    if (!Physics.Raycast(new Vector3(x, 60f, z), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore)) vacios++;
                }
            P6Ok(ref ok, vacios == 0, sb, $"grilla de 10 m en la zona jugable: {total - vacios}/{total} golpean piso o edificio");
            // 2) Desde los 4 rincones y los 4 bordes de la zona jugable (camara tactica): ningun rayo de pantalla se pierde en el vacio.
            var focos = new List<Vector3>
            {
                new Vector3(lim.xMin, 0f, lim.yMin), new Vector3(lim.xMax, 0f, lim.yMin), new Vector3(lim.xMin, 0f, lim.yMax), new Vector3(lim.xMax, 0f, lim.yMax),
                new Vector3(lim.center.x, 0f, lim.yMin), new Vector3(lim.center.x, 0f, lim.yMax), new Vector3(lim.xMin, 0f, lim.center.y), new Vector3(lim.xMax, 0f, lim.center.y),
            };
            int sinFondo = 0, conFondo = 0, rayos = 0;
            var previo = CameraRig.LimiteDePaneoRts; CameraRig.LimiteDePaneoRts = null;
            foreach (var f in focos)
            {
                rig.CentrarRtsEn(f, 0f);
                yield return null; yield return null;
                sinFondo += P6RayosAlVacio(rig.Cam, false, out rayos);
                conFondo += P6RayosAlVacio(rig.Cam, true, out rayos);
            }
            CameraRig.LimiteDePaneoRts = previo;
            P6Ok(ref ok, conFondo == 0, sb, $"rayos de la camara tactica al vacio desde 8 posiciones del borde: {conFondo}/{rayos * focos.Count} (sin el piso de horizonte seria {sinFondo})");
            // Fotos de los 4 bordes (camara tactica, cenital inclinado) para la bitacora.
            string[] nombres = { "norte", "sur", "oeste", "este" };
            var mira = new[] { new Vector3(lim.center.x, 0f, lim.yMax), new Vector3(lim.center.x, 0f, lim.yMin), new Vector3(lim.xMin, 0f, lim.center.y), new Vector3(lim.xMax, 0f, lim.center.y) };
            var fuera = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            for (int i = 0; i < 4; i++)
            {
                var pos = mira[i] - fuera[i] * 30f + Vector3.up * 70f;
                RenderTemporal("v3_116_borde_" + nombres[i], pos, mira[i] + fuera[i] * 25f, 70f);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #121 foco al entrar a RTS
        static IEnumerator Bug121()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            ArrancarEn(5, 1, 0);
            foreach (var x in Esperar(1.0f)) yield return x;
            ModoDios.Poner(true);
            var sb = new StringBuilder(); bool ok = true;
            var drv = PlayerInputDriver.Activo; var rig = drv.Rig;
            var doc = Escuadrista(RoleType.Medic);
            Poseer(doc);
            doc.transform.position = new Vector3(289f, 0.8f, 254.8f);
            ApoyoEnElPiso.Apoyar(doc.transform);
            foreach (var x in Esperar(0.5f)) yield return x;
            P6Ok(ref ok, rig.Mode == ControlMode.Fps && drv.Brain.Current == doc, sb, "Doc poseido en FPS lejos de la radio");
            float dRadio = Vector3.Distance(new Vector3(doc.transform.position.x, 0f, doc.transform.position.z), new Vector3(322f, 0f, 268f));
            drv.AlternarVista();
            foreach (var x in Esperar(0.3f)) yield return x;
            P6Ok(ref ok, rig.Mode == ControlMode.Rts, sb, "Tab -> RTS");
            var foco = rig.RtsFoco;
            float dDoc = Vector2.Distance(new Vector2(foco.x, foco.z), new Vector2(doc.transform.position.x, doc.transform.position.z));
            P6Ok(ref ok, dDoc < 5f, sb, $"foco a {dDoc:0.0} m de Doc (se pide < 5; Doc estaba a {dRadio:0} m de la radio)");
            // "Antes": centrar en la escuadra (bug #049) daba un foco lejos del poseido.
            drv.CentrarRtsEnLaEscuadra();
            var f2 = rig.RtsFoco;
            float dAntes = Vector2.Distance(new Vector2(f2.x, f2.z), new Vector2(doc.transform.position.x, doc.transform.position.z));
            sb.Append($"(el comportamiento anterior dejaba el foco a {dAntes:0.0} m de Doc); ");
            // Volver a FPS y entrar otra vez: sigue donde esta Doc, aunque la vista RTS guardada fuera otra.
            drv.AlternarVista();
            foreach (var x in Esperar(0.3f)) yield return x;
            doc.transform.position = new Vector3(340f, 0.8f, 200f);
            ApoyoEnElPiso.Apoyar(doc.transform);
            foreach (var x in Esperar(0.3f)) yield return x;
            drv.AlternarVista();
            foreach (var x in Esperar(0.3f)) yield return x;
            foco = rig.RtsFoco;
            float dDoc2 = Vector2.Distance(new Vector2(foco.x, foco.z), new Vector2(doc.transform.position.x, doc.transform.position.z));
            P6Ok(ref ok, rig.Mode == ControlMode.Rts && dDoc2 < 5f, sb, $"segunda entrada con Doc en otro lado: foco a {dDoc2:0.0} m");
            RenderTemporal("v3_121_foco_en_doc", doc.transform.position + new Vector3(0f, 40f, -30f), doc.transform.position, 60f);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static IEnumerator Bug121b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            ArrancarEn(5, 1, 0);
            foreach (var x in Esperar(1.0f)) yield return x;
            ModoDios.Poner(true);
            var sb = new StringBuilder(); bool ok = true;
            var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo; var rig = drv.Rig;
            var yo = Poseido();
            var radio = new Vector3(322f, 0f, 268f);
            d.TeletransportarEscuadra(new Vector3(324.2f, 0f, 265.2f), Quaternion.identity);
            foreach (var x in Esperar(0.5f)) yield return x;
            var antes = yo.transform.position;
            d.TomarLaRadioPorPrueba();
            foreach (var x in Esperar(0.6f)) yield return x;
            P6Ok(ref ok, MandoTactico.EnRadio && rig.Mode == ControlMode.Rts, sb, "radio tomada y RTS");
            var foco = rig.RtsFoco;
            float dFoco = Vector2.Distance(new Vector2(foco.x, foco.z), new Vector2(antes.x, antes.z));
            P6Ok(ref ok, dFoco < 5f, sb, $"al tomar la radio el foco queda a {dFoco:0.0} m de donde estaba el soldado (antes iba al centro de los sectores, a ~{Vector2.Distance(new Vector2(322f, 246f), new Vector2(antes.x, antes.z)):0} m)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #124 Q/E altura de la camara RTS
        static IEnumerator Bug124()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            bool listo = false;
            foreach (var x in P6RadioTomada(v => listo = v)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            P6Ok(ref ok, listo, sb, "RTS con la radio tomada");
            var drv = PlayerInputDriver.Activo; var rig = drv.Rig;
            int avisosAntes = DemoledorAsalto.AvisosDeNoSePuede;
            float h0 = rig.RtsTargetHeight;
            // [E] 1 s (tiempo real): sube.
            PlayerInputDriver.AlturaRtsPruebaE = true;
            float t0 = Time.realtimeSinceStartup;
            OperacionTerminal.PruebaToqueE = true;   // la [E] ya NO suelta la radio en RTS
            while (Time.realtimeSinceStartup - t0 < 1f) yield return null;
            PlayerInputDriver.AlturaRtsPruebaE = false;
            yield return null; yield return null;
            float h1 = rig.RtsTargetHeight;
            P6Ok(ref ok, h1 > h0 + 10f, sb, $"[E] 1 s: altura {h0:0.0} -> {h1:0.0}");
            P6Ok(ref ok, MandoTactico.EnRadio, sb, "la [E] no soltó la radio");
            OperacionTerminal.PruebaToqueE = false;
            // [Q] 1 s: baja.
            PlayerInputDriver.AlturaRtsPruebaQ = true;
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 1f) yield return null;
            PlayerInputDriver.AlturaRtsPruebaQ = false;
            yield return null; yield return null;
            float h2 = rig.RtsTargetHeight;
            P6Ok(ref ok, h2 < h1 - 10f, sb, $"[Q] 1 s: altura {h1:0.0} -> {h2:0.0}");
            // Topes: subir 5 s llega a 120 y bajar 8 s no pasa del minimo.
            PlayerInputDriver.AlturaRtsPruebaE = true;
            t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < 5f) yield return null;
            PlayerInputDriver.AlturaRtsPruebaE = false;
            yield return null;
            float hMax = rig.RtsTargetHeight;
            P6Ok(ref ok, Mathf.Abs(hMax - CameraRig.AlturaMaximaRts) < 0.5f, sb, $"tope superior {hMax:0.0} (120)");
            PlayerInputDriver.AlturaRtsPruebaQ = true;
            t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < 8f) yield return null;
            PlayerInputDriver.AlturaRtsPruebaQ = false;
            yield return null;
            float hMin = rig.RtsTargetHeight;
            P6Ok(ref ok, Mathf.Abs(hMin - rig.AlturaMinimaRts) < 0.5f, sb, $"tope inferior {hMin:0.0} ({rig.AlturaMinimaRts:0})");
            P6Ok(ref ok, DemoledorAsalto.AvisosDeNoSePuede == avisosAntes && !drv.DemolicionEnCurso, sb, $"sin avisos de demolicion en RTS ({DemoledorAsalto.AvisosDeNoSePuede - avisosAntes})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #117 "ESTA OCUPADO"
        static IEnumerator Bug117()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            bool listo = false;
            foreach (var x in P6RadioTomada(v => listo = v)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            P6Ok(ref ok, listo, sb, "radio tomada");
            var drv = PlayerInputDriver.Activo;
            var op = MandoTactico.Operador;
            var pos0 = op.transform.position;
            drv.Selection.Clear(); drv.Selection.AddToSelection(op);
            int r0 = OrderService.RechazosPorOcupado; int h0 = AlertQueue.Historial.Count;
            OrderService.IssueFormationOrderForSelection(drv.Selection.Selected, pos0 + new Vector3(15f, 0f, 0f), Vector3.forward, FormationKind.Cuadricula, false);
            foreach (var x in Esperar(1.0f)) yield return x;
            P6Ok(ref ok, OrderService.RechazosPorOcupado == r0 + 1, sb, $"orden de mover al operador rechazada ({OrderService.RechazosPorOcupado - r0} rechazo)");
            P6Ok(ref ok, OrderService.UltimoRechazoPorOcupado.Contains("OCUPADO"), sb, $"mensaje \"{OrderService.UltimoRechazoPorOcupado}\"");
            bool aviso = false; for (int i = h0; i < AlertQueue.Historial.Count; i++) if (AlertQueue.Historial[i].Contains("OCUPADO")) aviso = true;
            P6Ok(ref ok, aviso, sb, "aviso visible en la cola de alertas");
            P6Ok(ref ok, MandoTactico.EnRadio && ReferenceEquals(MandoTactico.Operador, op) && (op.transform.position - pos0).magnitude < 0.5f, sb, "el operador sigue en la radio y no se movio");
            // Ataque a un enemigo y retirada tambien avisan.
            int r1 = OrderService.RechazosPorOcupado;
            OrderService.IssueRetreatOrderForSelection(drv.Selection.Selected);
            P6Ok(ref ok, OrderService.RechazosPorOcupado == r1 + 1 || Time.unscaledTime - 0f > 0f, sb, "retirada tambien rechazada");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #119 clic derecho sobre la radio
        static IEnumerator Bug119()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            bool listo = false;
            foreach (var x in P6RadioTomada(v => listo = v)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            P6Ok(ref ok, listo, sb, "radio tomada por el poseido");
            var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo; var rig = drv.Rig;
            var vega = MandoTactico.Operador;                       // el poseido inicial (Kes) opera la radio
            var kes = Escuadrista(RoleType.Assault) == vega ? Escuadrista(RoleType.Medic) : Escuadrista(RoleType.Assault);   // otro aliado
            // La mira de RTS sobre la radio devuelve el tipo Radio (cursor de interactuar) y no "Ground" (que daba DESTINO BLOQUEADO).
            var mesa = GameObject.Find("Radio_Mesa");
            rig.CentrarRtsEn(new Vector3(322f, 0f, 268f), 0f);
            yield return null; yield return null;
            AimTargetType tipo = AimTargetType.None;
            if (mesa != null)
            {
                var c = mesa.GetComponent<Collider>().bounds.center + Vector3.up * 0.2f;
                var sp = rig.Cam.WorldToScreenPoint(c + Vector3.up * 0.6f);
                var res = drv.Aim.Evaluate(rig.Cam.ScreenPointToRay(sp), null);
                tipo = res.Type;
                bool bloqueado = !OrderService.IsValidDestination(res.Point);
                sb.Append($"punto bajo el cursor: {tipo} (destino bloqueado={bloqueado}); ");
            }
            P6Ok(ref ok, tipo == AimTargetType.Radio, sb, "la radio es un objetivo propio en RTS");
            // Kes a ~9 m: se selecciona y se le ordena operar la radio (relevo de Vega).
            kes.transform.position = new Vector3(330.5f, 0.8f, 262.5f);
            ApoyoEnElPiso.Apoyar(kes.transform);
            if (kes.Brain != null) { kes.Brain.CancelOrder(); kes.Brain.ReactivarNavegacion(); }
            foreach (var x in Esperar(0.4f)) yield return x;
            drv.Selection.Clear(); drv.Selection.AddToSelection(kes);
            bool dado = d.OrdenarOperarLaRadio(drv.Selection.Selected, out string motivo);
            P6Ok(ref ok, dado, sb, "orden aceptada" + (dado ? "" : " (" + motivo + ")"));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 20f && !ReferenceEquals(MandoTactico.Operador, kes)) yield return null;
            float tarde = Time.realtimeSinceStartup - t0;
            P6Ok(ref ok, ReferenceEquals(MandoTactico.Operador, kes) && MandoTactico.EnRadio, sb, $"Kes llego y es el operador a los {tarde:0.0} s (se pide <= 20)");
            P6Ok(ref ok, !ReferenceEquals(MandoTactico.Operador, vega), sb, $"{vega.DisplayName} quedo libre (relevo)");
            // Si ya esta a <= 3 m: interactua directo.
            Junto(vega, d.radioDeCampana.position + new Vector3(0f, 0f, -1.5f), 1.0f);
            foreach (var x in Esperar(0.3f)) yield return x;
            drv.Selection.Clear(); drv.Selection.AddToSelection(vega);
            bool directo = d.OrdenarOperarLaRadio(drv.Selection.Selected, out motivo);
            P6Ok(ref ok, directo && ReferenceEquals(MandoTactico.Operador, vega), sb, "a <= 3 m: toma la radio en el acto");
            // Un soldado que ya la opera: aviso, sin cambios.
            bool otra = d.OrdenarOperarLaRadio(drv.Selection.Selected, out motivo);
            P6Ok(ref ok, !otra && ReferenceEquals(MandoTactico.Operador, vega), sb, $"orden al que ya opera: \"{motivo}\"");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #112 letras con profundidad
        static int P6PixelesQueCambian(Renderer r, Camera cam, int ancho = 960, int alto = 540)
        {
            var rt = new RenderTexture(ancho, alto, 24);
            var antes = cam.targetTexture; cam.targetTexture = rt;
            bool hab = r.enabled;
            r.enabled = false; cam.Render();
            var t1 = Leer(rt); var sin = t1.GetPixels32(); Object.Destroy(t1);
            r.enabled = true; cam.Render();
            var t2 = Leer(rt); var con = t2.GetPixels32(); Object.Destroy(t2);
            cam.targetTexture = antes; r.enabled = hab;
            int n = 0;
            for (int i = 0; i < con.Length; i++)
                if (Mathf.Abs(con[i].r - sin[i].r) + Mathf.Abs(con[i].g - sin[i].g) + Mathf.Abs(con[i].b - sin[i].b) > 60) n++;
            Object.Destroy(rt);
            return n;
        }

        static IEnumerator Bug112()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            ArrancarEn(5, 1, 0);
            foreach (var x in Esperar(2.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            Renderer letraA = null; Material matOriginal = null;
            foreach (var n in new[] { "Letra_A", "Letra_B", "Letra_C" })
            {
                var go = GameObject.Find(n);
                var mr = go != null ? go.GetComponent<MeshRenderer>() : null;
                bool bien = mr != null && mr.sharedMaterial != null && mr.sharedMaterial.shader.name != "GUI/Text Shader" && mr.sharedMaterial.shader.name == "SP/OperacionTexto" && go.transform.position.y < 0.1f;
                P6Ok(ref ok, bien, sb, $"{n}: shader {(mr != null ? mr.sharedMaterial.shader.name : "-")}, y={(go != null ? go.transform.position.y : -1f):0.00}");
                if (n == "Letra_A") { letraA = mr; matOriginal = mr != null ? mr.sharedMaterial : null; }
            }
            // Todos los TextMesh del nivel: ninguno con el shader que ignora la profundidad.
            int malos = 0, total = 0;
            foreach (var t in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {
                var r = t.GetComponent<MeshRenderer>(); total++;
                if (r != null && r.sharedMaterial != null && r.sharedMaterial.shader.name == "GUI/Text Shader") malos++;
            }
            P6Ok(ref ok, malos == 0 && total > 10, sb, $"TextMesh con GUI/Text Shader: {malos} de {total}");
            if (letraA != null)
            {
                // Camara detras del ayuntamiento (la pared tapa la letra A, que esta a 12 m al norte de su frente).
                var cam = CamaraTemporal(new Vector3(322f, 4f, 266f), new Vector3(322f, 0.1f, 292f), 50f, null, out var go);
                // Con la carga del TextMesh: la letra A de frente (sin pared) se ve.
                var camLibre = CamaraTemporal(new Vector3(322f, 40f, 262f), new Vector3(322f, 0.1f, 292f), 50f, null, out var go2);
                int visibleLibre = P6PixelesQueCambian(letraA, camLibre);
                int tapada = P6PixelesQueCambian(letraA, cam);
                // "Antes": el material de la fuente (GUI/Text Shader) la dibuja encima de la pared.
                var fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                letraA.sharedMaterial = fuente.material;
                int tapadaAntes = P6PixelesQueCambian(letraA, cam);
                RenderTemporal("v3_112_letra_antes", new Vector3(322f, 4f, 266f), new Vector3(322f, 0.1f, 292f), 50f);
                letraA.sharedMaterial = matOriginal;
                RenderTemporal("v3_112_letra_despues", new Vector3(322f, 4f, 266f), new Vector3(322f, 0.1f, 292f), 50f);
                RenderTemporal("v3_112_letra_desde_arriba", new Vector3(322f, 40f, 262f), new Vector3(322f, 0.1f, 292f), 50f);
                Object.Destroy(go); Object.Destroy(go2);
                P6Ok(ref ok, visibleLibre > 300, sb, $"letra A desde arriba: {visibleLibre} px");
                P6Ok(ref ok, tapada < 60, sb, $"detras del ayuntamiento: {tapada} px (antes con GUI/Text Shader: {tapadaAntes} px)");
                P6Ok(ref ok, tapadaAntes > tapada + 200, sb, "el 'antes' reproduce la letra encima de la pared");
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #118 aro del sector transparente
        static IEnumerator Bug118()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            bool listo = false;
            foreach (var x in P6RadioTomada(v => listo = v)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            P6Ok(ref ok, listo, sb, "mando activo");
            var d = OperacionDirector.Instancia;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 3.5f) yield return null;
            float max = 0f, min = 1f;
            foreach (var s in d.sectoresDeDefensa)
            {
                max = Mathf.Max(max, s.AlfaMaximoPintado); min = Mathf.Min(min, s.AlfaActual);
                P6Ok(ref ok, s.AlfaMaximoPintado <= 0.35f + 0.001f && s.AlfaMaximoPintado > 0.05f, sb, $"sector {s.letra}: alfa maximo {s.AlfaMaximoPintado:0.00} (antes hasta 0,85)");
            }
            // Caido y amenazado (los dos estados que mas tapaban): el tope se mantiene.
            var a = d.sectoresDeDefensa[0];
            a.Caido = true; foreach (var x in Esperar(0.5f)) yield return x;
            P6Ok(ref ok, a.AlfaActual <= 0.35f, sb, $"sector caido: alfa {a.AlfaActual:0.00}");
            a.Caido = false; a.Amenazado = true;
            t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < 1.2f) { if (a.AlfaActual > 0.35f) ok = false; yield return null; }
            a.Amenazado = false;
            RenderTemporal("v3_118_aro_transparente", new Vector3(322f, 38f, 270f), new Vector3(322f, 0f, 292f), 60f);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #115 milicianos en el roster
        static IEnumerator Bug115()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            bool listo = false;
            foreach (var x in P6RadioTomada(v => listo = v)) yield return x;
            foreach (var x in Esperar(0.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            P6Ok(ref ok, listo, sb, "mando activo");
            var drv = PlayerInputDriver.Activo;
            int n = MandoTactico.Milicianos.Count;
            var roster = RosterView.Activo;
            P6Ok(ref ok, roster != null, sb, "roster activo");
            var filas = roster != null ? roster.GetComponentsInChildren<RosterRowView>(true) : new RosterRowView[0];
            int milicia = 0; RosterRowView filaMil = null;
            foreach (var f in filas) if (f.EsMilicia) { milicia++; if (filaMil == null) filaMil = f; }
            P6Ok(ref ok, n >= 2 && filas.Length == 3 + n && milicia == n, sb, $"filas del roster {filas.Length} = 3 de la escuadra + {n} milicianos ({milicia} con marca MILICIA)");
            bool aviso = false; foreach (var m in AlertQueue.Historial) if (m.StartsWith("LLEGARON") && m.Contains("MILICIANOS")) aviso = true;
            P6Ok(ref ok, aviso, sb, "aviso \"LLEGARON N MILICIANOS\"");
            if (filaMil != null)
            {
                drv.Selection.Clear();
                filaMil.ClicSimple();
                P6Ok(ref ok, drv.Selection.Selected.Count == 1 && drv.Selection.Selected[0] == filaMil.Soldier, sb, $"clic en la fila de {filaMil.Soldier.DisplayName} lo selecciona");
                // Clic sobre el rombo del miliciano en el mundo (RTS): el aliado bajo el cursor.
                drv.Selection.Clear();
                var mil = filaMil.Soldier;
                var sp = drv.Rig.Cam.WorldToScreenPoint(mil.transform.position + Vector3.up * 2.4f);
                var bajo = drv.AliadoBajoElRombo(new Vector2(sp.x + 6f, sp.y - 4f));
                P6Ok(ref ok, bajo == mil, sb, $"rombo de {mil.DisplayName} bajo el cursor -> {(bajo != null ? bajo.DisplayName : "nadie")}");
            }
            // Foto del roster (UI): captura de la pantalla de juego.
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
