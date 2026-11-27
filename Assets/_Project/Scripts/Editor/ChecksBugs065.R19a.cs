using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Mision;
using SP.Operacion;
using SP.Player;
using SP.Presentation;
using SP.Tutorial;
using SP.Vehicles;

namespace SP.EditorTools
{
    // Ronda 19 (Docs/PLAN_R19_v2.md), checks del carril A (G1): puntos 1 (Nivel 1 terminado), 3 (tutorial simple v2) y 7 en el tutorial.
    //   Bug994a (SC_Tutorial)  : victoria del simple -> panel NIVEL 1 COMPLETADO con 3 botones; JUGAR NIVEL 2 carga SC_Operacion
    //   Bug994b (SC_MainMenu)  : tarjetas COMPLETADO / SIGUIENTE; despues carga SC_Operacion, Arrancar(6) + victoria -> Nivel 2 completo + fila
    //   Bug994e (SC_Tutorial)  : simple de 8 pasos (textos, sub-pasos, [E], ola al sentarse en el canon) + corrida del autoplayer (<= 110 s)
    //   Bug994f (SC_Tutorial)  : completo intacto (>= 36 pasos, textos de entrar_tanque y mira_tanque, ayuda 16 s)
    //   Bug994l (SC_Tutorial)  : anillo de subida en entrar_tanque (radio de interaccion, sigue al tanque, onda al subir, baliza sin aro)
    // Todos guardan y DEVUELVEN los PlayerPrefs que tocan (progreso del tutorial y de los niveles 1 y 2): no borran el progreso real.
    // Correr:  return SP.EditorTools.ChecksBugs065.CorrerLista("Bug994a,Bug994b,Bug994e,Bug994f,Bug994l", true);
    public static partial class ChecksBugs065
    {
        const int R19aMaxMensaje = 110, R19aMaxSubPasos = 13;
        const float R19aMaxCorridaSimple = 110f, R19aMaxCargaNivel2 = 20f;
        static readonly string[] R19aIdsSimples = { "camara", "wasd", "disparar", "cambiar", "rts", "entrar_tanque", "canon_simple", "victoria" };

        // ---------------------------------------------------------------- PlayerPrefs: foto y restauracion
        struct R19aPref { public string Clave; public bool Habia, EsFloat; public int I; public float F; }

        static List<R19aPref> R19aGuardarPrefs()
        {
            var l = new List<R19aPref>();
            void Int(string k) => l.Add(new R19aPref { Clave = k, Habia = PlayerPrefs.HasKey(k), I = PlayerPrefs.GetInt(k, 0) });
            void Flo(string k) => l.Add(new R19aPref { Clave = k, Habia = PlayerPrefs.HasKey(k), EsFloat = true, F = PlayerPrefs.GetFloat(k, 0f) });
            Int(TutorialManager.ClaveProgresoSimple); Int(TutorialManager.ClaveProgresoCompleto);
            for (int n = 1; n <= 2; n++) { Int(ProgresoDeNiveles.ClaveCompleto(n)); Flo(ProgresoDeNiveles.ClaveMejorTiempo(n)); }
            return l;
        }

        static void R19aRestaurarPrefs(List<R19aPref> l)
        {
            if (l == null) return;
            foreach (var p in l)
            {
                if (!p.Habia) PlayerPrefs.DeleteKey(p.Clave);
                else if (p.EsFloat) PlayerPrefs.SetFloat(p.Clave, p.F);
                else PlayerPrefs.SetInt(p.Clave, p.I);
            }
            PlayerPrefs.Save();
        }

