using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Presentation;

namespace SP.EditorTools
{
    // Checks de los bugs #368-#395 (2026-11-08).
    public static partial class ChecksBugs065
    {
        // Diagnostico generico: lista los renderers altos cerca de un punto con el estado de su collider (para bugs de "lo atraviesa").
        static string RenderersCerca(Vector3 p, float radio)
        {
            var sb = new StringBuilder();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || (r.bounds.center - p).magnitude > radio || r.bounds.size.y < 1.2f) continue;
                var c = r.GetComponentInParent<Collider>();
                sb.AppendLine((r.transform.parent != null ? r.transform.parent.name + "/" : "") + r.name + " c=" + r.bounds.center + " s=" + r.bounds.size
                    + " col=" + (c == null ? "NO" : c.GetType().Name + " trig=" + c.isTrigger + " en=" + c.enabled + " sz=" + c.bounds.size + " obj=" + c.name));
            }
            return sb.ToString();
        }

        // Resumen por nombre de lo que hay cerca de un punto: renderers (agrupados) y luces (tipo, intensidad, rango).
        static string ResumenCerca(string titulo, Vector3 p, float radio, float radioLuces)
        {
            var sb = new StringBuilder("== " + titulo + " " + p + "\n");
            var grupos = new System.Collections.Generic.SortedDictionary<string, int>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || !r.enabled || (r.bounds.center - p).magnitude > radio) continue;
                string k = (r.transform.parent != null ? r.transform.parent.name + "/" : "") + r.name + (r.sharedMaterial != null ? " [" + r.sharedMaterial.name + "]" : "");
                grupos.TryGetValue(k, out int n); grupos[k] = n + 1;
            }
            foreach (var kv in grupos) sb.AppendLine("  R " + kv.Key + " x" + kv.Value);
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l == null || !l.enabled || (l.transform.position - p).magnitude > radioLuces) continue;
                sb.AppendLine("  L " + (l.transform.parent != null ? l.transform.parent.name + "/" : "") + l.name + " " + l.type + " int=" + l.intensity.ToString("0.0") + " rng=" + l.range.ToString("0") + " " + l.transform.position);
            }
            return sb.ToString();
        }

        static IEnumerator Bug375d()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            var t = new StringBuilder();
            t.Append(ResumenCerca("bug392 bandera azotea", new Vector3(328.2f, 20.3f, 261.3f), 10f, 14f));
            t.Append(ResumenCerca("bug383 fuente", new Vector3(318.9f, 0.8f, 217.6f), 12f, 14f));
            t.Append(ResumenCerca("bug387 luz mansion", new Vector3(308.7f, 13.8f, 270.2f), 6f, 10f));
            t.Append(ResumenCerca("bug375 luminarias", new Vector3(-155.8f, 0.7f, 280.2f), 14f, 18f));
            t.Append(ResumenCerca("bug382 ventanas", new Vector3(321.3f, 0.8f, 225.3f), 14f, 18f));
            File.WriteAllText("Temp/diag_375.txt", t.ToString());
            Fin("OK diag en Temp/diag_375.txt");
        }

        // ---------------------------------------------------------------- 368/393 modo dios: mecha de 3 s y carrera x4
        static string Bug368a()
        {
            var sb = new StringBuilder(); bool ok = true;
            W8Ok(ref ok, ModoDios.SegundosDeMechaEnModoDios == 3f, sb, "mecha del modo dios = 3 s");
            W8Ok(ref ok, ModoDios.FactorDeCarreraDelModoDios == 4f, sb, "carrera del modo dios x4 (el doble de x2)");
            return (ok ? "OK " : "FALLO ") + sb;
        }

        // ---------------------------------------------------------------- 369/375/392/382/387 ajustes de nivel
        static IEnumerator Bug369a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(3);
            foreach (var x in Esperar(4.5f)) yield return x;   // el vigilante reaplica cada 3 s
            int sinCollider = 0, total = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.parent != null && t.parent.name == "PuertasDeAcceso" && (t.name.StartsWith("Hoja_") || t.name.StartsWith("Jamba_")) && !t.name.EndsWith("_Franja"))
                { total++; if (t.GetComponent<Collider>() == null) sinCollider++; }
            W8Ok(ref ok, total > 0 && sinCollider == 0, sb, $"hojas y jambas de las puertas de acceso con collider ({total - sinCollider}/{total})");
            int banderas = 0, faroles = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (!t.gameObject.activeSelf) continue;
                if (t.name == "Bandera_Gala" || t.name == "Mastil_Gala") banderas++;
                if (t.name == "Farol_Luz" || t.name == "Farolillo" || t.name == "Luz_Hada") faroles++;
            }
            W8Ok(ref ok, banderas == 0, sb, $"bandera de gala quitada (activos: {banderas})");
            W8Ok(ref ok, faroles == 0, sb, $"luminarias chicas quitadas (activas: {faroles})");
            W8Ok(ref ok, AjustesDeNivelNov08.LucesAtenuadas > 0, sb, $"luces decorativas atenuadas ({AjustesDeNivelNov08.LucesAtenuadas}, x{AjustesDeNivelNov08.FactorDeLuces})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 385/389 pesado: vida x12 y arma segun movimiento
        static IEnumerator Bug389a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            ModoDios.Poner(true);
            var yo = Poseido();
            Soldier molde = null;
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.GetComponent<EnemigoPesado>() == null && !a.name.StartsWith("Pesado")) { molde = a; break; }
            if (yo == null || molde == null) { Fin("FALLO faltan jugador/molde"); yield break; }
            var go = Object.Instantiate(molde.gameObject, yo.transform.position + yo.transform.forward * 25f + Vector3.up * 0.1f, Quaternion.LookRotation(-yo.transform.forward), molde.transform.parent); go.SetActive(true);
            var s = go.GetComponent<Soldier>(); int vidaBase = s.Health.MaxHealth; var p = EnemigoPesado.Aplicar(s);
            foreach (var x in Esperar(1f)) yield return x;
            W8Ok(ref ok, s.Health.MaxHealth == vidaBase * 12, sb, $"vida x12 ({vidaBase} -> {s.Health.MaxHealth})");
            W8Ok(ref ok, s.Weapon.Loadout.Contains(WeaponKind.Minigun) && s.Weapon.Loadout.Contains(WeaponKind.Rocket), sb, "lleva minigun y obuses");
            W8Ok(ref ok, p.Estado == EnemigoPesado.Fase.Patrulla ? s.Weapon.CurrentWeaponKind == WeaponKind.Minigun : true, sb, $"en marcha usa la minigun ({s.Weapon.CurrentWeaponKind})");
            typeof(EnemigoPesado).GetProperty("Estado").SetValue(p, EnemigoPesado.Fase.Frenesi);   // detenido: explosivos
            foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, s.Weapon.CurrentWeaponKind == WeaponKind.Rocket, sb, $"detenido usa explosivos ({s.Weapon.CurrentWeaponKind})");
            Object.Destroy(go);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 394 un enemigo no hiere a otro enemigo
        static IEnumerator Bug394a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            Soldier a = null, b = null;
            foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Enemy && s.Health != null && s.Health.IsAlive) { if (a == null) a = s; else if (b == null) { b = s; break; } }
            if (a == null || b == null) { Fin("FALLO faltan enemigos"); yield break; }
            int antes = b.Health.Current;
            b.Health.TakeDamage(10, a.Id); b.Health.TakeDamage(10, -3); b.Health.TakeDamage(10, -2);
            W8Ok(ref ok, b.Health.Current == antes, sb, $"el fuego enemigo no le saca vida a otro enemigo ({antes} -> {b.Health.Current})");
            b.Health.TakeDamage(10, -1);
            W8Ok(ref ok, b.Health.Current == antes - 10, sb, $"lo que no tiene duenio (barriles) si lo hiere ({b.Health.Current})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 370/371 Huir: no se puede retroceder (sin colliders)
        static IEnumerator Bug370a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var d = Object.FindFirstObjectByType<OperacionDirector>();
            var yo = Poseido();
            if (d == null || yo == null) { Fin("FALLO faltan director/jugador"); yield break; }
            var p0 = yo.transform.position;
            yo.transform.position = p0 + new Vector3(0f, 0f, 30f); Physics.SyncTransforms();
            foreach (var x in Esperar(1.5f)) yield return x;
            float linea = d.LineaDeNoRetrocesoZ;
            yo.transform.position = new Vector3(p0.x, p0.y, linea - 12f); Physics.SyncTransforms();
            foreach (var x in Esperar(1.5f)) yield return x;
            sb.Append($"[diag: fase={d.Fase} sub={d.Subfase} tanque={d.EnTanque} p0z={p0.z:0.0}] ");
            W8Ok(ref ok, yo.transform.position.z >= linea - 0.5f, sb, $"al retroceder mas de {OperacionDirector.RetrocesoMaximoDeHuida} m se lo devuelve (z={yo.transform.position.z:0.0}, linea={linea:0.0})");
            W8Ok(ref ok, d.RetrocesosImpedidos > 0, sb, $"se contaron los retrocesos impedidos ({d.RetrocesosImpedidos})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 372/390 heli de descarga: derribable, sonido propio, explota al chocar
        static IEnumerator Bug372a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            var yo = Poseido();
            var h = HeliDeDescarga.Soltar(yo.transform.position + yo.transform.forward * 30f);
            W8Ok(ref ok, h != null, sb, "se creo el helicoptero de descarga");
            if (h == null) { Fin("FALLO " + sb); yield break; }
            W8Ok(ref ok, h.Vehiculo != null && h.Vehiculo.Health != null && h.Vehiculo.Health.MaxHealth == HeliDeDescarga.VidaDelHeli, sb, $"tiene vida propia ({(h.Vehiculo != null && h.Vehiculo.Health != null ? h.Vehiculo.Health.MaxHealth : -1)})");
            W8Ok(ref ok, h.TieneSonidoPropio, sb, "su rotor suena distinto (clip propio)");
            float t0 = Time.time; while (Time.time - t0 < 30f && h.Estado != HeliDeDescarga.Fase.Suspendido) yield return null;
            W8Ok(ref ok, h.Estado == HeliDeDescarga.Fase.Suspendido, sb, $"llega y queda suspendido ({h.Estado})");
            int exp0 = HeliDeDescarga.Explosiones;
            if (h.Vehiculo != null) { h.Vehiculo.Health.Invulnerable = false; h.Vehiculo.TakeDamage(9999, yo.Id); }
            sb.Append($"[diag: destruido={h.Vehiculo != null && h.Vehiculo.IsDestroyed}, vida={(h.Vehiculo != null ? h.Vehiculo.Health.Current : -1)}] ");
            foreach (var x in Esperar(0.3f)) yield return x;
            W8Ok(ref ok, h.Estado == HeliDeDescarga.Fase.Cayendo || h.Estado == HeliDeDescarga.Fase.Muerto, sb, $"con el danio recibido se lo derriba ({h.Estado})");
            t0 = Time.time; while (Time.time - t0 < 8f && HeliDeDescarga.Explosiones == exp0) yield return null;
            W8Ok(ref ok, HeliDeDescarga.Explosiones == exp0 + 1, sb, $"explota al llegar abajo (explosiones +{HeliDeDescarga.Explosiones - exp0})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 396 columnas nuevas del reporte (plan de Opus): contexto, CSV local, cultura, clasificacion
        static IEnumerator Bug396a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(3f)) yield return x;
            var actual = System.Globalization.CultureInfo.CurrentCulture;
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("es-AR");
            var e = SesionLog.Instancia.Capturar("prueba, con \"comillas\"\ny salto de linea");
            var cols = ContextoDeBug.Columnas(e, 3);
            System.Globalization.CultureInfo.CurrentCulture = actual;
            string V(string k) { foreach (var kv in cols) if (kv.Key == k) return kv.Value; return null; }
            W8Ok(ref ok, !string.IsNullOrEmpty(V("Bug_id")) && V("Bug_id").Contains("-"), sb, $"Bug_id='{V("Bug_id")}'");
            W8Ok(ref ok, !string.IsNullOrEmpty(V("Build")), sb, $"Build='{V("Build")}'");
            W8Ok(ref ok, !string.IsNullOrEmpty(V("Estado_control")), sb, $"Estado_control='{V("Estado_control")}'");
            W8Ok(ref ok, V("Ajustes") != null && V("Ajustes").Contains("ts=") && V("Ajustes").Contains("dios="), sb, $"Ajustes='{V("Ajustes")}'");
            W8Ok(ref ok, float.TryParse(V("FPS_5s"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fps) && fps > 0f, sb, $"FPS_5s={V("FPS_5s")}");
            W8Ok(ref ok, !string.IsNullOrEmpty(V("Objeto_mirado")) && V("Entorno") != null && V("Entorno").StartsWith("nav="), sb, $"mira/entorno ('{V("Objeto_mirado")}' / '{V("Entorno")}')");
            bool comaDecimal = false; foreach (var k in e.mision) if (System.Text.RegularExpressions.Regex.IsMatch(k.v ?? "", @"\d,\d+,\d")) comaDecimal = true;
            W8Ok(ref ok, !comaDecimal, sb, "la mision no sale con coma decimal en es-AR (H2)");
            // CSV: dos filas, una con comillas y saltos de linea
            string carpeta = Path.Combine("Temp", "csv_prueba"); if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true);
            ContextoDeBug.AgregarFilaCsv(carpeta, ContextoDeBug.FilaLocal(e, e.nota, "bug_999.json", "bug_999.png", cols));
            ContextoDeBug.AgregarFilaCsv(carpeta, ContextoDeBug.FilaLocal(e, "otra nota", "bug_998.json", "bug_998.png", cols));
            var bytes = File.ReadAllBytes(Path.Combine(carpeta, "bugs_columnas.csv"));
            W8Ok(ref ok, bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, sb, "el CSV lleva BOM UTF-8");
            var filas = ParsearCsv(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
            W8Ok(ref ok, filas.Count == 3, sb, $"3 filas logicas (cabecera + 2) pese a los saltos de linea ({filas.Count})");
            bool mismoAncho = filas.Count == 3 && filas[0].Count == filas[1].Count && filas[1].Count == filas[2].Count;
            W8Ok(ref ok, mismoAncho, sb, $"todas las filas tienen {filas[0].Count} columnas");
            int iCom = filas[0].IndexOf("Comentarios");
            W8Ok(ref ok, iCom >= 0 && filas.Count > 1 && filas[1][iCom] == "prueba, con \"comillas\"\ny salto de linea", sb, "la nota con comas, comillas y salto de linea vuelve idéntica");
            // clasificacion automatica (fixtures reales)
            W8Ok(ref ok, ContextoDeBug.Clasificar("Se trabo no avanza") == "Defecto·Colision/Nav·P0", sb, "#395 -> " + ContextoDeBug.Clasificar("Se trabo no avanza"));
            W8Ok(ref ok, ContextoDeBug.Clasificar("En el modo dios nose q velocidad tiena ahora pero quiero q sea el doble de la de ahora").StartsWith("Balance"), sb, "#393 -> " + ContextoDeBug.Clasificar("En el modo dios nose q velocidad tiena ahora pero quiero q sea el doble de la de ahora"));
            W8Ok(ref ok, ContextoDeBug.Clasificar("Quita esta bandera") .StartsWith("Pedido·Luces/Visual"), sb, "#392 -> " + ContextoDeBug.Clasificar("Quita esta bandera"));
            W8Ok(ref ok, ContextoDeBug.Clasificar("Creo q los enemigos se disparan entre ellos revisa q pasa y solucionalo") == "Defecto·IA enemigos·P1", sb, "#394 -> " + ContextoDeBug.Clasificar("Creo q los enemigos se disparan entre ellos revisa q pasa y solucionalo"));
            Directory.Delete(carpeta, true);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // Parser RFC 4180 minimo para verificar el CSV.
        static System.Collections.Generic.List<System.Collections.Generic.List<string>> ParsearCsv(string t)
        {
            var filas = new System.Collections.Generic.List<System.Collections.Generic.List<string>>(); var fila = new System.Collections.Generic.List<string>(); var cel = new StringBuilder(); bool q = false;
            for (int i = 0; i < t.Length; i++)
            {
                char c = t[i];
                if (q) { if (c == '"') { if (i + 1 < t.Length && t[i + 1] == '"') { cel.Append('"'); i++; } else q = false; } else cel.Append(c); }
                else if (c == '"') q = true;
                else if (c == ',') { fila.Add(cel.ToString()); cel.Clear(); }
                else if (c == '\r') { }
                else if (c == '\n') { fila.Add(cel.ToString()); cel.Clear(); filas.Add(fila); fila = new System.Collections.Generic.List<string>(); }
                else cel.Append(c);
            }
            if (cel.Length > 0 || fila.Count > 0) { fila.Add(cel.ToString()); filas.Add(fila); }
            return filas;
        }

        // ---------------------------------------------------------------- 374/377/380 HUD simple del tanque, minimapa visible y colores del minimapa
        static IEnumerator Bug374a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4);
            foreach (var x in Esperar(2f)) yield return x;
            SP.Vehicles.Vehicle v = null;
            foreach (var t in SP.Vehicles.Vehicle.Todos) if (t != null && t.Health != null && t.Bando != TeamId.Enemy && !t.IsDestroyed) { v = t; if (t.isActiveAndEnabled) break; }
            if (v == null)   // en Huir el Tanque_Fuga aun es "enemigo" hasta que se sube el jugador: se lo marca propio para probar el HUD
                foreach (var t in SP.Vehicles.Vehicle.Todos) if (t != null && t.Health != null && !t.IsDestroyed) { t.MarcarBando(TeamId.Player); v = t; break; }
            if (v == null)
            {
                string det = ""; foreach (var t in SP.Vehicles.Vehicle.Todos) if (t != null) det += $"[{t.name} bando={t.Bando} vida={(t.Health != null ? "si" : "null")} destruido={t.IsDestroyed} activo={t.isActiveAndEnabled}]";
                Fin($"FALLO no hay vehiculo propio (vehiculos={SP.Vehicles.Vehicle.Todos.Count}) {det}"); yield break;
            }
            SP.UI.CapasDeHud.ModoTanque(true);
            SP.UI.HudDeTanqueSimple.Mostrar(v);
            foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, SP.UI.HudDeTanqueSimple.Visible, sb, "la barra simple del tanque se ve en modo tanque");
            float esperado = v.Health.Current / (float)v.Health.MaxHealth;
            W8Ok(ref ok, Mathf.Abs(SP.UI.HudDeTanqueSimple.Instancia.FraccionDeVida - esperado) < 0.02f, sb, $"su barra marca la vida del tanque ({SP.UI.HudDeTanqueSimple.Instancia.FraccionDeVida:0.00} vs {esperado:0.00})");
            var cv = SP.UI.CapasDeHud.Instancia != null ? SP.UI.CapasDeHud.Instancia.GetComponentInParent<Canvas>() : null;
            float alfaRoster = -1f, alfaMini = -1f;
            if (cv != null) foreach (Transform h in cv.transform)
            {
                var g = h.GetComponent<CanvasGroup>();
                if (h.name == "Roster") alfaRoster = g != null ? g.alpha : 1f;
                if (h.name == "MinimapBorder") alfaMini = g != null ? g.alpha : 1f;
            }
            W8Ok(ref ok, alfaRoster < 0f || alfaRoster == 0f, sb, $"las tarjetas de aliados se ocultan en el tanque (alfa {alfaRoster})");
            W8Ok(ref ok, alfaMini < 0f || alfaMini > 0.9f, sb, $"el minimapa se mantiene visible (alfa {alfaMini})");
            SP.UI.CapasDeHud.ModoTanque(false);
            foreach (var x in Esperar(0.3f)) yield return x;
            W8Ok(ref ok, !SP.UI.HudDeTanqueSimple.Visible, sb, "al bajar del tanque la barra simple se apaga");
            W8Ok(ref ok, MinimapIcon.ObstacleMinimapColor.maxColorComponent < 0.5f && MinimapIcon.ObstacleMinimapColor.a < 0.8f, sb, "obstaculos del minimapa: gris oscuro y translucido (#380)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 384/386 aliados atrapados en otro piso reaparecen junto al jugador
        static IEnumerator Bug384a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            var yo = Poseido();
            Soldier aliado = null;
            foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Player && s != yo && s.Role != RoleType.Civilian && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy && s.Brain != null) { aliado = s; break; }
            if (yo == null || aliado == null) { Fin("FALLO faltan jugador/aliado"); yield break; }
            var d = Object.FindFirstObjectByType<OperacionDirector>();
            int antes = d.AliadosReubicadosDePiso;
            // el aliado queda 12 m por debajo del jugador (otro piso): a los 8 s deberia reaparecer a su lado
            aliado.transform.position = yo.transform.position + new Vector3(30f, -12f, 0f); Physics.SyncTransforms();
            float t0 = Time.time; while (Time.time - t0 < 25f && d.AliadosReubicadosDePiso == antes) yield return null;
            W8Ok(ref ok, d.AliadosReubicadosDePiso > antes, sb, $"el aliado trabado reaparece junto al jugador (+{d.AliadosReubicadosDePiso - antes})");
            Fin((ok ? "OK " : "FALLO ") + sb + $"[distancia final {Vector3.Distance(aliado.transform.position, yo.transform.position):0.0} m]");
        }

        // ---------------------------------------------------------------- 378 tanque enemigo: otro color, 3x vida, obus visible
        static IEnumerator Bug378a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4);
            ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            OperacionAuto auto = null; float t0 = Time.time;
            var dir = Object.FindFirstObjectByType<OperacionDirector>();
            // el director las manda con el tanque en marcha; para el test se pide una a mano
            typeof(OperacionDirector).GetMethod("CrearAuto", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(dir, null);
            foreach (var x in Esperar(0.5f)) yield return x;
            while (auto == null && Time.time - t0 < 60f)
            {
                foreach (var a in Object.FindObjectsByType<OperacionAuto>(FindObjectsInactive.Exclude)) if (a != null && !a.Muerto && !a.EsTanque && a.objetivo != null) { auto = a; break; }
                if (auto == null) yield return null;
            }
            if (auto == null) { Fin("FALLO no aparecio ninguna camioneta enemiga en 60 s"); yield break; }
            int vidaAntes = auto.Vehiculo.Health.MaxHealth; int danoAntes = auto.danoPorBala; var escalaAntes = auto.transform.localScale.x;
            var rend = auto.GetComponentInChildren<MeshRenderer>(); Color antes = rend != null && rend.sharedMaterial.HasProperty("_BaseColor") ? rend.sharedMaterial.GetColor("_BaseColor") : Color.clear;
            auto.ConvertirEnTanque();
            foreach (var x in Esperar(0.3f)) yield return x;
            W8Ok(ref ok, auto.EsTanque && auto.name.StartsWith("Tanque_Enemigo_"), sb, $"se convierte en tanque ({auto.name})");
            W8Ok(ref ok, auto.Vehiculo.Health.MaxHealth == vidaAntes * 3, sb, $"3x vida ({vidaAntes} -> {auto.Vehiculo.Health.MaxHealth})");
            W8Ok(ref ok, auto.danoPorBala == danoAntes * 2, sb, $"rafaga x2 ({danoAntes} -> {auto.danoPorBala})");
            W8Ok(ref ok, auto.transform.localScale.x > escalaAntes * 1.2f, sb, "mas grande");
            Color despues = rend != null && rend.material.HasProperty("_BaseColor") ? rend.material.GetColor("_BaseColor") : Color.clear;
            W8Ok(ref ok, rend == null || (despues.r > despues.g + 0.1f && despues != antes), sb, $"otro color: rojizo ({antes} -> {despues})");
            t0 = Time.time; while (auto != null && !auto.Muerto && auto.Obuses == 0 && Time.time - t0 < 25f) yield return null;
            W8Ok(ref ok, auto != null && (auto.Obuses > 0 || auto.Muerto), sb, $"dispara su obus visible (obuses={(auto != null ? auto.Obuses : -1)}, rafagas={(auto != null ? auto.Disparos : -1)})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 381 francotirador: laser de aviso y tiro
        static IEnumerator Bug381a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            ModoDios.Poner(true);
            var yo = Poseido();
            Soldier molde = null;
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.GetComponent<EnemigoPesado>() == null) { molde = a; break; }
            if (yo == null || molde == null) { Fin("FALLO faltan jugador/molde"); yield break; }
            var go = Object.Instantiate(molde.gameObject, yo.transform.position + yo.transform.forward * 35f + Vector3.up * 0.1f, Quaternion.LookRotation(-yo.transform.forward), molde.transform.parent); go.SetActive(true);
            var s = go.GetComponent<Soldier>(); var f = EnemigoFrancotirador.Aplicar(s);
            W8Ok(ref ok, f != null && s.name.StartsWith("Francotirador_"), sb, "se crea el francotirador");
            bool vioLaser = false; float t0 = Time.time;
            while (Time.time - t0 < 20f && f.Tiros == 0) { if (f.Laser != null && f.Laser.enabled) vioLaser = true; yield return null; }
            W8Ok(ref ok, vioLaser, sb, "marca al blanco con el laser rojo antes de tirar");
            W8Ok(ref ok, f.Tiros > 0, sb, $"dispara ({f.Tiros} tiros en {Time.time - t0:0.0} s; ultimo estado: {f.Razon}; distancia {Vector3.Distance(go.transform.position, yo.transform.position):0} m)");
            Object.Destroy(go);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 387 terminal de celdas en una pared
        static IEnumerator Bug387a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            var d = Object.FindFirstObjectByType<OperacionDirector>();
            var jug = Poseido();   // al 2.o piso, frente a las celdas (donde estaba el reporte #387): ahi existen los muros del piso
            if (jug != null) { jug.transform.position = new Vector3(308.7f, 13.9f, 270.2f); Physics.SyncTransforms(); }
            foreach (var x in Esperar(2f)) yield return x;
            if (d.TerminalDeCeldas == null) typeof(OperacionDirector).GetMethod("CrearTerminalDeCeldas", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(d, null);
            Physics.SyncTransforms();
            var tt = d.TerminalDeCeldas;
            W8Ok(ref ok, tt != null, sb, "la terminal existe");
            if (tt != null)
            {
                W8Ok(ref ok, d.TerminalEnPared, sb, "se monto contra una pared [" + d.DiagnosticoDeParedes + " pos=" + tt.position + "]");
                var o = tt.position + Vector3.up * 1.4f;
                bool pegada = Physics.Raycast(o, -tt.forward, out var h, 0.6f, ~0, QueryTriggerInteraction.Ignore) && !h.collider.transform.IsChildOf(tt);
                W8Ok(ref ok, pegada || !d.TerminalEnPared, sb, "su fondo toca la pared (<0,6 m)");
                var panel = tt.Find("Mueble"); W8Ok(ref ok, panel != null && panel.localScale.y > 1f, sb, "panel de pared (no mueble en el piso)");
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 383 chorros de la fuente alineados
        static IEnumerator Bug383a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(4.5f)) yield return x;
            int n = 0, malRadio = 0, malAngulo = 0, gotas = 0, gotasMal = 0;
            var eje = new Vector2(322f, 215.5f);
            var chorros = new System.Collections.Generic.List<Transform>(); var gotasT = new System.Collections.Generic.List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)) { if (t.name == "Fuente_Chorro") chorros.Add(t); else if (t.name == "Fuente_Gota") gotasT.Add(t); }
            foreach (var c in chorros)
            {
                n++; var v = new Vector2(c.position.x, c.position.z) - eje;
                if (Mathf.Abs(v.magnitude - AjustesDeNivelNov08.RadioDeChorros) > 0.02f) malRadio++;
                float a = Mathf.Atan2(v.y, v.x) / (Mathf.PI / 4f); if (Mathf.Abs(a - Mathf.Round(a)) > 0.01f) malAngulo++;
            }
            foreach (var g in gotasT)
            {
                gotas++; bool hay = false;
                foreach (var c in chorros) if (Mathf.Abs(c.position.x - g.position.x) < 0.02f && Mathf.Abs(c.position.z - g.position.z) < 0.02f) { hay = true; break; }
                if (!hay) gotasMal++;
            }
            W8Ok(ref ok, n == 8, sb, $"8 chorros ({n})");
            W8Ok(ref ok, malRadio == 0, sb, $"todos a {AjustesDeNivelNov08.RadioDeChorros} m del eje ({malRadio} fuera)");
            W8Ok(ref ok, malAngulo == 0, sb, $"a 45 grados exactos ({malAngulo} desalineados)");
            W8Ok(ref ok, gotas == 8 && gotasMal == 0, sb, $"cada gota sobre su chorro ({gotas} gotas, {gotasMal} desplazadas)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 397 fotos "despues" para el atlas antes/despues (los "antes" son las capturas de cada reporte)
        static IEnumerator Bug397a()   // plaza: fuente (#383), ventanas/luces (#382), pesado (#389)
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5); ModoDios.Poner(true);
            foreach (var x in Esperar(4.5f)) yield return x;
            foreach (var x in FotoCamara("v7_383_despues.jpg", new Vector3(327f, 3.4f, 208f), new Vector3(322f, 1.9f, 215.5f), 60f)) yield return x;
            foreach (var x in FotoCamara("v7_382_despues.jpg", new Vector3(321.3f, 3.0f, 219f), new Vector3(321.3f, 5f, 245f), 62f)) yield return x;
            var yo = Poseido(); Soldier molde = null;
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.GetComponent<EnemigoPesado>() == null) { molde = a; break; }
            if (yo != null && molde != null)
            {
                var pos = new Vector3(321f, 0.1f, 232f);
                var go = Object.Instantiate(molde.gameObject, pos, Quaternion.LookRotation(Vector3.back), molde.transform.parent); go.SetActive(true);
                var s = go.GetComponent<Soldier>(); EnemigoPesado.Aplicar(s);
                foreach (var x in Esperar(1.2f)) yield return x;
                foreach (var x in FotoCamara("v7_389_despues.jpg", pos + new Vector3(2.2f, 1.7f, -5.5f), pos + Vector3.up * 1.2f, 50f)) yield return x;
                Object.Destroy(go);
            }
            Fin("OK fotos de la plaza");
        }

        static IEnumerator Bug397b()   // terminal en la pared (#387)
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var d = Object.FindFirstObjectByType<OperacionDirector>(); var jug = Poseido();
            if (jug != null) { jug.transform.position = new Vector3(308.7f, 13.9f, 270.2f); Physics.SyncTransforms(); }
            foreach (var x in Esperar(2f)) yield return x;
            if (d.TerminalDeCeldas == null) typeof(OperacionDirector).GetMethod("CrearTerminalDeCeldas", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(d, null);
            var tt = d.TerminalDeCeldas; if (tt == null) { Fin("FALLO sin terminal"); yield break; }
            foreach (var x in FotoCamara("v7_387_despues.jpg", tt.position + tt.forward * 3.4f + Vector3.up * 1.9f, tt.position + Vector3.up * 1.3f, 62f)) yield return x;
            Fin("OK foto de la terminal");
        }

        static IEnumerator Bug397c()   // bandera quitada de la azotea (#392)
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(6); ModoDios.Poner(true);
            foreach (var x in Esperar(4.5f)) yield return x;
            foreach (var x in FotoCamara("v7_392_despues.jpg", new Vector3(323f, 22.6f, 268f), new Vector3(328.2f, 23.5f, 260f), 62f)) yield return x;
            Fin("OK foto de la azotea");
        }

        static IEnumerator Bug397d()   // tanque enemigo con obus (#378)
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(4); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var dir = Object.FindFirstObjectByType<OperacionDirector>();
            typeof(OperacionDirector).GetMethod("CrearAuto", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(dir, null);
            foreach (var x in Esperar(0.5f)) yield return x;
            OperacionAuto auto = null;
            foreach (var a in Object.FindObjectsByType<OperacionAuto>(FindObjectsInactive.Exclude)) if (a != null && !a.Muerto && a.objetivo != null) { auto = a; break; }
            if (auto == null) { Fin("FALLO sin camioneta"); yield break; }
            auto.ConvertirEnTanque();
            float t0 = Time.time; while (auto != null && auto.Obuses == 0 && Time.time - t0 < 25f) yield return null;
            if (auto == null) { Fin("FALLO la camioneta desaparecio"); yield break; }
            foreach (var x in FotoCamara("v7_378_despues.jpg", auto.transform.position + auto.transform.right * 8f + Vector3.up * 3.6f - auto.transform.forward * 3f, auto.transform.position + Vector3.up * 1.4f, 55f)) yield return x;
            Fin("OK foto del tanque enemigo");
        }

        static IEnumerator Bug397e()   // francotirador con laser (#381)
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var yo = Poseido(); Soldier molde = null;
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.GetComponent<EnemigoPesado>() == null) { molde = a; break; }
            if (yo == null || molde == null) { Fin("FALLO faltan jugador/molde"); yield break; }
            var go = Object.Instantiate(molde.gameObject, yo.transform.position + yo.transform.forward * 30f + Vector3.up * 0.1f, Quaternion.LookRotation(-yo.transform.forward), molde.transform.parent); go.SetActive(true);
            var f = EnemigoFrancotirador.Aplicar(go.GetComponent<Soldier>());
            float t0 = Time.time; while (Time.time - t0 < 15f && !(f.Laser != null && f.Laser.enabled && Time.time - t0 > 0.1f)) yield return null;
            if (f.Laser == null || !f.Laser.enabled) { Fin("FALLO el laser no se encendio"); yield break; }
            var p0 = f.Laser.GetPosition(0); var p1 = f.Laser.GetPosition(1); var dl = (p1 - p0).normalized; var lado = Vector3.Cross(Vector3.up, dl);
            foreach (var x in FotoCamara("v7_381_despues.jpg", p0 - dl * 3.5f + lado * 1.6f + Vector3.up * 0.7f, p0 + dl * 14f, 58f)) yield return x;
            Object.Destroy(go);
            Fin("OK foto del francotirador");
        }

        // ---------------------------------------------------------------- 373/379 la huida: sin tiron al subir y el viaje se cierra al llegar al final de la ruta
        static IEnumerator Bug379a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var d = Object.FindFirstObjectByType<OperacionDirector>();
            const System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            d.Embarcar();
            float t0 = Time.time; while (Time.time - t0 < 8f && d.Subfase != (int)SubfaseHuida.Carrera) yield return null;
            W8Ok(ref ok, d.Subfase == (int)SubfaseHuida.Carrera, sb, $"arranca la carrera (subfase {d.Subfase})");
            int cuadros = 0; while (cuadros < 400 && !d.RutaDespejadaPorLotes) { cuadros++; yield return null; }
            W8Ok(ref ok, d.RutaDespejadaPorLotes, sb, $"#373: la ruta se despeja por lotes, sin tiron al subir ({cuadros} cuadros, {d.PropsDespejadosDeLaRuta} props)");
            t0 = Time.time; while (Time.time - t0 < 12f && d.Reloj <= 5.2f) yield return null;
            var ruta = d.rutaDelTanque;
            var tanque = d.tanque;
            var fin = ruta[ruta.Length - 1].position;
            typeof(OperacionDirector).GetField("indiceRuta", bf).SetValue(d, ruta.Length - 1);
            var rb = tanque.GetComponent<Rigidbody>();
            var pos = new Vector3(fin.x - 3f, tanque.transform.position.y, fin.z);
            if (rb != null) { rb.position = pos; rb.linearVelocity = Vector3.zero; }
            tanque.transform.position = pos; Physics.SyncTransforms();
            int rescatesAntes = d.RescatesDelTanque;
            t0 = Time.time; while (Time.time - t0 < 6f && d.Subfase != (int)SubfaseHuida.CinematicaFinal) yield return null;
            W8Ok(ref ok, d.Subfase == (int)SubfaseHuida.CinematicaFinal && d.RutaTerminadaAntesDeTiempo, sb, $"#379: al llegar al final de la ruta se cierra el viaje (subfase {d.Subfase})");
            W8Ok(ref ok, d.RescatesDelTanque == rescatesAntes, sb, "sin 'rescates' contra el muro en el ultimo tramo");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 395 aliado parado en la azotea lejos del helicoptero: se lo acerca
        static IEnumerator Bug395a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(6); ModoDios.Poner(true);
            foreach (var x in Esperar(5f)) yield return x;
            var d = Object.FindFirstObjectByType<OperacionDirector>();
            const System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var heli = d.heli;
            var yo = Poseido(); Soldier aliado = null;
            foreach (var s in OperacionDirector.AliadosAEvacuar(true)) if (s != null && s != yo && s.Brain != null && s.gameObject.activeInHierarchy) { aliado = s; break; }
            if (heli == null || aliado == null) { Fin($"FALLO faltan heli/aliado (heli={(heli != null)}, aliado={(aliado != null)})"); yield break; }
            // el escenario real del #395: el heli posado en la azotea (y 19,5) y el aliado parado en el techo a 24 m
            heli.position = new Vector3(322f, 19.5f, 266f); if (d.heliAterrizaje != null) d.heliAterrizaje.position = heli.position;
            var lejos = heli.position + new Vector3(24f, 0.8f, 0f);
            aliado.transform.position = lejos; Physics.SyncTransforms();
            int antes = d.AliadosAyudadosAlHeli; float t0 = Time.time;
            while (Time.time - t0 < 40f && d.AliadosAyudadosAlHeli == antes) yield return null;
            W8Ok(ref ok, d.AliadosAyudadosAlHeli > antes, sb, $"el aliado parado a 24 m del heli en el techo se acerca (+{d.AliadosAyudadosAlHeli - antes} en {Time.time - t0:0} s) [fase={d.Fase} heli={heli.position} techo={(d.heliAterrizaje != null ? d.heliAterrizaje.position.y : -1f):0.0} aliadoY={aliado.transform.position.y:0.0} activo={aliado.gameObject.activeInHierarchy}]");
            var dist = Vector2.Distance(new Vector2(aliado.transform.position.x, aliado.transform.position.z), new Vector2(heli.position.x, heli.position.z));
            W8Ok(ref ok, dist <= OperacionDirector.RadioDeLaZonaDeSubida, sb, $"queda dentro de la zona de subida ({dist:0.0} m <= {OperacionDirector.RadioDeLaZonaDeSubida})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 398 fotos "despues" de los atascos para el atlas
        static IEnumerator Bug398a()   // #373: el tanque arranca sin tiron; #379: llega al final de la ruta y se cierra el viaje
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(4); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var d = Object.FindFirstObjectByType<OperacionDirector>();
            d.Embarcar();
            float t0 = Time.time; while (Time.time - t0 < 8f && d.Subfase != (int)SubfaseHuida.Carrera) yield return null;
            foreach (var x in Esperar(3f)) yield return x;
            var tq = d.tanque;
            foreach (var x in FotoCamara("v8_373_despues.jpg", tq.transform.position - tq.transform.forward * 9f + Vector3.up * 4.5f, tq.transform.position + tq.transform.forward * 8f + Vector3.up * 1f, 62f)) yield return x;
            t0 = Time.time; while (Time.time - t0 < 12f && d.Reloj <= 5.2f) yield return null;
            var ruta = d.rutaDelTanque; var fin = ruta[ruta.Length - 1].position;
            typeof(OperacionDirector).GetField("indiceRuta", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(d, ruta.Length - 1);
            var rb = tq.GetComponent<Rigidbody>(); var pos = new Vector3(fin.x - 3f, tq.transform.position.y, fin.z);
            if (rb != null) { rb.position = pos; rb.linearVelocity = Vector3.zero; }
            tq.transform.position = pos; Physics.SyncTransforms();
            t0 = Time.time; while (Time.time - t0 < 6f && d.Subfase != (int)SubfaseHuida.CinematicaFinal) yield return null;
            foreach (var x in Esperar(1.5f)) yield return x;
            foreach (var x in FotoCamara("v8_379_despues.jpg", tq.transform.position + tq.transform.right * 9f + Vector3.up * 4f - tq.transform.forward * 4f, tq.transform.position + Vector3.up * 1.4f + tq.transform.forward * 4f, 62f)) yield return x;
            Fin("OK fotos 373/379");
        }

        static IEnumerator Bug398b()   // #395: los aliados parados lejos en la azotea se acercan al helicoptero
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(6); ModoDios.Poner(true);
            foreach (var x in Esperar(5f)) yield return x;
            var d = Object.FindFirstObjectByType<OperacionDirector>(); var heli = d.heli;
            heli.position = new Vector3(322f, 19.5f, 266f); if (d.heliAterrizaje != null) d.heliAterrizaje.position = heli.position;
            var yo = Poseido(); int n = 0;
            foreach (var s in OperacionDirector.AliadosAEvacuar(true))
            {
                if (s == null || s == yo || s.Brain == null || !s.gameObject.activeInHierarchy) continue;
                s.transform.position = heli.position + new Vector3(18f + n * 2.5f, 0.8f, (n % 2 == 0 ? 6f : -6f)); n++;
            }
            if (yo != null) yo.transform.position = heli.position + new Vector3(-4f, 0.8f, 3f);
            Physics.SyncTransforms();
            int antes = d.AliadosAyudadosAlHeli; float t0 = Time.time;
            while (Time.time - t0 < 40f && d.AliadosAyudadosAlHeli == antes) yield return null;
            foreach (var x in Esperar(2f)) yield return x;
            foreach (var x in FotoCamara("v8_395_despues.jpg", new Vector3(323f, 22.6f, 268f), new Vector3(329f, 20.8f, 272f), 66f)) yield return x;
            Fin($"OK foto del techo (ayudados +{d.AliadosAyudadosAlHeli - antes}, {n} aliados movidos)");
        }

        static IEnumerator Bug369d()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(3);
            foreach (var x in Esperar(2f)) yield return x;
            var txt = RenderersCerca(new Vector3(-3.4f, 1f, -80.6f), 7f);
            File.WriteAllText("Temp/diag_369.txt", txt);
            Fin("OK diag en Temp/diag_369.txt (" + txt.Split('\n').Length + " lineas)");
        }
    }
}
