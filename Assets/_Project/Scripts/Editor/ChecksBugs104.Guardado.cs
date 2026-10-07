using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Mision;
using SP.Operacion;
using SP.Player;
using SP.Presentation;

namespace SP.EditorTools
{
    // P10 (#120): checks del guardado de partida. Todos corren en ModoPrueba (archivo partida_<escena>_prueba.json): la partida real del jugador no se toca.
    //   Bug120s  guardar en Resistir con un estado conocido (vida de Doc, reserva de Kes, miliciano caido, sector caido, operador distinto del poseido),
    //            salir de la escena (Continuar) y verificar que todo vuelve
    //   Bug120t  autoguardado: cada EntrarFase y cada cinematica escriben el archivo con el motivo correcto y fecha nueva
    //   Bug120u  archivo roto o de otra version: no crashea, no hay partida, aviso en el log, el boton del menu de pausa no aparece
    //   Bug120z (SC_MainMenu) el boton CONTINUAR aparece con partida valida y desaparece con una rota
    //   Bug120v  ganar la mision borra el archivo
    //   Bug120w  guardar y continuar en CADA objetivo y subfase de la Operacion
    //   Bug120x  oleadas del mando: las terminadas no vuelven, las que estaban en curso se relanzan
    //   Bug120y  interfaz: boton GUARDAR PARTIDA de la pausa, indicador y bloqueo en cinematica
    public static partial class ChecksBugs065
    {
        static Soldier SoldadoPor(string parte)
        {
            foreach (var a in ActorRegistry.All) if (a != null && a.name.Contains(parte)) return a;
            return null;
        }

        static string NombreDelPoseido()
        {
            var drv = PlayerInputDriver.Activo;
            return drv != null && drv.Brain != null && drv.Brain.Current != null ? drv.Brain.Current.name : "-";
        }

        static string Armas(Soldier s) => s != null && s.Weapon != null ? s.Weapon.CapturarEstadoDeArmas() + "@" + s.Weapon.CurrentAmmo + "g" + s.Weapon.Granadas : "-";

        static string RutaDePartidaDePrueba() => PartidaGuardada.RutaDe(PartidaGuardada.EscenaDeLaOperacion);

        static void LimpiarPartidasDePrueba()
        {
            PartidaGuardada.ModoPrueba = true;
            PartidaGuardada.Borrar();
        }

        // Guarda ("manual"), recarga la escena por el camino de CONTINUAR (sin la pantalla de carga) y espera a que termine la restauracion.
        static IEnumerable GuardarYContinuar(StringBuilder sb, Action<bool> resultado)
        {
            PartidaGuardada.ModoPrueba = true;
            if (!PartidaGuardada.Guardar("manual")) { sb.Append("no se pudo guardar (" + (PartidaGuardada.MotivoDeBloqueo() ?? PartidaGuardada.UltimoAviso) + "); "); resultado(false); yield break; }
            var viejo = OperacionDirector.Instancia;
            if (!PartidaGuardada.ContinuarSinPantallaDeCarga()) { sb.Append("Continuar fallo (" + PartidaGuardada.UltimoAviso + "); "); resultado(false); yield break; }
            float t0 = Time.realtimeSinceStartup;
            while (true)
            {
                yield return null;
                if (Time.realtimeSinceStartup - t0 > 90f) { sb.Append("la escena no volvio a cargar en 90 s; "); resultado(false); yield break; }
                var nuevo = OperacionDirector.Instancia;
                if (nuevo != null && nuevo != viejo && PartidaGuardada.PendienteDeRestaurar == null && !PartidaGuardada.Restaurando) break;
            }
            foreach (var x in Esperar(1.2f)) yield return x;
            resultado(true);
        }

        static bool Cerca(float a, float b, float tol) => Mathf.Abs(a - b) <= tol;

