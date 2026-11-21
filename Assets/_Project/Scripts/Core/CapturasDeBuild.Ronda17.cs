using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Operacion;

namespace SP.Core
{
    // Ronda 17: tomas ANTES/DESPUES del atlas. Usa SOLO APIs que ya existian en la build anterior (lo nuevo se lee por reflexion), para poder compilar
    // el mismo archivo contra el codigo viejo y sacar las tomas ANTES. Uso: -capturas <dir> -tomas r17_combate,r17_centro,r17_mundo
    public sealed partial class CapturasDeBuild
    {
        public static readonly string[] IdsR17Combate = { "r17_pesado_mascara", "r17_pesado_expuesto", "r17_escudero_calle", "r17_comun_calle", "r17_muerte_espalda", "r17_muerte_cabeza" };
        public static readonly string[] IdsR17Centro = { "r17_servidores_prop1", "r17_servidores_prop2", "r17_servidores_prop3", "r17_guarnicion", "r17_servidores_props", "r17_pc_bloqueada", "r17_servidores_a", "r17_servidores_b", "r17_hackeo", "r17_pc_libre", "r17_fachada_sur", "r17_fachada_oeste" };
        public static readonly string[] IdsR17Mundo = { "r17_fuente_cerca", "r17_fuente_lejos", "r17_anillo_lejos", "r17_anillo_cerca", "r17_reaccion" };

