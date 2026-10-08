using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;

namespace SP.EditorTools
{
    // Tanda #104-#132, P9: cinematica de apertura con rapel desde el helicoptero (#123).
    // El arnes de checks entra a un Play fresco y deja asentar 4 s (el rapel dura 5 s y el arnes lo salta antes de cada check), asi que el check la
    // vuelve a lanzar con ReiniciarPorPrueba (mismo camino que el Start: soldados en su lugar de inicio, sin OperacionPrueba.Arrancar). El Play fresco
    // real (la cinematica arrancando sola al entrar) se mide por CLI aparte (ver el informe 07).
    public static partial class ChecksBugs065
    {
        const string P9Prefijo = "v4_";   // v4: tomas nuevas (helicoptero en cuadro >= 4 s) y sonido del rotor con fundidos

        static IEnumerator Bug123()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo;
            if (d == null || drv == null) { Fin("FALLO no hay director o driver"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            bool opVistaAntes = CinematicaDeOperacion.YaVista;
            try
            {
                // Posiciones de inicio de la escuadra (antes de la cinematica) y zona de inicio.
                var inicio = new Dictionary<Soldier, Vector3>();
                foreach (var s in OperacionDirector.EscuadraDeLaOperacion()) inicio[s] = s.transform.position;
                var zona = d.entradas[0].position;
                P6Ok(ref ok, inicio.Count == 3, sb, $"{inicio.Count} soldados de la escuadra en su lugar de inicio");

                // ---- A) corre 5 +- 1 s, con las tres tomas, cuerdas, tres soldados colgando y sacudida ----
                var cin = CinematicaDeRapel.ReiniciarPorPrueba(false);
                // Muestra inmediata (el primer cuadro de la rutina ya corrio): el helicoptero se activo en silencio, sin golpe de sonido.
                float tIni = cin.Tiempo, veIni = d.ScriptHeli != null ? d.ScriptHeli.VolumenExtra : -1f, vrIni = d.ScriptHeli != null ? d.ScriptHeli.VolumenDelRotor : -1f;
                yield return null; yield return null;
                P6Ok(ref ok, CinematicaDeRapel.Activa && cin != null, sb, "arranca la cinematica de rapel");
                P6Ok(ref ok, !drv.enabled && SP.Ai.AiBrain.IAPausada && (drv.Rig == null || !drv.Rig.enabled), sb, $"bloqueo total: driver apagado={!drv.enabled}, IA pausada={SP.Ai.AiBrain.IAPausada}");
                var heli = d.heli;
                P6Ok(ref ok, heli != null && heli.gameObject.activeInHierarchy, sb, "el helicoptero esta en escena durante la cinematica");
                float t0 = Time.realtimeSinceStartup;
                int maxCuerdas = 0, maxColgando = 0, maxSuelo = 0; var tomas = new HashSet<int>(); float velMax = 0f; float tUltimo = 0f; float temblor = 0f, pasoMax = 0f; Vector3 camPrev = Vector3.zero; bool primera = true;
                Vector3 heliPrev = heli.position; float tPrev = 0f; float alturaEnHover = 0f;
                var momentos = new float[] { 0.5f, 1.5f, 2.5f, 3.5f, 4.5f, 5.5f, 8.2f, 11.4f };
                // Visibilidad del helicoptero (v4): proyectado con la camara de la cinematica, dentro del viewport (sin las barras de cine) y a menos de 120 m.
                float segEnCuadro = 0f, segEnCuadroSeguidos = 0f, mejorRacha = 0f; bool estabaEnCuadro = false; float tVisPrev = 0f; var scriptHeli = d.ScriptHeli;
                float volT0 = -1f, volMax2s = 0f, volMax10 = -1f, volRealT0 = -1f; bool vio2s = false, vio10 = false; float volRealMax = 0f; bool primeraMuestraDeVolumen = false; float tPrimera = tIni;
                if (tIni < 1.0f && veIni <= CinematicaDeRapel.VolumenDelRotorEn(tIni) + 0.03f) { volT0 = veIni; volRealT0 = vrIni; }   // si el primer cuadro llega tarde (editor saturado) vale lo que marca la curva en ese t
                int siguienteFoto = 0;
                while (CinematicaDeRapel.Activa && Time.realtimeSinceStartup - t0 < 14f)
                {
                    yield return null;
                    if (cin == null) break;
                    float t = cin.Tiempo; tUltimo = t;
                    // ---- visibilidad y volumen en cada cuadro ----
                    {
                        var camC = cin.Camara; bool enCuadro = false;
                        if (camC != null && heli != null)
                        {
                            var vp = camC.WorldToViewportPoint(heli.position + Vector3.up * 0.5f);
                            float dist = Vector3.Distance(camC.transform.position, heli.position);
                            enCuadro = vp.z > 0f && vp.x > 0.02f && vp.x < 0.98f && vp.y > 0.10f && vp.y < 0.90f && dist < 120f;
                        }
                        float dv = Mathf.Max(0f, t - tVisPrev);
                        if (enCuadro && estabaEnCuadro) { segEnCuadro += dv; segEnCuadroSeguidos += dv; mejorRacha = Mathf.Max(mejorRacha, segEnCuadroSeguidos); }
                        else if (!enCuadro) segEnCuadroSeguidos = 0f;
                        estabaEnCuadro = enCuadro; tVisPrev = t;
                        if (scriptHeli != null)
                        {
                            float ve = scriptHeli.VolumenExtra, vr = scriptHeli.VolumenDelRotor;
                            // t < 0,1 s; si el primer cuadro del Play llega tarde (carga de la escena), vale la primera muestra mientras siga sobre la curva (smoothstep).
                            if (t < 0.1f || (primeraMuestraDeVolumen && t < 0.3f && ve <= CinematicaDeRapel.VolumenDelRotorEn(t) + 0.03f)) { volT0 = Mathf.Max(volT0, ve); volRealT0 = Mathf.Max(volRealT0, vr); }
                            if (primeraMuestraDeVolumen) { primeraMuestraDeVolumen = false; tPrimera = t; }
                            if (t > 1.9f && t < 2.1f) { vio2s = true; volMax2s = Mathf.Max(volMax2s, ve); }
                            if (t > 10f) { vio10 = true; volMax10 = Mathf.Max(volMax10, ve); }
                            volRealMax = Mathf.Max(volRealMax, vr);
                        }
                    }
                    tomas.Add(cin.Toma);
                    maxCuerdas = Mathf.Max(maxCuerdas, cin.CuerdasVisibles); maxColgando = Mathf.Max(maxColgando, cin.CopiasColgando); maxSuelo = Mathf.Max(maxSuelo, cin.SoldadosEnElSuelo);
                    float dtt = t - tPrev; if (dtt > 0.001f && t > 0.25f && t < 5.6f) { velMax = Mathf.Max(velMax, Vector3.Distance(heli.position, heliPrev) / dtt); }   // entre 0,25 y 5,6 s (llegada): el primer cuadro salta desde la posicion de reposo y despues viene la salida
                    heliPrev = heli.position; tPrev = t;
                    if (t > 6.0f && t < 9.0f) alturaEnHover = Mathf.Max(alturaEnHover, heli.position.y - (zona.y - 0.8f));
                    var cp = cin.PosicionDeCamara;
                    if (!primera && t > 6.0f && t < 9.7f) { var paso = Vector3.Distance(cp, camPrev); temblor += paso; if (dtt > 0.001f) pasoMax = Mathf.Max(pasoMax, paso / dtt); }   // movimiento de camara dentro de la toma 2 (orbita lenta, sin sacudida)
                    camPrev = cp; primera = false;
                    if (siguienteFoto < momentos.Length && t >= momentos[siguienteFoto])
                    {
                        string nombre = P9Prefijo + "123_rapel_" + (siguienteFoto + 1) + "_t" + momentos[siguienteFoto].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', '_') + ".png";
                        siguienteFoto++;
                        foreach (var x in CapturarPantalla(nombre)) yield return x;
                    }
                }
                float durReal = Time.realtimeSinceStartup - t0;
                P6Ok(ref ok, tUltimo >= 12.0f && tUltimo <= 12.8f && durReal >= 8f && durReal <= 40f, sb, $"duracion de la cinematica {tUltimo:0.00} s de juego ({durReal:0.0} s reales con las capturas; se piden ~12,5)");
                P6Ok(ref ok, tomas.Contains(1) && tomas.Contains(2) && tomas.Contains(3) && tomas.Contains(4), sb, $"cuatro tomas ({string.Join(",", tomas)})");
                P6Ok(ref ok, maxCuerdas >= 3 && maxColgando >= 3, sb, $"hasta {maxCuerdas} cuerdas a la vez y {maxColgando} soldados colgando (se piden 3 y 3)");
                P6Ok(ref ok, velMax > 30f, sb, $"el helicoptero entra a {velMax:0} m/s (rapido: > 30; frena desde 42 m/s sobre la zona)");
                P6Ok(ref ok, alturaEnHover > 8f && alturaEnHover < 18f, sb, $"el helicoptero se estaciona a {alturaEnHover:0.0} m sobre el piso (cuerdas de ~{CinematicaDeRapel.AlturaDeRapel:0} m)");
                P6Ok(ref ok, temblor > 0.3f && pasoMax < 2.5f, sb, $"la camara orbita despacio y SIN sacudida en la toma 2 (recorrido {temblor:0.00} m, velocidad maxima {pasoMax:0.00} m/s < 2,5)");
                P6Ok(ref ok, maxSuelo >= 3, sb, $"{maxSuelo} soldados tocaron el piso");
                P6Ok(ref ok, segEnCuadro >= 4.0f, sb, $"VISIBILIDAD: el helicoptero esta en cuadro y a < 120 m {segEnCuadro:0.00} s acumulados (mejor racha {mejorRacha:0.00} s; se piden >= 4,0)");
                P6Ok(ref ok, volT0 >= 0f && volT0 <= CinematicaDeRapel.VolumenDelRotorEn(tPrimera) + 0.03f && volRealT0 >= 0f && volRealT0 <= CinematicaDeRapel.VolumenDelRotorEn(tPrimera) + 0.03f, sb, $"VOLUMEN al arrancar (muestra a t={tPrimera:0.000} s): VolumenExtra={volT0:0.000}, AudioSource={volRealT0:0.000} (se pide ~0, sobre la curva de entrada: sin golpe al activar)");
                P6Ok(ref ok, vio2s && volMax2s >= 0.95f, sb, $"VOLUMEN maximo en 1,9<t<2,1 s: {volMax2s:0.000} (se piden >= 0,95)");
                P6Ok(ref ok, vio10 && volMax10 < 0.5f, sb, $"VOLUMEN a t>10 s: maximo {volMax10:0.000} (se piden < 0,5; baja suave hasta ~0 al final)");

                // ---- B) al final: los 3 en el suelo en la zona de inicio, control del jugador (Kes), helicoptero fuera ----
                yield return null; yield return null;
                bool todosAbajo = true; float maxDesvio = 0f; int visibles = 0;
                foreach (var kv in inicio)
                {
                    var s = kv.Key; if (s == null) { todosAbajo = false; continue; }
                    if (Mathf.Abs(s.transform.position.y - 0.8f) > 0.1f) todosAbajo = false;
                    maxDesvio = Mathf.Max(maxDesvio, Vector3.Distance(s.transform.position, kv.Value));
                    bool vis = false; foreach (var r in s.GetComponentsInChildren<Renderer>(true)) if (r.enabled && r.gameObject.activeInHierarchy && r.GetComponent<ParticleSystem>() == null && r.GetComponent<TrailRenderer>() == null) { vis = true; break; }
                    if (vis) visibles++;
                    if (Vector3.Distance(new Vector3(s.transform.position.x, 0f, s.transform.position.z), new Vector3(zona.x, 0f, zona.z)) > 20f) todosAbajo = false;
                }
                P6Ok(ref ok, todosAbajo && visibles == inicio.Count && maxDesvio < 0.3f, sb, $"al final los {visibles}/{inicio.Count} soldados estan visibles en el suelo (y = 0,8 +- 0,1) en la zona de inicio (movieron {maxDesvio:0.00} m de su lugar)");
                var yo = drv.Brain.Current;
                P6Ok(ref ok, drv.enabled && (drv.Rig == null || drv.Rig.enabled) && !SP.Ai.AiBrain.IAPausada && yo != null && yo.Role == RoleType.Flanker, sb, $"el control es del jugador: driver activo={drv.enabled}, IA pausada={SP.Ai.AiBrain.IAPausada}, poseido={(yo != null ? yo.DisplayName + " (" + yo.Role + ")" : "nadie")}");
                P6Ok(ref ok, !heli.gameObject.activeInHierarchy && GameObject.Find("RapelCuerda_" + yo.name) == null && CinematicaDeRapel.YaVista, sb, "el helicoptero, las copias y las cuerdas se retiran y queda marcada como vista");
                P6Ok(ref ok, scriptHeli != null && Mathf.Approximately(scriptHeli.VolumenExtra, 1f), sb, $"al terminar el volumen del helicoptero vuelve a 1 (se reusa en Resistir): {(scriptHeli != null ? scriptHeli.VolumenExtra : -1f):0.00}");
                foreach (var x in CapturarPantalla(P9Prefijo + "123_despues_control.png")) yield return x;

                // ---- C) saltar con [Espacio] 1 s ----
                var cin2 = CinematicaDeRapel.ReiniciarPorPrueba(false);
                yield return null; yield return null;
                foreach (var x in Esperar(0.5f)) yield return x;
                CinematicaDeRapel.PruebaMantenerEspacio = true;
                float tS = Time.realtimeSinceStartup;
                while (CinematicaDeRapel.Activa && Time.realtimeSinceStartup - tS < 3f) yield return null;
                float tSalto = Time.realtimeSinceStartup - tS;
                CinematicaDeRapel.PruebaMantenerEspacio = false;
                yield return null;
                P6Ok(ref ok, !CinematicaDeRapel.Activa && CinematicaDeRapel.UltimaFueSaltada && tSalto >= 0.8f && tSalto <= 1.8f, sb, $"mantener [Espacio] la salta a los {tSalto:0.0} s (se espera ~1)");
                P6Ok(ref ok, drv.enabled && !SP.Ai.AiBrain.IAPausada && !heli.gameObject.activeInHierarchy && visibles == inicio.Count, sb, "tras saltar: control devuelto, helicoptero apagado, soldados visibles");
                P6Ok(ref ok, scriptHeli != null && Mathf.Approximately(scriptHeli.VolumenExtra, 1f), sb, $"tras saltar tambien se restaura el volumen del helicoptero a 1 ({(scriptHeli != null ? scriptHeli.VolumenExtra : -1f):0.00})");

                // ---- D) encadena con la cinematica de los pasos ----
                CinematicaDeOperacion.YaVista = false;
                var cin3 = CinematicaDeRapel.ReiniciarPorPrueba(true);
                yield return null; yield return null;
                CinematicaDeRapel.PruebaMantenerEspacio = true;
                float tD = Time.realtimeSinceStartup;
                while (CinematicaDeRapel.Activa && Time.realtimeSinceStartup - tD < 3f) yield return null;
                CinematicaDeRapel.PruebaMantenerEspacio = false;
                yield return null; yield return null;
                P6Ok(ref ok, !CinematicaDeRapel.Activa && CinematicaDeOperacion.Activa, sb, $"al terminar el rapel arranca la cinematica de los pasos (activa={CinematicaDeOperacion.Activa})");
                CinematicaDeOperacion.Saltar();
                yield return null;
                P6Ok(ref ok, !CinematicaDeOperacion.Activa && drv.enabled, sb, "se la corta y el control vuelve");

                // ---- E) OperacionPrueba.Arrancar sigue saltando las cinematicas ----
                CinematicaDeOperacion.YaVista = false;
                CinematicaDeRapel.ReiniciarPorPrueba(true);
                yield return null; yield return null;
                foreach (var x in Esperar(0.4f)) yield return x;
                bool activaAntes = CinematicaDeRapel.Activa;
                OperacionPrueba.Arrancar(2);
                yield return null; yield return null;
                P6Ok(ref ok, activaAntes && !CinematicaDeRapel.Activa && !CinematicaDeOperacion.Activa && drv.enabled && CinematicaDeRapel.YaVista, sb, $"Arrancar(2) con el rapel en curso ({activaAntes}) lo corta sin encadenar nada: rapel activo={CinematicaDeRapel.Activa}, pasos activos={CinematicaDeOperacion.Activa}");
            }
            finally
            {
                CinematicaDeRapel.PruebaMantenerEspacio = false;
                CinematicaDeRapel.Saltar(); CinematicaDeOperacion.Saltar();
                CinematicaDeOperacion.YaVista = true;
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