        // ---------------------------------------------------------------- 120s
        static IEnumerator Bug120s()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var dificultadPrevia = Dificultad.Actual;
            LimpiarPartidasDePrueba(); PartidaGuardada.AutoguardadoActivo = false;
            OperacionPrueba.Arrancar(5, 1, 1);   // Resistir, radio tomada y escuadra en sus sectores
            foreach (var x in Esperar(2.5f)) yield return x;
            var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo;
            ModoDios.Poner(false);
            var vega = SoldadoPor("Vega"); var kes = SoldadoPor("Kes"); var doc = SoldadoPor("Doc"); var m3 = SoldadoPor("Miliciano_3"); var m1 = SoldadoPor("Miliciano_1");
            if (vega == null || kes == null || doc == null || m3 == null || m1 == null || d.Subfase != 1) { Fin("FALLO faltan soldados o no esta en la subfase 1: vega=" + (vega != null) + " kes=" + (kes != null) + " doc=" + (doc != null) + " m3=" + (m3 != null) + " subfase=" + d.Subfase); yield break; }
            // Estado conocido: Kes poseida, Vega opera la radio (IA), Doc a 37, reserva de Kes en 3, una sola granada de Kes, MILICIANO 3 caido, sector B caido.
            drv.TryPossess(kes);
            foreach (var x in Esperar(0.4f)) yield return x;
            d.RelevarOperador(vega);
            foreach (var x in Esperar(0.6f)) yield return x;
            doc.Health.RestaurarVida(37);
            kes.Weapon.FijarReservaParaPrueba(kes.Weapon.CurrentWeaponKind, 3);
            kes.Weapon.ConsumirGranada(); kes.Weapon.ConsumirGranada();
            m3.Health.TakeDamage(100000, -1);
            d.sectoresDeDefensa[1].Caido = true;
            d.FijarRelojEfectivoParaPrueba(2f);   // antes del aviso de la primera oleada (no hay nada que relanzar)
            if (drv.Rig != null) drv.Rig.SetMode(SP.CameraSystem.ControlMode.Fps);
            // Los pasos hechos son los que dejo UbicarLaEscuadraSola (0..4). El reloj y los sectores se actualizan cada frame: se saca la foto ya.
            yield return null;
            string armasKes = Armas(kes), armasVega = Armas(vega), armasDoc = Armas(doc);
            int hpDoc = doc.Health.Current; float relojAntes = d.RelojEfectivo;
            string operadorAntes = MandoTactico.Operador != null ? MandoTactico.Operador.name : "-";
            string pasosAntes = string.Join(",", d.PasoHecho);
            string statsAntes = EstadisticasDeMision.Instancia != null ? EstadisticasDeMision.Instancia.Serializar() : "";
            W8Ok(ref ok, operadorAntes == vega.name && NombreDelPoseido() == kes.name && !m3.Health.IsAlive && d.sectoresDeDefensa[1].Caido && kes.Weapon.Granadas == 1, sb, $"estado inicial: operador={operadorAntes} poseido={NombreDelPoseido()} M3 vivo={m3.Health.IsAlive} B caido={d.sectoresDeDefensa[1].Caido} granadas Kes={kes.Weapon.Granadas} hpDoc={hpDoc} reservaKes={kes.Weapon.ReservaActual}");
            bool cargo = false; var salida = new StringBuilder();
            hpDocAlRestaurar = -1;
            System.Action<PartidaGuardadaDatos> alRestaurar = _ => { var dd = SoldadoPor("Doc"); hpDocAlRestaurar = dd != null ? dd.Health.Current : -2; };
            PartidaGuardada.Restaurada += alRestaurar;
            foreach (var x in GuardarYContinuar(salida, v => cargo = v)) yield return x;
            PartidaGuardada.Restaurada -= alRestaurar;
            sb.Append(salida);
            if (!cargo) { Dificultad.Actual = dificultadPrevia; ModoDios.Poner(true); Fin("FALLO " + sb); yield break; }
            // Despues de Continuar: la escena nueva, la fase y todo lo guardado.
            d = OperacionDirector.Instancia; drv = PlayerInputDriver.Activo;
            vega = SoldadoPor("Vega"); kes = SoldadoPor("Kes"); doc = SoldadoPor("Doc"); m3 = SoldadoPor("Miliciano_3"); m1 = SoldadoPor("Miliciano_1");
            W8Ok(ref ok, d.Fase == FaseOperacion.Resistir && d.Subfase == 1 && !CinematicaDeRapel.Activa && !CinematicaDeOperacion.Activa, sb, $"fase={d.Fase}/{d.Subfase} sin cinematicas");
            W8Ok(ref ok, MandoTactico.RadioTomada && MandoTactico.Operador != null && MandoTactico.Operador.name == vega.name, sb, $"radio tomada, operador={(MandoTactico.Operador != null ? MandoTactico.Operador.name : "-")} (esperaba {vega.name})");
            W8Ok(ref ok, NombreDelPoseido() == kes.name && MandoTactico.OperadorEsElJugador == false, sb, $"poseido={NombreDelPoseido()} (esperaba {kes.name}), el operador es IA");
            W8Ok(ref ok, (hpDocAlRestaurar >= hpDoc - 8 && hpDocAlRestaurar <= hpDoc + 30), sb, $"vida de Doc al restaurar {hpDocAlRestaurar} (guardada {hpDoc}; ahora {doc.Health.Current}, regenera sola)");
            W8Ok(ref ok, Armas(kes) == armasKes && kes.Weapon.Granadas == 1, sb, $"armas/reserva de Kes {Armas(kes)} (guardado {armasKes})");
            W8Ok(ref ok, Armas(vega) == armasVega && Armas(doc) == armasDoc, sb, "armas de Vega y Doc iguales");
            W8Ok(ref ok, m3 != null && !m3.Health.IsAlive && m1 != null && m1.Health.IsAlive && MandoTactico.Milicianos.Count == 4, sb, $"milicianos={MandoTactico.Milicianos.Count}, M3 caido={(m3 != null && !m3.Health.IsAlive)}, M1 vivo={(m1 != null && m1.Health.IsAlive)}");
            W8Ok(ref ok, d.sectoresDeDefensa[1].Caido && !d.sectoresDeDefensa[0].Caido, sb, $"sector B caido={d.sectoresDeDefensa[1].Caido}, A caido={d.sectoresDeDefensa[0].Caido}");
            W8Ok(ref ok, Cerca(d.RelojEfectivo, relojAntes, 3f), sb, $"reloj efectivo {d.RelojEfectivo:0.0} (guardado {relojAntes:0.0})");
            W8Ok(ref ok, string.Join(",", d.PasoHecho) == pasosAntes, sb, $"pasos del panel {string.Join(",", d.PasoHecho)} (guardado {pasosAntes})");
            string statsDespues = EstadisticasDeMision.Instancia != null ? EstadisticasDeMision.Instancia.Serializar() : "";
            W8Ok(ref ok, SinTiempo(statsAntes) == SinTiempo(statsDespues), sb, "estadisticas iguales");
            W8Ok(ref ok, !kes.Brain.IsPossessedByPlayer == false && vega.Brain != null && !vega.Brain.IsPossessedByPlayer, sb, "posesion coherente (Kes el jugador, Vega IA)");
            // El operador IA sigue operando (animacion Operar) y el reloj puede correr con el jugador en FPS.
            foreach (var x in Esperar(1.5f)) yield return x;
            W8Ok(ref ok, MandoTactico.EnRadio && (vega.Brain != null && vega.Brain.Quieto), sb, $"el operador IA sigue en la radio y quieto (EnRadio={MandoTactico.EnRadio}, quieto={(vega.Brain != null && vega.Brain.Quieto)})");
            var rig = drv.Rig;
            W8Ok(ref ok, rig != null && rig.Mode == SP.CameraSystem.ControlMode.Fps, sb, "la camara queda en FPS (no obliga a RTS)");
            Dificultad.Actual = dificultadPrevia; ModoDios.Poner(true);
            PartidaGuardada.Borrar();
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static int hpDocAlRestaurar;

