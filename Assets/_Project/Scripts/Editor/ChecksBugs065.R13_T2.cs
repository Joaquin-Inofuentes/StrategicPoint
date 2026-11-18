using System.Collections;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ronda 13, T2 (decision D4): el lanzacohetes mata al helicoptero jefe de la azotea con 4 / 4 / 5 cohetes (Facil / Medio / Dificil), pegue donde pegue.
    //   Bug958a  por cada dificultad: dano mayor que 0 contra la nariz, el centro y la cola (un cohete cada uno, por la MISMA ruta que el arma real:
    //            WeaponHolder.DispararEspecPrueba -> LanzarSegunEspec) y cohetes hasta morir (centro) <= 4 / 4 / 5.
    public static partial class ChecksBugs065
    {
        static readonly Vector3[] R13PuntosDelHeli = { new Vector3(0f, 1.8f, 4f), new Vector3(0f, 1.8f, -1f), new Vector3(0f, 1.8f, -5.5f) };
        static readonly string[] R13NombresDePunto = { "nariz", "centro", "cola" };

        // Un cohete real hacia un punto del heli, lanzado desde 10 m del lado del jugador (el heli se deja quieto: la medida es de dano, no de punteria).
        static bool R13Cohete(Soldier yo, Vector3 punto)
        {
            var wh = yo.Weapon; if (wh == null) return false;
            var spec = WeaponCatalog.Get(WeaponKind.Rocket);
            if (!wh.Loadout.Contains(WeaponKind.Rocket)) wh.Loadout.Add(WeaponKind.Rocket);
            if (wh.CurrentWeaponKind != WeaponKind.Rocket) wh.EquipWeapon(WeaponKind.Rocket, spec.Damage, spec.Cooldown, spec.Color);
            wh.FijarReservaParaPrueba(WeaponKind.Rocket, 99); wh.RestaurarMunicion(1);
            var lado = yo.transform.position - punto; lado.y = 0f; if (lado.sqrMagnitude < 0.01f) lado = Vector3.right; lado.Normalize();
            var origen = punto + lado * 10f + Vector3.up * 0.3f;
            float vel = Projectile.VelocidadBase * spec.ProjectileSpeed;
            var dir = WeaponHolder.DireccionBalistica(origen, punto, vel, spec.ProjectileGravity, (punto - origen).normalized);
            return wh.DispararEspecPrueba(spec, origen, dir);
        }

        static IEnumerator Bug958a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.0f)) yield return x;
            var d = OperacionDirector.Instancia;
            bool diosAntes = ModoDios.Activo; var difAntes = Dificultad.Actual; bool difActivaAntes = Dificultad.Activa;
            ModoDios.Poner(false); Dificultad.Activa = true;
            AjustesDeDano.ForzarParaPrueba(AjustesDeDano.PropioPorDefecto, AjustesDeDano.EnemigoPorDefecto);
            Soldier yo = null; bool invAntes = false;
            var limites = new[] { 4, 4, 5 };
            try
            {
                int i = 0;
                foreach (var dif in new[] { NivelDificultad.Facil, NivelDificultad.Medio, NivelDificultad.Dificil })
                {
                    Dificultad.Actual = dif; string dn = dif.ToString();
                    if (d.HeliDeLaAzotea == null || d.HeliDeLaAzotea.Derribado || d.HeliDeLaAzotea.Vehiculo == null || d.HeliDeLaAzotea.Vehiculo.IsDestroyed) d.SaltarALaAzotea();
                    float t0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t0 < 40f && !(d.HeliDeLaAzotea != null && d.HeliDeLaAzotea.Vehiculo != null && d.HeliDeLaAzotea.Vehiculo.Health != null && !d.HeliDeLaAzotea.Vehiculo.Health.Invulnerable && !d.HeliDeLaAzotea.Derribado)) yield return null;
                    var heli = d.HeliDeLaAzotea;
                    if (!W8Ok(ref ok, heli != null && heli.Vehiculo != null && !heli.Vehiculo.Health.Invulnerable, sb, $"[{dn}] el heli de la azotea queda vulnerable")) { i++; continue; }
                    yo = Poseido(); if (yo == null) { Fin("FALLO no hay poseido"); yield break; }
                    if (i == 0) invAntes = yo.Health.Invulnerable;
                    yo.Health.Invulnerable = true;                 // el tiroteo del techo no mata al que prueba
                    var hp = heli.Vehiculo.Health; var vt = heli.Vehiculo.transform;
                    heli.enabled = false;                          // quieto: se mide el dano, no la punteria
                    Physics.SyncTransforms();

                    // dano por punto (un cohete, vida restaurada)
                    var sbD = new StringBuilder(); bool todosMayorCero = true;
                    for (int p = 0; p < R13PuntosDelHeli.Length; p++)
                    {
                        hp.RestaurarVida(hp.MaxHealth); yield return null;
                        int v0 = hp.Current;
                        R13Cohete(yo, vt.TransformPoint(R13PuntosDelHeli[p]));
                        float tt = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - tt < 1.2f && hp.Current == v0) yield return null;
                        foreach (var x in Esperar(0.2f)) yield return x;
                        int dano = v0 - hp.Current;
                        if (dano <= 0) todosMayorCero = false;
                        sbD.Append(R13NombresDePunto[p]).Append('=').Append(dano).Append(' ');
                    }
                    W8Ok(ref ok, todosMayorCero, sb, $"[{dn}] dano > 0 en nariz, centro y cola ({sbD.ToString().Trim()})");

                    // cohetes hasta morir (centro)
                    hp.RestaurarVida(hp.MaxHealth); yield return null;
                    int usados = 0; int max = hp.MaxHealth;
                    for (int n = 1; n <= 12 && hp.IsAlive; n++)
                    {
                        int v0 = hp.Current;
                        R13Cohete(yo, vt.TransformPoint(R13PuntosDelHeli[1])); usados++;
                        float tt = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - tt < 1.2f && hp.Current == v0) yield return null;
                        foreach (var x in Esperar(0.15f)) yield return x;
                    }
                    W8Ok(ref ok, !hp.IsAlive && usados <= limites[i], sb, $"[{dn}] cohetes hasta morir {usados} (tope {limites[i]}, vida {max}, quedan {hp.Current})");
                    i++;
                }
            }
            finally
            {
                if (yo != null) yo.Health.Invulnerable = invAntes;
                AjustesDeDano.Liberar(); Dificultad.Actual = difAntes; Dificultad.Activa = difActivaAntes; ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
