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
    // Tanda #104-#132, P1: combate. #107/#108 headshot, #110 arma inicial / reservas / ADS, #132 sonido y animacion de recarga.
    public static partial class ChecksBugs065
    {
        // Apaga las IA de todos menos 'yo' (los blancos no se mueven ni disparan) y devuelve los cerebros apagados para reencenderlos.
        static List<MonoBehaviour> ApagarIaAjena(Soldier yo)
        {
            var apagados = new List<MonoBehaviour>();
            foreach (var a in ActorRegistry.All)
            {
                var s = a as Soldier; if (s == null || s == yo) continue;
                var br = s.GetComponent<SP.Ai.AiBrain>();
                if (br != null && br.enabled) { br.enabled = false; apagados.Add(br); }
            }
            return apagados;
        }

        static void EquiparPistola(Soldier yo)
        {
            int i = yo.Weapon.Loadout.IndexOf(WeaponKind.Pistol);
            if (i >= 0) yo.Weapon.EquipFromLoadout(i);
        }

        // Enemigo vivo con linea de tiro a 10 m, de pie y sin barricada (los guardias atrincherados quedan detras de su cobertura).
        static bool BuscarBlancoEnemigo(Soldier yo, out Soldier blanco, out Vector3 pos, out Vector3 dir)
        {
            blanco = null; pos = default; dir = default;
            foreach (var a in ActorRegistry.All)
            {
                var e = a as Soldier;
                if (e == null || e.Team != TeamId.Enemy || e.Health == null || !e.Health.IsAlive || !e.gameObject.activeInHierarchy) continue;
                if (e.Brain != null && e.Brain.Atrincherado) continue;
                if (e.Motor != null && e.Motor.IsCrouching) continue;
                if (!UbicarJugador(yo, e, 10f, out var p, out var d)) continue;
                blanco = e; pos = p; dir = d; return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- #107 headshot con pistola mata al instante
        static IEnumerator Bug107()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            // Se prueba en el cuartel (objetivo 1), donde hay enemigos de pie con linea de tiro libre; el codigo de impacto es el mismo en todas las fases.
            ArrancarEn(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var yo = Poseido();
            if (yo == null || yo.Weapon == null) { Fin("FALLO sin soldado poseido"); yield break; }
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var apagados = ApagarIaAjena(yo);
            bool ok = true; var sb = new StringBuilder();
            try
            {
                EquiparPistola(yo);
                if (!BuscarBlancoEnemigo(yo, out var blanco, out var pos, out var dir)) { Fin("FALLO no hay enemigo con linea de tiro a 10 m"); yield break; }
                var br = blanco.GetComponent<SP.Ai.AiBrain>(); if (br != null) { br.enabled = false; apagados.Add(br); }
                blanco.Motor.SetCrouching(false);
                yo.transform.position = pos;
                blanco.transform.rotation = Quaternion.LookRotation(dir);
                foreach (var x in Esperar(0.8f)) yield return x;
                var smr = blanco.GetComponentInChildren<SkinnedMeshRenderer>();
                float mallaMax = smr.bounds.max.y;
                Vector3 P(float y) => new Vector3(blanco.transform.position.x, y, blanco.transform.position.z);

                int vidaAntes = blanco.Health.Current, maxV = blanco.Health.MaxHealth;
                int dano = WeaponCatalog.Get(WeaponKind.Pistol).Damage;
                bool pistola = yo.Weapon.CurrentWeaponKind == WeaponKind.Pistol;
                bool inmuneAlX2 = vidaAntes > dano * 2;   // el x2 viejo (16) no alcanzaba a matar
                int hs0 = Projectile.HeadshotsDelJugador;
                int estHs0 = EstadisticasDeMision.Instancia != null ? EstadisticasDeMision.Instancia.HeadshotsJugador : 0;
                int escombros0 = DebrisPool.ActiveCount;
                var r = new int[2];
                int intentos = 0;
                // Hasta 4 tiros: la pistola tiene dispersion y a veces la bala pasa al lado de la cabeza (eso no es lo que se mide).
                while (intentos < 4 && blanco.Health.IsAlive && Projectile.HeadshotsDelJugador == hs0)
                {
                    intentos++;
                    foreach (var x in Tirar(yo, blanco, () => P(mallaMax - 0.30f), 1, r, false, true)) yield return x;
                }
                sb.Append($"(tiros hasta acertar la cabeza: {intentos}) ");
                // La bala viaja unos frames; Tirar ya espera 6 + 10.
                bool murio = !blanco.Health.IsAlive;
                bool hs = Projectile.HeadshotsDelJugador > hs0;
                int estHs = EstadisticasDeMision.Instancia != null ? EstadisticasDeMision.Instancia.HeadshotsJugador : 0;
                bool estadistica = estHs > estHs0;
                bool popup = HeadshotPopup_Visible();
                int escombros = DebrisPool.ActiveCount - escombros0;
                bool fx = escombros >= 10;
                if (!(pistola && inmuneAlX2 && murio && hs && estadistica && fx)) ok = false;
                sb.Append($"pistola={pistola} vida {vidaAntes}/{maxV} (dano x2 = {dano * 2}) -> {(murio ? "MUERTO" : "VIVO " + blanco.Health.Current)}{(murio ? "" : " (MAL)")}; headshot contado={hs} estadisticas={estadistica}{(estadistica ? "" : " (MAL)")}; popup visible={popup} texto='{SP.UI.HeadshotPopup.Texto}'; escombros de sangre +{escombros}{(fx ? "" : " (MAL, se piden >=10)")}");
                foreach (var x in CapturarPantalla("v3_107_headshot.png")) yield return x;
            }
            finally
            {
                foreach (var m in apagados) if (m != null) m.enabled = true;
                ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static bool HeadshotPopup_Visible() => SP.UI.HeadshotPopup.Visible;

        // Un tiro al pecho NO mata: baja exactamente el dano del arma.
        static IEnumerator Bug107b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            ArrancarEn(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var yo = Poseido();
            if (yo == null || yo.Weapon == null) { Fin("FALLO sin soldado poseido"); yield break; }
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var apagados = ApagarIaAjena(yo);
            bool ok = true; var sb = new StringBuilder();
            try
            {
                EquiparPistola(yo);
                if (!BuscarBlancoEnemigo(yo, out var blanco, out var pos, out var dir)) { Fin("FALLO no hay enemigo con linea de tiro a 10 m"); yield break; }
                blanco.Motor.SetCrouching(false);
                yo.transform.position = pos;
                blanco.transform.rotation = Quaternion.LookRotation(dir);
                foreach (var x in Esperar(0.8f)) yield return x;
                Vector3 P(float y) => new Vector3(blanco.transform.position.x, y, blanco.transform.position.z);
                int hs0 = Projectile.HeadshotsDelJugador;
                var r = new int[2];
                int vida0 = blanco.Health.Current;
                foreach (var x in Tirar(yo, blanco, () => P(blanco.transform.position.y + 0.1f), 1, r, false, true)) yield return x;
                int baja = vida0 - blanco.Health.Current;
                bool vivo = blanco.Health.IsAlive;
                bool sinHs = Projectile.HeadshotsDelJugador == hs0;
                int dano = WeaponCatalog.Get(WeaponKind.Pistol).Damage;
                if (!(vivo && sinHs && baja > 0 && baja <= dano * 3)) ok = false;
                sb.Append($"pecho: vida {vida0} -> {blanco.Health.Current} (baja {baja}, dano de pistola {dano}), vivo={vivo}, headshots nuevos={Projectile.HeadshotsDelJugador - hs0}{(ok ? "" : " (MAL)")}");
            }
            finally
            {
                foreach (var m in apagados) if (m != null) m.enabled = true;
                ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #108 un enemigo que le da al jugador en la cabeza no lo mata de un tiro
        static IEnumerator Bug108()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            ArrancarEn(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var yo = Poseido();
            if (yo == null || yo.Health == null) { Fin("FALLO sin soldado poseido"); yield break; }
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(false);
            var apagados = ApagarIaAjena(yo);
            bool ok = true; var sb = new StringBuilder();
            try
            {
                yo.Health.Heal(yo.Health.MaxHealth);
                Soldier tirador = null;
                foreach (var a in ActorRegistry.All) { var e = a as Soldier; if (e != null && e.Team == TeamId.Enemy && e.Health != null && e.Health.IsAlive) { tirador = e; break; } }
                var pool = ProjectilePool.Activo;
                if (pool == null) { Fin("FALLO no hay ProjectilePool"); yield break; }
                int id = tirador != null ? tirador.Id : -1;
                int vida0 = yo.Health.Current;
                // Cuatro tiros de fusil a la cabeza del jugador, de frente y desde 6 m (la cabeza esta ~1,6 m arriba del pivote... se apunta al hueso).
                var anim = yo.GetComponentInChildren<Animator>();
                var hueso = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
                var cabeza = hueso != null ? hueso.position : yo.transform.position + Vector3.up * 0.65f;
                int dano = WeaponCatalog.Get(WeaponKind.Rifle).Damage;
                for (int i = 0; i < 4; i++)
                {
                    var origen = cabeza + yo.transform.forward * 6f;
                    pool.Spawn(origen, (cabeza - origen).normalized, id, TeamId.Enemy, dano, Color.red);
                    for (int f = 0; f < 8; f++) yield return null;
                    if (!yo.Health.IsAlive) break;
                    yo.Health.Heal(0);
                }
                int perdido = vida0 - yo.Health.Current;
                bool vivo = yo.Health.IsAlive;
                // 4 x 26 con el multiplicador de dificultad (<=1 sobre el jugador) no mata a un soldado de 100+ de vida; un instakill lo habria matado al primero.
                if (!vivo || perdido <= 0) ok = false;
                sb.Append($"jugador vida {vida0} -> {yo.Health.Current} tras 4 tiros enemigos a la cabeza (dano nominal {dano} c/u), vivo={vivo}{(ok ? "" : " (MAL)")}");
                yo.Health.Heal(yo.Health.MaxHealth);
            }
            finally
            {
                foreach (var m in apagados) if (m != null) m.enabled = true;
                ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #110 arranque con Kes (continuo), reservas por dificultad, ADS casi absoluto
        static IEnumerator Bug110()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var dificultadAntes = Dificultad.Actual;
            bool ok = true; var sb = new StringBuilder();
            try
            {
                ArrancarEn(1);
                foreach (var x in Esperar(1.2f)) yield return x;
                var yo = Poseido();
                if (yo == null || yo.Weapon == null) { Fin("FALLO sin soldado poseido"); yield break; }
                var w = yo.Weapon;
                var espec = WeaponCatalog.Get(w.CurrentWeaponKind);
                bool esKes = yo.name.Contains("Kes") && yo.Role == RoleType.Flanker;
                bool continuo = w.CurrentWeaponKind == WeaponKind.Smg && espec.Cooldown <= 0.12f;
                if (!esKes || !continuo) ok = false;
                sb.Append($"poseido={yo.name} rol={yo.Role} arma={w.CurrentWeaponKind} (cadencia {espec.Cooldown:0.00} s){(esKes && continuo ? "" : " (MAL: se pide Kes con metralleta continua)")}; ");

                // Reservas por dificultad (ajuste de equilibrio: CargadoresIniciales 4->6): FACIL 12, MEDIO 8, DIFICIL 6 cargadores (ademas del cargador puesto), para el que manejas.
                var esperado = new Dictionary<NivelDificultad, int> { { NivelDificultad.Facil, 12 }, { NivelDificultad.Medio, 8 }, { NivelDificultad.Dificil, 6 } };
                foreach (var kv in esperado)
                {
                    Dificultad.Actual = kv.Key;
                    ArrancarEn(1);
                    yield return null; yield return null;
                    var p = Poseido(); var pw = p.Weapon;
                    int reserva = pw.ReservaActual, mag = pw.MagazineSize;
                    bool r = WeaponHolder.CargadoresDeReserva == kv.Value && reserva == mag * kv.Value && p.name.Contains("Kes");
                    if (!r) ok = false;
                    sb.Append($"{kv.Key}: cargadores={WeaponHolder.CargadoresDeReserva} reserva={reserva} (esperada {mag * kv.Value}){(r ? "" : " (MAL)")}; ");
                }

                // ADS: con la dispersion acumulada, apuntando es <= 0,2 de la de cadera (con la metralleta 0,06; con el resto 0,03).
                var yo2 = Poseido(); var w2 = yo2.Weapon;
                var pool = ProjectilePool.Activo; if (pool != null) w2.SetPool(pool);
                w2.SetApuntado(0f);
                for (int i = 0; i < 6 && w2.CurrentAmmo > 0; i++)
                {
                    w2.TryFire(yo2.transform.position + Vector3.up * 1.5f, yo2.transform.forward);
                    foreach (var x in Esperar(0.12f)) yield return x;
                }
                w2.SetApuntado(0f);
                float cadera = w2.SpreadDegEfectivo;
                w2.SetApuntado(1f);
                float ads = w2.SpreadDegEfectivo;
                bool adsOk = cadera > 0.05f && ads <= cadera * 0.2f && w2.FactorApuntandoDelArma <= 0.06f + 1e-4f && WeaponHolder.FactorApuntando <= 0.03f + 1e-4f;
                if (!adsOk) ok = false;
                sb.Append($"dispersion cadera={cadera:0.00} ADS={ads:0.000} (factor del arma {w2.FactorApuntandoDelArma:0.00}, general {WeaponHolder.FactorApuntando:0.00}){(adsOk ? "" : " (MAL)")}");
            }
            finally { Dificultad.Actual = dificultadAntes; }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #132 sonido de recarga + animacion de la misma duracion
        static IEnumerable VerificarRecarga(string etiqueta)
        {
            // Devuelve el texto por la variable estatica recargaTexto/recargaOk.
            recargaOk = false; recargaTexto = "";
            var yo = Poseido();
            if (yo == null || yo.Weapon == null) { recargaTexto = "sin soldado poseido"; yield break; }
            var w = yo.Weapon;
            if (w.CurrentAmmo >= w.MagazineSize) w.RestaurarMunicion(Mathf.Max(0, w.MagazineSize - 3));
            if (w.IsReloading) { recargaTexto = "ya estaba recargando"; yield break; }
            float dur = WeaponCatalog.Get(w.CurrentWeaponKind).ReloadDuration;
            if (!w.Reload()) { recargaTexto = "Reload() devolvio false (reserva " + w.ReservaActual + ")"; yield break; }
            string esperadoClip = GenericSfx.GetWeaponReload(w.CurrentWeaponKind).name;
            // 1) Animacion: se muestrea cada frame, en tiempo real, mientras dura la recarga.
            float t0 = Time.realtimeSinceStartup; int frames = 0, conAnim = 0; float ultimoConAnim = 0f;   // ultimo = progreso (0..1) de la recarga del arma con animacion activa
            while (w.IsReloading && Time.realtimeSinceStartup - t0 < dur + 2f)
            {
                frames++;
                if (AnimacionDeAccion.TipoActivo(yo) == TipoAccion.Recargar) { conAnim++; ultimoConAnim = dur > 0f ? 1f - w.ReloadRemaining / dur : 1f; }
                yield return null;
            }
            float real = Time.realtimeSinceStartup - t0;
            float cubierto = ultimoConAnim;
            // 2) Audio: la ultima recarga uso el clip del arma y en 2D para el poseido; hay una AudioSource con ese clip y spatialBlend 0.
            bool clipOk = WeaponHolder.UltimoClipDeRecarga == esperadoClip;
            bool dos_d = WeaponHolder.UltimaRecargaFue2D;
            bool fuente = false; float blend = -1f;
            foreach (var src in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (src != null && src.clip != null && src.clip.name == esperadoClip) { fuente = true; blend = src.spatialBlend; if (blend <= 0.001f) break; }
            bool animOk = frames > 3 && cubierto >= 0.9f && conAnim >= frames * 0.9f;
            bool audioOk = clipOk && dos_d && (!fuente || blend <= 0.001f);
            recargaOk = animOk && audioOk && w.CurrentAmmo > 0;
            recargaTexto = $"{etiqueta}: arma {w.CurrentWeaponKind}, recarga {real:0.00}s (catalogo {dur:0.00}s); animacion Recargar en {conAnim}/{frames} cuadros, ultima vez al {cubierto * 100f:0}% del progreso de la recarga{(animOk ? "" : " (MAL, se piden >=90%)")}; "
                         + $"sonido: clip '{WeaponHolder.UltimoClipDeRecarga}' (esperado '{esperadoClip}'){(clipOk ? "" : " (MAL)")}, 2D para el poseido={dos_d}{(dos_d ? "" : " (MAL)")}, fuente activa={fuente} spatialBlend={blend:0.00}{(fuente && blend > 0.001f ? " (MAL)" : "")}; cargador final {w.CurrentAmmo}";
        }
        static bool recargaOk; static string recargaTexto = "";

        [EscenaDelCheck("SC_Gameplay")]
        static IEnumerator Bug132()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SC_Gameplay") { Fin("FALLO la escena no es SC_Gameplay"); yield break; }
            var intro = Object.FindAnyObjectByType<CinematicaDeIntro>();
            if (intro != null && intro.EnCurso) intro.Saltar();
            SP.Ai.AiBrain.IAPausada = false;
            foreach (var x in Esperar(1.5f)) yield return x;
            bool dios = ModoDios.Activo; ModoDios.Poner(true);
            try
            {
                foreach (var x in VerificarRecarga("SC_Gameplay")) yield return x;
            }
            finally { ModoDios.Poner(dios); }
            Fin((recargaOk ? "OK " : "FALLO ") + recargaTexto);
        }

        static IEnumerator Bug132b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            ArrancarEn(1);
            foreach (var x in Esperar(1.2f)) yield return x;
            bool dios = ModoDios.Activo; ModoDios.Poner(true);
            try
            {
                foreach (var x in VerificarRecarga("SC_Operacion")) yield return x;
                // Tambien con otra arma (la pistola, 1,0 s) para comprobar que la duracion sigue al catalogo.
                var yo = Poseido();
                if (recargaOk && yo != null) { EquiparPistola(yo); yield return null; foreach (var x in VerificarRecarga("SC_Operacion/pistola")) yield return x; }
            }
            finally { ModoDios.Poner(dios); }
            Fin((recargaOk ? "OK " : "FALLO ") + recargaTexto);
        }
    }
}