        // Espera al TutorialManager con su primer paso arrancado (distinto de 'viejo', para despues de recargar la escena).
        static IEnumerable R19aEsperarTutorial(TutorialManager viejo, float tope)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < tope)
            {
                var tm = TutorialManager.Instance;
                if (tm != null && tm != viejo && tm.Indice >= 0) yield break;
                yield return null;
            }
        }

        // Salta pasos con SaltarPaso (como [F8]) hasta llegar al Id pedido (o al final).
        static IEnumerable R19aSaltarHasta(TutorialManager tm, string id, float tope)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < tope && tm != null && !tm.Terminado && (tm.PasoActual == null || tm.PasoActual.Id != id))
            {
                tm.SaltarPaso();
                foreach (var x in Esperar(0.3f)) yield return x;
            }
        }

        static string R19aTextoDe(UnityEngine.UI.Button b)
        {
            var t = b != null ? b.GetComponentInChildren<UnityEngine.UI.Text>(true) : null;
            return t != null ? t.text : "";
        }

        // ================================================================ 994a: el tutorial cierra el Nivel 1 y encadena al 2
        [EscenaDelCheck("SC_Tutorial")]
        static IEnumerator Bug994a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var prefs = R19aGuardarPrefs();
            foreach (var x in R19aEsperarTutorial(null, 15f)) yield return x;
            var tm = TutorialManager.Instance;
            if (tm == null) { R19aRestaurarPrefs(prefs); Fin("FALLO no hay TutorialManager"); yield break; }
            ModoDios.Poner(true);
            ProgresoDeNiveles.Borrar(1);
            W8Ok(ref ok, TutorialManager.ModoSimple, sb, "modo simple (el del menu)");
            foreach (var x in R19aSaltarHasta(tm, "victoria", 90f)) yield return x;
            if (tm.PasoActual == null || tm.PasoActual.Id != "victoria") { ModoDios.Poner(false); R19aRestaurarPrefs(prefs); Fin("FALLO no se llego al paso victoria (" + (tm.PasoActual != null ? tm.PasoActual.Id : "-") + ")"); yield break; }
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 6f && (VictoriaTutorial.Actual == null || !VictoriaTutorial.Actual.PanelVisible)) yield return null;
            foreach (var x in Esperar(2f)) yield return x;
            var v = VictoriaTutorial.Actual;
            W8Ok(ref ok, v != null && v.PanelVisible, sb, "panel final visible");
            W8Ok(ref ok, tm.Terminado && ProgresoDeNiveles.EstaCompleto(1) && ProgresoDeNiveles.MejorTiempo(1) > 0f, sb,
                $"Nivel 1 completo={ProgresoDeNiveles.EstaCompleto(1)} mejor={ProgresoDeNiveles.Formato(ProgresoDeNiveles.MejorTiempo(1))}");
            if (v == null) { ModoDios.Poner(false); R19aRestaurarPrefs(prefs); Fin("FALLO " + sb); yield break; }
            W8Ok(ref ok, v.TituloDelPanel == VictoriaTutorial.TextoTituloNivel1, sb, $"titulo '{v.TituloDelPanel}'");
            W8Ok(ref ok, v.BotonSiguiente != null && v.BotonRepetir != null && v.BotonMenu != null, sb, "3 botones (JUGAR NIVEL 2, REPETIR, MENU)");
            W8Ok(ref ok, R19aTextoDe(v.BotonSiguiente) == VictoriaTutorial.TextoBotonSiguiente, sb, $"boton primario '{R19aTextoDe(v.BotonSiguiente)}'");
            if (v.BotonSiguiente == null) { ModoDios.Poner(false); R19aRestaurarPrefs(prefs); Fin("FALLO " + sb); yield break; }
            ModoDios.Poner(false);
            t0 = Time.realtimeSinceStartup;
            v.BotonSiguiente.onClick.Invoke();
            while (Time.realtimeSinceStartup - t0 < R19aMaxCargaNivel2 + 5f && !(SceneManager.GetActiveScene().name == "SC_Operacion" && OperacionDirector.Instancia != null)) yield return null;
            float carga = Time.realtimeSinceStartup - t0;
            bool cargo = SceneManager.GetActiveScene().name == "SC_Operacion";
            W8Ok(ref ok, cargo && carga <= R19aMaxCargaNivel2, sb, $"JUGAR NIVEL 2 -> escena {SceneManager.GetActiveScene().name} en {carga:0.0} s (tope {R19aMaxCargaNivel2:0} s)");
            foreach (var x in Esperar(1f)) yield return x;
            W8Ok(ref ok, Health.RegeneracionPermitida && Mathf.Approximately(Demolicion.Segundos, Demolicion.SegundosNormales) && Mathf.Approximately(Time.timeScale, 1f) && TutorialManager.Instance == null, sb,
                $"estaticos del tutorial devueltos (regeneracion={Health.RegeneracionPermitida}, demolicion={Demolicion.Segundos:0.0} s, timeScale={Time.timeScale:0.00})");
            R19aRestaurarPrefs(prefs);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ================================================================ 994b: menu (COMPLETADO / SIGUIENTE) y victoria de la Operacion
        [EscenaDelCheck("SC_MainMenu")]
        static IEnumerator Bug994b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var prefs = R19aGuardarPrefs();
            foreach (var x in Esperar(0.8f)) yield return x;
            var mm = Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            if (mm == null) { R19aRestaurarPrefs(prefs); Fin("FALLO no hay MainMenuController"); yield break; }
            // Nivel 1 hecho (01:23), Nivel 2 sin hacer
            PlayerPrefs.SetInt(ProgresoDeNiveles.ClaveCompleto(1), 1); PlayerPrefs.SetFloat(ProgresoDeNiveles.ClaveMejorTiempo(1), 83f);
            ProgresoDeNiveles.Borrar(2);
            mm.MostrarNiveles();
            foreach (var x in Esperar(0.4f)) yield return x;
            string e1 = mm.TextoDeEstadoDelNivel(1), e2 = mm.TextoDeEstadoDelNivel(2);
            W8Ok(ref ok, e1.Contains(MainMenuController.TextoCompletado) && e1.Contains("01:23"), sb, $"tarjeta 1 '{e1}'");
            W8Ok(ref ok, e2.Contains(MainMenuController.TextoSiguiente) && mm.NivelResaltado(2) && !mm.NivelResaltado(1), sb, $"tarjeta 2 '{e2}' resaltada={mm.NivelResaltado(2)}");
            // El panel se cachea: al reabrirlo se repinta con el estado nuevo (sin Nivel 1 -> sin marcas)
            mm.PanelNiveles.SetActive(false);
            ProgresoDeNiveles.Borrar(1);
            mm.MostrarNiveles();
            foreach (var x in Esperar(0.2f)) yield return x;
            W8Ok(ref ok, mm.TextoDeEstadoDelNivel(1) == "" && mm.TextoDeEstadoDelNivel(2) == "" && !mm.NivelResaltado(2), sb, "al reabrir sin progreso no queda ninguna marca");
            mm.PanelNiveles.SetActive(false);

            // Victoria de la Operacion (Nivel 2)
            ProgresoDeNiveles.Borrar(2);
            SceneManager.LoadScene("SC_Operacion");
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 90f && OperacionDirector.Instancia == null) yield return null;
            if (OperacionDirector.Instancia == null) { R19aRestaurarPrefs(prefs); Fin("FALLO " + sb + "SC_Operacion no cargo en 90 s"); yield break; }
            foreach (var x in Esperar(4f)) yield return x;
            CinematicaDeOperacion.Saltar();
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            OperacionPrueba.Arrancar(6);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia; var outcome = GameOutcomeController.Activo;
            if (d == null || outcome == null || d.Fase != FaseOperacion.Extraer) { ModoDios.Poner(diosAntes); R19aRestaurarPrefs(prefs); Fin("FALLO " + sb + "no se llego a Extraer o falta el GameOutcomeController"); yield break; }
            d.SubirAlHeli();
            t0 = Time.realtimeSinceStartup;
            while (!outcome.IsShowing && Time.realtimeSinceStartup - t0 < 45f) yield return null;
            W8Ok(ref ok, outcome.IsShowing && d.Fase == FaseOperacion.Victoria, sb, $"victoria (fase {d.Fase})");
            W8Ok(ref ok, ProgresoDeNiveles.EstaCompleto(2) && ProgresoDeNiveles.MejorTiempo(2) > 0f, sb, $"Nivel 2 completo={ProgresoDeNiveles.EstaCompleto(2)} mejor={ProgresoDeNiveles.Formato(ProgresoDeNiveles.MejorTiempo(2))}");
            var filas = EstadisticasDeMision.Filas(true);
            bool fila = filas.Count > 0 && filas[0].Etiqueta == EstadisticasDeMision.EtiquetaNivel2Completado && filas[0].Valor.Contains("COMPLETADO");
            W8Ok(ref ok, fila, sb, $"primera fila del resultado '{(filas.Count > 0 ? filas[0].Etiqueta + " " + filas[0].Valor : "-")}'");
            ModoDios.Poner(diosAntes);
            R19aRestaurarPrefs(prefs);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ================================================================ 994e: tutorial simple v2
        [EscenaDelCheck("SC_Tutorial")]
        static IEnumerator Bug994e()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var prefs = R19aGuardarPrefs();
            foreach (var x in R19aEsperarTutorial(null, 15f)) yield return x;
            var tm = TutorialManager.Instance;
            if (tm == null) { R19aRestaurarPrefs(prefs); Fin("FALLO no hay TutorialManager"); yield break; }
            W8Ok(ref ok, TutorialManager.ModoSimple, sb, "modo simple");

            // --- 1. estructura: 8 pasos, mensajes cortos, <= 13 sub-pasos, ayuda a los 8 s
            var ids = new List<string>(); int subs = 0; string largo = ""; int maxLen = 0;
            foreach (var p in tm.Pasos)
            {
                ids.Add(p.Id);
                if (p.Subs == null) continue;
                subs += p.Subs.Length;
                foreach (var s in p.Subs)
                    if (s.Mensaje != null && s.Mensaje.Length > maxLen) { maxLen = s.Mensaje.Length; largo = p.Id; }
            }
            W8Ok(ref ok, tm.Total == TutorialManager.PasosSimples && tm.Total == 8, sb, $"Total={tm.Total}");
            W8Ok(ref ok, string.Join(",", ids) == string.Join(",", R19aIdsSimples), sb, "orden [" + string.Join(",", ids) + "]");
            W8Ok(ref ok, maxLen <= R19aMaxMensaje, sb, $"mensaje mas largo {maxLen} car. ({largo}; tope {R19aMaxMensaje})");
            W8Ok(ref ok, subs <= R19aMaxSubPasos, sb, $"{subs} sub-pasos (tope {R19aMaxSubPasos})");
            W8Ok(ref ok, Mathf.Approximately(TutorialManager.SegundosDeAyudaActual, TutorialManager.SegundosParaAyudaSimple) && Mathf.Approximately(TutorialManager.SegundosParaAyuda, 16f), sb,
                $"ayuda a los {TutorialManager.SegundosDeAyudaActual:0} s (completo sigue en {TutorialManager.SegundosParaAyuda:0})");

            // --- 2. entrar_tanque con [E]
            ModoDios.Poner(true);
            foreach (var x in R19aSaltarHasta(tm, "entrar_tanque", 60f)) yield return x;
            var drv = PlayerInputDriver.Activo;
            if (tm.PasoActual == null || tm.PasoActual.Id != "entrar_tanque" || drv == null || drv.Vehicle == null)
            { ModoDios.Poner(false); R19aRestaurarPrefs(prefs); Fin("FALLO " + sb + "no se llego a entrar_tanque con tanque"); yield break; }
            foreach (var x in Esperar(0.5f)) yield return x;
            W8Ok(ref ok, !drv.SoloRadial, sb, "entrar_tanque: SoloRadial=false ([E] habilitado)");

            // --- 3. canon_simple: la ola sale al sentarse en el canon (no antes)
            drv.EnterVehicle(drv.Vehicle);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 6f && (tm.PasoActual == null || tm.PasoActual.Id != "canon_simple")) yield return null;
            if (tm.PasoActual == null || tm.PasoActual.Id != "canon_simple")
            { ModoDios.Poner(false); R19aRestaurarPrefs(prefs); Fin("FALLO " + sb + "subir al tanque no paso a canon_simple (paso " + (tm.PasoActual != null ? tm.PasoActual.Id : "-") + ")"); yield break; }
            foreach (var x in Esperar(1.2f)) yield return x;
            bool sinOla = !tm.OlaDelCanonLanzada && tm.EnemigosVivosDeLaOla() == 0;
            W8Ok(ref ok, sinOla || drv.CurrentSeat == VehicleSeatRole.Gunner, sb, $"antes de sentarse en el canon no hay ola (vivos {tm.EnemigosVivosDeLaOla()}, asiento {drv.CurrentSeat})");
            drv.SwitchSeat(VehicleSeatRole.Gunner);
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 2f && tm.EnemigosVivosDeLaOla() < 3) yield return null;
            float tOla = Time.realtimeSinceStartup - t0;
            W8Ok(ref ok, tm.EnemigosVivosDeLaOla() >= 3, sb, $"al sentarse en el canon: {tm.EnemigosVivosDeLaOla()} enemigos de la ola en {tOla:0.0} s");
            foreach (var x in Esperar(1.3f)) yield return x;
            var vh = drv.Vehicle;
            W8Ok(ref ok, vh != null && vh.Health != null && Mathf.Approximately(vh.Health.FactorDeDano, TutorialManager.FactorDeDanoEnLasOlas), sb,
                $"dano reducido en la ola (tanque x{(vh != null && vh.Health != null ? vh.Health.FactorDeDano : -1f):0.00})");
            ModoDios.Poner(false);

            // --- 4. corrida del autoplayer en una escena limpia (sin modo dios: no se puede perder)
            var viejo = tm;
            SceneManager.LoadScene("SC_Tutorial");
            foreach (var x in R19aEsperarTutorial(viejo, 30f)) yield return x;
            tm = TutorialManager.Instance;
            if (tm == null || tm == viejo) { R19aRestaurarPrefs(prefs); Fin("FALLO " + sb + "no se recargo SC_Tutorial"); yield break; }
            TutorialManager.BorrarProgreso();
            foreach (var x in Esperar(1f)) yield return x;
            // Las referencias se toman ANTES: el panel final desactiva el driver y PlayerInputDriver.Activo puede quedar en null.
            var drvAuto = PlayerInputDriver.Activo;
            var squadAuto = new List<Soldier>();
            if (drvAuto != null && drvAuto.Squad != null) foreach (var s in drvAuto.Squad) if (s != null) squadAuto.Add(s);
            var tanqueAuto = drvAuto != null ? drvAuto.Vehicle : null;
            var ap = TutorialAutoPlayer.Lanzar(0, true, false);
            t0 = Time.realtimeSinceStartup;
            float tFin = -1f;
            while (Time.realtimeSinceStartup - t0 < R19aMaxCorridaSimple + 60f && ap != null && !ap.Cerrada)
            {
                if (tFin < 0f && tm != null && tm.Terminado) tFin = Time.realtimeSinceStartup - t0;
                yield return null;
            }
            if (tFin < 0f && tm != null && tm.Terminado) tFin = Time.realtimeSinceStartup - t0;
            if (ap == null) { R19aRestaurarPrefs(prefs); Fin("FALLO " + sb + "el autoplayer desaparecio"); yield break; }
            int buenos = ap.PasosOk + ap.PasosConReintento;
            W8Ok(ref ok, ap.Cerrada && ap.PasosFallidos == 0 && buenos >= TutorialManager.PasosSimples - 1 && ap.PasosSaltados <= 1, sb,
                $"autoplayer: OK={ap.PasosOk} reintento={ap.PasosConReintento} saltados={ap.PasosSaltados} (victoria) fallidos={ap.PasosFallidos}");
            W8Ok(ref ok, tFin > 0f && tFin <= R19aMaxCorridaSimple, sb, $"corrida en {tFin:0.0} s (tope {R19aMaxCorridaSimple:0})");
            int muertos = 0;
            foreach (var s in squadAuto) if (s == null || s.Health == null || !s.Health.IsAlive) muertos++;
            bool tanqueVivo = tanqueAuto != null && tanqueAuto.Health != null && tanqueAuto.Health.IsAlive;
            string vidaTanque = tanqueAuto != null && tanqueAuto.Health != null ? tanqueAuto.Health.Current + "/" + tanqueAuto.Health.MaxHealth : "sin tanque";
            W8Ok(ref ok, squadAuto.Count > 0 && muertos == 0 && tanqueVivo, sb, $"escuadra de {squadAuto.Count} sin bajas ({muertos}) y tanque vivo={tanqueVivo} ({vidaTanque})");
            R19aRestaurarPrefs(prefs);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ================================================================ 994f: el tutorial completo no cambio
        // Textos actuales (R17/R18) del completo: si alguien los toca, este check lo dice (R8). El completo de mira_tanque es la rama sin el prefijo del simple.
        static readonly string[] R19aTextosEntrarTanque =
        {
            "ENTRAR AL TANQUE (RADIAL)",
            "Camina hasta el tanque (columna naranja)", "Camina hasta el TANQUE marcado con la columna naranja (W A S D).", "Está al norte, al final del camino de tierra.",
            "Q → TANQUE → SUBIRME YO", "Estás cerca. MIRA al tanque y mantén [Q]: TANQUE aparece en dorado; elige SUBIRME YO (la cuarta opción). (En este paso [E] está desactivado para que practiques el radial.)", "Apunta al tanque antes de mantener Q. Hay que estar a menos de 7 m.",
        };
        static readonly string[] R19aTextosMiraTanque =
        {
            "MIRA DEL CAÑÓN Y LA METRALLETA",
            "Clic derecho: miras por la mira del cañón", "Con el CLIC DERECHO miras por el PERISCOPIO del cañón (en la Operación la mira ya viene puesta y [CLIC DERECHO] ALTERNA la mira; acá se mantiene): ventana rectangular con escala de distancia y escalera de elevación (no es la mira de a pie). El cañón gira lento hacia donde apuntas.", "Mantén el botón derecho estando en el asiento del cañón [2].",
            "[3] la metralleta: mirilla normal", "Suelta, aprieta [3]: la METRALLETA va en el puesto alto del tanque y usa la MIRILLA NORMAL, igual que las torretas fijas (sin zoom ni periscopio). Apunta con el mouse y mantén el CLIC IZQUIERDO para ráfagas.", "El [3] cambia de asiento; la metralleta no lleva mira óptica.",
            "Suelta y vuelve al cañón [2]", "Suelta el clic derecho y aprieta [2]: la cámara vuelve a la tercera persona.", "Suelta el botón derecho y aprieta 2.",
        };

        static List<string> R19aTextosDelPaso(TutorialManager.Paso p)
        {
            var l = new List<string>();
            if (p == null) return l;
            l.Add(p.Titulo);
            foreach (var s in p.Subs) { l.Add(s.Texto); l.Add(s.Mensaje); l.Add(s.Pista); }
            return l;
        }

        static string R19aDiferencia(List<string> real, string[] esperado)
        {
            if (real.Count != esperado.Length) return $"{real.Count} textos en vez de {esperado.Length}";
            for (int i = 0; i < esperado.Length; i++) if (real[i] != esperado[i]) return $"texto {i} cambio: '{real[i]}'";
            return "";
        }

        [EscenaDelCheck("SC_Tutorial")]
        static IEnumerator Bug994f()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var prefs = R19aGuardarPrefs();
            foreach (var x in R19aEsperarTutorial(null, 15f)) yield return x;
            var viejo = TutorialManager.Instance;
            if (viejo == null) { R19aRestaurarPrefs(prefs); Fin("FALLO no hay TutorialManager"); yield break; }
            foreach (var x in Esperar(1f)) yield return x;
            // El completo se pide como desde el menu: ModoSimple=false y se carga la escena de nuevo (el estatico vuelve a true al entrar a Play).
            TutorialManager.ModoSimple = false;
            SceneManager.LoadScene("SC_Tutorial");
            foreach (var x in R19aEsperarTutorial(viejo, 30f)) yield return x;
            var tm = TutorialManager.Instance;
            if (tm == null || tm == viejo) { TutorialManager.ModoSimple = true; R19aRestaurarPrefs(prefs); Fin("FALLO no se recargo SC_Tutorial en modo completo"); yield break; }
            W8Ok(ref ok, !TutorialManager.ModoSimple && tm.Total >= 36, sb, $"completo: Total={tm.Total}");
            TutorialManager.Paso entrar = null, mira = null; bool hayCanon = false;
            foreach (var p in tm.Pasos) { if (p.Id == "entrar_tanque") entrar = p; if (p.Id == "mira_tanque") mira = p; if (p.Id == "canon_simple") hayCanon = true; }
            W8Ok(ref ok, !hayCanon, sb, "sin canon_simple");
            string d1 = R19aDiferencia(R19aTextosDelPaso(entrar), R19aTextosEntrarTanque), d2 = R19aDiferencia(R19aTextosDelPaso(mira), R19aTextosMiraTanque);
            W8Ok(ref ok, entrar != null && d1 == "", sb, "textos de entrar_tanque identicos" + (d1 == "" ? "" : " (" + d1 + ")"));
            W8Ok(ref ok, mira != null && d2 == "", sb, "textos de mira_tanque identicos" + (d2 == "" ? "" : " (" + d2 + ")"));
            W8Ok(ref ok, Mathf.Approximately(TutorialManager.SegundosParaAyuda, 16f) && Mathf.Approximately(TutorialManager.SegundosDeAyudaActual, 16f), sb,
                $"ayuda a los {TutorialManager.SegundosDeAyudaActual:0} s");
            TutorialManager.ModoSimple = true;
            R19aRestaurarPrefs(prefs);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ================================================================ 994l: anillo de subida en el tutorial (punto 7)
        [EscenaDelCheck("SC_Tutorial")]
        static IEnumerator Bug994l()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var prefs = R19aGuardarPrefs();
            foreach (var x in R19aEsperarTutorial(null, 15f)) yield return x;
            var tm = TutorialManager.Instance;
            if (tm == null) { R19aRestaurarPrefs(prefs); Fin("FALLO no hay TutorialManager"); yield break; }
            ModoDios.Poner(true);
            foreach (var x in R19aSaltarHasta(tm, "entrar_tanque", 60f)) yield return x;
            var drv = PlayerInputDriver.Activo;
            if (tm.PasoActual == null || tm.PasoActual.Id != "entrar_tanque" || drv == null || drv.Vehicle == null)
            { ModoDios.Poner(false); R19aRestaurarPrefs(prefs); Fin("FALLO no se llego a entrar_tanque con tanque"); yield break; }
            foreach (var x in Esperar(0.8f)) yield return x;
            var v = drv.Vehicle; var a = AnilloDeSubida.Instancia;
            W8Ok(ref ok, AnilloDeSubida.EstaActivo && a != null && a.Objetivo == v.transform, sb, $"anillo activo={AnilloDeSubida.EstaActivo} sobre el tanque");
            if (a == null) { ModoDios.Poner(false); R19aRestaurarPrefs(prefs); Fin("FALLO " + sb); yield break; }
            W8Ok(ref ok, Mathf.Abs(a.RadioDeUso - drv.RadioDeInteraccion) <= 0.05f, sb, $"radio de uso {a.RadioDeUso:0.00} = radio de interaccion {drv.RadioDeInteraccion:0.00}");
            W8Ok(ref ok, a.TextoDelCartel == AnilloDeSubida.CartelDelTanque, sb, $"cartel '{a.TextoDelCartel}'");
            W8Ok(ref ok, tm.HayBalizaDelTanque && !tm.AnilloDeLaBalizaDelTanqueVisible, sb, "la baliza del tanque esta sin su aro (no se encima)");
            // sigue al tanque: se lo corre 3 m y el anillo va con el
            var antes = v.transform.position;
            var nueva = antes + v.transform.right * 3f;
            var rb = v.GetComponent<Rigidbody>();
            if (rb != null) rb.position = nueva;
            v.transform.position = nueva; Physics.SyncTransforms();
            foreach (var x in Esperar(0.3f)) yield return x;
            var pa = a.transform.position; var pv = v.transform.position; pa.y = pv.y = 0f;
            W8Ok(ref ok, Vector3.Distance(pa, pv) < 0.5f && Vector3.Distance(new Vector3(antes.x, 0f, antes.z), pv) > 2f, sb, $"sigue al tanque (desfase {Vector3.Distance(pa, pv):0.00} m)");
            // subir: onda de 3 aros y despues apagado
            int ondas = AnilloDeSubida.OndasLanzadas;
            drv.EnterVehicle(v);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 3f && AnilloDeSubida.OndasLanzadas == ondas) yield return null;
            bool enOnda = AnilloDeSubida.OndasLanzadas > ondas && AnilloDeSubida.Instancia != null && AnilloDeSubida.Instancia.EnOnda;
            W8Ok(ref ok, v.PlayerAboard && enOnda, sb, $"al subir: a bordo={v.PlayerAboard}, onda={enOnda}");
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 2f && AnilloDeSubida.EstaActivo) yield return null;
            float apagado = Time.realtimeSinceStartup - t0;
            W8Ok(ref ok, !AnilloDeSubida.EstaActivo, sb, $"apagado tras la onda ({apagado:0.00} s despues de empezar a esperar; onda {AnilloDeSubida.DuracionDeOnda:0.0} s)");
            ModoDios.Poner(false);
            R19aRestaurarPrefs(prefs);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
