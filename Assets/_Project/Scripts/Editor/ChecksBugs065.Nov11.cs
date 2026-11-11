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
    // Checks de los bugs #415-#422 (2026-11-11). Las capturas de los reportes estan en Registros/bug_415..422_*.png; las "despues" en Assets/Validacion~/v10_*.
    public static partial class ChecksBugs065
    {
        static int ContarActivosPorNombre(params string[] nombres)
        {
            int n = 0; var set = new HashSet<string>(nombres);
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude)) if (t != null && t.gameObject.activeInHierarchy && set.Contains(t.name)) n++;
            return n;
        }

        // ---------------------------------------------------------------- 415 cubos raros del centro de datos (los UPS y la torre negra)
        static IEnumerator Bug415a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(3);
            foreach (var x in Esperar(4f)) yield return x;
            int ups = ContarActivosPorNombre("UPS", "UPS_Panel", "UPS_Led");
            int torre = 0, mesa = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                bool enCentro = false; for (var p = t.parent; p != null; p = p.parent) if (p.name == "3_CentroDeDatos") { enCentro = true; break; }
                if (!enCentro) continue;
                if (t.name == "Torre_Servidor") torre++;
                if (t.name == "Mesa") mesa++;
            }
            W8Ok(ref ok, ups == 0, sb, $"no queda ningun UPS ({ups})");
            W8Ok(ref ok, torre == 0, sb, $"no queda la torre de servidor negra ({torre})");
            W8Ok(ref ok, mesa > 0, sb, $"la computadora (Mesa) sigue en su lugar ({mesa})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 416 modo dios: el tanque propio no pierde vida y la barra queda llena
        static IEnumerator Bug416a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var v = TanqueDelJugador();
            if (v == null) { Fin("FALLO no hay tanque propio"); yield break; }
            SP.UI.CapasDeHud.ModoTanque(true); SP.UI.HudDeTanqueSimple.Mostrar(v);
            foreach (var x in Esperar(0.5f)) yield return x;
            int max = v.Health.MaxHealth, mostradosAntes = SP.UI.ImpactoEnVehiculoView.Mostrados;
            for (int i = 0; i < 12; i++) { SP.Vehicles.Vehicle.DanoDeExplosionEnCurso = i % 2 == 0; v.TakeDamage(Mathf.Max(60, max / 6), 0); }
            SP.Vehicles.Vehicle.DanoDeExplosionEnCurso = false;
            foreach (var x in Esperar(0.4f)) yield return x;
            var h = SP.UI.HudDeTanqueSimple.Instancia;
            W8Ok(ref ok, v.Health.Current == max, sb, $"con modo dios la vida del tanque no baja ({v.Health.Current}/{max})");
            W8Ok(ref ok, h != null && h.FraccionDeVida >= 0.999f, sb, $"la barra de vida sigue al maximo ({(h != null ? h.FraccionDeVida : -1f):0.00})");
            W8Ok(ref ok, SP.UI.ImpactoEnVehiculoView.Mostrados > mostradosAntes, sb, $"el golpe se sigue sintiendo ({mostradosAntes} -> {SP.UI.ImpactoEnVehiculoView.Mostrados})");
            W8Ok(ref ok, v.Health.IsAlive && !v.IsDestroyed, sb, "el tanque sigue vivo");
            ModoDios.Poner(false);
            v.TakeDamage(Mathf.Max(60, max / 10), 0);
            foreach (var x in Esperar(0.3f)) yield return x;
            W8Ok(ref ok, v.Health.Current < max, sb, $"sin modo dios el tanque SI pierde vida ({v.Health.Current}/{max})");
            SP.UI.CapasDeHud.ModoTanque(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 417 el rombo de los coches enemigos (antes un cubo blanco/gris enorme)
        static IEnumerator Bug417a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var d = Object.FindFirstObjectByType<OperacionDirector>();
            var crear = typeof(OperacionDirector).GetMethod("CrearAuto", NoPublico);
            for (int i = 0; i < 6; i++) { crear?.Invoke(d, null); yield return null; }
            foreach (var x in Esperar(1f)) yield return x;
            int enemigos = 0, rojos = 0; var grises = new StringBuilder();
            foreach (var v in SP.Vehicles.Vehicle.Todos)
            {
                if (v == null || !v.isActiveAndEnabled || v.Bando != TeamId.Enemy) continue;
                var rombo = v.transform.Find("RomboTanque"); if (rombo == null) continue;
                var r = rombo.GetComponent<Renderer>(); if (r == null) continue;
                enemigos++;
                var c = ColorDe(r);
                if (c.r > 0.8f && c.g < 0.25f && c.b < 0.25f) rojos++; else grises.Append(v.name).Append(c).Append(' ');
            }
            W8Ok(ref ok, enemigos >= 4, sb, $"hay coches enemigos con rombo ({enemigos})");
            W8Ok(ref ok, rojos == enemigos, sb, $"todos los rombos de coches enemigos son rojos ({rojos}/{enemigos}; no rojos: {grises})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 418 correr (Shift) cuesta arriba sin atravesar la rampa
        // Sube la rampa mas larga corriendo (velocidad x3,4 = Shift x1,7 y modo dios x2) con cuadros de 0,15 s (un Editor cargado va a ~7 FPS).
        // Devuelve {llego, peorDesvio, ultimoDesvio, pasoMax, largo, cuadros}.
        static readonly StringBuilder trazaRampa = new StringBuilder();
        static IEnumerable SubirLaRampaCorriendo(RampaCaminable rampa, Soldier yo, float[] r)
        {
            var eje = rampa.arriba - rampa.abajo; eje.y = 0f; float largo = eje.magnitude; eje /= largo;
            var m = yo.Motor; float multAntes = m.SpeedMultiplier; m.SpeedMultiplier = 3.4f;
            var inicio = rampa.abajo + eje * 0.5f;
            yo.transform.position = new Vector3(inicio.x, rampa.alturaInicial + SoldierMotor.PivoteSobrePiso, inicio.z);
            yield return null; yield return null;
            float peor = 0f, pasoMax = 0f, s = 0f; int frames = 0;
            while (frames < 120 && s < largo - 0.5f)
            {
                var antes = yo.transform.position;
                m.Move(eje, Time.deltaTime);
                var p1 = yo.transform.position;
                pasoMax = Mathf.Max(pasoMax, Vector3.Distance(new Vector3(antes.x, 0f, antes.z), new Vector3(p1.x, 0f, p1.z)));
                yield return null; frames++;
                var p = yo.transform.position;
                s = Vector3.Dot(new Vector3(p.x - rampa.abajo.x, 0f, p.z - rampa.abajo.z), eje);
                if (rampa.Superficie(p, out float ry)) peor = Mathf.Max(peor, Mathf.Abs(p.y - (ry + SoldierMotor.PivoteSobrePiso)));
                if (frames <= 6) trazaRampa.Append($"f{frames}:s={s:0.0},y={p.y:0.0},salta={m.IsJumping} ");
            }
            var pf = yo.transform.position; rampa.Superficie(pf, out float ryF);
            m.SpeedMultiplier = multAntes;
            r[0] = s >= largo - 1.5f ? 1f : 0f; r[1] = peor; r[2] = Mathf.Abs(pf.y - (ryF + SoldierMotor.PivoteSobrePiso)); r[3] = pasoMax; r[4] = largo; r[5] = frames;
        }

        static IEnumerator Bug418a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var yo = Poseido();
            if (yo == null) { Fin("FALLO no hay soldado poseido"); yield break; }
            RampaCaminable rampa = null; float largoMax = 0f;
            foreach (var rr in RampaCaminable.Todas) { if (rr == null) continue; var e = rr.arriba - rr.abajo; e.y = 0f; if (e.magnitude > largoMax && Mathf.Abs(rr.alturaDelFinal - rr.alturaInicial) > 2f) { largoMax = e.magnitude; rampa = rr; } }
            if (rampa == null) { Fin("FALLO no hay rampa caminable"); yield break; }
            Time.captureDeltaTime = 0.15f;
            trazaRampa.Clear();
            var antes = new float[6]; var despues = new float[6];
            RampaCaminable.MargenPorVelocidad = false;   // comportamiento original
            foreach (var x in SubirLaRampaCorriendo(rampa, yo, antes)) yield return x;
            RampaCaminable.MargenPorVelocidad = true;
            foreach (var x in SubirLaRampaCorriendo(rampa, yo, despues)) yield return x;
            Time.captureDeltaTime = 0f;
            sb.Append($"[traza {trazaRampa}] ");
            sb.Append($"[antes: llega={antes[0]} desvio max {antes[1]:0.00} m, al final {antes[2]:0.00} m, {antes[3]:0.0} m por cuadro] ");
            W8Ok(ref ok, despues[0] > 0.5f, sb, $"llega al final de la rampa corriendo ({despues[4]:0.0} m en {despues[5]} cuadros, hasta {despues[3]:0.0} m por cuadro)");
            W8Ok(ref ok, despues[1] < 0.35f, sb, $"el soldado queda pegado a la superficie durante la subida (peor desvio {despues[1]:0.00} m)");
            W8Ok(ref ok, despues[2] < 0.35f, sb, $"termina sobre la rampa y no adentro (desvio final {despues[2]:0.00} m)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 419 los enemigos ignoran a los milicianos mientras estan encarcelados
        static IEnumerator Bug419a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var d = OperacionDirector.Instancia;
            d.SaltarAlSegundoPisoPorPrueba();
            foreach (var x in Esperar(2f)) yield return x;
            var presos = new List<Soldier>();
            foreach (var s in ActorRegistry.All) if (s != null && s.name.StartsWith("Prisionero_")) presos.Add(s);
            W8Ok(ref ok, presos.Count >= 1, sb, $"hay prisioneros en las celdas ({presos.Count})");
            bool todosPresos = true; foreach (var p in presos) if (!p.Encarcelado) todosPresos = false;
            W8Ok(ref ok, todosPresos, sb, "todos estan marcados como encarcelados");
            // un enemigo vivo y activo se planta a 4 m del primer prisionero, con linea de vista
            Soldier enemigo = null;
            foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Enemy && s.Health != null && s.Health.IsAlive && s.Brain != null) { enemigo = s; break; }
            if (enemigo == null || presos.Count == 0) { Fin("FALLO sin enemigo o sin presos: " + sb); yield break; }
            enemigo.gameObject.SetActive(true);
            var pos0 = presos[0].transform.position;
            enemigo.transform.position = pos0 + new Vector3(0f, 0f, -4f);
            SpatialGrid.Rebuild();
            var cercano = ActorRegistry.FindNearestEnemyInRange(enemigo.transform.position, TeamId.Enemy, 8f);
            W8Ok(ref ok, cercano == null || !cercano.Encarcelado, sb, $"la busqueda de blancos no devuelve a un preso ({(cercano != null ? cercano.name : "nada")})");
            int atacandoAPresos = 0; float t0 = Time.time;
            while (Time.time - t0 < 6f)
            {
                foreach (var e in ActorRegistry.All)
                    if (e != null && e.Team == TeamId.Enemy && e.Brain != null && e.Brain.CurrentTarget != null && e.Brain.CurrentTarget.Encarcelado) atacandoAPresos++;
                yield return null;
            }
            W8Ok(ref ok, atacandoAPresos == 0, sb, $"ningun enemigo apunta a un preso en 6 s ({atacandoAPresos} cuadros)");
            typeof(OperacionDirector).GetMethod("LiberarMilicianos", NoPublico).Invoke(d, null);
            foreach (var x in Esperar(1f)) yield return x;
            bool liberados = true; foreach (var p in presos) if (p == null || p.Encarcelado) liberados = false;
            W8Ok(ref ok, liberados, sb, "al liberarlos dejan de estar encarcelados");
            SpatialGrid.Rebuild();
            enemigo.transform.position = presos[0].transform.position + new Vector3(0f, 0f, -3f);
            SpatialGrid.Rebuild();
            var tras = ActorRegistry.FindNearestEnemyInRange(enemigo.transform.position, TeamId.Enemy, 8f);
            W8Ok(ref ok, tras != null, sb, $"liberados vuelven a ser blanco ({(tras != null ? tras.name : "nada")})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 420 la zona de uso y el rombo de la terminal estan en la pantalla
        static IEnumerator Bug420a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; var dd = new OperacionDirector[1];
            foreach (var x in ArrancarMansion(dd)) yield return x;
            var d = dd[0];
            var yo = PlayerInputDriver.Activo != null ? PlayerInputDriver.Activo.Brain.Current : null;
            if (yo == null) { Fin("FALLO no hay soldado poseido"); yield break; }
            typeof(OperacionDirector).GetMethod("EntrarSegundoPiso", NoPublico).Invoke(d, null);
            yo.transform.position = G.Centro(2) + new Vector3(0f, SoldierMotor.PivoteSobrePiso, -6f);
            // la cinematica de las celdas le saca el control un rato; despues el director vuelve a ver al jugador y pone la baliza
            float tEspera = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tEspera < 40f && (PlayerInputDriver.Activo == null || PlayerInputDriver.Activo.Brain.Current == null)) yield return null;
            foreach (var x in Esperar(2f)) yield return x;
            yo = PlayerInputDriver.Activo != null ? PlayerInputDriver.Activo.Brain.Current : yo;
            yo.transform.position = G.Centro(2) + new Vector3(0f, SoldierMotor.PivoteSobrePiso, -6f);
            foreach (var x in Esperar(1.5f)) yield return x;
            var term = d.TerminalDeCeldas;
            if (term == null) { Fin("FALLO no hay terminal de celdas"); yield break; }
            var pantalla = term.Find("Pantalla");
            Vector3 Plano2(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return a - b; }
            float dUso = Plano2(d.PosDeUsoDeLaTerminal, pantalla.position).magnitude;
            float dRombo = Vector3.Distance(d.PosDelRomboDeLaTerminal, pantalla.position);
            float dViejo = Plano2(d.PosTerminalDeCeldas, pantalla.position).magnitude;
            W8Ok(ref ok, dUso <= 1.3f, sb, $"la zona de uso queda delante de la pantalla ({dUso:0.0} m; el punto de diseno viejo estaba a {dViejo:0.0} m)");
            W8Ok(ref ok, dRombo <= 0.7f, sb, $"el rombo flota sobre la pantalla ({dRombo:0.0} m)");
            // la baliza real del director (el rombo que ve el jugador) esta sobre la pantalla; el soldado esta lejos, asi que todavia no esta en la zona
            SP.Tutorial.TutorialBeacon baliza = null; float mejor = float.MaxValue;
            foreach (var b in SP.Tutorial.TutorialBeacon.Activas) { if (b == null) continue; float dd2 = Plano2(b.Posicion, pantalla.position).magnitude; if (dd2 < mejor) { mejor = dd2; baliza = b; } }
            W8Ok(ref ok, baliza != null && mejor <= 1.3f, sb, $"la baliza (rombo) del director esta en la pantalla ({(baliza != null ? mejor.ToString("0.0") : "sin baliza")} m)");
            // parado en la zona de uso (1 m delante de la pantalla) se esta dentro del alcance y el aviso lo ofrece
            var u = d.PosDeUsoDeLaTerminal; yo.transform.position = new Vector3(u.x, G.Nivel(2) + SoldierMotor.PivoteSobrePiso, u.z);
            foreach (var x in Esperar(1f)) yield return x;
            float dUsuario = Plano2(yo.transform.position, term.position).magnitude;
            W8Ok(ref ok, dUsuario < OperacionDirector.AlcanceDeLaTerminalDeCeldas, sb, $"parado en la zona se esta dentro del alcance de uso ({dUsuario:0.0} m < {OperacionDirector.AlcanceDeLaTerminalDeCeldas} m)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 421 cuerpos flotando (colgados de la cuerda, ragdolls que se desarman en el aire...)
        static IEnumerator Bug421a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var d = OperacionDirector.Instancia;
            d.SaltarAlSegundoPisoPorPrueba();
            foreach (var x in Esperar(1f)) yield return x;
            typeof(OperacionDirector).GetMethod("EntrarAzotea", NoPublico).Invoke(d, null);
            // dos cadaveres colgados a proposito (no hace falta esperar a un helicoptero): se los deja a 6 m de la azotea
            int colgados = 0; var colgadosLista = new List<Soldier>();
            foreach (var s in ActorRegistry.All)
            {
                if (colgados >= 3) break;
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || s.Brain == null) continue;
                s.gameObject.SetActive(true);
                s.transform.position = new Vector3(318f + colgados * 2f, 26.5f, 268f);
                s.Health.TakeDamage(99999, -1);
                colgadosLista.Add(s); colgados++;
            }
            float t0 = Time.time, maxHueco = 0f; var flotando = new StringBuilder();
            while (Time.time - t0 < 12f) { yield return null; }
            foreach (var s in colgadosLista)
            {
                if (s == null) continue;
                float piso = PisoReal.Altura(s.transform.position + Vector3.up);
                float hueco = s.transform.position.y - piso - SoldierMotor.PivoteSobrePiso;
                maxHueco = Mathf.Max(maxHueco, hueco);
                if (hueco > 0.7f) flotando.Append(s.name).Append(" a ").Append(hueco.ToString("0.0")).Append(" m; ");
            }
            W8Ok(ref ok, colgados >= 2, sb, $"se colgaron {colgados} cadaveres a 6 m del piso");
            W8Ok(ref ok, flotando.Length == 0, sb, $"12 s despues ninguno flota (peor hueco {maxHueco:0.00} m; flotando: {flotando})");
            W8Ok(ref ok, CaidaDeCuerpos.Caidos >= 2, sb, $"el vigilante los hizo caer ({CaidaDeCuerpos.Caidos})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 422 bloques negros de la azotea
        static IEnumerator Bug422a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(4f)) yield return x;
            int equipos = ContarActivosPorNombre("Azotea_Equipo");
            W8Ok(ref ok, equipos == 0, sb, $"no queda ningun bloque negro de equipo en la azotea ({equipos})");
            var caja = new Bounds(new Vector3(325f, 21f, 266f), new Vector3(24f, 3f, 24f));
            int oscuros = 0; var nombres = new StringBuilder();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || !r.bounds.Intersects(caja)) continue;
                var m = r.sharedMaterial; if (m == null || m.name != "M_Op_Mansion_Barrote") continue;
                var sz = r.bounds.size; if (Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z)) < 1.2f || Mathf.Min(sz.x, sz.z) < 0.9f || sz.y < 0.6f) continue;   // cajas gordas: no barrotes ni cables
                oscuros++; nombres.Append(RutaCompleta(r.transform)).Append(' ');
            }
            W8Ok(ref ok, oscuros == 0, sb, $"no quedan cajas negras gordas sobre la azotea ({oscuros}: {nombres})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 806 medida de fluidez (#416 "se traba muchisimo"): tiempos de cuadro en la Huida y en la mansion
        static IEnumerator Bug806a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            foreach (int objetivo in new[] { 4, 5 })
            {
                OperacionPrueba.Arrancar(objetivo); ModoDios.Poner(true);
                foreach (var x in Esperar(3f)) yield return x;
                var dts = new List<float>(); var picos = new StringBuilder();
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 20f)
                {
                    yield return null;
                    float dt = Time.unscaledDeltaTime; dts.Add(dt);
                    if (dt > 0.12f && picos.Length < 160) picos.Append($"t={Time.realtimeSinceStartup - t0:0.0}s:{dt * 1000f:0}ms ");
                }
                dts.Sort();
                float p50 = dts[dts.Count / 2], p95 = dts[(int)(dts.Count * 0.95f)], mx = dts[dts.Count - 1]; int sobre = 0; foreach (var v in dts) if (v > 0.12f) sobre++;
                sb.Append($"[obj{objetivo}: {dts.Count} cuadros, p50 {p50 * 1000f:0} ms, p95 {p95 * 1000f:0} ms, max {mx * 1000f:0} ms, {sobre} sobre 120 ms {picos}] ");
                if (mx > 0.5f) ok = false;
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 899 fotos "despues" de los bugs #415-#422 para el atlas (Assets/Validacion~/v10_*)
        static Vector3 MiraDe(Vector3 pos, Vector3 rot) => pos + Quaternion.Euler(rot) * Vector3.forward * 10f;

        static IEnumerator Bug899a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var dir = System.IO.Path.Combine(Application.dataPath, "Validacion~"); System.IO.Directory.CreateDirectory(dir);
            // 415 y 422: la misma camara del reporte (camPos / camRot del json)
            OperacionPrueba.Arrancar(3);
            foreach (var x in Esperar(4f)) yield return x;
            var c415 = new Vector3(-7.234f, 3.586f, -52.583f);
            foreach (var x in FotoCamara("v10_415_despues.jpg", c415, MiraDe(c415, new Vector3(18.3f, 27.849f, 0f)), 60f, 1280, 720)) yield return x;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(4f)) yield return x;
            var c422 = new Vector3(315.623f, 23.185f, 263.863f);
            foreach (var x in FotoCamara("v10_422_despues.jpg", c422, MiraDe(c422, new Vector3(19.803f, 50.396f, 0f)), 60f, 1280, 720)) yield return x;

            // 417 y 416: la Huida con coches enemigos y el tanque con modo dios
            OperacionPrueba.Arrancar(4); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var d = Object.FindFirstObjectByType<OperacionDirector>();
            var crear = typeof(OperacionDirector).GetMethod("CrearAuto", NoPublico);
            for (int i = 0; i < 6; i++) { crear?.Invoke(d, null); yield return null; }
            foreach (var x in Esperar(1.5f)) yield return x;
            OperacionAuto auto = null;
            foreach (var a in Object.FindObjectsByType<OperacionAuto>(FindObjectsSortMode.None)) if (a != null && !a.EsTanque && !a.Muerto) { auto = a; break; }
            if (auto != null)
            {
                var cp = auto.transform.position;
                var camPos = cp - auto.transform.forward * 11f + auto.transform.right * 3f + Vector3.up * 3.4f;
                foreach (var x in FotoCamara("v10_417_despues.jpg", camPos, cp + Vector3.up * 3f, 55f, 1280, 720)) yield return x;
            }
            var v = TanqueDelJugador();
            if (v != null)
            {
                SP.UI.CapasDeHud.ModoTanque(true); SP.UI.HudDeTanqueSimple.Mostrar(v);
                foreach (var x in Esperar(0.5f)) yield return x;
                for (int i = 0; i < 12; i++) { SP.Vehicles.Vehicle.DanoDeExplosionEnCurso = i % 2 == 0; v.TakeDamage(Mathf.Max(60, v.Health.MaxHealth / 6), 0); }
                SP.Vehicles.Vehicle.DanoDeExplosionEnCurso = false;
                foreach (var x in Esperar(0.8f)) yield return x;
                try { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "v10_416_despues.png")); } catch { }
                foreach (var x in Esperar(1f)) yield return x;
                SP.UI.CapasDeHud.ModoTanque(false);
            }

            // 418: la rampa, vista de costado, con el original (margen apagado) y con el arreglo, en el mismo cuadro de la subida
            OperacionPrueba.Arrancar(5); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var yo = Poseido();
            RampaCaminable rampa = null; float largoMax = 0f;
            foreach (var rr in RampaCaminable.Todas) { if (rr == null) continue; var e = rr.arriba - rr.abajo; e.y = 0f; if (e.magnitude > largoMax && Mathf.Abs(rr.alturaDelFinal - rr.alturaInicial) > 2f) { largoMax = e.magnitude; rampa = rr; } }
            if (yo != null && rampa != null)
            {
                var eje = rampa.arriba - rampa.abajo; eje.y = 0f; eje.Normalize();
                var lado = Vector3.Cross(Vector3.up, eje);
                var medio = (rampa.abajo + rampa.arriba) * 0.5f; medio.y = (rampa.alturaInicial + rampa.alturaDelFinal) * 0.5f;
                var m = yo.Motor; float multAntes = m.SpeedMultiplier; m.SpeedMultiplier = 3.4f;
                Time.captureDeltaTime = 0.15f;
                foreach (bool arreglo in new[] { false, true })
                {
                    RampaCaminable.MargenPorVelocidad = arreglo;
                    var ini = rampa.abajo + eje * 0.5f;
                    yo.transform.position = new Vector3(ini.x, rampa.alturaInicial + SoldierMotor.PivoteSobrePiso, ini.z);
                    yield return null; yield return null;
                    for (int f = 0; f < 3; f++) { m.Move(eje, Time.deltaTime); yield return null; }
                    var sp = yo.transform.position; foreach (var x in FotoCamara(arreglo ? "v10_418_despues.jpg" : "v10_418_antes.jpg", sp - eje * 5f + lado * 2.5f + Vector3.up * 2.6f, sp + eje * 3f + Vector3.up * 0.6f, 60f, 1280, 720)) yield return x;
                }
                RampaCaminable.MargenPorVelocidad = true; Time.captureDeltaTime = 0f; m.SpeedMultiplier = multAntes;
            }

            // 419 y 420: las celdas del 2.o piso
            var dd = new OperacionDirector[1];
            foreach (var x in ArrancarMansion(dd)) yield return x;
            d = dd[0];
            yo = PlayerInputDriver.Activo != null ? PlayerInputDriver.Activo.Brain.Current : null;
            typeof(OperacionDirector).GetMethod("EntrarSegundoPiso", NoPublico).Invoke(d, null);
            float tEspera = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tEspera < 40f && (PlayerInputDriver.Activo == null || PlayerInputDriver.Activo.Brain.Current == null)) yield return null;
            foreach (var x in Esperar(2f)) yield return x;
            yo = PlayerInputDriver.Activo != null ? PlayerInputDriver.Activo.Brain.Current : yo;
            if (yo != null) yo.transform.position = G.Centro(2) + new Vector3(0f, SoldierMotor.PivoteSobrePiso, -6f);
            foreach (var x in Esperar(1.5f)) yield return x;
            var term = d.TerminalDeCeldas;
            if (term != null)
            {
                var pant = term.Find("Pantalla");
                var cam = pant.position + term.forward * 3.4f + Vector3.up * 0.4f;
                foreach (var x in FotoCamara("v10_420_despues.jpg", cam, pant.position + term.forward * 0.3f, 60f, 1280, 720)) yield return x;
            }
            Soldier preso = null; foreach (var s in ActorRegistry.All) if (s != null && s.name.StartsWith("Prisionero_")) { preso = s; break; }
            Soldier enemigo = null; foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Enemy && s.Health != null && s.Health.IsAlive && s.Brain != null) { enemigo = s; break; }
            if (preso != null && enemigo != null)
            {
                enemigo.gameObject.SetActive(true);
                enemigo.transform.position = preso.transform.position + new Vector3(0f, 0f, -4f);
                foreach (var x in Esperar(6f)) yield return x;
                var cp = preso.transform.position;
                foreach (var x in FotoCamara("v10_419_despues.jpg", cp + new Vector3(3.5f, 2.2f, -6.5f), cp + Vector3.up * 1f, 55f, 1280, 720)) yield return x;
            }

            // 421: cadaveres colgados en el aire (misma mansion)
            typeof(OperacionDirector).GetMethod("EntrarAzotea", NoPublico).Invoke(d, null);
            foreach (var x in Esperar(1f)) yield return x;
            int colgados = 0;
            foreach (var s in ActorRegistry.All)
            {
                if (colgados >= 3) break;
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || s.Brain == null) continue;
                s.gameObject.SetActive(true); s.transform.position = new Vector3(318f + colgados * 2f, 26.5f, 268f); s.Health.TakeDamage(99999, -1); colgados++;
            }
            foreach (var x in Esperar(12f)) yield return x;
            foreach (var x in FotoCamara("v10_421_despues.jpg", new Vector3(314f, 24f, 258f), new Vector3(320f, 20.5f, 268f), 60f, 1280, 720)) yield return x;
            Fin("OK fotos v10 generadas");
        }
    }
}
