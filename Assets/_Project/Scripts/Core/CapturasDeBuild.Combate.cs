using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Operacion;
using SP.Player;
using SP.Presentation;
using SP.Vehicles;

namespace SP.Core
{
    // R13 T0: tomas de combate (cuchillo, cohetes contra el heli, revivir) y del tanque.
    public sealed partial class CapturasDeBuild
    {
        // El modo dios sigue encendido (el tanque no cae) pero sin su aceleracion de tiempo: los tiempos de la toma son los del juego.
        void SinAceleracion()
        {
            bool vel = ModoDios.VelocidadActiva; ModoDios.VelocidadActiva = false; Time.timeScale = 1f;
            finalizadores.Add(() => { ModoDios.VelocidadActiva = vel; Time.timeScale = 1f; });
        }

        // La escuadra aliada (menos 'salvo') queda sin IA durante la toma; se restaura al terminar.
        void CongelarAliados(params Soldier[] salvo)
        {
            var congelados = new List<AiBrain>();
            foreach (var a in ActorRegistry.All)
            {
                if (a == null || a.Brain == null || a.Team != TeamId.Player) continue;
                bool exc = false; foreach (var s in salvo) if (a == s) exc = true;
                if (exc || a.Brain.Anulado) continue;
                a.Brain.Anulado = true; congelados.Add(a.Brain);
            }
            finalizadores.Add(() => { foreach (var b in congelados) if (b != null) b.Anulado = false; });
        }

        // ================================================================ TANQUE (T4, T13)
        IEnumerator Tanque()
        {
            var d = OperacionDirector.Instancia; if (d == null) yield break;
            SinAceleracion();
            CinematicaDeOperacion.Saltar(); d.SaltarA(OperacionDirector.Orden[3], true, 1); yield return Espera(3f);
            var drv = PlayerInputDriver.Activo; if (drv == null) { D("tanque_mira_inicial", "error", "sin driver"); yield break; }
            finalizadores.Add(() => { var dd = PlayerInputDriver.Activo; if (dd != null && dd.Vehicle != null) { try { var vv = dd.Vehicle; dd.ExitVehicle(); dd.BajarATodos(vv); } catch (Exception) { } } });
            d.Embarcar();
            yield return EsperarHasta(() => drv.CurrentSeat == VehicleSeatRole.Gunner, 12f);
            D("tanque_mira_inicial", "artillero", esperaOk);
            if (!esperaOk) yield break;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 1f) yield return null;
            var rig = drv.Rig; var cam = rig != null ? rig.Cam : (Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null);
            D("tanque_mira_inicial", "ads", rig != null ? rig.AdsBlend : -1f); D("tanque_mira_inicial", "zoom", rig != null && rig.EstaConZoom);
            D("tanque_mira_inicial", "fov_1s", cam != null ? cam.fieldOfView : -1f);
            D("tanque_mira_inicial", "fov_zoom", rig != null ? rig.FovDeZoom : -1f);
            yield return FotoJuego("tanque_mira_inicial");
            while (Time.realtimeSinceStartup - t0 < 2f) yield return null;
            D("tanque_mira_inicial", "fov_2s", cam != null ? cam.fieldOfView : -1f); D("tanque_mira_inicial", "ads_2s", rig != null ? rig.AdsBlend : -1f);
            // barra de vida del tanque: Image grises y grandes del HUD (T13)
            var hud = BuscarUno("HudDeTanqueSimple") as Component; int grises = 0; var lg = new StringBuilder();
            if (hud != null)
            {
                var canvas = hud.GetComponentInParent<Canvas>(); var raiz = canvas != null ? canvas.rootCanvas.transform : hud.transform;
                foreach (var im in raiz.GetComponentsInChildren<Image>(false))
                {
                    if (im == null || !im.gameObject.activeInHierarchy || !im.enabled) continue;
                    var c = im.color; if (c.a < 0.3f) continue;
                    var rt = im.rectTransform; float w = rt.rect.width * rt.lossyScale.x, h = rt.rect.height * rt.lossyScale.y; if (w * h < 1200f || w < 60f) continue;
                    float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                    if (mx - mn < 0.12f && mx >= 0.45f) { grises++; lg.Append(im.name).Append('(').Append(F(w)).Append('x').Append(F(h)).Append(") "); }
                }
            }
            D("tanque_hud_barra", "img_grises_grandes", grises); D("tanque_hud_barra", "lista", lg.Length > 0 ? lg.ToString().Trim() : "-"); D("tanque_hud_barra", "hud_encontrado", hud != null);
            yield return FotoJuego("tanque_hud_barra");
            // impactos desde la izquierda y desde atras (relativo a la camara): proyectil enemigo de 30, id -3
            var tanque = d.tanque; var pool = ProjectilePool.Activo;
            for (int k = 0; k < 2; k++)
            {
                string id = k == 0 ? "tanque_impacto_izq" : "tanque_impacto_atras";
                if (tanque == null || pool == null || cam == null) { D(id, "marcas", "n/a (sin tanque, pool o camara)"); continue; }
                var dir = k == 0 ? cam.transform.right : cam.transform.forward; dir.y = 0f; dir.Normalize();
                var centroT = tanque.transform.position + Vector3.up * 1.2f; Vector3 origen = centroT - dir * 25f + Vector3.up * 0.8f; string metros = "25";
                foreach (var m in new[] { 25f, 20f, 15f, 11f, 8f })
                {
                    var o = centroT - dir * m + Vector3.up * 0.8f; bool libre = true;
                    foreach (var h in Physics.RaycastAll(o, (centroT - o).normalized, Vector3.Distance(o, centroT), ~0, QueryTriggerInteraction.Ignore)) if (h.collider != null && h.collider.GetComponentInParent<Vehicle>() != tanque) { libre = false; break; }
                    if (libre) { origen = o; metros = m.ToString("0"); break; }
                }
                D(id, "origen_m", metros);
                var haciaTanque = (centroT - origen).normalized;
                int v0 = tanque.Health != null ? tanque.Health.Current : 0;
                pool.Spawn(origen, haciaTanque, -3, TeamId.Enemy, 30);
                yield return Espera(0.25f);
                var ang = Leer(hud, "AngulosActivos") as IEnumerable<float>;
                if (ang == null) D(id, "marcas", "n/a"); else { var sb = new StringBuilder(); foreach (var a in ang) sb.Append(a.ToString("0", Ci)).Append(' '); D(id, "marcas", sb.Length > 0 ? sb.ToString().Trim() : "(vacia)"); }
                D(id, "marcas_visibles", ContarPorNombre("MarcaDeImpacto_")); D(id, "anillo", ExisteActivo("AnilloDeImpactos"));
                D(id, "vida_tanque_delta", (tanque.Health != null ? tanque.Health.Current : 0) - v0);
                if (tanque.Health != null && tanque.Health.Current == v0) { D(id, "proyectil_conecto", false); tanque.TakeDamage(30, -3); yield return Espera(0.15f); } else D(id, "proyectil_conecto", true);
                yield return FotoJuego(id);
                yield return Espera(2.2f);
            }
        }