        static object LeerR17(object o, string nombre)
        {
            if (o == null) return null;
            var t = o as Type ?? o.GetType();
            var f = t.GetField(nombre, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (f != null) return f.GetValue(o is Type ? null : o);
            var p = t.GetProperty(nombre, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            return p != null ? p.GetValue(o is Type ? null : o) : null;
        }

        static void Escribir(Type t, string nombre, object valor)
        {
            var f = t != null ? t.GetField(nombre, BindingFlags.Public | BindingFlags.Static) : null;
            if (f != null) f.SetValue(null, valor);
        }

        Vector3 CabezaDe(Soldier s)
        {
            var h = s != null ? s.GetComponentInChildren<HitboxCabeza>(true) : null;
            if (h == null && s != null) h = HitboxCabeza.De(s);
            return h != null ? h.Centro : (s != null ? s.transform.position + Vector3.up * 1.6f : Vector3.zero);
        }

        // ---------------------------------------------------------------- combate: mascara del pesado, contraste, muerte
        IEnumerator R17Combate()
        {
            Saltar(2); yield return Espera(3f);
            yield return HayEnemigosParaClonar();
            SinDios(); var yo = Yo(); if (yo == null) yield break;
            CongelarAliados(yo);
            var f = yo.transform.forward; f.y = 0f; f.Normalize();
            var lado = yo.transform.right;

            // pesado: entero (con mascara en R17) -> 1.er headshot -> 2.o headshot
            var pesado = Clon(yo.transform.position + f * 7f, -f);
            if (pesado != null)
            {
                EnemigoPesado.Aplicar(pesado);
                yield return Espera(1.2f);
                if (pesado.Brain != null) pesado.Brain.Anulado = true;
                pesado.transform.position = yo.transform.position + f * 7f; pesado.transform.rotation = Quaternion.LookRotation(-f); Physics.SyncTransforms();
                yield return Espera(0.6f);
                D("r17_pesado_mascara", "mascara_puesta", LeerR17(pesado.GetComponent<EnemigoPesado>(), "MascaraPuesta") ?? "(sin la propiedad: build anterior)");
                D("r17_pesado_mascara", "capas_de_cabeza", LeerR17(pesado.Health, "CapasDeCabeza") ?? "(n/a)");
                var cab = CabezaDe(pesado);
                yield return Foto("r17_pesado_mascara", cab + (-(-f)) * 0f + (yo.transform.position - pesado.transform.position).normalized * 4.2f + Vector3.up * 0.4f, cab, 40f, false);
                int v0 = pesado.Health.Current;
                pesado.Health.TakeDamage(60, yo.Id, true);
                yield return Espera(0.5f);
                D("r17_pesado_expuesto", "vivo_tras_1er_headshot", pesado.Health.IsAlive); D("r17_pesado_expuesto", "dano_1er_headshot", v0 - pesado.Health.Current);
                D("r17_pesado_expuesto", "capas_restantes", LeerR17(pesado.Health, "CapasDeCabeza") ?? "(n/a)");
                D("r17_pesado_expuesto", "piezas_sueltas", LeerR17(pesado.GetComponent<EnemigoPesado>(), "PiezasDeMascaraSueltas") ?? "(n/a)");
                if (pesado.Health.IsAlive)
                {
                    yield return Foto("r17_pesado_expuesto", cab + (yo.transform.position - pesado.transform.position).normalized * 4.2f + Vector3.up * 0.4f, cab, 40f, false);
                    pesado.Health.TakeDamage(60, yo.Id, true); yield return Espera(0.3f);
                    D("r17_pesado_expuesto", "vivo_tras_2do_headshot", pesado.Health.IsAlive);
                }
                else yield return Foto("r17_pesado_expuesto", pesado.transform.position + (yo.transform.position - pesado.transform.position).normalized * 5f + Vector3.up * 1.5f, pesado.transform.position + Vector3.up * 0.6f, 45f, false);
                if (pesado != null) Destroy(pesado.gameObject);
            }

            // contraste: un enemigo comun y un escudero en la calle, a 12 m
            var comun = Clon(yo.transform.position + f * 12f + lado * -1.8f, -f);
            var escu = Clon(yo.transform.position + f * 12f + lado * 1.8f, -f);
            if (escu != null) EnemigoEscudero.Aplicar(escu);
            yield return Espera(1.5f);
            foreach (var s in new[] { comun, escu }) if (s != null && s.Brain != null) s.Brain.Anulado = true;
            if (comun != null) { comun.transform.position = yo.transform.position + f * 12f + lado * -1.8f; comun.transform.rotation = Quaternion.LookRotation(-f); }
            if (escu != null) { escu.transform.position = yo.transform.position + f * 12f + lado * 1.8f; escu.transform.rotation = Quaternion.LookRotation(-f); }
            Physics.SyncTransforms(); yield return Espera(0.5f);
            var medio = yo.transform.position + f * 12f + Vector3.up * 1.0f;
            yield return Foto("r17_comun_calle", yo.transform.position + Vector3.up * 1.7f, medio, 28f, false);
            if (escu != null)
            {
                escu.transform.rotation = Quaternion.LookRotation(f); Physics.SyncTransforms(); yield return Espera(0.3f);   // de espalda al jugador: se ve el marco del escudo mirando para otro lado
                yield return Foto("r17_escudero_calle", yo.transform.position + lado * 2.2f + Vector3.up * 1.6f, escu.transform.position + Vector3.up * 1.0f, 30f, false);
            }
            if (comun != null) Destroy(comun.gameObject);
            if (escu != null) Destroy(escu.gameObject);

            // muerte: golpe por la espalda y headshot de frente (el clip de muerte sale segun la direccion)
            var m1 = Clon(yo.transform.position + f * 5f, f);   // mira en la misma direccion que el jugador: de espaldas a el
            if (m1 != null)
            {
                yield return Espera(0.8f); if (m1.Brain != null) m1.Brain.Anulado = true;
                m1.transform.position = yo.transform.position + f * 5f; m1.transform.rotation = Quaternion.LookRotation(f); Physics.SyncTransforms(); yield return Espera(0.3f);
                m1.Health.TakeDamage(9999, yo.Id, false);
                yield return Espera(0.9f);
                D("r17_muerte_espalda", "variante", LeerR17(m1.GetComponentInChildren<SP.Presentation.CubeFxReactor>(), "MuerteVariante") ?? "(n/a)");
                yield return Foto("r17_muerte_espalda", yo.transform.position + lado * 3f + Vector3.up * 2.4f, m1.transform.position + Vector3.up * 0.4f, 45f, false);
                Destroy(m1.gameObject);
            }
            var m2 = Clon(yo.transform.position + f * 5f, -f);   // de frente al jugador
            if (m2 != null)
            {
                yield return Espera(0.8f); if (m2.Brain != null) m2.Brain.Anulado = true;
                m2.transform.position = yo.transform.position + f * 5f; m2.transform.rotation = Quaternion.LookRotation(-f); Physics.SyncTransforms(); yield return Espera(0.3f);
                m2.Health.TakeDamage(9999, yo.Id, true);
                yield return Espera(0.9f);
                yield return Foto("r17_muerte_cabeza", yo.transform.position + lado * 3f + Vector3.up * 2.4f, m2.transform.position + Vector3.up * 0.4f, 45f, false);
                Destroy(m2.gameObject);
            }
        }

        // ---------------------------------------------------------------- centro de datos: PC bloqueada, servidores, fachada
        IEnumerator R17Centro()
        {
            var tpd = typeof(OperacionDirector);
            Escribir(tpd, "ProbarBloqueoDelCentro", true);   // R17: la guarnicion y el bloqueo solo se arman fuera de los atajos de prueba
            Saltar(3); yield return Espera(5f);
            var d = OperacionDirector.Instancia; var yo = Yo(); SinDios();
            var pc = FindFirstObjectByType<OperacionTerminal>(FindObjectsInactive.Exclude);
            if (pc == null) { D("r17_pc_bloqueada", "error", "sin terminal"); yield break; }
            CongelarAliados(yo);
            if (yo != null) { yo.transform.position = EnMalla(pc.transform.position + new Vector3(0f, 0f, -3f)); Physics.SyncTransforms(); }
            yield return Espera(2f);
            var guarnicion = LeerR17(d, "GuarnicionDelCentro") as System.Collections.IEnumerable;
            int n = 0; if (guarnicion != null) foreach (var _ in guarnicion) n++;
            D("r17_pc_bloqueada", "guarnicion", guarnicion == null ? "(n/a: build anterior, sala vacia)" : (object)n);
            D("r17_pc_bloqueada", "bloqueada", LeerR17(pc, "Bloqueada") ?? "(n/a)"); D("r17_pc_bloqueada", "restantes", LeerR17(pc, "Restantes") ?? "(n/a)");
            yield return Foto("r17_pc_bloqueada", pc.transform.position + new Vector3(0f, 2.2f, -5.5f), pc.transform.position + Vector3.up * 1.2f, 55f, false);
            // datos del parche de decoracion + tomas de cerca de lo que creo y de la guarnicion
            var tr17 = Tipo("AjustesDeNivelR17");
            foreach (var k in new[] { "ListoInterior", "ListoFachada", "ObjetosCreados", "CoberturasCreadas", "CoberturasOmitidas", "LedsTotales", "LedsConFase", "FraccionLedsConFase", "TextosCreados" })
                D("r17_servidores_props", k, LeerR17(tr17, k) ?? "(n/a)");
            var grupoVida = GameObject.Find("R17_Servidores_Vida");
            D("r17_servidores_props", "grupo_vida", grupoVida != null ? grupoVida.transform.childCount + " hijos" : "(no existe)");
            if (grupoVida != null)
            {
                int k = 0;
                foreach (Transform h in grupoVida.transform)
                {
                    var rr = h.GetComponentInChildren<Renderer>(); if (rr == null) continue;
                    var c = rr.bounds.center; D("r17_servidores_props", "hijo_" + h.name, V(c));
                    if (k < 3 && !(h.name.Contains("Led"))) { yield return Foto("r17_servidores_prop" + (k + 1), c + new Vector3(0f, 2.2f, -4f), c, 55f, false); k++; }
                }
            }
            if (guarnicion != null) foreach (var o in guarnicion) { var s = o as Soldier; if (s != null) { yield return Foto("r17_guarnicion", s.transform.position + new Vector3(0f, 3f, -7f), s.transform.position + Vector3.up, 55f, false); break; } }
            yield return Foto("r17_servidores_a", new Vector3(0f, 3.2f, -45f), new Vector3(0f, 1.6f, -63f), 70f, false);
            yield return Foto("r17_servidores_b", new Vector3(-20f, 2.4f, -52f), new Vector3(8f, 1.5f, -66f), 70f, false);
            // hackeo en curso: balizas y luces del cuarto reaccionan (R17)
            if (yo != null) pc.Enganchar(yo);
            yield return Espera(3f);
            yield return Foto("r17_hackeo", new Vector3(0f, 3.2f, -45f), new Vector3(0f, 1.6f, -63f), 70f, false);
            // sala limpia: se ejecuta a toda la guarnicion y la PC queda libre
            if (guarnicion != null) foreach (var o in guarnicion) { var s = o as Soldier; if (s != null && s.Health != null && s.Health.IsAlive) s.Health.Ejecutar(yo != null ? yo.Id : -1, false); }
            yield return Espera(2f);
            D("r17_pc_libre", "bloqueada_despues", LeerR17(pc, "Bloqueada") ?? "(n/a)");
            yield return Foto("r17_pc_libre", pc.transform.position + new Vector3(0f, 2.2f, -5.5f), pc.transform.position + Vector3.up * 1.2f, 55f, false);
            // fachada exterior (a la altura del suelo)
            yield return Foto("r17_fachada_sur", new Vector3(0f, 3.5f, -104f), new Vector3(0f, 3.0f, -81f), 62f, false);
            yield return Foto("r17_fachada_oeste", new Vector3(-44f, 3.5f, -60f), new Vector3(-27f, 2.5f, -60f), 62f, false);
            Escribir(tpd, "ProbarBloqueoDelCentro", false);
        }

        // ---------------------------------------------------------------- mundo: fuente, anillo del tanque, reaccion al dano
        IEnumerator R17Mundo()
        {
            Saltar(5); yield return Espera(4f);
            SinDios(); var yo = Yo();
            Transform fuente = null;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)) if (r != null && r.name == "Fuente_Chorro") { fuente = r.transform; break; }
            if (fuente != null)
            {
                var p = fuente.position;
                D("r17_fuente_cerca", "sistemas_de_particulas", fuente.GetComponentsInChildren<ParticleSystem>(true).Length + fuente.parent.GetComponentsInChildren<ParticleSystem>(true).Length);
                yield return Foto("r17_fuente_cerca", p + new Vector3(3.2f, 1.6f, -3.2f), p + Vector3.up * 0.8f, 55f, false);
                yield return Foto("r17_fuente_lejos", p + new Vector3(16f, 7f, -16f), p + Vector3.up * 0.8f, 55f, false);
            }
            else D("r17_fuente_cerca", "error", "no hay Fuente_Chorro cargada");

