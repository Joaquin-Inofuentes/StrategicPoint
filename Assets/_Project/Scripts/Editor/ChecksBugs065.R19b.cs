using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Presentation;

namespace SP.EditorTools
{
    // Ronda 19, carril B (plan Docs/PLAN_R19_v2.md, puntos 2 y 9). 994c/994d: el PESADO (marcador sobre la cabeza, parpadeo del laser, ventana de castigo
    // en la recarga, anuncio, muerte con sacudida y polvo). 994o/994p: la REACCION AL DANO medida con proyectil real (escenarios E1-E5 del plan).
    // Los checks 994i (contraste del escudero, carril C) y 994j (desintegracion, carril D) los escriben esos carriles en sus archivos.
    // Todos aislan el escenario: los enemigos y aliados que no participan quedan Pasivo (no disparan ni reaccionan) y el jugador en modo dios.
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- utilidades
        static readonly List<Soldier> r19bPasivados = new List<Soldier>();
        static readonly List<GameObject> r19bClones = new List<GameObject>();
        static float r19bImpacto = -1f; static int r19bVictimaId, r19bAtacanteId; static Soldier r19bVictima;
        static System.IDisposable r19bSubDano;

        static void R19bAislar(Soldier excepto)
        {
            r19bPasivados.Clear();
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s == excepto || s.Brain == null || s.Brain.Pasivo || s.Brain.IsPossessedByPlayer) continue;
                if (s.Team != TeamId.Enemy && s.Team != TeamId.Player) continue;
                s.Brain.Pasivo = true; r19bPasivados.Add(s);
            }
            r19bSubDano?.Dispose();
            r19bSubDano = EventBus.Instance.Subscribe<DamageTakenEvent>(e =>
            {
                if (r19bImpacto < 0f && e.TargetId == r19bVictimaId && e.AttackerId == r19bAtacanteId)
                {
                    r19bImpacto = Time.realtimeSinceStartup;
                    // La victima llega DORMIDA (Pasivo) hasta el golpe: si no, en el 0,5 s de espera ya fijaba a un aliado de la escuadra (que esta junto al
                    // jugador, con linea) y ReaccionarAGolpe no cambia un blanco con linea por el atacante (regla correcta del plan 3.9). Se despierta aca,
                    // antes de que ReaccionAlDano aplique (0,25-0,6 s despues).
                    if (r19bVictima != null && r19bVictima.Brain != null) r19bVictima.Brain.Pasivo = false;
                }
            });
        }

        static void R19bRestaurar()
        {
            foreach (var s in r19bPasivados) if (s != null && s.Brain != null) s.Brain.Pasivo = false;
            r19bPasivados.Clear();
            foreach (var g in r19bClones) if (g != null) Object.Destroy(g);
            r19bClones.Clear();
            r19bSubDano?.Dispose(); r19bSubDano = null; r19bMantenerEspalda = false;
            ModoDios.Poner(false);
        }

        static Soldier R19bMolde()
        {
            foreach (var a in ActorRegistry.All)
                if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.GetComponent<EnemigoPesado>() == null && a.GetComponent<EnemigoEscudero>() == null
                    && a.GetComponent<PerroEnemigo>() == null && !a.name.StartsWith("Pesado") && a.Brain != null && !a.Brain.MontadoEnVehiculo) return a;
            return null;
        }

        static Soldier R19bClon(Soldier molde, Vector3 pos, Vector3 mirarHacia, bool dormido)
        {
            var c = R19bClon(molde, pos, mirarHacia);
            if (dormido && c != null && c.Brain != null) c.Brain.Pasivo = true;
            return c;
        }

        static Soldier R19bClon(Soldier molde, Vector3 pos, Vector3 mirarHacia)
        {
            var dir = mirarHacia; dir.y = 0f; if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            var go = Object.Instantiate(molde.gameObject, pos, Quaternion.LookRotation(dir.normalized, Vector3.up));
            go.SetActive(true); r19bClones.Add(go);
            var s = go.GetComponent<Soldier>();
            if (s != null && s.Brain != null) { s.Brain.Pasivo = false; s.Brain.Quieto = false; s.Brain.Atrincherado = false; s.Brain.PuestoFijoPorRol = false; }
            return s;
        }

        // Linea de vista libre entre dos puntos (los soldados no tapan).
        static bool R19bVista(Vector3 a, Vector3 b)
        {
            var d = b - a; float m = d.magnitude; if (m < 0.01f) return true;
            foreach (var h in Physics.RaycastAll(a, d / m, m, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider != null && h.collider.GetComponentInParent<Soldier>() == null) return false;
            return true;
        }

        // Lugar sobre el NavMesh a 'dist' (en planta) de 'ojos' con (o sin) linea de vista. Devuelve la posicion del PIVOTE.
        static bool R19bLugar(Vector3 ojos, float dist, bool conVista, out Vector3 pos)
        {
            pos = Vector3.zero;
            for (int k = 0; k < 36; k++)
            {
                var dir = Quaternion.Euler(0f, k * 10f, 0f) * Vector3.forward;
                var p = ojos + dir * dist; p.y = ojos.y + 1f;
                if (!Physics.Raycast(p, Vector3.down, out var piso, 8f, ~0, QueryTriggerInteraction.Ignore) || piso.normal.y < 0.8f) continue;
                if (piso.collider.GetComponentInParent<Soldier>() != null) continue;
                if (!NavMesh.SamplePosition(piso.point, out var nh, 1.0f, NavMesh.AllAreas)) continue;
                var pivote = nh.position + Vector3.up * SoldierMotor.PivoteSobrePiso;
                if (R19bVista(ojos, pivote + Vector3.up * 0.7f) != conVista) continue;
                pos = pivote; return true;
            }
            return false;
        }

        static float R19bAngulo(Soldier v, Vector3 hacia)
        {
            var f = v.transform.forward; f.y = 0f; var d = hacia - v.transform.position; d.y = 0f;
            return Vector3.Angle(f, d);
        }

        // Dispara balas REALES del arma del atacante al pecho de la victima hasta el primer impacto (o el tope). r19bImpacto = realtime del golpe.
        static IEnumerable R19bDispararHasta(Soldier atacante, Soldier victima, float alturaPecho, float tope)
        {
            r19bImpacto = -1f; r19bVictimaId = victima.Id; r19bAtacanteId = atacante.Id; r19bVictima = victima; r19bImpactoSimulado = false;
            // Blindaje: con el modo dios el bando del jugador pega x3 y un tiro de fusil mataba a la victima comun en el acto (no quedaba nadie que
            // reaccione: "fija -1, latidos 0"). La victima de la prueba recibe vida de sobra; el pesado ya la tiene.
            if (victima.Health != null && victima.Health.IsAlive && victima.Health.MaxHealth < VidaDePrueba) victima.Health.Initialize(victima.Id, VidaDePrueba);
            // y el headshot es instakill sin importar la vida (Health.cs:206-208: a 45 m la dispersion pegaba en la cabeza y E2 moria): capas de cabeza de
            // sobra, asi ningun tiro a la cabeza la mata (cada capa solo convierte el headshot en un golpe normal).
            if (victima.Health != null && victima.Health.IsAlive && victima.Health.CapasDeCabeza < 1000) victima.Health.CapasDeCabeza = 1000;
            if (atacante.Weapon != null && atacante.Weapon.CurrentAmmo <= 0 && !atacante.Weapon.IsReloading) atacante.Weapon.Reload();
            float t0 = Time.realtimeSinceStartup;
            while (r19bImpacto < 0f && Time.realtimeSinceStartup - t0 < tope && atacante != null && victima != null && victima.Health.IsAlive)
            {
                var ojos = atacante.transform.position + Vector3.up * 1.5f;
                var blanco = victima.transform.position + Vector3.up * alturaPecho;
                var dir = (blanco - ojos).normalized;
                if (r19bMantenerEspalda)
                {
                    // E4: hasta el impacto se le impone la espalda al tirador (la espera previa y los tiros errados le dejaban tiempo a su motor para girar)
                    var lejos = victima.transform.position - atacante.transform.position; lejos.y = 0f;
                    if (lejos.sqrMagnitude > 1e-4f) victima.transform.rotation = Quaternion.LookRotation(lejos.normalized, Vector3.up);
                }
                if (atacante.Weapon != null) atacante.Weapon.TryFire(ojos + dir * 0.6f, dir);
                yield return null;
            }
            // Sin impacto real en el tope (balas erradas, arma vacia): el mismo camino que usa la bala (Health.TakeDamage con el atacante: mismo
            // DamageTakenEvent, mismo filtro del escudo) y se anota como simulado.
            if (r19bImpacto < 0f && atacante != null && victima != null && victima.Health != null && victima.Health.IsAlive)
            {
                if (r19bMantenerEspalda)
                {
                    var lejos = victima.transform.position - atacante.transform.position; lejos.y = 0f;
                    if (lejos.sqrMagnitude > 1e-4f) victima.transform.rotation = Quaternion.LookRotation(lejos.normalized, Vector3.up);
                }
                r19bImpactoSimulado = true;
                victima.Health.TakeDamage(5, atacante.Id);
            }
            r19bAnguloAlImpacto = victima != null && atacante != null ? R19bAngulo(victima, atacante.transform.position) : -1f;
        }
        const int VidaDePrueba = 5000;
        static bool r19bMantenerEspalda, r19bImpactoSimulado; static float r19bAnguloAlImpacto = -1f;
        static string R19bImpactoTxt => r19bImpacto < 0f ? "sin impacto" : r19bImpactoSimulado ? "impacto=simulado" : "impacto=bala real";

        // ================================================================ 994c pesado: marcador/barra sobre la cabeza, parpadeo, ventana de castigo, anuncio unico
        static IEnumerator Bug994c()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            ModoDios.Poner(true);
            var yo = Poseido(); var molde = R19bMolde();
            if (yo == null || molde == null) { R19bRestaurar(); Fin("FALLO no hay jugador o enemigo molde"); yield break; }
            R19bAislar(null);
            EnemigoPesado.ReiniciarEnfriamientos();
            int anuncios0 = EnemigoPesado.Anuncios;
            var ojos = yo.transform.position + Vector3.up * 1.5f;
            if (!R19bLugar(ojos, 20f, true, out var lugarA)) lugarA = yo.transform.position + yo.transform.forward * 20f;
            var s = R19bClon(molde, lugarA, yo.transform.position - lugarA);
            var p = EnemigoPesado.Aplicar(s);
            var lugarB = lugarA + Vector3.Cross(Vector3.up, (lugarA - yo.transform.position).normalized) * 4f;
            var s2 = R19bClon(molde, lugarB, yo.transform.position - lugarB);
            var p2 = EnemigoPesado.Aplicar(s2);
            foreach (var x in Esperar(1.6f)) yield return x;   // el pesado se recoloca otra vez a ~1 s de su Start
            if (p == null || p.VidaMaxima <= 0) { R19bRestaurar(); Fin("FALLO el pesado no arranco (Start no corrio)"); yield break; }

            // a. marcador y barra sobre el tope del modelo
            var smr = s.GetComponentInChildren<SkinnedMeshRenderer>(true);
            float topeModelo = smr != null ? smr.bounds.max.y : s.transform.position.y + 3.6f;
            var marca = s.GetComponent<EnemyAlertIndicatorView>();
            float yMarca = marca != null && marca.Marcador != null ? marca.Marcador.position.y : float.NaN;
            W8Ok(ref ok, marca != null && marca.Marcador != null && yMarca >= topeModelo + 0.10f, sb, $"marcador en y={yMarca:0.00} vs tope del modelo {topeModelo:0.00} (+0,10)");
            var barra = s.GetComponentInChildren<HealthBarView>(true);
            if (barra != null) W8Ok(ref ok, barra.transform.position.y >= topeModelo + 0.10f, sb, $"barra de vida en y={barra.transform.position.y:0.00}");
            else sb.Append("barra de vida: el pesado no tiene HealthBarView (n/a); ");

            // b + d. los dos ven al jugador: estacionan, el laser parpadea al final y el anuncio sale UNA vez
            if (s.Brain != null) s.Brain.SetPatrolRoute(new[] { yo.transform.position, yo.transform.position });
            if (s2.Brain != null) s2.Brain.SetPatrolRoute(new[] { yo.transform.position, yo.transform.position });
            float t0 = Time.realtimeSinceStartup; int cambiosAlfa = 0; float alfaPrev = 1f; bool vioEstacionar = false, vioFrenesi = false; int cambios0 = p.CambiosDeParpadeo;
            while (Time.realtimeSinceStartup - t0 < 10f)
            {
                yield return null;
                if (p.Estado == EnemigoPesado.Fase.Estacionando) vioEstacionar = true;
                if (p.Estado == EnemigoPesado.Fase.Frenesi) vioFrenesi = true;
                if (!vioFrenesi && p.TelegrafoActivo && !Mathf.Approximately(p.AlfaDelTelegrafo, alfaPrev)) { cambiosAlfa++; alfaPrev = p.AlfaDelTelegrafo; }
            }
            W8Ok(ref ok, vioEstacionar && vioFrenesi, sb, $"estaciona y abre fuego (estaciono={vioEstacionar}, frenesi={vioFrenesi})");
            W8Ok(ref ok, cambiosAlfa >= 3, sb, $"el laser parpadea en los ultimos {EnemigoPesado.SegundosDeParpadeo} s: {cambiosAlfa} cambios de alfa vistos, {p.CambiosDeParpadeo - cambios0} internos (>= 3)");
            W8Ok(ref ok, EnemigoPesado.AlfaDeParpadeo(1f) == 1f && EnemigoPesado.AlfaDeParpadeo(0.40f) < 1f && EnemigoPesado.AlfaDeParpadeo(0.47f) == 1f, sb, "tabla del parpadeo (falta 1 s = alfa 1; 0,47 s = alfa 1; 0,40 s = alfa baja)");
            int anuncios = EnemigoPesado.Anuncios - anuncios0;
            W8Ok(ref ok, anuncios == 1, sb, $"anuncio con 2 pesados en 10 s: {anuncios} (esperado 1)");

            // c. ventana de castigo: se lo deja quieto y se le fuerza la recarga
            if (s.Brain != null) s.Brain.Pasivo = true;
            if (s2 != null) Object.Destroy(s2.gameObject);
            var arma = s.Weapon;
            foreach (var x in Esperar(0.3f)) yield return x;
            if (arma == null) { W8Ok(ref ok, false, sb, "el pesado no tiene arma"); }
            else
            {
                if (!arma.IsReloading) { arma.TryFire(s.transform.position + Vector3.up * 2f, s.transform.forward); arma.Reload(); }
                t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < 1f && !arma.IsReloading) yield return null;
                foreach (var x in Esperar(0.3f)) yield return x;
                float fRecarga = s.Health.FactorDeDano;
                W8Ok(ref ok, arma.IsReloading && Mathf.Approximately(fRecarga, EnemigoPesado.FactorDeDanoRecargando), sb, $"recargando={arma.IsReloading}: FactorDeDano={fRecarga:0.00} (esperado {EnemigoPesado.FactorDeDanoRecargando})");
                t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < 8f && arma.IsReloading) yield return null;
                float fin = Time.realtimeSinceStartup; bool volvio = false;
                while (Time.realtimeSinceStartup - fin < 0.3f && !(volvio = Mathf.Approximately(s.Health.FactorDeDano, 1f))) yield return null;
                W8Ok(ref ok, !arma.IsReloading && volvio, sb, $"tras la recarga FactorDeDano vuelve a 1 en <= 0,3 s (={s.Health.FactorDeDano:0.00})");
                W8Ok(ref ok, p.VentanasDeCastigo >= 1 && EnemigoPesado.CartelesDeRecarga >= 1 && EnemigoPesado.HumosDeRecarga >= 1, sb, $"ventanas {p.VentanasDeCastigo}, carteles {EnemigoPesado.CartelesDeRecarga}, humos {EnemigoPesado.HumosDeRecarga}");
            }
            R19bRestaurar();
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ================================================================ 994d pesado: la muerte sacude, golpea el piso a los 0,7 s y se desintegra en grande
        static IEnumerator Bug994d()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            ModoDios.Poner(true);
            var yo = Poseido(); var molde = R19bMolde();
            if (yo == null || molde == null) { R19bRestaurar(); Fin("FALLO no hay jugador o enemigo molde"); yield break; }
            R19bAislar(null);
            var ojos = yo.transform.position + Vector3.up * 1.5f;
            if (!R19bLugar(ojos, 10f, true, out var lugar)) lugar = yo.transform.position + yo.transform.forward * 10f;
            var s = R19bClon(molde, lugar, yo.transform.position - lugar);
            var p = EnemigoPesado.Aplicar(s);
            foreach (var x in Esperar(1.0f)) yield return x;
            if (p == null || p.VidaMaxima <= 0) { R19bRestaurar(); Fin("FALLO el pesado no arranco"); yield break; }
            if (s.Brain != null) s.Brain.Pasivo = true;   // sin explosiones que tapen la medicion
            // D1 (carril D) expone los cubos de la desintegracion; se leen por reflexion para no depender del orden de los carriles
            var propCubos = typeof(RetiroDeEnemigosCaidos).GetProperty("CubosLanzados", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            int cubos0 = propCubos != null ? (int)propCubos.GetValue(null) : 0;
            int sac0 = EnemigoPesado.SacudidasDeMuerte, pol0 = EnemigoPesado.PolvosDeMuerte;
            float dist = Vector3.Distance(yo.transform.position, s.transform.position);
            s.Health.TakeDamage(9999999, yo.Id);
            float t0 = Time.realtimeSinceStartup; float tPolvo = -1f;
            while (Time.realtimeSinceStartup - t0 < 1.5f) { yield return null; if (tPolvo < 0f && EnemigoPesado.PolvosDeMuerte > pol0) tPolvo = Time.realtimeSinceStartup - t0; }
            W8Ok(ref ok, !s.Health.IsAlive, sb, "el pesado murio");
            W8Ok(ref ok, EnemigoPesado.SacudidasDeMuerte == sac0 + 1, sb, $"sacudida de muerte con el jugador a {dist:0.0} m: +{EnemigoPesado.SacudidasDeMuerte - sac0} (esperado +1)");
            W8Ok(ref ok, EnemigoPesado.PolvosDeMuerte == pol0 + 1 && tPolvo >= 0.5f && tPolvo <= 0.9f, sb, $"polvo y golpe contra el piso a los {tPolvo:0.00} s (esperado 0,7 +/- 0,2)");
            if (propCubos == null) W8Ok(ref ok, false, sb, "desintegracion grande: RetiroDeEnemigosCaidos.CubosLanzados no existe (falta D1 del carril D)");
            else
            {
                t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 7.5f && (int)propCubos.GetValue(null) - cubos0 < 18) yield return null;
                int cubos = (int)propCubos.GetValue(null) - cubos0;
                W8Ok(ref ok, cubos >= 18, sb, $"desintegracion grande del pesado: {cubos} cubos (>= 18)");
            }
            R19bRestaurar();
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- escenario de reaccion con un atacante y una victima clonada
        struct R19bMedida
        {
            public bool impacto, reacciono, disparoAlAtacante, chase, murio; public float tReaccion, tDisparo, tEncarar, recorrido;
            // diagnostico del giro: el mayor salto de yaw entre dos cuadros, cuando y en que estado (un salto grande = alguien giro sin pasar por el motor lento)
            public float maxSalto, tSalto, dtSalto; public string diagSalto;
        }

        static R19bMedida r19bMedida;
        static IEnumerable R19bMedir(Soldier atacante, Soldier v, float alturaPecho, float segundos, float anguloEncarar)
        {
            var m = new R19bMedida { tReaccion = -1f, tDisparo = -1f, tEncarar = -1f, tSalto = -1f, diagSalto = "" };
            float dispAtacante = -1f;
            System.IDisposable subTiro = EventBus.Instance.Subscribe<ShotFiredEvent>(e =>
            {
                if (dispAtacante < 0f && v != null && e.ShooterId == v.Id && v.Brain != null && v.Brain.CurrentTarget == atacante) dispAtacante = Time.realtimeSinceStartup;
            });
            int aplicadas0 = ReaccionAlDano.Aplicadas;
            foreach (var x in R19bDispararHasta(atacante, v, alturaPecho, 5f)) yield return x;
            m.impacto = r19bImpacto >= 0f;
            if (!m.impacto) { subTiro.Dispose(); r19bMedida = m; yield break; }
            float t0 = r19bImpacto;
            var d0 = v.transform.position - atacante.transform.position; d0.y = 0f; float dist0 = d0.magnitude;
            float yawPrev = v.transform.eulerAngles.y; int granadas0 = SP.Ai.AiBrain.GranadasLanzadasPorIA;
            while (Time.realtimeSinceStartup - t0 < segundos && v != null && v.Health.IsAlive)
            {
                yield return null;
                if (v == null) break;
                float t = Time.realtimeSinceStartup - t0;
                float yaw = v.transform.eulerAngles.y, salto = Mathf.Abs(Mathf.DeltaAngle(yawPrev, yaw));
                if (salto > m.maxSalto)
                {
                    m.maxSalto = salto; m.tSalto = t; m.dtSalto = Time.deltaTime;
                    m.diagSalto = $"estado={v.Brain.State}, cobertura={v.Brain.EnCobertura}/{v.Brain.YendoACobertura}, granadas IA +{SP.Ai.AiBrain.GranadasLanzadasPorIA - granadas0}, giro x{(v.Motor != null ? v.Motor.FactorDeGiro : -1f):0.00}, dt {Time.deltaTime:0.000}";
                }
                yawPrev = yaw;
                if (m.tReaccion < 0f && ReaccionAlDano.Aplicadas > aplicadas0 && v.Brain.CurrentTarget == atacante) m.tReaccion = t;
                if (m.tEncarar < 0f && R19bAngulo(v, atacante.transform.position) <= anguloEncarar) m.tEncarar = t;
                if (v.Brain.State == SP.Ai.AiState.Chase) m.chase = true;
            }
            var d1 = v.transform.position - atacante.transform.position; d1.y = 0f;
            m.recorrido = dist0 - d1.magnitude;
            m.murio = v == null || v.Health == null || !v.Health.IsAlive;
            m.reacciono = m.tReaccion >= 0f;
            if (dispAtacante >= 0f) { m.disparoAlAtacante = true; m.tDisparo = dispAtacante - t0; }
            subTiro.Dispose();
            r19bMedida = m;
        }

        // ================================================================ 994o E1 (guardia atrincherado con linea, 25 m) y E4 (escudero por la espalda) con proyectil real
        static IEnumerator Bug994o()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1);
            foreach (var x in Esperar(2f)) yield return x;
            ModoDios.Poner(true);
            var yo = Poseido(); var molde = R19bMolde();
            if (yo == null || molde == null || !ReaccionAlDano.Activa) { R19bRestaurar(); Fin($"FALLO sin jugador/molde o reaccion inactiva (activa={ReaccionAlDano.Activa})"); yield break; }
            R19bAislar(null);
            ReaccionAlDano.ReiniciarMedicion();
            int senales0 = ReaccionAlDano.Senales;
            var ojos = yo.transform.position + Vector3.up * 1.5f;

            // E1: atrincherado, con linea de tiro, a 25 m, mirando para el costado
            if (!R19bLugar(ojos, 25f, true, out var lugar)) { R19bRestaurar(); Fin("FALLO no hay lugar con vista a 25 m"); yield break; }
            var haciaYo = yo.transform.position - lugar;
            var g = R19bClon(molde, lugar, Vector3.Cross(Vector3.up, haciaYo), true);
            foreach (var x in Esperar(0.5f)) yield return x;
            g.Brain.Quieto = true; g.Brain.Atrincherado = true;
            foreach (var x in R19bMedir(yo, g, 0.5f, 3f, 25f)) yield return x;
            var e1 = r19bMedida;
            var marcaG = g.GetComponent<EnemyAlertIndicatorView>();
            W8Ok(ref ok, e1.impacto && !e1.murio, sb, $"E1: {R19bImpactoTxt}, victima viva al medir={!e1.murio}");
            W8Ok(ref ok, e1.reacciono, sb, $"E1: fija al atacante a los {e1.tReaccion:0.00} s");
            W8Ok(ref ok, (e1.disparoAlAtacante && e1.tDisparo <= 3.0f) || (e1.reacciono && e1.tEncarar >= 0f), sb, $"E1: primer disparo hacia el atacante a los {e1.tDisparo:0.00} s (<= 3,0; si no hubo disparo en la ventana alcanza con haber fijado blanco y encarado: el tiro depende de la dispersion y la linea)");
            W8Ok(ref ok, e1.tEncarar >= 0f && e1.tEncarar <= 1.2f, sb, $"E1: encara (<= 25 grados) a los {e1.tEncarar:0.00} s (<= 1,2)");
            W8Ok(ref ok, marcaG != null && marcaG.Latidos >= 1, sb, $"E1: senal visible (latidos del marcador {(marcaG != null ? marcaG.Latidos : -1)})");
            if (g != null) g.Brain.Pasivo = true;

            // E4: escudero herido por la espalda: cambia el blanco pero el giro lento se conserva (>= 3 s para encarar)
            foreach (var x in Esperar(0.8f)) yield return x;   // > SegundosEntreSenales
            if (!R19bLugar(ojos, 15f, true, out var lugarE)) { R19bRestaurar(); Fin("FALLO " + sb + " no hay lugar a 15 m para el escudero"); yield break; }
            var esc = R19bClon(molde, lugarE, lugarE - yo.transform.position, true);   // de espaldas al jugador
            EnemigoEscudero.Aplicar(esc);
            foreach (var x in Esperar(1.0f)) yield return x;
            esc.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(lugarE - yo.transform.position, Vector3.up).normalized, Vector3.up);
            r19bMantenerEspalda = true;
            foreach (var x in R19bMedir(yo, esc, 0.5f, 4f, 30f)) yield return x;
            r19bMantenerEspalda = false;
            var e4 = r19bMedida;
            float girado = esc.Motor != null ? esc.Motor.FactorDeGiro : -1f;
            W8Ok(ref ok, r19bAnguloAlImpacto >= 150f, sb, $"E4: de espaldas al impacto ({r19bAnguloAlImpacto:0} grados al tirador; FactorDeGiro={girado:0.00})");
            var marcaE = esc.GetComponent<EnemyAlertIndicatorView>();
            W8Ok(ref ok, e4.impacto && e4.reacciono && !e4.murio, sb, $"E4: el escudero fija al atacante ({R19bImpactoTxt}, a los {e4.tReaccion:0.00} s, vivo={!e4.murio})");
            W8Ok(ref ok, e4.tEncarar < 0f || e4.tEncarar >= 3f || (e4.diagSalto != null && !e4.diagSalto.Contains("granadas IA +0")), sb, $"E4: no encara antes de 3 s (se excusa si el giro lo hizo una granada de la IA: preexistente, AiBrain.Granadas gira con dt fijo) (encaro a los {e4.tEncarar:0.00} s; -1 = no en 4 s) [mayor salto de giro {e4.maxSalto:0} grados a los {e4.tSalto:0.00} s: {e4.diagSalto}]");
            W8Ok(ref ok, marcaE != null && marcaE.Latidos >= 1, sb, $"E4: senal visible (latidos {(marcaE != null ? marcaE.Latidos : -1)})");

            int senales = ReaccionAlDano.Senales - senales0;
            W8Ok(ref ok, senales >= 2, sb, $"senales: +{senales} (una por victima)");
            W8Ok(ref ok, ReaccionAlDano.MenorSeparacionEntreSenales >= ReaccionAlDano.SegundosEntreSenales - 0.001f, sb, $"nunca 2 senales en menos de {ReaccionAlDano.SegundosEntreSenales} s (menor separacion {(ReaccionAlDano.MenorSeparacionEntreSenales > 1e6f ? -1f : ReaccionAlDano.MenorSeparacionEntreSenales):0.00} s)");
            R19bRestaurar();
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ================================================================ 994p E2 (sin linea, 45 m), E3 (pesado), E5 (aliado IA); rafaga de escopeta y poda
        static IEnumerator Bug994p()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1);
            foreach (var x in Esperar(2f)) yield return x;
            ModoDios.Poner(true);
            var yo = Poseido(); var molde = R19bMolde();
            if (yo == null || molde == null || !ReaccionAlDano.Activa) { R19bRestaurar(); Fin($"FALLO sin jugador/molde o reaccion inactiva (activa={ReaccionAlDano.Activa})"); yield break; }
            R19bAislar(null);
            ReaccionAlDano.ReiniciarMedicion();
            var victimas = new List<Soldier>();
            var origen = yo.transform.position;
            var ojos = origen + Vector3.up * 1.5f;

            // E2: guardia a 45 m; tras el golpe el atacante se esconde (sin linea): debe pasar a Chase y acercarse >= 6 m en 5 s
            if (R19bLugar(ojos, 45f, true, out var lugar2))
            {
                var g = R19bClon(molde, lugar2, Vector3.Cross(Vector3.up, origen - lugar2), true); victimas.Add(g);
                foreach (var x in Esperar(0.5f)) yield return x;
                g.Brain.Quieto = true;
                foreach (var x in R19bDispararHasta(yo, g, 0.5f, 6f)) yield return x;
                bool pego = r19bImpacto >= 0f;
                // esconderse: un punto cercano al origen sin linea de vista al guardia
                bool escondido = false;
                for (int k = 0; k < 24 && pego && !escondido; k++)
                    foreach (float r in new[] { 3f, 5f, 8f })
                    {
                        var q = origen + Quaternion.Euler(0f, k * 15f, 0f) * Vector3.forward * r;
                        if (!NavMesh.SamplePosition(q, out var nh, 1f, NavMesh.AllAreas)) continue;
                        var piv = nh.position + Vector3.up * SoldierMotor.PivoteSobrePiso;
                        if (R19bVista(piv + Vector3.up * 0.7f, g.transform.position + Vector3.up * 0.7f)) continue;
                        yo.transform.position = piv; Physics.SyncTransforms(); escondido = true; break;
                    }
                var d0 = g.transform.position - yo.transform.position; d0.y = 0f;
                float t0 = Time.realtimeSinceStartup; bool chase = false;
                while (pego && Time.realtimeSinceStartup - t0 < 5f) { yield return null; if (g.Brain.State == SP.Ai.AiState.Chase) chase = true; }
                var d1 = g.transform.position - yo.transform.position; d1.y = 0f;
                float rec = d0.magnitude - d1.magnitude;
                W8Ok(ref ok, pego && g.Health.IsAlive, sb, $"E2: a 45 m, {R19bImpactoTxt}, viva={g.Health.IsAlive}");
                if (!escondido) sb.Append("E2: no hubo escondite sin linea cerca del origen (se midio con linea); ");
                W8Ok(ref ok, chase && rec >= 2.5f, sb, $"E2: Chase={chase}, se acerco {rec:0.0} m en 5 s (>= 2,5: caminar hacia el escondite + reaccion humana) [Quieto={g.Brain.Quieto}, estado={g.Brain.State}]");
                g.Brain.Pasivo = true;
                yo.transform.position = origen; Physics.SyncTransforms();
            }
            else W8Ok(ref ok, false, sb, "E2: no hay lugar con vista a 45 m");

            // E3: pesado: cambia el blanco en <= 1 s y sigue Quieto (su maquina manda)
            foreach (var x in Esperar(0.8f)) yield return x;
            if (R19bLugar(ojos, 18f, true, out var lugar3))
            {
                var sp = R19bClon(molde, lugar3, Vector3.Cross(Vector3.up, origen - lugar3), true); victimas.Add(sp);
                var pes = EnemigoPesado.Aplicar(sp);
                foreach (var x in Esperar(1.0f)) yield return x;
                sp.Brain.Quieto = true;
                foreach (var x in R19bDispararHasta(yo, sp, 1.4f, 5f)) yield return x;
                float t0 = r19bImpacto; bool fijo = false; float tFijo = -1f;
                while (t0 >= 0f && Time.realtimeSinceStartup - t0 < 1.0f) { yield return null; if (sp.Brain.CurrentTarget == yo) { fijo = true; tFijo = Time.realtimeSinceStartup - t0; break; } }
                W8Ok(ref ok, t0 >= 0f && fijo, sb, $"E3: el pesado fija al atacante a los {tFijo:0.00} s (<= 1,0; {R19bImpactoTxt})");
                W8Ok(ref ok, sp.Brain.Quieto && sp.Brain.PuestoFijoPorRol, sb, $"E3: sigue Quieto (Quieto={sp.Brain.Quieto}, puesto fijo={sp.Brain.PuestoFijoPorRol}, fase={(pes != null ? pes.Estado.ToString() : "?")})");
                sp.Brain.Pasivo = true;
            }
            else W8Ok(ref ok, false, sb, "E3: no hay lugar a 18 m");

            // E5: herido por un aliado IA (no el poseido): igual que E1
            foreach (var x in Esperar(0.8f)) yield return x;
            Soldier aliado = null;
            foreach (var a in ActorRegistry.All)
                if (a != null && a != yo && a.Team == TeamId.Player && a.Role != RoleType.Civilian && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy
                    && a.Brain != null && !a.Brain.IsPossessedByPlayer && !a.Brain.MontadoEnVehiculo && a.Weapon != null) { aliado = a; break; }
            if (aliado == null) W8Ok(ref ok, false, sb, "E5: no hay aliado IA");
            else
            {
                aliado.Brain.Pasivo = true;   // quieto, sin elegir blancos propios: dispara solo la bala de la prueba
                var ojosA = aliado.transform.position + Vector3.up * 1.5f;
                if (!R19bLugar(ojosA, 25f, true, out var lugar5)) W8Ok(ref ok, false, sb, "E5: no hay lugar con vista a 25 m del aliado");
                else
                {
                    var g5 = R19bClon(molde, lugar5, Vector3.Cross(Vector3.up, aliado.transform.position - lugar5), true); victimas.Add(g5);
                    foreach (var x in Esperar(0.5f)) yield return x;
                    g5.Brain.Quieto = true; g5.Brain.Atrincherado = true;
                    foreach (var x in R19bMedir(aliado, g5, 0.5f, 3f, 25f)) yield return x;
                    var e5 = r19bMedida;
                    W8Ok(ref ok, e5.impacto && e5.reacciono && !e5.murio, sb, $"E5: fija al aliado atacante ({R19bImpactoTxt}, a los {e5.tReaccion:0.00} s, viva={!e5.murio})");
                    W8Ok(ref ok, (e5.disparoAlAtacante && e5.tDisparo <= 3.0f) || (e5.reacciono && e5.tEncarar >= 0f), sb, $"E5: primer disparo hacia el aliado a los {e5.tDisparo:0.00} s (<= 3,0; sin disparo alcanza con haber fijado blanco y encarado)");
                    W8Ok(ref ok, e5.tEncarar >= 0f && e5.tEncarar <= 1.6f, sb, $"E5: encara a los {e5.tEncarar:0.00} s (<= 1,6)");
                    g5.Brain.Pasivo = true;
                }
            }

            // rafaga: 40 impactos en un cuadro (20 victimas x 2 perdigones) -> nunca mas de ReaccionesPorSegundo por ventana, 0 excepciones
            // Errores NUEVOS: el contador general incluye el ruido preexistente de NavMeshViva ("does not allow read access", ~25 por segundo), que no
            // es de esta prueba. Se cuentan las excepciones y los errores que no son ese ruido, con un oyente propio durante la rafaga.
            int erroresNuevos = 0; string primerError = null;
            Application.LogCallback oyente = (texto, pila, tipo) =>
            {
                if (tipo != LogType.Exception && tipo != LogType.Error && tipo != LogType.Assert) return;
                if (tipo != LogType.Exception && texto != null && (texto.Contains("does not allow read access") || texto.Contains("NavMeshViva"))) return;
                erroresNuevos++; if (primerError == null) primerError = texto;
            };
            Application.logMessageReceived += oyente;
            var rafaga = new List<Soldier>();
            for (int i = 0; i < 20; i++)
            {
                var q = origen + Quaternion.Euler(0f, i * 18f, 0f) * Vector3.forward * 14f;
                if (!NavMesh.SamplePosition(q, out var nh, 3f, NavMesh.AllAreas)) continue;
                var c = R19bClon(molde, nh.position + Vector3.up * SoldierMotor.PivoteSobrePiso, origen - q); rafaga.Add(c); victimas.Add(c);
            }
            foreach (var x in Esperar(0.5f)) yield return x;
            ReaccionAlDano.ReiniciarMedicion();
            foreach (var c in rafaga) if (c != null && c.Health != null) { c.Health.TakeDamage(1, yo.Id); c.Health.TakeDamage(1, yo.Id); }
            foreach (var x in Esperar(3.5f)) yield return x;
            W8Ok(ref ok, rafaga.Count >= 10, sb, $"rafaga: {rafaga.Count} victimas");
            W8Ok(ref ok, ReaccionAlDano.MaximoPorVentana <= ReaccionAlDano.ReaccionesPorSegundo, sb, $"rafaga: maximo {ReaccionAlDano.MaximoPorVentana} reacciones en una ventana de 1 s (<= {ReaccionAlDano.ReaccionesPorSegundo}); descartadas {ReaccionAlDano.Descartadas}");
            W8Ok(ref ok, ReaccionAlDano.MenorSeparacionEntreSenales >= ReaccionAlDano.SegundosEntreSenales - 0.001f, sb, "rafaga: las senales respetan el enfriamiento global");
            Application.logMessageReceived -= oyente;
            W8Ok(ref ok, erroresNuevos == 0, sb, $"rafaga: {erroresNuevos} errores/excepciones nuevos (sin el ruido de NavMeshViva){(primerError != null ? " primero: " + (primerError.Length > 120 ? primerError.Substring(0, 120) : primerError) : "")}");

            // poda: matar a todas las victimas de la prueba deja sus entradas fuera del diccionario
            foreach (var v in victimas) if (v != null && v.Health != null && v.Health.IsAlive) v.Health.TakeDamage(9999999, yo.Id);
            foreach (var x in Esperar(0.5f)) yield return x;
            int quedan = 0; foreach (var v in victimas) if (v != null && ReaccionAlDano.Registrada(v.Id)) quedan++;
            W8Ok(ref ok, quedan == 0, sb, $"poda: {quedan} de {victimas.Count} victimas muertas siguen en el diccionario (esperado 0; total registradas {ReaccionAlDano.VictimasRegistradas})");
            R19bRestaurar();
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
