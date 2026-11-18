using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.Presentation;
using SP.UI;

namespace SP.EditorTools
{
    // Ronda 13, T5 (decision D7): al mantener [E] sobre un caido queda UN solo elemento de feedback visual (el engranaje radial de
    // la mira, EngranajeDeRevivir) mas el sonido. Jugador (asalto, 5 s -> 2 s = 0,40) y medico (2,5 s -> 2 s = 0,80).
    public static partial class ChecksBugs065
    {
        // Elementos de feedback de revivir activos ahora, por nombre. Devuelve el total y arma la lista.
        static int FeedbackDeRevivirActivo(StringBuilder lista, out int engranajes)
        {
            int total = 0; engranajes = 0;
            void Cuenta(string n, int c) { if (c <= 0) return; total += c; lista.Append(n).Append('x').Append(c).Append(' '); }
            int circ = 0, enf = 0, cruz = 0, anillo = 0, rombo = 0, marca = 0, eng = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                string n = t.name;
                if (n == "CirculoRevivir") circ++;
                else if (n == "CirculoEnfoque") enf++;
                else if (n == "CruzVerde") cruz++;
                else if (n == "AnilloCuracion") anillo++;
                else if (n == "Rombo" && t.parent != null && t.parent.name == "InteractGearMarker") rombo++;
                else if (n.StartsWith("Caido_")) marca++;
                else if (n == EngranajeDeRevivir.Nombre) eng++;
            }
            engranajes = eng;
            Cuenta("CirculoRevivir", circ); Cuenta("CirculoEnfoque", enf); Cuenta("CruzVerde", cruz); Cuenta("AnilloCuracion", anillo);
            Cuenta("InteractGearMarker", rombo); Cuenta("MarcaDeCaido", marca); Cuenta(EngranajeDeRevivir.Nombre, eng);
            Cuenta("EngranajesDeAccion.Activos", EngranajesDeAccion.Activos);
            var vista = Object.FindFirstObjectByType<AccionesEnCursoView>();
            Cuenta("BarrasVisibles", vista != null ? vista.BarrasVisibles : 0);
            // Textos: la linea "REVIVIENDO" del HUD, el aviso unico y la pista/instruccion con [E].
            foreach (var tx in Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (tx == null || !tx.gameObject.activeInHierarchy || string.IsNullOrEmpty(tx.text)) continue;
                string s = tx.text.ToUpperInvariant();
                if (s.Contains("REVIVIENDO")) Cuenta("texto_REVIVIENDO", 1);
                else if (s.Contains("MANTENGA [") || s.Contains("MANTENER [")) Cuenta("pista_E", 1);
            }
            if (AvisoUnico.TextoActual.ToUpperInvariant().Contains("REVIVIENDO")) Cuenta("aviso_accion", 1);
            return total;
        }

        static IEnumerator Bug976a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            if (Keyboard.current == null) { Fin("FALLO no hay Keyboard.current"); yield break; }
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var vega = Escuadrista(RoleType.Assault); var doc = Escuadrista(RoleType.Medic);
            var drv = PlayerInputDriver.Activo;
            if (vega == null || doc == null || drv == null) { ModoDios.Poner(diosAntes); Fin("FALLO faltan piezas (vega/doc/driver)"); yield break; }
            Key kE = KeyBindings.Get(KeyBindings.Interactuar);
            Soldier caidoA = null, caidoB = null;
            try
            {
                for (int caso = 0; caso < 2; caso++)
                {
                    bool medico = caso == 1;
                    var yo = medico ? doc : vega;
                    var caido = medico ? vega : doc;
                    if (medico) caidoB = caido; else caidoA = caido;
                    Poseer(yo); yield return null;
                    var mando = drv.Brain.Current;
                    Junto(caido, mando.transform.position, 1.5f, caido.transform.position);
                    for (int i = 0; i < 3; i++) yield return null;
                    ComandosDeDepuracion.Matar(caido.Id);
                    for (int i = 0; i < 5; i++) yield return null;
                    if (caido.Health.IsAlive) { ok = false; sb.Append($"{(medico ? "medico" : "jugador")}: el caido no cayo; "); continue; }

                    KeyBindings.ForzarInicioDePulsacion(KeyBindings.Interactuar, 0f);
                    Teclas(kE);
                    float t0 = Time.realtimeSinceStartup; float total = 0f; int n = 0; float fill = -1f; int eng = 0; var lista = new StringBuilder();
                    while (Time.realtimeSinceStartup - t0 < 2.0f)
                    {
                        yield return null;
                        KeyBindings.ForzarInicioDePulsacion(KeyBindings.Interactuar, Time.realtimeSinceStartup - t0);
                    }
                    lista.Clear();
                    n = FeedbackDeRevivirActivo(lista, out eng);
                    fill = EngranajeDeRevivir.Fill;
                    float esperado = medico ? 0.8f : 0.4f;
                    bool fillOk = Mathf.Abs(fill - esperado) <= 0.07f;
                    bool unico = n == 1 && eng == 1;
                    if (!fillOk || !unico) ok = false;
                    sb.Append($"{(medico ? "medico" : "jugador")} a 2 s: feedback_total={n} [{lista.ToString().Trim()}], fill={fill:0.00} (esperado {esperado:0.00}){(fillOk && unico ? "" : " (MAL)")}; ");
                    Teclas();
                    for (int i = 0; i < 10; i++) yield return null;
                    if (!caido.Health.IsAlive) Reanimacion.Ejecutar(caido);
                    foreach (var x in Esperar(0.4f)) yield return x;
                }
            }
            finally
            {
                Teclas();
                if (caidoA != null && !caidoA.Health.IsAlive) Reanimacion.Ejecutar(caidoA);
                if (caidoB != null && !caidoB.Health.IsAlive) Reanimacion.Ejecutar(caidoB);
                ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}

