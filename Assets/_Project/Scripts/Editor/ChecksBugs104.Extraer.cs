using System.Collections;
using System.Collections.Generic;
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
    // Tanda #104-#132, P8: Extraer y Victoria (#127 oleadas + helicoptero que cubre, #128 los milicianos suben, #129 himno nuevo).
    // Todos arrancan por el camino real: Arrancar(5, 1, 1) (radio tomada, 4 milicianos) y se acorta el reloj del mando (segundosDeResistencia)
    // para llegar a Extraer por FinalizarElMando. El ANTES se reproduce con interruptores estaticos de pruebas.
    public static partial class ChecksBugs065
    {
        const string P8Prefijo = "v3_";

        static void P8Limpiar(float resistenciaPrevia, bool diosAntes)
        {
            OperacionDirector.CoberturaEstacionaria = true; OperacionDirector.CoberturaEnSaltoDePrueba = false; OperacionDirector.MilicianosSuben = true;
            MusicDirector.CombateForzado = false;
            OperacionTerminal.PruebaToqueE = false; OperacionTerminal.PruebaMantenerE = false;
            var d = OperacionDirector.Instancia;
            if (d != null && resistenciaPrevia > 0f) d.segundosDeResistencia = resistenciaPrevia;
            ModoDios.Poner(diosAntes);
        }

        // Arranca en Resistir con la radio tomada, acorta el reloj y espera a que se llegue a Extraer. Devuelve false si no se llego.
        static IEnumerable P8LlegarAExtraer(float segundosDeMando, System.Action<bool> listo)
        {
            var d = OperacionDirector.Instancia;
            ArrancarEn(5, 1, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            ModoDios.Poner(true);
            d.segundosDeResistencia = segundosDeMando;
            foreach (var x in W9bHasta(() => d.Fase == FaseOperacion.Extraer, 90f)) yield return x;
            yield return null;
            listo(d.Fase == FaseOperacion.Extraer);
        }

        static List<Soldier> P8Aliados() => OperacionDirector.AliadosAEvacuar(true);

        // ---------------------------------------------------------------- #127
        static IEnumerator Bug127()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var d = OperacionDirector.Instancia; if (d == null) { Fin("FALLO no hay OperacionDirector"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            float resPrev = d.segundosDeResistencia; bool diosAntes = ModoDios.Activo;
            Soldier congelado = null;
            try
            {
                // ---- ANTES: sin la cobertura el helicoptero baja a posarse y no vienen oleadas ----
                OperacionDirector.CoberturaEstacionaria = false;
                bool listo = false;
                foreach (var x in P8LlegarAExtraer(6f, v => listo = v)) yield return x;
                P6Ok(ref ok, listo, sb, "ANTES: se llego a Extraer");
                foreach (var x in Esperar(12f)) yield return x;
                sb.Append($"ANTES (sin cobertura): helicoptero posado={d.HeliAterrizo}, oleadas={d.OleadasDeExtraer}, cobertura activa={d.CoberturaActiva} (esperado True/0/False); ");
                if (!d.HeliAterrizo || d.OleadasDeExtraer != 0 || d.CoberturaActiva) ok = false;
                OperacionDirector.CoberturaEstacionaria = true;

                // ---- DESPUES ----
                listo = false;
                foreach (var x in P8LlegarAExtraer(6f, v => listo = v)) yield return x;
                P6Ok(ref ok, listo, sb, "DESPUES: se llego a Extraer");
                var heli = d.heli; var zona = d.heliAterrizaje.position;
                P6Ok(ref ok, d.CoberturaActiva && !d.HeliAterrizo && heli.position.y - zona.y > 10f, sb, $"el helicoptero queda en estacionario a {heli.position.y - zona.y:0.0} m de altura sobre la zona (cobertura activa={d.CoberturaActiva})");
                P6Ok(ref ok, d.ScriptHeli.DanoDeCobertura >= 12 && Mathf.Approximately(d.ScriptHeli.RadioDeBlancoActual, OperacionDirector.RadioDeFuegoDelHeli), sb, $"fuego pesado: dano {d.ScriptHeli.DanoDeCobertura}/bala, radio {d.ScriptHeli.RadioDeBlancoActual:0} m");
                P6Ok(ref ok, MusicDirector.CombateForzado, sb, "musica de combate forzada al maximo");
                // El miliciano 4 queda congelado lejos (como si estuviera atascado) para que la cobertura dure los 40 s de la medicion; el resto de los aliados
                // no dispara (Pasivo), asi las bajas son del helicoptero.
                var mils = new List<Soldier>(d.Milicianos);
                congelado = mils.Count > 3 ? mils[3] : null;
                if (congelado != null && congelado.Brain != null) congelado.Brain.IsPossessedByPlayer = true;
                // El jugador y la escuadra ya estan en la zona (como cuando los lleva el jugador); los milicianos tambien, menos el congelado.
                int kz = 0;
                foreach (var a in P8Aliados())
                {
                    if (a == congelado) continue;
                    var yoA = PlayerInputDriver.Activo.Brain.Current;
                    if (a == yoA) Junto(a, zona, 6f); else Teletransportar(a, zona + new Vector3(-5f + (kz++ % 5) * 2.5f, 0f, 4f + (kz / 5) * 2f));
                    if (a.Brain != null && !a.Brain.IsPossessedByPlayer) a.Brain.Pasivo = true;
                }
                int disparos0 = d.ScriptHeli.DisparosHechos;
                float yaw0 = heli.eulerAngles.y;
                float t0 = Time.realtimeSinceStartup; bool cap1 = false, cap2 = false; float yawMaxDelta = 0f; int vivosMax = 0;
                while (Time.realtimeSinceStartup - t0 < 40f)
                {
                    yield return null;
                    float t = Time.realtimeSinceStartup - t0;
                    yawMaxDelta = Mathf.Max(yawMaxDelta, Mathf.Abs(Mathf.DeltaAngle(yaw0, heli.eulerAngles.y)));
                    vivosMax = Mathf.Max(vivosMax, d.EnemigosDeExtraerVivos);
                    if (!cap1 && t > 11f && d.EnemigosDeExtraerVivos >= 2)
                    {
                        cap1 = true;
                        RenderTemporal(P8Prefijo + "127_heli_cubre", zona + new Vector3(26f, 5f, -26f), zona + Vector3.up * 3f, 62f);
                        foreach (var x in CapturarPantalla(P8Prefijo + "127_pantalla.png")) yield return x;
                    }
                    if (!cap2 && t > 24f && d.EnemigosDeExtraerVivos >= 1)
                    {
                        cap2 = true;
                        RenderTemporal(P8Prefijo + "127_oleada_lejana", zona + new Vector3(-30f, 14f, 30f), zona + new Vector3(30f, 2f, -30f), 70f);
                    }
                }
                bool sigue = d.CoberturaActiva;
                float minDist = float.MaxValue; foreach (var o in d.OrigenesDeOlaDeExtraer) minDist = Mathf.Min(minDist, Vector3.Distance(new Vector3(o.x, 0f, o.z), new Vector3(zona.x, 0f, zona.z)));
                P6Ok(ref ok, d.OleadasDeExtraer >= 3, sb, $"{d.OleadasDeExtraer} oleadas en 40 s (se piden >= 3), {d.EnemigosDeExtraer} enemigos creados, vivos a la vez (max) {vivosMax}");
                P6Ok(ref ok, d.OrigenesDeOlaDeExtraer.Count >= 3 && minDist >= OperacionDirector.DistanciaMinimaDeOlaALaZona - 0.5f, sb, $"cada oleada nacio a >= {OperacionDirector.DistanciaMinimaDeOlaALaZona:0} m de la zona (la mas cercana a {minDist:0.0} m)");
                P6Ok(ref ok, d.BajasDelHeliEnExtraer >= 3, sb, $"el helicoptero registro {d.BajasDelHeliEnExtraer} bajas (se piden >= 3), disparos {d.ScriptHeli.DisparosHechos - disparos0}");
                P6Ok(ref ok, sigue && yawMaxDelta > 15f, sb, $"sigue cubriendo mientras falta un aliado (cobertura activa={sigue}); el helicoptero giro hasta {yawMaxDelta:0} grados para encarar a los atacantes");

                // Con todos en la zona baja y se posa: se los lleva a la zona (el jugador y su escuadra suelen venir solos; aca se los teletransporta).
                if (congelado != null && congelado.Brain != null) congelado.Brain.IsPossessedByPlayer = false;
                int k = 0;
                foreach (var a in P8Aliados()) { var yoA = PlayerInputDriver.Activo.Brain.Current; if (a == yoA) { Junto(a, zona, 6f); continue; } Teletransportar(a, zona + new Vector3(-5f + (k++ % 5) * 2.5f, 0f, 4f + (k / 5) * 2f)); }
                foreach (var x in W9bHasta(() => d.HeliAterrizo, 30f)) yield return x;
                P6Ok(ref ok, d.HeliAterrizo && !d.CoberturaActiva && Mathf.Abs(heli.position.y - zona.y) < 0.2f, sb, $"con todos los aliados en la zona el helicoptero aterriza (y={heli.position.y - zona.y:0.00}, cobertura activa={d.CoberturaActiva})");
                P6Ok(ref ok, !MusicDirector.CombateForzado, sb, "la musica forzada se libera al aterrizar");
            }
            finally
            {
                if (congelado != null && congelado.Brain != null) congelado.Brain.IsPossessedByPlayer = false;
                foreach (var a in P8Aliados()) if (a.Brain != null) a.Brain.Pasivo = false;
                P8Limpiar(resPrev, diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #128 (DESPUES)
        static IEnumerator Bug128() { foreach (var x in P8Embarque(true)) yield return x; }

        // #128 ANTES: con el interruptor apagado los milicianos se quedan (reproduce el reporte: sobrevivientes 3/7).
        static IEnumerator Bug128a() { foreach (var x in P8Embarque(false)) yield return x; }

        static IEnumerable P8Embarque(bool milicianosSuben)
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var d = OperacionDirector.Instancia; if (d == null) { Fin("FALLO no hay OperacionDirector"); yield break; }
            var drv = PlayerInputDriver.Activo;
            var sb = new StringBuilder(); bool ok = true;
            float resPrev = d.segundosDeResistencia; bool diosAntes = ModoDios.Activo;
            string et = milicianosSuben ? "DESPUES" : "ANTES (milicianos NO suben)";
            try
            {
                OperacionDirector.MilicianosSuben = milicianosSuben;
                bool listo = false;
                foreach (var x in P8LlegarAExtraer(8f, v => listo = v)) yield return x;
                P6Ok(ref ok, listo, sb, "se llego a Extraer");
                var mils = new List<Soldier>(d.Milicianos);
                var todos = new List<Soldier>();   // escuadra + milicianos, tal cual los pide el reporte (siete)
                foreach (var a in ActorRegistry.All)
                {
                    var s = a as Soldier;
                    if (s != null && s.Team == TeamId.Player && s.Role != RoleType.Civilian && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy) todos.Add(s);
                }
                P6Ok(ref ok, mils.Count == 4 && todos.Count == 7, sb, $"{mils.Count} milicianos + escuadra = {todos.Count} aliados vivos (se esperan 4 y 7)");
                var heli = d.heli; var zona = d.heliAterrizaje.position;
                // El jugador lleva a su escuadra a la zona (aca: se lo pone a 8 m del punto de aterrizaje y los aliados lo siguen).
                Junto(drv.Brain.Current, zona, 8f);

                // Esperar a que el helicoptero se pose (todos en la zona o tope de 75 s).
                float tZona = Time.realtimeSinceStartup;
                foreach (var x in W9bHasta(() => d.HeliAterrizo, 100f)) yield return x;
                float seg = Time.realtimeSinceStartup - tZona;
                int enZona = 0; foreach (var s in P8Aliados()) if (Vector3.Distance(new Vector3(s.transform.position.x, 0f, s.transform.position.z), new Vector3(zona.x, 0f, zona.z)) <= OperacionDirector.RadioDeLaZonaDeAterrizaje + 1f) enZona++;
                P6Ok(ref ok, d.HeliAterrizo, sb, $"el helicoptero se poso a los {seg:0} s ({enZona}/{P8Aliados().Count} de los que cuentan estaban en la zona)");
                foreach (var x in Esperar(0.5f)) yield return x;

                // Subir con [E] (camino real): el jugador junto al helicoptero.
                var yo = drv.Brain.Current;
                Junto(yo, heli.position, 6f);
                OperacionTerminal.PruebaToqueE = true;
                foreach (var x in W9bHasta(() => d.SubiendoAlHeli, 4f)) yield return x;
                OperacionTerminal.PruebaToqueE = false;
                P6Ok(ref ok, d.SubiendoAlHeli, sb, "[E] junto al helicoptero inicia el embarque");

                // Hasta el despegue: todos los que cuentan tienen que estar a bordo (apagados) cuando el helicoptero se levanta.
                float yPiso = heli.position.y; float t0 = Time.realtimeSinceStartup; bool despego = false; int aBordoAlDespegar = -1; int copias = -1; bool capEmb = false; int maxAfuera = 0;
                while (Time.realtimeSinceStartup - t0 < 90f && !despego)
                {
                    yield return null;
                    int afuera = 0; foreach (var s in todos) if (s != null && s.gameObject.activeInHierarchy) afuera++;
                    maxAfuera = Mathf.Max(maxAfuera, afuera);
                    if (!capEmb && afuera >= 3 && afuera <= todos.Count - 1 && Time.realtimeSinceStartup - t0 > 1.5f)
                    {
                        capEmb = true;
                        RenderTemporal(P8Prefijo + "128_" + (milicianosSuben ? "embarque" : "embarque_antes"), heli.position + new Vector3(-14f, 5f, -10f), heli.position + Vector3.up * 1.2f, 62f);
                    }
                    if (heli.position.y > yPiso + 0.4f)
                    {
                        despego = true;
                        int abordo = 0; foreach (var s in todos) if (s == null || !s.gameObject.activeInHierarchy) abordo++;
                        aBordoAlDespegar = abordo;
                        var tri = d.ScriptHeli.Tripulacion; copias = tri != null ? tri.CopiasDeEscuadra : -1;
                    }
                }
                int esperado = milicianosSuben ? todos.Count : 3;
                P6Ok(ref ok, despego && aBordoAlDespegar == esperado, sb, $"al despegar hay {aBordoAlDespegar} de {todos.Count} a bordo (se esperan {esperado}); el embarque tomo {Time.realtimeSinceStartup - t0:0.0} s, afuera a la vez (max) {maxAfuera}");
                int milicianosAfuera = 0; foreach (var m in mils) if (m != null && m.gameObject.activeInHierarchy) milicianosAfuera++;
                P6Ok(ref ok, milicianosSuben ? milicianosAfuera == 0 : milicianosAfuera == 4, sb, $"milicianos que quedaron en tierra: {milicianosAfuera} (se esperan {(milicianosSuben ? 0 : 4)})");
                P6Ok(ref ok, copias == Mathf.Min(esperado, 7), sb, $"copias sentadas en la cabina: {copias} (capacidad {TripulacionDelHeli.Capacidad})");
                if (milicianosSuben) RenderTemporal(P8Prefijo + "128_cabina", heli.position + heli.right * 3.2f + Vector3.up * 1.6f, heli.position + Vector3.up * 1.3f - heli.right * 0.2f, 55f);

                // Victoria: las estadisticas cuentan a los que subieron.
                var outcome = GameOutcomeController.Activo;
                foreach (var x in W9bHasta(() => outcome != null && outcome.IsShowing, 60f)) yield return x;
                var filas = EstadisticasDeMision.Filas(true);
                EstadisticasDeMision.Fila sup = default; bool hay = false;
                foreach (var f in filas) if (f.Etiqueta == "SUPERVIVIENTES") { sup = f; hay = true; }
                P6Ok(ref ok, d.Fase == FaseOperacion.Victoria && OperacionDirector.SubidosAlHeli == esperado && EstadisticasDeMision.SupervivientesForzados == esperado, sb,
                    $"Victoria: subidos={OperacionDirector.SubidosAlHeli}, SupervivientesForzados={EstadisticasDeMision.SupervivientesForzados} (se esperan {esperado})");
                P6Ok(ref ok, hay && sup.Numero == esperado && sup.Valor.Contains("/ 7"), sb, $"fila SUPERVIVIENTES = {(hay ? sup.Numero + " " + sup.Valor.Replace("{0}", "n") : "falta")} (se espera {esperado} / 7)");
                foreach (var x in Esperar(2.0f)) yield return x;
                foreach (var x in CapturarPantalla(P8Prefijo + "128_victoria_" + (milicianosSuben ? "7de7" : "3de7_antes") + ".png")) yield return x;
            }
            finally { P8Limpiar(resPrev, diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + et + ": " + sb);
        }

        // ---------------------------------------------------------------- #129
        static IEnumerator Bug129()
        {
            var sb = new StringBuilder(); bool ok = true;
            float[] datos = null; long ms = 0;
            var tarea = System.Threading.Tasks.Task.Run(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var r = SfxSintetico.HimnoEpicoDatos();
                sw.Stop(); ms = sw.ElapsedMilliseconds;
                return r;
            });
            float t0 = Time.realtimeSinceStartup;
            while (!tarea.IsCompleted && Time.realtimeSinceStartup - t0 < 30f) yield return null;
            if (!tarea.IsCompleted || tarea.IsFaulted) { Fin("FALLO la sintesis no termino o fallo: " + (tarea.Exception != null ? tarea.Exception.GetBaseException().Message : "timeout")); yield break; }
            datos = tarea.Result;
            int n = datos.Length / 2;
            float seg = n / (float)SfxSintetico.HimnoEpicoSR;
            double pico = 0, energia = 0; int nan = 0;
            for (int i = 0; i < datos.Length; i++)
            {
                float v = datos[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) { nan++; continue; }
                double a = System.Math.Abs(v); if (a > pico) pico = a; energia += (double)v * v;
            }
            double rms = System.Math.Sqrt(energia / datos.Length);
            // Canales distintos (estereo de verdad) y arco de intensidad: intro < tema B < climax.
            double difLR = 0, sumLR = 0;
            for (int i = 0; i < n; i++) { difLR += System.Math.Abs(datos[2 * i] - datos[2 * i + 1]); sumLR += System.Math.Abs(datos[2 * i]) + System.Math.Abs(datos[2 * i + 1]); }
            double bar = 4.0 * 60.0 / SfxSintetico.HimnoEpicoBpm;
            double RmsEntre(double a, double b)
            {
                int i0 = (int)(a * SfxSintetico.HimnoEpicoSR), i1 = Mathf.Min(n, (int)(b * SfxSintetico.HimnoEpicoSR)); double e = 0;
                for (int i = i0; i < i1; i++) e += (double)datos[2 * i] * datos[2 * i] + (double)datos[2 * i + 1] * datos[2 * i + 1];
                return System.Math.Sqrt(e / (2.0 * System.Math.Max(1, i1 - i0)));
            }
            double rIntro = RmsEntre(0, 2 * bar), rA = RmsEntre(2 * bar, 6 * bar), rB = RmsEntre(6 * bar, 10 * bar), rC = RmsEntre(10 * bar, 14 * bar);
            P6Ok(ref ok, seg >= 30f && seg <= 40f, sb, $"duracion {seg:0.0} s (30-40)");
            P6Ok(ref ok, datos.Length % 2 == 0 && sumLR > 0 && difLR / sumLR > 0.02, sb, $"estereo: canales distintos (diferencia relativa {difLR / System.Math.Max(1e-9, sumLR):0.000})");
            P6Ok(ref ok, pico <= 0.98 && nan == 0, sb, $"pico {pico:0.000} (<= 0,98), NaN/Inf={nan}");
            P6Ok(ref ok, rms >= 0.10 && rms <= 0.20, sb, $"RMS {rms:0.000} (0,10-0,20; la musica de combate del juego ronda 0,20 a volumen 0,35)");
            P6Ok(ref ok, rIntro < rA && rA < rB && rB < rC * 1.02, sb, $"arco de intensidad RMS intro {rIntro:0.000} < tema A {rA:0.000} < tema B {rB:0.000} <= climax {rC:0.000}");
            P6Ok(ref ok, ms < 4000, sb, $"sintesis en hilo: {ms} ms (< 4000)");
            // El clip se crea bien (hilo principal) y es estereo.
            var clip = SfxSintetico.ClipDelHimnoEpico(datos);
            P6Ok(ref ok, clip != null && clip.channels == 2 && clip.frequency == 44100 && Mathf.Abs(clip.length - seg) < 0.05f, sb, $"AudioClip: {(clip != null ? clip.channels + " canales, " + clip.frequency + " Hz, " + clip.length.ToString("0.0") + " s" : "null")}");
            if (clip != null) Object.Destroy(clip);
            // MusicDirector usa la pieza nueva (precarga en hilo).
            var mono = new float[n]; var izq = new float[n];
            for (int i = 0; i < n; i++) { mono[i] = 0.5f * (datos[2 * i] + datos[2 * i + 1]); izq[i] = datos[2 * i]; }
            GuardarOnda(P8Prefijo + "129_himno_onda.png", mono, SfxSintetico.HimnoEpicoSR, 1400, 260);
            sb.Append($"onda en {P8Prefijo}129_himno_onda.png");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