        // ================================================================ REVIVIR (T5)
        // Cuantos elementos de feedback de revivir hay activos y que son.
        void DatosDeFeedbackRevivir(string id)
        {
            var nombres = new List<string>();
            void Cuenta(string n, int c) { if (c > 0) nombres.Add(n + "x" + c); }
            Cuenta("CirculoRevivir", ContarPorNombre("CirculoRevivir")); Cuenta("CirculoEnfoque", ContarPorNombre("CirculoEnfoque"));
            Cuenta("EngranajeDeRevivir", ContarPorNombre("EngranajeDeRevivir"));
            Cuenta("CruzVerde", ContarPorNombre("CruzVerde")); Cuenta("AnilloCuracion", ContarPorNombre("AnilloCuracion"));
            Cuenta("MarcaEngranaje", ContarPorNombre("InteractGear", "*GearMarker", "*EngranajeDeAccion"));
            int rev = TextosActivos("REVIVIENDO").Count; Cuenta("texto_REVIVIENDO", rev);
            int pista = TextosActivos("MANTENGA", "MANTENER", "[E]").Count; Cuenta("pista_E", pista);
            var tipoEng = Tipo("EngranajesDeAccion"); if (tipoEng != null) { var act = Leer(tipoEng, "Activos"); if (act is int ai) Cuenta("EngranajesDeAccion.Activos", ai); }
            Cuenta("MarcaDeCaido", ContarPorNombre("MarcaCaido", "Caido_", "*IndicadorDeCaido"));
            int total = 0; foreach (var n in nombres) { int x = n.LastIndexOf('x'); total += int.Parse(n.Substring(x + 1)); }
            D(id, "feedback_total", total); D(id, "feedback_lista", nombres.Count > 0 ? string.Join(",", nombres) : "-");
            float fill = -1f; var eng = BuscarPorNombre("EngranajeDeRevivir");
            if (eng != null) { var rel = eng.Find("Relleno"); var im = rel != null ? rel.GetComponent<Image>() : eng.GetComponentInChildren<Image>(); if (im != null) fill = im.fillAmount; }
            D(id, "engranaje_fill", fill);
        }