            // anillo del tanque: lejos (aros de llamada) y cerca (aro de uso verde)
            Saltar(4); yield return Espera(4f);
            var d = OperacionDirector.Instancia; yo = Yo();
            var tanque = LeerR17(d, "tanque") as Component;
            if (tanque != null && yo != null)
            {
                var tp = tanque.transform.position;
                AnilloDeSubida.Mostrar(tanque.transform);
                yo.transform.position = EnMalla(tp + new Vector3(0f, 0f, 20f)); Physics.SyncTransforms(); yield return Espera(1.2f);
                yield return Foto("r17_anillo_lejos", tp + new Vector3(10f, 12f, 28f), tp, 55f, false);
                yo.transform.position = EnMalla(tp + new Vector3(0f, 0f, 3f)); Physics.SyncTransforms(); yield return Espera(1.2f);
                foreach (var k in new[] { "DentroDelRadio", "DistanciaAlJugador", "RadioDeUso", "LlamadaActiva", "EscalaActual" }) D("r17_anillo_cerca", k, LeerR17(AnilloDeSubida.Instancia, k) ?? "(n/a)");
                D("r17_anillo_cerca", "yo_a_tanque_m", Vector3.Distance(yo.transform.position, tp));
                yield return Foto("r17_anillo_cerca", tp + new Vector3(8f, 9f, 11f), tp, 55f, false);
                AnilloDeSubida.Ocultar();
            }
            else D("r17_anillo_lejos", "error", "sin tanque");