        static string SinTiempo(string s)
        {
            var r = new List<string>();
            foreach (var p in s.Split(';')) if (!p.StartsWith("t=")) r.Add(p);
            return string.Join(";", r);
        }

        // ---------------------------------------------------------------- 120t
        static string LeerMotivoDelArchivo(out string fechaUtc, out int objetivo)
        {
            fechaUtc = ""; objetivo = -1;
            try
            {
                var j = JsonUtility.FromJson<PartidaGuardadaDatos>(File.ReadAllText(RutaDePartidaDePrueba()));
                if (j == null) return "(vacio)";
                fechaUtc = j.fechaUtc; objetivo = j.objetivo;
                return j.motivo;
            }
            catch (Exception) { return "(sin archivo)"; }
        }

        static IEnumerator Bug120t()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            LimpiarPartidasDePrueba(); PartidaGuardada.AutoguardadoActivo = true;
            var d = OperacionDirector.Instancia;
            // 1) Fin de la cinematica de apertura (rapel sin encadenar), desde un Play fresco: no se guarda MIENTRAS corre y si al terminar.
            LimpiarPartidasDePrueba();
            CinematicaDeRapel.ReiniciarPorPrueba(false);
            float tt = Time.realtimeSinceStartup;
            bool durante = false;
            while (CinematicaDeRapel.Activa && Time.realtimeSinceStartup - tt < 12f)
            {
                yield return null;
                if (File.Exists(RutaDePartidaDePrueba())) durante = true;
            }
            W8Ok(ref ok, !durante, sb, "no se guarda mientras corre la cinematica de apertura");
            string mot = ""; string fe = ""; int ob = -1;
            tt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tt < 8f) { yield return null; mot = LeerMotivoDelArchivo(out fe, out ob); if (mot.StartsWith("auto")) break; }
            W8Ok(ref ok, mot.Contains("fin cinemática de apertura") && ob == 1, sb, $"fin de la apertura: motivo='{mot}'");
            // 2) Cada EntrarFase (objetivos 2..6) escribe "auto: inicio objetivo N" con fecha nueva. (Arrancar cancela la cinematica de apertura.)
            string fechaPrevia = "";
            for (int o = 2; o <= 6; o++)
            {
                OperacionPrueba.Arrancar(o);
                float t0 = Time.realtimeSinceStartup;
                string motivo = ""; string fecha = ""; int obj = -1;
                while (Time.realtimeSinceStartup - t0 < 12f)
                {
                    yield return null;
                    motivo = LeerMotivoDelArchivo(out fecha, out obj);
                    if (motivo == "auto: inicio objetivo " + o && fecha != fechaPrevia) break;
                }
                bool bien = motivo == "auto: inicio objetivo " + o && obj == o && fecha != fechaPrevia && string.CompareOrdinal(fecha, fechaPrevia) > 0;
                W8Ok(ref ok, bien, sb, $"objetivo {o}: motivo='{motivo}' objetivo={obj} fecha nueva={(fecha != fechaPrevia)}");
                fechaPrevia = fecha;
            }
            // 3) Toma del jefe (CineDeHuida): al terminar guarda "fin CineDeHuida".
            LimpiarPartidasDePrueba();
            OperacionPrueba.Arrancar(4);
            foreach (var x in Esperar(2.2f)) yield return x;
            LimpiarPartidasDePrueba();
            d.TeletransportarEscuadra(new Vector3(0f, 0f, -24f), Quaternion.identity);
            tt = Time.realtimeSinceStartup;
            bool vioCine = false;
            while (Time.realtimeSinceStartup - tt < 14f)
            {
                yield return null;
                if (CineDeHuida.Activa) { vioCine = true; if (File.Exists(RutaDePartidaDePrueba())) { W8Ok(ref ok, false, sb, "se guardo durante la toma del jefe"); break; } }
                mot = LeerMotivoDelArchivo(out fe, out ob);
                if (mot.StartsWith("auto") && vioCine && !CineDeHuida.Activa) break;
            }
            W8Ok(ref ok, vioCine && mot.Contains("fin CineDeHuida (toma del jefe)"), sb, $"toma del jefe: cinematica vista={vioCine}, motivo='{mot}'");
            // 4) Cinematica final: se combina con el inicio del objetivo 5 (mismo camino que usa el cierre de la huida).
            LimpiarPartidasDePrueba();
            d.ProgramarAutoguardado("fin CineDeHuida (cinemática final)", 0.6f);
            d.EntrarFase(FaseOperacion.Resistir);
            tt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tt < 8f) { yield return null; mot = LeerMotivoDelArchivo(out fe, out ob); if (mot.StartsWith("auto")) break; }
            W8Ok(ref ok, mot.Contains("fin CineDeHuida (cinemática final)") && mot.Contains("inicio objetivo 5") && ob == 5, sb, $"cierre de la huida: motivo='{mot}'");
            // 5) Con el autoguardado apagado no escribe nada.
            LimpiarPartidasDePrueba(); PartidaGuardada.AutoguardadoActivo = false;
            OperacionPrueba.Arrancar(3);
            foreach (var x in Esperar(3f)) yield return x;
            W8Ok(ref ok, !File.Exists(RutaDePartidaDePrueba()), sb, "con AutoguardadoActivo=false no se escribe");
            PartidaGuardada.Borrar();
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 120u
        static IEnumerator Bug120u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            LimpiarPartidasDePrueba(); PartidaGuardada.AutoguardadoActivo = false;
            OperacionPrueba.Arrancar(3);
            foreach (var x in Esperar(1.5f)) yield return x;
            string ruta = RutaDePartidaDePrueba();
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            // JSON roto.
            File.WriteAllText(ruta, "{ esto no es json ::: ");
            bool excepcion = false; PartidaGuardadaDatos datos = null; bool hay = true;
            try { hay = PartidaGuardada.HayPartida(); datos = PartidaGuardada.Cargar(); } catch (Exception) { excepcion = true; }
            W8Ok(ref ok, !excepcion && !hay && datos == null && !string.IsNullOrEmpty(PartidaGuardada.UltimoAviso), sb, $"JSON roto: sin excepcion, HayPartida={hay}, aviso='{PartidaGuardada.UltimoAviso}'");
            W8Ok(ref ok, !PartidaGuardada.ContinuarSinPantallaDeCarga() && OperacionDirector.Instancia != null, sb, "Continuar con el archivo roto no hace nada y no recarga la escena");
            // Version distinta.
            PartidaGuardada.Borrar();
            OperacionPrueba.Arrancar(3);
            foreach (var x in Esperar(0.6f)) yield return x;
            PartidaGuardada.Guardar("manual");
            var j = JsonUtility.FromJson<PartidaGuardadaDatos>(File.ReadAllText(ruta));
            W8Ok(ref ok, j != null && j.version == PartidaGuardada.VersionActual, sb, "el archivo guardado lleva la version " + PartidaGuardada.VersionActual);
            j.version = 999;
            File.WriteAllText(ruta, JsonUtility.ToJson(j));
            string aviso0 = PartidaGuardada.UltimoAviso;
            hay = PartidaGuardada.HayPartida(); datos = PartidaGuardada.Cargar();
            W8Ok(ref ok, !hay && datos == null && PartidaGuardada.UltimoAviso.Contains("version"), sb, $"version distinta: HayPartida={hay}, aviso='{PartidaGuardada.UltimoAviso}'");
            // Archivo vacio y escena equivocada.
            File.WriteAllText(ruta, "");
            W8Ok(ref ok, !PartidaGuardada.HayPartida(), sb, "archivo vacio: no hay partida");
            j.version = PartidaGuardada.VersionActual; j.escena = "SC_Gameplay";
            File.WriteAllText(ruta, JsonUtility.ToJson(j));
            W8Ok(ref ok, !PartidaGuardada.HayPartida(), sb, "partida de otra escena: no hay partida");
            // La escritura es atomica: tras guardar dos veces queda el .bak del anterior y ningun .tmp.
            PartidaGuardada.Borrar();
            PartidaGuardada.Guardar("manual"); PartidaGuardada.Guardar("manual");
            W8Ok(ref ok, File.Exists(ruta) && File.Exists(ruta + ".bak") && !File.Exists(ruta + ".tmp"), sb, "escritura atomica: archivo + .bak, sin .tmp");
            // Los botones de la pausa: con un archivo roto no se ofrece cargar.
            File.WriteAllText(ruta, "basura");
            var pc = UnityEngine.Object.FindFirstObjectByType<PauseController>(FindObjectsInactive.Include);
            pc.ShowPause();
            yield return null;
            W8Ok(ref ok, pc.BotonCargar != null && !pc.BotonCargar.gameObject.activeSelf, sb, "pausa: con el archivo roto el boton CARGAR no aparece");
            pc.OnContinueClicked();
            PartidaGuardada.Borrar();
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        [EscenaDelCheck("SC_MainMenu")]
        static IEnumerator Bug120z()
        {
            var sb = new StringBuilder(); bool ok = true;
            PartidaGuardada.ModoPrueba = true; PartidaGuardada.Borrar();
            var prevDif = Dificultad.Actual;
            // Un archivo valido minimo (objetivo 4/6).
            string ruta = RutaDePartidaDePrueba();
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            var datos = new PartidaGuardadaDatos { version = PartidaGuardada.VersionActual, escena = "SC_Operacion", fechaUtc = DateTime.UtcNow.ToString("o"), motivo = "manual", fase = "Huir", objetivo = 4, dificultad = 1, estado = new EstadoDeSesion { escena = "SC_Operacion" } };
            File.WriteAllText(ruta, JsonUtility.ToJson(datos));
            UnityEngine.SceneManagement.SceneManager.LoadScene("SC_MainMenu");
            yield return null; yield return null;
            foreach (var x in Esperar(0.8f)) yield return x;
            var mm = UnityEngine.Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
            W8Ok(ref ok, mm != null && mm.BotonContinuar != null && mm.BotonContinuar.gameObject.activeInHierarchy, sb, "con partida valida aparece CONTINUAR");
            string txt = mm != null && mm.BotonContinuar != null ? string.Join(" ", System.Array.ConvertAll(mm.BotonContinuar.GetComponentsInChildren<UnityEngine.UI.Text>(true), q => q.text)) : "";
            W8Ok(ref ok, txt.Contains("OBJETIVO 4/6"), sb, "el boton dice el objetivo: '" + txt.Replace("\n", " ") + "'");
            ScreenCapture.CaptureScreenshot(RutaValidacion("v3_120_menu_continuar.png")); foreach (var x in Esperar(0.6f)) yield return x;
            // Archivo roto: no aparece.
            File.WriteAllText(ruta, "{ roto");
            UnityEngine.SceneManagement.SceneManager.LoadScene("SC_MainMenu");
            yield return null; yield return null;
            foreach (var x in Esperar(0.8f)) yield return x;
            mm = UnityEngine.Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
            W8Ok(ref ok, mm != null && mm.BotonContinuar == null, sb, "con el archivo roto no aparece CONTINUAR");
            // Sin archivo: tampoco.
            PartidaGuardada.Borrar();
            UnityEngine.SceneManagement.SceneManager.LoadScene("SC_MainMenu");
            yield return null; yield return null;
            foreach (var x in Esperar(0.8f)) yield return x;
            mm = UnityEngine.Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
            W8Ok(ref ok, mm != null && mm.BotonContinuar == null, sb, "sin archivo no aparece CONTINUAR");
            Dificultad.Actual = prevDif;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 120v
        static IEnumerator Bug120v()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            LimpiarPartidasDePrueba(); PartidaGuardada.AutoguardadoActivo = false;
            OperacionPrueba.Arrancar(6);
            foreach (var x in Esperar(1.5f)) yield return x;
            W8Ok(ref ok, PartidaGuardada.Guardar("manual") && File.Exists(RutaDePartidaDePrueba()) && PartidaGuardada.HayPartida(), sb, "antes de ganar hay partida guardada");
            // Ganar: el mismo metodo que corre al terminar el despegue del helicoptero.
            var d = OperacionDirector.Instancia;
            var ganar = typeof(OperacionDirector).GetMethod("Ganar", BindingFlags.Instance | BindingFlags.NonPublic);
            ganar.Invoke(d, null);
            yield return null;
            W8Ok(ref ok, d.Fase == FaseOperacion.Victoria && !File.Exists(RutaDePartidaDePrueba()) && !PartidaGuardada.HayPartida(), sb, "al ganar el archivo se borra y CONTINUAR ya no aparece");
            W8Ok(ref ok, PartidaGuardada.MotivoDeBloqueo() != null, sb, "tras ganar no se puede guardar");
            PartidaGuardada.Borrar();
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 120q: la derrota conserva la partida y ofrece cargarla
        static IEnumerator Bug120q()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            LimpiarPartidasDePrueba(); PartidaGuardada.AutoguardadoActivo = false;
            var d = OperacionDirector.Instancia;
            OperacionPrueba.Arrancar(3);
            foreach (var x in Esperar(1.5f)) yield return x;
            PartidaGuardada.Guardar("manual");
            d.Perder("prueba");
            yield return null; yield return null;
            W8Ok(ref ok, PartidaGuardada.HayPartida(), sb, "al perder la partida guardada sigue");
            var outcome = GameOutcomeController.Activo;
            W8Ok(ref ok, outcome != null && outcome.BotonCargar != null && outcome.BotonCargar.gameObject.activeInHierarchy, sb, "la derrota ofrece CARGAR ULTIMO GUARDADO");
            ScreenCapture.CaptureScreenshot(RutaValidacion("v3_120_derrota_cargar.png")); foreach (var x in Esperar(0.6f)) yield return x;
            Time.timeScale = 1f;
            PartidaGuardada.Borrar();
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 120w: en cada objetivo y subfase
        struct CasoDeGuardado
        {
            public string nombre; public int objetivo, puesto, subfase;
            public Action<OperacionDirector> preparar;          // deja un estado conocido (se ejecuta 2,5 s despues de Arrancar)
            public Func<OperacionDirector, string> captura;      // estado especifico del caso (texto comparable)
            public Func<string, string, bool> igual;             // comparador especifico (null = igualdad exacta)
        }

        static string MatarN(OperacionDirector d, int n)
        {
            int k = 0;
            foreach (var s in d.enemigosCuartel) if (s != null && s.Health != null && s.Health.IsAlive && k < n) { s.Health.TakeDamage(100000, -1); k++; }
            return "matados=" + k;
        }

        static int VivosCuartel(OperacionDirector d) { int n = 0; foreach (var s in d.enemigosCuartel) if (s != null && s.gameObject.activeInHierarchy && s.Health != null && s.Health.IsAlive) n++; return n; }

        static List<CasoDeGuardado> CasosDeGuardado()
        {
            var l = new List<CasoDeGuardado>();
            l.Add(new CasoDeGuardado { nombre = "1 Infiltrar (3 abatidos)", objetivo = 1, preparar = d => MatarN(d, 3), captura = d => "vivos=" + VivosCuartel(d) });
            l.Add(new CasoDeGuardado { nombre = "2 Puestos (Oeste)", objetivo = 2, puesto = 1, captura = d => "volados=" + d.PuestoActual });
            l.Add(new CasoDeGuardado { nombre = "2 Puestos (Este, Oeste volado)", objetivo = 2, puesto = 2, captura = d => "volados=" + d.PuestoActual + ";oesteVolado=" + (d.puestos[0].carga != null && d.puestos[0].carga.EstaVolada) });
            l.Add(new CasoDeGuardado { nombre = "3 Centro de datos (hackeo 10 s)", objetivo = 3, preparar = d => { d.computadora.Avanzar(10f); }, captura = d => "progreso=" + d.computadora.Progreso01.ToString("0.00", CultureInfo.InvariantCulture), igual = (a, b) => Cerca(Num(a, "progreso"), Num(b, "progreso"), 0.06f) });
            l.Add(new CasoDeGuardado { nombre = "4.0 Huir: acercamiento", objetivo = 4, subfase = 0, captura = d => "subfase=" + d.Subfase });
            l.Add(new CasoDeGuardado { nombre = "4.1 Huir: jefe (60 % de vida)", objetivo = 4, subfase = 1, preparar = d => d.tanque.PonerVida01(0.6f), captura = d => "vida=" + d.tanque.Health.Current / (float)d.tanque.Health.MaxHealth, igual = (a, b) => Cerca(Num(a, "vida"), Num(b, "vida"), 0.08f) });
            l.Add(new CasoDeGuardado { nombre = "4.2 Huir: reparacion (40 %)", objetivo = 4, subfase = 2, preparar = d => { if (d.Reparacion != null) d.Reparacion.FijarProgreso(0.4f); }, captura = d => "progreso=" + (d.Reparacion != null ? d.Reparacion.Progreso01 : -1f), igual = (a, b) => Cerca(Num(a, "progreso"), Num(b, "progreso"), 0.1f) });
            l.Add(new CasoDeGuardado { nombre = "4.3 Huir: abordaje", objetivo = 4, subfase = 3, captura = d => "subfase=" + d.Subfase + ";bando=" + d.tanque.Bando });
            l.Add(new CasoDeGuardado { nombre = "4.4 Huir: carrera en tanque", objetivo = 4, subfase = 3, preparar = d => d.Embarcar(), captura = d => "enTanque=" + d.EnTanque + ";subfase=" + d.Subfase, igual = null });
            l.Add(new CasoDeGuardado { nombre = "5.0 Resistir: llegada a la radio", objetivo = 5, subfase = 0, captura = d => "radio=" + MandoTactico.RadioTomada + ";subfase=" + d.Subfase });
            l.Add(new CasoDeGuardado { nombre = "5.1 Resistir: mando (radio, milicianos, sectores)", objetivo = 5, subfase = 1, preparar = d => { d.sectoresDeDefensa[2].Caido = true; var m = SoldadoPor("Miliciano_2"); if (m != null) m.Health.TakeDamage(100000, -1); }, captura = d => "radio=" + MandoTactico.RadioTomada + ";milicianos=" + MandoTactico.Milicianos.Count + ";Ccaido=" + d.sectoresDeDefensa[2].Caido + ";M2vivo=" + (SoldadoPor("Miliciano_2") != null && SoldadoPor("Miliciano_2").Health.IsAlive) });
            l.Add(new CasoDeGuardado { nombre = "6 Extraer (helicoptero en la zona)", objetivo = 6, captura = d => "heliEnZona=" + d.HeliEnLaZona + ";fase=" + d.Fase });
            return l;
        }

        static float Num(string s, string clave)
        {
            foreach (var p in s.Split(';'))
                if (p.StartsWith(clave + "=", StringComparison.Ordinal) && float.TryParse(p.Substring(clave.Length + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
            return -999f;
        }

        static string CapturaGeneral(Vector3 pos, out Vector3 posicion)
        {
            var drv = PlayerInputDriver.Activo;
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            posicion = yo != null ? yo.transform.position : Vector3.zero;
            var doc = SoldadoPor("Doc"); var kes = SoldadoPor("Kes");
            return $"poseido={NombreDelPoseido()};docHp={(doc != null ? doc.Health.Current : -1)};kes={(kes != null ? Armas(kes) : "-")}";
        }

        static IEnumerator Bug120w()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var informe = new StringBuilder(); bool ok = true;
            var dificultadPrevia = Dificultad.Actual;
            LimpiarPartidasDePrueba(); PartidaGuardada.AutoguardadoActivo = false;
            foreach (var caso in CasosDeGuardado())
            {
                var sb = new StringBuilder(); bool casoOk = true;
                // Escena limpia para cada caso (el Continuar del caso anterior deja estado del director, p. ej. EnTanque).
                UnityEngine.SceneManagement.SceneManager.LoadScene("SC_Operacion");
                yield return null; yield return null;
                foreach (var x in Esperar(1.0f)) yield return x;
                OperacionPrueba.Arrancar(caso.objetivo, caso.puesto > 0 ? caso.puesto : 1, caso.subfase);
                foreach (var x in Esperar(2.5f)) yield return x;
                var d = OperacionDirector.Instancia;
                ModoDios.Poner(false);
                var doc = SoldadoPor("Doc"); var kes = SoldadoPor("Kes");
                if (kes != null && doc != null && d.Fase != FaseOperacion.Extraer)
                {
                    doc.Health.RestaurarVida(41);
                    kes.Weapon.FijarReservaParaPrueba(kes.Weapon.CurrentWeaponKind, 77);
                    kes.Weapon.ConsumirGranada();
                }
                caso.preparar?.Invoke(d);
                foreach (var x in Esperar(0.5f)) yield return x;
                string antesG = CapturaGeneral(Vector3.zero, out var posAntes);
                string antesC = caso.captura(d);
                string faseAntes = d.Fase + "/" + d.Subfase;
                bool enTanqueAntes = d.EnTanque;
                bool cargo = false; var salida = new StringBuilder();
                foreach (var x in GuardarYContinuar(salida, v => cargo = v)) yield return x;
                sb.Append(salida);
                if (!cargo) { casoOk = false; }
                else
                {
                    d = OperacionDirector.Instancia;
                    string despG = CapturaGeneral(Vector3.zero, out var posDesp);
                    string despC = caso.captura(d);
                    string faseDesp = d.Fase + "/" + d.Subfase;
                    bool mismaFase = faseDesp == faseAntes;
                    bool coincide = caso.igual != null ? caso.igual(antesC, despC) : antesC == despC;
                    float dist = Vector3.Distance(new Vector3(posAntes.x, 0f, posAntes.z), new Vector3(posDesp.x, 0f, posDesp.z));
                    bool generalOk = Num(antesG.Replace("poseido=", "p="), "docHp") >= 0 ? Num(despG, "docHp") >= Num(antesG, "docHp") - 8f && antesG.Split(';')[0] == despG.Split(';')[0] : true;
                    bool armasOk = antesG.Substring(antesG.IndexOf("kes=", StringComparison.Ordinal)) == despG.Substring(despG.IndexOf("kes=", StringComparison.Ordinal));
                    bool posOk = dist < (caso.objetivo == 4 && caso.subfase >= 3 && d.EnTanque ? 30f : 4f);
                    bool tanqueOk = enTanqueAntes == d.EnTanque;
                    bool sinCine = !CinematicaDeRapel.Activa && !CinematicaDeOperacion.Activa;
                    casoOk = mismaFase && coincide && generalOk && armasOk && posOk && tanqueOk && sinCine;
                    sb.Append($"fase {faseAntes}->{faseDesp}{(mismaFase ? "" : " (MAL)")}; especifico [{antesC}] -> [{despC}]{(coincide ? "" : " (MAL)")}; general {(generalOk ? "igual" : "DISTINTO " + antesG + " -> " + despG)}; armas {(armasOk ? "iguales" : "DISTINTAS " + antesG.Substring(antesG.IndexOf("kes=", StringComparison.Ordinal)) + " -> " + despG.Substring(despG.IndexOf("kes=", StringComparison.Ordinal)))}; mueve {dist:0.0} m{(posOk ? "" : " (MAL " + posAntes + "->" + posDesp + " " + antesG.Split(';')[0] + ")")}; tanque {(tanqueOk ? "ok" : "MAL")}; sin cinematica={sinCine}");
                }
                if (!casoOk) ok = false;
                informe.Append($"[{(casoOk ? "OK" : "FALLO")}] {caso.nombre}: {sb} | ");
                // Se restablece el estado para el siguiente caso (escena ya recargada por el Continuar).
                yield return null;
            }
            ModoDios.Poner(true);
            Dificultad.Actual = dificultadPrevia;
            PartidaGuardada.Borrar();
            Fin((ok ? "OK " : "FALLO ") + informe);
        }

        // ---------------------------------------------------------------- 120x: oleadas del mando
        static IEnumerator Bug120x()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var dificultadPrevia = Dificultad.Actual;
            LimpiarPartidasDePrueba(); PartidaGuardada.AutoguardadoActivo = false;
            OperacionPrueba.Arrancar(5, 1, 1);
            foreach (var x in Esperar(2.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            // El reloj a ~1 s antes de la oleada 3 (sale al 50 %): las oleadas 1 y 2 se lanzan ya y se matan; la 3 queda en curso al guardar.
            float total = d.segundosDeResistencia;
            d.FijarRelojEfectivoParaPrueba(total * 0.50f + 1f);
            float t0 = Time.realtimeSinceStartup;
            while (d.OleadasDelMandoLanzadas < 3 && Time.realtimeSinceStartup - t0 < 20f) yield return null;
            W8Ok(ref ok, d.OleadasDelMandoLanzadas >= 3, sb, $"oleadas lanzadas antes de guardar: {d.OleadasDelMandoLanzadas}");
            // Las oleadas 1 y 2: sus soldados muertos (terminadas). La 3: viva (en curso).
            // Los soldados de una oleada salen escalonados: se van matando a medida que aparecen hasta que no queda ninguno pendiente.
            float tk = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tk < 60f && (d.OleadasDelMando[0].Pendientes(d) > 0 || d.OleadasDelMando[1].Pendientes(d) > 0 || d.OleadasDelMando[0].Vivos > 0 || d.OleadasDelMando[1].Vivos > 0))
            {
                for (int i = 0; i < 2; i++) foreach (var s in d.OleadasDelMando[i].soldados) if (s != null && s.gameObject.activeInHierarchy && s.Health.IsAlive) s.Health.TakeDamage(100000, -1);
                yield return null;
            }
            foreach (var v in d.OleadasDelMando[0].vehiculos) if (v != null) UnityEngine.Object.Destroy(v.gameObject);
            foreach (var v in d.OleadasDelMando[1].vehiculos) if (v != null) UnityEngine.Object.Destroy(v.gameObject);
            yield return null; yield return null;
            for (int i = 0; i < 2; i++) { var oo = d.OleadasDelMando[i]; sb.Append($"[ola{i + 1}: lanzada={oo.lanzada} vivos={oo.Vivos} pend={oo.Pendientes(d)} veh={oo.VehiculosVivos}] "); }
            int enCurso = d.OleadasDelMando[2].Vivos + d.OleadasDelMando[2].Pendientes(d);
            W8Ok(ref ok, enCurso > 0, sb, $"la oleada 3 esta en curso: {enCurso} enemigos");
            bool cargo = false; var salida = new StringBuilder();
            foreach (var x in GuardarYContinuar(salida, v => cargo = v)) yield return x;
            sb.Append(salida);
            if (!cargo) { Dificultad.Actual = dificultadPrevia; Fin("FALLO " + sb); yield break; }
            d = OperacionDirector.Instancia;
            var o1 = d.OleadasDelMando[0]; var o2 = d.OleadasDelMando[1]; var o3 = d.OleadasDelMando[2];
            W8Ok(ref ok, o1.lanzada && o2.lanzada, sb, $"las oleadas 1 y 2 (terminadas) no se relanzan: lanzada={o1.lanzada}/{o2.lanzada} y sin soldados nuevos ({o1.soldados.Count}/{o2.soldados.Count})");
            W8Ok(ref ok, o1.soldados.Count == 0 && o2.soldados.Count == 0, sb, "no hay enemigos de las oleadas terminadas");
            // La oleada 3 en curso: el reloj retrocedio hasta antes de su aviso y se vuelve a anunciar y a lanzar.
            W8Ok(ref ok, d.RelojEfectivo < total * 0.50f, sb, $"el reloj retrocedio a {d.RelojEfectivo:0.0} (< {total * 0.5f:0.0})");
            t0 = Time.realtimeSinceStartup;
            // El reloj solo corre con los pasos hechos y la radio tomada: ya lo estan (Arrancar con subfase 1).
            while (!o3.lanzada && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            W8Ok(ref ok, o3.anunciada && o3.lanzada, sb, $"la oleada 3 se vuelve a anunciar y lanzar (anunciada={o3.anunciada}, lanzada={o3.lanzada}) tras {Time.realtimeSinceStartup - t0:0} s");
            Dificultad.Actual = dificultadPrevia;
            PartidaGuardada.Borrar();
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 120y: interfaz
        static IEnumerator Bug120y()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            LimpiarPartidasDePrueba(); PartidaGuardada.AutoguardadoActivo = false;
            OperacionPrueba.Arrancar(3);
            foreach (var x in Esperar(2f)) yield return x;
            var pc = UnityEngine.Object.FindFirstObjectByType<PauseController>(FindObjectsInactive.Include);
            if (pc == null) { Fin("FALLO no hay PauseController"); yield break; }
            ModoDios.Poner(false);
            pc.ShowPause();
            yield return null; yield return null;
            W8Ok(ref ok, pc.BotonGuardar != null && pc.BotonGuardar.interactable, sb, "la pausa tiene GUARDAR PARTIDA habilitado");
            W8Ok(ref ok, pc.BotonCargar != null && !pc.BotonCargar.gameObject.activeSelf, sb, "sin partida guardada no se ofrece CARGAR");
            foreach (var x in Esperar(0.3f)) yield return x;
            ScreenCapture.CaptureScreenshot(RutaValidacion("v3_120_pausa_sin_guardado.png")); foreach (var x in Esperar(0.6f)) yield return x;
            pc.BotonGuardar.onClick.Invoke();
            yield return null; yield return null;
            W8Ok(ref ok, PartidaGuardada.HayPartida() && PartidaGuardada.UltimoMotivoGuardado == "manual", sb, "clic en GUARDAR: archivo escrito con motivo 'manual'");
            W8Ok(ref ok, IndicadorDeGuardado.Visible && IndicadorDeGuardado.TextoActual == "PARTIDA GUARDADA", sb, $"feedback '{IndicadorDeGuardado.TextoActual}'");
            W8Ok(ref ok, pc.BotonCargar.gameObject.activeSelf && pc.BotonCargar.interactable, sb, "ahora se ofrece CARGAR ULTIMO GUARDADO");
            W8Ok(ref ok, pc.TextoDeLaNota.StartsWith("PARTIDA GUARDADA"), sb, "la nota dice: " + pc.TextoDeLaNota);
            foreach (var x in Esperar(0.4f)) yield return x;
            ScreenCapture.CaptureScreenshot(RutaValidacion("v3_120_pausa_guardado.png")); foreach (var x in Esperar(0.6f)) yield return x;
            pc.OnContinueClicked();
            yield return null;
            // Bloqueos: en el aire y durante una cinematica.
            var yo = PlayerInputDriver.Activo != null && PlayerInputDriver.Activo.Brain != null ? PlayerInputDriver.Activo.Brain.Current : null;
            string bloqueoCine = null;
            CinematicaDeRapel.ReiniciarPorPrueba(false);
            yield return null; yield return null;
            bloqueoCine = PartidaGuardada.MotivoDeBloqueo();
            W8Ok(ref ok, bloqueoCine != null && bloqueoCine.Contains("CINEMATICA") && !PartidaGuardada.Guardar("manual"), sb, $"durante una cinematica no se guarda ('{bloqueoCine}')");
            CinematicaDeRapel.Saltar();
            foreach (var x in Esperar(0.5f)) yield return x;
            W8Ok(ref ok, PartidaGuardada.MotivoDeBloqueo() == null, sb, "al terminar la cinematica se puede guardar de nuevo");
            if (yo != null)
            {
                yo.Health.TakeDamage(100000, -1);
                yield return null;
                W8Ok(ref ok, PartidaGuardada.MotivoDeBloqueo() != null, sb, "con el soldado caido no se guarda: " + PartidaGuardada.MotivoDeBloqueo());
            }
            Time.timeScale = 1f;
            ModoDios.Poner(true);
            PartidaGuardada.Borrar();
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