        IEnumerator HacerRevivir(string id, bool medico)
        {
            Saltar(2); yield return Espera(3f);
            SinDios();
            var drv = PlayerInputDriver.Activo; var yo = Yo(); if (drv == null || yo == null) { D(id, "error", "sin jugador"); yield break; }
            if (medico)
            {
                Soldier doc = null; foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Player && a.Role == RoleType.Medic && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy && a != yo) { doc = a; break; }
                if (doc == null) { D(id, "error", "sin medico"); yield break; }
                bool ok = drv.TryPossess(doc); yield return Espera(1f); D(id, "poseido_doc", ok && Yo() == doc);
                yo = Yo(); if (yo == null) yield break;
                if (yo.Health != null) yo.Health.Invulnerable = true;
            }
            var caido = AliadoNoMedico(yo); if (caido == null) { D(id, "error", "sin aliado para bajar"); yield break; }
            var yoOriginal = yo;
            // al terminar: el caido vuelve a la vida y el control al soldado de siempre (si no, la escuadra se va quedando sin gente en las tomas que siguen)
            finalizadores.Add(() => { if (caido != null && caido.Health != null && !caido.Health.IsAlive) Reanimacion.Ejecutar(caido); var d0 = PlayerInputDriver.Activo; if (d0 != null && yoOriginal != null && d0.Brain != null && d0.Brain.Current != yoOriginal && yoOriginal.Health.IsAlive) d0.TryPossess(yoOriginal); });
            CongelarAliados(yo, caido);
            caido.Health.TakeDamage(100000, -1);
            yield return Espera(0.6f);
            var cp = caido.transform.position; var haciaYo = yo.transform.position - cp; haciaYo.y = 0f; if (haciaYo.sqrMagnitude < 0.01f) haciaYo = Vector3.back; haciaYo.Normalize();
            yo.transform.position = EnMalla(cp + haciaYo * 1.2f); yo.transform.rotation = Quaternion.LookRotation(-haciaYo); Physics.SyncTransforms();
            yield return Espera(0.4f);
            D(id, "caido_cercano", drv.FindNearestDownedAlly() == caido); D(id, "caido_en_escuadra", drv.Squad != null && drv.Squad.Contains(caido));
            finalizadores.Add(() => TeclaE(false));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 2f) { TeclaE(true); yield return null; }
            D(id, "tecla_E_llego", KeyBindings.IsPressed(KeyBindings.Interactuar)); D(id, "keyboard_actual_es_virtual", UnityEngine.InputSystem.Keyboard.current == tecladoVirtual);
            DatosDeFeedbackRevivir(id);
            D(id, "reviviendo_activo", drv.ReviviendoActivo);
            yield return FotoJuego(id);
            TeclaE(false); yield return Espera(0.3f);
        }

        IEnumerator Revivir40() { return HacerRevivir("revivir_40", false); }
        IEnumerator RevivirMedico() { return HacerRevivir("revivir_medico", true); }

        static bool HayMolde()
        {
            if (moldeFijo != null) return true;
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy && a.GetComponent<EnemigoPesado>() == null && a.GetComponent<EnemigoEscudero>() == null && a.GetComponent<PerroEnemigo>() == null) return true;
            return false;
        }
        // Las tomas con clon necesitan un enemigo comun vivo para copiarlo: en la fase 2 pueden no quedar, y entonces se usa la fase 1.
        IEnumerator HayEnemigosParaClonar()
        {
            if (HayMolde()) yield break;
            D("molde", "fase", "no habia enemigos en la fase 2: se usa la fase 1");
            Saltar(1); yield return Espera(3f);
        }

        // ================================================================ CUCHILLO (T1)
        static int ParticulasDe(string prefijo)
        {
            int n = 0;
            foreach (var ps in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (ps != null && (ps.name.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase) || (ps.transform.parent != null && ps.transform.parent.name.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase)))) n += ps.particleCount;
            return n;
        }

        IEnumerator CuchilloSoldado()
        {
            Saltar(2); yield return Espera(3f);
            yield return HayEnemigosParaClonar();
            SinDios(); var yo = Yo(); if (yo == null) yield break;
            CongelarAliados(yo);
            var f = yo.transform.forward; f.y = 0f; f.Normalize();
            var victima = Clon(yo.transform.position + f * 1.6f, f);     // de espaldas al jugador: mira en la misma direccion
            if (victima == null) { D("cuchillo_soldado_0", "error", "sin molde"); yield break; }
            if (victima.Brain != null) victima.Brain.Anulado = true;
            yield return Espera(0.5f);
            victima.transform.position = yo.transform.position + f * 1.6f; victima.transform.rotation = Quaternion.LookRotation(f); Physics.SyncTransforms();
            yield return null;
            int v0 = victima.Health.Current; var wh = yo.Weapon;
            bool sonoLaCuchillada = wh != null && wh.TryMelee();
            yield return Espera(0.12f);
            D("cuchillo_soldado_0", "vida_antes", v0); D("cuchillo_soldado_0", "vida_despues", victima.Health.Current); D("cuchillo_soldado_0", "vivo", victima.Health.IsAlive); D("cuchillo_soldado_0", "tryMelee", sonoLaCuchillada);
            D("cuchillo_soldado_0", "particulas", ContarParticulas()); D("cuchillo_soldado_0", "particulas_sangre", ParticulasDe("SangreFx_"));
            var pecho = victima.transform.position + Vector3.up * 1.1f; var r = yo.transform.right;
            yield return Foto("cuchillo_soldado_0", yo.transform.position + r * 1.5f + Vector3.up * 1.6f, pecho, 55f, false);
            yield return Espera(0.6f);
            int decal = 0; foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (t != null && t.gameObject.activeInHierarchy && t.name.IndexOf("sangre", StringComparison.OrdinalIgnoreCase) >= 0 && !t.name.StartsWith("SangreFx_", StringComparison.OrdinalIgnoreCase) && Vector3.Distance(t.position, pecho) < 4f) decal++;
            D("cuchillo_soldado_1", "decal_sangre", decal); D("cuchillo_soldado_1", "sangrefx_objetos", ContarPorNombre("SangreFx_"));
            { var tk = Tipo("DecalKind"); D("cuchillo_soldado_1", "decalkind_sangre_existe", tk != null && Enum.IsDefined(tk, "Sangre")); }
            yield return Foto("cuchillo_soldado_1", yo.transform.position + r * 1.5f + Vector3.up * 1.6f, pecho, 55f, false);
            if (victima != null) Destroy(victima.gameObject);
        }

        IEnumerator CuchilloPesado()
        {
            Saltar(2); yield return Espera(3f);
            yield return HayEnemigosParaClonar();
            SinDios(); var yo = Yo(); if (yo == null) yield break;
            CongelarAliados(yo);
            var f = yo.transform.forward; f.y = 0f; f.Normalize();
            var pesado = Clon(yo.transform.position + f * 1.6f, f);
            if (pesado == null) { D("cuchillo_pesado", "error", "sin molde"); yield break; }
            EnemigoPesado.Aplicar(pesado);
            yield return Espera(0.5f);
            if (pesado.Brain != null) pesado.Brain.Anulado = true;
            pesado.transform.position = yo.transform.position + f * 1.6f; pesado.transform.rotation = Quaternion.LookRotation(f); Physics.SyncTransforms();
            yield return Espera(0.3f);
            int v0 = pesado.Health.Current, max = pesado.Health.MaxHealth; var wh = yo.Weapon;
            bool g1 = wh != null && wh.TryMelee(); yield return Espera(0.12f); int v1 = pesado.Health.Current;
            yield return Espera(0.6f);
            bool g2 = wh != null && wh.TryMelee(); yield return Espera(0.3f); int v2 = pesado.Health.Current;
            D("cuchillo_pesado", "vida_max", max); D("cuchillo_pesado", "vida_antes", v0); D("cuchillo_pesado", "vida_tras_1", v1); D("cuchillo_pesado", "vida_tras_2", v2);
            D("cuchillo_pesado", "pct_perdido_1", max > 0 ? 100f * (v0 - v1) / max : 0f); D("cuchillo_pesado", "vivo", pesado.Health.IsAlive); D("cuchillo_pesado", "golpes", (g1 ? 1 : 0) + (g2 ? 1 : 0));
            D("cuchillo_pesado", "particulas_sangre", ParticulasDe("SangreFx_"));
            yield return Foto("cuchillo_pesado", yo.transform.position + yo.transform.right * 1.8f + Vector3.up * 1.7f, pesado.transform.position + Vector3.up * 1.2f, 55f, false);
            if (pesado != null) Destroy(pesado.gameObject);
        }

        // ================================================================ COHETES CONTRA EL HELI JEFE (T2)
        // Dispara por el camino del arma real (WeaponHolder.TryFire con el lanzacohetes equipado), con adelanto de punteria.
        bool DispararCohete(Soldier yo, Vector3 punto, Vector3 velObjetivo)
        {
            var wh = yo.Weapon; if (wh == null) return false;
            var spec = WeaponCatalog.Get(WeaponKind.Rocket);
            if (!wh.Loadout.Contains(WeaponKind.Rocket)) wh.Loadout.Add(WeaponKind.Rocket);
            if (wh.CurrentWeaponKind != WeaponKind.Rocket) wh.EquipWeapon(WeaponKind.Rocket, spec.Damage, spec.Cooldown, spec.Color);
            wh.FijarReservaParaPrueba(WeaponKind.Rocket, 99); wh.RestaurarMunicion(1);
            var origen = yo.transform.position + Vector3.up * 1.5f;
            float vel = Projectile.VelocidadBase * spec.ProjectileSpeed;
            float t = Vector3.Distance(origen, punto) / Mathf.Max(1f, vel);
            var apunta = punto + velObjetivo * t;
            var dir = WeaponHolder.DireccionBalistica(origen, apunta, vel, spec.ProjectileGravity, (apunta - origen).normalized);
            yo.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z).normalized);
            return wh.TryFire(origen, dir, 0f, origen + dir * 0.8f);
        }

        IEnumerator EsperarHeliVulnerable(OperacionDirector d, float max)
        {
            yield return EsperarHasta(() => d.HeliDeLaAzotea != null && d.HeliDeLaAzotea.Vehiculo != null && d.HeliDeLaAzotea.Vehiculo.Health != null && !d.HeliDeLaAzotea.Vehiculo.Health.Invulnerable && !d.HeliDeLaAzotea.Derribado, max);
        }

        IEnumerator HeliCohetes()
        {
            var d = OperacionDirector.Instancia; if (d == null) yield break;
            d.SaltarALaAzotea();
            yield return EsperarHeliVulnerable(d, 40f);
            D("heli_cohetes", "heli_vulnerable", esperaOk); if (!esperaOk) { D("heli_cohetes", "error", "el heli no quedo vulnerable en 40 s"); yield break; }
            SinDios(); ConDificultad(NivelDificultad.Medio);
            var yo = Yo(); if (yo == null) yield break;
            CongelarAliados(yo);
            var wh0 = yo.Weapon; int idx0 = wh0 != null ? wh0.CurrentLoadoutIndex : 0;
            finalizadores.Add(() => { if (wh0 != null) { try { wh0.EquipFromLoadout(idx0); } catch (Exception) { } } });
            bool fotoHecha = false;
            foreach (var dif in new[] { NivelDificultad.Facil, NivelDificultad.Medio, NivelDificultad.Dificil })
            {
                Dificultad.Actual = dif; string dn = dif.ToString().ToLowerInvariant();
                if (d.HeliDeLaAzotea == null || d.HeliDeLaAzotea.Derribado) { d.SaltarALaAzotea(); yield return EsperarHeliVulnerable(d, 40f); if (!esperaOk) { D("heli_cohetes", "cohetes_hasta_morir." + dn + ".centro", "sin heli"); continue; } yo = Yo(); }
                var heli = d.HeliDeLaAzotea; var hp = heli.Vehiculo.Health; var vt = heli.Vehiculo.transform;
                Vector3 Punto(string p) => vt.TransformPoint(new Vector3(0f, 1.8f, p == "nariz" ? 4f : p == "cola" ? -5.5f : -1f));
                Vector3 vel = Vector3.zero; Vector3 ult = vt.position; float tUlt = Time.time;
                // sonda de dano por punto (2 cohetes, vida restaurada)
                foreach (var punto in new[] { "nariz", "cola" })
                {
                    hp.RestaurarVida(hp.MaxHealth); yield return Espera(0.3f);
                    int vida0 = hp.Current; int disparos = 0;
                    for (int i = 0; i < 2; i++)
                    {
                        if (Time.time - tUlt > 0.2f) { vel = (vt.position - ult) / Mathf.Max(0.01f, Time.time - tUlt); ult = vt.position; tUlt = Time.time; }
                        if (DispararCohete(yo, Punto(punto), vel)) disparos++;
                        yield return Espera(1.4f);
                    }
                    int dano = vida0 - hp.Current;
                    D("heli_cohetes", "dano_por_cohete." + dn + "." + punto, disparos > 0 ? (float)dano / disparos : 0f);
                    D("heli_cohetes", "cohetes_estimados." + dn + "." + punto, dano > 0 ? Mathf.CeilToInt(hp.MaxHealth / ((float)dano / Mathf.Max(1, disparos))) : -1);
                }
                // hasta morir al centro
                hp.RestaurarVida(hp.MaxHealth); yield return Espera(0.3f);
                int max = hp.MaxHealth, v0 = hp.Current, usados = 0; bool murio = false;
                for (int i = 1; i <= 12; i++)
                {
                    if (Time.time - tUlt > 0.2f) { vel = (vt.position - ult) / Mathf.Max(0.01f, Time.time - tUlt); ult = vt.position; tUlt = Time.time; }
                    bool ok = DispararCohete(yo, Punto("centro"), vel); if (ok) usados++;
                    if (i == 1) { yield return Espera(0.7f); D("heli_cohetes", "dano_por_cohete." + dn + ".centro", v0 - hp.Current);
                        if (!fotoHecha) { fotoHecha = true; yield return Foto("heli_cohetes", yo.transform.position + Vector3.up * 2f, vt.position + Vector3.up * 1.5f, 50f, false); } yield return Espera(0.7f); }
                    else yield return Espera(1.3f);
                    if (hp.Current <= 0 || heli.Derribado) { murio = true; break; }
                }
                D("heli_cohetes", "cohetes_hasta_morir." + dn + ".centro", murio ? usados.ToString() : "no murio con 12");
                D("heli_cohetes", "vida_final." + dn, hp.Current + "/" + max);
            }
            if (!fotoHecha) yield return Foto("heli_cohetes", yo.transform.position + Vector3.up * 2f, yo.transform.position + yo.transform.forward * 30f, 50f, false);
        }

        // ================================================================ AJUSTES: tarjeta DANO (T3)
        static Rect RectPantalla(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            var cv = rt.GetComponentInParent<Canvas>(); Camera cam = cv != null && cv.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? cv.rootCanvas.worldCamera : null;
            float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
            for (int i = 0; i < 4; i++) { Vector2 p = cam != null ? (Vector2)RectTransformUtility.WorldToScreenPoint(cam, c[i]) : (Vector2)c[i]; x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y); }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        void MedirAjustes(string id, Transform panel)
        {
            var pantalla = new Rect(0f, 0f, Screen.width, Screen.height);
            var piezas = new List<RectTransform>();
            foreach (var rt in panel.GetComponentsInChildren<RectTransform>(false))
            {
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                string n = rt.name; if (n == "AjusteDeDano" || n.StartsWith("Daño", StringComparison.OrdinalIgnoreCase) || n.StartsWith("Dano", StringComparison.OrdinalIgnoreCase) || n == "Sec_Dano" || n == "SecSep_Dano") piezas.Add(rt);
            }
            float area = 0f, dentro = 0f;
            var card = BuscarEn(panel, "AjusteDeDano");
            if (card != null) piezas = new List<RectTransform> { card };
            foreach (var rt in piezas) { var r = RectPantalla(rt); float a = r.width * r.height; if (a < 1f) continue; area += a; float w = Mathf.Max(0f, Mathf.Min(r.xMax, pantalla.xMax) - Mathf.Max(r.xMin, pantalla.xMin)), h = Mathf.Max(0f, Mathf.Min(r.yMax, pantalla.yMax) - Mathf.Max(r.yMin, pantalla.yMin)); dentro += w * h; }
            D(id, "dano_visible_pct", area > 0f ? 100f * dentro / area : -1f); D(id, "piezas_dano", piezas.Count);
            D(id, "tarjeta_dano_propia", card != null);
            int fuera = 0; var peor = new StringBuilder();
            foreach (var rt in panel.GetComponentsInChildren<RectTransform>(false))
            {
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                bool contenido = (rt.GetComponent<Text>() != null && !string.IsNullOrWhiteSpace(rt.GetComponent<Text>().text)) || rt.GetComponent<Slider>() != null || rt.GetComponent<Button>() != null || rt.GetComponent<Toggle>() != null || rt.GetComponent<Dropdown>() != null || rt.GetComponent<InputField>() != null;
                if (!contenido) continue;
                var r = RectPantalla(rt); if (r.width < 1f) continue;
                if (r.xMin < pantalla.xMin - 1f || r.yMin < pantalla.yMin - 1f || r.xMax > pantalla.xMax + 1f || r.yMax > pantalla.yMax + 1f) { fuera++; if (peor.Length < 200) peor.Append(rt.name).Append(' '); }
            }
            D(id, "fuera", fuera); D(id, "fuera_lista", peor.Length > 0 ? peor.ToString().Trim() : "-");
            D(id, "pantalla", Screen.width + "x" + Screen.height);
            var lay = Tipo("LayoutDeAjustes"); var panelGo = panel.gameObject;
            if (lay != null && Llamar(lay, "Diagnosticar", out var res, panelGo, new Vector2(Screen.width, Screen.height)) && res is IEnumerable<string> ps) { int k = 0; var sb = new StringBuilder(); foreach (var p in ps) { k++; if (sb.Length < 240) sb.Append(p).Append(" | "); } D(id, "diagnostico_n", k); D(id, "diagnostico", sb.Length > 0 ? sb.ToString() : "-"); }
        }
        static RectTransform BuscarEn(Transform raiz, string nombre) { foreach (var t in raiz.GetComponentsInChildren<Transform>(false)) if (t.name == nombre) return t as RectTransform; return null; }

        IEnumerator AjustesDano()
        {
            Saltar(2); yield return Espera(2f);
            var pc = FindFirstObjectByType<PauseController>(FindObjectsInactive.Include); if (pc == null) { D("ajustes_dano_1280", "error", "sin PauseController"); yield break; }
            finalizadores.Add(() => { try { if (pc != null) { pc.OnSettingsBackClicked(); if (pc.IsPaused) pc.OnContinueClicked(); } } catch (Exception) { } Screen.SetResolution(1600, 900, false); Time.timeScale = 1f; });
            var res = new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) }; var ids = new[] { "ajustes_dano_1280", "ajustes_dano_1600", "ajustes_dano_1920" };
            for (int i = 0; i < 3; i++)
            {
                yield return Resolucion(res[i].x, res[i].y);
                pc.ShowPause(); pc.OnSettingsClicked(); yield return Espera(1f);
                var panel = pc.transform.Find("SettingsPanel");
                if (panel == null) { D(ids[i], "error", "sin SettingsPanel"); }
                else MedirAjustes(ids[i], panel);
                yield return FotoJuego(ids[i]);
                pc.OnSettingsBackClicked(); if (pc.IsPaused) pc.OnContinueClicked(); yield return Espera(0.4f);
            }
            yield return Resolucion(1600, 900);
        }

        // ================================================================ CELDAS: cinematica de liberacion (T6)
        IEnumerator Celdas()
        {
            Saltar(5); yield return Espera(3f);
            var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo; if (d == null) yield break;
            d.SaltarAlSegundoPisoPorPrueba(); yield return Espera(3f);
            if (d.TerminalDeCeldas == null) { Llamar(d, "CrearTerminalDeCeldas"); yield return Espera(0.5f); }
            yield return EsperarHasta(() => !d.CineDeMansionEnCurso, 10f);           // la cinematica de llegada al 2.o piso dura 6,5 s
            var yo = Yo(); if (yo != null) { var pu = d.PosDeUsoDeLaTerminal; pu.y += 0.9f; yo.transform.position = pu; Physics.SyncTransforms(); }
            yield return Espera(1f);
            D("celdas_0", "liberados_antes", d.MilicianosLiberados);
            OperacionTerminal.PruebaToqueE = true;
            float t0 = Time.realtimeSinceStartup, cineIni = -1f, cineFin = -1f; bool ctrlDurante = true, ctrlVisto = false, ctrlDespues = false;
            var tiempos = new[] { 0.3f, 1.0f, 1.8f, 2.6f }; var ids = new[] { "celdas_0", "celdas_1", "celdas_2", "celdas_3" };
            for (int i = 0; i < 4; i++)
            {
                while (Time.realtimeSinceStartup - t0 < tiempos[i])
                {
                    float t = Time.realtimeSinceStartup - t0;
                    if (d.CineDeMansionEnCurso) { if (cineIni < 0f) cineIni = t; cineFin = t; if (drv != null) { ctrlVisto = true; if (drv.enabled) ctrlDurante = false; } }
                    yield return null;
                }
                if (i == 0 && Quiero("celdas_0")) { D("celdas_0", "cine_en_curso", d.CineDeMansionEnCurso); }
                if (i == 3) { ctrlDespues = drv != null && drv.enabled; D("celdas_3", "liberados", d.MilicianosLiberados); D("celdas_3", "reja_abierta", Leer(d, "RejaAbierta") ?? "n/a"); D("celdas_3", "control_despues", ctrlDespues); var al = TextosActivos("LIBERADOS"); D("celdas_3", "alerta", al.Count > 0 ? string.Join(" | ", al) : "ninguna"); D("celdas_3", "milicianos", MandoTactico.Milicianos.Count); }
                yield return FotoJuego(ids[i]);
            }
            // si la cinematica sigue o el estado cambia despues del ultimo cuadro, seguimos mirando hasta 4 s
            while (Time.realtimeSinceStartup - t0 < 4f) { float t = Time.realtimeSinceStartup - t0; if (d.CineDeMansionEnCurso) { if (cineIni < 0f) cineIni = t; cineFin = t; } yield return null; }
            D("celdas_0", "cine_s", cineIni >= 0f ? cineFin - cineIni : 0f); D("celdas_0", "control_durante", ctrlVisto ? (ctrlDurante ? "SIN_QUITAR" : "quitado") : "n/a");
            D("celdas_0", "cine_inicio_s", cineIni);
        }

        // ================================================================ PERROS DE LA CIUDAD (T7)
        IEnumerator Ciudad()
        {
            Saltar(5); yield return Espera(2f);
            var d = OperacionDirector.Instancia;
            if (d.plaza != null)
            {
                var yoC = Yo(); if (yoC != null) { yoC.transform.position = EnMalla(d.plaza.position + new Vector3(0f, 0.5f, -6f)); Physics.SyncTransforms(); }
                int kk = 0; foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Player && a != yoC && a.gameObject.activeInHierarchy) { a.transform.position = EnMalla(d.plaza.position + new Vector3(-3f + 2f * (kk++ % 4), 0.5f, -9f)); ApoyoEnElPiso.Apoyar(a.transform); }
            }
            finalizadores.Add(() => Time.timeScale = 1f);
            // la escuadra quieta (no mata a los perros antes de medirlos) y el jugador invulnerable
            { var yoI = Yo(); CongelarAliados(yoI); if (yoI != null && yoI.Health != null) { bool inv0 = yoI.Health.Invulnerable; yoI.Health.Invulnerable = true; finalizadores.Add(() => { if (yoI != null) yoI.Health.Invulnerable = inv0; }); } }
            Time.timeScale = 4f; float t0 = Time.realtimeSinceStartup;
            var mins = new List<float>(); var pares = new HashSet<long>(); int cuadros = 0, bien = 0; float minGlobal = 1e9f; var buf = new List<PerroEnemigo>();
            while (Time.realtimeSinceStartup - t0 < 30f)
            {
                buf.Clear();
                foreach (var p in FindObjectsByType<PerroEnemigo>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) { var s = p.GetComponent<Soldier>(); if (s != null && s.Health != null && s.Health.IsAlive && p.Estado != PerroEnemigo.Fase.Salto && p.Estado != PerroEnemigo.Fase.Muerto) buf.Add(p); }
                if (buf.Count >= 2)
                {
                    float m = 1e9f;
                    for (int i = 0; i < buf.Count; i++) for (int j = i + 1; j < buf.Count; j++)
                    {
                        var a = buf[i].transform.position; var b = buf[j].transform.position; float dd = new Vector2(a.x - b.x, a.z - b.z).magnitude;
                        if (dd < m) m = dd; if (dd < 0.4f) pares.Add(((long)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(buf[i]) << 32) ^ (uint)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(buf[j]));
                    }
                    mins.Add(m); cuadros++; if (m >= 0.7f) bien++; if (m < minGlobal) minGlobal = m;
                }
                yield return null;
            }
            Time.timeScale = 1f;
            mins.Sort(); float p01 = mins.Count > 0 ? mins[Mathf.Clamp((int)(mins.Count * 0.01f), 0, mins.Count - 1)] : -1f;
            int perros = 0, vivos = 0, escuderos = 0, pesados = 0, refuerzos = 0;
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.name.StartsWith("Refuerzo_Ciudad_")) refuerzos++;
            foreach (var p in FindObjectsByType<PerroEnemigo>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { perros++; var s = p.GetComponent<Soldier>(); if (s != null && s.Health.IsAlive) vivos++; }
            foreach (var e in FindObjectsByType<EnemigoEscudero>(FindObjectsInactive.Include, FindObjectsSortMode.None)) escuderos++;
            foreach (var e in FindObjectsByType<EnemigoPesado>(FindObjectsInactive.Include, FindObjectsSortMode.None)) pesados++;
            Dato($"ciudad tras 30 s a x4 (120 s de juego): refuerzos creados={d.RefuerzosCreados} (en escena {refuerzos}) perros={perros} (vivos {vivos}) escuderos={escuderos} pesados={pesados} reloj={F(d.Reloj)}");
            D("perros_manada", "min_p01", p01); D("perros_manada", "min_global", minGlobal < 1e8f ? minGlobal : -1f); D("perros_manada", "pares_lt_0_4", pares.Count);
            D("perros_manada", "pct_ge_0_7", cuadros > 0 ? 100f * bien / cuadros : -1f); D("perros_manada", "cuadros_con_2_o_mas", cuadros); D("perros_manada", "perros_vivos", vivos); D("perros_manada", "perros_creados", perros);
            D("perros_manada", "separacion_activa", Leer(typeof(PerroEnemigo), "SeparacionActiva") ?? "n/a");
            var yo = Yo(); var c = yo != null ? yo.transform.position : Vector3.zero;
            PerroEnemigo cerca = null; float md = 1e9f; var centroide = Vector3.zero; int nn = 0;
            foreach (var p in FindObjectsByType<PerroEnemigo>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) { var s = p.GetComponent<Soldier>(); if (s == null || !s.Health.IsAlive) continue; float dd = Vector3.Distance(p.transform.position, c); if (dd < md) { md = dd; cerca = p; } centroide += p.transform.position; nn++; }
            if (nn > 0) { centroide /= nn; yield return Foto("perros_manada", centroide + new Vector3(0f, 14f, -3f), centroide, 50f, false); }
            yield return Foto("ciudad_vista", c + new Vector3(0f, 60f, -18f), c, 62f, false);
            if (cerca != null) yield return Foto("ciudad_perro", cerca.transform.position + Vector3.up * 1.4f + cerca.transform.right * 2.6f, cerca.transform.position + Vector3.up * 0.4f, 50f);
        }

        // ================================================================ POPUPS: fuentes (T22)
        IEnumerator FuentesPopups()
        {
            Saltar(2); yield return Espera(3f);
            yield return HayEnemigosParaClonar();
            SinDios(); var yo = Yo(); if (yo == null) yield break;
            CongelarAliados(yo);
            var f = yo.transform.forward; f.y = 0f; f.Normalize();
            var victima = Clon(yo.transform.position + f * 8f, -f); if (victima == null) { D("fuentes_popups", "error", "sin molde"); yield break; }
            if (victima.Brain != null) victima.Brain.Anulado = true;
            yield return Espera(0.6f);
            victima.Health.TakeDamage(10, yo.Id, true);
            yield return null;
            var malos = new List<string>();
            foreach (var t in FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (t != null && t.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(t.text) && t.color.a >= 0.05f && !EsFuenteBelica(t.font)) malos.Add(t.name + ":" + (t.font != null ? t.font.name : "null") + ":" + (t.text.Length > 14 ? t.text.Substring(0, 14) : t.text).Replace("\n", " "));
            D("fuentes_popups", "no_belicas_lista", malos.Count > 0 ? string.Join(" | ", malos.GetRange(0, Mathf.Min(8, malos.Count))) : "-");
            yield return FotoJuego("fuentes_popups");
            if (victima != null) Destroy(victima.gameObject);
        }

        // ================================================================ RENDIMIENTO (T14, T15, T16)
        IEnumerator PerfEnPunto(string id, Vector3 pos, Vector3 mira, float seg)
        {
            var cam = CamaraDePantalla("CamPerf_" + id); cam.transform.position = pos; cam.transform.rotation = Quaternion.LookRotation(mira - pos); cam.fieldOfView = 60f;
            yield return Perf(seg, id); Destroy(cam.gameObject);
        }

        IEnumerator PerfTodas()
        {
            SinAceleracion();
            Screen.SetResolution(1600, 900, false); yield return Espera(1f);
            // perf_plaza y perf_inicio: camara fija en las tomas "ojo" (primero la fase 5 y la 1: de la 4 no se vuelve a la 5)
            Saltar(5); yield return Espera(2f);
            yield return PerfEnPunto("perf_plaza", new Vector3(322f, 1.7f, 205f), new Vector3(322f, 1.2f, 228f), 8f);
            Saltar(1); yield return Espera(3f);
            yield return PerfEnPunto("perf_inicio", new Vector3(0f, 1.7f, -378f), new Vector3(0f, 1.5f, -340f), 8f);
            // perf_ruta: recorrido de la ruta del tanque a 18 m/s y 2,5 m de altura durante 20 s
            Saltar(4); yield return Espera(3f);
            var pts = new List<Vector3>(); Transform ruta = null;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t != null && t.name == "RutaTanque" && t.parent != null && t.parent.name == "5_Autopista") { ruta = t; break; }
            if (ruta == null) ruta = BuscarPorNombre("RutaTanque", true);
            if (ruta != null) foreach (Transform p in ruta) pts.Add(new Vector3(p.position.x, 0f, p.position.z));
            D("perf_ruta", "origen_ruta", ruta != null ? "RutaTanque(" + pts.Count + " puntos)" : "recta (0,40)->(262,228)");
            if (pts.Count < 2) { pts.Clear(); pts.Add(new Vector3(0f, 0f, 40f)); pts.Add(new Vector3(262f, 0f, 228f)); }
            else pts.Insert(0, new Vector3(0f, 0f, 22f));
            var camR = CamaraDePantalla("CamPerfRuta"); camR.fieldOfView = 65f;
            var dts = new List<float>(); float rec = 0f; float t0 = Time.realtimeSinceStartup; int seg = 0; float enSeg = 0f; Vector3 antes = pts[0];
            yield return null; yield return null;
            while (Time.realtimeSinceStartup - t0 < 20f)
            {
                float dt = Time.unscaledDeltaTime; dts.Add(dt);
                float avance = 18f * dt;
                while (avance > 0f && seg < pts.Count - 1)
                {
                    float len = Vector3.Distance(pts[seg], pts[seg + 1]); float falta = len - enSeg;
                    if (avance < falta) { enSeg += avance; avance = 0f; } else { avance -= falta; seg++; enSeg = 0f; }
                }
                if (seg >= pts.Count - 1) { seg = 0; enSeg = 0f; }
                var pos = Vector3.Lerp(pts[seg], pts[Mathf.Min(seg + 1, pts.Count - 1)], enSeg / Mathf.Max(0.01f, Vector3.Distance(pts[seg], pts[Mathf.Min(seg + 1, pts.Count - 1)])));
                float y = 0f; if (Physics.Raycast(new Vector3(pos.x, 300f, pos.z), Vector3.down, out var hit, 400f, ~0, QueryTriggerInteraction.Ignore)) y = hit.point.y;
                pos.y = y + 2.5f; var adelante = pts[Mathf.Min(seg + 1, pts.Count - 1)] - pts[seg]; adelante.y = 0f; if (adelante.sqrMagnitude < 0.01f) adelante = Vector3.forward;
                camR.transform.position = pos; camR.transform.rotation = Quaternion.LookRotation(adelante.normalized + Vector3.down * 0.05f);
                yield return null;
            }
            ultimoPerf = CalcPerf(dts); DatosPerf("perf_ruta");
            Destroy(camR.gameObject);
        }
    }
}
