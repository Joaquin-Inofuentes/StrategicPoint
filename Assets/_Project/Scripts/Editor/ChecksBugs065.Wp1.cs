using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Mision;
using SP.Player;
using SP.Presentation;
using SP.UI;

namespace SP.EditorTools
{
    // WP1: combate. #094 headshots, #095 municion en el HUD, #079 rachas, #065 cursor de baja, #096 Q con margen y diana.
    // Hay que estar en Play sobre SC_Operacion.
    public static partial class ChecksBugs065
    {
        static Soldier Poseido()
        {
            var d = PlayerInputDriver.Activo;
            return d != null && d.Brain != null ? d.Brain.Current : null;
        }

        // ---------------------------------------------------------------- #094 headshots
        // Centro VISIBLE de la cabeza: vertices de la malla pesados al hueso Head, con la pose de este frame (independiente
        // de HitboxCabeza, que se mide con la pose de reposo).
        static bool CabezaVisible(Soldier e, out Vector3 centro)
        {
            centro = default;
            var anim = e.GetComponentInChildren<Animator>();
            var hueso = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            var smr = e.GetComponentInChildren<SkinnedMeshRenderer>();
            if (hueso == null || smr == null) return false;
            int idx = Array.IndexOf(smr.bones, hueso);
            if (idx < 0) return false;
            var malla = new Mesh();
            smr.BakeMesh(malla);
            var vs = malla.vertices; var pesos = smr.sharedMesh.boneWeights;
            Bounds b = default; bool primero = true;
            for (int i = 0; i < vs.Length; i++)
            {
                var w = pesos[i];
                float p = (w.boneIndex0 == idx ? w.weight0 : 0f) + (w.boneIndex1 == idx ? w.weight1 : 0f) + (w.boneIndex2 == idx ? w.weight2 : 0f) + (w.boneIndex3 == idx ? w.weight3 : 0f);
                if (p < 0.5f) continue;
                var wp = smr.transform.TransformPoint(vs[i]);
                if (primero) { b = new Bounds(wp, Vector3.zero); primero = false; } else b.Encapsulate(wp);
            }
            UnityEngine.Object.Destroy(malla);
            if (primero) return false;
            centro = b.center;
            return true;
        }