            // reaccion: se hiere a un guardia desde lejos y se cuenta cuantos enemigos cercanos pasan a perseguir en 4 s
            Saltar(2); yield return Espera(3f);
            yo = Yo();
            Soldier victima = null;
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy && a.GetComponent<PerroEnemigo>() == null && a.GetComponent<EnemigoPesado>() == null) { victima = a; break; }
            if (victima != null && yo != null)
            {
                int cerca0 = 0, persiguen0 = 0;
                foreach (var a in ActorRegistry.All) if (a != null && a != victima && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy && Vector3.Distance(a.transform.position, victima.transform.position) < 16f) { cerca0++; if (a.Brain != null && a.Brain.State == AiState.Chase) persiguen0++; }
                if (victima.Brain != null) { var pq = victima.Brain.Quieto; }
                victima.Health.TakeDamage(5, yo.Id, false);
                yield return Espera(4f);
                int cerca1 = 0, persiguen1 = 0;
                foreach (var a in ActorRegistry.All) if (a != null && a != victima && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy && Vector3.Distance(a.transform.position, victima.transform.position) < 16f) { cerca1++; if (a.Brain != null && (a.Brain.State == AiState.Chase || a.Brain.State == AiState.Attack)) persiguen1++; }
                D("r17_reaccion", "victima_estado", victima.Brain != null ? victima.Brain.State.ToString() : "?");
                D("r17_reaccion", "victima_quieto", victima.Brain != null && victima.Brain.Quieto);
                D("r17_reaccion", "grupo_16m", cerca1); D("r17_reaccion", "persiguen_antes", persiguen0); D("r17_reaccion", "persiguen_despues", persiguen1);
                yield return Foto("r17_reaccion", victima.transform.position + new Vector3(0f, 22f, -14f), victima.transform.position, 55f, false);
            }
            else D("r17_reaccion", "error", "sin victima");
        }
    }
}
