using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Mision;
using SP.Operacion;
using SP.Presentation;

namespace SP.EditorTools
{
    // WP7: helicoptero de extraccion y cierre de la mision. #075 ondas de viento y polvo, #076 ametralladoras con artilleros,
    // #077 toma final desde el piso con pantalla de resultado translucida. Hay que estar en Play sobre SC_Operacion; validar cada
    // check en un Play fresco (el de #075 y el de #077 terminan la mision).
    public static partial class ChecksBugs065
    {
        static Vector3 Wp7Plano(Vector3 v) { v.y = 0f; return v; }

        // Pone a un enemigo de la reserva (apagado) en la posicion pedida, quieto, para usarlo de blanco.
        static Soldier Wp7ColocarEnemigo(Vector3 pos)
        {
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || s.gameObject.activeInHierarchy) continue;
                if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var h, 6f, UnityEngine.AI.NavMesh.AllAreas)) pos = h.position;
                s.transform.position = new Vector3(pos.x, s.transform.position.y, pos.z);
                s.gameObject.SetActive(true);
                ApoyoEnElPiso.Apoyar(s.transform);
                if (s.Brain != null) { s.Brain.Pasivo = true; s.Brain.Quieto = true; }
                return s;
            }
            return null;
        }

        static List<Health> Wp7HacerInvulnerable()
        {
            var l = new List<Health>();
            foreach (var a in ActorRegistry.All)
                if (a != null && a.Team == TeamId.Player && a.Health != null && !a.Health.Invulnerable) { a.Health.Invulnerable = true; l.Add(a.Health); }
            return l;
        }

        // ---------------------------------------------------------------- #075 ondas de viento y polvo
        static IEnumerator Bug075()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play (SC_Operacion)"); yield break; }
            var d = OperacionDirector.Instancia;
            if (d == null) { Fin("FALLO no hay OperacionDirector"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            float segPrev = d.segundosDeResistencia;
            OperacionDirector.ResistirSinMando = true;   // WP10: mide la bajada del helicoptero con el reloj de la fase vieja
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.0f)) yield return x;
            var heli = d.ScriptHeli;
            if (heli == null || !heli.gameObject.activeInHierarchy) { Fin("FALLO el helicoptero no esta activo en Resistir"); yield break; }
            var inv = Wp7HacerInvulnerable();
            d.segundosDeResistencia = 24f;
            try
            {
                // --- A) bajada (fase Resistir): altura < 20 m con rotor a fondo ---
                int muestras = 0, conOndas = 0; float minAlt = 999f, maxPolvo = 0f, maxRes = 0f; int maxOndas = 0, maxPart = 0, maxRespart = 0; bool volandoConPolvo = false;
                bool capPolvo = false, capOndas = false;
                float t0 = Time.realtimeSinceStartup;
                while (d.Fase == FaseOperacion.Resistir && Time.realtimeSinceStartup - t0 < 50f)
                {
                    float alt = heli.Altura;
                    if (alt < 20f)
                    {
                        muestras++;
                        minAlt = Mathf.Min(minAlt, alt);
                        maxPolvo = Mathf.Max(maxPolvo, heli.EmisionDePolvo); maxRes = Mathf.Max(maxRes, heli.EmisionDeResiduos);
                        int on = heli.Ondas != null ? heli.Ondas.Activas : 0;
                        maxOndas = Mathf.Max(maxOndas, on); if (on >= 3) conOndas++;
                        maxPart = Mathf.Max(maxPart, heli.ParticulasDePolvo); maxRespart = Mathf.Max(maxRespart, heli.ParticulasDeResiduos);
                        if (heli.Volando && heli.EmisionDePolvo > 0f) volandoConPolvo = true;
                        var piso = heli.PuntoDePiso;
                        if (!capPolvo && alt < 12f && heli.ParticulasDePolvo > 60 && on >= 3)
                        {
                            capPolvo = true;
                            var c = piso + new Vector3(-13f, 2.2f, -9f);
                            Wp7Cap(c, Quaternion.LookRotation(piso + Vector3.up * 3f - c), "v2_075_polvo.png", 60f);
                        }
                        if (!capOndas && alt < 14f && on >= 4)
                        {
                            capOndas = true;
                            var c = piso + new Vector3(0f, 23f, -27f);
                            Wp7Cap(c, Quaternion.LookRotation(piso + Vector3.up * 4f - c), "v2_075_ondas.png", 60f);
                            // Misma toma sin las particulas de polvo, para ver solo los anillos del piso.
                            var rends = heli.GetComponentsInChildren<ParticleSystemRenderer>(true);
                            foreach (var pr in rends) pr.enabled = false;
                            Wp7Cap(c, Quaternion.LookRotation(piso + Vector3.up * 4f - c), "v2_075_ondas_solo.png", 60f);
                            foreach (var pr in rends) pr.enabled = true;
                        }
                    }
                    yield return null;
                }
                bool bajadaOk = muestras > 10 && maxPolvo >= 40f && conOndas >= 3 && maxOndas >= 3;
                if (!bajadaOk) ok = false;
                sb.Append($"BAJADA: muestras<20m={muestras} alturaMin={minAlt:0.0} emisionPolvoMax={maxPolvo:0} residuos={maxRes:0.0}/s particulasPolvoMax={maxPart} residuosMax={maxRespart} anillosSimultaneosMax={maxOndas} (muestras con >=3 anillos: {conOndas}) volando+polvo={volandoConPolvo}{(bajadaOk ? "" : " (MAL)")}; ");
                if (maxPart > heli.MaximoDePolvo || maxRespart > Helicoptero.MaximoDeResiduos || maxOndas > OndasDeRotor.Cupo) { ok = false; sb.Append("TOPE SUPERADO; "); }

                // --- B) despegue: mismo criterio, sin depender de Volando ---
                if (d.Fase != FaseOperacion.Extraer) { ok = false; sb.Append($"no se llego a Extraer (fase={d.Fase}); "); }
                else
                {
                    foreach (var x in Esperar(0.5f)) yield return x;
                    d.SubirAlHeli();
                    int m2 = 0, on2 = 0; float polvo2 = 0f; int ond2 = 0; float altMax2 = 0f; bool vol2 = false;
                    t0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t0 < 13f)
                    {
                        float alt = heli.Altura;
                        if (heli.Volando && alt < 20f)
                        {
                            m2++; altMax2 = Mathf.Max(altMax2, alt);
                            polvo2 = Mathf.Max(polvo2, heli.EmisionDePolvo);
                            int on = heli.Ondas != null ? heli.Ondas.Activas : 0;
                            ond2 = Mathf.Max(ond2, on); if (on >= 3) on2++;
                            vol2 = true;
                        }
                        yield return null;
                    }
                    bool despegueOk = vol2 && m2 > 10 && polvo2 >= 40f && on2 >= 3;
                    if (!despegueOk) ok = false;
                    sb.Append($"DESPEGUE (Volando=true): muestras<20m={m2} alturaMax={altMax2:0.0} emisionPolvoMax={polvo2:0} anillosMax={ond2} (con >=3: {on2}){(despegueOk ? "" : " (MAL)")}; ");
                }
                sb.Append($"capturas: polvo={capPolvo} ondas={capOndas}");
            }
            finally
            {
                d.segundosDeResistencia = segPrev;
                foreach (var h in inv) if (h != null) h.Invulnerable = false;
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static void Wp7Cap(Vector3 pos, Quaternion rot, string archivo, float fov)
        {
            string e = CapturarDesde(pos, rot, archivo, 1280, 720, fov);
            if (e != "") Debug.LogWarning("[Wp7] " + e);
        }

        // ---------------------------------------------------------------- #076 ametralladoras con artilleros
        static IEnumerator Bug076()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play (SC_Operacion)"); yield break; }
            var d = OperacionDirector.Instancia;
            if (d == null) { Fin("FALLO no hay OperacionDirector"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(6);
            foreach (var x in Esperar(1.5f)) yield return x;
            var heli = d.ScriptHeli;
            if (heli == null || !heli.gameObject.activeInHierarchy) { Fin("FALLO el helicoptero no esta activo en Extraer"); yield break; }
            heli.Alerta(true); heli.DisparaCobertura = true;   // como cuando llega (ArrancarHeli)
            foreach (var x in Esperar(2.5f)) yield return x;    // el rotor llega a k > 0.6

            // Tripulacion visible: 2 artilleros (uno por pivote) y 2 sentados, dentro del volumen de la cabina.
            var tri = heli.Tripulacion;
            int artilleros = tri != null ? tri.Artilleros : -1, sentados = tri != null ? tri.SentadosBase : -1;
            bool visibles = true; var dentro = new StringBuilder();
            if (tri != null)
            {
                var todos = new List<GameObject>(tri.ListaDeArtilleros); todos.AddRange(tri.ListaDeSentados);
                foreach (var g in todos)
                {
                    if (g == null) continue;
                    var smr = g.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    bool vis = smr != null && smr.enabled && smr.sharedMesh != null && g.activeInHierarchy;
                    var l = heli.transform.InverseTransformPoint(g.transform.position);
                    bool enCabina = Mathf.Abs(l.x) < 1.3f && l.z > -3.1f && l.z < 0.9f && l.y > 0.9f && l.y < 1.6f;
                    if (!vis || !enCabina) visibles = false;
                    dentro.Append($"({l.x:0.00},{l.y:0.00},{l.z:0.00}){(vis && enCabina ? "" : " MAL[smr=" + (smr != null) + (smr != null ? " en=" + smr.enabled + " mesh=" + (smr.sharedMesh != null) : "") + " act=" + g.activeInHierarchy + " cab=" + enCabina + "]")} ");
                }
            }
            bool tripOk = artilleros == 2 && sentados == 2 && visibles && heli.Pivotes.Length == 2;
            if (!tripOk) ok = false;
            sb.Append($"TRIPULACION: artilleros={artilleros} sentados={sentados} pivotes={heli.Pivotes.Length} posiciones(local)={dentro}{(tripOk ? "" : "(MAL)")}; ");

            // Blanco: un enemigo a ~35 m del helicoptero, hacia el costado derecho (rumbo ~60 grados desde la proa).
            var dirBlanco = heli.transform.TransformDirection(Quaternion.Euler(0f, 60f, 0f) * Vector3.forward);
            var blanco = Wp7ColocarEnemigo(heli.transform.position + Wp7Plano(dirBlanco).normalized * 35f);
            if (blanco == null) { Fin("FALLO no hay un enemigo de reserva para usar de blanco"); yield break; }
            float distBlanco = Wp7Plano(blanco.transform.position - heli.transform.position).magnitude;
            var yawMin = new float[heli.Pivotes.Length]; var yawMax = new float[heli.Pivotes.Length];
            for (int i = 0; i < heli.Pivotes.Length; i++) yawMin[i] = yawMax[i] = heli.Pivotes[i].YawLocal;
            var disparos = new List<(float t, Vector3 origen, PivoteMetralleta p, float distBoca)>();
            Action<Helicoptero, PivoteMetralleta, Vector3, Vector3> escucha = (h, p, o, dir) => disparos.Add((Time.realtimeSinceStartup, o, p, (o - p.Boca.position).magnitude));
            Helicoptero.AlDisparar += escucha;
            int casquillos0 = CasquillosDeMetralleta.Expulsados;
            int fx0 = SpriteFx.Lanzados;
            bool capturado = false;
            float t0 = Time.realtimeSinceStartup;
            try
            {
                while (Time.realtimeSinceStartup - t0 < 5f)
                {
                    for (int i = 0; i < heli.Pivotes.Length; i++) { float y = heli.Pivotes[i].YawLocal; yawMin[i] = Mathf.Min(yawMin[i], y); yawMax[i] = Mathf.Max(yawMax[i], y); }
                    if (!capturado && disparos.Count >= 4 && heli.UltimoPivoteQueDisparo != null)
                    {
                        capturado = true;
                        // Desde el costado del pivote que dispara, mirando hacia la puerta.
                        var pv = heli.UltimoPivoteQueDisparo;
                        var lado = heli.transform.right * pv.lado;
                        var foco = pv.transform.position + Vector3.up * 1.4f;
                        var c = foco + lado * 7.5f + heli.transform.forward * 2.5f + Vector3.up * 0.6f;
                        Wp7Cap(c, Quaternion.LookRotation(foco - c), "v2_076_artilleros.png", 45f);
                        var c2 = foco + lado * 11f - heli.transform.forward * 5f + Vector3.up * 2.5f;
                        Wp7Cap(c2, Quaternion.LookRotation(foco - c2), "v2_076_artilleros_b.png", 50f);
                    }
                    yield return null;
                }
            }
            finally { Helicoptero.AlDisparar -= escucha; }

            // Disparos: origen a menos de 0,3 m de la boca de un pivote, y distinto del viejo punto fijo.
            int cerca = 0; float maxDist = 0f; float minVieja = 999f;
            foreach (var s in disparos)
            {
                maxDist = Mathf.Max(maxDist, s.distBoca); if (s.distBoca < 0.3f) cerca++;
                var vieja = heli.transform.position + heli.transform.right * 1.3f + Vector3.up * 1.2f;
                minVieja = Mathf.Min(minVieja, (s.origen - vieja).magnitude);
            }
            float cambioYaw = 0f; for (int i = 0; i < yawMin.Length; i++) cambioYaw = Mathf.Max(cambioYaw, yawMax[i] - yawMin[i]);
            // Cadencia: primera rafaga (disparos con menos de 0,3 s entre ellos).
            float cadencia = 0f; int enRafaga = 0;
            if (disparos.Count > 2)
            {
                var porPivote = disparos.FindAll(x => x.p == disparos[0].p);
                float ini = porPivote[0].t, fin = ini; enRafaga = 1;
                for (int i = 1; i < porPivote.Count; i++) { if (porPivote[i].t - fin > 0.3f) break; fin = porPivote[i].t; enRafaga++; }
                cadencia = enRafaga > 1 ? (enRafaga - 1) / Mathf.Max(0.01f, fin - ini) : 0f;
            }
            bool tiroOk = cerca >= 1 && cerca == disparos.Count && cambioYaw >= 10f && minVieja > 0.3f;
            if (!tiroOk) ok = false;
            sb.Append($"TIRO: blanco a {distBlanco:0} m, disparos en 5 s={disparos.Count} (con origen < 0,3 m de la boca: {cerca}; dist max a la boca {maxDist:0.000} m; el origen dista {minVieja:0.0} m del viejo punto fijo), cambio de yaw {cambioYaw:0.0} grados (pivotes {Inv($"{yawMin[0]:0}..{yawMax[0]:0} y {(yawMin.Length > 1 ? yawMin[1] : 0f):0}..{(yawMax.Length > 1 ? yawMax[1] : 0f):0}")}), cadencia de la rafaga {cadencia:0.0}/s con {enRafaga} tiros{(tiroOk ? "" : " (MAL)")}; ");
            bool cadOk = cadencia >= 6f && cadencia <= 9.5f;
            if (!cadOk) { ok = false; sb.Append("CADENCIA FUERA DE RANGO; "); }
            int cas = CasquillosDeMetralleta.Expulsados - casquillos0, fx = SpriteFx.Lanzados - fx0;
            bool efectosOk = cas >= 3 && fx >= 3;
            if (!efectosOk) ok = false;
            var clip = GenericSfx.GetWeaponShot(WeaponKind.Heavy);
            sb.Append($"EFECTOS: casquillos={cas} (activos {CasquillosDeMetralleta.Activos}/{CasquillosDeMetralleta.Cupo}) sprites de fogonazo/humo={fx} clip='{(clip != null ? clip.name : "-")}'{(efectosOk ? "" : " (MAL)")}; capturas={capturado}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #077 toma final y pantalla de resultado translucida
        // Wp7Reales = true: antes de subir se activan 4 enemigos de la reserva detras del helipuerto (como los que quedan de las
        // oleadas) para probar el camino "enemigos reales"; con false la toma usa figurantes de reserva.
        public static bool Wp7Reales { get; set; }

        static IEnumerator Bug077()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play (SC_Operacion)"); yield break; }
            var d = OperacionDirector.Instancia;
            if (d == null) { Fin("FALLO no hay OperacionDirector"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(6);
            foreach (var x in Esperar(1.5f)) yield return x;
            var heli = d.ScriptHeli;
            var outcome = GameOutcomeController.Activo;
            if (heli == null || outcome == null) { Fin("FALLO sin helicoptero o sin GameOutcomeController"); yield break; }
            int reales = 0;
            if (Wp7Reales)
            {
                var rumbo = Wp7Plano(heli.transform.forward).normalized;
                var lat = Vector3.Cross(Vector3.up, rumbo);
                for (int i = 0; i < 4; i++)
                    if (Wp7ColocarEnemigo(heli.transform.position - rumbo * (24f + i * 3f) + lat * ((i - 1.5f) * 4f)) != null) reales++;
                foreach (var x in Esperar(0.5f)) yield return x;
            }
            d.SubirAlHeli();
            float t0 = Time.realtimeSinceStartup;
            bool tomaVista = false, capToma = false, capMitad = false, hudOculto = false, capIni = false, capFin = false; float tIniToma = 0f;
            int maxDisparando = 0; float alturaMax = 0f;
            while (!outcome.IsShowing && Time.realtimeSinceStartup - t0 < 40f)
            {
                if (d.TomaFinalActiva)
                {
                    if (!tomaVista) tIniToma = Time.realtimeSinceStartup;
                    tomaVista = true;
                    float enToma = Time.realtimeSinceStartup - tIniToma;
                    if (!capIni && enToma > 0.4f) { capIni = true; foreach (var x in CapturarPantalla("v2_077_toma_inicio.png")) yield return x; }
                    if (!capFin && enToma > 3.4f) { capFin = true; foreach (var x in CapturarPantalla("v2_077_toma_fin.png")) yield return x; }
                    alturaMax = Mathf.Max(alturaMax, d.AlturaDeCamaraSobrePiso);
                    maxDisparando = Mathf.Max(maxDisparando, d.TiradoresDisparandoEnPantalla(1f));
                    if (!capToma && d.DisparosDeLaTomaFinal >= 12 && maxDisparando >= 2)
                    {
                        capToma = true;
                        foreach (var x in CapturarPantalla("v2_077_toma.png")) yield return x;
                    }
                }
                yield return null;
            }
            if (!outcome.IsShowing) { Fin("FALLO la victoria no llego en 40 s (fase=" + d.Fase + ")"); yield break; }
            sb.Append($"TOMA{(Wp7Reales ? $" (con {reales} enemigos reales activos)" : "")}: activa={tomaVista} tiradores={d.TiradoresFinales.Count} (figurantes de reserva {d.FigurantesDeLaTomaFinal}) disparos={d.DisparosDeLaTomaFinal} alturaDeCamaraSobrePiso(max)={alturaMax:0.00} m, tiradores disparando dentro del frustum (max)={maxDisparando}, distancia camara-heli {d.DistanciaInicialCamaraHeli:0} m al inicio; ");
            bool tomaOk = tomaVista && alturaMax > 0f && alturaMax < 2.5f && maxDisparando >= 2;
            if (!tomaOk) ok = false;

            // Pantalla de resultado: translucida y en camara lenta; la toma sigue viva detras.
            var panel = outcome.transform.Find("VictoryPanel");
            var img = panel != null ? panel.GetComponent<Image>() : null;
            while (PantallaDeResultado.FilasMostradas < 4 && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            if (!capMitad) { capMitad = true; foreach (var x in CapturarPantalla("v2_077_victoria_mitad.png")) yield return x; }
            while (PantallaDeResultado.Animando && Time.realtimeSinceStartup - t0 < 70f) yield return null;
            foreach (var x in Esperar(0.4f)) yield return x;
            foreach (var x in CapturarPantalla("v2_077_victoria.png")) yield return x;
            float alfaImg = img != null ? img.color.a : -1f;
            float escala = Time.timeScale;
            float distMax = d.DistanciaMaximaCamaraHeli;
            var canvasHud = outcome.GetComponentInParent<Canvas>();
            hudOculto = canvasHud != null && canvasHud.rootCanvas != null && !canvasHud.rootCanvas.enabled;
            bool sinFundido = !d.FundidoActivo && GameObject.Find("OperacionFundido") == null && d.Fase == FaseOperacion.Victoria;   // sin fundido a negro (mismo criterio que ValidacionBugs "FINAL fundido se retira")
            if (!sinFundido) { ok = false; sb.Append("QUEDO UN FUNDIDO O LA FASE NO ES Victoria; "); }
            bool resultadoOk = alfaImg >= 0f && alfaImg <= 0.6f && outcome.AlfaFondoDeVictoria <= 0.6f && escala > 0f && !outcome.VictoriaCongelada && hudOculto;
            if (!resultadoOk) ok = false;
            sb.Append($"RESULTADO: alfa del panel={alfaImg:0.00} (ultimo pedido {PantallaDeResultado.AlfaDeFondoUltimo:0.00}) timeScale={escala:0.00} congelada={outcome.VictoriaCongelada} HUD apagado={hudOculto} distancia maxima camara-heli {distMax:0} m{(resultadoOk ? "" : " (MAL)")}; ");

            // Copias sentadas al embarcar.
            var tri = heli.Tripulacion;
            int copias = tri != null ? tri.CopiasDeEscuadra : -1;
            bool copiasOk = copias >= 1 && copias == Mathf.Min(OperacionDirector.SubidosAlHeli, 4);
            if (!copiasOk) ok = false;
            sb.Append($"EMBARQUE: subidos={OperacionDirector.SubidosAlHeli} copias sentadas={copias}{(copiasOk ? "" : " (MAL)")}; ");

            // Compatibilidad: ShowVictory() sin parametros (SC_Gameplay, tutorial) sigue en timeScale 0 y alfa 1.
            var campo = typeof(GameOutcomeController).GetField("shown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (campo != null)
            {
                campo.SetValue(outcome, false);
                outcome.ShowVictory();
                foreach (var x in Esperar(1.3f)) yield return x;
                float alfaViejo = img != null ? img.color.a : -1f;
                bool compat = Time.timeScale == 0f && outcome.AlfaFondoDeVictoria >= 0.999f && outcome.VictoriaCongelada && alfaViejo >= 0.99f;
                if (!compat) ok = false;
                sb.Append($"COMPAT ShowVictory(): timeScale={Time.timeScale:0.00} alfa={alfaViejo:0.00} congelada={outcome.VictoriaCongelada}{(compat ? "" : " (MAL)")}; ");
            }
            else { ok = false; sb.Append("COMPAT: no se encontro el campo shown; "); }
            Time.timeScale = 1f;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