        // Candidatos: enemigos vivos con un lugar libre a ~10 m y linea de tiro limpia. Devuelve la posicion del jugador.
        static bool UbicarJugador(Soldier yo, Soldier e, float dist, out Vector3 pos, out Vector3 dir)
        {
            pos = default; dir = default;
            foreach (var ang in new float[] { 0, 90, 180, 270, 45, 135, 225, 315 })
            {
                dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                pos = e.transform.position + dir * dist;
                if (Physics.CheckCapsule(pos + Vector3.up * 0.1f, pos + Vector3.up * 0.9f, 0.4f, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
                bool tapado = false;
                foreach (var h in new float[] { 0.2f, 0.7f, 1.2f, 1.7f })
                    if (Physics.Linecast(pos + Vector3.up * h, e.transform.position + Vector3.up * h, out var hit, ~(1 << 2), QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<Soldier>() != e) { tapado = true; break; }
                if (tapado) continue;
                return true;
            }
            return false;
        }

        static float AlturaDelHueso(Soldier e)
        {
            var anim = e.GetComponentInChildren<Animator>();
            var h = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            return h != null ? h.position.y : -99f;
        }

        // Congela la animacion del blanco en una postura: el driver deja de pisar el parametro y se lo fija a mano.
        static readonly List<MonoBehaviour> congelados = new List<MonoBehaviour>();
        static void FijarPostura(Soldier e, bool agachado)
        {
            e.Motor.SetCrouching(agachado);
            var drv = e.GetComponentInChildren<SP.Presentation.SoldierAnimatorDriver>();
            if (drv != null && drv.enabled) { drv.enabled = false; congelados.Add(drv); }
            var anim = e.GetComponentInChildren<Animator>();
            if (anim != null) anim.SetBool(SP.Presentation.SoldierAnimatorDriver.ParamAgachado, agachado);
        }

        static readonly StringBuilder detalleDeTiros = new StringBuilder();

        // Dispara de verdad (PlayerBrain.Fire) al punto dado, apuntando (ADS) para que la dispersion no tape lo que se mide.
        static IEnumerable Tirar(Soldier yo, Soldier objetivo, Func<Vector3> punto, int cuantos, int[] salida, bool agachado, bool conInstakill = false)
        {
            var drv = PlayerInputDriver.Activo;
            int hs0 = Projectile.HeadshotsDelJugador;
            int imp0 = EstadisticasDeMision.Instancia != null ? EstadisticasDeMision.Instancia.Impactos : 0;
            // #107: el headshot del jugador ahora MATA; para medir varios tiros seguidos sobre el mismo blanco se apaga el instakill (el headshot se cuenta igual).
            bool matarAntes = Projectile.HeadshotMataAlInstante; Projectile.HeadshotMataAlInstante = conInstakill;
            for (int i = 0; i < cuantos; i++)
            {
                // El blanco se agacha o se levanta solo (reaccion al dano / cerebro que el director reactiva): se lo fuerza a su
                // postura cada frame y recien se tira cuando la animacion la alcanzo y el arma esta lista, en el mismo frame.
                float t0 = Time.realtimeSinceStartup;
                bool disparo = false;
                while (Time.realtimeSinceStartup - t0 < 5f)
                {
                    objetivo.Health.Heal(objetivo.Health.MaxHealth);
                    var br = objetivo.GetComponent<SP.Ai.AiBrain>(); if (br != null) br.enabled = false;
                    FijarPostura(objetivo, agachado);
                    yo.Weapon.SetApuntado(1f);
                    yo.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(objetivo.transform.position - yo.transform.position, Vector3.up));
                    float hy = AlturaDelHueso(objetivo);
                    bool postura = objetivo.Motor.IsCrouching == agachado && (agachado ? hy < 1.10f : hy > 1.35f);
                    if (postura && yo.Weapon.CooldownRemaining <= 0f && !yo.Weapon.IsReloading && yo.Weapon.SpreadDegEfectivo < 0.15f)
                    {
                        disparo = drv.Brain.Fire(punto());
                        if (disparo) { detalleDeTiros.Append($"(fuego: crouch={objetivo.Motor.IsCrouching} hueso y={AlturaDelHueso(objetivo):0.00} t={Time.realtimeSinceStartup - t0:0.0}s) "); break; }
                    }
                    yield return null;
                }
                // La bala viaja unos frames: dejar que impacte antes de contar.
                for (int f = 0; f < 6; f++) { yo.Weapon.SetApuntado(1f); yield return null; }
                var pt = punto();
                detalleDeTiros.Append($"[a y={pt.y:0.00} muzzle y={(yo.Weapon.Muzzle != null ? yo.Weapon.Muzzle.position.y : -1f):0.00} impacto y={Projectile.UltimoImpactoEnSoldado.y:0.00} hs={Projectile.UltimoImpactoFueCabeza} caja y={(objetivo.GetComponentInChildren<HitboxCabeza>(true) != null ? objetivo.GetComponentInChildren<HitboxCabeza>(true).Centro.y : -1f):0.00} crouch={objetivo.Motor.IsCrouching} {objetivo.name}] ");
            }
            for (int f = 0; f < 10; f++) yield return null;
            Projectile.HeadshotMataAlInstante = matarAntes;
            salida[0] = Projectile.HeadshotsDelJugador - hs0;
            salida[1] = (EstadisticasDeMision.Instancia != null ? EstadisticasDeMision.Instancia.Impactos : 0) - imp0;
        }

        static IEnumerator Bug094()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var yo = Poseido();
            if (yo == null || yo.Weapon == null) { Fin("FALLO sin soldado poseido"); yield break; }
            var sb = new StringBuilder(); bool ok = true; detalleDeTiros.Clear();
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var apagados = new List<MonoBehaviour>();
            foreach (var a in ActorRegistry.All)
            {
                var s = a as Soldier; if (s == null || s == yo) continue;
                var br = s.GetComponent<SP.Ai.AiBrain>();
                if (br != null && br.enabled) { br.enabled = false; apagados.Add(br); }
            }
            try
            {
                // Un blanco de pie y otro agachado, con linea de tiro, sin hitbox de cabeza todavia (primer tiro del enemigo).
                Soldier dePie = null, agachado = null; Vector3 posDePie = default, dirDePie = default, posAg = default, dirAg = default;
                foreach (var a in ActorRegistry.All)
                {
                    var e = a as Soldier;
                    if (e == null || e.Team != TeamId.Enemy || e.Health == null || !e.Health.IsAlive || !e.gameObject.activeInHierarchy) continue;
                    if (e.Brain != null && e.Brain.Atrincherado) continue;   // WP4 (#085): los guardias se quedan DETRAS de su barricada (tapan el tiro)
                    bool ag = e.Motor != null && e.Motor.IsCrouching;
                    if ((ag ? agachado : dePie) != null) continue;
                    if (!UbicarJugador(yo, e, 10f, out var pos, out var dir)) continue;
                    if (ag) { agachado = e; posAg = pos; dirAg = dir; } else { dePie = e; posDePie = pos; dirDePie = dir; }
                    if (dePie != null && agachado != null) break;
                }
                if (dePie == null && agachado == null) { Fin("FALLO no encontre ningun enemigo con linea de tiro a 10 m"); yield break; }

                // ---- Escenario 1: enemigo de pie
                if (dePie != null)
                {
                    bool sinCaja = dePie.GetComponentInChildren<HitboxCabeza>(true) == null;
                    var br = dePie.GetComponent<SP.Ai.AiBrain>(); if (br != null) { br.enabled = false; apagados.Add(br); }
                    dePie.Motor.SetCrouching(false);
                    yo.transform.position = posDePie;
                    dePie.transform.rotation = Quaternion.LookRotation(dirDePie);
                    foreach (var x in Esperar(0.8f)) yield return x;
                    var smr = dePie.GetComponentInChildren<SkinnedMeshRenderer>();
                    float mallaMax = smr.bounds.max.y;
                    Vector3 P(float y) => new Vector3(dePie.transform.position.x, y, dePie.transform.position.z);
                    var r = new int[2];

                    int hsPrimero0 = Projectile.HeadshotsDelJugador;
                    foreach (var x in Tirar(yo, dePie, () => P(mallaMax - 0.30f), 1, r, false)) yield return x;
                    bool primeroHs = r[0] > 0;
                    int hsCabeza = r[0], impCabeza = r[1];
                    foreach (var x in Tirar(yo, dePie, () => P(mallaMax - 0.30f), 7, r, false)) yield return x;
                    hsCabeza += r[0]; impCabeza += r[1];
                    bool bCabeza = hsCabeza >= 6;
                    sb.Append($"de pie: cabeza visible {hsCabeza}/8 headshots ({impCabeza} impactos, primer tiro a enemigo sin caja={(sinCaja ? "si" : "no")} fue headshot={primeroHs}){(bCabeza ? "" : " (MAL, se piden >=6)")}; ");

                    foreach (var x in Tirar(yo, dePie, () => P(dePie.transform.position.y + 0.1f), 8, r, false)) yield return x;
                    bool bPecho = r[0] == 0 && r[1] >= 6;
                    sb.Append($"pecho {r[0]}/8 headshots ({r[1]} impactos){(bPecho ? "" : " (MAL, se piden 0 headshots y >=6 impactos)")}; ");

                    foreach (var x in Tirar(yo, dePie, () => P(mallaMax - 0.08f), 4, r, false)) yield return x;
                    bool bCasco = r[1] >= 3;
                    sb.Append($"casco {r[1]}/4 impactos ({r[0]} headshots){(bCasco ? "" : " (MAL, se piden >=3 impactos)")}; ");

                    if (!(bCabeza && bPecho && bCasco)) ok = false;
                }
                else sb.Append("no hay enemigo de pie con linea de tiro (se omite); ");

                // ---- Escenario 2: enemigo agachado (el caso real de los guardias del Cuartel): apuntando al centro VISIBLE de la cabeza
                if (agachado != null)
                {
                    var br = agachado.GetComponent<SP.Ai.AiBrain>(); if (br != null) { br.enabled = false; apagados.Add(br); }
                    yo.transform.position = posAg;
                    agachado.transform.rotation = Quaternion.LookRotation(dirAg);
                    foreach (var x in Esperar(0.6f)) yield return x;
                    var r = new int[2];
                    foreach (var x in Tirar(yo, agachado, () => CabezaVisible(agachado, out var c) ? c : agachado.transform.position, 6, r, true)) yield return x;
                    bool bAg = r[0] >= 5;
                    sb.Append($"agachado: cabeza visible {r[0]}/6 headshots ({r[1]} impactos){(bAg ? "" : " (MAL, se piden >=5)")}; ");
                    foreach (var x in Tirar(yo, agachado, () => new Vector3(agachado.transform.position.x, 0.45f, agachado.transform.position.z), 4, r, true)) yield return x;
                    bool bAgPecho = r[0] == 0;
                    sb.Append($"agachado a la cadera (y=0.45) {r[0]}/4 headshots{(bAgPecho ? "" : " (MAL, se piden 0)")}; ");
                    if (!(bAg && bAgPecho)) ok = false;
                }
                else sb.Append("no hay enemigo agachado con linea de tiro (se omite); ");

                // ---- Conteo en las estadisticas de mision
                var est = EstadisticasDeMision.Instancia;
                var filas = EstadisticasDeMision.Filas(true);
                EstadisticasDeMision.Fila? fila = null;
                foreach (var f in filas) if (f.Etiqueta == "HEADSHOTS") fila = f;
                bool conteo = est != null && fila.HasValue && fila.Value.Numero == est.HeadshotsJugador && est.HeadshotsJugador == Projectile.HeadshotsDelJugador && est.HeadshotsJugador > 0;
                if (!conteo) ok = false;
                sb.Append($"estadisticas: fila HEADSHOTS={(fila.HasValue ? fila.Value.Numero.ToString() : "falta")} stats={(est != null ? est.HeadshotsJugador : -1)} proyectil={Projectile.HeadshotsDelJugador}{(conteo ? "" : " (MAL)")}; ");
            }
            finally
            {
                foreach (var m in apagados) if (m != null) m.enabled = true;
                foreach (var m in congelados) if (m != null) m.enabled = true;
                congelados.Clear();
                ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb + (ok ? "" : " DETALLE: " + detalleDeTiros));
        }

        // Captura de pantalla del juego (Game View) al terminar el frame; el PNG queda en Assets/Validacion.
        static IEnumerable CapturarPantalla(string archivo)
        {
            string ruta = RutaValidacion(archivo);
            if (System.IO.File.Exists(ruta)) System.IO.File.Delete(ruta);
            ScreenCapture.CaptureScreenshot(ruta, 1);
            float t0 = Time.realtimeSinceStartup;
            while (!System.IO.File.Exists(ruta) && Time.realtimeSinceStartup - t0 < 3f) yield return null;
        }

        // #094: el popup HEADSHOT se ve (visible, con texto) y queda en una captura del Game View.
        static IEnumerator Bug094p()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            SP.UI.HeadshotPopup.Mostrar(false);
            yield return null; yield return null;
            bool visible = SP.UI.HeadshotPopup.Visible;
            string texto = SP.UI.HeadshotPopup.Texto;
            foreach (var x in CapturarPantalla("v2_094_headshot.png")) yield return x;
            Fin((visible && texto.Contains("HEADSHOT") ? "OK " : "FALLO ") + $"popup visible={visible} texto='{texto}' (captura v2_094_headshot.png)");
        }

        // ---------------------------------------------------------------- #095 municion total real en el HUD
        static IEnumerator Bug095()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.2f)) yield return x;
            var yo = Poseido();
            var vista = SP.UI.WeaponStatusView.Activo;
            if (yo == null || yo.Weapon == null) { Fin("FALLO sin soldado poseido"); yield break; }
            if (vista == null) { Fin("FALLO no hay WeaponStatusView activo"); yield break; }
            var w = yo.Weapon; var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            try
            {
                // 1) La Operacion usa reservas con 4 cargadores por arma.
                bool reservas = WeaponHolder.ReservasActivas && w.UsaReservas && WeaponHolder.CargadoresDeReserva == SP.Operacion.OperacionDirector.CargadoresPorDificultad();   // #110: 8/6/4 segun la dificultad
                bool reservaInicial = w.ReservaActual == w.MagazineSize * SP.Operacion.OperacionDirector.CargadoresPorDificultad();
                if (!reservas || !reservaInicial) ok = false;
                sb.Append($"UsaReservas={w.UsaReservas} cargadoresIniciales={WeaponHolder.CargadoresDeReserva} reserva={w.ReservaActual} (esperada {w.MagazineSize * SP.Operacion.OperacionDirector.CargadoresPorDificultad()}){(reservas && reservaInicial ? "" : " (MAL)")}; ");

                // 2) El texto del HUD es "cargador / (cargador + reserva)".
                yield return null; yield return null;
                string esperado = $"{w.CurrentAmmo} / {w.CurrentAmmo + w.ReservaActual}";
                bool texto = vista.Texto == esperado;
                if (!texto) ok = false;
                sb.Append($"HUD='{vista.Texto}' esperado='{esperado}'{(texto ? "" : " (MAL)")}; ");

                // 3) Disparar baja el total en 1.
                int total0 = w.MunicionTotal;
                yo.Weapon.SetApuntado(1f);
                bool disparo = false;
                for (int i = 0; i < 90 && !disparo; i++) { disparo = PlayerInputDriver.Activo.Brain.Fire(yo.transform.position + yo.transform.forward * 20f); if (!disparo) yield return null; }
                yield return null; yield return null;
                bool baja = disparo && w.MunicionTotal == total0 - 1 && vista.Texto == $"{w.CurrentAmmo} / {w.MunicionTotal}";
                if (!baja) ok = false;
                sb.Append($"disparo: total {total0}->{w.MunicionTotal}, HUD='{vista.Texto}'{(baja ? "" : " (MAL)")}; ");

                // 4) Un MunicionPickup sube el total del arma equipada y reparte a las otras armas del loadout.
                w.CycleNext(); int otraAntes = w.ReservaActual; var otraArma = w.CurrentWeaponKind; w.CyclePrevious();
                int total1 = w.MunicionTotal;
                MunicionPickup.Crear(yo.transform.position);
                for (int i = 0; i < 10; i++) yield return null;
                int total2 = w.MunicionTotal;
                w.CycleNext(); int otraDespues = w.ReservaActual; w.CyclePrevious();
                bool sube = total2 > total1 && vista.Texto == $"{w.CurrentAmmo} / {w.MunicionTotal}";
                bool reparte = otraDespues > otraAntes;
                if (!sube || !reparte) ok = false;
                sb.Append($"pickup: total {total1}->{total2}, HUD='{vista.Texto}'{(sube ? "" : " (MAL)")}; otra arma ({otraArma}) reserva {otraAntes}->{otraDespues}{(reparte ? "" : " (MAL, no reparte)")}; ");

                // 5) Sin reservas (ilimitada) el HUD dice "/ ∞" y la fuente tiene el glifo.
                Soldier aliado = null;
                foreach (var a in ActorRegistry.All) { var s = a as Soldier; if (s != null && s != yo && s.Team == TeamId.Player && s.Weapon != null && !s.Weapon.UsaReservas) { aliado = s; break; } }
                if (aliado != null)
                {
                    vista.UpdateFrom(aliado.Weapon);
                    bool inf = vista.Texto.EndsWith("∞") && vista.FuenteDelContador != null && vista.FuenteDelContador.HasCharacter('∞');
                    if (!inf) ok = false;
                    sb.Append($"ilimitada: HUD='{vista.Texto}' glifo={(vista.FuenteDelContador != null && vista.FuenteDelContador.HasCharacter('∞'))}{(inf ? "" : " (MAL)")}; ");
                    vista.UpdateFrom(w);
                }
                else sb.Append("sin aliado para probar el simbolo de infinito; ");

                w.ReponerMunicion();
                yield return null; yield return null;
                foreach (var x in CapturarPantalla("v2_095_hud.png")) yield return x;
            }
            finally { ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #079 rachas y FURIA
        static void MatarComo(Soldier yo, Soldier e)
        {
            if (e != null && e.Health != null && e.Health.IsAlive) e.Health.TakeDamage(e.Health.Current + 999, yo.Id);
        }

        static IEnumerator Bug079()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(9f)) yield return x;   // deja pasar el cartel de objetivo para que la captura salga limpia
            var yo = Poseido(); var racha = RachaDeBajas.Instancia;
            if (yo == null || racha == null) { Fin("FALLO sin soldado poseido o sin RachaDeBajas"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var enemigos = new List<Soldier>();
            foreach (var a in ActorRegistry.All)
            {
                var s = a as Soldier;
                if (s != null && s.Team == TeamId.Enemy && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy) enemigos.Add(s);
            }
            if (enemigos.Count < 18) { ModoDios.Poner(diosAntes); Fin($"FALLO hacen falta 18 enemigos vivos (hay {enemigos.Count})"); yield break; }
            int idx = 0;
            try
            {
                // Escenario de dano: un enemigo a 10 m. Sin FURIA un tiro al pecho le saca X; con FURIA, 2X.
                Soldier blanco = null; Vector3 posTiro = default, dirTiro = default;
                for (int i = enemigos.Count - 1; i >= 8 && blanco == null; i--)
                    if (!(enemigos[i].Brain != null && enemigos[i].Brain.Atrincherado)   // WP4 (#085): los guardias se quedan DETRAS de su barricada (tapan el tiro)
                        && UbicarJugador(yo, enemigos[i], 10f, out posTiro, out dirTiro)) blanco = enemigos[i];
                int danoBase = -1, danoFuria = -1;

                // 1) Dos bajas en menos de 4 s: DOBLE KILL.
                MatarComo(yo, enemigos[idx++]);
                yield return null;
                bool sinCartel = !CartelDeRacha.Visible;
                MatarComo(yo, enemigos[idx++]);
                yield return null;
                bool doble = CartelDeRacha.Visible && CartelDeRacha.Texto == "DOBLE KILL" && racha.Multikill == 2;
                if (!sinCartel || !doble) ok = false;
                sb.Append($"1 baja sin cartel={sinCartel}; 2 bajas: '{CartelDeRacha.Texto}' (multikill={racha.Multikill}){(doble ? "" : " (MAL)")}; ");

                // 2) 3 y 4: TRIPLE / CUADRUPLE.
                MatarComo(yo, enemigos[idx++]); yield return null;
                string t3 = CartelDeRacha.Texto;
                MatarComo(yo, enemigos[idx++]); yield return null;
                string t4 = CartelDeRacha.Texto;
                bool b34 = t3 == "TRIPLE KILL" && t4 == "CUÁDRUPLE KILL" && !racha.FuriaActiva;
                if (!b34) ok = false;
                sb.Append($"3: '{t3}', 4: '{t4}', furia aun apagada={!racha.FuriaActiva}{(b34 ? "" : " (MAL)")}; ");

                // Un tiro real sin FURIA (referencia de dano) justo antes de la quinta baja.
                if (blanco != null)
                {
                    yo.transform.position = posTiro;
                    blanco.GetComponent<SP.Ai.AiBrain>().enabled = false;
                    foreach (var x in Esperar(0.4f)) yield return x;
                    var rb = new int[1];
                    foreach (var x in TiroDeMedicion(yo, blanco, rb)) yield return x;
                    danoBase = rb[0];
                    // El tiro de referencia tardo: las bajas de arriba pueden haber vencido la ventana (se reponen abajo).
                }

                // 3) Racha de 5 en ventana: PENTA KILL + FURIA. Se rearma la racha (la espera del tiro pudo cortar la ventana).
                racha.SinEnfriamiento();
                for (int i = 0; i < 5; i++) { MatarComo(yo, enemigos[idx++]); yield return null; }
                bool penta = CartelDeRacha.Texto == "PENTA KILL" && racha.FuriaActiva
                    && Mathf.Abs(yo.Motor.FactorDeBuff - 1.3f) < 0.001f && Mathf.Abs(RachaDeBajas.MultiplicadorPara(yo.Id) - 2f) < 0.001f && racha.AuraVisible && CartelDeRacha.FuriaVisible;
                if (!penta) ok = false;
                sb.Append($"5 seguidas: '{CartelDeRacha.Texto}', furia={racha.FuriaActiva}, FactorDeBuff={yo.Motor.FactorDeBuff:0.00}, multiplicador={RachaDeBajas.MultiplicadorPara(yo.Id):0.0}, aura={racha.AuraVisible}, aviso='{CartelDeRacha.TextoFuria}'{(penta ? "" : " (MAL)")}; ");

                // Dano x2 en un disparo real con FURIA.
                if (blanco != null)
                {
                    var rf = new int[1];
                    // WP11: hasta 3 tiros; el cuerpo del blanco se mece y a veces el tiro entra en la cabeza (x2 propio de la zona, no de la FURIA).
                    for (int intento = 0; intento < 3; intento++)
                    {
                        foreach (var x in TiroDeMedicion(yo, blanco, rf)) yield return x;
                        if (danoBase > 0 && rf[0] > 0 && (Mathf.Abs(rf[0] / (float)danoBase - 2f) < 0.15f || Mathf.Abs(rf[0] / (float)danoBase - 4f) < 0.3f)) break;   // 4x = tiro a la cabeza (x2 de zona) con FURIA (x2); #110: con Kes/metralleta el flag de cabeza puede pisarse por balas aliadas
                    }
                    danoFuria = rf[0];
                    bool x2 = danoBase > 0 && danoFuria > 0 && (Mathf.Abs(danoFuria / (float)danoBase - 2f) < 0.15f || Mathf.Abs(danoFuria / (float)danoBase - 4f) < 0.3f);
                    if (!x2) ok = false;
                    sb.Append($"dano de un tiro: base {danoBase} -> FURIA {danoFuria}{(x2 ? "" : " (MAL, se espera x2)")}; ");
                }
                else sb.Append("sin enemigo con linea de tiro para medir el dano; ");

                // Velocidad: MoveSpeed con FURIA = 1.3 x la de sin FURIA.
                float conBuff = yo.Motor.MoveSpeed; yo.Motor.FactorDeBuff = 1f; float sinBuff = yo.Motor.MoveSpeed; yo.Motor.FactorDeBuff = RachaDeBajas.FactorDeVelocidad;
                bool vel = sinBuff > 0.01f && Mathf.Abs(conBuff / sinBuff - 1.3f) < 0.01f;
                if (!vel) ok = false;
                sb.Append($"velocidad {sinBuff:0.00} -> {conBuff:0.00} m/s{(vel ? "" : " (MAL)")}; ");

                // Captura del PENTA KILL con la FURIA (el cartel congelado a mitad de su vida).
                CartelDeRacha.InstanteForzado = 0.5f;
                yield return null; yield return null;
                foreach (var x in CapturarPantalla("v2_079_penta.png")) yield return x;
                CartelDeRacha.InstanteForzado = null;

                // 4) A los 12 s (tiempo de juego) la FURIA termina: todo vuelve a 1.
                float t0 = Time.time;
                while (racha.FuriaActiva && Time.time - t0 < 15f) yield return null;
                float duro = Time.time - t0;
                bool fin = !racha.FuriaActiva && Mathf.Approximately(yo.Motor.FactorDeBuff, 1f) && Mathf.Approximately(RachaDeBajas.MultiplicadorPara(yo.Id), 1f) && !racha.AuraVisible && !CartelDeRacha.FuriaVisible;
                if (!fin) ok = false;
                sb.Append($"la FURIA termino {duro:0.0} s despues de la captura (total ~12 s): FactorDeBuff={yo.Motor.FactorDeBuff:0.00}, multiplicador={RachaDeBajas.MultiplicadorPara(yo.Id):0.0}{(fin ? "" : " (MAL)")}; ");

                // 5) Enfriamiento: 5 bajas seguidas recien terminada la FURIA NO la reactivan (20 s).
                for (int i = 0; i < 5; i++) { MatarComo(yo, enemigos[idx++]); yield return null; }
                bool enfria = !racha.FuriaActiva && racha.EnfriamientoRestante > 15f;
                if (!enfria) ok = false;
                sb.Append($"reactivar de inmediato: furia={racha.FuriaActiva}, enfriamiento restante {racha.EnfriamientoRestante:0} s{(enfria ? "" : " (MAL)")}; ");

                // 6) Mas de 6 s entre bajas corta la racha (y mas de 4 el multikill).
                racha.SinEnfriamiento();
                foreach (var x in Esperar(0.3f)) yield return x;
                // Se espera (tiempo de juego) a que venza la racha anterior.
                float tw = Time.time; while (Time.time - tw < 6.5f) yield return null;
                MatarComo(yo, enemigos[idx++]); yield return null;
                bool corta = racha.Racha == 1 && racha.Multikill == 1;
                if (!corta) ok = false;
                sb.Append($"baja 6.5 s despues: racha={racha.Racha}, multikill={racha.Multikill}{(corta ? "" : " (MAL)")}; ");

                // 7) Al morir el soldado se resetea todo (FURIA incluida).
                racha.SinEnfriamiento();
                for (int i = 0; i < 5 && idx < enemigos.Count; i++) { MatarComo(yo, enemigos[idx++]); yield return null; }
                bool conFuria = racha.FuriaActiva;
                ComandosDeDepuracion.Matar(yo.Id);
                for (int i = 0; i < 3; i++) yield return null;
                bool reset = conFuria && !racha.FuriaActiva && racha.Racha == 0 && racha.Multikill == 0 && Mathf.Approximately(yo.Motor.FactorDeBuff, 1f) && !racha.AuraVisible;
                if (!reset) ok = false;
                sb.Append($"al morir: tenia furia={conFuria}, ahora furia={racha.FuriaActiva} racha={racha.Racha} multikill={racha.Multikill} FactorDeBuff={yo.Motor.FactorDeBuff:0.00}{(reset ? "" : " (MAL)")}; ");
            }
            finally { CartelDeRacha.InstanteForzado = null; ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // Dispara UN tiro real al pecho del blanco (con el jugador ya ubicado) y deja en salida[0] cuanta vida le saco (-1 si no pudo tirar).
        static IEnumerable TiroDeMedicion(Soldier yo, Soldier blanco, int[] salida)
        {
            var drv = PlayerInputDriver.Activo;
            salida[0] = -1;
            float t0 = Time.realtimeSinceStartup;
            bool matarAntes = Projectile.HeadshotMataAlInstante; Projectile.HeadshotMataAlInstante = false;   // #107: aca se mide dano, no el instakill (si la bala entra en la cabeza el blanco no debe morir)
            while (Time.realtimeSinceStartup - t0 < 4f)
            {
                blanco.Health.Heal(blanco.Health.MaxHealth);
                var br = blanco.GetComponent<SP.Ai.AiBrain>(); if (br != null) br.enabled = false;
                yo.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(blanco.transform.position - yo.transform.position, Vector3.up));
                yo.Weapon.SetApuntado(1f);
                if (yo.Weapon.CooldownRemaining <= 0f && !yo.Weapon.IsReloading && yo.Weapon.SpreadDegEfectivo < 0.15f)
                {
                    int antes = blanco.Health.Current;
                    var punto = new Vector3(blanco.transform.position.x, blanco.transform.position.y + 0.1f, blanco.transform.position.z);
                    if (drv.Brain.Fire(punto))
                    {
                        for (int f = 0; f < 10; f++) { yo.Weapon.SetApuntado(1f); yield return null; }
                        salida[0] = antes - blanco.Health.Current;
                        // WP11: el check mide el multiplicador de FURIA, no la zona de impacto. Si la bala pego en la cabeza (el cuerpo se mece y la
                        // caja de la cabeza es generosa) el dano ya viene duplicado: se lo normaliza al dano de torso para comparar torso con torso.
                        if (salida[0] > 0 && Projectile.UltimoImpactoFueCabeza) salida[0] = Mathf.RoundToInt(salida[0] / 2f);
                        Projectile.HeadshotMataAlInstante = matarAntes;
                        yield break;
                    }
                }
                yield return null;
            }
            Projectile.HeadshotMataAlInstante = matarAntes;
        }

        // ---------------------------------------------------------------- #065 cursor de baja
        static IEnumerator Bug065c()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.2f)) yield return x;
            var yo = Poseido(); var marca = SP.UI.MarcaDeImpacto.Instancia;
            if (yo == null || marca == null) { Fin("FALLO sin soldado poseido o sin MarcaDeImpacto"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var enemigos = new List<Soldier>();
            foreach (var a in ActorRegistry.All)
            {
                var s = a as Soldier;
                if (s != null && s.Team == TeamId.Enemy && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy) enemigos.Add(s);
            }
            if (enemigos.Count < 3) { Fin("FALLO hacen falta 3 enemigos vivos"); yield break; }
            try
            {
                // 1) Un impacto que NO mata: marca normal (4 rayas, 0.28 s, sin anillo).
                var herido = enemigos[0];
                herido.Health.TakeDamage(1, yo.Id);
                yield return null;
                bool n1 = !marca.EsBaja && marca.RayasActivas == 4 && marca.DuracionActual < 0.4f && !marca.AnilloVisible;
                if (!n1) ok = false;
                sb.Append($"herida: baja={marca.EsBaja} rayas={marca.RayasActivas} dur={marca.DuracionActual:0.00}s anillo={marca.AnilloVisible}{(n1 ? "" : " (MAL)")}; ");
                foreach (var x in Esperar(0.5f)) yield return x;

                // 2) Baja: 8 rayas, anillo, >= 0.6 s, golpe de escala, rojo (sin headshot).
                var victima = enemigos[1];
                victima.Health.TakeDamage(victima.Health.Current + 999, yo.Id);
                yield return null;
                float dur = marca.DuracionActual;
                marca.ProgresoForzado = 0f;     // t = 0: el estado de arranque (el reloj real ya corrio un par de cuadros)
                yield return null; yield return null;
                float escalaInicial = marca.EscalaActual, radioInicial = marca.RadioDelAnillo;
                marca.ProgresoForzado = 0.5f;   // t = 0.35 s: el anillo termino de expandirse
                yield return null; yield return null;
                float radioFinal = marca.RadioDelAnillo, escalaFinal = marca.EscalaActual;
                marca.ProgresoForzado = 0.3f;   // para la captura en "camara lenta"
                yield return null; yield return null;
                int rayas = marca.RayasActivas;
                bool n2 = marca.EsBaja && !marca.EsHeadshot && rayas == 8 && dur >= 0.6f && marca.AnilloVisible
                    && Mathf.Abs(radioInicial - 24f) < 3f && Mathf.Abs(radioFinal - 70f) < 3f && escalaInicial > 1.8f && Mathf.Abs(escalaFinal - 1f) < 0.05f;
                if (!n2) ok = false;
                sb.Append($"baja: rayas={rayas} dur={dur:0.00}s anillo {radioInicial:0}->{radioFinal:0} px, escala {escalaInicial:0.0}->{escalaFinal:0.0}{(n2 ? "" : " (MAL)")}; ");
                foreach (var x in CapturarPantalla("v2_065_cursor_baja.png")) yield return x;
                marca.ProgresoForzado = null;
                foreach (var x in Esperar(0.9f)) yield return x;

                // 3) Baja por headshot: dorada, con segundo anillo.
                var victima2 = enemigos[2];
                EventBus.Instance.Publish(new HeadshotEvent(yo.Id, victima2.Id, victima2.transform.position));
                victima2.Health.TakeDamage(victima2.Health.Current + 999, yo.Id);
                yield return null;
                bool hs = marca.EsBaja && marca.EsHeadshot;
                marca.ProgresoForzado = 0.4f;
                yield return null; yield return null;
                bool doble = marca.AnilloDobleVisible;
                bool n3 = hs && doble && marca.RayasActivas == 8;
                if (!n3) ok = false;
                sb.Append($"baja con headshot: dorada={hs} anilloDoble={doble}{(n3 ? "" : " (MAL)")}; ");
                foreach (var x in CapturarPantalla("v2_065_cursor_baja_headshot.png")) yield return x;
                marca.ProgresoForzado = null;
            }
            finally { if (marca != null) marca.ProgresoForzado = null; }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #096 Q con margen de 1 s + diana
        static IEnumerator Bug096()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.2f)) yield return x;
            var drv = PlayerInputDriver.Activo; var yo = Poseido();
            if (drv == null || yo == null) { Fin("FALLO sin driver o soldado poseido"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var enemigos = new List<Soldier>();
            foreach (var a in ActorRegistry.All)
            {
                var s = a as Soldier;
                if (s != null && s.Team == TeamId.Enemy && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy) enemigos.Add(s);
            }
            enemigos.Sort((p, q) => (p.transform.position - yo.transform.position).sqrMagnitude.CompareTo((q.transform.position - yo.transform.position).sqrMagnitude));
            if (enemigos.Count < 6) { ModoDios.Poner(diosAntes); Fin("FALLO hacen falta 6 enemigos vivos"); yield break; }
            try
            {
                var terreno = new AimResult { Type = AimTargetType.Ground, Point = yo.transform.position + yo.transform.forward * 6f };
                var e = enemigos[0];

                // A) Enemigo apuntado hace 0,6 s, mira en terreno: Q ataca a ESE enemigo y aparece la diana.
                drv.MiraForzada = terreno;
                yield return null; yield return null;
                drv.RecordarEnemigoApuntadoHace(e, 0.6f);
                drv.ResolverGestoDeQ(true, false, false);
                string accionA = drv.UltimaAccionRapida;
                for (int i = 0; i < 3; i++) yield return null;
                var diana = DianaDeObjetivo.SobreElEnemigo(e);
                bool atacan = DianaDeObjetivo.AlguienLoAtaca(e);
                bool okA = accionA == "ATACAR" && diana != null && atacan;
                if (!okA) ok = false;
                sb.Append($"a 0.6 s: Q='{accionA}', diana={(diana != null)}, aliados atacandolo={atacan}{(okA ? "" : " (MAL)")}; ");

                // La diana gira (anillos en sentidos opuestos) y queda a +2.3 m sobre el pivote.
                if (diana != null)
                {
                    var re = diana.transform.Find("Exterior"); var ri = diana.transform.Find("Interior");
                    float ext0 = re != null ? re.localEulerAngles.z : 0f, int0 = ri != null ? ri.localEulerAngles.z : 0f;
                    foreach (var x in Esperar(0.3f)) yield return x;
                    float ext1 = re != null ? re.localEulerAngles.z : 0f, int1 = ri != null ? ri.localEulerAngles.z : 0f;
                    float dExt = Mathf.DeltaAngle(ext0, ext1), dInt = Mathf.DeltaAngle(int0, int1);
                    bool gira = dExt > 5f && dInt < -5f;
                    float alto = diana.transform.position.y - e.transform.position.y;
                    bool arriba = Mathf.Abs(alto - DianaDeObjetivo.AlturaSobreElPivote) < 0.1f;
                    if (!gira || !arriba) ok = false;
                    sb.Append($"diana: giro exterior {dExt:0} / interior {dInt:0} grados en 0.3 s{(gira ? "" : " (MAL)")}, altura +{alto:0.00} m{(arriba ? "" : " (MAL)")}; ");
                }

                // B) El Q siguiente a menos de 1 s es el doble toque (SIGANME): manda la mira real aunque el margen siga vigente.
                drv.RecordarEnemigoApuntadoHace(e, 0.3f);
                drv.ResolverGestoDeQ(true, false, false);
                string accionB = drv.UltimaAccionRapida;
                bool okB = accionB == "SEGUIR TODOS";
                if (!okB) ok = false;
                sb.Append($"doble toque dentro de 1 s: Q='{accionB}'{(okB ? "" : " (MAL, esperaba SEGUIR TODOS)")}; ");

                // C) Con 1,3 s de antiguedad el margen vencio: SEGUIR.
                foreach (var x in Esperar(1.2f)) yield return x;
                drv.MiraForzada = terreno;
                drv.RecordarEnemigoApuntadoHace(e, 1.3f);
                drv.ResolverGestoDeQ(true, false, false);
                string accionC = drv.UltimaAccionRapida;
                bool okC = accionC == "SEGUIR TODOS";
                if (!okC) ok = false;
                sb.Append($"a 1.3 s: Q='{accionC}'{(okC ? "" : " (MAL, esperaba SEGUIR TODOS)")}; ");

                // D) El margen no pisa a un aliado: apuntando a un aliado vivo, Q posee (no ataca).
                Soldier aliado = null;
                foreach (var s2 in ActorRegistry.All) { var s = s2 as Soldier; if (s != null && s != yo && s.Team == TeamId.Player && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy && s.Role != RoleType.Civilian) { aliado = s; break; } }
                if (aliado != null)
                {
                    foreach (var x in Esperar(1.1f)) yield return x;
                    drv.MiraForzada = new AimResult { Type = AimTargetType.Ally, Soldier = aliado, Point = aliado.transform.position, HitTransform = aliado.transform };
                    yield return null; yield return null;   // el driver copia la mira forzada a ultimoResultadoDeMira una vez por cuadro
                    drv.RecordarEnemigoApuntadoHace(e, 0.3f);
                    drv.ResolverGestoDeQ(true, false, false);
                    string accionD = drv.UltimaAccionRapida;
                    bool okD = accionD != "ATACAR";
                    if (!okD) ok = false;
                    sb.Append($"apuntando a un aliado con margen vigente: Q='{accionD}'{(okD ? "" : " (MAL, no debe atacar)")}; ");
                    if (Poseido() != yo) drv.Brain.Possess(yo);   // volver al soldado original si Q lo cambio
                }

                // E) Pool de 4: ordenar atacar a 6 enemigos distintos nunca deja mas de 4 dianas.
                drv.MiraForzada = terreno;
                var dest = drv.DestinatariosDeOrden();
                for (int i = 0; i < 6; i++) { OrderService.IssueAttackOrderForSelection(dest, enemigos[i]); yield return null; }
                int activas = DianaDeObjetivo.Activas;
                bool cupo = activas <= DianaDeObjetivo.Cupo && activas >= 1;
                if (!cupo) ok = false;
                sb.Append($"6 ordenes seguidas: {activas} dianas activas (maximo {DianaDeObjetivo.Cupo}){(cupo ? "" : " (MAL)")}; ");

                // F) Muere el enemigo: la diana se apaga.
                var victima = enemigos[5];
                bool habia = DianaDeObjetivo.SobreElEnemigo(victima) != null;
                victima.Health.TakeDamage(victima.Health.Current + 999, yo.Id);
                for (int i = 0; i < 4; i++) yield return null;
                bool seApago = habia && DianaDeObjetivo.SobreElEnemigo(victima) == null;
                if (!seApago) ok = false;
                sb.Append($"al morir el enemigo la diana se apaga: habia={habia} apagada={seApago}{(seApago ? "" : " (MAL)")}; ");

                // Captura: jugador a 9 m de un enemigo, orden de ataque por Q con margen, diana a la vista.
                Soldier objetivo = null; Vector3 pos = default, dir = default;
                foreach (var c2 in enemigos)
                {
                    if (!c2.Health.IsAlive || c2 == victima) continue;
                    if (UbicarJugador(yo, c2, 9f, out pos, out dir)) { objetivo = c2; break; }
                }
                if (objetivo != null)
                {
                    yo.transform.position = pos;
                    yo.transform.rotation = Quaternion.LookRotation(-dir);
                    var rig = SP.CameraSystem.CameraRig.Instance;
                    if (rig != null) rig.AddPitch(-rig.Pitch);
                    drv.MiraForzada = terreno;
                    foreach (var x in Esperar(1.1f)) yield return x;
                    drv.RecordarEnemigoApuntadoHace(objetivo, 0.6f);
                    drv.ResolverGestoDeQ(true, false, false);
                    foreach (var x in Esperar(0.5f)) yield return x;
                    foreach (var x in CapturarPantalla("v2_096_diana.png")) yield return x;
                }
                else sb.Append("sin enemigo con linea de tiro para la captura; ");
            }
            finally { drv.MiraForzada = null; ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
